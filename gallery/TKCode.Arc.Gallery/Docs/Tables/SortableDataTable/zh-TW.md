---
title: 可排序資料表格
summary: 透過可排序的欄位比較結構化紀錄。
source-hash: a5b5f6c27ca4
---

<Demo name="Projects" />

## 何時使用

- 欄位相同的紀錄，例如專案、發票或使用者，人們需要依欄位進行比較。
- 需要核取方塊欄進行列選取、Shift 範圍選取以及計數列的表格。
- 中短清單。較長的清單請搭配 `Pagination`。

## 何時不用

- 順序由使用者手動決定：用 `ReorderableList`。
- 依時間先後排列的事件，例如活動記錄：用 `Timeline`。
- 依欄位與搜尋篩選列：在表格上方放置 `FilterToolbar`。
- 像試算表那樣原地編輯儲存格：本元件為唯讀。

## 用法

欄位在 XAML 中宣告。每一欄透過 `Binding` 讀取其儲存格的值。

```xml
<arc:SortableDataTable Caption="專案"
                       ItemsSource="{Binding Projects}"
                       ItemNameOne="專案"
                       ItemNameOther="專案"
                       SortKey="Budget"
                       SortDirection="Descending">
    <arc:SortableDataTable.Columns>
        <arc:DataTableColumn Header="專案" Key="Name" Binding="{Binding Name}" />
        <arc:DataTableColumn Header="負責人" Binding="{Binding Owner}" IsSortable="False" />
        <arc:DataTableColumn Header="預算" Key="Budget" Binding="{Binding Budget}" IsNumeric="True" />
    </arc:SortableDataTable.Columns>
</arc:SortableDataTable>
```

### 選取

設定 `IsSelectable` 即可顯示核取方塊欄。點選一列即選取它，Shift + 點選可選取一個範圍，計數列提供清除操作。`SelectedItems` 為雙向繫結。

```xml
<arc:SortableDataTable ItemsSource="{Binding Projects}"
                       IsSelectable="True"
                       SelectedItems="{Binding SelectedProjects, Mode=TwoWay}">
    <arc:SortableDataTable.Columns>
        <arc:DataTableColumn Header="專案" Binding="{Binding Name}" />
    </arc:SortableDataTable.Columns>
</arc:SortableDataTable>
```

### 儲存格內容與格式

文字使用 `Formatter`，自訂內容（例如狀態標示）使用 `CellTemplate`。當需要避免繫結時（例如應用程式以 AOT 方式發佈），可以在程式碼中使用型別化的值選擇器。

```csharp
var table = new SortableDataTable { ItemsSource = projects, Caption = "專案" };
table.Columns.Add(DataTableColumn.Create<Project>("專案", p => p.Name));
table.Columns.Add(DataTableColumn.Create<Project>("預算", p => p.Budget, key: "Budget"));
table.Columns[1].Formatter = value => value is decimal amount ? amount.ToString("C0", CultureInfo.GetCultureInfo("en-US")) : "-";
table.Columns.Add(new DataTableColumn("狀態", p => ((Project)p).Status)
{
    CellTemplate = new FuncDataTemplate<Project>((_, _) => new StatusPill())
});
```

### 排序與事件

`SortChanged` 和 `SelectionChanged` 回報使用者的操作。設定 `IsSortClearable` 後，使用者第三次點選表頭即可清除排序。

<Demo name="SortedFromCode" />

```csharp
table.SortChanged += (_, e) => status.Text = e.Column is null
    ? "已清除排序"
    : $"依 {e.Column.Header} 排序，{e.Direction}";
table.SelectionChanged += (_, e) => status.Text = $"已選取 {e.SelectedItems.Count} 項";
table.Sort(table.Columns[1], DataTableSortDirection.Ascending);
```

### 空白與載入狀態

沒有列時顯示 `EmptyMessage`。`IsLoading` 會以載入遮罩覆蓋各列，`LoadingMessage` 說明正在等待的內容。

<Demo name="EmptyAndLoading" />

```xml
<arc:SortableDataTable EmptyMessage="沒有符合這些篩選條件的專案"
                       IsLoading="{Binding IsBusy}" />
```

### 五千列

各列會被虛擬化，表頭保持固定。排序只重排索引，而不是重排容器；`IsSortClearable` 允許使用者第三次點選以清除排序。

<Demo name="FiveThousandRows" />

### 窄寬度

寬度低於 620px 時，各列會摺疊成兩行文字，表頭變成一排排序膠囊。

<Demo name="Narrow" />

## API 參考

<!-- api: SortableDataTable -->

<!-- api: DataTableColumn -->

## 無障礙

- 表格以其 `Caption` 作為名稱，並公開表格、表頭和列的角色。
- 可排序的表頭是一個按鈕，名稱類似「依預算排序，目前為升冪」。排序方向屬於名稱的一部分。
- 只選取部分列時，全選核取方塊顯示混合狀態。
- 排序與選取的變化會在狀態區域播報，且不打斷目前的朗讀。
- 方向鍵在表頭與核取方塊之間移動，Enter 或 Space 排序或切換選取，Escape 清除選取。

## 動態效果

- 排序變更時，各列以彈簧動畫滑動到新位置，排序箭頭翻轉。
- 選取計數滾動到新值，清除按鈕帶著輕微模糊淡入。
- 減少動態效果時各列立即重新排列，計數僅淡入淡出。關閉動態效果時直接到最終狀態。

## 相關元件

- `TreeView`：用於可巢狀的紀錄，例如帶子項目的資料夾。
- `ReorderableList`：使用者透過拖曳而非欄位來設定順序時使用。
- `FilterToolbar`：在紀錄進入表格之前，依欄位與搜尋縮小範圍。
- `Pagination`：將大量紀錄拆分為多頁。
