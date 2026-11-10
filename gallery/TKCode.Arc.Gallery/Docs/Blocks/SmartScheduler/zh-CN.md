---
title: Smart scheduler
summary: 用自然语言说出你在意的条件来找会议时间：最合适的时段显示在一周视图上，旁边是每个人的忙碌时间，每个时段都附有理由，可以直接预订。
source-hash: f9c24e5b370d
---

<Demo name="Preview" />

## 何时使用

`SmartScheduler` 为一组人找会议时间。选择参加的人和会议时长，然后用自然语言说出你在意的条件："afternoons, not Friday, keep lunch free"。调度器会把读懂的条件显示成小标签，比对每个人的日历，并在一周视图上显示最合适的几个时段；视图中的每个格子按当时忙碌的人数加深颜色。每个提议都会说明被选中的理由（"Everyone is free"、"Leaves a break before and after"）。在周视图或列表中选一个，然后预订。

- **团队会议。** 每周例会、评审、跨多个日历的面试安排。
- **客户电话。** 邀请同事，并让规划器遵守 "before 4pm" 这样的限制。
- **日程助手。** 接入你自己的查找逻辑，同时保留相同的周视图和预订流程。

## 何时不用

- **预订某一个人的空闲时间。** 带日期选择器的预订页更简单。
- **在完整日历上手动选时间。** 使用 `Week calendar` 或 `Availability picker` 区块。
- **规则复杂的周期性会议。** 规划器一次只看一周。

## 用法

本区块派生自 Avalonia 的 `TemplatedControl`。不设置任何数据时显示一个虚构的团队和一周（`SmartSchedulerSamples`）。文字由 `SmartSchedulerPlanner.Parse` 解读，时段由 `SmartSchedulerPlanner.Find` 查找，除非你设置了 `SlotFinder`。

```xml
<arc:SmartScheduler WeekStart="2026-11-16" ConstraintsText="Afternoons, not Friday" />
```

```csharp
scheduler.People = team.Select(p => new SmartSchedulerPerson
{
    Id = p.Id, Name = p.Name, Detail = p.Role,
    Busy = calendar.BusyTimes(p.Id, week).Select(b => new SmartSchedulerBusy(b.Day, b.Start, b.End)).ToArray(),
}).ToArray();
scheduler.BookAction = (e, cancellation) => calendar.InviteAsync(e.Start, e.End, e.People, cancellation);
scheduler.SlotBooked += (_, e) => toast.Show($"Booked {e.Start:t}");
```

### 规划器能读懂什么

上午和下午；"not"、"no"、"except" 或 "avoid" 加上星期几；"after 10"、"after 2pm"、"before 4pm"；午餐；"asap" 或 "as soon as possible"；"breaks" 或 "buffer"；以及像 "45 min" 或 "1 hour" 这样的时长，它也会改变时长选项。读懂的每一项都会在输入框下方显示为小标签，让人看到被读出的内容。规划器会在一周的每一天、工作日（`DayStartHour` 到 `DayEndHour`）的每半小时逐一尝试，保留所有受邀者都空闲且满足全部限制的时段，然后排序：前后留有空当、当天团队较空闲以及一天中的时间都会加分，"as soon as possible" 则偏向最早的时段。它会提议 `SlotCount` 个时段，能做到时每天最多一个。

<Demo name="Soonest" />

### 状态

`State` 从 `Idle` 进入 `Finding`，然后是 `Proposed` 或 `NoTimes`；预订会经过 `Booking` 到达 `Booked`，之后可以用 "Find another time" 重新开始。`Slots`、`SelectedSlot` 和 `Constraints` 报告当前显示的内容。`FindAsync()`、`SelectSlot(index)`、`BookAsync()`、`SetInvited(person, value)` 和 `StartOver()` 从代码完成控件所做的事。改变受邀的人或时长会清除提议，因为它们可能已不再合适。

### 文字

`Labels`（`SmartSchedulerLabels`）包含各个标题、按钮、状态提示、时段和小时的格式，以及 `Reasons` 中规划器给出的理由和小标签文字。

## API 参考

<!-- api: SmartScheduler -->

<!-- api: SmartSchedulerPerson -->

<!-- api: SmartSchedulerBusy -->

<!-- api: SmartSchedulerSlot -->

<!-- api: SmartSchedulerConstraints -->

<!-- api: SmartSchedulerState -->

<!-- api: SmartSchedulerLabels -->

## 无障碍

- 调度器是一个有名称的区域；标题是二级标题，表单的各部分是三级标题。
- 每个人都是一个以姓名命名、以说明文字描述的复选框；时长是一个分段控件。
- 周视图是一个 Tab 停靠点，作为图像读出，帮助文本是所选的提议。**Left**、**Right**、**Up** 和 **Down** 在提议的时段之间移动，**Home** 和 **End** 跳到第一个和最后一个。
- 每个提议都是一个按钮，命名为 "Option 1: Tue, Nov 17 · 2:00–2:30 PM"，以理由作为说明，并报告 "selected"。
- "Comparing calendars…"、第一个提议、"没有合适时间" 和预订结果都会礼貌地朗读。

## 动效

- 选择框以 snappy 弹簧在时段之间滑动；提议在悬停和选中时于 160 ms 内变色。
- 查找和预订按钮在工作期间显示加载圈。
- 减弱动效和关闭动效时选择框立即移动。

## 响应式

- 调度器最宽 1040。宽布局时，表单是周视图旁宽 300 的一列。宽度不超过 760 时（`:compact`），表单移到周视图上方，内边距从 24 收紧到 16。
- 周视图保持高度，并把宽度分给每一天；小时标签留在宽 48 的边栏中。
- 小标签会换行；提议的理由会换行。没有任何功能依赖悬停。

## 相关组件

- `Availability picker`，用于跨日历手动选择时间。
- `Week calendar`，支持拖放的完整日程表。
- `Textarea`、`Checkbox` 和 `SegmentedControl`，组成其表单的控件。
