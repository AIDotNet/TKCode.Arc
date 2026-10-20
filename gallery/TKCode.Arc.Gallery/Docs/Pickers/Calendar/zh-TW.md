---
title: 日曆
summary: 以清楚緊湊的月檢視瀏覽日期。
source-hash: e3b14dff05ce
---

## 何時使用

- 停留在頁面上的月檢視，例如預訂或可用性頁面。
- 日曆本身就是內容，需要選取一個日期。
- 透過 `MinDate`、`MaxDate` 或 `DisabledDates` 限制可選取的日期。

## 何時不用

- 會彈出浮層的表單日期欄位：用 `DatePicker`。
- 開始與結束日期：用 `DateRangePicker`。
- 一天中的某個時間：用 `TimePicker`。

## 用法

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:Calendar ShowToday="True" />
</UserControl>
```

### 選取日期

`SelectedDate` 是 `DateOnly?`，只使用其中的日期部分。它是雙向繫結的，繫結的值會跟隨使用者的選擇。點擊、按下 Enter 或 Space，以及點擊「今天」按鈕時都會觸發 `DateSelected`。

```xml
<arc:Calendar SelectedDate="{Binding StayDate, Mode=TwoWay}" />
```

```csharp
calendar.DateSelected += (_, e) => status.Text = $"已選取 {e.Date:D}";
```

### 範圍限制與停用日期

`MinDate` 和 `MaxDate` 限制導覽與選取的範圍。`DisabledDates` 是一個 `Func<DateOnly, bool>`，對不能選取的日期傳回 `true`。使用方向鍵移動焦點時會略過停用的日期。

```csharp
calendar.MinDate = today.AddDays(-3);
calendar.MaxDate = today.AddDays(45);
calendar.DisabledDates = d => d.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;
calendar.FirstDayOfWeek = DayOfWeek.Monday;
```

### 顯示月份

`DisplayMonth` 設定顯示的月份，是雙向繫結的，且只使用年與月。`GoToMonth` 可在程式碼中將檢視切換到某個日期所在的月份。

```xml
<arc:Calendar DisplayMonth="{Binding ViewMonth, Mode=TwoWay}" />
```

### 文案與語系

`Culture` 設定月份與星期名稱的語言。`TodayLabel`、`PreviousMonthLabel` 和 `NextMonthLabel` 取代按鈕文字與無障礙名稱。

```xml
<arc:Calendar TodayLabel="今天" PreviousMonthLabel="上個月" NextMonthLabel="下個月" />
```

## API 參考

<!-- api: Calendar -->

## 無障礙

- 日曆以月份標題命名。上個月、下個月和「今天」按鈕都有名稱；當月份包含 `MinDate` 或 `MaxDate` 時，對應的上個月或下個月按鈕會被停用。
- 每一天都是一個按鈕，名稱為完整日期。選取的日期會以已選取狀態呈現，今天會以狀態標示。
- 方向鍵按天和按週移動焦點，並可跨月移動。Home 和 End 移到所在週的第一天和最後一天。PageUp 和 PageDown 按月移動，按住 Shift 則按年移動。
- Enter 和 Space 選取目前焦點所在的日期。Tab 順序中同一時間只有一天。
- 月份切換以禮貌方式通知。滑出視野的月份無法取得焦點，並對輔助技術隱藏。

## 動態效果

- 月份沿移動方向在同一條帶上滑動，標題隨之滑動，選取的圓點從一天滑向另一天。圓點經過的數字會改變顏色。
- 大跨度跳轉（例如「今天」或 Shift + PageDown）從目標月份旁邊的月份開始，不會經過中間的各個月份。
- 減少動態效果時，月份與圓點直接跳到最終狀態，標題原地替換。關閉動態效果時亦同。

## 相關元件

- `DatePicker`：用於彈出日曆的日期欄位。
- `DateRangePicker`：附預設選項的開始與結束日期選取。
- `TimePicker`：用於選取一天中的時間。
