# 组件实现指南

本文是动手实现组件时的操作手册：目录与文件、控件骨架、Token、主题、动效、Core 基元、性能、API 易用性、展示页、测试、构建，以及踩过的坑。
架构层面的"为什么"见 [architecture/overview.md](../architecture/overview.md)，这里只写"怎么做"。

## 1. 目录与文件

每个组件一个目录，按大类归档（大类目录名见 [components/overview.md](../components/overview.md)）：

```text
src/TKCode.Arc/<Category>/<Component>/
  <Component>.cs              公开控件（namespace TKCode.Arc）
  <Component>Token.cs         [ControlDesignToken] internal sealed class <Component>Token
  <Component>*.cs             内部部件（internal），如 CalendarDayCell、CalendarMonthStrip
  Themes/<Component>Theme.axaml
gallery/TKCode.Arc.Gallery/Pages/<Category>/<Component>Page.cs
gallery/TKCode.Arc.Gallery/Pages/<Category>/<Category>Pages.X.cs   本团队的页面注册（X = A / B / C，见第 10 节）
tests/TKCode.Arc.Tests/<Category>/<Component>Tests.cs
```

- 命名空间一律 `TKCode.Arc`（扁平）；Core 的公开基础类型在 `TKCode.Arc.Motion` / `TKCode.Arc.Primitives`，XAML 前缀都是 `arc`（`xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc"`）。
- 类名取 Arc 标题的 PascalCase，不加 Arc 前缀。与 Avalonia / AtomUI 同名时在 C# 里用别名引用对方，例如 `using AvaloniaButton = Avalonia.Controls.Button;`。
- 一个大类内被多个组件共享的内部代码，放在该大类目录下的 `_Shared/`（internal）。跨大类复用的放 Core，见第 6 节。

## 2. 控件骨架

- 基类：交互控件优先派生 Avalonia 原生控件（`Button`、`ToggleButton`、`TextBox`、`ItemsControl`、`SelectingItemsControl`、`ContentControl`），获得键盘、命令、自动化行为；纯展示且大量重复的部件派生 `Control` 并自己 `Render`。
- 属性：`StyledProperty` 供样式/绑定；只读状态用 `DirectProperty`（如 `CopyButton.State`）。值类属性（`Value`、`SelectedDate`、`IsChecked`、`Text`、`SelectedItem`）用 `defaultBindingMode: BindingMode.TwoWay`。
- 伪类：用 `[PseudoClasses(...)]` 声明，常量命名 `XxxPseudoClass`，在 `OnPropertyChanged` 里 `PseudoClasses.Set`。
- 模板部件：`[TemplatePart(...)]` 需要 `using Avalonia.Controls.Metadata;`，常量命名 `XxxPart`，在 `OnApplyTemplate` 里取，先解绑旧部件的事件。
- 不要声明与基类成员同名的属性：`Effect`、`Name`、`Content`（非 ContentControl 时）、`Background` 等会触发 CS0108 并隐藏基类行为。
- 文本、标签类属性给英文默认值（与 Arc 一致，如 `Label = "Copy"`），由使用方覆盖；无障碍名称写 `AutomationProperties.Name`。
- 内部控件要进入逻辑树时用 `LogicalChildren.Add`；需要给浮层设置逻辑父级时用公开接口 `((ISetLogicalParent)popup).SetParent(target)`（命名空间 `Avalonia.Controls`），关闭后置回 `null`。

## 3. Token

```csharp
[ControlDesignToken]
internal sealed class CopyButtonToken : AbstractControlDesignToken
{
    public Thickness InlinePadding { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        InlinePadding = new Thickness(12, 0);
    }
}
```

- **每个公开控件都必须有自己的 Token 类，且至少有一个属性**。空 Token 类能编译，但运行时 AtomUI 报 `A Control without Own Tokens cannot define a Token factory`。
- 属性名不能与 AtomUI 的 255 个全局 Token 同名（构建报 ATOMUIGEN019），清单见 [reserved-token-names.md](reserved-token-names.md)。常见冲突是 `SizeSM`、`PaddingSM`、`FontSize`，应改成 `DiameterSM`、`InlinePaddingSM`、`LabelFontSize` 这类名字。
- 类型限 AtomUI 内置转换器支持的：`double`、`int`、`bool`、`string`、`Thickness`、`CornerRadius`、`Color`、`BoxShadows`、`TimeSpan`、`FontWeight`、`FontFamily`，以及 Avalonia 内置的 `SplineEasing`（自定义 Easing 子类不行）。
- 颜色只能从 `ArcPalette.Neutrals(isDarkMode)` 派生（与 accent 无关），例如 `Oklch.MixOklab(n.Foreground, n.Background, 0.14)`、`Oklch.WithAlpha(n.Border, 0.7)`。随 accent 变化的颜色一律在主题里直接引用 ArcDesign Token（`Accent`、`AccentStrong`、`AccentSubtle`、`AccentForeground`、`ControlOn`、`Series1..4` 等）。
- XAML 中：`{arc:ArcDesignTokenResource Surface}`、`{arc:CopyButtonTokenResource InlinePadding}`。C# 中：`ArcTokens.Bind(target, property, ArcDesignTokenKind.X)`、`ArcTokens.GetColor(control, kind)`。

### Arc CSS 变量与 ArcDesign Token 对照

| Arc CSS | Token / 常量 |
|---|---|
| `--background` `--surface` `--surface-raised` `--surface-muted` | `Background` `Surface` `SurfaceRaised` `SurfaceMuted` |
| `--foreground` `--text-secondary` `--text-muted` | `Foreground` `TextSecondary` `TextMuted` |
| `--border` `--border-subtle` `--border-strong` | `Border` `BorderSubtle` `BorderStrong` |
| `--accent` `--accent-strong` `--accent-subtle` `--accent-foreground` | 同名 PascalCase |
| `--control-on` `--control-track(-hover)` `--control-thumb(-on)` `--control-glyph` `--control-fill` `--control-thumb-shadow` | `ControlOn` `ControlTrack(Hover)` `ControlThumb(On)` `ControlGlyph` `ControlFill` `ControlThumbShadow` |
| `--success` `--warning` `--danger`（`-subtle`） | `Success` `Warning` `Danger`（`SuccessSubtle` …） |
| `--series-1..4` | `Series1..4` |
| `--shadow-resting/raised/floating` | `ShadowResting` `ShadowRaised` `ShadowFloating` |
| `--radius-control/panel/surface/pill` | `RadiusControl`(18) `RadiusPanel`(26) `RadiusSurface`(34) `RadiusPill` |
| `--control-height-sm/md/lg` | `ControlSizeSM/MD/LG`（36/44/50） |
| `--space-N` | `SpaceN`（N×4px） |
| `--text-xs/sm/base/lg/xl/2xl..5xl` | `TypeXS`(12) `TypeSM`(14) `TypeBase`(16) `TypeLG`(18) `TypeXL`(22) `Type2XL`(28) `Type3XL`(36) `Type4XL`(52) `Type5XL`(72) |
| `--font-body/display/mono` | `BodyFontFamily` `DisplayFontFamily` `MonoFontFamily` |
| `--focus-ring` | `FocusRing`（宽 2，偏移 2） |
| `--duration-instant/fast/exit/standard/considered` | `ArcMotionPresets.Instant/Fast/Exit/Standard/Considered`（120/160/180/240/480ms） |
| `--ease-enter/exit/standard/in-out` | `ArcEasings.Enter/Exit/Standard/InOut` |
| `--duration-spring` + `--ease-spring` | `ArcTween.Spring`（580ms + `ArcEasings.CssSpring`） |
| `motionTokens.spring.smooth/snappy/morph` | `ArcSpring.Smooth/Snappy/Morph` |
| `motionTokens.blur.subtle/soft/text` | 2 / 4 / 8 px（`BlurSubtle/BlurSoft/BlurText`） |
| `motionTokens.stagger.char/word/line/item` | `ArcMotionPresets.StaggerChar/Word/Line/Item`（16/40/80/35ms） |

## 4. 主题（ControlTheme）

```xml
<ResourceDictionary xmlns="https://github.com/avaloniaui"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                    xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc"
                    x:ClassModifier="internal">
    <ControlTheme x:Key="{x:Type arc:Foo}" TargetType="arc:Foo">
        <Setter Property="arc:ArcMotion.Tracks" Value="True" />
        <Setter Property="Template"> ... </Setter>
        <Setter Property="Background" Value="{arc:ArcDesignTokenResource Surface}" />
        <Style Selector="^:pointerover"> ... </Style>
        <Style Selector="^:motion-full">
            <Setter Property="Transitions">
                <Transitions>
                    <arc:ArcBrushTransition Property="Background" />
                    <arc:SpringTransformOperationsTransition Property="RenderTransform" Spring="Snappy" />
                </Transitions>
            </Setter>
        </Style>
        <Style Selector="^:motion-reduced">
            <Setter Property="Transitions">
                <Transitions><arc:ArcBrushTransition Property="Background" Tween="Instant" /></Transitions>
            </Setter>
        </Style>
    </ControlTheme>
</ResourceDictionary>
```

- `Themes/` 下每个 `.axaml` 都会被当作主题资产自动注册，无需手工合并。
- 过渡（Transitions）只写在 `^:motion-full` / `^:motion-reduced` 下，不写在基础层；`arc:ArcMotion.Tracks="True"` 让控件在 Loaded 之后才获得这两个伪类，因此首帧永远不播动画。动效等级为 Off 时两个伪类都不存在。
- 可用的过渡：`ArcBrushTransition`、`ArcDoubleTransition`、`ArcColorTransition`、`ArcBoxShadowsTransition`、`ArcEffectTransition`（模糊）、`ArcThicknessTransition`、`ArcTransformOperationsTransition`（`Tween=Instant|Fast|Exit|Standard|Considered|Enter|Spring`），以及 `SpringDoubleTransition`、`SpringTransformOperationsTransition`、`SpringThicknessTransition`、`SpringPointTransition`、`SpringSizeTransition`、`SpringCornerRadiusTransition`、`SpringVectorTransition`（`Spring=Smooth|Snappy|Morph|Responsive|Gentle`，或 `VisualDuration` + `Bounce`）。声明式弹簧被打断时从当前值以零速度重新开始；需要保留速度的运动用代码里的 `MotionValue<T>`。
- 一个模板里的内部按钮可直接复用 Arc 控件（`arc:Button Variant="Ghost"`），再用 `^ /template/ arc|Button.some-class` 调整尺寸。
- 动画期间改 `RenderTransform` / `Opacity` / 自绘属性，不要改 `Width` / `Height` / `Margin`，除非布局确实需要跟随（如宽度弹簧）。

## 5. 代码驱动的动效

```csharp
_progress ??= new MotionValue<double>(this, 0, MotionVectors.Double);
_progress.Changed += _ => InvalidateVisual();
await _progress.AnimateTo(1, MotionSpec.Spring(ArcSpring.Snappy));               // 弹簧：被打断时保留速度
await _progress.AnimateTo(0, MotionSpec.Tween(ArcMotionPresets.Fast, ArcEasings.Standard, delay));
```

- `AnimateTo` 返回 `Task<bool>`：到达目标为 true，被打断为 false。
- `MotionValue` 自动遵守动效等级：Off 时直接跳到终值；Reduced 时弹簧直接跳到终值，补间仍然运行。因此位移、缩放类的补间要自己先检查 `ArcMotion.IsFull(this)`，Reduced 下只做透明度。
- 帧由 `MotionScheduler` 统一驱动（`TopLevel.RequestAnimationFrame`），空闲时不请求帧；控件未挂到可视树时 `AnimateTo` 直接跳到终值。
- 在 `Changed` 回调里只做两件事：写渲染状态（变换、透明度、自绘字段）然后 `InvalidateVisual()`；只有尺寸真的要流动时才 `InvalidateMeasure()`。
- 不要在 `Render` 中改任何会使视觉失效的状态（会抛 "Visual was invalidated during the render pass"）；需要重新测量文本或几何时放到 `MeasureOverride` 或属性变更时做。

## 6. 现有 Core 基元与可复用控件

| 类型 | 用途 |
|---|---|
| `ArcContentSwap` | 内容切换：`None`、`Fade`、`BlurSwap`（P2，4px 上升 + 模糊）、`Roll`（P3，按 `Direction` 8px）、`Pop`（图标弹出）、`Slide`（水平 12px，按 `Direction`）。旧内容不参与布局。 |
| `SpringSizeDecorator` | 宽或高随内容弹簧变化（P5/P6）；`Axis=Width/Height/Both`，`IsExpanded` 用于高度展开收起，被动尺寸变化直接跳变。 |
| `PresenceHost` | 单个子元素的进出场（AnimatePresence）：`Mode=Fade/Pop/Scale/Rise/Drop`，`Collapse=Width/Height` 让占位尺寸跟随；首次状态不播动画；`Exited` 事件。 |
| `GlidingIndicator` | 在多个项目之间弹簧滑动的高亮或选择块（P7）：`MoveTo(rect)`、`Hide()`。 |
| `RollingDigits` | 逐位里程表数字（P14），`IsArmed` 控制首次揭示。 |
| `StrokeDrawGlyph` | 描边绘制图标（P15），`DrawOnAttach` 或 `Play(duration, easing)`。 |
| `ArcGlyph` + `ArcIcons` | 24×24 Lucide 描边图标（Core 内置 187 个，`ArcIcons.Check` …）；不在列表里的用 `ArcIcons.Parse(pathData)` 并注明 Lucide 名称。 |
| `ArcSpinner` | 加载圈，可见时才旋转。 |
| `ArcPopup` | 锚定浮层（AtomUI Popup + Arc 浮层阴影 + P10 进出场）：`Placement`、`EnterOffset`、`EnterScale`、`IsInstant`。子元素用 `Border` 或模板化表面，阴影会跟随圆角。 |
| `Tooltip`（Arc 控件） | 附加属性 `arc:Tooltip.Tip` / `Side` / `ShowDelay` / `IsOpen`，任何控件都可用。 |
| `ViewportVisibility` | 进入视口检测（阈值、只触发一次）。 |
| `ListKeyboardNavigator` | 列表/网格的方向键、Home/End、PageUp/PageDown、键入跳转。 |
| `PausableTimer` | 可暂停、可续、可查询剩余时间的计时器（Toast、HoldToConfirm、Carousel）。 |
| `ArcFormatting` | 数字拆成 parts（只滚数字、符号切换），`Compact`（1.2K）。 |
| `ArcToday` | 本地"今天"，跨午夜发 `Changed`。 |
| `ArcClipboard` | `CopyTextAsync(visual, text)`。 |
| `ArcTokens` / `ArcPalette` / `Oklch` | Token 读取与绑定、中性色、OKLCH/OKLab 混色。 |
| `PathMorph` / `PathMorphShape` / `MorphGlyph`（P16） | SVG 路径解析与展平、同结构逐顶点或按弧长重采样、`MotionValue` 驱动（打断保速）；`MorphGlyph` 绘制 24×24 描边形变图标。 |
| `ModalPresence`（`Primitives/Modal`，S8） | 模态宿主：覆盖层、遮罩与背景模糊（`ReduceTransparency` 时关闭）、焦点陷阱与归还、Escape/遮罩可取消关闭、堆叠、先退场后移除；`Entrance=Rise/Slide/Fade/Custom`。 |
| `DragTracker` / `VelocityTracker` / `RubberBand` / `MomentumProjector`（`Gestures`，S10） | 拖拽阈值与轴锁、速度估计、橡皮筋阻尼、按速度投影选择吸附点并交给保速弹簧（`SettleTo`）。 |
| 字段基座（Arc 程序集 `TextFields/_Shared`） | `Field`、`FieldFrame`、`FieldMessage`、`FieldTextBox`、`FieldIconButton`：标签、说明、错误、尺寸、输入框外观；文本输入派生 `FieldTextBox`，复合输入用 `Field` + `FieldFrame`。 |
| 菜单表面（`Menus/_Shared`） | `MenuSurface` + `MenuEntry` / `MenuEntrySeparator` / `MenuEntryLabel`；`arc:DropdownMenu.Menu`、`arc:ContextMenu.Menu` 附加属性；`Open` / `OpenAt` / `Toggle`；变体 Dropdown / Context / Account / Sheet。 |
| 浮层表面（`Overlays/_Shared`、`Overlays/Popover`） | `DialogSurface`、`ModalOverlay`、`ModalSurface`、`Popover.ShowAt(anchor)`、`arc:Popover.Attach`。 |
| 图表基础（`Charts/_Foundation`） | `ChartBase`：比例尺、坐标轴、配色、保速形变（`ChartSpringArray`）、提示框、首次揭示、可访问摘要。 |
| Arc 控件 | `Button`（Primary/Secondary/Ghost/Danger，三种尺寸，`IsLoading`）、`CopyButton`、`Switch`、`TextMorph`、`Avatar`、`AnimatedCounter`、`Tooltip`、`Calendar` 可直接组合使用。 |

只在 Core 里新增被两个以上大类用到的东西；只服务一个大类的放该大类的 `_Shared/`。

## 7. 性能规则

1. **重复项自绘**：图表的点/柱/格、日历的日、热力图的格子用一个控件的 `Render` 画，或用轻量 `Control` 子类自绘，不用模板化控件堆叠。
2. **动画不触发布局**：动画只改变换、透明度和自绘状态；需要布局跟随的情况（宽度弹簧、高度展开）限制在一个装饰器内。
3. **池化与复用**：滑动、翻页、列表项进出时复用子控件（参考 `CalendarMonthStrip` 的三格池），不要每次重建。
4. **零逐帧分配**：缓存 `FormattedText`、`Geometry`、`Pen`、画刷；颜色在动画中变化时用 `ImmutableSolidColorBrush`，或缓存之后再改其颜色。
5. **循环动画要有边界**：只在可见、挂载、动效等级允许时运行（`ViewportVisibility`、`DetachedFromVisualTree` 时停止），参考 `ArcSpinner`。
6. **长列表虚拟化**：`ItemsControl` + `VirtualizingStackPanel`；表格、树、聊天线程都要能处理上千项。
7. **AOT 友好**：不用反射、不用 `dynamic`；绑定写在 C# 里或使用编译绑定。

## 8. API 易用性规则

- 默认值与 Arc 演示的默认外观一致，零配置即可得到 Arc 的样子。
- XAML 友好：内容属性接受字符串，枚举表示变体与尺寸（`ControlSize.Small/Medium/Large`），集合控件同时支持 `ItemsSource` 绑定和在 XAML 里直接写子项（如 `<arc:DropdownMenu><arc:MenuItem Header="Rename"/></arc:DropdownMenu>`）。
- 修饰类能力优先做成附加属性（`arc:Tooltip.Tip`），不强迫使用方加包装控件。
- 用户动作用路由事件或 `ICommand`（`Command` / `CommandParameter`），状态用可绑定属性；两者都提供时以属性为准。
- 关键回调给出强类型参数（`CalendarDateSelectedEventArgs.Date`），不让使用方再去读发送者。
- 可编程控制：常用的打开/关闭、重置、聚焦等操作提供公开方法（`Calendar.GoToMonth`、`Tooltip.SetIsOpen`）。

## 9. 无障碍与键盘

- 按 Arc 源码实现完整键盘模型（方向键、Home/End、Enter/Space、Escape 关闭浮层、Tab 顺序）。
- 键盘焦点环：模板里放一个 `PART_FocusRing`（2px `FocusRing`，外扩 2–4px，圆角比控件大一圈），在 `^:focus-visible` 下显示；自绘控件检查 `Classes.Contains(":focus-visible")`。
- 每个可交互元素都要有 `AutomationProperties.Name`；状态变化（复制成功、月份切换）更新名称或帮助文本。

## 10. 展示页

```csharp
public static class CopyButtonPage
{
    public static Control Create() => GalleryUi.Column(20,
        GalleryUi.Card("Outline", "Click to copy ...", GalleryUi.Row(new CopyButton { Value = "npx arc add" })));
}
```

每个大类的 `<Category>Pages.cs` 调用三个 partial 钩子 `Register<Category>A/B/C()`。并行开发时每个团队只拥有一个钩子，在自己的 `<Category>Pages.X.cs` 中实现，互不改同一个文件：

```csharp
// gallery/TKCode.Arc.Gallery/Pages/Buttons/ButtonsPages.A.cs
public static partial class GalleryPages
{
    static partial void RegisterButtonsA()
    {
        Register("ActionButton", ActionButtonPage.Create);
    }
}
```

`GalleryUi` 提供 `Card`、`Row`、`Column`、`Text`。每个展示页覆盖 Arc 文档里的主要用例（变体、尺寸、状态、禁用、键盘）。一个示例一个 `Card`：`Card` 是页面右侧 "On this page" 的一项，标题要能单独读懂；`Row` 在舞台上居中，其他内容撑满舞台宽度。页头（标题、说明、状态、Preview 标签）由外壳统一生成，展示页不再自己画标题。

## 11. 测试

- `[AvaloniaFact]` + `TestHelpers.Show(control)`：动效默认 Off，断言是确定的；`TestHelpers.Descendant<T>(root)` 找模板部件；无头环境的浮层会落到窗口的 overlay 层（`TestHelpers.Show` 已开启）。
- 键盘用 `window.KeyPressQwerty(PhysicalKey.X, RawInputModifiers.None)`（`using Avalonia.Headless;`），需要 `OnClick` 的按钮用 Enter 触发，直接 `RaiseEvent(ClickEvent)` 不会走 `OnClick`。
- 必测：默认尺寸/外观关键值、状态切换、键盘模型、边界（最小/最大、禁用项）、事件参数。
- `GallerySmokeTests` 会在明暗两种主题下渲染每个已注册的展示页，模板或主题错误会在这里暴露。

## 12. 构建与验证

```bash
dotnet build src/TKCode.Arc -c Debug          # 必须零警告、零错误（net10.0 与 net11.0）
dotnet test tests/TKCode.Arc.Core.Tests
dotnet test tests/TKCode.Arc.Tests            # 含 Gallery 冒烟测试
dotnet build gallery/TKCode.Arc.Gallery.Desktop
```

## 13. 已知的坑

| 现象 | 原因与处理 |
|---|---|
| 运行时 `A Control without Own Tokens cannot define a Token factory` | Token 类没有属性；至少加一个有意义的属性。 |
| ATOMUIGEN019 | Token 名与全局 Token 冲突，查 [reserved-token-names.md](reserved-token-names.md)。 |
| ATOMUIGEN037 | 语义部件 Path 必须以 `arc.` 开头。 |
| 自定义 Easing 作为 Token 时冻结失败 | Token 值会被格式化再解析，只能用 Avalonia 内置的 Easing 类型。 |
| CS0108 隐藏 `Visual.Effect` 等 | 换名字（`PresenceHost.Mode`）。 |
| `Visual was invalidated during the render pass` | 在 `Render` 里改了状态；移到测量或属性变更。 |
| `Unable to create IPopupImpl and no overlay layer` | 无头测试需要 overlay 层（`TestHelpers.Show` 已处理）；应用里正常。 |
| `TemplatePart` 找不到 | 缺 `using Avalonia.Controls.Metadata;`。 |
| zsh 中 `for x in $list` 不分词 | 用数组或 Python 生成文件。 |
| 动画属性写在基础样式里导致首帧动画 | 过渡只放在 `^:motion-full` / `^:motion-reduced` 下。 |
| 状态样式改不动模板部件的颜色 | 模板元素上写 `X="{arc:…TokenResource}"` 是本地值，`^:state /template/ X` 覆盖不了；随状态变化的值放进非触发样式 `^ /template/ X`。 |
| 焦点环、阴影、气泡被裁掉 | AtomUI 模板化控件默认 `ClipToBounds=True`；主题里加 `<Setter Property="ClipToBounds" Value="False" />`。 |
| 键盘聚焦时出现蓝色默认焦点框 | 窗口 AdornerLayer 的默认焦点装饰；自绘焦点环的主题加 `<Setter Property="FocusAdorner" Value="{x:Null}" />`。 |
| 模板里设置的属性不生效 | 构造函数里的赋值是本地值，优先于模板与样式；默认值用 `OverrideDefaultValue`，或 `SetCurrentValue`。 |
| 弹层离锚点太远、悬停弹层闪烁、锚点点不动 | 原生弹窗把 28–68px 的浮层阴影作为窗口外边距：AtomUI 不扣除时可见间距 = 边距 + 阴影；扣除后阴影区域盖住锚点并抢走指针（悬停弹层开合循环、锚点点不到）。Core `ArcPopup` 因此默认使用窗口覆盖层（`ShouldUseOverlayLayer=true`，阴影只是绘制），并通过放大锚点矩形保证无论是否翻转，表面都恰好距锚点 `MarginToAnchor`。组件只设 Arc 的 `sideOffset`，不要自行补偿；无头测试与真实应用几何一致。 |
| 测试命名空间遮蔽库命名空间 | 测试文件夹即命名空间；`Tests/Gestures` 会遮蔽 `Gestures.RubberBand`。避免用 `Primitives`、`Motion`、`Gestures`、`Theme` 作测试文件夹名，或写 `global::TKCode.Arc...`。 |
| `RenderTransform` 关键帧报 "No animator registered" | 样式动画不能对 `RenderTransform` 做关键帧；预置 `RotateTransform` 等并动画其 `Angle`。 |
| 无头测试里计时器不触发 | `DispatcherTimer` 只在 `PushFrame` 时触发，`RunJobs` 不会；`KeyPressQwerty` 只发按下，空格激活按钮还要 `KeyReleaseQwerty`。 |
| 自绘控件命中区域不完整 | 只在画过的像素上命中；整块可点击先 `FillRectangle(Brushes.Transparent, bounds)`。 |
| `Panel.Render` 是 sealed | 自绘容器派生 `Control` 并自管子元素，或加一个绘制子控件。 |
| 嵌套 OpacityMask 不刷新 | 修改可变 `LinearGradientBrush` 的 GradientStops 不一定传到合成器；值变化时发布新的不可变画刷。 |
| 大量 Run 的文本很慢 | 不用 `textStyleOverrides`（O(n²)）；`SelectableTextBlock` 子类重写 `CreateTextLayout` + 自定义 `ITextSource`。 |
| ControlTheme 中的后代选择器 | `^ arc|Child` 在运行时抛异常；动态生成的内部部件在同一主题文件里给自己的 ControlTheme（internal 类型可以）。 |
| 改名后找不到 `IAtomUIBuilder` 等 | `TKCode.Arc` 不在 `AtomUI` 命名空间下，使用 AtomUI 根类型（`GetThemeManager`、`IAtomUIBuilder`）的文件要 `using AtomUI;`。 |
| 带参数默认值的 `record struct` 变成全零 | `new Pose()` 与 `default` 不会使用参数默认值（`Scale = 1` 变成 0）；提供 `Rest` 之类的静态值，或用可空参数表示“未指定”。 |

## 14. 从 Arc 源码到 Avalonia

- Arc 免费组件源码（MIT）：`https://uiarc.dev/r/<slug>.json`，`files[].content` 是 TSX 与 CSS Module。只读取免费组件，不获取 Pro 内容。
- 本地副本：`python3 -I tools/dev/fetch_arc_sources.py . .work/arc-src` 把 110 个免费组件解包到 `.work/arc-src/<slug>/`（`_meta.json` 含标题、说明与文档）。`.work/` 不纳入版本库。
- 并行开发用 `tools/dev/sandbox.sh create .work/sandboxes/<name>` 建隔离副本，完成后由集成者 `sandbox.sh merge` 指定路径回主仓库。
- React 状态 → 控件属性与私有字段；`useEffect` → 属性变更与挂载/卸载回调；`AnimatePresence` → `PresenceHost` / `ArcContentSwap`；`motion.div animate={{x}}` + spring → `MotionValue` 或 `Spring*Transition`；`layout` 动画 → `GlidingIndicator` / `SpringSizeDecorator` / 自绘位置弹簧；Radix 浮层 → `ArcPopup`；CSS `transition` → 主题 `Transitions`；`:hover` → `:pointerover`；`:active` → `:pressed`；`:focus-visible` → `:focus-visible`；`data-state` → 伪类。
- `@media (hover: hover) and (pointer: fine)` 的悬停效果：触控指针不触发悬停（`e.Pointer.Type == PointerType.Touch` 时忽略）。
- `prefers-reduced-motion` → `ArcMotionLevel.Reduced`：去掉位移、缩放和模糊，只保留短时透明度。
