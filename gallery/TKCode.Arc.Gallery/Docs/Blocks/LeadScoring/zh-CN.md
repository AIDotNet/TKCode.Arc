---
title: Lead scoring
summary: 按分数排序的潜在客户，带阶段标签、搜索和分数段；选择一位客户即可看到分数背后的匹配、互动和意向因素，并在阶段之间移动一位或多位客户。
source-hash: 03a21f4c8341
---

<Demo name="Preview" />

## 何时使用

`LeadScoring` 是销售团队日常使用的潜在客户列表。每位客户都有一个 0 到 100 的分数，由多个因素组成（匹配、互动或意向信号，各值若干分，有时为负），所以分数从来不是黑盒：选择一行，表格旁的面板就会列出加分和减分的原因。阶段标签、搜索和分数段（热、温、冷）用来缩小表格；面板中的阶段切换移动一位客户，表格上方的操作条移动所有选中的行。

- **每天的筛选。** 按分数排序，打开热门客户，把他们移到 Working。
- **解释一个分数。** 向经理或销售说明一位客户为什么排在这里。
- **整理一个阶段。** 选中 New 中的冷门客户，一次移到 Nurture。

## 何时不用

- **完整的客户记录。** 面板只解释分数；联系记录和备注请使用个人资料页。
- **在列之间拖放的看板。** 阶段是主要视图时，请使用 `Kanban board`。
- **无法解释的分数。** 没有因素时面板无内容可显示；`ScoreOverride` 只作为例外使用。

## 用法

本区块派生自 `TemplatedControl`。没有客户时，它显示一条虚构的销售管道。提供 `Stages` 和 `Leads`；客户的 `Score` 是其 `Factors` 的总和（限制在 0–100），除非设置了 `ScoreOverride`。

```xml
<arc:LeadScoring Title="Event sign-ups" HotThreshold="70" WarmThreshold="40" />
```

```csharp
board.Stages.Add(new LeadScoringStage("met", "Met at booth"));
board.Leads.Add(new LeadScoringLead("a", "Paula Ito", "Fernway Labs", "met")
{
    Factors = [new("Decision maker", 25, LeadScoringFactorKind.Fit), new("Asked for pricing", 30, LeadScoringFactorKind.Intent)],
});
board.LeadsMoved += (_, e) => crm.SaveStage(e.Leads, e.Stage.Key);
```

### 筛选与移动

`StageFilter`、`Band` 和 `SearchText` 都是双向的，所以可以由页面控制筛选。`ActiveLead` 是面板中的客户；选择或聚焦一行会设置它。`MoveLeads` 在代码中移动客户；每次移动都会写入 `StageKey`，引发带有原阶段的 `LeadsMoved`，并进行播报。文字都在 `Labels`（`LeadScoringLabels`）中。

<Demo name="OwnLeads" />

## API 参考

<!-- api: LeadScoring -->

<!-- api: LeadScoringLead -->

<!-- api: LeadScoringFactor -->

<!-- api: LeadScoringStage -->

## 无障碍

- 表格就是 Arc 的 `Table`：标题为它命名，行可以用键盘选择，表头可以排序。
- 阶段标签和分数段是名为 "Stage" 和 "Score" 的分段控件。
- 面板以客户命名；每个因素读出类别、说明和分数，圆环读出总分。
- 移动会被播报（"Moved Paula Ito to Qualified"）。

## 动效

- 选择另一位客户时，分数圆环追向新的值（仅完整动效）。
- 操作条用 160 毫秒淡入淡出；减少动效时立即出现。
- 表格自身的排序和选择动效照常。

## 响应式

- 在 `CompactWidth`（860 px）或更窄时，面板移到表格下方，内边距从 24 收紧到 16。
- 放不下时，筛选控件换到第二行。
- 低于 620 px 时，表格像 Arc 的 `Table` 一样折叠行。

## 相关组件

- `Table`、`SegmentedControl` 和 `Select`，组成本区块的控件。
- `Kanban board`，用于在阶段列之间拖动。
- `Call insights` 和 `Account health`。
