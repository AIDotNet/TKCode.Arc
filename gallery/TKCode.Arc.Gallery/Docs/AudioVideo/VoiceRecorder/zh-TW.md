---
title: Voice recorder
summary: 語音留言輸入區：錄進即時波形，回聽並拖曳定位這段錄音，再把它當成語音泡泡傳進對話。
source-hash: af4f52da0286
---

<Demo name="Preview" />

## 何時使用

`VoiceRecorder` 是聊天中的語音訊息角落：上方是一串語音泡泡，下方是只有一顆圓形麥克風按鈕的輸入區。輕點一下，麥克風就展開成錄音列，
裡面有呼吸閃爍的圓點、持續走動的計時和從右側捲入的波形。停止後同一條錄音列會變成回聽狀態，可以重播並拖曳定位；傳送會把錄音列帶進對話串，
成為你最新的一則泡泡。垃圾桶按鈕或 Escape 會把它收回成麥克風。

- **對話中的非同步語音回覆**：客服聊天、團隊討論串、客戶與施工方之間往返的簡短留言。
- **不方便打字的場合**：在手機上、在櫃檯前、在工地戴著手套時。
- **傳送前先回聽**：先聽一遍、看一遍這段錄音，不會誤傳。
- **雙向混排的對話串**：收到的泡泡在左側並附上傳送者頭像，你傳出的在右側。

## 何時不用

- **音樂、Podcast 或長錄音**：使用具備完整進度列與播放佇列的媒體播放器；泡泡適合簡短留言。
- **透過長按按鈕確認危險操作**：使用長按確認控制項。
- **只顯示某件事已進行多久**：使用計時器或碼錶，而不是錄音器。
- **對文字欄位即時口述**：使用 `DictationButton`，它會邊說邊把語音轉成文字。
- **朗讀一段文字回覆**：使用 `SpeechPlayer`。

## 用法

給錄音器一個對話串，並處理錄好的這段錄音。什麼都不設定時，對話串是空的，輸入區照常運作。
錄音是模擬的：每段錄音都會得到一個虛構但穩定的聲音，因此不需要麥克風也能執行。需要真實聲音時，
透過 `MessageSent` 與 `LevelSource` 接上你的音訊擷取。

```xml
<arc:VoiceRecorder x:Name="Recorder"
                   Height="408"
                   Label="Voice messages with Tomas Novak" />
```

```csharp
Recorder.Messages = VoiceRecorderSamples.Thread();
Recorder.MessageSent += (_, e) => store.Save(e.Message);
Recorder.Cancelled += (_, e) => log.Info($"Discarded {e.Message.Duration}");
```

`VoiceRecorder` 衍生自 Avalonia 的 `TemplatedControl`，位於 Special 套件中：在 `UseArcControls()` 之外再用
`UseArcSpecial()` 註冊。上方的預覽就是參考設定，人物皆為虛構：一張 416 × 408 的卡片，附一個標頭（繪製的首字母頭像、
聯絡人姓名、話題和一顆重設按鈕），三則訊息的對話串（對方、你、對方，長度分別為 14、6 和 9 秒），以及預設 60 秒的上限。標頭和重設按鈕
屬於範例本身；錄音器只為它們提供一個 `Header` 位置。

### 訊息

對話串是一組 `VoiceRecorderMessage` 記錄，最早的在前。帶有 `From`（或 `Avatar`）的訊息是收到的：
它在左側，附一個 28px 頭像（圖片，或 `From` 的首字母），泡泡為柔和底色、播放按鈕為深色。
兩者都沒有的訊息是你傳出的：靠右對齊，泡泡為深色、播放按鈕為淺色。`Meta` 是泡泡下方的小字行，例如傳送時間。

```csharp
var message = new VoiceRecorderMessage
{
    Id = "fence-1",
    From = "Tomas Novak",
    Duration = TimeSpan.FromSeconds(14),
    Peaks = VoiceRecorderPeaks.Generate(seed: 1, seconds: 14),
    Meta = "10:12 AM",
};
```

`Peaks` 是從 0 到 1 的長條高度，其中 0.12 代表靜音。一個泡泡有 14 根長條再加每秒 1.3 根，介於
18 到 36 之間（`VoiceRecorderPeaks.BarCount`），因此在約 20 秒以內，訊息越長泡泡越寬。用
`VoiceRecorderPeaks.Resample` 把你自己的音量序列變成這麼多根長條（每根長條取它那一段裡最響的樣本），
或用 `VoiceRecorderPeaks.Generate` 產生穩定的虛構波形。

傳送的錄音會附加到 `Messages`（唯讀清單會被替換成一份加上這段錄音的副本）。若要在多次工作階段之間保留對話串，
請在 `MessageSent` 中儲存這則訊息，下次再傳回來。`ResetMessages` 會替換整個對話串。

### 你自己的事件

<Demo name="Events" />

`MessageSent` 與 `Cancelled` 都以 `VoiceRecorderMessage` 帶著這段錄音：它的長度、長條和一個產生的 id。
傳送的那則還帶有 `SentMeta`（預設為 "Just now"），而且事件抵達時它已經在對話串中。
`Phase` 表示輸入區所處的階段（`Idle`、`Recording`、`Review`），可以繫結。`MaxDuration` 會自動結束一段錄音
並開啟回聽；這個範例在 15 秒處停止，並捲動一個更長的對話串。

你也可以從程式碼操作錄音器：`StartRecording`、`StopRecording`、`Send`、`Discard`，以及傳入訊息 id 的 `PlayMessage` 或
`PauseMessage`。

### 錄音與回聽

- **錄音**：錄音列顯示一個每 1.2 秒呼吸一次的玫紅圓點、精確到十分之一秒的時間（0:02.4）和即時
  波形。每 90ms 會有一根新長條從右側進入，整排等速向左滑動；越靠左端的長條越淡。
  停止按鈕的無障礙名稱會帶上時間（"Stop recording at 0:02"）。
- **回聽**：錄音列大小不變，交叉淡化成播放按鈕、這段錄音的 40 根長條和它的長度。播放時長條從左往右
  填滿，時間隨之增加；暫停或播完後時間回到總長度。在長條上任何位置按下即跳到那裡，不會開始
  播放。
- **傳送**：錄音列變成對話串末端的一則泡泡，從清單下緣升起，同時清單平滑捲到底部；
  麥克風彈回原位。

同一時間只播放一個聲音：開始播放一則泡泡會暫停其他泡泡和回聽，開始錄音會把它們全部
暫停。

### 來自麥克風的音量

<Demo name="CustomLabels" />

`LevelSource` 每 90ms 的樣本呼叫一次，參數是錄音編號和樣本索引；回傳 0 到 1 的輸入音量。
沒有設定時，錄音器會使用以錄音編號為種子（訊息數加一）的 `VoiceRecorderPeaks.Levels`，
因此捨棄後重新錄的那段會顯示同樣的聲音。這個範例也透過 `Labels` 改寫了文字，並修改了 `SentMeta`。

### 窄容器

<Demo name="Narrow" />

錄音器沒有中斷點：它跟隨得到的寬度。錄音列使用垃圾桶和傳送按鈕以外的空間，
泡泡被擠壓時長條會變細，連 1.5px 的長條都放不下時回聽會顯示較少的長條。`Plain`
會拿掉卡片的邊框、圓角和底色，適合手機寬度的欄位。

### 減少動態效果

<Demo name="ReducedMotion" />

### 結構

1. **標頭**（選用）：細線上方的任意內容；預覽在這裡放了頭像、姓名、話題和重設按鈕。
2. **對話串**：語音泡泡，底部對齊，捲動時不顯示捲軸。
3. **泡泡**：44px 的膠囊，附一個 32px 播放按鈕、波形（已播放部分為實色，其餘為 28%）和時間。
4. **附註行**：泡泡下方 12px 的淡色文字。
5. **輸入區**：靜止時是提示文字和 48px 麥克風；錄音或回聽時是垃圾桶、錄音列和停止（或傳送）按鈕。
6. **狀態區域**：用於播報的隱藏禮貌型即時區域。

### 內容指引

- 提示文字保持一行短句，說明輕點會發生什麼事。在窄容器中會被截斷。
- 在 `Meta` 中使用真實時間或相對日期，並保持簡短（"10:16 AM"、"Yesterday"）。
- 在 `Label` 中寫出對方的名字（"Voice messages with Tomas Novak"）：它是錄音器的無障礙名稱。
- 把 `SentMeta` 寫成狀態而不是指示（"Just now"、"Sent"、"Delivered"）。

### 最佳做法

- 立即儲存傳送的訊息；控制項只把它保存在 `Messages` 中。
- 選擇適合對話的 `MaxDuration`：一分鐘適合大多數語音留言。
- 給錄音器固定高度（或一個限制高度的父元素），讓對話串捲動而不是持續變高。
- 在本身已有外框的面板和手機版面中使用 `Plain`。
- 接上真實音訊時，讓 `LevelSource` 保持輕量：它每 90ms 會在 UI 執行緒上呼叫一次。

### 常見問題

**它會錄音嗎？** 不會。這個套件沒有麥克風或音訊相依性。音量和錄音都是模擬的；
請把你的擷取接到 `LevelSource` 與 `MessageSent`。

**為什麼捨棄後同一段錄音看起來一樣？** 模擬的聲音以錄音編號為種子，而錄音編號只在傳送後
才會改變。需要即時音量時請提供 `LevelSource`。

**兩則訊息能同時播放嗎？** 不能。開始播放一則會暫停另一則。

**如何清空對話串？** 用空清單呼叫 `ResetMessages`，或設定 `Messages`。

## API 參考

<!-- api: VoiceRecorder -->

<!-- api: VoiceRecorderMessage -->

<!-- api: VoiceRecorderLabels -->

<!-- api: VoiceRecorderMessageEventArgs -->

<!-- api: VoiceRecorderPeaks -->

<!-- api: VoiceRecorderSamples -->

列舉與委派：

| 型別 | 值 |
|---|---|
| `VoiceRecorderPhase` | `Idle`、`Recording`、`Review` |
| `VoiceRecorderVariant` | `Card`（預設）、`Plain` |
| `VoiceRecorderLevelSource` | `double (int take, int sample)`：一個 90ms 樣本的音量，0 到 1 |

## 無障礙

- 錄音器以 `Label` 命名；對話串命名為 "Messages"。
- 每顆按鈕都有名稱："Record voice note"、"Stop recording at 0:04"（每秒更新）、"Discard voice note"、
  "Send voice note"、"Play preview" 或 "Pause preview"，每則泡泡上則是 "Play voice message from Tomas Novak, 0:14" 或
  "Play voice message, 0:06"（播放時為 Pause）。
- 禮貌型即時區域會播報 "Recording"、"Recorded 0:04. Play it back or send it."、"Voice note sent" 和 "Voice note
  discarded"。播放與重設不會播報。
- 波形、計時和圓點屬於裝飾，對輔助技術隱藏；時間由按鈕名稱傳達。
- 鍵盤焦點會在圓形按鈕外 2px 處顯示 2px 的焦點環；指標焦點不顯示。
- 焦點跟著輸入區走：麥克風把焦點交給停止，停止交給傳送，傳送或捨棄再交回麥克風。指標按下
  同樣會把焦點留在錄音器內（不顯示焦點環），因此 Escape 始終有效。

### 鍵盤

| 按鍵 | 動作 |
|---|---|
| `Tab` / `Shift+Tab` | 依序經過標頭、各泡泡的播放按鈕，然後是輸入區：靜止時為麥克風；錄音時為垃圾桶和停止；回聽時為垃圾桶、播放預覽和傳送 |
| `Enter` / `Space` | 按下目前取得焦點的按鈕 |
| `Escape` | 錄音或回聽時，在錄音器內任何位置：捨棄這段錄音並把焦點交回麥克風 |

波形無法取得焦點；請用指標按下來定位。

## 動態效果

- 麥克風展開成錄音列（以及錄音列收回）使用同一個臨界阻尼彈簧，大約半秒看起來就已到位；
  兩端一起移動，內容貼在錄音列左端並被它裁切。在錄音列還在展開時開始收回，會保留當下的速度，
  因此會稍微多衝一點再折返。
- 錄音列展開時，垃圾桶和停止按鈕以較快的彈簧從 70% 彈出；離開時在 120ms 內縮小並淡出。
- 提示文字和錄音列的兩行內容以 2px 模糊互相替換：120ms 淡出，50ms 後以 240ms 淡入。
- 即時長條以約每秒 55 像素向左移動，並在約 110ms 內長高。
- 傳送時，泡泡以與麥克風相同的彈簧從錄音列中長出，同時清單在約 130ms 內平滑捲到底部；
  麥克風以較慢的彈簧從 80% 回來。
- 圓形播放按鈕按下時縮小到 92%。

| 變化 | 完整 | 減少 | 關閉 |
|---|---|---|---|
| 麥克風到錄音列、錄音列到麥克風、錄音列到泡泡 | 彈簧 | 立即 | 立即 |
| 按鈕出現 | 縮放並淡入 | 淡入 | 立即 |
| 提示文字與錄音列內容 | 模糊並淡入淡出，延遲 50ms | 模糊並淡入淡出，無延遲 | 立即 |
| 即時波形 | 平滑移動，長條逐漸長高 | 每個樣本移動一格 | 每個樣本移動一格 |
| 傳送後的清單 | 平滑捲動 | 直接跳轉 | 直接跳轉 |
| 錄音圓點 | 呼吸 | 呼吸 | 靜止 |

## 響應式

- `VoiceRecorder` 沒有中斷點；與參考一樣，它跟隨父元素給的寬度。
- 錄音列寬度為輸入區列寬減去 104 px（垃圾桶、傳送按鈕和兩個間距）；416 px 時為 282 px。
- 泡泡在放得下時保持自然寬度；被擠壓時會先讓 3 px 的長條變細。
- 回聽的 40 根長條以每根 1.5 到 3 px 分享錄音列的空間；再窄時會繪製較少的長條，而不是把末端裁掉。
- 高度由父元素決定：給錄音器固定高度，讓對話串捲動。

## 相關元件

- `DictationButton`：邊說邊把語音輸入到文字欄位。
- `SpeechPlayer`：朗讀一段文字回覆。

| 元件 | 何時選擇 |
|---|---|
| `VoiceRecorder` | 大家在對話串中互傳簡短語音留言 |
| `DictationButton` | 語音需要變成文字 |
| `SpeechPlayer` | 一段文字回覆需要被朗讀出來 |
