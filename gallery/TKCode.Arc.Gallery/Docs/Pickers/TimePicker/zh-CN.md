---
title: 时间选择器
summary: 以合理的键盘行为选择时间。
source-hash: aa3abfbf3231
---

## 何时使用

- 从等距选项中选择一天中的时间，例如会议或提醒。
- 以 `TimeOnly` 值存储、并按用户文化的时钟显示的时间。
- 与 `DatePicker` 配合，组成日期与时间的表单字段。

## 何时不用

- 少量有名称的时间，如"上午"或"晚上"：用 `Select`。
- 日期：用 `DatePicker`；停留在页面上的月份用 `Calendar`。
- 日期范围：用 `DateRangePicker`。

## 用法

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:TimePicker Label="会议时间"
                    Description="每 15 分钟一个时间点。"
                    SelectedTime="{Binding MeetingTime, Mode=TwoWay}" />
</UserControl>
```

### 步长与时钟

`MinuteStep` 设置选项之间的间隔，默认为 15 分钟。`ClockFormat` 选择显示格式：`TwelveHour` 显示 "9:15 AM"，`TwentyFourHour` 显示 "09:15"，`Auto` 跟随 `Culture` 的短时间格式。无论显示格式如何，值始终是 `TimeOnly`。

```xml
<arc:TimePicker Label="每 5 分钟" MinuteStep="5" />
<arc:TimePicker Label="24 小时制" MinuteStep="30" ClockFormat="TwentyFourHour" />
```

### 空值

`SelectedTime` 是 `TimeOnly?`。为 `null` 时，字段显示 `Placeholder`。

```xml
<arc:TimePicker Label="提醒" Placeholder="选择时间" />
```

```csharp
picker.SelectedTime = new TimeOnly(18, 30);
picker.SelectedTimeChanged += (_, e) =>
{
    status.Text = e.NewTime is { } time ? $"已设为 {time:HH:mm}" : "无时间";
};
```

### 尺寸与状态

`Size` 设置字段高度：`Small` 为 36，`Medium` 为 44（Arc 的默认外观），`Large` 为 50。非空的 `ErrorMessage` 会将字段标记为无效。`IsEnabled="False"` 会禁用它。

```xml
<arc:TimePicker Label="小" Size="Small" />
<arc:TimePicker Label="入住" ErrorMessage="入住时间从下午 3:00 开始。" />
<arc:TimePicker Label="已锁定" IsEnabled="False" />
```

## API 参考

<!-- api: TimePicker -->

## 无障碍

- `Label` 为字段命名。`Description` 与 `ErrorMessage` 是帮助文本。
- 列表打开时，焦点保留在触发元件上。触发元件会报告其展开状态与当前值，因此列表变化时，屏幕阅读器会播报当前时间。
- Enter、Space 或 ArrowDown 打开列表，所选时间位于中央并处于激活状态。ArrowUp 与 ArrowDown 在各个时间之间移动，并在两端循环。
- Escape 关闭列表。在列表外按下指针也会将其关闭。
- 触发元件在键盘获得焦点时显示焦点环。

## 动效

- 数值按时间方向滚动：较晚的时间从下方升起，较早的时间从上方落下。
- 列表以短促的弹簧从触发元件展开并淡入，关闭时几乎立即完成。
- 激活行会快速改变背景，一个小圆点标记所选时间，因此同一时间只有一行高亮。
- 减少动效时，数值只做交叉淡化，列表也仅淡入淡出。关闭动效时直接到终态。

## 相关组件

- `DatePicker`：将日期与时间配对。
- `Select`：少量有名称的选项。
- `DateRangePicker`：日期范围。
