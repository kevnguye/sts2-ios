#!/usr/bin/env python3
"""List/extract files in a Godot 4.x .pck (format v2/v3, unencrypted)."""
import struct, sys, os, stat, secrets

def extraction_parts(path):
    if path.startswith('res://'):
        path = path[6:]
    elif path.startswith('user://'):
        path = 'user/' + path[7:]
    parts = path.split('/')
    if not path or '\\' in path or ':' in path or '\x00' in path or any(
        p in ('', '.', '..') for p in parts
    ):
        raise ValueError(f'unsafe archive path: {path!r}')
    return parts

def extract_file(pack, entry, root_fd):
    """Walk relative to open directory descriptors; never follow archive symlinks."""
    path, offset, size, _ = entry
    parts = extraction_parts(path)
    directory = os.dup(root_fd)
    temporary = None
    try:
        for part in parts[:-1]:
            try:
                os.mkdir(part, mode=0o700, dir_fd=directory)
            except FileExistsError:
                pass
            child = os.open(part, os.O_RDONLY | os.O_DIRECTORY | os.O_NOFOLLOW, dir_fd=directory)
            os.close(directory)
            directory = child
        try:
            existing = os.stat(parts[-1], dir_fd=directory, follow_symlinks=False)
            if not stat.S_ISREG(existing.st_mode):
                raise ValueError(f'non-regular extraction target: {path!r}')
        except FileNotFoundError:
            pass
        # Replace the directory entry rather than truncating an existing hard link.
        temporary = '.pck-' + secrets.token_hex(16)
        fd = os.open(temporary, os.O_WRONLY | os.O_CREAT | os.O_EXCL | os.O_NOFOLLOW,
                     0o600, dir_fd=directory)
        with os.fdopen(fd, 'wb') as output:
            pack.seek(offset)
            remaining = size
            while remaining:
                chunk = pack.read(min(remaining, 1024 * 1024))
                if not chunk:
                    raise ValueError(f'truncated archive entry: {path!r}')
                output.write(chunk)
                remaining -= len(chunk)
        os.replace(temporary, parts[-1], src_dir_fd=directory, dst_dir_fd=directory)
        temporary = None
    finally:
        if temporary is not None:
            os.unlink(temporary, dir_fd=directory)
        os.close(directory)

def u32(f): return struct.unpack("<I", f.read(4))[0]
def u64(f): return struct.unpack("<Q", f.read(8))[0]

def read_dir(f):
    magic = f.read(4)
    assert magic == b"GDPC", f"bad magic {magic}"
    fmt = u32(f)
    ver = (u32(f), u32(f), u32(f))
    flags = u32(f)
    files_base = u64(f)
    dir_offset = u64(f) if fmt >= 3 else 0
    print(f"# pck format v{fmt}, godot {ver[0]}.{ver[1]}.{ver[2]}, flags={flags:#x}, files_base={files_base:#x}, dir_offset={dir_offset:#x}", file=sys.stderr)
    assert not (flags & 1), "directory is encrypted!"
    if fmt >= 3:
        f.seek(dir_offset)
    else:
        f.seek(16 * 4, 1)  # reserved
    count = u32(f)
    entries = []
    for _ in range(count):
        plen = u32(f)
        path = f.read(plen).rstrip(b"\x00").decode("utf-8")
        off = u64(f)
        size = u64(f)
        f.read(16)  # md5
        fflags = u32(f) if fmt >= 2 else 0
        if flags & 2:  # PACK_REL_FILEBASE
            off += files_base
        entries.append((path, off, size, fflags))
    return entries

def main():
    pck = sys.argv[1]
    mode = sys.argv[2] if len(sys.argv) > 2 else "ls"
    with open(pck, "rb") as f:
        entries = read_dir(f)
        if mode == "ls":
            for p, o, s, fl in entries:
                enc = " [ENCRYPTED]" if fl & 1 else ""
                print(f"{s:>12} {p}{enc}")
        elif mode == "x":  # extract paths matching substring argv[3] into argv[4]
            pat, out = sys.argv[3], sys.argv[4]
            selected = [e for e in entries if pat in e[0] and not e[3] & 1]
            length = os.fstat(f.fileno()).st_size
            for p, o, s, _ in selected:
                extraction_parts(p)
                if o > length or s > length - o:
                    raise ValueError(f'entry outside archive: {p!r}')
            os.makedirs(out, mode=0o700, exist_ok=True)
            root_fd = os.open(out, os.O_RDONLY | os.O_DIRECTORY | os.O_NOFOLLOW)
            try:
                for entry in selected:
                    extract_file(f, entry, root_fd)
                    print(f'extracted {entry[0]} into {out}')
            finally:
                os.close(root_fd)

if __name__ == "__main__":
    main()
