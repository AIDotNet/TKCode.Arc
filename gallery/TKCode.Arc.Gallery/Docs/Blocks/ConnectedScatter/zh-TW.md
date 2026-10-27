---
title: Connected scatter
summary: 兩個一起變化的度量畫成一條路徑：可以拖曳時間或回放的頭部點沿著月份前進，讀數卡隨之計數，目標線為健康的一側著色，一個開關可以讓整條路徑從一個系列變形到另一個系列。
source-hash: adce3d65008a
---

<Demo name="Preview" />

## 何時使用

`ConnectedScatter` 呈現兩個度量如何隨時間**一起**變化。每個月是 (x, y) 處的一個點，這些點依時間順序用平滑曲線連接，每一段只要夠長就帶一個小箭頭，頭部點標出你目前檢視的月份。折線圖會畫出兩條獨立的線，而這條路徑呈現的是兩者的關係：這對數值朝哪個方向走、在哪裡轉向、在哪裡越過閾值。

- **單位經濟模型。** 獲客成本對客戶價值，並帶有目標比值：當某個管道從回本變為虧損時，路徑會越過目標線。
- **隨時間變化的取捨。** 價格與轉換率、延遲與吞吐量、人數與產出：任何行進方向比單一數值更重要的資料對。
- **比較管道。** 分段開關會把同樣的月份從一個系列變形到下一個系列，讓眼睛跟著變化走。
- **講述一個故事。** 播放會讓頭部點以穩定的節奏走過各個月份，首次出現時會把整條路徑畫一遍。

## 何時不用

- **單一度量隨時間變化。** 請使用折線圖或面積圖（`Metrics dashboard`、`Revenue explorer`）；當另一根軸是時間時，路徑沒有任何額外價值。
- **同時呈現多個系列。** 一次只畫一條路徑。多個迴圈疊在一起無法閱讀；請使用開關，或小多圖版面。
- **無序的點。** 曲線暗示了順序。對於沒有先後次序的類別，請使用一般散佈圖。
- **數百個點。** 此區塊為每個系列大約 8 到 36 個月而設計；每個點都有圓點與箭頭，拖曳軸每個月有一個停靠點。

## 用法

此區塊衍生自 Avalonia 的 `TemplatedControl`。它的資料是一組 `ScatterSeries`；每個系列有 `Name`、選用的 `Key`，以及依時間順序排列的 `ScatterPoint`（`Label`、`X`、`Y`）。每個系列都應有相同的月份數，這樣開關才能把每個點移到與之對應的點。兩根軸是 `ScatterAxis` 物件。

```xml
<arc:ConnectedScatter Title="Unit economics by channel" TargetRatio="3">
    <arc:ConnectedScatter.XAxis>
        <arc:ScatterAxis Name="CAC" Title="Customer acquisition cost" Minimum="300" Maximum="1900" Step="200" />
    </arc:ConnectedScatter.XAxis>
    <arc:ConnectedScatter.YAxis>
        <arc:ScatterAxis Name="LTV" Title="12 month customer value" Minimum="1000" Maximum="6000" Step="1000" />
    </arc:ConnectedScatter.YAxis>
    <arc:ScatterSeries Name="Field sales" Key="field-sales">
        <arc:ScatterPoint Label="Jul 2024" X="1150" Y="3600" />
        <arc:ScatterPoint Label="Aug 2024" X="1175" Y="3630" />
    </arc:ScatterSeries>
</arc:ConnectedScatter>
```

```csharp
scatter.Series.Add(new ScatterSeries("Community", points, "community"));
scatter.SelectedSeriesKey = "community";   // the points morph to the new series
```

`SeriesSource` 可以改為接收你自己持有的集合（會跟隨 `ObservableCollection` 的變化）。只有一個系列時，開關會隱藏。

### 結構

- **標題區**：`Title`、說明（`Description`，或根據座標軸以及第一個與最後一個月份寫出的一句話）與系列開關。
- **讀數卡**：四格隨頭部點計數的內容：月份及其位置（"18 of 18"）、兩個數值及其相對第一個月的變化（"+$200 since Jan 2025"，或 "Same as Jan 2025"），以及比值，帶有狀態圓點與 "Above 3x target" 或 "Below 3x target"。狀態始終用文字寫出，絕不只靠顏色。
- **圖表**：縱軸標題與圖例（路徑、目標線、健康區），繪圖區，以及右側位於其下方的橫軸標題。
- **控制列**：一個 Play、Pause 或 Replay 按鈕，以及拖曳軸，其下方是季度標籤。

### 座標軸與目標線

`ScatterAxis` 的範圍與步長會嚴格依你寫的使用：不取整到好看的數字，也不強制從零開始，所以放大的座標軸會從你指定的位置開始。不設定 `Minimum`、`Maximum` 與 `Step`（NaN）時，會根據所有系列的資料推算，因此切換系列時座標軸保持不動。步長是 1,000 的倍數的刻度讀作 "$2k"；其他刻度使用區塊的 `ValueFormatter`；`TickFormatter` 會同時取代兩者。

`TargetRatio` 會畫出經過原點的虛線 y = k · x，為它標註（"3x CAC"），給它上方一側著色，並在其角落寫下 "Healthy, above 3x"。比值讀數與卡片顏色對未取整的數值採用同樣的判斷。設為 NaN 則移除這條線、健康區、狀態及其圖例項目。

<Demo name="Target" />

### 頭部點、拖曳軸與回放

頭部點位於真實的曲線上，而不是月份之間的直線上。**按下**拖曳軸會讓頭部點以 Smooth 彈簧滑到游標處；追上之後，拖曳時它與游標 1:1 跟隨，**放開**則會吸附到最近的月份。按鍵、點擊某個點以及回放結束，同樣會讓頭部點停在某個月份上。`MonthChanged` 恰好在此時觸發，絕不會逐影格觸發，帶有索引、系列與點；`MonthChangedCommand` 以相同的參數執行。`SelectedIndex` 是雙向的（−1 表示最後一個月），`Position` 是連續位置，包含頭部點移動時的小數部分。

**Play** 以恆定的節奏，從目前位置開始，每 `PlaybackMonthDuration`（240 ms）前進一個月；如果已在最後一個月，則從第一個月開始。**Pause** 會讓它停在兩個月之間；讀數卡寫出最接近的月份，數值則是插值得到的。到了最後一個月，按鈕變成 **Replay**。

<Demo name="Events" />

### 首次出現

區塊第一次有四分之一進入畫面時，頭部點會在 `DrawInDuration`（1.35 s，先慢後快再慢）內從第一個月跑到最後一個月，在身後畫出軌跡；落定時，虛線的未來路徑、空心圓點、未來的箭頭與標籤會在 240 ms 內一起淡入。設定 `PlayOnFirstAppear="False"` 可讓它靜止在最後一個月，或呼叫 `ReplayDrawIn()` 再次展示。減少動態效果與關閉動態效果會跳過這段。

<Demo name="Minimal" />

### 懸停、點擊與卡片

游標靠近某個月份 30 px 以內時，會出現光暈與一張卡片，顯示該月份、兩個數值及其比值。卡片不會移動頭部點；在點與點之間會滑行。點擊或按一下某個點會讓頭部點以彈簧移到那裡；在觸控螢幕上，手指按下時會顯示卡片。

### 標籤、註解與窄容器

第一個與最後一個月份標註在路徑的兩端外側，最後一個始終使用 Medium 字重。`ScatterNote` 為某個月份加上事件標籤（"Pricing change"），位於你選擇的一側。區塊會讀取自己的寬度：低於 720 時，讀數卡排成 2 × 2 格線，繪圖區高 330；低於 560 時，季度標籤縮減為第一個月、各個一月與最後一個月；低於 480 時，繪圖區高 300，註解、目標標籤與健康區標籤消失，橫軸每隔一個刻度跳過一個，拖曳軸移到按鈕下方；低於 380 時，數值為 18px。

<Demo name="Narrow" />

### 格式與文字

`ValueFormatter` 與 `RatioFormatter` 取代讀數卡、卡片、表格與播報中的 "$1,234" 和 "4.8x"。每一個可見的詞都是帶英文預設值的屬性：讀數名稱、"since" 與 "of" 的註解、狀態詞、目標與健康區文字、圖例、按鈕的文字與名稱、螢幕閱讀器表格以及播報格式。讀作 "Jan 2025" 的月份名稱會在拖曳軸下方得到季度標籤；其他的則得到大約六個等距的標籤。

<Demo name="Weekly" />

## API 參考

<!-- api: ConnectedScatter -->

<!-- api: ScatterSeries -->

<!-- api: ScatterPoint -->

<!-- api: ScatterNote -->

<!-- api: ScatterAxis -->

<!-- api: ScatterLabelSide -->

<!-- api: ScatterMonthChangedEventArgs -->

<!-- api: ScatterSeriesChangedEventArgs -->

## 無障礙

- 拖曳軸是名為 "Month" 的滑桿，範圍從 1 到月份數，並帶有已落定月份的值文字："Dec 2025: CAC $960, LTV $4,640, 4.8x, above the 3x target."。它在頭部點落定時更新，而不是在移動或回放過程中更新。
- 一個禮貌型即時區域會為每個落定的月份朗讀同樣的文字，系列變化時朗讀 "Series name. …"；回放在停止處播報。
- 圖表對螢幕閱讀器隱藏了細節，卡片也是如此；一張表格列出每個月份、兩個數值與比值，標題寫明系列與座標軸。
- 每個控制項都有名稱；播放按鈕的名稱隨狀態變化（"Play month by month"、"Pause"、"Replay from the first month"）。目標與狀態都用文字寫出。
- 顏色來自主題，淺色與深色下均適用；圓點、箭頭與標籤在著色的健康區上依然保持對比度。

### 鍵盤

- **Tab** 依序移到系列開關（一個停靠點，即所選系列）、播放按鈕與拖曳軸。圖表不接收焦點。
- 在拖曳軸上，**← ↓** 後退一個月，**→ ↑** 前進一個月；**PageUp** 前進三個月，**PageDown** 後退三個月；**Home** 與 **End** 跳到第一個與最後一個月。按鍵在兩端停住。從兩個月之間的位置出發，會前往所按方向上的下一個整月。每次按鍵都以 Smooth 彈簧移動頭部點，並落定在某個月份上。
- 在系列開關中，**← →** 立即改變選擇並循環；**↑ ↓** 前往上一個與下一個系列；**Home** 與 **End** 前往兩端。
- 在按鈕上按 **Enter** 或 **Space** 會播放、暫停或重播。
- 焦點環只在鍵盤焦點時繪製；在拖曳軸上用游標按下不會顯示它。

## 動態效果

- **首次執行**：頭部點用先慢後快再慢的曲線跑 1.35 s，讀數卡與拖曳軸隨之而動；落定時，未來的各圖層一起用 240 ms 淡入。
- **頭部點**：按鍵、點擊、放開與切換系列都使用 Smooth 彈簧（無過衝，約 0.65 s）；按下拖曳軸時它追趕游標，隨後 1:1 跟隨。回放或拖曳時，頭部點的光暈用 580 ms 的彈簧曲線增長到 1.25 倍。
- **回放**：恆定速度，每月 240 ms；暫停會保留小數位置。
- **切換系列**：每個點、每個箭頭乃至曲線本身，同時以 Smooth 彈簧彈向新系列；讀數卡計數；舊標籤用 180 ms 淡出，新標籤在其後用 240 ms 淡入。
- **懸停**：光暈用 160 ms 淡入；卡片原地淡入，並在點與點之間以 580 ms 的彈簧曲線滑行。
- **拖曳軸滑塊**：懸停時 1.08 倍，按下時 1.12 倍；鍵盤焦點時其中心圓點增長到 1.5 倍。
- 減少動態效果會跳過首次執行與滑行：回放每 960 ms 跳一個月，按鍵與點擊直接跳轉，切換系列會立即替換各點，只保留短暫的淡入淡出。關閉動態效果則讓一切立即生效。

## 相關

- `Metrics dashboard`、`KPI drilldown` 與 `Revenue explorer`，其他 Analytics 區塊。
- `SegmentedControl`，此區塊使用的開關。
- `Line replay`，另一個讓系列隨時間回放的區塊。
