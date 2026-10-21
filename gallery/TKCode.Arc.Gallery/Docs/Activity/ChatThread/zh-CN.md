---
title: 聊天会话
summary: 带分组消息、表情回应、已读回执、输入状态以及带附件输入区的聊天会话。
source-hash: 1cd766b47d7a
---

<Demo name="SupportConversation" />

## 何时使用

- 客服小组件、私信或同时在线的团队聊天。
- 按人员和时间分组的消息，带日期分隔符与已读回执。
- 可以发送文字和文件的输入区。

## 何时不用

- 围绕单个对象的线程式评审，回复嵌套在评论之下：用 `CommentThread`。
- 跨多个对象的事件历史：用 `Timeline`。
- 只需多行输入、没有会话：用 `Textarea`，带附件的输入区用 `ChatComposer`。

## 用法

`Participants` 列出会话中的人员，`CurrentUserId` 标明其中哪一位是你。你发送的消息显示在右侧，使用强调色。消息按从旧到新的顺序排列。

```xml
<arc:ChatThread Label="客服会话"
                Participants="{Binding Participants}"
                CurrentUserId="me"
                ItemsSource="{Binding Messages}"
                Height="560" />
```

```csharp
var me = new ChatParticipant("me", "Alex Kim");
var sam = new ChatParticipant("sam", "Sam Rivera");

var messages = new ObservableCollection<ChatMessage>
{
    new("m1", "sam", "嗨 Alex！感谢你的来信。", DateTimeOffset.Now)
};

var thread = new ChatThread
{
    Participants = [me, sam],
    CurrentUserId = me.Id,
    ItemsSource = messages,
    Label = "客服会话",
    Height = 560
};
```

没有 `ItemsSource` 时，会话会保存自己的列表，从输入区发送的消息会追加到其中。将 `ItemsSource` 绑定到你自己的集合，即可由你自行保存消息。

### 发送与附件

`SendRequested` 会提供一个 `ChatDraft`，其中包含去除首尾空白后的文字、文件及其附件。没有 `ItemsSource` 时，会话还会自动追加这条消息；绑定了 `ItemsSource` 时，需要你自己添加。`IsAttachmentEnabled` 与 `AcceptedFileTypes` 控制附件按钮。

```csharp
thread.SendRequested += (_, e) =>
{
    messages.Add(new ChatMessage($"s-{messages.Count + 1}", me.Id, e.Draft.Text, DateTimeOffset.Now)
    {
        Attachments = e.Draft.Attachments.Count > 0 ? e.Draft.Attachments : null,
        Status = ChatMessageStatus.Sent
    });
};
```

### 状态、已读回执与输入中

你自己发出的消息若为 `Failed`，会显示重试操作，按下时触发 `RetryRequested`。`ReadBy` 将每位参与者映射到其最后读到的消息，`TypingParticipantIds` 列出正在输入的人。

```csharp
thread.ReadBy = new Dictionary<string, string> { ["sam"] = "m6" };
thread.TypingParticipantIds = ["sam"];
thread.RetryRequested += (_, e) => e.Message.Status = ChatMessageStatus.Sent;
```

### 表情回应与流式输出

`ReactionToggled` 报告读者选择的表情回应。使用 `AppendToMessage` 与 `CompleteMessage` 可以流式输出回复：Markdown 正文只接收新增的文字。

<Demo name="StreamingMarkdownReply" />

```csharp
thread.ReactionToggled += (_, e) => ToggleReaction(e.Message, e.Emoji);

messages.Add(new ChatMessage("r1", "arc", null, DateTimeOffset.Now));
thread.AppendToMessage("r1", "下面是一个带重试功能的小工具函数。");
thread.CompleteMessage("r1");
thread.ScrollToBottom();
```

### 一千条消息

行会被虚拟化并回收：只有视野内（外加少量余量）的消息真正存在。向上滚动后，来自他人的新消息会让提示胶囊出现。

<Demo name="ThousandMessages" />

### 单独使用输入区

`ChatComposer` 最多增长到六行，可以通过按钮、粘贴或拖放接收文件，按 Enter 发送（Shift + Enter 换行）。

<Demo name="ComposerOnItsOwn" />

## API 参考

<!-- api: ChatThread -->

<!-- api: ChatMessage -->

## 无障碍

- 消息列表是一个具名列表，只播报新消息。输入状态会单独播报，例如 "Sam 正在输入"。
- 表情回应标签是切换按钮。已读回执会列出读过某条消息的人。头像仅作装饰。
- Enter 发送，Shift + Enter 换行。Tab 可依次到达每条消息的添加表情按钮。
- 在表情选择器中，左右方向键在表情之间移动，到头后回绕。Esc 关闭选择器，并将焦点交还按钮。

## 动效

- 新消息从其所在的一侧带着轻微缩放升起，列表会按新增的高度向上移动。
- 读者的头像会滑动到其已读的最新消息处。
- 表情回应标签以弹出方式出现，计数会滚动变化。表情行与附件托盘以弹簧展开。
- 输入指示点只在可见时循环。向上滚动时若有新消息到达，会出现"跳到最新"的胶囊按钮。
- 减弱动效时使用短促的淡入淡出，停止输入指示点，并立即滚动到底部。关闭动效时直接到最终状态。

## 相关组件

- `ChatComposer`：单独使用的带附件输入区。
- `CommentThread`：针对单个对象的线程式回复。
- `Timeline`：事件历史。
