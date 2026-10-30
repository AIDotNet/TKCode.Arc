---
title: Source cards
summary: AI 回答下方的一排引用來源：模型搜尋時卡片流入預留的位置，超出的部分收攏為一疊網站標記，懸停時浮出帶原文片段的預覽，內文中的 [n] 引用會醒目標示對應的卡片。
source-hash: 5ae8e5408b8e
---

<Demo name="Preview" />

## 何時使用

`SourceCards` 呈現 AI 回答所依據的網頁。模型搜尋時，一個載入圈和閃爍的 "Searching 3 of 7" 位於預留的位置之上，每筆結果抵達時淡入自己的位置，因此這一排不會跳動。搜尋完成後，狀態列顯示 "7 sources · 2.1s"，回答在卡片上方串流出現，回答中的 `SourceCitation` 標籤會醒目標示它所引用的卡片。超過 `Max` 的來源收攏為一疊相互重疊的標記（"+4 more"）。懸停或聚焦一張卡片時，會浮出一個預覽，顯示回答所用的原文片段。

- **AI 搜尋或文件助理回答下方的引用。**
- **即時的搜尋進度**（"Searching 3 of 8"），在代理瀏覽網頁時顯示。
- **把生成文字中的行內 [n] 引用**連結到它們出自的網頁。

## 何時不用

- **完整的搜尋結果頁**，包含大量結果和篩選條件：請改用清單版面。
- **帶參數和輸出的工具呼叫**：使用 `ToolCall`。
- **內文中的單一連結預覽**：使用工具提示或懸停卡片。

## 用法

依引用順序把來源交給卡片，這樣第 1 個來源就以 [1] 引用。`Status` 為 `Searching` 且 `Total` 說明將有多少筆結果時，隨著結果抵達向一個可觀察清單附加；模型開始作答時切換為 `Done`。把回答放進 `Content`：它位於狀態列和卡片之間，串流輸出時高度以彈簧變化，卡片隨之平滑下移。

```csharp
var sources = new AvaloniaList<SourceCardsSource>();
cards.Sources = sources;
cards.Query = "webhook signature verification";
cards.Total = 8;
cards.Status = SourceCardsStatus.Searching;

// As each result arrives.
sources.Add(new SourceCardsSource(result.Title, result.Url) { Site = result.Site, Snippet = result.Passage });

// When the model starts answering.
cards.Duration = searchTime;
cards.Status = SourceCardsStatus.Done;
```

沒有 `Sources` 時，卡片顯示你設定的狀態；什麼都不設定時顯示 "No sources found"。用於沒有搜尋的展示或原型時，給卡片一個 `SourceCardsSimulation`，並傳入要播放的來源和回答（寫成 `[n]` 的詞會變成引用）。它在 `Sources` 為 null 且顯示在畫面上時播放，`Scenario` 可選 Live、Error 或 Empty，切換情境或按下重試時會從頭開始。模擬的卡片不會開啟任何連結；設定 `Sources` 後模擬即結束。

```csharp
cards.Simulation = new SourceCardsSimulation
{
    Sources = recordedResults, // SourceCardsSource[] in citation order
    Query = "webhook signature verification",
    Answer = "Read the raw body before you parse it [1].",
    Duration = TimeSpan.FromSeconds(1.4),
    Scenario = SourceCardsScenario.Live,
};
```

### 串流輸出帶引用的回答

<Demo name="Streaming" />

`SourceCitation` 是一個行內標籤。用 `InlineUIContainer` 把它放進回答；在 `SourceCards` 內部，它會為螢幕閱讀器說出自己的來源，在對應卡片醒目標示時變為強調色，按下它會把卡片醒目標示 2.6 秒（卡片在疊放中時會先展開）。

```xml
<arc:SourceCards Status="Done">
    <TextBlock TextWrapping="Wrap">
        <Run Text="Read the raw body before parsing it " />
        <InlineUIContainer BaselineAlignment="Center">
            <arc:SourceCitation Number="1" />
        </InlineUIContainer>
        <Run Text="." />
    </TextBlock>
</arc:SourceCards>
```

自行設定 `Highlighted` 可以保持醒目標示（例如讀者懸停在某句話上時），呼叫 `HighlightSource(n)` 則得到一次定時的醒目標示。`HighlightChanged` 回報引用被按下。

### 疊放與窄容器

<Demo name="Narrow" />

寬容器顯示 `Max` + 1 個等寬欄；窄於 540px 時網格變為兩欄，疊放是最後一格。疊放、Show all 開關以及指向疊放內部的引用都會把它展開（`IsExpanded`，雙向繫結），並引發 `ExpandedChanged`。

### 自己的閱讀器與失敗

<Demo name="Reader" />

按下卡片會引發 `SourceInvoked`，然後在瀏覽器中開啟網址。把事件標記為已處理，就能改在自己的閱讀器裡開啟來源；或者設定 `OpensLinks="False"`，讓卡片從不開啟連結。搜尋失敗時保留已經抵達的結果，移除預留的位置，並顯示 "Search failed" 和 `ErrorText`；`AllowsRetry` 會加上 Retry（找不到任何結果時是 Search again），兩者都引發 `RetryRequested`。

### 狀態列

| 狀態 | 圖示 | 顯示 |
|---|---|---|
| `Searching` | 載入圈 | "Searching 3 of 7" 和查詢詞 |
| `Done` | 地球 | "7 sources · 2.1s" |
| `Done`，沒有來源 | 帶叉的搜尋 | "No sources found" 和查詢詞 |
| `Error` | 警示 | "Search failed" 和 `ErrorText` |

當介面已經顯示搜尋進度時，設定 `IsStatusVisible="False"`；Show all 和 Retry 仍會保留。

### 網站標記

卡片依網域繪製一個帶字母的圓（webref.example 顯示 W）。請把網站圖示的形狀作為 24 × 24 的描邊幾何圖形傳給 `SourceCardsSource.Icon`，而不是自行編造標誌。

### 自訂文案

`Labels` 包含卡片及其引用顯示或朗讀的每一個詞。替換整個物件即可在地化或改寫：

```csharp
cards.Labels = new SourceCardsLabels
{
    Searching = "Recherche",
    Count = n => n == 1 ? "1 source" : $"{n} sources",
    Progress = (n, total) => total is { } t ? $"{n} sur {t}" : $"{n}",
};
```

### 最佳實務

- 讓來源保持引用順序，使 [n] 對應第 n 張卡片；附加，而不是重建清單。
- 搜尋時傳入 `Total`，這樣位置會被預留，什麼都不會跳動。
- 不要用自己的連結包住卡片：在應用程式內閱讀請處理 `SourceInvoked`。

## API 參考

<!-- api: SourceCards -->

<!-- api: SourceCitation -->

<!-- api: SourceCardsSource -->

<!-- api: SourceCardsSimulation -->

<!-- api: SourceCardsLabels -->

## 無障礙

- 卡片是一個名為 "Sources" 的清單，搜尋時回報自己處於忙碌狀態；預留的位置對輔助技術隱藏。
- 一個禮貌型即時區域對每種狀態只播報一次：正在搜尋某個查詢、找到的數量、沒有來源，或失敗。不會在每張卡片抵達時播報數量。模擬搜尋會說明它是模擬的，其卡片不會開啟任何內容。
- 每張卡片的名稱依序讀出編號、標題和網域，並說明它會在瀏覽器中開啟。預覽開啟期間，原文片段是這張卡片的說明文字。
- `SourceCitation` 是一個真正的按鈕，名稱如 "Source 2: Request: text() method, webref.example"；它的醒目標示從不只靠顏色，因為對應卡片還會出現外框。
- 狀態列總是圖示與文字並用。

### 鍵盤

| 按鍵 | 操作 |
|---|---|
| Tab | 依序經過 Retry 或 Show all、引用、每張卡片和疊放。取得焦點的卡片會顯示它的預覽 |
| Enter, Space | 開啟卡片（或引發 `SourceInvoked`）、展開疊放，或醒目標示引用對應的卡片 |
| Escape | 關閉開啟中的預覽 |

## 動態效果

| 變化 | 完整 | 減弱 | 關閉 |
|---|---|---|---|
| 卡片抵達 | 在 180ms 內淡入並上升 4px；它的佔位在 120ms 內淡出 | 只淡入，不上升 | 立即 |
| 預留的位置 | 條塊脈動 | 靜止 | 靜止 |
| 疊放收起，或失敗的搜尋移除佔位 | 離開的格子在原地用 120ms 淡出，同時疊放淡入它的格子；網格高度平滑過渡一次 | 淡入淡出，高度立即變化 | 立即 |
| 數量變化 | 在預留寬度內捲動 5px（等寬數字） | 立即 | 立即 |
| 狀態圖示與文字 | 交叉淡化，淡出 100ms、淡入 160ms | 120ms 交叉淡化 | 立即 |
| 回答串流輸出 | 高度以無回彈的彈簧變化，卡片平滑下移；寬度變化時直接跳到新高度 | 立即 | 立即 |
| 預覽開啟 | 懸停 320ms 後，在 160ms 內從 0.98 倍縮放淡入並上升 4px；之後在卡片之間立即切換 | 只淡入 | 立即 |

沒有任何回彈。模擬搜尋在捲出畫面時暫停。

## 相關元件

- `ToolCall`：代理發起的工具呼叫。
- `AgentPlan`：代理搜尋時遵循的計畫。
- `AgentStatus`：整個執行過程的即時狀態。
