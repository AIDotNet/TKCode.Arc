---
title: 分頁
summary: 在有明確邊界的長集合中翻頁。
source-hash: b2a74f41b993
---

<Demo name="Default" />

## 何時使用

`Pagination` 讓使用者在應用程式分頁載入的同一個集合中前後翻頁：帳務主控台中的發票清單、門市後台的訂單表格、CRM 的搜尋結果。控制項保存目前的頁碼與總頁數，繪製上一頁、下一頁按鈕以及一個由五個頁碼按鈕組成的視窗，資料的載入則交由應用程式負責。它位於所分頁的資料下方，也正是使用者尋找下一批資料的位置。

- **帳務主控台中的發票與付款**：從伺服器每次載入 10 筆，共 248 張發票。頁尾顯示「第 12 頁，共 25 頁」，上一頁與下一頁按鈕在兩端停用，頁碼保存在檢視模型中，因此重新整理後使用者仍停留在同一頁。
- **門市後台的訂單與退貨**：訂單表格翻閱伺服器結果，頁碼也是客服溝通時引用的數字（「退貨佇列第 4 頁」）。
- **內部工具中的搜尋結果**：結果集太長而無法捲動，總數已知，每一頁都是一次獨立的要求。
- **總數已知的稽核紀錄與事件串流**：頁數來自伺服器回傳的總數，因此使用者在點擊之前就能看到清單的邊界。

## 何時不用

- **瀏覽項目的投影片**，例如商品圖片或引導卡片：使用 `Carousel`，它讓使用者一次檢視一個項目並逐一切換。
- **隨捲動閱讀的長資訊流**，例如活動串流或持續增長的紀錄：在 `ScrollArea` 這類可捲動容器中使用 `Timeline`。頁碼按鈕會打斷資訊流的閱讀節奏。
- **跳到層級結構中的某個位置**，例如從「帳務」到「發票」再到某一張發票：使用 `Breadcrumb`，它顯示通往目前頁面的路徑。
- **在同一個物件的不同檢視之間切換**，例如概覽、活動與設定：使用 `Tabs`。
- **選取一段日期或數值範圍**：使用 `DateRangePicker` 或 `Slider`。頁碼無法描述一段數值範圍。

## 用法

繫結 `CurrentPage` 與 `PageCount`。控制項會渲染一個包含五個頁碼按鈕的視窗，視窗隨目前頁移動。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:Pagination PageCount="{Binding PageCount}"
                    CurrentPage="{Binding CurrentPage, Mode=TwoWay}" />
</UserControl>
```

`Pagination` 衍生自 Avalonia 的 `TemplatedControl`，而不是清單控制項。它沒有 `ItemsSource`，也從不載入或計數任何內容：應用程式負責計算總頁數並回應頁碼變更。`CurrentPage` 預設為雙向繫結，因此 `Mode=TwoWay` 可以省略。兩個值都是一般整數，所以同一個控制項既可以使用伺服器回應中的總數，也可以使用本機清單的數量或常數。

### 結構

1. **地標**（`Label`）：導覽地標，預設名稱為「Pagination」。它不會被繪製。
2. **上一頁按鈕**（`PreviousLabel` 為其無障礙名稱）：36 px 見方的按鈕，內含 16 px 的左箭頭。在第一頁時停用，但位置保持不變。
3. **頁碼按鈕**：五個 36 px 見方的按鈕，彼此相距 4 px。數字使用等寬數字，字級為特小號，顏色為次要文字色，圓角與控制項一致。
4. **目前頁標記**：表面色的膠囊形狀，帶 1 px 邊框與靜止陰影。它位於目前數字之下，頁碼變更時滑動到下一個數字。
5. **邊界按鈕與省略號**（`ShowBoundaryPages`）：首頁與末頁按鈕，以及視窗兩側各一個 24 px 的省略號。只有當視窗未到達兩端時才會出現。
6. **下一頁按鈕**（`NextLabel` 為其無障礙名稱）：與上一頁按鈕對稱，在末頁時停用。
7. **焦點環**：2 px 的焦點色圓環，繪製在按鈕外側 4 px 處。只在鍵盤聚焦時出現。

控制項將各項目排成一列並保持靠左對齊。停用的按鈕降至 40% 不透明度，並顯示「不允許」游標。

### 回應頁碼變更

處理 `PageChanged` 以載入頁面。`NewPage` 是使用者要求的頁碼，`OldPage` 是變更之前的頁碼。只要 `CurrentPage` 變更就會引發此事件，因此由程式碼造成的變更（例如繫結或篩選後的重設）也會引發它。

```csharp
pagination.PageChanged += async (_, e) =>
{
    status.Text = $"第 {e.NewPage} 頁，共 {pagination.PageCount} 頁";
    await LoadPageAsync(e.NewPage);
};
```

`NewPage` 是 `CurrentPage` 的原始值，而不是限制範圍之後的值。如果程式碼在只有 25 頁的清單上把 `CurrentPage` 設為 99，那麼 `NewPage` 為 99，而按鈕顯示的是第 25 頁。可以在處理常式中限制範圍，或在檢視模型中讓 `CurrentPage` 保持在有效範圍內（見下文）。

### 邊界頁

`ShowBoundaryPages` 讓首頁與末頁都只需一次點擊即可到達，並在它們與視窗之間顯示省略號。它是 Arc 規範之外的延伸功能，預設關閉。只有當 `PageCount` 大於五時邊界按鈕才會出現，而且每個按鈕只在視窗尚未到達其端點時出現。適用於使用者可能需要快速跳到首頁或末頁的長清單，例如從最新項目開始閱讀的紀錄。

<Demo name="BoundaryPages" />

### 頁數很少或沒有頁

頁數少於五頁時全部顯示，兩端的箭頭會被停用。`PageCount` 為 `0` 時會渲染一個空的導覽地標：頁碼按鈕與箭頭都會隱藏，只保留地標。結果為空時使用零值，並以空狀態取代此控制項。

<Demo name="FewPages" />

<Demo name="NoPages" />

### 標籤

`Label` 為導覽地標命名。`PreviousLabel`、`NextLabel` 與 `PageLabelFormat` 為輔助技術的按鈕命名。當預設文字不是使用者所用的語言時，請進行設定。`PageLabelFormat` 接受一個 `{0}` 預留位置表示頁碼，並依目前的文化格式化。

```xml
<arc:Pagination Label="結果分頁"
                PreviousLabel="上一頁"
                NextLabel="下一頁" />
```

### 繫結到檢視模型

發票清單在檢視模型中保存目前頁與總頁數。頁數由伺服器回傳的總數推導，目前頁變更時載入對應的列。使用 CommunityToolkit.Mvvm 時，使用者變更頁碼會觸發產生的部分方法，因此載入不依賴於檢視。

```csharp
public sealed record InvoiceRow(string Id, string Customer, string Amount, string Status);

public sealed record InvoicePage(int Total, IReadOnlyList<InvoiceRow> Rows);

public interface IInvoiceService
{
    Task<InvoicePage> GetPageAsync(int page, int pageSize);
}

public partial class InvoiceListViewModel : ObservableObject
{
    private const int PageSize = 10;
    private readonly IInvoiceService _invoices;

    [ObservableProperty]
    private int _currentPage = 1;

    [ObservableProperty]
    private int _pageCount;

    public ObservableCollection<InvoiceRow> Rows { get; } = new();

    public InvoiceListViewModel(IInvoiceService invoices)
    {
        _invoices = invoices;
    }

    partial void OnCurrentPageChanged(int value)
    {
        _ = LoadPageAsync(value);
    }

    private async Task LoadPageAsync(int page)
    {
        var result = await _invoices.GetPageAsync(page, PageSize);
        PageCount = (int)Math.Ceiling(result.Total / (double)PageSize);
        if (PageCount > 0 && page > PageCount)
        {
            // 清單縮短了：跳到最後一頁，由它來載入。
            CurrentPage = PageCount;
            return;
        }
        Rows.Clear();
        foreach (var row in result.Rows)
        {
            Rows.Add(row);
        }
    }
}
```

```xml
<arc:Pagination PageCount="{Binding PageCount}"
                CurrentPage="{Binding CurrentPage}"
                Label="發票分頁" />
```

`CurrentPage` 是雙向的，因此控制項會把使用者選擇的頁碼寫回檢視模型，檢視模型則透過它自己的屬性做出回應。在這種設計中不需要 `PageChanged`。只有當檢視程式碼（而非檢視模型）必須對變更作出反應時才使用它，例如把清單捲動到頂端。

### 讓頁碼保持在範圍內

控制項會限制它所顯示的頁碼，但從不自行變更繫結的值。當 `PageCount` 減少到小於 `CurrentPage` 時（例如篩選移除了若干列），`CurrentPage` 會保持原值：按鈕顯示最後一頁，而檢視模型中的值已超出範圍。因此在總數變更時，應在檢視模型中把 `CurrentPage` 設為 `PageCount`，如上例中的 `LoadPageAsync` 所示。

`EffectivePage` 是按鈕所顯示的頁碼，沒有頁時為 0。`WindowStart` 是五頁視窗中的第一個數字。`GoToPage(page)` 會跳到某一頁，並限制在有效範圍內；沒有任何變更時回傳 `false`。`NextPage()` 與 `PreviousPage()` 前進或後退一頁，到達兩端時回傳 `false`。在程式碼後置或鍵盤快速鍵中使用這些方法，就能套用與按鈕相同的規則。

```csharp
// 一個快速鍵，不需事先知道總數即可跳到最後一頁。
pagination.GoToPage(int.MaxValue);
```

### 範例：發票清單頁尾

一張發票表格及其分頁頁尾。頁尾以文字說明頁碼與總數，這是使用者在頁碼變更後最先確認的資訊；控制項放在右側，讓數字與箭頭成為一組。目前為第 12 頁，共 25 頁，因此視窗為 10 到 14，兩個箭頭都可用。標籤將地標命名為「發票分頁」，方便螢幕閱讀器使用者使用；列中的金額保留兩位小數，狀態以文字顯示，而不只依賴顏色區分。

![一張發票表格，頁尾顯示第 12 頁，共 25 頁以及分頁控制項](images/InvoiceFooter.png)

```xml
<StackPanel Width="680" Spacing="0">
    <Grid ColumnDefinitions="2*,2*,*,*" Margin="0,0,0,8">
        <TextBlock Grid.Column="0" Text="Invoice" FontSize="12" FontWeight="Medium"
                   Foreground="{arc:ArcDesignTokenResource TextMuted}" />
        <TextBlock Grid.Column="1" Text="Customer" FontSize="12" FontWeight="Medium"
                   Foreground="{arc:ArcDesignTokenResource TextMuted}" />
        <TextBlock Grid.Column="2" Text="Amount" FontSize="12" FontWeight="Medium"
                   Foreground="{arc:ArcDesignTokenResource TextMuted}" />
        <TextBlock Grid.Column="3" Text="Status" FontSize="12" FontWeight="Medium"
                   Foreground="{arc:ArcDesignTokenResource TextMuted}" />
    </Grid>
    <Grid ColumnDefinitions="2*,2*,*,*" Height="40">
        <TextBlock Grid.Column="0" Text="inv_2026_0412" FontSize="14" VerticalAlignment="Center" />
        <TextBlock Grid.Column="1" Text="Northwind Trading" FontSize="14" VerticalAlignment="Center" />
        <TextBlock Grid.Column="2" Text="$4,800.00" FontSize="14" VerticalAlignment="Center" />
        <TextBlock Grid.Column="3" Text="Paid" FontSize="14" VerticalAlignment="Center" />
    </Grid>
    <Grid ColumnDefinitions="2*,2*,*,*" Height="40">
        <TextBlock Grid.Column="0" Text="inv_2026_0411" FontSize="14" VerticalAlignment="Center" />
        <TextBlock Grid.Column="1" Text="Contoso Ltd" FontSize="14" VerticalAlignment="Center" />
        <TextBlock Grid.Column="2" Text="$1,250.50" FontSize="14" VerticalAlignment="Center" />
        <TextBlock Grid.Column="3" Text="Open" FontSize="14" VerticalAlignment="Center" />
    </Grid>
    <Grid ColumnDefinitions="2*,2*,*,*" Height="40">
        <TextBlock Grid.Column="0" Text="inv_2026_0410" FontSize="14" VerticalAlignment="Center" />
        <TextBlock Grid.Column="1" Text="Fabrikam" FontSize="14" VerticalAlignment="Center" />
        <TextBlock Grid.Column="2" Text="$920.00" FontSize="14" VerticalAlignment="Center" />
        <TextBlock Grid.Column="3" Text="Overdue" FontSize="14" VerticalAlignment="Center" />
    </Grid>
    <Grid ColumnDefinitions="*,Auto" Margin="0,20,0,0">
        <TextBlock Grid.Column="0" Text="Page 12 of 25, 248 invoices" FontSize="12"
                   VerticalAlignment="Center"
                   Foreground="{arc:ArcDesignTokenResource TextMuted}" />
        <arc:Pagination Grid.Column="1" Label="Invoice pages" PageCount="25" CurrentPage="12" />
    </Grid>
</StackPanel>
```

### 文案規範

- **摘要文字**：在控制項旁以文字說明範圍：「第 12 頁，共 25 頁，248 張發票」或「顯示第 111 至 120 筆，共 248 筆」。控制項只顯示數字，文字讓掃視頁尾的使用者理解其含義。
- **標籤**：為清單命名，而不是為控制項命名：「發票分頁」「結果分頁」。地標名稱會被朗讀，只說「Pagination」的話，螢幕閱讀器使用者無法得知內容。
- **按鈕名稱**：除非應用程式的語言不同，否則保留預設的「上一頁」與「下一頁」。只有當預設的「第 {0} 頁」讀起來不合適時，才用 `{0}` 預留位置來格式化頁碼標籤，例如「前往第 {0} 頁」。
- **空結果**：結果為零時不要顯示分頁控制項。顯示空狀態，並用一句話說明搜尋的內容。

| 不要寫 | 改為 |
|---|---|
| 標籤 「Pagination」 | 發票分頁 |
| 頁尾 「12 / 25」 | 第 12 頁，共 25 頁，248 張發票 |
| 上一頁（只有圖示，沒有名稱） | 上一頁 |
| 空表格下方的分頁 | 沒有符合這些篩選條件的發票。 |

### 最佳做法

**建議**

- 在檢視模型中讓 `CurrentPage` 保持在有效範圍內；總數減少時把它設為 `PageCount`，使繫結的值始終與按鈕顯示的一致。
- 在檢視模型中依頁碼載入資料，使重新整理與深層連結都能到達同一頁。
- 在控制項旁以文字顯示範圍與總數，讓使用者在翻頁之前就知道共有多少列。
- 為頁面上的每個分頁設定 `Label`，使同一畫面上的兩個清單被讀成兩個地標。

**避免**

- 顯示並非來自伺服器總數的頁數。根據已載入的列計算出的數量會隱藏清單的其餘部分，並讓最後一頁看起來就是結尾。
- 在下一頁資料抵達之前清空列。要求進行期間保持目前的列顯示，並在頁尾文字中顯示變更，讓使用者看到的是進度，而不是空白表格。
- 在短清單上使用 `ShowBoundaryPages`。它增加的省略號與按鈕對只有三頁的清單沒有必要。
- 在總數仍在載入時設定 `PageCount`。為零或過期的數量會讓箭頭因錯誤的原因被停用。

### 常見問題

**控制項允許使用者輸入頁碼或變更每頁筆數嗎？** 不允許。Arc 的分頁是受控的：它只顯示視窗與箭頭。若需要每頁筆數選擇，可以在旁邊加一個 `Select`，再加一個跳頁用的 `NumberField`，並在它們的處理常式中呼叫 `GoToPage`。

**在程式碼中設定 `CurrentPage` 時，為什麼會引發 `PageChanged`？** 此事件由屬性變更引發，因此無論變更來自使用者還是程式碼都會引發。在開始要求之前，把 `e.NewPage` 與上次載入的頁碼比較，避免同一頁被重複載入。

**`PageCount` 為 0 時會怎樣？** 頁碼按鈕與箭頭會隱藏，`EffectivePage` 為 0。導覽地標仍保留在控制項樹中，因此只有在確實有內容可翻頁時才把它留在頁面上，否則在檢視中隱藏此控制項。

**一次能顯示超過五頁嗎？** 不能。視窗始終是五個頁碼按鈕（`WindowSize` 為 5）。對於首頁與末頁，使用 `ShowBoundaryPages`；其他情況請使用其他元件，或使用帶有自身捲動的清單。

## API 參考

<!-- api: Pagination -->

## 無障礙

- 此控制項是一個導覽地標，由 `Label` 命名，並會被朗讀為一組控制項。
- 每個頁碼按鈕由 `PageLabelFormat` 命名，例如「第 3 頁」。目前頁具有目前頁狀態。
- 上一頁與下一頁按鈕在兩端被停用，但保留其名稱。
- Tab 鍵在上一頁按鈕、頁碼按鈕與下一頁按鈕之間移動。Enter 或空白鍵進入焦點所在的頁碼。左右方向鍵在按鈕之間移動焦點。
- 省略號與隱藏的按鈕不可聚焦，並對輔助技術隱藏。
- 鍵盤焦點跟隨數字：頁碼按鈕取得焦點時視窗移動，焦點會隨該數字移動到它的新位置。

### 鍵盤

| 按鍵 | 操作 |
|---|---|
| `Tab` / `Shift+Tab` | 在上一頁按鈕、可見的頁碼按鈕與下一頁按鈕之間移動；略過停用的箭頭 |
| `Enter` / `Space` | 前往焦點所在的頁碼，或在箭頭上前往上一頁或下一頁 |
| `Left` / `Right` | 把焦點移到上一個或下一個可用的按鈕；按下 Enter 或 Space 之前頁碼不會改變 |

## 動態效果

- 單一目前頁標記以 morph 彈簧滑動到所選按鈕。
- 視窗移動時，數字依移動的槽位數像傳送帶一樣滑動，新數字淡入。
- 減少動態效果時，標記立即移動，數字原位替換。關閉動態效果時立即套用變更。

| 變化 | 完整 | 減少 | 關閉 |
|---|---|---|---|
| 目前頁變更 | 標記以 morph 彈簧滑動到新數字 | 標記立即移動 | 立即完成 |
| 視窗依若干槽位移動 | 數字依移動的槽位數滑動（每個 40 px），使用 smooth 彈簧；新數字淡入 | 數字原位替換 | 立即完成 |
| 數字離開視窗 | 滑入並在 120 ms 內淡出 | 立即移除 | 立即完成 |
| 按下箭頭 | 按住時箭頭縮放至 96% | 沒有按下縮放 | 沒有按下縮放 |
| 懸停、焦點 | 背景色與文字顏色漸變 | 立即完成 | 立即完成 |

## 相關元件

- `Tabs`：用於在檢視之間切換，而不是在一個集合的頁面之間切換。
- `Breadcrumb`：用於顯示頁面在層級結構中的位置。
- `Carousel`：用於逐張瀏覽投影片。

| 元件 | 適用情境 |
|---|---|
| `Tabs` | 各檢視是同一個物件的並列檢視，且各自有獨立的面板 |
| `Breadcrumb` | 頁面位於層級結構中，使用者需要沿路徑返回上層 |
| `Carousel` | 項目一次檢視一個，例如商品圖片 |
| `Timeline` | 資訊串流或紀錄依順序閱讀，清單隨捲動增長 |
| `ScrollArea` | 長清單透過捲動閱讀，而不是透過翻頁 |
| `Select` / `NumberField` | 頁尾還需要每頁筆數選擇或跳頁欄位 |
| `DateRangePicker` / `Slider` | 使用者選擇的是一段日期或數值，而不是頁碼 |
