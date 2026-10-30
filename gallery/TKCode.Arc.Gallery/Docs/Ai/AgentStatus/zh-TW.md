---
title: Agent status
summary: 執行中代理的即時狀態膠囊：其標記位於旋轉的環中，顯示目前的活動與計時器；按一下即展開成近期步驟，並提供暫停、停止與核准控制。
source-hash: f143ca47a5cb
---

<Demo name="Preview" />

## 何時使用

`AgentStatus` 是回答「代理是否仍在工作，以及正在做什麼？」最精簡也最誠實的方式。它是一個膠囊，放在標頭、標題列或撰寫框上方：代理的標記位於一個細環中，工作時環會旋轉；旁邊是目前的活動與其圖示，以及執行已經花了多久。按一下即可在原位把同一個膠囊展開成浮動面板，其中列出最近的步驟、一條分段進度條，以及暫停、停止、核准或重試的控制項。它下方的內容都不會移動。

- **應該一眼看出執行狀況的標頭或標題列**：代理現在正在做什麼、已經進行多久，而不必讓記錄佔滿頁面。
- **核准關卡**：等待人員處理的指令、部署或電子郵件。膠囊會變成一則請求，裡面直接有拒絕與核准按鈕，讓應用程式的任何位置都只需一按即可回覆。
- **偶爾才會查看的背景執行**：膠囊保持小巧；只有有人想看時，面板才會顯示最近幾個步驟與控制項。
- **失敗且可重試的執行**：膠囊會把環閉合成一個叉號，說明原因，並提供重試。
- **應用程式中的多個代理**：每個標頭插槽都可以放一個自己的膠囊；閒置的膠囊不會耗用任何動畫影格。

## 何時不用

- **執行本身就是頁面**：包含每個步驟、工具呼叫與輸出的完整記錄需要空間。請在聊天串中使用 `ToolCall` 列，或使用完整的執行檢視；膠囊的面板只列出最近的步驟。
- **跨代理的核准佇列**：請自行建立請求清單；一個膠囊只顯示一個代理的執行。
- **執行前由使用者編輯的計畫**：請使用 `AgentPlan`，它可以重新排序與編輯步驟。
- **在同一時間軸上比較多個代理**：請使用 `AgentTimeline`。
- **一個閃爍的字，沒有計時器也沒有控制項**：請使用 `TextShimmer`。

## 用法

未設定任何內容時，膠囊停在「就緒」（Ready）狀態：顯示名為 "Agent" 的預設標記，沒有環，也沒有計時器。套件不附帶任何範例執行。上方的預覽播放的是展示庫撰寫的模擬執行：一個以 Claude Sonnet 5.5 命名的程式代理，依序完成六個步驟，並在一個等待多久都可以的核准處停下。它的切換開關可切換到第二個執行（以 Gemini 4 Argon 命名的研究代理，其第三步會失敗一次，好讓你看到重試），「重播」則從頭開始；切換開關、「重播」按鈕與這些執行都屬於示範本身，不屬於膠囊。模型名稱是真實的；任務、檔案與計數是虛構的。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:AgentStatus />
</UserControl>
```

`AgentStatus` 衍生自 Avalonia 的 `TemplatedControl`。它位於 Special 套件中：請在 `UseArcControls()` 旁呼叫 `UseArcSpecial()` 來註冊。

在應用程式中，請由代理的事件驅動它。設定 `Status`，讓 `Activity` 保持為最新的工具呼叫（使用現在式），列出 `Steps`，給定一個在整個執行過程中固定不變的 `StartedAt`，並處理動作事件。有資料時，膠囊永遠不會自行變更狀態：按下核准會引發 `ApproveRequested`，接下來要怎麼做由你的程式碼決定。

```xml
<arc:AgentStatus Status="{Binding Run.Status}"
                 Activity="{Binding Run.Activity}"
                 StartedAt="{Binding Run.StartedAt}"
                 Agent="{Binding Run.Agent}" />
```

每個動作都是帶有型別參數的路由事件（`PauseRequested`、`ResumeRequested`、`StopRequested`、`ApproveRequested`、`DenyRequested`、`RetryRequested`、`OpenRunRequested`）。

```csharp
status.PauseRequested += (_, e) => agent.Pause(run.Id);
status.ApproveRequested += (_, e) => agent.Approve(run.Id);
```

```csharp
status.Steps.Add(new AgentStatusStep("scan", "Scanned 9 modules", AgentStatusStepState.Done, ArcIcons.Search)
{
    Duration = TimeSpan.FromMilliseconds(1917)
});
status.Activity = new AgentStatusActivity("Checking specs", ArcIcons.Search, progressValue: 8, progressTotal: 12);
```

### 每一種狀態

膠囊有七種狀態。每一種都以文字搭配環與標記，因此狀態不會只靠顏色區分。按下膠囊即可展開其面板。

<Demo name="States" />

| 狀態 | 環與標記 | 標籤 | 計時器 | 按鈕 |
|---|---|---|---|---|
| `Idle` | 只有標記，沒有環 | 以次要顏色顯示 "Ready" | 隱藏 | 面板中的開啟執行 |
| `Working` | 軌道加上一段短弧，每 0.9 s 轉一圈 | 目前的活動與其圖示；文字閃爍 | 計數 | 面板中的暫停與停止 |
| `NeedsInput` | 弧線消失；一個強調色圓點以一次脈動彈出 | 文字顏色的請求內容 | 持續計數 | 行內的拒絕與核准；面板中的核准與拒絕 |
| `Paused` | 弧線凍結並轉為灰色，標記變淡 | 附暫停圖示的 "Paused" | 凍結 | 面板中的繼續與停止 |
| `Done` | 環以成功顏色閉合，標記變成繪製出來的勾號 | "Done in 2m 23s · 5 files touched" | 隱藏 | 開啟執行 |
| `Failed` | 環以危險顏色閉合成一個叉號 | 失敗的內容 | 隱藏 | 行內與面板中的重試 |
| `Stopped` | 沒有環；一個小方塊取代標記 | 以次要顏色顯示 "Stopped after 7s" | 隱藏 | 開啟執行 |

結束的執行（`Done` 或 `Stopped`）會在 `SettleAfter` 之後（預設 4 秒；設為 null 則結果會保留在畫面上）回到 "Ready"。面板會保留上一次執行的步驟與計數器，並引發 `Settled`。

### 結構

1. **圓盤**：24 px 的圓盤。代理的標記在 1.5 px 環的中央以 12 px 繪製。工作時，環是一條軌道加上一段短弧；完成或失敗時是閉合的圓；閒置或停止時則沒有環。
2. **標籤**：16 px 的圖示加上一行 14 px 文字。新的活動會從下方滾入，舊的活動則向上滾出。像 "8 of 12" 這樣的計數會原地變更數字，並且是閃爍效果的一部分。當標籤寬於可用空間時，其右緣會在 24 px 內淡出，而不是換行。
3. **計時器**：以等寬數字顯示的分與秒（例如 "2:20"），在執行進行中時顯示。
4. **行內按鈕**：需要輸入時的拒絕與核准，失敗後的重試；高度 30 px。
5. **面板**：從膠囊展開。一行包含代理名稱與 "Step 3 of 6"（執行結束後為 "6 of 6 steps"），一條進度條（每個步驟一段 3 px），最近五個步驟（含圖示、可換行的標籤與時間），以及附有按鈕的頁尾。

### 面板

面板從膠囊自身的表面展開：寬度與高度一起以彈簧效果伸展，標頭列保持原位，內容則稍後淡入。它浮在視窗的覆蓋層中，因此會裁切子元素的標頭或捲動器都無法切掉它，下方的內容也不會移動。它的寬度為 384 px 或膠囊寬度（取較大者），且永遠不會超過容器寬度。

`PanelAlignment` 決定面板沿著哪一邊展開：`Start` 適用於位在標頭起點的膠囊，`End` 適用於位在終點的膠囊，`Center`（預設）則向兩側均等展開。

<Demo name="HeaderBar" />

`IsExpanded` 是雙向的。按下膠囊、Escape 與按下外部區域都會改變它；你也可以從程式碼設定它，或繫結它以自行開啟面板。`ExpandedChanged` 會回報每一次變更。`Expand()`、`Collapse()` 與 `Toggle()` 可從程式碼執行相同的操作。

```xml
<arc:AgentStatus IsExpanded="{Binding IsRunOpen, Mode=TwoWay}" PanelAlignment="End" />
```

面板列出最近的 `MaxSteps` 個（預設五個）非待處理步驟。待處理的步驟仍會計入進度條。新步驟會在底部淡入，較舊的步驟向上滑動，最舊的則在頂端淡出。步驟依 `Id` 比對，因此從 "Running tests" 變成 "Passed 12 of 12 tests" 的步驟會保留其列：舊的標籤淡出，新的標籤接替其位置。

### 你自己的執行

步驟標籤在執行時以現在式撰寫，完成後以過去式撰寫，讓文字本身就能傳達狀態。步驟的 `StartedAt` 會讓它在執行時以整數秒計時；已完成的步驟則以一位小數顯示其 `Duration`（"1.9s"）。失敗與略過的步驟不顯示時間。

計時器從 `StartedAt` 開始計算，並排除暫停與因失敗而自行停止的時間。請設定一次執行的真實開始時間，並保持固定；若你的後端知道 `EndedAt`，請設定它，否則會使用狀態變為完成或停止的那一刻。

<Demo name="HostDriven" />

```csharp
pill.ApproveRequested += (_, e) =>
{
    agent.Approve(run.Id);          // your backend
    pill.Activity = new AgentStatusActivity("Deploying build 2.4.1", ArcIcons.CloudUpload);
    pill.Status = AgentStatusRunState.Working;
};
```

`Notice` 是給螢幕閱讀器的一次性訊息，例如某個動作的結果；它會併入下一次播報。膠囊顯示或播報的每一個文字都來自 `Labels`（一個 `AgentStatusLabels`），包括含有數字的句子，因此膠囊可以翻譯或改寫。

### 只提供你能支援的動作

按鈕只有在 `Actions` 允許該動作、且執行狀態需要它時才會顯示：執行中顯示暫停，暫停時顯示繼續，執行中或暫停時顯示停止，需要輸入時顯示核准與拒絕，失敗後顯示重試，而開啟執行則在每種狀態都會顯示。`Actions` 是旗標型別的值，預設全部開啟。它與「只在處理常式存在時才顯示控制項」的做法相對應；請將它設為你的應用程式真正處理的項目。

<Demo name="Actions" />

```xml
<arc:AgentStatus Actions="Approve, Deny, OpenRun" />
```

面板頁尾的前兩個按鈕在狀態改變時會保留其位置：暫停變為繼續，核准變為暫停，停止變為拒絕。這是同一個按鈕，因此鍵盤焦點會留在它上面。當有焦點的按鈕永久消失（例如執行結束後的暫停）時，焦點會移到膠囊，而不會移到頁面上。開啟執行會以勾號與文字 "Opened" 確認約 1.6 秒。

### 模擬

模擬執行用於預覽、新手導覽與測試，由你的應用程式撰寫。`AgentRunScenario` 包含代理、步驟、結果與通知；每個 `AgentRunScenarioStep` 都有一個現在式標籤、一個過去式標籤、一個圖示、一段時長、一個可選的計數、一個可選的核准關卡，以及可選的首次嘗試失敗。`AgentRunSimulation` 是沒有 UI 的驅動器，它會以計時器播放該情境：列出步驟、推進計數、無限期等待核准或失敗，並把暫停、繼續、停止、核准、拒絕、重試與開啟執行套用到它自己的狀態上。把它交給 `AgentStatus.Simulation`，膠囊出現在畫面上後就會啟動它、顯示它並把按鈕的操作導回它；你也可以自行讀取其 `Status`、`Activity`、`Steps` 與 `Notice`，以驅動任何其他檢視。

```csharp
var run = new AgentRunScenario
{
    Agent = new AgentModel("Release bot", ArcIcons.Sparkles),
    Outcome = "2 files touched",
    Steps =
    [
        new() { Id = "read", ActiveLabel = "Reading 12 files", DoneLabel = "Read 12 files",
                Icon = ArcIcons.Search, Duration = TimeSpan.FromSeconds(2) },
        new() { Id = "push", ActiveLabel = "Pushing the branch", DoneLabel = "Pushed the branch",
                Icon = ArcIcons.CloudUpload, Duration = TimeSpan.FromSeconds(1.5),
                Request = "Waiting on you to push", WaitingLabel = "Approval needed to push" }
    ]
};
var simulation = new AgentRunSimulation(run);
pill.Simulation = simulation;
simulation.Replay();                                   // clears the run and starts again after 0.5 s
simulation.Scenario = otherRun;                        // starts over after 0.9 s
```

在 XAML 中，請在 `<arc:AgentStatus.Simulation>` 裡為每個膠囊建立一個模擬，並用 `{x:Static}` 設定它的 `Scenario`，寫法見預覽的 Code 分頁（無參數的模擬只有一個空執行，會停在就緒狀態）。起始延遲為 0.9 秒（`StartDelay`），呼叫 `Replay()` 後為 0.5 秒（`ReplayDelay`）。已完成或已停止的執行會保持原樣，直到膠囊將其回復。計時器執行於 `TimeProvider` 之上，因此測試可以用手動時鐘推進執行過程；膠囊另外提供自己的 `TimeProvider` 屬性，用於計時器與回復延遲。把 `Simulation` 設回 null 後，膠囊停在就緒狀態；執行本身會繼續，直到你呼叫 `Dispose()`。

### 窄容器

膠囊依循其容器的寬度，而不是視窗的寬度，且絕不會超過它。可用寬度低於 400 px 時，行內的拒絕、核准與重試會變成 30 px 的圖示按鈕，並帶有無障礙名稱；這些按鈕顯示時，計時器會退到一旁；面板中的開啟執行會變成圖示按鈕。過長的標籤會在其右緣淡出，而面板中的步驟標籤會換行，不會被截斷。

<Demo name="Narrow" />

### 減少動態效果

在減少動態效果下，每種狀態、文字、焦點與播報都會保留。寬度與面板會立即變化，新的標籤會原地淡入而不滾動，環不旋轉，圓點不脈動，勾號與閉合的環會以已繪製的狀態出現，工作中的文字也不閃爍。

<Demo name="ReducedMotion" />

### 內容指引

膠囊的標籤要簡短；歷史紀錄交給面板。請把活動寫成代理現在正在做的事（"Reading 12 files"），把請求寫成一個提出問題的句子（"Waiting on you to deploy to staging"），把失敗寫成發生了什麼事（"Fetch failed · 2 pages did not load"），而結果則只寫成果本身，時間由膠囊處理：膠囊會寫成 "Done in 1m 12s · 5 files touched"。

| 不要寫 | 改為寫 |
|---|---|
| 處理中 | 檢查規格 |
| 代理目前正在讀取專案的檔案 | 正在讀取 12 個檔案 |
| 等待中 | 等待你部署到 staging |
| 錯誤 | 抓取失敗 · 2 個頁面未能載入 |
| 執行完成！全部成功 | 5 個檔案已變動 |

### 最佳做法

**建議**

- 每個核准關卡（指令、推送、付款、電子郵件）都使用 `NeedsInput`，並把標籤寫成請求的句子。
- 整個執行過程中保持 `StartedAt` 固定，讓膠囊扣除暫停與失敗的時間。
- `Actions` 中只傳入你的後端支援的動作。
- 把膠囊放在會設定其寬度的容器中，例如標頭插槽，讓它能淡出過長的標籤並調整面板大小。
- 透過 `AgentModel.MarkBrush` 為標記指定品牌色；預設的標記以文字顏色繪製。

**避免**

- 把它當成記錄使用。它只列出最近的步驟，其餘的會被捨棄。
- 每次計數跳動都重寫 `Activity`。只變更計數即可（`new AgentStatusActivity(label, icon, 9, 12)`）：數字會原地變化，螢幕閱讀器也不會被打斷。
- 在標籤中放入很長的句子。它會在邊緣淡出，完整文字則保留在膠囊的無障礙名稱中。
- 同一個畫面上放置多個屬於同一次執行的膠囊。

### 常見問題

**為什麼面板不會把頁面往下推？** 它浮在視窗的覆蓋層中，位於頁面之上，從膠囊的佔用範圍開始展開。膠囊在版面中仍保持原位。

**設定 `ClipToBounds` 的標頭能容納膠囊嗎？** 可以。收合的膠囊在標頭之內；展開的面板則不在。

**如何為已經開始的執行顯示計時器？** 把 `StartedAt` 設為真正的開始時間，膠囊就會從那裡開始計算。

**如果同時設定 `Status` 與 `Simulation` 會怎樣？** 以你的資料為準：只要設定了 `Status`、`Activity`、`StartedAt` 或某個步驟，膠囊就顯示它們，不理會模擬（不會啟動它，按鈕也只引發各自的事件）。單獨設定 `Agent` 不算：它只是為模擬執行的代理命名。

**為什麼計數不像新活動那樣滾動？** 只有新的標籤與圖示會滾動。計數原地變更，因此每秒可以跳動數次。

## API 參考

<!-- api: AgentStatus -->

<!-- api: AgentStatusActivity -->

<!-- api: AgentStatusStep -->

<!-- api: AgentStatusLabels -->

<!-- api: AgentStatusActionEventArgs -->

<!-- api: AgentStatusExpandedChangedEventArgs -->

<!-- api: AgentRunSimulation -->

<!-- api: AgentRunScenario -->

<!-- api: AgentRunScenarioStep -->

列舉：

| 類型 | 值 |
|---|---|
| `AgentStatusRunState` | `Idle`, `Working`, `NeedsInput`, `Paused`, `Done`, `Failed`, `Stopped` |
| `AgentStatusStepState` | `Pending`, `Active`, `Waiting`, `Done`, `Failed`, `Skipped` |
| `AgentStatusActions` | `None`, `Pause`, `Resume`, `Stop`, `Approve`, `Deny`, `Retry`, `OpenRun`, `All` (flags) |
| `AgentStatusPanelAlignment` | `Start`, `Center` (default), `End` |

代理是 `AgentModel`（名稱、24 × 24 的描邊標記及其筆刷），與其他代理元件共用。

## 無障礙

- 膠囊是一個具有展開狀態的按鈕。其名稱會朗讀代理、帶計數的活動與已經經過的時間（"Claude Sonnet 5.5: Checking specs 8 of 12, 0:09 elapsed"）；執行結束後則朗讀其結果。
- 禮貌即時區域會播報每個新的活動與每個結果（"Claude Sonnet 5.5: done in 14 seconds, 5 files touched"），以及透過 `Notice` 傳來的動作結果。它不會播報計數或計時器的跳動。
- 展開的面板是一個名為 "{agent} run details" 的區域；進度條是名為 "Run progress" 的進度條，其值例如 "4 of 6 steps finished"。
- 狀態從不只靠顏色：每種狀態都有一個文字（Ready、Paused、Done in、Stopped after）以及不同的環或字符；步驟在執行中使用現在式，完成後使用過去式。
- 窄容器中的圖示按鈕都有無障礙名稱。被截斷的標籤在膠囊的名稱中以及面板的步驟清單中保持完整。
- 鍵盤焦點會顯示 2 px 的焦點環，在膠囊上內縮、在按鈕外側；滑鼠焦點則不顯示。
- 焦點會被保留：改變角色的按鈕會保留焦點；當有焦點的控制項消失時，焦點會移到膠囊。

### 鍵盤

| 按鍵 | 操作 |
|---|---|
| `Tab` / `Shift+Tab` | 先移到膠囊，再移到行內的拒絕與核准或重試。面板展開時：暫停或繼續、停止或拒絕、開啟執行，然後到頁面上的下一個控制項 |
| `Enter` / `Space` 於膠囊 | 開啟或關閉面板 |
| `Enter` / `Space` 於按鈕 | 按下該按鈕 |
| `Escape` | 在展開的面板內任何位置，關閉面板並把焦點移回膠囊 |

按下展開面板之外的位置會關閉面板，焦點則停留在按下的位置。面板不是模式對話方塊，也不會把焦點鎖在其中。

## 動態效果

- 新的活動會在 180 ms 內從下方 12 px 處滾入，舊的活動則在 140 ms 內向上滾出；膠囊的寬度以無回彈的彈簧效果跟隨變化；當標籤寬於可用空間時，其右緣會淡出。
- 面板會從膠囊的佔用範圍開始，寬度與高度一起以彈簧效果伸展（約 0.75 秒穩定）；內容在 80 ms 後以 200 ms 淡入，關閉時則以 75 ms 淡出。
- 環在工作時每 0.9 秒轉一圈；暫停時在原位凍結，繼續時從相同角度接續。需要輸入時，弧線消失，圓點以一次 0.9 秒的脈動彈入。執行結束時，環會在 0.44 秒內閉合繪製，標記縮小消失，而勾號（或叉號）會在短暫停頓後的 0.28 秒內繪製出來。
- 工作中的標籤有一個 2.4 秒循環的閃爍效果（1.7 秒掃過，然後暫停）；計數與作用中步驟的標籤會跟著閃爍。
- 新步驟在 155 ms 內淡入，較舊的步驟以平滑的彈簧效果向上滑動，進度段則在 480 ms 內從左側填滿。

| 變化 | 完整 | 減少 | 關閉 |
|---|---|---|---|
| 新活動 | 滾動並淡入 | 原地淡入 100 ms | 直接取代 |
| 膠囊寬度與面板大小 | 以彈簧伸展 | 立即 | 立即 |
| 環、閃爍、脈動 | 有動畫 | 靜止 | 靜止 |
| 完成與失敗 | 環與勾號繪製 | 以已繪製的狀態顯示，短暫淡入 | 以已繪製的狀態顯示 |
| 計時器與計數 | 原地變化 | 原地變化 | 原地變化 |

## 相關元件

- `ToolCall`：逐字記錄中的單一呼叫，含其輸入與輸出。
- `AgentPlan`：代理將執行的步驟，可在執行前編輯。
- `AgentTimeline`：在同一時間軸上重播多個代理。
- `TextShimmer`：單一閃爍的字。

| 元件 | 適用時機 |
|---|---|
| `AgentStatus` | 單一代理的執行應顯示在標頭中，並可按需展開 |
| `ToolCall` | 你在逐字記錄中列出呼叫 |
| `AgentPlan` | 使用者在執行前先編輯並核准步驟 |
| `AgentTimeline` | 多個代理與交接共用一條時間軸 |
| `Alert` | 你以文字回報真實作業的狀態 |
