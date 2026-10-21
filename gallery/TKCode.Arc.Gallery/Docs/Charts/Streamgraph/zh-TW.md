---
title: 串流圖
summary: 在擺動基線上堆疊的串流層，可在不同範圍之間變形；可單獨隔離某一層並逐週讀取。
source-hash: d237f20ddc0d
---

<Demo name="TopicsOverTime" />

## 何時使用

- 總量隨時間的組成，例如每週各主題的支援工單。
- 以流動的層來呈現，比以獨立折線更容易看清的季節性或高峰。
- 三到八層。把較小的層合併為「其他」系列後再傳入。

## 何時不用

- 需要精確數值或比較各系列：用 `LineChart`。
- 少量期間或離散類別：用 `BarChart`。
- 包含負值的資料：各層從基線堆疊，串流形狀無法正確讀取。請用 `LineChart`。

## 用法

每個 `ChartSeries` 為一層。`ItemsSource` 保存共用的 x 位置，例如每週的起始日期。懸停或聚焦某一層即可將其單獨隔離。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:Streamgraph Label="各主題的支援工單"
                     Unit="工單"
                     CategoryLabel="週"
                     ItemsSource="{Binding Weeks}">
        <arc:Streamgraph.Series>
            <arc:ChartSeries Key="billing" Name="帳務" Values="{Binding Billing}" />
            <arc:ChartSeries Key="login" Name="登入" Values="{Binding Login}" />
            <arc:ChartSeries Key="performance" Name="效能" Values="{Binding Performance}" />
        </arc:Streamgraph.Series>
    </arc:Streamgraph>
</UserControl>
```

### 範圍切換

同時替換 `ItemsSource` 與各系列的數值。每一層都會從畫面上的目前形狀融合到新形狀，長度不同的範圍同樣會變形過渡。

```csharp
void Range(int weeks)
{
    var start = ChartsADemoData.Today.AddDays(-7 * (weeks - 1));
    chart.ItemsSource = Enumerable.Range(0, weeks).Select(w => start.AddDays(7 * w)).ToArray();
    for (var i = 0; i < series.Length; i++)
    {
        series[i].Values = ChartsADemoData.Daily(weeks, 60 - i * 9, 40, weeks * 7 + i, i % 2 == 0 ? 0.8 : -0.3);
    }
}
```

### 偏移

`Offset` 決定各層的堆疊方式：`Wiggle`（預設）使各層的斜率最小化，形成流動的串流；`Silhouette` 將堆疊圍繞一條平直的中線置中；`Zero` 則從平直的基線依序堆疊各層。

```xml
<arc:Streamgraph Label="主題" Offset="Silhouette" ItemsSource="{Binding Weeks}" />
```

### 圖例、直接標籤與隱藏層

圖例與直接標籤用於命名各層。空間足夠時，直接標籤會繪製在某一層最粗的位置。`HiddenSeries` 為雙向繫結，因此檢視模型可以保存被隱藏的鍵。

```xml
<arc:Streamgraph Label="主題"
                 ShowLegend="True"
                 ShowDirectLabels="True"
                 HiddenSeries="{Binding HiddenKeys}"
                 ItemsSource="{Binding Weeks}" />
```

### 讀取某一週

`ActiveIndexChanged` 回報指標或鍵盤所在的週，`ActiveSeriesKey` 指明被隔離的層。

```csharp
chart.ActiveIndexChanged += (_, e) => readout.Text = e.Label ?? string.Empty;
```

## API 參考

<!-- api: Streamgraph -->

## 無障礙

- `Label` 為圖表命名。繪圖區是可聚焦的群組：取得焦點時讀取最新一週，每條讀數都會播報該週、被隔離層的數值與總量。
- 左、右方向鍵沿時間軸移動，上、下方向鍵切換被隔離的層。PageUp 與 PageDown 大約移動範圍的六分之一，Home 與 End 分別移到第一週與最後一週，Escape 清除讀數。
- 圖例項目為切換開關。懸停或聚焦某個項目即可隔離其所屬的層。
- 隱藏的資料表列出每一週的每一層。
- 各層直接在繪圖區上標示名稱，因此不會只靠明暗來區分。相鄰的層之間以 2 px 的表面顏色間隙分隔。

## 動態效果

- 首次顯示時，串流從中線處的一根細線沿中心向外膨脹，使用不過衝的長彈簧。
- 新範圍或新資料集會在共用網格上融合每一層，因此各層保持其順序與速度。
- 被隱藏的層會收縮至消失，其餘層圍繞它重新堆疊。
- 十字準線與提示在彈簧上滑動，隔離某一層時其餘層變暗。
- 減少動態效果時立即顯示最終狀態，並保留十字準線與提示但不移動它們。關閉動態效果時直接到達最終狀態。

## 相關元件

- `LineChart`：需要精確比較各系列數值時。
- `BarChart`：少量離散期間。
- `Ridgeline`：比較多個組的分布，而非其隨時間的總量。
- `DonutChart`：一個總量中各部分所佔的比例。
