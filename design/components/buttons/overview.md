# 按钮（Buttons）

> Arc Actions 分组中直接执行动作的控件：单个按钮、按钮组、原位确认类按钮与明暗切换，共享按压反馈、标签形变与键盘焦点规则。

## 1. 大类定位
- 解决"立即执行一个动作"：单一动作用 Button，异步提交用 ActionButton，有近似备选用 SplitButton，同一对象上的一组相关动作用 ButtonGroup / FloatingButtonGroup / ExpandingButtonGroup，复制值用 CopyButton，危险动作的原位二次确认用 ConfirmMorph，明暗切换用 ThemeSwitcher；IconMorph 是给这些按钮用的双态图标。
- 边界：本大类都是"动作"，不保存选中值。保持选中的选项用 Toggles 的 SegmentedControl 或 Selects 的 ChipGroup；没有默认动作的命令列表用 Menus 的 DropdownMenu；需要长按的危险动作用 Gestures 的 HoldToConfirm。
- 共同特征：按下时内容轻微缩小、松开弹回；标签变化时宽度弹簧过渡、新文字从柔和模糊中升起；结果（Saved、Copied、Deleted）在按钮原位呈现并礼貌播报，不额外弹 Toast。

## 2. 组件一览
| 组件 | 类名 | Arc 页面 | 阶段 | Arc 依赖 | 建议基类 | 原语 |
|---|---|---|---|---|---|---|
| 按钮 | Button | [button](https://uiarc.dev/components/button) | P0 | 无 | Avalonia Button | P1 P2 P5 P21 P29 |
| 操作按钮 | ActionButton | [action-button](https://uiarc.dev/components/action-button) | P3 | 无 | Arc Button | P1 P2 P4 P5 P15 P21 |
| 拆分按钮 | SplitButton | [split-button](https://uiarc.dev/components/split-button) | P5 | 无 | TemplatedControl | P1 P2 P4 P5 P10 P19 |
| 按钮组 | ButtonGroup | [button-group](https://uiarc.dev/components/button-group) | P5 | 无 | ItemsControl | P1 P2 P5 P7 P10 |
| 浮动按钮组 | FloatingButtonGroup | [floating-button-group](https://uiarc.dev/components/floating-button-group) | P2 | Tooltip | ItemsControl | P1 P2 P5 P7 P10 |
| 展开按钮组 | ExpandingButtonGroup | [expanding-button-group](https://uiarc.dev/components/expanding-button-group) | P7 | 无 | ItemsControl | P1 P2 P5 P15 |
| 复制按钮 | CopyButton | [copy-button](https://uiarc.dev/components/copy-button) | P1 | 无 | Avalonia Button | P1 P2 P4 P15 |
| 图标形变 | IconMorph | [icon-morph](https://uiarc.dev/components/icon-morph) | P3 | 无 | Control（自绘） | P15 P16 P26 P29 |
| 确认形变 | ConfirmMorph | [confirm-morph](https://uiarc.dev/components/confirm-morph) | P7 | 无 | TemplatedControl | P2 P5 P9 P14 P15 P20 P21 P22 P26 P29 |
| 主题切换 | ThemeSwitcher | [theme-switch](https://uiarc.dev/components/theme-switch) | P2 | Button | TemplatedControl | P1 P2 P29 |

## 3. 共享约定
- **尺寸**：Button 的 sm / md / lg 对应 `ControlSizeSM` 36 / `ControlSizeMD` 44 / `ControlSizeLG` 50（最小高度），水平内边距 12 / 16 / 20，字号 14、字重 500，圆角 `RadiusControl` 18，尺寸不随窗口宽度变化。组类控件自有高度：ButtonGroup 段高 34 / 42（边框内），FloatingButtonGroup 30 / 36，ExpandingButtonGroup 28 / 36；ThemeSwitcher 仅图标时 38×38、圆角 12。
- **按压与悬停（P1）**：内容缩放到 0.97，宽度不超过 48px 的按钮 0.96，宽于 220px 的按钮 0.985；按下用 instant 120ms + standard 缓动，松开用 snappy 弹回。作为弹层锚点的按钮（chevron、菜单触发器）永不缩放，只用颜色反馈，避免弹层按缩小后的矩形定位；组类控件只让段内内容或高亮下沉，组框与邻居不动。悬停样式只在鼠标 / 笔下生效（按 `PointerType` 过滤），触摸只有按下反馈。
- **变体色**：primary 用 `Foreground` 填充、`Background` 文字（不是 accent），悬停不透明度 0.91 加 `ShadowResting`，按下 0.84；secondary 为 `Surface` + `Border`；ghost 为透明底 `TextSecondary`；danger 为 `Danger` 文字的描边按钮。与 accent 无关的 color-mix 派生色（高亮、按下加深）在各自 `XxxToken` 中用中性色纯函数计算；依赖 accent 的派生色放进 ArcDesignToken（见 [控件 Token](../../architecture/theming/control-tokens.md#1-职责)）。
- **禁用与忙碌**：primary 禁用时整体淡到 0.52；secondary / danger 保留边框，填充 `SurfaceMuted`、文字 `TextMuted`；ghost 只降文字。loading、pending 与组内禁用项要保留键盘焦点：不设 `IsEnabled=false`，而是吞掉激活，并由自定义 AutomationPeer 报告不可用或忙碌；只有整组禁用才真正设 `IsEnabled=false`。
- **标签形变（P2、P5）**：新标签从 soft 4px 模糊中上移约 0.3em 进入（standard 240ms，enter 缓动），旧标签上移淡出（fast 160ms）；图标从 0.6 缩放加 subtle 2px 模糊以 snappy 进入；宽度以 morph 弹簧跟随新内容，只在内容变化后短时间内启用，字体加载等被动重排直接跳变。会切换的标签预留最宽状态（reserve、reserveLabels、doneLabel），不挤动邻居。
- **焦点与播报**：指针聚焦不画焦点环；键盘聚焦（`:focus-visible`，S7）画 2px、偏移 2px 的焦点环（`FocusRing` / `FocusRingWidth` / `FocusRingOffset`，即 `AccentStrong` 72%，见 [可访问性](../../architecture/accessibility.md)）；工具栏类组件的共享高亮同时是键盘位置指示。Saved、Copied 等结果走隐藏的 Polite 状态区（`AutomationProperties.LiveSetting`），与可见文字分离。
- **动效等级**：Reduced 去掉按压缩放、宽度弹簧、描边绘制与位移，统一换成 instant 120ms 淡入淡出；Off 立即到终态。

## 4. 组件
### 4.1 Button（Button）
`slug: button` · 阶段 P0 · Arc 依赖：无 · 建议基类：Avalonia Button

**Arc 行为摘要**：页面、表单、对话框里任何单一动作的默认选择（Save、Continue、Cancel）；标签原位变化（Save 到 Saved）时宽度弹簧过渡，短异步工作用 loading 显示旋转指示。需要在按钮上显示 pending 与 success 时改用 ActionButton，有 2 到 5 个近似变体时用 SplitButton，危险动作需要多于一次点击时用 HoldToConfirm。

**Arc API 要点**：
| Arc 属性 | 类型 / 默认值 | 建议 Avalonia 属性（类型） |
|---|---|---|
| `variant` | primary / secondary / ghost / danger，默认 primary | `Variant`（`ButtonVariant`） |
| `size` | sm / md / lg，默认 md | `Size`（Small / Medium / Large） |
| `loading` | boolean，默认 false | `IsLoading`（bool） |
| children、`disabled`、`onClick` | 标签内容与原生按钮属性 | `Content`（object）、`IsEnabled`、`Click` 事件 / `Command` |

**交互与键盘**：Enter / Space 激活；`IsLoading` 时保留焦点，但吞掉点击与键盘激活。

**可访问性**：
- 沿用 Button 角色；仅图标按钮必须设置 `AutomationProperties.Name`。
- loading 对应 aria-busy + aria-disabled：自定义 Peer 报告不可用，控件本身仍可聚焦，不因 loading 丢失键盘焦点。

**动效**：
- 按下缩放 0.97（窄图标按钮 0.96，宽按钮 0.985），按下用 instant + standard，松开用 snappy；弹层锚点不缩放（P1）。
- 新标签上移 4px 从 soft 模糊进入，旧标签上移 3px 淡出（fast），图标与 loading 指示以 0.6 缩放加 subtle 模糊、snappy 进入（P2）；标签槽宽度以 morph 弹簧跟随，形变期间加左右各外扩 12px 的裁剪（P5、P29）；loading 指示为 1.5px 圆环 0.7s 线性旋转（P21）。
- Reduced：去掉按压缩放，标签 instant 淡入淡出，旋转指示停止；Off 立即替换。

**Avalonia 实现要点**：
- 模板：`PART_LabelSlot`（`SpringSizeDecorator`，S5 观察标签尺寸）、新旧标签两个 Presenter（P2 的 `BlurSwapTransition`）、`PART_Spinner`（P21 的 `LiveLoop`）；伪类 `:loading`；按压深度按实际宽度计算。
- 服务：S1、S2、S4、S5、S7（S3 Presence 按原语目录在 P1 才落地，见风险）。`OnInitialized` 中 `DisableTransitions()`，Loaded 后启用，首帧不播标签形变；数百个按钮的长列表应关闭宽度形变。

**AtomUI 参考**：Button（仅供学习行为与边界，不复用其实现）。

**风险与未决**：与 `Avalonia.Controls.Button` 及 AtomUI `Button` 同名，XAML 用 `arc:` 前缀、C# 需要别名；作为 P1 P2 P5 P21 P29 的首个使用者，Phase 0 必须连带交付这些原语；原语目录把 P2 的 `BlurSwapTransition` 描述为 PresenceHost 转场，而 PresenceHost（S3）在 P1 落地，需决定 Phase 0 提前交付其最小子集，还是由 Button 自管新旧两个 Presenter。

### 4.2 ActionButton（Action button）
`slug: action-button` · 阶段 P3 · Arc 依赖：无 · 建议基类：Arc Button

**Arc 行为摘要**：带尾随箭头的行动号召按钮，用于单次异步提交（Save、Publish、Submit），pending 与 success 直接显示在按钮上，成功后按 resetAfterMs 自动回到 idle。普通表单提交用带 loading 的 Button，危险且需要刻意按压的动作用 HoldToConfirm。

**Arc API 要点**：
| Arc 属性 | 类型 / 默认值 | 建议 Avalonia 属性（类型） |
|---|---|---|
| `label`（必填） | string，idle 标签兼可访问名称 | `Label`（string） |
| `onAction`（必填） | 返回 void 或 Promise，pending 持续到 Promise 结束 | `AsyncAction`（`Func<Task>`），兼容 `Command` |
| `pendingLabel` / `successLabel` | string，默认 "Saving" / "Saved" | `PendingLabel` / `SuccessLabel`（string，默认值走本地化） |
| `resetAfterMs` | number，默认 2400，0 保持成功态 | `ResetAfter`（TimeSpan，Zero 保持） |
| `onActionError` | 回调，按钮回到 idle | `ActionFailed` 事件（携带 Exception） |

**交互与键盘**：Enter / Space 执行；pending 期间保留焦点，重复激活被忽略。

**可访问性**：
- 可见标签对辅助技术隐藏，Name 固定取 `Label`，避免逐字形变时被拆字朗读。
- pending 时 Peer 报告不可用但保留焦点；隐藏的 Polite 状态区播报 pending 与 success 标签。

**动效**：
- 变化的字母从 soft 模糊中升起（standard 240ms，按 char 16ms 交错），未变字母按位置滑动（P4）；宽度以 morph 弹簧跟随（P5）。
- 箭头沿动作方向右移 8px 淡出（fast），复位时从左 6px 处回来；pending 显示与 Button 相同的 0.7s 旋转指示（P21）；成功时对勾以 standard 240ms 自绘（P2、P15）；按下缩放 0.97，snappy 回弹（P1）。
- Reduced：去掉按压缩放、宽度弹簧与描边绘制，替换改为 instant 淡入淡出；Off 立即替换。

**Avalonia 实现要点**：
- 派生自 Arc Button，复用按压与焦点；内部状态 Idle / Pending / Success 以伪类 `:pending`、`:success` 暴露；逐字形变复用 CopyButton 落地的字形呈现器。
- 复位用 S12 `PausableTimer`，卸载时取消；每个字母一个动画元素，适合少量按钮，不要放进密集表格。

**AtomUI 参考**：Button(IsLoading)、PopupConfirm（仅供学习行为与边界，不复用其实现）。

**风险与未决**：`AsyncAction` 与 `Command` 并存的语义需在 spec 定稿（同步命令视为立即成功）；Arc 文档提到的 action-swap 不在免费目录中，不作为替代引用。

**实现说明（已落地）**：派生 Avalonia `Button`（不暴露无意义的 Variant / Size / IsLoading），`AsyncAction`（`Func<Task>`）在 Click / Command 之后运行，无异步动作视为立即成功；只读 `State` + `:pending` / `:success`、`ActionFailed`（带异常）、`Reset()`；标签用 `TextMorph`（PrefixSuffix），图标槽为 _Shared `ButtonsGlyphSwap`（箭头 +8px 离开、−6px 返回，旋转指示与对勾 0.6 缩放 + 2px 模糊 snappy 进入，对勾 240ms 自绘）；pending 保留焦点、Peer 报告不可用，Name 恒为 Label，Polite 播报 Saving / Saved。

### 4.3 SplitButton（Split button）
`slug: split-button` · 阶段 P5 · Arc 依赖：无 · 建议基类：TemplatedControl

**Arc 行为摘要**：一个默认动作连着一个 chevron 菜单，菜单里是 2 到 5 个近似变体（Merge pull request 搭配 Squash and merge、Rebase and merge），也适合有常用格式的导出与分享，以及把 Copy page 原位换成 Copied 的复制。所有选项地位相同用 DropdownMenu，没有备选用 Button，针对内容对象的动作用 ContextMenu。

**Arc API 要点**：
| Arc 属性 | 类型 / 默认值 | 建议 Avalonia 属性（类型） |
|---|---|---|
| `label`（必填）/ `onClick` | 主动作标签（变化时逐字形变）/ 主动作回调 | `Label`（string）/ `Click` 事件、`Command` |
| `actions`（必填） | 项：label、onSelect、disabled、destructive、icon | `Actions`（与 DropdownMenu 共用的菜单项集合） |
| `icon` | 换成不同图标时交叉淡化 | `Icon`（object） |
| `variant` | primary / secondary，默认 primary | `Variant`（Primary / Secondary） |
| `disabled` | boolean，同时禁用主按钮与菜单触发器 | `IsEnabled` |

**交互与键盘**：Enter / Space 在主按钮上执行主动作、在 chevron 上打开菜单；ArrowDown / ArrowUp 在菜单项间移动并首尾循环；Escape 关闭菜单并把焦点还给 chevron。

**可访问性**：
- 两个按钮：chevron 的 Name 为 `{Label} more actions`（本地化），暴露 ExpandCollapse；菜单为 Menu / MenuItem 角色。
- 主标签变化经 Polite 状态区播报。

**动效**：
- 主标签逐字形变（char 16ms 交错，上限 100ms），图标交叉淡化，宽度 morph 弹簧（P2、P4、P5）。
- 主半区按压缩放（P1）；chevron 永不缩放，菜单从静止锚点打开（end 对齐、间距 4px、碰撞边距 12px）（P10）；菜单项 standard 240ms 淡入，按序号交错 35ms，最多计到第 5 项（P19）。
- Reduced：去掉按压缩放、宽度弹簧与菜单变换，只留快速淡入；Off 立即显示。

**Avalonia 实现要点**：
- 模板部件 `PART_PrimaryButton`、`PART_MenuButton`；菜单复用 DropdownMenu 的 MenuSurface（同为 P5，须先落地），浮层 S8、菜单键盘 S9、项退场 S3。
- secondary 变体主半区最小宽 142px、字号 12；两半不随窄屏折叠，窄布局应独占一行。

**AtomUI 参考**：SplitButton（仅供学习行为与边界，不复用其实现）。

**风险与未决**：与 `Avalonia.Controls.SplitButton` 及 AtomUI `SplitButton` 同名，C# 需要别名；共享菜单项模型的命名需与 Menus 大类统一。

### 4.4 ButtonGroup（Button group）
`slug: button-group` · 阶段 P5 · Arc 依赖：无 · 建议基类：ItemsControl

**Arc 行为摘要**：同一对象上 2 到 5 个相关动作连成一个带细分隔线的表面（文档的 Share、Duplicate、Archive，缩放控件，Reply、Reply all、Forward），末尾可挂 chevron 菜单收纳低频与危险动作。选项需要保持按下用 SegmentedControl，一个主动作加变体用 SplitButton，切换视图用 Tabs。

**Arc API 要点**：
| Arc 属性 | 类型 / 默认值 | 建议 Avalonia 属性（类型） |
|---|---|---|
| `items`（必填） | 段：id、label、reserve、icon、iconOnly、content、onSelect、href、disabled | `ItemsSource`，容器 `ButtonGroupItem`（Label、ReserveLabels、Icon、IsIconOnly、Content、Command、NavigateUri、IsEnabled） |
| `label`（必填）/ `menu` | 组名称 / `{ label, items }` 末尾 chevron 段 | `Label`（string）/ `MenuLabel`（string）+ `MenuItems`（共享菜单项） |
| `variant` | outline / solid，默认 outline | `Variant`（Outline / Solid） |
| `size` / `orientation` | sm / md，默认 md / horizontal、vertical | `Size`（Small / Medium）/ `Orientation` |
| `collapseLabels` | boolean，默认 true | `IsLabelCollapseEnabled`（bool） |
| `disabled` | boolean，禁用所有段与菜单 | `IsEnabled` |

**交互与键盘**：
- 每段都是 Tab 停靠点；水平组左右、垂直组上下方向键在段间移动并循环（RTL 镜像），Home / End 到首尾，Enter / Space 激活。
- chevron 段上 Enter / Space / ArrowDown 打开菜单并高亮首项；菜单内方向键循环并跳过禁用项，Escape 关闭并还焦点，Tab 关闭并继续前进（非模态）。

**可访问性**：
- 根为 Group 角色并以 `Label` 命名；禁用段保持可聚焦并报告不可用；仅图标、实时值与折叠后的段以 Label 作为 Name 和 ToolTip，图标隐藏。
- 按下后 1 秒内的标签变化（Share 到 Copied）播报一次，安静复原不播报；状态不只靠颜色表达。

**动效**：
- 所有段共用一个高亮，随指针或键盘焦点以 snappy 滑到目标段，从组外进入时在落点淡入；高亮两侧的分隔线淡出（P7）。
- 按下时段内容缩到 0.97（仅图标 0.9）后弹回，高亮加深，组与邻居不动；chevron 只加深高亮（P1）。
- 预留的标签在固定盒内交叉淡化，未预留的标签以 morph 改变段宽且高亮锁定跟随（P2、P5）；菜单从组边缘以短位移与缩放长出，chevron 打开时翻转（P10）。
- Reduced：高亮直接跳转，去掉按压下沉与 chevron 翻转，标签与菜单交叉淡化；Off 立即到位。

**Avalonia 实现要点**：
- 高亮是一个由 x / y / width / height 动效值驱动的元素，按亚像素测量段位置；两个尺寸观察（S5）分别负责高亮对齐与标签折叠，标签槽另各有一个。
- 菜单复用 MenuSurface（S8、S9），非模态、不锁滚动；带 NavigateUri 的段用 Hyperlink 语义，经 `TopLevel.Launcher` 打开。

**AtomUI 参考**：CompactSpace、OptionButtonGroup（仅供学习行为与边界，不复用其实现）。

**风险与未决**：折叠标签后仍放不下时 Arc 无滚动条横向滚动，需要隐藏滚动条的 ScrollViewer 样式；`ButtonGroupItem` 的实时值 `Content` 与容器自身的 Content 语义重叠，命名需在 spec 定稿。

### 4.5 FloatingButtonGroup（Floating button group）
`slug: floating-button-group` · 阶段 P2 · Arc 依赖：Tooltip · 建议基类：ItemsControl

**Arc 行为摘要**：安静托盘里彼此分开的柔和按钮，一个共享高亮在按钮间形变移动；用于浮动编辑器或画布工具栏（Undo、Redo、Share、Present）、文本选区工具栏和垂直工具栏。它是动作工具栏，不保存选中；保持选中的开关用 SegmentedControl，一行放不下时用 DropdownMenu。

**Arc API 要点**：
| Arc 属性 | 类型 / 默认值 | 建议 Avalonia 属性（类型） |
|---|---|---|
| `items`（必填） | 按钮（label、icon、iconOnly、onSelect、disabled、pressed、shortcut、reserveLabels）或分隔符 | `ItemsSource`：`FloatingButtonGroupItem`（Label、Icon、IsIconOnly、IsEnabled、IsChecked、ShortcutHint、ReserveLabels）与分隔符项 |
| `label`（必填） | string，工具栏名称 | `Label`（string） |
| `variant` | muted / floating，默认 muted | `Variant`（Muted / Floating） |
| `size` / `orientation` | sm / md（按钮高 30 / 36px）/ horizontal、vertical | `Size`（Small / Medium）/ `Orientation` |
| `iconOnly` / `tooltipSide` | 默认 false，项级优先 / 行默认 top、列默认 right | `IsIconOnly`（bool）/ `TooltipPlacement`（Top / Bottom / Left / Right） |
| `onAction` | 项 id 回调，在项自身回调之后 | `ActionInvoked` 路由事件 |

**交互与键盘**：单一 Tab 停靠点（回到上次聚焦的按钮）；行内左右、列内上下方向键在可用按钮间移动并循环，Home / End 到首尾；Enter / Space 执行，按住期间高亮加深；shortcut 只在提示中显示，组件不绑定按键。

**可访问性**：
- 托盘为 ToolBar 角色，带 Name 与方向；分隔符为 Separator 角色；pressed 项暴露 Toggle 状态。
- 每个按钮以 Label 命名；仅图标或带快捷键的按钮显示 Tooltip，提示文字作为 HelpText；禁用项保持可读但被方向键跳过；共享高亮只为键盘焦点显示。

**动效**：
- 高亮以 morph 弹簧移动位置与尺寸，指针跨过间隙或分隔符时停留不动（P7）。
- 从无到有时在落点从 94% 以 snappy 长出，指针离开托盘时淡出；按下时高亮加深并以 snappy 收到 96.5%，按钮本身不缩放（P1）。
- 标签原位交叉淡化（短上移加模糊），有 reserveLabels 时按钮已是最宽标签的宽度，否则高亮重测并跟随（P2、P5）；Tooltip 弹出用 P10。
- Reduced：高亮跳到目标并短暂淡入，标签无位移交叉淡化；Off 直接到位。

**Avalonia 实现要点**：
- 高亮在托盘坐标中测量、按设备像素取整并补偿祖先变换；判断指针离开托盘时要排除 Tooltip 浮层。漫游焦点用 S9，一个尺寸观察（S5）同时观察托盘与按钮。
- reserveLabels 用 Grid 叠放隐藏副本交给布局测量；floating 变体用 `ShadowFloating` 加细边，muted 托盘色为前景 4.5%，与 accent 无关，在 `FloatingButtonGroupToken` 中用中性色纯函数计算。

**AtomUI 参考**：FloatButtonGroup（仅供学习行为与边界，不复用其实现）。

**风险与未决**：Arc 的 left / right 提示是贴着列的自绘提示而非库 Tooltip，Avalonia 侧是否统一用 Tooltip 需在 spec 决定。

**实现说明（已落地）**：`ItemsControl` + 容器 `FloatingButtonGroupItem : Button`（Label、Icon、`IsIconOnly`（bool?）、`IsChecked`（bool?，非空时 Toggle Peer）、Shortcut、ReserveLabels）与 `FloatingButtonGroupSeparator`，数据项自动成按钮，`ActionInvoked` 在项 Click 之后；共享高亮复用 Core `GlidingIndicator`（出现时 94%→100% snappy，按住 96.5% 并加深），按托盘坐标命中、间隙不动、只有键盘焦点占用；TabNavigation Once + 方向键循环跳过禁用项；仅图标 / 带快捷键项挂 Arc `Tooltip`；托盘 / 高亮 / 分隔色为中性色 Token，pressed 色调以 `ControlOn` 叠层 12% / 16% 近似 color-mix，ReserveLabels 由 `ButtonsLabelSizer` 预留。

### 4.6 ExpandingButtonGroup（Expanding button group）
`slug: expanding-button-group` · 阶段 P7 · Arc 依赖：无 · 建议基类：ItemsControl

**Arc 行为摘要**：紧凑的图标按钮组，指向或聚焦的按钮展开显示标签、邻居滑开，动作完成后原位确认（Archive 变 Archived）；适合消息、文件、卡片上 3 到 6 个相关动作。选择值用 SegmentedControl，只有一两个动作且空间足够时用带标签的 Button，超过约 6 个动作用 DropdownMenu。

**Arc API 要点**：
| Arc 属性 | 类型 / 默认值 | 建议 Avalonia 属性（类型） |
|---|---|---|
| `items`（必填） | label、icon、onSelect（可返回 false 或 Promise）、doneLabel、tone、disabled | `ItemsSource`：`ExpandingButtonGroupItem`（Label、Icon、DoneLabel、Tone、IsEnabled、`Func<Task<bool>>` 动作） |
| `label`（必填） | string，工具栏名称 | `Label`（string） |
| `size` | sm / md，高 28 / 36px，图标 14 / 16px | `Size`（Small / Medium） |
| `defaultExpanded` | 静止时展开的项 id，默认第一个可用项 | `DefaultExpandedItem`（object） |
| `onAction` / `confirmDuration` | 项 id 回调（在项回调之前）/ 默认 1400ms | `ActionInvoked` 路由事件 / `ConfirmDuration`（TimeSpan） |

**交互与键盘**：单一 Tab 停靠点（上次聚焦项，否则静止展开项）；左右方向键移动并循环，聚焦项展开；Home / End 到首尾；Enter / Space 执行。触摸没有悬停：第一次点按展开、第二次执行（静止展开项第一次即执行），点按组外收起。

**可访问性**：
- 根为 ToolBar；折叠时仍以完整 Label 作为 Name，可见标签与图标隐藏，避免重复朗读。
- 禁用项保持可聚焦、仍会展开但不执行；确认词经 Polite 状态区播报（重复动作再次播报）；动作未完成时报告忙碌。

**动效**：
- 按钮宽度以 morph 弹簧在图标宽与图标加标签宽之间变化，邻居在流式布局中滑开，组宽跟随（P5）。
- 标签槽预留 label 与 doneLabel 中较长者；标签从图标后滑出并带短淡入与模糊，收回更快（P2）；确认时对勾以 standard 240ms 自绘，旧词上移离开、新词升起（P15）。
- 指针进入邻居 4px 才转移展开，避免按钮滑到静止指针下误触发；按下只缩小图标，按钮盒不缩放（P1）。
- Reduced：标签与宽度立即变化并快速淡入，对勾直接出现；Off 同样立即。

**Avalonia 实现要点**：
- 标签宽度由槽内不可见副本测量（S5），宽度是动效值，只有展开项变化时才触发布局；组应起始对齐并预留最宽宽度（5 个 md 动作约 260 到 280px）。
- 漫游焦点用 S9；动作返回 false 或抛出时跳过确认；确认停留计时用 S12 `PausableTimer`。

**AtomUI 参考**：FloatButtonGroup（仅供学习行为与边界，不复用其实现）。

**风险与未决**：触摸"两次点按"模型需在桌面触屏上验证；宽度动画会触发组内布局，不宜放进大型可重排容器（Arc 性能说明）。

**实现说明（已落地）**：`ItemsControl` + `ExpandingButtonGroupItem : Button`（Label、Icon、DoneLabel、Tone、`IsDisabled`（可聚焦、仍展开、不执行）、`AsyncAction`（`Func<Task<bool>>`，false 或异常跳过确认）），只读 `ExpandedItem` / `IsExpanded` / `IsDone` / `IsBusy`，`ActivateAsync(item)`；项宽在 `MeasureOverride` 中以 morph `MotionValue` 在高度与 start+icon+gap+最长词+end 之间弹簧，标签显隐代码驱动（进 240ms enter 延迟 40ms、出 120ms，6px + 2px 模糊，主题无法表达非对称时长）；鼠标悬停 4px 迟滞、触摸两次点按、组外点按收起，确认停留用 `PausableTimer`，Polite 重复播报。

### 4.7 CopyButton（Copy button）
`slug: copy-button` · 阶段 P1 · Arc 依赖：无 · 建议基类：Avalonia Button

**Arc 行为摘要**：复制一个字符串并立即原位确认，放在 API key、安装命令、链接、代码片段旁边，也可在密集行中以仅图标形式出现。复制还有备选（Copy link、Copy as Markdown）时用 SplitButton，完整代码示例用自带复制控件的 CodeBlock。

**Arc API 要点**：
| Arc 属性 | 类型 / 默认值 | 建议 Avalonia 属性（类型） |
|---|---|---|
| `value`（必填） | string，要复制的文本 | `Value`（string） |
| `label` | string，默认 "Copy" | `Label`（string，默认值走本地化） |
| `iconOnly` / `variant` | 默认 false / outline、plain，默认 outline | `IsIconOnly`（bool）/ `Variant`（Outline / Plain） |
| `disabled` / `onCopied` | boolean / 成功后回调 | `IsEnabled` / `Copied` 事件 |

**交互与键盘**：Enter / Space 复制。

**可访问性**：
- Name 始终取 Label（仅图标时也是）；Polite 状态区播报 `{Label}: Copied` 或 `{Label}: Could not copy`（本地化）。
- 标签单元预留三种状态中最宽者，布局从不位移。

**动效**：
- 图标经 soft 模糊交换，缩放用慢而近临界阻尼的弹簧（visualDuration 0.5、bounce 0.06），对勾以 0.5s 自绘（P2、P15）。
- 标签只移动变化的字母，复位时反向播放（P4）；按下以 instant 120ms 缩到 0.97（仅图标 0.96），松开按 `EaseSpring` / `DurationSpring`（580ms CSS 弹簧曲线）回弹（P1）。
- Reduced：图标与文字 instant 淡入淡出，跳过描边绘制与按压缩放；Off 立即替换。

**Avalonia 实现要点**：
- 剪贴板走 `TopLevel.Clipboard`（异步），失败显示 Failed 态而不抛出；状态 Idle / Copied / Error 在 1900ms 后复位（S12 `PausableTimer`，本组件是首批使用者），把 Arc 的 use-copy-feedback 提取为内部服务，供 CodeBlock 等复用。
- 作为 P4（`GlyphMorphPresenter`）与 P15（`StrokeDrawPresenter`）的首批使用者，Phase 1 需连带交付字形呈现器与路径长度测量（StrokeDashArray / StrokeDashOffset）。

**AtomUI 参考**：无。

**风险与未决**：失败路径（Failed 态与 Could not copy 播报）需在 Headless 测试中模拟剪贴板失败来覆盖；图标交换的 0.5 / 0.06 弹簧不属于标准预设，需在 `CopyButtonToken` 中以 TimeSpan 加 double 的弹簧参数表示。

### 4.8 IconMorph（Icon morph）
`slug: icon-morph` · 阶段 P3 · Arc 依赖：无 · 建议基类：Control（自绘）

**Arc 行为摘要**：八个双态图标的统一入口，切换时形变而非替换：menu 到 close、play 到 pause、plus 或 copy 到 check、sun 到 moon、chevron 旋转、bell 开关、heart 点赞。放在含义随状态翻转的图标按钮里；完整复制控件用 CopyButton，带标签的明暗切换用 ThemeSwitcher，从不变化的图标直接用 Lucide 图标。

**Arc API 要点**：
| Arc 属性 | 类型 / 默认值 | 建议 Avalonia 属性（类型） |
|---|---|---|
| `name`（必填） | menu / play / plus / copy / theme / chevron / bell / heart | `Kind`（`IconMorphKind`） |
| `state` | boolean，默认 false（第二状态） | `IsActive`（bool） |
| `size` / `strokeWidth` | 默认 24px / 1.75（24 单位网格） | `IconSize`（double）/ `StrokeThickness`（double，按 size / 24 缩放） |
| ChevronIcon `turn` / `pointing` | 默认 180 度 / down | `ChevronTurn`（double）/ `ChevronPointing`（Down / Right） |
| 子导出 MenuIcon、HeartIcon 等 | 属性同 IconMorph（无 name） | 合并进 `Kind`，不单独建类 |

**交互与键盘**：图标不可聚焦、不处理键盘，由外层按钮负责。

**可访问性**：
- 图标对辅助技术隐藏（`AccessibilityView=Raw`），名称由外层按钮提供。
- 状态必须反映在按钮上：Toggle（aria-pressed）、ExpandCollapse（aria-expanded）或变化的 Name。

**动效**：
- 线条旋转平移、路径数据插值，都在可打断弹簧上（menu、play、plus、theme、chevron 用 morph，copy 与 bell 斜线用 smooth），快速切换从当前位置反向（P16）。
- copy 到 check 时纸张收起、对勾描边绘入（P15）；sun 到 moon 与 bell 斜线用遮罩裁切（P29）。
- heart 点赞时弹出放大并带一次光环与火花（P26）；bell 开启时 0.9s 摇铃（钟舌滞后 40ms）；两者都是每次开启重播的一次性动画。
- Reduced：所有形变立即完成，跳过摇铃与爆发；Off 同样立即。

**Avalonia 实现要点**：
- 自绘 `Control`，在 `Render` 中用 `DrawingContext` 画 24 单位网格路径；路径插值要求新旧路径命令结构一致（本组件与 Checkbox 是 P16 `PathMorph` 的首批使用者）。
- 遮罩用 `Geometry.Combine` 或 `OpacityMask`，每实例独立；颜色继承 `Foreground`；点赞色是控件 Token（默认 `Danger`，对应 Arc 的 `--icon-morph-like`，见 [Token 目录](../../architecture/theming/token-catalog.md#18-待组件规格补充的-token)）。

**AtomUI 参考**：无。

**风险与未决**：摇铃是固定关键帧（0.9s，easeOut），不属于任何原语，在组件内局部实现；八组路径数据需从 Arc 源码逐一移植并按 24 单位网格核对。

**实现说明（已落地）**：自绘 `Control`，`Kind` + `IsActive`（双向）+ `IconSize`（24）/ `StrokeThickness`（1.75，随尺寸缩放）/ `ChevronTurn` / `ChevronPointing` / `LikeBrush`（主题为 Danger）；Play↔Pause、Plus↔Check 走 Core P16 `PathMorph`（同结构逐顶点、可打断保速），其余为 `MotionValue` 驱动的旋转平移、缩放与 pathLength；日月与铃铛斜线遮罩为常驻 `CombinedGeometry(Exclude)`，值变化时就地更新；摇铃（0.9s easeOut，钟舌滞后 40ms）与点赞光环 + 火花只在 Full 下随开启重播，Reduced / Off 立即到终态，图标 `AccessibilityView=Raw`。

### 4.9 ConfirmMorph（Confirm morph）
`slug: confirm-morph` · 阶段 P7 · Arc 依赖：无 · 建议基类：TemplatedControl

**Arc 行为摘要**：危险或重要动作的按钮原位形变成一行确认问题，然后是旋转指示，最后是带 Undo 的结果，可以点按、长按或点按后短倒计时作答；用于删除选中的文件、撤销访问、移除成员、丢弃草稿。后果需要多行解释或要求输入名称时用 Dialog，安全的异步动作用 ActionButton，必须杜绝误触时用 HoldToConfirm。

**Arc API 要点**：
| Arc 属性 | 类型 / 默认值 | 建议 Avalonia 属性（类型） |
|---|---|---|
| `label`（必填）/ `icon` / `prompt` | prompt 默认为 label 加问号 | `Content` / `Icon` / `Prompt`（object） |
| `confirmLabel` 等 8 个文案 | 默认 Delete、Cancel、Deleting、Deleted、Couldn’t finish、Retry、Undo、Restoring | 同名 string 属性，默认值走本地化 |
| `tone` | danger / neutral，默认 danger（只影响确认按钮） | `Tone`（Danger / Neutral） |
| `confirmMode` / `holdDuration` / `countdown` | press / hold / countdown；1000ms；3s | `ConfirmMode`、`HoldDuration`（TimeSpan）、`CountdownSeconds`（int） |
| `onConfirm` / `onUndo` | 返回 Promise 显示 pending，拒绝进入 error 面 | `ConfirmAction` / `UndoAction`（`Func<Task>`） |
| `state` / `defaultState` / `onStateChange` | idle / confirming / pending / done / error | `State`（`ConfirmMorphState`，双向）+ `StateChanged` 事件 |
| `confirmTimeout` / `resultTimeout` / `cancelOnOutsidePress` | 6000 / 5000ms（悬停暂停，0 关闭）/ 默认 true | `ConfirmTimeout` / `ResultTimeout`（TimeSpan）/ `IsOutsidePressCancelEnabled`（bool）+ `Cancelled` 事件 |

**交互与键盘**：
- Enter / Space 按下当前面上的按钮（开始、Confirm、Cancel、Undo、Retry），hold 模式按住填充；提问时 Confirm 获得焦点，Enter 即确认，倒计时中 Enter 立即执行。
- Tab / Shift+Tab 在 Cancel 与 Confirm 间移动；Escape 取消提问或倒计时，或把结果收回静止态。

**可访问性**：
- 提问面是以 prompt 命名的 Group；打开后 280ms 内的按压与自动重复按键被忽略，防止双击或按住 Enter 误确认。
- 焦点跟随形变：提问时在 Confirm，倒计时或 pending 时在根（报告忙碌），结果时在 Undo 或 Retry，静止时回到触发按钮；hold 模式 HelpText 为 "Press and hold"，过早松开会播报需要更久。prompt、pending、结果与 "Undo is available" 经 Polite 状态区播报。

**动效**：
- 只有药丸宽度变化：变宽 morph、变窄 smooth，圆角与文字不缩放（P5、P9）；新面各部分按阅读顺序依次清除模糊进入（item 35ms），旧面原地淡出（P20、P2）。
- done 对勾弹出并自绘（P26、P15）；倒计时数字用等宽数字切换（P14）；pending 显示旋转指示（P21）。
- hold 模式下危险色实心副本随按压从左侧揭示，提前松开以弹簧退回（P29）；超时是一条可暂停的线性动效值（P22）。
- Reduced：去掉宽度弹簧、模糊与描边，面交叉淡化，旋转变慢，hold 填充与倒计时环仍显示进度；Off 立即切换。

**Avalonia 实现要点**：
- 模板部件 `PART_Pill`（宽度动效）、面 Presenter（只渲染当前面与退场面，S3）、`PART_HoldFill`；每个面一个尺寸观察（S5）。
- 超时用 S12 可暂停计时器（指针悬停或窗口不可见时暂停）；外部按压取消依赖 S8 的外部点击检测；hold 模式与 HoldToConfirm 共享按压计时。

**AtomUI 参考**：PopupConfirm（仅供学习行为与边界，不复用其实现）。

**风险与未决**：5 种状态 × 3 种确认模式 × 受控 / 非受控的状态机复杂，spec 需给出完整转移表与测试矩阵；Arc 的"标签页隐藏时暂停"在桌面上对应窗口最小化或被遮挡，需定义检测方式。

**实现说明（已落地）**：TemplatedControl，六个面全部在模板中声明，内部按钮用同文件的键控 ControlTheme（焦点只靠底色）；`State`（双向）+ `StateChanged` / `Cancelled`、`ConfirmAction` / `UndoAction`（`Func<Task>`）、`Ask()` / `Cancel()`；药丸宽度 `MotionValue` 变宽 morph、变窄 smooth 后交还 auto，新面各部分 35ms 交错清除 4px 模糊、旧面 120ms 淡出；280ms 武装期、Enter / Escape / 外部按压 / 悬停与最小化暂停的超时（`PausableTimer`）；Hold 模式与 HoldToConfirm 共享 `ButtonsHoldProgress`（回退弹簧 0.3s），倒计时用 `ButtonsTimeline` 驱动排空环与 Roll 秒数，Reduced 旋转变慢为 1.6s。

### 4.10 ThemeSwitcher（Theme switcher）
`slug: theme-switch` · 阶段 P2 · Arc 依赖：Button · 建议基类：TemplatedControl

**Arc 行为摘要**：顶栏里单个明暗切换：一个 secondary Button 交换太阳与月亮图标并报告下一个主题，页面过渡由使用方执行。四个变体对应四种页面过渡：reveal 从按钮处展开，[eclipse](https://uiarc.dev/components/theme-switch-eclipse) 像日食一样扫过页面（适合展示页），[split](https://uiarc.dev/components/theme-switch-split) 从中缝打开（与按钮位置无关，适合居中布局），[rise](https://uiarc.dev/components/theme-switch-rise) 从底部升起（最安静，Arc 自身顶栏用它的仅图标形式）。主题选择含 system 且属于账户菜单时用 UserMenu。

**Arc API 要点**：
| Arc 属性 | 类型 / 默认值 | 建议 Avalonia 属性（类型） |
|---|---|---|
| `theme`（必填） | light / dark，驱动图标与 aria-pressed | `ThemeVariant`（Light / Dark；`Theme` 已被 `StyledElement.Theme` 占用） |
| `onThemeChange`（必填） | `(next, variant, trigger)` | `ThemeChangeRequested` 事件（Next、Variant、触发按钮） |
| `variant` | reveal / eclipse / split / rise，默认 reveal | `Variant`（`ThemeSwitcherVariant`） |
| `label` / `iconOnly` | 默认 "Switch to dark mode" 或 "Switch to light mode" / 默认 false | `Label`（string，默认值走本地化）/ `IsIconOnly`（bool，38×38 方形） |

**交互与键盘**：Enter / Space 切换主题。

**可访问性**：
- 内部是 Arc Button，暗色时 Toggle 状态为 On（aria-pressed）；Name 默认指明下一个主题，图标隐藏。
- Reduced 时不播放页面过渡，直接切换。

**动效**：
- 太阳与月亮以共享旋转（±30 度）、缩放 0.7 与 subtle 模糊在 snappy 上交换，退场 fast 160ms（P2）；暗色时图标按变体轻推：reveal 放大到 1.08，eclipse 左移 2px，split 右移 1px，rise 上移 2px；按压缩放由 Button 负责（P1）。
- 页面过渡在 Arc 中交给 View Transition；Avalonia 侧由辅助类快照旧画面，切换主题后按变体裁切揭示新画面（P29）。
- 首个主题不做动画；Reduced 去掉轻推、图标 instant 淡入并跳过页面过渡；Off 立即切换。

**Avalonia 实现要点**：
- 模板包含一个 secondary Arc Button 与图标交换区；事件参数带触发按钮在 TopLevel 中的中心点，作为揭示原点。
- 建议在 Core 提供页面过渡辅助：`RenderTargetBitmap` 截取 TopLevel，经 AtomUI `IThemeManager` 切换主题，把旧快照放进 ScopeAwareOverlayLayer 并做圆形 / 扫掠 / 中缝 / 自下而上的裁切动画，结束后移除。

**AtomUI 参考**：无（主题切换走 ThemeManager API）。

**风险与未决**：高 DPI 大窗口的全窗口快照内存与 AtomUI 重算 Token 的耗时需 spike 验证；是否在控件内直接调用 `IThemeManager`（Arc 只报告不应用）需在 spec 决定。

**实现说明（已落地）**：TemplatedControl 内含复用 Arc Button 主题的 `ThemeSwitcherButton`（Toggle Peer，暗色为 On），`Appearance`（null 跟随 ActualThemeVariant）/ `Variant` / `Label` / `Text` / `IsIconOnly`（38×38、圆角 12）；先发 `ThemeChangeRequested`（Next、Variant、Trigger、Origin），未处理时经 `IThemeManager.ApplyThemeAsync` 沿用最近一次请求的配置只切换 `ThemeAlgorithm.Dark`（未观察到请求时保留当前 accent 预设）；View Transition 以 `RenderTargetBitmap` 快照放入 OverlayLayer、按变体裁切揭示新主题（Considered 480ms + InOut，Arc 免费源码无过渡 CSS），公开 `RunTransitionAsync`，Reduced / Off 直接切换；日月 ±30° 共享旋转交换，暗色按变体轻推（仅 Full）。

## 5. 实现顺序与依赖
- P0：Button，首个落地 P1 P2 P5 P21 P29，是 ActionButton 与 ThemeSwitcher 的基础。
- P1：CopyButton，与 TextMorph 一起首个落地 P4，首个落地 P15，并是 S12 `PausableTimer` 的首批使用者；Tables 的 CodeBlock（P2）依赖它。
- P2：FloatingButtonGroup（依赖 Overlays 的 Tooltip，P1）；ThemeSwitcher（依赖 Button，页面过渡辅助随它落地）。
- P3：ActionButton（派生自 Button，复用 P4）、IconMorph（与 Toggles 的 Checkbox 一起首个落地 P16）。
- P5：SplitButton、ButtonGroup，依赖 Menus 的 DropdownMenu 在同阶段先交付 MenuSurface 共享表面。
- P7：ExpandingButtonGroup；ConfirmMorph 依赖已落地的 P9（ExpandableCard，P4）、P20（TextReveal，P3）、P22（NumberField，P6），hold 模式与 Gestures 的 HoldToConfirm 同阶段共享按压计时。

## 6. 相关文档
- [组件总清单](../overview.md) · [规格模板](../spec-template.md) · [路线图](../../roadmap/overview.md)
- [动效原语目录](../../architecture/motion/primitives-catalog.md) · [Token 目录](../../architecture/theming/token-catalog.md)
- [浮层与输入](../../architecture/overlays-and-input.md) · [AtomUI 集成（短名冲突）](../../architecture/atomui-integration.md)
- 相邻大类：[手势](../gestures/overview.md) · [菜单](../menus/overview.md) · [浮层](../overlays/overview.md) · [开关与选项](../toggles/overview.md)
