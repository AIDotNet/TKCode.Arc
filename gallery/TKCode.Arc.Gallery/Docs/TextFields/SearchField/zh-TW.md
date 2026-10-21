---
title: 搜尋欄位
summary: 易於辨識的搜尋入口，並帶有清楚的操作提示。
source-hash: 72dd93e3ef34
---

<Demo name="FilterList" />

## 何時使用

`SearchField` 是用於已經顯示著可搜尋項目的檢視的搜尋欄位：客戶表格、發票清單、帶有大量開關的設定頁。它位於結果上方，隨使用者輸入逐步縮小結果；清除按鈕出現時寬度保持不變，因此使用者操作時周圍的工具列不會跳動。

- **在同一頁面篩選客戶或發票表格**：表格就在畫面上，每次按鍵都會立即縮小列，使用者馬上能看到效果。在 `TextChanged` 處理常式中篩選，表格的排序和分頁保持不變。
- **在很長的設定頁中尋找設定或功能開關**：一個有四十個開關的設定頁，可以在使用者輸入 「webhook」或「export」等詞時隱藏不符合的列，並保留分組標題。
- **在管理主控台中依 ID 開啟記錄**：操作員貼上 `inv_2026_0412` 並按 Enter。`SearchSubmitted` 會帶出文字，因此主控台可以直接開啟該記錄，而不是篩選清單。
- **資料密集工具中的精簡工具列**：帶篩選列的監控或帳單表格使用 `Size="Small"`，讓搜尋欄位與其他工具列控制項在 36 px 的列中對齊。
- **一鍵清除的搜尋**：Escape 可以從鍵盤清除搜尋欄位，清除按鈕會把焦點送回搜尋欄位，因此操作員不用滑鼠就能完成搜尋、清除、再搜尋。

## 何時不用

- **點選後會開啟結果面板的精簡型頁首搜尋**：使用 `ExpandingSearch`。它以圖示開始，依需要展開，並把結果歸組顯示在搜尋欄位下方。
- **搜尋文字需要當作表單值儲存**：使用 `Combobox`，它讓使用者從清單中選擇，保存的是所選值，而不是自由輸入的查詢字詞。
- **並排放置的多個篩選條件與排序選項**：使用 `FilterToolbar`，它把搜尋欄位與其他篩選控制項排在一起並保持對齊。
- **不是查詢的名稱、標題或備註**：使用 `Input`。搜尋欄位會在 Escape 時清除、在 Enter 時送出，這並不是名稱或備註需要的行為。
- **多行文字，例如給客服助理的長問題**：使用 `Textarea`。

## 用法

搜尋欄位開頭有一個搜尋圖示。欄位中有文字時，結尾會出現清除按鈕。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:SearchField Label="搜尋人員"
                     Placeholder="依姓名篩選"
                     Width="360"
                     HorizontalAlignment="Left" />
</UserControl>
```

`SearchField` 衍生自共用的 Arc 欄位基底類別，該基底建立在 Avalonia 的 `TextBox` 之上。`Text` 是查詢字詞且為雙向屬性，`TextChanged` 在每次按鍵時觸發，`PlaceholderText`、`IsReadOnly`、游標、選取、復原、輸入法和剪貼簿都與文字方塊的表現一致。欄位在此之上加入標籤、搜尋圖示、清除按鈕與 Arc 外框。它還透過 `Description` 和 `ErrorMessage` 提供 `Input` 的輔助文字列與錯誤列，因此當搜尋因某個必須讓使用者看到的原因失敗時，可以使用與表單欄位相同的訊息列。

### 結構

1. **標籤**（`Label`）：14 px、中等字重，位於外框上方 8 px。它是欄位的無障礙名稱。只有當可見的標題已經為搜尋命名時，才用 `IsLabelVisible="False"` 隱藏它，例如帶有明確標題的工具列。
2. **搜尋圖示**：位於外框開頭、18 px 的搜尋圖示，描邊為 2 px。靜止時為淡化色，欄位取得焦點或有文字時變為完整前景色。它對輔助技術隱藏。
3. **文字與預留位置文字**（`Placeholder`）：查詢字詞。欄位為空時以淡化文字顏色顯示預留位置文字，使用者開始輸入之前搜尋圖示也保持淡化。
4. **清除槽**：外框末尾一律保留一個 24 px 的方形槽位。24 px 的清除按鈕（中間是 16 px 的叉號）只在欄位有文字且可編輯時出現在槽位中。由於槽位一律存在，欄位寬度保持不變。
5. **輔助文字列與錯誤列**（`Description`、`ErrorMessage`）：與 Field 共用的訊息列，外框下方 12 px 的文案，行為與 `Input` 相同。

### 尺寸

`Size` 設定外框高度：`Small` 為 36 px，`Medium` 為 44 px（預設），`Large` 為 50 px。`Large` 還會把文字提升到基礎字級及其行高，因此作為頁面主搜尋時閱讀效果更好。

<Demo name="Sizes" />

每個工具列只用一種尺寸。密集的管理表格和篩選列用 `Small`，清單上方的搜尋用 `Medium`，頁面主要操作之一的搜尋（例如目錄或說明中心）用 `Large`。工具列中的欄位應與旁邊的控制項使用相同的 `Size`，讓整列共用同一高度。

### 繫結查詢字詞

`Text` 是雙向屬性，因此檢視模型可以讀取查詢字詞、重設它，並在頁面導覽之間保留它。使用預設繫結模式，由檢視模型篩選它所擁有的列。

```csharp
public partial class CustomerListViewModel : ObservableObject
{
    private static readonly string[] AllCustomers =
    [
        "Acme Analytics", "Acme Labs", "Acme Retail Group", "Northwind Logistics",
        "Contoso Health", "Fabrikam Energy"
    ];

    [ObservableProperty]
    private string _query = string.Empty;

    public ObservableCollection<string> Visible { get; } = new(AllCustomers);

    partial void OnQueryChanged(string value)
    {
        Visible.Clear();
        foreach (var name in AllCustomers.Where(n => n.Contains(value, StringComparison.CurrentCultureIgnoreCase)))
        {
            Visible.Add(name);
        }
    }

    [RelayCommand]
    private void ClearSearch() => Query = string.Empty;
}
```

```xml
<StackPanel Spacing="12">
    <arc:SearchField Label="搜尋客戶"
                     Placeholder="依姓名篩選"
                     Text="{Binding Query}"
                     Width="360"
                     HorizontalAlignment="Left" />
    <ItemsControl ItemsSource="{Binding Visible}" />
</StackPanel>
```

`ClearSearch` 是應用程式自己的命令；欄位本身透過清除按鈕和 Escape 清除，因此只有頁面其他位置有「清除」連結時才需要該命令。控制項本身沒有命令。

### 邊輸入邊篩選

`Text` 在每次按鍵時都會變動。在 `TextChanged` 處理常式中篩選，或在上面檢視模型的屬性變更中篩選。如果篩選成本較高，請在程式碼中做防彈跳處理，讓清單在使用者暫停後再更新。

```csharp
search.TextChanged += (_, _) =>
{
    var query = search.Text ?? string.Empty;
    list.Children.Clear();
    foreach (var name in names.Where(n => n.Contains(query, StringComparison.CurrentCultureIgnoreCase)))
    {
        list.Children.Add(new TextBlock { Text = name });
    }
};
```

### 送出

Enter 會觸發 `SearchSubmitted` 並帶出欄位中的文字，該事件被標記為已處理，因此 Enter 不會按下視窗中的預設按鈕。把它用於只開啟一個結果的搜尋，例如 ID 或工單號；不要用於已經隨每次按鍵更新的清單。

```csharp
search.SearchSubmitted += (_, e) => submitted.Text = $"已送出“{e.Query}”";
```

Escape 會清除有文字且非唯讀的欄位，並讓焦點留在欄位中。

### 從程式碼清除

呼叫 `Clear` 可以從程式碼清空欄位。除非傳入 `false`，焦點會保留在欄位中。這次變更會觸發一般的文字變動，因此 `TextChanged` 處理常式中的篩選也會執行。

```csharp
search.Clear();
search.Clear(focus: false);
```

### 篩選大型清單

欄位會把每次按鍵交給 `Text`。檢視模型應在記憶體中保留完整的資料集，並從該資料集而不是畫面上的列進行篩選，這樣清除查詢字詞就能恢復所有列。對於遠端資料來源，應使用 `SearchSubmitted` 事件或等待輸入暫停，而不是每次按鍵都發出一次要求。

```csharp
search.TextChanged += async (_, _) =>
{
    var query = search.Text ?? string.Empty;
    await Task.Delay(250);
    if (query != (search.Text ?? string.Empty))
    {
        return;
    }
    await LoadCustomersAsync(query);
};
```

延遲檢查會捨棄已被較新按鍵超越的查詢，因此緩慢的回應永遠不會覆蓋最新文字的結果。

### 範例：帶搜尋篩選的客戶表格

帳單主控台中的客戶表格。搜尋欄位位於表格上方，查詢字詞 `acme` 已輸入，因此清除按鈕和篩選後的列都可見。搜尋欄位與列是同一檢視中的同層元素，而不是另一個頁面，因此使用者能在同一個檢視中看到每次按鍵的效果。其下方的符合數量以文字說明結果，而不只是透過列來呈現。

![帶搜尋篩選、將表格縮減為三列符合結果的客戶搜尋](images/CustomerSearch.png)

```xml
<StackPanel Width="620" Spacing="16">
    <arc:SearchField Label="Search customers"
                     Placeholder="Filter by name or ID"
                     Text="acme" />
    <TextBlock Text="3 customers match “acme”."
               FontSize="14"
               Foreground="{arc:ArcDesignTokenResource TextSecondary}" />
    <Border BorderBrush="{arc:ArcDesignTokenResource Border}"
            BorderThickness="1"
            CornerRadius="8">
        <StackPanel>
            <Grid ColumnDefinitions="*,160,140" Margin="16,12">
                <TextBlock Text="Customer" FontWeight="SemiBold"
                           Foreground="{arc:ArcDesignTokenResource TextMuted}" />
                <TextBlock Grid.Column="1" Text="Customer ID" FontWeight="SemiBold"
                           Foreground="{arc:ArcDesignTokenResource TextMuted}" />
                <TextBlock Grid.Column="2" Text="Plan" FontWeight="SemiBold"
                           Foreground="{arc:ArcDesignTokenResource TextMuted}" />
            </Grid>
            <Border Height="1" Background="{arc:ArcDesignTokenResource Border}" />
            <Grid ColumnDefinitions="*,160,140" Margin="16,12">
                <TextBlock Text="Acme Analytics" Foreground="{arc:ArcDesignTokenResource Foreground}" />
                <TextBlock Grid.Column="1" Text="cus_1042"
                           Foreground="{arc:ArcDesignTokenResource TextSecondary}" />
                <TextBlock Grid.Column="2" Text="Enterprise"
                           Foreground="{arc:ArcDesignTokenResource Foreground}" />
            </Grid>
            <Border Height="1" Background="{arc:ArcDesignTokenResource Border}" />
            <Grid ColumnDefinitions="*,160,140" Margin="16,12">
                <TextBlock Text="Acme Labs" Foreground="{arc:ArcDesignTokenResource Foreground}" />
                <TextBlock Grid.Column="1" Text="cus_2031"
                           Foreground="{arc:ArcDesignTokenResource TextSecondary}" />
                <TextBlock Grid.Column="2" Text="Team"
                           Foreground="{arc:ArcDesignTokenResource Foreground}" />
            </Grid>
            <Border Height="1" Background="{arc:ArcDesignTokenResource Border}" />
            <Grid ColumnDefinitions="*,160,140" Margin="16,12">
                <TextBlock Text="Acme Retail Group" Foreground="{arc:ArcDesignTokenResource Foreground}" />
                <TextBlock Grid.Column="1" Text="cus_1187"
                           Foreground="{arc:ArcDesignTokenResource TextSecondary}" />
                <TextBlock Grid.Column="2" Text="Trial"
                           Foreground="{arc:ArcDesignTokenResource Foreground}" />
            </Grid>
        </StackPanel>
    </Border>
</StackPanel>
```

### 文案規範

為被搜尋的集合命名，並讓預留位置文字展示符合的範例，而不是指令。

| 不要寫 | 改為 |
|---|---|
| 搜尋 | 搜尋客戶 |
| 輸入以搜尋 | 依姓名或 ID 篩選 |
| 沒有結果 | 沒有與 “acme” 相符的客戶。 |
| 清除 | 清除搜尋 |
| 前往 | 開啟發票 |

使用句首大寫的句子式寫法。把符合數量以純文字放在欄位旁邊，並把空結果寫成一句重複查詢字詞的話，讓使用者看清哪些內容沒有符合。

### 最佳做法

**建議**

- 在標籤中為集合命名，例如「搜尋客戶」而不是「搜尋」。使用者知道查詢字詞作用於什麼，螢幕閱讀器也會播報同樣的名稱。
- 從完整的資料集中篩選，而不是從畫面上的列中篩選。清除查詢字詞後即可恢復所有列。
- 在欄位旁邊顯示符合數量。使用者在掃視空清單之前就能看出查詢字詞沒有符合任何內容。
- 保持欄位寬度固定。預留的清除槽已經讓寬度保持穩定，因此不要在按鈕出現時改變欄位寬度。

**避免**

- 對遠端資料來源每次按鍵都送出一次要求。應對查詢字詞做防彈跳，或等待 Enter，因為對較早查詢字詞的回應可能晚於後面的回應抵達。
- 在已經隨每次按鍵篩選的清單中使用 `SearchSubmitted`。這樣 Enter 不會產生任何效果，使用者會不知道它有什麼用。
- 在搜尋欄位中放入一個已選值。應使用 `Combobox`，它保存的是選擇，而不是自由輸入的查詢。
- 只依賴 Escape 清除欄位。清除按鈕是可見的提示，而 Escape 並不容易被發現。

### 常見問題

**為什麼唯讀欄位中按 Escape 沒有效果？** Escape 只會清除有文字且非唯讀的欄位。清除按鈕遵循同樣的規則，因此 `IsReadOnly` 為 true 時它會隱藏。

**如何翻譯清除按鈕？** 設定 `ClearText`。預設值為 "Clear search"，它也是按鈕的無障礙名稱。

**`Clear` 會移動焦點嗎？** 預設會。`Clear(focus: false)` 會清除欄位並保留焦點所在位置，這與 Escape 的行為一致。

**欄位為空時按 Enter 會送出嗎？** 會。`SearchSubmitted` 會以空的 `Query` 觸發，因此如果空的送出應當不做任何事，請在處理常式中忽略空查詢字詞。

## API 參考

<!-- api: SearchField -->

## 無障礙

- `Label` 為欄位命名。搜尋圖示對輔助技術隱藏，因此欄位透過標籤和值被播報。
- 清除按鈕預設名稱為「清除搜尋」。設定 `ClearText` 可將其翻譯。
- 按 Enter 送出查詢字詞，按 Escape 從鍵盤清除欄位。
- 清除按鈕顯示時可用鍵盤操作，取得焦點時繪製焦點框。在其上按 Enter 會清除欄位並把焦點送回欄位，因此鍵盤使用者不會失去目前的位置。
- `Description` 與 `ErrorMessage` 構成欄位的說明文字，與 `Input` 一樣，在名稱和值之後朗讀。
- 如範例所示，在欄位旁邊以純文字顯示符合數量，讓使用者無需逐列計數即可看到結果。

### 鍵盤

| 按鍵 | 操作 |
|---|---|
| 文字按鍵 | 編輯查詢字詞；每次按鍵都會執行 `TextChanged` |
| `Enter` | 以查詢字詞觸發 `SearchSubmitted` 並將該按鍵標記為已處理 |
| `Escape` | 清除有文字且非唯讀的欄位；焦點保留在欄位中 |
| `Tab` / `Shift+Tab` | 清除按鈕顯示時移到它，然後移到下一個控制項 |
| 清除按鈕上的 `Enter` | 清除欄位並把焦點送回欄位 |
| `Left` / `Right`、`Home` / `End`、`Shift` 選取、`Ctrl+A` | 原生文字方塊編輯 |

## 動態效果

- 清除按鈕從略小的尺寸放大出現，並伴隨輕微的模糊。
- 其槽位一律保留，因此無論按鈕是否可見，欄位寬度都保持不變。
- 按下清除按鈕時，按鈕會略微縮小，僅在完整動態效果下生效。
- 減少動態效果時，清除按鈕只淡入淡出，沒有縮放也沒有模糊。關閉動態效果時立即顯示或隱藏。

| 變化 | 完整 | 減少 | 關閉 |
|---|---|---|---|
| 清除按鈕出現 | 以靈敏的彈簧從 0.8 放大出現，並伴隨柔和的模糊 | 淡入 | 立即完成 |
| 清除按鈕消失 | 先播放退出動畫再移除 | 淡出 | 立即完成 |
| 按下清除按鈕 | 縮放至 0.96 | 不縮放 | 不縮放 |

## 相關元件

- `ExpandingSearch`：頁首中的精簡搜尋，並在其下方顯示結果。
- `Combobox`：從清單中選擇一個值。
- `Input`：一般用途的單行輸入框。

| 元件 | 適用情境 |
|---|---|
| `ExpandingSearch` | 搜尋位於頁首或工具列中，只在使用時才展開 |
| `FilterToolbar` | 搜尋與其他篩選、排序選項或檢視切換放在一起 |
| `Combobox` | 所選值需要儲存在表單中，而不是當作查詢字詞 |
| `Input` | 文字是名稱、標題或值，而不是查詢字詞 |
| `Textarea` | 查詢字詞需要多行，例如給助理的問題 |
| `Select` | 使用者從固定清單中選擇一個選項，而不是輸入 |
