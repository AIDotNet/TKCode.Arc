---
title: 浮动按钮组
summary: 将柔和按钮分隔放置在安静的托盘中，一个共享高亮随指针在按钮之间变形移动，按下状态则原地回落。
source-hash: 4ca39453b0d8
---

## 何时使用

- 浮动编辑器或画布上的操作，例如撤销、重做、分享和演示。
- 出现在文字上方的选择工具栏，包含仅图标的按钮与快捷键提示。
- 画布旁的纵向工具栏。

## 何时不用

- 保持选中状态的选择，例如文字对齐方式：用 Toggles 中的 `SegmentedControl`。
- 放不进所在行的工具栏：用 Menus 中的 `DropdownMenu` 承载溢出的内容。
- 用分隔线连接成同一表面的相关操作：用 `ButtonGroup`。
- 单个操作：用 `Button`。

## 用法

每个按钮都是一个 `FloatingButtonGroupItem`。为托盘设置 `Label`，它即是托盘的无障碍名称。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:FloatingButtonGroup Label="看板操作">
        <arc:FloatingButtonGroupItem Label="撤销" Shortcut="⌘Z" IsIconOnly="True" />
        <arc:FloatingButtonGroupItem Label="重做" Shortcut="⇧⌘Z" IsIconOnly="True" />
        <arc:FloatingButtonGroupSeparator />
        <arc:FloatingButtonGroupItem Label="分享" />
    </arc:FloatingButtonGroup>
</UserControl>
```

### 图标与仅图标按钮

在代码中将 `Icon` 设为 24 x 24 的 Lucide 图形。按钮上的 `IsIconOnly` 会隐藏其标签。在托盘上设置 `IsIconOnly`，可使每个按钮都只显示图标。按钮自身的设置优先于托盘的设置。仅图标的按钮以及带有 `Shortcut` 的按钮，会在工具提示中显示其标签。

```xml
<arc:FloatingButtonGroup Label="文字格式"
                         Variant="Floating"
                         IsIconOnly="True">
    <arc:FloatingButtonGroupItem Label="粗体" Shortcut="⌘B" />
    <arc:FloatingButtonGroupItem Label="斜体" Shortcut="⌘I" />
    <arc:FloatingButtonGroupSeparator />
    <arc:FloatingButtonGroupItem Label="链接" Shortcut="⌘K" />
</arc:FloatingButtonGroup>
```

```csharp
bold.Icon = ArcIcons.Bold;
link.Icon = ArcIcons.Link;
```

`Shortcut` 只显示提示。控件不绑定按键；请自行处理按键。

### 标签变化与预留宽度

将 `ReserveLabels` 设为按钮可能显示的其他标签，例如 "已复制"。按钮随后会保持最宽标签的宽度，因此相邻的按钮不会移动。

```csharp
var share = new FloatingButtonGroupItem { Label = "分享", ReserveLabels = ["已复制"] };
share.Click += async (_, _) =>
{
    share.Label = "已复制";
    await Task.Delay(1600);
    share.Label = "分享";
};
```

### 来自数据的项与纵向布局

`ItemsSource` 可将普通数据转换为按钮。`Orientation="Vertical"` 会将按钮纵向堆叠成一列，此时工具提示默认向右展开。设置 `TooltipPlacement` 可更改展开的方向。

```xml
<arc:FloatingButtonGroup Label="缩放" Size="Small" ItemsSource="{Binding ZoomLevels}" />

<arc:FloatingButtonGroup Label="工具" Orientation="Vertical" IsIconOnly="True">
    <arc:FloatingButtonGroupItem Label="选择" Shortcut="V" />
    <arc:FloatingButtonGroupItem Label="画笔" Shortcut="P" />
</arc:FloatingButtonGroup>
```

### 事件

`ActionInvoked` 会在某个项自身的 `Click` 处理程序和命令之后运行。`e.Item` 是执行操作的按钮，`e.Data` 则是源项。

```csharp
board.ActionInvoked += (_, e) => log.Text = $"操作：{e.Item.Label}";
```

## API 参考

<!-- api: FloatingButtonGroup -->

## 无障碍

- 托盘具有 ToolBar 角色，由 `Label` 命名，并报告其方向。
- 托盘是单一的 Tab 停靠点。焦点会返回到上一次获得焦点的按钮。
- 方向键在可用的按钮之间移动，并且会循环。Home 和 End 分别移动到第一个和最后一个按钮。
- Enter 和空格键激活按钮。已选中的按钮会报告其切换状态。
- 每个按钮都以其标签命名。分隔符会作为分隔符被播报。快捷键提示会作为帮助文字公开。
- 禁用的按钮仍可读取，方向键会跳过它们。

## 动效

- 一个高亮会在位置和尺寸上变形，移动到指针或键盘焦点所在的按钮。当指针跨过间隙或分隔符时，高亮保持不动。
- 托盘出现时，高亮会从按钮中生长出来。指针离开托盘时，高亮淡出。
- 按下的按钮会使高亮变暗，高亮随之略微收缩。按钮本身不缩放。
- 标签变化在原位交叉淡化。带有预留标签的按钮保持宽度；否则高亮会跟随新的宽度变化。
- 减弱动效时，高亮以短暂的淡入移动到目标位置，标签则在不发生位移的情况下交叉淡化。关闭动效时，所有内容立即到达最终位置。

## 相关组件

- `ButtonGroup`：用分隔线连接成同一表面的相关操作。
- `ExpandingButtonGroup`：紧凑的图标按钮，标签在获得焦点时展开。
- `SegmentedControl`：保持选中状态的选择。
- `Button`：单个操作。
