---
title: 时间线
summary: 按日期分组、最新在前，跟进事件的发生经过。
source-hash: 689d73e3bb87
---

<Demo name="LiveStream" />

## 何时使用

`Timeline` 是应用中的活动动态，其中事件的顺序与新近程度比彼此比较更重要。它按日期分组、最新的在前，并且在屏幕上时会接收更新。每一行是一条更新：人的操作或系统事件，带有简短标题、可选的上下文，以及在读者需要时原地展开的详情。

- **发布页上的项目历史**：Theo Walsh 将 Checkout 重新设计合并到 main，并以拉取请求作为上下文。读者通过日期分组即可看出一次发布是如何完成的。
- **DevOps 控制台中的部署流**：构建、发布与回滚，系统事件按成功或危险色调显示。标题直接说明结果，因此颜色从来不是唯一的信号。
- **管理后台中的审计日志**：谁在何时修改了账单方案或用户角色。每一行都可展开，显示修改前后的值，因此列表保持简短。
- **CRM 中的账号动态**：某一位客户的通话、邮件与合同变更，每条都附有执行人的头像。
- **状态页上的事件进展**：实时动态，响应人员发布更新时，每条都从顶部滑入。

## 何时不用

- **读者需要排序或比较的记录**，例如按金额排序的发票或按最后登录时间排序的用户：用 `Table`。
- **有当前位置的固定步骤**，例如结账或入职流程：用 `Stepper`。
- **针对单个对象的线程式讨论**：用 `CommentThread`；实时的双向对话用 `ChatThread`。
- **某个条目的一个当前状态**，例如发票上的“付款失败”：在该条目旁边用 `Alert`。只有一个事件的时间线只是一条消息，而不是动态流。

## 用法

`Timeline` 派生自 Avalonia 的 `ItemsControl`。它的项是 `TimelineEvent` 对象，可以通过 `ItemsSource` 绑定，也可以直接内联编写。事件可以按任意顺序提供，最新的显示在最前面。将 `Now` 设为参考时间：相对时间（如 “9m”、“2h”）以及日期分组 “Today” 与 “Yesterday” 都由它计算得出。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
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

`Timeline` 派生自 `ItemsControl`，并自行排列各行，因此应提供 `TimelineEvent` 项，而不是项模板。除非设置了 `MaxHeight`，动态流会随页面一起增长，因此应将它放在可滚动的页面中，或为它设定高度，让它在内部滚动。

### 结构

1. **日期标签**（`HeadingLevel`，默认为 3）：“Today”、“Yesterday”，或更早日期的星期与日期，并附带当天的更新数。它是一行 19.6 px 的文字，上下各留 8 px。
2. **标记**：直径 28 px 的圆形，中间是 14 px 的图标，位于行顶部下方 8 px。人的事件显示其 `Avatar`，系统事件显示 `Icon`；没有头像的人员显示空白标记。
3. **连接线**：从标记底部延伸到下一行的线条。行进入视口时它会自行绘制。
4. **触发器**：标题位于 20 px 的行中，`Meta` 位于 16.8 px 的行中，`Actor` 以前景色排在最前。触发器有 12 px 的内边距，距左边缘 36 px。
5. **时间**：位于标题之后，宽度至少 44 px。今天的更新显示相对时间（“9m”“2h”）；更早的日期显示钟点时间（“1:00 PM”）。
6. **箭头**：位于带有 `Detail` 的行的时间之后，为 16 px 的箭头。没有详情的行不显示箭头，也不会展开。
7. **详情**：在触发器下方展开的 `Detail`，两侧内边距 12 px，底部 16 px。
8. **吸顶日期标签**：在可滚动的动态流（设置了 `MaxHeight`）中，当前日期标签固定在顶部，其下的行滚动经过。
9. **羽化**：可滚动的动态流下方还有更多行时，其底边会在 36 px 的范围内渐隐。

时间线的日期标签默认使用 Arc 表面色，它会遮住滚动经过其下方的行。请将 `HeaderBackground` 设为动态流所在的表面，否则标签颜色会与页面不一致。

宽度小于 420 px 时，触发器的内边距降为 8 px，时间与箭头会更靠近标题。

### 绑定到视图模型

视图模型拥有事件、参考时间和展开的行。时间线负责显示它们，视图模型在事件到达时添加。本例使用 CommunityToolkit.Mvvm。

```csharp
public partial class DeploymentFeedViewModel : ObservableObject
{
    public ObservableCollection<TimelineEvent> Events { get; } = [];

    [ObservableProperty]
    private DateTimeOffset _now = DateTimeOffset.UtcNow;

    [ObservableProperty]
    private IReadOnlyList<string> _expandedIds = [];

    public void Add(TimelineEvent item) => Events.Add(item);

    public void Tick() => Now = DateTimeOffset.UtcNow;
}
```

```xml
<arc:Timeline Label="部署记录"
              ItemsSource="{Binding Events}"
              Now="{Binding Now}"
              ExpandedIds="{Binding ExpandedIds}"
              MaxHeight="420" />
```

- `ItemsSource` 接收事件。向集合中添加事件时，新行会从顶部滑入，视图模型无需重建列表。
- `Now` 为单向绑定。如下一节所示，由计时器设置它；它变化时只有时间标签会更新。
- `ExpandedIds` 为双向绑定。它保存已展开行的标识，因此页面重新加载时视图模型可以恢复它们。

### 实时动态

约每分钟用计时器推进一次 `Now`，让相对时间保持最新。当动态流离开屏幕时停止计时器，使未使用的页面不做任何工作。`IsScrollToNewEnabled`（默认为 true）在读者向下滚动时有新的最新更新到达，会将动态流滚回顶部。

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

带有 `Detail` 的行在被激活时会展开。`Detail` 可以是文字，也可以是任意控件，并在原位展开。`Detail` 为空字符串时，该行不会变为可展开。`Tone` 为系统事件着色：

| Tone | 用于 | 显示方式 |
|---|---|---|
| `Neutral`（默认） | 普通更新 | 安静的标记 |
| `Success` | 已完成的步骤：部署、迁移、付款 | 标记着色，图标使用成功色 |
| `Danger` | 失败或回滚 | 标记着色，图标使用危险色 |

标题与色调都要说明结果，使含义不只依赖颜色。

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

`ExpandedChanged` 会报告每一行的展开或收起，无论是用户操作，还是代码调用了 `Expand`、`Collapse` 或 `Toggle`。人的事件显示 `Avatar`；系统事件在没有头像时显示 `Icon`，例如 `ArcIcons.Check`。

### 长历史记录

设置 `MaxHeight` 后，动态流会在内部滚动。当前日期标签在其下的行滚动时保持固定。列表是虚拟化的，只有可见的行和少量缓冲行存在，因此长历史记录依然保持流畅。

<Demo name="ScrollingFeed" />

<Demo name="ThousandUpdates" />

```xml
<arc:Timeline Label="审计日志"
              Now="2026-10-20T12:00:00+00:00"
              ItemsSource="{Binding AuditEvents}"
              MaxHeight="380" />
```

使用 `ScrollIntoView(id)` 将某个事件滚动到可见区域，例如从通知中的链接跳转过去。`ScrollToTop(smooth)` 会把动态流滚回最新的更新，在完整动效下平滑滚动。

### 示例：事件进展动态流

状态控制台中 INC-204 的事件页面。动态流按顺序展示响应人员的操作与系统事件。失败的部署是危险事件，标题已说明结果，详情解释了重试；回滚是一位人员的操作，带有响应人员的姓名；健康检查则是成功事件。“Yesterday”分组表明页面会保留更早的历史。每一行的标题都是完整的句子，因此动态流读起来像一份日志。

![带有危险事件、回滚、成功事件与日期分组的事件进展动态流](images/IncidentFeed.png)

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc"
             Width="600">
    <Border Padding="16,12,16,4"
            CornerRadius="26"
            BorderThickness="1"
            Background="{arc:ArcDesignTokenResource Surface}"
            BorderBrush="{arc:ArcDesignTokenResource Border}">
        <arc:Timeline Label="Incident INC-204"
                      Now="2026-10-20T12:00:00+00:00">
            <arc:TimelineEvent Id="rollback"
                               At="2026-10-20T11:58:00+00:00"
                               Actor="Theo Walsh"
                               Title="rolled back api to 1.8.3"
                               Meta="Rollback #77"
                               Detail="Error rate returned to 0.2% within six minutes of the rollback." />
            <arc:TimelineEvent Id="healthy"
                               At="2026-10-20T11:55:00+00:00"
                               Title="Health checks pass in all three regions"
                               Meta="api · production"
                               Icon="{x:Static arc:ArcIcons.CircleCheck}"
                               Tone="Success" />
            <arc:TimelineEvent Id="failed"
                               At="2026-10-20T11:42:00+00:00"
                               Title="Deploy failed: health check timed out"
                               Meta="api · staging"
                               Detail="Retried twice. The failing check is tracked in INC-204."
                               Icon="{x:Static arc:ArcIcons.CircleX}"
                               Tone="Danger" />
            <arc:TimelineEvent Id="opened"
                               At="2026-10-20T11:44:00+00:00"
                               Actor="Ana Ruiz"
                               Title="opened INC-204 and paged on-call"
                               Meta="Severity 2" />
            <arc:TimelineEvent Id="closed"
                               At="2026-10-19T16:20:00+00:00"
                               Actor="Lee Chen"
                               Title="closed INC-198 after the cache fix"
                               Meta="Severity 3" />
        </arc:Timeline>
    </Border>
</UserControl>
```

场景中的事件保持内联写法，因为静态图片必须如此。在应用中它们来自 `ItemsSource`，时间标签随 `Now` 变化。

### 文案规范

- **标题**：用过去时的完整句子，动态流中执行者排在最前：“将 Checkout 重新设计合并到 main”。当事件由人引起时，执行者必须是人名，而不能是 “System”。
- **结果**：每个成功或危险事件的标题都要写明结果，使看不到颜色的读者仍然知道发生了什么。
- **Meta**：简短的引用，例如拉取请求、构建或严重级别：“PR #212”“Build #418”。
- **Detail**：一两句话，列出读者展开该行时需要的事实。当数字较长时，把它放在详情中，而不是标题中。
- **其他语言**：设置 `Culture` 以决定日期标题和钟点时间的格式。默认为 en-US。

| 不要写 | 改为 |
|---|---|
| 更新 | Ana Ruiz 将 web 部署到生产环境 |
| 错误！ | 部署失败：健康检查超时 |
| 系统：完成 | 数据库迁移已完成 |
| 点击查看更多 | 已重试两次。失败的检查记录在 INC-204 中。 |

### 最佳实践

**推荐**

- 用计时器设置 `Now`，并在动态流离开屏幕时停止计时器。相对时间只与 `Now` 一样新。
- 当受众共享同一时区（例如某个地区的运维团队）时设置 `TimeZone`。默认为 UTC，因此每位查看者看到的时钟都相同。
- 为每个事件设置稳定的 `Id`。展开状态、新鲜度提示以及新更新的播报都依赖它。
- 对较长的动态流使用 `MaxHeight`，使日期标签保持固定，读者不会失去位置。
- 在每个成功或危险事件的标题中写明结果。

**避免**

- 为添加一个事件而替换整个集合。应添加到集合中，使新行滑入。
- 在普通的人员事件上设置 `Tone`。色调用于标记系统结果，而人员的行已经有头像。
- 把 `Detail` 用于事件的主要内容。行首先显示标题，而包含完整故事的详情要等读者展开后才可见。
- 在放于彩色面板上的动态流中保留 `HeaderBackground` 的默认值。这样日期标签会以错误的颜色遮住各行。
- 用时间线展示读者需要比较的记录。排序与筛选应使用 `Table`。

### 常见问题

**为什么相对时间不变化？** 只有 `Now` 变化时时间标签才会更新。请约每分钟推进一次 `Now`，计时器需在动态流显示期间运行。

**为什么我的行不能展开？** 只有 `Detail` 有内容时，行才会展开。空字符串不算内容，因此该行只是普通文字，也不在 Tab 顺序中。

**能否让最旧的事件排在最前？** 不能。动态流总是把最新的更新排在最前，并按日期分组。如果需要另一种顺序，请在另一个视图中自行排序事件。

**如何通过代码打开某一行？** 调用 `Expand(id)`、`Collapse(id)` 或 `Toggle(id)`。每次调用都会引发 `ExpandedChanged`；`IsExpanded(id)` 可告诉你该行是否已展开。

## API 参考

<!-- api: Timeline -->

<!-- api: TimelineEvent -->

## 无障碍

- 动态流是一个带名称的区域。`Label` 是它的无障碍名称，例如 “Incident INC-204”。
- 每个日期标签都是标题，级别为 `HeadingLevel`（默认为 3，范围 2 到 6）。其无障碍名称包含更新数，例如 “Friday, October 16, 1 update”。每个时间都有隐藏的完整日期文字。
- 可展开的行会公布其展开状态。没有详情的行是普通文字，不在 Tab 顺序中。
- 新事件会以 “New update: ...” 在实时区域中播报，且不会移动焦点。
- 色调即颜色。标题必须说明结果，这是文案规范的要求。

### 键盘

| 按键 | 操作 |
|---|---|
| `Down` / `Up` | 移到下一个或上一个带详情的行 |
| `Home` / `End` | 移到第一个或最后一个带详情的行 |
| `Enter` 或 `Space` | 展开或收起当前行 |

带详情的行会以 Tab 顺序获得焦点；没有详情的行则不会。方向键会跳过无法展开的行。

## 动效

- 行进入视口时绘制连接线，标记随之弹出。
- 新事件从顶部滑入，下方的行随之下移。展开的详情以弹簧方式打开。
- 减弱动效时，移动改为短暂淡入，高度立即变化。关闭动效时直接到最终状态。

| 变化 | 完整 | 减弱 | 关闭 |
|---|---|---|---|
| 行进入视口 | 连接线绘制，标记弹出 | 短暂淡入 | 立即完成 |
| 新事件到达 | 从顶部滑入，下方的行下移 | 短暂淡入 | 立即完成 |
| 详情展开或收起 | 详情以弹簧方式展开 | 高度立即变化 | 立即完成 |
| 滚动时的日期标签 | 固定在动态流顶部 | 固定在顶部 | 固定在顶部 |

## 相关组件

- `CommentThread` 用于针对单个对象的回复，`ChatThread` 用于实时的双向对话。
- `Table` 用于读者需要排序或比较记录的场景。
- `Stepper` 用于有当前位置的固定步骤。

| 组件 | 适用场景 |
|---|---|
| `CommentThread` | 回复挂在某个对象上，并且可以标记为已解决 |
| `ChatThread` | 人们轮流对话，并有输入中提示与已读回执 |
| `Table` | 读者需要排序或比较记录 |
| `Stepper` | 步骤固定，且其中一项是当前步骤 |
| `Alert` | 单个当前状态应放在某一条目旁边 |
