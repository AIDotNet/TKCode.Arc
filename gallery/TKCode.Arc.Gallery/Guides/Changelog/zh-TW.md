---
title: 更新日誌
source-hash: 2a8a33522b07
---

# 更新日誌

TKCode.Arc 的所有重要變更，最新的在前。版本遵循[語意化版本](https://semver.org/)。版本帶有 `-preview` 後綴期間，API 在各發行版本之間仍可能變動；升級前請閱讀說明，並在專案中固定版本號。同一份清單也保存在儲存庫的 `CHANGELOG.md` 中。

每次發行附帶的 NuGet 套件：`TKCode.Arc`、`TKCode.Arc.Core`、`TKCode.Arc.Foundation`（0.4 起）、`TKCode.Arc.Fonts` 、`TKCode.Arc.Icons.Lucide`、`TKCode.Arc.Blocks`、`TKCode.Arc.AdvancedCharts` 、`TKCode.Arc.Special`、`TKCode.Arc.Workflow`、`TKCode.Arc.Themes.Pixel`、`TKCode.Arc.Themes.Corners`、`TKCode.Arc.Mobile`、`TKCode.Arc.Terminal`、`TKCode.Arc.Audio`、`TKCode.Arc.Audio.Native` 與 `TKCode.Arc.Audio.Browser`。

## 0.4.0-preview.1（未發佈）

Arc 不再依賴 AtomUI。每個套件現在只依賴 Avalonia 與新的 MIT 套件 `TKCode.Arc.Foundation`，整條相依鏈都採用寬鬆授權，閉源應用程式可以把 Arc 以單一檔案或 NativeAOT 方式發佈（issue #25）。應用程式的進入點與主題 API 有變化；控制項、控制項屬性與 XAML（`{arc:ArcDesignTokenResource ...}`、`{arc:XxxTokenResource ...}`）不變。請參閱下文的*從 0.3 遷移到 0.4*。

### 新增

- **Foundation**（新套件 `TKCode.Arc.Foundation`）：主題管理員（`ArcThemeManager`、`ArcTheme`、`ArcThemeState`、`ArcThemeBuilder`）、主題預設集（`ArcThemePreset`）、局部主題（`ArcThemeScope`）、套件註冊（`AppBuilder.UseArc(...)`、`ArcBuilder`）、語言（`ArcLanguage`、`ArcLanguageManager`），以及產生權杖、主題檔與語言目錄的原始碼產生器。不使用反射，相容於裁剪與 NativeAOT。控制項的主題在該型別的控制項第一次使用時才載入。
- **ArcImage** 與 `ImageSource`（Core）：在背景載入圖片（http、`avares://`、檔案、`data:`、點陣圖），帶共用快取，桌面與瀏覽器皆可使用。
- **ArcOverlayHost**（Core）：自帶覆疊層的面板，在其中開啟的對話方塊、抽屜與底部面板都限制在它的範圍內。
- **基礎主題**（Core）：Arc 為其範本用到的 Avalonia 控制項提供自己的主題（`ScrollViewer`、`ScrollBar`、`ItemsControl`、`PathIcon`、`TextSelectionHandle` 與快顯視窗宿主）。應用程式使用 `FluentTheme` 或 `SimpleTheme` 時，它會讓位。
- **捲軸**與 `ScrollArea` 一致：細長，指標停留在檢視上或捲動時顯示，靜止時隱藏。`ScrollViewer.AllowAutoHide="False"` 可讓它們一直顯示。
- **圓角主題**：`UseArcCorners()` 與 `t.Corners(style)`；現成的 `ArcCornerPresets.Square` 與 `ArcCornerPresets.Half`。
- **選單**：較長的選單在 `MaxDropDownHeight` 內捲動。
- **Gallery**：桌面視窗自行繪製標題列（標誌、標題、釘選按鈕；Windows 與 Linux 上還有視窗按鈕）。

### 變更

- **不相容：進入點。** `AppBuilder.UseAtomUI(b => b.UseDesktopControls().UseArc())` 改為 `AppBuilder.UseArc(arc => arc.UseArcControls())`。`Application` 類別不再需要 `partial`。
- **不相容：主題 API。** `ThemeConfigBuilder`、`ThemeAlgorithm`、`IThemeManager.ApplyThemeAsync` 與 `WithArc(...)` 由 `ArcTheme.Apply(t => ...)`、`ArcThemeManager.Apply` / `ApplyAsync` 以及 `UseArc` 中的 `WithTheme(...)` 取代。
- **不相容：Pixel 與 Corners** 改為主題預設集：`t.Pixel()` 與 `t.Corners(style)`。`ArcPixelTheme`、`ArcPixelScope`、`WithArcCorners` 與 `ArcCornerScope` 已移除；預設集會略過應用程式沒有註冊的套件。
- **不相容：語言**移到 `TKCode.Arc.Localization`（`ArcLanguage`、`ArcLanguageManager`）；`UseLanguages` 在 `ArcBuilder` 上。
- **不相容：圖片與快顯視窗。** `AsyncImage` 由 `ArcImage` 取代（`IsLoaded` 改為 `IsImageLoaded`）。`ArcPopup` 衍生自 Avalonia 的 `Popup`；`OpenMotion` 與 `CloseMotion` 的型別是 `ArcPopupMotion`，`AdjustPlacementAnchor` 移除了 `ref flipX, ref flipY` 參數，`IsOpen` 會維持為 true 直到退場動態效果結束。`ArcPopupFrame` 取代 `BaseMotionActor`，`ArcOverlayHost` 取代 `ScopeAwareOverlayLayerPanel`。
- **不相容：行動端權杖識別**改為 `MobileButton`、`MobileCheckbox`、`MobileDialog`、`MobileSwitch` 與 `MobileToast`，不再與桌面控制項同名。
- Arc 不再與 AtomUI 控制項共用主題；混用兩者的應用程式請留在 0.3.x。

### 修正

- **ToastStack**：在堆疊恢復計時器的過程中關閉某個 Toast，不再中斷恢復。
- **DataGrid**：篩選清單的搜尋框重新顯示（之前它沒有範本）。
- **PasswordField**：較長的強度標籤不再與強度條重疊（#23）。
- **Gallery**：環境變數事件記錄中的文字方塊重新顯示（之前它沒有範本）。

### 從 0.3 遷移到 0.4

1. 刪除所有 `AtomUI.*` 套件參照與 `using AtomUI...;`；Arc 會引入它需要的一切。
2. 把 `UseAtomUI(...)` 換成 `UseArc(...)`，並把 `UseArc()`（控制項套件）改名為 `UseArcControls()`。其他套件的註冊方法名稱不變，移到 `UseArc` 的回呼中。
3. 把主題請求換成 `ArcTheme.Apply`。一次變更只寫要改的部分，狀態的其餘部分維持不變。
4. `App` 類別可以拿掉 `partial` 關鍵字；不再有程式碼產生到它裡面。
5. 使用 .NET 11 SDK 且 `PublishAot` 時，在應用程式專案中設定 `<UseOptimizedPublish>false</UseOptimizedPublish>`。

```csharp
// 0.3
AppBuilder.Configure<App>()
          .UsePlatformDetect()
          .UseAtomUI(builder =>
          {
              builder.UseDesktopControls();
              builder.UseArc();
              builder.UseArcBlocks();
              builder.UseLanguages(LanguageTags.EnUS, [LanguageTags.EnUS, LanguageTags.ZhCN]);
          });

// 0.4
AppBuilder.Configure<App>()
          .UsePlatformDetect()
          .UseArc(arc => arc
              .UseArcControls()
              .UseArcBlocks()
              .UseLanguages(LanguageTags.EnUS, [LanguageTags.EnUS, LanguageTags.ZhCN])
              .WithTheme(t => t.Appearance(ArcAppearance.System)));
```

```csharp
// 0.3
var builder = new ThemeConfigBuilder()
    .WithAlgorithms(ThemeAlgorithm.Default, ThemeAlgorithm.Dark)
    .WithArc(arc => arc.Accent(ArcAccentPreset.Blue).MotionMode(ArcMotionLevel.Reduced));
await manager.ApplyThemeAsync(new ThemeRequest(IThemeManager.DEFAULT_THEME_ID, builder.Build(),
                                               ThemeTransitionReason.UserRequest));

// 0.4
ArcTheme.Apply(t => t
    .Appearance(ArcAppearance.Dark)
    .Accent(ArcAccentPreset.Blue)
    .MotionLevel(ArcMotionLevel.Reduced));
```

| 0.3（AtomUI） | 0.4 |
|---|---|
| `AppBuilder.UseAtomUI(b => ...)`、`UseDesktopControls()` | `AppBuilder.UseArc(arc => ...)` |
| `UseArc()`（控制項套件） | `UseArcControls()` |
| `IAtomUIBuilder` 上的 `UseArcCore`、`UseArcBlocks`、`UseArcAdvancedCharts`、`UseArcSpecial`、`UseArcWorkflow`、`UseArcMobile`、`UseArcPixelTheme`、`UseArcGallery` | `ArcBuilder` 上的同名方法；新增 `UseArcCorners()` |
| `builder.UseLanguages(...)` | `arc.UseLanguages(...)`，參數相同 |
| `partial class App`、`ApplicationStartup.Attach` | 不再需要 |
| `ThemeConfigBuilder`、`WithAlgorithms(ThemeAlgorithm.Dark)` | `t.Appearance(ArcAppearance.Dark)`（或 `Light`、`System`） |
| `IThemeManager.ApplyThemeAsync(new ThemeRequest(...))` | `ArcTheme.Apply(t => ...)`、`ArcThemeManager.Current.Apply` / `ApplyAsync`（請 await，切勿在 UI 執行緒上封鎖等待） |
| `WithArc(a => a.Accent(preset or color))` | `t.Accent(preset or color)` |
| `WithArc(a => a.Token(name, value))` | `t.Token(name, value)` |
| `WithArc(a => a.ControlToken(catalog, control, name, value))` | `t.Token(identity, name, value)` |
| `WithArc(a => a.MotionMode(level))` | `t.MotionLevel(level)` |
| `WithArcCorners(style, scope)`、`arc.Corners(style, scope)`、`ArcCornerScope` | `t.Corners(style)`、`ArcCornerPresets` |
| `ArcPixelTheme.CreateRequest(...)`、`WithArcPixel(...)`、`ArcPixelScope` | `t.Pixel()`、`ArcPixelPresets`；用 `t.RemovePreset(ArcPixelPresets.Slot)` 回到 Arc |
| `using AtomUI.Localization;` | `using TKCode.Arc.Localization;` |
| `Application.Current.GetLanguageManager()`、`ILanguageManager` | `ArcLanguageManager.Current` |
| `GetLocalizer().Get(kind)`、`Format(kind, args)` | `XxxLangResources.Get(kind)`、`XxxLangResources.Format(kind, args)` |
| `LanguageChanged`、`ChangeLanguage(tag)` | `Changed`、`Set(tag)`（或 `ArcLanguage.Changed`、`ArcLanguage.Set`） |
| `FormattingCulture`、`CurrentLanguage` | `ArcLanguage.Culture`、`ArcLanguage.Current` |
| 依目錄覆寫行動端權杖 | 識別 `MobileButton`、`MobileCheckbox`、`MobileDialog`、`MobileSwitch`、`MobileToast` |
| `AtomUI.Controls.ImageSource`、`AsyncImage`、`IsLoaded` | `TKCode.Arc.ImageSource`、`ArcImage`、`IsImageLoaded` |
| 以 AtomUI `Popup` 為基礎的 `ArcPopup`、`BaseMotionActor` | 以 Avalonia `Popup` 為基礎的 `ArcPopup`、`ArcPopupFrame`；`OpenMotion` / `CloseMotion` 的型別是 `ArcPopupMotion` |
| `AdjustPlacementAnchor(placement, ref flipX, ref flipY)` | `AdjustPlacementAnchor(placement)` |
| `ScopeAwareOverlayLayerPanel` | `ArcOverlayHost` |
| AtomUI 為 `ScrollViewer`、`ScrollBar`、`ItemsControl` 與快顯視窗宿主提供的主題 | Arc 的基礎主題；捲軸靜止時隱藏（`ScrollViewer.AllowAutoHide="False"` 讓它們一直顯示） |

- **視窗與覆疊層。** 少了 AtomUI 的視窗主題，一般的 Avalonia `Window` 只有在範本中含有 `EnableOverlayLayer="True"` 的 `VisualLayerManager` 時才有覆疊層。`ThemeSwitcher` 的轉場效果需要它；沒有時會直接切換。
- **AtomUI 控制項。** 同時使用 AtomUI 控制項的應用程式，無法再讓兩者共用同一個主題。在這些畫面遷移到 Arc 之前，請留在 0.3.x。

## 0.3.1-preview.6 (2026-10-28)

### 新增

- **Cascader**(Extras):依層級逐級選擇一個節點,如省、市、區。欄位、選單、滑動醒目提示和清除按鈕與 `Select` 一致;父節點的子級在右側一欄中展開,選單寬度用彈簧過渡到新的寬度。資料透過委派讀取(`ChildrenSelector`、`TextSelector`、`ValueSelector`),不使用反射。`SelectedItem`、`SelectedValue` 與 `SelectedPath` 始終保持一致,資料抵達之前寫入的值會被保留,資料抵達後再解析。選項:`LeafOnly`、`ExpandTrigger`、`ShowFullPath`、`PathSeparator`、`IsClearable`、`IsReadOnly`;完整的鍵盤支援。

## 0.3.1-preview.5 (2026-10-28)

### 新增

- **Audio**(新套件 `TKCode.Arc.Audio`、`TKCode.Arc.Audio.Native` 與 `TKCode.Arc.Audio.Browser`):媒體類 Block 使用的 `IAudioPlayer` 抽象,預設靜音模擬;桌面經 miniaudio 播放(`ArcAudioNative.Use()`),WebAssembly 經 HTML audio 元素播放(`ArcAudioBrowser.UseAsync()`)。
- **Blocks**:`MediaPlayer`(封面、播放清單、進度列、上一首與下一首、音量與靜音)和 `FileUpload`(拖放區、檔案清單、進度、驗證)。
- **Special**:新增 Cursor 分組,包含 `DotGrid`、`GravityWell` 和 `InkFluid` 三個指標效果。
- **Gallery**:`/demo` 下的 Northstar 儀表板展示,複刻 uiarc.dev/demo 的十一個頁面和登入頁;從首頁的「查看演示」或頂欄選單的「演示」進入。

### 修正

- **ArcContentSwap**:控制項被切換出去後再次顯示不再擲出例外(在 Webhooks 檢查面板裡切換 Payload 與 Headers 會導致程式當掉)。
- **Charts**:平均線旁邊的座標刻度保持隱藏,平均值標籤不再與它重疊。
- **Webhooks**:Payload 與 Headers 切換後都從第一行開始顯示。

## 0.3.1-preview.4 (2026-10-27)

### 新增

- **Terminal**(新套件 `TKCode.Arc.Terminal`,net10.0 與 net11.0):`TerminalView` 控制項和 libghostty-vt 的託管封裝(VT 解析、畫面狀態、選取、輸入編碼)。原生函式庫隨套件放在 `runtimes/<rid>/native`,涵蓋 Windows、macOS 與 Linux。

## 0.3.1-preview.3 (2026-10-27)

### 新增

- **Blocks**：Analytics、Developer、People、Finance 與 LineCharts 新增約 25 個區塊（API 金鑰、Webhook、整合、環境變數、團隊與角色、指標探索、MRR 瀑布圖、同期群留存、分級統計地圖、行情終端等），附 Gallery 頁面、範例與測試。
- 新增 **RichTextEditor** 與 **ActionSwap** 控制項。
- **行動端**：`Dialog`、`Toast`、`ActionSheet`、`Toolbar`、`PullToRefresh`、`Radio` 與 `IconButton`，鍵盤避讓與 Arc 動態。
- **Gallery**：桌面端支援檢查更新並設定 macOS Dock 圖示；瀏覽器端透過 HTTP 載入地圖資料。

### 變更

- 重做 **Table**、**Editors**、**Selects** 與 **Toggles**。

### 修復

- **TimePicker**、**FilterMenu**：列的空白區域不再回應指標輸入 (#20)；**Combobox**：清除後不再開啟清單。
- **Password**：顯隱選擇與輸入狀態保持一致 (#21)。
- **Gallery**：修復 Windows 上側邊欄搜尋框重複提交輸入法內容的問題。

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
