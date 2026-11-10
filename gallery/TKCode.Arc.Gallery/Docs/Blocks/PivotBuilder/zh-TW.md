---
title: Pivot builder
summary: 把欄位拖到行、列和值中來搭建透視表，也可以用每個欄位的鍵盤選單；為每個值選擇彙總方式，表格立即重新分組，並帶有行、列和總計。
source-hash: 803309efc7fa
---

<Demo name="Preview" />

## 何時使用

`PivotBuilder` 讓人不寫查詢就能彙總一組記錄。記錄的欄位放在一個清單裡；把一個欄位拖到 Rows，它的每個值成為一行；拖到 Columns，每個值成為一列；拖到 Values，每個單元格顯示一個彙總值（求和、計數、平均、最小或最大）。區域下方的表格立即重新分組，並為每一行、每一列以及全部資料加上合計。

- **臨時報表。** 按地區和季度的收入，按團隊和月份的工單。
- **探索導出的資料。** 在搭建儀表板之前先試幾種分組。
- **教人使用透視表。** 每次改動都立即看到結果。

## 何時不用

- **固定的報表。** 分組從不改變時，直接顯示 `Table` 或圖表。
- **上百萬條記錄。** 本區塊在介面執行緒的記憶體中分組；請先在伺服器上彙總。
- **編輯記錄。** 表格只顯示彙總值；編輯單元格請使用 `DataGrid`。

## 用法

本區塊派生自 `TemplatedControl`。沒有記錄時，它對虛構的銷售資料做透視。提供 `ItemsSource` 和 `Fields`；每個 `PivotBuilderField` 從一條記錄中讀出一個值（`PivotBuilderField.Create<T>` 有類型，可用於 AOT）。

```xml
<arc:PivotBuilder ShowTotals="False" IsFramed="False" />
```

```csharp
pivot.Fields.Add(PivotBuilderField.Create<Ticket>("team", "Team", t => t.Team));
pivot.Fields.Add(PivotBuilderField.Create<Ticket>("hours", "Hours", t => t.Hours, isNumeric: true, format: "N1"));
pivot.RowFields.Add("team");
pivot.ValueFields.Add(new PivotBuilderValue("hours", PivotBuilderAggregation.Average));
pivot.ItemsSource = tickets;
```

### 布局

`RowFields`、`ColumnFields` 和 `ValueFields` 保存布局，並隨拖曳而改變；在程式碼中修改它們可以設定初始布局。`MoveField` 和 `SetAggregation` 做欄位按鈕所做的事，並引發 `LayoutChanged`。放在 Values 中的非數字欄位按計數彙總。`Result` 保存計算好的透視結果，供你自己導出。文字都在 `Labels`（`PivotBuilderLabels`）中。

<Demo name="OwnRecords" />

## API 參考

<!-- api: PivotBuilder -->

<!-- api: PivotBuilderField -->

<!-- api: PivotBuilderValue -->

<!-- api: PivotBuilderAggregation -->

<!-- api: PivotBuilderZone -->

## 無障礙

- 每個欄位都是一個按鈕，以欄位和所在區域命名。**Enter** 或 **Space** 打開它的選單：移到另一個區域、左移或右移、選擇彙總方式或移除。**Delete** 把它從布局中移除。
- 各區域分別名為 Fields、Rows、Columns 和 Values；表格是帶標題的 Arc `Table`。
- 每次移動都會被播報（"Region moved to Columns"）。

## 動效

- 被拖曳的欄位通過變換跟隨指標，並帶上抬起的陰影；它下方的區域用 160 毫秒著色。
- 減少動效時仍可拖曳（這是直接操作），著色不再過渡。

## 響應式

- 在 `CompactWidth`（760 px）或更窄時，欄位清單移到區域上方；內邊距收緊到 16。
- 區域中的欄位會換行；過寬的表格在 Arc `Table` 內滾動。

## 相關元件

- `Table`，本區塊填充的表格。
- `DataGrid`，用於可編輯的表格。
- `Metric explorer` 和 `Revenue explorer`。
