---
title: 下拉選單
summary: 錨定在觸發器上的聚焦操作清單。
source-hash: b709b001eb3d
---

<Demo name="Actions" />

## 何時使用

下拉選單把一小組指令收納在帶標籤的觸發器之後。觸發器是有名稱的可見控制項，人們無需手勢就能找到這些操作；選取之後選單會關閉，頁面保持原位。它適用於一個物件或一個檢視有多個指令，而這些指令又不值得各自佔一個按鈕的情境。

- **資料表格中的列操作**：在 `Table` 中，發票或客戶列的「更多」按鈕之後放著「重新命名」「複製」「封存」與「刪除」。每列一個觸發器，破壞性操作放在最後，並以分隔線與其他操作隔開。
- **管理後台頁首中的操作**：報表頁面的「匯出」「分享」與「列印」。頁面為次要指令保留一個可見入口，主要操作則以按鈕的形式保留在旁邊。
- **清單工具列中的排序與檢視選項**：訂單清單工具列中，「最新」「最舊」或「名稱」以單選列呈現，「檢視選項」以核取列呈現。觸發器標籤顯示目前的選擇，例如「排序：最新」。
- **一次造訪中會切換的顯示設定**：「顯示側邊欄」「顯示縮圖」與「自動換行」以核取列呈現，並保持選單開啟，因此可以一次變更多個選項，而不必重新開啟選單。
- **帳單或報表頁面中的匯出與分享**：「匯出 CSV」「匯出 PDF」與「傳送到會計系統」歸為一組，不可逆的操作放在最後。

## 何時不用

- **有預設操作與少數替代項**：用 `SplitButton`。預設操作位於主按鈕上，選單只放較少用的選擇，因此主要操作一按即達。
- **針對某段內容的右鍵操作**：用 `ContextMenu`，它在指標處開啟、作用於內容本身，沒有可見的觸發器。
- **頭像背後包含身分與主題的帳戶選單**：用 `UserMenu`，它帶有個人資料標頭、主題切換與登出。
- **在表單中從清單選取一個值**：用 `Select` 或 `Combobox`。它們持有值、標籤與驗證狀態，選單則沒有這些。
- **始終可見、需要並排比較的選項**：少量互斥的檢視用 `SegmentedControl`，彼此獨立的選項用 `Checkbox` 群組。選單會在開啟前隱藏選項。

## 用法

`DropdownMenu` 衍生自 Avalonia 的 `TemplatedControl`。它內建的觸發器是 Arc `Button`（Secondary，Small），其選單列位於它自行建立並持有的 `MenuSurface` 中。控制項負責標籤變形、箭頭、鍵盤模型與動態效果；選單列與其他 Arc 選單使用相同的 `MenuEntry` 項目。直接在列內宣告選單列，並把 `Label` 設為觸發器上顯示的文字。

直接宣告選單列。指令用 `MenuEntry`，分隔線用 `MenuEntrySeparator`，群組標題用 `MenuEntryLabel`。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:DropdownMenu Label="操作">
        <arc:MenuEntry Header="重新命名" />
        <arc:MenuEntry Header="複製" />
        <arc:MenuEntry Header="刪除"
                       IsDestructive="True"
                       HasSeparatorBefore="True" />
    </arc:DropdownMenu>
</UserControl>
```

透過程式碼以 `ArcIcons` 設定圖示，它是 24x24 的 Lucide 幾何圖形。

```csharp
var rename = (MenuEntry)menu.Items[0];
rename.Icon = ArcIcons.Pencil;
```

### 結構

1. **觸發器**（內建的 `arc:Button`，或 `Trigger` 中的控制項）：高 36 px，即 Secondary 小號按鈕。懸停與開啟狀態會把邊框變為強邊框顏色；開啟狀態還會使用淡化的表面色。按下時觸發器從不縮放。
2. **前導圖示**（`Icon`）：16 px 的圖形，使用次要文字顏色。`Icon` 為 null 時隱藏。
3. **標籤**（`Label`）：觸發器的文字，超出最大寬度時以省略號截斷。變更它時觸發器會變形：新標籤向上浮現，觸發器寬度則以彈簧方式調整至合適的寬度。
4. **箭頭**：15 px，描邊寬 1.8 px，使用淡化文字顏色。選單開啟時旋轉 180 度。
5. **選單面板**：寬度至少 192 px（12 rem），與觸發器相距 6 px。`Alignment` 決定與哪一邊對齊，預設為 `End`。`Side` 決定從哪一邊展開：預設為 `Bottom`，也可為 `Top`、`Left` 或 `Right`。空間不足時選單會翻轉到另一側。
6. **選單列**（`MenuEntry`）：下拉變體中高 36 px。一列顯示圖示、標題、末端可選的快捷鍵提示，以及切換列的勾選或圓點。
7. **分隔線與分組標籤**（`HasSeparatorBefore`、`MenuEntrySeparator`、`MenuEntryLabel`）：列上方的一條線，或一個不可選取的標題，用來為其下的列分組。

對於標準的操作群組，保留內建觸發器。當觸發器必須是圖示按鈕、清單列的「更多」按鈕，或頁面上已有的控制項時，改用 `Trigger`；選單隨即錨定到該控制項。

### 選擇觸發器、方向與對齊

- 當操作群組需要一個名稱時，使用帶文字 `Label` 的內建觸發器，例如「操作」或「排序：最新」。可見文字在選單開啟前就告訴鍵盤使用者它的用途。
- 列內的「更多」選單使用幽靈按鈕與省略號圖示作為 `Trigger`。`Label` 只為內建觸發器命名，因此需要在自訂按鈕上設定 `AutomationProperties.Name`，說明它屬於哪一列。
- 頁面頂部的工具列保持 `Side` 為 `Bottom`。視窗底部的控制項使用 `Top`；只有位於側邊欄邊緣的控制項才使用 `Right` 或 `Left`。
- 工具列右側的觸發器保持 `Alignment` 為 `End`，使選單朝內容方向展開。位於左邊緣的觸發器使用 `Start`。

### 處理選取

`ItemClicked` 會回報被啟用的列。除非該列設定了 `StaysOpenOnClick`，否則事件之後選單會關閉。

```csharp
menu.ItemClicked += (_, e) => status.Text = $"已選取：{e.Header}";
```

對用途相同的一組列使用同一個處理常式，並讀取 `e.Entry` 來區分各列。當某一列有自己的行為時，使用該列自身的 `Click` 事件。產生列背後的資料項目位於 `e.Item` 中。

### 即時標籤與單選列

透過程式碼設定 `Label` 可以變更觸發器的文字。具有相同 `GroupName` 的單選列互斥，選取其中一列即關閉選單。設定了 `StaysOpenOnClick="True"` 的核取列（`ToggleType="CheckBox"`）會保持選單開啟，使人能在一次造訪中切換多個選項。

<Demo name="LabelAndToggles" />

當恰好一個選項適用時使用單選列，例如排序方式：觸發器標籤隨之說明目前的選擇。當每個選項彼此獨立時使用核取列，例如要顯示哪些欄。單選群組保持在五個左右的選項；更長的選擇應放進 `Select`。

### 資料驅動的列與自訂觸發器

`ItemsSource` 會為每個項目建立一列。`Trigger` 可用任意控制項取代內建按鈕。選單會錨定到該控制項，`Button` 點選後即可開啟選單。

<Demo name="CustomTrigger" />

```xml
<arc:DropdownMenu ItemsSource="{Binding FileActions}" Side="Bottom">
    <arc:DropdownMenu.Trigger>
        <Button Content="..." />
    </arc:DropdownMenu.Trigger>
</arc:DropdownMenu>
```

當項目不是 `MenuEntry` 物件時，資料驅動的列可透過 `ItemTemplate` 設定其標題。每列背後的項目透過 `ItemClicked` 中的 `e.Item` 回報，因此處理常式可以針對該項目執行指令。

### 以程式碼開啟

```csharp
menu.Open(focusFirst: true);
menu.Close();
```

`Open(true)` 會開啟選單並反白第一列，效果與在觸發器上按 Enter 或 ArrowDown 相同。`IsOpen` 是雙向的，檢視模型可以透過設定它來開啟選單。在 XAML 中設定了 `IsOpen` 而啟動即開啟的選單不會取得焦點。

### MenuSurface

`MenuSurface` 是每個 Arc 選單背後可重複使用的選單浮層。可在程式碼中針對任意錨點將其開啟。它的選單列、鍵盤模型與動態效果都與下拉選單相同，但它本身沒有觸發器。

<Demo name="MenuSurface" />

當多個控制項共用一個選單時，把 `MenuSurface` 宣告為資源，並透過 `Open(anchor)` 傳入錨點。單獨的浮層不會回應錨點的點選；當控制項應自行切換選單時，用 `DropdownMenu.Menu` 將其附加上去。

### 繫結到檢視模型

下拉選單通常會改變它所在的頁面。檢視模型持有標籤、所選項目與指令；選單負責顯示它們，並把所選項目回傳。在 XAML 中繫結標籤與項目，在程式碼中設定指令。

```csharp
public interface IExportService
{
    Task StartCsvAsync(string filter);
}

public partial class InvoiceListViewModel : ObservableObject
{
    private readonly IExportService _exports;

    public InvoiceListViewModel(IExportService exports) => _exports = exports;

    [ObservableProperty]
    private string _sortLabel = "排序：最新";

    [ObservableProperty]
    private string _filter = "status:open";

    [RelayCommand]
    private async Task ExportCsvAsync()
    {
        // 依目前的篩選條件開始匯出。
        await _exports.StartCsvAsync(Filter);
    }
}
```

```xml
<arc:DropdownMenu Label="{Binding SortLabel}"
                  Icon="{x:Static arc:ArcIcons.ArrowUpDown}"
                  Alignment="Start">
    <arc:MenuEntryLabel Text="排序方式" />
    <arc:MenuEntry Header="最新" ToggleType="Radio" GroupName="sort" IsChecked="True" />
    <arc:MenuEntry Header="最舊" ToggleType="Radio" GroupName="sort" />
</arc:DropdownMenu>
```

```csharp
menu.Items.Add(new MenuEntry { Header = "匯出 CSV", Command = viewModel.ExportCsvCommand });
menu.ItemClicked += (_, e) => viewModel.SortLabel = $"排序：{e.Header}";
```

- `Label` 為單向繫結：檢視模型修改它，觸發器隨之變形。由於單選列的 `IsChecked` 由選單自己修改，應使用 `ItemClicked` 把所選項目寫回。
- 列的 `Command` 在該列被啟用時執行；當指令無法執行時，該列會被停用。使用 CommunityToolkit.Mvvm 的 `[RelayCommand]` 及其 `CanExecute` 來停用不適用的列。
- 在程式碼中建立的列可在程式碼中設定 `Command`。在 XAML 中宣告的列是選單的一部分，因此把它們的邏輯保留在 `ItemClicked` 中。

### 焦點與關閉

觸發器是選單的錨點，也是焦點返回的位置。透過鍵盤開啟時，反白落在一列上；透過指標開啟時，則不設反白。選單會在按下 Escape 或 Tab（焦點返回觸發器）、選取了不保持開啟的列、外部按下，以及觸發器離開視覺樹時關閉。

只有列自己宣告時，狀態會變化的列才會讓選單保持開啟。人們會多次切換的核取列應設定 `StaysOpenOnClick="True"`。單選列會關閉選單，因為選擇是最終的。下拉選單只適合短清單：其列不做虛擬化，因此數百項的清單應使用 `Combobox`，或放在帶搜尋的頁面中。

### 範例：發票清單頁頁首

發票頁頁首有一個主要操作、一個可見的「匯出」選單，以及一個在標籤中寫明目前順序的「排序」選單。匯出選單把檔案格式歸在一起，並把不可逆的操作放在最後，以分隔線與其他操作隔開。匯出觸發器的文字寫明了操作，因此人們在開啟前就知道選單的用途。指令從檢視模型執行，標籤隨狀態變化。

```xml
<DockPanel>
    <TextBlock DockPanel.Dock="Left"
               Text="發票"
               FontSize="22"
               FontWeight="SemiBold"
               VerticalAlignment="Center"
               Foreground="{arc:ArcDesignTokenResource Foreground}" />
    <StackPanel Orientation="Horizontal" Spacing="8" HorizontalAlignment="Right">
        <arc:DropdownMenu Label="排序：最新"
                          Icon="{x:Static arc:ArcIcons.ArrowUpDown}"
                          Alignment="End">
            <arc:MenuEntryLabel Text="排序方式" />
            <arc:MenuEntry Header="最新" ToggleType="Radio" GroupName="sort" IsChecked="True" />
            <arc:MenuEntry Header="最舊" ToggleType="Radio" GroupName="sort" />
            <arc:MenuEntry Header="金額" ToggleType="Radio" GroupName="sort" />
        </arc:DropdownMenu>
        <arc:DropdownMenu Label="匯出" Icon="{x:Static arc:ArcIcons.Download}">
            <arc:MenuEntry Header="匯出 CSV" />
            <arc:MenuEntry Header="匯出 PDF" />
            <arc:MenuEntry Header="傳送到會計系統" />
            <arc:MenuEntry Header="作廢所選發票"
                           Icon="{x:Static arc:ArcIcons.Trash2}"
                           IsDestructive="True"
                           HasSeparatorBefore="True" />
        </arc:DropdownMenu>
    </StackPanel>
</DockPanel>
```

### 文案規範

觸發器標籤是一個簡短的片語，說明選單做什麼，或目前顯示什麼。每一列是一個動詞加其物件，或群組中帶標題的一個選項。

- 選單執行動作時，使用一個動詞或名詞，例如「匯出」或「操作」。標籤顯示狀態時，使用「名稱：值」，例如「排序：最新」。
- 選單列使用句首大寫的句式，動詞在前：寫「匯出 CSV」，而不是「CSV 匯出」。
- 破壞性列要寫明物件：「作廢所選發票」「刪除檔案」。
- 觸發器標籤保持在約 18 個字元以內；觸發器是工具列控制項，過長的標籤會擠壓工具列。
- 為每組單選列使用 `MenuEntryLabel` 標題，讓這一組有名稱。

| 不要寫 | 改為 |
|---|---|
| 排序 | 排序：最新 |
| 更多選項 | 操作 |
| 匯出資料 | 匯出 CSV |
| 刪除 | 刪除檔案 |
| 點選切換檢視 | 檢視 |

### 最佳做法

**建議**

- 每組保持兩到七列。更長的清單需要搜尋，而選單不提供搜尋。
- 將破壞性列放在最後，並設定 `HasSeparatorBefore="True"`，在其標題中寫明它將刪除什麼。
- 單選列用於對整個頁面生效的一個選擇，核取列用於彼此獨立的選項。
- 讓觸發器標籤與狀態保持一致，使選單開啟前頁面就顯示目前的排序或檢視。
- 為自訂觸發器用 `AutomationProperties.Name` 設定與其操作相同的名稱，因為 `Label` 只為內建觸發器命名。

**避免**

- 在下拉選單中放置表單值。為表單提供值的內容需要 `Select` 或 `Combobox`，它們持有值、標籤與驗證狀態。
- 隱藏唯一的操作入口。主要操作應放在可見的按鈕上，因為選單多了一步。
- 選單開啟時變更列。列在開啟時讀取；請在觸發器開啟選單之前更新清單。
- 用一個選單承載兩個操作。兩個操作用兩個按鈕或 `ButtonGroup` 更易讀。
- 僅靠顏色標示破壞性列。它的標題寫明了物件，因此不依賴顏色也能理解。

### 常見問題

**為什麼我的核取列會關閉選單？** 除非列設定了 `StaysOpenOnClick="True"`，否則選取之後選單會關閉。對於人們可能多次切換的每一個核取列，都加上該屬性。

**選取之後如何變更觸發器文字？** 在 `ItemClicked` 處理常式中設定 `Label`，或將它繫結到檢視模型屬性。新標籤會隨變形動態效果向上浮現，寬度以彈簧方式調整。

**能否以程式碼開啟選單？** 可以。呼叫 `Open()` 會在觸發器下方顯示選單，呼叫 `Open(focusFirst: true)` 則以反白第一列開始。兩者都要等觸發器成為視窗的一部分後才有效。

**如何使用自訂控制項作為觸發器？** 把 `Trigger` 設為任意控制項。選單會錨定到該控制項，`Button` 點選後切換選單。對於頁面上已有的控制項，改用 `DropdownMenu.Menu` 附加選單。

**沒有空間時，選單會朝哪一側開啟？** 它會翻轉到另一側。`Side` 是首選的邊緣，當視窗沒有足夠空間時選單會移動。

## API 參考

<!-- api: DropdownMenu -->

<!-- api: MenuEntry -->

## 無障礙

- 觸發器以其 `Label` 播報，並回報選單是否展開。
- 選單列具有選單項目角色。輸入首字母可依字母快速定位到對應列，停用的列會被略過。
- 在觸發器上按 Enter、Space 或 ArrowDown 會開啟選單並定位到第一列。Escape 關閉選單，並將焦點交還觸發器。
- 破壞性操作列僅以顏色標示，因此其 `Header` 應寫明將被刪除的內容。
- 圖示與反白對輔助技術隱藏。
- 為選單取名時，`Label` 應寫明選單的作用，例如「排序：最新」或「匯出」。沒有文字的自訂觸發器需要自己的 `AutomationProperties.Name`，因為 `Label` 只為內建觸發器命名。

### 鍵盤

| 按鍵 | 操作 |
|---|---|
| 觸發器上的 `Enter`、`Space` 或 `ArrowDown` | 開啟選單，並反白第一列 |
| 觸發器上的 `ArrowUp` | 開啟選單，並反白最後一列 |
| 在觸發器上點選或輕觸 | 切換選單；選單開啟時不反白任何列 |
| `ArrowDown` / `ArrowUp` | 將反白移到下一列或上一列；清單循環，並略過停用的列 |
| `Home` / `End` | 將反白移到第一列或最後一列 |
| 字母鍵 | 輸入首字母，移動到文字以所輸字母開頭的列 |
| 選單中的 `Enter` / `Space` | 啟用反白的列，然後關閉選單，除非該列保持開啟 |
| `Escape` / `Tab` | 關閉選單，並將焦點交還觸發器 |

## 動態效果

- 選單以彈簧從觸發器邊緣展開，並伴隨短暫的淡入。關閉時更快、幅度更小。
- 指標移動時，一個反白區塊在列之間滑動；鍵盤操作時則直接跳轉。選單列以短促的錯開方式依序淡入。
- 新的 `Label` 會帶著輕微模糊向上浮現，觸發器寬度隨之調整。選單開啟時，箭頭會旋轉。
- 減少動態效果時去除縮放與滑移，僅保留快速淡入。關閉動態效果時選單立即顯示。

| 變化 | 完整 | 減少 | 關閉 |
|---|---|---|---|
| 選單開啟 | 選單從觸發器邊緣展開，起始偏移 5 px、縮放 0.97，以 580 ms 的彈簧和 160 ms 淡入進行 | 快速淡入，無縮放 | 立即 |
| 選單關閉 | 更快、更小：130 ms，偏移減半，縮放至 0.985 | 快速淡入 | 立即 |
| 反白移動 | 指標反白以靈敏的彈簧滑動；鍵盤反白直接跳轉 | 反白直接跳轉 | 立即 |
| 列出現 | 每列在 240 ms 內淡入，相隔 35 ms 錯開 | 快速淡入 | 立即 |
| `Label` 變化 | 新標籤以 0.3 em 向上浮現並帶輕微模糊，觸發器寬度以彈簧調整 | 瞬間淡入 | 立即 |
| 箭頭 | 隨選單開啟以彈簧旋轉 180 度 | 立即轉向 | 立即 |

## 相關元件

- `ContextMenu`：針對選取物件的次要操作，透過右鍵開啟。
- `SplitButton`：一個操作為預設、選單提供替代項時使用。
- `ButtonGroup`：指令是一排相關按鈕，而非隱藏的清單時使用。
- `UserMenu`：應用程式頂列中的帳戶入口。
- `MenuSurface`：共用的選單浮層，可將選單錨定到任意控制項。

| 元件 | 適用情境 |
|---|---|
| `ContextMenu` | 操作屬於單一物件，應在指標處開啟 |
| `SplitButton` | 一個操作為預設，選單提供替代項 |
| `ButtonGroup` | 指令是一排相關按鈕，而非隱藏的清單 |
| `UserMenu` | 選單是頭像背後的帳戶，包含身分與主題 |
| `Select` / `Combobox` | 選單選取的值用於表單 |
| `MenuSurface` | 需要在非下拉觸發器的控制項上顯示選單 |
