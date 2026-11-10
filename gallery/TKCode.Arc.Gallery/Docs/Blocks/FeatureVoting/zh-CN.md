---
title: Feature voting
summary: 一张小卡片，用来对几个候选功能投票：选中一项时，它的票数会滚动，占比条随之移动；提交后卡片变成感谢页并显示结果。
source-hash: 1e2b391eed9f
---

<Demo name="Preview" />

## 何时使用

`FeatureVoting` 是一张只问一个问题的小卡片，例如“下一个做什么？”，下面列出三到六个候选功能。访客选中一项（或最多 `MaxVotes` 项），马上看到自己的一票加进了票数，再按“投票”提交。之后卡片会表示感谢，保留结果，并提供“修改我的投票”。它适合放在更新日志末尾、应用内公告、设置页或侧栏这类放不下完整反馈看板的地方。

- **快速投票**：让已登录的用户决定下一个功能。
- **排优先级**：每人可以选两到三项（`MaxVotes`）。
- **先投后看**：投票前不显示票数（`ShowsResultsBeforeVoting="False"`）。

## 何时不用

- **开放的想法或很长的列表**：使用 `FeedbackBoard`，它有状态、评论和提交新请求的表单。
- **评分或满意度**：滑块或评分控件问的是另一类问题。
- **需要防作弊的公开匿名投票**：卡片只收集选择，计票由你的服务器负责。

## 用法

```xml
<arc:FeatureVoting Title="What should we build next?" x:Name="Poll" MaxVotes="1">
    <arc:FeatureVotingOption Id="offline" Title="Offline mode" Description="Work without a connection." Votes="412" />
    <arc:FeatureVotingOption Id="calendar" Title="Calendar view" Votes="296" />
</arc:FeatureVoting>
```

```csharp
Poll.Voted += (_, e) =>
{
    var ids = e.Options.Select(o => o.Id);   // send the choice to your server
};
```

没有选项时卡片显示一组示例。`Votes` 是其他人的票数；选中一项时，卡片会在其上加上当前访客的一票，`TotalVotes` 与 `SelectedOptions` 随之更新。直接修改 `Votes` 即可显示实时票数。

### 多选与隐藏结果

<Demo name="PickTwo" />

`MaxVotes` 大于 1 时，说明下方的提示会显示已选数量；超过上限的选择会被拒绝，并读出提示。`ShowsResultsBeforeVoting="False"` 会在提交前隐藏票数和占比条。

### 用代码提交

`Toggle(option)` 选中或取消一项，`Submit()` 提交投票（没有选择时不做任何事），`ChangeVote()` 重新打开选择。`IsSubmitted` 是双向的，页面可以为已经投过票的人直接显示感谢页。

## API 参考

<!-- api: FeatureVoting -->

<!-- api: FeatureVotingOption -->

<!-- api: FeatureVotingVotedEventArgs -->

## 无障碍

- 选项是一个以标题命名的组。每个选项是一个切换按钮，名称会读出标题、票数、占比，选中时还会读出“你的投票”。
- 选择、被拒绝的选择和感谢页都会礼貌地读出。

### 键盘

| 按键 | 作用 |
|---|---|
| `Tab` | 移到选项，再移到按钮。 |
| `↑` / `↓`、`Home` / `End` | 在选项之间移动。 |
| `Enter` / `Space` | 选中或取消当前选项。 |
| `Ctrl+Enter` | 提交投票。 |

## 动效

- 选中一项时，票数逐位滚动，每个占比条以平滑弹簧移到新长度；滑动中途收到服务器的新票数会保留速度。
- 勾号弹入被选中的选项；标题区和按钮以短距离上移切换，感谢页的勾号会自己画出来。
- 减少动效时，占比条和数字直接到位，只保留短淡入淡出；关闭动效时一切立即变化。

## 响应式

- 卡片最宽 420 px（`CardMaxWidth`），更窄时随容器伸缩。标题和说明会换行，票数始终在右侧。
- 卡片没有自己的断点；在窄栏中请把说明控制在一行。

## 相关组件

- `FeedbackBoard`：带状态和评论的完整请求看板。
- `Reactions`：内容下方的轻量表情反馈。
