# 选择器（Selects）

> Arc 的选择器大类从已知选项中取值：紧凑下拉、可搜索单选、多选、触发器会长成列表的形变选择器，以及筛选芯片。

## 1. 大类定位
- 解决"从一组已知选项中挑一个或几个值"：短固定列表（Select）、长或可搜索的单选（Combobox）、紧凑多选（MultiSelect）、带分组、图标与搜索的属性选择（MorphSelect）、频繁切换的分面筛选（ChipGroup）。
- 边界：自由文本归 [文本输入](../text-fields/overview.md)，自由短值列表用 [特殊输入](../special-inputs/overview.md) 的 TagInput；两到五个始终可见的选项用 [开关与选项](../toggles/overview.md) 的 SegmentedControl 或 RadioGroup；执行动作而非存值用 [菜单](../menus/overview.md) 的 DropdownMenu。
- 共同特征：触发器沿用字段壳体；选中值在触发器中按列表方向滚动替换；弹出列表从上方轻落并缩放进入，高亮与选中标记独立于选项绘制。

## 2. 组件一览

| 组件 | 类名 | Arc 页面 | 阶段 | Arc 依赖 | 建议基类 | 原语 |
|---|---|---|---|---|---|---|
| Select | `Select` | [select](https://uiarc.dev/components/select) | P5 | 无 | SelectingItemsControl | P3 P10 |
| Combobox | `Combobox` | [combobox](https://uiarc.dev/components/combobox) | P6 | 无 | SelectingItemsControl | P2 P6 P10 P26 |
| Multi-select | `MultiSelect` | [multi-select](https://uiarc.dev/components/multi-select) | P6 | 无 | SelectingItemsControl | P5 P10 P14 P15 P26 |
| Morph select | `MorphSelect` | [morph-select](https://uiarc.dev/components/morph-select) | P6 | 无 | SelectingItemsControl | P3 P5 P7 P8 P9 |
| Chip group | `ChipGroup` | [chip-group](https://uiarc.dev/components/chip-group) | P6 | 无 | SelectingItemsControl | P1 P5 P6 P19 P26 |

## 3. 共享约定
字段结构（标签、描述、校验态）以 [forms-and-fields](../../architecture/forms-and-fields.md) 为准；本大类的事实与偏差如下。
- **触发器**：Select、Combobox、MultiSelect 的触发器最小高度 `ControlSizeMD`、`RadiusControl`，标签在上；Select 与 MultiSelect 的触发器是弹层锚点，按下不缩放、只用颜色反馈，保证锚点测量稳定。MorphSelect 的触发器是按标签宽度自适应的胶囊；ChipGroup 没有触发器（芯片高 `ControlSizeSM`）。
- **选项模型**：五个控件都派生 `SelectingItemsControl`（选择、`SelectedIndex` / `SelectedItem` / 双向 `SelectedValue`、`SelectionChanged` 由基类提供）。选项来自 `ItemsSource`（任意对象或 `SelectOption` 记录）或 XAML 内联的 `SelectItem`（只承载数据：Content、Value、Icon、Meta、Keywords、Group、IsEnabled，不作为容器显示）。标签依次取 `TextSelector`、`SelectItem.Content` / `SelectOption.Label`、`DisplayMemberBinding`、`ToString()`；值依次取 `ValueSelector`、`SelectItem.Value` / `SelectOption.Value`、项本身（默认 `SelectedValueBinding` 是无路径 `CompiledBinding` 加转换器，AOT 安全）；禁用取 `DisabledSelector`、`IsEnabled` / `IsDisabled`。多选控件另有 `SelectedItems` 与只读 `SelectedValues`。
- **弹出列表**：从上方 6px、0.97 缩放进入（Combobox、MultiSelect 用 snappy 弹簧加 fast 透明度，Select 用 standard + enter 的关键帧，向上展开时方向相反）；退出时上移 3 到 4px、0.98 缩放，instant（P10）；宽度跟随触发器。Arc 的半透明菜单带背景模糊，在减少透明度设置下改为不透明 `SurfaceRaised`。
- **键盘**：Enter / Space / ArrowDown 打开；方向键在可用项间移动（跳过禁用项）；Enter 选择；Escape 关闭且保持原值；Select 与 MorphSelect 支持键入跳转（typeahead）。
- **值滚动**：Select 与 MorphSelect 的触发器中，较后的选项从下方升起、较前的从上方落下，带 soft 模糊（P3）；Reduced 为 instant 交叉淡化。
- **清除按钮**（Combobox、MultiSelect）：从 0.6 缩放、subtle 模糊以 snappy 出现，按下 0.96。
- **可访问性**：字段 peer 为 ComboBox 类型，实现 IExpandCollapseProvider 与只读 IValueProvider（真实值，动画副本不暴露），名称取 `Label`、帮助文字取 `Description`。键盘焦点始终留在触发器或输入框，选项行是绘制出来的、没有独立 peer；高亮项与结果数通过隐藏的 `LiveSetting=Polite` 文本播报（如 "London, 3 of 12"、"5 results"）。ChipGroup 为命名的 Group，芯片为 Button + IToggleProvider，more 芯片带 ExpandCollapse。
- **不映射**：`name` 与隐藏 input 的原生表单提交；Radix 的 portal 由 S8 浮层替代。
- **列表规模**：Arc 的选项都不虚拟化；本库四个弹层控件共用内部的 `SelectOptionView`：只在 `Render` 中绘制滚动视口内的行（标签、图标、meta、勾、分组标题），没有任何容器，过滤只替换行索引表，几千项也只有几十个缓存的文本布局。高亮用 Core `GlidingIndicator`（Select、MorphSelect 滑动；Combobox、MultiSelect 按 Arc 的 80ms 变色改为直接跳到新行）。
- **动效降级**：Reduced 只保留透明度变化，尺寸与位置直接跳到终态；Off 直接切换。
- **共享内部件**（`Selects/_Shared`，internal）：`SelectEntrySource`（项到条目的解析与过滤）、`SelectOptionView`（绘制式虚拟化列表）、`SelectValueRoll`（触发器值滚动 P3 与飞入 P8，所有副本左对齐铺满槽位，只有标签省略号、不会横移）、`SelectInputBox`（代码模板的无框 TextBox）、`SelectTypeahead`（Radix 键入跳转：同字母重复则轮换）、`SelectFieldAutomationPeer`、`SelectGlyphs`（修正版 Lucide `x`）。
- **浮层**：都用 Core `ArcPopup`，模板内声明，`OverlayInputPassThroughElement` 设为触发器（MultiSelect 为触发器与清除按钮所在的容器），所以按下打开中的触发器是切换而不是轻触关闭再打开。

## 4. 组件

### 4.1 Select（Select）
`slug: select` · 阶段 P5 · Arc 依赖：无 · 建议基类：SelectingItemsControl

**Arc 行为摘要**：带标签的紧凑选择字段，弹出键盘友好的菜单，显示值按列表方向滚动替换。用于区域、排序方式等无需键入的短固定列表。长或可搜索的列表用 Combobox；多选用 MultiSelect；两到四个应始终可见的选项用 SegmentedControl。

**Arc API 要点**：

| Arc 属性 | 类型 / 默认值 | 建议 Avalonia 属性（类型） |
|---|---|---|
| `label`（必填） | `string` | `Label`（string） |
| `options`（必填） | `{ value, label, disabled? }[]` | `ItemsSource` + `SelectedValueBinding` / `DisplayMemberBinding` |
| `value` / `defaultValue` / `onValueChange` | `string` | `SelectedValue`（object?，双向）/ `SelectionChanged` 事件 |
| `open` / `onOpenChange` | `boolean` | `IsDropDownOpen`（bool，双向） |
| `placeholder` | `"Select an option"` | `PlaceholderText`（string，本地化默认） |
| `description` | `string` | `Description`（string?） |
| `disabled` / `required` | `boolean` | `IsEnabled`（继承）/ `IsRequired`（bool） |

**交互与键盘**：
- 触发器上 Enter / Space / ArrowDown 打开；ArrowUp / ArrowDown 在可用项间移动；Enter / Space 选择并关闭；Escape 关闭且不改值；键入字母跳到下一个以该字母开头的项。
- 菜单宽度等于触发器，最宽 min(24rem, 视口宽减 20px)，最高 320px 或可用高度，超出滚动；长标签在触发器中省略而不撑宽。

**可访问性**：
- Arc 的角色与焦点处理来自 Radix：映射为 ComboBox 类型 peer（IExpandCollapseProvider、ISelectionProvider）与 ListItem 选项。
- 真实值放在对辅助技术可见的隐藏文本中，动画副本隐藏；标签与描述分别走 `LabeledBy` 与 `HelpText`。

**动效**：
- 显示值：较后的选项从下方 0.35em 升起、较前的从上方落下，soft 模糊，standard + enter；离开的值 fast（P3）。
- 菜单进出见共享约定（P10）；选中指示从 0.6 缩放、2px 模糊进入。
- Reduced：值为 instant 交叉淡化，菜单只做 instant 淡入淡出；Off：直接切换。

**Avalonia 实现要点**：
- 公开 API：`Label`、`Description`、`Placeholder`（"Select an option"）、双向 `IsOpen`、`MaxDropDownHeight`（320）、`TextSelector` / `ValueSelector` / `DisabledSelector`、只读 `SelectedText`、`Open()` / `Close()`。
- 模板：标签、触发器 Border（44 高、RadiusControl、内边距 12，悬停 / 按下 / 打开只变色）、`SelectValueRoll`（0.35em 升入、soft 模糊）、旋转 180° 的 chevron（CSS 弹簧）、`ArcPopup`（Bottom、离触发器 4px、宽度等于触发器、自上方 6px 与 0.97 进入）内的 20 圆角菜单，Radix 的上下滚动按钮（悬停时每 50ms 滚一行）。
- 键盘：关闭时 Enter / Space / 上下键打开，打开时停在选中项；上下键不循环（Radix），Home / End、PageUp / PageDown；关闭时键入直接选中匹配项，打开时移动高亮；Tab 关闭。鼠标在按下时打开，触摸在抬起时打开。
- 选中项的勾在每次打开时从 0.6 缩放淡入（延迟 40ms，Arc 的 ItemIndicator 随菜单挂载）；Reduced 下直接出现。

**AtomUI 参考**：Select（仅供学习行为与边界，不复用其实现）。

**风险与未决**：
- Avalonia 没有通用的背景模糊（backdrop-filter），浮层无法模糊其后内容；统一用不透明 `SurfaceRaised`（即 Arc 的减少透明度外观）。
- 高亮滑动是按任务要求加的（Arc 的 Select 高亮是 120ms 变色）；ArcPopup 的进场固定为 fast，Arc 的 Select 为 standard。

### 4.2 Combobox（Combobox）
`slug: combobox` · 阶段 P6 · Arc 依赖：无 · 建议基类：SelectingItemsControl

**Arc 行为摘要**：可搜索的单选字段：在字段中键入过滤，从高度随结果弹簧变化的 listbox 中选择。用于时区、国家等长列表，以及人们知道名称、想直接键入（含别名关键字）的场景。短列表用 Select；多选用 MultiSelect；搜索是为了导航而非存值时用 ExpandingSearch。

**Arc API 要点**：

| Arc 属性 | 类型 / 默认值 | 建议 Avalonia 属性（类型） |
|---|---|---|
| `label`（必填） | `string`（同时命名 listbox） | `Label`（string） |
| `options`（必填） | `{ value, label, disabled?, keywords? }[]` | `ItemsSource`（ComboboxOption：Value、Label、IsDisabled、Keywords） |
| `value` / `defaultValue` / `onValueChange` | `string` / `""`（空串表示未选） | `SelectedValue`（object?，双向；未选为 null）/ `SelectionChanged` |
| `placeholder` | `"Search or select…"`；搜索时改显示已选标签 | `PlaceholderText`（string） |
| `emptyMessage` | `"No matches found"` | `EmptyText`（string，本地化默认） |
| `description` | `string` | `Description`（string?） |

**交互与键盘**：
- ArrowDown / ArrowUp 打开列表，再在可用项间移动并循环；Enter 选择当前项；Escape 关闭并丢弃查询。
- 弹层外任意指针按下即关闭；输入框显示的是标签而不是值。

**可访问性**：
- 输入框为 combobox（expanded、controls、autocomplete=list、activedescendant）；选项带 selected / disabled，列表名为 "<label> options"。
- 空状态是 status 行（`Polite`）；清除按钮名为 "Clear selection"。

**动效**：
- 弹层从上方弹簧进入（P10）；listbox 高度随过滤以 smooth 跟随（P6）。
- 选中的标签以 soft 模糊从下方 0.35em 升入字段，standard + enter（P2）；清除按钮缩放进入。
- Reduced：只保留透明度淡入淡出，高度立即变化；Off：直接切换。

**Avalonia 实现要点**：
- 公开 API：`Label`、`Description`、`Placeholder`（"Search or select…"）、`EmptyText`（"No matches found"）、双向 `IsOpen`、`MaxDropDownHeight`（300，另受窗口 40% 限制）、`FilterPredicate(item, query)`、三个 Selector、只读 `SearchText` / `SelectedText`、`InputBox`、`FocusInput()`、`Clear()`。
- 输入框是代码模板的 Avalonia `TextBox`（`SelectInputBox`），打开时显示查询、关闭时显示标签；搜索中占位符换成已选标签。获得焦点即打开（Arc 行为）；焦点离开字段（`IsKeyboardFocusWithin` 变为 false）时关闭，Arc 只在外部按下时关闭。
- 弹层锚在描述下方（无描述时锚在字段下方）8px，RadiusPanel、内边距 5；列表外包 `SpringSizeDecorator`（Height、smooth）跟随过滤后的高度；空状态以 4px 上升淡入。
- 选中后输入文字从下方 0.35em、4px 模糊升入（代码驱动的一次性动效）；清除按钮用 `PresenceHost`（Pop，0.6）出现，按下 0.96。

**AtomUI 参考**：AutoComplete、ComboBox（仅供学习行为与边界，不复用其实现）。

**风险与未决**：
- 焦点留在 TextBox 时向读屏报告当前高亮项（Arc 的 aria-activedescendant）没有直接等价物，需 overlays-and-input 的统一方案。
- 清除按钮的缩放进入属于 P26，主数据未列。
- 复核结论（汇总评审）：新增 P26，已同步到[组件总清单](../overview.md)。

### 4.3 MultiSelect（Multi-select）
`slug: multi-select` · 阶段 P6 · Arc 依赖：无 · 建议基类：SelectingItemsControl

**Arc 行为摘要**：从固定列表中选多个值的下拉，选中项以芯片显示在触发器中，超出部分折叠为滚动计数的 +N。用于标签、负责人等紧凑多选，以及需要在字段内显示已选值的筛选。所有选项都应可见时用 ChipGroup；自由值用 TagInput；单值用 Select。

**Arc API 要点**：

| Arc 属性 | 类型 / 默认值 | 建议 Avalonia 属性（类型） |
|---|---|---|
| `label`（必填） | `string`（同时命名 listbox） | `Label`（string） |
| `options`（必填） | `{ value, label, disabled? }[]` | `ItemsSource` + `SelectedValueBinding` |
| `value` / `defaultValue` / `onValueChange` | `string[]` / `[]` | `SelectedItems`（IList，`SelectionMode` 为 Multiple 加 Toggle）/ `SelectionChanged` |
| `placeholder` | `"Select options"` | `PlaceholderText`（string） |
| `maxVisible` | `2` | `MaxVisibleChips`（int） |
| `description` | `string` | `Description`（string?） |
| `disabled` | `false`（同时隐藏清除按钮） | `IsEnabled`（继承） |

**交互与键盘**：
- Enter / Space 打开或关闭，打开时 Enter 切换当前项；ArrowDown / ArrowUp 打开并在可用项间移动、循环；Escape 关闭。
- 芯片最宽 9rem 并省略，`MaxVisibleChips` 之外折叠为 +N，触发器保持一行。

**可访问性**：
- 触发器带 haspopup=listbox 与 expanded，名称由标签加隐藏的已选标签列表组成，映射为 `Name` = 标签加已选项摘要。
- 菜单为可多选的 listbox；芯片对辅助技术隐藏，以口语摘要代替；清除按钮名为 "Clear selections"。

**动效**：
- 芯片槽位宽度以 smooth 展开（P5），芯片从 0.9 带 soft 模糊以 snappy 长出（P26）；+N 计数上下滚动（P14）。
- 菜单中的勾按路径绘制（P15）；菜单从上方弹簧进入（P10）。
- Reduced：所有变化改为短交叉淡化，无缩放与宽度移动；Off：直接切换。

**Avalonia 实现要点**：
- 公开 API：`Label`、`Description`、`Placeholder`（"Select options"）、双向 `IsOpen`、`MaxVisibleChips`（2）、`MaxDropDownHeight`（默认不限，与 Arc 相同，由屏幕约束）、`IsSearchable` / `SearchPlaceholder` / `FilterPredicate` / `EmptyText`（搜索是补充功能，Arc 没有）、`SelectedItems`、`Selection`、只读 `SelectedValues`（按选择顺序）、`SelectAll()` / `UnselectAll()`（代码用；Arc 无全选 UI）。`SelectionMode` 默认 Multiple | Toggle。
- 芯片行 `MultiSelectChipRow`：每个芯片在一个裁剪槽位里，槽宽 smooth 弹簧、透明度 fast，芯片自 0.9 snappy 长出并从 4px 模糊变清晰，离开时 instant 缩回；空间不足时芯片槽按宽度比例收缩，+N 芯片不收缩，计数用 `SelectValueRoll` 上下滚动。芯片按选择顺序排列（Arc 的 value 数组顺序）。
- 菜单行高 38、圆角 21，勾按路径绘制（240ms enter）并在取消时回缩；Enter 切换当前项且菜单保持打开。

**AtomUI 参考**：Select(Mode=Multiple)（仅供学习行为与边界，不复用其实现）。

**风险与未决**：
- Arc 菜单不设最大高度，桌面窗口较矮时可能超出屏幕；是否加上限（偏离 Arc）需在规格中决定。

### 4.4 MorphSelect（Morph select）
`slug: morph-select` · 阶段 P6 · Arc 依赖：无 · 建议基类：SelectingItemsControl

**Arc 行为摘要**：触发器直接长成选项列表的选择器，带滑动高亮与键入跳转，选中标签飞回触发器。用于表单与属性面板中的已知列表、带图标与附加信息的分组列表（时区、负责人、项目），以及打开前像胶囊的紧凑工具栏选择。主要键入自由文本或列表很长且来自远端时用 Combobox；多选用 MultiSelect；执行动作用 DropdownMenu。

**Arc API 要点**：

| Arc 属性 | 类型 / 默认值 | 建议 Avalonia 属性（类型） |
|---|---|---|
| `label`（必填）/ `hideLabel` | `string` / `false` | `Label` / `IsLabelVisible`（bool） |
| `items`（必填） | 选项 `{ value, label, icon?, meta?, keywords?, disabled? }` 或分组 `{ label, options }` | `ItemsSource`（MorphSelectOption / MorphSelectGroup） |
| `value` / `defaultValue` / `onValueChange` | `string \| null`；回调附选中项 | `SelectedValue`（双向）/ `SelectionChanged` |
| `placeholder` | `"Select"` | `PlaceholderText`（string） |
| `searchable` / `searchPlaceholder` | `boolean \| "auto"` / `"auto"`（超过 8 项开启） | `SearchMode`（Auto / On / Off）/ `SearchPlaceholderText` |
| `panelWidth` / `maxListHeight` | `272` / `296` | `PanelWidth` / `MaxListHeight`（double） |
| `align` | `"start" \| "end"` / `"start"` | `PanelAlignment`（Start / End；保持不动的一侧） |
| `disabled` | `false` | `IsEnabled`（继承） |

**交互与键盘**：
- Enter / Space / ArrowDown / ArrowUp 在已选项处打开，打开后 Enter / Space 选择；方向键跳过禁用项；PageDown / PageUp 一次 8 项；Home / End 到首尾（关闭时直接在首尾打开）。
- Alt + ArrowUp 选择并关闭；可打印字符做键入跳转，开启搜索时转为搜索；Escape 关闭并把焦点还给触发器；Tab 关闭且不选择。

**可访问性**：
- 触发器为 combobox（haspopup=listbox、expanded、activedescendant）；开启搜索后由输入框接管 combobox 角色，触发器变为 inert。
- 选项带 selected / disabled，分组为以标题命名的 Group；搜索结果数量或 No matches 以 `Polite` 播报。

**动效**：
- 触发器表面长成列表：展开 0.4s、bounce 0.12，收起 0.34s、bounce 0 不过冲，圆角同步形变到 22（P9）。
- 单一高亮在选项间滑动，0.28s、bounce 0.08（P7）；选中标签从列表飞入触发器，0.3s、bounce 0.06（P8），同时触发器宽度以 morph 弹簧变化（P5），触发器中的值按列表方向上下滚动（P3）。
- Reduced：尺寸直接跳变，去掉飞行与滚动，改用短淡入淡出；Off：直接切换。

**Avalonia 实现要点**：
- 公开 API：`Label`、`IsLabelVisible`、`Placeholder`（"Select"）、双向 `IsOpen`、`SearchMode`（Auto：多于 8 项）、`SearchPlaceholder`、`PanelWidth`（272）、`MaxListHeight`（296）、`PanelAlignment`（Start / End）、`FilterPredicate`、三个 Selector、只读 `SelectedText`。分组用 `SelectItem.Group` / `SelectOption.Group`（连续同组的选项前插入标题），图标为 Lucide `Geometry` 或 `IImage` 头像，`Meta` 用等宽数字。
- 窗口内的触发器是 `MorphSelectAnchor` 里的胶囊（宽度 = 标签宽 + 50，加宽用 morph 弹簧、变窄用无过冲弹簧，上限 320）。打开时在 `ArcPopup` 中显示 `MorphSelectSurface`：起点与触发器完全重合（自定义放置回调，不翻转；阴影留白按窗口边缘裁小，避免浮层被推移），宽高以 grow（0.4s/0.12）或 shrink（0.34s/0）弹簧变化，圆角保持 22，材质从 Surface + 静止阴影变为 SurfaceRaised + 浮动阴影；列表面固定宽度、延迟 40ms 淡入下落。浮层内容尺寸固定为最大形状，窗口不会每帧改尺寸；动画中按形状裁剪子元素，静止时取消裁剪。
- 选中后标签从选项位置飞入盖子（fly 弹簧 0.3s/0.06），盖子同时收回到新标签宽度，收拢结束后浮层无淡出地关闭，下面的窗口触发器已是新值。
- 搜索行（图标、输入框、清除按钮、关闭热区）在打开时替换盖子里的值；结果数以 Polite 播报。键盘完全按 Arc：PageUp / PageDown 一次 8 项，Alt+上 选择并关闭，键入 600ms 内累积。

**AtomUI 参考**：Select（仅供学习行为与边界，不复用其实现）。

**风险与未决**：
- 表面形变与共享元素飞行要跨越触发器（窗口内）与浮层（可能是独立顶层）两层，坐标换算与首帧对齐风险高，建议加入 Phase 0 的浮层 spike（S0.10）用例。

### 4.5 ChipGroup（Chip group）
`slug: chip-group` · 阶段 P6 · Arc 依赖：无 · 建议基类：SelectingItemsControl

**Arc 行为摘要**：可切换的筛选芯片组，选中时芯片形变，长列表可折叠到 "+N more" 芯片后。用于频繁切换的分面筛选（主题、类别），也可作可清除的单选（multiple 为 false）。空间紧张时用 MultiSelect；表单中用 Checkbox；在几个视图中选一个用 SegmentedControl。

**Arc API 要点**：

| Arc 属性 | 类型 / 默认值 | 建议 Avalonia 属性（类型） |
|---|---|---|
| `options`（必填） | `{ value, label }[]` | `ItemsSource` + `DisplayMemberBinding` / `SelectedValueBinding` |
| `value`（必填）/ `onValueChange`（必填） | `string[]`（按选项顺序） | `SelectedItems`（IList，双向）/ `SelectionChanged` |
| `label`（必填） | `string`（组的可访问名称） | `Label`（写入 `AutomationProperties.Name`） |
| `multiple` | `true`（单选模式下已选项仍可清除） | `SelectionMode`（Multiple 加 Toggle；单选为 Single 加 Toggle） |
| `maxVisible` | `Infinity` | `MaxVisibleItems`（int?；已选项始终可见） |

**交互与键盘**：
- 方向键在芯片与 more 芯片间移动焦点并循环；Home / End 到首尾；Enter / Space 切换当前芯片或展开折叠部分。
- 已选芯片始终可见，即使超出 `MaxVisibleItems`，折叠不会藏起生效的筛选。

**可访问性**：
- 组为带名称的 group；每个芯片是带 pressed 状态的按钮，映射为 ToggleButton 语义（IToggleProvider）。
- 漫游 tabindex 只让一个芯片进入 Tab 顺序（`KeyboardNavigation.TabNavigation=Once`）；more 芯片带 expanded；勾与表面隐藏。

**动效**：
- 选中时勾从左缘长出、标签滑开，芯片边缘以 morph 弹簧跟随新宽度（P26、P5）；邻居芯片滑到新位置，跨行也连贯（P19）。
- 展开的芯片交错进入，总时长封顶 0.3s；组高度以 smooth 变化（P6）。芯片按下缩放 0.97。
- Reduced：形变改为短淡入淡出；Off：直接切换。

**Avalonia 实现要点**：
- 公开 API：`Label`（组的可访问名称）、`IsMultiple`（默认 true；false 时单选且可清除）、`MaxVisibleItems`、双向 `IsExpanded`、`TextSelector` / `ValueSelector`、`SelectedItems`、`Selection`、只读 `SelectedValues`（按选项顺序）、`Toggle(index)`。
- 芯片 `ChipGroupChip` 自绘（高 ControlSizeSM、内边距 14、字重 500），选中色在运行时由 Accent 与 Border / Surface 在 OKLab 中混合（11% / 42%，悬停 16% / 60%），勾为 AccentStrong；宽度变化时可见边缘以 morph 弹簧滞后并保留速度，勾按 (slot + lag) / 18 长出并描边，标签滑过。
- `ChipGroupPanel` 换行布局 + FLIP：每次重排后芯片从旧位置以 morph 弹簧滑到新位置；折叠时离开的芯片退出流并就地淡出；展开时新芯片按 35ms 交错（封顶 0.3s）以 snappy 长出；外层 `SpringSizeDecorator` 让组高 smooth 变化。按下芯片缩放 0.97，抬起按 CSS 弹簧回弹。
- 漫游焦点：只有一个芯片 `IsTabStop`（最后聚焦的、否则第一个已选的、否则第一个），方向键循环、Home / End。

**AtomUI 参考**：CheckableTagGroup（仅供学习行为与边界，不复用其实现）。

**风险与未决**：
- Arc 源码中的勾是缩放进入而非描边绘制，P15 可能多余；按下缩放（P1）未列。
- 复核结论（汇总评审）：新增 P1；移除 P15，已同步到[组件总清单](../overview.md)。

## 5. 实现顺序与依赖
- **P5**：Select 与 [菜单](../menus/overview.md) 同阶段落地，复用 S8 浮层、弹层进出（P10，Tooltip P1 已落地）与 S9 列表键盘导航；P3 来自 Calendar（P1）。
- **P6**：Combobox（P2、P6）→ MultiSelect（P5、P14、P15、P26；+N 复用逐位滚动 presenter）→ ChipGroup（P19 来自 DatePicker P2）→ MorphSelect（P8 来自 Card P5、P9 来自 ExpandableCard P4；与 PhoneInput 共建表面形变 presenter）。
- 跨大类：Combobox、MorphSelect 的过滤与高亮实现与 [文本输入](../text-fields/overview.md) 的 ExpandingSearch、[特殊输入](../special-inputs/overview.md) 的 PhoneInput 共用。

## 6. 相关文档
- [组件总清单](../overview.md) · [规格模板](../spec-template.md) · [路线图](../../roadmap/overview.md)
- [字段通用结构](../../architecture/forms-and-fields.md) · [浮层与输入](../../architecture/overlays-and-input.md)
- [原语目录](../../architecture/motion/primitives-catalog.md) · [Token 目录](../../architecture/theming/token-catalog.md)
- 相邻大类：[文本输入](../text-fields/overview.md) · [特殊输入](../special-inputs/overview.md) · [开关与选项](../toggles/overview.md) · [菜单](../menus/overview.md)
