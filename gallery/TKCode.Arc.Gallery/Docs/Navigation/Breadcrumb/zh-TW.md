---
title: 麵包屑
summary: 顯示頁面在階層結構中的位置。
source-hash: c0cd6cdda7cd
---

<Demo name="Default" />

## 何時使用

`Breadcrumb` 顯示從某個區域的根部一路到使用者目前頁面的路徑，例如工作區、設定和帳單。它位於頁面標題上方，回答「我現在在哪裡？」，並提供一鍵返回上層的入口。在桌面應用程式中，它也可以顯示用戶端路徑，例如檔案瀏覽器的資料夾路徑；此時選取某個麵包屑會改變檢視，而不是網址。

- **管理後台中的帳單區段**：路徑 Acme Analytics、帳單、發票、inv_2026_0412 告訴管理員他們位於哪個工作區和哪個區段，每個上層麵包屑都能返回上方的清單。
- **桌面應用程式中的檔案瀏覽器或素材庫**：每個資料夾一個麵包屑（主目錄、專案、Q3 報告）。選取某個麵包屑會移除其後的麵包屑，資料夾清單隨之更新為較短的路徑。
- **很深的設定樹**：組織、安全性、單一登入。使用者常常從搜尋結果或連結進入設定頁，因此上層麵包屑會指出返回的路徑。
- **開發者工具與事件紀錄**：平台、事件、INC-2026-114 顯示擁有該紀錄的團隊和產品，當同一個 ID 出現在多處時，這一點尤其重要。
- **窄視窗上的長階層結構**：使用 `MaxVisibleItems` 時，根部和最後幾層保持可見，中間的層級摺疊為一個溢出按鈕，因此很深的路徑也能放進側邊欄寬度的欄位中。

## 何時不用

- **瀏覽帶有可展開節點的整棵樹**：用 `TreeView`，它會顯示每一層的同層項目，並保持分支展開。
- **在同一物件的同層檢視之間切換**，例如概觀、活動和設定：用 `Tabs`。麵包屑是使用者會返回的位置，而不是用來切換的面板。
- **在同一集合的多個頁面之間翻頁**：用 `Pagination`，它知道總頁數和目前頁碼。
- **使用者依固定順序完成的步驟**，例如購物車、配送、付款和審核：用 `Stepper`，它顯示進度和每一步的狀態。
- **從清單中選取一個值**，例如表單中的資料夾或專案：用 `Select` 或 `Combobox`。麵包屑不保存表單提交的值。

## 用法

每個麵包屑都是一個 `BreadcrumbItem`。最後一個麵包屑是目前頁面：它是純文字，從不是連結。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:Breadcrumb>
        <arc:BreadcrumbItem Content="工作區" />
        <arc:BreadcrumbItem Content="設定" />
        <arc:BreadcrumbItem Content="帳單" />
    </arc:Breadcrumb>
</UserControl>
```

`Breadcrumb` 衍生自 Avalonia 的 `ItemsControl`，因此 `Items`、`ItemsSource` 和 `ItemTemplate` 的行為與任何清單一致。每個項目都包裝在 `BreadcrumbItem`（一個 `ContentControl`）中，因此其標籤就是 `Content`。面板把麵包屑排成一列；當路徑寬於可用空間時，它會換到新的一行。麵包屑本身從不取得焦點；只有可以被選取的麵包屑才會取得焦點。

### 結構

1. **地標**（`Breadcrumb`、`Label`）：由 `Label` 命名的導覽地標，預設為「麵包屑」。它的各個麵包屑組成一個清單，每一項都會播報其在路徑中的位置。
2. **分隔符號**：一個 16 px 的尖括號形箭頭，使用強邊框色，以 1.75 的描邊繪製。它位於除第一個之外的每個麵包屑前面，因此根部沒有箭頭。箭頭與其標籤之間有 6 px 的間距。
3. **標籤**（`BreadcrumbItem.Content`）：小字級，使用淡化的文字顏色，水平內距 2 px、垂直內距 4 px，位於圓角為 7 px 的外框內。相鄰麵包屑之間相距 6 px。
4. **懸停底線**：可互動麵包屑標籤下方的 1 px 線條。懸停時以 45% 的不透明度出現。
5. **目前麵包屑**（`IsCurrent`）：最後一個麵包屑。它使用強調色的強色和中等字重，且不可互動。每個麵包屑都會依中等字重為其標籤保留寬度，因此成為目前麵包屑時，前面的麵包屑不會移動。
6. **溢出按鈕**：當 `MaxVisibleItems` 摺疊路徑時，第一個被隱藏的位置會變成省略號按鈕，其名稱由 `OverflowLabel` 指定。
7. **焦點環**：2 px 的焦點色環，位於標籤外框外側 4 px 處。僅在鍵盤聚焦時出現。
8. **換行**：路徑換行時，行與行之間相距 2 px。

### 透過點按導覽

目前頁面之前的每個麵包屑都可以透過指標、`Enter` 或 `Space` 選取。處理 `ItemClick` 即可回應所選的麵包屑。`Index` 是它在路徑中的位置，0 為根部；`Item` 是資料項目（或內嵌撰寫的 `BreadcrumbItem`）。

```csharp
breadcrumb.ItemClick += (_, e) =>
{
    status.Text = $"導覽到 {((BreadcrumbItem)e.Item!).Content}（索引 {e.Index}）";
};
```

`ItemClick` 事件會先執行。接著執行該麵包屑的 `Command`（傳入其 `CommandParameter`），然後開啟其 `NavigateUri`。應用程式內的導覽請處理該事件，因為它能在同一處取得索引和項目；只有當麵包屑本身負責該操作時，才使用另外兩種方式。

### 繫結路徑

將 `ItemsSource` 設為路徑的集合。使用 `ObservableCollection<T>`，使麵包屑隨路徑的增減而變化。

<Demo name="ClientSidePath" />

開啟一個資料夾會透過 `path.Add(name)` 新增一個麵包屑。選取某個麵包屑會移除其後的麵包屑，被移除的麵包屑會以動畫退出。加到末尾的麵包屑從左側滑入，留在原位的麵包屑則滑動讓出空間。在相同索引處保留相同標籤的麵包屑不會再次播放其進入動畫。

### 命令與導覽 URI

帶有 `Command` 的麵包屑會在 `CanExecute` 回傳 true 時以 `CommandParameter` 執行它，並呈現為按鈕。帶有 `NavigateUri` 的麵包屑呈現為連結，其位址會透過頂層的 `Launcher` 開啟，並交由平台處理。每個可以被選取的麵包屑都會被播報為按鈕，帶有 `NavigateUri` 的麵包屑則播報為連結。對於桌面應用程式自身的檢視，優先使用 `ItemClick` 或命令，使檢視在視窗內變化，而不是在瀏覽器中開啟。

```xml
<arc:BreadcrumbItem Content="帳單"
                    Command="{Binding OpenBillingCommand}"
                    CommandParameter="{Binding BillingId}" />
```

設定了 `IsClickable="False"` 的麵包屑即使不是目前頁面，也只是純文字。用於僅作標籤的區段，例如沒有自己頁面的年份分組。

### 標籤

`Label` 為輔助技術命名導覽地標。當頁面上不只一個導覽地標時設定它，使螢幕閱讀器使用者能夠區分兩者。預設值為「麵包屑」。

```xml
<arc:Breadcrumb Label="資料夾路徑" />
```

### 摺疊路徑

`MaxVisibleItems` 會把很長路徑的中間部分摺疊進溢出選單。與 Arc 一樣，預設關閉，即路徑永不摺疊。當路徑長於 `MaxVisibleItems` 時，根部保持可見，第一個被隱藏的位置變成省略號按鈕，最後 `MaxVisibleItems − 1` 個麵包屑保持可見。按鈕會開啟一個選單，依路徑順序列出被隱藏的麵包屑。選取某個項目會引發 `ItemClick`，參數為該項目在路徑中的索引。

<Demo name="CollapsedPath" />

選單是位於按鈕下方的輕量關閉型彈出層。它可以透過程式碼用 `OpenOverflow()` 和 `CloseOverflow()` 開啟和關閉，`IsOverflowOpen` 表示其是否正在顯示。選單用 `Up`、`Down`、`Home` 和 `End` 移動焦點，按 `Escape` 關閉並將焦點返回按鈕，選擇之後也會關閉。

### 資料結構與效能

麵包屑描述的是一條路徑，因此只保留真正重要的層級。每個項目都是一個 `BreadcrumbItem` 容器，面板會量測並排列所有容器；控制項不會虛擬化其項目。設計用途是只放幾個層級的路徑。對於很深的階層結構，請縮短標籤並使用 `MaxVisibleItems`，而不是建立一條視窗必須換行的長路徑。

標籤應該是文字。內容為控制項的麵包屑會保留其位置，而無需為中等字重預留寬度；溢出選單顯示的是控制項的自動化名稱，而不是控制項本身。

### 繫結到檢視模型

檔案瀏覽器把路徑保存在檢視模型中，並將麵包屑繫結到它。使用 CommunityToolkit.Mvvm 時，路徑是檢視模型變更的 `ObservableCollection<string>`，麵包屑只負責顯示它。

```csharp
public partial class FolderBrowserViewModel : ObservableObject
{
    public ObservableCollection<string> Path { get; } = new() { "主目錄", "專案", "Acme Analytics" };

    public void OpenFolder(string name)
    {
        Path.Add(name);
        // 在此載入新路徑下的清單。
    }

    public void NavigateTo(int index)
    {
        while (Path.Count > index + 1)
        {
            Path.RemoveAt(Path.Count - 1);
        }
    }

    [RelayCommand]
    private void GoUp()
    {
        if (Path.Count > 1)
        {
            Path.RemoveAt(Path.Count - 1);
        }
    }
}
```

```xml
<StackPanel Spacing="12">
    <arc:Breadcrumb ItemsSource="{Binding Path}" Label="資料夾路徑" />
    <arc:Button Content="上一層" Variant="Ghost" Command="{Binding GoUpCommand}" />
</StackPanel>
```

```csharp
breadcrumb.ItemClick += (_, e) => viewModel.NavigateTo(e.Index);
```

麵包屑從不寫入集合。選取某個麵包屑只是一個請求，因此由檢視模型決定是否進行導覽、使用 `NavigateTo` 截斷路徑，並載入新的清單。`Path` 集合是唯一的資料來源；不要再保存一份目前資料夾名稱的副本。

### 範例：帳單區段頁首

管理後台中某張發票的頁首。路徑說明發票位於何處，標題給出它的名稱，兩個按鈕對它執行操作。目前麵包屑是發票 ID，它是文字而非連結，因此使用者能看到位置，而不會出現一個指回同一頁面的連結。標籤「發票位置」把這個地標與側邊欄中的導覽區分開。

![帶發票麵包屑、標題和兩個操作的帳單頁面頁首](images/InvoiceHeader.png)

```xml
<StackPanel Width="640" Spacing="12">
    <arc:Breadcrumb Label="Invoice location">
        <arc:BreadcrumbItem Content="Acme Analytics" />
        <arc:BreadcrumbItem Content="Billing" />
        <arc:BreadcrumbItem Content="Invoices" />
        <arc:BreadcrumbItem Content="inv_2026_0412" />
    </arc:Breadcrumb>
    <TextBlock Text="Invoice inv_2026_0412"
               FontSize="24"
               FontWeight="SemiBold"
               Foreground="{arc:ArcDesignTokenResource Foreground}" />
    <TextBlock Text="Northwind Trading, issued April 3, 2026. Paid on April 9, 2026."
               FontSize="14"
               TextWrapping="Wrap"
               Foreground="{arc:ArcDesignTokenResource TextMuted}" />
    <StackPanel Orientation="Horizontal" Spacing="8">
        <arc:Button Content="Download PDF" Variant="Secondary" />
        <arc:Button Content="Send receipt" />
    </StackPanel>
</StackPanel>
```

### 文案規範

- **麵包屑標籤**：使用者所熟悉的位置名稱；專有名詞用標題式大寫（「Acme Analytics」），一般區段用句首大寫（「Billing」）。每個標籤控制在一到兩個詞以內，使路徑保持在一行。
- **目前麵包屑**：頁面的名稱，而不是一句話。它會重複標題，因此兩者保持一致。
- **地標標籤**：一個簡短的名詞片語，說明路徑，例如「發票位置」或「資料夾路徑」。在已翻譯的介面中避免使用「麵包屑」，直接說明內容。
- **溢出標籤**：說明按鈕會顯示什麼（「顯示隱藏的路徑」），而不是它由什麼構成。

| 不要寫 | 改為 |
|---|---|
| 地標標籤 "Breadcrumb" | 發票位置 |
| 目前麵包屑做成指向同一頁面的連結 | 目前頁面名稱，不加連結 |
| 溢出標籤 "More" | 顯示隱藏的路徑 |
| 麵包屑 "前往帳單總覽頁面" | 帳單 |
| 麵包屑 "目前頁面：發票" | 發票 |

### 最佳做法

**建議**

- 把麵包屑放在頁面標題上方，並讓標題與目前麵包屑使用相同的名稱，使路徑與標題一致。
- 當頁面有多個導覽地標時，為每個麵包屑設定 `Label`，使輔助技術使用者能夠區分它們。
- 應用程式內的導覽使用 `ItemClick`，由檢視模型決定導覽方式，麵包屑本身只是一個簡單的請求。
- 對於可能超出一欄寬度的路徑，設定 `MaxVisibleItems`，並在頁面所支援的最窄寬度下測試路徑。

**避免**

- 用麵包屑表示同層檢視或步驟。使用者期望麵包屑指向階層的上方；而同層檢視或步驟會把他們帶向旁邊或前方，使路徑令人困惑。
- 把目前頁面做成連結。最後一個麵包屑是使用者所在的頁面；指向它的連結會重新載入同一檢視，並讓目前狀態更難辨認。
- 在程式碼後置中用字串建立路徑，再複製到檢視中。應將路徑繫結到同一個集合，使麵包屑與內容同步變化。
- 在麵包屑中放入控制項或長句子。麵包屑不會換行顯示自己的標籤，因此一句長話會讓該麵包屑遠寬於其他，並把路徑擠到第二行。

### 常見問題

**為什麼目前麵包屑對點按沒有反應？** 最後一個麵包屑是目前頁面，因此它不可互動，沒有懸停底線，也不在 Tab 順序中。其 `IsCurrent` 值跟隨位置：路徑中最後的那個麵包屑即為目前。

**如何讓中間的麵包屑不成為連結？** 在該 `BreadcrumbItem` 上設定 `IsClickable="False"`。它仍保留在路徑中，標籤也仍然可見，但它是純文字，無法被選取。

**如何知道選取了哪個麵包屑，怎樣取得資料項目？** 在 `ItemClick` 處理常式中讀取 `e.Index` 和 `e.Item`。對於 `ItemsSource` 路徑，`Item` 是資料物件；對於內嵌的麵包屑，它是 `BreadcrumbItem`。

**路徑變化時溢出選單會關閉嗎？** 會。變更項目或 `MaxVisibleItems` 會關閉選單，因此在選單開啟時到達的路徑更新不會在畫面上留下過期的清單。

**路徑可以顯示多行嗎？** 可以。當路徑寬於可用空間時，麵包屑會換到新的一行，行與行之間相距 2 px。路徑可能變長時，優先使用 `MaxVisibleItems`，因為換行的路徑較難快速瀏覽。

## API 參考

<!-- api: Breadcrumb -->

## 無障礙

- 根部是一個由 `Label` 命名的導覽地標，預設為「麵包屑」。其麵包屑組成一個清單，每一項都會播報其位置和路徑的大小，例如「第 2 項，共 4 項」。
- 目前麵包屑會被播報為目前頁面。它是文字，而不是按鈕或連結，且不能取得焦點。
- 帶有 `NavigateUri` 的麵包屑會被播報為連結；其他可以被選取的麵包屑會被播報為按鈕。
- 尖括號形分隔符號對輔助技術隱藏。
- 溢出按鈕由 `OverflowLabel` 命名，預設為「顯示隱藏的路徑」。被隱藏的麵包屑會以各自的名稱出現在其選單中。

### 鍵盤

| 按鍵 | 操作 |
|---|---|
| `Tab` / `Shift+Tab` | 在可選取的麵包屑與溢出按鈕之間移動；目前麵包屑會被跳過 |
| `Left` / `Right` | 將焦點移到上一個或下一個可選取的麵包屑 |
| `Enter` / `Space` | 選取取得焦點的麵包屑，這會引發 `ItemClick`，並執行其命令或開啟其位址 |
| `Down` / `Up` | 在溢出選單中移到下一個或上一個項目，到達兩端時循環 |
| `Home` / `End` | 在溢出選單中移到第一個或最後一個項目 |
| 溢出選單中的 `Enter` / `Space` | 選取該項目；選單關閉，焦點返回按鈕 |
| `Escape` | 在溢出選單中關閉選單，並將焦點返回溢出按鈕 |

在麵包屑上按下並放開指標，若放開發生在該麵包屑內，則選取它。

## 動態效果

- 新的麵包屑從左側滑入並逐漸清晰，同時其同層項目以平滑彈簧讓出空間。
- 被移除的麵包屑向左移動 4px 並快速淡出。其餘麵包屑讓出空間。
- 路徑未變化時，其版面不播放動畫。
- 減少動態效果和關閉動態效果會立即套用新增和移除的麵包屑。滑入與退出僅在完整動態效果下執行。

| 變化 | 完整 | 減少 | 關閉 |
|---|---|---|---|
| 在末尾新增麵包屑 | 從左側滑入 8 px，並在標準時長內逐漸清晰 | 立即顯示 | 立即顯示 |
| 移除麵包屑 | 向左移動 4 px，在 120 ms 內淡出並模糊消失 | 立即移除 | 立即移除 |
| 變化之後的麵包屑 | 以平滑彈簧滑至新位置 | 跳至新位置 | 跳至新位置 |
| 懸停、焦點、目前 | 標籤顏色漸變 | 立即完成 | 立即完成 |

## 相關元件

- `Tabs`：同一物件的同層檢視。
- `TreeView`：瀏覽整個階層結構。
- `Pagination`：翻閱結果的各頁。

| 元件 | 適用情境 |
|---|---|
| `Tabs` | 檢視是同一物件的同層，且每個檢視都有自己的面板 |
| `TreeView` | 使用者透過展開和摺疊節點來瀏覽階層結構 |
| `Pagination` | 結果是同一集合，並被分成多頁 |
| `Stepper` | 使用者依固定順序完成步驟 |
| `PageHeader`（區塊） | 頁面需要在一個標準頁首中包含標題、麵包屑和操作 |
| `Select` / `Combobox` | 使用者在表單中從清單中選取一個值 |
