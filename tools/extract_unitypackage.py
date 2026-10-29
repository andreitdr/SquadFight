#!/usr/bin/env python3
"""Extracts a .unitypackage (tar.gz of <guid>/{pathname,asset,asset.meta}) into the project, keeping GUIDs.
Usage: tools/extract_unitypackage.py <package> [project_root]"""
import os, sys, tarfile
pkg, root = sys.argv[1], (sys.argv[2] if len(sys.argv) > 2 else ".")
entries = {}
with tarfile.open(pkg, "r:gz") as t:
    for m in t.getmembers():
        if not m.isfile(): continue
        guid, _, name = m.name.lstrip("./").partition("/")
        entries.setdefault(guid, {})[name] = t.extractfile(m).read()
count = 0
for guid, files in entries.items():
    if "pathname" not in files: continue
    path = files["pathname"].decode("utf-8").splitlines()[0].strip()
    if not path.startswith("Assets/") or ".." in path: continue
    dest = os.path.join(root, path)
    if "asset" in files:
        os.makedirs(os.path.dirname(dest), exist_ok=True)
        if not os.path.exists(dest):
            open(dest, "wb").write(files["asset"]); count += 1
    else:
        os.makedirs(dest, exist_ok=True)
    if "asset.meta" in files and not os.path.exists(dest + ".meta"):
        open(dest + ".meta", "wb").write(files["asset.meta"])
print(f"extracted {count} files")
