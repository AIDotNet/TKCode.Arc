# 动效原语与服务目录

> Arc 110 个组件中反复出现的动效模式，提炼为 30 个原语（P1–P30）与 12 个基础服务（S1–S12）。原语随**首个使用它的组件**落地，后续组件复用。

## 1. 规则

- 原语在 Core 中实现一次，组件不各自重写。
- 落地阶段 = 使用该原语的组件中最早的阶段；首批使用者与原语一起验收。
- 组件规格的"动效表"必须引用原语编号；需要新原语时，先更新本目录，再写组件规格。
- 使用组件列表的单一来源是 [组件总清单](../../components/overview.md) 中每个组件的"原语"列；本目录给出统计与落地阶段。
- 统计来源：Arc 目录（catalog.json）中每个组件的 motion / performance 描述与源码扫描，快照日期 2026-10-20。

## 2. 动效原语（P1–P30）

| 编号 | 名称 | 说明 | Core 类型 | 主要 Token | 组件数 | 落地 | 首批使用者 |
|---|---|---|---|---|---|---|---|
| P1 | 按压反馈 | 按下缩放并弹回；作为弹层锚点的元素改用颜色响应 | `SpringTransformOperationsTransition` + 按压缩放规则 | 按下 instant + standard，松开 snappy；缩放 0.97（宽 ≤ 48px 为 0.96，宽 > 220px 为 0.985） | 15 | P0 | Button |
| P2 | 模糊交叉淡化 | 固定位置的内容切换：旧内容上移淡出，新内容从下方 4px、4px 模糊中升起；图标从 0.6 缩放与 2px 模糊中出现 | `BlurSwapTransition`（PresenceHost 转场） | 进场 standard + enter，退场 fast + standard，BlurSoft / BlurSubtle，图标缩放 snappy | 37 | P0 | Button |
| P3 | 方向滚动切换 | P2 的方向版本：新值更大或更靠后时向上滚入，否则向下 | `DirectionalRollTransition` | 同 P2，加 BlurSoft | 20 | P1 | Calendar |
| P4 | 字形形变 | 只动画变化的字符：共享字符滑到新位置，新字符从模糊中交错出现，删除的字符淡出 | `GlyphMorphPresenter` | 位置 morph，StaggerChar × 2，BlurSoft，退场 exit | 8 | P1 | CopyButton、TextMorph |
| P5 | 宽度随内容弹簧 | 宽度随新内容弹簧变化，或用隐形占位预留最宽标签；被动尺寸变化直接跳变 | `SpringSizeDecorator`（Width） | 增长 morph，部分组件收缩用 smooth | 26 | P0 | Button |
| P6 | 高度展开收起 | 高度在 0 与内容高度之间动画并带透明度，可中途反向；内容按最终宽度布局；展开箭头旋转 | `SpringSizeDecorator`（Height）、`DisclosureChevron` | smooth（折叠常用 0.28–0.36s），exit + exit，箭头 snappy | 35 | P2 | CodeBlock |
| P7 | 滑动指示器 | 一个指示器元素在各项之间弹簧移动位置与尺寸；首次出现时原地淡入；容器尺寸变化时直接吸附 | `GlidingIndicator` | 选择指示 morph；悬停高亮：指针 snappy，键盘立即 | 24 | P1 | Calendar |
| P8 | 共享元素飞行 | 元素在不同容器之间飞行 | `SharedElementScope` | morph（Card 用 smooth 0.3 / 0.26s） | 5 | P5 | Card |
| P9 | 表面形变 | 一个表面从触发器变为面板：宽、高、圆角、位置一起弹簧变化，内容交叉淡化 | `MorphSurface` | 展开带轻微回弹，收回用 smooth（无回弹） | 11 | P4 | ExpandableCard |
| P10 | 锚定弹层进出 | 以锚点为原点的缩放、位移与淡入；退场更快且可中途反向；可选项目交错；预热组 | `AnchoredPresence`（基于 AtomUI Popup） | ContextMenu snappy，DropdownMenu 用 CSS 弹簧曲线（`ArcTween.Spring`：580ms + `ArcEasings.CssSpring`），UserMenu / HoverCard smooth，退场 exit，StaggerItem | 22 | P1 | Tooltip |
| P11 | 模态与边缘呈现 | 遮罩淡入（可带背景模糊），面板升起或从边缘滑入 | `ModalPresence`（遮罩、焦点陷阱） | smooth；退场为更短的补间；BackdropBlur* | 7 | P5 | UserMenu、Dialog、Drawer、BottomSheet、Card |
| P12 | 拖拽物理 | 1:1 跟随（不逐帧重建可视树），越界橡皮筋，带速度松开，投影到吸附点或抛出 | `DragTracker`、`RubberBand`、`MomentumProjector`（S10） | 回位 smooth（物理形式） | 17 | P5 | UserMenu、Drawer、BottomSheet |
| P13 | 极限反馈 | 到达边界或输入无效时的拉伸、抖动或踢动 | `LimitFeedback` | 踢动用 morph（物理形式），抖动用专用弹簧 | 10 | P6 | PasswordStrength、NumberField、MoneyInput、ColorPicker |
| P14 | 滚动数字 | 逐位里程表滚轮，或数字补间 | `RollingDigits`（S11） | 数值变化 smooth，列宽 morph，首次揭示 StaggerItem + considered | 29 | P1 | AnimatedCounter |
| P15 | 描边绘制 | 路径从 0 绘制到完整长度 | `StrokeDrawPresenter` | fast / standard + enter，或 snappy；图表用更长的无回弹弹簧 | 21 | P1 | CopyButton |
| P16 | 路径与几何形变 | 形状插值；图表先重采样到共享网格，再从当前形状形变 | `PathMorph`（重采样） | smooth（物理形式）；图表更长（Ridgeline 0.7s、Treemap 0.62s） | 13 | P3 | IconMorph、Checkbox |
| P17 | 图表指针 | 十字线滑动；提示框用独立弹簧跟随，靠近边缘时翻转；非活动项变暗 | `PointerTracker`（图表基础） | 跟随弹簧，snappy | 11 | P8 | 全部 P8 图表 |
| P18 | 进入视口首次揭示 | 只运行一次的交错波 | `RevealOnView`（S6） | StaggerItem，inOut；触发比例 40%–60% | 17 | P1 | AnimatedCounter |
| P19 | 列表进出与重排 | 只动画位置的布局动画，配合交错与补位 | `PresenceItemsControl` + `LayoutAnimator` | 滑动 morph，补位 smooth，StaggerItem | 27 | P2 | DatePicker、AvatarGroup |
| P20 | 交错文字揭示 | 词、行或字从裁剪区升起，并从模糊变清晰 | `StaggeredText` | StaggerWord / Line / Char，BlurText，considered | 4 | P3 | TextReveal、InViewTitle |
| P21 | 持续循环 | 只在活动、可见、在视口内时运行；减少动效下停止 | `LiveLoop`（Composition，受可见性约束） | 各组件自定（如加载圈 0.7s 线性） | 12 | P0 | Button（加载圈） |
| P22 | 可暂停时间线 | 驱动填充与计时，可暂停、继续，并用 smooth 回退 | `MotionTimeline`（S12） | smooth（回退） | 6 | P7 | ConfirmMorph、HoldToConfirm、ToastStack、AnnouncementBar、Countdown、Carousel |
| P23 | 弹簧填充 | 填充与它的数值标签使用同一个 smooth 弹簧 | `SpringFill` | smooth | 7 | P3 | Progress、UsageMeter |
| P24 | 拇指拉伸 | 按下时拇指形变：向目标方向拉伸（Switch，宽度用 snappy）或膨胀（ElasticSlider 放大到 1.28 倍）；松开后拇指位移用 smooth 族弹簧（Switch 为 0.3s、bounce 0） | `ThumbStretch` | 形变 snappy，位移 smooth 族 | 3 | P0 | Switch |
| P25 | 方向滑动 | 内容沿行进方向滑入 | `DirectionalSlideTransition` | smooth / morph，位移 8px | 9 | P1 | Calendar |
| P26 | 弹出 | 从 0.6–0.94 的缩放与模糊中放大出现 | `PopInTransition` | 缩放 snappy，透明度与模糊 fast | 25 | P1 | Avatar |
| P27 | 强调 | 悬停抬起并让其余项变暗（只针对精确指针），或点按触发的速度冲量（Reactions，k=420、c=17，连点可叠加） | `Emphasis` | snappy；冲量用物理参数 | 11 | P2 | AvatarGroup |
| P28 | 堆叠纵深 | 分层露出与逐级缩放 | `StackDepthPanel` | smooth / morph | 4 | P2 | AvatarGroup |
| P29 | 裁剪与遮罩揭示 | 用动画裁剪几何或不透明度遮罩揭示内容 | `ClipReveal` | 各组件自定 | 18 | P0 | Button（宽度形变时裁剪标签槽） |
| P30 | 滚动跟随 | 滚动位置随内容变化弹簧跟随 | `ScrollLinked` | smooth | 5 | P4 | Tabs、ScrollArea、ExpandableText |

## 3. 基础服务（S1–S12）

| 编号 | 名称 | Core 类型 | 详细文档 | 落地 | 首批使用者 |
|---|---|---|---|---|---|
| S1 | 弹簧引擎 | `SpringSpec`、`SpringSolver`、`ArcMotionPresets` | [spring-engine.md](spring-engine.md) | P0 | Button、Switch |
| S2 | 动效值 | `MotionValue<T>`、`MotionScheduler`、`IFrameClock` | [values-and-scheduler.md](values-and-scheduler.md) | P0 | Button、Switch |
| S3 | Presence | `PresenceHost`（单内容）、`PresenceItemsControl`（列表） | [presence-and-layout.md](presence-and-layout.md) | P0（PresenceHost）/ P2（PresenceItemsControl，随 P19） | Button（标签交叉淡化）/ DatePicker、AvatarGroup |
| S4 | 动效等级 | `ArcMotion.Level`、`GetEffectiveLevel`、`MotionMode` Token | [reduced-motion.md](reduced-motion.md) | P0 | 全部 |
| S5 | 尺寸观察 | `SizeObserver` | [../primitives.md](../primitives.md) | P0 | Button |
| S6 | 视口可见性 | `ViewportVisibility`（基于 `EffectiveViewportChanged`） | [../primitives.md](../primitives.md) | P1 | AnimatedCounter |
| S7 | 键盘焦点环 | `FocusRing`（基于 Avalonia `:focus-visible`） | [../accessibility.md](../accessibility.md) | P0 | Button、Switch |
| S8 | 浮层 | `AnchoredOverlay`（AtomUI `Popup`）、`ModalOverlay`（`ScopeAwareOverlayLayer`） | [../overlays-and-input.md](../overlays-and-input.md) | P1（锚定）/ P5（模态） | Tooltip / Dialog |
| S9 | 列表键盘导航 | `ListKeyboardNavigator` | [../primitives.md](../primitives.md) | P1 | Calendar |
| S10 | 手势 | `DragTracker`、`VelocityTracker`、`RubberBand`、`MomentumProjector` | [../primitives.md](../primitives.md) | P5 | Drawer、BottomSheet、UserMenu |
| S11 | 数字与日期格式化 | `ArcFormatting` | [../localization.md](../localization.md) | P1 | AnimatedCounter、Calendar |
| S12 | 可暂停计时器 | `PausableTimer` | [../primitives.md](../primitives.md) | P1 | CopyButton、Tooltip |

## 4. 落地顺序总览

| 阶段 | 新落地的原语与服务 |
|---|---|
| P0 | S1、S2、S3（PresenceHost）、S4、S5、S7；P1、P2、P5、P21、P24、P29 |
| P1 | S6、S8（锚定）、S9、S11、S12；P3、P4、P7、P10、P14、P15、P18、P25、P26 |
| P2 | P6、P19、P27、P28 |
| P3 | P16、P20、P23 |
| P4 | P9、P30 |
| P5 | S8（模态）、S10；P8、P11、P12 |
| P6 | P13 |
| P7 | P22 |
| P8 | P17 |

P7、P9 与 P10 的首批使用者恰好也是这些原语最复杂的使用者之一（Calendar 的选择指示、ExpandableCard 的表面形变、Tooltip 的锚定弹层），落地时以通用 API 设计，不针对首个组件特化。
