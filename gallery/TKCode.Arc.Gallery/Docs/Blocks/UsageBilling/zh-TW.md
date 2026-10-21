---
title: 用量計費
summary: 按用量計費的週期，顯示已花費金額、預估總額、每日花費、各計量項目的超額以及支出上限。
source-hash: 964dab3e02ae
---

<Demo name="Period" />

## 何時使用

- 用於在基礎費用之外還按用量收費的方案帳單頁面，例如 API 呼叫、儲存空間或建置時數。
- 在發票寄出之前展示目前週期的走勢：已花費金額、預估總額以及每日花費。
- 讓擁有者用支出上限限制帳單，並在旁邊顯示說明該上限的數字。

## 何時不用

- 不按用量計費的方案：使用 `BillingOverview` 顯示方案、付款方式和發票。
- 只有配額而沒有價格的單項用量，例如方案儲存空間的已用量：使用 `UsageMeter` 或 `Progress`。
- 跨越多個週期的用量：單獨使用 `BarChart` 或 `LineChart`。

## 用法

設定週期、基礎費用和計量項目。每個 `UsageBillingMeter` 說明已用量、方案內含的額度以及超出部分的費用：`UnitPrice` 為每 `PriceUnit` 個單位收費，因此每 10,000 次呼叫 `$0.40` 寫作 `UnitPrice="0.40" PriceUnit="10000"`。該區塊會把各計量項目的費用彙總到 `MeteredTotal`，加上 `BaseFee` 得到 `CurrentTotal`，並根據週期已開始的比例推算 `ProjectedTotal`。直接內嵌編寫的計量項目會放入 `Meters`。

```xml
<arc:UsageBilling PeriodStart="2026-10-01"
                  PeriodEnd="2026-10-31"
                  BaseFee="90"
                  SpendLimit="250"
                  IsSpendLimitEnabled="True">
    <arc:UsageBillingMeter Name="API 呼叫"
                           Unit="次"
                           Used="1240000"
                           Included="1000000"
                           UnitPrice="0.40"
                           PriceUnit="10000"
                           Icon="{x:Static arc:ArcIcons.Activity}" />
    <arc:UsageBillingMeter Name="儲存空間" Unit="GB" Used="182" Included="250" UnitPrice="0.10" />
</arc:UsageBilling>
```

`DailySpend` 依序列出每一天的花費，從週期的第一天開始。今天之後的日期不繪製；沒有數值時，每日花費卡片會隱藏。傳入會發出集合變更通知的清單，例如 `AvaloniaList<double>`，圖表就會跟著更新。

```csharp
billing.DailySpend = new AvaloniaList<double> { 4.97, 4.82, 2.79, 2.41, 5.74 };
```

### 接近與超出上限

上限進度會比較 `CurrentTotal` 與 `SpendLimit`。花費超過 `AlertThreshold`（預設為 80%，即刻度所在的位置）後，進度變為警告色；達到上限後變為危險色。標題旁的徽章會顯示「接近上限」或「已達上限」，`LimitStatus` 也會回報相同的狀態。本例沒有 `DailySpend`，因此每日花費卡片會隱藏。

<Demo name="NearLimit" />

### 即時用量

計量項目會公布自身的變化，因此隨著用量產生，整個區塊也會跟著更新：已花費金額滾動到新的總額，預估總額、計量項目的進度條、其費用以及上限進度也會一併變化。本例中，每點擊一次就增加 50,000 次 API 呼叫，超出一百萬次內含額度的部分按 $2.00 計費，並把同樣的 $2.00 計入今天的長條。

<Demo name="Live" />

### 支出上限

`IsSpendLimitEnabled` 與 `SpendLimit` 為雙向繫結：最後一張卡片中的開關和金額輸入欄都會修改它們。`SpendLimitChanged` 會回報每次變更的舊金額、新金額以及上限是否開啟。使用者在輸入欄中鍵入時，變更只會在輸入欄失去焦點或按下 Enter 時回報一次。未設定上限時，摘要會提供「設定上限」，點擊後開啟上限並將焦點移到金額輸入欄。

```csharp
billing.SpendLimitChanged += async (_, e) =>
{
    await api.SaveSpendingLimitAsync(e.IsEnabled ? e.NewLimit : null);
};
```

### 今天與預估總額

「今天」來自 `Clock`，它是一個 `TimeProvider`（預設為系統時鐘）。它決定剩餘天數、圖表中繪製的天數以及預估總額：`BaseFee + MeteredTotal × days in the period ÷ days begun`。在一個 31 天的週期中，10 月 20 日已用的 $12.96 預估會達到 $20.09，加上基礎費用後為 $110.09。測試和預覽會傳入固定的時鐘。

```csharp
sealed class FixedClock(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now;
}

billing.Clock = new FixedClock(new DateTimeOffset(2026, 10, 20, 12, 0, 0, TimeSpan.Zero));
```

### 貨幣與文字

`Currency` 為 ISO 4217 貨幣代碼。金額會在數字前顯示 `$`、`€`、`£` 或 `¥`，其他代碼則放在數字後面，例如 `12.96 CHF`。所有內建文字都位於 `Labels` 中；替換該物件即可翻譯。`Labels.Culture` 用於格式化日期、金額和數量。

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

## API 參考

<!-- api: UsageBilling -->

### UsageBillingMeter

可在 XAML 中編寫的一般模型（實作 `INotifyPropertyChanged`）。修改任何值都會引發 `PropertyChanged`，區塊隨之重新計算。

| 屬性 | 類型 | 說明 |
|---|---|---|
| `Name` | `string` | 所計量的內容，例如「API 呼叫」；即該列的標題。 |
| `Unit` | `string` | 金額之後的單位，例如「次」、「GB」或「分鐘」。 |
| `Used` | `double` | 本週期截至目前已用的數量。 |
| `Included` | `double` | 方案無額外費用內含的數量。為 0 時，該列只顯示用量而不繪製進度條。 |
| `UnitPrice` | `decimal` | 超出內含數量後，每 `PriceUnit` 個單位的價格。 |
| `PriceUnit` | `double` | `UnitPrice` 對應的單位數量（預設為 1）。 |
| `Icon` | `Geometry?` | 顯示在該列圖塊中的 24×24 線條圖示。 |
| `Overage` | `double` | *唯讀。* `max(0, Used − Included)`。 |
| `Cost` | `decimal` | *唯讀。* `Overage ÷ PriceUnit × UnitPrice`。 |

### UsageBillingLimitChangedEventArgs

| 屬性 | 類型 | 說明 |
|---|---|---|
| `OldLimit` | `decimal?` | 變更前的上限金額。 |
| `NewLimit` | `decimal?` | 變更後的上限金額。 |
| `IsEnabled` | `bool` | 變更後上限是否開啟。 |

### UsageBillingLimitStatus

| 值 | 含義 |
|---|---|
| `None` | 沒有適用的上限：已關閉或沒有正數金額。 |
| `Ok` | 花費低於提醒門檻。 |
| `Near` | 花費已超過提醒門檻，但尚未達到上限。 |
| `Reached` | 花費已達到或超過上限。 |

### UsageBillingLabels

每一項都有英文預設值。帶參數的項目會接收格式化後的文字。

| 屬性 | 預設值（英文） |
|---|---|
| `Culture` | en-US |
| `PeriodTitle` | "This billing period" |
| `DaysLeft` | days ⇒ "11 days left", "1 day left", "Last day" |
| `PeriodEnded` | "Period ended" |
| `SpentSoFar` | "Spent so far" |
| `Projected` | amount ⇒ "Projected $110.09" |
| `SpendingLimit` | "Spending limit" |
| `LimitProgress` | (current, limit) ⇒ "$102.96 of $250.00 limit" |
| `NoLimit` | "No spending limit" |
| `SetLimit` | "Set a limit" |
| `NearLimit` / `LimitReached` | "Near limit" / "Limit reached" |
| `DailySpend` | "Daily spend"（圖表的無障礙名稱） |
| `DailyAverage` / `DaySpend` | "Average daily spend" / "Spent"（圖表標題，靜止時與查看某一天時） |
| `Day` | "Date"（圖表的資料表） |
| `Usage` | "Usage" |
| `MeterUsage` | (used, included, unit) ⇒ "1.2M of 1M calls included" |
| `MeterUsageOnly` | (used, unit) ⇒ "3.4K min"（沒有內含額度的計量項目） |
| `Overage` | amount ⇒ "240K over"（計量條的無障礙名稱中） |
| `Included` | "Included" |
| `Rate` | (price, per) ⇒ "$0.40 / 10K calls" |
| `UsageTotal` | "Usage charges so far" |
| `SpendingLimitDescription` | "Cap the total bill for one billing period." |
| `LimitAmount` | "Limit amount" |
| `LimitNote` | percent ⇒ "We email the owners at 80% and pause usage-based features at 100%." |

## 無障礙

- 每張卡片都是一個 `SettingsGroup`。已花費金額會連同標籤一起朗讀，例如「迄今花費：$102.96」。
- 上限進度是一個進度條，名稱類似「支出上限：$102.96（上限 $250.00）」。「接近上限」和「已達上限」徽章是禮貌的即時區域，因此變化會被播報。
- 每個計量條都是一個進度條，名稱包含計量項目、用量和超額，例如「API 呼叫：已用 1.2M 次，內含 1M 次，超出 240K」。費用會連同單價一起朗讀，例如「$9.60，$0.40 / 10K 次呼叫」。
- 每日花費圖表可以取得焦點。方向鍵在各天之間移動，Page Up 與 Page Down 每次移動一週，Home 與 End 分別前往第一天和最後一天，Escape 返回平均值。其無障礙摘要會說明花費最高和最低的那一天。
- 開關以設定的標題為名稱，以說明為提示文字。金額輸入欄的標籤為「上限金額」，並由其下方的說明描述。
- 「設定上限」會開啟上限並將焦點移到金額輸入欄。Tab 順序與卡片從上到下的順序一致。

## 動態效果

- 已花費金額會逐位滾動到新的總額。
- 計量條和上限進度會以彈簧過渡到新的長度；上限的顏色在越過提醒門檻或上限時交叉淡變。
- 狀態徽章在標題旁彈出和收起。
- 切換開關時，上限編輯器會以彈簧方式展開和收合高度。
- 圖表的長條在首次可見時以波浪形依序生長，數值變化時平滑變形。
- 減少動態效果時只保留顏色淡變：長條和編輯器立即採用新的尺寸。關閉動態效果時，所有變化立即完成。

## 相關元件

- 其他設定類區塊：`SettingsPage`、`SettingsCommand`、`SecuritySettings`、`BillingOverview` 與 `CancelFlow`。
- `SettingsGroup` 與 `SettingsRow`，即本頁每張卡片所用的共用部件（在 `SettingsPage` 頁面中說明）。
- `BarChart` 用於每日花費，`AnimatedCounter` 用於已花費金額，`MoneyInput` 與 `Switch` 用於上限，`Badge` 用於其狀態。
- `UsageMeter` 與 `Progress`：適用於沒有價格的配額。
