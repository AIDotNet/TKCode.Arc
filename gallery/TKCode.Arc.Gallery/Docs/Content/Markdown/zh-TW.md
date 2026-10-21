---
title: Markdown
summary: 以 Arc 的風格渲染 Markdown 文件與串流回覆，程式碼區塊使用 Arc 的語法配色高亮。
source-hash: 996fd7825241
---

<Demo name="Document" />

## 何時使用

`Markdown` 渲染作者以 Markdown 撰寫的文字：標題、清單、表格、引言、連結和圍欄程式碼。它用於產品呈現那些自己不手動排版的格式化文字，例如版本說明、說明文章或模型的回答。控制項接收 Markdown 原始碼，解析後以 Arc 的字型、顏色和間距繪製，因此面板、聊天泡泡或文件檢視中的文字與應用程式其餘部分保持一致。

- **版本說明與應用程式內說明**：Acme Analytics 中的「新功能」面板，呈現內容團隊以 Markdown 檔案撰寫的說明。標題、清單和連結在每個版本中外觀一致，無需為每個版本單獨手動排版。
- **支援聊天中的助理回覆**：逐個權杖抵達的回答。每個資料區塊呼叫 `Append`，結束時呼叫 `Complete`，回覆末尾的文字淡入，而前面的段落保持不動。
- **附程式碼範例的開發者文件**：API 指南中的圍欄區塊，C#、TypeScript、JSON 或 shell 程式碼使用 Arc 調色盤中的語法顏色。超過 12 行的長區塊會摺疊，因此一個範例不會把頁面其餘部分擠開。
- **來源不可控的富文字**：工單內文、CMS 欄位或匯入的筆記。標題、表格和連結與產品保持一致，連結預設在系統瀏覽器中開啟，除非應用程式接管。
- **字級較小的訊息泡泡**：支援對話中 14 px 的回覆，行高和區塊間距隨字級變化，使泡泡保持精簡。

## 何時不用

- **附複製按鈕的單一程式碼區塊**：用 `CodeBlock`，它是一個帶有自己標題列和操作的區塊。`Markdown` 用於混合了段落、清單和程式碼的文件。
- **需要自身捲動的長文件**：將控制項包裹在 `ScrollViewer` 中。文件隨內容增長，自身沒有捲軸。對於很長的對話，請使用 StreamMark 函式庫的對話記錄檢視。
- **原地編輯 Markdown 原始碼**：用 `Textarea`，它保存原始文字、自動換行，並允許使用者編輯。`Markdown` 只顯示渲染結果。
- **發票等結構化紀錄**：用「表格」分組中的 `SortableDataTable`，它會排序，並在列變化時保持欄位對齊。Markdown 表格是唯讀的，沒有排序功能。
- **數學公式、圖表或其他嵌入式格式**：`Markdown` 渲染文字、程式碼和表格。需要時，請把 StreamMark 的數學或圖表外掛加入你自己的 `MarkdownServices`，而不是在文字中貼上圖片。

## 用法

靜態文字透過 `Text` 設定。文件隨內容增長，內部沒有捲動。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:Markdown Text="## 版本說明&#10;&#10;Arc **0.4** 新增 `Markdown`。" />
</UserControl>
```

`Markdown` 衍生自 Avalonia 的 `TemplatedControl`。它的範本是包裹 StreamMark `MarkdownView` 的外框，解析與版面配置由該檢視完成。Arc 透過該檢視的 `Md.*` 資源提供顏色、字型和圓角，因此文件會跟隨主題範圍、明暗模式和強調色。由於文件是一個檢視而不是文字區塊，它可以像任何文件一樣選取、複製和換行，並隨內容增長。

### 結構

1. **外框**：透明的 `Border`。主題不為它設定邊框和內距，文件佔滿容器的全部寬度（`HorizontalAlignment` 為 `Stretch`）。
2. **內文**：16 px 的內文字型，行高 26 px（字級乘以 1.65）。文字依容器給出的寬度換行。
3. **區塊**：段落、清單、引言、程式碼和表格之間相隔 12 px（`BlockSpacing`）。
4. **標題與清單標記**：標題使用前景色，由文件檢視決定其大小。清單標記使用淡化文字顏色，使清單項目的文字保持最醒目。
5. **連結**：強調色並帶底線。預設在系統瀏覽器中開啟。
6. **行內程式碼**：等寬字型，背景為淡化的表面色。
7. **圍欄程式碼**：表面色背景，邊框為邊框色，使用等寬字型和 Arc 語法調色盤。超過 12 行的程式碼區塊會摺疊，並提供顯示全部行的控制項。程式碼區塊提供複製，不提供下載。
8. **引言**：被引用文字旁邊有一條強邊框色的直線。
9. **表格**：邊框為邊框色，表頭列為淡化表面色，儲存格文字使用內文字型。
10. **標示與選取**：被標示的文字位於警告色的淡化底色上，選取的文字位於強調色的淡化底色上。

### 繫結文件

將 `Text` 繫結到檢視模型屬性。設定它會取代仍在顯示的回覆，`null` 或空字串會顯示空白文件。

```xml
<arc:Markdown Text="{Binding ReleaseNotes}" FontSize="14" BlockSpacing="10" />
```

`FontSize`、`FontFamily`、`LineHeight` 和 `BlockSpacing` 是僅有的排版參數。請將 `LineHeight` 保持預設值（NaN），使行高跟隨字級。只有設計需要精確值時才設定它。

### 繫結到檢視模型

檢視模型擁有 Markdown 原始碼，對於回覆還擁有串流。使用 CommunityToolkit.Mvvm 時，文字是一個 `ObservableProperty`，串流是另一個。

```csharp
public sealed partial class ReleaseNotesViewModel : ObservableObject
{
    [ObservableProperty]
    private string? _releaseNotes;

    [ObservableProperty]
    private MarkdownStream? _reply;
}
```

```xml
<arc:Markdown Text="{Binding ReleaseNotes}" />
<arc:Markdown Stream="{Binding Reply}" />
```

請讓兩個繫結都保持單向。控制項自身在 `Append` 開始一個回覆時會把 `Text` 設為 `null` 並取代串流；若使用雙向繫結，這些值就會寫回檢視模型。`IsStreaming` 為唯讀，回報控制項上串流的狀態。

### 串流回覆

每個資料區塊抵達時呼叫 `Append`，回覆結束時呼叫 `Complete`。文字會依節奏顯示，新詞在文件末尾淡入。`IsStreaming` 在節奏化的待顯示內容全部呈現之前保持為 `true`，因此狀態列可以顯示「正在輸出」，直到回覆完全顯示在螢幕上，而不是直到最後一個資料區塊抵達。

<Demo name="StreamedReply" />

```csharp
markdown.Append(chunk);   // 權杖抵達時呼叫，在 UI 執行緒上執行
markdown.Complete();      // 回覆結束時呼叫
```

`Complete` 之後再呼叫 `Append` 會開始新的回覆，並取代目前顯示的內容：此後 `Text` 讀取為 `null`。你自行建立的串流可以改為設定到 `Stream` 上，例如由工作執行緒透過 `MarkdownStream.Push` 寫入、並以 `Complete` 結束的串流。控制項在 UI 執行緒上套用快照，因此工作執行緒只推送文字，從不觸碰控制項。

### 聊天尺寸

訊息泡泡可使用較小的 `FontSize`。`LineHeight` 預設為字級乘以 1.65，因此 14 px 的回覆行高為 23 px，程式碼字級也隨之變化。

<Demo name="ChatSize" />

### 連結

絕對路徑的 `http`、`https` 和 `mailto` 連結會透過系統啟動器開啟。處理 `LinkClicked` 即可接管，並將事件標記為已處理以阻止預設行為。設定 `OpensLinks="False"` 可關閉預設行為。被連結策略標記為需要確認的連結不會由預設處理常式開啟，因此請在你自己的處理常式中確認。指向文件內部的錨點會自行捲動到目標位置，且不會引發 `LinkClicked`。

```csharp
markdown.LinkClicked += (_, e) =>
{
    e.Handled = true;
    OpenInsideApp(e.Url);
};
```

### 宿主整合

在文件周圍自建介面（大綱側欄、尋找列、複製按鈕）的宿主使用公開介面，不必走訪範本。大綱依閱讀順序列出每個標題及其跳轉位置；`Find` 醒目提示所有相符項目並傳回數量；`FindNext` / `FindPrevious` 移動目前相符項目；選取方法複製讀者選取的內容。

<Demo name="Outline" />

```csharp
markdown.DocumentReady += (_, _) =>
{
    foreach (var item in markdown.GetOutline())
    {
        AddOutlineRow(item.Level, item.Text, () => markdown.ScrollToPoint(item.Target));
    }
};

var count = markdown.Find(query);      // 醒目提示所有相符項目
markdown.FindNext();                   // 目前相符項目移動並捲動到它
```

| 成員 | 用途 |
|---|---|
| `GetOutline()` | 依閱讀順序傳回標題：層級、文字、id 與目標位置 |
| `ScrollToPoint`、`ScrollToBlock`、`ScrollToAnchor` | 跳到大綱項目、區塊或標題錨點 |
| `Find`、`FindNext`、`FindPrevious`、`FindAt`、`ClearSearch` | 尋找列，`MatchCount` 與 `CurrentMatchIndex` 用於顯示進度 |
| `SelectAll`、`GetSelectedText`、`GetSelectedMarkdown`、`CopySelectionAsync` | 複製操作，`HasSelection` 用於按鈕的可用狀態 |
| `IsDocumentReady`、`DocumentReady` | 判斷大綱、相符項目與跳轉位置何時最終確定 |

解析在工作執行緒上進行，因此時序也是契約的一部分。範本尚未套用時沒有檢視：大綱為空、跳轉方法傳回 `false`、`Find` 傳回 0、選取範圍為空。設定新的 `Text` 或 `Stream` 後，`IsDocumentReady` 會在快照解析並套用之前保持 `false`，之後 `DocumentReady` 執行一次。尋找條件會被保留，因此文件在尋找期間變更時會重新執行尋找。重新套用範本會建立新檢視：文件交給新檢視，`DocumentReady` 再次執行，尋找也重新執行，但選取範圍不會保留。宿主在載入後還原捲動位置時，請等待 `DocumentReady` 事件，不要用計時器。

### 程式碼高亮

`MarkdownHighlightPlugin` 使用 `CodeBlock` 同樣使用的 TextMate 語法為圍欄程式碼上色，並以 Arc 語法調色盤中適用於明暗模式的顏色繪製每種角色。註解為斜體。沒有語法的語言保持為純文字，因此未知的標籤不會導致文件失敗。該外掛是預設服務的一部分。把語言名稱傳給它的建構函式，可以在第一個程式碼區塊請求之前於背景預先載入這些語法。

```csharp
markdown.Services = new MarkdownServices
{
    Plugins = [new MarkdownHighlightPlugin("cs", "ts")],
};
```

若某個檢視要求程式碼不帶顏色顯示，請不要把該外掛放入 `Plugins`，此時程式碼區塊就是等寬字型的純文字。

### 服務與優先順序

StreamMark 服務決定文件如何解析以及程式碼區塊如何表現。控制項依以下順序選取服務：自身的 `Services` 屬性，然後是祖先透過 `MarkdownServices.SetServices` 設定的服務，最後是 `Markdown.DefaultServices`。在視窗或頁面上設定服務，可以讓檢視中的每個文件使用相同的外掛。外掛可以改變 Markdown 方言，因此修改服務會重新解析靜態文字。

`Markdown.DefaultServices` 使用 `MarkdownHighlightPlugin` 高亮程式碼，複製程式碼但不提供下載，並把長程式碼區塊摺疊為 12 行。

### 範例：支援聊天中的助理回覆

一個支援助理在聊天泡泡中回答 Acme Analytics 的客戶。回覆使用一個簡短清單和一個指向匯出頁面的連結，並設為 14 px，以與對話中的其餘文字保持一致。泡泡有淡化的表面色和 16 px 的內距。這裡的回覆是靜態的，因此截圖展示的是已穩定的文字，也就是串流回覆在 `IsStreaming` 變為 `false` 後所達到的狀態。

![支援助理的回覆：聊天泡泡中包含一個簡短清單和一個連結](images/ChatReply.png)

```xml
<Border Width="600"
        Padding="16"
        Background="{arc:ArcDesignTokenResource SurfaceMuted}"
        CornerRadius="{arc:ArcDesignTokenResource RadiusPanel}">
    <arc:Markdown FontSize="14"
                  Text="Your March export is ready. The file holds **1,250** invoices in `invoices_2026_03.csv`.&#10;&#10;- Paid: **1,012**, pending: **105**&#10;- Overdue: **86**, failed: **47**&#10;&#10;You can open the [export page](https://uiarc.dev) to download it." />
</Border>
```

### 文案規範

渲染出的文字會被當作文件閱讀，因此請像撰寫文件那樣書寫：簡短的標題、完整的句子，以及能說明去向的連結文字。

| 不要寫 | 改為 |
|---|---|
| 把 `Here's the thing!` 用作標題 | `Changes in version 0.4` |
| 把 `Click here` 用作連結 | `Read the migration guide` |
| 把 `Loading...` 用作占位回覆 | 讓回覆保持為空，並用 `IsStreaming` 顯示狀態 |
| 八欄的表格 | 兩到三欄，其餘內容放在表格下方的清單中 |
| 聊天回覆中間一段 60 行的程式碼範例 | 關鍵的三行程式碼，以及指向完整範例的連結 |
| 句子中間的裸 URL | 以頁面名稱作為文字的連結 |

標題使用句首大寫的句子式寫法，並保持簡短，便於一眼掃讀。程式碼範例要便於讀者複製：寫完整的陳述式，並在圍欄中標明語言（`cs`、`ts`、`json`、`xml`、`sh`），以便高亮器為其上色。

### 最佳做法

**建議**

- 將 `FontSize` 設為與周圍文字一致，而不是使用預設值，使面板中的文件與泡泡中的回覆不會並排顯示兩種字級。
- 把 `IsStreaming` 顯示為狀態，並在它為 `true` 時停用傳送按鈕，使使用者不會在第一則回覆結束前傳送第二個問題。
- 為應用程式保留一個 `MarkdownServices`，並設定在視窗上，使每個文件的程式碼高亮方式一致。
- 將長文件放入 `ScrollViewer`，使捲動與捲軸來自同一個容器。
- 當連結必須在應用程式內開啟或需要確認時，處理 `LinkClicked`，使決定權留在你的程式碼中。

**避免**

- 在同一個控制項上同時設定 `Text` 和 `Stream`。最後設定的值生效，另一個會被清除或忽略。
- 逐個字元附加。每個資料區塊都會被解析並依節奏顯示，因此應推送幾個詞一組的資料區塊，就像模型的權杖串流那樣。
- 在多字級的版面中使用固定的 `LineHeight`。固定值不隨字級變化，因此 14 px 的回覆和 16 px 的文件會逐漸錯位。
- 依賴 `Markdown` 來呈現需要排序或篩選的表格。Markdown 表格沒有排序，也沒有選取功能。
- 在沒有 `LinkClicked` 處理常式的情況下顯示來源不可信的 Markdown。絕對連結預設會在系統瀏覽器中開啟，因此請先在你自己的處理常式中檢查 URL。

### 常見問題

**為什麼串流回覆結束後 `Text` 讀取為 `null`？** `Append` 會開始一個串流，並把 `Text` 設為 `null`，因為回覆會取代靜態文字。再次設定 `Text` 即可顯示靜態文字，同時也會結束串流。

**為什麼我的文件不能捲動？** `Markdown` 隨內容增長，沒有內部捲軸。將它包裹在 `ScrollViewer` 中，由它捲動整個文件。

**如何知道回覆已完整顯示？** 讀取 `IsStreaming`，或監聽 `PropertyChanged` 中它的變化。在 `Complete` 之後，待顯示內容全部呈現時它變為 `false`，這可能在最後一個資料區塊抵達之後片刻才發生。

**為什麼長程式碼區塊會被摺疊？** 預設服務會把程式碼區塊摺疊為 12 行，並顯示一個展開控制項，這樣長範例就不會把聊天回覆中的其餘內容推出視線之外。若要更改上限，請在 `MarkdownServices` 中設定你自己的 `CodeBlocks` 選項。

**為什麼點擊連結時沒有開啟？** 預設只開啟絕對路徑的 `http`、`https` 和 `mailto` 連結。相對連結、自訂配置或需要確認的連結，會交由你的 `LinkClicked` 處理常式決定。

## API 參考

<!-- api: Markdown -->

| 成員 | 類型 | 說明 |
|---|---|---|
| `Name` | `string` | 外掛名稱，即 `arc-highlight` |
| `Highlighter` | `ISyntaxHighlighter` | 該外掛註冊的高亮器，由所有文件共用 |

`MarkdownHighlightPlugin` 的建構函式接收需要預熱的語言名稱，例如 `"cs"` 和 `"ts"`。

## 無障礙

- 文字可以選取並複製。連結帶有底線，並使用強調色，因此不會只靠顏色區分。
- 程式碼區塊使用與 `CodeBlock` 相同的調色盤。顏色用於輔助原始文字，從不取代原始文字：每個權杖都保留其字元，因此程式碼在螢幕閱讀器或純文字複製中讀起來一樣。
- 在串流回覆仍未結束時，使用 `IsStreaming` 顯示狀態。在 `IsStreaming` 變為 `false` 之前一直顯示「正在輸出」的狀態列，會告訴使用者回覆何時完成。

### 鍵盤

- `Markdown` 自身不接收鍵盤焦點，也不新增任何按鍵繫結。選取與複製來自文件檢視。

## 動態效果

- 串流回覆中的新文字在 240 ms 內淡入。節奏控制會把文字分散開來，使其不會成批湧現。
- 減少動態效果時，淡入縮短為 160 ms，且只使用不透明度變化。關閉動態效果時，新文字立即顯示，沒有淡入。
- 節奏控制在所有動態效果等級下都生效。它控制文字何時出現，而不是一段動畫。

| 變化 | 完整 | 減少 | 關閉 |
|---|---|---|---|
| 串流回覆中的新文字 | 在文件末尾淡入，240 ms | 僅以不透明度淡入，160 ms | 立即顯示 |
| 串流回覆的節奏 | 套用 | 套用 | 套用 |

## 相關元件

- `CodeBlock`：單獨展示一個程式碼範例。
- `Textarea`：使用者需要編輯多行文字時使用。

| 元件 | 適用情境 |
|---|---|
| `CodeBlock` | 一個帶有自己複製操作的程式碼範例 |
| `Textarea` | 使用者編輯多行文字，且原始碼才是重點 |
| `SortableDataTable` | 紀錄必須排序，例如發票表格 |
