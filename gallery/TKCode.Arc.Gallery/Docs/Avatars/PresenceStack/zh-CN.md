---
title: Presence stack
summary: 以重叠的头像显示此刻正在查看页面的人，每人带有自己的颜色；新来的人在最前面放大出现，离开的人缩小消失，其余的人收进 +N 标签。
source-hash: 5cd3b713c88e
---

<Demo name="LiveViewers" />

## 何时使用

`PresenceStack` 显示此刻还有谁打开了这个文档、看板或页面。每位查看者是一个头像，外圈是他自己的颜色（例如他的光标颜色），与下一个头像重叠，
最近到达的人在最前面。它跟随一个实时列表：新来的人在最前面放大出现，并向外发出一圈光环，其余的人向旁边滑开；这一排满了时，最后一个可见的人
滑进 "+N" 标签；离开的人缩小消失，空位随之合拢。暂时离开但仍在连接中的人会变淡。一个滚动的计数说明有多少人在查看，点击标签可以打开完整列表。

- **协作编辑器**：共享文档或设计文件标题栏中的头像。
- **仪表板与工单**：“此刻还有谁在看这个故障”。
- **实时会话**：演示或会议页面的观众。

## 何时不用

- **固定的团队或负责人列表**：使用 `AvatarGroup`，它显示的是成员，而不是谁在线。
- **成百上千的查看者**：改为显示一个数字；这组头像是给用户想要认出的那几个人用的。
- **每个人具体在哪里**：文档中的光标和选区高亮会说明这一点；这组头像只说明谁在这里。

## 用法

按从早到晚的顺序添加 `PresenceStackViewer` 项，也就是把新来的人加在末尾，并移除离开的人；头像组会把最新的人显示在最前面
（`IsNewestFirst="False"` 保持列表原有顺序）。

```xml
<arc:PresenceStack MaxVisibleCount="3">
    <arc:PresenceStackViewer Name="Iris Mendel" Detail="Reading" />
    <arc:PresenceStackViewer Name="Jon Kale" IsIdle="True" />
    <arc:PresenceStackViewer Name="Kim Arden" Detail="Editing" />
    <arc:PresenceStackViewer Name="Lou Prentiss" />
</arc:PresenceStack>
```

对于实时列表，把 `ItemsSource` 绑定到可观察集合。同时发生的变化（几个人一起加入）会一起移动。

```csharp
viewers.Add(new PresenceStackViewer("Ada Brook", detail: "Editing the intro"));
viewers.Remove(leaving);
```

查看者的 `Name` 提供姓名首字母、工具提示和无障碍名称；`Source` 设置一张图片；`Detail` 添加一句说明（"Editing slide 3"）；`IsIdle` 让头像变淡，
外圈变成灰色。没有设置 `Brush` 时，外圈会根据名字选取一种图表系列颜色，因此同一个人在不同会话中保持相同的颜色。

### 尺寸与数量

`Size` 为 `Small`（28 px 头像，默认）或 `Medium`（36 px）。`MaxVisibleCount`（默认 4）是显示标签前最多显示的头像数。`IsCountVisible` 和
`CountLabel` 控制这一排后面的 "4 viewing" 文字；`ViewerCount` 报告人数。

<Demo name="Sizes" />

### 完整列表

标签会打开一个列出所有查看者的列表，带有他们的说明或 "Idle"。`IsListOpen`（双向）、`OpenList()` 和 `CloseList()` 可以在代码中控制它；
按 Escape 或点击外部会关闭它。

## API 参考

<!-- api: PresenceStack -->

<!-- api: PresenceStackViewer -->

## 无障碍

- 头像组是一个名为 "Viewing now, 4 viewing" 的列表（`Label` 和 `CountLabel`）。这一排是一个 Tab 停靠点：方向键在各个人和标签之间移动，
  Home 和 End 跳到两端。每个人的名字、离开状态和说明就是他的无障碍名称和工具提示。
- 标签是一个名为 "2 more viewers" 的按钮；Enter 或空格打开列表，Escape 关闭列表并把焦点放回原处。
- 加入和离开会以礼貌方式播报（"Ada Brook joined"、"Ben Ortiz left"）；如果页面已经会播报这些，设置 `AnnouncesChanges="False"`。
- 离开状态用文字说出，而不只靠变淡表示。

## 动效

| 变化 | 完整 | 减弱 | 关闭 |
|---|---|---|---|
| 有人加入 | 在最前面放大出现，向外发出一圈光环，其余的人滑开 | 淡入，其余的人立即移动 | 立即 |
| 有人离开 | 在原位置缩小消失，空位合拢 | 淡出，空位立即合拢 | 立即 |
| 这一排已满 | 最后一个人滑进标签并淡出 | 淡出 | 立即 |
| 计数和标签 | 数字滚动 | 立即改变 | 立即改变 |
| 这一排的宽度 | 弹簧动画 | 立即 | 立即 |

## 响应式

- 头像组的宽度等于头像和计数的宽度；它不会拉伸。在较窄的标题栏中调低 `MaxVisibleCount`。
- 工具提示需要指针；在触摸设备上，标签打开的列表会显示每个名字和说明。
- 小头像为 28 px，小于 44 px 的触摸尺寸；每个头像仍然可以获得焦点，列表也只需轻点一下就能打开。

## 相关组件

| 组件 | 何时选择 |
|---|---|
| `AvatarGroup` | 显示团队成员，而不是此刻谁在这里 |
| `Avatar` | 显示一个人 |
| `LiveVisitors` | 在地图上统计匿名访客 |
