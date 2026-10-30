---
title: Connector setup
summary: AI 助理的「連結你的工具」卡片：每個應用程式都在行內完成重新導向、授權和首次同步，並以一條進度細線顯示進度，接著列出取得的權限範圍，並提供附帶行內確認的中斷連結。
source-hash: f520864183fa
---

<Demo name="Preview" />

## 何時使用

`ConnectorSetup` 是讓使用者允許助理進入團隊所用應用程式的卡片。每個應用程式占一列，包含它的標記、名稱和一行狀態。
按下 Connect 會在這一列裡走完整個交握流程：登入頁面開啟、使用者核准，首次同步一邊計數已讀取的內容，一邊讓一條 2px
細線沿著這一列填滿。已連結的應用程式顯示其帳號和一個 Connected 按鈕，按下後展開助理擁有的權限範圍，並提供
Disconnect 和行內確認。

- **助理的設定頁面**：使用者在這裡連結並檢視工作區應用程式。
- **首次使用檢查清單**：需要在不離開頁面的情況下展示 OAuth 交握和首次同步。
- **複查存取權限**：檢視助理在每個應用程式中擁有哪些權限範圍，並撤銷它們。

## 何時不用

- **依類別瀏覽數十個整合**：使用 `Integrations` 區塊，這是一個附篩選的應用程式市集網格。
- **核准智慧代理想執行的某一個動作**：使用附核准的 `ToolCall`，或使用 `Dialog`。
- **展示智慧代理此刻正在呼叫的工具**：使用 `ToolCall`。

## 用法

在 `Connectors` 中傳入你的應用程式，在 `ConnectedIds` 中傳入已經連結的 id。設定 `ConnectHandler` 來執行你的 OAuth
流程：透過內容物件回報各個步驟，傳回帳號、權限範圍和同步數量；擲回附有簡短易懂訊息（"Needs admin approval"）的例外，
即可顯示該訊息和 Retry。使用者按下 Cancel 或卡片離開視窗時，內容物件的權杖會被取消。

```csharp
setup.Connectors = apps;                  // ConnectorApp: Id, Name, Description, Kind or Glyph, Scopes, SyncCount, SyncNoun
setup.ConnectedIds = savedIds;
setup.ConnectHandler = async (app, context) =>
{
    var grant = await OpenOAuthPopupAsync(app.Id, context.CancellationToken);
    context.ReportAuthorizing();
    var approved = await grant.ResultAsync;
    var total = await StartFirstSyncAsync(app.Id, approved, synced => context.ReportSyncing(synced), context.CancellationToken);
    return new ConnectorConnectResult { Account = approved.Workspace, Scopes = approved.Scopes, Synced = total };
};
setup.DisconnectHandler = (app, token) => RevokeGrantAsync(app.Id, token);
setup.ConnectedIdsChanged += (_, e) => SaveConnectedApps(e.Ids);
```

沒有設定處理常式時，流程是模擬的，不會傳送任何內容，即時區域也會說明這一點。沒有 `Connectors` 時，卡片列出它的
`Simulation` 中的應用程式；兩者都沒有時不列出任何應用程式。`AssistantName` 在文字中為助理命名；沒有設定時文字會寫成「your assistant」。

### 模擬

`ConnectorSetupSimulation` 是卡片自行播放的一組應用程式：`AssistantName`、`Apps`、已連結的 `Connected` id，以及腳本示範的
`Steps`。卡片出現在螢幕上後，每個 `ConnectorSetupSimulationStep` 先等待它的 `Pause`，然後在其應用程式處於閒置或失敗狀態時連結它；
帶有 `Failure` 的步驟會在核准後失敗，以便下一步重試。腳本示範會在卡片內第一次出現指標按下或按鍵時永久停止。你自己設定的
`Connectors`、`ConnectedIds` 與 `AssistantName` 優先於它；把 `Simulation` 設為 null 會停止示範並清空清單。

```csharp
setup.Simulation = new ConnectorSetupSimulation
{
    AssistantName = "Atlas",
    Apps = apps,                          // the same ConnectorApp records as Connectors
    Connected = ["code"],
    Steps =
    [
        new ConnectorSetupSimulationStep("chat", TimeSpan.FromMilliseconds(900)),
        new ConnectorSetupSimulationStep("crm", TimeSpan.FromMilliseconds(1200), "Needs admin approval"),
        new ConnectorSetupSimulationStep("crm", TimeSpan.FromMilliseconds(1800)),
    ],
};
```

本頁的示範從 `ConnectorSetupDemoData` 列出八種通用應用程式，帳號與數量屬於一家虛構的公司，助理也是虛構的。

### 你自己的流程

<Demo name="YourFlow" />

登入頁面等待核准時，處理常式呼叫 `ReportAuthorizing()`；首次同步推進時呼叫 `ReportSyncing(synced, total)`。這些回報
呼叫可以來自任何執行緒。`ConnectedIds` 是雙向的，每次連結或中斷完成後都會寫回，同時引發 `ConnectedIdsChanged`。
`AppConnected` 附帶帳號、權限範圍和同步數量；`AppDisconnected` 在撤銷之後引發。

### 新手引導

<Demo name="Onboarding" />

`IsSearchable="False"` 為較短的清單拿掉搜尋欄位。`Title`、`Description` 和 `Footer` 取代標題、標題下的一行文字和
隱私說明（`IsFooterVisible="False"` 隱藏說明）。`AssistantName` 用於描述、權限範圍標題、確認問題和頁尾。

### 標記

<Demo name="Marks" />

應用程式以代表其類型的中性 Lucide 圖示顯示（`ConnectorMarkKind`：Chat、Code、Docs、Drive、Issues、Calendar、Mail、
Crm、Database、Analytics），絕不使用品牌標誌。在 `ConnectorApp.Glyph` 中傳入應用程式自己的 24 × 24 描邊標記；兩者
都沒有時，顯示名稱的首字母。`ConnectorMark` 可以在設定介面的其他位置繪製同樣的圖塊。

### 結構

1. **頁首**：標題、其下的一行文字和搜尋欄位（比對名稱、描述、帳號和關鍵字）。
2. **列**：高 60px。標記、名稱及其下的一行狀態，以及一個 32px 的動作：Connect、Cancel、Retry 或 Connected。
3. **進度細線**：應用程式連結期間，沿列底邊顯示的 2px 細線。
4. **存取詳細資料**：位於已連結的列下方，與其文字對齊："Atlas can" 和權限範圍，然後是 Disconnect。
5. **頁尾**：一把鎖和一段簡短的隱私說明。

### 狀態

| 階段 | 狀態行 | 動作 |
|---|---|---|
| `Idle` | 應用程式的描述 | Connect |
| `Redirecting` | "Opening chat.example.com…" | Cancel |
| `Authorizing` | "Waiting for you to approve in Chat…" | Cancel |
| `Syncing` | "Syncing 1,240 messages"，數字滾動 | Cancel |
| `Connected` | "tailspin.chat · 1,240 messages synced" | Connected，展開詳細資料 |
| `Failed` | 警示圖示和原因 | Retry |
| `Disconnecting` | "Disconnecting…" | Connected（停用） |

`GetPhase(id)` 讀取應用程式所處的階段；`StartConnect`、`CancelConnect`、`SetAccessOpen`、`Disconnect` 和
`StopAutoPlay` 用於在程式碼中驅動卡片。

### 自訂文字

`Labels` 包含卡片顯示或朗讀的每一個詞。取代整個物件即可在地化或改寫：

```csharp
setup.Labels = new ConnectorSetupLabels
{
    Title = "Verbinden Sie Ihre Tools",
    Connect = "Verbinden",
    Syncing = "Synchronisiere",
};
```

### 內容準則

- 用淺白的能力描述來寫權限範圍（"Post replies as Atlas"），不要寫原始的 OAuth scope 字串。
- 失敗訊息要簡短、易懂（"Needs admin approval"）；它與警示圖示共用一行。
- `SyncNoun` 使用複數形式（"messages"、"files"）。

### 最佳做法

- 在設定欄中只放一張卡片；它在 520px 以內隨容器伸縮，最窄可到 360px。
- 把權杖傳給流程中的每一次呼叫，這樣 Cancel 才能真正停止它。
- 應用程式有數百個時，在你這邊篩選，再把結果作為 `Connectors` 傳入。

### 常見問題

**應用程式連結期間使用者離開頁面會怎樣？** 權杖被取消，這一列回到靜止狀態。

**卡片會儲存已連結清單嗎？** 每次變化後它都會寫回 `ConnectedIds`；在 `ConnectedIdsChanged` 中儲存即可。

## API 參考

<!-- api: ConnectorSetup -->

<!-- api: ConnectorApp -->

<!-- api: ConnectorConnectResult -->

<!-- api: ConnectorConnectContext -->

<!-- api: ConnectorMark -->

<!-- api: ConnectorSetupLabels -->

<!-- api: ConnectorSetupSimulation -->

<!-- api: ConnectorSetupSimulationStep -->

## 無障礙

- 卡片是一個附標籤的群組，有真正的標題；清單中每個應用程式一項，並依搜尋顯示出的應用程式編號。
- 一個禮貌型即時區域播報每一步：正在連結、已連結及同步數量、失敗及原因、已取消和已中斷。使用模擬流程時，會附加
  "Simulated, nothing was sent."
- 狀態從不只靠顏色表達：失敗時警示圖示與訊息同時出現，已連結的應用程式顯示勾號和 Connected 一詞。
- Connected 按鈕會回報展開或摺疊狀態。Disconnect 會先詢問；按下後焦點移到安全的回答 Cancel 上，破壞性按鈕只會由
  使用者自己聚焦。
- 正在淡出的內容會立即離開無障礙樹狀結構，並且不再接收點擊。

### 鍵盤

| 按鍵 | 動作 |
|---|---|
| Tab | 依序經過搜尋欄位、每一列的 Connect、Cancel、Retry 或 Connected 按鈕，以及已展開的詳細資料 |
| Enter、Space | 連結、取消、重試，或展開、收合已連結應用程式的存取詳細資料 |
| Escape | 清除搜尋欄位，或退出中斷連結的確認 |

## 動態效果

| 變化 | 完整 | 減少 | 關閉 |
|---|---|---|---|
| 狀態行或動作變化 | 舊內容在 100ms 內淡出，新內容在 160ms 內淡入並上升 2px，位於同一個儲存格 | 立即 | 立即 |
| 步驟之間的進度 | 細線在 240ms 內緩動 | 立即 | 立即 |
| 同步時的進度 | 細線線性跟隨計數；數字以等寬數字滾動，寬度依最終值預留 | 隨計數跳變 | 隨計數跳變 |
| 連結完成 | 細線填滿後淡出 | 立即 | 立即 |
| 詳細資料展開或收合 | 高度以彈簧變化（0.24s，無回彈），內容淡入淡出 | 立即 | 立即 |
| 箭頭 | 以彈簧轉動 | 立即 | 立即 |
| 腳本示範（模擬） | 卡片出現在螢幕上後開始，首次按下時停止 | 相同 | 相同 |

減少動態效果會保留所有狀態和播報。每種狀態下列高都是 60px，所以應用程式連結期間，它下方的內容不會移動。

## 相關元件

- `ToolCall`：智慧代理此刻正在呼叫的工具，可就地核准。
- `AgentStatus`：整個智慧代理執行的即時狀態。
- `ConfirmMorph`：就地詢問的單一破壞性按鈕。
