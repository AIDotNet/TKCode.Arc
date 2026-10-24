---
title: Workflow canvas
summary: "在点阵画布上展示工作流并播放其运行过程：各步骤依次点亮，数据沿连线绘制，每个步骤都报告其结果和耗时。"
source-hash: 6fe02e81f603
---

<Demo name="Preview" />

## 何时使用

- 展示自动化、流水线或智能体工作流是如何组成的：它的步骤、分支以及各分支通向何处。
- 播放正在发生的一次运行，或从历史记录中重放一次运行：哪个步骤正在运行、每个步骤发现了什么、耗时多久，以及这次运行跳过了哪个分支。
- 解释在后台为用户工作的产品（webhook、数据同步、部署），其中看到步骤运行本身正是重点。
- 让人们自行搭建或修改工作流：移动步骤、连接步骤、按条件分支。

## 何时不用

- 人按顺序完成的简短直线式步骤：使用 `Stepper`。
- 没有结构、按时间顺序排列的事件：使用 `Timeline`，智能体运行则使用 `AgentTimeline`。
- 可编辑清单形式的智能体计划：使用 `AgentPlan`。
- 各阶段之间流动的数量：使用桑基图（Sankey）之类的图表。

## 用法

用步骤（`WorkflowNode`）以及步骤之间的连接（`WorkflowEdge`，或 `Connect`）构建一个 `WorkflowGraph`，再把它交给 `WorkflowCanvas`。没有 `Position` 的步骤会按层从上到下排列，排列得到的位置会写回到步骤中。

```xml
<arc:WorkflowCanvas x:Name="Canvas" />
```

```csharp
var graph = new WorkflowGraph();
var trigger = new WorkflowNode("trigger", "Order placed") { Icon = ArcIcons.Zap };
var lookup = new WorkflowNode("lookup", "Find customer account") { Description = "Plan, seats and owner" };
graph.Nodes.Add(trigger);
graph.Nodes.Add(lookup);
graph.Connect(trigger, lookup);
Canvas.Graph = graph;
```

### 播放一次运行

在步骤真正发生变化时设置其 `State`：步骤开始时设为 `Running`，完成时设为 `Done`（并设置 `Result` 和 `Duration`），失败时设为 `Failed`（并设置 `Error`），对于分支未走到的步骤则设为 `Skipped`。下一次运行前调用 `ResetRun`。连线会自动跟随其步骤：当一个步骤完成、下一个步骤开始时，数据会以强调色沿连线绘制；通往已跳过步骤的连线会变为虚线。

```csharp
lookup.State = WorkflowNodeState.Done;
lookup.Result = "Enterprise, 212 seats";
lookup.Duration = TimeSpan.FromMilliseconds(118);
branch.State = WorkflowNodeState.Running;
```

你可以让下一个步骤在上一个步骤完成的同一时刻开始运行：开启 `QueueRunVisuals`（默认开启）后，下一张卡片会一直显示为等待中，直到数据绘制进入卡片为止，因此运行总是按"步骤、连线、步骤"的顺序呈现。只有卡片所显示的内容会等待；`State` 仍是你设置的值。

### 步骤状态

每张卡片在行尾显示其步骤的状态：没有运行时不显示任何内容，等待时为空心圆环，运行时为旋转的圆环，完成时为耗时加一个对勾，失败时为警示标记，跳过时为减号。运行中的卡片采用强调色，失败的卡片使用红色边框，跳过的卡片会淡出。`WorkflowNodeCard` 也可以在画布之外单独使用。

<Demo name="States" />

### 已完成的运行

在画布出现之前就设置好最终状态，即可展示一次来自历史记录的运行：不会播放任何动画，这次运行所经过的每条连线都会立即绘制完成。

<Demo name="FinishedRun" />

### 紧凑步骤

`Appearance="Compact"` 会将卡片排成两行，尺寸为 200 × 84：上方为图标和状态，下方为标题和描述。适用于从一个步骤分叉出一排操作的场景，如预览中所示。

### 从左到右

`LayoutDirection="LeftToRight"` 会让工作流沿画布从左到右展开：连线从卡片的右侧引出，并进入下一张卡片的左侧；排列时各层会并排放置。

<Demo name="LeftToRight" />

### 连线样式

`EdgeStyle` 将连线绘制为曲线（`Bezier`，即 Arc 的默认样式）、带圆角的直角（`Step`）或直线（`Straight`）。运行的数据会沿其中任何一种连线绘制。

<Demo name="EdgeStyles" />

### 平移与缩放

用户可以拖动画布（或使用中键）进行平移，按住 Ctrl / ⌘ 并滚动滚轮、使用触控板双指捏合或触摸双指缩放来进行缩放，也可以使用 + 和 − 键。`WheelMode` 决定普通滚动的行为：`Pan`（默认）会平移画布，适合填满面板的画布；`ModifierZoom` 则把普通滚动留给页面，适合放在可滚动页面中的画布。`IsPanZoomEnabled="False"` 会关闭以上所有操作，适用于只需适配显示的工作流，例如预览。在空白画布上双击会重新适配整个工作流。

`WorkflowZoomControls` 是一个小巧的缩放条：依次为缩小、当前缩放比例（点击可回到 100%）、放大和适配。将它的 `Target` 指向画布，并放在某个角落上方。在代码中，`ZoomTo`、`ZoomIn`、`ZoomOut`、`FitToContent` 和 `ScrollIntoView` 会移动视图，默认带有短暂的缓出动画，除非你传入 `animate: false`。

<Demo name="PanZoom" />

```xml
<Panel>
    <arc:WorkflowCanvas x:Name="Canvas" AutoFit="Fit" />
    <arc:WorkflowZoomControls Target="{Binding #Canvas}" Margin="12"
                              HorizontalAlignment="Left" VerticalAlignment="Bottom" />
</Panel>
```

### 排布

画布会排布没有 `Position` 的步骤，并记住这一点：当这类工作流增加或减少步骤时，会重新排布，卡片会平滑滑动到新的位置。`ArrangeNodes()` 会以 Arc 的间距（层与层之间 52，分叉步骤下方 72，相邻步骤之间 20）重新排布所有步骤，并把你的代码放置过的步骤也交还给画布。

<Demo name="Arrange" />

### 编辑

`IsEditable="True"` 让用户可以重新排布工作流。拖动步骤即可移动它（选中的步骤会一起移动，松手时吸附到点阵；按住 Alt 则可自由放置），在空白画布上按住 Shift 拖动可选中矩形框所触及的每一个步骤，把步骤拖到靠近边缘的位置则会滚动视图。使用键盘时，Space 选中聚焦的步骤，Ctrl / ⌘ + A 全选，M 开始用方向键移动选中项（Shift 移动五个网格步长，Enter 放置，Escape 取消），Delete 删除选中的步骤和连线。带有 `IsLocked` 的步骤会留在原位且不能删除，但仍可以被选中。

Ctrl / ⌘ + C 复制选中的步骤以及它们之间的连线，Ctrl / ⌘ + X 剪切它们，Ctrl / ⌘ + V 将它们粘贴到复制位置右下方一个网格步长处（如果该位置已移出视野，则粘贴到视图中央），Ctrl / ⌘ + D 则不经过剪贴板，直接创建副本；粘贴的步骤会获得新的 id，并成为当前选中项。在同一应用内粘贴会保留步骤的全部内容，运行状态除外；在不同应用之间，剪贴板以 JSON 形式携带步骤，不包含图标。只读画布同样可以复制。在代码中可使用 `CopySelectionAsync`、`CutSelectionAsync`、`PasteAsync` 和 `DuplicateSelection`。

在任何画布上都可以选择：单击选择一个步骤，Ctrl / ⌘ + 单击切换其选中状态，Shift + 单击加选，单击空白画布则清除选择。读取 `SelectedNodes`，用 `Select` 和 `ClearSelection` 修改它，并通过 `SelectionChanged` 跟踪它。每次编辑完成都会触发一次 `GraphEdited`，附带发生了什么变化（`Move`、`Delete`、`Connect`、`Disconnect`、`Paste`、`Undo` 或 `Redo`）、相关步骤和连线：请在那里保存工作流。

<Demo name="Editing" />

```csharp
Canvas.GraphEdited += (_, e) =>
{
    // Move: the steps are at their new Position. Delete: they are already out of Canvas.Graph.
    foreach (var node in e.Nodes)
    {
        Debug.WriteLine($"{e.Kind} {node.Id} at {node.Position}");
    }
};
```

你移动过的步骤从此就归你管理：当工作流变化或缩减时，画布不再排布它，直到 `ArrangeNodes` 把它交还给画布为止。人在画布上所做的编辑，不会再让画布排布这个工作流；而你的代码为画布已排布过的工作流添加的连线，则会。

### 连接步骤

在可编辑的画布上，卡片在你指向它、选中它或让它获得焦点时会显示其端口：顶部的输入端口，以及底部的输出端口（工作流横向展开时则在左侧和右侧）。从一个输出端口拖到另一个步骤上，或拖到其某个输入端口附近，即可将两者相连。拖动时，无法连接的步骤会淡化，指向其中一个步骤时会告诉你原因。把连线的一端从输入端口拖开即可将其移到另一个步骤；松手于空白画布上则会删除这条连线。单击连线即可选中它（Ctrl / ⌘ + 单击切换选中状态，Shift + 单击加选）；Delete 删除它。使用键盘时，C 从聚焦的步骤开始连接：若它有多个输出，用方向键选择一个并按 Enter；然后用方向键选择一个步骤，再按 Enter 完成连接。

没有端口的步骤各自拥有一个输入和一个输出（此时连线的端口为 null）。在工作流分支的地方为步骤设置带名称的端口，例如条件判断的 "Yes" 和 "No"；它们的标签始终显示在端口旁边。为没有上游的触发器关闭 `HasDefaultInput`。

```csharp
var yes = new WorkflowPort(WorkflowPortDirection.Output, "Yes");
var no = new WorkflowPort(WorkflowPortDirection.Output, "No");
check.Outputs.Add(yes);
check.Outputs.Add(no);
graph.Connect(check, yes, approve, null);
graph.Connect(check, no, refund, null);
```

每次连接建立前都会经过检查。以下情况会被拒绝：将步骤连到它自己、终点是输出端口、重复已有的连接、超过端口的 `MaxConnections`、连接的端口 `DataType` 不同，或形成环路（除非开启了 `AllowCycles`）。`CanConnect` 在代码中执行同样的检查，并告诉你原因。若要添加自己的规则，请在 `ConnectionRequested` 中设置 `Cancel`，并设置用户会听到的 `Reason`。松手于空白画布上的连线会引发 `ConnectionDroppedOnCanvas`，并附带画布单位下的位置：在那里提供步骤列表，并添加所选的步骤。

<Demo name="Connecting" />

```csharp
Canvas.ConnectionRequested += (_, e) =>
{
    if (e.SourcePort == yes && e.TargetNode == refund)
    {
        e.Cancel = true;
        e.Reason = "Amounts over $500 need a manager's approval first";
    }
};
Canvas.ConnectionDroppedOnCanvas += (_, e) =>
{
    var step = new WorkflowNode("notify", "Notify the team") { Position = new Point(e.Position.X - 150, e.Position.Y) };
    // One edit: a single undo takes the step and its connection back.
    using (graph.History.BeginGroup())
    {
        graph.Nodes.Add(step);
        graph.Connect(e.SourceNode, e.SourcePort, step, null);
    }
};
```

### 撤销与重做

`WorkflowGraph.History` 保留人在画布上所做的最近 100 次编辑：移动、删除、连接、移动或删除连线，以及粘贴。Ctrl / ⌘ + Z 撤销，Ctrl / ⌘ + Shift + Z 或 Ctrl + Y 重做；画布的 `Undo()` 和 `Redo()` 为你的按钮提供相同的操作，被移动的卡片会平滑滑回原位。跟随 `History.Changed`，即可根据 `CanUndo` 和 `CanRedo` 更新这些按钮。

只有在 `History.BeginGroup()` 之内，你的代码对工作流所做的更改才会被记录：在分组被释放之前，添加、删除或移动的所有内容合为一次编辑，如上方的处理程序所示。如果你的应用自行保存撤销历史，请关闭 `History.IsEnabled`，改为跟随 `GraphEdited`。

### 工具栏命令

画布为工具栏按钮和菜单项提供了命令：`UndoCommand`、`RedoCommand`、`DeleteSelectionCommand`、`CopyCommand`、`CutCommand`、`PasteCommand`、`DuplicateCommand`、`ZoomInCommand`、`ZoomOutCommand` 和 `FitCommand`。每个命令只在确实能起作用时才可执行，例如有可撤销的编辑时才能撤销、有选中内容时才能删除，因此绑定了命令的按钮会自动启用或禁用。

```xml
<arc:Button Content="Undo" Command="{Binding #Canvas.UndoCommand}" />
<arc:Button Content="Delete" Command="{Binding #Canvas.DeleteSelectionCommand}" />
```

### 自定义卡片内容

`NodeTemplates` 会替换与之匹配的卡片的内容：`WorkflowNodeTemplate` 以 XAML 编写，针对一种 `Kind` 的步骤，其数据为 `WorkflowNode`；对某个步骤而言，第一个匹配的模板生效。卡片保留其边框、运行颜色、选中状态、焦点环和端口；没有模板匹配的步骤仍使用默认卡片。未设置 `Size` 时，卡片的高度与其内容一致。卡片中的控件（例如输入框或开关）保留自己的点击和按键，因此在其中输入从不会移动或连接该步骤。

<Demo name="CustomNodes" />

```xml
<arc:WorkflowCanvas x:Name="Canvas" IsEditable="True">
    <arc:WorkflowCanvas.NodeTemplates>
        <arc:WorkflowNodeTemplate Kind="email" x:DataType="arc:WorkflowNode">
            <StackPanel Spacing="10">
                <TextBlock Text="{Binding Title}" FontWeight="{arc:ArcDesignTokenResource WeightMedium}" />
                <arc:Input Text="{Binding Description, Mode=TwoWay}" />
                <arc:Switch Content="Attach the invoice" IsChecked="True" />
            </StackPanel>
        </arc:WorkflowNodeTemplate>
    </arc:WorkflowCanvas.NodeTemplates>
</arc:WorkflowCanvas>
```

在代码中，可以添加任意 `IDataTemplate`（例如 `FuncDataTemplate<WorkflowNode>`）。单独使用的卡片通过 `ContentTemplate` 接收其模板。

### 大型工作流

画布为数百个步骤而设计：无论显示多少卡片、阴影和点阵，绘制成本都保持较低，因此包含 500 个步骤和 800 条连线的工作流在桌面端平移、缩放和拖动时依然流畅。`WorkflowMinimap` 以缩小的形式显示整个工作流，并用一个框标出当前可见的部分：按下即可跳转到该处，拖动则可平移。将它的 `Target` 指向画布，并像缩放条一样放在某个角落上方。

<Demo name="LargeGraph" />

```xml
<Panel>
    <arc:WorkflowCanvas x:Name="Canvas" MinZoom="0.05" />
    <arc:WorkflowMinimap Target="{Binding #Canvas}" Margin="12"
                         HorizontalAlignment="Right" VerticalAlignment="Bottom" />
</Panel>
```

将 `MinZoom` 调低（默认为 0.25），以便适配时能显示大型工作流的全部内容。

### 窄画布

低于 `NarrowBelowWidth`（默认 520，与 Arc 一致）时，画布会切换为 Arc 的手机布局：单行卡片变为 344 × 52，双行卡片变为 108 × 72；由画布排布的工作流会以更紧凑的间距重新排布（分别为 26、36 和 10），因此能以实际尺寸显示，而不是缩小。由你的代码放置的步骤保持其位置。紧凑步骤的标题请控制在一两个词以内，因为手机上的空间很有限。

<Demo name="Narrow" />

### 适配与尺寸

`AutoFit` 决定工作流如何适配画布。`ShrinkToFit`（默认）以实际尺寸居中显示，放不下时会像截图一样整体缩小；`Fit` 会将其放大到尽可能大，但不超过 `MaxZoom`；`None` 则保留你通过 `Zoom` 和 `Offset` 设置的视图。平移、缩放，或自行设置 `Zoom` 或 `Offset` 后，在调用 `FitToContent` 之前不再自动适配。在高度不受限的容器中（例如可滚动的页面），画布的高度与适配后的工作流一样高。`Padding`（默认为 24）使工作流与边缘保持距离。

### 放置步骤

为步骤设置 `Position` 即可自行放置它（即卡片在画布上的左上角）。新步骤如果没有位置，会按层排在你的代码已放置的步骤之后。

### 内容准则

- 用步骤所做的事情为其命名，简短，采用句子大小写："Find customer account"，而不是 "HubSpot lookup step"。
- 在 `Description` 中写明步骤处理的对象，在 `Result` 中写明它发现的内容；两者都保持在一行内。
- 错误要具体且简短："Health check failed in eu-west"，而不是 "Error"。

## API 参考

<!-- api: WorkflowCanvas -->

<!-- api: WorkflowNodeCard -->

<!-- api: WorkflowGraph -->

<!-- api: WorkflowNode -->

<!-- api: WorkflowEdge -->

<!-- api: WorkflowPort -->

<!-- api: WorkflowHistory -->

<!-- api: WorkflowZoomControls -->

<!-- api: WorkflowMinimap -->

<!-- api: WorkflowNodeTemplate -->

<!-- api: WorkflowNodeEventArgs -->

<!-- api: WorkflowEditEventArgs -->

<!-- api: WorkflowSelectionChangedEventArgs -->

<!-- api: WorkflowConnectionRequestedEventArgs -->

<!-- api: WorkflowConnectionDroppedEventArgs -->

## 无障碍

- 画布默认名为 "Workflow, 6 steps"；如果你用 `AutomationProperties.Name` 为它命名，则使用你设置的名称。
- 每张卡片的名称依次给出标题、类型、文字形式的状态、耗时，以及标题下方的那一行，例如 "Find customer account, Done, 118 ms, Enterprise, 212 seats"。状态从不只靠颜色传达：每种状态都有自己的标记。
- 画布被视为一个分组，每张卡片则是其中的一项。连线、端口、点阵和标记都是装饰性的，对辅助技术隐藏；键盘的连接模式可以到达指针能够建立的每一条连接。小地图只是按键功能的指针快捷方式，因此它不获得焦点，屏幕阅读器也会跳过它。
- 失败的步骤和一次运行的结束会以礼貌方式播报："Create onboarding task failed: Task service timed out"、"Run finished: 4 done, 1 failed, 1 skipped"。编辑操作同样会被播报："Moved 2 steps"、"Deleted 1 step"、"Connected Amount over $500? to Manager approval"、"Can't connect: This would create a loop"、"Pasted 2 steps"、"Undone"，键盘的移动模式和连接模式开始时也会播报其用法。
- 选中的卡片会在其名称后加上 "selected"，并显示强调色边框和柔和的光晕，而不只是颜色变化。
- 画布的帮助文字列出了下面的按键。

### 键盘

| 按键 | 操作 |
| --- | --- |
| Tab | 进入画布，移到上次获得焦点的步骤，再离开画布；卡片合为一个 Tab 停靠点。 |
| 方向键 | 移到该方向上最近的步骤。 |
| Alt + 方向键 | 沿连接移动：顺着工作流移到下一个步骤，逆着移到上一个步骤，横向移到另一个与它共享同一上一步骤的相邻步骤。 |
| Home / End | 移到第一个或最后一个步骤。 |
| Enter | 打开步骤（`NodeActivated`）。 |
| + / − | 以焦点所在步骤为中心放大或缩小。 |
| Shift + 1 | 适配整个工作流。 |
| Ctrl / ⌘ + 0 | 缩放到 100%。 |
| Shift + F10，菜单键 | 请求打开上下文菜单（`ContextRequested`，其来源为卡片）。 |
| Space | 选中或取消选中聚焦的步骤。 |
| Ctrl / ⌘ + A | 选中所有步骤和连线。 |
| Escape | 取消移动或连接，或清除选择。 |
| M | 在可编辑画布上：用方向键移动选中项（或聚焦的步骤）；Shift 移动五个网格步长，Enter 放置，Escape 取消。 |
| Delete / Backspace | 在可编辑画布上：删除选中的步骤和连线（或聚焦的步骤）；锁定的步骤保留。 |
| C | 在可编辑画布上：连接聚焦的步骤。有多个输出时，用方向键选择一个并按 Enter 继续；然后用方向键按阅读顺序选择一个步骤，按 Enter 连接，按 Escape 取消。 |
| Ctrl / ⌘ + Z | 在可编辑画布上：撤销上一次编辑。 |
| Ctrl / ⌘ + Shift + Z, Ctrl + Y | 在可编辑画布上：重做。 |
| Ctrl / ⌘ + C | 复制选中的步骤（或聚焦的步骤）以及它们之间的连线。 |
| Ctrl / ⌘ + X, Ctrl / ⌘ + V | 在可编辑画布上：剪切它们，并粘贴。 |
| Ctrl / ⌘ + D | 在可编辑画布上：为选中的步骤（或聚焦的步骤）创建副本。 |

在卡片中的控件（自定义内容中的输入框）里输入的按键，归该控件所有。

通过键盘到达的步骤会被滚动到视图中。

## 动效

- 运行中卡片的边框和填充会在标准时长内渐变为强调色；跳过的卡片会淡化至一半的不透明度。
- 状态变化时，卡片末端的标记会被替换：新标记从 0.6 倍大小放大，旧标记同时收缩消失，采用轻快的弹簧。
- 运行中的圆环每 0.7 秒转一圈，且仅在其显示于屏幕上时转动。
- 当一个步骤完成且下一个步骤已开始时，数据会在 400ms 内以强调色沿连线绘制，采用缓入缓出曲线；下一张卡片要等数据到达后才会显示为运行中。
- 适配、使用按钮或按键缩放以及将步骤带入视图，会以 Arc 的标准缓出曲线在 360ms 内移动视图；拖动、滚轮和双指捏合则紧随手指，不做缓动。
- 排布会以同样的方式把卡片滑动到新的位置，连线随之移动。
- 被拖动的卡片紧随指针移动，不做缓动，并浮在其他卡片之上；松手时以轻快的弹簧吸附到点阵上；取消的移动则滑回起点。
- 正在绘制的连线紧随指针移动，不做缓动，并跳到 24px 内最近的输入端口，该端口会放大并采用强调色；无法连接的步骤则在标准时长内淡化至 40% 的不透明度。撤销和重做会让被移动的卡片滑回原位。
- 减少动态效果：不做颜色渐变，标记以短暂的淡入淡出切换，圆环保持静止，数据直接显示为已绘制完成，视图和卡片直接跳到目的位置。工作流处于静止状态时，不会有任何循环动画。

## 相关组件

- `Stepper`：人按顺序完成的简短直线式步骤。
- `AgentPlan`：可编辑清单形式的智能体计划。
- `AgentTimeline`：在同一时间轴上重放多智能体运行。
- `DataFlow`：展示数据从来源流向目的地的插图。
