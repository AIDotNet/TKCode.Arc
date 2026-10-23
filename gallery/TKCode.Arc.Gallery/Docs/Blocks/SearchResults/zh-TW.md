---
title: 搜尋結果
summary: 工作區搜尋頁：附建議彈出視窗的搜尋欄位、數量隨查詢變化的篩選器、日期範圍、排序、可移除的篩選標籤、醒目提示的結果，以及結果詳情面板。
source-hash: eb3de9ba7c69
---

<Demo name="Preview" />

## 何時使用

`SearchResults` 是工作區的搜尋頁面：一個大型搜尋欄位，附有最近搜尋與建議搜尋的彈出視窗；一欄數量隨查詢變化的篩選器；一個顯示結果數量並帶有排序開關的標題；以及一份把人員與文件放在一起、並標出符合字詞的結果清單。它佔滿應用程式的主要內容區，針對可能跨越多種資料類型的查詢回答一個問題：哪些內容符合、屬於哪一類、位於何處。選取某筆結果會開啟詳情，不必離開頁面。

- **說明中心或知識庫**，文章、操作指南與版本說明並列放置。客戶輸入「退款」時，可以勾選符合問題的類型，每個值旁邊的數量會在點按之前就顯示答案位於何處。
- **索引了文件、檔案和人員的專案工作區。** 「團隊」篩選器可以把「onboarding」縮小到某一個小組，日期範圍會隱藏今年沒人動過的內容，每筆結果都會說明自己位於何處（「Doc in Support / Onboarding」），方便選中正確的項目。
- **在伺服器端搜尋工單和巨集的客服主控台。** 將 `IsFilteringEnabled` 設為 false，顯示伺服器給出的順序與數量，同時保留控制項已經繪製的醒目提示、篩選標籤與詳情。
- **搜尋事件、執行手冊和儀表板的維運工具。** 值班工程師按下 `/`，輸入服務名稱，按 ArrowDown，再按 Enter 開啟執行手冊，全程不必離開鍵盤。
- **搜尋發票、客戶和匯出檔案的財務後台。** 結果會附帶符合處前後的一段文字、最後編輯日期，在詳情中還有負責人與大小，讓人能分辨兩張相似的發票。

## 何時不用

- **在應用程式任何位置跳到頁面或執行動作。** 用 `CommandPalette`，它覆蓋整個視窗，把命令與頁面並列列出。
- **頁首或工具列中的精簡搜尋。** 用 `ExpandingSearch`，它平時只是一個圖示，開啟前不佔用頁面空間。
- **篩選頁面上已有的清單。** 在清單上方使用 `SearchField`，並在程式碼中篩選清單。幾十列的清單不需要篩選器、數量或詳情。
- **從短小的已知清單中選擇一個值**，例如狀態或國家。用 `Select` 或 `Combobox`；只有五個選項時，帶篩選器的搜尋頁面就太重了。
- **依欄位排序和篩選地瀏覽記錄。** 用 `SortableDataTable` 搭配 `FilterToolbar`。`SearchResults` 中的結果是用來閱讀和開啟的，而不是跨欄位比較的。

## 用法

為控制項設定項目，並在左側欄放入需要的篩選器。查詢中的每個字詞都必須與某個項目的標題、副標題、摘錄、註腳、位置、內文或某個關鍵字相符。比對、篩選、計數與排序都由控制項自行完成，除非你關閉這些功能，交由伺服器處理。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:SearchResults Height="640" Query="發票" Label="搜尋財務資料">
        <arc:SearchResults.Filters>
            <arc:SearchValueFilter Key="kind" Title="類型" />
            <arc:SearchValueFilter Key="team" Title="團隊" />
            <arc:SearchDateFilter Key="edited" Title="編輯時間" AppliesTo="Docs, Files" />
        </arc:SearchResults.Filters>
        <arc:SearchResultItem Title="發票範本"
                              Kind="Docs"
                              KindLabel="文件"
                              Location="財務 / 範本"
                              Snippet="每一張客戶發票都使用此範本。"
                              Icon="{x:Static arc:ArcIcons.FileText}" />
    </arc:SearchResults>
</UserControl>
```

`SearchResults` 衍生自 Avalonia 的 `TemplatedControl`，因此可以使用一般的樣式與主題資源。它由 Arc 的各個部件組成：自己的搜尋欄位（經過樣式化的 `TextBox`）、`Checkbox` 列、用於日期範圍的雙滑桿把手 `Slider`、用於排序方式的 `SegmentedControl`、用於人員的 `Avatar`、無結果時的 `EmptyState`、載入時的 `Skeleton`，以及清單的 `ScrollArea`。每筆結果都是一個 `SearchResultItem`，它是 `AvaloniaObject`，其屬性可以像其他物件一樣繫結。控制項會設定 `:narrow`、`:loading`、`:empty`、`:no-filters` 與 `:filtered` 這幾個偽類，供樣式使用。

### 結構

1. **搜尋欄位**（`Label`、`PlaceholderText`、`ShortcutText`）：高 50 px，圓角 18 px，包含放大鏡、文字、輸入第一個字元後出現的 24 px 清除按鈕，以及一個 24 px 的鍵帽，顯示用於聚焦搜尋欄位的按鍵（預設為「/」，搜尋欄位取得焦點時隱藏）。填色為淡化色並帶淡邊框；滑過與聚焦時使用強調邊框。
2. **建議彈出視窗**（`Recent`、`Suggestions`、`RecentLabel`、`SuggestedLabel`）：在取得焦點的搜尋欄位下方開啟，寬度與搜尋欄位相同，與搜尋欄位相距 8 px。分為兩組：帶時鐘圖示的「Recent」與帶放大鏡圖示的「Suggested」，列高 40 px，圓角 14 px。與查詢相同的那一列會顯示 Enter 鍵圖示。
3. **篩選器欄**（`Filters`、`FiltersTitle`、`ClearAllText`）：寬 244 px，帶分隔線。頂端是標題與一個「Clear all」連結，有篩選器作用時才淡入；其後每個篩選器一組：34 px 高的核取方塊列，數量靠右對齊，以及帶兩個滑桿把手與說明文字的日期範圍。沒有篩選器時這一欄不存在，結果佔滿整個寬度。
4. **結果標題**（`ResultsFormat`、`OneResultFormat`、`ElapsedFormat`、`SortOrder`）：中等字重的數量（滾動數字）、淡化顏色的耗時，以及右側的排序開關（「Best match」「Newest」「A to Z」）。
5. **已啟用篩選列**（`RemoveFilterText`、`ClearAllText`）：位於標題下方的圓角列，以高度動畫展開與收合。每個勾選的值與縮小的範圍都是一個 34 px 高的膠囊形標籤，例如帶叉號的「Type · People」，末尾是「Clear all」。
6. **結果列**（`SearchResultItem`）：36 px 的前導欄（人員為圓形 `Avatar`，其他為 20 px 的描邊圖示）、中等字重的標題、極小號淡化顏色的副標題、最多兩行的次要文字顏色摘錄，以及淡化顏色的註腳。列的左右內距為 20 px，圓角 24 px，列與列之間相距 4 px。
7. **游標**：位於目前結果列之後的一塊醒目提示區域，可由鍵盤或指標移動。建議彈出視窗中有自己的同類游標。
8. **按鍵提示**（`IsHintsVisible`、`HintMoveText`、`HintOpenText`、`HintSearchText`）：結果下方的小型鍵帽，「↑ ↓ to move, ↵ to open, / to search」，有結果列出時才顯示。
9. **詳情**（`OpenedItem`、`DetailTemplate`）：寬螢幕上是對話方塊，窄螢幕上是底部面板，包含類型、圓形關閉按鈕、標題、副標題、內文、事實表格與兩個按鈕。
10. **空狀態與骨架列**：置中的 `EmptyState`，帶搜尋圖示；載入時則是三列骨架列。

外框帶有 1 px 邊框與表面圓角。搜尋欄位所在的列內距為 24 px（窄螢幕時為 16 px），其下有一條 1 px 的分隔線。寬度小於 560 px 時套用窄螢幕版面配置。

### 項目

直接內嵌加入 `SearchResultItem` 物件，或將 `ItemsSource` 設為一份清單。`ItemsSource` 為 null 時使用內嵌的 `Items` 集合。清單為可觀察集合時，結果會隨其變化而更新。一筆結果由下列屬性組成：

- `Id` 是項目的穩定鍵，例如記錄自身的識別碼。為空時改用標題。
- `Title` 是結果的名稱。符合處會被標出，它還決定排序。
- `Subtitle` 是標題下方的一行，例如人員的職稱與城市。沒有它時，該列會寫成「`KindLabel` in `Location`」（格式為 `KindLocationFormat`），或者只寫位置。
- `Snippet` 是內容中的一段文字。過長的文字會被裁剪為圍繞第一個符合處的一個視窗。
- `Caption` 是摘錄下方的最後一行淡化文字，例如電子郵件地址。
- `Kind` 是結果的類型（「Docs」「People」「Files」）：鍵為 `kind` 的篩選器讀取的值。`KindLabel` 是它的單數名稱（「Doc」），用於副標題與詳情；為空時回退到 `Kind`。
- `Location` 是路徑或麵包屑，例如「Engineering / Handbook」，同樣會被搜尋。
- `Icon` 接受 24 × 24 的 Lucide 圖形或任何控制項。未設定圖示時，結果使用 FileText 圖示。設定了 `AvatarName`（姓名縮寫）或 `Avatar`（相片）的人員會改為顯示圓形大頭貼。
- `Keywords` 是額外的字詞，同樣能找到該項目，例如同義詞、工單編號與標籤。關鍵字參與搜尋，但不顯示。
- `Date` 是項目最後一次編輯的時間。它決定「Newest」的排序，並為日期範圍提供資料。
- `Attributes` 保存其他篩選器讀取的值，以篩選器的鍵為索引（`["team"] = "Design"`）。
- `Body`、`Url` 與 `Details` 用來填入詳情：內文段落、「Copy link」所複製的位址，以及事實表格中的各列。
- `IsEnabled` 設為 false 時，項目以淡化樣式列出。鍵盤游標會跳過它，且無法開啟。
- `Tag` 是開啟項目時希望取回的任何值。

```csharp
search.ItemsSource = new ObservableCollection<SearchResultItem>
{
    new()
    {
        Id = "q3",
        Title = "Q3 規劃筆記",
        Kind = "Docs",
        KindLabel = "文件",
        Location = "產品 / 規劃",
        Snippet = excerpt,
        Body = fullText,
        Date = new DateTimeOffset(2026, 9, 14, 9, 0, 0, TimeSpan.Zero),
        Url = "https://example.com/docs/q3",
        Attributes = { ["team"] = "產品" },
        Details = { new SearchResultDetail { Label = "作者", Value = "Maya Lindqvist", AvatarName = "Maya Lindqvist" } }
    },
    new()
    {
        Id = "maya",
        Title = "Maya Lindqvist",
        Kind = "People",
        KindLabel = "成員",
        Subtitle = "產品負責人，斯德哥爾摩",
        Caption = "maya@example.com",
        AvatarName = "Maya Lindqvist",
        Attributes = { ["team"] = "產品" }
    }
};
```

如果你就地修改了某個項目，請呼叫 `Refresh()`，讓搜尋與數量重新計算。從可觀察集合中加入或移除項目則無需呼叫，對 `Filters` 清單、篩選器的值，以及篩選器的 `Key` 與日期範圍所做的修改也無需呼叫。

### 比對、排序與醒目提示

查詢會依空白字元拆分為互不相同的字詞。只要每個字詞都出現在項目的標題、副標題、摘錄、註腳、位置、內文或某個關鍵字中，該項目即為符合。同一個字詞可以出現在不同欄位中。比較時忽略大小寫，但不會折疊重音符號：「resume」找不到「résumé」。空查詢會列出全部項目。`Details` 的各列只用於顯示，`Attributes` 只為篩選器提供資料，兩者都不參與搜尋。

排序只使用查詢的第一個字詞，並且只作用於「Best match」。在同一排名內，結果保持其原始順序。

| 排名 | 第一個字詞出現在 | 以 "release" 為例 |
|---|---|---|
| 1 | 標題的開頭 | "Release checklist" |
| 2 | 標題中某個單字的開頭 | "Q3 release notes" |
| 3 | 標題某個單字的內部 | "Prerelease audit" |
| 4 | 僅出現在副標題、摘錄、註腳、位置、內文或關鍵字中 | "Sync design: the release queue" |

標題、副標題、註腳與摘錄中每個字詞的每一次出現都會被標出：強調色淡底、主要文字顏色與中等字重。重疊的標記會合併。

長摘錄會圍繞第一個符合處裁剪：若符合處位於前幾行之內，則從開頭開始；否則從符合處之前約 36 個字元處開始，並對齊到單字的起點。摘錄最多 220 個字元、兩行，被裁切的一端會顯示省略號。只有 `Body` 包含這些字詞時，該列會用內文中的一段取代摘錄，因此每筆結果都能顯示它符合的原因。

### 篩選器

`Filters` 是左側欄的篩選器集合，`SearchFilter` 是它們的基底類別。`SearchValueFilter` 是針對某個屬性的一組核取方塊。它的 `Key` 為 `kind` 時讀取 `SearchResultItem.Kind`，否則讀取 `SearchResultItem.Attributes` 中同名的項目。`Values` 留空時，篩選器會依首次出現的順序列出它在各項目中找到的值（之後的新項目帶來的新值會繼續附加）；也可以自行把它們列成 `SearchFilterValue` 物件，以固定順序與標籤。同一個篩選器中勾選的多個值以「或」組合，不同篩選器之間以「且」組合。

每個值都會顯示在目前查詢下它會帶來多少筆結果。該數量不計入該篩選器自身的勾選（與分面搜尋一致），因此勾選「Docs」不會讓「Files」變成 0。數量會在輸入時跟隨查詢與其他篩選器變化。沒有結果的值保留在原位、顏色變淡，且無法勾選（已經勾選的值除外，這樣它還能被取消）。

`SearchDateFilter` 是針對 `SearchResultItem.Date` 的雙滑桿把手範圍滑桿。範圍從它所適用的項目中最早的日期延伸到最晚的日期；設定了 `Minimum` 與 `Maximum` 時則以它們為準。`Start` 與 `End` 是選定的兩端（null 表示範圍的那一端，把滑桿把手移回端點會再次設為 null）。`AppliesTo` 以逗號分隔的清單指明它適用的類型；其他類型的項目直接通過，因此針對文件的範圍不會隱藏人員，滑桿下方的說明文字也會這樣寫（「Applies to docs and files」）。範圍被縮小時，適用類型中沒有日期的項目會被隱藏。日期篩選器一律讀取 `Date`，它的 `Key` 只是用來命名。

<Demo name="Filters" />

勾選的值與縮小的範圍會作為篩選標籤出現在結果標題下方的列裡。標籤上的叉號移除對應的篩選，列中與左側欄中的「Clear all」則移除全部。在程式碼中可以讀寫值的 `IsSelected` 與日期篩選器的 `Start`、`End`，呼叫 `ClearFilters()`，並讀取 `HasActiveFilters` 與 `ActiveFilterCount`。控制項最多讀取 64 個篩選器。

### 排序與結果標題

標題列顯示結果數量、搜尋耗時與排序開關。`SortOrder` 為雙向：最佳符合、最新（`SearchResultItem.Date`，沒有日期的排在最後）或 A 到 Z（依標題，使用目前的地區設定）。控制項無法測量耗時，請在搜尋完成時設定 `Elapsed`；保持為 null 則只顯示數量。兩個數字都會滾動到新的值。沒有任何符合時，標題改為顯示 `NoResultsText`。

### 建議與最近搜尋

聚焦搜尋欄位會在其下方開啟一個彈出視窗，列出最近搜尋與你提供的 `Suggestions`（最多六則，且不重複已在「Recent」中的項目）。每當送出一個查詢（按 Enter、選擇一則建議或點按「Try」標籤），或帶著該查詢開啟一筆結果，控制項都會把它加入 `Recent`，新的在前，並去除重複。`MaxRecent` 限制該清單的長度。請在頁面顯示前預先放入 `Recent`，並透過它的 `CollectionChanged` 事件儲存；它是一個 `AvaloniaList<string>`，不是可繫結的屬性。

方向鍵在各列之間移動，Enter 執行醒目提示的一列或照原樣輸入的文字，Escape 關閉彈出視窗，點按一列會填入搜尋欄位並送出。一旦編輯了文字，彈出視窗只列出包含該文字的項目，沒有項目時會自行關閉。`QuerySubmitted` 會通知你每一個送出的查詢。若希望搜尋欄位永遠不開啟彈出視窗，請將 `IsSuggestionsEnabled` 設為 false。

<Demo name="Suggestions" />

### 繫結到檢視模型

檢視模型持有查詢、排序方式、項目和開啟命令。控制項則持有游標、篩選器的勾選狀態與版面配置。

```csharp
public partial class WorkspaceSearchViewModel : ObservableObject
{
    private readonly INavigationService _navigation;

    public WorkspaceSearchViewModel(INavigationService navigation)
    {
        _navigation = navigation;
    }

    [ObservableProperty]
    private string _query = string.Empty;

    [ObservableProperty]
    private SearchSortOrder _sort = SearchSortOrder.BestMatch;

    [ObservableProperty]
    private TimeSpan? _elapsed;

    public ObservableCollection<SearchResultItem> Results { get; } = [];

    [RelayCommand]
    private void Open(SearchResultItem? item) => _navigation.Navigate(item?.Tag);
}
```

```xml
<arc:SearchResults Label="搜尋工作區"
                   ItemsSource="{Binding Results}"
                   Query="{Binding Query}"
                   SortOrder="{Binding Sort}"
                   Elapsed="{Binding Elapsed}"
                   IsDetailEnabled="False"
                   InvokeCommand="{Binding OpenCommand}" />
```

- `Query` 與 `SortOrder` 是雙向的，因此輸入會更新檢視模型，連結也可以在開啟頁面時就填好查詢。
- `ItemsSource` 與 `Elapsed` 是單向的。請將 `Results` 保持為 `ObservableCollection`，並就地修改它。
- `InvokeCommand` 以被開啟的項目作為參數執行，控制項會先檢查 `CanExecute`。這個檢視模型自行負責導覽，所以範例關閉了內建詳情；如果希望檢視模型知道哪個詳情處於開啟狀態，請保留內建詳情並繫結雙向的 `OpenedItem`。
- `Filters` 與 `Recent` 是由你填入和讀取的集合，不是用來繫結的屬性。

### 開啟結果

在結果上按 Enter 或點按會以該項目觸發 `ItemInvoked`，以同一個項目執行 `InvokeCommand`，然後開啟內建詳情。`ActiveItem` 是鍵盤游標所在的結果，在你移動它之前就是第一筆可用的結果，因此狀態列可以說明按 Enter 會開啟什麼。`IsEnabled` 為 false 的結果會變淡，游標會跳過它，也無法開啟。如果你的應用程式自行負責導覽而不是顯示詳情，請將 `IsDetailEnabled` 設為 false，或在事件上設定 `Handled`。

```csharp
search.ItemInvoked += (_, e) =>
{
    Navigate(e.Item.Id);
    e.Handled = true; // 跳過內建詳情
};
```

詳情在寬螢幕上是對話方塊，在窄螢幕上是帶拖曳把手的底部面板。它依序顯示結果的類型與圓形關閉按鈕、帶醒目提示的標題、副標題、`Body` 的各段（以空行分隔；沒有內文時顯示摘錄）、由第一列「Matches」（查詢在標題與內文中被標出的次數）與該項目 `Details` 組成的表格，以及兩個按鈕。「Open」（`OpenFormat`，其中的類型名稱為小寫）與「Copy link」會觸發 `DetailActionInvoked`；「Copy link」還會把 `Url` 放到剪貼簿。按 Escape、點按關閉按鈕或點按變暗的頁面都會關閉它，焦點隨後回到原處。

`OpenedItem` 為雙向：設定它或呼叫 `ShowDetail(item)` 即可在程式碼中開啟詳情，`CloseDetail()` 則關閉詳情。`DetailTemplate` 可取代標題與按鈕之間的內容，其資料是該 `SearchResultItem`。

<Demo name="Detail" />

```csharp
search.DetailActionInvoked += (_, e) =>
{
    if (e.Action == SearchResultAction.Open)
    {
        Navigate(e.Item.Id);
        search.CloseDetail();
    }
};
```

### 伺服器端搜尋

由伺服器搜尋時，請將 `IsFilteringEnabled` 設為 false。此時各項目依你給出的順序顯示，篩選值上的數量由你設定，查詢中的字詞仍會被標出。查詢、某個篩選器或排序方式發生變化、需要重新請求時，會觸發 `SearchChanged`，其 `Reason` 為 `Query`、`Filters` 或 `Sort`。請求進行期間設定 `IsLoading`：骨架列會取代結果的位置並保持先前結果的高度，因此頁面不會跳動，數量列顯示「Searching…」，游標與按鍵提示則會消失。在呼叫伺服器之前對查詢做防抖動處理，並取消被較新的擊鍵所取代的請求。沒有防抖動時，每次擊鍵都會發出一個請求，結果會閃爍。

<Demo name="ServerSearch" />

### 無結果

沒有任何符合時，空狀態會在標題中顯示查詢（`EmptyTitleFormat`，其中 `{0}` 為查詢字詞），並在描述中顯示 `EmptyDescription`。數量列顯示 `NoResultsText`。查詢為空且沒有任何項目時，標題同樣使用 `NoResultsText`。

如果有篩選器處於啟用狀態，描述會變為 `EmptyFilteredDescription`，即「The filters leave nothing. Remove one to see more.」，並提供一個「Clear filters」按鈕（`ClearFiltersText`）來移除它們。`Suggestions` 會以按鈕的形式列在 `TryText`（預設為「Try」）之後，供使用者嘗試。選擇其中一個會填入搜尋欄位並執行搜尋。

<Demo name="NoResults" />

### 窄螢幕版面

寬度小於 560 像素時，搜尋欄位所在列的內距從 24 像素收緊為 16 像素，篩選器欄收合為一個帶有已啟用篩選數量的「Filters」按鈕，點按後在底部面板中開啟同樣的篩選器（在程式碼中呼叫 `OpenFiltersSheet()` 效果相同）。排序開關移到數量下方，詳情變為底部面板，高度不超過視窗的 92%。完全沒有篩選器時，窄螢幕版面會讓排序開關保持在右側。控制項重新變寬時，篩選器面板會自動關閉。

<Demo name="Narrow" />

### 長清單

只會建立前 `PageSize` 筆結果（預設 15 筆）。捲動到距清單末端 240 像素以內、按「Show more」（`ShowMoreText`），或在最後一筆已列出的結果上按 ArrowDown 或 PageDown 時，再載入後面的結果。新的查詢、篩選或排序方式會從頂端重新開始。

有上千筆資料時輸入依然流暢。只增長的查詢只在上一次的符合結果中搜尋，而不是走訪每個項目；勾選篩選器不會再次搜尋文字；數量與篩選在同一次走訪中完成；保留下來的列是同一批控制項，因此每次擊鍵只重新配置變化的部分。在開發用筆記型電腦上，搜尋並篩選 5000 個項目只需一兩毫秒，每次擊鍵的大部分開銷在於重新配置發生變化的列。

### 文案

每段文字都有英文預設值與可修改它的屬性。格式中以 `{0}` 表示數量、秒數、查詢字詞或類型，需要第二個值時用 `{1}`。

| 屬性 | 預設值 | 用途 |
|---|---|---|
| `Label` | "Search" | 地標與搜尋欄位的名稱 |
| `PlaceholderText` | "Search" | 空搜尋欄位中的提示 |
| `ShortcutText` | "/" | 搜尋欄位鍵帽與按鍵提示中的按鍵 |
| `FiltersTitle` | "Filters" | 篩選器欄與面板的標題 |
| `ClearAllText` | "Clear all" | 移除全部篩選的連結 |
| `ClearFiltersText` | "Clear filters" | 空狀態中移除篩選的按鈕 |
| `ResultsFormat` | "{0} results" | 多筆結果時的數量 |
| `OneResultFormat` | "{0} result" | 一筆結果時的數量 |
| `NoResultsText` | "No results" | 沒有符合時的數量，以及沒有查詢時的空狀態標題 |
| `LoadingText` | "Searching…" | `IsLoading` 為 true 時的數量列 |
| `ElapsedFormat` | "in {0} seconds" | 數量之後的耗時；`{0}` 為秒數 |
| `BestMatchText` | "Best match" | 第一個排序選項 |
| `NewestText` | "Newest" | 第二個排序選項 |
| `TitleSortText` | "A to Z" | 第三個排序選項 |
| `RecentLabel` | "Recent" | 彈出視窗中最近搜尋的群組 |
| `SuggestedLabel` | "Suggested" | 彈出視窗中建議的群組 |
| `TryText` | "Try" | 空狀態中建議字詞之前的文字 |
| `EmptyTitleFormat` | "No results for “{0}”" | 空狀態標題 |
| `EmptyDescription` | "Check the spelling or try fewer words." | 空狀態文字 |
| `EmptyFilteredDescription` | "The filters leave nothing. Remove one to see more." | 有篩選器處於啟用狀態時的空狀態文字 |
| `ShowMoreText` | "Show more" | 還有更多結果時，已列出結果下方的按鈕 |
| `KindLocationFormat` | "{0} in {1}" | 由類型名稱與位置組成的副標題 |
| `HintMoveText` | "to move," | 方向鍵之後的提示 |
| `HintOpenText` | "to open," | Enter 鍵之後的提示 |
| `HintSearchText` | "to search" | 快速鍵之後的提示 |
| `MatchesLabel` | "Matches" | 詳情表格的第一列 |
| `OpenFormat` | "Open {0}" | 詳情的主要按鈕；`{0}` 為小寫的類型名稱 |
| `CopyLinkText` | "Copy link" | 詳情的次要按鈕 |
| `CloseText` | "Close" | 關閉按鈕的無障礙名稱 |
| `ClearSearchText` | "Clear search" | 搜尋欄位清除按鈕的無障礙名稱 |
| `FiltersButtonText` | "Filters" | 窄螢幕版面中的按鈕 |
| `AppliesToFormat` | "Applies to {0}" | 沒有自訂說明的日期篩選器的說明文字；`{0}` 列出它適用的類型 |
| `AndText` | "and" | 該說明文字中最後兩個類型之間的連接詞 |
| `RemoveFilterText` | "Remove filter" | 篩選標籤叉號的無障礙名稱 |
| `DateFormat` | "MMM yyyy" | 日期範圍兩端的寫法，依目前的地區設定格式化 |

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:SearchResults Label="搜尋說明中心"
                       PlaceholderText="搜尋文章"
                       ResultsFormat="{}{0} 篇文章"
                       OneResultFormat="{}{0} 篇文章"
                       NoResultsText="沒有文章"
                       ElapsedFormat="{}耗時 {0} 秒"
                       FiltersTitle="縮小範圍"
                       EmptyTitleFormat="找不到與「{0}」相關的內容" />
</UserControl>
```

### 資料結構與效能

控制項會讀取每個項目的標題、副標題、摘錄、註腳、類型、位置、內文、關鍵字、日期、屬性與 id，以及詳情的各列；其他內容請放在 `Tag` 中。

- 新的查詢會讓本機比對檢查每個項目，查詢只增長時則只檢查上一次的符合結果，因此幾千個項目的本機索引依然靈敏。超過這個規模，請改為在伺服器端搜尋，並將 `IsFilteringEnabled` 設為 false。
- 在設定 `Details` 之前，先依閱讀者的地區設定格式化日期與大小；控制項會原樣顯示你給出的字串。日期範圍與排序使用目前的地區設定，一個處理序中第一次依 A 到 Z 排序所需的資料，會在建立第一個搜尋頁面時預先載入，不會讓視窗卡頓。
- 讓 `PageSize` 保持較小。每個已列出的列都會被建立與配置，下一頁則在使用者捲動時載入。

### 範例：發票的財務搜尋

財務後台在發票清單中點按連結後開啟搜尋頁，查詢字詞「invoice」已經填好。「Team」篩選器勾選了 Finance，「Edited」範圍從 8 月 1 日開始，因此標題下方有兩個篩選標籤，較早的一份發票 PDF 被排除在外。Sales 與 Support 旁邊的數量顯示了其他團隊會帶來多少筆結果。最新優先的排序把沒有日期的人員排在最後。

![帶有篩選器、篩選標籤、一位人員與多份文件的發票搜尋結果，符合的字詞已被標出](images/SearchResults.png)

```xml
<arc:SearchResults Width="840"
                   Height="860"
                   Label="Search finance"
                   PlaceholderText="Search invoices, customers and exports"
                   Query="invoice"
                   SortOrder="Newest"
                   Elapsed="0:0:0.08">
    <arc:SearchResults.Filters>
        <arc:SearchValueFilter Key="kind" Title="Type">
            <arc:SearchFilterValue Value="Docs" />
            <arc:SearchFilterValue Value="Files" />
            <arc:SearchFilterValue Value="People" />
        </arc:SearchValueFilter>
        <arc:SearchValueFilter Key="team" Title="Team">
            <arc:SearchFilterValue Value="Finance" IsSelected="True" />
            <arc:SearchFilterValue Value="Sales" />
            <arc:SearchFilterValue Value="Support" />
        </arc:SearchValueFilter>
        <arc:SearchDateFilter Key="edited" Title="Edited" AppliesTo="Docs, Files" Start="2026-08-01" />
    </arc:SearchResults.Filters>
    <arc:SearchResultItem Title="Invoice template"
                          Kind="Docs"
                          KindLabel="Doc"
                          Location="Finance / Templates"
                          Date="2026-09-14"
                          Snippet="Use this template for every customer invoice. Fill in the purchase order number before you send it."
                          Caption="Edited by Nia Okonkwo"
                          Icon="{x:Static arc:ArcIcons.FileText}">
        <arc:SearchResultItem.Attributes>
            <x:String x:Key="team">Finance</x:String>
        </arc:SearchResultItem.Attributes>
    </arc:SearchResultItem>
    <arc:SearchResultItem Title="Late invoices"
                          Kind="Docs"
                          KindLabel="Doc"
                          Location="Finance / Reports"
                          Date="2026-09-02"
                          Snippet="Invoices more than 30 days overdue, grouped by customer, with the contact who owns each account."
                          Caption="Edited by Soren Lindgren"
                          Icon="{x:Static arc:ArcIcons.FileText}">
        <arc:SearchResultItem.Attributes>
            <x:String x:Key="team">Finance</x:String>
        </arc:SearchResultItem.Attributes>
    </arc:SearchResultItem>
    <arc:SearchResultItem Title="Invoices Q2.xlsx"
                          Kind="Files"
                          KindLabel="File"
                          Location="Finance / Exports"
                          Date="2026-08-19"
                          Snippet="One row per invoice with the customer, the amount and the day it was paid."
                          Caption="Edited by Tobias Wren · 48 KB"
                          Icon="{x:Static arc:ArcIcons.File}">
        <arc:SearchResultItem.Attributes>
            <x:String x:Key="team">Finance</x:String>
        </arc:SearchResultItem.Attributes>
    </arc:SearchResultItem>
    <arc:SearchResultItem Title="Tobias Wren"
                          Kind="People"
                          KindLabel="Person"
                          Subtitle="Finance partner, Dublin"
                          Snippet="Approves every invoice above 10,000 and keeps the payment calendar."
                          Caption="tobias@tidewater.example"
                          AvatarName="Tobias Wren">
        <arc:SearchResultItem.Attributes>
            <x:String x:Key="team">Finance</x:String>
        </arc:SearchResultItem.Attributes>
    </arc:SearchResultItem>
    <arc:SearchResultItem Title="Invoice questions macro"
                          Kind="Docs"
                          KindLabel="Doc"
                          Location="Support / Macros"
                          Date="2026-08-22"
                          Snippet="A reply for customers who ask why an invoice shows two charges.">
        <arc:SearchResultItem.Attributes>
            <x:String x:Key="team">Support</x:String>
        </arc:SearchResultItem.Attributes>
    </arc:SearchResultItem>
    <arc:SearchResultItem Title="Invoice discount rules"
                          Kind="Files"
                          KindLabel="File"
                          Location="Sales / Pricing"
                          Date="2026-08-27"
                          Snippet="Which discounts may appear on an invoice, and who has to approve them.">
        <arc:SearchResultItem.Attributes>
            <x:String x:Key="team">Sales</x:String>
        </arc:SearchResultItem.Attributes>
    </arc:SearchResultItem>
    <arc:SearchResultItem Title="INV-1042.pdf"
                          Kind="Files"
                          KindLabel="File"
                          Location="Customers / Brightline"
                          Date="2026-03-12"
                          Snippet="The signed invoice for the annual plan.">
        <arc:SearchResultItem.Attributes>
            <x:String x:Key="team">Finance</x:String>
        </arc:SearchResultItem.Attributes>
    </arc:SearchResultItem>
</arc:SearchResults>
```

### 文案規範

請讓標籤和結果本身就能讓人在不開啟的情況下做出選擇。類型是首字母大寫的複數名詞（「Docs」「People」「Files」），因為它們是篩選器的值；再為每個類型設定一個單數的 `KindLabel`（「Doc」「Person」「File」），用於副標題與「Open doc」按鈕。標題應說明事物本身，而不是一句關於它的話。位置是以 " / " 串接的麵包屑。人員的副標題是職稱與城市，註腳則是聯絡他們的唯一方式。`Details` 中的列是簡短的事實：名稱、大小、相對時間。

| 不要寫 | 改為 |
|---|---|
| 結果：4 | 4 筆結果（`ResultsFormat`，傳入數量） |
| 沒有與你的查詢相符的結果 | 沒有「季度預測」的結果（`EmptyTitleFormat`） |
| 重設 | 清除篩選（`ClearFiltersText`） |
| 開啟 | 開啟文件（`OpenFormat`，傳入類型名稱） |

### 最佳做法

**建議**

- 為每筆結果設定人們熟悉的 `Kind` 與 `KindLabel`；「Type」篩選器與每則副標題都會讀取它們。
- 把人們會搜尋的字詞優先放在標題中。標題會被搜尋並參與排序；位置、副標題與關鍵字會被搜尋；摘錄會被搜尋並被標出。較長的文字請放在 `Body` 中，它會被搜尋，並在詳情中顯示。
- 提供兩到四個篩選器，依它們縮小搜尋範圍的頻率排序；順序有意義時，請自行列出它們的值（「Support」「Design」「Engineering」比首次出現的順序更好）。
- 對於耗時超過一瞬間的請求設定 `IsLoading`，使頁面保持高度，並表明工作正在進行。
- 自行測量耗時，只為真實的搜尋設定 `Elapsed`；虛構的數字讀起來像一個承諾。

**避免**

- 為每個屬性都做一個篩選器。過長的篩選器欄需要捲動，使用者得先讀選項才能看到結果；篩選器只有在能快速縮小一份很長的清單時才有價值。
- 在 `Snippet` 中放入整份文件。控制項會圍繞符合處裁剪長文字；過長的段落會掩蓋結果為何符合。完整的文字請放在 `Body` 中。
- 就地修改項目的 `Kind`、`Title` 或 `Attributes` 卻不呼叫 `Refresh()`。數量與排序是在搜尋執行時計算的。
- 用 `IsEnabled` 隱藏結果。被停用的結果仍會列出、淡化並計入數量；如果根本不應出現，請從項目中移除。
- 在 `ItemInvoked` 中導覽，卻讓 `IsDetailEnabled` 保持開啟。這樣一次 Enter 會同時做兩件事；請關閉詳情，或設定 `Handled`。

### 常見問題

**能改變篩選值的順序嗎？** 可以。把它們作為 `SearchFilterValue` 物件列在篩選器裡。只有沒有任何值的篩選器才會依首次出現的順序列出它找到的值。

**如何在程式碼中清除搜尋？** 將 `Query` 設為空字串，並呼叫 `ClearFilters()`。空查詢會再次列出全部項目。在搜尋欄位中按 Escape 也能透過鍵盤清除查詢，前提是先關閉建議彈出視窗。

**能隱藏篩選器嗎？** 讓 `Filters` 保持為空（或者不給任何值篩選器提供值）：這一欄會消失，結果佔滿整個寬度。

**為什麼某個值顯示 0，並且無法勾選？** 在目前的查詢與其他篩選器下，它不會顯示任何結果，所以它變淡並被停用，同時保留在原位，使使用者輸入時這一欄不會跳動。已經勾選的值始終可用，因此可以取消勾選。

**如何在多次工作階段之間保留最近搜尋？** 處理 `Recent.CollectionChanged` 並儲存這份清單；在頁面顯示前，把儲存的項目加入 `Recent` 中。

## API 參考

<!-- api: SearchResults -->

<!-- api: SearchResultItem -->

<!-- api: SearchResultDetail -->

<!-- api: SearchFilter -->

<!-- api: SearchValueFilter -->

<!-- api: SearchFilterValue -->

<!-- api: SearchDateFilter -->

## 無障礙

- 該控制項是一個搜尋地標，由 `Label` 命名，它同時也為搜尋欄位命名。清除按鈕、篩選器按鈕與關閉按鈕都有自己的名稱。
- 結果是一份以結果數量命名的清單，數量列是禮貌式即時區域，因此結果數量變化時會被朗讀。游標所在的結果是清單的選取項目，每筆結果以標題、類型與副標題命名。
- 每個篩選器是一個以標題命名的群組。每個值是一個核取方塊，名稱包含標籤與數量；沒有結果的值被停用，但保留在原位。日期範圍的滑桿把手以篩選器命名（「Edited start」「Edited end」），方向鍵移動一天，PageUp 與 PageDown 移動 30 天。篩選標籤以其篩選器與值命名，它的叉號讀作「Remove filter」加上這個名稱。
- 排序開關是分段清單：方向鍵選取上一個或下一個選項，只有選取的一項是 Tab 停駐點。
- 在搜尋欄位中，建議彈出視窗開啟時 ArrowDown 在建議中移動，否則進入結果。Enter 執行醒目提示的建議或輸入的文字，彈出視窗關閉時則開啟游標所在的結果。Escape 先關閉彈出視窗，再清除查詢。焦點在控制項內時，「/」或 Ctrl+F（macOS 上為 Cmd+F）會把焦點移到搜尋欄位，在結果中輸入的字元會繼續寫入搜尋欄位。
- 在結果中，ArrowUp、ArrowDown、Home、End、PageUp、PageDown 移動游標，在第一筆結果上按 ArrowUp 回到搜尋欄位，Enter 或 Space 開啟結果，Escape 回到搜尋欄位。
- 詳情與篩選器面板是模態的：焦點移入並保持在其中，Escape、關閉按鈕或點按變暗的頁面會將其關閉，焦點隨後回到原處。關閉模態不會讓建議彈出視窗自行重新開啟。
- 被停用的結果保留在清單中並淡化，但游標會跳過它們，因此鍵盤使用者永遠不會停在無法開啟的項目上。

### 鍵盤

| 按鍵 | 位置 | 操作 |
|---|---|---|
| `/`、`Ctrl+F`（`Cmd+F`） | 控制項內任何位置 | 將焦點移到搜尋欄位並選取其中的文字 |
| 取得焦點 | 搜尋欄位 | 開啟建議彈出視窗 |
| `ArrowDown` / `ArrowUp` | 搜尋欄位，彈出視窗已開啟 | 在建議中移動醒目提示；在第一則上按 ArrowUp 會取消醒目提示 |
| `Enter` | 搜尋欄位，彈出視窗已開啟 | 執行醒目提示的建議，或照原樣輸入的文字 |
| `Escape` | 搜尋欄位 | 關閉彈出視窗；彈出視窗已關閉時清除查詢 |
| `ArrowDown` | 搜尋欄位，彈出視窗已關閉 | 移入結果；游標位於第一筆結果 |
| `Enter` | 搜尋欄位，彈出視窗已關閉 | 開啟游標所在的結果 |
| `ArrowUp` / `ArrowDown` | 結果 | 游標移動一筆結果 |
| `Home` / `End`、`PageUp` / `PageDown` | 結果 | 將游標移到已列出的第一筆或最後一筆結果，或依頁移動 |
| 在最後一筆已列出的結果上按 `ArrowDown`、`PageDown` | 結果 | 列出下一頁，然後移動 |
| `ArrowUp`（在第一筆結果上） | 結果 | 回到搜尋欄位並清除游標 |
| `Enter` 或 `Space` | 結果 | 開啟游標所在的結果 |
| `Escape` | 結果 | 回到搜尋欄位 |
| 任何字元 | 結果 | 回到搜尋欄位並把它加入查詢 |
| `ArrowLeft` / `ArrowRight` | 日期範圍的滑桿把手 | 移動一天 |
| `PageUp` / `PageDown` | 日期範圍的滑桿把手 | 移動 30 天 |
| `Space` | 篩選值 | 勾選或取消勾選該值 |
| `ArrowLeft` / `ArrowRight` | 排序開關 | 選取上一個或下一個選項，到頭後循環 |
| `Escape` | 詳情或篩選器面板 | 關閉它；焦點回到原處 |
| 指標懸停 | 結果 | 將游標移到指標下方的列（觸控不會觸發） |
| 點按 | 結果 | 開啟結果 |

## 動態效果

- 游標在結果之間滑動：指標移動時使用彈簧，方向鍵移動時使用 70 ms 的補間動畫。游標首次出現時淡入，清除時淡出。建議彈出視窗中有同樣的游標。
- 之前沒有顯示過的結果會淡入並上移 4 像素，彼此相隔 35 ms，最多四列。保持不變的結果不會移動，因此只讓清單變短的輸入不會產生任何動畫。
- 數量與耗時會滾動到新的值。排序的選取塊以形變彈簧在選項之間滑動。
- 已啟用篩選列以高度彈簧展開與收合，其中的標籤隨篩選一同出現。篩選器欄中的「Clear all」連結淡入淡出，搜尋欄位的清除按鈕則以彈入、彈出的方式出現與消失。
- 建議彈出視窗以輕微的縮放與短距離的上移進入，離開更快，與其他浮動表面一致。
- 詳情在寬螢幕上升起到位，在窄螢幕上從底部滑入；篩選器面板同樣從底部滑入。
- 減少動態效果模式保留淡入淡出，去掉所有位移：游標與排序選取塊直接跳轉，數量立即變化，結果只做淡入。關閉動態效果時所有變更立即生效。

| 變化 | 完整 | 減少 | 關閉 |
|---|---|---|---|
| 游標跟隨指標 | 靈敏的彈簧 | 直接跳到該列 | 立即完成 |
| 游標跟隨方向鍵 | 70 ms 補間 | 直接跳到該列 | 立即完成 |
| 游標出現或被清除 | 80 ms 內淡入，100 ms 內淡出 | 淡入淡出 | 立即完成 |
| 新結果出現 | 淡入並上升 4 px，錯開 35 ms，最多四列 | 淡入 | 立即完成 |
| 數量或耗時變化 | 數字滾動到新值 | 立即變化 | 立即完成 |
| 排序選項變化 | 選取塊在形變彈簧上滑動 | 直接跳轉 | 立即完成 |
| 已啟用篩選列展開或收合 | 高度彈簧 | 淡入淡出 | 立即完成 |
| 建議彈出視窗開啟或關閉 | 縮放與上移，並伴隨淡入淡出 | 淡入淡出 | 立即完成 |
| 詳情開啟 | 升起到位（窄螢幕時從底部滑入） | 淡入淡出 | 立即完成 |
| 篩選器面板開啟 | 從底部滑入 | 淡入淡出 | 立即完成 |
| 數量列、空狀態、載入列 | 交叉淡化 | 淡入淡出 | 立即完成 |

## 相關元件

- `CommandPalette` 用於在應用程式任何位置執行動作和導覽。
- `ExpandingSearch` 用於頁首中的精簡搜尋。
- `SearchField` 用於篩選頁面上的清單。
- `Checkbox`、`Slider` 與 `SegmentedControl`，本區塊的篩選器與排序開關由它們建構。
- `EmptyState` 和 `Skeleton`，本區塊在無結果和載入時使用它們。

| 元件 | 適用情境 |
|---|---|
| `CommandPalette` | 人們需要在應用程式任何位置執行命令或跳到頁面 |
| `ExpandingSearch` | 搜尋位於頁首或工具列中，依需要開啟 |
| `SearchField` | 文字用於篩選頁面上已有的清單 |
| `FilterToolbar` | 篩選器位於表格或清單上方，以標籤形式呈現，不需要搜尋頁面 |
| `Tabs` | 這些類型是同一頁面的不同檢視，而不是查詢的篩選器 |
| `SortableDataTable` | 結果是需要依欄位比較的記錄 |
