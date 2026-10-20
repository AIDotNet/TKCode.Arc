# 浮层（overlays）

> 浮层类组件在当前页面之上呈现临时表面：模态的对话框、抽屉与底部面板，以及锚定在触发器上的弹出框、悬停卡片与提示。

## 1. 大类定位
- 解决：必须打断的决策（Dialog）、保持页面上下文的侧边工作区（Drawer）、移动优先的分档面板（BottomSheet）、点击打开的非模态交互面板（Popover）、悬停或聚焦时的只读预览（HoverCard）、图标按钮的一行标签（Tooltip）。
- 边界：命令列表用 DropdownMenu / ContextMenu（Menus）；行内持续消息用 Alert，瞬时结果用 Toast（Messages）。Share access 属 Pro，不在范围。
- 共享基础设施：锚定与模态宿主、外部点击与 Escape 关闭、焦点陷阱与归还、遮罩模糊、拖拽手势与键盘约定都由 S8 提供，规格见 [浮层与输入](../../architecture/overlays-and-input.md)；本文只写各组件特有的行为与数值。
- 共享表面：Dialog 的面板表面复用于 Drawer、BottomSheet 与 Card 快速预览；Popover 的表面复用于 HoverCard 与 Reactions（Gestures）。

## 2. 组件一览
| 组件 | 类名 | Arc 页面 | 阶段 | Arc 依赖 | 建议基类 | 原语 |
|---|---|---|---|---|---|---|
| 对话框 | `Dialog` | [dialog](https://uiarc.dev/components/dialog) | P5 | 无 | ContentControl（Overlay 宿主） | P2 P11 |
| 抽屉 | `Drawer` | [drawer](https://uiarc.dev/components/drawer) | P5 | 无 | ContentControl（Overlay 宿主） | P2 P11 P12 |
| 底部面板 | `BottomSheet` | [bottom-sheet](https://uiarc.dev/components/bottom-sheet) | P5 | 无 | ContentControl（Overlay 宿主） | P11 P12 |
| 弹出框 | `Popover` | [popover](https://uiarc.dev/components/popover) | P5 | 无 | ContentControl + AtomUI Popup | P10 |
| 悬停卡片 | `HoverCard` | [hover-card](https://uiarc.dev/components/hover-card) | P5 | 无 | ContentControl + AtomUI Popup | P10 P19 |
| 提示 | `Tooltip` | [tooltip](https://uiarc.dev/components/tooltip) | P1 | 无 | 附加属性服务 + TemplatedControl | P2 P5 P10 |

## 3. 共享约定
- **两类宿主**：模态类（Dialog、Drawer、BottomSheet）在 S8 的模态层中渲染遮罩与面板，带焦点陷阱，关闭后焦点回到触发器；锚定类（Popover、HoverCard、Tooltip）基于 AtomUI Popup 定位，默认非模态。
- **打开状态**：Arc 的 `open` / `defaultOpen` / `onOpenChange` 统一映射为双向 `IsOpen`（bool）+ `Opened` / `Closed` 事件。Arc 的 Trigger / Close 部件不单独建控件：任意按钮绑定 IsOpen，或使用控件提供的 `OpenCommand` / `CloseCommand`。
- **表面**：模态面板为 SurfaceRaised + 1px Border + ShadowFloating，Dialog 用 RadiusSurface（34），Drawer 贴边一侧直角、另一侧 22px，BottomSheet 顶部两角 RadiusSurface；Popover 与 HoverCard 用 RadiusPanel（26）、SurfaceRaised、ShadowFloating；Tooltip 反色（Foreground 底、Background 字）、RadiusControl、ShadowRaised。
- **遮罩**：Dialog 为 oklch(10% 0 0 / 0.46) + 7px 背景模糊；Drawer 为 oklch(10% 0 0 / 0.38) + 4px 背景模糊；BottomSheet 不模糊，明色 oklch(16% 0 0 / 0.34)、暗色 oklch(0% 0 0 / 0.52)，透明度随面板位置变化。遮罩颜色作为各控件的 Own Token（明暗变体各一个 Token），模糊的实现与降级见浮层架构文档。
- **标题变化**：Dialog 与 Drawer 打开期间更换标题时，新标题以 0.3em + blur soft 升入（standard + enter），旧标题以 −0.3em + blur subtle、fast + standard 退出（Dialog 的 P2；Drawer 见 4.2 未决）。
- **退场可逆**：退出动画进行中点击会穿透到触发器，重新打开从当前状态反向（Arc 用 transition 而非 keyframes 保证可逆）；S3 Presence 的退场必须可打断并反向。
- **按下反馈**：Popover 等锚定类触发器按下只变色、不缩放（缩放会让定位偏移）；面板内的关闭按钮按下缩到 0.96（instant），松开弹簧回弹。
- **动效等级**：Reduced 去掉位移、缩放与拖拽，只保留短淡入淡出；Off 立即呈现或移除。

## 4. 组件

### 4.1 Dialog（Dialog）
`slug: dialog` · 阶段 P5 · Arc 依赖：无 · 建议基类：ContentControl（Overlay 宿主）

**Arc 行为摘要**：需要注意力的聚焦表面，用于必须打断的确认与决策（如 Delete project）、能放进一个面板的短表单（重命名、邀请），以及标题在步骤间原地交叉淡化的分步流程。长表单或详情面板用 Drawer，移动优先的分档任务用 BottomSheet，轻量的非模态锚定内容用 Popover。

**Arc API 要点**：
| Arc 属性 | 类型 / 默认值 | 建议 Avalonia 属性（类型） |
|---|---|---|
| `open` / `defaultOpen` / `onOpenChange` | `boolean`，`false` | `IsOpen`（bool，双向）+ `Opened` / `Closed` 事件 |
| `title`（必填，DialogContent） | `string`，打开中变化会交叉淡化 | `Title`（string） |
| `description` | `string` | `Description`（string?） |
| `children`（必填） | 正文，通常是表单或操作 | 继承的 `Content` |

Arc 经透传的 Radix 属性支持 modal 与 Escape / 外部点击回调；Avalonia 侧以 `IsModal`（bool，true）与可取消的 `Closing` 事件（带关闭原因：Escape、外部点击、关闭按钮）提供。

**交互与键盘**：
- Escape 关闭并把焦点还给触发器；Tab / Shift+Tab 在对话框内循环。
- 头部右侧关闭按钮（32px、RadiusControl、1px Border），与标题首行垂直居中。
- 退出过程中再次点击触发器可重新打开，从当前状态反向。

**可访问性**：
- 面板 Name 绑定 Title、HelpText 绑定 Description；焦点陷阱与归还由 S8 提供。
- 关闭按钮 Name 为 "Close dialog"（本地化）。

**动效**：
- 打开：遮罩以 standard + enter 淡入；面板从 y 8、scale 0.96 以 smooth 弹簧升起，透明度 fast + enter（P11）。
- 关闭更短更安静：面板到 y 4、scale 0.98，遮罩与面板以 fast + standard 淡出，并从入场的当前状态开始（P11）。
- 标题与描述变化以 blur soft 升入（P2）。
- Reduced：只保留 instant 透明度淡入淡出。

**Avalonia 实现要点**：
- 面板宽 min(视口 − 32, 440)、居中；高度上限为视口减去边距，超出后面板内部滚动（可用 ScrollArea）。头部与正文内边距 24px，头部下方 1px Border；标题 text-lg 字重 500，描述 text-sm TextSecondary。
- 内容只在打开时创建，退出动画结束后卸载（S3）。
- 服务：S3、S7、S8。

**AtomUI 参考**：Dialog（仅供学习行为与边界，不复用其实现）。

**风险与未决**：
- 7px 背景模糊需要对下层内容做模糊合成，Arc 文档也提示低端设备可能掉帧；需在 P5 开始前验证各平台成本，不支持时退化为纯色遮罩。
- 是否提供静态 API（如 `Dialog.ShowAsync`）属于对 Arc 的扩展，需单独决定。

### 4.2 Drawer（Drawer）
`slug: drawer` · 阶段 P5 · Arc 依赖：无 · 建议基类：ContentControl（Overlay 宿主）

**Arc 行为摘要**：用于专注工作的临时侧边表面：筛选、设置、记录详情等需要保持页面上下文的侧面板，以及对对话框来说过长、又不该离开当前视图的表单；可从任意一边出现，头部可拖拽关闭。短决策与确认用 Dialog，带档位的移动面板用 BottomSheet，不需要模态遮罩的小型锚定内容用 Popover。

**Arc API 要点**：
| Arc 属性 | 类型 / 默认值 | 建议 Avalonia 属性（类型） |
|---|---|---|
| `open` / `defaultOpen` / `onOpenChange` | `boolean`，`false` | `IsOpen`（bool，双向）+ `Opened` / `Closed` 事件 |
| `title`（必填）/ `description` | `string`，打开中新标题升入 | `Title` / `Description`（string） |
| `side` | `"left" \| "right" \| "top" \| "bottom"`，`"right"` | `Side`（enum DrawerSide：Left / Right / Top / Bottom，Right） |
| `children`（必填） | 正文，可滚动 | 继承的 `Content` |

modal 与 onInteractOutside / onEscapeKeyDown 的映射同 Dialog（`IsModal` + 可取消的 `Closing`）；异步提交后关闭用受控 IsOpen，或在 Closing 中取消。

**交互与键盘**：
- Escape 关闭并归还焦点；Tab / Shift+Tab 在面板内循环。
- 头部是拖拽手柄，正文照常滚动：朝贴边方向拖动跟手，反方向橡皮筋；拖过面板尺寸的 1/3 或朝边缘的速度超过 500px/s 时关闭，否则以 snappy 弹回。
- 拖拽只是可选方式，始终有关闭按钮（Name "Close drawer"）。

**可访问性**：
- 同 Dialog：面板 Name 取 Title，HelpText 取 Description，焦点陷阱与归还由 S8 提供。
- Reduced 时禁用拖拽，确保关闭不依赖手势。

**动效**：
- 打开：从贴边处以 smooth 弹簧滑入，遮罩 standard + enter 淡入（P11）。
- 点击或 Escape 关闭：standard（240ms）+ standard 缓动的补间滑出，阴影在最后 35% 行程中淡出；甩动关闭改用继承释放速度的 smooth 弹簧（时长同 standard）（P11、P12）。
- 反方向拖动时位移按平方根衰减（P12）。
- Reduced：禁用拖拽，滑动换成短透明度淡化。

**Avalonia 实现要点**：
- 尺寸：左右面板宽 min(30rem, 视口 − 边距)，视口 < 40rem 时接近全宽并收紧内边距；上下面板全宽、最高 min(32rem, 视口高)，底部面板顶端有 2.25rem × 4px 的握把条（BorderStrong，75% 不透明）。
- 1px Border 只画非贴边的三边；拖拽由 S10 手势驱动，偏移写入 RenderTransform，不触发每帧布局；面板只在滑出期间保持挂载（S3）。
- 服务：S3、S7、S8、S10。

**AtomUI 参考**：Drawer（仅供学习行为与边界，不复用其实现）。

**风险与未决**：
- 22px 圆角不在 Token 档位中（RadiusControl 18 / RadiusPanel 26），需作为 DrawerToken 自有值或调整到最近档位，需与 Token 目录核对。
- 标题变化的 blur 升入属于 P2，主数据未为 Drawer 列出 P2，需主笔确认。
- 复核结论（汇总评审）：新增 P2，已同步到[组件总清单](../overview.md)。

### 4.3 BottomSheet（Bottom sheet）
`slug: bottom-sheet` · 阶段 P5 · Arc 依赖：无 · 建议基类：ContentControl（Overlay 宿主）

**Arc 行为摘要**：从底边升起的模态面板，停在一个或多个档位（detent），可跟手拖动、甩动与键盘控制。用于详情、筛选、分享选项等移动优先的次要任务，适合先露出一部分再展开到近全屏的内容，以及需要在档位间拖动的地图与媒体视图。任何屏幕上的打断式决策用 Dialog，宽桌面布局的侧面板用 Drawer，不应压暗页面的小型锚定内容用 Popover。

**Arc API 要点**：
| Arc 属性 | 类型 / 默认值 | 建议 Avalonia 属性（类型） |
|---|---|---|
| `title`（必填）/ `description` | `string` | `Title` / `Description`（string） |
| `children`（必填） | 可滚动内容 | 继承的 `Content` |
| `trigger` | 打开面板的控件，关闭后焦点回到它 | `Trigger`（Control?） |
| `open` / `defaultOpen` / `onOpenChange` | `boolean`，`false` | `IsOpen`（bool，双向）+ `Opened` / `Closed` 事件 |
| `detents` | `number[]`，`[0.45, 0.92]`（视口高度比例） | `Detents`（`IList<double>`，0.45、0.92；单值即固定高度） |
| `initialDetent` / `onDetentChange` | `0`（排序后档位的索引）/ 回调 | `InitialDetentIndex`（int，0）+ 只读 `DetentIndex` + `DetentChanged` 事件 |
| `closeLabel` | `"Close"` | `CloseButtonName`（string，关闭按钮的 AutomationProperties.Name） |

**交互与键盘**：
- 握把上：ArrowUp / ArrowDown 上下移动一档，Home / End 到最高 / 最低档，Enter / Space 在最低与最高档之间切换。
- 面板上：ArrowDown / PageDown / Space 先展开到最高档，再滚动内容；Escape 关闭。
- 触摸在内容上拖动时先移动面板直到全开，然后才滚动内容；鼠标与触控笔从头部拖动。

**可访问性**：
- 模态层提供焦点陷阱与归还；初始焦点落在面板本身。
- 握把为 Button，暴露 ExpandCollapse，Name 为 "Expand sheet" / "Collapse sheet"。
- 档位变化写入 Polite 播报；Tab 到折叠区以下的元素时面板展开，使聚焦元素可见。

**动效**：
- 一个动效值驱动面板：拖动 1:1 跟手，超过最高档时橡皮筋（最大 120px），松手按 0.2s 的速度投影选最近档位或关闭；速度超过 320px/s 的甩动即使投影仍落在原档也会换档（P12）。
- 档位落位用 smooth，关闭用 visualDuration 0.3 的 smooth 并继承甩动速度（P11）。
- 遮罩透明度是面板位置的函数：最低档保留 78% 的压暗，向下拖动时线性减淡，不按计时器运行（P11）。
- Reduced：档位间直接跳转，面板淡入淡出。

**Avalonia 实现要点**：
- 面板宽 min(100%, 36rem)，档位是 TopLevel 客户区高度的比例；底部额外延伸 160px，遮住橡皮筋上拉时露出的空隙；宽 < 40rem 时收紧左右内边距。
- 握把命中区 3.5rem × 1.25rem，可见条 2.25rem × 5px（BorderStrong，悬停 TextMuted）；面板 ShadowFloating，内容滚动后头部出现分隔线。
- "先移动面板再滚动内容"需要协调面板拖拽与内部 ScrollViewer 的触摸手势（S10）：内容未滚到顶或面板未全开时手势交给面板。
- 视口尺寸变化时重算档位（S5）；内容不虚拟化，长列表应分页。
- 服务：S2、S3、S5、S8、S10。

**AtomUI 参考**：Drawer(Placement=Bottom)（仅供学习行为与边界，不复用其实现）。

**风险与未决**：
- 桌面平台上面板拖拽与内部 ScrollViewer 的手势仲裁是主要风险，需在 Windows 触屏与 macOS 触控板上验证。
- Arc 的档位基于移动浏览器的动态视口高度（dvh）；桌面窗口缩放时档位的重算与当前档位保持策略需在规格中写明。

### 4.4 Popover（Popover）
`slug: popover` · 阶段 P5 · Arc 依赖：无 · 建议基类：ContentControl + AtomUI Popup

**Arc 行为摘要**：锚定在触发器上的小型表面，点击打开，可承载交互内容（分享设置、小筛选表单、用自有控件拼出的选取器）；默认非模态，用户可以继续操作页面其余部分。短悬停标签用 Tooltip，悬停只读预览用 HoverCard，必须阻断页面用 Dialog，命令列表用 DropdownMenu。

**Arc API 要点**：
| Arc 属性 | 类型 / 默认值 | 建议 Avalonia 属性（类型） |
|---|---|---|
| `open` / `defaultOpen` / `onOpenChange` | `boolean`，`false` | `IsOpen`（bool，双向）+ `Opened` / `Closed` 事件 |
| `modal` | `false`；true 时焦点陷阱并阻止外部交互 | `IsModal`（bool，false） |
| `side`（经 Radix 透传） | 默认 bottom | `Placement`（enum：Top / Bottom / Left / Right，Bottom） |
| `align` | `"start" \| "center" \| "end"`，`"start"` | `Alignment`（enum：Start / Center / End，Start） |
| `sideOffset` | `6`（px） | `Offset`（double，6） |
| `collisionPadding` | `10`（px） | `CollisionPadding`（double，10） |

组合方式（已定案）：`Trigger`（Control）为原地显示的触发器，继承的 `Content` 为面板内容；或附加属性 `arc:Popover.Attach` 挂到任意控件；代码用 `ShowAt(anchor)`。`CloseCommand` 对应 PopoverClose。

**交互与键盘**：
- 触发器 Enter / Space 打开，Escape 关闭并把焦点还给触发器。
- 打开时焦点移入面板，关闭后回到触发器；非模态时点击外部关闭。
- 关闭过程中再次打开，从当前位置反向。

**可访问性**：
- 触发器暴露 ExpandCollapse；面板含控件时必须有标题或 Name（Arc 要求 heading 或 aria-label）。
- 非模态：焦点进入面板但不陷阱；IsModal=true 时由 S8 提供陷阱。

**动效**：
- 打开：面板从朝向触发器偏移 5px、scale 0.97 处淡入，以弹簧落位（Arc 用 CSS 弹簧 duration-spring 580ms 近似；透明度 fast + enter）（P10）。
- 关闭：fast + standard 淡出，位移减半、scale 0.98（Arc 文档写约 140ms）。
- 打开与关闭可互相打断并反向。
- Reduced：去掉变换，只保留 instant 淡入淡出。

**Avalonia 实现要点**：
- 面板最小宽 12rem，最大 min(22rem, 视口 − 20px)，内边距 16px，RadiusPanel、SurfaceRaised、ShadowFloating、1px Border。
- 定位、翻转与边缘避让交给 AtomUI Popup 与 S8；变换原点取 Popup 实际放置的方向。
- 内容只在打开时挂载（S3）。
- 服务：S1、S3、S8。

**AtomUI 参考**：FlyoutHost、Flyout（仅供学习行为与边界，不复用其实现）。

**风险与未决**：
- Arc 的 Popover 是纯 CSS 过渡，本库改用 S1 弹簧（ControlTheme 中为 ArcSpringEasing），手感可能有细微差异，需 Gallery 对照。
- 组合方式已定案为 `Trigger` + `Content`（面板内容）与附加属性 `Popover.Attach` 两种，HoverCard 相同（见第 5 节）。

### 4.5 HoverCard（Hover card）
`slug: hover-card` · 阶段 P5 · Arc 依赖：无 · 建议基类：ContentControl + AtomUI Popup

**Arc 行为摘要**：悬停或聚焦时预览人物或链接，不离开页面：提及、头像、作者名背后的人物卡片，点击必须留给导航的链接或引用预览，以及在密集列表中在触发器之间移动时几乎立即打开。纯文本标签用 Tooltip，内容含按钮或输入时用 Popover，自己头像上的账户菜单用 UserMenu。

**Arc API 要点**：
| Arc 属性 | 类型 / 默认值 | 建议 Avalonia 属性（类型） |
|---|---|---|
| `children`（必填） | 可聚焦的触发器 | `Trigger`（Control?），或附加属性 `arc:HoverCard.Attach` |
| `content`（必填） | 预览内容 | 继承的 `Content`（人物预览用 `HoverCardProfile`） |
| `side` | `"top" \| "bottom" \| "left" \| "right"`，`"bottom"` | `Placement`（enum，Bottom） |
| `align` | `"start" \| "center" \| "end"`，`"start"` | `Alignment`（enum，Start） |
| `openDelay` / `closeDelay` | `500` / `140`（ms） | `OpenDelay` / `CloseDelay`（TimeSpan，500ms / 140ms） |
| HoverCardProfile `name`（必填）/ `role` / `avatar` / `bio` | 文本；avatar 为 URL 或自定义图片元素 | `DisplayName`（`Name` 与 StyledElement 冲突）/ `Role` / `AvatarSource`（AtomUI `ImageSource`，经 `arc:Avatar`）/ `Bio` |
| HoverCardProfile `stats` / `meta` | `{ label, value }[]`，`[]` / 页脚节点 | `Stats`（`IList<HoverCardStat>`）/ `Meta`（object） |

**交互与键盘**：
- 悬停 500ms 后打开；指针离开后有 140ms 宽限，期间移入卡片可保持打开。
- Tab 聚焦触发器立即打开，Enter 跳过延迟打开；Escape 关闭，并在指针仍停在触发器上时保持关闭。
- 触摸时轻点切换卡片；全页同一时刻只打开一张卡片。

**可访问性**：
- 卡片 peer 为 ToolTip 类型；Avalonia 没有 aria-describedby，打开期间把卡片摘要写入触发器的 HelpText。
- 焦点从不进入卡片，因此内容必须只读，可操作的内容应放在触发器背后。

**动效**：
- 从触发器边缘长出：scale 0.96 → 1 与朝触发器的 4px 偏移共用一个 smooth 弹簧，透明度单独淡入；退出途中返回会重定向（P10）。
- 距上一张卡片关闭 300ms 内，下一张在 80ms 后打开且只淡入。
- HoverCardProfile 各行从 y 4 依次落位（standard + enter，延迟 0.04s + 序号 × item 35ms）（P19）；头像异步解码后从 blur 4px 淡入。
- Reduced：去掉缩放与位移，只保留 0.14s + enter 的淡入。

**Avalonia 实现要点**：
- 卡片宽 18.5rem，受可用空间或视口 − 24px 限制，内边距 16px；头像 48px 圆形，名字与角色单行省略，统计项可换行。
- "同一时刻只开一张"与"300ms 内热启动"由按 TopLevel 的协调器实现（Tooltip 用同样的模式但状态独立）。
- 打开 / 关闭用计时器（S12），不做指针追踪循环；头像用 AtomUI AsyncImage 异步解码。
- 服务：S3、S8、S12。

**AtomUI 参考**：FlyoutHost(Trigger=Hover)（仅供学习行为与边界，不复用其实现）。

**风险与未决**：
- 桌面上"触摸轻点切换"需按 PointerType 区分触摸与鼠标，并避免与触发器自身的点击导航冲突。
- Arc 只靠 140ms 宽限、没有"安全三角"判定；若桌面上误关率高，增加判定属于偏离 Arc，需要记录。

### 4.6 Tooltip（Tooltip）
`slug: tooltip` · 阶段 P1 · Arc 依赖：无 · 建议基类：附加属性服务 + TemplatedControl

**Arc 行为摘要**：为不熟悉的控件提供简短辅助文字：工具栏图标按钮的一行标签、截断文字的完整内容；在一组提示之间移动时，第一个之后立即显示。含链接或控件用 Popover，人物或链接等丰富预览用 HoverCard；触摸设备上必须看到的信息不要放进提示。

**Arc API 要点**：
| Arc 属性 | 类型 / 默认值 | 建议 Avalonia 属性（类型） |
|---|---|---|
| `content`（必填） | `ReactNode`；字符串 / 数字在打开中变化会交叉淡化并改变尺寸 | 附加属性 `Tooltip.Content`（object） |
| `children`（必填） | 可聚焦的触发器 | 附加到任意 Control，如 `arc:Tooltip.Content="Copy"` |
| `side` | `"top" \| "bottom"`，`"top"` | 附加属性 `Tooltip.Placement`（enum：Top / Bottom，Top） |

打开延迟 250ms、热启动窗口 300ms、间距 8px、边缘避让 12px 在 Arc 中是固定值，不暴露为属性，可放入 TooltipToken。

**交互与键盘**：
- 悬停 250ms 后打开；任一提示打开期间或最后一个关闭后 300ms 内，下一个立即打开且不带位移。
- Tab 聚焦触发器时打开，Escape 关闭。
- 只在悬停与键盘聚焦时出现，触摸用户几乎看不到。

**可访问性**：
- 提示 peer 为 ToolTip 类型；Avalonia 没有 aria-describedby，打开期间把文本同步到触发器的 HelpText。
- 内容只是补充：仅图标的触发器仍需自己的 AutomationProperties.Name；提示内不能放交互元素。

**动效**：
- 首次打开：从朝向触发器 3px、scale 0.97 处升起（P10）；热启动时只做 instant 淡入。
- 关闭：instant 淡出并缩到 0.98。
- 打开中字符串内容变化：新文本以 0.3em + blur soft 升入（P2），气泡宽高以 morph 弹簧过渡到新文本尺寸（P5）。
- Reduced：去掉变换，只保留短淡入淡出（Arc 文档为 90ms）。

**Avalonia 实现要点**：
- 以附加属性服务实现（用法类似 Avalonia 的 ToolTip.Tip），模板控件只负责气泡外观：最大宽 15rem，内边距 12px / 16px，RadiusControl，反色，1px 边框为 Background 14% 混入 Foreground，ShadowRaised。
- 热启动窗口用按 TopLevel 共享的状态实现（Arc 每个 Tooltip 自带 provider，窗口状态在模块级共享）。
- 气泡尺寸变化由 S5 测量文本、S2 驱动宽高；靠近边缘时翻到另一侧（S8）。
- 服务：S2、S3、S5、S8。

**AtomUI 参考**：ToolTip（仅供学习行为与边界，不复用其实现）。

**风险与未决**：
- 与 Avalonia 内置 ToolTip.Tip 共存：同一控件同时设置两者时的优先级需在规格中规定。
- Arc 源码中 `side` 还接受 left / right，但文档只列 top / bottom；本库以文档为准，是否开放左右待定。

## 5. 实现说明（已落地）

### 5.1 公共结构
- 模态三件（Dialog、Drawer、BottomSheet）派生公共基类 `ModalOverlay : ContentControl`：声明处不占位、不渲染（`IsVisible` 默认 false），打开时由 Core 的 `ModalPresence`（S8，见[浮层与输入](../../architecture/overlays-and-input.md) 3.5）把各自的面板挂到窗口覆盖层。`Content`、`Actions` 与面板都留在该控件的逻辑树中，DataContext、样式与绑定照常工作。
- 面板派生公共基类 `ModalSurface`（Title / Description / Actions / 关闭按钮 / `CloseRequested`）：`DialogSurface`、`DrawerSurface`、`BottomSheetSurface`。Card 快速预览可直接用 `DialogSurface` + `ModalPresence`。
- 统一 API：`IsOpen`（双向）、`Opened`、可取消的 `Closing(OverlayClosingEventArgs: Reason, Result)`、`Closed(OverlayClosedEventArgs)`、`OpenCommand` / `CloseCommand`（参数即结果）、`Close(result)`、`ShowAsync(owner?)` 返回结果、`CloseResult`、`LastCloseReason`、`CloseOnEscape` / `CloseOnOutsidePress` / `IsModal`。`DismissReason`：Programmatic、CloseButton、Escape、PressOutside、Gesture、Action。
- 附加属性 `arc:Dialog.Result="…"`：放在 Dialog / Drawer / BottomSheet 内的任意按钮上，点击即以该值关闭最近的浮层（实现为 Button.Click 的类处理器 + 逻辑祖先查找）。
- 遮罩色为各控件 Own Token（`DialogToken.ScrimColor` 等），模糊半径由主题设为 `BackdropBlurDialog` / `BackdropBlurDrawer`，BottomSheet 为 0。
- 关闭按钮是各面板主题内的键控 ControlTheme（`ArcDialogCloseButtonTheme` 等）：32px、1px Border、静止底色为 SurfaceMuted 50% / 65%（Token），悬停 SurfaceMuted + Foreground，按下 0.96（snappy）。图标为 `ArcIcons.X`。

### 5.2 Dialog
- 额外：`Size`（`ControlSize`，Small 360 / Medium 440 = Arc / Large 560，始终不超过视口 − 32）。`Dialog.ShowAsync(owner, title, content, description, params DialogAction[])`、`Dialog.ConfirmAsync(...)`（返回 bool）、`Dialog.BuildActions(...)`。
- 整个面板在超高时滚动（Arc 的 overflow auto），标题与描述用 `ArcContentSwap`（BlurSwap）交叉淡化。初始焦点为第一个可聚焦元素（通常是关闭按钮，与 Radix 一致）。

### 5.3 Drawer
- `Side`（`DrawerSide`，默认 Right）。尺寸规则在 `DrawerSurface.MeasureOverride` 中按视口计算，< 40rem 进入 `:narrow`（宽 = 视口 − 12，圆角 18，内边距 20）。
- 头部拖拽：`DragTracker`（阈值 3px，按侧锁轴，按钮上不起拖）；朝边缘 1:1 跟手，反向 `RubberBand.Sqrt`；超过面板 1/3 或朝边缘 > 500px/s 时以 Gesture 关闭，退出弹簧（smooth，visualDuration 0.24）继承释放速度；否则 snappy 弹回。Reduced 下禁用拖拽。

### 5.4 BottomSheet
- `Detents`（`BottomSheetDetents`，可写 `Detents="0.45, 0.92"`，静态 `Parse`）及别名 `SnapPoints`、`InitialDetentIndex`、只读 `DetentIndex`、`DetentChanged`、`SnapTo` / `Expand` / `Collapse`、`CloseButtonName`（默认 "Close"）。
- 使用 `ModalEntrance.Custom`：一个 `MotionValue` 驱动面板位移，遮罩透明度由面板位置计算（最低档 78%）写入 `ModalPresence.ScrimOpacity`；松手用 `MomentumProjector.SelectSnapPoint`（投影 0.2s、甩动 320px/s）选档或关闭；上拉越界 `RubberBand.Apply(…, 120)`。
- 触摸在正文上拖动先移动面板（`DragTracker.Starting` 中按是否全开、是否滚到顶决定），滚轮与阅读键先展开再滚动；档位变化通过 `LiveSetting=Polite` 的隐藏文本播报。
- 偏离：Arc 的 dvh 换成覆盖层高度；窗口尺寸变化时保持当前档位（动画中则重定向）。

### 5.5 Popover 与 HoverCard
- 共用 internal `OverlayAnchoredPresence`：宿主为 `ArcPopup`（定位、翻转、浮层阴影），但以 Animation 优先级关闭 AtomUI 自带动效，改为在弹层的 `BaseMotionActor` 上播放 Arc 动效（阴影随表面运动），变换原点取实际落位的一侧（读 `IsVerticalFlipped` / `IsHorizontalFlipped`），开合可互相打断反向，退出结束后才关闭弹层。
- Start / End 对齐用"居中放置 + 偏移"实现：AtomUI 的 EdgeAligned 放置在覆盖层宿主且阴影较大时会落到另一侧（已复现，见集成说明）。
- Popover：面板为公开的 `PopoverSurface`（最小 12rem、最大 22rem、内边距 16、RadiusPanel）；外部按下关闭（`IsModal` 时吞掉该次按下）；按在模态遮罩上只关闭最上层的弹出框。触发器若为 `arc:Button` 自动关闭按压缩放。
- HoverCard：复用 `PopoverSurface`（宽 18.5rem，不可聚焦）；全局只开一张，打开中或关闭后 300ms 内为"热"，下一张 80ms 后只淡入；离开触发器有 140ms 宽限，指针在卡片上时保持打开；键盘 Tab 聚焦立即打开、Enter 跳过延迟、Escape 关闭且指针停留时不再打开；触摸轻点切换。打开期间把摘要写入触发器 HelpText。
- `HoverCardProfile` 各行以一个线性 `MotionValue` 推进的错峰时间轴落位（0.04s + 序号 × 35ms，standard + enter）。

## 6. 实现顺序与依赖
- **P1 Tooltip**：S8 锚定浮层与 P10 的首个使用者，Phase 0 的 spike S0.10 先验证 AtomUI Popup 上的弹簧 Presence；FloatingButtonGroup（P2）硬依赖它。
- **P5 Dialog**：与 UserMenu 同阶段落地 P11 与 S8 的模态层（焦点陷阱、遮罩模糊）；其面板表面随后复用于 Drawer、BottomSheet 与 Card 快速预览。
- **P5 Drawer、BottomSheet**：排在 Dialog 之后，依赖 S10 手势与 P12（与 UserMenu 同阶段首次落地）。
- **P5 Popover → HoverCard**：Popover 表面复用于 HoverCard 与 Reactions（Gestures，P5）；HoverCard 的 P19 已由 DatePicker（P2）落地。
- 共用前置：可打断、可反向的 S3 Presence，以及按 TopLevel 的热启动协调器（Tooltip 与 HoverCard 共用实现、分开状态）。

## 7. 相关文档
- [组件总清单](../overview.md)
- [组件规格模板](../spec-template.md)
- [浮层与输入](../../architecture/overlays-and-input.md)
- [动效原语目录](../../architecture/motion/primitives-catalog.md)
- [Token 目录](../../architecture/theming/token-catalog.md)
- [路线图](../../roadmap/overview.md)
- Arc 原页面见第 2 节表格中的链接。
