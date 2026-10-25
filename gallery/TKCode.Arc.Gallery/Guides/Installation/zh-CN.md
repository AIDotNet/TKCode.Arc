---
title: 安装
source-hash: 8cb6181df977
---

# 安装

本指南介绍如何把 TKCode.Arc 添加到 Avalonia 桌面应用中，从包引用一直到第一个界面。在新项目上大约需要五分钟，并且不会改变你的 Avalonia 代码其余部分的工作方式。

## 环境要求

| 项目 | 版本 | 说明 |
|---|---|---|
| .NET | `net10.0` 或 `net11.0` | 这些包同时面向两个目标框架。 |
| Avalonia | `12.1.4` | 固定为库构建时所用的确切版本。 |
| AtomUI | `6.2.8` | `AtomUI.Desktop.Controls`；Arc 的主题与令牌系统基于 AtomUI。 |
| 字体（可选） | `Avalonia.Fonts.Inter` | Arc 的正文使用 Inter；Geist 来自 `TKCode.Arc.Fonts`。 |

TKCode.Arc 目前处于预览阶段，AtomUI 与 Avalonia 都固定为确切版本。请在你的应用中使用相同的 Avalonia 版本；版本混用会导致还原警告，并在运行时出现样式缺失或类型加载错误。

## 1. 添加包

```bash
dotnet add package TKCode.Arc --version 0.3.1-preview.1
dotnet add package TKCode.Arc.Fonts --version 0.3.1-preview.1           # 可选：Geist 与 Geist Mono
dotnet add package TKCode.Arc.Icons.Lucide --version 0.3.1-preview.1    # 可选：完整的 Lucide 图标集
dotnet add package Avalonia.Fonts.Inter --version 12.1.4                 # 可选：正文使用的 Inter
```

`TKCode.Arc` 会引入 `TKCode.Arc.Core`、AtomUI 和 StreamMark。如果你的解决方案使用集中式包管理，请把版本号一次性添加到 `Directory.Packages.props` 中，并让项目文件不包含版本号：

```xml
<ItemGroup>
    <PackageVersion Include="TKCode.Arc" Version="0.3.1-preview.1" />
    <PackageVersion Include="TKCode.Arc.Fonts" Version="0.3.1-preview.1" />
    <PackageVersion Include="TKCode.Arc.Icons.Lucide" Version="0.3.1-preview.1" />
    <PackageVersion Include="Avalonia.Fonts.Inter" Version="12.1.4" />
</ItemGroup>
```

请固定确切的预览版本。像 `0.1.*` 这样的浮动版本可能会在下一次还原时拉取到带有破坏性变更的预览版。

## 2. 在应用构建器中注册 Arc

Arc 在 AtomUI 的构建器中注册，与 AtomUI 的桌面控件并列。一个典型的 `Program.cs` 如下：

```csharp
using AtomUI;
using AtomUI.Desktop.Controls;
using Avalonia;
using TKCode.Arc;
using TKCode.Arc.Fonts;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args) => BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
                  .UsePlatformDetect()
                  .WithInterFont()      // 正文使用的 Inter（Avalonia.Fonts.Inter）
                  .WithArcFonts()       // 展示文字使用 Geist，代码使用 Geist Mono（TKCode.Arc.Fonts）
                  .UseAtomUI(builder =>
                  {
                      builder.UseDesktopControls();
                      builder.UseArc();
                  });
}
```

各个调用的作用：

| 调用 | 用途 |
|---|---|
| `UseAtomUI(...)` | 为应用启动 AtomUI 的主题管理器、设计令牌与本地化。 |
| `UseDesktopControls()` | AtomUI 的桌面控件包，Arc 构建于其上。 |
| `UseArc()` | 注册 Arc 的控件、主题与令牌。它也会注册 `TKCode.Arc.Core`（`UseArcCore()`），因此你无需自行调用。 |
| `WithInterFont()`、`WithArcFonts()` | 注册 Arc 字号体系所设计的字体。若不注册，文字会回退到系统字体。 |

把 `Application` 类声明为 `partial`。AtomUI 的源代码生成器会为它添加启动代码；如果该类不是 partial，构建会因 `ATOMUILOC008` 而停止：

```csharp
public partial class App : Application
{
    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow();
        }
        base.OnFrameworkInitializationCompleted();
    }
}
```

## 3. 在 XAML 中使用控件

Arc 的 XAML 命名空间是 `https://github.com/AIDotNet/TKCode.Arc`；所有控件、令牌资源与标记扩展都位于其中。

![A notification settings card built in the first minutes with Arc](images/FirstScreen.png)

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc"
             Width="560">
    <arc:Card Title="Notifications" Description="Choose how Acme Analytics keeps your team informed.">
        <StackPanel Spacing="16" Margin="0,12,0,0">
            <arc:Input Label="Alert email"
                       Text="ops@acme.com"
                       Description="Incidents and failed payments are sent here." />
            <arc:Switch Content="Weekly usage digest" IsChecked="True" />
            <arc:Switch Content="Product announcements" />
            <StackPanel Orientation="Horizontal" Spacing="8" HorizontalAlignment="Right">
                <arc:Button Content="Cancel" Variant="Secondary" />
                <arc:Button Content="Save changes" />
            </StackPanel>
        </StackPanel>
    </arc:Card>
</UserControl>
```

这些控件的行为与它们派生自的 Avalonia 控件一致：将 `Text`、`IsChecked` 和 `Command` 绑定到视图模型，用选择器为它们设置样式，并使用 Avalonia 的数据验证。本图库中的每个组件页面都展示了该控件的绑定、事件与键盘操作模型。

### 与 Avalonia 控件的名称冲突

`Button`、`Calendar`、`Carousel`、`ContextMenu`、`DatePicker`、`Slider`、`SplitButton`、`TabItem`、`TimePicker`、`TreeView` 和 `TreeViewItem` 与 `Avalonia.Controls` 中的类型同名。在 XAML 中，`arc:` 前缀可以把它们区分开。在 C# 中，如果同一个文件同时使用两者，请为其中一个命名空间设置别名：

```csharp
using Avalonia.Controls;
using ArcButton = TKCode.Arc.Button;

var save = new ArcButton { Content = "保存更改" };
```

## 可选包

### 图标

Arc 自有的图标字形（`{x:Static arc:ArcIcons.Check}` 以及另外 186 个）覆盖了组件使用的所有图标。若需要其他 Lucide 图标，请添加 `TKCode.Arc.Icons.Lucide` 并使用它的命名空间：

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:lucide="https://github.com/AIDotNet/TKCode.Arc/lucide">
    <lucide:LucideIcon Kind="Sparkles" Size="20" />
</UserControl>
```

### 区块与数据表格

`TKCode.Arc.Blocks`（页面级区块）、`TKCode.Arc.AdvancedCharts`（`DataGrid`）和 `TKCode.Arc.Special`（AI 动画讲解）是独立的包。按需添加，并把它们注册在 `UseArc()` 旁边：

```bash
dotnet add package TKCode.Arc.Blocks --version 0.3.1-preview.1
dotnet add package TKCode.Arc.AdvancedCharts --version 0.3.1-preview.1
dotnet add package TKCode.Arc.Special --version 0.3.1-preview.1
dotnet add package TKCode.Arc.Workflow --version 0.3.1-preview.1
dotnet add package TKCode.Arc.Themes.Pixel --version 0.3.1-preview.1    # 可选：像素风主题（UseArcPixelTheme()）
dotnet add package TKCode.Arc.Mobile --version 0.3.1-preview.1    # 可选：移动端控件，尚未在真机上验证（UseArcMobile()）
```

```csharp
builder.UseDesktopControls();
builder.UseArc();
builder.UseArcBlocks();          // TKCode.Arc.Blocks
builder.UseArcAdvancedCharts();  // TKCode.Arc.AdvancedCharts
builder.UseArcSpecial();         // TKCode.Arc.Special
```

### 语言

应用语言由 AtomUI 处理。若要提供多种语言，请在构建器中声明它们；本图库以这种方式提供英文、简体中文和繁体中文：

```csharp
builder.UseLanguages(LanguageTags.EnUS, [LanguageTags.EnUS, LanguageTags.ZhCN, LanguageTags.ZhTW]);
```

Arc 控件为其内置文字提供英文默认值（例如 "Show password" 或 "Clear search"）。控件将这些文字以属性或标签对象的形式公开，因此你可以在自己的资源中进行翻译。

## 发布

- **NativeAOT 与裁剪。** 这些库兼容裁剪与 AOT，且不使用反射。若要将桌面应用发布为单个自包含文件，请使用 `-p:PublishAot=true`；若不使用 AOT，则使用 `-p:PublishSingleFile=true --self-contained`。
- **全球化。** 不要启用 `InvariantGlobalization`。输入框、选择器与图表中的数字、货币和日期格式化需要区域文化数据。
- **macOS。** 仓库中的 `tools/macos` 脚本演示了如何将图库打包为已签名并完成公证的 `.app`；同样的流程也适用于你的应用。

## 从源代码构建

构建仓库需要 .NET 11 SDK（见 `global.json`）以及 .NET 10 运行时，AtomUI 的构建任务运行在其上：

```bash
dotnet build TKCode.Arc.slnx
dotnet test tests/TKCode.Arc.Core.Tests
dotnet test tests/TKCode.Arc.Tests
dotnet run --project gallery/TKCode.Arc.Gallery.Desktop -f net10.0
```

## 故障排查

| 现象 | 原因 | 解决方法 |
|---|---|---|
| Arc 控件没有样式或显示为空 | 没有在 `UseAtomUI(...)` 中调用 `UseArc()` | 把它注册在 `UseDesktopControls()` 之后。 |
| 构建错误 `ATOMUILOC008` | `Application` 类不是 `partial` | 将其声明为 `public partial class App : Application`。 |
| 标题与代码使用系统字体 | 没有注册字体 | 在 `AppBuilder` 上调用 `WithInterFont()` 和 `WithArcFonts()`。 |
| 数字或日期格式错误，或格式化时抛出异常 | 启用了 `InvariantGlobalization` | 从项目文件中移除它。 |
| `'Button' is an ambiguous reference` | `TKCode.Arc` 与 `Avalonia.Controls` 同时被导入 | 为其中一个设置别名，如上所示。 |
| 关于 Avalonia 版本的还原警告 | 其他包引入了不同版本的 Avalonia | 在应用中显式引用 Avalonia `12.1.4`。 |
| 找不到 Blocks、`DataGrid` 或 Special 类型 | 没有引用该包或没有注册 | 添加 `TKCode.Arc.Blocks`、`TKCode.Arc.AdvancedCharts` 或 `TKCode.Arc.Special`，并调用 `UseArcBlocks()`、`UseArcAdvancedCharts()` 或 `UseArcSpecial()`。 |

## 下一步

- **主题**：浅色与深色模式、八种强调色以及设计令牌。
- **动效**：动效级别与减弱动效。
- 组件页面：从按钮、输入框和卡片开始，它们是大多数界面的基础。
