---
title: 斜率圖
summary: 兩個座標軸上的前後比較：線條逐條繪出，每個數值旁顯示排名變化，切換資料集時每條線都會滑動到新的斜率。
source-hash: 469da0f5d974
---

## 何時使用

- 對兩個時間點的前後比較，例如第一季與第二季各通路的轉換率。
- 顯示哪些項目上升或下降，以及兩個時間點之間排序如何變化。
- 三到十項。超過約十項時，標籤會互相擠壓。

## 何時不用

- 三個或更多時間點：用 `LineChart`。
- 只需要每項的最新數值：用 `BarChart`。
- 整體中的各部分，而非兩個時間點之間的變化：用 `DonutChart`。

## 用法

每個 `SlopeItem` 是一條線，包含 `Key`、`Label`、`Start` 數值與 `End` 數值。`StartLabel` 與 `EndLabel` 用於命名兩個時間點。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:SlopeChart Label="各通路轉換率"
                    StartLabel="Q1"
                    EndLabel="Q2"
                    HighlightKey="search"
                    ItemsSource="{Binding Channels}" />
</UserControl>
```

### 資料集

替換 `ItemsSource` 即可切換資料集。每條線、每個點與每個標籤都會滑動到新的斜率。在同一步驟中變更 `Label`，使文字與資料保持一致。

```csharp
conversion.Click += (_, _) =>
{
    chart.Label = "各通路轉換率";
    chart.ItemsSource = Conversion;
};
```

### 數值與變化的格式

`ValueFormatter` 格式化兩端的數值。`ChangeFormatter` 格式化排名旁的變化量，它接收變化量與對應的項目。

```csharp
chart.ValueFormatter = v => $"{v:0.0}%";
chart.ChangeFormatter = (change, _) => $"{(change >= 0 ? "+" : "−")}{Math.Abs(change):0.0} 個百分點";
```

### 高亮與排名

`HighlightKey` 以第一個系列的顏色繪製一個項目，其餘項目保持中性色。`IsRankVisible` 用於顯示或隱藏排名變化。

```xml
<arc:SlopeChart Label="收盤價"
                StartLabel="2023"
                EndLabel="2024"
                IsRankVisible="False"
                MaxWidth="420"
                ItemsSource="{Binding Values}" />
```

### 讀取項目

`ActiveKey` 為指標或鍵盤所在的項目，且為雙向繫結。每次變更都會觸發 `ActiveKeyChanged`。

```csharp
chart.ActiveKeyChanged += (_, e) => reading.Text = e.Item is { } item
    ? $"{item.Label}: {item.Start} → {item.End}"
    : "沒有聚焦的項目";
```

## API 參考

<!-- api: SlopeChart -->

<!-- api: SlopeItem -->

## 無障礙

- `Label` 為圖表命名。圖表是帶有角色描述與按鍵說明的可聚焦群組。
- Tab 鍵將焦點移到圖表上，並優先讀取結束數值最高的項目。下、右方向鍵移到下一項，上、左方向鍵移到上一項，順序依據結束數值。
- Home 與 End 分別移到第一項與最後一項，Escape 清除讀數。觸控按下並拖曳會讀取最近的線條。
- 禮貌的即時區域會播報兩個數值、變化量與排名變化。隱藏的資料表依結束數值的順序列出各項目。
- 排名變化使用帶數字的箭頭符號，因此不只依靠顏色表示。線條與標籤對輔助技術隱藏。

## 動態效果

- 首次顯示時，每條線從起點繪製到終點，並以短暫的錯落時差進行。圓點與標籤隨線條落定而出現。
- 切換資料集時，線條、圓點、標籤與引導線在共用的變形彈簧上移動。排名數字會朝變化的方向滾動。
- 取得焦點的項目移到最前，其餘項目淡出。提示滑動到其所在線條的中點。
- 減少動態效果時，所有線條一次繪製完成，變化時也一次移動。關閉動態效果時直接到達最終狀態。

## 相關元件

- `LineChart`：三個或更多時間點。
- `BarChart`：單一時間點上每項的單一數值。
- `Ridgeline`：各組分布隨時間的變化。
