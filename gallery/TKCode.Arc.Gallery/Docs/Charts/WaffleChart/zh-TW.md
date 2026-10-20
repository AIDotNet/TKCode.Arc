---
title: 華夫圖
summary: 十乘十的單位圖表，每個方格代表百分之一；資料變化時，方格會飛到各自的新分組。
source-hash: cec4f2f81214
---

## 何時使用

- 需要讓人數清楚的佔比，例如回答佔比或以百分比表示的能源結構。
- 在兩組資料之間比較同一批類別，原地不動的方格說明了哪些發生了變化。
- 佔比很小卻必須保持可見的情境；圓餅圖或環形圖只會顯示一條細線。

## 何時不用

- 佔比隨時間變化：用 `LineChart` 或 `Streamgraph`。
- 類別很多且需要精確比較：用 `BarChart`。
- 巢狀整體中需要下鑽的某一部分：用 `Treemap`。

## 用法

每個 `WaffleCategory` 是一組方格。數值為權重：方格依其比例分配。第一個類別從左下角開始填滿，逐欄進行。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:WaffleChart Label="發電量"
                     Unit="TWh"
                     ItemsSource="{Binding Mix}" />
</UserControl>
```

也可以透過 `Items` 直接新增類別。

```csharp
var neutral = new WaffleChart { Label = "瀏覽器佔比", AccentKey = WaffleChart.NoAccent };
neutral.Items.Add(new WaffleCategory("chrome", "Chrome", 65.4));
neutral.Items.Add(new WaffleCategory("safari", "Safari", 18.6));
neutral.Items.Add(new WaffleCategory("other", "其他", 16.0));
```

### 資料集

替換 `ItemsSource` 即可切換資料集。保留所在分組的方格原地不動，其餘方格飛到各自的新區塊。

```csharp
energy.ItemsSource = Mix2015;
```

### 選取器與非類別資料

當資料來源不是 `WaffleCategory` 時，設定 `KeySelector` 與 `ValueSelector`。`LabelSelector` 讀取每個項目的標籤，`Decimals` 設定佔比顯示的小數位數。

```csharp
var survey = new WaffleChart
{
    Label = "問卷回答",
    ItemsSource = answers,
    KeySelector = item => ((Source)item).Name,
    ValueSelector = item => ((Source)item).Weight,
    Decimals = 1,
    MaxWidth = 300,
    IsLegendVisible = false
};
```

### 網格與強調色

`Rows` 與 `Columns` 設定網格，預設為十乘十。`AccentKey` 指定使用第一個數列顏色的類別；其他類別依序使用次要顏色，然後是中性色調。將 `AccentKey` 設為 `NoAccent`，則只使用中性色調。

```xml
<arc:WaffleChart Label="配額使用情況" Rows="5" Columns="20" AccentKey="used" ItemsSource="{Binding Quota}" />
```

### 釘選類別

釘選圖例中的某一列後，其方格會保持醒目標示。`ActiveKey` 保存被釘選的鍵，且為雙向繫結。

```csharp
energy.ActiveKeyChanged += (_, e) => pinned.Text = $"已釘選：{e.Category?.Label ?? "無"}";
```

## API 參考

<!-- api: WaffleChart -->

<!-- api: WaffleCategory -->

## 無障礙

- `Label` 為圖表命名。網格是一個可聚焦的群組，帶有角色描述以及按鍵說明。
- Tab 鍵先將焦點移到網格，然後依序移到每個圖例列。每個圖例列都是帶文字名稱的切換按鈕。
- 在網格中，方向鍵每次將讀數移動一個方格。PageUp 與 PageDown 跳到上一個或下一個類別的第一個方格；Home 與 End 分別跳到第一個與最後一個已填滿的方格。Escape 清除讀數。
- 在圖例列上按 Enter 或空白鍵，可釘選或釋放其類別。
- 禮貌型即時區域會朗讀讀數下方的類別、佔比與數值。一個隱藏的資料表列出每個類別的數值、佔比與方格數量。
- 方格對輔助技術隱藏。螢幕上會縮短較長的圖例名稱，但完整名稱保留在工具提示、即時區域與資料表中。

## 動態效果

- 方格盡量保留原位。其餘方格以錯落的波浪形式飛到新區塊，飛行中的方格會略微縮小並改變顏色。
- 圖例數值以臨界阻尼彈簧平順計數到新值。
- 首次顯示時，方格從左下角依序出現。
- 減少動態效果時，方格與佔比立即定位到位，並取消縮小、出現與錯落的效果。關閉動態效果的表現與減少動態效果相同。

## 相關元件

- `DonutChart`：單一環形中的佔比，中心顯示總計。
- `BarChart`：類別之間的精確比較。
- `Progress`：單一任務的完成進度；`UsageMeter`：配額與上限的對比。
- `Treemap`：類別呈巢狀結構且需要下鑽時使用。
