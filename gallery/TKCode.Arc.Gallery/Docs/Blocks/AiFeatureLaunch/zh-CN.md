---
title: AI feature launch
summary: 在产品内宣布一项新的 AI 功能：一段可以暂停的循环演示、功能说明、一个试用按钮，以及会记住关闭原因的关闭操作。
source-hash: b1798cb94f14
---

<Demo name="Preview" />

## 何时使用

`AiFeatureLaunch` 在产品内告诉人们一项新的 AI 功能已经可用，并邀请他们试一试。顶部的一段短循环演示展示功能的效果，"New" 标签和功能名称引出正文，一两句话说明它能做什么，几条要点说明可以期待什么，试用按钮让人立即开始。还没准备好的人可以点 "Maybe later" 或关闭按钮；`Dismissed` 会报告是哪一种，方便应用决定是否再次显示。

- **发布新功能。** 在功能所在的页面显示一次：讨论串里的总结按钮、编辑器里的写作助手。
- **让不起眼的功能被看见。** 功能已经存在但很少有人发现时，在它能帮上忙的地方显示。
- **就地的更新说明。** 每个功能一张卡片，代替没人读的长列表。

## 何时不用

- **同时介绍多个功能。** 只展示最有用的一个；轮播式的发布很容易被忽略。
- **警告和故障。** 使用 `Alert` 或 `AnnouncementBar`；这张卡片是邀请，不是通知。
- **首次使用的设置。** 使用分步流程，例如 `AI setup wizard` 或 `Multi-step form`。

## 用法

本区块派生自 Avalonia 的 `TemplatedControl`。不设置任何数据时显示一份虚构的示例（`AiFeatureLaunchSamples`）。

```xml
<arc:AiFeatureLaunch Title="Thread summaries"
                     Description="Catch up on any long thread in seconds."
                     TryCommand="{Binding OpenSummaries}" />
```

```csharp
launch.Highlights = ["Decisions, owners and open questions", "Links back to every message"];
launch.Dismissed += (_, e) => settings.MarkSeen("thread-summaries", e.Reason);
```

### 结构

- **演示**：内置的循环动画（一页消息行被一条柔和的光带从上到下读过，变成三条要点的总结），或放在 `Demo` 中的你自己的内容（图片、视频、任意控件）。`DemoDescription` 是读屏软件在此处读出的内容。`IsDemoVisible` 可隐藏这一区域。
- **暂停与关闭**：演示角上的暂停按钮切换 `IsDemoPlaying`；关闭按钮以原因 `Closed` 关闭。
- **文字**："New" 标签、`Title`、`Description` 和 `Highlights`。
- **按钮**："Maybe later"（原因 `Later`，用 `IsLaterVisible="False"` 隐藏）和试用按钮（`TryRequested`、`TryCommand`；开启 `DismissOnTry` 时也会以原因 `Tried` 关闭）。

### 记住回答

`IsOpen`（双向）表示卡片是否显示。`Dismiss(reason)` 从代码关闭它，`Show()` 让它重新出现。直接设置 `IsOpen` 关闭时不会触发 `Dismissed`。

<Demo name="Remembered" />

### 文字

`Labels`（`AiFeatureLaunchLabels`）包含标签、按钮文字，以及暂停、播放、关闭按钮和要点列表的读屏名称。

## API 参考

<!-- api: AiFeatureLaunch -->

<!-- api: AiFeatureLaunchDismissReason -->

<!-- api: AiFeatureLaunchLabels -->

## 无障碍

- 卡片是一个有名称的区域；标题是二级标题。要点组成一个有名称的列表。
- 演示是一张以 `DemoDescription` 命名的图像；其中的绘制内容对读屏软件隐藏。
- 暂停按钮按状态命名为 "Pause the demo" 或 "Play the demo"；关闭按钮命名为 "Dismiss"。
- 在卡片内任意位置按 **Escape** 都会关闭它（原因 `Closed`）。**Tab** 按阅读顺序到达暂停、关闭、稍后和试用按钮。

## 动效

- 演示每 6.4 秒循环一次：文字行逐行写出，一条柔和的光带从上到下读过，总结卡片升起，要点逐条变长，然后画面淡出并重新开始。它只在完整动效下、可见且在视口内时运行，每帧不分配内存。
- 暂停时演示停在当前画面。减弱动效或关闭动效时，显示完成后的画面，从不移动。
- 关闭时卡片在淡出的同时略微缩小，高度随之收起；再次显示时反向进行。减弱动效只保留淡出；关闭动效时立即关闭。

## 响应式

- 卡片最宽 420，在更窄的容器中占满宽度。宽度不超过 360 时（`:compact`）正文内边距收紧，两个按钮改为全宽上下排列，试用按钮在上。
- 演示保持高度（184），按得到的宽度绘制画面；需要时标题换到标签下方。
- 没有任何功能依赖悬停。

## 相关组件

- `AnnouncementBar`，用于横跨页面的一行通知。
- `Card` 和 `Badge`，组成这张卡片的部件。
- `AI setup wizard`，用于为整个工作区开启功能。
