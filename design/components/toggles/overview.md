# 开关与选项（Toggles）

> Arc 的开关与选项大类覆盖二元开关、单选与分段选择，每个控件都用一个独立的指示物（拇指、勾、圆点、选中环、胶囊）以弹簧表达状态。

## 1. 大类定位
- 解决在可见选项中即时做选择：立即生效的设置（Switch）、提交前确认的独立勾选与半选（Checkbox）、带说明的单选（RadioGroup、RadioCards）、视图切换（SegmentedControl）、定价页的计费周期（BillingToggle）。
- 边界：选项多或需要搜索时用 [选择器](../selects/overview.md)，频繁切换的筛选芯片用其中的 ChipGroup；切换整块内容面板用 [导航](../navigation/overview.md) 的 Tabs；明暗切换用 [按钮](../buttons/overview.md) 的 ThemeSwitcher。
- 共同特征：颜色来自 `ControlOn`、`ControlGlyph`、`ControlTrack`、`ControlThumb` 系列 Token；组内只有一个 Tab 停靠点，方向键选择并移动焦点；指示物在选项之间滑动，尺寸变化时直接对齐、不重放动画。

## 2. 组件一览

| 组件 | 类名 | Arc 页面 | 阶段 | Arc 依赖 | 建议基类 | 原语 |
|---|---|---|---|---|---|---|
| Radio cards | `RadioCards` | [radio-cards](https://uiarc.dev/components/radio-cards) | P3 | 无 | SelectingItemsControl | P7 P26 |
| Billing toggle | `BillingToggle` | [billing-toggle](https://uiarc.dev/components/billing-toggle) | P2 | animated-counter（AnimatedCounter） | TemplatedControl | P2 P5 P7 P14 |
| Checkbox | `Checkbox` | [checkbox](https://uiarc.dev/components/checkbox) | P3 | 无 | Avalonia ToggleButton | P1 P15 P16 P26 |
| Radio group | `RadioGroup` | [radio-group](https://uiarc.dev/components/radio-group) | P3 | 无 | SelectingItemsControl | P7 P26 |
| Switch | `Switch` | [switch](https://uiarc.dev/components/switch) | P0 | 无 | Avalonia ToggleButton | P24 |
| Segmented control | `SegmentedControl` | [segmented-control](https://uiarc.dev/components/segmented-control) | P3 | 无 | SelectingItemsControl | P7 |

## 3. 共享约定
- **选择控件 Token**（Arc foundation 对明暗与 8 种 accent 分别调校）：`ControlOn` 为选中填充，`ControlGlyph` 为其上的勾或圆点，`ControlTrack` 为未选中轨道（悬停加深一档），`ControlThumb` / `ControlThumbOn` 为拇指；拇指在两种状态下都保持白色以区别于轨道，中性暗色下反转为浅轨深拇指。轨道悬停色与拇指阴影若未进入 [token-catalog](../../architecture/theming/token-catalog.md)，作为控件 own token。
- **焦点**：指针聚焦不画焦点环；键盘聚焦显示 2px、偏移 2px 的 accent 轮廓（S7）。
- **命中区**：Checkbox 命中区为 `ControlSizeMD` 正方形（绘制框 18px）；Switch 最小高度 `ControlSizeMD`，整段标签都可点击。
- **组控件**（RadioCards、BillingToggle、RadioGroup、SegmentedControl）：只有已选项可 Tab 聚焦（`KeyboardNavigation.TabNavigation=Once`），方向键选中并聚焦相邻项、首尾循环并跳过禁用项，Home / End 到首尾（Arc 的 RadioGroup 沿用原生单选、没有 Home / End，这里统一提供）；一个共享指示物以弹簧移到选中项（P7），尺寸变化时立即对齐不重放。实现集中在 `Toggles/_Shared`（`TogglesGroupHost`、`TogglesItem`、默认 `SelectedValueBinding` 取选项的 `Value`）。
- **受控模型**：Checkbox / Switch 的 `checked` + `onCheckedChange` 映射为 `IsChecked` + `IsCheckedChanged`（Checkbox 另有 `CheckedState` 与 `CheckedStateChanged`）；组控件的 `value` + `onValueChange` 映射为双向 `SelectedValue` / `SelectedItem` 加 `SelectionChanged`（BillingToggle 另有 `Value` + `ValueChanged`）；选项可来自 `ItemsSource` 或内联的选项对象；`name` 原生表单提交不映射。
- **可访问性**：指示物（拇指、圆点、选中环、胶囊）一律对辅助技术隐藏，状态由控件自身的 peer 报告；选中不只靠颜色（环加圆点、勾形）。
- **动效降级**：Reduced 下指示物直接到终态，去掉拉伸与按下缩放；Off 同上并关闭颜色过渡。

## 4. 组件

### 4.1 RadioCards（Radio cards）
`slug: radio-cards` · 阶段 P3 · Arc 依赖：无 · 建议基类：SelectingItemsControl

**Arc 行为摘要**：可选的选项卡片，带滑动的选中环与价格、描述槽位，行为与单选组一致。用于每个选项都需要描述、价格或预估才能决定的场景（套餐、配送、区域、规格），以及需要说明不可用原因的选项。纯文字短选项用 RadioGroup；两到四个即时切换的视图用 SegmentedControl；长列表用 Select 或 Combobox。

**API（已实现）**：

| Arc 属性 | 类型 / 默认值 | Avalonia 属性（类型） |
|---|---|---|
| `options`（必填） | `{ value, label, description?, meta?, icon?, disabled?, disabledReason? }[]` | `ItemsSource` 或内联项，均为 `RadioCardOption`（Value、Label、Description、Meta、Icon、IsDisabled、DisabledReason）；Icon 为 `Geometry` 时画 18px Lucide 图标 |
| `value` / `defaultValue` / `onValueChange` | `string \| null` / `null` | `SelectedValue`（双向，取选项的 Value）/ `SelectedItem`（双向）/ `SelectionChanged` |
| `layout` | `"grid" \| "list"` / `"grid"` | `Layout`（RadioCardsLayout：Grid / List） |
| `minColumnWidth` | `180` | `MinColumnWidth`（double，180） |
| `required` / `disabled` | 无 / `false` | `IsRequired`（bool，写入 IsRequiredForForm）/ `IsEnabled`（继承） |

**交互与键盘**：
- Tab 聚焦已选卡片，没有则聚焦第一个可用卡片；方向键移到上一个或下一个可用卡片并选中、循环，从右到左布局时左右互换；Home / End 选中首尾；Enter / Space 选中当前卡片。
- grid 布局按 `MinColumnWidth` 自动填充列（`repeat(auto-fill, minmax(min(100%, 180px), 1fr))`，间距 10px），窄容器降为一列；list 布局整行排列（间距 8px）、meta 在行尾，长描述换行不截断。

**可访问性**：
- 卡片为 RadioButton 类型 peer（ISelectionItemProvider），组的名称用 `AutomationProperties.Name`。
- 卡片名称取标签、HelpText 取描述，禁用且有 DisabledReason 时 HelpText 与可见描述都换成原因。

**动效**：
- 一个选中环（1.5px `ControlOn`、20px 圆角）以 morph 弹簧移到选中卡片的位置与尺寸，首次放置时 fast 淡入；尺寸变化时直接对齐（P7，Core `GlidingIndicator`）。
- 6px 指示圆点从 0 以 snappy 缩放进入（P26）；卡片背景与边框颜色 fast 过渡。
- Reduced：环与圆点直接到终态，无颜色过渡；Off：同上。

**Avalonia 实现要点（已实现）**：
- 卡片是内部容器 `RadioCard`（共享 `TogglesItem` 基类：松开时选中、Space / Enter 选中、`:selected` / `:pressed`），布局由内部 `RadioCardsPanel` 按列宽计算，同一行卡片拉伸到最高者，价格对齐。
- 选中环是盖在卡片上的单一覆盖层，矩形内缩半个描边以与 CSS 边框重合；选中底色是 `ControlOn` 4% 覆盖层（与 `color-mix(… 4%, surface)` 等效）。
- 漫游焦点、方向键与指示物跟随由 Toggles `_Shared` 的 `TogglesGroupHost` 统一处理，四个组控件共用。

**AtomUI 参考**：RadioButtonGroup（仅供学习行为与边界，不复用其实现）。

**风险与未决**：
- Arc 的选中环是 0.42s、bounce 0.14，`GlidingIndicator` 只接受预设，使用 morph（bounce 0.16），差异不可察觉。
- Arc 卡片本身没有焦点环（依赖全局 outline），这里与其他控件一致画 2px 焦点环。

### 4.2 BillingToggle（Billing toggle）
`slug: billing-toggle` · 阶段 P2 · Arc 依赖：animated-counter（AnimatedCounter） · 建议基类：TemplatedControl

**Arc 行为摘要**：月付 / 年付切换，带节省徽标，价格滚动到新金额；同模块导出显示价格的 BillingPrice。用于定价页顶部的计费周期切换、随周期或套餐变化滚动的价格，以及带节省说明的小型套餐选择（如年付与终身）。与价格无关的视图切换用 SegmentedControl；需要描述或纵向布局用 RadioGroup；单个开关设置用 Switch。

**API（已实现）**：

| Arc 属性 | 类型 / 默认值 | Avalonia 属性（类型） |
|---|---|---|
| `value`（必填）/ `onValueChange`（必填） | `string`（仅受控） | `Value`（string?，双向，与 `SelectedValue` 同步）+ `ValueChanged`（BillingToggleValueChangedEventArgs：OldValue / NewValue） |
| `options` | Monthly 与 Yearly（徽标 "Save 20%"） | `ItemsSource` 或内联 `BillingToggleOption`（Value、Label、Badge、ActiveBadge）；都没有时用 `DefaultOptions` |
| `label` | `"Billing period"` | `AutomationProperties.Name`（默认 "Billing period"） |
| `size` | `"md" \| "lg"` / `"md"` | `Size`（BillingToggleSize：Medium 每项 34 / Large 42，字号 sm / base） |
| `--billing-accent`（CSS 变量） | 默认 `--success` | `BadgeAccent`（IBrush，默认 `Success`）；选中徽标底色与文字在代码中由它派生 |
| BillingPrice：`amount` / `currency` / `decimals` | `number` / `"$"` / `0` | 公开伴生控件 `BillingPrice`：`Amount`（decimal）/ `Currency`（string，"$"）/ `Decimals`（int） |
| BillingPrice：`was` / `period` | `number` / `string` | `WasAmount`（decimal?，高于 Amount 时才显示删除线）/ `Period`（string?） |

**交互与键盘**：
- Tab 只聚焦已选项；ArrowRight / ArrowDown 选中并聚焦下一项，ArrowLeft / ArrowUp 上一项，首尾循环；Home / End 选首尾。
- `Value` 未设置时像 Arc 一样把第一项显示为已选（并写回 Value）；先设 Value 后到达的选项会被正确匹配。

**可访问性**：
- 选项是 RadioButton 类型 peer；名称为"标签 + 当前徽标文本"（如 "Yearly Save 20%"）；拇指不进可访问性树；BillingPrice 的名称为 "$24, was $30 per month"。

**动效**：
- 一个拇指按选中项的 x 与宽度滑动，临界阻尼弹簧 visualDuration 0.34s、无回弹（内部 `BillingToggleThumb`，自带 MotionValue，因为该弹簧不在 GlidingIndicator 预设内）。
- 选中带徽标的选项时徽标以 standard 染上 `BadgeAccent`（亮 13% / 暗 18% 底色，文字亮色混 12% 前景、暗色直接用 accent），文本用 `ArcContentSwap` BlurSwap 交换；所有候选文本叠放预占宽度（P2）。
- BillingPrice 数字经 AnimatedCounter 滚动（P14），旧价用 `PresenceHost`（Fade + Collapse=Width，smooth）展开，周期文本在 `SpringSizeDecorator` 中 BlurSwap 原位交换；未选项按下内容缩放 0.97（fast）。
- Reduced：拇指直接到位，无按下缩放；Off：同上。

**Avalonia 实现要点（已实现）**：
- 派生 SelectingItemsControl，选项容器为内部 `BillingToggleItem`；默认选项在挂到逻辑树且没有任何选项时作为 ItemsSource 提供（因此内联选项需在挂载前添加）。
- 亮暗两套拇指阴影（含 `BorderSubtle` / `Border` 色的 1px 环）作为 own token `ThumbShadow`。

**AtomUI 参考**：Segmented（参考）（仅供学习行为与边界，不复用其实现）。

**风险与未决**：
- BillingPrice 作为同目录公开伴生控件（带 `BillingPriceToken`）实现；旧价宽度弹簧用 smooth（0.4s）近似 Arc 的 0.36s。

### 4.3 Checkbox（Checkbox）
`slug: checkbox` · 阶段 P3 · Arc 依赖：无 · 建议基类：Avalonia ToggleButton

**Arc 行为摘要**：状态精确清晰的二元选择，勾可以形变为半选横线再变回。用于需要提交确认的独立开关选择（如同意条款），以及用半选表示部分子项已选的父行。立即生效的设置用 Switch；单选用 RadioGroup；常切换的筛选用 ChipGroup。

**API（已实现）**：

| Arc 属性 | 类型 / 默认值 | Avalonia 属性（类型） |
|---|---|---|
| `label` | `string`（缺省时需 aria-label，回退名称 "Checkbox"） | `Content`（object，继承）；无内容时 `AutomationProperties.Name` 回退为 "Checkbox" |
| `description` | `string` | `Description`（string?，同时写入 HelpText） |
| `checked` / `defaultChecked` / `onCheckedChange` | `boolean \| "indeterminate"` / `false` | `IsChecked`（bool?，null 为半选，双向，继承）+ `CheckedState`（CheckboxState 枚举，双向，与 IsChecked 同步）+ `CheckedStateChanged`（旧 / 新状态）与继承的 `IsCheckedChanged` |
| `disabled` / `required` | `boolean` | `IsEnabled`（继承）/ `IsRequired`（bool，IsRequiredForForm） |
| `aria-invalid` | 无 | `IsInvalid`（bool，`:invalid`，未选中时方框边框为 `Danger`） |

**交互与键盘**：
- Space 切换（Enter 不切换，与 WAI-ARIA / Radix 一致）；点击半选状态时变为选中（Radix 行为）；`IsThreeState=true` 时改用 Avalonia 的三态循环。
- 标签与描述在框旁换行，标签首行与 18px 方框居中对齐（上边距 12.2px）。

**可访问性**：
- 派生 Avalonia CheckBox，得到 CheckBox 类型 peer + IToggleProvider（null 对应 Indeterminate）；描述进入 HelpText；绘制的勾不进可访问性树。

**动效**：
- 填充（`ControlOn`）从 0.6 以 snappy 缩放进入，透明度选中时 instant、取消时 fast（P26）。
- 勾路径以 snappy 绘制（P15）；切到半选时同一条三点路径以 morph 形变为横线（P16）；方框按下缩放 0.95（instant，松开走 CSS spring），边框颜色 fast。
- Reduced：所有状态变化立即应用，无按下缩放；Off：同上。

**Avalonia 实现要点（已实现）**：
- 派生 Avalonia CheckBox，覆写 `Toggle()`；命中区 44×44，绘制框 18px、圆角 5px、1px `BorderStrong`，禁用时只有方框半透明（与 Arc 一致）。
- 填充与勾由内部 `CheckboxMark` 一次 Render 画出：四个 MotionValue（填充缩放、淡入、描边长度、形变进度），勾与横线三点逐点插值（`CheckboxPathMorph`，两段线 + 圆头，零逐帧分配）。

**AtomUI 参考**：CheckBox（仅供学习行为与边界，不复用其实现）。

**风险与未决**：
- 路径形变在本地实现（Core 的通用 PathMorph 并行开发中），两者算法一致时可替换。

### 4.4 RadioGroup（Radio group）
`slug: radio-group` · 阶段 P3 · Arc 依赖：无 · 建议基类：SelectingItemsControl

**Arc 行为摘要**：从一组可见选项中选一个，单一高亮滑到所选行。用于两到六个需要描述的互斥选项（如套餐），以及需要原生提交的表单选择。简短的行内视图选项用 SegmentedControl；长列表用 Select；可多选时用 Checkbox。

**API（已实现）**：

| Arc 属性 | 类型 / 默认值 | Avalonia 属性（类型） |
|---|---|---|
| `label`（必填） | `string`（渲染为 fieldset 的 legend） | `Header`（派生 HeaderedSelectingItemsControl；为字符串时同时作为 `AutomationProperties.Name`） |
| `options`（必填） | `{ value, label, description? }[]` | `ItemsSource` 或内联 `RadioOption`（Value、Label、Description、IsDisabled）；也接受任意对象 |
| `value`（必填）/ `onValueChange`（必填） | `string` | `SelectedValue`（双向，取选项的 Value）/ `SelectedItem`（双向）/ `SelectionChanged` |

**交互与键盘**：
- 方向键在选项间移动选择并循环、跳过禁用项；Home / End 到首尾（Arc 原生单选没有，这里与其他组控件一致）；Tab 在已选项处进入与离开组；Space / Enter 选中焦点行。
- 行纵向堆叠并拉伸到列宽，长描述在窄屏上整齐换行。

**可访问性**：
- 选项为 RadioButton 类型 peer（ISelectionItemProvider），描述写入 HelpText；高亮与圆点不进可访问性树；组名来自 Header。

**动效**：
- 单一高亮（1px `ControlOn` 边框加 5% 填充）以 morph 滑到所选行（P7，`GlidingIndicator`）；新圆点从 0.4 以 snappy 弹入、fast 淡入，旧圆点缩小并 instant 淡出（P26）。
- 圆环按下缩放 0.88（instant，松开走 CSS spring），按下未选行时预览圆点 0.22；尺寸变化时高亮直接重新放置。
- Reduced：高亮与圆点直接到位，无按下缩放；Off：同上。

**Avalonia 实现要点（已实现）**：
- 行为内部容器 `RadioGroupItem`：1px `Border`、`RadiusControl`、内边距 12px；圆环 17px、圆点 7px（选中为 `ControlGlyph`）。
- 高亮画在行之上（Arc 是行边框之上、文字之下）；5% 填充覆盖文字的差异不可察觉，换来单一覆盖层。高亮填充由描边色在代码里派生（随 accent 变化）。
- Arc 的原生 radio 被隐藏后没有可见焦点环；这里为键盘焦点加了 2px 焦点环。

**AtomUI 参考**：RadioButtonGroup（仅供学习行为与边界，不复用其实现）。

**风险与未决**：
- 无。

### 4.5 Switch（Switch）
`slug: switch` · 阶段 P0 · Arc 依赖：无 · 建议基类：Avalonia ToggleButton

**Arc 行为摘要**：用于立即生效设置的触感开关：按下时拇指拉伸，松开后弹簧滑到另一侧。用于通知等一拨即生效的设置，以及每行一个开关的设置列表。等提交按钮确认的选择用 Checkbox；在具名选项间选择用 SegmentedControl；明暗切换用 ThemeSwitcher。

**Arc API 要点**：

| Arc 属性 | 类型 / 默认值 | 建议 Avalonia 属性（类型） |
|---|---|---|
| `label` | `string`（显示在开关内，未传 aria-label 时兼作名称） | `Content`（object）；为字符串时同时写入 `AutomationProperties.Name` |
| `checked` / `defaultChecked` / `onCheckedChange` | `boolean` / `false` | `IsChecked`（bool?，只用 true / false）/ `IsCheckedChanged` 事件 |
| `disabled` | `boolean` | `IsEnabled`（继承） |

**交互与键盘**：
- Space / Enter 切换；按住 Space 期间拇指保持拉伸，直到松开。
- 调用方的指针与按键处理是追加而非替换；整段标签都是触控目标。

**可访问性**：
- role="switch" 加 checked：Avalonia 没有 switch 控件类型，使用 Button 类型 peer + IToggleProvider。
- 只有图标没有标签时必须设置 `AutomationProperties.Name`。

**动效**：
- 轨道 42×24（内边距 3px），拇指 18px、行程 18px；按下时拇指向行程方向加宽 5px 并保持远端不动，宽度用 snappy（P24）。
- 松开后拇指位移用 0.3s、无回弹的弹簧；未经按下的切换（键盘或程序设置）播放一次 0.34s 横向拉伸关键帧（1 到 1.16 再回 1）。
- 选中填充 `ControlOn` 以 standard 淡入，拇指颜色同步过渡。
- Reduced：无拉伸，拇指立即移动，颜色过渡也关闭；Off：同上。

**Avalonia 实现要点**：
- Arc 的动效全是 CSS 过渡、无观察器与逐帧工作：Switch 作为 P0 试点，在 ControlTheme 中用 `Spring*Transition` 实现拇指位移与宽度，验证 S0.7（多控件共享主题实例安全、首帧不播动画）；拉伸关键帧与按住状态用少量代码驱动。
- 拇指阴影对应 Arc 的三层 control-thumb-shadow，用 Border 的 `BoxShadow`（BoxShadows 类型，支持多层）表达。

**AtomUI 参考**：ToggleSwitch（仅供学习行为与边界，不复用其实现）。

**风险与未决**：
- Arc 组件文档称松开时为 snappy，源码中位移是 0.3s 无回弹、宽度是 snappy，写作说明又把开关拇指列为 smooth 用例；需在 token-catalog 中统一（建议以源码为准）。
- 读屏会把 Switch 报为切换按钮而不是开关，这是 Avalonia 自动化模型的限制。

### 4.6 SegmentedControl（Segmented control）
`slug: segmented-control` · 阶段 P3 · Arc 依赖：无 · 建议基类：SelectingItemsControl

**Arc 行为摘要**：在少量相关视图间切换的一排按钮，单一选中胶囊在其间滑动。用于日、周、月这类两到五个短视图选项，以及即时生效的布局或模式切换。每个选项切换一整块内容面板时用 Tabs；表单中或选项需要描述时用 RadioGroup；单个开关设置用 Switch。

**API（已实现）**：

| Arc 属性 | 类型 / 默认值 | Avalonia 属性（类型） |
|---|---|---|
| `options`（必填） | `{ value, label, accessory? }[]`（accessory 如徽标，排在标签后） | `ItemsSource` 或内联 `SegmentedOption`（Value、Label、Accessory、IsDisabled）；也接受字符串 |
| `value`（必填）/ `onValueChange`（必填） | `string`；点击或方向键、Home、End 触发 | `SelectedValue`（双向）/ `SelectedItem`（双向）/ `SelectionChanged` |
| `label` | `string`（组的 aria-label） | `AutomationProperties.Name` |
| `onOptionIntent` | `(value) => void`，指针或焦点到达但尚未选择时触发 | `ItemIntent` 事件（SegmentedItemIntentEventArgs：Item、Value），鼠标进入与获得焦点时触发 |

**交互与键盘**：
- Tab 进入控件并落在已选段（无选中时第一段），再按一次离开；方向键选中并聚焦相邻段、首尾循环；Home / End 选首尾。
- 段不收缩也不换行，放不下时整行在边框内横向滚动（滚动条隐藏），有更多内容的一侧边缘 20px 渐隐；选中段始终完整滚入视野并留 20px 余量（standard 补间，首次与 Reduced 直接定位）。

**可访问性**：
- 段为 Button 类型 peer + IToggleProvider（对应 aria-pressed）；滑动胶囊不进可访问性树。

**动效**：
- 共享胶囊（`Surface` 填充、1px `Border`、`ShadowResting`、14px 圆角）以 morph 滑到选中段（P7，`GlidingIndicator`，画在标签之下、随内容滚动）。
- 段本身从不缩放或改变字重，只有胶囊移动，标签颜色 fast 从 `TextMuted` 变为 `Foreground`（悬停与按下同）。
- Reduced：胶囊立即移动，颜色无过渡；Off：同上。

**Avalonia 实现要点（已实现）**：
- 根：1px `Border`、`SurfaceMuted`、`RadiusControl`，内含 `ScrollViewer`；轨道内边距 3px、段间距 2px；段最小高度 `ControlSizeSM`、圆角 14px、左右内边距 12px。
- 边缘渐隐是 ScrollViewer 的 `OpacityMask`（复用一个 LinearGradientBrush，只改停靠点）。

**AtomUI 参考**：Segmented（仅供学习行为与边界，不复用其实现）。

**风险与未决**：
- 窄于 380px 视口时内边距 10px 的媒体查询未映射；`ItemIntent` 不做节流（与 Arc 一致）。
- 键盘焦点环画在段内侧 2px（Arc 的 outline 会被滚动容器裁掉）。

## 5. 实现顺序与依赖
- **P0**：Switch 作为试点，是 P24（拇指拉伸）的首个使用者，同时验证 ControlTheme 中 `Spring*Transition` 的写法（S0.7）与 Token 载体（S0.2）。
- **P2**：BillingToggle，硬依赖 Charts 大类的 AnimatedCounter（P1）；所需 P2、P5（Button，P0）、P7、P14（Calendar，P1）、P24（Switch，P0）均已就绪。
- **P3**：RadioGroup 先行，沉淀组控件的漫游焦点与单一指示物覆盖层；随后 RadioCards、SegmentedControl（P7）与 Checkbox（P15 来自 CopyButton P1，P26 来自 Avatar P1；P16 的首个使用者 IconMorph 也在 P3，需协调先后）。
- 跨大类：Messages 大类的 CookieConsent（P7）依赖 Switch；BillingPrice 依赖 [图表](../charts/overview.md) 的 AnimatedCounter。

## 6. 相关文档
- [组件总清单](../overview.md) · [规格模板](../spec-template.md) · [路线图](../../roadmap/overview.md)
- [原语目录](../../architecture/motion/primitives-catalog.md) · [Token 目录](../../architecture/theming/token-catalog.md)
- [字段通用结构](../../architecture/forms-and-fields.md) · [浮层与输入](../../architecture/overlays-and-input.md)
- 相邻大类：[选择器](../selects/overview.md) · [导航](../navigation/overview.md) · [按钮](../buttons/overview.md) · [图表](../charts/overview.md)
