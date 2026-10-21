---
title: 迷你趨勢圖
summary: 在數值旁顯示精簡的趨勢。
source-hash: cd53535b6fd3
---

<Demo name="KpiCards" />

## 何時使用

- 卡片、表格列或 KPI 區塊中，位於主要數值旁的小型趨勢。
- 比起精確資料點，更關注方向與形狀的趨勢。
- 讀者可以拖曳查看較早資料點、但不需要完整座標軸的數值。

## 何時不用

- 需要座標軸進行比較的離散期間：用 `BarChart`。
- 只有最新數值及其變化、沒有趨勢的情況：用 `MetricCard`。
- 需要座標軸、多個系列或圖例的趨勢：用 `LineChart`。

## 用法

`ItemsSource` 依由舊到新的順序保存數值。`Value` 與 `Change` 構成靜止時顯示的主數值與附註。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:Sparkline Label="營收"
                   Value="$48.2K"
                   Change="+4.1%"
                   Tone="Success"
                   Width="220"
                   ItemsSource="{Binding Revenue}" />
</UserControl>
```

### 色調

`Tone` 決定線條與變化量的顏色。使用 `Success` 或 `Danger` 表示指標的走向，使用 `Warning` 表示需要留意的數值。

```xml
<arc:Sparkline Label="流失率" Value="2.4%" Change="+0.3 pts" Tone="Danger" ItemsSource="{Binding Churn}" />
<arc:Sparkline Label="佇列深度" Value="1,204" Change="上升" Tone="Warning" ItemsSource="{Binding Queue}" />
```

### 資料點標籤

`PointLabels` 為每個資料點提供一個標籤。讀者拖曳線條時，會讀出並顯示該資料點的標籤，而不是位置。

```csharp
var labels = days.Select(d => d.ToString("MMM d", CultureInfo.GetCultureInfo("en-US"))).ToArray();
revenue.PointLabels = labels;
```

### 沒有主數值

未設定 `Value` 時，拖曳所得的數值及其資料點會顯示在標籤旁。新資料會使線條變形過渡。

<Demo name="WithoutHeadline" />

```xml
<arc:Sparkline Label="造訪量" Width="300" ItemsSource="{Binding Visits}" />
```

### 高密度表格

在列數很多的表格中設定 `IsInteractive="False"`。此時迷你趨勢圖沒有 Tab 停留點，也不處理指標事件。設定 `ShowArea="False"` 可讓線條保持簡潔。

<Demo name="DenseTable" />

```xml
<arc:Sparkline Label="api-gateway"
               Width="160"
               DrawingHeight="32"
               ShowArea="False"
               IsInteractive="False"
               ItemsSource="{Binding Gateway}" />
```

## API 參考

<!-- api: Sparkline -->

## 無障礙

- `Label` 為圖表命名。繪圖本身對輔助技術隱藏。
- 圖表可互動時，繪圖區是可聚焦的滑桿。其數值文字會讀出資料點標籤與數值。
- 左、右方向鍵以及上、下方向鍵每次移動一個資料點。PageUp 與 PageDown 大約移動範圍的六分之一，Home 與 End 分別移到第一個與最後一個資料點，Escape 停止拖曳並回到最新數值。
- 設定 `PointLabels`，使拖曳到的資料點以日期讀出，而不是以它在清單中的位置讀出。
- 主數值與變化量是純文字，因此按原樣讀出。

## 動態效果

- 首次顯示時，線條繪製一次，終點圓點隨線條落定而出現。
- 拖曳時，游標以靈敏的彈簧在資料點之間移動，游標之後的資料點變暗。
- 主數值與附註滾動為新的文字，主數值的寬度緩和調整以容納新文字。
- 減少動態效果時整條線立即顯示，游標移動時不使用彈簧。關閉動態效果時直接到達最終狀態。

## 相關元件

- `BarChart`：需要座標軸的離散期間。
- `LineChart`：帶有座標軸、多個系列或圖例的趨勢。
- `MetricCard`：單一數值及其變化，沒有趨勢。
- `BrushChart`：需要縮放的高密度系列。
