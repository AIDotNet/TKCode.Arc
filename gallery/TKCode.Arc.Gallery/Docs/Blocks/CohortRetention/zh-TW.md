---
title: Cohort retention
summary: 每個起始群體中有多少人留了下來，逐月呈現：熱力圖三角形可變形為留存曲線，頭條數字比較最新與最早的世代，分段會重繪每個儲存格，並有同時適用於指標與鍵盤的儲存格游標。
source-hash: 97b2545d3dc0
---

<Demo name="Preview" />

## 何時使用

`CohortRetention` 回答一個問題：在某個週期開始的人（帳戶、工作區、安裝）中，一個月、兩個月、三個月後還有多少仍然活躍，並且情況是否在變好？每一列是一個世代，從最早的開始，每一欄是自世代開始後的一個月。較新的世代被測量的月數較少，因此熱力圖是一個三角形；其下依規模加權的平均列顯示一個典型世代的樣子。

- **成長與客戶成功儀表板。** 頂部的第 1 個月與第 6 個月留存，以及最新三個與最早三個世代之間的變化，一眼就能看出新手引導工作是否見效。
- **比較分段。** 把方案、管道或地區作為分段：切換時三角形以波浪形重繪，每個數字滾動，因此兩組之間的差異在變化的過程中就能看到。
- **形狀與水準兼顧。** 熱力圖顯示每個儲存格的水準；曲線顯示各世代在哪裡趨於平緩，以及哪些世代比平均值下降得更快。

## 何時不用

- **單一留存數字。** 沒有可比較的世代時，請使用統計數字或折線圖。
- **漏斗與轉換步驟。** 人們依序經過的步驟屬於漏斗或 `Journey flow` 區塊，而不是自開始以來的月數。
- **數百個世代。** 此區塊為大約 6 到 20 個世代及同樣多的週期而設計；在顯示更多之前請進一步彙總（用月代替週）。
- **需要精確查詢的每日數據。** 有幾十欄的留存表在 `DataGrid` 中讀起來更好。

## 用法

此區塊衍生自 Avalonia 的 `TemplatedControl`。它的資料是一組 `RetentionSegment`，每個分段包含其 `RetentionCohort`，從最早開始。一個世代有 `Label`（"Jun 2026"）、供窄版版面使用的可選 `ShortLabel`（"Jun ’26"）、`Size`（有多少人開始）以及 `Retained`，即第 1、2、3 個月……仍然活躍的人數。`Retained` 中值的個數就是目前已測量的月數。每個留存率、平均列與頭條數字都由區塊計算。

```xml
<arc:CohortRetention Title="Space retention"
                     Subtitle="Team spaces still editing each month after creation."
                     FirstMetricLabel="First-month retention"
                     SecondMetricLabel="Half-year retention"
                     TotalLabel="Spaces created">
    <arc:RetentionSegment Label="All" Description="all spaces">
        <arc:RetentionCohort Label="Jun 2026" ShortLabel="Jun ’26" Size="412" Retained="{Binding June}" />
        <arc:RetentionCohort Label="Jul 2026" ShortLabel="Jul ’26" Size="398" Retained="{Binding July}" />
    </arc:RetentionSegment>
</arc:CohortRetention>
```

```csharp
retention.Segments.Add(new RetentionSegment("Starter", starterCohorts, "Starter-plan spaces") { Key = "starter" });
retention.SelectSegment("starter");
```

`SegmentsSource` 則接受由你持有的集合（`ObservableCollection` 會被追蹤）。只有一個分段時，分段開關會被隱藏。修改所顯示分段中的某個世代（例如它的 `Retained`）會就地更新區塊。

### 結構

- **標題區**：`Title`、`Subtitle`，右側是分段開關。
- **頭條數字**：`FirstMetricMonth`（1）與 `SecondMetricMonth`（6）依規模加權的平均留存，各自帶著已達到該月份的最新與最早 `CompareCohortCount`（3）個世代之間以百分點計的變化；以及開始的總數，附第一個與最後一個世代的名稱。數值逐位滾動。
- **工具列**：圖例（六個熱力等級及其閾值，或兩種線條樣式）與檢視開關。
- **圖表**：左側是世代名稱與規模，然後是儲存格、平均列與月份座標軸。

### 熱力圖與曲線

`View` 是 `Heatmap`（預設）或 `Curves`，雙向。在熱力圖中，每個儲存格顯示四捨五入後的百分比，以及混入表面色的強調色等級：從最低到最高的等級依次是 8%、18%、32%、48%、66% 與 86%，各等級的起點是留存 20%、35%、50%、65% 與 80%。等級使用精確的留存率，因此 79.8% 讀作 "80"，但仍屬於第五級。用 `Thresholds` 變更閾值，用 `RampMix` 變更混合比例（比閾值多一個值）。混合比例達到 50% 或更高的等級用強調色的前景色繪製數字。

在曲線檢視中，y 軸從 0 到 100%；每個世代是一條細的直線段折線，平均值是疊在最上面的粗強調色線，每個月一個點。

<Demo name="Curves" />

### 檢視儲存格

指向一個儲存格（或曲線檢視中的一個點）即可檢視它：一個方框標出該儲存格，圖表中其所在列與欄之外的部分淡出，該列的名稱與月份編號變為加重，儲存格旁邊的一張卡片給出精確的留存率、開始的人數中仍然活躍的人數，以及與該月平均值的差異。在平均列上，卡片會說明平均值涵蓋了多少個世代。在曲線檢視中，最近的月份由 x 決定，該月最近的點由 y 決定，一條十字線延伸到 y 軸並在標籤中給出精確值，該世代的線條變深，其他線條淡出。

`CursorCell` 保存被檢視的 `CohortCell`（雙向，無則為 null），`CursorChanged` 在每次變化時回報世代、留存率與人數。點擊，或按 Enter 或 Space，會引發 `CellInvoked` 並執行 `CellCommand`：用它來開啟該世代的人員或詳細頁。

<Demo name="Events" />

### 週、其他週期、其他閾值

區塊中沒有任何東西與月綁定：欄數跟隨最長的世代（或 `Months`），每一個提到週期的文字都是屬性（`MonthFormat`、`MetricLabelFormat`、`AxisTitle`、`AxisTitleShort`、播報格式）。

```csharp
retention.Thresholds = [0.10, 0.25, 0.40, 0.55, 0.70];
retention.MonthFormat = "Week {0}";
retention.MetricLabelFormat = "Week {0} retention";
```

<Demo name="Weekly" />

### 窄版容器

此區塊讀取自身的寬度，而不是視窗的寬度。小於 600 時圖表切換為精簡幾何：世代名稱縮短，沒有規模欄，列高 24px，座標軸標題為 "Month"；當某一欄窄於 32 時，數字會離開儲存格。小於 560 時內距收緊到 16，分段開關移到標題下方。小於 440 時頭條數字上下堆疊，每列一個且數值靠右，檢視開關移到圖例上方。頭條數值隨區塊寬度在 22 到 28 之間縮放。

<Demo name="Compact" />

### 格式與文字

`PercentFormatter`（0 到 1 的留存率）、`CountFormatter` 與 `DeltaFormatter`（百分點）取代預設的 "48.5%"、"5,199" 與 "+5.2 pts"。每一個可見的文字都是帶有英文預設值的屬性：頭條標籤、圖例文字、卡片的列標籤（`StillActiveText`、`VsAverageText`、`WeightedOverText`）、開關按鈕及其名稱、空白文字與播報。`ShowSizeColumn` 與 `ShowAverageRow` 會隱藏規模欄與平均列（以及平均線）。

## API 參考

<!-- api: CohortRetention -->

<!-- api: RetentionSegment -->

<!-- api: RetentionCohort -->

<!-- api: CohortCell -->

<!-- api: CohortCellEventArgs -->

<!-- api: CohortRetentionView -->

## 無障礙

- 圖表是一個可聚焦的群組，其名稱是一段摘要（檢視、分段、世代的範圍與兩個頭條數值），並以按鍵提示作為說明文字。鍵盤焦點環繪製在它的周圍。
- 每次游標移動都會以禮貌方式播報："Jun 2026, month 3: 56.2% still active, 236 of 420."。平均列會說明它涵蓋了多少個世代。Escape 會清空播報。
- 檢視變化會播報檢視所顯示的內容；分段變化會播報分段與兩個頭條數值。
- 一張隱藏的表格列出每個世代及其規模與每個月的留存率（或 "not measured yet"），平均列在最後，因此螢幕閱讀器無需圖表就能讀出這些數字。
- 每個頭條數字作為一個短語朗讀，並用文字說出其變化（"up 5.2 pts newest 3 vs oldest 3 cohorts"），而不僅靠顏色。圖例以其閾值命名。
- 顏色在兩種主題下都來自強調色與表面色；在深色等級上，數字切換為強調色的前景色。

### 鍵盤

- **Tab** 依序移到分段開關、檢視開關與圖表，各占一個停靠點。Tab 進入圖表時，游標落在最早世代的第一個月。
- **← →** 沿世代移動游標，並停在其第一個與最後一個已測量的月份。
- **↑ ↓** 在世代之間移動並保持月份，若該世代尚未到達該月，則取該世代最後一個已測量的月份。從最新世代按 **↓** 會到達平均列。
- **Home / End** 跳到世代的第一個與最後一個已測量的月份。
- **Escape** 清除游標；焦點保留在圖表上。
- **Enter / Space** 為游標所在的儲存格引發 `CellInvoked`。
- 在兩個開關中，**← →** 立即變更選取並循環；**Home / End** 跳到兩端。

## 動態效果

- **熱力圖到曲線**：一條主彈簧驅動變形；每個部分取其中一段，沿對角線從左上角開始依次更晚。數字淡出，儲存格縮成點，點從它們的列移動到它們的留存率位置並拉開線條，列標籤淡出，然後線條與 y 格線淡入，全部在約一秒內完成。返回時以相反順序播放相同的片段：最後的儲存格最先移動，每個儲存格先移動再增大。
- **分段開關**：每個值都以彈簧變化到新的留存率，沿對角線每一步晚 9 ms 開始；每當一個值越過閾值，儲存格就在 160 ms 內重繪，因此攀升三個等級的儲存格會重繪三次，其數字在中途變為淺色或深色。頭條數字滾動；變化文字向上滑出再滑入。
- **游標**：方框、環與十字線跳到新的儲存格；其他儲存格在 160 ms 內淡出；卡片在出現的位置淡入（120 ms），然後在一條快速彈簧上滑向每個新儲存格，並在 160 ms 內淡出。
- **圖例**：舊圖例上升 6px 並淡出，新圖例上升到位，240 ms。
- 減少動態效果會讓變形、數值與顏色立即到位，只保留短暫的淡入淡出（卡片、圖例、變化文字）。關閉會立即套用一切。

## 相關元件

- `Metrics dashboard`、`KPI drilldown` 與 `Journey flow`，即其他 Analytics 區塊。
- `SegmentedControl`，此區塊所用的開關。
- `Activity terrain`，另一個在兩種檢視之間變形的熱力圖。
