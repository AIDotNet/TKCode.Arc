---
title: Support conversation
summary: 客服人員查看一位客戶對話的界面：消息、可寫回覆或內部備注並帶常用回覆的輸入區、狀態，以及顯示客戶信息和團隊備注的面板。
source-hash: f5bb97038d7b
---

<Demo name="Preview" />

## 何時使用

`SupportConversation` 是客服人員這一側的單個客戶對話。消息在中間，輸入區可以寫客戶能看到的回覆，也可以寫只有團隊能看到的內部備注（備注帶暖色底，兩者不會混淆）；常用回覆一按即可插入，狀態切換說明輪到誰處理。右側面板顯示客戶是誰：方案、地點、價值、標籤和團隊備注。

- **客服系統或共享收件箱**：從對話列表中打開。
- **客戶成功工具**：帳戶背景和消息同樣重要。

## 何時不用

- **客戶這一側的聊天**：使用 `SupportWidget` 或 `ChatThread`。
- **大量對話的隊列**：先用 `InboxTriage` 整理，再在這裡打開一個。
- **團隊聊天**：單獨使用 `ChatThread`。

## 用法

```xml
<arc:SupportConversation x:Name="Conversation" Subject="Charged twice in March" AgentId="agent" Status="Open" />
```

```csharp
Conversation.ReplySent += (_, e) => api.Deliver(e.Text, e.Mode);
Conversation.Customer = new SupportCustomer { Name = "Mara Lindqvist", Email = "mara@example.com", Plan = "Team" };
Conversation.Participants.Add(new ChatParticipant("cust", "Mara Lindqvist"));
Conversation.Messages.Add(new ChatMessage("m1", "cust", "Hi! I was charged twice.", DateTimeOffset.Now));
Conversation.CannedReplies.Add(new SupportCannedReply("Refund issued", "Hi {name}, the refund is on its way."));
```

回覆會作為客服的消息加入 `Messages`，並把處理中的對話改為等待中；備注加入 `Notes`。兩者都會觸發 `ReplySent`，帶上文字、模式和加入的項目。常用回覆中的 `{name}` 會換成客戶的名字。消息以 Markdown 顯示。首次加載時沒有消息，會顯示一個示例對話。

### 窄欄

<Demo name="Narrow" />

`Send()`、`InsertCannedReply(reply)` 和 `OpenCannedReplies()` 可以用程式碼完成輸入區的操作。

## API 參考

<!-- api: SupportConversation -->

<!-- api: SupportCustomer -->

<!-- api: SupportNote -->

<!-- api: SupportCannedReply -->

<!-- api: SupportConversationStatus -->

<!-- api: SupportComposerMode -->

<!-- api: SupportReplyEventArgs -->

<!-- api: SupportStatusChangedEventArgs -->

## 無障礙

- 客戶名字是標題；狀態和輸入模式是有名稱的分段控件。
- 輸入區的名稱隨模式變化（“回覆”或“內部備注”），讀屏用戶能知道誰會看到這段文字。發送和狀態變化都會讀出。
- 備注以“某人的備注”加內容命名；客戶信息是標籤和值的組合。

### 鍵盤

| 按鍵 | 作用 |
|---|---|
| 輸入區中的 `Ctrl+Enter` | 發送回覆或添加備注。 |
| 空輸入區中的 `/` | 打開常用回覆。 |
| 常用回覆中的 `↑` / `↓`、`Enter`、`Escape` | 選擇一條或關閉列表。 |

## 動效

- 新消息以對話列表自身的動效出現（向上浮入，已讀標記移到最新已讀的消息）。
- 輸入區的底色和分段控件以短暫淡入淡出變化；常用回覆在一個縮放出現的浮層中打開。
- 減少動效時只保留淡入淡出；關閉動效時變化立即發生。

## 響應式

- 容器沒有給出高度時，區塊高 620 px（`DefaultHeight`）。
- 寬度在 820 px 及以上時，客戶面板在右側（300 px，`SidePanelWidth`）。更窄時（`:narrow`），它移到標題區的面板按鈕後面，顯示時覆蓋在消息上。

## 相關元件

- `ChatThread`：單獨的消息列表。
- `InboxTriage`：對話隊列。
- `SupportWidget`：客戶這一側。
