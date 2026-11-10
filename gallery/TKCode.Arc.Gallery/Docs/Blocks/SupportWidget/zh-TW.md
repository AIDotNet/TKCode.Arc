---
title: Support widget
summary: 客戶在網站角落打開的浮動幫助窗口：問候語、文章搜索和最近文章、能看到對方正在輸入並收到回覆的聊天，以及以前的對話記錄。
source-hash: e5fcf8ee85cf
---

<Demo name="Preview" />

## 何時使用

`SupportWidget` 是網站或應用角落裡的幫助按鈕。按下後會打開一個面板：問候語、負責回答的人的頭像和回覆速度、幫助文章搜索和最新文章、開始聊天的“給我們發消息”按鈕，以及以前的對話記錄。聊天中能看到團隊正在輸入和他們的回覆。

- **營銷網站或網頁應用**：訪客不用離開頁面就能得到幫助。
- **應用內幫助入口**：先提供文章，再轉給真人。

## 何時不用

- **完整的幫助網站**：使用 `HelpCenter`。
- **客服人員這一側**：使用 `SupportConversation`。
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

浮窗會填滿給它的空間，並讓自己停在右下角，所以請把它放在頁面上層。`Agents` 是負責回答的人，`Articles` 用於搜索和最新文章列表，`Conversations` 是對話記錄，最新的在前。開啟 `SimulatesReplies`（預設）時，第一位客服會自己輸入並回答，這適合預覽，不適合正式使用。首次加載時沒有文章，會顯示一組示例。

### 關閉狀態

<Demo name="Closed" />

`IsOpen` 是雙向的；`Open()`、`Close()`、`StartConversation()`、`OpenChat(conversation)`、`OpenArticle(article)`、`GoBack()` 和 `SendMessage(text)` 可以用程式碼控制它。`Screen` 表示面板正在顯示什麼。

## API 參考

<!-- api: SupportWidget -->

<!-- api: SupportWidgetScreen -->

<!-- api: SupportWidgetArticle -->

<!-- api: SupportWidgetConversation -->

<!-- api: SupportWidgetMessageEventArgs -->

## 無障礙

- 啟動按鈕名為“打開幫助”或“關閉幫助”；面板是名為“幫助”的組，每一屏都有標題。
- 打開時焦點移到搜索框；關閉時回到啟動按鈕。團隊的回覆會讀出。
- 列表中的行是以標題命名的按鈕；未讀的對話會說明未讀。

### 鍵盤

| 按鍵 | 作用 |
|---|---|
| 啟動按鈕上的 `Enter` / `Space` | 打開或關閉面板。 |
| `Escape` | 在聊天或文章中返回；否則關閉面板。在搜索框中先清空內容。 |
| 聊天輸入區中的 `Enter` | 發送消息。 |

## 動效

- 面板從啟動按鈕所在的角落縮放出現，再縮放收回；啟動按鈕的圖標在氣泡和箭頭之間彈出切換。
- 各屏横向滑動，進入聊天或文章時向前，返回時向後；正在輸入的提示和新消息來自 `ChatThread`。
- 減少動效時只用淡入淡出；關閉動效時變化立即發生。

## 響應式

- 面板大小為 380 × 560 px（`PanelWidth`、`PanelHeight`），距離角落 20 px（`CornerMargin`）。
- 在手機大小的頁面上，請讓浮窗占滿屏幕，並通過主題設置更小的 `PanelWidth`，或改為打開 `HelpCenter`。

## 相關元件

- `HelpCenter`：完整的幫助網站。
- `ChatThread`：單獨的聊天。
- `SupportConversation`：客服人員的視圖。
