---
title: 更新日誌
source-hash: ce993bc90d50
---

# 更新日誌

TKCode.Arc 的所有重要變更，最新的在前。版本遵循[語意化版本](https://semver.org/)。版本帶有 `-preview` 後綴期間，API 在各發行版本之間仍可能變動；升級前請閱讀說明，並在專案中固定版本號。同一份清單也保存在儲存庫的 `CHANGELOG.md` 中。

每次發行附帶的 NuGet 套件：`TKCode.Arc`、`TKCode.Arc.Core`、`TKCode.Arc.Fonts` 與 `TKCode.Arc.Icons.Lucide`。`TKCode.Arc.Blocks` 與 `TKCode.Arc.AdvancedCharts` 從儲存庫建置。

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
