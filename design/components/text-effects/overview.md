# 文字效果（text-effects）

> Arc 中作用于文字本身的动效组件：首屏标题揭示、滚动进入的分区标题、短标签逐字形变与表示进行中的微光。

## 1. 大类定位
- 让文字本身承载动效：页面加载时的 hero 标题（TextReveal），滚动进入视口的分区标题（InViewTitle），只变化几个字母的状态标签（TextMorph），以及表示工作仍在进行的状态行（TextShimmer）。
- 边界：会变化的数字归 [图表](../charts/overview.md)（AnimatedCounter）；需要按钮行为时归 [按钮](../buttons/overview.md)（Button）；内容占位与可度量的进度归 [进度](../progress/overview.md)（Skeleton、Progress）。
- 共同特征：只处理单行或短文本；可访问文本与动画字形分离，读屏只读到一份干净的文字；都不主动播报变化；Reduced 下取消位移、模糊、遮罩与循环，改为快速淡入或直接替换。

## 2. 组件一览
| 组件 | 类名 | Arc 页面 | 阶段 | Arc 依赖 | 建议基类 | 原语 |
|---|---|---|---|---|---|---|
| Text reveal | TextReveal | [text-reveal](https://uiarc.dev/components/text-reveal) | P3 | 无 | Control（逐词/逐行排版） | P20 P29 |
| In-view title | InViewTitle | [in-view-title](https://uiarc.dev/components/in-view-title) | P3 | 无 | Control（逐词/逐行排版） | P18 P20 P29 |
| Text morph | TextMorph | [text-morph](https://uiarc.dev/components/text-morph) | P1 | 无 | Control（逐字形排版） | P4 P5 |
| Text shimmer | TextShimmer | [text-shimmer](https://uiarc.dev/components/text-shimmer) | P3 | 无 | Control | P2 P21 |

## 3. 共享约定
- 可访问文本：完整文本作为 `AutomationProperties.Name`（TextReveal、InViewTitle 的标题，TextMorph 的标签），动画单元一律 `AutomationProperties.AccessibilityView=Raw`；四者都不主动播报变化，需要播报时由外层的 `LiveSetting` 区域负责。
- 单行短文本：TextMorph 与 TextShimmer 永不换行；TextReveal 最宽 24ch 并平衡换行；InViewTitle 的 line 变体使用显式行、不重排；逐单元动效都只适合标题与标签，不适合长正文。
- 标题级别：TextReveal（h1、h2、h3、p）与 InViewTitle（h1 到 h3）的 `as` 映射为 `Level` 属性，并写入 `AutomationProperties.HeadingLevel`；TextMorph 与 TextShimmer 的 `as` 只影响语义元素，Avalonia 侧不需要对应属性。
- 逐单元排版：TextReveal、InViewTitle、TextMorph 都要把文本拆成词、行或字形，让每个单元独立变换、模糊与裁剪；前两者基于 Core 的 `StaggeredText`（P20），TextMorph 基于 `GlyphMorphPresenter`（P4），文本测量统一用 TextLayout。
- 触发方式：TextReveal 挂载时播放一次且首帧即开始；InViewTitle 在 45% 可见时触发（S6）；TextMorph 在文本变化时形变；TextShimmer 只在激活、距视口 64px 以内且窗口可见时运行。
- Token：交错 StaggerWord（40ms）、StaggerLine（80ms）、StaggerChar（16ms）；模糊 BlurText（8px，标题）与 BlurSoft（4px，段落等需要更柔和的场景）；总交错时长封顶（Token 目录约定在约 0.4s 内）。
- 降级：Reduced 去掉位移、模糊、遮罩与循环，改为整体快速淡入（fast）或直接替换；Off 直接显示终态。

## 4. 组件

### 4.1 TextReveal（Text reveal）
`slug: text-reveal` · 阶段 P3 · Arc 依赖：无 · 建议基类：Control（逐词/逐行排版）

**Arc 行为摘要**：挂载时揭示一次的标题：每个词从各自的裁剪框中升起，同时从柔和的模糊变清晰；Arc 用纯 CSS 实现，首帧即开始，脚本加载慢也不会让文字停留在隐藏状态。用于首屏 hero 标题与落地页开场。页面下方的分区标题用 InViewTitle。

**Arc API 要点**：
| Arc 属性 | 类型 / 默认值 | 建议 Avalonia 属性（类型） |
|---|---|---|
| `text`（必填） | string，用 `\n` 强制换行 | `Text`（string） |
| `as` | `"h2"`（h1、h2、h3、p，p 用更柔和的模糊） | `Level`（`TextLevel`：H1、H2、H3、Paragraph） |
| `delay` | `0`（秒） | `Delay`（TimeSpan，第一个词升起前的等待） |
| `id` | 转发给元素，供 aria-labelledby 引用 | 无需对应：其他控件用 `AutomationProperties.LabeledBy` 直接引用本控件 |

**交互与键盘**：
- 无交互，不可聚焦。

**可访问性**：
- H1 到 H3 时设置 `HeadingLevel` 1 到 3，Paragraph 时为普通 Text 角色；名称都是完整文本（Arc 的 p 改用隐藏副本是 HTML 的限制，Avalonia 不需要）。
- 强制换行读作空格；按页面大纲选择级别。

**动效**：
- 每个词从裁剪框中升起（P29），同时由模糊变清晰，按词交错（P20；StaggerWord，BlurText，Paragraph 用 BlurSoft）。
- 总交错时长封顶，长文本不会拖长入场；单个词 considered + enter。
- Reduced：去掉裁剪与升起，词快速淡入（fast）；Off：直接显示。

**Avalonia 实现要点**：
- 基于 Core 的 `StaggeredText`（P20）逐词排版：用 TextLayout 测量每个词，最宽 24ch 并平衡换行；Avalonia 的 TextWrapping 没有 balance 模式，需要自行计算断行。
- 首帧即开始：在首次布局完成时启动入场，动画没有运行时一律显示终态。这与"控件首次显示不播放动画"的首帧规则（见 [减少动效](../../architecture/motion/reduced-motion.md) 第 5 节）相反，需要登记为有意的入场例外，并用代码驱动的 `MotionValue` 时间线实现，不走 Transitions。
- 只播放一次；Arc 通过更换元素 key 重播，Avalonia 可提供 `Replay()` 方法或在 `Text` 变化时重播（待定）。
- 每个词各有一个模糊效果（BlurEffect），这是主要开销。

**AtomUI 参考**：—（AtomUI 无同类控件）

**风险与未决**：
- 平衡换行与 `\n` 强制换行在窄屏上的组合效果需在 Demo 中验证（强制换行在所有宽度都生效）。
- 逐词 BlurEffect 在软件渲染或低端 GPU 下的开销需测量，必要时降级为只做透明度与位移。

**实现记录**：
- 共享部件（`TextEffects/_Shared`，TextReveal / InViewTitle / TextShimmer 共用）：公开枚举 `TextLevel`（H1 / H2 / H3 / Paragraph，写入 HeadingLevel 1–3 / 0）；`TextEffectsWordLayout` 把文本拆成词（`\n` 为强制换行）或显式行并用 TextLayout 逐词测量、贪心换行、可选平衡换行（二分求保持行数的最窄宽度）、可选逐字母测量，布局只在文本 / 样式 / 宽度变化时重建；`TextEffectsLoop`（挂在 `MotionScheduler` 上的 P21 循环）；`TextEffectsBlur`（量化缓存的模糊效果）。设计中预想的 Core `StaggeredText` 未新建，逐词绘制由各控件在 `Render` 中完成（一个控件，无逐词子控件）。
- `TextReveal : Control`：`Text`、`Level`（默认 H2）、`Delay`、`MaxWidthInChars`（24ch）、`Tracking` / `Leading` / `BlurRadius`（主题按级别设置：标题为 Display 字体、TrackingDisplay、1.1 行高、BlurText；段落为 Body、1.4、BlurSoft）、`Replay()`、只读 `IsRevealing`。
- 每词在 `Render` 中：裁剪框（上 0.1em、左右 0.16em、下 0.24em）+ 底部 0.18em 羽化遮罩（缓存渐变画刷），0.76s enter 从 0.62em 升起、0.44s standard 淡入、0.58s enter 去模糊（`PushEffect`），延迟 = Delay + 位置 × 步长 + 强制行号 × 步长，步长 = min(40ms, 0.48s / 词数)；单个 `MotionValue` 时间线驱动全部词，结束后按普通文字绘制。Reduced：保留交错，只做 fast 淡入；Off：直接显示。
- 挂载时已在视口内则首帧即开始（同 Arc）；挂载在视口外则隐藏等待，首次进入视口（Core `ViewportVisibility`，once）时播放。

### 4.2 InViewTitle（In-view title）
`slug: in-view-title` · 阶段 P3 · Arc 依赖：无 · 建议基类：Control（逐词/逐行排版）

**Arc 行为摘要**：滚动进入视口时揭示自己的分区标题，提供五种揭示风格。用于功能页各分区标题的统一入场，`once` 设为 false 时每次进入都重播。首屏 hero 文案用 TextReveal，状态行用 TextShimmer。

**Arc API 要点**：
| Arc 属性 | 类型 / 默认值 | 建议 Avalonia 属性（类型） |
|---|---|---|
| `text`（必填） | string | `Text`（string） |
| `variant` | `"blur"`（word、line、blur、tracking、wipe） | `Variant`（`InViewTitleVariant`：Word、Line、Blur、Tracking、Wipe） |
| `as` | `"h2"`（h1 到 h3） | `Level`（`TextLevel`：H1、H2、H3） |
| `lines` | `string[]`，仅 line 变体使用 | `Lines`（`IReadOnlyList<string>?`，未设置时整段作为一行） |
| `once` | `true` | `IsReplayEnabled`（bool，默认 false；true 对应 Arc 的 once 为 false） |
| `id` | 转发给标题 | 同 TextReveal，用 `AutomationProperties.LabeledBy` 引用 |

**交互与键盘**：
- 无交互，不可聚焦。

**可访问性**：
- 标题带完整文本名称与 `HeadingLevel`，动画部分为 Raw；按页面的标题大纲选择级别。
- 进入页面时已经滚过的标题直接显示终态，不再播放。

**动效**：
- 标题 45% 可见时触发（P18）：不透明度先行，模糊其次清除，变换最后落定，文字在完全到位前已可读（P20；StaggerWord、StaggerLine，BlurText，considered）。
- word 与 line 从裁剪框升起，wipe 从左向右揭开（P29）；tracking 由收紧的字距展开；blur 由模糊变清晰。
- `IsReplayEnabled` 时离开视口为无交错的快速淡出（exit）。
- Reduced：立即显示，没有变换、模糊或遮罩；Off：同 Reduced。

**Avalonia 实现要点**：
- 可见性检测走 S6 的 `ViewportVisibility`（基于 EffectiveViewportChanged，阈值 0.45；重播时 `once` 关闭），每个标题一个观察。
- 每个词或行是一个可变换单元（模糊加变换）；Line 变体使用显式行、不重排，每行要短到能放进窄屏。
- tracking 变体需要逐帧改变 `LetterSpacing` 并重新排版，或按字形逐个定位。
- 与 TextReveal 共用 Core 的 `StaggeredText`（P20）。

**AtomUI 参考**：—（AtomUI 无同类控件）

**风险与未决**：
- tracking 变体逐帧改字距会触发文本重排，开销与宽度抖动需评估。
- `StaggeredText` 要同时满足 TextReveal（挂载即播）与 InViewTitle（五种变体、可重播）的需求，接口需在阶段 P3 一起定稿。

**实现记录**：
- `InViewTitle : Control`：`Text`、`Variant`（Word / Line / Blur / Tracking / Wipe，默认 Blur）、`Level`、`Lines`、`IsReplayEnabled`、只读 `IsRevealed`。45% 可见时触发（Core `ViewportVisibility`；重播时 once 关闭），顶部已滚出视口超过 55% 的标题直接显示终态。
- 时间线按 Arc 的 reveal()：位移 settle（Word / Blur 0.8、Line 0.86、Tracking / Wipe 0.9，Tracking 字母 1.0）enter，模糊 0.78×settle enter，透明度 0.56×settle standard；词步长 min(60ms, 0.4s / 词数)，行 80ms，Tracking 32ms。Word / Line 为硬裁剪升起（0.9em / 112%），Tracking 逐字母从 (中心 − i) × 0.06em 展开，Wipe 用缓存的边缘渐变遮罩 + 实心裁剪两段绘制（遮罩 2.3 倍宽、44%–56% 羽化，位置 100% → 0%）。
- 离开视口（重播模式）为无交错的 exit 180ms 淡出。Reduced / Off：立即显示终态。

### 4.3 TextMorph（Text morph）
`slug: text-morph` · 阶段 P1 · Arc 依赖：无 · 建议基类：Control（逐字形排版）

**Arc 行为摘要**：把一个短标签原地形变为下一个：共同的字母滑到新位置，新字母清晰地淡入，移除的字母模糊消失，宽度弹簧适配。用于只变化几个字母的状态词（Follow 与 Following、Save 与 Saved），以及自定义按钮或徽标里需要形变的标签。需要按钮行为时用 Button；会换行的文本不适用，因为它总是单行。

**Arc API 要点**：
| Arc 属性 | 类型 / 默认值 | 建议 Avalonia 属性（类型） |
|---|---|---|
| `children`（必填） | string，当前标签，变化即形变 | `Text`（string） |
| `as` | `"span"`（span、div、p、strong、h1 到 h3） | 无直接对应：标题语义用 `AutomationProperties.HeadingLevel`，字重等用继承的字体属性 |
| `id` | 转发给元素 | 同 TextReveal |

**交互与键盘**：
- 无交互，不可聚焦。

**可访问性**：
- 纯文本标签作为名称（Text 角色），动画字形为 Raw。
- 不播报变化；状态变化需要播报时由外层设置 `LiveSetting`。

**动效**：
- 字母按字符与出现次数匹配，共同的字母在弹簧上滑到新位置（P4；morph）。
- 新字母带模糊交错进入，移除的字母模糊淡出（P4；StaggerChar × 2，BlurSoft，移除用 exit）。
- 外框宽度弹簧到新标签的宽度（P5；morph）；字体加载或容器尺寸变化时立即跟随，不做动画。
- Reduced：标签与宽度立即替换；Off：同 Reduced。

**Avalonia 实现要点**：
- 基于 Core 的 `GlyphMorphPresenter`（P4）：每个字符一个字形单元（TextLayout 逐字形测量），以"字符加第 n 次出现"为 key 做 FLIP。
- 宽度弹簧由 `SpringSizeDecorator`（P5）在测量阶段报告动画中的宽度；字体变化经 S5 直接跳到新宽度。
- 单行不换行，字体属性继承自 `TextElement`；ColorPicker（阶段 P6）硬依赖它，这是它排在阶段 P1 的原因。

**AtomUI 参考**：—（AtomUI 无同类控件）

**风险与未决**：
- 连字与字距调整会让逐字形定位与整体排版不一致，需要按字形簇拆分；中文等不以空格分词的文本的匹配策略需在规格中确定。
- P4 由 CopyButton 与 TextMorph 在阶段 P1 共同落地，`GlyphMorphPresenter` 的接口需同时满足两者，并为后续的 ActionButton、Input、ColorPicker 等保持通用。

### 4.4 TextShimmer（Text shimmer）
`slug: text-shimmer` · 阶段 P3 · Arc 依赖：无 · 建议基类：Control

**Arc 行为摘要**：一道柔和的光带扫过短状态行，表示工作仍在进行；`active` 变为 false 时光带滑出，文字变为实色。用于 AI 思考或加载标签，例如 "Generating summary"。内容占位用 Skeleton，可度量的进度用 Progress；完成时应切换 `active` 而不是卸载组件，让文字平稳落定。

**Arc API 要点**：
| Arc 属性 | 类型 / 默认值 | 建议 Avalonia 属性（类型） |
|---|---|---|
| `children`（必填） | string，一行短文本，新值原地升入 | `Text`（string） |
| `active` | `true` | `IsActive`（bool） |
| `duration` | `1.8`（秒，扫过一次） | `SweepDuration`（TimeSpan） |
| `as` | `"span"`（span、p、div、h2 到 h4） | 同 TextMorph |

**交互与键盘**：
- 无交互，不可聚焦。

**可访问性**：
- Arc 在激活时设置 aria-busy；Avalonia 没有忙碌属性，可用 `AutomationProperties.ItemStatus` 表达（需验证读屏效果）。
- 文字始终是真实可读的文本；完成时的播报由外部 Live 区域负责。

**动效**：
- 钟形高亮带以当前文字色在弱化的底色上线性扫过，每趟之间短暂停顿（P21；每趟 `SweepDuration`）。
- 只在激活、在视口内且窗口可见时运行，暂停后从停下的位置继续（P21）。
- 标签变化时新文字带柔和模糊升入（P2；standard + enter，BlurSoft）；`IsActive` 变为 false 时光带滑出，文字变为实色。
- Reduced：停止扫光，标签以普通淡入淡出替换；Off：立即替换。

**Avalonia 实现要点**：
- 单个元素上的渐变动画：用 LinearGradientBrush 作为文字的 Foreground 并移动其变换，不拆逐字元素。
- 扫光由 P21 的 `LiveLoop` 驱动并保存相位；视口检测（S6，64px 余量）与窗口可见性共同决定是否运行。
- 高对比模式（`PlatformColorValues.ContrastPreference`）下去掉渐变，文字用系统色；保持最大宽度 100%，避免换行。

**AtomUI 参考**：Skeleton(IsActive)（参考）（仅供学习行为与边界，不复用其实现）

**风险与未决**：
- `LiveLoop` 基于 Composition，而文字渐变画刷的变换能否在合成线程上动画需验证，否则退回 UI 线程逐帧驱动（S2）；"暂停后从原处继续"只需保存循环相位，不依赖阶段 P6 才落地的 P22。
- aria-busy 在 Avalonia 没有直接等价，ItemStatus 方案需验证。

**实现记录**：
- `TextShimmer : Control`：`Text`、`IsActive`（true）、`SweepDuration`（1.8s）、`BaseForeground`（可选；默认 = TextMuted 与文字色 12% 的 OKLab 混色，混合比来自 Token）。
- 绘制：底色文字 →（settle 比例的）实色文字 → 再画一遍实色文字并通过钟形渐变遮罩（缓存画刷，宽 4.4em，按 t·宽 + (2t − 1)·2.2em 平移）显出光带，逐帧零分配。扫光为 `TextEffectsLoop`：首趟前等 0.12s，每趟线性，趟间停 0.32 × 时长；只在激活、距视口 64px 内、可见且 Full 时运行，暂停后从原处继续。
- `IsActive` 关闭：飞行中的光带以 enter 曲线在 min(0.42s, 剩余 × 0.5) 内滑出，文字 0.36s 变实；开启：0.24s 回到底色。标签变化：新文字 0.3em + BlurSoft 升入、旧文字 −0.24em + BlurSubtle fast 离开，宽度在交换期间以 morph 弹簧过渡。无障碍：Name = Text，激活时 ItemStatus "Busy"。高对比模式未单独处理。

## 5. 实现顺序与依赖
- 阶段 P1：TextMorph。ColorPicker（阶段 P6）硬依赖它；随它与同阶段的 CopyButton 一起落地 P4 字形形变（`GlyphMorphPresenter`），P5 宽度弹簧已随 Button（阶段 P0）落地。
- 阶段 P3：TextReveal 先行（P20 的首个使用者，同时定稿 `StaggeredText`）→ InViewTitle（复用 `StaggeredText`，加 P18 与 S6，两者已随 AnimatedCounter 在阶段 P1 落地）→ TextShimmer（P2、P21 已随 Button 落地，另需 S6）。
- 跨大类：P20 之后还被 Card（阶段 P5）与 ConfirmMorph（阶段 P7）使用；P4 还被 ActionButton、Input、Textarea、ExpandableCard、SplitButton、ColorPicker 使用，字形形变实现应保持通用。

## 6. 相关文档
- [组件总清单](../overview.md) · [规格模板](../spec-template.md)
- [动效原语目录](../../architecture/motion/primitives-catalog.md) · [Token 目录](../../architecture/theming/token-catalog.md)
- [浮层与输入](../../architecture/overlays-and-input.md) · [路线图](../../roadmap/overview.md)
- 相邻大类：[图表](../charts/overview.md) · [按钮](../buttons/overview.md) · [进度](../progress/overview.md)
