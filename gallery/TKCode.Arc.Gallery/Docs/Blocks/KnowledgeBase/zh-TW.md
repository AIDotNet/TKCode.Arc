---
title: 知識庫
summary: 助理可以搜尋的文件：用選擇器或拖放加入檔案，看著每個檔案上傳、解析和向量化並顯示分塊數，重新索引或刪除檔案，並預覽一個問題會找到哪些段落，相符的字詞會被標出。
source-hash: 589ff42e1b82
---

<Demo name="Preview" />

## 何時使用

知識庫是人們管理助理回答前所讀文件的頁面：手冊、政策、產品指南和筆記。人們在這裡需要知道三件事：裡面有什麼，每個檔案是否已經進入索引，以及助理對某個問題實際會找到什麼。`KnowledgeBase` 在一張卡片上回答這三個問題。頁首統計文件數、分塊數和大小，並說明還有多少檔案在索引中。檔案透過「Add files」或拖放到卡片上進入佇列。每一列隨後依序經過 Uploading、Parsing 和 Embedding，並顯示進度環，最後變為 Ready 並顯示分塊數和日期，或者變為 Failed 並顯示原因和「Retry」按鈕。每列末端有「Re-index」和「Delete」（帶列內確認）。清單旁邊的搜尋預覽會為一個問題替已索引的段落排序，顯示每個段落的相關度，並標出相符的字詞。按下一個結果會跳到它所在的文件。

- **助理或客服機器人的來源頁面**：人們上傳說明中心文章，並看到每篇文章何時可以被找到。
- **排查回答**：助理答錯時，搜尋預覽會顯示它讀了哪些段落。
- **檢索流程的管理檢視**：各個階段、失敗和分塊數透過 `IndexHandler` 來自你自己的索引程式。

## 何時不用

- **一般的檔案儲存**：這裡沒有資料夾、共用或版本。請使用 `FileUpload` 加上你自己的清單。
- **聊天中的一個附件**：請使用 `AiComposer`，它把檔案上傳到訊息中。
- **編輯文件**：卡片只列出並索引檔案，不會開啟或修改它們。
- **完整的搜尋頁**：預覽只顯示幾個段落，用來檢查檢索結果。供人瀏覽的搜尋請使用 `SearchResults`。

## 用法

`KnowledgeBase` 衍生自 Avalonia 的 `TemplatedControl`。一個文件是一個 `KnowledgeDocument`，它是一個 `AvaloniaObject`，包含 id、名稱、類型、大小、狀態、進度、分塊數、日期、錯誤，以及供內建流程使用的文字和分塊。沒有你自己的文件時，卡片會顯示一個虛構的資料庫（`KnowledgeBaseSamples`）。真實的空知識庫請設定 `IsSampleData="False"`。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:KnowledgeBase AssistantName="Pip" DocumentsSource="{Binding Documents}" />
</UserControl>
```

### 組成

- **頁首**：標題、寫有助理名字的副標題、總計（「6 documents · 13 chunks · 2.6 MB」）、帶轉圈的索引提示，以及「Add files」。
- **拖放區**：檔案拖到卡片上方時會亮起的一條區域，顯示可接受的類型和大小上限，或者被略過檔案的說明。
- **文件清單**：每個檔案一列。類型圖示、名稱及其下方的資訊（類型、大小、分塊數、日期）或錯誤、帶進度環的階段，然後是「Retry」、「Re-index」和「Delete」。
- **搜尋預覽**：一個搜尋框、一行狀態，以及最多 `MaxPassages` 個結果。每個結果顯示排名、文件和分塊、帶長條的分數，以及標出相符字詞的段落。

### 加入文件

「Add files」會開啟系統的檔案選擇器，並依 `AcceptedExtensions` 篩選。你也可以把檔案拖放到卡片上的任何位置。其他類型或大於 `MaxFileSize`（20 MB）的檔案會被略過，拖放區會說明這一點。其餘檔案以 Queued 狀態加到清單頂端。最多 `MaxConcurrentIndexing`（2）個文件同時索引，其餘依到達順序等待。在程式碼中可以呼叫 `AddDocument`、`AddFiles`（名稱、大小和文字）或 `AddFilesAsync`（儲存檔案）。

<Demo name="Empty" />

### 索引

設定 `IndexHandler` 可以把文件傳送到你的後端。透過 progress 參數回報階段和整體進度，列中的文字和進度環會跟著變化。文件進入索引後傳回分塊；擲回例外則文件失敗，並顯示例外的訊息。

```csharp
knowledge.IndexHandler = async (document, progress, cancellation) =>
{
    progress.Report(new KnowledgeIndexProgress(KnowledgeDocumentStatus.Uploading, 0.1));
    var id = await api.UploadAsync(document.SourceFile, cancellation);
    progress.Report(new KnowledgeIndexProgress(KnowledgeDocumentStatus.Embedding, 0.5));
    return await api.WaitForChunksAsync(id, cancellation);
};
```

沒有設定時，卡片會執行一個模擬流程：選擇的檔案分四步上傳，文字被分成約 480 個字元的分塊（段落盡量保持完整），向量化時每個分塊一步。純文字檔案（.txt、.md、.csv、.json、.html）的文字在加入時讀取；其他檔案需要你的處理程式，模擬流程會以「No readable text」讓它們失敗。`KnowledgeBase.ChunkText` 用同樣的方式分塊，所以你也可以在伺服器上分塊。

<Demo name="Pipeline" />

### 重新索引、重試和刪除

「Re-index」會讓已就緒的文件再走一遍流程；「Retry」對失敗的文件做同樣的事。「Delete」會把這一列換成一個問題（「Delete Returns and refunds.pdf? Its 5 chunks leave the index.」），並提供「Cancel」和「Delete」。設定 `DeleteHandler` 可以先從你的儲存中刪除文件；如果它擲回例外，文件會保留，訊息會被朗讀。`Reindex`、`ReindexAll` 和 `RemoveAsync` 在程式碼中做同樣的事。

### 搜尋預覽

輸入一個問題，稍停片刻後，預覽會替已就緒文件的分塊排序。內建搜尋對字詞使用 BM25，忽略「the」和「how」這類常見字，也能找到複數和更長的形式（「refund」能找到「refunds」和「refunded」）。分數在 0 到 1 之間。設定 `SearchHandler` 可以改為查詢你的向量索引；預覽仍會在你傳回的結果中標出查詢的字詞。Enter 立即搜尋，Escape 清空搜尋框。按下一個結果會把清單捲動到它的文件，替這一列著色，並引發 `PassageInvoked`。

`SearchText` 是雙向的，`Passages` 保存結果。

### 唯讀

`IsReadOnly` 會隱藏「Add files」、拖放區和列內按鈕，保留清單和搜尋預覽。適合可以查看來源但不能修改的人。

<Demo name="ReadOnly" />

### 文字

所有文字都來自 `Labels`（`KnowledgeBaseLabels`）。帶數量或名稱的文字是函式，因此譯文可以依自己的語序排列。控制項上的 `Title` 優先於標籤。

### 內容指南

- **保留人們上傳時的檔名**：他們認得自己取的名字，搜尋結果也會顯示它。
- **用一句話說明失敗原因**：「The workbook is password protected」告訴人們要修什麼；「Error 500」做不到。
- **用真實的問題來預覽**：輸入客戶會問的話，而不是關鍵字，才能看到助理會讀到什麼。

## API 參考

<!-- api: KnowledgeBase -->

<!-- api: KnowledgeDocument -->

### 事件

| 事件 | 何時引發 | 參數 |
|---|---|---|
| `DocumentAdded` | 一個文件加入清單 | `Document` |
| `DocumentDeleted` | 一個文件離開清單 | `Document` |
| `IndexingCompleted` | 一個文件已就緒 | `Document`、`ChunkCount`、`IsReindex` |
| `IndexingFailed` | 索引一個文件失敗 | `Document`、`Exception`、`Message` |
| `PassageInvoked` | 按下了一個搜尋結果 | `Passage` |

## 無障礙

- 卡片是一個區域。標題是二級標題；「Documents」和「Search preview」是三級標題。
- 每一列有一個名稱，讀出檔案、階段、資訊和錯誤（「Battery care.txt, Embedding 64%, TXT · 6 KB」）。「Re-index」、「Delete」和「Retry」按鈕的名稱帶有檔名（「Delete Shipping FAQ.md」），並有工具提示。
- 一個禮貌的即時區域會朗讀加入的檔案、每個就緒或失敗的文件、刪除、重新索引以及找到的段落數。
- 「Delete」從不立即執行：問題出現時焦點在「Cancel」上；取消後焦點回到「Delete」，刪除後焦點移到下一列。
- 狀態從不只靠顏色：Ready 有勾號，Failed 有警告圖示和原因，進行中的階段有名稱和百分比。

### 鍵盤

| 按鍵 | 操作 |
|---|---|
| `Tab` | 「Add files」，然後是每列的按鈕、搜尋框和結果 |
| 列內按鈕上的 `ArrowUp` / `ArrowDown` | 上一列或下一列中的同一個按鈕 |
| 列內按鈕上的 `Home` / `End` | 第一列或最後一列 |
| 刪除問題中的 `Escape` | 取消並回到「Delete」 |
| 搜尋框中的 `Enter` | 立即搜尋 |
| 搜尋框中的 `Escape` | 清空搜尋框 |
| 搜尋框中的 `ArrowDown` | 移到第一個結果；`ArrowUp` / `ArrowDown` 在結果之間移動 |
| 結果上的 `Enter` 或 `Space` | 在清單中開啟它的文件 |

## 動效

- 進度環平順地跟隨每次回報的進度，而不是跳動。
- 檔案拖到卡片上方時，拖放區的底色和邊框在 160 ms 內變化。
- 滑過時，列和結果的底色在 160 ms 內變化。刪除問題在 240 ms 內淡入。
- 拖放區和搜尋預覽中的狀態文字交叉淡化，進入時上升 2 px。
- 減少動效時淡化很短，進度環直接畫到目前的值。關閉動效時所有變化立即生效。

| 變化 | 完整 | 減少 | 關閉 |
|---|---|---|---|
| 進度環 | 跟隨數值 | 跳動 | 跳動 |
| 拖放區和列的底色 | 160 ms | 立即 | 立即 |
| 刪除問題 | 淡入 240 ms | 立即 | 立即 |
| 狀態文字 | 交叉淡化，上升 2 px | 120 ms 交叉淡化 | 立即 |

## 響應式

- `KnowledgeBase` 依自身寬度而不是視窗寬度變化。從 760 px（`WideWidth`，`:wide`）起，搜尋預覽是清單旁邊一個 340 px 寬的欄；更窄時它排在清單下方。
- 窄於 480 px（`CompactWidth`，`:compact`）時，內距收緊，列會隱藏日期和階段文字（進度環和圖示保留）。
- 卡片最寬 960 px 並置中。清單超過 420 px 時在卡片內捲動，並且是虛擬化的，所以幾千個文件也很快。
- 名稱和資訊以省略號結尾；錯誤和段落會換行。

## 相關元件

- `FileUpload` 用於上傳檔案而不建立索引。
- `AiChat` 用於使用這些文件的聊天。
- `AgentMemory` 用於助理記住的關於一個人的內容。

| 元件 | 何時選擇 |
|---|---|
| `FileUpload` | 檔案只是儲存，不為助理建立索引 |
| `SearchResults` | 人們瀏覽搜尋結果 |
| `AgentMemory` | 助理保存關於一個人的簡短事實 |
| `AiAnswer` | 你要展示一個帶來源的回答 |
