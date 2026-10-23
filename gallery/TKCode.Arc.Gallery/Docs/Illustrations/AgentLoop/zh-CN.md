---
title: Agent loop
summary: 智能体收集上下文、调用工具并检查结果，沿循环反复进行，直到任务完成。
source-hash: dc59ab4f64b0
---

<Demo name="SampleRun" />

## 何时使用

`AgentLoop` 说明一个使用工具的 AI 智能体如何工作：它收集上下文，用一个工具执行动作，检查结果，并在检查通过之前沿循环再走一圈。一个明亮的光点带着柔和的拖尾，绕着环形轨道经过围绕模型的三个站点。上下文块加入环旁的堆栈，工具调用从环中落下并得出结果，检查则要么通过，要么让循环再走一圈。运行结束时，环被完整画出，并附上摘要。它是对你所编写的一次运行的插图，而不是某个真实智能体的日志，其中也不会调用任何服务。

- **智能体功能的产品页与文档页**：介绍助手如何修复失败的构建或分拣支持工单的页面。卡片展示了智能体为何要经过多轮，而不是一次给出答案。
- **讲解失败与重试**：第一次检查失败并附带重试说明，或者工具调用超时并在同一步中缩小范围，这说明智能体是通过重新查看来恢复，而不是靠猜测。
- **内部智能体平台的入门材料**：面向即将编写第一个工具的工程师的简短插图，放在描述平台如何将工具结果回馈给模型的文字旁边。
- **落地页与演讲**：一个自包含、无障碍的动画，滚动进入视野时播放一次，并停在一幅静态摘要上，适合用作截图。
- **已完成运行的回顾**：在减弱动效下，或将演示过程直接跳到结尾时，卡片就是智能体积累的上下文、最后一次工具调用与最后一次检查的摘要。

## 何时不用

- **流式呈现真实智能体的步骤**：使用 `ChatThread`，或使用展示智能体实际消息与工具输出的日志视图。`AgentLoop` 播放的是你描述的运行过程，播放期间它不接受任何事件。
- **对话记录中的单次工具调用**：将该调用显示为聊天或日志中的一行。卡片讲的是循环，而只有一次调用且检查一次通过的运行说明不了太多问题。
- **搜索大量候选思路**：使用 `ReasoningTree`，它会对分支评分并剪枝。智能体循环是一条不断重复的路径。
- **显示上下文窗口的占用程度**：这里的 token 总数只统计故事中的块，它不是预算，也不是仪表，更不会对某个上限发出警告。
- **实时作业的状态**：使用 `Alert`、`Badge` 或展示真实状态的表格。静态插图无法报告生产环境中的运行已经失败。

## 用法

卡片在未提供任何迭代时，会播放一次内置的示例运行：三轮虚构的分拣，使用虚构的模型与工具。卡片首次大部分进入视野时开始播放，并在完成状态时停止。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:AgentLoop />
</UserControl>
```

`AgentLoop` 派生自 `Explainer`，后者是 Special 包中动画 AI 讲解控件的基类，而 `Explainer` 派生自 Avalonia 的 `TemplatedControl`。基类提供故事时钟、`IsPlaying`、`Speed`、`AutoPlay`、`IsLooping`、播放、单步与重新开始按钮、礼貌级别的实时区域以及减弱动效下的开场状态。`AgentLoop` 则增加了迭代、环及其站点、泳道和上下文堆栈。请在 `UseArc()` 旁调用 `UseArcSpecial()` 以注册该包。

卡片中的数字（token、时长、结果）仅作示意。当页面涉及真实产品时，请在卡片周围的文字中说明这一点。

### 结构

1. **标题**：左侧为 `Title`（默认为 "Agent loop"），右侧为状态：一个图标加上就绪、运行中、已暂停、已完成或已停止。其下的说明文字保留两行的空间：就绪或运行中时显示 `Task`，运行结束后显示 `Summary`。
2. **环**：一条 1.5 px 的轨道，上面有三个顺时针指向的小箭头。半径最大为 94 px；在窄列中会缩小，以便下方的标签能与其节点并排放下。
3. **站点**：顶部为 Gather context（收集上下文），右下为 Take action（采取行动），左下为 Verify（验证），每个站点都是带标签和图标的节点。空闲站点显示为弱化色，活动站点以强调色填充，并带有细环和名称上的轻柔闪烁；已访问的站点保留强调色轮廓。
4. **光点与拖尾**：一个明亮的圆点在站点之间缓动移动，并拖出一条渐变拖尾。拖尾随每一趟行程伸长或缩短，光点落地时拖尾消失。
5. **中心**：模型的标志与名称（`Model`），其下一行显示：就绪、循环计数，或结束语（"Done in 3 loops"，或 "Stopped after 2 loops"）。
6. **连线与泳道**：从每个下方站点向其泳道引出一条短线。工具调用落在 Take action 下方的泳道，检查落在 Verify 下方的泳道，每项都以标签块的形式呈现。在第一个标签块出现之前，泳道中由一个虚线占位符填充。
7. **上下文堆栈**：标题加上正在累计的 token 总数，以及每个循环一个的槽位。每个块显示一个标志、一个标签、它的 token 数和一行详细信息。在第一个块加入之前，堆栈中由一个虚线占位符填充。
8. **页脚**：左侧为播放或暂停、Next step 和 Restart，右侧为 0.5×、1× 与 2× 的速度控件。`AreControlsVisible` 会隐藏整行。

### 故事与时间

运行过程是由迭代一次性构建的时间线，因此暂停、单步与重播都是精确的。以 1× 速度计算，每个循环耗时如下：

| 阶段 | 时间 |
|---|---|
| 在 Gather context（区块在 0.1 s 时加入） | 0.85 s |
| 前往 Take action | 0.7 s |
| 在 Take action，一次调用 | 1.35 s |
| 同一循环中每多一次调用 | + 1.15 s |
| 前往 Verify | 0.5 s |
| 在 Verify（检查在 0.1 s 时落下） | 0.95 s |
| 返回 Gather context | 0.7 s |

因此一个循环耗时 3.7 s，再加上它在 Take action 的时间，并且运行在 Ready 之后 0.22 s 开始。示例运行的调用次数分别为一次、两次和两次，所以总耗时为 0.22 + 5.05 + 6.2 + 6.2 = 17.67 秒。调用在光点到达后 0.1 s 落下，显示 "Calling" 及其工具 0.75 s，然后变为结果；下一次调用在同一泳道中接替它的位置。运行在最后一个循环结束、光点回到 Gather context 时结束。如果最后一次检查通过，环被画出，状态显示为 Done；如果失败，环保持中性，状态显示为 Stopped。

token 总数是目前已收集块的总和。低于 1,000 的数字显示为整数，更大的数字保留一位小数并带上 K，因此示例运行结束时 1,400 + 5,800 + 8,600 显示为 15.8K。

### 你自己的运行

将循环作为内容交给卡片。每个 `AgentLoopIteration` 包含一个 `Gather` 块、一到三个作为其内容的调用，以及一个 `Verify` 检查。标志是 24 乘 24 的描边图标，例如 `ArcIcons` 的成员。

<Demo name="BuildFix" />

```xml
<arc:AgentLoop Task="Fix the failing nightly build and tell #builds."
               Summary="The build cache key had changed. Clearing it fixed the build.">
    <arc:AgentLoopIteration>
        <arc:AgentLoopIteration.Gather>
            <arc:AgentLoopContext Label="Build log" Detail="Stage 3 of 7 failed" Tokens="2900"
                                  Mark="{x:Static arc:ArcIcons.Terminal}" />
        </arc:AgentLoopIteration.Gather>
        <arc:AgentLoopCall Tool="CI" Action="Read pipeline run" Result="Cache key changed"
                           Mark="{x:Static arc:ArcIcons.GitBranch}" />
        <arc:AgentLoopIteration.Verify>
            <arc:AgentLoopCheck Passed="False" Note="Fix not applied yet" />
        </arc:AgentLoopIteration.Verify>
    </arc:AgentLoopIteration>
    <!-- More loops; the last one should pass its check. -->
</arc:AgentLoop>
```

`Task` 是运行播放期间的说明文字。`Summary` 是运行结束时的说明文字；若不设置，则显示循环与工具调用的计数。每个循环从一开始就在上下文堆栈中预留一个槽位，因此卡片在播放时不会变高。两到四轮的效果最好。

在调用上设置 `Status="Error"` 即可显示失败的调用：旋转指示器变为叉号，结果显示为失败原因，屏幕阅读器会读出 "failed"。随后在同一循环中再加一个调用，即可展示智能体再次尝试的过程。

### 运行中止

当最后一个循环的检查失败时，运行以中止结束：状态显示为 Stopped，环保持中性，中心处显示运行了多少个循环。若未设置 `Summary`，说明文字会显示运行在多少个循环和调用之后停止。

<Demo name="StoppedRun" />

### 播放、单步与速度

`IsPlaying` 与 `Speed` 是双向的：页脚会写入它们，应用也可以绑定它们。`Speed` 缩放故事时钟（0 表示完全暂停，最大为 4）；页脚提供 0.5×、1× 和 2× 三档。标签块落下或环被画出等过渡效果，无论速度如何都保持自己的节奏。`Play()`、`Pause()`、`StepForward()` 和 `Restart()` 可在代码中完成相同的操作。

Next step 会播放到下一个节拍并停在那里：新的上下文块、每个工具结果、每次检查以及结束。它以 2.5 倍速播放（至少 2.5×），因此单步很快，但仍能展示行程。Restart 回到 Ready 并开始播放。每当光点到达一个站点时，`StationChanged` 触发一次，参数包括该站点和从零开始计数的循环序号；运行到结束时，`Finished` 触发。

<Demo name="EventLog" />

```csharp
loop.StationChanged += (_, e) => log.Add($"Loop {e.Loop + 1}: {e.Station}");
loop.Finished += (_, _) => status.Text = loop.Phase == AgentLoopPhase.Done ? "Done" : "Stopped";
```

`Phase` 是状态所显示的阶段：Ready、Running、Paused、Done 或 Stopped，对应就绪、运行中、已暂停、已完成和已停止。`IsLooping` 会在运行结束四秒后重新播放。

### 窄容器

布局跟随卡片自身的宽度，而不是窗口的宽度。宽度为 640 px 的容器中，上下文堆栈与环并排成一列，其下是两条泳道。更窄时，堆栈移到环的下方，宽度为 460 px 起每行两个块，更窄则每行一个。在环的列宽低于 360 px 时，站点标签不再显示图标并缩小，两条泳道合并为一条，只显示最新的工具调用或检查。在最窄的卡片上，下方两个站点会进一步下移，以便标签能放得下，速度控件则在按钮下方换行。

<Demo name="Narrow" />

### 减弱动效

在减弱动效下，卡片直接以完成的运行打开：环已画出，堆栈中的每个块都在，最后的标签块与摘要也都在。重新播放与 Next step 仍然可用。光点在站点之间直接跳转，没有拖尾，所有变化都原地生效，不带下落或淡入。由于卡片打开时运行会直接播放到结尾，`StationChanged` 会在那一刻为所有站点一起触发，随后触发 `Finished`。

<Demo name="ReducedMotion" />

### 从数据驱动

当循环来自视图模型时，绑定 `IterationsSource`。替换为新集合，或修改一个可观察集合，都会重建故事。卡片在被看到之前是静默重建，因此自动播放在首次可见时仍会开始；之后则会从头开始。

```csharp
public sealed class AgentDemoViewModel
{
    public ObservableCollection<AgentLoopIteration> Loops { get; } =
    [
        new(new AgentLoopContext("Support ticket", "Refund not received", 1200, ArcIcons.Inbox),
            [new AgentLoopCall("Billing", "Find the payment", "Refund issued on May 2", mark: ArcIcons.CreditCard)],
            new AgentLoopCheck(true, "Customer told"))
    ];
}
```

```xml
<arc:AgentLoop IterationsSource="{Binding Loops}"
               Task="Answer a refund question."
               Speed="{Binding Speed}" />
```

`IterationsSource` 为空时，环处于静止状态且控件关闭。`Labels` 接受一个 `AgentLoopLabels` 对象，其中包含卡片显示或播报的每一个文字，包括站点名称；只需设置你需要的部分。

### 内容准则

让运行中的每个元素都能放在一行内：上下文标签为一到三个词，详细信息少于约 30 个字符，调用动作为两到四个词，结果少于约 30 个字符。请用你的产品中真实存在的工具名称来命名，并使用通用图标，而不是品牌标志。

| 避免 | 改为 |
|---|---|
| Context | Build log |
| Retrieved the complete build log for analysis | Stage 3 of 7 failed |
| Tool call 1 | Read pipeline run |
| The tool returned a successful response | Cache key changed |
| Verification failed | Fix not applied yet |
| The agent has completed the task successfully | The cache key had changed. Clearing it fixed the build. |

站点名称保持在两个词左右；过长的名称会在站点旁被截断。任务写成一句话；摘要可以写两行。

### 最佳实践

**建议**

- 让最后一个循环的检查通过，让更早的循环以带重试说明的方式失败。失败的检查正是解释智能体为何要循环的关键。
- 展示一次失败的工具调用，随后在同一循环中接一次范围更小的调用，因为在一个步骤内恢复，是这张图的第二个要点。
- 将运行控制在两到四个循环。循环越多，卡片越高，故事也越长，读者不会看完。
- 在文案中说明该运行仅为示意，并让其中的工具与你的产品真正拥有的工具相匹配。

**避免**

- 把这些数字当作测量结果。token 与时长只是用来讲故事，并非从模型中读取。
- 在标签块中使用真实服务的品牌标志。请使用通用图标，并在文字中写出工具名称。
- 通过计时器替换 `IterationsSource`。每次变化都会让运行从头开始。
- 同一屏幕上有多张正在播放的卡片。每张卡片在播放时都会逐帧重绘它的环。

### 常见问题

**运行需要多长时间？** 0.22 s 加每个循环 3.7 s，再加上在 Take action 的 1.35 s，每多一次调用再加 1.15 s。再除以速度即可。

**单步为什么停在这里？** 单步会在读者能够说出名称的每个节拍处停下：一个块加入、每个结果、每次检查以及结束。一次开始的调用不算停顿，因为它的结果随后就到。

**卡片能在一个循环中显示三个以上的调用吗？** 不能。第三个之后的调用会被忽略，因为泳道每次只显示一个标签块，过长的步骤会掩盖故事。请把工作拆分到多个循环中。

**没有调用的循环会怎样？** 光点仍会在 Take action 停留一次调用的时长，但泳道中不会落下任何东西。

**如何隐藏模型？** 将 `Model` 设为 null。此时中心只显示循环计数。

## API 参考

<!-- api: AgentLoop -->

<!-- api: AgentLoopIteration -->

<!-- api: AgentLoopContext -->

<!-- api: AgentLoopCall -->

<!-- api: AgentLoopCheck -->

<!-- api: AgentLoopLabels -->

<!-- api: AgentLoopStationChangedEventArgs -->

枚举如下：

| 类型 | 取值 |
|---|---|
| `AgentLoopStation` | `Gather`、`Act`、`Verify` |
| `AgentLoopCallStatus` | `Success`（默认）、`Error` |
| `AgentLoopPhase` | `Ready`、`Running`、`Paused`、`Done`、`Stopped` |

`AgentLoopDefaults` 保存示例运行（`CreateIterations()`、`CreateModel()`、`Task`、`Summary`），因此页面可以从它开始，只修改其中一个循环。

## 无障碍

- 卡片是一个以 `Title` 命名的分组。环的绘制部分（轨道、箭头、连线、光点与拖尾）对辅助技术隐藏；站点名称、中心、标签块与上下文块都是真正的文字。
- 上下文堆栈是一个名为 "Context blocks" 的列表，按块被收集的顺序朗读。块的 token 数之后会读出单词 "tokens"。
- 结果会先读出工具名称（"Tracker returned: …"），失败的调用会读出 "failed"。检查会显示一个图标和文字 "Check passed" 或 "Retry"，因此从不只依赖颜色。
- 状态用图标和文字配对表示每种状态：就绪、运行中、已暂停、已完成和已停止。
- 礼貌级别的实时区域会在你单步或暂停时播报每个节拍（一个块加入、一次调用进行中、一个结果、一次检查），并在结束时播报摘要。卡片自行播放时它保持安静。
- 单步到达结尾时，Next step 会关闭，键盘焦点移到 Restart，而不是落回页面。
- 键盘焦点在按钮和速度控件周围显示 2 px 的焦点环；指针焦点则不显示。

### 键盘

| 按键 | 操作 |
|---|---|
| `Tab` / `Shift+Tab` | 在播放或暂停、Next step、Restart 与速度控件之间移动 |
| 播放上的 `Enter` / `Space` | 播放或暂停；运行结束后则重新播放 |
| Next step 上的 `Enter` / `Space` | 播放到下一个节拍并停在那里 |
| Restart 上的 `Enter` / `Space` | 从 Ready 重新开始并播放 |
| 速度控件上的 `Left` / `Right`（或 `Up` / `Down`） | 在 0.5×、1× 与 2× 之间移动，并循环 |
| 速度控件上的 `Home` / `End` | 选择 0.5× 或 2× |

## 动效

- 光点在站点之间缓动（1× 时为 0.7 s、0.5 s 与 0.7 s），并沿环拖出一条渐变拖尾。拖尾在行程中点最长，光点落地时消失，因此它永远不会停在原地拖着尾巴。
- 活动站点被填充，细环在其节点周围定位，其名称带有轻柔的闪烁。
- 工具调用从环中落下：Take action 下方的连线在 180 ms 内向下画出，标签块在末端以 6 px 的下落与轻微缩放落地，使用无回弹的弹簧。它的旋转指示器交叉淡化为对号或叉号。第二次调用在第一次于 80 ms 内淡出后接替其位置。
- 上下文块以短暂下落的方式淡入，同时 token 总数在 500 ms 内计数增长。
- 循环计数器滚动其数字；状态、说明文字与中心行交叉淡化。
- 结束时，环在 760 ms 内顺时针画出，光点淡出。中止的运行使环保持中性。

| 变化 | 完整 | 减弱 | 关闭 |
|---|---|---|---|
| 开场 | 首次看到时播放 | 以完成的运行打开 | 以完成的运行打开 |
| 光点在站点之间 | 沿环缓动并带有拖尾 | 在每趟行程末尾跳转 | 在每趟行程末尾跳转 |
| 标签块与块 | 以弹簧下落，被替换时淡出 | 原地出现 | 原地出现 |
| token 总数 | 在 500 ms 内计数增长 | 直接跳到总数 | 直接跳到总数 |
| 站点颜色与标签块边框 | 在 240 ms 内变化（新块为 480 ms） | 立即变化 | 立即变化 |
| 完成时的环 | 在 760 ms 内画出 | 直接显示为已画出 | 直接显示为已画出 |

## 相关组件

- `ReasoningTree`：用于在候选思路之间搜索，而不是工具调用的循环。
- `PromptCache`：说明共享的提示前缀如何在多个请求之间复用。
- `SpeculativeDecoding` 与 `MixtureOfExperts`：说明模型如何生成它的 token。

| 组件 | 何时选用 |
|---|---|
| `ChatThread` | 你展示与智能体的真实对话，逐条消息呈现 |
| `Timeline` | 你按顺序、带时间地列出真实运行做了什么 |
| `Alert` | 你以文字报告真实作业的状态 |
| `ReasoningTree` | 故事讲的是在候选思路之间做选择 |
| `DataFlow` | 故事讲的是数据流经一条管线 |
