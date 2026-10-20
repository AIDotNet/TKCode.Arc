---
title: 日历
summary: 以清晰紧凑的月视图浏览日期。
source-hash: e3b14dff05ce
---

## 何时使用

- 停留在页面上的月视图，例如预订或可用性页面。
- 日历本身就是内容，需要选择一个日期。
- 通过 `MinDate`、`MaxDate` 或 `DisabledDates` 限制可选日期。

## 何时不用

- 弹出浮层的表单日期字段：用 `DatePicker`。
- 开始与结束日期：用 `DateRangePicker`。
- 一天中的某个时间：用 `TimePicker`。

## 用法

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:Calendar ShowToday="True" />
</UserControl>
```

### 选中日期

`SelectedDate` 是 `DateOnly?`，只使用其中的日期部分。它是双向绑定的，绑定的值会跟随用户的选择。点击、按下 Enter 或 Space，以及点击“今天”按钮时都会触发 `DateSelected`。

```xml
<arc:Calendar SelectedDate="{Binding StayDate, Mode=TwoWay}" />
```

```csharp
calendar.DateSelected += (_, e) => status.Text = $"已选择 {e.Date:D}";
```

### 范围限制与禁用日期

`MinDate` 和 `MaxDate` 限制导航与选择的范围。`DisabledDates` 是一个 `Func<DateOnly, bool>`，对不能选择的日期返回 `true`。使用方向键移动焦点时会跳过禁用的日期。

```csharp
calendar.MinDate = today.AddDays(-3);
calendar.MaxDate = today.AddDays(45);
calendar.DisabledDates = d => d.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;
calendar.FirstDayOfWeek = DayOfWeek.Monday;
```

### 显示月份

`DisplayMonth` 设置显示的月份，是双向绑定的，且只使用年和月。`GoToMonth` 可以在代码中将视图切换到某个日期所在的月份。

```xml
<arc:Calendar DisplayMonth="{Binding ViewMonth, Mode=TwoWay}" />
```

### 文案与文化

`Culture` 设置月份与星期名称的语言。`TodayLabel`、`PreviousMonthLabel` 和 `NextMonthLabel` 替换按钮文字与无障碍名称。

```xml
<arc:Calendar TodayLabel="今天" PreviousMonthLabel="上个月" NextMonthLabel="下个月" />
```

## API 参考

<!-- api: Calendar -->

## 无障碍

- 日历以月份标题命名。上一月、下一月和“今天”按钮都有名称；当月份包含 `MinDate` 或 `MaxDate` 时，对应的上一月或下一月按钮会被禁用。
- 每一天都是一个按钮，名称为完整日期。选中的日期会以选中状态暴露，今天会以状态标记。
- 方向键按天和按周移动焦点，并可跨月移动。Home 和 End 移到所在周的第一天和最后一天。PageUp 和 PageDown 按月移动，按住 Shift 则按年移动。
- Enter 和 Space 选择当前焦点所在的日期。Tab 顺序中同一时间只有一天。
- 月份切换以礼貌方式播报。滑出视野的月份不可获得焦点，并对辅助技术隐藏。

## 动效

- 月份沿移动方向在同一条带上滑动，标题随之滑动，选中的圆点从一天滑向另一天。圆点经过的数字会改变颜色。
- 大跨度跳转（例如“今天”或 Shift + PageDown）从目标月份旁边的月份开始，不会经过中间的各个月份。
- 减弱动效时，月份与圆点直接跳到终态，标题原地替换。关闭动效时同样如此。

## 相关组件

- `DatePicker`：用于弹出日历的日期字段。
- `DateRangePicker`：带预设的开始与结束日期选择。
- `TimePicker`：用于选择一天中的时间。
