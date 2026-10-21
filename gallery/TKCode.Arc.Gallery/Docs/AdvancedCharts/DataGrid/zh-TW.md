---
title: 資料表格
summary: 試算表式的表格：範圍選取、就地編輯、填滿控點、剪貼簿、復原、排序、篩選、分組、總計與匯出。
source-hash: 81a7289e5a82
---

<Demo name="Renewals" />

## 何時使用

- 像試算表一樣就地修改大量數值，例如預算、價目表或庫存。
- 需要透過複製貼上與 Excel 或 Google 試算表往來的資料。
- 需要範圍選取、填滿控點、向下填滿和復原的工作。
- 數萬列以內、需要篩選、排序、分組小計和總計的表格。
- 無法一次載入的伺服器資料，透過資料來源分塊讀取。
- 對勾選列的批次操作：複製、匯出、刪除或自訂操作。

## 何時不用

- 唯讀紀錄，只需排序和列選取：使用 `SortableDataTable`。
- 一次編輯一筆紀錄：使用 `Input`、`Select` 等欄位組成的表單。
- 每筆紀錄需要圖片或長文字等豐富內容：使用卡片或清單。

## 用法

欄透過 `Key` 對應列的欄位。列可以是 `DataGridRow` 物件（一個鍵加上數個具名欄位），也可以是任意類別，透過欄的繫結或存取函式讀取。每一列都需要唯一且穩定的鍵。請儲存原始值：數字存成數字，日期存成 `DateOnly`，不要存格式化後的文字。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:DataGrid Label="Renewals" MaxViewportHeight="440" ExportFormats="All">
        <arc:DataGridColumn Key="account" Header="Account" Width="188" IsPinned="True" />
        <arc:DataGridColumn Key="stage" Header="Stage" Type="Select" />
        <arc:DataGridColumn Key="arr" Header="ARR" Type="Currency" />
        <arc:DataGridColumn Key="health" Header="Health" Type="Percent" />
        <arc:DataGridColumn Key="renewal" Header="Renews" Type="Date" />
    </arc:DataGrid>
</UserControl>
```

```csharp
grid.ItemsSource = new ObservableCollection<DataGridRow>
{
    new("acct-1") { { "account", "Lumen Labs" }, { "stage", "Proposal" }, { "arr", 48000m }, { "renewal", new DateOnly(2026, 9, 30) } }
};
```

### 欄類型

`Type` 決定一欄如何讀取、顯示和彙總它的值。`Number`、`Currency` 和 `Percent` 靠右對齊，能解析輸入的數字（可帶貨幣符號、百分比符號、分位符號和會計括號），並在總計列中加總（百分比取平均）。`Select` 從 `Options` 中選擇，選項順序也是排序順序。`Date` 儲存 `DateOnly`，以介面語言的月份名稱或 `DateFormat` 顯示，用日曆編輯，並拒絕 01/02/2026 這類各國讀法不同的純數字寫法。`Decimals` 和 `CurrencySymbol` 控制數字格式；貨幣符號在任何介面語言下都保持不變。

```xml
<arc:DataGrid Label="Price list">
    <arc:DataGridColumn Key="price" Header="Price" Type="Currency" CurrencySymbol="€" Decimals="2" />
    <arc:DataGridColumn Key="margin" Header="Margin" Type="Percent" Decimals="1" Aggregate="Min" />
    <arc:DataGridColumn Key="due" Header="Due" Type="Date" DateFormat="yyyy-MM-dd" />
</arc:DataGrid>
```

### 編輯、剪貼簿與復原

直接輸入會取代原值；按 Enter 或 F2（或按兩下）則保留原值進入編輯。Enter 和 Tab 提交並移動；無法解析的值會讓編輯器保持開啟並抖動一下。複製會同時寫入以定位字元分隔的文字和 HTML 表格，因此 Excel 和 Google 試算表能辨識其中的數字；貼上兩種格式都能讀取，選取範圍恰好是剪貼簿內容的整數倍時會平鋪填滿。拖曳填滿控點可以延續數列（數字依間距，日期依天或依月），按 Ctrl+D（Cmd+D）向下填滿。復原保留 `UndoLimit` 步（預設 100）。計算欄或鎖定的欄設為 `IsEditable="False"`：編輯、貼上、填滿和清除都會略過它們。

<Demo name="Budget" />

`CellEditEnding` 讓應用程式可以拒絕某個值，`CellsChanged` 回報每個被寫入的儲存格及其新舊值和寫入原因。編輯值不會重新排序或篩選，列會停在原位，直到排序或篩選改變。

```csharp
grid.CellEditEnding += (_, e) =>
{
    if (e.NewValue is decimal amount && amount < 0)
    {
        e.Cancel = true;
        e.ErrorMessage = "ARR cannot be negative";
    }
};
grid.CellsChanged += (_, e) => SaveChanges(e.Changes);
```

### 使用自己的類別作為列

為欄設定編譯繫結 `Binding`（編輯會雙向寫回），或用 `DataGridColumn.Create` 提供存取函式，後者相容 AOT、不需要繫結。只有取值函式、沒有設值函式的欄是唯讀的。

<Demo name="TypedRows" />

```csharp
grid.Columns.Add(DataGridColumn.Create<Account>("arr", "ARR", a => a.Arr, (a, v) => a.Arr = v as decimal? ?? 0,
    DataGridColumnType.Currency));
grid.Columns.Add(DataGridColumn.Create<Account>("total", "Total", a => a.Arr * a.Seats, type: DataGridColumnType.Currency));
```

### 排序、篩選與搜尋

按一下欄標題依序遞增、遞減、取消排序；按住 Shift 按一下會把該欄加入為下一個排序鍵。欄選單可以排序、篩選、分組、固定和隱藏。篩選列接受文字（`=` 表示完全相符，`!` 表示排除）、數字（`>100`、`10..50`）、日、月或年（`> 2026-03`、`2026-01-01..2026-03-31`），選項欄則使用值的勾選清單。快速搜尋要求每個字詞都出現在某個可見欄中。

### 檢視狀態

排序、篩選、搜尋、勾選列、隱藏和固定的欄、列密度、分組以及篩選列的開合都是雙向屬性，檢視模型可以儲存和還原檢視，也可以從外部設定。

<Demo name="ControlledState" />

```xml
<arc:DataGrid SortDescriptions="{Binding Sort}"
              Filters="{Binding Filters}"
              CheckedRowKeys="{Binding Checked}"
              Density="{Binding Density}" />
```

### 分組與總計

`GroupBy` 接受欄鍵，外層在前。分組列顯示摺疊按鈕、分組值、列數，以及彙總方式不為 `None` 的各欄小計；同一彙總方式也用於總計列，總計列一律統計篩選後保留的全部列。匯出 Excel 時，分組會寫成帶 SUBTOTAL 公式的大綱列。

<Demo name="Ledger" />

### 勾選列與批次操作

在列號欄勾選列（按住 Shift 按一下可勾選一段，Shift+Space 勾選選取範圍所在的列）。工具列隨後提供複製、匯出、你的 `BulkActions` 和刪除；刪除可以在狀態列復原。

```xml
<arc:DataGrid Label="Accounts">
    <arc:DataGrid.BulkActions>
        <arc:DataGridBulkAction Label="Archive" Icon="{x:Static arc:ArcIcons.Archive}" Command="{Binding ArchiveCommand}" />
    </arc:DataGrid.BulkActions>
</arc:DataGrid>
```

### 匯出

`ExportFormats` 列出可選的 CSV 和 Excel。匯出依檢視順序寫出篩選後保留的全部列（批次操作中的匯出只寫勾選列）；`Exporting` 可以改檔名或取消匯出。`ExportAsync` 用於從程式碼匯出。CSV、xlsx、剪貼簿、解析和篩選函式都是公開的靜態輔助類別（`DataGridCsv`、`DataGridXlsx`、`DataGridClipboardText`、`DataGridValueParser`、`DataGridFilterExpression`、`DataGridSorting`），也可以在伺服器端使用。

```csharp
grid.Exporting += (_, e) => e.FileName = $"renewals-{DateTime.Today:yyyy-MM-dd}";
await grid.ExportAsync(DataGridExportFormat.Xlsx);
```

### 伺服器資料

資料太大、無法一次傳來時，設定 `DataSource`：表格會請求涵蓋可見列的資料塊（載入中顯示預留列），最多快取 40 塊，並在排序、篩選或搜尋變更時取消進行中的請求、重新開始。此時表格為唯讀，也不分組。

<Demo name="ServerData" />

### 載入與空狀態

`IsLoading` 顯示六列預留列。`EmptyMessage` 取代「尚無資料」；篩選把所有列都隱藏時，表格會提示並提供「清除篩選」。

<Demo name="States" />

## API 參考

<!-- api: DataGrid -->

<!-- api: DataGridColumn -->

<!-- api: DataGridBulkAction -->

## 無障礙

- 表格主體是一個可聚焦元素。方向鍵、Shift+方向鍵、Ctrl+方向鍵（macOS 上為 Cmd）、Home、End、PageUp 和 PageDown 移動和擴展選取範圍；Tab 離開表格。
- 主體的無障礙名稱跟隨作用中的儲存格（「欄名，列名：值」），說明文字提供位址，因此螢幕閱讀器會朗讀每次移動。Avalonia 沒有表格類的自動化模式，所以列和儲存格不是獨立的自動化元素。
- 編輯、排序、勾選、複製、貼上、填滿、復原和重做都會在禮貌型即時區域朗讀。
- 分組時，Alt+← 摺疊作用中列所在的分組，Alt+→ 再次展開；會朗讀分組及其列數。
- 欄標題的排序按鈕名稱包含排序方向、優先順序和調整欄寬的按鍵（Alt+方向鍵）。全選方塊是三態核取方塊。
- 直接輸入會帶著輸入的文字開始編輯。使用輸入法（中文、日文）時，請先按 Enter、F2 或按兩下進入編輯，再在編輯器中組字。

## 動態效果

- 選取範圍和作用中的儲存格在不同範圍之間以彈簧移動；捲動和調整欄寬時直接到位。
- 排序後，列滑到新位置，從檢視外進來的列淡入。
- 貼上、填滿、復原和重做的儲存格閃一下，並在 0.9 秒內淡出。
- 工具列切換到批次操作時短暫上升並淡入；總計、統計和勾選數以滾動數字顯示。
- 無效編輯抖動一次，篩選列以彈簧展開，分組箭頭隨之旋轉。
- 減少動態效果時選取範圍直接到位，移除列滑動和抖動，保留短暫的淡入淡出。關閉動態效果時所有變化立即顯示。

## 相關元件

- `SortableDataTable`：唯讀紀錄。
- `FilterToolbar`：清單或表格上方的篩選工具列。
- `DatePicker` 和 `Calendar`：日期編輯器使用的日曆。
- `AnimatedCounter`：總計使用的滾動數字。
