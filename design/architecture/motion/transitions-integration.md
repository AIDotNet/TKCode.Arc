# ControlTheme 集成

> 如何在 ControlTheme 中声明状态动画（颜色、透明度、按压、拇指位移），满足 AtomUI 的"ControlTheme 优先、Transitions 只能写在 style setter 中"规则。
> 本文描述的是已实现的方案（S0.7）；它与最初设想的"自实现 ITransition、保留速度"不同，原因见第 1 节。

## 1. 约束与取舍

- Avalonia 12.1.4 的 `ITransition` 只能由框架内部实现：`TransitionBase` 的 `Apply` / `DoTransition` 是 internal，外部无法写一个"读取当前速度再重定向"的过渡。
- 因此声明式弹簧过渡是**内置过渡的子类**（`DoubleTransition`、`TransformOperationsTransition` 等），把弹簧在其收敛时间内烘焙成 `ArcSpringEasing`，`Duration` 取收敛时间。
  - 曲线形状与 framer-motion 的同参数弹簧一致（含回弹）。
  - 被打断时从当前值以**零速度**重新开始，这是 Avalonia 过渡的固有行为。按压、悬停、开关这类短促状态变化中，这个差异肉眼难以察觉。
- 需要保留速度的运动（手势松手、连续重定向的指示器、日历月份条、数字滚轮）一律用代码中的 `MotionValue<T>`（见 [values-and-scheduler.md](values-and-scheduler.md)）。
- 共享主题实例不是问题：内置过渡的运行状态由 Avalonia 按控件实例保存，过渡对象本身无状态。

## 2. 提供的类型

| 类型 | 动画属性类型 | 典型用途 |
|---|---|---|
| `SpringDoubleTransition` | double | 拇指宽度、标量 |
| `SpringPointTransition` | Point | 位置 |
| `SpringSizeTransition` | Size | 尺寸 |
| `SpringThicknessTransition` | Thickness | 拇指偏移（Margin）、内边距 |
| `SpringCornerRadiusTransition` | CornerRadius | 圆角形变 |
| `SpringTransformOperationsTransition` | `TransformOperations` | 按压缩放、位移 |
| `SpringVectorTransition` | Vector | 偏移向量 |
| `ArcBrushTransition` / `ArcColorTransition` | IBrush / Color | 颜色补间 |
| `ArcDoubleTransition` | double | 透明度补间 |
| `ArcBoxShadowsTransition` | BoxShadows | 阴影补间 |
| `ArcEffectTransition` | IEffect | 模糊补间 |
| `ArcThicknessTransition` / `ArcTransformOperationsTransition` | Thickness / Transform | 非弹簧的位移、旋转 |

- 弹簧过渡的属性：`Spring`（`Smooth` / `Snappy` / `Morph` / `Responsive` / `Gentle`），或 `VisualDuration` + `Bounce` 自定义。
- 补间过渡的属性：`Tween`，可取 `Instant`、`Fast`、`Exit`、`Standard`、`Considered`、`Enter`（240ms + enter 曲线）、`Spring`（Arc 的 CSS 弹簧：580ms + `ArcEasings.CssSpring`）。

## 3. 动效等级与首帧

- 控件主题设置 `arc:ArcMotion.Tracks="True"`。控件 Loaded 之后（经一次 Dispatcher 投递），`ArcMotion` 根据有效等级给控件加伪类 `:motion-full` 或 `:motion-reduced`。
  - 有效等级取继承属性 `arc:ArcMotion.Level` 与主题 Token `MotionMode` 中较低者。
  - 等级为 Off 时两个伪类都没有。
- 主题只在这两个伪类下声明 `Transitions`。Loaded 之前没有伪类，也就没有过渡，所以首帧和初始值永远不播动画。这取代了最初设想的 `DisableTransitions()` / `EnableTransitions()` 方案，不需要反射，也不需要逐个处理模板元素。
- `:motion-reduced` 下只保留短时（Instant）颜色和透明度过渡，不声明位移、缩放类过渡。

## 4. 写法示例

```xml
<ControlTheme x:Key="{x:Type arc:Switch}" TargetType="arc:Switch">
    <Setter Property="arc:ArcMotion.Tracks" Value="True" />
    <!-- 模板省略 -->
    <Style Selector="^:motion-full">
        <Style Selector="^ /template/ Border#PART_Thumb">
            <Setter Property="Transitions">
                <Transitions>
                    <arc:SpringThicknessTransition Property="Margin" VisualDuration="0.3" Bounce="0" />
                    <arc:SpringDoubleTransition Property="Width" Spring="Snappy" />
                </Transitions>
            </Setter>
        </Style>
        <Style Selector="^ /template/ Border#PART_Track">
            <Setter Property="Transitions">
                <Transitions>
                    <arc:ArcBrushTransition Property="Background" />
                </Transitions>
            </Setter>
        </Style>
    </Style>
    <Style Selector="^:motion-reduced /template/ Border#PART_Track">
        <Setter Property="Transitions">
            <Transitions>
                <arc:ArcBrushTransition Property="Background" Tween="Instant" />
            </Transitions>
        </Setter>
    </Style>
</ControlTheme>
```

- `Transitions` 永远放在 style setter 中，不放在 ControlTemplate 内容里（AtomUI 规则）。
- 模板内部元素的过渡写在 `^:motion-full /template/ ...` 选择器下。

## 5. 什么时候不用过渡，而用代码

| 情况 | 原因 | 做法 |
|---|---|---|
| 值来自手势（拖拽、滑动） | 过渡只能响应属性变化，不能跟随手指并继承速度 | `MotionValue<T>` + 手势服务 |
| 需要在重定向时保留速度 | 内置过渡从零速度重启 | `MotionValue<T>` |
| 元素需要先保持挂载再移除 | 过渡不管理元素生命周期 | `PresenceHost` / `ArcContentSwap` |
| 位置变化由布局引起，或需要逐项交错 | 属性没有变化，过渡不会触发 | `GlidingIndicator`、自绘位置弹簧 |
| 大量重复元素（图表、日历日格） | 每个元素一套过渡开销大 | 在一个控件的 `Render` 中按 `MotionValue` 自绘 |

代码中注释说明 ControlTheme 不可行的具体原因（AtomUI 规则）。
