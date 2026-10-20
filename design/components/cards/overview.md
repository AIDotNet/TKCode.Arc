# 卡片（Cards）

> Arc Data 分组中承载一组相关内容的容器：内容卡片、指标卡片与空状态。

## 1. 大类定位
- Card 用于网格中可浏览的条目（项目、房源、帖子），可选地从卡片本身长成快速预览对话框；MetricCard 给一个需要上下文的数字（运行时间、订单）配上里程表式滚动数字与变化芯片；EmptyState 在没有内容时给出有用的下一步（空列表、零搜索结果、首次使用）。
- 边界：无对话框的原地展开用 Expand 的 ExpandableCard；没有卡片可以长出的内容直接用 Overlays 的 Dialog；数据仍在加载时用 Progress 的 Skeleton；页面仍有内容时的错误用 Messages 的 Alert；只看趋势用 Charts 的 Sparkline；不需要卡片外框的数字用 Charts 的 AnimatedCounter。
- 共同特征：`Surface` 底、`Border` 细边与 Arc 大圆角；文字变化逐词或整行从柔和模糊中滚入，容器高度或芯片宽度随内容弹簧；控件保持挂载、修改属性即可在状态之间形变。

## 2. 组件一览
| 组件 | 类名 | Arc 页面 | 阶段 | Arc 依赖 | 建议基类 | 原语 |
|---|---|---|---|---|---|---|
| 卡片 | Card | [card](https://uiarc.dev/components/card) | P5 | 无 | ContentControl | P8 P11 P20 P27 |
| 指标卡片 | MetricCard | [metric-card](https://uiarc.dev/components/metric-card) | P2 | AnimatedCounter | TemplatedControl | P3 P5 P14 P18 |
| 空状态 | EmptyState | [empty-state](https://uiarc.dev/components/empty-state) | P3 | 无 | TemplatedControl | P2 P3 P6 P26 |

## 3. 共享约定
- **外观**：Card 圆角 `RadiusPanel` 26、悬停时边框 `BorderStrong` 加 `ShadowRaised`；MetricCard 圆角 `RadiusSurface` 34、内边距 24、`ShadowResting`；EmptyState 本身无外框，图标块 48×48、`RadiusPanel`、`SurfaceMuted`。
- **文字滚入**：新文字从 soft 4px 模糊上移 0.3em 进入（standard 240ms，enter 缓动），旧文字上移 0.3em 并带 subtle 2px 模糊离开（fast 160ms）；Card 的 status 按词交错 40ms，MetricCard 按数值变化方向决定上移或下落。淡出中的旧文字对辅助技术隐藏。
- **只在内容变化时弹簧**：高度 / 宽度弹簧只在内容变化后的短窗口内启用（EmptyState 为 120ms），字体加载等被动重排直接跳变。
- **语义**：Card 与 MetricCard 为文章语义（Group），标题 `HeadingLevel` 3；EmptyState 为区域语义；只有 Card 的 status 主动经 Polite 状态区播报，MetricCard 与 EmptyState 需要时由外层设置 `AutomationProperties.LiveSetting`。
- **悬停**：Card 的上浮与媒体放大只对鼠标生效，触摸与笔不触发。
- **动效等级**：Reduced 去掉上浮、放大、共享元素形变与位移，改为淡入淡出、尺寸直接跳变；Off 立即。

## 4. 组件
### 4.1 Card（Card）
`slug: card` · 阶段 P5 · Arc 依赖：无 · 建议基类：ContentControl

**Arc 行为摘要**：一组相关内容与动作的容器，用于网格中可浏览的条目（项目、房源、帖子），可带媒体、归属者署名和原位更新的状态行；设置 details 后可从卡片本身长成更大的快速预览对话框，不离开页面。无对话框的原地展开用 ExpandableCard，数字用 MetricCard，没有卡片可以长出时直接用 Dialog。

**Arc API 要点**：
| Arc 属性 | 类型 / 默认值 | 建议 Avalonia 属性（类型） |
|---|---|---|
| `title`（必填） | string；设置 details 时成为对话框触发器 | `Title`（string） |
| `description` / children | 标题下的辅助行 / 描述与页脚之间的额外内容 | `Description`（string）/ `Content`（object） |
| `media` | 顶部图像或视觉，悬停时轻微放大 | `Media`（object） |
| `action` / `avatar` / `meta` | 页脚尾部控件 / 页脚前导小视觉 / 归属者 | `Action` / `Avatar` / `Meta`（object） |
| `status` | string，如 "Updated 2 hours ago"，变化时逐词滚入并礼貌播报 | `Status`（string） |
| `details` | 快速预览内容；设置后卡片可展开为对话框 | `Details`（object） |
| `open` / `defaultOpen` / `onOpenChange` | boolean，默认 false | `IsDetailsOpen`（bool，双向）+ `DetailsOpenChanged` 事件 |

**交互与键盘**：设置 details 时标题上 Enter / Space 打开快速预览；Escape 关闭并把焦点还给标题；预览打开期间 Tab 被困在对话框内。

**可访问性**：
- 根为文章语义（Group），标题 `HeadingLevel` 3；快速预览以标题与描述作为对话框的 Name 与 HelpText。
- 只有标题变成按钮，嵌套动作保持各自可聚焦；status 变化经 Polite 状态区朗读；媒体图片要提供替代文本或标为装饰，仅图标的动作要设置 Name。

**动效**：
- 鼠标悬停时卡片以 snappy 上浮 2px，边框与阴影在 standard 240ms 内加深；媒体以 960ms 缓慢放大到 1.04，离开时 480ms 回落（P27）。
- 快速预览共享元素：表面、照片、标题、署名在同一个临界阻尼弹簧上飞行（smooth，打开 visualDuration 0.3、关闭 0.26），圆角同步形变；details 在 120ms 后上移 6px 淡入（standard 240ms）（P8）。
- 遮罩 standard 240ms 淡入、exit 180ms 淡出，带 `BackdropBlurDialog`（7px）背景模糊；关闭按钮 120ms 后从 0.9 缩放出现，按下缩到 0.94（P11）。
- status：旧行带 subtle 模糊上移离开（fast），新词从 soft 模糊上移 0.3em 依次升起，按 word 40ms 交错（P20）。
- Reduced：去掉上浮、放大与形变，对话框直接淡入淡出（instant 120ms）；Off 立即。

**Avalonia 实现要点**：
- 模板部件 `PART_Media`、`PART_Title`（有 Details 时为按钮）、`PART_Status`、页脚的 `PART_Avatar` 与 `PART_Action`；快速预览复用 Overlays 的 Dialog 表面（同为 P5，须先落地），面板宽 min(30rem, 窗口宽减边距)，高度不超过窗口。
- 共享元素飞行用 SharedElementScope（本组件是 P8 的首个使用者），模态浮层、焦点陷阱与归还用 S8，退场用 S3；Token 圆角需以像素读回才能参与形变。卡片填满网格单元（最小宽 0）。

**AtomUI 参考**：Card（仅供学习行为与边界，不复用其实现）。

**风险与未决**：7px 背景模糊在 Avalonia 中只能对页面内容施加 BlurEffect 实现，低端设备与复杂页面上可能掉帧，需与浮层文档的遮罩模糊策略一致；P8 首次落地，表面、照片、标题、署名同时飞行时的坐标换算与圆角形变需要 spike 验证。

### 4.2 MetricCard（Metric card）
`slug: metric-card` · 阶段 P2 · Arc 依赖：AnimatedCounter · 建议基类：TemplatedControl

**Arc 行为摘要**：给一个需要上下文的数字做紧凑摘要：标签、里程表式滚动数字、上下文行和可选的变化芯片；适合运行时间、订单等单个纯数字，以及实时更新、按变化方向滚动的数字。趋势比最新值重要时用 Sparkline，不需要卡片外框时用 AnimatedCounter。

**Arc API 要点**（已实现）：
| Arc 属性 | 类型 / 默认值 | Avalonia 属性 |
|---|---|---|
| `label`（必填） | string，测量对象 | `Label`（string） |
| `value`（必填） | number；首次可见时从 0 滚动，之后在值之间滚动 | `Value`（double）；另有 `Prefix`、`Decimals`（交给 AnimatedCounter） |
| `suffix` | string，如 "%" 或 " ms" | `Suffix`（string） |
| `context`（必填） | string，如 "vs last week" | `Context`（string） |
| `change` | string，如 "+12%"，省略则隐藏芯片 | `Change`（string?）；只读 `ChangeDirection`（`MetricChangeDirection`：None / Up / Down，按首字符 + / - / −） |
| — | 本库扩展 | `IsChangeGlyphVisible`（bool，false：芯片内加 TrendingUp / TrendingDown）、`Sparkline`（`IList<double>?`：数字旁的小趋势线） |

**交互与键盘**：无，非交互元素。

**可访问性**：
- 根为 Group（`ControlTypeOverride`），Name 为标签、HelpText 为"变化 上下文"；数字的朗读交给 AnimatedCounter。
- 变化芯片是纯文本，应写明方向（+ 或 −），不依赖颜色；值变化不主动播报，需要时由外层设置 `LiveSetting`。

**动效**：
- 数值交给 AnimatedCounter（`AnimateOnView`）：进入视口时首次从 0 滚动（P18），之后各位按数值变化方向转动（P14）。
- 标签、上下文与芯片文字（Messages 的 `MessagesTextSwap`）从数值变化的方向滚入：取各自文本中的第一个数字比较，变小时从上方落下，其余从下方 0.3em 升起；进入 soft 模糊 standard，离开 subtle 模糊 fast（P3）。
- 芯片宽度以 morph 弹簧适配新文字（Messages 的 `MessagesSizeFrame` 横向、不外溢裁剪，P5）；芯片出现 / 消失为 0.96 缩放加淡入的 snappy 弹簧（Core `PresenceHost` Scale）。
- 可选趋势线：首次进入视口时 0.9s in-out 描绘，数据变化 standard 交叉淡化（几何只在数据或尺寸变化时重建）。
- Reduced：数字直接跳到新值，文字 instant 淡入淡出，芯片宽度跳变，趋势线直接显示；Off 立即。

**Avalonia 实现要点**：
- 外观：`Surface` + 1px `Border`、`RadiusSurface` 34、内边距 24、`ShadowResting`；标签 TypeSM / TextSecondary，标签行下距 32；上下文 TypeSM / TextMuted、上距 16；芯片为药丸、`Border` 描边、`TextSecondary`、内边距 3 8，Up 为 Success 11% 底加 Success 字、Down 为 Danger 11% 底加 Danger 字（无描边），背景与描边 fast 过渡。
- 趋势线 96×36，描边 2.5 圆头、9% 同色填充、7px 末端圆点，颜色随方向（Accent / Success / Danger）。
- 窄布局按窗口宽 ≤ 380px 判断（与 Arc 的视口媒体查询一致）：内边距 16、芯片换到标签下方（上距 12）、标签行下距 24。
- Token：`MetricCardToken`（CardPadding、NarrowCardPadding、TopRowMargin、NarrowTopRowMargin、ContextMargin、ChipPadding、NarrowChipMargin、CopyLineHeight、SparklineWidth、SparklineHeight、UpFill、DownFill）。

**AtomUI 参考**：Statistic（仅供学习行为与边界，不复用其实现）。

**风险与未决**：曾考虑按控件自身宽度判断 380 断点，但仪表盘里一行多张卡片通常都窄于 380px，会全部变成窄布局，与 Arc 演示不符，故改为按窗口宽度；Arc 文档引用的 stat-card 不在免费目录，本库不提供对应控件；芯片的等宽数字（tabular-nums）未启用。

### 4.3 EmptyState（Empty state）
`slug: empty-state` · 阶段 P3 · Arc 依赖：无 · 建议基类：TemplatedControl

**Arc 行为摘要**：没有内容可显示时给出有用的下一步：居中的图标、标题、一两句说明与可选动作，用于空列表、零搜索结果与首次使用；保持挂载并修改属性即可在空与成功等状态之间形变。数据仍在加载时用 Skeleton，页面仍有内容时的错误用 Alert。

**Arc API 要点**（已实现）：
| Arc 属性 | 类型 / 默认值 | Avalonia 属性 |
|---|---|---|
| `title`（必填） | string；新标题升起、旧标题离开 | `Title`（string） |
| `description`（必填） | string，一两句说明为何为空与下一步 | `Description`（string） |
| `action` | 行动号召，通常是按钮 | `Action`（object：一个按钮，或一组按钮的面板） |
| `icon` | 默认 Folder；换成不同图标组件时交叉淡化 | `Icon`（object：控件，或 24×24 Lucide `Geometry`；默认 Lucide Folder、描边 1.5） |
| `label` | 区域的可访问名称 | `Label`（string） |

**交互与键盘**：自身无键盘交互，动作区的按钮各自处理。

**可访问性**：
- 根为 Region 地标，Label 为其 Name；标题 `HeadingLevel` 3。
- 图标不进入自动化树；淡出中的旧文字为 Raw。

**动效**：
- 标题或描述变化时原位滚动（Messages 的 `MessagesTextSwap`）：新文字从 soft 模糊上移 0.3em 进入（standard），旧文字带 subtle 模糊上移离开（fast），旧文字保持原宽度不重排（P2、P3）。
- 文字块高度以 smooth 弹簧适配新内容（Messages 的 `MessagesSizeFrame`；窗口缩放等被动变化直接跳变）（P6）。
- 新图标以 Core `ArcContentSwap` 的 Pop 弹出（P26）；图标块首次挂载时从 0.92 缩放淡入（considered 480ms，enter 缓动），第一帧之前就已隐藏，不闪现。
- Reduced：文字淡入淡出、高度直接跳变，图标块不做入场；Off 立即。

**Avalonia 实现要点**：
- 模板：图标块 48×48（`RadiusPanel`、`SurfaceMuted`、`Border`、`TextSecondary`）、标题（上距 20，TypeBase / Medium）、描述（上距 8、最大宽 288、居中、平衡换行）、动作区（上距 20、居中，有动作时 `:has-action`）。
- 垂直内边距按窗口宽 8% 夹在 32 到 48px 之间（对应 `clamp(space-8, 8vw, space-12)`），水平 20px。
- Token：`EmptyStateToken`（IconBlockSize、TitleMargin、DescriptionMargin、DescriptionMaxWidth、ActionMargin、InlinePadding、TitleLineHeight、DescriptionLineHeight）。

**AtomUI 参考**：Empty（仅供学习行为与边界，不复用其实现）。

**风险与未决**：Arc 文档称图标有 idle animation，源码中实际是一次性入场，已按入场实现；Arc 的图标换形是 snappy 加 2px 模糊，Core `ArcContentSwap` 的 Pop 为更慢的弹簧加 4px 模糊，差异可接受；Arc 文档提到的 onboarding-checklist 不在免费目录。

## 5. 实现顺序与依赖
- P2：MetricCard，硬依赖 Charts 的 AnimatedCounter（P1）；P3（Calendar，P1）、P14（Calendar，P1）、P18（AnimatedCounter，P1）与 P5（Button，P0）都已落地。
- P3：EmptyState，使用已落地的 P2 P3 P6 P26（Avatar，P1 首个落地 P26）；动作区通常放 Button（P0）。
- P5：Card，依赖 Overlays 的 Dialog 在同阶段先交付模态表面与遮罩；首个落地 P8（共享元素飞行），并与 Dialog、Drawer、BottomSheet、UserMenu 同为 P11（模态与边缘呈现）的首批使用者；P20（TextReveal，P3）与 P27（AvatarGroup，P2）已落地；署名常配 Avatars 的 Avatar（P1）。

## 6. 相关文档
- [组件总清单](../overview.md) · [规格模板](../spec-template.md) · [路线图](../../roadmap/overview.md)
- [动效原语目录](../../architecture/motion/primitives-catalog.md) · [Token 目录](../../architecture/theming/token-catalog.md)
- [浮层与输入](../../architecture/overlays-and-input.md)（模态浮层、焦点陷阱与归还、遮罩模糊）
- 相邻大类：[图表](../charts/overview.md) · [展开与滚动](../expand/overview.md) · [浮层](../overlays/overview.md) · [头像与徽标](../avatars/overview.md)
