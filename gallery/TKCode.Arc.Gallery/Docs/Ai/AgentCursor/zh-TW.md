---
title: Agent cursor
summary: 一份即時文件，AI 代理像協作者一樣在其中編輯：它帶名字標籤的游標選取、改寫並修正文字，同時一位同事的游標跟著閱讀，所有變更保持著色，直到有人接受或復原。
source-hash: 281ae90d39e9
---

<Demo name="Preview" />

## 何時使用

`AgentCursor` 展示 AI 代理像同事一樣編輯文件。它帶名字標籤的游標從頁首的大頭貼中出來，拖曳選取一個段落並串流寫入改寫後的文字，新增一個標題，再修正一個錯字，同時一位同事的游標跟著閱讀。每處變更都以代理的顏色著色，直到有人在浮動的審閱列中接受或復原。沒有自己的資料時，它會循環播放你提供的模擬（預覽中是一份虛構的發佈簡報、一個以真實模型命名的代理和一位同事）。

- **AI 寫作助理**：與人一起編輯文件，並提供接受與復原。
- **到達頁和新手導覽**：展示一個正在運作的協作式 AI 編輯器。
- **從代理串流接收結構化編輯的產品介面**：需要一個人工審閱步驟。

## 何時不用

- **自由對話或問答**：使用聊天紀錄元件。
- **真正的 RTF 編輯器**：使用編輯器內建的協作游標；這張卡片呈現的是唯讀的區塊。
- **包含許多章節的長文件**：卡片針對五到十個區塊的簡短摘錄。
- **單段文字的差異**：使用差異檢視。

## 用法

把文件以 `Blocks`（帶穩定 id 的標題、小標題和段落區塊）交給卡片，把編輯以 `Edits` 交給它。編輯以 id 指向區塊：改寫取代一個區塊的文字，插入在某個區塊之後新增一個區塊，修正取代區塊文字中一個確切的字詞。卡片依序播放這些編輯，並停在審閱狀態。

```csharp
card.Blocks =
[
    new AgentCursorBlock("title", AgentCursorBlockKind.Title, "Weekly update"),
    new AgentCursorBlock("shipped", AgentCursorBlockKind.Paragraph, "This week the team shipped the new onboarding flow."),
];
card.Edits =
[
    AgentCursorEdit.Rewrite("shipped", "We shipped the new onboarding flow.", "Making it shorter"),
    AgentCursorEdit.Insert("shipped", AgentCursorBlockKind.Heading, "Next week"),
];
```

既沒有 `Blocks`、`Edits`、`Run` 也沒有 `Simulation` 時，卡片顯示一份空白文件；沒有名稱的代理稱為 "Agent"（`AgentCursorLabels.Agent`），沒有 `Collaborator` 時也沒有人跟著閱讀。

### 模擬

想在沒有代理的情況下展示卡片（示範、設計審查、新手導覽），就給它一個 `AgentCursorSimulation`：文件、編輯、代理、一位同事和中繼資訊行。卡片沒有 `Blocks`、`Edits` 或 `Run` 時會播放這個模擬，說明什麼都沒有儲存，並在動態效果為完整時循環。你自己的資料優先於模擬；把 `Simulation` 設為 null 會停止模擬並顯示空白文件。

```csharp
card.Simulation = new AgentCursorSimulation
{
    Blocks = [new AgentCursorBlock("summary", AgentCursorBlockKind.Paragraph, "Shared inbox is a new place where teams answer email.")],
    Edits = [AgentCursorEdit.Rewrite("summary", "Shared inbox lets a team answer email together.", "Tightening the summary")],
    Agent = new AgentModel("Claude Opus 5.5", ArcIcons.Sparkles),
    Collaborator = new AgentCursorCollaborator("Maya Ortiz"),
};
```

上方的預覽播放的就是一個模擬，其簡報、編輯、代理和同事來自圖庫自己的示範資料。

### 結構

1. **頁首**：代理的大頭貼（以其顏色為底的標記），旁邊是同事的大頭貼；一行狀態說明代理正在做什麼；以及 Ask、Stop、Run again 或 Retry 按鈕。
2. **文件**：標題、選用的淡化 `Meta` 行、小標題和段落。
3. **代理游標**：代理顏色的箭頭和它的名字。它負責選取、串流寫入和標示錯字。
4. **同事游標**：第二個箭頭，帶有這個人的姓名縮寫和名字，跟著閱讀。
5. **審閱列**：浮在底部，顯示變更數量、Undo 和 Accept changes。

### 你自己的文件和編輯

<Demo name="YourDocument" />

`AutoPlay="False"` 讓卡片保持靜止，直到有人按下 Ask（或你呼叫 `Ask()`）。`Agent` 為代理命名並給它一個標記；透過純色的 `MarkBrush` 也決定其游標、選取範圍和變更著色的顏色，沒有時它們跟隨強調色。`Collaborator` 是跟著閱讀的人；設定 `IsCollaboratorVisible="False"` 可以隱藏他。

從 `Accepted` 儲存文件，再以 `Blocks` 傳回。卡片依內容比較區塊，所以傳回它剛顯示的文件不會有任何改變；不同的文件會在沒有執行使用它時取代畫面上的文件。

### 從你的代理取得編輯

<Demo name="FromYourAgent" />

`Run` 接受一個取得編輯的函式。等待期間，游標會閱讀文件。按下 Stop 會取消權杖；失敗的工作會以錯誤狀態結束並顯示 Retry 按鈕，其訊息會被播報。

```csharp
card.Run = async cancellation =>
{
    var response = await http.PostAsJsonAsync("/api/agent/edits", document, cancellation);
    return await response.Content.ReadFromJsonAsync<List<AgentCursorEdit>>(cancellation) ?? [];
};
```

### 審閱、接受與復原

完成的編輯保持著色，審閱列會計算它們。Accept changes 清除著色，並以編輯後的文件引發 `Accepted`；Undo 還原文字、收合插入的區塊，並引發 `Undone`。Stop 會保留已完成的編輯以供審閱，並還原進行到一半的編輯。`Phase` 和 `PhaseChanged` 跟隨執行過程：Idle、Thinking、Editing、Review、Accepted、Reverted、Stopped、Empty 和 Error。`Accept()`、`Undo()` 和 `Stop()` 在程式碼中完成同樣的操作。

模擬會循環：同事在短暫停頓後接受，然後文件重設，執行重新開始。指標停在卡片上或焦點在卡片內時，這段停頓不會結束。設定 `Loop` 可以重播你自己的執行；低於完整動態效果時從不循環。

### 窄容器

<Demo name="Narrow" />

卡片在 680px 以內隨容器伸縮。低於 480px 時，文件內距收緊，審閱列去掉 "by" 和代理的名稱，以保持在一行內。文字會換行，選取範圍和游標目標依即時的行來量測，靠近右邊緣時游標的標籤會移到箭頭左側。

### 你自己的用語

`Labels` 保存卡片顯示或朗讀的每一個字詞。包含名字或數量的文字是函式：

```csharp
card.Labels = new AgentCursorLabels
{
    Reading = agent => agent + " lit le document",
    Ready = count => count + " modifications prêtes",
    Accept = "Accepter",
};
```

### 內容準則

- 摘錄保持簡短：五到十個區塊。
- 用幾個字為每個編輯寫一個 `Description`（"Tightening the summary"）；狀態行和螢幕閱讀器會用到它。
- 修正編輯需要區塊文字中要取代的確切字詞。

### 最佳做法

- 儲存來自 `Accepted` 的文字，並以 `Blocks` 傳回。
- 在多次執行之間保持區塊 id 穩定，讓編輯找到它們的區塊。
- 想讓示範更快時使用 `Speed`，而不是縮短編輯。

### 常見問題

**使用者可以在文件中輸入嗎？** 不行。這些區塊是唯讀的；真正的編輯請使用編輯器內建的協作游標。

**如果編輯指向的區塊或字詞不存在會怎樣？** 該編輯會被略過。

## API 參考

<!-- api: AgentCursor -->

<!-- api: AgentCursorBlock -->

<!-- api: AgentCursorEdit -->

<!-- api: AgentCursorCollaborator -->

<!-- api: AgentCursorLabels -->

<!-- api: AgentCursorSimulation -->

<!-- api: AgentCursorDocumentEventArgs -->

<!-- api: AgentCursorPhaseChangedEventArgs -->

## 無障礙

- 一個禮貌層級的即時區域播報每一步：閱讀、目前的編輯、編輯就緒、已接受、已復原以及錯誤。模擬執行會附加 "Simulated, nothing was saved"。
- 游標、選取範圍和著色是裝飾性的，對輔助科技隱藏；文件仍然是真實的文字區塊。
- 狀態從不只依賴顏色：每個結束狀態都同時帶有圖示和文字，待審閱的變更在審閱列中計數。
- 焦點在卡片內或指標停在卡片上時，循環播放的模擬會停在審閱狀態，這樣沒人會在伸手去點時失去審閱列。
- 每個字詞都在 `AgentCursorLabels` 中。

### 鍵盤

| 按鍵 | 操作 |
|---|---|
| Tab | 在頁首的 Ask、Stop、Run again 或 Retry 之間移動，然後到審閱列中的 Undo 和 Accept changes |
| Enter、Space | 啟用取得焦點的按鈕 |

## 動態效果

| 變化 | 完整 | 減少 | 關閉 |
|---|---|---|---|
| 游標移動 | 沿一條平緩的弧線以緩入緩出曲線滑動，依距離 280 到 600ms | 直接跳到目標 | 直接跳到目標 |
| 游標出現或離開 | 從大頭貼中長出並在 200ms 內淡入淡出 | 淡入淡出 | 立即 |
| 選取範圍 | 隨拖曳逐行填滿 | 逐行填滿 | 逐行填滿 |
| 改寫 | 舊文字在 110ms 內淡出，然後每 34ms 寫入 3 到 6 個字詞，每段在 140ms 內淡入 | 新文字整體出現 | 新文字整體出現 |
| 區塊高度 | 以無回彈的 0.24s 彈簧跟隨內容；插入的區塊從零高度展開，然後文字淡入 | 立即 | 立即 |
| 狀態行 | 交叉淡化，100ms 淡出、160ms 淡入，代理工作時帶微光 | 短暫交叉淡化 | 立即 |
| 審閱列 | 上升 8px 並在 200ms 內淡入 | 淡入 | 立即 |
| 循環與同事漂移 | 審閱後循環；同事閱讀時輕微漂移 | 關閉 | 關閉 |

卡片不在畫面上時，播放會暫停。游標透過轉譯變換移動，因此移動從不觸發版面配置。

## 相關元件

- `AgentStatus`：顯示整個執行的即時狀態。
- `ToolCall`：顯示代理發起的工具呼叫。
- `AgentPlan`：顯示代理行動前寫下的計畫。
