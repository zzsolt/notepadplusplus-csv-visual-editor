from __future__ import annotations

from copy import deepcopy
from pathlib import Path
import argparse
import csv
import json
import re

PARAM = re.compile(r'\{[0-9]+(?:,-?[0-9]+)?(?::[^{}]+)?\}')
PROTECTED = re.compile(
    r'\{[0-9]+(?:,-?[0-9]+)?(?::[^{}]+)?\}'
    r'|CSV Visual Editor|Notepad\+\+|Windows-1250|Windows|UTF-16|UTF-8|GitHub|Scintilla|DataGridView'
    r'|Native AOT|WinForms|\.NET|\bCSV\b|\bDLL\b|\bJSON\b|\bXML\b|\bx64\b|\bCRLF\b|\bLF\b'
    r'|Ctrl\+[A-Z]|Shift\+F3|Shift\+Enter|\bF3\b|https?://\S+|[\w.+-]+@[\w.-]+',
    re.IGNORECASE,
)

SOURCE_ALIASES = {
    'table': {'table'},
    'column': {'column'},
    'value': {'value'},
    'rows': {'rows'},
    'preview': {'preview'},
    'number': {'number'},
    'source': {'source'},
    'filter-sort': {'filter & sort', 'filter and sort', 'filter / sort', 'filter/sort'},
    'summary': {'column summary', 'summary'},
    'spaces': {'show spaces'},
    'reset-view': {'reset view'},
    'about': {'about'},
}


def read_json(path: Path):
    return json.loads(path.read_text(encoding='utf-8'))


def write_json(path: Path, value):
    path.write_text(json.dumps(value, ensure_ascii=False, indent=2) + '\n', encoding='utf-8', newline='\n')


def normalize_label(text: str) -> str:
    return re.sub(r'\s+', ' ', text.replace('&', '')).strip().casefold()


def read_glossaries(paths: list[Path]) -> dict[str, dict[str, str]]:
    result: dict[str, dict[str, str]] = {}
    for path in paths:
        lines = path.read_text(encoding='utf-8').splitlines()
        if not lines:
            continue
        header = lines[0].lstrip('# ').split('|')
        if not header or header[0] != 'code':
            raise ValueError(f'{path}: invalid glossary header')
        for row in csv.DictReader(lines[1:], fieldnames=header, delimiter='|'):
            code = (row.get('code') or '').strip()
            if not code:
                continue
            if code in result:
                raise ValueError(f'duplicate glossary code: {code}')
            result[code] = {key: (value or '').strip() for key, value in row.items() if key != 'code'}
    return result


def insert_mnemonic(text: str) -> str:
    i = next((i for i, char in enumerate(text) if char.isalpha()), None)
    return text if i is None else text[:i] + '&' + text[i:]


def apply_glossary(catalog: dict, source: dict, terms: dict[str, str] | None) -> None:
    if not terms:
        return
    reverse = {}
    for field, aliases in SOURCE_ALIASES.items():
        for alias in aliases:
            reverse[alias] = field
    for key, source_entry in source.items():
        field = reverse.get(normalize_label(source_entry['text']))
        if not field or not terms.get(field):
            continue
        value = terms[field]
        if source_entry['text'].startswith('&'):
            value = insert_mnemonic(value.replace('&', ''))
        catalog['messages'][key]['text'] = value


def transform_unprotected(text: str, transform) -> str:
    parts = []
    pos = 0
    for match in PROTECTED.finditer(text):
        parts.append(transform(text[pos:match.start()]))
        parts.append(match.group(0))
        pos = match.end()
    parts.append(transform(text[pos:]))
    return ''.join(parts)


SR_CYR_TO_LAT = {
    'а':'a','б':'b','в':'v','г':'g','д':'d','ђ':'đ','е':'e','ж':'ž','з':'z','и':'i','ј':'j','к':'k','л':'l','љ':'lj',
    'м':'m','н':'n','њ':'nj','о':'o','п':'p','р':'r','с':'s','т':'t','ћ':'ć','у':'u','ф':'f','х':'h','ц':'c','ч':'č','џ':'dž','ш':'š',
}
SR_CYR_TO_LAT.update({k.upper(): (v.upper() if len(v) == 1 else v[0].upper() + v[1:]) for k, v in list(SR_CYR_TO_LAT.items())})
SR_LAT_DIGRAPHS = {
    'dž':'џ','Dž':'Џ','DŽ':'Џ','lj':'љ','Lj':'Љ','LJ':'Љ','nj':'њ','Nj':'Њ','NJ':'Њ',
}
SR_LAT_TO_CYR = {
    'a':'а','b':'б','v':'в','g':'г','d':'д','đ':'ђ','e':'е','ž':'ж','z':'з','i':'и','j':'ј','k':'к','l':'л','m':'м','n':'н',
    'o':'о','p':'п','r':'р','s':'с','t':'т','ć':'ћ','u':'у','f':'ф','h':'х','c':'ц','č':'ч','š':'ш',
}
SR_LAT_TO_CYR.update({k.upper(): v.upper() for k, v in list(SR_LAT_TO_CYR.items())})


def sr_to_latin(text: str) -> str:
    return ''.join(SR_CYR_TO_LAT.get(ch, ch) for ch in text)


def sr_to_cyrillic(text: str) -> str:
    for latin, cyr in SR_LAT_DIGRAPHS.items():
        text = text.replace(latin, cyr)
    return ''.join(SR_LAT_TO_CYR.get(ch, ch) for ch in text)


UZ_CYR_TO_LAT = {
    'а':'a','б':'b','в':'v','г':'g','д':'d','е':'e','ё':'yo','ж':'j','з':'z','и':'i','й':'y','к':'k','қ':'q','л':'l','м':'m','н':'n',
    'о':'o','п':'p','р':'r','с':'s','т':'t','у':'u','ў':'oʻ','ф':'f','х':'x','ҳ':'h','ц':'ts','ч':'ch','ш':'sh','ъ':'ʼ','ь':'',
    'э':'e','ю':'yu','я':'ya','ғ':'gʻ',
}
UZ_CYR_TO_LAT.update({k.upper(): ('' if not v else v.upper() if len(v) == 1 else v[0].upper() + v[1:]) for k, v in list(UZ_CYR_TO_LAT.items())})
UZ_LAT_DIGRAPHS = [
    ('Gʻ','Ғ'),('G’','Ғ'),("G'",'Ғ'),('gʻ','ғ'),('g’','ғ'),("g'",'ғ'),
    ('Oʻ','Ў'),('O’','Ў'),("O'",'Ў'),('oʻ','ў'),('o’','ў'),("o'",'ў'),
    ('Sh','Ш'),('SH','Ш'),('sh','ш'),('Ch','Ч'),('CH','Ч'),('ch','ч'),
    ('Yo','Ё'),('YO','Ё'),('yo','ё'),('Yu','Ю'),('YU','Ю'),('yu','ю'),('Ya','Я'),('YA','Я'),('ya','я'),
    ('Ts','Ц'),('TS','Ц'),('ts','ц'),
]
UZ_LAT_TO_CYR = {
    'a':'а','b':'б','v':'в','g':'г','d':'д','e':'е','j':'ж','z':'з','i':'и','y':'й','k':'к','q':'қ','l':'л','m':'м','n':'н',
    'o':'о','p':'п','r':'р','s':'с','t':'т','u':'у','f':'ф','x':'х','h':'ҳ',
}
UZ_LAT_TO_CYR.update({k.upper(): v.upper() for k, v in list(UZ_LAT_TO_CYR.items())})


def uz_to_latin(text: str) -> str:
    return ''.join(UZ_CYR_TO_LAT.get(ch, ch) for ch in text)


def uz_to_cyrillic(text: str) -> str:
    for latin, cyr in UZ_LAT_DIGRAPHS:
        text = text.replace(latin, cyr)
    return ''.join(UZ_LAT_TO_CYR.get(ch, ch) for ch in text)


WORD = re.compile(r"[A-Za-z]+(?:'[A-Za-z]+)?")
VOWELS = set('aeiouAEIOU')


def pig_word(word: str) -> str:
    if len(word) <= 1:
        return word + 'ay'
    upper = word.isupper()
    title = word[0].isupper() and not upper
    raw = word.lower()
    if raw[0] in 'aeiou':
        out = raw + 'way'
    else:
        match = re.match(r'[^aeiouy]+', raw)
        cut = len(match.group(0)) if match else 1
        out = raw[cut:] + raw[:cut] + 'ay'
    if upper:
        return out.upper()
    if title:
        return out[:1].upper() + out[1:]
    return out


def pig_latin(text: str) -> str:
    return WORD.sub(lambda m: pig_word(m.group(0)), text)


VARIANT_REPLACEMENTS = {
    'es-AR': [
        ('Haga clic', 'Hacé clic'), ('haga clic', 'hacé clic'),
        ('Seleccione', 'Seleccioná'), ('seleccione', 'seleccioná'),
        ('Elija', 'Elegí'), ('elija', 'elegí'),
    ],
    'pt-BR': [
        ('Ficheiros', 'Arquivos'), ('ficheiros', 'arquivos'),
        ('Ficheiro', 'Arquivo'), ('ficheiro', 'arquivo'),
        ('Pré-visualização', 'Prévia'), ('pré-visualização', 'prévia'),
        ('Repor', 'Redefinir'), ('repor', 'redefinir'),
        ('Seleccione', 'Selecione'), ('seleccione', 'selecione'),
    ],
    'pt-PT': [
        ('Arquivos', 'Ficheiros'), ('arquivos', 'ficheiros'),
        ('Arquivo', 'Ficheiro'), ('arquivo', 'ficheiro'),
    ],
}


def apply_replacements(text: str, target: str) -> str:
    for old, new in VARIANT_REPLACEMENTS.get(target, []):
        text = text.replace(old, new)
    return text


def transformed_catalog(base: dict, target: str, transform=None) -> dict:
    catalog = deepcopy(base)
    catalog['language'] = target
    for entry in catalog['messages'].values():
        value = entry['text']
        if transform:
            value = transform_unprotected(value, transform)
        entry['text'] = apply_replacements(value, target)
    return catalog


def english_pig_catalog(source_catalog: dict) -> dict:
    messages = {}
    for key, entry in source_catalog['messages'].items():
        messages[key] = {
            'text': transform_unprotected(entry['text'], pig_latin),
            'sourceHash': entry['sourceHash'],
        }
    return {'language': 'la-pig', 'messages': messages}


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument('--model-dir', type=Path, required=True)
    parser.add_argument('--resource', type=Path, required=True)
    parser.add_argument('--glossary', type=Path, action='append', default=[])
    args = parser.parse_args()

    source_catalog = read_json(args.resource / 'Catalogs/en.json')
    source = source_catalog['messages']
    glossaries = read_glossaries(args.glossary)

    catalogs: dict[str, dict] = {}
    for path in sorted(args.model_dir.glob('*.json')):
        if path.name.endswith('.review.json'):
            continue
        value = read_json(path)
        code = value.get('language')
        if not isinstance(code, str):
            raise ValueError(f'{path}: missing language identity')
        if code in catalogs:
            raise ValueError(f'duplicate model catalog: {code}')
        catalogs[code] = value

    # Apply curated terminology to direct model catalogs when a glossary is available.
    for code, catalog in catalogs.items():
        apply_glossary(catalog, source, glossaries.get(code))
        write_json(args.model_dir / f'{code}.json', catalog)

    variants = {
        'pt-PT': ('pt', None),
        'pt-BR': ('pt', None),
        'es-AR': ('es', None),
        'oc-aran': ('oc', None),
        'sr-Latn': ('sr', sr_to_latin),
        'sr-Cyrl': ('sr', sr_to_cyrillic),
        'uz-Latn': ('uz', uz_to_latin),
        'uz-Cyrl': ('uz', uz_to_cyrillic),
    }
    for target, (base, transform) in variants.items():
        if base not in catalogs:
            raise ValueError(f'missing provider catalog {base} for {target}')
        catalog = transformed_catalog(catalogs[base], target, transform)
        apply_glossary(catalog, source, glossaries.get(target))
        write_json(args.model_dir / f'{target}.json', catalog)

    pig = english_pig_catalog(source_catalog)
    apply_glossary(pig, source, glossaries.get('la-pig'))
    write_json(args.model_dir / 'la-pig.json', pig)

    print('Prepared explicit variant catalogs:', ', '.join(sorted((*variants, 'la-pig'))))


if __name__ == '__main__':
    main()
