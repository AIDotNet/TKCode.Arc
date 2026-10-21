---
title: 富文本编辑器
summary: 轻量编辑器，支持 Markdown 快捷输入、浮动工具栏、斜杠菜单，并可输出 HTML 与 Markdown。
source-hash: 413255cd7252
---

<Demo name="Shortcuts" />

## 何时使用

- 需要加粗、斜体、列表、引用和代码块的评论、笔记、帖子以及中短篇文档。
- 以 HTML 或 Markdown 保存、并能重新读回编辑器的内容。
- 需要编辑器自身撤销历史的写作场景，该历史与平台的撤销相互独立。

## 何时不用

- 不需要格式的纯多行文本：用 `Textarea`。
- 在文本中提及人员或标签：用 `MentionInput`。
- 在页面上原位编辑的单行文本：用 `InlineEdit`。
- 源代码或只读代码清单：用 `CodeBlock`。

## 用法

通过 `Markdown` 设置初始内容。`Markdown` 与 `Html` 都是双向的：为其中任意一个赋值都会重新加载编辑器，且两者都会跟随每次编辑更新。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:RichTextEditor Markdown="## 笔记&#10;&#10;边写边输入 **Markdown**，或输入 `/` 插入块。" />
</UserControl>
```

### 占位文字与块提示

`Placeholder` 在空编辑器中显示。`BlockHint` 显示在放置插入符的空行上。

<Demo name="Placeholder" />

### 只读

设置 `IsReadOnly` 后，编辑、工具栏和快捷键都会关闭。文字仍可选中并复制。

<Demo name="ReadOnly" />

### 读取内容

每次编辑后都会执行 `ContentChanged`。其 `Value` 包含 `Html`、`Markdown`、`Text` 与 `IsEmpty`。方法 `GetHtml()`、`GetMarkdown()` 和 `GetText()` 可随时读取内容。

<Demo name="MarkdownAndHtml" />

```csharp
editor.ContentChanged += (_, e) =>
{
    draft.Markdown = e.Value.Markdown;
    canPost = !e.Value.IsEmpty;
};

var html = editor.GetHtml();
```

### 自定义工具栏

`Formats` 报告插入符处的标记与块类型，`CanUndo` 与 `CanRedo` 报告历史状态。可用它们设置自己按钮的状态。`ToggleMark`、`SetBlockType`、`SetLink` 与 `EditLink` 用于驱动编辑器。

将 `UndoCommand` 与 `RedoCommand` 绑定到按钮，或直接调用 `Undo()` 与 `Redo()`。

<Demo name="OwnToolbar" />

## API 参考

<!-- api: RichTextEditor -->

## 无障碍

- 编辑区是一个多行文本控件。默认名称为“编辑器”；设置 `AutomationProperties.Name` 可为其在表单中命名。
- 键盘：Mod+B、Mod+I、Mod+Shift+X、Mod+E 与 Mod+K 应用标记和链接。Mod+Z 与 Mod+Shift+Z 撤销与重做。Alt+F10 将焦点移入浮动工具栏，按 Escape 则返回文本。
- 编辑结果如“已设置格式”“已添加链接”和“已撤销”以礼貌方式朗读。
- 限制：编辑器不向屏幕阅读器提供字符或行的导航。它们会将整段文本作为一个值读取。

## 动效

- 浮动工具栏会滑动以跟随选区。在格式按钮与链接输入框之间切换时，工具栏尺寸会变形，两个界面相互滑过。
- 块菜单从 `/` 所在位置打开，列表被筛选时，高亮会在各行之间滑动。
- 减弱动效时，工具栏的移动与尺寸调整一步完成，两个界面之间交叉淡化。块菜单只淡入淡出，不缩放也不移动。

## 相关组件

- `Textarea`：不带格式的纯文本。
- `MentionInput`：提及人员或标签的文本。
- `CodeBlock`：用于展示代码，而非编写代码。
