---
title: Ask your data
summary: 自然语言生成图表的卡片：输入问题，SQL 流式写入一个可折叠、带语法着色的面板，查询运行时行数滚动计数，答案以堆叠柱形、折线或表格逐步呈现，下方附一行洞察。
source-hash: 8e9aa8eaacfc
---

<Demo name="Preview" />

## 何时使用

`AskYourData` 在一张卡片里把一句白话问题变成图表。模型写出的 SQL 带着语法着色流式写入“Generated SQL”面板，查询运行时已扫描行数滚动计数，答案以堆叠柱形、折线或表格逐步呈现，附带图例、每个类别的读数，以及在下方流式出现的一行洞察。SQL 始终只需一次点击即可查看，方便核对或复制实际执行的内容。

- **产品内的自助分析**：仪表板、指标页或管理后台里的提问框。
- **AI 助手的结果视图**：当答案是一个最适合用图表阅读的数字时。
- **讲解或审计文本转 SQL**：展示生成的查询能建立信任。

## 何时不用

- **查询是固定的**，没有问题可问：使用 `BarChart` 或 `LineChart`。
- **数据库查询只是较长智能体记录中的一步**：使用 `ToolCall`。
- **由人手写并调优查询**：使用 SQL 编辑器。

## 用法

通过三个委托把卡片接入你的模型和只读数据库角色。一个委托都没有时，它会播放它的 `Simulation`，也就是由你提供的答案；两者都没有时，它停在空状态（“Ask a question about your data”），提问按钮不可用。

```xml
<arc:AskYourData Placeholder="Ask about your orders" />
```

你的模型和数据库：

```csharp
card.Source = new AskYourDataSource { Name = "analytics", Dialect = "PostgreSQL" };

// The whole SQL at once: the card reveals it four tokens a tick at its final size.
card.GenerateSql = request => model.WriteSqlAsync(request.Question, request.CancellationToken);

// Run it read only and shape the rows for the chart. Throw to show the database's message.
card.RunQuery = async request =>
{
    var rows = await db.QueryAsync(request.Sql, request.CancellationToken);
    request.ReportProgress(rows.Count, rows.Count);
    return new AskYourDataResult
    {
        Title = "Weekly signups by plan",
        Categories = WeeksOf(rows),
        Series = SeriesByPlan(rows),
        RowCount = rows.Count,
        DurationMs = rows.Milliseconds,
    };
};

card.ResultReady += (_, e) => history.Save(e.Result.Title, e.Sql);
```

`AutoRun` 只在由模拟编写 SQL 时默认开启；把它设为 `True`，卡片一出现就会提出初始的 `Question`。`Ask()` 提问输入框里的问题，`Ask(question)` 先把新问题放进输入框再提问，`Stop()` 取消正在进行的运行。

### 模拟

`AskYourDataSimulation` 播放一个由你描述的模型和数据库：卡片有 35% 出现在屏幕上时自行提出的 `Question`、`Suggestions`、`Source`，以及它的 `Answers`。问题中包含某个答案的 `Keywords` 之一时，得到该答案的 `Sql`、`RowsScanned` 和 `Result`；其他问题得到 `Fallback`（SQL 中的 `{keyword}` 会替换为问题中最长的词），或者没有任何行。SQL 在短暂停顿后逐步显示，计数分十步、每步 110ms 滚动上升，然后图表构建、洞察逐字出现；实时区域会补充说明没有查询任何内容。它只替代你没有接入的委托，你自己设置的 `Question`、`Suggestions` 和 `Source` 优先于它。把 `Simulation` 设为 null 会停止它的运行并回到空状态。

```csharp
card.Simulation = new AskYourDataSimulation
{
    Question = "Weekly signups by plan since August",
    Suggestions = ["Weekly signups by plan since August", "Churn rate by plan per month"],
    Source = new AskYourDataSource { Name = "analytics", Dialect = "PostgreSQL" },
    Answers =
    [
        new AskYourDataSimulationAnswer
        {
            Keywords = ["signup", "sign up"],
            Sql = "SELECT week, plan, count(*) AS signups FROM signups GROUP BY week, plan;",
            RowsScanned = 18_342,
            Result = weeklySignups, // an AskYourDataResult you build
        },
    ],
};
```

本页的演示从 `AskYourDataDemoData` 获取答案：一个虚构产品的注册数、MRR 和流失率。

### 流式输出 SQL

<Demo name="Streaming" />

用 `StreamSql` 代替 `GenerateSql`，传入来自模型的异步分块流，面板会随分块到达而增高，每个分块淡入出现。在 `RunQuery` 中调用 `ReportProgress(rowsScanned, totalRows)` 可让运行状态中的计数滚动；传入总数会预留宽度，这一行就不会跳动。两个委托都会收到取消令牌：请遵守它，这样“停止”才能真正取消。

```csharp
card.StreamSql = request => model.StreamSqlAsync(request.Question, request.CancellationToken);
```

### 结果

`AskYourDataResult` 的结构为图表而设计：`Categories` 是按顺序排列的 X 轴标签，每个 `AskYourDataSeries` 为每个类别提供一个值。各系列默认可以相加（`Stacked`）：柱形堆叠，图例、读数和表格显示合计。比率和平均值请设 `Stacked = false`；柱形改为并排分组，也不显示合计。

| 属性 | 作用 |
|---|---|
| `Title`、`Subtitle` | 图表建成后的标题（"Aug 3 to Oct 25 · 1,430 signups"） |
| `LegendValue` | 注册数等流量用 `Sum`，MRR 等存量用 `Last`，不显示用 `None` |
| `Chart` | 该结果打开时的视图；否则使用 `DefaultChart` |
| `FormatValue` | 格式化刻度、图例、读数和表格单元格（$258k、3.9%） |
| `Insight` | 解读图表的一两句话，在图表下方流式出现，并预留两行高度 |
| `RowCount`、`DurationMs` | SQL 元信息："PostgreSQL · 48 rows · 214ms" |

没有类别或系列的结果会显示 "No rows matched" 并给出建议问题。

### 错误与停止

<Demo name="Errors" />

在 `GenerateSql`、`StreamSql` 或 `RunQuery` 中抛出异常，卡片会原样显示消息并提供“重试”，同时引发 `Failed`，并给出失败的阶段（`Sql` 或 `Query`）。卡片工作期间，“提问”按钮会停止运行，在输入框中按 Escape 也会停止；停止的运行显示 "Stopped"，不会引发任何事件。

### SQL 面板

编写 SQL 时面板展开，查询开始后、图表建成之前折叠，这样同一时刻只有一个区域在动。`KeepSqlOpen` 让它保持展开。一旦用户在运行中自己展开或折叠了面板，就以用户的选择为准。SQL 由库自带的代码高亮着色，并在自己的区域内横向滚动；“复制 SQL”会复制整条查询。

### 图表视图

切换器在柱形、折线和表格之间切换。`Chart` 保持为 null 时，每个结果按自己的 `Chart` 或 `DefaultChart` 打开；绑定 `Chart` 即可自行控制视图。用户切换时会引发 `ChartChanged`。

```xml
<arc:AskYourData DefaultChart="Table" KeepSqlOpen="True" />
```

### 窄卡片

<Demo name="Narrow" />

宽度小于 520px 时，数据源只保留图标，图表切换器移到结果中、图例上方并占满整行，舞台区域会增高以容纳换行后的图例和洞察。类别标签会自动稀疏，保持 46px 的间距；过宽的 SQL 行和表格在各自的区域内滚动。

### 自定义文案

`Labels` 包含卡片显示或朗读的所有文字；`Placeholder` 是输入框的提示，`Suggestions` 是空状态中提供的问题（空列表会隐藏它们）。

```csharp
card.Labels = new AskYourDataLabels
{
    WritingSql = "SQL wird geschrieben",
    RunningQuery = "Abfrage läuft",
    Rows = rows => $"{rows} Zeilen",
};
```

### 最佳实践

- 把数据库结构写进模型的提示词，并用只读角色和语句超时来运行 SQL。
- 使用默认配色时，系列最多保持四个。
- 让模型根据返回的行来写洞察，使用结果中确实存在的具体数字。
- 极快的进度报告请合并为每秒几次。

## API 参考

<!-- api: AskYourData -->

<!-- api: AskYourDataResult -->

<!-- api: AskYourDataSeries -->

<!-- api: AskYourDataSource -->

<!-- api: AskYourDataSimulation -->

<!-- api: AskYourDataSimulationAnswer -->

<!-- api: AskYourDataLabels -->

## 无障碍

- 一个礼貌型实时区域会播报运行过程：正在编写 SQL、正在运行查询，然后是图表标题、行数和洞察。没有 `GenerateSql`、`StreamSql` 和 `RunQuery` 时，还会加上 "Simulated with sample data, nothing was queried."
- 状态从不只靠颜色表达：SQL 行和舞台标题中，文字标签旁总有旋转指示器、绘制出的对勾、警告或停止图标。
- SQL 行是一个会报告展开或折叠状态的按钮，名称由标题和元信息组成，例如 "Generated SQL, PostgreSQL · 48 rows · 214ms"。
- 图表可获得焦点，以结果命名；方向键移动读数并播报，例如 "Aug 10: Free 61, Pro 26, Team 10, Enterprise 2, total 99"。
- 输入框获得焦点时边框变为强调色；其他控件显示 Arc 的键盘焦点环。

### 键盘

| 按键 | 操作 |
|---|---|
| Enter | 在问题输入框中提问 |
| Escape | 在问题输入框中停止正在进行的运行 |
| Tab | 依次经过输入框、提问按钮、Generated SQL 行、复制 SQL、SQL 区域、图表切换器、图表，以及建议问题或“重试” |
| Enter、Space | 在 Generated SQL 行上展开或折叠 SQL |
| Left、Right、Home、End | 在图表上于类别之间移动读数；在图表切换器上切换视图 |
| Escape（图表） | 清除读数 |

## 动效

| 变化 | 完整 | 减弱 | 关闭 |
|---|---|---|---|
| SQL 流式出现 | 每 34ms 四个记号，每个分块用 140ms 淡入；先完成排版，面板保持最终尺寸 | 一次显示全部 | 一次显示全部 |
| SQL 面板展开或折叠 | 300ms 无回弹的高度弹簧，内容淡入淡出 | 立即 | 立即 |
| 柱形 | 从基线升起，用时 320ms，错开最多 180ms | 直接画出 | 直接画出 |
| 折线 | 从左向右绘制，用时 460ms | 直接画出 | 直接画出 |
| 切换视图 | 在固定区域内交叉淡化，淡出 100ms、淡入 160ms | 立即 | 立即 |
| 标题与 SQL 元信息 | 以单行交叉淡化，并有 2px 的落定 | 短暂淡化 | 立即 |
| 已扫描行数 | 等宽数字逐位滚动，宽度预留 | 原地更新 | 原地更新 |
| 洞察 | 每个节拍四个词，预留两行高度 | 一次显示全部 | 一次显示全部 |
| 旋转指示器与骨架 | 旋转与呼吸；离开屏幕时暂停 | 静止 | 静止 |

## 相关组件

- `BarChart` 和 `LineChart`：查询固定、没有问题可问时使用。
- `ToolCall`：数据库查询作为智能体记录中的一步时使用。
- `ReasoningBlock`：答案上方展示模型的思考过程。
