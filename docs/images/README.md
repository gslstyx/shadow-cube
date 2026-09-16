# 参考图 / 截图存档

用于后续复盘的视觉参考资料。图片由 Git LFS 管理（`.gitattributes` 已覆盖 `*.png` / `*.jpg`）。

## 命名约定

| 前缀 | 用途 | 示例 |
|---|---|---|
| `reference-*` | 竞品 / 目标效果参考图 | `reference-01-light-theme.jpg` |
| `prototype-*` | 本项目原型截图（按里程碑） | `prototype-m1-walls.png` |
| `bug-*` | 缺陷复现截图 | `bug-build-01-no-walls.png` |

## 待补

| 文件 | 说明 | 状态 |
|---|---|---|
| `reference-01-light-theme.jpg` | 浅色系参考图（白墙 / 黑方块 / 黑投影，等距视角） | ⏳ 待放入（附件未落盘，需手动保存到本目录） |
| `prototype-m1-walls.png` | M1 两面投影墙显形后的画面（无头渲染，2026-09-16） | ✅ 已生成 |

> `prototype-*.png` 由 `ShadowCube/Assets/Editor/ShadowCubeScreenshot.cs` 无头渲染生成（不需要开编辑器、不需要真机）：
> `Unity -batchmode -projectPath ShadowCube -executeMethod ShadowCube.EditorTools.ShadowCubeScreenshot.Capture`
