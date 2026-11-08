---
title: 循环面板
summary: 首尾无缝循环的列表：最后一项之后又是第一项，可用滚轮、触控板或代码滚动，并报告当前可见的项。
source-hash: 2baee2bd3be5
---

<Demo name="Preview" />

## 何时使用

`CyclicPanel` 把子元素显示成一个无尽的循环：滚过最后一个子元素会回到第一个，两个方向都可以，没有跳变，也不需要复制数据。
视口里有固定数量、大小相等的槽位。

- **滚轮选择器**：小时、分钟、月份等循环的值。
- **轮转的条带**：卡片、媒体或快捷方式。
- **时间轴、日期条**：在一周里循环。

## 何时不用

- **有明确开头和结尾的列表**：请用 `ScrollViewer` 或 `ScrollArea`。
- **带分页圆点和自动播放的分页幻灯片**：请用 `Carousel`。
- **成千上万项**：面板会创建每一个子元素，不做虚拟化。

## 用法

`CyclicPanel` 派生自 Avalonia 的 `Panel`，自己处理滚动，不需要 `ScrollViewer`。它把长度分成 `VisibleItemCount` 个槽位（默认 5 个），
槽位之间的间距为 `Spacing`（0）；400 px 高、五个槽位的面板，每项得到 80 px。子元素在横向上铺满面板。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:CyclicPanel Orientation="Vertical" VisibleItemCount="5" Height="400">
        <TextBlock Text="Item 0" />
        <TextBlock Text="Item 1" />
        <TextBlock Text="Item 2" />
        <TextBlock Text="Item 3" />
        <TextBlock Text="Item 4" />
        <TextBlock Text="Item 5" />
    </arc:CyclicPanel>
</UserControl>
```

滚动位置是 `Offset`，单位是项：2.5 表示前缘位于第三项的一半处。静止时它总是被折回 0 到项数之间，所以无论滚动多久都不会越来越大。

每个子元素只放一次，放在离视口最近的位置。一个控件不能同时出现在两处，所以至少有 `VisibleItemCount` + 1 个可见子元素时循环才是无缝的。
少于这个数时，每个子元素仍然显示一次，其余槽位留空。折叠的子元素（`IsVisible="False"`）不参与循环。

### 横向

`Orientation="Horizontal"` 让槽位从左到右排列。触控板可以横向滚动它，竖直方向的滚轮也能滚动它。

<Demo name="Horizontal" />

### 可见项数

修改 `VisibleItemCount` 会重新计算槽位大小，滚动位置保持不变。

<Demo name="VisibleItemCount" />

### 用代码滚动

- `ScrollToIndex(index)` 把子元素滚到前缘（顶部或左侧）。
- `ScrollToIndexCentered(index)` 把它滚到中间的槽位；`VisibleItemCount` 为偶数时，它居中在中间两个槽位之间。
- `ScrollNext()` 和 `ScrollPrevious()` 移动一项，`ScrollBy(items)` 移动任意项数。滚动进行中再次调用会累加到它的目标上，
  所以快速连续调用不会丢失。

每次滚动都走循环中较短的那条路：八项中从第 0 项到第 7 项是往回退一项，而不是往前走七项。索引超出子元素范围或子元素已折叠时，
索引方法返回 `false` 且不做任何事。传入 `animated: false` 可以直接跳转。

<Demo name="ScrollToIndex" />

### 滚轮与触控板

鼠标滚轮每转一格移动一项。触控板或平滑滚轮跟随手指移动，手指停下后面板停靠到最近的一项（`SnapsToItems`，默认为 true）。
`IsWheelEnabled="False"` 把滚轮留给父级。

### 可见项

`VisibleItemIndices` 按显示顺序（前缘在前）列出视口中子元素的索引；只要槽位有任何部分在面板内就算可见，所以滚到一半时会多报告一项。
它是 Avalonia 属性，只在列表变化时才变化，可以绑定或监听它来预加载图片、追踪当前项。

```csharp
panel.PropertyChanged += (_, e) =>
{
    if (e.Property == CyclicPanel.VisibleItemIndicesProperty)
    {
        Preload(panel.VisibleItemIndices);
    }
};
```

### 动画

代码发起的滚动和滚轮格滚动会以 `AnimationEasing`（Arc 标准缓出曲线）在 `AnimationDuration`（240 ms）内平滑过渡。
`IsAnimationEnabled="False"` 让它们直接跳转。`AnimationEasing` 接受任何 Avalonia 缓动，例如 `CubicEaseInOut`，也接受三次贝塞尔控制点。

<Demo name="Animation" />

### 在 ItemsControl 中使用

作为 `ItemsPanel` 时，面板循环的是项容器，索引就是项索引。`ObservableCollection` 中增删的项会加入或离开循环，滚动位置保持在循环之内。

<Demo name="ItemsControl" />

## API 参考

<!-- api: CyclicPanel -->

## 无障碍

- 面板本身不增加语义，也不处理键盘；它通过滚轮、触控板和自身的方法滚动。要做键盘操作的选择器，请在自己的按键处理中调用
  `ScrollNext()` 和 `ScrollPrevious()`（例如在获得焦点的项上按上、下方向键）。
- 滚出视口的项仍然是子元素，仍在 Tab 顺序中；可交互的项请给出各自的无障碍名称（`AutomationProperties.Name`）。

## 动效

- 滚动使用动效引擎的补间，以 `AnimationEasing` 在 `AnimationDuration` 内过渡，走循环中较短的那条路；新的滚动从当前进行中的位置开始。
- 触控板滚动直接跟随手指，停下后平滑停靠到最近的一项。
- 减少动态效果或关闭动效时，所有滚动都直接跳转：平滑滚动会移动内容，而减少动态效果会去掉移动。

## 响应式

- 槽位总是平分面板的长度，项随之变大变小；槽位数保持为 `VisibleItemCount`。
- 长度无界时（例如在沿滚动方向排列的 `StackPanel` 中），每个槽位与最长的项一样长。请给面板一个尺寸，或把它放进 `Grid` 单元格。

## 相关组件

- `Carousel`：带圆点、箭头和自动播放的分页幻灯片。
- `WaterfallPanel`：子元素高度不限的等宽列。
- `TilePanel`：磁贴可跨行跨列的网格。
