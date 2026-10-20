# 选取器（pickers）

> 选取器在 Arc 中负责日期、日期区间、时刻与颜色这类结构化值的选择：候选值由规则生成的网格、列表或色域给出，值的变化方向通过滚动与滑动动效表达。

## 1. 大类定位
- 解决结构化值的输入：常驻月历（Calendar）、表单日期字段（DatePicker）、带预设的日期区间（DateRangePicker）、固定步长的时刻（TimePicker）、带透明度与对比度读数的颜色（ColorPicker）。输出值可直接序列化，避免手输格式错误。
- 与 Selects 的边界：选取器的候选由规则生成（6 周网格、按 minuteStep 生成的时刻、连续色域），Select 的候选由调用方列出；只有少数命名时刻时 Arc 建议用 Select。与 TextFields 的边界：字段外壳共用 [字段通用结构](../../architecture/forms-and-fields.md)，但值不靠键入（ColorPicker 的格式文本框例外）。Emoji picker 属 Pro，不在本库范围。
- 共同特征：字段触发器 + 锚定弹层（DatePicker、TimePicker），或触发器表面直接长成面板（DateRangePicker 的尺寸形变、ColorPicker 的裁剪揭示）；值变化按时间方向滚动（更晚的值从下方升起）；网格中的选中高亮在格子之间滑行，而不是跳变。

## 2. 组件一览
| 组件 | 类名 | Arc 页面 | 阶段 | Arc 依赖 | 建议基类 | 原语 |
|---|---|---|---|---|---|---|
| 日历 | `Calendar` | [calendar](https://uiarc.dev/components/calendar) | P1 | 无 | TemplatedControl | P3 P7 P25 |
| 日期选择 | `DatePicker` | [date-picker](https://uiarc.dev/components/date-picker) | P2 | `Calendar` | TemplatedControl | P3 P10 P19 |
| 日期区间选择 | `DateRangePicker` | [date-range-picker](https://uiarc.dev/components/date-range-picker) | P6 | 无 | TemplatedControl | P3 P7 P8 P9 P25 |
| 时间选择 | `TimePicker` | [time-picker](https://uiarc.dev/components/time-picker) | P6 | 无 | TemplatedControl | P3 P10 |
| 颜色选择 | `ColorPicker` | [color-picker](https://uiarc.dev/components/color-picker) | P6 | `TextMorph` | TemplatedControl | P4 P9 P12 P13 P14 P19 P26 P29 |

## 3. 共享约定
- **字段外壳**（DatePicker、TimePicker）：标签（text-sm、字重 500，Arc 中必填）在上，触发器居中，可选 description（text-xs、TextMuted）在下，间距 7–8px；结构与校验态遵循 [字段通用结构](../../architecture/forms-and-fields.md)。
- **触发器**：最小高度 ControlSizeMD（44）、RadiusControl、1px Border、Surface 底；左侧 16px Lucide 图标（TextMuted），右侧 ChevronDown 展开时以 Arc 的 CSS 弹簧（duration-spring 580ms，ControlTheme 中用 ArcSpringEasing）旋转 180°。悬停、按下与展开态统一为 BorderStrong + SurfaceMuted；禁用 opacity 0.5。触发器是弹层锚点，**按下只变色、不缩放**（Arc 源码明确：缩放会让弹层错位），不套用 P1。
- **弹层表面**：RadiusPanel（26）、SurfaceRaised、1px Border、ShadowFloating，距触发器 6–8px。进入时 opacity 0、y −6~−8、scale 0.95~0.97 → 1，位移与缩放走 snappy，透明度 fast + enter；退出约 0.12–0.14s + standard（P10）。任意外部 pointerdown 关闭；Escape 关闭并把焦点还给触发器。锚定、外部点击与焦点归还的通用机制见 [浮层与输入](../../architecture/overlays-and-input.md)。
- **值滚动（P3）**：新值晚于旧值时从下方 +0.35em 升起（blur soft 4px，standard + enter），早于旧值时从上方落下；旧值以 −0.3em、blur subtle 2px、fast（或 0.14s）+ standard 退出。可读文本放在读屏专用元素里，滚动层 AccessibilityView=Raw。
- **日期网格**：每月固定 6 周 42 格，格子为正方形圆盘，行列间距 4px；星期名取本地化短名的前两个字符；今日为数字下方 4px 处的 4px 圆点；选中为 Accent 底 + AccentForeground 字，悬停 SurfaceMuted，选中悬停 AccentStrong；月外日期 TextMuted，禁用日数字 opacity 0.42；按下时内圈缩到 0.94（100ms），松开弹簧回弹。
- **网格键盘**：方向键按日 / 周移动并跨月，跳过禁用日；Home / End 到周首 / 周尾；PageUp / PageDown 按月，加 Shift 按年（日期按目标月天数夹取：1 月 31 日加一个月为 2 月最后一天）；roving tab stop，只有一个日单元格在 Tab 顺序中。
- **本地化**：Arc 的 `locale`（BCP 47 字符串）映射为 `Culture`（CultureInfo?），未设置时跟随应用语言；按钮与预设文案（Today、Clear、Apply、Cancel、Last 7 days 等）走 xlf，日期格式化走 S11。
- **动效等级**：Reduced 去掉滑动、滚动与形变，只保留终态与短淡入淡出；Off 立即到终态。

## 4. 组件

### 4.1 Calendar（Calendar）
`slug: calendar` · 阶段 P1 · Arc 依赖：无 · 建议基类：TemplatedControl

**Arc 行为摘要**：单选日期的紧凑月视图，月份在一条"月份带"上沿行进方向滑动，选中圆盘在日期之间滑行。适合月历需要常驻的场景（如预订页），可用 `minDate`、`maxDate`、`disabledDates` 屏蔽日期。表单中弹出的日期字段用 DatePicker，时刻用 TimePicker。

**Arc API 要点**：
| Arc 属性 | 类型 / 默认值 | 建议 Avalonia 属性（类型） |
|---|---|---|
| `value` / `onChange` | `Date`，仅受控 | `SelectedDate`（DateTime?，只用日期部分，双向）+ `SelectedDateChanged` 事件 |
| `month` / `onMonthChange` | `Date`，可受控或内部维护 | `DisplayMonth`（DateTime，月首，双向）+ `DisplayMonthChanged` 事件 |
| `minDate` / `maxDate` | `Date`，无 | `MinDate` / `MaxDate`（DateTime?） |
| `disabledDates` | `(date: Date) => boolean`，无 | `DisabledDatePredicate`（`Func<DateTime, bool>?`，返回 true 屏蔽） |
| `locale` | `string`，`"en-US"` | `Culture`（CultureInfo?） |
| `showToday` | `boolean`，`false` | `IsTodayButtonVisible`（bool，false） |

**交互与键盘**：
- 网格键盘见"共享约定"；跨月移动焦点时，焦点落到新日与月份开始滑动发生在同一帧。
- Enter / Space 选中焦点日；点击禁用日无效。
- Today 按钮回到当月，今天可选时同时选中；已在当月且今天已选中（或不可选）时呈禁用态。
- 长跳转（Today、Shift + PageUp / PageDown）从目标月相邻的一个月处起滑，不滚过中间月份；连续点击重定向同一个弹簧，不排队。

**可访问性**：
- Avalonia 12.1.4 没有 Grid / GridItem 自动化模式：根 peer 用 Calendar 类型，日单元格为 Button 类型 peer，Name 用长日期格式（如 "Tuesday, October 20, 2026"），选中态经 ISelectionItemProvider 暴露，今天用 ItemStatus 标注。
- 月份变化写入 LiveSetting=Polite 的播报元素；上一月 / 下一月按钮有 Name，在 MinDate / MaxDate 所在月禁用。
- 键盘焦点环用 S7；滑出中的月份不可聚焦、不进可访问性树。

**动效**：
- 月份带：位置以"月"为单位，弹簧 visualDuration 0.36s、bounce 0（Arc 自定义值，接近 smooth），相邻月透明度随距离降到 0.4（P25）。
- 标题：旧月名朝行进方向移出 12px 淡出（0.14s + standard），新月名从另一侧淡入（standard + enter）（P3）。
- 选中圆盘按行列坐标在格间直线滑行，走 morph；首次出现从 scale 0.6 以 snappy 长出、透明度 fast + enter，离开本月时 0.14s 淡出缩小（P7）。日期数字的颜色在圆盘经过时才翻转（按距离计算 20–260ms 延迟）。
- Reduced：月份与圆盘直接跳到终态，标题原地替换；Off 同。

**Avalonia 实现要点**：
- 模板部件：`PART_Header`、`PART_PreviousButton` / `PART_NextButton` / `PART_TodayButton`（32px 圆形，按下 0.96）、`PART_MonthViewport`（ClipToBounds 的月份带）；月份为内部 `CalendarMonthPane`，日为内部 `CalendarDayButton`；语义部件 `arc.header`、`arc.day`、`arc.selection`。
- 月份带只渲染弹簧当前跨度两端的月份加目标月（最多 3 个 pane、约 126 个日按钮），pane 复用；月份带位置与圆盘坐标用 S2 动效值驱动 RenderTransform，不触发重新布局。根宽 min(100%, 328px)；头部宽 < 260px 时标题独占一行、导航移到下方。
- "今天"跨午夜自动更新：经 .NET TimeProvider 读取本地日期，定时到下一个本地午夜刷新，便于测试。
- 依赖服务：S1、S2、S7、S11；日期运算（addDays、addMonths、sameDay、startOfDay 与按月夹取）放入 Core 内部工具类，DateRangePicker 复用。

**AtomUI 参考**：Calendar（仅供学习行为与边界，不复用其实现）。

**风险与未决**：
- Arc 文档与当前源码有出入：文档称在 4 / 5 / 6 行之间形变高度、标题字母与年份数字滚动；源码固定 6 周（高度不变），标题整体横移交叉淡化。若以源码为准，主数据中 Calendar 的 P6、P14 可能多余，P6 的首个使用组件会随之后移，需主笔确认。
- 值类型取 DateTime? 还是 DateOnly?：前者与 Avalonia / AtomUI 生态一致，后者语义更准；需在详细规格中定案，DatePicker、DateRangePicker、TimePicker 同步。
- Arc 的 Calendar 周首固定为周日（DateRangePicker 才有 `weekStartsOn`）；zh-CN 下是否按 Culture 的 FirstDayOfWeek 调整属于偏离 Arc 的决定。
- 复核结论（汇总评审）：移除 P6、P14，已同步到[组件总清单](../overview.md)。

### 4.2 DatePicker（Date picker）
`slug: date-picker` · 阶段 P2 · Arc 依赖：Calendar · 建议基类：TemplatedControl

**Arc 行为摘要**：字段按钮，点击后在弹层中打开 Calendar，并按日期部件滚动显示所选值。用于表单中的开始日期、截止日期等，可设上下限与屏蔽日，按 locale 格式显示。日历需要常驻时用 Calendar；选时刻用 TimePicker，二者并排组成完整时间戳。

**Arc API 要点**：
| Arc 属性 | 类型 / 默认值 | 建议 Avalonia 属性（类型） |
|---|---|---|
| `label`（必填） | `string` | `Label`（string） |
| `value` / `onChange` | `Date`，仅受控；Clear 时回调 undefined | `SelectedDate`（DateTime?，双向）+ `SelectedDateChanged` 事件 |
| `description` | `string` | `Description`（string?） |
| `placeholder` | `"Select a date"` | `Placeholder`（string） |
| `minDate` / `maxDate` / `disabledDates` | 同 Calendar | `MinDate` / `MaxDate` / `DisabledDatePredicate` |
| `locale` | `"en-US"` | `Culture`（CultureInfo?） |
| `format` | `{ month: "short", day: "numeric", year: "numeric" }` | `DisplayFormat`（string?，.NET 日期格式串；空则用 Culture 的缩写月份模式） |
| `showToday` | `boolean` | `IsTodayButtonVisible`（bool） |

**交互与键盘**：
- 触发器上 Enter / Space / ArrowDown 打开日历，焦点落到持有 tab stop 的日（选中日，否则今天）；指针打开时不显示焦点环。
- 弹层内沿用 Calendar 键盘；Escape 关闭，焦点原本在弹层内时还给触发器；Tab 移出或任意外部 pointerdown 关闭。
- 选中日期后先让圆盘滑到位、页脚确认，约 240ms 后关闭（源码为 300ms）；Reduced 或 Clear 时立即关闭。
- 页脚：Clear 按钮（无值时禁用）+ 状态文案 "Selected Oct 20, 2026" / "Choose a day"。

**可访问性**：
- 触发器暴露 ExpandCollapse（IExpandCollapseProvider），弹层 Name 为 `"{Label} calendar"`。
- 格式化后的值放在读屏专用元素中，组成触发器 Name 的一部分；滚动文本层为 Raw。
- 标签经 AutomationProperties.LabeledBy 关联触发器，description 作为 HelpText。

**动效**：
- 值按日期部件滚动：只有变化的部件滚动，日期变晚上升、变早下降（P3）；宽度改变的部件让后续部件沿同一曲线（standard + enter）平移让位（P19）。页脚状态文案同样以 0.3em + blur soft 升入。
- 弹层：y −8、scale 0.95 → 1 走 snappy，透明度 fast + enter；退出 y −6、scale 0.97、0.14s + standard；变换原点在日历图标处（P10）。
- Reduced：值改为 instant 交叉淡化，弹层只淡入淡出，选中后立即关闭。

**Avalonia 实现要点**：
- 模板部件：`PART_Trigger`（Button）、`PART_ValuePresenter`（按部件滚动）、`PART_Popup`（AtomUI Popup，内容为 Calendar + 页脚）；Calendar 只在打开时创建，退场由 S3 保活。
- .NET 没有 `Intl.DateTimeFormat.formatToParts`：按部件滚动需要把 DateTimeFormatInfo 的模式串拆成"字段 / 字面量"序列，由 S11 提供 `FormatToParts` 能力。
- 弹层宽 min(320, 视口 − 32)，左对齐字段，内边距 16px；视口宽 < 360px 时拉伸到字段两边、内边距 12px。打开时 description 淡出，避免从弹层圆角下露出。
- Arc 没有 `name` 属性（需要自行序列化到隐藏 input）；Avalonia 侧直接绑定 SelectedDate 即可。
- 服务：S3、S7、S8、S11。

**AtomUI 参考**：DatePicker（仅供学习行为与边界，不复用其实现）。

**风险与未决**：
- 部件拆分要覆盖 zh-CN（"2026年10月20日"）等含字面量的模式，测试需覆盖 en-US、zh-CN、zh-TW。
- 关闭延迟 240ms（文档）与 300ms（源码）不一致，以 Gallery 对照站点实测定值。

### 4.3 DateRangePicker（Date range picker）
`slug: date-range-picker` · 阶段 P6 · Arc 依赖：无 · 建议基类：TemplatedControl

**Arc 行为摘要**：紧凑触发器，打开时表面长成带一或两个月份与预设栏的面板，Apply 后收回并显示已应用的区间。适合报表与分析筛选（Last 7 days、This quarter 等预设）、预订或请假的起止日期，以及需要保持紧凑的工具栏。单个日期用 DatePicker，月份常驻用 Calendar，选时刻用 TimePicker。

**Arc API 要点**：
| Arc 属性 | 类型 / 默认值 | 建议 Avalonia 属性（类型） |
|---|---|---|
| `value` / `defaultValue` | `DateRange \| null`，`null`（首尾均含整天） | `SelectedRange`（DateRange?，双向） |
| `onChange` | 仅在 Apply 时回调 | `SelectedRangeChanged` 事件（只在 Apply 时触发） |
| `label` / `placeholder` | `"Date range"` / `"Select dates"` | `Label` / `Placeholder`（string） |
| `presets` | `DateRangePreset[]`，默认 Today 到 Year to date 共 8 项 | `Presets`（`IList<DateRangePreset>`：Label + `Func<DateTime, DateRange>`） |
| `minDate` / `maxDate` | `Date` | `MinDate` / `MaxDate`（DateTime?） |
| `weekStartsOn` | `0 \| 1`，`0` | `FirstDayOfWeek`（DayOfWeek，Sunday） |
| `months` | `"auto" \| 1 \| 2`，`"auto"` | `MonthCount`（enum：Auto / One / Two） |
| `locale` / `boundary` | `"en-US"` / 视口 | `Culture`（CultureInfo?）/ `Boundary`（Control?，面板需留在其内） |

**交互与键盘**：
- 触发器 Enter / Space 打开面板；网格中 Enter / Space 依次选起点与终点，选定起点后方向键移动会延伸区间预览。
- 方向键、Home / End、PageUp / PageDown（加 Shift 按年）与 Calendar 一致；预设栏用 ArrowUp / ArrowDown，紧凑模式的横向预设行用 ArrowLeft / ArrowRight。
- 选择是草稿，只有 Apply 才提交；Cancel、Escape 或点击外部丢弃草稿，焦点回到触发器。
- 默认预设：Today、Yesterday、Last 7 days、Last 30 days、This month、Last month、This quarter、Year to date；草稿与某个预设相同时该预设高亮。

**可访问性**：
- 触发器暴露 ExpandCollapse，Name 包含当前区间；每个月份是以月标题命名的网格（映射方式同 Calendar），区间内的日暴露选中，今天标注 ItemStatus。
- 日单元格 roving tab stop；离场中的月份集合不可聚焦。
- Polite 播报先读起点，再读区间及天数（如 "12 days"）。

**动效**：
- 触发器表面长成面板：宽、高、圆角（18 → 26）与水平偏移一起走 GROW 弹簧（0.48s、bounce 0.12），Apply 后用 SHRINK（0.38s、bounce 0）收回（P9）。面板内容晚 0.07s 跟进（0.26s + enter，−4px + blur soft），离开时先于表面消失。
- 格式化区间标签以共享元素从面板摘要飞回触发器（P8）。
- 区间高亮每周一条横条，悬停或键盘移动时以 STRETCH（0.3s、bounce 0.04）伸缩，两端实心圆以 GLIDE（0.3s、bounce 0.1）在日间滑行，预设高亮在预设间滑行（P7）。
- 月份按方向滑入（x +48 / −36，blur soft，SLIDE 0.4s、bounce 0.06）（P25）；标签逐词按时间方向滚动（±0.45em、0.26s）（P3）。
- Reduced：表面尺寸直接跳变，去掉共享元素与滑动，改用 0.08–0.12s 透明度淡化。

**Avalonia 实现要点**：
- 表面形变需在浮层层（AtomUI Popup / ScopeAwareOverlayLayer）中绘制一块与触发器重合的表面再动画其尺寸与圆角；打开期间触发器保持占位、不可交互。S5 观察触发器与面板尺寸，同一轮布局后统一驱动弹簧。
- `MonthCount=Auto` 时边界宽 ≥ 712px 显示两月 + 竖向预设栏，否则一月 + 横向滚动预设行；紧凑面板宽 min(352, 可用宽)，日格 32–42px；面板水平平移以保持离边界 8px，窗口尺寸变化时重算。
- GROW、SHRINK、STRETCH、GLIDE、SLIDE 五组弹簧用 S1 的 SpringSpec(visualDuration, bounce) 表达，作为本控件常量或 Own Token，不新增全局预设。
- 每周横条与两个端点各自由 S2 动效值驱动，悬停只改动效值、不重建网格；DateTimeFormatInfo 按 Culture 缓存。
- 服务：S1、S2、S5、S8、S11；P8 复用 Card（P5）落地的 SharedElementScope。

**AtomUI 参考**：RangeDatePicker（仅供学习行为与边界，不复用其实现）。

**风险与未决**：
- "从触发器长成面板"跨越布局层与浮层层，是本大类最复杂的动效，需在 P6 前用原型验证裁剪、阴影与焦点归还。
- Arc 不依赖 Calendar 而自带月份网格；是否抽取共享的内部网格与日期工具（不影响公开 API）在详细规格中决定。

### 4.4 TimePicker（Time picker）
`slug: time-picker` · 阶段 P6 · Arc 依赖：无 · 建议基类：TemplatedControl

**Arc 行为摘要**：触发器 + 按固定分钟步长生成的可滚动时刻列表，显示的时间像时钟一样滚动。用于会议、提醒等固定间隔的时刻；显示 12 小时制，存储 24 小时 "HH:mm"。日期用 DatePicker（二者组合成时间戳），只允许少数命名时刻时用 Select。

**Arc API 要点**：
| Arc 属性 | 类型 / 默认值 | 建议 Avalonia 属性（类型） |
|---|---|---|
| `label`（必填） | `string` | `Label`（string） |
| `value` / `defaultValue` / `onChange` | `"HH:mm"` 字符串，默认 `"09:00"` | `SelectedTime`（TimeSpan?，双向，默认 09:00；为 null 时显示 Placeholder）+ `SelectedTimeChanged` 事件 |
| `description` | `string` | `Description`（string?） |
| `placeholder` | `"Select a time"` | `Placeholder`（string） |
| `minuteStep` | `1 \| 5 \| 10 \| 15 \| 30`，`15` | `MinuteStep`（int，限这五个值，15） |
| `format` | `"12h" \| "24h"`，`"12h"`（值始终 24 小时制） | `ClockFormat`（enum：TwelveHour / TwentyFourHour） |
| `disabled` | `false` | `IsEnabled`（继承） |

**交互与键盘**：
- Enter / Space 打开列表，列表打开时选中活动项；ArrowDown / ArrowUp 在关闭时打开列表，打开后在时刻间移动并首尾循环。
- Escape 关闭；任意外部 pointerdown 关闭；指针移动即改变活动行。
- 打开时列表以选中时刻居中，只滚动列表自身、不滚动页面；方向键移动时保持活动行可见。

**可访问性**：
- 触发器暴露 ExpandCollapse，Name 由标签与隐藏的当前值组合；列表为 List，选项为 ListItem 并经 ISelectionItemProvider 暴露选中。
- Arc 中焦点始终留在触发器、活动行只是视觉高亮；Avalonia 中需让读屏感知活动行（把焦点移到选项，或在触发器 peer 上报告活动项），规格中二选一。

**动效**：
- 显示值：更晚的时刻从下方升起、更早的从上方落下，带 blur soft；退出 fast + standard（P3）。
- 列表：y −6、scale 0.97 → 1，snappy + fast 透明度；退出 y −4、scale 0.98，instant（P10）。
- 活动行背景 SurfaceMuted 以 80ms 切换；选中项右侧 6px Foreground 圆点，因此任一时刻只有一行高亮。
- Reduced：值交叉淡化，列表只淡入淡出。

**Avalonia 实现要点**：
- 模板部件：`PART_Trigger`、`PART_ValuePresenter`、`PART_Popup`、`PART_List`（列表最高 250px、内边距 5px、与触发器同宽，选项最小高 36px、圆角 RadiusPanel − 5px）。
- 选项数为 1440 / MinuteStep；Arc 一次渲染全部（步长 1 时 1440 行），Avalonia 侧用虚拟化面板，并在首帧布局前设置 ScrollViewer.Offset 使选中项居中。
- 12 小时制文案：Arc 固定为 "h:mm AM/PM"；本地化时改用 DateTimeFormatInfo 的 AMDesignator / PMDesignator 还是保留英文，需定案。
- 服务：S3、S8、S9、S11。

**AtomUI 参考**：TimePicker（仅供学习行为与边界，不复用其实现）。

**风险与未决**：
- TimeSpan? 与 TimeOnly? 的选择与 Calendar 一并决定；对外另提供 "HH:mm" 字符串转换以对齐 Arc 语义。
- 虚拟化后"打开即居中"需在首帧前完成测量，否则会出现一帧跳动。

### 4.5 ColorPicker（Color picker）
`slug: color-picker` · 阶段 P6 · Arc 依赖：TextMorph · 建议基类：TemplatedControl

**Arc 行为摘要**：色块触发器长成选色面板：饱和度 / 明度区域、色相与不透明度滑块、可切换格式的文本值、WCAG 对比度读数与保存的色板。用于主题编辑器、品牌设置，以及需要 hex、RGB、HSL、OKLCH 输入与透明度的设计工具。在少数固定颜色中选择用 SegmentedControl 或 RadioGroup，纯 hex 文本用 Input，小预设色标签用 ChipGroup。

**Arc API 要点**：
| Arc 属性 | 类型 / 默认值 | 建议 Avalonia 属性（类型） |
|---|---|---|
| `value` / `defaultValue` | `string`（hex、rgb()、hsl()、oklch()），默认 `"#2F6BFF"` | `Color`（Avalonia Color，双向，默认 #2F6BFF） |
| `onValueChange` | 回调大写 hex，非不透明时带两位 alpha | `ColorChanged` 事件；另提供只读 `HexValue`（string，CSS 顺序 #RRGGBBAA） |
| `background` | `string`，`"#FFFFFF"` | `ContrastBackground`（Color，White） |
| `label` | `"Color"` | `Label`（string） |
| `swatches` / `defaultSwatches` / `onSwatchesChange` | `ColorSwatch[]`（id、color），默认空 | `Swatches`（`IList<ColorSwatch>`，双向）+ `SwatchesChanged` 事件 |
| `maxSwatches` | `7`，超出丢弃最旧 | `MaxSwatches`（int，7） |
| `defaultFormat` | `"hex" \| "rgb" \| "hsl" \| "oklch"`，`"hex"` | `Format`（enum ColorFormat：Hex / Rgb / Hsl / Oklch） |

**交互与键盘**：
- 方向键在二维区域中移动饱和度 / 明度拇指，或调整聚焦的色相 / 不透明度滑块；Shift 与 PageUp / PageDown 步长 10，Home / End 到最小 / 最大。
- 文本框 Enter 应用输入值，非法值抖动并显示错误；Escape 先丢弃输入草稿，再关闭面板并把焦点还给触发器。
- 色板行：Home / End 到首尾，Alt + ArrowLeft / ArrowRight 移动聚焦色板，Delete / Backspace 删除，也可拖拽重排。
- 吸管按钮只在 window.EyeDropper 存在时出现（Chromium 桌面）。

**可访问性**：
- 触发器暴露 ExpandCollapse，面板 Name 为 `"{Label} color"`，关闭过程中不可交互。
- 区域拇指为 Slider 类型 peer，GetLocalizedControlTypeCore 返回 "2D slider"，值文本同时报饱和度与明度；色相 / 不透明度滑块经 IRangeValueProvider 报角度与百分比。
- 文本框非法时以 HelpText 指向错误信息并 Polite 播报；对比度与 WCAG 等级（AAA ≥ 7、AA ≥ 4.5、AA large ≥ 3、Fails）同时提供文字版本。
- 色板为横向 List，roving tab stop，当前色暴露选中，Name 说明移动与删除方式。

**动效**：
- 面板从色块以裁剪矩形揭示：内缩量随弹簧（visualDuration 0.42、bounce 0.1）变化，圆角 22 → 26；关闭用 0.3s 无回弹弹簧，阴影跟随裁剪形状（P29、P9）。
- 拇指以快速弹簧追随指针（0.26s、bounce 0.22），拖拽中换成 0.1s 无回弹的硬弹簧以贴手（P12）。
- 对比度数值滚动计数到新值（P14）；格式按钮文字经 TextMorph 在 HEX、RGB、HSL、OKLCH 间形变（P4）。
- 非法输入抖动 x 0 → −5 → 4 → −2 → 0，0.32s + standard（P13）；色板以 snappy 弹出，重排用 smooth 布局弹簧，拖拽时放大到 1.15（P26、P19）。
- Reduced：拇指直接跳到位，面板淡入代替裁剪，去掉格式形变与抖动。

**Avalonia 实现要点**：
- 二维区域与滑块用指针捕获 + S10 手势，拖动不滚动外层（对应 Arc 的 touch-action none）；拇指位置用 S2 动效值，拖动不重绘轨道。
- 颜色换算（HSV、HSL、OKLCH、合成后的 WCAG 对比度）纯数学实现，与 ArcPalette 的 OKLCH 推导共用 Core 中的颜色工具。
- 面板宽 min(19rem, 视口 − 32px)，在色块右下方展开，只在打开时创建；视口 < 360px 时隐藏 "against background" 说明。裁剪揭示用 Clip 几何动画，Clip 会连同 BoxShadow 一起裁掉，阴影需画在与裁剪矩形同步的独立层上。
- 服务：S1、S2、S3、S7、S8、S9、S10。

**AtomUI 参考**：ColorPicker（独立包）（仅供学习行为与边界，不复用其实现）。

**风险与未决**：
- 十六进制字节序：Arc 输出 CSS 顺序 #RRGGBBAA，而 Avalonia Color 的 8 位十六进制是 #AARRGGBB；解析与输出必须自己实现，不能直接用 Color.Parse / ToString。
- 吸管：Avalonia 没有跨平台读取屏幕颜色的 API（macOS 还涉及录屏权限），v1 建议不提供并在规格中记为差异。
- 抖动（P13）首次落地在同阶段的 PasswordStrength，需协调实现顺序。

## 5. 实现顺序与依赖
1. **P1 Calendar**：本大类第一个落地，按主数据也是 P3、P6、P7、P14、P25 的首个使用者（P6、P14 见 4.1 未决）；需要 S1、S2、S7、S11 与网格键盘模型，日期工具放入 Core 供后续复用。
2. **P2 DatePicker**：硬依赖 Calendar；首次落地 P19（部件重排）；P10 与 S8 锚定浮层由 Tooltip（P1）先行落地；需要 S11 的 FormatToParts。
3. **P6 TimePicker**：依赖 S8、S9，复用 DatePicker 的字段外壳与值滚动。
4. **P6 DateRangePicker**：依赖 P8（Card，P5 落地）、P9（ExpandableCard，P4 落地）与 S5；在 Calendar 的日期工具基础上实现自有双月网格。
5. **P6 ColorPicker**：硬依赖 TextMorph（文字效果大类，P1）；P13 与同阶段的 PasswordStrength 协调，P12（UserMenu，P5）、P26（Avatar，P1）、P29（Button，P0）已先落地；需要 S10。
- 跨大类：字段外壳遵循 forms-and-fields；TimePicker 的列表行为（S9）与 Selects 保持一致；DatePicker 与 TimePicker 常并排组成时间戳，二者高度、标签与描述对齐需一致。

## 6. 相关文档
- [组件总清单](../overview.md)
- [组件规格模板](../spec-template.md)
- [动效原语目录](../../architecture/motion/primitives-catalog.md)
- [Token 目录](../../architecture/theming/token-catalog.md)
- [浮层与输入](../../architecture/overlays-and-input.md)
- [字段通用结构](../../architecture/forms-and-fields.md)
- [路线图](../../roadmap/overview.md)
- Arc 原页面见第 2 节表格中的链接。
