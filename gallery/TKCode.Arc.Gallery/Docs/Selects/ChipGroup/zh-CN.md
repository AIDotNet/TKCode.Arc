---
title: 标签组
summary: 用几个会随选择而变形的标签进行筛选。
source-hash: be8b71bea6f8
---

<Demo name="FilterChips" />

## 何时使用

- 用户经常切换的少数维度筛选，例如主题或类别。
- 允许清除的单选场景，使用 `IsMultiple="False"`。
- 所有选项都应保持可见，便于用户一眼比较各个选择。

## 何时不用

- 空间紧张，或字段值必须显示在触发器内：用 `MultiSelect`。
- 表单中每个选项都是"是"或"否"的回答：用 `Checkbox`。
- 在几个视图中选择一个：用 `SegmentedControl`。

## 用法

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:ChipGroup Label="主题" MaxWidth="560">
        <arc:SelectItem Content="设计" />
        <arc:SelectItem Content="工程" />
        <arc:SelectItem Content="研究" />
        <arc:SelectItem Content="市场" />
        <arc:SelectItem Content="销售" />
    </arc:ChipGroup>
</UserControl>
```

### 绑定选项与值

将 `ItemsSource` 设为任意对象，或 `SelectOption` 记录。`SelectedValues` 按选项顺序列出所选标签的值。它是只读的，因此请在处理程序中读取。

```xml
<arc:ChipGroup Label="主题"
               MaxWidth="560"
               ItemsSource="{Binding Topics}"
               SelectedItems="{Binding ChosenTopics}" />
```

```csharp
var topics = new ChipGroup
{
    Label = "主题",
    MaxWidth = 560,
    ItemsSource = new[] { "设计", "工程", "研究", "市场" }
};
topics.Selection.Select(1);
topics.SelectionChanged += (_, _) => Console.WriteLine(string.Join(", ", topics.SelectedValues));
```

### 单选

`IsMultiple="False"` 一次只允许一个标签。已选中的标签仍可再次点击以清除选择。

<Demo name="SingleChoice" />

### 折叠长列表

`MaxVisibleItems` 显示指定数量的标签，其余标签折叠在"+N 更多"标签之后。已选中的标签即使超出限制也会保持可见。`IsExpanded` 为双向绑定，用于显示或隐藏折叠的标签。

<Demo name="Folded" />

```xml
<arc:ChipGroup Label="主题"
               MaxVisibleItems="5"
               MaxWidth="560"
               IsExpanded="{Binding ShowAllTopics, Mode=TwoWay}"
               ItemsSource="{Binding Topics}" />
```

### 在代码中切换

`Toggle(index)` 切换某个索引处的标签，效果与点击相同。

```csharp
topics.Toggle(0);
```

## API 参考

<!-- api: ChipGroup -->

<!-- api: SelectItem -->

## 无障碍

- `Label` 是该组的无障碍名称。
- 每个标签都是带按下状态的按钮。屏幕阅读器会将其播报为切换按钮。
- 该组采用漫游停靠点：Tab 顺序中只有一个标签。方向键在标签之间移动并循环，Home 与 End 分别前往第一个与最后一个标签。
- Space 或 Enter 切换获得焦点的标签。在"+N 更多"标签上，则展开或收起折叠的标签。
- "+N 更多"标签会报告折叠的标签是否已展开。焦点框绘制在获得焦点的标签上。

## 动效

- 选中的标签会从左边缘长出一个对勾，标签文字向外滑开至新的宽度。
- 相邻的标签滑动到新的位置，包括跨行时。
- 列表展开时，折叠的标签会以短暂的错开时间出现，组的高度平滑变化。
- 按下的标签会轻微缩小，松开时以弹簧回弹。
- 减弱动效时，形变改为短暂的淡入淡出。关闭动效时，标签直接切换。

## 相关组件

- `MultiSelect`：值必须放进紧凑字段时使用。
- `Checkbox`：表单中各自独立的选项。
- `SegmentedControl`：从两到四个视图中选择一个。
- `Select`：表单中从短列表选择单一值。
