---
title: 更新日誌
source-hash: ebb66486ef60
---

# 更新日誌

TKCode.Arc 的所有重要變更，最新的在前。版本遵循[語意化版本](https://semver.org/)。版本帶有 `-preview` 後綴期間，API 在各發行版本之間仍可能變動；升級前請閱讀說明，並在專案中固定版本號。同一份清單也保存在儲存庫的 `CHANGELOG.md` 中。

每次發行附帶的 NuGet 套件：`TKCode.Arc`、`TKCode.Arc.Core`、`TKCode.Arc.Fonts` 、`TKCode.Arc.Icons.Lucide`、`TKCode.Arc.Blocks`、`TKCode.Arc.AdvancedCharts` 、`TKCode.Arc.Special`、`TKCode.Arc.Workflow`、`TKCode.Arc.Themes.Pixel`、`TKCode.Arc.Themes.Corners` 與 `TKCode.Arc.Mobile`。

## 0.3.1-preview.2 (2026-10-26)

### 新增

- **圓角主題**：新套件 `TKCode.Arc.Themes.Corners`（`WithArcCorners(style, scope)`），僅透過令牌覆寫為所有 Arc 控制項切換直角或縮放圓角；顏色、邊框、陰影與動態維持不變。
- **行動端**：`ListGroup` / `ListItem`、`Sheet`、`TextField` 與 `Searchbar`，大標題 `Navbar` 以及更多 `Button` 變體；Gallery 新增列表分組、導覽堆疊與底部面板的頁面與範例。
- **Core**：新增更多設計令牌與 `ArcTokens` 輔助方法，用於繫結圓角半徑。

### 變更

- **像素主題**：圓角與主題資源改由共用的圓角表重新產生。
- **Blocks**：`WeekCalendar` 使用新的邊線令牌繪製邊緣。

### 修復

- **Stepper**：目前步驟的光暈不再被裁切 (#19)。

## 0.3.1-preview.1 (2026-10-26)

### 新增

- **行動端**：新套件 `TKCode.Arc.Mobile`（`UseArcMobile()`），首批觸控優先控制項：`Button`（按壓狀態、44 × 44 觸控目標）、`Switch`、`Page`（頁首、內容與頁尾位於安全區內）、`Navbar` 與 `Tabbar`。只依賴 `TKCode.Arc.Core`，有自己的命名空間。尚未在 Android 或 iOS 真機上驗證，並且仍會經由 `TKCode.Arc.Core` 間接引入 `Avalonia.Desktop` 與 `AtomUI.Desktop.Controls`。
- **Gallery › Mobile**：新增 Mobile 區，以手機框預覽（iPhone 15、Pixel、小螢幕手機、平板，直式與橫式，狀態列、模擬安全區、軟鍵盤佔位）。這是桌面上的模擬，不是真機。
- **像素風主題**：新套件 `TKCode.Arc.Themes.Pixel`（`UseArcPixelTheme()`）：直角、硬邊陰影、受限調色盤，明暗兩套；像素字型 Silkscreen 與 VT323（SIL OFL）隨 `TKCode.Arc.Fonts` 提供（`WithArcPixelFonts()`）。Gallery 的外觀面板新增「風格」切換：Arc 或 Pixel。
- **表格 › TreeTable**：具巢狀列、三態選取、保留階層的排序、延遲載入子節點與 treegrid 鍵盤導覽的表格。Gallery 以「New」標記新元件，持續 60 天。
- **Core**：邊框寬度與圓角的設計 Token（`EdgeThickness`、`FocusEdgeThickness`、`RadiusStep*`、`EdgeThickness*`），主題可以改寫它們；所有預設值與原先的取值一致。

### 變更

- **Gallery**（網頁）：WebAssembly 主機重新改為不做提前編譯，因為編譯版在啟動時當機。圖示網址加上版本號，瀏覽器會丟棄舊圖示。

### 修正

- **Carousel**：計時已耗盡的投影片不再不讓出執行緒就再次推進輪播，而是重新開始一個間隔。

## 0.3.0-preview.5 (2026-10-24)

### 新增

- **工作流程畫布**：新套件 `TKCode.Arc.Workflow`（`UseArcWorkflow()`）：`WorkflowCanvas` 顯示步驟與連線組成的圖並播放執行過程，支援平移縮放、編輯、連線、復原重做、複製貼上、節點範本、縮圖，500 個步驟的版面約 2 毫秒。另有 `WorkflowNodeCard` 與 `WorkflowZoomControls`。
- **Blocks › 驗證**：`SignIn`、`SignupForm`、`LoginSplit`、`LoginCentered`、`LoginImmersive`、`OtpInput`、`OnboardingIllustration` 和 `MultiStepForm`。
- **Core**：`PanZoomTransform`、`WheelRouter` 和 `LayeredGraphLayout`。

### 變更

- **Gallery**（網頁）：網站改為 WebAssembly 預先編譯，慢速 CPU 上動畫更流暢；首次造訪下載量較大。

### 修復

- **記憶體**：許多控制項（`TreeView`、`ChatThread`、`Markdown`、圖表、`DataGrid`、AI、Projects 和 Special 區塊等）在整個生命週期內都監聽其顯示的資料，使頁面一直留在記憶體中；現在只在位於視覺樹時監聽。`SiteFooter`、`SemanticZoom`、`AgentStatus` 和 Gallery 頁面也不再洩漏。
- **選單**：關閉的面板不再把遮罩留在視窗上。
- **ChatThread**：表情選擇器不再向主控台寫入除錯訊息。
- **Accordion** 與 **ExpandableCard**：展開時分隔線開銷更低，外觀不變。
- **Markdown** 與 AI 回答改用 StreamMark `0.1.0`。

## 0.3.0-preview.4 (2026-10-24)

### 新增

- **特別元件 › AI**：`TKCode.Arc.Special` 中的五個智慧代理介面元件：`AgentStatus`（即時狀態膠囊，可展開查看最近的步驟）、`ToolCall` 與 `ToolCallGroup`（顯示輸入與輸出的工具呼叫列，可核准與重試）、`AgentPlan`（可編輯的計畫草稿，核准後成為即時清單）、`AgentTimeline`（在同一時間軸上重播多代理執行）與 `ContextWindow`（具壓縮門檻的 200 格權杖預算網格）。
- **Blocks › 頁首與頁尾**：`SiteHeader`、`SiteFooter`、`CtaSection` 與 `NewsletterSignup`，移植自 Arc 的免費區塊。
- **Blocks › AI**：模型競技場（`ModelArena`，兩個模型的盲測：串流回答、投票與排行）與文件擷取（`DocumentExtraction`，文件旁的表單隨模型閱讀逐項填寫，每個值都連結到其來源文字）。九個 AI 區塊現已全部可用。
- **AnnouncementBar**：`DismissalDuration` 讓記住的關閉狀態在一段時間後失效。

### 變更

- **Gallery**：語言在頂欄有了獨立的選單，首次啟動跟隨系統語言並會被記住。首頁新增友站連結，文件頁載入時顯示佔位內容。

### 修復

- **Combobox**（#9）：聚焦時不再顯示深色的環，只把邊框變為 `BorderStrong`，與 Arc 一致。
- **Gallery**（#8、#10）：範例不再切掉行內編輯框與焦點環的左側。
- **Gallery**（#11）：以 NativeAOT 發佈的桌面 Gallery 能重新載入文件頁與截圖。

## 0.3.0-preview.3 (2026-10-23)

### 修復

- **Select**：選項高亮是直角；現在使用主題的選項圓角（14）。
- **Select**：Full 動態等級下，選取後選單會在欄位下方的文字上淡出。現在依文件約定立即關閉；為此 `ArcPopup` 新增了 `IsExitInstant`。Reduced 等級仍然淡出。
- **Combobox、MorphSelect**：在 macOS 上點擊輸入框可能因 `NSInvalidArgumentException` 導致應用程式結束。無障礙即時區域不再接收空文字。

## 0.3.0-preview.2 (2026-10-23)

### 新增

- **特別元件**：新套件 `TKCode.Arc.Special`（`UseArcSpecial()`）中的五個 AI 動畫講解：`AgentLoop`、`MixtureOfExperts`、`SpeculativeDecoding`、`PromptCache` 與 `ReasoningTree`，以共用的 `Explainer` 基底類別建置。所有數字都由資料計算而得。
- **Blocks**：五個專案區塊（`WeekCalendar`、`AvailabilityPicker`、`ProjectBoard`、`ReleaseReadiness`、`KanbanBoard`），六個展示區塊（`ActivityTerrain`、`RevenueGlobe`、`CustomerGalaxy`、`SemanticZoom`、`LayoutMorph`、`ExplodedView`），以及首批 AI 區塊（模型選擇器、智慧代理記憶、AI 輸入框、AI 聊天、AI 回答，以及側邊面板與提示詞工作台）。
- **Core**：`ArcSurface.Elevation`（對話方塊、抽屜、氣泡卡片與選單的內容知道自己位於抬升的表面上）、`RollingDigitsFalloff.None`、新的彈簧、`ArcContentSwap` 模式、`ArcRing`、`WordDiff`、`WordPacer`、`DecoratedTextBlock`，以及另外 23 個 Lucide 圖示。
- **Button**：抬升表面上的幽靈按鈕現在有清楚可見的懸停與按下填色。
- **特殊輸入**：`TokenTextEditor`，權杖視為單一單位的多行編輯器；`MentionInput` 以它建置。
- **Gallery**：文件網站提供 `llms.txt`、`llms-full.txt`、每個頁面的 Markdown 版本，以及供 AI 助理使用的代理技能。

### 變更

- **破壞性變更，儲存庫搬移**：儲存庫現在位於 `AIDotNet/TKCode.Arc`，XAML 命名空間隨之改變。請將 `xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc"` 改為 `xmlns:arc="https://github.com/AIDotNet/TKCode.Arc"`（Lucide 命名空間同樣處理）。
- **Sidebar**：`ResizeMode` 現在預設為 `Overlay`；設為 `ResizeMode="Push"` 可恢復先前的行為。
- **效能**：動態排程器每個畫面只推進每個驅動一次，隱藏的迴圈不再產生負擔，快取模糊效果與文字排版，ActivityTerrain 與 LayoutMorph 大幅加速。
- **Gallery**：WebAssembly 網站在第一個畫面之前少下載約 1.5 MB。

### 修正

- **Tabs**、**PageHeader**、**Breadcrumb**、**ArcContentSwap**、**DataGrid**：範本不再以 `null` 資料內容建置。
- **Core**：中途離開視窗的內容切換不再恢復為半透明；錨定彈出層的陰影重新使用圓角。
- **Sidebar**：從側欄軌道展開後，內容不再保留第一個畫面的寬度。
- **Motion**：從 `Transparent` 淡入或淡出填色時不再閃出灰色（`ArcBrushTransition` 使用預乘 alpha 混合）。
- **TagInput**、**Textarea**、**TimePicker**：移除粗重的焦點環；移除 `FieldFrameToken` 的 `FocusRing*` 值。

完整清單請見儲存庫中的 `CHANGELOG.md`。

## 0.3.0-preview.1 (2026-10-22)

### 新增

- **Core**：`ArcZoom.Scale`，一個可繼承的附加屬性，供縮放視窗內容的應用程式使用。錨定彈層（選單、選取器、工具提示、懸停卡片、氣泡卡片）繪製在視窗的覆蓋層裡，位於被縮放的容器之外，因此原先無法跟隨縮放；現在它們會依繼承到的比例排版。`ArcPopup.Surface` 傳回指派給 `Child` 的那個介面元素（彈層現在把它放進一個縮放元素中，因此 `Child` 本身是那個元素）。
- **ExpandableContent**：把 ExpandableText 的截斷、切換按鈕和動態效果用在任意內容上，例如可選取的文字、唯讀編輯器或轉譯後的 Markdown。截斷停在內容中第一段可見文字的某一行上（`TextBlock`、`SelectableTextBlock`，或實作了新增 `IExpandableLineSource` 的控制項）；其他內容依 `Lines × LineHeight` 截斷。`ExpandableText` 與 `ExpandableContent` 共用新增的 `ExpandableClamp` 基底類別。
- **ExpandableText**、**ExpandableContent**：`IsFadeEnabled` 可以關閉最後一行上的漸隱。

### 變更

- AtomUI 固定為 6.2.8；0.2.0-preview.1 以 6.2.7 建置。
- **ExpandableText**：摺疊時，文字只排版到截斷線之後的一行，展開時才完整排版。600 行的文字摺疊時測得約 1ms，而不是約 135ms。帶有內嵌控制項的文字仍會完整排版，以便截斷線以下的連結可以取得焦點。截斷現在停在文字自身的行邊界上，而不是 `Lines × LineHeight`。
- **ExpandableText**：`Lines`、`IsExpanded`、`MoreLabel`、`LessLabel`、`LineHeight`、`IsOverflowing` 和 `ExpandedChanged` 現在宣告在新增的 `ExpandableClamp` 基底類別上。透過 `ExpandableText` 使用它們的程式碼不受影響。

### 修正

- **ExpandableText**：隨新的資料內容到來的 `IsExpanded` 變化（例如在回收的項目範本中）不再產生動畫，也不再捲動頁面。
- **Switch**、**Checkbox**：隨新的資料內容到來的值（例如詳情檢視被重複使用於下一項時）會立即顯示；滑塊不再滑動和拉伸，勾號也不再像使用者點選過那樣畫出。
- **Progress**：`Value` 變化但整數百分比不變時，不再重啟彈簧，也不再更新讀數和無障礙狀態。每秒回報數百次的下載現在每個百分點只更新一次。
- **SegmentedControl** 以及其他帶滑動指示條的切換控制項：隱藏的控制項不再在視窗的每次版面配置時放置指示條；控制項再次顯示時才會放置。
- **GlidingIndicator**：描邊畫筆在影格之間保留，不再每次轉譯都新建。
- TreeView 和 JsonViewer：在下次版面配置之前對列再次做出更改，而第一次更改的列還在關閉過程中，會在 `RowTop` 中擲出 `IndexOutOfRangeException`。面板現在在列變化後立即重新整理列的幾何資訊。

## 0.2.0-preview.1 (2026-10-21)

### 新增

- **目標框架**：函式庫同時面向 `net8.0`，與 `net10.0`、`net11.0` 並列。net8.0 建置需要 StreamMark `0.1.0-alpha.25`，該版本為其套件加入了 net8.0。
- **Markdown**（`TKCode.Arc`）：大綱與搜尋。提供 `GetOutline`、`ScrollToPoint`、`ScrollToAnchor`、`Find` 和 `GetSelectedText`，並有 `IsDocumentReady`、`MatchCount`、`CurrentMatchIndex`、`HasSelection` 屬性，以及 `DocumentReady` 和 `SearchChanged` 事件。宿主可以圍繞文件搭建大綱側欄、搜尋列和複製操作。
- **RichTextEditor**（`TKCode.Arc`）：`TrySetMarkdown` 匯入 Markdown，並回傳 `RichTextImportReport`，列出編輯器無法保留的內容（`RichTextImportIssueKind`：標題層級、程式碼圍欄的資訊字串、波浪線圍欄、表格等）。
- **DataGrid**（`TKCode.Arc.AdvancedCharts`，儲存庫內，尚未發布到 NuGet）：儲存格渲染器（徽章、進度、連結、圖示文字、核取方塊、操作）、驗證規則（必填、範圍、最大長度、模式）、條件格式規則、`CellInvoked` 事件、右鍵選單事件、CSV 匯出選項，以及篩選與版面狀態。
- **Blocks**（`TKCode.Arc.Blocks`，儲存庫內，尚未發布到 NuGet）：命令面板對話框；帶數值與日期篩選、詳情和操作的搜尋結果；側欄的摺疊與調整大小模式；重做的通知中心。

### 變更

- AtomUI 鎖定為 6.2.7；0.1.1-preview.1 基於 6.2.6 建置。AtomUI 相依套件隨 Arc 版本一同變更。
- DataGrid 的 CSV 匯出預設會處理試算表公式（`NeutralizeCsvFormulas`），超過 `MaxPasteCells`（預設 1,000,000 個儲存格）的貼上會被拒絕。
- 在 .NET 8 和 9 上，DataGrid 的自然文字排序使用內建比較器；在 .NET 10 及更高版本上仍使用 `CompareOptions.NumericOrdering`。

### 修正

- `LiveLoop.Start` 在已經在輸出影格時提前返回，因此每次指標移動都啟動迴圈的呼叫端不再每次重新讀取門控條件。

## 0.1.1-preview.1 (2026-10-21)

### 新增

- **元件**：Activity、Content、Editors、Illustrations、Media 與 SpecialInputs 分類，以及 ButtonGroup、SplitButton、Card、Reactions、SwipeActions，還有 Color、Date、DateRange 與 Time 挑選器。
- **Core**：`MotionTimeline` 與 `LiveLoop` 動態效果驅動程式、`SharedElement` 基礎元件、`ArcBlur` 與 `ArcViewport`。
- **Blocks**（`TKCode.Arc.Blocks`）：AppShell 區塊（Sidebar、PageHeader、CommandPalette、SearchResults）以及設定區塊（SettingsPage、SecuritySettings、BillingOverview、UsageBilling、CancelFlow、SettingsCommand）。
- **AdvancedCharts**（`TKCode.Arc.AdvancedCharts`）：一個新專案，首先提供 DataGrid 控制項。
- **圖庫**
  - 元件頁面即其附即時範例的文件頁面；每個範例都是一個 `.axaml` 檔案，其「程式碼」索引標籤會顯示該原始碼。
  - 側邊欄新增文件區，按 `Cmd+K` 或 `Ctrl+K` 可在視窗的任何位置聚焦搜尋。
  - 從 XML 文件產生的 API 表格，附 zh-CN 與 zh-TW 翻譯；外殼文字與文件頁面提供 en-US、zh-CN 與 zh-TW 版本。
  - 透過 StreamMark `0.1.0-alpha.23` 實現 Markdown 渲染與程式碼醒目提示，這是第一個能在瀏覽器中執行的版本。
  - WebAssembly 主機（`TKCode.Arc.Gallery.Browser`），以及桌面與瀏覽器主機的應用程式圖示。
- **發佈**
  - 圖庫的 WebAssembly 映像在 GitHub 上建置，以 `ghcr.io/atomuicommunity/tkcode-arc-gallery` 發佈，並以 brotli、預先壓縮的檔案與瀏覽器快取提供服務。
  - 圖庫桌面應用程式的發行建置：Windows 與 Linux（未簽章），macOS（已簽章並完成公證）。
  - 透過 nuget.org 信任發佈，從 CI 打包並發佈 NuGet 套件。
- **工具**：API 文件翻譯流程的指令碼，以及圖示建置器。
- 簡體中文版 README 與贊助者區段。

### 變更

- 各分類的共用輔助程式由 Core 共用基礎元件取代。
- 圖庫頁面從依分類撰寫的 C# 頁面，改為 Markdown 文件頁面與 `.axaml` 範例。
- 許多控制項的公開屬性與事件新增了 XML `<summary>` 文字；它們會填入圖庫的 API 表格。

### 修正

- 動態效果：等待暫停的迴圈可能讓渲染器閒置，且影格要求懸而未決；現在影格會持續產生。
- HoldToConfirm、ConfirmMorph（長按模式）、Tabs 面板與 Carousel 可視範圍在重複使用剪裁區域時，沒有讓被剪裁的視覺元素失效，因此長按填滿不會移動，成長中的面板或調整大小後的輪播會保留過期的剪裁區域。
- Countdown：靜止的滾輪會繪製為一般字形，使滴答作響的錶面影格時間減半。
- 當 XAML 將 `Minimum` 設為高於目前的 `Maximum` 時（例如 `Minimum="360" Maximum="900"`），Slider 與 ElasticSlider 會因 `Math.Clamp` 擲出例外。
- 切換選取的索引標籤時，Tabs 不再捲動它所在的頁面；索引標籤清單會自行顯示選取的索引標籤。
- 圖庫瀏覽器主機：為 WebAssembly 建置原生 Skia，發佈時略過裁剪。

### 從 0.1.0-preview.1 升級

- 請將所有 TKCode.Arc 套件同時更新到相同版本。
- 新增的分類與控制項除 `UseArc()` 外不需額外設定。區塊與 DataGrid 需要各自的註冊（`UseArcBlocks()`、`UseArcAdvancedCharts()`）以及專案參照，詳見**安裝**。

## 0.1.0-preview.1 (2026-10-20)

首個預覽版本。
