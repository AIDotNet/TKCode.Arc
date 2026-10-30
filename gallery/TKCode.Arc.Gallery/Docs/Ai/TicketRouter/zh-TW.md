---
title: Ticket router
summary: 支援訊息逐則送達，AI 分類器標註意圖、情緒與信賴度，每張工單都會移入有負責人的團隊泳道，或在複核泳道中等待一次點擊。
source-hash: 7fc5577c3e60
---

<Demo name="Preview" />

## 何時使用

`TicketRouter` 展示 AI 分類器如何分流進來的支援訊息。來自電子郵件、聊天、訊息工作區與服務台的訊息逐則送達。分類器為每則訊息標註意圖、情緒與信賴度，一道脈衝沿著連接線跑向目標團隊，工單隨即移入該團隊的泳道，由輪到的下一位成員接手。分類器沒有把握的工單會帶著它的建議停在複核泳道，在那裡點一下即可送出。

- **服務台與營運儀表板**：一個即時分流小工具，並為低信賴度保留人工複核泳道。
- **展示 AI 路由如何運作**：分類器讀到了什麼、有多確定、工單去了哪裡。
- **客服類產品的形象頁與新手導覽**，以 `Simulation` 循環播放一個由你提供的收件匣。

## 何時不用

- **完整的工單收件匣**（含搜尋與批次操作）：請使用表格或清單。
- **對話紀錄中的單一工具步驟**：請使用 `ToolCall`。
- **一般的多步驟代理進度**：請使用 `AgentStatus` 或 `AgentPlan`。

## 用法

```xml
<arc:TicketRouter />
```

`TicketRouter` 是 Special 套件中的 `TemplatedControl`（以 `UseArcSpecial()` 註冊）。它會填滿容器，最寬 840px；請放在約 360 到 880px 的內容欄中。沒有工單時，它會停在預設泳道（Billing、Engineering、Success 與 Needs review）中的「No tickets yet」。設定 `Tickets` 來路由你自己的訊息，或設定 `Simulation` 讓它自行播放一個收件匣。

### 模擬

`TicketRouterSimulation` 是 `Tickets` 為 null 時路由器播放的收件匣：分類器的 `Model` 名稱、`Lanes`、依到達順序排列的 `Tickets`（每則的 `Sample` 中是模擬分類器給出的答案），以及已在泳道中的 `Routed` 工單。它會循環播放，卡片離開螢幕時等待，並隨 `IsPaused` 暫停；它的播報會說明這是一次模擬執行、沒有送出任何內容。你自己設定的 `Tickets`、`Lanes`、`InitialRouted` 與 `Model` 優先於它；把 `Simulation` 設為 null 會回到空狀態重新開始。

```csharp
router.Simulation = new TicketRouterSimulation
{
    Model = "Your classifier",
    Tickets =
    [
        new TicketMessage("t-1", TicketChannel.Email, "Hannah Brooks", "Lumen Bakery", "Charged twice for the March invoice",
            "Our card shows two charges of $49 on the 3rd.")
        {
            Sample = new TicketClassification(TicketIntent.Billing, TicketSentiment.Negative, 0.94),
        },
    ],
};
```

本頁的示範從 `TicketRouterDemoData` 取得收件匣：人物、公司與訊息都是虛構的，分類器使用真實模型的名稱（核對於 2026-10-29）。

### 結構

1. **標頭**：上色方塊上的模型標記、標題、模型名稱、狀態（Routing、Paused、All caught up）以及暫停 / 繼續按鈕。
2. **送達的工單**：管道圖示與名稱、客戶與公司、送達時間、主旨以及開頭幾句話。每一行都只有一行高，並以刪節號結尾，所以長主旨永遠不會改變卡片高度。
3. **分類列**：分類器讀取時 "Classifying with Mistral Small 4" 閃爍，接著意圖、情緒與信賴度（一條短量條與百分比）依序淡入，末端是路由目標：團隊，或 "Needs review · Suggested Billing"。
4. **連接線**：從舞台引出的直線、橫跨各泳道的細線，以及伸入每條泳道的短線。
5. **泳道**：每條泳道的名稱與數量，下方是最新的幾列。一列顯示負責人的大頭貼（姓名縮寫）、主旨，以及意圖與客戶。複核列顯示信賴度與它將前往的泳道，並帶一個箭頭。

### 你的分類器

把 `Classifier` 設為你的模型呼叫。它接收訊息與取消權杖，傳回一個 `TicketClassification`（意圖、情緒、0 到 1 的信賴度）。流程暫停、卡片捲出畫面以及被卸載時，權杖都會被取消；流程恢復時會重新呼叫。擲出例外即顯示錯誤狀態："Couldn't classify"，並提供 Retry 與 Send to review。

<Demo name="YourClassifier" />

```csharp
router.Classifier = async (ticket, token) =>
{
    var reply = await http.PostAsJsonAsync("/api/classify", ticket, token);
    return (await reply.Content.ReadFromJsonAsync<TicketClassification>(token))!;
};
router.TicketRouted += (_, e) => helpdesk.Assign(e.Ticket.Message.Id, e.Ticket.LaneId, e.Ticket.Assignee);
router.ReviewConfirmed += (_, e) => helpdesk.Assign(e.Ticket.Message.Id, e.LaneId, e.Ticket.Assignee);
```

沒有分類器時，卡片會讀取每則訊息的 `Sample`，它的播報會說明這是一次模擬執行、沒有送出任何內容。沒有範例答案的訊息會被視為一個不確定的問題，進入複核。

### 泳道與路由

`Lanes` 由左到右列出泳道。每個 `TicketLane` 有 id、標籤、輪流接單的負責人（輪替）、先前已路由的工單數 `Count`，以及在收集低信賴度工單的泳道上設定的 `IsReview`。`Route` 為一次分類挑選泳道 id；預設的 `TicketRouter.DefaultRoute` 把帳務與退款送往 `billing`，缺陷送往 `engineering`，功能需求與問題送往 `success`。不符合任何團隊泳道的 id 會回退到第一條團隊泳道。沒有 `Lanes`（且沒有播放模擬）時，卡片會使用帶有這些 id 的預設泳道（`TicketRouter.BillingLaneId`、`EngineeringLaneId`、`SuccessLaneId` 與 `ReviewLaneId`），名稱來自標籤文字，沒有負責人。

低於 `ReviewThreshold`（預設 0.7）的工單會落入複核泳道，以挑選出的泳道作為建議，且沒有負責人。點擊它，或在它上面按 Enter 或 Space，就會把它送往該泳道、由那裡的下一位負責人接手，並觸發 `ReviewConfirmed`。`ConfirmReview(ticket)` 在程式碼中做同樣的事。

### 你的佇列

`Tickets` 依序接收你的訊息。除非設定 `Loop`，你自己的佇列不會循環，因此最後停在 "All caught up"；向 `ObservableCollection` 追加訊息，流程就會接著處理新工單。空佇列顯示 "No tickets yet"。`InitialRouted` 在首次顯示時填入泳道（最新的在前），`Restart()` 從頭開始，`RoutedTickets` 保留最新的 80 張工單。

### 暫停與速度

`IsPaused`（雙向）讓流程停在目前位置，並取消進行中的分類器呼叫；暫停 / 繼續按鈕會設定它，每次變化都會觸發 `PausedChanged`。在標記中設定 `IsPaused="True"` 即可以暫停狀態啟動。`Speed` 調整節奏的倍數（1 約為每張工單兩秒）。一個影格迴圈驅動整個流程，卡片不在畫面上時它會等待。

### 窄容器

<Demo name="Narrow" />

內容寬度低於 600px 時，四條泳道摺成 2 × 2 格線，每條最多兩列（較寬時 `MaxVisible` 為 3），連接線隱藏，標籤堆疊在路由目標上方。低於 400px 時，信賴度量條與模型名稱隱藏，百分比保留。

### 你自己的文字

`Labels` 包含卡片顯示或播報的每一個字詞，包括意圖、情緒、管道以及每一則播報：

```csharp
router.Labels = new TicketRouterLabels
{
    Routing = "Tri en cours",
    Intent = intent => intent == TicketIntent.Billing ? "Facturation" : intent.ToString(),
    AnnounceRouted = (subject, lane, assignee) => $"{subject} : {lane}",
};
```

`Title`、`Model` 與 `ModelMark`（24 × 24 描邊圖形，預設為閃光圖示）設定標頭。管道使用中性的圖示（信封、聊天泡泡、訊息方框、救生圈），從不使用品牌標誌。

### 內容指引

- 主旨是客戶寫下的原文；保持原樣，讓卡片去截斷。
- 泳道名稱保持一兩個字詞（"Billing"、"Needs review"）。
- 把門檻設在「路由錯誤的代價高於短暫等待」的位置。

### 最佳做法

- 把 `Classifier` 接到你的模型，把 `TicketRouted` 接到你的服務台；卡片本身從不送出任何內容。
- 善用複核泳道：低信賴度正是應該由人來決定的時刻。
- 把 `CancellationToken` 一路傳給你的 HTTP 呼叫，暫停才能真正停止它。

### 常見問題

**同一則訊息會落地兩次嗎？** 模擬循環時會；每次落地都是一個獨立的 `RoutedTicket`。

**複核泳道中的工單會計入某個團隊嗎？** 不會。在有人確認之前，它計入複核泳道。

## API 參考

<!-- api: TicketRouter -->

<!-- api: TicketMessage -->

<!-- api: TicketClassification -->

<!-- api: TicketLane -->

<!-- api: RoutedTicket -->

<!-- api: TicketRouterLabels -->

<!-- api: TicketRouterSimulation -->

<!-- api: TicketRoutedEventArgs -->

<!-- api: TicketReviewConfirmedEventArgs -->

<!-- api: TicketClassificationFailedEventArgs -->

<!-- api: TicketRouterPausedChangedEventArgs -->

## 無障礙

- 卡片以其標題命名。每條泳道以其標籤與工單數命名（"Billing, 14 tickets"）。
- 一個禮貌的即時區域會播報每次路由（"routed to Billing, assigned to Andre Williams"）、複核工單、失敗、暫停與確認；沒有設定分類器時，還會補充說明這是模擬執行。
- 意圖、情緒與複核狀態一律圖示與文字成對出現；信賴度以百分比讀出，從不只靠顏色。
- 複核工單是真正的按鈕，名稱包含主旨、信賴度與目標泳道。團隊列不是 Tab 停駐點。

### 鍵盤

| 按鍵 | 動作 |
|---|---|
| Tab | 在暫停按鈕、出現時的 Retry 與 Send to review，以及各張複核工單之間移動 |
| Enter、Space | 暫停或繼續、重試失敗的分類、送入複核，或確認複核工單的泳道 |

## 動態效果

| 變化 | 完整 | 減少 | 關閉 |
|---|---|---|---|
| 工單送達 | 舊卡片在 100ms 內淡出，新卡片在 160ms 內淡入並上升 4px | 淡入淡出，不上升 | 立即 |
| 分類中 | 標籤閃爍 | 靜止 | 靜止 |
| 標籤 | 以 50ms 的間隔依序在 160ms 內淡入並上升 2px | 淡入，不上升 | 立即 |
| 連接線 | 一道脈衝在 240ms 內畫向目標泳道，然後淡出 | 無 | 無 |
| 落地 | 列以位置彈簧（0.32s，無回彈）從舞台移入其泳道 | 原地出現 | 原地出現 |
| 列的進入與離開 | 高度以彈簧變化（0.24s，無回彈） | 立即 | 立即 |
| 落地醒目提示 | 列亮起強調色底色，並在 900ms 內淡去 | 保留 | 無 |
| 泳道計數 | 在預留的等寬位置中滾動 | 立即 | 立即 |

速度為 1 時每張工單約兩秒：送達 220ms、分類、標籤、240ms 的脈衝，然後落地。卡片不在畫面上時流程暫停。工單流動時卡片高度從不改變。

## 相關元件

- `AgentStatus`：單一執行中代理的即時狀態。
- `ToolCall`：對話紀錄中的單一工具步驟。
- `AgentPlan`：以清單呈現的代理計畫。
