---
title: 窗口标题栏按钮
summary: 内容延伸到顶部的窗口所用的最小化、最大化和关闭按钮：放进你自己的页头，按钮跟随窗口状态，并在 Windows 上打开贴靠布局。
source-hash: 4f02708a501c
---

<Demo name="Preview" />

## 何时使用

`WindowCaptionButtons` 把 Arc 标题栏的最小化、最大化和关闭按钮做成了一个独立控件。窗口内容延伸到顶边、由应用自己绘制页头时使用它：例如 `IsTitleBarVisible="False"` 的 `ArcWindow` 中侧边栏一直延伸到顶部，页头同时充当标题栏，或者用一排标签页代替标题栏。

这些按钮像系统按钮一样控制所在的窗口：

- 最小化、最大化和关闭窗口，窗口最大化时最大化按钮显示为还原；
- `CanResize` 或 `CanMaximize` 为 false 时最大化按钮禁用，`CanMinimize` 为 false 时最小化按钮禁用；
- 在 Windows 上，它们会告诉系统自己是什么按钮，因此指向最大化按钮会打开 Windows 11 的贴靠布局。

## 何时不用

- **保留 Arc 标题栏的窗口**：`ArcWindow` 的 `TitleBar` 已经绘制同样的按钮。每个窗口只用一组。
- **macOS**：系统在每个窗口左上角保留红绿灯按钮。在 macOS 上隐藏这些按钮（见“平台”）。
- **浏览器和移动应用**：它们没有窗口，把内容放在单一视图中即可。
- **关闭面板或对话框**：使用 `Dialog` 或 `Drawer` 的关闭按钮，或一个图标 `Button`。

## 用法

隐藏 `ArcWindow` 的标题栏，把按钮放在你自己页头的右端。窗口仍把客户区扩展到装饰区域，不绘制自己的标题栏，并可从边缘调整大小。

```xml
<arc:ArcWindow xmlns="https://github.com/avaloniaui"
               xmlns:arc="https://github.com/AIDotNet/TKCode.Arc"
               Title="Atlas" IsTitleBarVisible="False">
    <DockPanel>
        <Grid DockPanel.Dock="Top" Height="48" ColumnDefinitions="*,Auto">
            <TextBlock Text="Inbox" Margin="20,0" VerticalAlignment="Center" />
            <arc:WindowCaptionButtons Grid.Column="1" Margin="0,0,8,0" />
        </Grid>
        <TextBlock Text="Content" />
    </DockPanel>
</arc:ArcWindow>
```

使用设置了 `ExtendClientAreaToDecorationsHint` 的普通 Avalonia `Window` 也可以，但在 Windows 和 Linux 上，Avalonia 会在你的内容之上再绘制一个自带标题栏按钮的标题栏。`ArcWindow` 会关闭这个标题栏。

预览下方的“Open a window”会打开这样一个窗口：页头里有搜索框和这些按钮，内容放在覆盖层宿主中。

### 选择按钮

`ShowMinimizeButton`、`ShowMaximizeButton` 和 `ShowCloseButton` 默认都为 `True`。把其中一个设为 `False` 即隐藏对应按钮；其余按钮靠拢，隐藏的按钮不占空间。尺寸固定的窗口显示最小化和关闭按钮，工具窗口只显示关闭按钮。

<Demo name="Buttons" />

### 移动窗口

这些按钮不会移动窗口，由你的页头负责。在 Windows 上，给页头设置 `TitleBar` 元素角色，给页头里的控件设置 `User` 角色；页头就成为系统标题栏：拖动它移动窗口，双击最大化，右键打开系统菜单，拖到屏幕边缘贴靠。给页头设置背景（`Transparent` 即可），让它的空白部分能被命中。在 macOS 和 Linux 上，改为在 `PointerPressed` 处理程序中移动窗口：在那里系统只会对直接命中的元素生效，或者吞掉双击。

```csharp
if (OperatingSystem.IsWindows())
{
    WindowDecorationProperties.SetElementRole(header, WindowDecorationsElementRole.TitleBar);
    WindowDecorationProperties.SetElementRole(search, WindowDecorationsElementRole.User);
}
header.PointerPressed += (_, e) =>
{
    // On Windows a press on the caption never gets here.
    if (e.Handled || !e.GetCurrentPoint(header).Properties.IsLeftButtonPressed)
    {
        return;
    }
    if (e.ClickCount == 2)
    {
        window.WindowState = window.WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    }
    else
    {
        window.BeginMoveDrag(e);
    }
    e.Handled = true;
};
```

`WindowDecorationProperties` 位于 `Avalonia.Controls.Chrome`，`WindowDecorationsElementRole` 位于 `Avalonia.Input`。`PageHeader`（在 `TKCode.Arc.Blocks` 中）在 `IsTitleBar` 为 `True` 时会完成以上全部工作。

### 让模态框留在标题栏下方

`Dialog`、`Drawer`、`BottomSheet` 等模态框在窗口的覆盖层上打开，覆盖整个窗口，也包括你的页头，因此模态框打开期间，标题栏按钮和拖动区域都在遮罩之下。要让它们始终可用，把应用内容放进 `ArcOverlayHost`，把页头放在它旁边、宿主之外。这样从内容中打开的模态框只覆盖宿主，永远不会盖住页头。

```xml
<arc:ArcWindow xmlns="https://github.com/avaloniaui"
               xmlns:arc="https://github.com/AIDotNet/TKCode.Arc"
               Title="Atlas" IsTitleBarVisible="False">
    <DockPanel>
        <!-- Outside the host: never under a scrim. -->
        <Grid DockPanel.Dock="Top" Height="48" ColumnDefinitions="*,Auto">
            <TextBlock Text="Atlas" Margin="20,0" VerticalAlignment="Center" />
            <arc:WindowCaptionButtons Grid.Column="1" Margin="0,0,8,0" />
        </Grid>
        <!-- The app: dialogs, drawers and sheets opened in here cover this area only. -->
        <arc:ArcOverlayHost>
            <TextBlock Text="Content" />
        </arc:ArcOverlayHost>
    </DockPanel>
</arc:ArcWindow>
```

应用需要始终显示在模态框之上的其他内容（例如启动画面）也用同样的方式放在宿主旁边。弹出层、菜单、工具提示和通知仍在窗口的覆盖层上打开。在预览打开的窗口中选择“Open a dialog”即可看到效果。

### 平台

- **Windows**：按钮由应用绘制，但行为与系统按钮相同：按下它们就是系统的最小化、最大化或关闭，指向最大化按钮会打开 Windows 11 的贴靠布局。
- **macOS**：系统保留红绿灯按钮。在 macOS 上隐藏这些按钮，并在页头左侧为红绿灯留出约 80 px。
- **Linux**：按钮的行为与 Windows 相同，但没有贴靠布局。

```xml
<arc:WindowCaptionButtons xmlns="https://github.com/avaloniaui"
                          xmlns:arc="https://github.com/AIDotNet/TKCode.Arc"
                          IsVisible="{OnPlatform True, macOS=False}" />
```

### 每个窗口一组按钮

一个窗口只显示一组标题栏按钮。如果有两组最大化和关闭按钮，Windows 对标题栏按钮和贴靠布局的命中测试就无法分辨哪一组属于窗口。`ArcWindow` 的 `TitleBar` 在 `CaptionButtons` 为 `System` 或 `None` 时会放弃其按钮的角色，用 `IsTitleBarVisible="False"` 隐藏的标题栏完全不参与命中测试，因此在这些窗口中你的按钮是唯一的一组。放在 `CaptionButtons="None"` 的标题栏的 `RightContent` 中的按钮同样会控制窗口。

### 预览

`IsPreview="True"` 只绘制按钮而不控制窗口：它们不会最小化、最大化或关闭任何窗口，系统也不会把它们当作标题栏按钮。本页的示意图都使用了它，因此不会影响 Gallery 窗口。

## API 参考

<!-- api: WindowCaptionButtons -->

## 无障碍

- 每个按钮都带有工具提示和无障碍名称，使用应用当前的语言：最小化、最大化或还原、关闭。
- 与系统标题栏一样，这些按钮不在 Tab 顺序中。键盘可以通过系统使用相同的命令：Windows 上 Alt+Space 打开系统菜单，Alt+F4 关闭窗口，Windows+向上键最大化窗口。
- 窗口不允许的操作（不能调整大小或最小化的窗口）对应的按钮处于禁用状态，图标变暗。

## 动效

- 指针悬停时，按钮的填充色和图标颜色渐变；关闭按钮变为危险色。
- 减弱动效或关闭动效时，颜色立即变化。
- 最小化、最大化和贴靠使用系统自己的动画。

## 响应式

- 按钮为 30 px 的正方形，间距 4 px（WindowCaptionButtonsToken 中的 `ButtonSize` 和 `ButtonSpacing`）：三个按钮共 98 px。在任何宽度下都要在页头末端留出这些空间，让页头自己的内容先让位。
- 隐藏的按钮不占空间，所以控件的宽度就是它显示的按钮的宽度。
- 按钮太小，不要把它们当作触控目标依赖。手机和浏览器没有窗口，只在桌面应用中显示它们。

## 相关组件

- `ArcWindow`：配合使用的窗口，设置 `IsTitleBarVisible="False"`。
- `PageHeader`（TKCode.Arc.Blocks）：`IsTitleBar="True"` 让页头成为窗口的标题栏。
- `Dialog`、`Drawer` 和 `BottomSheet`：覆盖最近的 `ArcOverlayHost` 或整个窗口的模态框。

| 组件 | 适用场景 |
|---|---|
| `ArcWindow` | 窗口保留 Arc 标题栏，标题栏在你的控件旁绘制同样的按钮 |
| `PageHeader` | 带面包屑、标题和操作的页头同时充当窗口的标题栏 |
| `Dialog` | 任务会打断页面；放在 `ArcOverlayHost` 中时不会盖住标题栏 |
| `Button` | 面板或卡片需要自己的关闭按钮 |
