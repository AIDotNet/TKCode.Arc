---
title: LLM cost calculator
summary: 針對 LLM 工作負載的價格試算器：設定每次請求的 token 數、每天的請求數與提示快取，各模型依月費重新排名，長條平滑滑動，價格滾動變化。
source-hash: 6b4c782fba53
---

<Demo name="Preview" />

## 何時使用

`LlmCostCalculator` 在一個面板裡提供某個 LLM 工作負載可信的每月估算。頁首以大型滾動數字顯示所選模型的每月總額。預設、三個滑桿和一個 Cached input 開關設定工作負載，所有模型依月費由低到高排在一份清單中，每列有一根相對於最貴模型的長條。數值改變時，各列滑動到新的名次；選取某一列，總額、明細與價格列就切換為該模型。

- **價格頁或設定頁**：需要呈現一個工作負載在多個模型上的費用。
- **為某項功能挑選模型**：讓使用者看到費用如何隨提示長度、輸出長度、流量與快取變化。
- **內部工具**：在遷移或預算審查前比較各家供應商的價格。

## 何時不用

- **在聊天或輸入框裡選擇模型**，且不需要比較費用：請使用模型選單。
- **單一對話的即時 token 用量**：請使用 `ContextWindow`。
- **依方案計量的實際花費**：本元件只做估算，不做報告。

## 用法

傳入要比較的模型及其牌價（每 1M token 的美元價格），並在 `Description` 中說明價格來源與核對日期；沒有 `Models` 時，面板顯示空狀態（"No models to compare"）：

```csharp
calculator.Models = priceList
    .Select(p => new LlmModelPrice(p.ApiId, p.Name, p.Provider, input: p.Input, output: p.Output, cachedInput: p.CachedInput))
    .ToList();
calculator.Description = $"List prices per 1M tokens, checked {priceList.CheckedOn:yyyy-MM-dd}";
calculator.Note = "List prices, not a quote. Calculated on this device, nothing was sent.";
```

本頁的展示以各廠商的牌價（標準級距，核對於 2026-10-29）比較真實模型。這些是短提示的價格：Claude Haiku 5.5 對超過 100K token 的提示收取較高費用，GPT-6 系列為超過 272K，Grok 4.7 為達到 200K 起，且依整個請求計費；DeepSeek 的價格是其尖峰時段費率（離峰時段為一半），Gemini 3.8 Flash 的推廣價持續到 2026 年底。工作負載預設是虛構的。

`CachedInput` 是快取讀取價格；未設定時，快取的 token 依輸入價格計費。`Mark` 接受模型的標誌，也就是 24 × 24 的描邊圖形，以 16px 繪製；沒有標誌時，該列會在淺色方塊中顯示供應商名稱的第一個字母。

### 你的價格與工作負載

<Demo name="YourPrices" />

`Usage` 是工作負載，採雙向繫結：每次請求的輸入與輸出 token 數、每天的請求數，以及是否開啟快取。滑桿的每一步、每個預設和每次切換快取都會寫入它並觸發 `UsageChanged`。介於滑桿刻度之間的值仍依精確值計算，滑桿顯示最接近的刻度。`SelectedId` 選擇模型（為 null 時選第二個模型；未知的 id 會退回最便宜的模型），透過指標或方向鍵選取某一列時，`SelectedModelChanged` 會帶上該模型及其費用。`SelectedModel` 與 `SelectedCost` 永遠是目前的選擇。

`CacheHitRate` 是開啟 Cached input 時依快取價格計費的輸入比例（預設 0.6，顯示在開關旁邊）。`DaysPerMonth` 設定一個計費月的天數（30）。

### 在伺服器上得到相同的數字

`LlmCostCalculator.ComputeCost` 就是面板使用的演算法，因此你可以把同樣的估算與方案一起儲存，或寫進發票：

```csharp
var cost = LlmCostCalculator.ComputeCost(model, new LlmUsage(16_000, 500, 5_000, Cached: true), cacheHitRate: 0.6);
// cost.Input, cost.CachedInput, cost.Output, cost.Total (dollars a month) and cost.PerRequest
```

### 預設

<Demo name="NoPresets" />

`Presets` 是分段控制項那一列裡的工作負載捷徑（展示傳入 Chat、RAG、Agent 和 Batch）。滑桿與任何預設都不相符時，Custom 分段會自動亮起。未設定預設或傳入空清單時，這一列隱藏。

### 載入、錯誤與沒有模型

`IsLoading` 顯示與清單幾何完全一致的佔位列，並停用各個控制項。`ErrorText` 在清單的位置顯示 "Prices did not load" 和你提供的原因；設定 `AllowsRetry` 會出現一個 Try again 按鈕，它會觸發 `RetryRequested`。沒有 `Models` 或其為空清單時顯示空狀態。清單區域在所有狀態下維持同一高度，沒有清單時各個數字顯示為短橫線。

```csharp
calculator.IsLoading = true;
try
{
    calculator.Models = await prices.FetchAsync();
    calculator.ErrorText = null;
}
catch (HttpRequestException ex)
{
    calculator.ErrorText = ex.Message;
}
finally
{
    calculator.IsLoading = false;
}
```

### 窄版面

<Demo name="Narrow" />

面板讀取自身的寬度，而不是視窗的寬度。寬度低於 600px 時，滑桿改為垂直堆疊。低於 520px 時，總額移到標題下方，每根長條各占一行、位於模型名稱下方（列高從 44px 增加到 58px），明細折成兩欄。每月費用從 $100,000 起改用精簡數字（$420K、$4.2M），所以費用欄不會溢出；總額保留全部位數。面板會填滿其容器，最寬 720px。

### 你自己的文字

`Labels` 包含面板顯示或朗讀的每一個詞；`Title`、`Description` 和 `Note` 可以覆寫大多數應用程式會修改的三段文字：

```csharp
calculator.Labels = new LlmCostCalculatorLabels
{
    Title = "Coût mensuel estimé",
    PerMonth = "/mois",
    Status = (name, cost, rank, count) => $"{name} coûte {cost} par mois, rang {rank} sur {count}.",
};
```

### 內容指引

- 價格是輸入。傳入目前的牌價，並在 `Description` 中說明來源與日期。
- 廠商對長提示加價或在離峰時段降價時，在 `Note` 中寫明價格對應的級距。
- 預設標籤維持一個簡短的詞。

## API 參考

<!-- api: LlmCostCalculator -->

<!-- api: LlmModelPrice -->

<!-- api: LlmUsagePreset -->

<!-- api: LlmCostCalculatorLabels -->

## 無障礙

- 面板是一個以其標題命名的群組。模型清單是一份名為 "Models ranked by monthly cost, lowest first" 的清單，各列是選項按鈕，只有所選模型是一個 Tab 停駐點。
- 無障礙樹依名次列出各列，所以螢幕閱讀器會依顯示的順序朗讀。每列的名稱包含模型、供應商、名次和每月費用。
- 滑桿是 Arc 滑桿，朗讀的是 token 數和請求數，而不是刻度序號。
- 一個禮貌型狀態區域在數字靜止 450ms 後（而不是拖曳的每一步）播報所選模型的每月費用與名次。
- 選取狀態以淺色底和強調色直條呈現，並回報為已選取，從不只依靠顏色。錯誤狀態把圖示與 "Prices did not load" 文字放在一起。
- 大型總額和滾動數字對輔助科技隱藏，因為狀態區域和列名稱已經包含這些數值。
- 指標取得焦點時不繪製焦點環；鍵盤焦點使用 Arc 共用的焦點環。

### 鍵盤

| 按鍵 | 操作 |
|---|---|
| Tab | 依序經過預設、三個滑桿、Cached input 開關、所選模型列，錯誤狀態下再到 Try again |
| 左方向鍵、右方向鍵 | 在預設上切換工作負載；在滑桿上移動一個刻度 |
| Page Up、Page Down、Shift + 方向鍵 | 在滑桿上移動兩個刻度 |
| Home、End | 在滑桿上跳到最小或最大的刻度；在模型清單中選取最便宜或最貴的模型 |
| 上方向鍵、下方向鍵 | 在模型清單中依名次選取上一個或下一個模型，焦點隨之移動 |
| 空白鍵 | 切換 Cached input |

## 動效

| 變化 | 完整 | 減少 | 關閉 |
|---|---|---|---|
| 某列的名次變化 | 透過變換在臨界阻尼彈簧上滑到新位置（約 240ms，無回彈），不重新排版 | 直接跳到 | 直接跳到 |
| 長條的比例變化 | 跟隨同一個彈簧 | 直接跳到 | 直接跳到 |
| 價格變化 | 各位數字在 240ms 內以 ease-out 曲線滾動到新值，使用等寬數字 | 直接跳到 | 直接跳到 |
| 選取變化 | 總額下方的模型名稱與價格列交叉淡化（淡出 100ms，淡入 160ms）；列的淺色底與強調色直條在 160ms 內淡入淡出 | 短暫淡化 | 立即 |
| 載入、清單、錯誤與空狀態 | 清單區域以固定高度在 160ms 內交叉淡化 | 短暫淡化 | 立即 |
| 載入 | 佔位列呼吸閃爍 | 靜止 | 靜止 |

列、長條和數字在移動時是繪製出來的，不參與排版，所以一次讓八列重新排名的拖曳不會觸發排版。面板閒置時不執行任何動作。

## 相關

- `ContextWindow`：單一對話的即時 token 用量。
- `AgentTimeline`：一次代理程式執行的時間、token 與費用。
- `Slider` 與 `SegmentedControl`：面板的輸入控制項。
