---
title: Design to code
summary: 截圖轉程式碼卡片：掃描線找出拖入的截圖或設計檔畫框中的版面區域，接著程式碼在旁邊串流寫出，並與區域一一對應，切換一個分頁即可看到即時預覽。
source-hash: 0ad3fab6e8cd
---

<Demo name="Preview" />

## 何時使用

`DesignToCode` 是「截圖轉程式碼」功能的卡片。使用者把介面截圖拖到設計那一側，或從設計檔匯入一個畫框。一條掃描線掃過截圖，
它找到的每個版面區域都以帶標籤的方框出現。接著程式碼在旁邊依區域逐段串流寫出：正在撰寫的區域在截圖上反白，對應的程式碼行帶有淡淡的底色。
指向方框或標籤會點亮它的程式碼行，指向某一行程式碼也會點亮它的方框。切換一個分頁，「預覽」就會呈現可以實際操作的結果。

- **設計轉程式碼功能**：上傳一張截圖或選擇設計檔中的一個畫框，取得一個元件。
- **展示 AI 視覺步驟**：當模型辨識出了什麼很重要時，例如產生之前的版面解析。
- **行銷與新手導覽**：一眼說明程式碼產生產品在做什麼。給卡片一個 `Simulation`，它就會在可見期間循環播放一個設計及其程式碼。

## 何時不用

- **沒有產生步驟的一般檔案上傳**：使用檔案上傳元件。
- **只呈現完成的程式碼而不需要來源設計**：使用 `CodeBlock`。
- **具備檔案、分頁與終端機的完整編輯器**：建立一個工作區版面，而不是把這張卡片撐大。

## 用法

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:DesignToCode />
</UserControl>
```

`DesignToCode` 是 Special 套件中的 `TemplatedControl`。在 `UseArcControls()` 旁邊以 `UseArcSpecial()` 註冊這個套件。
卡片會填滿容器的寬度；請把它放在頁面或對話方塊中，寬度保持在 360 到 760px 之間。

既沒有 `Generator` 也沒有 `Simulation` 時，卡片以拖放區開啟：拖入截圖、選擇檔案或匯入設計檔。接上 `Generator` 把設計轉成程式碼，或者給卡片一個 `Simulation` 來播放。

### 模擬

`DesignToCodeSimulation` 是一個設計以及模型可能為它寫出的程式碼：`Source`（一張影像，或以程式碼繪製的草圖）、掃描找到的
`Regions`、分段的程式碼 `Parts`（每個 `DesignToCodeSimulationPart` 標註它實作的區域），以及可選的署名模型 `Model`、`FileName`、
模擬匯入時帶入的 `FrameName`、`PreviewFactory`，和循環時對預覽呼叫的 `PreviewAction`。沒有 `Generator` 時，`Source` 未設定期間
卡片顯示它的設計，「Use sample」會把它帶回來。這次執行是模擬的：不會上傳或傳送任何內容，朗讀的提示也會說明這一點。卡片在畫面上時
會循環播放（產生、停留在程式碼上、顯示預覽、重新開始），直到卡片內第一次出現指標按下或按鍵。把 `Simulation` 設為 null 會停止模擬
執行並回到拖放區。

```csharp
card.Simulation = new DesignToCodeSimulation
{
    Source = new DesignToCodeSource("pricing-card.png", screenshot),
    Regions = [new DesignToCodeRegion("price", "Price", 7.5, 42.5, 52, 14.5)],
    Parts =
    [
        new DesignToCodeSimulationPart("export function PricingCard() {\n  return (\n"),
        new DesignToCodeSimulationPart("    <p className=\"text-4xl\">$24</p>\n", "price"),
        new DesignToCodeSimulationPart("  );\n}\n"),
    ],
    Model = new AgentModel("Your model"),
    FileName = "PricingCard.tsx",
};
```

設計也可以用程式碼繪製，而不是提供影像：為來源設定 `Sketch`（一個 `DesignToCodeSketch`，以卡片的 `DesignToCodeSketchPalette`
在設計的矩形中繪製，因此會跟隨淺色與深色主題）以及它的 `SketchSize`。本頁的示範就是這樣繪製一張小型價格卡片，所有內容都來自
`DesignToCodeDemoData`；署名的模型是真實的（核對於 2026-10-29），方案與價格是虛構的。

### 結構

1. **設計面板**：標題列顯示設計的名稱、「Sample」或「Design file」標籤以及「移除」按鈕；下方是帶區域方框與標籤的截圖，
   沒有設計時則是拖放區。
2. **區域方框與標籤**：每個區域一個 1px 的強調色外框，以及一個與其頂邊對齊、位於其左側留白中的小標籤（沒有空間時放在方框內）。
   標籤是一個按鈕，名稱類似「Price: show its code」。
3. **輸出面板**：標題列顯示檔名、模型的標誌與名稱、「程式碼 / 預覽」切換以及「複製」；下方是帶行號的程式碼，或即時預覽。
4. **底列**：狀態（圖示、簡短標籤、檔案以及類似「52 lines in 3.4s」的附加資訊）、「匯入設計檔」以及主要操作，
   主要操作依序顯示 Generate code、Stop、Regenerate 或 Try again。

### 一次執行

`Phase` 跟著執行進度：沒有設計時為 `Empty`，有設計時為 `Ready`，接著是 `Analyzing`、`Writing` 與 `Done`，或是
`Stopped` 與 `Error`。Generate code（或 `Generate()`）開始一次執行，並把輸出切回「程式碼」。Stop、Escape 或 `Stop()`
結束正在分析或撰寫的執行；已經寫出的程式碼會保留。`Code` 保存畫面上的程式碼，`Completed` 回報完成的程式碼以及行數與耗時。

| 階段 | 狀態 | 主要操作 |
|---|---|---|
| Empty | Add a design to start | Generate code（無法使用） |
| Ready | Ready pricing-card.png | Generate code |
| Analyzing | Analyzing layout pricing-card.png · 3 regions | Stop |
| Writing | Writing PricingCard.tsx · 5 regions | Stop |
| Done | Done PricingCard.tsx · 52 lines in 3.4s | Regenerate |
| Stopped | Stopped PricingCard.tsx · Partial code kept | Regenerate |
| Error | Generation failed: the model timed out. Try again. | Try again |

### 接上你的模型

為卡片提供 `Generator`：一個接收設計與 `DesignToCodeContext` 的非同步委派。每辨識出一個版面區域就呼叫 `AddRegion`
（方框以設計尺寸的百分比表示），接著在程式碼串流抵達時呼叫 `Write`，並傳入該程式碼片段所實作區域的 id，讓這些行與方框連結。
完成時結束工作，擲出例外以顯示錯誤，並在內容的權杖取消時停止。程式碼片段可以是任意大小：卡片會把它們整理成完整的單字逐批顯示。

<Demo name="YourModel" />

```csharp
card.Model = new AgentModel("Your model");
card.Generator = async (source, context) =>
{
    await foreach (var e in backend.StreamAsync(source.File, context.CancellationToken))
    {
        if (e.Region is { } region)
        {
            context.AddRegion(new DesignToCodeRegion(region.Id, region.Label, region.X, region.Y, region.Width, region.Height));
        }
        else
        {
            context.Write(e.Text, e.RegionId);
        }
    }
};
card.Completed += (_, e) => drafts.Save(e.Code);
```

`AddRegion` 與 `Write` 可以在任何執行緒呼叫。設定了產生器的卡片會以拖放區開啟，也不會循環播放；設定 `Source` 即可從某個設計開始。
`Model` 在程式碼標題列中註明模型，並顯示它的標誌（24 × 24 的描邊圖形）；沒有設定時卡片只顯示閃光圖示（播放模擬時顯示模擬的模型）。

### 拖入截圖

在設計那一側的任何位置都可以拖放，檔案停留時會顯示「Drop to use this design」遮罩。每種拖放方式都有對應的按鈕：
Choose file、Use sample 與 Import design file。`AcceptedFileTypes`（寫法同輸入框的 accept 屬性：MIME 類型、`image/*`
或副檔名）與 `MaxFileSize`（10 MB）決定接受哪些檔案；其他檔案會顯示一則說明問題與解決方式的錯誤，目前顯示的設計維持不變。

<Demo name="DropZone" />

Choose file 會引發 `ChooseFileRequested`。在其中開啟你的檔案選擇器，把檔案交給 `LoadFileAsync`，並設定 `Handled`；
沒有人處理時，卡片會改用模擬的設計（如果有），並說明沒有開啟任何檔案。拖入或載入的檔案會變成一個 `DesignToCodeSource`，包含它的名稱、
解碼後的影像以及 `IStorageFile`，你的產生器可以把它傳送到後端。卡片自己解碼的影像在被取代時會釋放。

```csharp
card.ChooseFileRequested += async (_, e) =>
{
    e.Handled = true;
    var files = await TopLevel.GetTopLevel(card)!.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions());
    if (files.Count > 0)
    {
        await card.LoadFileAsync(files[0]);
    }
};
```

### 設計檔

Import design file 會執行 `DesignFileImporter`，也就是你的選擇器或連結流程：回傳帶算繪影像的畫框（`DesignToCodeSource`），
使用者取消時回傳 null。沒有設定時，如果有模擬，卡片會以它的 `FrameName` 匯入模擬的設計，並說明沒有開啟任何檔案。沒有影像的來源會繪製它的 `Sketch`。

### 即時預覽

「預覽」分頁呈現完成的程式碼實際執行的樣子。`PreviewFactory` 負責算繪，例如放在沙箱化的網頁檢視中；切勿在你的應用程式內執行產生的程式碼。
沒有工廠時，模擬執行使用模擬的 `PreviewFactory`（示範會算繪價格卡片，Monthly 與 Yearly 會切換價格），其他情況顯示一則簡短說明。「程式碼」與「預覽」始終保持掛載，因此切換時會保留各自的捲動位置；
每次新的執行都會回到「程式碼」，讓串流輸出可見。

### 窄容器

版面跟著卡片本身的寬度。從 640px 起兩個面板並排（設計 344px，程式碼佔其餘部分）；更窄時上下堆疊，各高 352px，
因此程式碼串流寫出時卡片的尺寸不會改變。低於 520px 時，狀態不再顯示檔名，「匯入設計檔」只顯示圖示，但保留其名稱供輔助技術使用。
低於 440px 時，附加資訊與模型名稱隱藏，標誌保留。較長的程式碼行以凸排縮排換行，因此不會出現水平捲動。

<Demo name="Narrow" />

### 減少動態效果

在減少動態效果下沒有掃描線，也沒有程式碼片段淡入：區域立即出現，程式碼一次抵達一段（一個區域），模擬只播放一次而不再循環。

<Demo name="ReducedMotion" />

### 內容指引

區域標籤是貼在小截圖上的標籤：用一兩個詞說明這個部分，而不是元件的實作。

| 不要寫 | 應寫 |
|---|---|
| div.pricing-header-container | Header |
| Segmented control (2 options) | Billing toggle |
| Generation completed successfully | Done |

`Labels` 接受一個 `DesignToCodeLabels` 物件，包含卡片顯示或朗讀的每一個詞。帶數值的句子是函式，因此其他語言可以調整語序：

```csharp
card.Labels = new DesignToCodeLabels
{
    Generate = "Write the code",
    DoneMeta = (lines, time) => $"{lines} lines, {time.TotalSeconds:0.0}s",
};
```

### 最佳實務

**建議**

- 先傳送區域，再傳送標註了所實作區域的程式碼，讓方框與程式碼行互相連結。
- 在沙箱中算繪預覽，並且每個頁面或對話方塊只放一張卡片。
- 內容的權杖取消時停止你的請求。

**避免**

- 把模擬當成真實的執行來呈現。它是虛構的，也會如實說明。
- 為了預覽而在應用程式內執行產生的程式碼。
- 傳送沒有 id 的區域：程式碼片段透過 id 與它的方框連結。

### 常見問題

**卡片為什麼以一個設計開啟？** 沒有設定 `Source` 也沒有 `Generator` 時，它顯示模擬的設計。把 `Source` 設為 null 即可以拖放區開啟。

**為什麼我的程式碼行沒有連結到方框？** 一行屬於它第一個字元所在的區域；請在實作該區域的每個程式碼片段中傳入區域的 id。

**Stop 之後抵達的程式碼會怎樣？** 會被捨棄。只保留畫面上的程式碼，已停止的執行之後的寫入都會被忽略。

## API 參考

<!-- api: DesignToCode -->

<!-- api: DesignToCodeSource -->

<!-- api: DesignToCodeRegion -->

<!-- api: DesignToCodeContext -->

<!-- api: DesignToCodeCompletedEventArgs -->

<!-- api: DesignToCodeSimulation -->

<!-- api: DesignToCodeSimulationPart -->

<!-- api: DesignToCodeSketchPalette -->

<!-- api: DesignToCodeLabels -->

列舉：

| 類型 | 值 |
|---|---|
| `DesignToCodePhase` | `Empty`、`Ready`、`Analyzing`、`Writing`、`Done`、`Stopped`、`Error` |
| `DesignToCodeView` | `Code`、`Preview` |
| `DesignToCodeSourceKind` | `Sample`、`File`、`DesignFile` |

## 無障礙

- 卡片名為「Design to code」，包含兩個具名群組：Design 與 Output。一個禮貌型即時區域朗讀每一步：正在分析、找到的區域、
  完成時的行數與耗時、已停止以及錯誤。模擬會說明沒有上傳或開啟任何內容。
- 狀態從不只靠顏色表達：每種狀態都搭配圖示與簡短標籤，錯誤會說明問題與解決方式。
- 區域標籤是真正的按鈕，名稱類似「Price: show its code」。程式碼區是可聚焦的具名區域，程式碼串流寫出時其狀態讀作「Writing」；
  它不是即時區域，因此螢幕閱讀器不會被逐字朗讀淹沒。
- 「程式碼 / 預覽」中隱藏的那一層不可見，也不在 Tab 順序中。以程式碼繪製的設計是裝飾性的；拖入的影像以檔名命名。
- 每種拖放方式都有按鈕：Choose file、Use sample 與 Import design file。

### 鍵盤

| 按鍵 | 操作 |
|---|---|
| `Tab` | 依序經過「移除」、區域標籤、檢視切換、「複製」、「匯入設計檔」與主要操作 |
| `Enter` / `Space` | 啟用取得焦點的控制項；在拖放區上，Choose file 會要求一個檔案 |
| `Left` / `Right` | 在檢視切換上於「程式碼」與「預覽」之間切換 |
| `Escape` | 停止正在分析或撰寫的執行；保留已寫出的部分程式碼 |
| 聚焦某個區域標籤 | 反白該區域的方框及其程式碼行，並把它們捲動到可見位置 |

## 動效

- 分析：一條 1px 的掃描線以一秒掃過截圖，掃描線抵達某個區域的頂端時，該區域的方框與標籤淡入（160ms 不透明度，無縮放）。
- 撰寫：程式碼大約每 34ms 以一批完整單字串流寫出（依閱讀速度每批 3 到 8 個單字）；每批只做 140ms 的淡入。
  正在撰寫的區域在截圖上反白，它的程式碼行帶有淡淡的底色。
- 狀態、預留位置提示以及「程式碼」與「預覽」兩層交叉淡化：舊內容 100ms 淡出，新內容 160ms 淡入。主按鈕的寬度跟著標籤變化，沒有回彈。
- 預覽中，計費開關的滑塊以 0.24s、無回彈的彈簧滑動，價格以等寬數字做 2px 的交叉淡化切換。
- 卡片不在畫面上時，模擬的循環會暫停等候。

| 變化 | 完整 | 減少 | 關閉 |
|---|---|---|---|
| 掃描線 | 1s 掃過 | 無 | 無 |
| 區域 | 掃描線抵達時淡入 | 立即出現 | 立即出現 |
| 程式碼 | 依單字分批，逐批淡入 | 一次一段 | 一次一段 |
| 程式碼與預覽 | 交叉淡化 | 短暫交叉淡化 | 立即切換 |
| 模擬循環 | 循環 | 播放一次 | 播放一次 |

## 相關

- `CodeBlock`：附複製功能的完成程式碼。
- `ToolCall`：代理程式執行中的一個步驟。
- `AgentStatus`：正在執行的代理程式的精簡即時狀態。

| 元件 | 適用情境 |
|---|---|
| `CodeBlock` | 程式碼已經完成，設計不是要講述的內容 |
| `ToolCall` | 產生只是代理程式對話記錄中的一步 |
| `AgentStatus` | 只在意一次執行的進度 |
