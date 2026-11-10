---
title: Pivot builder
summary: 把字段拖到行、列和值中来搭建透视表，也可以用每个字段的键盘菜单；为每个值选择汇总方式，表格立即重新分组，并带有行、列和总计。
source-hash: 803309efc7fa
---

<Demo name="Preview" />

## 何时使用

`PivotBuilder` 让人不写查询就能汇总一组记录。记录的字段放在一个列表里；把一个字段拖到 Rows，它的每个值成为一行；拖到 Columns，每个值成为一列；拖到 Values，每个单元格显示一个汇总值（求和、计数、平均、最小或最大）。区域下方的表格立即重新分组，并为每一行、每一列以及全部数据加上合计。

- **临时报表。** 按地区和季度的收入，按团队和月份的工单。
- **探索导出的数据。** 在搭建仪表盘之前先试几种分组。
- **教人使用透视表。** 每次改动都立即看到结果。

## 何时不用

- **固定的报表。** 分组从不改变时，直接显示 `Table` 或图表。
- **上百万条记录。** 本区块在界面线程的内存中分组；请先在服务器上汇总。
- **编辑记录。** 表格只显示汇总值；编辑单元格请使用 `DataGrid`。

## 用法

本区块派生自 `TemplatedControl`。没有记录时，它对虚构的销售数据做透视。提供 `ItemsSource` 和 `Fields`；每个 `PivotBuilderField` 从一条记录中读出一个值（`PivotBuilderField.Create<T>` 有类型，可用于 AOT）。

```xml
<arc:PivotBuilder ShowTotals="False" IsFramed="False" />
```

```csharp
pivot.Fields.Add(PivotBuilderField.Create<Ticket>("team", "Team", t => t.Team));
pivot.Fields.Add(PivotBuilderField.Create<Ticket>("hours", "Hours", t => t.Hours, isNumeric: true, format: "N1"));
pivot.RowFields.Add("team");
pivot.ValueFields.Add(new PivotBuilderValue("hours", PivotBuilderAggregation.Average));
pivot.ItemsSource = tickets;
```

### 布局

`RowFields`、`ColumnFields` 和 `ValueFields` 保存布局，并随拖动而改变；在代码中修改它们可以设置初始布局。`MoveField` 和 `SetAggregation` 做字段按钮所做的事，并引发 `LayoutChanged`。放在 Values 中的非数字字段按计数汇总。`Result` 保存计算好的透视结果，供你自己导出。文字都在 `Labels`（`PivotBuilderLabels`）中。

<Demo name="OwnRecords" />

## API 参考

<!-- api: PivotBuilder -->

<!-- api: PivotBuilderField -->

<!-- api: PivotBuilderValue -->

<!-- api: PivotBuilderAggregation -->

<!-- api: PivotBuilderZone -->

## 无障碍

- 每个字段都是一个按钮，以字段和所在区域命名。**Enter** 或 **Space** 打开它的菜单：移到另一个区域、左移或右移、选择汇总方式或移除。**Delete** 把它从布局中移除。
- 各区域分别名为 Fields、Rows、Columns 和 Values；表格是带标题的 Arc `Table`。
- 每次移动都会被播报（"Region moved to Columns"）。

## 动效

- 被拖动的字段通过变换跟随指针，并带上抬起的阴影；它下方的区域用 160 毫秒着色。
- 减少动效时仍可拖动（这是直接操作），着色不再过渡。

## 响应式

- 在 `CompactWidth`（760 px）或更窄时，字段列表移到区域上方；内边距收紧到 16。
- 区域中的字段会换行；过宽的表格在 Arc `Table` 内滚动。

## 相关组件

- `Table`，本区块填充的表格。
- `DataGrid`，用于可编辑的表格。
- `Metric explorer` 和 `Revenue explorer`。
