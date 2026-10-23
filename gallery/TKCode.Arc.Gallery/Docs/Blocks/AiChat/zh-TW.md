---
title: AI 對話
summary: 會填滿其容器的助理對話：上方有標題選單、模型按鈕與新對話按鈕，對話串中有串流的 Markdown 回覆、可收合的推理、工具膠囊、編號引用與訊息動作，底部是訊息框。
source-hash: 6a840f2320f1
---

<Demo name="Preview" />

## 何時使用

AI 對話是助理的整個對話介面：過去對話的清單、對話串、模型選擇，以及人們撰寫內容的方框。回覆以串流方式抵達，會呈現模型在過程中做了什麼（推理、搜尋、查詢），並引用其來源。

- **產品的主要助理**：頁面或抽屜，人們在此提問、追問、重試並評分。
- **儀表板卡片中的多輪助手**：它會依自己的寬度調整，窄到手機大小的欄位也可以。
- **必須核對答案的地方**：引用會在指向時預覽其來源，工具膠囊可展開，顯示所用的搜尋詞或執行的查詢。
- **會被編輯的對話**：編輯訊息或重試回覆會產生該輪次的新版本；人們可以回到較早的版本。

## 何時不用

- **只需要提示框**：使用 `AiComposer`。
- **人與人之間的聊天**，有參與者與已讀回執：使用 `ChatThread`。
- **一個問題與一段長回答**：一頁以 `Markdown` 呈現的文字比較簡單。
- **在設定頁面選擇模型**：使用 `ModelPicker`。
- **執行模型**：對話本身不會呼叫任何東西。它會向你的 `IAiChatResponder` 詢問，或等待你的程式碼填入回覆。

## 用法

`AiChat` 衍生自 Avalonia 的 `TemplatedControl`，會填滿其容器，因此需要給它一個高度。設定 `Responder` 以回答，給它 `Models` 與對話，並處理你需要的事件。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:AiChat Height="640" ColumnWidth="760" GreetingText="What should we look at today?">
        <arc:AiChat.Models>
            <arc:AiModel Id="orbit-pro" Name="Orbit Pro 3" Note="Northwind Labs" />
            <arc:AiModel Id="quill-2" Name="Quill 2.4" Note="Fernwood AI" />
        </arc:AiChat.Models>
    </arc:AiChat>
</UserControl>
```

### 以回應器回答

`IAiChatResponder` 接收所顯示分支的訊息，並回傳一串事件的串流。對話會在每個事件抵達時顯示；你永遠不需要碰檢視。

```csharp
public sealed class MyResponder : IAiChatResponder
{
    public async IAsyncEnumerable<AiChatStreamEvent> RespondAsync(AiChatRequest request, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        yield return new AiChatReasoningDelta("Compare the stage timings before and after the jump.");
        yield return new AiChatReasoningEnd();
        yield return new AiChatToolUpdate("search", AiChatToolKind.Search, AiChatToolStatus.Running, "Searching the docs");
        // ... the same id again with Status Done, a summary, the queries and the sources
        yield return new AiChatSourcesEvent(sources);              // before the text that cites them
        await foreach (var chunk in model.StreamAsync(request, cancellationToken))
        {
            yield return new AiChatTextDelta(chunk);                // any size; the chat shows whole words on a steady clock
        }
    }
}
```

事件包括：推理用 `AiChatReasoningDelta` 與 `AiChatReasoningEnd`，工具呼叫用 `AiChatToolUpdate`（相同 id 的更新會改變同一個工具），來源用 `AiChatSourcesEvent`，回覆文字用 `AiChatTextDelta`，失敗用 `AiChatErrorEvent`。擲出例外的效果與錯誤事件相同。當人們停止回覆時，傳入的 `cancellationToken` 會被取消。

<Demo name="Empty" />

### 對話串

會話是一個 `AiChatConversation`，有標題與其 `Turns`。每個 `AiChatTurn` 有多個版本；一個版本包含提示（`AiChatUserMessage`：文字與附件）、回覆（`AiChatReply`），以及在其後接續的輪次。所顯示的是**可見分支**：每個輪次所選的版本，再接續到其後續。回應器只會收到該分支。

回覆帶有其狀態（思考中、串流中、完成、已停止或失敗）、推理、工具呼叫（`AiChatTool`）、來源（`AiSource`）、文字與人們的回饋。文字是 Markdown 的子集：段落、清單、短標題、粗體、行內程式碼、程式碼區塊，以及依來源抵達順序編號的 `[1]` 引用。

### 推理、工具與引用

模型思考時，回覆的開頭會顯示閃爍的 "Thinking"（思考中）；完成後顯示 "Thought for 3s"（思考了 3 秒），並可展開以查看推理。每個工具呼叫是一個膠囊：執行中（轉圈，尚不可展開）、已完成（圖示、來源標記與簡短摘要，例如 "3 sources"（3 個來源））或失敗。搜尋膠囊展開為其搜尋詞與編號來源；查詢膠囊展開為其陳述式與結果；自訂工具則展開為你自己的範本（`ToolDetailTemplate`）。

文字中的 `[1]` 會變成一個小型編號藥丸。指向或聚焦它會顯示一張卡片，內含網站、標題與摘錄；按下它會引發 `CitationInvoked`，除非你處理該事件，否則會開啟來源的 `Url`。

### 版本

對已完成的回覆重試，會為該輪次新增一個版本；編輯訊息並送出，同樣會新增版本。動作列中會出現切換器（"2 / 2"），版本之後的輪次屬於該版本，因此回到版本 1 會帶回它的後續。對失敗的回覆重試會在原地再執行一次，且不新增版本。

<Demo name="Versions" />

### 停止與失敗

回覆生成期間，傳送鈕會變成停止鈕，而方框中按 Escape 也會停止。已停止的回覆會保留已顯示的內容，並顯示 "Stopped"（已停止）或 "Stopped before answering"（回答前已停止）；失敗的回覆會顯示錯誤卡片，附一個 "Retry" 按鈕。兩者都保留動作列，失敗的回覆除外，它沒有動作列。

### 沒有回應器

沒有 `Responder` 時，送出的訊息只會出現在對話串中，並引發 `MessageSent`；你的程式碼會填入它所取得的 `AiChatReply`，檢視會跟隨其變化。

<Demo name="HostDriven" />

### 會話、模型與新對話

標題按鈕會開啟 `Conversations` 的清單，依 `Group` 分組，"New chat"（新對話）排在最前。開啟其中一個會引發 `ConversationOpened`；每個會話保有自己的捲動位置，已完成的回覆不會重新播放。模型按鈕會開啟 `Models`；`SelectedModel` 是雙向的，`ModelChanged` 回報變更。新回覆使用所選的模型；既有的回覆則保留寫下它的模型標記。"New chat" 會引發 `NewChatRequested`；若沒有任何處理，對話會清空 `CurrentConversation`，並顯示空的對話與其問候語，以及最多三個 `Suggestions`。空對話中的第一則訊息會建立一個會話（`ConversationCreated`），其標題取自前六個詞。

### 寬度

對話會依自己的寬度調整：在 480 及以下時，氣泡變寬，來源網域隱藏；在 360 及以下時，按鈕變得更緊湊。`ColumnWidth`（760）設定輪次欄的寬度。

<Demo name="Narrow" />

### 文案

所有文字都來自 `Labels`（`AiChatLabels`）：要翻譯或改寫，就替換這個實例；格式化的文字是函式。設定 `GreetingText` 與 `PlaceholderText` 時，它們優先於 labels。

### 文案規範

- **問候語**：一個邀請人們執行任務的問題，例如 "What should we look at today?"（今天我們要看什麼？）。
- **建議**：最多三個，每個都是真實的任務，以人們會說的方式表達。
- **工具標籤**：執行中用現在式的動詞，例如 "Searching the docs"（正在搜尋文件）；完成時用過去式，例如 "Searched the docs"（已搜尋文件）。摘要要簡短，例如 "3 sources"、"212 rows"（212 列）、"Timed out"（已逾時）。
- **來源**：人們認得的標題、網域，以及一到兩句的摘錄。

### 最佳做法

- 在引用它們的文字之前送出來源，並依該順序從 1 為引用編號。
- 工具摘要要簡短；細節放在面板中。
- 讓人們能停止：務必把取消權杖傳遞下去。
- 不要隱藏失敗：錯誤事件會給人們一個 "Retry"，在原地再執行一次。

### 常見問題

**為什麼送出的訊息沒有得到回答？** 沒有 `Responder` 時，對話會等待你的程式碼填入回覆。

**切換會話會停止回覆嗎？** 不會。執行中的回覆會繼續執行，而已完成的回覆在你回來時會立即顯示。

**我可以顯示自己的工具細節嗎？** 可以：種類為 `Custom` 的工具會以 `ToolDetailTemplate` 繪製，並以該工具作為資料內容。

## API 參考

<!-- api: AiChat -->

<!-- api: AiChatConversation -->

<!-- api: AiChatTurn -->

<!-- api: AiChatTurnVersion -->

<!-- api: AiChatUserMessage -->

<!-- api: AiChatReply -->

<!-- api: AiChatTool -->

<!-- api: AiSource -->

<!-- api: AiChatSuggestion -->

### 事件

| 事件 | 引發時機 | 引數 |
|---|---|---|
| `MessageSent` | 人們送出訊息，包括編輯後送出的 | `Conversation`, `Turn`, `Prompt` |
| `ReplyCompleted` | 回覆結束：完成、已停止或失敗 | `Reply` |
| `FeedbackChanged` | 回覆被評分，或評分被收回 | `Reply`, `Value` |
| `ModelChanged` | 人們選擇另一個模型 | 舊模型與新模型 |
| `NewChatRequested` | 人們要求新對話；設定 `Handled` 即可自行處理 | |
| `ConversationOpened` | 從選單開啟一個會話 | `Conversation` |
| `ConversationCreated` | 空對話中的第一則訊息建立一個會話 | `Conversation` |
| `CitationInvoked` | 按下引用或來源列；設定 `Handled` 即可自行開啟 | `Item` |

### 回應器

| 類型 | 說明 |
|---|---|
| `IAiChatResponder` | `RespondAsync(request, cancellationToken)` 以事件串流回傳回覆 |
| `AiChatRequest` | 會話、所選模型，以及所顯示分支的訊息，結尾是要回答的提示 |
| `AiChatReasoningDelta` / `AiChatReasoningEnd` | 更多推理文字；推理的結束，可附帶選擇性的時長 |
| `AiChatToolUpdate` | 工具呼叫開始或變更（相同的 `Id` 會更新同一個工具） |
| `AiChatSourcesEvent` | 文字將引用的來源 |
| `AiChatTextDelta` | 更多回覆文字，大小不拘 |
| `AiChatErrorEvent` | 回覆失敗，附上要顯示的訊息 |

## 無障礙

- 對話是名為 "AI chat" 的區域；對話串是輪次的清單，每則訊息與回覆都以其作者命名。
- 標題按鈕名為 "Chats, current: …"，模型按鈕名為 "Model: …"；兩者都回報它們會開啟選單，該選單具有一個已勾選的項目。
- 一個禮貌的即時區域會宣告回覆已開始（例如 "Orbit Pro 3 is replying"（Orbit Pro 3 正在回覆））、已結束、已停止或失敗，並宣告複製、評分、版本變更、新模型，以及會話已開啟。串流中的文字不會逐字宣告。
- 推理按鈕回報它是否已展開；工具膠囊回報其狀態，執行中無法展開；版本切換器是名為 "Version 2 of 2"（第 2 版，共 2 版）的群組。
- 引用會依閱讀順序取得焦點，取得焦點時開啟其卡片，並以 Escape 關閉，焦點不移動。
- 動作按鈕有名稱與提示；它們在懸停與鍵盤焦點時出現，且不佔用額外空間。
- 焦點會回到人們原本所在之處：選擇對話後回到標題按鈕；送出或取消編輯後回到 "Edit" 按鈕；按下 "New chat" 後回到方框。
- 減少動態效果會保留每一種狀態與每一則宣告。

### 鍵盤

| 按鍵 | 操作 |
|---|---|
| `Tab` | 標題按鈕、模型按鈕、新對話、對話串的控制項（依閱讀順序）、訊息框 |
| 按鈕上的 `Enter` 或 `Space` | 按下它；在標題或模型按鈕上，開啟選單 |
| 選單中的 `ArrowUp` / `ArrowDown` | 在列之間移動；按字母鍵跳到相符的列 |
| 訊息框中的 `Enter` | 送出（回覆生成中或輸入法組字中不會） |
| `Shift+Enter` | 加入一行 |
| 訊息框中的 `Escape` | 停止正在生成的回覆 |
| 訊息編輯區中的 `Enter` / `Escape` | 送出編輯 / 取消編輯 |
| 引用上的 `Escape` | 關閉其卡片，焦點不移動 |

## 動態效果

- 串流文字以 34 ms 為節拍，以整個詞為單位釋出（約每步三個詞），每一步以 150 ms 淡入；已完成的回覆立即顯示。
- 推理與工具細節以彈簧展開，文字以 160 ms 淡入、100 ms 淡出。
- 膠囊與標籤以彈簧取得所需寬度；"Thinking" 會閃爍，直到推理結束。
- 對話串會跟隨底部的新內容，每一影格縮短剩餘距離的五分之一；向上捲動會停止跟隨，跳轉按鈕會讓它回來。
- 切換會話時，對話串以交叉淡化轉換；版本以新的高度淡入。
- 減少動態效果會移除彈簧與移動，並保留以批次方式出現的文字，但不淡入；關閉則立即套用每項變更。

| 變化 | 完整 | 減少 | 關閉 |
|---|---|---|---|
| 新文字 | 每 34 ms 一整批詞，150 ms 淡入 | 相同批次，不淡入 | 相同批次，不淡入 |
| 推理、工具細節 | 高度彈簧，淡入淡出 | 120 ms 淡入淡出 | 立即 |
| 跟隨底部 | 追到末端 | 跳到末端 | 跳到末端 |
| 會話切換 | 100 / 160 ms 交叉淡化 | 120 ms 交叉淡化 | 立即 |
| 閃爍、轉圈 | 移動 | 靜止 | 靜止 |

## 相關元件

- `AiComposer`：用於只有提示框、沒有對話串的情境。
- `ModelPicker`：用於單獨選擇模型。
- `AgentMemory`：用於查看助理記住了什麼。
- `Markdown`：用於單獨呈現回覆的文字。

| 元件 | 適用情境 |
|---|---|
| `AiComposer` | 你需要的是方框，而非整個對話 |
| `ChatThread` | 對話是在人與人之間進行 |
| `ModelPicker` | 模型選擇位於設定頁面 |
| `Markdown` | 你只需要呈現文字 |
