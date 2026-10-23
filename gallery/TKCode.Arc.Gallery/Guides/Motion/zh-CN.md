---
title: 动效
source-hash: 09a655958638
---

# 动效

TKCode.Arc 中的动效遵循 Arc 的动效体系：凡是移动或改变尺寸的内容使用弹簧，颜色和不透明度使用简短的缓动淡入淡出，文字变化则使用滚动数字和逐词改写。动效的作用是说明发生了什么变化（面板从哪里来、哪个数值移动了、哪个词是新的），而不是装饰。库中的每个动画都遵循动效级别，因此对动效敏感的用户，以及需要保持轻量运行的设备，都能获得更平静的界面，同时不丢失任何信息。

## 动效级别

共有三种级别：`ArcMotionLevel.Full`、`Reduced` 和 `Off`：

| 级别 | 移动的内容 | 适用场景 |
|---|---|---|
| `Full`（默认） | 弹簧、位移、缩放、形变、模糊、错峰入场、滚动数字 | 常规体验。 |
| `Reduced` | 仅短淡入淡出和颜色变化；位置与尺寸直接跳到终点，装饰性循环停止 | 要求减少动效的用户、远程桌面会话、演示。 |
| `Off` | 无；所有变化立即生效 | 测试与截图、自助终端和低功耗设备。 |

信息不会仅由动效传递：在 `Reduced` 和 `Off` 下，最终状态与 `Full` 下完全相同。

### 设置级别

`ArcMotion.Level` 是一个可继承的附加属性。在窗口上设置会影响其中的全部内容；在任意容器上设置则只影响界面的该部分：

```csharp
ArcMotion.SetLevel(mainWindow, ArcMotionLevel.Reduced);
```

```xml
<StackPanel arc:ArcMotion.Level="Off" Spacing="12">
    <arc:Progress Label="导入" Value="64" />
    <arc:Switch Content="后台同步" IsChecked="True" />
</StackPanel>
```

主题也可以为整个应用设置级别，方法是通过 `WithArc(arc => arc.MotionMode(...))`（参见 **主题**）。控件的实际级别取两者中较低的那个：继承的 `ArcMotion.Level` 与主题的动效模式。在设为 `Reduced` 的主题中，即使窗口设为 `Full`，也会以减弱动效运行。

库本身不会读取操作系统的减少动效偏好。请在应用程序中提供设置（图库的外观面板提供 Full、Reduced 和 Off 三个选项），将其与用户的其他偏好一起保存，并在启动时应用到主窗口。

## 动效系统

### 弹簧

Arc 的弹簧由它们呈现出的时长与回弹程度定义，而不是由物理常数定义。预设是 `ArcSpring` 值，也可通过 `ArcMotionPresets` 获取：

| 名称 | 形态 | 用途 |
|---|---|---|
| `Smooth` | 0.4 s，无回弹 | 反映状态的动效：开关滑块、面板、进度和数值。 |
| `Snappy` | 0.26 s，回弹 0.12 | 按压、小型指示器、图标切换、弹入和菜单。 |
| `Morph` | 0.42 s，回弹 0.16 | 共享高亮、随内容变化的宽度、形状变形。 |
| `Responsive` | 刚度 520，阻尼 38 | 必须跟上指针的直接操作。 |
| `Gentle` | 刚度 340，阻尼 34 | 幅度较大、较平缓的移动。 |

动画运行期间启动的弹簧，会从当前的位置和速度继续，因此打断动画（连按两次按钮、拖回）时永远不会跳变。

### 时长与缓动

| 令牌 | 取值 | 用途 |
|---|---|---|
| `DurationInstant` | 120 ms | 悬停与按压的颜色。 |
| `DurationFast` | 160 ms | 小幅淡入淡出，以及大多数位移在减弱动效下的替代方案。 |
| `DurationExit` | 180 ms | 元素离开；离场比入场更快。 |
| `DurationStandard` | 240 ms | 大多数淡入淡出和颜色过渡。 |
| `DurationConsidered` | 480 ms | 幅度较大、经过深思的变化。 |
| `DurationSpring` | 580 ms | CSS 风格的弹簧曲线。 |

缓动曲线为 `EaseEnter`、`EaseExit`、`EaseStandard` 和 `EaseInOut`（代码中为 `ArcEasings`）。错峰入场在元素之间使用 `StaggerChar`（16 ms）、`StaggerWord`（40 ms）、`StaggerLine`（80 ms）和 `StaggerItem`（35 ms）。

### 性能

动效运行在共享的帧调度器上：正在运行的弹簧和循环会在每一帧合并为一批同步推进，没有任何内容移动时则不安排任何工作。持续性效果（骨架屏脉冲、闪烁高光、实时指示点）在其控件滚出可视区域、被隐藏或从视觉树中分离时暂停。动态模糊量化为 0.25 px 的步进并可复用，因此运行中的模糊不会每帧分配内存。

## 在自己的控件中使用动效

库遵循的规则同样适用于你自己编写的控件。

### 控件主题中的过渡

在控件的 `ControlTheme` 中，于动效伪类下声明过渡，并设置 `arc:ArcMotion.Tracks="True"`，以便控件接收这些过渡。加载后，被追踪的控件会获得 `:motion-full` 或 `:motion-reduced`，因此第一帧永远不会产生动画；级别为 `Off` 时两者都不会获得，因此不会有任何动画：

```xml
<ResourceDictionary xmlns="https://github.com/avaloniaui"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                    xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <ControlTheme x:Key="StatusPanelTheme" TargetType="Border">
        <Setter Property="arc:ArcMotion.Tracks" Value="True" />
        <Style Selector="^:motion-full">
            <Setter Property="Transitions">
                <Transitions>
                    <arc:SpringDoubleTransition Property="Opacity" Spring="Smooth" />
                    <arc:SpringThicknessTransition Property="Padding" Spring="Snappy" />
                </Transitions>
            </Setter>
        </Style>
        <Style Selector="^:motion-reduced">
            <Setter Property="Transitions">
                <Transitions>
                    <DoubleTransition Property="Opacity" Duration="0:0:0.16" />
                </Transitions>
            </Setter>
        </Style>
    </ControlTheme>
</ResourceDictionary>
```

弹簧过渡（`SpringDoubleTransition`、`SpringThicknessTransition`、`SpringCornerRadiusTransition`、`SpringSizeTransition`、`SpringPointTransition`、`SpringVectorTransition` 和 `SpringTransformOperationsTransition`）接受 `Spring` 预设，或自定义的 `VisualDuration` 与 `Bounce`。在减弱动效下，用短暂的不透明度淡入淡出替代移动，并让尺寸和位置立即变化。

### 控件主题无法表达的动效

当某个数值必须跟随指针、在打断时保持速度，或驱动自定义绘制时，请使用 `MotionValue<T>`。它遵循其所属对象的实际动效级别：在 `Off` 下，以及在 `Reduced` 下的弹簧，都会直接跳到目标值。

```csharp
public sealed class SpendBar : Control
{
    private readonly MotionValue<double> _fill;
    private double _shown;

    public SpendBar()
    {
        _fill = new MotionValue<double>(this, 0, MotionVectors.Double);
        _fill.Changed += value =>
        {
            _shown = value;
            InvalidateVisual();
        };
    }

    public void ShowShare(double share) => _ = _fill.AnimateTo(share, MotionSpec.Spring(ArcSpring.Smooth));

    public override void Render(DrawingContext context)
    {
        var accent = ArcTokens.GetColor(this, ArcDesignTokenKind.Accent);
        context.FillRectangle(new SolidColorBrush(accent), new Rect(0, 0, Bounds.Width * _shown, Bounds.Height));
    }
}
```

`MotionVectors` 涵盖 `double`、`Point`、`Vector`、`Size`、`Rect`、`Thickness`、`CornerRadius` 和 `Color`；每个分量各自运行自己的弹簧。`MotionSpec.Tween(duration, easing)` 运行定时曲线而非弹簧，`MotionSpec.Instant` 直接跳到目标。对于循环（脉冲、扫光），使用 `LiveLoop`：它在屏幕外以及低于其最低动效级别时暂停；对于在固定时间内运行到 1 的数值（长按确认的填充、倒计时），使用 `MotionTimeline`。

只有当控件主题无法表达时才使用代码驱动的动效，并在旁边的注释中说明原因。主题过渡的开销更低，更容易审查，并且会自动遵循动效级别。

## 指导原则

**推荐**

- 为变化而设计动画，而不是为元素而设计：让移动的内容移动，让出现的内容淡入，让变化的数字滚动。
- 入场控制在半秒以内，离场比入场更快。
- 为每个动画变化提供在没有动画时也能正确呈现的终态，然后用 `Off` 检查。
- 按压的反馈使用 `Snappy`，报告数值的内容使用 `Smooth`。

**避免**

- 仅用动效作为状态变化的信号；应与文字、图标或颜色搭配。
- 同时为大量大型表面添加动画；应错峰进行，或只对容器做动画。
- 在代表金额、进度或错误的内容上使用回弹：它会显得轻浮。
- 在主要内容中使用无限循环；应留给加载状态，并在内容到达时停止。

## 测试

- 使用 `ArcMotionLevel.Off` 运行测试并截图，使结果显示最终状态，而不依赖时序。库自身的测试和图库截图都是这样做的。
- 用 `Reduced` 检查每个屏幕一次：不应有任何滑动、缩放或回弹，也不应缺少任何内容。
- 注意 `Off` 下的布局跳变：如果某个容器只有依靠尺寸动画才显得正确，则需要固定的布局。
