---
title: 文件擷取
summary: 將文件轉為具型別的資料，並顯示每個值的來源：安靜的文件檢視器旁邊是可編輯的表單，模型閱讀時欄位會逐一填入，並搭配著色的來源文字、曲線連線、低信心度覆核與具型別的 JSON 結果。
source-hash: 9e50c2c8b02b
---

<Demo name="Preview" />

## 何時使用

文件擷取適用於人必須確認模型從文件讀到的內容是否可信的時刻。覆核者能並排看到文件與擷取出的資料，確切看到每個值的來源，並在資料往下送之前決定要修正什麼。

- **發票、收據與訂單寫入系統紀錄**：模型讀取時欄位會逐一填入，明細表格也會逐列增加。
- **電子郵件與訊息分類**：同一個檢視器讀取一封郵件，並擷取出訂單、貨運業者與日期。
- **檢視模型的疑慮**：低於信心度門檻的值會附上模型的理由與 "Looks right"（看起來正確）按鈕，而 "Review"（覆核）按鈕會跳到下一個。
- **把具型別的資料交給你的程式碼**：日期以 ISO 文字送達，金額為數字，IBAN 不含空格；若值不符合其型別，會成為 null，並阻擋 "Accept all"（全部接受）。

## 何時不用

- **關於文件的對話**：使用 `AiChat` 或 `AiAnswer`。
- **閱讀非常長的文件**：檢視器一次只顯示一頁，而表單適合的是幾十個欄位。
- **在大量文件上訓練或評分模型**：這是由人覆核單一文件。
- **顯示檔案**：檢視器繪製的是你給它的文件模型，而不是 PDF 檔案。

## 用法

`DocumentExtraction` 衍生自 Avalonia 的 `TemplatedControl`，由資料驅動。給它 `Samples`（每個都是一份文件加上其結構描述）、用於頁尾的 `Model`，以及呼叫你的模型的 `Extractor`。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:DocumentExtraction x:Name="Extraction" MaxWidth="1080" />
</UserControl>
```

```csharp
extraction.Samples.Add(new ExtractionSample { Document = invoice, Schema = invoiceSchema });
extraction.SelectedSample = extraction.Samples[0];
extraction.Model = new ExtractionModelInfo { Name = "My model", Id = "my-model", InputPricePerMillion = 0.12m, OutputPricePerMillion = 0.60m };
extraction.Extractor = new MyExtractor();
extraction.Accepting += (_, e) => e.Defer(SaveAsync(e.Output.ToJson()));

public sealed class MyExtractor : IDocumentExtractor
{
    public async IAsyncEnumerable<ExtractionEvent> ExtractAsync(
        ExtractionDocument document, ExtractionSchema schema, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        // Send document.ToPlainText() and schema.ToJsonSchema() to your model, then report what comes back.
        await foreach (var found in model.ReadAsync(document.ToPlainText(), schema.ToJsonSchema(), cancellationToken))
        {
            yield return new ExtractionValueEvent(found.Path, found.Value, found.SourceId, found.Confidence, found.Note);
        }
    }
}
```

### 文件

`ExtractionDocument` 是一個名稱加上一串區塊，繪製在一張白紙上（電子郵件則繪製為郵件頁面）：抬頭、標題、段落、地址行、鍵與值的配對、一排有標籤的儲存格、欄、表格、合計、分隔線與頁尾。文字由文字段（runs）與 `DocumentSpan` 組成：片段是值可以指回的一段文字，帶有一個 id，擷取器會以 `SourceId` 引用它。

### 結構描述

`ExtractionSchema` 列出要找的欄位：一個鍵、一個標籤與一個型別（`Text`、`Date`、`Money`、`Currency`、`Iban`、`Integer`、`Number`）。表格欄位有欄，其值以路徑 `key.row.column` 回報。欄位隸屬於群組；每個群組依序取用四個系列色之一，同一顏色會同時著色圓點、欄位列、被引用的文字與連線。若沒有群組，每個欄位各有自己的顏色。

<Demo name="Custom" />

### 一次執行

擷取會讀取文件：狀態列以閃爍效果顯示 "Reading the document"（正在讀取文件），一道掃描光掠過頁面，接著值逐一落定。每個落定的值會填入其欄位、為它來源的文字著色，並在頁尾升起一條長條。檢視器會跟隨這些值，直到你手動捲動為止。按鈕會變成 Stop（停止）；停止會保留目前已找到的值。失敗的執行同樣會保留這些值，並提供 Retry（重試）。再次擷取會從頭開始。

- **事件**：擷取器對每個值產生 `ExtractionValueEvent`（文字如文件所寫，例如 "1,400.00"；日期為 `YYYY-MM-DD`）、以權杖總數計算費用的 `ExtractionUsageEvent`，以及使執行失敗的 `ExtractionErrorEvent` 或例外。透過 `CancellationToken` 取消。
- **費用與時間**：頁尾顯示模型、依權杖總數與價格計算的費用，以及經過的時間。

### 覆核

- **連線**：將游標移到欄位上或使其取得焦點，會點亮頁面上對應的文字，並畫出一條曲線連線；指向頁面上的文字則會點亮其欄位，點擊則讓該欄位取得焦點。在窄的容器中，窗格會上下堆疊，且不畫連線。
- **低信心度**：低於 `ReviewThreshold`（0.8）的值會顯示警告、模型的理由，以及 "Looks right"（看起來正確）。狀態列中的 "Review"（覆核）會跳到第一個。
- **編輯**：每個欄位都是真正的輸入框。編輯會把值標記為 "Edited"（已編輯）、清除其低信心度標記，並更新 JSON 分頁，其中被編輯的那一行會閃爍。不符合其型別的值會顯示訊息、在輸出中為 null，並阻擋 "Accept all"（全部接受）。
- **頁籤**：Fields（欄位）、JSON（具型別的輸出，與欄位相連）與 Schema（傳送給模型的 JSON 結構描述）。

<Demo name="Narrow" />

### 接受

按下 "Accept all"（全部接受）會以具型別的輸出觸發 `Accepting`。使用 `e.Defer(task)` 取得延遲，以非同步方式儲存；設定 `Cancel` 或讓工作出錯，即可撤銷接受。成功儲存後，每個欄位都會顯示為已接受。之後編輯某個值，只會讓該值回到未接受的狀態。

### 縮放

檢視器以步進方式從 40% 縮放到 200%，可用 Ctrl 或 Cmd 搭配加號、減號與 0 鍵。"Fit"（符合寬度）會讓頁面寬度與窗格相同。縮放、`IsFitWidth` 與頁籤皆為雙向屬性。

### 文字

每個字都來自 `Labels`（`DocumentExtractionLabels`）：要翻譯或改寫時，請替換該實例；有格式的文字則為函式。

### 內容指引

- **欄位標籤**：簡短的名詞（例如 "Invoice no."（發票號碼））。
- **描述**：說明要找什麼；它們會以 JSON 結構描述送給模型。
- **理由**：擷取器的註記應以一句話說明它為何不確定。

### 最佳實務

- 為每個在文件中有位置的值引用 `SourceId`，讓文字能著色並可連線。
- 回報真實的信心度；覆核流程依賴它。
- 傳遞取消權杖（`CancellationToken`），讓 Stop 與新的一次執行能真正中止模型。
- 在 `Accepting` 中儲存，並在儲存失敗時拋出例外。

### 常見問題

**為什麼沒有連線線？** 連線只在並排配置中繪製，而且只有當兩端都可見時才會繪製。

**為什麼 JSON 中的值是 null？** 它的文字不符合其型別，例如不是 `YYYY-MM-DD` 的日期。

**平均信心度計算的是什麼？** 模型找到的每一個值，包括表格儲存格。

## API 參考

<!-- api: DocumentExtraction -->

<!-- api: ExtractionDocument -->

<!-- api: ExtractionSchema -->

<!-- api: ExtractionValue -->

### 事件

| 事件 | 觸發時機 | 引數 |
|---|---|---|
| `ExtractionStarted` | 一次執行開始時 | |
| `ExtractionCompleted` | 一次執行結束：完成、停止或失敗 | `Result`、`Error` |
| `ValueChanged` | 值因模型或人員而改變時 | `Path`、`OldText`、`NewText`、`Origin` |
| `ValueChecked` | 人員將值標記為 "Looks right"（看起來正確）時 | `Path` |
| `Accepting` | "Accept all"（全部接受）需要儲存輸出；設定 `Cancel` 或讓延遲出錯以撤銷 | `Output`、`Values`、`Cancel`、`Defer` |
| `Accepted` | 接受已儲存時 | |
| `SelectedPathChanged` | 連線的欄位改變時 | `Path` |

### 串流與輸出

| 型別 | 說明 |
|---|---|
| `IDocumentExtractor` | `ExtractAsync(document, schema, cancellationToken)` 回傳一次執行的事件 |
| `ExtractionValueEvent` | 一個值：路徑、文字、來源片段 id、信心度與註記 |
| `ExtractionUsageEvent` | 到目前為止的權杖總數 |
| `ExtractionErrorEvent` | 模型無法完成 |
| `ExtractionOutput` | 依結構描述順序排列的具型別結果；`ToJson()` 將其寫出 |
| `ExtractionModelInfo` | 模型的名稱、id，以及每百萬權杖的價格 |

## 無障礙

- 檢視器是一個以檔案名稱與縮放比例命名的區域，頁面是一篇文章，表單的輸入框是真正的、有名稱的文字方塊；表格的儲存格以欄與列命名。
- 信心度以文字朗讀（例如 "Low confidence, 64 percent, check this value"（低信心度，64%，請檢查此值）），狀態絕不只靠顏色：警告、勾選與編輯都有圖示與文字。
- 無效的值會被標記為無效，並以其訊息描述；低信心度的值則以模型的理由描述。
- 兩個禮貌型即時區域會宣布執行（開始、找到多少、需要檢查多少、停止、失敗）與覆核（已覆核、已接受、需先修正的值）。失敗的執行會以強制型（assertive）宣布。
- 減少動態效果時，仍會保留每個狀態與宣布。

### 鍵盤

| 按鍵 | 動作 |
|---|---|
| `Tab` | 檔案按鈕、縮放按鈕、頁面、頁籤、動作按鈕、依序的欄位（低信心度欄位之後接著其 "Looks right"（看起來正確）按鈕）、"Accept all"（全部接受） |
| 欄位取得焦點 | 選取它：頁面捲動到其文字，並畫出連線 |
| `Escape` | 停止執行；否則清除已選取的欄位 |
| 頁面上搭配 `Ctrl` 或 `Cmd` 與 `+`、`-`、`0` | 放大、縮小、符合寬度 |
| 頁面上的 `ArrowUp` / `ArrowDown` | 捲動頁面 |
| 頁籤上的 `ArrowLeft` / `ArrowRight` | 移動並選取 |
| `Enter` / `Space` | 啟動按鈕，或在選單中選擇文件 |

## 動態效果

- 值以文字與信心度欄的短暫淡入落定；被引用的文字以較深的色調閃爍後平復；頁尾長條以彈簧升起，平均值與費用則會滾動更新。
- 表格列以彈簧展開；模型的理由以彈簧展開與收合；狀態列以交叉淡入淡出切換，其高度隨之調整。
- 連線在五分之一秒內自行繪出，並有一次掠過的光澤；切換連線時則交叉過渡。
- 頁面以彈簧繞其中心縮放；掃描光在執行開始時橫越頁面一次。
- "Accept all"（全部接受）會依序標記各欄位，彼此相隔 35 ms。
- 減少動態效果會移除掃描、連線繪製、彈簧與交錯效果，並保留交叉淡入淡出；關閉則會立即套用每項變化。

| 變化 | 完整 | 減少 | 關閉 |
|---|---|---|---|
| 連線 | 繪出並有光澤 | 出現與消失 | 出現與消失 |
| 落定的值 | 淡入並閃爍 | 淡入 | 立即 |
| 縮放、列、長條 | 彈簧 | 立即 | 立即 |
| "Accept all"（全部接受） | 交錯 35 ms | 同時完成，並交叉淡入淡出 | 立即 |

## 相關元件

- `ModelArena`：依回答判斷兩個模型。
- `PromptStudio`：編輯擷取背後的提示。
- `AiAnswer`：附有引用來源的答案。

| 元件 | 適用時機 |
|---|---|
| `ModelArena` | 你比較模型，而不是閱讀文件 |
| `PromptStudio` | 你要維護提示 |
| `AiAnswer` | 結果是附有引用的文字 |
