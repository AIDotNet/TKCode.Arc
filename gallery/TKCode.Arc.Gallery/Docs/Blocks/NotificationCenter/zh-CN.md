---
title: 通知中心
summary: 集中查看通知，支持已读状态、分组信息和带动画的展开与收起。
source-hash: 25d3d3b869e5
---

<Demo name="Preview" />

## 何时使用

- 通知需要一个地方暂存，等人们有空时查看：提及、已完成的任务、审批和账户警告。
- 人们需要将通知标为已读或未读、打开其中一条的详情，或清除旧通知。
- 应用页头有空间放一个图标按钮，并能显示未读通知的数量。

## 何时不用

- 无需后续操作的简短确认，例如“已保存”：使用 `Toast`。
- 当前页面上必须立即让人看到的问题：使用 `Alert`。
- 带搜索和筛选的完整历史记录：使用 `Timeline` 或表格搭建页面。

## 用法

添加 `NotificationItem` 子元素，或设置 `ItemsSource`。每个项都有 `Title`、可选的 `Description`、`Time` 时间文字和 `IsRead` 状态。来自某个人的通知会设置 `ActorName` 与 `ActorSource`，并显示其头像。系统通知会显示其 `Tone` 对应的图标：Info、Success 或 Warning。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:NotificationCenter>
        <arc:NotificationItem Id="deploy" Title="部署已完成" Description="版本 2.4.1 已上线。" Time="18 分钟前" Tone="Success" />
        <arc:NotificationItem Id="mention" Title="Mia Chen 提到了你" Time="2 分钟前" ActorName="Mia Chen" />
        <arc:NotificationItem Id="digest" Title="你的每周摘要已就绪" Time="昨天" IsRead="True" />
    </arc:NotificationCenter>
</UserControl>
```

铃铛会显示未读数量，最多显示到“9+”。点击铃铛即可打开面板。点击条目可显示其详情，并提供“标为已读”和“关闭”按钮。同一时间只会展开一个条目。

### 已读状态与你的数据

控件会更改项上的 `IsRead`，并引发 `ReadChanged`。“全部标为已读”会为它标记的每个项各引发一次。“关闭”（移除通知）会将项从 `Items` 中移除；若 `ItemsSource` 是你可以修改的列表，则从 `ItemsSource` 中移除，并引发 `Dismissed`。“清除已读”会为每个已读项各引发一次。请在这些处理程序中将更改保存到你的数据存储中。当存储发生变化时，铃铛与面板会跟随项和集合更新。

<Demo name="ReadState" />

```csharp
center.ItemsSource = store;   // 一个 ObservableCollection<NotificationItem>
center.ReadChanged += (_, e) => api.SetRead(e.Item.Id, e.IsRead);
center.Dismissed += (_, e) => api.Dismiss(e.Item.Id);
```

只读数据源会保留其项，控件则会隐藏已关闭的项。

### 未读视图与空状态

面板会列出所有通知，或只列出未读通知。`View` 是双向的。列表为空时，面板会显示“暂无通知”或“没有未读通知”。存在已读通知时，会出现一个链接，用于返回“全部”视图。

<Demo name="UnreadView" />

### 在应用页头中

面板在铃铛下方 12 像素处打开，并与窗口边缘保持 12 像素的距离，因此铃铛可以位于页头的边缘。未读徽标会超出铃铛右上角 5 像素，因此在会裁剪内容的容器中，请在铃铛周围预留同样的 5 像素空间。窗口宽度为 380 像素或更窄时，面板会使用更紧凑的间距，“全部标为已读”只显示图标。

<Demo name="AppHeader" />

### 文字

`Label` 为面板和铃铛命名。`Labels` 保存其余所有文字，默认为英文。给 `Labels` 赋一个新的 `NotificationCenterLabels` 即可修改它们。在 `ReadCount` 中，`{0}` 是会滚动变化的数字。带数量的文字（例如标题下方的摘要）是可在代码中设置的函数。

<Demo name="CustomWording" />

```csharp
center.Labels = new NotificationCenterLabels
{
    Summary = unread => unread > 0 ? $"{unread} 条新通知" : "暂无新通知",
    Trigger = (label, unread) => $"{label}，{unread} 条新通知"
};
```

### 从代码打开

`IsOpen` 是双向的，`Open()` 与 `Close()` 的作用与之相同。`MarkAllRead()` 与 `ClearRead()` 会执行批量操作，并引发与按钮相同的事件。

```csharp
center.Open();
center.MarkAllRead();
```

## API 参考

<!-- api: NotificationCenter -->

<!-- api: NotificationItem -->

## 无障碍

- 铃铛是一个按钮，其无障碍名称由 `Label` 与未读数量组成，例如“通知，3 条未读”。它会报告面板是否已展开。
- 打开面板时，焦点会移到其关闭按钮。Tab 依次经过关闭按钮、“全部”和“未读”、“全部标为已读”、各条目、已展开条目的按钮以及“清除已读”，并在面板内循环。
- Escape 与关闭按钮会关闭面板，并将焦点返回铃铛。
- “全部”和“未读”是切换按钮。每个条目是一个按钮，其无障碍名称包含标题与状态，例如“部署已完成，未读。显示详情”，并会报告它是否已展开。
- 在“未读”视图中将条目标为已读，或关闭某个条目后，焦点会依次移到下一个条目、上一个条目，然后是“未读”按钮。执行“全部标为已读”后，焦点会返回当前视图的按钮。
- 标题下方的摘要是礼貌式实时区域，因此新的数量会被朗读出来。

## 动效

- 面板从铃铛处展开：开始时向铃铛方向偏移 6 像素，缩放为 0.97，并在 240 毫秒内淡入。关闭时用 120 毫秒离开，移动距离减半，缩放为 0.98。
- 面板打开时铃铛会微微倾斜。徽标以弹跳方式出现与消失，其数字会向上或向下滚动。
- 面板打开或视图新增条目时，条目会从下方 4 像素处滑入就位，每个相隔 35 毫秒。离开的条目会淡出，并以平滑的弹簧动画收起高度。执行批量操作后，条目会依次离开，与第一个条目的间隔最多为 200 毫秒。
- 数量会滚动，摘要会向上滑入新文字，“全部”与“未读”的高亮会在两个按钮之间滑动。详情、页脚和空状态会以相同的弹簧展开或收起高度。
- 动效等级为 Reduced 时，只保留 120 毫秒的短淡入淡出，没有位移、缩放或模糊。面板会立即显示或隐藏。动效等级为 Off 时，所有变化立即生效。

## 相关组件

- `Toast`：用于会自行消失的简短消息。
- `Popover`：用于锚定在按钮上的其他内容。
- `Badge`：用于在其他控件上显示数量或状态。
