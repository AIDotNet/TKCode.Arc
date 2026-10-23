---
title: 提示词工作室
summary: 一张用于编辑提示词模板的卡片：变量以彩色芯片显示，可以用保存的测试用例试运行，并可对比和上线版本。卡片包含版本与模型菜单、变量字段、运行按钮以及显示回复的输出面板。
source-hash: da3db15ce44c
---

<Demo name="Preview" />

## 何时使用

提示词工作室面向维护 AI 功能背后提示词的人，而不是功能的最终用户。它把整个循环放在一张卡片上：编辑模板，用保存的测试用例试运行，查看版本之间有什么变化，并把其中一个版本上线。

- **AI 功能的管理员或设置页**：帮助台的客服回复提示词，或文档工具的摘要提示词。
- **审查提示词的改动**：上线之前，把打开的版本与另一个版本做逐词对比。
- **用真实示例试用提示词**：保存的测试用例填入变量，运行后显示回复及其用时、token 数和费用。
- **在提示词旁边选择模型和推理强度**：附带价格，使每次改动的成本一目了然。

## 何时不用

- **与助手的聊天**：使用 `AiChat`。
- **代码或自由文本编辑器**：模板编辑器只认识一种标记，即双花括号中的变量。
- **在数百个用例上评估提示词**：卡片一次只运行一个测试用例。
- **存储提示词**：卡片编辑的是草稿，并告诉你改动了什么；保存、版本和上线由你的代码负责。

## 用法

`PromptStudio` 派生自 Avalonia 的 `TemplatedControl`。给它提供 `Versions`、`Variables`、`TestCases` 和 `Models`，再设置 `RunHandler`；没有处理程序时，内置的模拟器会作答，卡片会注明这是模拟的。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:PromptStudio Title="Refund reply" MaxWidth="660" />
</UserControl>
```

```csharp
studio.Versions.AddRange(versions);                 // newest first
studio.Variables.AddRange(["buyer_name", "ticket_id"]);
studio.TestCases.AddRange(testCases);
studio.Models.AddRange(models);
studio.OpenVersion = studio.Versions[0];
studio.ProductionVersion = studio.Versions[0];
studio.RunHandler = async IAsyncEnumerable<string> (request, cancellationToken) => { /* yield text */ };
```

### 模板

模板包含一条系统消息和一条用户消息（分别是「System」和「User」标签页）。变量写在双花括号中，例如 `{{buyer_name}}`，在编辑器中显示为一个彩色芯片，芯片上只有变量名。
每个变量按 `Variables` 中的顺序取四种系列色之一；文本中找到但未列出的变量名，会依次添加到后面。

- 输入两个左花括号，会在插入点打开已知变量的列表：上下方向键移动，`Enter` 或 `Tab` 插入，`Escape` 关闭，输入文字会过滤列表；最后一行会把你输入的名称添加为新变量。输入变量名加两个右花括号也会生成芯片；粘贴的花括号内带空格的文本会被规范化。
- 芯片是一个整体：插入点会跳过它，`Backspace` 或 `Delete` 会整个删除它，复制它时写入的是花括号和变量名。
- 撤销和重做会恢复芯片；输入的文字按约一秒的停顿和空格分成若干步骤。
- 点按芯片会聚焦该变量的字段。

`Draft` 是正在编辑的模板（双向绑定），每次编辑后都会触发 `TemplateChanged`，请在那里保存它。编辑器右上角显示所显示部分的 token 估算；设置 `TokenEstimator` 可以使用你自己的分词器。

### 变量与测试用例

每个变量都有一个字段，字段前是一个带变量颜色的圆点和标签。聚焦某个字段会高亮编辑器中它的芯片；每次击键都会使其闪烁；值为空时，芯片以虚线轮廓绘制。选择一个测试用例会把值重置为该用例自己的值，你刚输入的值会被丢弃。`Values` 保存当前的值。

<Demo name="Minimal" />

### 运行

按下运行（或在卡片的任何位置按 Ctrl+Enter；在 Mac 上为 Cmd+Enter），会用这些值渲染模板，并调用 `RunHandler`，它以文本流的形式返回回复。输出面板依次显示 "Thinking"，然后是 "Writing"，回复以整词的形式出现，最后是 "Reply"，页脚类似 "1.57 s, 245 tokens, $0.0014"；费用由模型的每百万 token `InputPrice` 和 `OutputPrice` 计算得出，若模型没有价格则不显示。运行期间按钮变为 Stop；停止会保留已经写出的内容。
处理程序抛出异常时，会显示一个错误框，并带有 Retry 按钮（`RunFailed` 允许你改写错误信息）。同一时间只运行一次：开始新运行或按下 Stop，都会取消前一次运行的令牌。`RunState`、`LastResult`、`RunStarted` 和 `RunCompleted` 提供其余信息。

### 版本

版本标签（"v4, Production"）会打开版本菜单：每个版本都显示其作者、提交说明和时间，并对选中的版本提供两个操作。

- **Compare**（对比）会把编辑器变成打开版本与所选版本之间的逐词对比：插入的词以颜色标示，删除的词加删除线，并显示 +17 −1 这样的计数以及 "Back to editing" 按钮；标签页上的圆点表示哪一部分发生了变化。`IsCompareMode` 和 `CompareVersion` 是双向的，按 Escape 即可退出。
- **Promote to production**（上线）会把 Production 标记移到所选版本。`ProductionVersion` 立即改变，并引发 `Promoted`，其参数为该版本和之前的版本；把 `Cancel` 设为 true 可拒绝上线，此时工作室会回滚。菜单中的 Undo 会把两者换回，并再次引发 `Promoted`，其 `IsUndo` 为 true。

<Demo name="Compare" />

### 模型与推理强度

模型标签会打开 `Models` 菜单，其中显示每百万 token（输入和输出）的价格，以及一行推理强度。选择模型会把推理强度重置为该模型的默认值，并让菜单保持打开；标明为示例的价格会在菜单底部注明。

### 文案

每个词都来自 `Labels`（`PromptStudioLabels`）：要翻译或改写时，替换该实例即可；带格式的文字是函数。`Now` 设置相对时间（如 "last week"，上周）的计算基准时刻。

### 文案规范

- **变量名**：小写，用下划线分隔，并说明应填入什么（`buyer_name`）。
- **测试用例**：标签要简短（最多三到四个词）；它们共用同一个分段控件。
- **提交说明**：用几个词说明改了什么。
- **价格**：要说明它是示例。

### 最佳实践

- 在 `TemplateChanged` 中保存；当服务器拒绝上线时，用 `Cancel` 回滚该次上线。
- 在运行处理程序中传递取消令牌，使 Stop 真正停止运行。
- 保留覆盖提示词边界情况的测试用例，例如缺少某个值的情况。
- 上线前先对比：差异就是审查。

### 常见问题

**为什么运行时显示 simulated？** 没有设置 `RunHandler`，因此回答的是内置的模拟器。

**为什么芯片是虚线的？** 它的变量在当前测试用例中没有值。

**我能使用自己的变量语法吗？** 卡片使用双花括号。

## API 参考

<!-- api: PromptStudio -->

<!-- api: PromptVersion -->

<!-- api: PromptAuthor -->

<!-- api: PromptTestCase -->

### 事件

| 事件 | 触发时机 | 参数 |
|---|---|---|
| `TemplateChanged` | 模板被编辑 | `Template` |
| `RunStarted` | 运行开始 | `Request` |
| `RunCompleted` | 运行结束：完成、停止或失败 | `State`、`Result` |
| `RunFailed` | 处理程序抛出异常；修改 `Message` 可改写错误信息 | `Exception`、`Message` |
| `Promoted` | 某个版本被上线或撤销上线；设置 `Cancel` 可回滚 | `Version`、`Previous`、`IsUndo` |
| `VariableAdded` | 从编辑器添加了一个变量 | |

### 运行

| 类型 | 说明 |
|---|---|
| `PromptRunHandler` | `(request, cancellationToken)`，以 `IAsyncEnumerable<string>` 的形式返回回复 |
| `PromptRunRequest` | 原始模板和渲染后的模板、各个值、模型、推理强度、测试用例 id 和版本 id |
| `PromptRunResult` | 文本、经过的时间、估算的 token 数、费用，以及是否为模拟回复 |
| `PromptTemplate` | 系统消息和用户消息 |

## 无障碍

- 卡片是一个以提示词命名的区域。标签页是一个标签列表；变量字段、测试用例和输出面板是有名称的分组。
- 编辑器对辅助技术而言是一个文本框：芯片读作带花括号的变量名，变量列表会播报有多少个名称匹配。
- 版本和模型标签是按钮，它们会报告自己打开的是一个对话框；版本是单选项；Production 版本会被明确命名为 Production。
- 实时区域（礼貌级别）会播报：运行已开始、回复已就绪、运行已停止或失败并附上原因、已添加变量、版本已上线或上线已撤销、对比视图已打开，以及回复已复制。流式输出的词不会逐个播报。
- 状态从不只依靠颜色：虚线芯片同时出现在缺失列表中，错误有图标和一句说明，Production 有文字标示。
- 减弱动效会保留每一种状态和每一条播报。

### 键盘

| 按键 | 操作 |
|---|---|
| `Ctrl+Enter` / `Cmd+Enter` | 在卡片的任何位置运行提示词 |
| `Tab` | 标签、编辑器、变量字段、测试用例、运行、复制 |
| 标签页和测试用例上的 `ArrowLeft` / `ArrowRight` | 移动并选中 |
| 编辑器中输入两个左花括号 | 打开变量列表 |
| 列表中的 `ArrowUp` / `ArrowDown` | 移动；到达两端时循环 |
| 列表中的 `Enter` 或 `Tab` | 插入变量 |
| 芯片旁的 `Backspace` / `Delete` | 整个删除它 |
| `Ctrl+Z`、`Ctrl+Shift+Z` | 撤销和重做，包括芯片 |
| `Escape` | 关闭列表或菜单；在对比视图中，返回编辑 |

## 动效

- 版本和模型标签以弹簧方式取得所需的宽度，并交叉淡入淡出其标签；标签页的高亮和测试用例的高亮以快速弹簧滑动。
- Production 标记在版本菜单的各行之间滑动；菜单从其标签处生长出来。
- 输出面板以弹簧跟随其内容，在重新运行时保持原有高度；回复以整词的形式出现，每一步在 150 毫秒内淡入。
- 芯片在其字段被编辑时闪烁；token 计数的数字会滚动变化。
- 减弱动效会去掉弹簧和移动，保留文字的节奏但不淡入；关闭动效时，每项变化立即生效。

| 变化 | 完整 | 减弱 | 关闭 |
|---|---|---|---|
| 标签文字和宽度 | 交叉淡入淡出，宽度弹簧 | 120 毫秒交叉淡入淡出 | 立即 |
| 回复文字 | 整词，150 毫秒淡入 | 相同的步骤，无淡入 | 相同的步骤，无淡入 |
| 输出面板高度 | 弹簧，重新运行时保持 | 立即 | 立即 |
| 芯片闪烁 | 短暂脉冲 | 无 | 无 |

## 相关组件

- `AiSidePanel`：页面旁边的助手。
- `ModelPicker`：只需要单独选择模型时使用。
- `TokenTextEditor`：提示词编辑器所基于的标记文本编辑器。

| 组件 | 适用场景 |
|---|---|
| `AiSidePanel` | 被编辑的对象是一页内容 |
| `ModelPicker` | 只需要选择模型 |
| `TokenTextEditor` | 你需要在文本编辑器中使用自己的标记 |
