---
title: PDF 檢視器
summary: 檢視 PDF，帶縮圖、目錄、搜尋、文字選取、連結和密碼提示；桌面上由 PDFium 繪製，瀏覽器中由 pdf.js 繪製。
source-hash: 0f5b8e340c0d
---

<Demo name="Preview" />

## 何時使用

`PdfViewer` 在應用程式內開啟 PDF 檔案。它在 `PageViewer` 之上增加了帶縮圖和目錄的側邊欄、搜尋、文字選取、連結和密碼提示，並有自己的載入和失敗狀態。

- **業務工具中的合約和發票**：職員閱讀文件，搜尋姓名，複製一個數字。
- **桌面應用程式中的手冊和報告**：讀者透過目錄跳轉，並沿連結在章節間移動。
- **Web 應用程式中產生的檔案**：使用者不離開頁面就能檢查伺服器產生的 PDF。
- **受保護的文件**：讀者在檢視器中輸入密碼，或由你的應用程式提供。

## 何時不用

- **編輯 PDF**：註解、表單填寫、數位簽章、列印、檢視旋轉和雙頁模式都不在此版本內。
- **指令碼和 XFA 表單**：文件中的指令碼從不執行，也不支援 XFA 表單。
- **圖片或你自己繪製的頁面**：用主套件中的 `PageViewer`，不需要原生函式庫。
- **需要重排的文字**：用 `Markdown`。PDF 頁面的版面是固定的，手機上的讀者只能縮放和平移。

## 用法

加入 `TKCode.Arc.Pdf` 和一個後端，註冊套件，並在啟動時啟用後端：

```csharp
AppBuilder.Configure<App>()
    .UseArc(arc => arc.UseArcControls().UseArcPdf());

ArcPdfNative.Use();              // desktop: TKCode.Arc.Pdf.Native
await ArcPdfBrowser.UseAsync();  // browser: TKCode.Arc.Pdf.Browser
```

把 `Source` 設為絕對位址或路徑。`http`、`https` 和 `avares` 位址都可以，檔案路徑也可以。在程式碼中，`PdfSource` 還能開啟位元組和資料流。

```xml
<arc:PdfViewer Source="avares://MyApp/Assets/report.pdf" Height="600" />
```

```csharp
viewer.Source = PdfSource.FromFile(path);
viewer.Source = PdfSource.FromBytes(bytes);
viewer.Source = PdfSource.FromStream(() => File.OpenRead(path));
viewer.DocumentOpened += (_, e) => status.Text = e.Document.Info.Title;
viewer.DocumentFailed += (_, e) => status.Text = e.Error.Kind.ToString();
```

`PdfViewer` 衍生自 `PageViewer`，所以縮放、版面、平移、`CurrentPageIndex` 和工具列的用法相同。`Status` 表示目前狀態：`Empty`、`Loading`、`Ready`、`PasswordRequired` 或 `Failed`。沒有顯示文件時，工具列隱藏頁碼、縮放、側邊欄和搜尋控制項；已開啟的側邊欄或搜尋列會隨下一個文件回來。

`Document` 顯示你自己開啟的 `PdfDocument`。它仍歸你所有，檢視器從不釋放它；兩者都設定時 `Source` 優先。`SimulatedPdfDocument` 提供產生的頁面，用於測試和設計預覽。

### 結構

1. **工具列**：`PageViewer` 的工具列，開頭是側邊欄按鈕，結尾是搜尋按鈕。
2. **側邊欄**：縮圖和目錄，分兩個頁籤。沒有目錄的文件只顯示縮圖。
3. **頁面**：白紙，淺色和深色主題下都一樣。檢視器不反轉頁面顏色。
4. **搜尋列**：浮在右上角，帶計數、上一處和下一處、大小寫須相符、全字和關閉。
5. **狀態覆蓋層**：開啟時的載入動畫、失敗後的原因，以及密碼表單。

### 側邊欄

`IsSidebarOpen` 顯示側邊欄，`SidebarMode` 選擇 `Thumbnails` 或 `Outline`；兩者都是雙向繫結。縮圖從與頁面相同的快取中按需繪製，點按目錄項目會跳到對應位置。

<Demo name="Sidebar" />

### 搜尋

`Ctrl` 或 `Cmd` + `F` 開啟搜尋列。`SearchText` 在停止輸入 300 毫秒後開始搜尋，從目前頁面向後並環繞。每頁完成就回報，所以 `IsSearching` 為 true 時 `SearchMatchCount` 不斷增長；有了第一個結果，檢視器就跳過去。`IsMatchCase` 和 `IsWholeWord` 縮小搜尋範圍，中文、日文和韓文沒有全字比對。`SearchNext`、`SearchPrevious` 和雙向的 `CurrentSearchMatch` 在結果之間移動。

<Demo name="Search" />

### 選取文字

在文字上拖曳即可選取，按兩下選字，按三下選行。Shift 加點按擴充選取範圍，選取範圍可以跨頁。`SelectedText` 保存所選文字；`Ctrl` 或 `Cmd` + `C` 和右鍵選單可以複製，`CopySelection`、`SelectAll` 和 `ClearSelection` 在程式碼中完成相同動作。`IsTextSelectionEnabled` 關閉以上全部功能。

此版本的文字選取不支援觸控拖曳：手指捲動頁面，點兩下選字。

### 連結

點按連結會引發 `LinkActivated`；設定 `Handled` 可改做別的事。否則文件內部的連結跳到對應頁面。外部連結只對 `http`、`https` 和 `mailto` 開啟；其他協定（如 `file:` 或 `javascript:`）一律不開啟，因為文件不可信。`ExternalLinkBehavior="None"` 把外部連結交給你的處理常式。

```csharp
viewer.LinkActivated += (_, e) =>
{
    if (e.Link.Uri is { } address)
    {
        e.Handled = true;
        ConfirmAndOpen(address);
    }
};
```

### 加密文件

帶密碼的文件會顯示表單，密碼錯誤時表單抖動並給出提示。要由應用程式回答，請處理 `PasswordRequested`：設定 `Password`，或設定 `Cancel` 放棄；需要稍後回答時取一個 deferral。`IsRetry` 表示上一個密碼是錯的。檢視器只在開啟期間保留密碼，也從不寫入記錄。

這個範例的密碼是 `arc-sample`：

<Demo name="Locked" />

```csharp
viewer.PasswordRequested += async (_, e) =>
{
    using var deferral = e.GetDeferral();
    e.Password = await vault.GetPasswordAsync();
};
```

### 只有頁面

`Pdf.Source` 在一般 `PageViewer` 中開啟 PDF，只有頁面和縮放，沒有上面那些工具。這裡沒有密碼表單和失敗覆蓋層：`Pdf.Error` 說明文件未能開啟的原因，需要密碼也在其中。

<Demo name="PagesOnly" />

### 平台與套件

| 套件 | 內容 | 大小 | 入口 |
|---|---|---|---|
| `TKCode.Arc.Pdf` | `PdfViewer`、`Pdf.Source`、後端介面 | 0.3 MB | `UseArcPdf()` |
| `TKCode.Arc.Pdf.Native` | win、linux、osx 的 x64 和 arm64 版 PDFium | 套件 22 MB；單一平台應用程式中約 7.5 MB | `ArcPdfNative.Use()` |
| `TKCode.Arc.Pdf.Browser` | pdf.js、其 worker 和資料檔 | 套件 2 MB；伺服器上 4 MB，按需取得 | `await ArcPdfBrowser.UseAsync()` |

兩個入口在無法載入時都傳回 false，後端保持原樣。沒有後端時，文件以 `BackendUnavailable` 失敗，檢視器會說明缺少哪個呼叫，絕不顯示假頁面。

`ArcPdfBrowser.UseAsync` 從 `_content/TKCode.Arc.Pdf.Browser/pdfjs/` 匯入 pdf.js，所以宿主必須提供套件的靜態 Web 資源，並為 `.mjs` 檔案設定 JavaScript 類型。要從別處提供這些檔案，請傳入位址。

需要預先考慮的限制：

- **字型**：沒有內嵌中文、日文或韓文字型的文件依賴本機字型。在沒有字型的 Linux 機器上，這些文字顯示為空白。
- **瀏覽器中的字型**：Liberation 字型因授權原因不隨瀏覽器套件提供，所以未內嵌 Helvetica 或 Arial 的文件會回退到本機字型或瀏覽器的無襯線字型，字寬會不同。
- **觸控**：此版本的 `IsTextSelectionEnabled` 不支援觸控拖曳選取。
- **文字中的網址**：純文字寫的網址只有在 PDFium 下才算連結。在瀏覽器中，只有真正的連結註解才是連結。
- **瀏覽器中的選取**：pdf.js 按文字段回報文字，檢視器按寬度比例拆分每一段，所以選取範圍可能與字形略有偏差。
- **不可信檔案**：PDFium 在你的處理程序內剖析檔案。請保持更新，也不要把它當作處理陌生檔案的沙箱。
- **離開視覺樹**：檢視器離開視覺樹時會關閉從 `Source` 開啟的文件，回來時重新開啟，加密文件會再次詢問密碼。一步之內把檢視器移到另一個父元素則文件保持開啟。

### 內容指南

- 頁面知道文件名稱時，用 `AutomationProperties.Name` 給檢視器一個無障礙名稱。
- 文件可能沒有標題時，給 `PdfSource` 設定 `DisplayName`。`Title` 會回退到它。
- 在讀者無法離開的流程中，`DocumentFailed` 觸發時要說明下一步怎麼做。

| 不要寫 | 應該寫 |
|---|---|
| 錯誤 | 無法開啟此檔案 |
| PDF 文件 | 租賃合約 |

### 最佳實踐

**建議**

- 給檢視器一個高度，或放在有高度的父元素中。在高度無限的父元素中，它會增長到整欄頁面的高度。
- 在開啟第一個文件之前啟用後端。
- 文件中的連結需要讀者確認時，處理 `LinkActivated`。

**避免**

- 釋放檢視器從 `Source` 開啟的文件。它歸檢視器所有。
- 在 `PasswordRequested` 之後仍把密碼留在檢視模型中。
- 把檢視器當作安全邊界。它檢查連結協定，也不執行指令碼，但檔案仍在你的處理程序內剖析。

### 常見問題

**為什麼檢視器說沒有設定繪製器？** `ArcPdf.Backend` 還是預設值。請呼叫 `ArcPdfNative.Use()` 或 `await ArcPdfBrowser.UseAsync()`，並確認參考了對應平台的套件。

**為什麼中文顯示為空白？** 文件沒有內嵌字型，而本機沒有合適的字型。見上面的限制。

**為什麼又詢問密碼？** 檢視器離開了視覺樹，這會關閉文件並忘記密碼。

**為什麼深色主題下頁面是白的？** 頁面是紙。檢視器不反轉頁面顏色。

## API 參考

<!-- api: PdfViewer -->

`Pdf` 的附加屬性，用於一般 `PageViewer`：

<!-- api: Pdf -->

`PdfViewer` 還有這些方法：

| 方法 | 含義 |
|---|---|
| `SearchNext()`、`SearchPrevious()` | 跳到下一處或上一處結果，到頭後環繞。 |
| `ClearSearch()` | 結束搜尋並清空 `SearchText`。 |
| `SelectAll()`、`ClearSelection()`、`CopySelection()` | 選取目前頁面、清除選取範圍或複製選取範圍。 |

`PdfSource` 說明文件的來源：

| 成員 | 含義 |
|---|---|
| `FromFile(path)` | 檔案。不適用於瀏覽器，那裡沒有可用的檔案系統。 |
| `FromBytes(data)` | 記憶體中的位元組。 |
| `FromStream(open)` | 可定位的資料流；每次開啟呼叫一次 `open`。 |
| `FromUri(uri)` | `http` 或 `https`，或表示應用程式資源的 `avares`。 |
| `Parse(text)` | 絕對位址或路徑，XAML 用的就是它。 |
| `DisplayName` | 文件沒有標題時顯示的名稱。 |

`PdfDocument` 是已開啟的文件：一個 `IPageSource`，帶 `Info`、`GetPageLabel`、`GetOutlineAsync`、`GetPageTextAsync` 和 `GetLinksAsync`。`ArcPdf.Backend` 是開啟文件的 `IPdfBackend`；`PdfException.Kind` 是 `PdfErrorKind`：`BackendUnavailable`、`NotFound`、`Network`、`Corrupt`、`Unsupported`、`PasswordRequired` 或 `Aborted`。

## 無障礙

- 檢視區對輔助技術來說是一個文件，名稱為「Page 3 of 12」，除非 `AutomationProperties.Name` 設定了名稱。螢幕閱讀器可以透過說明文字讀取目前頁面的文字；此版本沒有完整的文字模式。
- 工具列和搜尋列的每個按鈕都有目前語言（英文、簡體中文、繁體中文）的名稱和工具提示。
- 搜尋結果數以禮貌方式播報。
- 密碼表單出現時取得焦點，Enter 和 Escape 可以作答。
- 沒有顯示文件時，工具列的控制項隱藏，不在 Tab 順序中。

### 鍵盤

檢視區有焦點時按鍵生效。

| 按鍵 | 動作 |
|---|---|
| 方向鍵 | 捲動 40 個單位 |
| `PageUp` / `PageDown` | 捲動一屏；在 `Single` 版面中翻頁 |
| `Home` / `End` | 跳到第一頁或最後一頁 |
| `Ctrl` 或 `Cmd` + `=` / `-` / `0` | 放大、縮小或回到 100% |
| 按住 `Space` 並拖曳 | 平移 |
| `Ctrl` 或 `Cmd` + `F` | 開啟搜尋並聚焦 |
| `Enter` / `Shift` + `Enter`、`F3` / `Shift` + `F3` | 下一處或上一處結果 |
| `Escape` | 關閉搜尋；否則清除選取範圍 |
| `Ctrl` 或 `Cmd` + `C` / `A` | 複製，或選取目前頁面 |
| 頁碼欄位中按 `Enter` | 跳到該頁，超出範圍時鉗制 |

## 動效

- 側邊欄開啟時寬度沿平滑彈簧變化；在窄檢視器中，它滑入並浮在頁面上。
- 搜尋列淡入並落到位。
- 密碼錯誤時表單抖動。
- 縮放和翻頁與 `PageViewer` 相同。更清晰的點陣圖直接取代舊點陣圖，文字不會閃動。

| 變化 | 完整 | 減弱 | 關閉 |
|---|---|---|---|
| 側邊欄開合 | 彈簧或滑動 | 淡入淡出 | 立即 |
| 搜尋列開合 | 淡入並下落 | 淡入淡出 | 立即 |
| 密碼錯誤 | 抖動 | 只有錯誤文字 | 只有錯誤文字 |
| 工具列縮放、`GoToPage` | 彈簧 | 立即 | 立即 |

## 響應式

- 檢視器沒有自身大小：給它一個高度，它會填滿得到的寬度。
- 寬度小於 640 時，側邊欄浮在頁面上，不再佔用頁面的寬度。
- 寬度小於 520 時，工具列只保留頁碼欄位、總頁數、縮放控制項和兩端的按鈕。
- 在觸控裝置上，手指捲動，捏合以手指為中心縮放，點兩下選字。沒有依賴懸停的功能。

## 相關元件

- `PageViewer` 用於圖片或你自己繪製的頁面。
- `VideoPlayer` 是同類的套件，每個平台有單獨的後端。
- `Markdown` 用於需要重排的文件。

| 元件 | 何時選擇 |
|---|---|
| `PageViewer` | 頁面是圖片，或由你的程式碼繪製 |
| `Markdown` | 文字應隨寬度重排 |
| `ArcImage` | 版面中的單張圖片 |
