---
title: 瀑布流面板
summary: 瀑布流（Masonry）面板：高度不一的卡片流入等宽的列，每张都放进当前最短的那一列，排列紧凑、没有空隙。
source-hash: e167720d49f0
---

<Demo name="Preview" />

## 何时使用

`WaterfallPanel` 把高度不一的子元素排进等宽的列（或把宽度不一的子元素排进等高的行），不会像 `WrapPanel` 或
`UniformGrid` 那样在行与行之间留下空隙。每个子元素都放到当前最短那条通道的末尾，所以各列长度保持接近。

- **图片墙、灵感板**：图片保持各自的宽高比。
- **笔记、卡片列表**：卡片里的文字长短不一。
- **资源浏览器、仪表盘**：卡片高度各不相同。

## 何时不用

- **需要跨行的阅读顺序**：瀑布流是沿着列往下读的，不是横着读。请用 `WrapPanel` 或网格。
- **需要跨多列的磁贴**：请用 `TilePanel`。
- **成千上万项**：面板会创建每一个子元素，不做虚拟化。

## 用法

`WaterfallPanel` 派生自 Avalonia 的 `Panel`。可以直接放子元素，也可以作为 `ItemsControl` 的 `ItemsPanel`。
`Orientation="Vertical"`（默认）时，宽度被分成 `Columns` 列（默认 3 列），列间距为 `Spacing`（12 px）；每个子元素按列宽测量，
保留自己的高度。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <ScrollViewer>
        <arc:WaterfallPanel Columns="3" Spacing="10">
            <Border Height="120" Background="SteelBlue" />
            <Border Height="200" Background="Orange" />
            <Border Height="150" Background="SeaGreen" />
            <Border Height="90" Background="Purple" />
        </arc:WaterfallPanel>
    </ScrollViewer>
</UserControl>
```

按子元素顺序，最短通道优先：子元素放进末端最靠上的那一列，两列一样短时取靠前的一列。因此同样的子元素总是得到同样的布局，
在末尾追加子元素也不会移动前面的元素。尺寸默认来自子元素自己的测量结果；`ItemSizing="Varied"` 改由面板来定
（见[错落尺寸](#varied-sizes)）。面板的高度等于最长的那一列，所以放在 `ScrollViewer` 里能正确滚动。

折叠的子元素（`IsVisible="False"`）不占位置。增删子元素、子元素尺寸变化或修改任何属性，面板都会重新布局。

### 横向

`Orientation="Horizontal"` 交换两个轴：高度被分成 `Rows` 行（默认 3 行），子元素保留自己的宽度，放到最短那一行的末尾。
请把它放进横向滚动的 `ScrollViewer`。

<Demo name="Horizontal" />

### 通道数

`Columns`（横向时为 `Rows`）设置通道数。设为 `0` 时按宽度放下尽可能多的通道，每条至少 `MinLaneSize`（默认 200 px）宽：
窄窗口一两列，宽窗口五六列。`LaneCount` 报告上一次布局实际使用的通道数。

<Demo name="Columns" />

### 子元素尺寸限制

`MinItemSize` 和 `MaxItemSize` 限制每个子元素沿排列方向的尺寸（列中即高度）。子元素在 `MaxItemSize` 之内测量，
所以换行的文字会按实际能得到的空间排版。子元素自己的 `MinHeight`（行中为 `MinWidth`）总是优先于 `MaxItemSize`，
内容不会被裁到它要求的尺寸以下。

<Demo name="ItemSize" />

### 错落尺寸

所有数据项使用同一个模板时，它们的高度相同，面板排出来就是整齐的网格。设置 `ItemSizing="Varied"` 后面板不再看内容的尺寸：
它在 `MinItemSize` 与 `MaxItemSize` 之间为每个子元素分配高度（行中为宽度），同一模板的卡片也能排出瀑布流。子元素在分到的尺寸内测量，
所以模板应当可以拉伸（铺满卡片的图片或色块），而不是写死高度。

- **结果确定。** 尺寸只取决于子元素的索引和 `Seed`（默认 0）。布局时不使用随机数：同样的数据项、种子和通道数，
  排出来永远一样，改变窗口大小也不会打乱排列。换一种排列就换一个 `Seed`。
- **按索引计算。** 在末尾追加数据项不会改变前面各项的尺寸；在中间插入或删除一项，会改变它后面各项的尺寸。
  隐藏子元素（`IsVisible="False"`）不影响其他子元素的尺寸。
- **上下限。** 同时设置 `MinItemSize` 和 `MaxItemSize` 得到固定的区间。没有设置的一端跟随通道尺寸：
  最小值为半条通道，最大值为一条半通道。子元素自己的 `MinHeight`（或 `MinWidth`）仍然优先。

<Demo name="VariedSizes" />

### 填充末尾元素

`FillLastItem` 把每条通道的最后一个子元素拉伸到面板末端，让所有通道一起结束。末端是最长的那一列；如果父级给面板的空间更大
（固定的 `Height`、可拉伸的 `Grid` 行），则是面板的排列尺寸。在 `ScrollViewer` 里末端始终是最长的那一列，所以不会无限拉伸。
只改变每条通道最后一个可见的子元素，折叠的子元素永远不会被拉伸。

<Demo name="FillLastItem" />

### 在 ItemsControl 中使用

作为 `ItemsPanel` 时，面板排列的是项容器，因此 `ObservableCollection` 中增删的项会就地出现或消失。这个示例还用了
`Columns="0"`，列数随宽度变化。

<Demo name="ItemsControl" />

## API 参考

<!-- api: WaterfallPanel -->

## 无障碍

- 面板本身不增加语义；子元素的顺序就是阅读和 Tab 顺序，在瀑布流中它是按放置顺序沿列往下，而不是从左到右。请让子元素的顺序有意义。
- 可交互的卡片请给出各自的无障碍名称（`AutomationProperties.Name`）。

## 动效

- 面板不做动画：宽度、通道数或子元素变化时，子元素在一次布局中直接移到新位置。

## 响应式

- `Columns` 固定时，各列平分面板得到的宽度，随之变宽变窄。
- `Columns="0"` 时列数随宽度变化：放下尽可能多的、至少 `MinLaneSize` 宽的列，至少一列。
- 在横向 `ScrollViewer` 中（宽度无界）使用 `Orientation="Vertical"` 时，每列与最宽的子元素一样宽；建议改用纵向 `ScrollViewer`。

## 相关组件

- `TilePanel`：磁贴可跨行跨列的网格。
- `CyclicPanel`：首尾无限循环的列表。
