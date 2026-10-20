# 头像与徽标（Avatars）

> Arc Data 分组中表示身份与状态的紧凑元素：单人头像、头像堆叠与状态徽标。

## 1. 大类定位
- Avatar 在名字、评论、记录旁标识一个人，照片缺失时显示首字母，可带在线 / 离线点；AvatarGroup 在小空间里显示团队或协作者，超出部分折叠为 +N；Badge 是标题旁或表格单元里的短状态、分类或元数据标签（Live、Draft、Failed）。
- 边界：头像作为账户操作触发器时用 Menus 的 UserMenu；需要看到每个人的名字与角色时用列表或 Tables 的 SortableDataTable；需要整句消息时用 Messages 的 Alert / Toast；用户可切换的值用 Selects 的 ChipGroup；带上下文的大数字用 Cards 的 MetricCard。
- 共同特征：非交互、不可聚焦（AvatarGroup 只有指针悬停展开）；内容变化（照片加载、成员进出、计数、标签）在原位形变或弹簧过渡，前提是控件保持挂载；颜色从不是状态的唯一载体。

## 2. 组件一览
| 组件 | 类名 | Arc 页面 | 阶段 | Arc 依赖 | 建议基类 | 原语 |
|---|---|---|---|---|---|---|
| 头像 | Avatar | [avatar](https://uiarc.dev/components/avatar) | P1 | 无 | TemplatedControl | P2 P26 |
| 头像组 | AvatarGroup | [avatar-group](https://uiarc.dev/components/avatar-group) | P2 | Avatar | ItemsControl | P5 P14 P19 P27 P28 |
| 徽标 | Badge | [badge](https://uiarc.dev/components/badge) | P3 | 无 | ContentControl | P2 P5 |

## 3. 共享约定
- **尺寸**：Avatar sm / md / lg / xl 直径 28 / 36 / 48 / 88px（AvatarGroup 只用 sm / md / lg），首字母字号 10 / 12 / 14 / 22；Badge md 最小高 26、sm 22，圆角 `RadiusPill`。尺寸由 `Size` 决定，不随窗口变化。
- **外观**：头像圆形、`SurfaceMuted` 底、1px `Border`；堆叠中的头像与 +N 芯片外加 2px `Surface` 描边与 1px `Border` 环；在线点 `Success`、离线点 `TextMuted`，带 2px `Surface` 描边。
- **语义**：Avatar 与 +N 芯片是 Image 角色，Name 由名字加状态组成；装饰性的照片、首字母、状态点与图标都对辅助技术隐藏（`AccessibilityView=Raw`）；Badge 是行内文本，自身不播报变化。
- **原位变化**：只在内容变化时弹簧（标签、计数、成员）；字体加载等被动重排直接跳变；照片已缓存时立即显示，只有仍在加载的照片做模糊淡入。
- **命名**：Arc 的 `name` 不能映射为 `Name`（已被 `StyledElement.Name` 占用），本大类与 UserMenu 统一用 `DisplayName`。
- **图片**：照片经 AtomUI `ImageLoader` / `AsyncImage`（白名单公开基础设施）异步解码，按显示直径乘以 DPI 解码，失败回落首字母且不反复重试同一地址。
- **动效等级**：Reduced 去掉模糊淡入、弹出与扇形展开，计数与标签改为快速淡入淡出、宽度直接跳变；Off 立即。

## 4. 组件
### 4.1 Avatar（Avatar）
`slug: avatar` · 阶段 P1 · Arc 依赖：无 · 建议基类：TemplatedControl

**Arc 行为摘要**：人和账户的紧凑身份标记：圆形照片，缺图或加载失败时回落为首字母，可显示在线 / 离线点；用于名字、评论、记录旁的单个人，以及页头或列表里的在线状态。多个人带溢出计数用 AvatarGroup，头像作为账户操作触发器时用 UserMenu。

**Arc API 要点**：
| Arc 属性 | 类型 / 默认值 | 建议 Avalonia 属性（类型） |
|---|---|---|
| `name`（必填） | string，用于可访问名称与前两个首字母 | `DisplayName`（string） |
| `src` | string，加载失败回落首字母 | `Source`（图片地址，经 ImageLoader / AsyncImage 加载） |
| `size` | sm / md / lg / xl，默认 md | `Size`（Small / Medium / Large / ExtraLarge） |
| `status` | online / offline，追加到可访问名称 | `Status`（`AvatarStatus?`：Online / Offline） |

**交互与键盘**：无，非交互元素，不可聚焦。

**可访问性**：
- 根为 Image 角色，Name 为名字加状态，如 "Maya Chen, online"（本地化）。
- 照片与首字母是装饰，对辅助技术隐藏，名字只读一次；状态点隐藏，状态由 Name 表达。

**动效**：
- 仍在加载的照片从 soft 4px 模糊以 standard 240ms 淡入，已缓存的照片立即显示（P2）。
- 状态点以 snappy 从 0.6 缩放弹出，退出 fast 160ms（P26）。
- Reduced：状态点立即切换，照片不做淡入；Off 立即。

**Avalonia 实现要点**：
- 模板：圆形裁切的 `PART_Image`（AtomUI AsyncImage）、`PART_Initials`、`PART_StatusDot`（S3 让状态点退场）；首字母取按空白分隔的前两个词的首字符并大写。
- 状态点直径 sm 8、md 10、lg 12、xl 14px，贴右下角（xl 内缩 4px）；照片加载失败后记住该地址，切换 `Source` 时再重试。
- 本组件是 P26（弹出）的首个使用者，Phase 1 需连带交付；Arc 通过 ArcProvider 换用 next/image 的机制在 Avalonia 中不需要。

**AtomUI 参考**：Avatar、AsyncImage（Avatar 仅供学习行为与边界，不复用其实现；AsyncImage / ImageLoader 是白名单内的公开基础设施，直接使用）。

**风险与未决**：中文等无空格姓名按 Arc 规则只会取一个字符（"张三" 得到 "张"），是否按书写系统改为取前两个字符需在 spec 决定；状态点的形状是否与 UserMenu 的 PresenceDot（四种状态各有形状）统一需一并确定。

### 4.2 AvatarGroup（Avatar group）
`slug: avatar-group` · 阶段 P2 · Arc 依赖：Avatar · 建议基类：ItemsControl

**Arc 行为摘要**：在小空间里显示团队或贡献者：重叠的头像堆叠加 +N 溢出芯片；用于页头、卡片或表格单元里表示谁拥有或在编辑某物，成员在页面打开期间加入或离开时堆叠平滑滑动，长成员列表折叠为 +N。单个人用 Avatar，需要看到每个名字与角色时用列表或 SortableDataTable。

**Arc API 要点**：
| Arc 属性 | 类型 / 默认值 | 建议 Avalonia 属性（类型） |
|---|---|---|
| `members`（必填） | `{ name, src?, status? }[]`，按显示顺序；name 兼作 key，须唯一 | `ItemsSource`（成员：DisplayName、Source、Status） |
| `max` | number，默认 4，超出部分折叠进溢出计数 | `MaxVisibleCount`（int） |
| `size` | sm / md / lg，默认 md | `Size`（Small / Medium / Large，传给每个 Avatar 与溢出芯片） |
| `label` | string，默认 "Team members"，也用于溢出标签 | `Label`（string，默认值走本地化） |

**交互与键盘**：无键盘交互；鼠标 / 笔悬停时堆叠展开并显示被指成员的名字，触摸不展开。

**可访问性**：
- 根为 Group 角色并以 Label 命名；每个头像保留自己的名字与状态 Name。
- 溢出芯片为 Image 角色，Name 形如 "2 more editors"（由 Label 生成，需要本地化模板与复数规则）。

**动效**：
- 成员加入或离开时，其槽位宽度在 0 与自然宽度之间以 morph 弹簧变化，同时做透明度与 0.9 缩放，堆叠滑动而不跳变（P5、P19）。
- 溢出计数增长时向上滚动、减少时向下滚动（P14）。
- 悬停展开（standard 240ms）：每个头像按到中心的序号距离乘以 4px 散开（P28）；被指者上移 2px、环线变 `BorderStrong` 并在 60ms 后显示名字提示，左右邻居各让开 3px（P27）。
- Reduced：去掉展开，计数用普通淡入淡出；Off 立即。

**Avalonia 实现要点**：
- 自定义面板：头像重叠 −6px，z 顺序与展开偏移在面板中计算（Arc 用 CSS 兄弟选择器实现）；每个成员由一个包装层承载全部悬停位移，堆叠整体尺寸不变。
- 只实现 `MaxVisibleCount` 个 Avatar（P1）加一个溢出芯片（`SurfaceMuted`、`TextSecondary`、12px、等宽数字），长成员列表也很便宜；退场用 S3，计数格式化用 S11。
- 名字提示是模板内的轻量元素（`Foreground` 底、`Background` 字、药丸形），不走 Tooltip 服务；传入完整成员列表，由控件截断，否则溢出计数会丢失。

**AtomUI 参考**：AvatarGroup（仅供学习行为与边界，不复用其实现）。

**风险与未决**：
- "N more X" 在 en-US、zh-CN、zh-TW 间语序与复数不同，必须用本地化模板而不是字符串拼接。
- Arc 以名字作 key，重名成员会冲突；Avalonia 侧建议按数据项引用区分进出动画。

**实现记录**：
- `AvatarGroup : TemplatedControl`：`ItemsSource` 或内联 `Items`（`AvatarGroupMember`：DisplayName、Source、Status，也接受 `Avatar` 或纯字符串）、`MaxVisibleCount`（4）、`Size`（复用 `AvatarSize`，XLarge 按 Large）、`Label`（"Team members"）、只读 `OverflowCount`。只为可见成员与 +N 芯片创建控件；成员按数据项引用（重复项按出现序号）区分进出。
- 每人一个内部 `AvatarGroupSlot`（承载 Avatar 或芯片）：自绘 1px Border 环（悬停交叉到 BorderStrong），头像本身设 2px Surface 边；槽位宽度 = (直径 − 6) × presence，由 `AvatarGroupPanel` 排布，进出以 morph 弹簧同时驱动宽度、透明度与 0.9 缩放（P19，加入的槽位在进入视觉树后才开始动画）。
- 悬停（仅非触摸指针）：所有人按到中心的序号 × 4px 散开（P28），被指者上移 2px、ZIndex 1、环变深并在 60ms 后显示名字药丸（Foreground 底、Background 字，3px 落位），左右邻居各让开 3px（P27）；位移为 standard 240ms 的 RenderTransform，不触发布局。Reduced 只保留环与名字。
- +N 芯片用 `ArcContentSwap`（Roll，按增减方向滚动），Image 角色，Name 为 "2 more team members"（Label 小写；本地化模板仍待定）。

### 4.3 Badge（Badge）
`slug: badge` · 阶段 P3 · Arc 依赖：无 · 建议基类：ContentControl

**Arc 行为摘要**：表示状态、分类或元数据的小药丸：标题旁或表格单元里的短状态（Live、Draft、Failed），原位变化时应形变而不是跳变的计数或状态，以及给一行打上一个色调加可选图标的标记。需要整句消息用 Alert 或 Toast，用户可切换的值用 ChipGroup，带上下文的大数字用 MetricCard。

**Arc API 要点**：
| Arc 属性 | 类型 / 默认值 | 建议 Avalonia 属性（类型） |
|---|---|---|
| `tone` | neutral / success / info / warning / danger，默认 neutral | `Tone`（`BadgeTone`） |
| `size` | sm / md，默认 md | `Size`（Small / Medium） |
| `icon` | 前导图标，换成不同图标组件时交叉淡化 | `Icon`（object） |
| children | 标签；字符串或数字获得滚动替换，其他节点原样呈现 | `Content`（string 或数值时启用形变） |

**交互与键盘**：无，非交互元素。

**可访问性**：
- 行内文本语义，随周围文字一起朗读；图标隐藏，标签必须自己说明状态，不能依赖色调。
- 淡出中的旧标签对辅助技术隐藏，只读当前文字；Badge 不播报变化，需要朗读时放进 `LiveSetting` 为 Polite 的容器。

**动效**：
- 新标签从 soft 模糊上移 0.3em 进入，旧标签带 subtle 模糊上移离开，药丸宽度以 morph 弹簧适配（P2、P5）。
- 新图标以 0.6 缩放加 subtle 模糊、snappy 进入；字体替换等被动重排立即改变尺寸，只有内容变化才弹簧。
- Reduced：标签快速淡入淡出，宽度直接跳变；Off 立即。

**Avalonia 实现要点**：
- 尺寸：md 最小高 26、字号 12；sm 最小高 22、字号 11、水平内边距 8；标签不换行，窄单元里保持一两个词。
- 色调：neutral 为 `TextSecondary` 字、`SurfaceMuted` 底、`Border` 边；info 使用 accent（Token 目录约定），`AccentStrong` 字、`AccentSubtle` 底，边框混入 24% accent，属依赖 accent 的派生色，放进 ArcDesignToken；success / warning / danger 的底色与边框是与 accent 无关的 10% 到 27% color-mix，在 `BadgeToken` 中用中性色纯函数计算（见 [控件 Token](../../architecture/theming/control-tokens.md#1-职责)）。
- 保持挂载并修改 `Content` 才能触发形变（重新创建会丢失）；每个 Badge 一个尺寸观察（S5），上千个的表格应关闭宽度弹簧；新旧标签用 S3。

**AtomUI 参考**：CountBadge、DotBadge（仅供学习行为与边界，不复用其实现）。

**风险与未决**：Arc Badge 是独立的状态药丸，不是 AtomUI CountBadge / DotBadge 那样叠在其他元素上的角标，同名不同用途，文档与 Demo 需说明；Badge 的 10% 底色与 Token 目录中 12% / 16% 的 `SuccessSubtle` 等不同，是否直接复用需在 Alert（P3）与 Badge 规格中一并确认。

**实现记录**：
- `Badge : ContentControl`：`Tone`（Neutral / Success / Info / Warning / Danger）、`Size`（`BadgeSize`：Small 22px / Medium 26px）、`Icon`（object）。Arc 的 Badge 没有圆点或角标计数变体，因此未提供；字符串与数字内容（伪类 `:text`）以 `ArcContentSwap` BlurSwap 原位替换，药丸宽度由 `SpringSizeDecorator`（morph）跟随，新图标以 Pop 弹出。
- 成功 / 警告 / 危险的底色与边框、所有色调的悬停色是中性色纯函数（BadgeToken，10%–32% color-mix）；Info 的边框（24% → 悬停 32% accent）与悬停底（10% accent 叠在 Surface 上）依赖 accent，模板中用 Accent 色的覆盖层加不透明度近似 color-mix，不新增 ArcDesign Token。
- 无障碍：字符串 / 数字内容同步为 AutomationProperties.Name，图标为 Raw；不可聚焦。

## 5. 实现顺序与依赖
- P1：Avatar，首个落地 P26（弹出）；它也是 UserMenu（P5）触发器与 Card（P5）署名的可选组成部分。
- P2：AvatarGroup，硬依赖 Avatar；首个落地 P27（强调）与 P28（堆叠纵深），并与同阶段的 DatePicker 同为 P19（列表进出与重排）的首批使用者；P5（Button，P0）与 P14（Calendar，P1）已落地。
- P3：Badge，只用 P2 与 P5，无跨大类依赖；Tables 的 SortableDataTable 等会大量使用，需关注每实例尺寸观察的开销。

## 6. 相关文档
- [组件总清单](../overview.md) · [规格模板](../spec-template.md) · [路线图](../../roadmap/overview.md)
- [动效原语目录](../../architecture/motion/primitives-catalog.md) · [Token 目录](../../architecture/theming/token-catalog.md)
- 相邻大类：[菜单](../menus/overview.md) · [卡片](../cards/overview.md) · [消息](../messages/overview.md) · [表格与数据](../tables/overview.md)
