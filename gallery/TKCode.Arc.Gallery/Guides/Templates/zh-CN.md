---
title: 模板
source-hash: e07cc444920a
---

# 模板

`TKCode.Arc.Templates` 提供 `dotnet new` 模板，用来创建基于 TKCode.Arc 的应用，以及往应用里添加常用部件。新应用创建后即可构建和运行：它引用与模板同时发布的 Arc 和 Avalonia 版本，Arc 主题、Inter 和 Geist 字体、Lucide 图标以及明暗主题切换都已配置好。

```bash
dotnet new install TKCode.Arc.Templates
dotnet new tkcode-arc -n MyApp --layout sidebar
cd MyApp
dotnet run
```

Visual Studio 和 Rider 会在“新建项目”对话框中列出这些模板（**TKCode.Arc App**、**TKCode.Arc Cross-platform App**），选项显示为复选框和列表。

## 模板一览

| 模板 | 短名称 | 生成内容 |
|---|---|---|
| TKCode.Arc App | `tkcode-arc` | 桌面应用（Windows、macOS、Linux） |
| TKCode.Arc Cross-platform App | `tkcode-arc-xplat` | 同样的应用，拆成共享项目，加上桌面宿主和浏览器（WebAssembly）宿主 |
| TKCode.Arc Admin App | `tkcode-arc-admin` | 完整的 SaaS 后台应用：登录、仪表盘、客户、团队、账单、设置 |
| TKCode.Arc AI Chat App | `tkcode-arc-ai-chat` | 完整的 AI 聊天客户端：对话列表、流式 Markdown 回复、模型选择、附件 |
| TKCode.Arc Developer Tool | `tkcode-arc-devtool` | 完整的 IDE 式工具：文件浏览、编辑器标签、属性、构建输出、保存的布局 |
| `tkcode-arc-workflow` | 步骤面板、可编辑的 `WorkflowCanvas`（拖动连线、Delete 删除、撤销与重做、自动排列），带小地图和缩放控件；选中步骤的属性检查器；Run 依次运行各步骤。流程保存在 `workflow.json` 中，导入和导出使用同样的 JSON。 |
| `tkcode-arc-projects` | 侧边栏切换 `KanbanBoard`（拖动卡片、快速添加、卡片详情）、`GanttTimeline`、`WeekCalendar` 和 `ReleaseReadiness` 检查清单。 |
| `tkcode-arc-analytics` | 页面切换 `KpiDrilldown`、`RevenueExplorer`、`CohortRetention`、`UsageForecast` 和 `UptimeStatus`。 |
| TKCode.Arc Workflow Editor | `tkcode-arc-workflow` | 完整的流程编辑器：步骤面板、可编辑画布、属性检查器、运行、JSON 导入导出 |
| TKCode.Arc Project Planner | `tkcode-arc-projects` | 完整的项目规划应用：看板、甘特时间线、周日历、发布检查清单 |
| TKCode.Arc Analytics Dashboard | `tkcode-arc-analytics` | 完整的数据看板：KPI 下钻、收入、留存分组、用量预测、服务状态 |
| TKCode.Arc Component Library | `tkcode-arc-lib` | 基于 Arc 的控件库：示例控件及其 Token 和主题、测试、示例应用，可直接打包 |
| TKCode.Arc Page | `tkcode-arc-page` | `Pages/<Name>Page.axaml`，可带 ViewModel |
| TKCode.Arc Window | `tkcode-arc-window` | `Views/<Name>Window.axaml`：带按钮页脚的次级窗口 |
| TKCode.Arc Dialog | `tkcode-arc-dialog` | `Dialogs/<Name>Dialog.axaml`：放在 Arc `Dialog` 中的表单，带可等待的 `ShowAsync` |
| TKCode.Arc Control | `tkcode-arc-control` | `Controls/<Name>/`：模板化控件、它的设计 Token 类和主题文件 |

## 布局

`--layout` 决定窗口结构。每种布局都把内容放在 `Views/MainView.axaml` 中：桌面窗口显示它，浏览器则直接显示它，不需要窗口。

| 布局 | 内容 |
|---|---|
| `blank`（默认） | 顶栏（应用名称和主题开关）加一个带欢迎卡片的页面。 |
| `sidebar` | 来自 `TKCode.Arc.Blocks` 的可收起 `Sidebar`：图标、64 px 图标栏（Cmd+B / Ctrl+B），以及三个页面：首页、项目（带“新建项目”操作的列表）和设置（外观和强调色）。 |
| `header` | 同样的三个页面，用顶栏中的 `SegmentedControl` 切换。 |
| `dock` | 来自 `TKCode.Arc.Dock` 的 IDE 式布局：工具面板、文档标签和输出面板，可以拖动停靠、自动隐藏或浮动，并有“重置布局”操作。 |

## 选项

| 选项 | 取值 | 默认 | 说明 |
|---|---|---|---|
| `--layout` | `blank`、`sidebar`、`header`、`dock` | `blank` | 窗口布局（见上文）。 |
| `--mvvm` | `none`、`toolkit` | `none` | `toolkit`：用 CommunityToolkit.Mvvm 编写的 ViewModel（源生成的属性和命令）和编译绑定。 |
| `--di` | | 关 | 用 Microsoft.Extensions.DependencyInjection 创建 ViewModel 和服务（`Services/AppServices.cs`）。隐含 `--mvvm toolkit`。 |
| `--settings` | `true`、`false` | `true` | 保存外观、强调色、窗口大小与位置以及布局状态（侧边栏的收起、停靠面板、语言），并在下次启动时恢复。 |
| `--i18n` | | 关 | 把界面文字放在 `Localization/*.xlf`（English、简体中文、繁體中文）中，并加入语言切换。 |
| `--ui-language` | `en-US`、`zh-CN`、`zh-TW` | `en-US` | 界面文字的语言；与 `--i18n` 一起使用时，是应用启动时的语言。 |
| `--tests` | | 关 | 加入一个在 Avalonia Headless 平台上启动应用的测试项目，以及一个解决方案。 |
| `--custom-titlebar` | | 关 | 用 Arc 的标题栏（`ArcWindow`）代替系统标题栏，并可在其中放入自己的控件。 |
| `-f`、`--framework` | `net10.0`、`net8.0`、`net11.0` | `net10.0` | 目标框架。`net11.0` 需要 .NET 11 SDK；跨平台模板只有 `net10.0`。 |
| `--accent` | `Neutral`、`Violet`、`Blue`、`Green`、`Amber`、`Orange`、`Coral`、`Rose` | `Neutral` | 应用启动时的强调色。 |
| `--no-restore` | | | 创建项目后不自动还原。 |

选项可以自由组合，例如：

```bash
dotnet new tkcode-arc -n Studio --layout dock --mvvm toolkit --tests
dotnet new tkcode-arc -n Console --layout header --i18n --ui-language zh-CN --custom-titlebar
```

## 生成的文件

```text
MyApp/
  MyApp.csproj        TKCode.Arc, TKCode.Arc.Fonts, TKCode.Arc.Icons.Lucide, Avalonia.Desktop, Avalonia.Fonts.Inter
                      (+ TKCode.Arc.Blocks, TKCode.Arc.Dock, CommunityToolkit.Mvvm ... as the options need them)
  Program.cs          the desktop entry point
  AppSetup.cs         UseArcApp(): the fonts, UseArc(...) with Arc's packages, the languages and the start-up theme
  App.cs              creates the main window (on the desktop) or the main view (in a browser); no App.axaml
  Views/              MainWindow (the desktop window) and MainView (the layout)
  Navigation/         sidebar and header layouts: AppPages (the page list) and PageHost (shows the current page)
  Pages/, Models/     sidebar and header layouts: the sample pages and their sample data
  ViewModels/         --mvvm toolkit
  Services/           --di
  Settings/           --settings: AppSettings (JSON, generated serializer, trimming and AOT safe)
  Localization/       --i18n: AppLangResourceKind, en-US.xlf, zh-CN.xlf, zh-TW.xlf, AppLanguages
```

使用 `--tests` 时，应用移到 `MyApp/MyApp/`，测试位于 `MyApp/MyApp.Tests/`，`MyApp/MyApp.slnx` 包含两者。在解决方案目录运行 `dotnet test` 即可执行测试；测试把设置保存在内存中，不会改动你自己的设置。

只有应用用到的 Lucide 图标会被编译进去。运行时才决定的图标需要在项目文件中声明，例如 `<LucideIcon Include="Bell;ArrowRight" />`。

## 跨平台应用

`tkcode-arc-xplat` 的布局和选项与 `tkcode-arc` 相同，生成一个解决方案：

```text
MyApp/
  MyApp.slnx
  MyApp/              the app: views, view models, AppSetup.cs (a library; everything you write goes here)
  MyApp.Desktop/      the desktop host
  MyApp.Browser/      the browser host (WebAssembly): index.html, main.js, and a Chinese font when the UI has Chinese text
```

```bash
dotnet workload install wasm-tools-net10    # once; with the .NET 10 SDK: wasm-tools
dotnet new tkcode-arc-xplat -n MyApp --layout sidebar
cd MyApp
dotnet run --project MyApp.Desktop
dotnet run --project MyApp.Browser          # then open the address it prints
```

在浏览器中，设置保存在页面的本地存储里。浏览器不为应用提供系统字体，所以界面含有中文时，浏览器宿主会带上 Noto Sans SC 子集（SIL Open Font License），用来显示中文。

## 从完整的应用开始

场景模板生成的应用一创建就能用：真实的界面由 Arc 的 Block 组成，示例数据放在服务接口后面。MVVM（CommunityToolkit.Mvvm）、依赖注入和设置保存始终开启。界面是英文的，因为 Block 的内置文字是英文（每个 Block 通过自己的 `Labels` 修改）。

```bash
dotnet new tkcode-arc-admin -n Backoffice
dotnet new tkcode-arc-ai-chat -n Assistant --tests
dotnet new tkcode-arc-devtool -n Studio --custom-titlebar
```

| 模板 | 内容 |
|---|---|
| `tkcode-arc-admin` | 先是登录页（`LoginSplit`；示例账户的密码是 `demo`），然后是侧边栏和这些页面：仪表盘（`MetricCard`、`LineChart`）、客户（带搜索、状态筛选、分页和详情 `Drawer` 的 `Table`）、团队（`TeamMembers`、`RolesPermissions`）、账单（`BillingOverview`）和设置（`SettingsPage`）。顶栏有命令面板（Cmd+K / Ctrl+K）和通知。 |
| `tkcode-arc-ai-chat` | `AiChat` 旁边是对话列表：流式 Markdown 回复，带推理过程、附件、模型菜单和建议。`SampleResponder` 不接任何模型，流式输出预设的回复；它的注释里有一个基于 `Microsoft.Extensions.AI` 的 `IChatClient` 的写法。对话保存在设置旁边的 `conversations.json` 中。 |
| `tkcode-arc-devtool` | `DockLayout` 布局：文件浏览（`TreeView`）、编辑器标签（Cmd+S / Ctrl+S 保存）、`PropertyGrid`、构建输出和问题列表、Run 按钮、命令面板和“重置布局”。打开的文件随布局一起保存，下次启动时恢复。 |

它们接受 `--tests`、`--custom-titlebar`、`-f`、`--accent` 和 `--no-restore`，作用与 `tkcode-arc` 相同。要改成自己的应用，替换 `SampleData/` 中的示例服务即可：页面只与 `Services/` 中的接口交互，`Services/AppServices.cs` 注册由谁来实现它们。

开发工具没有终端面板。`TKCode.Arc.Terminal` 是终端模拟器，背后没有进程，需要你自己接上 pty；等应用有了要交互的进程再加入。

## 编写控件库

`tkcode-arc-lib` 创建一个基于 Arc 的自有控件库：

```bash
dotnet new tkcode-arc-lib -n Acme.Controls
cd Acme.Controls
dotnet test
dotnet run --project samples/Acme.Controls.Sample
dotnet pack src/Acme.Controls -c Release -o artifacts
```

`src/<name>/` 中有一个示例控件（`StatTile`）及其设计 Token 类和主题，以及供 `UseArc(...)` 调用的 `Use<Name>()` 方法，它注册 Foundation 生成器找到的全部内容。`tests/` 在 Avalonia 的 Headless 平台上运行这些控件，`samples/` 是展示它们的桌面应用。`--tests false` 和 `--sample false` 可以去掉这两个项目。示例 Token 的身份以库名开头（`AcmeControlsStatTile`），因为身份在应用注册的所有包（包括 Arc 自己的）中必须唯一。

## 往应用里添加部件

在项目已还原之后，在项目目录中运行项模板（它们读取项目的 `RootNamespace`；否则请传入 `--namespace`）：

```bash
dotnet new tkcode-arc-page -n Reports                 # Pages/ReportsPage.axaml
dotnet new tkcode-arc-page -n Reports --view-model    # ... and ViewModels/ReportsPageViewModel.cs
dotnet new tkcode-arc-window -n About                 # Views/AboutWindow.axaml
dotnet new tkcode-arc-dialog -n Rename                # Dialogs/RenameDialog.axaml
dotnet new tkcode-arc-control -n StatusTile           # Controls/StatusTile/: StatusTile.cs, StatusTileToken.cs, Themes/StatusTileTheme.axaml
```

- **页面**：要显示它，把它加入 `Navigation/AppPages.cs`，并在导航中添加一个键相同的条目。页面 `.axaml.cs` 顶部的注释写有这两行代码。
- **对话框**：在窗口中的任意控件里调用 `if (await RenameDialog.ShowAsync(this, "Draft") is { } result) { ... }`；返回 null 表示已取消。
- **控件**：应用自己的程序集在 `UseArc(...)` 中注册后，Arc 才会加载控件的主题并发布它的 Token。`AppSetup.cs` 中有这行代码，默认被注释掉（使用 `--i18n` 时已经启用）；添加第一个控件时取消注释即可。Token 类名去掉 `Token` 后不能与 Arc 已用的名称重复（`Card`、`Badge` 等）；如果重复，请设置 `[ControlDesignToken(Identity = "...")]`。

## 卸载

```bash
dotnet new uninstall TKCode.Arc.Templates
```
