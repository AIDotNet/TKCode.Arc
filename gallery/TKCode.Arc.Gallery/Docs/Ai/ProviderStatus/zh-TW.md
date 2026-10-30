---
title: Provider status
summary: 一張呈現產品所依賴的 AI 服務供應商狀態的卡片：以圖示和標籤表示健康狀態，顯示中位延遲與每天都附提示的 30 天可用率條，事件可以展開查看更新，下一次檢查有可見的倒數計時。
source-hash: 59e821a9305c
---

<Demo name="Preview" />

## 何時使用

`ProviderStatus` 是一張卡片，列出產品所路由到的 AI 服務供應商。每一列顯示供應商的標誌、以圖示和標籤表示的健康狀態、中位（p50）延遲，以及由一個個小刻度組成的 30 天可用率條，每一天都有自己的提示。帶有事件的列是一個按鈕，點擊後依從新到舊的順序展開事件的更新。標題列彙總所有供應商的健康狀態，並依可見的倒數計時環再次檢查。

- **產品所依賴的模型供應商的即時健康狀態**，放在設定頁或管理儀表板上。
- **向使用者或管理員說明 AI 功能為何降級**，並就地呈現供應商的事件更新。
- **路由與備援設定**，需要讓人看清此刻哪些供應商是健康的。

## 何時不用

- **完整的公開狀態頁**，包含元件、訂閱與歷史記錄：應該做成一個頁面，而不是卡片。
- **聊天標題列中單一供應商的狀態**：使用 `AgentStatus` 或一個小徽章。
- **隨時間變化的詳細延遲**：使用圖表。

## 用法

傳入你的供應商，以及一個從你自己的彙整端點取得最新狀態的 `RefreshHandler`。不要在用戶端輪詢供應商的狀態頁。處理常式回傳新的供應商清單，或自行更新 `Providers` 並回傳 `null`。它會在倒數結束時和按下重新整理按鈕時執行；新的檢查會透過取消權杖取消較早的檢查。

```csharp
card.Providers = await statusApi.GetProvidersAsync();
card.RefreshHandler = async token =>
{
    var response = await http.GetAsync("/api/provider-status", token);
    if (!response.IsSuccessStatusCode)
    {
        throw new InvalidOperationException("Couldn’t reach the status feeds. Showing the last results.");
    }
    return await response.Content.ReadFromJsonAsync<List<ProviderStatusItem>>(token);
};
card.IncidentOpened += (_, e) => OpenBrowser(e.Incident.Url);
card.Footnote = "Latency is the median over the last 5 minutes.";
```

既沒有供應商、處理常式，也沒有模擬時，卡片顯示空白狀態：標題列顯示 "No providers"，重新整理控制項會隱藏。

### 模擬檢查

用於展示或原型時，不設定 `Providers` 和 `RefreshHandler`，而是給卡片一個帶有若干 `ProviderStatusSimulationStep` 的 `ProviderStatusSimulation`。卡片立即顯示第一步，並每隔 `Interval`（8 秒）進行一次模擬檢查：每次檢查耗時 `CheckDelay`（900ms），然後進入下一步並循環，因此事件會開啟、發布更新並解決；帶有 `Fails` 的步驟會顯示錯誤狀態並保留上一次的結果。其 Status page 連結會說明該操作是模擬的；沒有 `Footnote` 時，卡片會註明資料是範例。

```csharp
card.Simulation = new ProviderStatusSimulation
{
    Steps =
    [
        new ProviderStatusSimulationStep { Providers = beforeTheIncident },
        new ProviderStatusSimulationStep { Providers = incidentOpened },
        new ProviderStatusSimulationStep { Fails = true },
        new ProviderStatusSimulationStep { Providers = resolved },
    ],
};
card.Footnote = "Sample data: the incidents and numbers here are invented.";
```

預覽播放的就是這樣一段故事，使用真實的供應商名稱及其狀態頁列出的元件；其中每個事件、延遲與可用率數字都是虛構的，絕不是供應商的真實歷史，註腳也會說明這一點。

### 你的供應商

<Demo name="OwnData" />

每個 `ProviderStatusItem` 都有一個 `Id`（只要它還在，該列就會被重複使用）、一個 `Name`、一行 `Detail`（例如你呼叫的模型）、它的 `Health`、以 `LatencyMs` 表示的中位延遲，以及它的 `Days`。卡片會自行格式化數字（412 ms、1.92 s、99.61%），因此請以一般數字傳入。

`Mark` 是一個 24 × 24 的描邊圖形，以 16px 和文字顏色繪製；傳入供應商標誌的路徑即可，`MarkBrush` 用來為它上色。沒有標誌時，該列會在一個上色的方塊中顯示名稱的第一個字母。

### 可用率條

`Days` 是逐日的歷史記錄，最早的在前。可用率條顯示截至最後一天的 30 天，最後一天標為 Today，缺少的日期補為 No data。每一天有一個 `Date`、一個 0 到 100 的 `Uptime`（或 null）、一個選用的 `Health`（省略時由可用率推導：低於 99.9 為降級，低於 99 為中斷），以及一則用於提示的選用 `Note`。可用率條旁的百分比是所有有資料日子的平均值。

整張卡片共用一個提示：指向某個刻度、輕觸它，或在可用率條上橫向拖曳，提示就會逐日移動。

### 事件

在供應商上設定 `Incident`，它所在的列就會變成一個按鈕，用來展開事件的 `Updates`。每則更新有一個 `Stage`（Investigating、Identified、Monitoring、Resolved）、一個以 `At` 表示的時間（顯示為 HH:MM UTC）或預先格式化的 `AtText`，以及一則 `Message`。更新依從新到舊顯示；新的更新會在最上方淡入，同時面板隨之變高。

`ExpandedId` 指出目前展開的供應商，是雙向的；`ExpandedChanged` 回報使用者展開和收合列。更新下方的 Status page 連結會觸發 `IncidentOpened`。沒有任何處理常式處理它時，連結會開啟事件的 `Url`；兩者都沒有時，連結會隱藏。

### 檢查與錯誤

`RefreshInterval` 設定兩次自動檢查之間的時間（預設 30 秒）。倒數計時環朝下一次檢查逐漸消退，卡片不在畫面上或被隱藏時會暫停。檢查進行期間，一個短暫的載入圈和一個閃爍的 "Checking" 會取而代之。處理常式擲回例外時，標題列顯示例外的訊息，上一次的結果留在畫面上，倒數變為重試，最多等待 10 秒。`RefreshInterval="{x:Null}"` 會關閉自動檢查，但保留重新整理按鈕。沒有處理常式時，重新整理控制項會隱藏。`CheckNow()` 會立即檢查。

### 狀態

<Demo name="States" />

`IsLoading` 在第一次載入時顯示列高相同的骨架列。`ErrorText` 在標題列中顯示你自己的錯誤，例如第一次載入失敗。

### 窄卡片

<Demo name="Narrow" />

卡片寬度不小於 560px 時，每一列佔一行，分欄顯示狀態、p50 延遲、可用率條與 30 天百分比。更窄的卡片每列佔兩行：上面是名稱和健康狀態，延遲位於名稱下方；下面是佔滿寬度的可用率條及其百分比。這條規則跟隨卡片，而不是視窗，在 360px 時依然可用；過長的名稱和說明以刪節號結尾。

### 自訂文字

`Labels` 包含卡片顯示或朗讀的每一個字詞；數字、日期以及帶有數值的句子是函式：

```csharp
card.Labels = new ProviderStatusLabels
{
    Operational = "Opérationnel",
    Degraded = "Dégradé",
    Outage = "Panne",
    NextCheck = seconds => $"Prochaine vérification dans {seconds} s",
};
```

### 最佳做法

- 把 `RefreshHandler` 接到你自己的、彙整各供應商狀態來源的端點。
- 用 `Footnote` 標示範例資料或合成資料。
- `Detail` 保持簡短，例如你呼叫的模型；空間不足時它會以刪節號結尾。
- 事件解決後，在供應商上再保留一段時間，讓人能讀到發生了什麼事。

## API 參考

<!-- api: ProviderStatus -->

<!-- api: ProviderStatusItem -->

<!-- api: ProviderStatusDay -->

<!-- api: ProviderStatusIncident -->

<!-- api: ProviderStatusUpdate -->

<!-- api: ProviderStatusLabels -->

<!-- api: ProviderStatusSimulation -->

<!-- api: ProviderStatusSimulationStep -->

## 無障礙

- 卡片是一個以標題命名的群組。各列組成一份清單；每一列以供應商及其健康狀態命名。
- 事件列有一個涵蓋整列的按鈕，會回報展開或收合狀態，並以供應商、健康狀態和事件標題命名。
- 健康狀態從不只靠顏色表達：每種狀態都有圖示和標籤，每一天的健康狀態也寫在它的提示和可用率條的名稱中。
- 每個可用率條是一個 Tab 停駐點，以其 30 天的摘要命名。方向鍵在各天之間移動，一個禮貌的即時區域逐一朗讀，因此 180 個刻度不會塞滿 Tab 順序。
- 同一個即時區域也會播報檢查、失敗的檢查、`Notice` 以及模擬操作（"Simulated, no requests were sent"）。
- 鍵盤焦點會在列按鈕內側和可用率條周圍顯示焦點環；指標焦點則不顯示。

### 鍵盤

| 按鍵 | 操作 |
|---|---|
| Tab | 依序移到重新整理按鈕、每個事件列和每個可用率條 |
| Enter、Space | 在事件列上展開或收合其更新 |
| 左方向鍵、右方向鍵 | 在可用率條上把日期提示往前或往後移動一天 |
| Home、End | 在可用率條上跳到最早的一天或今天 |
| Escape | 在可用率條上隱藏日期提示 |

## 動態效果

| 變化 | 完整 | 減少 | 關閉 |
|---|---|---|---|
| 健康狀態、延遲、可用率與摘要變化 | 在預留的儲存格中做一次交叉淡化：舊內容 100ms 淡出，新內容 160ms 淡入並上升 2px | 交叉淡化，不上升 | 立即 |
| 事件展開或收合 | 高度以彈簧過渡到量得的內容高度（0.24s，無回彈），內容淡入淡出 | 立即 | 立即 |
| 新的更新抵達 | 在最上方淡入，同時面板以彈簧過渡到新高度 | 立即 | 立即 |
| 刻度的健康狀態變化 | 顏色在 240ms 內過渡 | 顏色在 240ms 內過渡 | 立即 |
| 倒數計時 | 環線性消退；秒數每秒更新一次 | 相同 | 相同 |
| 檢查中 | 短暫的載入圈和閃爍的標籤 | 靜止的標籤，沒有載入圈 | 靜止的標籤，沒有載入圈 |
| 骨架列 | 脈動 | 靜止 | 靜止 |
| 箭頭 | 以彈簧轉動 | 立即 | 立即 |

卡片不在畫面上或所在視窗被隱藏時，倒數計時會暫停。各列的欄寬固定，因此數值變化時不會重新排版。

## 相關元件

- `AgentStatus`：單次代理程式執行的即時狀態。
- `ToolCall`：代理程式對話記錄中的一次工具呼叫。
- `Tooltip`：簡短的輔助說明文字。
