"""
端到端测试脚本：下载数据 -> Diff -> Patch -> 校验
用法:
    python run_test.py [--skip-download] [--auto] [--help]
"""
import subprocess
import sys
import os
import hashlib

TESTDATA_DIR = os.path.join(os.path.dirname(__file__), "testdata")
V1_DIR = os.path.join(TESTDATA_DIR, "v1")
V2_DIR = os.path.join(TESTDATA_DIR, "v2")
DIFF_DIR = os.path.join(TESTDATA_DIR, "diff")
PATCHED_DIR = os.path.join(TESTDATA_DIR, "patched")

DIFF_EXE = os.path.join("DiffDemo", "bin", "Release", "net10.0", "DiffDemo")
PATCH_EXE = os.path.join("PatchDemo", "bin", "Release", "net10.0", "PatchDemo")

STEPS = ["下载测试数据", "构建项目", "生成 Diff", "应用 Patch", "校验结果"]

auto_mode = "--auto" in sys.argv

def print_help():
    print("端到端测试脚本：下载数据 -> Diff -> Patch -> 校验")
    print()
    print("用法: python run_test.py [选项]")
    print()
    print("选项:")
    print("  --auto            全自动运行，不暂停等待确认")
    print("  --skip-download   跳过下载测试数据步骤")
    print("  --help            显示此帮助信息")
    print()
    print("流程:")
    for i, s in enumerate(STEPS, 1):
        print(f"  {i}. {s}")

def step(index: int):
    name = STEPS[index]
    print(f"\n{'='*60}")
    print(f"  [{index+1}/{len(STEPS)}] {name}")
    print(f"{'='*60}\n")

def wait(current_index: int):
    if auto_mode:
        return
    next_name = STEPS[current_index + 1] if current_index + 1 < len(STEPS) else None
    if next_name:
        input(f"完成。下一步: {next_name}，按回车继续...")
    else:
        input("完成。按回车查看结果...")

def run(cmd: list[str], check=True):
    print(f"> {' '.join(cmd)}")
    result = subprocess.run(cmd, check=check)
    return result.returncode

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
        print_help()
        return

    # 1. 下载数据
    if "--skip-download" not in sys.argv:
        step(0)
        run([sys.executable, "download_testdata.py"])
        wait(0)

    # 2. 构建
    step(1)
    run(["dotnet", "build", "-c", "Release", "-f", "net10.0"])
    wait(1)

    # 3. Diff
    step(2)
    run([DIFF_EXE, V1_DIR, V2_DIR, DIFF_DIR])
    wait(2)

    # 4. Patch
    step(3)
    run([PATCH_EXE, V1_DIR, DIFF_DIR, PATCHED_DIR])
    wait(3)

    # 5. 校验
    step(4)
    passed = verify()

    print()
    if passed:
        print("测试通过!")
    else:
        print("测试失败!")
        sys.exit(1)

if __name__ == "__main__":
    main()
