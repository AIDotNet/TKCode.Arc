---
title: Generation stats
summary: 串流模型回覆的即時統計列：首個 token 時間、附迷你折線的速度、輸出 token 與費用逐步累加，結束後定格，並可展開為延遲與 token 費用明細。
source-hash: 599b25d0c851
---

<Demo name="Preview" />

## 何時使用

`GenerationStats` 描述一次模型回覆在串流輸出時與結束後的情況。頂部顯示模型的標記與名稱，以及由圖示和文字組成的狀態：Waiting、Streaming、"Done in 4.18s"、"Stopped at 1.90s" 或 Failed。下方是四個儲存格：首個 token 時間、附迷你折線的每秒 token 速度、輸出 token 與費用。回覆串流輸出時，每個數字都以等寬數字逐步累加；結束後定格在精確值上。Details 會展開明細，其中有一條延遲條和一張附單價與費用的 token 表格。

- **聊天產品中助理訊息的下方**，讓使用者看到一次回覆花了多少錢、有多快。
- **Playground、評測執行器與內部開發面板**，在延遲、吞吐量與費用上比較模型。
- **管理或除錯檢視**，檢查單一請求的 token 用量與快取命中率。

## 何時不用

- **一段對話用了多少上下文**：使用 `ContextWindow`。
- **多個請求或多天的花費**：使用圖表或表格；這個統計列只描述一次回覆。
- **有多個步驟、長時間執行的代理**：使用 `AgentStatus`；這裡量測的是一次模型呼叫。

## 用法

用你既有的串流處理程式為統計列提供資料。請求送出時設定 `StartedAt`，並把 `Status` 設為 `Waiting`；收到第一個資料區塊時設定 `Usage.Ttft`，並把 `Status` 設為 `Streaming`；每個資料區塊都傳入一個帶有持續增加之 `OutputTokens` 的新 `Usage`；最後一個區塊時設定 `Usage.Duration`，並把 `Status` 設為 `Done`。速度、延遲與費用會隨之得出。既未設定 `Status` 也未設定 `Simulation` 時，統計列處於閒置狀態：頭部顯示 Idle，每個儲存格顯示短橫線。

```csharp
// When the request leaves. The provider is your own key for it, written as given into the copied JSON.
stats.Model = new GenerationModel(name: "Claude Sonnet 5.5", provider: "anthropic", id: "claude-sonnet-5-5");
stats.Pricing = new GenerationPricing(input: rates.Input, output: rates.Output, cachedInput: rates.CachedInput);
stats.StartedAt = DateTimeOffset.Now;
stats.Usage = new GenerationUsage { InputTokens = input + cacheRead, CachedTokens = cacheRead };
stats.Status = GenerationStatus.Waiting;

// On every chunk (the first one also sets Ttft and Status = Streaming).
stats.Usage = stats.Usage with { OutputTokens = outputSoFar };

// On the last one.
stats.Usage = stats.Usage with { Duration = DateTimeOffset.Now - started };
stats.Status = GenerationStatus.Done;
```

<Demo name="Streaming" />

### 結構

1. **頂部**：模型的標記（字母徽標，或你在 `GenerationModel.Mark` 中提供的圖形）、名稱（過長時截斷而不換行）、狀態圖示與文字，以及 Details。
2. **儲存格**：First token、Speed（附迷你折線）、Output 與 Cost，以細線分隔。
3. **錯誤列**：請求失敗時，顯示 `ErrorText` 中的原因與 Retry。
4. **明細**：延遲條（首個 token、生成、總計）、token 表格（輸入、快取輸入、輸出、推理、總計）及其單價與費用、一則備註，以及把統計複製為 JSON 的按鈕。

### 狀態

| 狀態 | 頂部顯示 | 儲存格 |
|---|---|---|
| `Idle`，或既無 `Status` 也無 `Simulation` | Idle | 短橫線 |
| `Waiting` | 旋轉圖示與 "Waiting" | First token 從 `StartedAt` 起即時計時 |
| `Streaming` | 強調色旋轉圖示與 "Streaming" | 輸出、速度與費用逐步累加；迷你折線逐步繪出 |
| `Done` | 自行繪出的勾號與 "Done in 4.18s" | 精確值 |
| `Stopped` | 方塊與 "Stopped at 1.90s" | 已抵達的部分 |
| `Error` | 警示圖示與 "Failed" | 已抵達的部分；錯誤列展開 |

### 速度與費用

速度在資料區塊抵達時量測（自首個 token 以來的 token 數除以自那時起經過的時間），因此在兩個資料區塊之間保持不動；回覆結束後，速度為輸出 token 除以生成時間。輸出增加期間，迷你折線每 160ms 加入一個平滑後的讀數，點數達到 48 時減半，因此始終呈現整個執行過程。若你的服務會回報這些值，請傳入 `Usage.TokensPerSecond` 或 `SpeedSamples`。

費用由 `Pricing`（每百萬 token 的美元價格）算出：未快取的輸入按輸入單價，快取輸入按快取單價（未提供時按輸入單價），輸出與推理按輸出單價。若服務供應商會回報費用，請傳入 `Usage.CostUsd` 並省略 `Pricing`；此時 Rate 欄會隱藏。

服務供應商的用量欄位這樣對應：Anthropic 的 `input_tokens` 加上 `cache_read_input_tokens` 是 `InputTokens`，`cache_read_input_tokens` 是 `CachedTokens`；OpenAI 的 `prompt_tokens` 與 `prompt_tokens_details.cached_tokens` 以同樣方式對應，`completion_tokens_details.reasoning_tokens` 是 `ReasoningTokens`。`OutputTokens` 計算的是不含推理的輸出。

### 明細

<Demo name="Breakdown" />

Details 用來展開明細；`IsExpanded="True"` 讓它在首次轉譯時即展開。使用者按下 Details 時會引發 `DetailsToggled`。`Note` 會在明細下方放一則註腳，例如單價的來源。

### 行內

<Demo name="Inline" />

`Variant="Inline"` 是一行安靜的文字，用於聊天訊息下方的空間（"412ms to first token · 88 tok/s · 284 tokens · $0.0049"）。在手機上它會換到第二行而不是捲動，明細則以其下方的面板展開。

### 失敗與停止

使用者停止回覆時，設定 `Usage.Duration`，並把 `Status` 設為 `Stopped`。請求失敗時，把 `ErrorText` 設為簡短而具體的原因，並把 `Status` 設為 `Error`：錯誤列會帶著 Retry 展開，按下 Retry 會引發 `RetryRequested`。`AllowsRetry="False"` 會隱藏它。

### 模擬執行

不設定 `Status`，並給統計列一個 `GenerationStatsSimulation`，即可播放你自己的執行，適用於到達頁、原型或展示：每個 `GenerationStatsRun` 指定模型及其價格、最終的 token 數、首個 token 時間與峰值速度。統計列會等待、以帶爬升與輕微波動的資料區塊串流輸出、定格，然後進入下一個；帶有 `FailAtTokens` 的執行會在該處以其 `ErrorText` 失敗一次，重試後成功。統計列位於畫面上時這些執行循環播放，明細展開時停留，減弱動態效果下立即定格。上面的預覽播放四個真實模型依其牌價計費的執行；token 數與耗時是虛構的。

```csharp
stats.Simulation = new GenerationStatsSimulation { Runs = MyRuns.All };
stats.Note = "Simulated runs at list prices.";

public static class MyRuns
{
    public static IReadOnlyList<GenerationStatsRun> All { get; } =
    [
        new()
        {
            Model = new GenerationModel("Claude Sonnet 5.5", "anthropic", "claude-sonnet-5-5"),
            Pricing = MyPrices.ClaudeSonnet, // your price list, US dollars per million tokens
            InputTokens = 3_682,
            CachedTokens = 2_816,
            OutputTokens = 284,
            TimeToFirstToken = TimeSpan.FromMilliseconds(412),
            PeakTokensPerSecond = 96,
        },
    ];
}
```

在 XAML 中，把 `<arc:GenerationStats.Simulation>` 設為一個 `GenerationStatsSimulation`，其 `Runs` 來自 `{x:Static}`，如預覽的 Code 分頁所示。每個統計列使用自己的模擬（它保存播放狀態）；執行資料可以共用。設定 `Status` 後統計列改為顯示你的資料，把它設回 null 會讓模擬從第一個執行重新開始。

### 回應式

統計列寬度達到 600px 時，四個儲存格排成一列；低於 600px 時排成二乘二的格線，讓速度儲存格保有放迷你折線的空間。低於 440px 時，Details 變為圖示按鈕（名稱仍為 "Show details" / "Hide details"），表格的 Rate 欄隱藏。統計列始終填滿其容器；數字在預留的等寬儲存格中累加，因此旁邊的內容不會移動。

### 自訂文案

`Labels` 包含統計列顯示或朗讀的所有文字。替換整個物件即可在地化或改寫：

```csharp
stats.Labels = new GenerationStatsLabels
{
    DoneIn = time => $"Terminé en {time}",
    FirstToken = "Premier jeton",
    Details = "Détails",
};
```

### 最佳做法

- 模型名稱使用大家熟知的寫法（"Claude Sonnet 5.5"），把 API id 放進 `GenerationModel.Id`。
- 在聊天訊息下方使用行內變體，在評測檢視、Playground 與管理面板中使用卡片。
- 每個資料區塊傳入一個新的 `Usage`，而不是重建統計列；數字會從目前位置平滑過渡。

## API 參考

<!-- api: GenerationStats -->

<!-- api: GenerationModel -->

<!-- api: GenerationUsage -->

<!-- api: GenerationPricing -->

<!-- api: GenerationStatsSimulation -->

<!-- api: GenerationStatsRun -->

<!-- api: GenerationStatsLabels -->

## 無障礙

- 統計列是一個以模型命名的群組（"Claude Sonnet 5.5 stats"）。每個儲存格以其標籤與值命名，因此讀作 "First token, 412ms"。
- Details 是一個會回報展開或收合狀態的按鈕；在較窄的寬度下變為圖示按鈕，但保留名稱 "Show details" 或 "Hide details"。
- 一個禮貌型狀態區域會播報定格的結果（"Claude Sonnet 5.5 done in 4.18s, 284 output tokens at 88 tokens per second, $0.0049"）、失敗與停止，從不播報即時計數。正在播放的模擬只說一次其執行是模擬的，在有人與統計列互動之前保持安靜。
- 狀態從不只依賴顏色：旋轉圖示、勾號、警示圖示或方塊旁邊總有文字。延遲條是裝飾性的；它的數值以文字列在旁邊。
- token 表格的每一列都帶著欄標題命名（"Input: Tokens 866, Rate $2.00 per million tokens, Cost $0.0017"）。
- 指標聚焦不顯示焦點環；鍵盤聚焦顯示 Arc 的焦點環。

### 鍵盤

| 按鍵 | 動作 |
|---|---|
| Tab | 移到 Details，回覆失敗時再移到 Retry，然後移到已展開明細中的複製按鈕 |
| Enter、Space | 展開或收合明細、重試，或把統計複製為 JSON |

## 動態效果

| 變化 | 完整 | 減少 | 關閉 |
|---|---|---|---|
| 數字變化 | 以等寬數字在約 100ms 內平滑過渡到新值；數值下降時直接跳到新值 | 立即 | 立即 |
| 狀態變化 | 舊文字與圖示在 100ms 內淡出，新的在 160ms 內淡入並上升 2px | 短暫交叉淡化 | 立即 |
| 完成 | 勾號自行繪出一次 | 完整繪出 | 完整繪出 |
| 等待、串流輸出 | 旋轉圖示每 800ms 轉一圈 | 靜止 | 靜止 |
| 明細或錯誤列 | 高度以 0.26s 臨界阻尼彈簧變化，內容在 160ms 內淡入淡出；沒有回彈 | 立即 | 立即 |
| Details 箭頭 | 以同一彈簧旋轉 | 立即 | 立即 |
| 模擬執行 | 在畫面上時循環播放；明細展開時停留 | 停在一個已定格的執行上 | 停在一個已定格的執行上 |

即時時鐘只在等待或串流輸出時執行，模擬只在統計列位於畫面上時以虛擬時間推進。

## 相關元件

- `ContextWindow`：一段對話用了模型多少上下文。
- `AgentStatus`：長時間執行之代理的即時狀態。
- `ToolCall`：代理發出的工具呼叫。
