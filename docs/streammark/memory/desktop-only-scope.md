---
name: desktop-only-scope
description: 平台范围只有 win/linux/osx × x64/arm64，移动端是有意不做
type: project
updated: 2026-10-16
source: init
evidence: [docs/streammark/decisions.md]
---
平台范围是六个桌面 RID；移动端不投入，因为 TextMateSharp 的 Oniguruma 没有 iOS / Android 构建（`docs/streammark/decisions.md` 的「平台」一行）。六个 RID 在 `tools/streammark/build-ratex.sh <rid>` 与 CI 的 `native-ratex` 矩阵里各有一条。

**Why:** 不做移动端是当时权衡后的决定，不是遗漏。
**How to apply:** 不要为 iOS / Android 增加条件编译分支或新 RID；新增桌面 RID 时记得同时更新交叉编译表和 CI 矩阵（详见 [native-ratex 页](../codemap/modules/native-ratex.md)）。
