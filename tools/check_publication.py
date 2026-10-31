#!/usr/bin/env python3
"""Guard against publishing local game/SDK files or obvious credentials.

This is an exclusion/provenance guardrail, not a copyright-compliance guarantee.
"""
import argparse
import hashlib
import re
import subprocess
from pathlib import Path

EXCLUDED = re.compile(r'(?:^|/)(?:game-refs|prebuilt|\.work|\.godot|node_modules|bin|obj|security-review)(?:/|$)|(?:^|/)config\.sh$')
SUFFIXES = {'.dll', '.pck', '.ipa', '.app', '.framework', '.xcframework', '.a', '.so', '.dylib',
            '.exe', '.bank', '.spskel', '.skel', '.atlas', '.tscn', '.tres', '.png', '.jpg', '.jpeg',
            '.webp', '.wav', '.ogg', '.mp3', '.ttf', '.otf', '.p12', '.pfx', '.mobileprovision',
            '.pem', '.key', '.save', '.zip', '.dmg', '.tpz'}
# Individually rendered/reviewed technical reports; retain the old hash for Git history.
REVIEWED_PDFS = {
    '2368c949d95ce68255ff71684914ec6367ad89d8ebbe3bcb1ca6a45eccc602b5',
    '88495a86ec66843b6a6698c9519569f0b39864a5afb687dd628096f0e7767f6e',
}
PATTERNS = {
    'private key': re.compile(rb'-----BEGIN (?:RSA |EC |OPENSSH )?PRIVATE KEY-----'),
    'GitHub token': re.compile(rb'(?:gh[pousr]_[A-Za-z0-9]{20,}|github_pat_[A-Za-z0-9_]{40,})'),
    'embedded image': re.compile(rb'data:image/[^;]+;base64,'),
}


def inspect(path, data):
    problems = []
    if EXCLUDED.search(path) or Path(path).suffix.lower() in SUFFIXES:
        problems.append('excluded local/game/SDK/signing path')
    if data.startswith((b'MZ', b'GDPC', b'\x7fELF', b'\xcf\xfa\xed\xfe', b'\xfe\xed\xfa\xcf', b'!<arch>\n', b'PK\x03\x04')):
        problems.append('executable/archive/game-pack signature')
    for label, pattern in PATTERNS.items():
        if pattern.search(data): problems.append(label)
    if path.endswith('.cs') and re.search(rb'^\s*namespace\s+MegaCrit\.', data, re.M):
        problems.append('possible decompiled game namespace')
    if path.lower().endswith('.pdf') and hashlib.sha256(data).hexdigest() not in REVIEWED_PDFS:
        problems.append('PDF needs manual image/content review')
    if path.lower().endswith('.html') and re.search(rb'<(?:img|image|svg|object|embed)\b', data, re.I):
        problems.append('HTML embeds require manual review')
    return problems


def git(*args, input=None):
    return subprocess.check_output(['git', *args], input=input)


def check(history=False):
    root = Path(git('rev-parse', '--show-toplevel').decode().strip())
    paths = set(git('ls-files', '-z').split(b'\0')) | set(git('ls-files', '--others', '--exclude-standard', '-z').split(b'\0'))
    failures = []
    count = 0
    for raw in sorted(paths - {b''}):
        path = raw.decode()
        file = root / path
        if not file.exists(): continue
        count += 1
        problems = inspect(path, file.read_bytes())
        if problems: failures.append((path, problems))
    blobs = 0
    if history:
        objects = git('rev-list', '--objects', '--all').decode().splitlines()
        info = git('cat-file', '--batch-check=%(objectname) %(objecttype)',
                   input=('\n'.join(x.split(' ', 1)[0] for x in objects) + '\n').encode()).decode().splitlines()
        for entry, meta in zip(objects, info):
            sha, kind = meta.split()
            if kind != 'blob': continue
            blobs += 1
            path = entry.partition(' ')[2]
            problems = inspect(path, git('cat-file', 'blob', sha))
            if problems: failures.append(('history ' + sha[:12] + ' ' + path, problems))
    for path, problems in failures:
        print('FAIL:', path, '—', ', '.join(problems))
    print(f'Publication check: {count} candidate files, {blobs} historical blobs, {len(failures)} flagged items')
    return bool(failures)


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--history', action='store_true')
    raise SystemExit(check(parser.parse_args().history))
