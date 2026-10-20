# 术语表

> Arc（React/CSS/Motion）、Avalonia 与 AtomUI 三套体系的术语对照。设计文档中的术语以本表为准。

## 1. 产品与范围

| 术语 | 含义 |
|---|---|
| Arc | uiarc.dev 上的 React 组件与区块库。本项目只移植其免费组件。 |
| AtomUI | Ant Design 6 风格的 Avalonia 组件库，提供主题、Token、注册、浮层等基础设施。 |
| TKCode.Arc | 本项目：基于 AtomUI 的扩展库，复刻 Arc 的风格与动效。 |
| 大类 | Arc 网站侧边栏的分组（Buttons、Gestures…），共 22 个；对应代码中的一级目录。 |
| 组件 | 一个公开控件及其附属类型，对应大类下的一个目录。 |
| Pro 组件 | Arc 的付费组件，受许可约束，明确排除。 |
| 阶段（P0–P9） | 路线图中的实现批次，见 [路线图](roadmap/overview.md)。 |

## 2. 主题与 Token

| 术语 | Arc | Avalonia / AtomUI | 说明 |
|---|---|---|---|
| 设计 Token | CSS 变量（`--accent`） | AtomUI Design Token | 命名的设计值 |
| 全局 Token | `:root` 变量 | AtomUI `DesignToken`（Seed / Map / Alias） | AtomUI 的全局 Token 集是封闭的，外部包不能扩展 |
| ArcDesign Token | 语义变量（background、surface…） | 载体控件 `ArcDesign` 的 own token | Arc 全局语义 Token 的承载方式，见 [载体 Token](architecture/theming/arc-design-token.md) |
| Own token / 控件 Token | 组件 CSS module 里的局部值 | `[ControlDesignToken]` 类（如 `ButtonToken`） | 只存控件专属值 |
| accent | `data-accent` | ArcDesign identity 上的 `ColorPrimary` | 8 个预设：neutral、violet、blue、green、amber、orange、coral、rose |
| 外观（appearance） | `data-theme="dark"` | AtomUI 主题算法 Default / Dark，Avalonia `ThemeVariant` | 明 / 暗 |
| 作用域 | 元素上的 `data-accent` | `ThemeConfigProvider`（`ThemeVariantScope`） | 子树局部主题 |
| identity | — | `ControlTokenIdentity`（Catalog + Id） | 生成器为每个有 own token 的控件生成；Arc 的 Catalog 为 `TKCode.Arc` |

## 3. 控件结构

| 术语 | 含义 |
|---|---|
| ControlTheme | Avalonia 的控件主题：模板 + 样式。Arc 每个控件一个 `XxxTheme.axaml`。 |
| 模板部件（template part） | ControlTemplate 中具名、被代码访问的元素（`PART_Xxx`）。 |
| 语义部件（Semantic Part） | AtomUI 的公开定制入口：控件声明 `[SemanticPart]`，生成器产出专用 Style 类。Arc 的 Path 一律以 `arc.` 开头。 |
| presenter | 模板内部使用的 internal 控件（如 `SwitchThumb`），没有自己的 identity。 |
| 包入口 | `UseArcCore()` / `UseArc()`：在 `UseAtomUI(...)` 中显式启用 Arc。 |

## 4. 动效

| 术语 | Arc / Motion | TKCode.Arc | 说明 |
|---|---|---|---|
| 弹簧（spring） | `{ type: "spring", visualDuration, bounce }` | `SpringSpec` | 由视觉时长与回弹换算出刚度 / 阻尼，见 [弹簧引擎](architecture/motion/spring-engine.md) |
| 补间（tween） | `{ duration, ease }` | `TweenSpec` | 固定时长 + 三次贝塞尔缓动 |
| 预设 | `smooth`、`snappy`、`morph` | `ArcMotionPresets` | Arc 实际使用的三个弹簧预设 |
| 动效值 | motion value | `MotionValue<T>` | 可打断、重定向时保留速度的代码驱动动画值（声明式 `Spring*Transition` 不保留速度） |
| 帧时钟 | requestAnimationFrame | `TopLevel.RequestAnimationFrame` / `IFrameClock` | 逐帧驱动 |
| 退场保活（presence） | `AnimatePresence` | `PresenceHost` | 元素退场动画结束前不移除 |
| 布局动画 | `layout` / `layoutId` | `LayoutAnimator`（FLIP）、`SharedElementScope` | 位置变化与共享元素飞行 |
| 动效等级 | `prefers-reduced-motion` | `ArcMotionLevel`：Full / Reduced / Off | 见 [减少动效](architecture/motion/reduced-motion.md) |
| 原语（P1–P30） | — | Core 中的可复用动效构件 | 见 [原语目录](architecture/motion/primitives-catalog.md) |
| 服务（S1–S12） | — | Core 中的基础服务 | 同上 |

## 5. 构建与注册

| 术语 | 含义 |
|---|---|
| CPM | Central Package Management，`Directory.Packages.props` 统一管理版本。 |
| buildTransitive | NuGet 包中随依赖传递的 MSBuild 文件；AtomUI 用它把生成器与注册 targets 带给消费者。 |
| 生成器 | AtomUI.Generator（Roslyn）：生成 Token 键、identity、语义部件 Style、包注册清单。 |
| ATOMUIGEN / ATOMUIREG | AtomUI 生成器 / 注册链路的诊断前缀，例如 ATOMUIGEN037（语义部件 Style 类型冲突）、ATOMUIREG014（语言覆盖不全）。 |
| TypeMap | .NET 10+ 的类型映射 API；AtomUI 用它在裁剪 / AOT 发布时保留注册内容。 |
