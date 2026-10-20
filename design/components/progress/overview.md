# 进度（progress）

> 进度类组件报告"完成了多少、还剩多久、用了多少"：确定进度条、加载占位、多步流程指示、倒计时与配额用量。

## 1. 大类定位
- 解决：已知任务的确定进度（Progress）、形状已知内容的加载占位（Skeleton）、严格多步流程中的位置（Stepper）、到某个固定时刻的倒计时（Countdown）、固定额度下的用量构成（UsageMeter）。
- 边界：仪表盘单一指标用 Gauge 或 MetricCard，无上限的类别比较用 BarChart（Charts / Cards）；加载完成但没有内容用 EmptyState；AI "思考中"的状态行用 TextShimmer；站点横幅里的倒计时用 AnnouncementBar 自带的计时（Messages）。
- 共同特征：数值与填充由同一个弹簧驱动、永远一致（Progress 的填充与计数、UsageMeter 的分段与总数）；数字像里程表一样按变化方向滚动；读屏读专用文本，滚动的数字层不进可访问性树。

## 2. 组件一览
| 组件 | 类名 | Arc 页面 | 阶段 | Arc 依赖 | 建议基类 | 原语 |
|---|---|---|---|---|---|---|
| 进度条 | `Progress` | [progress](https://uiarc.dev/components/progress) | P3 | 无 | RangeBase | P14 P23 P26 |
| 骨架屏 | `Skeleton` | [skeleton](https://uiarc.dev/components/skeleton) | P3 | 无 | TemplatedControl | P6 P21 |
| 步骤条 | `Stepper` | [stepper](https://uiarc.dev/components/stepper) | P4 | 无 | ItemsControl | P2 P6 P15 P16 P23 P26 |
| 倒计时 | `Countdown` | [countdown](https://uiarc.dev/components/countdown) | P7 | 无 | TemplatedControl | P2 P5 P6 P14 P18 P21 P22 P29 |
| 用量计 | `UsageMeter` | [usage-meter](https://uiarc.dev/components/usage-meter) | P3 | 无 | TemplatedControl | P5 P14 P18 P23 P29 |

## 3. 共享约定
- **弹簧填充（P23）**：填充值走 smooth（0.4s、bounce 0，不过冲）。Progress 的填充是一条满宽色条在裁剪轨道中平移（保持圆头形状），UsageMeter 每个分段各自一个弹簧重新排布。Avalonia 中用圆角裁剪的轨道 + TranslateTransform 实现，避免缩放让圆头变形。
- **滚动数字（P14）**：UsageMeter 与 Countdown 的每位数字是一个裁剪的数字轮（UsageMeter 按数值变化方向转动，Countdown 按最短路径转动）；Progress 的百分比随填充弹簧逐帧计数。可见数字为 Raw，旁边放读屏专用文本；数字一律使用等宽数字（tabular nums）。
- **状态色**：完成用 Success（Progress 的填充与对勾、Countdown 的 "Live now" 圆点），接近上限 Warning、超出上限 Danger（UsageMeter 徽标）；状态不只靠颜色表达（超限另有斜线填充与文字）。
- **首次揭示（P18）**：UsageMeter 在 40% 可见时首次生长，Countdown 首次显示时数字交错滚入；可见性由 S6 提供，只触发一次。
- **持续循环（P21）**：Skeleton 的脉冲（有限次数）与 Countdown 的直播圆点；Reduced 与 Off 下停止。
- **播报**：Progress 不是 live region（完成需另行播报）；Stepper 与 UsageMeter 以 Polite 播报步骤与状态变化；Countdown 的计时器 LiveSetting=Off，只提供不含秒数的文字。
- **动效等级**：Reduced 数值与填充直接跳到新值，文字以短淡入淡出替换；Off 同。

**已落地的共享部件（`Progress/_Shared`，internal）**：
- `ProgressSwapText`：自绘文字交换（P2），新文字从 0.3em + BlurSoft 升入（standard + enter），旧文字带 BlurSubtle 上移淡出（默认 fast，可设 instant），旧文字保持原位且不占布局（popLayout），左对齐标签不会漂移；Reduced 只做短淡入淡出。Progress 标签、Stepper 描述与说明行、UsageMeter 说明与徽标文字共用。
- `ProgressHeightFrame`：`Arm()` 之后一段时间内（Skeleton 120ms、Stepper 700ms）的高度变化以 smooth 弹簧过渡，其他被动变化直接跟随，只在运动中裁剪。
- `ProgressSizeFrame`：宽 / 高分别以 morph / smooth 弹簧跟随子元素，子元素可左 / 中 / 右对齐（UsageMeter 徽标从右缘生长，Countdown 外壳居中）；正确处理无限可用尺寸（Core `SpringSizeDecorator.SameSize` 对 ∞ − ∞ 得 NaN 的问题见集成报告）。
- `ProgressPresenceStage`：叠放多个 `PresenceHost`，只有在场的决定尺寸（Skeleton 占位 / 内容、Countdown 数字 / 完成标签）。
- `ProgressLoop`：挂在 Core `MotionScheduler` 帧批上的 P21 循环（`IMotionDriver`），隐藏或脱离时由调度器暂停，由控件按可见性、视口与动效等级重新启动；`ProgressBlur` 缓存量化的 `ImmutableBlurEffect`，逐帧模糊零分配。

## 4. 组件

### 4.1 Progress（Progress）
`slug: progress` · 阶段 P3 · Arc 依赖：无 · 建议基类：RangeBase

**Arc 行为摘要**：显示已知任务完成了多少：上传、导入或长任务的确定进度，可用 showValue 显示计数百分比并在完成时出现对勾。没有可报告进度的加载用 Skeleton，配额对上限用 UsageMeter，仪表盘指标用 Gauge，多步流程中的位置用 Stepper。

**Arc API 要点**：
| Arc 属性 | 类型 / 默认值 | 建议 Avalonia 属性（类型） |
|---|---|---|
| `value` | `number`，`0`，夹在 0 与 max 之间 | 继承的 `Value`（double） |
| `max` | `100`，视为完成的值 | 继承的 `Maximum`（double，100；`Minimum` 固定为 0） |
| `label` | `string`，可见标签兼无障碍名称，缺省读 "Progress" | `Label`（string?；未设置时 AutomationProperties.Name 为 "Progress"） |
| `showValue` | `false`，显示计数百分比与 100% 时的对勾 | `IsValueVisible`（bool，false） |

**交互与键盘**：
- 只读指示，不可聚焦，无键盘交互。

**可访问性**：
- ProgressBar 类型 peer（只读 IRangeValueProvider），值文本为百分比。
- 标签变化交叉淡化，退出中的旧副本为 Raw。
- 不是 live region，完成时如需提醒由调用方另行播报（例如配合 Toast）。

**动效**：
- 一个 smooth 弹簧同时驱动填充与计数，二者始终一致；填充从左侧滑入以保持圆头（P23、P14）。
- 到 100% 时填充在 200ms 延迟后转为 Success，对勾在 0.24s 延迟后以 snappy 从 scale 0.6 + blur subtle 落位。
- 标签变化以 0.3em + blur soft 升入（standard + enter）。
- Reduced：填充与计数直接跳到新值。

**Avalonia 实现要点**：
- 模板部件 `PART_Track`（高 7px、1px Border、全圆角、SurfaceMuted 底、ClipToBounds）与 `PART_Fill`（Accent），可选 `PART_Meta`（标签与计数，两端对齐，下边距 8px）。
- 百分比 = round(Value / Maximum × 100)，Maximum ≤ 0 时按 100 处理；计数文字绑定到同一个动效值（S2），不另起动画。
- 没有观察器与计时器，开销极低，可在列表中大量使用。
- 服务：S1、S2。

**AtomUI 参考**：ProgressBar（仅供学习行为与边界，不复用其实现）。

**风险与未决**：
- 完成对勾的弹出（scale 0.6 → 1，snappy）接近 P26，主数据未列；可视为 P23 终态的一部分，需原语目录确认。
- RangeBase 允许非零 Minimum，而 Arc 固定从 0 开始；建议锁定 Minimum = 0，开放与否在规格中说明。
- 复核结论（汇总评审）：新增 P26，已同步到[组件总清单](../overview.md)。

**实现记录**：
- `Progress : RangeBase`（`Maximum` 默认 100，`Minimum` 视为 0），`Label`、`IsValueVisible`、只读 `Percentage` / `IsComplete`（伪类 `:complete`）。百分比为 `Math.Round(clamp / max × 100)`，与 JS 一致（1380 / 2400 → 57）。
- 轨道与填充由内部 `ProgressTrack` 自绘：圆角裁剪内平移整条填充；完成色在 200ms 后以 standard 240ms 叠加 Success（离开 100% 无延迟）。计数与对勾由 `ProgressReadout` 自绘：右对齐在 "100%" 宽的盒内，FormattedText 按整数缓存（tnum），对勾 0.24s 后 snappy 从 0.6 + 2px 模糊落位。
- 一个 `MotionValue`（smooth）同时驱动填充与计数；标签用 `ProgressSwapText`。无障碍：ControlType ProgressBar，Name 为标签或 "Progress"，ItemStatus 为 "57%"。Arc 没有不确定态，因此未提供。

### 4.2 Skeleton（Skeleton）
`slug: skeleton` · 阶段 P3 · Arc 依赖：无 · 建议基类：TemplatedControl

**Arc 行为摘要**：内容加载时预留空间：形状已知的个人资料或评论的加载态，并能在加载完成时交叉淡化为真实内容、高度弹簧过渡。能报告百分比时用 Progress，加载完但没有内容时用 EmptyState，AI 思考或状态行用 TextShimmer。

**Arc API 要点**：
| Arc 属性 | 类型 / 默认值 | 建议 Avalonia 属性（类型） |
|---|---|---|
| `label` | `"Loading content"` | `AutomationProperties.Name`（默认 "Loading content"） |
| `lines` | `3`，夹在 1–6 | `Lines`（int，3） |
| `avatar` | `false`，加圆形头像占位 | `IsAvatarVisible`（bool，false） |
| `children` | 加载完成后显示的内容 | `Content`（object?） |
| `loading` | `true`，仅在有 children 时生效 | `IsLoading`（bool，true） |

**交互与键盘**：
- 无交互；加载完成后不移动焦点。

**可访问性**：
- 占位为 status 语义（Polite）并标记忙碌，形状本身为 Raw；有内容时，包裹层在加载期间标记忙碌。Avalonia 没有 aria-busy 的直接等价物，用 ItemStatus（如 "Loading"）表达，需与可访问性架构统一。

**动效**：
- 各块自上而下依次脉冲，形成平缓的波：1.8s 周期、inOut 缓动、每块延迟 90ms，不透明度 1 → 0.52，运行 11 次（约 20s）后静止（P21）。
- 加载完成：占位以 fast 淡出，内容上移 4px 落位，高度以 smooth 从占位高度弹到内容高度（P6）。
- Reduced：停止脉冲，以短淡入淡出替换，不做高度动画。

**Avalonia 实现要点**：
- 块颜色为 BorderStrong 38% 混入 Surface，圆角 RadiusControl；头像为 ControlSizeMD（44）圆形；文本行高 12px，首行 16px 高、宽 48%，末行宽 72%，其余全宽，行距 12px；行宽是容器百分比，随插槽缩放。
- 脉冲用 ControlTheme 中有限次数的 Animation 或 S1 逐帧，Reduced 时移除；只绘制文本行与头像，网格或媒体需调用方自建形状。
- 高度过渡依赖 S5 观察内容尺寸；避免同时渲染上百个骨架。
- 服务：S3、S4、S5。

**AtomUI 参考**：Skeleton（仅供学习行为与边界，不复用其实现）。

**风险与未决**：
- 是否提供可组合的形状部件（如 SkeletonBlock）以覆盖网格与媒体，属于对 Arc 的扩展，需单独决定。

**实现记录**：
- `Skeleton : ContentControl`：`Lines`（强制 1–6）、`IsAvatarVisible`、`IsLoading`、`Label`（"Loading content"）。无内容时总显示占位。
- 占位形状由 `SkeletonBlocks` 一次自绘（头像 44px 圆、首行 16px/48%、末行 72%、行高 12、行距 12、顶部 4），色块为 Token `BlockColor`（BorderStrong 38% 混入 Surface）。脉冲为 `ProgressLoop`：1.8s、inOut、每块 90ms、11 次后静止；只在挂载、可见、在视口内且动效为 Full 时推进，离开视口暂停并保留相位。
- 加载结束：`ProgressPresenceStage` 中占位 `PresenceHost`(Fade) 淡出、内容 `PresenceHost`(Rise 4px) 升入，`ProgressHeightFrame`（120ms 窗口）让高度以 smooth 弹簧从占位高度过渡到内容高度。
- 无障碍：Group + LiveSetting Polite，加载中 Name = Label、ItemStatus = "Loading"（aria-busy 的近似）。

### 4.3 Stepper（Stepper）
`slug: stepper` · 阶段 P4 · Arc 依赖：无 · 建议基类：ItemsControl

**Arc 行为摘要**：显示用户在多步流程中的位置以及已完成的步骤：结账、账户设置等严格的多步流程，可经 onStepSelect 跳回已完成步骤，或作为带描述与错误态的竖向步骤时间线。只需一个百分比时用 Progress。Stepper 只负责指示，步骤内容由调用方渲染并驱动 current。

**Arc API 要点**：
| Arc 属性 | 类型 / 默认值 | 建议 Avalonia 属性（类型） |
|---|---|---|
| `steps`（必填） | `{ id, label, description?, error? }[]`；error 把标记变为警示并替换描述 | 继承的 `ItemsSource`（项为 StepperStep：Id、Label、Description、Error） |
| `current`（必填） | 进行中步骤的索引，等于步骤数时表示全部完成 | `CurrentIndex`（int） |
| `orientation` | `"horizontal" \| "vertical"`，`"horizontal"` | `Orientation`（Avalonia Orientation，Horizontal） |
| `onStepSelect` | 选择已完成步骤时回调，缺省即只读 | `StepSelected` 事件 + `IsNavigable`（bool，false） |
| `details` | `"all" \| "current"`，`"all"`；错误总是显示 | `DescriptionMode`（enum：All / Current） |
| `compact` | `false`；横向宽度 < 30rem 时自动切换 | `IsCompact`（bool，false） |
| `label` / `completeLabel` | `"Progress"` / `"All steps complete"` | `AutomationProperties.Name` / `CompleteLabel`（string） |

**交互与键盘**：
- 可导航时方向键在可到达的步骤之间移动焦点（遵循 RTL），Home / End 跳到第一步或当前步，Enter / Space 选择已完成步骤。
- 未开始的步骤不可选、不在 Tab 顺序中。

**可访问性**：
- 可导航时根为具名的导航分组，否则为 Group；当前步骤以 ItemStatus 标注为 current。
- 每步追加读屏专用状态：Completed、Not started、Error。
- 步骤变化以 Polite 播报 "Step 2 of 4: Shipping"，全部完成播报 CompleteLabel。

**动效**：
- 进度跨越多步时连接线依次填充（每段间隔 line 交错 80ms），随后新当前标记外的光环以 snappy 从 scale 0.6 长出（P23、P26）。
- 数字形变为描绘的对勾或警示符号：字形以 scale 0.5 + blur subtle 弹出（snappy），路径以 standard + enter 描绘（P16、P15）。
- 描述文字以 0.3em + blur soft 升入，所在插槽高度弹簧适配（P2、P6）。
- Reduced：所有变化立即应用。

**Avalonia 实现要点**：
- 标记为 28px 圆盘：Surface 底 + 1px BorderStrong 内描边；光环外扩 4px，AccentSubtle 底，错误态为 Danger 16%；连接线 2px、Border 底，填充沿轨道按弹簧缩放，两端与标记保持间隙。
- 横向容器宽 < 30rem 时自动切到仅标记 + 标题说明（S5）；竖向适合侧栏与窄列，标签与描述换行不溢出。
- 项容器为内部 StepperItem；键盘导航用 S9（跳过不可达步骤）。
- 服务：S1、S2、S3、S5、S9。

**AtomUI 参考**：Steps（仅供学习行为与边界，不复用其实现）。

**风险与未决**：
- P16（数字到对勾的几何形变）首次落地在 IconMorph（P3），Stepper 应复用其路径形变，而不是另写一套。
- 连接线依次填充依赖"上一次的 current"；控件初始化与首帧不播放动画（DisableTransitions 约定）时需要正确记录起点。

**实现记录**：
- `Stepper : TemplatedControl`（非 ItemsControl：步骤少且需要整体控制动效）：`ItemsSource` 或内联 `Steps`（`StepperStep`：Id、Label、Description、Error，属性变化即刷新）、`CurrentIndex`（TwoWay）、`Orientation`、`IsNavigable`、`Details`、`IsCompact`、`Label`、`CompleteLabel`、`StepSelected` 路由事件（未处理时自动设为当前步）、`Select(index)`。
- 每步为内部 `StepperItem`（模板：标记 + 标签 + `ProgressSwapText` 描述，高度在 700ms 窗口内 smooth 弹簧），连接线在 `StepperItem.Render` 中绘制（轨道 Border，填充 Accent，smooth 弹簧，延迟 = 自上一个当前步起逐段 80ms）。`StepperMarker` 自绘光环（snappy 从 0.6，延迟 = 跨越步数 × 80ms + 0.12s）、圆盘与内描边（颜色 240ms 交叉）、数字 / 对勾 / 警示字形（0.5 + 2px 模糊弹出，路径 standard + enter 描绘，警示点 snappy）。
- 横向宽度 < 480px 自动紧凑并在下方显示当前步说明行；`StepperPanel` 横向为 n − 1 等宽列 + 末列自适应。键盘：方向键在可到达步骤间移动（横向 RTL 反向）、Home / End、Enter / Space 选择已完成步骤；焦点环自绘在步骤头外 4px。
- 无障碍：每步 Name 带 "Completed / Not started / Error"，当前步 ItemStatus "Current step"；Stepper LiveSetting Polite，ItemStatus 为 "Step 2 of 4: Shipping" 或 CompleteLabel。路径形变（P16）未复用 IconMorph（尚不存在），改为字形交换 + 描边绘制。

### 4.4 Countdown（Countdown）
`slug: countdown` · 阶段 P7 · Arc 依赖：无 · 建议基类：TemplatedControl

**Arc 行为摘要**：倒数到某个固定时刻，数字轮滚动，到零时形变为直播态。用于发布页或活动首屏、表格行或横幅中的紧凑截止标签，以及到点后自动切换为 Live now 的销售或报名窗口。站点横幅中带行动按钮的倒计时用 AnnouncementBar。

**Arc API 要点**：
| Arc 属性 | 类型 / 默认值 | 建议 Avalonia 属性（类型） |
|---|---|---|
| `target`（必填） | `Date \| number \| string`：epoch 毫秒、带偏移的 ISO，或 timeZone 中的墙上时间 | `Target`（DateTimeOffset；字符串目标经静态 `Countdown.ResolveTarget(text, timeZone)` 转换） |
| `timeZone` | IANA 名称，按目标日期处理夏令时 | `TimeZone`（TimeZoneInfo?） |
| `units` | `["days", "hours", "minutes", "seconds"]`，最大单位吸收其余 | `Units`（CountdownUnits 标志枚举：Days / Hours / Minutes / Seconds） |
| `variant` | `"large" \| "compact"`，`"large"` | `Variant`（enum：Large / Compact） |
| `completeLabel` | `"Live now"`，带脉冲的直播圆点 | `CompleteContent`（object，默认 "Live now"） |
| `onComplete` | 挂载期间到零时触发一次，隐藏标签页中也准时 | `Completed` 事件 |
| `label` | `"Time remaining"` | `AutomationProperties.Name`（默认 "Time remaining"） |
| `clock` | `() => number`，`Date.now` | `TimeProvider`（.NET TimeProvider，默认 System） |

**交互与键盘**：
- 只读，不可聚焦，无键盘交互。

**可访问性**：
- Avalonia 没有计时器控件类型：peer 用 Text 类型，GetLocalizedControlTypeCore 返回 "timer"，Name 取 label；可见数字为 Raw。
- 隐藏文本用文字读剩余时间，剩余超过一分钟时省略秒数以免频繁变化；完成时读 CompleteContent 的字符串，非字符串内容读 "Complete"。

**动效**：
- 每位数字是裁剪的数字轮，按最短方向转动（00 到 59 是向下一步），窗口上下羽化 0.16em，转动中按偏移加模糊（最多 blur subtle）（P14、P29）。
- 首次显示时数字以 smooth 交错滚入（item 35ms，上限 0.3s）（P18），之后每秒用 snappy。
- 单位列出现或消失时外壳宽度以 morph、高度以 smooth 弹簧变化，紧凑胶囊把这一形变表现为自己的轮廓（P5、P6）。
- 到零时数字以 scale 0.92 + blur soft 淡出并形变为完成标签（P2），直播圆点以 2s 周期脉冲（P21）。
- 秒级更新是一条与时钟对齐的时间线（P22）。
- Reduced：数字轮直接跳变，外壳立即定尺寸，以淡入淡出切换，停止脉冲。

**Avalonia 实现要点**：
- 计时：单个计时器在每个整秒后约 8ms 醒来；窗口不可见或控件不在视口内时暂停（S6、S12）；另有一个长计时器保证 Completed 准时触发（超长目标按 Int32 毫秒上限分段）。
- 时区：用 TimeZoneInfo 解析 IANA 名称，不依赖设备时区；文档应建议目标总是带偏移或时区，保证所有用户倒数到同一瞬间。
- large 变体的数字随容器宽度缩放（Arc 为 clamp(1.75rem, 12.5cqi, text-5xl)，四组在 320px 宽可放下）；compact 为最小高 32px 的胶囊（1px Border、全圆角、Surface），完成后边框为 Success 35%、底色为 Success 9%。
- 每位数字渲染整个数字轮（≤ 10 个字形），页面上只放少量倒计时。
- 服务：S1、S2、S3、S5、S6、S11、S12。

**AtomUI 参考**：TimerStatistic（仅供学习行为与边界，不复用其实现）。

**风险与未决**：
- Windows 上 TimeZoneInfo 识别 IANA 名称依赖 ICU；若应用开启 InvariantGlobalization 或 NativeAOT 裁剪了 ICU，需要回退方案，需在 AOT spike 中验证。
- 主数据中的 P29 指数字轮窗口的羽化遮罩；若原语目录把它归入 P14，可去重。

**实现记录**：
- `Countdown : TemplatedControl`：`Target`（DateTimeOffset?）或 `Duration`（TimeSpan?，可暂停）、`Units`（标志枚举，最大单位吸收其余）、`Variant`（Large / Compact）、`CompleteContent`（"Live now"）、`Label`、`TimeProvider`、`IsPaused`（TwoWay）+ `Pause()` / `Resume()` / `Restart()`、只读 `Remaining` / `IsCompleted`、`Completed` 路由事件；静态 `ResolveTarget(text, TimeZoneInfo?)` 不读设备时区（无偏移的墙上时间按给定时区解析，跳过的夏令时时刻向后取）。
- 计时：Core `PausableTimer` 作为完成计时器（目标模式加 8ms 余量并按 Int32 毫秒分段重排，时长模式即剩余时间，可暂停）；另有一个对齐整秒 + 8ms 的 DispatcherTimer 只在挂载、视口内且未暂停时刷新显示。挂载时目标已过：直接显示完成态，不触发事件。目标模式下暂停只冻结显示，完成仍准时。
- 显示由 `CountdownFace` 一次自绘：每位数字为裁剪 + 上下羽化遮罩的数字轮，十位用 6（分 / 秒）或 3（时）格，按最短方向转动，模糊随偏移增长（最多 2px）；首次读数前为 0.3 透明的 00（pending），随后 smooth + 每位 35ms（上限 0.3s）滚入，此后每秒 snappy。Large 数字字号 = clamp(28, 12.5% 宽, 72)，完成标签 clamp(28, 11% 宽, 52)。
- 外壳为 `ProgressSizeFrame`（宽 morph、高 smooth），完成时数字与 "Live now" 以 `PresenceHost`(Scale 0.92, morph) 交叉（未做 4px 模糊）；`CountdownPulse` 直播点 2s 脉冲（P21）。无障碍：ControlType Text、Name = Label、HelpText 为不含秒数（>1 分钟时）的文字、LiveSetting Off。新出现的数字列只做淡入（宽度直接定位）。

### 4.5 UsageMeter（Usage meter）
`slug: usage-meter` · 阶段 P3 · Arc 依赖：无 · 建议基类：TemplatedControl

**Arc 行为摘要**：显示是什么占满了额度、离上限还有多近：存储、席位、API 调用等固定额度下的用量；计费页展示各类别的占用与套餐余量；可超限的套餐需要在不依赖颜色的情况下清楚表达超出部分。任务完成度用 Progress，无上限的类别比较用 BarChart，单一头部指标用 Gauge 或 MetricCard。

**Arc API 要点**：
| Arc 属性 | 类型 / 默认值 | 建议 Avalonia 属性（类型） |
|---|---|---|
| `label`（必填） | `string`，如 "Workspace storage" | `Label`（string） |
| `segments`（必填） | `{ id, label, value }[]`，按显示顺序，id 保持稳定 | `Segments`（`IList<UsageSegment>`：Id、Label、Value） |
| `limit`（必填） | `number`，与分段同单位 | `Limit`（double） |
| `unit` / `decimals` | `""` / `1` | `Unit`（string）/ `Decimals`（int，1） |
| `freeLabel` / `overLabel` | `"Free"` / `"Over limit"` | `FreeLabel` / `OverLabel`（string，默认值来自 xlf） |
| `warnAt` | `0.9`（占上限的比例） | `WarnThreshold`（double，0.9） |

**交互与键盘**：
- 图例为 roving tab stop：方向键在图例项之间移动，Home / End 到首 / 末项。
- Enter / Space 固定或取消固定聚焦的类别，Escape 清除固定。
- 鼠标悬停在条上高亮对应类别（超出上限的区域高亮超限部分），触摸时轻点固定。

**可访问性**：
- 整体为以标题命名的 Group；条为 Image 类型，Name 是包含每个分段与状态的完整文字摘要。
- 滚动数字为 Raw，旁边有读屏专用文本；状态变化（接近上限、超限、超出量增长）以 Polite 播报。
- 图例项是带按下状态的按钮（IToggleProvider），Name 形如 "Media, 14.2 GB"。

**动效**：
- 进入视口 40% 后，分段从左到右生长一次（0.58s 无回弹弹簧，分段间隔 70ms）（P18、P23）；之后数值变化时每段一个 smooth 弹簧重新排布。
- 用量跨过上限时，斜线超限填充与上限标记以 fast 淡入（P29）。
- 数字像里程表一样按数值变化方向滚动（P14）；状态徽标宽度以 morph 弹簧适配（P5）。
- Reduced：数值直接跳变，文字以短淡入淡出替换。

**Avalonia 实现要点**：
- 分段颜色是 accent 的阶梯（Accent，及 Accent 以 62%、36%、22% 混入 Surface），而不是 Series 色，让条读起来是同一个额度；剩余空间就是轨道本身（Foreground 7% 混入 Surface，高 14px、全圆角），分段间隔 2px，最多 4 段可读。
- 超限：已用颜色保持，叠加斜线图案（平铺几何画刷），高亮超限部分时以 Danger 填满。
- 徽标为 28px 高的胶囊：正常 SurfaceMuted，接近上限 Warning 14% 底，超限 Danger 12% 底，并带图标；总量用展示字体 text-3xl。
- 图例为 auto-fit 网格（最小列宽 108px），标题单行省略；S5 观察条宽，S6 触发首次揭示，S11 格式化数值与单位。
- 服务：S1、S2、S5、S6、S9、S11。

**AtomUI 参考**：ProgressBar（仅供学习行为与边界，不复用其实现）。

**风险与未决**：
- 分段使用 accent 阶梯而不是 Series1–4，需在 Token 目录中确认是否新增派生色 Token（颜色 Token 的每个变体都要单独发布）。

**实现记录**：
- `UsageMeter : TemplatedControl`：`Label`、`ItemsSource` 或内联 `Segments`（`UsageSegment`：Id、Label、Value，属性变化即刷新）、`Limit`、`Unit`、`Decimals`（1）、`FreeLabel`、`OverLabel`、`WarnThreshold`（0.9）、只读 `Status`（Ok / Near / Over）与 `Total`、`ClearPin()`。
- 条形由内部 `UsageMeterBar` 自绘：每类一个 `MotionValue`（按 Id 保持），首次 40% 可见后（推迟到 Loaded 之后，相当于 Arc 的 soon()）以 0.58s 无回弹弹簧、每段 70ms 生长，之后 smooth 重排；段间 2px，上限之外以 Surface 色 45° 斜线（2.5px / 6px）覆盖并有 2px 上限标记，二者在越线时 fast 淡入；高亮时其余段 0.28 透明、超限部分以 Danger 填满。指针（鼠标）预览，触摸 / 笔轻点固定。
- 分段颜色是 Accent 以 100 / 62 / 36 / 22% 混入 Surface，在 C# 中由主题提供的 Accent 与 Surface 画刷计算（与 accent 相关，不进 Token）；轨道（Foreground 7%）、徽标底（Warning 14% / Danger 12%）为 Token。
- 总数与图例数值用 Core `RollingDigits`；说明行与徽标文字用 `ProgressSwapText`；徽标宽度用 `ProgressSizeFrame`（morph，从右缘生长）。图例为内部 `UsageMeterLegendItem : ToggleButton`（按下 = 固定），`UsageMeterLegendPanel` 为 auto-fit 网格（最小 108px，间距 2）；roving tab stop、方向键循环、Home / End、Enter / Space 固定、Escape 清除。
- 无障碍：Group，条为 Image 且 Name 为完整摘要；状态变化（及超出量增长）写入 ItemStatus 并以 LiveSetting Polite 播报。

## 5. 实现顺序与依赖
- **P3**：Progress（P23 的首个使用者，FileDropzone、Stepper、UsageMeter 复用）、Skeleton（P6、P21 已由 Calendar 与 Button 落地）、UsageMeter（P14 来自 Calendar（P1）、P18 来自 AnimatedCounter（P1），需要 S6）。
- **P4**：Stepper，P16 由 IconMorph（P3）先落地，P15（CopyButton，P1）、P26（Avatar，P1）已就绪。
- **P7**：Countdown，P22 由 NumberField（P6）先落地，需要 S12；与 AnnouncementBar 同阶段，二者共用 S12 的秒级对齐计时。
- 跨大类：数字轮实现与 AnimatedCounter（Charts，P1）共享；UsageMeter 与 Countdown 的数值、单位与时长格式化走 S11。

## 6. 相关文档
- [组件总清单](../overview.md)
- [组件规格模板](../spec-template.md)
- [动效原语目录](../../architecture/motion/primitives-catalog.md)
- [Token 目录](../../architecture/theming/token-catalog.md)
- [路线图](../../roadmap/overview.md)
- Arc 原页面见第 2 节表格中的链接。
