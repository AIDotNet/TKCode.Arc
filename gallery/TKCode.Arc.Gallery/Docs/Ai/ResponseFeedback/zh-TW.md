---
title: Response feedback
summary: 針對 AI 回覆的意見回饋控制項：按倒讚會在行內展開一個附原因標籤與備註的面板，按讚會詢問哪裡做得好，送出後短暫顯示感謝，隨後收回到操作列中。
source-hash: 7d49fffaded3
---

<Demo name="Preview" />

## 何時使用

`ResponseFeedback` 是一整張 AI 回覆卡片：泡泡中的使用者訊息、模型的標誌與名稱、回覆內容，以及包含複製、可選的重新產生和讚與倒讚的操作列。`FeedbackActions` 是同一個控制項的單獨版本，用於你已經自行繪製的訊息列。按倒讚會在行內展開一個面板，其中有原因標籤和可選的備註；按讚會填滿圖示並詢問哪裡做得好；送出後顯示簡短的感謝，接著面板收回到操作列中。

- **對 AI 回覆按讚或倒讚**：聊天、Copilot 或搜尋答案中。
- **結構化的原因**（不正確、不安全），用於模型評估或客服分流。
- **訊息操作列**：在意見回饋旁提供複製與重新產生。

## 何時不用

- **對產品或工作階段的星級或數字評分**：使用評分控制項。
- **對貼文的表情回應**：使用 `Reactions`。
- **與單則回覆無關的一般產品意見**：使用意見回饋表單。
- **包含許多問題的長問卷**：使用表單或對話框。

## 用法

沒有資料時，`ResponseFeedback` 只顯示框架：模型標誌、空白的回覆與操作列；在你設定 `FeedbackHandler` 之前，意見回饋為模擬傳送。傳入你的回覆、模型名稱、它所回答的問題與要複製的文字，並傳入 `MessageId`，讓每次送出都能與該訊息一起儲存：

```xml
<arc:ResponseFeedback ModelName="Claude Sonnet 5.5"
                      Prompt="How do I rotate an API key?"
                      CopyText="Create a new key, deploy it, then revoke the old one."
                      MessageId="msg_0192"
                      AllowsRegenerate="True">
    <TextBlock TextWrapping="Wrap" Text="Create a new key, deploy it, then revoke the old one." />
</arc:ResponseFeedback>
```

`Prompt`、`ModelName` 與 `CopyText` 預設為 null：在你設定之前，問題、名稱與複製都保持隱藏。`ModelMark` 接受一個 24 × 24 的描邊圖形，以 16px 繪製（預設為中性的閃光圖示）。

### 你自己的訊息列

<Demo name="OwnRow" />

`FeedbackActions` 就是單獨的操作列。`Leading` 放在讚與倒讚之前的操作（在 36px 的小按鈕中使用 16px 圖示，例如僅圖示的 `CopyButton`），`Trailing` 固定在列尾，用來顯示時間或權杖數。

### 結構

1. **讚與倒讚**：兩個 36px 的圓形切換按鈕。啟用的那個會在外框之上填滿，因此沒有任何東西移動。
2. **面板**：按倒讚後顯示 "What went wrong?" 與關閉按鈕、原因標籤、備註，以及底部的隱私說明與 Cancel、Submit 按鈕。按讚後顯示較輕量的 "What was good?" 列，附 Skip 與 Send。
3. **感謝**：一個只繪製一次的勾號、"Thanks for your feedback" 與第二行文字；停留 `ThanksDuration`（1.6 秒）後面板收起。
4. **錯誤列**：操作按鈕旁的圖示與簡短訊息；Submit 會變成 Try again。評分儲存失敗時，操作列中會顯示同樣的錯誤列，並附有它自己的 Try again。

### 儲存意見回饋

<Demo name="Persisting" />

設定 `FeedbackHandler`。每次按下讚或倒讚時，它都會收到一筆 `Rating` 送出；送出原因或備註時，會收到一筆 `Details` 送出。擲回例外即可顯示錯誤與 Try again；面板關閉或評分改變時權杖會被取消，所以請把它傳給你的請求：

```csharp
reply.FeedbackHandler = async (feedback, cancellation) =>
{
    using var response = await http.PostAsJsonAsync("/api/feedback", new
    {
        feedback.MessageId,
        stage = feedback.Stage.ToString(),
        rating = feedback.Rating.ToString(),
        feedback.Reasons,
        feedback.Comment,
    }, cancellation);
    response.EnsureSuccessStatusCode();
};
```

在 `Rating` 階段儲存評分（`None` 表示使用者清除了評分），在 `Details` 階段附加原因與備註。沒有處理常式時傳送是模擬的：感謝列與播報都會說明沒有傳送任何內容。無論哪種情況，每次送出都會引發 `FeedbackSubmitted`，每次按下讚或倒讚都會引發 `RatingChanged`。

`Rating` 是雙向的：設定它可以顯示從資料庫讀取的評分，繫結它則可以自行掌管狀態。從外部設定的評分只會勾選對應按鈕，不會開啟面板，也不會傳送任何內容。

### 原因與備註

<Demo name="Reasons" />

`NegativeReasons` 與 `PositiveReasons` 是原因標籤。`PositiveReasons` 為空時，按讚後不顯示後續列。標為 Other 的標籤會把焦點移到備註並更換其預留位置文字。`AllowComment="False"` 會移除備註，`CommentPlaceholder` 與 `CommentMaxLength`（1000）決定它的樣子，`PrivacyNote`（null 時隱藏）是 Submit 旁那行低調的說明。

### 回覆仍在串流時

<Demo name="Streaming" />

回覆仍在串流時設定 `IsRatingEnabled="False"`，讓使用者評價的是完成後的答案。

### 你自己的用語

`Labels` 包含控制項顯示或播報的每一個字詞。替換整個物件即可在地化或改寫：

```csharp
reply.Labels = new ResponseFeedbackLabels
{
    GoodResponse = "Bonne réponse",
    BadResponse = "Mauvaise réponse",
    NegativeTitle = "Qu'est-ce qui n'allait pas ?",
    Submit = "Envoyer",
};
```

### 窄容器

寬度低於 480px 時卡片內距為 16px，以上為 24px；原因標籤會換行，因此在 360px 下也不需要水平捲動。寬度低於 400px 時，隱私說明與 Cancel 會讓出位置（Close 代替 Cancel），因此錯誤列與 Try again 一定能放在同一列。觸控操作之後，原因標籤的高度從 28px 增加到 32px。

### 內容準則

- 原因保持一兩個詞（"Too long"、"Unsafe"），這樣在 360px 下標籤只佔一兩行。
- 在隱私說明中用幾個字說明會分享什麼。
- 感謝列感謝使用者並說明接下來會發生什麼，不再索取更多內容。

### 最佳做法

- 單則助理回覆及其操作使用 `ResponseFeedback`；自行繪製訊息時使用 `FeedbackActions`。
- 一律傳入 `MessageId`，並保存兩個階段。
- 在處理常式中擲回例外以顯示錯誤；控制項會處理 Try again，並透過權杖中止過期的呼叫。

### 常見問題

**使用者可以改變心意嗎？** 可以。按下另一個按鈕會切換評分與面板；再次按下目前啟用的按鈕會清除評分。每次按下都會傳送。

**Escape 有什麼作用？** 它會關閉開啟的面板，並把焦點交還給開啟它的那個按鈕。評分保持不變。

## API 參考

<!-- api: ResponseFeedback -->

<!-- api: FeedbackActions -->

<!-- api: FeedbackSubmission -->

<!-- api: ResponseFeedbackLabels -->

<!-- api: FeedbackSubmittedEventArgs -->

<!-- api: FeedbackRatingChangedEventArgs -->

## 無障礙

- 讚與倒讚是會回報按下狀態的切換按鈕，名稱為 "Good response" 與 "Bad response"，工具提示相同。
- 原因標籤是切換按鈕，位於以面板標題命名的群組中。選取的標籤會反轉為實心填滿，因此狀態從不依賴色相。
- 以鍵盤按下倒讚會把焦點移到第一個原因；以指標按下則焦點維持不變。以鍵盤送出後，焦點落在感謝列上，面板收起時回到按鈕。
- 一個禮貌（polite）的即時區域會播報評分變化、傳送中、感謝狀態與錯誤，並在傳送為模擬時說明沒有傳送任何內容。
- 錯誤顯示在操作按鈕旁，附有圖示與文字，從不只靠顏色。

### 鍵盤

| 按鍵 | 操作 |
|---|---|
| Tab | 依序經過複製、重新產生、讚與倒讚，然後進入開啟的面板 |
| Enter、Space | 按下讚或倒讚、切換原因標籤，或啟用 Submit、Send、Skip 與 Cancel。再次按下目前啟用的按鈕會清除評分 |
| Escape | 關閉開啟的面板，並把焦點交還給開啟它的按鈕。評分保持不變 |
| Cmd + Enter、Ctrl + Enter | 在備註欄中送出 |

## 動態效果

| 變化 | 完整 | 減弱 | 關閉 |
|---|---|---|---|
| 面板開啟或關閉 | 量測到的高度以彈簧變化（0.24 秒，無回彈），同時面板在 160ms 內淡入淡出 | 高度立即變化，面板在 160ms 內淡入淡出 | 立即 |
| 表單切換為感謝 | 一次交叉淡化：舊內容 100ms 淡出，新內容 160ms 淡入並上升 2px，同時高度以彈簧過渡到新內容 | 120ms 交叉淡化，高度立即變化 | 立即 |
| 按鈕填滿 | 實心圖形在外框之上 160ms 淡入；沒有任何東西移動或改變尺寸 | 同樣的淡入 | 立即 |
| 按下 | 立即縮放到 0.94，放開時以彈簧恢復 | 不縮放 | 不縮放 |
| 成功勾號 | 在 280ms 內繪製一次 | 立即顯示 | 立即顯示 |

按鈕的尺寸從不改變，操作列隨容器延展，因此按鈕填滿或面板開啟時列寬不會變化。所有結束狀態與播報在每個動態效果等級下都相同。

## 相關元件

- `ReasoningBlock`：回覆上方模型的思考過程。
- `ToolCall`：代理程式呼叫的工具。
- `CopyButton`：在你自己的訊息列中提供複製操作。
