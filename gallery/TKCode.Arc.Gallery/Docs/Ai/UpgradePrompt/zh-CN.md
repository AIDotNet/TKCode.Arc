---
title: Upgrade prompt
summary: 在用户用完额度的那一刻出现的升级提示：用量条、可在月付与年付之间切换并滚动变化的价格、三项具体的解锁内容和成功状态，卡片尺寸始终不变。
source-hash: 4c42633c3bda
---

<Demo name="Preview" />

## 何时使用

`UpgradePrompt` 是 AI 产品在用户用完额度的那一刻显示的卡片：它说明用掉了什么，显示已到上限的用量条，提供下一档方案的价格和解锁内容，
并让用户立即升级或稍后再说。升级后卡片滚入成功状态，列出新的额度，用量条也随之落到新的上限。

- **AI 产品的用量上限**：点数、生成次数或席位用完，某个操作被拦下。把提示放在锚定于被拦下控件的对话框或弹出层中，或直接替换被拦下的结果。
- **付费墙时刻**：只有一档下一级方案，带月付和年付价格，年付节省的金额是卖点的一部分。
- **注重转化但保持克制的提示**：Maybe later 之后显示一条安静的提醒并留有返回入口，而不是再推销一次。

## 何时不用

- **并排比较多个方案**：使用定价表或方案选择器。这个提示只提供一档方案。
- **不改方案、只购买预付点数**：使用点数充值卡片。
- **在账单页被动展示用量**：使用用量条或 `Progress`。这个提示是一次推动，不是报表。
- **页面一加载就显示**：只在某个操作真的因上限被拦下时显示。

## 用法

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:UpgradePrompt Used="50" Limit="50" ResetsOn="Nov 1" />
</UserControl>
```

`UpgradePrompt` 是 Special 包中的 `TemplatedControl`。在 `UseArcControls()` 旁边用 `UseArcSpecial()` 注册这个包。卡片最多 440px
宽，随自身宽度布局，从不随内容变宽。

方案（`Plan`）和用量（`Used`、`Limit`、`Unit`、`ResetsOn`）由你的应用提供。没有设置方案时，卡片为空：标题显示
"No plan to offer"，没有用量条也没有报价，Upgrade 和 Maybe later 都不可用；包里不附带示例方案。预览传入的是展示库自己的方案：
一个虚构产品的 Pro 方案，每月 $20 或按年付费每月 $16，含 2,000 个 AI 点数、Claude Opus 5.5 和优先队列。模型是真实的；方案、
价格与额度属于这个虚构产品，并非模型厂商的价格。没有 `UpgradeHandler` 时，Upgrade 运行一个 1.1 秒、不收取任何费用的模拟，成功状态也会如实说明。

### 结构

1. **标题区**：带图形的着色小方块（报价时是闪电，升级后是对勾，提醒时是时钟）、作为标题的标题文字和两行说明。
2. **用量条**：“AI credits this month”，右侧是计数（“50 / 50”），下面是胶囊形进度条，再下面是状态行：图标加标签（“Limit reached”或“1,950 AI credits left”），右侧是重置日期或方案的额度。
3. **价格**：方案名称在上，下面是滚动的每月价格和“/ month”，右侧是计费切换（Monthly，以及带“Save 20%”的 Yearly）。下方一个位置显示计费说明（“$192 billed yearly”）或错误。
4. **解锁内容**：“Pro unlocks”和三行内容，每行一个放在小方块里的标记和一行文字。
5. **底部**：Maybe later 保持自然宽度，Upgrade 填满其余空间。成功状态是 Continue，提醒状态是 See Pro again。

### 状态

| 状态 | 显示 | 按钮 |
|---|---|---|
| `Offer` | 上限、带切换的价格和解锁内容 | Maybe later、Upgrade to Pro |
| `Processing` | 同上，切换不可用 | Maybe later（不可用）、带加载圈的 Upgrading… |
| `Error` | 价格下方的错误信息 | Maybe later、Try again |
| `Success` | “You're on Pro”、落到新上限的用量条和新的额度 | Continue |
| `Reminder` | “No problem”、额度何时重置以及一条提示 | See Pro again |

所有状态共用一个单元格：正文面板和底部都叠放在一起并交叉淡化，因此卡片保持最高那个状态的尺寸，按钮也待在原位。`State` 只读且可绑定；卡片处于
`Error` 时，`ErrorMessage` 保存失败的信息。

### 你的方案和单位

`Plan` 接受一个 `UpgradePromptPlan`：名称、月付价格、按年付费时的每月价格、货币、新的上限、解锁内容（`UpgradePromptFeature`：id、标签，以及可选的标记和颜色）
和新的额度（`UpgradePromptLimit`：标签、值和可选的标记）。Yearly 上的节省徽标和年付金额都由这两个价格算出。`Unit` 说明计数的是什么，`Title` 和
`Description` 替换报价状态的文字。

<Demo name="YourPlan" />

```xml
<arc:UpgradePrompt Used="3" Limit="3" Unit="seats" Interval="Monthly" Title="You've used all 3 seats">
    <arc:UpgradePrompt.Plan>
        <arc:UpgradePromptPlan Name="Team" MonthlyPrice="48" YearlyPrice="40" Currency="USD" Limit="25" />
    </arc:UpgradePrompt.Plan>
</arc:UpgradePrompt>
```

标记是 24 × 24 的描边图形，例如 `ArcIcons` 的成员。请使用中性图形或你自己产品的标记；预览在模型名称旁使用闪光图形，而不是任何厂商的标志。

### 执行升级

`UpgradeHandler` 收到一个 `UpgradePromptSelection`（方案、计费周期、今天收取的金额和货币）以及一个取消令牌。方案生效后完成任务；抛出异常则在价格下方
显示异常信息并提供 Try again。提示离开可视树时令牌会被取消，提示随后回到报价状态。

<Demo name="Checkout" />

```csharp
prompt.UpgradeHandler = async (selection, cancellationToken) =>
{
    var response = await http.PostAsJsonAsync("/api/billing/upgrade",
        new { plan = selection.Plan.Name, interval = selection.Interval.ToString() }, cancellationToken);
    if (!response.IsSuccessStatusCode)
    {
        throw new InvalidOperationException("Your card was declined. Try another card.");
    }
};
prompt.Upgraded += (_, e) => workspace.Refresh();
prompt.Dismissed += (_, _) => nudges.Snooze("upgrade");
prompt.ContinueRequested += (_, e) =>
{
    e.Handled = true;
    dialog.Close();
    RetryBlockedAction();
};
```

升级成功、成功状态显示之后引发 `Upgraded`。`UpgradeFailed` 带有异常。`Dismissed` 在 Maybe later 之后引发，请在这里记录暂缓。`ContinueRequested` 在
Continue 之后引发：关闭提示时把 `Handled` 设为 true 可保留成功状态；未处理时，提示重置回报价状态。`Interval` 是双向的，`IntervalChanged` 跟随切换。
`UpgradeAsync()`、`Dismiss()`、`ShowOffer()` 和 `Reset()` 可以从代码执行同样的步骤。

选择中的金额只用于确认文案。请在服务器上按方案和计费周期收费。

### 窄容器

卡片在 440px 以内随容器伸缩。卡片宽度小于 420px 时，内边距从 24 降到 20。价格和切换无法并排放下时，切换移到价格下方。解锁内容始终保持一行，
超出部分以省略号结尾；按钮在任何宽度下都并排显示。

<Demo name="Narrow" />

### 减弱动效

减弱动效时，进度条和价格直接跳到目标值，交叉淡化变为普通淡入淡出。

<Demo name="ReducedMotion" />

### 内容准则

三项解锁内容要具体，并与用户刚才在做的事有关：更多他们刚用完的东西、更好的模型、无需等待。

| 不要写 | 改写为 |
|---|---|
| Unlock premium features | 2,000 AI credits every month |
| Access to advanced AI | Claude Opus 5.5 for your hardest tasks |
| Enhanced performance | Priority queue, no waiting at peak times |

`Labels` 接受一个 `UpgradePromptLabels` 对象，包含卡片显示或播报的每个词。带值的句子是函数，因此其他语言可以调整语序：

```csharp
prompt.Labels = new UpgradePromptLabels
{
    MaybeLater = "Not now",
    TitleAtLimit = (limit, unit) => $"All {limit} {unit} used"
};
```

### 最佳实践

**建议**

- 在某个操作因上限被拦下时显示提示，并锚定在被拦下的控件上。
- 在 `Dismissed` 中暂缓这条提示，避免每次被拦下都再次出现。
- 处理 `ContinueRequested` 来关闭提示并重试被拦下的操作，让升级感觉像是替用户解除了阻碍。

**避免**

- 把演示中的价格当作真实价格展示。预览中的方案价格是为一个虚构产品编造的。
- 按选择中的金额收费。请在服务器上按方案和计费周期收费。
- 列出超过三项解锁内容；卡片只为三项预留了空间。

### 常见问题

**为什么 Maybe later 之后卡片没有变小？** 所有状态共用一个单元格，因此卡片保持最高那个状态的尺寸，周围的内容都不会移动。

**用量条能显示未到上限的用量吗？** 能。未到上限时，标题说明还剩多少，状态显示带仪表图标的“5 AI credits left”。

**升级进行中关闭提示会怎样？** 处理程序的令牌被取消，提示回到报价状态。

## API 参考

<!-- api: UpgradePrompt -->

<!-- api: UpgradePromptPlan -->

<!-- api: UpgradePromptFeature -->

<!-- api: UpgradePromptLimit -->

<!-- api: UpgradePromptSelection -->

<!-- api: UpgradePromptLabels -->

<!-- api: UpgradePromptIntervalChangedEventArgs -->

<!-- api: UpgradePromptUpgradedEventArgs -->

<!-- api: UpgradePromptFailedEventArgs -->

枚举：

| 类型 | 值 |
|---|---|
| `UpgradePromptInterval` | `Monthly`、`Yearly` |
| `UpgradePromptState` | `Offer`、`Processing`、`Error`、`Success`、`Reminder` |

## 无障碍

- 卡片是名为“Upgrade to Pro”的分组，标题是一个标题元素，因此放在对话框或弹出层中也易于朗读。
- 用量条是一个进度条，名称就是它的值（“50 of 50 AI credits used”），状态作为帮助文本。
- 状态始终是图标加标签（“Limit reached”、“1,950 AI credits left”），从不只靠颜色。
- 一个礼貌的实时区域播报升级中、成功、错误和提醒。没有 `UpgradeHandler` 时，它会说明升级是模拟的，不会收取任何费用。
- 滚动的价格对辅助技术隐藏；价格组以纯文本命名（“$16 a month, $192 billed yearly”）。
- 未激活的面板是惰性且隐藏的：不可命中、Tab 会跳过、也不会公开，因此焦点永远不会落在淡出的面板上。状态变化隐藏了获得焦点的按钮时，焦点移到新状态的主按钮。
- 升级进行中 Upgrade 仍可获得焦点，因此键盘焦点不会丢失。

### 键盘

| 按键 | 操作 |
|---|---|
| `Tab` / `Shift+Tab` | 在计费切换、Maybe later 和 Upgrade 之间移动；其他状态下是 Continue 或 See Pro again |
| `Left` / `Right` | 在计费切换上切换 Monthly 和 Yearly |
| `Enter` / `Space` | 激活获得焦点的按钮 |

## 动效

- 卡片第一次滚入视口时用量条填充一次，升级后以 0.5 秒无回弹的弹簧落到新的上限。填充是一个通过平移移动并裁剪在轨道内的胶囊，所以它的圆角端从不被拉伸。
- 价格在月付和年付之间以 0.28 秒无回弹的弹簧逐位滚动，使用等宽数字，旁边的内容不会移动。
- 标题、说明、用量条标签、计数、状态和计费说明交叉淡化：100ms 淡出，160ms 淡入并上升 2px。
- 正文面板和底部以同样方式淡化并上升 4px；旧面板淡出后新面板才开始出现。
- 计费切换的滑块以自身的弹簧滑动，选中 Yearly 后“Save 20%”徽标换上成功色调。

| 变化 | 完整 | 减弱 | 关闭 |
|---|---|---|---|
| 进度条 | 0.5 秒弹簧 | 直接跳转 | 直接跳转 |
| 价格 | 以 0.28 秒弹簧滚动 | 直接跳转 | 直接跳转 |
| 文字 | 交叉淡化并上升 2px | 淡入淡出 | 直接替换 |
| 面板与底部 | 淡化并上升 4px | 淡入淡出 | 直接替换 |

## 相关组件

- `BillingToggle` 和 `BillingPrice`：定价页上的计费周期切换。
- `Progress`：带标签的单一额度。
- `ContextWindow`：模型上下文窗口里装了什么。

| 组件 | 何时选择 |
|---|---|
| `BillingToggle` | 你在做包含多个方案的定价页 |
| `Progress` | 你只需要报告用量 |
| `ContextWindow` | 上限是模型的上下文，而不是方案 |
