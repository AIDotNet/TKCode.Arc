---
title: 账单概览
summary: 在一页中展示当前套餐、下次扣款、付款方式和发票记录，每项旁边都有对应的操作。
source-hash: 239be44eea73
---

<Demo name="Overview" />

## 何时使用

- 账户设置中的账单分区，用户可以在这里查看自己要付多少钱、下次何时扣款，以及之前付过什么。
- 通向应用已有账单操作的页面：更改或取消套餐、更新银行卡、修改计费邮箱和下载发票。
- 每个计费周期价格固定的订阅，无论是否按席位计费。

## 何时不用

- 总额取决于本期用量的按量计费套餐：使用 `UsageBilling`。
- 选择或比较套餐：使用带有 `BillingToggle` 和 `BillingPrice` 的定价布局。
- 收集银行卡信息：此区块只显示已存档的卡片，并请宿主应用更新卡片。
- 很长且可搜索的付款记录：使用 `SortableDataTable`。

## 用法

设置 `Plan`、`PaymentMethod`、`BillingEmail` 和 `Invoices`。这些模型都是普通类，因此既可以像预览中那样用 XAML 编写，也可以在代码中创建。它们会引发 `PropertyChanged`，所以在显示之后更改套餐、卡片或发票，视图也会随之更新。`Invoices` 初始为一个空列表，可以向其中添加项目；也可以替换它，或绑定到其他集合来显示该集合，可观察集合的变化会更新表格。发票按从新到旧的顺序显示。

```xml
<arc:BillingOverview Plan="{Binding Plan}"
                     PaymentMethod="{Binding Card}"
                     BillingEmail="{Binding BillingEmail}"
                     Invoices="{Binding Invoices}"
                     VisibleInvoiceCount="6" />
```

套餐卡片显示套餐名称和状态徽标（生效中为成功色，试用中为强调色，逾期为危险色，已取消为中性色）、席位数和续订日期，以及按月或按年计算的价格。其页脚显示下次扣款金额，取自 `NextInvoiceAmount`；该值为 null 时，则显示套餐价格。已取消的套餐没有“取消套餐”按钮；没有套餐时，卡片会提供“选择套餐”。金额使用套餐的 `Currency`，发票可以设置自己的货币。

宽度小于 560 px（`NarrowWidth`）时，价格移到套餐名称下方，页脚上下堆叠，发票列隐藏。

### 试用与即将到期的卡片

试用中的套餐会显示试用结束的时间，以及首次扣款的金额。当卡片的到期月份在 60 天内开始时，到期信息行会变为警告色并显示“即将到期”；已过到期月份的卡片则以危险色显示“已过期”。`IsPaymentMethodExpiringSoon` 和 `IsPaymentMethodExpired` 会给出相同的判断。卡片品牌以简短文字显示在小边框中（VISA、MC、AMEX），而不是标志。没有发票时，表格会替换为“暂无发票”。

<Demo name="Trial" />

“今天”的日期来自 CLR 属性 `Clock`，它是一个 `TimeProvider`（默认为系统时钟）。在测试或预览中，可以将它设为固定时钟：

```csharp
billing.Clock = TimeProvider.System; // 也可以是任何 TimeProvider，例如测试中的 FakeTimeProvider
```

### 处理操作

每个按钮都会引发路由事件，但本身不做任何修改：`ChangePlanRequested`、`CancelPlanRequested`、`UpdatePaymentMethodRequested`、`EditBillingEmailRequested` 和 `InvoiceDownloadRequested`，其中 `InvoiceDownloadRequested` 的参数携带 `Invoice`。请在这些事件中打开你自己的对话框或流程，并随后更新数据模型。此处显示前三张发票，其余的收在“显示全部发票”后面。

<Demo name="Events" />

```csharp
billing.InvoiceDownloadRequested += async (_, e) =>
{
    var pdf = await invoices.DownloadAsync(e.Invoice.Number);
    await SaveAsync(pdf, $"{e.Invoice.Number}.pdf");
};

billing.CancelPlanRequested += (_, _) => cancelDialog.Open(); // 例如在 Dialog 中使用 CancelFlow
```

`VisibleInvoiceCount`（默认为 4）设置在“显示全部发票（n）”之前显示的发票数量。按钮会展开其余发票，并变为“收起”；`IsInvoiceListExpanded` 表示同一状态，且支持双向绑定。

### 文字与语言

所有内置文字都位于 `BillingOverviewLabels` 中。替换整个对象即可改写文字或将区块本地化；带有数值的文字是函数，接收的数值已按 `Culture` 格式化。

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

## API 参考

<!-- api: BillingOverview -->

`Clock`（`TimeProvider`，默认为 `TimeProvider.System`）是一个 CLR 属性，作为到期警告中“今天”的来源。

### BillingPlan

| 属性 | 类型 | 说明 |
|---|---|---|
| `Name` | `string` | 套餐名称，例如“Studio”。 |
| `Price` | `decimal` | 每个周期收取的总额，包含所有席位。 |
| `Currency` | `string` | 价格的 ISO 4217 代码（默认为 "USD"）。 |
| `Interval` | `BillingInterval` | `Month` 或 `Year`：价格的收取频率。 |
| `Seats` | `int?` | 付费席位数；对于不按席位计价的套餐为 null。 |
| `Status` | `BillingPlanStatus` | `Active`、`Trialing`、`PastDue` 或 `Canceled`。 |
| `RenewsOn` | `DateTime?` | 下次续订日期；试用期间为试用结束日期，取消后为访问权限的结束日期。 |

### BillingPaymentMethod

| 属性 | 类型 | 说明 |
|---|---|---|
| `Brand` | `string` | 卡组织的文字名称，例如 "Visa" 或 "Mastercard"。 |
| `Last4` | `string` | 卡号的后四位。 |
| `ExpiryMonth` | `int` | 到期月份，范围为 1 至 12。 |
| `ExpiryYear` | `int` | 四位数的到期年份。 |

### BillingInvoice

| 属性 | 类型 | 说明 |
|---|---|---|
| `Number` | `string` | 发票编号，例如 "LS-2026-010"。 |
| `Date` | `DateTime` | 开具发票的日期；表格按此排序，最新的在前。 |
| `Amount` | `decimal` | 发票总额。 |
| `Currency` | `string?` | 金额的 ISO 4217 代码；为 null 时使用套餐的货币。 |
| `Status` | `BillingInvoiceStatus` | `Paid`、`Open`、`PastDue`、`Refunded` 或 `Void`。 |
| `Description` | `string?` | 发票的用途说明；会加入该行的无障碍名称中。 |

### BillingInvoiceEventArgs

| 属性 | 类型 | 说明 |
|---|---|---|
| `Invoice` | `BillingInvoice` | 下载按钮被按下的发票。 |

## 无障碍

- 每个按钮都有名称。下载按钮只有图标，名称为“下载 LS-2026-010”，同时也是其工具提示；“更新”按钮的名称为“更新付款方式”，“编辑”按钮的名称为“编辑计费邮箱”。
- 每条发票行会朗读为一行，例如“2026 年 10 月 1 日，LS-2026-010，$90.00，已付款”。列标题对辅助技术隐藏。
- 状态徽标会朗读其文字。到期警告同时以文字和图标表示，而不只依靠颜色。
- “显示全部发票”按钮的名称与其文字一致。收起的发票会移出 Tab 停靠点，因此 Tab 键不会停在隐藏的下载按钮上。
- Tab 按阅读顺序移动焦点，依次经过套餐操作、付款按钮、下载按钮以及“显示全部发票”。按钮可用 Enter 或空格键触发，并显示 Arc 焦点环。

## 动效

- 较早的发票以无回弹的高度弹簧展开和收起；“显示全部发票”的箭头以灵敏的弹簧转动，其文字则通过短暂的模糊切换。
- 原位变化的徽标和按钮文字会以模糊切换，与 `Badge` 和 `Button` 的做法相同。到期信息行会平滑过渡到警告色。
- 减少动效时保留淡入淡出，但高度和箭头改为直接跳变。关闭动效时，所有内容立即就位。

## 相关组件

- 其他设置区块：`SettingsPage`、`SettingsCommand`、`SecuritySettings`、`UsageBilling` 和 `CancelFlow`（处理 `CancelPlanRequested` 的自然选择）。
- `SettingsGroup` 和 `SettingsRow`，即组成该区块的设置分组与设置行。
- `Badge` 用于套餐与发票状态，`Button` 用于操作。
- `BillingToggle` 和 `BillingPrice` 用于选择套餐，`Dialog` 用于确认更改。
