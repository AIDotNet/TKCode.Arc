---
title: 樹狀表格
summary: 帶巢狀列、三態選取、保持階層的排序，以及按需載入子列的表格。
source-hash: ee6c67ae7e45
---

<Demo name="Budget" />

## 何時使用

`TreeTable` 用來顯示可以巢狀、並共用同一組欄位的紀錄。一列可以展開以顯示它的子列，每一列保持相同的欄位，父列還能彙總其下的內容。使用者展開自己關心的分支，在分支內部排序，並用一個核取方塊選取整個分支。

- **帶有大小、擁有者和日期欄位的檔案清單**：資料夾展開後顯示其中的檔案，依大小排序時，每個資料夾內部的檔案會重新排列，但不會被拉出所屬的資料夾。
- **逐層彙總的預算與成本報表**：部門包含團隊，團隊包含明細項目，頁尾可以加總使用者選取的列。
- **組織架構與權限矩陣**：團隊包含成員，每一列帶有擁有者、角色或數量。
- **按需載入的大型樹**：可以把一列標記為「有尚未載入的子列」，表格在該列第一次展開時取得子列，請求失敗時顯示重試列。
- **對階層進行批次操作**：勾選父列會勾選其下的所有內容，只勾選了部分子列時，父列顯示混合狀態。

## 何時不用

- **沒有欄位的導覽樹**：用 `TreeView`，它每個節點只繪製一個標籤，並能虛擬化數千列。
- **依欄位比較的扁平紀錄**：用 `Table`。在沒有巢狀時，樹狀表格為第一欄增加縮排和箭頭並沒有任何好處。
- **逐個儲存格編輯的試算表**：用 `DataGrid`，它支援範圍選取和就地編輯。
- **展開後是自由格式內容的項目**：用 `Accordion`。樹狀表格展開後得到的是更多具有相同欄位的列。
- **同時展開幾千列**：每一個可見的列都真實存在並帶有動畫。展開的樹請保持在幾百列以內，更深的層級按需載入。

## 用法

把 `ItemsSource` 設為頂層列，把 `ChildrenSelector`（一個委派，在程式碼中設定）設為傳回某一列的子列。選擇器傳回空清單的列是葉節點。向 `Columns` 加入欄位；顯示出來的第一欄承載樹。

```xml
<arc:TreeTable Caption="Files"
               ItemsSource="{Binding Files}"
               IsSelectable="True"
               MaxHeight="480">
    <arc:TreeTableColumn Header="Name" Binding="{Binding Name}" />
    <arc:TreeTableColumn Header="Size" Binding="{Binding SizeMb}" StringFormat="{}{0} MB"
                         Width="96" IsEndAligned="True" />
    <arc:TreeTableColumn Header="Owner" Binding="{Binding Owner}" HideBelow="520" />
</arc:TreeTable>
```

`TreeTable` 衍生自 Avalonia 的 `TemplatedControl`。與 Arc 一樣，它不做虛擬化：每個可見列都是真實的列，會長出、離開並帶有動畫。`ChildrenSelector`、`KeySelector` 這類選擇器接受委派，因此在程式碼中設定；表格的其餘部分可以用 XAML 撰寫。

```csharp
var table = new TreeTable
{
    ItemsSource = files,
    ChildrenSelector = item => ((FileNode)item).Children,
    KeySelector = item => ((FileNode)item).Path,
    RowLabelSelector = item => ((FileNode)item).Name,
    IsSelectable = true
};
table.Columns.Add(TreeTableColumn.Create<FileNode>("Name", f => f.Name));
table.Columns.Add(new TreeTableColumn { Header = "Size", ValueSelector = f => ((FileNode)f).SizeMb, IsEndAligned = true });
```

### 組成

1. **外框**：表面背景，1 px 的邊框色框線和面板圓角。表格填滿可用寬度。`MaxHeight` 限制整個表格的高度：表頭保持固定，表身捲動。
2. **表頭**：40 px 高，使用小號字型和弱化文字色，下方有一條 1 px 的分隔線。可排序的表頭是一個按鈕；第一欄的表頭還包含「全部展開」開關。
3. **列**：44 px 高，下方有一條細線。滑過和聚焦時列以弱化表面色填滿，選取的列使用淡強調色。
4. **樹儲存格**：顯示出來的第一欄。它從 10 px 處開始，每一層縮排 20 px，內容前有一個 24 px 的箭頭位置。葉節點保留這個空位，讓標籤對齊。
5. **縮排參考線**：穿過每個祖先箭頭欄的 1 px 線。指標下的分支或取得焦點的列所在的分支，其參考線會變深。
6. **儲存格**：每個其他顯示的欄位一個，左右內距 12 px。靠右對齊的欄位把文字靠右，並使用等寬數字。
7. **核取方塊欄**（`IsSelectable`）：44 px 寬，表頭中有「全選」核取方塊。
8. **載入列與重試列**：在正在取得子列的父列下方，顯示一列帶旋轉圖示的載入列，其他欄位裡是骨架條；或者顯示「Couldn't load」列與重試按鈕。

### 欄位

`TreeTableColumn` 定義一欄。儲存格的值來自 `ValueSelector`（委派，適合 AOT）或 `Binding`，再由 `Formatter` 或 `StringFormat` 轉為文字。需要文字以外的內容時設定 `CellTemplate`；它的資料內容是列項目。

| 屬性 | 用途 |
|---|---|
| `Header`、`Key` | 為欄位命名，並為 `SortKey` 提供名稱（未設定 `Key` 時以表頭文字作為索引鍵） |
| `Width` | 設定像素寬度（`96`）或剩餘空間的份額（`2*`）；預設是 `1*`，它會縮小並截斷 |
| `IsEndAligned` | 讓數字和金額靠右對齊 |
| `HideBelow` | 當表格窄於這個像素值時隱藏該欄 |
| `SortValueSelector`、`IsSortable` | 依顯示文字以外的值排序，或關閉排序 |

各欄共用同一組軌道，因此在每一層都對齊。當表格比某欄的 `HideBelow` 更窄時，該欄會從表頭和每一列中移除；樹所在的欄始終保留。

### 列識別

`KeySelector` 傳回一列的識別。`ExpandedKeys` 和 `SelectedKeys` 儲存的就是這些索引鍵，所以請使用穩定的值，例如 id 或路徑。未設定 `KeySelector` 時，列項目本身就是索引鍵。

### 展開與收合

點選父列、點選它的箭頭、按 Enter 以及方向鍵，都會切換它的展開狀態。`ExpandedKeys` 是雙向的：設定它可以在程式碼中展開列，也可以繫結到檢視模型；讀取它可以儲存展開狀態。表頭的開關會展開所有已載入子列的列，或者全部收合。

<Demo name="Controlled" />

```csharp
table.Expand(item);
table.Collapse(item);
table.ExpandAll();          // every row whose children are known
table.CollapseAll();
table.FocusItem(item);      // moves keyboard focus to a visible row
```

### 選取

設定 `IsSelectable` 後，每一列都有一個核取方塊。勾選父列會勾選其下的所有內容，子列只被勾選一部分的父列顯示混合狀態。`SelectedKeys` 始終儲存正規化後的集合：當且僅當某個父列的所有已知子列都被選取時，它才在集合中。只選取部分列時，全選核取方塊為混合狀態。

`SelectionChanged` 回報新的索引鍵和已載入的列項目。父列會彙總其子列，因此對選取內容加總時只應統計葉節點。

```csharp
table.SelectionChanged += (_, e) =>
{
    var leaves = e.SelectedItems.Cast<Spend>().Where(s => s.Children is not { Count: > 0 });
    footer.Text = $"{leaves.Sum(s => s.Monthly):C0} per month";
};
```

### 排序

帶有值的表頭依序循環：遞增、遞減、關閉。排序對階層是安全的：每個父列的同層列在該父列內部排序，所以子列永遠不會離開自己的分支。數字依數值比較，文字依自然順序比較，空值在兩個方向上都排在最後。`SortKey` 和 `SortDirection` 是雙向的，因此可以在程式碼中設定或儲存排序。

<Demo name="Files" />

### 按需載入子列

用 `HasChildrenSelector` 標記子列尚未載入的列，並為表格設定 `ChildrenLoader`。這樣的列第一次展開時，表格呼叫載入器，並在其下方顯示一列載入列。工作完成後，第一個子列接替載入列的位置。工作發生錯誤時，會出現帶「重試」按鈕的「Couldn't load」列，同時觸發 `ChildrenLoadFailed`，在父列上按 Enter 也會重試。在已勾選的父列下到達的子列預設處於勾選狀態。一列只載入一次：再次展開不會重新取得。

<Demo name="LazyLoading" />

```csharp
table.HasChildrenSelector = item => ((Account)item).HasChildren;
table.ChildrenLoader = async item => await api.GetChildrenAsync(((Account)item).Id);
```

### 啟動一列

點選沒有子列的列，或在其上按 Enter，會觸發 `RowActivated`，你可以在這裡開啟檔案或紀錄。父列則會切換展開狀態。

### 回應式欄位

表格適應的是它所得到的寬度，而不是視窗寬度。為最不重要的欄位（例如擁有者或修改日期）設定 `HideBelow`，表格變窄時它們會依序消失。

### 範例：依團隊統計的雲端支出

頂部的範例把支出逐層彙總了三層。擁有者欄在窄於 560 px 時隱藏，資源數欄在窄於 420 px 時隱藏，月度欄靠右對齊並格式化為貨幣，頁尾統計選取的明細列。

### 內容準則

- 保持樹所在欄的文字簡短。它會被截斷，而第一欄是使用者尋找列的地方。
- 把使用者要比較的數字放在靠右對齊的欄位裡，並在表頭或格式中寫明單位，例如 `{0} MB`。
- 用 `RowLabelSelector` 為輔助技術命名列，例如檔案名稱或團隊名稱；它也是每個核取方塊的名稱。
- 用 `EmptyMessage` 說明缺少什麼，例如「此資料夾中沒有檔案」。

### 最佳做法

- 為每一列提供穩定的 `KeySelector`。基於索引的索引鍵會在資料重新排序時破壞展開狀態。
- 用 `ChildrenLoader` 按需載入更深的層級，而不是預先載入數千列。
- 階層的彙總值請自己放在父列上；表格不會計算它們。
- 只在次要的欄位上使用 `HideBelow`，這樣表格不會丟掉使用者最先閱讀的欄位。
- 對可能變得很高的表格設定 `MaxHeight`，讓表頭始終可見。

### 常見問題

**為什麼父列無法展開？** 當選擇器至少傳回一個子列，或者 `HasChildrenSelector` 為 true 且設定了 `ChildrenLoader` 時，列才可以展開。空清單表示葉節點。

**欄位可以把樹排成平鋪嗎？** 不可以。排序只對每個父列內部的同層列排序，階層永遠不會被破壞。平鋪清單請使用 `Table`。

**為什麼重新載入資料後展開狀態遺失了？** `ExpandedKeys` 儲存的是索引鍵。如果 `KeySelector` 每次載入都傳回新值，列就對不上了。請使用重新載入後依然不變的 id。

**表格會統計選取的內容嗎？** 不會。請用 `SelectionChanged` 並自行累加需要的項目。

## API 參考

<!-- api: TreeTable -->

<!-- api: TreeTableColumn -->

### 事件引數

| 型別 | 成員 | 意義 |
|---|---|---|
| `TreeTableExpandedChangedEventArgs` | `ExpandedKeys` | 所有已展開列的索引鍵 |
| `TreeTableSelectionChangedEventArgs` | `SelectedKeys`、`SelectedItems` | 選取的索引鍵，以及它們對應的已載入列項目 |
| `TreeTableSortChangedEventArgs` | `Column`、`Direction` | 被排序的欄位（清除時為 null）與方向 |
| `TreeTableRowEventArgs` | `Item`、`Key`、`Error` | 對應的列；載入失敗時設定 `Error` |

## 無障礙

- 表格預設名稱為「Tree table」。設定 `Caption` 可以依你的內容命名。
- 列會提供名稱、在集合中的位置、集合大小和展開狀態。可排序的表頭會回報排序方向。
- Tab 順序中只有一列，方向鍵在列之間移動。
- 每個核取方塊都以 `RowLabelSelector` 命名，例如「Select Design」，並提供混合狀態。
- 載入、載入失敗、排序、全部展開和選取變更都透過禮貌型即時區域播報。
- 如果取得焦點的列因為其父列收合而消失，焦點會移到最近的可見祖先。
- 滑過填滿只對滑鼠指標生效；觸控點選會直接切換並啟動列。

### 鍵盤

| 按鍵 | 操作 |
|---|---|
| `Up` / `Down` | 在可見列之間移動 |
| `Right` | 展開一列；在已展開的列上，移到它的第一個子列 |
| `Left` | 收合一列；否則移到它的父列 |
| `Home` / `End` | 移到第一個或最後一個可見列 |
| `PageUp` / `PageDown` | 移動十列 |
| `*` | 展開取得焦點的列的所有同層列 |
| `Space` | 切換該列的選取狀態（需要 `IsSelectable`） |
| `Enter` | 切換父列、重試失敗的載入，或啟動葉節點 |
| `Ctrl` / `Cmd` + `A` | 選取所有列，或清除選取（需要 `IsSelectable`） |

## 動態效果

- 列從零高度在平滑彈簧上長出並淡入，同層列之間有 18 ms 的錯開；父列收合時它們會縮小消失。
- 載入列把自己的位置交給第一個到達的子列，所以不會出現列相互擠壓的情況。
- 排序時每一列在彈簧上移動到新位置。
- 箭頭在靈敏的彈簧上轉動四分之一圈，排序箭頭翻轉，「全部展開」圖示帶著轉動切換。
- 滑過與選取的填滿以 160 ms 淡入。

| 變化 | Full | Reduced | Off |
|---|---|---|---|
| 一列展開或收合 | 高度彈簧、錯開與淡入 | 短暫淡入，沒有高度彈簧和錯開 | 立即完成 |
| 排序 | 列在彈簧上移到新位置 | 列直接跳到新位置 | 立即完成 |
| 箭頭與排序箭頭 | 彈簧轉動 | 立即完成 | 立即完成 |

## 相關元件

- `TreeView`：沒有欄位的導覽樹。
- `Table`：帶可排序欄位的扁平紀錄。
- `DataGrid`：支援範圍選取和就地編輯的試算表式格線。
- `Checkbox`：每一列選取背後的二元控制項。

| 元件 | 何時選擇 |
|---|---|
| `TreeView` | 階層是導覽，每個節點只有一個標籤 |
| `Table` | 紀錄是扁平的，並依欄位比較 |
| `DataGrid` | 使用者要編輯儲存格並選取範圍 |
| `Accordion` | 少量區塊展開後是自由格式的內容 |
