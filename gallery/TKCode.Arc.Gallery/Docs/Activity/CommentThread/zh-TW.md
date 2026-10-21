---
title: 留言串
summary: 支援回覆、表情反應、提及、原地編輯與解決的巢狀留言。
source-hash: 775965f7a0c4
---

<Demo name="DesignReview" />

## 何時使用

- 針對文件、設計稿、工單或提取要求的審閱討論。
- 附著在某個物件上的回饋，回覆巢狀排列在它所回應的留言之下。
- 問題確認後，可由某人標記為已解決的串。

## 何時不用

- 雙方同時在線的即時對話：用 `ChatThread`。
- 跨多個物件的事件紀錄：用 `Timeline`。
- 只需要帶提及的輸入、沒有留言串：用 `MentionInput`。

## 用法

將 `CurrentUser` 設為閱讀留言串的人。他們可以編輯和刪除自己的留言。`Comments` 是留言樹，回覆巢狀排列在 `Replies` 之下。

```csharp
var ana = new CommentAuthor("ana", "Ana Ruiz");
var mina = new CommentAuthor("mina", "Mina Park");

var thread = new CommentThread
{
    Title = "首頁標題",
    CurrentUser = ana,
    Comments =
    [
        new ThreadComment("c1", mina, "新標題讀起來很順。@Theo Walsh 可以再把副標題精簡一下嗎？", "2h")
        {
            Replies =
            [
                new ThreadComment("c2", new CommentAuthor("theo", "Theo Walsh"), "好，精簡成一行。", "1h")
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

### 提及與人員

在編輯框中輸入 `@` 即可提示人員。`People` 列出可被提及的人。未設定時，會提供留言串中的參與者。

<Demo name="EmptyThread" />

`Reactions` 設定每則留言可用的表情。`MaxDepth` 限制巢狀層級：超出的回覆會掛到允許的最深父層之下。

### 解決與重新開啟

解決留言串後，它會收合為一個標籤，並保留參與者的資訊。`IsResolved` 為雙向繫結。

<Demo name="Resolved" />

```csharp
thread.ResolvedChanged += (_, e) => Status.Text = e.IsResolved ? "已解決" : "已重新開啟";

thread.Resolve();
thread.Reopen();
```

### 變更與儲存

`CommentsChanged` 會將每次變更回報為一個 `CommentThreadChange`，其 `Kind` 為 `Reply`、`Edit`、`Delete` 或 `React`。請將該變更儲存到伺服器，而不是比對整棵留言樹。伺服器還必須檢查人員只能編輯和刪除自己的留言。若要拒絕某次變更，請在處理常式中將 `Comments` 設回先前的值。

```csharp
thread.CommentsChanged += (_, e) =>
{
    if (e.Change.Kind == CommentThreadChangeKind.Edit)
    {
        // 為 e.Change.Id 儲存 e.Change.Body。
    }
};
```

### 窄寬度

寬度小於 420px 時，輸入框不再顯示頭像，回覆縮排 12px。

<Demo name="Narrow" />

## API 參考

<!-- api: CommentThread -->

<!-- api: ThreadComment -->

## 無障礙

- 編輯框是一個組合方塊：其提及建議是一個帶有目前項目的清單。每則留言是一個帶名稱的群組，包含作者與時間。已刪除的留言會唸為「已刪除的留言」。
- 表情標籤是切換按鈕。用於收合回覆的按鈕會公開其展開狀態。
- 僅有圖示的操作（如編輯、刪除和回覆）都有明確的名稱。
- Ctrl 或 Cmd + Enter 送出。上下方向鍵在建議項目之間移動，Enter 或 Tab 插入提及，Escape 會先關閉建議清單，再取消編輯或回覆。

## 動態效果

- 展開回覆、新增留言以及收合已解決的留言串時，串的高度以彈簧方式變化。
- 表情計數滾動到新值；他人新增或移除表情時，標籤隨之移動。
- 編輯表單、刪除確認和表情選擇器會以短距離滑入。
- 減少動態效果時，移動改為原地變化，並關閉轉場。關閉動態效果時直接到最終狀態。

## 相關元件

- `ChatThread`：雙方同時在線的即時對話。
- `Timeline`：跨多個物件的活動動態。
- `MentionInput`：只有帶提及建議的單一輸入，沒有留言串。
