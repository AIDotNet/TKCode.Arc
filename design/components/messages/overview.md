# 消息（messages）

> 消息类组件向用户报告状态与结果：页面内持续的提示、动作完成后的瞬时通知、站点顶部的公告与同意征询；它们共享"内容原地形变、高度弹簧跟随"的表达方式。

## 1. 大类定位
- 解决：持续的行内消息（Alert）、单个本地受控的完成确认（Toast）、应用级通知队列（ToastStack：类型、动作、更新与堆叠）、站点顶部的轮播公告（AnnouncementBar）、Cookie 与跟踪同意（CookieConsent）。
- 边界：没有内容可显示用 EmptyState（Cards）；必须先回应才能继续用 Dialog（Overlays）；任务进度用 Progress 大类；可回看的通知历史不在本库组件范围内。
- 共同特征：一个实例在状态之间形变（换语气、换文案、loading 变 success），而不是替换组件；文案变化时新文本升入、高度弹簧跟随；带计时的组件在悬停、聚焦或窗口不可见时暂停（Toast 例外）。

## 2. 组件一览
| 组件 | 类名 | Arc 页面 | 阶段 | Arc 依赖 | 建议基类 | 原语 |
|---|---|---|---|---|---|---|
| 警示 | `Alert` | [alert](https://uiarc.dev/components/alert) | P3 | 无 | ContentControl | P2 P6 |
| 轻提示 | `Toast` | [toast](https://uiarc.dev/components/toast) | P7 | 无 | TemplatedControl + ToastManager 服务 | P2 P6 P11 P12 P15 |
| 通知堆叠 | `ToastStack` | [toast-stack](https://uiarc.dev/components/toast-stack) | P7 | 无 | TemplatedControl（宿主） | P2 P5 P6 P12 P13 P19 P21 P22 P28 |
| 公告栏 | `AnnouncementBar` | [announcement-bar](https://uiarc.dev/components/announcement-bar) | P7 | 无 | TemplatedControl | P3 P6 P14 P22 P25 |
| Cookie 同意 | `CookieConsent` | [cookie-consent](https://uiarc.dev/components/cookie-consent) | P7 | `Button`、`Switch` | TemplatedControl | P2 P9 P11 |

## 3. 共享约定
- **语气**：Alert 为 info / success / warning / danger，ToastStack 的类型为 success / info / warning / error / loading；图标颜色 info → Accent、success → Success、warning → Warning、danger / error → Danger。Alert 图标为 Lucide Info、Check、TriangleAlert、CircleX（18px）；ToastStack 的 success 用 CircleCheck，loading 用旋转指示器，读屏类型标签依次为 Success、Info、Warning、Error、In progress。
- **文案形变（P2）**：新文本从 0.3em + blur soft（4px）升入（standard + enter），旧文本以 −0.3em + blur subtle（2px）、fast + standard 退出；退出中的旧副本为 Raw，播报只读当前文本。
- **高度跟随（P6）**：出现、消失或文案变化时高度以 smooth 弹簧变化，下方内容随之合拢；内容尺寸由 S5 观察，弹簧结束后回到 Auto 高度，被动重排（窗口缩放、字体切换）即时跟随。
- **播报**：danger / error 用 LiveSetting=Assertive，其余 Polite；ToastStack 每条前缀读屏专用的类型标签（如 "Error:"）。
- **计时（S12）**：ToastStack 与 AnnouncementBar 在悬停、键盘焦点在内、拖动中或窗口不可见时暂停；单个 Toast 的自动关闭不暂停（Arc 明确），因此文案应简短。浏览器"标签页隐藏"在桌面上的对应条件（窗口最小化或被遮挡）由 S12 统一定义。
- **持久化**：AnnouncementBar 的关闭记忆与 CookieConsent 的同意记录在 Arc 中写 localStorage；桌面没有 localStorage，两者需共用一个可替换的存储接口（默认实现写入应用数据目录），见各自"风险与未决"。
- **关闭按钮**：28–32px 圆形图标按钮，按下缩到 0.96（instant），Name 带上下文（如 `"Dismiss: {Title}"`、"Dismiss notification"），文案走 xlf。
- **动效等级**：Reduced 去掉高度动画、位移与拖拽，改为短淡入淡出，并关闭自动轮播；Off 立即到终态。

### 3.1 共享实现（`Messages/_Shared/`，internal）
- `MessagesTextSwap`：P2 / P3 文案形变。新文本自 0.3em（按字号）下方、4px 模糊升入（standard + enter），旧副本 −0.3em、2px 模糊、fast 退出；`Direction` 为 −1 时反向（MetricCard 的数字变小）。旧副本保持自身宽度与对齐（不随新文本重排），并设为 Raw；`IsBalanced` 用二分法实现 `text-wrap: balance`；TextBlock 池化（最多 2 个），每次形变只分配一个 MotionValue。Core 的 `ArcContentSwap` 把旧内容居中排列，左对齐文案会跳动，故此处另行实现（建议后续并入 Core）。
- `MessagesPresenceFrame`：带尺寸收拢的出现 / 消失（AnimatePresence + 高度或宽度 0 ↔ auto），尺寸走弹簧、透明度走补间，只在运动或关闭时裁剪，完成后发 `Settled`（Alert 的 Dismissed、AnnouncementBar 的收起依赖它）。文案变化的高度跟随用 `MessagesSizeFrame`（内容驱动的变化走弹簧，可用宽度变化与首次测量直接跳变，只在运动中裁剪）。Core `SpringSizeDecorator` 的被动变化判断用 `Math.Abs(a - b) < 0.5` 比较尺寸，可用尺寸为无穷大时（StackPanel、自动尺寸父级）∞ − ∞ 为 NaN，每次都被判为被动变化而直接跳变，所以本大类暂不使用它（修复建议见集成说明）。
- `MessagesGlyphSwap`：语气 / 类型图标换形（0.6 缩放 + 2px 模糊，缩放走 snappy，透明度与模糊走 fast 补间），也可显示 Arc 的 15px、1.75 描边、0.8s 一圈的 loading 环（只在可见且完整动效时转动）；全部在 `Render` 中绘制。
- `MessagesLiveRegion`：隐藏的实时区域（Name 变化即播报，同文案重复时也会再次播报）。
- `ToastSwipeTracker`：6px 判定水平 / 垂直、100ms 采样窗口估算释放速度、停手超过 60ms 不带速度、投影（0.99/ms 减速）与橡皮筋公式；Toast 与 ToastStack 共用（Core 手势库并行开发中，未依赖）。
- `IMessageStorage` / `MessageStorage.Default`（公开）：替代 localStorage 的可替换存储，默认 `FileMessageStorage` 写入 `<LocalApplicationData>/TKCode.Arc/<应用名>/messages`，另有 `InMemoryMessageStorage`；AnnouncementBar 与 CookieConsent 共用，各自也有 `Storage` 属性。
- `MessagesIcons`：Core 生成的 Lucide 数据把多段路径用空格拼接，后段以相对 `m` 开头时位置错误（X、CircleX、CircleCheck、ArrowRight、TrendingUp/Down 等 48 个图标受影响），本大类暂用修正后的数据，待生成器修复后改回 `ArcIcons`。
- AtomUI 默认让模板化控件 `ClipToBounds=True`，本大类所有 ControlTheme 显式设为 False，阴影、变换与焦点环才不被裁掉；模板中由 `{arc:…TokenResource}` 设置的值以本地优先级生效、样式无法覆盖，需随状态变化的值一律写在样式里。
- 内部模板部件（`ToastStackItem`、`AnnouncementBarRing`、`AnnouncementBarLink`、`CookieConsentPolicyLink`）在同一主题文件里有各自的 ControlTheme（ControlTheme 不允许后代选择器）。

## 4. 组件

### 4.1 Alert（Alert）
`slug: alert` · 阶段 P3 · Arc 依赖：无 · 建议基类：ContentControl

**Arc 行为摘要**：帮助用户恢复或继续的持续消息：关于页面或表单的行内提示（如信用卡即将过期）、随时间变化并在语气之间形变的状态、关闭后收起并合拢下方空隙的通知。动作的瞬时结果用 Toast 或 ToastStack，没有内容时用 EmptyState，必须回应时用 Dialog。

**Arc API 要点**（已实现）：
| Arc 属性 | 类型 / 默认值 | Avalonia 属性 |
|---|---|---|
| `title`（必填） | `string`，新标题从旧标题上升起 | `Title`（string） |
| `tone` | `"info" \| "success" \| "warning" \| "danger"`，`"info"` | `Tone`（`AlertTone`：Info / Success / Warning / Danger） |
| `children` | 标题下的详情；字符串变化时交叉淡化 | 继承的 `Content`：字符串走文案形变，其他内容原样显示（`:rich-content`） |
| `open` | `boolean`，控制出现；隐藏时收起高度并淡出 | `IsOpen`（bool，true，双向） |
| `onDismiss` | 设置后显示关闭按钮；非受控时先收起再回调 | `IsDismissible`（bool）+ `Dismissed` 路由事件（收起动画结束后触发）+ `Dismiss()` 方法 |

**交互与键盘**：
- 有关闭按钮时 Enter / Space 激活：先把 `IsOpen` 写回 false（双向绑定立即得到 false），收起结束后再触发 `Dismissed`；重新设 `IsOpen=true` 时高度展开、图标做一次 0.6 → 1 的落定。
- 推荐保持一个 Alert 实例，通过修改 Tone、Title、Content 在状态间形变，而不是替换控件。

**可访问性**：
- danger 用 Assertive，其余 Polite；控件 Name 为标题、HelpText 为详情，标题变化即播报；退出中的旧副本为 Raw。
- 关闭按钮 Name 为 `"Dismiss: {Title}"`；语气图标不在自动化树中。

**动效**：
- 出现 / 消失：`MessagesPresenceFrame` 高度 smooth 弹簧，透明度入 standard + enter、出 fast + standard，下方内容合拢（P6）。
- 语气变化：`MessagesGlyphSwap` 0.6 缩放 + 2px 模糊换形（P2 图标）；文案 `MessagesTextSwap` 升入，`MessagesSizeFrame` 高度同时弹簧适配。
- Reduced：文案与图标只淡入淡出（instant），出现 / 消失立即完成（与 Arc `{duration: 0}` 一致）；Off 立即。

**Avalonia 实现要点**：
- 表面：RadiusControl、Surface、1px Border、ShadowResting，内边距 16；18px 图标（Lucide 描边 2）下移 1px 对齐标题首行；标题 TypeSM / Medium、行高 19.6，详情 TypeXS / TextSecondary、行高 16.8、上距 4（无详情时为 0）；关闭按钮为 28px 圆形 Ghost `arc:Button`（TextMuted，悬停 Foreground + SurfaceMuted，外边距 −5 −6 −5 0）；三栏用 DockPanel，文案栏每次布局只被测量一次。
- Token：`AlertToken`（SurfacePadding、GlyphSize、GlyphMargin、DescriptionGap、TitleLineHeight、DescriptionLineHeight、DismissDiameter、DismissMargin）。

**AtomUI 参考**：Alert（仅供学习行为与边界，不复用其实现）。

**风险与未决**：
- Arc 的 HeightFrame 只在内容变化后 120ms 内弹簧；`MessagesSizeFrame` 把所有内容驱动的尺寸变化都做弹簧、可用宽度变化则直接跳变，效果等价。

### 4.2 Toast（Toast）
`slug: toast` · 阶段 P7 · Arc 依赖：无 · 建议基类：TemplatedControl + ToastManager 服务

**Arc 行为摘要**：已完成后台操作的简短确认：用本地状态控制的单个成功提示（如 Changes saved），适合不需要队列的小应用或演示。应用级通知（类型、动作、更新、堆叠）用 ToastStack，应留在页面流中的持续消息用 Alert。

**Arc API 要点**（已实现）：
| Arc 属性 | 类型 / 默认值 | Avalonia 属性 |
|---|---|---|
| `title`（必填） | `string`，显示中变化会交叉淡化 | `Title`（string） |
| `description` | `string` | `Description`（string?） |
| `open` / `onOpenChange` | `true`；关闭、滑走或超时回调 false；关闭后再设 true 会重新弹出 | `IsOpen`（bool，true，双向）+ `Closed` 路由事件（`ToastClosedEventArgs.Reason`：Dismissed / Swiped / TimedOut）+ `Close()` |
| `duration` | `4500`（ms），≤ 0 关闭自动消失 | `Duration`（TimeSpan，4.5s；≤ 0 不自动关闭） |

应用级入口也挂在本类上：`Toast.Show(owner, title, kind, description, action, duration, id)` 返回 id，`Toast.Update(owner, id, ToastUpdate)`、`Toast.Dismiss(owner, id?)`，都转给 `ToastHost.GetService(owner)`（见 4.3）。

**交互与键盘**：
- Tab 到达关闭按钮后 Enter 关闭。
- 鼠标与触摸均可水平拖动（不在按钮上起手）：超过 80px 或速度超过 480px/s 时按方向甩出，`x` 以 0.3s 无回弹弹簧并继承释放速度，否则以 snappy 带速度弹回。
- 自动关闭计时不随悬停暂停（Arc 明确）。

**可访问性**：
- Polite 播报（Name 为标题、HelpText 为描述，整条重读），新内容不抢焦点；关闭按钮 Name 为 "Dismiss notification"；对勾图标不在自动化树中。

**动效**：
- 进入：从 y 16、scale 0.96 以 morph 弹簧升起，透明度 standard + enter（P11）；Arc 自己的对勾路径（`M4 12.5l5 5L20 6.5`，描边 2.25）在 0.12s 延迟后以 standard + enter 描绘（Core `StrokeDrawGlyph`，P15）。运行中挂载的 Toast 都会升起（对应 Arc 的 hydration 后挂载）。
- 关闭：下沉 8px、scale 0.97，exit（180ms）+ standard；甩出时透明度以 exit 时长淡出（P12）。退场结束后不再占位。
- 文案变化升入，高度弹簧适配（P2、P6）。
- Reduced：透明度淡入淡出（instant），禁用拖动；Off 立即。

**Avalonia 实现要点**：
- 表面宽 min(可用宽, 26rem=416)，RadiusPanel，底色 Surface 94% 混入 Background（Token `SurfaceColor`），ShadowFloating，内边距 14 14 14 16；左侧 30px 圆形图标底为 Success 10% 混入 Surface、描边 Success 24% 混入 Border；标题单行省略，描述换行、上距 2；关闭按钮 32px 圆形，X 描边 2。
- 位移、缩放、透明度由四个 `MotionValue` 写入模板根的 `MatrixTransform` 与 Opacity（甩出要继承速度，ControlTheme 过渡无法表达）。
- Toast 可以直接放进布局（由调用方定位，Arc 亦然）；应用级通知由 ToastStack 承担。
- Token：`ToastToken`（MaxToastWidth、SurfacePadding、IconMargin、IconDiameter、CheckSize、CopyPadding、CopyLineHeight、CloseDiameter、CloseMargin、SurfaceColor、IconFill、IconRing）。

**AtomUI 参考**：WindowMessageManager（仅供学习行为与边界，不复用其实现）。

**风险与未决**：
- Arc 只有在提供 onOpenChange 时才自动关闭；Avalonia 中"是否有人监听"不可靠，已改为 Duration > 0 即自动关闭。
- 复核结论（汇总评审）：移除 P22，已同步到[组件总清单](../overview.md)。

### 4.3 ToastStack（Toast stack）
`slug: toast-stack` · 阶段 P7 · Arc 依赖：无 · 建议基类：TemplatedControl（宿主）

**Arc 行为摘要**：把简短结果堆叠在视口边缘，直到用户伸手查看：应用级的异步结果、错误与撤销，任务完成时由 loading 形变为 success 或 error 的提示，以及带 View、Undo 等动作按钮的提示。单个本地确认用 Toast，页面内的持续消息用 Alert。

**Arc API 要点**（已实现）：
| Arc 属性 | 类型 / 默认值 | Avalonia 属性 |
|---|---|---|
| Provider `duration` | `5000`（ms）；warning / error 为 1.6 倍，loading 等待更新 | `Duration`（TimeSpan，5s） |
| Provider `limit` | `12`，超出丢弃最旧 | `Limit`（int，12） |
| `label` | `"Notifications"` | `Label`（string，"Notifications"；Name 为 `"{Label} (Alt+T)"`） |
| `position` | `"bottom-right" \| "bottom-center" \| "bottom-left"`，`"bottom-right"` | `Position`（`ToastStackPosition`：BottomRight / BottomCenter / BottomLeft） |
| `contained` | `false`，固定在最近的定位祖先而非窗口 | `IsContained`（bool，false：距自身边 16 而非 24，且不做窄窗口居中） |
| `visibleToasts` | `3` | `VisibleToasts`（int，3） |
| `hotkey` | `true`，Alt+T 把焦点移入堆叠 | `IsHotkeyEnabled`（bool，true）+ `FocusInto()` |
| useToastStack `toast` / `update` / `dismiss` / `count` | 返回 id / 按 id 形变并重启计时 / 关闭一条或全部 / 队列数 | `IToastService`：`Show(ToastOptions)` 返回 id、`Update(id, ToastUpdate)`、`Dismiss(id?)`、`Count`；`ToastStack` 实现它，另有 `ToastsChanged` 事件与只读 `IsExpanded` |

`ToastOptions`：Id、Kind（`ToastKind`：Success / Info / Warning / Error / Loading）、Title、Description、Action（`ToastAction(label, Action<string>)`）、Duration（null 用类型默认，`Timeout.InfiniteTimeSpan` 常驻）；复用同一 Id 即更新。`ToastUpdate` 的 null 成员保持不变，`RemoveAction` / `RemoveDescription` 显式移除。

**服务与宿主**：`ToastHost.GetService(owner)` / `GetStack(owner)` 返回 owner 所在窗口的堆叠：窗口里放置的 ToastStack 在挂载时自行注册（优先），否则在窗口的 OverlayLayer 上创建一个并随其尺寸变化（必要时打开 `VisualLayerManager.EnableOverlayLayer`）。`Toast.Show(owner, "Saved", ToastKind.Success, action: …)` 即走这里。

**交互与键盘**：
- Alt+T（TopLevel 隧道路由）把焦点移到最前一条的第一个按钮并展开堆叠；Tab 在各提示的动作与关闭按钮间移动；Escape 关闭聚焦的提示。
- 鼠标悬停列表（列表区域覆盖展开后的间隙）或键盘焦点进入时展开为列表（间距 12px），离开后收拢；触摸轻点卡片切换展开，点在堆叠外收拢。
- 向右滑动 1:1 跟手，`偏移 + 投影(速度) > 卡片宽 40%` 且速度 > −200 时以 0.32s 无回弹弹簧带速度甩出并关闭；向左为橡皮筋，弹回速度 × 0.3。拖动中计时暂停。
- 动作运行后若回调没有更新该提示就关闭它；回调更新且去掉了动作时，键盘用户的焦点落到关闭按钮。聚焦的提示被关闭时，焦点移到下一条，或回到进入堆叠前的元素。

**可访问性**：
- 视口为 Region 地标；两个隐藏实时区域：Error 用 Assertive，其余 Polite，新增与更新都播报 `"{类型}: {标题}. {描述}"`；每条卡片 Name 为 `"{类型}: {标题}"`。
- 隐藏（超出 VisibleToasts）与正在离开的提示不可命中、不可聚焦；悬停、聚焦、拖动或窗口最小化时计时暂停（Core `PausableTimer`，每条一个）。

**动效**：
- 新提示从自身高度下方升起（透明度 standard + enter）；旧提示每层向后收 14px、缩小 0.05、内容淡出并取最前一条的高度，展开时以 morph 弹簧散开，其他变化走 smooth（P28、P19）。
- 右滑甩出时卡片透明度在 280px 内降到 25%（P12、P13）；关闭的提示下沉 10px、缩到 0.96、exit 淡出，甩出的 0.22s 淡出。
- update 原地形变图标（`MessagesGlyphSwap`）、文案、高度（卡片高度弹簧）与动作按钮宽度（`MessagesPresenceFrame` 横向，P5）；计时是可暂停的时间线（P22）；loading 环 0.8s 一圈（P21），Reduced 下静止。
- Reduced：位置直接跳变，只做 fast 淡入淡出；Off 立即。

**Avalonia 实现要点**：
- `ToastStackPanel` 充满 ToastStack，只有列表矩形参与命中（`ICustomHitTest`），页面其余部分可点击；列表宽 min(360, 宽 − 32)，距底 24（Contained 或窄于 480px 时 16），窄窗口一律底部居中。
- 每条 `ToastStackItem`（internal，自带 ControlTheme）按自然高度布局一次，之后的提升、缩放、卡片高度、透明度与横移全是 `MotionValue` → 矩阵变换 / 自绘卡片 / 内容裁剪，不触发布局；卡片（SurfaceRaised、Border、RadiusPanel、ShadowRaised，最前一条与展开时 ShadowFloating，收起的后层 SurfaceMuted、standard 过渡）在 `Render` 中按动画高度绘制。
- 卡片内容：内边距 12 12 12 16，18×20 图标位（Lucide 18px、描边 1.75），文案 TypeSM、行高 20、描述上距 2；动作 28px 胶囊（Secondary Small，悬停 BorderStrong）；关闭 28px 圆形、X 描边 1.75；卡片宽 < 336 时动作移到文案下方。
- Token：`ToastStackToken`（MaxListWidth、ContentPadding、IconSlotWidth/Height、IconMargin、CopyLineHeight、ActionHeight、ActionPadding、ActionSlotMargin、ActionMargin、NarrowActionSlotMargin、CloseDiameter、CloseMargin）。

**AtomUI 参考**：WindowNotificationManager（仅供学习行为与边界，不复用其实现）。

**风险与未决**：
- Alt+T 在 Windows 上可能与菜单访问键冲突，可用 `IsHotkeyEnabled=false` 关闭并自行调用 `FocusInto()`。
- 浏览器"标签页隐藏"对应为窗口最小化；被其他窗口完全遮挡时不暂停。
- 复核结论（汇总评审）：新增 P21，已同步到[组件总清单](../overview.md)。

### 4.4 AnnouncementBar（Announcement bar）
`slug: announcement-bar` · 阶段 P7 · Arc 依赖：无 · 建议基类：TemplatedControl

**Arc 行为摘要**：页面顶部的细横幅，轮播消息、显示倒计时，关闭时平滑收起。用于全站促销、发布或维护通知，在一条横幅中轮播 2–4 条短公告，或带实时倒计时的促销横幅。与某一区域或表单相关的消息用 Alert，对访客刚做之事的反馈用 Toast，同意选择用 CookieConsent。

**Arc API 要点**（已实现）：
| Arc 属性 | 类型 / 默认值 | Avalonia 属性 |
|---|---|---|
| `messages`（必填） | `Announcement[]`：id、message、action { label, href, onClick }、countdown { to, label } | `Messages`（`AvaloniaList<Announcement>`，XAML 内容属性）或 `ItemsSource`（`IList<Announcement>`）；`Announcement`：Id、Message、Action（`AnnouncementAction`：Label、Href、Command、CommandParameter）、Countdown（`AnnouncementCountdown`：To、Label） |
| `id` | 关闭记忆键 `arc-announcement:<id>`，换 id 即重新显示 | `DismissalKey`（string?）+ `Storage`（`IMessageStorage?`） |
| `open` / `defaultOpen` / `onOpenChange` | `true` | `IsOpen`（bool，双向）+ `Closed` 路由事件 + `Dismiss()` |
| `index` / `defaultIndex` / `onIndexChange` | `0` | `SelectedIndex`（int，双向）+ `SelectedIndexChanged` 事件 + `Next()` / `Previous()` |
| `interval` / `autoPlay` | `6000`（ms）/ `true`（偏好减少动效时关闭） | `Interval`（TimeSpan，6s）/ `IsAutoPlayEnabled`（bool，true） |
| `controls` / `dismissible` | `false` / `true` | `IsNavigationVisible`（bool，false）/ `IsDismissible`（bool，true） |
| `tone` | `"neutral" \| "inverted"`，`"neutral"` | `Tone`（`AnnouncementBarTone`：Neutral / Inverted） |
| `onAction` / `onCountdownEnd` | 回调 | `ActionInvoked` / `CountdownEnded` 事件（`AnnouncementEventArgs`：Announcement、Index） |
| `label` | `"Announcements"` | `Label`（string） |

clearAnnouncementDismissal(id) 映射为静态 `AnnouncementBar.ClearDismissal(key, storage?)`；暂停环的按下状态对外为 `IsPaused`（bool，双向）。

**交互与键盘**：
- Tab 依次经过行动项、上一条、暂停环、下一条与关闭按钮；Enter / Space 按下聚焦的控件，暂停环是真正的 ToggleButton。
- 悬停（鼠标）、焦点在内、窗口最小化或暂停环按下时暂停；行动项有 Href 时用系统启动器打开，Command 同时执行。
- 某条消息的倒计时到零时触发一次 CountdownEnded。

**可访问性**：
- Region 地标（Name 为 Label）；多条消息时每张消息面 Name 为 "n of total"，退出中的面为 Raw。
- 视口在轮播中 LiveSetting=Off，暂停时 Polite（Name 为当前消息）。
- 暂停环 Name 随状态在 "Pause announcements" / "Resume announcements" 间切换；倒计时 LiveSetting=Off，Name 以天、小时与分钟表述。

**动效**：
- 下一条从下方 70% 升起、上一条向上 60% 离开（反向时相反），带 4px 模糊，位移走 0.46s / bounce 0.1 弹簧；视口高度以 0.42s 无回弹弹簧适配（P25、P6）。
- 暂停环在 Interval 内线性填满，暂停时停在原处，恢复后补完剩余部分（P22）：时钟是 Core `PausableTimer`，环只是跟随它的线性 `MotionValue`（描边虚线偏移，无逐帧几何分配）。
- 倒计时数字在格内从上方落入、旧数字向下离开（0.32s、bounce 0.08）（P3、P14），整串在一个自绘控件中完成。
- 关闭时整条以 0.44s 无回弹弹簧收起高度、exit 淡出，下方页面随之上移（P6）。
- Reduced：关闭轮播，消息与数字用淡入淡出替换，关闭只淡出（fast）不收起高度；Off 立即。

**Avalonia 实现要点**：
- 三列 Grid：左侧空列宽度 = 控件数 × 32 + 间隙 × 2（`SideWidth`），消息在页面上居中；控件宽 ≤ 560 时（`:narrow`）左列为 0、列距 8、左内边距 14、消息起始对齐；条内边距 6 8 6 16，底边 1px Border，背景 SurfaceMuted。
- 图标按钮 32px 圆形 Ghost（不按压缩放），颜色为次要色，悬停填充为悬停色 10%、按下 12%（Token 预混）；行动项为内部 `AnnouncementBarLink`（继承前景、Medium、箭头悬停右移 2px、fast 过渡）。
- 消息面（`AnnouncementBarFace`，WrapPanel：文字、倒计时、行动项，行距 2、列距 12）池化复用；倒计时每秒对齐整秒醒来一次，只在挂载时运行。
- inverted：Foreground 底、Background 文字、次要文字为 Background 66%，图标悬停色为 Background。
- Token：`AnnouncementBarToken`（BarPadding、NarrowBarPadding、ColumnGap、NarrowColumnGap、IconDiameter、ControlGap、FaceMinHeight、MessageLineHeight、IconHoverFill、IconPressedFill、InvertedMutedForeground、InvertedIconHoverFill、InvertedIconPressedFill）。

**AtomUI 参考**：Alert、MarqueeLabel（仅供学习行为与边界，不复用其实现）。

**风险与未决**：
- Arc 在受控 `open` 时忽略已记忆的关闭；Avalonia 无法区分"是否受控"，记忆的关闭总是隐藏横幅（`IsOpen` 仍为 true），需要强制显示时先调用 `ClearDismissal`。

### 4.5 CookieConsent（Cookie consent）
`slug: cookie-consent` · 阶段 P7 · Arc 依赖：Button、Switch · 建议基类：TemplatedControl

**Arc 行为摘要**：同意横条可形变为偏好面板，做出选择后折叠为一个重新打开按钮。用于营销站点或应用征询 Cookie / 跟踪同意，以及需要按类别选择并能事后改变主意的站点。无需选择的全站通知用 AnnouncementBar，一次性自动消失的消息用 Toast，必须阻断页面直到决定时用 Dialog。

**Arc API 要点**（已实现）：
| Arc 属性 | 类型 / 默认值 | Avalonia 属性 |
|---|---|---|
| `categories` | `CookieCategory[]`（id、label、description、required、defaultEnabled），默认 4 类 | `Categories`（`AvaloniaList<CookieCategory>`，XAML 内容属性）或 `ItemsSource`（`IList<CookieCategory>`，设置时优先；都为空时用 `DefaultCategories`：Strictly necessary（必需）、Preferences、Analytics、Marketing）；`CookieCategory`：Id、Label、Description、IsRequired、IsEnabledByDefault |
| `value` / `defaultValue` / `onChange` | 类别 id → 是否允许，`null` 表示未决定；回调带 action 与 updatedAt | `Consent`（`IReadOnlyDictionary<string,bool>?`，双向）+ `ConsentChanged` 事件（`CookieConsentChangedEventArgs`：Consent、Action（AcceptAll / RejectAll / Save / Reset）、UpdatedAt） |
| `storageKey` / `version` | 无 / `"1"`，版本变化后重新征询 | `StorageKey`（string?）/ `Version`（string，"1"）/ `Storage`（`IMessageStorage?`） |
| `preferencesOpen` / `defaultPreferencesOpen` / `onPreferencesOpenChange` | `false` | `IsPreferencesOpen`（bool，双向） |
| `policy` | `{ label, href?, onClick? }`，无 href 时渲染为按钮 | `Policy`（`CookieConsentPolicy`：Label、Href、Command），在横条消息与面板描述末尾内联显示 |
| `position` / `strategy` | `"bottom" \| "bottom-left" \| "bottom-right"` / `"fixed" \| "absolute"` | `Position`（`CookieConsentPosition`）；strategy 由放置位置决定（见风险） |
| `showReopenButton` / `reopenSide` | `true` / 跟随横条一侧，居中时为左 | `IsReopenButtonVisible`（bool，true）/ `ReopenSide`（`CookieConsentSide?`） |
| `labels` | 10 项文案覆盖 | `Labels`（`CookieConsentLabels`，Arc 英文默认，每个实例独立） |

命令式句柄映射为 `OpenPreferences()`、`ClosePreferences()`、`Reset()`；readCookieConsent 映射为静态 `CookieConsent.Read(storageKey, version, storage?)`，返回 `CookieConsentRecord`（Value、Version、UpdatedAt）。存储格式与 Arc 相同：`{"value":{…},"version":"1","updatedAt":"…"}`（`Utf8JsonWriter` / `JsonDocument`，无反射）。

**交互与键盘**：
- Tab 在横条或面板的按钮与开关间移动；Space 切换聚焦的类别开关，点击类别名也会切换（对应 `<label for>`）；Escape 不保存地关闭偏好面板，焦点回到横条的 Customize 或重新打开按钮。
- Reject all 与 Accept all 并排、同等权重。
- 做出选择后折叠为 44px 的 Cookie 按钮（获得焦点），可随时重新打开偏好；也可从外部调用 `OpenPreferences()`。

**可访问性**：
- 横条与面板都是非模态对话框：横条 Name 为偏好标题、HelpText 为消息，面板 Name 为标题、HelpText 为描述；打开面板时焦点到标题，保存后焦点到重新打开按钮（Name 取 reopen 文案）。
- 每个开关以类别名为 Name、以提示文字为 HelpText；必需类别显示 "Always on" 文字而非开关。
- Save 按钮的 HelpText 为计数（如 "2 of 3 optional categories allowed"）。

**动效**：
- 同一块表面（`CookieConsentSurface`，internal）在横条、面板、44px 重新打开按钮之间以四个 `MotionValue` 形变位置、宽、高与圆角（P9）：面积变大用 0.46s / bounce 0.14，变小用 0.38s / bounce 0；表面自绘（SurfaceRaised、1px Border 外环 + ShadowFloating），各面在最终宽度贴底，按动画中的圆角矩形裁剪，期间无布局。
- 各面从短模糊中升起（y 10 → 0，0.42s / bounce 0.08；透明度 0.22s、模糊 0.26s，延迟 0.04s），离开时 0.12s 淡出、下移 4、模糊 4（P2）。
- 入场：从底边升起 24px、scale 0.98 → 1（0.5s、bounce 0.1），透明度 0.24s + enter（P11）；没有可显示的面时 0.18s 下沉淡出。
- Reduced：表面直接跳到各形态，各面 fast 淡入、0.1s 淡出；Off 立即。

**Avalonia 实现要点**：
- 控件是一个图层：放在页面之上（如窗口根 Grid 的最后一个子元素或预览面板），除表面外不接收指针（`ICustomHitTest`）；表面距图层左右下 16px。
- 尺寸：横条最宽 660px、面板 420px；横条宽 < 560px 时消息堆在按钮上方（Customize 自适应，两个选择按钮平分），内容宽 ≤ 300px 时两个选择按钮一行、Customize 另起一行；面板内容宽 ≥ 340px 时三个操作按钮排成一行。布局在测量前按宽度切换（`:bar-stacked`、`:bar-compact`、`:panel-wide`）。
- 类别列表最高 min(窗口高 46%, 300px)，超出滚动；行内边距 10 6、上分隔线 BorderSubtle。
- 开关与按钮直接使用 Switch 与 Button（Small）；挂载时同步读取存储，已决定的访客不会看到横条闪现，版本不匹配的旧记录被忽略。
- 消息与描述的行距用 `LineSpacing`（而非 `LineHeight`）：固定行高会让含内联策略链接的行错位并被裁切。
- Token：`CookieConsentToken`（LayerMargin、BarPadding、PanelPadding、PanelGap、ActionGap、PanelHeadMargin、ListMargin、PanelActionsPadding、CloseDiameter、CloseMargin、ChipDiameter、MessageLineSpacing、TitleLineHeight）。

**AtomUI 参考**：—（AtomUI 无对应控件）。

**风险与未决**：
- `strategy`（fixed / absolute）在桌面上由放置位置决定：放在窗口根部即固定在窗口底边，放在容器里即固定在容器底边；因此没有 `IsContained` 属性。
- 桌面应用中"Cookie"的语义较弱，示例文案与默认类别是否改为"数据与遥测同意"属于产品决定；组件 API 保持与 Arc 一致。
- 重新打开按钮是普通按钮（Invoke），没有 ExpandCollapse 模式。

## 5. 实现顺序与依赖
- 五个组件已在同一批次落地：共享实现（第 3.1 节）先行，Alert → Toast → ToastStack → AnnouncementBar → CookieConsent。
- 硬依赖：CookieConsent 使用 Button 与 Switch；ToastStack / AnnouncementBar 使用 Core `PausableTimer`；文案高度跟随使用本大类的 `MessagesSizeFrame`；Toast 的对勾使用 Core `StrokeDrawGlyph`。
- 持久化接口 `IMessageStorage` 由本大类定义并供 AnnouncementBar 与 CookieConsent 共用；如需跨大类复用，可迁入 Core。
- 待集成：`MessagesTextSwap`（左对齐安全的文案形变）与 Lucide 生成器修复建议迁入 Core（见第 3.1 节）。

## 6. 相关文档
- [组件总清单](../overview.md)
- [组件规格模板](../spec-template.md)
- [动效原语目录](../../architecture/motion/primitives-catalog.md)
- [Token 目录](../../architecture/theming/token-catalog.md)
- [浮层与输入](../../architecture/overlays-and-input.md)
- [路线图](../../roadmap/overview.md)
- Arc 原页面见第 2 节表格中的链接。
