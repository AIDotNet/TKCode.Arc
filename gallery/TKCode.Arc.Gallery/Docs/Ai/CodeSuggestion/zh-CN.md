---
title: Code suggestion
summary: AI 代码修改建议：模型提出的改动以行内差异块的形式流式进入一个小型编辑器面板，由人逐个接受或拒绝，也可以用 Cmd 或 Ctrl + Enter 一次全部接受。
source-hash: 37c31166bfe2
---

<Demo name="Preview" />

## 何时使用

`CodeSuggestion` 在一个小型编辑器面板中，以行内差异的形式展示 AI 对单个文件提出的修改。标题栏写明模型和用户的要求；面板显示文件，每处改动是一个差异块：删除的行以红色底并带减号，新增的行以绿色底并带加号。新行逐行流入，随后每个差异块下方出现"拒绝"和"接受"，底栏提供"全部拒绝"和"全部接受"。被接受的差异块以平滑的高度变化并入文件。

- **编辑器中的行内修改**：模型改写用户正在打开的文件的一部分。
- **批准智能体的文件改动**：在写入之前审阅，并清楚记录保留了哪些内容。
- **配置或 SQL 改动预览**：每一行改动都应在生效前被读到。
- **落地页和文档**：用一个逼真、自动播放的修改来展示 AI 编程功能。

## 何时不用

- **跨多个文件的大型差异或左右对比**：使用专门的差异工具；这张卡片只用于一个文件和少量差异块。
- **没有改动的代码**：使用 `CodeBlock`。
- **不是文件修改的智能体步骤**：使用 `ToolCall`。
- **一般性的操作审批**（邮件、付款、部署）：使用审批卡片，而不是代码审阅。

## 用法

把文件放进 `Code`，把改动放进 `Hunks`，并设置 `Status`。每个 `CodeSuggestionHunk` 从从零开始计数的第 `Start` 行起，用它的 `Insert` 行替换 `Remove` 行（remove 为 0 时在 `Start` 之前插入，insert 为空时表示删除）。与前面某个差异块重叠的差异块会被忽略。既没有代码也没有差异块时，卡片显示一个空文件。

```xml
<arc:CodeSuggestion FileName="load.ts"
                    FilePath="src/api"
                    Instruction="Time out slow requests"
                    Code="export async function load(url: string) {&#10;  const res = await fetch(url);&#10;  return res.json();&#10;}">
    <arc:CodeSuggestionHunk Id="timeout" Start="1" Remove="1" Summary="Add a 10s timeout"
                            InsertText="  const res = await fetch(url, { signal: AbortSignal.timeout(10_000) });" />
</arc:CodeSuggestion>
```

```csharp
// 每个差异块都有了决定后再写入文件；卡片自己从不写入任何内容。
private async void OnResolved(object? sender, CodeSuggestionResolvedEventArgs e)
{
    await File.WriteAllTextAsync(path, e.Code);
}
```

如果希望随着用户的操作逐步写入，每接受或拒绝一个差异块都会触发 `DecisionMade`，并附带文件当前的内容。`GetCode()` 随时返回同样的文本，`Accept`、`Reject`、`AcceptAll` 和 `RejectAll` 可在代码中做出决定。

### 结构

1. **标题栏**：着色方块中的模型标记，作为标题的用户要求，以及其下方的模型名称。
2. **文件标签**：两个字母的语言标记、文件名、所在文件夹（卡片宽度达到 520px 时显示）以及实时的 +/− 计数。
3. **代码**：固定 20px 高的行，带行号；待定差异块的删除行为红色并带减号，新增行为绿色并带加号。待定的新增行还没有行号。
4. **审阅栏**：每个待定差异块下方，显示它的摘要以及"拒绝"和"接受"。
5. **底栏**：状态（一个图标加一段文字）以及操作：停止、全部拒绝与全部接受、重新生成或重试。

### 流式

<Demo name="Streaming" />

模型读取文件时把 `Status` 设为 `Thinking`，然后设为 `Streaming`，并随着令牌到达扩充 `Hunks`：先添加一个没有行的差异块，再用 `AppendLine`（或替换 `Insert`）追加行。每个新行展开到自己的行高并淡入。回答完整后把 `Status` 设为 `Ready`，此时每个差异块都会出现审阅栏。回到 `Thinking` 会清除之前的决定。

`AllowsStop` 会在模型读取或写入时显示"停止"，并引发 `StopRequested`：中止你的请求并设置状态。`AllowsRegenerate` 会在审阅完成后显示"重新生成"，出错后显示"重试"，并引发 `RegenerateRequested`。

### 状态

| 状态 | 代码 | 底栏 |
|---|---|---|
| `Thinking` | 原始文件 | 旋转图标和闪烁的 "Reading fetch-invoice.ts"，停止 |
| `Streaming` | 正在增长的差异块 | 旋转图标和 "Writing changes"，停止 |
| `Ready` | 带审阅栏的差异块 | 圆点和 "3 changes to review"，全部拒绝和全部接受 |
| `Ready`，全部已决定 | 文件当前的样子 | 对勾和 "Applied 2 of 3 changes"，或叉号和 "Rejected all 3 changes"，重新生成 |
| `Error` | 原始文件 | 警示图标和 `ErrorText`，重试 |

### 窄卡片

<Demo name="Narrow" />

卡片填满容器，最宽 720px。长行在代码区域内横向滚动，而行号固定在左侧，每个审阅栏固定在可见宽度内，因此"接受"始终触手可及。卡片窄于 520px 时隐藏文件夹。窄于 440px 时，行号栏变窄，代码缩小到 12px，差异块摘要隐藏（按钮仍保留供屏幕阅读器使用的名称），底栏把状态叠放在通栏操作按钮上方。最窄可到 320px。

### 长文件

<Demo name="LongFile" />

`MaxCodeHeight` 限制代码区域的高度；更长的文件在其中滚动。`AreShortcutsEnabled="False"` 关闭键盘快捷键（此时"全部接受"不显示提示），`Model="{x:Null}"` 隐藏模型那一行。

### 模型与文件

`Model` 接受一个 `AgentModel`：它的 `Name` 显示在标题下方，它的 `Mark`（24 × 24 的描边图形）显示在标题栏的方块中，颜色为 `MarkBrush` 或文字颜色。`FileName` 根据扩展名选择高亮语言；库中代码高亮器认识的语言都会着色，其他语言以普通文字颜色显示。`FileMark` 用你自己的内容替换两个字母的语言标记，`ShowsFileMark="False"` 则将其隐藏。

### 模拟

用于没有模型的演示或原型时，给卡片一个 `CodeSuggestionSimulation`，传入要播放的文件和差异块，文件名、指令和模型照常设置在卡片上。卡片没有自己的代码和差异块时就播放这段模拟：读取文件 0.8 秒，流入差异块（三个约需 1.6 秒），然后自行接受和拒绝，结束约 2.6 秒后重播。卡片内的任何指针、按键或焦点都会把这一轮交给用户控制；`Restart()` 会开始新的一轮。`AutoPlay="False"` 保留流式过程，但把审阅留给用户。设置 `Code` 或 `Hunks` 后模拟即结束。预览中的模型是真实的模型，配以中性的标识，不会绘制任何品牌标记。

```xml
<arc:CodeSuggestion FileName="greet.ts" Instruction="Greet by name">
    <arc:CodeSuggestion.Simulation>
        <arc:CodeSuggestionSimulation Code="export const greet = () =&gt; &quot;Hello&quot;;" />
    </arc:CodeSuggestion.Simulation>
</arc:CodeSuggestion>
```

```csharp
card.Simulation = new CodeSuggestionSimulation
{
    Code = recordedFile,
    Hunks = [new CodeSuggestionHunk("greet", 0, 1, "export const greet = (name: string) => `Hello, ${name}`;")],
};
```

### 自定义文案

`Labels` 包含卡片显示或朗读的每一个词。替换整个对象即可本地化或改写：

```csharp
card.Labels = new CodeSuggestionLabels
{
    Accept = "Übernehmen",
    Reject = "Verwerfen",
    ToReview = count => $"{count} Änderungen offen",
};
```

### 减少动态效果

开启减少动态效果时，卡片直接显示已完成的建议，没有流式过程和自动播放，折叠立即完成。决定仍然生效，状态也照常更新。

### 内容准则

- 为每个差异块写一个两到六个词的摘要（"Add retry limits"）；它会成为屏幕阅读器读出的按钮名称。
- 让差异块保持有序且互不重叠。从统一差异格式映射时，`Start` 是第一行删除行（或插入位置）从零开始的行号，`Remove` 是删除的行数，`Insert` 是新增的行。
- 内容是示例时，请在周围的文案中说明。

### 最佳实践

- 在 `Resolved` 中写入文件，或在 `DecisionMade` 中逐步写入；不要假定建议已经被应用。
- 通过追加 `Insert` 来流式更新；不要在每个令牌到达时重建差异块列表。
- 把 `StopRequested` 接到中止请求的逻辑上。

### 常见问题

**模型还在写入时，用户能做决定吗？** 不能：状态变为 `Ready` 后才会出现审阅栏。

**重叠的差异块会怎样？** 触及前面某个差异块已改动的行的差异块会被忽略。

## API 参考

<!-- api: CodeSuggestion -->

<!-- api: CodeSuggestionHunk -->

<!-- api: CodeSuggestionSimulation -->

<!-- api: CodeSuggestionLabels -->

<!-- api: CodeSuggestionDecisionEventArgs -->

<!-- api: CodeSuggestionResolvedEventArgs -->

## 无障碍

- 卡片是一个带标签的组（"Code suggestion: Add retry with backoff"），代码是以文件名命名的可聚焦区域。
- 在代码的描述中，每个待定的删除行或新增行都以 "Removed:" 或 "Added:" 开头，因此差异从不只依赖红色和绿色底色；可见的减号和加号也为视力正常的读者传达了这一信息。
- "接受"和"拒绝"以所属差异块命名（"Accept Retry 429 and 5xx with backoff"）。"全部接受"公开了它的快捷键。
- 一个礼貌型实时区域会朗读状态（"3 changes to review"）、每个决定（"Accepted: Add retry limits. 2 changes to review"）以及最终结果。模拟还会加上 "Simulated, nothing was written to disk."。流式过程只朗读一次，而不是每行一次。
- 状态总是图标加文字：读取和写入时是旋转图标，审阅时是圆点，已应用是对勾，已拒绝是叉号，出错是警示图标。
- 被按下的控件随其差异块或状态离开时，焦点不会落回页面。指针聚焦不显示焦点环；键盘聚焦使用 Arc 统一的焦点环。

### 键盘

| 按键 | 操作 |
|---|---|
| Tab | 先移到代码区域，再依次移到每个差异块的"拒绝"和"接受"，然后是底栏操作 |
| Enter、Space | 按下获得焦点的接受、拒绝、全部接受、全部拒绝、停止或重新生成；一个差异块决定后，焦点移到下一个待定差异块的"接受"，没有剩余时移到代码区域 |
| Cmd 或 Ctrl + Enter | 焦点在卡片内时，接受所有待定差异块 |
| Cmd 或 Ctrl + Backspace | 焦点在卡片内时，拒绝所有待定差异块 |
| 左方向键、右方向键 | 代码区域获得焦点时横向滚动长行 |

## 动效

| 变化 | 完整 | 减少 | 关闭 |
|---|---|---|---|
| 思考 | 约 0.8 秒，标签上有平缓的闪烁（模拟） | 无闪烁，模拟不流式 | 立即 |
| 差异块出现 | 其删除行在 240ms 内染成红色 | 立即 | 立即 |
| 新行流入 | 在 120ms 内展开到 20px 行高并淡入，每 38ms 一行（模拟） | 立即 | 立即 |
| 审阅栏出现 | 同时出现，使用临界阻尼弹簧（0.24 秒，无回弹） | 立即 | 立即 |
| 接受 | 删除行和审阅栏以 settle 弹簧（0.26 秒）折叠为零，下方代码随之上移；绿色底色和加号在 240ms 内淡出 | 立即 | 立即 |
| 拒绝 | 新增行和审阅栏以同样方式折叠；红色底色淡出 | 立即 | 立即 |
| 底栏状态 | 图标、文字和操作各自原地交叉淡化（淡出 100ms，淡入 160ms，文字上移 2px）；图标只在种类变化时切换 | 短暂交叉淡化 | 立即 |

行高固定为 20px，折叠在已知高度之间进行，因此动画过程中不需要测量任何内容。每一行按文本只排版一次，所以流入新行不会重新高亮其他行。模拟只使用单次计时器，卡片进入视口后才开始，滚出视口时暂停等待。

## 相关

- `CodeBlock`：展示没有改动的代码。
- `ToolCall`：不是文件修改的智能体步骤。
- `AgentPlan`：智能体在修改任何内容之前写下的计划。
