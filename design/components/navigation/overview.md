# 导航（Navigation）

> Arc 中帮助在同一上下文的平级视图、层级位置与分页集合之间移动的控件：标签页、面包屑与分页。

## 1. 大类定位
- Tabs 在同一对象的平级视图之间切换（Overview、Activity、Settings），面板高度不同时容器弹簧过渡；Breadcrumb 显示页面在层级中的位置，路径随下钻增长；Pagination 在边界明确的长集合中翻页。
- 边界：不绑定面板的紧凑值切换用 Toggles 的 SegmentedControl；按顺序阅读的堆叠段落用 Expand 的 Accordion；浏览整棵层级用 Tables 的 TreeView；浏览幻灯片用 Media 的 Carousel；滚动式信息流用 Activity 的 Timeline。Arc 的应用级路由导航组件属于 Pro，不在范围内。
- 共同特征：当前项用一个共享标记表示（Tabs 的选择药丸、Pagination 的当前页标记），在项之间弹簧滑动；新内容沿选择移动的方向进入；尺寸固定，窄窗口时滚动或换行而不是缩小。

## 2. 组件一览
| 组件 | 类名 | Arc 页面 | 阶段 | Arc 依赖 | 建议基类 | 原语 |
|---|---|---|---|---|---|---|
| 标签页 | Tabs | [tabs](https://uiarc.dev/components/tabs) | P4 | 无 | SelectingItemsControl | P6 P7 P25 P30 |
| 面包屑 | Breadcrumb | [breadcrumb](https://uiarc.dev/components/breadcrumb) | P4 | 无 | ItemsControl | P19 |
| 分页 | Pagination | [pagination](https://uiarc.dev/components/pagination) | P4 | 无 | TemplatedControl | P7 P19 P25 |

## 3. 共享约定
- **当前项标记**：一个元素在项之间移动，不在每项各画一份；首次测量完成前由当前项自己绘制，避免首帧跳动；容器尺寸变化时由 S5 触发重测，不逐帧轮询。Tabs 药丸与 Pagination 标记都用 morph 弹簧。
- **方向**：方向由新旧索引比较得出；Tabs 面板沿方向滑入 8px，Pagination 数字按移动的槽位数平移，Breadcrumb 新项从左侧 8px 滑入。
- **外观**：Tabs 触发器与 Pagination 按钮高 `ControlSizeSM` 36、圆角 `RadiusControl`；当前项标记为 `Surface` + `Border` + `ShadowResting`；非当前项文字为 `TextMuted`（Tabs）或 `TextSecondary`（Pagination）。
- **语义**：Breadcrumb 与 Pagination 是 Navigation 地标（`AutomationProperties.LandmarkType`），当前项暴露"当前页"语义（建议用 `AutomationProperties.ItemStatus` 表达 aria-current，具体在 spec 统一）；Tabs 使用 Tab / TabItem 角色与选中状态。
- **键盘**：Tabs 列表内单一 Tab 停靠、方向键切换；Breadcrumb 与 Pagination 的每项都是 Tab 停靠点，Enter（Pagination 另有 Space）激活。
- **动效等级**：Reduced 时标记直接跳转，内容原地交叉淡化，不做高度与位移动画；Off 立即切换。

## 4. 组件
### 4.1 Tabs（Tabs）
`slug: tabs` · 阶段 P4 · Arc 依赖：无 · 建议基类：SelectingItemsControl

**Arc 行为摘要**：在同一对象的平级视图之间切换（Overview、Activity、Settings）；面板高度不同时容器从旧高度弹簧到新高度；标签很多时横向滚动并显示边缘滚动按钮。不绑定面板的紧凑值切换用 SegmentedControl，按顺序阅读的堆叠段落用 Accordion。

**Arc API 要点**：
| Arc 属性 | 类型 / 默认值 | 建议 Avalonia 属性（类型） |
|---|---|---|
| Tabs `value` / `defaultValue` / `onValueChange` | string，受控或非受控的当前标签 | `SelectedItem` / `SelectedIndex`（双向）+ `SelectionChanged` 事件 |
| TabsTrigger `value`（必填）/ `disabled` | 与 TabsContent 的值配对 | 容器 `TabsItem`（暂名）的 `Header`、`IsEnabled` |
| TabsContent `value`（必填） | 与 TabsTrigger 的值配对 | `TabsItem.Content`（标题与内容同属一项，无需值配对） |
| TabsContent `forceMount` | true 时常驻且跳过进出动画 | `TabsItem.KeepsContentLoaded`（bool） |
| TabsList aria-label | 标签列表名称 | Tabs 的 `AutomationProperties.Name` |

**交互与键盘**：ArrowLeft / ArrowRight 在标签间移动并立即激活，Home / End 到首尾；Tab 从标签列表进入当前面板；聚焦的标签自动滚入可见区，溢出时边缘按钮滚动列表。

**可访问性**：
- 列表为 Tab 角色、每项为 TabItem（SelectionItem 选中状态），面板与对应项关联；列表需要 Name。
- 退场中的旧面板在淡出期间不可聚焦、不可命中、从自动化树隐藏（对应 inert）；溢出滚动按钮名为 "Scroll tabs left" / "Scroll tabs right"（本地化）。

**动效**：
- 选择药丸以 morph 弹簧在标签间滑动，作用域限于本 Tabs 实例（P7）。
- 新面板沿移动方向从 8px 外滑入（x 用 smooth，透明度 standard 240ms），旧面板固定在原位置，反向移 6px 并在 instant 120ms 内淡出（P25）。
- 容器高度从旧面板高度以 smooth 弹簧到新面板高度，下方内容随之平滑移动（P6）；聚焦标签滚入可见区（P30）。
- Reduced：药丸立即切换，面板原地交叉淡化（进入 instant 120ms、退出 100ms），不做高度动画；Off 立即。

**Avalonia 实现要点**：
- 模板部件：`PART_ListViewport`（隐藏滚动条的 ScrollViewer）、`PART_ScrollLeft` / `PART_ScrollRight`、`PART_SelectionPill`、`PART_ContentHost`（新旧两个 Presenter，S3 保留退场面板）。
- 外观：触发器最小宽 5.5rem（88px）、高 36，列表圆角 `RadiusControl`、底色 `SurfaceMuted`，触发器文字 `TextMuted`，药丸 `Surface` + `Border` + `ShadowResting`。
- 服务：S5（列表溢出与面板高度）、S9（方向键）、S7；本组件与 Expand 的 ScrollArea、ExpandableText 同为 P30 `ScrollLinked` 的首批使用者。每个实例两个尺寸观察，同屏避免几十组 Tabs。

**AtomUI 参考**：TabControl、TabStrip（仅供学习行为与边界，不复用其实现）。

**风险与未决**：
- 容器类名（暂名 `TabsItem`）需避开 `Avalonia.Controls.TabItem`；Radix 的 activationMode（manual，方向键只移动焦点不激活）是否支持需在 spec 决定。
- 高度弹簧期间新面板从旧高度起算并裁切溢出，面板内容自身尺寸变化时不应触发弹簧。

### 4.2 Breadcrumb（Breadcrumb）
`slug: breadcrumb` · 阶段 P4 · Arc 依赖：无 · 建议基类：ItemsControl

**Arc 行为摘要**：显示页面在层级中的位置（Workspace、Settings、Billing），最后一项是当前页；也用于文件浏览器这类客户端路径（项调用 onClick 而不跳转），路径随下钻增长时新项滑入。浏览整棵层级用 TreeView，切换平级视图用 Tabs，翻页用 Pagination。

**Arc API 要点**：
| Arc 属性 | 类型 / 默认值 | 建议 Avalonia 属性（类型） |
|---|---|---|
| `items`（必填） | `{ label, href?, onClick? }[]`，从根到当前页 | `ItemsSource`：`BreadcrumbItem`（Label、NavigateUri、Command） |
| 项 `onClick` | 只有 onClick 的项渲染为按钮 | `ItemClick` 路由事件 / 项 `Command` |
| `ariaLabel` | string，默认 "Breadcrumb" | `Label`（string，默认值走本地化） |

**交互与键盘**：Tab 在面包屑链接与按钮间移动，Enter 跟随聚焦项；当前页不是链接，不可聚焦。

**可访问性**：
- 根为 Navigation 地标，内部是有序列表（List / ListItem，带 PositionInSet / SizeOfSet）。
- 最后一项是纯文本并带"当前页"语义，永远不是链接；chevron 分隔符对辅助技术隐藏。

**动效**：
- 首次渲染已有的项保持静止；新项从左侧 8px 外带 subtle 模糊滑入（standard 240ms），兄弟项以 smooth 弹簧让位（P19）。
- 移除的项左移 4px 并带 subtle 模糊淡出（instant 120ms），其余项让位。
- Reduced：增删项没有位移，只做淡入淡出；Off 立即。

**Avalonia 实现要点**：
- ItemsControl + WrapPanel：窄窗口时整体换行，单个面包屑内部不换行；Arc 不截断、不折叠长路径，Avalonia 保持一致。
- 让位与退场用 P19 的 `LayoutAnimator`（FLIP）与 S3 的 `PresenceItemsControl`；分隔符为 16px chevron（`BorderStrong`，描边 1.75）。路径不变时不做任何布局动画。
- 带 NavigateUri 的项经 `TopLevel.Launcher` 打开或交给使用方的导航处理；只有 Command 的项呈现为按钮。

**AtomUI 参考**：Breadcrumb（仅供学习行为与边界，不复用其实现）。

**风险与未决**：Arc 通过 ArcProvider 注入路由的 Link 组件，桌面应用的应用内导航需要约定（`ItemClick` 事件或命令），不引入路由依赖；长路径不折叠，窄窗口中可能换成多行，需由使用方缩短。

### 4.3 Pagination（Pagination）
`slug: pagination` · 阶段 P4 · Arc 依赖：无 · 建议基类：TemplatedControl

**Arc 行为摘要**：上一页 / 下一页按钮夹着一个滑动的五页窗口和一个游走的当前页标记，用于服务端分页结果与长表格，页码保存在状态或 URL 中，变化时重新拉取。浏览幻灯片用 Carousel，滚动信息流用带 maxHeight 的 Timeline。

**Arc API 要点**：
| Arc 属性 | 类型 / 默认值 | 建议 Avalonia 属性（类型） |
|---|---|---|
| `page`（必填） | number，从 1 开始，越界钳制 | `CurrentPage`（int，双向） |
| `pageCount`（必填） | number，0 时渲染空导航 | `PageCount`（int） |
| `onPageChange`（必填） | 请求的页码 | `PageChanged` 事件（与 CurrentPage 双向绑定二选一） |
| `label` | string，默认 "Pagination" | `Label`（string，默认值走本地化） |

**交互与键盘**：Tab 在上一页、页码与下一页按钮间移动；Enter / Space 跳到该页。

**可访问性**：
- 根为 Navigation 地标并以 Label 命名；页码按钮名为 "Page 3"（本地化），当前页带"当前页"语义。
- 上一页 / 下一页在两端禁用并带名称。

**动效**：
- 单个当前页标记以 morph 弹簧（x、y 两个分量）移到选中按钮；首次测量前由当前按钮自己绘制（P7）。
- 窗口移动时数字像传送带一样按移动的槽数平移，每槽为 36px 按钮加 4px 间距；x 用 smooth，进入透明度 standard 240ms，退出透明度 instant（P25、P19）。
- Reduced：标记直接跳转，数字原地替换；Off 立即。

**Avalonia 实现要点**：
- 窗口起点 = max(1, min(PageCount − 4, CurrentPage − 2))；无论 PageCount 多大都只渲染 5 个页码按钮，宽度恒定，容器过窄时换行，标记在尺寸变化时重测（S5）。
- 按钮 36×36（`ControlSizeSM`）、字号 12、等宽数字、圆角 `RadiusControl`；标记 `Surface` + `Border` + `ShadowResting`；页码格式化走 S11。

**AtomUI 参考**：Pagination（仅供学习行为与边界，不复用其实现）。

**风险与未决**：Arc 完全受控，没有页码跳转与每页条数（AtomUI 有），需要时由使用方组合，不扩展 API；四位以上页码在 36px 按钮内的显示 Arc 未说明，需在 spec 中验证。

## 5. 实现顺序与依赖
- 三者都在 P4，彼此无依赖，可并行。Tabs 与 Expand 的 ScrollArea、ExpandableText 同为 P30（滚动跟随）的首批使用者，需协调 `ScrollLinked` 的通用 API；P6、P7、P25（Calendar，P1）与 P19（DatePicker，P2）已在更早阶段落地。
- Breadcrumb 与 Pagination 只依赖已有原语与 S3 / S5 / S11，适合作为 P4 的早期交付；Tabs 的面板高度弹簧直接复用 P6 的 SpringSizeDecorator，与 Expand 大类同阶段的 Accordion、ExpandableCard 保持同一套高度动效参数。
- 跨大类：Tables 的 SortableDataTable（P9）可组合 Pagination；Tabs 的药丸与 Toggles 的 SegmentedControl（P3）视觉相近，Token 命名应保持一致。

## 6. 实现记录

### 6.1 Tabs
- `Tabs : SelectingItemsControl`（默认 AlwaysSelected）+ `TabItem : HeaderedContentControl`；内联 `<arc:TabItem Header=…>内容</arc:TabItem>` 与 `ItemsSource`（`ItemTemplate` 画触发器、`ContentTemplate` 画面板）两种写法；`SelectedIndex` / `SelectedItem` 双向，另有只读 `SelectedContent`。
- 选择药丸是列表内的 Core `GlidingIndicator`（morph 弹簧），与触发器同处 `TabsListPanel` 坐标系，随列表一起滚动；排列后只在目标矩形变化时跳位，避免打断进行中的滑动。
- 面板由内部 `TabsPanelHost` 的两个池化 ContentPresenter 轮换：新面板 x 从 ±8px 走 smooth 弹簧、透明度 standard+enter；旧面板钉在原位，120ms 内反向漂移 6px 淡出并设为不可交互；容器高度从旧面板高度以 smooth 弹簧到新高度（变高时裁切）。Reduced 原地交叉淡化（120ms / 100ms），无高度动画；Off 立即切换。
- 键盘：Left/Right 循环跳过禁用项、Home/End；`ActivationMode` = Automatic（Arc 默认，方向键即选中）或 Manual（Enter/Space 选中）；只有当前项是 Tab 停靠点，面板宿主可聚焦（偏移 3px 的焦点环）。溢出时出现 33px 边缘按钮（翻 75% 视口）与 35px 边缘渐隐，选中或聚焦的标签以 smooth 弹簧滚入视口并为按钮留出 34px（P30）。

### 6.2 Breadcrumb
- `Breadcrumb : ItemsControl` + `BreadcrumbItem : ContentControl`（`NavigateUri` 经 `TopLevel.Launcher` 打开、`Command`、`IsClickable=false` 渲染为纯文本）；选择时发 `ItemClick`（Item、Index）。最后一项为当前页（AccentStrong + Medium，不可聚焦，ItemStatus = "current page"）；每项用一个零高度的 Medium 字重副本占住宽度，成为当前页时路径不位移。
- 运动：首屏已有项静止；之后新增项从 −8px 带 2px 模糊滑入（standard+enter），兄弟项由 `BreadcrumbPanel` 记录旧位置后以 smooth 弹簧让位（FLIP）；移除项以非交互的克隆在覆盖层左移 4px、模糊淡出（120ms）。按"标签+索引"判断同一项，整体替换 ItemsSource 时相同前缀不重播。Reduced / Off 不做位移。
- 新增（Arc 没有）：`MaxVisibleItems` > 1 时保留根与末尾 N−1 项，中间折叠为省略号按钮，点开 Core `ArcPopup` 列表（上下键、Home/End、Enter、Escape 回到按钮）；默认 0 不折叠，与 Arc 一致。另加 Left/Right 在面包屑间移动焦点。

### 6.3 Pagination
- `Pagination : TemplatedControl`：`CurrentPage`（双向、1 起、显示时钳制）、`PageCount`、`PageChanged`（Old/New）、`GoToPage` / `NextPage` / `PreviousPage`、`Label` / `PreviousLabel` / `NextLabel` / `PageLabelFormat`。五个页码按钮是池化的槽位，36×36、12px 等宽数字。
- 当前页标记为 Core `GlidingIndicator`（morph），目标取按钮的布局位置；窗口移动 n 格时各数字从原位置（n×40px，最多一个窗口）以 smooth 弹簧回到新槽位，新数字透明度 standard+enter，离开的数字在 `PaginationGhostLayer` 中绘制为幽灵并 120ms 淡出；焦点跟随原来的数字。仅箭头按钮有按压缩放（0.96，CSS 弹簧）。Reduced 标记跳转、数字原地替换。
- 新增：`ShowBoundaryPages`（默认关）在窗口外保留首页 / 末页与省略号；Left/Right 在按钮间移动焦点。PageCount = 0 时只保留空的 Navigation 地标。

## 7. 相关文档
- [组件总清单](../overview.md) · [规格模板](../spec-template.md) · [路线图](../../roadmap/overview.md)
- [动效原语目录](../../architecture/motion/primitives-catalog.md) · [Token 目录](../../architecture/theming/token-catalog.md) · [可访问性](../../architecture/accessibility.md)
- 相邻大类：[开关与选项](../toggles/overview.md) · [展开与滚动](../expand/overview.md) · [表格与数据](../tables/overview.md) · [媒体](../media/overview.md)
