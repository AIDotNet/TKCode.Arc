---
title: Trial banner
summary: 试用状态横幅：圆环倒数剩余天数，文案说明工作区将失去什么，升级只需点击一次；可作为细横幅、侧边栏卡片或标题栏胶囊。
source-hash: 42c8d885a742
---

<Demo name="Preview" />

## 何时使用

`TrialBanner` 告诉处于免费试用期的工作区还剩多久、试用结束时会失去什么，以及如何保留。圆环倒数天数，文案说明会失去的内容（"SAML SSO and 12 seats"），"添加付款方式"只需点击一次。在最后几天它切换为警告色调并说出结束的那一天；试用结束后显示锁形图标和工作区失去的内容；付款方式登记后显示对勾。

- **提供免费试用的 SaaS 应用**，需要一条安静且始终可见的升级途径。
- **告诉工作区试用结束时会失去什么**，并在最后几天更积极地提醒。
- **显示已结束的试用**，而不阻挡应用。

## 何时不用

- **阻挡某项功能的硬性付费墙**：使用对话框或升级页面。
- **一般公告或发布说明**：使用普通横幅或 Toast。
- **用量限制**（额度、席位）：使用用量仪表。

## 用法

传入来自计费服务的剩余天数或结束日期、方案、工作区、它将失去的内容，以及你的结账流程。`DaysLeft` 和 `EndsAt` 都没有时，没有可显示的试用：横幅在空的圆环上方显示 "No trial in progress"，没有操作按钮（即预览中的 "No trial" 选项）。`Plan`、`Workspace`、`LossSummary` 和 `Features` 默认为空；包里不附带示例试用。预览传入的是展示库虚构的试用：Northwind 工作区的 Pro 试用，到期后将失去 SAML SSO 和 12 个席位。

```csharp
banner.DaysLeft = trial.DaysLeft;            // or: banner.EndsAt = trial.EndsAt;
banner.EndsAt = trial.EndsAt;                // for the date in the copy
banner.AddPaymentHandler = async cancellation =>
{
    // Open checkout or the billing portal; return once a payment method is on file, throw if it failed.
    await billing.AddPaymentMethodAsync(cancellation);
};
banner.ComparePlansRequested += (_, e) => { e.Handled = true; OpenPlansPage(); };
banner.Snoozed += (_, e) => settings.TrialBannerSnoozedUntil = e.Until;
banner.Dismissed += (_, _) => settings.TrialBannerDismissed = true;
```

### 天数与日期

设置了 `DaysLeft` 时以它为准：横幅不会为天数读取时钟，因此显示的正是计费服务给出的值（0 或更小即为已结束状态）。将它保留为 null 并设置 `EndsAt`，横幅会从今天起按本地日历日计数，并在本地午夜重新计数。`TimeProvider` 决定"今天"从何而来（默认为系统时钟）；测试中可传入自己的实现。`EndsAt` 也提供文案中的日期；没有它时，日期为今天加上剩余天数。

`WarnAt`（3）表示最后几天使用警告色调。`TrialDays`（14）是试用期的长度：圆环显示其中剩余的天数。

### 变体

<Demo name="Card" />

`Variant` 选择形态。`Banner` 是放在应用顶部的细横幅。`Card` 是侧边栏卡片，最宽 340px，列出工作区会失去的内容：在 `Features` 中传入三到四个 `TrialFeature`（名称、可选的次要说明和 14px 图标）。`Pill` 是紧凑的标题栏胶囊，在弹出层中打开卡片。

<Demo name="Pill" />

### 结账

<Demo name="Checkout" />

`AddPaymentHandler` 打开你的结账流程。运行期间按钮显示加载指示，文案为"Opening checkout…"；返回后横幅显示已升级状态并设置 `IsUpgraded`；抛出异常时，文案说明结账未能打开，按钮变为 Try again。如果横幅离开窗口，取消令牌会被触发。没有处理程序时，该调用为模拟（900ms）。

"比较方案"引发 `ComparePlansRequested`；在已升级状态下，"管理账单"引发 `ManageBillingRequested`。请设置 `Handled`；未处理的请求会在文案位置显示一条简短的模拟提示。

### 关闭与稍后提醒

关闭会引发 `Dismissed`；稍后提醒菜单提供"直到明天"、"三天"或"直到警告开始"，并引发 `Snoozed`，附带所选选项和 `Until`（那一天本地时间的开始）。请自行保存这两者，并在稍后提醒结束前不再显示横幅。在此期间，横幅折叠为试用胶囊（`IsCollapsed` 变为 true）；点击胶囊会再次展开。`CollapseTo="None"` 则什么也不显示。`IsDismissible` 和 `IsSnoozable` 用于隐藏对应按钮。

### 窄容器

<Demo name="Narrow" />

横幅读取自身宽度：小于 600px 时，操作按钮移到文案下方，与文案左边缘对齐，第二行文案可以占两行（两行空间都会预留，因此不会跳动）。在 360px 宽度下也不会出现水平滚动。

### 自定义文字

`Labels` 包含横幅显示或朗读的所有文字。带有数值的行是格式化函数：

```csharp
banner.Labels = new TrialBannerLabels
{
    Days = days => days == 1 ? "1 Tag" : $"{days} Tage",
    ActiveTitle = (days, plan) => $"Noch {days} in Ihrer {plan}-Testphase",
    AddPayment = "Zahlungsmethode hinzufügen",
};
```

### 内容准则

- `LossSummary` 保持在几个词以内（"SAML SSO and 12 seats"），让横幅保持一行。
- `Features` 保持三到四行：这些是付费的理由，而不是功能对照表。
- 写明工作区正在试用的方案（`Plan`）和工作区名称（`Workspace`）；两者都会出现在文案中。

### 最佳实践

- 横幅放在应用内容上方，卡片放在侧边栏底部，胶囊放在紧凑的标题栏中。
- 从计费服务传入天数或结束日期（例如订阅的试用结束时间）。
- 自行保存关闭与稍后提醒，并在稍后提醒结束前不再显示横幅。

## API 参考

<!-- api: TrialBanner -->

<!-- api: TrialFeature -->

<!-- api: TrialSnoozedEventArgs -->

<!-- api: TrialBannerLabels -->

## 无障碍

- 横幅和卡片是带名称的区域（"Pro trial"）；圆环仅作装饰，天数始终写在文字中。
- 礼貌模式的实时区域会播报计费、稍后提醒和关闭的结果，并说明计费是否为模拟。
- 状态从不只依赖颜色：警告色调同时会在文案中说出结束的那一天，已结束的试用显示锁形图标，已升级的显示对勾。
- 关闭或稍后提醒后，焦点移到试用胶囊；再次展开横幅时，焦点移到主要操作。

### 键盘

| 按键 | 操作 |
|---|---|
| Tab | 依次经过比较方案、添加付款方式、稍后提醒和关闭 |
| Enter、Space | 执行获得焦点的操作、打开稍后提醒菜单或打开胶囊的卡片 |
| 上方向键、下方向键 | 在稍后提醒选项之间移动 |
| Escape | 关闭稍后提醒菜单或胶囊的弹出层 |

## 动效

| 变化 | 完整 | 减弱 | 关闭 |
|---|---|---|---|
| 天数变化 | 圆环的弧以无回弹的弹簧（0.24s）变到新长度 | 立即 | 立即 |
| 色调变化 | 圆环的颜色在 160ms 内跟随 | 立即 | 立即 |
| 文案、天数与图标变化 | 在同一格中交叉淡化一次：旧内容 100ms 淡出，新内容 160ms 淡入并上升 2px | 立即 | 立即 |
| 按钮文字变化 | 宽度无回弹地变形 | 立即 | 立即 |
| 胶囊文字变化 | 宽度以弹簧变到测得的文字宽度 | 立即 | 立即 |
| 关闭、稍后提醒、重新展开 | 表面在横幅与胶囊之间以弹簧过渡（0.33s，无回弹）；两种形态交叉淡化 | 立即 | 立即 |

横幅空闲时不运行任何计时器或循环：只有模拟的计费调用和简短的模拟提示使用超时。

## 相关组件

- `ContextWindow`：显示预算已用了多少。
- `AgentStatus`：实时状态胶囊。
