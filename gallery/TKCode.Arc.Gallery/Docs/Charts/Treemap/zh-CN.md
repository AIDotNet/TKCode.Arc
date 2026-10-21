---
title: 矩形树图
summary: 方形化的矩形树图：点击即可下钻，色块放大填满视图，面包屑可返回，指标变化时每个色块平滑过渡。
source-hash: d8e4665db708
---

<Demo name="RevenueByRegion" />

## 何时使用

`Treemap` 以嵌套矩形展示层级数据。每个色块的大小是它占整体的份额，第二个指标还可以加深颜色，因此一个视图就能回答“钱（或负载、席位）集中在哪里，其中哪些部分正在增长？”。它适用于叶子节点很多的树：如果每个叶子节点都画一根柱子，柱子会多到无法阅读；读者也无需离开当前屏幕，就能下钻到某个分支再返回。

- **财务评审中按地区、国家和套餐划分的营收**：色块大小表示各市场的规模，颜色深浅表示其增长率。读者无需查看百分比表，就能看出美洲承担了大部分营收，印度增长最快。
- **管理控制台中按文件夹划分的存储用量**：磁盘用量视图中每个文件夹是一个色块，点击较大的文件夹即可下钻到其子文件夹。面包屑显示回到卷的路径。
- **账单视图中按客户细分划分的席位与许可用量**：同一棵树改为展示席位而非营收。读者切换指标后，色块会原地改变大小，两个视图讲的是同一个故事。
- **可靠性报告中按服务和组件划分的事件**：色块大小取事件数量，颜色深浅取相对上一季度的变化，并可从平台下钻到触发最多告警的服务。
- **云成本评审中按团队和成本中心划分的支出**：点击团队色块即可展开其账号，面包屑始终保留通往总计的路径，读者可以在查看明细的同时不丢失上下文。

## 何时不用

- **数值彼此接近、需要精确比较**：人对矩形面积的判断不如对条形长度精确。数值共享同一基线时，用 `BarChart`。
- **只有少数几个部分构成整体，角度就足以说明问题**：四到五段的构成用环形图会更清楚。用 `DonutChart`。
- **随时间的变化**：树图只呈现某一时刻。趋势用 `LineChart`，随时间变化的构成用 `Streamgraph`。
- **普通的导航步骤列表**：返回上一级的路径本身就是面包屑，不是图表。页面需要时用 `Breadcrumb`。
- **读者需要逐项阅读的层级，例如包含数千条目的文件树**：浏览用 `TreeView`，查看精确数值用 `DataGrid`。树图会隐藏小色块，放不下时其标签也会消失。

## 用法

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

`Treemap` 派生自 Avalonia 的 `Control`。它的数据来自以下三种来源之一，优先级依次为：`Root`（`TreemapNode` 树）、`ItemsSource`（你自己的对象，通过选择器或成员绑定读取）、`Items`（内联的 `TreemapNode` 对象，适用于一小组固定的叶子节点）。色块的大小取自 `Value`，深浅取自 `ColorValue`。分支本身没有数值，其大小是子节点之和。`Label` 为图表命名；设置 `Root` 时，根节点自身的标签就是面包屑的第一级。

### 结构

1. **面包屑**：从根节点到当前视图中节点的路径。每一级高 32 units，最后一级为当前层级。视图变化时，面包屑从左侧滑入。
2. **色块区域**（`StageRadius`，12 units）：容纳色块的矩形区域。色块会被裁剪到其圆角之内。
3. **色块**：方形化布局。相邻色块之间保留 2 unit 的间距。带有子节点的分支会预留 22 unit 的标题区显示名称，其子节点在标题区下方、以 3 unit 的内边距排列。
4. **色块标签**：每个色块的名称，按该色块自身的大小排版。只有标签能放入色块时才会显示，因此小色块不显示标签，其名称出现在工具提示中。
5. **色阶**（设置 `ColorLabel` 时显示）：着色指标的名称，以及从最低值到最高值的色阶。面包屑、色块和色阶之间的间距为 12 units。
6. **工具提示**：色块的父级路径、色块名称及其格式化后的数值，以及它占父级的比例，例如“占欧洲的 75%”。

### 选择树的来源

| 来源 | 适用场景 |
|---|---|
| 使用 `TreemapNode` 的 `Root` | 树在代码中构建，或由服务返回嵌套模型后转换一次 |
| 使用选择器或成员绑定的 `ItemsSource` | 领域对象本身已构成树，且不希望复制一份 |
| XAML 中的 `Items` | 一小组固定的叶子节点，例如演示中的文件夹或静态报表 |

使用 `ItemsSource` 时，`KeySelector`（或 `KeyMemberBinding`）必须为每一项返回唯一的 id，因为正是这个 id 在数据变化时保持色块的身份。`ChildrenSelector`（或 `ChildrenMemberBinding`）返回分支的嵌套项，叶子节点则返回空。

### 节点树

数据在代码中构建时，将 `Root` 设为由 `TreemapNode` 对象组成的树。节点接受 `Id`、`Label`、可选的叶子 `Value`、可选的着色 `ColorValue` 以及 `Children`。树中每个 `Id` 都必须唯一：保留同一 `Id` 的色块在数据变化时会过渡到新的大小。

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

分支的 `ColorValue` 是其子节点按大小加权的平均值，因此即使分支本身没有颜色，也会显示它所代表地区的增长情况。从 `ItemsSource` 读取的节点，`TreemapNode.Source` 会返回原始数据项。

未设置 `ColorLabel` 时，每个色块都以同一深度绘制。若只需要一组扁平的顶层叶子节点，使用 `Items`：

<Demo name="OneLevel" />

```csharp
chart.Items.Add(new TreemapNode("video", "视频", 112));
chart.Items.Add(new TreemapNode("photos", "照片", 48));
chart.Items.Add(new TreemapNode("docs", "文档", 12));
```

### 着色与颜色

`ColorLabel` 启用第二个指标。每个顶层分支依次取前四个系列色相之一；每个位于其中的色块则按 `ColorValue` 在几个步长之间由浅到深加深。第五个及之后的分支保持中性色，而不会复用色相，因此若色相有意义，请把顶层分支保持在四个或以内。没有 `ColorValue` 的色块取中性色；负值与正值使用同一色阶，因此正在缩小的地区会显示为色阶的浅色一端。

`ColorDomainMinimum` 和 `ColorDomainMaximum` 固定着色范围的两端。默认值为 `NaN`，表示最小和最大的叶子值。当同一屏幕上的两个树图需要共用一个色阶，或希望色阶在数据变化时保持不变（例如增长率范围为负 10% 到正 40%）时，将它们固定。

### 格式化

`ValueFormatter` 用于格式化大小，`ColorFormatter` 用于格式化着色指标。默认的大小格式会分组显示数字，并最多保留一位小数。两个格式化器都用于工具提示、色阶以及无障碍朗读，因此它们必须与你在 `Label` 和 `ColorLabel` 中写的标签保持一致。

```xml
<arc:Treemap Label="席位"
             ColorLabel="增长率"
             ValueFormatter="{Binding FormatSeats}"
             ColorFormatter="{Binding FormatGrowth}"
             ColorDomainMinimum="-10"
             ColorDomainMaximum="40" />
```

### 切换指标

同时修改 `Label`、`ValueFormatter` 和 `Root`。`Id` 相同的色块会保持其身份，因此它们会在原处改变大小和颜色，而不是被替换。总计数值会滚动到新值，面包屑中的路径在其 id 仍然存在时保持不变。

```csharp
void ShowSeats()
{
    chart.Label = "席位";
    chart.ValueFormatter = v => $"{v:#,0}";
    chart.Root = seatsRoot;
}
```

### 焦点与缩放

点击色块会将视图缩放到包含它的顶层分支：该分支填满视图，其同级色块移出，其子节点在其中展开。`FocusedNodeId` 保存当前视图中节点的 id，`null` 表示根节点；该属性是双向的，因此视图模型可以在刷新后保留下钻状态。在代码中设置它会缩放到对应节点，这也是以指定分支打开视图的方式。

`FocusedNodeChanged` 会报告视图的每一次变化，包括通过面包屑或键盘缩小层级。其 `Node` 是当前视图中的节点。其 `Id` 是该节点的 id，因此视图位于根节点时它是根节点的 id（例如 `all`），而 `FocusedNodeId` 对根节点为 `null`。

```xml
<arc:Treemap Label="营收" Root="{Binding Tree}" FocusedNodeId="{Binding FocusedId, Mode=TwoWay}" />
```

```csharp
chart.FocusedNodeChanged += (_, e) => status.Text = $"当前视图：{e.Node?.Label}";
```

### 绑定到视图模型

在 MVVM 应用中，视图模型拥有树、指标和下钻状态。使用 CommunityToolkit.Mvvm 时，视图模型是一个 `ObservableObject`，指标切换是一条同时替换标签、格式化器和树的命令。

```csharp
public interface IRegionData
{
    TreemapNode BuildRevenueTree();

    TreemapNode BuildSeatsTree();
}

public partial class RevenueMapViewModel : ObservableObject
{
    private readonly TreemapNode _revenue;
    private readonly TreemapNode _seats;

    public RevenueMapViewModel(IRegionData data)
    {
        _revenue = data.BuildRevenueTree();
        _seats = data.BuildSeatsTree();
        Tree = _revenue;
    }

    [ObservableProperty]
    private TreemapNode? _tree;

    [ObservableProperty]
    private string? _focusedId;

    [ObservableProperty]
    private string _measureLabel = "营收";

    [RelayCommand]
    private void ShowRevenue()
    {
        MeasureLabel = "营收";
        Tree = _revenue;
    }

    [RelayCommand]
    private void ShowSeats()
    {
        MeasureLabel = "席位";
        Tree = _seats;
    }
}
```

```xml
<arc:Treemap Label="{Binding MeasureLabel}"
             ColorLabel="增长率"
             Root="{Binding Tree}"
             FocusedNodeId="{Binding FocusedId, Mode=TwoWay}" />
```

需要绑定的属性是 `Root`、`FocusedNodeId` 和 `PlotHeight`。`FocusedNodeId` 是双向的：读者的下钻状态会回流到视图模型，视图模型可以保存或恢复它。`Label` 是单向的；格式化器属于代码，应在视图的代码后置文件中设置，或在与之作用的树相邻的视图模型格式化服务中设置。

### 数据结构与性能

每个色块的大小由其下方的叶子节点计算得出，布局每个聚焦节点重新计算一次，而不是每一帧都计算。Arc 的建议是单个视图上放几百个节点；超过这个数量时，用 `FocusedNodeId` 下钻，并每次只显示一个层级。

- **叶子节点**：叶子节点的 `Value` 必须大于零才会占据空间。没有值或值为零的叶子节点不占空间，因此在传入数据之前应检查是否存在空项。
- **深度**：每个分支都会增加标题区和内边距，因此深层树给叶子节点留下的空间更少。当色块必须显示标签时，将树控制在两到三层。
- **Id**：在刷新之间保持 id 稳定。新 id 意味着新色块，复用的 id 则表示同一个色块，它会移动到新的大小。
- **更新频率**：每分钟刷新一次看起来就是更新。若树在每个时钟周期都变化，色块会持续移动，读者将无法跟踪某一个色块。
- **格式化**：大小和颜色由 `ValueFormatter` 和 `ColorFormatter` 格式化。每个屏幕都使用相同的货币和百分比约定，使共享的视图显示一致的数字格式。

### 示例：财务评审中按地区划分的营收

财务负责人在季度评审开始时，用一张图展示营收集中在哪里，以及各地区增长的速度。地区是顶层，因此每个地区取四种系列色相之一，地区内的国家就是它的色块。色块大小是以千美元计的营收，颜色深浅是相对去年同一季度的增长率，因此德国大而平缓，印度较小但颜色深得多。读者下钻到欧洲查看其国家，再用面包屑返回。因为场景是静态的，这里的数值是字面量；实时视图则如上所示绑定 `Root` 或 `ItemsSource`。

![2026 年第二季度各地区营收，大小表示营收，颜色深浅表示增长率](images/RevenueByRegion.png)

```xml
<StackPanel Width="720" Spacing="12">
    <arc:Treemap Label="Revenue, $K, Q2 2026"
                 ColorLabel="Growth, %"
                 PlotHeight="320">
        <arc:Treemap.Items>
            <arc:TreemapNode Id="amer" Label="Americas">
                <arc:TreemapNode Id="us" Label="United States" Value="420" ColorValue="18" />
                <arc:TreemapNode Id="ca" Label="Canada" Value="80" ColorValue="12" />
                <arc:TreemapNode Id="br" Label="Brazil" Value="65" ColorValue="31" />
            </arc:TreemapNode>
            <arc:TreemapNode Id="emea" Label="Europe">
                <arc:TreemapNode Id="de" Label="Germany" Value="310" ColorValue="7" />
                <arc:TreemapNode Id="uk" Label="United Kingdom" Value="240" ColorValue="-2" />
                <arc:TreemapNode Id="fr" Label="France" Value="90" ColorValue="5" />
            </arc:TreemapNode>
            <arc:TreemapNode Id="apac" Label="Asia Pacific">
                <arc:TreemapNode Id="jp" Label="Japan" Value="190" ColorValue="14" />
                <arc:TreemapNode Id="in" Label="India" Value="65" ColorValue="44" />
                <arc:TreemapNode Id="au" Label="Australia" Value="65" ColorValue="4" />
            </arc:TreemapNode>
        </arc:Treemap.Items>
    </arc:Treemap>
</StackPanel>
```

标签中写明了单位，因为默认的数字格式只显示数字。增长率为负 2 时，英国色块会落在色阶的浅色一端，这就是在不使用红色的情况下表示市场萎缩的方式。

### 文案规范

用指标而不是视图来命名整体，例如“营收，$K，2026 年第二季度”。把单位写进标签或格式化器，因为在一个有三个指标的屏幕上，单独的“420”没有任何意义。色块名称要足够短，能放进你预期需要显示标签的最小色块。

| 不要写 | 改为 |
|---|---|
| `Label="Chart"` | `Label="Revenue, $K, Q2 2026"` |
| `ColorLabel="g"` | `ColorLabel="Growth, %"` |
| 图表为“营收”时，根节点标签为 `Label="All regions"` | 图表标签为 `Label="Revenue"`，根节点命名为“全部地区” |
| `Label="United Kingdom (UK)"` | `Label="United Kingdom"` |
| `Label="Seats (number)"` | `Label="Seats"` |
| 用代码命名的色块，例如 `uk-ent` | 为套餐或细分市场取一个易读的名称 |

### 最佳实践

**推荐**

- 在 `Label` 中写明指标及其单位；当单纯的数字含义模糊时，用 `ValueFormatter` 格式化大小。屏幕阅读器首先朗读的就是标签。
- 为每个节点设置稳定的 `Id`。id 决定数据变化时色块是过渡还是被替换。
- 当色相承载含义时，将顶层分支保持在四个或以内，因为第五个及之后的分支是中性色。
- 当两个树图需要共用一个色阶时，设置 `ColorDomainMinimum` 和 `ColorDomainMaximum`，使同一种颜色在两个图中表示相同的增长率。
- 当下钻状态在刷新后必须保留时，将 `FocusedNodeId` 设为双向绑定，使读者不会丢失他们正在查看的分支。

**避免**

- 大量需要标签的小叶子节点。色块太小时不显示标签，读者只能通过工具提示阅读。
- 五到六层的深树。每一层都要占用标题区和内边距，叶子节点会变成细条。
- 对没有方向的指标使用颜色深浅，例如人员数量。对没有好坏之分的值使用色阶，容易造成错误的解读。
- 每次刷新都用新 id 重建的树。色块会被替换而非过渡，下钻状态也会丢失。
- 只用颜色深浅来区分地区。色相、标签和朗读内容才承载身份信息。

### 常见问题

**为什么某个色块没有标签？** 只有标签能放入色块时才会显示。把图表放大、提高 `PlotHeight`，或下钻到该分支使色块变大即可。色块的名称和数值仍在其工具提示中，并且色块获得焦点时也会朗读。

**为什么同一地区的色块颜色相同？** 色相以顶层分支为单位：地区内的每个色块都采用该地区的色相。其内部的深浅取决于每个色块自身的 `ColorValue`，整张图使用同一个色阶。

**如何让树图打开时就缩放到某个分支？** 将 `FocusedNodeId` 设为该分支的 id，例如 `FocusedNodeId="emea"`。`null` 显示根节点。

**为什么 `FocusedNodeChanged` 报告的是根节点的 id，而不是 `null`？** 该事件报告的是当前视图中的节点，因此根节点报告的是它自己的 id，例如 `all`。`FocusedNodeId` 对根节点为 `null`，所以应与节点的 `Id` 比较，而不要判断事件中的 `Id` 是否为 `null`。

**分支可以有数值吗？** 不可以。分支的大小是其子节点之和，其颜色深浅是子节点 `ColorValue` 按大小加权的平均值。应把数值放在叶子节点上。

## API 参考

<!-- api: Treemap -->


<!-- api: TreemapNode -->

## 无障碍

- `Label` 为图表命名。色块区域会播报图表名称、当前视图中的节点，以及可用的按键，例如“营收，全部地区。方向键在色块间移动，Enter 放大，Escape 缩小。”获得焦点的色块会朗读其路径、数值及占父级的比例，例如“欧洲，德国，15，占欧洲的 75%”。
- 色块区域是礼貌的实时区域。缩放变化以及获得焦点的色块的朗读内容会被播报，但焦点不会移动。
- 面包屑是导航地标，其最后一项标示当前层级。
- 一个隐藏的资料表列出了每个节点，因此即使看不到色块，也能读取整个层级结构。
- 色块的颜色深浅从来不是唯一的信息来源。朗读内容包含着色指标的数值，色阶也会标明该指标的名称。
- 只有标签能放入色块时才会显示标签。对于放不下标签的色块，获得焦点时仍可通过色块区域的朗读内容读取，鼠标指向时也可通过工具提示读取。

### 键盘

色块区域是一个 Tab 停靠点。方向键在当前视图节点的顶层色块之间移动；面包屑是第二个 Tab 停靠点，用于查看路径。

| 按键 | 操作 |
|---|---|
| `Tab` / `Shift+Tab` | 在色块区域与面包屑之间移动焦点 |
| `Arrow` 方向键 | 移动到该方向上最近的顶层色块 |
| `Home` / `End` | 移动到第一个或最后一个顶层色块 |
| `Enter` / `Space` | 放大到获得焦点的分支，并聚焦其第一个子节点 |
| `Escape` / `Backspace` | 缩小一级，并聚焦刚离开的节点 |
| 在面包屑中：`Left` / `Right` | 在各级面包屑之间移动 |
| 在面包屑中：`Home` / `End` | 移动到根级面包屑或当前层级 |
| 在面包屑中：`Enter` / `Space` | 缩小到获得焦点的面包屑所对应的层级 |

在叶子节点上按 Enter 不会有任何效果，因为叶子节点没有可打开的子节点。在根节点上按 Escape 会清除获得焦点的色块。

## 动效

- 放大时，所选色块通过一次仿射移动填满视图：其同级色块飞出，其子节点在其中展开。缩小时沿相同路径反向进行。
- 更改指标时，每个色块在原处移动到新的大小和颜色，总计数值滚动到新值。
- 缩放过程中，标签保持其大小。只有标签能放入色块时才会显示。
- 工具提示以灵敏的弹簧跟随指针移动，并始终停留在色块区域之内。
- 减弱动效时，色块立即定位到最终位置。关闭动效的效果与减弱动效相同。

| 变化 | 完整 | 减弱 | 关闭 |
|---|---|---|---|
| 放大或缩小 | 一次仿射移动，平滑弹簧约 0.62 s；同级色块飞出，子节点展开 | 色块直接跳到最终位置 | 立即完成 |
| 指标或数据变化 | 每个色块以平滑弹簧在原处移动并改变颜色；总计数值滚动 | 色块直接跳到最终位置 | 立即完成 |
| 面包屑变化 | 各级面包屑滑动到新位置 | 立即完成 | 立即完成 |
| 工具提示 | 以灵敏的弹簧跟随指针 | 立即完成 | 立即完成 |

## 相关组件

- `BarChart`：数值彼此接近时的精确比较。
- `DonutChart`：少数几个部分构成整体的场景。
- `Breadcrumb`：不属于图表的导航路径。

| 组件 | 适用场景 |
|---|---|
| `BarChart` | 数值彼此接近，读者必须精确比较 |
| `DonutChart` | 只有少数几个部分构成整体，占比就是要传达的信息 |
| `LineChart` | 问题是某个总量随时间如何变化 |
| `Streamgraph` | 各部分的构成随时间变化，流动过程就是故事 |
| `Breadcrumb` | 在没有树图的页面上需要导航路径 |
| `DataGrid` | 读者需要每个节点及其精确数值，并可排序和筛选 |
