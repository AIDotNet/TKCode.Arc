# 手势（Gestures）

> Arc Actions 分组中依赖按压时长、横向滑动或轻点回应的交互控件：长按确认、行滑动操作与表情回应。

## 1. 大类定位
- 解决"单击不够合适"的场景：后果已在屏幕上可见的危险动作用长按代替确认对话框（HoldToConfirm），触摸优先的分诊列表用横向滑动露出行操作（SwipeActions），对帖子或评论一键回应并让计数滚动（Reactions）。
- 边界：点按即完成的动作属于 Buttons（ConfirmMorph 的 hold 模式与 HoldToConfirm 共享按压计时）；桌面优先的行操作用 Menus 的 ContextMenu / DropdownMenu；纵向拖拽排序属于 Tables 的 ReorderableList；需要先阅读后果的确认用 Overlays 的 Dialog。
- 共同特征：动画直接跟随手指或按压进度（线性时间线、带释放速度的弹簧），中途放弃会从当前进度反向；每个手势都有等价的键盘或菜单路径，手势从不是唯一入口。

## 2. 组件一览
| 组件 | 类名 | Arc 页面 | 阶段 | Arc 依赖 | 建议基类 | 原语 |
|---|---|---|---|---|---|---|
| 长按确认 | HoldToConfirm | [hold-to-confirm](https://uiarc.dev/components/hold-to-confirm) | P7 | 无 | Avalonia Button | P1 P2 P5 P15 P22 P29 |
| 滑动操作 | SwipeActions | [swipe-actions](https://uiarc.dev/components/swipe-actions) | P7 | 无 | ContentControl | P6 P10 P12 P19 |
| 表情回应 | Reactions | [reactions](https://uiarc.dev/components/reactions) | P5 | 无 | ItemsControl | P5 P6 P7 P10 P14 P19 P26 P27 |

## 3. 共享约定
- **指针模型**：统一用指针事件加指针捕获处理鼠标、笔与触摸（S10）；悬停效果只跟随鼠标 / 笔，触摸只有按下期间的反馈。
- **放弃与取消**：长按提前松开、滑动未过阈值都以 smooth 弹簧（不过冲）回到起点并保留当前速度，不存在"半完成"状态；按压类手势在指针移出控件超过 24px 时取消，滑动类手势移动超过 8px 才锁定方向，纵向移动交还给 ScrollViewer。
- **等价路径**：HoldToConfirm 支持按住 Space / Enter；SwipeActions 每行都有 More actions 菜单；Reactions 的药丸与选择器完全可用键盘操作。手势控件的测试计划必须覆盖非手势路径。
- **反馈与播报**：完成结果（Done、移除后的焦点转移、回应变化）经隐藏的 Polite 状态区播报（`AutomationProperties.LiveSetting`），可见文字与播报文字分离。
- **数值来源**：阈值、速度与弹簧参数照抄 Arc 源码（24px、8px、900px/s、刚度 420 / 阻尼 17 等），不属于标准预设的弹簧参数放进各自的 `XxxToken`（见 [控件 Token](../../architecture/theming/control-tokens.md#6-动效相关)），来源在组件规格中注明。
- **桌面限制**：Arc 在触摸设备完成长按时震动 12ms，Avalonia 桌面没有通用触觉 API，不实现。
- **动效等级**：Reduced 去掉跟随位移、回退弹簧与布局弹簧，改为跳到停靠点加短淡入淡出，但功能性进度（长按填充）保留；Off 立即到终态。

## 4. 组件
### 4.1 HoldToConfirm（Hold to confirm）
`slug: hold-to-confirm` · 阶段 P7 · Arc 依赖：无 · 建议基类：Avalonia Button

**Arc 行为摘要**：危险动作只在按住达到时长后才提交，填充跟随按压进度，提前松开会回退；用于后果已在屏幕上可见的删除或撤销、在紧凑设置行里代替确认对话框，以及防止触摸设备上的误触。需要先阅读后果时用 Dialog，安全且易撤销的动作用 Button 或 ActionButton，触摸列表里的删除用 SwipeActions。

**Arc API 要点**：
| Arc 属性 | 类型 / 默认值 | 建议 Avalonia 属性（类型） |
|---|---|---|
| `label`（必填） | string，写成指令，如 "Hold to delete project" | `Label`（string） |
| `onConfirm`（必填） | 按住完成时调用一次 | `Confirmed` 事件 / `Command` |
| `confirmedLabel` | string，默认 "Done" | `ConfirmedLabel`（string，默认值走本地化） |
| `duration` | number，默认 1200ms | `HoldDuration`（TimeSpan） |
| `icon` | 默认 Trash2，完成后换成自绘对勾 | `Icon`（object，默认 Lucide trash-2） |
| `tone` | danger / neutral，默认 danger（填充色） | `Tone`（Danger / Neutral） |
| `confirmed` | boolean，受控完成态，置回 false 复位；不传则自管 | `IsConfirmed`（bool，双向） |
| `onHoldChange` | 按住开始与结束时回调，用于 "Keep holding" 之类的提示 | `IsHolding`（bool，只读）+ `HoldingChanged` 事件 |

**交互与键盘**：鼠标、笔、触摸按住填充，提前松开回退，指针移出按钮超过 24px 取消；键盘按住 Space / Enter 填充、松开回退，Tab 移走焦点取消当前按住。

**可访问性**：
- `AutomationProperties.HelpText` 为隐藏提示 "Press and hold for N seconds to confirm. With a keyboard, hold Space or Enter."（本地化，N 由 HoldDuration 计算）。
- Name 跟随可见标签；完成后 Polite 状态区播报 confirmedLabel，按钮报告不可用但保留焦点；文字绘制两份（底层与填充层各一份），颜色恰好在填充边缘切换，任何进度下都保持对比度。

**动效**：
- 按住时填充沿线性时间线前进，时长为剩余比例乘以 HoldDuration；松开时以 smooth 弹簧（初速 0）回退；快速再按从当前进度继续（P22）。
- 填充层从左侧裁切揭示，层内文字换为填充上的前景色（P29）。
- 按下缩放 0.97（宽于 220px 为 0.985），snappy（P1）；完成时新标签从 soft 模糊上移 0.3em 进入、旧标签带 subtle 模糊离开（P2），宽度 morph 弹簧（P5），图标换成对勾并描边绘入（P15）。
- Reduced：去掉按压缩放与回退弹簧，标签和图标淡入淡出，填充仍跟随按压；Off 只保留填充进度，其余立即。

**Avalonia 实现要点**：
- 模板部件：`PART_Surface`（底层内容）、`PART_Fill`（裁切宽度由进度驱动）、`PART_FillContent`（填充层内容）、`PART_LabelSlot`；伪类 `:holding`、`:confirmed`。
- 覆盖 Button 默认的 Click 时机：Space 抬起或 Enter 按下都不触发 `Click`，改由计时完成触发，并忽略按键自动重复。指针按住用 S10（捕获、24px 取消），进度用 P22 `MotionTimeline`（首个落地于 NumberField，P6），完成后的宽度用 S5。
- 进度由单个线性动效值驱动，不逐帧写 StyledProperty。

**AtomUI 参考**：CircleProgress（参考；仅供学习行为与边界，不复用其实现）。

**风险与未决**：键盘按住依赖 KeyDown / KeyUp 配对，窗口失活时可能丢失 KeyUp，需在 LostFocus 与窗口 Deactivated 时取消；Off 等级下填充是否仍然动画需在动效等级策略中明确（建议保留，它是功能性进度而非装饰）。

**实现说明（已落地）**：派生 Avalonia `Button`，`Tone` 按 Arc 源码默认 Accent（另有 Danger / Neutral），`Icon`（Geometry，默认 Trash2），`IsConfirmed` 双向（置回 false 回退并形变回去），`IsHolding` + `HoldingChanged`，完成时 `Confirmed` 后执行 Click / Command（普通点击与自动化 Invoke 不确认）；进度为 _Shared `ButtonsHoldProgress`（P22 `ButtonsTimeline` 基于 `PausableTimer` 按帧报告，任何动效等级都跟随按压；松开 smooth 回退），填充是圆角层内的第二份面并以可变矩形从左裁切，文字颜色恰在边缘切换；Space / Enter 按住（忽略自动重复）、指针移出 24px / 失焦 / 窗口失活取消，HelpText 与 Polite 播报，触觉反馈未实现。

### 4.2 SwipeActions（Swipe actions）
`slug: swipe-actions` · 阶段 P7 · Arc 依赖：无 · 建议基类：ContentControl

**Arc 行为摘要**：横向滑动行露出左右两侧的操作，完整滑动直接提交最外侧操作并让行滑走、列表合拢；每行另有一个包含相同操作的 More actions 菜单。用于触摸优先的分诊列表（邮件、通知、任务），每侧一到两个快捷操作（Archive、Mark unread）。桌面优先的表格用 ContextMenu 或 DropdownMenu，需要纵向拖拽用 ReorderableList，列表外的单个危险动作用 HoldToConfirm。

**Arc API 要点**：
| Arc 属性 | 类型 / 默认值 | 建议 Avalonia 属性（类型） |
|---|---|---|
| SwipeActionsRow `leading` / `trailing` | `SwipeAction[]`，默认空；最外侧项可完整滑动提交 | `LeadingActions` / `TrailingActions`（`SwipeAction` 集合） |
| SwipeActionsRow `fullSwipe` | boolean，默认 true | `IsFullSwipeEnabled`（bool） |
| SwipeActionsRow `label`（必填）/ children（必填） | 命名行的 More actions 按钮 / 行内容 | `Label`（string）/ `Content`（object） |
| SwipeAction `label` / `icon`（必填） | 显示在图标下方与菜单中 | `SwipeAction.Label` / `Icon` |
| SwipeAction `onSelect`（必填） | 完整滑动、点按或菜单选择时执行，通常在此移除数据项 | `SwipeAction.Command` + `Invoked` 事件 |
| SwipeAction `tone` / `keepRow` | neutral / accent / danger / 执行后行弹回而不滑走 | `Tone`（Neutral / Accent / Danger）/ `KeepsRow`（bool） |
| SwipeActions `label`（必填） | 列表名称；列表同时只开一行 | 宿主列表的 `AutomationProperties.Name`；同组只开一行用 `GroupName`（见风险） |

**交互与键盘**：指针横向拖动行，移动超过 8px 后锁定横轴、纵向移动交还页面滚动，点按露出的操作即执行；Enter / Space 从行的 More actions 按钮打开菜单，ArrowDown / ArrowUp 在菜单项间移动，Escape 关闭菜单或收起已打开的行，外部按压也会收起。

**可访问性**：
- 列表为 List 角色并带 Name，每行为 ListItem；每行都有 `More actions for {Label}` 菜单，鼠标与键盘用户从不需要手势。
- 露出的操作按钮对辅助技术隐藏且不在 Tab 顺序中；移除型操作执行后，焦点移到相邻行。

**动效**：
- 行跟随手指，超过最后停靠点后按橡皮筋阻尼跟随：超出量 o、行宽 d 时位移为 (1 − 1 / (0.55·o / d + 1))·d（P12）。
- 松开时以带释放速度的弹簧停靠：回原位用 smooth，到打开停靠点用 snappy；按释放速度投射 0.1s 的位移，超过 900px/s 视为甩动（P12）。
- 完整滑动阈值取"打开宽度 + 48px"与"行宽 56%"中较大者；越过阈值时最外侧操作以 morph 拉伸铺满整行，提交后行以 smooth 滑出。
- 行移除时高度以 smooth 收起（延迟 120ms），列表合拢（P6、P19）；More actions 菜单沿用 MenuSurface 的进出（P10）。
- Reduced：行直接跳到停靠点，行进出改为淡入淡出、不做高度动画；Off 立即到位。

**Avalonia 实现要点**：
- 模板部件：`PART_LeadingActions`、`PART_TrailingActions`（打开时每个操作宽 76px）、`PART_Content`（RenderTransform 平移）、`PART_MoreButton`；行宽由 S5 观察，用于计算阈值。
- S10 负责轴向锁定、速度采样与指针捕获，锁定横轴后阻止外层 ScrollViewer 的手势识别；拖动直接写动效值，行不重新布局。菜单复用 MenuSurface（S8、S9），移除动画依赖 S3 Presence。

**AtomUI 参考**：Carousel 滑动逻辑（参考；仅供学习行为与边界，不复用其实现）。

**风险与未决**：Arc 的 SwipeActions 是列表容器、SwipeActionsRow 才是行；主数据建议 ContentControl，对应单行。"同时只开一行、外部按压或 Escape 收起"建议用类似 RadioButton 的 `GroupName` 协调，类名分工需在 spec 定稿；行移除后的高度收起需要宿主 ItemsControl 配合 S3（Arc 不虚拟化），虚拟化列表回收容器会丢失打开状态，需按数据项记录。

### 4.3 Reactions（Reactions）
`slug: reactions` · 阶段 P5 · Arc 依赖：无 · 建议基类：ItemsControl

**Arc 行为摘要**：带计数的回应药丸栏加一个打开选择托盘的添加按钮，类似 Slack 的回应；一次点按即可对评论、帖子或发布说明表示认可，计数数字滚动变化。需要说具体内容时应提供回复输入框，不能被修改的静态计数用 Badge。

**Arc API 要点**：
| Arc 属性 | 类型 / 默认值 | 建议 Avalonia 属性（类型） |
|---|---|---|
| `options`（必填） | `ReactionOption[]`：id、label、icon（Lucide 组件类型） | `Options`（`ReactionOption` 集合：Id、Label、Lucide 图标 Kind） |
| `value` / `defaultValue` | `Reaction[]`：id、count、reacted；计数为 0 时移出栏，默认空 | `Value`（`IReadOnlyList<Reaction>`，双向，控件据此生成药丸） |
| `onValueChange` | `(next, change)`，change 含 id 与 reacted | `ValueChanged` 事件（Next、Change） |
| `label` | string，默认 "Reactions" | `Label`（string，默认值走本地化） |

**交互与键盘**：Enter / Space 在药丸上切换自己的回应，在添加按钮上打开选择器；选择器内四个方向键在选项间移动并循环，Home / End 到首尾，Escape 关闭并把焦点还给添加按钮；悬停高亮只跟随鼠标，触摸点按直接选择。

**可访问性**：
- 栏为 Group 角色；每个药丸是可切换按钮（Toggle 状态），Name 形如 "Love, 4 reactions, including yours"（本地化并处理复数）。
- 选择器为 ToolBar 角色、漫游焦点、选项带名称；添加按钮名为 "Add reaction"。
- Polite 状态区播报每次变化；聚焦中的药丸离开时，焦点交给相邻药丸或添加按钮。

**动效**：
- 药丸从 0.5 缩放加 soft 模糊进入（缩放 snappy，透明与模糊 fast 160ms），离开时缩到 0.6 并淡出（P26）；位置以 morph 弹簧重排（P19）。
- 图标获得速度冲量（P27）：弹簧刚度 420、阻尼 17，缩放初速 +11、旋转初速 −420°/s，快速连点叠加而不重启；计数只滚动变化的位，方向随增减（y 用 snappy）（P14）。
- 选择器从添加按钮以 0.5 缩放长出（snappy），选项在 40ms 后按 item 35ms 交错进入；共享高亮在选项间滑动，被指向的图标放大到 1.14 并上移 1px（P10、P7）。
- 尺寸跟随：内容变化后药丸与栏的尺寸弹簧到新值，被动重排直接跳变；栏换行时高度随之弹簧（P5、P6）。
- Reduced：全部换成短淡入淡出，不做布局弹簧（Arc 在水合后才读取，Avalonia 在 Loaded 时读取）；Off 立即。

**Avalonia 实现要点**：
- 模板部件：`PART_ItemsPanel`（WrapPanel 加 LayoutAnimator 做 FLIP 重排）、`PART_AddButton`、`PART_Picker`（复用 Overlays 的 Popover 表面，S8，靠近窗口边缘时重新定位，变换原点放在添加按钮中心）。
- 服务：S3（药丸退场）、S5（栏与药丸尺寸）、S9（选择器漫游）、S11（计数格式化）；每个回应栏一个尺寸观察，长评论串不要一次渲染数百个。

**AtomUI 参考**：Rate、FlyoutHost（参考；仅供学习行为与边界，不复用其实现）。

**风险与未决**：冲量弹簧（刚度 420 / 阻尼 17）不属于标准预设，需在 `ReactionsToken` 中以两个 double 表示；原语目录把 P27 定义为悬停抬起，这里是点按冲量，`Emphasis` 的 API 需兼容两种触发；`Value` 受控 / 非受控与 ItemsControl 的 `ItemsSource` 的关系需在 spec 定稿。

## 5. 实现顺序与依赖
- P5：Reactions，依赖 Overlays 的 Popover（同为 P5，表面需先落地），以及已落地的 P6、P7、P14（Calendar，P1）、P10（Tooltip，P1）、P26（Avatar，P1）、P19 与 P27（DatePicker、AvatarGroup，P2）。
- P7：HoldToConfirm，依赖 NumberField（P6）首个落地的 P22 可暂停时间线、Button 的 P1 P2 P5 P29 与 CopyButton 的 P15；与 Buttons 的 ConfirmMorph hold 模式同阶段交付并共享按压计时。
- P7：SwipeActions，依赖 P5 落地的 P12 拖拽物理与 S10 手势服务（`DragTracker`、`VelocityTracker`、`RubberBand`、`MomentumProjector`，首批使用者为 Drawer、BottomSheet、UserMenu），以及 Menus 的 MenuSurface（P5）与 S3 Presence。

## 6. 相关文档
- [组件总清单](../overview.md) · [规格模板](../spec-template.md) · [路线图](../../roadmap/overview.md)
- [动效原语目录](../../architecture/motion/primitives-catalog.md) · [Token 目录](../../architecture/theming/token-catalog.md)
- [浮层与输入](../../architecture/overlays-and-input.md)（手势与键盘约定、浮层关闭）
- 相邻大类：[按钮](../buttons/overview.md) · [菜单](../menus/overview.md) · [浮层](../overlays/overview.md) · [表格与数据](../tables/overview.md)
