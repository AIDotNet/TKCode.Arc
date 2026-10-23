---
title: AI 回答
summary: 像文章一樣回答一個問題的頁面：推理、來源卡片、串流的回答（含標題、清單、表格、程式碼與編號引用，被引用的句子會亮起），接著是動作、相關問題與追問方框。
source-hash: 871e3dc49657
---

<Demo name="Preview" />

## 何時使用

AI 回答適用於值得一頁篇幅的問題，而不只是一個對話泡泡。它會顯示答案是如何得出的（推理與來源），以完整的排版寫出回答，並讓人們核對每一項主張：指向引用時，會顯示該句話的出處。

- **搜尋或研究助理**：一個問題進去，一個附引用的回答出來，來源以卡片形式顯示在文字上方。
- **說明中心的解答**：頁面把問題保留為標題，因此可以分享或以連結複製。
- **必須核實的回答**：引用、來源卡片與它們所支持的句子會彼此點亮，雙向皆然。
- **可就地改進的回答**：重新回答、更簡短、更詳細或換用另一個模型，並在各版本之間翻頁。

## 何時不用

- **來回對話**：使用 `AiChat`。在這裡再問一次，會取代原本的回答。
- **只需要一個提示框**：使用 `AiComposer`。
- **沒有引用的純文字**：用 `Markdown` 呈現。
- **執行模型**：此區塊本身不會呼叫任何東西。它會詢問你的 `IAiAnswerResponder`，或顯示你交給它的回答。

## 用法

`AiAnswer` 衍生自 Avalonia 的 `TemplatedControl`，會填滿其容器，因此需要給它一個高度。設定 `Responder` 與 `Model`，或直接給它一個完成的 `Answer`。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:AiAnswer Height="720" Question="Which checks should block a merge during the move?">
        <arc:AiAnswer.Models>
            <arc:AiModel Id="lumen-pro" Name="Lumen Pro 4" />
            <arc:AiModel Id="lumen" Name="Lumen 4" />
        </arc:AiAnswer.Models>
    </arc:AiAnswer>
</UserControl>
```

### 以回應器回答

`IAiAnswerResponder` 接收問題、提問方式與模型，並以一串事件的形式回傳答案。

```csharp
public sealed class MyResponder : IAiAnswerResponder
{
    public async IAsyncEnumerable<AiAnswerEvent> RespondAsync(AiAnswerRequest request, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        yield return new AiAnswerSourcesEvent(sources);            // early, so the sources row fills while the model thinks
        yield return new AiAnswerReasoningEvent("The risk is not the new pipeline...");
        yield return new AiAnswerThoughtEvent();                   // the label turns to "Thought for 8s"
        await foreach (var chunk in StreamAnswerAsync(request, cancellationToken))
        {
            yield return new AiAnswerTextEvent(chunk);              // Markdown, of any size
        }
        yield return new AiAnswerDoneEvent(Tokens: 812, Duration: TimeSpan.FromSeconds(18.6), FollowUps: followUps);
    }
}
```

`request.Mode` 為 `Answer`、`Retry`、`Concise`、`Detailed` 或 `SwitchModel` 之一，`PreviousAnswerMarkdown` 則是被取代的回答。當人們停止回答、改問其他問題或離開時，這個權杖會被取消。擲出例外會使回答失敗，並顯示一則附有 "Try again" 按鈕（再試一次）的通知。

### 完成的回答

沒有回應器時，設定 `Question`、`Answer`（帶有 `[1]` 引用的 Markdown）、`Sources`、`Reasoning`、`Usage` 與 `FollowUps`，區塊會整體顯示它們：沒有逐字呈現，也沒有追問方框。

<Demo name="Static" />

### 推理與來源

模型思考時，推理列會閃爍顯示 "Thinking"（思考中）；文字開始時改為 "Thought for 8s"（思考了 8 秒），並可展開查看推理，即使推理仍在寫入，也能展開。
來源列最多顯示四個重疊的網站標記與 "N sources"（N 個來源）；展開後，來源是兩欄的卡片，若區塊較窄（576 及以下），則是一列可橫向捲動的卡片。
每張卡片都是連結：它會引發 `SourceInvoked`，除非你處理該事件，否則會開啟其 `Url`。`IsReasoningExpanded` 與 `IsSourcesExpanded` 都是雙向的。

<Demo name="Narrow" />

### 引用點亮句子

在文字中指向或聚焦一個引用時，它前面的句子、其來源卡片與引用的膠囊都會亮起；稍候片刻，會開啟一張預覽卡片，內含網站、標題、摘錄與網域。若改為指向來源卡片，則所有引用它的句子與膠囊都會亮起。在窄寬度下，亮起的卡片會捲動到可見範圍內。

### 版本

使用 "Try again"（再試一次）、"More concise"（更簡短）、"More detailed"（更詳細）或 "Switch model"（換模型，即 `Models` 中除目前模型以外的模型，列在子選單中）重新回答，會新增一個回答的**版本**。動作列中會出現一個翻頁器（"2 / 2"）；每個版本各自保有模型、文字、費用、評分，以及是否曾被停止。`CurrentVersionIndex` 是雙向的，`Versions` 則列出所有版本。

### 回饋

按下讚會立即記錄。按下倒讚會開啟一小段原因清單（`FeedbackReasons`，或來自 `Labels` 的預設值）；選擇一項會記錄評分並感謝人們，清單隨即自行關閉。
再按一次同一個評分會取消它；兩者互斥，且每個版本各自獨立。`FeedbackChanged` 會回報數值、原因、問題與版本。

### 複製與分享

"Copy answer"（複製回答）會把文字與其編號來源放到剪貼簿上。連結按鈕與 "Share thread"（分享對話串）會複製一個由**你的程式碼**透過 `ShareLinkProvider` 提供的連結：若沒有提供，這兩個按鈕都會隱藏，因為此區塊絕不會複製它自己編造的連結。`Copied` 會回報複製了什麼。

### 提問

答案下方的方框用來追問；相關問題（`FollowUps`，或回應器以 `AiAnswerDoneEvent` 送出的問題）會就地提問。提問會取代整頁：問題、標題與回答都會改變，先前的版本則會被捨棄。`QuestionAsked` 會先被引發，並附上其 `Origin`（發起來源）；設定 `Cancel` 則不提問。`Stop()` 或 Escape 會停止回答：已寫好的內容會保留，並附上一則說明可能不完整的通知，以及一個 "Try again" 按鈕。

<Demo name="Empty" />

### 空白頁面

沒有問題與回答時，頁面會顯示一個標題、一行說明，以及以清單呈現的 `Suggestions`；"New thread"（新對話串）與 "Share"（分享）會停用，方框則取得焦點。`NewThread()` 與 `NewThreadCommand` 會回到此頁。

### 文案

每個字都來自 `Labels`（`AiAnswerLabels`）：要翻譯或改寫時，替換該實例即可；格式化的文字是函式。設定 `Title` 後，它會優先於問題。

### 內容指引

- **問題**：完整的問題，就像人們會問的那樣。
- **回答**：先用一句話回答，接著是簡短的段落；引用的是主張，而不是段落。
- **來源**：人們認得的標題、網域、日期，以及一到兩句的摘錄。
- **推理**：幾句話說明模型接下來要做什麼，而不是逐字記錄。

### 最佳做法

- 在引用它們的文字之前先送出來源，並從 1 開始編號。
- 永遠傳遞取消權杖，讓停止真的能停止。
- 提供真正的分享連結，或完全不提供。
- 把相關問題控制在三到四個，且要是來源能夠回答的問題。

### 常見問題

**為什麼沒有對話？** 此區塊一次只回答一個問題；要進行對話請使用 `AiChat`。

**為什麼複製按鈕會包含來源？** 回答只有附上它的出處，才值得複製。

**為什麼看不到分享按鈕？** 沒有設定 `ShareLinkProvider`。

## API 參考

<!-- api: AiAnswer -->

<!-- api: AiAnswerVersion -->

### 事件

| 事件 | 引發時機 | 引數 |
|---|---|---|
| `QuestionAsked` | 問題即將被回答；設定 `Cancel` 則不提問 | `Question`, `Origin` |
| `FeedbackChanged` | 回答被評分、倒讚附上原因，或評分被取消 | `Value`, `Reason`, `Question`, `Version`, `VersionIndex` |
| `SourceInvoked` | 按下來源卡片或引用；設定 `Handled` 即可自行開啟 | `Item`, `FromCitation` |
| `Copied` | 有東西被複製 | `Kind` (`Answer`, `ShareLink`, `ThreadLink`, `Code`), `Text` |
| `StatusChanged` | 狀態改變 | `OldStatus`, `NewStatus` |

### 回應器

| 類型 | 說明 |
|---|---|
| `IAiAnswerResponder` | `RespondAsync(request, cancellationToken)` 以一串事件的形式回傳答案 |
| `AiAnswerRequest` | 問題、模式、模型，以及被取代的回答 |
| `AiAnswerReasoningEvent` | 更多推理文字 |
| `AiAnswerSourcesEvent` | 來源；請盡早送出 |
| `AiAnswerThoughtEvent` | 推理結束，文字開始 |
| `AiAnswerTextEvent` | 更多 Markdown 回答，長度不限 |
| `AiAnswerDoneEvent` | 回答完成，附上其費用與相關問題 |

## 無障礙

- 此區塊是名為 "AI answer"（AI 回答）的區域；問題是一個標題，當相關問題或建議提出新問題時會取得焦點。
- 推理列與來源列是按鈕，會回報是否展開；收合的內容會從 Tab 順序中移除。來源卡片是連結，名稱為 "Source 1: title, domain"（來源 1：標題、網域）。
- 引用依閱讀順序可聚焦；取得焦點時開啟其卡片，按 Escape 則關閉卡片，且不移動焦點。亮起的句子只是視覺輔助，卡片本身攜帶相同的資訊。
- 一則禮貌的即時區域會朗讀 "Thinking"（思考中）、"Writing the answer"（正在撰寫回答）、"Answer ready"（回答已就緒）或 "Answer stopped"（回答已停止）、失敗、複製、評分、評分變更與版本變更。串流的文字不會逐字朗讀，且回答在撰寫期間會被標記為忙碌。
- 動作按鈕都有名稱與工具提示；評分按鈕會回報是否被按下；版本翻頁器是一個名為 "Version 2 of 2"（第 2 版，共 2 版）的群組。
- 焦點會移到人們接下來會看的地方：選單之後回到重新回答按鈕，原因清單之後回到倒讚按鈕，相關問題提問之後則回到新問題。
- 狀態從不只靠顏色傳達：亮起的句子也有卡片與編號，失敗則有圖示與一句說明。
- 減少動態效果會保留每一種狀態與每一則朗讀。

### 鍵盤

| 按鍵 | 操作 |
|---|---|
| `Tab` | "New thread"（新對話串）、"Share"（分享）、推理與來源列、引用、動作列、相關問題、方框 |
| `Enter` 或 `Space` | 按下已聚焦的按鈕或卡片；在引用上，則開啟其來源 |
| `Escape` | 關閉引用卡片、選單或原因清單，且不移動焦點；在方框中，停止回答 |
| 選單中的 `ArrowDown` / `ArrowUp` | 在項目之間移動；字母會跳到相符的項目 |
| `ArrowRight` / `ArrowLeft` | 開啟與關閉 "Switch model"（換模型）清單 |
| 方框中的 `Enter` | 提問；`Shift+Enter` 新增一行 |

## 動態效果

- 串流文字以約每 50 毫秒一組完整單字釋出（約每步四個字），每一步在 160 毫秒內淡入；標題、清單項目、表格列與程式碼行則在整行到達後才出現。
- 推理與來源以彈簧展開，內容隨之淡入；指向來源標記時，標記會扇形展開。
- 亮起句子時，其背景會在 160 毫秒內淡入。
- 切換版本時回答會交叉淡入，高度以彈簧跟隨，翻頁器的數字則會滾動。
- 選單與原因清單從其按鈕長出來。
- 減少動態效果會保留單字的節奏，但移除淡入、彈簧與亮起的過渡；關閉時所有變化都會立即套用。

| 變化 | 完整 | 減少 | 關閉 |
|---|---|---|---|
| 新文字 | 每 50 毫秒一組完整單字，160 毫秒淡入 | 同樣的批次，無淡入 | 同樣的批次，無淡入 |
| 推理與來源 | 高度彈簧，淡入 | 120 毫秒淡入 | 立即 |
| 亮起的句子與卡片 | 160 毫秒 | 立即 | 立即 |
| 切換版本 | 交叉淡入；高度以彈簧變化 | 120 毫秒交叉淡入 | 立即 |
| 選單 | 從按鈕長出來 | 120 毫秒淡入 | 立即 |

## 相關元件

- `AiChat`：用於對話。
- `AiComposer`：用於提示框。
- `Markdown`：用於單獨的回答文字。
- `ModelPicker`：用於在其他地方選擇模型。

| 元件 | 適用情境 |
|---|---|
| `AiChat` | 人們來回對話 |
| `AiComposer` | 你只需要提示框 |
| `Markdown` | 你只需要呈現文字 |
| `ModelPicker` | 模型的選擇位於設定頁面 |
