---
title: 评论线程
summary: 支持回复、表情反应、提及、原位编辑与解决的嵌套评论。
source-hash: 775965f7a0c4
---

<Demo name="DesignReview" />

## 何时使用

- 针对文档、设计稿、工单或拉取请求的评审讨论。
- 附着在某个对象上的反馈，回复嵌套在它所回应的评论之下。
- 问题确认后，可由某人标记为已解决的线程。

## 何时不用

- 双方同时在线的实时对话：用 `ChatThread`。
- 跨多个对象的事件历史：用 `Timeline`。
- 只需要带提及的输入、没有线程：用 `MentionInput`。

## 用法

将 `CurrentUser` 设为阅读线程的人。他们可以编辑和删除自己的评论。`Comments` 是评论树，回复嵌套在 `Replies` 之下。

```csharp
var ana = new CommentAuthor("ana", "Ana Ruiz");
var mina = new CommentAuthor("mina", "Mina Park");

var thread = new CommentThread
{
    Title = "首页标题",
    CurrentUser = ana,
    Comments =
    [
        new ThreadComment("c1", mina, "新标题读起来很顺。@Theo Walsh 能把副标题再精简一下吗？", "2h")
        {
            Replies =
            [
                new ThreadComment("c2", new CommentAuthor("theo", "Theo Walsh"), "好的，精简成一行。", "1h")
            ]
        }
    ]
};
```

```xml
<arc:CommentThread Title="价格表间距"
                   Placeholder="回复此线程"
                   CurrentUser="{Binding CurrentUser}"
                   Comments="{Binding Comments}"
                   IsResolved="{Binding IsResolved}" />
```

### 提及与人员

在编辑框中输入 `@` 即可提示人员。`People` 列出可被提及的人。未设置时，将提供线程中的参与者。

<Demo name="EmptyThread" />

`Reactions` 设置每条评论可用的表情。`MaxDepth` 限制嵌套层级：超出的回复会挂到允许的最深父级之下。

### 解决与重新打开

解决线程后，它会折叠为一个标签，并保留参与者的信息。`IsResolved` 为双向绑定。

<Demo name="Resolved" />

```csharp
thread.ResolvedChanged += (_, e) => Status.Text = e.IsResolved ? "已解决" : "已重新打开";

thread.Resolve();
thread.Reopen();
```

### 变更与持久化

`CommentsChanged` 会将每次变更报告为一个 `CommentThreadChange`，其 `Kind` 为 `Reply`、`Edit`、`Delete` 或 `React`。请将该变更保存到服务器，而不是比较整棵评论树。服务器还必须校验人员只能编辑和删除自己的评论。若要拒绝某次变更，请在处理程序中将 `Comments` 设回之前的值。

```csharp
thread.CommentsChanged += (_, e) =>
{
    if (e.Change.Kind == CommentThreadChangeKind.Edit)
    {
        // 为 e.Change.Id 保存 e.Change.Body。
    }
};
```

### 窄宽度

宽度小于 420px 时，输入框不再显示头像，回复缩进 12px。

<Demo name="Narrow" />

## API 参考

<!-- api: CommentThread -->

<!-- api: ThreadComment -->

## 无障碍

- 编辑框是一个组合框：其提及建议是一个带有当前项的列表。每条评论是一个带名称的分组，包含作者与时间。已删除的评论会读作“已删除的评论”。
- 表情标签是切换按钮。用于折叠回复的按钮会公开其展开状态。
- 仅有图标的操作（如编辑、删除和回复）都有明确的名称。
- Ctrl 或 Cmd + Enter 发送。上下方向键在建议项之间移动，Enter 或 Tab 插入提及，Escape 会先关闭建议列表，再取消编辑或回复。

## 动效

- 展开回复、添加评论以及折叠已解决的线程时，线程高度以弹簧方式变化。
- 表情计数滚动到新值；他人添加或移除表情时，标签随之移动。
- 编辑表单、删除确认和表情选择器会以短距离滑入。
- 减弱动效时，移动改为原位变化，并关闭过渡。关闭动效时直接到最终状态。

## 相关组件

- `ChatThread`：双方同时在线的实时对话。
- `Timeline`：跨多个对象的活动动态。
- `MentionInput`：只有带提及建议的单个输入，没有线程。
