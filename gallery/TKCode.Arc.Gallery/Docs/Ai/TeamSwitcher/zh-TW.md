---
title: Team switcher
summary: 工作區切換器：觸發器顯示目前工作區的標誌與方案，選單可以搜尋你的工作區，為每個工作區顯示你的角色與 Cmd/Ctrl+數字快速鍵，並能就地建立或加入工作區。
source-hash: e7beb3710fa5
---

<Demo name="Preview" />

## 何時使用

`TeamSwitcher` 放在應用程式側邊欄頂端或頁首，讓使用者在自己所屬的組織、團隊、專案或商店之間切換。觸發器顯示使用者目前所在的位置：
工作區的標誌、名稱與方案。選單可以依名稱、方案或角色尋找工作區，為每個工作區顯示角色與鍵盤快速鍵，並且不必離開頁面就能建立或加入工作區。
切換時，觸發器會經過一段短暫的載入狀態，交叉淡化到新的工作區。

- **多租戶應用程式**：一個帳號屬於多個組織或團隊。
- **帳號、專案或商店選擇器**：需要搜尋、角色與鍵盤快速鍵。
- **建立工作區或接受邀請**：不必把使用者帶到另一個頁面。

## 何時不用

- **表單中一份簡短的固定清單**：使用 `Select` 或 `SegmentedControl`。
- **跨頁面與指令的全域導覽**：使用指令面板。
- **同一份資料的兩三種檢視**：使用 `Tabs`。

## 用法

加入你的工作區，並把目前工作區的 id 繫結到 `Value`。把 `SwitchHandler` 接到載入工作區的邏輯上（切換工作階段、預先擷取路由）；在你的工作完成之前，
觸發器會以載入狀態顯示新的工作區。你沒有提供的每個委派都會被模擬。沒有工作區（也沒有下文的模擬）時，觸發器顯示 "No workspace"，選單在 Create workspace 與 Join with invite 上方顯示沒有工作區（上方的 Empty 分頁）。

```xml
<arc:TeamSwitcher xmlns:arc="https://github.com/AIDotNet/TKCode.Arc" Value="acme">
    <arc:TeamSwitcherWorkspace Id="acme" Name="Acme" Plan="Enterprise" Members="214" Role="Admin" />
    <arc:TeamSwitcherWorkspace Id="acme-labs" Name="Acme Labs" Plan="Pro" Members="12" Role="Owner" />
</arc:TeamSwitcher>
```

```csharp
switcher.SwitchHandler = (workspace, token) => session.SwitchAsync(workspace.Id, token);
switcher.ValueChanged += (_, e) => navigation.GoHome(e.Id);
```

### 結構

1. **觸發器**：工作區的圖塊、名稱與其下方的方案列（「Enterprise · 214 members」），以及一個在載入工作區時變成載入圈的箭頭。
   它高 52px，在不超過 360px 的範圍內填滿容器。其下的選單與觸發器同寬，但至少 288px，這樣切換器放在窄側邊欄中時名稱依然清楚易讀。
2. **搜尋**：輸入時依名稱、方案或角色篩選清單。在清單中移動時焦點一直留在這裡。
3. **列**：一次顯示六列，每列 48px，超出後清單捲動，但不顯示捲軸。每一列包含圖塊、名稱與方案列（兩者都會截斷）、你的角色、
   快速鍵，目前的工作區還有一個勾號。
4. **動作**：分隔線下方的「建立工作區」與「透過邀請加入」。
5. **醒目提示**：一塊低調的色塊，滑動到指標或鍵盤所指的那一列。

### 你的工作區

<Demo name="YourWorkspaces" />

每個 `TeamSwitcherWorkspace` 都有 `Id` 與 `Name`，也可以有 `Plan`、`Members` 與你的 `Role`。沒有標誌時，它的圖塊在一個方塊中顯示名稱的
第一個字母，方塊的色調取自主題的強調色與系列色（色調由名稱決定，所以同一個工作區永遠保持同一種顏色）。租戶的品牌標誌用 `Logo`
提供（24 × 24 方框內的填滿幾何圖形），並放在 `TileBackground` 上；也可以把標誌圖片（點陣圖或 `DrawingImage`）放進 `LogoImage`，
它會鋪滿整個圖塊。`TeamSwitcherTile` 可以單獨繪製同樣的圖塊，例如在設定頁中。

### 切換

點選一列、在列上按 Enter 或按它的快速鍵，都會關閉選單並開始切換。`SwitchHandler` 執行期間，觸發器立刻顯示新的工作區，
帶有閃爍的「Switching…」與載入圈；工作完成後再顯示它的方案，此時 `Value` 才會改變並引發 `ValueChanged`。如果工作擲回例外，
觸發器會回到先前的工作區並顯示例外訊息幾秒鐘，同時引發 `SwitchFailed`。較新的切換會取消較早切換的權杖；你自己設定 `Value`
會放棄進行中的載入。

呼叫 `SwitchToAsync(id)` 可以用同樣的方式從程式碼切換。

### 建立與加入

「建立工作區」與「透過邀請加入」會開啟內嵌表單，以交叉淡化的方式取代清單；面板高度維持不變。搜尋沒有相符項目時，按 Enter
會開啟「建立工作區」，並把搜尋文字當作名稱填好。

```csharp
switcher.CreateHandler = async (name, token) =>
{
    var created = await api.Workspaces.CreateAsync(name, token);   // throw to show a message under the field
    return new TeamSwitcherWorkspace(created.Id, created.Name, "Free", 1, "Owner");
};
switcher.JoinHandler = (invite, token) => api.Invites.AcceptAsync(invite, token);
switcher.WorkspaceAdded += (_, e) => myWorkspaces.Add(e.Workspace);
```

傳回新的工作區即可加入並切換過去，傳回 null 則只關閉選單。切換器會立刻顯示它建立或加入的工作區；也請在 `WorkspaceAdded`
中把它加入你自己的清單，切換器會依 id 去除重複。沒有 `CreateHandler` 時，模擬的建立會用這個名稱建立一個只有一名成員的工作區；沒有 `JoinHandler` 時，模擬的加入會拒絕所有邀請，除非模擬中列出了它。

用於展示或原型時，讓 `Workspaces` 維持空白，並給切換器一個 `TeamSwitcherSimulation`：它的工作區、模擬加入所接受的邀請（每個都是一個 `TeamSwitcherSimulationInvite`：一個邀請碼，可以單獨比對，也可以位於連結結尾，以及它加入的工作區；加入檢視會列出這些邀請碼），以及模擬建立時給予的方案與角色。你自己的 `Workspaces` 會讓它讓位，把 `Simulation` 設為 null 會清空切換器。預覽中的工作區與邀請（ADATUM-4821、TAILSPIN-0917）是虛構的展示資料。

```csharp
switcher.Simulation = new TeamSwitcherSimulation
{
    Workspaces = [new TeamSwitcherWorkspace("acme", "Acme", "Enterprise", 214, "Admin")],
    Invites = [new TeamSwitcherSimulationInvite { Code = "ACME-1234", Workspace = new TeamSwitcherWorkspace("acme-labs", "Acme Labs") }],
    CreatedPlan = "Free",
    CreatedRole = "Owner",
};
```

### 快速鍵

<Demo name="Sidebar" />

前九個工作區在 macOS 上對應 Cmd+1 到 9，在其他系統上對應 Ctrl+1 到 9。焦點在切換器內部時它們有效；
設定 `UsesGlobalShortcuts="True"` 後，在視窗的任何位置都有效。

### 停用

<Demo name="Disabled" />

停用的切換器仍然顯示工作區，但不會開啟，也會忽略快速鍵。

### 自訂文字

`Labels` 包含切換器顯示或朗讀的所有文字。取代整個物件即可在地化或改寫：

```csharp
switcher.Labels = new TeamSwitcherLabels
{
    Search = "Arbeitsbereich suchen…",
    CreateWorkspace = "Arbeitsbereich erstellen",
    PlanLine = (plan, members) => $"{plan} · {members} Mitglieder",
};
```

### 減少動態效果

減少動態效果時，選單、觸發器的各部分與表單都以單純的淡入淡出交換，醒目提示直接移動而不滑動。關閉動態效果時一切立即改變。

### 文案規範

- 用大家熟悉的名稱命名工作區，而不是它的 id。
- 方案用一個詞（「Pro」「Enterprise」）；角色也一樣（「Owner」「Admin」「Member」「Guest」）。
- 錯誤訊息要說明發生了什麼事以及該怎麼做：「That invite has expired. Ask for a new one.」

### 最佳做法

- 讓切換工作盡量簡短，交給觸發器顯示載入狀態；不要卡住頁面。
- 把切換器放在下方大約有 450px 空間的位置，或放在選單可以溢出的地方。
- 維持清單順序穩定，讓快速鍵始終不變。

### 常見問題

**應用程式可以開啟選單嗎？** 設定 `IsOpen`，這樣開啟不會移動焦點。`Open()` 會開啟選單並聚焦搜尋欄位。

**模擬的操作會做什麼？** 沒有 `SwitchHandler`、`CreateHandler` 或 `JoinHandler` 時，它會等待 `SimulatedDelay`（650ms），並播報沒有傳送任何內容。

## API 參考

<!-- api: TeamSwitcher -->

<!-- api: TeamSwitcherWorkspace -->

<!-- api: TeamSwitcherTile -->

<!-- api: TeamSwitcherLabels -->

<!-- api: TeamSwitcherSimulation -->

<!-- api: TeamSwitcherSimulationInvite -->

<!-- api: TeamSwitcherValueChangedEventArgs -->

<!-- api: TeamSwitcherSwitchFailedEventArgs -->

<!-- api: TeamSwitcherWorkspaceAddedEventArgs -->

<!-- api: TeamSwitcherOpenChangedEventArgs -->

<!-- api: TeamSwitcherAddKind -->

## 無障礙

- 觸發器是一個按鈕，會回報選單是否開啟。它的名稱讀出工作區與其方案（「Northwind, Enterprise plan」），載入工作區時狀態為「Switching…」。
- 方向鍵在清單中移動時，搜尋欄位一直保有焦點；它的說明文字是醒目提示列的名稱，因此螢幕閱讀器不必讓焦點離開欄位就能讀到它。
- 目前的工作區以勾號以及名稱中的「current」標示，而不只依靠顏色。錯誤同時顯示警告圖示與文字。
- 一個禮貌的即時區域會播報切換開始、切換結果，以及工作由模擬操作完成的情況（「Simulated, nothing was sent」）。沒有工作區時，觸發器的名稱為 "No workspace"。
- 用 Escape 關閉或選擇後，焦點回到觸發器。用 Tab 離開或點選外部會關閉選單。

### 鍵盤

| 按鍵 | 動作 |
|---|---|
| Enter、Space、下方向鍵 | 在觸發器上開啟選單，並把焦點移到搜尋欄位 |
| 輸入 | 依名稱、方案或角色篩選；沒有相符項目時，Enter 開啟「建立工作區」並填好名稱 |
| 下方向鍵、上方向鍵 | 在工作區與動作之間移動醒目提示，到兩端時循環 |
| Home、End | 搜尋為空時跳到第一項或最後一項 |
| Enter | 切換到醒目提示的工作區，或開啟「建立工作區」或「透過邀請加入」 |
| Cmd/Ctrl+1 到 9 | 切換到清單中對應位置的工作區（在切換器內部，或開啟全域快速鍵後在任何位置） |
| Escape | 先從表單返回，再清除搜尋，最後關閉選單並把焦點還給觸發器 |
| Tab | 離開選單並將其關閉 |

## 動態效果

| 變化 | 完整 | 減少 | 關閉 |
|---|---|---|---|
| 選單開啟 | 在 180ms 內從 98% 淡入並放大 | 120ms 淡入 | 立即 |
| 選單關閉 | 在 120ms 內淡出並縮小到 98% | 120ms 淡出 | 立即 |
| 醒目提示移動 | 沿 0.24s 無回彈彈簧滑動；在原位出現，絕不從頂端滑入 | 直接跳到定位 | 直接跳到定位 |
| 切換 | 標誌、名稱與方案列各自交叉淡化：100ms 淡出，160ms 淡入並上升 2px；接著顯示閃爍文字與載入圈 | 120ms 淡入淡出，沒有閃爍 | 立即 |
| 開啟建立或加入 | 檢視交叉淡化（100ms 淡出，160ms 淡入），並沿前進方向平移 6px | 120ms 淡入淡出 | 立即 |

沒有任何東西改變尺寸：觸發器在所有狀態下維持同樣的高度，選單面板在搜尋或開啟表單時也維持同樣的高度。

## 相關元件

- `DropdownMenu`：錨定在觸發器上的一組聚焦動作。
- `Popover`：小型錨定浮層。
- `Avatar`：代表一個人的精簡標識。
