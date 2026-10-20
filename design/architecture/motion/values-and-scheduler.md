# 动效值与调度器

> `MotionValue<T>`、向量类型、`MotionScheduler`、帧时钟与可见性规则。命名空间 `TKCode.Arc.Motion`。

## 1. MotionValue<T>

```csharp
public sealed class MotionValue<T> : IDisposable where T : struct
{
    public MotionValue(Visual owner, T initial, IMotionVector<T> vector);

    public T Value { get; }                 // 当前值
    public T Velocity { get; }              // 当前速度（每秒）
    public T Target { get; }
    public bool IsAnimating { get; }

    public event Action<T>? Changed;        // 每帧推进后触发，用于写入属性或 InvalidateVisual

    public Task AnimateTo(T target, MotionSpec spec, T? initialVelocity = null);   // 进行中调用：保留当前位置与速度
    public void Jump(T value);              // 立即设置并停止，速度归零
    public void Stop();                     // 停在当前值
}
```

- `owner` 决定使用哪个 TopLevel 的调度器，并用于可见性与分离检测。
- `AnimateTo` 返回的 Task 在收敛、被 `Jump` / `Stop` 打断或 owner 分离时完成，不抛异常。
- 等级为 Off 时，`AnimateTo` 等价于 `Jump`；Reduced 下的行为由调用方按 [减少动效](reduced-motion.md) 选择规格。
- 线程：只能在 UI 线程使用。

## 2. 向量类型

`IMotionVector<T>` 提供分量拆分与组合，使多维值的每个分量独立运行弹簧（与 Motion 一致）：

| 类型 | 分量 | 备注 |
|---|---|---|
| `double` | 1 | |
| `Point`、`Vector` | 2 | |
| `Size` | 2 | |
| `Rect` | 4 | 位置 + 尺寸 |
| `Thickness` | 4 | |
| `CornerRadius` | 4 | 表面形变时圆角与尺寸一起变化 |
| `Color` | 4 | 在 OKLab 空间插值，避免 sRGB 插值发灰 |
| `Transform2D` | 5 | TranslateX、TranslateY、ScaleX、ScaleY、Rotate；写入复用的 `MatrixTransform` |

`Transform2D` 是 Arc 定义的值类型。`TransformMotion` 辅助类把它写入元素唯一的 `MatrixTransform`，每帧只改 `Matrix` 属性，不分配新对象。

## 3. 调度器

```csharp
internal sealed class MotionScheduler
{
    public static MotionScheduler For(TopLevel topLevel);   // 每个 TopLevel 一个
    public void Register(IMotionDriver driver);             // MotionValue 开始动画时注册
}
```

- 首个活动值注册时调用 `TopLevel.RequestAnimationFrame(OnFrame)`；每帧推进所有活动值。只要还有活动值，就继续请求下一帧；全部收敛后停止请求。
- 帧时间取回调提供的 `TimeSpan` 时间戳。帧间隔大于 64ms 时按子步推进（见 [弹簧引擎](spring-engine.md#8-数值稳定性)）。
- 同一帧内的所有写入集中完成，Avalonia 在随后的渲染中合并。
- 调度器持有活动值的强引用，持有 owner 的弱引用，避免泄漏。

## 4. 帧时钟抽象

```csharp
public interface IFrameClock
{
    void RequestFrame(Action<TimeSpan> callback);
}
```

| 实现 | 用途 |
|---|---|
| `TopLevelFrameClock` | 生产环境：包装 `TopLevel.RequestAnimationFrame` |
| `ManualFrameClock` | 测试：`Advance(TimeSpan)` 精确推进；配合 `AvaloniaHeadlessPlatform.ForceRenderTimerTick` |

`ArcMotion.FrameClockOverride`（internal，测试可见）用于在测试中替换时钟。

## 5. 可见性与生命周期

| 情况 | 行为 |
|---|---|
| owner 从可视树分离 | 所有关联的动效值立即跳到目标，Task 完成 |
| owner 不可见（`IsEffectivelyVisible=false`） | 跳到目标；持续循环暂停 |
| 窗口最小化或隐藏 | 跳到目标；持续循环暂停 |
| 主题切换 | 不打断进行中的动画；颜色类的值从当前值开始新的补间 |
| 控件被回收（虚拟化容器复用） | 调用方在 `PrepareContainer` 时 `Jump` 到新状态 |

## 6. 写入目标

| 目标 | 方式 | 触发布局 |
|---|---|---|
| `RenderTransform` | `TransformMotion` 写复用的 `MatrixTransform`，配合 `RenderTransformOrigin` | 否 |
| `Opacity` | 直接设置 | 否 |
| `Effect`（BlurEffect） | 复用一个 `BlurEffect`，更新 `Radius`；值为 0 时移除 Effect | 否 |
| `Clip` | 更新几何对象（裁剪揭示，P29） | 否 |
| 自绘属性 | 控件的 `StyledProperty` 注册时声明 `AffectsRender` | 否 |
| `Width` / `Height` | `SpringSizeDecorator` 在 Measure 中使用当前值 | 是（每帧） |

## 7. 诊断

- Debug 构建提供 `ArcMotion.Diagnostics`：当前活动值数量、每帧推进耗时、最长帧。Gallery 的开发者面板显示这些数据。
- Release 构建中诊断代码被裁剪。
