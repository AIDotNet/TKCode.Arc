---
name: diff-view-in-flight
description: 2026-10-16 正在加入代码块的差异视图（统一/并排），它会收紧视图复用与块状态的不变量
type: project
updated: 2026-10-16
source: init
evidence: [src/TKCode.Arc.StreamMark/Avalonia/Rendering/DiffBlockView.cs, src/TKCode.Arc.StreamMark/Avalonia/Extensions/CodeBlockOptions.cs, cd7a2a8, a72b840]
---
2026-10-16 期间并发加入的「差异视图」已提交为 `cd7a2a8`（统一与并排模式）与 `a72b840`（默认 GitHub 风格、表头切换并排）：新增 `Rendering/DiffBlockView.cs` 与 `Extensions/CodeBlockOptions.cs`，代码块围栏的呈现被替换。

**Why:** 生成代码地图时这条特性还在推进，地图的部分页面写在它落地之前。
**How to apply:** 动代码块的呈现、视图复用或块状态时先看 `DiffBlockView` 是否已有对应逻辑；相关地图页面可能落后于代码，以代码为准并顺手更新页面。
