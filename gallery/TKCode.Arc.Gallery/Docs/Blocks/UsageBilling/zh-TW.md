---
title: 用量計費
summary: 按用量計費的週期，顯示已花費金額、預估總額、每日花費、各計量項目的超額以及支出上限。
source-hash: 6b25f373b4b6
---

<Demo name="Period" />

## 何時使用

用量計費是在基本費用之上還按耗用量收費的方案的帳單頁面。它回答使用者在帳單寄到之前最想知道的兩個問題：這個週期目前花了多少，又會花到多少。它放在帳號或帳單頁面中，位於帳單紀錄上方，也是擁有者設定支出上限的地方。

- **按用量計費的 API 方案**：一個開發者平台，在包含的一百萬次之外，每 10,000 次呼叫收費 $0.40。每個計量項目的超額會隨著產生而顯示，擁有者在帳單送達之前就能看到即將出現的意外支出。
- **儲存空間與建置時間**：一個雲端產品，對儲存空間以 GB 計費，對超出方案額度的 CI 建置分鐘數計費。每個計量項目保留自己的單位，因此 "3,420 min" 與 "182 GB" 並排顯示時都能正確閱讀。
- **團隊的支出上限**：擁有者把本週期限定為 $250。摘要在達到 80% 時變為「接近上限」，到達上限時變為「已達上限」，而且金額可以在顯示的位置直接編輯。
- **每日花費檢視**：一個財務頁面，繪製每天的花費金額，這樣某一天的高峰可以與平均值對照看出。
- **即時用量**：一個帳號頁面，頁面開啟期間會持續收到用量事件。總計、預估總額和長條會隨每次變動更新，無需重新整理。

## 何時不用

- **沒有按量計費的方案**：方案、付款方式和帳單請用 `BillingOverview`。
- **沒有價格的配額**，例如相對於方案空間或剩餘席次已用的儲存空間：用 `UsageMeter` 或 `Progress`，它們顯示佔上限的比例，但不為超額定價。
- **跨多個週期的用量**：單獨使用 `BarChart` 或 `LineChart` 查看趨勢。本區塊只顯示一個週期。
- **歷史帳單清單**：用 `Table`，它可以對列排序和篩選；本區塊保留給進行中的週期。

## 用法

設定週期、基本費用和計量項目。每個 `UsageBillingMeter` 說明已用量、方案包含的額度以及超出部分的價格：`UnitPrice` 為每 `PriceUnit` 個單位付費，因此每 10,000 次呼叫 `$0.40` 寫作 `UnitPrice="0.40" PriceUnit="10000"`。區塊把各計量項目的費用加總到 `MeteredTotal`，再加上 `BaseFee` 得到 `CurrentTotal`，並依週期已開始的比例推算 `ProjectedTotal`。直接寫在標籤裡的計量項目會進入 `Meters`，即內容屬性。

```xml
<arc:UsageBilling PeriodStart="2026-10-01"
                  PeriodEnd="2026-10-31"
                  BaseFee="90"
                  SpendLimit="250"
                  IsSpendLimitEnabled="True">
    <arc:UsageBillingMeter Name="API 呼叫"
                           Unit="calls"
                           Used="1240000"
                           Included="1000000"
                           UnitPrice="0.40"
                           PriceUnit="10000"
                           Icon="{x:Static arc:ArcIcons.Activity}" />
    <arc:UsageBillingMeter Name="儲存空間" Unit="GB" Used="182" Included="250" UnitPrice="0.10" />
</arc:UsageBilling>
```

`UsageBilling` 衍生自 `TemplatedControl`。它的卡片由設定區塊的共用部件組成：每張卡片是一個 `SettingsGroup`，上限開關和每個計量項目的列都是 `SettingsRow`。這讓它與周圍的設定頁面保持一致，同樣的佈景主題和動態效果也隨之適用。

`DailySpend` 列出每一天的花費金額，從週期的第一天開始。今天之後的日期不繪製；沒有數值時，每日花費卡片會隱藏。傳入會引發集合變更通知的清單，例如 `AvaloniaList<double>`，圖表就會隨之更新。

```csharp
billing.DailySpend = new AvaloniaList<double> { 4.97, 4.82, 2.79, 2.41, 5.74 };
```

### 結構

1. **摘要卡片**：標題來自 `Labels.PeriodTitle`，卡片說明中是週期的日期和剩餘天數；當上限接近或已達到時，標題列的操作區會顯示狀態徽章。
2. **已花費**：已花費金額，以卡片中最大的字級顯示，上方是標籤 `SpentSoFar`。每次總額更新時它都會捲動到新值。
3. **預估**：`Projected` 以小字級顯示在金額下方，使用次要文字顏色。
4. **上限欄**：標籤 `SpendingLimit`，中等字重的上限佔比，一條 16 px 高的進度條（8 px 軌道，在 `AlertThreshold` 處有一個刻度），以及說明文字 `LimitProgress`。沒有上限時顯示 `NoLimit` 和一個「設定上限」按鈕。
5. **每日花費卡片**：一個 `BarChart`，繪圖區高 160 px；`DailySpend` 為 null 或空時隱藏。
6. **用量卡片**：每個計量項目一列，包括圖示色塊、名稱、用量行（`MeterUsage`），以及右側的費用：超額金額及其費率，或「包含在內」。每列下方有一條 6 px 的進度條：包含的部分以強調色填滿，超額部分以警告色填滿。頁尾顯示 `UsageTotal` 和計量費用總計。
7. **支出上限卡片**：一個帶開關和說明的 `SettingsRow`，下方是 320 px 寬的 `MoneyInput`（Small），縮排 50 px 以與列文字對齊。`LimitNote` 的提示位於其下方。

在摘要中，花費與上限並排顯示，中間間距 40 px。當寬度小於 `NarrowWidth`（預設 600 px）時，上限欄移到花費下方，並向下偏移 24 px。

### 接近與超過上限

上限進度比較 `CurrentTotal` 與 `SpendLimit`。花費超過 `AlertThreshold`（預設 80%，刻度所在的位置）時進度條變為警告色；達到上限時變為危險色。標題旁的徽章顯示「接近上限」或「已達上限」，`LimitStatus` 回報相同的狀態。你可以用這個狀態做自己的邏輯，例如在導覽區顯示提醒。本範例沒有 `DailySpend`，因此每日花費卡片隱藏。

<Demo name="NearLimit" />

### 即時用量

計量項目會通知自身的變動，因此區塊會隨用量進入而更新：已花費金額捲動到新總額，預估總額、計量項目的長條、它的費用以及上限進度都隨之變化。這裡每次點擊增加 50,000 次 API 呼叫，超過包含的一百萬次後每次收費 $2.00，同時在今天的長條上加上相同的數量。

<Demo name="Live" />

### 資料結構與效能

用量次數保持為 `double`，金額使用 `decimal`：`Used` 和 `Included` 計的是單位數量，而 `BaseFee`、`UnitPrice` 和 `SpendLimit` 是金額。區塊以 `decimal` 進行計算，因此總額精確到分。單價保留所需的小數位，最多四位，因此每分鐘 `$0.008` 會依原樣顯示。

每個計量項目都是一個 `INotifyPropertyChanged` 模型。修改 `Used` 會引發計算屬性 `Overage` 和 `Cost` 的變化，並更新總額、該計量項目的費用及其長條；其他列保持不變。向 `Meters` 新增或移除計量項目時，只建立或移除該列，然後重新計算總額。請以人能夠讀取的速率從用量資料來源更新計量項目；每秒變動多次的值無法被跟上。

圖表取從週期第一天到今天的日期，因此剛開始的週期只會繪製幾根長條。

### 支出上限

`IsSpendLimitEnabled` 與 `SpendLimit` 是雙向的：最後一張卡片中的開關和金額欄位都會修改它們。`SpendLimitChanged` 在每次變動時回報舊金額、新金額以及上限是否開啟。使用者在欄位中輸入時，變動只在欄位失去焦點或按下 Enter 時回報一次。沒有上限時，摘要會提供「設定上限」，它會開啟上限並把焦點移到金額欄位。

關閉上限會保留金額。編輯器收合，金額仍保存在 `SpendLimit` 中，下次開啟開關時直接使用，因此使用者不必重新輸入上個月設定的上限。

```csharp
billing.SpendLimitChanged += async (_, e) =>
{
    await api.SaveSpendingLimitAsync(e.IsEnabled ? e.NewLimit : null);
};
```

### 繫結到檢視模型

本區塊是一個週期的顯示與編輯介面。檢視模型擁有週期的值、計量項目和儲存邏輯，區塊繫結到它們。`Meters` 與 `DailySpend` 可以設定，因此檢視模型可以直接提供它們。

```csharp
public partial class UsageBillingViewModel : ObservableObject
{
    private readonly IBillingApi _api;

    public UsageBillingViewModel(IBillingApi api) => _api = api;

    [ObservableProperty]
    private string _currency = "USD";

    [ObservableProperty]
    private decimal _baseFee = 90m;

    [ObservableProperty]
    private decimal? _spendLimit = 250m;

    [ObservableProperty]
    private bool _isSpendLimitEnabled = true;

    [ObservableProperty]
    private IList<double>? _dailySpend;

    public ObservableCollection<UsageBillingMeter> Meters { get; } = new();

    public async Task SaveSpendingLimitAsync(decimal? limit) => await _api.SaveSpendingLimitAsync(limit);
}
```

```xml
<arc:UsageBilling Currency="{Binding Currency}"
                  BaseFee="{Binding BaseFee}"
                  Meters="{Binding Meters}"
                  DailySpend="{Binding DailySpend}"
                  SpendLimit="{Binding SpendLimit, Mode=TwoWay}"
                  IsSpendLimitEnabled="{Binding IsSpendLimitEnabled, Mode=TwoWay}" />
```

- `SpendLimit` 和 `IsSpendLimitEnabled` 是雙向屬性。繫結它們，使檢視模型始終持有使用者看到的值。
- 從 `SpendLimitChanged` 儲存，而不要只依賴繫結。該事件回報舊金額、新金額以及上限是否開啟，並且在欄位釋放時只為輸入的金額回報一次。
- `BaseFee`、`Currency`、`PeriodStart` 和 `PeriodEnd` 是單向輸入。它們變動時，區塊會重新計算。
- `Clock` 不是樣式屬性，因此請在程式碼中設定，而不要透過繫結設定。參見「今天與預估總額」。

### 今天與預估總額

「今天」來自 `Clock`，它是一個 `TimeProvider`（預設為系統時鐘）。它決定剩餘天數、繪製的天數以及預估總額：`BaseFee + MeteredTotal × 週期天數 ÷ 已開始的天數`。在一個 31 天週期的 10 月 20 日，目前 $12.96 的用量預估為 $20.09，加上基本費用後為 $110.09。預估總額假設用量維持目前的速度，天數比例保持在一天的比例與 1 之間。測試和預覽會傳入固定的時鐘。

```csharp
sealed class FixedClock(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now;
}

billing.Clock = new FixedClock(new DateTimeOffset(2026, 10, 20, 12, 0, 0, TimeSpan.Zero));
```

### 貨幣與文字

`Currency` 是 ISO 4217 代碼。金額在數字前顯示 `$`、`€`、`£` 或 `¥`，其他代碼則顯示在數字之後，例如 `12.96 CHF`。所有內建文字都位於 `Labels` 中；如需翻譯，請替換整個物件。`Labels.Culture` 決定日期、金額和計數的格式。

```csharp
billing.Currency = "EUR";
billing.Labels = new UsageBillingLabels
{
    Culture = CultureInfo.GetCultureInfo("nl-NL"),
    PeriodTitle = "Deze factuurperiode",
    SpentSoFar = "Tot nu toe besteed",
    Projected = amount => $"Verwacht {amount}",
};
```

### 範例：開發者平台的十月用量

開發者平台在十月週期中途的帳單頁面。90 美元的基本費用是固定的。API 呼叫已超過包含的一百萬次，因此需要付費；儲存空間仍在額度之內，不產生費用；建置分鐘數也略微超出。擁有者把帳單上限設為 $250，而上限保持開啟。每個金額、長條和預估總額都來自這三個計量項目，因此數字彼此一致。

![開發者平台的十月用量：花費、預估總額、計量項目和支出上限](images/OctoberUsage.png)

```xml
<arc:UsageBilling Width="640"
                  PeriodStart="2026-10-01"
                  PeriodEnd="2026-10-31"
                  BaseFee="90"
                  SpendLimit="250"
                  IsSpendLimitEnabled="True">
    <arc:UsageBillingMeter Name="API calls"
                           Unit="calls"
                           Used="1240000"
                           Included="1000000"
                           UnitPrice="0.40"
                           PriceUnit="10000"
                           Icon="{x:Static arc:ArcIcons.Activity}" />
    <arc:UsageBillingMeter Name="Storage"
                           Unit="GB"
                           Used="182"
                           Included="250"
                           UnitPrice="0.10"
                           Icon="{x:Static arc:ArcIcons.Database}" />
    <arc:UsageBillingMeter Name="Build minutes"
                           Unit="min"
                           Used="3420"
                           Included="3000"
                           UnitPrice="0.008"
                           Icon="{x:Static arc:ArcIcons.Timer}" />
</arc:UsageBilling>
```

### 文案規範

- **計量項目名稱**：句首大寫的名詞，說明被計量的是什麼，例如 "API calls"、"Storage"、"Build minutes"。單位來自 `Unit`，並沿用方案自己的說法："calls"、"GB"、"min"。
- **週期標題**：保留「本計費週期」，由說明文字帶出日期。標題中寫入月份名稱，在週期變更後就會過時。
- **上限文案**：「支出上限」命名該設定，說明文字陳述效果（「限制一個計費週期的帳單總額。」）。在 `LimitNote` 中說明每個門檻會發生什麼，百分比與 `AlertThreshold` 保持一致。
- **金額與計數**：讓區塊透過 `Culture` 和 `Currency` 進行格式化。不要把金額預先格式化為文字，因為文字值無法跟隨地區設定的變更。

| 不要寫 | 改為 |
|---|---|
| 帳單總額 | 已花費 |
| 上限 | 支出上限 |
| 點此設定上限 | 設定上限 |
| 超出 240000 | 超出 240K（由區塊格式化） |
| 預估帳單 | 預估 $110.09 |

### 最佳做法

**建議**

- 在預覽和測試中設定 `Clock`。剩餘天數、繪製的天數和預估總額都取決於今天，固定的時鐘能讓截圖和測試保持穩定。
- 用 `IsSpendLimitEnabled` 關閉上限，而不是清空金額。金額會保留到下次開啟開關時使用。
- `Currency` 與 `Labels.Culture` 要一起設定。貨幣決定符號，地區決定分隔符號，因此應為同一地區同時設定兩者。
- 應用程式其他地方的提醒使用 `LimitStatus`。它與徽章顯示的是同一狀態，因此提醒與卡片永遠不會互相矛盾。

**避免**

- 把預估總額當作帳單展示。它假設用量維持目前速度，因此要像預設值那樣標註為「預估」。
- 只在介面中實施上限。區塊只顯示狀態並引發 `SpendLimitChanged`，它不會停止用量。你的服務必須執行上限，並讀取同一個金額。
- 在一個區塊中混合多個週期。它顯示的是從 `PeriodStart` 到 `PeriodEnd` 的一個週期；第二個週期需要自己的區塊或趨勢圖表。
- 在用量次數到達計量項目之前對其取整。超額和費用來自精確的次數，取整可能使次數越過包含的額度。

### 常見問題

**為什麼預估總額與我自己的估算不同？** 預估總額等於 `BaseFee` 加上計量費用總計，再乘以依 `Clock` 已開始的週期天數比例。在一個 31 天週期的 10 月 20 日，$12.96 的用量預估為 $20.09 的用量費用，加上基本費用後為 $110.09。先檢查 `Clock`。

**為什麼沒有每日花費卡片？** `DailySpend` 為 null 或空時會隱藏該卡片。卡片也只繪製到今天為止的日期，因此剛開始的週期只會顯示很短的圖表。

**上限比較的是哪個金額？** `CurrentTotal`，即基本費用加上計量費用總計，而不只是用量費用。在 90 美元基本費用的情況下，$250 的上限還剩 $160 的用量空間才會到達。

**區塊會在上限處停止用量嗎？** 不會。區塊顯示 `LimitStatus` 並引發 `SpendLimitChanged`。由你的帳單服務執行上限，欄位下方的說明描述的正是你所實作的行為。

## API 參考

<!-- api: UsageBilling -->

### UsageBillingMeter

一個可以在 XAML 中撰寫的一般模型（`INotifyPropertyChanged`）。修改任何值都會引發 `PropertyChanged`，區塊隨之重新計算。

| 屬性 | 類型 | 說明 |
|---|---|---|
| `Name` | `string` | 被計量的內容，例如 "API calls"；即該列的標題。 |
| `Unit` | `string` | 金額之後的單位，例如 "calls"、"GB" 或 "min"。 |
| `Used` | `double` | 本週期目前已用的數量。 |
| `Included` | `double` | 方案不額外收費包含的數量。為 0 時，該列只顯示用量而不顯示長條。 |
| `UnitPrice` | `decimal` | 超出包含數量後，每 `PriceUnit` 個單位的價格。 |
| `PriceUnit` | `double` | `UnitPrice` 為多少個單位付費（預設 1）。 |
| `Icon` | `Geometry?` | 顯示在該列色塊中的 24×24 描邊圖示。 |
| `Overage` | `double` | *唯讀。* `max(0, Used − Included)`。 |
| `Cost` | `decimal` | *唯讀。* `Overage ÷ PriceUnit × UnitPrice`。 |

### UsageBillingLimitChangedEventArgs

| 屬性 | 類型 | 說明 |
|---|---|---|
| `OldLimit` | `decimal?` | 變更前的上限金額。 |
| `NewLimit` | `decimal?` | 變更後的上限金額。 |
| `IsEnabled` | `bool` | 變更後上限是否開啟。 |

### UsageBillingLimitStatus

| 值 | 意義 |
|---|---|
| `None` | 沒有生效的上限：已關閉，或沒有正數金額。 |
| `Ok` | 花費低於提醒門檻。 |
| `Near` | 花費已超過提醒門檻，但未達到上限。 |
| `Reached` | 花費已達到或超過上限。 |

### UsageBillingLabels

每個項目都有英文預設值。帶參數的項目會接收格式化後的文字。

| 屬性 | 預設值 |
|---|---|
| `Culture` | en-US |
| `PeriodTitle` | "This billing period" |
| `DaysLeft` | 天數 ⇒ "11 days left"、"1 day left"、"Last day" |
| `PeriodEnded` | "Period ended" |
| `SpentSoFar` | "Spent so far" |
| `Projected` | 金額 ⇒ "Projected $110.09" |
| `SpendingLimit` | "Spending limit" |
| `LimitProgress` | (目前, 上限) ⇒ "$102.96 of $250.00 limit" |
| `NoLimit` | "No spending limit" |
| `SetLimit` | "Set a limit" |
| `NearLimit` / `LimitReached` | "Near limit" / "Limit reached" |
| `DailySpend` | "Daily spend"（圖表的無障礙名稱） |
| `DailyAverage` / `DaySpend` | "Average daily spend" / "Spent"（圖表在靜止時以及讀取某一天時的標題） |
| `Day` | "Date"（圖表的資料表） |
| `Usage` | "Usage" |
| `MeterUsage` | (已用, 包含, 單位) ⇒ "1.2M of 1M calls included" |
| `MeterUsageOnly` | (已用, 單位) ⇒ "3.4K min"（沒有包含額度的計量項目） |
| `Overage` | 金額 ⇒ "240K over"（在計量長條的無障礙名稱中） |
| `Included` | "Included" |
| `Rate` | (價格, 每) ⇒ "$0.40 / 10K calls" |
| `UsageTotal` | "Usage charges so far" |
| `SpendingLimitDescription` | "Cap the total bill for one billing period." |
| `LimitAmount` | "Limit amount" |
| `LimitNote` | 百分比 ⇒ "We email the owners at 80% and pause usage-based features at 100%." |

## 無障礙

- 每張卡片都是一個 `SettingsGroup`。已花費金額與其標籤一起朗讀，例如「已花費：$102.96」。
- 上限進度是一條進度條，名稱類似「支出上限：$102.96 / $250.00 上限」。「接近上限」和「已達上限」徽章是禮貌的即時區域，因此變動會被播報。
- 每個計量長條都是一條進度條，名稱包含計量項目、用量和超額，例如「API calls：1.2M of 1M calls included，超出 240K」。費用與費率一起朗讀，例如「$9.60，$0.40 / 10K calls」。
- 開關以設定的標題作為名稱，以說明作為說明文字。金額欄位的標籤為「上限金額」，並由其下方的提示描述。
- 區塊本身不取得焦點，取得焦點的是其中的控制項。Tab 順序從上到下依卡片排列。
- 上限關閉時，編輯器是收合的。它的欄位仍保留在樹狀結構中，因此區塊會在上限開啟之前將其移出 Tab 順序。

### 鍵盤

| 按鍵 | 操作 |
|---|---|
| `Tab` / `Shift+Tab` | 依卡片順序在上限開關、金額欄位（僅在上限開啟時）、「設定上限」按鈕和每日花費圖表之間移動 |
| 金額欄位中的 `Enter` | 結束編輯並回報新的上限 |
| 「設定上限」上的 `Enter` | 開啟上限，並把焦點移到金額欄位 |
| 圖表上的方向鍵 | 在日期之間移動；Home 和 End 移到第一天和最後一天 |
| 圖表上的 `Page Up` / `Page Down` | 移動一週 |
| 圖表上的 `Escape` | 把標題恢復為平均值 |

圖表會取得焦點。它的無障礙摘要會說明最高和最低的日期，因此使用者在逐一瀏覽日期之前就能聽到數值範圍。

## 動態效果

- 已花費金額會逐位捲動到新總額。
- 計量長條和上限進度會彈性伸展到新的長度；上限的顏色在越過提醒門檻或上限時交叉淡變。
- 狀態徽章在標題旁彈出和收合。
- 上限編輯器在開關變動時以彈簧方式展開和收合高度。
- 圖表的長條在首次看到圖表時以波浪方式生長，數值變動時平滑變形。
- 減少動態效果時只保留顏色淡變：長條和編輯器立即採用新尺寸。關閉動態效果時所有變動都立即完成。

| 變化 | 完整 | 減少 | 關閉 |
|---|---|---|---|
| 已花費金額變動 | 數字捲動到新總額 | 立即變動 | 立即完成 |
| 計量長條或上限進度改變長度 | 彈性伸展到新長度 | 立即採用新長度 | 立即完成 |
| 上限越過提醒門檻或上限 | 顏色交叉淡變（標準） | 顏色交叉淡變（快速） | 立即完成 |
| 上限編輯器展開或收合 | 高度以彈簧變化 | 立即採用新高度 | 立即完成 |
| 每日花費圖表出現或變動 | 長條以波浪生長，然後變形 | 立即變動 | 立即完成 |

## 相關元件

- 其他設定區塊：`SettingsPage`、`SettingsCommand`、`SecuritySettings`、`BillingOverview` 和 `CancelFlow`。
- `SettingsGroup` 與 `SettingsRow`，這裡每張卡片所用的共用部件（在 `SettingsPage` 頁面中說明）。
- 每日花費使用 `BarChart`，已花費金額使用 `AnimatedCounter`，上限使用 `MoneyInput` 與 `Switch`，其狀態使用 `Badge`。
- 沒有價格的配額使用 `UsageMeter` 和 `Progress`。

| 元件 | 適用情境 |
|---|---|
| `BillingOverview` | 頁面需要顯示方案、付款方式和帳單，無論是否包含用量 |
| `UsageMeter` / `Progress` | 配額有上限但沒有價格，例如席次或儲存空間 |
| `BarChart` / `LineChart` | 跨多個週期的趨勢比單一週期的總額更重要 |
| `MoneyInput` | 單獨輸入一個金額，位於支出上限卡片之外 |
| `Table` | 歷史帳單或用量明細需要排序和篩選 |
| `CancelFlow` | 擁有者要結束方案，並需要填寫原因和提供優惠的步驟 |
