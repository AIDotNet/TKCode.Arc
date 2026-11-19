---
name: ratex-fbox-upstream-patch
description: native/patches 里的 \fbox 颜色补丁应提交上游；上游修复后要移除补丁并重新接线托管侧的判断
type: project
updated: 2026-10-16
source: init
evidence: [native/ratex/patches/0001-enclose-border-uses-current-color.patch, src/TKCode.Arc.StreamMark.RaTeX/RaTeXMathLayout.cs, docs/streammark/decisions.md]
---
`native/ratex/patches/0001-enclose-border-uses-current-color.patch` 把 RaTeX 的 `\fbox` / `\boxed` 边框从固定黑色改成当前颜色，`tools/streammark/build-ratex.sh` 等脚本在构建前自动应用；这个修复应当提交上游。

**Why:** 上游的固定黑色边框在深色主题下不可用，补丁是临时的；托管侧因此留下了「边框是否未着色」的判断逻辑。
**How to apply:** 升级 RaTeX（`native/ratex/ratex-upstream.json` 的提交）时先确认上游是否已包含该修复；包含则删除补丁，并同步清理托管侧依赖该补丁的代码路径，否则判断会与新的原生产物不一致。
