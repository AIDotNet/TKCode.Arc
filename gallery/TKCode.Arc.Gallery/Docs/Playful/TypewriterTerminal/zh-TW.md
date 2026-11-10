---
title: Typewriter terminal
summary: 一個會播放腳本的終端機視窗：按人的節奏逐字輸入每條命令，再逐行列印輸出，等待時游標閃爍。
source-hash: e272c2f47b7c
---

<Demo name="Preview" />

## 何時使用

`TypewriterTerminal` 是一個演出腳本的小終端機視窗。每條命令出現在提示符之後，一個字元一個字元地輸入，節奏略有不均，像真人在敲；稍作停頓，彷彿按下了 Enter，輸出就逐行出現。輸入的位置有一個方塊游標，終端機等待時它會閃爍。腳本可以播放一次或循環，並且會等視窗捲動進入視野後才開始，所以人們能從頭看到。

- **產品或文件頁面的安裝與快速開始部分**：展示命令和它們的輸出。
- **命令列工具的發行說明與更新記錄**：一小段工作階段就能把故事講清楚。
- **開發者工具的首頁插圖**：不用影片也能顯得有生氣。

## 何時不用

- **真實、可互動的命令列**：它只播放腳本，不接收輸入。
- **人們要複製的程式碼**：播放時輸入的文字無法選取。請同時用附複製按鈕的程式碼區塊展示這些命令。
- **很長的記錄檔**：每一行都由控制項繪製。腳本請控制在幾十行以內。

## 用法

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:TypewriterTerminal Title="~/projects/lantern">
        <arc:TypewriterTerminalLine Kind="Command" Text="lantern init my-site" />
        <arc:TypewriterTerminalLine Text="Creating a new site in ./my-site" Tone="Muted" />
        <arc:TypewriterTerminalLine Text="Ready in 2.3s" Tone="Success" />
    </arc:TypewriterTerminal>
</UserControl>
```

`TypewriterTerminal` 是 Special 套件裡的控制項。在 `UseArcControls()` 旁邊用 `UseArcSpecial()` 註冊這個套件。它的內容就是腳本：直接寫在裡面的 `TypewriterTerminalLine`，或在程式碼中加入 `Lines`（`TypewriterTerminalLine.Command("...")` 和 `TypewriterTerminalLine.Output("...", tone)`）。視窗會預留整個腳本的高度，所以播放時頁面不會移動。給它設定 `Height`，文字就會捲動跟隨最新的一行。

### 行與色調

行的 `Kind` 是 `Output`（整行列印，預設）或 `Command`（在 `Prompt` 之後輸入，`Prompt` 預設是 `$`）。`Tone` 給行上色：`Default`，`Muted` 用於進度和註解，`Accent` 用於連結，`Success`、`Warning` 和 `Danger` 用於結果。`Pause` 在某一行之前加一段等待，比如緩慢的建置步驟。只播放一次的腳本在結束時，標題列會顯示一個重播按鈕（`ShowsReplay`，文字為 `ReplayText`）。

<Demo name="Tones" />

### 節奏

`TypingSpeed` 是輸入速度，單位為每秒字元數（28）；每次按鍵的間隔在平均值的約一半到一倍半之間變化。`CommandDelay` 是命令最後一次按鍵與輸出之間的停頓（450 ms），`OutputDelay` 是兩行輸出之間的停頓（90 ms）。`IsLooping` 會在最後的提示符處等待 `LoopDelay`（3 s）後重新開始腳本。

### 用程式碼播放

`AutoPlay`（true）在視窗進入視野時播放腳本。把它設為 false，再自己呼叫 `Play()`。`Pause()` 讓腳本停在目前位置，`Restart()` 從頭開始，`Complete()` 一次顯示所有行。`IsPlaying` 和 `IsFinished` 回報狀態，每一行完成時觸發 `LineCompleted`（帶行號），最後一行出現時觸發 `Completed`。

<Demo name="Controls" />

### 最佳實務

**建議**

- 命令要短，看起來真實，讓輸出說明結果：一行成功訊息勝過十行進度訊息。
- 在緩慢步驟之後的那一行使用 `Pause`，讓時間感覺真實。
- 用 `Title` 給視窗命名：它同時也是無障礙名稱。

**避免**

- 腳本太長，人們沒看完就離開了；請改為循環播放一段短的。
- 把唯一一份安裝命令藏在動畫裡。

### 常見問題

**人們不用等待就能讀完整個腳本嗎？** 可以。輔助技術會一次讀出完整的記錄，按 End 或 Enter 會顯示所有行，關閉動效時整個腳本立即顯示。

**為什麼游標過一段時間就不閃了？** 在不循環的腳本的最後一個提示符處，游標閃爍 12 秒，然後保持恆亮，這樣頁面就不再要求影格了。

## API 參考

<!-- api: TypewriterTerminal -->

<!-- api: TypewriterTerminalLine -->

<!-- api: TypewriterTerminalLineEventArgs -->

## 無障礙

- 視窗是一個可聚焦的群組，由 `Title` 命名，並描述為「terminal」。它的說明文字是帶提示符的完整記錄，所以輔助技術不必等待輸入過程。
- 視窗上的圓點只是裝飾，對輔助技術隱藏。重播按鈕是真正的按鈕，由 `ReplayText` 命名。
- 鍵盤聚焦時，視窗周圍顯示焦點環；指標聚焦不顯示。

### 鍵盤互動

| 按鍵 | 操作 |
|---|---|
| `Tab` | 聚焦終端機 |
| `Space` | 暫停，或繼續播放 |
| `End` / `Enter` | 一次顯示所有行 |
| `Home` | 從頭開始腳本 |

## 動效

- 命令的提示符先出現；0.5 秒後按 `TypingSpeed` 開始輸入，每次按鍵的間隔是平均間隔的 0.55 到 1.45 倍。
- 游標是一個字元寬的方塊。敲鍵時和之後半秒內保持恆亮，然後在終端機等待時以 1.06 秒為週期閃爍。
- 輸出行整行出現；沒有滑動也沒有淡入，就像真正的終端機。
- 文字每行只排版一次；正在輸入的命令是整行被游標處裁切後的畫面，所以輸入時不配置記憶體。只有在腳本播放或游標閃爍時才執行影格，視窗離開視野時腳本會等待。

| 變化 | 完整 | 減弱 | 關閉 |
|---|---|---|---|
| 命令 | 逐個字元輸入 | 到時整條出現 | 全部一次顯示 |
| 輸出 | 逐行出現 | 逐行出現 | 全部一次顯示 |
| 游標 | 等待時閃爍 | 恆亮 | 恆亮 |

## 響應式行為

- 視窗填滿給定的寬度，長行在其中換行，命令在提示符的寬度之後換行。
- 它的高度是該寬度下整個腳本的高度，所以窄螢幕手機會得到更高的視窗；設定 `Height` 可以讓它保持精簡，並讓文字捲動到最新的一行。
- 文字是 13 px 等寬字型；命令要短，這樣在手機上不用換行也放得下。

## 相關元件

- `CodeBlock` 展示人們可以閱讀和複製的程式碼。
- `InstallSnippet` 用帶編號的步驟和程式碼片段，帶開發者從安裝走到第一個結果。

| 元件 | 何時選擇 |
|---|---|
| `TypewriterTerminal` | 一小段工作階段要像真的在發生一樣播放 |
| `CodeBlock` | 人們需要閱讀或複製程式碼 |
| `InstallSnippet` | 快速開始需要人們跟著做並複製的步驟 |
