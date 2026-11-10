---
title: Agent run
summary: 即時展示一次 AI 智慧體執行：目標，由思考、帶輸入與結果的工具呼叫、改動的檔案和訊息組成的時間線，計時、費用與 token，以及停止或重試。
source-hash: c189a387022d
---

<Demo name="Preview" />

## 何時使用

`AgentRun` 是你在 AI 智慧體執行任務時觀看的頁面。目標放在最上方，讓你始終知道智慧體想做什麼。下面的時間線一步一步增長：智慧體想了什麼，呼叫了哪個工具、輸入是什麼、傳回了什麼，新建或修改了哪些檔案，以及最後對你說了什麼。角落裡顯示計時、花費和用掉的 token，一條進度條顯示完成了幾步，一個按鈕用來停止執行或重新開始。

- **程式、資料或維運智慧體**：要花幾分鐘、呼叫很多次工具，使用者想看到進度並隨時介入。
- **任務的詳情頁**：從智慧體工作的清單或看板開啟。
- **事後查看一次已結束的執行**：同一個檢視可以用儲存的步驟顯示已完成、已停止或失敗的執行。

## 何時不用

- **只有幾次工具呼叫的聊天回覆。** 請使用 `AI chat` 或 `AI answer`，它們在對話中直接顯示工具。
- **同時查看很多次執行。** 請用 `Agent tasks` 看總覽，再從那裡開啟一次執行。
- **成千上萬行的長記錄。** 原始記錄請放在記錄檢視器中；這裡的一步是一個有意義的操作。

## 用法

區塊衍生自 Avalonia 的 `TemplatedControl`。不設定任何屬性時，它會播放一次內建的範例執行，方便先試用。在應用程式中，給它目標、作為 `ObservableCollection<AgentRunStep>` 的步驟、狀態、費用和 token，並處理停止與重試：

```xml
<arc:AgentRun Goal="{Binding Goal}"
              Steps="{Binding Steps}"
              State="{Binding State}"
              Cost="{Binding Cost}"
              Tokens="{Binding Tokens}" />
```

```csharp
run.StopRequested += (_, _) => agent.Cancel();
run.RetryRequested += (_, _) => StartNewRun();

// As your agent works: add a step when it starts, update it when it ends.
var step = new AgentRunStep { Kind = AgentRunStepKind.ToolCall, Title = "Find the route", ToolName = "search_code",
                              Input = query, Status = AgentRunStepStatus.Running };
steps.Add(step);
step.Output = result;
step.Duration = TimeSpan.FromSeconds(1.3);
step.Status = AgentRunStepStatus.Done;
```

### 步驟

每個 `AgentRunStep` 有類型、標題和狀態。思考步驟把推理放在 `Text` 中，工具呼叫有 `ToolName`、`Input` 和 `Output`，檔案改動有 `Files`（路徑、新建、修改或刪除、增加和刪除的行數），訊息則放在 `Text` 中。按下步驟的標題列會展開詳情；訊息始終展開。執行顯示在螢幕上時新到的步驟會升起就位，在你向上捲動之前，時間線會一直跟隨最新的步驟。

### 狀態、計時與費用

`State` 是雙向的。處於 `Running` 時，計時從 `Elapsed` 開始自動計數，按鈕顯示 Stop。按下 Stop 會引發 `StopRequested`，然後把執行和正在進行的步驟標為已停止。已完成的執行提供 Run again，已停止或失敗的執行提供 Retry；兩者都會引發 `RetryRequested`。失敗的執行在底部顯示 `FailureMessage`。

<Demo name="Failed" />

### 已結束的執行

設定儲存的步驟、狀態和合計，即可顯示一次已經結束的執行。

<Demo name="Completed" />

## API 參考

<!-- api: AgentRun -->

<!-- api: AgentRunStep -->

<!-- api: AgentRunFile -->

<!-- api: AgentRunLabels -->

## 無障礙

- 執行是名為 "Agent run" 的區域，目標是標題。
- **Tab** 可到達每個步驟的標題列以及 Stop 或 Retry 按鈕。**Up** 和 **Down** 在步驟標題列之間移動，**Home** 和 **End** 跳到第一個和最後一個；**Enter** 或 **Space** 展開或收起步驟。
- 步驟標題列會讀出類型、標題、狀態和時間（"Tool call: Find the route, done, 1.3s"），並報告自身展開或收起的狀態。
- 一個禮貌式即時區域會在步驟開始和執行結束時播報（"Completed in 1m 12s"）。
- 狀態由圖示和文字表示，從不只靠顏色。

## 動效

- 新步驟上升 6px，同時淡入並展開高度。
- 步驟詳情以彈簧展開並帶短暫淡入；箭頭以靈敏的彈簧轉動。
- 狀態圓點和步驟標記在 240ms 內變色。
- 減少動效時只保留短暫淡入淡出，沒有位移；關閉動效時所有變化立即生效。

## 響應式

- 執行最寬 720，小於這個寬度時跟隨容器。
- 寬度在 480 或以下時，計時、費用和 token 移到狀態列下方，兩側內距收緊。
- 時間線超過 420 高時捲動；標題和路徑用省略號縮短。

## 相關元件

- `Agent tasks`，一個由多次執行組成的看板，每次執行都能開啟這個檢視。
- `Research run`，用於閱讀資料並撰寫報告的研究智慧體。
- `AI chat`，用於呼叫工具的對話。
