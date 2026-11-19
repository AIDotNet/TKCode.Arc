---
name: remend-tail-only
description: remend 的末尾补全只用于流式尾部；冻结片段与最终整篇解析只启用转义修正
type: project
updated: 2026-10-16
source: init
evidence: [src/TKCode.Arc.StreamMark/Core/Streaming/DocumentComposer.cs, docs/streammark/decisions.md, docs/streammark/streaming-rules.md]
---
冻结片段与 `Complete()` 的整篇解析只启用 `singleTilde` 与 `comparisonOperators` 两个位置无关的转义修正（`DocumentComposer` 里的 settled 选项）；完整的末尾补全只作用于尾部。

**Why:** 已经完整的文本不该被「补全」，否则冻结时与整篇解析的结果不一致，块 key 与内容都会漂移（`docs/streammark/decisions.md`）。
**How to apply:** 新增或调整 remend handler 时必须同时检查这条界线；把补全用到冻结片段上会让流式渲染与最终渲染产生差异。
