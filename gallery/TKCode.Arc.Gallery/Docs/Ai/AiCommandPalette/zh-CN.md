---
title: AI command
summary: 带 AI 模式的 Cmd+K 命令面板：搜索页面与操作，或按 Tab 提问，回答就地流式呈现，附带编号来源，以及可直接在列表中执行的后续步骤。
source-hash: 5fdd634d1cb1
---

<Demo name="Preview" />

## 何时使用

`AiCommandPalette` 是一个也能回答问题的命令面板。输入内容会搜索你的页面和操作；按 Tab 会把查询变成向模型提出的问题，
回答在面板内流式呈现，并附带编号来源。模型建议的后续步骤（例如提交问题单或打开文档）会作为列表行出现，按 Enter 就地执行。

- **全局 Cmd+K**：在一个人们既要跳转页面、又要询问工作区情况的产品中。
- **无结果时回退到回答的搜索**：没有匹配项时，最后一行会就该查询询问模型。
- **引出其他工具中操作的回答**，例如提交问题单或把摘要发到频道。

## 何时不用

- **带追问的长对话**：使用聊天线程；面板一次只回答一个问题。
- **没有 AI 的普通导航**：使用 `CommandPalette` 区块。
- **在表单字段中输入时的建议**：使用自动补全输入框。

## 用法

把你的页面和操作作为 `AiCommandPaletteItem` 交给面板，并用 `Responder` 回答问题。两者都不提供时，它是一个空面板，提问也不会得到回答。`ModelName` 默认为 null。

```csharp
palette.ItemsSource = commands;
palette.ItemSelected += (_, e) => Navigate(e.Item.Id);
palette.Responder = async context =>
{
    var reply = await api.AskAsync(context.Question, context.CancellationToken);
    context.SetSources(reply.Sources);          // the chips land first
    await foreach (var delta in reply.Stream)
    {
        context.Append(delta);                   // cite sources as [1], [2]
    }
    return new AiCommandPaletteAnswer
    {
        Actions = [new AiCommandPaletteAction("issue", "Create issue in Linear", "ENG", ArcIcons.CircleDot,
            token => CreateIssueAsync(context.Question, token))],
    };
};
```

### 你自己的页面和操作

<Demo name="Wired" />

每个条目有一个标签、一个用于给结果分区的可选 `Group`、一个 16px 的 `Icon`、低调的 `Meta` 文字、以键帽显示的 `Shortcut`，
以及额外的 `Keywords`。标记为 `IsSuggested` 的条目会在查询为空时列出。搜索是同步的按词匹配：查询中的每个词都必须是标签、
分组、附注或关键词中某个词的开头，因此适合几百行的规模；超出时请在服务器端过滤。选择一行会引发 `ItemSelected`，并执行该条目的 `Command`。

### 提问

`Responder` 接收一个 `AiCommandPaletteAskContext`，其中包含问题和一个取消令牌。用 `Append` 以任意大小的片段流式输出文字，
并在引用来源的文字之前调用 `SetSources`。最后返回一个 `AiCommandPaletteAnswer`，其中包含建议的操作（如果没有流式输出，
还要包含完整的 `Text`）。抛出异常会显示失败状态和一行“Try again”。当用户再次提问、返回搜索或面板离开窗口时，令牌会被取消。

引用在文字中写作 `[1]`、`[2]`，并会变成小按钮；悬停或聚焦其中一个会点亮对应的来源标签，反之亦然。选择任意一个都会引发
`SourceSelected`；带有 `Href` 的来源会用系统启动器打开，除非你处理了该事件。

### 建议的操作

每个 `AiCommandPaletteAction` 都就地执行：该行先显示加载圈和执行中的标签，然后显示只绘制一次的对勾和 `Run` 返回的标签
（“Created ENG-2481”），或者显示警示图标和错误标签，此时按 Enter 会重试。`ActionCompleted` 报告结果。设置 `DoneMeta`
说明发生了什么，例如模拟操作没有发送任何内容。

### 作为对话框

<Demo name="Dialog" />

`AiCommandPaletteDialog` 把面板显示在页面上方、靠近窗口顶部，并带有变暗的遮罩。Cmd+K（其他平台为 Ctrl+K）可在窗口任意位置
切换它，`IsOpen` 是双向的，按钮上的 `arc:AiCommandPaletteDialog.Opens` 可以打开它。在空搜索中按 Escape、点击遮罩或选择页面
都会关闭它。把你自己的面板放进去：

```xml
<arc:AiCommandPaletteDialog IsOpen="{Binding IsPaletteOpen}">
    <arc:AiCommandPalette ItemsSource="{Binding Commands}" />
</arc:AiCommandPaletteDialog>
```

内嵌使用时，`IsElevated` 会加上浮层阴影，`IsHotkeyEnabled` 会让 Cmd+K 或 Ctrl+K 聚焦输入框。在空搜索中按 Escape 会引发
`CloseRequested`。

### 窄容器

<Demo name="Narrow" />

面板会填满容器，最宽 680px，窄到 360px 也能正常使用。过长的标签和附注会以省略号截断，附注先截断；宽度低于 540px 时，
页脚中的导航提示会隐藏，来源标签会换行。`ResultsHeight` 设置结果区域的高度（默认 392），在输入、切换模式或流式输出时都不会改变。

### 你自己的措辞

`Labels` 包含面板显示或朗读的所有文字，包括占位文字、“Searching your workspace”状态和页脚提示。`ModelName` 和 `ModelMark`
在页脚、输入框和每条回答旁边标明所用模型。

### 模拟工作区

用于没有后端的演示或原型时，给面板一个 `AiCommandPaletteSimulation`：它的 `Items`、`Questions` 和预设的 `Answers` 会补上面板缺少的部分。模拟模型会选出问题中提到其 `Keywords` 最多的回答（没有关键词的回答接住其余问题），“搜索” 700ms，放出来源并流式输出文字；它的操作只是假装运行，并显示 “Simulated, nothing was sent”。面板自己既没有条目也没有 responder 时，还会在可见时播放演示：搜索 `SearchQuery`，打开 Ask AI，提出第一个问题并运行第一个操作；在面板内第一次真实的点击或按键会永久停止演示。

```csharp
palette.ModelName = "Your model";
palette.Simulation = new AiCommandPaletteSimulation
{
    Items = [new AiCommandPaletteItem("inbox", "Inbox", "Pages", ArcIcons.Inbox) { IsSuggested = true }],
    Questions = ["What changed this week?"],
    SearchQuery = "in",
    Answers =
    [
        new AiCommandPaletteSimulationAnswer
        {
            Sources = [new AiCommandPaletteSource("notes", "Release notes")],
            Text = "Saved views shipped on Tuesday [1].",
            Actions = [new AiCommandPaletteSimulationAction("open", "Open release notes", "Docs", ArcIcons.FileText, "Opened")],
        },
    ],
};
```

```csharp
palette.Labels = new AiCommandPaletteLabels
{
    SearchPlaceholder = "Rechercher des pages et des actions",
    AskMode = "Demander à l’IA",
    Thinking = "Recherche dans votre espace",
};
```

### 最佳实践

- 条目标签保持简短，把上下文放进 `Meta`；用 `Group` 给结果分区。
- 先返回来源再返回文字，让来源标签先出现，并在文字中引用它们。
- 给每个操作提供一个遵守令牌的 `Run`，并把所创建对象的名称作为完成标签返回。

## API 参考

<!-- api: AiCommandPalette -->

<!-- api: AiCommandPaletteDialog -->

<!-- api: AiCommandPaletteItem -->

<!-- api: AiCommandPaletteSource -->

<!-- api: AiCommandPaletteAction -->

<!-- api: AiCommandPaletteAnswer -->

<!-- api: AiCommandPaletteAskContext -->

<!-- api: AiCommandPaletteLabels -->

<!-- api: AiCommandPaletteSimulation -->

<!-- api: AiCommandPaletteSimulationAnswer -->

<!-- api: AiCommandPaletteSimulationAction -->

## 无障碍

- 焦点始终留在输入框中，输入框被读作组合框；各行是列表项，高亮的那一行会报告自己处于选中状态，因此方向键永远不会把焦点移出输入框。
- 一个礼貌型实时区域会播报问题、完成的回答、操作结果和模拟执行（“Simulated, nothing was sent”）。
- 状态从不只靠颜色表达：执行中、已完成或失败的操作会同时改变图标和标签。
- 来源标签和引用都是按钮，以来源的完整说明命名；悬停或聚焦其中一个会点亮与之对应的另一个。
- 模式切换是一对带按下状态的按钮，不会从输入框夺走焦点。

### 键盘

| 按键 | 操作 |
|---|---|
| Up、Down | 在结果、问题或建议的操作之间移动（到头时循环） |
| Enter | 打开页面、执行操作、提出问题，或重试失败的回答或操作；在 Ask AI 中有文字时提出该问题 |
| Tab | 就当前查询询问 AI；查询为空时打开带建议问题的 Ask AI，在那里按 Tab 返回搜索 |
| Escape | 从 Ask AI 返回搜索（取消正在流式输出的回答）；在搜索中先清空查询，再引发 `CloseRequested` |
| Cmd+K、Ctrl+K | 开启 `IsHotkeyEnabled` 时聚焦输入框；切换 `AiCommandPaletteDialog` |

## 动效

| 变化 | 完整 | 减弱 | 关闭 |
|---|---|---|---|
| 视图（结果、问题、回答） | 旧视图在 100ms 内淡出，新视图在 160ms 内淡入并上升 2px | 120ms 淡入淡出 | 立即 |
| 行间高亮 | 130ms 滑动；列表本身变化时直接跳到位、不移动 | 立即 | 立即 |
| 回答文字 | 每 34ms 出现 3 到 7 个词，每批在 140ms 内淡入；视图随文字向下跟随，除非你向上滚动 | 每 240ms 最多 24 个词，不淡入 | 与减弱相同 |
| 来源标签、建议的操作 | 160ms 淡入 | 立即 | 立即 |
| 操作标签、状态图标、页脚提示 | 一次交叉淡化；完成的对勾只绘制一次 | 短暂淡入淡出 | 立即 |
| 演示 | 可见时播放，不可见时暂停，回来后从干净状态重新开始 | 从不播放 | 从不播放 |

面板的尺寸从不改变：结果区域在每个视图中都保持同样的高度。

## 相关组件

- `CommandPalette`（区块）：不带 AI 的命令菜单。
- `ToolCall`：智能体发起的工具调用。
- `AgentStatus`：运行中智能体的实时状态。
