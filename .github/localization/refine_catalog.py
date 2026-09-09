"""Refine catalog messages with full-sentence context and checked placeholders."""
from __future__ import annotations
from collections import Counter
from pathlib import Path
import argparse
import json
import re
import time
import unicodedata

ROOT = Path('src/CsvVisualEditor.Localization')
PARAM = re.compile(r'\{\d+(?:,-?\d+)?(?::[^{}]+)?\}')
TOKEN = re.compile(r'\{\d+\}')
TERMS = re.compile(r'CSV Visual Editor|Notepad\+\+|Windows-1250|UTF-16|UTF-8|GitHub|Scintilla|DataGridView|\bCSV\b|Ctrl\+[A-Z]|Shift\+F3|Shift\+Enter|\bF3\b|-12\.5|250,000|10,000|16 Mi|65,001|4,096')
MODEL = 'Nextcloud-AI/madlad400-3b-mt-ct2-int8'
REVISION = 'aa32bbdeba7880eff2096ec044cb155a340a9400'


def repeated(text):
    return bool(re.search(r'([^\W\d_]{2,32})\1{3,}',text,re.I) or re.search(r'(\b\w+(?:\s+\w+){0,6}\s+)\1{3,}',text,re.I))


def invariant(text):
    return not re.search(r'[A-Za-z]',TERMS.sub('',PARAM.sub('',text)))


def protect(text):
    tokens={}
    def take(match):
        marker='{'+str(len(tokens))+'}'
        tokens[marker]=match[0]
        return marker
    clean=PARAM.sub(take,text.replace('&',''))
    clean=TERMS.sub(take,clean)
    return clean.strip(),tokens


def restore(text,tokens):
    text=text.replace('\uff5b','{').replace('\uff5d','}')
    def digits(match):
        return '{'+''.join(str(unicodedata.digit(c)) for c in match[1] if c.isdigit())+'}'
    text=re.sub(r'\{\s*([\d ]+)\s*\}',digits,text)
    if Counter(TOKEN.findall(text))!=Counter(tokens.keys()):return None
    return TOKEN.sub(lambda m:tokens[m[0]],text)


def main(language):
    import ctranslate2
    from transformers import AutoTokenizer
    from huggingface_hub import snapshot_download
    source=json.loads((ROOT/'Catalogs/en.json').read_text(encoding='utf-8'))['messages']
    previous=Path('previous')/(language+'.json')
    result=json.loads(previous.read_text(encoding='utf-8'))['messages'] if previous.exists() else {}
    report_path=previous.with_suffix('.review.json')
    report=json.loads(report_path.read_text(encoding='utf-8')).get('review',{}) if report_path.exists() else {}
    todo=[key for key,entry in source.items() if key not in result or key in report or
          PARAM.search(entry['text']) or TERMS.search(entry['text']) or result[key]['text']==entry['text'] and not invariant(entry['text'])]
    path=snapshot_download(MODEL,revision=REVISION,allow_patterns=['*.json','*.bin','*.model'],ignore_patterns=['pytorch_model*'])
    tokenizer=AutoTokenizer.from_pretrained(path,local_files_only=True,trust_remote_code=False)
    assert f'<2{language}>' in tokenizer.get_vocab()
    engine=ctranslate2.Translator(path,device='cpu',compute_type='int8',inter_threads=1,intra_threads=2)
    cache={}; warnings={}; started=time.monotonic()
    def translate(texts,beam=2):
        unique=list(dict.fromkeys(t for t in texts if t not in cache))
        if unique:
            assert all(not re.search(r'[\r\n\t]',t) for t in unique)
            sequences=[tokenizer.convert_ids_to_tokens(tokenizer.encode(f'<2{language}> '+t)) for t in unique]
            outputs=engine.translate_batch(sequences,beam_size=beam,max_batch_size=24,max_decoding_length=192,no_repeat_ngram_size=6)
            for original,output in zip(unique,outputs):
                value=tokenizer.decode(tokenizer.convert_tokens_to_ids(output.hypotheses[0]),skip_special_tokens=True).strip()
                if not value or repeated(value) or len(value)>max(200,5*len(original)):
                    raise ValueError('Invalid catalog output: '+original)
                cache[original]=value
        return [cache[t] for t in texts]
    plans={}; text_parts=[]
    for key in todo:
        plan=[]
        for part in re.split(r'(\r\n|\n|\r|\t)',source[key]['text']):
            if not part or part.isspace() or invariant(part):plan.append((part,None,None));continue
            clean,tokens=protect(part);plan.append((part,clean,tokens));text_parts.append(clean)
        plans[key]=plan
    translate(text_parts)
    for key,plan in plans.items():
        parts=[]
        for original,clean,tokens in plan:
            if clean is None:parts.append(original);continue
            value=restore(cache[clean],tokens)
            if value is None:
                del cache[clean]
                translate([clean],beam=4)
                value=restore(cache[clean],tokens)
            if value is None:
                # Keep data tokens exact when the model cannot retain a sentence's
                # placeholder structure. Record this for contextual review.
                chunks=re.split(r'(\{\d+\})',clean)
                translate([c.strip() for c in chunks if not TOKEN.fullmatch(c) and re.search(r'[A-Za-z]',c)])
                for i,chunk in enumerate(chunks):
                    if TOKEN.fullmatch(chunk):chunks[i]=tokens[chunk]
                    elif chunk.strip() in cache:
                        chunks[i]=re.match(r'^\s*',chunk)[0]+cache[chunk.strip()]+re.search(r'\s*$',chunk)[0]
                value=''.join(chunks);warnings[key]='placeholder-segment-review'
            parts.append(re.match(r'^\s*',original)[0]+value.strip()+re.search(r'\s*$',original)[0])
        value=''.join(parts).replace('&','')
        if source[key]['text'].startswith('&'):
            i=next((i for i,c in enumerate(value) if c.isalpha()),None)
            if i is not None:value=value[:i]+'&'+value[i:]
        if Counter(PARAM.findall(value))!=Counter(PARAM.findall(source[key]['text'])):raise ValueError('Placeholder mismatch: '+key)
        if repeated(value):raise ValueError('Repeated output: '+key)
        result[key]={'text':value,'sourceHash':source[key]['sourceHash']}
    assert set(result)==set(source)
    out=Path('output');out.mkdir(exist_ok=True)
    (out/(language+'.json')).write_text(json.dumps({'language':language,'messages':result},ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    (out/(language+'.review.json')).write_text(json.dumps({'language':language,'refined':len(todo),'review':warnings,'seconds':round(time.monotonic()-started,2)},ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    print('Refined',language,len(todo),'messages; contextual review',len(warnings),'elapsed',round(time.monotonic()-started,2))


if __name__=='__main__':
    parser=argparse.ArgumentParser();parser.add_argument('--language',required=True);args=parser.parse_args();main(args.language)
