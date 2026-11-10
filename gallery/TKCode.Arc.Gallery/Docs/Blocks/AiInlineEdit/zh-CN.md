---
title: AI inline edit
summary: 就地用 AI 修改一段文字：选中文字，说出要改什么，在段落中以划掉和新增的词查看改写，并逐处接受或拒绝。
source-hash: c5eceb9f0df4
---

<Demo name="Preview" />

## 何时使用

`AiInlineEdit` 让用户在不离开文字的情况下借助 AI 改进它。选中一段文字，按 Edit with AI（或 Ctrl+K），说出想要的效果，或选一个快捷指令，例如 Make it shorter。改写不会立刻替换原文，而是在段落中以逐词的改动显示：删掉的词被划掉，新增的词带底色，每处改动都有自己的接受和拒绝按钮。每处改动都决定后，或按下 Accept all 或 Reject all 后，文字更新，改过的段落保持选中，方便下一次请求。

- **文档、邮件、笔记和说明文字**：用户想在措辞上得到帮助，但要掌控每一个词。
- **审阅流程**：AI 的建议在落地前必须经过检查。

## 何时不用

- **从零开始写。** 请使用 `AI composer` 或聊天；没有可以对比的原文。
- **涉及整篇文档或多个文件的修改。** 请使用 `AI side panel`，它按段落列出改动。
- **单纯的拼写检查。** 请使用系统自带的拼写检查。

## 用法

区块派生自 Avalonia 的 `TemplatedControl`。`Text` 是双向的，会收到修改后的文字。改写来自 `RewriteHandler`；没有设置时，一个简单的内置替身会改写示例文字：

```xml
<arc:AiInlineEdit Text="{Binding Draft}" />
```

```csharp
editor.RewriteHandler = async (request, cancellation) =>
{
    // request.Selection is the passage, request.Instruction what to do, request.Text the whole text for context.
    return await model.RewriteAsync(request.Selection, request.Instruction, request.Text, cancellation);
};
editor.EditApplied += (_, e) => history.Push(e.OldText);
```

### 阶段

用户阅读和选择时 `Phase` 为 `Idle`，输入框打开时为 `Instructing`，生成改写时为 `Thinking`，决定改动时为 `Reviewing`。选中范围两端的空格不计入选区。处理程序抛出异常时，消息显示在底栏中，输入框保持打开，用户可以再试一次。如果改写与原文相同，底栏会说明没有建议的改动。

### 审阅改动

改写与原段落逐词比较。每个改动区域是一处改动：旧词以危险色划掉，新词带绿色底色，后面跟着一个勾和一个叉。已接受的改动在审阅结束前以下划线显示新词；已拒绝的改动恢复为旧词。`AcceptChange`、`RejectChange`、`AcceptAll`、`RejectAll` 和 `Cancel` 可在代码中完成同样的操作。

<Demo name="Handler" />

## API 参考

<!-- api: AiInlineEdit -->

<!-- api: AiInlineEditRequest -->

<!-- api: AiInlineEditLabels -->

## 无障碍

- 编辑器是名为 "Text with AI editing" 的区域；文字可以用键盘选择。
- **Ctrl+K**（或 **Cmd+K**）为选区打开输入框；**Enter** 发送；**Escape** 取消，审阅时则拒绝剩下的改动。
- 审阅时焦点移到第一处未决定的改动；**Tab** 依次经过每处改动的接受和拒绝按钮，按钮以改动命名（"Accept: replace "utilize" with "use""）。
- 一个礼貌式实时区域会在改动就绪和应用后播报。
- 删掉和新增的词通过划线和底色区分，不只靠颜色。

## 动效

- 改动按钮在指针悬停时于 160ms 内变色。
- 底栏的面板立即切换；生成改写时发送按钮显示加载圈。
- 减少动效和关闭动效时颜色变化立即生效。

## 响应式

- 编辑器最宽 680，小于这个宽度时跟随容器。
- 段落和审阅内容自动换行；快捷指令会换到更多行。

## 相关组件

- `AI side panel`，用于整篇文档中的建议改动。
- `AI composer`，用于撰写新的提示词。
- `Prompt studio`，逐词比较提示词的不同版本。
