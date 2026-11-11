---
title: 切换按钮组
summary: 一排切换按钮，用于单选或多个相互独立的选项。
source-hash: bbe990e3c809
---

<Demo name="TextStyle" />

## 何时使用

切换按钮组把几个紧密相关的开关放在同一个扁平的行里。按下的选项沉入带色调的填充中，因此看起来是表面上的工具，而不是页面之间的切换。

- **编辑器工具栏的文字格式**：粗体、斜体和下划线各自切换，`Mode="Multiple"` 让相邻且都按下的选项连成一个形状。
- **对齐或布局这类显示选项**：一次一个选项，填充滑向新的选择。
- **列表上方的快速筛选**：可以同时生效多个，每个都只是一个短词或一个图标。
- **紧凑的工具面板**：`Size="Small"` 提供 28 px 的选项，仍能与小尺寸控件对齐。

## 何时不用

- **在内容的不同视图之间切换**：使用 `SegmentedControl`，凹陷轨道上凸起的滑块表达“这个视图”。
- **单个开或关的设置**：使用 `Switch`，它立即生效，不需要分组。
- **每个选项都需要说明的选择**：使用 `RadioGroup` 或 `RadioCards`，它们有放说明的位置。
- **来自数据集的大量选项或筛选**：使用 `ChipGroup`，它可以换到多行。

## 用法

把选项写在组内，或把 `ItemsSource` 绑定到 `ToggleGroupOption` 列表。在默认的单选模式下，`SelectedValue` 保存按下选项的 `Value`；再次按下时保持不变，与单选组一样。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:ToggleGroup Label="Alignment" SelectedValue="{Binding Alignment}">
        <arc:ToggleGroupOption Value="start" Label="Start" />
        <arc:ToggleGroupOption Value="center" Label="Center" />
        <arc:ToggleGroupOption Value="end" Label="End" />
    </arc:ToggleGroup>
</UserControl>
```

`ValueChanged` 在使用者切换选项时触发，代码设置值时不触发。它的参数带有被切换的 `Value`、该选项现在是否 `IsPressed`，以及全部 `Values`。

### 图标与文字

`Icon` 接受 24×24 的描边图形（例如 `ArcIcons.List`），并以合适的尺寸绘制在内容之前。选项的内容就是它的可见文字。只有图标的选项必须提供 `Label`：它会成为无障碍名称和工具提示，工具提示需要不同文字时用 `Title` 替换。既无图标也无内容的选项把 `Label` 显示为文字，不带工具提示。

<Demo name="Layout" />

### 同时多个选项

`Mode="Multiple"` 让每个选项各自切换。`SelectedValues` 按按下的先后顺序列出已按下的值；在 XAML 中写成逗号分隔的列表。还没有对应选项的值会保留，等选项出现时生效。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:ToggleGroup Label="Text style" Mode="Multiple" SelectedValues="bold,italic">
        <arc:ToggleGroupOption Value="bold" Label="Bold" />
        <arc:ToggleGroupOption Value="italic" Label="Italic" />
        <arc:ToggleGroupOption Value="underline" Label="Underline" />
    </arc:ToggleGroup>
</UserControl>
```

### 尺寸

`Size` 设置选项高度：`Small` 为 28 px，图标 14 px；默认的 `Medium` 为 36 px，图标 16 px。`Large` 与 `Medium` 外观相同。

<Demo name="Sizes" />

### 禁用选项

选项上的 `IsDisabled` 让它不再响应键盘和指针，但仍保留在行中的位置。组上的 `IsEnabled="False"` 禁用所有选项并让组变淡。

<Demo name="Disabled" />

### 数据绑定的项

选项不必是 `ToggleGroupOption` 对象。把 `ItemsSource` 绑定到你自己的项，并为组提供用于内容的 `ItemTemplate`。每个选项都是公开的 `ToggleGroupItem`，其 `DataContext` 就是它的项，因此像 Avalonia 的 `ListBoxItem` 一样，可以用针对 `arc|ToggleGroupItem` 的样式把选项的其余部分绑定到项：`Icon`、`IsEnabled`、`AutomationProperties.Name` 和 `arc:Tooltip.Tip`。把 `SelectedValueBinding` 设为标识项的属性；不设置时，值就是项本身。

```xml
<arc:ToggleGroup Label="View"
                 ItemsSource="{Binding Views}"
                 SelectedValue="{Binding ViewKey}"
                 SelectedValueBinding="{Binding Key}">
    <arc:ToggleGroup.ItemTemplate>
        <DataTemplate>
            <TextBlock Text="{Binding Title}" />
        </DataTemplate>
    </arc:ToggleGroup.ItemTemplate>
    <arc:ToggleGroup.Styles>
        <Style Selector="arc|ToggleGroupItem">
            <Setter Property="Icon" Value="{Binding Glyph}" />
            <Setter Property="AutomationProperties.Name" Value="{Binding Title}" />
        </Style>
    </arc:ToggleGroup.Styles>
</arc:ToggleGroup>
```

- 使用编译绑定时，为 `DataTemplate` 和 `Style` 设置你的项类型作为 `x:DataType`。
- 只显示图标的选项需要从样式获得 `AutomationProperties.Name` 和工具提示（`arc:Tooltip.Tip`），就像 `Label` 为 `ToggleGroupOption` 提供它们一样。
- 也可以使用 `ItemContainerTheme`。让它基于选项的主题 `BasedOn="{StaticResource {x:Type arc:ToggleGroupItem}}"`，这样选项会保留填充、按压效果和焦点环。
- `ToggleGroupOption` 会自己设置所在选项的内容、图标、名称和工具提示，这些值优先于样式。

## API 参考

<!-- api: ToggleGroup -->
<!-- api: ToggleGroupOption -->
<!-- api: ToggleGroupItem -->

## 无障碍

| 按键 | 操作 |
|---|---|
| Right、Down | 把焦点移到下一个选项，并循环 |
| Left、Up | 把焦点移到上一个选项，并循环 |
| Home、End | 把焦点移到第一个或最后一个可用选项 |
| Space、Enter | 切换获得焦点的选项 |

方向键只移动焦点，因此使用者可以到达某个选项而不改变任何内容。整个组是一个制表位：上次获得焦点的选项，否则是第一个按下的选项，否则是第一个可用选项。`Label` 为组命名。单选模式下，选项报告为单选按钮；多选模式下，选项报告为带按下状态的按钮。`ToggleGroupOption` 以其 `Label` 命名；数据绑定的选项需要通过针对 `arc|ToggleGroupItem` 的样式设置 `AutomationProperties.Name`。

## 动效

单选模式下，一个填充以 morph 弹簧在选项之间滑动。多选模式下，填充在 160 ms 内淡入，同时在 360 ms 内从 86% 缩放稳定下来；连接时在 240 ms 内跨过 2 px 间隙并把相接的圆角变成直角。按下选项时内容在 90 ms 内缩小到 92%，并在 320 ms 内恢复（`ToggleGroupItem` 的 Token `PressScale`、`PressDownDuration` 和 `PressUpDuration`）；选项的方框从不缩放，因此相邻选项不会移动。减弱动效时，填充直接跳转或在 120 ms 内淡入淡出，没有缩放，颜色在 120 ms 内变化。

## 响应式

选项保持自身尺寸，不换行也不收缩，因此要让这一行在最窄的布局中也放得下，或把较长的集合改用 `ChipGroup`。悬停颜色只对能够悬停的指针出现，触摸时不出现。在手机上使用短标签或图标，中等尺寸提供 36 px 的目标。

## 相关组件

| 组件 | 适用场景 |
|---|---|
| `SegmentedControl` | 选择会切换旁边的视图 |
| `Switch` | 单个设置的开或关 |
| `RadioGroup` | 每个选项都需要标签和说明 |
| `ChipGroup` | 大量筛选项需要换到多行 |
