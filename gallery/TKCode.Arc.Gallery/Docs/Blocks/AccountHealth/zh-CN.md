---
title: Account health
summary: 一个客户账户的健康状况：分数及其等级和每周走势、续约日期和剩余天数、未关闭的工单、按指标查看的使用量、风险，以及能预估挽回分数的建议行动。
source-hash: 9825ce6f3fc4
---

<Demo name="Preview" />

## 何时使用

`AccountHealth` 把一个客户账户放在一张卡片上，给负责续约的人看。三个方块回答最先要问的问题：账户有多健康（分数、等级以及几周来的变化）、什么时候续约还剩几天、有多少工单未关闭。下面是可以切换指标的使用量图表、按严重程度排列的风险，以及可以勾选的建议行动，勾选后能看到预计挽回的分数。

- **准备续约。** 通话前先看走势、未关闭的工单和风险。
- **每周的账户回顾。** 每个账户一张卡片，每次布局都一样。
- **制定挽留计划。** 勾选要采取的行动，读出预计分数。

## 何时不用

- **同时看很多账户。** 请用 `Table` 列出账户，再为其中一个打开本区块。
- **原始使用量分析。** 深入分析请使用 `LineChart` 或分析类区块。
- **无法解释的分数。** 本区块只显示分数；请在能展示输入的地方计算它。

## 用法

本区块派生自 `TemplatedControl`。没有健康历史时，它显示一个虚构的账户。提供 `HealthHistory`（每周分数，从早到晚）、`RenewalDate`、`Usage`、`Tickets`、`Risks` 和 `Actions`。

```xml
<arc:AccountHealth AccountName="Copperleaf Hotels" Subtitle="Scale plan" RenewalWarningDays="90" />
```

```csharp
account.HealthHistory.AddRange([58, 61, 63, 66, 70, 74, 79, 81]);
account.Usage.Add(new AccountHealthUsageMetric("Active properties", [12, 14, 18, 21, 26, 28], "properties") { Limit = 40 });
account.ActionToggled += (_, e) => plan.Save(e.Action, e.ProjectedScore);
```

### 分数、续约与行动

`Score` 是历史中的最后一个值；75 分起为健康，50 分起为观察，低于 50 为有风险。`DaysToRenewal` 从 `Today`（或今天）算起；在 `RenewalWarningDays` 之内，这一行变为警告色。勾选一项行动会写入 `IsDone`，更新 `ProjectedScore`（分数加上所有已完成行动的影响，最多 100）并引发 `ActionToggled`。`SelectedMetric`（双向）是使用量图表的指标。文字都在 `Labels`（`AccountHealthLabels`）中。

<Demo name="OwnAccount" />

## API 参考

<!-- api: AccountHealth -->

<!-- api: AccountHealthUsageMetric -->

<!-- api: AccountHealthTicket -->

<!-- api: AccountHealthRisk -->

<!-- api: AccountHealthAction -->

## 无障碍

- 账户名称是二级标题；每个部分有三级标题。
- 每个方块读作一行（"Health score 64, Watch, −18 in 11 weeks"）。
- 使用量图表就是 Arc 的 `LineChart`，带有自己的摘要和键盘读数。
- 行动是复选框；预计分数变化时会以礼貌方式播报。

## 动效

- 健康走势第一次显示时从左向右画出（700 毫秒，仅完整动效）。
- 使用量图表像 `LineChart` 一样在指标之间变形。
- 减少动效和关闭动效时两者都立即显示。

## 响应式

- 在 `CompactWidth`（720 px）或更窄时，三个方块上下排列，列表也上下排列；内边距收紧到 16。
- 只有一个指标时，指标切换会隐藏。
- 过长的工单标题和风险说明会换行。

## 相关组件

- `LineChart`、`Badge` 和 `Checkbox`，组成本区块的控件。
- `Metrics dashboard` 和 `KPI drilldown`，用于整个产品的数字。
- `Call insights` 和 `Lead scoring`。
