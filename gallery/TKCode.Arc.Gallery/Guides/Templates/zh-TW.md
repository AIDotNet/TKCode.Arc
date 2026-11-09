---
title: 範本
source-hash: e07cc444920a
---

# 範本

`TKCode.Arc.Templates` 提供 `dotnet new` 範本，用來建立以 TKCode.Arc 為基礎的應用程式，以及在應用程式中加入常用元件。新的應用程式建立後即可建置與執行：它參照與範本同時發行的 Arc 和 Avalonia 版本，Arc 佈景主題、Inter 與 Geist 字型、Lucide 圖示以及明暗佈景主題切換都已設定好。

```bash
dotnet new install TKCode.Arc.Templates
dotnet new tkcode-arc -n MyApp --layout sidebar
cd MyApp
dotnet run
```

Visual Studio 與 Rider 會在「新增專案」對話方塊中列出這些範本（**TKCode.Arc App**、**TKCode.Arc Cross-platform App**），選項顯示為核取方塊與清單。

## 範本一覽

| 範本 | 簡短名稱 | 產生內容 |
|---|---|---|
| TKCode.Arc App | `tkcode-arc` | 桌面應用程式（Windows、macOS、Linux） |
| TKCode.Arc Cross-platform App | `tkcode-arc-xplat` | 同樣的應用程式，拆成共用專案，加上桌面主機與瀏覽器（WebAssembly）主機 |
| TKCode.Arc Admin App | `tkcode-arc-admin` | 完整的 SaaS 後台應用程式：登入、儀表板、客戶、團隊、帳單、設定 |
| TKCode.Arc AI Chat App | `tkcode-arc-ai-chat` | 完整的 AI 聊天用戶端：對話清單、串流 Markdown 回覆、模型選擇、附件 |
| TKCode.Arc Developer Tool | `tkcode-arc-devtool` | 完整的 IDE 式工具：檔案總管、編輯器索引標籤、屬性、建置輸出、儲存的版面配置 |
| `tkcode-arc-workflow` | 步驟面板、可編輯的 `WorkflowCanvas`（拖曳連線、Delete 刪除、復原與重做、自動排列），附小地圖與縮放控制項；選取步驟的屬性檢查器；Run 依序執行各步驟。流程儲存在 `workflow.json` 中，匯入與匯出使用同樣的 JSON。 |
| `tkcode-arc-projects` | 側邊欄切換 `KanbanBoard`（拖曳卡片、快速新增、卡片詳細資料）、`GanttTimeline`、`WeekCalendar` 與 `ReleaseReadiness` 檢查清單。 |
| `tkcode-arc-analytics` | 頁面切換 `KpiDrilldown`、`RevenueExplorer`、`CohortRetention`、`UsageForecast` 與 `UptimeStatus`。 |
| TKCode.Arc Workflow Editor | `tkcode-arc-workflow` | 完整的流程編輯器：步驟面板、可編輯畫布、屬性檢查器、執行、JSON 匯入匯出 |
| TKCode.Arc Project Planner | `tkcode-arc-projects` | 完整的專案規劃應用程式：看板、甘特時間軸、週行事曆、發行檢查清單 |
| TKCode.Arc Analytics Dashboard | `tkcode-arc-analytics` | 完整的資料儀表板：KPI 下鑽、營收、留存分群、用量預測、服務狀態 |
| TKCode.Arc Component Library | `tkcode-arc-lib` | 以 Arc 為基礎的控制項程式庫：範例控制項及其 Token 與佈景主題、測試、範例應用程式，可直接封裝 |
| TKCode.Arc Page | `tkcode-arc-page` | `Pages/<Name>Page.axaml`，可附 ViewModel |
| TKCode.Arc Window | `tkcode-arc-window` | `Views/<Name>Window.axaml`：附按鈕頁尾的次要視窗 |
| TKCode.Arc Dialog | `tkcode-arc-dialog` | `Dialogs/<Name>Dialog.axaml`：放在 Arc `Dialog` 中的表單，附可等待的 `ShowAsync` |
| TKCode.Arc Control | `tkcode-arc-control` | `Controls/<Name>/`：範本化控制項、它的設計 Token 類別與佈景主題檔 |

## 版面配置

`--layout` 決定視窗結構。每種版面配置都把內容放在 `Views/MainView.axaml` 中：桌面視窗顯示它，瀏覽器則直接顯示它，不需要視窗。

| 版面配置 | 內容 |
|---|---|
| `blank`（預設） | 頂端列（應用程式名稱與佈景主題切換）加上一個附歡迎卡片的頁面。 |
| `sidebar` | 來自 `TKCode.Arc.Blocks` 的可收合 `Sidebar`：圖示、64 px 圖示列（Cmd+B / Ctrl+B），以及三個頁面：首頁、專案（附「新增專案」動作的清單）與設定（外觀與強調色）。 |
| `header` | 同樣的三個頁面，以頂端列中的 `SegmentedControl` 切換。 |
| `dock` | 來自 `TKCode.Arc.Dock` 的 IDE 式版面配置：工具窗格、文件索引標籤與輸出窗格，可以拖曳停靠、自動隱藏或浮動，並有「重設版面配置」動作。 |

## 選項

| 選項 | 值 | 預設 | 說明 |
|---|---|---|---|
| `--layout` | `blank`、`sidebar`、`header`、`dock` | `blank` | 視窗版面配置（見上文）。 |
| `--mvvm` | `none`、`toolkit` | `none` | `toolkit`：以 CommunityToolkit.Mvvm 撰寫的 ViewModel（原始碼產生的屬性與命令）與編譯繫結。 |
| `--di` | | 關 | 以 Microsoft.Extensions.DependencyInjection 建立 ViewModel 與服務（`Services/AppServices.cs`）。隱含 `--mvvm toolkit`。 |
| `--settings` | `true`、`false` | `true` | 儲存外觀、強調色、視窗大小與位置，以及版面配置的狀態（側邊欄的收合、停靠窗格、語言），並在下次啟動時還原。 |
| `--i18n` | | 關 | 把介面文字放在 `Localization/*.xlf`（English、简体中文、繁體中文）中，並加入語言切換。 |
| `--ui-language` | `en-US`、`zh-CN`、`zh-TW` | `en-US` | 介面文字的語言；與 `--i18n` 一起使用時，是應用程式啟動時的語言。 |
| `--tests` | | 關 | 加入一個在 Avalonia Headless 平台上啟動應用程式的測試專案，以及一個方案。 |
| `--custom-titlebar` | | 關 | 以 Arc 的標題列（`ArcWindow`）取代系統標題列，並可在其中放入自己的控制項。 |
| `-f`、`--framework` | `net10.0`、`net8.0`、`net11.0` | `net10.0` | 目標框架。`net11.0` 需要 .NET 11 SDK；跨平台範本只有 `net10.0`。 |
| `--accent` | `Neutral`、`Violet`、`Blue`、`Green`、`Amber`、`Orange`、`Coral`、`Rose` | `Neutral` | 應用程式啟動時的強調色。 |
| `--no-restore` | | | 建立專案後不自動還原。 |

選項可以自由組合，例如：

```bash
dotnet new tkcode-arc -n Studio --layout dock --mvvm toolkit --tests
dotnet new tkcode-arc -n Console --layout header --i18n --ui-language zh-CN --custom-titlebar
```

## 產生的檔案

```text
MyApp/
  MyApp.csproj        TKCode.Arc, TKCode.Arc.Fonts, TKCode.Arc.Icons.Lucide, Avalonia.Desktop, Avalonia.Fonts.Inter
                      (+ TKCode.Arc.Blocks, TKCode.Arc.Dock, CommunityToolkit.Mvvm ... as the options need them)
  Program.cs          the desktop entry point
  AppSetup.cs         UseArcApp(): the fonts, UseArc(...) with Arc's packages, the languages and the start-up theme
  App.cs              creates the main window (on the desktop) or the main view (in a browser); no App.axaml
  Views/              MainWindow (the desktop window) and MainView (the layout)
  Navigation/         sidebar and header layouts: AppPages (the page list) and PageHost (shows the current page)
  Pages/, Models/     sidebar and header layouts: the sample pages and their sample data
  ViewModels/         --mvvm toolkit
  Services/           --di
  Settings/           --settings: AppSettings (JSON, generated serializer, trimming and AOT safe)
  Localization/       --i18n: AppLangResourceKind, en-US.xlf, zh-CN.xlf, zh-TW.xlf, AppLanguages
```

使用 `--tests` 時，應用程式移到 `MyApp/MyApp/`，測試位於 `MyApp/MyApp.Tests/`，`MyApp/MyApp.slnx` 包含兩者。在方案資料夾執行 `dotnet test` 即可執行測試；測試把設定保存在記憶體中，不會變動你自己的設定。

只有應用程式用到的 Lucide 圖示會編譯進去。執行時才決定的圖示需要在專案檔中宣告，例如 `<LucideIcon Include="Bell;ArrowRight" />`。

## 跨平台應用程式

`tkcode-arc-xplat` 的版面配置與選項和 `tkcode-arc` 相同，產生一個方案：

```text
MyApp/
  MyApp.slnx
  MyApp/              the app: views, view models, AppSetup.cs (a library; everything you write goes here)
  MyApp.Desktop/      the desktop host
  MyApp.Browser/      the browser host (WebAssembly): index.html, main.js, and a Chinese font when the UI has Chinese text
```

```bash
dotnet workload install wasm-tools-net10    # once; with the .NET 10 SDK: wasm-tools
dotnet new tkcode-arc-xplat -n MyApp --layout sidebar
cd MyApp
dotnet run --project MyApp.Desktop
dotnet run --project MyApp.Browser          # then open the address it prints
```

在瀏覽器中，設定保存在頁面的本機儲存空間裡。瀏覽器不為應用程式提供系統字型，所以介面含有中文時，瀏覽器主機會附上 Noto Sans SC 子集（SIL Open Font License），用來顯示中文。

## 從完整的應用程式開始

情境範本產生的應用程式一建立就能使用：真實的畫面由 Arc 的 Block 組成，範例資料放在服務介面後面。MVVM（CommunityToolkit.Mvvm）、相依性插入與設定儲存一律開啟。介面是英文的，因為 Block 的內建文字是英文（每個 Block 透過自己的 `Labels` 修改）。

```bash
dotnet new tkcode-arc-admin -n Backoffice
dotnet new tkcode-arc-ai-chat -n Assistant --tests
dotnet new tkcode-arc-devtool -n Studio --custom-titlebar
```

| 範本 | 內容 |
|---|---|
| `tkcode-arc-admin` | 先是登入頁（`LoginSplit`；範例帳戶的密碼是 `demo`），然後是側邊欄與這些頁面：儀表板（`MetricCard`、`LineChart`）、客戶（附搜尋、狀態篩選、分頁與詳細資料 `Drawer` 的 `Table`）、團隊（`TeamMembers`、`RolesPermissions`）、帳單（`BillingOverview`）與設定（`SettingsPage`）。頂端列有命令面板（Cmd+K / Ctrl+K）與通知。 |
| `tkcode-arc-ai-chat` | `AiChat` 旁邊是對話清單：串流 Markdown 回覆，附推理過程、附件、模型選單與建議。`SampleResponder` 不連接任何模型，串流輸出預設的回覆；它的註解中有一個以 `Microsoft.Extensions.AI` 的 `IChatClient` 為基礎的寫法。對話儲存在設定旁邊的 `conversations.json` 中。 |
| `tkcode-arc-devtool` | `DockLayout` 版面配置：檔案總管（`TreeView`）、編輯器索引標籤（Cmd+S / Ctrl+S 儲存）、`PropertyGrid`、建置輸出與問題清單、Run 按鈕、命令面板與「重設版面配置」。開啟的檔案隨版面配置一起儲存，下次啟動時還原。 |

它們接受 `--tests`、`--custom-titlebar`、`-f`、`--accent` 與 `--no-restore`，作用與 `tkcode-arc` 相同。要改成自己的應用程式，替換 `SampleData/` 中的範例服務即可：頁面只與 `Services/` 中的介面互動，`Services/AppServices.cs` 註冊由誰實作它們。

開發工具沒有終端機面板。`TKCode.Arc.Terminal` 是終端機模擬器，背後沒有處理程序，需要你自行接上 pty；等應用程式有了要互動的處理程序再加入。

## 撰寫控制項程式庫

`tkcode-arc-lib` 建立一個以 Arc 為基礎的自有控制項程式庫：

```bash
dotnet new tkcode-arc-lib -n Acme.Controls
cd Acme.Controls
dotnet test
dotnet run --project samples/Acme.Controls.Sample
dotnet pack src/Acme.Controls -c Release -o artifacts
```

`src/<name>/` 中有一個範例控制項（`StatTile`）及其設計 Token 類別與佈景主題，以及供 `UseArc(...)` 呼叫的 `Use<Name>()` 方法，它註冊 Foundation 產生器找到的全部內容。`tests/` 在 Avalonia 的 Headless 平台上執行這些控制項，`samples/` 是展示它們的桌面應用程式。`--tests false` 與 `--sample false` 可以去掉這兩個專案。範例 Token 的身分以程式庫名稱開頭（`AcmeControlsStatTile`），因為身分在應用程式註冊的所有套件（包括 Arc 自己的）中必須唯一。

## 在應用程式中加入元件

在專案已還原之後，在專案資料夾中執行項目範本（它們讀取專案的 `RootNamespace`；否則請傳入 `--namespace`）：

```bash
dotnet new tkcode-arc-page -n Reports                 # Pages/ReportsPage.axaml
dotnet new tkcode-arc-page -n Reports --view-model    # ... and ViewModels/ReportsPageViewModel.cs
dotnet new tkcode-arc-window -n About                 # Views/AboutWindow.axaml
dotnet new tkcode-arc-dialog -n Rename                # Dialogs/RenameDialog.axaml
dotnet new tkcode-arc-control -n StatusTile           # Controls/StatusTile/: StatusTile.cs, StatusTileToken.cs, Themes/StatusTileTheme.axaml
```

- **頁面**：要顯示它，把它加入 `Navigation/AppPages.cs`，並在導覽中新增一個索引鍵相同的項目。頁面 `.axaml.cs` 頂端的註解寫有這兩行程式碼。
- **對話方塊**：在視窗中的任何控制項裡呼叫 `if (await RenameDialog.ShowAsync(this, "Draft") is { } result) { ... }`；傳回 null 表示已取消。
- **控制項**：應用程式自己的組件在 `UseArc(...)` 中註冊後，Arc 才會載入控制項的佈景主題並發布它的 Token。`AppSetup.cs` 中有這行程式碼，預設被註解掉（使用 `--i18n` 時已經啟用）；加入第一個控制項時取消註解即可。Token 類別名稱去掉 `Token` 後不能與 Arc 已用的名稱重複（`Card`、`Badge` 等）；如果重複，請設定 `[ControlDesignToken(Identity = "...")]`。

## 解除安裝

```bash
dotnet new uninstall TKCode.Arc.Templates
```
