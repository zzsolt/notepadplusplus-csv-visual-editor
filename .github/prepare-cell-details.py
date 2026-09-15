"""Mechanically finalize a locally authored delta on its exact accepted base."""
from pathlib import Path
import base64
import gzip
import hashlib
import json
import subprocess
import sys


def git(*args):
    return subprocess.check_output(['git', *args], text=True).strip()


def write(path, content):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(content, encoding='utf-8', newline='\n')


def checked_path(name):
    path = Path(name)
    if path.is_absolute() or '..' in path.parts or path.parts[0] == '.git':
        raise ValueError('Invalid path in reviewed delta')
    return path


def main():
    compressed = base64.b64decode(Path(sys.argv[1]).read_text(), validate=True)
    if hashlib.sha256(compressed).hexdigest() != sys.argv[2]:
        raise ValueError('Reviewed payload checksum mismatch')
    payload = json.loads(gzip.decompress(compressed))
    if git('rev-parse', 'HEAD') != payload['base'] or git('status', '--porcelain'):
        raise ValueError('Finalization requires the exact clean accepted base')
    branch = payload['branch']
    remote = git('ls-remote', 'origin', 'refs/heads/' + branch).split()
    if len(remote) != 2 or remote[0] != payload['base']:
        raise ValueError('The target branch changed; preserve concurrent work')
    planned = {}
    for name, update in payload['updates'].items():
        path = checked_path(name)
        if git('hash-object', '--', name) != update['gitBlob']:
            raise ValueError('Current source differs from reviewed preimage: ' + name)
        text = path.read_text(encoding='utf-8')
        for old, new in update['edits']:
            if text.count(old) != 1:
                raise ValueError('Patch anchor is not unique: ' + name)
            text = text.replace(old, new)
        planned[path] = text
    for name, text in payload['newFiles'].items():
        path = checked_path(name)
        if path.exists(): raise ValueError('New file already exists: ' + name)
        planned[path] = text
    resource = Path('src/CsvVisualEditor.Localization')
    codes = {'en','hu','zh-CN','hi','es','ar','fr'}
    if set(payload['messages']) != codes or {p.stem for p in (resource/'Catalogs').glob('*.json')} != codes:
        raise ValueError('Exactly seven languages must be maintained')
    for code in codes:
        path = resource/'Catalogs'/(code+'.json')
        catalog = json.loads(path.read_text(encoding='utf-8'))
        additions = payload['messages'][code]
        if len(catalog['messages']) != 376 or set(additions) != set(payload['messages']['en']):
            raise ValueError('Baseline source or new language coverage changed')
        for key, text in additions.items():
            if key in catalog['messages']: raise ValueError('Message already exists: '+key)
            entry = {'text':text, 'sourceHash':hashlib.sha256(payload['messages']['en'][key].encode()).hexdigest()}
            if code == 'en':
                entry['context'] = 'Cell details dialog. Preserve exact escape notation, technical tokens and typed format arguments. ' + key
            catalog['messages'][key] = entry
        planned[path] = json.dumps(catalog, ensure_ascii=False, indent=2)+'\n'
    for path, text in planned.items(): write(path,text)
    subprocess.run([sys.executable,'tools/localization/generate.py'],check=True)
    sys.path.insert(0,str(Path('tools/localization').resolve()))
    import verify
    raw_path=resource/'raw-literals.json'
    raw=verify.read(raw_path)
    actual=verify.literal_inventory()
    touched={p.relative_to('src/CsvVisualEditor').as_posix() for p in planned
             if p.as_posix().startswith('src/CsvVisualEditor/') and p.suffix=='.cs'}
    approved_new={'CsvCellValueEditor','CsvCellValuePreview','CsvCellMetrics','CsvCellValidation',
                  'CsvCellAccept','CsvCellCancel','CsvCellDetailsButton','Cell details','Alt+Enter'}
    for name in touched:
        delta=set(actual.get(name,[]))-set(raw['files'].get(name,[]))
        if delta-approved_new:
            raise ValueError('Unreviewed new UI literal: '+name+' '+str(delta-approved_new))
        if name in actual: raw['files'][name]=actual[name]
        else: raw['files'].pop(name,None)
    raw['files'] = dict(sorted(raw['files'].items()))
    write(raw_path,json.dumps(raw,ensure_ascii=False,indent=2)+'\n')
    subprocess.run([sys.executable,'-m','unittest','discover','-s','tests/ci','-p','test_*.py'],check=True)
    subprocess.run([sys.executable,'-m','unittest','discover','-s','tests/localization','-p','test_*.py'],check=True)
    subprocess.run([sys.executable,'tools/localization/verify.py'],check=True)
    if git('hash-object','src/CsvVisualEditor/CsvWhitespaceCellPainter.cs') != '39ab5cc25f9b88e946e5feabdf77e439019d0229':
        raise ValueError('Accepted whitespace painter changed')
    subprocess.run(['git','add','src','tests','tools/build-local.ps1','.github/workflows/ci.yml','CHANGELOG.md','docs/cell-details.md'],check=True)
    subprocess.run(['git','-c','user.name=Zolnai Zsolt','-c','user.email=44463109+zzsolt@users.noreply.github.com',
                    'commit','-m','Add lossless cell details and pending cell editing [test-package]'],check=True)
    # No force or ref rewriting. An intervening update causes the push to fail.
    subprocess.run(['git','push','origin','HEAD:refs/heads/'+branch],check=True)
    print('CANDIDATE_HEAD='+git('rev-parse','HEAD'))
    print('CANDIDATE_TREE='+git('rev-parse','HEAD^{tree}'))


if __name__=='__main__':
    main()
