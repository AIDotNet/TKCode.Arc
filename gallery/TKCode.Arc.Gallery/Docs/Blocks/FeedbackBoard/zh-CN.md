---
title: Feedback board
summary: 一个公开的功能请求看板：按状态筛选，按热门、最新或趋势排序，投票时票数滚动，打开请求在评论中讨论，也可以提出新的请求。
source-hash: bd175dd43029
---

<Demo name="Preview" />

## 何时使用

`FeedbackBoard` 是一个公开的功能请求看板。大家可以看到每个想法的进度（审核中、已计划、进行中、已完成），为想要的请求投票，打开请求阅读和讨论，并提出新的请求。列表可按票数、最新或趋势排序；投票或排序改变顺序时，各行会滑到新的位置。

- **产品反馈页**：放在应用里或网站上。
- **公开路线图**：只显示已计划和进行中的请求。
- **团队内部的想法看板**。

## 何时不用

- **在几个选项中快速投票**：使用 `FeatureVoting`。
- **带字段和附件的问题报告**：使用提交问题的表单。
- **一对一的私密支持**：使用 `SupportConversation`。

## 用法

```xml
<arc:FeedbackBoard x:Name="Board" Title="Product feedback" SortOrder="Top">
    <arc:FeedbackRequest Id="r1" Title="Dark mode for the mobile app" Votes="214" Status="Planned" />
</arc:FeedbackBoard>
```

```csharp
Board.VoteChanged += (_, e) => api.Vote(e.Request.Id, e.Request.HasVoted);
Board.RequestSubmitted += (_, e) => api.Create(e.Request.Title, e.Request.Description);
```

看板会直接修改 `FeedbackRequest` 对象：投票更新 `Votes` 和 `HasVoted`，评论替换 `Comments`（`CommentsChanged` 会给出具体变化），新请求以“审核中”加入，并带上作者的一票。请求计划或发布时，由你这边修改 `Status`。首次加载时没有请求，会显示一组示例。

### 路线图视图

<Demo name="Roadmap" />

`StatusFilter`、`SortOrder` 和 `SelectedRequest` 都是双向的。`CanSubmit="False"` 隐藏“提出功能建议”表单。在代码中，`ToggleVote(request)` 和 `Submit(title, description)` 与按钮的作用相同。

## API 参考

<!-- api: FeedbackBoard -->

<!-- api: FeedbackRequest -->

<!-- api: FeedbackRequestEventArgs -->

<!-- api: FeedbackStatus -->

<!-- api: FeedbackSort -->

## 无障碍

- 每一行是一个切换按钮，名称包括标题、状态、票数和评论数；其中的投票按钮会说明票数以及你是否已投票。
- 状态总是以文字写在标签上，不只靠颜色。投票和新请求会礼貌地读出。
- 表单的标题为空时会显示错误；`Escape` 关闭表单，`Ctrl+Enter` 提交。

### 键盘

| 按键 | 作用 |
|---|---|
| `Tab` | 依次移过标题区、筛选、各行及其投票按钮，以及打开的请求。 |
| `Enter` / `Space` | 打开请求、投票或按下标签。 |
| 表单中的 `Ctrl+Enter` | 提交请求。 |
| 表单中的 `Escape` | 关闭表单。 |

## 动效

- 投票或新的排序改变顺序时，各行以平滑弹簧从原位置滑到新位置；滑动中途再次变化会保留速度。
- 票数逐位滚动；表单以高度变化打开和关闭。
- 减少动效时各行直接到位，只保留淡入淡出；关闭动效时一切立即变化。

## 响应式

- 宽度在 760 px 及以上时，打开的请求显示在列表旁边（380 px，`DetailWidth`）。更窄时（`:stacked`）它取代列表，并带返回按钮。
- 在窄栏中，筛选和排序会分成两行。

## 相关组件

- `FeatureVoting`：对几个选项快速投票。
- `CommentThread`：单独的讨论。
- `HelpCenter`：提问前可以先看的答案。
