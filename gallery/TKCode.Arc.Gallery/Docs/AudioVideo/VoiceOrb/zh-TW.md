---
title: Voice orb
summary: 一顆發光的語音模式球體：靜止時緩緩呼吸，聆聽時泛起漣漪，思考時旋轉，說話時隨之脈動，並提供即時字幕與通話控制。
source-hash: a7c6fb7116ea
---

<Demo name="Preview" />

## 何時使用

`VoiceOrb` 是語音對話中助理的臉孔。它是一顆流動、帶有大理石紋的球體，一眼就能讓人知道現在輪到誰：沒有任何動作時它緩緩呼吸；聆聽時隨使用者的聲音膨脹並送出漣漪；思考時，一條明亮的光帶沿著邊緣繞行，球體向內收攏；回答時，它會隨每個字變得更加起伏並跳動。兩個配角完成語音模式的畫面：`VoiceTranscript` 逐字顯示即時字幕，`VoiceControls` 則是通話列，包含開始、靜音、計時器與結束。

- **免持的助理畫面**：廚房顯示器、汽車儀表板、服務台的自助機，或與機器人的電話通話。
- **聊天應用程式中的語音模式**：使用者點按按鈕，聊天畫面退到一旁，直到通話結束前由光球接管。
- **任何需要讓人知道自己是否被聽見的地方**：聆聽、思考與說話看起來明顯不同，而靜音的光球會變得安靜並呈灰色。

## 何時不用

- **錄製要送出的語音訊息**：請使用附波形與審閱步驟的錄音器；光球呈現的是即時對話，而不是錄音檔。
- **文字聊天**：請使用含串流文字的聊天串。字幕只顯示目前的輪次。
- **一般的載入狀態**：請使用轉圈、進度條或骨架屏。光球的動態代表「助理正在思考你說的話」，而不是「頁面正在載入」。
- **聽寫到文字欄位**：請使用 `DictationButton`，它會把內容寫入欄位。

## 用法

三個控制項位於 Special 套件中：請在 `UseArcControls()` 旁呼叫 `UseArcSpecial()` 來註冊。它們各自獨立；語音畫面通常會把它們疊在一起。

```xml
<StackPanel Spacing="14" MaxWidth="480">
    <arc:VoiceOrb State="{Binding State}" IsMuted="{Binding IsMuted}" Size="200" />
    <arc:VoiceTranscript Turn="{Binding Turn}" Speaker="{Binding Speaker}"
                         Text="{Binding Caption}" IsComplete="{Binding CaptionIsFinal}" />
    <arc:VoiceControls IsActive="{Binding InCall, Mode=TwoWay}" IsMuted="{Binding IsMuted, Mode=TwoWay}"
                       Elapsed="{Binding Elapsed}" />
</StackPanel>
```

上方的預覽由 `VoiceOrbSession` 驅動。這是套件為示範與原型提供的替身，用來取代真實的語音後端。它會以逼真的時序循環播放一段虛構的週末出遊對話：使用者的字在開始聆聽 0.9 s 後出現，之後每個字的間隔為 150 ms，再加上前一個字每個字元 22 ms；光球在最後一個字之後 0.7 s 開始思考，持續 1.5 s；回答逐字說出（每個字都讓光球跳動一次），下一段對話則在回答結束 1.3 s 後開始。點按光球會執行當下狀態所邀請的動作：開始通話、立即送出這句話、取消回答並繼續說話，或打斷助理。光球按鈕與其上方的狀態列屬於示範本身。

```csharp
var session = new VoiceOrbSession(VoiceOrbSamples.WeekendTrip);
var link = session.Attach(Orb, Transcript, Controls);   // dispose to disconnect
OrbButton.Click += (_, _) => session.Tap();
```

### 每一種狀態

<Demo name="States" />

| 狀態 | 本體 | 周圍 | 大小（占 `Size` 的比例） |
|---|---|---|---|
| `Idle` | 緩慢的大理石流動 | 微弱的光暈；光球每 5.03 s 呼吸一次 | 0.302 ± 0.0085 |
| `Listening` | 隨輸入音量膨脹 | 兩道漣漪從邊緣向外擴散；光暈變亮 | 0.31，聲音大時最多約 0.34 |
| `Listening`（靜音） | 灰色，稍暗 | 沒有漣漪，光暈暗淡 | 0.295 |
| `Thinking` | 向內收攏；大理石紋扭成漩渦 | 一條明亮的光帶順時針繞行邊緣 | 0.275 |
| `Speaking` | 輪廓更起伏，隨輸出音量脈動 | 最強的光暈 | 0.31，最多約 0.34 |

每次變化都以彈簧平順過渡（約 0.8 s，無過衝）；在過渡途中再次變更狀態時會保留速度，因此快速的聆聽、思考、說話循環不會出現跳動。

### 你自己的音量電平

<Demo name="Levels" />

光球從不開啟麥克風。聆聽時，它每一影格讀取 `InputLevel`（0 到 1）；說話時則讀取 `OutputLevel`。若不設定它們（即非數字），光球會改用 `LevelSource`，或內建的模擬語音：每個音節一次確定性的脈動，片語之間則有換氣。對於語音輸出，你也可以像工作階段那樣，每說出一個字就呼叫一次 `PulseOutput`：每次脈動都會在一個影格內把光球抬起，並在約一秒內淡去。

```csharp
// A microphone meter from your audio stack, 0..1:
meter.LevelChanged += level => Orb.InputLevel = level;
// Or implement IVoiceOrbLevelSource to be read once per frame:
Orb.LevelSource = new MyLevels();
```

### 字幕與通話列

<Demo name="CallBar" />

`VoiceTranscript` 依空白字元切分 `Text`。附加的字只會對新增的字做動畫；其他任何變更都會直接取代整行，不做進場動畫。若要開始新的輪次，請同時變更 `Turn`、`Speaker` 與新的 `Text`（通常為空）：舊的一行向上離開，新的一行淡入。助理的行使用前景色，使用者的行使用次要文字色，而系統行（提示，以及 "Call ended after 1:07." 之類的訊息）字級小一級並以淡色顯示。畫面上保留兩行；較舊的行會在柔和的頂部邊緣下方滑出。

`VoiceControls` 在使用者按下按鈕時會自行維護 `IsActive` 與 `IsMuted`（請雙向繫結），並引發 `StartRequested`、`MuteChanged` 與 `EndRequested`。它不計算時間：請由你的通話計時器推送 `Elapsed`。

### 顏色

<Demo name="Tints" />

光球跟隨主題的強調色。`Tint` 會取代本體、光暈與漣漪的顏色；大理石的淺色紋路則使用 `Tint2`，或由色調衍生出較淺、較柔和的色階（暖色調會偏向橘色）。在深色主題中，同樣的強調色會呈現淡銀色的光球與明亮的光暈。

### 結構

1. **本體**：雙色流動的大理石紋，具備球體的明暗、菲涅耳邊緣光，以及位於左上方的柔和高光。其輪廓是一條 96 點的曲線，帶有輕微飄移的小葉片。
2. **光暈**：色調從邊緣向外淡出，在 `Size` 的 0.47 處完全消失；方形其餘的空間則留給光暈與漣漪。
3. **漣漪**（聆聽時）：兩道細環，生成於邊緣外 4 px，向外擴散至 19 px 並逐漸淡去；音量越大，漣漪出現得越快。
4. **光帶**（思考時）：約 60° 寬的明亮弧線，以每秒 160° 順時針繞行邊緣，並略微溢出邊緣。
5. **字幕**：最多兩行置中文字，16/22.4（系統行為 14/19.6），寬度最多 480。
6. **通話列**：50 px 的膠囊，內含開始（麥克風圖示與文字）或靜音（40 px，靜音時呈按下狀態）、m:ss 計時器，以及結束（40 px，危險色調）。

### 減少動態效果

<Demo name="ReducedMotion" />

### 內容指引

- 字幕要呈現人們實際說出的字詞，不要改寫；讓助理的句子保持簡短，好放進兩行。
- 用系統行來呈現提示與結果，例如 "Tap the orb to start" 與 "Call ended after 1:07."。
- 當螢幕會從遠處使用時，請在光球旁配上一個簡短的狀態詞（"Listening"、"Thinking"）。
- 清楚說明錄下了什麼。若不儲存任何內容，請在控制項旁邊說明。

### 最佳做法

- 讓 `State` 由你後端的真實事件驅動：麥克風開啟時開始聆聽，請求送出時進入思考，音訊播放時開始說話。
- 有真實音量時就推送它；模擬語音只用於原型與示範。
- 在完整的語音畫面上使用 `Size` 200 到 208，在窄螢幕或行內則使用約 144。
- 只有把光球包在一個名稱說明點按動作的按鈕裡，它才會成為點按目標。
- 靜音必須停止你的麥克風，而不只是把光球變成灰色。

### 常見問題

**光球會錄音嗎？** 不會。本套件從不開啟麥克風或喇叭；音量電平是你的應用程式推送的數字。

**可以使用品牌色嗎？** 可以：設定 `Tint`，並以 `Tint2` 作為大理石的第二種顏色。

**為什麼字幕不會把每個字都唸出來？** 螢幕閱讀器只會在 `IsComplete` 被設定時朗讀一次整個輪次，而不是一連串的碎片。

**隱藏時會耗用影格嗎？** 光球離開畫面、隱藏或低於完整動態時，影格迴圈會暫停。

## API 參考

<!-- api: VoiceOrb -->

<!-- api: VoiceTranscript -->

<!-- api: VoiceControls -->

<!-- api: VoiceOrbLabels -->

<!-- api: VoiceControlsLabels -->

<!-- api: VoiceControlsMuteChangedEventArgs -->

<!-- api: IVoiceOrbLevelSource -->

<!-- api: VoiceOrbSimulatedLevels -->

<!-- api: VoiceOrbSession -->

<!-- api: VoiceOrbScript -->

<!-- api: VoiceOrbScriptTurn -->

| 類型 | 值 |
|---|---|
| `VoiceOrbState` | `Idle`, `Listening`, `Thinking`, `Speaking` |
| `VoiceTranscriptSpeaker` | `Assistant`, `User`, `System` |

`VoiceOrbSamples.WeekendTrip` 是示範播放的虛構對話。

## 無障礙

- 光球是一個以其狀態命名的影像（"Voice assistant is idle"、"Listening"、"Thinking"、"Speaking"、"Muted"）；若要使用你自己的文字，請設定 `Label` 或 `Labels`。它不接受焦點。
- 動畫中的字幕文字會對輔助技術隱藏。當輪次定案（`IsComplete`）時，一個禮貌即時區域會把整個輪次讀出一次：使用者的句子在送出時、助理的回答在完成時，系統行則立即讀出。
- 靜音按鈕是切換鈕（靜音時呈按下狀態），名稱為 "Mute microphone" 或 "Unmute microphone"。計時器會以文字命名（"1 minute 5 seconds"）。
- 從通話列開始通話時，焦點會移到靜音；結束通話時，焦點會移回開始。
- 鍵盤焦點會顯示 2 px 的焦點環：位於圓形按鈕的外側，以及開始按鈕的邊緣內側（否則膠囊會把它裁掉）。滑鼠焦點不顯示；懸停樣式只套用於滑鼠或筆。

### 鍵盤

| 按鍵 | 操作 |
|---|---|
| `Tab` / `Shift+Tab` | 通話前：開始。通話中：先靜音，再結束（計時器會被略過） |
| `Enter` / `Space` | 按下有焦點的按鈕；在靜音上則切換麥克風 |

## 動態效果

| 變化 | 完整 | 減少 | 關閉 |
|---|---|---|---|
| 狀態變化 | 十個形狀彈簧（k 55、c 17：ω 7.4、ζ 1.15），約 0.8 s | 每種狀態一個靜態影格 | 每種狀態一個靜態影格 |
| 呼吸、流動、漣漪、光帶 | 持續 | 靜止 | 靜止 |
| 音量與字的脈動 | 在 1 到 3 個影格內膨脹，約 0.5 s 內回落 | 不顯示 | 不顯示 |
| 新的字幕字詞 | 從 4 px 的模糊中升起 3 px，340 ms | 120 ms 淡入 | 直接出現 |
| 更多行 | 區塊向上彈起一行（ω 13.5） | 立即 | 立即 |
| 新輪次 | 舊行在 180 ms 內向上離開 10 px 並模糊；新行在 700 ms 內上升 8 px | 120 ms 淡入 | 直接取代 |
| 通話列切換 | 寬度彈簧（起始 ω 11、ζ 0.93；結束 ω 10.6、ζ 0.9）；兩組內容以縮放 0.92 與 4 px 模糊交替（淡出 120 ms，淡入 160 ms） | 120 ms 淡入，寬度立即變化 | 立即 |
| 靜音圖示 | 在麥克風與關閉麥克風之間彈出切換 | 淡入 | 直接取代 |
| 按鈕填色 | 160 ms | 立即 | 立即 |

## 響應式

- 光球保持其 `Size`；窄螢幕請選擇較小的尺寸。光暈需要整個方形，因此不要裁切它。
- 字幕採用所給的寬度（最多 480），並以平衡的行數換行；在 320 px 的螢幕上仍顯示兩行。
- 通話列的寬度與其內容相同（閒置時 166 px，通話中 144 px），並自行置中。

## 相關元件

| 元件 | 適用時機 |
|---|---|
| `VoiceOrb` | 即時語音對話需要一個臉孔、字幕與通話控制 |
| `DictationButton` | 語音要寫入文字欄位 |
| `SpeechPlayer` | 以播放器朗讀書面回答 |
| `AiMascot` | 助理需要一個角色，而不是抽象的光球 |
