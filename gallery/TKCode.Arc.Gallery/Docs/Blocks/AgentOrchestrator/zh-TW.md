---
title: 智慧體編排
summary: 一個主智慧體把工作分給多個子智慧體，並即時展示：在一張圖上，每個任務、問題和結果都化作一個圓點在智慧體之間移動；另有訊息記錄，以及顯示每個智慧體的任務、當前步驟和結果的詳情面板。
source-hash: bf1670f71367
---

<Demo name="Preview" />

## 何時使用

智慧體編排在一次多智慧體執行進行時把它展示出來。主智慧體接到一個目標，把它拆成多個任務，再把每個任務交給一個子智慧體。卡片把主智慧體畫在上方，下方每個子智慧體一個節點，主智慧體到每個子智慧體之間各有一條線。兩個智慧體之間的每則訊息都會作為一個圓點沿線移動：任務向下，結果向上返回，兩個子智慧體互相提問時則橫向移動。每個節點顯示智慧體的名稱、角色、狀態（空閒、思考中、工作中、等待中、完成或失敗）以及它現在正在做的事。圖的下方是按順序列出全部訊息的記錄，詳情面板顯示所選智慧體的任務、當前步驟、訊息數量和結果。

- **智慧體產品中的執行檢視**：人們可以看到誰在做什麼，以及執行卡在了哪裡。
- **可以重播的執行記錄**：把記錄下來的步驟作為 `Script` 傳入，然後播放、暫停或重設。
- **即時執行**：用 `Post` 從你自己的事件中發送每則訊息；智慧體會按訊息類型自動更新。
- **多智慧體功能的演示**：沒有數據時，卡片會播放一段虛構的範例執行。

## 何時不用

- **單個助手回答問題**：請使用 `AiChat` 或 `AiAnswer`；這裡沒有可以分出去的工作。
- **步驟和分支很多的大型流程**：這張圖是為一個主智慧體和最多約六個子智慧體設計的。大型流程請使用流程編輯器或表格。
- **需要搜尋或篩選的日志**：訊息記錄是一次執行的可讀過程，不是日志查看器。請使用表格。
- **執行智慧體本身**：卡片只展示執行。調用模型和工具由你的程式碼負責。

## 用法

`AgentOrchestrator` 派生自 Avalonia 的 `TemplatedControl`。智慧體是 `OrchestratorAgent` 物件，包含 id、名稱、角色和模型。除非 `LeadId` 指定了其他智慧體，否則第一個智慧體就是主智慧體。

```xml
<arc:AgentOrchestrator Goal="Plan the team offsite" IsAutoStart="False">
    <arc:OrchestratorAgent Id="lead" Name="Atlas" Role="Planner" />
    <arc:OrchestratorAgent Id="venue" Name="Scout" Role="Venues" />
    <arc:OrchestratorAgent Id="budget" Name="Ledger" Role="Budget" />
</arc:AgentOrchestrator>
```

### 結構

- **頭部**：標題和狀態膠囊（Running、Paused、Complete、Failed）、目標、進度行（"2 of 5 agents done · 9 messages"），以及 Reset 和 Run 按鈕。
- **圖**：帶 Lead 標籤的主智慧體、各個子智慧體、它們之間的線和移動的圓點。
- **訊息記錄**：每則訊息的發送方、接收方、類型、執行時間和文字，最新的在最後。
- **詳情面板**：所選的智慧體（在你選擇其他智慧體之前是主智慧體），顯示它的狀態、模型、訊息數量、任務、當前步驟和結果。

### 用你的程式碼驅動執行

沒有 `Script` 時，由你的程式碼驅動執行。`Post` 添加一則訊息並按它的類型更新智慧體；`SetStatus` 直接改變一個智慧體；`Complete` 和 `Fail` 結束執行。

<Demo name="Programmatic" />

```csharp
orchestrator.Start();
orchestrator.Post(new OrchestratorMessage
{
    From = "lead", To = "venue", Kind = OrchestratorMessageKind.Delegate, Text = "Find three venues near the lake"
});
orchestrator.SetStatus("lead", OrchestratorAgentStatus.Waiting, "Waiting on two agents");
```

| 類型 | 改變的內容 |
|---|---|
| `Delegate` | 接收方的任務變為這段文字，並開始工作 |
| `Progress` | 發送方的當前步驟變為這段文字 |
| `Question` | 發送方進入等待 |
| `Answer` | 接收方（提問的一方）重新開始工作 |
| `Result` | 發送方完成；它的結果就是這段文字 |
| `Error` | 發送方失敗；它的結果就是這段文字 |

一則訊息只能發送一次；要再次發送相同的文字，請發送 `message.Copy()`。智慧體是 `AvaloniaObject`，因此直接設置它的 `Status`、`Task`、`Activity` 或 `Result` 也會更新卡片。

### 播放指令碼

`Script` 是一個 `OrchestratorStep` 清單：每一步在等待 `Delay` 之後發送一則訊息或設置一個智慧體的狀態。可以用 `OrchestratorStep.Send` 和 `OrchestratorStep.Set` 創建步驟。`Speed` 按比例縮放所有等待時間，`IsAutoStart` 讓卡片顯示時就開始播放指令碼，Run 按鈕用於暫停、繼續和重新播放。暫停時正在等待的那一步，在繼續後會重新開始等待。使用範例智慧體且沒有指令碼時，卡片會播放 `AgentOrchestratorSamples.Script()`。

<Demo name="Recovery" />

### 選擇智慧體

點擊一個節點，或讓它獲得焦點後按 Enter 或 Space。`SelectedAgentId` 是雙向的；為 null 時顯示主智慧體。

### 較窄的容器

卡片按自身寬度變化。子智慧體無法並排放下時，會在主智慧體下方排成一列。

<Demo name="Narrow" />

### 文字

所有文字都來自 `Labels`（`AgentOrchestratorLabels`）。帶值的文字是函數，因此可以改變詞序。

```csharp
orchestrator.Labels = new AgentOrchestratorLabels
{
    Title = "Équipe d'agents",
    StatusName = status => status == OrchestratorAgentStatus.Done ? "Terminé" : status.ToString()
};
```

### 內容建議

- **按智慧體做的事給它命名**，並配上簡短的角色："Scout · Market research"。
- **每則訊息一句話**，用智慧體自己的口吻寫："Checked two of three rival stores"。
- **任務要短**：節點最多顯示兩行智慧體正在做的事。

## API 參考

<!-- api: AgentOrchestrator -->

<!-- api: OrchestratorAgent -->

### OrchestratorMessage

| 屬性 | 說明 |
|---|---|
| `From`、`To` | 發送方和接收方的 id |
| `Kind` | `Delegate`、`Progress`、`Question`、`Answer`、`Result` 或 `Error` |
| `Text` | 一句話 |
| `Sequence` | 它在本次執行中的序號，從 1 開始（發送時設置） |
| `Elapsed` | 發送時的執行時間（發送時設置） |

### OrchestratorStep

| 屬性 | 說明 |
|---|---|
| `Delay` | 在上一步之後等待的時間（速度為 1 時） |
| `Message` | 要發送的訊息，或 null |
| `AgentId`、`Status`、`Activity`、`Result` | 要改變的智慧體，或 null |

### 方法

| 方法 | 作用 |
|---|---|
| `Start()` | 開始執行（暫停的執行會繼續，已結束的執行會重新開始） |
| `Pause()`、`Resume()`、`Toggle()` | 暫停、繼續，或執行 Run 按鈕的操作 |
| `Reset()` | 清空執行：所有智慧體空閒，沒有訊息 |
| `Complete()`、`Fail()` | 結束執行 |
| `Post(message)` | 發送一則訊息 |
| `SetStatus(id, status, activity)` | 改變一個智慧體 |

### 事件

| 事件 | 觸發時機 | 參數 |
|---|---|---|
| `MessagePosted` | 發送了一則訊息 | `Message` |
| `AgentStatusChanged` | 某個智慧體的狀態改變 | `Agent`、`OldStatus`、`NewStatus` |
| `RunStateChanged` | 執行開始、暫停、結束或被重設 | `OldState`、`NewState` |

## 無障礙

- 圖是一個名為 "Agents" 的清單；每個節點是一個清單項，名稱包含智慧體的名稱、角色、狀態和當前步驟（"Scout, Market research, Working: Checked two of three rival stores"），並隨執行過程更新。
- 狀態從不只靠顏色表達：每種狀態都有自己的圖示（轉圈、沙漏、勾號、叉號）和文字。
- 一個禮貌型即時區域會讀出每次狀態變化（"Scout is done"）以及執行的開始、暫停和結束。
- 訊息記錄的每一行名為 "Atlas to Scout, Task: …"；Run 按鈕的名稱跟隨它的文字，Reset 有名稱和提示。

### 鍵盤

| 按鍵 | 操作 |
|---|---|
| `Tab` | Reset、Run，然後是圖（一個停靠點），然後是訊息記錄 |
| `ArrowLeft` / `ArrowRight` | 上一個或下一個節點（在排成一列時 `ArrowUp` / `ArrowDown` 也可以） |
| `ArrowUp` / `ArrowDown` | 從子智慧體到主智慧體，從主智慧體到所選的子智慧體 |
| `Home` / `End` | 主智慧體、最後一個子智慧體 |
| `Enter` 或 `Space` | 選擇獲得焦點的節點 |

## 動效

- 每則訊息會讓一個圓點在 900 ms 內以緩入緩出曲線沿線移動，在兩端淡入淡出，並帶有柔和的光暈。
- 通往工作中智慧體的線變為強調色；已完成的線變為綠色，失敗的線變為紅色虛線。
- 工作中和思考中的節點顯示一個轉圈；節點的邊框和背景在 160 到 240 ms 內變化。
- 執行時，狀態膠囊會交叉淡化它的文字並轉動它的轉圈。
- 減少動效時不顯示圓點，但保留所有狀態變化；邊框立即改變。關閉動效時一切立即生效。

| 變化 | 完整 | 減少 | 關閉 |
|---|---|---|---|
| 訊息 | 圓點移動 900 ms | 沒有圓點 | 沒有圓點 |
| 節點狀態 | 邊框漸變；轉圈轉動 | 立即 | 立即 |
| 線的狀態 | 立即 | 立即 | 立即 |

## 響應式

- 卡片最寬 960 px，按自身寬度而不是窗口寬度變化。
- 當每個子智慧體都能以 156 px 以上的寬度並排放下時，它們在主智慧體下方展開。否則它們在主智慧體下方排成一列，左側有一條直線，節點只顯示一行文字。
- 寬度在 640 px 及以下（`:compact`）時，內距變小，詳情面板移到訊息記錄上方。
- 訊息記錄高 260 px，因此執行時卡片高度保持不變；記錄在其中滾動，並跟隨新訊息，除非你向上滾動過。

## 相關元件

- `AiChat`：一個助手在對話中回答。
- `AgentMemory`：助手記住了什麼。
- `ModelArena`：在同一個提示上比較兩個模型。

| 元件 | 何時選擇 |
|---|---|
| `AiChat` | 一個助手和一個人對話 |
| `ModelArena` | 兩個模型回答同一個提示 |
| `Timeline` | 沒有智慧體、按時間排列的事件 |
