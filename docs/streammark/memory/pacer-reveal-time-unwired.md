---
name: pacer-reveal-time-unwired
description: StreamPacer.GetRevealTime 全仓库只有测试在调用，渐显时间实际由 MarkdownStream 的时钟记录
type: project
updated: 2026-10-16
source: init
evidence: [src/TKCode.Arc.StreamMark/Core/Pacing/StreamPacer.cs, src/TKCode.Arc.StreamMark/Avalonia/MarkdownStream.cs, tests/TKCode.Arc.StreamMark.Tests/Core/Pacing/StreamPacerTests.cs]
---
`StreamPacer` 暴露的 `GetRevealTime` / `RevealHistory` 在生产路径上没有消费者（仓库内只有测试与它自身声明），渐显的时间记录由 Avalonia 侧的 `MarkdownStream` 用自己的时钟完成。

**Why:** 排查「渐显/光标时间不对」时容易先怀疑 pacer，但那条线其实没接上。
**How to apply:** 改渐显计时前先确认改动落在真正生效的那条路径上；若要启用 `GetRevealTime`，需先把它接到渲染侧的时钟并补测试。
