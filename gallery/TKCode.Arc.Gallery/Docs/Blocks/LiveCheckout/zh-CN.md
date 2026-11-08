---
title: Live checkout
summary: 订阅结账页面，订单摘要会随套餐、计费周期、附加项和账单信息的每次变化重新计算，付款后移到中央并展开成收据。
source-hash: 4e5adf668697
---

<Demo name="Preview" />

## 何时使用

`LiveCheckout` 是用于付费注册或订阅升级的单页结账。左侧的表单一次性收集所有信息：快捷钱包、套餐和计费周期、席位数、附加项、账单信息和银行卡。右侧的订单摘要在客户仍在考虑时就回答"我要付多少钱？"：每次变化都会重新计算各行、优惠码、账单国家的税和合计，每个金额都会逐位滚动到新值。付款成功后，表单退到一旁，摘要平滑移到中央并变成收据。

- **按席位计费产品的自助注册**：团队选择套餐，设置使用人数，无需联系销售即可付款。
- **从免费版升级**：用客户点击的套餐和他们已在使用的席位数打开结账，他们只需添加一张银行卡。
- **带可选附加项和地区税的套餐**：附加项作为摘要中的行出现和移除，税这一行随国家变化（VAT、GST、销售税或不收税）。

## 何时不用

- **带购物车和配送的实物商品**：使用购物车（Cart drawer）以及为地址、配送方式和运费设计的结账。
- **只需要合计，不需要付款**：如果付款在别处进行，你只需要一个带促销码的摘要，请使用 Checkout summary。
- **在套餐之间做选择**：在客户做出决定之前，用 Plan comparison 或 Pricing calculator 并排比较套餐，然后用选定的套餐打开结账。
- **需要自己存储的银行卡数据**：这里的银行卡字段是普通文本框，适合预览或沙盒环境。在生产环境中，请用支付服务商托管的银行卡字段替代它们，或者让快捷钱包承载银行卡。

## 用法

本区块派生自 Avalonia 的 `TemplatedControl`。不做任何设置时，它显示示例目录（三个套餐、三个附加项、六个国家、三个虚构的钱包），并模拟优惠码校验和付款，因此你可以直接把它放到页面上试用。要用于真实结账，请提供你的目录和两个处理程序。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:LiveCheckout x:Name="Checkout"
                      Currency="EUR"
                      SelectedPlanId="studio"
                      Seats="3"
                      Billing="Yearly"
                      CountryCode="FR" />
</UserControl>
```

```csharp
Checkout.Plans =
[
    new LiveCheckoutPlan { Id = "sketch", Name = "Sketch", MonthlySeatPrice = 9m, Blurb = "Boards and comments" },
    new LiveCheckoutPlan { Id = "studio", Name = "Studio", MonthlySeatPrice = 19m, Blurb = "Shared libraries" },
];
Checkout.Addons = [new LiveCheckoutAddon { Id = "review", Name = "Client review", MonthlyPrice = 29m }];

// Your server checks the code and charges the order.
Checkout.ValidateCouponAsync = (code, ct) => api.CheckCouponAsync(code, ct);
Checkout.PayAsync = async (order, ct) =>
{
    var charge = await api.ChargeAsync(order, ct);          // throw to show a message above Pay
    return new LiveCheckoutReceipt { OrderNumber = charge.Number };
};
Checkout.Paid += (_, e) => analytics.Track("checkout_paid", e.Order.Totals.Total);
```

<Demo name="Handlers" />

### 结构

- **快捷钱包**：一行最多三个按钮（44 px，使用钱包自己的颜色，而不是主题色），下面是一行 "Or pay with card"。`Wallets` 列表为空时，这一行按钮和这行文字都会隐藏。
- **套餐**：标题旁是计费切换（Monthly / Yearly，年付折扣以绿色显示），下面每个套餐一张卡片，包含名称、每席位每月价格（周期变化时滚动）和一行简短说明。选中的卡片带有一个圆环，会在卡片之间平滑移动。
- **席位**：一张卡片，数字框两侧是 − / + 步进按钮。
- **附加项**：复选框行组成的列表，每行有名称、一行说明和月价格。
- **账单信息**：Email、Name on card、Country（一个 `Select`）、Address、City 和 Postal code。有效字段末尾显示绿色对勾；无效字段在下方显示消息。
- **银行卡**：标题旁是 "Use test card"（设置了 `TestCard` 时显示），下面是 Card number、Expiry 和 CVC。识别出卡号后，卡组织名称会在其标签末尾淡入。
- **付款**：一个 50 px 高的按钮，显示 "Pay" 和合计；发生付款错误时，错误显示在按钮上方；按钮下方是一行安全说明（`LiveCheckoutLabels.SecureNote`；为空时隐藏）。
- **订单摘要**：一张卡片，包含一行套餐（"8 seats × $22.40 × 12 months"）和每个勾选的附加项各一行，优惠码位置（Add coupon、优惠码输入框或已应用的优惠码），Subtotal，可选的折扣行，税这一行和合计。
- **收据**：付款后的同一张卡片，包含对勾徽标、标题、一句关于套餐的说明、各行和合计，虚线分隔线下方是订单号、付款方式、日期和收据邮箱，然后是 "Receipt PDF" 和 "Start over"。

### 金额如何计算

所有金额都是以 `Currency` 主单位表示的 `decimal`，并使用 `Labels.Culture` 的区域格式进行格式化。

| 值 | 月付 | 年付（`YearlyDiscount` d，默认为 0.2） |
| --- | --- | --- |
| 套餐卡片上的席位价格 | `MonthlySeatPrice` | `MonthlySeatPrice × (1 − d)`，四舍五入到分 |
| 套餐行 | `seats × MonthlySeatPrice` | `seats × seat price × 12` |
| 附加项行 | `MonthlyPrice` | `MonthlyPrice × 12 × (1 − d)` |

然后：**小计**是各行之和；**折扣**是小计乘以优惠码的百分比，四舍五入到分；**税**是 `(subtotal − discount) × TaxRate`（该国家的税率），四舍五入到分（税率为 0 时显示 "Not charged"）；**合计**是 `subtotal − discount + tax`。以示例数据为例，在荷兰按年计费购买 8 个席位的 Crew 并加上 Priority care，金额为 2,150.40 + 432.00 = 2,582.40，加上 21% 的 VAT 542.30，今天应付 3,124.70。

税这一行以国家和税率命名，例如 "VAT 21%" 或 "GST 5%"，末尾的零会被去掉（"8.1%"）。席位数不影响附加项价格。`Totals` 和 `Lines` 始终保存屏幕上显示的内容，`TotalsChanged` 报告每次变化。

请把这些金额视为预览。扣款之前在你的服务器上重新计算订单，依据 `LiveCheckoutOrder` 中的 id，而不是其中的金额。

### 付款前的检查

字段在被编辑并离开后开始检查，此后每次按键都会检查；没有输入就离开的字段不会被标记。Pay（或在任意字段中按 Enter）会一次性检查所有字段，显示所有消息，并按表单顺序把焦点移到第一个需要处理的字段。所有字段通过之前不会扣款。

| 字段 | 规则 |
| --- | --- |
| Email | 必填；形如 `name@company.com`。 |
| Name on card | 至少两个字符。 |
| Address | 至少三个字符。 |
| City | 必填。 |
| Postal code | 必填；必须完全匹配该国家的 `PostalPattern`，否则显示其 `PostalHint`。更改国家时，已编辑过的邮编会立即重新检查。 |
| Card number | 必填；输入时自动分组（4-4-4-4，15 位卡号为 4-6-5）；长度满足其卡组织的要求；通过 Luhn 校验。 |
| Expiry | MM/YY。首位数字为 2 到 9 时变为 "0N/"；斜杠会自动添加；月份必须在 01–12 之间且不能已过去（当前月份可以）。 |
| CVC | 三位数字，15 位卡号为四位。 |

卡组织是虚构的，按卡号开头数字识别：Novacard（4）、Meridian（51–55、2221–2720）和 Atlas（34、37；十五位卡号和四位安全码）。用 `LiveCheckoutLabels.BrandName` 给它们改名。

### 优惠码

"Add coupon" 会变成一个优惠码输入框和一个 Apply 按钮（输入框获得焦点，并把输入的内容转为大写）。按 Enter 或 Apply 会调用 `ValidateCouponAsync`，期间 Apply 显示加载指示器；按 Escape 关闭输入框。被拒绝的优惠码会在输入框下方显示结果的 `Message`（或 `LiveCheckoutLabels.CouponNotFound`）。被接受的优惠码会变成一个标签块，显示优惠码和 "15% off"，合计中滑入一行 "Discount SPRING15"，焦点移到标签块的移除按钮。移除标签块后，"Add coupon" 重新出现，合计滚动回原值。没有处理程序时，示例会在 480 ms 后接受 SPRING15（15%）和 CREW10（10%）。处理程序抛出异常时视为拒绝，并显示异常的消息。

### 付款

`PayAsync` 接收一个 `LiveCheckoutOrder`，其中包含套餐、席位数、周期、附加项、优惠码、账单信息、付款方式、各行和合计。它运行期间 `IsProcessing` 为 true：付款按钮显示 "Charging card…" 和加载指示器，表单被禁用。返回 `LiveCheckoutReceipt`（或 null）表示成功；你留为 null 的内容会被自动填入（六位随机数字作为订单号、时钟的日期、"Novacard ending 1111"、邮箱字段的值）。抛出异常表示失败：异常的消息显示在付款按钮上方，直到下一次编辑，同时引发 `PaymentFailed`。

快捷钱包会跳过字段检查，以 `PaymentMethod` 设为 `Wallet` 和钱包的 id 调用 `PayAsync`；加载指示器显示在钱包按钮上，而不是付款按钮上。没有处理程序时，区块模拟付款：银行卡 1.5 s，钱包 1.1 s，卡号 `4000 0000 0000 9995`（`LiveCheckoutSamples.DeclinedCardNumber`）会被拒绝。

成功后 `IsPaid` 为 true，`Receipt` 保存收据显示的内容，并引发 `Paid`。"Receipt PDF" 会引发 `ReceiptRequested`（在那里生成或下载文档），其标签变为 "Downloaded"。"Start over" 或 `Reset()` 会把每个值恢复为区块初次显示时的状态，并返回表单。`SubmitAsync()` 的作用与付款按钮相同，并返回付款是否成功；`FillTestCard()` 的作用与 "Use test card" 相同。

### 其他文字、货币和国家

每段可见文字和每个无障碍名称都是 `LiveCheckoutLabels` 的一个属性，默认为英文；带数字的句子是函数（`PlanDetail`、`ReceiptSentence`、`CouponNotFound`）。它的 `Culture` 用于格式化金额和收据日期。可以把 `Currency` 设为任意 ISO 4217 代码；在 `Countries` 中为每个国家提供税名、税率和邮编规则。

<Demo name="Labels" />

### 文案规范

- 套餐名称保持一个词，下方说明保持一个短语；这样三个套餐在任何宽度下都能放进一行。
- 附加项说明写成带来的好处（"Answers within four hours"），而不是功能列表。
- 邮编提示写成正确格式的示例（"Use the A1A 1A1 format"），而不是规则。
- 付款错误应说明下一步该做什么（"Try a different card"），而不是支付服务商返回的代码。

### 最佳实践

- 用客户已在别处选好的套餐和席位数打开结账；表单已经够长了，不要让客户重复做决定。
- 让 `Currency` 和各国家的税规则与服务器实际扣款保持一致，这样预览永远不会与发票不符。
- 只在有帮助时才在沙盒环境中使用 `TestCard` 和快捷钱包；在生产环境中把 `TestCard` 设为 null。
- 在小屏幕上把区块放进滚动视图。表单以两列滚动时，摘要停留在视图顶部；收据出现时会被滚动到可见区域。

### 常见问题

**为什么收据中没有优惠码位置？** 已付款的订单不能再使用优惠码；在收据出现的那一帧，优惠码位置会离开摘要，已应用的折扣作为一行保留。

**可以保留我自己的表单字段吗？** 本区块是一个完整的流程。如果需要不同的字段，请自己构建表单，并在旁边使用 Checkout summary。

**区块会向外发送任何数据吗？** 不会。区块之外的一切都通过 `ValidateCouponAsync` 和 `PayAsync` 进行；没有它们时，区块只做模拟。

## API 参考

<!-- api: LiveCheckout -->

<!-- api: LiveCheckoutPlan -->

| 属性 | 类型 | 说明 |
|---|---|---|
| `Id` | `string` | 稳定的 Id，与 `SelectedPlanId` 匹配。 |
| `Name` | `string` | 卡片和摘要中显示的套餐名。 |
| `MonthlySeatPrice` | `decimal` | 一个席位一个月的价格。 |
| `Blurb` | `string?` | 价格下方的一行简短说明；为 null 时隐藏。 |

<!-- api: LiveCheckoutAddon -->

| 属性 | 类型 | 说明 |
|---|---|---|
| `Id` | `string` | 稳定的 Id，选中时列在 `SelectedAddonIds` 中。 |
| `Name` | `string` | 附加项名称。 |
| `Description` | `string?` | 名称下方的一行简短说明。 |
| `MonthlyPrice` | `decimal` | 一个月的固定价格。 |

<!-- api: LiveCheckoutCountry -->

| 属性 | 类型 | 说明 |
|---|---|---|
| `Code` | `string` | ISO 3166 代码，与 `CountryCode` 匹配。 |
| `Name` | `string` | 国家列表中显示的名称。 |
| `TaxRate` | `decimal` | 0 到 1（21% 写作 0.21）；为 0 时显示 "Not charged"。 |
| `TaxLabel` | `string?` | 税名（"VAT"、"GST"）；为空时使用 `TaxFallback`。 |
| `PostalPattern` | `string?` | 邮编必须完整匹配的模式；为 null 时接受任何值。 |
| `PostalHint` | `string?` | 邮编不匹配时显示的消息。 |

<!-- api: LiveCheckoutWallet -->

| 属性 | 类型 | 说明 |
|---|---|---|
| `Id` / `Name` | `string` | Id 报告在 `WalletId` 中；名称用作可访问名称并显示在收据上。 |
| `MarkText` / `Glyph` | `string? / Geometry?` | 字标文字，以及 24 单位方框内的小标志。 |
| `IsGlyphFilled` / `IsMarkItalic` | `bool` | 填充而不是描边标志；字标使用斜体。 |
| `Background` / `Foreground` / `BorderColor` | `Color` | 浅色主题下按钮自己的颜色。 |
| `DarkBackground` / `DarkForeground` | `Color?` | 深色主题下的颜色；为 null 时沿用浅色值。 |

<!-- api: LiveCheckoutTestCard -->

<!-- api: LiveCheckoutCouponResult -->

| 属性 | 类型 | 说明 |
|---|---|---|
| `IsValid` | `bool` | 代码被接受时为 true。 |
| `Code` | `string` | 显示的代码。 |
| `Percent` | `decimal` | 折扣百分比（0–100）。 |
| `Message` | `string?` | 拒绝的原因；为 null 时使用 `CouponNotFound`。 |

<!-- api: LiveCheckoutOrder -->

| 属性 | 类型 | 说明 |
|---|---|---|
| `PlanId`, `Seats`, `Billing`, `AddonIds` |  | 所选内容。 |
| `CouponCode`, `CouponPercent` | `string?, decimal` | 已应用的优惠码。 |
| `CountryCode`, `Email`, `CardholderName`, `Address`, `City`, `PostalCode` | `string` | 账单信息。 |
| `PaymentMethod`, `WalletId` |  | 卡片，或按下的钱包。 |
| `CardNumber`, `Expiry`, `Cvc`, `CardBrand` |  | 卡片信息（仅卡片支付）。 |
| `Lines`, `Totals`, `Currency` |  | 显示过的行与金额。 |

<!-- api: LiveCheckoutReceipt -->

| 属性 | 类型 | 说明 |
|---|---|---|
| `OrderNumber` | `string?` | 为 null 时显示六位随机数字。 |
| `PaidAt` | `DateTimeOffset?` | 为 null 时使用 `Clock`。 |
| `PaymentText` | `string?` | 为 null 时生成 "Novacard ending 1111" 或钱包名称。 |
| `Email` | `string?` | 为 null 时使用邮箱字段。 |

<!-- api: LiveCheckoutLine -->

<!-- api: LiveCheckoutTotalsChangedEventArgs -->

<!-- api: LiveCheckoutCouponEventArgs -->

<!-- api: LiveCheckoutPaidEventArgs -->

<!-- api: LiveCheckoutPaymentFailedEventArgs -->

<!-- api: LiveCheckoutLabels -->

## 无障碍

- 套餐和计费周期是单选组。每个附加项都是真正的复选框。席位数字框、每个字段和每个按钮都有来自 `LiveCheckoutLabels` 的名称；有错误的字段以其消息作为描述。
- 每个金额都按完整文字朗读（"$3,124.70"），从不逐位朗读；滚动的数字条对屏幕阅读器隐藏。
- 处理中状态、应用或移除优惠码以及 "Downloaded" 会以礼貌方式播报；付款错误会立即播报。
- 检查失败时，焦点移到第一个需要处理的字段。付款后焦点移到收据标题，这样屏幕阅读器会从收据开始朗读。应用优惠码后焦点移到标签块的移除按钮，按 Escape 后回到 "Add coupon"，"Start over" 后移到第一个钱包（或选中的套餐）：焦点从不丢失。
- 焦点环只在使用键盘时显示：2 px，在按钮和套餐卡片外侧 2 px 处。文本字段改用更深的边框表示焦点。

### 键盘

| 按键 | 操作 |
| --- | --- |
| Tab / Shift+Tab | 依次经过钱包、Monthly、Yearly、选中的套餐、席位步进器、附加项、账单字段、"Use test card"、银行卡字段、Pay，然后是优惠码位置。 |
| 方向键 | 在套餐上：移到并选中下一个或上一个套餐，到两端时循环。在计费切换上：切换周期。 |
| Up / Down | 在席位数字框中：增加或减少一个席位（按住 Shift 时为十个）。这是我们新增的行为。 |
| Enter | 在表单字段中：付款（检查之后）。在优惠码输入框中：应用优惠码。 |
| Escape | 在优惠码输入框中：关闭它并回到 "Add coupon"。 |
| Space | 切换获得焦点的附加项；按下获得焦点的按钮。 |

## 动效

- **金额**逐位滚动：每一位直接从旧数字滚到新数字（不绕圈），使用约四分之一秒的临界阻尼弹簧；所有位同时开始。新增的最高位立即出现，金额的宽度随之变化。
- **套餐圆环和计费胶囊**以平滑的临界阻尼弹簧（约 0.4 s）移到新的选项。
- **摘要行**、折扣行、字段消息和付款错误在淡入淡出的同时展开和收起高度，使用同样的平滑弹簧。
- **有效对勾**从 0.6 倍缩放弹出；附加项的勾选标记会逐笔画出；付款按钮标签和 "Downloaded" 标签以从模糊中短暂上升的方式切换。
- **收据**：摘要从原位置平滑移到区块中央（只有平移；新尺寸立即生效），背景和阴影在 240 ms 内变化，收据标题从 75 ms 开始在 240 ms 内淡入并上升 8 px，合计下方的详情从 176 ms 开始作为一个整体上升 12 px，徽标的对勾从 320 ms 开始在 470 ms 内画出。同一时刻，表单在 240 ms 内沿退出曲线淡出并向左移动 24 px。单列布局中，折叠的摘要改为展开（高度 480 ms，淡入 240 ms）。如果页面已滚动，会先把收据滚动到可见区域，因此平滑移动是一个连续的动作。
- **Start over** 立即恢复表单，并以同样的弹簧把摘要平滑移回它的列。
- 减少动效和关闭动效时，所有变化都立即完成：金额、行、收据和表单。付款所需时间不变，焦点的移动方式也不变。

<Demo name="ReducedMotion" />

## 响应式

- 区块读取自身的宽度，而不是窗口的宽度。宽度不小于 760 px 时（低于此宽度为 `:narrow`），表单和 380 px 宽的摘要并排放置，间距 32 px，表单滚动时摘要停留在滚动视图顶部。
- 低于 760 px 时，摘要移到表单上方，并折叠成一个显示 "Show order summary" 和合计的条；点击这个条可以展开摘要。收据占满整个宽度。
- 低于 520 px 时（`:stacked`），钱包、套餐和账单字段堆叠成一列；Expiry 和 CVC 仍然并排，每张套餐卡片把价格放在右侧。
- 悬停只改变色调；在触摸屏上没有任何功能依赖悬停。

<Demo name="Narrow" />

## 相关组件

- Checkout summary，用于在你自己的表单旁边显示带促销码的摘要。
- Plan comparison 和 Pricing calculator，用于在结账前选择套餐。
- Cart drawer，用于实物商品。
- `Input`、`Select` 和 `Checkbox`，构成这些字段的控件。
