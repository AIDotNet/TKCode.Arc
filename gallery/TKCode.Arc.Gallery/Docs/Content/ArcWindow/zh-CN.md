---
title: 窗口
summary: 带 Arc 标题栏的桌面窗口：左、中、右三个区域放置你的控件，保留原生拖动、贴靠和缩放，标题栏按钮跟随主题。
source-hash: b87bd013c4a2
---

<Demo name="Basic" />

## 何时使用

`ArcWindow` 用在窗口客户区内绘制的 Arc `TitleBar` 代替系统标题栏，作用类似 WPF 的 `WindowChrome`。标题栏使用主题的浅色和深色配色，你还可以在其中放置自己的控件。窗口的行为仍和系统窗口一致：

- 拖动标题栏移动窗口，双击最大化或还原；
- 拖动边缘调整大小；
- 在 Windows 上，标题栏就是系统标题栏：右键打开系统菜单，拖到屏幕边缘贴靠，指向最大化按钮打开 Windows 11 的贴靠布局；
- 在 macOS 上保留红绿灯按钮，标题栏为它们留出位置。

适用于桌面应用的主窗口：从顶边开始是一整块界面，标题栏里放菜单、标签页、搜索框或工具按钮。

## 何时不用

- **浏览器和移动应用**：它们没有窗口，把内容放在单一视图中即可。
- **窗口内的对话框**：使用 `Dialog`、`Drawer` 或 `BottomSheet`，它们在窗口的覆盖层中打开。
- **需要和系统中其他窗口外观一致的窗口**：普通的 Avalonia `Window` 保留系统标题栏。

## 用法

让主窗口派生自 `ArcWindow` 而不是 `Window`。不做其他设置时，标题栏显示窗口的 `Title`，在 Windows 和 Linux 上还显示最小化、最大化和关闭按钮。

```xml
<arc:ArcWindow xmlns="https://github.com/avaloniaui"
               xmlns:arc="https://github.com/AIDotNet/TKCode.Arc"
               Title="Atlas" Width="1200" Height="800">
    <TextBlock Text="Content" />
</arc:ArcWindow>
```

```csharp
public partial class MainWindow : ArcWindow
{
    public MainWindow() => InitializeComponent();
}
```

窗口把客户区扩展到装饰区域（`ExtendClientAreaToDecorationsHint` 为 true），并且在 Avalonia 原本会绘制标题栏的平台上不再绘制（使用空的 `WindowDecorationsTheme`）。它向系统请求与 Arc 标题栏等高的标题栏，因此 macOS 会把红绿灯按钮垂直居中。内容从标题栏下方开始。弹出层、工具提示和通知会显示在标题栏和内容之上，与其他 Arc 窗口相同。

### 在标题栏中放置控件

把窗口的 `TitleBar` 设为你自己的 `TitleBar`（在 XAML 中写在 `<arc:ArcWindow.TitleBar>` 内），并填充它的三个区域：

- `LeftContent` 位于图标和标题之后（菜单、导航）；
- `CenterContent` 在两侧放得下时居中于整个标题栏（搜索框、标签页）；
- `RightContent` 位于标题栏按钮之前（工具按钮、主题切换）。

区域中的控件自己处理点击；它们周围的空白部分仍可用来移动窗口。

<Demo name="Areas" />

```xml
<arc:TitleBar>
    <arc:TitleBar.LeftContent>
        <arc:Button Content="File" Variant="Ghost" Size="Small" />
    </arc:TitleBar.LeftContent>
    <arc:TitleBar.CenterContent>
        <arc:Input Placeholder="Search" Size="Small" Width="220" />
    </arc:TitleBar.CenterContent>
    <arc:TitleBar.RightContent>
        <arc:Button Variant="Ghost" Size="Small">
            <arc:ArcGlyph Data="{x:Static arc:ArcIcons.Settings}" Size="15" />
        </arc:Button>
    </arc:TitleBar.RightContent>
</arc:TitleBar>
```

窗口变窄时，右侧区域保持宽度，左侧区域先让出空间，中间区域向较宽的一侧移动，然后缩小到两侧之间的空隙。请给中间内容设置 `Width` 或 `MaxWidth`。

`Icon` 显示在标题之前，例如应用标志的 `Image` 或一个 `ArcGlyph`。`Title` 跟随窗口的 `Title`，除非你在标题栏上单独设置。

### 高度

标题栏高 40 px。在标题栏上设置 `Height` 可以改变高度，窗口会把它告知系统。

```xml
<arc:TitleBar Height="48" />
```

### 标题栏按钮

`CaptionButtons` 决定由谁绘制最小化、最大化和关闭按钮：

| 值 | 按钮 |
|---|---|
| `Auto`（默认） | 在 macOS 上为 `System`，其他系统为 `Drawn` |
| `Drawn` | 由标题栏在右端绘制。指针悬停时关闭按钮变红。 |
| `System` | 由系统绘制（macOS 红绿灯）；标题栏在左侧为它们留出位置 |
| `None` | 都不绘制 |

<Demo name="CaptionButtons" />

窗口最大化时，最大化按钮显示为还原。`CanResize` 或 `CanMaximize` 为 false 时最大化按钮禁用，`CanMinimize` 为 false 时最小化按钮禁用。

### 窗口置顶

在 `TitleBar` 上将 `ShowTopmostButton` 设为 `True`，标题栏按钮之前会出现一个图钉开关。窗口的 `Topmost` 为 true 时开关处于选中状态，切换开关会设置 `Topmost`，因此在代码中修改 `Topmost` 也会更新按钮。默认关闭，且在任何 `CaptionButtons` 取值下都会显示。

```xml
<arc:TitleBar ShowTopmostButton="True" />
```

### 不显示标题栏

把 `IsTitleBarVisible` 设为 `False`（或把 `TitleBar` 设为 null），内容会延伸到窗口顶部；窗口仍扩展到装饰区域，并可从边缘调整大小。此时需要由你的内容来移动窗口，例如调用 `BeginMoveDrag`。同时把 `ExtendClientAreaToDecorationsHint` 设为 `False` 可以恢复系统标题栏。

需要在自己的页头里放最小化、最大化和关闭按钮时，使用 `WindowCaptionButtons`，并把内容放进页头旁边的 `ArcOverlayHost`，让对话框和抽屉不会盖住页头（见“窗口标题栏按钮”）。以这种方式隐藏的标题栏不参与命中测试，它的按钮不会与你的按钮冲突。

### 窗口边框

Windows 11 由 DWM 在窗口的左、右、下边缘画 1px 边框，但客户区延伸到标题栏后不再画顶边。`ArcWindow` 在 Windows 上、窗口处于普通状态时（最大化、全屏或 `WindowDecorations="None"` 时不画）设置伪类 `:top-edge`，自己画出顶边（颜色 `Border`），四边保持一致。不需要这条线时用样式覆盖：

```xml
<arc:ArcWindow xmlns="https://github.com/avaloniaui"
               xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:ArcWindow.Styles>
        <Style Selector="arc|ArcWindow:top-edge /template/ Border#PART_Root">
            <Setter Property="BorderThickness" Value="0" />
        </Style>
    </arc:ArcWindow.Styles>
    <!-- ... -->
</arc:ArcWindow>
```

### 预览

只有作为 `ArcWindow` 的 `TitleBar` 时，`TitleBar` 才会控制窗口。放在其他位置的标题栏（例如本页的预览）外观相同，但不会移动、最大化或关闭任何窗口。

## API 参考

<!-- api: ArcWindow -->

<!-- api: TitleBar -->

<!-- api: TitleBarCaptionButtons -->

## 无障碍

- 标题栏按钮带有工具提示和无障碍名称（最小化、最大化或还原、关闭），使用应用当前的语言。
- 与系统标题栏一样，它们不在 Tab 顺序中。键盘可以通过系统使用相同的命令（Windows 上 Alt+Space 打开系统菜单，Alt+F4 关闭窗口）。
- 其他窗口处于活动状态时，标题变暗。
- 置顶开关带有工具提示和无障碍名称（置顶、取消置顶），在 Tab 顺序中，可用空格或回车切换。

## 动效

- 指针悬停时，标题栏按钮的填充色和图标颜色渐变。
- 减弱动效或关闭动效时，颜色立即变化。
- 最大化、还原和贴靠使用系统自己的动画。

## 响应式

- `ArcWindow` 是桌面窗口，没有断点。它的 `MinWidth` 为 360 px（来自 ArcWindowToken），是窗口能够缩小到的最窄宽度，此时标题与窗口按钮仍能并排放下。
- 标题栏宽度不足时，右侧包含窗口按钮的区域保持宽度，左侧区域先让位，中间区域先朝较宽的一侧移动再收缩。请为中间内容设置 `Width` 或 `MaxWidth`；标题以省略号截断。
- 窗口按钮为 30 px（`CaptionButtonSize`），不要把它们当作触控目标依赖。
- 手机与浏览器没有窗口：应在那里以单一视图呈现内容，`ArcWindow` 留给桌面应用。

## 相关组件

- `Dialog`、`Drawer` 和 `BottomSheet`：在窗口之上打开的内容。
- `WindowCaptionButtons`：内容延伸到顶部的窗口所用的最小化、最大化和关闭按钮。
- `DropdownMenu`：标题栏左侧区域的菜单。
- `Tabs`：标题栏中间区域的标签页。
