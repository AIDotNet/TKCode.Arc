# 媒体（media）

> Arc 中以图片与卡片为主体的浏览组件：前后对比、横向轮播与逐张决定的卡片堆，都以跟手拖拽和按速度落定为核心手感。

## 1. 大类定位
- 以媒体内容为主体的浏览与决定：拖动分隔线比较两张图（ImageCompare），横向浏览一小组同类幻灯片（Carousel），以及一次处理一张卡片的分拣（CardStack）。
- 边界：列表行上的快捷操作归 [手势](../gestures/overview.md)（SwipeActions）；分割实时内容而不是图片归 [展开与滚动](../expand/overview.md)（ResizablePanels）；需要并排比较的记录归 [表格与数据](../tables/overview.md)（SortableDataTable）；在轨道上选取数值归 [滑块](../sliders/overview.md)（Slider）。
- 共同特征：都以拖拽物理为核心（跟手、按释放速度落定或抛出）；每个手势都有等价的键盘与按钮路径；触摸时只在横向拖动时接管，纵向滑动照常滚动页面；尺寸随容器而不是窗口变化。

## 2. 组件一览
| 组件 | 类名 | Arc 页面 | 阶段 | Arc 依赖 | 建议基类 | 原语 |
|---|---|---|---|---|---|---|
| Image compare | ImageCompare | [image-compare](https://uiarc.dev/components/image-compare) | P7 | 无 | TemplatedControl | P12 P13 P16 P24 P29 |
| Carousel | Carousel | [carousel](https://uiarc.dev/components/carousel) | P7 | 无 | SelectingItemsControl | P7 P12 P22 P25 P27 P28 |
| Card stack | CardStack | [card-stack](https://uiarc.dev/components/card-stack) | P7 | 无 | ItemsControl | P8 P12 P28 |

## 3. 共享约定
- 拖拽：三者都以 P12 为核心，手势识别、方向判定与释放速度计算走 S10；触摸时等待横向意图再接管（ImageCompare 明确等待横向拖动，Carousel 与 CardStack 保留纵向页面滚动），落定或抛出由 S1 弹簧带着释放速度完成。
- 等价操作：ImageCompare 用方向键移动分隔线，Carousel 用方向键、指示器与箭头按钮，CardStack 用方向键、按钮与撤销快捷键；手势从来不是唯一途径。
- 角色描述：Carousel 与 CardStack 用 roledescription（"carousel"、"card stack"）说明自定义控件，Avalonia 通过重写 AutomationPeer 的 `GetLocalizedControlTypeCore` 提供；各自的切换与决定经 Polite 区域播报。
- 尺寸：都按自身容器宽度布局（ImageCompare 全宽加固定宽高比，默认 3 / 2；Carousel 幻灯片 min(80% 容器宽, 340px)；CardStack 舞台 min(100%, 20rem)），由 S5 观察并切换紧凑形态。
- 挂载：ImageCompare 两张图常驻叠放，Carousel 所有幻灯片常驻（非活动项 inert），CardStack 只渲染顶部 4 张；图片内容统一用 AtomUI 的 ImageLoader / AsyncImage 加载并按画框尺寸解码。
- 动效降级：Reduced 保留必要的指针跟随，去掉弹簧落定、自动轮播与装饰性位移（ImageCompare 直接到位，Carousel 直接跳页并停用轮播，CardStack 原地淡出淡入）；Off 立即终态。

## 4. 组件

### 4.1 ImageCompare（Image compare）
`slug: image-compare` · 阶段 P7 · Arc 依赖：无 · 建议基类：TemplatedControl

**Arc 行为摘要**：在两张叠放的图片上拖动分隔线，查看前后差异。用于修图、改版、清理结果等前后对比，`orientation` 设为 vertical 时改为上下对比。浏览多张图片用 Carousel，分割实时内容而不是图片用 ResizablePanels。两张图应使用相同的裁剪与尺寸，揭示才能对齐。

**Arc API 要点**：
| Arc 属性 | 类型 / 默认值 | 建议 Avalonia 属性（类型） |
|---|---|---|
| `before` / `after`（必填） | ReactNode，左或上为原图，右或下为结果 | `Before` / `After`（object，内容可为 Image 或任意控件） |
| `position` / `defaultPosition` / `onPositionChange` | 百分比 / `50` | `Position`（double，0 到 100，双向）+ `PositionChanged` 事件（分隔线落定时触发） |
| `orientation` | `"horizontal"` | `Orientation`（Avalonia `Orientation`，切换时分隔线摆动过去） |
| `labels` | `["Before", "After"]` 或 false | `BeforeLabel` / `AfterLabel`（string）+ `IsLabelVisible`（bool） |
| `label` | `"Before and after"` | `AutomationProperties.Name`（分隔线手柄的名称，默认本地化） |
| `aspectRatio` | `"3 / 2"` | `AspectRatio`（double，默认 1.5） |

**交互与键盘**：
- 方向键移动 1%，按住 Shift 移动 10%；PageUp / PageDown 移动 10%；Home / End 显示全部原图或全部结果；双击手柄回到中间。
- 整个画面任意位置都能拖动；触摸时先等待横向拖动意图再移动分隔线，单击直接跳到点击处。

**可访问性**：
- 手柄是可聚焦的 Slider（`IRangeValueProvider`，带方向），值文本形如 "40% after"。
- 说明 chip 为 Raw；两张图都需要有意义的替代文本（各自的 `AutomationProperties.Name`）。

**动效**：
- 分隔线跟随指针，松手后按释放速度落定（P12；smooth）；拖动时手柄拉伸为胶囊（P24；snappy）。
- 键盘在边缘继续按时给一个小的踢动（P13；morph）；分隔线到达说明 chip 时 chip 淡出（fast）。
- 结果图按分隔线位置裁剪揭示（P29），裁剪与分隔线由同一个动效值驱动。
- Reduced：分隔线与手柄不带弹簧直接到位；Off：立即终态。

**Avalonia 实现要点**：
- 两张图常驻叠放，结果层的裁剪由 Core 的 `ClipReveal`（P29）按 `MotionValue` 更新，拖动中不触发布局（S1、S2）；手柄拉伸与边缘踢动复用 `ThumbStretch`（P24）与 `LimitFeedback`（P13）。
- 全宽、固定宽高比，宽度变化走 S5；方向判定、单击跳转与释放速度走 S10 的 `DragTracker` / `VelocityTracker`；双击手柄回中。
- 图片用 AsyncImage / ImageLoader，按画框尺寸解码，避免大图常驻内存。

**AtomUI 参考**：—（AtomUI 无同类控件）

**风险与未决**：
- `PositionChanged` 只在落定时触发（Arc 语义），双向绑定的 `Position` 在拖动过程中是否实时回写需在规格中确定。
- 切换方向时"分隔线摆动"的具体形式文档未细述，可能涉及 P16 几何形变（主数据未列），需对照源码确认。
- 复核结论（汇总评审）：新增 P16，已同步到[组件总清单](../overview.md)。

### 4.2 Carousel（Carousel）
`slug: carousel` · 阶段 P7 · Arc 依赖：无 · 建议基类：SelectingItemsControl

**Arc 行为摘要**：居中的幻灯片行，可以拖动、甩动、触控板横扫、方向键或胶囊指示器浏览，可选自动轮播。用于一小组同类项目，两侧露出的邻居暗示还有更多。逐张做决定用 CardStack；除非确有必要，自动轮播应保持关闭。

**Arc API 要点**：
| Arc 属性 | 类型 / 默认值 | 建议 Avalonia 属性（类型） |
|---|---|---|
| `children`（必填） | 每个子元素一张幻灯片 | `Items` / `ItemsSource`（继承），列表会变时每项需要稳定 key |
| `label`（必填） | string | `Label`（string，轮播区域的可访问名称） |
| `index` / `defaultIndex` / `onIndexChange` | `0` | `SelectedIndex`（继承，双向）+ `SelectionChanged`（继承） |
| `slideSize` | `"min(80cqw, 340px)"` | `SlideWidthRatio`（double，0.8）+ `MaxSlideWidth`（double，340），按控件自身宽度计算 |
| `slideLabel` | `(index, count) => string`，默认 "2 of 5" | `SlideLabelFormatter`（`Func<int, int, string>?`，默认本地化格式） |
| `interval` / `autoplay` | 无 / `false` | `Interval`（TimeSpan?，设置后出现播放控件）/ `IsAutoPlay`（bool） |

**交互与键盘**：
- ←/→ 上一张或下一张；Home / End 到首末张。
- Tab 依次到幻灯片区、播放控件、当前指示器与箭头按钮。

**可访问性**：
- 根部角色描述为 "carousel"；幻灯片是带名称的 TabItem 面板，指示器是 Tab 列表；切换经 Polite 区域播报。
- 非活动幻灯片相当于 inert：从焦点链、命中测试与 Automation 树中排除；鼠标悬停、键盘焦点进入或拖动时暂停轮播，并提供可见的暂停控件。

**动效**：
- 幻灯片跟随手指并带动量落定（P12；smooth），键盘或按钮切换时方向滑动（P25；smooth）。
- 邻居按距离缩小并变暗（P28、P27；随轨道位置连续映射，不另起弹簧）。
- 指示器拉伸为跟随幻灯片的胶囊（P7；morph），并以排空表示本张剩余的轮播时间（P22）。
- Reduced：直接跳到目标幻灯片并停用自动轮播；Off：立即终态。

**Avalonia 实现要点**：
- 自建面板加变换，不使用 Avalonia Carousel 的 PageTransition 模型（与跟手拖动不符）；所有幻灯片常驻。
- 自动轮播用 S12 的 `PausableTimer`（`PauseWhenHidden`），离开视口（S6）或窗口不可见时暂停（Arc 用 IntersectionObserver 与 visibilitychange），指示器排空由其进度驱动；拖动与动量走 S10（Carousel 的抛掷阈值为 360px/s）。
- 幻灯片尺寸相对控件宽度（对应 Arc 的容器单位 cqw），控件宽度小于 340px 时控件收紧。

**AtomUI 参考**：Carousel（仅供学习行为与边界，不复用其实现）

**风险与未决**：
- 触控板横扫对应 PointerWheelChanged 的水平增量，需与动量落定和吸附协调。
- inert 在 Avalonia 没有直接等价，需要同时处理焦点、命中与 Automation 三处。

### 4.3 CardStack（Card stack）
`slug: card-stack` · 阶段 P7 · Arc 依赖：无 · 建议基类：ItemsControl

**Arc 行为摘要**：一次审阅一张卡片的牌堆，向左或向右滑动做出决定，可以撤销、可以重置。用于入围筛选、审阅队列、入门偏好选择等逐个分拣的场景。需要并排比较时用列表或 SortableDataTable，只浏览不做决定用 Carousel，列表行的快捷操作用 SwipeActions。

**Arc API 要点**：
| Arc 属性 | 类型 / 默认值 | 建议 Avalonia 属性（类型） |
|---|---|---|
| `items` / `getKey` / `renderCard`（必填） | `T[]` / `(item) => string` / `(item) => ReactNode` | `ItemsSource` / `ItemTemplate`（继承），项默认按引用识别，可选 `KeySelector` |
| `getLabel`（必填） | `(item) => string`，如 "Lisbon" | `LabelBinding`（`IBinding`，用于卡片名称与播报） |
| `onDecide` | `(item, decision)`，decision 为 left 或 right | `Decided` 事件（Item、Decision：Left、Right） |
| `onUndo` / `onReset` | 回调 | `DecisionUndone` 事件（Item、Decision）/ `StackReset` 事件 |
| `labels` / `outcomes` | `{ left: "Pass", right: "Keep" }` / 无 | `LeftLabel` / `RightLabel`（按钮与拖动印章文字）+ `LeftOutcome` / `RightOutcome`（播报用过去式，如 "skipped"） |
| `label` / `renderEmpty` | `"Cards"` / 默认 Start over 按钮 | `AutomationProperties.Name` / `EmptyTemplate`（全部决定后显示，可触发重置） |

**交互与键盘**：
- 牌堆获得焦点时 ←/→ 把顶部卡片判为左或右。
- Backspace、Cmd + Z 或 Ctrl + Z 撤销上一次决定；按钮镜像每个手势。

**可访问性**：
- 舞台是可聚焦 Group，角色描述为 "card stack"，附带说明拖动与按键的隐藏提示；每张卡名称形如 "Lisbon, 1 of 3"。
- 决定、撤销与重置经 Polite Status 区域播报；按钮镜像每个手势，滑动从来不是必需操作。

**动效**：
- 顶部卡片跟随拖动并倾斜、显示印章；甩动或拖得足够远时带速度抛出（P12；smooth）。
- 下面的卡片升到位（P28；morph）。
- 撤销时卡片从离开的位置飞回（P8；morph）。
- Reduced：卡片在原位淡出淡入，不移动；Off：立即终态。

**Avalonia 实现要点**：
- 舞台只渲染顶部 4 张卡，不论牌堆多长；拖动与抛出由 `MotionValue` 驱动，没有逐帧布局（S2、S10）。
- 被抛出的卡片在动画结束前需保活（S3），并允许画到舞台边界之外（`ClipToBounds=false` 或提升到 AdornerLayer）；下层卡片用 `StackDepthPanel`（P28），撤销飞回用 `SharedElementScope`（P8），决定历史栈支撑撤销。
- 舞台宽 min(100%, 20rem)；小于 26rem 时卡片变矮，小于 22.5rem 时撤销按钮只显示图标（S5）。

**AtomUI 参考**：—（AtomUI 无同类控件）

**风险与未决**：
- 撤销"从离开处飞回"需要记录每次抛出的终点与速度，并与 Presence 保活配合。
- 卡片内容是任意模板，倾斜与缩放状态下的文字清晰度需验证。

## 5. 实现顺序与依赖
- 三者都在阶段 P7（手势、物理、计时与反馈），与 Gestures、Sliders、ReorderableList 共用同一批基础：P12 拖拽物理（首个使用者 UserMenu，阶段 P5）与 S10 手势。
- 其余原语都已更早落地：P24 随 Switch（阶段 P0），P29 随 Button（阶段 P0），P7、P25 随 Calendar（阶段 P1），P27、P28 随 AvatarGroup（阶段 P2），P8 随 Card（阶段 P5），P13 随 PasswordStrength 与 P22 随 NumberField（阶段 P6）。
- 建议顺序：ImageCompare（单轴拖动、裁剪与边界回弹，最简单）→ Carousel（加动量吸附、堆叠纵深与可暂停的自动轮播，需要 S12）→ CardStack（加抛出、Presence 保活与撤销的共享元素飞行）。
- 跨大类：Carousel 的胶囊指示器与 Tabs、SegmentedControl 的滑动指示器（P7）同源；CardStack 的抛出手感与 SwipeActions 相近，可共享 S10 的速度判定参数。

## 6. 相关文档
- [组件总清单](../overview.md) · [规格模板](../spec-template.md)
- [动效原语目录](../../architecture/motion/primitives-catalog.md) · [Token 目录](../../architecture/theming/token-catalog.md)
- [浮层与输入](../../architecture/overlays-and-input.md) · [路线图](../../roadmap/overview.md)
- 相邻大类：[手势](../gestures/overview.md) · [展开与滚动](../expand/overview.md) · [滑块](../sliders/overview.md)
