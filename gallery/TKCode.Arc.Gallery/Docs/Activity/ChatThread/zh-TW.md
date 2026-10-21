---
title: 聊天對話
summary: 帶分組訊息、表情回應、已讀回執、輸入狀態以及附帶附件輸入區的聊天對話。
source-hash: 2d352d6afccd
---

<Demo name="SupportConversation" />

## 何時使用

`ChatThread` 是人們輪流交談的應用程式中的對話介面：帳單或訂單頁面上的客服元件、CRM 中的私訊面板，或內部工具中的團隊聊天室。它負責訊息清單、日期分隔線、捲動行為與輸入區。周圍的頁面提供訊息，並處理使用者送出的內容。

- **帳單或訂單頁面上的客戶支援**：客服人員回覆發票問題。訊息依人員與時間分組，客戶的訊息位於右側並帶有送達狀態。
- **CRM 中的客戶對話**：業務負責人與客戶就續約交換訊息。已讀回執顯示對方看過哪些訊息。
- **內部工具中的團隊聊天室**：事故處理聊天室中，工程師張貼文字、圖片與記錄檔。附件以圖片集或可開啟檔案的檔案列顯示。
- **分段抵達的助理回覆**：機器人把答案逐段串流到泡泡中。對話會把每個片段附加到 Markdown 內文，而不會從頭重新解析整則回覆。
- **可能失敗的訊息**：結帳或支援流程中，伺服器未接受的訊息。它會變暗、顯示「未送達」，並在你最後一則訊息上提供重試。

## 何時不用

- **圍繞單一物件的串接式討論**，例如合約條款上的審閱意見：用 `CommentThread`，回覆會巢狀在留言之下，並可標示為已解決。
- **跨多個物件的事件歷程**，例如部署或稽核紀錄：用 `Timeline`，它沒有輸入區，也沒有已讀回執。
- **只有多行輸入、沒有對話**，例如紀錄上的一則備註：用 `Textarea`。若要在單一欄位中 @提及，用 `MentionInput`。
- **需要放在你自己的訊息清單旁邊的輸入區**：單獨使用 `ChatComposer`。它具有相同的文字方塊、附件托盤與送出行為，但沒有清單。
- **需要依日期從伺服器分頁載入的歷史紀錄**：對話只顯示交給它的訊息，不會載入歷史紀錄。請在檢視模型中載入分頁，再加入集合。

## 用法

`Participants` 列出對話中的人員，`CurrentUserId` 標示其中哪一位是你。你的訊息以強調色顯示在右側，其他人的訊息顯示在左側並附上姓名。訊息由舊到新排列，清單始終停留在最新一則。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:ChatThread Label="支援對話"
                    Participants="{Binding Participants}"
                    CurrentUserId="me"
                    ItemsSource="{Binding Messages}"
                    Height="560" />
</UserControl>
```

```csharp
var me = new ChatParticipant("me", "Alex Kim");
var sam = new ChatParticipant("sam", "Sam Rivera");

var messages = new ObservableCollection<ChatMessage>
{
    new("m1", "sam", "嗨 Alex！謝謝你的聯絡。", DateTimeOffset.Now)
};

var thread = new ChatThread
{
    Participants = [me, sam],
    CurrentUserId = me.Id,
    ItemsSource = messages,
    Label = "支援對話",
    Height = 560
};
```

`ChatThread` 衍生自 Avalonia 的 `ItemsControl`，因此 `ItemsSource` 與 `Items` 接受清單所用的集合。控制項會自行建構每一列，所以你提供的是資料，而不是項目範本。對話需要父容器給它一個有界的高度。

沒有 `ItemsSource` 時，對話會自行維護清單：從輸入區送出的訊息會被附加，表情回應則原地切換。當訊息來自伺服器時，請把 `ItemsSource` 繫結到你自己的集合。

### 結構

1. **訊息清單**（`Label` 為其命名）：可捲動的區域。讀者距離結尾在 48 px 以內時，它會跟隨最新訊息。
2. **日期分隔線**：12 px 的標籤（「昨天」、「今天」或日期），位於兩條 1 px 的線之間，線條使用柔和的邊框色。
3. **作者頭像**：28 px，位於他人訊息旁的邊欄中。沒有 `Avatar` 時顯示姓名縮寫。
4. **中繼資訊列**：作者姓名為 12 px 中等字重，時間為 12 px 淡化文字。
5. **泡泡**：18 px 圓角，內距為上 8 px、左右 13 px、下 9 px。他人的泡泡使用淡化表面色（深色模式下會提亮）；你的泡泡使用強調色。泡泡最多佔列寬的 78%（寬度小於 420 px 時為 84%），且最寬不超過 544 px。
6. **附件**：圖片組成寬 240 px 的圖片集，間距 3 px，最多顯示四個方塊，第四個方塊上有「+N」遮罩。其他檔案以一列顯示名稱與大小。
7. **回應列**：每個表情一個標籤，附上計數；另有一個 28 px 的新增回應按鈕，會開啟選擇器。
8. **回執列**：狀態文字，以及 16 px 的已讀者頭像，彼此重疊 4 px。
9. **輸入中列**：36 px 的泡泡，內含循環動畫的圓點，在 `TypingParticipantIds` 非空時顯示。
10. **輸入區**（`IsComposerVisible`）：可增長至六行的文字方塊、附件按鈕與送出按鈕。
11. **跳至膠囊**：高 32 px，位於底邊上方 12 px。當視窗外有新訊息時顯示「3 則新訊息」；讀者停在較上方且沒有新訊息時顯示「跳至最新」。

### 繫結到檢視模型

檢視模型擁有訊息、它們的狀態與回執，對話則負責顯示。本例使用 CommunityToolkit.Mvvm。

```csharp
public interface ISupportApi
{
    Task<SendResult> SendMessageAsync(string text, int fileCount);
}

public sealed record SendResult(bool IsSuccess);

public partial class SupportChatViewModel : ObservableObject
{
    private readonly ISupportApi _api;

    public SupportChatViewModel(ISupportApi api)
    {
        _api = api;
        Participants =
        [
            new ChatParticipant("me", "Alex Kim"),
            new ChatParticipant("sam", "Sam Rivera")
        ];
    }

    public IReadOnlyList<ChatParticipant> Participants { get; }

    public ObservableCollection<ChatMessage> Messages { get; } = [];

    [ObservableProperty]
    private IReadOnlyDictionary<string, string> _readBy = new Dictionary<string, string>();

    [ObservableProperty]
    private IReadOnlyList<string> _typingParticipantIds = [];

    public async Task SendAsync(ChatDraft draft)
    {
        var message = new ChatMessage(Guid.NewGuid().ToString("N"), "me", draft.Text, DateTimeOffset.Now)
        {
            Attachments = draft.Attachments.Count > 0 ? draft.Attachments : null,
            Status = ChatMessageStatus.Sending
        };
        Messages.Add(message);

        var result = await _api.SendMessageAsync(draft.Text, draft.Files.Count);
        message.Status = result.IsSuccess ? ChatMessageStatus.Sent : ChatMessageStatus.Failed;
    }

    [RelayCommand]
    private async Task RetryAsync(ChatMessage message)
    {
        message.Status = ChatMessageStatus.Sending;
        var result = await _api.SendMessageAsync(message.Text ?? string.Empty, 0);
        message.Status = result.IsSuccess ? ChatMessageStatus.Sent : ChatMessageStatus.Failed;
    }
}
```

```xml
<arc:ChatThread Label="支援對話"
                Participants="{Binding Participants}"
                CurrentUserId="me"
                ItemsSource="{Binding Messages}"
                ReadBy="{Binding ReadBy}"
                TypingParticipantIds="{Binding TypingParticipantIds}"
                RetryCommand="{Binding RetryCommand}" />
```

- 輸入屬性（`Participants`、`ItemsSource`、`ReadBy`、`TypingParticipantIds`、`RetryCommand`）都是單向的：對話從不把它們寫回。`IsPinned` 與 `UnreadCount` 是唯讀輸出。
- `SendRequested` 是事件，不是命令。程式碼後置會訂閱它，並把草稿傳給 `SendAsync`。訊息本身由檢視模型加入，因此對話不會再附加一次。
- 變更訊息的 `Status` 或 `Text` 會原地更新其泡泡。只有在新增或移除訊息時才需要取代集合。

### 分組、時間與文化

同一人發出、間隔短於 `GroupWindow` 的訊息會共用一組，因此只有第一則會顯示姓名。預設間隔為 5 分鐘。來自其他人的訊息，或間隔更長之後的訊息，會開始新的一組。

`Culture` 會格式化時間與日期分隔線（預設為 en-US），`TimeZone` 設定它們顯示所在的時區（預設為機器的本地時區）。請為產品所在的市場同時設定兩者。

```csharp
thread.Culture = CultureInfo.GetCultureInfo("en-GB");
thread.TimeZone = TimeZoneInfo.FindSystemTimeZoneById("Europe/London");
thread.GroupWindow = TimeSpan.FromMinutes(10);
```

### 送出與附件

當使用者在輸入區按下 Enter 或送出按鈕時，`SendRequested` 會執行。`ChatDraft` 攜帶去除前後空白的 `Text`、所選的 `Files`（本機 `IStorageFile` 項目）以及對應的 `Attachments`。沒有文字也沒有檔案的草稿不會送出。沒有 `ItemsSource` 時，對話會自行附加訊息；有 `ItemsSource` 時，由處理常式加入訊息並決定其狀態，如同上方的檢視模型所做的。

`IsAttachmentEnabled`（預設為 true）會顯示附件按鈕，並接受貼上與拖放的檔案。`AcceptedFileTypes` 限制選擇器，托盤每次新增最多接受 10 個檔案。帶有 `Url` 或 `File` 的 `Image` 附件會加入圖片集；其他附件以檔案列顯示其大小。

當使用者點按圖片或檔案時，會執行 `AttachmentClicked`。除非處理常式把事件標示為已處理，否則對話會開啟 `Url` 或本機檔案。因此，當你的應用程式有自己的檢視器時，請把事件標示為已處理。

### 狀態、已讀回執與輸入中

`ChatMessage.Status` 會設定你自己最後一則訊息下方的文字。各狀態如下：

| 狀態 | 顯示的文字 | 何時設定 |
|---|---|---|
| `Sending` | 「傳送中」 | 訊息正在傳送途中，尚未被接受 |
| `Sent` | 「已傳送」 | 伺服器已接受（你自己訊息的預設值） |
| `Delivered` | 「已送達」 | 訊息已抵達對方 |
| `Read` | 「已讀」 | 訊息已被閱讀；在它當下或之後的回執也會顯示此狀態 |
| `Failed` | 「未送達」 | 傳送失敗。泡泡會淡化到 60% 不透明度 |

`ReadBy` 把每位參與者的 id 對應到該人最後讀過的訊息；當對應改變時，其頭像會平順地滑到較新的訊息上。每次都請指派新的字典。`TypingParticipantIds` 列出正在輸入的人。只有在訂閱 `RetryRequested` 或設定 `RetryCommand` 時，重試才會出現，而且只出現在你最後一則訊息上。命令會收到失敗的 `ChatMessage`。

### 表情回應與選擇器

新增回應按鈕會開啟一個選擇器，其中列出 `Reactions` 內的表情。預設集合是 Arc 選擇器的六個表情，`DefaultReactions` 在程式碼中回傳同一份清單。選取一個表情，或點按標籤，都會觸發 `ReactionToggled`。沒有 `ItemsSource` 時，對話會自行切換回應；有 `ItemsSource` 時，由應用程式設定訊息的 `Reactions`：

```csharp
thread.ReactionToggled += (_, e) =>
{
    var reactions = new List<ChatReaction>(e.Message.Reactions ?? []);
    var at = reactions.FindIndex(r => r.Emoji == e.Emoji);
    if (at < 0)
    {
        reactions.Add(new ChatReaction(e.Emoji, 1, true));
    }
    else
    {
        var reaction = reactions[at];
        reactions[at] = reaction with { Count = reaction.Count + (reaction.Mine ? -1 : 1), Mine = !reaction.Mine };
    }
    e.Message.Reactions = reactions.Where(r => r.Count > 0).ToArray();
};
```

### 串流回覆

分段抵達的回覆會先以空文字加入，隨著片段抵達由 `AppendToMessage` 填入，最後由 `CompleteMessage` 關閉。每次呼叫在沒有訊息擁有該 id 時回傳 `false`，因此已被移除之訊息遲到的片段不會產生任何作用。`IsMessageStreaming` 表示某則回覆是否仍處於開啟狀態。

<Demo name="StreamingMarkdownReply" />

```csharp
messages.Add(new ChatMessage("r1", "arc", null, DateTimeOffset.Now));
thread.AppendToMessage("r1", "這裡有一個用來重試請求的小型協助方法。");
thread.CompleteMessage("r1");
```

Markdown 內文只接收新增的文字，因此每個片段抵達時，較長的回覆都不會從頭重新解析。

### 資料結構與效能

每個 `ChatMessage` 都會引發 `PropertyChanged`，因此更新其屬性會更新它的泡泡。請為每則訊息提供穩定的 `Id`，因為 `ReadBy`、串流與表情回應的回呼都是透過它來尋找訊息。

列是虛擬化的。一千則訊息的清單實際產生的容器不到 60 個，而且對話最多保留 64 個已算繪的訊息內文，因此 Markdown 快取不會隨歷程增長。

<Demo name="ThousandMessages" />

讀者向上捲動時抵達的訊息會計入 `UnreadCount`，並由跳至膠囊顯示。在這個版本中，新增到頂端的較舊訊息不會為你固定位置，因此上線前請先測試這條路徑。

### 單獨使用輸入區

`ChatComposer` 是對話的輸入部分，它是公開的，因此可以單獨使用。它最多增長到六行，按 Enter 送出。`Text` 是雙向的。`Placeholder`（預設為 "Message"）同時是文字方塊的無障礙名稱。`Sent` 攜帶 `ChatDraft`。`Send()` 從程式碼送出，`FocusInput()` 把焦點移到文字方塊，`BrowseAsync()` 開啟檔案選擇器。

<Demo name="ComposerOnItsOwn" />

### 範例：帳單頁面上的客戶支援對話

SaaS 產品帳單頁面上的一個支援面板。面板有固定高度，因此對話會在其中捲動，輸入區始終可見。客服的回覆以檔案列附上對帳單，並顯示其大小。客戶最後一則訊息下方的回執表示客服已讀，輸入中列表示客服仍在撰寫。預留位置文字點出了客服的名字，比預設的「Message」更清楚。

![帳單對話中帶有檔案附件、已讀回執與輸入中指示的支援對話](images/SupportConversation.png)

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc"
             xmlns:generic="using:System.Collections.Generic"
             Width="600">
    <Border Height="600"
            CornerRadius="16"
            BorderThickness="1"
            ClipToBounds="True"
            Background="{arc:ArcDesignTokenResource Surface}"
            BorderBrush="{arc:ArcDesignTokenResource Border}">
        <arc:ChatThread Label="Support conversation"
                        CurrentUserId="me"
                        Placeholder="Reply to Sam">
            <arc:ChatThread.Participants>
                <generic:List x:TypeArguments="arc:ChatParticipant">
                    <arc:ChatParticipant Id="me" Name="Alex Kim" />
                    <arc:ChatParticipant Id="sam" Name="Sam Rivera" />
                </generic:List>
            </arc:ChatThread.Participants>
            <arc:ChatThread.ReadBy>
                <generic:Dictionary x:TypeArguments="x:String, x:String">
                    <x:String x:Key="sam">m4</x:String>
                </generic:Dictionary>
            </arc:ChatThread.ReadBy>
            <arc:ChatThread.TypingParticipantIds>
                <generic:List x:TypeArguments="x:String">
                    <x:String>sam</x:String>
                </generic:List>
            </arc:ChatThread.TypingParticipantIds>
            <arc:ChatMessage Id="m1" AuthorId="sam"
                             Text="Hi Alex, I can see the duplicate charge on invoice INV-2026-0412."
                             CreatedAt="2026-10-20T09:02:00+00:00" />
            <arc:ChatMessage Id="m2" AuthorId="me"
                             Text="Yes, the card was charged twice. Can you refund one of them?"
                             CreatedAt="2026-10-20T09:04:00+00:00"
                             Status="Read" />
            <arc:ChatMessage Id="m3" AuthorId="sam"
                             Text="Refund started. The statement copy is attached."
                             CreatedAt="2026-10-20T09:06:00+00:00">
                <arc:ChatMessage.Attachments>
                    <generic:List x:TypeArguments="arc:ChatAttachment">
                        <arc:ChatAttachment Id="f1" Name="statement-2026-09.pdf"
                                            Kind="File" Size="182400" />
                    </generic:List>
                </arc:ChatMessage.Attachments>
            </arc:ChatMessage>
            <arc:ChatMessage Id="m4" AuthorId="me"
                             Text="Thank you. When will it reach my account?"
                             CreatedAt="2026-10-20T09:08:00+00:00"
                             Status="Read" />
        </arc:ChatThread>
    </Border>
</UserControl>
```

場景中的訊息以內嵌方式保留，這是靜態圖片所必需的。在應用程式中，它們來自 `ItemsSource`。

### 文案規範

- **Label**：簡短的名詞片語，說明這段對話，例如「支援對話」「Atlas 專案」。它是清單的無障礙名稱，因此描述的是對話本身，而不是產品。
- **Placeholder**：點名收件人或用途，例如「回覆 Sam」。預設的「訊息」在客服元件中看起來像表單標籤。
- **錯誤**：使用對話顯示的狀態文字與重試操作。不要在泡泡下方再加一段錯誤文字。
- **其他語言**：設定 `Labels`；複數規則與名稱應放在其函式中。

| 不要寫 | 改為 |
|---|---|
| 訊息 | 回覆 Sam |
| 對話 | 支援對話 |
| 未送達，請稍後再試。 | 未送達。重試 |
| Sam 正在輸入… | Sam 正在輸入 |
| 1 則新訊息們 | 1 則新訊息 |

### 最佳做法

**建議**

- 把對話放在高度固定的容器中，讓清單有空間捲動，輸入區則保持在底部。
- 請求開始時把 `Status` 設為 `Sending`，伺服器回應時設為 `Sent` 或 `Failed`。
- 訊息變更時更新既有的 `ChatMessage`，其泡泡就會原地更新。
- 為產品所在的市場設定 `Culture` 與 `TimeZone`，讓時間與客服人員的時鐘一致。

**避免**

- 為了改變一則訊息而取代整個 `ItemsSource`。取代集合會重建每一列。
- 回覆抵達後仍讓「Sam 正在輸入」留在畫面上。事件結束時請清除 `TypingParticipantIds`，否則讀者會一直等一則不會來的訊息。
- 期待較早失敗的訊息上出現重試。重試只會出現在你最後一則訊息上。
- 對於有自己用語的產品仍保留預設的 `Reactions`。請設定團隊實際使用的表情。

### 常見問題

**清單不再跟隨新訊息。為什麼？** 讀者距離結尾超過 48 px。新訊息會計入 `UnreadCount`；點按跳至膠囊或呼叫 `ScrollToBottom()` 可以讓清單重新釘在底部。

**為什麼失敗的訊息上沒有重試？** 重試需要 `RetryRequested` 處理常式或 `RetryCommand`，而且只會出現在你最後一則訊息上。請重新送出該訊息，或在你自己的介面中顯示錯誤。

**為什麼我的訊息顯示為純文字？** `MessageFormat` 預設為 `Markdown`。請在對話上把它設為 `PlainText`，或在單一訊息上設定 `Format`。

## API 參考

<!-- api: ChatThread -->

<!-- api: ChatMessage -->

## 無障礙

- 訊息清單是一個具名的清單（`Label`，預設為「對話」），並設為禮貌的即時設定，因此每則新訊息都會被朗讀，而焦點不會移動。
- 輸入狀態在獨立的狀態區域中播報（「Sam 正在輸入」「Sam 和 Lee 正在輸入」）。圓點不會被播報。
- 表情回應標籤是切換按鈕。已讀回執會說明讀過訊息的人。頭像僅作裝飾。
- 附件、送出與新增回應按鈕的名稱取自 `Labels`（「附加檔案」「送出」「新增回應」）。文字方塊由 `Placeholder` 命名，托盤由 `Labels.Attachments` 命名。
- 選擇器開啟時，其第一個表情會取得焦點。Escape 會關閉選擇器，並把焦點送回新增回應按鈕。

### 鍵盤

| 按鍵 | 操作 |
|---|---|
| `Enter`（在輸入區中） | 草稿有文字或檔案時送出；輸入法組字期間忽略 |
| `Shift` + `Enter`（在輸入區中） | 新增一行 |
| `Tab` / `Shift+Tab` | 在輸入區、附件，以及每則訊息的新增回應與重試按鈕之間移動 |
| `Enter` 或 `Space`（在按鈕上） | 開啟選擇器、選取表情、切換標籤或按下重試 |
| `Left` / `Right`（在選擇器中） | 在表情之間移動，並在兩端循環 |
| `Escape`（在選擇器中） | 關閉選擇器，並把焦點送回新增回應按鈕 |

指標停在訊息上，或其中的控制項取得焦點時，新增回應按鈕會顯示，因此 `Tab` 可以到達它。

## 動態效果

`ScrollToBottom()` 會平順地滑到結尾，除非你傳入 `smooth: false`。下表列出每種變化：

| 變化 | 完整 | 減少 | 關閉 |
|---|---|---|---|
| 新訊息抵達 | 從所在的一側帶著輕微縮放升起；清單向上移動 | 短暫淡入 | 立即完成 |
| 釘在底部時內容增長 | 清單以彈簧跟隨最新訊息 | 立即完成 | 立即完成 |
| 已讀回執移動 | 頭像滑向較新的訊息 | 短暫淡入 | 立即完成 |
| 新增或移除表情回應 | 標籤彈出，計數滾動變化 | 短暫淡入 | 立即完成 |

| 輸入中指示 | 可見時圓點循環 | 圓點停止 | 圓點停止 |
| 跳至膠囊出現 | 從下方升起 | 短暫淡入 | 立即完成 |

## 相關元件

- `ChatComposer`：需要單獨使用附帶附件的輸入區時。
- `CommentThread`：圍繞單一物件的巢狀回覆，可解決。
- `Timeline`：沒有輸入區的事件歷程。

| 元件 | 適用情境 |
|---|---|
| `ChatComposer` | 需要不含訊息清單的文字方塊與附件托盤 |
| `CommentThread` | 回覆巢狀在單一物件之下，例如審閱意見，並且可以解決 |
| `Timeline` | 歷程是跨多個物件的事件清單，而不是對話 |
| `Textarea` | 備註或描述以多行撰寫，且沒有回覆 |
| `MentionInput` | 使用者輸入單則訊息時需要補全 @提及 |
