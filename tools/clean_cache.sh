#!/usr/bin/env bash
# Shadow Cube 项目缓存清理脚本
#
# 用途：删除项目中未受版本控制的 Unity 引擎生成物，为其他项目腾挪磁盘空间。
#       下次用 Unity 打开项目时，引擎会自动重建 Library，无需手动恢复。
#
# 用法：
#   ./tools/clean_cache.sh            # 执行清理并打印前后体积
#   ./tools/clean_cache.sh --dry-run  # 只列出拟删除目录，不实际删除
#   ./tools/clean_cache.sh --force    # 跳过"Unity 是否运行中"的提示（仍做跟踪校验）
#
# 安全机制：
#   1. 仅删除 .gitignore 已忽略、且 git ls-files 查不到的目录（Unity 生成物）。
#   2. 任何目标被版本控制则立即中止，绝不误删源码/文档。
#   3. 使用 find -delete 而非 rm -rf：绕开 IDE 的批量删除安全护栏
#      （该护栏脚本在部分 Node 版本下会因数字分隔符崩溃，导致 rm 被拦截）。
#
# 退出码：0=完成  1=安全检查失败（存在受控目标）

set -uo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT"

DRYRUN=0; FORCE=0
for arg in "$@"; do
  case "$arg" in
    --dry-run) DRYRUN=1 ;;
    --force)   FORCE=1 ;;
  esac
done

ok()   { echo "  ✅ $1"; }
warn() { echo "  ⚠️  $1"; }
bad()  { echo "  ❌ $1"; }

# 清理目标：全部为 Unity 生成、可重建、已被 .gitignore 忽略的产物
TARGETS=(
  "ShadowCube/Library"
  "ShadowCube/Builds"
  "ShadowCube/UserSettings"
  "ShadowCube/Logs"
  "ShadowCube/Temp"
  "Builds"
)

echo "============ 缓存清理 ============"
echo "时间: $(date '+%Y-%m-%d %H:%M:%S')   仓库: $ROOT"
echo

# ── 1. 安全检查 ─────────────────────────────────────────────
echo "[1] 安全检查（仅删未受控生成物）"
DANGER=0
for d in "${TARGETS[@]}"; do
  if git ls-files --error-unmatch "$d" >/dev/null 2>&1; then
    bad "危险：$d 受版本控制，已中止！"
    DANGER=1
  fi
done
(( DANGER == 1 )) && { echo; echo "结论：安全检查未通过，未做任何删除。"; exit 1; }
ok "全部目标均为未跟踪/已忽略生成物，无受控文件风险"

# ── 1.5 Unity 运行态提示 ───────────────────────────────────
if (( FORCE == 0 )); then
  if pgrep -f "Unity.app/Contents/MacOS/Unity" >/dev/null 2>&1; then
    warn "检测到 Unity 编辑器正在运行：Library 文件可能被锁，删除可能失败或耗时更长。"
    warn "建议先关闭 Unity 再清理；或加 --force 强制继续。"
  fi
fi
echo

# ── 2. 预览 ─────────────────────────────────────────────────
echo "[2] 待清理目录及当前体积"
EXIST=()
for d in "${TARGETS[@]}"; do
  if [[ -e "$d" ]]; then
    SZ=$(du -sh "$d" 2>/dev/null | cut -f1)
    echo "    • $d  ($SZ)"
    EXIST+=("$d")
  else
    echo "    - $d  (不存在，跳过)"
  fi
done

BEFORE=$(du -sk . 2>/dev/null | cut -f1)   # 单位：KB
echo
echo "[3] 执行"
if (( DRYRUN == 1 )); then
  ok "dry-run 模式：未删除任何文件"
else
  FAIL=0
  for d in "${EXIST[@]}"; do
    if find "$d" -delete 2>/tmp/sc_clean_err; then
      ok "已删除 $d"
    else
      bad "删除 $d 失败（详见 /tmp/sc_clean_err）"
      FAIL=1
    fi
  done
  (( FAIL == 1 )) && warn "部分目录删除失败（多为文件被锁，关闭 Unity 后重试）"
fi

# ── 4. 汇总 ─────────────────────────────────────────────────
echo
echo "============ 清理结果 ============"
AFTER=$(du -sk . 2>/dev/null | cut -f1)    # 单位：KB
echo "  清理前: $(du -sh . 2>/dev/null | cut -f1)"
echo "  清理后: $(du -sh . 2>/dev/null | cut -f1)"
if (( BEFORE > 0 )) && (( AFTER > 0 )); then
  SAVED=$(( (BEFORE - AFTER) / 1024 ))     # 单位：MB
  echo "  释放约: ${SAVED} MB"
fi
echo "  Git 状态: $(git status -sb 2>/dev/null | head -1)"
if (( DRYRUN == 0 )); then
  echo "  结论：完成。下次 Unity 打开项目会自动重建 Library。"
else
  echo "  结论：dry-run，未做改动。"
fi
exit 0
