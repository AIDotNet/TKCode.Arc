---
title: Tool call
summary: 智能體逐字稿中的工具呼叫列，顯示工具、它做了什麼與結果如何；點開可看到呼叫的輸入與輸出，核准與重試也都在列中完成。
source-hash: 034f5a63d7bf
---

<Demo name="Preview" />

## 何時使用

`ToolCall` 是 AI 代理逐字稿中的一行：工具的標記、用淺白文字描述呼叫做了什麼、像是 "4 results · 1.2s" 這樣低調的 meta，以及狀態圖示。`ToolCallGroup` 會把某一輪助理回應的每個呼叫收進一個標頭，標頭寫著 "Used 4 tools"。點擊呼叫即可展開成它的輸入與輸出，讓逐字稿保持易讀，而細節仍只需一次點擊。

- **顯示 AI 代理做了什麼**：在聊天或代理記錄中，參數與結果都只需一次點擊即可看到。
- **人在迴路中**：在執行前必須先允許或拒絕的呼叫，例如張貼訊息或寫入資料庫，列中直接提供 Deny 與 Allow。
- **可復原的失敗**：逾時的查詢會顯示其原因，並在發生處提供 Retry 按鈕。
- **除錯與稽核檢視**：人們需要複製呼叫精確的輸入與輸出時使用。

## 何時不用

- **含步驟與進度的計畫**：請使用 `AgentPlan`；工具呼叫是單一動作，不是待辦清單。
- **整個執行的單一狀態**：請使用 `AgentStatus`；群組標頭只會總結它所持有的呼叫。
- **跨多次執行的核准佇列**：請自行建立清單；列只能替自己的那一個呼叫作決定。
- **AI 代理可使用的工具**：那是設定，不是歷史紀錄。

## 用法

為列提供一個 `ToolCallItem`。工具是資料，不是品牌：它包含供螢幕閱讀器使用的名稱、一個 24 × 24 的描邊標記（Lucide 字符），以及每個狀態的片語。沒有項目時，列會顯示一筆範例網路搜尋。

```xml
<arc:ToolCall>
    <arc:ToolCallItem Id="tickets" ToolName="Tracker"
                      ToolMark="{x:Static arc:ArcIcons.Inbox}" Status="Success"
                      Title="Searched tickets in finance/close" Summary="4 results" Duration="0:0:1.18" />
</arc:ToolCall>
```

單獨使用時，列位於 1 px 的 Surface 外框中（圓角 16，內縮 4）；在群組中則貼齊、沒有外框。

對於 AI 代理的一輪回應，代理開始呼叫工具時就把它們加入 `ToolCallGroup`，並原地更新：

```csharp
var group = new ToolCallGroup();
group.Items.Add(call);                  // a new row grows in from zero height
call.Status = ToolCallStatus.Success;   // the row keeps its place and its open state
group.IsActive = agentIsWorking;        // keeps "Using" between calls
```

### 結構

1. **列**：高 40 px。標記（16 px）、標題（一行，會截斷）、meta、狀態圖示與一個箭頭。
2. **原因行**：位於失敗呼叫的標題之下（說明其原因），或等待核准之呼叫的標題之下（說明允許後會發生什麼），字級 12 px。有原因行的列高 58 px。
3. **按鈕**：失敗的呼叫上有 Retry，等待核准的呼叫上有 Deny 與 Allow，高 28 px。
4. **Input 與 Output**：等寬字的鍵與值列表，結果則以清單、表格、上色的 JSON 或文字呈現。每一項都有一個複製按鈕，會以勾號就地確認 1.9 秒。
5. **群組標頭**：重疊的標記（每個工具一個），接著是 "Using 3 tools"、"Used 4 tools" 或 "Waiting for approval"；右側則是 "1 failed" 或總時長。

### 狀態

<Demo name="Statuses" />

每個狀態都有自己的圖示形狀，因此顏色從來不是唯一的提示：旋轉指示器（執行中）、勾號只描繪一次（成功）、警示（錯誤）、帶問號的盾牌（核准），以及禁止符號（拒絕）。執行中的標題會閃爍。等待核准的列會以強調色外框疊在淡淡的強調色底色上。已拒絕的列會淡化，其 meta 顯示 "Denied"。

`ToolCallItem` 為每個狀態提供一個片語：`Title` 是已完成的過去式（"Searched …"），`RunningTitle` 是現在式，`ApprovalTitle` 是請求（"Post a status note to …"），`ErrorTitle` 是失敗（"Could not read …"）。缺少某個片語時，會退回使用 `Title`。

### 輸出

<Demo name="Outputs" />

| 輸出 | 外觀 | 摺疊 | 複製的內容 |
|---|---|---|---|
| `ToolCallListOutput` | 等寬數字的前導值、一個標題與一個標籤；32 px 的列 | 超過 5 列後摺疊 | 每筆項目一行 |
| `ToolCallTableOutput` | 標題列與等寬字的儲存格；30 px 的列；在其凹槽中可橫向捲動 | 超過 5 列後摺疊 | 以 Tab 分隔的列，並含標題列 |
| `ToolCallJsonOutput` | 縮排的 JSON，鍵、字串、數字與常值都上了色；20 px 的行 | 超過 10 行後摺疊 | JSON 文字 |
| `ToolCallTextOutput` | 一段保留換行的文字 | 從不摺疊 | 文字 |

JSON 建立在 `System.Text.Json.Nodes` 之上，上色則由我們自己的小型詞法分析器完成，因此全程不使用反射。摺疊的內容隱藏在 "Show 25 more lines" 之後，點擊後會變成 "Show less"。缺少摘要時，會從輸出推導："N results"、"N rows"、"N fields"。`OutputTemplate` 可用你自己的檢視取代內建的檢視，用來呈現成功呼叫的結果。

### 由你自己的資料驅動的群組

<Demo name="OwnData" />

群組會引發 `ApproveRequested`、`DenyRequested` 與 `RetryRequested`（它們從列冒泡而來，並帶著該項目）。由你自己更新呼叫，通常先改為執行中，再改為成功，並設定 `Handled`。控制項絕不會變更你所驅動的狀態。

當沒有人處理某個動作時，控制項會保留本地行為，使沒有接線的頁面仍能運作：**Deny** 會把呼叫標記為已拒絕；**Allow** 與 **Retry** 只會模擬執行，而且只限於內建的範例資料（`ToolCallSamples`、`ToolCallSimulation`）。對於其他資料，什麼都不會改變。模擬執行時，列的即時區域會說明這一點（"This is sample data, so the call is only simulated."）。

### 展開與摺疊

展開標頭時，各列會以 20 ms 的間隔交錯滑入，每一列落定時向下移 6 px。群組展開期間新增的呼叫，則改為從零高度長出。AI 代理工作時，群組會保持展開；每個呼叫都完成後 450 ms（`AutoCollapse`、`AutoCollapseDelay`），它會自行收合，讓答案緊接在 "Used 4 tools" 之下閱讀。一旦使用者展開或收合過群組或某一列，它就不會再自行摺疊。需要決定的呼叫會打開一個摺疊的群組。`ExpandedChanged` 會告訴你該變更是否為自動進行。

### 模擬

`ToolCallSimulation` 會依時鐘把一個預先寫好的輪次播放到一個可觀察的清單中：呼叫約每 225 ms 加入一次，執行其所需時間後落定；失敗的呼叫會等待 `Retry`；需要核准的呼叫則等待 `Approve` 或 `Deny`。把群組的 `ItemsSource` 綁定到 `Items`，並把 `IsActive` 綁定到 `IsWorking`。預設腳本是 `ToolCallSamples.Script()`；傳入 `TimeProvider` 即可在測試中逐步執行它。預覽中群組周圍的逐字稿（問題、句子、答案與 "Run again"）屬於示範內容，不是控制項的一部分。

### 窄容器

<Demo name="Narrow" />

列本身寬度低於 440 px 時，meta 只保留時長，原因會換行，按鈕會移到其下方，Input 與 Output 則取消縮排。寬度低於約 370 px 時，meta 會整個隱藏。寬的 JSON 與表格會在其凹槽中捲動。

### 減少動態效果

在 Reduced 與 Off 層級，交錯進場、旋轉指示器、閃爍的移動，以及所有彈簧動畫都會停止：面板、列與狀態會立即改變。標籤、通知、焦點與所有內容都會保留。

### 內容指引

- 將標題寫成動詞片語：已完成時用過去式（例如 "Read the export job log"），執行中則用現在式。
- 以工具的用途為它命名（"Tracker"、"Warehouse"）。它會先被讀到，然後才是標題。
- 原因行用一句話說明出了什麼錯，或允許後會發生什麼。
- 選擇最好讀的輸出：搜尋結果用清單，資料列用表格，結構化資料用 JSON，已送出的訊息或簡短記錄用文字。

### 最佳實務

- 保持 `Id` 穩定：列在狀態改變時會保留其展開狀態。
- 原地更新項目；新的呼叫加到結尾。
- AI 代理工作時設定 `IsActive`，讓標頭不會在 "Using" 與 "Used" 之間閃爍。
- 每個工具標記都使用 Lucide 字符；除非你設定了 `ToolMarkBrush`，否則標記會跟隨文字顏色。

### 常見問題

**我能使用品牌標誌作為標記嗎？** 標記可以是任何 24 × 24 的描邊幾何圖形。品牌標誌須由你自行取得授權；範例只使用 Lucide 字符。

**Allow 會執行工具嗎？** 不會。它會引發 `ApproveRequested`；由你的程式碼執行工具並更新狀態。

**為什麼執行中呼叫的 Output 是空的？** 還沒有結果；該區段會說明這一點。

## API 參考

<!-- api: ToolCall -->

<!-- api: ToolCallGroup -->

<!-- api: ToolCallItem -->

<!-- api: ToolCallArgument -->

<!-- api: ToolCallOutput -->

<!-- api: ToolCallListOutput -->

<!-- api: ToolCallListEntry -->

<!-- api: ToolCallTableOutput -->

<!-- api: ToolCallJsonOutput -->

<!-- api: ToolCallTextOutput -->

<!-- api: ToolCallLabels -->

<!-- api: ToolCallSimulation -->

<!-- api: ToolCallSimulationStep -->

<!-- api: ToolCallActionEventArgs -->

<!-- api: ToolCallExpandedChangedEventArgs -->

<!-- api: ToolCallSimulationEventArgs -->

列舉如下：

| 型別 | 值 |
|---|---|
| `ToolCallStatus` | `Running`, `Success`, `Error`, `Approval`, `Denied` |
| `ToolCallSimulationOutcome` | `Succeeds`, `FailsUntilRetried`, `NeedsApproval` |
| `ToolCallSimulationState` | `Idle`, `Working`, `WaitingForRetry`, `WaitingForApproval`, `Finished` |
| `ToolCallSimulationPhase` | `Started`, `CallAppended`, `CallSucceeded`, `CallFailed`, `WaitingForApproval`, `Retried`, `Allowed`, `Denied`, `Finished` |

`ToolCallSamples` 保存虛構的範例呼叫與預設腳本。

## 無障礙

- 群組標頭與每一列都是按鈕，會回報自己是否已展開。列的名稱會朗讀工具、片語與狀態："Tracker: Searched tickets in finance/close Done, 4 results, 1.2s"。
- 狀態絕不只靠顏色傳達：每個狀態都有自己的圖示，失敗與等待中的呼叫會顯示原因行，名稱也會把狀態拼寫出來。
- 每一列都透過禮貌的即時區域通報狀態變化，包括範例呼叫只是被模擬這件事。
- Retry、Allow 與 Deny 會以它們所作用的呼叫來命名："Allow: Post a status note to #finance-ops"。按鈕消失時，焦點會移到其所屬的列，而不會掉到頁面上。
- Input 與 Output 是有名稱的群組；複製按鈕會透過即時區域確認，且不改變寬度。
- 指標焦點不畫焦點環；鍵盤焦點會在列內顯示 2 px 的焦點環，因此不會被裁切。
- 每個字詞都位於 `ToolCallLabels` 中。

### 鍵盤

| 按鍵 | 動作 |
|---|---|
| Tab | 依序移動於群組標頭、每一列、列上的 Retry、Deny 或 Allow，以及展開的列中的複製按鈕與摺疊按鈕 |
| Enter, Space | 展開或收合群組或列；在按鈕上則按下該按鈕 |

## 動效

| 變化 | Full | Reduced | Off |
|---|---|---|---|
| 展開一列或群組 | 高度以彈簧變化，內容在 160 ms 內淡入 | 立即 | 立即 |
| 群組展開時的各列 | 每列間隔 20 ms 交錯，落定時下移 6 px | 無 | 無 |
| 加入已展開群組的列 | 從零高度長出 | 立即 | 立即 |
| 狀態圖示 | 縮放與淡入淡出交換；勾號在 330 ms 內描繪完成 | 立即 | 立即 |
| 執行中的標題與 "Using 3 tools" | 閃爍，每次 1.8 s 掃過一遍 | 靜止 | 靜止 |
| 標題或標頭文字變更 | 從柔和的模糊中升起，耗時 240 ms | 在 130 ms 內淡入 | 立即 |
| 標頭中新增的工具標記 | 以彈簧展開寬度 | 立即 | 立即 |
| 展開箭頭 | 以彈簧旋轉 | 立即 | 立即 |
| 核准外框 | 在 240 ms 內淡入 | 立即 | 立即 |

## 相關

- `AgentStatus`：整個執行的即時狀態。
- `AgentPlan`：AI 代理行動前所寫的計畫。
- `AgentTimeline`：在單一時間軸上重播多代理執行的過程。
