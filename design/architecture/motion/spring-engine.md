# 弹簧引擎

> `SpringSpec`、`SpringSolver` 的数学模型、与 Motion（framer-motion）的参数换算、收敛判定与重定向规则。命名空间 `TKCode.Arc.Motion`。

## 1. 模型

阻尼谐振子：`m·x'' + c·x' + k·(x − target) = 0`。

- `k`：刚度（stiffness），`c`：阻尼（damping），`m`：质量（mass，默认 1）。
- 固有角频率 `ω₀ = √(k/m)`，阻尼比 `ζ = c / (2·√(k·m))`。
- 初始偏移 `x₀ = from − target`，初始速度 `v₀`（单位：值 / 秒）。

## 2. 解析解

| 情况 | 位移 `x(t) − target` | 说明 |
|---|---|---|
| 欠阻尼 ζ < 1 | `e^(−ζω₀t) · (x₀·cos(ω_d t) + ((v₀ + ζω₀x₀)/ω_d)·sin(ω_d t))`，`ω_d = ω₀√(1−ζ²)` | 有回弹 |
| 临界阻尼 ζ = 1 | `e^(−ω₀t) · (x₀ + (v₀ + ω₀x₀)·t)` | 最快无回弹 |
| 过阻尼 ζ > 1 | `e^(−ζω₀t) · (x₀·cosh(ω_h t) + ((v₀ + ζω₀x₀)/ω_h)·sinh(ω_h t))`，`ω_h = ω₀√(ζ²−1)` | 缓慢无回弹 |

- 速度由解析式求导得到，不做数值微分。
- ζ 与 1 的差小于 1e-6 时按临界阻尼处理，避免除以接近 0 的 `ω_d`。

## 3. 与 Motion 的换算

Arc 的组件源码用以下辅助函数（26 个组件使用）把视觉参数换算为物理参数，以便重定向时保留速度：

```ts
const physical = (visualDuration, bounce) => {
  const root = 2 * Math.PI / (visualDuration * 1.2);
  return { type: "spring", stiffness: root * root, damping: 2 * (1 - bounce) * root, mass: 1 };
};
```

`SpringSpec.FromVisual(visualDuration, bounce)` 与之等价：

```
root = 2π / (1.2 · visualDuration)
k    = root²
ζ    = clamp(1 − bounce, 0.05, 1)
c    = 2 · ζ · √k        （m = 1）
```

Arc 预设换算结果：

| 预设 | visualDuration | bounce | k | c | ζ | 用途 |
|---|---|---|---|---|---|---|
| smooth | 0.40s | 0 | 171.3 | 26.18 | 1.00 | 报告状态的运动：开关拇指、抽屉边缘、面板高度、进度、数值 |
| snappy | 0.26s | 0.12 | 405.6 | 35.44 | 0.88 | 按压、小指示器、图标切换、弹出、菜单进入、悬停高亮 |
| morph | 0.42s | 0.16 | 155.4 | 20.94 | 0.84 | 共享高亮、随内容变化的宽度、形状形变、列表重排 |
| responsive | — | — | 520 | 38 | 0.83 | 保留预设（`FromPhysical`），免费组件未使用 |
| gentle | — | — | 340 | 34 | 0.92 | 保留预设（`FromPhysical`），免费组件未使用 |

组件可以在预设基础上覆盖 visualDuration，例如收起时用 `smooth` 加 0.3s。也可以直接使用物理参数，例如 Reactions 的速度踢 `k=420, c=17`、PasswordStrength 的抖动 `k=900, c=15, v₀=−240`。这些特殊值写在组件规格与控件 Token 中。

## 4. 类型

```csharp
public readonly record struct SpringSpec(double Stiffness, double Damping, double Mass = 1)
{
    public static SpringSpec FromVisual(double visualDuration, double bounce);
    public static SpringSpec FromPhysical(double stiffness, double damping, double mass = 1);
    public double DampingRatio { get; }
    public TimeSpan EstimateSettle(double initialDelta, double initialVelocity);   // 用于 ArcSpringEasing 与测试
}

public readonly record struct TweenSpec(TimeSpan Duration, Easing Easing, TimeSpan Delay = default);

public abstract record MotionSpec   // Spring | Tween | Instant，统一传给 MotionValue 与过渡
{
    public sealed record Spring(SpringSpec Value, TimeSpan Delay = default) : MotionSpec;
    public sealed record Tween(TweenSpec Value) : MotionSpec;
    public sealed record Instant : MotionSpec;
}

public static class ArcMotionPresets   // 默认值来自 ArcDesign Token，可被主题覆盖
{
    public static SpringSpec Smooth { get; }
    public static SpringSpec Snappy { get; }
    public static SpringSpec Morph { get; }
    public static TweenSpec Fast { get; }       // DurationFast + EaseStandard
    public static TweenSpec Exit { get; }       // DurationExit + EaseExit
    // …
}

internal static class SpringSolver
{
    public static (double Position, double Velocity) Evaluate(in SpringSpec spec, double x0, double v0, double t);
}
```

`ArcMotionPresets` 在首次访问与主题发布时，从 ArcDesign Token（`SpringSmoothDuration` 等）重建，保证主题覆盖生效。

## 5. 收敛判定

与 Motion 默认值一致（实现时对照 Motion 源码核对）：

| 量级 | 判定 |
|---|---|
| 初始偏移 \|Δ\| < 5（缩放、透明度、0–1 进度等精细量） | \|x − target\| < 0.005 且 \|v\| < 0.01 |
| 其他（像素位移、尺寸） | \|x − target\| < 0.5 且 \|v\| < 2 |

收敛后，值吸附到目标并停止推进。多维值（Point、Rect、颜色等）的所有分量都收敛后才算结束。

## 6. 重定向

动画进行中目标改变时：

1. 在当前时刻求出位置 `p` 与速度 `v`。
2. 以 `p` 为起点、新目标为终点、`v` 为初速度，开始新的弹簧段。
3. 时间从 0 重新计算，规格可以不同（例如从 snappy 切到 smooth）。

由此，快速反复切换（例如连续点击开关）时运动连续，不会出现 Avalonia 原生过渡那样的速度归零与跳变。

## 7. 补间

- `TweenSpec` 用于颜色、透明度与短淡入淡出：时长来自 `DurationInstant`…`DurationConsidered`，缓动来自 `EaseEnter` / `EaseExit` / `EaseStandard` / `EaseInOut`（三次贝塞尔）。
- Arc 规则：模糊与透明度永远用补间，只有缩放或位移才用弹簧，"这样模糊不会因回弹变成负值"。
- 补间被打断时，从当前值开始新的补间（没有速度概念）。

## 8. 数值稳定性

- 帧间隔超过 64ms 时，按 16ms 的子步推进，避免大步长误差。
- 只做解析求值，不积分，所以没有积分漂移；子步只用于检查收敛的时机。
- `k`、`c`、`m` 必须为正；非法输入在构造时抛出 `ArgumentOutOfRangeException`。
