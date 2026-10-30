---
title: Image generation
summary: 一張圖像生成卡片：四張圖像以像素馬賽克的方式逐步成形，從粗略的色塊到完整細節，每張都有自己的進度環；選取其中一張即可放大、產生變體或下載。
source-hash: 11e47e45be40
---

<Demo name="Preview" />

## 何時使用

`ImageGeneration` 是一張文字轉圖像的卡片。使用者寫下提示詞，選擇模型和畫面比例，四張圖像便並排以像素馬賽克的方式成形：先是粗略的色塊，接著在一道柔和的光帶下變成越來越細的色塊，最後呈現完整圖像，每張都有自己的進度環。使用者選取其中一張繼續操作：Upscale 把它放大到鋪滿整個網格，並讓一道清晰像素帶掃過畫面；Vary 以它為基礎產生四張新圖；Download 把它交給你的應用程式。

- **產品內的文字轉圖像功能**：行銷素材、商品照片、簡報或文件的插圖。
- **任何需要比較幾個候選再繼續其中一個的流程**（放大、變體、下載）。
- **在後端就緒之前為圖像模型整合製作原型**，使用一個模擬。

## 何時不用

- **完整的畫布編輯器**，帶圖層、遮罩和局部重繪：請以繪圖表面為基礎來建構。
- **只顯示一張帶載入狀態的圖像**，沒有生成控制項：用圖像自己的預留位置即可。
- **歷史結果的圖庫**：卡片只保存目前這一批；請搭配你自己的素材清單。

## 用法

沒有產生器時，卡片會播放它的 `Simulation`；兩者都沒有時，「生成」無法使用。`ImageGenerationSimulation` 保存卡片起始的 `Prompt`、可選的 `Models` 與 `Suggestions`，以及一個為提示詞繪製圖片的 `Painter`（`ImageGenerationPainter`）；卡片在兩到三秒內逐影格回報進度，每個圖塊從繪製的預覽中逐步呈現，並在第一次捲動進入可視範圍時自動生成一次。不會下載或傳送任何內容。你自己設定的 `Prompt`、`Models` 與 `Suggestions` 優先於模擬中的值，把 `Simulation` 設為 null 會停止它的執行。

```csharp
card.Simulation = new ImageGenerationSimulation
{
    Prompt = "A quiet mountain lake at golden hour",
    Models = [new ImageGenerationModel { Id = "your-model", Name = "Your image model", Mark = ArcIcons.Sparkles }],
    Suggestions = ["A neon city street after rain at night"],
    // Opaque BGRA bytes, width × height × 4; the same prompt and seed should paint the same picture.
    Painter = (prompt, seed, width, height) => MyPainter.Paint(prompt, seed, width, height),
};
```

本頁的示範從 `ImageGenerationDemoData` 繪製黃金時刻的湖泊、雨後的城市街道、陽光窗台上的杯子或粉彩色的房間。圖庫其他 AI 示範所用的模型目錄裡沒有圖像生成模型，因此這些圖像模型保留虛構的名稱。

用 `Generator` 把它連接到你的圖像服務。每個格子呼叫一次，平行執行，用於 Generate、Vary 和 Retry：

```csharp
card.Generator = async (request, context) =>
{
    var job = await images.CreateAsync(request.Prompt, request.Model.Id, request.Aspect.Width, request.Aspect.Height,
        seedFrom: request.VariationOf?.Id, context.CancellationToken);
    await foreach (var step in job.ProgressAsync(context.CancellationToken))
    {
        context.ReportProgress(step.Fraction, step.Preview);
    }
    return new ImageGenerationImage
    {
        Id = job.Id,
        Source = await job.DownloadBitmapAsync(context.CancellationToken),
        Description = request.Prompt,
        Width = request.Aspect.Width,
        Height = request.Aspect.Height,
    };
};
```

以 0 到 1 回報進度。提供預覽點陣圖時，馬賽克會從你的圖像中成形；沒有預覽時，一個安靜的預留網格會閃爍，直到圖像到達後淡入；完全不回報時，進度環會自行估算。擲出例外即把該格子標記為失敗；Stop 會取消 `context.CancellationToken`。`Upscaler` 對選取的圖像以同樣的方式運作。

### 結構

1. **提示詞**：一個文字欄位；Cmd/Ctrl + Enter 開始生成。
2. **標籤**：模型（選取的顯示名稱，其餘收合為帶提示的 16px 標誌）、畫面比例（各帶一個小輪廓）以及 Generate，批次執行時它顯示為 Stop。
3. **網格**：四個格子維持本批次的畫面比例，高度不超過 420px。每個執行中的格子有一個玻璃質感的小膠囊，內含進度環和百分比；選取的格子有強調色外框和一個勾號徽章。
4. **頁尾**：一行狀態，接著是 Upscale、Vary 和 Download。

### 選取、放大、變體與下載

點按一張完成的圖像（或聚焦後按 Enter 或 Space）即可選取；再按一次取消選取。Upscale 會把選取的圖像從它的格子放大到整個網格，並讓一道清晰像素帶由左向右掃過：前方柔和，後方清晰。按 Escape 或角落的按鈕回到網格。Vary 以選取的圖像作為 `VariationOf` 產生四張新圖。Download 觸發 `DownloadRequested`，附帶圖像和一個由提示詞、圖像編號與尺寸組成的檔名；請在那裡儲存它。

失敗的格子顯示警示圖示和 "Failed"，被停止的格子顯示方塊和 "Stopped"；點按任一個都會重新生成該圖像。

### 直式畫面比例

<Demo name="Portrait" />

每一批都維持自己的畫面比例：選擇另一個比例標籤只對下一次生成生效。網格以彈簧動畫過渡到新比例，並把高度限制在 420px，因此直式批次依然精簡。

### 你自己的產生器

<Demo name="YourGenerator" />

這裡用卡片的模擬代替圖像服務（`Card.Simulation.Generate`），第 3 張圖在第一次嘗試時失敗。設定了產生器後 `AutoGenerate` 預設關閉；把它設為 `True`，卡片會在捲動進入可視範圍時自動生成一次。

### 監聽卡片

<Demo name="Events" />

`SelectionChanged` 回報選取的圖像（放大完成後回報放大後的圖像），`SelectedImage` 保存它，`DownloadRequested` 攜帶圖像和檔名。

```csharp
card.DownloadRequested += async (_, e) =>
{
    var file = await TopLevel.GetTopLevel(card)!.StorageProvider.SaveFilePickerAsync(new() { SuggestedFileName = e.FileName });
    if (file is not null && e.Image.Source is { } bitmap)
    {
        await using var stream = await file.OpenWriteAsync();
        bitmap.Save(stream);
    }
};
```

### 窄容器

<Demo name="Narrow" />

卡片填滿容器，最寬 640px。寬度小於 420px 時，比例標籤移到第二列；小於 520px 時，頁尾改為上下排列：狀態在上，下面是三個等寬按鈕。

### 你自己的文案與模型

`Models` 和 `AspectRatios` 取代標籤（`ImageGenerationModel` 有 id、名稱和選用的標誌；`ImageGenerationAspect` 有 id、顯示文字和一次生成傳回的像素尺寸）。`Suggestions` 取代空網格中提供的提示詞。`Labels` 保存卡片顯示或播報的每一個詞：

```csharp
card.Labels = new ImageGenerationLabels
{
    Generate = "Créer",
    Stop = "Arrêter",
    Generating = (count, model) => $"Création de {count} images avec {model}",
};
```

### 減少動態效果

減少動態效果時，進度環、分步的馬賽克（它跟隨進度而非時間）以及最終狀態保留；光帶、放大掃描、懸停縮放和展開位移會被移除。關閉動態效果時一切立即變化。

### 文案規範

- 模型名稱只寫你的服務實際提供的；示範中的名稱是虛構的。
- 傳回真實的寬高，讓頁尾和檔名顯示真實尺寸。
- 在 `Description` 中描述每張圖像（通常就是提示詞）：螢幕閱讀器會在格子上朗讀它。

### 最佳做法

- 把 `context.CancellationToken` 傳給每一次呼叫，這樣 Stop 才會真正停止工作。
- 盡早回報預覽：馬賽克從第一步起就會顯示真實的顏色。
- 一次呼叫傳回四張圖的服務，可以快取同一個請求，再依序號分別取出每一張。

### 常見問題

**批次執行時使用者能修改提示詞嗎？** 可以；修改會在下一次 Generate 時生效。

**卡片會保留之前的批次嗎？** 不會。它只保存目前批次；監聽 `SelectionChanged` 來保留結果。

## API 參考

<!-- api: ImageGeneration -->

<!-- api: ImageGenerationModel -->

<!-- api: ImageGenerationAspect -->

<!-- api: ImageGenerationImage -->

<!-- api: ImageGenerationRequest -->

<!-- api: ImageGenerationContext -->

<!-- api: ImageGenerationLabels -->

<!-- api: ImageGenerationSelectionChangedEventArgs -->

<!-- api: ImageGenerationDownloadEventArgs -->

<!-- api: ImageGenerationSimulation -->

<!-- api: ImageGenerationPainter -->

## 無障礙

- 模型標籤和比例標籤各是一個單選群組，每組只有一個 Tab 停駐點；收合的模型標籤仍以名稱作為無障礙名稱，並顯示在提示中。
- 每個格子都是一個切換按鈕，會回報是否被選取；它的名稱朗讀圖像編號和描述，或說明正在生成、已失敗或已停止。
- 一個禮貌的即時區域會播報開始、完成、失敗、放大和下載，並說明結果是否為模擬。
- 狀態從不只靠顏色表達：失敗配有危險圖示和 "Failed"，停止配有方塊和 "Stopped"，選取配有強調色外框和勾號徽章。
- 圖像展開時，其餘格子不接收指標或焦點。

### 鍵盤

| 按鍵 | 操作 |
|---|---|
| Cmd/Ctrl + Enter | 從提示詞欄位開始生成 |
| 方向鍵 | 在模型標籤或比例標籤之間移動（並選擇），以及在網格中的圖像之間移動 |
| Enter、Space | 選取或取消選取目前聚焦的圖像；重試失敗或已停止的圖像 |
| Escape | 關閉展開的圖像並回到網格 |
| Tab | 提示詞、模型、比例、Generate、選取的圖像，接著是 Upscale、Vary 和 Download |

## 動態效果

| 變化 | 完整 | 減少 | 關閉 |
|---|---|---|---|
| 圖像成形 | 4 欄色塊依帶種子的順序從中心淡入，接著 8、16、32、64 欄依序落在其上，最後交叉淡入完整圖像；同一個進度值驅動馬賽克、進度環和百分比 | 同樣的步驟，由進度驅動 | 同樣的步驟 |
| 執行中的格子 | 一道柔和的光帶沿對角線掃過成形中網格的色塊，隨著細節出現而淡去，每趟 1.6s，離開螢幕時暫停 | 無 | 無 |
| 完成的格子 | 懸停時畫面放大 2%；按下時格子縮到 98.5% | 無 | 無 |
| 放大 | 格子以無回彈的 240ms 彈簧從它的格位長到整個網格，其餘格子淡出；一道清晰像素帶由左向右掃過 | 立即，無掃描 | 立即 |
| 新批次的比例 | 網格以彈簧動畫過渡到新比例；容器尺寸變化則直接切換到新尺寸 | 立即 | 立即 |
| 按鈕文字 | 疊放在同一格中交叉淡化（淡出 100ms，淡入 160ms），按鈕寬度不變 | 交叉淡化 | 立即 |
| 頁尾狀態 | 舊的一行在 100ms 內淡出，新的一行在 160ms 內淡入並上移 2px | 交叉淡化 | 立即 |
| 模型標籤 | 選取的標籤依測得的寬度以彈簧展開名稱 | 立即 | 立即 |

## 相關元件

- `ContextWindow`：一次工作階段用了模型視窗的多少。
- `ToolCall`：代理程式發出的工具呼叫。
- `AgentStatus`：執行中代理程式的即時狀態。
