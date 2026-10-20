---
title: 多选
summary: 选择多个值，同时保持字段清晰易读。
source-hash: 46f2d403bf6d
---

## 何时使用

- 从固定列表中选择多个值的紧凑场景，例如标签或负责人。
- 在字段内直接显示已选值的筛选器。
- 选项可能需要简短搜索的列表，可通过 `IsSearchable` 开启。

## 何时不用

- 所有选项都需要保持可见，并能同时切换：用 `ChipGroup`。
- 用户自行输入并创建的自由值：用 `TagInput`。
- 只需选择一个值：用 `Select`；列表较长时用 `Combobox`。

## 用法

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:MultiSelect Label="标签"
                     Description="显示在问题卡片上。"
                     Width="320">
        <arc:SelectItem Content="缺陷" Value="bug" />
        <arc:SelectItem Content="功能" Value="feature" />
        <arc:SelectItem Content="文档" Value="docs" />
        <arc:SelectItem Content="基础设施" Value="infra" IsEnabled="False" />
    </arc:MultiSelect>
</UserControl>
```

### 绑定选项与值

将 `ItemsSource` 设为 `SelectOption` 记录。`SelectedValues` 按选择的先后顺序列出值。它是只读的，因此请在事件处理程序或代码中读取。

```csharp
var labels = new MultiSelect
{
    Label = "标签",
    Width = 320,
    ItemsSource = new[]
    {
        new SelectOption("bug", "缺陷"),
        new SelectOption("feature", "功能"),
        new SelectOption("docs", "文档"),
        new SelectOption("infra", "基础设施", isDisabled: true),
    }
};
labels.SelectionChanged += (_, _) => Console.WriteLine(string.Join(", ", labels.SelectedValues));
```

### 标签片与溢出

已选值会以标签片的形式显示在字段中。`MaxVisibleChips` 设置直接显示的数量，其余项会折叠为一个 `+N` 标签片。默认值为 2。

```xml
<arc:MultiSelect Label="负责人"
                 MaxVisibleChips="1"
                 Width="280"
                 ItemsSource="{Binding Owners}" />
```

### 可搜索的菜单

`IsSearchable` 会在菜单顶部添加搜索框。`SearchPlaceholder` 设置其提示文字。这是 Arc 的扩展功能，Arc 原生菜单没有搜索框。

```xml
<arc:MultiSelect Label="城市"
                 Placeholder="选择城市"
                 SearchPlaceholder="输入城市"
                 IsSearchable="True"
                 Width="320"
                 ItemsSource="{Binding Cities}" />
```

### 通过代码选择

`Selection` 是选择模型。`SelectAll()` 和 `UnselectAll()` 仅供代码调用，因为 Arc 没有全选控件。

```csharp
labels.Selection.Select(0);
labels.Selection.Select(1);
labels.UnselectAll();
```

### 占位文字与禁用状态

未选择任何项时显示 `Placeholder`。禁用的字段会忽略输入，并隐藏清除按钮。

```xml
<arc:MultiSelect Label="审阅者" Placeholder="选择选项" IsEnabled="False" />
```

## API 参考

<!-- api: MultiSelect -->

<!-- api: SelectOption -->

## 无障碍

- `Label` 为字段命名。名称中同时列出已选标签，屏幕阅读器因此能读出当前的值。
- 字段会说明它是一个多选列表。菜单打开时，展开状态会对外提供。
- Enter 或 Space 可打开和关闭菜单。方向键可打开菜单并移动高亮，高亮会循环并跳过禁用的选项。Enter 切换高亮选项的选中状态，菜单保持打开。Escape 关闭菜单。
- 标签片对辅助技术隐藏，并由一段朗读摘要代替。清除按钮的名称为“清除选择项”。
- 焦点框绘制在触发器上。

## 动效

- 标签片位置以平滑的弹簧效果展开到新宽度。每个标签片都从模糊中渐渐放大出现。
- `+N` 计数会随数字变化向上或向下滚动。
- 菜单中的每个勾选标记都会自行绘制，菜单从上方落下。
- 减弱动效时，尺寸与缩放变化改为短暂的交叉淡入淡出。关闭动效时，变化直接完成。

## 相关组件

- 所有选项都需要可见，并且要切换几个维度时用 `ChipGroup`。
- 用户需要创建自由值时用 `TagInput`。
- 从简短列表中选择一个值用 `Select`。
- 从用户需要搜索的长列表中选择一个值用 `Combobox`。
