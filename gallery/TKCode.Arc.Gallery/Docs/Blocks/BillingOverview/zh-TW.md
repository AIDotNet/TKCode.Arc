---
title: 帳單概覽
summary: 在同一頁呈現目前方案、下次扣款、付款方式與發票紀錄，每一項旁邊都有對應的操作。
source-hash: f2c9dc50b8bb
---

<Demo name="Overview" />

## 何時使用

`BillingOverview` 是帳戶或工作區設定中的帳單頁面。它回答使用者查看帳單時最關心的三個問題：要付多少錢、下次何時扣款、之前付過什麼；同時把會改變這些答案的操作，放在對應事實的旁邊：變更方案、更新信用卡、編輯計費電子郵件和下載發票。

- **SaaS 管理後台中的工作區帳單**：擁有者開啟設定、進入帳單，就能看到團隊方案在 11 月 1 日按 12 個席位續訂、將由哪張信用卡扣款，以及下一張發票何時到期。此區塊無需另外的報表即可給出這些答案。
- **把發票交給財務**：財務負責人需要上一季的發票用於報帳。發票表格依由新到舊排列，每列都有下載按鈕，無需再向廠商索取 PDF。
- **價格固定的按席位方案**：按月或按年計費的團隊方案，下次扣款金額就是目前席位數對應的方案價格。方案卡片顯示每個週期的價格、席位數以及下次扣款金額。
- **試用與付款失敗**：試用會顯示結束時間與首次扣款金額；最近一次扣款失敗的方案會顯示危險色徽章，讓擁有者在權限受影響之前修正信用卡。
- **會開啟你自己流程的帳單操作**：變更方案、更新信用卡與取消通常需要確認、按比例計費或付款服務的表單。此區塊只引發事件，每個流程仍留在你的程式碼中。

## 何時不用

- **本期總額取決於計量用量的按量計費**：使用 `UsageBilling`，它顯示目前已產生的費用、預估總額、每個計量項的用量以及消費上限。
- **在定價頁選擇或比較方案**：使用 `BillingToggle` 與 `BillingPrice`。`BillingOverview` 顯示客戶已有的方案，而不是可供選擇的選項。
- **收集信用卡資料**：此區塊只顯示已存檔的信用卡，從不接收卡號。請在 `UpdatePaymentMethodRequested` 中開啟 `Dialog`，再放入付款服務商的表單。
- **篇幅很長且可搜尋的付款紀錄**：使用 `Table`，它可以排序、篩選與分頁。此處的發票表格只顯示簡短、由新到舊的清單，其餘發票透過「顯示全部發票」查看。
- **需要詢問原因或提供挽留優惠的取消**：使用 `CancelFlow`，它是可以從 `CancelPlanRequested` 啟動的多步驟流程。`BillingOverview` 只發出請求。

## 用法

```xml
<arc:BillingOverview Plan="{Binding Plan}"
                     PaymentMethod="{Binding Card}"
                     BillingEmail="{Binding BillingEmail}"
                     Invoices="{Binding Invoices}"
                     VisibleInvoiceCount="6" />
```

`BillingOverview` 衍生自 Avalonia 的 `TemplatedControl`，由 `SettingsGroup`、`SettingsRow`、`Badge` 與 `Button` 組成。它繼承了這些控制項的間距、焦點環、懸停狀態與動態效果，因此由它建構的帳單頁面無需額外樣式，就能與其他設定區塊保持一致。需要設定四個資料屬性：`Plan`、`PaymentMethod`、`BillingEmail` 與 `Invoices`。三個模型類別（`BillingPlan`、`BillingPaymentMethod` 與 `BillingInvoice`）都是一般物件，既可以像預覽中那樣以 XAML 撰寫，也可以在程式碼中建立。它們會引發 `PropertyChanged`，因此顯示之後對方案、信用卡或發票的變更會直接更新檢視，無需取代物件。

`Invoices` 起初為一個可以加入項目的空清單。你可以取代它，也可以繫結到自己的集合；可觀察集合的變更會更新表格。發票始終依 `Date` 由新到舊顯示。

### 結構

頁面由三張卡片組成，之間以 16 px 的 `GroupGap` 分隔。由上到下依序為：

1. **方案卡片**（`Plan`）：眉題「目前方案」、方案名稱（`PlanNamePart`，`TypeXL`）與狀態徽章（`PlanStatusPart`）。名稱下方是詳情列，顯示席位數與續訂日期。右側是 `TypeXL` 字級的價格（`PricePart`），後面以次要文字顏色顯示「每月」或「每年」。頁尾（`NextInvoicePart`）顯示下次扣款，金額以強調方式呈現，之後是「取消方案」與「變更方案」按鈕。
2. **付款卡片**（`PaymentMethod`、`BillingEmail`）：品牌標記（`CardBrandPart`）是一個 36 × 24 px 的圓角方框，內含 `TypeXS` 字級的品牌文字，之後是「{Brand} 末四碼 {Last4}」（`CardTitlePart`）與到期列（`CardExpiryPart`）。「更新」按鈕（`UpdateCardPart`）位於右側。分隔線下方是「計費電子郵件」列（`EmailRowPart`），顯示電子郵件地址與「編輯」按鈕（`EditEmailPart`）。
3. **發票卡片**（`Invoices`）：標題「發票」，以及一列 32 px 高的欄標題，包含日期、發票、金額與狀態（`InvoiceHeaderPart`）。每一列（`InvoiceRowsPart`）高 48 px，上方有一條細線，依序為日期、發票編號、以等寬數字靠右對齊的金額、狀態徽章，以及 36 px 的下載圖示按鈕。
4. **收合的列與「顯示全部發票」**（`MoreInvoicesPart`、`ShowAllPart`）：超過 `VisibleInvoiceCount` 的發票放在可摺疊的部分中，頁尾按鈕負責切換它。
5. **空狀態**（`InvoicesEmptyPart`）：沒有發票時，一個 40 px 的圖塊、「尚無發票」與一行提示文字取代表格。

寬度小於 560 px（`NarrowWidth`）時，價格移到方案名稱下方，頁尾上下堆疊，發票欄隱藏。卡片中的列寬度小於 440 px 時，按鈕會移到文字下方。狀態欄與前一欄之間保持 14 px 間距，而不是 28 px。

### 方案狀態

方案的 `Status` 決定徽章、詳情列與頁尾，因此每種狀態的呈現方式都一致。

| `Status` | 徽章 | 詳情列 | 頁尾 |
|---|---|---|---|
| `Active` | 成功色 | 「6 個席位 · 2026 年 11 月 1 日續訂」 | 下次扣款、「取消方案」、「變更方案」 |
| `Trialing` | 強調色 | 「試用結束於 2026 年 11 月 1 日」 | 下次扣款、「取消方案」、「變更方案」 |
| `PastDue` | 危險色 | 「付款自 2026 年 10 月 1 日起逾期」 | 下次扣款、「取消方案」、「變更方案」 |
| `Canceled` | 中性色 | 「結束於 2026 年 11 月 1 日」 | 只有「變更方案」，沒有「取消方案」 |

沒有方案（`Plan` 為 null）時，卡片顯示「尚無方案」、「選擇一個方案開始使用。」以及「選擇方案」按鈕，該按鈕會引發 `ChangePlanRequested`。已取消的方案會保留其物件，因此擁有者仍能看到服務何時結束。付款卡片遵循同樣的規則：沒有 `PaymentMethod` 時，「新增信用卡」會引發 `UpdatePaymentMethodRequested`。

### 發票狀態

| `Status` | 徽章 | 列的處理方式 |
|---|---|---|
| `Paid` | 成功色 | 正常 |
| `Open` | 中性色 | 正常；已開立但尚未付款 |
| `PastDue` | 危險色 | 正常；已到期且未付款 |
| `Refunded` | 中性色 | 淡化：徽章與金額的不透明度為 70% |
| `Void` | 中性色 | 淡化：徽章與金額的不透明度為 70% |

淡化的列仍保留在表格中，下載按鈕也保留，因為客戶仍然需要這份文件。不要為了隱藏已退款的發票而把它從 `Invoices` 中移除，否則歷史紀錄會與銀行對帳單不一致。

### 試用與即將到期的信用卡

試用中的方案會顯示試用結束的時間以及首次扣款的金額：將 `Status` 設為 `Trialing`，並把 `RenewsOn` 設為試用結束日期。

信用卡的到期情況會依據 `Clock` 檢查。到期月份在 60 天內開始時，到期列變為警告色，並顯示「即將到期」與日期；已超過到期月份的信用卡則以危險色顯示「已過期」。`IsPaymentMethodExpiringSoon` 與 `IsPaymentMethodExpired` 會回報相同的兩種狀態，因此你可以在自己的頁面中顯示橫幅，或寄送提醒郵件。品牌以簡短文字（VISA、MC、AMEX）顯示在小框線中，從不使用標誌。沒有發票時，表格會被「尚無發票」取代。

<Demo name="Trial" />

「今天」的日期來自 CLR 屬性 `Clock`，它是一個 `TimeProvider`（預設為系統時鐘）。在測試中可以將它設為固定時鐘：

```csharp
billing.Clock = TimeProvider.System; // 也可以是測試提供的任何 TimeProvider
```

### 處理操作

每個按鈕都會引發路由事件，但本身不做任何變更。這些事件包括 `ChangePlanRequested`、`CancelPlanRequested`、`UpdatePaymentMethodRequested` 與 `EditBillingEmailRequested`，以及 `InvoiceDownloadRequested`，其 `BillingInvoiceEventArgs.Invoice` 是該列對應的發票。在事件中開啟你自己的對話方塊或流程，呼叫你的 API，然後更新 `Plan`、`PaymentMethod`、`BillingEmail` 或 `Invoices`，區塊就會顯示結果。此區塊不做確認、不做按比例計算，也不呼叫付款服務。

<Demo name="Events" />

Events 範例會把每個事件寫入記錄。下載處理常式是非同步的，因此檢視模型可以在背景儲存檔案：

```csharp
billing.InvoiceDownloadRequested += async (_, e) =>
{
    var pdf = await invoices.DownloadAsync(e.Invoice.Number);
    await SaveAsync(pdf, $"{e.Invoice.Number}.pdf");
};

billing.CancelPlanRequested += (_, _) => cancelDialog.Open(); // 例如在 Dialog 中使用 CancelFlow
```

`VisibleInvoiceCount`（預設為 4）設定在「顯示全部發票（n）」之前顯示的發票數量。按鈕會展開其餘發票，並變為「收合」。`IsInvoiceListExpanded` 表示同一狀態；將它雙向繫結，即可在檢視模型中保持該狀態。

### 資料結構與效能

此區塊不做分頁、排序或搜尋。請只傳入頁面應顯示的發票：概覽中顯示最新的幾張，並連結到完整歷史。集合變更時，表格會重用既有的列，因此新增一張發票不會重建整個表格。為每張發票設定唯一的 `Number`，因為它用來命名下載按鈕。

金額與日期使用 `Labels.Culture`（預設為 en-US）。每個金額使用其發票的 `Currency`，發票未設定時則使用方案的幣別。金額只在此區塊中格式化一次。

### 繫結到檢視模型

在 MVVM 應用程式中，檢視模型擁有帳單資料與流程，區塊負責顯示。下面的範例使用 CommunityToolkit.Mvvm。應用程式自己的服務（`IBillingService`）及其方法是你 API 的佔位符。

```csharp
public partial class BillingViewModel : ObservableObject
{
    private readonly IBillingService _billing;

    public BillingViewModel(IBillingService billing)
    {
        _billing = billing;
    }

    [ObservableProperty]
    private BillingPlan? _plan;

    [ObservableProperty]
    private BillingPaymentMethod? _paymentMethod;

    [ObservableProperty]
    private string? _billingEmail;

    [ObservableProperty]
    private decimal? _nextInvoiceAmount;

    [ObservableProperty]
    private bool _isInvoiceListExpanded;

    public ObservableCollection<BillingInvoice> Invoices { get; } = new();

    [RelayCommand]
    private async Task CancelPlanAsync()
    {
        await _billing.CancelAtPeriodEndAsync();
        Plan!.Status = BillingPlanStatus.Canceled;
    }

    [RelayCommand]
    private async Task DownloadInvoiceAsync(BillingInvoice invoice)
    {
        var pdf = await _billing.DownloadAsync(invoice.Number);
        await _files.SaveAsync(pdf, $"{invoice.Number}.pdf");
    }
}
```

```xml
<arc:BillingOverview Plan="{Binding Plan}"
                     PaymentMethod="{Binding PaymentMethod}"
                     BillingEmail="{Binding BillingEmail}"
                     NextInvoiceAmount="{Binding NextInvoiceAmount}"
                     Invoices="{Binding Invoices}"
                     IsInvoiceListExpanded="{Binding IsInvoiceListExpanded, Mode=TwoWay}"
                     VisibleInvoiceCount="6" />
```

此區塊不公開命令，因此需要在程式碼後置中把它的事件連接到檢視模型，呼叫工具組自動產生的 `CancelPlanCommand` 與 `DownloadInvoiceCommand`。

雙向繫結：`IsInvoiceListExpanded` 是唯一需要設定 `Mode=TwoWay` 的屬性，因為「顯示全部發票」會寫入它。其他輸入都是單向的：變更模型物件（例如 `Plan.Status`）或取代屬性，檢視就會隨之更新。

### 文字與語言

所有內建文字都位於 `BillingOverviewLabels` 中。取代整個物件即可改寫文字或在地化此區塊；包含數值的文字是函式，接收的數值已依 `Culture` 格式化。預設文字為英文，例如 “Renews on Nov 1, 2026”、“Show all invoices (6)” 與 “Download LS-2026-010”。

```csharp
billing.Labels = new BillingOverviewLabels
{
    Culture = CultureInfo.GetCultureInfo("nl-NL"),
    CurrentPlan = "Huidig abonnement",
    ChangePlan = "Abonnement wijzigen",
    RenewsOn = date => $"Wordt verlengd op {date}",
    ShowAll = count => $"Alle facturen tonen ({count})",
};
```

請整體取代該物件，而不是只取代其中一個欄位，因為它的屬性只能在初始化時設定。保留接收數值的函式，因為它們決定了詞序。一個物件即可涵蓋整個區塊，從方案卡片到空狀態。

### 範例：團隊方案帳單頁面

工作區擁有者開啟團隊方案的帳單。十月新增了兩個席位，因此方案目前每月 $288、共 14 個席位，頁尾顯示 11 月 1 日的下一張發票，金額相同。該卡片是一張有效期至 2029 年的萬事達卡，因此不顯示警告。發票表格顯示三列，以及一個「顯示全部發票（4）」按鈕；其中一張較早的發票已退款，並以淡化樣式顯示。擁有者可以在此下載任何發票、變更方案或更新信用卡。

頁尾的下次扣款金額預設跟隨 `Price`，除非設定了 `NextInvoiceAmount`，因此本例將其保持為 null。只有當下次扣款與方案價格不同時才需要設定它。發票描述不可見，它們會成為每個下載按鈕無障礙名稱的一部分。

![團隊方案帳單頁面，包含方案卡片、已存檔的信用卡、計費電子郵件與三張近期發票](images/BillingOverview.png)

```xml
<StackPanel Width="680" Spacing="16">
    <arc:BillingOverview BillingEmail="accounts@northwind.dev"
                         VisibleInvoiceCount="3">
        <arc:BillingOverview.Plan>
            <arc:BillingPlan Name="Team" Price="288" Currency="USD" Seats="14"
                             Interval="Month" Status="Active" RenewsOn="2026-11-01" />
        </arc:BillingOverview.Plan>
        <arc:BillingOverview.PaymentMethod>
            <arc:BillingPaymentMethod Brand="Mastercard" Last4="5108"
                                      ExpiryMonth="4" ExpiryYear="2029" />
        </arc:BillingOverview.PaymentMethod>
        <arc:BillingOverview.Invoices>
            <arc:BillingInvoice Number="LS-2026-010" Date="2026-10-01" Amount="288"
                                Status="Paid" Description="Team plan, 14 seats" />
            <arc:BillingInvoice Number="LS-2026-009" Date="2026-09-01" Amount="240"
                                Status="Paid" Description="Team plan, 12 seats" />
            <arc:BillingInvoice Number="LS-2026-008" Date="2026-08-01" Amount="200"
                                Status="Paid" Description="Team plan, 10 seats" />
            <arc:BillingInvoice Number="LS-2026-007" Date="2026-07-01" Amount="200"
                                Status="Refunded" Description="Team plan, 10 seats" />
        </arc:BillingOverview.Invoices>
    </arc:BillingOverview>
</StackPanel>
```

### 文案規範

金額要附上幣別，日期要寫出月份名稱，如 “$240.00” 與 “Nov 1, 2026”。方案名稱應使用客戶購買時的叫法，並與產品其餘部分的用詞一致：如果應用程式中統一稱為「方案」，就寫「方案」，而不要寫「訂閱」。

- 按鈕文字是句首大寫的動詞加受詞：「變更方案」「取消方案」「更新付款方式」。
- 狀態詞要簡短。一到兩個字即可放進徽章：「生效中」「試用中」「逾期」「已取消」。
- 說明存取權限會如何變化，而不只是方案本身：「結束於 2026 年 11 月 1 日」讓擁有者知道服務何時停止。

| 不要寫 | 改為 |
|---|---|
| 訂閱等級 | 方案 |
| 取消訂閱 | 取消方案 |
| 付款失敗 | 逾期 |
| 12 Seats | 12 seats |
| 按這裡 | 下載 LS-2026-010（該列的下載按鈕） |
| 信用卡即將過期，請更新 | 即將到期 · 2029 年 4 月（預設文案） |

### 最佳做法

**建議**

- 每次變更時都從帳單服務同步 `Status` 與 `RenewsOn`，因為頁尾、徽章與詳情列都會讀取它們。過時的狀態會讓擁有者對自己的存取權限產生錯誤判斷。
- 當下次扣款與方案價格不同時（例如新增席位或適用折扣後），設定 `NextInvoiceAmount`，避免頁尾顯示擁有者實際不會支付的價格。
- 在 `CancelPlanRequested` 中開啟確認對話方塊或 `CancelFlow`，並且只有在伺服器確認變更後才更新 `Plan`。此區塊無法撤銷伺服器拒絕的變更。
- 在 `Invoices` 中只保留最新的發票，並連結到完整歷史。概覽中的長清單會讓最近的發票更難找到。

**避免**

- 隱藏已退款或已作廢的發票。擁有者與會計團隊會將此清單與對帳單比對，缺少的列看起來就像缺少的付款。
- 在檢視模型中先格式化金額再傳入文字。此區塊使用 `Labels.Culture` 與發票幣別來格式化金額；二次格式化會導致同一頁面出現兩種不同的數值。
- 為已取消的訂閱把 `Plan` 設為 null。擁有者會看到「選擇方案」，並失去服務結束日期。
- 在已存檔信用卡旁邊的欄位中收集卡號。卡片資料應放在付款服務商託管的表單中，這樣它才不會進入你的應用程式。

### 常見問題

**為什麼卡片顯示「選擇方案」而不是「取消方案」？** 當 `Plan` 為 null 時，此區塊顯示「尚無方案」，此時唯一的操作是「選擇方案」，它會引發 `ChangePlanRequested`。已取消的方案會保留其物件並設定 `Status="Canceled"`，因此對它隱藏「取消方案」，並顯示其結束日期。

**為什麼沒有出現「顯示全部發票」？** 只有當 `Invoices` 的數量超過 `VisibleInvoiceCount` 時才會出現該按鈕。將 `VisibleInvoiceCount` 設得更小，或增加發票數量，即可看到它。

**區塊會取消訂閱或下載檔案嗎？** 不會。它會引發 `CancelPlanRequested` 與 `InvoiceDownloadRequested`，由你的程式碼呼叫帳單 API、顯示確認並儲存檔案。

**為什麼同一個頁面在一個環境中顯示「即將到期」，在另一個環境中卻不顯示？** 此檢查使用 `Clock`，預設為系統時鐘。在測試中將 `Clock` 設為固定的 `TimeProvider`，並確保每個環境中信用卡的到期月份設定方式一致。

**如何為某一張發票顯示不同的幣別？** 在該發票上設定 `BillingInvoice.Currency`；其他發票仍使用方案的 `Currency`。

## API 參考

<!-- api: BillingOverview -->

`Clock`（`TimeProvider`，預設為 `TimeProvider.System`）是一個 CLR 屬性，作為到期警告中「今天」的來源。

### BillingPlan

| 屬性 | 類型 | 說明 |
|---|---|---|
| `Name` | `string` | 方案名稱，例如 “Studio”。 |
| `Price` | `decimal` | 每個週期收取的總額，包含所有席位。 |
| `Currency` | `string` | 價格的 ISO 4217 代碼（預設為 "USD"）。 |
| `Interval` | `BillingInterval` | `Month` 或 `Year`：價格收取的頻率。 |
| `Seats` | `int?` | 付費席位數；不按席位計價的方案為 null。 |
| `Status` | `BillingPlanStatus` | `Active`、`Trialing`、`PastDue` 或 `Canceled`。 |
| `RenewsOn` | `DateTime?` | 下次續訂日期；試用期間為試用結束日期，取消後為存取權限的結束日期。 |

### BillingPaymentMethod

| 屬性 | 類型 | 說明 |
|---|---|---|
| `Brand` | `string` | 卡組織的文字名稱，例如 "Visa" 或 "Mastercard"。 |
| `Last4` | `string` | 卡號的末四碼。 |
| `ExpiryMonth` | `int` | 到期月份，範圍為 1 至 12。 |
| `ExpiryYear` | `int` | 四位數的到期年份。 |

### BillingInvoice

| 屬性 | 類型 | 說明 |
|---|---|---|
| `Number` | `string` | 發票編號，例如 "LS-2026-010"。 |
| `Date` | `DateTime` | 開立發票的日期；表格依此排序，最新的在前。 |
| `Amount` | `decimal` | 發票總額。 |
| `Currency` | `string?` | 金額的 ISO 4217 代碼；為 null 時使用方案的幣別。 |
| `Status` | `BillingInvoiceStatus` | `Paid`、`Open`、`PastDue`、`Refunded` 或 `Void`。 |
| `Description` | `string?` | 發票的用途說明；會加入該列的無障礙名稱中。 |

### BillingInvoiceEventArgs

| 屬性 | 類型 | 說明 |
|---|---|---|
| `Invoice` | `BillingInvoice` | 下載按鈕被按下的發票。 |

### 列舉

| 類型 | 成員 |
|---|---|
| `BillingInterval` | `Month`、`Year` |
| `BillingPlanStatus` | `Active`（成功色徽章）、`Trialing`（強調色徽章）、`PastDue`（危險色徽章）、`Canceled`（中性色徽章） |
| `BillingInvoiceStatus` | `Paid`（成功色徽章）、`Open`、`PastDue`（危險色徽章）、`Refunded` 與 `Void`（淡化的中性色徽章） |

## 無障礙

- 每個按鈕都有名稱。下載按鈕只有圖示，名稱為「下載 LS-2026-010」，同時也是其工具提示；「更新」按鈕的名稱為「更新付款方式」，「編輯」按鈕的名稱為「編輯計費電子郵件」。
- 每一筆發票列會被朗讀為一行，例如「2026 年 10 月 1 日，LS-2026-010，$90.00，已付款」。欄標題對輔助技術隱藏。
- 狀態徽章會朗讀其文字。到期警告同時以文字與圖示呈現，而不只依賴顏色。
- 「顯示全部發票」按鈕的名稱跟隨其文字。收合的發票會移出 Tab 停駐點，因此 Tab 不會停在隱藏的下載按鈕上。
- 此區塊自身不接收焦點；Tab 依閱讀順序依序經過各按鈕。

### 鍵盤

| 按鍵 | 操作 |
|---|---|
| `Tab` / `Shift+Tab` | 依閱讀順序移到下一個或上一個按鈕 |
| `Enter` / `Space` | 按下目前聚焦的按鈕，例如「變更方案」或某個下載按鈕 |

## 動態效果

- 較早的發票以無回彈的高度彈簧展開與收合；「顯示全部發票」的箭頭以俐落的彈簧轉動，同時其文字以短暫的模糊切換。
- 原地變更的徽章與按鈕文字會以模糊切換，與 `Badge` 和 `Button` 的做法相同。到期資訊列會平滑過渡到警告色。
- 減少動態效果時保留淡入淡出，但高度與箭頭改為直接跳變。關閉動態效果時，所有內容立即就位。

| 變化 | 完整 | 減少 | 關閉 |
|---|---|---|---|
| 較早的發票收合或展開 | 高度以無回彈的平滑彈簧展開 | 高度直接跳到終態 | 立即完成 |
| 「顯示全部發票」切換 | 箭頭以俐落的彈簧轉動；文字以模糊切換 | 箭頭直接跳變；文字淡入淡出 | 立即完成 |
| 徽章或按鈕文字變化 | 文字以短暫模糊切換 | 淡入淡出 | 立即完成 |
| 到期資訊列變為警告 | 文字平滑過渡到警告色 | 顏色淡入淡出 | 立即完成 |

## 相關元件

- 其他設定區塊：`SettingsPage`、`SettingsCommand`、`SecuritySettings`、`UsageBilling`，以及 `CancelFlow`（處理 `CancelPlanRequested` 的自然選擇）。
- `SettingsGroup` 與 `SettingsRow`，即組成此區塊的卡片與列。
- `Badge` 用於方案與發票狀態，`Button` 用於操作。
- `BillingToggle` 與 `BillingPrice` 用於選擇方案，`Dialog` 用於確認變更。

| 元件 | 適用情境 |
|---|---|
| `UsageBilling` | 本期費用取決於計量用量，並設有消費上限 |
| `CancelFlow` | 取消需要多個步驟、原因選擇或挽留優惠 |
| `Table` | 付款紀錄較長，必須排序、篩選或分頁 |
| `SettingsGroup` / `SettingsRow` | 你要建構的設定卡片不是帳單摘要 |
| `BillingToggle` / `BillingPrice` | 客戶要在多個方案或計費週期之間選擇 |
| `Dialog` | 變更方案、更新信用卡或下載需要確認或表單 |
