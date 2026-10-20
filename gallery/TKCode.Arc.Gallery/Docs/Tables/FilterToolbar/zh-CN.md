---
title: 筛选工具栏
summary: 让集合筛选条件保持在近处，并便于重置。
source-hash: f50ac881ac43
---

## 何时使用

- 位于表格或列表上方的分面筛选，例如状态、负责人或优先级。
- 筛选、搜索与表格协同工作的列表页面。
- 用户可逐个移除的筛选条件，并在设置了任意筛选时提供「全部清除」操作。

## 何时不用

- 以切换按钮展示的一组固定选项：用 `ChipGroup`。
- 仅有自由文本搜索：用 `SearchField`。
- 需要接收多个值的表单字段：用 `MultiSelect`。

## 用法

每个筛选条件都是一个 `FilterChip`，包含 `Id`、`Label` 与 `Value`。「添加筛选」菜单中的字段放入 `Fields`。每个字段都有选项，用户在第二步中进行选择。

```xml
<arc:FilterToolbar Label="当前筛选"
                   ActiveFilters="{Binding Filters}"
                   MenuAlignment="Start">
    <arc:FilterToolbar.Fields>
        <arc:FilterField Id="status" Label="状态">
            <arc:FilterField.Options>
                <arc:FilterOption Value="待处理" />
                <arc:FilterOption Value="处理中" />
                <arc:FilterOption Value="已关闭" />
            </arc:FilterField.Options>
        </arc:FilterField>
        <arc:FilterField Id="owner" Label="负责人">
            <arc:FilterField.Options>
                <arc:FilterOption Value="Mina" />
                <arc:FilterOption Value="Theo" />
            </arc:FilterField.Options>
        </arc:FilterField>
    </arc:FilterToolbar.Fields>
</arc:FilterToolbar>
```

### 通过代码设置筛选

将当前生效的筛选创建为 `FilterChip` 列表。每个字段只保留一个条件：添加与已有条件 `Id` 相同的条件时，会替换原有的条件。

```csharp
var toolbar = new FilterToolbar
{
    ActiveFilters = new ObservableCollection<FilterChip>
    {
        new("status", "状态", "待处理"),
        new("owner", "负责人", "Mina")
    }
};
toolbar.AddFilter(new FilterChip("priority", "优先级", "高"));
toolbar.RemoveFilter("owner");
toolbar.ClearFilters();
```

### 响应变化

`FiltersChanged` 会报告发生了什么变化（`Kind`）、涉及的条件，以及全部当前生效的筛选。用它重新加载工具栏所筛选的数据。

```csharp
toolbar.FiltersChanged += (_, e) => Refilter(e.ActiveFilters);
```

### 搜索

`IsSearchVisible` 会在工具栏中添加搜索框。`SearchText` 为双向绑定，因此表格可以对其作出响应。

```xml
<arc:FilterToolbar IsSearchVisible="True"
                   SearchPlaceholder="搜索问题"
                   SearchText="{Binding Query, Mode=TwoWay}" />
```

### 额外操作

`Actions` 可将其他控件（例如导出按钮）放在「添加筛选」触发器旁边。

```xml
<arc:FilterToolbar>
    <arc:FilterToolbar.Actions>
        <Button Content="导出" />
    </arc:FilterToolbar.Actions>
</arc:FilterToolbar>
```

## API 参考

<!-- api: FilterToolbar -->

<!-- api: FilterChip -->

<!-- api: FilterField -->

## 无障碍

- 工具栏默认是一个名为「当前筛选」的分组。设置 `Label` 可为其指定适合页面的名称。
- 每个条件都有一个移除按钮，名称形如 "移除 状态：待处理"。移除后，焦点会移动到相邻的条件，或「添加筛选」触发器。
- 添加和移除筛选会在状态区域中播报。
- 「添加筛选」菜单是一个带名称的对话框，其选项为可勾选的菜单项。
- 按 Down 或 Up 可从触发器打开菜单。方向键在列表中移动，Right 进入字段的值，Left 返回，输入字符可跳转到对应选项。
- Escape 关闭菜单，并将焦点返回到触发器。

## 动效

- 条件会在自身的位置展开与收起，相邻的条件滑动让出空间。
- 值变化时在原位变形。设置了任意筛选时，「全部清除」即会显示。
- 「添加筛选」触发器会展开为菜单面板，字段与值的步骤则朝各自的方向滑入。
- 减弱动效时，变化立即切换，并伴随约 160 ms 的短暂淡入淡出。关闭动效时直接跳到最终状态。

## 相关组件

- `ChipGroup`：一小组固定的切换项。
- `SearchField`：不带筛选的自由文本搜索。
- `SortableDataTable`：被筛选条件缩小范围的表格。
- `MultiSelect`：需要接收多个值的表单字段。
