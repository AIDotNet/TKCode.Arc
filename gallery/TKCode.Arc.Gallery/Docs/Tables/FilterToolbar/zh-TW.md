---
title: 篩選工具列
summary: 讓集合篩選條件保持在近處，並方便重設。
source-hash: c9a11d7afb0d
---

<Demo name="Filters" />

## 何時使用

`FilterToolbar` 是位於表格或清單上方的一條工具列，它顯示哪些篩選條件正在縮小資料列的範圍，讓使用者逐一移除它們，並能一起重設。它以可移除的篩選標籤呈現已套用的篩選，提供新增下一個篩選的「新增篩選」選單，還可包含一個可選的搜尋欄位以及頁面所需的任何操作。如此清單頁面就只有一個一致的頁首，而不是一組零散的下拉選單。

- **訂單清單中的分面篩選**：出貨團隊按狀態、承運商與地區篩選訂單，看到「狀態：已出貨」與「地區：EU」以標籤呈現，換班時用一個操作即可全部清除。
- **事件與工單佇列**：值班檢視按嚴重程度、服務與負責人篩選事件。當有人在審查中分享連結或螢幕截圖時，篩選標籤讓目前的檢視一目了然。
- **發票與帳單清單**：財務人員按狀態與客戶篩選發票，然後透過放在「新增篩選」觸發器旁邊的操作，匯出篩選後的資料列。
- **客戶與 CRM 表格**：搜尋方塊與兩三個分面協同運作的情境。搜尋縮小文字範圍，篩選標籤縮小區段，下方的表格顯示剩餘的內容。
- **窄視窗中的高密度管理主控台**：寬度低於 520 px 時，篩選標籤堆疊在操作之上，因此同一個工具列無需另行配置即可用於側邊面板。

## 何時不用

- **以開關形式呈現的一組固定選項**，例如清單的三種檢視（開啟、關閉、全部）：用 `ChipGroup`，它會同時顯示所有選項，無需選單。
- **沒有分面、只有自由文字搜尋**：用 `SearchField`。工具列的搜尋方塊是為了與篩選條件並列而存在的；沒有篩選的頁面不會從篩選標籤中獲得任何好處。
- **需要接受多個值的表單欄位**，例如編輯器中任務的負責人：用 `MultiSelect`。篩選工具列將值套用於清單；表單欄位則將值儲存到記錄中。
- **從長清單中選擇一個值**，例如從數千名客戶中選擇一位：用附有自身搜尋的 `Combobox`。「新增篩選」選單會列出每個選項，因此只適合較短的清單。
- **對表格的資料列進行排序**：使用 `SortableDataTable` 的欄標題。篩選會移除資料列，排序則決定資料列的順序，兩者應放在不同的位置。

## 用法

每個篩選都是一個 `FilterChip`，包含 `Id`、`Label` 與 `Value`。「新增篩選」選單的欄位放入 `Fields`。每個欄位都有選項，使用者在第二步中進行選擇。

```xml
<arc:FilterToolbar Label="目前篩選"
                   ActiveFilters="{Binding Filters}"
                   MenuAlignment="Start">
    <arc:FilterToolbar.Fields>
        <arc:FilterField Id="status" Label="狀態">
            <arc:FilterField.Options>
                <arc:FilterOption Value="待處理" />
                <arc:FilterOption Value="處理中" />
                <arc:FilterOption Value="已關閉" />
            </arc:FilterField.Options>
        </arc:FilterField>
        <arc:FilterField Id="owner" Label="負責人">
            <arc:FilterField.Options>
                <arc:FilterOption Value="Mina" />
                <arc:FilterOption Value="Theo" />
            </arc:FilterField.Options>
        </arc:FilterField>
    </arc:FilterToolbar.Fields>
</arc:FilterToolbar>
```

`FilterToolbar` 衍生自 Avalonia 的 `TemplatedControl`，並且是輔助技術中的一個群組。它的組成部分包括標籤區域、搜尋方塊、「新增篩選」觸發器、「全部清除」按鈕以及任何操作，並排在同一列中。工具列將已套用的篩選保存在 `ActiveFilters` 中，該屬性為雙向繫結，因此你所繫結的清單是唯一的資料來源，篩選標籤只是它的檢視。

### 結構

1. **標籤區域**（`ActiveFilters`）：每個已套用的篩選對應一個標籤，順序與清單一致。標籤顯示名稱、中間點與值，例如「狀態 · 待處理」。沒有 `Value` 的標籤只顯示名稱。
2. **標籤移除按鈕**：每個標籤末端有一個 24 px 的點擊區域，內含 14 px 的關閉圖示。它的名稱是標籤的文字，例如「移除 狀態：待處理」。標籤帶有 1 px 邊框、弱化的表面色與膠囊形圓角，彼此相距 8 px。
3. **空狀態提示**（`EmptyText`）：沒有設定任何篩選時，標籤區域起始端顯示弱化文字顏色的「未套用任何篩選」。最後一個標籤移除時，它會升起出現。
4. **搜尋方塊**（`IsSearchVisible`、`SearchText`、`SearchPlaceholder`）：寬 200 px，高度與小尺寸控制項相同，帶有搜尋圖示、1 px 邊框與控制項圓角。取得焦點時會繪製較深的邊框顏色與焦點環。
5. **新增篩選觸發器**（`AddFilterLabel`）：一個加號圖示與中等字重的文字，高度至少與小尺寸控制項相同。當 `Fields` 至少有一個欄位時出現，點選後開啟兩步驟選單。
6. **操作**（`Actions`）：你新增的任何控制項，放在「新增篩選」觸發器之後，與其他操作相距 12 px。
7. **全部清除**（`ClearAllLabel`）：弱化文字顏色的幽靈按鈕。只有在設定了任一篩選時才會出現。
8. **框體**：表面色背景、1 px 邊框、面板圓角，水平內距 16 px，垂直內距 12 px。標籤區域與操作之間相隔 16 px。

寬度低於 520 px（堆疊斷點）時，工具列會堆疊：標籤佔據第一列，操作移到第二列並靠末端對齊。

「新增篩選」選單會開啟一個寬 256 px（16 rem）的面板，內距為 6 px。欄位與值清單的每一列高 36 px，標題列高 32 px。高度超過 320 px 的值清單會捲動。將 `MenuAlignment` 設為 `Start` 或 `End`，可選擇面板與觸發器對齊的邊緣；空間不足時，面板會翻轉到另一側。

### 以程式碼設定篩選

將目前生效的篩選建立為 `FilterChip` 清單。每個欄位只保留一個標籤：新增與既有標籤 `Id` 相同的標籤時，會取代原有的標籤。

```csharp
var toolbar = new FilterToolbar
{
    ActiveFilters = new ObservableCollection<FilterChip>
    {
        new("status", "狀態", "待處理"),
        new("owner", "負責人", "Mina")
    }
};
toolbar.AddFilter(new FilterChip("priority", "優先順序", "高"));
toolbar.RemoveFilter("owner");
toolbar.ClearFilters();
```

`AddFilter`、`RemoveFilter` 與 `ClearFilters` 是工具列自身的操作，它們會觸發 `FiltersChanged`。當沒有標籤的 id 與之相符時，`RemoveFilter` 不會執行任何動作；`AddFilter` 新增完全相同的標籤時也不會執行任何動作。

### 回應變更

`FiltersChanged` 會回報變更的內容（`Kind`）、涉及的標籤，以及全部已套用的篩選。用它重新載入工具列所篩選的資料。

```csharp
toolbar.FiltersChanged += (_, e) => Refilter(e.ActiveFilters);
```

`FilterChangeKind` 有四個值：`Added`、`Changed`、`Removed` 與 `Cleared`。當欄位取得新值時觸發 `Changed`，`Previous` 保存被取代的標籤。當變更來自「新增篩選」選單時，會設定 `Field`。

此事件針對透過工具列所做的變更觸發：篩選標籤的移除按鈕、全部清除、「新增篩選」選單，以及上述三個操作。當你的程式碼直接修改 `ActiveFilters` 清單時，它不會觸發。篩選標籤仍會更新，變更也會被播報，因此對於直接編輯清單的情況，應改為訂閱清單的 `CollectionChanged`。

### 搜尋

`IsSearchVisible` 會在工具列中新增搜尋方塊。`SearchText` 為雙向繫結，因此表格可以對其作出回應。

<Demo name="SearchAndTable" />

```xml
<arc:FilterToolbar IsSearchVisible="True"
                   SearchPlaceholder="搜尋問題"
                   SearchText="{Binding Query, Mode=TwoWay}" />
```

`SearchPlaceholder` 設定搜尋方塊為空時顯示的提示；預設值為「搜尋」。搜尋不是篩選標籤，因此不會出現在 `ActiveFilters` 中，也不屬於 `FiltersChanged` 事件。資料列的篩選應同時基於 `SearchText` 與篩選標籤。

### 額外操作

`Actions` 可將其他控制項（例如匯出按鈕）放在「新增篩選」觸發器旁邊。

```xml
<arc:FilterToolbar>
    <arc:FilterToolbar.Actions>
        <Button Content="匯出" />
    </arc:FilterToolbar.Actions>
</arc:FilterToolbar>
```

### 沒有篩選

沒有生效的篩選時，空狀態提示位於起始端。將 `MenuAlignment` 設為 `Start`，可使選單與觸發器的起始邊緣對齊。

<Demo name="NoFilters" />

### 窄寬度

寬度低於 520px 時，工具列會堆疊：先是篩選標籤，然後是靠末端對齊的操作。

<Demo name="Narrow" />

### 單獨使用篩選選單

`FilterMenu` 就是「新增篩選」觸發器及其兩步驟面板。在觸發器上按 Down 會在第一個欄位上開啟選單，Right 進入欄位，Left 返回。

<Demo name="FilterMenu" />

`FilterMenu` 也可以脫離工具列使用。它接受自己的 `Fields`，透過 `ActiveFilters` 顯示每個欄位已有的值，並在選取某個值時觸發 `FilterSelected`。該事件中的標籤帶有欄位的 `Id`，因此你的處理常式可以使用工具列所用的同一 id 取代規則來套用它。

### 繫結到檢視模型

在 MVVM 應用程式中，檢視模型擁有已套用的篩選、搜尋文字以及它們所縮小的資料列。工具列編輯的是它所取得的清單，因此檢視模型始終是唯一的資料來源。使用 CommunityToolkit.Mvvm 時：

```csharp
public partial class InvoicesViewModel : ObservableObject
{
    public InvoicesViewModel()
    {
        ActiveFilters.CollectionChanged += (_, _) => ApplyFilters();
    }

    public ObservableCollection<FilterChip> ActiveFilters { get; } = new();

    [ObservableProperty]
    private string? _searchText;

    partial void OnSearchTextChanged(string? value) => ApplyFilters();

    [RelayCommand]
    private Task ExportAsync() =>
        File.WriteAllLinesAsync("invoices.csv", VisibleInvoices.Select(invoice => invoice.Id));

    private void ApplyFilters()
    {
        // 根據 ActiveFilters 與 SearchText 篩選資料列，然後重新整理 VisibleInvoices。
    }
}
```

```xml
<StackPanel Spacing="16">
    <arc:FilterToolbar ActiveFilters="{Binding ActiveFilters}"
                       IsSearchVisible="True"
                       SearchPlaceholder="搜尋發票"
                       SearchText="{Binding SearchText}">
        <arc:FilterToolbar.Fields>
            <arc:FilterField Id="status" Label="狀態">
                <arc:FilterOption Value="草稿" />
                <arc:FilterOption Value="已寄出" />
                <arc:FilterOption Value="已逾期" />
            </arc:FilterField>
        </arc:FilterToolbar.Fields>
        <arc:FilterToolbar.Actions>
            <arc:Button Content="匯出 CSV" Variant="Secondary" Command="{Binding ExportCommand}" />
        </arc:FilterToolbar.Actions>
    </arc:FilterToolbar>
    <arc:SortableDataTable ItemsSource="{Binding VisibleInvoices}" />
</StackPanel>
```

- `ActiveFilters` 與 `SearchText` 都是雙向繫結，這也是它們的預設值。在工具列中移除的標籤會更新集合，檢視模型透過 `CollectionChanged` 即可得知。
- `ActiveFilters` 也接受唯讀清單（例如陣列）。此時工具列會以一個新清單取代它並寫回，因此當檢視模型需要回應每一次變更時，應使用 `ObservableCollection`。
- 工具列中的 `Fields` 是唯讀集合，因此應在 XAML 中定義欄位，或在程式碼中透過 `toolbar.Fields.Add(...)` 新增一次。它們不與檢視模型繫結。
- 只有當變更需要事件的 `Kind` 或 `Previous` 時，才在程式碼後置中訂閱 `FiltersChanged`。對於單純的重新篩選，上面的 `CollectionChanged` 處理常式已經足夠，因為它也涵蓋工具列不會回報的變更。

### 資料結構與效能

選單會列出欄位的每一個選項。選項不做虛擬化，因此應保持每個值清單簡短，大約幾十個值。擁有數百個值的欄位應使用附有自身搜尋的 `Combobox`，工具列只顯示所選的值。

標籤保存選項的文字：選項有 `Label` 時取其 `Label`，否則取 `Value`；標籤的 `Value` 即為該文字。應依標籤的值進行篩選，因此要讓文字與查詢所用的資料保持一致。當使用者閱讀的文字必須與資料不同時，應在檢視模型中將標籤的 `Id` 與 `Value` 對應到查詢。

`FilterOption.Hint` 位於列尾，適合顯示「開啟 4」之類的計數。

### 焦點與關閉

「新增篩選」選單會保持開啟，直到使用者選擇一個值或按下 Escape。無論是透過 Escape 關閉，還是選取某個值之後關閉，面板關閉時觸發器都會重新取得焦點。

移除標籤時，焦點會移到下一個標籤的移除按鈕；如果被移除的是最後一個標籤，則移到前一個標籤的移除按鈕。最後一個標籤被移除後，若「新增篩選」觸發器可見，焦點會移到它上面。

### 範例：帳務主控台中的發票清單

財務團隊審查逾期發票。已套用兩個篩選，「新增篩選」觸發器已準備好新增下一個分面。因為至少設定了一個篩選，所以「全部清除」會出現。工具列下方的表格顯示同時符合兩個標籤的資料列；團隊開始新一輪審查時，只需一步即可清除所有標籤。

![帶有狀態與負責人篩選標籤以及新增篩選觸發器的篩選工具列](images/InvoiceFilters.png)

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc"
             xmlns:collections="using:System.Collections.ObjectModel">
    <StackPanel Width="600">
        <arc:FilterToolbar>
            <arc:FilterToolbar.ActiveFilters>
                <collections:ObservableCollection x:TypeArguments="arc:FilterChip">
                    <arc:FilterChip Id="status" Label="Status" Value="Overdue" />
                    <arc:FilterChip Id="owner" Label="Owner" Value="Mina" />
                </collections:ObservableCollection>
            </arc:FilterToolbar.ActiveFilters>
            <arc:FilterField Id="status" Label="Status" Icon="{x:Static arc:ArcIcons.CircleDot}">
                <arc:FilterOption Value="Draft" Hint="3" />
                <arc:FilterOption Value="Sent" Hint="18" />
                <arc:FilterOption Value="Overdue" Hint="6" />
            </arc:FilterField>
            <arc:FilterField Id="owner" Label="Owner" Icon="{x:Static arc:ArcIcons.User}">
                <arc:FilterOption Value="Mina" />
                <arc:FilterOption Value="Theo" />
                <arc:FilterOption Value="Ana" />
            </arc:FilterField>
            <arc:FilterField Id="region" Label="Region" Icon="{x:Static arc:ArcIcons.Flag}">
                <arc:FilterOption Value="EU" />
                <arc:FilterOption Value="US" />
                <arc:FilterOption Value="APAC" />
            </arc:FilterField>
        </arc:FilterToolbar>
    </StackPanel>
</UserControl>
```

每個欄位只有一個標籤，因此選擇另一位負責人會取代標籤，而不是新增第二個。欄位選項帶有用於狀態計數的 `Hint`，因此選單會在團隊挑選之前，就顯示每種狀態下有多少張發票。

### 文案規範

- **標籤**（欄位名稱）：與其所篩選的欄對應的簡短名詞，句首大寫：「狀態」「負責人」「地區」。不要使用動詞，也不要重複頁面標題。
- **值**：使用者在表格中認得的文字：「已逾期」「Mina」「EU」。與資料使用相同的拼寫。
- **新增篩選**：保留預設值，或使用描述該操作的動詞片語。「新增篩選」是預設值，適用於大多數頁面。
- **空狀態提示**：用平實的文字說明情況：「未套用任何篩選」，而不是「沒有內容可顯示」。
- **搜尋提示**：說明搜尋的對象：「搜尋發票」「依名稱搜尋客戶」。

| 不要寫 | 改為 |
|---|---|
| 篩選 | 狀態 |
| 選擇一個值... | 搜尋發票 |
| 清除 | 全部清除 |
| 0 個篩選 | 未套用任何篩選 |
| STATUS = OVERDUE | 狀態：已逾期 |

### 最佳做法

**建議**

- 讓篩選標籤作為過濾資料列的紀錄。從 `ActiveFilters` 與 `SearchText` 推導資料列，使重新載入、保存的檢視或分享的連結所顯示的結果，與標籤所描述的一致。
- 為每個欄位設定一個 `Id`，並在各版本之間保持不變。id 決定新值取代哪個標籤。
- 當搜尋方塊與篩選標籤縮小的是同一批資料列時，把它們放在同一個工具列中。使用者會將它們視為同一組篩選。
- 保持值清單簡短，並把最有用的值放在最前面。

**避免**

- 新增不會改變結果的篩選，例如只有一個選項的欄位。標籤會留在畫面上，卻不能向使用者傳達任何資訊。
- 在工具列旁邊再新增一個「全部清除」按鈕。工具列在設定了任一篩選時已會顯示自己的按鈕，兩個重設按鈕會讓使用者分不清哪一個會清除資料列。
- 從背景執行緒修改 `ActiveFilters`。控制項必須在 UI 執行緒上變更，因此應先用 `Dispatcher.UIThread` 將變更切換到 UI 執行緒。
- 依賴 `FiltersChanged` 處理直接編輯清單的情況，因為直接編輯不會觸發它。應改為從清單的 `CollectionChanged` 重新篩選。

### 常見問題

**為什麼「新增篩選」觸發器不顯示？** 只有當 `Fields` 至少有一個欄位時才會顯示。請在 XAML 中新增欄位，或在工具列顯示之前於程式碼中用 `Fields.Add` 新增。

**如何為已儲存的檢視預先選取篩選？** 在檢視開啟時，於 XAML 或檢視模型中將 `ActiveFilters` 設為已儲存的標籤。初始值不會觸發任何事件，因此應在同一步驟中對資料列套用相同的篩選。

**為什麼選擇第二個值會取代標籤，而不是新增一個？** 每個標籤的 id 就是欄位的 id，工具列對每個欄位只保留一個標籤。為已有標籤的欄位選擇一個值會變更該標籤，`FiltersChanged` 會以 `Kind` 為 `Changed`、`Previous` 為舊標籤的方式回報此變更。

**如何顯示沒有值的篩選？** 建立不帶 `Value` 的標籤，例如 `new FilterChip("archived", "Archived")`。標籤只顯示名稱，移除按鈕也以名稱命名。

**如何在程式碼中重設所有篩選？** 呼叫 `ClearFilters()`，它會以 `Kind` 為 `Cleared` 觸發 `FiltersChanged`。直接用 `ActiveFilters.Clear()` 清空清單會更新標籤，但不觸發事件，因此應選擇與你的處理常式所需相符的呼叫方式。

## API 參考

<!-- api: FilterToolbar -->

<!-- api: FilterChip -->

<!-- api: FilterField -->

## 無障礙

- 工具列預設是一個名為「目前篩選」的群組。設定 `Label` 可為其指定適合頁面的名稱。
- 每個篩選標籤都有一個移除按鈕，名稱形如「移除 狀態：待處理」。移除後，焦點會移到相鄰的標籤，或「新增篩選」觸發器。
- 新增與移除篩選會在狀態區域中播報。
- 「新增篩選」選單是一個帶名稱的對話方塊，其選項為可勾選的選單項目。
- 按 Down 或 Up 可從觸發器開啟選單。方向鍵在清單中移動，Right 進入欄位的值，Left 返回，輸入字元可跳到對應選項。
- Escape 關閉選單，並將焦點返回到觸發器。

### 鍵盤

| 按鍵 | 操作 |
|---|---|
| `Tab` / `Shift+Tab` | 在篩選標籤的移除按鈕、搜尋方塊、「新增篩選」觸發器、操作與「全部清除」之間移動 |
| 標籤的移除按鈕上的 `Enter` 或 `Space` | 移除該篩選；焦點移到相鄰的標籤或「新增篩選」觸發器 |
| 搜尋方塊中的 `Escape` | 搜尋文字不為空時將其清空 |
| 「新增篩選」觸發器上的 `Enter`、`Space` 或 `Down` | 在第一個欄位上開啟選單 |
| 「新增篩選」觸發器上的 `Up` | 在最後一個欄位上開啟選單 |
| 選單中的 `Down` / `Up` | 移到下一列或上一列；清單會循環 |
| 選單中的 `Home` / `End` | 移到第一列或最後一列 |
| 欄位上的 `Right` | 顯示該欄位的值 |
| 值清單中的 `Left` | 返回欄位 |
| 值上的 `Enter` 或 `Space` | 套用該值並關閉選單 |
| 選單中輸入字母 | 跳到標籤以所輸入字母開頭的下一列 |
| 選單中的 `Escape` | 關閉選單，並將焦點返回到觸發器 |

## 動態效果

- 標籤在自己的位置展開與收合，相鄰的標籤滑動讓出空間。
- 值變更時以柔和的模糊在原位切換。設定了任一篩選時，「全部清除」即會顯示。
- 「新增篩選」觸發器會展開為選單面板，欄位與值的步驟則朝各自的方向滑入。
- 減少動態效果時，變更立即切換，並伴隨約 160 ms 的短暫淡入淡出。關閉動態效果時直接到最終狀態。

| 變化 | 完整 | 減少 | 關閉 |
|---|---|---|---|
| 新增篩選 | 其標籤在彈性動畫中展開出一個位置，之後的標籤滑動讓出空間 | 約 160 ms 的短暫淡入淡出 | 立即 |
| 移除篩選 | 其位置收合，之後的標籤滑動回位 | 約 160 ms 的短暫淡入淡出 | 立即 |
| 值變更 | 值在原位以柔和的模糊切換 | 約 160 ms 的短暫淡入淡出 | 立即 |
| 「全部清除」出現或消失 | 其位置展開或收合 | 約 160 ms 的短暫淡入淡出 | 立即 |
| 選單開啟 | 觸發器展開為面板，開啟時帶有輕微的過衝，關閉時平滑 | 約 160 ms 的短暫淡入淡出 | 立即 |
| 從欄位到值清單 | 面板沿行進方向滑動 8 px | 約 160 ms 的短暫淡入淡出 | 立即 |

## 相關元件

- `ChipGroup`：一小組固定的切換項目。
- `SearchField`：不帶篩選的自由文字搜尋。
- `SortableDataTable`：被篩選縮小範圍的表格。
- `MultiSelect`：需要接受多個值的表單欄位。

| 元件 | 適用情境 |
|---|---|
| `ChipGroup` | 選項是一小組固定的值，使用者在其間切換，且全部可見 |
| `SearchField` | 頁面只搜尋文字，沒有需要縮小範圍的分面 |
| `SortableDataTable` | 篩選後剩餘的資料列以表格形式顯示並可排序 |
| `MultiSelect` | 表單在一筆記錄上儲存多個值 |
| `Combobox` | 必須從需要自身搜尋的長清單中選擇一個值 |
