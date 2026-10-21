---
title: 矩形树图
summary: 方形化的矩形树图：点击即可下钻，色块放大填满视图，面包屑可返回，指标变化时每个色块平滑过渡。
source-hash: 05f28046d980
---

<Demo name="RevenueByRegion" />

## 何时使用

- 叶子节点很多的层级数据，例如按地区、国家和套餐划分的营收，需要看清每一部分所占的比例。
- 需要在大小之外再读取的第二个指标，例如增长率，以每个色块颜色的深浅表示。
- 向下钻取某个分支再返回的场景，面包屑会保持路径可见。

## 何时不用

- 数值彼此接近、需要精确比较：用 `BarChart`。
- 只有少数几个部分构成整体，角度即可说明问题：用 `DonutChart`。
- 只是一组普通的导航步骤：单独使用 `Breadcrumb`。

## 用法

色块的大小取自 `Value`，深浅取自 `ColorValue`。分支本身没有数值，其大小是子节点之和。`Label` 为整体命名，同时也是面包屑的第一级。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:Treemap Label="营收"
                 ColorLabel="增长率"
                 ItemsSource="{Binding Regions}"
                 KeyMemberBinding="{Binding Code}"
                 LabelMemberBinding="{Binding Name}"
                 ValueMemberBinding="{Binding Revenue}"
                 ColorValueMemberBinding="{Binding Growth}"
                 ChildrenMemberBinding="{Binding Countries}" />
</UserControl>
```

### 节点树

数据在代码中构建时，将 `Root` 设为由 `TreemapNode` 对象组成的树。树中每个 `Id` 都必须唯一：保留同一 `Id` 的色块在数据变化时会平滑过渡到新的大小。

```csharp
var root = new TreemapNode("all", "全部地区", children:
[
    new TreemapNode("amer", "美洲", children:
    [
        new TreemapNode("us", "美国", 420, 18),
        new TreemapNode("ca", "加拿大", 80, 12)
    ]),
    new TreemapNode("emea", "欧洲", children:
    [
        new TreemapNode("de", "德国", 190, 7),
        new TreemapNode("uk", "英国", 150, -2)
    ])
]);

chart.Label = "营收";
chart.Root = root;
```

如果不设置 `ColorLabel`，每个色块都以同一深度绘制。若只需要一组扁平的顶层叶子节点，使用 `Items`：

<Demo name="OneLevel" />

```csharp
chart.Items.Add(new TreemapNode("video", "视频", 112));
chart.Items.Add(new TreemapNode("photos", "照片", 48));
chart.Items.Add(new TreemapNode("docs", "文档", 12));
```

### 格式化与着色

`ValueFormatter` 用于格式化大小，`ColorFormatter` 用于格式化着色指标。`ColorDomainMinimum` 与 `ColorDomainMaximum` 固定着色范围的两端；默认情况下，它们分别是最小与最大的叶子节点数值。

```xml
<arc:Treemap Label="席位"
             ColorLabel="增长率"
             ValueFormatter="{Binding FormatSeats}"
             ColorFormatter="{Binding FormatGrowth}"
             ColorDomainMinimum="-10"
             ColorDomainMaximum="40" />
```

### 切换指标

同时修改 `Label`、`ValueFormatter` 与 `Root`。`Id` 相同的色块会保持其身份，因此它们会在原处改变大小和颜色，而不是被替换。

```csharp
void ShowSeats()
{
    chart.Label = "席位";
    chart.ValueFormatter = v => $"{v:#,0}";
    chart.Root = seatsRoot;
}
```

### 焦点与缩放

点击某个分支会让它填满视图。`FocusedNodeId` 保存当前视图中节点的 id（`null` 表示根节点），也可以在代码中设置它，将视图缩放到指定节点。`FocusedNodeChanged` 会在每次变化时发出通知。

```xml
<arc:Treemap Label="营收" Root="{Binding Tree}" FocusedNodeId="{Binding FocusedId, Mode=TwoWay}" />
```

```csharp
chart.FocusedNodeChanged += (_, e) => status.Text = $"当前视图：{e.Node?.Label}";
```

## API 参考

<!-- api: Treemap -->

<!-- api: TreemapNode -->

## 无障碍

- `Label` 为图表命名。每个色块都是可聚焦的分组，其名称给出路径、数值及其占父级的比例。
- 方向键移动到该方向上最近的色块；Home 与 End 分别跳到第一个和最后一个顶层色块；Enter 或空格键会缩放到当前聚焦的分支。
- Escape 或 Backspace 缩小一级。面包屑是导航地标，并标明当前层级。
- 缩放变化以及聚焦色块的路径、数值和着色，会以礼貌方式朗读。
- 一个隐藏的数据表列出了每个节点，因此即使看不到色块，也能读取整个层级结构。

## 动效

- 放大时，所选色块通过一次仿射移动填满视图：其同级色块飞出，其子节点在其中展开。缩小时沿相同路径反向进行。
- 更改指标时，每个色块在原处移动到新的大小与颜色，总计数值滚动到新值。
- 缩放过程中，标签保持其大小。只有标签能放入色块时才会显示。
- 减弱动效时，色块立即定位到最终位置。关闭动效的效果与减弱动效相同。

## 相关组件

- `BarChart`：数值彼此接近时的精确比较。
- `DonutChart`：少数几个部分构成整体的场景。
- `Breadcrumb`：不属于图表的导航路径。
