---
title: Usage forecast
summary: 對照預算檢視一個計費週期：已量測的用量和帶可能範圍的虛線預測，一條可拖曳或用按鍵步進的預算線，標題、磚塊、紅色超額部分和提醒標籤都會隨之變化，還有一個讓所有路徑變形的指標開關。
source-hash: 64c726ac820f
---

<Demo name="Preview" />

## 何時使用

`UsageForecast` 回答「本週期我們會不會控制在預算之內，什麼時候會超出？」。它從左到右繪製一個計費週期的每一天：截至今天已量測的內容是實線折線，預測是虛線折線，預測周圍有一個越往後看越寬的預測錐。一條水平線是預算。預測與它相交的位置，會有一個標記指出日期，而預算線之上的每一段路徑都會變紅。

- **帳單和方案頁面。** 在一個檢視中呈現用量、預測和預計超額，帳戶擁有者可以移動預算，檢視不同的上限意味著什麼。
- **提醒和上限。** 50%、80% 和 100% 標籤說明各個閾值已在何時達到或將在何時達到，並讓使用者選擇要接收哪些通知。
- **多個計量，一個故事。** 呼叫次數、儲存空間和席位共用同一個區塊：開關會把同一張圖表變形為另一個計量，帶有它自己的座標軸、預算範圍、單位和價格。

## 何時不用

- **沒有預測的花費。** 只想檢視單一數值的歷史時，請使用折線圖；本區塊的價值在於預測和預算。
- **同時檢視許多指標。** 本區塊一次只顯示一個計量。需要比較多個計量時，請使用 `Metrics dashboard` 或 `Small multiples`。
- **長達數百天的週期。** 每天繪製一個頂點，日期標籤相隔一週；本區塊是為大約一個月的週期而設計。

## 用法

為區塊提供週期（`PeriodStart`、`PeriodDays`），並為每個計量提供一個 `UsageMetric`。一個指標包含第 1 天到今天的 `Readings`，一個 `StartValue`（累計總量用 0；對於像正在使用的儲存空間這類水準值則省略），固定的數值座標軸，帶範圍和步長的預算，單位，以及超出預算部分每個單位的價格。除非 `TodayIndex` 另有指定，今天就是讀數的個數。當你沒有提供預測及其範圍時，會根據最近兩週計算得出。

```xml
<arc:UsageForecast Title="Usage outlook" Subtitle="Harborline Studio · Growth plan · Renews Jul 1" PeriodDays="30">
    <arc:UsageMetric Key="events" Label="Events" Readings="{Binding EventReadings}" StartValue="0"
                     AxisMaximum="40" BudgetMinimum="8" BudgetMaximum="40" BudgetStep="0.5" Budget="26"
                     Unit="M" UnitPrice="150" PriceNote="At $0.15 per 1k events" />
</arc:UsageForecast>
```

```csharp
forecast.PeriodStart = new DateOnly(2027, 6, 1);
forecast.Metrics.AddRange(metrics);
forecast.SelectedMetricKey = "database";
```

`MetricsSource` 接受由你自己持有的集合（會跟隨 `ObservableCollection` 的變化）。只有一個指標時開關會隱藏，`UnitPrice` 為 0 時成本磚塊會隱藏。`Budget` 在每個指標上都是雙向的，切換離開再切換回來時會保留其值。

### 結構

- **頁首**：`Title`、`Subtitle`，以及右側的指標開關。
- **標題句**：一句話有四種形式（已在某天超出預算、預測在某天達到預算、在最後一天達到預算、維持在預算之下），其下一行顯示剩餘時間和可能範圍。拖曳時它們會被改寫，只有變化的詞會播放動畫。
- **磚塊**：到目前為止的用量及其占預算的比例，最後一天的預測及其範圍，以及超額成本（大於零時為紅色，下方帶有價格說明）。
- **圖表**：固定座標軸上的六個數值刻度，相隔一週的日期，一條 "Today" 線，預測錐，預測線，已量測的線，提醒刻度，帶日期膠囊的交點標記，以及預算帶。
- **頁尾**：提醒標籤和圖例。

### 預算線

線周圍整個 32px 的帶狀區域都回應指標。拖曳是相對的（在偏離中心的位置按下不會讓線跳動），數值會貼齊到 `BudgetStep`，超出預算範圍後線仍會跟隨，但會被壓縮，放開後彈回。每一步都會重新計算標題句、磚塊、標籤和交點；`BudgetChanged` 在每一步都會觸發，此時 `IsCommitted` 為 false，並在放開或按鍵之後再觸發一次，此時 `IsCommitted` 為 true，這就是儲存的時機：

```csharp
forecast.BudgetChanged += (_, e) => { if (e.IsCommitted) Save(e.Metric.Key, e.NewValue); };
```

`BudgetCommittedCommand` 透過繫結完成同樣的事，新預算作為其參數。`SetBudget` 用同樣的彈簧從程式碼中移動這條線。

<Demo name="Events" />

### 提醒

`AlertThresholds` 保存這些標籤；當你沒有新增任何閾值時，區塊使用 50%（關閉）、80% 和 100%（開啟）。閾值的 `IsEnabled` 是雙向的，它唯讀的 `Status` 為 `AlreadyPast`、`Reached`、`Expected` 或 `NotExpected`，`Date` 是對應的日期。已啟用的閾值會在線到達預算該比例的位置繪製一個刻度，旁邊帶有它的百分比。`AlertThresholdToggled` 回報每次切換；區塊本身從不傳送任何內容。

### 結果

<Demo name="StaysUnder" />

<Demo name="AlreadyPassed" />

唯讀屬性把同樣的事實提供給頁面的其餘部分：`Outcome`、`CrossingDay`、`CrossingEarly`、`CrossingLate`、`ProjectedTotal`、`ProjectedLow`、`ProjectedHigh` 和 `OverageCost`。

### 檢視某一天

指向圖表，或讓它取得焦點並使用按鍵，即可得到十字準線和最近一天的卡片：它的日期、是量測的還是預測的、數值，以及占預算的比例或可能範圍。卡片位於十字準線右側，靠近邊緣時翻轉到左側。`InspectedDay` 是雙向的（null 表示沒有）。

### 窄容器

區塊讀取自己的寬度。小於 560 時內距收緊，開關移到標題下方，圖表高 232，磚塊收緊；小於 440 時磚塊改為堆疊，數值在右側，每個標籤占滿一列。標題句的字級隨寬度在 18 到 22 之間變化。

<Demo name="Flat" />

### 文字、格式和日期

`IsFramed` 為 false 時去掉卡片邊框。`DateFormat` 決定每個日期的寫法。指標的 `ValueFormatter`、`Unit`、`UnitSeparator` 和 `CurrencyFormatter` 控制數值的顯示方式；磚塊會根據 `ValueFormatter` 給出的小數位滾動數字，因此它應傳回純數字。每個句子、標籤和朗讀文字都是 `Labels` 的一個屬性。

<Demo name="Words" />

## API 參考

<!-- api: UsageForecast -->

<!-- api: UsageMetric -->

<!-- api: UsageAlertThreshold -->

<!-- api: UsageForecastLabels -->

<!-- api: UsageBudgetChangedEventArgs -->

<!-- api: UsageOutcome -->

<!-- api: UsageAlertStatus -->

## 無障礙

- 圖表是涵蓋各天的滑桿：它的名稱是一個完整的句子（指標、到目前為止的用量、預測、預算和標題句），它的值讀作 "Jun 21: 22.9M used, 88% of budget" 或 "Jun 27 projected: 29.4M, likely 28.2M to 31M"，它的說明文字說明如何使用按鍵。
- 預算帶是涵蓋預算的滑桿，數值以文字形式提供，提示為 "Drag the budget line or press the arrow keys."。
- 當預算確定、選擇了指標或切換了提醒時，標題句及其下一行會透過禮貌的即時區域朗讀；動畫中的詞本身對螢幕閱讀器隱藏。
- 提醒標籤是切換按鈕，以其百分比和狀態命名（"80% of budget, hit jun 19"）。
- 一個隱藏的表格列出每一天的讀數和範圍，預測的日期有標記。
- 超額部分會變紅，同時還會寫出 "Overage" 和金額；路徑中變紅的部分由標題句重複說明。

### 鍵盤

- **Tab** 依序造訪指標開關（方向鍵變更選擇）、圖表、預算線和每個標籤。
- 圖表：**← →**（或 **↓ ↑**）移動一天，**Page Up / Page Down** 移動一週，**Home / End** 到第一天和最後一天，**Escape** 隱藏卡片。讓圖表取得焦點會顯示今天。
- 預算線：**↑ →** 上調一步，**↓ ←** 下調一步，**Page Up / Page Down** 五步，**Home / End** 到最低和最高預算。
- 標籤：**Enter** 或 **Space** 切換。

## 動態效果

- **預算線**：按鍵之後沿用靈敏彈簧移動，拖曳時用較硬的彈簧跟隨；紅色裁剪區域隨之移動。超出範圍時線會被壓縮並彈回。
- **交點標記、提醒刻度**：它們沿折線追隨所顯示的預算與所顯示的線的交點；日期膠囊淡入淡出，經過時附近的日期標籤淡出。
- **提醒刻度**由下而上繪製（700 ms），並以同樣的方式擦除；它們的標籤淡入淡出。
- **指標開關**：已量測的線、預測線和預測錐的每個頂點同時彈向新指標，預算線和刻度隨之移動，數值標籤交叉淡入淡出，磚塊數字滾動，標題句只替換變化的詞（退出 160 ms，進入 240 ms，間隔 40 ms），標籤狀態逐字母變形。
- **標籤**：勾選標記在快速彈簧上開啟它的槽位，寬度隨之變化。
- **檢視卡片**：以較硬的彈簧跟隨十字準線，淡入 160 ms。
- 減少動態效果會讓每個彈簧立即到位，只保留短暫的淡入淡出（詞為 120 ms）。關閉則立即套用所有變化。

## 相關

- `Metrics dashboard`、`KPI drilldown`、`Cohort retention`，即其他 Analytics 區塊。
- `SegmentedControl`，本區塊使用的開關。
