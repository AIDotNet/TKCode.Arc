---
title: 展開式搜尋
summary: 圖示展開為搜尋欄位，並在下方顯示結果。
source-hash: a65b1d0f5fb6
---

<Demo name="HeaderSearch" />

## 何時使用

`ExpandingSearch` 用於頁首或工具列空間有限的搜尋。靜止時它是一個 44 px 的圓形按鈕，帶搜尋圖示，所佔空間並不比圖示按鈕多。使用者啟動後，圓形會展開為文字欄位，符合的結果依群組顯示在其下方。它適用於完整搜尋欄位會佔用頁面導覽、標題和操作所需空間的情境，也適用於搜尋只是偶爾用來跳轉、而非主要任務的情境。

- **SaaS 主控台頁首中的專案與文件跳轉**：Northwind 工作區頁首，帶通知鈴鐺和大頭貼。輸入「atlas」會開啟 Atlas 重新設計專案，兩次按鍵即可到達目標位置。
- **輸入前展示最近項目**：`Suggestions` 會在「最近」標題下顯示使用者最近開啟的三份文件或儀表板，因此欄位在輸入任何文字前就已經有用。
- **分析應用程式中切換儀表板與報表**：工具列上已有日期範圍和匯出按鈕。搜尋可以依名稱、關鍵字或所屬團隊找到 200 個儀表板中的一個，不需要再增加一列控制項。
- **支援與營運工具**：內部主控台頂列中的工單、客戶或訂單查詢。每個結果都有標題和一行詮釋資料（例如「Order · Paid · 12 Mar 2026」），用來區分兩個相似的結果。
- **設定與管理導覽**：從任何畫面快速前往設定頁面的精簡方式，頁面清單夠短，可以依標題和關鍵字比對。

## 何時不用

- **篩選頁面上已有的表格、清單或格線**：用 `SearchField`，它會原地篩選，並帶清除按鈕和 `SearchSubmitted`。展開式欄位把篩選藏在一次點按之後，使用者看不出清單已被縮小。
- **為表單中儲存的值做選擇**：用 `Combobox` 或 `Select`。它們把所選的值保存為表單的欄位並支援驗證；本控制項只回報被選取的項目。
- **執行動作的全域指令面板**：用展示庫中的 `CommandPalette` 區塊，它是為帶快速鍵和分類的指令而建。`ExpandingSearch` 用於尋找記錄和位置。
- **透過伺服器分頁搜尋數千筆記錄**：控制項每次按鍵都會在記憶體中對傳入的項目排序。應使用 `SearchField` 查詢伺服器，並把分頁後的結果顯示在 `Table` 或 `DataGrid` 中。
- **必須保持可見的搜尋**：需要逐行閱讀的結果清單（例如含多行摘要的訊息搜尋）需要空間。應把它放在帶有 `SearchField` 和結果清單的頁面上。
- **頁首中單一的固定選擇**，例如工作區切換器：用 `DropdownMenu` 或 `Select`，它們會持續顯示帶標籤的選項。

## 用法

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:ExpandingSearch Label="搜尋專案與文件"
                         MaxResults="6"
                         ExpandedWidth="360" />
</UserControl>
```

`ExpandingSearch` 衍生自 Avalonia 的 `TemplatedControl`。它的範本由三個部分組成：覆蓋收合圓形的按鈕、隨欄位一起增長的外殼，以及基於 Avalonia `TextBox` 建構的欄位。因此游標、選取、輸入法組字和剪貼簿快速鍵的行為與文字方塊一致。結果清單是錨定在欄位上的彈出層，因此不會被它所在的頁首裁切。控制項本身負責狀態機：展開、收合、反白和選取。

### 結構

1. **觸發按鈕**：覆蓋收合圓形的透明按鈕，其名稱為 `Label`。圓形為 44 px，即中等控制項尺寸的高度。
2. **搜尋圖示**：18 px 的 Lucide 搜尋圖示，位於圓形前緣內側 12 px 處。靜止時使用次要文字顏色；欄位展開或取得焦點時使用前景色。
3. **外殼**：1 px 邊框，表面色背景。靜止時使用預設邊框色，展開時使用強邊框色，欄位或按鈕有焦點時使用前景色。外殼的圓角隨寬度增加，從圓形過渡到控制項圓角。
4. **焦點環**：外殼外側 3 px 的焦點色光暈，僅在鍵盤焦點時顯示。
5. **欄位文字與預留位置文字**：文字為內文字級（14 px），前緣保留 38 px，後緣保留 40 px，使文字避開清除按鈕。欄位為空時，`PlaceholderText` 以淡化的文字顏色顯示；未設定時，預留位置文字即為 `Label`。
6. **清除按鈕**：28 px 的圓形按鈕，位於後緣 7 px 處，內含 16 px 的關閉圖示。僅在欄位有文字時出現。它不可取得焦點；鍵盤可透過 `Escape` 清除查詢，同時收合欄位。
7. **結果面板**：浮動表面，使用提升的表面色、浮動陰影和面板圓角。位於欄位下方 8 px，寬度跟隨欄位，高度不超過 352 px 或視窗高度的 60%（取較小者）。較長的清單會在面板內捲動。
8. **群組標題**：群組名稱（`Group`），淡化顏色，12 px，中等字重，位於其結果列上方。
9. **結果列**：高度至少 48 px，水平內距 12 px、垂直內距 6 px，圓角 20 px。包含可選的 18 px 圖示（`Icon`）、14 px 的標題（符合的文字為中等字重），以及可選的 12 px 詮釋資料行（淡化顏色）。
10. **空白狀態**：標題為「找不到結果」，其後接加引號的查詢內容，下方是淡化顏色的 `EmptyHint` 行。

### 項目與建議

將 `ItemsSource` 填入 `ExpandingSearchItem` 物件。每個項目都有 `Id`（穩定的鍵，為空時回退到標題）和 `Title`，另可設定選用的 `Meta`、`Group`、`Icon`、`Keywords` 和 `Tag`。`Keywords` 是用來找到該項目的額外詞語，但不會顯示。`Tag` 可以是任何物件，在選取該項目時回傳，例如記錄本身或它的路由。在 XAML 中直接加入的 `Items` 用法相同：控制項會監聽該清單的變更。

`Suggestions` 是另一個清單，會在使用者輸入任何內容之前顯示，標題為 `SuggestionsLabel`（預設標題為「最近」）。建議項目不會被查詢篩選；使用者一開始輸入，它們就會被結果取代。

```csharp
var items = new List<ExpandingSearchItem>
{
    new() { Id = "q3", Title = "季度報告", Meta = "文件 · 2 小時前編輯", Group = "文件" },
    new() { Id = "atlas", Title = "Atlas重新設計", Meta = "專案 · 12 個待辦事項", Group = "專案" },
    new() { Id = "billing", Title = "帳單遷移", Group = "專案", Keywords = ["invoices", "payments"] }
};

header.ItemsSource = items;
header.Suggestions = new[] { items[0], items[1] };
header.SuggestionsLabel = "最近";
```

#### 結果的排序方式

比對不區分大小寫，並使用去除前後空白後的查詢。每個項目取四個比對層級中最好的一個，層級數字越小排得越前面：

| 層級 | 查詢符合 | 以 "report" 為例 |
|---|---|---|
| 1 | 標題的開頭 | "Report automation" |
| 2 | 標題中某個詞的開頭（位於空白、`-` 或 `/` 之後） | "Quarterly report" |
| 3 | 標題中某個詞的內部 | "Subreport tools" |
| 4 | 標題不符合時，詮釋資料行或關鍵字符合 | "Revenue"，詮釋資料為 "Monthly report" |

沒有符合的項目會被捨棄。同一層級中的項目保持提供時的順序。`MaxResults`（預設為 6）在排序之後生效，因此截斷清單時保留的是最佳符合項目。之後結果會依 `Group` 標題分組，群組的顯示順序是其第一個項目在來源清單中出現的順序，而不是最佳符合的順序。每個群組中只顯示符合的項目。

由於層級優先，以查詢開頭的短項目會勝過只包含該查詢的長項目。標題應使用人們真正會輸入的詞，因為標題開頭的權重最大。

### 選取

使用者有兩種方式選取結果：在反白的結果上按 `Enter`，或用滑鼠左鍵按一下某一列。不論哪種方式，選取都依以下順序進行：

1. 欄位收合為圖示，查詢內容被清除。
2. 焦點回到觸發按鈕。
3. 以被選取的項目作為 `e.Item` 觸發 `ItemSelected`。該事件會冒泡，因此樹狀結構中較上層的處理常式也能收到。
4. 若該命令可以執行，則以被選取的項目作為參數執行 `SelectCommand`。

在 MVVM 應用程式中使用 `SelectCommand`；當程式碼後置需要直接處理該項目時，則使用事件。

```csharp
header.ItemSelected += (_, e) => Open(e.Item.Id);
```

```xml
<arc:ExpandingSearch Label="搜尋"
                     ItemsSource="{Binding Items}"
                     SelectCommand="{Binding OpenCommand}" />
```

### 繫結到檢視模型

檢視模型擁有項目、查詢和導覽。控制項負責顯示結果並回報選取。`Query` 與 `IsExpanded` 是雙向的；`ItemsSource`、`Suggestions` 和 `SelectCommand` 是單向的。

```csharp
public partial class HeaderViewModel : ObservableObject
{
    private readonly INavigationService _navigation;

    public HeaderViewModel(INavigationService navigation)
    {
        _navigation = navigation;
    }

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private bool _isSearchOpen;

    public ObservableCollection<ExpandingSearchItem> SearchItems { get; } = new();

    public ObservableCollection<ExpandingSearchItem> RecentItems { get; } = new();

    [RelayCommand]
    private void OpenResult(ExpandingSearchItem? item)
    {
        if (item is null)
        {
            return;
        }
        IsSearchOpen = false;
        _navigation.Open(item.Id);
    }
}
```

```xml
<arc:ExpandingSearch Label="搜尋專案與文件"
                     ItemsSource="{Binding SearchItems}"
                     Suggestions="{Binding RecentItems}"
                     SuggestionsLabel="最近"
                     Query="{Binding SearchText, Mode=TwoWay}"
                     IsExpanded="{Binding IsSearchOpen, Mode=TwoWay}"
                     SelectCommand="{Binding OpenResultCommand}" />
```

- `Query` 是雙向的，隨每次按鍵更新。透過 `Escape` 或選取結果收合欄位時，它會被清除，檢視模型會收到空字串。從檢視模型設定它會更新欄位。
- `IsExpanded` 是雙向的。設為 `True` 會展開欄位並使其取得焦點；欄位收合時，控制項會把它設回 `False`。若要用視窗中的鍵盤快速鍵開啟搜尋，請使用它。
- 如果搜尋必須隨輸入進行，請在 `Query` 變更的地方（其 setter 或屬性變更處理常式中）執行搜尋。
- 每次查詢變更都會重新讀取 `ItemsSource`。對 `ObservableCollection` 的修改會在下一次按鍵時生效；若指派新的集合給 `ItemsSource`，則立即生效。
- 編譯繫結需要在檢視上設定 `x:DataType`（為求簡潔，這裡的範例省略了它）。

### 在程式碼中開啟與關閉

`Expand` 會開啟欄位並將焦點移入其中。`Collapse` 會將其收合為圖示，並清除查詢。預設情況下，`Collapse` 會把焦點返回觸發按鈕；當呼叫端要把焦點移到別處（例如移到該項目開啟的頁面）時，傳入 `returnFocus: false`。

```csharp
search.Expand();
search.Collapse();
search.Collapse(returnFocus: false);
```

`IsExpanded` 是雙向的，因此也可以繫結到檢視模型。另有三個唯讀成員回報清單的狀態：`IsPanelOpen`（結果是否正在顯示）、`VisibleItems`（依順序排列的可見項目，即建議或結果）以及 `ActiveIndex`（`VisibleItems` 中反白的位置，無則為 -1）。欄位中的文字請讀取 `Query`。

### 錨點與寬度

`Anchor` 設定按鈕所在的邊緣。欄位從該邊緣向外展開，最寬至 `ExpandedWidth`（預設為 360）。請把控制項放在能容納完整寬度的區域中：控制項會填滿其區域，而欄位絕不會超過該區域或 `ExpandedWidth`，取兩者中較小的一個。在較窄的頁首上，欄位會縮小到區域寬度，而不是擠開頁首中的其他項目。

當控制項位於頁首或面板的左邊緣時，使用 `Anchor="Start"`，如此圓形會保持在使用者預期的位置，欄位則朝內容方向展開。控制項位於工具列右邊緣（常見情況）時，保留預設的 `End`。

<Demo name="AnchoredAtStart" />

### 焦點與關閉

開啟欄位會把焦點移入其中。結果面板只在欄位有焦點時開啟，並一直保持開啟，直到欄位失去焦點或欄位收合。點按面板外部不會關閉它，因為輕觸關閉已停用；面板會在焦點離開欄位時關閉。

- **焦點離開空白欄位**：欄位立即收合為圖示，焦點不移動。
- **焦點離開有文字的欄位**：結果關閉，欄位保持展開並保留其文字。透過點按或鍵盤移動焦點回到欄位時，結果會重新開啟。
- **切換到其他視窗**：欄位保持其狀態，視窗失去啟用時不會收合。
- **Escape**：收合欄位，清除查詢，並把焦點返回觸發按鈕。

### 範例：主控台頁首中的專案搜尋

分析主控台的頁首。工作區名稱和大頭貼分別位於左側和右側，搜尋以 44 px 圓形居中顯示，即使用者使用前的樣子。頁首為欄位展開時所需的空間預留了位置，因此使用者啟動搜尋時，標題和大頭貼不會移動。欄位的結果不在圖中：它們會在輸入時於浮動面板中開啟，如上面的面板說明所述。在實際應用程式中，頁首還會設定 `Suggestions`，使面板在欄位開啟時即提供最近的項目。

![主控台頁首，顯示工作區名稱、收合的專案搜尋圓形按鈕和大頭貼](images/ProjectSearchHeader.png)

```xml
<Border Width="680"
        Padding="16,12"
        CornerRadius="16"
        BorderThickness="1"
        BorderBrush="{arc:ArcDesignTokenResource Border}"
        Background="{arc:ArcDesignTokenResource Surface}">
    <DockPanel>
        <Border DockPanel.Dock="Right"
                Width="32"
                Height="32"
                CornerRadius="16"
                Margin="12,0,0,0"
                Background="{arc:ArcDesignTokenResource AccentSubtle}">
            <TextBlock Text="AL"
                       FontSize="12"
                       FontWeight="Medium"
                       HorizontalAlignment="Center"
                       VerticalAlignment="Center"
                       Foreground="{arc:ArcDesignTokenResource Foreground}" />
        </Border>
        <TextBlock DockPanel.Dock="Left"
                   Text="Acme Analytics"
                   FontSize="16"
                   FontWeight="Medium"
                   VerticalAlignment="Center"
                   Foreground="{arc:ArcDesignTokenResource Foreground}" />
        <arc:ExpandingSearch Label="Search projects and docs"
                             ExpandedWidth="360"
                             HorizontalAlignment="Right" />
    </DockPanel>
</Border>
```

### 文案規範

- **標籤**：簡短的名詞片語，說明搜尋的對象，例如「搜尋專案與文件」或「搜尋訂單」。它是按鈕和欄位的無障礙名稱，因此單獨看也必須說得通。
- **預留位置文字**：只有當預設值（標籤）不夠清楚時才設定 `PlaceholderText`。它應說明能找到什麼，而不是下指令，例如「專案、文件、儀表板」。
- **項目標題**：使用者會認得的名稱：專案、文件、客戶。標題盡量控制在 40 個字元以內，避免結果列被截斷。
- **詮釋資料行**：一行簡短的事實，用來區分相似的項目：類型、擁有者或最近變更，數字使用阿拉伯數字（「12 個待辦事項」）。
- **空白狀態提示**：說明下一步（預設為「請嘗試較短的詞，或檢查拼字。」）。不要責怪使用者。
- **群組名稱**：複數名詞（「專案」「文件」「儀表板」）。
- **建議標題**：簡短的標題，說明為什麼顯示這些項目（「最近」「已釘選」）。

| 不要寫 | 改為 |
|---|---|
| 搜尋… | 搜尋專案與文件 |
| 找不到任何符合項目！！！ | 找不到 "forecast" 的結果 |
| 結果 | 專案 |
| 輸入你要找的項目名稱 | 專案、文件、儀表板 |

### 最佳做法

**建議**

- 依欄位要尋找的內容為其命名（「搜尋專案與文件」）。`Label` 是按鈕、欄位和結果的無障礙名稱，籠統的標籤會讓每個結果清單都含糊不清。
- 為使用者最常開啟的目的地設定 `Suggestions`。這樣面板在輸入前就有有用的內容，第一次按鍵只會縮小一個較短的清單，而不是從零開始。
- 為每個項目設定穩定的 `Id` 和 `Group`。`Id` 讓被選取的項目可以追溯到其記錄，群組則讓較長的結果清單容易瀏覽。
- 把控制項放在能容納 `ExpandedWidth` 的區域中。欄位永遠不會超出其區域，因此過窄的區域會造成搜尋擁擠、結果列被裁切。
- 在 MVVM 應用程式中繫結 `SelectCommand`。該命令在欄位收合之後執行，因此導覽從穩定的頁首開始。

**避免**

- 搜尋頁面上已有的清單。應使用 `SearchField`，它會原地篩選，且不會把篩選藏在點按之後。
- 在 `ItemsSource` 中填入數千筆記錄。每次按鍵都要對所有項目排序，因此應先依權限篩選，並讓伺服器處理大型查詢。
- 依賴查詢為空時隱藏的結果。沒有查詢也沒有 `Suggestions` 時，面板什麼也不顯示，對尚未輸入的使用者來說，欄位看起來像是壞了。
- 欄位開啟時替換項目，卻不發出新的查詢。面板只在查詢變更或屬性被重新指派時才重新讀取 `ItemsSource`，因此過時的清單會一直顯示到下一次按鍵。
- 用控制項保存表單中的值。`Combobox` 會保存該值並進行驗證；本控制項只回報被選取的項目。

### 資料結構與效能

每次按鍵都會對所有項目執行排序，然後建構結果列。對於頁首中的數百個項目，這很快；對於數千個項目則會變慢。對於大型目錄，應把 `ItemsSource` 限制為使用者在本工作區中可以開啟的項目（繫結之前先依權限篩選），並在清單過長、記憶體放不下時讓伺服器處理查詢。保持 `MaxResults` 較小（6 到 8），因為面板本來就設計得較短，過長的清單只會捲動。

每個工作階段建構一次項目並重複使用。為每筆記錄建立新的 `ExpandingSearchItem` 時，都應設定穩定的 `Id`，使控制項和檢視模型能把被選取的項目對應回其記錄。當路由需要的資訊超過 `Id` 時，把記錄本身放入 `Tag`。

用應用程式的文化設定來格式化詮釋資料行中的數字和日期，例如金額使用 `amount.ToString("C", CultureInfo.CurrentUICulture)`，使中文或德文使用者看到當地的格式。控制項不會替你格式化數值，它會依原樣顯示詮釋資料字串。

### 常見問題

**我點按別處時欄位為什麼會收合？** 空白欄位在失去焦點時會收合，這是刻意的設計，以免閒置的搜尋佔用頁首空間。有文字的欄位會保持展開並保留查詢，方便使用者回來繼續。

**如何用鍵盤快速鍵開啟搜尋？** 在視窗中把快速鍵繫結到一個命令，該命令會把檢視模型的 `IsSearchOpen` 設為 `True`；或在程式碼後置中對控制項呼叫 `Expand`。焦點會移入欄位，使用者可以立即輸入。

**能否只用滑鼠選取結果？** 可以。用左鍵按下某一列時，焦點仍留在欄位中；在同一列上放開按鈕即選取它。指標移到某一列上時，該列也會取得反白，因此鍵盤使用者和滑鼠使用者看到的反白是一致的。

**輸入時為什麼第一個結果已經反白？** 查詢一有文字，第一個結果就會反白，因此 `Enter` 會選取它。建議項目不帶反白顯示；按 `Down` 會選取第一個建議。

**為什麼我的項目沒有列在我設定的群組下？** 群組依其第一個項目在 `ItemsSource` 中出現的順序顯示，群組內的項目保持排序後的順序。把你想要的群組放在清單頂端，並檢查 `Group` 是否完全一致，因為群組是依書寫的文字進行比較。

## API 參考

<!-- api: ExpandingSearch -->

<!-- api: ExpandingSearchItem -->

## 無障礙

- `Label` 為觸發按鈕和欄位命名。預留位置文字會回退為 `Label`，因此欄位為空時名稱仍然可見。
- 反白結果的標題是欄位的說明文字。螢幕閱讀器在讀出欄位時，也會在使用者瀏覽清單時說明哪個結果被反白。
- 每個結果列以其標題命名，如果有詮釋資料行，則接著讀出（「帳單遷移，專案 · 已暫停」）。
- 結果數量、帶查詢內容的「找不到結果」或建議數量會以禮貌的方式播報。只有在數量或狀態變更時才會播報。
- 收合的欄位處於惰性狀態：不可取得焦點、不接收指標輸入，也不在 Tab 順序中，因此不會擋住其下方的內容。
- 清除按鈕不在 Tab 順序中。鍵盤使用者可以選取文字後刪除來清除，或用 `Escape` 收合整個欄位。
- 按鈕的焦點環只在鍵盤焦點（Tab 或方向鍵導覽）時出現，因此指標按下會開啟欄位而不顯示焦點環。欄位在任何焦點下都會顯示其焦點邊框。
- 結果列不在 Tab 順序中。欄位有焦點時，鍵盤使用者用 `Down` 與 `Up` 來到它們。

### 鍵盤

| 按鍵 | 操作 |
|---|---|
| `Tab` / `Shift+Tab` | 在頁首的控制項之間移動；收合的按鈕是停靠點，展開的欄位會取代其位置 |
| 按鈕上的 `Enter` 或 `Space` | 開啟欄位並把焦點移入其中 |
| 輸入文字 | 更新 `Query`，列出結果或空白狀態；有文字後反白第一個結果 |
| `Down` / `Up` | 把反白移到下一個或上一個結果，並在兩端循環。沒有反白時，`Down` 選取第一個，`Up` 選取最後一個 |
| `Enter` | 選取反白的結果，收合欄位並把焦點返回按鈕。沒有反白時控制項不處理它 |
| `Escape` | 將欄位收合為圖示，清除查詢，並把焦點返回按鈕 |
| `Ctrl+A`、`Ctrl+C`、`Ctrl+X`、`Ctrl+V`（macOS 上為 `Cmd`） | 在欄位中全選、複製、剪下和貼上 |

## 動態效果

- 展開時寬度以帶輕微過衝的彈簧變形，圓形圖示變為圓角欄位。圓角與寬度共用同一彈簧，因此兩者不會錯位。
- 收合時寬度以更平滑、無過衝的彈簧收回，因此圓形不會縮小到低於其尺寸。
- 結果面板以平滑彈簧從零高度展開，並淡入其文字。減少動態效果時只做淡入。
- 反白以靈敏彈簧在結果之間滑動，並保持上一次移動的速度。新的結果集在其列上淡入，沒有位移。
- 按下按鈕時縮放至 97%，直到放開；放開時由變形接手。

| 變化 | 完整 | 減少 | 關閉 |
|---|---|---|---|
| 欄位展開 | 寬度以彈簧變形；形狀開始移動後，預留位置文字與文字淡入 | 寬度立即變化；文字在 150 ms 內淡入 | 立即完成 |
| 欄位收合 | 寬度以平滑彈簧收回；文字先淡出 | 寬度立即變化；文字在 100 ms 內淡出 | 立即完成 |
| 結果面板展開或收合 | 高度以平滑彈簧展開；文字淡入 | 僅淡入淡出 | 立即完成 |
| 反白移動 | 以靈敏彈簧在列之間滑動，保持速度 | 不滑動；反白原地移動 | 立即完成 |
| 按鈕按下 | 縮放至 97%，直到放開 | 不縮放 | 立即完成 |
| 懸停與焦點顏色 | 邊框、背景和圖示顏色漸變；焦點環淡入 | 立即完成 | 立即完成 |

## 相關元件

- `SearchField`：用於篩選頁面上已有內容的欄位。
- `Combobox`：用於選擇表單中儲存的值。
- `Input`：一般用途的單行輸入框；搜尋欄位的文字方塊即以它為基礎。

| 元件 | 適用情境 |
|---|---|
| `SearchField` | 搜尋會在頁面上原地篩選表格、清單或頁面 |
| `Combobox` / `Select` | 所選的值儲存為表單的欄位 |
| `DropdownMenu` | 簡短固定的操作或選項清單需要持續以選單形式顯示 |
| `Input` | 單行值需要輸入並儲存，而不是搜尋 |
| `CommandPalette` 區塊 | 使用者透過鍵盤快速鍵在應用程式中執行指令 |
| `Breadcrumb` | 使用者需要看清自己所在的位置，並向上移動層級 |
