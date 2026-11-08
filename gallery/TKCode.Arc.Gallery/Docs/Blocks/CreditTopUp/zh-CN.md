---
title: Credit top-up
summary: 一张用于购买用量额度的卡片：余额与两周用量图表及剩余天数，带赠送额度标签的预设套餐，附带实时价格的自定义金额滑块，设有阈值的自动充值，已保存的卡，以及像里程表一样滚动更新的新余额。
source-hash: b89d9ae3ae0d
---

<Demo name="Preview" />

## 何时使用

`CreditTopUp` 在一张卡片中出售预付额度（AI token、API 调用、渲染分钟数）。它回答人们打开计费页面时的三个问题：还剩多少、能用多久、充值要花多少钱。余额旁边是两周的用量和剩余天数的估算；三个套餐和一个滑块用来选择金额；自动充值和已保存的卡就位于结算区域上方，因此一次点击即可完成购买，新余额会像里程表一样滚动更新。

- **计费页面的额度区域**，用户还可以在此管理自动充值和银行卡。
- **“额度耗尽”对话框或抽屉。** 工作已经停止时，消耗速率与一键充值应放在一起。
- **带批量赠送的预付方案。** 每个价格、标签和总额都来自同一个 `CreditTopUpPricing`，因此你销售的赠送档位就是卡片所显示的内容。

## 何时不用

- **订阅变更**，涉及席位、方案和计费周期：请使用方案对比或结算区块。
- **收集新的卡号。** 卡片只显示已保存的卡；请通过你的支付服务商自己的表单录入新卡，然后将已保存的卡传入。
- **只读的用量报告。** 请使用 `Usage billing` 或图表；此卡片以购买为核心。

## 用法

该区块派生自 Avalonia 的 `TemplatedControl`。不设置任何属性时，它会显示一个示例工作区，余额为 1,260 额度，并模拟 1.2 秒的扣款，因此你可以在接入任何内容之前先试用。在应用中，请双向绑定余额、每日用量和已保存的卡，并为它提供一个通过你的后端扣款的 `PurchaseHandler`：

```xml
<arc:CreditTopUp Balance="{Binding Credits}"
                 Usage="{Binding DailyUsage}"
                 PaymentMethods="{Binding Cards}"
                 IsAutoTopUpEnabled="{Binding AutoTopUp}"
                 AutoTopUpThreshold="{Binding AutoTopUpThreshold}" />
```

```csharp
public interface IBilling
{
    Task BuyCreditsAsync(CreditTopUpOrder order, CancellationToken cancellation); // throws with a short message on failure
    void SaveAutoTopUp(bool enabled, long threshold, long credits);
    void SetDefaultCard(string cardId);
}

// order: Credits, Bonus, Total, Price, Currency, PaymentMethodId.
card.PurchaseHandler = (order, cancellation) => billing.BuyCreditsAsync(order, cancellation);
card.AutoTopUpChanged += (_, e) => billing.SaveAutoTopUp(e.Settings.IsEnabled, e.Settings.Threshold, e.Settings.Credits);
card.PaymentMethodChanged += (_, e) => billing.SetDefaultCard(e.PaymentMethod.Id);
```

服务器端也使用相同的计算方式下单：`CreditTopUp.Quote(credits, pricing)` 会返回额度、赠送额度、总额和价格，与卡片显示的完全一致。

### 结构

- **余额**：一个小标题，带健康状态标签（琥珀色警告图标的 "Low balance"，或绿色对勾的 "On track"），余额以大号等宽数字显示，后接 "credits"，以及按当前日均用量计算的剩余天数。
- **用量图表**：每天一根柱子，最后一根（今天）使用强调色。指向某一天会将其柱子变为文字颜色、淡化其他柱子，并在图表下方读出该日数据（"Nov 14: 529"、"Today: 362"）。
- **充值**：标题及费率（"$8 per 1,000"）、三个套餐（金额、价格，适用时显示绿色的赠送标签），以及带滚动读数的自定义金额滑块。
- **自动充值**：一个开关，以及胶囊式的阈值选择。自动充值关闭时，该行保持原位，变暗且无法操作。
- **已保存的卡**：一个小的卡组织标识、品牌及末四位数字、有效期，以及一个 "Change" 按钮，用于依次切换已保存的卡。
- **结算**：总额度（包括赠送额度），其下方是赠送额度行，右侧是价格，以及购买按钮和一行状态文字。

### 金额、套餐与赠送额度

只有一个金额，即 `SelectedCredits`（双向）。套餐相当于某个数值的快捷方式：按下套餐会把滑块移到该处，将滑块拖到某个套餐的数值上则会选中该套餐。选择环在套餐之间滑动；当金额从其他地方落到某个套餐上，或离开所有套餐时，选择环会原地淡入淡出。金额保持在 `Range` 之内，并按其步长取值（默认从 500 到 30,000，步长为 500）；PageUp 和 PageDown 每次移动范围的十分之一（3,000）。

赠送额度来自不超过该金额的最高档位（`CreditTopUpBonusTier`，例如从 6,000 起为 +10%）。低于所有档位时，总额下方的文字会说明赠送从哪里开始。每 1,000 的费率是单纯的单价：赠送额度只会增加额度，不会改变价格。

### 余额、剩余天数与状态

剩余天数行用余额除以 `Usage` 中**完整**天数的平均值，并向下取整：今天仍在进行，因此不计入平均值，"4.8 days" 显示为 4。只要余额低于 `AutoTopUpThreshold`，`Health` 就会显示为余额不足，即使自动充值已关闭也是如此；选择另一个阈值会立即重新判断。将 `Usage` 留空即可隐藏图表和剩余天数行。

### 购买

按下按钮时，订单（金额、赠送额度、总额、价格和卡）即被固定。扣款进行期间，按钮只显示加载指示，忽略后续的按压但保留键盘焦点，状态行会说明正在扣款的卡。套餐、滑块和设置仍可使用；更改将应用于下一次购买。处理程序完成后，余额会按总额向上滚动，剩余天数和状态标签随之更新，按钮显示带对勾的 "Added"，状态行确认额度已到账。2.4 秒后两者都恢复到静止状态。`Purchased` 报告订单和新余额；`PurchaseAsync()` 执行与按钮相同的操作。

没有 `PurchaseHandler` 时，卡片会模拟扣款，状态行会说明并未实际扣款。将卡片从窗口中移除，会通过处理程序的令牌取消正在进行的扣款。

### 失败与重试

处理程序抛出异常时，状态行以危险色显示异常消息并附带警告图标，按钮显示 "Retry"，`PurchaseFailed` 报告订单和消息。卡片保持其尺寸。按下 Retry 会按当前选择再次扣款；若先更改金额或卡，按钮会先恢复到静止状态。

<Demo name="Declined" />

### 余额自动变化

余额由你来修改：用量消耗余额时，设置 `Balance`，数字就会向下滚动到新值；购买时它仍会向上滚动。滚动途中到来的更改会从每个数字当前所绘制的位置重新规划，因此快速更新不会让某个数字只显示一半。

<Demo name="Draining" />

### 窄容器

卡片最宽 480，并随自身宽度伸缩，读取的是它自己的宽度。宽度为 448 或更小时，图表移到余额下方并横向拉伸，其标签位于右侧；宽度为 416 或更小时，所有左右内边距由 24 收紧为 16。三个套餐始终保持在一行。

<Demo name="Narrow" />

### 货币、文化与文案

价格使用 `Pricing` 的 ISO 货币代码，并按 `CultureName` 书写金额的方式排版（de-DE 下为 "48,00 €"），所有计数都使用该文化的分隔符。所有可见文字和无障碍名称都位于 `CreditTopUpLabels` 中；句子以 `{0}`、`{1}` 接收格式化后的数字；当数字发生滚动时（总额、赠送额度行），`{0}` 周围的文字保持不动。

<Demo name="Euro" />

## API 参考

<!-- api: CreditTopUp -->

<!-- api: CreditTopUpPricing -->

<!-- api: CreditTopUpRange -->

<!-- api: CreditTopUpPaymentMethod -->

<!-- api: CreditTopUpLabels -->

<!-- api: CreditTopUpPhase -->

<!-- api: CreditTopUpHealth -->

<!-- api: CreditTopUpCardBrand -->

## 无障碍

- **Tab** 依次移动到选中的套餐（未选中时为第一个）、滑块、自动充值开关、选中的阈值（仅在自动充值开启时）、Change 按钮和购买按钮。图表不是 Tab 停靠点。
- **方向键**在套餐和阈值之间移动并选择，到达两端时循环；**Home** 和 **End** 分别跳到第一个和最后一个。
- 在滑块上，**Left** 和 **Right** 每次移动 500，**PageUp** 和 **PageDown** 每次移动 3,000，**Home** 和 **End** 移到两端。
- **空格键**切换开关；**Enter** 或**空格键**会按下 Change 按钮和购买按钮。
- 卡片是一个名为 "Buy credits"（`CardName`）的区域。
- 套餐和阈值会被读作单选按钮："6,000 credits, $48, 10% bonus"。滑块以其标题命名，并将其值读作 "6,000 credits"。开关以自动充值的标题命名。
- 图表是一张图片，附带语音摘要（"Daily usage over the past 14 days, averaging 440 credits a day."）；其下方的读数供指针用户使用，不会被朗读。
- 滚动中的数字以其最终值朗读，绝不逐位朗读。
- 状态行是礼貌的实时区域：扣款中、结果、错误和设置提示都会被播报。
- 状态通过图标和文字表示，而不只是颜色。
- 键盘焦点在获得焦点的元素外侧显示 2px 的环；指针焦点则不显示。

## 动效

- **套餐、阈值和滑块拇指**在快速、无回弹的弹簧上滑动（约 0.25 秒）。选择出现或消失时，套餐环会原地淡入淡出（170 毫秒）。
- **结算数字**在 240 毫秒内以 Arc 的标准曲线（.22, 1, .36, 1）逐位滚动：数值增大时向上，减小时向下。
- **余额**在 900 毫秒内以相同曲线像里程表一样滚动：发生变化的最高位走最短路径，其下方的每一位多走一圈，因此读起来像计数器。新增的最高位不滚动：它们以快速弹簧展开宽度，并在 100 毫秒内淡入。
- **文字变化**（状态标签、剩余天数、图表读数、卡片、状态行、按钮标签）采用交叉淡化：旧文字在 100 毫秒内淡出，新文字在 160 毫秒内淡入并上升 2 px。
- **指向某一天**时，其他柱子在 160 毫秒内淡化至 55%。按下套餐会在 120 毫秒内将其缩放至 0.98。
- 减少动效会让数字、选择环、胶囊和拇指立即就位，并保留短暂的淡入淡出。关闭动效则会立即应用每一次变化。

## 响应式行为

- 卡片最宽 480，低于此宽度时随其容器变化；断点使用卡片自身的宽度，而不是窗口的宽度。
- 宽度为 448 或更小（`:narrow`）时，图表移到余额下方并拉伸，其柱子高度降为 32。
- 宽度为 416 或更小（`:compact`）时，左右内边距由 24 收紧为 16。
- 状态行以省略号缩短而不是换行，并且卡片在每个购买状态下都保持相同高度。

## 相关组件

- `Usage billing` 和 `Billing overview`，即其他计费区块。
- `Slider`、`Switch` 和 `Button`，即组成该卡片的控件。
- 同一分组中的 `Plan comparison` 和 `Pricing calculator`。
