---
title: Live visitors
summary: 即時訪客卡片：線上人數逐位滾動，新訪客在點陣世界地圖上閃現，熱門頁面帶著長條重新排序，註冊與升級依序出現。
source-hash: 57663a6874b8
---

<Demo name="Preview" />

## 何時使用

`LiveVisitors` 用一張卡片回答「此刻誰在這裡」：一個脈動的即時狀態、逐位滾動的站內人數、一張點陣世界地圖（每位新訪客在上面閃現後淡去）、
帶長條的最熱門頁面，以及一條註冊與升級動態，顯示城市、國家和多久之前。

- **網站或應用程式的即時訪客**，與剛剛發生的註冊和升級放在一起。
- **發表日和行銷活動**，團隊即時關注流量與轉換。
- **管理與成長儀表板**，需要一塊一眼看清「此刻」的卡片。

## 何時不用

- **幾天或幾週的趨勢**：使用帶座標軸的圖表。
- **精確的地理位置或依國家細分**：使用表格或真正的地圖；點陣地圖只是裝飾。
- **沒有脈絡的單一數字**：使用統計卡片。

## 用法

沒有資料也沒有模擬（見下文）時，卡片維持預留位置狀態，狀態顯示 No data。一旦設定了 `Count`，它就只顯示你傳入的資料。從你的即時資料來源（websocket、伺服器推送事件，或每兩秒輪詢一次分析 API）
設定新的快照即可，卡片會根據 id 判斷哪些發生了變化：

```csharp
card.Count = snapshot.Online;
card.Pages = snapshot.Pages.Select(p => new LiveVisitorsPage(p.Path, p.Visitors)).ToArray();
card.Visitors = snapshot.Arrivals          // the last 5 to 8 seconds of arrivals
    .Select(a => new LiveVisitorsPing(a.Id, a.Latitude, a.Longitude)).ToArray();
card.Events = snapshot.Conversions         // newest first
    .Select(c => new LiveVisitorsEvent(c.Id, c.Label, c.City, c.CountryCode, c.At)).ToArray();
card.Connection = socket.IsOpen ? LiveVisitorsConnection.Live : LiveVisitorsConnection.Reconnecting;
```

### 事件抵達時推送

<Demo name="YourData" />

資料串流逐筆傳送事件而不是快照時，直接推送。`PushVisitor` 新增一位訪客，它立即閃現並在六秒後淡出；`PushEvent` 在動態頂端新增一次轉換，
帶有經緯度時還會在地圖上以更明顯的光環閃現，並標出城市兩秒。推送的內容會與 `Visitors`、`Events` 中的資料合併顯示。

```csharp
socket.OnVisit += v => card.PushVisitor(v.Latitude, v.Longitude);
socket.OnConversion += c => card.PushEvent(new LiveVisitorsEvent(c.Id, "Signed up", c.City, c.Country, c.At)
{
    Latitude = c.Latitude,
    Longitude = c.Longitude,
});
```

### 模擬資料串流

用於原型或展示時，不要設定 `Count`，而是給卡片一個 `LiveVisitorsSimulation`，描述一個網站：約 `TypicalCount` 人分布在 `SitePages` 上，每 600ms 有一位來自 `Cities` 之一的新訪客，頁面人數會漂移，每 2.4s 發生一次 `Conversions` 之一，每項都依權重選取。卡片在畫面上、處於 Live 且未暫停時由自己的時鐘推進它，並會說明一次資料串流是模擬的。你自己的 `Count` 會讓它讓位，把 `Simulation` 設為 null 會清空卡片。預覽中的網站、城市、頁面與轉換都是虛構的展示資料。

```csharp
card.Simulation = new LiveVisitorsSimulation
{
    TypicalCount = 128,
    Cities = [new LiveVisitorsSimulationCity("Berlin", "DE", 52.52, 13.40, 4), new LiveVisitorsSimulationCity("Tokyo", "JP", 35.68, 139.69, 3)],
    SitePages = [new LiveVisitorsSimulationPage("/", 34), new LiveVisitorsSimulationPage("/pricing", 22)],
    Conversions = [new LiveVisitorsSimulationConversion("Signed up", 62), new LiveVisitorsSimulationConversion("Upgraded to Pro", 28)],
};
```

### 結構

1. **狀態**：一個圓點和一個詞：Live（圓點脈動）、Paused、Connecting 或 Reconnecting（警告色圓點）。
2. **暫停**：保留最後一份快照的切換按鈕。`IsPauseVisible="False"` 可隱藏它。
3. **人數**：等寬數字，每一位在自己的滾輪上滾動，後面是 `Labels.People` 提供的文字。
4. **地圖**：北緯 80° 到南緯 56° 的點陣世界地圖。訪客是強調色圓點加一圈光環；轉換的圓點更大，光環更明顯。
5. **熱門頁面**：最熱門的 `MaxPages` 個頁面（預設 4 個），長條依最熱門頁面縮放。
6. **註冊與升級**：最新的 `MaxEvents` 次轉換（預設 4 次），帶國家代碼標籤、發生了什麼、城市和時間。

### 連線狀態

`Connection` 反映你的連線狀態，而不是把卡片藏起來。`Connecting` 在人數和清單的位置顯示預留位置長條。`Reconnecting` 保留最後的資料，
狀態切換為警告色圓點和 Reconnecting 字樣。

### 暫停

暫停按鈕設定 `IsPaused`（雙向）並引發 `PausedChanged`；在其中暫停或關閉你自己的訂閱。暫停期間卡片保留最後一份快照，
不論你繼續傳入什麼，它的時鐘也會停止。恢復後顯示最新的資料。從程式碼設定 `IsPaused` 效果相同，但不會引發該事件。

### 窄容器

<Demo name="Narrow" />

卡片最寬 640px，在更窄的容器中填滿寬度。卡片寬度小於 520px 時兩份清單上下堆疊，內距降為 16px；地圖隨卡片縮放。
路徑和城市過長時以刪節號結尾，人數和時間保持固定的靠右對齊欄位。

### 你自己的文字

`Labels` 包含卡片顯示或朗讀的每個詞，包括人數的單複數、簡短與朗讀用的時間，以及國家名稱。替換整個物件即可在地化或改寫：

```csharp
card.Labels = new LiveVisitorsLabels
{
    People = n => n == 1 ? "person watching the stream" : "people watching the stream",
    EventsHeading = "New subscribers",
};
```

### 國旗

卡片以小標籤顯示國家的 ISO 兩位字母代碼，並為螢幕閱讀器唸出完整國名。可在 `LiveVisitorsEvent.Flag` 中傳入你自己的圖片來取代；它仍然只是裝飾。

### 內容指引

- 事件標籤簡短，使用過去式："Signed up"、"Upgraded to Pro"。
- 絕不在動態中放入個人資料，例如電子郵件地址；城市和國家就足夠了。
- 以五到八秒的滾動視窗傳送訪客；卡片根據 id 為抵達和離開播放動畫。

### 最佳實務

- 在快照之間保持 id 穩定，這樣列會就地更新，只有真正的變化才會移動。
- 用 `Connection` 表示連線狀態，而不是隱藏或清空卡片。
- 在 `PausedChanged` 中暫停你自己的訂閱，讓暫停的卡片不產生開銷。

### 常見問題

**模擬資料串流能在別處使用嗎？** 可以。給 `LiveVisitorsSimulation` 提供資料，用經過的時間呼叫 `Advance`，然後讀取 `Count`、`Pages`、
`Visitors` 和 `Events`；`Restart` 會讓它從頭開始。不會擷取或追蹤任何內容。

**為什麼數字在計數時寬度不變？** 數字是等寬的，每一位在自己的滾輪上滾動；只有新增的最高位（99 到 100）會展開出現。

## API 參考

<!-- api: LiveVisitors -->

<!-- api: LiveVisitorsPage -->

<!-- api: LiveVisitorsPing -->

<!-- api: LiveVisitorsEvent -->

<!-- api: LiveVisitorsLabels -->

<!-- api: LiveVisitorsSimulation -->

<!-- api: LiveVisitorsSimulationCity -->

<!-- api: LiveVisitorsSimulationPage -->

<!-- api: LiveVisitorsSimulationConversion -->

## 無障礙

- 卡片是一個帶名稱的群組，名稱來自 `Labels.Title`（"Live visitors"）。
- 人數以純文字唸出一次（"128 people on the site now"）；滾動的數字對螢幕閱讀器隱藏，人數變化也不會播報，以免資訊氾濫。
- 暫停按鈕是一個會回報是否按下的切換按鈕；它的名稱和提示說明操作（"Pause live updates"、"Resume live updates"）。
- 狀態從不只靠顏色表達：圓點旁邊總有 Live、Paused、Connecting 或 Reconnecting。
- 每個事件列唸作一句話，國家完整唸出，時間用文字表達（"Signed up, Berlin, Germany, 12 seconds ago"）；代碼標籤只是裝飾。
  每個頁面列唸作「/pricing, 24 people」。
- 地圖是裝飾且被隱藏；同樣的資訊在清單中。
- 一個禮貌的即時區域會回報暫停與恢復，並說明資料串流是模擬的。

### 鍵盤

| 按鍵 | 操作 |
|---|---|
| Tab | 移到暫停按鈕。地圖和清單為唯讀。 |
| Enter、Space | 暫停或恢復即時更新。 |

## 動效

| 變化 | 完整 | 減少 | 關閉 |
|---|---|---|---|
| 人數變化 | 每一位在自己的滾輪上滾動（280ms，ease-out）；新增的最高位展開出現 | 立即變化 | 立即變化 |
| 一位訪客 | 圓點淡入，一圈光環在 900ms 內擴散並淡去 | 圓點淡入 | 立即出現 |
| 帶位置的一次轉換 | 更大的圓點和更明顯的光環，城市標註 2 秒 | 圓點和標註淡入 | 立即出現；標註仍在 2 秒後離開 |
| 訪客過期 | 在 400ms 內淡出 | 在 400ms 內淡出 | 立即離開 |
| 列重新排序或新列到來 | 各列滑動一列高度（0.28 秒無回彈彈簧），新列在 160ms 內淡入，最後一列滑出裁切區並淡去 | 各列淡入淡出，不滑動 | 立即 |
| 頁面長條 | 在 280ms 內緩動到新寬度 | 在 280ms 內緩動 | 立即 |
| 即時圓點 | 即時且在螢幕上時每 1.6 秒發出一圈光環 | 靜止 | 靜止 |
| 狀態文字 | 在預留寬度內交叉淡化（淡出 100ms，淡入 160ms） | 短暫交叉淡化 | 立即 |

卡片的高度從不改變：清單保留各列的空間，地圖保持長寬比。卡片離開螢幕或暫停時，脈動、時鐘和模擬都會停止。

## 相關

- `AgentStatus`：執行中代理程式的即時狀態。
- `ContextWindow`：另一張即時、可自動播放的資料卡片。
- `AgentTimeline`：一次執行的回放，計數隨播放滾動。
