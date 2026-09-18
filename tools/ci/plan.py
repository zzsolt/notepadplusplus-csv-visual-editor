"""Select cheap validation and explicit full candidates without a hosted matrix."""
from __future__ import annotations

import json
import os
from pathlib import Path
import re
import subprocess

SOURCE_PREFIXES = ('src/', 'tests/', 'tools/', 'packaging/', '.github/')
SOURCE_FILES = {'Directory.Build.props', 'Directory.Build.targets', 'global.json',
                'NuGet.config', 'nuget.config', 'packages.lock.json', 'LICENSE', 'THIRD_PARTY_NOTICES.txt'}
SHA = re.compile(r'[0-9a-fA-F]{40}')


def classify(paths: list[str], event_name: str, head_message: str) -> dict[str, bool]:
    if event_name not in ('pull_request', 'push', 'workflow_dispatch'):
        raise ValueError('Unsupported CI event')
    relevant = any(path.startswith(SOURCE_PREFIXES) or path in SOURCE_FILES
                   or path.endswith(('.sln', '.slnx')) for path in paths)
    full = event_name == 'workflow_dispatch' or (relevant and '[test-package]' in head_message)
    return {'validate': full or relevant, 'full': full}


def git(*args: str) -> str:
    return subprocess.check_output(['git', *args], text=True, encoding='utf-8')


def revision(value: str) -> str:
    if not SHA.fullmatch(value):
        raise ValueError('Missing or invalid event commit SHA')
    return value


def plan_event(event_name: str, event: dict, current_sha: str) -> dict[str, bool]:
    if event_name == 'workflow_dispatch':
        return classify([], event_name, '')
    if event_name == 'pull_request':
        pr = event['pull_request']
        base, head = revision(pr['base']['sha']), revision(pr['head']['sha'])
        # On synchronize, consider this push rather than the accumulated PR diff.
        # A documentation-only follow-up must not rerun the existing source tests.
        if event.get('action') == 'synchronize' and event.get('before'):
            base = revision(event['before'])
    elif event_name == 'push':
        if event.get('deleted'):
            return {'validate': False, 'full': False}
        base, head = revision(event['before']), revision(event.get('after', current_sha))
    else:
        raise ValueError('Unsupported CI event')
    # The marker belongs only to the proposed head, never the synthetic merge/base.
    message = git('log', '-1', '--format=%B', head, '--')
    changed = (git('ls-tree', '-r', '--name-only', '-z', head) if base == '0' * 40
               else git('diff', '--name-only', '-z', base, head, '--'))
    return classify([name for name in changed.split('\0') if name], event_name, message)


def main() -> None:
    event = json.loads(Path(os.environ['GITHUB_EVENT_PATH']).read_text(encoding='utf-8'))
    result = plan_event(os.environ['GITHUB_EVENT_NAME'], event, os.environ['GITHUB_SHA'])
    with Path(os.environ['GITHUB_OUTPUT']).open('a', encoding='utf-8') as output:
        for key, value in result.items():
            output.write(f'{key}={str(value).lower()}\n')
    print(json.dumps(result, sort_keys=True))


if __name__ == '__main__':
    main()
