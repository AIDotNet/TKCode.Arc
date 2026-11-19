---
name: native-library-two-paths
description: 原生库有两条分发路径：NuGet 包的 runtimes/<rid>/native 与仓库内 artifacts/native/<rid>
type: project
updated: 2026-11-19
source: init
evidence: [build/Arc.Repository.targets, src/TKCode.Arc.StreamMark.RaTeX/TKCode.Arc.StreamMark.RaTeX.csproj, tools/streammark/sync-native.sh, docs/streammark/decisions.md]
---
对外分发时，RaTeX 库放进 NuGet 包的 `runtimes/<rid>/native/`（`tools/streammark/sync-native.sh` 复制进 `src/TKCode.Arc.StreamMark.RaTeX/runtimes/`），使用方按自己的 RID 自动选用；仓库内部通过项目引用使用它的可执行项目（Gallery、测试）设 `ArcStreamMarkNative=true`，由 `build/Arc.Repository.targets` 按其目标 RID 从 `artifacts/native/<rid>/` 复制。

**Why:** SDK 不会把 `RuntimeIdentifier` 传给被引用项目，所以包内机制在仓库内不生效；之前就是因此交叉发布 osx-x64 时复制了 arm64 的库。
**How to apply:** 改动原生库路径或复制规则时两条路径都要检查，并跑一次交叉发布；只改一边会让另一类使用方静默拿到错误的架构。
