"""
根据 testdata 中的 old.csv 和 new.csv 下载对应的 bundle 文件。
用法:
    python download_testdata.py [--workers N]
"""
import sys
import os
import csv
import requests
from concurrent.futures import ThreadPoolExecutor, as_completed
from tqdm import tqdm

BASE_URL = "https://line3-patch-blhx.bilibiligame.net/android/resource/"
TESTDATA_DIR = os.path.join(os.path.dirname(__file__), "testdata")

def load_csv(path: str) -> list[tuple[str, str, str]]:
    rows = []
    with open(path, newline="", encoding="utf-8") as f:
        for row in csv.reader(f):
            if len(row) >= 3:
                rows.append((row[0], row[1], row[2]))
    return rows

def download_file(name: str, md5: str, output_dir: str) -> tuple[str, bool, str]:
    url = BASE_URL + md5
    file_path = os.path.join(output_dir, name)
    if os.path.exists(file_path):
        return name, True, "skipped"
    os.makedirs(os.path.dirname(file_path), exist_ok=True)
    try:
        r = requests.get(url, timeout=30)
        r.raise_for_status()
        with open(file_path, "wb") as f:
            f.write(r.content)
        return name, True, "ok"
    except Exception as e:
        return name, False, str(e)

def main():
    if len(sys.argv) < 1:
        print(f"用法: python {sys.argv[0]} [--workers N]")
        return

    workers = 8
    if "--workers" in sys.argv:
        idx = sys.argv.index("--workers")
        workers = int(sys.argv[idx + 1])

    data_dir = TESTDATA_DIR
    if not os.path.exists(data_dir):
        print(f"目录不存在: {data_dir}")
        return

    old_rows = load_csv(os.path.join(data_dir, "old.csv"))
    new_rows = load_csv(os.path.join(data_dir, "new.csv"))

    old_dir = os.path.join(data_dir, "v1")
    new_dir = os.path.join(data_dir, "v2")

    tasks = []
    for name, size, md5 in old_rows:
        tasks.append((name, md5, old_dir))
    for name, size, md5 in new_rows:
        tasks.append((name, md5, new_dir))

    print(f"旧版本: {len(old_rows)} 个文件 -> {old_dir}")
    print(f"新版本: {len(new_rows)} 个文件 -> {new_dir}")
    print(f"并发数: {workers}")
    print()

    success = 0
    failed = 0
    skipped = 0

    with ThreadPoolExecutor(max_workers=workers) as pool:
        futures = {pool.submit(download_file, name, md5, out_dir): name for name, md5, out_dir in tasks}
        with tqdm(total=len(futures), desc="下载中") as pbar:
            for future in as_completed(futures):
                name, ok, msg = future.result()
                if msg == "skipped":
                    skipped += 1
                elif ok:
                    success += 1
                else:
                    failed += 1
                    tqdm.write(f"  ✗ {name}: {msg}")
                pbar.update(1)

    print(f"\n完成: {success} 下载, {skipped} 跳过, {failed} 失败")

if __name__ == "__main__":
    main()
