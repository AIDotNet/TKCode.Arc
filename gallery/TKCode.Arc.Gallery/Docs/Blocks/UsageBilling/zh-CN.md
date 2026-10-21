---
title: 用量计费
summary: 按用量计费的周期，显示已花费金额、预计总额、每日花费、各计量项的超额以及花费上限。
source-hash: 964dab3e02ae
---

<Demo name="Period" />

## 何时使用

- 用于在基础费用之外还按用量收费的套餐账单页面，例如 API 调用、存储或构建分钟数。
- 在发票寄出之前展示当前周期的走势：已花费金额、预计总额以及每日花费。
- 让所有者用花费上限限制账单，并在旁边显示解释该上限的数字。

## 何时不用

- 不按用量计费的套餐：使用 `BillingOverview` 展示套餐、付款方式和发票。
- 只有配额而没有价格的单项用量，例如套餐存储空间的已用量：使用 `UsageMeter` 或 `Progress`。
- 跨越多个周期的用量：单独使用 `BarChart` 或 `LineChart`。

## 用法

设置周期、基础费用和计量项。每个 `UsageBillingMeter` 说明已用量、套餐包含的额度以及超出部分的费用：`UnitPrice` 为每 `PriceUnit` 个单位收费，因此每 10,000 次调用 `$0.40` 写作 `UnitPrice="0.40" PriceUnit="10000"`。该块会把各计量项的费用汇总到 `MeteredTotal`，加上 `BaseFee` 得到 `CurrentTotal`，并根据周期已开始的比例推算 `ProjectedTotal`。直接内联编写的计量项会放入 `Meters`。

```xml
<arc:UsageBilling PeriodStart="2026-10-01"
                  PeriodEnd="2026-10-31"
                  BaseFee="90"
                  SpendLimit="250"
                  IsSpendLimitEnabled="True">
    <arc:UsageBillingMeter Name="API 调用"
                           Unit="次"
                           Used="1240000"
                           Included="1000000"
                           UnitPrice="0.40"
                           PriceUnit="10000"
                           Icon="{x:Static arc:ArcIcons.Activity}" />
    <arc:UsageBillingMeter Name="存储" Unit="GB" Used="182" Included="250" UnitPrice="0.10" />
</arc:UsageBilling>
```

`DailySpend` 按顺序列出每一天的花费，从周期的第一天开始。今天之后的日期不绘制；没有数值时，每日花费卡片会隐藏。传入会发出集合变更通知的列表，例如 `AvaloniaList<double>`，图表就会跟随更新。

```csharp
billing.DailySpend = new AvaloniaList<double> { 4.97, 4.82, 2.79, 2.41, 5.74 };
```

### 接近与超出上限

上限进度比较 `CurrentTotal` 与 `SpendLimit`。花费超过 `AlertThreshold`（默认为 80%，即刻度所在的位置）后，进度变为警告色；达到上限后变为危险色。标题旁的徽标会显示“接近上限”或“已达上限”，`LimitStatus` 也会报告相同的状态。本例没有 `DailySpend`，因此每日花费卡片会隐藏。

<Demo name="NearLimit" />

### 实时用量

计量项会公布自身的变化，因此随着用量产生，整个块也会随之更新：已花费金额滚动到新的总额，预计总额、计量项的进度条、其费用以及上限进度也会跟着变化。本例中，每点击一次就增加 50,000 次 API 调用，超出一百万次包含额度的部分按 $2.00 计费，并把同样的 $2.00 计入今天的柱条。

<Demo name="Live" />

### 花费上限

`IsSpendLimitEnabled` 与 `SpendLimit` 为双向绑定：最后一张卡片中的开关和金额输入框都会修改它们。`SpendLimitChanged` 会报告每次变更的旧金额、新金额以及上限是否开启。用户在输入框中键入时，变更只在输入框失去焦点或按下 Enter 时报告一次。未设置上限时，摘要会提供“设置上限”，点击后开启上限并将焦点移到金额输入框。

```csharp
billing.SpendLimitChanged += async (_, e) =>
{
    await api.SaveSpendingLimitAsync(e.IsEnabled ? e.NewLimit : null);
};
```

### 今天与预计总额

“今天”来自 `Clock`，它是一个 `TimeProvider`（默认为系统时钟）。它决定剩余天数、图表中绘制的天数以及预计总额：`BaseFee + MeteredTotal × days in the period ÷ days begun`。在一个 31 天的周期中，10 月 20 日已用的 $12.96 预计会达到 $20.09，加上基础费用后为 $110.09。测试和预览会传入固定的时钟。

```csharp
sealed class FixedClock(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now;
}

billing.Clock = new FixedClock(new DateTimeOffset(2026, 10, 20, 12, 0, 0, TimeSpan.Zero));
```

### 货币与文本

`Currency` 为 ISO 4217 货币代码。金额会在数字前显示 `$`、`€`、`£` 或 `¥`，其他代码则放在数字后面，例如 `12.96 CHF`。所有内置文字都位于 `Labels` 中；替换该对象即可翻译。`Labels.Culture` 用于格式化日期、金额和数量。

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

## API 参考

<!-- api: UsageBilling -->

### UsageBillingMeter

可在 XAML 中编写的普通模型（实现 `INotifyPropertyChanged`）。修改任何值都会引发 `PropertyChanged`，块随之重新计算。

| 属性 | 类型 | 说明 |
|---|---|---|
| `Name` | `string` | 所计量的内容，例如“API 调用”；即该行的标题。 |
| `Unit` | `string` | 金额之后的单位，例如“次”、“GB”或“分钟”。 |
| `Used` | `double` | 本周期截至目前已用的数量。 |
| `Included` | `double` | 套餐无额外费用包含的数量。为 0 时，该行只显示用量而不绘制进度条。 |
| `UnitPrice` | `decimal` | 超出包含数量后，每 `PriceUnit` 个单位的价格。 |
| `PriceUnit` | `double` | `UnitPrice` 对应的单位数量（默认为 1）。 |
| `Icon` | `Geometry?` | 显示在该行图块中的 24×24 描边图标。 |
| `Overage` | `double` | *只读。* `max(0, Used − Included)`。 |
| `Cost` | `decimal` | *只读。* `Overage ÷ PriceUnit × UnitPrice`。 |

### UsageBillingLimitChangedEventArgs

| 属性 | 类型 | 说明 |
|---|---|---|
| `OldLimit` | `decimal?` | 变更前的上限金额。 |
| `NewLimit` | `decimal?` | 变更后的上限金额。 |
| `IsEnabled` | `bool` | 变更后上限是否开启。 |

### UsageBillingLimitStatus

| 值 | 含义 |
|---|---|
| `None` | 没有适用的上限：已关闭或没有正数金额。 |
| `Ok` | 花费低于提醒阈值。 |
| `Near` | 花费已超过提醒阈值，但未达到上限。 |
| `Reached` | 花费已达到或超过上限。 |

### UsageBillingLabels

每一项都有英文默认值。带参数的项会接收格式化后的文字。

| 属性 | 默认值（英文） |
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
| `DailySpend` | "Daily spend"（图表的无障碍名称） |
| `DailyAverage` / `DaySpend` | "Average daily spend" / "Spent"（图表标题，静止时与查看某一天时） |
| `Day` | "Date"（图表的数据表） |
| `Usage` | "Usage" |
| `MeterUsage` | (used, included, unit) ⇒ "1.2M of 1M calls included" |
| `MeterUsageOnly` | (used, unit) ⇒ "3.4K min"（没有包含额度的计量项） |
| `Overage` | amount ⇒ "240K over"（计量条的无障碍名称中） |
| `Included` | "Included" |
| `Rate` | (price, per) ⇒ "$0.40 / 10K calls" |
| `UsageTotal` | "Usage charges so far" |
| `SpendingLimitDescription` | "Cap the total bill for one billing period." |
| `LimitAmount` | "Limit amount" |
| `LimitNote` | percent ⇒ "We email the owners at 80% and pause usage-based features at 100%." |

## 无障碍

- 每张卡片都是一个 `SettingsGroup`。已花费金额会连同标签一起朗读，例如“迄今花费：$102.96”。
- 上限进度是一个进度条，名称类似“花费上限：$102.96（上限 $250.00）”。“接近上限”和“已达上限”徽标是礼貌的实时区域，因此变化会被播报。
- 每个计量条都是一个进度条，名称包含计量项、用量和超额，例如“API 调用：已用 1.2M 次，包含 1M 次，超出 240K”。费用会连同单价一起朗读，例如“$9.60，$0.40 / 10K 次调用”。
- 每日花费图表可以获得焦点。方向键在各天之间移动，Page Up 与 Page Down 每次移动一周，Home 与 End 分别前往第一天和最后一天，Escape 返回平均值。其无障碍摘要会说明花费最高和最低的那一天。
- 开关以设置的标题为名称，以说明为帮助文字。金额输入框的标签为“上限金额”，并由其下方的说明描述。
- “设置上限”会开启上限并将焦点移到金额输入框。Tab 顺序与卡片从上到下的顺序一致。

## 动效

- 已花费金额会逐位滚动到新的总额。
- 计量条和上限进度会以弹簧过渡到新的长度；上限的颜色在越过提醒阈值或上限时交叉淡变。
- 状态徽标在标题旁弹出和收起。
- 切换开关时，上限编辑器会以弹簧方式展开和收起高度。
- 图表的柱条在首次可见时以波浪形依次生长，数值变化时平滑变形。
- 减少动效时只保留颜色淡变：柱条和编辑器立即采用新的尺寸。关闭动效时，所有变化立即完成。

## 相关组件

- 其他设置类区块：`SettingsPage`、`SettingsCommand`、`SecuritySettings`、`BillingOverview` 与 `CancelFlow`。
- `SettingsGroup` 与 `SettingsRow`，即本页每张卡片所用的共享部件（在 `SettingsPage` 页面中说明）。
- `BarChart` 用于每日花费，`AnimatedCounter` 用于已花费金额，`MoneyInput` 与 `Switch` 用于上限，`Badge` 用于其状态。
- `UsageMeter` 与 `Progress`：适用于没有价格的配额。
