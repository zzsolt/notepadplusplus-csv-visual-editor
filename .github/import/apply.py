"""Import the reviewed source delta only after exact integrity checks."""
import base64, gzip, hashlib, json, pathlib, subprocess
P = 65521
ALPHABET = 'ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/='
def moments(text):
    return [sum(ord(c) * pow(i + 1, k, P) for i, c in enumerate(text)) % P for k in range(4)]
def substitute(text, expected, two=True):
    actual = moments(text)
    d = [(a-b) % P for a,b in zip(expected, actual)]
    if not any(d): return text
    if d[0]:
        i = d[1] * pow(d[0], -1, P) % P
        if 1 <= i <= len(text) and d[2] == d[0]*i*i%P and d[3] == d[0]*i*i*i%P:
            c = (ord(text[i-1]) + d[0]) % P
            if c < 128 and chr(c) in ALPHABET:
                value = text[:i-1] + chr(c) + text[i:]
                if moments(value) == expected: return value
    if not two: return None
    for i in range(1, len(text)+1):
        denominator = (d[1]-i*d[0]) % P
        if not denominator: continue
        j = (d[2]-i*d[1])*pow(denominator, -1, P)%P
        if not i < j <= len(text): continue
        e = (d[1]-j*d[0])*pow((i-j)%P,-1,P)%P
        f = (d[0]-e)%P
        if (e*i**3+f*j**3)%P != d[3]: continue
        a,b = (ord(text[i-1])+e)%P,(ord(text[j-1])+f)%P
        if a >= 128 or b >= 128 or chr(a) not in ALPHABET or chr(b) not in ALPHABET: continue
        value = text[:i-1]+chr(a)+text[i:j-1]+chr(b)+text[j:]
        if moments(value) == expected: return value
    return None
def recover(text, expected, length):
    if len(text) == length: return substitute(text, expected)
    if len(text) == length-1:
        char = (expected[0]-moments(text)[0])%P
        choices = chr(char) if char < 128 and chr(char) in ALPHABET else ALPHABET
        for c in choices:
            for i in range(length):
                result = substitute(text[:i]+c+text[i:], expected, False)
                if result is not None: return result
    if len(text) == length+1:
        for i in range(len(text)):
            result = substitute(text[:i]+text[i+1:], expected, False)
            if result is not None: return result
    return None
folder = pathlib.Path('.github/import')
lines = {}
for file in sorted(folder.glob('part-*.txt')):
    for line in file.read_text().splitlines():
        index, length, parity, value = line.split(':',3)
        assert int(index) not in lines
        lines[int(index)] = (int(length),parity,value)
for line in (folder/'overrides.txt').read_text().splitlines():
    index, length, parity, value = line.split(':',3)
    lines[int(index)] = (int(length),parity,value)
assert sorted(lines) == list(range(434))
parts=[]; failures=[]
for index,(length,parity,value) in sorted(lines.items()):
    expected=[int(parity[i:i+4],16) for i in range(0,16,4)]
    recovered=recover(value, expected,length)
    if recovered is None: failures.append(index)
    else: parts.append(recovered)
if failures: raise ValueError('Invalid transfer blocks: '+str(failures))
packed=base64.b64decode(''.join(parts),validate=True)
assert hashlib.sha256(packed).hexdigest() == 'b18565bbc60bc71b618b4b5182cc94711286902d00aa0b20fda1f24103f8008a'
delta=json.loads(gzip.decompress(packed))
def checked(name):
    path=pathlib.PurePosixPath(name)
    assert not path.is_absolute() and '..' not in path.parts
    assert name in ('README.md','CHANGELOG.md','.gitignore','.github/pull_request_template.md') or name.startswith(('src/','tests/','tools/','docs/'))
    return pathlib.Path(name)
for name,entry in delta['files'].items():
    path=checked(name)
    if 'new' in entry:
        assert not path.exists(), name
        text=entry['new']
    else:
        original=path.read_bytes()
        assert hashlib.sha256(original).hexdigest() == entry['sha256'], name
        rows=original.decode('utf-8').splitlines(keepends=True)
        for start,end,replacement in reversed(entry['edits']): rows[start:end]=[replacement]
        text=''.join(rows)
    path.parent.mkdir(parents=True,exist_ok=True)
    if name == 'tools/localization/csharp_strings.py': text = text.rstrip() + '\n'
    path.write_text(text,encoding='utf-8',newline='\n')
for name in delta['delete']:
    assert name.startswith('docs/') and name.endswith('.md')
    checked(name).unlink()
english=pathlib.Path('src/CsvVisualEditor.Localization/Catalogs/en.json')
catalog=json.loads(english.read_text())
for entry in catalog['messages'].values():entry['sourceHash']=hashlib.sha256(entry['text'].encode()).hexdigest()
english.write_text(json.dumps(catalog,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
subprocess.run(['python','tools/localization/generate.py'],check=True)
subprocess.run(['python','tools/localization/verify.py','--english-only'],check=True)
subprocess.run(['git','add','src','tests','tools','docs','README.md','CHANGELOG.md','.gitignore','.github/pull_request_template.md'],check=True)
subprocess.run(['git','diff','--cached','--check'],check=True)
assert subprocess.check_output(['git','hash-object','src/CsvVisualEditor/CsvWhitespaceCellPainter.cs']).decode().strip() == '39ab5cc25f9b88e946e5feabdf77e439019d0229'
print('Reviewed source delta verified and staged.')
