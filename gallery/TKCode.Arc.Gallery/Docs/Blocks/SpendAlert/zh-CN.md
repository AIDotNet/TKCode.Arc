---
title: Spend alert
summary: 带消费提醒的预算：已花费金额与预算和本期节奏的对比、为每个提醒画出刻度的进度条、预算与提醒的编辑器，以及消费越过提醒时出现的横幅。
source-hash: 7575cac3499d
---

<Demo name="Preview" />

## 何时使用

`SpendAlert` 帮人守住一项预算。它显示本期已花费的金额与预算的对比、还剩多少、还有几天，以及按目前的速度本期结束时会花到多少。进度条为人们设置的每个提醒画出标记，例如预算的 50%、80% 和 100%。下方可以编辑预算和提醒：打开或关闭提醒、修改、删除或新增。当消费上升并越过一个已打开的提醒时，横幅会说明发生了什么，并通过事件告诉应用，以便发送相应的通知。

- **个人理财**：每个分类一项月度预算，并带通知。
- **团队或云服务的花费**：越过阈值时通知负责人。
- **任何有上限的用量**：由用户自己选择提醒点。

## 何时不用

- **同时查看很多项预算**：使用 Budget variance 区块，它把各部门放在同一刻度上比较。
- **带预测曲线的用量变化**：使用 Usage forecast 区块。
- **一次性的警告**：使用 `Alert` 或 `Toast`。

## 用法

该区块派生自 Avalonia 的 `TemplatedControl`。设置 `Title`、`Period`、`Budget`、`Spent`，并用 `DaysElapsed` 与 `DaysInPeriod` 计算节奏。提醒是 `SpendThreshold`，包含占预算的比例 `Share`（0.8 表示 80%）和 `IsEnabled`。

```xml
<arc:SpendAlert Title="Dining out" Period="November" Budget="600" Spent="412.80" DaysElapsed="18" DaysInPeriod="30">
    <arc:SpendThreshold Share="0.5" />
    <arc:SpendThreshold Share="0.8" />
    <arc:SpendThreshold Share="1" />
</arc:SpendAlert>
```

### 越过提醒

只有 `Spent` 上升才会触发提醒：当新的金额达到一个已打开、而旧金额还没达到的提醒时，其中最高的那个会显示在横幅中、被读出，并由 `ThresholdCrossed` 报告。`AddSpending` 用于加上一笔消费。横幅会一直显示，直到按下 Dismiss（或调用 `DismissAlert`），或者消费、预算或提醒的变化让它不再成立。编辑提醒本身不会触发提醒。

### 状态

`Status` 的值为 OnTrack、AtRisk（还没有达到提醒，但按目前节奏会超出预算）、Warning（已达到某个提醒）或 Over（消费已超过预算），标题旁的标签随之变化。

### 编辑器

预算字段写入 `Budget`。每一行提醒有开关、百分比和删除按钮；Add alert 在最高的提醒之上加十个百分点新增一个，最多 `MaxThresholds`（5）个。编辑会写回 `SpendThreshold` 对象并引发 `ThresholdsEdited`。`IsEditable="False"` 会隐藏编辑器，作为摘要卡片使用。

<Demo name="ReadOnly" />

## API 参考

<!-- api: SpendAlert -->

<!-- api: SpendThreshold -->

## 无障碍

- 进度条的名称包含已花费金额、预算和比例。
- 每个提醒的开关名为 "Alert at 80%"，字段名为 "Alert share of budget, percent"，删除按钮名为 "Remove the 80% alert"。删除后焦点移到下一行的删除按钮，或移到 Add alert。
- 越过提醒时，强制的实时区域会读出横幅内容。

## 动效

- 已花费的填充、节奏段和刻度以 smooth 弹簧移动到新值。
- 横幅上升 6 px 并在 240 ms 内淡入；从警告变为危险时颜色交叉淡化。
- **减弱动效**：横幅只淡入不上升，进度条立即到位。**关闭**时所有变化立即生效。

## 响应式

- 低于 520 px 时，预算字段移到提醒标题下方，节奏说明移到剩余金额下方。
- 进度条占满区块宽度；刻度上方的标签始终留在区块内。

## 相关组件

- Budget variance 区块：比较各部门的预算。
- Usage forecast 区块：账单周期内的预算线。
- Wallet card 区块：消费所用的卡片。
