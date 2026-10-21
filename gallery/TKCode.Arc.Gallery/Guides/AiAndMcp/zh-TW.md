---
title: AI 與 MCP
source-hash: 625e2175ed37
---

# AI 與 MCP

AI 程式設計助理能很好地配合 TKCode.Arc 使用，因為它需要的一切都以純文字寫成：每個元件都有一份根據其原始碼撰寫的 Markdown 頁面，每個公開屬性與事件都有 XML 說明，而且各部分的慣例保持一致。產生的程式碼能否編譯、會不會憑空捏造屬性，關鍵在於助理拿到的脈絡。本頁說明這些脈絡位於何處、如何提供給助理，以及如何審查它回傳的結果。

## 助理應先了解的事項

助理在本程式庫上出錯，大多源於幾個錯誤的假設。請在專案指示檔中或對話開始時先把這些事實說明清楚：

| 事實 | 重要原因 |
|---|---|
| 這是 Avalonia 12 與 AtomUI 6.2.7，而不是 WPF、WinUI 或 HTML。 | 否則助理會混入 WPF 屬性（`Visibility`、`DataTemplateSelector`）或 CSS 的概念。 |
| Arc 控制項位於 `xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc"`。 | 沒有該前綴時，`Button` 會解析為 Avalonia 自帶的按鈕，失去 Arc 的外觀。 |
| 應用程式在 `UseAtomUI(...)` 中呼叫 `UseArc()`，並且 `Application` 類別為 `partial`。 | 缺少其中任何一項，都會導致控制項沒有樣式或建置錯誤。 |
| 顏色、圓角與字型來自 `{arc:ArcDesignTokenResource ...}` 或 `ArcTokens`。 | 寫死的十六進位數值會破壞深色模式與強調色。 |
| 動態效果宣告在 `^:motion-full` 與 `^:motion-reduced` 之下，並遵循 `ArcMotion.Level`。 | 手寫的動畫會忽略減少動態效果的設定。 |
| 只存在文件中列出的屬性。 | 元件頁面與 API 表格列出了全部屬性；其他屬性都是憑空捏造的。 |

## 知識的來源

| 來源 | 位置 | 最適合 |
|---|---|---|
| 元件頁面 | `gallery/TKCode.Arc.Gallery/Docs/<Category>/<Component>/en-US.md`（另有 `zh-CN.md`、`zh-TW.md`） | 了解何時使用某個元件、用法、繫結、驗證、無障礙、鍵盤與動態效果。 |
| 即時範例 | `gallery/TKCode.Arc.Gallery/Demos/<Category>/<Component>/*.axaml` | 頁面中每個範例的可執行 XAML。 |
| API 說明 | 隨套件發佈的 XML 文件 | 每個公開屬性、事件與型別，即 IntelliSense 顯示的內容以及 API 表格列出的內容。 |
| 本圖庫 | 文件區及每個元件頁面 | 結合即時範例在脈絡中閱讀。 |
| 儲存庫慣例 | 儲存庫根目錄下的 `AGENTS.md` | 處理函式庫本身的貢獻者與助理。 |

### 複製頁面

每個元件頁面頂端都有 **複製頁面**。它會把整個頁面複製為 Markdown，包括每個即時範例的原始碼（`.axaml` 及其程式碼後置檔）以及產生的 API 表格。在請求元件協助時把它貼入對話中，助理便會依據真實的 API 與真實的範例工作，而不是憑記憶。

若某個畫面使用了多個元件，請複製這些元件各自的頁面；如果助理能直接讀取檔案，也可以讓它查看儲存庫中的 Markdown 檔案。

## 專案指示檔

Claude Code、Cursor 與 GitHub Copilot 等助理會讀取儲存庫中的指示檔（例如 `AGENTS.md`、`CLAUDE.md` 或 `.github/copilot-instructions.md`）。像下面這樣、並針對你的應用程式改寫的一節，可以避免上述大部分錯誤：

```text
## UI 函式庫：TKCode.Arc

- 介面使用 Avalonia 12.1.4 + AtomUI 6.2.7 + TKCode.Arc。不要使用 WPF 或 WinUI 的 API。
- Arc 控制項使用 xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc"（arc:Button、arc:Input 等）。
- 只使用元件文件頁面或 API 表格中列出的屬性、事件與列舉值。
- 顏色、圓角、間距與字型：XAML 中使用 {arc:ArcDesignTokenResource Name}，程式碼中使用 ArcTokens.Bind。
  永遠不要寫死顏色。
- 表單：使用帶有 Label 與 Description 的 arc:Input / arc:Select / arc:Switch；錯誤透過 ErrorMessage
  或被繫結屬性上的資料驗證顯示。
- 動態效果：Arc 控制項會自行產生動畫。對於自訂控制項，請在 ^:motion-full 與
  ^:motion-reduced 之下宣告轉場，並設定 arc:ArcMotion.Tracks="True"。
- 檢視模型使用 CommunityToolkit.Mvvm（[ObservableProperty]、[RelayCommand]）。
- 元件文件：<複製儲存庫的路徑>/gallery/TKCode.Arc.Gallery/Docs/<Category>/<Component>/en-US.md
```

## 有效的提示詞

把商業目標、元件頁面與限制條件一併交給助理。對比如下：

| 不要這樣寫 | 應當這樣寫 |
|---|---|
| 「做一個設定頁面。」 | 「用 arc:Input 建立工作區設定檢視，填寫名稱與帳單電子郵件；用 arc:Select 選擇幣別；用 arc:Switch 設定每週摘要。參考所附的 Input、Select 與 Switch 頁面。繫結到使用 ObservableValidator 的 WorkspaceSettingsViewModel，並在電子郵件失去焦點時驗證。」 |
| 「加一個圖表。」 | 「用 arc:LineChart 顯示最近 30 天的每日活躍使用者，參照所附的 LineChart 頁面。資料來自 IAnalyticsService.GetDailyActiveUsersAsync()；數字依目前的文化格式化。」 |
| 「讓它看起來更好。」 | 「依照 Input 頁面的內容指南調整此表單：句子式大小寫的標籤，每個有限制的欄位都有輔助文字，錯誤提示要說明如何修正該值。」 |

每次只請助理處理一個畫面或一個元件，並請它列出所用的屬性以及每個屬性記錄在哪裡。這份清單很容易與 API 表格對照檢查。

## 審查產生的程式碼

在接受助理撰寫的程式碼之前，請檢查：

- **名稱。** 每個 `arc:` 元素、屬性、事件與列舉值都存在於該元件的 API 表格中。XAML 中不存在的屬性會在建置時失敗；參數型別錯誤的事件處理常式則會在之後出錯。
- **權杖。** 檢視中沒有十六進位顏色或固定的圓角，改用權杖。
- **繫結模式。** 雙向屬性（`Text`、`IsChecked`、`SelectedItem`、`Value`）繫結到可設定的檢視模型屬性；唯讀屬性（`EffectiveErrorMessage`、`SelectedValues`）僅用於讀取。
- **無障礙。** 每個欄位都有 `Label`（可見，或僅當畫面上其他內容已為其命名時才以 `IsLabelVisible="False"` 隱藏）；只有圖示的按鈕都有無障礙名稱。
- **動態效果。** 控制項已經產生動畫的地方不再手寫動畫；自訂轉場放在動態效果偽類之下。
- **執行緒。** 來自背景工作的值在 UI 執行緒上設定（`Dispatcher.UIThread`）。

## MCP

模型脈絡協定（MCP）讓助理可以從伺服器讀取資源並呼叫工具，而不必依賴對話中貼上的內容。

TKCode.Arc 目前尚未提供自己的 MCP 伺服器。在提供之前，有兩種方式可以直接把文件交給助理：

- **能讀取檔案的助理**（Claude Code、Cursor 等）：把儲存庫複製到你的專案旁邊，或將其加入工作區，並讓助理指向 `gallery/TKCode.Arc.Gallery/Docs` 與 `.../Demos`。
- **只使用 MCP 的用戶端**：在文件資料夾上執行檔案系統 MCP 伺服器。使用 MCP 專案的參考伺服器時，用戶端設定如下：

```json
{
  "mcpServers": {
    "tkcode-arc-docs": {
      "command": "npx",
      "args": ["-y", "@modelcontextprotocol/server-filesystem", "/path/to/AtomUI.Arc/gallery/TKCode.Arc.Gallery/Docs"]
    }
  }
}
```

這樣助理就能列出各元件資料夾，並讀取它所用的每個元件的頁面。請讓複製的儲存庫與你應用程式中套件的版本保持一致，使文件所描述的 API 與你編譯所用的 API 相同。
