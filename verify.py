"""
校验脚本：对比 v2 和 patched 目录的文件是否一致
用法:
    python run_test.py [--help]
"""
import sys
import os
import hashlib

TESTDATA_DIR = os.path.join(os.path.dirname(__file__), "testdata")
V1_DIR = os.path.join(TESTDATA_DIR, "v1")
V2_DIR = os.path.join(TESTDATA_DIR, "v2")
DIFF_DIR = os.path.join(TESTDATA_DIR, "diff")
PATCHED_DIR = os.path.join(TESTDATA_DIR, "patched")

def file_hash(path: str) -> str:
    h = hashlib.md5()
    with open(path, "rb") as f:
        for chunk in iter(lambda: f.read(8192), b""):
            h.update(chunk)
    return h.hexdigest()

def verify():
    v2_files = {}
    for root, _, files in os.walk(V2_DIR):
        for f in files:
            full = os.path.join(root, f)
            rel = os.path.relpath(full, V2_DIR)
            v2_files[rel] = full

    patched_files = {}
    for root, _, files in os.walk(PATCHED_DIR):
        for f in files:
            full = os.path.join(root, f)
            rel = os.path.relpath(full, PATCHED_DIR)
            patched_files[rel] = full

    all_files = set(v2_files.keys()) | set(patched_files.keys())
    ok = 0
    mismatch = 0
    only_v2 = []
    only_patched = []

    for rel in sorted(all_files):
        if rel not in v2_files:
            only_patched.append(rel)
        elif rel not in patched_files:
            only_v2.append(rel)
        else:
            h1 = file_hash(v2_files[rel])
            h2 = file_hash(patched_files[rel])
            if h1 == h2:
                ok += 1
            else:
                v2_size = os.path.getsize(v2_files[rel])
                patched_size = os.path.getsize(patched_files[rel])
                print(f"  MISMATCH: {rel}")
                print(f"    v2:      {h1} ({v2_size:,} bytes)")
                print(f"    patched: {h2} ({patched_size:,} bytes)")
                mismatch += 1

    if only_v2:
        print(f"\n  仅在 v2 中 ({len(only_v2)} 个):")
        for rel in only_v2:
            print(f"    {rel}")

    if only_patched:
        print(f"\n  仅在 patched 中 ({len(only_patched)} 个):")
        for rel in only_patched:
            print(f"    {rel}")

    print(f"\n结果: {ok} 一致, {mismatch} 不一致, {len(only_v2)} 仅v2, {len(only_patched)} 仅patched")
    return mismatch == 0 and len(only_v2) == 0 and len(only_patched) == 0

def main():
    if "--help" in sys.argv:
        print("校验脚本：对比 v2 和 patched 目录的文件是否一致")
        print()
        print("用法: python run_test.py [--help]")
        return

    passed = verify()

    print()
    if passed:
        print("测试通过!")
    else:
        print("测试失败!")
        sys.exit(1)

if __name__ == "__main__":
    main()
