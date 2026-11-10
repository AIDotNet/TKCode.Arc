---
title: Catch up
summary: 一份“你不在时”的总结：一段文字回顾，带有人物、地点和时间的要闻、提及与决定，按分组筛选，并可逐条或一次全部标为已读。
source-hash: dddd2473b6a6
---

<Demo name="Preview" />

## 何时使用

`CatchUp` 迎接度过周末或假期后回来的人。一段简短的文字回顾用两三句话说明发生了什么变化。下面的条目分为要闻（值得知道的事）、提及（有人找他）和决定（某件事已经定下），每条都写明是谁、在哪里、什么时候，并有未读圆点。筛选器一次显示一个分组及其未读数量。打开某条会把它的链接交给应用并标为已读；每条旁边的小按钮可以标为已读或未读；"Mark all as read" 清空全部，回顾随之变为 "You are all caught up."

- **回到工作区。** 离开一段时间后看到的第一个画面，由你的总结服务写成。
- **每日摘要。** 用同一张卡片展示过去 24 小时。
- **频道或项目回顾。** 把条目限定在一个地方。

## 何时不用

- **实时通知。** 持续更新的信息流请使用 `Notification center` 区块。
- **完整的活动记录。** 每个事件都重要时，使用 `Timeline`，而不只是挑出重要的事。
- **单条更新。** 使用 `Toast` 或 `Alert`。

## 用法

本区块派生自 Avalonia 的 `TemplatedControl`。不设置任何数据时显示一个虚构的周末（`CatchUpSamples`）。

```xml
<arc:CatchUp Since="Since Friday, 6:10 PM" Summary="{Binding Recap}" Items="{Binding Changes}" />
```

```csharp
catchUp.Items =
[
    new() { Id = "m1", Kind = CatchUpKind.Mention, Title = "Priya asked you to review the pricing page",
            Author = "Priya Raman", Source = "Pricing refresh", Time = "Sat 3:02 PM", Link = "doc:pricing#c12" },
];
catchUp.ItemInvoked += (_, e) => Navigate(e.Item.Link);
catchUp.ItemsRead += (_, e) => store.SaveRead(e.Items.Select(i => i.Id), e.IsRead);
```

### 结构

- **页头**：标题、带未读数量的 `Since`，以及 "Mark all as read"。
- **回顾**：`Summary` 和旁边的闪光图标；全部已读后，变为带双对勾的 "You are all caught up."。
- **筛选器**：All、Highlights、Mentions 和 Decisions，各带未读数量（`Filter`，双向）。
- **条目**：按 `CatchUpItem.Kind` 依次分组；每条显示作者的生成头像（或分组图标）、标题、详情、地点和时间以及未读圆点；旁边的按钮切换已读状态。

已读状态按 `CatchUpItem.Id` 保存；条目上的 `IsRead` 设定初始值。`IsRead(item)`、`SetRead(item, value)`、`MarkAllRead()` 和 `Open(item)` 从代码完成按钮所做的事；`UnreadCount` 随之更新。设置 `MarkReadOnOpen="False"` 时，打开条目不会将其标为已读。

### 打开条目

<Demo name="Opened" />

### 文字

`Labels`（`CatchUpLabels`）包含标题、分组名称、筛选器格式、全部已读和为空时的提示，以及已读按钮的读屏名称。

## API 参考

<!-- api: CatchUp -->

<!-- api: CatchUpItem -->

<!-- api: CatchUpKind -->

<!-- api: CatchUpFilter -->

<!-- api: CatchUpLabels -->

## 无障碍

- 卡片是一个有名称的区域；标题是二级标题，每个分组名称是三级标题。
- 每个条目都是一个按钮，以标题命名，未读时加上 "unread"，并以详情、作者、地点和时间作为说明。**Up** 和 **Down** 在条目之间移动，**Home** 和 **End** 跳到两端，**Enter** 或 **Space** 打开。
- 每条旁边的已读按钮命名为 "Mark "…" as read" 或 "as unread"。
- 筛选器是可用方向键操作的分段控件；"Mark all as read" 之后会播报 "You are all caught up."。

## 动效

- 回顾以上升方式切换为全部已读的提示（旧内容 100 ms 淡出，新内容 160 ms 淡入并上升 4 px），高度随平滑弹簧变化。
- 条目的未读圆点淡出，文字在 240 ms 内淡到已读的强度；行在悬停时于 160 ms 内变色。
- 减弱动效时回顾只交叉淡化不上升；关闭动效时所有变化立即生效。

## 响应式

- 卡片最宽 600。宽度不超过 440 时（`:compact`）内边距从 20 收紧到 16，"Mark all as read" 移到标题下方。
- 标题会换行；详情最多显示两行并以省略号结尾；地点和时间以省略号截断。
- 已读按钮始终可见，因此没有任何功能依赖悬停。

## 相关组件

- `Notification center`，用于实时更新的信息流。
- `Timeline` 和 `Comment thread`，用于完整的历史记录。
- `AI insight card`，用于说明一个指标的变化。
