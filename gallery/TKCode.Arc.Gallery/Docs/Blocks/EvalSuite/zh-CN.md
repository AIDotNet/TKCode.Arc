---
title: 评测套件
summary: 用于提示词或模型的评测套件：测试用例在基准版本和候选版本上运行，显示两者的通过率和相差的百分点，每个用例标为通过、失败、退步或修复，可以只看退步的用例，打开一个用例可以并排查看两个输出，或查看逐词差异。
source-hash: bbec12b9b38e
---

<Demo name="Preview" />

## 何时使用

评测套件在提示词或模型的改动上线之前回答一个问题：新版本是否比现在使用的版本更好，又弄坏了什么？它是一张卡片。顶部选择要比较的两个版本：基准版本（现在在用的）和候选版本（这次的改动），然后运行套件。三个方块给出两个版本各自的通过率以及相差的百分点。下面每个测试用例占一行，显示它的状态、输入、检查方式，以及每个版本的结果。一个用例只会有四种变化：两边都通过、两边都失败、**退步**（原来通过，现在失败）或**修复**（原来失败，现在通过）。筛选胶囊可以直接跳到退步的用例；打开一个用例会显示两个输出以及检查给出的原因，可以并排查看，也可以看成一份逐词差异。运行时所有用例先排队，每次运行几个，每个用例一有结果就翻成结果；运行可以随时停止。

- **审查提示词改动**：更短的系统提示词、新的语气或新的指令，在上线前用重要的用例检查一遍。
- **比较模型**：同一组用例分别在现有模型和更便宜或更新的模型上运行。
- **发布前的关口**：有没有退步一眼就能看到，每一个退步都可以打开阅读和讨论。
- **保留历史**：较早版本的结果留在 `Results` 中，切换基准版本就能看到以前的比较，不必再运行。

## 何时不用

- **对开放式回答做两两投票**，没有固定检查：请使用 `ModelArena`。
- **编辑并试用单个提示词**：请使用 `PromptStudio`；需要在很多用例上检查改动时再来这里。
- **实时监控线上流量**：随时间变化的图表属于仪表板；套件是一组固定的用例。
- **每个用例有几百个指标**：每行只显示一个状态和每个版本的一个结果。宽的数字结果请使用 `Table` 或 `DataGrid`。

## 用法

`EvalSuite` 派生自 Avalonia 的 `TemplatedControl`。不设置任何属性时，它显示一个虚构的示例套件：一家虚构的共享单车的十四个客服回复用例，三个提示词版本及其结果，比较 v12 和 v13，**Run suite** 后面是模拟的运行。

```xml
<arc:EvalSuite Title="Support replies"
               Cases="{Binding Cases}"
               Versions="{Binding Versions}"
               Results="{Binding Results, Mode=TwoWay}"
               BaselineVersionId="v12"
               CandidateVersionId="v13" />
```

### 结构

- **顶部**：标题；一行文字说明用例数量和运行进行到哪里；**Run suite** 按钮（运行时变为 **Stop**）；以及两个版本选择，中间有一个从基准指向候选的箭头。
- **进度**：运行时显示一条细的进度条。
- **方块**：基准和候选的通过率，下面是"10 of 14 passed"；以及相差的百分点，带一个箭头和"2 regressions · 3 fixed"。相差的值是两个取整后通过率的差，所以三个数字总是对得上。
- **筛选**：All、Regressions、Fixed 和 Failing（候选失败：退步加上两边都失败的用例），每个都带数量。
- **用例列表**：状态标签、用例标题和下面的输入、检查方式（窄时隐藏），以及每个版本的结果：对勾加"Pass"、叉号加"Fail"，或警告加"Error"；评分用例还会加上分数。
- **打开的用例**：标题和状态、输入和检查，以及两个输出各自的结果和原因；或者从基准输出到候选输出的一份逐词差异。

### 用例、检查与结果

一个 `EvalCase` 有 `Id`、简短的 `Title`、`Input`、`Expected` 文本和一个 `Check`：

| 检查 | 通过条件 |
|---|---|
| `Exact` | 去掉首尾空白后的输出等于 `Expected` |
| `Contains` | 输出包含 `Expected`，不区分大小写 |
| `Regex` | 输出匹配 `Expected` 中的模式，不区分大小写 |
| `Rubric` | 评分器给出的 `Score`（0 到 1）达到 `Threshold`（默认 0.7）；`Expected` 描述评分标准 |

一个 `EvalResult` 属于一个用例和一个版本（`CaseId`、`VersionId`），包含 `Output`、`Passed`、可选的 `Score`、一行 `Detail`（原因）和 `Duration`。`Results` 可以包含任意多个版本的结果；套件从中取出要比较的两个。`EvalSuite.Grade(case, versionId, output, score)` 会应用用例的检查，并替你写好原因。

### 运行

**Run suite**（或 `RunAsync()`）先删除这两个版本在这些用例上的旧结果，把所有用例排队，每次运行 `MaxConcurrency` 个用例（默认 2 个），每个用例同时在两个版本上运行。一行先显示 Queued，然后是带旋转图标的 Running，两个结果都到达后显示它的状态；数量、方块和筛选都随每个用例更新。每个用例完成后 `Results` 会换成一个新列表，所以双向绑定可以边运行边保存。**Stop**（或 `Stop()`）会取消正在运行的用例；已完成的用例保留结果，其余显示"Not run"。

设置 `RunCaseAsync` 可以用你自己的代码运行用例。返回评过分的结果；抛出的异常会变成带有其消息的"Error"结果；运行停止或套件离开窗口时，令牌会被取消。

```csharp
suite.RunCaseAsync = async (evalCase, version, cancellation) =>
{
    var reply = await client.CompleteAsync(version.Id, evalCase.Input, cancellation);
    var score = evalCase.Check == EvalCheckKind.Rubric
        ? await grader.ScoreAsync(evalCase.Expected, reply, cancellation)
        : (double?)null;
    return EvalSuite.Grade(evalCase, version.Id, reply, score);
};
```

<Demo name="CustomRunner" />

`RunStarted`、`CaseCompleted`（带两个结果和状态）和 `RunCompleted`（带 `Summary`、是否被停止以及用时）报告一次运行。`Phase`、`FinishedCount`、`RunTotal` 和 `Summary` 可以读取或绑定。

### 退步与打开的用例

选择一个筛选胶囊，或设置 `Filter`。点击一行，或在行上按 Enter，即可在列表下方打开该用例（`SelectedCaseId`）；再点一次、按 Escape 或按关闭按钮即可关闭。打开一个用例时，方向键会移到下一个用例并打开它。`DetailMode` 在 **Side by side** 和 **Word diff** 之间切换；差异视图把新增的词标为绿色、删除的词以红色删除线标出，每段都带有符号，因此不只依靠颜色。

<Demo name="Regressions" />

### 文案

所有文字都来自 `Labels`（`EvalSuiteLabels`）。以 `Format` 结尾的项在 `{0}`、`{1}` … 中接收已格式化的值，因此可以调整词序。`CultureName` 决定数字的格式。

```csharp
suite.Labels = new EvalSuiteLabels
{
    Run = "运行",
    Stop = "停止",
    PassedOfFormat = "{1} 个中通过 {0} 个",
};
```

### 内容建议

- **标题说明用例检查什么**，而不是它问什么："Greets by name"、"Quotes the day pass"。
- **优先使用仍然公平的最严格检查**：事实用 `Contains`，格式用 `Regex`，只有语气或长度这类特点才用 `Rubric`。
- **原因要简短**：原因只占一句（"Missing “30 days”"、"Scored 0.62, needs 0.70"）。
- **版本名要短**，把区别写在备注里："v13 · Shorter prompt"。

## API 参考

<!-- api: EvalSuite -->

<!-- api: EvalCase -->

<!-- api: EvalVersion -->

<!-- api: EvalResult -->

<!-- api: EvalSummary -->

<!-- api: EvalSuiteLabels -->

### 事件

| 事件 | 触发时机 | 参数 |
|---|---|---|
| `RunStarted` | 一次运行开始 | `Baseline`、`Candidate`、`Cases` |
| `CaseCompleted` | 一个用例有了两个结果 | `Case`、`Baseline`、`Candidate`、`Status` |
| `RunCompleted` | 一次运行完成或被停止 | `Summary`、`WasStopped`、`Finished`、`Elapsed` |

## 无障碍

- 卡片是一个以标题命名的区域。每个方块有一段无障碍文字（"v13: 79% pass rate, 11 of 14 cases"；"Change: +8 points. 2 regressions · 3 fixed"）；滚动的数字本身对读屏软件隐藏。
- 用例列表只占一个 Tab 位置。它的名称是键盘光标所在的行（"2 of 14: Greets by name, Regression. Baseline Pass, candidate Fail"），帮助文字说明显示了多少个用例，因此移动光标就会读出每一行。
- 状态和结果从不只依靠颜色：每一种都有图标（退步是向下的箭头，修复是向上的箭头，以及对勾、叉号、警告）和一个词。
- 副标题是一个礼貌的实时区域；另有一个隐藏的实时区域在运行开始和结束时播报（"Run complete. v13 passed 11 of 14, +8 points. 2 regressions."），或说明运行在哪里停止。
- 版本选择、筛选胶囊、详情切换和关闭按钮都是带名称的 Arc 控件；输出是可以选择的文字。

### 键盘

| 按键 | 操作 |
|---|---|
| `Tab` | Run、两个版本选择、筛选胶囊、用例列表，然后是打开用例的切换和关闭按钮 |
| 列表中的 `ArrowUp` / `ArrowDown` | 上一个或下一个用例（有用例打开时会打开它） |
| `Home` / `End`、`PageUp` / `PageDown` | 第一个或最后一个用例，向上或向下一页 |
| 列表中的 `Enter` 或 `Space` | 打开或关闭光标所在的用例 |
| `Escape` | 关闭打开的用例并回到列表 |
| 胶囊上的 `ArrowLeft` / `ArrowRight` | 上一个或下一个筛选 |

## 动效

- 通过率和相差的值以弹簧滚动到新值；相差的箭头和颜色立即切换。
- 完成的用例的标签和结果在 240 ms 内淡入，并上升 4 px；正在运行的用例的图标会旋转。
- 打开的用例以高度弹簧展开和收起；并排视图和逐词差异之间立即切换。
- 选中的筛选胶囊在胶囊之间滑动。
- 减弱动效时保留淡入但不上升，旋转的图标停止；高度和数字直接跳到位。关闭动效时所有变化立即生效。

| 变化 | 完整 | 减弱 | 关闭 |
|---|---|---|---|
| 用例完成 | 淡入并上升 4 px，240 ms | 淡入，240 ms | 立即 |
| 正在运行的用例 | 图标旋转 | 图标静止 | 图标静止 |
| 通过率和相差 | 以弹簧滚动 | 直接跳到位 | 直接跳到位 |
| 打开或关闭用例 | 高度弹簧 | 立即 | 立即 |

## 响应式

- `EvalSuite` 按自身宽度变化，而不是按窗口宽度。卡片最宽 960 px（`CardMaxWidth`）。
- 宽度为 620 px 或更小时（`CompactWidth`、`:compact`），内边距收紧，三个方块上下排列，两个版本选择平分宽度，检查列隐藏，结果列只保留图标，打开用例的两个输出上下排列。
- 用例列表超过 448 px（`ListMaxHeight`）时在卡片内滚动，并且只绘制可见的行，所以很长的套件也很快。标题和输入以省略号结尾；打开的用例会完整显示它们。
- 让卡片拉伸（`HorizontalAlignment="Stretch"`），并给它 400 px 或更宽的空间。

<Demo name="Narrow" />

## 相关组件

- `PromptStudio`：在这里检查之前，先编辑和试用一个提示词。
- `ModelArena`：在两个模型之间做匿名偏好投票。
- `ModelPicker`：选择一个版本使用的模型。

| 组件 | 何时选择 |
|---|---|
| `PromptStudio` | 正在编写和试用一个提示词 |
| `ModelArena` | 人们在没有固定检查的情况下为两个回答投票 |
| `Table` | 每个用例的结果有很多数字列 |
