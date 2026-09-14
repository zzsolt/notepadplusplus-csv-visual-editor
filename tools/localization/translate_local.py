"""Incremental OFFLINE translation drafts; never a release or linguistic-review gate.

Requires a predownloaded CTranslate2 model directory and its tokenizer. One model
instance serves all requested languages. Shipped catalogs are never overwritten;
failed messages remain visibly incomplete and can be resumed. No network calls,
related-language substitution, sentence-fragment fallback or hash laundering.
"""
from __future__ import annotations

import argparse
from collections import Counter
import hashlib
import json
import os
from pathlib import Path
import re
import sys
import tempfile
from typing import Callable, Protocol

from import_catalogs import PROVIDER_NAMES, RESOURCE, fingerprint, validate
from verify import read

MARKER = re.compile(r'\{[0-9]+\}')
PROTECTED = re.compile(
    r'\{\{|\}\}|\{[0-9]+(?:,-?[0-9]+)?(?::[^{}]+)?\}'
    r'|CSV Visual Editor|Notepad\+\+|Windows-1250|UTF-16|UTF-8|GitHub|Scintilla|DataGridView'
    r'|\bCSV\b|Ctrl\+[A-Z]|Shift\+F3|Shift\+Enter|\bF3\b|[+-]?\d+(?:[.,]\d+)*')
LANGUAGE_CODE = re.compile(r'[A-Za-z0-9]+(?:-[A-Za-z0-9]+)*')


class Engine(Protocol):
    def supports(self, language: str) -> bool: ...
    def translate(self, language: str, texts: list[str]) -> list[str]: ...


def protect(text: str) -> tuple[str, dict[str, str]]:
    tokens: dict[str, str] = {}

    def take(match: re.Match[str]) -> str:
        marker = '{' + str(len(tokens)) + '}'
        tokens[marker] = match[0]
        return marker

    return PROTECTED.sub(take, text), tokens


def restore(text: str, tokens: dict[str, str]) -> str:
    # Count dictionary KEYS. Counter(tokens) would treat replacement strings as
    # counts, reject valid sentences, and incorrectly force fragment translation.
    if Counter(MARKER.findall(text)) != Counter(tokens.keys()):
        raise ValueError('Translation changed protected arguments or technical terms')
    return MARKER.sub(lambda match: tokens[match[0]], text)


def needs_update(entry: object, source: dict, code: str, key: str) -> bool:
    try:
        validate({key: entry}, {key: source}, code)
    except (ValueError, TypeError, KeyError):
        return True
    return False


def translate_messages(code: str, source: dict, previous: dict, engine: Engine) -> tuple[dict, dict]:
    messages = {key: dict(value) for key, value in previous.items() if key in source and isinstance(value, dict)}
    todo = [key for key, entry in source.items() if needs_update(messages.get(key), entry, code, key)]
    report = {'requested': len(todo), 'translated': 0, 'invalid': {}}
    if not todo:
        return messages, report
    provider = PROVIDER_NAMES.get(code, code)
    if not engine.supports(provider):
        report['invalid']['language'] = 'No exact-language model support; provide an explicit reviewed catalog'
        return messages, report
    plans: dict[str, list] = {}
    pending: list[str] = []
    for key in todo:
        plan = []
        for line in re.split(r'(\r\n|\r|\n|\t)', source[key]['text']):
            if not line or line.isspace():
                plan.append((line, None, None))
                continue
            prepared, tokens = protect(line)
            if not re.search('[A-Za-z]', MARKER.sub('', prepared)):
                plan.append((line, None, None))
                continue
            plan.append((line, prepared, tokens))
            pending.append(prepared)
        plans[key] = plan
    unique = list(dict.fromkeys(pending))
    translated = engine.translate(provider, unique) if unique else []
    if len(translated) != len(unique) or any(not isinstance(value, str) for value in translated):
        raise ValueError('Model returned an invalid batch')
    lookup = dict(zip(unique, translated))
    for key, plan in plans.items():
        try:
            parts = []
            for original, prepared, tokens in plan:
                if prepared is None:
                    parts.append(original)
                else:
                    value = restore(lookup[prepared], tokens).strip()
                    if not value:
                        raise ValueError('Empty translated sentence')
                    # Preserve resource whitespace and line structure explicitly.
                    parts.append(re.match(r'^\s*', original)[0] + value + re.search(r'\s*$', original)[0])
            candidate = {'text': ''.join(parts), 'sourceHash': source[key]['sourceHash']}
            validate({key: candidate}, {key: source[key]}, code)
            messages[key] = candidate
            report['translated'] += 1
        except (ValueError, KeyError, TypeError) as error:
            # Keep the old sourceHash when a stale entry fails. It cannot become
            # current merely because generation was attempted.
            report['invalid'][key] = str(error)
    return messages, report


class OfflineEngine:
    def __init__(self, directory: Path):
        if not directory.is_dir() or not (directory / 'model.bin').is_file():
            raise ValueError('Supply a predownloaded CTranslate2 model directory containing model.bin')
        os.environ['HF_HUB_OFFLINE'] = '1'
        os.environ['TRANSFORMERS_OFFLINE'] = '1'
        from transformers import AutoTokenizer
        import ctranslate2
        self.tokenizer = AutoTokenizer.from_pretrained(str(directory), local_files_only=True, trust_remote_code=False)
        self.vocabulary = self.tokenizer.get_vocab()
        self.engine = ctranslate2.Translator(str(directory), device='cpu', compute_type='int8',
                                            inter_threads=1, intra_threads=min(4, os.cpu_count() or 1))

    def supports(self, language: str) -> bool:
        return '<2' + language + '>' in self.vocabulary

    def translate(self, language: str, texts: list[str]) -> list[str]:
        if not texts:
            return []
        encoded = [self.tokenizer.convert_ids_to_tokens(self.tokenizer.encode('<2' + language + '> ' + text))
                   for text in texts]
        results = self.engine.translate_batch(encoded, beam_size=2, max_batch_size=16,
                                             max_decoding_length=256, no_repeat_ngram_size=6)
        return [self.tokenizer.decode(self.tokenizer.convert_tokens_to_ids(result.hypotheses[0]),
                                      skip_special_tokens=True).strip() for result in results]


def atomic_json(path: Path, value: dict) -> None:
    payload = (json.dumps(value, ensure_ascii=False, indent=2) + '\n').encode('utf-8', errors='strict')
    handle, name = tempfile.mkstemp(prefix=path.name + '.', suffix='.tmp', dir=path.parent)
    try:
        with os.fdopen(handle, 'wb') as output:
            output.write(payload)
        os.replace(name, path)
    finally:
        Path(name).unlink(missing_ok=True)


def process(resource: Path, output: Path, engine_factory: Callable[[], Engine] | None,
            requested: list[str] | None = None, resume: bool = False) -> dict:
    canonical = (resource / 'Catalogs').resolve()
    if output.resolve() == canonical or canonical in output.resolve().parents:
        raise ValueError('Draft output cannot target shipped catalogs')
    source_catalog = read(canonical / 'en.json')
    source = source_catalog['messages']
    if source_catalog.get('language') != 'en':
        raise ValueError('Invalid English source identity')
    for key, entry in source.items():
        if entry['sourceHash'] != hashlib.sha256(entry['text'].encode('utf-8')).hexdigest():
            raise ValueError('Stale English source hash: ' + key)
    codes = {item['code'] for item in read(resource / 'languages.json')['languages']}
    if 'en' not in codes or any(not LANGUAGE_CODE.fullmatch(code) for code in codes):
        raise ValueError('Invalid language inventory')
    selected = sorted(set(requested) if requested else codes - {'en'})
    if set(selected) - codes or 'en' in selected:
        raise ValueError('Choose known non-English languages')
    if engine_factory is not None and output.exists() and not resume:
        raise ValueError('Output exists; use --resume explicitly or choose a fresh directory')
    report = {'sourceFingerprint': fingerprint(source), 'expectedCatalogs': len(codes),
              'keyCount': len(source), 'languages': {}, 'missingCatalogs': [], 'complete': False,
              'provenance': 'Machine-assisted drafts; no native-speaker review implied'}
    engine = None
    complete_codes = {'en'}
    for code in sorted(codes - {'en'}):
        # A resumed draft takes precedence so reviewed/newer edits are not lost.
        draft_path = output / (code + '.json')
        path = draft_path if draft_path.exists() else canonical / (code + '.json')
        previous = {}
        if path.exists():
            catalog = read(path)
            if catalog.get('language') != code or not isinstance(catalog.get('messages'), dict):
                raise ValueError('Invalid catalog identity/schema: ' + code)
            previous = catalog['messages']
            if set(previous) - set(source):
                raise ValueError('Unknown message keys in ' + code)
        pending = [key for key, entry in source.items() if needs_update(previous.get(key), entry, code, key)]
        detail = {'requested': len(pending), 'translated': 0, 'invalid': {}}
        messages = previous
        if code in selected and pending and engine_factory is not None:
            if engine is None:
                engine = engine_factory()
            messages, detail = translate_messages(code, source, previous, engine)
            output.mkdir(parents=True, exist_ok=True)
            atomic_json(draft_path, {'language': code, 'messages': messages})
        missing = [key for key, entry in source.items() if needs_update(messages.get(key), entry, code, key)]
        detail['pendingKeys'] = missing
        report['languages'][code] = detail
        if missing:
            report['missingCatalogs'].append(code)
        else:
            complete_codes.add(code)
    report['availableCatalogs'] = len(complete_codes)
    report['complete'] = complete_codes == codes
    return report


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--resource', type=Path, default=RESOURCE)
    parser.add_argument('--output', type=Path, required=True)
    parser.add_argument('--report', type=Path, required=True)
    parser.add_argument('--model-dir', type=Path)
    parser.add_argument('--languages', nargs='+')
    parser.add_argument('--resume', action='store_true')
    parser.add_argument('--plan', action='store_true', help='Report changed/missing keys without loading a model')
    args = parser.parse_args()
    try:
        canonical = (args.resource / 'Catalogs').resolve()
        report_path = args.report.resolve()
        if report_path == canonical or canonical in report_path.parents or report_path == args.output.resolve() or args.output.resolve() in report_path.parents:
            raise ValueError('Write the report outside shipped catalogs and the draft output directory')
        if not args.plan and args.model_dir is None:
            raise ValueError('--model-dir is required unless --plan is used')
        factory = None if args.plan else lambda: OfflineEngine(args.model_dir)
        report = process(args.resource, args.output, factory, args.languages, args.resume)
        args.report.parent.mkdir(parents=True, exist_ok=True)
        atomic_json(args.report, report)
    except (OSError, ValueError, KeyError, TypeError, ImportError) as error:
        print(str(error), file=sys.stderr)
        return 1
    print(f"{report['availableCatalogs']}/{report['expectedCatalogs']} catalogs structurally complete. "
          'Drafts require contextual review and the full build gate.')
    return 0 if report['complete'] else 2


if __name__ == '__main__':
    raise SystemExit(main())
