---
title: 下拉選擇
summary: 精簡的選擇欄位，選單支援鍵盤操作。
source-hash: 1659bd3fc4e6
---

<Demo name="Field" />

## 何時使用

`Select` 是從一個簡短的固定清單中選取一個值的預設欄位：幣別、計費週期、排序方式、方案或地區。它在表單中以有標籤的欄位出現，在觸發器中保留所選的值，並從那裡展開清單，因此使用者無需離開表單就能瀏覽選項。

- **發票設定中的計費週期與幣別**：財務管理員為工作區選擇 USD 與年度週期。這份簡短清單由計費系統固定，所選的值與發票設定表單一起儲存。
- **表格或清單工具列中的排序方式**：事件清單的工具列中，在篩選控制項旁顯示「排序方式：最新優先」。欄位精簡，清單仍便於瀏覽。
- **訂閱變更表單中的方案或等級**：團隊管理員在 Free、Pro 或 Team 中選擇。方案名稱就是值，停用的選項可以顯示帳號不允許選擇的方案。
- **工作區設定中的地區或資料駐留**：加入流程的精靈詢問資料儲存的位置。欄位附有標籤與說明，因此使用者知道這個選擇為什麼重要。
- **工單編輯器中的優先順序或嚴重程度**：客服人員為案件設定 P1 到 P4。選項少且始終相同，因此這個欄位比自由文字方塊更快。

## 何時不用

- **較長的清單或需要搜尋的清單**，例如時區、客戶帳號或國家：用 `Combobox`。選單會捲動，但較長的清單透過輸入來尋找會更快。
- **同時選取多個值**：用 `MultiSelect`，它會把每個已選的值以標籤的形式保留在欄位中。
- **只有兩到四個選項且應持續可見**：用 `SegmentedControl`。所有選項同時顯示，使用者無需開啟選單即可比較。
- **每個選項需要說明或價格才能決定**：用 `RadioCards`。Select 的列只顯示標籤，因此說明必須緊挨著選項。
- **執行動作而不是選取值**，例如編輯、複製或刪除：用 `DropdownMenu`。在那裡選取一個項目會執行動作；在這裡選取選項則會儲存一個值。

## 用法

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:Select Label="地區"
                Description="工作區資料的儲存位置。"
                Width="280">
        <arc:SelectItem Content="北美" Value="na" />
        <arc:SelectItem Content="歐洲" Value="eu" />
        <arc:SelectItem Content="南美（即將推出）" Value="sa" IsEnabled="False" />
        <arc:SelectItem Content="非洲" Value="af" />
    </arc:Select>
</UserControl>
```

`Select` 衍生自 Avalonia 的 `SelectingItemsControl`，處於單選模式，因此保留了項目模型：`ItemsSource`、`Items`、`SelectedIndex`、`SelectedItem`、`SelectedValue`、`SelectionChanged` 與 `IsEnabled`。控制項已關閉 Avalonia 內建的文字搜尋，因為 `Select` 有自己的鍵入搜尋（見「鍵盤」）。Arc 層在此之上加入標籤、說明、帶值捲動動態效果的觸發器，以及帶反白與勾選標記的選單。`SelectItem` 子元素與 `SelectOption` 記錄都可以使用；沒有 `Value` 的 `SelectItem` 以其 `Content` 作為值。

### 結構

1. **標籤**（`Label`）：14 px、中等字重，位於觸發器上方。它同時是欄位的無障礙名稱，字級與 `Input` 的標籤相同。
2. **觸發器**：至少 44 px 高（`ControlSizeMD`）的方框，左右各 12 px 內距，1 px 邊框，控制項圓角（18 px）。使用表面色。懸停與展開時邊框變為強邊框色，背景變為淡化的表面色。鍵盤焦點會在觸發器外 4 px 處繪製 2 px 的焦點環。
3. **值**（`SelectedText`）：所選選項的標籤，在選擇變動時以捲動動態效果顯示。未選取任何項目時，`Placeholder` 以淡化的文字顏色顯示。
4. **箭頭**：值之後、間隔 12 px 的 16 px 下箭頭。選單開啟時旋轉 180 度。
5. **說明**（`Description`）：觸發器下方 12 px 的淡化文字。它同時是欄位的無障礙說明文字。
6. **選單**：帶 1 px 邊框、20 px 圓角與 6 px 內距的浮起表面。它在觸發器下方 4 px 處展開，寬度與觸發器完全一致。
7. **選項列**：36 px 高，圓角 14 px。標籤從左側 11 px 處開始，右側保留 34 px 放置勾選標記，勾選標記距右邊緣 10 px。反白的列使用淡化表面色，停用的列以 45% 不透明度繪製。
8. **捲動按鈕**：清單頂部與底部 28 px 的區域，當上方或下方還有列時出現。將指標停留其上即可捲動清單。
9. **選單高度**：清單高度上限為 `MaxDropDownHeight`（預設 320 px），超出的部分在該高度內捲動。

停用的欄位會淡化到 50% 不透明度，顯示「不允許」游標，並在選單開啟時將其關閉。

### 繫結選項與值

將 `ItemsSource` 設為任意物件或 `SelectOption` 記錄。`SelectedValue` 為雙向繫結，所選的值會寫入被繫結的屬性。

```xml
<arc:Select Label="排序方式"
            Width="220"
            ItemsSource="{Binding SortOrders}"
            SelectedValue="{Binding SortKey, Mode=TwoWay}" />
```

```csharp
var sort = new Select
{
    Label = "排序方式",
    Width = 220,
    ItemsSource = new[]
    {
        new SelectOption("newest", "最新優先"),
        new SelectOption("oldest", "最舊優先"),
        new SelectOption("az", "名稱，A 到 Z"),
    },
    SelectedValue = "newest"
};
sort.SelectionChanged += (_, _) => Console.WriteLine(sort.SelectedValue);
```

`SelectedValue` 是表單儲存的值。`SelectedText` 是所選選項的標籤，為唯讀，適合用作摘要列或記錄訊息。`SelectedIndex` 與 `SelectedItem` 則分別依位置或項目物件來選取。

### 繫結到檢視模型

在 MVVM 應用程式中，檢視模型擁有選擇與選項清單，欄位負責顯示它們。使用 CommunityToolkit.Mvvm 時，檢視模型以一般字串保存已儲存的值，欄位把所選的值寫入其中。

```csharp
public partial class InvoiceSettingsViewModel : ObservableObject
{
    private readonly IBillingSettings _settings;

    public InvoiceSettingsViewModel(IBillingSettings settings) => _settings = settings;

    public ObservableCollection<SelectOption> Currencies { get; } = new()
    {
        new SelectOption("usd", "美元（USD）"),
        new SelectOption("eur", "歐元（EUR）"),
        new SelectOption("gbp", "英鎊（GBP）"),
    };

    [ObservableProperty]
    private string _currency = "usd";

    [ObservableProperty]
    private string _billingCycle = "monthly";

    [RelayCommand]
    private async Task SaveAsync()
    {
        await _settings.SaveAsync(Currency, BillingCycle);
    }
}
```

```xml
<StackPanel Width="360" Spacing="16">
    <arc:Select Label="幣別"
                ItemsSource="{Binding Currencies}"
                SelectedValue="{Binding Currency, Mode=TwoWay}" />
    <arc:Select Label="計費週期"
                SelectedValue="{Binding BillingCycle, Mode=TwoWay}">
        <arc:SelectItem Content="按月" Value="monthly" />
        <arc:SelectItem Content="按年付，預先收費" Value="annual" />
    </arc:Select>
    <arc:Button Content="儲存設定" Command="{Binding SaveCommand}" />
</StackPanel>
```

- `SelectedValue` 為雙向繫結，因此當檢視模型需要接收選擇時，應明確設定 `Mode=TwoWay`。它保存的是選項的值，而不是標籤。
- `IsOpen` 為雙向繫結，可由檢視模型開啟或關閉選單。`SelectedText` 為唯讀，因此只用單向繫結來顯示。
- 只在處理副作用時使用 `SelectionChanged`，例如統計分析或預覽。檢視模型應讀取繫結的值，這樣程式碼修改與使用者操作都以相同的方式傳到它。
- 編譯繫結需要在檢視上設定 `x:DataType`（這裡的範例為求精簡而省略了它）。

### 自訂標籤與值

對於不是 `SelectOption` 的資料項目，請設定 `TextSelector` 與 `ValueSelector`。它們與 `DisplayMemberBinding` 不同，對 AOT 友善。標籤是使用者看到的內容，值是表單儲存的內容。

<Demo name="Selectors" />

```csharp
public sealed record Plan(string Code, string Name, int MonthlyPrice);

var plans = new Select
{
    Label = "方案",
    Width = 260,
    ItemsSource = new[]
    {
        new Plan("free", "免費版", 0),
        new Plan("pro", "專業版", 24),
        new Plan("team", "團隊版", 48),
    },
    TextSelector = item => item is Plan p ? $"{p.Name} (${p.MonthlyPrice}/月)" : null,
    ValueSelector = item => item is Plan p ? p.Code : null,
};
```

沒有 `ValueSelector` 時，值就是項目本身，因此 `SelectedValue` 會傳回 `Plan` 物件。只要表單儲存的是代碼或識別碼而非物件，就應設定選擇器。

### 預留位置文字與說明文字

尚未選取任何項目時會顯示 `Placeholder`，預設值為 “Select an option”。預留位置文字應告訴使用者該選什麼，例如「選擇方案」，並保持足夠簡短以放進觸發器。`Description` 是觸發器下方的淡色提示，同時也是無障礙的說明文字。

```xml
<arc:Select Label="方案" Placeholder="選擇方案" Description="之後可以隨時變更。" />
```

說明應寫選擇的後果（「從下一張發票起生效」），而不是選項清單。一旦選定值，預留位置文字就會消失，因此它不應承載欄位在那之後仍需要的資訊。

### 長清單與停用狀態

選單的高度上限為 `MaxDropDownHeight`（預設 320 px），超出的部分可以捲動。只會繪製可見的列，因此包含數千個項目的清單仍能保持流暢回應。停用的 `Select` 會忽略輸入，也不會展開。

<Demo name="LongLists" />

<Demo name="Disabled" />

```xml
<arc:Select Label="建置編號" ItemsSource="{Binding Builds}" MaxDropDownHeight="240" />
<arc:Select Label="方案" IsEnabled="False" SelectedIndex="1">
    <arc:SelectItem Content="免費版" />
    <arc:SelectItem Content="專業版" />
</arc:Select>
```

當欄位位於視窗底部或較窄的對話方塊中時，請降低 `MaxDropDownHeight`，讓選單保持在視窗之內。避免把高度設得太小，以致只能容納一列。

### 資料結構與效能

選項可以是字串、記錄、`SelectOption` 值或內嵌的 `SelectItem` 子元素。控制項會自行繪製每個可見的選項，不會為每個項目建立一個控制項，因此成本取決於畫面上的列數，而不是清單的大小。保持項目模型精簡：一個值、一個標籤，以及所需的旗標，例如停用狀態。大量或遠端資料應載入到檢視模型中，每次載入時設定一次 `ItemsSource`，而不是在每次按鍵時修改集合。

- 在建立時設定一次 `TextSelector` 與 `ValueSelector`。變更它們會重建選項清單。
- 值就是表單儲存的內容；讓它在各版本之間保持穩定，這樣即使方案標籤被重新命名，已儲存的方案代碼仍能解析。
- 用 `SelectedValue` 取得儲存的值，用 `SelectedText` 作為摘要。不要解析標籤來反推值。

### 焦點與關閉

選單開啟時焦點保留在觸發器上，反白的選項會被朗讀而不是取得焦點。Escape 關閉選單且不改變值。Tab 關閉選單，並照常移動焦點。點擊選單外部會關閉選單（輕量關閉），再次按下觸發器也會關閉選單，因此觸發器不會重新開啟剛關閉的選單。滑鼠在按下時開啟選單，因此按下、拖曳並放開即可在一次手勢中選取選項；觸控則在放開時開啟。選取選項後選單關閉，焦點回到觸發器。

欄位被停用或離開視窗時，開啟的選單會自行關閉。在程式碼中將 `IsOpen` 設為 `False` 可在儲存後關閉選單，或呼叫 `Close()`；呼叫 `Open()` 則會開啟選單並反白所選的選項。

### 範例：發票設定

SaaS 管理後台中的計費設定表單。兩個欄位都是有標籤的簡短固定清單，所選的值正是帳號實際使用的：USD 與月繳週期。幣別欄位有值，因此觸發器顯示標籤與箭頭；週期欄位顯示 Monthly，年繳選項的計費規則寫在其標籤中。輔助文字說明變更何時生效，讓使用者在儲存前就知道其後果。

![選取了幣別與計費週期、付款條款為空的發票設定表單](images/InvoiceSettingsForm.png)

```xml
<StackPanel Width="560" Spacing="20">
    <arc:Select Label="Currency"
                Description="Invoices and payouts use this currency."
                SelectedIndex="0">
        <arc:SelectItem Content="US dollar (USD)" Value="usd" />
        <arc:SelectItem Content="Euro (EUR)" Value="eur" />
        <arc:SelectItem Content="Pound sterling (GBP)" Value="gbp" />
        <arc:SelectItem Content="Japanese yen (JPY)" Value="jpy" />
    </arc:Select>
    <arc:Select Label="Billing cycle"
                Description="Changes apply from the next invoice."
                SelectedIndex="0">
        <arc:SelectItem Content="Monthly" Value="monthly" />
        <arc:SelectItem Content="Annual, billed up front" Value="annual" />
        <arc:SelectItem Content="Quarterly" Value="quarterly" IsEnabled="False" />
    </arc:Select>
    <arc:Select Label="Payment terms"
                Placeholder="Choose payment terms"
                Description="Net 30 is the default for new customers.">
        <arc:SelectItem Content="Net 15" Value="net15" />
        <arc:SelectItem Content="Net 30" Value="net30" />
        <arc:SelectItem Content="Net 60" Value="net60" />
        <arc:SelectItem Content="Net 90 (needs approval)" Value="net90" IsEnabled="False" />
    </arc:Select>
    <StackPanel Orientation="Horizontal" Spacing="8" HorizontalAlignment="Right">
        <arc:Button Content="Cancel" Variant="Secondary" />
        <arc:Button Content="Save settings" />
    </StackPanel>
</StackPanel>
```

季度週期與 90 天帳期被停用，讓使用者看到這些選項存在，並知道它們需要其他方案或審核。付款條款欄位尚無值，顯示預留位置文字，提示在儲存表單之前需要做出選擇。

### 文案規範

- **標籤**：句首大寫的簡短名詞，例如「幣別」「計費週期」「地區」。不要在標籤中重複問題（「您要使用哪種幣別？」）。
- **預留位置文字**：「選擇方案」或「請選擇一個選項」這類指示。選定第一個選項後它就會消失，因此它只需在欄位為空時讀起來正確。
- **選項標籤**：使用者在產品中已經看到的名稱，句首大寫。只有清單需要時才加入單位或代碼，例如「美元（USD）」「Net 30」。
- **說明**：一句關於選擇後果的話：「從下一張發票起生效。」
- **停用的選項**：在標籤或說明中寫明原因，例如「Net 90（需要審核）」。

| 不要寫 | 改為 |
|---|---|
| 選取 | 選擇方案 |
| 您要使用哪種幣別？ | 幣別 |
| 選取一個值 | 請選擇一個選項 |
| usd - 美元 - 預設 | 美元（USD） |
| 選項 3 | 按季度 |

### 最佳做法

**建議**

- 清單應簡短到便於瀏覽，約十幾個選項。較長的清單改用 `Combobox`，讓使用者透過輸入來尋找值。
- 為欄位提供標籤；當選擇會產生後果時，寫出該後果的說明。
- 儲存值而不是標籤，這樣重新命名不會改變已儲存的資料。
- 存在最常見的選項時預先選取它，使常見情況下無需選擇即可完成表單。

**避免**

- 用 `Select` 選取多個值；欄位只保存一個值，每次選取後選單都會關閉。
- 把選單選項藏在「選取」這類對選擇毫無說明的預留位置文字之後。
- 透過停用選項來隱藏它們。停用的選項依然顯示，使用者因此知道它存在以及為何被鎖定。
- 在選單開啟時更改清單。選單會立即重新填入，使用者正在閱讀的列會在眼前變動。
- 把 `Select` 當作唯讀顯示。停用的欄位會變暗；當值不應看起來可以互動時，改用純文字。

### 常見問題

**設定 `SelectedValue` 後欄位顯示預留位置文字，為什麼？** 沒有選項具有該值，因此未選取任何項目，於是顯示預留位置文字。請檢查該值是否與選項的 `Value` 相符，或與 `ValueSelector` 傳回的值相符。

**為什麼反白停在最後一個選項，而不會循環？** `Select` 在第一個和最後一個可用選項處停止，讓使用者看到清單在哪裡結束。`MultiSelect` 則會循環。

**如何在程式碼中開啟或關閉選單？** 呼叫 `Open()` 或 `Close()`，或設定 `IsOpen`。`Open()` 會反白所選的選項；未選取任何項目時，則反白第一個可用的選項。

**能否顯示一個無法變更的值，又不讓它看起來像欄位？** `Select` 沒有唯讀狀態。當選擇被鎖定時，停用它並說明原因；當表單不應暗示該值可以變更時，則在標籤中以純文字顯示該值。

**鍵入會改變值嗎？** 會。選單關閉時，鍵入一個字母會選取下一個文字以該字母開頭的選項，而無需開啟選單。選單開啟時，鍵入會反白符合的項目，按 Enter 即可選取它。

## API 參考

<!-- api: Select -->

<!-- api: SelectItem -->

## 無障礙

- `Label` 為欄位命名，`Description` 是其說明文字。
- 所選的值以純文字形式提供給輔助技術，帶動畫的副本會被隱藏。
- 欄位會公開其選單是否展開。
- 反白移動時，被反白的選項會在禮貌即時區域中播報，並附上它在清單中的位置與清單長度。
- 選單開啟時焦點框仍保留在觸發器上。懸停與開啟只改變邊框和背景。

### 鍵盤

以下按鍵在觸發器取得焦點時生效。

| 按鍵 | 操作 |
|---|---|
| `Enter`、`Space`、`Down`、`Up`（選單關閉時） | 開啟選單並反白所選的選項；未選取任何項目時反白第一個可用的選項 |
| `Down` / `Up`（選單開啟時） | 將反白移到下一個或上一個可用的選項；在第一個和最後一個選項處停止 |
| `Home` / `End`（選單開啟時） | 移到第一個或最後一個可用的選項 |
| `PageDown` / `PageUp`（選單開啟時） | 每次將反白移動八個選項 |
| `Enter` 或 `Space`（選單開啟時） | 選取反白的選項，關閉選單並將焦點返回觸發器 |
| `Escape`（選單開啟時） | 關閉選單，不做任何變更 |
| `Tab`（選單開啟時） | 關閉選單並移動焦點 |
| 字母鍵（選單關閉時） | 選取下一個文字以所鍵入字母開頭的選項 |
| 字母鍵（選單開啟時） | 反白下一個與所鍵入字母相符的選項 |

鍵入停頓約一秒後，會開始新的鍵入搜尋。方向鍵、Home、End、翻頁鍵以及鍵入搜尋都會略過停用的選項。

## 動態效果

- 顯示的值沿清單方向捲動：後面的選項從下方升起，前面的選項從上方落下。
- 選單以短暫的縮放與淡入淡出從觸發器展開。關閉時立即完成。
- 反白會從一個選項平滑滑到下一個。
- 減少動態效果時，值以瞬間交叉淡入淡出的方式切換，選單則淡入淡出。關閉動態效果時，值與選單直接切換，不含任何過場。

| 變化 | 完整 | 減少 | 關閉 |
|---|---|---|---|
| 所選的值變化 | 沿清單方向滾入 | 瞬間交叉淡入淡出 | 立即完成 |
| 選單開啟 | 從觸發器展開，帶輕微縮放與 6 px 位移 | 淡入 | 立即完成 |
| 選單關閉 | 立即完成 | 淡出 | 立即完成 |
| 箭頭轉動 | 以彈簧旋轉 | 立即完成 | 立即完成 |
| 懸停、開啟、焦點 | 邊框與背景顏色漸變 | 立即完成 | 立即完成 |

## 相關元件

- 清單較長或需要使用者搜尋時用 `Combobox`。
- 需要同時選取多個值時用 `MultiSelect`。
- 觸發器需要展開為清單，且選項需要圖示或分組時用 `MorphSelect`。
- 兩到四個選項需要持續可見時用 `SegmentedControl`。

| 元件 | 適用情境 |
|---|---|
| `Combobox` | 清單較長，或使用者需要輸入來尋找值 |
| `MultiSelect` | 選取多個值，並以標籤形式顯示 |
| `MorphSelect` | 觸發器需要展開為清單，或選項需要圖示、輔助文字或分組 |
| `SegmentedControl` | 兩到四個選項需要持續可見 |
| `RadioCards` | 每個選項在使用者選擇前需要各自的說明 |
| `DropdownMenu` | 選單執行動作，而不是儲存值 |
