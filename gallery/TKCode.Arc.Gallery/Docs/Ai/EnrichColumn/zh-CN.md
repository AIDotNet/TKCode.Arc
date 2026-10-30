---
title: 补全列
summary: 数据表中的 AI 补全列：同一个提示词逐行运行，每个单元格在处理时显示微光，随后落定为带来源的值，或显示可重试的失败。
source-hash: 741e6dfbca5e
---

<Demo name="Preview" />

## 何时使用

`EnrichColumn` 是带一列 AI 列的表格卡片。同一个提示词逐行运行：每个单元格先排队，智能体处理时显示微光，随后落定为一个简短的值和它的来源，或者显示失败和重试控件。用户可以停止运行、重新运行整列或单独一行，也可以在原地编辑这一列的名称和提示词。

- **补全公司或线索信息**：员工人数、融资、技术栈等智能体能查到的事实。
- **用一个提示词对各行分类或摘要**，并为每个答案显示来源。
- **在 CRM、销售工具或内部后台中展示逐行 AI 进度**，支持停止、重新运行和重试。

## 何时不用

- **列很多且需要编辑的大型表格**：使用数据表格，在其中加一列 AI 列。
- **针对整个数据集的单个问题**：问一次、显示一个答案，而不是一整列。
- **单个智能体的工具调用记录**：使用 `ToolCall`。

## 用法

把行和一个填充单元格的 `Enrich` 委托交给这一列。委托收到行和一个 `EnrichColumnContext`，返回值和它的来源。抛出异常即把单元格标为失败：异常消息会作为原因显示在“重试”旁边。

```csharp
table.Rows = accounts.Select(a => new EnrichColumnRow(a.Id, a.Name, a.Domain)).ToList();
table.Column = new EnrichColumnDefinition("Headcount and funding", "Find the headcount and latest funding round");
table.Enrich = async (row, context) =>
{
    context.SetActivity($"Searching for {row.Name}");
    var answer = await service.EnrichAsync(row.Domain, context.Prompt, context.CancellationToken);
    if (answer is null)
    {
        throw new InvalidOperationException("No source found");
    }
    return new EnrichColumnResult(answer.Value, new EnrichColumnSource(answer.Host, answer.Url));
};
table.CellChanged += (_, e) =>
{
    if (e.Cell.Status == EnrichColumnCellStatus.Done)
    {
        store.Save(e.Row.Id, e.Cell);
    }
};
```

这一列自己没有行时，会播放它的 `Simulation`，也就是由你提供的表格；两者都没有时显示空框架：顶栏显示 “0 rows”，AI 列没有名称，
没有行，“Run column” 不可用。只有你自己的行而没有 `Enrich` 时，行照常显示，但不会运行任何内容。

### 模拟

`EnrichColumnSimulation` 是 `Rows` 为 null 时这一列播放的表格：`Title`、`Column`、页脚中的 `Model`、`Rows`，以及
`Answers`：每个行 id 对应一个 `EnrichColumnSimulationAnswer`，包含单元格最终显示的值和来源。它的 `Steps` 是单元格在此之前显示的
活动，每个持续各自的 `Duration`；带有 `Error` 的答案在第一次尝试时失败，从而显示失败状态和重试，重试后得到该值。这一列首次进入
视口时会自动运行它（`AutoRun`），在顶栏（“sample data”）和读屏软件中说明这一点，并且不会发送任何内容。你自己设置的 `Title`、
`Column` 和 `Model` 优先于模拟中的值；把 `Simulation` 设为 null 会停止运行并显示空框架。

```csharp
table.Simulation = new EnrichColumnSimulation
{
    Title = "Target accounts",
    Column = new EnrichColumnDefinition("Headcount and funding", "Find the headcount and latest funding round"),
    Model = new EnrichColumnModel("Your model", "web search"),
    Rows = [new EnrichColumnRow("acme", "Acme Robotics", "acme.example")],
    Answers =
    [
        new EnrichColumnSimulationAnswer("acme", "410 people · Series C", new EnrichColumnSource("acme.example/press", "https://acme.example/press"))
        {
            Steps =
            [
                new EnrichColumnSimulationStep("Searching for Acme Robotics", TimeSpan.FromMilliseconds(700)),
                new EnrichColumnSimulationStep("Reading acme.example", TimeSpan.FromMilliseconds(900)),
            ],
            Error = "No source found", // the first attempt fails, so Retry shows
        },
    ],
};
```

本页的演示从 `EnrichColumnDemoData` 获取表格：公司和数字都是虚构的，模型是真实的（核对于 2026-10-29）。

### 你的行与服务

<Demo name="YourData" />

上下文包含单元格开始时的提示词和列定义、一个在用户停止运行或重新运行整列时触发的 `CancellationToken`、尝试次数（首次运行为 1，第一次重试为 2），以及 `SetActivity`：它设置运行中单元格的微光标签，可以在任何线程调用。把令牌传给你的请求：被取消的单元格会恢复为运行前的显示，停止后才到达的答案会被忽略。

### 结构

1. **顶栏**：表格名称（`Title`）、行数和 `Note`，以及列按钮：未运行时为“Run column”，运行中为“Stop”，有值后为“Re-run”。
2. **表头**：行列的表头，然后是 AI 列：一个闪光图标、列名及其下方的提示词，以及编辑控件。
3. **编辑器**：在表头下方展开，包含列名和提示词。“Save and run”应用修改并重新运行每一行；“Cancel”或 Escape 关闭它。
4. **行**：标志（16px；行没有 `Logo` 时显示首字母）、名称和域名，然后是单元格：状态图标、内容和这一行的运行或重试控件；控件在指针悬停、获得焦点和触屏时显示。
5. **页脚**：模型（`Model` 或模拟中的模型；为 null 时隐藏）和进度，以及失败数。

### 状态

| 状态 | 图标 | 内容 |
|---|---|---|
| `Idle` | 浅色虚线圆环 | 一条短横线 |
| `Queued` | 时钟 | “Queued” |
| `Running` | 加载圈 | 显示微光的活动标签 |
| `Done` | 自行描出的对勾 | 值（单行，悬停显示全文）和来源链接 |
| `Failed` | 红色警示图标 | “Failed”和原因 |

### 运行

`Concurrency`（默认 2）限制同时运行的单元格数。各个工作者相隔 140ms 启动，使这一列从上到下依次填充；每个工作者完成后从队列取下一行。工作者在取行时读取最新的行和列定义，因此运行中修改行不会作用于过时的数据：排队期间被移除的行会被跳过。

`Run()`、`Stop()` 和 `RunRow(id)` 与对应按钮的作用相同。只要有单元格在排队或运行，`IsRunning` 就为 true。一次运行落定（完成或停止）时触发 `RunCompleted`，并带上所有单元格。

### 已保存的值

<Demo name="SavedValues" />

把已有的值按行 id 通过 `InitialCells` 传回。`AutoRun` 会在这一列首次滚入视口时运行一次，但仅当还没有任何单元格有值时。播放模拟时默认开启，使用你自己的委托时默认关闭，除非你显式设置。`IsEditable="False"` 隐藏编辑控件。

### 窄屏与长表格

<Demo name="Narrow" />

卡片填满容器，设计宽度最大为 760px。宽度小于 560px 时，域名、来源文字和模型说明会收起；来源保留箭头链接。小于 440px 时，左右留白收紧到 16px，值在 60px 高的行中换成两行，失败原因隐藏在标签后面（悬停或聚焦即可查看）。在触屏上，行控件始终可见。

行是虚拟化列表。给卡片设置高度（或 `MaxHeight`）后，只会创建屏幕上的行；不设置时显示全部行。

### 自定义文案

`Labels` 包含这一列显示或朗读的所有文字。替换整个对象即可本地化或改写：

```csharp
table.Labels = new EnrichColumnLabels
{
    RowHeader = "Account",
    RunColumn = "Enrich",
    Rows = n => $"{n} accounts",
};
```

### 内容准则

- 值保持为简短的一行（“240 people · Series B”）；单元格会以省略号截断，悬停时显示完整的值。
- 为每个值提供用户可以打开的来源；可核实的链接才让 AI 给出的值真正可用。
- 失败原因要简短具体（“No source found”、“Rate limited”）。

### 最佳实践

- 遵守取消令牌；对有速率限制的服务，把 `Concurrency` 设低一些。
- 在 `CellChanged` 中保存值，下次通过 `InitialCells` 传回。
- 行数达到数百时，给卡片设置高度以启用虚拟化，并保持较低的并发数。

### 常见问题

**用户停止后单元格会怎样？** 它的请求被取消，并恢复为运行前的显示：之前的值，或一条短横线。

**保存列定义会保留旧值吗？** 每一行都会用新的提示词重新运行；轮到之前显示“Queued”。

**如何自己打开来源？** 处理 `SourceRequested` 并设置 `Handled`；否则由系统打开该地址。

## API 参考

<!-- api: EnrichColumn -->

<!-- api: EnrichColumnRow -->

<!-- api: EnrichColumnDefinition -->

<!-- api: EnrichColumnCell -->

<!-- api: EnrichColumnContext -->

<!-- api: EnrichColumnResult -->

<!-- api: EnrichColumnSource -->

<!-- api: EnrichColumnModel -->

<!-- api: EnrichColumnSimulation -->

<!-- api: EnrichColumnSimulationAnswer -->

<!-- api: EnrichColumnSimulationStep -->

<!-- api: EnrichColumnLabels -->

## 无障碍

- 各行构成一个表格：公司是每一行的行标题，AI 列的名称和提示词是它的列标题。
- 每种单元格状态都由图标和文字搭配表示（Queued、微光活动、值、带原因的 Failed），从不只靠颜色。
- 礼貌模式的实时区域会播报开始运行、停止和最终数量，并在值是模拟数据、没有发送任何内容时说明。
- 图标按钮的标签包含行名（“Run Lumen Freight”、“Retry Quarry Labs”）；来源链接会说明在新标签页中打开。
- 所有文字都在 `EnrichColumnLabels` 中。

### 键盘

| 按键 | 操作 |
|---|---|
| Tab | 依次经过列按钮、编辑控件、每行的运行或重试控件和来源链接。行控件在获得焦点时出现 |
| Enter、Space | 运行、停止或重新运行整列，运行一行，重试失败的单元格，或打开来源 |
| Ctrl + Enter（⌘ + Enter） | 保存编辑的列并重新运行 |
| Escape | 不保存并关闭列编辑器，焦点回到编辑控件 |

## 动效

| 变化 | 完整 | 减弱 | 关闭 |
|---|---|---|---|
| 单元格状态变化 | 在同一网格单元内交叉淡化一次：旧内容 100ms 淡出，新内容 160ms 淡入并上升 2px | 120ms 交叉淡化，不上升 | 立即 |
| 状态图标 | 120ms 交叉淡化，配合无回弹的缩放（0.6 到 1） | 仅交叉淡化 | 立即 |
| 完成对勾 | 240ms 描出 | 直接完整显示 | 直接完整显示 |
| 运行标签 | 平缓的微光，每 1.8s 扫过一次；新的活动文字在其中替换 | 静止 | 静止 |
| 加载圈 | 每 800ms 转一圈 | 每 1.6s 转一圈 | 静止 |
| 编辑器展开或收起 | 临界阻尼的高度弹簧（0.25s，无回弹） | 立即 | 立即 |
| 行控件出现 | 160ms 淡入 | 120ms 淡入 | 立即 |

单元格处理期间尺寸不会变化：行高固定，单元格内容在同一位置替换。

## 相关组件

- `ToolCall`：智能体发起的工具调用。
- `AgentStatus`：整个运行的实时状态。
- `Table`：不带 AI、可排序、可选择的数据表格。
