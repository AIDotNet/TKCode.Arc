---
title: Activity feed
summary: 工作区里发生了什么，按天分组：谁做了什么，每类动作有自己的图标，可按类型和人员筛选；新动态在实时模式下滑入，暂停时在提示条后面等待。
source-hash: 8d85db799894
---

<Demo name="Preview" />

## 何时使用

`ActivityFeed` 显示工作区里发生的事：谁评论、修改、发布或加入，最新的在前，按天分组。标签按类型筛选，选择框按人员筛选。实时模式下，新动态一发生就从顶部滑入；暂停时，它们在“新动态”提示条后面等待，列表不会在读者眼前移动。

- **项目或工作区首页**：在内容旁边显示最新动态。
- **审计视图**：只看某个人或某类变化。
- **发布日志**：构建和发布实时到达。

## 何时不用

- **需要处理的个人收件箱**：使用 `NotificationCenter` 或 `InboxTriage`。
- **聊天**：使用 `ChatThread`。
- **需要搜索的数万条事件**：使用带筛选和分页的 `Table`。

## 用法

```xml
<arc:ActivityFeed x:Name="Feed" Title="Workspace activity" KindFilter="Deploy" />
```

```csharp
Feed.Push(new ActivityFeedItem("e42", DateTimeOffset.Now, "Rosa Lind", "commented on Launch plan", ActivityKind.Comment)
{
    Target = "Spring release",
    Detail = "Can we move the screenshots first?",
});
```

`Items` 保存全部动态，顺序不限。`Push` 在事情发生时加入一项：`IsLive` 时直接进入列表（如果通过筛选）；暂停时它会等待，`PendingCount` 增加，点击提示条或调用 `ShowPending()` 后才加入。每一项有执行人（系统事件为 null）、动作、对象说明、决定图标和筛选标签的类型（`Comment`、`Change`、`Deploy`、`Member`、`Other`）、可选头像、表示结果的 `Tone`，以及展开后显示的可选 `Detail`。首次加载时没有动态，会显示一个示例工作区。

### 已筛选并暂停

<Demo name="Comments" />

`KindFilter` 和 `ActorFilter` 是双向的，标签和人员选择框会设置它们。

## API 参考

<!-- api: ActivityFeed -->

<!-- api: ActivityFeedItem -->

## 无障碍

- 动态是一个以标题命名的 `Timeline` 区域；日期是标题，带详情的行可用 `Enter` 或 `Space` 展开。
- 实时标签会说明是否在实时更新，以及按下后会怎样。暂停时到达的新动态会读出（“3 条新动态”）；发布失败这类结果写在动作文字里，而不只靠颜色。
- 提示条和标签都能用 `Tab` 到达；在时间线中用 `↑` / `↓` 在可展开的行之间移动。

## 动效

- 新动态从顶部滑入，下方的行随之下移；行进入视野时，标记之间的线会画出来。
- 实时圆点每 1.6 秒向外发出一圈柔和的光环。它运行在按帧循环上，离开屏幕、被隐藏或减少动效时会停止。
- “新动态”提示条从上方落下。减少动效时只用淡入淡出；关闭动效时变化立即发生。

## 响应式

- 动态最宽 640 px，时间线超过 560 px（`FeedMaxHeight`）后滚动。
- 在窄栏中，标签和人员选择框会换成两行；时间线在自身较窄时会收紧排版。

## 相关组件

- `Timeline`：单独的按天分组列表。
- `NotificationCenter`：带已读状态的个人更新。
- `InboxTriage`：每项都需要决定的事项。
