---
title: 桑基流向图
summary: 用条带表示各阶段之间的流量，粒子沿条带漂移：悬停或聚焦节点、条带，即可追踪其份额贯穿整个旅程。
source-hash: 02cdef11fcdd
---

<Demo name="TrialJourney" />

## 何时使用

`SankeyFlow` 展示一个数量在各阶段流转时如何拆分与汇合。每个节点是途中的一站，每条条带是一股流量，条带的粗细就是它的值，读者一眼就能看出大部分流向哪里、又在哪里流失。

- **产品旅程**：从试用来自的渠道，经过上手引导，到最终选择的方案。追踪一个渠道，就能看到它实际带来了哪些方案。
- **预算**：从收入来源，经过各部门，到钱用在了哪里。
- **能源或物料平衡**：在公用事业或运营报告中，从供应经过储存到使用。
- **工单流转**：从工单的来源，经过接手的团队，到它的结案方式。

## 何时不用

- **只会逐步收窄的单列阶段**，例如没有分支的注册漏斗：使用 `BarChart`，它能精确比较每一步的大小。
- **没有阶段的整体与部分**：使用 `DonutChart` 或 `Treemap`。
- **随时间的变化**：使用 `LineChart` 或 `Streamgraph`。流向图只展示一个时期。
- **重点在于图的结构**，只有步骤和分支而没有数量：使用 `WorkflowCanvas`。
- **超过几百股流量**：条带会细到无法阅读。先把小流量归并到一个“其他”节点。

## 用法

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:SankeyFlow Label="Signups by source" Unit=" people" TotalLabel="all signups">
        <arc:SankeyFlow.Nodes>
            <arc:SankeyFlowNode Id="web" Label="Website" />
            <arc:SankeyFlowNode Id="app" Label="Mobile app" />
            <arc:SankeyFlowNode Id="kept" Label="Still active" />
            <arc:SankeyFlowNode Id="left" Label="Left" />
        </arc:SankeyFlow.Nodes>
        <arc:SankeyFlow.Links>
            <arc:SankeyFlowLink Source="web" Target="kept" Value="420" />
            <arc:SankeyFlowLink Source="web" Target="left" Value="180" />
            <arc:SankeyFlowLink Source="app" Target="kept" Value="260" />
            <arc:SankeyFlowLink Source="app" Target="left" Value="40" />
        </arc:SankeyFlow.Links>
    </arc:SankeyFlow>
</UserControl>
```

`SankeyFlow` 派生自 Avalonia 的 `Control`，由自身绘制。可以像上面这样内联添加节点和连接，也可以在代码或绑定中把任意列表赋给 `Nodes` 和 `Links`。替换列表或修改可观察列表都会重绘图表。

### 结构

1. **阶段名称**（`Columns`）：绘图区上方一行，竖向布局时在每一行上方。
2. **节点**：条形的长度取流入与流出中较大的一个。图表的所有列共用一个数值比例，因此条形和条带可以跨整张图比较。
3. **条带**：每对节点一条。条带两端的粗细都等于它的值，颜色从来源阶段渐变到目标阶段。
4. **粒子**：沿每条条带朝流动方向漂移的小点。
5. **标签**：条形旁边的节点名称和数值。相邻节点过近时，先省略数值行，再省略标签；提示框里仍然可以读到。
6. **粒子开关**：在绘图区下方靠右。
7. **提示框**：被追踪的内容、它的值，以及占总量的份额。

### 列

没有 `Column` 的节点放在所有流入它的节点中最靠后的那个的下一列，因此没有流入的节点从第一列开始。设置 `Column` 可以自行放置节点，例如让一个结束状态与其他结果对齐。`Columns` 从第一列起命名各阶段。

```xml
<arc:SankeyFlow Label="Home energy">
    <arc:SankeyFlow.Nodes>
        <arc:SankeyFlowNode Id="battery" Label="Home battery" Column="1" />
    </arc:SankeyFlow.Nodes>
</arc:SankeyFlow>
```

### 连接与环

连接的 `Source` 和 `Target` 是节点 id。找不到对应节点的连接端会得到一个以 id 命名的节点，所以简单的图只写连接就够了。相同两个节点之间的连接会累加。自环和非正数的值会被忽略。

环不会让图表出错：闭合环的那条连接会弯回到它的目标。追踪沿这条连接走一圈后停止。`Column` 也可能让连接反向，绘制方式相同。

### 格式与总量

`Unit` 跟在每个值后面，`ValueFormatter` 替换整个格式。`TotalLabel` 命名份额行里的整体，例如把“42% of the total”写成“42% of the city budget”。总量是进入旅程的量：所有没有流入的节点的流出之和。

<Demo name="CityBudget" />

### 追踪

悬停在节点或条带上、用键盘聚焦或轻触它。它的份额会以强调色点亮前后每个阶段，其他内容变暗；每个节点按到达它的相同比例继续传递。色带的粗细就是份额，所以只供给后续阶段一部分的节点，也只覆盖那个阶段的相应部分。

### 粒子

粒子除了方向和大致分量外不携带数据：条带越重，粒子越多，最多 34 个。用 `IsParticlesEnabled` 关闭粒子，它是双向属性并会触发 `ParticlesChanged`。开关写入的是同一个属性。把 `IsParticleToggleVisible` 设为 `False` 可移除开关，用 `ParticleToggleLabel` 翻译开关文字。

<Demo name="ParticlesOff" />

```csharp
chart.ParticlesChanged += (_, e) => settings.ShowParticles = e.IsEnabled;
```

### 绑定到视图模型

使用 CommunityToolkit.Mvvm 时，由视图模型持有列表，并在切换时期时替换它们。id 不变的节点和连接会变形到新的大小和位置。

```csharp
public partial class JourneyViewModel : ObservableObject
{
    [ObservableProperty]
    private IList<SankeyFlowLink> _flows = [];

    [ObservableProperty]
    private bool _showParticles = true;

    public void Load(IEnumerable<(string From, string To, double Count)> rows)
        => Flows = rows.Select(row => new SankeyFlowLink(row.From, row.To, row.Count)).ToList();
}
```

```xml
<arc:SankeyFlow Label="Trial journey"
                Links="{Binding Flows}"
                IsParticlesEnabled="{Binding ShowParticles, Mode=TwoWay}" />
```

### 数据形状与性能

每次数据或尺寸变化只布局一次。绘制使用缓存的几何，粒子位于单独的图层，因此一帧粒子动画只重绘这些点，图表自身的代码不产生分配。几百条连接的图表依然流畅，粒子总数也限制在几百个以内。

### 内容准则

- **图表标签**：流动的是什么、在什么时候，例如“Lumen Notes trials, third quarter”。它为辅助技术命名图表。
- **节点标签**：简短的名词短语。过长的名称以省略号截断，竖向布局时截断得最紧。
- **阶段名称**：尽量一个词，例如“Channel”或“Outcome”。
- **单位**：单词要包含前导空格（" trials"），符号则不要（"%"）。

| 不要写 | 应该写 |
|---|---|
| Node 1 | Search ads |
| Step 2 | Onboarding |
| Dropped | Lapsed |
| Total | all trials |

### 最佳实践

**建议**

- 让流入节点的量等于流出的量，除非旅程确实有增减，这样份额才能对得上。
- 使用跨时期不变的 id，让图表变形而不是替换。
- 条带细到一两个像素以下时，把小流量归并到一个“其他”节点。

**避免**

- 用条带颜色表达含义。颜色只标记阶段，追踪使用强调色和变暗。
- 在一个阶段里重复计算同一个人。份额是相对总量读取的，重复会使其虚高。
- 在窄面板里放很多列。四五个阶段读起来最好；低于 420 px 时每个阶段占一行。

### 常见问题

**为什么多个节点的份额都是 100%？** 每个节点的份额是它的值与总量之比，所有流量都经过的阶段就是 100%。

**为什么缺少标签？** 相邻的标签会避让以免重叠。悬停或聚焦节点，可在提示框里读取。

**为什么按 Tab 进不了图表？** 图表只有在有流量时才能获得焦点。空图表显示 `EmptyLabel`。

## API 参考

<!-- api: SankeyFlow -->

<!-- api: SankeyFlowNode -->

<!-- api: SankeyFlowLink -->

## 无障碍

- 图表是一个可聚焦的分组，由 `Label` 命名。追踪节点或条带时，名称会以礼貌方式播报它的值、份额和去向。
- 帮助文本包含每个节点的摘要和按键说明。
- 追踪使用强调色加变暗，而不只靠色相，获得焦点的节点还有焦点环。
- 在触摸屏上，轻触可追踪，再次轻触可清除。

### 键盘

| 按键 | 操作 |
|---|---|
| `Tab` | 聚焦图表并追踪第一个节点。没有流量时跳过图表 |
| `Up` / `Down` | 列中的上一个或下一个节点。竖向布局时为上一行或下一行 |
| `Left` / `Right` | 上一列或下一列中最近的节点。竖向布局时为行中的上一个或下一个节点 |
| `Home` / `End` | 第一列或最后一列的第一个节点 |
| `Escape` | 清除追踪 |

## 动效

- 首次显示时，节点和条带逐列生长出来。
- 数据变化时，它们在一个共享的弹簧上变形；新增的从无到有，移除的收缩消失。
- 聚焦时，被追踪的份额沿条带在弹簧上向外倾注，其余部分变暗。
- 粒子由 `LiveLoop` 驱动，图表离开屏幕时暂停。每条条带最多 34 个。
- 减弱动效时直接绘制最终状态，并移除粒子和开关。

| 变化 | Full | Reduced | Off |
|---|---|---|---|
| 首次显示 | 逐列生长 | 短暂淡入 | 直接绘制 |
| 数据变化 | 弹簧变形 | 直接绘制 | 直接绘制 |
| 追踪 | 份额沿条带倾注 | 直接绘制 | 直接绘制 |
| 粒子 | 沿条带漂移 | 与开关一同移除 | 与开关一同移除 |

## 响应式

- 宽度低于 420 px 时，图表从上到下排列：列变成行，标签位于节点下方，阶段名称位于每行上方。
- 它测量自身宽度，所以窄面板中的图表与手机上的图表变化方式相同。
- 竖向布局每行至少 112 px，因此可能比 `PlotHeight` 更高。
- 方向键跟随布局：上下键在行之间移动。
- 用手指操作时，轻触追踪，再次轻触清除，因为触摸没有悬停。

## 相关组件

- `BarChart`：精确比较少量数值。
- `Treemap`：层级结构中的整体与部分。
- `Streamgraph`：随时间变化的图层。
- `WorkflowCanvas`：没有数量的步骤图。

| 组件 | 何时选择 |
|---|---|
| `BarChart` | 每一步有一个需要精确比较的值 |
| `DonutChart` | 数据是一个整体的几个部分 |
| `Treemap` | 各部分按层级嵌套 |
| `Streamgraph` | 各图层随时间变化 |
| `WorkflowCanvas` | 重要的是流程的形状，而不是数量 |
