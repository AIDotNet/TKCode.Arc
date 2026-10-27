---
title: Budget variance
summary: 依部門比較預算與實際支出：共用刻度的子彈圖、帶正負號的差異、讓每根條一起彈簧的期間切換、就地展開的預算科目，以及合計列。
source-hash: 87ccea4ca2f3
---

<Demo name="Preview" />

## 何時使用

`BudgetVariance` 一眼回答一個問題：哪些團隊花得比計畫多，哪些花得少，差了多少。每一列都是同一刻度上的子彈圖，所以預算刻度從上到下對齊，超支最多的那一列不用讀數字就能看出來。

- **月結與預算回顧。** 依部門或成本中心比較計畫與實際營運支出，再展開某個部門，看是哪些科目造成了差異。
- **財務主管的看板。** 在本月、本季和年初至今之間切換；每根條和每個數字一起移動到新的期間。
- **任何帶容差的計畫對實際。** 人力成本、各團隊的雲端支出或專案的各個階段：上下幾個百分點沒關係，超出就值得看一眼。

## 何時不用

- **解釋一個總數是怎麼變化的。** 從起始值走到結束值的瀑布圖更合適。
- **跨很多期間的趨勢。** 此區塊一次只比較一個期間；連續序列請使用 `LineChart` 或 `BarChart`。
- **營收。** 每個數字都按支出來解讀，所以低於預算是有利的。營收對目標的解讀方向正好相反。
- **幾十個成本中心。** 當排序與篩選比圖形更重要時，請使用 `Table`。

## 用法

此區塊衍生自 Avalonia 的 `TemplatedControl`。`Periods` 是切換器裡的期間；每個 `BudgetPeriod` 有一個 id、一個簡短標籤、標題下方顯示的說明，以及用於螢幕閱讀器句子的短語。`Departments` 包含 `BudgetLine`，每個科目針對每個期間 id 有一個 `BudgetAmount`（預算與實際）。部門數字和合計都由科目加總，所以它們總是對得上。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:BudgetVariance SelectedPeriod="month" ExpandedDepartment="eng">
        <arc:BudgetVariance.Periods>
            <arc:BudgetPeriod Id="month" Label="Month" Description="March 2026" Phrase="in March" />
            <arc:BudgetPeriod Id="quarter" Label="Quarter" Description="January to March 2026" Phrase="in the first quarter" />
        </arc:BudgetVariance.Periods>
        <arc:BudgetDepartment Id="eng" Name="Engineering" Owner="Ravi Menon">
            <arc:BudgetLine Id="cloud" Name="Cloud hosting">
                <arc:BudgetAmount Period="month" Budget="176000" Actual="213700" />
                <arc:BudgetAmount Period="quarter" Budget="528000" Actual="613400" />
            </arc:BudgetLine>
        </arc:BudgetDepartment>
    </arc:BudgetVariance>
</UserControl>
```

在程式碼中，`new BudgetLine("cloud", "Cloud hosting").With("month", 176_000, 213_700)` 會加入一個期間的金額並回傳這個科目。

### 結構

- **標頭**：`Title`、期間說明（或 `Description`）以及期間切換。
- **欄標題**：Department、圖表上方的刻度（預算的 0%、50% 與 100%）、Actual、Budget 和 Variance。
- **合計列**：所有部門的總和，名稱下方是部門數量。
- **部門列**：負責人的頭像（除非設定了 `OwnerAvatar`，否則顯示首字母）、名稱與負責人、子彈圖、數字和一個箭頭。展開的部門帶淺色填色，它的科目列在下方，並縮排到頭像之後。
- **圖例**：說明條、刻度、紅色部分和區帶的含義。

### 讀懂子彈圖

- 每根條後面的**區帶**標出容差：到預算的 `RangeLow`、從那裡到 `RangeHigh`，以及再往外。它們以低透明度的文字顏色繪製，所以無論主題色是什麼，區塊都保持中性。
- **條**是實際支出。超過預算**刻度**的部分以危險色繪製。
- **刻度範圍**由每一列、每一個期間共用：至少是預算的 120%，並向上取整到十分之一，讓任何期間裡最大的數字都放得下。切換期間時刻度位置不變。
- **差異**是一個帶正負號的金額和一個以 "over"、"under" 或 "on plan" 結尾的比例。差異小於預算的 `OnPlanThreshold`（預設千分之五）時視為 on plan，保持中性；超支為紅色，節省為綠色。

<Demo name="Tolerance" />

### 期間與部門

`SelectedPeriod`（雙向）指定顯示的期間；切換器會改變它。`ExpandedDepartment`（雙向）指定展開的部門，或為 null；`ToggleDepartment(id)` 會展開或收合一個部門。展開一個部門會收合原本展開的那個。`PeriodChanged` 回報期間變化，`DepartmentToggled` 回報每一次展開與收合，被收合的那個先回報。

<Demo name="Events" />

### 窄版容器

區塊讀取的是自身寬度。寬度不小於 760 時是欄位對齊的表格。更窄時，每一列把名稱、「實際 of 預算」和差異放在上面，子彈圖橫跨整列放在下面，所以圖表永遠不會被擠壓。長名稱會換行，而不是被截斷。

<Demo name="Narrow" />

### 文案與貨幣

`Currency` 放在每個金額前（預設 "$"）；金額縮寫為 k 和 M。其餘所有文字都來自 `Labels`（一個 `BudgetVarianceLabels`）：替換它即可在地化區塊，包括螢幕閱讀器聽到的句子。

## API 參考

<!-- api: BudgetVariance -->

<!-- api: BudgetPeriod -->

<!-- api: BudgetDepartment -->

<!-- api: BudgetLine -->

<!-- api: BudgetAmount -->

<!-- api: BudgetVarianceKind -->

<!-- api: BudgetVarianceLabels -->

## 鍵盤

- **Tab** 先到期間切換，再進入部門清單，然後離開區塊。
- 在期間切換上，**Left** 和 **Right** 改變期間。
- 在部門清單中，**Up** 和 **Down** 移到上一個或下一個部門，**Home** 和 **End** 移到第一個或最後一個。
- **Enter** 或 **Space** 展開或收合焦點所在的部門。

## 無障礙

- 每個部門列都是一個回報展開或收合狀態的按鈕，名稱是一個完整的句子：在哪個期間、花了多少、預算是多少、差異多少，以及是否有利。合計列和每個預算科目也帶有同樣的句子；圖表和正在計數的數字本身不會被朗讀，所以不會重複朗讀，也不會讀到計數中途的值。
- 切換期間時，透過禮貌模式的即時區域播報新的期間和合計。
- 差異從不只靠顏色表達：金額帶正負號，每個比例都以 over、under 或 on plan 結尾。
- 指標聚焦不繪製焦點環；鍵盤聚焦時該列帶有填色，並顯示 Arc 的焦點環。

## 動態效果

- **切換期間**：每根條在同一個臨界阻尼彈簧（smooth，無過衝）上伸縮到新長度，每個金額和比例也在同一彈簧上計數到新值。紅色的超支部分就是同一根條被裁剪到刻度右側的部分，所以顏色恰好在條越過刻度時出現。
- **展開部門**：科目在高度與透明度上一起長出，沒有過衝；收合更快。箭頭沿 Arc 的彈簧曲線轉動，展開時使用主題色。
- 每個動畫都可以被打斷：新的期間或展開從條和數字目前的位置開始。
- 減少動態效果時，條和數字直接跳到終值，科目直接以全高展開。關閉動態效果時所有變化立即生效。

## 相關

- `Market terminal`，Finance 分組的另一個區塊。
- `SegmentedControl` 和 `Avatar`，組成此區塊的控制項。
- 只有一根對照目標的進度條時使用 `Progress`。
