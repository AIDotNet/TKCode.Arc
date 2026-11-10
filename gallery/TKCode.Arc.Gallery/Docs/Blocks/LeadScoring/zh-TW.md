---
title: Lead scoring
summary: 按分數排序的潛在客戶，帶階段標籤、搜尋和分數段；選擇一位客戶即可看到分數背後的匹配、互動和意向因素，並在階段之間移動一位或多位客戶。
source-hash: 03a21f4c8341
---

<Demo name="Preview" />

## 何時使用

`LeadScoring` 是銷售團隊日常使用的潛在客戶清單。每位客戶都有一個 0 到 100 的分數，由多個因素組成（匹配、互動或意向信號，各值若干分，有時為負），所以分數從來不是黑盒：選擇一行，表格旁的面板就會列出加分和減分的原因。階段標籤、搜尋和分數段（熱、溫、冷）用來縮小表格；面板中的階段切換移動一位客戶，表格上方的操作條移動所有選中的行。

- **每天的篩選。** 按分數排序，打開熱門客戶，把他們移到 Working。
- **解釋一個分數。** 向經理或銷售說明一位客戶為什麼排在這裡。
- **整理一個階段。** 選中 New 中的冷門客戶，一次移到 Nurture。

## 何時不用

- **完整的客戶記錄。** 面板只解釋分數；聯系記錄和備註請使用個人資料頁。
- **在列之間拖放的看板。** 階段是主要檢視時，請使用 `Kanban board`。
- **無法解釋的分數。** 沒有因素時面板無內容可顯示；`ScoreOverride` 只作為例外使用。

## 用法

本區塊派生自 `TemplatedControl`。沒有客戶時，它顯示一條虛構的銷售管道。提供 `Stages` 和 `Leads`；客戶的 `Score` 是其 `Factors` 的總和（限制在 0–100），除非設定了 `ScoreOverride`。

```xml
<arc:LeadScoring Title="Event sign-ups" HotThreshold="70" WarmThreshold="40" />
```

```csharp
board.Stages.Add(new LeadScoringStage("met", "Met at booth"));
board.Leads.Add(new LeadScoringLead("a", "Paula Ito", "Fernway Labs", "met")
{
    Factors = [new("Decision maker", 25, LeadScoringFactorKind.Fit), new("Asked for pricing", 30, LeadScoringFactorKind.Intent)],
});
board.LeadsMoved += (_, e) => crm.SaveStage(e.Leads, e.Stage.Key);
```

### 篩選與移動

`StageFilter`、`Band` 和 `SearchText` 都是雙向的，所以可以由頁面控制篩選。`ActiveLead` 是面板中的客戶；選擇或聚焦一行會設定它。`MoveLeads` 在程式碼中移動客戶；每次移動都會寫入 `StageKey`，引發帶有原階段的 `LeadsMoved`，並進行播報。文字都在 `Labels`（`LeadScoringLabels`）中。

<Demo name="OwnLeads" />

## API 參考

<!-- api: LeadScoring -->

<!-- api: LeadScoringLead -->

<!-- api: LeadScoringFactor -->

<!-- api: LeadScoringStage -->

## 無障礙

- 表格就是 Arc 的 `Table`：標題為它命名，行可以用鍵盤選擇，表頭可以排序。
- 階段標籤和分數段是名為 "Stage" 和 "Score" 的分段控制項。
- 面板以客戶命名；每個因素讀出類別、說明和分數，圓環讀出總分。
- 移動會被播報（"Moved Paula Ito to Qualified"）。

## 動效

- 選擇另一位客戶時，分數圓環追向新的值（僅完整動效）。
- 操作條用 160 毫秒淡入淡出；減少動效時立即出現。
- 表格自身的排序和選擇動效照常。

## 響應式

- 在 `CompactWidth`（860 px）或更窄時，面板移到表格下方，內邊距從 24 收緊到 16。
- 放不下時，篩選控制項換到第二行。
- 低於 620 px 時，表格像 Arc 的 `Table` 一樣折疊行。

## 相關元件

- `Table`、`SegmentedControl` 和 `Select`，組成本區塊的控制項。
- `Kanban board`，用於在階段列之間拖曳。
- `Call insights` 和 `Account health`。
