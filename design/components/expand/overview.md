# 展开与滚动（expand）

> 展开与滚动类组件在原地给内容更多空间：覆盖式滚动容器、手风琴、可展开卡片、可拖拽分栏与折叠长文本，尺寸变化都是真实宽高的动画，并且可以随时反向。

## 1. 大类定位
- 解决：内容超出容器时如何保持页面结构（ScrollArea 的覆盖式滚动条与边缘渐隐），以及如何按需展开（Accordion、ExpandableCard、ExpandableText）或在并排面板之间交换空间（ResizablePanels）。
- 边界：同级视图切换用 Tabs（Navigation）；逐张翻页用 Carousel、同一位置浏览多张卡片用 CardStack（Media）；覆盖页面的侧边面板用 Drawer（Overlays）。
- 共同特征：尺寸动画是真实的宽高而不是缩放，文字与圆角不拉伸；中途再次点击从当前尺寸与速度反向；折叠的内容保持挂载但不可交互（ExpandableText 例外：全文始终留在可访问性树中）。

## 2. 组件一览
| 组件 | 类名 | Arc 页面 | 阶段 | Arc 依赖 | 建议基类 | 原语 |
|---|---|---|---|---|---|---|
| 滚动区域 | `ScrollArea` | [scroll-area](https://uiarc.dev/components/scroll-area) | P4 | 无 | Avalonia ScrollViewer | P27 P29 P30 |
| 手风琴 | `Accordion` | [accordion](https://uiarc.dev/components/accordion) | P4 | 无 | ItemsControl | P6 |
| 可展开卡片 | `ExpandableCard` | [expandable-card](https://uiarc.dev/components/expandable-card) | P4 | 无 | HeaderedContentControl | P4 P6 P9 |
| 可调分栏 | `ResizablePanels` | [resizable-panels](https://uiarc.dev/components/resizable-panels) | P7 | 无 | TemplatedControl（自定义 Panel） | P12 P26 |
| 可展开文本 | `ExpandableText` | [expandable-text](https://uiarc.dev/components/expandable-text) | P4 | 无 | TemplatedControl | P2 P6 P29 P30 |

## 3. 共享约定
- **展开收起（P6）**：高度走 smooth（0.4s、bounce 0，不回弹）；中途切换从当前高度与速度重定向，不重启。ExpandableText 例外，使用按距离计算时长的缓出补间（见 4.5）。
- **折叠态内容**：保持挂载以便中途反向；折叠完成后不可聚焦、不进可访问性树（Accordion 折叠完成后隐藏，ExpandableCard 与 ResizablePanels 为 inert）。Avalonia 没有 inert，统一用 IsHitTestVisible=false、子树 KeyboardNavigation.TabNavigation=None 与 AutomationProperties.AccessibilityView=Raw 的组合实现，作为本大类共用的内部辅助。
- **展开指示**：Chevron 旋转 180°；Accordion 用 snappy，ExpandableCard 与盒子同一个弹簧，ExpandableText 由同一进度值驱动。图标 16–18px，TextMuted，展开后为 Foreground。
- **触发器**：整行（或整个头部）都是触发器，具 Button 语义并暴露 ExpandCollapse（IExpandCollapseProvider）；悬停变色只对鼠标指针生效。
- **尺寸测量**：内容尺寸变化由 S5 观察；ScrollArea 与 ExpandableText 在宽度、字体或内容变化后重新判断是否溢出，不需要调用方测量。
- **动效等级**：Reduced 直接应用终态（ScrollArea 去掉滚动条过渡与平滑翻页）；Off 同。

## 4. 组件

### 4.1 ScrollArea（Scroll area）
`slug: scroll-area` · 阶段 P4 · Arc 依赖：无 · 建议基类：Avalonia ScrollViewer

**Arc 行为摘要**：保留原生滚动的容器，叠加细的覆盖式滚动条，并且只在内容溢出时出现边缘渐隐。用于可能溢出的面板、侧栏与弹层，以及需要滚轮横向滚动与吸附点的横向卡片 / 标签条。逐张翻页用 Carousel；不要包裹整个页面；大数据量表格应使用自带虚拟化滚动的表格控件。

**Arc API 要点**：
| Arc 属性 | 类型 / 默认值 | 建议 Avalonia 属性（类型） |
|---|---|---|
| `orientation` | `"vertical" \| "horizontal" \| "both"`，`"vertical"` | `Orientation`（enum ScrollAreaOrientation：Vertical / Horizontal / Both；内部设置继承的 Horizontal / VerticalScrollBarVisibility） |
| `fade` | `number`，`28`（px，0 关闭渐隐） | `FadeLength`（double，28） |
| `scrollbars` | `"auto" \| "always"`，`"auto"` | `OverlayScrollbarMode`（enum：Auto / Always，避免与继承的 ScrollBarVisibility 混淆） |
| `hideDelay` | `900`（ms） | `HideDelay`（TimeSpan，900ms） |
| `maxHeight` | CSS 长度 | 使用继承的 `MaxHeight` |
| `snap` | scroll-snap-type，如 `"x mandatory"` | 继承的 `HorizontalSnapPointsType` / `VerticalSnapPointsType` |
| `wheelToHorizontal` | `true` | `IsWheelToHorizontalEnabled`（bool，true） |
| `onEdgeChange` | `({ top, bottom, left, right }) => void` | `EdgesChanged` 事件 + 只读 `Edges`（ScrollAreaEdges） |

`label` 映射为 AutomationProperties.Name（设置后视口成为具名区域），`onScroll` 映射为继承的 ScrollChanged。

**交互与键盘**：
- 视口可聚焦（在 Tab 顺序中），聚焦后方向键、PageUp / PageDown、Home / End、Space 原生滚动。
- 拖动拇指按比例映射滚动距离；按下轨道向指针方向翻页视口的 90%，平滑滚动（Reduced 下瞬时）。
- 横向区域中竖直滚轮转为横向滚动，只在还能滚动时截获，到达两端后把滚轮交还外层。

**可访问性**：
- 滚动保持原生语义：继承 ScrollViewer 的自动化（IScrollProvider）；覆盖层的轨道与拇指为 Raw，真正的控件仍是 ScrollViewer。
- 设置 Name 时视口作为具名区域（Group 类型 + Name）；未设置时由周围上下文说明用途。

**动效**：
- 滚动条在滚动或悬停时以 fast 淡入，停止滚动后保留 HideDelay（900ms），再以 considered（480ms）淡出。
- 拇指在指针下从 5px 变粗到 7px，颜色从 Foreground 30% 变为 46%（fast）（P27）。
- 每条边的渐隐长度等于该边之外剩余的内容（上限 FadeLength），因此随最初几像素的滚动逐渐出现（P29、P30）；拇指位置与长度（最小 28px）随滚动直接写入（P30）。
- Reduced：去掉滚动条过渡，轨道翻页改为瞬时。

**Avalonia 实现要点**：
- 派生自 ScrollViewer，自定义 ControlTheme：隐藏原生滚动条外观，叠加命中区 11px 的覆盖轨道（拇指 5px、距边 3px，悬停时 7px、距边 2px）；Orientation=Both 时两条轨道互让 13px。ScrollViewer.AllowAutoHide 可作参考，但淡入淡出时机按 Arc 自行控制。
- 边缘渐隐用视口的 OpacityMask（LinearGradientBrush），在 ScrollChanged 中只更新渐变停靠点、不触发布局；Both 时需两条渐变相交（两层蒙版或自绘）。
- 滚轮转横向：处理 PointerWheelChanged，只在还有横向余量时标记 Handled，与 IsScrollChainingEnabled 的语义一致。
- EdgesChanged 只在某条边状态翻转时触发，可用于"更多内容"提示或在 bottom 变为 false 时加载更多。
- 服务：S4、S5（内容尺寸与重排）、S12（隐藏延迟计时）。

**AtomUI 参考**：ScrollViewer（仅供学习行为与边界，不复用其实现）。

**风险与未决**：
- 主数据中的 P21（持续循环）在 Arc ScrollArea 中找不到对应行为（滚动条只是淡入淡出），疑为多余，建议主笔核对。
- 触摸滚动依赖 Avalonia 的 ScrollGestureRecognizer 与惯性（IsScrollInertiaEnabled），与浏览器原生动量在手感上可能有差异，需实机对比。
- 复核结论（汇总评审）：移除 P21，已同步到[组件总清单](../overview.md)。

### 4.2 Accordion（Accordion）
`slug: accordion` · 阶段 P4 · Arc 依赖：无 · 建议基类：ItemsControl

**Arc 行为摘要**：单开、可全部收起的问答行列表（Arc 基于 Radix Accordion）。用于一次只展开一个答案的 FAQ、用短问题分组长内容的设置或帮助页，页面级 FAQ 用 `size="lg"`。单个独立的披露用 ExpandableCard，同级视图切换用 Tabs。Arc 只支持单开模式。

**Arc API 要点**：
| Arc 属性 | 类型 / 默认值 | 建议 Avalonia 属性（类型） |
|---|---|---|
| `items`（必填） | `{ title: string; content: ReactNode }[]` | 继承的 `ItemsSource`（项为 AccordionItem：Header + Content，或配合数据模板） |
| `defaultOpen` | `number`，`0`；`-1` 全部收起 | `ExpandedIndex`（int，0；−1 表示全部收起，双向） |
| `size` | `"md" \| "lg"`，`"md"` | `Size`（enum：Medium / Large，Medium） |

Arc 只有非受控的 defaultOpen；Avalonia 侧以双向 ExpandedIndex 同时满足两种用法。

**交互与键盘**：
- Enter / Space 切换聚焦行；ArrowDown / ArrowUp 在各行触发器之间移动焦点，Home / End 跳到首 / 末触发器。
- 打开另一行时当前行收起（单开）；再次点击已展开的行将其收起。

**可访问性**：
- 每行触发器为带 ExpandCollapse 的 Button，置于标题语义中（AutomationProperties.HeadingLevel）；面板以 LabeledBy 关联触发器。
- 折叠完成后面板隐藏、离开可访问性树，但保持挂载；Chevron 为 Raw。

**动效**：
- 面板高度走 smooth 弹簧、不回弹；透明度展开时 standard + enter，收起时 fast + standard（P6）。
- 答案从 −6px 与 blur subtle（2px）下滑落位：位移用 smooth，模糊 standard + enter；中途切换从当前高度重定向。
- Chevron 以 snappy 旋转 180°。
- Reduced：同样的终态一步到位。

**Avalonia 实现要点**：
- 项容器为内部 `AccordionItem`（HeaderedContentControl），模板部件 `PART_Header`（Button）、`PART_Panel`（高度动画宿主）、`PART_Chevron`（17px）；语义部件 `arc.item`、`arc.trigger`、`arc.panel`。
- 尺寸：md 触发器最小高度 ControlSizeLG（50）、text-sm 字重 500；lg 为 76px、text-lg，答案 text-base、行高 1.65、最大行长 62ch；视口 < 520px 时 lg 降为 68px、答案 text-sm。
- 列表顶部与每行底部各一条 1px Border；面板文字 TextSecondary。
- 所有面板常驻（Arc 同样如此），重内容应在面板内部延迟加载。
- 服务：S1、S2、S5（auto 高度目标）、S7、S9（触发器间导航）。

**AtomUI 参考**：Collapse(IsAccordion)（仅供学习行为与边界，不复用其实现）。

**风险与未决**：
- 主数据只列 P6；答案的 6px 下滑 + 模糊与 Chevron 旋转也是动效，应归入 P6 / P2 的组合还是单列，需原语目录确认。
- Avalonia 没有 ch 单位，"62ch" 按 "0" 字形宽度换算还是改用固定宽度，需在规格中决定。

### 4.3 ExpandableCard（Expandable card）
`slug: expandable-card` · 阶段 P4 · Arc 依赖：无 · 建议基类：HeaderedContentControl

**Arc 行为摘要**：头部可切换的卡片，在下方展开详情面板，让紧凑的卡片按需获得更多空间。用于套餐、订单或日志条目这类单张摘要卡片；摘要行更新时变化的词原地滚入。互斥的多段内容用 Accordion，无可展开内容用 Card，同一位置浏览多张卡片用 CardStack。

**Arc API 要点**：
| Arc 属性 | 类型 / 默认值 | 建议 Avalonia 属性（类型） |
|---|---|---|
| `title`（必填） | `string` | 继承的 `Header`（字符串时渲染为卡片标题） |
| `description` | `string`，变化的词滚入 | `Description`（string?） |
| `children`（必填） | 详情内容 | 继承的 `Content` |
| `defaultExpanded` | `false` | `IsExpanded`（bool，false，双向） |
| `width` | `number`（px），省略时填满容器 | `CollapsedWidth`（double?） |
| `expandedWidth` | `number`（px），默认同 width | `ExpandedWidth`（double?） |

Arc 的状态只在内部维护、没有受控属性；Avalonia 侧以双向 IsExpanded 兼顾。

**交互与键盘**：
- 头部按钮 Enter / Space 切换；焦点在卡片内时 Escape 收起，并把焦点还给头部。
- 整个头部都是触发器，最小高度 64px，便于触摸。
- 动画途中再次点击从当前尺寸与速度反向；反向一次进行中的展开时跳过"详情先淡出"的等待。

**可访问性**：
- 头部为 Button，暴露 ExpandCollapse；面板以 LabeledBy 关联。
- 摘要行的滚动词为 Raw，另有读屏专用的完整文本。
- 收起的面板不可交互（inert 组合，见共享约定）。

**动效**：
- 开合是一次形变：宽与高在同一个 smooth 弹簧上一起变化、不过冲，箭头随同一弹簧旋转（P9、P6）。
- 详情在盒子腾出空间后淡入（standard + standard，延迟 0.12s）；收起时详情先在 0.1s 内淡出，盒子 0.06s 后开始收缩。详情始终按展开宽度布局，文字不重排也不拉伸。
- 摘要行中变化的词从 0.35em + blur soft 升入，旧词以 −0.3em + blur subtle 退出，未变化的词保持不动（P4）。
- Reduced：终态立即应用，摘要直接替换。

**Avalonia 实现要点**：
- 卡片表面：RadiusSurface（34）、Surface、1px Border、ShadowResting；头部悬停与按下为 SurfaceMuted，内边距 16px / 20px；面板底色 SurfaceMuted，顶部分隔用内阴影线而非边框，保证收起后真正为 0 高。
- 宽度以 px 由 S2 动效值驱动，首次测量直接落位、之后带速度弹簧；宽高是真实尺寸动画（不用 ScaleTransform），因此详情宜简短。
- 外层居中轨道用 S5 测量可用宽度，宽度上限不超过容器（手机上即全宽）。
- 摘要按空白分词，每词一个文本元素滚动。
- 服务：S1、S2、S3、S5、S7。

**AtomUI 参考**：Expander、Card（仅供学习行为与边界，不复用其实现）。

**风险与未决**：
- 中文没有空格分词，按空白切分时 zh-CN 摘要会整行一起滚动；是否按字或标点拆分需在规格中定义。
- 真实宽高动画让详情每帧参与布局；需验证 Avalonia 在复杂详情下的帧耗时，必要时把详情布局宽度锁定为展开宽度（Arc 同样如此）。

### 4.4 ResizablePanels（Resizable panels）
`slug: resizable-panels` · 阶段 P7 · Arc 依赖：无 · 建议基类：TemplatedControl（自定义 Panel）

**Arc 行为摘要**：横向分栏视图，通过拖动面板之间的分隔条交换空间，分隔条可键盘操作；可折叠面板可拖过最小值或朝边缘甩动来折叠。用于编辑器、邮件、仪表盘布局，可经 onLayoutChange 持久化。覆盖页面的面板用 Drawer，同一时间只显示一个面板用 Tabs；Arc 只支持横向，不适用于上下分割。

**Arc API 要点**：
| Arc 属性 | 类型 / 默认值 | 建议 Avalonia 属性（类型） |
|---|---|---|
| `children`（必填） | 按顺序的 ResizablePanel，集合应保持稳定 | `Panels`（ResizablePanel 集合，作为内容子项） |
| `label` | `string`，分组的 aria-label | `AutomationProperties.Name` |
| `onLayoutChange` | `(sizes: number[]) => void`，调整结束后，百分比 | `LayoutChanged` 事件（`IReadOnlyList<double>`，百分比） |
| ResizablePanel `id` / `label`（必填） | `string`，供分隔条引用与命名 | `ResizablePanel.Id` / `Label`（string） |
| `defaultSize`（必填） | 相对份额，不必合计 100 | `DefaultSize`（double） |
| `minSize` / `maxSize` | `80`（px，开始产生阻力的宽度）/ 无 | `MinSize`（double，80）/ `MaxSize`（double?） |
| `collapsible` / `defaultCollapsed` | `boolean` / `boolean`（需 collapsible） | `IsCollapsible`（bool）/ `IsCollapsed`（bool，双向） |

**交互与键盘**：
- 聚焦分隔条后 ArrowLeft / ArrowRight 每次移动 16px，Shift 为 64px；越过极限时折叠可折叠面板。
- Home / End 移到最小 / 最大；Enter 折叠或恢复相邻的可折叠面板；拖动中 Escape 退回起点。
- 指针拖动 1:1 跟手，越界产生橡皮筋（最大拉伸 44px）；松手时按速度投影（衰减率 0.99）判断甩动，甩向边缘折叠、甩回恢复；双击分隔条重置布局。

**可访问性**：
- 分隔条可聚焦，peer 用 Thumb 类型并实现 IRangeValueProvider：当前值、最小值、最大值均为百分比；值文本报份额，或"面板已隐藏，按 Enter 显示"。
- 分组为 Group 并以 label 命名；折叠的面板不可交互、不进可访问性树。

**动效**：
- 拖动时直接写宽度；松手或折叠时以 smooth 弹簧落位并保留速度，键盘步进用 snappy（P12）。
- 面板被压到最小值以下时内容裁剪并按比例淡出；折叠后分隔条上的恢复标签以 snappy 弹出，透明度与模糊 fast + enter（P26）。
- Reduced：面板直接跳到目标，恢复标签只淡入。

**Avalonia 实现要点**：
- 自定义 Panel 按份额与最小 / 最大约束分配宽度；分隔条是 1px 线 + 更宽的命中区，悬停或拖动时显示握把，折叠后显示恢复标签；容器尺寸变化时按份额重算（S5）。
- 拖动用指针捕获与 S10 手势，保留最近 16 个采样点计算速度；拖动只改宽度、不重建面板内容，弹簧只在松手或折叠时运行。
- 需要父级给出确定高度（Arc 同样要求）；窄屏不自动堆叠，由调用方改用 Tabs。
- 持久化：LayoutChanged 给出百分比，调用方存储后经 DefaultSize 恢复，组件本身不读写存储。
- 服务：S1、S2、S5、S7、S10。

**AtomUI 参考**：Splitter（仅供学习行为与边界，不复用其实现）。

**风险与未决**：
- 是否增加竖向分割（桌面布局常见上下分栏）属于偏离 Arc 的扩展，需单独决策。
- 甩动判定与橡皮筋曲线依赖输入采样频率，需在三平台的触控板与鼠标上调参验证。

### 4.5 ExpandableText（Expandable text）
`slug: expandable-text` · 阶段 P4 · Arc 依赖：无 · 建议基类：TemplatedControl

**Arc 行为摘要**：长文本按行数截断，末行覆盖柔和渐隐；Read more 平滑展开到全高，Show less 收起，切换按钮只在文本确实溢出时出现。用于评论列表、商品或个人简介，以及长文本会让卡片高低不齐的场景。成组标题内容或 FAQ 用 Accordion，结构化的隐藏内容用 ExpandableCard，必须固定高度时用 ScrollArea；法律条款、错误详情等必须阅读的内容不要截断。

**Arc API 要点**：
| Arc 属性 | 类型 / 默认值 | 建议 Avalonia 属性（类型） |
|---|---|---|
| `children`（必填） | 文本，可含段落与内联链接 | `Text`（string）或 `Inlines`（InlineCollection，含链接时使用） |
| `lines` | `number`，`3`，按文本自身行高计 | `Lines`（int，3） |
| `expanded` / `defaultExpanded` / `onExpandedChange` | 受控 / `false` / 回调 | `IsExpanded`（bool，false，双向）+ `IsExpandedChanged` 事件 |
| `moreLabel` / `lessLabel` | `"Read more"` / `"Show less"` | `MoreLabel` / `LessLabel`（string，默认值来自 xlf） |

**交互与键盘**：
- Tab 先到达文本中的链接，再到切换按钮；聚焦到截断线以下的链接会自动展开文本。
- 切换按钮 Enter / Space 展开或收起；按钮命中高度 28px，位于文本下方左对齐。
- 从页面很下方收起时，同时把文本起点滚回视口。
- 行数建议：评论等密集列表 2 行，商品或个人简介 3–4 行。

**可访问性**：
- 截断只是视觉效果：完整文本始终在可访问性树中，读屏无需按按钮即可读全文。
- 切换按钮暴露 ExpandCollapse；两个标签叠放在同一格中交叉淡化，隐藏的那个为 Raw，按钮名称始终是当前标签。
- 截断区内的链接获得键盘焦点即展开，焦点永远不会被隐藏。

**动效**：
- 高度、渐隐蒙版与 Chevron 由同一进度值驱动，始终同步；中途点击从当前高度反向（P6）。
- 展开 500–600ms、收起 400–450ms，随距离略增（展开 0.5 + min(d / 1600, 0.1) 秒，收起 0.4 + min(d / 2000, 0.05) 秒），缓出曲线 (0.32, 0.5, 0.2, 1)，不过冲。
- 末行渐隐（1.6 倍行高）在展开时消散、接近截断时才回来，新露出的行从柔和边缘中浮现（P29）。
- 两个标签以 4px 漂移与 2px 模糊交叉淡化，按钮宽度不变（P2）；从页面下方收起时同步滚回文本起点（P30）。
- Reduced：立即展开或收起。

**Avalonia 实现要点**：
- 静止时截断高度 = Lines × 行高（Arc 静止时是纯 CSS 的 max-height: N lh），宽度与字体变化无需代码介入；只有动画期间才设置显式 Height 与 OpacityMask，展开完成后去掉蒙版。
- 溢出判断：比较完整文本的测量高度与截断高度，S5 在宽度或字体变化时重算，决定按钮是否出现；内容内部需保持单一行高（标题或大图会破坏按行截断）。
- 曲线 (0.32, 0.5, 0.2, 1) 与按距离计算的时长不在全局缓动 / 时长 Token 中，作为本控件常量（或 Own Token）定义。
- 收起滚回：对文本起点调用 BringIntoView，Reduced 下瞬时。
- 服务：S4、S5、S7。

**AtomUI 参考**：—（AtomUI 无对应控件）。

**风险与未决**：
- 主数据列了 P5（宽度随内容弹簧），但 Arc 明确两个标签同格叠放、按钮宽度不变，P5 可能多余，建议主笔核对。
- Avalonia 的内联元素不可聚焦：截断文本中可聚焦的链接需用 InlineUIContainer 承载 HyperlinkButton，"聚焦即展开"要基于这些子控件的 GotFocus 实现。
- 复核结论（汇总评审）：移除 P5，已同步到[组件总清单](../overview.md)。

## 5. 实现顺序与依赖
- **P4 批次**（与 Navigation 同期）：
  - ScrollArea 最先，弹层、菜单、侧栏、TimePicker 列表与 Dialog 正文溢出都可复用；P27（AvatarGroup，P2）、P29（Button，P0）已落地，P30 与同阶段的 Tabs 协调首个实现。
  - Accordion：P6 已由 Calendar（P1）落地，可最早完成。
  - ExpandableCard：P9（表面形变）的首个使用者，DateRangePicker、RichTextEditor、CookieConsent 等后续复用；P4 由 CopyButton（P1）先落地。
  - ExpandableText：P2、P5、P29 已就绪，P30 与 ScrollArea 同批。
- **P7** ResizablePanels：依赖 S10 手势与 P12（UserMenu，P5 首次落地）、P26（Avatar，P1）。
- 共用前置：S5 尺寸观察贯穿全部组件，应在 P4 前就绪；"inert 组合"辅助随 Accordion / ExpandableCard 首次实现并沉淀到 Core。

## 6. 实现记录

### 6.1 ScrollArea
- 派生 `ScrollViewer` 的自有模板：无原生滚动条，叠加两条内部 `ScrollAreaScrollbar`（命中区 11px、自绘药丸 5→7px、30%→46% 前景色、最短 28px），按需相互让出 13px；拖动拇指按比例映射，按轨道向指针方向翻 90% 视口（smooth 弹簧，Reduced 瞬时）。
- 显隐由代码驱动：滚动、拖动或鼠标悬停时 fast 淡入，停止后保留 `HideDelay`（900ms）再以 considered 淡出；`OverlayScrollbarMode=Always` 常显。渐隐用两层包裹 Border 的 OpacityMask（竖向在内、横向在外，Both 时相乘），每条边长度 = min(FadeLength, 该边剩余内容)，只在长度变化时发布新的不可变画刷。
- `Orientation`、`FadeLength`、`HideDelay`、`IsWheelToHorizontalEnabled`（横向区域把竖向滚轮转为横向，两端交还外层）、`Label`、只读 `Edges` + `EdgesChanged`；可聚焦，方向键 40px、PageUp/PageDown/Space 90% 视口、Home/End。

### 6.2 Accordion
- `Accordion : ItemsControl` + `AccordionItem : HeaderedContentControl`（内部触发器 `AccordionTrigger` 是带 ExpandCollapse 的按钮，HeadingLevel 3）；`ExpandedIndex` 双向（默认 0，−1 全部收起）、`ExpandMode` Single（Arc）/ Multiple（新增）、`Size` Medium / Large、`ContentTemplate`。
- 高度用 Core `SpringSizeDecorator`（smooth）。注意：Core 版本把无穷大的可用高度比较成"尺寸变化"而直接跳变，本类别用 `ExpandHeightDecorator` 子类把无穷约束换成 `double.MaxValue` 规避（已向集成者提交 Core 修复）。透明度展开 standard+enter、收起 fast，答案 −6px 走 smooth 并带 2px 模糊；Chevron 以 snappy 旋转。Reduced / Off 一步到位。
- 收起的答案立即不可交互（`ExpandInert`），焦点在内部时回到问题按钮；Up/Down 循环、Home/End 在触发器间移动。Large 的 62ch 按 Inter "0" 宽 0.6em 折算为 595px；视口 < 520px 的降级未实现。

### 6.3 ExpandableCard
- `ExpandableCard : HeaderedContentControl`：`Header`、`Description`、`IsExpanded`（双向）、`CollapsedWidth` / `ExpandedWidth`（NaN = 填满容器）。控件自身测量居中轨道：宽度、面板高度（内部 `ExpandableCardPanel`）与 Chevron 用同一 smooth 弹簧规格（关闭且非反向时延迟 60ms），首次测量与容器尺寸变化直接落位；详情始终按展开宽度布局。
- 详情展开时延迟 120ms 以 standard 淡入，收起时 100ms 先淡出；收起后详情 inert，焦点回到头部；Escape 从卡片内任意处收起。摘要行由 `ExpandableCardSummary` 自绘逐词滚动（变化的词 0.35em + 4px 模糊升入，旧词 −0.3em + 2px 模糊离开，模糊半径按 0.25px 量化缓存）；中文按空白分词，整句一起滚动。

### 6.4 ResizablePanels
- `ResizablePanels : Control`，内容为 `Panels`（`ResizablePanel : ContentControl`：Id、Label、DefaultSize、MinSize=80、MaxSize、IsCollapsible、IsCollapsed 双向）；分隔条是内部自绘的 `ResizablePanelsHandle`（1px 线 + 13px 命中区，折叠后向可见侧扩到 31px），握把与恢复标签由 MotionValue 驱动。
- 布局按 CSS flex（grow = 尺寸、min/max 冻结重分配）；首次交互或容器变宽后把尺寸同步为屏幕 px；拖动 1:1 跟手、越界橡皮筋 44px、越过 collapseAt 折叠、松手按 0.99 衰减投影判断甩动并以 smooth 弹簧带指针速度落位；键盘 16/64px（snappy）、Home/End、Enter、Escape 回退、双击重置。
- 持久化：`Sizes`（双向，百分比）在每次落位后更新，设置它即按份额恢复；`LayoutChanged` 同时给出百分比。拖动期间面板内容不接受指针；折叠面板 inert。恢复标签的弹出没有模糊（只做缩放与透明度）。

### 6.5 ExpandableText
- `ExpandableText : TemplatedControl`：`Text`（内容属性）或 `Inlines`（可含 `InlineUIContainer` 链接）、`Lines`=3、`IsExpanded` 双向 + `ExpandedChanged`、`MoreLabel` / `LessLabel`、只读 `IsOverflowing`、`LineHeight`（主题给 text-sm × 1.4）。
- 内部 `ExpandableTextViewport` 静止时高度 = Lines × 行高或全文，宽度 / 字体变化自动重判溢出；切换时一个进度值驱动高度、末行渐隐（1.6 行，(1−p)^1.35）与 Chevron；时长按距离 0.5+min(d/1600,0.1)s / 0.4+min(d/2000,0.05)s，曲线 (0.32,0.5,0.2,1)，中途反向从当前高度重算。Reduced / Off 立即。
- 两个标签同格叠放，在主题中以 4px 漂移 + 2px 模糊交叉淡化；截断线以下的链接获得焦点时自动展开；收起时 `BringIntoView` 文本起点（瞬时）。

## 7. 相关文档
- [组件总清单](../overview.md)
- [组件规格模板](../spec-template.md)
- [动效原语目录](../../architecture/motion/primitives-catalog.md)
- [Token 目录](../../architecture/theming/token-catalog.md)
- [浮层与输入](../../architecture/overlays-and-input.md)
- [路线图](../../roadmap/overview.md)
- Arc 原页面见第 2 节表格中的链接。
