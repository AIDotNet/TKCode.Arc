---
title: 日期范围选择器
summary: 从触发器展开为两个月份面板的范围选择器，附带预设项和随选区伸展的高亮。
source-hash: 9a9365b4fde3
---

## 何时使用

- 报表与分析筛选，带有“最近 7 天”或“本季度”等预设。
- 预订或请假的开始与结束日期。
- 紧凑的工具栏：范围在更改前以一段简短的标签显示。

## 何时不用

- 单个日期：用 `DatePicker`。
- 需要停留在页面上的月视图：用 `Calendar`。
- 开始与结束的时间：用 `TimePicker`。

## 用法

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:DateRangePicker Label="日期范围" />
</UserControl>
```

```csharp
var today = DateOnly.FromDateTime(DateTime.Today);
picker.SelectedRange = new DateRange(today.AddDays(-6), today);
```

### 预设项

`Presets` 会替换默认的八个预设项列表。每个预设项包含一个标签，以及一个根据当天返回 `DateRange` 的函数。`DateRangePresets.Default` 保存默认列表，因此可以在其基础上扩展自定义列表。

```csharp
picker.Presets =
[
    new DateRangePreset("本周末", t => WeekendFrom(t)),
    new DateRangePreset("两周", t => new DateRange(t, t.AddDays(13))),
];
```

### 范围限制与每周起始日

`MinDate` 和 `MaxDate` 会禁用范围之外的日期，以及超出范围的月份按钮。`FirstDayOfWeek` 设置网格的第一列。`Culture` 设置月份标题、星期标题和范围标签。

```xml
<arc:DateRangePicker Label="入住"
                     FirstDayOfWeek="Monday"
                     Culture="en-US" />
```

```csharp
picker.MinDate = today;
picker.MaxDate = today.AddMonths(6);
```

### 一个月或两个月

`MonthCount` 决定面板布局。`Auto` 在窗口足够宽时显示两个月并附带预设列；否则显示一个月，并配合可滚动的预设行。`One` 始终使用紧凑面板。

```xml
<arc:DateRangePicker Label="紧凑" MonthCount="One" />
```

### 响应“应用”

在按下“应用”之前，选择的范围只是草稿。`SelectedRangeChanged` 只会在“应用”时触发，`FormattedValue` 提供当前值的文本。

```csharp
picker.SelectedRangeChanged += (_, e) =>
{
    status.Text = e.NewRange is { } range ? $"{range.Days} 天" : "没有范围";
};
```

## API 参考

<!-- api: DateRangePicker -->

<!-- api: DateRange -->

## 无障碍

- 触发器会暴露展开状态，其名称包含当前范围。`Label` 是字段名称。
- 每个月份是一个以其标题命名的网格。范围内的日期以选中状态暴露，今天会被标记。
- 在某一天上按 Enter 或 Space 依次选择开始和结束日期。选定开始日期后，方向键会延伸范围预览。
- 预设列表可用 ArrowUp 和 ArrowDown 移动。在紧凑布局中，预设行可用 ArrowLeft 和 ArrowRight 移动。
- Cancel、Escape 或在外部按下指针都会放弃草稿，并将焦点返回触发器。“应用”会提交草稿，并播报开始日期、范围和天数。

## 动效

- 触发器表面会展开为面板，圆角与尺寸同时变化。点击“应用”后它收缩回触发器，范围标签飞回原位。
- 范围高亮从一天伸展到另一天，两端的端点滑动到新的日期。当前预设在各预设项之间滑动。
- 月份沿移动方向滑入，并在身后淡出。标签中的文字沿时间方向滚动。
- 减弱动效时，表面尺寸直接跳转，共享元素和滑动效果被移除。内容以 0.08 到 0.12 秒淡入淡出。关闭动效时直接到终态。

## 相关组件

- `DatePicker`：表单字段中的单个日期。
- `Calendar`：停留在页面上的月视图。
- `TimePicker`：当日期范围需要包含时间时，选择一天中的时间。
