---
title: 支付流程
summary: 一笔银行卡付款从结账开始，经过令牌化、授权和发卡行的决定，直至结算。
source-hash: 5a65871a5021
---

<Demo name="CardPayment" />

## 何时使用

- 说明客户点击“付款”到商户收到款项之间发生的过程。
- 结账页和帮助页需要说明付款为何会被通过或拒绝时。
- 支付接入文档，按配置显示金额、币种和商户。

## 何时不用

- 输入付款金额：用 `NumberField`；输入银行卡信息本身则用表单。
- 绘制随时间变化的总额：用图表分类中的 `BarChart` 或 `LineChart`。

## 用法

故事会依次播放每一笔付款。图表下方的说明文字描述当前步骤，每笔获批的付款都会让商户余额增加。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:PaymentFlow />
</UserControl>
```

### 金额、币种与商户

`Amount` 为 `decimal` 类型（默认 42），`Currency` 为 ISO 币种代码（默认 USD）。`MerchantName` 设置结账时显示的商户名称，`CardLast4` 显示银行卡号的后四位。常见币种以符号显示，其他币种以代码显示。

<Demo name="AmountAndMerchant" />

### 拒绝

`DeclineEvery` 每隔 N 笔付款拒绝一笔（默认 4）。设为 0 则通过所有付款。

```xml
<arc:PaymentFlow DeclineEvery="0" />
```

### 标签

`Labels` 接受一个 `PaymentFlowLabels` 对象，用于设置图表中所有可见的文字，包括徽标（`已通过`、`已拒绝`、`已付款`）和步骤说明文字。

```csharp
var payment = new PaymentFlow
{
    Amount = 19.00m,
    Labels = new PaymentFlowLabels { Merchant = "商户", Issuer = "发卡行", Approved = "通过", Declined = "拒绝" }
};
```

### 响应决定

发卡行对付款作出决定时会触发 `PaymentCompleted`，事件参数中包含 `Approved` 和 `Amount`。`PaymentCount` 和 `Balance` 分别给出已作出决定的付款笔数和商户余额。

```csharp
payment.PaymentCompleted += (_, e) =>
    log.Text = $"{(e.Approved ? "已通过" : "已拒绝")} {e.Amount:0.00} · 余额 {payment.Balance:0.00}";
```

### 播放

`IsPlaying` 用于播放和暂停故事，`Speed` 缩放播放速度（0.25 到 4），`Restart()` 从第一步重新开始。`Step`、`StepCount` 和 `StepChanged` 跟踪一笔付款的七个步骤。

```xml
<arc:PaymentFlow Speed="0.5" IsPlaying="False" />
```

### 减弱动效

在减弱动效下，插图是获批路径的静态画面。

<Demo name="ReducedMotion" />

## API 参考

<!-- api: PaymentFlow -->

<!-- api: PaymentFlowLabels -->

## 无障碍

- `Label` 是无障碍名称，当前的说明文字作为帮助文本提供。
- 插图可获得焦点，焦点环宽 2px。
- 空格键或 Enter 播放与暂停故事，R 键从头开始播放。
- 说明文字的变化不会被播报，因此故事不会向屏幕阅读器用户重复播报。

## 动效

- 金额浮现在客户上方，银行卡信息传送至网关，请求经由卡组织网络到达发卡行。
- 发卡行的决定以绿色“已通过”或红色“已拒绝”徽标显示，响应沿另一条通道返回。
- 获批的付款沿虚线结算至商户账户，余额读数随之增加。
- 减弱动效和关闭动效时，显示获批路径的静态画面，并带有“已通过”和“已付款”徽标。播放按钮会隐藏。

## 相关组件

- `DataFlow`：付款后存储事件的管道。
- `MultiRegionFailover`：区域故障时保持支付服务可用。
- `Stepper`：用户逐步完成的固定步骤序列。
