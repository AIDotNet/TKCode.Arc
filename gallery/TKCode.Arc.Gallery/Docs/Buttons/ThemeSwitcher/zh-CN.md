---
title: 主题切换
summary: 在亮色与暗色外观之间切换的四种平滑方式。
source-hash: 07f56514c77f
---

<Demo name="Variants" />

## 何时使用

主题切换是产品外层的一个控件，位于页头、工具栏或桌面工具的顶栏中。用户按一下，整个窗口就在亮色与暗色之间切换。`ThemeSwitcher` 会为辅助技术给出下一种外观的名称，显示当前外观的图标，并播放与布局形状相配的页面过渡。当外观的变化应当显得有意为之、而不是突兀时，就使用它。

- **管理后台页头中的切换**：切换控件位于工作区菜单与通知铃铛旁边。仅图标的形式让页头保持简洁，并且控件在每个页面上都位于同一个角落。
- **长时间分析会话中在亮色与暗色之间切换**：财务团队在一天快结束时复查营收仪表板，并把窗口切换为暗色。`Rise` 变体最安静，因此图表颜色平稳过渡，而不会在数据上扫过一道干扰的光。
- **在演示或发布页面预览外观**：设计团队展示新产品版本时使用 `Eclipse`，其圆盘横穿页面，让观众看到变化的发生。
- **居中顶栏的文档站或营销首页**：居中布局没有固定的角落可作为变化的起点。`Split` 从中线接缝展开，不依赖按钮的位置。
- **按工作区记住外观的桌面工具**：内部 CRM 或开发者工具把选择保存在设置中，并应用到每个窗口。处理程序接收请求，因此由应用决定如何应用。

## 何时不用

- **在亮色、暗色和跟随系统之间做选择，或选择项位于账户菜单中**：使用 `UserMenu`。菜单可以把这三个选项收在菜单里，而不占用每天都需要的页头空间。
- **需要在设置页中持续可见的选择**：使用 `SegmentedControl`，把亮色、暗色和跟随系统作为保持选中的具名选项；每个选项都需要说明时使用 `RadioCards`。
- **不改变主题的普通操作**，例如导出报表或邀请成员：使用 `Button`。
- **单一的开或关设置**，例如紧凑侧边栏或在图表上显示数值：使用 `Switch`，它表示一项设置，而不是整个窗口的变化。
- **其他具名的选择，例如语言、密度或强调色**：使用 `Select` 或 `SegmentedControl`。`ThemeSwitcher` 只在亮色与暗色之间切换。
- **不是主题的两种图标状态**，例如播放与暂停、菜单与关闭：使用 `IconMorph`，即双状态图标按钮。

## 用法

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:ThemeSwitcher Variant="Reveal" />
</UserControl>
```

`ThemeSwitcher` 派生自 Avalonia 的 `TemplatedControl`，其模板中包含一个小尺寸、次要变体的 Arc `Button`。因此按下缩放、悬停与焦点环都来自 Arc 按钮主题，与任何其他 Arc 按钮一致。切换控件在此之上加入外观逻辑：它跟踪窗口的主题变体，切换太阳与月亮图标，在主题变化前发出请求，并把按钮作为切换按钮暴露给辅助技术。未设置 `Appearance` 时，它跟随窗口实际的主题变体。

### 结构

1. **按钮外框**（内部的 Arc 按钮）：小尺寸的次要按钮，高 36 px。文字形式下，悬停会把边框加深为强边框颜色。仅图标的形式是 38 × 38 px 的方形，圆角 12 px，没有内边距；悬停时会移除阴影，因此方块只以表面颜色的变化作出反应。
2. **图标**（图标部分）：亮色外观显示太阳，暗色外观显示月亮。图标为 16 px，描边 1.75 px，绘制在 18 px 的图标区域内，该区域位于 20 px 的方形中，并带有变体对应的图标位移。
3. **间距**（`IconTextGap`）：图标与文字之间 8 px。
4. **文字**（`Text`）：可见的标签。默认为“切换主题”，采用按钮的文字颜色；当 `IsIconOnly` 为 `True` 时隐藏。

切换控件自身靠左对齐、垂直居中，因此无需额外布局即可放在工具栏的起始位置。把它放进 `StackPanel` 或 `DockPanel`，由父级容器决定位置即可。

### 变体

`Variant` 选择页面过渡方式。同一个值也决定暗色模式下图标位移的方向，因此图标与揭示效果朝同一方向移动。过渡只在切换控件自行应用变化时播放；变体本身并不改变外观。

| 变体 | 新外观 | 适用场景 |
|---|---|---|
| `Reveal`（默认） | 从按钮处以圆形向窗口最远的角落扩散 | 切换位于角落，因此揭示从用户按下的位置开始 |
| `Eclipse` | 一个与窗口对角线等宽的圆盘从右侧进入，并覆盖在页面上 | 展示页、演示与发布页，变化本身就是重点 |
| `Split` | 中线处的细缝扩展为整个窗口 | 居中布局，此时按钮的位置不提供方向感 |
| `Rise` | 从底部边缘升起，直到顶部 | 信息密集的工作界面，此时最安静的变化最合适；它是仅图标页头的选择 |

```xml
<arc:ThemeSwitcher Variant="Eclipse" />
<arc:ThemeSwitcher Variant="Split" />
<arc:ThemeSwitcher Variant="Rise" IsIconOnly="True" />
```

每个产品只选定一种变体，并在每个页面上保持一致。在页头中学会揭示效果的用户，在其他地方使用切换时也应看到相同的动效。当切换位于角落、揭示可以从那里开始时，使用 `Reveal`；当切换居中时，选择 `Split`，因为从居中栏的中间揭示会显得随意。

### 仅图标与文字

`IsIconOnly` 显示 38 × 38 的方形按钮，并隐藏 `Text`。适用于图标已足够的紧凑工具栏或页头，且周围的控件也都是图标。当切换与带文字的操作并列，或用户可能看不出太阳和月亮是主题控件时，使用文字形式，即设置 `Text="外观"` 或保留默认值。

```xml
<arc:ThemeSwitcher Text="外观" Label="切换外观" />
```

`Label` 设置无障碍名称。未设置 `Label` 时，切换控件会自行说明下一种外观（亮色时为“切换到暗色模式”，暗色时为“切换到亮色模式”）。固定的 `Label` 会在两种外观中替换这一名称，因此它不再说明下一次按下会切换到哪种外观。只有当名称不应改变时才设置 `Label`，并保持简短、具体。

`Text` 从不被用作无障碍名称。切换控件会自行设置按钮的名称，因此可见文字是给看得见控件的用户看的，而不是给辅助技术读取的名称。

### 处理切换请求

每次按下以及调用 `Toggle()` 时，都会在主题变化之前触发 `ThemeChangeRequested`。其参数给出要切换到的外观（`Next`）、变体（`Variant`）、被按下的按钮（`Trigger`），以及该按钮中心在其顶层坐标系中的位置（`Origin`），即揭示效果的起点。设置 `Handled` 后由你自行应用主题；切换控件随即不再处理主题，也不会修改 `Appearance`：若你的处理程序没有设置它，图标与名称都保持原样。

<Demo name="HandledByApp" />

```csharp
switcher.ThemeChangeRequested += (_, e) =>
{
    e.Handled = true;
    switcher.Appearance = e.Next;
    ApplyAppearance(e.Next);
};
```

当应用自行掌握外观时（例如选择按用户保存、应用到多个窗口，或在服务器调用之后才应用），处理这一请求。可以在切换控件或其父级上处理它：事件会冒泡，因此窗口上的一个处理程序即可服务其中的每个切换控件。当处理程序需要与内置应用相同的页面过渡时，把触发按钮、变体以及你的应用步骤传给 `RunTransitionAsync`：

```csharp
switcher.ThemeChangeRequested += async (_, e) =>
{
    e.Handled = true;
    await ThemeSwitcher.RunTransitionAsync(e.Trigger, e.Variant,
        () => _viewModel.ApplyAsync(e.Next));
};
```

`RunTransitionAsync` 会为窗口截取快照，在其下方执行你的应用步骤，并按变体揭示新外观。在减弱动效、关闭动效，或触发按钮没有覆盖层时，它会直接执行应用步骤并返回，不播放过渡。

### 受控外观

`Appearance` 是当前显示的外观，决定图标、按下状态与名称。它是可为空的 `ThemeSwitcherAppearance`，取值为 `Light` 与 `Dark`，默认是双向绑定。在代码中设置它，可使切换控件与应用保持同步。`EffectiveAppearance` 返回实际生效的外观，无论它来自 `Appearance` 还是来自窗口。

- 当 `Appearance` 为 `null`（默认值）时，切换控件跟随窗口实际的主题变体，并在其变化时更新。
- 当 `Appearance` 有值时，切换控件显示该值，并停止跟随窗口。将其设回 `null` 即可恢复跟随。
- 设置 `Appearance` 会改变图标与名称，但不会播放页面过渡。过渡只在切换控件自行应用变化时运行，即由按下或 `Toggle()` 触发。

```xml
<arc:ThemeSwitcher Appearance="Light" Variant="Rise" />
```

```csharp
switcher.Toggle();
```

`Toggle()` 的行为与按下一样：切换到另一种外观，引发 `ThemeChangeRequested`，并且除非请求已被处理，否则应用此次变化。可将它用于应用中的键盘快捷键，例如命令面板中的一个命令。

### 绑定到视图模型

在 MVVM 应用中，视图模型掌握外观与应用步骤；切换控件只显示该值并报告请求。下面的视图模型使用 CommunityToolkit.Mvvm 与应用端的设置存储。`ApplyAsync` 是应用自己的代码，负责改变应用的主题。

```csharp
public partial class AppearanceViewModel : ObservableObject
{
    private readonly IAppearanceStore _store;
    private readonly IThemeService _themes;

    [ObservableProperty]
    private ThemeSwitcherAppearance? _appearance;

    public AppearanceViewModel(IAppearanceStore store, IThemeService themes)
    {
        _store = store;
        _themes = themes;
        _appearance = store.Load();
    }

    public async Task ApplyAsync(ThemeSwitcherAppearance next)
    {
        await _themes.ApplyAsync(next == ThemeSwitcherAppearance.Dark);
        Appearance = next;
        await _store.SaveAsync(next);
    }
}
```

```xml
<arc:ThemeSwitcher x:Name="ThemeSwitch"
                   Appearance="{Binding Appearance}"
                   Variant="Rise" />
```

```csharp
public partial class ShellView : UserControl
{
    public ShellView()
    {
        InitializeComponent();
        ThemeSwitch.ThemeChangeRequested += OnThemeChangeRequested;
    }

    private async void OnThemeChangeRequested(object? sender, ThemeSwitcherChangeRequestedEventArgs e)
    {
        e.Handled = true;
        await ThemeSwitcher.RunTransitionAsync(e.Trigger, e.Variant,
            () => _viewModel.ApplyAsync(e.Next));
    }
}
```

- `Appearance` 是双向的：外观变化时，切换控件把其值写入视图模型；应用设置它时（例如启动时加载已保存的选择），视图模型的值会传到切换控件。
- 切换控件没有命令。请求通过事件发出，因为事件携带了过渡所需的触发按钮与起点。如上所示在代码后置中连接事件，并让处理程序调用视图模型。
- 编译绑定需要在视图上设置 `x:DataType`（为简洁起见，这里的示例省略了它）。

### 持久化选择

在第一帧之前或之后立即加载已保存的值，并将其赋给视图模型。首次设置外观时不产生动画，并且在最初几帧内到达的值也不会播放图标切换，因此切换控件会以正确的状态开始，不会闪烁。在 `ApplyAsync` 中于主题变化之后再保存该值，这样应用失败时，就不会保存一个窗口从未显示过的外观。

### 克制地使用动效

页面过渡适用于整个窗口都发生的变化。它让用户看到主题在各处都已改变，而快速的重新着色可能会被忽略。不要把它用于经常发生的变化：一分钟内多次切换主题的用户，使用安静的 `Rise` 比每次都出现日食效果更合适。过渡会按渲染比例捕获整个窗口，因此其开销随窗口尺寸和显示密度增长。在大窗口中保持变体安静。

减弱动效会移除页面过渡与图标位移，图标立即淡入。关闭动效则直接切换主题，没有任何这些效果。

### 示例：分析控制台页头

运维控制台的页头，包含产品名称与工作区、一个用于导出的幽灵按钮，以及末端一个仅图标的切换控件。由于切换控件没有设置 `Appearance`，它跟随窗口的主题；页头本身不决定外观，由应用决定。仅图标的 `Rise` 变体让页头保持安静，导出按钮采用幽灵样式，因此不会与下方的内容争夺注意力。切换位于行的末端，这也是用户寻找窗口级控件的位置。

![控制台页头，包含导出幽灵按钮与仅图标的主题切换](images/ConsoleHeader.png)

```xml
<Border Width="640"
        Padding="16,12"
        CornerRadius="12"
        Background="{arc:ArcDesignTokenResource Surface}"
        BorderBrush="{arc:ArcDesignTokenResource BorderSubtle}"
        BorderThickness="1">
    <DockPanel LastChildFill="False">
        <StackPanel DockPanel.Dock="Left" Spacing="2">
            <TextBlock Text="Acme Analytics"
                       FontSize="15"
                       FontWeight="SemiBold" />
            <TextBlock Text="Workspace: Northwind EMEA"
                       FontSize="12"
                       Foreground="{arc:ArcDesignTokenResource TextMuted}" />
        </StackPanel>
        <StackPanel DockPanel.Dock="Right"
                    Orientation="Horizontal"
                    Spacing="8"
                    VerticalAlignment="Center">
            <arc:Button Content="Export" Variant="Ghost" />
            <arc:ThemeSwitcher Variant="Rise" IsIconOnly="True" />
        </StackPanel>
    </DockPanel>
</Border>
```

### 文案规范

- **默认名称与文字**：页头中保留默认文字“切换主题”，以及说明下一种外观的默认无障碍名称。当切换位于一排带标签的设置中时，用一个简短的名词覆盖文字，例如“外观”。
- **标签**：设置时，应说明动作及其切换到的外观，例如“切换到暗色模式”。省去屏幕阅读器已经会播报的词，例如“开关”“按钮”。
- **文字**：句子式大小写的动词加宾语或名词短语，不加标点。文字中不要写出当前状态，因为图标已经表明了。
- **仅图标**：只在周围的控件同样是图标时使用仅图标形式。在带标签的按钮中间单独放一个太阳与月亮，很容易被忽略。

| 不要写 | 改为 |
|---|---|
| 深色 | 切换到暗色模式 |
| 更改外观 | 切换外观 |
| 主题切换（点击） | 切换主题 |
| 应用的夜间模式 | 切换到暗色模式 |
| 模式 | 切换到亮色模式 |

### 最佳实践

**推荐**

- 在每个页面的页头相同位置放置切换控件。找到它一次的用户会预期它总在同一位置，因此切换控件始终容易被发现。
- 当应用需要保存选择、把它应用到多个窗口，或在调用服务器之后应用时，处理 `ThemeChangeRequested`。处理程序是唯一能同时看到下一种外观、变体与起点的地方。
- 整个产品只保留一种变体。只有重复出现时，动效才会成为产品的标志。
- 除非名称必须固定，否则不要设置 `Label`。默认名称会告诉屏幕阅读器下一次按下会切换到哪种外观，而固定名称做不到。

**避免**

- 在信息密集的仪表板中使用 `Eclipse` 或 `Reveal`。整窗扫过的效果会与图表重绘争夺注意力；更安静的 `Rise` 能把注意力留给数据。
- 在代码中设置 `Appearance` 来伪造主题切换。设置它只会移动图标与名称，窗口仍保持旧的外观。应当先在处理程序中应用主题，再设置该值。
- 在两种状态下都使用固定的 `Label`，例如“切换外观”。名称将不再告诉用户下一次按下会切换到哪种外观。
- 页头有空间时，把切换控件放进与系统选项并列的菜单中。菜单会把切换控件藏起来，而三选一的选择应当放在 `UserMenu` 中。
- 在代码中循环按下切换，例如自动化测试在每一步都翻转主题。每次按下都会捕获整个窗口并运行过渡，因此快速连续的操作每次都要付出一次快照的代价。

### 常见问题

**为什么图标变了，窗口却没有变？** 设置 `Appearance` 只会移动图标、按下状态与名称。当切换控件或你的处理程序应用主题时，主题才会改变。如果你处理了请求，请像“处理切换请求”中所示，在处理程序里应用主题。

**设置 `Appearance` 后，为什么图标不再跟随窗口？** `Appearance` 中的值会覆盖窗口的主题变体。将 `Appearance` 设回 `null` 即可再次跟随窗口。

**启动时加载已保存的偏好，会播放过渡吗？** 不会。设置 `Appearance` 从不播放页面过渡。图标切换只在切换控件加载并稳定之后运行，因此启动时设置的值会直接显示，不产生切换动画。

**内置应用会保留我的强调色和其他主题设置吗？** 会。切换控件会沿用它最近观察到的主题请求的配置，并且只切换暗色算法。在任何请求之前，它会先沿用当前的 Arc 强调色预设，因此切换外观时强调色不会重置。

**内置应用能使用我自己的过渡吗？** 可以。处理请求，并以触发按钮、变体和你的应用步骤调用 `RunTransitionAsync`，如“处理切换请求”中所示。该辅助方法会运行变体指定的过渡，在减弱动效下则退回为直接应用。

## API 参考

<!-- api: ThemeSwitcher -->

**`ThemeSwitcherChangeRequestedEventArgs`** 是 `ThemeChangeRequested` 的参数。

| 成员 | 类型 | 含义 |
|---|---|---|
| `Next` | `ThemeSwitcherAppearance` | 要切换到的外观 |
| `Variant` | `ThemeSwitcherVariant` | 切换控件请求的页面过渡 |
| `Trigger` | `Control` | 被按下的按钮 |
| `Origin` | `Point` | 触发按钮中心在其顶层坐标中的位置，即揭示效果的起点 |
| `Handled` | `bool` | 设为 `True` 即表示由你自行应用主题；继承自 `RoutedEventArgs` |

**`ThemeSwitcherVariant`** 与 **`ThemeSwitcherAppearance`** 是切换控件使用的两个枚举。

| 枚举 | 成员 |
|---|---|
| `ThemeSwitcherVariant` | `Reveal`（默认）、`Eclipse`、`Split`、`Rise` |
| `ThemeSwitcherAppearance` | `Light`、`Dark` |

## 无障碍

- 切换控件是一个切换按钮。处于暗色外观时它报告为开启，处于亮色外观时为关闭；外观每次变化都会为辅助技术引发一次切换状态变更。
- 除非设置了 `Label`，其无障碍名称会说明下一种外观（“切换到暗色模式”或“切换到亮色模式”）。外观变化时名称随之更新。
- 太阳与月亮图标对辅助技术隐藏。名称承载的是含义，而不是图标。
- 切换控件与其他 Arc 按钮一样可以获得键盘焦点，并显示标准的焦点环。Enter 与 Space 都可以切换外观。
- 减弱动效时，主题立即切换，不播放页面过渡，也不做图标位移。

### 键盘

| 按键 | 操作 |
|---|---|
| `Enter` 或 `Space` | 切换到另一种外观；先触发 `ThemeChangeRequested` |

## 动效

- 太阳与月亮沿共享路径相互转动、缩放，旧图标迅速离开。两个图标朝同一方向转动，因此切换读起来是一次旋转，而不是两个分开的图标。
- 暗色模式下图标会朝其变体的方向移动：`Reveal` 将其放大到 108%，`Eclipse` 向左移动 2 px，`Split` 向右移动 1 px，`Rise` 向上移动 2 px。
- 页面过渡按变体揭示新外观。窗口作为快照保留在覆盖层中，新外观在其下方应用，快照的裁剪区域随之打开以显示新外观。
- 首次设置外观时不产生动画。图标切换只在切换控件加载并稳定之后开始。

| 变化 | 完整 | 减弱 | 关闭 |
|---|---|---|---|
| 主题变化 | 按变体播放页面过渡 | 无过渡；主题立即切换 | 立即完成 |
| 太阳与月亮切换 | 图标相互转动、缩放 | 图标立即淡入 | 立即完成 |
| 暗色模式图标位移 | 图标按其变体移动 | 无位移 | 立即完成 |

## 相关组件

- `Button`：不改变主题的普通操作，也是切换控件所基于的按钮。
- `UserMenu`：包含跟随系统选项、位于账户菜单中的主题选择。
- `SegmentedControl`：在几个具名选项之间选择，并保持选中状态。
- `IconMorph`：在两种状态之间变形的独立图标。

| 组件 | 适用场景 |
|---|---|
| `Button` | 操作不改变主题 |
| `UserMenu` | 主题选择包含跟随系统，或位于账户菜单中 |
| `SegmentedControl` | 亮色、暗色与跟随系统是设置页中可见的具名选项 |
| `RadioCards` | 每个外观选项在设置页中都需要自己的说明 |
| `Switch` | 单一的开或关设置，且不改变整个窗口 |
| `IconMorph` | 不是主题的双状态图标 |
