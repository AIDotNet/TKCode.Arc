---
title: 帳單概覽
summary: 在同一頁呈現目前方案、下次扣款、付款方式與發票紀錄，每一項旁邊都有對應的操作。
source-hash: 239be44eea73
---

<Demo name="Overview" />

## 何時使用

- 帳戶設定中的帳單區段，使用者可在此查看應付金額、下次扣款時間，以及先前的付款紀錄。
- 通往應用程式既有帳單操作的頁面：更換或取消方案、更新信用卡、變更計費電子郵件，以及下載發票。
- 每個計費週期價格固定的訂閱，無論是否按席位計費。

## 何時不用

- 總額取決於本期用量的按量計費方案：使用 `UsageBilling`。
- 選擇或比較方案：使用搭配 `BillingToggle` 與 `BillingPrice` 的定價版面。
- 收集信用卡資料：此區塊只顯示已存檔的卡片，並請宿主應用程式更新卡片。
- 篇幅很長且可搜尋的付款紀錄：使用 `SortableDataTable`。

## 用法

設定 `Plan`、`PaymentMethod`、`BillingEmail` 與 `Invoices`。這些模型都是一般類別，因此可以像預覽中那樣以 XAML 撰寫，也可以在程式碼中建立。它們會引發 `PropertyChanged`，所以在顯示後變更方案、卡片或發票，檢視都會隨之更新。`Invoices` 起初為一個空清單，可以向其中加入項目；也可以取代它，或繫結到其他集合來顯示該集合，可觀察集合的變更會更新表格。發票依由新到舊的順序顯示。

```xml
<arc:BillingOverview Plan="{Binding Plan}"
                     PaymentMethod="{Binding Card}"
                     BillingEmail="{Binding BillingEmail}"
                     Invoices="{Binding Invoices}"
                     VisibleInvoiceCount="6" />
```

方案卡片會顯示方案名稱與狀態徽章（生效中為成功色、試用中為強調色、逾期為危險色、已取消為中性色）、席位數與續訂日期，以及按月或按年計算的價格。其頁尾顯示下次扣款金額，取自 `NextInvoiceAmount`；若該值為 null，則顯示方案價格。已取消的方案沒有「取消方案」按鈕；沒有方案時，卡片會提供「選擇方案」。金額使用方案的 `Currency`，發票可以設定自己的幣別。

寬度小於 560 px（`NarrowWidth`）時，價格會移到方案名稱下方，頁尾改為上下堆疊，發票欄隱藏。

### 試用與即將到期的卡片

試用中的方案會顯示試用結束的時間，以及首次扣款的金額。卡片的到期月份在 60 天內開始時，到期資訊列會變為警告色並顯示「即將到期」；已超過到期月份的卡片則以危險色顯示「已過期」。`IsPaymentMethodExpiringSoon` 與 `IsPaymentMethodExpired` 也會給出相同的判斷。卡片品牌會以簡短文字顯示在小框線中（VISA、MC、AMEX），而不是標誌。沒有發票時，表格會改為顯示「尚無發票」。

<Demo name="Trial" />

「今天」的日期來自 CLR 屬性 `Clock`，它是一個 `TimeProvider`（預設為系統時鐘）。在測試或預覽中，可以將它設為固定時鐘：

```csharp
billing.Clock = TimeProvider.System; // 也可以是任何 TimeProvider，例如測試中的 FakeTimeProvider
```

### 處理操作

每個按鈕都會引發路由事件，本身不會做任何變更：`ChangePlanRequested`、`CancelPlanRequested`、`UpdatePaymentMethodRequested`、`EditBillingEmailRequested` 與 `InvoiceDownloadRequested`，其中 `InvoiceDownloadRequested` 的參數會攜帶 `Invoice`。請在這些事件中開啟你自己的對話方塊或流程，並於之後更新資料模型。此處會顯示前三張發票，其餘的則收合在「顯示全部發票」之後。

<Demo name="Events" />

```csharp
billing.InvoiceDownloadRequested += async (_, e) =>
{
    var pdf = await invoices.DownloadAsync(e.Invoice.Number);
    await SaveAsync(pdf, $"{e.Invoice.Number}.pdf");
};

billing.CancelPlanRequested += (_, _) => cancelDialog.Open(); // 例如在 Dialog 中使用 CancelFlow
```

`VisibleInvoiceCount`（預設為 4）設定在「顯示全部發票（n）」之前顯示的發票數量。按鈕會展開其餘發票，並變為「收合」；`IsInvoiceListExpanded` 表示相同的狀態，且可雙向繫結。

### 文字與語言

所有內建文字都位於 `BillingOverviewLabels` 中。取代整個物件即可改寫文字或將區塊在地化；帶有數值的文字是函式，接收的數值已依 `Culture` 格式化。

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

## API 參考

<!-- api: BillingOverview -->

`Clock`（`TimeProvider`，預設為 `TimeProvider.System`）是一個 CLR 屬性，作為到期警告中「今天」的來源。

### BillingPlan

| 屬性 | 類型 | 說明 |
|---|---|---|
| `Name` | `string` | 方案名稱，例如「Studio」。 |
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

## 無障礙

- 每個按鈕都有名稱。下載按鈕只有圖示，名稱為「下載 LS-2026-010」，同時也是其工具提示；「更新」按鈕的名稱為「更新付款方式」，「編輯」按鈕的名稱為「編輯計費電子郵件」。
- 每一筆發票列會朗讀為一行，例如「2026 年 10 月 1 日，LS-2026-010，$90.00，已付款」。欄標題對輔助技術隱藏。
- 狀態徽章會朗讀其文字。到期警告同時以文字與圖示呈現，而不只依賴顏色。
- 「顯示全部發票」按鈕的名稱與其文字一致。收合的發票會移出 Tab 停駐點，因此 Tab 不會停在隱藏的下載按鈕上。
- Tab 依閱讀順序移動焦點，依序經過方案操作、付款按鈕、下載按鈕與「顯示全部發票」。按鈕可按 Enter 或空白鍵啟動，並顯示 Arc 焦點環。

## 動態效果

- 較舊的發票以無回彈的高度彈簧展開與收合；「顯示全部發票」的箭頭以俐落的彈簧轉動，其文字則以短暫的模糊切換。
- 原地變更的徽章與按鈕文字會以模糊切換，與 `Badge` 和 `Button` 的做法相同。到期資訊列會平滑過渡到警告色。
- 減少動態效果時保留淡入淡出，但高度與箭頭改為直接跳變。關閉動態效果時，所有內容立即就位。

## 相關元件

- 其他設定區塊：`SettingsPage`、`SettingsCommand`、`SecuritySettings`、`UsageBilling` 與 `CancelFlow`（處理 `CancelPlanRequested` 的自然選擇）。
- `SettingsGroup` 與 `SettingsRow`，即組成此區塊的設定群組與設定列。
- `Badge` 用於方案與發票狀態，`Button` 用於操作。
- `BillingToggle` 與 `BillingPrice` 用於選擇方案，`Dialog` 用於確認變更。
