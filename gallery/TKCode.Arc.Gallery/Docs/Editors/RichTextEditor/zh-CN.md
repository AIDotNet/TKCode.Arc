---
title: 富文本编辑器
summary: 轻量编辑器，支持 Markdown 快捷输入、浮动工具栏、斜杠菜单，并可输出 HTML 与 Markdown。
source-hash: b7c2d44c1172
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
- **排版复杂的文档**：表格只保存行内文字（无嵌套块），图片独占一个块。需要页面排版、分栏，或需要带进度上传文件时，请用文档编辑器或 `FileDropzone`。

## 用法

用 `Markdown` 设置初始内容。`Markdown` 与 `Html` 都是双向的：为其中任一项赋值都会重新加载编辑器，且两者都会跟随每次编辑更新。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
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
| `Heading1`、`Heading2`、`Heading3` | `# `、`## `、`### ` | `Arc` 配置会把导入的 h4 到 h6 变为 `Heading3` |
| `Heading4`、`Heading5`、`Heading6` | `#### `、`##### `、`###### ` | 仅 `Docs` 配置；`Heading6` 以次要文字颜色显示 |
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

### Docs 配置

设置 `Profile="Docs"` 即可得到文档风格的编辑器。默认的 `Arc` 配置与上文所述完全一致；`Docs` 增加较长文档所需的功能：

<Demo name="DocsProfile" />

| 功能 | 用法 | 写出形式 |
|---|---|---|
| 下划线 | `Ctrl+U`、工具栏按钮、`ToggleMark(RichTextMark.Underline)` | HTML 中的 `<u>` |
| 上标和下标 | `Ctrl+.` 和 `Ctrl+,`、工具栏按钮、`ToggleMark(RichTextMark.Superscript)` 或 `ToggleMark(RichTextMark.Subscript)`；开启其中一个会关闭另一个 | `<sup>`、`<sub>` |
| 文字大小 | 工具栏的字号按钮（小、默认、大、特大，相对于所在块的文字）；`SetTextSize` | `<span data-size="large">` |
| 行距 | 块菜单中的“Line spacing”选项（紧凑、默认、宽松、加宽）；`SetLineSpacing` 作用于选中的块。代码块和表格单元格保持自己的行距 | 段落、标题、列表项、引用和标注段落上的 `data-spacing="relaxed"` |
| 4 到 6 级标题 | 行首输入 `#### `、`##### ` 或 `###### `、斜杠菜单、块菜单、`SetBlockType(RichTextBlockType.Heading4)` | `<h4>` 到 `<h6>`，Markdown 中为 `####` 到 `######` |
| 文字色与高亮色 | 工具栏的颜色按钮打开七色调色板；`SetTextColor`、`SetHighlight` | `<span data-color="red">`、`<mark data-bg="yellow">` |
| 待办列表 | 行首输入 `[] ` 或 `[x] `、斜杠菜单、`SetBlockType(RichTextBlockType.TodoList)`；点击复选框即可勾选 | `<ul data-todo="true"><li data-checked="true">`，Markdown 中为 `- [x] ` |
| 折叠列表 | 斜杠菜单或块菜单、`SetBlockType(RichTextBlockType.Toggle)`。点击箭头、按 `Ctrl+Enter` 或调用 `ToggleFold()`，可把嵌套在它下面的项折叠起来；在展开的折叠项末尾按 Enter 会新建一个嵌套项，`Tab` 可把任何列表项嵌套到上方的折叠项下。折叠的项不占空间，插入点会跳过它们；伸入折叠内容的编辑（缩进到折叠项下、在其末尾按 Delete）会把它展开 | `<ul data-toggle="true"><li data-collapsed="true">`；Markdown 中为普通的 `- ` 项 |
| 文本对齐 | `Ctrl+Shift+L`、`Ctrl+Shift+E`、`Ctrl+Shift+R`；`SetAlign` | 段落、标题和引用上的 `data-align="center"` 或 `"end"` |
| `Ctrl+Shift+上箭头`、`Ctrl+Shift+下箭头` | 上移或下移插入点所在的块（`Docs` 配置） |
| 在折叠项上按 `Ctrl+Enter` | 折叠或展开它（`Docs` 配置） |
| 代码语言 | 点击代码块上方的标签打开语言列表；`SetCodeLanguage("csharp")`。代码按所选语言高亮（纯文本或没有语法的语言保持原样） | `<pre data-lang="csharp">`，Markdown 中为带信息串的围栏 |
| 高亮块 | `/` 菜单或块菜单；块菜单可设置颜色和图标；`SetCalloutColor`、`SetCalloutIcon`。回车延续同一个框，空行回车则离开 | `<aside data-callout="blue" data-icon="💡">`，Markdown 中为 GitHub 提示语法（`> [!NOTE]`），只保留五种提示类型 |

颜色来自固定的调色板（`RichTextColor`：灰、红、橙、黄、绿、蓝、紫），在浅色和深色模式下随主题变化，因此保存的内容不含原始颜色值。`Formats` 还会报告 `IsUnderline`、`IsSuperscript`、`IsSubscript`、`TextColor`、`Highlight`、`Size`、`Align`、`LineSpacing` 和 `CodeLanguage`，供您自己的工具栏使用。每行的高度取该行最大文字所需的高度，所以一个大号的词会让整个块的行距变大。

```xml
<arc:RichTextEditor Profile="Docs" Html="{Binding Body, Mode=TwoWay}" />
```

- **Markdown 对颜色、字号和行距是有损的。** Markdown 没有这些设置，所以 `GetMarkdown()` 保留文字、丢弃它们。下划线、上标和下标在 Markdown 中也没有语法，会写成行内 `<u>`、`<sup>` 和 `<sub>` 标签，`Docs` 配置读取时会还原。内容必须保留颜色时，请保存 `Html`。
- **`Profile` 的赋值顺序无关紧要。** 在配置之前赋入的内容，会按 Docs 规则重新读取。
- **自定义语言。** 语言列表末尾有一行“自定义…”，可输入任何名称；`CodeLanguages` 可用您自己的名称和 id 替换整个列表，例如 `new RichTextCodeLanguage("Zig", "zig")`。高亮需要该 id 有对应的语法；没有语法的名称会被保留并显示，代码保持原样。
- **别处的任务列表可以导入。** Markdown 的 `- [ ]`、`- [x]` 项，以及以复选框 input 开头的 HTML 列表项，会成为待办项；在 `Arc` 配置下它们仍是普通列表项。
- **其余行为不变。** 分隔线、引用、链接、斜杠菜单和撤销历史与 `Arc` 配置一致，`Arc` 配置写出的文档可原样加载。

### 图片与表格

`Docs` 配置还能保存图片和表格。图片是一个块，保存地址和描述；表格是单元格网格，新建的表格带一行表头。两者都可通过 Markdown（单独一行的 `![alt](src)`、管道表格）和 HTML（`img`、`table`）读入和写出。`Arc` 配置不把它们放进文档。

<Demo name="ImagesAndTables" />

| 功能 | 用法 |
|---|---|
| 插入图片 | `/` 后选择 Image（文件选择器）、粘贴图片文件或截图、把图片文件拖入编辑器、粘贴单独的图片地址、输入 `![alt](地址)`，或调用 `InsertImage(地址, alt)` |
| 调整图片大小 | 单击图片，然后拖动右下角的手柄；比例保持不变，宽度会被保存（HTML 中的 `width`；Markdown 会把调整过的图片写成 `<img>` 标签） |
| 选中并删除图片 | 单击图片，然后按 Backspace 或 Delete；在下一段开头按 Backspace 也会删除 |
| 插入表格 | `/` 后选择 Table（3×3），或 `InsertTable(行数, 列数)` |
| 在表格中移动 | Tab 和 Shift+Tab 在单元格间移动（在最后一格按 Tab 会新增一行）；Enter 向下移动一格；上下方向键保持在同一列 |
| 编辑行和列 | 右键单击单元格，或调用 `AddTableRow`、`AddTableColumn`、`DeleteTableRow`、`DeleteTableColumn`、`DeleteTable` |
| 合并与拆分单元格 | 拖选多个单元格后右键选择“Merge cells”，或调用 `MergeTableCells`；右键单击合并后的单元格选择“Split cell”，或调用 `SplitTableCell`。`CanMergeTableCells` 与 `CanSplitTableCell` 可告诉您自己的工具栏当前是否可用 |
| 调整列宽 | 拖动两列之间的边框；双击边框、选择“Distribute columns”或调用 `DistributeTableColumns` 可重新平分宽度 |
| 表头行与表头列 | 右键切换“Header row”或“Header column”，或调用 `SetTableHeaderRow` 和 `SetTableHeaderColumn`；用 `TableHasHeaderRow` 和 `TableHasHeaderColumn` 读取 |
| 单元格颜色 | 选中单元格后右键选择颜色，或调用 `SetTableCellColor`；`TableCellColor` 读取它们共同的颜色 |
| 选择单元格、行和列 | 从一个单元格拖到另一个单元格即可选中两者之间的矩形；单击手柄（列的顶部、行的左侧），或调用 `SelectTableRow` 和 `SelectTableColumn` |
| 移动行和列 | 把手柄拖到新位置，右键选择“Move row up”“Move row down”“Move column left”或“Move column right”，或调用 `MoveTableRow` 和 `MoveTableColumn`（`CanMoveTableRow` 和 `CanMoveTableColumn` 告诉您的工具栏能否移动） |
| 复制和粘贴单元格 | 复制矩形选区得到以制表符分隔的文本，电子表格可直接读取；把表格或以制表符分隔的文本粘贴到单元格中，会从该单元格开始填充，并按需添加行和列 |
| 列对齐 | `Ctrl+Shift+L`、`Ctrl+Shift+E`、`Ctrl+Shift+R` 或 `SetAlign` 会让光标所在单元格的整列对齐 |

**图片存放在哪里由你决定。** 编辑器从不上传任何内容。当用户选择、粘贴或拖入图片时，它会触发 `ImageSourceRequested`，携带文件名、媒体类型和字节（`OpenReadAsync`、`ReadAllBytesAsync`）。已知地址时用 `Source` 回答，上传进行中时用 `SourceTask` 回答；任务完成后编辑器才插入图片，任务失败或返回 null 则不插入。设置 `Cancel` 可拒绝该图片。没有处理程序时，图片会以 `data:` 地址嵌入，文档自包含但体积较大。

```csharp
editor.ImageSourceRequested += (_, e) =>
{
    e.SourceTask = UploadAsync(e.FileName, e.ContentType, e.OpenReadAsync());   // 返回 CDN 地址
};
```

**图片如何加载也可以由你决定。** 编辑器需要某个地址的像素时会触发 `ImageLoading`。设置 `Bitmap` 或 `StreamTask` 即可自行加载，例如来自需要令牌的存储或自定义协议；没有回答时，编辑器会读取 `data:`、http(s)、`avares:` 和本地文件地址。`ReloadImages()` 会清除已加载的内容。

- **地址会被检查。** 内容加载和插入图片时，脚本协议以及非位图的 `data:` 地址会被拒绝；自定义协议（`app://files/1`）可以通过。
- **图片会缩小到编辑器宽度**并保持比例。图片加载中或加载失败时，会显示同宽的占位。
- **单元格只保存行内文字**：标记、链接和换行（Shift+Enter），不含列表或嵌套块。导入的这类内容会被压平成多行。较短的行会被补齐。
- **合并单元格**可跨越多行多列，被合并的文字会连成多行。HTML 以 `colspan` 和 `rowspan` 保存；Markdown 没有合并单元格，会把被覆盖的单元格写成空的，合并必须保留时请保存 `Html`。Tab 会跳过被覆盖的单元格，Enter 会移到合并单元格的下方。
- **列宽是比例**：表格始终占满编辑器宽度，没有设置宽度的列平分其余列留下的空间（每列至少 48 px）。HTML 以 `<col width>` 保存列宽、以 `data-bg` 保存单元格颜色，并把表头单元格写成 `th`；第一行没有 `th` 单元格的 `table` 读入后没有表头行。Markdown 不保留这些设置，其表格总有表头行。
- **选中的矩形单元格**以整块显示。删除或输入会清空这些单元格，不会合并它们；粘贴会从第一个单元格开始填充，并跳过被覆盖的单元格。从表格第一个字符选到最后一个字符，或选区超出表格时，仍会删除整个表格。
- **合并单元格会把它跨越的行或列连在一起**：如果有合并单元格跨过某一行，或跨过它要移到的位置，这一行就不能移动；列也一样。无论哪一行移到最上面，第一行始终是表头行。

### 块手柄

在 `Docs` 配置下，鼠标悬停在某个块上时，其左侧的边栏会出现两个按钮。拖动手柄（⋮⋮）可把块移到新位置：一条线指示落点，列表项会带着嵌套项一起移动，表格则整体移动。点击手柄会打开块菜单，可把块转为其他类型（文本、标题、列表、待办、引用、代码），或复制、删除。加号按钮会在下方添加一个空段落，并在那里打开块菜单。键盘上，`Ctrl+Shift+上箭头` 和 `Ctrl+Shift+下箭头` 移动插入点所在的块。每次移动都是一个撤销步骤。

在 `IsReadOnly` 模式和 `Arc` 配置下不显示手柄，文字与编辑器边缘齐平。边栏是书写面左侧 56 px 的外边距。

### 查找与替换

`Ctrl+F` 会在编辑器右上角打开查找栏，并填入当前选中的文字（限一行之内的片段）。输入时所有匹配项都会高亮，选区处或之后的第一个匹配项会被选中；`Enter` 和 `Shift+Enter`（或 `F3` 和 `Shift+F3`）在其余匹配项之间跳转，查找栏会显示当前位置（如“2 of 5”）。`Ctrl+H` 或查找栏上的替换按钮会展开替换行：在其中按 `Enter` 会替换当前匹配项并跳到下一个，“全部替换”会把所有匹配项作为一步撤销一起替换。`Escape` 关闭查找栏并回到正文，最后一个匹配项保持选中。

- **匹配不跨块。** 段落、列表项、表格单元格和代码块各自单独搜索，所以被拆在两个块里的短语找不到。
- **替换后的文字沿用被替换文字的样式**：即匹配项第一个字符的标记、颜色和链接。
- **大小写。** 默认不区分大小写，打开“Match case”后才区分。
- **在代码中使用**：`OpenFind(replace)`、`CloseFind()`、`IsFindOpen`、`CountMatches(text, matchCase)` 和 `ReplaceAll(text, replacement, matchCase)`，后者返回替换的数量。只读编辑器可以查找，但不会替换。

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
| 4 到 6 级标题（`Arc` 配置） | `Arc` 配置只有三级标题，它们会变成三级标题 |
| 代码围栏的信息串 | 编辑器不保存代码块的语言 |
| 波浪线围栏 | 编辑器只识别反引号围栏 |
| 表格 | `Docs` 配置会保存；`Arc` 配置下单元格会合并为一个段落 |
| setext 标题 | 编辑器只识别 `#` 标题 |
| 缩进代码 | 编辑器只识别围栏代码 |
| 起始编号大于 1 的列表 | 编辑器从 1 开始编号 |
| 硬换行、块级 HTML、行内文字中的图片（`Arc` 配置下的所有图片）、引用式链接、脚注、反斜杠转义 | 编辑器把源文本保留为文字 |

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
- 在 `Arc` 配置下使用图片和表格。只有 `Docs` 配置会保存它们，下次保存时内容会丢失。
- 在长文档中每次按键都读取 `GetHtml()`。每次按键时的序列化比编辑本身更耗时。
- 通过在每次变化时赋值 `Markdown` 来替换正在编辑的文档。请使用 `SetMarkdown()` 或 `Clear()`，它们各占一个撤销步骤，用户可以撤销这次替换。
- 期望屏幕阅读器用户按字符、词或行浏览文字。辅助技术会把整个文档当作一个值读取，因此文字较长时应提供 `GetText()` 的输出供审阅。

### 常见问题

**为什么 Ctrl+U（macOS 上为 Cmd+U）没有任何作用？** 在默认的 `Arc` 配置下，下划线不属于编辑器的子集。该按键会被吞掉，因此文字不会改变，而 Markdown 输出中也没有下划线语法。设置 `Profile="Docs"` 后，`Ctrl+U` 会为选区加下划线。

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
| `Ctrl+F`、`Ctrl+H` | 打开查找栏，或打开带替换行的查找栏 |
| `F3`、`Shift+F3` | 查找栏打开时跳到下一个、上一个匹配项（在搜索框中为 `Enter` 和 `Shift+Enter`） |
| `Ctrl+U` | 切换下划线（`Docs` 配置） |
| `Ctrl+.`、`Ctrl+,` | 切换上标、下标（`Docs` 配置） |
| `Ctrl+Shift+L`、`Ctrl+Shift+E`、`Ctrl+Shift+R` | 左对齐、居中、右对齐（`Docs` 配置） |
| `Ctrl+Z`、`Ctrl+Shift+Z`（或 `Ctrl+Y`） | 撤销、重做 |
| `Ctrl+A` | 全选 |
| `Enter` | 新建块；在空的列表项或引用上离开列表；在代码块末尾连按两次则离开代码 |
| `Shift+Enter` | 在块内换行 |
| `Tab`、`Shift+Tab` | 缩进或取消缩进列表项；在代码块中 `Tab` 插入两个空格 |
| 在块首按 `Backspace` | 将标题、引用、代码块或列表项转为段落，或取消其缩进；位于分隔线之后时删除分隔线 |
| 行首或空格之后的 `/` | 打开块菜单；输入即可筛选，`Up` 与 `Down` 移动，`Enter` 或 `Tab` 选择，`Escape` 关闭 |
| `Alt+F10` | 将焦点移入浮动工具栏；`Left` 与 `Right` 在其按钮之间移动，`Escape` 返回文字 |

Ctrl 在 macOS 上为 Command，在其他平台上为 Control。单词移动遵循平台的文本导航（macOS 上为 Option，其他平台为 Control）。在 `Arc` 配置下 `Ctrl+U` 会被吞掉，因为该配置不支持下划线；`Docs` 配置则用它加下划线。

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
