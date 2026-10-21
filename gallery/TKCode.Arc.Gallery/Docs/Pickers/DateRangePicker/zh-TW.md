---
title: 日期範圍選擇器
summary: 從觸發元件展開為兩個月份面板，附帶預設選項與可延伸的範圍高亮。
source-hash: e9ac32a5a133
---

<Demo name="Report" />

## 何時使用

- 報表與分析篩選，附帶「最近 7 天」或「本季」等預設選項。
- 預訂或請假時的開始與結束日期。
- 緊湊的工具列，範圍在變更前以一段簡短標籤顯示。

## 何時不用

- 單一日期：用 `DatePicker`。
- 停留在頁面上的月份檢視：用 `Calendar`。
- 開始與結束的時刻：用 `TimePicker`。

## 用法

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:DateRangePicker Label="日期範圍" />
</UserControl>
```

```csharp
var today = DateOnly.FromDateTime(DateTime.Today);
picker.SelectedRange = new DateRange(today.AddDays(-6), today);
```

### 預設選項

`Presets` 會取代預設的八個選項清單。每個預設選項包含一個標籤，以及一個以當天為基準回傳 `DateRange` 的函式。`DateRangePresets.Default` 保存預設清單，因此自訂清單可以在它的基礎上擴充。

```csharp
picker.Presets =
[
    new DateRangePreset("本週末", t => WeekendFrom(t)),
    new DateRangePreset("兩週", t => new DateRange(t, t.AddDays(13))),
];
```

### 範圍限制與每週起始日

`MinDate` 與 `MaxDate` 會停用範圍之外的日期，以及超出範圍的月份按鈕。`FirstDayOfWeek` 設定格線的第一欄。

<Demo name="Booking" />

### 單月或雙月

`MonthCount` 選擇面板版面。`Auto` 在視窗夠寬時顯示兩個月與預設選項欄，否則顯示一個月與可捲動的預設選項列。`One` 一律使用緊湊面板。

<Demo name="OneMonth" />

### 文化特性

`Culture` 設定月份標題、星期標題與範圍標籤。

<Demo name="Culture" />

### 回應「套用」

在按下「套用」之前，選擇內容都是草稿。`SelectedRangeChanged` 只會在套用時引發，`FormattedValue` 提供目前值的文字。

```csharp
picker.SelectedRangeChanged += (_, e) =>
{
    status.Text = e.NewRange is { } range ? $"{range.Days} 天" : "無範圍";
};
```

## API 參考

<!-- api: DateRangePicker -->

<!-- api: DateRange -->

## 無障礙

- 觸發元件會公開展開狀態，其名稱包含目前的範圍。`Label` 是欄位名稱。
- 每個月份都是以其標題命名的格線。範圍內的日期會公開為已選取，今天會被標記。
- 在某一天上按 Enter 或 Space，會先選擇開始日，再選擇結束日。選定開始日後，方向鍵會延伸範圍預覽。
- 預設選項清單可用 ArrowUp 與 ArrowDown 移動。在緊湊版面中，預設選項列可用 ArrowLeft 與 ArrowRight 移動。
- 取消、Escape 以及在外部按下指標，都會捨棄草稿並將焦點送回觸發元件。套用會提交草稿，並播報開始日、範圍與天數。

## 動態效果

- 觸發元件的表面會展開為面板，其邊角與尺寸同時變化。套用時它會收回觸發元件，範圍標籤飛回原位。
- 範圍高亮會逐日延伸，兩端的端點滑到新的日期。目前的預設選項會在各選項之間滑動。
- 月份會朝移動方向滑入，並在其後淡出。標籤文字依時間方向滾動出現。
- 減少動態效果時，表面尺寸直接跳變，共享元素與滑動效果會移除。內容以 0.08 到 0.12 秒淡入淡出。關閉動態效果時直接到最終狀態。

## 相關元件

- `DatePicker`：表單欄位中的單一日期。
- `Calendar`：停留在頁面上的月份。
- `TimePicker`：日期範圍需要時間時，用來選擇一天中的時刻。
