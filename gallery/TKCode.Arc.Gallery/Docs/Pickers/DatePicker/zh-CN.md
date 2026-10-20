---
title: 日期选择器
summary: 选择日期，同时不丢失上下文。
source-hash: 786fcc258ea9
---

## 何时使用

- 表单中的日期字段，例如开始日期或截止日期。
- 用户既可以直接输入、又需要在输入时检查上限与下限的日期。
- 必须落在某个范围内，或需要跳过某些日期的值，通过 `MinDate`、`MaxDate` 和 `DisabledDates` 实现。

## 何时不用

- 需要一直停留在页面上可见的日历：用 `Calendar`。
- 开始与结束日期：用 `DateRangePicker`。
- 一天中的时间，或同时包含日期和时间：在 `DatePicker` 旁边使用 `TimePicker`。

## 用法

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:DatePicker Label="开始日期"
                    Description="输入日期，或按 Alt+Down 打开日历。"
                    SelectedDate="{Binding StartDate, Mode=TwoWay}" />
</UserControl>
```

### 范围限制与禁用日期

`MinDate` 和 `MaxDate` 限定可以选择的日期范围。`DisabledDates` 是一个 `Func<DateOnly, bool>`，对需要禁止选择的日期返回 `true`。这些限制同时作用于日历和手动输入的日期。输入的日期超出范围时会被拒绝：字段会抖动，并恢复为上一个有效值。

```csharp
picker.MinDate = today;
picker.MaxDate = today.AddDays(56);
picker.DisabledDates = d => d.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;
picker.ShowToday = true;
```

### 尺寸

`Size` 决定字段高度：`Small` 为 36，`Medium` 为 44（Arc 的默认外观），`Large` 为 50。

```xml
<arc:DatePicker Label="小" Size="Small" />
<arc:DatePicker Label="大" Size="Large" />
```

### 文化与格式

`Culture` 控制解析方式、月份名称和星期缩写。`DisplayFormat` 接受 .NET 日期格式字符串，会替换该文化的默认显示格式。

```xml
<arc:DatePicker Label="德语" Culture="de-DE" FirstDayOfWeek="Monday" />
<arc:DatePicker Label="ISO 格式" DisplayFormat="yyyy-MM-dd" />
```

### 错误与禁用状态

`ErrorMessage` 非空时字段即为无效态。禁用的字段会淡化为一半透明度。

```xml
<arc:DatePicker Label="发票日期" ErrorMessage="请选择早于截止日期的日期。" />
<arc:DatePicker Label="已锁定" IsEnabled="False" />
```

### 响应变化

`SelectedDateChanged` 同时提供旧日期与新日期。`ClearText`、`EmptyStatusText` 和 `SelectedStatusFormat` 用于设置弹出层底部的文案。

```csharp
picker.SelectedDateChanged += (_, e) =>
{
    status.Text = e.NewDate is { } d ? $"开始：{d:yyyy-MM-dd}" : "没有开始日期";
};
```

## API 参考

<!-- api: DatePicker -->

## 无障碍

- `Label` 为字段命名，并与触发器关联。`Description` 是辅助文字。
- 触发器会暴露展开状态。弹出层以标签命名，例如“开始日期日历”。
- Enter、Space 或 ArrowDown 打开日历，并将焦点移到已选日期；未选择日期时移到今天。日历内部的按键与 `Calendar` 一致。
- Escape 关闭日历并将焦点返回字段。通过 Tab 移出日历，或在日历外按下指针，同样会关闭它。
- 焦点环仅在键盘聚焦时显示。通过指针打开日历时不显示。

## 动效

- 日期中变化的部分沿时间方向滚动：较晚的日期向上升起，较早的日期向下落下。只有变化的部分会移动。
- 选定日期后，圆点滑入到位，弹出层稍后关闭。
- 弹出层从日历图标处以短暂的淡入展开，关闭时淡出时间更短。
- 减弱动效时，数值以淡入淡出替换，选定日期后弹出层立即关闭。关闭动效时直接到终态。

## 相关组件

- `Calendar`：停留在页面上的月视图。
- `DateRangePicker`：附预设选项的开始与结束日期选择。
- `TimePicker`：选择时间，与日期搭配使用。
- `Field` 与 `FieldFrame` 为任何编辑器或触发器提供相同的标签、外框与消息行。
