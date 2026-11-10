---
title: Wallet card
summary: 钱包里的一张支付卡：用代码绘制的卡面和虚构的卡组织标志、余额与本月消费占限额的比例、最近的交易、让卡面结霜的冻结，以及显示并复制卡号的卡片详情。
source-hash: 3f898e148396
---

<Demo name="Preview" />

## 何时使用

`WalletCard` 像银行或钱包应用那样展示一张支付卡。左边是用代码绘制的卡片：卡面颜色、芯片、非接触标志、一个虚构的卡组织标志（倾斜圆环里的圆点）、遮住的卡号、持卡人和有效期。旁边是人们打开这张卡时最关心的问题：还有多少可用、本月限额已经用了多少、最近几笔消费是什么。卡片下方有两个操作。冻结会停止新的消费并让卡面结霜；显示详情会在一段时间内显示完整卡号和安全码，并提供复制卡号。

- **钱包或银行应用里的卡片页**，一次一张卡。
- **虚拟卡**：人们需要把卡号复制到结账页。
- **卡片丢失时的流程**：先冻结，再决定下一步。

## 何时不用

- **很多张卡的列表**：显示一排小卡面，选中后再打开这个区块。
- **添加新卡**：使用带 `Input` 字段的表单（Live checkout 区块里就有一个）。
- **完整账单**：动态列表只显示最近几行；需要筛选和分页的历史记录请用 `Table`。

## 用法

该区块派生自 Avalonia 的 `TemplatedControl`。设置卡片（`HolderName`、`CardNumber`、`Expiry`、`SecurityCode`、`Finish`）、金额（`Balance`、`MonthlyLimit`、`SpentThisMonth`、`CurrencySymbol`），并用 `WalletTransaction` 按从新到旧的顺序填充 `Transactions`。`WalletCardSamples.Apply` 会用预览中虚构的持卡人填充区块。

```xml
<arc:WalletCard HolderName="Ines Moreau" CardNumber="4929 1820 5531 4821" Expiry="08/29" SecurityCode="317"
                Balance="2481.56" MonthlyLimit="3000" SpentThisMonth="1240.18">
    <arc:WalletTransaction Merchant="Lantern Coffee" Amount="6.40" Category="Cafes" />
</arc:WalletCard>
```

### 冻结

冻结按钮会用想要的状态调用 `SetFrozenAsync` 并等待结果：按钮显示加载圈，在你的服务回应之前卡片保持原样。返回 true 时，霜层盖上卡面，冻结说明出现，并引发 `FrozenChanged`；返回 false（或抛出异常）时卡片不变，并读出 "The card could not be changed. Try again."。没有处理程序时立即生效。在代码里设置 `IsFrozen` 会直接生效。

```csharp
card.SetFrozenAsync = async (frozen, cancellation) =>
    await cards.SetFrozenAsync(cardId, frozen, cancellation);
```

### 详情

显示详情会把遮住的卡号换成完整卡号，显示 CVC 和复制卡号按钮，并在 `DetailsTimeout`（30 秒；0 表示一直显示到手动隐藏）之后再次隐藏。按 Escape 也会隐藏。

### 卡面与限额

`Finish` 可选 Midnight、Ocean、Coral、Forest 或 Accent（主题的强调色）。消费超过限额时进度条变为危险色，`MonthlyLimit="0"` 会隐藏它。

<Demo name="Finishes" />

## API 参考

<!-- api: WalletCard -->

<!-- api: WalletTransaction -->

## 无障碍

- 卡面的名称包含卡组织、持卡人、卡号后四位以及是否已冻结。
- 冻结和显示详情是按钮，名称随状态变化（"Unfreeze"、"Hide details"）。
- 每一行动态都是按钮，名称包含商户、带符号的金额、日期以及待处理或已拒绝的状态；**Up**、**Down**、**Home** 和 **End** 在行之间移动，**Enter** 或 **Space** 引发 `TransactionInvoked`。
- 礼貌的实时区域会读出冻结、解冻、修改失败以及详情的显示和隐藏。

## 动效

- 霜层在 480 ms 内淡入，淡出也一样；完整卡号和 CVC 与遮住的卡号交叉淡化（淡出 160 ms，淡入 240 ms）。
- 限额进度条以 smooth 弹簧移动到新的比例。
- **减弱动效**：霜层和详情在 120 ms 内淡入淡出，进度条立即到位。**关闭**时所有变化立即生效。

## 响应式

- 区块读取自身宽度。低于 680 px 时，卡片放在余额和动态的上方，而不是旁边。
- 卡面保持 1.586 的银行卡比例，最宽 360 px。
- 过长的商户名称以省略号结尾；金额不会换行。

## 相关组件

- Credit top-up 区块：用已保存的卡购买用量额度。
- Spend alert 区块：在卡片消费之上设置预算和提醒。
- `CopyButton`、`Button` 和 `Badge`：区块用到的控件。
