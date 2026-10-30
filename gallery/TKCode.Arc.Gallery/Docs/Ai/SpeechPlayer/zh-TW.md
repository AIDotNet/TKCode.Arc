---
title: Speech player
summary: 為 AI 回覆朗讀的播放器：Read aloud 會變成一個精簡的播放器，具備播放、可拖曳的進度列、語速與聲音選擇，醒目標示會隨著朗讀的字詞在回覆中移動。
source-hash: 0f58f8c877f9
---

<Demo name="Preview" />

## 何時使用

`SpeechPlayer` 會把 AI 的回覆唸出來。回覆下方是它的操作列；Read aloud 會把這一列變成同樣高度的精簡播放器：播放與暫停、已播放時間、
一條可以拖曳的細進度列、總長度、語速按鈕、聲音選擇和 Stop reading。播放時，一個圓角醒目標示會像卡拉 OK 一樣隨著唸到的字詞在文字中
移動，還沒唸到的字詞則維持淡化。

- **聊天與回答介面**：使用者希望回覆被唸出來，並在文字中跟著目前唸到的字詞。
- **無障礙與免看閱讀**：朗讀較長的回答、摘要或簡報。
- **試聽文字轉語音的聲音與語速**：用真實的產品文案比較效果。

## 何時不用

- **把語音轉成文字**寫進輸入框：請使用聽寫控制項。
- **Podcast 或長錄音**，沒有可標示的逐字稿：請使用一般的音訊播放器。
- **文字與實際朗讀內容不一致**：請先移除 Markdown 與格式，否則醒目標示會錯位。

## 用法

把 `Text` 設為回覆內容，把 `Synthesize` 設為呼叫你的文字轉語音服務的函式。它會收到一個 `SpeechRequest`（文字、聲音與取消權杖），
並傳回一個 `SpeechAudio`：以 `ISpeechAudio` 表示的播放、逐字時間與總長度。播放器會驅動播放（播放、暫停、跳轉、倍速），並在每一個
影格讀取它的位置，所以你的音訊就是時鐘。

```csharp
player.Text = message.Text;
player.Synthesize = async request =>
{
    var speech = await tts.SpeakAsync(request.Text, request.Voice.Id, request.CancellationToken);
    return new SpeechAudio
    {
        Audio = new MyAudioAdapter(speech.Stream),   // implements ISpeechAudio
        Timings = speech.Words                       // one start and end per word, or null
            .Select(w => new SpeechWordTiming(w.Start, w.End))
            .ToList(),
    };
};
```

`Text` 中每個以空白分隔的字詞是一個標示步驟，空行開始新的段落，因此服務供應商的逐字對齊資料可以一一對應。沒有逐字時間時，播放器
會根據文字估算，並依音訊長度伸縮對齊（`SpeechSimulation.EstimateTimings`、`SpeechSimulation.FitTimings`）。未設定 `Synthesize`
時，播放器是不發出聲音的定時模擬。未設定 `Text`（也沒有下文的模擬）時播放器為空白：沒有回覆，Read aloud 維持停用（上方的 Empty 分頁）。未設定 `Voices` 時，聲音選擇器只提供一個 id 為空的聲音，也就是你的服務的預設聲音，名稱來自 `SpeechPlayerLabels.DefaultVoice`。

### 結構

1. **作者列**：16px 的標誌與模型名稱（`AuthorName`、`AuthorMark`）；沒有作者時隱藏。
2. **回覆**：分段的文字。朗讀時，目前的字詞落在強調色的醒目標示上，已唸過的字詞為完整顏色，還沒唸到的字詞淡化。
3. **底列**：一個 32px 的位置，放操作列（Read aloud，接著是 Copy、Good response 與 Bad response，或你的 `Actions`）或是播放器。
4. **播放器**：播放、已播放時間、進度列、總長度、語速、聲音與 Stop reading。

### 狀態

| 狀態 | 播放器顯示 |
|---|---|
| `Idle` | 含 Read aloud 的操作列 |
| `Preparing` | 播放按鈕中的載入圈，以及帶有微光的 "Preparing Ava's voice" |
| `Playing` | 暫停按鈕、移動的進度列，以及跟著字詞移動的醒目標示 |
| `Paused` | 播放按鈕；醒目標示停在目前的字詞上 |
| `Finished` | Play again；整段回覆為完整顏色 |
| `Failed` | Retry、警示圖示，以及 "Couldn't generate audio" 與你的服務擲回的原因 |

朗讀中切換聲音時，會中止舊聲音的請求、取得新聲音，並從目前唸到的字詞繼續。語音會依聲音保留，因此切回某個聲音、或在 Stop reading
之後再次朗讀，都會立即開始。

### 在聊天欄中

<Demo name="Plain" />

`Variant="Plain"` 會移除邊框與內距，讓回覆融入對話紀錄。`Actions` 以你自己的按鈕取代 Copy、Good response 與 Bad response；
`ShowsDefaultActions="False"` 只保留 Read aloud。`Voices` 設定聲音選擇中的聲音，每個聲音都有 id、名稱與一行簡短說明。

### 事件

<Demo name="Events" />

`Played`、`Paused`、`Ended` 與 `Seeked` 帶有 1× 時間軸上的時間與總長度、字詞索引、聲音與語速。`WordChanged` 另外帶有該字詞，可用於
字幕或分析。`Seeked` 會在拖曳進度列結束時觸發一次，而不是每次移動都觸發。另有 `VoiceChanged`、`SpeedChanged` 與 `Closed`。
`SelectedVoiceId` 與 `Speed` 為雙向。

```csharp
player.WordChanged += (_, e) => caption.Text = e.Word;
player.Played += (_, e) => analytics.Track("tts_play", e.Voice.Id, e.Speed);
```

### 以程式碼控制

`Open()` 等同於 Read aloud；`Play()`、`Pause()`、`TogglePlayback()`、`Seek(TimeSpan)`、`SeekToWord(int)`、`Retry()` 與 `Close()`
的作用與對應的按鈕相同。`State`、`IsOpen`、`CurrentWordIndex`、`Position` 與 `Duration` 說明它目前的狀態。

### 模擬語音

用於展示時，不要設定 `Text`，而是給播放器一個 `SpeechSimulation`，並傳入你自己的回覆、作者與聲音。播放器進入可視範圍後它開始朗讀，在完整動效下，短暫停頓後會改用前兩個聲音中的另一個再唸一次，直到有人操作。應用程式自己的 `Text` 一律優先，把 `Simulation` 設為 null 會停止它。預覽中的回覆與聲音是虛構的；作者是真實模型 Claude Sonnet 5.5。

```csharp
player.Simulation = new SpeechSimulation
{
    Text = "Lisbon in early May is a lovely choice. Mornings start near 16°C…",
    AuthorName = "Claude Sonnet 5.5",
    Voices = [new SpeechVoice("ava", "Ava", "Warm and conversational"), new SpeechVoice("leo", "Leo", "Calm, lower register")],
};
```

`SpeechSimulation.SynthesizeAsync` 是所有未設定 `Synthesize` 的播放器背後的替身：它會像一次網路往返那樣等待（640ms），然後傳回估算的逐字時間，不含音訊。
傳入 `Fail = true` 可查看失敗狀態，傳入較長的 `Delay` 可查看 Preparing。使用它時請保持 `IsSimulated` 開啟，讓聲音選單與播報說明
不會播放聲音。

```csharp
player.Synthesize = request => SpeechSimulation.SynthesizeAsync(request,
    new SpeechSimulationOptions { Delay = TimeSpan.FromSeconds(2) });
```

### 自訂文案

`Labels` 包含播放器顯示或播報的每一個字詞。替換整個物件即可在地化或改寫：

```csharp
player.Labels = new SpeechPlayerLabels
{
    ReadAloud = "Vorlesen",
    StopReading = "Vorlesen beenden",
    PreparingVoice = voice => $"Stimme {voice} wird vorbereitet",
};
```

### 窄容器與觸控

卡片會填滿容器，最寬 640px。寬度小於 440px 時，總長度會讓位，讓進度列保有空間；已播放時間、語速、聲音與 Stop reading 仍會顯示。
偵測到觸控後，底列會增高到 36px，讓每個控制項都符合觸控目標尺寸。

### 最佳做法

- 傳入與朗讀內容完全一致的文字：字詞數量必須與逐字時間相符。
- 在 `Synthesize` 中擲回帶有簡短、易懂訊息的例外；它會顯示在 "Couldn't generate audio" 旁邊。
- 遵守請求的取消權杖：切換聲音、Stop reading 或新的文字都會取消它。
- 在對話紀錄中使用 plain 變體，並傳入與訊息其他按鈕一致的操作。

### 常見問題

**它自己會發出聲音嗎？** 不會。`TKCode.Arc.Special` 不相依任何音訊元件；聲音由你的 `ISpeechAudio` 播放。沒有它時，播放器自行計時，
不會發出任何聲音。

**服務供應商沒有逐字時間怎麼辦？** 只傳回音訊即可。逐字時間會根據文字估算，並依音訊長度伸縮對齊。

## API 參考

<!-- api: SpeechPlayer -->

<!-- api: SpeechVoice -->

<!-- api: SpeechAudio -->

<!-- api: ISpeechAudio -->

<!-- api: SpeechRequest -->

<!-- api: SpeechWordTiming -->

<!-- api: SpeechPlaybackEventArgs -->

<!-- api: SpeechWordEventArgs -->

<!-- api: SpeechVoiceChangedEventArgs -->

<!-- api: SpeechSpeedChangedEventArgs -->

<!-- api: SpeechSimulation -->

<!-- api: SpeechSimulationOptions -->

<!-- api: SpeechPlayerLabels -->

## 無障礙

- 播放器是一個有名稱的群組。播放按鈕的名稱會隨狀態改變：Play、Pause、Play again、Retry 或 Preparing audio（請求進行中時保留焦點，
  但按下沒有作用）。
- 進度列是一個滑桿，其數值文字為 "0:04 of 0:22"，每秒更新一次，而不是每個影格更新。
- 一個禮貌模式的狀態區域會播報變化：以 1× 使用 Ava 朗讀（模擬，不播放聲音）、暫停於 0:12、切換到 Leo、朗讀完畢、失敗原因、語速
  變化與回饋。
- 狀態從不只靠顏色表達：失敗時警示圖示會與 "Couldn't generate audio" 及原因一起出現；淡化的未朗讀字詞維持內文對比度，只作為進度
  提示。
- Read aloud 以回覆內容作為描述。聲音選單使用單選列，開啟時定位在已選的聲音上。
- 底列中隱藏的一半在淡出後會離開視覺樹，因此 Tab 不會落在看不見的控制項上。指標取得焦點時不顯示焦點框；鍵盤焦點顯示 Arc 的
  焦點框。

### 鍵盤

| 按鍵 | 操作 |
|---|---|
| Tab | 在 Read aloud 與各操作之間移動；朗讀時在播放、進度列、語速、聲音與 Stop reading 之間移動 |
| Enter、Space | 在 Read aloud 上開啟播放器並開始朗讀，焦點落在播放按鈕；在播放按鈕上播放或暫停；在 Stop reading 上關閉播放器，焦點回到 Read aloud |
| 左方向鍵、右方向鍵 | 在進度列上倒退或前進 2 秒 |
| Page Down、Page Up | 在進度列上倒退或前進 10 秒 |
| Home、End | 在進度列上跳到開頭或結尾 |
| Space、K | 在進度列上播放或暫停 |
| 上方向鍵、下方向鍵 | 在聲音選單中切換聲音；Enter 選擇，Escape 關閉選單 |

## 動態效果

| 變化 | 完整 | 減少 | 關閉 |
|---|---|---|---|
| Read aloud 與 Stop reading | 在同一個 32px 位置中，一列在 100ms 內淡出，另一列在 160ms 內淡入並上升 2px | 120ms 交叉淡化 | 立即 |
| 醒目標示在一行內移動 | 1× 時 150ms 滑過，1.5× 時 100ms，2× 時 75ms | 立即移動 | 立即 |
| 醒目標示到達新的一行 | 直接切過去 | 直接切過去 | 直接切過去 |
| 一個字詞被唸到 | 醒目標示落下時從淡化漸變為完整顏色 | 漸變 | 立即 |
| 開始或停止朗讀 | 淡化效果與醒目標示在 160ms 內淡入淡出 | 淡入淡出 | 立即 |
| 進度列 | 填色與拖曳點逐影格跟隨；滑鼠移入、取得焦點與拖曳時，軌道在 160ms 內變粗、拖曳點放大出現 | 立即變粗 | 立即 |
| 播放、暫停、重播、重試、載入圈 | 原地以無回彈的縮放（0.26s）與 160ms 淡化切換 | 120ms 淡化 | 立即 |
| 語速與聲音標籤 | 在所有選項的隱藏副本上交叉淡化，按鈕寬度不變 | 120ms 淡化 | 立即 |
| Preparing 標籤 | 平緩的微光 | 靜止 | 靜止 |
| 模擬 | 在兩個聲音之間循環，直到有人操作 | 只唸一次 | 只唸一次 |

時鐘在所有動態效果等級下都會執行：朗讀是內容，不是裝飾。尺寸改變時醒目標示會重新測量，不播放動畫。

## 相關元件

- `AgentStatus`：代理程式處理回覆時的即時狀態。
- `ToolCall`：代理程式在回答時呼叫的工具。
- `CopyButton`：訊息操作列中的複製按鈕。
