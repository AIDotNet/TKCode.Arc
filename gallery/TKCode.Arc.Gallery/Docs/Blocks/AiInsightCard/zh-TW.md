---
title: AI insight card
summary: 一張儀表板卡片，由助手說明某個指標為什麼變化：變化幅度、標出相關時段的圖表、可以選中的可能原因，以及一條建議的下一步。
source-hash: d3a4c23533f3
---

<Demo name="Preview" />

## 何時使用

`AiInsightCard` 放在儀表板上，緊挨著它所說明的數字。當某個指標突然上升或下降時，人們會問：什麼變了、為什麼、現在該做什麼。卡片給出指標的當前值和變化，給出說明所指的圖表並用色帶標出變化的時段，附上一段簡短的文字總結，按各自能解釋多少變化列出可能原因，最後給出一條建議的下一步和一個按鈕。選中某個原因時，色帶會移到這個原因對應的日期，文字和圖表始終指向同一件事。

- **儀表板上的異常。** 活躍使用者下降、成本飆升、轉化率下滑：卡片把告警變成可以據此行動的說明。
- **每週回顧。** 每個發生變化的指標一張卡片，由分析任務寫好，在會上閱讀。
- **有人把關的監控。** 建議的按鈕會啟動修復（復原、建工作單），由人來決定，由系統去執行。

## 何時不用

- **單純的數字。** 沒有需要說明的內容時，使用 `MetricCard` 或單獨的圖表。
- **長篇報告。** 卡片只容得下兩三句話和幾個原因。更多內容請連結到完整報告。
- **圍繞資料的實時對話。** 需要提問和追問時，使用 `AI chat` 或 `AI side panel` 區塊。

## 用法

本區塊派生自 Avalonia 的 `TemplatedControl`。不設定任何資料時顯示一份虛構的示例（`AiInsightCardSamples`）；設定屬性即可顯示你自己的資料。

```xml
<arc:AiInsightCard Title="Weekly active users"
                   Value="18,420"
                   Change="-12.4"
                   PeriodText="vs. the 14 days before"
                   Summary="Active users fell 12.4% over the last five days, almost all of it on Android."
                   NextStep="Roll Android back to 4.1.3 for magic-link sign-ins." />
```

```csharp
card.Points = values;               // oldest first
card.PointLabels = days;            // "Nov 4", one per point
card.ComparisonPoints = lastPeriod; // dashed line, or null
card.FocusRange = new AiInsightRange(9, 13);
card.Causes =
[
    new() { Title = "Android 4.2 release", Detail = "Sign-ins on Android fell 31%.", Share = 0.64, Range = new(9, 13) },
    new() { Title = "Regional holiday", Share = 0.09, Range = new(9, 10) },
];
card.ActionInvoked += (_, e) => rollouts.Rollback("android", e.NextStep);
```

### 結構

- **小標籤與點讚**：一個小小的 "AI insight" 標簽，以及讚和踩（`Feedback`，雙向；再按一次同一個按鈕會撤回回答）。
- **指標**：`Title`、`Value`、根據 `Change` 生成的變化標簽和 `PeriodText`。`IsIncreaseGood` 決定色調：預設上升為綠色；用於成本和錯誤率時設為 false。
- **讀數與圖表**：`Points` 畫成的折線和下方的淺色面積、虛線表示的 `ComparisonPoints`，以及覆蓋 `FocusRange`（或選中原因的 `Range`）的色帶，色帶內的折線使用變化的顏色。上方的讀數顯示所選的點，沒有選中時顯示最後一個點。
- **總結、原因、下一步**：`Summary`；`Causes`，每個原因帶占比條和百分比；`NextStep` 和按鈕（`IsActionVisible`、`ActionCommand`、`ActionInvoked`）。
- **腳注**：`Footnote`，說明資料來源和說明寫成的時間。

### 另一個指標

<Demo name="CostRise" />

### 正在生成時

在任務撰寫說明期間設定 `IsAnalyzing`：文字會換成一行閃光的提示和若干預留位置行，圖表保持不變。它再次變為 false 時，會朗讀總結。

<Demo name="Analyzing" />

### 文字

所有文字都在 `Labels`（`AiInsightCardLabels`）中：各個標題、按鈕、生成中的提示、讀屏名稱，以及變化、占比和讀數的格式。`ValueFormatter` 用於格式化讀數裡的數值。

## API 參考

<!-- api: AiInsightCard -->

<!-- api: AiInsightCause -->

<!-- api: AiInsightRange -->

<!-- api: AiInsightCardLabels -->

## 無障礙

- 卡片是一個有名稱的區域；指標名稱是二級標題，各部分的標題是三級標題。
- 圖表是一個 Tab 停靠點，作為以指標命名的影像讀出，說明文字是第一個和最後一個值。**Left** 和 **Right** 選擇資料點，**Home** 和 **End** 跳到兩端，**Escape** 回到最後一個點；用鍵盤選中的每個點都會讀出（"Nov 4: 17,980, before 20,110"）。用指標停留時不會朗讀。
- 每個原因都是一個切換按鈕，以標題和占比命名（"Android 4.2 release, 64% of the change"），並以證據作為說明。**Up** 和 **Down** 在原因之間移動，**Space** 或 **Enter** 選中。
- 讚和踩分別命名為 "Helpful" 和 "Not helpful"，並報告 "selected"；回答後會禮貌地確認。
- 生成中的提示和完成後的總結都會禮貌地朗讀。

## 動效

- 卡片第一次出現時，折線從左向右畫出（480 ms，標準緩動）。
- 選中原因時，色帶以 snappy 彈簧滑到對應時段，色帶內的顏色隨之移動。
- 行和點讚按鈕的顏色在 160 ms 內過渡；變化標簽的色調交叉淡化。
- 減弱動效時整條折線立即顯示，色帶不經彈簧直接移動。關閉動效時所有變化立即生效。

## 響應式

- 卡片最寬 640，在更窄的容器中占滿寬度。寬度不超過 420 時（`:compact`）內邊距從 20 收緊到 16。
- 數值、變化標簽和對比時段放不下時換到第二行。
- 圖表保持高度並按新寬度重繪；占比條保持尺寸，標題以省略號截斷。
- 沒有任何功能依賴停留：輕點原因即可選中，輕點圖表即可選擇資料點。

## 相關元件

- `MetricCard` 和 `LineChart`，用於不需要說明的數字和圖表。
- `AI answer`，用於對一個問題給出更長、帶引用的回答。
- `Metrics dashboard` 和 `KPI drilldown`，這張卡片所在的分析區塊。
