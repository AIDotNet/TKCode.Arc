---
title: 智能体构建器
summary: 在实时测试对话旁设置一个 AI 智能体：名称和说明、带 token 计数的指令、模型、带开关的工具、在原处建立索引的知识来源、带"放弃"的保存栏，以及带状态标签的"发布"。
source-hash: 5cabb78a31b0
---

<Demo name="Preview" />

## 何时使用

`AgentBuilder` 是人们创建或编辑 AI 智能体、并在别人使用之前先试一试的页面。左边的卡片按人们做决定的顺序放着定义智能体的一切：它是谁（名称、说明），它被告知什么（指令），由什么来思考（模型），它能做什么（工具），以及它知道什么（知识来源）。右边的卡片是与这个智能体的对话。修改在保存之前一直是草稿；"发布"把已保存的版本交给使用这个智能体的人，标签会说明他们用的是否是最新版本。测试对话会记住它开始时的设置，所以一条回复不会混用两个版本：草稿改变后，会出现一条提示，让你用新设置重新开始对话。

- **AI 产品、内部工具平台或客服台中的智能体或助手编辑器**。
- **调整提示词和工具**：改一条指令或切换一个工具，重新开始对话，再问一次同样的问题。
- **依靠知识的智能体**：添加链接或上传文件，看着每一项建好索引，之后智能体才能搜索它。

## 何时不用

- **只需要编辑提示词并在测试用例上运行**：使用 `PromptStudio`，它可以比较版本和输出。
- **只需要选择模型**：使用 `ModelPicker`。
- **给最终用户的对话**：使用 `AiChat`。这里的测试对话是给搭建智能体的人用的，不保留历史。
- **带分支和步骤的工作流**：一个带工具的智能体和多个智能体组成的流程是两回事；请使用图形编辑器。

## 用法

这个区块派生自 Avalonia 的 `TemplatedControl`。没有数据时，它会显示一个虚构的客服智能体 Harbor（三个模型、五个工具、三个来源，其中一个索引失败），并模拟保存、发布、建立索引和测试回复，因此在接入任何东西之前就可以试用。

```xml
<arc:AgentBuilder AgentName="{Binding Name}"
                  AgentDescription="{Binding Description}"
                  Instructions="{Binding Instructions}"
                  SelectedModelId="{Binding ModelId}" />
```

在代码中给它你自己的目录（`Models`、`Providers`、`Tools`、`Sources`、`Suggestions`）；只要在它显示之前设置了其中任意一项、名称或指令，就不会出现示例。设置 `ShowsSampleData="False"` 可以得到一个从空白开始的构建器。

<Demo name="NewAgent" />

### 结构

- **页头**：一个显示名称首字母的头像（颜色随名称而定）、名称、状态标签（Draft、Published、Unpublished changes）、说明和 **Publish**。
- **身份**：名称（保存时必须填写）和一行说明。
- **指令**：一个多行输入框，带字符数和大致的 token 数（大约四个字符一个 token）。超过 `InstructionsMaxLength`（8,000）后，输入框会显示错误，草稿无法保存。
- **模型**：`ModelPicker` 的模型胶囊；测试对话的模型按钮修改的是同一个设置。
- **工具**：每个 `AgentBuilderTool` 一行，带一个开关；按在这一行的任何地方都会切换它。计数说明有几个已打开。
- **知识**：每个 `AgentBuilderSource` 一行，显示类型、大小和状态：建立索引时显示一个圆环，就绪时显示对勾和段落数，失败时显示原因和 **Retry**，另有 **Remove**。列表下方是链接输入框，以及 **Add link** 和 **Upload files**。
- **保存栏**："All changes saved" 或 "Unsaved changes"，带 **Discard** 和 **Save**；保存失败时，消息显示在这里。
- **测试对话**：一个 `AiChat`，回复来自 `Responder`；草稿改变后会出现一条提示和 **Restart**。

### 草稿、已保存和已发布

屏幕上的设置就是草稿（`Config`）。`SavedConfig` 是已保存的内容；构建器第一次显示时如果它为 null，屏幕上的设置就算作已保存。`IsDirty` 比较这两者，**Discard** 会恢复已保存的设置。`PublishedConfig` 是人们正在使用的内容：null 表示 Draft，与已保存的设置相同表示 Published，其他情况都是 Unpublished changes。需要时 **Publish** 会先保存。这三个快照都是 `AgentBuilderConfig` 记录；当名称、说明、指令、模型、已打开的工具和来源 id 都相同时，两个快照相等。

```csharp
builder.SaveHandler = async (config, cancellation) => await api.SaveAgentAsync(agentId, config, cancellation);
builder.PublishHandler = async (config, cancellation) => await api.PublishAgentAsync(agentId, cancellation);
builder.Load(await api.GetAgentAsync(agentId));   // 作为已保存的内容显示
```

抛出异常的处理程序会保留草稿，并显示 "Not saved: …" 和异常消息。`Ctrl+S`（`Cmd+S`）用于保存。`SaveAsync`、`PublishAsync` 和 `Discard` 在代码中做同样的事；`ConfigChanged`、`Saved` 和 `Published` 报告每一步。

<Demo name="Wired" />

### 知识来源

**Add link**（或在输入框中按 Enter）只接受完整的 `http` 或 `https` 地址，其他内容或已是来源的页面都会被拒绝，并在输入框下方显示消息。**Upload files** 调用 `PickFilesHandler`，未设置时使用窗口的文件选择器（两者都没有时按钮会隐藏）。新的来源和重试的来源通过 `IndexHandler` 建立索引：

```csharp
builder.IndexHandler = async (source, progress, cancellation) =>
{
    var job = await api.StartIndexAsync(source.Url, cancellation);
    await foreach (var step in api.WatchAsync(job, cancellation))
    {
        progress.Report(step.Fraction);
    }
    return job.PassageCount;
};
```

抛出异常会把来源标记为失败并显示消息；Remove 会取消这项工作。没有处理程序时会模拟建立索引（大约 2.4 秒）。`AddSources` 添加你自己的代码收到的来源，`RemoveSource` 和 `RetrySource` 作用于单个来源，`SourceIndexed` 报告每个完成或失败的来源。

### 测试对话

对话使用它第一条消息时的设置来回答（`TestConfig`）。修改之后，`IsTestStale` 变为 true，提示会说明这一点；**Restart**（或对话自己的新对话按钮，或 `RestartTest()`）会清空对话，下一条消息将使用草稿。实现 `IAgentBuilderResponder` 即可接入你的智能体运行环境：

```csharp
public sealed class RuntimeResponder(AgentRuntime runtime) : IAgentBuilderResponder
{
    public async IAsyncEnumerable<AiChatStreamEvent> RespondAsync(AgentBuilderTestRequest request,
        [EnumeratorCancellation] CancellationToken cancellation)
    {
        await foreach (var delta in runtime.StreamAsync(request.Config, request.Chat.Messages, cancellation))
        {
            yield return new AiChatTextDelta(delta);
        }
    }
}
```

请求中带有设置、模型、已打开的工具和对话内容。没有设置回复者时，由 `AgentBuilderSimulatedResponder` 回答：它会搜索已就绪的来源，在问题需要时运行一个已打开的工具（"Where is order 4417?" 会运行 Order lookup），并总结智能体的设置，所以每个开关都有看得见的效果。

### 窄容器

布局跟随构建器自身的宽度：低于 760 px 时，测试对话移到设置下方；低于 520 px 时，各部分的内边距会收紧。侧边面板或手机上用的是同一个构建器。

<Demo name="Narrow" />

### 文案

所有文字都来自 `Labels`（`AgentBuilderLabels`），包括测试对话的（`Chat`）和模型菜单的（`Model`）。带数值的文字是函数，因此可以调整语序。`CultureName` 决定计数和大小的格式。

### 内容指南

- **名称**：简短而具体，因为人们会在列表中看到它（"Harbor"、"Release notes writer"）。
- **说明**：一行文字说明智能体能帮什么忙，结尾不加句号。
- **指令**：规则列表比长段落更好读；写明智能体不能做什么。
- **工具说明**：一行文字说明智能体能用这个工具做什么，而不是它如何工作。

## API 参考

<!-- api: AgentBuilder -->

<!-- api: AgentBuilderTool -->

<!-- api: AgentBuilderSource -->

### AgentBuilderConfig

| 属性 | 说明 |
|---|---|
| `Name`、`Description`、`Instructions` | 文字设置 |
| `ModelId` | 所选模型的 id，或 null |
| `ToolIds` | 已打开的工具的 id，按 `Tools` 的顺序 |
| `Sources` | 知识来源，按顺序 |

### 事件

| 事件 | 何时触发 | 参数 |
|---|---|---|
| `ConfigChanged` | 草稿改变 | `Field`、`Config`、`IsDirty` |
| `Saved` | 保存成功 | `Config` |
| `Published` | 发布成功 | `Config` |
| `SourceIndexed` | 一个来源完成索引或失败 | `Item` |

## 无障碍

- 设置卡片是一个名为 "Agent settings" 的表单，每个部分有三级标题；智能体的名称是二级标题。
- 每个输入框都有标签或无障碍名称；指令输入框把提示作为帮助文字。错误（名称为空、指令太长、链接无效）以输入框消息的形式出现，用文字表达，而不只靠颜色。
- 每个工具的开关带有工具的名称和说明；它是这一行唯一的 Tab 停留点。
- 来源行会读出名称和状态（"returns-and-repairs.pdf, File · 412 KB · Ready · 86 passages"）；Remove 和 Retry 的名称带有来源（"Remove help.tidewater.example/fitting"）。
- 一个礼貌的实时区域会播报保存、放弃、发布、切换的工具、添加、移除、就绪或失败的来源、过期的测试对话和重新开始。保存栏的状态也是一个礼貌的实时区域。
- 焦点会去往人们接下来要看的地方：移除来源后，到下一行的 Remove（或链接输入框）；重试后，到这一行的 Remove；放弃后，到名称输入框；重新开始后，到对话的消息框。

### 键盘

| 按键 | 操作 |
|---|---|
| `Tab` | Publish、各个输入框、模型胶囊、每个工具的开关、每个来源的 Retry 和 Remove、链接输入框、Add link、Upload、Discard、Save，然后是测试对话 |
| `Space` | 切换获得焦点的工具开关 |
| 在链接输入框中按 `Enter` | 添加链接 |
| `Ctrl+S` / `Cmd+S` | 保存草稿 |
| 在模型胶囊上按 `Enter`、`Space` 和 `ArrowDown` | 打开模型菜单 |

## 动效

- 保存栏的状态在已保存、未保存、保存中和失败之间变化时交叉淡入淡出（100 ms 淡出，160 ms 淡入，上升 2 px）。
- 过期提示淡入，它的高度以弹簧方式在对话上方展开；重新开始后它会再次收起。
- 来源的圆环跟随索引进度，每一帧追上一点；就绪时对勾取代它。
- 悬停时各行在 160 ms 内着色；工具打开时，它的图标方块换成强调色；名称改变头像颜色时，颜色会交叉淡入淡出。
- 测试对话像 `AiChat` 一样流式显示，按钮、开关和输入框保留各自的动效。
- 减少动效时所有状态都会保留：提示只淡入而不展开，圆环直接跳到进度，颜色立即改变。

| 变化 | 完整 | 减少 | 关闭 |
|---|---|---|---|
| 保存栏状态 | 交叉淡入淡出并上升 2 px | 120 ms 交叉淡入淡出 | 立即 |
| 过期提示 | 淡入，高度以弹簧展开 | 淡入 | 立即 |
| 索引圆环 | 追赶进度 | 直接跳到 | 直接跳到 |
| 行和图标的颜色 | 160 ms | 立即 | 立即 |

## 响应式

- `AgentBuilder` 按自身宽度变化，而不是按窗口宽度。从 760 px（`StackWidth`）起，对话位于设置旁边，约占宽度的 46%，至少 340 px 宽、至少 560 px 高（随设置卡片一起变高）。低于 760 px（`:stacked`）时，对话移到设置下方，高度为 540 px。
- 低于 520 px（`:compact`）时，各部分的内边距从 24 收紧到 16。
- 名称和说明过长时以省略号结尾；工具说明、来源状态和过期提示会换行。
- 让构建器横向拉伸（`HorizontalAlignment="Stretch"`），并把它放进可以滚动的页面：设置卡片的高度与内容相同。

## 相关组件

- `ModelPicker`：只选择模型。
- `PromptStudio`：在保存的测试用例上编辑提示词。
- `AiChat`：给你的用户看的对话。
- `AgentMemory`：助手记住了关于一个人的什么。

| 组件 | 何时选择 |
|---|---|
| `PromptStudio` | 你要在测试用例上比较提示词版本 |
| `ModelPicker` | 只需要选择模型 |
| `AiChat` | 人们与完成的智能体对话 |
| `SettingsPage` | 设置与智能体无关 |
