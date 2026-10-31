import pathlib, tempfile, struct, hashlib, subprocess, sys, json, ast
root = pathlib.Path(__file__).resolve().parents[1]
results = []
def make_pack(path, name):
    data = b'SECURITY_REVIEW_SENTINEL\n'
    pb = name.encode()
    pb += b'\0' * ((-len(pb)) % 4)
    header = struct.pack('<4sIIIIIQQ', b'GDPC', 3, 4, 5, 1, 0, 0, 40 + len(data))
    directory = struct.pack('<II', 1, len(pb)) + pb + struct.pack('<QQ', 40, len(data)) + hashlib.md5(data).digest() + struct.pack('<I', 0)
    path.write_bytes(header + data + directory)
with tempfile.TemporaryDirectory(prefix='sts2-security-review-') as td:
    base = pathlib.Path(td)
    for case in ['normal', 'parent_traversal', 'absolute_path', 'symlink_escape']:
        area = base / case
        out = area / 'output'
        out.mkdir(parents=True)
        outside = area / 'outside'
        outside.mkdir()
        if case == 'normal':
            entry = 'res://normal.txt'
            target = out / 'normal.txt'
        elif case == 'parent_traversal':
            entry = 'res://../outside/marker.txt'
            target = outside / 'marker.txt'
        elif case == 'absolute_path':
            target = outside / 'marker.txt'
            entry = str(target)
        else:
            (out / 'link').symlink_to(outside, target_is_directory=True)
            entry = 'res://link/marker.txt'
            target = outside / 'marker.txt'
        pack = area / 'synthetic.pck'
        make_pack(pack, entry)
        run = subprocess.run([sys.executable, '-I', str(root/'tools/pck_ls.py'), str(pack), 'x', '', str(out)], capture_output=True, text=True, timeout=5)
        wrote = target.exists() and target.read_bytes() == b'SECURITY_REVIEW_SENTINEL\n'
        expected_secure = wrote if case == 'normal' else not wrote
        results.append({'check': case, 'expected_security_behavior': 'allow normal extraction' if case == 'normal' else 'reject write outside output', 'status': 'PASS' if expected_secure else 'FAIL', 'exit_code': run.returncode, 'marker_written': wrote})
syntax=[]
for p in sorted((root/'ios-export').rglob('*.sh')):
    r=subprocess.run(['/bin/bash','-n',str(p)],capture_output=True,text=True)
    syntax.append({'file':str(p.relative_to(root)),'status':'PASS' if r.returncode==0 else 'FAIL','detail':r.stderr})
for p in sorted(p for folder in ('tools', 'tests') for p in (root/folder).rglob('*.py')):
    try:
        ast.parse(p.read_text(), filename=str(p))
        syntax.append({'file':str(p.relative_to(root)),'status':'PASS'})
    except SyntaxError as e:
        syntax.append({'file':str(p.relative_to(root)),'status':'FAIL','detail':str(e)})
report={'extractor_checks':results,'syntax_checks':syntax,'scope':'Only the extractor was executed, with synthetic inputs inside a temporary directory. No build/install/sync scripts were executed. No personal game files, credentials, or devices were accessed.'}
dest=root/'ios-export/.work/security-verification.json'
dest.parent.mkdir(parents=True, exist_ok=True)
dest.write_text(json.dumps(report,indent=2)+'\n')
print(json.dumps(report,indent=2))

sys.exit(1 if any(x["status"] == "FAIL" for x in results + syntax) else 0)
