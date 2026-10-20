---
title: 折線圖
summary: 多系列折線圖，帶有平滑滑動的十字準線、圖例開關，以及在不同範圍之間變形的路徑。
source-hash: 38391cf58aac
---

## 何時使用

- 隨時間變化的一到三個指標，例如每天的註冊數或營收。
- 以虛線系列將本期與上一期或目標進行比較。
- 7 天、30 天、90 天等範圍切換時，應讓同一組線條變形過渡，而不是重新繪製。

## 何時不用

- 與單一數值並排的精簡趨勢：用 `Sparkline`。
- 星期或地區等離散類別：用 `BarChart`。
- 部分與整體的關係：用 `DonutChart` 或 `WaffleChart`。

## 用法

每個 `ChartSeries` 都有穩定的 `Key`，因此資料變化時線條仍保有其身分。設定 `Label` 可為輔助技術命名圖表。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:LineChart Label="註冊數" CategoryLabel="日期" ItemsSource="{Binding Days}">
        <arc:LineChart.Series>
            <arc:ChartSeries Key="current" Name="本期" Values="{Binding Current}" />
            <arc:ChartSeries Key="previous" Name="上一期" Values="{Binding Previous}" IsDashed="True" />
        </arc:LineChart.Series>
    </arc:LineChart>
</UserControl>
```

### 範圍切換

範圍變更時，替換 `ItemsSource` 與各系列的數值。每條線都會從畫面上的目前形狀變形到新形狀，數值軸也會以彈簧效果過渡到新範圍。

```csharp
void Range(int days)
{
    chart.ItemsSource = ChartsADemoData.Days(days);
    current.Values = ChartsADemoData.Daily(days, 420, 160, days, 2.4);
    previous.Values = ChartsADemoData.Daily(days, 380, 140, days + 11, 1.1);
}
```

### 單一系列與單位

`ChartPoint` 項目的清單不需要額外的系列。`Unit` 會顯示在提示中，`Curve` 可將預設的平滑曲線切換為直線段。

```xml
<arc:LineChart Label="p95 延遲" Unit="ms" Curve="Linear" PlotHeight="180" ItemsSource="{Binding Latency}" />
```

### 圖例與隱藏系列

存在多個系列時，圖例會為每個系列顯示一個開關。`ShowLegend` 可強制顯示或隱藏圖例。繫結 `HiddenSeries`，即可在檢視模型中保存被隱藏的鍵。

```xml
<arc:LineChart Label="註冊數"
               ItemsSource="{Binding Days}"
               ShowLegend="True"
               HiddenSeries="{Binding HiddenKeys}" />
```

### 載入與空白狀態

`IsLoading` 會讓線條變暗，並在沒有資料點時顯示骨架畫面。`EmptyLabel` 用於說明空白的範圍。

```xml
<arc:LineChart Label="退款" EmptyLabel="此範圍內沒有退款" IsLoading="{Binding IsBusy}" />
```

### 讀取數值

十字準線移動時以及放開時，都會觸發 `ActiveIndexChanged`。可用它在標題中顯示數值。

```csharp
chart.ActiveIndexChanged += (_, e) => readout.Text = e.Index is null
    ? "將滑鼠懸停或使用方向鍵讀取數值。"
    : $"{e.Label}: {string.Join(", ", e.Values.Select(v => $"{v.Name} {v.Value:N0}"))}";
```

## API 參考

<!-- api: LineChart -->

## 無障礙

- `Label` 為圖表命名。繪圖區會在隱藏的摘要中公開範圍與最新數值，隱藏的資料表則列出每個資料點。
- 繪圖區是可聚焦的滑桿。方向鍵每次移動一個資料點，PageUp 與 PageDown 大約移動範圍的六分之一，Home 與 End 分別移到第一個與最後一個資料點，Escape 釋放十字準線。
- 圖例項目是切換按鈕。每個系列既以名稱識別，也以線型識別，而不只依靠顏色。
- 載入狀態會以「正在載入」禮貌地播報。線條與提示對輔助技術隱藏，以避免重複朗讀。

## 動態效果

- 首次顯示時，線條從左到右繪出，面積填色隨線條一同顯現。
- 新範圍會在共用的 x 位置上重新取樣線條，並從目前形狀變形過渡。格線隨數值軸滑動。
- 十字準線在資料點之間滑動，提示由其自身的彈簧跟隨，並在邊緣處翻轉。
- 載入中的線條會脈動，直到資料到達。圖表離開可視範圍時脈動暫停。
- 減少動態效果時，略過繪製、變形與滑動過程，並停止載入脈動。關閉動態效果時直接到達最終狀態。

## 相關元件

- `Sparkline`：與數值並排的精簡趨勢。
- `BarChart`：在離散的日、週或月之間比較單一指標。
- `BrushChart`：需要縮放的高密度時間序列。
- `Streamgraph`：需要將多個系列當作堆疊層來閱讀時。
