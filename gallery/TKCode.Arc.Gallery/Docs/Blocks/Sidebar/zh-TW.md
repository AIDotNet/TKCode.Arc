---
title: 側邊欄
summary: 工作區導覽，包含群組、巢狀頁面、數量與滑動的選取標記；可收合為圖示窄欄。
source-hash: 8399d3f503f7
---

<Demo name="Preview" />

## 何時使用

- 頂層頁面超過幾個的應用程式主導覽，例如工作區、儀表板或管理工具。
- 頁面分屬不同群組，並帶有一層子頁面（專案、報表、設定區域）。
- 導覽需要一直顯示在畫面上，但需要時可以少佔空間：側邊欄會收合為 64 像素的圖示窄欄。

## 何時不用

- 同一物件的兩到五個檢視：使用 `Tabs`。
- 序列中的步驟，或階層中的路徑：使用 `Stepper` 或 `Breadcrumb`。
- 深層的資料夾與檔案樹：使用 `TreeView`。側邊欄只顯示一層巢狀頁面。
- 動作而非位置：使用 `DropdownMenu` 或 `CommandPalette`。

## 用法

在側邊欄中放入 `SidebarSection` 元素，並在群組中放入 `SidebarItem` 元素。沒有 `Header` 的群組不顯示標題，適合作為第一組（首頁、收件匣）。為每個頁面設定 `Key`，並將 `SelectedKey` 繫結到你的路由。側邊欄的寬度由主題決定（展開時為 260，窄欄時為 64），因此請將它放在 `DockPanel` 或 `Auto` 格線欄中，不要設定 `Width`。

```xml
<DockPanel xmlns="https://github.com/avaloniaui"
           xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
           xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc"
           Height="420">
    <arc:Sidebar DockPanel.Dock="Left" SelectedKey="home" IsFilterVisible="True">
        <arc:SidebarSection>
            <arc:SidebarItem Key="home" Label="首頁" Icon="{x:Static arc:ArcIcons.House}" />
            <arc:SidebarItem Key="inbox" Label="收件匣" Icon="{x:Static arc:ArcIcons.Inbox}" Badge="4" />
        </arc:SidebarSection>
        <arc:SidebarSection Header="工作區">
            <arc:SidebarItem Key="projects" Label="專案" Icon="{x:Static arc:ArcIcons.Folder}" IsExpanded="True">
                <arc:SidebarItem Key="projects/atlas" Label="Atlas" />
                <arc:SidebarItem Key="projects/beacon" Label="Beacon" />
            </arc:SidebarItem>
        </arc:SidebarSection>
    </arc:Sidebar>
    <TextBlock Margin="24" Text="頁面內容" />
</DockPanel>
```

### 群組與巢狀頁面

帶有子項目的項目是父層級，父層級永遠不會被選取：選取它會展開或收合其下的頁面，這些頁面垂掛在一條細引導線上。`IsExpanded` 是雙向的。若要讓使用者收合整個群組，請在有 `Header` 的群組上設定 `IsCollapsible`；該群組的 `IsExpanded` 同樣是雙向的。只顯示一層巢狀。

`Badge` 會在列尾顯示一個簡短的數量。`IsEnabled="False"` 會讓頁面變淡，方向鍵會略過它。`Icon` 接受 24×24 的 Lucide 圖形（例如 `ArcIcons` 中的字形），也可以是任何控制項。

### 選取與路由

`SelectedItem` 與 `SelectedKey` 是雙向的，並保持同步。在頁面出現之前設定 `SelectedKey` 也沒有問題：頁面出現後，側邊欄會選取它。每次選取頁面時都會引發 `ItemInvoked`，即使該頁面已被選取；`SelectCommand` 會以該項目作為參數執行。每個項目還可以各自帶有 `Command` 與 `Tag`。

<Demo name="DataBinding" />

```csharp
sidebar.ItemInvoked += (_, e) => router.Navigate(e.Item.Key!);
```

### 圖示窄欄

`IsCollapsed` 會將側邊欄收合為 64 像素的窄欄，它是雙向的。頭部區域中的收合按鈕、`ToggleCollapsed()` 方法，以及焦點位於側邊欄時的 Ctrl+B（macOS 上為 ⌘B），都會執行相同的操作。在窄欄上，每個頁面右側都會顯示工具提示，數量會變成圖示上的圓點，群組標題變為細線，篩選欄位變為一個搜尋按鈕，點擊後會開啟側邊欄；父層級則會在選單中開啟其頁面。沒有圖示的頁面會顯示其第一個字母。

頭部與底部會保留側邊欄展開時的配置，並裁切至窄欄寬度，因此位於起始邊緣的標誌或大頭貼仍會顯示。如預覽所示，使用 `:collapsed` 偽類淡出其餘內容。收合按鈕可見時，窄欄上的頭部會讓位給它。

<Demo name="Rail" />

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <UserControl.Styles>
        <Style Selector="arc|Sidebar:collapsed TextBlock.rail-fade">
            <Setter Property="Opacity" Value="0" />
        </Style>
    </UserControl.Styles>
    <arc:Sidebar IsCollapsed="True" Height="320" />
</UserControl>
```

### 篩選

`IsFilterVisible` 會在頭部下方顯示篩選欄位。名稱包含該文字的頁面會保留，含有符合頁面的父層級會展開，沒有符合項的群組會隱藏。按 Escape 清除篩選欄位，按 ↓ 移到第一個頁面，按 Enter 選取它。`FilterText` 是雙向的，`NoMatchesText` 用來取代預設的“No matches”。

### 即時資料

側邊欄會跟隨其資料變化：修改項目上的 `Badge`、`Label`、`Icon` 或 `IsEnabled`，或新增與移除項目及群組，列都會原地更新。

<Demo name="LiveUpdates" />

### 鍵盤

頁面清單只佔一個 Tab 停駐點，焦點從選取的頁面開始。

| 按鍵 | 操作 |
|---|---|
| ↑ / ↓ | 移到上一個或下一個可見列 |
| Home / End | 移到第一列或最後一列 |
| → | 展開父層級；若已展開，則移入其頁面。在窄欄上，開啟其選單 |
| ← | 收合父層級；若位於頁面上，則移到其父層級或群組標題 |
| Enter / Space | 選取頁面，或展開、收合父層級或群組 |
| 字母鍵 | 跳到下一個以所輸入文字開頭的頁面 |
| Ctrl+B / ⌘B | 收合或展開側邊欄 |

## API 參考

<!-- api: Sidebar -->

<!-- api: SidebarSection -->

<!-- api: SidebarItem -->

## 無障礙

- 側邊欄是導覽地標，名稱由 `Label` 提供（預設為“Sidebar”）。
- 頁面構成一棵樹。每一列的無障礙名稱由其名稱與數量組成（例如“收件匣，12”），並回報是否被選取；父層級還會回報是否已展開。
- 可收合的群組標題是展開按鈕，其他標題只是文字。
- 收合按鈕與窄欄上的搜尋按鈕，其無障礙名稱分別由 `CollapseButtonText`、`ExpandButtonText` 與 `FilterPlaceholder` 提供。覆寫它們即可在地化。
- 鍵盤焦點會顯示 2 像素的焦點環。已停用的頁面會變淡，並會被略過。

## 動態效果

- 選取標記是一個帶有強調色條的柔和區塊，它以俐落的彈簧在各列之間滑動。當選取的頁面收合在父層級內時，標記會停留在父層級上。
- 父層級與可收合的群組會以平滑的高度彈簧展開或收合，箭頭同時旋轉。
- 收合為窄欄時，寬度以平滑彈簧過渡。名稱、數量與標題會快速淡出，圖示保持原位，數量則變為圓點。
- 動態效果等級為 Reduced 時，寬度與高度會立即變化，選取標記也會立即移動。文字仍會淡入淡出。
- 動態效果等級為 Off 時，所有變化會立即套用。

## 相關元件

- `Drawer`：用於在窄視窗中承載側邊欄，固定的側邊欄在這種情況下會佔用過多空間。
- `Tabs`：在同一頁面的檢視之間切換。
- `TreeView`：用於深層的資料夾樹。
- `CommandPalette`：用於透過鍵盤搜尋頁面與操作。
- `UserMenu` 或 `DropdownMenu`：放在頭部與底部，用於工作區切換或帳戶選單。
