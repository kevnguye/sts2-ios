#!/usr/bin/env python3
"""Stage and validate save transfers. No game code or credentials required."""
import hashlib
import json
import os
from pathlib import Path
import shutil
import sys

MANIFEST = 'manifest.json'


def inventory(root):
    root = Path(root)
    if root.is_symlink() or not root.is_dir():
        raise ValueError('save root must be a real directory')
    result = []
    for directory, dirs, files in os.walk(root, followlinks=False):
        for name in dirs + files:
            path = Path(directory) / name
            if path.is_symlink():
                raise ValueError(f'symlink in save tree: {path}')
        for name in sorted(files):
            path = Path(directory) / name
            if not path.is_file():
                raise ValueError(f'non-regular save: {path}')
            relative = path.relative_to(root).as_posix()
            if any(c in relative for c in ('\\', ':', '\n', '\r')):
                raise ValueError('unsupported save filename')
            before = path.stat()
            with path.open('rb') as stream:
                digest = hashlib.file_digest(stream, 'sha256').hexdigest()
            after = path.stat()
            if (before.st_size, before.st_mtime_ns) != (after.st_size, after.st_mtime_ns):
                raise ValueError('save changed while reading; close the game and retry')
            result.append({'path': relative, 'size': after.st_size, 'sha256': digest,
                           'mtime': int(after.st_mtime)})
    result.sort(key=lambda f: f['path'])
    profile = next((f for f in result if f['path'] == 'profile.save'), None)
    if profile is None or profile['size'] == 0:
        raise ValueError('missing or empty profile.save')
    return result


def stage(source, destination):
    """Publish a manifest only after an independently checked, stable copy."""
    before = inventory(source)
    destination = Path(destination)
    destination.mkdir(parents=True, exist_ok=False)
    shutil.copytree(source, destination / '1', symlinks=True)
    copied = inventory(destination / '1')
    if before != inventory(source) or before != copied:
        raise ValueError('save changed during copy; transfer cancelled')
    timestamp = max(f['mtime'] for f in copied if not Path(f['path']).name.startswith('sync_'))
    if timestamp <= 0:
        raise ValueError('invalid save timestamp')
    manifest = {'version': 1, 'timestamp': timestamp, 'files': copied}
    (destination / MANIFEST).write_text(json.dumps(manifest, ensure_ascii=False), encoding='utf-8')
    return timestamp


def validate(directory):
    directory = Path(directory)
    manifest = json.loads((directory / MANIFEST).read_text(encoding='utf-8'))
    if manifest.get('version') != 1 or manifest.get('files') != inventory(directory / '1'):
        raise ValueError('save manifest mismatch')
    return manifest['timestamp']


def recover(target):
    """Restore the old desktop tree if a prior process stopped between renames."""
    target = Path(target)
    previous = target.with_name(target.name + '.previous')
    pending = target.with_name(target.name + '.pending')
    for path in (target, previous, pending):
        if path.is_symlink() or (path.exists() and not path.is_dir()):
            raise ValueError('unsafe replacement path')
    if not target.exists() and previous.exists():
        previous.rename(target)


def replace(source, target):
    """Retain the previous desktop tree and recover interrupted renames."""
    target = Path(target)
    recover(target)
    previous = target.with_name(target.name + '.previous')
    pending = target.with_name(target.name + '.pending')
    before = inventory(source)
    if pending.exists():
        shutil.rmtree(pending)
    shutil.copytree(source, pending, symlinks=True)
    if inventory(pending) != before or inventory(source) != before:
        raise ValueError('save changed during replacement')
    if previous.exists():
        shutil.rmtree(previous)
    moved = False
    try:
        if target.exists():
            target.rename(previous)
            moved = True
        pending.rename(target)
    except OSError:
        if moved and not target.exists():
            previous.rename(target)
        raise


if __name__ == '__main__':
    try:
        if sys.argv[1] == 'stage':
            print(stage(sys.argv[2], sys.argv[3]))
        elif sys.argv[1] == 'newest':
            print(max(f['mtime'] for f in inventory(sys.argv[2])
                      if not Path(f['path']).name.startswith('sync_')))
        elif sys.argv[1] == 'validate':
            print(validate(sys.argv[2]))
        elif sys.argv[1] == 'same':
            if inventory(sys.argv[2]) != inventory(sys.argv[3]):
                raise ValueError('save trees differ; close both games and retry')
        elif sys.argv[1] == 'replace':
            replace(sys.argv[2], sys.argv[3])
        elif sys.argv[1] == 'recover':
            recover(sys.argv[2])
        elif sys.argv[1] == 'fingerprint':
            print(hashlib.sha256(json.dumps(inventory(sys.argv[2]), sort_keys=True).encode()).hexdigest())
        else:
            raise ValueError('expected stage, newest, validate, same, replace, recover or fingerprint')
    except (OSError, ValueError, KeyError, IndexError) as error:
        print(f'Save validation failed: {error}', file=sys.stderr)
        sys.exit(1)
