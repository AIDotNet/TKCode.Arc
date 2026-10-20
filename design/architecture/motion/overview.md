# 动效系统总览

> TKCode.Arc 的动效目标、三层模型、选型决策表、与 AtomUI 的边界、动效等级策略以及性能与生命周期规则。决策见 [ADR 0006](../../decisions/0006-motion-engine.md)。

## 1. 保真目标

- **与 Arc（Motion / framer-motion）等价**：同样的弹簧参数得到同样的位移曲线；代码驱动的 `MotionValue<T>` 在中途改变目标时位置与速度连续，手势松开时继承手指速度；ControlTheme 中声明的弹簧过渡曲线一致，但被打断时从零速度重启（Avalonia 限制，见 [ControlTheme 集成](transitions-integration.md)）。
- **继承 Arc 的动效原则**（来自 Arc `motion.md`）：
  1. 只对 transform、opacity 与短暂模糊做动画；只有尺寸本身就是信息时才对宽高做动画，并且用弹簧。
  2. 一次交互一个连续动作，不要"先淡入、再滑动、再缩放"。
  3. 一切可打断；用户可逆的动作用弹簧。
  4. 尺寸不在一帧内突变：要么动画，要么预留空间。
  5. 数字滚动到新值，使用等宽数字与稳定宽度。
  6. 按压缩放到约 0.97；但作为弹层或菜单锚点的元素只用颜色响应（弹层按按下后的尺寸定位）。
  7. 退场快于进场。
  8. 每页最多一个编排过的入场动画；不给每个区块加滚动揭示；除非表示实时状态，否则不循环。
  9. 动画元素保持挂载、键稳定、不切换元素类型；重新挂载会让动画重启、焦点丢失。
- **首帧不动画**：控件首次显示与主题初始应用时不播放过渡。
- **每个动画都有减少动效分支**（见 [减少动效](reduced-motion.md)）。

## 2. 三层模型

| 层 | 用途 | 机制 | 写在哪里 |
|---|---|---|---|
| L1 声明式状态过渡 | 伪类 / 属性变化引起的颜色、透明度、位移、缩放 | `Spring*Transition`、`ArcSpringEasing`、Avalonia / AtomUI 的颜色过渡 | ControlTheme 的 `Transitions` setter（AtomUI 规则：只能写在 style setter 中） |
| L2 命令式动效值 | 手势跟随、拖拽惯性、连续驱动、需要读速度的场景 | `MotionValue<T>` + `MotionScheduler` | 控件代码；注释说明为什么 ControlTheme 无法表达 |
| L3 编排服务 | 进场 / 退场保活、列表重排（FLIP）、共享元素飞行、尺寸弹簧 | `PresenceHost`、`PresenceItemsControl`、`LayoutAnimator`、`SharedElementScope`、`SpringSizeDecorator` | 模板中使用 Core 提供的基元 |

## 3. 选型决策表

| 场景 | 选择 |
|---|---|
| 悬停 / 按下的颜色变化 | L1：`SolidColorBrushTransition`，时长 `DurationFast`，缓动 `EaseStandard` |
| 按压缩放 | L1：`SpringTransformOperationsTransition`（按下 instant + standard，松开 snappy） |
| 开关拇指、指示器在状态之间移动 | L1：`SpringDoubleTransition` / `SpringPointTransition`（smooth / morph） |
| 拖拽、滑动、拉伸、惯性抛掷 | L2：`MotionValue<T>` + 手势服务（S10） |
| 元素出现 / 消失 | L3：`PresenceHost`（退场保活） |
| 列表增删、排序 | L3：`PresenceItemsControl` + `LayoutAnimator` |
| 高亮在不同容器之间飞行 | L3：`SharedElementScope`（`MotionId`） |
| 高度展开收起、宽度随内容变化 | L3：`SpringSizeDecorator` |
| 自绘持续循环（闪光、骨架脉冲、实时线） | `CompositionCustomVisualHandler`，并受可见性约束 |
| 一次性的标准进出场（与弹簧无关） | 可使用 AtomUI MotionScene 的 `AbstractMotion` 预设，但缓动换成 Arc Token |

## 4. 与 AtomUI 的边界

| AtomUI 机制 | Arc 的用法 |
|---|---|
| `IMotionAwareControl` / `IsMotionEnabled` | 未采用。等级由 `arc:ArcMotion.Level`（继承）与 `MotionMode` Token 决定 |
| `DisableTransitions()` / `EnableTransitions()` | 未采用。首帧不动画由 `arc:ArcMotion.Tracks` 伪类方案保证 |
| ControlTheme 中的动效选择器 | 所有 `Transitions` setter 都放在 `^:motion-full` / `^:motion-reduced` 下 |
| `AbstractMotion` / `MotionActor` | 可用于简单进出场；需要弹簧时用 Arc 的 Presence |
| `Popup` 的 `OpenMotion` / `CloseMotion` | `ArcPopup` 把 Arc 的进出场写成 `AbstractMotion` 子类（`ArcPopupEnterMotion` / `ArcPopupExitMotion`）挂在 AtomUI 浮层的 motion actor 上，阴影随表面一起运动 |
| `EnableMotion=false` | 映射为 Arc 等级 Off |

## 5. 动效等级策略（摘要）

- 有效等级 = min(`ArcMotion.Level` 附加属性, `MotionMode` Token, 控件 `IsMotionEnabled`, 可选的系统探针)，顺序为 Full > Reduced > Off。
- **Reduced**：保留终态、焦点与反馈；位移、缩放、尺寸变化改为终态加短淡入淡出；去掉模糊、循环、视差与滚动跟随。
- **Off**：所有过渡立即到终态。
- 用户直接驱动的拖拽在任何等级下都跟随手指；只有松开后的惯性与回弹受等级影响。
- 细则见 [减少动效](reduced-motion.md)。

## 6. 性能与生命周期

- 每个 TopLevel 一个 `MotionScheduler`，批量推进所有活动值；没有活动值时不请求帧。
- 只写 RenderTransform、Opacity、Effect 与 `AffectsRender` 的自绘属性，不触发布局。宽高弹簧（P5 / P6）每帧触发布局，只在尺寸就是信息时使用。
- 帧间隔超过阈值时（例如窗口刚恢复）限制步长，避免弹簧数值爆炸。
- owner 从可视树分离或不可见时，动效值立即跳到目标并停止。
- 持续循环（P21）必须受有效可见性约束：不可见、离屏或窗口最小化时暂停。
- 每个组件规格写明动效预算；Phase 0 的 S0.6 给出引擎基准：200 个变换弹簧每帧 ≤ 2ms。

## 7. 子文档

| 文档 | 内容 |
|---|---|
| [spring-engine.md](spring-engine.md) | `SpringSpec`、`SpringSolver`、与 Motion 的换算、收敛判定、重定向 |
| [values-and-scheduler.md](values-and-scheduler.md) | `MotionValue<T>`、向量类型、调度器、帧时钟、可见性 |
| [transitions-integration.md](transitions-integration.md) | `Spring*Transition`、`ArcSpringEasing` 在 ControlTheme 中的用法 |
| [presence-and-layout.md](presence-and-layout.md) | Presence、FLIP、共享元素、尺寸弹簧 |
| [reduced-motion.md](reduced-motion.md) | 等级、来源组合、分类降级策略 |
| [primitives-catalog.md](primitives-catalog.md) | 动效原语 P1–P30 与服务 S1–S12 的目录与落地阶段 |
| [verification.md](verification.md) | 数值对照、确定性、性能预算 |
