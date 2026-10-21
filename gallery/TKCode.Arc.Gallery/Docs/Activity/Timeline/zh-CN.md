---
title: 时间线
summary: 按日期分组、最新在前，跟进事件的发生经过。
source-hash: e6dcd3991034
---

<Demo name="LiveStream" />

## 何时使用

- 项目历史、审计日志或部署流，其中事件的顺序与新旧至关重要。
- 由人或系统产生的事件，每条包含简短标题和可选的上下文。
- 在屏幕上时会不断接收新事件的动态流。

## 何时不用

- 读者需要排序或比较的记录：用 `SortableDataTable`。
- 有当前位置的固定步骤：用 `Stepper`。
- 针对单个对象的线程式讨论：用 `CommentThread`，实时对话用 `ChatThread`。

## 用法

将 `Now` 设为参考时间。相对时间（“9m”“2h”）和日期分组（“Today”“Yesterday”）都由它计算得出。事件可以任意顺序提供，最新的事件会显示在最前面。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:Timeline Label="发布历史"
                  Now="2026-10-20T12:00:00+00:00">
        <arc:TimelineEvent Id="build-418"
                           At="2026-10-20T11:48:00+00:00"
                           Actor="Ana Ruiz"
                           Title="将 web 部署到生产环境"
                           Meta="Build #418"
                           Detail="全部 312 项检查在 4m 12s 内通过。" />
        <arc:TimelineEvent Id="pr-212"
                           At="2026-10-20T11:02:00+00:00"
                           Actor="Theo Walsh"
                           Title="将 Checkout 重新设计合并到 main"
                           Meta="PR #212" />
    </arc:Timeline>
</UserControl>
```

### 实时动态

将 `ItemsSource` 绑定到一个 `ObservableCollection<TimelineEvent>`。加入其中的事件会从顶部滑入。用计时器推进 `Now`，让相对时间保持最新。Arc 建议约每分钟推进一次。

```csharp
var events = new ObservableCollection<TimelineEvent>();
var timeline = new Timeline { Label = "部署记录", Now = DateTimeOffset.UtcNow, ItemsSource = events };

events.Add(new TimelineEvent("post-1", DateTimeOffset.UtcNow, "将 Checkout 重新设计合并到 main", "Theo Walsh")
{
    Meta = "PR #212"
});

var clock = new DispatcherTimer { Interval = TimeSpan.FromMinutes(1) };
clock.Tick += (_, _) => timeline.Now = DateTimeOffset.UtcNow;
timeline.AttachedToVisualTree += (_, _) => clock.Start();
timeline.DetachedFromVisualTree += (_, _) => clock.Stop();
```

### 可展开的行与色调

带有 `Detail` 的行在被激活时会展开。没有详情的行只是普通文字。`Tone` 会为系统事件着色，因此其标题必须同时说明结果。

<Demo name="ExpandableDetails" />

<Demo name="TonesAndPortraits" />

```xml
<arc:Timeline Label="流水线" Now="2026-10-20T12:00:00+00:00">
    <arc:TimelineEvent Id="t1"
                       At="2026-10-20T11:55:00+00:00"
                       Title="部署成功"
                       Meta="web · production"
                       Tone="Success" />
    <arc:TimelineEvent Id="t2"
                       At="2026-10-20T11:42:00+00:00"
                       Title="部署失败：健康检查超时"
                       Meta="api · staging"
                       Detail="已重试两次。失败的检查记录在 INC-204 中。"
                       Tone="Danger" />
</arc:Timeline>
```

```csharp
timeline.ExpandedChanged += (_, e) => Status.Text = $"{e.Item.Title}，展开状态：{e.IsExpanded}";
timeline.Expand("t2");
```

人员的事件会显示 `Avatar`。系统事件会显示 `Icon`，例如 `ArcIcons.Check`，并由 `Tone` 着色。

### 长历史记录

设置 `MaxHeight` 后，动态流会在内部滚动。当前日期标签会在其行滚动时保持固定，并且列表是虚拟化的，因此长历史记录依然流畅。

<Demo name="ScrollingFeed" />

<Demo name="ThousandUpdates" />

```xml
<arc:Timeline Label="审计日志"
              Now="2026-10-20T12:00:00+00:00"
              ItemsSource="{Binding AuditEvents}"
              MaxHeight="380" />
```

## API 参考

<!-- api: Timeline -->

<!-- api: TimelineEvent -->

## 无障碍

- 动态流是一个带名称的区域。`Label` 是它的无障碍名称。
- 每个日期标签是一个标题，级别为 `HeadingLevel`（默认为 3），每个时间都有隐藏的完整日期文字。
- 可展开的行是按钮，会报告其展开状态。没有详情的行不在 Tab 顺序中。
- 新事件会在状态区域中播报，且不会移动焦点。
- 上下方向键在可展开的行之间移动，Home 和 End 分别跳到第一行和最后一行，Enter 或空格键展开或收起当前行。

## 动效

- 行进入视口时会绘制其连接线，标记点随之弹出。
- 新事件从顶部滑入，下方的行随之下移。展开的详情以弹簧方式打开。
- 减弱动效时，移动改为短暂的淡入，高度立即变化。关闭动效时直接到最终状态。

## 相关组件

- `CommentThread`：针对单个对象的回复；`ChatThread`：实时的双向对话。
- `SortableDataTable`：读者需要排序或比较记录时使用。
- `Stepper`：有当前位置的固定步骤。
