---
title: 留言串
summary: 支援回覆、表情反應、提及、原地編輯與解決的巢狀留言。
source-hash: d0215cd8a3c4
---

<Demo name="DesignReview" />

## 何時使用

`CommentThread` 是附著在一個物件上的討論：設計稿中的一個畫框、文件中的一條條款、工單，或一個提取要求（pull request）。每則留言帶有自己的回覆，人們可以用表情反應，作者可以原地編輯與刪除自己的留言，問題確定之後串可以被標示為已解決。留言串與物件綁在一起，因此決策的理由就在決策旁邊。

- **結帳頁面的設計審閱**：產品設計師與工程師討論訂單摘要的間距。回覆巢狀在它所回應的留言之下，間距達成共識後留言串被標示為已解決。
- **對法律條款的回饋**：審閱者對合約中的某一條款留言，並以「@」提及負責人，使其收到通知。已解決的留言串作為決策紀錄保留在該條款上。
- **支援工單上的討論**：工程師與支援主管討論工單 INC-2026-0931 中事故的原因。將留言串標示為已解決，表示問題已關閉，但不會刪除討論內容。
- **對指標的提問**：財務主管詢問年度經常性收入數字為何變動，分析師在附著於該數字的留言串中回答。

## 何時不用

- **同時在線的人之間的即時對話**：用 `ChatThread`，它有訊息清單、輸入狀態與已讀回執。
- **跨多個物件的事件歷程**，例如部署或狀態變更：用 `Timeline`。
- **只需要帶提及建議的輸入欄位，沒有討論**：用 `MentionInput`。
- **一則沒有人回覆的單一備註**：用 `Textarea`。只有一則留言且沒有回覆的留言串，等於是附帶額外控制項的備註。

## 用法

將 `CurrentUser` 設為閱讀此留言串的人。他們只能編輯與刪除自己的留言。`Comments` 是留言樹，由舊到新排列，每則留言的回覆位於其 `Replies` 清單之下。

```csharp
var ana = new CommentAuthor("ana", "Ana Ruiz");
var mina = new CommentAuthor("mina", "Mina Park");

var thread = new CommentThread
{
    Title = "首屏標題",
    CurrentUser = ana,
    Comments =
    [
        new ThreadComment("c1", mina, "新標題讀起來很順。@Theo Walsh 能把副標題再精簡一下嗎？", "2 小時前")
        {
            Replies =
            [
                new ThreadComment("c2", new CommentAuthor("theo", "Theo Walsh"), "好的，把它精簡成一行。", "1 小時前")
            ]
        }
    ]
};
```

```xml
<arc:CommentThread Title="價格表間距"
                   Placeholder="回覆此留言串"
                   CurrentUser="{Binding CurrentUser}"
                   Comments="{Binding Comments}"
                   IsResolved="{Binding IsResolved}" />
```

`CommentThread` 衍生自 `TemplatedControl`，並依內容調整自身大小，因此請把它放在能提供所需寬度的容器中。留言串會立即顯示每一項變更：它建構一棵新的不可變留言樹並顯示，然後回報這項變更。它不會等待應用程式儲存任何內容。

### 結構

1. **頁首**：`Title`（字串，或僅在頁首中使用的控制項）、副標題「3 則留言」，以及「解決」按鈕。頁首左側內距為 18 px，頂部內距為 12 px。
2. **留言**：16 px 圓角表面，內距 10 px，留言之間相距 10 px。包含頭像（28 px，沒有 `Avatar` 時顯示名字縮寫）、作者姓名、時間，以及已編輯留言的「已編輯」標記。
3. **內文**：14 px 文字，行高 1.4。以「@全名」書寫的已知人員會以中等字重顯示。
4. **操作**：每則留言都有「回覆」；「編輯」與「刪除」只出現在目前使用者自己的留言上。操作按鈕高 28 px。指標停在留言上、鍵盤焦點位於留言內、提示框開啟時，以及觸控螢幕上，它們始終可見。
5. **表情反應**：每個表情一個標籤，附上計數。新增反應按鈕會開啟一個選擇器，其中是 28 px 的表情按鈕，表情為 16 px。
6. **回覆切換**：「展開 2 則回覆」或「收合回覆」。回覆位於一條導軌線之下，縮排 24 px（窄版時為 12 px）。
7. **已刪除占位**：已刪除且帶有回覆的留言會保留為「此留言已刪除」，讓留言串維持結構。
8. **輸入區**：目前使用者的頭像（寬度低於 420 px 時隱藏）、一個從 38 px 增長到 160 px、圓角為 14 px 的欄位，以及高 38 px 的傳送按鈕（編輯時為 32 px）。
9. **回覆狀態列**：輸入區上方的「正在回覆 Theo Walsh」，並帶有 22 px 的關閉按鈕。
10. **提及建議**：欄位下方的清單，每列高 32 px，最多顯示五人。
11. **已解決標籤**：留言串被解決後，頁首會收合為一個 24 px 的圖示、標題、參與人數，以及高 32 px 的「重新開啟」按鈕。標籤的圓角為 22 px。

線條與間距由元件主題設定。若要改變整個留言串的外觀，請使用主題資源，而不要使用逐個實例的屬性。

### 繫結到檢視模型

檢視模型擁有留言與已解決狀態。它載入這些內容，套用留言串回報的每一項變更，並回傳儲存後的樹。`Comments` 與 `IsResolved` 是雙向的，因此留言串在它們變動時會寫回新值。本例使用 CommunityToolkit.Mvvm。

```csharp
public interface IReviewStore
{
    Task<ThreadComment[]> LoadCommentsAsync(string reviewId);

    Task<bool> IsResolvedAsync(string reviewId);

    Task ApplyChangeAsync(string reviewId, CommentThreadChange change);
}

public partial class DesignReviewViewModel : ObservableObject
{
    private readonly IReviewStore _store;

    public DesignReviewViewModel(IReviewStore store)
    {
        _store = store;
        CurrentUser = new CommentAuthor("ana", "Ana Ruiz", "avares://Acme.Review/Assets/ana.png");
    }

    public CommentAuthor CurrentUser { get; }

    [ObservableProperty]
    private IReadOnlyList<ThreadComment> _comments = [];

    [ObservableProperty]
    private bool _isResolved;

    public async Task LoadAsync(string reviewId)
    {
        Comments = await _store.LoadCommentsAsync(reviewId);
        IsResolved = await _store.IsResolvedAsync(reviewId);
    }

    public Task RecordAsync(string reviewId, CommentThreadChange change) =>
        _store.ApplyChangeAsync(reviewId, change);
}
```

```xml
<arc:CommentThread Title="價格表間距"
                   CurrentUser="{Binding CurrentUser}"
                   Comments="{Binding Comments, Mode=TwoWay}"
                   IsResolved="{Binding IsResolved, Mode=TwoWay}"
                   People="{Binding People}" />
```

- `CommentsChanged` 是承載每項變更的事件。程式碼後置會把它轉送給 `RecordAsync`，因此由儲存層自行儲存變更，檢視模型不需要比較樹。
- `People` 列出可被提及的人員。保留為 null 時，會提供留言串中的所有人以及目前使用者。
- 若要拒絕某項變更，請在處理常式中把 `Comments` 設回先前的樹。留言串隨後會顯示舊的樹。

### 提及與人員

在輸入區中輸入 `@` 可以提示人員。`People` 設定該清單。若未設定，則提供留言串中的人員與目前使用者。建議會比對名稱中任一個字的開頭，因此輸入「w」可以找到「Theo Walsh」。

<Demo name="EmptyThread" />

### 反應與層級

`Reactions` 設定選擇器為每則留言提供的表情。預設為六個表情：讚、紅心、彩炮、眼睛、火箭與勾號。`MaxDepth` 限制巢狀層級（預設 2）：超過該深度的回覆會附加到允許的最深父層上。

### 解決與重新開啟

`Resolve()` 會把留言串折疊為已解決標籤，且只有在留言串有留言時才有效。`Reopen()` 會重新開啟已解決的留言串。`IsResolved` 是雙向的，`ResolvedChanged` 會隨每次變動回報新的狀態。

<Demo name="Resolved" />

```csharp
thread.ResolvedChanged += (_, e) => Status.Text = e.IsResolved ? "已解決" : "已重新開啟";

thread.Resolve();
thread.Reopen();
```

### 變更與持久化

`CommentsChanged` 以 `CommentThreadChange` 的形式回報每一項變更。請在伺服器上儲存該變更，而不要比較整棵樹。伺服器還必須檢查人們是否只編輯與刪除自己的留言，因為它接收到的只是用戶端回報的內容。

| 類型 | 已設定的欄位 | 意義 |
|---|---|---|
| `Reply` | `Comment`、`ParentId`（頂層留言為 null） | 寫入了一則新留言或回覆 |
| `Edit` | `Id`、`Body` | 留言的內文被變更 |
| `Delete` | `Id` | 留言被刪除 |
| `React` | `Id`、`Emoji`、`Added` | 新增了表情反應（`Added` 為 true）或移除了它（為 false） |

```csharp
thread.CommentsChanged += (_, e) =>
{
    if (e.Change.Kind == CommentThreadChangeKind.Edit)
    {
        // 為 e.Change.Id 儲存 e.Change.Body。
    }
};
```

### 窄版

寬度低於 420 px 時，輸入區不再顯示頭像，回覆的縮排由 24 px 改為 12 px。

<Demo name="Narrow" />

### 範例：定價頁面的設計審閱

SaaS 行銷頁面中定價表的一次審閱。第一則留言獲得了目前使用者的一個表情反應，並在內文中以醒目樣式標示了對 Theo Walsh 的提及。回覆以一行回答了問題，最後一則留言已被編輯，因此顯示「已編輯」標記。頁首帶有所審閱元素的標題，以及帶有數量的副標題。留言串尚未解決，因此頁首中仍有「解決」按鈕。

![定價表設計審閱，包含一則提及、一則回覆、一個表情反應和一則已編輯的留言](images/DesignReview.png)

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc"
             xmlns:generic="using:System.Collections.Generic"
             Width="600">
    <arc:CommentThread Title="Pricing table spacing"
                       Placeholder="Reply to the thread">
        <arc:CommentThread.CurrentUser>
            <arc:CommentAuthor Id="ana" Name="Ana Ruiz" />
        </arc:CommentThread.CurrentUser>
        <arc:CommentThread.Comments>
            <generic:List x:TypeArguments="arc:ThreadComment">
                <arc:ThreadComment Id="c1"
                                   Body="The annual toggle no longer lines up with the plan names on mobile. @Theo Walsh can we fix it before the release?"
                                   CreatedAt="2h">
                    <arc:ThreadComment.Author>
                        <arc:CommentAuthor Id="mina" Name="Mina Park" />
                    </arc:ThreadComment.Author>
                    <arc:ThreadComment.Reactions>
                        <generic:List x:TypeArguments="arc:CommentReaction">
                            <arc:CommentReaction Emoji="👍">
                                <arc:CommentReaction.UserIds>
                                    <generic:List x:TypeArguments="x:String">
                                        <x:String>ana</x:String>
                                        <x:String>theo</x:String>
                                    </generic:List>
                                </arc:CommentReaction.UserIds>
                            </arc:CommentReaction>
                        </generic:List>
                    </arc:ThreadComment.Reactions>
                    <arc:ThreadComment.Replies>
                        <generic:List x:TypeArguments="arc:ThreadComment">
                            <arc:ThreadComment Id="c2"
                                               Body="Yes, I will move the toggle up with the plan names in the next build."
                                               CreatedAt="1h">
                                <arc:ThreadComment.Author>
                                    <arc:CommentAuthor Id="theo" Name="Theo Walsh" />
                                </arc:ThreadComment.Author>
                            </arc:ThreadComment>
                        </generic:List>
                    </arc:ThreadComment.Replies>
                </arc:ThreadComment>
                <arc:ThreadComment Id="c3"
                                   Body="Checked at 375 px. The gap between the price and the unit is 8 px now."
                                   CreatedAt="45m"
                                   IsEdited="True">
                    <arc:ThreadComment.Author>
                        <arc:CommentAuthor Id="ana" Name="Ana Ruiz" />
                    </arc:ThreadComment.Author>
                </arc:ThreadComment>
            </generic:List>
        </arc:CommentThread.Comments>
    </arc:CommentThread>
</UserControl>
```

場景中的留言以內嵌方式保留，這是靜態圖片所必需的。在應用程式中，它們來自檢視模型，留言串透過 `CommentsChanged` 回報每一項變更。

### 文案規範

- **Title**：點出所審閱的物件（「價格表間距」「第 4.2 條終止條款」），而不是討論本身。
- **Placeholder**：邀請人們回覆此留言串（「回覆此留言串」）。預設的「回覆，或 @提及某人」適合每天都使用提及的團隊。
- **留言**：用完整的句子寫清觀點與問題。只有當某人必須採取行動時，才以「@」提及他。
- **空狀態**：預設的「尚無留言。在下方開始討論。」適合審閱工具。若產品不是審閱工具，請讓文字簡短些。
- **其他語言**：為操作、計數與播報設定 `Labels`。計數與名稱是函式，因此複數規則應放在其中。

| 不要寫 | 改為 |
|---|---|
| 留言串 1 | 價格表間距 |
| 在這裡回覆… | 回覆此留言串 |
| 1 則留言們 | 1 則留言 |
| 刪除？ | 刪除此留言？ |
| 留言串已關閉 | 已解決 |

### 最佳做法

**建議**

- 在顯示留言串之前，把 `CurrentUser` 設為已登入的人。「編輯」與「刪除」跟隨這個身分，錯誤的使用者會顯示錯誤的操作。
- 在伺服器上儲存每一個 `CommentThreadChange`，並在伺服器上檢查人們是否只變更了自己的留言。
- 為每則留言提供穩定的 `Id`。留言樹變動時，留言串會依 id 重用每則留言的檢視。
- 問題得到解答後解決留言串，使已解決標籤顯示結果，並把討論收合起來。
- 把 `People` 設為該物件的審閱者，讓建議優先顯示正確的人。

**避免**

- 透過比較兩棵樹來找出變動。`CommentsChanged` 中的變更已經說明了發生什麼事。
- 人們輸入時把草稿寫入 `Comments`。只有已送出的留言才應進入樹中。
- 用同一個留言串承載多個物件。每個留言串只屬於一個物件，第二個物件需要第二個留言串。
- 從儲存空間中刪除帶有回覆的留言。改為將其標示為已刪除，使留言串保留占位，回覆也維持原位。
- 對於使用自有表情的產品，仍保留 `Reactions` 的預設值，例如只用勾號審閱的團隊。

### 常見問題

**為什麼別人的留言上沒有「編輯」或「刪除」？** 只有作者的 `Id` 與 `CurrentUser` 相同的留言才會顯示「編輯」與「刪除」。伺服器必須再次檢查。

**為什麼在新的留言串上「解決」沒有作用？** `Resolve()` 只有在留言串有留言時才會折疊它，因此空的留言串保持開啟。

**如何從程式碼開始回覆？** 以留言的 id 呼叫 `ReplyTo(id)`。它會把焦點移到輸入區，並且回覆狀態列會顯示該留言的作者。`CancelReply()` 取消進行中的回覆，`ReplyingToId` 回傳正在回覆的 id，若沒有則為 null。

**能否在伺服器接受之前保留一項變更？** 留言串會立即顯示變更。若要拒絕某項變更，請如上文所述，在處理常式中把 `Comments` 設回先前的樹。

## API 參考

<!-- api: CommentThread -->

<!-- api: ThreadComment -->

## 無障礙

- 留言串是一個具名區域（「留言串」，即 `Labels.Thread` 上的 `Label`）。每則留言是一個具名群組，包含作者與時間，例如「Mina Park，2 小時前」。已刪除的留言會被播報為「已刪除的留言」。
- 開啟提及建議時，輸入區是一個組合方塊，其展開狀態會暴露給輔助技術。
- 表情反應標籤是切換按鈕，其名稱包含計數以及你是否已回應，例如「讚，2，包括你」。
- 用於收合回覆的按鈕會暴露其展開狀態。僅有圖示的操作，例如編輯、刪除與回覆，都有名稱。
- 編輯表單名為「編輯留言」，其儲存按鈕名為「儲存」。

### 鍵盤

| 按鍵 | 操作 |
|---|---|
| `Enter`（在輸入區中） | 新增一行 |
| `Ctrl` + `Enter` 或 `Cmd` + `Enter` | 送出新留言，或儲存編輯 |
| `@` | 開啟提及建議 |
| `Up` / `Down`（在建議中） | 移動目前的建議；在兩端循環 |
| `Enter` 或 `Tab`（在建議中） | 插入目前的人員，格式為「@全名 」 |
| `Escape` | 先關閉建議；然後取消編輯、回覆、刪除確認或表情選擇器 |

留言中的操作與其他按鈕一樣可透過 `Tab` 取得焦點，因此鍵盤使用者無需指標即可到達「回覆」「編輯」與「刪除」。

## 動態效果

- 展開回覆、新增留言以及折疊已解決的留言串，都會以彈簧效果改變留言串的高度。
- 表情反應計數會滾動到新值，別人新增或移除表情時，標籤會隨之移動。
- 編輯表單、刪除提示與表情選擇器會平移滑入一小段距離。

| 變化 | 完整 | 減少 | 關閉 |
|---|---|---|---|
| 回覆展開或收合 | 留言串高度以彈簧過渡到新尺寸 | 內容原地變化 | 立即完成 |
| 新增留言 | 留言串高度以彈簧變化，留言隨之出現 | 內容原地變化 | 立即完成 |
| 留言串被解決或重新開啟 | 留言串以彈簧折疊為標籤，或展開 | 內容原地變化 | 立即完成 |
| 表情反應計數變化 | 計數滾動到新值 | 計數原地變化 | 立即完成 |
| 編輯表單、刪除提示或選擇器開啟 | 滑入 8 px | 內容原地變化 | 立即完成 |

減少動態效果會以原地變化取代移動，並關閉過渡。關閉動態效果則直接跳到最終狀態。

## 相關元件

- `ChatThread`：同時在線的人之間的即時對話。
- `Timeline`：跨多個物件的活動動態。
- `MentionInput`：帶提及建議的單一輸入欄位，沒有留言串。

| 元件 | 適用情境 |
|---|---|
| `ChatThread` | 人們輪流對話，並帶有輸入狀態與已讀回執 |
| `Timeline` | 歷程是跨多個物件的事件清單 |
| `MentionInput` | 你需要一個帶提及的輸入欄位，而不是討論 |
| `Textarea` | 撰寫單一備註，且沒有人回覆 |
| `Avatar` | 你需要留言串之外某人的頭像或名字縮寫 |
