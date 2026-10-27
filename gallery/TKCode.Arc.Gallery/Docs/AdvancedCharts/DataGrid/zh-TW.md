---
title: 資料表格
summary: 試算表式的表格：範圍選取、就地編輯、填滿控點、剪貼簿、復原、排序、篩選、分組、總計與匯出。
source-hash: 6f5dd7d44970
---

<Demo name="Renewals" />

## 何時使用

`DataGrid` 是讓使用者就地編輯表格的控制項，用起來像試算表。它自帶工具列、欄標題選單、篩選列、合計列和狀態列，因此表格本身就是工作區域。

- **財務工具中的預算與預測規劃**：規劃人員按季度填寫明細項目，從試算表貼上資料，跨月份填滿，貼上出錯時復原。數值變動時，合計列保持最新。
- **客戶成功主控台中的續約管線**：數百個客戶帳號，包含負責人、區域、階段、席位和 ARR。管理者可以篩選到某一區域，依續約日期排序，並直接在表格中推進階段。
- **電商後台的價目表與商品目錄**：批次維護價格、毛利和生效日期，複製貼上的行為與 Excel 和 Google Sheets 一致。
- **金融科技或帳務中的日記帳覆核**：依帳戶和期間分組的分錄，附小計，匯出為保留大綱結構和 SUBTOTAL 公式的活頁簿。
- **伺服器端的大量營運歷史**，例如訂單、付款或同步記錄：`DataSource` 只載入可視範圍內的資料區塊。

## 何時不用

- **附排序與列選取的唯讀記錄**，例如客服工單或使用者：用 `Table`，它不需要編輯相關機制。
- **一次只編輯一筆記錄**，例如客戶資料或發票抬頭：用由 `Input`、`Select` 和 `DatePicker` 組成的表單，它們會逐一欄位驗證。
- **含圖片、長文字或操作的列**：用 `Card` 或 `ReorderableList`，每個項目都有容納內容的空間。
- **資料夾或會計科目表等階層資料**：用 `TreeView`。分組依鍵值折疊列，不會根據父子記錄建立樹狀結構。
- **只在儀表板上彙總數字的表格**：用 `MetricCard` 或 `BarChart`。對於摘要呈現來說，工具列和狀態列過於繁雜。

## 用法

欄透過 `Key` 對應到列的欄位。列是 `DataGridRow` 物件（一個鍵加上具名欄位），或任何可透過欄繫結或存取子讀取的類別。每一列都需要唯一且穩定的鍵。儲存原始值：數字存為數字，日期存為 `DateOnly`，不要存格式化後的文字，這樣排序、合計、篩選和匯出都能作用於數值本身。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:DataGrid Label="續約" MaxViewportHeight="440" ExportFormats="All">
        <arc:DataGridColumn Key="account" Header="客戶" Width="188" IsPinned="True" />
        <arc:DataGridColumn Key="stage" Header="階段" Type="Select" />
        <arc:DataGridColumn Key="arr" Header="ARR" Type="Currency" />
        <arc:DataGridColumn Key="health" Header="健康度" Type="Percent" />
        <arc:DataGridColumn Key="renewal" Header="續約" Type="Date" />
    </arc:DataGrid>
</UserControl>
```

```csharp
grid.ItemsSource = new ObservableCollection<DataGridRow>
{
    new("acct-1") { { "account", "Lumen Labs" }, { "stage", "Proposal" }, { "arr", 48000m }, { "renewal", new DateOnly(2026, 9, 30) } }
};
```

`DataGrid` 衍生自 `TemplatedControl`，只繪製可視範圍內的列和欄。它的檢視狀態（排序、篩選、搜尋、已勾選的列、隱藏與固定的欄、密度和分組）都是雙向的，因此檢視模型可以儲存和還原它。

### 結構

1. **工具列**（`ShowToolbar`）：高 52 px，按鈕 32 px，搜尋欄位 220 px。提供搜尋、復原與重做、篩選、欄、密度和匯出。勾選列後工具列切換為批次狀態。寬度低於 640 px 時按鈕只顯示圖示；低於 460 px 時隱藏列數以及復原與重做。
2. **表頭列**：40 px。每個儲存格顯示 `Header`，以及排序按鈕和選單按鈕，選單可執行排序、篩選、分組、固定和隱藏。數值欄的表頭靠右對齊。
3. **篩選列**（`IsFilterRowOpen`）：40 px，從「篩選」或欄選單開啟。無效的運算式會標記所在儲存格。
4. **側欄**（`ShowRowNumbers`、`IsRowCheckEnabled`）：48 px，顯示列號和核取方塊。設定 `ShowRowNumbers="False"` 後寬度縮為 40 px，核取方塊仍然可見。
5. **資料列**（`Density`）：`Compact` 下為 32 px（13 px 文字），`Standard` 下為 40 px，`Comfortable` 下為 48 px，列與列之間以 1 px 的淡化分隔線隔開。
6. **選取範圍、作用中儲存格與填滿控點**：選取範圍以 9% 不透明度的強調色填滿，並帶 1 px 強調色外框；作用中儲存格有 2 px 強調色環。填滿控點是位於選取範圍右下角的 8 px 強調色方塊，外圍有 2 px 表面色環。
7. **合計列**（`ShowTotals`）：40 px，黏附在底部。它統計篩選保留的每一列，包括折疊分組中的列。
8. **狀態列**：34 px。顯示選取範圍的位址以及加總、平均值和計數（`ShowSelectionStats`），顯示列數（例如篩選時顯示「120 列中的 12 列」），以及訊息。提供「復原」的訊息停留 6 秒，其他訊息停留 2.4 秒。
9. **固定欄**（`IsPinned`）：表格本體橫向捲動時固定在左邊緣，帶 10 px 的邊緣陰影。

### 欄類型

`Type` 決定欄如何顯示、呈現、解析和彙總其值。數字、貨幣和百分比欄靠右對齊，並能解析帶貨幣符號、百分號、千分位和會計括號的輸入，例如 `(12)` 表示 -12。

| 類型 | 儲存 | 顯示 | 預設合計 | 輸入接受 |
|---|---|---|---|---|
| `Text` | `string` | 文字 | 無 | 任意文字 |
| `Number` | `decimal` | 依 `Decimals` 位數顯示的分組數字 | 加總 | 數字 |
| `Currency` | `decimal` | `CurrencySymbol`（預設 `$`）及分組數字 | 加總 | 數字，可帶或不帶符號 |
| `Percent` | `decimal` | 數值加百分號 | 平均值 | 數字，可帶或不帶百分號 |
| `Select` | `string` | 選項文字 | 無 | `Options` 中的一項，忽略大小寫 |
| `Date` | `DateOnly` | 所在語言的月份名稱，或 `DateFormat` | 無 | 日曆選取、ISO 日期、月份名稱 |
| `Boolean` | `bool` | 核取方塊 | 無 | yes、no、true、false、1、0、是、否 |

`Select` 依 `Options` 的順序排序，而不是依字母順序。`Date` 拒絕 01/02/2026 這類純數字格式，因為不同國家的讀法不同；請寫 `2026-02-01` 或使用月份名稱。貨幣符號不會隨介面語言改變，因此切換語言後歐元欄仍以歐元顯示。

`Aggregate` 可取 `None`、`Sum`、`Average`、`Min`、`Max` 或 `Count`（非空白儲存格），例如利潤率審查中使用 `Min`。

`Boolean` 欄在儲存格中間畫一個核取方塊。點擊它，或按空白鍵、Enter、F2，即可翻轉值；輸入 yes 或 no 會直接設定。翻轉屬於一般編輯：`CellEditEnding` 可以拒絕，每次翻轉是一步復原，唯讀欄不會翻轉。複製和 CSV 寫出 TRUE 與 FALSE，Excel 匯出寫成布林儲存格，篩選提供「是」和「否」。

### 儲存格轉譯器

預設情況下，欄會把值畫成文字。設定 `Renderer` 可以畫出更豐富的內容：

| 轉譯器 | 繪製內容 | 設定 |
|---|---|---|
| `BadgeRenderer` | 依值著色的小膠囊，適合階段或狀態 | `ToneMap`（或 `Tones`）、`DefaultTone` |
| `ProgressRenderer` | 一條進度列，值的文字顯示在右端 | `Minimum`、`Maximum`、`ShowText`、`Tone` |
| `LinkRenderer` | 強調色連結；點擊會觸發 `CellInvoked`，並且不會選取該儲存格 | `Part` |
| `IconTextRenderer` | 依值選擇的圖示，後面接著文字 | `Icons`、`DefaultIcon`、`Tones`、`ShowText` |
| `CheckboxRenderer` | 置中的核取方塊；點擊會翻轉值（Boolean 欄預設使用） | 無 |
| `ActionsRenderer` | 一排圖示按鈕，例如列尾的編輯和刪除；點擊會帶著該列執行動作的 `Command`，並觸發 `CellInvoked` | `Actions` |

<Demo name="CustomCells" />

```xml
<arc:DataGrid Label="Deals">
    <arc:DataGridColumn Key="stage" Header="Stage" Type="Select">
        <arc:DataGridColumn.Renderer>
            <arc:BadgeRenderer ToneMap="Won=Success, Proposal=Warning, Lost=Danger" />
        </arc:DataGridColumn.Renderer>
    </arc:DataGridColumn>
</arc:DataGrid>
```

表格會像繪製文字一樣，把轉譯器和列的其餘部分一起繪製，所以有數千列的表格依然流暢；轉譯器不是控制項。轉譯器只改變儲存格的外觀，不改變它保存的內容：排序、篩選、合計、匯出和編輯仍然作用於值，複製和螢幕閱讀器讀取的是 `GetText`。色彩來自 `DataGridTone`（`Neutral`、`Accent`、`Success`、`Warning`、`Danger`），在明暗主題下都會跟隨主題和強調色。

要繪製其他內容，請衍生 `DataGridCellRenderer` 並覆寫 `Render`。一個轉譯器由所在欄的所有儲存格共用，因此不要為單一儲存格保存狀態。

```csharp
public sealed class RatingRenderer : DataGridCellRenderer
{
    public override void Render(in DataGridCellRenderContext context)
    {
        var stars = context.Value is decimal value ? (int)value : 0;
        context.DrawText(new string('★', stars), context.Bounds.X, context.Bounds.Width, context.Palette.Accent);
    }

    public override string GetText(object? value, DataGridCellFormat format, CultureInfo culture) =>
        value is decimal d ? $"{d:0} of 5" : string.Empty;
}
```

用 `DrawText` 繪製文字，它會在影格之間保留每一行的排版；用相同的引數呼叫 `MeasureText` 來計算周圍元素的大小，筆刷請取自 `Palette`。覆寫 `OnPointerPressed`（以及用於手形游標的 `IsInteractiveAt`）來回應點擊，並呼叫 `Invoke` 觸發 `CellInvoked`。

要用自己的控制項編輯某欄的儲存格，請設定 `EditTemplate`。範本為 `DataGridEditContext` 建構內容：讀寫它的 `Value`，Enter、Tab 或 Escape 會提交或取消編輯（也可以呼叫 `Commit()` 與 `Cancel()`）。只有正在編輯的儲存格是真正的控制項，所以表格依然很快。值與鍵入走同一條路徑：必須符合欄的類型，`CellEditEnding` 可以拒絕它。

```csharp
column.EditTemplate = new FuncDataTemplate<DataGridEditContext>((context, _) =>
{
    var slider = new Slider { Minimum = 0, Maximum = 100, Value = context.Value is decimal v ? (double)v : 0 };
    slider.ValueChanged += (_, e) => context.Value = Math.Round((decimal)e.NewValue);
    return slider;
});
```

### 編輯、剪貼簿與復原

直接輸入即可取代值；按 Enter、F2 或連按兩下可進入編輯並保留原值，游標位於末尾。Enter 送出並向下移動，Shift+Enter 向上移動，Tab 送出並向右移動。Escape 取消。無法解析的值會保留編輯器，文字以危險色顯示並輕微晃動。在表格外按一下時，有效值會被送出，無效值會被捨棄。

複製時會寫入以定位字元分隔的文字和 HTML 表格，因此 Excel 和 Google Sheets 能辨識輸入的數字。貼上可讀取兩種格式；如果選取範圍是剪貼簿內容的整數倍，則平鋪填入。拖曳填滿控點可延續序列：數字依步長，日期依天或依月。Ctrl+D 向下填滿。復原保留 `UndoLimit` 步（預設 100 步；設為 0 則關閉復原）。對於計算欄或鎖定欄，請設定 `IsEditable="False"`：編輯、貼上、填滿和清除都會略過它們。貼上內容超過 `MaxPasteCells` 個儲存格（預設 1,000,000）時會被拒絕並給出提示，因此過大或惡意構造的表格不會耗盡記憶體。

<Demo name="Budget" />

`CellEditEnding` 在寫入值之前觸發，應用程式可以據此拒絕修改。`CellsChanged` 在每次寫入後觸發（包括復原和重做），事件引數包含每個儲存格的舊值和新值，以及 `Reason`（`Edit`、`Paste`、`Cut`、`Clear`、`Fill`、`FillDown`、`Undo` 或 `Redo`）。編輯不會重新排序或重新篩選，因此列保持原位。

```csharp
grid.CellEditEnding += (_, e) =>
{
    if (e.NewValue is decimal amount && amount < 0)
    {
        e.Cancel = true;
        e.ErrorMessage = "ARR 不能為負數";
    }
};
grid.CellsChanged += (_, e) => SaveChanges(e.Changes);
```

### 驗證、鎖定儲存格與條件樣式

為欄設定 `Rules` 來檢查使用者輸入、貼上或填滿的內容。內建規則有 `RequiredRule`、`RangeRule`（`Minimum`、`Maximum`）、`MaxLengthRule` 和 `PatternRule`；`DataGridRule.Custom` 接收一個函式，回傳錯誤訊息，通過時回傳 null。訊息會跟隨介面語言，也可以設定 `Message`。

預設（`ValidationMode="Block"`）會拒絕不通過的值：編輯器保持開啟並顯示訊息，貼上或填滿時不通過的儲存格會被略過，並計入狀態列。清除儲存格、復原和取消復原從不被阻止。設為 `ValidationMode="Flag"` 時，值會被接受，儲存格以紅色外框和一個紅點標出；工具列顯示被標出的數量（點擊它或按 F8 跳到下一個），選取被標出的儲存格會播報其訊息。`ValidateAll()` 會檢查每一列，適用於一開始就有錯誤的資料，`GetErrors()` 列出被標出的儲存格。

<Demo name="Validation" />

```xml
<arc:DataGrid Label="Orders">
    <arc:DataGridColumn Key="qty" Header="Qty" Type="Number">
        <arc:DataGridColumn.Rules>
            <arc:RequiredRule />
            <arc:RangeRule Minimum="1" Maximum="99" />
        </arc:DataGridColumn.Rules>
    </arc:DataGridColumn>
</arc:DataGrid>
```

`IsEditable` 鎖定整欄。要鎖定列或單一儲存格，請設定 `CanEditCell`，它是儲存格（列、索引鍵、欄和值）的函式：編輯、貼上、填滿和清除會略過它拒絕的儲存格。

要突顯某些值，請為欄設定 `FormatRules`。每條規則測試值（`GreaterThan`、`LessThan`、`Between`、`Equal`、`Contains`、`IsEmpty`、`Negative` 等），並用色調（`Neutral`、`Accent`、`Success`、`Warning` 或 `Danger`）為儲存格著色，還可以加粗；第一條符合的規則生效。被比較的值依輸入該欄的寫法書寫，例如 `$1,000` 或 `2026-06-01`。

```xml
<arc:DataGrid Label="Orders">
    <arc:DataGridColumn Key="due" Header="Due" Type="Date">
        <arc:DataGridColumn.FormatRules>
            <arc:DataGridFormatRule Condition="LessThan" Value="2026-06-01" Tone="Danger" />
        </arc:DataGridColumn.FormatRules>
    </arc:DataGridColumn>
</arc:DataGrid>
```

規則無法表達的邏輯，請設定 `CellStyleSelector`（依儲存格）或 `RowStyleSelector`（依列）。它們每一影格都會為檢視區內的每個儲存格執行，所以要保持輕量。

### 新增列

設定 `CanInsertRows` 後，工具列會顯示「新增列」，並允許呼叫 `AddRow()` 和 `InsertRow()`；Ctrl+Shift+加號在作用列上方插入。`DataGridRow` 類型的列無需其他設定；自有類別的列需要一個 `RowFactory`，回傳帶有自己索引鍵的新物件，並且 `ItemsSource` 必須是可寫入的清單。新增一列是一步復原，`RowsInserted` 會回報它，復原時會觸發 `RowsDeleted`。設定 `PasteOverflow="AppendRows"`，超出最後一列的貼上會新增所需的列，而不是捨棄。

```csharp
grid.CanInsertRows = true;
grid.RowFactory = () => new Account(Guid.NewGuid().ToString("N"));
```

### 欄、已儲存的檢視與儲存格事件

拖曳表頭標籤即可移動該欄，一條線會顯示它將落在哪裡。在表頭上按 Alt+Shift+Left 或 Alt+Shift+Right 可以用鍵盤完成同樣的操作。固定欄只在固定欄之間重新排列。`ColumnOrder` 保存依顯示順序排列的欄索引鍵，`ColumnWidths` 保存使用者設定的寬度；與其他檢視狀態一樣，兩者都是雙向的。按兩下表頭右邊緣，或在欄選單中選擇「符合內容寬度」，會把該欄調整到最寬儲存格和表頭所需的寬度（測量前 1,000 列）。`AutoFitColumns()` 處理所有欄，`ResetColumnWidths()` 復原這些寬度。

要儲存檢視，`GetLayoutState()` 回傳 `DataGridLayoutState`（欄順序與寬度、固定與隱藏的欄、排序、篩選、分組和列密度），`ToJson()` 用對裁剪與 AOT 安全的序列化器把它寫成 JSON。`SetLayoutState()` 還原它：狀態中缺少的欄位回到預設值，已不存在的欄的索引鍵會被忽略，所以欄變動後，儲存的檢視依然可用。

```csharp
File.WriteAllText(path, grid.GetLayoutState().ToJson());

// later, when the window opens:
if (DataGridLayoutState.FromJson(File.ReadAllText(path)) is { } state)
{
    grid.SetLayoutState(state);
}
```

表格會為使用者觸碰的儲存格觸發事件。`CellClick` 在用主按鈕按下主體儲存格時觸發，此時選取範圍已經移動。`CellDoubleClick` 在編輯器開啟之前觸發；設定 `Handled` 可以讓編輯器保持關閉。`RowActivated` 在對不可編輯的儲存格按 Enter 時觸發，也在按兩下列號時觸發，這就是「開啟這一列」的手勢。`CellContextRequested` 在按右鍵、Shift+F10、功能表鍵和長按時觸發；設定 `Handled` 可以顯示你自己的選單。

否則表格會選取該儲存格（當它在選取範圍之外時）並開啟選單：剪下、複製、複製（含表頭）和貼上；在上方或下方插入列（需要 `CanInsertRows`）和刪除列（需要 `CanDeleteRows`）；清除內容和向下填滿；依此值篩選、排序和分組。用 `ContextMenuItems` 新增項目，每個項目會帶著被點擊的列執行它的 `Command`；用 `ContextMenuOpening`（`Items`、`Cancel`）為某一次開啟修改它們；或設定 `ShowContextMenu="False"` 關閉選單。

```xml
<arc:DataGrid Label="Accounts">
    <arc:DataGrid.ContextMenuItems>
        <arc:DataGridMenuItem Label="Open account" />
    </arc:DataGrid.ContextMenuItems>
</arc:DataGrid>
```

### 尋找與取代

Ctrl+F 會在主體右上角開啟尋找列，Ctrl+H 會再加上取代列（也可以點箭頭）。它在使用者看到的文字中尋找，所以 `1,200` 能找到顯示為 `$1,200` 的儲存格。尋找列開啟時，所有相符的儲存格都會著色，尋找列會給出計數（「第 3 個，共 27 個」）。Enter 與 Shift+Enter，或 F3 與 Shift+F3，跳到下一個和上一個相符項目，到頭會循環；Escape 關閉尋找列。「區分大小寫」和「符合整個儲存格」可以縮小範圍。

「取代」會取代目前相符項目中的文字並跳到下一個；「全部取代」把所有相符項目作為一步復原一次改完。新文字會依輸入的方式讀回，所以在貨幣或日期儲存格裡取代時，只要結果仍是有效值就能成功；會破壞儲存格的取代（不存在的選項、數字欄裡的文字）會被略過，並計入狀態列。唯讀儲存格也會被略過。使用 `DataSource` 時，Ctrl+F 改為聚焦工具列搜尋，因為只有檢視區內的區塊被載入。

```csharp
grid.FindText = "labs";
grid.FindMatchCase = false;
grid.ReplaceAll("Group");   // one undo step; cells whose new text does not parse are skipped
```

### 使用自有類別的列

為欄指定已編譯的 `Binding`（編輯會雙向寫回），或使用 `DataGridColumn.Create` 建立欄：它使用存取子，不需要繫結，並且對 AOT 友善。沒有 setter 的欄為唯讀。兩種方式接收的都是正規化後的值：字串、`decimal`、`DateOnly` 或 null。

<Demo name="TypedRows" />

```csharp
public sealed class Account { public decimal Arr { get; set; } public int Seats { get; set; } }

grid.Columns.Add(DataGridColumn.Create<Account>("arr", "ARR", a => a.Arr, (a, v) => a.Arr = v as decimal? ?? 0,
    DataGridColumnType.Currency));
grid.Columns.Add(DataGridColumn.Create<Account>("total", "Total", a => a.Arr * a.Seats, type: DataGridColumnType.Currency));
```

### 排序、篩選與搜尋

按一下表頭可依序切換為遞增、遞減和取消排序。Shift+按一下會將該欄加入為下一個排序鍵；若它已是排序鍵，則反轉其方向。無論哪種方式，空值都會沉到底部。篩選列接受以下運算式：

- **文字**：`abc` 表示包含該文字，`=abc` 表示完全相符，`!abc` 表示排除。比對忽略大小寫和重音符號。
- **數字、貨幣和百分比**：`>100`、`<5`、`=12`、`!=0`，或封閉區間如 `10..50`。
- **日期**：`2026-01-31` 表示某一天，`2026-03` 表示某個月，`2026` 表示某一年。像 `> 2026-03` 這樣帶前綴的寫法依邊界比較，即表示 2026 年 3 月之後。區間寫作 `2026-01-01..2026-03-31`。
- **選取欄**：一組值的核取清單；超過七個值時上方會出現搜尋欄位，清單中還有一項用於空白儲存格。

無法讀取的運算式會將所在儲存格標記為無效，並且不生效。隱藏欄上的篩選仍然生效，並仍會計入「篩選」按鈕的數量。快速搜尋（`SearchText`）會把文字拆分為多個詞；每個詞都必須出現在某個可見欄中。

### 檢視狀態

排序、篩選、搜尋、已勾選的列、隱藏與固定的欄、密度和分組都是雙向屬性，因此檢視模型可以儲存、還原或重設檢視。`ClearFilters()` 會清除欄篩選和快速搜尋。

<Demo name="ControlledState" />

```xml
<arc:DataGrid SortDescriptions="{Binding Sort}"
              Filters="{Binding Filters}"
              CheckedRowKeys="{Binding Checked}"
              Density="{Binding Density}" />
```

### 分組與合計

`GroupBy` 接受欄的鍵，最外層的在前。分組列顯示一個展開或折疊切換、分組值、列數，以及每個彙總不為 `None` 的欄下的小計。應依不同值較少的欄分組；數字欄預設不可分組，設定 `IsGroupable="True"` 可允許對某一數字欄分組。

<Demo name="Ledger" />

Excel 匯出會把分組寫為帶大綱的列，並附帶 SUBTOTAL 公式，因此讀者在 Excel 中也能折疊它們。

### 勾選列與批次操作

在側欄中勾選列：Shift+按一下可勾選一段連續的列，Shift+Space 勾選選取範圍內的列。勾選列後，工具列會提供「複製」「匯出」、你的 `BulkActions`，以及「刪除」（需開啟 `CanDeleteRows`）。刪除會記錄列的位置，因此復原可將列依原順序放回；狀態列同樣會在 6 秒內提供復原。

```xml
<arc:DataGrid Label="帳戶">
    <arc:DataGrid.BulkActions>
        <arc:DataGridBulkAction Label="封存" Icon="{x:Static arc:ArcIcons.Archive}" Command="{Binding ArchiveCommand}" />
        <arc:DataGridBulkAction Label="移出計畫" IsDanger="True" Command="{Binding RemoveCommand}" />
    </arc:DataGrid.BulkActions>
</arc:DataGrid>
```

命令接收的是依資料順序排列的已勾選列，類型為 `IReadOnlyList<object>`。表格還會觸發 `BulkActionInvoked`，攜帶該操作和這些列。表格寬度低於 640 px 時標籤會隱藏，因此請為每個操作都提供圖示。

### 匯出

`ExportFormats` 可列出 `Csv`、`Xlsx` 或兩者（`All`）；選擇多種格式時，「匯出」會開啟一個選單。匯出會依檢視順序寫出篩選後保留的每一列；批次匯出只寫出已勾選的列。`Exporting` 可以重新命名或取消檔案，`ExportAsync` 則可從程式碼匯出。宿主無法顯示儲存對話方塊時，匯出被停用。

用 Excel 或 Sheets 開啟 CSV 時，以 `=`、`+`、`-` 或 `@` 開頭的文字會被當作公式執行，惡意的儲存格因此可以讀取資料或啟動程式（CSV 注入）。所以表格寫出這類文字時會在前面加一個單引號（`'=1+1`），單引號會顯示在儲存格裡。數字和日期不會加前綴，Excel 匯出則始終把文字存為文字。只有在檔案不會進入試算表、並且文字必須逐位元組往返時，才設定 `NeutralizeCsvFormulas="False"`。

```csharp
grid.Exporting += (_, e) => e.FileName = $"renewals-{DateTime.Today:yyyy-MM-dd}";
await grid.ExportAsync(DataGridExportFormat.Xlsx);
```

寫入器、剪貼簿輔助類別和解析器都是公開靜態類別（`DataGridCsv`、`DataGridXlsx`、`DataGridClipboardText`、`DataGridValueParser`、`DataGridFilterExpression`、`DataGridSorting`），伺服器端可以重複使用它們。

### 伺服器端資料

資料量過大、無法一次傳送時，將 `DataSource` 設為 `IDataGridDataSource`。表格會請求涵蓋可視列的資料區塊，在載入期間顯示預留位置列，並最多保留 40 個資料區塊。排序、篩選或搜尋變動時，它會重新開始並取消進行中的請求。此時表格為唯讀，不分組，隱藏「全選」，匯出時僅匯出已勾選的列。還需要設定 `RowKeySelector`，使用資料來源時它是必要的。

<Demo name="ServerData" />

### 載入與空白狀態

`IsLoading` 顯示六個預留位置列，合計列顯示「載入中」。當沒有任何列時，`EmptyMessage` 會取代內建的「No rows yet」（尚無列）。當篩選隱藏了所有列時，表格會說明這一點，並提供「清除篩選」。

<Demo name="States" />

### 繫結到檢視模型

在 MVVM 應用程式中，檢視模型負責管理列、檢視狀態和命令。使用 CommunityToolkit.Mvvm 時，列是 `ObservableCollection<DataGridRow>`，檢視狀態是可觀察屬性，表格會將變動寫回這些屬性。

```csharp
public partial class RenewalsViewModel : ObservableObject
{
    private readonly IRenewalService _renewals;

    public RenewalsViewModel(IRenewalService renewals)
    {
        _renewals = renewals;
    }

    public ObservableCollection<DataGridRow> Renewals { get; } = new();

    [ObservableProperty]
    private IReadOnlyList<DataGridSort> _sortDescriptions = [];

    [ObservableProperty]
    private IReadOnlyDictionary<string, DataGridFilter> _filters = new Dictionary<string, DataGridFilter>();

    [ObservableProperty]
    private IReadOnlyList<object> _checkedRowKeys = [];

    [ObservableProperty]
    private DataGridDensity _density = DataGridDensity.Standard;

    [RelayCommand]
    private async Task ArchiveAsync(IReadOnlyList<object> rows)
    {
        await _renewals.ArchiveAsync(rows.Cast<DataGridRow>().Select(r => r.Key));
    }
}
```

```xml
<arc:DataGrid Label="續約"
              ItemsSource="{Binding Renewals}"
              SortDescriptions="{Binding SortDescriptions}"
              Filters="{Binding Filters}"
              CheckedRowKeys="{Binding CheckedRowKeys}"
              Density="{Binding Density}">
    <arc:DataGrid.BulkActions>
        <arc:DataGridBulkAction Label="封存" Command="{Binding ArchiveCommand}" />
    </arc:DataGrid.BulkActions>
</arc:DataGrid>
```

- `SortDescriptions`、`Filters`、`CheckedRowKeys`、`Density`、`SearchText` 和 `GroupBy` 都是雙向的：表頭按一下會更新檢視模型，檢視模型中的變動也會更新表格。
- `ItemsSource` 是單向的。編輯會寫入列物件，表格對每次寫入都會觸發 `CellsChanged`。請在程式碼後置檔中的 `CellsChanged` 處理常式裡儲存變更，因為它是事件而非命令。

### 資料結構與效能

每一列都需要穩定的鍵（`DataGridRow.Key`，或用於強型別列的 `RowKeySelector`），且鍵不應改變，因為它用來識別已勾選的列和捲動目標。設計目標是：用戶端模式下約 10,000 列流暢，約 50,000 列可用；超過此規模時請使用 `DataSource`。數字存為 `decimal`，日期存為 `DateOnly`；對於屬於固定地區的數字，請設定 `Culture`。

### 範例：客戶成功團隊的續約審查

客戶成功經理審查未來兩個季度的續約。固定的客戶欄讓名稱始終可見。階段是帶有有序選項清單的 `Select`，因此排序會依銷售管線的順序進行。ARR 和區域為唯讀，因為帳務匯出負責這些資料；在此編輯會在下次匯入時被覆寫。`Min` 健康度合計顯示可視範圍內最弱的帳戶，而 `CanDeleteRows="False"` 則確保帳戶在帳務結案前不會被刪除。

```xml
<arc:DataGrid Label="2026 年第四季續約"
              MaxViewportHeight="440"
              ExportFormats="All"
              ExportFileName="renewals-q4-2026"
              CanDeleteRows="False">
    <arc:DataGridColumn Key="account" Header="客戶" Width="188" MinWidth="120" IsPinned="True" IsEditable="False" />
    <arc:DataGridColumn Key="owner" Header="負責人" Width="148" />
    <arc:DataGridColumn Key="region" Header="區域" Type="Select" Width="144" IsEditable="False" />
    <arc:DataGridColumn Key="stage" Header="階段" Type="Select" Width="140" Options="{Binding StageOptions}" />
    <arc:DataGridColumn Key="seats" Header="席位" Type="Number" Width="96" Aggregate="Sum" />
    <arc:DataGridColumn Key="arr" Header="ARR" Type="Currency" CurrencySymbol="$" Width="128" IsEditable="False" />
    <arc:DataGridColumn Key="health" Header="健康度" Type="Percent" Width="104" Aggregate="Min" />
    <arc:DataGridColumn Key="renewal" Header="續約" Type="Date" DateFormat="yyyy-MM-dd" Width="132" />
</arc:DataGrid>
```

### 文案規範

- **表頭**：句首大寫的簡短名詞片語，例如「續約」或「ARR」。當儲存格不顯示單位時，加上單位（「ARR（美元）」）。表頭是該欄的無障礙名稱，也是匯出時的欄標題。
- **標籤**：為開啟表格的人命名。它是無障礙名稱，也是預設的匯出檔名。
- **批次操作**：使用動詞，例如「封存」。破壞性操作使用 `IsDanger`；當記錄仍保留在其他系統中時，使用「移出計畫」。

| 不要寫 | 改為 |
|---|---|
| 金額 | ARR（美元） |
| 儲存檔案 | 匯出為 Excel |
| 錯誤 | ARR 不能為負數 |
| 立即全部刪除 | 封存已勾選的帳戶 |

### 最佳做法

**建議**

- 儲存原始值（decimal、`DateOnly`、選項字串），讓欄負責格式化，這樣排序、合計和匯出都與使用者看到的一致。
- 為每一列設定唯一且穩定的值作為鍵。排序或刪除會移動列，只有穩定的鍵才能讓勾選始終對應正確的列。
- 固定用於識別列的欄，通常是名稱，使其在表格橫向捲動時仍然可見。
- 將匯入或計算得到的欄標記為 `IsEditable="False"`，而不是指望使用者不去修改它們。
- 在 `CellEditEnding` 中用具體的訊息進行驗證。被拒絕的編輯會保留編輯器，讓使用者就地修正。

**避免**

- 對幾百列資料使用 `DataSource`。它會使表格變為唯讀並停用分組，而用戶端模式在這個規模下是支援分組的。
- 在數值到達表格之前將其格式化為字串。字串會依文字排序，而且合計會忽略它。
- 同一畫面上有多個帶批次操作的可編輯表格。每個表格都有各自的勾選集合，使用者很難分辨批次操作會影響哪一個。

### 常見問題

**為什麼我修改了已排序的值後，列仍留在原位？** 編輯不會重新排序或重新篩選，因此列會保持位置，直到排序或篩選條件改變。再次按一下表頭，或修改篩選，即可重新排序。

**如何在程式碼中復原批次刪除？** 呼叫 `Undo()`，它會還原上一次編輯、填滿、貼上、清除或刪除列的操作。`CanUndo` 表示是否存在可復原的步驟，`ClearHistory()` 會清除所有步驟，例如在儲存成功後呼叫。`UndoLimit` 設定保留的步數。

## API 參考

<!-- api: DataGrid -->

<!-- api: DataGridColumn -->

<!-- api: DataGridBulkAction -->

<!-- api: DataGridCellRenderer -->

<!-- api: BadgeRenderer -->

<!-- api: ProgressRenderer -->

<!-- api: LinkRenderer -->

<!-- api: IconTextRenderer -->

<!-- api: CheckboxRenderer -->

<!-- api: DataGridCellAction -->

<!-- api: ActionsRenderer -->

<!-- api: DataGridEditContext -->

<!-- api: RequiredRule -->

<!-- api: RangeRule -->

<!-- api: MaxLengthRule -->

<!-- api: PatternRule -->

<!-- api: DataGridRule -->

<!-- api: DataGridFormatRule -->

<!-- api: DataGridCellContext -->

<!-- api: DataGridMenuItem -->

<!-- api: DataGridCellEventArgs -->

<!-- api: DataGridContextMenuOpeningEventArgs -->

<!-- api: DataGridLayoutState -->

<!-- api: DataGridFilterState -->

<!-- api: DataGridCellRenderContext -->

<!-- api: DataGridCellPointerContext -->

<!-- api: DataGridRenderPalette -->

<!-- api: DataGridCellInvokedEventArgs -->

伺服器端資料來自 `IDataGridDataSource`。表格會為每個需要的資料區塊呼叫 `GetRowsAsync`。

| 成員 | 說明 |
|---|---|
| `int BlockSize` | 每次請求的列數。預設為 100。 |
| `Task<DataGridRowsResult> GetRowsAsync(DataGridRowsRequest request, CancellationToken cancellation)` | 傳回一個資料區塊，其中包含已排序、已篩選和已搜尋的列。 |

`DataGridRowsRequest` 包含 `Start`（含）、`End`（不含）、`Sort`、`Filters` 和 `Search`。結果包含 `Rows`、`RowCount`（整個篩選後的集合）和 `Totals`（依欄的鍵；為 null 時保留先前的合計）。

## 無障礙

- 表格本體是一個可取得焦點的元素。方向鍵、Shift 加方向鍵、Home、End、PageUp 和 PageDown 用於移動和擴大選取範圍；Tab 離開表格。
- 本體的無障礙名稱跟隨作用中儲存格（「欄，列：值」），其說明文字給出位址，因此螢幕閱讀器會播報每次移動。Avalonia 沒有表格自動化模式，因此列和儲存格不是獨立節點。
- 編輯、排序、勾選、複製、貼上、填滿、復原和重做都會在禮貌型即時區域中播報。
- 表頭排序按鈕會說明方向和優先順序；全選方塊是三態核取方塊。
- 直接鍵入會以輸入的文字開始編輯。對於中文或日文等輸入法，請先按 Enter、F2 或連按兩下，然後在編輯器中組字。
- Narrator、NVDA 和 VoiceOver 尚未針對此控制項測試；請在每個平台上驗證播報內容。

### 鍵盤

表格本體是焦點停留點。以下按鍵在未開啟編輯器時生效；在 macOS 上 Ctrl 即 Cmd。

| 按鍵 | 操作 |
|---|---|
| 方向鍵 | 移動作用中儲存格，並將選取範圍收合到該儲存格 |
| `Shift` + 方向鍵 | 從錨點儲存格擴大選取範圍 |
| `Ctrl` + 方向鍵 | 跳到該方向上的第一列或最後一列、第一欄或最後一欄；加上 Shift 可擴大選取範圍 |
| `Home` / `End` | 移到該列的第一欄或最後一欄；`Ctrl+Home` 與 `Ctrl+End` 移到第一個和最後一個儲存格 |
| `Ctrl+Space` | 將選取範圍擴大到整欄 |
| `Shift+Space` | 勾選選取範圍內的列；若全部已勾選，則取消勾選 |
| `Enter` / `F2` | 編輯作用中儲存格並保留其值 |
| 可列印字元 | 開始編輯，並以輸入的文字取代值 |
| `Alt+Down` | 在 `Select` 或 `Date` 欄上開啟選項清單或日曆 |
| `Alt+Left` / `Alt+Right` | 在分組表格中，折疊或展開作用中列所在的分組 |
| `F8` | 選取下一個被驗證標出的儲存格 |
| `Ctrl+Shift+加號` | 在作用列上方插入一列（需要 `CanInsertRows`） |
| `Shift+F10` / 功能表鍵 | 在作用中儲存格上開啟右鍵選單 |
| `Escape` | 將多儲存格選取範圍收合為作用中儲存格；否則清除列的勾選 |
| `Delete` / `Backspace` | 清除選取範圍；唯讀儲存格會被略過 |
| `Ctrl+D` | 從選取範圍的第一列向下填滿 |
| `Ctrl+F` / `Ctrl+H` | 開啟尋找，或尋找與取代 |
| `F3` / `Shift+F3` | 下一個或上一個相符項目 |
| `Ctrl+C` / `Ctrl+X` / `Ctrl+V` | 複製、剪下和貼上 |
| `Ctrl+Z` / `Ctrl+Shift+Z` 或 `Ctrl+Y` | 復原和重做 |
| `Tab` / `Shift+Tab` | 離開表格 |

編輯器開啟時，Enter 與 Shift+Enter 送出並向下或向上移動，Tab 與 Shift+Tab 送出並向右或向左移動，Escape 取消。在表頭上，Alt+Left 與 Alt+Right 以 16 px 為步長縮窄和加寬該欄，Alt+Shift+Left 與 Alt+Shift+Right 移動該欄。

## 動態效果

- 排序之後，列會在 700 ms 內滑動到新位置；從可視範圍外進入的列會淡入。

| 變化 | 完整 | 減少 | 關閉 |
|---|---|---|---|
| 選取範圍與作用中儲存格 | 在範圍之間彈簧式移動 | 直接跳到新範圍 | 直接跳到新範圍 |
| 排序後的列 | 在 700 ms 內滑到新位置 | 直接跳到新位置 | 直接跳到新位置 |
| 貼上、填滿、復原或重做的儲存格 | 0.9 秒的高亮波紋，之後淡出 | 短暫淡入 | 立即完成 |
| 工具列批次狀態與狀態訊息 | 模糊並上下切換 | 僅淡入 | 立即完成 |
| 合計、選取統計與已勾選數量 | 數字滾動 | 依計數規則滾動數字 | 立即完成 |
| 篩選列與分組箭頭 | 彈簧展開，箭頭旋轉 | 高度直接變化，不透明度淡入 | 立即完成 |

## 相關元件

| 元件 | 適用情境 |
|---|---|
| `Table` | 記錄是唯讀的，使用者需要對其排序、選取和翻頁 |
| `Input` / `Select` / `DatePicker` | 使用者一次編輯一筆記錄，並需要逐一欄位驗證 |
| `Card` / `ReorderableList` | 每個項目都有自己的內容、圖片或操作 |
| `TreeView` | 資料是階層式的，使用者需要展開分支 |
| `BarChart` / `MetricCard` | 重點是數字或趨勢，而不是個別的列 |
