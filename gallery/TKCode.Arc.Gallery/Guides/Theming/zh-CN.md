---
title: 主题
source-hash: 65d9e8b78c7c
---

# 主题

TKCode.Arc 中的每一种颜色、圆角、阴影、字体大小和动效时长，都来自同一套设计令牌，即 ArcDesign 令牌。控件从不硬编码数值，而是读取令牌。因此，切换外观（浅色或深色）、强调色或某一个令牌，都会一次性重绘整个应用，并让所有控件保持一致。

本页说明主题如何构建、如何在运行时切换、有哪些令牌，以及如何在你自己的视图和控件中使用它们。

## 主题的构建方式

主题运行时属于 `TKCode.Arc.Foundation`，由 `TKCode.Arc` 一并引入：

1. **主题管理器**（`ArcThemeManager`，每个应用一个，由 `UseArc(...)` 创建）持有当前主题，并把它的令牌发布到应用的资源中。
2. **主题状态**（`ArcThemeState`）是计算主题所需的全部输入：外观（`Light`、`Dark` 或 `System`）、强调色种子、动效级别、主题预设和令牌覆盖。它不可变；`ArcThemeBuilder` 描述一次更改，更改中没有提到的部分保持原样。
3. **ArcDesign 标识**持有 Arc 自己的令牌：颜色、圆角、字体、间距和动效。
4. **控件令牌**持有单个控件的几何参数（内边距、宽度、偏移量），以及不依赖强调色的颜色。

应用主题时，每个令牌都会根据外观和强调色重新计算，只写入发生变化的值，读取令牌的每个控件都会原地更新：无需重新加载页面，也无需重启。

## 浅色与深色

在 `UseArc(...)` 中设置应用启动时的主题，之后用 `ArcTheme.Apply` 更改：

```csharp
using TKCode.Arc;

// Program.cs：第一帧就使用这个主题绘制。
.UseArc(arc => arc
    .UseArcControls()
    .WithTheme(t => t.Appearance(ArcAppearance.System).Accent(ArcAccentPreset.Violet)))

// 运行时：只改变外观，强调色和其他设置保持不变。
public static void ApplyAppearance(bool dark)
    => ArcTheme.Apply(t => t.Appearance(dark ? ArcAppearance.Dark : ArcAppearance.Light));
```

`ArcAppearance.System` 跟随操作系统的浅色或深色设置，并随之切换。`ArcTheme.State` 是当前状态，`ArcThemeManager.Current` 是管理器：它的 `IsDark` 表示当前显示的是哪种外观，每次更改后都会引发 `StateChanged`。`ArcThemeManager.ApplyAsync` 在后台线程计算令牌，在 UI 线程发布；请 await 它，切勿在 UI 线程上阻塞等待它（`.Wait()`、`.Result`），否则会死锁。如果一次更改中有未知的令牌或无效的值，会抛出 `ArcThemeException` 并列出全部问题，且不做任何更改。

将用户的选择（浅色、深色或跟随系统）保存在设置存储中，并在启动时传给 `WithTheme`，使第一帧就具有正确的外观。图库的外观面板正是这样工作的。

### ThemeSwitcher 控件

对于页头或工具栏，`ThemeSwitcher` 是现成的浅色/深色切换控件，带有 Arc 的页面过渡效果（通过 `Variant` 设置 `Reveal`、`Eclipse`、`Split` 和 `Rise`）。按下时会触发 `ThemeChangeRequested`。如果没有处理程序将事件标记为已处理，切换器会自行通过 `ArcThemeManager` 更改外观，并保留当前主题的其余部分。当应用程序自己掌管外观时（例如需要保存用户的选择），请处理该事件：

```csharp
themeSwitcher.ThemeChangeRequested += async (_, e) =>
{
    e.Handled = true;
    await settings.SaveAppearanceAsync(e.Next);
    ApplyAppearance(e.Next == ThemeSwitcherAppearance.Dark);
};
```

过渡效果绘制在窗口的覆盖层上。如果窗口模板中没有 `EnableOverlayLayer="True"` 的 `VisualLayerManager`，切换时就没有过渡。

## 强调色

Arc 提供八种强调色预设。强调色驱动按钮、选中状态、焦点环、开关、滑块、图表系列和链接；其他部分保持中性，因此更换强调色只会改变品牌色，不会改变布局或文字的对比度。

![八种强调色预设的浅色与深色色调](images/AccentPresets.png)

```xml
<StackPanel Width="720" Spacing="10">
    <Grid ColumnDefinitions="120,*,*">
        <TextBlock Grid.Column="1" Text="Light" Foreground="{arc:ArcDesignTokenResource TextMuted}" />
        <TextBlock Grid.Column="2" Text="Dark" Foreground="{arc:ArcDesignTokenResource TextMuted}" />
    </Grid>
    <Grid ColumnDefinitions="120,*,*">
        <TextBlock Text="Neutral" VerticalAlignment="Center" Foreground="{arc:ArcDesignTokenResource Foreground}" />
        <Border Grid.Column="1" Height="32" CornerRadius="16" Margin="0,0,8,0" Background="#353535" />
        <Border Grid.Column="2" Height="32" CornerRadius="16" Background="#BEBEBE" />
    </Grid>
    <Grid ColumnDefinitions="120,*,*">
        <TextBlock Text="Violet" VerticalAlignment="Center" Foreground="{arc:ArcDesignTokenResource Foreground}" />
        <Border Grid.Column="1" Height="32" CornerRadius="16" Margin="0,0,8,0" Background="#7747FF" />
        <Border Grid.Column="2" Height="32" CornerRadius="16" Background="#A78BFF" />
    </Grid>
    <Grid ColumnDefinitions="120,*,*">
        <TextBlock Text="Blue" VerticalAlignment="Center" Foreground="{arc:ArcDesignTokenResource Foreground}" />
        <Border Grid.Column="1" Height="32" CornerRadius="16" Margin="0,0,8,0" Background="#0562EF" />
        <Border Grid.Column="2" Height="32" CornerRadius="16" Background="#6BA3FF" />
    </Grid>
    <Grid ColumnDefinitions="120,*,*">
        <TextBlock Text="Green" VerticalAlignment="Center" Foreground="{arc:ArcDesignTokenResource Foreground}" />
        <Border Grid.Column="1" Height="32" CornerRadius="16" Margin="0,0,8,0" Background="#0DB879" />
        <Border Grid.Column="2" Height="32" CornerRadius="16" Background="#53DCA6" />
    </Grid>
    <Grid ColumnDefinitions="120,*,*">
        <TextBlock Text="Amber" VerticalAlignment="Center" Foreground="{arc:ArcDesignTokenResource Foreground}" />
        <Border Grid.Column="1" Height="32" CornerRadius="16" Margin="0,0,8,0" Background="#F3AD20" />
        <Border Grid.Column="2" Height="32" CornerRadius="16" Background="#FFCB62" />
    </Grid>
    <Grid ColumnDefinitions="120,*,*">
        <TextBlock Text="Orange" VerticalAlignment="Center" Foreground="{arc:ArcDesignTokenResource Foreground}" />
        <Border Grid.Column="1" Height="32" CornerRadius="16" Margin="0,0,8,0" Background="#F48120" />
        <Border Grid.Column="2" Height="32" CornerRadius="16" Background="#FFAD5E" />
    </Grid>
    <Grid ColumnDefinitions="120,*,*">
        <TextBlock Text="Coral" VerticalAlignment="Center" Foreground="{arc:ArcDesignTokenResource Foreground}" />
        <Border Grid.Column="1" Height="32" CornerRadius="16" Margin="0,0,8,0" Background="#F15F55" />
        <Border Grid.Column="2" Height="32" CornerRadius="16" Background="#FF9386" />
    </Grid>
    <Grid ColumnDefinitions="120,*,*">
        <TextBlock Text="Rose" VerticalAlignment="Center" Foreground="{arc:ArcDesignTokenResource Foreground}" />
        <Border Grid.Column="1" Height="32" CornerRadius="16" Margin="0,0,8,0" Background="#ED4E9D" />
        <Border Grid.Column="2" Height="32" CornerRadius="16" Background="#FF8BC5" />
    </Grid>
</StackPanel>
```

| 预设 | 种子色 | 强调色，浅色 | 强调色，深色 | 较深强调色，浅色 |
|---|---|---|---|---|
| `Neutral`（默认） | `#353535` | `#353535` | `#BEBEBE` | `#1F1F1F` |
| `Violet` | `#7747FF` | `#7747FF` | `#A78BFF` | `#5528CE` |
| `Blue` | `#0562EF` | `#0562EF` | `#6BA3FF` | `#074DB7` |
| `Green` | `#0DB879` | `#0DB879` | `#53DCA6` | `#087C54` |
| `Amber` | `#F3AD20` | `#F3AD20` | `#FFCB62` | `#9C6300` |
| `Orange` | `#F48120` | `#F48120` | `#FFAD5E` | `#AA4700` |
| `Coral` | `#F15F55` | `#F15F55` | `#FF9386` | `#B92C27` |
| `Rose` | `#ED4E9D` | `#ED4E9D` | `#FF8BC5` | `#AE2570` |

每个预设都为两种外观提供了手工调校的值：强调色、用于文字和按下状态的较深色阶、用于选中背景的淡色调、强调色上的文字颜色，以及开关和复选框的颜色。

### 品牌色

当所有预设都不符合品牌时，直接传入品牌色本身。Arc 会在 OKLCH 色彩空间中由它推导出浅色与深色的强调色系，使各个色阶在视觉上保持均匀：

```csharp
ArcTheme.Apply(t => t.Accent(Color.Parse("#0E7C86")));
```

请在两种外观下检查结果。非常浅的种子色（黄色、青柠色）在白色背景上作为文字颜色效果很差；这类情况下请优先使用预设，或使用品牌色的较深色调。

## 设计令牌

令牌按其描述的内容分组。下面的名称是 `ArcDesignTokenKind` 的成员，XAML 中也以相同名称引用。

![根据当前外观的令牌读取的表面、文字层级、状态颜色与圆角](images/TokenPreview.png)

```xml
<StackPanel Width="720" Spacing="16">
    <UniformGrid Columns="4">
        <Border Height="84" Margin="0,0,8,0" Padding="12" CornerRadius="{arc:ArcDesignTokenResource RadiusControl}"
                Background="{arc:ArcDesignTokenResource Background}"
                BorderBrush="{arc:ArcDesignTokenResource Border}" BorderThickness="1">
            <TextBlock Text="Background" Foreground="{arc:ArcDesignTokenResource TextSecondary}" />
        </Border>
        <Border Height="84" Margin="4,0" Padding="12" CornerRadius="{arc:ArcDesignTokenResource RadiusControl}"
                Background="{arc:ArcDesignTokenResource Surface}"
                BorderBrush="{arc:ArcDesignTokenResource Border}" BorderThickness="1">
            <TextBlock Text="Surface" Foreground="{arc:ArcDesignTokenResource TextSecondary}" />
        </Border>
        <Border Height="84" Margin="4,0" Padding="12" CornerRadius="{arc:ArcDesignTokenResource RadiusControl}"
                Background="{arc:ArcDesignTokenResource SurfaceMuted}"
                BorderBrush="{arc:ArcDesignTokenResource Border}" BorderThickness="1">
            <TextBlock Text="SurfaceMuted" Foreground="{arc:ArcDesignTokenResource TextSecondary}" />
        </Border>
        <Border Height="84" Margin="8,0,0,0" Padding="12" CornerRadius="{arc:ArcDesignTokenResource RadiusControl}"
                Background="{arc:ArcDesignTokenResource SurfaceRaised}"
                BorderBrush="{arc:ArcDesignTokenResource Border}" BorderThickness="1">
            <TextBlock Text="SurfaceRaised" Foreground="{arc:ArcDesignTokenResource TextSecondary}" />
        </Border>
    </UniformGrid>
    <StackPanel Spacing="4">
        <TextBlock Text="Foreground: titles and primary text" Foreground="{arc:ArcDesignTokenResource Foreground}" FontSize="16" />
        <TextBlock Text="TextSecondary: descriptions and secondary values" Foreground="{arc:ArcDesignTokenResource TextSecondary}" />
        <TextBlock Text="TextMuted: hints, captions and placeholders" Foreground="{arc:ArcDesignTokenResource TextMuted}" />
    </StackPanel>
    <StackPanel Orientation="Horizontal" Spacing="8">
        <arc:Badge Content="Success" Tone="Success" />
        <arc:Badge Content="Warning" Tone="Warning" />
        <arc:Badge Content="Danger" Tone="Danger" />
        <arc:Button Content="Accent" />
        <arc:Button Content="Secondary" Variant="Secondary" />
    </StackPanel>
</StackPanel>
```

| 分组 | 令牌 | 取值与用途 |
|---|---|---|
| 表面 | `Background`、`Surface`、`SurfaceMuted`、`SurfaceRaised`、`Scrim` | 页面、卡片与字段、弱化的底色区域、抬起的菜单与弹出层，以及对话框背后的遮罩层。 |
| 文字 | `Foreground`、`TextSecondary`、`TextMuted` | 三级强调。`TextMuted` 只用于提示和说明文字，切勿用于读者必须看到的信息。 |
| 边框 | `Border`、`BorderStrong`、`BorderSubtle` | 卡片轮廓、字段外框与分隔线。 |
| 强调 | `Accent`、`AccentStrong`、`AccentSubtle`、`AccentForeground` | 品牌色、用于文字的较深色阶、用于选中背景的淡色调，以及强调色上的文字。 |
| 控件 | `ControlOn`、`ControlOnSubtle`、`ControlThumb`、`ControlThumbOn`、`ControlTrack`、`ControlTrackHover`、`ControlGlyph`、`ControlFill`、`ControlThumbShadow` | 开关、复选框、单选框和滑块。 |
| 状态 | `Success`、`Warning`、`Danger` 及其 `…Subtle` 淡色调 | 结果、警告和错误；淡色调用于徽章与警示的背景。 |
| 数据 | `Series1` 至 `Series4`、`Neutral0` 至 `Neutral11` | 跟随强调色的图表系列，以及用于图表与插图的中性色阶。 |
| 焦点 | `FocusRing`、`FocusRingWidth`（2）、`FocusRingOffset`（2） | 键盘焦点环。 |
| 形状 | `RadiusControl`（18）、`RadiusPanel`（26）、`RadiusSurface`（34）、`RadiusPill` | 字段与按钮、卡片与面板、大型表面，以及胶囊形和徽章。 |
| 尺寸 | `ControlSizeSM`（36）、`ControlSizeMD`（44）、`ControlSizeLG`（50）；`GlyphSizeSM`（16）、`GlyphSizeMD`（20）、`GlyphSizeLG`（24）；`IconStrokeWidth` | `Small`、`Medium` 与 `Large` 三种尺寸对应的控件高度，以及图标尺寸。 |
| 间距 | `Space1`（4）至 `Space24`（96） | 以 4 px 为基准的间距刻度：4、8、12、16、20、24、32、40、48、64、80、96。 |
| 字号 | `TypeXS`（12）、`TypeSM`（14）、`TypeBase`（16）、`TypeLG`（18）、`TypeXL`（22）、`Type2XL`（28）、`Type3XL`（36）、`Type4XL`（52）、`Type5XL`（72） | 字号刻度。 |
| 字体样式 | `BodyFontFamily`、`DisplayFontFamily`、`MonoFontFamily`、`WeightRegular`、`WeightMedium`、`LeadingBody`（1.4）、`LeadingDisplay`（1.1）、`TrackingBody`、`TrackingDisplay` | 正文与展示文字的字体族、字重、行高和字距。 |
| 层级 | `ShadowResting`、`ShadowRaised`、`ShadowFloating` | 静止的卡片、抬起的卡片与菜单、浮动的对话框与弹出框。深色模式使用更深的阴影。 |
| 效果 | `BlurSubtle`、`BlurSoft`、`BlurText`、`BackdropBlurDialog`、`BackdropBlurDrawer`、`ReduceTransparency` | 动态模糊的步进，以及对话框和抽屉的毛玻璃背景。 |
| 动效 | `Duration…`、`Ease…`、`Spring…`、`Stagger…`、`MotionMode` | 时长、曲线、弹簧预设与动效级别。参见 **动效**。 |

## 在视图中使用令牌

### 在 XAML 中

`ArcDesignTokenResource` 读取令牌，并跟随主题变化：

```xml
<Border Padding="16"
        Background="{arc:ArcDesignTokenResource Surface}"
        BorderBrush="{arc:ArcDesignTokenResource BorderSubtle}"
        BorderThickness="1"
        CornerRadius="{arc:ArcDesignTokenResource RadiusPanel}">
    <StackPanel Spacing="4">
        <TextBlock Text="本月用量"
                   FontSize="{arc:ArcDesignTokenResource TypeLG}"
                   Foreground="{arc:ArcDesignTokenResource Foreground}" />
        <TextBlock Text="已用 182 / 250 GB"
                   Foreground="{arc:ArcDesignTokenResource TextSecondary}" />
    </StackPanel>
</Border>
```

### 在代码中

`ArcTokens.Bind` 将属性绑定到令牌，使其随主题更新；`ArcTokens.Get` 和 `ArcTokens.GetColor` 读取当前值一次，例如在自定义的 `Render` 中绘制时使用：

```csharp
ArcTokens.Bind(card, Border.BackgroundProperty, ArcDesignTokenKind.Surface);
ArcTokens.Bind(title, TextBlock.ForegroundProperty, ArcDesignTokenKind.Foreground);

var accent = ArcTokens.GetColor(chart, ArcDesignTokenKind.Accent);
```

对于会持续显示在屏幕上的内容，优先使用 `Bind` 而不是 `Get`：一次读取的值不会在用户切换到深色模式时改变。

### 控件令牌

每个 Arc 控件都有自己的令牌类，用于读取其几何参数，在 XAML 中写作 `{arc:<Control>TokenResource Name}`。例如，所有文本字段都共用 `{arc:FieldTextBoxTokenResource SelectionFill}` 作为选中颜色。控件令牌保存的值不依赖强调色，因此很少需要修改；编写一个应当与 Arc 字段外观一致的控件时，请读取这些令牌，而不是复制数值。

## 更改令牌

`Token(name, value)` 覆盖某个 ArcDesign 令牌的最终值，`Token(identity, name, value)` 覆盖某个控件的令牌，`MotionLevel` 为整个主题设置动效级别：

```csharp
ArcTheme.Apply(t => t
    .Accent(ArcAccentPreset.Blue)
    .Token(nameof(ArcDesignTokenKind.Danger), "#D92D20")
    .Token("Switch", "TrackWidth", "50")
    .MotionLevel(ArcMotionLevel.Reduced));
```

值是文本，按令牌的类型解析（`#RRGGBB`、`12`、`4,8`、`120ms`、枚举成员名）。控件令牌的标识就是控件名；移动端控件使用 `MobileButton`、`MobileCheckbox`、`MobileDialog`、`MobileSwitch` 和 `MobileToast`。`ClearTokens()` 会移除全部覆盖。

尽量少改。令牌的修改会应用到所有地方：覆盖 `Danger` 会改变应用中每一条错误提示、每一个破坏性按钮和每一个失败状态。颜色的覆盖值应在两种外观下检查，因为深色模式不会重新计算该覆盖值。

## 圆角风格

Arc 的圆角偏大：控件 18、面板 26、表面 34。如果产品想要更方的外观，请改用圆角风格，而不是只覆盖四个 `Radius*`
令牌。这四个只是最上层：模板内部使用的圆角阶梯、约 300 个控件属性各自的圆角令牌（复选框的方框、Combobox 的选项、
悬停菜单行背后的高亮），以及某些控件在动画中自己计算的圆角，都是另外的值。只改那四个，弹层、悬停背景和复选框仍然是圆的。

`TKCode.Arc.Themes.Corners` 包用一次调用改掉全部，并且只改圆角：颜色、描边、阴影和动效保持不变。在 `UseArc(...)` 中用
`UseArcCorners()` 注册它，然后：

```csharp
using TKCode.Arc.Themes.Corners;

ArcTheme.Apply(t => t
    .Accent(ArcAccentPreset.Blue)
    .Corners(ArcCornerStyle.Square));
```

| 风格 | 结果 |
|---|---|
| `ArcCornerStyle.Default` | 保持 Arc 原样，不写入任何值。 |
| `ArcCornerStyle.Scale(0.5)` | 每个圆角乘以系数。`Scale(0)` 等于 `Square`，`Scale(1)` 等于 `Default`，大于 1 则更圆。 |
| `ArcCornerStyle.Square` | 所有圆角变为直角。 |

- **药丸与圆形。** 半径达到 999 及以上的（药丸按钮、圆形头像、圆点）在 `Scale` 下保持形状，只有 `Square` 会把它们变平，
  所以直角风格会把圆形头像变成方形。想保留圆形，请用 `Scale`。
- **预设。** 圆角风格是一个主题预设（`ArcThemePreset`）：一组带名字、分浅色与深色的令牌覆盖。`Corners(style)` 等于
  `Preset(ArcCornerPresets.Create(style))`；`ArcCornerPresets.Square` 和 `ArcCornerPresets.Half` 是现成的。预设覆盖所有
  Arc 包，并跳过应用没有注册的包。
- **替换。** 圆角风格占用一个槽位（`ArcCornerPresets.Slot`），所以下一次 `Corners(...)` 会替换它，
  `Corners(ArcCornerStyle.Default)` 会移除它。你自己的 `Token(...)` 覆盖在预设之后应用，优先生效。
- **滚动条。** 无论哪种圆角风格，滚动条都保持细长的胶囊形状。
- **Pixel。** `TKCode.Arc.Themes.Pixel` 包（`UseArcPixelTheme()`）提供 Pixel 风格：直角、硬阴影、浅色与深色各一套有限
  调色板以及像素字体。用 `ArcTheme.Apply(t => t.Pixel())` 应用，用 `t.RemovePreset(ArcPixelPresets.Slot)` 恢复。它有
  自己的槽位，所以在它之后选择的圆角风格会改变它的圆角。

## 单个控件的样式

如需局部调整，请使用 Avalonia 的常规工具，而不是令牌：

- **属性**：`Width`、`Margin`、`HorizontalAlignment` 以及各控件有文档的属性。
- **样式**：使用如 `arc|Button.toolbar` 之类选择器的 `Style`，可以更改某个范围内带有该类的每一个按钮。
- **控件主题**：以 Arc 主题为 `BasedOn` 的 `ControlTheme`，可以替换应用中某一类控件的模板，同时保留 Arc 的状态。

这些样式中的颜色应继续基于令牌（`{arc:ArcDesignTokenResource ...}`），以便它们仍然跟随外观变化。

## 指导原则

**推荐**

- 所有颜色都从令牌读取。视图中单独一个十六进制值就是深色模式下的缺陷。
- 只为屏幕上最重要的那一个操作或状态使用强调色，其余保持中性。
- 将状态颜色与文字或图标搭配使用。仅靠红色边框无法说明错误，而且有些读者分辨不出红色与绿色的差别。
- 发布前，请在浅色与深色下、至少使用两种强调色测试每个屏幕。

**避免**

- 将 `TextMuted` 用于读者必须阅读的信息：它的对比度在三级文字中最低，仅适用于简短提示和说明文字。
- 覆盖大量令牌，将库改造成另一套设计系统：控件的间距、圆角和动效是一起调校的。
- 在强调色背景上的文字硬编码 `#FFFFFF`；应使用 `AccentForeground`，它在 Amber 这类浅色强调色上为深色。
