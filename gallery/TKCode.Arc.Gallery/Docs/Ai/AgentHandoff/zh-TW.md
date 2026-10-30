---
title: Agent handoff
summary: AI 助理把對話轉交給真人的客服聊天：一張卡片顯示它交給團隊的摘要與一般的等候時間，接著真人加入，左側一條細時間軸標出每一步。
source-hash: a81b332d51fd
---

<Demo name="Preview" />

## 何時使用

`AgentHandoff` 是一段客服對話：AI 助理先回答，再把對話轉交給真人。轉接時，對話裡的一張卡片會顯示助理交給團隊的摘要與一般的等候時間。接著真人加入並接手，左側一條細時間軸標出每一步：助理、轉接、真人加入與問題解決。

- **客服小工具**：AI 先回答，需要時升級給真人客服。
- **讓客戶看到助理交給團隊的內容**，不必再說一次。
- **行銷或文件頁面**，說明從 AI 到真人的轉接流程。

## 何時不用

- **只有一個助理、沒有轉接的一般聊天**：用 `ChatThread`。
- **呈現代理的內部步驟或工具呼叫**：用 `AgentTimeline` 或 `ToolCall`。
- **給客服人員用的完整工單收件匣**：用清單與詳細資料版面組合。

## 用法

既沒有 `Stage` 也沒有模擬時，卡片是一個空對話：只有標題列和助理的步驟。要接上你的客服後端，設定 `Stage` 與資料，並處理事件：

```csharp
chat.Title = "Order NW-48213";
chat.Subtitle = "Northwind Home support";
chat.Summary = new AgentHandoffSummary(
    "Desk arrived with a cracked leg. Customer wants a full refund.",
    new AgentHandoffField("Order", "NW-48213"),
    new AgentHandoffField("Asks for", "Full refund"));
chat.Agent = new AgentHandoffAgent("Jordan Reyes", "Support specialist", "14:05");
chat.ShowsComposer = true;
chat.AllowsRequestHuman = true;
chat.SendRequested += (_, e) => backend.Send(e.Text);
chat.HumanRequested += (_, _) => backend.Escalate();

// Then follow the backend.
chat.Stage = conversation.Stage;
```

助理的訊息放在 `Messages`，轉接卡片之後的一切放在 `AgentMessages`：客戶等候時補充的說明，以及與真人的對話。加入步驟會放在真人的第一則訊息之前。

### 階段

<Demo name="YourBackend" />

由後端驅動 `Stage`：建立工單時設為 `Transferring`，有人接手時設為 `Connected`，對方結束對話時設為 `Resolved`，排隊逾時設為 `Unavailable`。

| 階段 | 標題列狀態 | 轉接卡片 |
|---|---|---|
| `Assistant` | 星芒圖示與「AI assistant」 | 不顯示；最後一則回答下方可以出現 Talk to a person |
| `Transferring` | 載入圈與「Transferring」 | 「Transferring you to a person」、一般的等候時間（`ReplyEstimate`）、Cancel |
| `Connected` | 人像圖示與「With Jordan」 | 「Jordan joined from Help desk」、接手的人、摘要 |
| `Resolved` | 勾號與「Resolved」 | 「Resolved by Jordan」；最後一步為時間軸收尾 |
| `Unavailable` | 警示圖示與「No one available」 | 「No one is available right now」、Try again |

### 動作與輸入框

每個動作要在你開啟對應屬性後才會顯示，並觸發一個冒泡的路由事件：

- `AllowsRequestHuman`：在助理最後一則已完成的回答下方顯示 Talk to a person 快速回覆，觸發 `HumanRequested`。
- `AllowsCancelTransfer`：轉接中在卡片上顯示 Cancel，觸發 `CancelTransferRequested`。
- `AllowsRetry`：無人接手時在卡片上顯示 Try again，觸發 `RetryRequested`。
- `ShowsComposer`：顯示輸入框。按 Enter 或傳送按鈕會觸發 `SendRequested`，附帶去除前後空白的文字；訊息要由你自己加到 `Messages` 或 `AgentMessages`。提示文字會隨階段改變（「Message Jordan」），除非你設定了 `Placeholder`。

### 串流輸出與輸入中

要串流輸出助理的文字，在設定了 `IsStreaming` 的訊息上更新 `Text`；卡片會讓每次附加的片段淡入。把 `Typing` 設為 `Assistant` 會顯示一行簡短的閃光 Thinking，設為 `Agent` 會顯示輸入中的圓點。

```csharp
var reply = new AgentHandoffMessage(AgentHandoffSender.Assistant, string.Empty) { IsStreaming = true };
chat.Messages.Add(reply);
await foreach (var chunk in stream)
{
    reply.Text += chunk;
}
reply.IsStreaming = false;
```

### 窄容器

<Demo name="Narrow" />

卡片填滿容器，最寬 560px；在 360px 寬時依然好用，內距更緊湊、對話泡泡更寬。卡片高度固定（預設 600，可設定 `Height`），對話在內部捲動並固定在最新訊息，所以對話變長時頁面不會跳動。過長的名稱與中繼資訊列會以刪節號截斷，訊息文字會換行。

### 標誌與大頭貼

`AssistantMark` 與 `ProviderMark` 接受一個 24 × 24 的描邊圖形（Lucide 圖示或你自己的路徑），分別用於第一步的助理與轉接卡片旁的客服系統；`AssistantMarkBrush` 與 `ProviderMarkBrush` 設定它們的顏色。接手者的大頭貼顯示姓名縮寫，或 `AgentHandoffAgent.Avatar` 中的相片。

### 模擬

`AgentHandoffSimulation` 為示範和原型播放一段依劇本進行的對話，就像上方的預覽。把台詞放進 `AgentHandoffScript`（留為 null 的台詞會被略過），再把它設為卡片的 `Simulation`：卡片在未設定 `Stage` 時播放它，每次顯示時從頭開始，輸入框以及 Talk to a person、Cancel 與 Try again 操作都能使用。`Scenario="Unavailable"` 會停在無人接手的時候，於是顯示 Try again；切換情境會讓故事從頭開始。它公開 `Stage`、`Messages`、`AgentMessages`、`Typing`、`Summary`、`Agent` 與 `Notice`，並接受 `RequestHuman`、`CancelTransfer`、`Retry` 與 `Send`。設定 `Stage` 後，卡片就回到你的資料。不要在示範以外使用它。

```xml
<arc:AgentHandoff Title="Order 1042" AssistantModel="Your model">
    <arc:AgentHandoff.Simulation>
        <arc:AgentHandoffSimulation Scenario="Unavailable" />
    </arc:AgentHandoff.Simulation>
</arc:AgentHandoff>
```

```csharp
chat.Simulation = new AgentHandoffSimulation
{
    Script = new AgentHandoffScript
    {
        CustomerOpening = "My order arrived damaged.",
        AssistantAnswer = "Sorry about that. I can send a replacement part.",
        CustomerRequest = "Can I talk to a person?",
        AssistantHandoff = "Of course, I'll pass this to our team with a summary.",
        Summary = new AgentHandoffSummary("Order arrived damaged; wants a refund."),
        Agent = new AgentHandoffAgent("Sam Lee", "Support specialist"),
        AgentGreeting = "Hi, I'm Sam. I've read the summary.",
    },
};
```

### 你自己的文案

`Labels` 包含卡片顯示或朗讀的每一個字詞。替換整個物件即可在地化或改寫：

```csharp
chat.Labels = new AgentHandoffLabels
{
    Transferring = "Übergabe",
    TalkToPerson = "Mit einer Person sprechen",
    With = name => "Mit " + name,
};
```

### 最佳做法

- `Summary` 放入你傳給客服系統的同一批事實，讓客戶看到分享了什麼。
- 對話很長時只傳最近的訊息；這張卡片是給進行中的對話用的，不是封存。
- 一次性的結果（例如「Refund issued」）用 `Notice`，它只會朗讀一次。

## API 參考

<!-- api: AgentHandoff -->

<!-- api: AgentHandoffMessage -->

<!-- api: AgentHandoffLabels -->

<!-- api: AgentHandoffSimulation -->

<!-- api: AgentHandoffScript -->

## 無障礙

- 對話區是一個名為「Conversation」的可聚焦記錄，鍵盤使用者可以捲動它；它不會朗讀每一個串流片段。
- 階段變化與 `Notice` 會透過禮貌的狀態區域朗讀一次，其中包括示範是模擬的這件事。
- 每個狀態都由圖示與文字搭配：Transferring、With Jordan、Resolved、No one available。顏色從來不是唯一的訊號。
- 摘要切換按鈕會回報展開或收合。輸入中的圓點有名稱，例如「Jordan is typing」。
- 時間軸上的標誌與大頭貼在可見文字旁只是裝飾。

### 鍵盤

| 按鍵 | 動作 |
|---|---|
| Tab | 依序移到對話區、Talk to a person、卡片上的動作、摘要切換按鈕與輸入框 |
| Up、Down、Page Up、Page Down、Home、End | 捲動取得焦點的對話區 |
| Enter | 傳送輸入框中的訊息，或按下取得焦點的按鈕 |
| Space | 摘要按鈕取得焦點時展開或收合摘要 |

## 動態效果

| 變化 | 完整 | 減少 | 關閉 |
|---|---|---|---|
| 助理回覆串流輸出 | 約每 34ms 一段，每段 3 到 8 個字詞；每段只做 140ms 的淡入 | 回覆整段出現 | 回覆整段出現 |
| 新列到來或文字換行 | 對話區以無回彈的 0.24s 彈簧跟隨量得的高度，往上推開而不跳動 | 立即 | 立即 |
| 轉接中 | 一個強調色小圓點沿著轉接卡片旁的時間軸往下移動 | 沒有移動的圓點 | 沒有移動的圓點 |
| 狀態文字變化 | 舊文字 100ms 淡出，新文字 160ms 淡入並上升 2px | 短暫交叉淡化 | 立即 |
| 輸入中的圓點、載入圈 | 呼吸、旋轉 | 保持靜止 | 保持靜止 |
| 模擬對話 | 播放並重複 | 只播放一次 | 只播放一次 |

離開畫面時循環會暫停。

## 相關元件

- `ChatThread`：與單一助理或真人之間的聊天。
- `AgentTimeline`：多代理執行的各個步驟。
- `ToolCall`：代理發出的工具呼叫。
