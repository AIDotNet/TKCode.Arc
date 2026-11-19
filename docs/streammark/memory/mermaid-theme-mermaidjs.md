---
name: mermaid-theme-mermaidjs
description: Mermaid 配色默认复刻 mermaid.js 官方主题；宿主半透明主题色必须叠到真实底色上，绝不能丢 alpha；高图只按宽度适配
type: project
updated: 2026-10-18
source: session
evidence: [src/TKCode.Arc.StreamMark.Mermaid/MermaidThemer.cs, src/TKCode.Arc.StreamMark.Mermaid/MermaidPalette.cs, src/TKCode.Arc.StreamMark/Avalonia/Rendering/DiagramBlockView.cs, tests/TKCode.Arc.StreamMark.Tests/Avalonia/Mermaid/MermaidThemeTests.cs, tests/TKCode.Arc.StreamMark.Tests/Avalonia/Rendering/DiagramBlockViewTests.cs, docs/streammark/decisions.md]
---
Mermaid 图表默认复刻 mermaid.js 官方主题（暗色 dark、亮色 default，`MermaidTheme.MermaidJsNeutral` 给灰阶亮色，`MermaidTheme.Native` 是 Mermaider 原生风格）；`DiagramBlockView` 把主题里的半透明文字 / 填充色叠到块背后真实的底色上再传给渲染器；图表高度只按宽度适配，不再封顶 900px（只留 6000px 安全上限）。

**Why:** 2026-10-18 用户在 TCode（AtomUI 暗色）里看到图表是白节点白字、完全看不清。根因是 AtomUI 的 `ColorText`（85% 白）和 `ColorFillQuaternary`（4% 白）是半透明的，旧代码 `Opaque()` 只取 RGB，得到 `Bg = Fg = #F0F0F0`；亮色下是黑节点黑字。用户要求「和 Web 端真实效果一样的主题设计」，对照 mermaid.js 12.2 的实际渲染后，选定复刻官方配色而不是保留 Mermaider 原生风格。高流程图被 900px 高度上限压到约 3px 文字（Web 是约 12px）。
**How to apply:**
- 任何把宿主颜色交给渲染器的地方，先用 `DiagramRenderContext.Flatten` 叠到不透明底色，别 `new Color(R, G, B)` 丢 alpha。
- 改 `MermaidPalette` 的颜色时对照 mermaid.js 渲染出的样式表（`.node rect`、`.cluster rect`、`.edgePaths .path`、`.edgeLabel`），不要凭感觉调。
- `DataPalette` 只能给结构类图表换成中性灰；饼图、甘特图等的色板是分类色。色板里放蓝色会被当作 `Info` 角色色相排除、静默回退到 slate，探测用途时用灰色。
- 子图与连线标签靠 `MermaidThemer.PaintSvg` 按 `subgraph` / `edge-label` 类重涂；Mermaider 升级（现为 0.15.1）后先看这两个类和子图内部结构（rect / path / line）有没有变。
- 已知差异（没做）：Mermaider 的布局引擎对带回边的图会把主链上下翻转（用户那张安全链路图），实体 `&lt;` 在标签里按字面显示；两者都不是配色问题。
