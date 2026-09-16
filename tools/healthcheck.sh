#!/usr/bin/env bash
# Shadow Cube 项目体检脚本
#
# 用法：
#   ./tools/healthcheck.sh             # 快速体检（编译 + 单测 + Git 卫生 + 文档 + 统计台账）
#   ./tools/healthcheck.sh --build     # 完整体检（额外跑一次联调包构建，约 20~30 分钟）
#   ./tools/healthcheck.sh --skip-tests  # 只查编译与环境（测试条数沿用上次结果）
#   ./tools/healthcheck.sh --sync-docs   # 把自动统计值回写文档（测试条数/代码行数/问题条数）
#
# 退出码：0=通过  1=存在失败项

set -uo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
PROJECT="$ROOT/ShadowCube"
UNITY="${UNITY_PATH:-/Applications/Unity/Hub/Editor/6000.3.24f1/Unity.app/Contents/MacOS/Unity}"
BUILD=0; SKIP_TESTS=0; SYNC_DOCS=0
for arg in "$@"; do
  case "$arg" in
    --build)       BUILD=1 ;;
    --skip-tests)  SKIP_TESTS=1 ;;
    --sync-docs)   SYNC_DOCS=1 ;;
  esac
done
EDIT_TOTAL=""; PLAY_TOTAL=""

PASS=0; WARN=0; FAIL=0
ok()   { echo "  ✅ $1"; PASS=$((PASS+1)); }
warn() { echo "  ⚠️  $1"; WARN=$((WARN+1)); }
bad()  { echo "  ❌ $1"; FAIL=$((FAIL+1)); }

echo "================ 项目体检 ================"
echo "时间: $(date '+%Y-%m-%d %H:%M:%S')   工程: $PROJECT"
echo

# ── 1. 工具链 ────────────────────────────────────────────────
echo "[1] 工具链"
command -v git >/dev/null && ok "git $(git --version | awk '{print $3}')" || bad "git 未安装"
if command -v git-lfs >/dev/null; then ok "git-lfs $(git-lfs version | awk '{print $1}')"; else warn "git-lfs 未安装"; fi
[[ -x "$UNITY" ]] && ok "Unity 存在" || bad "Unity 不存在：$UNITY"
[[ -f "$PROJECT/BuildConfig/build.json" ]] && ok "BuildConfig/build.json 存在" || warn "缺少 BuildConfig/build.json"
echo

# ── 2. 脚本编译 ──────────────────────────────────────────────
echo "[2] C# 脚本编译"
if [[ ! -x "$UNITY" ]]; then
  warn "跳过（无 Unity）"
else
  LOG="/tmp/sc_healthcheck_$(date +%s).log"
  "$UNITY" -batchmode -quit -projectPath "$PROJECT" \
    -executeMethod ShadowCube.EditorTools.ShadowCubeBuildPipeline.ApplyBuildConfig \
    -logFile "$LOG" >/dev/null 2>&1
  if grep -qE "error CS" "$LOG"; then
    bad "存在编译错误："
    grep -E "error CS" "$LOG" | sort -u | head -5 | sed 's/^/       /'
  else
    ok "编译通过（0 error CS）"
  fi
  rm -f "$LOG"
fi
echo

# ── 2.5 单元测试（EditMode）──────────────────────────────────
if (( SKIP_TESTS == 1 )); then
  echo "[2.5] 单元测试：跳过（--skip-tests，条数沿用上次结果）"
else
  echo "[2.5] 单元测试（EditMode / PlayMode）"
  if [[ ! -x "$UNITY" ]]; then
    warn "跳过（无 Unity）"
  else
    for PLATFORM in EditMode PlayMode; do
      RES="/tmp/sc_tests_${PLATFORM}.xml"
      rm -f "$RES"
      "$UNITY" -batchmode -runTests -testPlatform "$PLATFORM" -projectPath "$PROJECT" \
        -testResults "$RES" -logFile "/tmp/sc_tests_${PLATFORM}.log" >/dev/null 2>&1
      if [[ ! -f "$RES" ]]; then
        warn "$PLATFORM：未生成测试结果（可能未安装 Test Framework）"
        continue
      fi
      SUMMARY=$(python3 -c "
import re
t=open('$RES',encoding='utf-8',errors='ignore').read()
m=re.search(r'total=\"(\d+)\" passed=\"(\d+)\" failed=\"(\d+)\"',t)
print(' '.join(m.groups()) if m else '? ? ?')" 2>/dev/null)
      TOTAL=$(echo "$SUMMARY" | awk '{print $1}')
      PASSED=$(echo "$SUMMARY" | awk '{print $2}')
      FAILED=$(echo "$SUMMARY" | awk '{print $3}')
      if [[ "$PLATFORM" == "EditMode" ]]; then EDIT_TOTAL="$TOTAL"; else PLAY_TOTAL="$TOTAL"; fi
      if [[ "$FAILED" == "0" ]]; then
        ok "$PLATFORM 测试通过 $PASSED/$TOTAL"
      else
        bad "$PLATFORM 测试失败 $FAILED/$TOTAL"
        python3 -c "
import re
t=open('$RES',encoding='utf-8',errors='ignore').read()
for n,r in re.findall(r'<test-case[^>]*fullname=\"([^\"]+)\"[^>]*result=\"(Failed)\"',t)[:5]:
    print('       ✗', n)" 2>/dev/null
      fi
    done
  fi
fi
echo

# ── 3. Git 卫生 ──────────────────────────────────────────────
echo "[3] Git 卫生"
BRANCH=$(git -C "$ROOT" rev-parse --abbrev-ref HEAD 2>/dev/null)
ok "当前分支：$BRANCH"

# 敏感文件
SENSITIVE=$(git -C "$ROOT" status --porcelain 2>/dev/null | grep -iE "keystore|\.jks|\.key$|BuildConfig/keystore.json" || true)
if [[ -n "$SENSITIVE" ]]; then bad "存在密钥类文件变更，禁止提交：$SENSITIVE"; else ok "无密钥文件入库风险"; fi

# 大文件是否走 LFS
STAGED=$(git -C "$ROOT" diff --cached --name-only 2>/dev/null)
BIG_MISS=""
for f in $STAGED; do
  [[ -f "$ROOT/$f" ]] || continue
  SIZE=$(stat -f%z "$ROOT/$f" 2>/dev/null || echo 0)
  if (( SIZE > 5242880 )); then
    ATTR=$(git -C "$ROOT" check-attr filter -- "$f" 2>/dev/null | grep -o "filter: lfs" || true)
    [[ -z "$ATTR" ]] && BIG_MISS="$BIG_MISS $f"
  fi
done
[[ -n "$BIG_MISS" ]] && warn "大于 5MB 且未走 LFS：$BIG_MISS" || ok "大文件 LFS 规则正常"

# 未跟踪文件
UNTRACKED=$(git -C "$ROOT" status --porcelain 2>/dev/null | grep -c "^??" || true)
(( UNTRACKED > 0 )) && warn "有 $UNTRACKED 个未跟踪文件（确认是否需入库）" || ok "无未跟踪残留"
echo

# ── 4. 文档同步 ──────────────────────────────────────────────
echo "[4] 文档"
for d in 01_需求文档 02_开发环境文档 03_打包发布流程文档 04_测试文档 \
         05_开发协作约定 06_开发总结 07_玩法扩展评估 08_AI游戏项目经验沉淀; do
  [[ -f "$ROOT/docs/${d}.md" ]] && ok "${d}.md" || warn "缺少 docs/${d}.md"
done
echo

# ── 4.5 统计台账（自动统计，防止手写数字腐化）─────────────────
# 动机见 docs/08 §4.8：手写汇总表必然过期（本项目已多次出现同一事实多处不一致）
echo "[4.5] 统计台账"
if command -v python3 >/dev/null; then
  STAT_ARGS=(--edit="$EDIT_TOTAL" --play="$PLAY_TOTAL")
  if (( SYNC_DOCS == 1 )); then
    python3 "$ROOT/tools/stats.py" --sync "${STAT_ARGS[@]}" | sed 's/^/  /'
    ok "已回写文档统计标记"
  else
    if python3 "$ROOT/tools/stats.py" --check "${STAT_ARGS[@]}" | sed 's/^/  /'; then
      ok "文档统计值与实测一致"
    else
      warn "文档统计值已过期（详见上方差异，执行 --sync-docs 回写）"
    fi
  fi
else
  warn "未安装 python3，跳过统计台账"
fi
echo

# ── 5. 构建（可选）───────────────────────────────────────────
if (( BUILD == 1 )); then
  echo "[5] 联调包构建（--build）"
  if "$ROOT/tools/build.sh debug" >/tmp/sc_build_check.log 2>&1; then
    ok "构建成功：$(cat "$PROJECT/Builds/Android/last-build.txt" 2>/dev/null)"
  else
    bad "构建失败，详见 /tmp/sc_build_check.log"
  fi
  echo
fi

# ── 汇总 ─────────────────────────────────────────────────────
echo "================ 体检结果 ================"
echo "  通过 $PASS ｜ 警告 $WARN ｜ 失败 $FAIL"
if (( FAIL > 0 )); then
  echo "  结论：未通过，修复后再提交/推送"
  exit 1
fi
echo "  结论：通过（警告项请按需处理）"
exit 0
