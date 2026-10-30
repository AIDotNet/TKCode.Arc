---
title: AI workflow
summary: 一張自動化卡片：觸發條件、AI 步驟、分支與動作排在同一條軌道上，測試執行沿著軌道逐步滑下並展開每一步的輸出，步驟從精簡的指令選單中新增。
source-hash: 31dbad0538c5
---

<Demo name="Preview" />

## 何時使用

`AiWorkflow` 呈現一段簡短的自動化流程：應用程式觸發條件、模型步驟與應用程式動作混在一起，而且可以就地測試。步驟是排在同一條細線
軌道上的 32px 精簡列。測試執行一次只動一件事：一個小小的強調色圓點滑向下一步，該步驟執行時閃爍，接著顯示耗時與它產出內容的預覽。
分支會選定其中一條路徑，並把另一條標為 Skipped。步驟從一個小指令選單中新增，用所在列上的按鈕移除。

- **自動化與整合頁面**：使用者在這裡組合一個觸發條件、一個 AI 步驟和幾個動作。
- **新手導覽與產品導覽**：說明 AI 步驟在團隊既有的應用程式之間位於何處。
- **檢閱工作流程**：它做了什麼，以及一次範例執行在每一步產出了什麼。

## 何時不用

- **大型圖形**：有許多分支、迴圈或匯合時，請使用 Workflow 套件中的 `WorkflowCanvas`。
- **單一工具呼叫，或代理程式的即時步驟**：請使用 `ToolCall` 或 `AgentStatus`。
- **代理程式寫下的計畫**：請使用 `AgentPlan`。
- **開啟自動播放的正式產品**：請設定 `AutoPlay="False"`；重複播放的執行只用於展示。

## 用法

把你的流程放進 `Steps`。卡片自己沒有步驟時，會播放它的 `Simulation`，也就是由你提供的流程。兩者都沒有時顯示空狀態：只有外框，
測試執行無法使用，並顯示 "No steps yet"；如果 `Catalog` 裡有觸發條件，則列出可作為起點的觸發條件。只要沒有接上執行器，標題列就
會顯示 "Simulated"。

```xml
<arc:AiWorkflow Title="Escalate urgent support email"
                Description="Runs on every new email to support@example.com"
                AutoPlay="False" />
```

### 步驟

`Steps` 就是流程，依序排列。每個 `AiWorkflowStep` 都有 `Id`、`Kind`（`Trigger`、`Ai`、`Action` 或 `Branch`）、`App`（內建的
`AiWorkflowApp` 之一，或你自己的名稱與標記）、`Title`，以及選用的 `Detail`、執行時顯示的 `Activity`，還有模擬執行使用的
`Output`、`Duration` 與 `Error`。分支帶有 `Paths`（每條都是一個 `AiWorkflowBranchPath`，包含 id、分頁標籤文字與它自己的步驟），
而且必須是最後一個頂層步驟。

```csharp
flow.Steps =
[
    new AiWorkflowStep { Id = "mail", Kind = AiWorkflowStepKind.Trigger, App = AiWorkflowApp.Mail, Title = "New email", Detail = "support@example.com" },
    new AiWorkflowStep { Id = "label", Kind = AiWorkflowStepKind.Ai, App = AiWorkflowApp.Ai, Title = "Classify with Claude Sonnet 5.5", Activity = "Reading the email" },
    new AiWorkflowStep { Id = "issue", Kind = AiWorkflowStepKind.Action, App = AiWorkflowApp.Tracker, Title = "Create issue", Detail = "Support team" },
];
```

卡片把步驟當成值：要修改步驟，就給 `Steps` 一個新清單。空清單會顯示空狀態，其中列出目錄裡可作為起點的觸發條件。

內建應用程式都是通用的，使用 Lucide 圖示：`Mail`、`Ai`（模型步驟，以強調色繪製）、`Chat`、`Tracker`、`Docs`、`Sheets`、`Crm`、
`Helpdesk`、`Repo`、`Payments`、`Schedule`、`Webhook` 與 `Branch`。真實產品請傳入 `new AiWorkflowApp("id", "Name", geometry)`，
並使用你自己的 24 × 24 描邊圖形；模型步驟的應用程式以模型廠商命名（`isAccent: true` 讓它的標記以強調色繪製）。

### 模擬

`AiWorkflowSimulation` 保存一個在 `Steps` 為 null 時播放的流程：`Title`、`Description`、`Steps` 以及新增步驟用的 `Catalog`。
卡片會顯示這個流程；開啟 `AutoPlay` 時，只要卡片在螢幕上就會測試執行它，短暫停頓後重複，直到有人在卡片內按下滑鼠或按鍵。每一步
都依它的 `Output`、`Duration` 與 `Error` 模擬，不會送出任何內容。你自己設定的 `Steps`、`Title`、`Description` 與 `Catalog`
優先於它。新增或移除步驟會把編輯後的流程放進 `Steps`；把 `Simulation` 設為 null 會停止執行並顯示空狀態。

```csharp
flow.Simulation = new AiWorkflowSimulation
{
    Title = "Escalate urgent support email",
    Description = "Runs on every new email to support@example.com",
    Steps = SupportFlow.Steps(),     // List<AiWorkflowStep> with Output, Duration and Error
    Catalog = SupportFlow.Catalog(), // List<AiWorkflowCatalogItem>
};
```

本頁的示範在 `AiWorkflowDemoData` 中建立流程；模型步驟使用廠商官方頁面上的真實模型（核對於 2026-10-29），所有人物、公司與數字
都是虛構的。

### 實際執行步驟

<Demo name="OwnRunner" />

設定 `RunStep`，就能讓每一步呼叫你的測試端點。它會收到步驟與一個 `AiWorkflowRunStepContext`，其中有 `Cancellation` 權杖（在
Stop、開始新一輪執行或卡片離開視窗時取消）、依步驟 id 索引的 `Previous` 輸出，以及 `Attempt` 次數。傳回其中一種輸出即可；擲回例
外則以例外訊息讓該步驟失敗。分支傳回 `AiWorkflowRouteOutput` 來選擇路徑。

```csharp
flow.RunStep = async (step, context) =>
{
    using var response = await http.PostAsync($"/api/workflows/test/{step.Id}", null, context.Cancellation);
    response.EnsureSuccessStatusCode();
    return new AiWorkflowTextOutput { Text = await response.Content.ReadAsStringAsync(context.Cancellation) };
};
```

沒有 `RunStep` 時，每一步都依它的 `Output` 與 `Duration` 模擬；帶有 `Error` 的步驟會在第一次嘗試時失敗，以便展示 Retry。
`RunCompleted` 會回報通過的執行，或停在失敗步驟上的執行，並附上耗時、所有輸出與失敗步驟的 id。`StartRun`、`StopRun` 與 `Retry`
可以從程式碼驅動執行。

### 輸出

每種輸出都以小號的淡化文字內嵌在其步驟下方，與標題對齊：

| 輸出 | 顯示 |
|---|---|
| `AiWorkflowEmailOutput` | 一行寄件者與主旨，接著是兩行內文 |
| `AiWorkflowLabelOutput` | 一個帶圓點的細線標籤、等寬字型的信心值，以及串流出現的理由 |
| `AiWorkflowRouteOutput` | 分支走了哪條路徑，可附一則說明 |
| `AiWorkflowMessageOutput` | 頻道與作者，接著是訊息內容 |
| `AiWorkflowRecordOutput` | 等寬字型的紀錄編號、它的標題與一則補充資訊 |
| `AiWorkflowTextOutput` | 自由文字，例如草擬的回覆，以串流出現 |

### 新增與移除步驟

Add step 會開啟一個精簡的指令選單：一個依標題、應用程式或詳細資訊篩選的搜尋欄位，以及帶有應用程式標記與 1 到 9 快速鍵的列。新步驟
會放在流程末端；流程以分支結尾時，放在目前可見路徑的末端。除了觸發條件之外，每個步驟都有移除按鈕。兩者都會給 `Steps`（雙向屬性）
一個新清單，並觸發 `StepsChanged`，參數中有新清單、變更類型與對應的步驟。

`Catalog` 存放選單的項目（`AiWorkflowCatalogItem`，也就是沒有 id 的步驟）。觸發條件只會出現在空狀態中。只有在流程中還沒有分支時才
提供分支。目錄為空時會隱藏選單。

### 唯讀

<Demo name="ReadOnly" />

`IsEditable="False"` 會隱藏 Add step 與移除按鈕。Test run 仍然可以使用。

### 載入中

`IsLoading` 會在流程載入時顯示三個骨架步驟。在它變成 false 之前，Test run 與選單都無法使用。

### 窄版面

<Demo name="Narrow" />

卡片會填滿容器，最寬 640px。寬度小於 480px 時，每一列會增高到 44px，詳細資訊移到標題下方，狀態欄位從 72px 縮到 64px，標題列折成
兩行，空狀態中的觸發條件不再顯示詳細資訊。

### 你自己的文字

`Labels` 包含卡片顯示或朗讀的每一個字，包括螢幕閱讀器名稱與播報。替換整個物件即可在地化或改寫：

```csharp
flow.Labels = new AiWorkflowLabels
{
    TestRun = "Probar",
    AddStep = "Añadir paso",
    Remove = title => "Quitar " + title,
    Passed = duration => "Correcto en " + duration.TotalSeconds.ToString("0.0") + " s",
};
```

### 內容指引

- 步驟標題以動詞開頭（"Create issue"、"Post message"）；詳細資訊說明位置（"#support"、"Support team"）。
- AI 步驟在標題中寫出模型名稱（"Classify with Claude Sonnet 5.5"）。
- 讓流程保持簡短：一個觸發條件、一到兩個 AI 步驟、末端一個分支，每條路徑幾個動作。

### 最佳實務

- 把 `RunStep` 接到測試端點，傳回輸出以便呈現預覽；擲回例外即可展示帶有 Retry 的失敗。
- 替換清單時保持步驟 id 不變，這樣列與執行結果都會留在原位。
- 在展示頁與行銷頁之外關閉 `AutoPlay`。

### 常見問題

**自動播放為什麼停了？** 在卡片內第一次按下滑鼠或按鍵後，它會永久停止，以免在別人操作時執行。卡片不在螢幕上時它也會等待。

**一個流程可以有兩個分支嗎？** 不行。每個流程只有一個分支，而且是最後一個頂層步驟。更大的圖形請使用 `WorkflowCanvas`。

## API 參考

<!-- api: AiWorkflow -->

<!-- api: AiWorkflowStep -->

<!-- api: AiWorkflowBranchPath -->

<!-- api: AiWorkflowCatalogItem -->

<!-- api: AiWorkflowSimulation -->

<!-- api: AiWorkflowApp -->

<!-- api: AiWorkflowOutput -->

<!-- api: AiWorkflowEmailOutput -->

<!-- api: AiWorkflowLabelOutput -->

<!-- api: AiWorkflowRouteOutput -->

<!-- api: AiWorkflowMessageOutput -->

<!-- api: AiWorkflowRecordOutput -->

<!-- api: AiWorkflowTextOutput -->

<!-- api: AiWorkflowRunStepContext -->

<!-- api: AiWorkflowStepsChangedEventArgs -->

<!-- api: AiWorkflowRunCompletedEventArgs -->

<!-- api: AiWorkflowLabels -->

## 無障礙

- 卡片是一個以標題命名的群組（載入時為 "Loading workflow"）；步驟清單與每條分支路徑都是有名稱的清單。
- 每個步驟的名稱都包含標題、應用程式與狀態（"Create issue, Tracker, Done in 700ms"）。狀態以圖示加上簡短文字呈現，從不只靠顏色。
- 一個禮貌的即時區域會播報執行開始、每一步的結果、失敗及其訊息與最終結果；沒有接上 `RunStep` 時會補上
  "Simulated, nothing was sent."。新增與移除步驟也會播報。
- 新增步驟選單的搜尋欄位始終保有焦點；它的說明文字會指出目前醒目提示的項目。
- 分支路徑是一個使用漫遊焦點的單選群組：只有選取的分頁是 Tab 停駐點。
- 移除按鈕依步驟命名（"Remove Create issue"）。使用滑鼠時，它會在所在列被懸停或自身取得鍵盤焦點時顯示；在觸控螢幕上一律顯示。
- 所有文字都在 `AiWorkflowLabels` 中。

### 鍵盤

| 按鍵 | 操作 |
|---|---|
| Tab | 依序經過 Test run（或 Stop）、每個步驟的移除按鈕、分支路徑分頁、失敗步驟上的 Retry，以及 Add step |
| Enter、Space | 開始或停止執行、移除步驟、重試失敗的步驟，或開啟新增步驟選單 |
| 方向鍵 | 在分支分頁之間切換路徑（Home 與 End 跳到兩端）；選單開啟時，Up 與 Down 移動醒目提示 |
| 輸入文字 | 依標題、應用程式或詳細資訊篩選新增步驟選單 |
| Enter（選單） | 新增醒目提示的步驟並關閉選單 |
| 1 到 9（選單） | 搜尋欄位為空時，新增對應快速鍵的步驟 |
| Escape | 關閉選單，並把焦點還給 Add step |

## 動效

| 變化 | 完整 | 減少 | 關閉 |
|---|---|---|---|
| 執行進入下一步 | 一個 5px 的強調色圓點在 240ms 內沿連接線滑下，身後的線隨之填滿 | 連接線立即填滿，沒有圓點 | 立即 |
| 步驟開始或結束 | 狀態在固定的 72px 欄位中交叉淡化（淡出 100ms，淡入 160ms）；詳細資訊交叉淡化為閃爍的活動列 | 短暫淡化，不閃爍 | 立即 |
| 輸出、錯誤、新增或移除的步驟 | 以 0.22 秒無回彈彈簧依量測高度展開或收合，內容淡入淡出 | 立即 | 立即 |
| 理由或草稿 | 每 34ms 串流出現五個字詞，每塊淡入；最終版面從第一個影格起就預留好 | 分塊出現，不淡入 | 一次全部顯示 |
| 切換路徑 | 清單交叉淡化（淡出 100ms，淡入 160ms），高度隨之變化；分頁醒目提示以同一彈簧滑動 | 120ms 交叉淡化，高度立即變化 | 立即 |
| 新增步驟選單 | 在 160ms 內淡入並定位，無回彈，120ms 內離開；篩選時清單以動畫變為新的高度 | 淡入淡出 | 立即 |
| 自動播放 | 卡片不在螢幕上時等待，在卡片內第一次按下或按鍵後永久停止 | 相同 | 相同 |

在所有狀態下，步驟列保持高度，狀態欄位保持寬度，因此執行播放時沒有任何東西位移。

## 相關

- `AgentPlan`：代理程式行動前寫下的計畫。
- `ToolCall`：代理程式對話紀錄中的單一工具呼叫。
- `AgentStatus`：整個代理程式執行的即時狀態。
- `WorkflowCanvas`：大型自由形式的工作流程圖。
