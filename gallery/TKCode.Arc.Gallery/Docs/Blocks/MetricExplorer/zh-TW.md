---
title: Metric explorer
summary: 一排 KPI 卡片，每張都能展開為可拖曳檢視的圖表：卡片表面長成面板，分頁讓折線逐點從一個指標變形到另一個指標，範圍開關在按日資料上移動一個鏡頭視窗，並把本週期與上一週期對照繪製。
source-hash: 08fe2ea12c17
---

<Demo name="Preview" />

## 何時使用

`MetricExplorer` 位於成長或財務頁面的頂部：四個左右重要的數字，各自帶有近期走勢，並提供一種無需離開頁面就能細看的方式。卡片顯示最新值、所選範圍內的變化和一條迷你趨勢線。開啟一張卡片，它就變成同一指標的圖表，附有其他指標的分頁、一個範圍開關，以及一個跟隨指標的讀數卡。

- **需要再深一層細節的 KPI 列。** 圖表在背後畫出上一週期的線，回答「這是偶發波動還是趨勢？」，無需單獨的頁面。
- **週期對比。** 圖表上的每一天都與一個週期之前的同一天對比，因此拖曳檢視時讀到的是「Sep 6 vs Aug 7」和變化，而不只是一個值。
- **兩三種日資料。** 金額、計數和比率共用一個區塊：每個指標各自選擇其格式、座標軸精度，以及上升是否算好。

## 何時不用

- **單一主打數字。** 請使用統計卡片；為一個值而展開圖表是多餘的負擔。
- **幾十個指標。** 這一列和分頁列是為三到五個指標設計的。更多時請使用表格或 `Metrics dashboard` 區塊。
- **非按日的資料。** 圖表假定每天一個值，並將一個範圍與其之前相同天數的範圍比較。對不規則數列進行刷選和縮放，請使用 `Revenue explorer`。
- **精確查詢。** 讀數卡一次只顯示一天；當使用者需要複製數字時，請在區塊旁放一個表格。

## 用法

此區塊衍生自 Avalonia 的 `TemplatedControl`。它的內容是一組 `ExplorerMetric`。每個指標有 `Title`（卡片的標籤和讀數卡的標題）、用於分頁的 `ShortTitle`、用於圖表下方那一行的 `Description`，以及 `Values`：一個按日的數列，最早的在前，最後一個值屬於 `EndDate`（未設定時為今天）。該數列至少需要最長範圍的兩倍再加一天，因為圖表會把每個範圍與它之前的範圍對照繪製。

```xml
<arc:MetricExplorer Title="Workspace health" Caption="Refreshed Oct 14, 2026 at 08:30 UTC">
    <arc:ExplorerMetric Title="Annual run-rate" ShortTitle="ARR" ValueFormat="CurrencyCompact"
                        Values="{Binding Arr}" EndDate="{Binding Today}"
                        Description="Yearly value of active contracts, after discounts." />
</arc:MetricExplorer>
```

```csharp
explorer.Metrics.Add(new ExplorerMetric("Weekly active teams", teams, ExplorerValueFormat.Count) { ShortTitle = "Teams" });
explorer.Open(0);          // the surface grows out of the first card
explorer.SelectedRangeIndex = 1;   // 90 days
```

`MetricsSource` 可改為接受您自己持有的集合（`ObservableCollection` 會被追蹤）。修改指標的屬性會就地更新區塊。

### 結構

- **標題區**：`Title` 在左，`Caption` 在右；沒有空間時，說明文字會換行到標題下方。
- **卡片**：標籤、最新值、帶變化和範圍（「30 days」）的箭頭，以及以圓點收尾的迷你趨勢線。寬度小於 760 時為兩欄，大於 760 時為四欄。滑鼠停留會使邊緣變深；Enter、Space 或按一下會開啟卡片。
- **展開的圖表**：指標分頁（附帶它們的值）、關閉按鈕、讀數卡、範圍開關、帶日期跨度的圖例、圖表，以及指標的描述。

### 數值格式、變化與色調

`ValueFormat` 選擇數值的寫法：`Count`（「5,874」）、`Currency`（「$12,480」）、`CurrencyCompact`（「$4.62M」、「$412.00K」）、`Percent`（以百分比為單位的值，「27.3%」）或 `Ratio`（「1.12×」）。`ValueFormatter` 可以取而代之，`AxisFormatter` 接收刻度的值和刻度之間的步長，用於 y 標籤。y 座標軸始終有五個刻度，並在 1、1.5、2、2.5、3、4 和 5 乘以 10 的冪中選擇能容納兩條線且留有 3% 餘量的最細一檔；`MaxAxisDecimals` 限制步長最多可有幾位小數（精簡金額為兩位，百分比為一位，計數為零位）。

`ChangeMode` 為 `Relative` 時是常見的「+1.6%」，為 `Points` 時用於比率（「+1.2 pts」）。箭頭表示方向，顏色表示這是否是好事：設定 `IsHigherBetter="False"` 後，上升顯示為紅色，下降顯示為綠色，因此成本下降會以向下的箭頭讀作好消息。

<Demo name="Finance" />

### 範圍

範圍是一個天數視窗：預設為 30、90 和 364（按鈕「30D」、「90D」和「12M」）。可用 `Ranges` 或 `RangesSource` 建立自己的範圍；每個 `ExplorerRange` 有 `Label`、`Days`、卡片顯示的 `CardText`（「30 days」）以及讀數卡顯示的 `CompareText`（「vs 30 days ago」）。`SparklineStep` 是迷你趨勢線相鄰點之間的天數：按日為 1，長範圍為 7，因此一年保持在 53 個點。`SelectedRangeIndex` 是雙向的，卡片會跟隨它，所以您留著開啟的那張圖表決定了收起的卡片上顯示什麼。

<Demo name="Ranges" />

`ShowPreviousPeriod` 會隱藏虛線以及檢視時的第二個圓點。

### 檢視某一天

在繪圖區上移動、按下並拖曳，或觸控它：最近的一天會出現十字線、每條線上的一個圓點，以及座標軸上的日期膠囊。讀數卡裡的數字、變化和對比會切換為那一天與一個週期之前對應日期的比較（「Sep 6 vs Aug 7」），變化的數字會滾動。離開繪圖區會清除檢視；手指抬起也會。

`ScrubIndex` 是被檢視的那一天（0 是範圍的第一天），`ScrubChanged` 回報日期、值和上一週期的值。按一下會讓圖表取得焦點；使用鍵盤時，Tab 到圖表上會顯示最後一天，方向鍵則逐天移動。

<Demo name="Events" />

### 窄容器

區塊讀取的是自身寬度。小於 760 時卡片占兩欄。小於 640 時，展開的圖表把內距收緊到 16，去掉分頁中的值，並把範圍開關放到讀數卡下方。在 440 及以下，卡片的標籤變為兩行、數字更小，分頁共占一整列，關閉按鈕移到下方、緊鄰讀數卡，x 座標軸選用更稀疏的刻度檔位。

<Demo name="Compact" />

### 文字與螢幕閱讀器文字

每一個可見和可朗讀的詞都是帶英文預設值的屬性：圖例（`ThisPeriodText`、`PreviousPeriodText`）、關閉按鈕、分頁和範圍的標籤、卡片名稱（`CardNameFormat`）、播報（`AnnouncementFormat`、`ClosedAnnouncementFormat`）、檢視時的對比（`ScrubCompareFormat`）以及表示方向的詞（`UpText`、`DownText`、`FlatText`）。

## API 參考

<!-- api: MetricExplorer -->

<!-- api: ExplorerMetric -->

<!-- api: ExplorerRange -->

<!-- api: ExplorerValueFormat -->

<!-- api: ExplorerChangeMode -->

<!-- api: MetricExplorerMetricEventArgs -->

<!-- api: MetricExplorerRangeEventArgs -->

<!-- api: MetricExplorerScrubEventArgs -->

## 無障礙

- 每張卡片是一個按鈕，以其內容命名：「Annual run-rate: $4.62M, up 1.6% over 30 days. Open chart」。
- 分頁列名為「Metric」，範圍開關名為「Chart range」；兩者都是一個 Tab 停靠點，並跟隨所選項目。
- 圖表是一個滑桿，名為「Scrub」加上指標名，其值文字是被檢視的那一天及其上一週期的對應日（「Sep 6, 2026: $4.55M. Aug 7, 2026: $4.43M」）。取得焦點時會圍繞繪圖區繪製一個環。裝飾性部分（迷你趨勢線、座標軸、圖例）對輔助技術隱藏。
- 在展開的圖表中切換指標或範圍時，會以禮貌方式播報最新值和變化；關閉圖表時會播報顯示了多少個指標。檢視某一天不會播報，因為滑桿已經讀出了它。
- 方向除顏色外始終有文字（「up」、「down」、「unchanged」）。
- 顏色來自淺色和深色下的主題權杖；折線、圓點和膠囊在兩種主題下都保持對比度。

### 鍵盤

- **Tab** 依序經過各張卡片，然後（在展開時）經過所選分頁、關閉按鈕、所選範圍和圖表。
- 卡片上的 **Enter / Space** 開啟它，並將焦點移到它的分頁。
- 分頁和範圍開關上的 **← →** 立即選取上一項或下一項並循環；**Home / End** 跳到兩端。
- 圖表上的 **← ↓** 和 **→ ↑** 步進到前一天或後一天，並停在兩端；**Home / End** 跳到第一天和最後一天。
- **Escape** 首先清除檢視；再按一次（或在圖表的任何其他控制項上按）會關閉圖表，並把焦點返回到它的卡片。

## 動態效果

- **卡片到圖表**：卡片的表面在一條平滑彈簧上（約半秒）移動並長成圖表的矩形，其他卡片則立即退去；圖表的內容帶著輕微上浮和模糊淡入，從飛行開始後 120 ms 起。關閉時反向執行：表面縮回它的卡片，其他表面淡入回來，卡片內容再次升起（開啟過的那張最後）。區塊的高度立即變化。
- **範圍開關**：一個罩在按日資料上的鏡頭。一條彈簧移動視野內的天數，每一格畫面都為該視窗重建折線，因此點數連續變化，而今天始終固定在右邊緣；y 範圍跟隨同一條彈簧，x 標籤隨之滑動。標籤檔位（每週、每兩週、每月）進行交叉淡入：舊檔位在 160 ms 內離開，新檔位在 240 ms 內到來。
- **指標切換**：折線上的每個點都從目前位置彈向新位置，曲線因此從一個指標流向下一個；中途再次切換會從螢幕上的形狀開始。y 標籤從底部起逐個交叉淡入，間隔 35 ms。讀數卡的各行上升並淡入；變化的數字像里程表一樣滾動，箭頭隨之轉向。
- **分頁和範圍開關**：高亮在同一條彈簧上滑動。
- **檢視**出現、移動和消失都沒有動畫。
- 減少動態效果時，表面、折線和高亮會立即落位，只保留 120 ms 的淡入淡出；關閉則立即套用所有變化。

## 相關

- `Metrics dashboard`、`KPI drilldown` 和 `Revenue explorer`，其他圍繞 KPI 的 Analytics 區塊。
- `SegmentedControl`，此區塊使用的範圍開關。
- `Cohort retention`，另一個在同一資料集的不同檢視之間變形的區塊。
