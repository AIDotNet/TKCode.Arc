---
title: TL;DR toggle
summary: 一張文章卡片，可在原文位置串流呈現 AI 摘要或重點，附有長度滑桿，每則重點都能跳回其出處段落。
source-hash: 036af066c83d
---

<Demo name="Preview" />

## 何時使用

`TldrToggle` 是一張文章卡片，具有「全文」「摘要」「重點」切換和一個長度滑桿。切換後，AI 摘要會在原文的位置串流出現，卡片高度隨之以彈簧過渡到合適的大小；每則重點都連結回它所出自的段落，點選後該段落會捲動到視野中並醒目提示。

- **快速摘要或重點**：放在長篇文章、文件、工單或報告的上方，或直接取代原文。
- **讓讀者核對 AI 的結論**：重點連結回段落，讀者可以對照原文。
- **閱讀情境**：讀者透過長度控制項自行決定想看多少細節。

## 何時不用

- **針對內容的開放式提問**：改用問答或聊天元件。
- **很短的內容**：只有一兩段時，摘要省不了時間。
- **一次彙整多份文件**：改用清單或摘要彙編版面，而不是每項一張卡片。

## 用法

為卡片提供一個 `Article`，其段落帶有穩定的 id，再提供一個以串流傳回 `TldrStreamEvent` 的 `Summarizer`。內文用 `TldrStreamEvent.OfText` 傳送；重點則在每則重點的文字之前先傳送 `TldrStreamEvent.OfPoint(paragraphId)`，這樣它的跳轉連結才會落在正確的段落上。沒有摘要器時，卡片會以文章的首句模擬摘要（`TldrStreams.Simulate`），不會傳送任何內容。既沒有 `Article` 也沒有 `Simulation` 時，卡片顯示其安靜的空狀態：沒有頁首，顯示「This article has no text to summarize yet.」，切換器和滑桿處於關閉狀態。

```csharp
card.Article = new TldrArticle
{
    Title = post.Title,
    SourceName = "Engineering blog",
    AuthorName = post.Author,
    Date = "Oct 21, 2026",
    Paragraphs = post.Paragraphs.Select(p => new TldrParagraph(p.Id, p.Text)).ToList(),
};
card.Model = new AgentModel("GPT-6 Luna", ArcIcons.Sparkles);
card.Summarizer = request => myService.StreamTldr(request); // IAsyncEnumerable<TldrStreamEvent>
card.Jumping += (_, e) => analytics.Track("tldr_jump", e.ParagraphId);
```

請求中包含 `Mode`（`Summary` 或 `KeyPoints`）、`Length`（1 到 5）、`TargetWords`（35 到 130）、`PointCount`（3 到 7）、文章本身，以及一個 `CancellationToken`：在停止、按 Escape、切換模式或長度、換了文章，或卡片離開視窗時取消。把長度以 `TargetWords` 或 `PointCount` 傳給模型，而不是「簡短」這類含糊的字眼。

### 模擬

想在沒有摘要器的情況下展示卡片（示範、設計審查、新手導覽），就給它一個 `TldrToggleSimulation`：文章、要標示的模型、為每個長度寫好的摘要，以及依重要性排序的重點。卡片會在短暫的思考停頓之後串流輸出寫好的文字，說明這是模擬、沒有傳送任何內容，並在卡片有一半位於可見區域一秒後自動開啟摘要（`AutoPlay="False"` 會讓它停留在文章上）。沒有寫好摘要的長度會使用文章的首句。你自己的 `Article` 優先於模擬；把 `Simulation` 設為 null 會停止模擬並顯示空狀態。

```csharp
card.Simulation = new TldrToggleSimulation
{
    Article = post,
    Model = new AgentModel("GPT-6 Luna", ArcIcons.Sparkles),
    Summaries = [shortest, shorter, medium, longer, longest],
    KeyPoints = [new TldrKeyPoint("results", "Median cold start fell from 4.2 seconds to 0.9.")],
};
```

上方的預覽播放的就是一個模擬，其文章、摘要和重點來自圖庫自己的示範資料。

### 結構

1. **頁首**：來源的標誌（或放在著色方塊中的首字母）、名稱、日期和閱讀時間；標題；附大頭貼的作者。
2. **控制項**：模式切換（`SegmentedControl`）與長度滑桿（`Slider`），滑桿顯示「~60 words」或「4 points」，在全文模式下停用。
3. **內文**：文章、串流摘要或重點清單，高度以彈簧跟隨內容。
4. **頁尾**：以圖示加文字表示的狀態、附 14px 標誌的模型，以及停止、重新產生和複製按鈕。

### 重點

<Demo name="KeyPoints" />

重點以清單形式出現：每則重點寫完時，圖示從虛線圓圈變為勾號。整份清單完成後，每則重點的 ¶ 連結會切回文章，等高度穩定後把段落捲動到視野中，把焦點移過去並醒目提示它。在此之前會先觸發 `Jumping`，參數是段落的 id。在程式碼中呼叫 `JumpTo(id)` 也能做到同樣的事。

### 結果、停止與重新產生

完成的結果依模式和長度保留，所以切回去是即時的，不會再發出請求。`Regenerate`（頁尾的按鈕）會重新請求。停止或按 Escape 會結束串流，並保留已收到的內容。`Completed` 會回報每個完成的結果，附帶文字、重點和字數，可用於分析或伺服器端快取；被停止或失敗的執行不會觸發它。`ModeChanged` 與 `LengthChanged` 回報讀者做出的變更；滑桿在拖曳放開或按鍵調整時才回報長度，所以每次變更只發出一個請求。

### 自訂摘要器

<Demo name="YourSummarizer" />

服務能以串流傳回時就用串流。如果它一次傳回完整結果，用 `TldrStreams.FromResult(text, token)` 或 `TldrStreams.FromResult(points, token)` 包裝，卡片仍會以每塊 3 到 8 個字詞的節奏串流呈現。

### 失敗

<Demo name="Failure" />

摘要器擲出的例外會以警示框顯示其訊息，並附上「Try again」按鈕。`TldrStreams.Simulate(request, fail: true)` 和 `TldrToggleSimulation.Summarize(request, fail: true)` 會在思考停頓之後擲出例外。自行包裝模擬時，請設定 `IsSimulated="True"`，讓播報和錯誤文字都註明「Simulated, nothing was sent」。

### 窄容器

<Demo name="Narrow" />

卡片會填滿容器，最寬 680px。寬度小於 520px 時，長度滑桿移到模式切換下方並占滿整列；小於 440px 時，內距從 24px 縮小到 16px。頁尾文字保持一行，模型名稱最先被截斷。

### 自動播放

沒有摘要器時，卡片在至少一半進入視野滿一秒後會自動開啟摘要，除非讀者已經在卡片裡按過滑鼠或鍵盤。設定 `AutoPlay` 可以明確開啟或關閉。

### 自訂文字

`Labels` 包含卡片顯示或播報的所有文字。取代整個物件即可在地化或改寫：

```csharp
card.Labels = new TldrToggleLabels
{
    Full = "Article",
    KeyPoints = "Highlights",
    Words = words => $"{words} words",
};
```

### 最佳做法

- 為段落提供穩定的 id，並讓模型傳回每則重點出自哪個 id。
- 以 `TargetWords` 或 `PointCount` 傳遞長度。
- 沒有摘要器時卡片執行的是模擬，並會在播報中說明；切勿把它發布到正式環境。

## API 參考

<!-- api: TldrToggle -->

<!-- api: TldrArticle -->

<!-- api: TldrParagraph -->

<!-- api: TldrKeyPoint -->

<!-- api: TldrRequest -->

<!-- api: TldrStreamEvent -->

<!-- api: TldrStreams -->

<!-- api: TldrToggleSimulation -->

<!-- api: TldrToggleLabels -->

<!-- api: TldrModeChangedEventArgs -->

<!-- api: TldrLengthChangedEventArgs -->

<!-- api: TldrCompletedEventArgs -->

<!-- api: TldrJumpEventArgs -->

<!-- api: AgentModel -->

## 無障礙

- 卡片以文章標題命名，標題是第二層標題。
- 模式切換是有標籤的群組（「Reading mode」）；長度滑桿朗讀的值是目標（「~60 words」、「4 points」），在全文模式下停用。
- 一個禮貌型即時區域對每次執行只播報一次（「Summarizing with GPT-6 Luna」、「Summary ready, 58 words」、「4 key points ready」、「Stopped」），從不朗讀串流片段。使用模擬時會加上「Simulated, nothing was sent」。
- 失敗以警示框呈現，附上訊息和「Try again」按鈕。
- 跳轉連結以其重點命名（「Jump to paragraph 3: Partial bootstrapping loads...」），跳轉會把焦點移到醒目提示的段落，讓鍵盤和螢幕閱讀器使用者落在視線所在的位置。
- 狀態從不只靠顏色表達：完成、停止和失敗都配有圖示和文字，重點從虛線圓圈變為勾號。

### 鍵盤

| 按鍵 | 操作 |
|---|---|
| Tab | 依序經過模式切換、長度滑桿、跳轉連結和頁尾按鈕 |
| 方向鍵 | 在模式切換上，於全文、摘要和重點之間移動；在滑桿上，把長度改變一格（每格一個請求） |
| Home、End | 第一個或最後一個模式，或最短、最長的長度 |
| Enter、Space | 在跳轉連結上，切回文章，捲動到對應段落，聚焦並醒目提示它 |
| Escape | 停止進行中的串流，保留已收到的內容 |

## 動態效果

| 變化 | 完整 | 減少 | 關閉 |
|---|---|---|---|
| 內文高度（切換檢視或流入文字） | 一個彈簧，0.26s，無回彈 | 立即 | 立即 |
| 容器尺寸變化 | 直接跳到新高度 | 立即 | 立即 |
| 檢視切換 | 舊檢視 100ms 淡出，新檢視 160ms 淡入 | 120ms 交叉淡化 | 立即 |
| 頁尾狀態與按鈕 | 同樣的交叉淡化 | 120ms 交叉淡化 | 立即 |
| 串流片段（3 到 8 個字詞，每 30 到 40ms 一塊） | 140ms 淡入 | 直接出現 | 直接出現 |
| 已保留的結果 | 整體出現，不淡入 | 直接出現 | 直接出現 |
| 思考（約 0.7s） | 內文那一行平靜地閃爍；隨後頁尾的 Writing 閃爍 | 靜止 | 靜止 |
| 重點打勾 | 虛線圓圈淡出並縮小，勾號以同一彈簧放大出現 | 立即 | 立即 |
| 跳轉 | 等待高度穩定，平滑捲動，醒目提示 160ms 淡入、600ms 淡出 | 直接跳到，醒目提示淡入淡出 | 直接跳到，醒目提示直接顯示 |

同一時間只有一處閃爍。

## 相關元件

- `ReasoningBlock`：AI 回覆中的思考過程。
- `SegmentedControl` 與 `Slider`：卡片用它們來切換模式和調整長度。
- `CopyButton`：複製一個值並立即確認。
- `ToolCall`：智慧代理紀錄中的工具呼叫。
