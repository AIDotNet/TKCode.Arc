---
title: 代码块
summary: 以清晰的层级呈现代码，并提供便捷的复制入口。
source-hash: 9560241e1e99
---

<Demo name="Default" />

## 何时使用

`CodeBlock` 以只读、可选中的框体展示源文件或代码片段，框体带有文件名标题栏、语法配色和复制操作。它用于文档、设置页和开发者工具中，让人能够阅读、复制代码，并知道代码属于哪个文件，而无需提供编辑器。

- **安装与配置说明**：入门或集成页面中的 `dotnet add package` 命令、`.env` 文件或 `docker compose` 文件，标题栏中写明文件名，让人知道每一行该放在哪里。
- **生成或导出的配置**：管理后台中操作员要复制到其他系统的 Webhook 负载、CI 流水线或 Terraform 计划。复制操作复制的是完整源码，而不仅是可见部分。
- **需要折叠的长文件**：一份 200 行的部署脚本或生成的 SDK 客户端只显示前几行，并提供「显示全部」开关，让设置页保持易于浏览。
- **开发者门户或 API 参考中的片段**：`json` 格式的请求与响应、C# 客户端调用或 `curl` 命令，各自带有文件名或语言标签。
- **支持或事件视图中的代码示例**：工程师与时间线并排阅读的失败查询或配置差异，带有行号，团队可以在讨论中引用某一行。

## 何时不用

- **复制单个值，例如 API 密钥或 Webhook 密钥**：单独使用 `CopyButton`。单个令牌不需要代码框体，额外的标题与框架只会喧宾夺主。
- **需要按键浏览、搜索或展开的 JSON**：用 `JsonViewer`，它以树形展示并支持搜索与逐行复制。`CodeBlock` 只把 JSON 当作文本显示。
- **可导航的文件夹与文件名层级**：用 `TreeView` 浏览文件，并将 `CodeBlock` 放在旁边，让所选文件决定显示的代码。
- **原地编辑代码或文本**：多行纯文本用 `Textarea`。`CodeBlock` 是只读的，人们可以选中并复制其中的文本，但不能修改。
- **混合了标题、列表与代码的富文档**：用 `Markdown`，它会在文档流中以相同的语法配色渲染围栏代码。

## 用法

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:CodeBlock FileName="greeting.tsx"
                   Language="tsx"
                   Code="{Binding Source}" />
</UserControl>
```

`CodeBlock` 派生自 Avalonia 的 `TemplatedControl`，因此拥有控件常见的布局、样式与自动化行为。它不是 `TextBox`：`Code` 是它唯一接受的文本，文本为只读，框体在代码区域之外还有标题与可选的展开行。语法高亮使用 StreamMark 引擎的 TextMate 语法，并以 Arc 的语法角色颜色绘制。

### 结构

1. **标题**（`FileName`、`SourceCodeLabel`、`Language`）：高度至少 52 px，左侧距边缘 16 px，右侧 12 px，上下各 8 px。它显示文件图标、文件名、中间点与语言名称。长文件名会以省略号截断，语言名称保留其宽度。`FileName` 为空时，标题显示 `SourceCodeLabel`（默认为“Source code”）。
2. **复制按钮**（`CopyLabel`）：标题末端的 `CopyButton`，距文件标签 12 px。它复制 `Code`。
3. **行号栏**（`ShowLineNumbers`）：右对齐的一列数字，颜色偏淡，并向表面色混合。它对辅助技术隐藏，代码横向滚动时也不会移动。
4. **代码区域**：使用 Arc 令牌中的等宽字体，字号 14 px，行高 23.8 px（字号的 1.7 倍），四周内边距 20 px。文本可选中。长行会在代码块内横向滚动。
5. **展开开关**（`IsCollapsible`、`MaxLines`、`IsExpanded`）：一个全宽的行，高度至少 44 px，带顶部边框、16 px 的箭头，标签为“显示全部 14 行”（展开时为“收起部分行”）。只有当源码行数超过 `MaxLines` 时才会出现。
6. **框体**：1 px 边框，圆角为面板圆角，表面色背景。框体会裁剪其内容，因此标题与展开行会保持框体的圆角。

窄布局从宽度低于 420 px 开始（主题中的 `CompactWidth`）：标题左侧内边距降至 12 px，复制按钮只显示图标，代码字号降至 12 px，行高 20.4 px，内边距 16 px，行号也随之缩小。

### 折叠长代码

`IsCollapsible` 会添加展开行。`MaxLines` 设置折叠时显示的行数（默认 12，至少为 1）。行数等于或少于它的源码不显示切换开关，因此可以在列表中的每个代码块上都保持开启 `IsCollapsible`。

<Demo name="Collapsible" />

`IsExpanded` 为双向绑定，表示代码块是否已展开。当页面需要记住状态时绑定它；当周围的操作应当展开源码时（例如指向第 40 行的搜索结果），则从代码中设置它。

```csharp
block.IsCollapsible = true;
block.MaxLines = 6;
block.IsExpanded = false;
```

折叠高度等于 `MaxLines` 乘以行高，再加上代码区域的内边距，因此相同设置下，折叠的代码块总有相同的高度，与代码内容无关。

### 语言

`Language` 设置语法高亮以及标题中的语言标签。它接受常用名称以及代码围栏使用的别名，例如 `cs`、`ts`、`py`、`sh`、`yaml`、`json` 和 `axaml`。名称不区分大小写，前导点号会被忽略，因此 `.TSX` 与 `tsx` 是同一种语言。默认值为 `tsx`。

没有语法的语言（例如 `text`）会以无颜色的纯文本显示。日志、笔记和非代码的输出使用它，这样代码块就不会意外地为其中的单词上色。

<Demo name="Languages" />

```xml
<arc:CodeBlock FileName="stats.py" Language="py" Code="{Binding PythonSource}" />
<arc:CodeBlock FileName="notes.txt" Language="text" Code="纯文本。" />
```

高亮器将 TextMate 的词法颜色映射到九种角色：注释、字符串、数字、关键字、类型、函数、属性、标签与标点。注释为斜体，使用弱化的文字颜色；标点使用次要文字颜色；其他角色在每种外观下各有自己的颜色。角色颜色不跟随强调色，因此产品更换强调色时，代码的可读性不会改变。该映射只是对语法类别的近似。少数词元（例如 JavaScript 对象字面量的键）可能显示为纯文本，某些语言中的内置类型名也会被着色为类型。

### 复制与行号

复制按钮位于标题中，复制的内容与你设置的 `Code` 完全一致：制表符与换行符都与源码相同。显示只在两方面有所不同：制表符显示为两个空格，回车加换行显示为一个换行。`ShowLineNumbers` 会添加行号列，代码横向滚动时该列保持不动。

<Demo name="LineNumbers" />

```xml
<arc:CodeBlock FileName="deploy.sh"
               Language="bash"
               ShowLineNumbers="True"
               Code="{Binding Script}" />
```

`Copied` 是路由事件，在复制操作把代码放入剪贴板之后触发。用它在你自己的状态区域中确认复制：

```csharp
block.Copied += (_, _) => status.Text = "代码已复制";
```

`LineCount` 给出 `Code` 中的行数，可用于“42 行”之类的说明文字。

### 标签

复制标签、文件名回退文字与折叠文字都是属性，因此可以翻译，或使用你的产品用语。`ShowAllLinesFormat` 接受一个 `{0}` 占位符，它会接收行数，并按当前文化格式化。

```xml
<arc:CodeBlock Code="{Binding Source}"
               CopyLabel="复制代码片段"
               SourceCodeLabel="代码片段"
               ShowAllLinesFormat="显示全部 {0} 行"
               ShowFewerLinesLabel="收起部分行" />
```

### 切换文件

同时设置新的 `FileName`、`Language` 与 `Code`，即可就地换入另一个文件。文件名所在的位置会跟随新的宽度，新的源码上浮出现，高度以弹簧动画跟随。三个属性应一起修改，这样标题与语法高亮就不会出现一个文件的名称配着另一个文件的代码。

<Demo name="SwitchingFiles" />

### 窄宽度

宽度低于 420 px 时，内边距与文字缩小，复制操作只保留图标。代码块按自身宽度调整，而不是窗口宽度，因此窄侧边栏无需任何代码即可获得紧凑布局。

<Demo name="Narrow" />

### 数据结构与性能

`CodeBlock` 只保存一个字符串。它不会为每个词元创建一个控件：整段源码是一个文本布局，带有着色的文本段，因此开销随源码长度增长，而不是随词元数量增长。

- 高亮在 `Code` 或 `Language` 变化时运行，而不是在每次重绘时运行。若源码不超过 32 KB 且其语法已被使用过，会立即在 UI 线程上高亮。其他源码会先显示纯文本，再由后台处理获得颜色，因此会话中第一个长文件可能会短暂地无颜色显示。
- 对超过几屏的源码进行折叠。折叠的代码块保持固定高度，其余源码按需展开，因此包含许多长文件的页面依然易于浏览。
- 每个文件使用独立的代码块。代码块是一个可选中的文本布局，因此许多短片段的页面适合拆分为多个代码块，各自带有文件名和复制操作。

### 绑定到视图模型

在 MVVM 应用中，视图模型拥有源码、文件名与展开状态，代码块只负责呈现。使用 CommunityToolkit.Mvvm 时，`ObservableObject` 与 `[ObservableProperty]` 即已足够。

```csharp
public partial class PipelineViewModel : ObservableObject
{
    [ObservableProperty]
    private string _fileName = "deploy.yml";

    [ObservableProperty]
    private string _language = "yaml";

    [ObservableProperty]
    private string _source = string.Empty;

    [ObservableProperty]
    private bool _isSourceExpanded;
}
```

```xml
<StackPanel Spacing="16">
    <arc:CodeBlock FileName="{Binding FileName}"
                   Language="{Binding Language}"
                   Code="{Binding Source}"
                   IsCollapsible="True"
                   MaxLines="8"
                   IsExpanded="{Binding IsSourceExpanded}" />
</StackPanel>
```

- `Code`、`FileName` 与 `Language` 为单向输入。在视图模型中修改它们，代码块会同时更新标题、语法高亮与折叠状态。
- `IsExpanded` 默认为双向绑定。用户的切换操作会写回 `IsSourceExpanded`，因此页面可以记住状态，或视图模型可以在搜索结果指向代码块时将其展开。
- `Copied` 是事件，而不是命令。在代码后置或附加行为中订阅它，并把反馈保留在视图模型中，由它负责状态文字的显示。

### 示例：设置页上的部署流水线

部署工具中项目设置页展示项目所用的流水线文件。标题写明文件名及其语言，行号让工程师在评审中能够引用“第 4 行”，源码折叠为三行，完整行数显示在切换标签中。复制操作把整个文件交给需要粘贴到其他仓库的人。下方的安装命令未设置 `IsCollapsible`，因此不显示切换开关。

![设置页上带有行号和折叠展开行的部署流水线文件](images/DeployPipeline.png)

```xml
<StackPanel Width="680" Spacing="16">
    <arc:CodeBlock FileName="deploy.yml"
                   Language="yaml"
                   ShowLineNumbers="True"
                   IsCollapsible="True"
                   MaxLines="3"
                   Code="on: push&#10;jobs:&#10;  build:&#10;    runs-on: ubuntu-latest" />
    <arc:CodeBlock FileName="install.sh"
                   Language="bash"
                   CopyLabel="Copy command"
                   Code="dotnet add package TKCode.Arc" />
</StackPanel>
```

### 文案规范

- **文件名**：代码所属的路径，即读者在项目中找到它的方式：`deploy.yml`、`Program.cs`。只有在没有文件时才使用描述性标签，并保持简短：“Webhook 负载”“示例响应”。
- **复制标签**：动词加对象，句子式大小写：“复制命令”“复制代码片段”。使用你的产品在其他地方对该操作所用的标签，让人们能够认出它。
- **折叠标签**：在标签中保留行数，让人们在打开前就知道文件有多长。默认的“显示全部 14 行”即是如此；翻译后的格式必须保留 `{0}`。
- **源码文本**：让代码按其应当运行的样子呈现。在代码进入页面前，删除机密信息、真实客户 ID 与内部主机名，因为复制操作会让它们很容易被带走。

| 不要写 | 改为 |
|---|---|
| 点击下方复制代码 | 复制命令 |
| 代码 | 源代码，或文件名 |
| 显示更多 | 显示全部 14 行 |
| 输出（纯文本） | 将 `text` 作为语言，并将文件命名为 "output.txt" |

### 最佳实践

**推荐**

- 为读者可能复制进文件的每个代码块设置 `FileName`。标题告诉人们代码应放在哪里，代码块也因此获得自己的名称，供辅助技术使用。
- 用 `IsCollapsible` 折叠长源码，并设置 `MaxLines` 显示人们最需要的部分，例如函数签名或流水线的第一个作业。
- 当人们讨论具体行时（例如评审、事件或支持对话），使用 `ShowLineNumbers`。
- 输出与日志使用 `text`，这样代码块就不会为不是代码的文字上色。

**避免**

- 用代码块展示单个值：密钥、令牌或 URL 应使用 `CopyButton`，单行外加框体会吸引超过该值应得的注意力。
- 在未设置 `Language` 的情况下把 `Code` 换成另一种语言的示例。新文本会沿用旧语言的语法高亮，于是 Python 示例会显示 C# 的颜色。应同时设置 `FileName`、`Language` 与 `Code`。
- 每次按键都从文本编辑器驱动 `Code`。代码块是只读视图，每次变化都会启动一次新的高亮处理；编辑应使用 `Textarea`，只在人员保存后才以 `CodeBlock` 展示结果。
- 期望复制的文本中含有行号。行号栏不属于 `Code`，因此复制操作永远不会包含它们。

### 常见问题

**源码很长时，为什么代码块没有切换开关？** 只有当 `IsCollapsible` 为 `True` 且源码行数超过 `MaxLines` 时才会出现切换开关。两者都要检查，并确认 `MaxLines` 大于你预计要隐藏的行数。

**折叠会改变复制操作复制的内容吗？** 不会。复制操作会完整复制 `Code`，无论是否折叠，因此人们可以从只显示八行的代码块中复制一个 200 行的文件。

**为什么代码会在一段时间内没有颜色？** 当源码的语法尚未加载，或源码大于 32 KB 时，高亮器会在后台处理中为其着色。颜色到达之前，文字以纯文本显示。没有语法的语言（例如 `text`）始终保持纯文本。

**如何在自己的文字中显示行数？** 读取 `LineCount`。它在换行符规范化之后统计 `Code` 的行数，因此末尾带换行符的源码会比显示的文字多计一行。

**能修改制表符宽度吗？** 不能通过属性修改。制表符始终显示为两个空格。当精确宽度很重要时，在源码中使用空格，因为复制操作会按你设置的样子复制源码。

## API 参考

<!-- api: CodeBlock -->

## 无障碍

- 代码块是一个带名称的区域。其名称来自 `FileName`（例如“deploy.yml 源代码”），或在文件名为空时来自语言（“yaml 源代码”），因此页面上的每个代码块都有各自的名称。
- 代码区域名为“可选中的源代码”。行号栏被标记为原始内容，因此屏幕阅读器读取代码时会跳过行号。
- Tab 依次移动到复制按钮、代码区域与展开开关。
- Enter 或 Space 会复制代码或切换展开状态，取决于当前获得焦点的控件。
- 代码区域可以获得焦点，因此键盘用户可以用方向键滚动长行。键盘焦点会在框体内显示 2 px 的焦点环；鼠标点击则不显示。
- 展开开关会向辅助技术公开其展开状态。复制结果由 `CopyButton` 播报。

### 键盘

| 按键 | 操作 |
|---|---|
| `Tab` / `Shift+Tab` | 在复制按钮、代码区域与展开开关之间移动 |
| 复制按钮上的 `Enter` 或 `Space` | 将 `Code` 复制到剪贴板 |
| 展开开关上的 `Enter` 或 `Space` | 展开或收起源码 |
| 代码区域中的方向键 | 滚动源码；长行横向滚动 |
| 代码区域中的 `Ctrl+C`（macOS 上为 `Cmd+C`） | 复制选中的文本 |

## 动效

- 展开或收起时，代码的高度以平滑的弹簧变化。
- 新代码会淡入并轻微上升，使更新清晰可见，而不会突然跳动。
- 展开切换按钮的文字以短暂的模糊切换，箭头随之转动。
- 文件名以短暂的模糊切换，其后的语言标签滑动到新的位置。
- 减弱动效时，高度立即设定，文字原位切换，没有移动。关闭动效时直接跳到最终状态。

| 变化 | 完整 | 减弱 | 关闭 |
|---|---|---|---|
| 展开或收起 | 高度以弹簧变化到新尺寸，代码区域裁剪至该尺寸 | 高度立即设定 | 立即 |
| 新的 `Code` | 文字在淡入的同时上升 4 px | 文字原位替换 | 立即 |
| 切换标签 | 标签模糊地淡出后淡入，箭头在快速弹簧上转动 180 度 | 标签原位切换，箭头立即转动 | 立即 |
| 新的 `FileName` | 名称模糊地淡出后淡入，语言标签滑动到新位置 | 名称原位切换 | 立即 |

## 相关组件

- `CopyButton`：复制单个值，例如令牌或 URL。
- `JsonViewer`：用户需要搜索和浏览的 JSON。
- `TreeView`：放在代码块旁边，让所选文件决定显示的代码。
- `Markdown`：混合了说明文字与围栏代码的文档。

| 组件 | 适用场景 |
|---|---|
| `CopyButton` | 要复制的是一个值，而不是文件或片段 |
| `JsonViewer` | 人们需要搜索、展开或复制 JSON 文档的一部分 |
| `TreeView` | 文件构成层级，显示的代码取决于所选内容 |
| `Markdown` | 代码嵌在说明文字中，例如帮助文章或变更日志 |
| `Textarea` | 文本是纯文本，人们需要编辑它 |
