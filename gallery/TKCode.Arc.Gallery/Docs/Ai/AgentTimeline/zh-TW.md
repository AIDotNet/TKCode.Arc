---
title: Agent timeline
summary: 在同一條時間軸上重播多代理執行過程：每個代理的思考、工具呼叫與等待、代理之間的交接，以及時間、token 與成本。
source-hash: ba648bfd9702
---

<Demo name="Preview" />

## 何時使用

`AgentTimeline` 以精簡的甘特圖呈現一次由多個 AI 代理編排完成的執行過程。協調器位於最上方一列，每個子代理在其下各佔一列，列上有一個色點、名稱，以及它所使用的模型。共用時間座標軸上的時間段說明每個代理做了什麼、何時做的：思考是代理顏色的淡色變化，工具呼叫為實心，等待則以斜線網底表示。細的直角連接線表示交給子代理的工作，以及結果回傳給上層的過程。執行過程會以播放頭重播，標頭會計算經過的時間、token 與成本，最後以包含並行加速比的摘要結束。

- **執行紀錄與追蹤檢視器**：顯示一次已完成之多代理執行過程的時間都花在哪裡、哪些代理平行工作，哪些只是在等待。
- **除錯面板**：失敗的工具呼叫及其重試會以叉號與重試箭頭醒目標示；釘選某個時間段，即可追蹤它在整次執行中的交接路徑。
- **產品示範與更新紀錄**：以真實執行過程的實際時間、token 與成本重播，可選 1x 或更快的速度。
- **追蹤進行中的執行過程**：即時模式會讓尚未結束的時間段延伸到目前時間，同時你的後端持續附加事件。

## 何時不用

- **單一代理的步驟與答案**：當人們需要的是代理的推理、來源與輸出，而非時間安排時，請以 `ToolCall` 列或 `AgentPlan` 顯示對話紀錄。
- **一眼看出單一代理的狀態**：請使用 `AgentStatus`，它是精簡的即時膠囊。
- **分散式請求追蹤**：服務與網路呼叫的時間段應放在追蹤檢視中，而非這裡；時間軸的詞彙（思考、工具呼叫、等待、交接）是關於代理的。
- **數千個步驟的長追蹤**：時間軸可以從容處理數百個時間段。若追蹤更長，請傳入執行過程的一段視窗，或先合併極小的時間段。
- **說明一般代理如何運作**：請使用 `AgentLoop` 插圖，它以虛構的步驟說明這個故事。

## 用法

未提供資料時，時間軸會顯示一次虛構的範例執行過程，並在第一次它有約五分之一進入可視範圍時開始重播。請在 `UseArc()` 旁呼叫 `UseArcSpecial()` 來註冊該套件。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:AgentTimeline />
</UserControl>
```

`AgentTimeline` 衍生自 Avalonia 的 `TemplatedControl`。它會填滿其容器，最寬到 744px；在 360 到 760px 寬度之間閱讀最舒適。

### 結構

1. **標頭**：`Title`（若未設定，則為執行過程的標題，再否則為 "Agent run"），其下為狀態列。重播進行中時，狀態列顯示目前狀態（Running 並帶有閃爍效果、Paused 或 Ready）、經過的時間、token 與成本；結束時會變成勾號與摘要：代理數、長度、並行加速比與成本。
2. **代理標籤**：每個代理一列，高 44px，協調器排在最前面：一個色點、名稱，以及其下的模型標記與模型名稱。
3. **繪圖區**：所有代理的時間段位於同一條時間座標軸上，並在刻度處有格線。仍在執行的時間段，前緣為方形，並帶有一道短暫的光澤；已結束的時間段則為圓角。
4. **連接線**：從協調器往下到子代理下一個時間段的直角連接線（派送），或從子代理最後一個時間段往上到協調器的直角連接線（結果），在與上一列相交處有一個圓點。
5. **播放頭與座標軸**：一條細線標示目前時間；列下方則是座標軸、刻度標籤與拖曳定位的把手。
6. **頁尾**：左側為播放或暫停（完成後則重播）、重新開始與速度控制項；右側為圖例（Thinking、Tool call、Waiting、Handoff、Failed），空間不足時則移到控制項下方。
7. **時間段卡片**：將滑鼠移到時間段上或使其取得焦點，即可看到是哪個代理在哪個模型上做了什麼、它的工具或種類、長度與 token，以及失敗、重試或交接的註記。

### 範例執行過程

範例是一項虛構的事件調查，名為 "Signup emails bouncing since Mar 3"。協調器規劃三項檢查，並將它們交給狀態監看器、設定稽核員與日誌讀取器，三者平行工作。日誌讀取器的第一次掃描失敗，改用較窄的查詢後第二次就成功。協調器彙整各份報告，由書記撰寫事件說明並歸檔。五個代理、30 個時間段與八次交接，共耗時 26.8 秒、49,920 token、$0.15，並行加速比為 1.5x。`AgentTimelineSamples.CreateIncidentRun()` 會回傳它的新副本，`CreateRefundRun()` 則回傳一個小型的兩代理執行過程。

### 你自己的執行過程

把代理（依列的順序排列，協調器在最前）、以秒為單位、從執行開始計時的時間段，以及交接，交給時間軸即可，形式為 `AgentTimelineRun`。自訂的執行過程一開始就是已結束的狀態，可直接重播；若要改為在進入可視範圍時自動重播，請設定 `AutoPlay="True"`。

<Demo name="OwnRun" />

```xml
<arc:AgentTimeline>
    <arc:AgentTimeline.Run>
        <arc:AgentTimelineRun Title="Monthly refund summary">
            <arc:AgentTimelineRun.Agents>
                <arc:AgentTimelineAgent Id="planner" Name="Planner" Price="8" />
                <arc:AgentTimelineAgent Id="clerk" Name="Data clerk" Price="0.2" />
            </arc:AgentTimelineRun.Agents>
            <arc:AgentTimelineRun.Spans>
                <arc:AgentTimelineSpan AgentId="planner" Kind="Thinking" Start="0" End="1.2"
                                       Label="Outlined the summary" Tokens="1100" />
                <arc:AgentTimelineSpan AgentId="planner" Kind="Waiting" Start="1.2" End="5.8"
                                       Label="Waiting on the data clerk" />
                <arc:AgentTimelineSpan AgentId="clerk" Kind="Tool" Start="1.5" End="3.9"
                                       Label="Exported last month's refunds" Tool="Ledger"
                                       ToolMark="{x:Static arc:ArcIcons.Database}" Tokens="2200" />
            </arc:AgentTimelineRun.Spans>
            <arc:AgentTimelineRun.Handoffs>
                <arc:AgentTimelineHandoff From="planner" To="clerk" At="1.2" Label="Which refund reasons grew?" />
            </arc:AgentTimelineRun.Handoffs>
        </arc:AgentTimelineRun>
    </arc:AgentTimeline.Run>
</arc:AgentTimeline>
```

```csharp
timeline.SpanSelected += (_, e) => details.Show(e.Span);   // null when the pin is removed
```

- **代理**：`Id` 用來在時間段與交接中指名該代理。`Model` 是一個 `AgentModel`（名稱加上 24×24 描邊標記，例如 `ArcIcons` 字符）。`Price` 是混合費率，單位為每百萬 token 的美元。`Color` 會覆寫列的顏色；預設情況下，協調器為中性色，其後的代理依序取圖表系列色 1 到 4。
- **時間段**：`Kind` 為 `Thinking`、`Tool` 或 `Waiting`。工具呼叫會指名其 `Tool`，並可帶有 `ToolMark`（未設定時為扳手）。`Status="Failed"` 搭配 `Detail` 會標示失敗的呼叫；`IsRetry` 標示重試該呼叫的那一次，其註記放在 `Detail`。`Tokens` 會在時間段內平均累加，`Cost` 則會取代依代理單價計算出的成本。
- **交接**：形狀由系統推斷。沿列往下的工作是派送：它在 `At` 落下，並轉為目標的下一個時間段。往上的工作是結果：它從來源最後一個時間段的結尾離開，並在 `At` 升到目標。派送的時間應略早於目標的第一個時間段，結果的時間則應略晚於來源的最後一個時間段。
- 時間軸上的 `Agents`、`Spans` 與 `Handoffs` 會覆寫執行過程的清單，而且每個清單在發出集合變更通知時都會被觀察。時間段是 `AvaloniaObject`，因此新的 `End` 或 `Status` 會立即反映到繪圖區。

### 數字與格式

- **時間**：一位小數，四捨五入（"26.8s"）；滿一分鐘後為 "1m 05.3s"。
- **Token**：每個時間段的 token 從起點到終點平均累加，加總後四捨五入，並以千分位分隔。
- **成本**：每個時間段的 token 乘以其代理每百萬的單價（或時間段自己的 `Cost`），取兩位小數。
- **加速比**：所有代理花在思考與呼叫工具上的時間加總，再除以執行過程的長度。等待屬於閒置時間，不計入。低於 1.05 時，摘要會顯示 "no parallel speedup"。
- 計數器約每秒更新二十次，且每個計數器都保留其最終數值的寬度，因此標頭在計數時不會跳動。

座標軸的長度與執行過程相同。刻度間距取自以下最小的一個：0.1、0.2、0.5、1、2、5、10、15、30 秒，然後是 1、2、5、10、15、30 分鐘，使得從零開始最多六個刻度，且彼此至少相距 40px。因此 26.8 秒會在任何寬度下以五秒為間隔顯示 0 到 25；6.2 秒以兩秒為間隔；一分鐘則以 15 秒為間隔。

### 播放與速度

`IsPlaying` 與 `Speed` 是雙向的：頁尾會寫入它們，應用程式也可以繫結它們。播放會從重播目前所在的位置繼續；播放完畢後，則會從頭開始。重新開始一律回到零，並以目前速度播放。`Speeds` 列出速度控制項的選項（預設為 1x 與 2x）；只有一個選項或沒有選項時，控制項會隱藏。`Play()`、`Pause()`、`Restart()` 與 `Seek(seconds)` 可在程式碼中執行相同的操作。`PlaybackChanged` 會回報每一次人為的操作（播放、暫停、重新開始、拖曳定位、速度），而 `Completed` 則在每次重播到達結尾時觸發。

<Demo name="Speeds" />

在 1x 時，重播以即時速度執行。單一影格最多前進 0.1 秒乘以速度，因此視窗停滯時會損失一些時間，而不會跳著前進；時間軸移出螢幕時，時鐘也會停止。

在座標軸上按下或拖曳任何位置，會將重播移到該處並暫停；此時把手會取得鍵盤焦點。`Phase` 在零點為 Ready，播放中為 Running，中途暫停為 Paused，結束時為 Finished。

### 釘選時間段

點擊時間段，或在其上按下 Enter，即可釘選。卡片會留在畫面上，而不在時間段交接路徑上的所有內容都會淡化：時間段降到 28%，連接線降到 16%。若是協調器的時間段，路徑包含該時間段本身、在它期間開始或結束的交接，以及這些交接另一端的時間段。若是子代理的時間段，路徑則為該代理的整列、與該代理有關的每一次交接，以及這些交接另一端的時間段。再次點擊該時間段、再次按下 Enter 或按 Escape，即可取消釘選。`PinnedSpan` 為雙向繫結，`SpanSelected` 則會回報每次的變更。

### 即時執行

執行過程進行中時設定 `IsLive`。時間從執行的 `StartedAt` 開始計算（若未設定，則從即時模式開始的那一刻起算）；沒有 `End` 的時間段會延伸到目前時間，播放控制項則會隱藏。事件到達時附加時間段與交接，並在每個時間段結束時設定其 `End`。當時間超過座標軸的 90% 時，座標軸會以平穩的步調擴展：它會以彈簧效果延伸到超過時間一點五倍的整齊長度（5、8、12、20、30、50 秒，依此類推）。執行過程結束時設定 `IsLive` 為 false：時間軸會停在摘要上，對外宣告，並觸發 `Completed`。

<Demo name="Live" />

```csharp
timeline.Run = liveRun;            // an AgentTimelineRun your event handler appends to
timeline.IsLive = true;
// on each event
liveRun.Spans.Add(new AgentTimelineSpan("logs", AgentTimelineSpanKind.Tool, 3.1, null, "Bounce scan"));
span.End = 5.2;
// when the run ends
timeline.IsLive = false;
```

`AgentTimelineLiveDriver` 會把一次已結束的執行過程，當作正在此時發生般播放到時間軸中，上方的示範即以此運作。

### 窄容器

版面配置依循時間軸本身的寬度，而不是視窗的寬度。寬度低於 520px 時，標籤欄會從 176 縮窄為 132px，且只顯示色點與名稱（時間段卡片仍會標示模型）；內距則從 24 降為 16px。當繪圖區窄於 300px 時，它會保持 300px 寬，並讓標籤固定不動、橫向捲動；捲動後，標籤旁會出現分隔線；除非讀者在最近 2.5 秒內有捲動，否則重播會讓播放頭維持在可視範圍內。狀態列會在分隔符號後換行，圖例則移到控制項下方，並自行換行。

<Demo name="Narrow" />

### 內容指引

每個時間段的標籤都應寫成代理做了什麼，使用過去式，且只用幾個字；卡片會完整顯示它，但螢幕閱讀器的無障礙名稱會把代理、種類與時間放在其前後一起朗讀。工具請以你的產品中的實際用途來命名，並以通用字符作為標記。交接標籤請保持為一句簡短的話：它們會在卡片中以 "From Coordinator: …" 與 "To Coordinator: …" 的形式出現。

| 不要寫 | 改為 |
|---|---|
| Tool call | 搜尋了我們網域的黑名單報告 |
| LLM generation step 3 | 將退信比對到 SPF 驗證失敗 |
| Error | 完整掃描郵件事件超過時間限制 |
| Handoff | 自 3 月 1 日起的郵件設定變更 |

### 最佳做法

**建議**

- 把協調器放在最前面，並安排子代理的順序，讓連接線穿過閒置的列：最先回傳結果的代理緊接在協調器下方，派送時則先派給最深的那一列。
- 將阻塞的時間標為 `Waiting`，讓平行工作與閒置時間一眼可辨，且加速比只計入真正的工作。
- 以 `Status="Failed"` 與 `Detail` 標示失敗的呼叫，並以 `IsRetry` 標示重試它的那次呼叫。
- 為代理設定 `Price`，讓成本具有意義；或者乾脆所有代理都不設定，而不是只設定其中一部分。

**避免**

- 使用真實模型供應商或服務的品牌標誌。請使用通用字符，並在文字中說明模型名稱。
- 在即時執行過程中，每次事件都替換整個執行物件；應改為附加到它的清單中。
- 同一畫面上有數個正在重播的時間軸。每個時間軸在播放時都會逐影格繪製。

### 常見問題

**為什麼我的執行過程一開始就是已結束的狀態？** 自訂的執行過程屬於紀錄，因此一開始即完整呈現；若要在進入可視範圍時重播，請設定 `AutoPlay="True"`。在減少動態效果下，每個時間軸都會以已結束的狀態開啟。

**為什麼連接線只是一小段短短的樁？** 派送會尋找目標在其時間或之後開始的第一個時間段。在即時執行過程中，該時間段尚未到達；待它到達後，直角連接線才會完成。

**為什麼卡片顯示的時間比時間段更長？** 卡片一律顯示時間段的最終長度、token 與交接，即使重播仍在繪製它也一樣。

**我可以翻譯它嗎？** 可以。將 `Labels` 設為一個 `AgentTimelineLabels` 物件，並填入你的文字；含有數字的句子是函式，因此詞序可以依語言調整。

## API 參考

<!-- api: AgentTimeline -->

<!-- api: AgentTimelineRun -->

<!-- api: AgentTimelineAgent -->

<!-- api: AgentTimelineSpan -->

<!-- api: AgentTimelineHandoff -->

<!-- api: AgentTimelineLabels -->

<!-- api: AgentTimelineSpanSelectedEventArgs -->

<!-- api: AgentTimelinePlaybackChangedEventArgs -->

<!-- api: AgentTimelineLiveDriver -->

<!-- api: AgentModel -->

列舉與值：

| 類型 | 值 |
|---|---|
| `AgentTimelineSpanKind` | `Thinking`, `Tool`, `Waiting` |
| `AgentTimelineSpanStatus` | `Ok` (default), `Failed` |
| `AgentTimelinePhase` | `Ready`, `Running`, `Paused`, `Finished`, `Live` |
| `AgentTimelinePlaybackAction` | `Play`, `Pause`, `Restart`, `Seek`, `Speed` |
| `AgentTimelineSummary` | `AgentCount`, `Duration`, `Speedup`, `Tokens`, `Cost` (the `Summary` property) |

`AgentTimelineSamples` 保存虛構的執行過程（`CreateIncidentRun()`、`CreateRefundRun()`）。

## 無障礙

- 時間軸以其標題命名。每個時間段對輔助技術而言都是一個按鈕，其名稱包含代理、種類、工具、"failed" 或 "retry"、它做了什麼、時間範圍與 token（例如 "Log reader, Tool call, Mail logs, failed: Bounce scan took too long, 3.1s to 5.2s, 430 tokens"，即日誌讀取器、工具呼叫、郵件日誌、失敗：掃描逾時，3.1 秒到 5.2 秒，430 個 token）；釘選的時間段會被讀為已按下。時間段只佔一個 Tab 停駐點，方向鍵則在它們之間移動。
- 繪圖區是以繪製方式呈現的，因此時間段本身沒有各自的控制項：鍵盤所在的時間段上會有一個焦點替身，帶有它的名稱；每按一次方向鍵，焦點就會移到一個新的替身上，讓螢幕閱讀器朗讀新的時間段。
- 拖曳定位器是一個名為 "Run time" 的滑桿，其值為目前時間，並附有 "12.4s of 26.8s" 之類的說明文字。
- 狀態絕不只靠顏色傳達：失敗的呼叫在其卡片與圖例中會顯示叉號與 "Failed"，重試會顯示箭頭與其註記，而完成狀態則顯示勾號與摘要。
- 當讀者操作時，禮貌即時區域會朗讀：按下播放、重新開始或在播放中變更速度時朗讀 "Playing at 1x"，暫停或拖曳定位時朗讀 "Paused at 11.1s"，結束時朗讀執行過程的摘要。使用者第一次看到的重播是靜默的；計數中的狀態列不是即時區域（其無障礙名稱包含計數與忙碌中的代理數量）。
- 時間段卡片會重複時間段名稱已說出的內容，因此對輔助技術隱藏。
- 鍵盤焦點會在時間段、把手與控制項周圍顯示 2px 的焦點環；滑鼠焦點則不顯示。

### 鍵盤

| 按鍵 | 操作 |
|---|---|
| `Tab` / `Shift+Tab` | 在時間段（一個停駐點）、拖曳定位器、播放或暫停、重新開始與速度控制項之間移動 |
| `Left` / `Right` 於時間段 | 同一列中的上一個或下一個時間段 |
| `Up` / `Down` 於時間段 | 上一列或下一列中，中點在時間上最接近的時間段（跳過尚無時間段的列） |
| `Home` / `End` 於時間段 | 其所在列的第一個或最後一個時間段 |
| `Enter` / `Space` 於時間段 | 釘選並突顯其交接路徑；再按一次則取消釘選 |
| `Escape` 於時間段 | 取消釘選 |
| `Left` / `Down`，`Right` / `Up` 於拖曳定位器 | 往回或往前 0.5 秒（並暫停） |
| `Shift` + 方向鍵，`Page Down` / `Page Up` 於拖曳定位器 | 往回或往前 5 秒 |
| `Home` / `End` 於拖曳定位器 | 回到起點（Ready）或執行過程的結尾（Finished） |
| `Left` / `Right` 於速度控制項 | 上一個或下一個速度，並循環繞回 |

## 動態效果

- 播放頭、把手與每個作用中的時間段都會隨重播時鐘在每個影格移動；時間段的延伸不需要任何版面配置計算。計數器約每秒更新二十次。
- 作用中的思考或工具時間段有方形的前緣，以及一道 28px 的光澤，每 1.5 秒橫掃一次，耗時 1.05 秒；暫停時也會持續。時間段結束時，其右側圓角會變圓，前緣淡出；失敗的呼叫會在 160ms 內轉為失敗的外觀，叉號或重試箭頭則在 160ms 內出現。
- 連接線從交接的出發側開始繪製，耗時 360ms：派送從上方，結果從下方。其圓點在線條到達後彈出，耗時 160ms；派送立即彈出，結果則在 260ms 後彈出。
- 釘選會在 160ms 內淡化或還原；將滑鼠移到思考或工具時間段上，會在 160ms 內加深其顏色。
- 時間段卡片在 160ms 內淡入並上升 3px，以臨界阻尼彈簧（約 20 rad/s）在時間段之間滑動，並彈性調整到新的高度，內容則交叉淡入淡出（100ms 淡出，160ms 淡入）；它會在 80ms 的寬限期後以 120ms 淡出離開。
- 執行結束時，播放頭在 240ms 內淡出，狀態列交叉淡入淡出為摘要（160ms 淡入，100ms 淡出）；播放按鈕的圖示也會交叉淡入淡出。把手在聚焦或拖曳時於 160ms 內放大到 1.3 倍。

| 變化 | 完整 | 減少 | 關閉 |
|---|---|---|---|
| 開啟 | 進入可視範圍時重播範例 | 以已結束的狀態開啟 | 以已結束的狀態開啟 |
| 播放 | 依要求播放 | 依要求播放 | 依要求播放 |
| 作用中時間段的光澤 | 每 1.5 s 橫掃一次 | 無 | 無 |
| 時間段完成、淡化、懸停 | 160ms | 立即 | 立即 |
| 連接線繪製與圓點彈出 | 360ms 與 160ms | 立即出現 | 立即出現 |
| 卡片位置與高度 | 彈簧效果 | 直接跳動 | 直接跳動 |
| 卡片淡入與內容交叉淡入淡出 | 160ms / 120ms，100 / 160ms | 保留（短暫的交叉淡入淡出） | 立即 |
| 狀態列與播放頭 | 交叉淡入淡出，240ms 淡出 | 立即 | 立即 |

## 相關元件

- `AgentStatus`：以精簡膠囊顯示一個代理的即時狀態。
- `ToolCall`：顯示單一工具呼叫的輸入與輸出。
- `AgentPlan`：顯示代理遵循的計畫，逐步呈現。
- `ContextWindow`：顯示模型的上下文已使用多少。
- `AgentLoop`：以虛構的故事，說明單一代理如何在上下文、工具與檢查之間循環。

| 元件 | 適用時機 |
|---|---|
| `AgentStatus` | 你要顯示一個執行中代理目前正在做什麼 |
| `ToolCall` | 人們需要一次呼叫的輸入與輸出 |
| `AgentPlan` | 故事重點是計畫，而非時間安排 |
| `ContextWindow` | 問題是上下文使用了多少 |
| `AgentLoop` | 你要以虛構的故事說明代理循環 |
