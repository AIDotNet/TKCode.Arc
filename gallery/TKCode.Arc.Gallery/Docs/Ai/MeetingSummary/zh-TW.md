---
title: Meeting summary
summary: 一次通話的 AI 會議紀錄：平台、與會者和時長，接著是串流出現的摘要、決議和待辦事項；每一行都連結到它出自的逐字稿原話，每項任務都可以勾選完成或傳送到議題追蹤工具。
source-hash: 3e51431ba295
---

<Demo name="Preview" />

## 何時使用

`MeetingSummary` 是 AI 記錄員在通話結束後留下的卡片。頁首寫明平台、日期、時長和與會的人。一段簡短的彙整過程之後，三個區段以每次幾個字詞的節奏串流出現：摘要（Summary）、決議（Decisions）和待辦事項（Action items）。每一行都帶有時間戳記，滑鼠停在這一行上（或聚焦它的時間戳記）會顯示它出自的逐字稿原話，讀者可以把重點和實際說過的話逐條核對。待辦事項可以勾選完成，也可以傳送到議題追蹤工具或文件應用程式。

- **通話結束後的會議紀錄**：用於 CRM、招募工具、會議錄製工具或團隊工作區。
- **把逐字稿變成決議和有負責人的任務**，再由大家推送到自己的追蹤工具。
- **任何需要可核對的摘要**：每一行都能對照它的出處。

## 何時不用

- **通話進行中的即時字幕或捲動逐字稿**：直接顯示陸續抵達的逐字稿本身。
- **背後沒有會議的一般任務清單**：在你自己的清單裡使用一列列 `Checkbox`。
- **包含很多章節的長文件**：把它算繪成文件頁面，而不是卡片。

## 用法

把會議紀錄放進 `Meeting`，並自己驅動 `Status`：請求進行中設為 `Summarizing`，請求完成時設為 `Ready`（紀錄隨後串流出現），失敗時設為 `Error` 並附上 `ErrorText`。不設定 `Status` 時，卡片在捲動進入畫面後會自己播放一段簡短的彙整過程。既沒有 `Meeting`、`Summarizer` 也沒有 `Simulation` 時，卡片直接顯示「無逐字稿」狀態（"No transcript to summarize"），頁首不顯示詳細資訊。

```csharp
card.Meeting = new MeetingSummaryData
{
    Title = "Onboarding flow review",
    Platform = "Video call",
    StartsAt = new DateTime(2026, 10, 27, 10, 30, 0),
    DurationMinutes = 48,
    Attendees = [new("ines", "Ines Duarte", "Design"), new("kofi", "Kofi Mensah")],
    Summary = [new("s1", "Most new teams stall on the invite step.",
        new MeetingSummaryQuote("ines", TimeSpan.FromSeconds(252), "Seven of the ten teams got stuck on invites."))],
    ActionItems = [new MeetingActionItem("a1", "Move the invite step after project creation", "kofi",
        new DateOnly(2026, 11, 3))],
};
card.Status = MeetingSummaryStatus.Summarizing;
// ... when your request resolves:
card.Status = MeetingSummaryStatus.Ready;
```

`Quote.At` 是從錄音開始算起的時間，卡片把它顯示為 `4:12`。`StartsAt` 依原樣印出，因此請在傳入之前完成時區換算。

### 模擬

想在沒有模型的情況下展示卡片（示範、設計審查、新手導覽），就給它一個 `MeetingSummarySimulation`：會議紀錄、要署名的模型，以及模擬傳送的編號方式。卡片捲動進入畫面後，會播放彙整過程、串流顯示紀錄，並且只要沒有人先操作卡片，就會勾選第二個待辦事項並傳送第一個；傳送是模擬的，並會說明什麼都沒有傳送。你自己的 `Meeting` 或 `Summarizer` 優先於模擬；把 `Simulation` 設為 null 會停止模擬並顯示「無逐字稿」狀態。

```csharp
card.Simulation = new MeetingSummarySimulation
{
    Meeting = notes,
    Model = new AgentModel("Qwen3.8-Max", ArcIcons.Sparkles),
    ReferenceFormats = new Dictionary<string, string> { ["issues"] = "ISS-{0}", ["docs"] = "Notes {0}" },
    FirstReferenceNumber = 142,
};
```

上方的預覽播放的就是一個模擬，其會議來自圖庫自己的示範資料。

### 你自己的會議

<Demo name="YourMeeting" />

這張卡片的資料、狀態和傳送都由應用程式掌管。頁尾的重新產生和錯誤狀態中的重試都會觸發 `RegenerateRequested`；自己驅動狀態的卡片還會重新播放它的彙整過程。

### 從你的模型串流取得

<Demo name="Summarizer" />

`Summarizer` 是一個非同步串流委派。卡片自己驅動狀態時，會在第一次進入畫面和重新產生時呼叫它：在第一個快照抵達之前顯示彙整過程，之後串流顯示你產出的每一個快照（每一個都是截至目前的紀錄）。仍然吻合的文字會繼續串流出現，擲出例外則會顯示帶有例外訊息的錯誤狀態。請把 `Meeting` 設為這次通話的頁首資訊，這樣在彙整期間也能顯示標題和與會者。

```csharp
card.Meeting = header; // title, date and attendees, no notes yet
card.Summarizer = SummarizeAsync;

async IAsyncEnumerable<MeetingSummaryData> SummarizeAsync([EnumeratorCancellation] CancellationToken token)
{
    await foreach (var notes in myModel.StreamNotesAsync(transcript, token))
    {
        yield return notes;
    }
}
```

### 傳送待辦事項

每個待辦事項為 `SendTargets` 中的每一項提供一個傳送按鈕：預設是通用的 `Issues` 和 `Docs`（可以傳入帶有你產品標誌的 `MeetingSummarySendTarget`，或傳入空清單以隱藏按鈕）。按下按鈕會觸發 `SendRequested`；把 `Completion` 設為你的請求，並把權杖傳下去。按鈕先顯示載入圈，然後顯示一個標籤，內含你傳回的編號（"ENG-31"）和一個勾號；任務出錯時則顯示警示圖示和 Retry。沒有處理常式時，傳送是模擬的，即時區域會說明什麼都沒有傳送；其標籤顯示「Sent」，或模擬的 `ReferenceFormats` 提供的編號。

```csharp
card.SendRequested += (_, e) => e.Completion = CreateIssueAsync(e.Item, e.Target, e.CancellationToken);

async Task<MeetingSummarySendResult?> CreateIssueAsync(MeetingActionItem item, MeetingSummarySendTarget target,
    CancellationToken token)
{
    var issue = await tracker.CreateAsync(item.Text, token);
    return new MeetingSummarySendResult(issue.Key, issue.Url);
}
```

傳送成功後會觸發 `ActionItemSent`。卡片被移除或重新產生時，權杖會被取消，按鈕回到它的標誌。帶有 `Url` 的已傳送標籤點按後會開啟該連結。

### 勾選待辦事項

勾選一個事項會觸發 `ActionItemChanged`，參數中帶有該事項及其新狀態；勾選狀態由卡片自己保存。`IsDone` 為 true 的事項一開始就是勾選狀態。

### 逐字稿原話

在任一行上稍作停留，它的原話就會在下方開啟（視窗空間不足時翻到上方）。原話開啟期間，移到另一行會立即切換。用鍵盤聚焦時間戳記會顯示它的原話；Enter、Space 或輕點會把它固定住，Escape 或在別處按下則會關閉。

### 狀態

| Status | 主體 |
|---|---|
| `Summarizing` | 在佔位列上方閃爍的 "Summarizing transcript" |
| `Ready` | 摘要、決議和待辦事項（沒有內容的區段不顯示） |
| `Ready`，清單為空 | "No transcript to summarize" |
| `Error` | "Couldn't summarize this meeting"、`ErrorText` 和 Try again |

### 窄卡片

<Demo name="Narrow" />

卡片會填滿容器，最寬 640px，最窄可到約 360px。寬度低於 460px 時，與會者頭像組移到日期下方；低於 520px 時，負責人只顯示名字，讓每個事項下的資訊保持在一行（螢幕閱讀器仍會讀出全名）。`AutoPlay="False"` 讓卡片一開始就顯示完成的紀錄。

### 你自己的文字

`Labels` 包含卡片顯示或朗讀的每一個文字。替換這個物件即可在地化或改寫：

```csharp
card.Labels = new MeetingSummaryLabels
{
    Summary = "Résumé",
    ActionItems = "Actions",
    Due = due => "Pour le " + due.ToString("d MMM"),
};
```

`Model` 設定頁尾中署名的模型（一個帶名稱和標誌的 `AgentModel`）；設為 null 則隱藏署名。`MeetingSummaryData.PlatformMark` 用你的平台標誌取代視訊圖示。

### 減少動態效果

減少動態效果時，紀錄整段出現而不是串流出現，主體和傳送按鈕位置的尺寸立即改變，核取方塊、刪除線和原話直接顯示最終狀態，不做描繪或滑動。所有播報照常進行。彙整過程會等到卡片出現在螢幕上才開始。

### 內容指引

- 一條摘要是一句話，說明發生了什麼；一條決議說明決定了什麼，而不是誰爭論了什麼。
- 待辦事項以動詞開頭，並只寫一位負責人。
- 每段原話只保留支撐這一行的一兩句話。

### 常見問題

**能顯示已經完成的紀錄嗎？** 把 `Status` 設為 `Ready`（或把 `AutoPlay` 設為 false），紀錄就會整段顯示。

**勾選狀態由誰保存？** 由卡片保存。`ActionItemChanged` 會告訴你每一次變化；`IsDone` 提供初始狀態。

## API 參考

<!-- api: MeetingSummary -->

<!-- api: MeetingSummaryData -->

<!-- api: MeetingActionItem -->

<!-- api: MeetingSummarySendTarget -->

<!-- api: MeetingSummaryLabels -->

<!-- api: MeetingSummarySimulation -->

## 無障礙

- 卡片是一個以會議命名的群組，標題是二級標題，每個區段是三級標題。
- 時間戳記是按鈕，原話開啟期間回報為已展開；原話的文字是它的說明文字。
- 每個核取方塊都是具有核取方塊角色和勾選狀態的按鈕，每個核取方塊和傳送按鈕都以待辦事項的文字命名，因此即使連成一串也能讀得清楚。
- 16px 的核取方塊有 32px 的點按區域，鍵盤焦點環緊貼方框。
- 一個禮貌型即時區域會播報彙整開始、彙整完成、勾選的事項和傳送結果；沒有處理常式時還會說明 "Simulated, nothing was sent"。
- 狀態從不只依賴顏色：傳送失敗顯示警示圖示和 Retry，已傳送的事項顯示目標的標誌、編號和勾號。
- 平台名稱先於日期讀出；只顯示名字的負責人對螢幕閱讀器仍保留全名。

### 鍵盤

| 按鍵 | 動作 |
|---|---|
| Tab | 依序經過與會者、每個時間戳記、核取方塊和傳送按鈕，然後是複製和重新產生 |
| 聚焦時間戳記 | 顯示這一行的逐字稿原話 |
| Enter、Space | 在時間戳記上固定或關閉原話；在核取方塊上切換勾選；在傳送按鈕上傳送該事項 |
| Escape | 關閉開啟的原話 |

## 動效

| 變化 | 完整 | 減少 | 關閉 |
|---|---|---|---|
| 彙整中 | 約一秒，標籤閃爍 | 相同，不閃爍 | 相同，不閃爍 |
| 紀錄抵達 | 每 34ms 出現 3 到 8 個字詞，每塊淡入（總共約 1.5 秒） | 立即整段出現 | 立即整段出現 |
| 主體在狀態之間切換 | 高度彈簧過渡（0.24 秒，無回彈），新狀態淡入 | 立即 | 立即 |
| 負責人、日期、按鈕 | 文字串流出現時保留位置，文字完整後淡入 | 立即 | 立即 |
| 勾選事項 | 填色放大出現（160ms），勾號描繪出來（200ms，ease-out），1px 刪除線橫掃每一個換行，文字過渡為淡化色 | 立即到最終狀態 | 立即到最終狀態 |
| 取消勾選 | 反向進行，更快（120ms） | 立即 | 立即 |
| 按下核取方塊 | 方框縮放到 0.9，整列不縮放 | 相同 | 相同 |
| 傳送 | 標誌、載入圈和已傳送標籤交叉淡化，按鈕位置彈簧過渡到新寬度（0.25 秒，無回彈） | 短暫交叉淡化，尺寸立即改變 | 立即 |
| 原話 | 短暫停留後用 160ms 淡入並上升 3px；在列之間立即切換 | 淡入 | 立即 |

## 相關

- `ToolCall`：代理程式發起的工具呼叫。
- `AgentStatus`：正在執行的代理程式的即時狀態。
- `Checkbox`：一般的勾選清單。
- `Avatar` 和 `AvatarGroup`：人員。
- `CopyButton`：複製一個值並給出確認。
