---
title: Small multiples
summary: 一組小折線圖，每個分組一張，合在一起當作一幅圖來讀：一條十字線穿過所有面板，縱軸可在各分組自己的範圍與統一範圍之間切換，面板可依數值、成長或名稱重新排序，變化不大的分組可以淡化。
source-hash: 1c23390a1f38
---

<Demo name="Preview" />

## 何時使用

`SmallMultiples` 回答的問題是：「這些分組在同一段時間裡如何比較？」每個序列都有自己的小面板，橫軸相同、編碼相同，讀者一眼就能看完整張格狀圖，不必在一張圖裡追蹤八條互相交錯的線。

- **一個指標，多個分組。** 各市場的營收、各方案的註冊數、各客服管道的工單：同一個數字，在相同的月份或週裡為多個分組記錄。
- **找出變化大的分組。** 每個面板都標出自較早期間以來變化了多少，格狀圖可以依此排序，幾乎沒變的分組可以淡化到背景裡。
- **在所有面板中讀同一個期間。** 指向任何面板，十字線就會穿過所有面板移動，格狀圖上方的讀數會顯示該期間與總量。

## 何時不用

- **同一張圖上的少量序列。** 當兩三條線最好直接比較時，`LineChart` 會把它們畫在一起，並提供一條十字線與圖例切換。
- **需要深入探索的單一指標。** 請使用可縮放或下鑽的單張圖表；小倍數圖以細節換取全貌。
- **表格裡的趨勢。** 改在列中放一個 `Sparkline`。
- **精確的數字。** 如果讀者更需要數字而不是形狀，表格更合適。

## 用法

本區塊衍生自 Avalonia 的 `TemplatedControl`。提供一組期間（由舊到新），並為每個面板提供一個 `SmallMultiplesSeries`，每個期間一個值。四到十二個面板效果最好：容器寬度達到 640px 時格狀圖為四欄，較窄時為兩欄。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:SmallMultiples Title="Revenue by market"
                        Description="Monthly net revenue"
                        SeriesLegendText="Market"
                        GhostLegendText="All markets, scaled to fit" />
</UserControl>
```

```csharp
chart.Periods.AddRange(months.Select(m => new SmallMultiplesPeriod(m.ToString("MMM yyyy"), m.ToString("MMMM yyyy"))));
chart.Series.Add(new SmallMultiplesSeries("us", "United States", usRevenue));
chart.Series.Add(new SmallMultiplesSeries("ca", "Canada", caRevenue));
```

也可以用 `SeriesSource` 與 `PeriodsSource` 傳入你自己持有的集合（`ObservableCollection` 會被追蹤）。序列的 `Key` 能在清單重建時保留它的面板與進行中的動畫。

### 組成

- **頁首**：`Title`、`Description` 以及三個控制項：排序（Latest、Growth、Name）、刻度（Independent、Shared）與 "Dim flat trends" 開關。寬度小於 640 時，它們會換行到標題下方。
- **讀數**：十字線所在的期間（或最新期間）、它的位置（"11 of 24"）或 "Latest"、所有序列的合計，以及圖例：實線代表一個序列，虛線代表總量。
- **面板**：名稱、十字線處的值、帶箭頭的成長與比較的期間（"vs"）、右側欄位中的縱軸刻度、總量的虛線虛影，以及面積、折線與末端圓點。

### 成長與平緩趨勢

成長比較的是最新值與 `GrowthLag` 個期間之前的值（預設 12，也就是一年前的同月；期間不足時與第一期比較）。成長的絕對值小於 `FlatThreshold`（0.05 即 5%）時視為平緩：面板顯示 "Flat" 與其百分比，並且在 `IsFlatDimmed` 開啟時，面板內容淡化到 38%，格線則維持不變。

### 刻度

`Scale` 決定面板之間如何比較。**Independent** 讓每個序列配合自己的範圍，因此比較的是形狀；**Shared** 讓每個面板都從零開始、使用最大的範圍，因此比較的是大小。切換時，座標軸與折線一起以彈簧過渡。虛影一律使用自己的範圍：它呈現的是整體形狀，而不是可供比較的大小。

<Demo name="SharedScale" />

### 自訂單位

預設把數值讀作千美元（$412k、$1.71M）。為面板標題、合計與螢幕閱讀器傳入 `ValueFormatter`，為精簡的縱軸刻度傳入 `TickFormatter`。這個範例呈現每週計數，成長與四週前比較，寬度為 560。

<Demo name="Signups" />

### 追蹤十字線

`CrosshairIndex` 是十字線所在的期間，隱藏時為 null；`CrosshairIndexChanged` 會回報每次移動。`SetCrosshairIndex` 從程式碼移動十字線。`SortChanged` 與 `ScaleChanged` 回報控制項的變化；`Sort`、`Scale` 與 `IsFlatDimmed` 都是雙向繫結。

<Demo name="Crosshair" />

### 其他文字

每段文字都是一個帶英文預設值的屬性：選項（`LatestSortText`、`GrowthSortText`、`NameSortText`、`IndependentScaleText`、`SharedScaleText`、`DimFlatText`）、讀數（`LatestText`、`PositionFormat`、`TotalText`）、圖例（`SeriesLegendText`、`GhostLegendText`）、趨勢（`FlatText`、`CompareFormat`、`UpFormat`、`DownFormat`）、螢幕閱讀器名稱（`SortLabel`、`ScaleLabel`、`PanelsLabel`、`KeyboardHint`）以及各種播報。

## API 參考

<!-- api: SmallMultiples -->

<!-- api: SmallMultiplesSeries -->

<!-- api: SmallMultiplesPeriod -->

<!-- api: SmallMultiplesSort -->

<!-- api: SmallMultiplesScale -->

## 無障礙

- **Tab** 依序經過排序控制項、刻度控制項、淡化開關，然後是面板；所有面板是同一個 Tab 停駐點。
- 在面板上，**Left** 與 **Right** 讓十字線在所有面板中移動一個期間；**Page up** 與 **Page down** 移動十二個；**Home** 與 **End** 跳到第一個或最新的期間；**Escape** 清除十字線。以鍵盤聚焦面板時，十字線會落在最新期間。
- 在排序與刻度控制項中，方向鍵選擇上一個或下一個選項。
- 鍵盤移動會以禮貌方式讀出：期間、合計與領先的分組。變更排序、刻度或淡化也會被讀出。
- 每個面板的名稱包含序列名稱、十字線處的值，以及用文字描述的趨勢（"Up 15.3% vs Oct 2025"、"Flat"）。
- 淡化從來不是唯一的訊號：每個面板還會顯示箭頭與帶正負號的百分比（使用真正的減號），或是 "Flat" 一詞。鍵盤聚焦時，面板四周會繪製共用的焦點框。

## 動態效果

- **切換刻度**：每個面板縱軸範圍的兩端都沿平滑彈簧移動（約 400 毫秒，無回彈）。繪圖區裡的一切每一格都依這個範圍重畫，所以刻度標籤隨數值一起移動，折線隨之拉長或壓平；新刻度在 240 毫秒內淡入，舊刻度同時淡出。
- **排序**：每個面板從原本的位置出發，以彈簧落入新的格子，並帶著自己的分隔線一起移動。
- **十字線**：第一個位置立即顯示；之後的移動以短彈簧滑動，每個面板的圓點沿著自己的折線在點與點之間移動。
- **淡化**：平緩面板的內容在 240 毫秒內淡化，格線維持不變。
- 減少動態效果時，重新縮放、重新排序與十字線移動都會立即完成。關閉動態效果時，所有變化立即生效。

## 相關

- `LineChart`：在同一張圖上繪製少量序列，並提供滑動的十字線。
- `Sparkline`：在數值旁顯示精簡的趨勢。
- `Line replay`：Line charts 中的另一個區塊，用於依時間重播單一序列。
