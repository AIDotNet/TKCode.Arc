---
title: MCP 伺服器
summary: 管理助理可以呼叫的工具伺服器：每個伺服器都有即時狀態，工具由開關控制，連線失敗時顯示錯誤和記錄，並可透過 URL 或命令新增伺服器。
source-hash: 2d02a8e47e09
---

<Demo name="Preview" />

## 何時使用

MCP 伺服器是一張設定卡片，人們在這裡決定助理可以呼叫哪些工具伺服器。工具伺服器（使用 Model Context Protocol 的伺服器）可以透過 HTTP 位址存取，也可以作為本機程式啟動，它會列出 `search_notes`、`post_entry` 這樣的工具。卡片把每個伺服器顯示為一列：標記、名稱、HTTP 或 Local 標籤、即時狀態列（"Connected · 5 tools"、"Connecting…"、"Couldn't connect"、"Off"）以及一個開關。展開一列會顯示它的工具，每個工具都有自己的開關；會寫入資料的工具帶有 "Changes data" 標籤，另有 Enable all 和 Disable all。連線失敗的伺服器會顯示錯誤訊息、可以打開的記錄和 Retry。這一列還列出位址或命令、請求標頭或環境變數的鍵名以及版本，並提供 Reconnect 和 Remove（先確認）。Add server 打開一個表單，可以填寫 URL，或填寫命令及其參數，還可以加上請求標頭或環境變數；新伺服器加入清單後會立即連線。

- **帶有外掛工具的助理或代理**：人們能看到哪些已連線、哪些失敗了，以及每個工具能做什麼。
- **最小權限**：不必刪除，就能關閉一個會寫入資料的工具，或關閉整個伺服器。
- **修復連線問題**：錯誤、記錄和 Retry 都在同一個地方。

## 何時不用

- **一鍵連接應用程式的目錄**：請使用 `Integrations`。
- **選擇由哪個模型回答**：請使用 `ModelPicker`。
- **編輯金鑰**：卡片只在新增伺服器時接收請求標頭和變數，之後不會再顯示它們的值；金鑰請用 `ApiKeys` 或 `EnvVariables` 管理。
- **執行協定本身**：卡片只顯示並修改一組 `McpServer` 物件。連線、列出工具和儲存清單都由你的程式碼完成（`ConnectAsync` 和各個事件）。

## 用法

`McpServers` 衍生自 Avalonia 的 `TemplatedControl`。不設定任何屬性時，它會顯示四個虛構的範例伺服器並模擬連線，因此可以直接用於原型。把你自己的清單交給 `Servers`；使用 `ObservableCollection<McpServer>` 時，卡片會跟隨你的修改，你也能看到卡片做出的修改。

```xml
<arc:McpServers Servers="{Binding ToolServers}" />
```

```csharp
var servers = new ObservableCollection<McpServer>
{
    new McpServer
    {
        Id = "field-notes", Name = "Field notes", Transport = McpTransport.Http,
        Url = "https://notes.example.dev/mcp", Status = McpServerStatus.Connected,
        Tools = { new McpTool("search_notes", "Finds notes that match a query.") }
    }
};
block.Servers = servers;
```

### 結構

- **頁首**：標題、一句說明、摘要（"3 of 4 connected · 8 tools on"）以及 Add server。
- **新增伺服器表單**：名稱、URL 或 Command、位址或命令及其參數、可選的請求標頭或環境變數，以及 Cancel 和 Add and connect。
- **伺服器列**：標記（名稱首字母）、名稱和傳輸標籤、狀態圓點和文字、位址或命令列、展開箭頭以及開關。
- **展開的伺服器**：錯誤框（訊息、Retry、Show log）、帶開關的工具、詳細資訊、Reconnect 和 Remove。

### 連線

設定 `ConnectAsync` 以進行真正的連線。它會收到伺服器和一個取消權杖；傳回 `McpConnectResult.Success(tools, version)` 或 `McpConnectResult.Failure(message, log)`。擲回例外時，以例外訊息作為失敗原因。當伺服器被關閉、被移除、再次連線，或卡片離開視窗時，權杖會被取消。伺服器再次列出之前已有的工具時，這些工具保持原來的開關狀態。

<Demo name="Connect" />

```csharp
block.ConnectAsync = async (server, cancellation) =>
{
    var session = await client.ConnectAsync(server.Url!, cancellation);
    var tools = await session.ListToolsAsync(cancellation);
    return McpConnectResult.Success(tools.Select(t => new McpTool(t.Name, t.Description)), session.ServerVersion);
};
```

未設定 `ConnectAsync` 時，卡片會等待 `SimulatedConnectDuration`（1.2 秒），然後使用 `McpServersSamples.SimulateConnect`：主機名稱以 `.invalid` 結尾的位址，或名稱以 `missing` 開頭的命令會失敗並給出記錄；其他情況都會成功，並得到三個虛構的工具。

### 新增和移除

Add server 打開表單（`IsAddFormOpen`、`OpenAddForm()`）。表單會檢查名稱（必填，且不能重複）、位址（以 `http://` 或 `https://` 開頭的完整位址）或命令（必填），以及變數（每行一個 `KEY=value`）。焦點會移到第一個有問題的欄位，輸入時錯誤會隨之更新。表單有效時會新增伺服器、引發 `ServerAdded`、關閉表單並連線伺服器。`AddServer(draft)` 在程式碼中做同樣的事。

Remove 會先詢問："Remove Field notes? Its 5 tools stop being offered."。Remove server 會把它移出清單並引發 `ServerRemoved`；Keep 或 Escape 則返回。把 `CanAddServers` 或 `CanRemoveServers` 設為 false 可以隱藏這些操作；唯讀清單會同時隱藏兩者。

<Demo name="Empty" />

### 關閉伺服器和工具

列上的開關可以關閉伺服器（狀態變為 Off，連線被取消）或打開它（開始連線）。每個工具的開關決定是否提供這個工具；Enable all 和 Disable all 一次改變全部工具。`ServerToggled` 和 `ToolToggled` 報告這些變化，`ConnectedCount` 和 `EnabledToolCount` 給出摘要中顯示的總數。

### 在窄欄中

設定 `ExpandedServer` 可以在程式碼中展開一個伺服器。在側邊面板中，卡片會依自身寬度收緊內距。

<Demo name="Narrow" />

### 文案

所有文字都來自 `Labels`（`McpServersLabels`）：替換這個實例即可翻譯或改寫。帶有數值的句子是函式，因此可以調整語序。控制項上的 `Title` 和 `Subtitle` 優先於文案物件。

```csharp
block.Labels = new McpServersLabels
{
    Title = "Serveurs d'outils",
    AddServer = "Ajouter un serveur",
    RemovedAnnouncement = name => $"{name} retiré"
};
```

### 內容準則

- **依伺服器保存的內容來命名**（"Field notes"、"Ledgerline"），不要用協定名稱來命名。
- **用一句以動詞開頭的話描述工具**："Finds notes whose title or text matches a query."
- **給每個會寫入、傳送或刪除內容的工具設定** `ChangesData`；人們會最先關閉這些工具。
- **錯誤要說明發生了什麼以及可以怎麼做**："The server answered 401: the access key was refused."

## API 參考

<!-- api: McpServers -->

<!-- api: McpServer -->

<!-- api: McpTool -->

### 事件

| 事件 | 何時引發 | 參數 |
|---|---|---|
| `ServerAdded` | 有伺服器加入清單 | `Server` |
| `ServerRemoved` | 有伺服器離開清單 | `Server` |
| `ServerToggled` | 伺服器的開關改變 | `Server`、`IsEnabled` |
| `ToolToggled` | 工具被打開或關閉 | `Server`、`Tools`、`IsEnabled` |
| `StatusChanged` | 伺服器的狀態改變 | `Server`、`OldStatus`、`NewStatus` |

## 無障礙

- 每個伺服器的標題是一個按鈕，以伺服器名稱命名，以狀態和位址作為說明，並報告展開或收合。它旁邊的開關名為 "Use Field notes"。
- 工具開關以工具名稱命名（會寫入資料時加上 "Changes data"），並以工具的說明句作為描述。
- 狀態從不只靠顏色表達：文字會說明狀態，打開時圓點是實心的，關閉時是空心的。
- 一個禮貌的即時區域會播報：正在連線、已連線及工具數量、失敗及其訊息、新增、移除、關閉伺服器以及每一次工具變化。
- 表單欄位都有可見的標籤；錯誤顯示在欄位下方，焦點會移到第一個錯誤。

### 鍵盤

| 按鍵 | 操作 |
|---|---|
| `Tab` | Add server，然後是每個伺服器的標題和開關，再然後是展開的伺服器中的控制項 |
| 標題上的 `Enter` 或 `Space` | 展開或收合伺服器 |
| 標題上的 `ArrowDown` / `ArrowUp` | 下一個或上一個伺服器 |
| 標題上的 `Home` / `End` | 第一個或最後一個伺服器 |
| 展開的伺服器中的 `Escape` | 收合它（或取消移除的詢問），並回到它的標題 |
| 單行表單欄位中的 `Enter` | 新增伺服器 |
| 表單中的 `Escape` | 取消表單並回到 Add server |

## 動效

- 展開的伺服器內容依平滑彈簧變高；展開箭頭依彈簧轉動。
- 狀態文字透過短暫的標籤切換改變（舊文字 100 毫秒淡出，新文字 160 毫秒淡入並上升 2 像素）。
- 已連線圓點的光暈每 2.4 秒向外呼吸一次；正在連線的圓點每 0.9 秒呼吸一次。
- 表單的高度依彈簧展開和收合。
- 列的底色在 160 毫秒內變化。

| 變化 | 完整 | 減弱 | 關閉 |
|---|---|---|---|
| 展開伺服器 | 高度彈簧，箭頭轉動 | 高度和箭頭直接跳到位 | 立即 |
| 狀態文字 | 標籤切換 | 120 毫秒交叉淡化 | 立即 |
| 即時圓點 | 呼吸 | 靜止 | 靜止 |
| 表單 | 高度彈簧 | 直接跳到位 | 立即 |

## 響應式

- 卡片依自身寬度變化。寬度不超過 520 像素時（`:compact`），頁首、清單、表單和展開內容的內距都會收緊；卡片最寬 720 像素，在更寬的容器中置中。
- 名稱和位址列以省略號結尾；工具說明、錯誤和記錄會換行。記錄超過 160 像素時捲動。
- 在表單中，命令和參數依 2:3 的比例共用一列。
- 讓卡片自然拉伸，不要給它固定寬度。

## 相關元件

- `Integrations`：連接應用程式的目錄。
- `AgentMemory`：助理記住的內容。
- `ModelPicker`：呼叫這些工具的模型。

| 元件 | 何時選擇 |
|---|---|
| `Integrations` | 人們從目錄中連接應用程式 |
| `ApiKeys` | 人們建立和撤銷金鑰 |
| `Switch` | 一個設定的打開或關閉 |
| `Accordion` | 只需展開和收合各部分，不帶狀態和操作 |
