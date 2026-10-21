---
title: 刷選圖表
summary: 帶概覽條的密集時間序列：拖曳視窗即可縮放，透過控點調整大小，並在原處查看事件。
source-hash: a7849aba3612
---

<Demo name="OverviewAndDetail" />

## 何時使用

- 涵蓋一年或更長時間的每日序列，讀者需要放大查看某個時段。
- 事件檢討與其他帶標註事件的時間軸，需要在原處讀取事件。
- 數千個資料點的資料集，概覽條可讓完整範圍始終可見。

## 何時不用

- 幾週的資料或需要比較多個資料系列：使用 `LineChart`。
- 無需互動的小型趨勢：使用 `Sparkline`。
- 包含負值或需要對數刻度：數值軸始終從零開始，因此不適用。

## 用法

`ItemsSource` 保存依日期遞增排列的資料點。日期取自 `ChartPoint` 的 X 值，數值軸則為資料點的值。繪圖區下方的概覽條顯示完整範圍，視窗決定繪圖區顯示的內容。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:BrushChart Label="每日活躍使用者"
                    Unit="使用者"
                    PlotHeight="240"
                    OverviewHeight="52"
                    ItemsSource="{Binding Points}" />
</UserControl>
```

### 標註

`Annotations` 在繪圖區與概覽條上標記事件。每個標註包含日期、標籤以及可選的描述，描述會顯示在提示中。

```csharp
chart.Annotations =
[
    new ChartAnnotation(start.AddDays(120), "v2 上線", "新的導覽流程已開放給所有使用者。"),
    new ChartAnnotation(start.AddDays(410), "媒體提及", "被每週電子報刊登。"),
    new ChartAnnotation(start.AddDays(530), "定價調整")
];
```

### 預設與視窗

`Range` 為可見視窗，並且是雙向繫結的。為其指定新值即可移動視窗，視窗會以彈簧動畫滑動到該位置。使用 `RangeChanged` 可以以文字顯示視窗範圍。

```csharp
chart.Range = new ChartTimeRange(last.AddDays(-30), last);
chart.RangeChanged += (_, e) => range.Text = $"視窗：{e.Range.Start:MMM d, yyyy} – {e.Range.End:MMM d, yyyy}";
```

### 最小跨度

`MinSpan` 限制視窗不會縮小到該跨度以下。預設值為七天。

```xml
<arc:BrushChart Label="要求數" MinSpan="14.00:00:00" ItemsSource="{Binding Points}" />
```

### 格式化

`DateFormatter` 格式化提示與視窗文字中的日期，`TickFormatter` 格式化數值軸。

```xml
<arc:BrushChart Label="營收"
                Unit="USD"
                TickFormatter="{Binding FormatMoney}"
                ItemsSource="{Binding Points}" />
```

## API 參考

<!-- api: BrushChart -->

## 無障礙

- `Label` 為圖表的名稱。視窗及其兩個控點為滑桿，其數值文字即為日期。
- 繪圖區是可取得焦點的群組。其讀數會播報該日的日期、數值以及當天的任何事件。
- 在繪圖區中，左右方向鍵將十字線移動一個資料點。在視窗中，左右方向鍵按視窗寬度的十分之一平移，Shift 或 PageUp 與 PageDown 平移二分之一，加號與減號鍵則以中心為基準縮放。
- 在控點上，左右方向鍵移動一個資料點，按住 Shift 則移動十個。Home 與 End 分別跳到第一個與最後一個資料點，按 Escape 或 0 恢復為完整範圍。
- 一張隱藏的資料表列出視窗內的每個資料點及其事件。事件以第二種資料系列顏色標記，並附帶文字標籤或提示，因此不會僅靠顏色來區分。

## 動態效果

- 首次顯示時，折線從左側一次性繪製出來，使用較長且無回彈的彈簧。
- 拖曳時視窗與指標一對一對應。預設、重設以及 `Range` 的變更則使視窗以彈簧動畫滑動。
- 數值軸以彈簧動畫調整到可見範圍內的最高點。隨著密度變化，原始折線與平滑趨勢線交叉淡化。
- 十字線在資料點之間平滑滑動。
- 減少動態效果時視窗立即到位，十字線保持不動。關閉動態效果時直接到達最終狀態。

## 相關元件

- `LineChart`：幾週的資料或多個資料系列。
- `Sparkline`：無互動的小型趨勢。
- `Streamgraph`：多個圖層隨時間的組成。
- `AnimatedCounter`：變動中的單一總數。
