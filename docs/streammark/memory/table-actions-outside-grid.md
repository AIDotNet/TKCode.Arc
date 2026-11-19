---
name: table-actions-outside-grid
description: 表格的复制/全屏按钮在网格上方的操作条里，不进单元格
type: project
updated: 2026-10-19
source: session
evidence: "src/TKCode.Arc.StreamMark/Avalonia/Rendering/TableBlockView.cs:TableBlockView.ActionBarHeight; https://github.com/AtomUICommunity/TKCode/issues/7"
---

表格悬停按钮（复制、全屏）排在边框上方一条 26px 的操作条里，视觉子级在单元格之后。

**Why:** 按钮原先排在表头右上角（y = 3）。单元格在 `OnUpdate` 里后加入，画在按钮上面，表头文字会把按钮盖住（TKCode issue 7 的截图）。放到控件边界外（负坐标或 Popup）会被 `ClipToBounds` 裁掉，或者命中测试到不了，点不中。操作条算进测量高度，悬停时布局不跳。

**How to apply:** 改这些按钮的位置时，保持它们的底边不超过第一个单元格的顶边，不要为了省掉那 26px 把按钮塞回表头。`TranscriptPanel` 的表格行高估算要加上 `TableBlockView.ActionBarHeight`。
