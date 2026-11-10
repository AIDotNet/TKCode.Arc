---
title: Agent routines
summary: 按计划执行的智能体例行任务：用平实语言描述的 cron 计划和下次运行时间、近期结果条、暂停或恢复的开关、Run now，以及按需查看的指令和运行记录。
source-hash: fc89f6491b4b
---

<Demo name="Preview" />

## 何时使用

`AgentRoutines` 管理 AI 智能体按计划完成的工作：早间摘要、夜间检查、每周报告。每一行显示例行任务的名称和智能体、用平实语言描述的计划（"Weekdays at 08:00"）和下次运行时间，以及一条近期结果条：绿色表示成功，红色且较高表示失败，灰色且较短表示跳过。一个开关用来暂停或恢复，Run now 立即运行。展开一行可以看到智能体遵循的指令以及近期运行和结果。

- **按计划运行的智能体**，用于自动化或助手产品。
- **运维面板**：用户确认例行工作在运行，并在某次失败时介入。

## 何时不用

- **一次性任务。** 请使用 `Agent tasks`。
- **编辑计划。** 这个区块只显示计划；请用你自己的表单编辑。
- **没有智能体的普通 cron 任务。** 表格更合适。

## 用法

区块派生自 Avalonia 的 `TemplatedControl`。不设置任何属性时，它显示示例例行任务，Run now 会播放一次简短的替身运行。在应用中，给它你的例行任务，并在你的后端启动运行：

```xml
<arc:AgentRoutines Routines="{Binding Routines}" />
```

```csharp
list.RunHandler = (routine, cancellation) => api.RunNowAsync(routine.Id, cancellation); // returns an AgentRoutineRun
list.EnabledChanged += (_, e) => api.SetEnabled(e.Routine.Id, e.Routine.IsEnabled);
```

### 计划

`Schedule` 是五个字段的 cron 表达式：分钟、小时、日期、月份、星期（星期日为 0）。字段可以是数字、范围、列表和步长。`AgentRoutineSchedule.Describe` 把常见形式转成文字，`Next` 给出下次运行时间；两者都是公开的，服务器可以使用同样的规则。暂停的例行任务显示 Paused，而不是下次运行时间。

### 运行

`Runs` 按从新到旧列出过去的运行，每次都有开始时间、结果、耗时和摘要。结果条显示最近 `HistoryLength` 次运行。用 Run now 启动的运行经过 `RunHandler`；结果加在 `Runs` 的最前面，处理程序抛出异常时记为一次失败的运行并带上消息。

<Demo name="Events" />

## API 参考

<!-- api: AgentRoutines -->

<!-- api: AgentRoutine -->

<!-- api: AgentRoutineRun -->

<!-- api: AgentRoutineSchedule -->

<!-- api: AgentRoutinesLabels -->

## 无障碍

- 列表是名为 "Routines" 的区域；标题是标题。
- **Tab** 可到达每个例行任务的开关、标题行和 Run now。**Space** 切换开关；**Enter** 或 **Space** 展开例行任务或开始运行。
- 开关读作 "Run Morning inbox digest on schedule"；标题行读出名称、计划和下次运行时间，并报告自身展开或收起的状态。
- 结果条读作一句摘要（"8 of 10 recent runs succeeded"）。
- 一个礼貌式实时区域会在用 Run now 启动的运行结束时播报。

## 动效

- 例行任务的详情以弹簧展开并带短暂淡入；箭头以灵敏的弹簧转动。
- 暂停的例行任务把标题行淡到 60%。
- 减少动效时只保留短暂淡入淡出；关闭动效时所有变化立即生效。

## 响应式

- 列表最宽 760，小于这个宽度时跟随容器。
- 宽度在 560 或以下时，结果条和 Run now 移到计划下方。
- 名称和计划用省略号缩短。

## 相关组件

- `Agent tasks`，交给智能体的一次性工作。
- `Agent run`，详细查看一次运行。
- `Switch`，用来暂停例行任务的控件。
