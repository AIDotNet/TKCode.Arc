---
title: 按钮组
summary: 将相关操作合并到一个带细分隔线的整体中：悬停高亮在各段之间滑动，按下的段就地响应，附加菜单还可以收起整排按钮。
source-hash: ef3f1c0641b5
---

## 何时使用

- 同一对象上两到五个相关操作，例如文档上的分享、复制与归档。
- 缩放控件或小型编辑工具栏，各段作用于同一个对象。
- 不常用或具有破坏性的操作，与常用操作放在一起，并收入附加菜单。

## 何时不用

- 需要保持选中状态的选项，例如视图模式：使用 Toggles 中的 `SegmentedControl`。
- 一个主要操作加若干相近的备选：使用 `SplitButton`。
- 在不同视图或面板之间切换：使用 `Tabs`。

## 用法

每一段都是一个 `ButtonGroupItem`。为组设置 `Label`，它就是无障碍名称。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:ButtonGroup Label="文档操作">
        <arc:ButtonGroupItem Label="分享" />
        <arc:ButtonGroupItem Label="复制" />
        <arc:ButtonGroupItem Label="归档" />
    </arc:ButtonGroup>
</UserControl>
```

### 图标与纯图标段

在代码中将 `Icon` 设为 24 乘 24 的 Lucide 图形。`IsIconOnly` 会隐藏文字，此时文字会成为提示与无障碍名称。

```xml
<arc:ButtonGroup Label="缩放" Size="Small">
    <arc:ButtonGroupItem Label="缩小" IsIconOnly="True" />
    <arc:ButtonGroupItem Label="放大" IsIconOnly="True" />
</arc:ButtonGroup>
```

### 实时数值与文字变化

`Value` 会在文字位置显示内容，例如缩放比例。`Label` 仍是无障碍名称。文字也可以在按下后变化；将其他文字列在 `ReserveLabels` 中，这一段就会保留最宽的宽度。

```csharp
var zoomValue = new ButtonGroupItem { Label = "重置缩放", Value = "100%" };
zoomValue.Click += (_, _) => zoomValue.Value = "100%";

var share = new ButtonGroupItem { Label = "分享", ReserveLabels = ["已复制"] };
share.Click += async (_, _) =>
{
    share.Label = "已复制";
    await Task.Delay(1600);
    share.Label = "分享";
};
```

### 附加菜单

`Menu` 会在整排末尾添加一个折角段。`MenuLabel` 为它命名。菜单与组的边缘对齐打开，`MenuItemClicked` 会报告所选的行。

```xml
<arc:ButtonGroup Label="文档操作" MenuLabel="更多文档操作">
    <arc:ButtonGroupItem Label="分享" />
    <arc:ButtonGroupItem Label="归档" />
    <arc:ButtonGroup.Menu>
        <arc:MenuSurface>
            <arc:MenuEntry Header="置顶" />
            <arc:MenuEntry Header="删除" IsDestructive="True" HasSeparatorBefore="True" />
        </arc:MenuSurface>
    </arc:ButtonGroup.Menu>
</arc:ButtonGroup>
```

### 变体、方向与事件

`Variant="Solid"` 使用主填充色，每个屏幕最多使用一次。`Orientation="Vertical"` 会将各段纵向堆叠，此时菜单对齐到起始边。`ActionInvoked` 在某一段自身的点击处理程序之后执行。

```xml
<arc:ButtonGroup Label="消息操作" Variant="Solid">
    <arc:ButtonGroupItem Label="回复" />
    <arc:ButtonGroupItem Label="全部回复" />
    <arc:ButtonGroupItem Label="转发" />
</arc:ButtonGroup>

<arc:ButtonGroup Label="插入" Orientation="Vertical">
    <arc:ButtonGroupItem Label="图片" />
    <arc:ButtonGroupItem Label="表格" />
</arc:ButtonGroup>
```

```csharp
document.ActionInvoked += (_, e) => log.Text = $"操作：{e.Item.Label}";
```

## API 参考

<!-- api: ButtonGroup -->

## 无障碍

- 该组具有 Group 角色，并由 `Label` 命名。
- 每一段都是一个 Tab 停靠点。方向键沿组的方向在各段之间移动，并可循环。Home 与 End 分别移到第一段和最后一段。
- Enter 或 Space 激活某一段。在菜单段上，Enter、Space 或 ArrowDown 会打开菜单。
- 纯图标段与收起的段以其文字作为名称与提示，其图标会被隐藏。
- 按下后很快发生变化的文字只会播报一次。状态从不仅靠颜色来表示。

## 动效

- 一个高亮会滑动到指针或键盘焦点处。高亮经过时，相邻的分隔线会淡出。
- 按下的段会轻微下沉其内容，然后以弹簧回弹。组的外框及相邻的段不会移动。
- 文字发生变化的段会以弹簧调整尺寸；若已预留宽度，则改为交叉淡化。
- 菜单从组的边缘展开，打开期间折角会旋转。
- 减弱动效时，高亮直接跳到目标位置，取消按下下沉与折角旋转，文字与菜单改为交叉淡化。关闭动效时，所有内容立即到达最终状态。

## 相关组件

- `SegmentedControl`：需要保持选中状态的选择。
- `SplitButton`：一个默认操作加一组备选菜单。
- `FloatingButtonGroup`：托盘中相互独立的柔和按钮。
- `ExpandingButtonGroup`：紧凑的图标按钮，焦点时显示文字。
</content>
