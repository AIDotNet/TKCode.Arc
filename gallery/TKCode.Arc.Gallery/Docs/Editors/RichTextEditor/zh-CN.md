---
title: 富文本编辑器
summary: 轻量编辑器，支持 Markdown 快捷输入、浮动工具栏、斜杠菜单，并可输出 HTML 与 Markdown。
source-hash: cebd50d380c7
---

<Demo name="Shortcuts" />

## 何时使用

`RichTextEditor` 是需要轻量格式的内容的写作界面：加粗与斜体文字、标题、列表、引用和代码块。它自带文档、浮动工具栏和块菜单，因此表单无需在它外面另外排版。内容会以干净的 HTML 或 Markdown 离开编辑器，由应用保存，之后再读回。

- **开发者工具中的发布说明与更新日志草稿**：工程师输入 `## ` 插入标题、输入 `- ` 插入列表，编辑器保存的 Markdown 会被发布流水线原样发布。
- **项目跟踪工具中的评论与回复，或 CRM 备注**：带加粗文字、列表和引用的简短备注。编辑器只保留自己的标签，因此输出的 HTML 可以安全渲染。
- **知识库或帮助中心文章**：作者在管理后台撰写文章，保存的 HTML 由公开站点渲染，步骤与命令用标题和代码块呈现。
- **事故报告与复盘**：用标题、面向客户措辞的引用和日志行的代码块。报告发布后设置 `IsReadOnly`，文字仍可阅读和复制，但不能修改。
- **账单或账号控制台中的公告**：由需要格式但不需要排版的财务或支持人员撰写的简短消息，附带发票页面的链接。

## 何时不用

- **无格式的纯多行文本**：用 `Textarea`，它会换行并可调整大小，不带工具栏或块。
- **在文本框中提及人员或标签**：用 `MentionInput`，它会给出提示并保存提及内容。
- **在页面上原位重命名的单行文本**：用 `InlineEdit`，编辑前它看起来就是普通文字。
- **源代码或只读的代码清单**：用 `CodeBlock`，它自带复制操作。
- **图片、表格或带下划线的文字**：编辑器都不支持（它没有图片、表格或下划线块）。需要在文字旁附加图片时用 `FileDropzone`，内容需要复杂排版时用文档编辑器。

## 用法

用 `Markdown` 设置初始内容。`Markdown` 与 `Html` 都是双向的：为其中任一项赋值都会重新加载编辑器，且两者都会跟随每次编辑更新。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:RichTextEditor Markdown="## 笔记&#10;&#10;边写边输入 **Markdown**，或输入 `/` 插入块。" />
</UserControl>
```

`RichTextEditor` 派生自 `TemplatedControl`，而不是 `TextBox`。它是块编辑器：文档是一列块（段落、标题、列表项、引用、代码块和分隔线），每个块用自己的标记排版自己的文字。这让编辑器拥有独立的撤销历史、剪贴板规则和输出，因此文字并不是由控件原地编辑的原始字符串。它不是 `TextBox`，所以没有插入符索引或 `MaxLength`；用 `GetText()` 读取文字，其他成员见 API 参考。

### 结构

1. **写作区**：可编辑的文档，最小高度 128 px。它绘制文字、列表标记、引用竖条、代码背景和选区，并定位插入符。
2. **占位文字**（`Placeholder`，默认“开始写作”）：文档为空时以弱化的文字颜色显示。
3. **块提示**（`BlockHint`，默认“输入 / 插入块”）：显示在放置插入符的空行上。
4. **浮动工具栏**：出现在选区上方，按钮为 32 px 的方形，圆角 16 px。它包含行内标记（加粗、斜体、删除线、行内代码）、链接按钮和块类型。编辑链接时，其链接字段会原位替换按钮。
5. **链接字段**：工具栏中的单行字段，接受选区或插入符处链接的 URL。
6. **块菜单**：在 `/` 处打开、并随输入筛选的列表。每行高 34 px，圆角 12 px，位于圆角 18 px 的面板中。
7. **实时区域**：隐藏的元素，向辅助技术播报“已设置格式”或“已添加链接”等结果。

### 块类型与标记

文档由 `RichTextBlockType` 类型的块构成，行内标记来自 `RichTextMark`。工具栏和块菜单通过 `ToggleMark`、`SetBlockType` 与 `Formats` 报告，使用这些值。

| 类型 | 书写方式 | 说明 |
|---|---|---|
| `Paragraph` | 纯文本 | 默认块 |
| `Heading1`、`Heading2`、`Heading3` | `# `、`## `、`### ` | 导入的 h4 到 h6 会变为 `Heading3` |
| `BulletedList`、`NumberedList` | `- `、`1. ` | 用 Tab 嵌套；同一层级保持相同的列表类型 |
| `Quote` | `> ` | 起始处带竖条的引用块 |
| `CodeBlock` | 三个反引号 | 只保存纯文本；行不换行 |
| `Divider` | `---` | 水平分隔线；`SetBlockType` 会用它替换空块 |

| 标记 | 书写方式 | 说明 |
|---|---|---|
| `Bold` | `**text**` | |
| `Italic` | `_text_` 或 `*text*` | |
| `Strikethrough` | `~~text~~` | |
| `Code` | `` `text` `` | 限定在一个块内 |

链接不在此列表中：`SetLink` 与链接字段会把地址附加到选中的文字上。

### Markdown 快捷输入与支持的子集

编辑器会在输入时把 Markdown 转换为格式。快捷输入在所输入的文字完成它时立即生效，因此无需预览即可看到结果。代码块中的快捷输入不会生效。

| 输入 | 结果 |
|---|---|
| 位于行首的 `# `、`## `、`### ` | 一级、二级或三级标题 |
| `- `、`* `、`+ ` | 无序列表项 |
| `1. `、`1) ` | 有序列表项 |
| `> ` | 引用 |
| `---` | 分隔线 |
| 三个反引号 | 代码块 |
| `**text**`、`_text_`、`~~text~~`、`` `text` `` | 加粗、斜体、删除线、行内代码 |

编辑器只读写一个固定的子集：段落、三级标题、可嵌套的有序和无序列表、引用、代码块、分隔线，以及四种行内标记和链接。粘贴的 HTML 会被净化为该子集：未知的标签与样式会被移除，其中的文字保留。请把这个子集当作该功能的内容约定：需要其他内容的文档必须存放在别处。

### 占位文字与块提示

`Placeholder` 在空编辑器中显示，说明该字段用来写什么。`BlockHint` 显示在插入符所在的空行上，告诉用户如何添加结构。

<Demo name="Placeholder" />

当字段用于评论或备注时，两者都应设置，让空状态说明下一步该做什么。占位文字要简短：开始输入文字后它就会消失，块提示也随之消失。

### 只读

设置 `IsReadOnly` 后，编辑、工具栏和快捷键都会关闭。文字仍可选中和复制，辅助技术也会读取。

<Demo name="ReadOnly" />

对于已发布的文章、复盘报告或需要让人引用的签署备注，使用只读模式。在编辑器外提供一个编辑操作，把 `IsReadOnly` 切回 `False`，让修改是有意为之的。

### 内容格式

用消费者读取的格式保存内容：

| 格式 | 读取方式 | 用途 |
|---|---|---|
| Markdown（`Markdown`、`GetMarkdown()`） | 你的 Markdown 渲染器或编辑器 | 流水线中可编辑的文字、差异比较和版本历史 |
| HTML（`Html`、`GetHtml()`） | 浏览器或 Web 视图 | 直接渲染内容的页面 |
| 纯文本（`GetText()`） | 搜索与预览 | 每个块一行，不含标记 |

`Html` 与 `Markdown` 在加载时都会被净化。`SetHtml` 与 `SetMarkdown` 将文字作为一个撤销步骤加载。为 `Html` 或 `Markdown` 属性赋值时，只有当值与编辑器上次写出的值不同，文档才会重新加载，因此双向绑定不会在用户输入时重置插入符。

### 导入已有的 Markdown

编辑器只保存固定的子集，因此打开别处写好的文件可能改变它。`RichTextEditorMarkdown.Analyze` 报告编辑器无法保存的每个结构及其行、列和原因，不做任何转换。`TrySetMarkdown` 只在没有任何损失时导入，拒绝时不改动编辑器，宿主因此可以保留原文件、给出警告或先打开源码编辑器。

<Demo name="ImportCheck" />

```csharp
if (!editor.TrySetMarkdown(fileText, out var report))
{
    Warn($"{report.Issues.Count} constructs would be lost; the file is unchanged.");
    foreach (var issue in report.Issues.Where(i => i.Severity == RichTextImportSeverity.Lossy))
    {
        Warn($"Line {issue.Line}, column {issue.Column}: {issue.Reason}");
    }
}
```

| 报告为损失 | 原因 |
|---|---|
| 4 到 6 级标题 | 编辑器只有三级标题，它们会变成三级标题 |
| 代码围栏的信息串 | 编辑器不保存代码块的语言 |
| 波浪线围栏 | 编辑器只识别反引号围栏 |
| 表格 | 编辑器没有表格块，单元格会合并为一个段落 |
| setext 标题 | 编辑器只识别 `#` 标题 |
| 缩进代码 | 编辑器只识别围栏代码 |
| 起始编号大于 1 的列表 | 编辑器从 1 开始编号 |
| 硬换行、块级 HTML、图片、引用式链接、脚注、反斜杠转义 | 编辑器把源文本保留为文字 |

用其他写法表示的标记会报告为 `RichTextImportSeverity.Normalized`，不阻止导入：`*` 与 `+` 项目符号、`1)` 编号、`__粗体__`、`*斜体*` 和 `***` 分隔线会分别写回为 `-`、`1.`、`**`、`_` 和 `---`。支持的子集内的内容不会产生任何问题。宽松路径保持不变：`Markdown`、`SetMarkdown` 与形似 Markdown 的粘贴文本仍按原来的方式转换，只有主动调用 `TrySetMarkdown` 的调用方会经过这道门。

### 读取内容

每次编辑、撤销、重做和加载之后都会执行 `ContentChanged`。其 `Value` 是 `RichTextValue`，HTML、Markdown 和文本在首次读取时计算。`GetHtml()`、`GetMarkdown()` 和 `GetText()` 可以随时读取内容，并且始终是同步的。

| `RichTextValue` 的成员 | 类型 | 含义 |
|---|---|---|
| `Html` | `string` | 内容的干净 HTML |
| `Markdown` | `string` | 内容的 Markdown |
| `Text` | `string` | 纯文本，每个块一行 |
| `IsEmpty` | `bool` | 文档没有文字时为 true |

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

`Formats`（`RichTextFormats` 记录）报告插入符处的标记、链接和块类型。`CanUndo` 与 `CanRedo` 报告历史状态。用它们设置你自己按钮的状态，并调用 `ToggleMark`、`SetBlockType`、`SetLink` 和 `EditLink` 作用于选区。

将 `UndoCommand` 与 `RedoCommand` 绑定到按钮，或直接调用 `Undo()` 与 `Redo()`。工具栏按钮不应获得焦点，以便编辑器保留插入符；示例为此在每个按钮上设置了 `Focusable="False"`。

<Demo name="OwnToolbar" />

### 绑定到视图模型

在 MVVM 应用中，视图模型拥有文档。将 `Markdown` 双向绑定，从 `ContentChanged` 读取 `IsEmpty`，并在视图中使用撤销与重做命令。

```csharp
public partial class ReleaseNoteViewModel : ObservableObject
{
    private readonly IReleaseNotes _notes;

    [ObservableProperty]
    private string? _bodyMarkdown;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(PublishCommand))]
    private bool _hasContent;

    public ReleaseNoteViewModel(IReleaseNotes notes) => _notes = notes;

    [RelayCommand(CanExecute = nameof(CanPublish))]
    private async Task PublishAsync()
    {
        await _notes.PublishAsync(BodyMarkdown ?? string.Empty);
    }

    private bool CanPublish() => HasContent;
}
```

```xml
<StackPanel Spacing="16">
    <arc:RichTextEditor x:Name="Editor"
                        Markdown="{Binding BodyMarkdown, Mode=TwoWay}"
                        Placeholder="描述此次发布" />
    <arc:Button Content="发布说明"
                Command="{Binding PublishCommand}"
                IsDefault="True" />
</StackPanel>
```

```csharp
// Code behind: 编辑器通过 ContentChanged 报告是否为空；vm 是视图的 ReleaseNoteViewModel。
Editor.ContentChanged += (_, args) => vm.HasContent = !args.Value.IsEmpty;
```

- `Markdown`（以及 `Html`）是双向的：视图模型会收到每一次编辑，而当视图模型设置了不同的值时，编辑器会重新加载。对于长文档，该值会在短暂停顿后跟随输入（见性能一节），因此应在发布命令中调用 `GetMarkdown()`，而不是信任缓存的副本。
- `Placeholder` 与 `BlockHint` 是单向的；`IsReadOnly` 也是单向的，通常跟随已发布的状态。
- `UndoCommand` 与 `RedoCommand` 是供你自己的工具栏使用的命令。`CanUndo` 与 `CanRedo` 是只读的，并会引发变更通知。
- 判断是否为空、统计数量或自动保存时，应监听 `ContentChanged`。在处理程序中使用 `Value`；HTML 与 Markdown 会在首次读取时计算。

### 撤销与历史

编辑器保留自己的历史，最多 300 步，与平台的撤销相互独立。一秒之内的输入会合并为一步；空格或输入类型的改变会开始新的一步，每个命令（标记、块类型、链接）各占一步。`SetHtml`、`SetMarkdown` 和 `Clear` 各占一步，因此加载也可以撤销。编辑器启动时设置 `Markdown` 或 `Html` 属性不会进入历史。

### 长文档与性能

编辑器只重绘发生变化的块，并且只绘制视口之内及其附近的那部分文档。当文档不超过 400 个块时，每次编辑后都会写出 HTML 与 Markdown 的值。更长的文档则在用户停顿约 250 ms、或编辑器失去焦点或被移除时才写出。在 5,000 块的文档中，序列化会等待停顿，因此输入保持轻快。

`GetHtml()`、`GetMarkdown()` 和 `GetText()` 总是序列化当前文档，因此应在保存时调用，而不是在每次按键时调用。

### 示例：发布说明草稿

发布经理为开发者工具的某个版本撰写说明。占位文字告诉作者这里应该写什么，正文已有一个标题、一个列表、一处加粗的短语，以及一段引用的已知问题；发布按钮位于其下方，使用默认尺寸。编辑器中的文字就是发布流水线保存的同一份 Markdown。

![发布说明草稿，包含标题、加粗与列表文字，以及一段引用的已知问题](images/ReleaseNotes.png)

```xml
<StackPanel Width="640" Spacing="16">
    <TextBlock Text="Release notes"
               FontSize="18"
               FontWeight="SemiBold"
               Foreground="{arc:ArcDesignTokenResource Foreground}" />
    <Border Padding="24,20"
            CornerRadius="26"
            BorderThickness="1"
            Background="{arc:ArcDesignTokenResource Surface}"
            BorderBrush="{arc:ArcDesignTokenResource Border}">
        <arc:RichTextEditor Markdown="## Acme Analytics 4.2&#10;&#10;- Dashboards load **twice as fast**&#10;- Export charts as _PNG_ from the menu&#10;&#10;> Known issue: CSV exports above 500 MB can time out." />
    </Border>
    <StackPanel Orientation="Horizontal" Spacing="8" HorizontalAlignment="Right">
        <arc:Button Content="Discard" Variant="Secondary" />
        <arc:Button Content="Publish notes" />
    </StackPanel>
</StackPanel>
```

### 文案规范

- **占位文字**：说明该字段保存什么内容以及应该写什么，例如“描述此次发布”，而不是“输入文字”。
- **块提示**：点明最关键的一个手势，例如“输入 / 插入块”。不要列出所有快捷键。
- **按钮**：使用动词加宾语的句子式大小写，例如“发布说明”“保存草稿”。对于会被发布的说明，避免使用“提交”，因为用户期望文字即将上线。
- **链接文字**：写用户所读的文字，把 URL 放在链接字段中，而不是放进句子里。
- **标题**：保持简短并使用句子式大小写；文档外部已有标题时，文档内的章节使用 `Heading2`。

| 不要写 | 改为 |
|---|---|
| 在此输入文字 | 描述此次发布 |
| 点击这里 | 阅读迁移指南 |
| 提交 | 发布说明 |
| 输入 /// 插入块 | 输入 / 插入块 |

### 最佳实践

**推荐**

- 为每个空字段设置 `Placeholder` 与 `BlockHint`，让第一行就说明编辑器的用法。
- 内容会被再次编辑时保存 `Markdown`；当另一个页面只负责渲染时保存 `Html`。对于支持的子集两者等价，因此应选定其中一个作为事实来源。
- 在保存命令中用 `GetMarkdown()` 或 `GetHtml()` 读取内容。`ContentChanged` 的处理程序适合判断是否为空和统计数量，而不适合处理每一次写入。
- 已发布的文档切换为 `IsReadOnly`，并提供一个明确的编辑操作将其重新打开。
- 当工具栏按钮作用于选区时，像自定义工具栏示例那样让它们移出 Tab 顺序，使插入符留在文字中。

**避免**

- 保存来自其他编辑器的 HTML 并期望其样式得以保留。加载器只保留编辑器的子集，未知标签与行内样式会被丢弃，版式随之改变。
- 用编辑器处理图片、表格或带下划线的文字。编辑器无法保存它们，下次保存时内容会丢失。
- 在长文档中每次按键都读取 `GetHtml()`。每次按键时的序列化比编辑本身更耗时。
- 通过在每次变化时赋值 `Markdown` 来替换正在编辑的文档。请使用 `SetMarkdown()` 或 `Clear()`，它们各占一个撤销步骤，用户可以撤销这次替换。
- 期望屏幕阅读器用户按字符、词或行浏览文字。辅助技术会把整个文档当作一个值读取，因此文字较长时应提供 `GetText()` 的输出供审阅。

### 常见问题

**为什么 Ctrl+U（macOS 上为 Cmd+U）没有任何作用？** 下划线不属于编辑器的子集。该按键会被吞掉，因此文字不会改变，而 Markdown 输出中也没有下划线语法。

**如何在代码中添加或修改链接？** 调用 `SetLink(url)` 为选区或插入符处的链接设置链接。传入 `null` 或空字符串可以移除链接。`EditLink()` 打开与工具栏相同的链接字段，作用于选区或插入符处的链接。键盘上的 Ctrl+K（macOS 上为 Cmd+K）效果相同。

**为什么我再次设置相同的 HTML 时，绑定的 `Html` 没有重新加载？** 编辑器只在值与其上次写出的值不同时才重新加载。因此回显编辑器自身输出的双向绑定不会移动插入符。若要用相同内容强制重新加载，请改为调用 `SetHtml` 或 `SetMarkdown`。

**为什么快捷输入不会在代码块中转换文字？** 代码块只保存纯文本。`# ` 与 `**` 等快捷输入在其中不生效，Tab 插入两个空格，而不是改变列表层级。

**如何在列表项或引用中开始新的一行？** 在同一个块中按 Shift+Enter 换行。在空的列表项或引用上按 Enter 可以退出列表；在代码块末尾连按两次 Enter 可以退出代码块。

## API 参考

<!-- api: RichTextEditor -->

## 无障碍

- 写作区是一个多行可编辑文本控件，默认名称为“编辑器”。设置 `AutomationProperties.Name` 可为其在表单中命名，例如“发布说明正文”。
- 键盘（macOS 上为 Cmd）：`Ctrl+B`、`Ctrl+I`、`Ctrl+Shift+X` 与 `Ctrl+E` 应用标记，`Ctrl+K` 编辑链接。`Ctrl+Z` 与 `Ctrl+Shift+Z` 撤销与重做。`Alt+F10` 将焦点移入浮动工具栏，`Escape` 则返回文字。
- 编辑结果，如“已设置格式”“已添加链接”“已移除链接”“已撤销”“已重做”和“已添加编号列表”，会以礼貌方式播报，不会打断语音。
- 浮动工具栏是一个工具栏，其格式按钮是切换按钮，会报告自身的状态。它们的工具提示会显示键盘快捷键。
- 块菜单是一个列表。浏览时会播报当前项。
- 限制：编辑器不向屏幕阅读器提供字符、词或行的导航，它们会把整段文字作为一个值读取。

### 键盘

| 按键 | 操作 |
|---|---|
| `Ctrl+B`、`Ctrl+I` | 切换加粗、斜体 |
| `Ctrl+Shift+X` | 切换删除线 |
| `Ctrl+E` | 切换行内代码，限于一个块内 |
| `Ctrl+K` | 为选区或插入符处的链接打开链接字段；`Enter` 应用，`Escape` 关闭 |
| `Ctrl+Z`、`Ctrl+Shift+Z`（或 `Ctrl+Y`） | 撤销、重做 |
| `Ctrl+A` | 全选 |
| `Enter` | 新建块；在空的列表项或引用上离开列表；在代码块末尾连按两次则离开代码 |
| `Shift+Enter` | 在块内换行 |
| `Tab`、`Shift+Tab` | 缩进或取消缩进列表项；在代码块中 `Tab` 插入两个空格 |
| 在块首按 `Backspace` | 将标题、引用、代码块或列表项转为段落，或取消其缩进；位于分隔线之后时删除分隔线 |
| 行首或空格之后的 `/` | 打开块菜单；输入即可筛选，`Up` 与 `Down` 移动，`Enter` 或 `Tab` 选择，`Escape` 关闭 |
| `Alt+F10` | 将焦点移入浮动工具栏；`Left` 与 `Right` 在其按钮之间移动，`Escape` 返回文字 |

Ctrl 在 macOS 上为 Command，在其他平台上为 Control。单词移动遵循平台的文本导航（macOS 上为 Option，其他平台为 Control）。`Ctrl+U` 会被吞掉，因为不支持下划线。

## 动效

- 浮动工具栏会以轻微回弹滑动，跟随选区。在格式按钮与链接字段之间切换时，其尺寸会变形，两个界面向相反方向相互滑过。
- 块菜单从 `/` 所在位置打开。高亮在各行之间滑动；筛选改变列表时，其高度以平滑弹簧变化。
- 减弱动效时，工具栏的移动与尺寸调整一步完成，两个界面交叉淡化。块菜单只淡入淡出，不缩放也不移动。

| 变化 | 完整 | 减弱 | 关闭 |
|---|---|---|---|
| 工具栏跟随新的选区 | 带轻微回弹滑动到新位置 | 直接跳到该位置 | 立即 |
| 工具栏切换到链接字段或从其切换回来 | 尺寸变形，两个界面相互滑过 | 位置与尺寸瞬间完成；两个界面交叉淡化 | 立即 |
| 块菜单打开 | 从 `/` 所在位置弹出 | 淡入 | 立即 |
| 块菜单的高亮移动 | 在各行之间滑动 | 一步移动 | 立即 |
| 筛选时菜单列表变化 | 高度以平滑弹簧变化 | 立即 | 立即 |

## 相关组件

- `Textarea`：无格式的纯文本。
- `MentionInput`：提及人员或标签的文本。
- `CodeBlock`：用于展示代码，而非编写代码。
- `FileDropzone`：与文字一同提供的图片和文件。

| 组件 | 适用场景 |
|---|---|
| `Textarea` | 文字是纯文本并跨多行，没有加粗、列表或块 |
| `MentionInput` | 文字提及人员，且提及内容需要提示并保存 |
| `InlineEdit` | 在显示的位置重命名单个标题或名称 |
| `CodeBlock` | 代码用于展示、复制或阅读，而非编写 |
| `FileDropzone` | 图片或文件伴随文字出现，且必须上传 |
| `Input` | 值是表单中的一行，例如主题或标题 |
