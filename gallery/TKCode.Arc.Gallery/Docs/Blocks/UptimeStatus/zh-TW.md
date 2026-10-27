---
title: Uptime status
summary: 一組服務的狀態頁：90 天的每日柱及可用性數字，每天一張懸停卡片；所選服務相對於目標的回應時間圖表，帶 p50 和 p95 曲線；以及事故時間軸。全部由服務的取樣和事故推導而來，並支援用鍵盤按一週翻頁。
source-hash: ff329a7a9498
---

<Demo name="Preview" />

## 何時使用

`UptimeStatus` 是把公開或內部狀態頁集於一個區塊。它按人們提問的順序回答三個問題：現在一切正常嗎（頁首），每個服務有多可靠（90 天裡每天一根柱，外加可用性數字），以及發生了什麼、在何時發生（事故時間軸）。選擇一個服務，就會繪製它最近一天的回應時間，這樣柱中某個緩慢的週二就可以與延遲曲線和一次事故對應起來。

- **面向客戶或團隊的狀態頁。** 事故未解決期間，頁首從勾號變為驚嘆號並說明出了什麼問題；每一列都會說明它是哪個服務。
- **服務等級檢討。** 柱和可用性數字由你給出的事故計算而來，因此頁面不會與事故清單不一致。
- **作業手冊的搭檔。** 延遲圖表把 p95 與目標放在一起，於是「慢」就是一個數字，旁邊還有一條線。

## 何時不用

- **即時監控。** 區塊顯示每日歷史和每半小時一次的取樣；它不會輪詢任何東西。請從你的監控系統向它提供資料，並讓它跟隨你變更的資料。
- **隨時間變化的單一指標。** 當沒有可與之關聯的服務、事故或目標時，請使用折線圖或 `Metric explorer` 區塊。
- **數百個服務。** 頁面是為大約 3 到 10 列而設計；其餘的請先分組再顯示。
- **分鐘級的細節。** 一根柱就是完整的一個 UTC 日。事故的精確時間請在其時間軸中顯示。

## 用法

區塊衍生自 Avalonia 的 `TemplatedControl`，本身不含資料。為它提供 `Services` 和 `Incidents`：

```xml
<arc:UptimeStatus PageName="Larkspur status" UpdatedAt="2027-03-14T14:32:00Z">
    <arc:UptimeService Key="edge" Name="Edge" Description="Routing and TLS" TargetP95="350" />
    <arc:UptimeService Key="console" Name="Console" Description="console.larkspur.test" TargetP95="900" />
</arc:UptimeStatus>
```

```csharp
status.Services.AddRange(services);                        // each with 48 half-hourly LatencySamples
status.Incidents.Add(new UptimeIncident
{
    Title = "Elevated 5xx on Edge",
    Severity = ServiceStatus.Outage,
    Start = DateTimeOffset.Parse("2027-02-20T07:40:00Z"),
    Duration = TimeSpan.FromMinutes(19),
    ServiceKeys = { "edge" },
    Updates = { new IncidentUpdate("Investigating", start, "Some requests fail.", IncidentTone.Warning) }
});
```

`ServicesSource` 和 `IncidentsSource` 接受由你自己持有的集合（會跟隨 `ObservableCollection` 的變化）。區塊還會跟隨服務取樣的變化以及事故每個屬性的變化，並在 UI 執行緒的下一輪把一連串的變化一起顯示出來。

### 推導的內容

頁面上的一切都由資料推導而來，因此沒有需要手動保持同步的東西：

- **某一天的可用性**是 1 減去落在該 UTC 日內的事故分鐘數除以 1,440；跨越午夜的事故會在兩天之間拆分。懸停卡片以兩位小數寫出（"98.40%"）。
- **某一天的狀態**取當天最嚴重的事故：沒有事故為正常，降級為 65% 的柱，中斷為 35% 的柱。降級和中斷的分鐘數都計為停機時間。
- **某個服務的可用性**是 1 減去它的全部分鐘數除以 `DayCount` 乘以 1,440（90 天內 69 分鐘為 "99.95%"）。
- **某個服務的狀態**（其列上的徽章、標題）來自它未解決的事故。已解決的事故會讓當天的柱保持降級，因為當天發生過事故，但頁面已恢復正常。
- "Incidents" 下方的**計數**是視窗內的事故數，清單按最新在前排列。

`Today` 是視窗的最後一天（預設：最新延遲取樣所在的日子），`DayCount` 是柱的數量。

### 柱

每個柱帶與列等寬。柱寬為 `round((W - 2 (n - 1)) / n)`，不小於 3，第 *i* 根柱從 `round(i (W - w) / (n - 1))` 開始：1,112px 的帶得到 10px 的柱，間隙為 2 或 3px，472px 的帶得到 3px 的柱。指向該帶會貼齊到最近的柱（卡片跟隨柱，而不是指標），將該服務其他柱調暗到 38%，並在帶的上方開啟懸停卡片：服務和日期及當天的可用性、狀態，以及當天的每次事故及其分鐘數，或 "No incidents recorded"。卡片寬 256，位於帶上方 8 處，與區段邊緣保持 8 的距離，可能會蓋住上面一列。

### 回應時間圖表

透過名稱、點擊其柱，或在其柱上按 Enter 或 Space 來選擇服務；`SelectedService` 是雙向的。圖表顯示最近 48 個半小時取樣：p95 與 p50 之間的色帶，p95 曲線（2px，強調色），p50 曲線（1.5px），以及 `TargetP95` 處的一條虛線。兩條曲線都是單調三次曲線，因此永遠不會超出資料。y 軸從 0 開始，到 `ChartHeadroom`（1.2）乘以目標與最高 p95 中較大者之上的整數為止：目標 400、最高 p95 為 266 時得到 0 到 500，步長 100；目標 1,000、最高 612 時得到 0 到 1,250，步長 250。圖表上方的三個數字（p95、p50、目標）會滾動到新服務的值。

指向圖表（或在取得焦點的圖表上按 → 或 ←）會把帶有每條曲線一個圓點的十字準線貼齊到最近的取樣，並用該取樣的數字和右上角的時間取代原有內容，不帶滾動；離開後恢復為最新的取樣。

<Demo name="Events" />

### 事故

每個事故是一個按鈕，帶有嚴重程度標記、標題、中繼資料列（"Major outage · Feb 20, 07:40 UTC · 19 min · Edge"）、狀態（持續期間為徽章，之後為 "Resolved"）和一個箭頭。展開它會顯示更新，最新在前，最新的圓點以其 `IncidentUpdate.Tone` 填滿，較早的為空心，還有關於負責回應的人員的那一行。`IsExpanded` 是雙向的，可以同時展開多個事故。

### 變化的狀態

新增一個 `IsResolved` 為 false 的事故，區塊就會改變該事故涉及的一切：頁首圖示變為驚嘆號，標題換成 `DegradedTitle`（或 `OutageTitle`），受影響服務的列出現徽章，今天的柱淡化為降級並縮短，它的可用性滾動，計數滾動，新事故在清單頂部生長出來。設定 `IsResolved`、`Duration` 並新增一則 "Resolved" 更新，頁面就恢復正常，徽章消失，今天的柱保持降級，時間軸中最新的項目生長出來。移除該事故則全部還原。`StatusSummary` 是出問題時頁面名稱旁的那一行（"Events delayed since 13:58 UTC"）；沒有它時，這一行根據 `UpdatedAt` 顯示為 "Updated …"。

Preview 示範正是用一個按鈕驅動這一切；模擬邏輯位於示範的程式碼中，而不在區塊裡。

### 頁首操作

`ActionContent` 是你想放在頁首右側的任何內容（按鈕、連結）。區塊寬度小於 560 時，它移到文字下方。

### 其他範例

<Demo name="AllGreen" />

<Demo name="Outage" />

### 窄容器

區塊讀取自己的寬度，而不是視窗的寬度。小於 860 時下面兩個區段堆疊；小於 640 時圖例移到提示下方；小於 560 時內距收緊到 16 和 14，頁首操作移到文字下方，列的描述和 "uptime" 一詞被隱藏。柱保持最小 3px，所以在手機上間隙會縮小。

<Demo name="Compact" />

### 文字和格式

每個詞都在 `Labels`（一個 `UptimeStatusLabels`）中，三個標題是屬性，`LatencyFormatter`、`PercentFormatter` 和 `DateFormatter` 會改變懸停卡片、圖表刻度標籤和播報中數字與日期的寫法。

<Demo name="Formats" />

## API 參考

<!-- api: UptimeStatus -->

<!-- api: UptimeService -->

<!-- api: LatencySample -->

<!-- api: UptimeIncident -->

<!-- api: IncidentUpdate -->

<!-- api: UptimePerson -->

<!-- api: UptimeDay -->

<!-- api: UptimeDayIncident -->

<!-- api: UptimeStatusLabels -->

<!-- api: UptimeServiceEventArgs -->

<!-- api: UptimeDayEventArgs -->

<!-- api: UptimePointEventArgs -->

<!-- api: UptimeIncidentEventArgs -->

<!-- api: ServiceStatus -->

<!-- api: IncidentTone -->

## 無障礙

- 每個服務有一個以其可用性和狀態命名的按鈕（"Edge, 99.95% uptime, Operational"），在該服務被繪製時處於按下狀態；還有一個可取得焦點的柱群組，名稱為 "Edge, daily status for the last 90 days"，並以視窗內的可用性和事故作為說明。
- 用鍵盤檢視某一天會以禮貌的方式播報："Sat, Feb 20: Major outage, 98.68% uptime. Elevated 5xx on Edge."。檢視某個取樣會播報 "Mar 14, 14:00 UTC: p50 80 ms, p95 210 ms."。懸停卡片本身對輔助技術隱藏；播報承載了它的內容。
- 圖表是一個可取得焦點的群組，帶有文字摘要（最新的 p50 和 p95、最高的 p95 及其時間、目標），以及每半小時一列的隱藏表格。
- 事故按鈕會說明是否已展開；每條時間軸是一個名為 "Updates, newest first" 的清單，會讀出每則更新。
- 一個單獨的狀態區域會播報出現的事故和已解決的事故，`Announce` 讓你說出自己的句子。
- 顏色從不是唯一的訊號：柱除了顏色之外高度也不同，徽章和頁首圖示帶有文字和形狀。
- 柱、圖表、服務按鈕和事故按鈕上的焦點環為 2px，位於外側 2px，僅用於鍵盤焦點。

### 鍵盤

- **Tab** 依序經過頁首操作，然後是每個服務的名稱按鈕及其柱，再到圖表，然後是每個事故。
- 在**柱**上，透過鍵盤取得焦點時會檢視最新的一天。**← →** 移動一天，**PageUp / PageDown** 移動七天，**Home / End** 到兩端並在那裡停止。**Escape** 清除（下一次 **←** 從最新一天的前一天開始），**Enter / Space** 繪製該服務。
- 在**圖表**上，取得焦點不會檢視。**→** 顯示最新的取樣，**←** 顯示它之前的一個；之後 **← →** 移動半小時，**Home / End** 跳到兩端。翻頁鍵沒有作用。**Escape** 清除。
- 在**事故**上，**Enter / Space** 展開或收合。

## 動態效果

- **柱**：指向時其他柱在 120ms（Standard）內調暗到 38%，而被檢視的那根的全強度副本保持在最上層。狀態變化的一天，顏色在 240ms 內淡入，高度彈動（k 170，c 27）。
- **懸停卡片**：從 97% 縮放在 160ms 內淡入；在柱之間移動時，它以較硬的彈簧（k 925，c 58，約 240ms）跟隨，列與列之間也是如此；離開時，它在 120ms 內淡出到 98%。
- **選擇服務**：醒目提示滑向該列（k 150，c 24，約半秒），同時曲線、色帶、目標線和 y 域一起彈動（k 171，c 26）；刻度標籤交叉淡入淡出，副標題升出再升入，三個數字滾動。保留的刻度隨域滑動。
- **頁首**：圖示的線端在勾號和驚嘆號之間彈動（k 160，c 26），同時顏色在 240ms 內淡變；標題和摘要以短暫的上升（約 200ms）互換，徽章的顏色在 240ms 內淡變。
- **事故**：新事故從零高度在彈簧上生長，同時在 200ms 內淡入；被移除的事故在 130ms 內淡出並摺疊。時間軸項目在彈簧上生長進入（k 180，c 27）。展開事故時高度在彈簧上變化（k 171），內容在 240ms 內淡入；箭頭在快速彈簧上轉半圈。
- 減少動態效果保留短暫的淡入淡出（懸停卡片、調暗、離開的刻度標籤），並讓彈簧、滾動數字和變形立即就位。關閉則立即套用所有變化。

## 相關

- `Metrics dashboard`、`KPI drilldown` 和 `Metric explorer`，即其他 Analytics 區塊。
- `Badge` 和 `Avatar`，狀態徽章和回應負責人所借鑒的小元件。
- `Release readiness`，一個頁首狀態同樣會變化的 Projects 區塊。
