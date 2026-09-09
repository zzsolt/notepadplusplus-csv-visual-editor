"""Build reviewable UI catalogs from a pinned offline translation model."""
from __future__ import annotations
from collections import Counter
from pathlib import Path
import argparse
import hashlib
import json
import re
import time
import unicodedata

MODEL = 'Nextcloud-AI/madlad400-3b-mt-ct2-int8'
REVISION = 'aa32bbdeba7880eff2096ec044cb155a340a9400'
ROOT = Path('src/CsvVisualEditor.Localization')
PARAM = re.compile(r'\{\d+(?:,-?\d+)?(?::[^{}]+)?\}')
TOKEN = re.compile(r'\{\d+\}')
TERMS = re.compile(r'CSV Visual Editor|Notepad\+\+|Windows-1250|UTF-16|UTF-8|GitHub|Scintilla|Ctrl\+[A-Z]|Shift\+F3|Shift\+Enter|\bF3\b|-12\.5|250,000|10,000|16 Mi|65,001|4,096')
VARIANTS = {'en':None, 'ext':None, 'sgs':None, 'kab':None, 'ab':None, 'la-pig':None,
    'yue-Hant':'zh_Hant', 'zh-TW':'zh_Hant','zh-CN':'zh','nb':'no','tl':'fil',
    'sr-Latn':'sr','sr-Cyrl':'sr','uz-Latn':'uz','uz-Cyrl':'uz','oc-aran':'oc',
    'pt-BR':'pt','pt-PT':'pt','es-AR':'es'}
CONTEXT = {'View.ShowSpaces':'Show space characters', 'Common.Table':'Data table',
    'Common.Cut':'Cut', 'Common.Close':'Close', 'Transform.TrimOuterWhitespace':'Trim leading and trailing whitespace'}


def model_code(code):
    return VARIANTS.get(code,code)


def invariant(text):
    return not re.search(r'[A-Za-z]', TERMS.sub('', PARAM.sub('', text)))


def repetitive(text):
    return bool(re.search(r'([^\W\d_]{2,32})\1{3,}',text,re.I) or
        re.search(r'(\b\w+(?:\s+\w+){0,6}\s+)\1{3,}',text,re.I))


def read_source():
    return json.loads((ROOT/'Catalogs/en.json').read_text(encoding='utf-8'))['messages']


def run(language):
    import ctranslate2
    from transformers import AutoTokenizer
    from huggingface_hub import snapshot_download
    source=read_source()
    location=snapshot_download(MODEL,revision=REVISION,allow_patterns=['*.json','*.bin','*.model'],ignore_patterns=['pytorch_model*'])
    tokenizer=AutoTokenizer.from_pretrained(location,local_files_only=True,trust_remote_code=False)
    if f'<2{language}>' not in tokenizer.get_vocab():raise ValueError('Unsupported model language')
    engine=ctranslate2.Translator(location,device='cpu',compute_type='int8',inter_threads=1,intra_threads=2)
    cache={}

    def translate_many(texts,beam=2):
        unique=list(dict.fromkeys(t for t in texts if t not in cache))
        if unique:
            # The model is line-oriented. Never send CR/LF/tab-delimited examples.
            assert all(not re.search(r'[\r\n\t]',t) for t in unique)
            encoded=[tokenizer.convert_ids_to_tokens(tokenizer.encode(f'<2{language}> '+t)) for t in unique]
            results=engine.translate_batch(encoded,beam_size=beam,max_batch_size=24,
                max_decoding_length=192,no_repeat_ngram_size=6,repetition_penalty=1.0)
            for original,result in zip(unique,results):
                value=tokenizer.decode(tokenizer.convert_tokens_to_ids(result.hypotheses[0]),skip_special_tokens=True).strip()
                if not value or repetitive(value) or len(value)>max(120,5*len(original)):
                    raise ValueError('Invalid translation output: '+original)
                cache[original]=value
        return [cache[t] for t in texts]

    def prepare(text):
        replaces={}
        def protect(match):
            marker='{'+str(len(replaces))+'}'
            replaces[marker]=match[0]
            return marker
        # Protect each occurrence, including repeated format arguments, separately.
        prepared=PARAM.sub(protect,text.replace('&',''))
        prepared=TERMS.sub(protect,prepared)
        return prepared.strip(),replaces

    def restore(text,replaces):
        text=text.replace('\uff5b','{').replace('\uff5d','}')
        def fix(match):
            digits=''.join(str(unicodedata.digit(c)) for c in match[1] if c.isdigit())
            return '{'+digits+'}'
        text=re.sub(r'\{\s*([\d ]+)\s*\}',fix,text)
        if Counter(TOKEN.findall(text))!=Counter(replaces):return None
        return TOKEN.sub(lambda m:replaces[m[0]],text)

    plans={}; all_parts=[]
    for key,entry in source.items():
        # Preserve actual newlines in the resource, but translate each line independently.
        parts=re.split(r'(\r\n|\n|\r|\t)',CONTEXT.get(key,entry['text']))
        plan=[]
        for part in parts:
            if not part or invariant(part) or part.isspace():plan.append((part,None,None));continue
            prepared,replaces=prepare(part)
            plan.append((part,prepared,replaces));all_parts.append(prepared)
        plans[key]=plan
    started=time.monotonic()
    translate_many(all_parts)
    output={}; review={}
    for key,plan in plans.items():
        parts=[]
        for original,prepared,replaces in plan:
            if prepared is None:parts.append(original);continue
            value=restore(cache[prepared],replaces)
            if value is None:
                # Exact placeholder-preserving segment recovery; context can be refined
                # in the checked-in catalog without touching the runtime or source key.
                segments=re.split(r'(\{\d+\})',prepared)
                work=[s.strip() for s in segments if not TOKEN.fullmatch(s) and re.search(r'[A-Za-z]',s)]
                translate_many(work)
                for j,segment in enumerate(segments):
                    if TOKEN.fullmatch(segment):segments[j]=replaces[segment]
                    elif segment.strip() in cache:
                        segments[j]=re.match(r'^\s*',segment)[0]+cache[segment.strip()]+re.search(r'\s*$',segment)[0]
                value=''.join(segments)
                review[key]='context-review'
            value=re.match(r'^\s*',original)[0]+value.strip()+re.search(r'\s*$',original)[0]
            parts.append(value)
        value=''.join(parts)
        if PARAM.findall(source[key]['text']) and Counter(PARAM.findall(value))!=Counter(PARAM.findall(source[key]['text'])):
            raise ValueError('Placeholder mismatch: '+key)
        value=value.replace('&','')
        if source[key]['text'].startswith('&'):
            pos=next((i for i,c in enumerate(value) if c.isalpha()),None)
            if pos is not None:value=value[:pos]+'&'+value[pos:]
        if value==source[key]['text'] and not invariant(value):review[key]='terminology-review'
        output[key]={'text':value,'sourceHash':source[key]['sourceHash']}
    target=Path('output');target.mkdir(exist_ok=True)
    (target/(language+'.json')).write_text(json.dumps({'language':language,'messages':output},ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    (target/(language+'.review.json')).write_text(json.dumps({'language':language,'source':hashlib.sha256(json.dumps(source,sort_keys=True).encode()).hexdigest(),'review':review,'seconds':round(time.monotonic()-started,2)},ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    print('Generated',language,len(output),'messages in',round(time.monotonic()-started,2),'seconds.')


if __name__=='__main__':
    parser=argparse.ArgumentParser();parser.add_argument('--language');parser.add_argument('--list',action='store_true');args=parser.parse_args()
    if args.list:
        inventory=json.loads((ROOT/'languages.json').read_text(encoding='utf-8'))['languages']
        print(json.dumps(sorted({model_code(i['code']) for i in inventory if model_code(i['code'])})))
    else:run(args.language)
