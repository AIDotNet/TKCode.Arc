---
title: 可排序数据表格
summary: 通过可排序的列比较结构化记录。
source-hash: 2f0ca35e75db
---

## 何时使用

- 字段相同的记录，例如项目、发票或用户，人们需要按列进行比较。
- 需要复选框列进行行选择、Shift 范围选择以及计数行的表格。
- 中短列表。较长的列表请搭配 `Pagination`。

## 何时不用

- 顺序由用户手动决定：用 `ReorderableList`。
- 按时间先后排列的事件，例如活动日志：用 `Timeline`。
- 按字段和搜索筛选行：在表格上方放置 `FilterToolbar`。
- 像电子表格那样原地编辑单元格：本组件为只读。

## 用法

列在 XAML 中声明。每一列通过 `Binding` 读取其单元格的值。

```xml
<arc:SortableDataTable Caption="项目"
                       ItemsSource="{Binding Projects}"
                       ItemNameOne="项目"
                       ItemNameOther="项目"
                       SortKey="Budget"
                       SortDirection="Descending">
    <arc:SortableDataTable.Columns>
        <arc:DataTableColumn Header="项目" Key="Name" Binding="{Binding Name}" />
        <arc:DataTableColumn Header="负责人" Binding="{Binding Owner}" IsSortable="False" />
        <arc:DataTableColumn Header="预算" Key="Budget" Binding="{Binding Budget}" IsNumeric="True" />
    </arc:SortableDataTable.Columns>
</arc:SortableDataTable>
```

### 选择

设置 `IsSelectable` 即可显示复选框列。点击一行即选中它，Shift + 点击选中一个范围，计数行提供清除操作。`SelectedItems` 为双向绑定。

```xml
<arc:SortableDataTable ItemsSource="{Binding Projects}"
                       IsSelectable="True"
                       SelectedItems="{Binding SelectedProjects, Mode=TwoWay}">
    <arc:SortableDataTable.Columns>
        <arc:DataTableColumn Header="项目" Binding="{Binding Name}" />
    </arc:SortableDataTable.Columns>
</arc:SortableDataTable>
```

### 单元格内容与格式

文本使用 `Formatter`，自定义内容（例如状态徽标）使用 `CellTemplate`。当需要避免绑定时（例如应用以 AOT 方式发布），可以在代码中使用类型化的值选择器。

```csharp
var table = new SortableDataTable { ItemsSource = projects, Caption = "项目" };
table.Columns.Add(DataTableColumn.Create<Project>("项目", p => p.Name));
table.Columns.Add(DataTableColumn.Create<Project>("预算", p => p.Budget, key: "Budget"));
table.Columns[1].Formatter = value => value is decimal amount ? amount.ToString("C0", CultureInfo.GetCultureInfo("en-US")) : "-";
table.Columns.Add(new DataTableColumn("状态", p => ((Project)p).Status)
{
    CellTemplate = new FuncDataTemplate<Project>((_, _) => new StatusPill())
});
```

### 排序与事件

`SortChanged` 和 `SelectionChanged` 报告用户的操作。设置 `IsSortClearable` 后，用户第三次点击表头即可清除排序。

```csharp
table.SortChanged += (_, e) => status.Text = e.Column is null
    ? "已清除排序"
    : $"按 {e.Column.Header} 排序，{e.Direction}";
table.SelectionChanged += (_, e) => status.Text = $"已选中 {e.SelectedItems.Count} 项";
table.Sort(table.Columns[1], DataTableSortDirection.Ascending);
```

### 空状态与加载状态

没有行时显示 `EmptyMessage`。`IsLoading` 会以加载遮罩覆盖各行，`LoadingMessage` 说明正在等待的内容。

```xml
<arc:SortableDataTable EmptyMessage="没有符合这些筛选条件的项目"
                       IsLoading="{Binding IsBusy}" />
```

## API 参考

<!-- api: SortableDataTable -->

<!-- api: DataTableColumn -->

## 无障碍

- 表格以其 `Caption` 作为名称，并公开表格、表头和行的角色。
- 可排序的表头是一个按钮，名称类似"按预算排序，当前为升序"。排序方向属于名称的一部分。
- 只选中部分行时，全选复选框显示混合状态。
- 排序与选择的变化会在状态区域播报，且不打断当前的朗读。
- 方向键在表头与复选框之间移动，Enter 或 Space 排序或切换选中，Escape 清除选择。

## 动效

- 排序变化时，各行以弹簧动画滑动到新位置，排序箭头翻转。
- 选中计数滚动到新值，清除按钮带着轻微模糊淡入。
- 减弱动效时各行立即重新排列，计数仅淡入淡出。关闭动效时直接到达最终状态。

## 相关组件

- `TreeView`：用于可嵌套的记录，例如带子项的文件夹。
- `ReorderableList`：用户通过拖动而非列来设置顺序时使用。
- `FilterToolbar`：在记录进入表格之前，按字段和搜索缩小范围。
- `Pagination`：将大量记录拆分为多页。
