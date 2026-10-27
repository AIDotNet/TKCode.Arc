---
title: 富文字編輯器
summary: 輕量編輯器，支援 Markdown 快速輸入、浮動工具列、斜線選單，並可輸出 HTML 與 Markdown。
source-hash: b7c2d44c1172
---

<Demo name="Shortcuts" />

## 何時使用

`RichTextEditor` 是需要輕量格式之內容的寫作介面：粗體與斜體文字、標題、清單、引言與程式碼區塊。它自帶文件、浮動工具列與區塊選單，因此表單不需要在它外面另外排版。內容會以乾淨的 HTML 或 Markdown 離開編輯器，由應用程式儲存，之後再讀回。

- **開發者工具中的發行說明與更新日誌草稿**：工程師輸入 `## ` 插入標題、輸入 `- ` 插入清單，編輯器儲存的 Markdown 會被發行流程原樣發佈。
- **專案追蹤工具中的留言與回覆，或 CRM 備註**：帶粗體文字、清單與引言的簡短備註。編輯器只保留自己的標籤，因此輸出的 HTML 可以安全地渲染。
- **知識庫或說明中心文章**：作者在管理後台撰寫文章，儲存的 HTML 由公開網站渲染，步驟與指令以標題和程式碼區塊呈現。
- **事故報告與事後檢討**：用標題、面向客戶措辭的引言，以及記錄行的程式碼區塊。報告發布後設定 `IsReadOnly`，文字仍可閱讀和複製，但不能修改。
- **帳單或帳號主控台中的公告**：由需要格式但不需要版面配置的財務或支援人員撰寫的簡短訊息，附帶發票頁面的連結。

## 何時不用

- **沒有格式的純多行文字**：用 `Textarea`，它會換行並可調整大小，不帶工具列或區塊。
- **在文字中提及人員或標籤**：用 `MentionInput`，它會提供建議並儲存提及內容。
- **在頁面上就地重新命名的單行文字**：用 `InlineEdit`，編輯前它看起來就是一般文字。
- **原始程式碼或唯讀程式碼清單**：用 `CodeBlock`，它自帶複製操作。
- **版面複雜的文件**：表格只儲存行內文字（無巢狀區塊），圖片獨佔一個區塊。需要頁面版面、分欄，或需要帶進度上傳檔案時，請用文件編輯器或 `FileDropzone`。

## 用法

用 `Markdown` 設定初始內容。`Markdown` 與 `Html` 都是雙向的：為其中任一項指派值都會重新載入編輯器，且兩者都會跟隨每次編輯更新。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:RichTextEditor Markdown="## 筆記&#10;&#10;邊寫邊輸入 **Markdown**，或輸入 `/` 插入區塊。" />
</UserControl>
```

`RichTextEditor` 衍生自 `TemplatedControl`，而不是 `TextBox`。它是區塊編輯器：文件是一列區塊（段落、標題、清單項目、引言、程式碼區塊與分隔線），每個區塊以自己的標記排版自己的文字。這讓編輯器擁有獨立的復原歷程、剪貼簿規則與輸出，因此文字並不是由控制項原地編輯的原始字串。它不是 `TextBox`，所以沒有插入點索引或 `MaxLength`；請用 `GetText()` 讀取文字，其他成員見 API 參考。

### 結構

1. **書寫區**：可編輯的文件，最小高度為 128 px。它繪製文字、清單標記、引言直條、程式碼背景與選取範圍，並定位插入點。
2. **預留位置文字**（`Placeholder`，預設為「開始寫作」）：文件為空時以淡化的文字顏色顯示。
3. **區塊提示**（`BlockHint`，預設為「輸入 / 插入區塊」）：顯示在放置插入點的空白行上。
4. **浮動工具列**：出現在選取範圍上方，按鈕為 32 px 的方形，圓角 16 px。它包含行內標記（粗體、斜體、刪除線、行內程式碼）、連結按鈕與區塊類型。編輯連結時，其連結欄位會就地取代按鈕。
5. **連結欄位**：工具列中的單行欄位，接受選取範圍或插入點處連結的 URL。
6. **區塊選單**：在 `/` 處開啟、並隨輸入篩選的清單。每列高 34 px，圓角 12 px，位於圓角 18 px 的面板中。
7. **即時區域**：隱藏的元素，向輔助技術播報「已套用格式」或「已新增連結」等結果。

### 區塊類型與標記

文件由 `RichTextBlockType` 類型的區塊構成，行內標記則來自 `RichTextMark`。工具列與區塊選單透過 `ToggleMark`、`SetBlockType` 與 `Formats` 報告，並使用這些值。

| 類型 | 書寫方式 | 說明 |
|---|---|---|
| `Paragraph` | 純文字 | 預設區塊 |
| `Heading1`、`Heading2`、`Heading3` | `# `、`## `、`### ` | `Arc` 設定會把匯入的 h4 到 h6 變成 `Heading3` |
| `Heading4`、`Heading5`、`Heading6` | `#### `、`##### `、`###### ` | 僅 `Docs` 設定；`Heading6` 以次要文字顏色顯示 |
| `BulletedList`、`NumberedList` | `- `、`1. ` | 用 Tab 巢狀化；同一層級維持相同的清單類型 |
| `Quote` | `> ` | 起始處帶直條的引言區塊 |
| `CodeBlock` | 三個反引號 | 只保存純文字；行不換行 |
| `Divider` | `---` | 水平分隔線；`SetBlockType` 會用它取代空白區塊 |

| 標記 | 書寫方式 | 說明 |
|---|---|---|
| `Bold` | `**text**` | |
| `Italic` | `_text_` 或 `*text*` | |
| `Strikethrough` | `~~text~~` | |
| `Code` | `` `text` `` | 限定在一個區塊內 |

連結不在此清單中：`SetLink` 與連結欄位會把位址附加到選取的文字上。

### Markdown 快速輸入與支援的子集

編輯器會在輸入時把 Markdown 轉換為格式。快速輸入在所輸入的文字完成它時立即生效，因此不需要預覽即可看到結果。程式碼區塊中的快速輸入不會生效。

| 輸入 | 結果 |
|---|---|
| 位於行首的 `# `、`## `、`### ` | 一級、二級或三級標題 |
| `- `、`* `、`+ ` | 無序清單項目 |
| `1. `、`1) ` | 有序清單項目 |
| `> ` | 引言 |
| `---` | 分隔線 |
| 三個反引號 | 程式碼區塊 |
| `**text**`、`_text_`、`~~text~~`、`` `text` `` | 粗體、斜體、刪除線、行內程式碼 |

編輯器只讀寫一個固定的子集：段落、三級標題、可巢狀的有序與無序清單、引言、程式碼區塊、分隔線，以及四種行內標記與連結。貼上的 HTML 會被淨化為此子集：未知的標籤與樣式會被移除，其中的文字則保留。請把這個子集當作此功能的內容約定：需要其他內容的文件必須存放在別處。

### Docs 設定

設定 `Profile="Docs"` 即可得到文件風格的編輯器。預設的 `Arc` 設定與上文所述完全一致；`Docs` 增加較長文件所需的功能：

<Demo name="DocsProfile" />

| 功能 | 用法 | 寫出形式 |
|---|---|---|
| 底線 | `Ctrl+U`、工具列按鈕、`ToggleMark(RichTextMark.Underline)` | HTML 中的 `<u>` |
| 上標和下標 | `Ctrl+.` 與 `Ctrl+,`、工具列按鈕、`ToggleMark(RichTextMark.Superscript)` 或 `ToggleMark(RichTextMark.Subscript)`；開啟其中一個會關閉另一個 | `<sup>`、`<sub>` |
| 文字大小 | 工具列的字級按鈕（小、預設、大、特大，相對於所在區塊的文字）；`SetTextSize` | `<span data-size="large">` |
| 行距 | 區塊選單中的「Line spacing」選項（緊密、預設、寬鬆、加寬）；`SetLineSpacing` 作用於選取的區塊。程式碼區塊和表格儲存格保持自己的行距 | 段落、標題、清單項目、引言和標注段落上的 `data-spacing="relaxed"` |
| 4 到 6 級標題 | 行首輸入 `#### `、`##### ` 或 `###### `、斜線選單、區塊選單、`SetBlockType(RichTextBlockType.Heading4)` | `<h4>` 到 `<h6>`，Markdown 中為 `####` 到 `######` |
| 文字色與醒目標示色 | 工具列的顏色按鈕開啟七色調色盤；`SetTextColor`、`SetHighlight` | `<span data-color="red">`、`<mark data-bg="yellow">` |
| 待辦清單 | 行首輸入 `[] ` 或 `[x] `、斜線選單、`SetBlockType(RichTextBlockType.TodoList)`；點擊核取方塊即可勾選 | `<ul data-todo="true"><li data-checked="true">`，Markdown 中為 `- [x] ` |
| 摺疊清單 | 斜線選單或區塊選單、`SetBlockType(RichTextBlockType.Toggle)`。點擊箭頭、按 `Ctrl+Enter` 或呼叫 `ToggleFold()`，可把巢狀在它下面的項目摺疊起來；在展開的摺疊項目末尾按 Enter 會新增一個巢狀項目，`Tab` 可把任何清單項目巢狀到上方的摺疊項目下。摺疊的項目不佔空間，插入點會略過它們；伸入摺疊內容的編輯（縮排到摺疊項目下、在其末尾按 Delete）會把它展開 | `<ul data-toggle="true"><li data-collapsed="true">`；Markdown 中為一般的 `- ` 項目 |
| 文字對齊 | `Ctrl+Shift+L`、`Ctrl+Shift+E`、`Ctrl+Shift+R`；`SetAlign` | 段落、標題和引用上的 `data-align="center"` 或 `"end"` |
| `Ctrl+Shift+上方向鍵`、`Ctrl+Shift+下方向鍵` | 上移或下移插入點所在的區塊（`Docs` 設定） |
| 在摺疊項目上按 `Ctrl+Enter` | 摺疊或展開它（`Docs` 設定） |
| 程式碼語言 | 點擊程式碼區塊上方的標籤開啟語言清單；`SetCodeLanguage("csharp")`。程式碼依所選語言醒目提示（純文字或沒有語法的語言維持原樣） | `<pre data-lang="csharp">`，Markdown 中為帶資訊字串的圍欄 |
| 醒目提示區塊 | `/` 選單或區塊選單；區塊選單可設定顏色和圖示；`SetCalloutColor`、`SetCalloutIcon`。Enter 延續同一個框，空行 Enter 則離開 | `<aside data-callout="blue" data-icon="💡">`，Markdown 中為 GitHub 提示語法（`> [!NOTE]`），只保留五種提示類型 |

顏色來自固定的調色盤（`RichTextColor`：灰、紅、橙、黃、綠、藍、紫），在淺色和深色模式下隨主題變化，因此儲存的內容不含原始顏色值。`Formats` 還會回報 `IsUnderline`、`IsSuperscript`、`IsSubscript`、`TextColor`、`Highlight`、`Size`、`Align`、`LineSpacing` 和 `CodeLanguage`，供您自己的工具列使用。每行的高度取該行最大文字所需的高度，所以一個大號的詞會讓整個區塊的行距變大。

```xml
<arc:RichTextEditor Profile="Docs" Html="{Binding Body, Mode=TwoWay}" />
```

- **Markdown 對顏色、字級和行距是有損的。** Markdown 沒有這些設定，所以 `GetMarkdown()` 保留文字、捨棄它們。底線、上標和下標在 Markdown 中也沒有語法，會寫成行內 `<u>`、`<sup>` 與 `<sub>` 標籤，`Docs` 設定讀取時會還原。內容必須保留顏色時，請儲存 `Html`。
- **`Profile` 的指派順序無關緊要。** 在設定之前指派的內容，會依 Docs 規則重新讀取。
- **自訂語言。** 語言清單末尾有一列「自訂…」，可輸入任何名稱；`CodeLanguages` 可用您自己的名稱和 id 取代整份清單，例如 `new RichTextCodeLanguage("Zig", "zig")`。醒目提示需要該 id 有對應的語法；沒有語法的名稱會被保留並顯示，程式碼維持原樣。
- **別處的工作清單可以匯入。** Markdown 的 `- [ ]`、`- [x]` 項，以及以核取方塊 input 開頭的 HTML 清單項，會成為待辦項；在 `Arc` 設定下它們仍是一般清單項。
- **其餘行為不變。** 分隔線、引用、連結、斜線選單和復原歷史與 `Arc` 設定一致，`Arc` 設定寫出的文件可原樣載入。

### 圖片與表格

`Docs` 設定檔還能儲存圖片和表格。圖片是一個區塊，儲存位址與描述；表格是儲存格網格，新建的表格帶一列表頭。兩者都可透過 Markdown（單獨一行的 `![alt](src)`、管線表格）和 HTML（`img`、`table`）讀入與寫出。`Arc` 設定檔不把它們放進文件。

<Demo name="ImagesAndTables" />

| 功能 | 用法 |
|---|---|
| 插入圖片 | `/` 後選擇 Image（檔案選擇器）、貼上圖片檔或螢幕截圖、把圖片檔拖入編輯器、貼上單獨的圖片位址、輸入 `![alt](位址)`，或呼叫 `InsertImage(位址, alt)` |
| 調整圖片大小 | 點擊圖片，然後拖曳右下角的控點；比例保持不變，寬度會被儲存（HTML 中的 `width`；Markdown 會把調整過的圖片寫成 `<img>` 標籤） |
| 選取並刪除圖片 | 點擊圖片，然後按 Backspace 或 Delete；在下一段開頭按 Backspace 也會刪除 |
| 插入表格 | `/` 後選擇 Table（3×3），或 `InsertTable(列數, 欄數)` |
| 在表格中移動 | Tab 與 Shift+Tab 在儲存格間移動（在最後一格按 Tab 會新增一列）；Enter 向下移動一格；上下方向鍵保持在同一欄 |
| 編輯列與欄 | 在儲存格上按右鍵，或呼叫 `AddTableRow`、`AddTableColumn`、`DeleteTableRow`、`DeleteTableColumn`、`DeleteTable` |
| 合併與拆分儲存格 | 拖曳選取多個儲存格後按右鍵選擇「Merge cells」，或呼叫 `MergeTableCells`；在合併後的儲存格上按右鍵選擇「Split cell」，或呼叫 `SplitTableCell`。`CanMergeTableCells` 與 `CanSplitTableCell` 可告訴您自己的工具列目前是否可用 |
| 調整欄寬 | 拖曳兩欄之間的框線；按兩下框線、選擇「Distribute columns」或呼叫 `DistributeTableColumns` 可重新平分寬度 |
| 表頭列與表頭欄 | 按右鍵切換「Header row」或「Header column」，或呼叫 `SetTableHeaderRow` 與 `SetTableHeaderColumn`；用 `TableHasHeaderRow` 與 `TableHasHeaderColumn` 讀取 |
| 儲存格顏色 | 選取儲存格後按右鍵選擇顏色，或呼叫 `SetTableCellColor`；`TableCellColor` 讀取它們共同的顏色 |
| 選取儲存格、列和欄 | 從一個儲存格拖曳到另一個儲存格即可選取兩者之間的矩形；按一下控點（欄的頂端、列的左側），或呼叫 `SelectTableRow` 與 `SelectTableColumn` |
| 移動列和欄 | 把控點拖曳到新位置，按右鍵選擇「Move row up」「Move row down」「Move column left」或「Move column right」，或呼叫 `MoveTableRow` 與 `MoveTableColumn`（`CanMoveTableRow` 與 `CanMoveTableColumn` 告訴您的工具列能否移動） |
| 複製和貼上儲存格 | 複製矩形選取範圍得到以定位字元分隔的文字，試算表可直接讀取；把表格或以定位字元分隔的文字貼到儲存格中，會從該儲存格開始填入，並視需要新增列和欄 |
| 欄對齊 | `Ctrl+Shift+L`、`Ctrl+Shift+E`、`Ctrl+Shift+R` 或 `SetAlign` 會讓游標所在儲存格的整欄對齊 |

**圖片存放在哪裡由你決定。** 編輯器從不上傳任何內容。當使用者選擇、貼上或拖入圖片時，它會觸發 `ImageSourceRequested`，附帶檔名、媒體類型與位元組（`OpenReadAsync`、`ReadAllBytesAsync`）。已知位址時用 `Source` 回應，上傳進行中時用 `SourceTask` 回應；工作完成後編輯器才插入圖片，工作失敗或傳回 null 則不插入。設定 `Cancel` 可拒絕該圖片。沒有處理常式時，圖片會以 `data:` 位址內嵌，文件自成一體但體積較大。

```csharp
editor.ImageSourceRequested += (_, e) =>
{
    e.SourceTask = UploadAsync(e.FileName, e.ContentType, e.OpenReadAsync());   // 傳回 CDN 位址
};
```

**圖片如何載入也可以由你決定。** 編輯器需要某個位址的像素時會觸發 `ImageLoading`。設定 `Bitmap` 或 `StreamTask` 即可自行載入，例如來自需要權杖的儲存體或自訂協定；沒有回應時，編輯器會讀取 `data:`、http(s)、`avares:` 與本機檔案位址。`ReloadImages()` 會清除已載入的內容。

- **位址會被檢查。** 內容載入與插入圖片時，指令碼協定以及非點陣圖的 `data:` 位址會被拒絕；自訂協定（`app://files/1`）可以通過。
- **圖片會縮小到編輯器寬度**並保持比例。圖片載入中或載入失敗時，會顯示同寬的佔位。
- **儲存格只儲存行內文字**：標記、連結與換行（Shift+Enter），不含清單或巢狀區塊。匯入的這類內容會被壓平成多行。較短的列會被補齊。
- **合併儲存格**可跨越多列多欄，被合併的文字會連成多行。HTML 以 `colspan` 與 `rowspan` 儲存；Markdown 沒有合併儲存格，會把被覆蓋的儲存格寫成空的，合併必須保留時請儲存 `Html`。Tab 會跳過被覆蓋的儲存格，Enter 會移到合併儲存格的下方。
- **欄寬是比例**：表格始終佔滿編輯器寬度，沒有設定寬度的欄平分其餘欄留下的空間（每欄至少 48 px）。HTML 以 `<col width>` 儲存欄寬、以 `data-bg` 儲存儲存格顏色，並把表頭儲存格寫成 `th`；第一列沒有 `th` 儲存格的 `table` 讀入後沒有表頭列。Markdown 不保留這些設定，其表格總有表頭列。
- **選取的矩形儲存格**以整塊顯示。刪除或輸入會清空這些儲存格，不會合併它們；貼上會從第一個儲存格開始填入，並略過被覆蓋的儲存格。從表格第一個字元選到最後一個字元，或選取範圍超出表格時，仍會刪除整個表格。
- **合併儲存格會把它跨越的列或欄連在一起**：如果有合併儲存格跨過某一列，或跨過它要移到的位置，這一列就不能移動；欄也一樣。無論哪一列移到最上面，第一列始終是表頭列。

### 區塊控點

在 `Docs` 設定下，滑鼠停在某個區塊上時，其左側的邊欄會出現兩個按鈕。拖曳控點（⋮⋮）可把區塊移到新位置：一條線指示落點，清單項會帶著巢狀項一起移動，表格則整體移動。點擊控點會開啟區塊選單，可把區塊轉為其他類型（文字、標題、清單、待辦、引用、程式碼），或複製、刪除。加號按鈕會在下方新增一個空段落，並在那裡開啟區塊選單。鍵盤上，`Ctrl+Shift+上方向鍵` 和 `Ctrl+Shift+下方向鍵` 移動插入點所在的區塊。每次移動都是一個復原步驟。

在 `IsReadOnly` 模式和 `Arc` 設定下不顯示控點，文字與編輯器邊緣齊平。邊欄是書寫面左側 56 px 的外邊距。

### 尋找與取代

`Ctrl+F` 會在編輯器右上角開啟尋找列，並填入目前選取的文字（限一行之內的片段）。輸入時所有相符項目都會醒目提示，選取範圍處或之後的第一個相符項目會被選取；`Enter` 和 `Shift+Enter`（或 `F3` 和 `Shift+F3`）在其餘相符項目之間跳轉，尋找列會顯示目前位置（如「2 of 5」）。`Ctrl+H` 或尋找列上的取代按鈕會展開取代列：在其中按 `Enter` 會取代目前的相符項目並跳到下一個，「全部取代」會把所有相符項目當作一步復原一起取代。`Escape` 關閉尋找列並回到內文，最後一個相符項目保持選取。

- **相符項目不跨區塊。** 段落、清單項目、表格儲存格和程式碼區塊各自單獨搜尋，所以被拆在兩個區塊裡的詞組找不到。
- **取代後的文字沿用被取代文字的樣式**：即相符項目第一個字元的標記、顏色和連結。
- **大小寫。** 預設不區分大小寫，開啟「Match case」後才區分。
- **在程式碼中使用**：`OpenFind(replace)`、`CloseFind()`、`IsFindOpen`、`CountMatches(text, matchCase)` 和 `ReplaceAll(text, replacement, matchCase)`，後者傳回取代的數量。唯讀編輯器可以尋找，但不會取代。

### 預留位置文字與區塊提示

`Placeholder` 在空白的編輯器中顯示，說明此欄位用來寫什麼。`BlockHint` 顯示在插入點所在的空白行上，告訴使用者如何加入結構。

<Demo name="Placeholder" />

當欄位是留言或備註時，兩者都應設定，讓空白狀態說明下一步該做什麼。預留位置文字要簡短：開始輸入文字後它就會消失，區塊提示也隨之消失。

### 唯讀

設定 `IsReadOnly` 後，編輯、工具列與快速鍵都會關閉。文字仍可選取與複製，輔助技術也會讀取。

<Demo name="ReadOnly" />

對於已發布的文章、事後檢討或需要讓人引用的簽署備註，請使用唯讀模式。在編輯器外提供一個編輯操作，把 `IsReadOnly` 切回 `False`，讓修改是經過考量的。

### 內容格式

以消費者讀取的格式儲存內容：

| 格式 | 讀取方式 | 用途 |
|---|---|---|
| Markdown（`Markdown`、`GetMarkdown()`） | 你的 Markdown 渲染器或編輯器 | 流程中可編輯的文字、差異比對與版本歷史 |
| HTML（`Html`、`GetHtml()`） | 瀏覽器或 Web 檢視 | 直接渲染內容的頁面 |
| 純文字（`GetText()`） | 搜尋與預覽 | 每個區塊一行，不含標記 |

`Html` 與 `Markdown` 在載入時都會被淨化。`SetHtml` 與 `SetMarkdown` 會把文字載入為一個復原步驟。為 `Html` 或 `Markdown` 屬性指派值時，只有當值與編輯器上次寫出的值不同，文件才會重新載入，因此雙向繫結不會在使用者輸入時重設插入點。

### 匯入既有的 Markdown

編輯器只儲存固定的子集，因此開啟別處寫好的檔案可能改變它。`RichTextEditorMarkdown.Analyze` 報告編輯器無法儲存的每個結構及其行、欄與原因，不做任何轉換。`TrySetMarkdown` 只在沒有任何損失時匯入，拒絕時不改動編輯器，宿主因此可以保留原始檔案、提出警告或先開啟原始碼編輯器。

<Demo name="ImportCheck" />

```csharp
if (!editor.TrySetMarkdown(fileText, out var report))
{
    Warn($"{report.Issues.Count} constructs would be lost; the file is unchanged.");
    foreach (var issue in report.Issues.Where(i => i.Severity == RichTextImportSeverity.Lossy))
    {
        Warn($"Line {issue.Line}, column {issue.Column}: {issue.Reason}");
    }
}
```

| 報告為損失 | 原因 |
|---|---|
| 4 到 6 級標題（`Arc` 設定） | `Arc` 設定只有三級標題，它們會變成三級標題 |
| 程式碼圍籬的資訊字串 | 編輯器不儲存程式碼區塊的語言 |
| 波浪線圍籬 | 編輯器只辨識反引號圍籬 |
| 表格 | `Docs` 設定檔會儲存；`Arc` 設定檔下儲存格會合併為一個段落 |
| setext 標題 | 編輯器只辨識 `#` 標題 |
| 縮排程式碼 | 編輯器只辨識圍籬程式碼 |
| 起始編號大於 1 的清單 | 編輯器從 1 開始編號 |
| 強制換行、區塊 HTML、行內文字中的圖片（`Arc` 設定檔下的所有圖片）、參照式連結、註腳、反斜線轉義 | 編輯器把來源文字保留為文字 |

以其他寫法表示的標記會報告為 `RichTextImportSeverity.Normalized`，不阻止匯入：`*` 與 `+` 項目符號、`1)` 編號、`__粗體__`、`*斜體*` 和 `***` 分隔線會分別寫回為 `-`、`1.`、`**`、`_` 和 `---`。支援的子集內的內容不會產生任何問題。寬鬆路徑保持不變：`Markdown`、`SetMarkdown` 與形似 Markdown 的貼上文字仍按原來的方式轉換，只有主動呼叫 `TrySetMarkdown` 的呼叫端會經過這道門。

### 讀取內容

每次編輯、復原、重做與載入之後都會執行 `ContentChanged`。其 `Value` 是 `RichTextValue`，HTML、Markdown 與文字會在第一次讀取時計算。`GetHtml()`、`GetMarkdown()` 與 `GetText()` 可隨時讀取內容，且一律是同步的。

| `RichTextValue` 的成員 | 型別 | 意義 |
|---|---|---|
| `Html` | `string` | 內容的乾淨 HTML |
| `Markdown` | `string` | 內容的 Markdown |
| `Text` | `string` | 純文字，每個區塊一行 |
| `IsEmpty` | `bool` | 文件沒有文字時為 true |

<Demo name="MarkdownAndHtml" />

```csharp
editor.ContentChanged += (_, e) =>
{
    draft.Markdown = e.Value.Markdown;
    canPost = !e.Value.IsEmpty;
};

var html = editor.GetHtml();
```

### 自訂工具列

`Formats`（`RichTextFormats` 記錄）回報插入點處的標記、連結與區塊類型。`CanUndo` 與 `CanRedo` 回報復原歷程。用它們設定你自己按鈕的狀態，並呼叫 `ToggleMark`、`SetBlockType`、`SetLink` 與 `EditLink` 來作用於選取範圍。

將 `UndoCommand` 與 `RedoCommand` 繫結到按鈕，或直接呼叫 `Undo()` 與 `Redo()`。工具列按鈕不應取得焦點，以便編輯器保留插入點；範例為此在每個按鈕上設定了 `Focusable="False"`。

<Demo name="OwnToolbar" />

### 繫結到檢視模型

在 MVVM 應用程式中，檢視模型擁有文件。將 `Markdown` 雙向繫結，從 `ContentChanged` 讀取 `IsEmpty`，並在檢視中使用復原與重做命令。

```csharp
public partial class ReleaseNoteViewModel : ObservableObject
{
    private readonly IReleaseNotes _notes;

    [ObservableProperty]
    private string? _bodyMarkdown;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(PublishCommand))]
    private bool _hasContent;

    public ReleaseNoteViewModel(IReleaseNotes notes) => _notes = notes;

    [RelayCommand(CanExecute = nameof(CanPublish))]
    private async Task PublishAsync()
    {
        await _notes.PublishAsync(BodyMarkdown ?? string.Empty);
    }

    private bool CanPublish() => HasContent;
}
```

```xml
<StackPanel Spacing="16">
    <arc:RichTextEditor x:Name="Editor"
                        Markdown="{Binding BodyMarkdown, Mode=TwoWay}"
                        Placeholder="描述此次發佈" />
    <arc:Button Content="發佈說明"
                Command="{Binding PublishCommand}"
                IsDefault="True" />
</StackPanel>
```

```csharp
// Code behind: 編輯器透過 ContentChanged 回報是否為空；vm 是檢視的 ReleaseNoteViewModel。
Editor.ContentChanged += (_, args) => vm.HasContent = !args.Value.IsEmpty;
```

- `Markdown`（以及 `Html`）是雙向的：檢視模型會收到每一次編輯，而當檢視模型設定了不同的值時，編輯器會重新載入。對於長文件，該值會在短暫停頓後跟隨輸入（見效能一節），因此應在發佈命令中呼叫 `GetMarkdown()`，而不是信任快取的副本。
- `Placeholder` 與 `BlockHint` 是單向的；`IsReadOnly` 也是單向的，通常跟隨已發佈的狀態。
- `UndoCommand` 與 `RedoCommand` 是供你自己的工具列使用的命令。`CanUndo` 與 `CanRedo` 是唯讀的，並會引發變更通知。
- 判斷是否為空、統計數量或自動儲存時，應監聽 `ContentChanged`。在處理常式中使用 `Value`；HTML 與 Markdown 會在第一次讀取時計算。

### 復原與歷程

編輯器保有自己的歷程，最多 300 步，與平台的復原相互獨立。一秒之內的輸入會合併為一步；空白或輸入類型的改變會開始新的一步，每個命令（標記、區塊類型、連結）各佔一步。`SetHtml`、`SetMarkdown` 與 `Clear` 各佔一步，因此載入也可以復原。編輯器啟動時設定 `Markdown` 或 `Html` 屬性不會進入歷程。

### 長文件與效能

編輯器只重繪發生變化的區塊，並且只繪製可視範圍之內及其附近的那一部分文件。當文件不超過 400 個區塊時，每次編輯後都會寫出 HTML 與 Markdown 的值。較長的文件則會在使用者停頓約 250 ms、或編輯器失去焦點或被移除時才寫出。在 5,000 個區塊的文件中，序列化會等待停頓，因此輸入保持輕快。

`GetHtml()`、`GetMarkdown()` 與 `GetText()` 總是序列化目前的文件，因此應在儲存時呼叫，而不是在每次按鍵時呼叫。

### 範例：發行說明草稿

發行負責人為開發者工具的某個版本撰寫說明。預留位置文字告訴作者這裡應該寫什麼，正文已有一個標題、一個清單、一處粗體短語，以及一段引言形式的已知問題；發佈按鈕位於其下方，使用預設尺寸。編輯器中的文字就是發行流程儲存的同一份 Markdown。

![發行說明草稿，包含標題、粗體與清單文字，以及一段引言形式的已知問題](images/ReleaseNotes.png)

```xml
<StackPanel Width="640" Spacing="16">
    <TextBlock Text="Release notes"
               FontSize="18"
               FontWeight="SemiBold"
               Foreground="{arc:ArcDesignTokenResource Foreground}" />
    <Border Padding="24,20"
            CornerRadius="26"
            BorderThickness="1"
            Background="{arc:ArcDesignTokenResource Surface}"
            BorderBrush="{arc:ArcDesignTokenResource Border}">
        <arc:RichTextEditor Markdown="## Acme Analytics 4.2&#10;&#10;- Dashboards load **twice as fast**&#10;- Export charts as _PNG_ from the menu&#10;&#10;> Known issue: CSV exports above 500 MB can time out." />
    </Border>
    <StackPanel Orientation="Horizontal" Spacing="8" HorizontalAlignment="Right">
        <arc:Button Content="Discard" Variant="Secondary" />
        <arc:Button Content="Publish notes" />
    </StackPanel>
</StackPanel>
```

### 文案規範

- **預留位置文字**：說明此欄位保存什麼內容以及應該寫什麼，例如「描述此次發佈」，而不是「輸入文字」。
- **區塊提示**：點出最關鍵的一個手勢，例如「輸入 / 插入區塊」。不要列出所有快速鍵。
- **按鈕**：使用動詞加受詞的句首大寫，例如「發佈說明」「儲存草稿」。對於會被發佈的說明，避免使用「送出」，因為使用者期望文字即將上線。
- **連結文字**：寫使用者所讀的文字，把 URL 放在連結欄位中，而不是放進句子裡。
- **標題**：保持簡短並使用句首大寫；文件外部已有標題時，文件內的段落使用 `Heading2`。

| 不要寫 | 改為 |
|---|---|
| 在此輸入文字 | 描述此次發佈 |
| 點這裡 | 閱讀遷移指南 |
| 送出 | 發佈說明 |
| 輸入 /// 插入區塊 | 輸入 / 插入區塊 |

### 最佳做法

**建議**

- 為每個空白欄位設定 `Placeholder` 與 `BlockHint`，讓第一行就說明編輯器的用法。
- 內容會被再次編輯時儲存 `Markdown`；當另一個頁面只負責渲染時儲存 `Html`。對於支援的子集兩者等價，因此應選定其中一個作為事實來源。
- 在儲存命令中用 `GetMarkdown()` 或 `GetHtml()` 讀取內容。`ContentChanged` 的處理常式適合判斷是否為空與統計數量，而不適合處理每一次寫入。
- 已發佈的文件切換為 `IsReadOnly`，並提供一個明確的編輯操作將其重新開啟。
- 當工具列按鈕作用於選取範圍時，像自訂工具列範例那樣讓它們移出 Tab 順序，使插入點留在文字中。

**避免**

- 儲存來自其他編輯器的 HTML 並期望其樣式得以保留。載入器只保留編輯器的子集，未知的標籤與行內樣式會被捨棄，版面也隨之改變。
- 在 `Arc` 設定檔下使用圖片和表格。只有 `Docs` 設定檔會儲存它們，下次儲存時內容會遺失。
- 在長文件中每次按鍵都讀取 `GetHtml()`。每次按鍵時的序列化比編輯本身更耗時。
- 每次變化時都指派 `Markdown` 來取代正在編輯的文件。請使用 `SetMarkdown()` 或 `Clear()`，它們各佔一個復原步驟，使用者可以復原這次取代。
- 期望螢幕閱讀器使用者依字元、詞或行瀏覽文字。輔助技術會把整份文件當作一個值讀取，因此文字較長時應提供 `GetText()` 的輸出供審閱。

### 常見問題

**為什麼 Ctrl+U（macOS 上為 Cmd+U）沒有任何作用？** 在預設的 `Arc` 設定下，底線不屬於編輯器的子集。該按鍵會被吞掉，因此文字不會改變，而 Markdown 輸出中也沒有底線語法。設定 `Profile="Docs"` 後，`Ctrl+U` 會為選取範圍加底線。

**如何在程式碼中新增或修改連結？** 呼叫 `SetLink(url)` 為選取範圍或插入點處的連結設定連結。傳入 `null` 或空字串可以移除連結。`EditLink()` 會開啟與工具列相同的連結欄位，作用於選取範圍或插入點處的連結。鍵盤上的 Ctrl+K（macOS 上為 Cmd+K）效果相同。

**為什麼我再次設定相同的 HTML 時，繫結的 `Html` 沒有重新載入？** 編輯器只在值與其上次寫出的值不同時才會重新載入。因此回顯編輯器自身輸出的雙向繫結不會移動插入點。若要以相同內容強制重新載入，請改為呼叫 `SetHtml` 或 `SetMarkdown`。

**為什麼快速輸入不會在程式碼區塊中轉換文字？** 程式碼區塊只保存純文字。`# ` 與 `**` 等快速輸入在其中不生效，Tab 插入兩個空白，而不是改變清單層級。

**如何在清單項目或引言中開始新的一行？** 在同一個區塊中按 Shift+Enter 換行。在空白的清單項目或引言上按 Enter 可以離開清單；在程式碼區塊末尾連按兩次 Enter 可以離開程式碼區塊。

## API 參考

<!-- api: RichTextEditor -->

## 無障礙

- 書寫區是一個多行可編輯文字控制項，預設名稱為「編輯器」。設定 `AutomationProperties.Name` 可為其在表單中命名，例如「發行說明內文」。
- 鍵盤（macOS 上為 Cmd）：`Ctrl+B`、`Ctrl+I`、`Ctrl+Shift+X` 與 `Ctrl+E` 套用標記，`Ctrl+K` 編輯連結。`Ctrl+Z` 與 `Ctrl+Shift+Z` 復原與重做。`Alt+F10` 將焦點移入浮動工具列，`Escape` 則返回文字。
- 編輯結果，如「已套用格式」「已新增連結」「已移除連結」「已復原」「已重做」與「已新增編號清單」，會以禮貌的方式播報，不會打斷語音。
- 浮動工具列是一個工具列，其格式按鈕是切換按鈕，會回報自身的狀態。它們的工具提示會顯示鍵盤快速鍵。
- 區塊選單是一個清單。瀏覽時會播報目前項目。
- 限制：編輯器不向螢幕閱讀器提供字元、詞或行的導覽，它們會把整段文字當作一個值讀取。

### 鍵盤

| 按鍵 | 操作 |
|---|---|
| `Ctrl+B`、`Ctrl+I` | 切換粗體、斜體 |
| `Ctrl+Shift+X` | 切換刪除線 |
| `Ctrl+E` | 切換行內程式碼，限於一個區塊內 |
| `Ctrl+K` | 為選取範圍或插入點處的連結開啟連結欄位；`Enter` 套用，`Escape` 關閉 |
| `Ctrl+F`、`Ctrl+H` | 開啟尋找列，或開啟帶取代列的尋找列 |
| `F3`、`Shift+F3` | 尋找列開啟時跳到下一個、上一個相符項目（在搜尋欄位中為 `Enter` 和 `Shift+Enter`） |
| `Ctrl+U` | 切換底線（`Docs` 設定） |
| `Ctrl+.`、`Ctrl+,` | 切換上標、下標（`Docs` 設定） |
| `Ctrl+Shift+L`、`Ctrl+Shift+E`、`Ctrl+Shift+R` | 靠左、置中、靠右對齊（`Docs` 設定） |
| `Ctrl+Z`、`Ctrl+Shift+Z`（或 `Ctrl+Y`） | 復原、重做 |
| `Ctrl+A` | 全選 |
| `Enter` | 新增區塊；在空白的清單項目或引言上離開清單；在程式碼區塊末尾連按兩次則離開程式碼 |
| `Shift+Enter` | 在區塊內換行 |
| `Tab`、`Shift+Tab` | 縮排或取消縮排清單項目；在程式碼區塊中 `Tab` 插入兩個空白 |
| 在區塊開頭按 `Backspace` | 將標題、引言、程式碼區塊或清單項目轉為段落，或取消其縮排；位於分隔線之後時刪除分隔線 |
| 行首或空白之後的 `/` | 開啟區塊選單；輸入即可篩選，`Up` 與 `Down` 移動，`Enter` 或 `Tab` 選擇，`Escape` 關閉 |
| `Alt+F10` | 將焦點移入浮動工具列；`Left` 與 `Right` 在其按鈕之間移動，`Escape` 返回文字 |

Ctrl 在 macOS 上為 Command，在其他平台上為 Control。單字移動遵循平台的文字導覽（macOS 上為 Option，其他平台為 Control）。在 `Arc` 設定下 `Ctrl+U` 會被吞掉，因為該設定不支援底線；`Docs` 設定則用它加底線。

## 動態效果

- 浮動工具列會以輕微回彈滑動，跟隨選取範圍。在格式按鈕與連結欄位之間切換時，其尺寸會變形，兩個介面向相反方向相互滑過。
- 區塊選單從 `/` 所在位置開啟。醒目提示會在各列之間滑動；篩選改變清單時，其高度以平滑彈簧變化。
- 減少動態效果時，工具列的移動與尺寸調整一步完成，兩個介面交叉淡化。區塊選單只淡入淡出，不縮放也不移動。

| 變化 | 完整 | 減少 | 關閉 |
|---|---|---|---|
| 工具列跟隨新的選取範圍 | 帶輕微回彈滑動到新位置 | 直接跳到該位置 | 立即 |
| 工具列切換到連結欄位或從其切換回來 | 尺寸變形，兩個介面相互滑過 | 位置與尺寸瞬間完成；兩個介面交叉淡化 | 立即 |
| 區塊選單開啟 | 從 `/` 所在位置彈出 | 淡入 | 立即 |
| 區塊選單的醒目提示移動 | 在各列之間滑動 | 一步移動 | 立即 |
| 篩選時選單清單變化 | 高度以平滑彈簧變化 | 立即 | 立即 |

## 相關元件

- `Textarea`：沒有格式的純文字。
- `MentionInput`：提及人員或標籤的文字。
- `CodeBlock`：用於顯示程式碼，而非撰寫程式碼。
- `FileDropzone`：與文字一同提供的圖片與檔案。

| 元件 | 適用情境 |
|---|---|
| `Textarea` | 文字是純文字並跨多行，沒有粗體、清單或區塊 |
| `MentionInput` | 文字提及人員，且提及內容需要建議並儲存 |
| `InlineEdit` | 在顯示的位置重新命名單一標題或名稱 |
| `CodeBlock` | 程式碼用於顯示、複製或閱讀，而非撰寫 |
| `FileDropzone` | 圖片或檔案伴隨文字出現，且必須上傳 |
| `Input` | 值是表單中的一行，例如主旨或標題 |
