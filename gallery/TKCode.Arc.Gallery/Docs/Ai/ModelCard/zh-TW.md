---
title: Model card
summary: 模型詳情卡片：切換模型時，每個價格、上限和分數都在原位捲動到新值，用量滑桿估算每月費用，基準測試長條顯示各模型的排名，比較開關以交叉淡化切換到與任一對手並排的表格。
source-hash: dd18090b77cf
---

<Demo name="Preview" />

## 何時使用

`ModelCard` 描述一個 AI 模型：它接受哪些輸入、能容納多少內容、費用多少，以及與相近對手相比得分如何。切換模型時，每個價格、上限和分數都在原位捲動到新值；用量滑桿即時估算每月費用；比較開關以交叉淡化切換到與任一對手並排的表格。無論切換模型、檢視還是狀態，卡片尺寸都不變。

- **模型選擇器的詳情面板**：在有人選定之前，呈現目前醒目提示的模型能做什麼、費用多少。
- **文件或市集中的模型頁面**：呈現上限、價格，以及與相近對手的快速基準比較。
- **團隊選擇預設模型的設定頁**：需要與備選模型並排核對。

## 何時不用

- **在許多模型中挑選**：呈現一個精簡的選擇器，並為醒目提示的模型顯示一張 `ModelCard`；卡片是詳情檢視，不是清單列。
- **顯示進行中的對話用了多少上下文**：使用 `ContextWindow`。
- **方案定價**：使用定價表；這張卡片顯示的是按 token 計費的 API 價格。
- **包含許多模型的官方排行榜**：基準測試長條只顯示目前模型和兩個對手。

## 用法

為卡片設定 `Model`、一組 `Rivals` 以及要顯示的 `Benchmarks`。所有模型共用同一組對手即可：卡片會自動排除模型本身，因此數值捲動時，基準測試列和排名維持穩定。沒有 `Model` 時，卡片保留其框架，顯示「No model selected」，並把每個值顯示為未列出。

```csharp
var model = new ModelCardModel
{
    Id = entry.ApiId,                 // from your model catalog
    Name = entry.Name,
    Provider = entry.ProviderName,
    Summary = entry.Summary,
    Modalities = ModelCardModalities.Text | ModelCardModalities.Image | ModelCardModalities.Tools,
    ContextWindow = entry.ContextWindow,
    MaxOutput = entry.MaxOutputTokens,
    KnowledgeCutoff = entry.KnowledgeCutoff,
    Pricing = new ModelCardPricing { Input = entry.Input, Output = entry.Output, CachedInput = entry.CachedInput },
    Scores = entry.Scores,            // benchmark id → score; leave out what the vendor does not publish
};

card.Benchmarks = [new ModelCardBenchmark { Id = "gpqa-diamond", Name = "GPQA Diamond", ShortName = "Science" }];
card.Rivals = catalog;                // every model, the current one included
card.Model = model;
card.Note = "List prices and vendor-published scores, checked 2026-10-29.";
```

`ContextWindow` 和 `MaxOutput` 是 token 數（1,000,000 顯示為 1M）。`KnowledgeCutoff` 是 ISO 月份，例如 `"2026-03"`（顯示為 Mar 2026），其他文字依原樣顯示。價格以 `Currency` 計，單位為每百萬 token。缺少的值顯示為「Not listed」。`Note` 是比較表下方的註腳：說明這些資料的來源與核對日期。資料仍是預留位置時開啟 `IsSample`：基準測試說明末尾會加上「Sample scores」，註腳則說明價格和分數是範例。

本頁的展示呈現真實模型及其廠商牌價（標準級距，核對於 2026-10-29），並且只顯示各廠商公布的基準測試分數；Gemini 4 Argon 的價格是推廣價，Terminal-Bench 分數依各廠商自行選擇的推理強度回報。每個 Terminal-Bench 版本都使用獨立的基準測試 id，因此 4.0 的分數從不與 2.1 的分數一起排名；沒有公布分數的模型顯示為「Not published」（展示設定了 `Labels.None` 和 `Labels.NotListed`），而不是零。

### 結構

1. **頁首**：模型標誌放在上色的方塊中（沒有標誌時顯示名稱首字母），旁邊是名稱和供應商，以及比較開關。
2. **簡介**：一句話，預留兩行（窄卡片預留三行），超出部分截斷。
3. **模態標籤**：全部七個（文字、圖片、PDF、音訊、影片、工具、推理）；不支援的標籤為虛線外框並變淡。
4. **規格**：上下文視窗（附一條容量條和可容納的頁數，1M token 約為 1,500 頁）、最大輸出和知識截止時間。
5. **價格**（每百萬 token）：輸入、輸出和快取輸入（只有對手池中有模型提供快取價格時才顯示快取儲存格）。
6. **費用估算**：每月用量滑桿（1M 到 1B token）和估算費用，依 `InputShare` 計費。
7. **基準測試**：切換器、說明文字、模型及其前兩個對手的長條，以及附排名的讀數。
8. **頁尾**：API 模型 id、Copy model id 按鈕，以及用於選擇器的 Use model 按鈕。

### 比較

<Demo name="Compare" />

比較開關或 `View="Compare"` 以交叉淡化切換到與一個對手比較的表格：每項規格、價格、所選用量下的費用和每個基準測試各佔一列。較佳的值以較粗的字重顯示，並附有勾號、供螢幕閱讀器朗讀的「better」以及領先幅度（+2.3、−20%、2x）。表格下方的統計說明誰領先。從選單中選擇對手，或把 `CompareWith` 設為對手的 id。

### 載入與錯誤

<Demo name="States" />

`IsLoading` 顯示一個脈動的骨架畫面，停用比較開關，並朗讀「Loading model details」。`ErrorText` 以訊息取代卡片主體；`CanRetry` 加入一個 Retry 按鈕，它會觸發 `RetryRequested`。兩種狀態都保留頁首和卡片尺寸。沒有 `Model` 時（Empty），頁首顯示「No model selected」，比較開關和基準測試隱藏，Copy model id 被停用。

### 用在模型選擇器中

<Demo name="Picker" />

`CanSelect` 加入一個主要的 Use model 按鈕，按下時以該模型觸發 `SelectRequested`；當它是目前的選擇時設定 `IsSelected`，按鈕會顯示 Selected 和勾號。`SelectLabel` 用於重新命名該按鈕。

```csharp
card.SelectRequested += (_, e) => settings.DefaultModel = e.Model.Id;
card.IsSelected = card.Model?.Id == settings.DefaultModel;
```

### 窄卡片

<Demo name="Narrow" />

卡片在 640px 以內填滿容器，並讀取自身寬度。低於 480px 時，規格和價格變成「標籤與值」的列，費用估算移到滑桿下方，基準測試切換器移到標題下方，頁尾按鈕移到模型 id 下方。低於 420px 時，簡介預留三行；低於 360px 時，每個長條移到名稱下方。寬度達到 520px 時，比較表格附有標籤欄並顯示領先幅度和迷你長條；更窄時，每個標籤位於其兩個值的上方。

### 事件

```csharp
card.ViewChanged += (_, e) => Save("view", e.View);
card.CompareWithChanged += (_, e) => Save("rival", e.Id);
card.BenchmarkChanged += (_, e) => Save("benchmark", e.Id);
card.MonthlyTokensChanged += (_, e) => Save("volume", e.Tokens);
card.IdCopied += (_, e) => Log("copied", e.Id);
```

`View`、`CompareWith`、`Benchmark` 和 `MonthlyTokens` 都是雙向的，也可以直接繫結它們。`MonthlyTokens` 會貼齊最近的級距：1M、5M、10M、25M、50M、100M、250M、500M 或 1B。

### 你自己的用語

`Labels` 包含卡片顯示或朗讀的每個字詞；帶數值的句子是函式：

```csharp
card.Labels = new ModelCardLabels
{
    Compare = "Vergleichen",
    Rank = (rank, count) => $"Platz {rank} von {count}",
};
```

### 減少動態效果

在減少動態效果下，檢視、長條、數字和值都在原位直接變化，骨架畫面保持靜止。關閉動態效果時，所有變化立即完成。

### 文案規範

- 簡介保持一句話，約 90 個字元。
- 為模型和對手提供相同基準測試 id 的分數。沒有分數的列顯示 None，比較表格顯示 Not listed，因此列不會出現或消失。
- 資料仍是預留位置時開啟 `IsSample`；使用真實價格和分數時保持關閉，並在 `Note` 中說明資料來源。
- 只對同一版本的基準測試分數排名：每個版本使用自己的 id。
- 標誌只具裝飾作用；可以把任意 24 × 24 的描邊圖形作為 `Mark` 傳入並設定 `MarkColor`，或省略它以顯示首字母。

### 常見問題

**為什麼隱藏比較檢視時卡片不會變小？** 所有面板都疊放在同一個儲存格中，所以卡片始終與最高的面板一樣高。切換不會移動周圍的內容。

**每月費用如何估算？** 用量中 `InputShare` 的部分依輸入價格計費，其餘依輸出價格計費（預設 75% 為輸入）。

## API 參考

<!-- api: ModelCard -->

<!-- api: ModelCardModel -->

<!-- api: ModelCardPricing -->

<!-- api: ModelCardBenchmark -->

<!-- api: ModelCardLabels -->

<!-- api: ModelCardViewChangedEventArgs -->

<!-- api: ModelCardIdEventArgs -->

<!-- api: ModelCardMonthlyTokensEventArgs -->

<!-- api: ModelCardModelEventArgs -->

## 無障礙

- 卡片以模型名稱命名。每項規格和價格都會連同其標籤一起朗讀。
- 比較表格的每一列以其標籤命名；較佳的值會讀出「better」，因此比較不依賴顏色。
- 切換檢視、對手或基準測試時，一個禮貌的狀態區域會說明發生了什麼變化（「Comparing Claude Opus 5.5 with Gemini 4 Argon」）。Copy model id 會自行朗讀 Copied。
- 基準測試長條對輔助技術隱藏；每一列都可以取得焦點，並以完整的讀數（分數、排名以及與領先者的差距）命名。標誌只具裝飾作用。
- 模態標籤會讀出支援或不支援；不支援的標籤既變淡又使用虛線外框。
- 載入時比較開關被停用，狀態區域朗讀「Loading model details」。錯誤會立即朗讀。
- 隱藏的面板不接收指標輸入，Tab 也不會進入其中。
- 指標取得焦點時不顯示焦點環；鍵盤取得焦點時顯示 Arc 的焦點環。

### 鍵盤

| 按鍵 | 動作 |
|---|---|
| Tab | 依序經過比較開關、用量滑桿、基準測試切換器和每個基準測試列（取得焦點時顯示其讀數），在比較檢視中則經過對手選單，然後是 Copy model id 和 Use model |
| 方向鍵、Page Up、Page Down、Home、End | 在用量滑桿上逐級切換每月用量；估算費用捲動到新值 |
| Space、Enter | 切換比較、複製模型 id 或選擇模型 |
| 左方向鍵、右方向鍵 | 在切換器中的基準測試之間移動 |
| Enter、下方向鍵 | 開啟對手選單；方向鍵在對手之間移動，Enter 選擇，Escape 關閉 |

## 動態效果

| 變化 | 完整 | 減少 | 關閉 |
|---|---|---|---|
| 詳情、比較、載入、錯誤 | 舊面板在 100ms 內淡出，同時新面板在 160ms 內淡入 | 立即 | 立即 |
| 切換模型：價格、上限、費用、頁數、分數 | 數字以等寬數字捲動到新值 | 立即 | 立即 |
| 切換模型：名稱、簡介、截止時間、讀數 | 原位交叉淡化（淡出 100ms，淡入 160ms） | 立即 | 立即 |
| 長條與上下文容量條 | 以臨界阻尼彈簧滑動（0.24s，無回彈） | 立即 | 立即 |
| 首次出現的長條 | 卡片大部分進入可視範圍時從零長出一次，間隔 40ms | 立即 | 立即 |
| 游標停留或聚焦基準測試列 | 該列上色，其他列淡到一半，讀數交叉淡化（160ms） | 立即 | 立即 |
| 載入 | 骨架畫面脈動 | 靜止 | 靜止 |

每個位置都已預留：兩行簡介、七個標籤、每個規格和價格儲存格、三個基準測試列，以及保持行數的讀數，因此卡片尺寸始終不變。

## 相關元件

- `ContextWindow`：顯示進行中的對話用了多少上下文。
- `CopyButton`、`Switch`、`Slider` 和 `SegmentedControl`：卡片內部使用的控制項。
- `DropdownMenu`：對手選單。
