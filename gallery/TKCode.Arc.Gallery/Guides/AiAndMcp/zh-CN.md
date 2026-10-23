---
title: AI 与 MCP
source-hash: abfe18a713be
---

# AI 与 MCP

AI 编程助手能很好地配合 TKCode.Arc 使用，因为它需要的一切都以纯文本形式写成：每个组件都有一个根据其源代码编写的 Markdown 页面，每个公共属性和事件都有 XML 描述，而且各部分的约定保持一致。生成的代码能否编译、会不会凭空捏造属性，关键在于助手拿到的上下文。本页说明这些上下文位于何处、如何提供给助手，以及如何审查它返回的结果。文档站点也为能自行获取内容的助手发布了同样的知识：`llms.txt` 索引、每个页面的 Markdown 版本，以及一个智能体技能（skill）。

## 助手应先了解的事项

助手在本库上出错，大多源于几个错误的假设。请在项目指令文件中或对话开始时先把这些事实说明清楚：

| 事实 | 重要原因 |
|---|---|
| 这是 Avalonia 12 与 AtomUI 6.2.8，而不是 WPF、WinUI 或 HTML。 | 否则助手会混入 WPF 属性（`Visibility`、`DataTemplateSelector`）或 CSS 的思路。 |
| Arc 控件位于 `xmlns:arc="https://github.com/AIDotNet/TKCode.Arc"`。 | 没有该前缀时，`Button` 会解析为 Avalonia 自带的按钮，失去 Arc 的外观。 |
| 应用在 `UseAtomUI(...)` 中调用 `UseArc()`，并且 `Application` 类是 `partial`。 | 缺少其中任何一项，都会导致控件没有样式或构建错误。 |
| 颜色、圆角与字体来自 `{arc:ArcDesignTokenResource ...}` 或 `ArcTokens`。 | 硬编码的十六进制值会破坏深色模式与强调色。 |
| 动效声明在 `^:motion-full` 与 `^:motion-reduced` 之下，并遵循 `ArcMotion.Level`。 | 手写的动画会忽略减弱动效的设置。 |
| 只存在文档中列出的属性。 | 组件页面与 API 表格列出了全部属性；其他属性都是凭空捏造的。 |

## 知识的来源

| 来源 | 位置 | 最适合 |
|---|---|---|
| `llms.txt` | https://docs.tkcode.si/llms.txt | 最先交给助手的内容：安装步骤、规则，以及每个页面的一句话摘要和指向其 Markdown 的链接。 |
| 页面的 Markdown 版本 | 页面地址加 `.md`，例如 https://docs.tkcode.si/components/button.md（译文位于 `/zh-CN/` 与 `/zh-TW/` 之下） | 无需克隆仓库即可完整读取一个组件、区块或指南，包括示例源代码与 API 参考。 |
| 智能体技能 | https://docs.tkcode.si/skills/tkcode-arc/SKILL.md | 会加载技能的编程智能体：工作流程、规则、组件选择与审查清单。 |
| 组件页面 | `gallery/TKCode.Arc.Gallery/Docs/<Category>/<Component>/en-US.md`（另有 `zh-CN.md`、`zh-TW.md`） | 了解何时使用某个组件、用法、绑定、验证、无障碍、键盘与动效。 |
| 实时示例 | `gallery/TKCode.Arc.Gallery/Demos/<Category>/<Component>/*.axaml` | 页面中每个示例的可运行 XAML。 |
| API 描述 | 随包发布的 XML 文档 | 每个公共属性、事件与类型，即 IntelliSense 显示的内容以及 API 表格列出的内容。 |
| 本图库 | 文档区及每个组件页面 | 结合实时示例在上下文中阅读。 |
| 仓库约定 | 仓库根目录下的 `AGENTS.md` | 处理库本身的贡献者与助手。 |

### 复制页面

每个组件页面顶部都有 **复制页面**。它会将整个页面复制为 Markdown，包括每个实时示例的源代码（`.axaml` 及其代码后置文件）以及生成的 API 表格。在请求组件帮助时把它粘贴到对话中，助手便会依据真实的 API 与真实的示例工作，而不是凭记忆。

若某个界面使用了多个组件，请复制这些组件各自的页面；如果助手能直接读取文件，也可以让它查看仓库中的 Markdown 文件。

## llms.txt 与 Markdown 页面

位于 https://docs.tkcode.si 的图库是一个绘制在画布上的 Avalonia WebAssembly 应用，助手获取页面地址时拿不到任何文字。因此站点在每次发布时，都会根据文档页面生成同样内容的纯文本版本：

| 地址 | 内容 |
|---|---|
| `/llms.txt` | 采用 llms.txt 格式的索引：库的简介、安装方法、给智能体的规则，以及每个指南、组件与区块的一句话摘要和指向其 Markdown 的链接。 |
| `/llms-full.txt` | 全部英文页面合为一个文件，适合上下文窗口较大的助手。 |
| `/<section>/<slug>.md` | 单个页面：页面地址加 `.md`，例如 `/docs/theming.md`、`/components/button.md` 或 `/blocks/sidebar.md`。组件页面的内容与 **复制页面** 复制的相同，顶部另有包名与安装命令。 |
| `/zh-CN/…`、`/zh-TW/…` | 同样页面的简体中文与繁体中文版本，例如 `/zh-CN/components/button.md`。 |

对于能获取网页的助手，一句话就够了：

```text
使用 TKCode.Arc 编写界面代码之前，先阅读 https://docs.tkcode.si/llms.txt，再获取你所用每个组件的
Markdown 页面，并且只使用其 API 参考中列出的属性与事件。
```

## 智能体技能

技能（skill）是一个指令文件夹，编程智能体会在任务需要时加载它。TKCode.Arc 技能包含工作流程、本页中的规则、一张说明哪种需求适合哪个组件的表格，以及两个参考文件：

| 文件 | 内容 |
|---|---|
| `SKILL.md` | 工作流程（查找、阅读、安装、连接、样式、审查）、规则、常用令牌，以及快速选择组件的表格。 |
| `components.md` | 按分组列出的每个组件与区块，包括类名、用途与页面链接。 |
| `checklist.md` | 智能体在完成一个界面之前运行的审查清单。 |

要在某个项目中为 Claude Code 安装它，把这三个文件下载到 `.claude/skills/tkcode-arc/`：

```bash
mkdir -p .claude/skills/tkcode-arc
for file in SKILL.md components.md checklist.md; do
  curl -fsSL "https://docs.tkcode.si/skills/tkcode-arc/$file" -o ".claude/skills/tkcode-arc/$file"
done
```

若要在所有项目中使用，请把该文件夹放到 `~/.claude/skills/`；其他支持技能的智能体会从各自的技能文件夹读取同样的结构。这些文件注明了它们所描述的 TKCode.Arc 版本，升级之后请重新下载。

## 项目指令文件

Claude Code、Cursor 与 GitHub Copilot 等助手会读取仓库中的指令文件（例如 `AGENTS.md`、`CLAUDE.md` 或 `.github/copilot-instructions.md`）。像下面这样、并针对你的应用改写的一节，可以避免上述大部分错误：

```text
## UI 库：TKCode.Arc

- 界面使用 Avalonia 12.1.4 + AtomUI 6.2.8 + TKCode.Arc。不要使用 WPF 或 WinUI 的 API。
- Arc 控件使用 xmlns:arc="https://github.com/AIDotNet/TKCode.Arc"（arc:Button、arc:Input 等）。
- 只使用组件文档页面或 API 表格中列出的属性、事件与枚举值。
- 颜色、圆角、间距与字体：XAML 中使用 {arc:ArcDesignTokenResource Name}，代码中使用 ArcTokens.Bind。
  永远不要硬编码颜色。
- 表单：使用带有 Label 与 Description 的 arc:Input / arc:Select / arc:Switch；错误通过 ErrorMessage
  或被绑定属性上的数据验证显示。
- 动效：Arc 控件会自行产生动画。对于自定义控件，请在 ^:motion-full 与
  ^:motion-reduced 下声明过渡，并设置 arc:ArcMotion.Tracks="True"。
- 视图模型使用 CommunityToolkit.Mvvm（[ObservableProperty]、[RelayCommand]）。
- 组件文档：https://docs.tkcode.si/llms.txt（每个页面都有 Markdown 版本，例如
  https://docs.tkcode.si/zh-CN/components/button.md）
```

## 有效的提示词

把业务目标、组件页面和约束一并交给助手。对比如下：

| 不要这样写 | 应当这样写 |
|---|---|
| “做一个设置页面。” | “用 arc:Input 构建工作区设置视图，填写名称与账单邮箱；用 arc:Select 选择货币；用 arc:Switch 设置每周摘要。参考所附的 Input、Select 与 Switch 页面。绑定到使用 ObservableValidator 的 WorkspaceSettingsViewModel，并在邮箱失去焦点时验证。” |
| “加一个图表。” | “用 arc:LineChart 展示最近 30 天的每日活跃用户，参照所附的 LineChart 页面。数据来自 IAnalyticsService.GetDailyActiveUsersAsync()；数字按当前文化格式化。” |
| “让它看起来更好。” | “按照 Input 页面的内容指南调整此表单：句子式大小写的标签，每个有约束的字段都有辅助文字，错误提示要说明如何修正该值。” |

每次只请助手处理一个界面或一个组件，并请它列出所用的属性以及每个属性记录在哪里。这份清单很容易与 API 表格对照检查。

## 审查生成的代码

在接受助手编写的代码之前，请检查：

- **名称。** 每个 `arc:` 元素、属性、事件与枚举值都存在于该组件的 API 表格中。XAML 中不存在的属性会在构建时失败；参数类型错误的事件处理程序则会在之后出错。
- **令牌。** 视图中没有十六进制颜色或固定的圆角，改用令牌。
- **绑定模式。** 双向属性（`Text`、`IsChecked`、`SelectedItem`、`Value`）绑定到可设置的视图模型属性；只读属性（`EffectiveErrorMessage`、`SelectedValues`）仅用于读取。
- **无障碍。** 每个字段都有 `Label`（可见，或仅当屏幕上其他内容已为其命名时才以 `IsLabelVisible="False"` 隐藏）；只有图标的按钮都有无障碍名称。
- **动效。** 控件已经产生动画的地方不再手写动画；自定义过渡放在动效伪类之下。
- **线程。** 来自后台工作的值在 UI 线程上设置（`Dispatcher.UIThread`）。

技能中的 `checklist.md` 是这份清单的详细版本，供智能体对自己写的代码逐项检查。

## MCP

模型上下文协议（MCP）让助手可以从服务器读取资源并调用工具，而不必依赖对话中粘贴的内容。

TKCode.Arc 目前尚未提供自己的 MCP 服务器。在提供之前，有三种方式可以直接把文档交给助手：

- **能获取网页的助手**：让它从 https://docs.tkcode.si/llms.txt 开始（见上文）。
- **能读取文件的助手**（Claude Code、Cursor 等）：把仓库克隆到你的项目旁边，或将其加入工作区，并让助手指向 `gallery/TKCode.Arc.Gallery/Docs` 与 `.../Demos`。
- **只使用 MCP 的客户端**：在文档文件夹上运行文件系统 MCP 服务器。使用 MCP 项目的参考服务器时，客户端配置如下：

```json
{
  "mcpServers": {
    "tkcode-arc-docs": {
      "command": "npx",
      "args": ["-y", "@modelcontextprotocol/server-filesystem", "/path/to/AtomUI.Arc/gallery/TKCode.Arc.Gallery/Docs"]
    }
  }
}
```

这样助手就能列出各组件文件夹，并读取它所用的每个组件的页面。请让克隆的仓库与你应用中包的版本保持一致，使文档所描述的 API 与你编译所用的 API 相同。
