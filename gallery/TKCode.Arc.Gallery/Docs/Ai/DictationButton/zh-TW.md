---
title: Dictation button
summary: 具備語音輸入的聊天輸入框：麥克風以彈簧動態展開成錄音膠囊，附即時波形、計時器、取消與插入按鈕；說話時文字流入輸入框，辨識器確定之前以淡色顯示。
source-hash: a1eb764b4d8a
---

<Demo name="Preview" />

## 何時使用

`DictationButton` 是為寧可說話也不想打字的人準備的輸入框。按下麥克風，它會向左以彈簧動態展開成錄音膠囊：取消、即時波形、已錄時間與插入按鈕；插入按鈕佔據麥克風原本的位置，下一個動作正好就在指標所在之處。說話時文字流入輸入框，辨識器的猜測以淡色顯示，確定後轉為內文顏色。插入會把文字附加到輸入框，並把焦點交還給輸入框；按 Enter 傳送。

- **聊天與代理的輸入框**：觸控裝置、較長的提示詞，或打字不便的使用者。
- **筆記、留言與客服回覆**：口述能節省時間，傳送前仍可編輯。
- **任何已有傳送動作的輸入框**：需要一個狀態清楚的語音入口時，請使用 `DictationControl`。

## 何時不用

- **全雙工語音對話**（含語音回覆）：請改用附通話控制項的語音代理模式。
- **需要保留的錄音**（語音備忘錄）：聽寫只會插入文字，音訊會被捨棄。
- **命令面板**：一句口令就該立即執行的情境；只有在確定的情況下才在 `SpeechEnded` 中免手動確認。

## 用法

TKCode.Arc 本身不會錄下任何內容。語音來自你為所在平台或語音服務撰寫的 `IDictationRecognizer`：它回報輸入音量與辨識出的文字，其餘由輸入框處理。沒有辨識器（也沒有下文的模擬）時，輸入框只是一個一般欄位：沒有模型標籤、沒有預留位置文字，按下麥克風會提示聽寫無法使用（`DictationErrorCode.Unsupported`）。

```xml
<arc:DictationButton Placeholder="Message" MaxDuration="60" />
```

```csharp
composer.SendRequested += (_, e) =>
{
    // The send button spins until the task completes; the field clears when it succeeds.
    e.Completion = chat.SendAsync(e.Text);
};

composer.Recognizer = new MyPlatformRecognizer();
```

`Text` 是雙向的：輸入、插入的聽寫內容以及傳送後的清空都會改變它。沒有 `SendRequested` 處理常式時，傳送是模擬的。`Model` 是工具列開頭的標籤（為 null，即預設值時隱藏）；請如實傳入輸入框實際傳送到的模型。預覽顯示的是真實模型 Claude Sonnet 5.5，即這則訊息將傳送到的模型；聽寫的文字是虛構的。

### 結構

1. **卡片**：具 1px 邊框的 Surface 卡片，填滿所在的容器。
2. **輸入框**：16/24 的文字，一到六行，超過後捲動，並讓最新的文字保持可見。即時文字接在內文之後：已確定的字詞使用內文顏色，辨識器的猜測使用淡色。
3. **工具列**（36px）：標籤（模型、聆聽時的鍵盤提示，或錯誤訊息）、聽寫位置與強調色的傳送按鈕。
4. **錄音膠囊**：寬 216px、高 36px、內距 4px：28px 的取消、波形、計時器與 28px 的插入。

### 狀態

| 狀態 | 聽寫位置顯示 | 工具列標籤 |
|---|---|---|
| `Idle` | 麥克風 | 模型 |
| `Starting` | 膠囊，長條靜止為圓點 | 鍵盤提示 |
| `Listening` | 附即時波形與計時器的膠囊 | 鍵盤提示 |
| `Processing` | 膠囊，插入按鈕轉圈（"Transcribing"） | 模型 |
| `Denied` | 附麥克風關閉圖示的 Try again | "Microphone blocked in system settings" |
| `Error` | 附警示圖示的 Try again | 簡短原因 |

`MaxDuration`（預設 120 秒，0 表示不限制）時間一到會自動停止聆聽並插入。

### 自訂辨識器

<Demo name="YourRecognizer" />

約定很簡單。`StartAsync` 在音訊開始流入後完成；麥克風遭封鎖時擲回帶 `NotAllowed` 的 `DictationException`。執行期間透過處理常式回報，它們可以從任何執行緒呼叫。權杖被取消表示使用者取消了：此時請釋放麥克風。`StopAsync` 傳回最終文字。

```csharp
public sealed class MyPlatformRecognizer : IDictationRecognizer
{
    public async Task<IDictationSession> StartAsync(DictationHandlers handlers, CancellationToken cancellationToken)
    {
        var engine = await SpeechEngine.OpenAsync(cancellationToken)
            ?? throw new DictationException(DictationErrorCode.NotAllowed);
        engine.Result += (final, interim) => handlers.OnTranscript(final, interim);
        engine.Level += level => handlers.OnLevel(level);         // 0 to 1, as often as you like
        engine.UtteranceEnded += () => handlers.OnSpeechEnd();
        engine.Failed += code => handlers.OnError(code);
        cancellationToken.Register(engine.Dispose);
        return engine;                                            // an IDictationSession: StopAsync returns the text
    }
}
```

用於展示和設計審查時，改為把 `Simulation` 設為一個 `SimulatedDictationRecognizer`：它從不存取麥克風，朗讀你自己的 `Scripts`，每次聽寫一段，每段由 `DictationPhrase` 組成，每個片語可附一個先串流顯示、隨後被更正的聽錯 `Guess`。`Deny` 會像麥克風被封鎖一樣拒絕每次啟動；`StartDelay`、`FinalizeDelay` 與 `WordInterval` 設定它的節奏。沒有腳本時聲音保持靜默。`Recognizer` 一律優先於模擬，把 `Simulation` 設為 null 會停止它。請給每個控制項各自的執行個體。

```csharp
composer.Simulation = new SimulatedDictationRecognizer
{
    Scripts =
    [
        [
            new DictationPhrase("Draft a short reply to the design team", "Draft a short reply to the desk team"),
            new DictationPhrase("and ask if the empty states can ship on Friday."),
        ],
    ],
};
```

### 只用控制項

<Demo name="ControlOnly" />

`DictationControl` 是不含輸入框的麥克風與膠囊，用於你已有的輸入框。自行呈現 `TranscriptChanged`（暫定文字用淡色），附加 `Committed` 的文字，並在 `Cancelled` 時捨棄即時文字。`SpeechEnded` 表示辨識器聽到了一句話的結尾。兩者背後的無介面狀態機 `DictationController` 同樣是公開的。

### 麥克風遭封鎖

<Demo name="Blocked" />

辨識器回報 `NotAllowed` 時，聽寫位置以彈簧動態變成附麥克風關閉圖示的 Try again，工具列說明該到哪裡修正。其他錯誤顯示警示圖示與簡短原因。Try again 會重新開始；Escape 可將它關閉。

### 窄欄

<Demo name="Narrow" />

斷點依卡片本身的寬度而定，而不是視窗：寬度小於 460px 時隱藏鍵盤提示，小於 400px 時錯誤文字縮短，360px 時錄音期間工具列標籤會讓出位置。膠囊始終保持 216px，說話時不會有任何重新排版。

### 減少動態效果

<Demo name="ReducedMotion" />

減少動態效果時，膠囊與輸入框的尺寸直接改變，文字出現時不淡入，波形不再移動：長條隨聲音在原地起伏。關閉動態效果時長條靜止為圓點。

### 自動播放

設定了 `Simulation` 且沒有 `Recognizer` 時，輸入框會自行播放它的腳本（聆聽、插入、傳送），直到卡片內第一次出現指標、按鍵或焦點；卡片不在畫面上時暫停。設定 `AutoPlay="False"` 即可關閉；它是供文件與展示使用，不適用於正式環境。

### 自訂文字

`Labels` 包含輸入框顯示或朗讀的所有文字，包括錯誤訊息與朗讀內容：

```csharp
composer.Labels = new DictationButtonLabels
{
    Dictate = "Dicter",
    Insert = "Insérer",
    Error = code => code == DictationErrorCode.NotAllowed ? "Micro bloqué dans les réglages" : "La dictée a échoué",
};
```

### 最佳做法

- 在產品文案中說明麥克風遭封鎖時該怎麼做；輸入框已經說明了該到哪裡修正。
- 讓標籤如實：傳入輸入框實際傳送到的模型，或傳入 null。
- 不要在正式環境開啟自動播放；它是供文件與展示使用。

## API 參考

<!-- api: DictationButton -->

<!-- api: DictationControl -->

<!-- api: SimulatedDictationRecognizer -->

<!-- api: DictationButtonLabels -->

## 無障礙

- 禮貌型即時區域會朗讀：開始聆聽（以及模擬辨識器不會錄下任何內容）、正在轉寫、插入的字數、取消、傳送，以及每個錯誤與修正方式。流入的文字不會被朗讀。
- 狀態從不只靠顏色表達：麥克風遭封鎖時顯示麥克風關閉圖示與說明，其他錯誤顯示警示圖示與簡短原因；Try again 沿用同一個圖示。
- 只有聽寫位置目前可見的那一面接受指標與 Tab。焦點跟著動作走：麥克風把焦點移到插入，插入把焦點交還到輸入框文字末端，取消交還給麥克風，啟動失敗則移到 Try again。
- 已錄時間使用等寬數字並預留寬度；波形是裝飾性的，對輔助技術隱藏。
- 麥克風、取消、插入與傳送都是具名稱與提示的圖示按鈕（Dictate、Cancel、Insert 或 Transcribing、Send）。
- 暫定文字使用介於 TextMuted 與 TextSecondary 之間的色調，確定後轉為內文顏色，因此在兩種主題下都能分辨，而不依賴色相。

### 鍵盤

| 按鍵 | 動作 |
|---|---|
| Enter、Space | 在麥克風上：開始聽寫；焦點移到插入 |
| Enter | 聆聽時：停止並插入，在卡片內任何位置皆有效 |
| Escape | 聽寫時：取消並捨棄；在 Try again 上：關閉錯誤 |
| Enter | 在輸入框中：傳送。Shift+Enter 換行 |
| Tab | 依序經過取消、插入，然後是傳送；聽寫位置隱藏的那幾面會被略過 |

## 動態效果

| 變化 | 完整 | 減少 | 關閉 |
|---|---|---|---|
| 麥克風變成膠囊 | 寬度向左以彈簧展開到 216px（臨界阻尼，0.24s，無回彈） | 直接改變 | 直接改變 |
| 膠囊變成 Try again | 寬度以彈簧變化到標籤的實測寬度 | 直接改變 | 直接改變 |
| 聽寫位置的各面 | 交叉淡化：淡出 100ms，淡入 160ms | 交叉淡化 | 直接改變 |
| 工具列標籤 | 舊內容 100ms 淡出，新內容 160ms 淡入並上升 2px | 短暫淡化 | 直接改變 |
| 轉寫時的插入按鈕 | 勾號交叉淡化成轉圈（160ms） | 直接改變 | 直接改變 |
| 新字詞出現 | 淡入 140ms；被更正的猜測只讓改變的字詞重新淡入 | 直接出現 | 直接出現 |
| 輸入框變高 | 以彈簧變化到每一新行 | 直接改變 | 直接改變 |
| 波形 | 每 60ms 從右側進入一個取樣，每根長條逐格緩動靠近它；停止時長條在原地靜止為圓點 | 長條隨聲音在原地起伏 | 圓點 |

長條靜止後波形的逐格迴圈即停止，卡片不在畫面上或被隱藏時暫停。逐格零配置：音量存放在固定大小的環狀緩衝區中，所有長條由一個控制項繪製。

## 相關元件

- `AiComposer`：附檔案、工具與模型選單的完整提示詞輸入框。
- `ToolCall` 與 `AgentStatus`：代理如何處理這則訊息。
- `Tooltip`：圖示按鈕的名稱提示。
