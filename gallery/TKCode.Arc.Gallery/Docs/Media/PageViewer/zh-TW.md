---
title: 頁面檢視器
summary: 縮放、平移並翻閱圖片或按需繪製的頁面。
source-hash: 0dc771826687
---

<Demo name="Overview" />

## 何時使用

`PageViewer` 顯示一組頁面，讓讀者在頁面之間移動、放大並平移。頁面可以是集合中的圖片，也可以是 `IPageSource` 在進入視野時才繪製的頁面，例如掃描的合約或報告的各頁。它只繪製可見的頁面，所以一萬頁的文件捲動起來和短文件一樣輕快。

- **檔案工具中的掃描文件**：承辦人員逐頁閱讀掃描的表單，並放大查看簽名。
- **出版工具中的校樣**：編輯在付印前以原始大小翻閱版面的各頁。
- **由伺服器繪製的報告**：分析師捲動瀏覽一份長報告，報告的各頁在需要時才以圖片形式送達。
- **在頁面上做標記**：審閱者點一下頁面留下標記，標記由 `PageLayer` 繪製，縮放和捲動時都留在頁面上。

## 何時不用

- **瀏覽一排圖片**：用 `Carousel`，它透過拖曳在投影片之間切換。
- **同一張圖片的兩個版本**：用 `ImageCompare`，它用分隔線比較兩者。
- **版面中的一張圖片**：用 `ArcImage`。頁面檢視器帶有工具列和捲動畫布，單張圖片用不到。
- **需要重排的文字**：用 `Markdown`。頁面的版面是固定的，手機上的讀者只能縮放和平移。
- **閱讀 PDF 檔案**：用 `TKCode.Arc.Pdf` 中的 `PdfViewer`，它增加了縮圖、目錄、搜尋、文字選取、連結和密碼提示。只需要頁面時，在 `PageViewer` 上設定 `Pdf.Source`。

## 用法

給檢視器一個高度和一組圖片。`ItemsSource` 接受 `Bitmap` 物件、`ImageSource` 物件，以及字串或 `Uri` 形式的位址；集合實作 `INotifyCollectionChanged` 時會跟隨其變更。

```xml
<arc:PageViewer Height="600" FitMode="Page" />
```

```csharp
viewer.ItemsSource = new[]
{
    "https://example.com/scan-1.png",
    "https://example.com/scan-2.png"
};
viewer.CurrentPageChanged += (_, e) => status.Text = $"Page {e.NewIndex + 1}";
```

`PageViewer` 衍生自 Avalonia 的 `TemplatedControl`。它在捲動檢視器內的一個表面上自行繪製頁面，所以頁面不是控制項，也沒有逐頁的範本。

### 結構

1. **工具列**：頁碼欄位和總頁數、上一頁和下一頁、縮小、縮放選單、放大，以及版面和平移開關。`ToolbarStart` 和 `ToolbarEnd` 在兩端加入你自己的內容，`IsToolbarVisible` 可隱藏工具列。沒有頁面時，它只顯示兩端你自己的內容。
2. **畫布**：頁面背後的表面，使用柔和的表面色。
3. **頁面**：帶靜置陰影的白紙，明暗主題都一樣。沒有點陣圖的頁面顯示一個靜止的圓環，直到點陣圖送達。
4. **圖層**（`Layers`）：在每頁之上進行你自己的繪製和指標輸入。

### 版面

`LayoutMode` 預設為 `Continuous`，所有頁面排成一欄捲動。`Single` 只顯示目前頁面；工具列、`NextPage`、`PreviousPage` 和翻頁鍵負責切換。

<Demo name="SinglePage" />

`CurrentPageIndex` 是檢視區中線所在的頁面。它是雙向的，從 0 開始，設定它就會跳到該頁。`GoToPage` 作用相同，並可以關閉動畫。

### 符合與縮放

`FitMode` 讓縮放符合檢視區，並在檢視區變更時重新計算。預設的 `Width` 讓最寬的頁面填滿寬度，`Height` 讓最高的頁面填滿高度，`Page` 讓一整頁保持可見。在 `Single` 版面中它們以目前頁面為準。

<Demo name="FitWidth" />

`Zoom` 為 1 時頁面顯示為自身大小，並限制在 `MinZoom` 和 `MaxZoom` 之間。從外部設定的值會把 `FitMode` 變為 `None`，工具列和縮放鍵也一樣。`ZoomIn` 和 `ZoomOut` 在 10、25、50、75、100、125、150、200、300、400、600 和 800% 這些級距之間移動。

```csharp
viewer.ZoomIn();
viewer.ResetZoom();
viewer.FitMode = PageFitMode.Page;
```

### 平移

`InteractionMode` 決定在頁面上拖曳的效果。預設的 `Select` 把拖曳交給圖層，`Pan` 移動檢視。在任何模式下，滑鼠中鍵和按住空白鍵拖曳也能平移，手指則透過捲動檢視器平移。

<Demo name="PanMode" />

### 按需繪製

當頁面不應全部留在記憶體中時，請實作 `IPageSource`。`RenderAsync` 為可見頁面及兩側各一頁執行，離中心最近的優先。它在 UI 執行緒上呼叫，可以在任意執行緒上完成。請回應取消權杖，因為離開視野的頁面會被取消。`TargetSize` 是整頁的像素大小，即頁面大小乘以縮放再乘以螢幕縮放比例。

<Demo name="OnDemand" />

```csharp
public sealed class ReportPages : IPageSource
{
    public int PageCount => 240;

    public event EventHandler? PagesChanged;

    public Size GetPageSize(int pageIndex) => new(612, 792);

    public async ValueTask<PageImage?> RenderAsync(PageRenderRequest request, CancellationToken cancellationToken)
    {
        var bitmap = await Task.Run(() => Draw(request), cancellationToken);
        return new PageImage(bitmap);
    }
}
```

- `OwnsBitmap` 為 true 的點陣圖歸檢視器所有，淘汰後由它釋放。`RenderCacheBytes` 限制保留的位元組數（預設 192 MB）；可見頁面正在繪製的點陣圖不會被淘汰。
- 縮放進行中檢視器不發出要求，只拉伸已有的點陣圖。縮放停止 120 ms 後要求清晰的點陣圖並替換。
- 超過 1600 萬像素的頁面先得到一張設有上限的整頁點陣圖；捲動停止 120 ms 後，再發出一個設定了 `Region` 的要求，只要檢視區周圍的部分。請只繪製目標大小中的這個矩形。
- 傳回 null 或擲回例外都會讓頁面保留預留位置。在 `PagesChanged` 觸發之前，檢視器不會再次要求該點陣圖。
- 頁數或頁面大小變更時，請在 UI 執行緒上引發 `PagesChanged`。檢視器會捨棄點陣圖並重新讀取大小。

### 圖層

`PageLayer` 在每個可見頁面上繪製（裁切到頁面）並接收指標輸入。圖層按清單順序繪製；指標事件從最後一層往前分派，`Handled` 會停止分派。被處理的按下會把後續拖曳留給該圖層。

<Demo name="Marks" />

### 移動到某處

`ScrollIntoView` 以最短路徑把頁面的某部分移入畫面，已可見時不動，適合搜尋結果和連結目標。`PageHitTest` 把控制項上的一點換算為頁面和該頁上的點。

```csharp
viewer.ScrollIntoView(4, new Rect(40, 300, 200, 24));

if (viewer.PageHitTest(e.GetPosition(viewer), out var page, out var point))
{
    status.Text = $"Page {page + 1} at {point.X:0}, {point.Y:0}";
}
```

### 衍生

`PageViewer` 不是密封類別。其他組件中的衍生檢視器透過帶 `BasedOn="{StaticResource {x:Type arc:PageViewer}}"` 的控制項主題重用範本（`PdfViewer` 就是這樣做的），並使用下面這些受保護成員。

| 成員 | 用途 |
|---|---|
| `LeadingPane` | 頁面左側、工具列下方的側邊欄 |
| `ContentOverlay` | 頁面之上的內容，例如浮動搜尋列 |
| `OnViewerKeyDown` | 最先看到按鍵；傳回 true 表示接管該鍵 |
| `OnPageSourceChanged`、`OnPagesChanged` | 資料來源被替換，或其頁面發生變更 |
| `GetPageBitmap`、`OnPageBitmapReady`、`CancelPageBitmapRequests` | 從同一快取取得點陣圖，用於縮圖等其他檢視 |
| `EffectivePageSource` | 目前顯示的資料來源 |

### 文案規範

- 文件有名稱時，用 `AutomationProperties.Name` 給檢視器一個無障礙名稱，例如「租賃合約」。沒有名稱時它讀作「第 3 頁，共 12 頁」。
- 當檢視器周圍的頁面沒有顯示文件標題時，把簡短的標題放進 `ToolbarStart`。

| 不要寫 | 應寫 |
|---|---|
| 文件 | 租賃合約 |
| 圖片 1/6 | 第 1 頁，共 6 頁 |
| 檢視器 | 掃描發票 4471 |

### 最佳做法

**建議**

- 給檢視器一個高度，或放進能提供高度的父層。如果父層給出無限高度，它會撐到整欄頁面的高度並把它們全部繪製出來。
- 回報與點陣圖一致的頁面大小，這樣頁面不會在點陣圖送達時改變形狀。
- 取消權杖觸發時停止工作。快速捲動的讀者會把許多頁面拋在後面。

**避免**

- 以固定大小繪製。`TargetSize` 才是螢幕在目前縮放下需要的大小，更小的點陣圖會變模糊。
- 釋放你以 `OwnsBitmap` 為 true 傳回的點陣圖。檢視器可能還在繪製它。
- 保留 `GetPageBitmap` 傳回的點陣圖。每次繪製都重新取得，因為它可能已被淘汰。

### 常見問題

**為什麼縮放後頁面會短暫模糊？** 縮放進行中檢視器拉伸已有的點陣圖。縮放停止 120 ms 後，清晰的點陣圖會替換它。

**為什麼稍微捲動時目前頁面沒有變？** 目前頁面是檢視區中線所在的頁面。跳轉後，在檢視再次移動之前，目標頁一直是目前頁面，即使中線落在另一頁上，例如文件的最後一頁。

**為什麼圖片在很高的縮放下不會變清晰？** 圖片只有自身的像素，`ImagePageSource` 沒有細節層。能以任意大小繪製的 `IPageSource` 則始終清晰。

**為什麼深色主題下頁面仍是白色？** 頁面就是紙張。檢視器不會反轉頁面的顏色。

## API 參考

<!-- api: PageViewer -->

`PageChangedEventArgs` 是 `CurrentPageChanged` 的事件資料：

| 成員 | 意義 |
|---|---|
| `OldIndex` | 之前的目前頁面。 |
| `NewIndex` | 現在的目前頁面。 |

`PageViewer` 還有以下方法：

| 方法 | 意義 |
|---|---|
| `GoToPage(index, animate)` | 跳到某頁，超出範圍時限制在範圍內。 |
| `NextPage()`、`PreviousPage()` | 跳到下一頁或上一頁。 |
| `ZoomIn()`、`ZoomOut()` | 以檢視區中心為錨點，縮放到下一級或上一級。 |
| `ResetZoom()` | 以檢視區中心為錨點，縮放到 100%。 |
| `ScrollIntoView(pageIndex, pageRegion, animate)` | 把頁面的某部分移入畫面；已可見時不動。 |
| `PageHitTest(pointInViewer, out pageIndex, out pointInPage)` | 尋找控制項上某點下的頁面；在畫布或工具列上傳回 false。 |

`IPageSource` 是按需頁面的來源，`ImagePageSource` 是 `ItemsSource` 背後的實作：

| 成員 | 意義 |
|---|---|
| `PageCount` | 頁數。 |
| `GetPageSize(pageIndex)` | 頁面大小，單位為頁面 DIP：縮放為 1 時按此大小顯示。 |
| `RenderAsync(request, cancellationToken)` | 繪製一頁或其中一部分；傳回 `PageImage`，暫時沒有內容時傳回 null。 |
| `PagesChanged` | 頁數或頁面大小發生變更；在 UI 執行緒上引發。 |

`PageRenderRequest` 有 `PageIndex`、`TargetSize`（整頁的像素大小）和 `Region`（null 表示整頁，否則是目標大小中的一個矩形）。`PageImage` 有 `Bitmap` 和 `OwnsBitmap`。

`PageLayer` 在頁面之上繪製：

| 成員 | 意義 |
|---|---|
| `Render(context, page)` | 在一個可見頁面上繪製。`page` 有 `PageIndex`、`PageBounds` 和 `Scale`。 |
| `OnPointerPressed(e)`、`OnPointerMoved(e)`、`OnPointerReleased(e)` | 頁面上的指標輸入。`e` 有 `PageIndex`、`PointInPage`、`Modifiers`、`ClickCount`、`PointerType` 和 `Handled`。除非圖層處理了按下，手指會捲動檢視，所以接管拖曳前先檢查 `PointerType`。 |
| `OnPointerDragCancelled()` | 圖層持有的拖曳沒有經過放開就結束了：擷取遺失，或檢視器離開了視覺樹。請復原拖曳已開始的動作。 |
| `GetCursor(pageIndex, pointInPage)` | 某點的游標；null 表示交給下面的圖層。 |
| `Invalidate()` | 要求重新繪製圖層。 |

## 無障礙

- 對輔助技術而言，檢視區是一份文件。除非 `AutomationProperties.Name` 另有設定，否則它的名稱是「第 3 頁，共 12 頁」，值模式讀到相同的文字。目前頁面變更時會更新。
- 檢視區可取得鍵盤焦點，從鍵盤取得焦點時顯示焦點環。
- 每個工具列按鈕都有目前語言（英文、簡體中文、繁體中文）的名稱和提示。

### 鍵盤

檢視區有焦點時按鍵生效。工具列有焦點時縮放鍵同樣有效。

| 按鍵 | 操作 |
|---|---|
| 方向鍵 | 捲動 40 個單位 |
| `PageUp` / `PageDown` | 捲動一個畫面；在 `Single` 版面中翻頁 |
| `Home` / `End` | 跳到第一頁或最後一頁 |
| `Ctrl` 或 `Cmd` + `=` / `-` / `0` | 放大、縮小或回到 100% |
| 按住 `Space` 並拖曳 | 平移 |
| 在頁碼欄位中按 `Enter` | 跳到該頁，超出範圍時限制在範圍內 |

## 動態效果

- 工具列和縮放鍵以檢視區中心為錨點，用俐落的彈簧改變縮放。`Zoom` 立即保存目標值。
- `GoToPage` 和 `ScrollIntoView` 用平滑的彈簧移動檢視。移動期間 `CurrentPageIndex` 保持目標值，讀者的任何捲動都會停止彈簧。
- 滾輪和捏合跟手，沒有動畫。更清晰的點陣圖直接替換舊點陣圖，文字不會閃動。

| 變化 | 完整 | 減少 | 關閉 |
|---|---|---|---|
| 工具列或按鍵縮放 | 彈簧 | 立即 | 立即 |
| `GoToPage`、`ScrollIntoView` | 彈簧 | 立即 | 立即 |
| 滾輪或捏合縮放 | 跟手 | 跟手 | 跟手 |
| 工具列按鈕背景 | 緩動 | 立即 | 無 |

## 響應式

- 檢視器沒有自身尺寸：給它一個高度，它會填滿得到的寬度。
- 寬度小於 640 時，頁面周圍的畫布內距從 24 縮小到 8，`FitMode` 會把多出的空間給頁面。
- 寬度小於 520 時，工具列只保留頁碼欄位、總頁數和縮放控制項。
- 觸控螢幕上手指捲動，捏合以手指為錨點縮放。沒有任何功能依賴懸停。

## 相關元件

- `PdfViewer` 用於 PDF 檔案，帶側邊欄、搜尋、選取和連結。
- `Carousel` 用於一排圖片或投影片，一次顯示一張。
- `ImageCompare` 用於在分隔線下比較同一張圖片的兩個版本。
- `ArcImage` 用於版面中的一張圖片。

| 元件 | 何時選擇 |
|---|---|
| `PdfViewer` | 文件是 PDF 檔案 |
| `Carousel` | 讀者透過拖曳翻閱投影片 |
| `ImageCompare` | 比較同一張圖片的兩個版本 |
| `ArcImage` | 版面中只有一張圖片 |
| `Markdown` | 文字應隨寬度重排 |
