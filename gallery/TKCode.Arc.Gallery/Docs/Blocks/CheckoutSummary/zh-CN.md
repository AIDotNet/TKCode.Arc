---
title: Checkout summary
summary: 一个订单摘要，被接受的优惠码变为可移除的标签块，折扣行和税费行滑入，总计滚动到新金额，付款按钮依次经历 Processing 到 Paid。
source-hash: 14d7aeafe553
---

<Demo name="Preview" />

## 何时使用

`CheckoutSummary` 是某人为购买一件商品付款前的最后一张卡片。它展示用户正在购买的内容、通向总计的每一项金额、优惠码输入框以及付款按钮。任何内容变化时都会随之移动：被接受的优惠码变为标签块，其折扣行滑入明细；税费重新计算；总计以滚动数字的方式逐位滚到新金额；付款按钮也随之显示新的价格。

- **单品结账。** 套餐、通行证、许可证或一次性购买，订单已经确定，只剩优惠码和支付。
- **升级确认。** 定价页之后的一步：用户选中的套餐、其年度折扣、税费，以及一个清晰的按钮。
- **嵌入式支付步骤。** 结账页的摘要栏，地址和银行卡字段位于别处；卡片上的付款按钮会启动宿主自己的支付调用。

## 何时不用

- **收集地址和银行卡。** 摘要不要求支付信息；请把这些字段放在单独的表单中，并将本卡片放在它旁边。
- **多件商品或多个数量。** 当用户需要添加、删除和增减商品时，请使用 `CartDrawer`。
- **现有订阅的账单。** 发票、付款方式和下一次扣款请使用 `BillingOverview`。

## 用法

该区块派生自 Avalonia 的 `TemplatedControl`，由数据驱动：`Item`（名称、描述、价格和周期）、以子元素形式编写的自动 `Adjustments`、可选的 `Tax`、`Currency`（ISO 代码），以及用于格式化金额的 `CultureName`。为优惠码输入框设置 `CouponValidator` 后它才会出现；付款按钮运行 `PayAsync`。

```xml
<arc:CheckoutSummary x:Name="Summary" Width="440">
    <arc:CheckoutSummary.Item>
        <arc:CheckoutSummaryItem Name="Sketchbook Pro, annual"
                                 Description="Infinite canvases and backups for one maker"
                                 Price="120"
                                 Period="billed yearly" />
    </arc:CheckoutSummary.Item>
    <arc:CheckoutSummaryLine Id="annual" Label="Annual plan" Note="Three months free" Amount="-30" />
    <arc:CheckoutSummary.Tax>
        <arc:CheckoutSummaryTax Label="Sales tax" Rate="0.075" />
    </arc:CheckoutSummary.Tax>
</arc:CheckoutSummary>
```

```csharp
Summary.CouponValidator = async (code, token) =>
{
    var answer = await api.CheckPromoCodeAsync(code, token);
    return answer.Ok
        ? CheckoutSummaryCouponResult.ValidPercent(answer.Code, answer.Percent, answer.Label)
        : CheckoutSummaryCouponResult.Invalid(answer.Message);
};
Summary.PayAsync = token => api.ChargeAsync(Summary.Total, token);
```

`CheckoutSummarySamples` 保存预览的内容和一个模拟后端（700 ms 的优惠码检查，接受 HELLO15 和 FRIEND5；1400 ms 的支付，可设置为失败）。请用你自己的调用替换这两者。

### 结构

- **标题**："Order summary" 为默认值（`Title`），同时也是卡片的无障碍名称。
- **条目行**：名称占一行（超出部分以省略号截断），描述可换行，价格和周期位于右侧；名称前可选一个 44 px 的 `Media` 单元格。
- **优惠码**：带有 Apply 的输入框；被接受后变为带有移除按钮的 "applied" 标签块；被拒绝时在其下方展开一行错误信息。
- **明细**：小计、按顺序排列的每个调整项、优惠码行，然后是税费。负数金额为折扣，以绿色显示。
- **总计**：以展示字体显示的滚动数字。
- **付款按钮**：带锁图标，然后是安全提示（"Encrypted payment"）和可选的 `Footnote`。

### 计算方式

小计即商品价格。调整项加到小计上（调整后的小计永远不会低于零）。百分比优惠码作用于调整后的小计，而不是标价；固定金额优惠码则从中扣除其金额，且扣除额不超过剩余金额。税费为税率乘以剩余金额，并在加入之前四舍五入到分。以示例数据计算：120 − 30 = 90，税费 6.75，总计 96.75；使用 HELLO15 时，优惠码扣除 13.50，税费 5.74，总计 82.24。

卡片上的所有金额使用相同的小数位数：当所有金额都是整数时不显示小数（$50），否则保留两位（$96.75）。折扣以真正的减号开头（−$30.00）。符号和分隔符遵循 `Currency` 与 `CultureName`；表中没有符号的货币则显示其代码。

### 优惠码

输入的字母会自动转换为大写（`UppercaseCoupon`）。点击 Apply 或在输入框中按 Enter，会将去除首尾空格后的文本发送给 `CouponValidator`；空白输入框不做任何操作。检查进行期间，Apply 显示加载动画并忽略按下操作。

- **已接受**：输入框淡出，同时标签块放大出现；该优惠码的行滑入明细，税费和总计随之更新，焦点移到标签块的移除按钮。会引发 `CouponApplied`。
- **已拒绝**：区域左右抖动，输入框的边框变为玫瑰色，并在其下方展开提示信息；焦点停留在输入框中，输入任何内容都会清除错误。`CouponRejected` 携带该提示信息。验证器抛出的异常视为拒绝，并使用标签中的通用提示信息。
- **已移除**：标签块淡出，空白输入框回来并获得焦点，该行滑出，总计回滚。会引发 `CouponRemoved`。

`ApplyCouponAsync` 和 `RemoveCoupon` 可从代码中执行相同的操作；`AppliedCoupon` 与 `CouponDiscount` 告诉你当前正在使用什么。

### 付款

付款会引发 `PayRequested`（将 `Cancel` 设为 true 即可中止），把 `PayState` 切换为 `Processing`，并等待 `PayAsync`。完成后转为 `Paid`，这是最终状态：按钮变为绿色，显示对勾，并保持禁用。抛出异常则转为 `Error`：按钮下方展开一行错误信息（`PayErrorMessage`，或标签中的通用文字），按钮显示 "Try again"，点击后开始新的尝试。处理中和付款完成后，优惠码会被锁定，以确保收费金额与显示的总计不会出现差异。`PayStateChanged` 报告每一次变化，若有异常则附带该异常。

没有 `PayAsync` 时，付款只引发 `PayRequested` 并显示 Processing；当你的支付表单或跳转返回时，请自行设置 `PayState`，并调用 `ResetPayState` 以重新开始。使用预览中的开关即可看到失败的情况。

<Demo name="Events" />

### 状态

`PayState` 可以直接设置，下方的卡片就是这样展示处理中、已付款和付款失败的。第一张卡片还带有媒体单元格，且没有优惠码输入框。

<Demo name="States" />

### 窄容器

卡片最宽为 440 px，在较窄的容器中会填满容器。描述在名称下方换行，价格列保持在一行内，标签块让优惠码和移除按钮始终可见。

<Demo name="Narrow" />

### 其他文字、货币与动效

所有可见和可朗读的文字都位于 `Labels`（即 `CheckoutSummaryLabels`）中；带有金额的条目是接收格式化后数值的函数。本示例使用德语数字规则格式化欧元，重命名了每一个标签，省略了税率（税费行随之显示 "Added at the next step"，总计也不包含税费），并以减少动效运行。

<Demo name="CustomLabels" />

### 内容指南

- 商品名称要足够简短，以便在一行内显示；细节放在描述中。
- 以调整项的实际内容来命名（"Annual plan"、"Referral credit"），并用备注说明原因（"Three months free"）。
- 拒绝提示应告诉用户下一步该做什么（"Try HELLO15"），而不只是说优惠码失败了。
- 付款按钮的标签只保留动词和金额，因为按钮本身已经表明支付是安全的。

## API 参考

<!-- api: CheckoutSummary -->

<!-- api: CheckoutSummaryItem -->

<!-- api: CheckoutSummaryLine -->

<!-- api: CheckoutSummaryTax -->

<!-- api: CheckoutSummaryCouponResult -->

<!-- api: CheckoutSummaryLabels -->

<!-- api: CheckoutSummaryCouponEventArgs -->

<!-- api: CheckoutSummaryPayRequestedEventArgs -->

<!-- api: CheckoutSummaryPayStateChangedEventArgs -->

<!-- api: CheckoutSummarySamples -->

## 无障碍

- 卡片以其 `Title` 命名。优惠码输入框名为 "Promo code"；被拒绝后，提示信息成为其帮助文字，错误行以强提示方式播报。
- 优惠码检查期间，Apply 名为 "Checking code"。标签块的移除按钮名为 "Remove code HELLO15"。
- 优惠码被接受后，焦点移到移除按钮，使键盘用户不会被丢回窗口顶部；移除优惠码后，焦点回到输入框。
- 付款按钮是一个礼貌型实时区域，以其当前标签命名，因此 "Processing"、"Paid" 和 "Try again" 会被朗读出来。付款失败的错误行会以强提示方式播报。
- 滚动数字的总计以完整金额命名（"Total $96.75"），因此屏幕阅读器不会在滚动过程中朗读数字。
- 键盘焦点环为 2 px，位于 Apply、移除按钮和付款按钮外侧 2 px 处；输入框则通过更醒目的边框显示焦点。指针按下时不绘制焦点环。

### 键盘

| Keys | Action |
| --- | --- |
| Enter | 在优惠码输入框中：应用该优惠码（空白输入框不做任何操作）。 |
| Enter / Space | 在 Apply、移除按钮或付款按钮上：按下该按钮。移除后焦点回到输入框。 |
| Tab / Shift+Tab | 没有优惠码时依次为输入框、Apply、付款按钮；有优惠码时依次为移除按钮、付款按钮。 |

## 动效

- **行**：出现或消失的行，其高度以 0.4 s 的临界阻尼弹簧伸缩，同时在 240 ms（ease-out）内淡入淡出，因此文字在行完全展开之前即可阅读。下方的各行和卡片底部随高度移动。
- **优惠码已接受**：输入框在 160 ms 内淡出；标签块从 0.97 放大，并以同样的 0.4 s 弹簧淡入。
- **优惠码已移除**：标签块在 160 ms 内淡出；输入框从 0.98 放大，并在 240 ms 内以 enter 曲线淡入。
- **优惠码被拒绝**：区域左右抖动（0、−6、6、−4、3、0 px，共 360 ms）；错误行以弹簧展开，边框颜色在 160 ms 内变化。
- **总计**：每一位数字都是一个滚轮；所有滚轮同时朝金额变化的方向转动，使用同一个弹簧，因此步数多的滚轮转得更快，且所有滚轮同时停下。字形会随其与中线的距离而淡化。
- **付款按钮标签**：任何变化（新总计、Processing、Paid）都会向上切换：旧标签在 160 ms 内上升 8 px 并淡出，新标签在 240 ms 内从下方 8 px 处上升。切换为 Paid 时，颜色在 160 ms 内变为绿色。
- **按下**：Apply 在 160 ms 内缩放至 0.97，付款按钮缩放至 0.98；鼠标指针悬停在付款按钮上时会略微变暗。
- **加载动画**每 700 ms 旋转一周。
- 减少动效：没有抖动，没有缩放；行、错误行和标签块立即出现，总计直接跳转，标签在 240 ms 内交叉淡入，加载动画减慢为每圈 1.6 s。关闭动效则一切立即变化。

## 响应式

- 卡片最宽为 440 px（`MaxWidth`），在较窄的容器中会填满容器。
- 其内边距随视口宽度变化，与参考设计一致：视口宽度的 5%，介于 20 与 28 px 之间（窗口宽 560 px 及以上为 28 px，手机上为 20 px）。
- 其他部分不随宽度变化：描述会换行，总计保持其大小，标签块中的 "applied" 字样会优先让出空间。

## 相关组件

- `CartDrawer`：多件商品、带数量并包含结账按钮的场景。
- `BillingOverview` 和 `UsageBilling`：用于现有订阅。
- `AnimatedCounter` 和 `RollingDigits`：单独用于滚动数字。
