---
title: 聊天對話
summary: 帶分組訊息、表情回應、已讀回執、輸入狀態以及附帶附件輸入區的聊天對話。
source-hash: 1cd766b47d7a
---

<Demo name="SupportConversation" />

## 何時使用

- 客服小工具、私訊或同時在線的團隊聊天。
- 依人員與時間分組的訊息，帶日期分隔線與已讀回執。
- 可以傳送文字與檔案的輸入區。

## 何時不用

- 圍繞單一物件的串接式審閱，回覆巢狀於留言之下：用 `CommentThread`。
- 跨多個物件的事件紀錄：用 `Timeline`。
- 只需多行輸入、沒有對話：用 `Textarea`；附帶附件的輸入區用 `ChatComposer`。

## 用法

`Participants` 列出對話中的人員，`CurrentUserId` 標明其中哪一位是你。你傳送的訊息會顯示在右側，使用強調色。訊息依由舊到新的順序排列。

```xml
<arc:ChatThread Label="客服對話"
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
    new("m1", "sam", "嗨 Alex！感謝你的來信。", DateTimeOffset.Now)
};

var thread = new ChatThread
{
    Participants = [me, sam],
    CurrentUserId = me.Id,
    ItemsSource = messages,
    Label = "客服對話",
    Height = 560
};
```

沒有 `ItemsSource` 時，對話會保存自己的清單，從輸入區傳送的訊息會附加到其中。將 `ItemsSource` 繫結到你自己的集合，即可由你自行儲存訊息。

### 傳送與附件

`SendRequested` 會提供一個 `ChatDraft`，其中包含去除前後空白的文字、檔案及其附件。沒有 `ItemsSource` 時，對話還會自動附加這則訊息；繫結了 `ItemsSource` 時，則需要你自己加入。`IsAttachmentEnabled` 與 `AcceptedFileTypes` 控制附件按鈕。

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

### 狀態、已讀回執與輸入中

你自己發出的訊息若為 `Failed`，會顯示重試動作，按下時觸發 `RetryRequested`。`ReadBy` 將每位參與者對應到其最後讀取的訊息，`TypingParticipantIds` 列出正在輸入的人。

```csharp
thread.ReadBy = new Dictionary<string, string> { ["sam"] = "m6" };
thread.TypingParticipantIds = ["sam"];
thread.RetryRequested += (_, e) => e.Message.Status = ChatMessageStatus.Sent;
```

### 表情回應與串流輸出

`ReactionToggled` 回報讀者選取的表情回應。使用 `AppendToMessage` 與 `CompleteMessage` 可以串流輸出回覆：Markdown 內文只接收新增的文字。

<Demo name="StreamingMarkdownReply" />

```csharp
thread.ReactionToggled += (_, e) => ToggleReaction(e.Message, e.Emoji);

messages.Add(new ChatMessage("r1", "arc", null, DateTimeOffset.Now));
thread.AppendToMessage("r1", "以下是一個帶重試功能的小工具函式。");
thread.CompleteMessage("r1");
thread.ScrollToBottom();
```

### 一千則訊息

列會被虛擬化並回收：只有視野內（外加少量餘量）的訊息真正存在。向上捲動後，來自他人的新訊息會讓提示膠囊出現。

<Demo name="ThousandMessages" />

### 單獨使用輸入區

`ChatComposer` 最多增長到六行，可以透過按鈕、貼上或拖放接收檔案，按 Enter 送出（Shift + Enter 換行）。

<Demo name="ComposerOnItsOwn" />

## API 參考

<!-- api: ChatThread -->

<!-- api: ChatMessage -->

## 無障礙

- 訊息列表是一個具名清單，只播報新訊息。輸入狀態會另外播報，例如 "Sam 正在輸入"。
- 表情回應標籤是切換按鈕。已讀回執會列出讀過某則訊息的人。頭像僅作裝飾。
- Enter 傳送，Shift + Enter 換行。Tab 可依序到達每則訊息的新增表情按鈕。
- 在表情選擇器中，左右方向鍵在表情之間移動，到頭後回繞。Esc 關閉選擇器，並將焦點交還按鈕。

## 動態效果

- 新訊息從其所在的一側帶著輕微縮放升起，清單會依新增的高度向上移動。
- 讀者的頭像會滑動到其已讀的最新訊息處。
- 表情回應標籤以彈出方式出現，計數會捲動變化。表情列與附件託盤以彈簧展開。
- 輸入指示點只在可見時循環。向上捲動時若有新訊息到達，會出現「跳到最新」的膠囊按鈕。
- 減少動態效果時使用短促的淡入淡出，停止輸入指示點，並立即捲動到底部。關閉動態效果時直接到最終狀態。

## 相關元件

- `ChatComposer`：單獨使用的附帶附件輸入區。
- `CommentThread`：針對單一物件的串接式回覆。
- `Timeline`：事件紀錄。
