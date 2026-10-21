---
title: 使用者選單
summary: 頭像背後的帳號、設定、主題與登出。在手機上以底部面板的形式開啟。
source-hash: 639cc7cc0bf1
---

<Demo name="TopBar" />

## 何時使用

使用者選單是應用程式頂列中唯一的帳號入口。它位於頭像之後，匯集已登入使用者相關的內容：身分資訊標頭、兩到四個帳號入口、主題選擇、可選的線上狀態，以及登出。它讓帳號相關內容遠離頁面工具列，頁面自身的操作保持可見，而帳號仍然只需一按即可到達。

- **SaaS 管理後台中的帳號入口**：管理多個工作區的管理員，其頭像之後有個人資料、工作區設定和帳單。方案徽章在名稱旁顯示「管理員」或「Pro」。
- **團隊應用程式中的線上狀態**：值班工程師在應變期間把狀態設為「忙碌」，頭像上的圓點即可告訴同事其狀態，無需傳送聊天訊息。
- **儀表板或設計工具中的主題選擇**：在選單中選擇「淺色」「深色」或「跟隨系統」。選擇之後選單保持開啟，使頁面可以在使用者面前切換主題。
- **耗時較長的登出**：登出時需要在伺服器端撤銷權杖。該列會顯示載入指示器，直到工作完成，選單隨後關閉。
- **窄視窗與手機**：寬度低於 640 px 時，同一個選單以底部面板的形式開啟，因此手機上也能找到帳號，而無需第二種導覽模式。

## 何時不用

- **與帳號無關的一般命令清單**：用 `DropdownMenu`，它帶有標籤觸發器，例如報表頁面上的「操作」選單。
- **單獨使用、不在帳號選單之內的主題切換**：用 `ThemeSwitcher`，它是與特定使用者無關的淺色與深色控制項。
- **超過四個入口**：把多出的入口移到一個頁面中，再從其中一個入口連結過去。選單過長就不再是捷徑了。
- **從清單列或卡片開啟的選單**：對物件的右鍵操作用 `ContextMenu`，或在可見的觸發器上使用 `DropdownMenu`。
- **從清單中選取的表單值**：用 `Select` 或 `Combobox`。使用者選單承載的是指令與兩個切換項，而不是為表單提供數值的控制項。

## 用法

設定身分屬性，並在 `UserMenu.Items` 下宣告入口。頭像即為觸發器。

`UserMenu` 衍生自 Avalonia 的 `TemplatedControl`。它的觸發器是 Arc `Button`（Ghost，40 px 高的膠囊形），其中包含頭像、線上狀態圓點和姓名。其面板是 Account 變體的 `MenuSurface`，由控制項設定並持有。寬度低於 640 px 時，同一個浮層切換為 Sheet 變體。面板包含由身分屬性產生的標頭、入口列、主題與狀態切換，以及登出列。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:UserMenu DisplayName="Ada Lovelace"
                  Email="ada@analytical.dev"
                  Plan="Pro"
                  IsNameVisible="True"
                  SignOutGestureText="⇧⌘Q">
        <arc:UserMenu.Items>
            <arc:MenuEntry Header="個人資料" />
            <arc:MenuEntry Header="設定" InputGestureText="⌘ ," />
            <arc:MenuEntry Header="帳單" />
        </arc:UserMenu.Items>
    </arc:UserMenu>
</UserControl>
```

### 結構

1. **觸發器**（範本中的 Ghost `arc:Button`）：40 px 高的膠囊，四周內距為 4 px。顯示姓名時，右側內距為 10 px。按下時它從不縮放；按下與開啟只會改變其背景色。
2. **頭像**（`DisplayName`、`AvatarSource`）：32 px 的頭像。設定了 `AvatarSource` 時顯示照片；照片載入期間或載入失敗時，顯示姓名首字母。
3. **線上狀態圓點**（`Status`、`IsStatusVisible`）：位於頭像右下角的 11 px 圓點，外圈有 2 px 的觸發器背景色描邊。「線上」為實心的成功色圓點，「忙碌」為帶橫條的危險色圓點，「離開」為中空的警告色圓點，因此即使不依賴顏色也能分辨狀態。
4. **姓名**（`DisplayName`、`IsNameVisible`）：中等字重，最寬 176 px，超出時以省略號截斷。只有當 `IsNameVisible` 為 `True` 且視窗寬度至少 640 px 時，它才會顯示在頭像旁邊。
5. **箭頭**：16 px，使用淡化文字顏色。它與姓名一同顯示，選單開啟時旋轉 180 度。
6. **面板**（`PanelAlignment`）：帳號面板寬 248 px（15.5 rem），與頭像相距 8 px。預設對齊為 `End`，使面板與觸發器的右邊緣對齊。
7. **身分標頭**：34 px 的頭像（顯示狀態時帶有線上狀態圓點）、姓名及其旁邊的 `Plan` 徽章，以及姓名下方的 `Email`。過長的姓名、方案或電子郵件會以省略號結束，而不會讓面板變寬。
8. **入口**（`Items` 或 `ItemsSource`）：32 px 高的列，文字 13 px，每列可帶可選圖示，末端為可選的快速鍵提示。
9. **主題切換**（`ThemePreference`、`IsThemeSwitchVisible`）：一列「主題」，含三個分段：淺色（太陽圖示）、深色（月亮圖示）和跟隨系統（螢幕圖示）。在面板中高 36 px，在底部面板中高 52 px。
10. **狀態切換**（`Status`、`IsStatusVisible`）：一列「狀態」，含三個分段：線上、忙碌和離開，高度與主題切換相同。
11. **登出列**：LogOut 圖示、「登出」文字，以及 `SignOutGestureText` 提示。登出工作執行時，文字變為「正在登出」，圖示變為載入指示器。
12. **分隔線**：入口、切換項與登出列之間的線條。

### 選擇版面配置

- 在頂列較寬、姓名有助於確認帳號的場合，顯示姓名（`IsNameVisible="True"`）。在較窄的工具列中則隱藏姓名，僅頭像就已足夠。
- 在支援兩種主題的應用程式中，保持 `IsThemeSwitchVisible` 的預設值 `True`。若應用程式只有一種主題，則設為 `False`，使選單不提供無效的選擇。
- 只在團隊成員會設定和查看狀態的應用程式中顯示線上狀態圓點（`IsStatusVisible="True"`）。沒有人設定的圓點會讓每個頭像都多了干擾。
- 當頭像位於視窗左邊緣時使用 `PanelAlignment="Start"`，使面板朝內容方向展開。

### 主題與狀態

`ThemePreference` 可為 `Light`、`Dark` 或 `System`。選單會回報所選的值並保持開啟。主題由應用程式自行套用。`IsThemeSwitchVisible="False"` 會隱藏主題切換。

<Demo name="Presence" />

```xml
<arc:UserMenu DisplayName="Grace Hopper"
              Email="grace@compiler.dev"
              ThemePreference="{Binding ThemePreference, Mode=TwoWay}"
              Status="{Binding Status, Mode=TwoWay}"
              IsStatusVisible="True" />
```

```csharp
userMenu.ThemePreferenceChanged += (_, e) => ApplyTheme(e.ThemePreference);
userMenu.StatusChanged += (_, e) => status.Text = $"狀態：{e.Status}";
```

`Status` 與 `ThemePreference` 為雙向繫結，因此檢視模型始終持有目前的選擇。這些事件只在使用者於選單中作出選擇時觸發。使用它們來套用主題或把狀態傳送到伺服器，因為選單本身兩者都不做。

### 非同步登出

選取登出列時會執行 `SignOutAction`。工作執行期間，該列顯示載入指示器，選單保持開啟。工作完成後選單才會關閉。

```csharp
userMenu.SignOutAction = () => auth.SignOutAsync();
userMenu.SignOutRequested += (_, _) => status.Text = "正在登出…";
```

`SignOutRequested` 在動作執行之前觸發，因此適合在此顯示狀態文字或清除本機快取。如果動作擲出例外，選單仍會停止等待並關閉；失敗資訊應由動作自行回報，因為選單不會顯示它。未設定動作，或工作已經完成時，選單會立即關閉，並把焦點交還頭像。

### 照片與資料驅動的入口

`AvatarSource` 用於顯示照片。未設定照片，或照片仍在載入時，顯示姓名首字母。當 `Items` 不夠用時，`ItemsSource` 可根據資料產生入口。

```xml
<arc:UserMenu DisplayName="{Binding Name}"
              Email="{Binding Email}"
              AvatarSource="{Binding Avatar}"
              ItemsSource="{Binding AccountLinks}" />
```

每個產生的列顯示該項目；若設定了 `ItemTemplate`，則顯示範本的結果。`ItemClicked` 透過 `e.Item` 回報所在列及其資料項目，因此一個處理常式即可把每個入口導覽到對應的頁面。

### 繫結到檢視模型

檢視模型持有身分、主題、狀態與登出邏輯。選單負責顯示它們，並透過雙向屬性與事件回報。把帳號入口作為資料保存在檢視模型中，使選單與導覽保持一致。

```csharp
public partial class AccountViewModel : ObservableObject
{
    private readonly IAuthService _auth;

    public AccountViewModel(IAuthService auth) => _auth = auth;

    [ObservableProperty]
    private string _displayName = "Ada Lovelace";

    [ObservableProperty]
    private string _email = "ada@analytical.dev";

    [ObservableProperty]
    private UserMenuThemePreference _themePreference = UserMenuThemePreference.System;

    [ObservableProperty]
    private UserMenuStatus _status = UserMenuStatus.Available;

    [RelayCommand]
    private Task SignOutAsync() => _auth.SignOutAsync();
}
```

```xml
<arc:UserMenu DisplayName="{Binding DisplayName}"
              Email="{Binding Email}"
              IsNameVisible="True"
              ThemePreference="{Binding ThemePreference, Mode=TwoWay}"
              IsStatusVisible="True"
              Status="{Binding Status, Mode=TwoWay}" />
```

```csharp
userMenu.SignOutAction = () => viewModel.SignOutCommand.ExecuteAsync(null);
```

- `ThemePreference` 與 `Status` 為雙向繫結，因此在選單中選取的主題會傳到檢視模型，而在程式碼中設定的狀態會反映到圓點與切換項上。
- `DisplayName` 與 `Email` 為單向繫結：資料更新後檢視模型修改它們，標頭隨之更新。
- 使用 `ThemePreferenceChanged` 套用主題，使用 `StatusChanged` 儲存狀態。雙向值只記錄選擇；應用程式真正據此執行動作的地方是事件。
- 登出命令是一個 `Func<Task>`，因此應在程式碼中指派，或把命令包裝在回傳 `Task` 的方法中。

### 焦點與關閉

指標開啟選單時，焦點位於面板上，且不設反白列。鍵盤開啟時，按 Enter、Space 或 ArrowDown 會反白第一列，按 ArrowUp 則反白最後一列。頁面載入時若以 `IsOpen` 設定為啟動即開啟的選單，不會取得焦點，也不會捲動頁面。

選取某個入口會關閉選單。主題與狀態切換會讓選單保持開啟，使使用者可以在一次造訪中切換兩者。登出列會讓選單保持開啟並顯示載入指示器，直到工作結束後再關閉它。外部按下會關閉選單；在底部面板中，按下遮罩同樣會關閉。

視窗跨過 640 px 斷點時，若選單正處於開啟狀態，選單會直接關閉，而不是把面板轉換為底部面板，使用者需要在新的形式下重新開啟它。Escape 和 Tab 會關閉選單，並把焦點交還頭像。

### 範例：營運主控台頂列

事件應變主控台的頂列顯示值班工程師的帳號。主控台是寬視窗，因此顯示姓名；線上狀態圓點顯示「忙碌」，讓同事知道工程師正在處理事件。方案徽章顯示「管理員」，選單包含三個入口，登出位於最後並帶有快速鍵提示。主題切換保持預設值，因為應變人員常在暗色環境中工作，需要無需進入設定頁即可切換。

```xml
<Border Padding="16,8"
        CornerRadius="18"
        BorderThickness="1"
        Background="{arc:ArcDesignTokenResource Surface}"
        BorderBrush="{arc:ArcDesignTokenResource Border}">
    <DockPanel>
        <TextBlock DockPanel.Dock="Left"
                   Text="事件主控台"
                   FontSize="15"
                   FontWeight="Medium"
                   VerticalAlignment="Center"
                   Foreground="{arc:ArcDesignTokenResource Foreground}" />
        <arc:UserMenu DockPanel.Dock="Right"
                      DisplayName="Grace Hopper"
                      Email="grace@compiler.dev"
                      Plan="管理員"
                      IsNameVisible="True"
                      IsStatusVisible="True"
                      Status="Busy"
                      SignOutGestureText="⇧⌘Q">
            <arc:MenuEntry Header="個人資料" Icon="{x:Static arc:ArcIcons.User}" />
            <arc:MenuEntry Header="值班排程" Icon="{x:Static arc:ArcIcons.Clock}" />
            <arc:MenuEntry Header="帳單" Icon="{x:Static arc:ArcIcons.CreditCard}" />
        </arc:UserMenu>
    </DockPanel>
</Border>
```

### 文案規範

選單簡短，一眼即可讀完。入口是它所開啟頁面的名詞，切換項與登出列則使用應用程式其他地方通用的用詞。

- 入口以其頁面命名，例如「帳單」「工作區設定」。保持在約 24 個字元以內。
- 登出列使用「登出」，由動詞及其補語構成，標籤中不加驚嘆號，也不加確認文字。
- 方案徽章保持一個詞，例如「Pro」或「管理員」；它是徽章，不是句子。
- 三種狀態按原樣使用。選單提供「線上」「忙碌」和「離開」；「離線」不是其中之一。
- 主題選項寫成單一詞：淺色、深色、跟隨系統。

| 不要寫 | 改為 |
|---|---|
| 登出系統 | 登出 |
| 我的帳號設定 | 個人資料 |
| 暗黑模式 | 深色 |
| 離線 | 離開 |
| 立即登出！ | 登出 |

### 最佳做法

**建議**

- 入口保持在兩到四個。第五個入口應放在頁面中，並從其中一個入口連結過去。
- 登出始終放在最後一列。版面配置本身已將它與其他列分開，不要在它下面再加列。
- 只在團隊成員會設定和查看狀態的應用程式中顯示線上狀態圓點。沒有人設定的圓點會讓每個頭像都多了干擾。
- 在 `ThemePreferenceChanged` 中套用主題，使選擇在選單仍然開啟時即刻生效。
- 讓 `Email` 與 `Plan` 使用合適長度的值。過長的值會被截斷，因此簡短的姓名與方案更易讀。

**避免**

- 在選單中放置長篇的設定表單。表單應放在 `Drawer` 或頁面中；選單只用於選擇與導覽。
- 放置很長的帳號或工作區清單。切換工作區應使用 `Select` 或頁面，因為選單只能舒適地容納少數幾列。
- 圍繞選單中的某個設定來設計頁面。斷點固定為 640 px，因此頁面要同時為面板與底部面板而設計。
- 執行登出作業卻不給予回饋。未完成的工作會顯示載入指示器；瞬間完成的工作則直接關閉選單，不顯示指示器。
- 在選單中加入自訂切換項。API 只提供主題與狀態切換；其他選擇應放在 `DropdownMenu` 或頁面中。

### 常見問題

**為什麼我的窄視窗上不顯示姓名？** 寬度低於 640 px 時，觸發器只顯示頭像，選單以底部面板的形式開啟。`CompactBreakpoint` 是固定為 640 的常數，因此應為窄視窗中只顯示頭像的觸發器規劃版面配置。

**為什麼線上狀態圓點不見了？** `IsStatusVisible` 預設為 `False`。將其設為 `True` 即可顯示圓點與狀態切換。

**如何隱藏主題選擇？** 設定 `IsThemeSwitchVisible="False"`。主題列隨即從面板中移除，應用程式可在其他位置提供自己的主題控制項。

**登出的載入指示器如何出現？** 從 `SignOutAction` 回傳一個未完成的 `Task`。該列會顯示載入指示器和「正在登出」文字，直到工作完成，然後選單關閉。

**能否在窄視窗中顯示姓名與箭頭？** 不能。姓名與箭頭只在寬度至少 640 px 的視窗中顯示，底部面板只保留頭像。應圍繞這一點規劃頁面版面配置，因為選單沒有將斷點作為可設定項公開。

## API 參考

<!-- api: UserMenu -->

<!-- api: MenuEntry -->

選單回報的兩種選擇類型：

| 類型 | 值 | 意義 |
|---|---|---|
| `UserMenuThemePreference` | `Light`、`Dark`、`System` | 使用者所選的主題；預設為 `System` |
| `UserMenuStatus` | `Available`、`Busy`、`Away` | 圓點與狀態切換所顯示的線上狀態；預設為 `Available` |

## 無障礙

- 頭像觸發器會以「帳號選單」加顯示名稱的方式被播報。顯示狀態時，狀態會附加在名稱之後。
- 各列具有選單項目角色。主題與狀態切換分別是有名稱的單選群組，每個選項都可被選取。
- 方向鍵可在主題或狀態切換中選擇，Enter 或空白鍵啟動某一列。
- Escape 或 Tab 會關閉選單，並將焦點交還頭像。
- 線上狀態圓點在顏色之外還會改變形狀，因此不只依賴顏色。
- 登出工作執行期間，登出列會回報自身處於忙碌狀態。
- 為帳號入口提供說明其去向的 `Header`，例如「帳單」或「工作區設定」。螢幕閱讀器透過各列的標題來朗讀它們。

### 鍵盤

| 按鍵 | 操作 |
|---|---|
| 頭像上的 `Enter`、`Space` 或 `ArrowDown` | 開啟選單，並反白第一列 |
| 頭像上的 `ArrowUp` | 開啟選單，並反白最後一列 |
| 在頭像上點選或輕觸 | 切換選單 |
| `ArrowDown` / `ArrowUp` | 將反白移到下一列或上一列；清單循環，並略過停用的列 |
| `Home` / `End` | 將反白移到第一列或最後一列 |
| 字母鍵 | 輸入首字母，移動到文字以所輸字母開頭的列，例如 `S` 定位到「登出」 |
| 切換項上的 `ArrowLeft` / `ArrowRight` | 選擇上一個或下一個選項，到頭後回繞；選單保持開啟 |
| `Enter` / `Space` | 啟用某個入口或登出列；在切換列上無作用 |
| `Escape` / `Tab` | 關閉選單，並將焦點交還頭像 |

## 動態效果

- 在寬視窗中，面板從頭像處展開，沒有回彈過衝；各列依序淡入。
- 一個反白條在各列之間滑動，主題與狀態的滑塊滑到所選選項上。
- 寬度低於 640px 時，選單以底部面板的形式開啟，從底邊升起。它會跟隨向下拖曳，拖得夠遠時關閉。
- 減少動態效果時，面板與切換項使用短暫的淡入，反白直接跳轉，並停用拖曳。關閉動態效果時選單立即顯示。

| 變化 | 完整 | 減少 | 關閉 |
|---|---|---|---|
| 寬面板開啟 | 從頭像處以平滑彈簧從 0.94 展開，無過衝，隨後各列以 16 ms 間隔淡入 | 短暫淡入 | 立即 |
| 寬面板關閉 | 在 120 ms 內縮放至 0.97 並淡出 | 短暫淡入 | 立即 |
| 反白移動 | 指標與鍵盤操作均在各列之間滑動 | 直接跳轉 | 立即 |
| 切換滑塊移動 | 以 580 ms 的彈簧滑到所選選項 | 短暫淡入 | 立即 |
| 底部面板開啟（低於 640 px） | 從底邊以平滑彈簧升起；各列在 60 ms 後以 20 ms 間隔進入 | 短暫淡入 | 立即 |
| 底部面板拖曳 | 跟隨向下拖曳；超過 80 px 或快速輕掃時關閉 | 停用拖曳 | 立即 |
| 箭頭 | 隨選單開啟以彈簧旋轉 180 度 | 立即轉向 | 立即 |

## 相關元件

- `DropdownMenu`：帶標籤觸發器之後的一般命令清單。
- `ThemeSwitcher`：獨立的淺色與深色切換，不屬於帳號選單。
- `Avatar`：觸發器所顯示的圖片，也可在身分標頭中重複使用。
- `MenuSurface`：面板與底部面板所依據的共用選單浮層。

| 元件 | 適用情境 |
|---|---|
| `DropdownMenu` | 命令與帳號無關，需要帶標籤的觸發器 |
| `ThemeSwitcher` | 淺色與深色切換單獨存在，不屬於帳號 |
| `Avatar` | 在清單或標頭中以圖片或首字母標識一個人 |
| `ContextMenu` | 操作屬於單一物件，並在指標處開啟 |
| `Drawer` | 帳號設定需要帶表單的完整側邊面板，而非簡短選單 |
| `Dialog` | 登出或變更帳號前需要先確認 |
