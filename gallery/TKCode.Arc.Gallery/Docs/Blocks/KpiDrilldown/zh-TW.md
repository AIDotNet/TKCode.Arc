---
title: KPI drilldown
summary: 附迷你趨勢線和週期變化的 KPI 卡片，可在原位展開為詳細檢視，包含趨勢圖、上一週期對比、可用鍵盤操作的讀數卡，以及依維度劃分的明細。
source-hash: 12e55a3e2f11
---

<Demo name="Preview" />

## 何時使用

`KpiDrilldown` 是一個保留脈絡的概覽。卡片格狀排列顯示每個指標的最新值、相對上一週期的變化以及該週期的走勢；選擇一張卡片，它會在原位展開為完整的內容，按 Escape 則收回。

- **財務或成長概覽。** 營收、帳號數、留存、流失和轉換放在同一頁，每一項只需一次點擊就能看到它的圖表和拆分。
- **週期回顧。** 一個範圍開關（7、30 或 90 天）讓每張卡片和開啟的詳細檢視一起變化，Compare 則把上一週期放在目前週期之後作為對照。
- **變化從何而來？** 明細表依方案、地區或管道拆分目前開啟的指標，並給出每個成員的占比、值和變化。

## 何時不用

- **指標很多。** 六張卡片一目了然；超過這個數量應使用表格或指標選擇器。
- **自由探索。** 詳細檢視只有一張圖表、一個範圍和一種拆分。需要縮放、刷選或選擇數列時，請使用 metric explorer。
- **即時串流數值。** 此區塊圍繞按日的數列設計，只有範圍、Compare 或維度改變時資料才會變化。

## 用法

此區塊衍生自 Avalonia 的 `TemplatedControl`，不含資料：請給它 `Metrics`，每個是一個 `KpiMetric`，其 `Values` 是以 `EndDate` 結尾的每日數值（至少是最長範圍的兩倍，這樣上一週期才存在）；並給它用於明細的 `Dimensions`，以及每個指標上與之對應的 `KpiBreakdown`。

```xml
<arc:KpiDrilldown Title="Overview" RangeDays="30">
    <arc:KpiDrilldown.Dimensions>
        <arc:KpiDimension Key="tier" Label="Tier" />
    </arc:KpiDrilldown.Dimensions>
    <arc:KpiMetric Key="arr" Label="Annual run rate" Format="CurrencyCompact" Description="Contracted yearly revenue." />
</arc:KpiDrilldown>
```

```csharp
var metric = new KpiMetric("refunds", "Refund rate", dailyValues)
{
    Kind = KpiKind.Rate,        // the change is a difference in points ("−0.2 pts")
    IsLowerBetter = true,       // a fall is green; the arrow still points down
    Format = KpiValueFormat.Percent,
};
metric.Breakdowns.Add(new KpiBreakdown("tier", [new KpiBreakdownRow("Team", teamValues)]));
```

示範中的所有數字都是用固定種子虛構的。

### 卡片顯示什麼

數值是數列的最後一天，不隨範圍變化。變化是將目前週期的最後一天與上一週期的最後一天相比：`Total` 指標為相對百分比，`Rate` 指標為百分點之差。它顯示一個箭頭、一個符號（真正的減號）和一種顏色；當四捨五入後為零時，顯示「unchanged」和一個水平箭頭。迷你趨勢線是將目前週期重新取樣為 40 個點，以直線繪製，並依自身範圍正規化。

### 詳細檢視

詳細檢視顯示名稱、數值、變化和「vs … in the prior N days」、指標的 `Description`、一張 220px 高的趨勢圖（72 個點；三條格線分別標出最高、中間和最低值，以及三個日期）、一個圖例和明細。指向圖表時，十字線會吸附到最近的點，並顯示一個讀數卡，包含日期、數值，在開啟 Compare 時還包含上一週期同一天的值。

<Demo name="Controlled" />

### 繫結狀態與事件

`SelectedKey`、`RangeDays`、`IsCompareEnabled` 和 `SelectedDimensionKey` 是雙向的。`Open(key)` 和 `Close()` 完成選擇卡片和按 Escape 所做的事。`SelectedChanged`、`RangeChanged`、`CompareChanged` 和 `DimensionChanged` 回報使用者做了什麼，而 `SelectedCommand` 在詳細檢視開啟時（參數：該指標）或關閉時（參數：null）執行。

<Demo name="FourMetrics" />

### 更少的指標，其他範圍

`Ranges` 設定範圍開關的天數；沒有 `Dimensions` 時會省略明細，`IsCompareEnabled` 則讓區塊啟動時隱藏上一週期。`MetricsSource` 會跟隨您自己持有的清單。

<Demo name="Narrow" />

### 窄容器

區塊讀取的是自身寬度：從 760 起為三欄，從 520 起為兩欄，小於 520 則為一欄、22px 的數值、僅有圖示的 Compare 按鈕，以及不含占比欄的明細。

### 文字

除資料之外的每一個詞都來自 `Labels`（`KpiDrilldownLabels`）：Compare 文字、範圍名稱、圖例、「vs」行、讀數卡中的上一週期行、卡片的朗讀名稱和圖表的摘要。

## API 參考

<!-- api: KpiDrilldown -->

<!-- api: KpiMetric -->

<!-- api: KpiBreakdown -->

<!-- api: KpiBreakdownRow -->

<!-- api: KpiDimension -->

<!-- api: KpiDrilldownLabels -->

<!-- api: KpiKind -->

<!-- api: KpiValueFormat -->

<!-- api: KpiSelectionChangedEventArgs -->

<!-- api: KpiRangeChangedEventArgs -->

<!-- api: KpiCompareChangedEventArgs -->

## 無障礙

### 鍵盤

- **Tab** 依序到達 Compare、範圍開關（一個停靠點；方向鍵移動選取，Home 和 End 跳轉）和每張卡片。
- 卡片上的 **Enter** 或 **Space** 開啟它，並將焦點移到關閉按鈕。
- 在詳細檢視中，Tab 依序到達關閉按鈕、圖表和維度開關；後面的卡片會被跳過。
- 在圖表上，**←** 和 **→** 每次將十字線移動一天（第一次按鍵顯示最後一天），**Home** 和 **End** 跳到第一天和最後一天，圖表失去焦點時十字線消失。
- **Escape** 從頁面上任何位置關閉詳細檢視，並把焦點返回到該卡片。

### 螢幕閱讀器

- 每張卡片是一個按鈕，其名稱由標籤、數值和用文字表達的變化組成（「Refund rate, 2.6%, down 0.2 pts against the prior 30 days. Show details」）。詳細檢視開啟時，卡片會對輔助技術隱藏。
- 圖表是一幅影像，名稱為其起訖的摘要；用鍵盤移動十字線時，會以禮貌方式播報日期和數值。
- 明細是一個有標籤的區段，每個成員一個有名稱的列；變化除顏色外還使用箭頭或符號。
- Compare 縮小為圖示時仍保留其名稱。焦點環為 2px，位於元素外側，僅在鍵盤操作時顯示。

## 動態效果

- **開啟**：卡片隱藏，面板在同一條彈簧上從自身的框體長到卡片區域（內縮 8），真實的寬度、高度和位置都在變化（視覺時長約 0.5 秒，無回彈）；內容保持最終版面並被裁切。遮罩在 240 ms 內淡入，主體在 120 ms 延遲後用 480 ms 淡入，表面在 480 ms 內變暖。區域的高度隨之變化。
- **關閉**：主體在 120 ms 內淡出，陰影在 480 ms 內淡出，面板在同一條彈簧上折回卡片，然後卡片重新出現；焦點返回到卡片。
- **範圍、Compare、維度**：每條迷你趨勢線和圖表線在平滑彈簧上逐點變形，座標軸標籤顯示內插後的數字，每個數字逐位滾動，上一週期的線在 240 ms 內淡入，文字交叉淡入（先用 80 ms 淡出，再用 240 ms 淡入），占比條則以彈簧運動。
- 減少動態效果時，面板以最終尺寸顯示並帶 160 ms 的淡入；變形、占比條和數字直接跳轉。關閉則立即套用所有變化。

## 相關

- `SegmentedControl`，用於其他區塊中的開關。
- `Metric explorer` 和 `Revenue explorer`，其他更深入某個數字的 Analytics 區塊。
- `Metrics dashboard`，用於沒有下鑽的一列 KPI。
