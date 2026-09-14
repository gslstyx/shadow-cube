#!/usr/bin/env bash
# Shadow Cube 一键打包脚本（macOS）
#
# 用法：
#   ./tools/build.sh debug      # 联调测试包：APK / ARMv7+ARM64+x86_64 / 可调试（真机 + 模拟器）
#   ./tools/build.sh release    # 正式包：AAB / ARM64+x86_64 / 签名（上架 Google Play）
#
# 可选环境变量：
#   UNITY_PATH  指定 Unity 可执行文件路径
#   BOUNCE=1    构建完成后在 Finder 中打开产物目录

set -euo pipefail

PROFILE="${1:-debug}"
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
PROJECT="$ROOT/ShadowCube"
UNITY="${UNITY_PATH:-/Applications/Unity/Hub/Editor/6000.3.24f1/Unity.app/Contents/MacOS/Unity}"
METHOD_NAMESPACE="ShadowCube.EditorTools.ShadowCubeBuildPipeline"

case "$PROFILE" in
  debug|dev|test)   METHOD="BuildDevelopment"; LABEL="联调测试包 (APK)" ;;
  release|prod)     METHOD="BuildRelease";     LABEL="正式包 (AAB)" ;;
  *) echo "用法: $0 {debug|release}"; exit 1 ;;
esac

if [[ ! -x "$UNITY" ]]; then
  echo "❌ 找不到 Unity：$UNITY"
  echo "   可用 UNITY_PATH=/path/to/Unity ./tools/build.sh $PROFILE 指定"
  exit 1
fi

echo "==> 工程：$PROJECT"
echo "==> 模式：$LABEL"
"$UNITY" -batchmode -quit \
  -projectPath "$PROJECT" \
  -executeMethod "${METHOD_NAMESPACE}.${METHOD}" \
  -logFile "$ROOT/Builds/unity-build-$(date +%Y%m%d_%H%M%S).log"

LAST_BUILD="$PROJECT/Builds/Android/last-build.txt"
if [[ -f "$LAST_BUILD" ]]; then
  echo "✅ 构建成功，产物：$(cat "$LAST_BUILD")"
  [[ "${BOUNCE:-0}" == "1" ]] && open "$PROJECT/Builds/Android"
else
  echo "❌ 构建失败，请查看 $ROOT/Builds/ 下的日志"
  exit 1
fi
