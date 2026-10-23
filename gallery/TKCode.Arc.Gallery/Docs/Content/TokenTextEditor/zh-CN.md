---
title: 令牌文本编辑器
summary: 多行文本编辑器，其中的令牌（如命令和提及）作为单个单元处理，触发符会打开你自己列表的查询。
source-hash: f2eeb20780e2
---

<Demo name="Preview" />

## 何时使用

令牌文本编辑器是提示框、评论框或模板编辑器背后的书写区域：纯文本，其中部分内容不只是文字。`/recap` 命令或 `@file` 提及是一个整体单元，你可以整体移动、删除和选择，并且你的代码可以读回它的含义。

- **带命令和提及的提示框**：行首的“/”选择命令，“@”选择文件或人员。
- **带变量的模板编辑器**：以双大括号包住的变量（例如客户名称）不得逐字符编辑。
- **评论框**：提及会通知某人，同时文字保持可读。
- **你自己的提及控件**：`MentionInput` 构建于此编辑器之上；当令牌不是人员和频道时，直接使用该编辑器。

## 何时不用

- **普通多行文本**：使用 `Textarea`。
- **已有现成列表的 @人员 和 #频道**：使用 `MentionInput`，它自带建议列表和序列化。
- **富文本**（加粗、链接、图片）：令牌是唯一的格式，文本是纯文本。
- **单个简短的值**：使用 `Input`，或用 `TagInput` 处理值的列表。

## 用法

`TokenTextEditor` 派生自 Avalonia 的 `TemplatedControl`。它包含一个真正的 `TextBox`，其文字透明，并在其后绘制相同的文字且令牌着色，因此插入点、选择、输入法、撤销和剪贴板都由平台自身提供。它不绘制外框：请把它放在你自己的界面中。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:TokenTextEditor Placeholder="输入 / 选择命令，输入 @ 选择文件" MinLines="2" MaxLines="5">
        <arc:TokenTextEditor.Triggers>
            <arc:TokenTrigger Symbol="/" Kind="command" Position="LineStart" />
            <arc:TokenTrigger Symbol="@" Kind="mention" Position="AfterWhitespaceOrLineStart" />
        </arc:TokenTextEditor.Triggers>
    </arc:TokenTextEditor>
</UserControl>
```

### 触发符

`TokenTrigger` 是打开查询的符号：`Symbol`、它生成的令牌 `Kind`，以及它可以开始的位置（`Position`）：`Anywhere`、`AfterWhitespaceOrLineStart`（文本开头、行首或空格之后）或 `LineStart`（仅作为行的第一个字符）。查询在空格处结束，除非 `AllowSpacesInQuery` 或 `MaxQuerySpaces` 允许其中的部分空格；另一个触发符的符号同样会结束它。`AllowAfterOpeningBracket` 允许提及紧跟在“(”之后开始。

编辑器不显示列表。它会引发 `TriggerChanged`，并附带查询的状态（触发符、其后输入的文字，以及符号和插入点的矩形区域，用于放置你的列表）；你过滤自己的项目，并用 `InsertToken(kind, id, label)` 作答。这会用令牌和一个空格替换已输入的查询，并把插入点留在其后。`CancelTrigger()` 告诉编辑器你的列表已被关闭。

<Demo name="Preview" />

### 令牌

`Tokens` 列出当前文本中的 `TokenSpan`：你给出的 `Kind`、`Id` 和 `Label`，以及 `Start` 和 `End` 偏移量。当其前面的文字变化时它们会随之移动；当有人在令牌内部编辑时，它们会变回普通文本。用 `SetTextWithTokens(text, tokens)` 设置整个值，用 `InsertTokenAtCaret` 在插入点添加一个令牌，并在 `TextChanged` 中读取变化。

方向键会跨过一个令牌，令牌之后的 `Backspace` 与令牌之前的 `Delete` 会删除整个令牌，点击令牌内部会落在最近的边缘上，按住 Shift 配合方向键会逐字母选择（设置 `ExpandSelectionToTokens` 可整体选择令牌）。撤销和重做会随文字一起恢复被删除的令牌。

### 令牌样式

每种类型都可以有自己的颜色：在 `TokenStyles` 中添加一个带 `Kind`、`Fill` 和 `Foreground` 的 `TokenStyle`。没有对应项时，编辑器的 `TokenFill` 和 `TokenForeground` 生效。`TokenCornerRadius`、`TokenPadding` 和 `TokenOutset` 决定方框的形状；跨越换行的令牌默认会被切片（`TokenDecorationBreak`）。

### 高度与提交

高度随文字从 `MinLines` 到 `MaxLines` 行变化，之后文字滚动。Enter 会引发 `SubmitRequested`，并附带文本和令牌；Shift+Enter 添加一行，输入法组字期间永远不会提交。将 `SubmitOnEnter` 设为 false 可使 Enter 添加一行。将事件标记为已处理即可保留文本；要重新开始时，由你自己清除。

<Demo name="Growth" />

### 读取状态

`TriggerState` 是当前查询（或 null），`CaretIndex` 是插入点位置，`IsComposing` 表示输入法是否正在组字。`InsertText`、`Select`、`Clear`、`Undo` 和 `Redo` 的作用与其名称相同。

## API 参考

<!-- api: TokenTextEditor -->

<!-- api: TokenTrigger -->

<!-- api: TokenStyle -->

### TokenSpan

| 属性 | 说明 |
|---|---|
| `Kind` | 你给令牌的类型，例如 `command` 或 `mention` |
| `Id` | 你为令牌所代表的对象设定的标识符 |
| `Label` | 符号之后的文字 |
| `Start` / `End` | 令牌在文本中的范围（包括符号） |

### 事件

| 事件 | 引发时机 | 参数 |
|---|---|---|
| `TextChanged` | 文本或令牌发生变化 | `OldText`、`OldTokens`、`Text`、`Tokens` |
| `TriggerChanged` | 查询打开、变化或关闭 | `PreviousState`、`State`（关闭时为 null） |
| `TokenInserted` | `InsertToken` 生成了一个令牌 | `Token` |
| `SubmitRequested` | 未按 Shift 的 Enter，且没有输入法在组字 | `Text`、`Tokens`；设置 `Handled` 以保留文本 |

## 无障碍

- 对辅助技术而言它是一个文本框：保留平台的插入点、选择和输入法行为，它读取的是纯文本，包括令牌。
- 列表打开时，设置 `SuggestionsExpanded`，让编辑器报告自己处于展开状态；辅助技术请求关闭时，它会引发 `CollapseRequested`。在你自己的实时区域中播报匹配数量。
- 令牌不会作为单独的控件被朗读：为它们提供清晰的标签（“recap”，而不是 id）。
- 令牌颜色在浅色和深色主题中都保持对比度；不要仅用颜色传达含义。

### 键盘

| 按键 | 操作 |
|---|---|
| 输入触发符 | 在触发符的位置规则允许的地方打开查询 |
| `ArrowLeft` / `ArrowRight` | 移动插入点，一步跨过一个令牌 |
| 令牌之后的 `Backspace` / 令牌之前的 `Delete` | 删除整个令牌 |
| `Shift` + 方向键 | 逐字母选择 |
| `Enter` | 引发 `SubmitRequested` |
| `Shift+Enter` | 添加一行 |
| `Ctrl/Cmd+Z`、`Ctrl/Cmd+Shift+Z` | 撤销与重做，包括令牌 |

## 动效

- 高度在 `MinLines` 到 `MaxLines` 之间以短缓动跟随文字（默认 160 毫秒；设置 `HeightMotion` 可更改）。
- 令牌是绘制出来的，而不是做动画：令牌在插入时立即出现，其着色随主题变化。
- 减弱动效和关闭时，高度变化立即生效。

| 变化 | 完整 | 减弱 | 关闭 |
|---|---|---|---|
| 高度增加或减少 | 短缓动 | 立即 | 立即 |
| 插入或移除令牌 | 立即 | 立即 | 立即 |

## 相关组件

- `MentionInput` 用于带内置列表的 @人员 和 #频道。
- `Textarea` 用于普通多行文本。
- `TagInput` 用于短值列表。

| 组件 | 适用场景 |
|---|---|
| `MentionInput` | 需要开箱即用、带建议列表的人员和频道 |
| `Textarea` | 文本中没有令牌 |
| `TagInput` | 值是一个标签列表 |
| `AiComposer` | 你需要完整的提示框：附件、工具、模型和发送 |
