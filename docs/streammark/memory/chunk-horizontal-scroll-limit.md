---
name: chunk-horizontal-scroll-limit
description: 长代码块各分段的横向滚动彼此独立，这是已知限制而不是待修的 bug
type: project
updated: 2026-10-16
source: init
evidence: [src/TKCode.Arc.StreamMark/Avalonia/Rendering/CodeBlockView.cs, docs/streammark/decisions.md]
---
超过 200 行的代码块在会话里按 128 行一段拆成多行；每段的横向滚动偏移各自独立。

**Why:** 分段与视图回收后被重建的视图共用块共享状态（偏移全局化，选区跨段连续），但横向滚动没有跨段同步，当时接受为已知限制。
**How to apply:** 遇到「长代码块横向滚动不同步」时不要当成回归；要修的话需要先决定跨段共享的滚动状态放在哪里。
