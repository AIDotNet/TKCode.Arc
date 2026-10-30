---
title: AI translate
summary: 一張訊息卡片：選擇一種語言後，AI 譯文會在原處串流出現，逐句覆蓋變暗的原文，並附有偵測到的語言標籤、「顯示原文」開關和狀態列。
source-hash: 0c94b955a2d4
---

<Demo name="Preview" />

## 何時使用

`AiTranslate` 在產品內依需求翻譯一則訊息或一段文字。文字上方是作者署名和一個標明文字語言的標籤；下方是 Translate 選擇器、Show original 開關，以及以模型標誌開頭的狀態列。選擇一種語言後，畫面上的文字會變暗，譯文在原處串流出現：每開始一個新句子，就撤下一句舊文字，同時卡片高度以彈簧動畫配合內容。完成的譯文會被保留，因此「顯示原文」和切換回來時都會立即交叉淡化。

- **以另一種語言撰寫的訊息、留言、評論或客服單。**
- **多個地區的讀者會以自己的語言開啟的版本說明或公告。**
- **任何希望 AI 翻譯顯得內嵌、可還原的地方**，原文只需點一下。

## 何時不用

- **翻譯整個頁面或應用程式介面**：請使用你的在地化框架。
- **含標題和清單的長文件**：卡片只呈現一段純文字。
- **即時字幕或語音**：請使用即時串流元件。

## 用法

為卡片設定 `Text`，並把 `Translator` 接到你的模型。翻譯函式會收到一個 `AiTranslateRequest`（文字、來源語言、目標語言和取消權杖），並以文字增量的非同步串流傳回譯文。可以在模型串流輸出時逐段 yield，也可以一次 yield 整段譯文：不論哪種方式，卡片都會把收到的內容以每 34ms 一塊、每塊 3 到 6 個字詞的節奏放出。

```csharp
card.Text = message.Body;
card.Translator = TranslateAsync;

async IAsyncEnumerable<string> TranslateAsync(AiTranslateRequest request)
{
    // Pass the token on: it is cancelled when the reader picks another language or shows the original.
    await foreach (var delta in model.StreamAsync(request.Text, request.From, request.To, request.CancellationToken))
    {
        yield return delta;
    }
}
```

沒有翻譯函式時，如果傳入現成譯文，卡片會用 `Translations` 模擬串流輸出。既沒有 `Text` 也沒有 `Simulation` 時，卡片是空的：沒有署名列和語言標籤，段落為空，翻譯選擇器處於關閉狀態，直到有文字傳入。

### 模擬

想在沒有翻譯函式的情況下展示卡片（示範、設計審查、新手導覽），就給它一個 `AiTranslateSimulation`：訊息、署名、要標示的模型以及預先準備好的譯文。選擇一種語言後，卡片會在短暫等待後原地串流輸出現成譯文（沒有譯文的語言會顯示失敗狀態），狀態會說明這是模擬、沒有送出任何內容，並且卡片會自動依序播放 `Tour`，直到有人在卡片內按下指標或按鍵。你自己的 `Text` 優先於模擬；把 `Simulation` 設為 null 會停止模擬並顯示空卡片。

```csharp
card.Simulation = new AiTranslateSimulation
{
    Text = "La version 4.2 est disponible.",
    Author = new AiTranslateAuthor { Name = "Camille Laurent", Role = "Product lead" },
    ModelName = "Gemini 3.8 Flash",
    Translations = new Dictionary<AiTranslateLanguage, string>
    {
        [AiTranslateLanguage.English] = "Version 4.2 is out.",
        [AiTranslateLanguage.German] = "Version 4.2 ist da.",
    },
    Tour = [AiTranslateLanguage.English, AiTranslateLanguage.German],
};
```

上方的預覽播放的就是一個模擬，其版本說明和譯文來自圖庫自己的示範資料。

### 從你的模型串流輸出

<Demo name="Streaming" />

未設定 `SourceLanguage` 時，卡片會檢查常見字詞來粗略偵測來源語言，標籤顯示「Detected · German」。失敗（翻譯函式擲回例外）時，先前的文字會還原，顯示「Couldn't translate」和 Retry，例外訊息保留為狀態列的提示，並引發 `TranslationFailed`。已取消的要求不會引發它。

### 現成譯文

<Demo name="Ready" />

把預先準備好的譯文傳給 `Translations`，卡片會以模擬串流輸出它們。缺少的語言會顯示失敗狀態。已知來源語言時請設定 `SourceLanguage`，標籤會顯示「Written in Spanish」。把 `Value` 設為來源語言以外的語言時，卡片一顯示就開始翻譯。沒有 `Author` 時卡片不顯示署名列，語言標籤仍保留。

### 跟隨偏好語言

<Demo name="Thread" />

`Value` 是畫面上的語言（雙向）；null 或來源語言代表原文。每則訊息呈現一張卡片，並以共用的偏好語言設定每張卡片的 `Value`。只有讀者選擇語言或切換「顯示原文」時才會引發 `ValueChanged`，因此可以在其中記住讀者的選擇。「顯示原文」會記住上一次的譯文，供「顯示譯文」使用。

### 窄螢幕

<Demo name="Narrow" />

寬度小於 470px 時，卡片內距變為 16px，狀態列移到控制項下方，與內文靠左對齊；小於 380px 時，標籤只顯示語言名稱。名字和附加資訊過長時以刪節號截斷。

### 結構

1. **署名列**：顯示姓名縮寫的頭像、名字及其下方的職稱和時間，最右側是語言標籤。
2. **內文**：畫面上語言的文字。譯文串流輸出時，新文字在前，剩下的舊文字變暗接在後面。
3. **控制項**：Translate 選擇器（翻譯後顯示目前語言的原生名稱）、Show original 開關，以及失敗後出現的 Retry。每個控制項高 32px，並預留其最長標籤的寬度。
4. **狀態列**：模型標誌加一個標籤：閒置時是模型名稱，接著是「Translating to German」「Translated from French」「Showing original」，或警告圖示加「Couldn't translate」。

### 狀態

| `State` | 內文 | 狀態列 |
|---|---|---|
| `Original` | 原本的文字 | 模型名稱，之後是「Showing original」 |
| `Translating` | 譯文在變暗的文字上串流出現 | 「Translating to German」 |
| `Translated` | 譯文 | 「Translated from French」 |
| `Failed` | 先前的文字，已還原 | 警告圖示和「Couldn't translate」，附 Retry |

### 保存譯文

譯文完整送達後、卡片快取之前會引發 `Translated`。快取依卡片、依文字保存；設定新的 `Text` 會清除快取並顯示新的原文。要跨工作階段保留譯文，請在 `Translated` 中儲存，再透過 `Translations` 傳回。

### 自訂文字和標誌

`Labels` 包含卡片顯示或朗讀的每個字詞，包括語言名稱。`ModelName` 是閒置時的狀態，`ModelMark` 是每個狀態開頭的 24 × 24 描邊圖形；請傳入你的服務供應商的標誌。

```csharp
card.ModelName = "Gemini 3.8 Flash";
card.Labels = new AiTranslateLabels
{
    Translate = "Traduire",
    ShowOriginal = "Voir l'original",
    TranslatingTo = language => "Traduction en " + language,
};
```

### 自動播放

播放模擬的卡片會依序切換到模擬的 `Tour` 中的語言（預覽中是 English、Deutsch、日本語 和 Português），再回到原文，並循環播放；沒有 `Tour` 時會依序播放每種有譯文的語言。卡片離開畫面時暫停，在卡片內第一次按下指標或按鍵後永久停止。`AutoPlay` 可為任何卡片開啟或關閉（你自己的卡片會依序播放選擇器中的每種語言）；減少動態效果時只翻譯一次就停止。`IsSimulated` 會在朗讀中加上「Simulated, nothing was sent」（沒有翻譯函式或正在播放模擬時預設開啟）。

### 內容指南

- 每張卡片放一段文字或一則訊息。討論串中每則訊息呈現一張卡片。
- 標籤寫來源語言；選擇器以每種語言的原生名稱列出語言，來源語言標為 Original。
- 在 `ModelName` 中寫你實際呼叫的模型。

### 最佳做法

- 一律把要求的取消權杖傳給網路呼叫，這樣切換語言時舊要求就會停止。
- 收到增量就 yield，不要等整段譯文完成。
- 已知來源語言時請提供 `SourceLanguage`；偵測只是為短訊息準備的粗略檢查。

### 常見問題

**句子怎麼切分？** 在後面接空白的 `.`、`!` 和 `?` 處切分，也在 `。`、`！` 和 `？` 處切分。4.2 這類小數保持完整。

**串流途中切換語言會浪費要求嗎？** 會取消它：權杖被取消，先前的文字還原，接著新語言開始串流輸出。

## API 參考

<!-- api: AiTranslate -->

<!-- api: AiTranslateAuthor -->

<!-- api: AiTranslateRequest -->

<!-- api: AiTranslateSimulation -->

<!-- api: AiTranslateLabels -->

## 無障礙

- 卡片的無障礙名稱是「Message from」加作者名稱；內文的名稱是畫面上的文字，譯文串流輸出時回報為忙碌。
- 開始、完成、顯示原文和失敗都會在禮貌型狀態區域中朗讀；模擬時，朗讀會說明沒有傳送任何內容。
- 語言選單是一組單選列，目前語言處於選取狀態。Show original 開關會回報按下狀態，名稱切換為 Show translation。
- 狀態從不只靠顏色表達：模型標誌加標籤，或警告圖示加「Couldn't translate」。
- 每個控制項至少高 32px，標籤預留其最長版本的寬度，指標下的內容不會移動。

### 鍵盤

| 按鍵 | 操作 |
|---|---|
| Tab | 依序經過 Translate 選擇器、Show original 開關，失敗後還有 Retry |
| Enter、Space、Arrow down | 在選擇器上開啟語言選單，定位到目前語言 |
| 方向鍵、Home、End | 在語言間移動；輸入字母可跳到對應語言。清單會循環 |
| Enter、Space | 選擇反白的語言並關閉選單。選擇剛失敗的語言會重試 |
| Escape | 關閉選單，焦點回到選擇器 |

## 動態效果

| 變化 | 完整 | 減少 | 關閉 |
|---|---|---|---|
| 一塊文字送達 | 每 34ms 一塊，每塊 3 到 6 個字詞（日文約 10 個字元），各自在 140ms 內淡入；沒有模糊、滑動或彈簧 | 立即 | 立即 |
| 開始翻譯 | 畫面上的文字在 200ms 內變暗到 45% | 立即 | 立即 |
| 新句子開始 | 撤下一句變暗的舊句子 | 相同 | 相同 |
| 已快取的譯文或原文 | 一次交叉淡化：舊文字在 100ms 內離開，新文字在 160ms 內淡入並上升 2px | 立即 | 立即 |
| 內文高度 | 以臨界阻尼彈簧（0.24s，無回彈）跟隨文字；視窗大小改變時直接跳到新高度 | 立即 | 立即 |
| 狀態、選擇器和開關的標籤 | 在預留寬度內交叉淡化（100ms 淡出，160ms 淡入） | 短暫淡化 | 立即 |
| 自動播放 | 循環 | 翻譯一次後停止 | 翻譯一次後停止 |

## 相關

- `ToolCall`：同一段對話紀錄中代理發起的工具呼叫。
- `DropdownMenu`：錨定在觸發器上的一組聚焦操作。
- `Badge`：類似語言標籤的小標籤。
