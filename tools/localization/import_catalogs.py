"""Import translation drafts without confusing language coverage with fallback.

Outputs ordinary, editable keyed JSON catalogs. All inputs are validated before
anything is written. Partial staging is explicit and cannot target shipped
Catalogs. Legacy numbered TSV files require an exact English order fingerprint.
No translation service is called by this tool.
"""
from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path
import re
import sys
from typing import Any

from verify import placeholders, read

ROOT = Path(__file__).resolve().parents[2]
RESOURCE = ROOT / 'src/CsvVisualEditor.Localization'
# These map provider identifiers to the same language, not to a related language.
# Regional and script variants require their own catalog or complete curated text.
PROVIDER_NAMES = {'nb': 'no', 'tl': 'fil', 'zh-CN': 'zh', 'zh-TW': 'zh_Hant'}


def fingerprint(messages: dict[str, Any]) -> str:
    """Pin both identity and order before importing old index-based translations."""
    payload = [[key, entry['sourceHash']] for key, entry in messages.items()]
    return hashlib.sha256(json.dumps(payload, ensure_ascii=True,
                                    separators=(',', ':')).encode('utf-8')).hexdigest()


def validate(messages: dict[str, Any], source: dict[str, Any], code: str) -> None:
    if not isinstance(messages, dict) or set(messages) != set(source):
        raise ValueError(f'{code}: translation key coverage differs from English')
    for key, original in source.items():
        entry = messages[key]
        if not isinstance(entry, dict):
            raise ValueError(f'{code}/{key}: invalid message entry')
        text = entry.get('text')
        if not isinstance(text, str) or not text.strip():
            raise ValueError(f'{code}/{key}: empty or invalid text')
        if entry.get('sourceHash') != original['sourceHash']:
            raise ValueError(f'{code}/{key}: stale source hash')
        if placeholders(text) != placeholders(original['text']):
            raise ValueError(f'{code}/{key}: changed format arguments')
        if any(0xD800 <= ord(char) <= 0xDFFF or ord(char) < 32
               and char not in '\n\r\t' for char in text):
            raise ValueError(f'{code}/{key}: invalid Unicode/control character')
        if code != 'en' and len(original['text']) > 80 and text == original['text']:
            raise ValueError(f'{code}/{key}: untranslated prose')
        if re.search(r'<0x[0-9A-Fa-f]+>', text):
            raise ValueError(f'{code}/{key}: undecoded byte token')
        if '-12.5' in original['text'] and '-12.5' not in text:
            raise ValueError(f'{code}/{key}: altered numeric syntax example')
        if len(text) > max(250, 5 * len(original['text'])):
            raise ValueError(f'{code}/{key}: excessive translated length')
        if re.search(r'([^\W\d_]{2,32})\1{3,}', text, re.I) or re.search(
                r'(\b\w+(?:\s+\w+){0,6}\s+)\1{3,}', text, re.I):
            raise ValueError(f'{code}/{key}: repeated translation output')
        # Abkhaz UI prose uses Cyrillic; Han characters indicate a corrupt draft.
        # This is a specific quality guard, not a claim of semantic proofreading.
        if code == 'ab' and re.search(r'[\u3400-\u9fff]', text):
            raise ValueError(f'{code}/{key}: unexpected Han text in Abkhaz catalog')
    if code != 'en':
        prose = [key for key in source if len(source[key]['text']) > 45]
        if prose and all(messages[key]['text'] == source[key]['text'] for key in prose):
            raise ValueError(f'{code}: English copies are not translations')


def decode_tsv_text(value: str) -> str:
    # Decode only the documented escapes. unicode_escape corrupts non-ASCII text.
    def replace(match: re.Match[str]) -> str:
        return {'n': '\n', 'r': '\r', 't': '\t', '\\': '\\'}[match[1]]
    if re.search(r'\\(?![nrt\\])', value):
        raise ValueError('Unsupported escape in legacy translation')
    return re.sub(r'\\([nrt\\])', replace, value)


def read_legacy(folder: Path, code: str, source: dict[str, Any],
                expected_fingerprint: str | None) -> dict[str, Any] | None:
    paths = sorted(folder.glob(code + '-*.tsv'))
    if not paths:
        return None
    if expected_fingerprint != fingerprint(source):
        raise ValueError('Legacy English key-order fingerprint is missing or stale')
    rows: dict[int, str] = {}
    for path in paths:
        for line_number, line in enumerate(path.read_text(encoding='utf-8').splitlines(), 1):
            if not line or line.startswith('#'):
                continue
            parts = line.split('\t', 1)
            if len(parts) != 2 or not re.fullmatch('[0-9]+', parts[0]):
                raise ValueError(f'{path.name}:{line_number}: expected index and TAB')
            index = int(parts[0])
            if index in rows or not 0 <= index < len(source):
                raise ValueError(f'{path.name}:{line_number}: duplicate/out-of-range index')
            rows[index] = decode_tsv_text(parts[1])
    if set(rows) != set(range(len(source))):
        raise ValueError(f'{code}: incomplete legacy translation ({len(rows)}/{len(source)})')
    return {key: {'text': rows[index], 'sourceHash': entry['sourceHash']}
            for index, (key, entry) in enumerate(source.items())}


def assemble(resource: Path, inputs: Path, curated: Path | None = None,
             expected_fingerprint: str | None = None,
             overrides_path: Path | None = None) -> tuple[dict[str, Any], dict[str, Any]]:
    source_catalog = read(resource / 'Catalogs/en.json')
    if source_catalog.get('language') != 'en':
        raise ValueError('Invalid English source identity')
    source = source_catalog['messages']
    for key, entry in source.items():
        if entry['sourceHash'] != hashlib.sha256(entry['text'].encode('utf-8')).hexdigest():
            raise ValueError('Stale English source hash: ' + key)
    inventory = read(resource / 'languages.json')['languages']
    codes = sorted({entry['code'] for entry in inventory})
    if 'en' not in codes:
        raise ValueError('English fallback missing from inventory')
    overrides = read(overrides_path) if overrides_path else {}
    if not isinstance(overrides, dict) or set(overrides) - set(codes):
        raise ValueError('Override contains an unknown catalog language')
    for code, patches in overrides.items():
        if code == 'en':
            raise ValueError('Translation overrides cannot replace the English source')
        if not isinstance(patches, dict) or set(patches) - set(source):
            raise ValueError(f'{code}: override contains unknown message keys')
        for key, entry in patches.items():
            validate({key: entry}, {key: source[key]}, code)
    catalogs = {'en': source_catalog}
    report: dict[str, Any] = {'sourceFingerprint': fingerprint(source),
                             'keyCount': len(source), 'expectedCatalogs': len(codes),
                             'hostOptions': len(inventory), 'imported': {},
                             'missing': [], 'invalid': {}, 'complete': False}
    for code in codes:
        if code == 'en':
            continue
        try:
            messages = read_legacy(curated, code, source, expected_fingerprint) if curated else None
            origin = 'curated-indexed' if messages is not None else 'keyed-catalog'
            if messages is None:
                input_code = code
                path = inputs / (code + '.json')
                if not path.exists() and code in PROVIDER_NAMES:
                    input_code = PROVIDER_NAMES[code]
                    path = inputs / (input_code + '.json')
                if not path.exists():
                    report['missing'].append(code)
                    continue
                draft = read(path)
                if draft.get('language') != input_code:
                    raise ValueError('Catalog identity does not match its filename')
                messages = draft['messages']
            # Overrides retain explicit source hashes; no stale draft is silently
            # marked fresh. Each patch was prevalidated, including its identity.
            messages = {**messages, **overrides.get(code, {})}
            validate(messages, source, code)
            # Canonical ordering and schema: keep text and its original source hash.
            catalogs[code] = {'language': code, 'messages': {
                key: {'text': messages[key]['text'], 'sourceHash': messages[key]['sourceHash']}
                for key in source}}
            report['imported'][code] = origin
        except (ValueError, KeyError, TypeError, OSError) as error:
            report['invalid'][code] = str(error)
    report['availableCatalogs'] = len(catalogs)
    report['complete'] = not report['missing'] and not report['invalid']
    return catalogs, report


def write_output(destination: Path, catalogs: dict[str, Any], report: dict[str, Any],
                 stage: bool = False, resource: Path = RESOURCE) -> None:
    canonical = (resource / 'Catalogs').resolve()
    if stage and (destination.resolve() == canonical or canonical in destination.resolve().parents):
        raise ValueError('Partial staging cannot target shipped Catalogs')
    if not stage and not report['complete']:
        raise ValueError('Incomplete catalog set; no catalogs written. Inspect the coverage report.')
    # Do not delete or merge with an existing directory: that can leave stale files
    # or destroy reviewed catalog edits. A caller promotes reviewed output explicitly.
    if destination.exists():
        raise ValueError('Output directory already exists; choose a fresh staging path')
    encoded = {code: json.dumps(value, ensure_ascii=False, indent=2) + '\n'
               for code, value in catalogs.items()}
    for value in encoded.values():
        value.encode('utf-8', errors='strict')
    destination.mkdir(parents=True)
    for code, value in encoded.items():
        (destination / (code + '.json')).write_text(value, encoding='utf-8', newline='\n')


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--input', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True)
    parser.add_argument('--report', type=Path, required=True)
    parser.add_argument('--curated', type=Path)
    parser.add_argument('--legacy-source-fingerprint')
    parser.add_argument('--overrides', type=Path, help='Explicit source-hashed keyed corrections')
    parser.add_argument('--stage', action='store_true', help='Stage validated drafts; not a release gate')
    args = parser.parse_args()
    try:
        catalogs, report = assemble(RESOURCE, args.input, args.curated, args.legacy_source_fingerprint, args.overrides)
        if args.report.resolve().is_relative_to((RESOURCE / 'Catalogs').resolve()):
            raise ValueError('Coverage reports do not belong in shipped Catalogs')
        if args.report.resolve().is_relative_to(args.output.resolve()):
            raise ValueError('Coverage report must be outside the output catalog directory')
        args.report.parent.mkdir(parents=True, exist_ok=True)
        args.report.write_text(json.dumps(report, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
        write_output(args.output, catalogs, report, args.stage)
    except (OSError, ValueError, KeyError, TypeError) as error:
        print(str(error), file=sys.stderr)
        return 1
    print(f"{report['availableCatalogs']}/{report['expectedCatalogs']} catalogs validated; "
          f"missing={len(report['missing'])}, invalid={len(report['invalid'])}. "
          + ('STAGING ONLY.' if args.stage else 'Ready for review and the full build gate.'))
    # Even staging must not masquerade as a successful complete-language check.
    return 0 if report['complete'] else 2


if __name__ == '__main__':
    raise SystemExit(main())
