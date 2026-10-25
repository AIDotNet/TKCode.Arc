# TKCode.Arc

[English](README.md) | 简体中文

面向 Avalonia 的 Arc 风格组件库，基于 [AtomUI](https://github.com/AtomUI/AtomUI) 构建。
TKCode.Arc 移植了 [Arc](https://uiarc.dev) 的免费组件（22 个分组共 111 个组件），
保留 Arc 的视觉语言和弹簧动效。

- 目标框架 `net10.0` 和 `net11.0`；支持桌面平台（Windows、macOS、Linux）。
- 依赖 AtomUI 6.2.8 和 Avalonia 12.1.4（精确版本），Markdown 与代码高亮依赖
  [StreamMark](https://www.nuget.org/packages/TKCode.StreamMark.Highlighting.TextMate)。
- 设计文档：[design/overview.md](design/overview.md)。
- 在线体验站点：<https://docs.tkcode.si/>，在浏览器里查看全部组件。

## 包

| 包 | 内容 |
|---|---|
| `TKCode.Arc` | 全部组件（`UseArc()`），目录结构与 Arc 侧边栏一致，另含流式渲染的 `Markdown` 控件。 |
| `TKCode.Arc.Core` | ArcDesign 令牌与调色板、弹簧动效引擎、共享基础组件、187 个内置 Lucide 图标。 |
| `TKCode.Arc.Icons.Lucide` | 完整的 Lucide 图标集（2155 个图标）：`LucideIcon` 控件和 `LucideGeometry` 标记扩展。 |
| `TKCode.Arc.Fonts` | Geist 与 Geist Mono 字体（`WithArcFonts()`）；正文使用 `Avalonia.Fonts.Inter` 提供的 Inter。 |

## 安装

最新预览版为 [`0.3.0-preview.5`](CHANGELOG.md)，已发布到 [NuGet](https://www.nuget.org/packages/TKCode.Arc)：

```bash
dotnet add package TKCode.Arc --version 0.3.0-preview.5
dotnet add package TKCode.Arc.Fonts --version 0.3.0-preview.5           # 可选：Geist 与 Geist Mono 字体
dotnet add package TKCode.Arc.Icons.Lucide --version 0.3.0-preview.5    # 可选：完整的 Lucide 图标集
```

`TKCode.Arc` 会带上 `TKCode.Arc.Core`。`TKCode.Arc.Blocks`、`TKCode.Arc.AdvancedCharts` 与 `TKCode.Arc.Special`
（Special components 区的 AI 动画讲解组件）是独立的 NuGet 包，需要时单独安装；工作流画布 `TKCode.Arc.Workflow`（`UseArcWorkflow()`）随 0.3.0-preview.5 起一同发布。版本号带 `-preview` 后缀，各版本之间 API 仍可能变化。

## 快速开始

```csharp
AppBuilder.Configure<App>()
    .UsePlatformDetect()
    .WithInterFont()
    .WithArcFonts()          // 可选：Geist 展示字体和 Geist Mono 代码字体
    .UseAtomUI(builder =>
    {
        builder.UseDesktopControls();
        builder.UseArc();
    });

public partial class App : Application { }
```

```xml
<Window xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
  <StackPanel Spacing="12">
    <arc:Button Content="Save changes" />
    <arc:Button Content="Cancel" Variant="Secondary" />
    <arc:Switch Content="Email notifications" IsChecked="True" />
    <arc:CopyButton Value="npx arc add copy-button" />
    <arc:Button Content="Share" arc:Tooltip.Tip="Copy link" />
    <arc:Calendar ShowToday="True" />
  </StackPanel>
</Window>
```

图标：Arc 内置图标（`{x:Static arc:ArcIcons.Check}`）覆盖了所有组件用到的图标；其他 Lucide 图标
来自图标包：

```xml
<Window xmlns:lucide="https://github.com/AIDotNet/TKCode.Arc/lucide">
  <lucide:LucideIcon Kind="Sparkles" Size="20" />
</Window>
```

主题色、外观与动效级别：

```csharp
var config = new ThemeConfigBuilder()
    .WithAlgorithms(ThemeAlgorithm.Default, ThemeAlgorithm.Dark)
    .WithArc(arc => arc.Accent(ArcAccentPreset.Violet))
    .Build();
await Application.Current!.GetThemeManager()!.ApplyThemeAsync(
    new ThemeRequest(IThemeManager.DEFAULT_THEME_ID, config, ThemeTransitionReason.UserRequest));

ArcMotion.SetLevel(window, ArcMotionLevel.Reduced);   // Full、Reduced 或 Off
```

Markdown 与代码：`CodeBlock` 开箱即用地高亮代码（TextMate 语法，使用 Arc 调色板着色），`Markdown`
以相同的样式渲染完整文档或流式回复：

```csharp
var reply = new Markdown();
reply.Append("Here is the **fix**:\n\n```csharp\n");
reply.Append("var total = items.Sum(i => i.Price);\n```\n");
reply.Complete();
```

应用开发注意事项：

- 不要启用 `InvariantGlobalization`（数字和日期格式化依赖区域性数据）。
- 锚点弹出层（菜单、选择器、工具提示、气泡卡片）绘制在窗口的覆盖层中，与触发元素的距离正好是
  `sideOffset`，不会遮挡触发元素。
- `Button`、`Calendar`、`Carousel`、`ContextMenu`、`DatePicker`、`Slider`、`SplitButton`、`TabItem`、`TimePicker`、
  `TreeView` 和 `TreeViewItem` 与 `Avalonia.Controls` 中的类型同名；同一文件同时引用两者时，请为其中一个命名空间设置别名。

## 构建

需要 .NET 11 SDK（见 `global.json`）和 .NET 10 运行时（AtomUI 的构建任务运行在其上）。

```bash
dotnet build TKCode.Arc.slnx
dotnet test tests/TKCode.Arc.Core.Tests
dotnet test tests/TKCode.Arc.Tests
dotnet run --project gallery/TKCode.Arc.Gallery.Desktop -f net10.0
```

## 赞助

TKCode.Arc 主要由 [nextco.work](https://nextco.work/) 赞助，其提供低价、好用的 Claude、OpenAI 和 DeepSeek 模型服务。
感谢对本项目的支持。

## 许可证

MIT。Arc（MIT）、Lucide（ISC）、字体（OFL）、StreamMark、TextMateSharp、Oniguruma 和 Markdig，
以及 AtomUI（LGPL-3.0）依赖的许可信息见 [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md)。
