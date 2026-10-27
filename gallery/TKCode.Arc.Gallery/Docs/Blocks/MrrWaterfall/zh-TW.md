---
title: MRR waterfall
summary: 從週期開始到結束的經常性收入橋接：為新增業務、擴充、重新啟用、收縮與流失繪製的懸浮柱，一個列出任意柱背後帳戶的側面板，以及讓每根柱、刻度與數字在同一條彈簧上移動的週期與單位開關。
source-hash: 48c0aedf240d
---

<Demo name="Preview" />

## 何時使用

`MrrWaterfall` 回答「這個週期裡我們的經常性收入去了哪裡？」第一根柱是期初收入，最後一根柱是期末收入，中間的柱懸浮著：每一根都從上一步停下的位置開始，贏得收入（新增業務、擴充、重新啟用）時上升，失去收入（收縮、流失）時下降。圖表上方，期末收入及其變化旁邊是財務團隊最先詢問的三個比率：淨收入留存率、快速比率與客戶流失率。

- **董事會與財務報告。** 一張圖展示一個月或一個季度，每一步背後的帳戶只需點擊一下。
- **營收營運檢討。** 在各月與整個季度之間切換，看哪一類變動發生了變化，並用占期初百分比的單位比較規模不同的週期。
- **客戶成功追蹤。** 選取流失柱看誰離開了以及原因，再選取擴充柱看誰成長了。

## 何時不用

- **多個週期的趨勢。** 橋接一次只展示一個週期；十二個月請使用折線圖或面積圖。
- **不是從已知水準起步的收入。** 每根柱都取決於期初收入；沒有它時請使用長條圖。
- **每一步有數百個帳戶。** 面板列出金額最大的帳戶，並把其餘帳戶合併為一列；它不是表格。請從該列連結到帳戶清單。

## 用法

此區塊衍生自 Avalonia 的 `TemplatedControl`。它的資料是一組 `MrrPeriod`。一個週期包含期初收入（`StartingMrr`）、期初與期末的付費帳戶數、它的名稱與日期（`Label`、`LongLabel`、`Caption`、`StartDateText`、`EndDateText`），以及依柱順序排列的 `Movements`。每個 `MrrMovement` 有一個 `Kind`、一個正數 `Amount`（正負號由類型決定）、一個 `AccountCount` 與其背後具名的 `Accounts`。期末收入、淨變化與每個比率都由區塊計算。

```xml
<arc:MrrWaterfall Title="Recurring revenue bridge" Scope="All tiers">
    <arc:MrrPeriod Label="Q2" LongLabel="Q2 2026" Caption="Apr 1 – Jun 30, 2026"
                   StartDateText="Apr 1" EndDateText="Jun 30"
                   StartingMrr="286400" StartingAccounts="892" EndingAccounts="948">
        <arc:MrrMovement Kind="New" Label="New business" ShortLabel="New" CompactLabel="New"
                         Amount="39800" AccountCount="116" CountNoun="new customers" />
    </arc:MrrPeriod>
</arc:MrrWaterfall>
```

```csharp
bridge.Periods.AddRange(periods);
bridge.SelectedPeriod = periods[^1];
bridge.SelectedIndex = 5; // the churn bar
```

`PeriodsSource` 則接受由你持有的集合（`ObservableCollection` 會被追蹤，修改所顯示清單中的某個週期會就地更新區塊）。只有一個週期時，週期開關會被隱藏。

### 結構

- **標題區**：`Title`、副標題（`Scope` 後接週期的 `Caption`），以及週期開關與單位開關。
- **摘要**：逐位滾動的期末收入、它相對期初的變化（箭頭、金額與百分比，依方向著色），以及三個比率。
- **圖表**：一條數值座標軸、七根帶標籤的柱、把每一步的終點帶到下一根柱的連接線，以及選取柱下方的標記。
- **面板**：選取柱（或被預覽的柱）背後的帳戶。

### 座標軸與柱

數值座標軸不從零開始，否則各步在總額旁邊將看不見。它的取值範圍在最低水準之下延伸橋接跨度的 0.6，在最高水準之上延伸 0.05，其刻度步長取最接近該範圍六分之一的、由 1、2、2.5 與 5 乘以十的冪得到的值。因此第一根與最後一根柱被截斷：每根柱上的一道斜向斷裂標記表明了這一點。柱最寬 56，或為其所占區間的 56%，且每條邊都落在整像素上。

### 選取一根柱與預覽一根柱

點擊一根柱或按方向鍵，面板會列出其背後的帳戶：依金額排列最大的 `MaxNamedAccounts`（5）個，然後是代表其餘部分的一列。每一列有一條表示其占該步份額的長條。起點柱與終點柱則列出 `StartingBreakdown` 與 `EndingBreakdown`（方案層級），沒有「其餘」列。`SelectedIndex` 是雙向的，在週期與單位變化後依然保留，並引發 `SelectionChanged` 與 `SelectionCommand`。

使用滑鼠時，指向一根柱會預覽它的清單而不選取它：該柱出現底板，其他柱與標籤淡出，提示文字變為說明點擊會保留該清單。`HoveredIndex` 回報它。觸控只會選取。

### 週期與單位

`SelectedPeriod` 與 `ValueMode` 是雙向的。變更其中任何一個都會讓柱、連接線、座標軸、標籤與面板列一起移動：見動態效果。

<Demo name="Percent" />

在 `PercentOfStart` 中，每個值都是占該週期期初收入的份額，因此起點柱是 100%。頭條數字仍然使用貨幣。刻度、標籤與面板金額跟隨該單位。

### 文字、符號與格式

所有文字都來自 `Labels`（`MrrWaterfallLabels`）：開關名稱、頭條標籤、面板提示、讀給螢幕閱讀器的句子。`CurrencySymbol` 在所有位置變更符號；`CurrencyFormatter`、`CompactFormatter` 與 `PercentFormatter` 取代圖表與朗讀文字的數字格式。會滾動的數字使用該符號與內建格式。

<Demo name="SinglePeriod" />

### 窄版容器

此區塊讀取自身的寬度。小於 800 時面板移到圖表下方；小於 560 時頭條與統計數字縮小，統計數字共用一列；當某個區間窄於 72 時，柱名稱縮短為 `CompactLabel`，柱標籤去掉貨幣符號；小於 48 時名稱使用 10px 字級。

<Demo name="Narrow" />

### 事件

<Demo name="Events" />

## API 參考

<!-- api: MrrWaterfall -->

<!-- api: MrrPeriod -->

<!-- api: MrrMovement -->

<!-- api: MrrAccount -->

<!-- api: MrrWaterfallLabels -->

<!-- api: MrrSelectionChangedEventArgs -->

<!-- api: MrrPeriodChangedEventArgs -->

<!-- api: MrrValueModeChangedEventArgs -->

<!-- api: MrrMovementKind -->

<!-- api: MrrValueMode -->

## 無障礙

- 圖表是一個可聚焦的群組，其名稱是一句話，給出週期、期初收入、每一步與期末收入，並以按鍵提示作為說明文字。鍵盤焦點環圍繞選取柱所在的欄。
- 每次選取都會以禮貌方式播報柱的名稱、帶正負號的金額與帳戶數："Churn, −$18,460, 79 customers left"。週期變化會播報發生了什麼變動。
- 一張隱藏的表格為每根柱列出變動、金額、占期初收入的份額與帳戶數。
- 面板以它所展示的柱命名（"Accounts behind churn"）。開關有名稱，頭條數字作為一個短語朗讀，並用文字說出其變化，而不僅靠顏色。

### 鍵盤

- **Tab** 依序經過週期開關、單位開關與圖表，各占一個停靠點。
- **← ↑** 與 **→ ↓** 把選取移到上一根或下一根柱，並從最後一根柱循環回第一根。移動即選取，因此面板隨之變化。
- **Home / End** 選取第一根與最後一根柱。
- 在兩個開關中，方向鍵變更選取並循環。

## 動態效果

- **週期或單位**：柱端、連接線、標籤與座標軸取值範圍共用一條臨界阻尼彈簧，約 600 ms 內落定。標籤隨之計數（"+$21.9k" 變到 "+$39.8k"）。保留的刻度隨取值範圍滑動；新刻度淡入（60 ms 後開始，240 ms），被去掉的刻度在滑動的同時於 160 ms 內淡出。單位變化會取代所有刻度。
- **面板列**：列依帳戶比對。保留的列在同一條彈簧上滑到新位置，新列淡入並上升（240 ms，間隔 35 ms），被移除的列在原處於 120 ms 內淡出，份額長條在彈簧上成長。列內的數字逐位滾動。
- **選取標記**：一條快速、不回彈的彈簧（橫跨圖表寬度約 400 ms）；柱名稱在 160 ms 內變為加重。
- **懸停與焦點**：其他柱在 160 ms 內淡至 28%，標籤淡至 32%；底板立即出現。
- **文字**：副標題、頭條標籤、變化文字、面板標題與列詳細資料向上滑出再滑入（滑出 160 ms，滑入 240 ms）。
- 減少動態效果會讓一切立即到位，唯獨被去掉的刻度仍會淡出 160 ms。關閉會立即套用每一個變化。

## 相關元件

- `Metrics dashboard`、`Cohort retention` 與其他 Analytics 區塊。
- `SegmentedControl`，此區塊所用的開關。
