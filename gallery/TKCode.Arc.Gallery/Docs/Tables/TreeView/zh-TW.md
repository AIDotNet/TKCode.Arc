---
title: 樹狀檢視
summary: 瀏覽巢狀的資料夾與結構化內容。
source-hash: a4b719513550
---

<Demo name="FileTree" />

## 何時使用

`TreeView` 用於顯示使用者可以展開、收合和選取的巢狀紀錄：資料夾與檔案、會計科目表、分類階層。它位於頁面的導覽區域，樹中的選取通常會驅動旁邊的內容，例如預覽、表單或程式碼檢視器。列由扁平的虛擬化清單繪製，因此大型階層結構依然保持流暢。

- **開發者工具中的儲存庫與檔案瀏覽器**：資料夾與檔案可以展開和收合，選取的檔案顯示在樹旁邊的 `CodeBlock` 中。
- **內部後台中的文件與知識庫導覽**：章節可巢狀多層，選取某個頁面後會在編輯區載入它。
- **財務應用程式中的會計科目與成本中心**：父科目展開後顯示子科目，選取的科目驅動分類帳檢視。
- **人事或管理後台中的組織與團隊結構**：部門包含團隊，團隊包含成員，選取成員後開啟其個人資料。
- **包含數千個檔案的資料夾的媒體與素材庫**：只有可視範圍附近的列會被建立，因此樹仍然可以流暢使用。

## 何時不用

- **幾段需要從上到下閱讀的可收合內容**：用 `Accordion`。
- **頁面階層中的目前位置**：用 `Breadcrumb`，它顯示路徑並可返回上層。
- **一份 JSON 文件的鍵與值**：用 `JsonViewer`，它在鍵旁顯示值，並支援搜尋和複製。
- **使用者會重新排序的扁平清單**：用 `ReorderableList`。樹的同層節點之間沒有拖曳排序。
- **使用者依欄位比較、欄位相同的紀錄**：用 `Table`。樹沒有可以排序或比較的欄位。

## 用法

節點可以直接在 XAML 中內嵌宣告。帶有子項目的 `TreeViewItem` 即為資料夾。

```xml
<arc:TreeView MinWidth="260" ShowIndentGuides="True">
    <arc:TreeView.Items>
        <arc:TreeViewItem Header="src" IsExpanded="True">
            <arc:TreeViewItem.Items>
                <arc:TreeViewItem Header="components">
                    <arc:TreeViewItem.Items>
                        <arc:TreeViewItem Header="button.tsx" />
                        <arc:TreeViewItem Header="tree-view.tsx" />
                    </arc:TreeViewItem.Items>
                </arc:TreeViewItem>
                <arc:TreeViewItem Header="lib" />
            </arc:TreeViewItem.Items>
        </arc:TreeViewItem>
        <arc:TreeViewItem Header="README.md" />
    </arc:TreeView.Items>
</arc:TreeView>
```

`TreeView` 衍生自 Avalonia 的 `TemplatedControl`。它不使用 Avalonia 的巢狀項目容器：它保留一個扁平的池化列清單，因此展開資料夾只需為其顯示的列付出成本，樹的其餘部分不會重新配置版面。內嵌 `Items` 適合你在 XAML 中撰寫的固定導覽樹。對於來自服務或資料庫的資料，請繫結 `ItemsSource`；兩者同時設定時，`ItemsSource` 優先。

### 結構

1. **外框**：表面背景，1 px 邊框（邊框色），16 px 圓角，內距為 8、6、8、8 px。將 `HorizontalAlignment` 設為 `Stretch`（預設值）時，樹會填滿其寬度。
2. **列**：高 36 px，下方 2 px 間距，因此每列占用 38 px 的空間。`RowHeight` 統一改變每一列的高度。列的圓角為 10 px，左右內距為 10 px。
3. **縮排**：每層 18 px。設定 `ShowIndentGuides` 後，一條使用邊框色的短分支線會把巢狀列連接到其上層。
4. **箭頭**：資料夾上的箭頭，資料夾展開時箭頭會轉動。
5. **圖示**（`ShowIcons`、`IconSelector`、`TreeViewItem.Icon`）：標籤前 16 px 的位置。資料夾顯示展開狀態的資料夾圖示；檔案顯示檔案圖示。`Icon` 與 `IconSelector` 接受 24 × 24 的圖形，例如 `ArcIcons` 的成員。`ShowIcons` 為 false 時，標籤從縮排處開始。
6. **標籤**（`Header`、`HeaderSelector`、`ItemTemplate`）：列文字，使用次要文字顏色。目前啟用的列使用前景色，停用的列使用弱化文字顏色。`ItemTemplate` 以你自己的內容取代這段文字。
7. **列填色**：懸停填色與選取填色，均使用淡化的表面色。選取填色會從一列平滑滑向另一列。
8. **檔案色調**：`.ts` 與 `.tsx` 檔案，以及 `.css`、`.md`、`.json` 檔案，帶有檔案類型色調。其他檔案使用淡化的文字顏色。
9. **焦點框**：由控制項繪製，僅用於鍵盤焦點。

### 選擇內嵌項目或資料來源

| 來源 | 適用情境 | 子項目 |
|---|---|---|
| 內嵌 `Items` | 樹是固定的導覽，規模較小，並在 XAML 中撰寫 | 巢狀的 `TreeViewItem` 元素 |
| `ItemsSource` | 節點來自資料，並在執行階段變化 | `ChildrenSelector` 回傳每個節點的子項目 |

### 使用子項目選擇器繫結資料

繫結 `ItemsSource`，並在程式碼中設定選擇器。`ChildrenSelector` 會回傳節點的子項目，帶有子項目的節點即為資料夾。

<Demo name="DataWithChildrenSelector" />

```csharp
var tree = new TreeView
{
    ItemsSource = nodes,
    ChildrenSelector = item => ((FileNode)item).Children,
    HeaderSelector = item => ((FileNode)item).Name,
    Width = 300
};
tree.SelectionChanged += (_, e) => selected.Text = $"已選取項目：{e.NewItem}";
tree.SelectedItem = nodes[0].Children![2];   // 會展開該節點的上層節點
```

`SelectedItem` 為雙向繫結，因此可以繫結到檢視模型：

```xml
<arc:TreeView ItemsSource="{Binding Files}"
              SelectedItem="{Binding SelectedFile, Mode=TwoWay}" />
```

### 繫結到檢視模型

在 MVVM 應用程式中，檢視模型擁有節點資料與選取狀態，樹只負責呈現它們。範例使用 CommunityToolkit.Mvvm。節點是一般的紀錄，檢視模型負責載入樹並保存選取的節點。

```csharp
public sealed record FileNode(string Name, IReadOnlyList<FileNode>? Children = null)
{
    public override string ToString() => Name;
}

public interface IRepositoryService
{
    Task<IReadOnlyList<FileNode>> GetTreeAsync();
}

public partial class WorkspaceViewModel : ObservableObject
{
    private readonly IRepositoryService _repository;

    public WorkspaceViewModel(IRepositoryService repository) => _repository = repository;

    [ObservableProperty]
    private ObservableCollection<FileNode> _files = [];

    [ObservableProperty]
    private FileNode? _selectedFile;

    [RelayCommand]
    private async Task LoadAsync() => Files = new ObservableCollection<FileNode>(await _repository.GetTreeAsync());
}
```

```xml
<arc:TreeView ItemsSource="{Binding Files}"
              SelectedItem="{Binding SelectedFile, Mode=TwoWay}"
              ShowIcons="True" />
```

選擇器是函式，XAML 無法表達，因此請在檢視的程式碼後置檔中設定一次：

```csharp
Tree.ChildrenSelector = item => ((FileNode)item).Children;
Tree.HeaderSelector = item => ((FileNode)item).Name;
```

- `SelectedFile` 為雙向繫結。從檢視模型設定它會選取該節點並展開其上層節點，因此指向某個檔案的深層連結會顯示該檔案。
- 重新整理後請使用相同的節點實例，否則選取狀態與展開的資料夾將與列不再一致。當服務回傳新物件時，依路徑找到選取的節點，並指派為新的實例。
- 編譯繫結需要在檢視上設定 `x:DataType`（範例為求簡潔省略了它）。

### 圖示

`ShowIcons` 控制整棵樹是否顯示圖示，`IconSelector` 為每個資料節點提供圖示。`TreeViewItem` 則使用它自己的 `Icon`。兩者都接受 24 × 24 的描邊幾何圖形，`ArcIcons` 的成員通常是其來源。

```csharp
tree.ShowIcons = true;
tree.IconSelector = item => ((FileNode)item).Children is null ? ArcIcons.File : ArcIcons.Folder;
```

### 豐富內容列

當一列需要不只一個標籤時設定 `ItemTemplate`：名稱加型別、列尾的狀態點、工具提示。範本填滿箭頭和圖示之後的標籤區。列高、填色、縮排、參考線、箭頭和圖示仍由樹繪製，所以展開、選取和虛擬化照常運作。列是池化的：範本為每個池化的列只建立一次，捲動時只更換它的資料，所以請繫結到節點，而不是只讀取一次。

<Demo name="RichRows" />

```xml
<arc:TreeView x:Name="Tree" Width="320">
    <arc:TreeView.ItemTemplate>
        <DataTemplate>
            <DockPanel Background="Transparent" ToolTip.Tip="{Binding Path}">
                <Ellipse DockPanel.Dock="Right" Width="6" Height="6"
                         Fill="{arc:ArcDesignTokenResource Success}"
                         IsVisible="{Binding IsIndexed}" />
                <DockPanel VerticalAlignment="Center">
                    <TextBlock DockPanel.Dock="Left" Text="{Binding Name}" />
                    <TextBlock DockPanel.Dock="Left" Margin="4,0,0,0" Text=":"
                               Foreground="{arc:ArcDesignTokenResource TextMuted}"
                               IsVisible="{Binding IsColumn}" />
                    <TextBlock Margin="4,0,0,0" Text="{Binding Detail}" TextTrimming="CharacterEllipsis"
                               Foreground="{arc:ArcDesignTokenResource TextMuted}" />
                </DockPanel>
            </DockPanel>
        </DataTemplate>
    </arc:TreeView.ItemTemplate>
</arc:TreeView>
```

```csharp
Tree.ItemsSource = tables;
Tree.ChildrenSelector = item => ((SchemaNode)item).Columns;
Tree.HeaderSelector = item => ((SchemaNode)item).Name;          // type-ahead and screen readers
Tree.DisabledSelector = item => ((SchemaNode)item).IsGenerated;  // shown, not selectable
```

- 範本中的文字使用該列的標籤顏色：平時為次要文字顏色，滑鼠停留和選取的列為前景色，停用的列為弱化文字顏色。需要保持弱化的部分（例如型別）請自行設定 `Foreground`。
- 在預設列高下，內容請保持一行。需要標題加副標題時，調高 `RowHeight`，例如設為 56。所有列等高，所以樹仍然是虛擬化的。
- 鍵入跳轉和列的無障礙名稱使用 `HeaderSelector`，未設定時使用 `ToString()`。即使範本已經顯示了文字，也請設定它。
- 帶 `DataType` 的範本只用於相符的節點。其他節點使用樹或其祖系的 `DataTemplates`，所以資料夾和葉節點可以顯示得不同。
- 內容中的按鈕和文字方塊可以正常使用。Enter 和 Space 交給擁有焦點的控制項，方向鍵仍在列之間移動，在文字方塊中輸入的按鍵留在文字方塊中。只有取得焦點的列，其內容才在 Tab 順序中。

### 停用的節點

`DisabledSelector` 標記顯示出來但不能選取的資料項，例如自動產生的欄位，或在你的應用程式中目前使用者無權開啟的資料夾。內嵌節點使用 `TreeViewItem.IsDisabled`。停用的列會變暗，沒有滑鼠停留填色。點選、Enter、Space 或設定新的 `SelectedItem` 都不會選取它，也不會觸發 `ItemInvoked`。鍵盤焦點仍可到達它，所以停用的資料夾可以用方向鍵或點選展開，其子項仍然可以存取。

```xml
<arc:TreeView Width="280">
    <arc:TreeViewItem Header="reports" IsExpanded="True">
        <arc:TreeViewItem Header="2025-summary.md" />
        <arc:TreeViewItem Header="archive" IsDisabled="True">
            <arc:TreeViewItem Header="2023-summary.md" />
        </arc:TreeViewItem>
    </arc:TreeViewItem>
</arc:TreeView>
```

### 從程式碼展開與收合

```csharp
tree.ExpandAll();
tree.CollapseAll();
tree.ToggleExpanded(folderNode);
tree.ScrollIntoView(fileNode);
tree.FocusItem(fileNode);
```

`ScrollIntoView` 與 `FocusItem` 會先展開節點的上層節點，使使用者尚未看到的節點變得可見。`IsItemExpanded` 回報資料夾是否已展開。對於內嵌的 `TreeViewItem`，`IsExpanded` 為雙向繫結，樹會保持它同步。

### 事件

`ItemInvoked` 在一列被點選或按下 Enter 或 Space 啟用時觸發，包括已被選取的列。`SelectionChanged` 回報選取節點的變更。`ItemExpanded` 與 `ItemCollapsed` 回報展開與收合的資料夾。啟用資料夾會選取它並切換其展開狀態，因此透過點選展開的資料夾同樣會觸發 `ItemExpanded`。

```csharp
tree.ItemInvoked += (_, e) => OpenFile(e.Item);
tree.ItemExpanded += (_, e) => log.Text = $"已開啟 {e.Item}";
```

### 數千個節點

這棵樹有 40 個資料夾，每個資料夾含 250 個檔案。列會被虛擬化並池化，只有可視範圍附近的列會被實例化，因此展開一個資料夾只需付出約一個畫面列數的成本，樹的其餘部分不會被建構。

<Demo name="ThousandsOfNodes" />

### 範例：文件儲存庫瀏覽器

文件團隊瀏覽其元件文件的儲存庫。樹位於左側窗格，右側窗格中的 `CodeBlock` 顯示選取的檔案，如頁面頂部的檔案樹示範所示。預設展開兩個資料夾，使讀者一眼看出結構，而團隊很少編輯的資料夾保持收合。檔案色調無需額外的欄位，即可顯示哪些檔案是程式碼、樣式、文件與設定。

![帶有兩個展開資料夾和多種類型檔案的文件儲存庫樹](images/DocsRepository.png)

```xml
<arc:TreeView Width="320"
              HorizontalAlignment="Left"
              ShowIndentGuides="True"
              ShowIcons="True">
    <arc:TreeViewItem Header="acme-docs" IsExpanded="True">
        <arc:TreeViewItem.Items>
            <arc:TreeViewItem Header="guides" IsExpanded="True">
                <arc:TreeViewItem.Items>
                    <arc:TreeViewItem Header="getting-started.md" />
                    <arc:TreeViewItem Header="theming.md" />
                </arc:TreeViewItem.Items>
            </arc:TreeViewItem>
            <arc:TreeViewItem Header="api" IsExpanded="True">
                <arc:TreeViewItem.Items>
                    <arc:TreeViewItem Header="tree-view.tsx" />
                    <arc:TreeViewItem Header="tree-view.module.css" />
                    <arc:TreeViewItem Header="motion-tokens.ts" />
                </arc:TreeViewItem.Items>
            </arc:TreeViewItem>
            <arc:TreeViewItem Header="examples">
                <arc:TreeViewItem.Items>
                    <arc:TreeViewItem Header="basic-tree.tsx" />
                </arc:TreeViewItem.Items>
            </arc:TreeViewItem>
            <arc:TreeViewItem Header="package.json" />
            <arc:TreeViewItem Header="README.md" />
        </arc:TreeViewItem.Items>
    </arc:TreeViewItem>
</arc:TreeView>
```

### 內容規範

節點標籤是使用者會搜尋的名稱，因此應依來源資料中儲存的原樣顯示。

| 不要寫 | 改為 |
|---|---|
| 未命名的資料夾 | 資料夾的真實名稱；建立過程中顯示「新增資料夾」 |
| 文件（42 個檔案） | 文件；數量顯示在頁面的其他位置 |
| 點選以開啟檔案 | 無文字；列本身就是控制項 |
| 檔案 | 專案檔案，或儲存庫的名稱 |
| 項目 1 | 檔名，例如 theming.md |

- 標籤保持為儲存的名稱，包括大小寫與副檔名，使使用者能夠與儲存庫中的名稱對應。
- 樹的無障礙名稱應具體，例如「儲存庫檔案」或「會計科目表」，而不是「樹」。

### 最佳做法

**建議**

- 預設展開前一到兩層，使使用者無需尋找即可看到結構。
- 層級超過兩層的樹使用 `ShowIndentGuides`，使每一列的上層易於辨認。
- 重新整理時保持相同的節點實例，使選取狀態和展開的資料夾與列保持一致。
- 將 `SelectedItem` 設為雙向繫結，使深層連結或搜尋結果能從檢視模型選取節點。
- 當樹位於包含其他內容的頁面中時，用 `MaxHeight` 限制其高度，使捲動區域就是樹本身。

**避免**

- 用樹來顯示幾段文章：`Accordion` 的閱讀體驗更好，也不需要鍵盤模型。
- 把大小和日期等資料欄位放進標籤中。紀錄請使用 `Table`。
- 期望多選。樹只有一個 `SelectedItem`，修飾鍵也不會選取一個範圍。
- 預設展開過多層級。同時展開的深層資料夾會使結構難以掃視。
- 每次按鍵都從資料的新副本重建樹。應在明確的操作後重新整理。

### 常見問題

**為什麼有子項目的節點不顯示其子項目？** `ItemsSource` 需要 `ChildrenSelector`。沒有它時，每個節點都會被當作檔案。請從選擇器回傳子項目集合；對於檔案，回傳 null 或空清單。

**如何將樹開啟到某個特定檔案？** 將 `SelectedItem` 設為該節點。樹會展開其上層節點並將該列捲動到可見位置。若只想顯示節點而不選取它，請使用 `ScrollIntoView`。

**樹能選取多個節點嗎？** 不能。`SelectedItem` 只保存一個節點，鍵盤會忽略 Control、Alt 和 Meta 等修飾鍵。當使用者必須選取多筆紀錄時，請使用設定了 `IsSelectable` 的 `Table`。

**一列能顯示標籤以外的內容嗎？** 能。設定 `ItemTemplate` 自行排版標籤區，例如名稱加型別，或在列尾放一個狀態點。請繼續設定 `HeaderSelector`，供鍵入跳轉和螢幕閱讀器使用。

**樹會對節點排序嗎？** 不會。它依你提供的順序顯示節點，因此繫結前請先對來源資料排序。

## API 參考

<!-- api: TreeView -->

<!-- api: TreeViewItem -->

### 事件參數

| 類型 | 成員 | 意義 |
|---|---|---|
| `TreeViewItemEventArgs` | `Item` | 節點：一個 `TreeViewItem` 或 `ItemsSource` 中的一項 |
| `TreeViewSelectionChangedEventArgs` | `OldItem`、`NewItem` | 先前選取的節點與新選取的節點 |

## 無障礙

- 樹預設名稱為「檔案樹」。設定 `AutomationProperties.Name` 可依你的內容為其命名。
- 各列公開樹狀項目角色，並提供層級、在同層級中的位置、展開狀態與選取狀態。
- 只有取得焦點的列在 Tab 順序中。方向鍵、Home 與 End、PageUp 與 PageDown 在列之間移動。
- 右方向鍵展開資料夾或進入其第一個子項目，左方向鍵收合資料夾或移到其上層列。
- Enter 或 Space 選取該列並切換資料夾的展開狀態。鍵入名稱會移到下一個以該文字開頭的列。
- 停用的列會回報自己處於停用狀態，鍵盤仍可到達，所以它的名稱仍會被朗讀。
- 焦點框僅為鍵盤焦點而繪製。圖示與縮排參考線不會被朗讀。

### 鍵盤

| 按鍵 | 操作 |
|---|---|
| `Up` / `Down` | 移到上一列或下一列可見列 |
| `Right` | 展開已收合的資料夾；在已展開的資料夾上，移到其第一個子項目 |
| `Left` | 收合已展開的資料夾；否則移到上層列 |
| `Home` / `End` | 移到第一列或最後一列可見列 |
| `PageUp` / `PageDown` | 大約移動一個畫面的列數 |
| `Enter` / `Space` | 選取該列，觸發 `ItemInvoked`，並切換資料夾的展開狀態；在停用的列上只切換資料夾 |
| 字母鍵 | 移到下一個標籤以所鍵入文字開頭的列 |

樹會忽略使用 Control、Alt 或 Meta 的按鍵，因此視窗的快速鍵仍然有效。

## 動態效果

- 展開資料夾時，其空間以彈簧增長，新列從左側依序錯落進入。
- 選取高亮在各列之間平滑滑動，資料夾箭頭以俐落的彈簧轉動。
- 收合資料夾時，其列平滑收起。
- 減少動態效果時列立即顯示或隱藏。關閉動態效果時直接到達最終狀態。

| 變化 | 完整 | 減少 | 關閉 |
|---|---|---|---|
| 資料夾展開或收合 | 空間以彈簧增長或收攏；新列錯落進入 | 列立即顯示或隱藏 | 立即完成 |

選取高亮僅在完整動態效果下於各列之間滑動。

## 相關元件

- `Accordion` 用於可展開與收合的少量內容分區。
- `Breadcrumb` 用於顯示目前頁面在階層中的位置。
- `JsonViewer` 用於供人檢視、可搜尋與複製的巢狀資料。
- `CodeBlock` 用於顯示樹中所選檔案的原始碼。

| 元件 | 適用情境 |
|---|---|
| `Accordion` | 少量文章分區需要展開和收合，且順序固定 |
| `Breadcrumb` | 頁面需顯示其在階層中的位置，並可返回上層 |
| `JsonViewer` | 巢狀資料是使用者需要檢視和複製的一份文件 |
| `CodeBlock` | 所選檔案的原始碼需與樹並排顯示 |
| `Table` | 紀錄的欄位相同，使用者需要依欄位比較 |
| `ReorderableList` | 使用者透過拖曳對扁平清單排序 |
