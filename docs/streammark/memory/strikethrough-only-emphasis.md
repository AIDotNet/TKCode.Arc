---
name: strikethrough-only-emphasis
description: 强调扩展只启用删除线，上标/下标/插入/高亮被有意关掉
type: project
updated: 2026-10-16
source: init
evidence: [src/TKCode.Arc.StreamMark/Core/Parsing/MarkdownProfile.cs, docs/streammark/decisions.md]
---
`MarkdownProfileBuilder` 只启用 `EmphasisExtraOptions.Strikethrough`（`src/TKCode.Arc.StreamMark/Core/Parsing/MarkdownProfile.cs`），上标 `^`、下标 `~`、插入 `++`、高亮 `==` 都不启用。

**Why:** 这些标记在大模型输出的普通文本里误触发太多——「C++ 和 C++」、「a == b 且 c == d」、「约 ~5 到 ~10」（`docs/streammark/decisions.md` 的实施中追加的决定）。
**How to apply:** 想启用其中任何一个之前，先确认它不会在常见自然语言文本里误触发；默认方言被所有宿主共用，改它等于改所有宿主的渲染结果。
