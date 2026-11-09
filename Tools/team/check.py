"""Check committed PR scope and common Unity mistakes; Python stdlib only.

Not a distributed lock, secret scanner, Unity compiler, or substitute for human review.
"""
import argparse
import json
from pathlib import Path, PurePosixPath
import re
import subprocess
import sys
import unicodedata

REPO = 'https://github.com/ghwls9896/CampusCommons'
OWNERS = {'HIGHCHOI', 'Yeni-sjs', 'ghwls9896', 'GongBeak'}
SHARED = {
    'AGENTS.md', 'backend/app.py', 'render.yaml',
    *(f'Assets/GrandLibrary/Scripts/{name}.cs' for name in ('Player', 'GameStart', 'Portal', 'LobbyDoor')),
}
GUID = re.compile(rb'^guid: ([0-9a-fA-F]{32})\s*$', re.MULTILINE)
CONFLICT = re.compile(rb'^(?:<<<<<<< .+|=======$|>>>>>>> .+)$', re.MULTILINE)


def safe_path(path):
    return (isinstance(path, str) and bool(path) and not path.startswith('/')
            and not any(c in path for c in '\\*?[]:\n\r')
            and all(part not in ('', '.', '..') for part in path.rstrip('/').split('/')))


def strings(value):
    return isinstance(value, list) and all(isinstance(v, str) and v.strip() for v in value)


def task_errors(task, branch):
    errors = []
    if not isinstance(task, dict):
        return ['Task JSON must be an object']
    identifier = task.get('id', '')
    if not isinstance(identifier, str) or not re.fullmatch(r'cc-[1-9][0-9]*', identifier):
        errors.append('id must be cc-<GitHub issue number>')
    if task.get('owner') not in OWNERS:
        errors.append('owner must match docs/team/OWNERSHIP.md')
    if not isinstance(task.get('title'), str) or not task['title'].strip():
        errors.append('title is required')
    if task.get('branch') != branch or not re.fullmatch(r'(feat|fix|chore|docs|test|refactor|codex)/[a-z0-9][a-z0-9/-]*', branch):
        errors.append('branch must match the task and use a task branch (never main)')
    if task.get('issue') != f'{REPO}/issues/{str(identifier).removeprefix("cc-")}':
        errors.append('issue must point to this repository and match id')
    scope = task.get('scope')
    if not strings(scope) or not scope or any(not safe_path(p) or p == 'Assets/' for p in scope):
        errors.append('scope needs exact files or directories ending in /; no wildcards/root/Assets/')
    shared = task.get('shared_files')
    if not strings(shared) or any(not safe_path(p) or p.endswith('/') for p in shared):
        errors.append('shared_files must list exact relative file paths (or [])')
    elif strings(scope) and any(not in_scope(p, scope) for p in shared):
        errors.append('shared_files must also be included in scope')
    if not strings(task.get('validation')) or not task.get('validation'):
        errors.append('validation must name actual planned checks')
    if not strings(task.get('depends_on')):
        errors.append('depends_on must be an array of issue/PR references (or [])')
    return errors


def in_scope(path, scope):
    return any(path.startswith(p) if p.endswith('/') else path == p for p in scope)


def is_shared(path):
    return (path in SHARED or path.endswith(('.unity', '.prefab'))
            or path.startswith(('ProjectSettings/', 'Packages/', '.github/', 'Tools/team/')))


def pr_errors(body, task):
    expected = f'Task: docs/tasks/{task["id"]}.json'
    return [] if expected in body.splitlines() else [f'PR description must contain a line: {expected}']


def ignored_by_unity(path):
    # Unity 6000.4 SpecialFolders: dot names are imported in StreamingAssets.
    parts = PurePosixPath(path).parts[1:]
    streaming = path.startswith('Assets/StreamingAssets/')
    return (any(p.endswith('~') or p == 'cvs' for p in parts)
            or path.endswith('.tmp')
            or (not streaming and any(p.startswith('.') for p in parts)))


def change_errors(task, changes, head, base):
    errors = []
    own_notes = {f'docs/tasks/{task["id"]}.json', f'docs/tasks/{task["id"]}.md'}
    guid_paths = {}
    case_paths = {}
    for path in head:
        parts = PurePosixPath(path).parts
        for depth in range(1, len(parts) + 1):
            name = '/'.join(parts[:depth])
            key = unicodedata.normalize('NFC', name).casefold()
            case_paths.setdefault(key, set()).add(name)
    for path, data in head.items():
        if path.endswith('.meta'):
            match = GUID.search(data)
            if match:
                guid_paths.setdefault(match[1].lower(), []).append(path)
    base_dirs = {str(parent) for p in base for parent in PurePosixPath(p).parents}
    for status, path in changes:
        if path not in own_notes and not in_scope(path, task['scope']):
            errors.append(f'Outside task scope: {path}')
        if is_shared(path) and path not in task['shared_files']:
            errors.append(f'Missing shared_files declaration (not approval): {path}')
        if status == 'D':
            if path.startswith('Assets/') and path + '.meta' in head:
                errors.append(f'Deleted asset leaves orphan .meta: {path}.meta')
            continue
        parts = PurePosixPath(path).parts
        for depth in range(1, len(parts) + 1):
            name = '/'.join(parts[:depth])
            if len(case_paths.get(unicodedata.normalize('NFC', name).casefold(), set())) > 1:
                errors.append(f'Cross-platform path case collision: {name}')
        data = head.get(path, b'')
        name = PurePosixPath(path).name
        if ((name == '.env' or name.startswith('.env.')) and name != '.env.example') or path.endswith(('.pem', '.p12', '.pfx', '.key')):
            errors.append(f'Possible secret file: {path}')
        if '\x00' not in data[:8192].decode('latin1'):
            if CONFLICT.search(data):
                errors.append(f'Unresolved conflict markers: {path}')
            if (path.endswith('.json') and re.search(rb'"private_key"\s*:\s*"[^"\s]', data)) or re.search(rb'-----BEGIN (?:RSA |EC |OPENSSH )?PRIVATE KEY-----', data):
                errors.append(f'Possible private key: {path}')
        if not path.startswith('Assets/'):
            continue
        if status == 'A':
            if not path.endswith('.meta') and not ignored_by_unity(path) and path + '.meta' not in head:
                errors.append(f'New Unity asset missing .meta: {path}')
            for parent in PurePosixPath(path).parents:
                folder = str(parent)
                if folder in ('.', 'Assets'):
                    break
                if folder not in base_dirs and not ignored_by_unity(folder) and folder + '.meta' not in head:
                    errors.append(f'New Unity directory missing .meta: {folder}')
        if path.endswith('.meta'):
            match = GUID.search(data)
            if not match:
                errors.append(f'Invalid .meta GUID: {path}')
            elif len(guid_paths[match[1].lower()]) > 1:
                errors.append(f'New/changed .meta has duplicate GUID: {path}')
            old = GUID.search(base.get(path, b''))
            if old and match and old[1].lower() != match[1].lower():
                errors.append(f'Existing GUID changed: {path}')
            asset = path[:-5]
            if asset not in head and not any(p.startswith(asset + '/') for p in head) and not re.search(rb'^folderAsset: yes\s*$', data, re.MULTILINE):
                errors.append(f'Unity .meta has orphan target: {path}')
    # Also catch a removed .meta beside a still-present asset.
    for status, path in changes:
        if status == 'D' and path.endswith('.meta') and (path[:-5] in head or any(p.startswith(path[:-5] + '/') for p in head)):
            errors.append(f'Existing asset lost .meta: {path[:-5]}')
    return sorted(set(errors))


def git(root, *args):
    return subprocess.check_output(['git', '-C', str(root), *args], stderr=subprocess.PIPE)


def snapshot(root, ref, paths):
    # Only materialize changed files and .meta; names for the rest still matter.
    names = git(root, 'ls-tree', '-r', '--name-only', '-z', ref).decode().split('\0')
    result = {}
    for path in filter(None, names):
        result[path] = git(root, 'show', f'{ref}:{path}') if path in paths or path.endswith('.meta') else b''
    return result


def check_repo(root, base_ref, branch, body=None):
    base = git(root, 'rev-parse', '--verify', '--end-of-options', base_ref + '^{commit}').decode().strip()
    ancestor = git(root, 'merge-base', base, 'HEAD').decode().strip()
    raw = git(root, 'diff', '--no-renames', '--name-status', '-z', ancestor, 'HEAD').decode().split('\0')
    changes = list(zip(raw[0:-1:2], raw[1:-1:2]))
    paths = {p for _, p in changes}
    head = snapshot(root, 'HEAD', paths)
    base_files = snapshot(root, ancestor, paths)
    tasks = []
    for path in head:
        if re.fullmatch(r'docs/tasks/cc-[1-9][0-9]*\.json', path):
            data = json.loads(git(root, 'show', f'HEAD:{path}'))
            if isinstance(data, dict) and data.get('branch') == branch:
                tasks.append((path, data))
    if len(tasks) != 1:
        return [f'Expected exactly one task JSON for branch {branch}; found {len(tasks)}'], len(changes)
    task_path, task = tasks[0]
    errors = task_errors(task, branch)
    if task_path != f'docs/tasks/{task.get("id")}.json':
        errors.append('Task filename must match id')
    if errors:
        return errors, len(changes)
    if body is not None:
        errors += pr_errors(body, task)
    return errors + change_errors(task, changes, head, base_files), len(changes)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--base', default='origin/main')
    parser.add_argument('--branch')
    parser.add_argument('--event', help='GitHub pull_request event JSON; also validates PR description')
    args = parser.parse_args()
    root = Path(__file__).resolve().parents[2]
    body = None
    branch = args.branch
    if args.event:
        event = json.loads(Path(args.event).read_text())
        pr = event['pull_request']; branch = pr['head']['ref']; body = pr.get('body') or ''
        args.base = pr['base']['sha']
    if not branch:
        branch = git(root, 'branch', '--show-current').decode().strip()
    try:
        errors, count = check_repo(root, args.base, branch, body)
    except (subprocess.CalledProcessError, ValueError, KeyError, TypeError) as exc:
        print(f'TEAM_CHECK_FAILED: {type(exc).__name__}; check Git refs and task JSON', file=sys.stderr)
        return 1
    for error in errors:
        print('ERROR: ' + error)
    if errors:
        return 1
    print(f'TEAM_CHECK_PASS: {count} committed changed paths; scope and file hygiene only (no Unity runtime/lock verification)')
    return 0


if __name__ == '__main__':
    sys.exit(main())
