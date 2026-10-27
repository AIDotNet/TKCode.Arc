---
title: Metrics dashboard
summary: 單螢幕分析概覽：附範圍開關、會數到總數的指標分頁、可檢視並可疊加上一週期的面積圖，以及兩個排名清單。
source-hash: 6b2b0ac2d55e
---

<Demo name="Preview" />

## 何時使用

`MetricsDashboard` 在一個精簡的介面中回答「過去 N 天我們表現如何？」。頂部標題說明這些數字的主題，並提供 7D / 30D / 90D 開關和 Export 按鈕。其下每個指標是一個分頁，顯示該範圍內的總數以及與上一週期相比的變化；所選分頁驅動圖表。區塊的末尾是兩個排名清單，用於熱門頁面和熱門來源，或任何其他兩組明細。

- **產品與行銷概覽。** 工作階段數、註冊數、轉換率，以及流失率或跳出率，並附上帶來流量的頁面和參照來源。
- **團隊或客戶的使用情況彙總。** 活躍團隊、新專案、席位使用率和失敗工作，以及最繁忙的工作區和它們所用的方案。
- **深入報表之前的登陸檢視。** 使用 `PointInspected`、`SelectedMetricChanged` 和 `RangeChanged`，為某人正在檢視的日期、指標或範圍開啟詳細頁面。

## 何時不用

- **一張圖表上有多個數列。** 圖表只繪製一個指標，並可視需要繪製它的上一週期。需要並排比較多個數列時請使用折線圖，數列很多時請使用 `Small multiples`。
- **即時的、按秒變化的資料。** 每個點都是一天或一週。即時營運資料請使用串流圖表。
- **任意日期範圍。** 範圍是以 `EndDate` 結尾的固定長度。當使用者需要選擇任意起訖日期時，請使用日期範圍選擇器和您自己的圖表。
- **超過六個指標。** 四個恰好填滿一列，中等版面會把它們排成兩列兩欄。更長的指標集請拆分到多個儀表板，或使用表格。

## 用法

此區塊衍生自 Avalonia 的 `TemplatedControl`，不含範例資料。每個指標是一個 `MetricsDashboardMetric`，`Daily` 中每天一個值，最後一個值對應 `EndDate`（未設定時為今天）。資料量至少應為最長範圍的兩倍，這樣每個範圍都有上一週期可供比較；沒有上一週期時，變化行會被隱藏。清單列是 `MetricsDashboardItem`，`Values` 中每個範圍一個值，最大的在前。

```xml
<arc:MetricsDashboard x:Name="Dashboard"
                      Title="Insights"
                      Source="northwind.dev"
                      PrimaryListTitle="Top paths"
                      SecondaryListTitle="Referrers"
                      ListValueHeader="Sessions" />
```

```csharp
Dashboard.EndDate = new DateOnly(2026, 10, 18);
Dashboard.Metrics.Add(new MetricsDashboardMetric { Label = "Sessions", Daily = sessions });
Dashboard.Metrics.Add(new MetricsDashboardMetric
{
    Label = "Churn risk", Kind = MetricsDashboardMetricKind.Ratio, HigherIsBetter = false, Daily = churn
});
Dashboard.PrimaryItems.Add(new MetricsDashboardItem { Label = "/", Values = [2602, 10840, 29268] });
```

`MetricsSource`、`PrimaryItemsSource` 和 `SecondaryItemsSource` 接受您自己持有的集合；`ObservableCollection` 會被追蹤，修改指標或項目的屬性會更新區塊，並讓數字數到新的值。

### 計數與比率

`Count` 指標會累加：一個範圍或一週的總數是其各天之和，變化為相對值（「11.7%」）。`Ratio` 指標是以百分比為單位的比率：變化為百分點之差（「0.1 pts」）。提供 `Numerators` 和 `Denominators`（轉換數與工作階段數，已用席位與已付費席位）後，每一週和每個範圍都是各項總和的精確比率；不提供時則為每日比率的平均值。`HigherIsBetter="False"` 會讓下降顯示為綠色，適用於流失率、跳出率或錯誤率。箭頭始終表示方向，螢幕閱讀器會用文字讀出。

### 範圍與週

`Ranges`（7、30、90）是開關的各個區段，以 `RangeLabelFormat`（「{0}D」）書寫，並以 `RangeNameFormat`（「Last {0} days」）為螢幕閱讀器命名；`SelectedRange` 是雙向的。長於 `WeeklyThreshold`（60）的範圍按週繪製，週從範圍的第一天開始計算：90 天得到 13 個點，最後一週只有剩下的 6 天。它的讀數卡會這樣說明（「Week of Oct 13 (6 days)」），因為不足一週的部分按原樣加總，讀數會比其他週低。

### 窄容器

區塊讀取的是自身寬度，而不是視窗寬度。小於 640 時分頁排成兩列兩欄，清單上下堆疊；小於 460 時範圍開關撐滿標題列，數值縮小到 18px，繪圖區高 168，x 座標軸最多顯示四個日期。當標題和操作無法放在同一行時，操作會移到標題下方。

<Demo name="Narrow" />

### 其他資料，其他文字

每一段文字都是帶英文預設值的屬性：`SubtitleFormat` 和 `SubtitleRangeFormat`、`CompareText`、`PreviousLegendText`、`PreviousValueFormat`（「{0} previous」）、`DateFormat`、`WeekTitleFormat`、`PartialWeekTitleFormat`、`ExportText`、`ExportedText`、各清單標題和 `ListValueHeader`，以及螢幕閱讀器文字 `RangeGroupName`、`MetricGroupName`、`ChartNameFormat`、`ChartHelpText` 和 `ChangeContextText`。指標可以用 `ValueFormatter`（分頁和讀數卡）、`AxisFormatter`（y 座標軸）和 `DeltaFormatter`（變化）格式化自己的數字；`ListValueFormatter` 對清單發揮同樣的作用。此示範使用 14、28 和 84 天的範圍、精確的席位使用率比率，以及保留兩位小數的失敗工作率。

<Demo name="ProductUsage" />

### 狀態與事件

`SelectedMetricIndex`、`SelectedRange` 和 `IsCompareEnabled` 是雙向的，因此檢視可以被儲存和還原，或由其他地方驅動。`RangeChanged`、`SelectedMetricChanged` 和 `CompareChanged` 回報每一次變化；每當檢視移動時，`PointInspected` 會回報那一天（或那一週）、它的值，並在開啟 Compare 時回報上一週期的值。`InspectPoint(index)` 和 `ClearInspection()` 在程式碼中完成指標和方向鍵所做的事；`ActiveIndex` 表示正在檢視哪個點。

<Demo name="Events" />

### 匯出

按鈕本身不會匯出任何內容。`ExportCommand` 會帶著一個 `MetricsDashboardExportRequest`（以天為單位的範圍、它的第一天和最後一天、所選指標，以及是否顯示上一週期）執行；當它無法執行時，什麼都不會發生。隨後會觸發 `ExportRequested`，按鈕顯示一個勾號和 `ExportedText` 作為確認，持續 `ExportConfirmDuration`（1.8 秒）。`Export()` 在程式碼中完成同樣的事；`IsExportVisible` 可隱藏該按鈕。

<Demo name="Export" />

## API 參考

<!-- api: MetricsDashboard -->

<!-- api: MetricsDashboardMetric -->

<!-- api: MetricsDashboardItem -->

<!-- api: MetricsDashboardExportRequest -->

<!-- api: MetricsDashboardPointEventArgs -->

## 無障礙

### 鍵盤

- **Tab** 依序經過範圍開關、Export、指標分頁、Compare 和圖表。範圍開關和分頁各算一個停靠點，停在所選項目上。
- 在範圍開關中，**Left** / **Right**（以及 **Up** / **Down**）移動並選取，到兩端會循環；**Home** 和 **End** 跳到第一個和最後一個範圍。
- 在指標分頁中，**Left** / **Right** 移動並選取，會循環；**Home** 和 **End** 選取第一個和最後一個指標。
- **Space** 切換 Compare。**Enter** 或 **Space** 按下 Export。
- 在圖表上，僅有焦點不會顯示任何內容。**Right** 顯示最後一個點，**Left** 顯示它前面的一個；之後 **Left** / **Right** 逐點移動並停在兩端，**Home** 和 **End** 跳到第一個和最後一個點，**Escape** 結束檢視。把焦點移開或把指標移出繪圖區也會結束檢視。

### 螢幕閱讀器與焦點

- 區塊以其標題命名，標題是第 2 級標題；清單標題是第 3 級標題，並為各自的區段命名。
- 範圍開關是一個由 `RangeGroupName` 命名的群組；它的區段是名為「Last 7 days」等的切換按鈕。
- 指標分頁是由 `MetricGroupName` 命名的群組中的分頁；每個都會讀出它的標籤、總數和用文字表達的變化，例如「Sessions, 36,686, up 11.7% versus the previous period」，因此含義不依賴顏色。
- 圖表是一幅名為「Sessions, last 30 days」的影像，以 `ChartHelpText` 作為說明文字。被檢視的點（「Sep 21, 1,370, 1,111 previous」）在變化時（無論來自鍵盤還是指標）會以禮貌方式播報。圖例、座標軸、格線和讀數卡本身不會被單獨朗讀。
- 每個清單列讀作「名稱，數值」；過長的名稱以省略號截斷，並以讀數卡顯示完整內容。
- Export 的確認會被播報；按鈕的名稱隨其文字變化。
- 焦點環只在鍵盤焦點時繪製：圍繞範圍區段和 Export，位於分頁邊緣內側（因此相鄰項目或區塊邊緣永遠不會遮住它），圍繞 14px 的 Compare 核取方塊，以及圍繞繪圖區。

## 動態效果

- **範圍變化**：開關的滑塊在一條快速的臨界阻尼彈簧上滑動（約 330 ms）；每個分頁的總數和清單數值同時在 480 ms（.22, 1, .36, 1）內從舊數字數到新數字；圖表在 240 ms 內從舊畫面交叉淡入到新畫面。座標軸標籤、日期、副標題和變化行則立即切換。
- **指標變化**：底線在一條平滑彈簧上滑向新的分頁（約 530 ms），在 2 × 2 版面中可跨列；圖表在 240 ms 內交叉淡入。
- **Compare**：虛線的上一週期在平滑彈簧上淡入淡出（約 700 ms）；核取方塊的顏色在 160 ms 內變化。
- **檢視**：指示線、圓點和讀數卡立即出現、移動和消失。讀數卡位於繪圖區中遠離該點的那一半，絕不會蓋住它。
- **Export**：文字交叉淡入，圖示以淡入加從 .8 起縮放的方式替換，耗時 160 ms；滑鼠停留會在 160 ms 內為按鈕和分頁著色，按下 Export 會將它縮放到 .97。
- 區塊首次出現時沒有任何動畫。減少動態效果會讓滑塊和底線直接跳轉，立即替換數字和圖表，並去掉顏色轉場；關閉則立即套用每一處變化。

## 相關

- `KPI drilldown`、`Metric explorer` 和 `Revenue explorer`，其他帶有指標卡片和圖表的 Analytics 區塊。
- `Small multiples`，一眼查看多個數列。
- `SegmentedControl`，用作獨立的範圍開關。
