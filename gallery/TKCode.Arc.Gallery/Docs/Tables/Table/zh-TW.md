---
title: 表格
summary: 透過可排序的欄位比較結構化紀錄。
source-hash: f422614c088d
---

<Demo name="Projects" />

## 何時使用

`Table` 用於顯示欄位相同的紀錄，並讓使用者依欄位進行比較。它適合紀錄本身就是工作內容的頁面：帳單紀錄、管理後台中的專案清單、使用者目錄。每一欄都可以單獨排序，各列可以帶核取方塊以便批次操作，計數列會說明共有多少列、選取了多少列。

Arc 稱它為 sortable data table（可排序資料表格），早期預覽版的類別名稱是 `SortableDataTable`。舊類別名稱目前仍可使用，但會給出過時警告，並將在之後的預覽版中移除。

- **發票與帳單紀錄**：在帳單頁面依開立日期或金額排序，找到最近或最大的一筆費用，然後選取列下載或重新寄送。
- **管理後台中的專案與工作流程**：依負責人、狀態或預算排序，看出哪些工作需要關注；狀態標示放在 `CellTemplate` 欄中。
- **內部目錄中的使用者**：用核取方塊欄選取多人，然後執行一項批次操作，例如停用帳號。準備操作時，計數列會顯示「已選取 3 項」。
- **後台中的訂單**：依總額排序以找到大額訂單，並用 Shift + 點選選取一段連續的列，以進行批次退款或匯出。
- **開發者工具中的匯出與工作歷程**：列出過去的執行紀錄及其狀態和時長，依使用者關心的欄位排序，無需編輯。

## 何時不用

- **需要由人工設定順序的紀錄**，例如發佈佇列或手動置頂的優先順序清單：用 `ReorderableList`，因為欄位排序無法表達自訂順序。
- **依時間先後排列的事件**，例如稽核記錄或事故紀錄：用 `Timeline`，它能顯示事件的先後順序與間隔。
- **依欄位與搜尋文字縮小範圍**：在表格上方放置 `FilterToolbar`，並只傳入符合條件的列。表格只對收到的列排序，不負責篩選。
- **巢狀的紀錄**，例如包含檔案的資料夾：用 `TreeView`，它可以逐層展開和收合。
- **像試算表那樣就地編輯儲存格**：本表格為唯讀。請用 `Input` 和 `Select` 組成的表單。

## 用法

欄位在 XAML 中宣告。每一欄透過 `Binding` 讀取其儲存格的值；除非該欄設定了單獨的排序值，表格會依該值排序。

```xml
<arc:Table Caption="專案"
                       ItemsSource="{Binding Projects}"
                       ItemNameOne="專案"
                       ItemNameOther="專案"
                       SortKey="Budget"
                       SortDirection="Descending">
    <arc:Table.Columns>
        <arc:DataTableColumn Header="專案" Key="Name" Binding="{Binding Name}" />
        <arc:DataTableColumn Header="負責人" Binding="{Binding Owner}" IsSortable="False" />
        <arc:DataTableColumn Header="預算" Key="Budget" Binding="{Binding Budget}" IsNumeric="True" />
    </arc:Table.Columns>
</arc:Table>
```

`Table` 衍生自 Avalonia 的 `TemplatedControl`。它不以 `ItemsControl` 或 `DataGrid` 為基礎，而是自行繪製表頭、列和計數列。儲存格預設為文字，這讓每一列的繪製成本很低；需要超出文字的欄位則設定 `CellTemplate`。

### 結構

1. **標題**（`Caption`）：表格的無障礙名稱。它不會被繪製出來，因此應寫成名詞片語，例如「2026 年 10 月的發票」。
2. **表頭列**：高 44 px（含 1 px 分隔線）。表頭文字使用中等字重。可排序的表頭是按鈕；`IsSortable="False"` 的欄位則是一般標籤。已排序欄位的儲存格帶有淡化的欄底色。
3. **全選核取方塊**（`IsSelectable`、`SelectAllLabel`）：表頭起始處寬 44 px 的核取方塊欄，第一欄前額外留 8 px 的內距。只選取部分列時顯示混合狀態。
4. **資料列**：高 52 px，帶 1 px 分隔線（使用次要邊框色）和 16 px 的儲存格內距。`IsNumeric` 欄右對齊。懸停時整列填滿，完整動態效果下選取背景以標準補間動畫淡入。
5. **計數列**（`ItemNameOne`、`ItemNameOther`、`SelectedNoun`）：僅當 `IsSelectable` 為 true 時顯示的 44 px 頁尾。未選取時讀作「6 個專案」，選取一列時為「已選取 2 項」。未選取列時文字為淡化色。
6. **清除選取**（`ClearSelectionLabel`）：頁尾中 30 px 的幽靈按鈕，僅在有選取列時顯示。
7. **空列**（`EmptyMessage`）：高 120 px，使用淡化的文字顏色。
8. **載入遮罩**（`IsLoading`、`LoadingMessage`）：以表面色、60% 不透明度覆蓋表頭下方的列，中間為 18 px 的載入指示器，並顯示淡化色的文字。
9. **外框**：表面背景、1 px 邊框（邊框色）和控制項圓角。表格寬度低於 560 px 時，表格會橫向捲動，而不是壓縮欄寬。

### 欄位與排序

表頭預設作為排序識別碼，因此除非設定了 `Key`，`SortKey` 和排序事件回報的都是 `Header`。依資料選擇合適的屬性：

| 欄位屬性 | 適用情境 |
|---|---|
| `Binding` | 值是列項目的屬性，繫結即可滿足需求。 |
| `ValueSelector` 或 `DataTableColumn.Create<T>` | 應用程式以 AOT 或裁剪方式發佈，或偏好不含繫結的型別化程式碼。 |
| `SortBinding` 或 `SortValueSelector` | 顯示的文字不是用於排序的值，例如應依數字排序的格式化金額。 |
| `Comparer` | 排序規則與業務相關，例如發佈版本號。它依升冪排列；空值仍排在最後。 |
| `IsSortable="False"` | 該欄僅作描述，例如自由文字備註。 |
| `IsNumeric="True"` | 該欄為金額或計數，應右對齊。不設定時會自動偵測數字欄。 |
| `Width` | 像素寬度（`Width="120"`）使欄寬保持穩定；`*` 則佔用剩餘空間。 |

文字依自然順序排序（連續的數字依數值比較，因此「專案 2」排在「專案 10」之前），數字依數值排序，日期依其自身的順序排序。兩個方向上，空值都排在最後。

### 選取

設定 `IsSelectable` 即可顯示核取方塊欄。點選一列會選取它，Shift + 點選會選取上次切換的列與被點選列之間的所有列，表頭的核取方塊則選取或清除所有列。`SelectedItems` 為雙向繫結，表格會就地更新繫結的可變清單。

如果點選列要做別的事，例如開啟該列的詳細資料，可以設定 `SelectsOnRowClick="False"`：這時只有核取方塊那一格會切換選取，點選列的其他位置會交給你自己的事件處理。兩種模式下鍵盤的選取方式相同。

```xml
<arc:Table ItemsSource="{Binding Projects}"
                       IsSelectable="True"
                       SelectedItems="{Binding SelectedProjects, Mode=TwoWay}">
    <arc:Table.Columns>
        <arc:DataTableColumn Header="專案" Binding="{Binding Name}" />
    </arc:Table.Columns>
</arc:Table>
```

選取狀態跟隨列項目；設定了 `RowKeySelector` 時則跟隨它回傳的鍵。當重新整理會為同一批紀錄建立新物件時，請設定 `RowKeySelector`，這樣選取狀態和列動態效果就會始終跟隨同一筆紀錄。

### 儲存格內容與格式

文字使用 `Formatter`，自訂內容（例如狀態標示）使用 `CellTemplate`。`Formatter` 接收原始值並回傳文字，因此對金額和日期應傳入地區設定。`StringFormat` 是較簡單的選項，適用於標準或複合格式。兩者同時設定時，`Formatter` 優先；值為 null 時顯示一個 en 破折號（–）。

```csharp
var table = new Table { ItemsSource = projects, Caption = "專案" };
table.Columns.Add(DataTableColumn.Create<Project>("專案", p => p.Name));
table.Columns.Add(DataTableColumn.Create<Project>("預算", p => p.Budget, key: "Budget"));
table.Columns[1].Formatter = value => value is decimal amount ? amount.ToString("C0", CultureInfo.GetCultureInfo("en-US")) : "-";
table.Columns.Add(new DataTableColumn("狀態", p => ((Project)p).Status)
{
    CellTemplate = new FuncDataTemplate<Project>((_, _) => new StatusPill())
});
```

列會隨捲動被回收，因此範本控制項必須在資料內容變更時重新讀取其內容。範例中的狀態標示在 `OnDataContextChanged` 中完成這項工作：

```csharp
public sealed record Project(string Name, string Owner, string Status, decimal Budget, DateOnly Due);

private sealed class StatusPill : Border
{
    private readonly TextBlock _text = new() { FontSize = 12 };

    public StatusPill()
    {
        Child = _text;
        Padding = new Thickness(8, 2);
        CornerRadius = new CornerRadius(999);
        HorizontalAlignment = HorizontalAlignment.Left;
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        _text.Text = (DataContext as Project)?.Status ?? string.Empty;
    }
}
```

### 排序與事件

點選可排序的表頭即依升冪排序，再次點選為降冪；新欄位總是從升冪開始。啟用 `IsSortClearable` 後，第三次點選會清除排序，列回到來源資料的順序。`SortChanged` 回報每一次使用者排序以及每一次對 `Sort` 的呼叫，`SelectionChanged` 回報使用者對選取的變更。`SortColumn`、`SortKey` 與 `SortDirection` 均為雙向繫結，因此檢視模型可以設定或讀取排序狀態。

<Demo name="SortedFromCode" />

```csharp
table.SortChanged += (_, e) => status.Text = e.Column is null
    ? "已清除排序"
    : $"依 {e.Column.Header} 排序，{e.Direction}";
table.SelectionChanged += (_, e) => status.Text = $"已選取 {e.SelectedItems.Count} 項";
table.Sort(table.Columns[1], DataTableSortDirection.Ascending);
```

### 繫結到檢視模型

檢視模型擁有列資料、排序與選取，表格只負責呈現它們。表格對收到的列進行排序，因此檢視模型應設定 `SortKey` 與 `SortDirection`，並回應選取的變更，而不是自行對集合排序。範例使用 CommunityToolkit.Mvvm。

```csharp
public sealed record Invoice(string Number, string Customer, decimal Amount, DateOnly Issued);

public interface IInvoiceService
{
    Task<IReadOnlyList<Invoice>> ListAsync();
    Task ResendAsync(IReadOnlyList<string> numbers);
}

public partial class InvoicesViewModel : ObservableObject
{
    private readonly IInvoiceService _invoices;

    public InvoicesViewModel(IInvoiceService invoices) => _invoices = invoices;

    [ObservableProperty]
    private ObservableCollection<Invoice> _rows = [];

    [ObservableProperty]
    private ObservableCollection<Invoice> _selectedRows = [];

    [ObservableProperty]
    private string? _sortKey = "Issued";

    [ObservableProperty]
    private DataTableSortDirection _sortDirection = DataTableSortDirection.Descending;

    [ObservableProperty]
    private bool _isBusy;

    [RelayCommand]
    private async Task RefreshAsync()
    {
        IsBusy = true;
        try
        {
            Rows = new ObservableCollection<Invoice>(await _invoices.ListAsync());
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private Task ResendAsync() => _invoices.ResendAsync(SelectedRows.Select(r => r.Number).ToList());

    private bool HasSelection => SelectedRows.Count > 0;
}
```

```xml
<arc:Table Caption="2026 年 10 月的發票"
                       ItemsSource="{Binding Rows}"
                       IsLoading="{Binding IsBusy}"
                       IsSelectable="True"
                       ItemNameOne="發票"
                       ItemNameOther="發票"
                       SortKey="{Binding SortKey, Mode=TwoWay}"
                       SortDirection="{Binding SortDirection, Mode=TwoWay}"
                       SelectedItems="{Binding SelectedRows, Mode=TwoWay}">
    <arc:Table.Columns>
        <arc:DataTableColumn Header="編號" Key="Number" Binding="{Binding Number}" />
        <arc:DataTableColumn Header="客戶" Binding="{Binding Customer}" />
        <arc:DataTableColumn Header="金額" Key="Amount" Binding="{Binding Amount}" IsNumeric="True"
                             StringFormat="C0" />
        <arc:DataTableColumn Header="開立日期" Key="Issued" Binding="{Binding Issued}" StringFormat="d" />
    </arc:Table.Columns>
</arc:Table>
```

- `SortKey` 與 `SortDirection` 為雙向繫結：使用者選擇的排序會寫回檢視模型，檢視模型中預設的「Issued」降冪會在表格載入時生效。
- `SelectedItems` 為雙向繫結，需要 `ObservableCollection<Invoice>` 作為目標。`ResendAsync` 讀取的是同一個清單。
- 將 `IsLoading` 繫結到忙碌旗標，使請求進行期間遮罩覆蓋各列。
- 重新整理時重建集合，或修改現有的 `ObservableCollection<T>`。選取跟隨列的鍵，因此當列對應的仍是同一物件或 `RowKeySelector` 回傳相同的鍵時，選取在重新整理後得以保留。
- 編譯繫結需要在檢視上設定 `x:DataType`（範例為求簡潔省略了它）。

### 資料結構與效能

每一列都是一筆紀錄，每一欄的欄位都相同。表格在用戶端對收到的列進行排序，因此它適合已經載入完成的結果集。只有可視範圍附近的列會被實例化，這使下方 5,000 列的示範仍能流暢捲動；排序只重新排列索引，而不是重新排列列控制項。

排序會在每次來源資料或排序條件變更時執行，因此排序值應保持輕量：依已儲存的數字或 `DateOnly` 排序，而不是依多個欄位拼接出的字串排序。對於數萬筆紀錄的結果集，請每次只載入一頁，並在表格下方顯示 `Pagination`，這樣表格持有的列數永遠不會超過一頁所需。

### 空白與載入狀態

沒有列時顯示 `EmptyMessage`；`IsLoading` 會以遮罩覆蓋各列，遮罩文字由 `LoadingMessage` 指定。空白狀態的文案應寫明原因與下一步，尤其是在篩選之後。

<Demo name="EmptyAndLoading" />

```xml
<arc:Table EmptyMessage="沒有符合這些篩選條件的專案"
                       IsLoading="{Binding IsBusy}" />
```

### 五千列

5,000 列的示範設定了 `IsSortClearable` 和 420 px 的最大高度，因此表格在自己的外框內捲動，表頭保持固定。

<Demo name="FiveThousandRows" />

### 窄寬度

寬度低於 620 px 時，各列會摺疊為兩行，列高增至 64 px，表頭變成一排高 32 px 的排序膠囊，「全選」固定在起始位置。點選膠囊會依其欄位排序，並將該膠囊捲動到可見範圍。

<Demo name="Narrow" />

### 範例：帶選取的匯出歷程

開發者工具列出團隊產生的匯出檔案。每個匯出項目是一個檔名，操作者選取要下載或刪除的檔案。列是一般字串，因此該欄沒有 `Binding`，每個儲存格直接顯示列項目本身；在實際應用程式中，該欄會繫結到 `FileName` 之類的屬性。名稱依升冪排列，使匯出依類型分組；選取了兩列，因此計數列顯示「已選取 2 項」，並出現清除選取按鈕。

![帶有兩個選取列和清除選取操作的匯出歷程表格](images/ExportHistory.png)

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc"
             xmlns:generic="using:System.Collections.Generic"
             Width="560">
    <arc:Table Caption="Export history"
                           ItemNameOne="export"
                           ItemNameOther="exports"
                           IsSelectable="True"
                           SortKey="Export"
                           SortDirection="Ascending">
        <arc:Table.ItemsSource>
            <generic:List x:TypeArguments="x:String">
                <x:String>invoices_2026_08.csv</x:String>
                <x:String>invoices_2026_09.csv</x:String>
                <x:String>usage_2026_09.csv</x:String>
                <x:String>payouts_2026_q3.csv</x:String>
                <x:String>audit_log_2026_10_14.csv</x:String>
            </generic:List>
        </arc:Table.ItemsSource>
        <arc:Table.SelectedItems>
            <generic:List x:TypeArguments="x:Object">
                <x:String>invoices_2026_09.csv</x:String>
                <x:String>usage_2026_09.csv</x:String>
            </generic:List>
        </arc:Table.SelectedItems>
        <arc:Table.Columns>
            <arc:DataTableColumn Header="Export" Width="*" />
        </arc:Table.Columns>
    </arc:Table>
</UserControl>
```

### 內容規範

表格中的文字是用來掃讀的，而不是通讀的。表頭保持在一兩個名詞以內，讓細節由儲存格承載。

| 不要寫 | 改為 |
|---|---|
| 含稅的美元總額 | 總計 |
| 發票開立的日期 | 開立日期 |
| 目前已選取 5 項 | 已選取 5 項 |
| 沒有資料 | 沒有符合這些篩選條件的發票 |
| 載入中，請稍候…… | 正在載入發票 |
| 選取本頁的所有紀錄 | 全選發票 |

### 最佳做法

**建議**

- 表頭為顯示標籤的欄位設定 `Key`，這樣事件和檢視模型保存的排序識別碼在標籤被翻譯後仍保持穩定。
- 金額與計數欄位設定 `IsNumeric="True"`，使數字右對齊。
- 重新整理會為同一批紀錄建立新物件時，設定 `RowKeySelector`，使選取在重新整理後得以保留。
- 為表格至少留出 560 px 寬度（低於此寬度時表格會橫向捲動），並在 620 px 以下進行測試，那裡會切換為精簡版面。

**避免**

- 依帶格式的字串（例如「$1,200」）排序，卻不設定 `SortBinding` 或 `SortValueSelector`：文字依數字片段比較，因此「$1,200」會排在「$900」之前。
- 在沒有 `Pagination` 的情況下載入超大資料集：所有列都會駐留記憶體，且每次點選排序都要處理全部資料。
- 只靠顏色表示狀態。請把標示文字保留在儲存格中，使狀態不依賴顏色也能讀懂。
- 期望表格可以編輯列。它是唯讀的，也不追蹤編輯；請改為開啟表單。

### 常見問題

**為什麼數字欄位依文字排序？** 儲存格的值是字串，通常是因為繫結回傳了格式化後的值。請繫結數字本身，或將 `SortBinding`（或 `SortValueSelector`）設定為數字屬性。

**如何在程式碼中清除排序？** 呼叫 `Sort(null, DataTableSortDirection.None)`。列會恢復到來源資料的順序，且 `SortChanged` 會回報一個 null 欄位。

**如何選取符合篩選條件的列？** 將 `ItemsSource` 設為篩選後的列並呼叫 `SelectAll()`，或將 `SelectedItems` 繫結到相符的項目。`ClearSelection()` 會清除選取，並把焦點移到全選核取方塊。

**表格能篩選列嗎？** 不能。它只對收到的列進行排序和選取。請在檢視模型中篩選，或使用 `FilterToolbar`，並將篩選後的集合作為 `ItemsSource` 傳入。

## API 參考

<!-- api: Table -->

<!-- api: DataTableColumn -->

## 無障礙

- 表格以其 `Caption` 作為名稱，並公開表格、表頭和列的角色。
- 可排序的表頭是一個按鈕，名稱類似「依預算排序，目前為升冪」。排序方向屬於名稱的一部分，表頭還會以項目狀態（升冪、降冪或無）回報。
- 只選取部分列時，全選核取方塊顯示混合狀態。其名稱為 `SelectAllLabel`，預設為「選取所有列」。
- 排序與選取的變更會在禮貌型即時區域中播報，且不打斷目前的朗讀，例如「依預算排序，升冪」「已清除排序」「已選取 5 項中的 2 項」「已清除選取」。
- 表格只為鍵盤焦點繪製自己的焦點框，並會隱藏視窗預設的焦點框。

### 鍵盤

| 按鍵 | 操作 |
|---|---|
| `Left` / `Right` 在表頭中 | 移到上一個或下一個表頭控制項；啟用選取時，全選為第一個 |
| `Home` / `End` 在表頭中 | 移到第一個或最後一個表頭控制項 |
| `Down` 在全選上 | 移到第一列的核取方塊（僅當 `IsSelectable` 為 true 且有列時） |
| `Enter` / `Space` 在可排序的表頭上 | 升冪，再降冪；啟用 `IsSortClearable` 時，第三次按下清除排序 |
| `Enter` / `Space` 在全選上 | 選取所有列；若所有列均已選取，則清除選取 |
| `Up` / `Down` 在列中 | 移到上一列或下一列；從第一列按 `Up` 返回全選 |
| `Home` / `End` 在列中 | 移到第一列或最後一列 |
| `PageUp` / `PageDown` 在列中 | 大約移動一個畫面的列數 |
| `Enter` / `Space` 在列上 | 切換該列的選取狀態（僅當 `IsSelectable` 為 true 時） |
| `Shift` + `Enter` / `Space` 在列上 | 切換該列，並將上次切換的列到此列之間的各列設為同一新狀態 |
| `Shift` + 點選列 | 將上次切換的列到被點選列之間的各列設為被點選列的新狀態 |
| `Escape` | 有選取列時清除選取；若焦點在「清除選取」上，則焦點返回全選 |

## 動態效果

- 排序變更時，留存的列會以平滑彈簧從原位滑動到新位置，排序箭頭以俐落的彈簧翻轉。
- 選取計數滾動到新值，清除選取按鈕帶著輕微模糊淡入。
- 減少動態效果時列立即重新排序，計數僅淡入淡出。關閉動態效果時直接到達最終狀態。

| 變化 | 完整 | 減少 | 關閉 |
|---|---|---|---|
| 排序變更 | 列以平滑彈簧滑動到新位置；排序箭頭翻轉 | 列立即重新排序 | 立即完成 |
| 選取計數變更 | 計數滾動到新值 | 計數淡入淡出 | 立即完成 |

## 相關元件

- `TreeView` 用於可巢狀的紀錄，例如帶子項目的資料夾。
- `ReorderableList` 用於使用者透過拖曳而非欄位來設定順序的情境。
- `FilterToolbar` 可在列進入表格之前，依欄位與搜尋縮小範圍。
- `Pagination` 用於將大量紀錄拆分為多頁。

| 元件 | 適用情境 |
|---|---|
| `ReorderableList` | 使用者透過拖曳排列項目，且沒有欄位能描述順序 |
| `Timeline` | 紀錄是依時間排列的事件，順序比欄位更重要 |
| `FilterToolbar` | 列在比較前需要上方的篩選器或搜尋欄位 |
| `Pagination` | 結果集太大無法一次載入，每一頁都是單獨的查詢 |
| `TreeView` | 紀錄會巢狀排列，使用者需要逐層展開和收合 |
| `CodeBlock` | 該值是供閱讀和複製的程式碼片段，而不是可比較的紀錄集 |
