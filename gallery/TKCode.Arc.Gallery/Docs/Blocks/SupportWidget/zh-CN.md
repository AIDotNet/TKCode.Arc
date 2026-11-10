---
title: Support widget
summary: 客户在网站角落打开的浮动帮助窗口：问候语、文章搜索和最近文章、能看到对方正在输入并收到回复的聊天，以及以前的对话记录。
source-hash: e5fcf8ee85cf
---

<Demo name="Preview" />

## 何时使用

`SupportWidget` 是网站或应用角落里的帮助按钮。按下后会打开一个面板：问候语、负责回答的人的头像和回复速度、帮助文章搜索和最新文章、开始聊天的“给我们发消息”按钮，以及以前的对话记录。聊天中能看到团队正在输入和他们的回复。

- **营销网站或网页应用**：访客不用离开页面就能得到帮助。
- **应用内帮助入口**：先提供文章，再转给真人。

## 何时不用

- **完整的帮助网站**：使用 `HelpCenter`。
- **客服人员这一侧**：使用 `SupportConversation`。
- **公告或提示**：使用 `AnnouncementBar` 或 `Toast`。

## 用法

```xml
<Panel>
    <!-- the page -->
    <arc:SupportWidget x:Name="Widget" TeamName="Lumen support" SimulatesReplies="False" />
</Panel>
```

```csharp
Widget.MessageSent += (_, e) => api.Send(e.Conversation.Id, e.Message.Text);

// When the team answers:
conversation.Messages.Add(new ChatMessage("r1", "kofi", "Happy to help!", DateTimeOffset.Now));
```

浮窗会填满给它的空间，并让自己停在右下角，所以请把它放在页面上层。`Agents` 是负责回答的人，`Articles` 用于搜索和最新文章列表，`Conversations` 是对话记录，最新的在前。开启 `SimulatesReplies`（默认）时，第一位客服会自己输入并回答，这适合预览，不适合正式使用。首次加载时没有文章，会显示一组示例。

### 关闭状态

<Demo name="Closed" />

`IsOpen` 是双向的；`Open()`、`Close()`、`StartConversation()`、`OpenChat(conversation)`、`OpenArticle(article)`、`GoBack()` 和 `SendMessage(text)` 可以用代码控制它。`Screen` 表示面板正在显示什么。

## API 参考

<!-- api: SupportWidget -->

<!-- api: SupportWidgetScreen -->

<!-- api: SupportWidgetArticle -->

<!-- api: SupportWidgetConversation -->

<!-- api: SupportWidgetMessageEventArgs -->

## 无障碍

- 启动按钮名为“打开帮助”或“关闭帮助”；面板是名为“帮助”的组，每一屏都有标题。
- 打开时焦点移到搜索框；关闭时回到启动按钮。团队的回复会读出。
- 列表中的行是以标题命名的按钮；未读的对话会说明未读。

### 键盘

| 按键 | 作用 |
|---|---|
| 启动按钮上的 `Enter` / `Space` | 打开或关闭面板。 |
| `Escape` | 在聊天或文章中返回；否则关闭面板。在搜索框中先清空内容。 |
| 聊天输入区中的 `Enter` | 发送消息。 |

## 动效

- 面板从启动按钮所在的角落缩放出现，再缩放收回；启动按钮的图标在气泡和箭头之间弹出切换。
- 各屏横向滑动，进入聊天或文章时向前，返回时向后；正在输入的提示和新消息来自 `ChatThread`。
- 减少动效时只用淡入淡出；关闭动效时变化立即发生。

## 响应式

- 面板大小为 380 × 560 px（`PanelWidth`、`PanelHeight`），距离角落 20 px（`CornerMargin`）。
- 在手机大小的页面上，请让浮窗占满屏幕，并通过主题设置更小的 `PanelWidth`，或改为打开 `HelpCenter`。

## 相关组件

- `HelpCenter`：完整的帮助网站。
- `ChatThread`：单独的聊天。
- `SupportConversation`：客服人员的视图。
