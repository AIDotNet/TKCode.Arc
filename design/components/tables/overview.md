# 表格与数据（tables）

> Arc 的结构化数据组件：记录表、层级树、手动排序列表、筛选条与代码和 JSON 查看器，强调行级动效与完整的键盘导航。

## 1. 大类定位
- 浏览与整理结构化数据：可排序、可选择的记录表（SortableDataTable），可展开的层级（TreeView、JsonViewer），由用户手动排序的短列表（ReorderableList），列表页上方的分面筛选（FilterToolbar），以及带复制的源码展示（CodeBlock）。
- 边界：按时间排列的活动流归 [动态与会话](../activity/overview.md)（Timeline）；少量可折叠内容归 [展开与滚动](../expand/overview.md)（Accordion）；单个值的复制归 [按钮](../buttons/overview.md)（CopyButton）；分页归 [导航](../navigation/overview.md)（Pagination）；固定选项的筛选归 [选择器](../selects/overview.md)（ChipGroup）。像电子表格一样编辑单元格不在免费组件范围内。
- 共同特征：行级动效（重排滑动、高度展开、当前行高亮滑动）；方向键加单一 Tab 停靠的键盘模型；排序、选择、筛选、复制等状态变化经 Status 区域播报；Arc 的行都不虚拟化，长列表依靠分页或折叠控制规模。

## 2. 组件一览
| 组件 | 类名 | Arc 页面 | 阶段 | Arc 依赖 | 建议基类 | 原语 |
|---|---|---|---|---|---|---|
| Sortable data table | SortableDataTable | [sortable-data-table](https://uiarc.dev/components/sortable-data-table) | P9 | 无 | TemplatedControl | P2 P14 P19 |
| Tree view | TreeView | [tree-view](https://uiarc.dev/components/tree-view) | P9 | 无 | TemplatedControl（扁平虚拟化行） | P6 P7 P19 |
| Reorderable list | ReorderableList | [reorderable-list](https://uiarc.dev/components/reorderable-list) | P7 | 无 | ItemsControl | P1 P12 P14 P19 |
| Filter toolbar | FilterToolbar | [filter-toolbar](https://uiarc.dev/components/filter-toolbar) | P6 | 无 | TemplatedControl | P3 P5 P9 P19 P25 |
| Code block | CodeBlock | [code-block](https://uiarc.dev/components/code-block) | P2 | copy-button | TemplatedControl | P2 P3 P6 |
| JSON viewer | JsonViewer | [json-viewer](https://uiarc.dev/components/json-viewer) | P9 | 无 | TemplatedControl | P2 P6 P7 P26 P30 |

## 3. 共享约定
- 树形键盘（TreeView、JsonViewer）遵循 WAI-ARIA tree 模式：↑/↓ 到可见的上一行或下一行，→ 展开或进入第一个子项，← 折叠或回到父级，Home / End 到首末可见行，Enter / Space 激活；同一时间只有一行在 Tab 序列中（roving tabindex），由 S9 列表键盘导航统一实现。
- 状态播报：排序、选择、筛选增删、复制成功或失败都写入 Status 区域（`LiveSetting=Polite`），不打断正在进行的朗读。
- 动效分工：行进出与重排用 P19（morph），高度展开收起用 P6（smooth），当前行或选中高亮滑动用 P7（morph），数量变化滚动用 P14（smooth）；Reduced 下重排与高度立即完成、计数改为淡入淡出，Off 立即终态。
- 悬停：行的悬停填充只对可悬停的精确指针（鼠标）生效；需要悬停才出现的操作在触摸设备上改为常显，或只在当前行显示（JsonViewer）。
- 窄容器：各有紧凑布局断点（SortableDataTable 小于 620px、FilterToolbar 小于 520px、CodeBlock 小于 420px、JsonViewer 视口不大于 420px）；超长内容在组件内部横向滚动或省略号截断，不撑开页面。
- 规模：Arc 的行都不虚拟化，靠分页（Pagination、JsonViewer 的 `pageSize`）或折叠控制节点数；Avalonia 版保持一致，若将来引入虚拟化，必须先解决与 FLIP 重排动效的冲突。
- 本地化：默认文案（"Data table"、"No rows to show"、"File tree"、"Reorder items"、"Add filter"、"Active filters"、"Source code"、"JSON" 等）进入本地化目录。

## 4. 组件

### 4.1 SortableDataTable（Sortable data table）
`slug: sortable-data-table` · 阶段 P9 · Arc 依赖：无 · 建议基类：TemplatedControl

**Arc 行为摘要**：通用表格，列可排序，可选行（复选框列、点击行选择、Shift 范围选择、带 Clear 的计数行），重新排序时行在弹簧上滑到新位置。用于需要排序和选择的记录，例如项目、发票、用户。按时间排列的活动用 Timeline，由用户手动定顺序用 ReorderableList；像电子表格一样编辑单元格不在本库范围内。

**Arc API 要点**：
| Arc 属性 | 类型 / 默认值 | 建议 Avalonia 属性（类型） |
|---|---|---|
| `rows` / `rowKey`（必填） | `T[]` / 字段名或 `(row) => string` | `ItemsSource`（IEnumerable）/ `RowKeySelector`（`Func<object, string>`） |
| `columns`（必填） | `{ key, label, sortable?, render?, numeric?, width? }[]`，默认可排序，全为数字时识别为数值列 | `Columns`（`AvaloniaList<DataTableColumn>`：Key、Header、IsSortable、CellTemplate、IsNumeric、Width） |
| `caption` / `emptyMessage` | `"Data table"` / `"No rows to show"` | `Caption`（string，同时是表的可访问名称）/ `EmptyMessage`（string） |
| `defaultSort` / `onSortChange` | `{ key, direction }` | `Sort`（`TableSort?`：Key、Direction，双向）+ `SortChanged` 事件 |
| `selectable` / `selectedKeys` / `defaultSelectedKeys` / `onSelectionChange` | `false` / `string[]` | `IsSelectable`（bool）+ `SelectedKeys`（`IList<string>`，双向）+ `SelectionChanged` 事件 |
| `itemName` | `{ one: "row", other: "rows" }` | `ItemName`（`PluralNoun`，计数行如 "6 projects"） |

**交互与键盘**：
- ←/→ 在可排序表头之间移动；↑/↓ 在行复选框之间移动，从 Select all 向下进入行；Home / End 到首末表头或行复选框。
- Enter / Space 按表头排序或切换复选框；Shift + 点击选择一段连续的行；Escape 清空选择。

**可访问性**：
- Arc 使用原生 table（caption、带 scope 的表头、可排序列的 aria-sort）；Avalonia 提供 Table、HeaderItem、DataItem 角色的 AutomationPeer，排序状态写入表头的 `ItemStatus`。
- 排序按钮名称形如 "Sort by Budget, currently ascending"；Select all 复选框有混合态（`ToggleState.Indeterminate`）；排序与选择变化经 Status 区域播报。

**动效**：
- 排序变化时行在弹簧上滑到新位置（P19；morph），排序箭头翻转（snappy）。
- 选择计数滚动到新值（P14；smooth），Clear 带短模糊淡入（P2；standard + enter，BlurSubtle）。
- Reduced：立即重排，计数改为淡入淡出；Off：立即终态。

**Avalonia 实现要点**：
- 不派生 DataGrid：表头行加 ItemsControl 行容器，行重排用 Core 的 LayoutAnimator（FLIP），排序在客户端对传入的行执行。
- 复选框复用 Toggles 大类的 Checkbox；单元格内容用 `CellTemplate`（对应 Arc 的 `render`，用于徽标、头像、格式化数字）。
- 宽布局在表内横向滚动；小于 620px 时每行折成两行，表头变成可横向滚动的排序按钮条并固定 Select all（断点由 S5 判断）。

**AtomUI 参考**：DataGrid（参考）（仅供学习行为与边界，不复用其实现）

**风险与未决**：
- 主数据列了 P6，但 Arc 文档没有描述高度动画（推测用于计数行的出现与收起），需对照源码确认。
- 行不虚拟化且每行一个布局动画，长列表必须配合 Pagination；`T` 泛型行与 `RowKeySelector` 在 XAML 与编译绑定（AOT）下的表达方式需在规格中确定。
- 复核结论（汇总评审）：移除 P6，已同步到[组件总清单](../overview.md)。

### 4.2 TreeView（Tree view）
`slug: tree-view` · 阶段 P9 · Arc 依赖：无 · 基类：TemplatedControl（扁平虚拟化行）

**Arc 行为摘要**：键盘可导航的文件树，展开带动画，选中高亮在行间滑动。用于用户展开和选择的文件、文件夹与嵌套类别，常放在 CodeBlock 或详情视图旁边，由选择驱动内容。少量可折叠的内容区块用 Accordion，显示页面所在路径用 Breadcrumb。

**Arc API 要点**：
| Arc 属性 | 类型 / 默认值 | 建议 Avalonia 属性（类型） |
|---|---|---|
| `nodes`（必填） | `{ id, label, children?, icon? }[]`，有 children 即为文件夹 | `ItemsSource`（继承）+ `ItemTemplate`（TreeDataTemplate），节点含 Id、Label、Children、Icon，id 在整棵树中唯一 |
| `defaultExpandedIds` / `expandedIds` / `onExpandedChange` | `string[]` / `[]` | `ExpandedIds`（`IList<string>`，双向，与容器的 `IsExpanded` 同步）+ `ExpandedIdsChanged` 事件 |
| `onSelect` | `(node) => void`，点击或激活时 | `SelectedItem`（继承）+ `ItemInvoked` 事件 |
| `aria-label` | `"File tree"` | `AutomationProperties.Name`（默认取本地化的 "File tree"） |

**交互与键盘**：
- ↓/↑ 到下一个或上一个可见行；→ 展开文件夹，已展开时进入第一个子项；← 折叠已展开的文件夹，否则回到父级；Home / End 到首末可见行。
- Enter / Space 选中该行，并切换文件夹的展开状态。

**可访问性**：
- Tree / TreeItem 角色，提供层级、`PositionInSet` / `SizeOfSet`、展开状态（`IExpandCollapseProvider`）与选中状态（`ISelectionItemProvider`）。
- roving tabindex 让只有一行在 Tab 序列中；图标、箭头与分支线为 Raw。

**动效**：
- 展开文件夹时空间增长（P6；smooth），子项交错进入（P19；morph + StaggerItem）；收起时行平滑折叠。
- 选中高亮在行间滑动（P7；morph），箭头以 snappy 弹簧旋转。
- Reduced：行立即显示或隐藏；Off：立即终态。

**Avalonia 实现要点**（已实现）：
- 派生 TemplatedControl 而不是 Avalonia TreeView：可见节点拍平成一列，由 `Tables/_SharedA/TablesTreeRowsPanel` 按有效视口虚拟化（自身 ScrollViewer 或页面滚动都可），行是池化、自绘的 `TreeViewRow`；上千个展开节点只实例化约一屏的行。嵌套容器无法虚拟化，展开动画也会让整棵树重新布局，所以不用嵌套 TreeViewItem 容器。
- 节点来源：内联 `TreeViewItem`（StyledElement：Header、Icon、IsExpanded 双向、Items）或 `ItemsSource` + `ChildrenSelector` / `HeaderSelector` / `IconSelector`；`SelectedItem` 双向（选中隐藏节点会展开其祖先）；事件 `SelectionChanged`、`ItemInvoked`（Arc onSelect）、`ItemExpanded` / `ItemCollapsed`；方法 `Expand`、`Collapse`、`ToggleExpanded`、`ExpandAll`、`CollapseAll`、`ScrollIntoView`、`FocusItem`、`IsItemExpanded`；`ShowIndentGuides`、`ShowIcons`。
- 动效：面板按 key 比较前后两次的可见行，新增段每行高度 0→38 走 smooth 弹簧（P6），透明度 fast + enter、x −6→0 按 StaggerItem 交错（最多 7 级）；移除段的行作为“幽灵行”留在原处收起，instant 淡出、x −4；只有视口附近的段参与动画（每段最多 48 行）。选中填充是面板上的 `GlidingIndicator`（morph），行随展开移动时跟随；箭头 snappy 旋转，文件夹图标弹出替换（无模糊）；分支线随行进入从上向下生长。Reduced / Off 立即完成。
- 键盘：↑/↓、Home/End、PageUp/PageDown、→/←、Enter/Space、键入跳转（S9）；只有焦点行 IsTabStop，焦点行在虚拟化时保持实例化；行的自动化角色为 TreeItem（ExpandCollapse、SelectionItem、PositionInSet / SizeOfSet）。焦点环在键盘焦点时绘制（Arc 原版为 outline none）。Arc 没有复选框，未实现。

**AtomUI 参考**：TreeView（仅供学习行为与边界，不复用其实现）

**风险与未决**：
- 已解决：Avalonia TreeView 键位与 Arc 不一致的问题不存在（自实现键盘）；`ExpandedIds` 未做成集合属性，数据节点的展开状态通过方法与事件控制，内联节点用 `TreeViewItem.IsExpanded`。
- 与 Arc 不同：行虚拟化（Arc 不虚拟化）；收起中的文件夹再次展开时，新行从“幽灵行”当前高度继续生长，而不是反向播放原动画。

### 4.3 ReorderableList（Reorderable list）
`slug: reorderable-list` · 阶段 P7 · Arc 依赖：无 · 建议基类：ItemsControl

**Arc 行为摘要**：竖向列表，拖动抓手或在抓手上按方向键即可重排。用于由用户决定顺序的短列表，例如优先级或播放列表，并且必须同时支持键盘与拖动。顺序来自某一列时用 SortableDataTable，行需要快捷操作而不是重排时用 SwipeActions。

**Arc API 要点**：
| Arc 属性 | 类型 / 默认值 | 建议 Avalonia 属性（类型） |
|---|---|---|
| `items`（必填） | `{ id, title, detail? }[]`，当前顺序 | `ItemsSource`（`IList`，项含 Id、Title、Detail） |
| `onReorder`（必填） | `(items) => void`，拖动或键盘移动后给出新顺序 | `Reordered` 事件（Item、OldIndex、NewIndex）；源为可写集合时控件直接移动项 |
| `label` | `"Reorder items"` | `AutomationProperties.Name`（默认本地化） |

**交互与键盘**：
- 抓手上 ↑/↓ 把该项上移或下移一位，移动后焦点仍在被移动项的抓手上。
- 只有抓手能开始拖动，行内文字保持可选。

**可访问性**：
- 每个抓手是 Button，名称形如 "Reorder Ship pricing page. Position 1 of 3. Use arrow keys."，位置变化后名称随之更新。
- 键盘移动后焦点保持在原项的抓手上，不跳回列表开头。

**动效**：
- 按下时行轻微抬起（P1；按下 instant，松开 snappy）。
- 拖动中行跟随指针（P12），相邻行在弹簧上让位（P19；morph）。
- 位置序号按行移动的方向滚动（P14；smooth）。
- Reduced：保留指针跟随，但行落位立即完成且不抬起；Off：同 Reduced。

**Avalonia 实现要点**：
- 抓手 36px，由 S10 的 `DragTracker` 在抓手上独占指针（等价 touch-action none），行的其余区域照常滚动页面；行填满宽度，最小高 56px。
- 拖动中的行用 RenderTransform 平移并提升 ZIndex，邻行位移用 LayoutAnimator（FLIP），放下时以弹簧落位。
- 受控语义：Arc 只回调新顺序，由调用方保存；Avalonia 中 `ItemsSource` 为 `ObservableCollection` 时直接 `Move`，否则只发事件。

**AtomUI 参考**：TabReorder、DataGrid 行拖拽（参考）（仅供学习行为与边界，不复用其实现）

**风险与未决**：
- 不虚拟化且每行一个布局动画，只适合短列表；拖到容器边缘时的自动滚动 Arc 未定义，是否实现待定。
- 抓手名称更新在 Avalonia 读屏中是否会被朗读需验证，必要时补一个 Polite 区域播报新位置。

### 4.4 FilterToolbar（Filter toolbar）
`slug: filter-toolbar` · 阶段 P6 · Arc 依赖：无 · 建议基类：TemplatedControl

**Arc 行为摘要**：一行可移除的筛选 chip，有筛选时出现 Clear all，可选的 Add filter 打开"先选字段、再选值"的两步菜单；其中的 FilterMenu 也可以单独使用。用于表格和列表上方的分面筛选，常与 SortableDataTable、Pagination 组成完整列表页。固定的一组开关用 ChipGroup，自由文本搜索用 SearchField，表单里一个字段取多个值用 MultiSelect。

**Arc API 要点**：
| Arc 属性 | 类型 / 默认值 | 建议 Avalonia 属性（类型） |
|---|---|---|
| `filters`（必填） | `{ id, label, value? }[]` | `Filters`（`IList<FilterChip>`） |
| `onRemove`（必填）/ `onClearAll` | `(id) => void` / `() => void` | `FilterRemoved` 事件（Id）/ `ClearAllRequested` 事件（有筛选时显示 Clear all） |
| `addFilter` | `{ fields, onAdd, label?, align? }` | `Fields`（`IList<FilterField>`，设置后显示 Add filter）+ `FilterAdded` 事件 + `AddFilterLabel`（string）+ `MenuAlignment`（Start、End，默认 End） |
| `children` | ReactNode | `Actions`（object，Add filter 旁的额外操作） |
| `label` | `"Active filters"` | `AutomationProperties.Name`（默认本地化） |
| FilterMenu：`fields` / `onSelect` / `active` / `label` / `align` | 字段含 options（value、label、hint、icon）/ 回调 / `[]` / `"Add filter"` / `"end"` | 独立控件 `FilterMenu`：`Fields`、`FilterSelected` 事件、`ActiveFilters`、`Label`、`Alignment`（空间不足时翻转） |

**交互与键盘**：
- 触发器上 ↓/↑ 打开菜单并定位到首项或末项；菜单内 ↑/↓ 移动，Home / End 到首末项。
- →/← 进入字段的值列表或返回字段列表；字母键 typeahead；Escape 关闭菜单并把焦点还给触发器。

**可访问性**：
- 工具条是 Group；菜单面板是有名称的对话框（Pane 角色），内含可勾选的菜单项（MenuItem 加 `IToggleProvider`）。
- 移除按钮名称形如 "Remove Status: Open"，移除后焦点移到相邻 chip 或 Add filter 触发器；增删经 Status 区域播报。

**动效**：
- chip 打开或关闭自己的槽位，邻居随之滑动（P19、P5；morph）；值在原位形变（P3；standard + enter，BlurSoft）。
- Add filter 触发器形变为菜单表面（P9；展开带轻微回弹，收回 smooth），字段与值两步之间方向滑动（P25；smooth，位移 8px）。
- Reduced：形变与滑动改为立即变化加短淡入淡出（fast 160ms）；Off：立即终态。

**Avalonia 实现要点**：
- 菜单面板经 S8 锚定浮层呈现，宽 min(16rem, 视口宽减边距)，列表最高 min(20rem, 55vh)，对齐边没有空间时翻转；字段与值列表的导航与 typeahead 走 S9。
- chip 宽度与菜单尺寸测量走 S5（Arc 为此用了多个 ResizeObserver）；选项不虚拟化，值列表应保持简短。
- 小于 520px 时工具条堆成一列，操作靠末端对齐；每个字段只保留一个 chip（`FilterAdded` 给出以字段 id 为键的 chip，替换同 id 的旧 chip）。

**AtomUI 参考**：—（AtomUI 无同类控件）

**风险与未决**：
- 触发器到菜单表面的形变跨越宿主窗口与 Popup，需先在 Overlays 规格中验证 ScopeAwareOverlayLayer 上的表面形变方案。
- 菜单是否复用 DropdownMenu 的 MenuSurface（阶段 P5）待定，主数据未列依赖。

### 4.5 CodeBlock（Code block）
`slug: code-block` · 阶段 P2 · Arc 依赖：copy-button · 建议基类：TemplatedControl

**Arc 行为摘要**：源码查看器，带文件名头部、轻量语法高亮、复制按钮与可选折叠。用于文档代码片段与安装命令，长文件用 `maxLines` 折叠到几行并提供 Show all。只复制一行值（如 API key）时单独用 CopyButton；需要支持大量语言时用完整的高亮库。

**Arc API 要点**：
| Arc 属性 | 类型 / 默认值 | 建议 Avalonia 属性（类型） |
|---|---|---|
| `code`（必填） | string | `Code`（string，显示与复制的内容） |
| `filename` | 默认 "Source code" | `FileName`（string） |
| `language` | `"tsx"` | `Language`（string，头部标签与高亮模式） |
| `maxLines` | 无 | `MaxLines`（int?，超出时折叠并显示 Show all） |

**交互与键盘**：
- Tab 依次到复制按钮、可滚动的源码区与展开切换按钮。
- Enter / Space 复制代码或切换展开状态。

**可访问性**：
- 整块是带名称的区域（`LandmarkType=Region`），名称形如 "hello.ts source code"；源码区可聚焦，便于键盘滚动。
- 展开切换按钮提供 `IExpandCollapseProvider`（对应 aria-expanded 与 aria-controls）；复制反馈由 CopyButton 负责。

**动效**：
- 展开或折叠时源码高度弹簧变化（P6；smooth）；代码更新时新内容原地淡入上移（P2；standard + enter，BlurSubtle）。
- 切换按钮的文字滚动切换（P3；standard + enter，BlurSoft），箭头旋转（snappy）。
- Reduced：高度直接到位，文字原地替换不移动；Off：立即终态。

**Avalonia 实现要点**（已实现）：
- 高亮器 `CodeBlockTokenizer`：手写的单次扫描，与 Arc 的 token 正则（注释、模板 / 引号字符串、`\b` 数字、标识符、标点）及分类规则（关键字与字面量、内置类型、JSX/HTML 标签、调用、属性、JSON 键）等价；不用 Regex、不产生中间字符串，只在 Code / Language 变化时运行一次。扩展了 `cs` / `csharp` 关键字与类型、`xml` / `xaml` / `axaml` / `svg` 标签（含命名空间前缀）与特性。
- 文本：`CodeBlockText`（SelectableTextBlock 子类）重写 CreateTextLayout，用自定义 ITextSource 以二分查找给出着色 run，整段代码只有一个 TextLayout，没有 Inline 或逐 token 控件；保留选择与 Ctrl+C。语法色是 Own Token（明暗各一套 oklch，与 accent 无关），字体用 `MonoFontFamily`，制表符按 2 空格显示。
- 折叠：`IsCollapsible` + `MaxLines`（默认 12）+ `IsExpanded`（双向）；`CodeBlockViewport` 报告折叠高度但按全高排列，外层 `SpringSizeDecorator`（Height、smooth、ClipToBounds）负责弹簧与裁剪，代码变化引起的高度变化同样走弹簧。切换按钮两种文字预留宽度、BlurSwap 换字、箭头 snappy 旋转 180°，并提供 ExpandCollapse 自动化模式。
- 头部：文件名 BlurSwap，名称槽宽度走 morph 弹簧（`CodeBlockHeaderLabel`），长名省略号截断；新代码上浮 4px 淡入（旧代码不做淡出）。`ShowLineNumbers` 为扩展（行号列不随横向滚动）；控件宽度小于 420px 时进入紧凑布局（Arc 按视口判断）。

**AtomUI 参考**：GalleryCodeViewer（参考）（仅供学习行为与边界，不复用其实现）

**风险与未决**：
- 已解决：xml 与 csharp 模式已作为扩展实现；长源码不再生成大量 Run（单个 TextLayout + 自定义文本源）。
- 注意：模板里用 Token 标记扩展直接写在元素上的属性无法被 `/template/` 样式覆盖，需要随状态变化的尺寸（紧凑布局）写在主题样式里。

### 4.6 JsonViewer（JSON viewer）
`slug: json-viewer` · 阶段 P9 · Arc 依赖：无 · 建议基类：TemplatedControl

**Arc 行为摘要**：可折叠的 JSON 树，按类型着色，搜索时自动展开匹配的分支，长数组或长对象分页加载，每行可复制值或路径。用于开发者面板里检查 API 响应、Webhook 负载、日志与配置。整段 JSON 作为可复制的源码展示用 CodeBlock，文件夹等非 JSON 层级用 TreeView。

**Arc API 要点**：
| Arc 属性 | 类型 / 默认值 | 建议 Avalonia 属性（类型） |
|---|---|---|
| `data`（必填） | `unknown`，任意类 JSON 值 | `Data`（`JsonNode?`） |
| `rootName` / `label` | `"root"` / `"JSON"` | `RootName`（string，路径前缀，如 root.users[0].name）/ `AutomationProperties.Name` |
| `defaultExpandDepth` / `expanded` / `defaultExpanded` / `onExpandedChange` | `1`（只展开根）/ `string[]` | `DefaultExpandDepth`（int）+ `ExpandedPaths`（`IList<string>`，双向）+ `ExpandedPathsChanged` 事件 |
| `searchable` / `query` / `defaultQuery` / `onQueryChange` | `true` / `""` | `IsSearchable`（bool）+ `Query`（string，双向） |
| `pageSize` | `50` | `PageSize`（int，长分支每页显示的子项数） |
| `copyable` / `onCopy` | `true` | `IsCopyable`（bool）+ `Copied` 事件（Kind 为 Value 或 Path、Path、Text） |
| `onSelect` / `showPath` / `maxHeight` | 无 / `true` / `420` | `CurrentRowChanged` 事件（Path、Value、Type）/ `IsPathVisible`（bool）/ `MaxHeight`（继承，树滚动区高度） |

**交互与键盘**：
- ↓/↑、→/←、Home / End 与 TreeView 相同；Enter / Space 切换分支，或在 Show more 行加载下一页。
- Cmd/Ctrl + C 复制当前行的值，加 Shift 复制路径（有选中文本时不拦截）；/ 聚焦搜索框，框内 Enter / Shift + Enter 跳到下一个或上一个匹配，Escape 清空，↓ 把焦点移入树。

**可访问性**：
- WAI-ARIA tree 模式：Tree / TreeItem 角色，带层级、`PositionInSet` / `SizeOfSet`、展开与选中状态；同一时间只有一行可 Tab，行名称读出键与值，或类型与项数。
- 匹配计数是 Polite 区域，复制成功或失败经 Status 播报；行内复制按钮不在 Tab 序列中，键盘用 Cmd/Ctrl + C 或路径栏的复制按钮。

**动效**：
- 每行各自展开高度，展开分支、加载分页与全部展开共用同一动效（P6；smooth）。
- 当前行高亮以共享布局在行间滑动（P7；morph），树滚动以弹簧移到行落定后的位置（P30；smooth）。
- 复制图标换成对勾或叉，带小缩放与模糊（P26、P2；缩放 snappy，透明度与模糊 fast，BlurSubtle）。
- Reduced 或可见行超过 400 时行变化立即完成，Reduced 还让滚动与高亮直接到位；Off：立即终态。

**Avalonia 实现要点**（已实现）：
- 输入：`Json`（字符串，解析失败时显示 `ParseError`）、`Value`（JsonNode）或 `Element`（JsonElement，内部克隆），统一为 JsonDocument；节点按需惰性创建，路径规则与 Arc 相同（`root.users[0].name`，非标识符键为 `["..."]`），分支复制为两空格缩进 JSON。
- 与 TreeView 共用 `TablesTreeRowsPanel`：扁平行、按视口虚拟化、行高 smooth 弹簧展开收起、当前行高亮 smooth 滑动；可见行超过 400 时立即完成。保留 Arc 的分页（`PageSize` 50，“Show N more”行）。
- 搜索对键与原始值做大小写不敏感匹配（最多 2000 个），自动展开所在分支，搜索中手动收起的分支记到查询变化为止；Enter / Shift+Enter、Escape、↓ 进入树，树中 / 聚焦搜索，Ctrl/Cmd+C 复制当前值（加 Shift 复制路径）；树滚动用 MotionValue smooth 弹簧滚到行落定后的位置（P30），滚轮会打断。
- 行内复制按钮为自绘（不在 Tab 序列，悬停或当前行显示，图标弹成对勾 / 叉），路径栏复制按钮可聚焦；`MaxTreeHeight` 对应 Arc 的 maxHeight，`ExpandedPaths` 双向，`CurrentRowChanged` 对应 onSelect；宽度不大于 420px 时缩进 12px、键最多 40%、隐藏路径类型。

**AtomUI 参考**：TreeView（参考）（仅供学习行为与边界，不复用其实现）

**风险与未决**：
- 搜索仍在 UI 线程上全量遍历，多 MB 负载建议调用方防抖 `Query`。
- 只接受 System.Text.Json 的输入（AOT 友好），不支持任意 CLR 对象；行内复制按钮没有 Arc 的 title 提示。

## 5. 实现顺序与依赖
- 阶段 P2：CodeBlock。硬依赖 CopyButton（阶段 P1）；Demo 应用用它展示示例代码，所以排在早期；所需原语 P2、P3、P6 已随 Button 与 Calendar 落地。
- 阶段 P6：FilterToolbar。依赖 S8 锚定浮层与 S9（阶段 P1 落地，阶段 P5 的菜单使其成熟）、P9 表面形变（首个使用者 ExpandableCard，阶段 P4）与 P25（随 Calendar）；菜单是否复用 DropdownMenu 的表面待定。
- 阶段 P7：ReorderableList。依赖 P12 拖拽物理（首个使用者 UserMenu，阶段 P5）与 S10 手势，与 Media、Gestures、Sliders 的拖拽组件同阶段。
- 阶段 P9：先 TreeView（沉淀树形键盘与 P7 行高亮），再 JsonViewer（复用树形键盘，加 P26 与 P30，后者首个使用者为 Tabs，阶段 P4），SortableDataTable 复用 Checkbox（阶段 P3）。
- 跨大类：SortableDataTable、FilterToolbar 与 Pagination 组成完整列表页；TreeView 常与 CodeBlock 搭配；JsonViewer 的搜索框可参考 SearchField。

## 6. 相关文档
- [组件总清单](../overview.md) · [规格模板](../spec-template.md)
- [动效原语目录](../../architecture/motion/primitives-catalog.md) · [Token 目录](../../architecture/theming/token-catalog.md)
- [浮层与输入](../../architecture/overlays-and-input.md) · [路线图](../../roadmap/overview.md)
- 相邻大类：[按钮](../buttons/overview.md) · [导航](../navigation/overview.md) · [选择器](../selects/overview.md) · [动态与会话](../activity/overview.md)
