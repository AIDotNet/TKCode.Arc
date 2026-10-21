---
title: 手風琴
summary: 在原位逐步展示補充資訊。
source-hash: 2e9810ba8b53
---

<Demo name="Default" />

## 何時使用

`Accordion` 是一欄問題列，每一列的答案都在原位展開。它適合讀者透過標題快速掃讀、只在需要的地方閱讀的內容：一個答案展開時其他答案保持收合，頁面長度不變，讀者也不會失去原來的位置。

- **說明中心與帳單常見問題**：為 SaaS 產品回答「之後可以更改方案嗎？」或「退款如何計算？」的頁面。同一時間只展開一個答案，讀者無需離開頁面即可比較各個問題。
- **帶分組說明的設定頁**：管理後台中的「資料保留」或「單一登入」等設定區塊，每個標題背後都隱藏著某項選擇的後果。控制項保持可見，手風琴負責承載原因說明。
- **結帳與訂單確認**：「收件地址」「付款方式」和「稅務資訊」作為收合的列出現，客戶只需展開想要核對的那一項，再下單。
- **開發者文件側邊欄**：與 API 參考並排的「速率限制」「分頁」和「Webhook 重試」清單，每個答案是幾段簡短文字加一個程式碼範例。

## 何時不用

- **單獨一個展開區塊**，例如定價卡片上的「顯示方案詳情」面板：用 `ExpandableCard`，它會保持卡片標題可見，並且可以橫向展開。
- **同一資料的並列檢視**，例如某個帳號的「總覽」「用量」和「發票」：用 `Tabs`，它一次只顯示一個面板，並把選項呈現為標籤清單。
- **只需要「閱讀更多」連結的短段文字**，例如商品描述或評價：用 `ExpandableText`，它會把文字截斷為指定的行數。
- **必須按順序完成的步驟**，例如設定精靈：用 `Stepper`，它會顯示進度與目前步驟。
- **供人比較或排序的記錄列**，例如按日統計的帳單或用量：用 `SortableDataTable`，它的各欄在各列之間保持對齊。

## 用法

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:Accordion>
        <arc:AccordionItem Header="免費方案包含哪些內容？">
            無限專案、三位協作者，以及一週的版本歷程。
        </arc:AccordionItem>
        <arc:AccordionItem Header="之後可以更改方案嗎？">
            可以。方案變更立即生效，差額會按比例計入下一期帳單。
        </arc:AccordionItem>
    </arc:Accordion>
</UserControl>
```

每一列都是一個 `AccordionItem`。`Accordion` 衍生自 Avalonia 的 `ItemsControl`，因此各列也可以來自 `Items` 或 `ItemsSource`；`AccordionItem` 衍生自 `HeaderedContentControl`，`Header` 是問題，`Content` 是答案。Arc 額外提供的是展開列的狀態（`ExpandedIndex`、`IsExpanded`）、單選或多選模式、尺寸，以及問題之間的鍵盤操作方式。按一下已展開的列會將其收合，展開另一列時，目前展開的列會自動收合。

### 結構

1. **清單分隔線**：第一列上方一條 1 px 的邊框顏色線。每一列在其下方繪製自己的 1 px 分隔線，因此清單的頂部以及各列之間都有分隔線。
2. **問題**（`Header`）：整列是一個按鈕。在 Medium 尺寸下至少 50 px 高，上下內距 8 px，使用小號字級（`TypeSM`）、中等字重。在 Large 尺寸下至少 76 px 高，上下內距 20 px，使用大號字級（`TypeLG`）。
3. **箭頭**：17 px 的向下箭頭。在 Medium 中位於問題之後 20 px，在 Large 中位於問題之後 32 px。列收合時為淡化文字顏色；列展開或指標懸停在列上時變為前景色；列展開時旋轉 180 度。
4. **答案**（`Content`）：次要文字顏色，Medium 下使用小號字級、1.4 的行高。Medium 答案右側保留 32 px、下方保留 20 px。Large 答案使用基礎字級、1.65 的行高，右側保留 48 px、下方保留 24 px，並在約 62 個字元（約 595 px）處換行。
5. **列分隔線**：每一列下方 1 px 的邊框顏色線。
6. **焦點環**：2 px 的焦點顏色環，水平方向在列外 4 px、垂直方向在列外 2 px，圓角 10 px。僅在鍵盤焦點時出現。

懸停只會改變問題文字（變為次要文字顏色）與箭頭顏色，尺寸不變。

### 尺寸

`Size` 設定手風琴中所有列的尺寸。`Medium`（預設）適合密集管理版面中的設定面板與說明抽屜；`Large` 適合頁面層級的常見問題，此時問題是頁面的標題，答案以內文字級閱讀。

```xml
<arc:Accordion Size="Large">
    <arc:AccordionItem Header="可以隨時取消嗎？">
        月付方案會保持有效，直到目前的計費週期結束。
    </arc:AccordionItem>
</arc:Accordion>
```

同一個清單只用一種尺寸。窄側邊欄中的 Large 清單會把答案擠成很長的一欄，此時應改用 Medium。尺寸同樣作用於之後新增的列，因為手風琴會為它準備的每一列設定尺寸。

<Demo name="Large" />

### 資料驅動的列

設定 `ItemsSource`，問題使用 `ItemTemplate`，答案使用 `ContentTemplate`。`ExpandedIndex = -1` 讓所有列從收合狀態開始，這是常見問題頁的常用做法：使用者提問之前，不應有任何答案處於展開狀態。

```xml
<arc:Accordion Size="Large"
               ExpandedIndex="-1"
               ItemsSource="{Binding Questions}">
    <arc:Accordion.ItemTemplate>
        <DataTemplate>
            <TextBlock Text="{Binding Question}" TextWrapping="Wrap" />
        </DataTemplate>
    </arc:Accordion.ItemTemplate>
    <arc:Accordion.ContentTemplate>
        <DataTemplate>
            <TextBlock Text="{Binding Answer}" TextWrapping="Wrap" />
        </DataTemplate>
    </arc:Accordion.ContentTemplate>
</arc:Accordion>
```

每個資料項目都應包含相同的兩個欄位：一個問題和一個答案。兩個範本會套用到每一列，無論展開還是收合，因此要保持範本輕量；關於如何按需載入較重的內容，請見「資料結構與效能」一節。

### 單列與多列展開

`ExpandMode` 決定可以同時展開幾列。`Single` 是 Arc 的預設值：同一時間只有一列展開，展開另一列會收合目前的列；已展開的列也可以被收合，使所有列都處於收合狀態。當使用者需要在多個答案中選擇一個來閱讀時使用它，例如常見問題。

`Multiple` 讓各列獨立地展開與收合。當使用者需要並排比較兩個答案（例如兩個方案的限額），或各列是某個類似表單頁面中彼此獨立的區塊時，使用它。

<Demo name="Multiple" />

在 `Multiple` 模式下，`ExpandedIndex` 仍然存在。它保存最後一次展開的列；該列收合時，它會移到第一個仍然展開的列上，若沒有任何列展開則為 `-1`。

要讓多列預設展開，請在每個 `AccordionItem` 上設定 `IsExpanded="True"`。寫成展開狀態的項目優先於 `ExpandedIndex` 的預設值，因此兩個設定不會互相衝突。

### 透過程式碼展開某一列

`ExpandedIndex` 與 `IsExpanded` 都是雙向的。可以將它們繫結到檢視模型屬性，也可以在程式碼中設定。`Toggle(int)` 會打開或收合指定索引處的列，`CollapseAll()` 會收合所有列，適合在搜尋或切換頁面之後用來重設。

```csharp
accordion.ExpandedIndex = 2;
item.IsExpanded = false;

accordion.Toggle(0);
accordion.CollapseAll();
```

### 繫結到檢視模型

FAQ 通常來自伺服器，而展開的列屬於頁面狀態。使用 CommunityToolkit.Mvvm 時，檢視模型同時持有問題清單和展開索引，手風琴同時繫結這兩者。

```csharp
public interface IBillingApi
{
    Task<string[]> GetInvoiceRulesAsync(string question);
}

public sealed partial class FaqEntry : ObservableObject
{
    public FaqEntry(string question, string answer)
    {
        Question = question;
        Answer = answer;
    }

    public string Question { get; }

    public string Answer { get; }

    /// <summary>首次展開該列時載入的明細列。</summary>
    [ObservableProperty]
    private string[]? _details;
}

public partial class BillingHelpViewModel : ObservableObject
{
    private readonly IBillingApi _billing;

    public BillingHelpViewModel(IBillingApi billing) => _billing = billing;

    [ObservableProperty]
    private int _openIndex = -1;

    [ObservableProperty]
    private ObservableCollection<FaqEntry> _entries = new()
    {
        new("我之後可以更改方案嗎？", "可以。方案變更立即生效，差額會按比例計入下一期帳單。"),
        new("退款如何運作？", "年付方案可在 30 天內退款。月付方案會保持有效，直到目前的計費週期結束。")
    };

    [RelayCommand]
    private void CollapseAll() => OpenIndex = -1;

    /// <summary>首次展開某列時，載入該答案的明細列。</summary>
    public async Task LoadDetailsAsync(FaqEntry entry)
    {
        if (entry.Details is null)
        {
            entry.Details = await _billing.GetInvoiceRulesAsync(entry.Question);
        }
    }
}
```

```xml
<StackPanel Width="640" Spacing="12">
    <Button Content="全部收合" Command="{Binding CollapseAllCommand}" HorizontalAlignment="Right" />
    <arc:Accordion Size="Large"
                   ExpandedIndex="{Binding OpenIndex, Mode=TwoWay}"
                   ItemsSource="{Binding Entries}">
        <arc:Accordion.ItemTemplate>
            <DataTemplate>
                <TextBlock Text="{Binding Question}" TextWrapping="Wrap" />
            </DataTemplate>
        </arc:Accordion.ItemTemplate>
        <arc:Accordion.ContentTemplate>
            <DataTemplate>
                <TextBlock Text="{Binding Answer}" TextWrapping="Wrap" />
            </DataTemplate>
        </arc:Accordion.ContentTemplate>
    </arc:Accordion>
</StackPanel>
```

`ExpandedIndex` 是需要繫結的雙向屬性：使用者的點按會把索引寫回 `OpenIndex`，而 `OpenIndex` 的變動會展開或收合對應的列。`ExpandMode` 與 `Size` 應使用單向繫結，因為它們在頁面中只設定一次。需要在每次展開時作出回應（例如記錄使用者閱讀了哪個答案）的檢視模型，可以在 `AccordionItem` 上使用 `ExpandedChanged`；該事件會冒泡，因此手風琴上的一個處理常式就能收到每一列的事件。

### 資料結構與效能

每一列都是一個帶有自身答案的 `AccordionItem` 容器，答案在該列收合時仍保持掛載。這樣第二次點按才能在展開過程中途反向，代價是收合列的開銷等同於其內容的開銷。答案應只包含文字與簡單控制項。當答案需要資料（例如一個小表格的列）時，應在列展開時由檢視模型載入，而不是為每一列預先載入：

```csharp
// 在檢視的初始化中：手風琴上的一個處理常式接收每一列的事件。
accordion.AddHandler(AccordionItem.ExpandedChangedEvent, OnRowExpandedChanged);

private void OnRowExpandedChanged(object? sender, RoutedEventArgs e)
{
    if (e.Source is AccordionItem { IsExpanded: true, DataContext: FaqEntry entry })
    {
        _ = _billingHelp.LoadDetailsAsync(entry);
    }
}
```

幾十列的 FAQ 對這個控制項來說是正常負載。長清單應按標題分組，或在檢視模型中先過濾再交給手風琴，因為每一列都是一個容器，需要與清單中的其他項目一起測量與排列。

### 範例：帳單說明中心

SaaS 管理後台帳單頁面中的一個說明面板。它預設展開第一個答案，因為大多數人進入時最先關心的就是第一個問題。問題都是客戶會輸入的完整句子，答案寫明了日期與金額；清單使用 Large 尺寸，因為它是頁面的主要內容。寬度 680 讓答案保持舒適的行長。

![帳單說明中心：第一個答案已展開，另外三個問題收合](images/BillingHelp.png)

```xml
<StackPanel Width="680" Spacing="16">
    <TextBlock Text="Billing questions"
               FontSize="20"
               FontWeight="SemiBold"
               Foreground="{arc:ArcDesignTokenResource Foreground}" />
    <arc:Accordion Size="Large" ExpandedIndex="0">
        <arc:AccordionItem Header="When is the Team plan charged?">
            Acme Analytics charges the Team plan on the first day of each billing period. Annual plans are
            charged once a year on the date the subscription started.
        </arc:AccordionItem>
        <arc:AccordionItem Header="Can I change plans later?">
            Yes. Upgrades apply at once, and the unused part of your current plan is credited on the next invoice.
        </arc:AccordionItem>
        <arc:AccordionItem Header="How do refunds work for annual plans?">
            Annual plans can be refunded within 30 days of the charge. Contact billing with the invoice ID,
            such as inv_2026_0412.
        </arc:AccordionItem>
        <arc:AccordionItem Header="Who can see invoices?">
            Owners and billing admins can open and download every invoice. Members see only the plan name.
        </arc:AccordionItem>
    </arc:Accordion>
</StackPanel>
```

### 文案規範

- **問題**：句首大寫的完整問句，以問號結尾，寫法與客戶會提出的問題一致。問題應點明主題，讓使用者不必展開就能掃讀清單。
- **答案**：第一句直接給出答案，之後是細節。用數字寫明日期、金額與限額（「30 天」「每個席位 $24」）。答案保持在一到兩段簡短文字；更長的內容應放到連結的頁面中。
- **設定清單中的標題**：點明設定項目的名詞片語，後果寫在答案裡。
- **長度**：問題應能在所選尺寸下放在一行內。較長的問題讀起來像需要掃讀的句子，在 Large 尺寸下會換行。

| 不要寫 | 改為 |
|---|---|
| 資訊 | 年付方案如何退款 |
| 點按閱讀 | 我之後可以更改方案嗎？ |
| 隨時都可以，您可以這樣做。 | 可以。方案變更立即生效。 |
| 帳單 | 誰可以查看發票？ |
| 更多內容請閱讀文件。 | 擁有者與帳務管理員可以下載每一張發票。 |

### 最佳做法

**建議**

- 把最有用的問題放在最前面，並且當頁面只討論一個主題時預設展開它（`ExpandedIndex="0"`），讓使用者不用點按就能看到真正的答案。
- 長的常見問題清單從 `ExpandedIndex="-1"` 開始，讓頁面打開時就是一份可掃讀的問題清單。
- 每個問題只對應一個答案，避免一列裡藏著使用者可能錯過的第二個主題。
- 只有當使用者需要比較答案時才使用 `Multiple`。兩列同時展開便於比較，五列同時展開就會開始爭奪螢幕空間。

**避免**

- 把必要的步驟（例如法律條款，或需要處理的錯誤）藏在問題後面。使用者可能永遠不會展開它，因此必要的文字應直接顯示在頁面上。
- 同一個清單中混用 `Medium` 與 `Large` 的列。兩種尺寸的高度與字級不同，清單會顯得不一致。
- 在答案中放置表單。收合的列中，欄位在展開前無法操作，而收合的答案中出現的驗證訊息很容易被忽略。應把表單放在頁面上。
- 頁面打開時就為每一列載入大量內容。應在列展開時再載入，如「資料結構與效能」中所示。
- 把手風琴當作頁面之間的導覽。各列只在原位切換；若需要獨立的檢視，請使用 `Tabs` 或導覽清單。

### 常見問題

**如何讓兩列預設展開？** 在每個應當展開的 `AccordionItem` 上設定 `IsExpanded="True"`。寫成展開狀態的項目優先於 `ExpandedIndex` 的預設值，因此這些列不需要 `ExpandedIndex`。如果希望使用者能同時保持兩列展開，請使用 `Multiple`。

**為什麼按一下已展開的列會將其收合？** 已展開的列會在它自己的問題上收合，以便使用者把所有列都收合。此時 `ExpandedIndex` 變為 `-1`。如果要讓某一列始終保持展開，可以在檢視模型中，當索引變為 `-1` 時把 `OpenIndex` 設回原來的索引；或者接受收合，作為設計的一部分。

**如何知道使用者展開了哪個答案？** 在手風琴上訂閱 `AccordionItem.ExpandedChangedEvent`（它會從每一列冒泡），並把 `e.Source` 當作 `AccordionItem` 讀取。當列來自 `ItemsSource` 時，它的 `DataContext` 就是資料項目。

**答案裡可以放按鈕或連結嗎？** 可以。`Content` 接受任何控制項。收合的答案處於惰性狀態，在它所在的列展開之前，無法被點按或取得焦點。

## API 參考

<!-- api: Accordion -->

<!-- api: AccordionItem -->

### 列舉值

`AccordionExpandMode` 設定可以展開的列數。

| 值 | 含義 |
|---|---|
| `Single` | 同一時間只有一列；展開另一列會收合它，已展開的列也可以被收合（Arc 預設值） |
| `Multiple` | 各列獨立地展開與收合 |

`AccordionSize` 設定列的尺寸。

| 值 | 含義 |
|---|---|
| `Medium` | 問題使用小號字級，列高 50 px（預設值） |
| `Large` | 頁面層級常見問題的列：問題使用大號字級，列高 76 px，答案為內文字級 |

## 無障礙

- 每一列的觸發器都是一個按鈕，遵循展開與收合模式，因此輔助技術會朗讀該列是展開還是收合，狀態變動時也會同步朗讀。
- 每個問題都是三級標題，螢幕閱讀器使用者可以依標題在各個問題之間跳轉。
- 標題為字串時，每一列的無障礙名稱取自其 `Header`。
- 每個答案都由其問題標示。收合的答案會離開無障礙樹，不參與命中測試，也不會取得鍵盤焦點。
- 焦點位於答案內部、而該列收合時，焦點會回到問題上，因此焦點永遠不會消失在隱藏的面板裡。
- 減少動態效果時，展開與收合的狀態立即呈現，朗讀的內容與可取得焦點的元素都不變。

### 鍵盤

問題是一個按鈕，因此標準按鈕按鍵同樣適用；手風琴額外提供問題之間的移動。

| 按鍵 | 操作 |
|---|---|
| `Enter` / `Space` | 展開或收合目前焦點所在的列 |
| `Down Arrow` / `Up Arrow` | 把焦點移到下一個或上一個問題；從最後一個問題循環到第一個，反之亦然 |
| `Home` / `End` | 把焦點移到第一個或最後一個問題 |
| `Tab` / `Shift+Tab` | 在問題和已展開答案中的控制項之間移動；收合的答案不接受焦點 |

方向鍵與 `Home` / `End` 只移動焦點，從不展開或收合列。停用的列會被方向鍵略過。

## 動態效果

- 列高以不過衝的平滑彈簧展開。動畫進行中再次按一下，列會從目前高度掉頭，而不是從任一端開始。
- 展開時，答案會從 2 px 的模糊中下沉 6 px 落到位。收合時，答案快速淡出，面板隨之收合。
- 列展開時，箭頭以靈敏的彈簧旋轉 180 度。
- 減少與關閉動態效果時，直接一步到達相同的最終狀態。

| 變化 | 完整 | 減少 | 關閉 |
|---|---|---|---|
| 列展開 | 高度以彈簧展開，答案以標準補間淡入 | 直接以展開狀態顯示 | 立即完成 |
| 列收合 | 面板收合的同時，答案快速淡出 | 直接收合 | 立即完成 |
| 答案就位 | 從 2 px 的模糊中下沉 6 px 落到位 | 立即完成 | 立即完成 |
| 箭頭旋轉 | 以靈敏的彈簧旋轉 180 度 | 立即完成 | 立即完成 |
| 懸停與焦點 | 問題與箭頭顏色漸變 | 立即完成 | 立即完成 |

## 相關元件

- `ExpandableCard`：用於單一展開區塊，使密集的卡片展開。
- `ExpandableText`：用於將一段長文字截斷為幾行。
- `Tabs`：用於在同一內容的不同檢視之間切換。

| 元件 | 適用情境 |
|---|---|
| `ExpandableCard` | 單張摘要卡片隱藏的詳細資訊會在原位展開，並可橫向增長 |
| `ExpandableText` | 一段文字截斷為幾行，並帶有「閱讀更多」切換 |
| `Tabs` | 使用者在同一內容的並列檢視之間切換 |
| `Stepper` | 使用者依有序步驟操作，並能看到目前位置 |
| `Card` | 內容無需展開，應始終保持可見 |
| `SortableDataTable` | 列是供人比較或排序的記錄 |
