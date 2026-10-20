---
title: 日期選擇器
summary: 選取日期，同時不遺失上下文。
source-hash: 786fcc258ea9
---

## 何時使用

- 表單中的日期欄位，例如開始日期或截止日期。
- 使用者也能直接輸入的日期，輸入時會檢查是否符合限制。
- 必須落在某個範圍內，或需要略過某些日子的日期，透過 `MinDate`、`MaxDate` 和 `DisabledDates` 實現。

## 何時不用

- 需要一直停留在頁面上可見的日曆：用 `Calendar`。
- 開始與結束日期：用 `DateRangePicker`。
- 一天中的時間，或同時包含日期與時間：在 `DatePicker` 旁邊使用 `TimePicker`。

## 用法

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:DatePicker Label="開始日期"
                    Description="輸入日期，或按 Alt+Down 開啟日曆。"
                    SelectedDate="{Binding StartDate, Mode=TwoWay}" />
</UserControl>
```

### 範圍限制與停用日期

`MinDate` 和 `MaxDate` 限定可以選取的日期範圍。`DisabledDates` 是一個 `Func<DateOnly, bool>`，對需要禁止選取的日期傳回 `true`。這些限制同時套用於日曆和手動輸入的日期。輸入的日期超出範圍時會被拒絕：欄位會抖動，並還原為上一個有效值。

```csharp
picker.MinDate = today;
picker.MaxDate = today.AddDays(56);
picker.DisabledDates = d => d.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;
picker.ShowToday = true;
```

### 尺寸

`Size` 決定欄位高度：`Small` 為 36，`Medium` 為 44（Arc 的預設外觀），`Large` 為 50。

```xml
<arc:DatePicker Label="小" Size="Small" />
<arc:DatePicker Label="大" Size="Large" />
```

### 語系與格式

`Culture` 控制解析方式、月份名稱與星期縮寫。`DisplayFormat` 接受 .NET 日期格式字串，會取代該語系的預設顯示格式。

```xml
<arc:DatePicker Label="德文" Culture="de-DE" FirstDayOfWeek="Monday" />
<arc:DatePicker Label="ISO 格式" DisplayFormat="yyyy-MM-dd" />
```

### 錯誤與停用狀態

`ErrorMessage` 不為空時欄位即為無效狀態。停用的欄位會淡化為一半透明度。

```xml
<arc:DatePicker Label="發票日期" ErrorMessage="請選擇早於截止日期的日期。" />
<arc:DatePicker Label="已鎖定" IsEnabled="False" />
```

### 回應變更

`SelectedDateChanged` 同時提供舊日期與新日期。`ClearText`、`EmptyStatusText` 和 `SelectedStatusFormat` 用於設定彈出層底部的文案。

```csharp
picker.SelectedDateChanged += (_, e) =>
{
    status.Text = e.NewDate is { } d ? $"開始：{d:yyyy-MM-dd}" : "沒有開始日期";
};
```

## API 參考

<!-- api: DatePicker -->

## 無障礙

- `Label` 為欄位命名，並與觸發器關聯。`Description` 是說明文字。
- 觸發器會呈現展開狀態。彈出層以標籤命名，例如「開始日期日曆」。
- Enter、Space 或 ArrowDown 開啟日曆，並將焦點移到已選取的日期；未選取日期時移到今天。日曆內部的按鍵與 `Calendar` 一致。
- Escape 關閉日曆並將焦點返回欄位。以 Tab 移出日曆，或在日曆外按下指標，同樣會關閉它。
- 焦點環僅在鍵盤聚焦時顯示。以指標開啟日曆時不顯示。

## 動態效果

- 日期中變化的部分沿時間方向滾動：較晚的日期向上升起，較早的日期向下落下。只有變化的部分會移動。
- 選取日期後，圓點滑入定位，彈出層稍後關閉。
- 彈出層從日曆圖示處以短暫的淡入展開，關閉時淡出時間較短。
- 減少動態效果時，數值以淡入淡出替換，選取日期後彈出層立即關閉。關閉動態效果時直接到最終狀態。

## 相關元件

- `Calendar`：停留在頁面上的月檢視。
- `DateRangePicker`：附預設選項的開始與結束日期選取。
- `TimePicker`：選取時間，與日期搭配使用。
- `Field` 與 `FieldFrame` 為任何編輯器或觸發器提供相同的標籤、外框與訊息列。
