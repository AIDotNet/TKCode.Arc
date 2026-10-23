---
title: AI 输入框
summary: AI 应用的提示输入框：文本区域会随内容增长，"/" 命令和 @ 提及会变成令牌，附件原地上传，工具开关、模型与推理强度菜单、字数计数器，发送按钮在生成回复时变为停止按钮。
source-hash: 922e5fc99c9d
---

<Demo name="Preview" />

## 何时使用

AI 输入框是人们撰写提示的地方。它是一个圆角的整体表面，上方是文本区域，下方是工具栏，从输入到发送之间的一切都由它负责：命令、文件、工具、模型和字数上限。

- **聊天或助手应用**：位于对话底部的输入框，生成回复时显示停止按钮。
- **工具中的提示框**："/" 命令对应你的产品中的操作，"@" 提及对应它可以读取的文件或文档。
- **随提示一同提交文件的表单**：选择、粘贴或拖放文件；文件原地上传，消息中只带上已就绪的文件。
- **提示旁边的模型选择**：工具栏中有一个模型胶囊，附带推理强度设置，使选择留在工作发生的地方。

## 何时不用

- **简短的单行字段**：使用 `Input`；搜索请使用 `SearchField`。
- **带 @ 人员的评论框**：使用 `MentionInput`，它提供人员、频道和序列化。
- **带有你自己的令牌和表面的文本编辑器**：基于 `TokenTextEditor` 构建，它就是输入框内部的编辑器。
- **在设置页中选择模型**：使用 `ModelPicker`；输入框中的胶囊用于撰写时的选择。
- **运行模型**：输入框只报告消息。由你的代码调用模型，并告知何时正在生成回复。

## 用法

`AiComposer` 派生自 Avalonia 的 `TemplatedControl`。把人们可以使用的内容（命令、上下文项、工具、模型）交给它，并处理 `Sent`。发送后它会自行清空；消息中包含文本、令牌、已就绪的附件、模型、推理强度以及每个工具的状态。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:AiComposer Placeholder="Ask a question, type / for actions or @ for a file">
        <arc:AiComposer.Commands>
            <arc:AiCommand Id="recap" Name="recap" Description="Boil a doc or file down to its main points" />
            <arc:AiCommand Id="rewrite" Name="rewrite" Description="Rewrite in a clearer or friendlier tone" />
        </arc:AiComposer.Commands>
        <arc:AiComposer.ContextItems>
            <arc:AiContextItem Id="style-guide" Label="style-guide.pdf" Hint="Brand, 3.1 MB" Group="Files" />
        </arc:AiComposer.ContextItems>
    </arc:AiComposer>
</UserControl>
```

```csharp
composer.Sent += (_, e) =>
{
    var message = e.Message;               // Text, Tokens, Attachments, Model, Effort, Tools
    StartReply(message);
    composer.IsGenerating = true;          // the send button becomes a stop button
};
composer.StopRequested += (_, _) => CancelReply();
```

### 命令与提及

"/" 位于一行开头时，会打开 `Commands` 列表（按名称开头过滤）；"@" 位于一行开头或空格之后时，会打开 `ContextItems` 列表。该列表按 `MentionGroups` 的顺序分组（默认先 "Files"，再 "Docs"），并按标签或提示过滤。"@" 之前是 "(" 时不会打开列表。上下方向键可以移动（并循环），Enter 或 Tab 插入，Escape 关闭，列表从不获得焦点。插入的内容会变成一个令牌，后面跟一个空格；`Value.Tokens` 会列出它们及其偏移量。没有命令或没有项时，该符号只是普通文本。

`SetPrompt(text)` 为建议填充输入框：它所识别的命令和提及会变成令牌，插入点移到末尾，焦点移到文本。`InsertToken(kind, id, label)` 在插入点添加一个令牌。

<Demo name="Suggestions" />

### 附件

点击加号按钮会打开一个菜单，其中有 "Upload file"（上传文件）、你的 `AttachActions`（例如云盘、代码仓库、屏幕截图，如果你要求，还会在它们之间加一条分隔线），以及存在上下文项时的 "Mention a file or doc"（提及文件或文档）。文件也可以粘贴或拖放到输入框上。粘贴的内容达到 `LongPasteCharacters` 个字符或 `LongPasteLines` 行时，会变成一个 "Pasted text"（粘贴的文本）附件，而不是文本。

每个文件都会变成一个芯片，并原地上传：上传时显示带百分比的圆环，完成后显示对勾，然后出现移除按钮。超过 `MaxFileSize`（默认 30 MB）或不符合 `AcceptedFileTypes`（语法与 `FileDropzone.Accept` 相同）的文件会得到一个错误芯片，并附上原因；它可以被移除，也不会阻止发送。`UploadHandler` 负责上传：报告进度、遵守取消令牌、抛出异常即拒绝。没有处理程序时，文件会立即就绪。`IsAttachmentEnabled` 可以关闭加号、粘贴和拖放。

<Demo name="Attachments" />

### 工具、模型与推理强度

`Tools` 中的每个 `AiComposerTool` 都是工具栏中的一个开关（一个图标加一个标签，在较窄的输入框中标签会隐藏）；它的 `IsChecked` 是双向的，`ToolToggled` 报告变化。当 `Models` 不为空时，会显示一个胶囊，展示所选模型及其推理强度；它的菜单中有各个模型、一条分隔线，以及一行推理强度档位，选择后立即生效。`SelectedModel` 和 `Effort` 是双向的，`ModelChanged` 和 `EffortChanged` 报告变化；模型不提供的推理强度会移到它所提供的最接近的一档。

### 字数上限

`MaxLength`（默认 4000；为 null 时没有上限）会添加一个计数器：从 `CounterThreshold`（80%）开始显示剩余字符数，从 `WarningThreshold`（95%）开始切换为警告颜色；超过上限时显示超出的字符数，并阻止发送。文本从不会被截断。

<Demo name="Limit" />

### 发送与停止

发送按钮有四种状态：空（没有可发送的内容）、就绪、禁用（仍有上传在进行，或文本超过上限；其名称会说明原因）以及生成中。Enter 发送；Shift+Enter 添加一行；在输入法正在组合、列表处于打开状态或正在生成回复时，Enter 不做任何事（输入框仍可编辑，消息不会排队）。当 `IsGenerating` 为 true 时，按钮变为圆环中的一个停止方块，圆环跟随 `GenerationProgress` 变化且从不倒退；按下它或按 Escape 会触发 `StopRequested`。你的代码通过把 `GenerationProgress` 设为 1、再把 `IsGenerating` 设为 false 来结束一次回复。

`SendCommand` 和 `StopCommand` 为 MVVM 提供相同的功能。`SendState` 告诉你按钮处于四种状态中的哪一种。

### 最简输入框

<Demo name="Minimal" />

### 文案

所有文字都来自 `Labels`（`AiComposerLabels`）：占位符、按钮名称、计数器、芯片文字、拖放遮罩和播报。替换该实例即可翻译或改写；格式化的文字是函数。

### 文案规范

- **占位符**：用一行说明输入框接受什么，例如 "Ask a question, type / for actions or @ for a file"。
- **命令**：一个简短的动词（"recap"、"rewrite"），加上一行说明它做什么。
- **上下文项**：人们认得出的名称，以及一个能区分同名项的提示（路径、来源）。
- **芯片上的错误**：几个字，例如 "Over 30 MB"（超过 30 MB）、"Type not accepted"（不接受此类型）。

### 最佳实践

- 把输入框放在对话区的底部，并给它与对话区相同的宽度；它会根据自身宽度调整（低于 680 px 时为紧凑布局，低于 560 和 400 时更窄）。
- 开始生成回复时就设置 `IsGenerating`，并始终处理 `StopRequested`。
- 也要在服务器上验证：输入框只限制它发送的内容，它不是安全边界。
- 不要在回复运行时悄悄地排队消息；输入框不会这样做。

### 常见问题

**为什么发送后输入框被清空了？** 它总是清空文本、令牌和附件；工具、模型和推理强度保持不变。如果你需要恢复消息，请自行保留它。

**语音输入在哪里？** 本版本不包含语音输入。

## API 参考

<!-- api: AiComposer -->

<!-- api: AiCommand -->

<!-- api: AiContextItem -->

<!-- api: AiAttachment -->

<!-- api: AiComposerTool -->

<!-- api: AiComposerAction -->

### AiComposerMessage

| 属性 | 说明 |
|---|---|
| `Text` / `Tokens` / `Value` | 提示、其中的令牌，以及两者合起来 |
| `Attachments` | 已就绪的附件；失败或仍在上传的附件不包含在内 |
| `Model` / `Effort` | 所选的模型和推理强度 |
| `Tools` | 对每个工具 id，记录它是否开启 |

### 事件

| 事件 | 触发时机 | 参数 |
|---|---|---|
| `Sent` | 人们发送提示 | `Message` |
| `StopRequested` | 人们停止正在生成的回复 | |
| `AttachmentAdded` / `AttachmentRemoved` | 附件被添加、移除，或其上传被取消 | `Attachment` |
| `ToolToggled` | 工具被开启或关闭 | `Tool` |
| `ModelChanged` / `EffortChanged` | 人们选择模型或推理强度 | 旧值和新值 |

## 无障碍

- 文本区域有无障碍名称 "Message"（消息）；命令和提及列表是列表，其行以标题和提示命名，高亮的行通过文本区域播报，因此焦点始终留在人输入的地方。
- 发送按钮的名称说明它做什么，或说明为何不能发送："Send message"（发送消息）、"Type a message to send"（输入消息后发送）、"Wait for uploads to finish"（等待上传完成）、"Shorten the message by N characters"（将消息缩短 N 个字符）、"Stop generating"（停止生成）。
- 工具栏按钮都有名称；工具开关会报告是否处于按下状态；加号按钮会报告它打开一个菜单。
- 一个礼貌的实时区域会播报建议的数量、工具的开启或关闭、模型和推理强度的变化、已完成和失败的上传、移除、长粘贴、已发送的消息以及停止。
- 芯片上的按钮有名称："Cancel upload of report.pdf"（取消上传 report.pdf）、"Remove report.pdf"（移除 report.pdf）；移除最后一个芯片时，焦点移到文本。
- 焦点移到人接下来会看的地方：在菜单中按 Escape 后回到加号按钮，选择某个条目后回到文本，做出选择后回到模型胶囊。
- 状态从不只依靠颜色：错误会添加图标和原因，计数器会添加数字。
- 减弱动效会保留每一种状态和每一条播报。

### 键盘

| 按键 | 操作 |
|---|---|
| `Enter` | 发送（不在输入法组合中、列表打开或生成回复时） |
| `Shift+Enter` | 添加一行 |
| 行首的 `/`，空格之后的 `@` | 打开命令或提及列表 |
| 列表中的 `ArrowUp` / `ArrowDown` | 在各行之间移动；循环 |
| 列表中的 `Enter` 或 `Tab` | 把该行插入为令牌 |
| `Escape` | 关闭列表或菜单；没有打开的菜单时，停止正在生成的回复 |
| 加号或模型胶囊上的 `Enter`、`Space` 或 `ArrowDown` | 打开其菜单 |
| 推理强度行上的 `ArrowLeft` / `ArrowRight` | 改变推理强度 |
| `Tab` | 依次经过加号、工具、计数器、模型、发送按钮和芯片 |

## 动效

- 文本区域从 `MinLines` 到 `MaxLines`（1 到 8 行）逐行增长，耗时 160 ms 的缓动，然后开始滚动。
- 输入框获得焦点时边框加深（160 ms）；文件被拖到上方时，拖放遮罩淡入。
- 芯片以 0.92 的缩放同时淡入；圆环在弹簧上变为对勾，然后变为移除按钮。
- 发送按钮的箭头在弹簧上变为停止方块，同时其周围的圆环淡入；圆环跟随进度，且从不倒退。
- 菜单和建议列表从其按钮或插入点处生长；列表的高度在弹簧上跟随其行变化。
- 计数器和模型胶囊的文字在弹簧上取得所需的宽度。
- 减弱动效保留 120 ms 的淡入淡出，去掉移动和弹簧。关闭动效时，每项变化都立即生效。

| 变化 | 完整 | 减弱 | 关闭 |
|---|---|---|---|
| 输入框的高度 | 160 ms 缓动 | 立即 | 立即 |
| 边框和悬停颜色 | 160 ms | 120 ms | 立即 |
| 发送变为停止 | 形状在弹簧上变化，圆环淡入 | 120 ms 淡入切换 | 立即 |
| 芯片进入或离开 | 缩放并淡入淡出 | 120 ms 淡入淡出 | 立即 |
| 菜单和建议列表 | 从锚点处生长 | 120 ms 淡入淡出 | 立即 |

## 相关组件

- `ModelPicker`：用于在设置页中选择模型。
- `TokenTextEditor`：仅需要带令牌的文本区域时使用。
- `MentionInput`：用于带有人员和频道的评论。
- `AgentMemory`：用于查看助手记住了什么。

| 组件 | 适用场景 |
|---|---|
| `ModelPicker` | 模型的选择与撰写提示无关 |
| `TokenTextEditor` | 你只需要编辑器，而不需要整个输入框 |
| `MentionInput` | 提及的是评论中的人员和频道 |
| `Textarea` | 文本不包含命令、文件或工具 |
