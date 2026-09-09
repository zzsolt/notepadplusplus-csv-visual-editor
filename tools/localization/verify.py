"""Fail closed on missing/stale translations, format changes, and new UI literals."""
from __future__ import annotations
from pathlib import Path
import argparse
import hashlib
import json
import re
import subprocess
import sys
import urllib.request
from csharp_strings import tokens

ROOT = Path(__file__).resolve().parents[2]
RESOURCE = ROOT / 'src/CsvVisualEditor.Localization'


def unique_object(pairs):
    result = {}
    for key, value in pairs:
        if key in result:raise ValueError('Duplicate JSON key: ' + key)
        result[key] = value
    return result


def read(path):
    return json.loads(path.read_text(encoding='utf-8'), object_pairs_hook=unique_object)


def placeholders(text: str) -> list[str]:
    parts=[];index=0
    while index<len(text):
        if text[index:index+2] in ('{{','}}'):index+=2;continue
        if text[index]=='{':
            match=re.match(r'\{[0-9]+(?:,-?[0-9]+)?(?::[^{}]+)?\}',text[index:])
            if not match:raise ValueError('Malformed format placeholder')
            parts.append(match[0]);index+=len(match[0]);continue
        if text[index]=='}':raise ValueError('Unbalanced closing brace')
        index+=1
    return sorted(parts)


def literal_inventory() -> dict:
    found={}
    for path in sorted((ROOT/'src/CsvVisualEditor').rglob('*.cs')):
        if any(part in ('obj','bin') for part in path.parts):continue
        values=sorted({token.text for token in tokens(path.read_text(encoding='utf-8')) if re.search('[A-Za-z]{2}',token.text)})
        if values:found[path.relative_to(ROOT/'src/CsvVisualEditor').as_posix()]=values
    return found


def verify(check_upstream=False, english_only=False) -> None:
    errors=[]
    inventory=read(RESOURCE/'languages.json')
    entries=inventory['languages']
    codes={entry['code'] for entry in entries}
    files=[entry['file'] for entry in entries]
    if len(files)!=len(set(files)):errors.append('Duplicate host language filenames')
    if 'en' not in codes:errors.append('English fallback missing')
    source=read(RESOURCE/'Catalogs/en.json')['messages']
    for key,entry in source.items():
        if entry['sourceHash']!=hashlib.sha256(entry['text'].encode()).hexdigest():errors.append('Stale English source hash: '+key)
        if not entry.get('context'):errors.append('Missing translator context: '+key)
        try:placeholders(entry['text'])
        except ValueError as error:errors.append(key+': '+str(error))
    expected={'en'} if english_only else codes
    present={path.stem for path in (RESOURCE/'Catalogs').glob('*.json')}
    if not english_only and present!=expected:errors.append('Catalog inventory mismatch: missing='+str(sorted(expected-present))+' extra='+str(sorted(present-expected)))
    for code in sorted(expected):
        path=RESOURCE/'Catalogs'/(code+'.json')
        if not path.exists():continue
        target=read(path)
        if target['language']!=code:errors.append('Wrong catalog identity: '+code)
        messages=target['messages']
        if set(messages)!=set(source):errors.append('Key coverage mismatch: '+code)
        for key,entry in messages.items():
            if key not in source:continue
            text=entry.get('text','')
            if not isinstance(text,str) or not text.strip():errors.append(code+'/'+key+': empty text');continue
            if entry.get('sourceHash')!=source[key]['sourceHash']:errors.append(code+'/'+key+': stale translation')
            try:
                if placeholders(text)!=placeholders(source[key]['text']):errors.append(code+'/'+key+': changed placeholders')
            except ValueError as error:errors.append(code+'/'+key+': '+str(error))
            if code != 'en' and len(source[key]['text']) > 80 and text == source[key]['text']:
                errors.append(code+'/'+key+': untranslated prose')
            if re.search(r'([^\W\d_]{2,32})\1{3,}',text,re.I) or re.search(r'(\b\w+(?:\s+\w+){0,6}\s+)\1{3,}',text,re.I):
                errors.append(code+'/'+key+': repeated translation output')
            if re.search(r'<0x[0-9A-Fa-f]+>',text):errors.append(code+'/'+key+': undecoded byte token')
            if len(text) > max(250,5*len(source[key]['text'])):errors.append(code+'/'+key+': excessive translated length')
            if '-12.5' in source[key]['text'] and '-12.5' not in text:errors.append(code+'/'+key+': altered numeric syntax example')
            if any(0xD800<=ord(char)<=0xDFFF or ord(char)<32 and char not in '\n\r\t' for char in text):errors.append(code+'/'+key+': invalid Unicode/control character')
        # Entire English copies are not accepted as translations. Shared technical
        # words are legitimate, but a non-English catalog must translate its prose.
        if code!='en':
            prose=[key for key,entry in source.items() if len(entry['text'])>45]
            if prose and all(messages.get(key,{}).get('text')==source[key]['text'] for key in prose):errors.append(code+': untranslated English catalog')
    actual=literal_inventory();allowed=read(RESOURCE/'raw-literals.json')['files']
    if actual!=allowed:
        for name in sorted(set(actual)|set(allowed)):
            new=set(actual.get(name,[]))-set(allowed.get(name,[]))
            removed=set(allowed.get(name,[]))-set(actual.get(name,[]))
            if new or removed:errors.append('Review untranslated literals in '+name+': new='+str(sorted(new))+' removed='+str(sorted(removed)))
    subprocess.run([sys.executable,str(ROOT/'tools/localization/generate.py'),'--check'],check=True)
    if check_upstream:
        url='https://raw.githubusercontent.com/notepad-plus-plus/notepad-plus-plus/master/PowerEditor/src/localizationString.h'
        request=urllib.request.Request(url,headers={'User-Agent':'CsvVisualEditor-localization-check'})
        with urllib.request.urlopen(request,timeout=30) as response:header=response.read().decode('utf-8-sig')
        upstream=set(re.findall(r'\{L"[^"]+",\s*L"([^"]+)"\}',header))
        if not upstream:errors.append('Official language inventory could not be parsed')
        if upstream!=set(files):errors.append('Official language inventory changed: add='+str(sorted(upstream-set(files)))+' removed='+str(sorted(set(files)-upstream)))
    if errors:raise SystemExit('\n'.join(errors))
    print('Localization checks passed:',len(expected),'catalogs,',len(source),'keys;',len(files),'Notepad++ options.')


if __name__=='__main__':
    parser=argparse.ArgumentParser();parser.add_argument('--check-upstream',action='store_true')
    parser.add_argument('--english-only',action='store_true',help='Bootstrap validation only; release CI never uses this option.')
    args=parser.parse_args();verify(args.check_upstream,args.english_only)
