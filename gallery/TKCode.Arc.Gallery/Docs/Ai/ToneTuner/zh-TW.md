---
title: Tone tuner
summary: 一張品牌語氣卡片：兩個滑桿（正式程度與詳略）和語氣標籤改寫一封客戶回覆，每個改動過的句子原地交叉淡化，新詞可以對照原稿醒目提示，「複製」取走目前版本。
source-hash: 1659bebbd79f
---

<Demo name="Preview" />

## 何時使用

`ToneTuner` 為 AI 改寫提供幾個旋鈕。一封客戶回覆放在卡片裡，配有 Friendly、Confident、Empathetic 三個語氣標籤，以及正式程度與詳略兩個滑桿。每次調整後稍候片刻，它會向你的模型要求新版本；只有改動過的句子會淡出再淡入，相對原稿新增的詞可以醒目提示，「複製」取走螢幕上的版本。

- **撰寫器或客服工單回覆**：送出前提供 AI 語氣控制。
- **品牌語氣工具**：行銷人員比較同一段文案的不同版本。
- **任何改寫情境**：大家寧可轉幾個旋鈕，也不想寫提示詞。

## 何時不用

- **附提示詞輸入框的自由 AI 編輯**：改用聊天或撰寫器。
- **長篇文件**：卡片的尺寸適合幾段文字的訊息。
- **逐行精確的程式碼或合約差異**：使用專門的差異檢視。

## 用法

把原稿傳給 `Text`，把你的模型呼叫傳給 `Rewrite`。該函式會收到一個 `ToneRewriteRequest`，其中包含原稿、語氣（`Formality`、`Detail`、`Voice`）以及現成的 `Instructions` 字串（它已經要求模型保留事實、數字與姓名），還有一個 `CancellationToken`，當較新的調整取代這次要求時它會被取消。回傳段落分隔與原稿相同的純文字，這樣句子才能對齊，只有改動過的句子會產生動畫。

```csharp
tuner.Text = draft;
tuner.RecipientName = "Sofia Ramirez";
tuner.RecipientSubject = "Re: Duplicate charge on INV-20481";
tuner.Rewrite = async (request, token) =>
{
    var reply = await model.CompleteAsync(request.Instructions, request.Text, token);
    return reply.Text;
};
tuner.Rewritten += (_, e) => Save(e.Text, e.Value);
```

既沒有 `Text` 也沒有 `Simulation` 時，卡片是空的：沒有標題列，訊息為空，語氣標籤、滑桿、「Changes」和「複製」都處於關閉狀態，直到有原稿傳入。你自己的原稿如果沒有 `Rewrite`，會用與模擬相同的簡單規則改寫（見下文），不會送出任何內容。

### 模擬

想在沒有模型的情況下展示卡片（示範、設計審查、新手導覽），就給它一個 `ToneTunerSimulation`：原稿、收件人、要顯示的模型名稱，以及你為每種語氣寫好的版本（`ToneTunerVersion`）。卡片會在 `Latency`（650ms）之後用對應版本回應每種語氣，說明這次改寫是模擬的、沒有送出任何內容，並自動輪流展示各種語氣，直到有人在卡片內按下或聚焦任何元素（`AutoPlay="False"` 讓它保持靜止）。沒有對應版本的語氣會用幾條簡單規則改寫（縮寫、問候語、減少句子、加一句開場白）。你自己的 `Text` 優先於模擬；把 `Simulation` 設為 null 會停止模擬並顯示空卡片。

```csharp
tuner.Simulation = new ToneTunerSimulation
{
    Text = draft,
    RecipientName = "Sofia Ramirez",
    RecipientSubject = "Re: Duplicate charge on INV-20481",
    Model = "Claude Sonnet 5.5",
    Versions =
    [
        new ToneTunerVersion(new ToneSettings(2, 1, ToneVoice.Neutral), formalReply),
        new ToneTunerVersion(new ToneSettings(0, 1, ToneVoice.Friendly), friendlyReply),
    ],
};
```

上方的預覽播放的就是一個模擬，其原稿和各個版本來自圖庫自己的示範資料。讓滑桿不涉及的句子在各版本之間保持完全相同，這樣只有改動過的句子會交叉淡化。

### 你的模型

<Demo name="YourModel" />

原稿在起始語氣（`ToneSettings.Default`：適中、標準、中性）下顯示。其他任何語氣會在最後一次調整後等待 `Debounce`（220ms），再呼叫 `Rewrite`，因此拖曳滑桿時，停下後最多只送出一次要求；語氣再次變化時，仍在執行的要求會被取消，其結果會被忽略。`Rewritten` 回報每個落定的版本、它的語氣以及是否為模擬。

### 語氣

`Value` 是語氣，雙向繫結：`new ToneSettings(formality, detail, voice)`，各檔位為 0 到 2。`ValueChanged` 在每次滑桿步進與標籤按下時觸發，早於防彈跳。標籤會設定它的語氣，並把兩個滑桿移到它的起點；再按一次則回到中性語氣。用 `Presets` 取代這些標籤：

```csharp
tuner.Presets =
[
    new TonePreset { Voice = ToneVoice.Friendly, Label = "Warm", Icon = ArcIcons.Smile, Formality = 0, Detail = 1 },
    new TonePreset { Voice = ToneVoice.Confident, Label = "Direct", Icon = ArcIcons.Zap, Formality = 1, Detail = 0 },
];
```

### 改動

`ShowChanges`（雙向，即 Changes 開關）以圓角強調色背景醒目提示與原稿不同的詞，頁首會計算它們的數量。在程式碼中可以透過 `ChangedWords` 與 `DisplayedText` 讀取同樣的數量與文字。調整期間請保持原稿不變：比較一律針對 `Text`。

### 失敗

<Demo name="Failure" />

當 `Rewrite` 擲出例外時，螢幕上的文字維持不變，狀態顯示「Couldn't rewrite」並附上重試按鈕，即時區域會讀出錯誤訊息。在程式碼中可以用 `Retry()` 再次執行最近的語氣。

### 窄螢幕

<Demo name="Narrow" />

卡片在 640px 以內自適應寬度。窄於 520px 時，兩個滑桿上下堆疊，標籤在一列內等寬排列，狀態隱藏其設定說明。窄於 400px 時，標籤圖示、改動字數與「複製」的文字都會拿掉，以免被截斷；在 360px 下依然完整。

### 自訂文案

`Labels` 包含卡片顯示或朗讀的所有文字，包括滑桿朗讀的檔位名稱：

```csharp
tuner.Labels = new ToneTunerLabels
{
    Formality = "Ton",
    FormalityLevels = ["Familier", "Neutre", "Soutenu"],
    Rewriting = model => $"Réécriture avec {model}",
};
```

### 最佳做法

- 把 `Instructions` 當作系統或使用者指示送給模型；它已經要求保留事實、數字與姓名。
- 回傳與原稿相同的段落分隔，讓未改動的句子保持靜止。
- 在 `Model` 中寫明模型名稱，讓狀態說明是誰在改寫。

## API 參考

<!-- api: ToneTuner -->

<!-- api: ToneSettings -->

<!-- api: TonePreset -->

<!-- api: ToneRewriteRequest -->

<!-- api: ToneTunerSimulation -->

<!-- api: ToneTunerVersion -->

<!-- api: ToneTunerLabels -->

## 無障礙

- 語氣標籤是會回報按下狀態的切換按鈕，位於名為 Voice 的群組中。
- 每個滑桿都有名稱（Formality、Detail），朗讀的是檔位名稱（「Formal」）而不是數字。
- 一個禮貌型即時區域會播報每次落定的改寫及其設定，說明改寫是模擬的（沒有送出任何內容），並回報失敗。
- 醒目提示的詞會在頁首計數（「12 words changed」），因此改動不只靠顏色表達。
- 狀態把圖示與簡短標籤配對：Original draft、Rewriting、Rewritten，或附重試按鈕的 Couldn't rewrite。

### 鍵盤

| 按鍵 | 動作 |
|---|---|
| Tab | 依序經過語氣標籤、兩個滑桿、顯示時的重試按鈕、Changes 開關與複製按鈕 |
| Enter、Space | 按下語氣標籤、切換 Changes、重試或複製 |
| 左、右方向鍵 | 讓取得焦點的滑桿在三個檔位之間步進 |
| Home、End | 讓取得焦點的滑桿跳到第一個或最後一個檔位 |

## 動態效果

| 變化 | 完整 | 減弱 | 關閉 |
|---|---|---|---|
| 改寫落定 | 每個改動過的句子在 100ms 內淡出，所有句子在同一個影格替換（只重排一次，且看不見），新句子在 160ms 內淡入；未改動的句子從不產生動畫 | 立即替換 | 立即替換 |
| 正式程度或詳略變化 | 文字區域以無回彈的彈簧過渡到新的預留高度 | 立即 | 立即 |
| 語氣變化 | 沒有任何移動：文字區域已經為目前正式程度與詳略下最高的語氣預留了高度 | 沒有移動 | 沒有移動 |
| 狀態 | 與輸入在同一個影格切換為 Rewriting，帶柔和的閃光，然後交叉淡化為 Rewritten | 無閃光，短暫淡化 | 立即 |
| Changes 開關 | 醒目提示在 160ms 內淡入淡出 | 立即 | 立即 |
| 自動輪播 | 在螢幕上時每 3.2s 換一種語氣，直到卡片內第一次按下或取得焦點 | 關閉 | 關閉 |

## 相關元件

- `Slider`：單獨使用的正式程度與詳略旋鈕。
- `Switch`：立即生效的設定。
- `CopyButton`：附確認的複製。
- `AgentStatus` 與 `ToolCall`：AI 對話的其他部分。
