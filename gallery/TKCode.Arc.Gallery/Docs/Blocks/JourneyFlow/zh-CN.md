---
title: Journey flow
summary: "以桑基图呈现旅程：指向一个步骤或一条流向，即可追踪该群体经过的每个阶段；选中一个步骤，查看这些人从哪里来、接下来去了哪里；切换细分群体，观看每条流向变形。"
source-hash: 340dafb201d4
---

<Demo name="Preview" />

## 何时使用

`JourneyFlow` 展示一群人如何穿过一系列阶段，以及他们在哪里离开。阶段从左到右排列；每个阶段包含主路径上的一个步骤，以及在它下方停在这里的人；相邻阶段之间的丝带有多粗，就表示走这条路的人有多少。最后一个阶段中的目标为绿色。

- **带流失的漏斗。** 访问、注册、引导、激活、套餐：每一步的流失都是一条看得见的丝带，而不只是一个百分比。
- **比较细分群体。** 共享同一结构的渠道、群组或月份。图表上方的开关会把每根柱、每条丝带和每个计数变形为另一个细分群体，因此人们看到的是变化本身。
- **追问"这些人是谁"。** 指向一个步骤，会在整个旅程中追踪所有经过它的人：他们从哪里来，最后去了哪里。指向单条丝带，则对走这条路的人做同样的事。

## 何时不用

- **回环、跳转或众多路径。** 连线只连接相邻阶段，节点按你给出的顺序堆叠。对于人们会回退或跳到后面的旅程，请使用图视图。
- **每个阶段超过三个节点。** 每个节点需要在上一个节点下方留出 44 像素放置它的标签；一列中有四五个节点时，留给柱的空间所剩无几。
- **单一转化率。** KPI 磁贴或一根小柱用更小的空间就能说明"6% 已订阅"。
- **先要精确数字。** 图表用于呈现形状和比例；其下的 "Flow table" 提供精确计数，当数字才是重点时，普通表格更合适。

## 用法

区块派生自 Avalonia 的 `TemplatedControl`，图表由它自己绘制。为它提供阶段、节点（每个节点带有其阶段的键）以及一个或多个细分群体，每个细分群体是从一个节点到下一个阶段中某个节点的连线列表。

```xml
<arc:JourneyFlow Title="Trial to paid journeys"
                 SubtitleFormat="{}{0} visits in October, from first page to plan"
                 GoalCaption="of visitors picked a paid plan">
    <arc:JourneyFlow.Stages>
        <arc:JourneyStage Key="landing" Header="Landing" ShortHeader="Land" />
        <arc:JourneyStage Key="trial" Header="Trial" />
        <arc:JourneyStage Key="plan" Header="Plan" />
    </arc:JourneyFlow.Stages>
    <arc:JourneyFlow.Nodes>
        <arc:JourneyNode Key="arrived" StageKey="landing" Header="Arrived" Description="Opened a course page" />
        <arc:JourneyNode Key="started" StageKey="trial" Header="Started trial" />
        <arc:JourneyNode Key="left" StageKey="trial" Header="Left" Kind="Exit" DropOffLabel="Left before the trial" />
        <arc:JourneyNode Key="subscribed" StageKey="plan" Header="Subscribed" Kind="Goal" />
        <arc:JourneyNode Key="lapsed" StageKey="plan" Header="Lapsed" Kind="Exit" />
    </arc:JourneyFlow.Nodes>
    <arc:JourneySegment Key="all" Header="All learners">
        <arc:JourneyLink SourceKey="arrived" TargetKey="started" Value="5320" />
        <arc:JourneyLink SourceKey="arrived" TargetKey="left" Value="11470" />
        <arc:JourneyLink SourceKey="started" TargetKey="subscribed" Value="1025" />
        <arc:JourneyLink SourceKey="started" TargetKey="lapsed" Value="4295" />
    </arc:JourneySegment>
</arc:JourneyFlow>
```

### 结构

- **页眉**：`Title`、由 `SubtitleFormat` 得到的副标题（访客数在 `{0}` 的位置滚动显示），以及细分群体开关（有两个或更多细分群体时显示）。
- **图表**：每个阶段一列，带有一条参考线，阶段名称位于其下；节点柱宽 8，从顶部起每隔 44 堆叠；每根柱上方有两行标签（名称，然后是访客的 "count · share"）；阶段之间是丝带。节点的计数取流入和流出中较大的一个，因此第一个阶段的总数就是访客数。所有阶段使用同一比例，所以最满的阶段会占满整个高度。
- **侧边面板**：从 860 像素起位于图表旁边，更窄时位于图表下方。没有选中项时它是概览：目标数量及其占访客的比例，最大的流失（最后一个阶段之前、无人继续前行的退出，以及它们占上一步的比例），以及最后一个阶段的结果。选中某个步骤时它显示该步骤：它的计数、占访客的比例、其人员从哪里到达以及接下来去了哪里。
- **Flow table**：图表下方的折叠面板，列出每条流向及其占所离开步骤的比例。

### 节点类型

`Kind` 决定颜色：`Step` 以强调色绘制，流入它的流向为 10% 的 Accent；`Exit` 为柔和的灰色，流向很淡（4%）；`Goal` 为成功色，流向为绿色（16%）。退出是概览中列为流失的内容，目标则是概览所统计的内容。

### 追踪与选择

指向一个步骤或它的标签，或指向一条丝带，会追踪该群体：图表的其余部分淡出，该群体走过的每条流向都被一条同等大小的强调色带覆盖，群体在每根柱中所占的部分变为实心；该群体从未到达的步骤的标签也会淡出。提示框跟随指针，显示计数和两个比例。点击一个步骤会选中它：当你指向别处时它仍保持被追踪状态，侧边面板切换到它，并且选择在切换细分群体后依然保留。再次点击它、点击图表空白处、按 Escape 或使用面板的 Clear 按钮即可清除选择。点击丝带不会选中。

`SelectedNode` 和 `SelectedSegment` 是双向的。`SelectedNodeChanged`、`SelectedSegmentChanged` 和 `TraceChanged` 报告这些变化（最后一个针对被预览的节点或连线，指针或键盘均可），`NodeSelectedCommand` 和 `SegmentSelectedCommand` 在用户操作时运行，`GetCohort` 返回每条连线上被追踪的计数，供你自己的汇总使用。

<Demo name="Selection" />

```csharp
journey.SelectNode("hooked");
journey.SelectedSegment = journey.Segments.First(s => s.Key == "social");
journey.TraceChanged += (_, e) =>
{
    if (e.Node is { } node)
    {
        var cohort = journey.GetCohort(node);
        status.Text = $"{node.Header}: {cohort.Values.Sum():N0} people across {cohort.Count} flows";
    }
};
```

### 窄容器

区块读取自己的宽度，而不是窗口的宽度。小于 860 时，侧边面板移到图表下方，其两个分组并排；559 或更小时，开关占满整个宽度，内边距收紧到 16，分组改为堆叠；479 或更小时，标签变小。图表从不滚动或旋转：它在每个宽度下都会重新布局，720 像素宽的图表高 404，480 起为 372，更窄则为 340。小于 600 时标签去掉比例，小于 440 时每个阶段最上方的节点只显示计数，阶段名称切换为 `ShortHeader`。

<Demo name="Narrow" />

### 数字和文字

`ValueFormatter` 格式化每一个计数（标签、提示框、面板、表格、朗读的名称），`ShareFormatter` 格式化每个以分数给出的比例，`LabelShareFormatter` 格式化图表标签上取整后的比例。即使使用你自己的格式化器，面板中的数字也会逐位滚动：数字前后的文字保持不动。每个短语都是带有英文默认值的属性：`CountFormat`（"{0} users"）、`ShareOfFormat`、`VisitorsShareText`、`VisitorsCaption`、`GoalCaption`、`OverviewHint`、`DropOffsHeader`、`OutcomesHeader`、`InboundHeader`、`OutboundHeader`、`JourneyStartText`、`JourneyEndText`、`ClearText`、`TableHeader`、`TableCaptionFormat`、`TableColumnHeaders` 和 `AnnouncementFormat`。提示框中的 "Share of …" 一行会指明该阶段所有人所经过的步骤；设置 `JourneyStage.ShareBaseLabel` 或 `JourneyNode.ShareLabel` 即可自己定义这行的措辞。

只有一个细分群体时开关隐藏；`IsTableVisible` 和 `IsSidePanelVisible` 可移除表格和面板。

<Demo name="Tickets" />

### 实时计数

修改所显示细分群体中某条连线的 `Value`，图表会就地变形，就像切换细分群体一样。`StagesSource`、`NodesSource` 和 `SegmentsSource` 接受由你自己持有的集合（会跟随可观察集合的变化），取代 `Stages`、`Nodes` 和 `Segments` 列表。

<Demo name="Live" />

## API 参考

<!-- api: JourneyFlow -->

<!-- api: JourneyStage -->

<!-- api: JourneyNode -->

<!-- api: JourneyLink -->

<!-- api: JourneySegment -->

<!-- api: JourneyNodeKind -->

<!-- api: JourneyNodeChangedEventArgs -->

<!-- api: JourneySegmentChangedEventArgs -->

<!-- api: JourneyTraceChangedEventArgs -->

## 无障碍

### 键盘

- **Tab** 停在细分群体开关上，然后在图表上停一次（上次获得焦点的步骤，起初是第一个），选中某个步骤时再停在面板的 Clear 按钮上，然后是 "Flow table"。
- 在开关上，**←/↑** 和 **→/↓** 移到上一个和下一个细分群体并循环；图表立即跟随。**Home** 和 **End** 跳到第一个和最后一个。
- 在图表上，**↑/↓** 在阶段内移动（到两端停止），**←/→** 移到相邻阶段中中心最近的步骤，**Home** 和 **End** 跳到第一个和最后一个步骤。获得焦点的步骤会被追踪，其提示框位于它旁边。
- **Enter** 或 **Space** 选中获得焦点的步骤，若已选中则清除。**Escape** 清除选择并让焦点保持原处。
- 点击 Clear 之后，焦点回到原来被选中的步骤。

### 屏幕阅读器

- 每个步骤是一个切换按钮，名称如 "Started trial: 5,320 users, 31.7% of visitors. Began the free trial."，其按下状态就是选中状态。这些步骤构成一个由 `ChartLabel` 命名的分组，它的帮助文字逐阶段朗读整张图表。
- 细分群体开关是一个由 `SegmentsLabel` 命名的分组；其中的各个细分群体会报告哪一个被按下。
- 切换细分群体时会以礼貌的方式播报（"Search: 8,050 visits and 510 paid plans."）。追踪和选择不会播报；获得焦点的步骤的名称已说明了一切。
- 提示框只是绘制出来的，并对屏幕阅读器隐藏。滚动的数字读出的是最终值，而不是滚动中的值。
- 侧边面板是一个由 `PanelLabel` 命名的区域；空的行槽位会被隐藏。Flow table 按钮会报告是否已展开，表格以带标题的文字形式读出。
- 焦点环宽 2 像素，位于步骤命中框外 2 像素处。

## 动效

- **细分群体开关**：每个计数都跟随一个弹簧（刚度 70，阻尼 16.4，无过冲，大约 0.8 秒稳定），整张图表每一帧都根据移动中的计数重新布局，因此柱、丝带和标签作为一个整体移动。标签上的计数以同样的弹簧晚 65 ms 跟随。副标题中的访客数和面板中的数字在平滑弹簧上逐位滚动，比例条滑动，面板的标题在 120 ms 内淡出，而新标题在 240 ms 内上升 4 像素进入。开关的滑块在快速弹簧上滑动。
- **追踪**：图表的其余部分在 160 ms 内淡出；该群体的色带和柱上的叠层在快速弹簧上增长，当你移到另一个步骤时，它们从当前位置变化，而不是重新开始。提示框在 120 ms 内淡入，毫无延迟地跟随指针，并在被追踪的对象变化时交叉淡入淡出其内容。
- **选择**：步骤的环在 160 ms 内淡入；面板的文字如上所述切换。
- **Flow table**：高度在平滑弹簧上展开，箭头在快速弹簧上翻转。
- 减弱动效会让图表和数字立即就位，只保留短暂的淡入淡出（提示框、面板标题）。关闭则立即应用每个变化。区块首次出现时没有任何动画。

## 相关

- `Cohort retention` 和 `Metrics dashboard`，即其他 Analytics 区块。
- `SegmentedControl`，用于独立的开关。
- `Sortable data table`，适用于数字比形状更重要的场合。
