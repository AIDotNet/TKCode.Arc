---
title: Call insights
summary: 拆開來看的一通銷售通話錄音：帶情緒曲線、每位發言人一條軌道、關鍵時刻和話題的時間軸，可播放、拖曳或用鍵盤逐步移動的播放頭；跟隨播放頭的文字記錄；發言時長占比和後續步驟。
source-hash: 43832af61191
---

<Demo name="Preview" />

## 何時使用

`CallInsights` 是銷售團隊在通話錄音之後打開的頁面。它一眼回答四個問題：通話的氣氛如何隨時間變化、誰在說話、談了什麼、約定了什麼。時間軸把情緒曲線（中線以上為綠色，以下為紅色）、每位發言人一條軌道和關鍵時刻放在同一條時間軸上，下方有播放頭；旁邊的文字記錄跟隨播放頭，所以時間軸上的某個時刻和背後的原話始終只差一次點擊。

- **回看一次需求或演示通話。** 看看氣氛在哪裡下降，跳到反對意見處，讀一讀當時說了什麼。
- **輔導。** 發言時長占比、最長的獨白和銷售提出的問題，能看出通話是否均衡。
- **交接一單生意。** 話題和後續步驟告訴下一個人已經談過什麼、還欠什麼。

## 何時不用

- **即時通話。** 本區塊回看錄音，不會即時傳輸語音，也不做轉寫。
- **播放音訊。** 本區塊沒有自己的音訊。有播放器時，把 `Position` 綁定到播放器上。
- **沒有發言人的長篇會議記錄。** 沒有可對齊的文字記錄時，請使用 `Markdown` 或文件檢視。

## 用法

本區塊派生自 `TemplatedControl`。沒有資料時，它顯示一通虛構的 24 分鐘需求通話。通過五個集合提供你自己的通話：`Speakers`（鍵、名字、角色以及是否屬於你這一方）、`Utterances`（發言人鍵、開始、結束、文字以及 −1 到 1 的情緒值）、`Moments`、`Topics` 和 `NextSteps`。

```xml
<arc:CallInsights Title="Renewal check-in" Subtitle="Thu, 6 min" IsFramed="False" />
```

```csharp
call.Speakers.Add(new CallInsightsSpeaker("jonas", "Jonas Weber", "Customer success", isHost: true));
call.Utterances.Add(new CallInsightsUtterance("jonas", TimeSpan.Zero, TimeSpan.FromSeconds(40), "How has the queue been?", 0.3));
call.NextStepToggled += (_, e) => tasks.SetDone(e.Step.Text, e.IsDone);
```

### 時間軸與播放頭

`Position`（雙向）就是播放頭。在時間軸上按下或拖曳會移動它，按在某個時刻標記上會對齊到該時刻，點擊文字記錄中的一行或側欄中的一個時刻會跳到它的開始處。這些操作都會引發 `Seeked`。播放按鈕按 `PlaybackRate` 即時移動播放頭（速度按鈕在 1×、1.5× 和 2× 之間切換），到結尾時停止；`Play`、`Pause` 和 `Seek` 在程式碼中做同樣的事。

### 文字記錄、話題與後續步驟

`SearchText` 只保留包含它的行，並在軌道下方標出。選擇一個話題（`SelectedTopic`）會在時間軸上為它的各段著色。勾選一個後續步驟會寫入 `IsDone` 並引發 `NextStepToggled`。發言時長占比、最長獨白和提問次數都由發言內容算出（`TalkShares`）。本區塊顯示的每個文字都在 `Labels`（`CallInsightsLabels`）中。

<Demo name="OwnCall" />

## API 參考

<!-- api: CallInsights -->

<!-- api: CallInsightsSpeaker -->

<!-- api: CallInsightsUtterance -->

<!-- api: CallInsightsMoment -->

<!-- api: CallInsightsTopic -->

<!-- api: CallInsightsNextStep -->

## 無障礙

- 時間軸是一個名為 "Call timeline" 的滑桿：**←/→** 移動 5 秒（**Shift**：30 秒），**Home/End** 跳到兩端，**PageUp/PageDown** 跳到上一個或下一個時刻，**Space** 播放或暫停。它的值是以秒計的位置。
- 文字記錄的行、時刻和話題都是按鈕，以其內容命名；選中的話題會報告 "selected"。
- 後續步驟是核取方塊，以步驟命名，負責人和截止日期作為說明。
- 跳轉、話題選擇和搜尋結果數會以禮貌方式播報。

## 動效

- 區塊第一次顯示時，情緒曲線從左向右畫出（900 毫秒）。
- 行和話題在懸停時用 160 毫秒著色；播放頭通過變換移動，所以播放時不會重畫曲線。
- 減少動效和關閉動效時，曲線立即顯示，著色也不再過渡。

## 響應式

- 本區塊讀取自身的寬度。在 `CompactWidth`（760 px）或更窄時，側欄移到文字記錄下方，內邊距從 24 收緊到 16。
- 時間軸總是占滿整個寬度；刻度文字會變稀，保證彼此至少相隔 70 px。
- 過長的名字和懸停讀數以省略號結尾，而不是換行。

## 相關元件

- `LineChart` 和 `Sparkline`，用於普通的時間序列。
- `Timeline` 和 `ChatThread`，用於動態和消息。
- `Account health` 和 `Lead scoring`，其他銷售類區塊。
