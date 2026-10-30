---
title: Agent cursor
summary: 一份实时文档，AI 智能体像协作者一样在其中编辑：它带名字标签的光标选中、改写并修正文字，同时一位同事的光标跟着阅读，所有改动保持着色，直到有人接受或撤销。
source-hash: 281ae90d39e9
---

<Demo name="Preview" />

## 何时使用

`AgentCursor` 展示 AI 智能体像同事一样编辑文档。它带名字标签的光标从页头的头像中出来，拖动选中一个段落并流式写入改写后的文字，添加一个标题，再修正一个拼写错误，同时一位同事的光标跟着阅读。每处改动都以智能体的颜色着色，直到有人在浮动的审阅栏中接受或撤销。没有自己的数据时，它会循环播放你提供的模拟（预览中是一份虚构的发布简报、一个以真实模型命名的智能体和一位同事）。

- **AI 写作助手**：与人一起编辑文档，并提供接受与撤销。
- **落地页和新手引导**：展示一个正在运行的协作式 AI 编辑器。
- **从智能体流式接收结构化编辑的产品界面**：需要一个人工审阅步骤。

## 何时不用

- **自由对话或问答**：使用聊天记录组件。
- **真正的富文本编辑器**：使用编辑器自带的协作光标；这张卡片渲染的是只读的块。
- **包含很多章节的长文档**：卡片面向五到十个块的简短摘录。
- **单段文字的差异**：使用差异视图。

## 用法

把文档作为 `Blocks`（带稳定 id 的标题、小标题和段落块）交给卡片，把编辑作为 `Edits`。编辑按 id 指向块：改写替换一个块的文字，插入在某个块之后添加一个块，修正替换块文字中的一个确切的词。卡片按顺序播放这些编辑，并停在审阅状态。

```csharp
card.Blocks =
[
    new AgentCursorBlock("title", AgentCursorBlockKind.Title, "Weekly update"),
    new AgentCursorBlock("shipped", AgentCursorBlockKind.Paragraph, "This week the team shipped the new onboarding flow."),
];
card.Edits =
[
    AgentCursorEdit.Rewrite("shipped", "We shipped the new onboarding flow.", "Making it shorter"),
    AgentCursorEdit.Insert("shipped", AgentCursorBlockKind.Heading, "Next week"),
];
```

既没有 `Blocks`、`Edits`、`Run` 也没有 `Simulation` 时，卡片显示一份空文档；没有名称的智能体称为 "Agent"（`AgentCursorLabels.Agent`），没有 `Collaborator` 时也没有人跟着阅读。

### 模拟

想在没有智能体的情况下展示卡片（演示、设计评审、新手引导），就给它一个 `AgentCursorSimulation`：文档、编辑、智能体、一位同事和元信息行。卡片没有 `Blocks`、`Edits` 或 `Run` 时会播放这个模拟，说明什么都没有保存，并在动效为完整时循环。你自己的数据优先于模拟；把 `Simulation` 设为 null 会停止模拟并显示空文档。

```csharp
card.Simulation = new AgentCursorSimulation
{
    Blocks = [new AgentCursorBlock("summary", AgentCursorBlockKind.Paragraph, "Shared inbox is a new place where teams answer email.")],
    Edits = [AgentCursorEdit.Rewrite("summary", "Shared inbox lets a team answer email together.", "Tightening the summary")],
    Agent = new AgentModel("Claude Opus 5.5", ArcIcons.Sparkles),
    Collaborator = new AgentCursorCollaborator("Maya Ortiz"),
};
```

上方的预览播放的就是一个模拟，其简报、编辑、智能体和同事来自图库自己的演示数据。

### 结构

1. **页头**：智能体的头像（以其颜色为底的标记），旁边是同事的头像；一行状态说明智能体正在做什么；以及 Ask、Stop、Run again 或 Retry 按钮。
2. **文档**：标题、可选的弱化 `Meta` 行、小标题和段落。
3. **智能体光标**：智能体颜色的箭头和它的名字。它负责选中、流式写入和标记拼写错误。
4. **同事光标**：第二个箭头，带有这个人的姓名首字母和名字，跟着阅读。
5. **审阅栏**：浮在底部，显示改动数量、Undo 和 Accept changes。

### 你自己的文档和编辑

<Demo name="YourDocument" />

`AutoPlay="False"` 让卡片保持静止，直到有人按下 Ask（或你调用 `Ask()`）。`Agent` 为智能体命名，并给它一个标记；通过纯色的 `MarkBrush` 还决定其光标、选区和改动着色的颜色，没有时它们跟随强调色。`Collaborator` 是跟着阅读的人；设置 `IsCollaboratorVisible="False"` 可以隐藏他。

从 `Accepted` 中保存文档，再作为 `Blocks` 传回。卡片按内容比较块，所以传回它刚显示的文档不会有任何变化；不同的文档会在没有运行使用它时替换屏幕上的文档。

### 从你的智能体获取编辑

<Demo name="FromYourAgent" />

`Run` 接受一个获取编辑的函数。等待期间，光标会阅读文档。按下 Stop 会取消令牌；失败的任务会以错误状态结束并显示 Retry 按钮，其消息会被播报。

```csharp
card.Run = async cancellation =>
{
    var response = await http.PostAsJsonAsync("/api/agent/edits", document, cancellation);
    return await response.Content.ReadFromJsonAsync<List<AgentCursorEdit>>(cancellation) ?? [];
};
```

### 审阅、接受与撤销

完成的编辑保持着色，审阅栏会统计它们。Accept changes 清除着色，并以编辑后的文档引发 `Accepted`；Undo 恢复文字、收起插入的块，并引发 `Undone`。Stop 会保留已完成的编辑以供审阅，并恢复进行到一半的编辑。`Phase` 和 `PhaseChanged` 跟随运行过程：Idle、Thinking、Editing、Review、Accepted、Reverted、Stopped、Empty 和 Error。`Accept()`、`Undo()` 和 `Stop()` 在代码中完成同样的操作。

模拟会循环：同事在短暂停顿后接受，然后文档重置，运行重新开始。指针停在卡片上或焦点在卡片内时，这段停顿不会结束。设置 `Loop` 可以重放你自己的运行；低于完整动效时从不循环。

### 窄容器

<Demo name="Narrow" />

卡片在 680px 以内随容器伸缩。低于 480px 时，文档内边距收紧，审阅栏去掉 "by" 和智能体的名称，以保持在一行内。文字会换行，选区和光标目标按实时的行来测量，靠近右边缘时光标的标签会移到箭头左侧。

### 你自己的措辞

`Labels` 保存卡片显示或朗读的每一个词。包含名字或数量的文字是函数：

```csharp
card.Labels = new AgentCursorLabels
{
    Reading = agent => agent + " lit le document",
    Ready = count => count + " modifications prêtes",
    Accept = "Accepter",
};
```

### 内容准则

- 摘录保持简短：五到十个块。
- 用几个词为每个编辑写一个 `Description`（"Tightening the summary"）；状态行和屏幕阅读器会用到它。
- 修正编辑需要块文字中要替换的确切的词。

### 最佳实践

- 保存来自 `Accepted` 的文字，并作为 `Blocks` 传回。
- 在多次运行之间保持块 id 稳定，以便编辑找到它们的块。
- 想让演示更快时使用 `Speed`，而不是缩短编辑。

### 常见问题

**用户可以在文档中输入吗？** 不可以。这些块是只读的；真正的编辑请使用编辑器自带的协作光标。

**如果编辑指向的块或词不存在会怎样？** 该编辑会被跳过。

## API 参考

<!-- api: AgentCursor -->

<!-- api: AgentCursorBlock -->

<!-- api: AgentCursorEdit -->

<!-- api: AgentCursorCollaborator -->

<!-- api: AgentCursorLabels -->

<!-- api: AgentCursorSimulation -->

<!-- api: AgentCursorDocumentEventArgs -->

<!-- api: AgentCursorPhaseChangedEventArgs -->

## 无障碍

- 一个礼貌级别的实时区域播报每一步：阅读、当前编辑、编辑就绪、已接受、已撤销以及错误。模拟运行会附加 "Simulated, nothing was saved"。
- 光标、选区和着色是装饰性的，对辅助技术隐藏；文档仍然是真实的文字块。
- 状态从不只依赖颜色：每个结束状态都同时带有图标和文字，待审阅的改动在审阅栏中计数。
- 焦点在卡片内或指针停在卡片上时，循环播放的模拟会停在审阅状态，这样没人会在伸手去点时失去审阅栏。
- 每个词都在 `AgentCursorLabels` 中。

### 键盘

| 按键 | 操作 |
|---|---|
| Tab | 在页头的 Ask、Stop、Run again 或 Retry 之间移动，然后到审阅栏中的 Undo 和 Accept changes |
| Enter、Space | 激活获得焦点的按钮 |

## 动效

| 变化 | 完整 | 减弱 | 关闭 |
|---|---|---|---|
| 光标移动 | 沿一条平缓的弧线以缓入缓出曲线滑动，按距离 280 到 600ms | 直接跳到目标 | 直接跳到目标 |
| 光标出现或离开 | 从头像中长出并在 200ms 内淡入淡出 | 淡入淡出 | 立即 |
| 选区 | 随拖动逐行填充 | 逐行填充 | 逐行填充 |
| 改写 | 旧文字在 110ms 内淡出，然后每 34ms 写入 3 到 6 个词，每段在 140ms 内淡入 | 新文字整体出现 | 新文字整体出现 |
| 块高度 | 以无回弹的 0.24s 弹簧跟随内容；插入的块从零高度展开，然后文字淡入 | 立即 | 立即 |
| 状态行 | 交叉淡化，100ms 淡出、160ms 淡入，智能体工作时带微光 | 短暂交叉淡化 | 立即 |
| 审阅栏 | 上升 8px 并在 200ms 内淡入 | 淡入 | 立即 |
| 循环与同事漂移 | 审阅后循环；同事阅读时轻微漂移 | 关闭 | 关闭 |

卡片不在屏幕上时，播放会暂停。光标通过渲染变换移动，因此移动从不触发布局。

## 相关组件

- `AgentStatus`：显示整个运行的实时状态。
- `ToolCall`：显示智能体发起的工具调用。
- `AgentPlan`：显示智能体行动前写下的计划。
