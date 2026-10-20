# 0006 动效引擎：自研弹簧引擎

- 状态：已接受；S0.6 已验证，S0.7 按下方修订实现并验证
- 日期：2026-10-20

## 背景

- Arc 的动效建立在 Motion（framer-motion）的弹簧之上。它的规则（`motion.md`）要求：
  - 一切可打断；
  - 尺寸跟随弹簧；
  - 数字滚动到新值；
  - 用户可逆的动作用弹簧；
  - 首帧不播放动画；
  - 每个动画都有减少动效分支。
- Arc 实际使用的弹簧预设：`smooth`（visualDuration 0.4，bounce 0）、`snappy`（0.26，0.12）、`morph`（0.42，0.16）。
  - 26 个组件通过 `physical(visualDuration, bounce)` 把它们换算成刚度 / 阻尼，以便重定向时保留速度。
- AtomUI 只有固定时长的缓动曲线：`AbstractMotion` 预设、`BaseTransitionUtils`、`SolidColorBrushTransition` 等。
- Avalonia 12.1.4 的 `SpringEasing` 是按动画时长归一化的曲线：它不会按物理过程自行收敛，打断时也不保留速度。
- Avalonia 提供 `TopLevel.RequestAnimationFrame(Action<TimeSpan>)`，可用于逐帧驱动。`CompositionCustomVisualHandler` 运行在渲染线程，只能绘制自身。
- AtomUI 的硬约束：
  - Transitions 只能声明在 ControlTheme 的 style setter 中；
  - 能用 ControlTheme 表达的绑定不写在代码里；
  - 控件初始化时 `DisableTransitions()`，Loaded 之后再 `EnableTransitions()`。

## 决策

1. 在 `TKCode.Arc.Core`（命名空间 `TKCode.Arc.Motion`）实现自研弹簧引擎：
   - `SpringSpec`：`FromVisual(visualDuration, bounce)` 与 Motion 的换算一致；另有 `FromPhysical(stiffness, damping, mass)`。
   - `SpringSolver`：解析解阻尼振子，覆盖欠阻尼、临界阻尼、过阻尼三种情况。
   - `MotionValue<T>`：可打断，重定向时保留当前位置与速度。
   - `MotionScheduler`：每个 TopLevel 一个，基于 `RequestAnimationFrame` 批量推进，空闲时不请求帧。另有 `ManualFrameClock` 供测试使用。
2. 提供可在 ControlTheme 中声明的适配器，满足 AtomUI 的"ControlTheme 优先"规则：
   - `ArcSpringEasing`：把弹簧在收敛时间内烘焙成曲线；
   - `Spring*Transition`：内置过渡的子类，使用烘焙后的弹簧曲线（修订见下）。
3. 提供 Presence（退场保活）、FLIP 布局动画、共享元素、尺寸弹簧等服务，以及 30 个动效原语。见 [原语目录](../architecture/motion/primitives-catalog.md)。
4. 动效分三级：Full / Reduced / Off。
   - 有效等级 = min(`ArcMotion.Level` 附加属性, `MotionMode` Token, 控件 `IsMotionEnabled`, 可选的系统探针)。
   - 每类动效的降级方式见 [减少动效](../architecture/motion/reduced-motion.md)。
5. `CompositionCustomVisualHandler` 只用于自绘的持续动画（闪光、骨架脉冲、图表实时线等），不作为通用驱动。

## 修订（实现阶段，2026-10-20）

- Avalonia 12.1.4 的 `ITransition` 不能由外部实现（`TransitionBase.Apply` 为 internal），因此第 2 条中"按控件保存速度的 `Spring*Transition`"无法实现。
- 实际做法：
  - `Spring*Transition` 继承内置过渡，使用 `ArcSpringEasing` 并把 `Duration` 设为收敛时间。曲线与弹簧一致，被打断时从零速度重启。
  - 需要保留速度的运动（手势、连续重定向、月份条、数字滚轮）一律走 `MotionValue<T>`。
- 首帧不动画改用 `arc:ArcMotion.Tracks` 伪类方案：Loaded 后才出现 `:motion-full` / `:motion-reduced`，过渡只声明在这两个伪类下。不使用 `DisableTransitions()` / `EnableTransitions()`。
- 有效等级 = min(`ArcMotion.Level`, `MotionMode` Token)；控件级 `IsMotionEnabled` 与系统探针未实现。
- 详见 [ControlTheme 集成](../architecture/motion/transitions-integration.md)。

## 备选方案

- **只用 Avalonia Transitions / Animation + SpringEasing 或贝塞尔近似**：完全声明式、实现量最小；但打断时速度丢失会跳变，拖拽惯性、橡皮筋与手势回弹无法还原。未选。
- **以 Composition 线程动画为主**：渲染线程运行最流畅；但 Avalonia 没有弹簧自然动画 API，难以与布局、输入联动，调试成本高。未选。

## 后果

- 正面：
  - 动效手感与 Arc 原版等价，可打断、可重定向、可与手势速度衔接。
  - 测试可以用 `ManualFrameClock` 精确推进时间。
- 负面：
  - 需要自行维护数值稳定性与性能，并防止空闲帧请求。
  - 动画在 UI 线程推进，宽高弹簧会触发逐帧布局，需要控制预算。
- 验证：
  - S0.6：5 个预设与 framer `spring()` 参考值的误差 ≤ 1e-3；重定向时速度连续；空闲时不请求帧；200 个变换弹簧每帧 ≤ 2ms（Release，UI 线程）。
  - S0.7：`Spring*Transition` 在多个控件共享主题实例时安全，首帧不播放动画，Off 等级立即到终态（按修订后的方案验证）。

## 关联文档

- [动效系统总览](../architecture/motion/overview.md)
- [弹簧引擎](../architecture/motion/spring-engine.md)
- [ControlTheme 集成](../architecture/motion/transitions-integration.md)
- [减少动效](../architecture/motion/reduced-motion.md)
