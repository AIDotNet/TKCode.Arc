---
title: 長條圖
summary: 跨日、週或月比較同一指標，並可逐一查看任一長條的數值。
source-hash: 7232de4d9e70
---

## 何時使用

- 同一指標在日、週或月之間的比較，例如每天的活躍分鐘數。
- 需要以虛線顯示平均值作為參考的數值範圍。
- 離散的類別，例如每週的部署次數，每個長條各自獨立。

## 何時不用

- 包含多個資料點或多個資料系列的連續趨勢：使用 `LineChart`。
- 與數值並列的精簡趨勢：使用 `Sparkline`。
- 以格線呈現一整年每日活動：使用 `ActivityHeatmap`。

## 用法

標題區在靜止時顯示平均值；指標或鍵盤停在某個長條上時，顯示該長條的數值。`Period` 設定標題下方顯示的時間範圍。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:BarChart Label="活躍分鐘數"
                  Period="2026 年 9 月 24–30 日"
                  Unit="分鐘"
                  CategoryLabel="日"
                  ItemsSource="{Binding Week}" />
</UserControl>
```

### 時間範圍

時間範圍變更時，替換 `ItemsSource` 與 `Period`。鍵相同的長條會變形到新的高度與位置，新增的長條則在旁邊生長出來。

```csharp
void Week()
{
    var days = ChartsADemoData.Days(7);
    var values = ChartsADemoData.Daily(7, 46, 30, 3);
    chart.Period = "2026 年 9 月 24–30 日";
    chart.ItemsSource = days.Select((day, i) => new ChartPoint(day, values[i])
    {
        Label = day.ToString("dddd, MMM d", System.Globalization.CultureInfo.GetCultureInfo("en-US")),
        AxisLabel = day.ToString("ddd", System.Globalization.CultureInfo.GetCultureInfo("en-US"))
    }).ToArray();
}
```

`ChartPoint.Label` 是提供給輔助技術朗讀的完整名稱。`ChartPoint.AxisLabel` 是長條下方的簡短文字。

### 標題標籤

`AverageLabel` 為靜止時標題區的名稱，`ValueLabel` 為逐一查看時長條數值的名稱。若參考線沒有用處，可使用 `ShowAverage` 隱藏平均線。

```xml
<arc:BarChart Label="部署次數"
              Period="最近 12 週"
              AverageLabel="每週平均"
              ValueLabel="部署次數"
              CategoryLabel="週"
              ShowAverage="False"
              PlotHeight="140"
              ItemsSource="{Binding Weeks}" />
```

### 座標軸

`ShowGrid`、`ShowXAxis` 與 `ShowYAxis` 控制參考線。`YMaximum` 固定數值軸的上限，`TickFormatter` 格式化數值軸的刻度。

```xml
<arc:BarChart Label="工單" YMaximum="100" ShowGrid="False" ItemsSource="{Binding Tickets}" />
```

### 讀取數值

`ActiveIndexChanged` 會回報指標或鍵盤所在的長條。`Average` 給出所有數值的平均值。

```csharp
chart.ActiveIndexChanged += (_, e) => status.Text = e.Index is null
    ? $"平均 {chart.Average:N0}"
    : e.Label;
```

## API 參考

<!-- api: BarChart -->

## 無障礙

- `Label` 為圖表的名稱。摘要會說明平均值、最高與最低的長條。
- 方向鍵在長條之間移動，PageUp 與 PageDown 每次移動至多七個長條，Home 與 End 分別跳到第一個與最後一個長條，按 Escape 停止逐一查看並返回平均值。
- 一張隱藏的資料表列出每個數值，因此即使看不到長條也能讀取圖表。
- 時間範圍變更時會以禮貌方式播報。
- 長條對輔助技術隱藏。逐一查看的位置由游標指示，而不是焦點框。

## 動態效果

- 首次顯示時，長條從最新的一個開始向後依序生長。
- 時間範圍變更時，保留的長條、座標軸刻度與平均線會變形到新的數值。
- 查看游標在長條之間平滑滑動，標題數值滾動到新的數字。
- 標題標籤在平均值與數值標籤之間切換，並伴隨短暫的模糊。
- 減少動態效果時長條直接顯示為最終高度，數字原地替換。關閉動態效果時直接到達最終狀態。

## 相關元件

- `LineChart`：多個資料點的連續趨勢。
- `Sparkline`：表格儲存格或卡片中的精簡趨勢。
- `ActivityHeatmap`：一整年的每日活動。
- `DonutChart`：長條需要表示整體的各個部分時使用。
