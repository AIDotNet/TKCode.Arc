# 菜单（Menus）

> Arc Actions 分组中锚定在触发器、指针或头像上的命令列表：通用下拉菜单、右键菜单与账户菜单，三者共用一套菜单表面。

## 1. 大类定位
- 解决"命令多于可见按钮"：带标签触发器后的命令列表用 DropdownMenu，内容对象上的次要动作用 ContextMenu（右键、Shift+F10、ContextMenu 键），顶栏唯一的账户入口用 UserMenu（身份、主题、在线状态、退出登录）。
- 边界：有一个默认命令时用 Buttons 的 SplitButton；触摸优先的列表行操作用 Gestures 的 SwipeActions；独立的明暗切换用 Buttons 的 ThemeSwitcher；非命令的富内容浮层用 Overlays 的 Popover。
- 共同特征：同一套菜单表面（`SurfaceRaised`、`RadiusPanel`、`ShadowFloating`、5px 内边距）、一个在行间移动的共享高亮（指针滑动、键盘跳转）、短交错的行进入；触发器是锚点，永不缩放；菜单项按压只着色。

## 2. 组件一览
| 组件 | 类名 | Arc 页面 | 阶段 | Arc 依赖 | 建议基类 | 原语 |
|---|---|---|---|---|---|---|
| 下拉菜单 | DropdownMenu | [dropdown-menu](https://uiarc.dev/components/dropdown-menu) | P5（已实现） | 无 | TemplatedControl + MenuSurface | P2 P5 P7 P10 P19 |
| 右键菜单 | ContextMenu | [context-menu](https://uiarc.dev/components/context-menu) | P5（已实现） | 无 | ContentControl + MenuSurface | P7 P10 P19 |
| 账户菜单 | UserMenu | [user-menu](https://uiarc.dev/components/user-menu) | P5（已实现） | 无 | TemplatedControl + MenuSurface | P7 P10 P11 P12 P19 |

## 3. 共享约定（`Menus/_Shared/`，公开类型）
- **MenuSurface**（`HeaderedItemsControl`）：三者共用、也供 SplitButton、ButtonGroup（Buttons）与 SwipeActions（Gestures）复用的菜单表面。它自己持有一个懒创建、可复用的 `MenuSurfacePopup`（Core `ArcPopup` 子类），负责定位与翻转（S8）、共享高亮、行交错进入、方向键与 typeahead（S9）、退场保活（S3）、外部按压关闭与焦点归还。
  - 打开：`Open(anchor, MenuOpenFocus)`（按 `Side` / `Alignment` / `AnchorGap` 贴边）、`OpenAt(anchor, point, focus)`（从某点长出，再次调用即移到新点）、`Close(returnFocus)`；`IsOpen` 双向，`PlacementTarget`、`FocusReturnTarget`、`ClosesOnViewportChange`（滚动、窗口尺寸变化即关闭）。
  - 事件：`ItemClicked`（`MenuEntryClickedEventArgs`：`Entry`、`Item`、`Header`）、`Opened`、`Closed`（退场动画结束后）。
  - 弹出层的逻辑父级在打开时设为锚点（`ISetLogicalParent`），关闭后清空，绑定（DataContext、Command）在打开时生效。锚点上的按压穿透轻关闭层，触发器可以自己切换开合。
  - 也可直接放进布局，作为静态菜单面板使用（高亮、键盘、激活照常，不弹出）。
- **共享菜单项模型**：`MenuEntry`（`TemplatedControl`）字段为 `Header` / `HeaderTemplate`、`Icon`（Lucide `Geometry` 或任意控件）、`InputGesture` / `InputGestureText`（仅显示）、`Command` / `CommandParameter` / `Click`、`IsEnabled`、`IsDestructive`、`ToggleType`（None / CheckBox / Radio）+ `IsChecked`（双向）+ `GroupName`、`HasSeparatorBefore`、`StaysOpenOnClick`；另有 `MenuEntrySeparator`、`MenuEntryLabel`（分组标题）。`ItemsSource` 中的 `MenuEntry` 原样使用，其他数据生成一行并作为 `Header`。
- **变体 `MenuSurfaceVariant`**（继承到各行）：`Dropdown`（36px 行、最小宽 192）、`Context`（34px 行、最小宽 208、最大高 300、按压着色、无图标行已勾选显示对勾否则显示省略号）、`Account`（宽 248、4px 内边距、10px 圆角、32px 行 13px 字、低调高亮）、`Sheet`（窄窗口底部抽屉：全宽、顶部圆角、拖动把手、46px 行 16px 字、遮罩、可下拉关闭）。
- **外观**：表面 `SurfaceRaised` + `Border` + `ShadowFloating`（浮层阴影由 ArcPopup 的运动层承载），圆角 `RadiusPanel` 26，内边距 5px；行圆角 20（RadiusPanel − 6）；图标 16px、`TextSecondary`；危险项文字与高亮用 `Danger` 系色调（高亮 = Surface 混 8% Danger，Token 预先算好）；与 accent 无关的派生色都在各自 Token 中由中性色计算。MenuSurface、MenuEntry、MenuEntrySeparator、MenuEntryLabel 都是公开控件，各有 Token。
- **高亮**：`GlidingIndicator` 在行间移动：指针以 snappy 滑动、键盘立即跳转，指针跨过分隔线或禁用行时 70ms 宽限后释放；行采用漫游焦点（指针移动也移动焦点），菜单项不画焦点环。高亮出现 / 消失沿用 GlidingIndicator 的 fast 160ms 淡入淡出（Arc 为 80ms / 120ms）。
- **键盘**：ArrowDown / ArrowUp 在可用项间首尾循环，Home / End，字母 typeahead（500ms 缓冲），Enter / Space 激活，Escape / Tab 关闭并把焦点还给锚点（键盘关闭时显示焦点环），ArrowLeft / ArrowRight 交给当前行（UserMenu 的内联切换）。
- **进出场**：表面动效在 `MenuSurfacePopup` 中按变体替换 ArcPopup 的单一时长：透明度与变换分开计时（见各组件"动效"）；打开方向与变换原点取自定位器实际使用的方位（含翻转）。行交错由 `MotionValue` 驱动（每行延迟取决于序号与打开时刻，ControlTheme 过渡表达不了）。
- **透明度**：Avalonia 浮层没有背景模糊，统一使用实色 `SurfaceRaised`。
- **动效等级**：Reduced 去掉缩放、位移、交错与高亮滑动，只保留 120ms 线性淡入淡出（Sheet 同时关闭拖动）；Off 立即显示 / 隐藏。

## 4. 组件
### 4.1 DropdownMenu（Dropdown menu）
`slug: dropdown-menu` · 阶段 P5 · Arc 依赖：无 · 建议基类：TemplatedControl + MenuSurface

**Arc 行为摘要**：带标签触发器后面的一组命令（Rename、Duplicate、Delete），也适合行或卡片上比右键更明确的操作入口，以及用分隔线分组、危险项放最后的短菜单。有默认命令用 SplitButton，内容上的右键动作用 ContextMenu，账户菜单用 UserMenu。

**Arc API 要点**：
| Arc 属性 | 类型 / 默认值 | 建议 Avalonia 属性（类型） |
|---|---|---|
| `label`（必填） | string，触发器文字；新标签升起且触发器宽度弹簧 | `Label`（string） |
| `items`（必填） | label、onSelect、disabled、icon、destructive、separatorBefore；label 兼作 key，须唯一 | `Items`（内联 `MenuEntry`，内容属性）/ `ItemsSource` / `ItemTemplate`，`ItemClicked` |
| `icon` | 触发器前导图标 | `Icon`（`Geometry`）；另有 `Trigger`（自定义触发器）、`IsOpen`、`Side`、`Alignment`、`Menu` |

**交互与键盘**：
- 触发器上 Enter / Space / ArrowDown 打开菜单；菜单内 ArrowDown / ArrowUp 循环，Home / End 到首尾，输入字母 typeahead。
- Escape 关闭并把焦点还给触发器；触摸点按直接选择，不留悬停高亮。

**可访问性**：
- 触发器暴露 ExpandCollapse（aria-expanded）与弹出菜单语义；项为 MenuItem 角色并支持 typeahead；禁用项被键盘跳过。
- 图标与高亮对辅助技术隐藏；危险项只靠颜色标记，文案应写明会删除什么。

**动效**：
- 菜单从触发器边缘长出：向触发器方向偏移 5px、缩放 0.97 到 1，变换用 `EaseSpring` / `DurationSpring`（580ms CSS 弹簧曲线，`ArcSpringEasing` 0.58 / 0.15），透明度 fast 160ms；关闭用 130ms，偏移减半并缩到 0.985（P10）。
- 高亮：指针 snappy 滑动、键盘立即跳转，透明度 80ms（P7）；项按序号交错 35ms 以 standard 240ms 淡入（P19）。
- 触发器标签变化：新标签从 soft 模糊上移 0.3em 进入，旧标签带 subtle 模糊上移离开，宽度弹簧适配（P2、P5）。
- Reduced：去掉菜单变换与高亮滑动，只留快速淡入淡出；Off 立即显示。

**Avalonia 实现要点**：
- MenuSurface 在本组件首次落地：S8 定位（间距 6px、end 对齐、碰撞边距 12px、最小宽 12rem 即 192px，最大不超过可用宽度），S9 键盘与 typeahead，S3 退场；菜单内容只在打开时创建。
- 触发器高 `ControlSizeSM` 36、`RadiusControl`、`Surface` + `Border`，只用颜色反馈；标签在最大宽度内省略号截断。项不虚拟化，保持短列表。

**实现说明**：
- 触发器是 `arc:Button`（Secondary、Small、`IsPressScaleEnabled=False`），标签槽为 `SpringSizeDecorator`（Morph）+ `ArcContentSwap`（BlurSwap），悬停、按下、打开只改颜色；V 形图标打开时以 CSS 弹簧转 180°。
- `Trigger` 放任意控件即替换内置触发器：按钮按 Click 切换，其他控件按 Tapped；Arc Button 自动关闭按压缩放。Enter / Space / ArrowDown 打开并高亮首项，ArrowUp 高亮末项。
- 菜单表面最小宽 192、间距 6、end 对齐；Radix 的 12px 碰撞边距没有对应，翻转与滑动交给 Avalonia 定位器。

**AtomUI 参考**：DropdownButton、MenuFlyout（仅供学习行为与边界，不复用其实现）。

**风险与未决**：
- Arc DropdownMenu 的菜单变换是 580ms CSS 弹簧曲线（`EaseSpring` + `DurationSpring`），ContextMenu 用 snappy；原语目录 P10 已按此修正（汇总评审）。
- 共享菜单项模型的命名与字段需一次覆盖 SplitButton、ButtonGroup、SwipeActions、ContextMenu、UserMenu，避免各自定义。

### 4.2 ContextMenu（Context menu）
`slug: context-menu` · 阶段 P5 · Arc 依赖：无 · 建议基类：ContentControl + MenuSurface

**Arc 行为摘要**：包住一块内容，右键时在指针处、点按或键盘触发时在目标下方打开菜单，把文件、卡片、行上的次要动作放在对象旁边，省去可见菜单按钮的杂乱；适合只有一个危险项的短平列表。动作值得可见触发器时用 DropdownMenu，触摸优先列表用 SwipeActions；目标本身需要点击动作时不要用它，因为普通点击也会打开菜单。

**Arc API 要点**：
| Arc 属性 | 类型 / 默认值 | 建议 Avalonia 属性（类型） |
|---|---|---|
| children（必填） | 目标区域 | `Content`（object）/ `ContentTemplate` |
| `items`（必填） | id、label、icon、onSelect、disabled、destructive、checked | `Items`（`<arc:ContextMenu.Items>`）/ `ItemsSource`，`MenuEntry.IsChecked`；`ItemClicked` |
| `label` | string，默认 "Context menu"，目标与菜单共用的名称 | `Label`（string，默认 "Context menu"）；另有 `OpensOnClick`、`IsOpen`、`Open(point)`、附加属性 `arc:ContextMenu.Menu` |

**交互与键盘**：
- 右键在指针处打开；ContextMenu 键 / Shift+F10 在聚焦目标下方打开；左键点按或触摸点按在目标下方打开。
- ArrowDown / ArrowUp 在可用项间循环，Home / End 到首尾，Enter / Space 选择并关闭，Escape 关闭并把焦点还给目标；任何滚动、窗口尺寸变化或外部按压都会关闭菜单，不重新定位。

**可访问性**：
- 目标可聚焦，暴露弹出菜单与 ExpandCollapse；菜单为 Menu 角色，项为 MenuItem；打开时聚焦第一个可用项，关闭时把焦点还给目标。
- 无图标的已勾选项显示对勾，但 Arc 不向辅助技术暴露勾选状态（需要时写进标签）；Avalonia 侧可用 Toggle 状态补上，需在 spec 决定。

**动效**：
- 菜单以 snappy 从指针处缩放 0.96 到 1 长出，透明度 fast 160ms；即使被窗口边缘钳制，变换原点仍落在指针位置；再次打开时旧菜单原地淡出（P10）。
- 关闭：缩到 0.98 并淡出，instant 120ms。
- 共享高亮：指针 snappy 滑动、键盘立即跳转（P7）；行按序号交错 35ms（最多计到第 5 项）以 standard 240ms 进入（P19）；行按压只着色不缩放。
- Reduced：去掉缩放与滑动，改为 instant 淡入淡出；Off 立即。

**Avalonia 实现要点**：
- 订阅 `ContextRequested`（覆盖右键、ContextMenu 键与 Shift+F10），并补充普通点按打开；位置钳制在窗口内 8px，宽度上限窗口宽减 16px、最小 13rem（208px），最大高度 300px、超出滚动；行高 34px，显示前先测量真实尺寸，只定位一次，不跟踪滚动。
- 复用 MenuSurface（S8、S9、S3）；监听祖先 ScrollViewer 的滚动与 TopLevel 尺寸变化以关闭菜单。

**实现说明**：
- 订阅 `ContextRequested`（右键在指针处，ContextMenu 键 / Shift+F10 在目标下方 12/8px 处），并以 Tapped 处理普通点按；菜单打开时左键按下目标只关闭、随后的点击不再打开。
- 附加属性 `arc:ContextMenu.Menu`（值为 `MenuSurface`）给任意控件加右键菜单，不包裹；未显式设置时变体改为 Context 并在滚动 / 尺寸变化时关闭。
- 与 Arc 的差异：贴近窗口边缘时由定位器翻转而不是钳制（变换原点仍落在指针所在的角）；在新位置再次打开时旧菜单立即消失而不是原地淡出。C# 中与 `Avalonia.Controls.ContextMenu` 同名，需要别名。

**AtomUI 参考**：ContextMenu（仅供学习行为与边界，不复用其实现）。

**风险与未决**：
- 与 `Avalonia.Controls.ContextMenu` 及 AtomUI `ContextMenu` 同名，且 Avalonia 习惯通过 `Control.ContextMenu` 属性挂载菜单；Arc 版本是包裹式控件，XAML 用 `arc:` 前缀、C# 需要别名。
- 需要承载目标内容，建议基类已改为 ContentControl（汇总评审）；普通点击也会打开菜单，包裹本身需要点击动作的目标时会冲突，文档与 Gallery 需提示。

### 4.3 UserMenu（User menu）
`slug: user-menu` · 阶段 P5 · Arc 依赖：无 · 建议基类：TemplatedControl + MenuSurface

**Arc 行为摘要**：应用顶栏里唯一的账户入口：头像触发器后是紧凑的身份头部、三到四个账户链接、内联的主题切换（light / dark / system）、可选的在线状态切换与退出登录，退出登录可以异步并在菜单内显示旋转指示；窗口宽度小于 640px 时改为底部抽屉。通用命令列表用 DropdownMenu，独立的明暗切换用 ThemeSwitcher。

**Arc API 要点**：
| Arc 属性 | 类型 / 默认值 | 建议 Avalonia 属性（类型） |
|---|---|---|
| `user`（必填） | name、email、plan、avatarSrc；无照片回落首字母，plan 显示为 accent 小徽标 | 平铺属性 `DisplayName`、`Email`、`Plan`、`AvatarSource`（XAML 友好） |
| `items` / `showName` | label、icon、keys、onSelect，建议三到四项 / 默认 false，640px 以上显示名字与 chevron | `Items`（内联 `MenuEntry`）/ `ItemsSource`，`IsNameVisible`（bool） |
| `theme` / `defaultTheme` / `onThemeChange` / `showTheme` | light / dark / system，默认 system，选择后菜单保持打开；showTheme 默认 true | `ThemePreference`（`UserMenuThemePreference`，双向）+ `ThemePreferenceChanged`、`IsThemeSwitchVisible` |
| `status` / `defaultStatus` / `onStatusChange` | available / busy / away；提供后显示状态点与状态切换 | `Status`（`UserMenuStatus`，双向）+ `IsStatusVisible` + `StatusChanged` |
| `onSignOut` / `signOutKeys` | 返回 Promise 时保持打开并显示旋转指示，结束后关闭 | `SignOutAction`（`Func<Task>`）+ `SignOutRequested` 事件 + `IsSigningOut` / `SignOutGestureText` |
| `open` / `defaultOpen` / `onOpenChange` | boolean，默认 false；初始即打开时不抢焦点 | `IsOpen`（bool，双向）+ `Opened` / `Closed` 事件 |
| `align` | start / center / end，默认 end | `PanelAlignment`（Start / Center / End） |

**交互与键盘**：头像上 Enter / Space / ArrowDown 打开并移到第一行，ArrowUp 打开并移到最后一行；ArrowDown / ArrowUp 在行间循环，Home / End 到首尾，A 到 Z typeahead；ArrowLeft / ArrowRight 在主题或状态切换中选择前后选项；Enter / Space 激活行，或选择主题 / 状态而不关闭菜单；Escape / Tab 关闭并把焦点还给头像。

**可访问性**：
- 触发器暴露弹出菜单与 ExpandCollapse，Name 为 "Account menu, {DisplayName}"，显示状态时附加状态（本地化）；行为 MenuItem，主题与状态切换是带名称的单选组（以 SelectionItem 表达 menuitemradio 与 aria-checked）。
- 键盘位置由高亮表示，指针聚焦不画焦点环；外部按压、遮罩与下滑都会关闭；退出登录进行中报告忙碌；快捷键提示隐藏。

**动效**：
- 桌面面板从头像中心缩放 0.94 淡入，弹簧为 smooth 的变体（visualDuration 0.3、bounce 0，不过冲），透明度 fast；行在 30ms 后按 16ms 交错进入；关闭时缩到 0.97，以 exit 缓动在 120ms 内淡出（P10、P19）。
- 一个高亮随指针与键盘在行间滑动；内联切换的滑块以 `EaseSpring` / `DurationSpring`（580ms）平移到所选项（P7）。
- 窄窗口抽屉从底边以 smooth（visualDuration 0.36）升起，行在 60ms 后按 20ms 交错进入（P11）；抽屉跟随向下拖动（上方弹性 0.04、下方 0.9），超过 80px 或下滑速度超过 500px/s 时关闭（P12）。
- Reduced：开关改为短淡入淡出，高亮跳转，关闭拖动；Off 立即。

**Avalonia 实现要点**：
- 面板宽 17.5rem（280px），上限窗口宽减 24px，碰撞边距 12px，下方空间不足时翻到上方，放置后再计算变换原点；触发器是固定 40px 头像（可复用 Avatars 的 Avatar，P1，非 Arc 依赖），主题行最小高 44px，长名字与邮箱省略号截断。
- 服务：S8（锚定面板与带遮罩的模态抽屉）、S9（漫游与 typeahead）、S10（抽屉拖动，复用 BottomSheet 的拖拽逻辑）、S3、S5（按 TopLevel 宽度在 640px 切换形态）；每次打开重置高亮的布局作用域，避免从旧位置滑入；照片异步解码，失败回落首字母。

**实现说明**：
- 触发器是 `arc:Button`（Ghost、40px 胶囊、`IsPressScaleEnabled=False`）内放 32px `Avatar` 与内部 `UserMenuPresenceDot`（实心 / 横杠 / 空心三种形状，颜色交叉淡化、杠与孔以 CSS 弹簧生长，环色跟随触发器背景）。
- 面板是 `MenuSurface`（Account）：页眉为身份行（34px 头像、名字、计划徽标、邮箱），行顺序为 [分隔线、账户项]、[分隔线、状态、主题]、分隔线、退出登录。内联切换 `UserMenuChoiceRow` 继承 `MenuEntry`：上下键经过、左右键选择、指针悬停不拖动高亮，滑块按 CSS 弹簧平移（自绘）。
- 宽度按 Arc CSS 取 15.5rem（248px，文档所写 17.5rem 与源码不符）；窗口宽度 < 640 时变体切换为 Sheet：遮罩在窗口 overlay / adorner 层，表面贴底全宽、随窗口尺寸变化，拖动下滑超过 80px 或速度超过 500px/s 关闭，否则以 smooth 弹回。
- 异步退出登录：行标签以 BlurSwap 换成 "Signing out"、图标换成 `ArcSpinner`，任务结束后关闭；主题选择只报告，应用主题由使用方负责。

**AtomUI 参考**：DropdownButton、Avatar（仅供学习行为与边界，不复用其实现）。

**风险与未决**：
- `ThemePreference` 只报告选择，应用主题由使用方负责（与 ThemeSwitcher 一致），System 需对接 AtomUI 的跟随系统外观。
- PresenceDot（Arc 单独导出，available / busy / away / offline 各有形状与颜色）主数据未列，可作为模板部件或公开小控件，需与 Avatar 的状态点统一。

## 5. 实现顺序与依赖
- 已按此顺序落地：MenuSurface（含 Sheet 变体）→ DropdownMenu → ContextMenu → UserMenu。
- 三者都在 P5。先落地 DropdownMenu 及其 MenuSurface（共享表面、高亮、行交错、S8 / S9 / S3 接入），同阶段的 SplitButton、ButtonGroup 随后复用；ContextMenu 在 MenuSurface 之上补指针定位与 `ContextRequested` 接入。
- UserMenu 最后落地：它与 Overlays 的 Dialog、Drawer、BottomSheet 同为 P11（模态与边缘呈现）、P12（拖拽物理）与 S10 手势服务的首批使用者，窄窗口抽屉复用 BottomSheet 的拖拽逻辑，宜排在 BottomSheet 之后；P7（Calendar，P1）、P10（Tooltip，P1）与 P19（DatePicker，P2）已在更早阶段落地；DropdownMenu 触发器的 P2 / P5 来自 Button（P0）。
- 跨大类：Gestures 的 SwipeActions（P7）复用 MenuSurface 与 P5 落地的 P12、S10。

## 6. 相关文档
- [组件总清单](../overview.md) · [规格模板](../spec-template.md) · [路线图](../../roadmap/overview.md)
- [动效原语目录](../../architecture/motion/primitives-catalog.md) · [Token 目录](../../architecture/theming/token-catalog.md)
- [浮层与输入](../../architecture/overlays-and-input.md)（锚定与模态浮层、关闭、焦点陷阱与归还）· [AtomUI 集成（短名冲突）](../../architecture/atomui-integration.md)
- 相邻大类：[按钮](../buttons/overview.md) · [手势](../gestures/overview.md) · [浮层](../overlays/overview.md) · [头像与徽标](../avatars/overview.md)
