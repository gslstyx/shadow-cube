#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
Shadow Cube 项目统计台账：自动统计 → 与文档标记值比对 → 回写。

动机（见 docs/08 §4.8「文档腐化自检清单」）：
    手写汇总表必然过期。本项目已多次出现同一事实在多处不一致
    （测试条数 04=99 / 06=73 / 实测 135；LFS 路径 §5.1 与 §10 不一致等）。
    因此把「可被机器统计的数字」交给机器，文档只保留标记位。

托管方式：
    文档中用成对标记包裹可被托管的值：
        <!-- STAT:EDIT_COUNT -->53<!-- /STAT:EDIT_COUNT -->
    本脚本统计出真值后，替换标记之间的内容。

用法：
    python3 tools/stats.py --check                        # 只比对（退出码 0=一致，2=有差异）
    python3 tools/stats.py --check --edit=59 --play=76    # 带入本次实测的测试条数
    python3 tools/stats.py --sync  [--edit=59 --play=76]  # 回写到文档标记位

统计口径：
    CS_TOTAL / CS_RUNTIME / CS_TESTS   Assets 下 *.cs 行数（运行时+编辑器 / 测试）
    CS_RATIO                           测试行数占比
    EDIT_COUNT / PLAY_COUNT            Unity Test Framework 结果 XML 的 total（EditMode / PlayMode）
    TOTAL_TESTS                        两者之和
    PROB_ENV                           docs/06 中 PROB-ENV-* 去重条数
    PROB_BUILD                         PROB-BUILD-*
    PROB_CODE                          PROB-CODE-* + PROB-RENDER-*
    PROB_LOGIC                         PROB-LOGIC-* + PROB-TEST-*
    PROB_TOTAL                         以上合计
"""

import argparse
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
ASSETS = ROOT / "ShadowCube" / "Assets"
DOCS = ROOT / "docs"
SUMMARY_DOC = DOCS / "06_开发总结.md"

# 统计名 → 归入该类的 PROB 前缀
PROB_CATEGORIES = {
    "PROB_ENV": ("ENV",),
    "PROB_BUILD": ("BUILD",),
    "PROB_CODE": ("CODE", "RENDER"),
    "PROB_LOGIC": ("LOGIC", "TEST"),
}

ANY_MARK_RE = re.compile(
    r"(<!--\s*STAT:([A-Z_]+)\s*-->)(.*?)(<!--\s*/STAT:\2\s*-->)", re.S
)


# ── 统计 ────────────────────────────────────────────────────────
def cs_lines(base: Path) -> int:
    if not base.exists():
        return 0
    total = 0
    for p in base.rglob("*.cs"):
        try:
            with p.open(encoding="utf-8", errors="ignore") as f:
                total += sum(1 for _ in f)
        except OSError:
            continue
    return total


def prob_counts() -> dict:
    """按编号去重统计 docs/06 中的问题条目。"""
    if not SUMMARY_DOC.exists():
        return {}
    text = SUMMARY_DOC.read_text(encoding="utf-8")
    found: dict[str, set] = {}
    for m in re.finditer(r"【PROB-([A-Z]+)-(\d+)】", text):
        found.setdefault(m.group(1), set()).add(int(m.group(2)))
    res = {k: sum(len(found.get(c, ())) for c in cats) for k, cats in PROB_CATEGORIES.items()}
    res["PROB_TOTAL"] = sum(res[k] for k in PROB_CATEGORIES)
    return res


def xml_total(platform: str):
    """读取上次测试结果 XML 的 total；不存在返回 None。"""
    f = Path("/tmp/sc_tests_%s.xml" % platform)
    if not f.exists():
        return None
    try:
        t = f.read_text(encoding="utf-8", errors="ignore")
    except OSError:
        return None
    m = re.search(r'total="(\d+)"', t)
    return int(m.group(1)) if m else None


def expected(args) -> dict:
    edit = int(args.edit) if getattr(args, "edit", None) else xml_total("EditMode")
    play = int(args.play) if getattr(args, "play", None) else xml_total("PlayMode")

    runtime = cs_lines(ASSETS / "Scripts") + cs_lines(ASSETS / "Editor")
    tests = cs_lines(ASSETS / "Tests")
    total = runtime + tests

    exp = {
        "CS_RUNTIME": runtime,
        "CS_TESTS": tests,
        "CS_TOTAL": total,
        "CS_RATIO": ("%.1f%%" % (100.0 * tests / total)) if total else "0.0%",
    }
    if edit:
        exp["EDIT_COUNT"] = edit
    if play:
        exp["PLAY_COUNT"] = play
    if edit or play:
        exp["TOTAL_TESTS"] = (edit or 0) + (play or 0)
    exp.update(prob_counts())
    return exp


# ── 标记扫描 / 回写 ──────────────────────────────────────────────
def scan_marks() -> dict:
    """{统计名: [(文件, 当前值), ...]}"""
    marks: dict[str, list] = {}
    for f in sorted(DOCS.glob("*.md")):
        try:
            text = f.read_text(encoding="utf-8")
        except OSError:
            continue
        for m in ANY_MARK_RE.finditer(text):
            marks.setdefault(m.group(2), []).append((f, m.group(3)))
    return marks


def sync(exp: dict) -> int:
    """把真值写回所有文档标记位，返回改动处数。"""
    changed = 0
    for f in sorted(DOCS.glob("*.md")):
        try:
            text = f.read_text(encoding="utf-8")
        except OSError:
            continue

        def repl(m):
            nonlocal changed
            name = m.group(2)
            if name not in exp:
                return m.group(0)
            new = str(exp[name])
            if m.group(3) == new:
                return m.group(0)
            changed += 1
            return m.group(1) + new + m.group(4)

        new_text = ANY_MARK_RE.sub(repl, text)
        if new_text != text:
            f.write_text(new_text, encoding="utf-8")
    return changed


def main() -> int:
    ap = argparse.ArgumentParser(description="项目统计台账（自动统计 / 比对 / 回写）")
    ap.add_argument("--check", action="store_true", help="只比对，不修改文档")
    ap.add_argument("--sync", action="store_true", help="回写到文档标记位")
    ap.add_argument("--edit", help="本次实测 EditMode 用例数（覆盖 XML 读取）")
    ap.add_argument("--play", help="本次实测 PlayMode 用例数（覆盖 XML 读取）")
    ap.add_argument("--quiet", action="store_true", help="回写时精简输出")
    args = ap.parse_args()

    if not (args.check or args.sync):
        args.check = True

    exp = expected(args)
    marks = scan_marks()

    if args.sync:
        n = sync(exp)
        if not args.quiet:
            print("[统计] 已回写 %d 处标记" % n)
            for k in sorted(exp):
                print("       %-12s = %s" % (k, exp[k]))
        return 0

    # --check
    diffs, same = [], 0
    for name, occurrences in sorted(marks.items()):
        if name not in exp:
            diffs.append((name, "?", "?", "文档中的标记无对应统计口径"))
            continue
        want = str(exp[name])
        for f, cur in occurrences:
            if cur.strip() == want:
                same += 1
            else:
                diffs.append((name, cur.strip() or "(空)", want, f.name))

    if diffs:
        print("[统计] 文档统计值与实测不一致（%d 处）：" % len(diffs))
        for name, cur, want, where in diffs:
            print("       %-12s 文档=%s  实测=%s   @ %s" % (name, cur, want, where))
        print("[统计] 修复方式：./tools/healthcheck.sh --sync-docs")
        return 2

    print("[统计] 全部一致（%d 处标记）" % same)
    for k in sorted(exp):
        print("       %-12s = %s" % (k, exp[k]))
    return 0


if __name__ == "__main__":
    sys.exit(main())
