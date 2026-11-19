---
name: textruncache-invalidation
description: TextRunCache 必须从「差异点所在段落的起点」失效；按差异点失效会让每次追加变成硬换行
type: project
updated: 2026-10-16
source: init
evidence: [src/TKCode.Arc.StreamMark/Avalonia/Rendering/TextSegmentView.cs, tests/TKCode.Arc.StreamMark.Tests/Avalonia/Transcript/SmoothStreamWrapTests.cs, c2c3d5c, docs/streammark/decisions.md]
---
`TextSegmentView` 的整形缓存按「换行符分隔的段落」为单位、按段落起点保存整段结果，`InvalidateFrom(i)` 只丢弃起点不早于 `i` 的条目，因此传进去的必须是差异点所在段落的起点。

**Why:** alpha.8 与 alpha.9 直接按差异点失效，旧段落连同它的段末换行被复用，于是每次追加都变成一次硬换行；alpha.10 修正后流式段落的排版大约快 2 倍。
**How to apply:** 改动追加路径或缓存失效范围时，跑 `SmoothStreamWrapTests`；不要把 `InvalidateFrom` 的语义改成「按位置失效」。
