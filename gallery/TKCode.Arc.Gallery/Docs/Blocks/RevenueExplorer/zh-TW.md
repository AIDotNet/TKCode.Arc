---
title: Revenue explorer
summary: 每月經常性收入依新增、擴充與流失三層堆疊，搭配三個頭條數字與一條總覽列：拖曳其視窗或把手，路徑、座標軸與數字都會滑向新的範圍，並提供範圍預設、系列圖例按鈕、與上一週期的比較，以及同時適用於指標與鍵盤的十字線讀數卡。
source-hash: 509e2de1d63e
---

<Demo name="Preview" />

## 何時使用

`RevenueExplorer` 在一張卡片裡回答訂閱制事業的每月問題：現在有多少經常性收入（MRR），新客戶與既有客戶每月各增加了多少，流失了多少，以及是否比上一週期更好？圖表把新增與擴充收入堆疊在零線上方，把流失畫在零線下方，因此一個月的淨變動就是各層之間的距離。圖表下方的總覽列容納每一個月；它的視窗就是圖表、三個頭條數字與表格所顯示的範圍。

- **收入檢討頁面。** 期末 MRR、每月平均淨新增與流失率，各自帶著變化量，位於圖表上方並隨範圍變化。
- **在時間中移動。** 拖曳總覽視窗可以瀏覽歷史，拉動把手可以放大；路徑、座標軸標籤與數字會滑動到位，而不是重新繪製。
- **用過去檢驗趨勢。** "Compare" 會把相同長度的上一週期畫成兩條虛線，讀數卡還會加上上個月的淨新增。

## 何時不用

- **單一指標隨時間變化。** 沒有堆疊也不需要縮放時，折線圖（或 `Metric explorer` 區塊）更輕量。
- **從一個總額到另一個總額的橋接。** `MRR waterfall` 展示期初 MRR 如何變成期末 MRR。
- **按日或按小時的資料。** 這裡的單位是月：視窗至少包含三個月，標籤也是月份。
- **超過幾年的月份。** 三十六到六十個月效果良好；超出後請彙總為季度。

## 用法

此區塊衍生自 Avalonia 的 `TemplatedControl`。它的資料是一組 `RevenueMonth`，從最舊開始，每個日曆月一個，不留缺口：`New` 與 `Expansion`（零或更大）、`Churn`（負數；傳入正數時會被當作其負數）以及可選的該月期末 `Mrr`。省略 `Mrr` 時，區塊會從 `OpeningMrr`（第一個月之前的收入）開始，把每個月的淨新增累加到上一個月上。`OpeningMrr` 也是第一個月流失率與第一個變化量的基數。

```xml
<arc:RevenueExplorer Title="Subscription revenue" OpeningMrr="1050000">
    <arc:RevenueMonth Month="2024-03-01" New="41200" Expansion="14800" Churn="-17600" />
    <arc:RevenueMonth Month="2024-04-01" New="43900" Expansion="15300" Churn="-18200" />
</arc:RevenueExplorer>
```

```csharp
revenue.OpeningMrr = 1_050_000;
revenue.Months.AddRange(months);          // or: revenue.MonthsSource = observableMonths;
revenue.SetRange(12, 23);                 // months 12 to 23, sliding there
revenue.ApplyPreset(3);                   // the newest three months, as the "3M" button does
```

`MonthsSource` 接受由你持有的集合（`ObservableCollection` 會被追蹤）；就地修改某個月的數字會重建區塊。在你選擇範圍之前，顯示最新的 `DefaultRangeMonths`（12）個月。

### 結構

- **標題區。** `Title`，其下以文字顯示的範圍（`Mar – Aug 2026`；在同一年之內省略第一個年份），右側是範圍按鈕。這些按鈕選擇最新的 3 個月、6 個月、12 個月與全部；範圍為其他值時，沒有按鈕處於選取狀態。可以用你自己的 `Presets` 取代它們。
- **頭條數字。** 期末 MRR 及其相對範圍前一個月的變化；每月平均淨新增及其相對相同長度的上一範圍的變化；流失率（流失收入除以這些月份的期初收入）及其以百分點計的變化。數值逐位滾動，箭頭在向上與向下之間翻轉，文字向上滑出再滑入。基數缺失的變化（最初幾個月、`All`）會被省略。流失下降是好事，以綠色繪製。
- **工具列。** 三個系列圖例按鈕（`New`、`Expansion`、`Churn`）與比較開關。
- **圖表。** 左側是 Y 軸標籤，繪圖區下方是月份標籤（一月會顯示年份），以及各個層。
- **總覽。** 每個月以小型層顯示，帶有年份刻度，視窗兩側各有一個把手。

### 範圍

`RangeStart` 與 `RangeEnd`（月份索引，都包含在內，雙向）就是範圍；`RangeChanged` 在每次變化時回報其來源：`Preset`、`Drag`、`Keyboard`、`Click` 或 `Api`。拖曳視窗時它與指標一比一移動，而範圍是最接近的整月，因此數字會在視窗越過某個月時變化；圖表以彈簧追隨視窗，放開後視窗會落定到整月。把手移動一條邊，且與另一條邊的距離永遠不會小於 `MinRangeMonths`（3）。點擊視窗之外的總覽列，會把視窗的中心移到那裡。

<Demo name="LongHistory" />

### 系列與比較

`IsNewVisible`、`IsExpansionVisible` 與 `IsChurnVisible`（雙向）切換各層：被隱藏的層收縮為零，其上方的層隨之下落，Y 軸重新適配，圖例按鈕的色塊變空。取值範圍跟隨可見的堆疊：上界比最高堆疊高 18%，再加上最深流失的 8%，下界比最深流失低 20%。所有系列都關閉時，座標軸保留，圖表顯示 `EmptyText`。

`IsCompareEnabled` 為上一週期的月份畫出兩條虛線（新增與擴充堆疊的頂部，以及流失），並把它們納入 Y 軸範圍。在讀數卡中，會有一列加上該月的淨新增。沒有上一週期的月份（最初幾個月）沒有虛線。

<Demo name="Compare" />

### 檢視某個月

指向圖表：十字線與三個圓點標出範圍內最近的月份，一個面板顯示三個系列、淨新增，以及在比較時上一週期的淨值。面板位於十字線右側 14px 處，快要超出圖表時翻到它的左側；它在月份之間滑行，並淡入淡出。`ScrubIndex`（雙向）是被檢視的月份，`ScrubChanged` 回報它。

<Demo name="Events" />

### 窄版容器

此區塊讀取自身的寬度。小於 600 時繪圖區高 216；小於 560 時內距收緊到 16，頭條數字得到的空間變少；小於 440 時範圍按鈕獨占一列並平均分配，數字上下堆疊且數值靠右，圖例按鈕縮小。當月份間距小於 40px 時，座標軸每隔一個（每隔兩個……）月份顯示一次。

<Demo name="Narrow" />

### 文字、格式與貨幣

每一個可見的文字都是帶有英文預設值的屬性（`NewLabel`、`MrrLabel`、`CompareText`、`SinceFormat`、`VersusFormat`、螢幕閱讀器名稱、播報格式）。金額是精簡貨幣格式（`$63.3k`、`$2.56M`）：用 `CurrencySymbol` 變更符號，用 `CultureName` 變更語言，或用 `ValueFormatter`（讀數卡、表格、播報）、`AxisFormatter`、`MonthFormatter`、`MonthYearFormatter` 與 `RangeFormatter` 取代文字。頭條數字始終遵循 `CurrencySymbol`：它們的數字會滾動，這需要把符號與單位作為單獨的部分。

<Demo name="Euro" />

## API 參考

<!-- api: RevenueExplorer -->

<!-- api: RevenueMonth -->

<!-- api: RevenueRangePreset -->

<!-- api: RevenueRangeChangedEventArgs -->

<!-- api: RevenueSeriesChangedEventArgs -->

<!-- api: RevenueCompareChangedEventArgs -->

<!-- api: RevenueScrubChangedEventArgs -->

<!-- api: RevenueSeries -->

<!-- api: RevenueRangeSource -->

## 無障礙

- 圖表是一個滑桿，名為 "Inspect a month"，涵蓋範圍內的各個月；它的值文字包含該月的所有數字（"Mar 2026: new $72.5k, expansion $31.5k, churn −$34.1k, net new $69.9k"），其說明文字概述範圍、期末 MRR 與每月平均淨新增。
- 總覽視窗及其兩個把手是滑桿（"Selected range"、"Range start"、"Range end"），其值文字是範圍或月份。
- 範圍按鈕與系列圖例按鈕是切換按鈕（有按下狀態）；比較開關是一個開關。每個頭條數字作為一個短語朗讀，並用文字說出其變化（"MRR, $2.56M, up +42.3% since Feb 2025"）。
- 一張隱藏的表格列出範圍內每個月的變動與期末 MRR，並隨範圍變化。
- 用鍵盤移動範圍或讀數卡時，會以禮貌方式播報；讀數卡與十字線對螢幕閱讀器隱藏，因為相同的文字就是滑桿的值。
- 鍵盤焦點環繪製在圖例按鈕、開關、繪圖區（圓角 12）、視窗（圓角 10）或把手（方形）外側 2px 處。

### 鍵盤

- **Tab** 依序造訪範圍按鈕（一個停靠點；**← →** 變更範圍）、三個圖例按鈕、比較開關、圖表、總覽視窗、起點把手與終點把手。
- **圖表：** 聚焦時在最後一個月開啟讀數卡；**← ↓** 與 **→ ↑** 各移動一個月，**Home / End** 跳到範圍的兩端，**Escape** 關閉讀數卡並保留焦點（下一次方向鍵重新從最後一個月開始）。這裡 **Shift** 不會改變步長。
- **視窗：** **← ↓** 與 **→ ↑** 移動一個月（**Shift** 為三個月），**Home / End** 到歷史的兩端；寬度保持不變。**起點把手：** 移動第一個月，與最後一個月的距離永遠不小於三個月；**終點把手** 同理。
- **Enter / Space** 按下圖例按鈕、開關與範圍按鈕。

## 動態效果

- **範圍變化**（預設、點擊總覽列、按鍵）：圖表的 x 取值範圍、視窗、把手與月份標籤跟隨同一條 Smooth 彈簧並同時抵達；路徑每一幀重建（沒有路徑變形）。y 取值範圍同樣使用彈簧；新的格線在滑入的同時於 50ms 後淡入，被移除的格線在 160ms 內淡出。範圍文字、頭條文字與變化量向上滑出再滑入 0.45em；數字滾動；預設膠囊在 Gentle 彈簧上滑行。
- **拖曳**：視窗精確跟隨指標，圖表持續追趕；放開後視窗以 Gentle 彈簧落定到整月（約半秒）。按下時抓握部件縮放到 1.15；視窗邊框在懸停時從強邊框色變為弱化文字色，按住時變為強調色。
- **系列**：每一層的厚度在 0 與 1 之間以彈簧變化（Smooth），它的線條隨之淡出；空文字在 120ms 後淡入。圖例按鈕的色塊在 240ms 內填滿，並在一條 580ms 的曲線上於 1 與 0.8 之間縮放。
- **比較**：一條彈簧把每條虛線從目前的線混合到上一週期的線，同時淡入（在回溯的前 40% 保持不透明）。
- **讀數卡**：十字線與圓點跳到每個月；面板在 160ms 內以 0.97 的縮放淡入，在彈簧上滑行，再淡出（參考實作把面板遺留在原處；這裡會乾淨地離開）。
- **減少動態效果** 會讓所有彈簧立即到位，只保留短暫的淡入淡出（讀數卡、刻度，文字切換為 120ms 的交叉淡入淡出）。**關閉** 會立即套用所有變化。

## 相關元件

- `Metrics dashboard`、`KPI drilldown` 與 `Metric explorer`，即其他 Analytics 區塊。
- `MRR waterfall`，從期初到期末 MRR 的橋接。
- `SegmentedControl`，預設所參照的公開範圍開關。
