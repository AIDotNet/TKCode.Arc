---
name: research-decisions-recorded
description: 每个关键依赖都是实测后的选择，被否决的方案与理由记在 docs/streammark/decisions.md；改选依赖前先读它
type: project
updated: 2026-10-16
source: init
evidence: [docs/streammark/decisions.md, docs/streammark/research/, README.md]
---
解析器选 Markdig 1.4、数学选 RaTeX、图表选 Mermaider、高亮选 TextMateSharp，都是五路调研（`docs/streammark/research/01-streamdown.md` … `05-native-streaming.md`）后的决定；被否决的方案连同理由（CSharpMath 覆盖率、自研增量解析成本、AvaloniaEdit 太重等）记在 [docs/streammark/decisions.md](../../docs/streammark/decisions.md)。

**Why:** 这些选择当时都有实测数据，重新讨论它们会重走一遍调研。
**How to apply:** 要换依赖或引入同类库之前先读 `docs/streammark/decisions.md`；结论被推翻时更新该表，并在这里补一条记录原因的条目。
