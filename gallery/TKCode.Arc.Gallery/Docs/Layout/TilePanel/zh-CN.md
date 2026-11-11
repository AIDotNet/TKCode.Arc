---
title: 磁贴面板
summary: 开始屏幕式的磁贴网格：磁贴可跨行跨列，按顺序放到第一个能放下的位置，互不重叠，并随面板缩放。
source-hash: c46efcaade96
---

<Demo name="Preview" />

## 何时使用

`TilePanel` 把区域分成相等的单元格，每个子元素可以覆盖其中几个：1×1、1×2、2×2 等。子元素按顺序放到第一个能放下的空位，
大小混合的磁贴会排成整齐的网格，无需自己计算任何位置。

- **仪表盘**：重要的指标用更大的磁贴。
- **启动器、开始屏幕**：应用与快捷方式。
- **图库**：少数重点项更大。

## 何时不用

- **要把每个子元素放到指定的行列**：请用 Avalonia 的 `Grid`。
- **子元素高度不固定**：请用 `WaterfallPanel`。
- **磁贴大小全都相同**：Avalonia 的 `UniformGrid` 或 `WrapPanel` 更简单。

## 用法

`TilePanel` 派生自 Avalonia 的 `Panel`。用附加属性 `TilePanel.RowSpan` 和 `TilePanel.ColumnSpan`（默认均为 1）设置子元素覆盖的单元格数。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:TilePanel Columns="6" Spacing="6">
        <Border arc:TilePanel.RowSpan="2" arc:TilePanel.ColumnSpan="2" Background="SteelBlue" />
        <Border arc:TilePanel.ColumnSpan="2" Background="Orange" />
        <Border Background="SeaGreen" />
    </arc:TilePanel>
</UserControl>
```

放置采用首个可用位置（first fit），结果完全确定：

1. 子元素按顺序放置。折叠的子元素（`IsVisible="False"`）不占单元格。
2. 子元素的跨度会被限制在 `MaxRowSpan` 和 `MaxColumnSpan` 之内（0 为默认值，表示不限制），也不会超出网格。
3. 子元素放到第一个所有单元格都空闲的位置：从上往下逐行扫描，每行从左往右。因此后面的小磁贴可以填上前面宽磁贴留下的空洞。
4. 磁贴永不重叠；网格会按行增长，直到每个磁贴都有位置，不会丢掉任何磁贴。

`GetPlacement(child)` 返回子元素得到的行、列和跨度，`RowCount` / `ColumnCount` 返回网格大小。

### 网格大小

`Columns` 固定列数，各列平分宽度。`Rows` 让各行平分高度（高度有界时）；放不进这么多行的磁贴会继续排在下方同样大小的新行里。
没有被设定的一边沿用另一边的尺寸，因此除非两边都设定，单元格都是正方形。

<Demo name="FixedGrid" />

`Columns="0"`（默认）时，面板按宽度放下尽可能多的、至少 `MinCellSize`（96 px）宽的列，宽度变化时磁贴重新排列。
宽度无界时（例如在横向 `ScrollViewer` 中）请改设 `Rows`：行数固定，磁贴从上到下填满一列再到下一列，网格向右增长。

<Demo name="AutoColumns" />

### 跨度上限

`MaxRowSpan` 和 `MaxColumnSpan` 限制任何磁贴能覆盖的范围，不论它要求多少。

<Demo name="SpanLimits" />

### 自动填充

`IsAutoFill`（默认关闭）是放置之后的一个可选步骤，让磁贴扩展到空单元格中。规则固定，结果可以预测：

- 按子元素顺序依次处理磁贴。
- 磁贴先向右扩展，每次一列，条件是它旁边下一列的每个单元格都为空，最多到 `MaxColumnSpan` 和最后一列。
- 然后以同样的方式向下扩展，每次一行，最多到 `MaxRowSpan` 和网格高度：`Rows` 与磁贴已用行数中较大的那个。
- 之后仍为空的每个空洞，再交给它四周的磁贴：左边或右边的磁贴横向扩展，上方或下方的磁贴纵向扩展，条件是扩展所需的整条单元格都为空，
  且不超过跨度上限。重复这一步，直到没有磁贴能再扩展。
- 磁贴从不移动、重叠或缩小，所以显式设置的跨度总会保留。只有当四周没有磁贴能在 `MaxRowSpan` / `MaxColumnSpan` 之内盖住某个空洞时，它才保持为空。

<Demo name="AutoFill" />

### 自动跨度

`IsAutoSpan`（默认关闭）让面板决定磁贴的大小：既没有设置 `TilePanel.RowSpan` 也没有设置 `TilePanel.ColumnSpan` 的子元素，
由面板分配 1×1、1×2、2×1 或 2×2。它适合所有数据项使用同一个模板的 `ItemsControl`，这时数据项上没有任何信息说明每一项该多大。

- **结果确定。** 磁贴的大小取决于它的索引、`Seed`（默认 0）和网格，布局时从不使用随机数：同样的数据项、种子和网格，
  排出来永远一样；`Columns` 固定时，改变窗口大小只会缩放磁贴。换一种排列就换一个 `Seed`。在末尾追加数据项不会改变前面的磁贴；
  在中间插入或删除一项，会改变它后面的磁贴。
- **磁贴前面不留空洞。** 自动磁贴放到第一个空单元格，并选择在那里放得下的大小，所以它前面不会留下空单元格。
- **大磁贴分散开。** 较大的磁贴彼此不相邻，不会扎堆；也不会跨过 `Rows` 的最后一行。`MaxRowSpan` 和 `MaxColumnSpan`
  仍然有效（`MaxColumnSpan="1"` 时只有 1×1 和 2×1）。
- **显式跨度优先。** 设置了任一跨度的子元素保持它要求的跨度，与不开自动跨度时一样。

同时开启 `IsAutoFill` 且网格固定时（除 `Columns` 外还设置了 `Rows`，或由宽度决定列数），面板会让自动磁贴盖满整个
`Columns` × `Rows` 网格：数据项越少，磁贴越大，剩下的空格由自动填充步骤补上。数据项多于单元格时，自动磁贴都是 1×1，
多出的部分接着排在后面的行里。

<Demo name="AutoSpan" />

### 在 ItemsControl 中使用

面板的子元素是项容器，所以跨度要设在容器上，例如用一个样式从数据项中读取。替换某一项会用新跨度重建它的容器。
不需要指定大小的数据项，配合 `IsAutoSpan` 可以完全不设跨度（见[自动跨度](#auto-span)）。

<Demo name="ItemsControl" />

## API 参考

<!-- api: TilePanel -->

## 无障碍

- 面板本身不增加语义；子元素的顺序就是阅读和 Tab 顺序，与放置顺序一致（从左到右、从上到下，小磁贴可能填到更靠前的空洞里）。请让顺序有意义。
- 可交互的磁贴请给出各自的无障碍名称（`AutomationProperties.Name`）。

## 动效

- 面板不做动画：宽度、网格或子元素变化时，磁贴在一次布局中直接移到新的单元格。

## 响应式

- `Columns` 固定时，放置结果不变，只有单元格尺寸随宽度变化。
- `Columns="0"` 时列数随宽度变化（放下尽可能多的、至少 `MinCellSize` 宽的列），磁贴重新排列。
- 比网格还宽的跨度会被裁到列数，所以窄宽度下磁贴也不会超出面板。

## 相关组件

- `WaterfallPanel`：子元素高度不限的等宽列。
- `CyclicPanel`：首尾无限循环的列表。
