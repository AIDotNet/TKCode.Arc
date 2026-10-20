# 特殊输入（SpecialInputs）

> Arc 的特殊输入大类处理有固定形状的值：有界数字、金额、电话、拖拽调值、标签列表、提及与快捷键，把格式化、校验与反馈内建在字段里。

## 1. 大类定位
- 解决值有结构或格式的输入：数量与价格（NumberField）、金额与币种（MoneyInput）、E.164 电话（PhoneInput）、检查器里的拖拽调值（ScrubInput）、自由短值列表（TagInput）、带结构化引用的正文（MentionInput）、键盘快捷键（ShortcutRecorder）。
- 边界：自由文本归 [文本输入](../text-fields/overview.md)；从固定列表取值归 [选择器](../selects/overview.md)；近似值与范围用 [滑块](../sliders/overview.md)；日期、时间、颜色归 [选取器](../pickers/overview.md)。
- 共同特征：值以结构化类型输出（数字、最小货币单位、E.164、字符串数组、带偏移的提及、规范化快捷键串）；显示层动画（滚动数字、键帽、标签进出）对辅助技术隐藏，由真实输入承载值；越界与非法输入用拉伸或抖动加文案反馈，从不只靠颜色。

## 2. 组件一览

| 组件 | 类名 | Arc 页面 | 阶段 | Arc 依赖 | 建议基类 | 原语 |
|---|---|---|---|---|---|---|
| Number field | `NumberField` | [number-field](https://uiarc.dev/components/number-field) | P6 | 无 | TemplatedControl | P2 P6 P12 P13 P14 |
| Money input | `MoneyInput` | [money-input](https://uiarc.dev/components/money-input) | P6 | 无 | TemplatedControl | P1 P3 P5 P10 P13 P14 |
| Phone input | `PhoneInput` | [phone-input](https://uiarc.dev/components/phone-input) | P6 | 无 | TemplatedControl | P3 P6 P7 P9 P26 |
| Scrub input | `ScrubInput` | [scrub-input](https://uiarc.dev/components/scrub-input) | P7 | 无 | TemplatedControl | P2 P12 P13 P23 |
| Tag input | `TagInput` | [tag-input](https://uiarc.dev/components/tag-input) | P6 | 无 | TemplatedControl | P6 P7 P19 P26 P27 |
| Mention input | `MentionInput` | [mention-input](https://uiarc.dev/components/mention-input) | P6 | 无 | TemplatedControl（内含 TextBox） | P6 P7 P10 |
| Shortcut recorder | `ShortcutRecorder` | [shortcut-recorder](https://uiarc.dev/components/shortcut-recorder) | P6 | 无 | TemplatedControl | P6 P19 P21 P26 |

## 3. 共享约定
字段通用结构以 [forms-and-fields](../../architecture/forms-and-fields.md) 为准，壳体与消息行沿用 [文本输入](../text-fields/overview.md) 的约定；本大类的偏差如下。
- **标签**：MoneyInput、PhoneInput、ShortcutRecorder 有 `hideLabel`（只留给读屏），映射为 `IsLabelVisible`（bool，默认 true）；ScrubInput 的标签本身是拖拽手柄（文本或 16px 图标）；MentionInput 没有可见标签，用 `AutomationProperties.Name` 或外部 `LabeledBy`。
- **尺寸**：只有 NumberField 有 `Size`（Small / Medium / Large，默认宽 164 / 196 / 228px，步进按钮 28 / 34 / 40px）；ScrubInput 为 Small 30px / Medium 36px（`ControlSizeSM`）；MoneyInput 的金额壳体约 76px 高、使用大字号；其余沿用 `ControlSizeMD` 壳体。
- **数值类**（NumberField、MoneyInput、ScrubInput）：role="spinbutton" 映射为 `AutomationControlType.Spinner` + IRangeValueProvider，valuetext 带前后缀、单位或币种；ArrowUp / ArrowDown 步进，Shift 走大步（NumberField 与 MoneyInput 另支持 PageUp / PageDown），Enter 提交或规整草稿（NumberField 与 ScrubInput 用 Escape 回退）；数字用等宽字形逐位滚动（P14），滚动副本对辅助技术隐藏。
- **越界反馈**：值向按压方向拉伸或横踢几像素后弹回，弹簧写成刚度与阻尼以便携带初速度（NumberField 由 morph 换算，连续推压逐次加大并封顶），被拒绝的操作伴随抖动与文案（P13）；Reduced 改为短暂的 `Warning` 着色或淡入淡出。
- **区域与格式化**：Arc 默认固定 `locale="en-US"` 以保证两端一致，映射为 `Culture`（CultureInfo，默认 en-US），格式化与解析走 S11；软键盘类型用 `TextInputOptions.ContentType`。
- **弹层列表**（MoneyInput 币种菜单、PhoneInput 国家列表、MentionInput 建议）：焦点留在输入或按钮上，列表采用 activedescendant 模式；Avalonia 侧用 S8 浮层加 S9 高亮导航，读屏报告方式遵循 [overlays-and-input](../../architecture/overlays-and-input.md)。
- **播报**：增删、越界、币种或国家变化、结果数量用 `AutomationLiveSetting.Polite` 的隐藏播报节点；校验错误沿用字段错误行（`Assertive`）。
- **受控与事件**：`value` + `onValueChange` 映射为双向 `Value` 属性加 `ValueChanged` 事件，事件参数携带 Arc 的 details 对象；`name` 生成的隐藏 input 与 `inputMode`、`autocomplete` 等网页提示不映射。
- **动效降级**：Reduced 去掉滚动、拉伸、抖动、布局移动与缩放，保留终态与淡入淡出；Off 直接到终态。

## 4. 组件

### 4.1 NumberField（Number field）
`slug: number-field` · 阶段 P6 · Arc 依赖：无 · 建议基类：TemplatedControl

**Arc 行为摘要**：带步进按钮的有界数字字段：数字以滚轮方式滚动，长按连发，可选拖动标签调值。用于座位数、件数、价格等精确数值，支持前缀与可复数化的后缀。近似位置比精确数字重要时用 Slider；电话号码、验证码等数字串用 Input。

**Arc API 要点**：

| Arc 属性 | 类型 / 默认值 | 建议 Avalonia 属性（类型） |
|---|---|---|
| `value` / `defaultValue` / `onValueChange` | `number` / `0` | `Value`（decimal，双向）+ `ValueChanged` 事件 |
| `min` / `max` | `0` / `Number.MAX_SAFE_INTEGER` | `Minimum` / `Maximum`（decimal） |
| `step` / `largeStep` | `1` / `step * 10` | `Step` / `LargeStep`（decimal；step 的精度决定默认小数位） |
| `prefix` / `suffix` | `string \| (value) => string` | `Prefix` / `Suffix`（string）；函数形式对应 `SuffixFormatter`（Func<decimal, string>?） |
| `scrub` | `false` | `IsScrubEnabled`（bool） |
| `locale` / `formatOptions` | `"en-US"` / 小数位与分组 | `Culture`（CultureInfo）/ `MinimumFractionDigits`、`MaximumFractionDigits`、`UseGrouping` |
| `size` | `"sm" \| "md" \| "lg"` / `"md"` | `Size`（Small / Medium / Large） |
| `limitHint` | `boolean \| (edge, limit) => string` / `true` | `IsLimitHintEnabled`（bool）+ `LimitHintFormatter`（Func<LimitEdge, decimal, string>?） |

**交互与键盘**：
- ArrowUp / ArrowDown 步进，按住连发并加速，在极限处持续推压；Shift + 方向键或 PageUp / PageDown 走 `LargeStep`；非输入态 Home / End 跳到极限。
- Enter 提交草稿（按 min / max 钳制）或全选；Escape 把草稿还原为编辑前的值；`Minimum` < 0 时接受负号；开启 scrub 后横向拖动标签，每 6px 一步。键盘与读屏点击只走一步，只有指针长按会连发。

**可访问性**：
- Spinner 类型 peer，valuetext 含前后缀；步进按钮名为 "Increase <label>" / "Decrease <label>"，到达极限时呈禁用态但仍可聚焦；指针聚焦不画焦点环，键盘聚焦显示 S7 焦点环。
- 变化以 `Polite` 播报，钳制时说明触及的极限（如 "10 seats, maximum"）；超限草稿置无效态并把极限说明加入 `HelpText`。

**动效**：
- 数字像里程表滚轮按变化方向滚动，9 到 0 回绕，快速连发时中途改目标；前后缀原位改写（P14、P2）。
- 长按 400ms 后开始连发，间隔从 150ms 按 0.86 递减到 40ms；在极限处每 240ms 推压一次（连发计时用 S12 的 RepeatRamp，推压为 P13）。
- 极限：数值拉伸后弹回，被拒按钮抖一次，极限说明从所守的一侧升起（snappy、subtle 模糊），约 1.5s 后消失（P13）；超限草稿以 `Warning` 着色壳体，提交后带数字滚动弹回极限。
- Reduced：值立即变化，极限按压只让数字短暂显示警告色；Off：同上。

**Avalonia 实现要点**：
- 模板：标签行（标签 + 极限说明）、壳体（减号按钮、数值 presenter 与隐形 TextBox 草稿、加号按钮）；数值 presenter 每位一个滚轮列，上下边缘用渐隐遮罩。
- 连发用 S12 计时器（递增超时，非逐帧）；scrub 用 S10 指针捕获，触摸时纵向平移仍可滚动；格式器按 Culture 缓存（S11）。

**AtomUI 参考**：NumericUpDown（仅供学习行为与边界，不复用其实现）。

**风险与未决**：
- Arc 的长按连发是递增超时的计时器，更接近 S12；主数据记为 P22，需在原语目录确认归属。另外，标签 scrub 的橡皮筋拖拽（P12）与描述行（P6）未列入主数据原语。
- 复核结论（汇总评审）：新增 P6、P12；移除 P22，已同步到[组件总清单](../overview.md)。

### 4.2 MoneyInput（Money input）
`slug: money-input` · 阶段 P6 · Arc 依赖：无 · 建议基类：TemplatedControl

**Arc 行为摘要**：货币金额字段：输入时实时分组，缺失的分位以淡色占位，宽度稳定，长金额缩放适配，输出最小货币单位。用于付款、小费、转账、预算等手填金额，以及币种可变的发票与报销。数量等非货币数字用 NumberField；从区间中挑选用 Slider；设计工具式拖拽调值用 ScrubInput。

**Arc API 要点**：

| Arc 属性 | 类型 / 默认值 | 建议 Avalonia 属性（类型） |
|---|---|---|
| `value` / `defaultValue` / `onValueChange` | `number \| null`（最小单位）/ `null`；回调附 `{ currency, major, formatted }` | `Value`（long?，双向）+ `ValueChanged` 事件 |
| `currency` / `defaultCurrency` / `onCurrencyChange` | ISO 4217 / `"USD"` | `Currency`（string，双向） |
| `currencies` | USD、EUR、GBP、JPY、CAD、AUD、CHF、INR | `Currencies`（IList<string>；只有一个时显示静态代码） |
| `min` / `max` | 最小单位 | `Minimum` / `Maximum`（long?） |
| `quickAdd` | `[10, 50, 100]`（主单位） | `QuickAddAmounts`（IList<decimal>；空列表隐藏） |
| `step` | 一个主单位 | `Step`（long?，最小单位） |
| `locale` / `hideLabel` | `"en-US"` / `false` | `Culture`（CultureInfo）/ `IsLabelVisible`（bool） |
| `error` / `description` | `string` | `Error` / `Description`（`Error` 替换内置区间提示） |

**交互与键盘**：
- ArrowUp / ArrowDown 加减一步（默认一个主单位），Shift 或 PageUp / PageDown 走十步；Enter 补齐小数（12.5 变为 12.50）；分组符上的 Backspace / Delete 删除相邻数字。超过 `Maximum` 的输入直接拒绝；`Minimum` 只在失焦时检查，允许途经较小金额。
- 币种按钮上 ArrowDown / ArrowUp 打开菜单；菜单内方向键与 Home / End 移动，键入字母按代码或名称跳转，Enter / Space 选择，Escape 关闭并回到按钮；切换币种时保留主单位金额。

**可访问性**：
- spinbutton，valuenow 为主单位，valuetext 为完整货币串；动画数字隐藏，真实输入承载纯文本。
- 区间、提示与错误进入 `HelpText`；越界、快捷加额与币种变化以 `Polite` 播报；币种菜单为带 typeahead 的 listbox。

**动效**：
- 快捷加额与方向键让每个数字列按变化方向滚动（P14），新增的列展开宽度（P5）；直接键入时跳到结果，保证光标不滞后；被拒绝的变化让金额以带回弹的弹簧横向踢一下，并闪烁区间提示（P13）。
- 币种符号短距离滚动切换（P3），符号槽位弹簧到新宽度；长金额以 smooth 缩小以适配宽度。
- Reduced：去掉滚动、踢动与宽度弹簧，数字淡入淡出、尺寸直接跳变；Off：同上。

**Avalonia 实现要点**：
- 值用 long 存最小单位，避免浮点误差；整数部分最多 12 位，限制滚动列数；币种的符号与小数位（如 JPY 为 0）来自内置 ISO 4217 表，格式器按 Culture 与币种缓存（S11）。
- 金额区用 ScaleTransform 适配宽度（S5 观察）；币种菜单放浮层（S8）从按钮角部展开，导航与 typeahead 用 S9；快捷加额为 30px 高的胶囊按钮，按下 0.96。

**AtomUI 参考**：NumericUpDown（仅供学习行为与边界，不复用其实现）。

**风险与未决**：
- 币种菜单的进出（0.96 缩放、上移 4px、subtle 模糊）属于 P10，快捷加额的按下缩放属于 P1，主数据均未列。
- 键入时显示真实 TextBox、步进时显示滚动数字，两层之间的光标与选区同步是主要难点；.NET 无按币种代码查询符号与小数位的 API，需维护内置表。
- 复核结论（汇总评审）：新增 P1、P10，已同步到[组件总清单](../overview.md)。

### 4.3 PhoneInput（Phone input）
`slug: phone-input` · 阶段 P6 · Arc 依赖：无 · 建议基类：TemplatedControl

**Arc 行为摘要**：电话字段，可搜索的国家选择器从国旗按钮中长出来，输入时按国家格式化，输出 E.164。用于注册、结账、联系表单，以及发送短信验证码前的号码录入。只存自由文本、从不拨号时用 Input；验证码本身用 Arc 的 OTP 区块（不在本库范围）；需要运营商或号码类型校验时用完整的号码元数据库。

**Arc API 要点**：

| Arc 属性 | 类型 / 默认值 | 建议 Avalonia 属性（类型） |
|---|---|---|
| `value` / `defaultValue` / `onValueChange` | E.164 `string`；回调附 `{ country, formatted, status, valid }` | `Value`（string，双向）+ `ValueChanged` 事件 |
| `country` / `defaultCountry` / `onCountryChange` | ISO 代码 / `"US"` | `Country`（string，双向） |
| `countries` | `string[]`（限定可选国家） | `Countries`（IList<string>?） |
| `preferredCountries` | `["US", "CA", "GB"]`（置顶于 Suggested） | `PreferredCountries`（IList<string>） |
| `validate` | `true`（失焦后长度不符时提示） | `IsValidationEnabled`（bool） |
| `error` / `description` | `string` | `Error` / `Description`（`Error` 替换内置校验文案） |
| `hideLabel` / `required` | `false` / 无 | `IsLabelVisible` / `IsRequired`（bool） |

**交互与键盘**：
- 号码中键入 + 打开选择器并按区号搜索；国家按钮上 ArrowDown / ArrowUp 打开，键入字母直接按该字母搜索。
- 搜索中 ArrowDown / ArrowUp / PageDown / PageUp 移动（一页 8 项），空搜索时 Home / End 到首尾；Enter 选择并回到号码；Escape 关闭并回到国家按钮。
- 分隔符上的 Backspace / Delete 删除相邻数字；粘贴或自动填充的国际号码自动切换国家，+1 按区号区分加拿大。

**可访问性**：
- 搜索框为 combobox，listbox 分为 Suggested 与 All countries 两组；国家按钮名称为 "Country, <name> +<code>"；选择、粘贴引起的国家变化与结果数量以 `Polite` 播报。
- 号码错误在失焦后以错误行显示；有效号码播报 "Valid <country> number" 并出现勾。

**动效**：
- 同一块表面把宽、高与圆角从国家按钮弹簧到列表（圆角 17 到 22，最宽 340px），展开用 0.4s、bounce 0.12，收起用 0.32s、bounce 0（P9）。
- 国家变化时国旗与区号按列表方向滚动（P3）；高亮在行间滑动，0.28s、bounce 0.08（P7）；消息行高度 smooth 并伴随小幅上升与模糊（P6）；有效勾以 snappy 弹出（P26）。
- Reduced：表面直接跳到尺寸，滚动、模糊与滑动改为短淡入淡出；Off：直接切换。

**Avalonia 实现要点**：
- 内置 49 个国家的表（区号、格式模式、长途前缀、示例），无网络请求；静态 `PhoneNumbers` 提供 Parse / Format / FormatNational，对应 parsePhoneNumber、formatPhoneNumber、formatNational。
- 粘贴走 TextBox 的 `PastingFromClipboard` 与 `TextChanging` 解析；列表宽度跟随字段、上限 340px（S5）；形变表面放浮层（S8），与 MorphSelect 共用内部表面形变 presenter。

**AtomUI 参考**：Select + LineEdit（参考）（仅供学习行为与边界，不复用其实现）。

**风险与未决**：
- Arc 用国旗 emoji（flagOf），Windows 默认字体不渲染国旗 emoji，需要内置国旗图片或退化为国家代码。
- 表面形变横跨触发按钮与浮层两层，打开与收起时的坐标衔接需在 S0.10 浮层 spike 的基础上验证。

### 4.4 ScrubInput（Scrub input）
`slug: scrub-input` · 阶段 P7 · Arc 依赖：无 · 建议基类：TemplatedControl

**Arc 行为摘要**：Figma 式数字字段：拖动标签调值，或键入数值、单位与四则运算，越界时橡皮筋式拉伸。用于检查器里的位置、尺寸、旋转与不透明度，以及既要精确键入又要拖拽的密集设置。带步进按钮的普通表单字段用 NumberField；范围与位置更重要时用 Slider；金额用 MoneyInput。

**Arc API 要点**：

| Arc 属性 | 类型 / 默认值 | 建议 Avalonia 属性（类型） |
|---|---|---|
| `label`（必填） | 文本或 16px 图标，兼作拖拽手柄 | `Label`（object） |
| `value` / `defaultValue` / `onValueChange` / `onValueCommit` | `number` / `0`；details 含 source 与 unit | `Value`（double，双向）+ `ValueChanged`（Source：Scrub / Type / Key / Reset）+ `ValueCommitted` 事件 |
| `min` / `max` / `wrap` | `-Infinity` / `Infinity` / `false` | `Minimum` / `Maximum`（double）/ `IsWrapping`（bool，需两端有限） |
| `step` / `fineStep` / `coarseStep` | `1` / `step / 10` / `step * 10` | `Step` / `FineStep` / `CoarseStep`（double） |
| `precision` / `pixelsPerStep` | 由 step 推导 / `2` | `Precision`（int?）/ `PixelsPerStep`（double） |
| `unit` / `units` / `onUnitChange` | `string` / `string[]` | `Unit`（string，双向）/ `Units`（IList<string>） |
| `resetValue` / `track` | 无 / min 与 max 都有限时为 true | `ResetValue`（double?）/ `IsTrackVisible`（bool?） |
| `size` / `pointerLock` | `"sm" \| "md"` / `true` | `Size`（Small 30 / Medium 36）/ `IsPointerLockEnabled`（bool） |

**交互与键盘**：
- ArrowUp / ArrowDown 步进，Shift 粗调 10 倍（吸附粗网格），Alt 细调到 1/10；拖拽中按住 Shift / Alt 即时切换并显示倍率。
- Enter 提交键入（支持 12*2、(100-24)/2、50% 等，逗号视为小数点）；Escape 在键入时恢复上次值，在拖拽时取消回到起点；双击标签回到 `ResetValue`；指针移动超过 3px 才进入拖拽，每 `PixelsPerStep` 像素一步。

**可访问性**：
- spinbutton，有限的 valuemin / valuemax，valuetext 读出百分比与角度；标签是真实标签，点击即聚焦并全选；非法键入置无效态 900ms 并保留旧值；单位循环按钮名为 "Unit: <unit>. Change unit."。

**动效**：
- 越过 min / max 时以橡皮筋阻力拉伸，最多 12px，松手弹回（0.34s、bounce 0.18），数字随之倾斜（P12、P13）。
- 非法输入与在极限处步进让字段短促抖动（0.3s、bounce 0.42，P13）。
- 单位变化从小幅模糊中升起（snappy，P2）；轨道填充跟手 1:1，键入或步进时以 smooth 滑到新值。
- Reduced：去掉拉伸、抖动与滑动，单位改为淡入淡出；Off：直接到终态。

**Avalonia 实现要点**：
- 标签手柄用 S10 指针捕获，触摸时只拦截横向；只在取整后的值变化时发事件，`ValueCommitted` 在拖拽结束、Enter / 失焦或单步落定时各触发一次。
- 表达式求值抽为静态 `ScrubExpression.Evaluate(text, units)`（对应 evaluateScrubExpression，deg 与 ° 互为别名）；字段宽度用 S5 观察以换算拉伸比例。

**AtomUI 参考**：无（AtomUI 没有同类控件）。

**风险与未决**：
- Avalonia 没有指针锁定与光标重定位 API，`IsPointerLockEnabled` 首版只能退化为普通捕获（Arc 在浏览器拒绝锁定时也如此），长距离拖拽会碰到屏幕边缘。
- 轨道填充的弹簧滑动属于 P23，主数据未列。
- 复核结论（汇总评审）：新增 P23，已同步到[组件总清单](../overview.md)。

### 4.5 TagInput（Tag input）
`slug: tag-input` · 阶段 P6 · Arc 依赖：无 · 建议基类：TemplatedControl

**Arc 行为摘要**：把键入的短文本变成可移除的标签，支持键盘挑选与滑动的选中环。用于标签、关键词、邮箱等自由短值列表，按 Enter 或逗号添加。值来自固定列表时用 MultiSelect 或 ChipGroup；单个值用 Input。

**Arc API 要点**：

| Arc 属性 | 类型 / 默认值 | 建议 Avalonia 属性（类型） |
|---|---|---|
| `label`（必填） | `string` | `Label`（string） |
| `value` / `defaultValue` / `onValueChange` | `string[]` / `[]`；每次增删回调完整列表 | `Value`（IReadOnlyList<string>，双向）+ `ValueChanged` 事件 |
| `placeholder` | `"Add a tag"`（无标签时显示） | `PlaceholderText`（string，本地化默认） |
| `description` | `string` | `Description`（string?） |

**交互与键盘**：
- Enter 或逗号添加；重复项（不区分大小写）不新增，而是让已有标签脉冲一次。
- 输入框开头按 Backspace 选中最后一个标签，再按一次删除；Delete 删除选中标签；ArrowLeft / ArrowRight 在标签与输入框间移动选中；Escape 取消选中。
- 失焦时把未提交的草稿转为标签。

**可访问性**：
- 每个标签有名为 "Remove <tag>" 的移除按钮。
- 添加、移除、选中与重复以 `Polite` 播报。

**动效**：
- 新标签在键入处带模糊缩放出现（P26）；移除的标签离开，其余标签以 morph 滑到新位置（P19）。
- 选中环在标签之间滑动（P7）；标签换行时壳体高度以 smooth 跟随（P6），字段从不横向滚动。
- Reduced：去掉布局移动、缩放与模糊，只保留淡入淡出；Off：直接到终态。

**Avalonia 实现要点**：
- 模板为换行面板（标签加内部 TextBox），TextBox 最小宽 80px，最后一行放不下时自成一行；标签重排用 FLIP（`LayoutAnimator`），高度用 `SpringSizeDecorator`。
- 标签只是字符串，校验与规范化由调用方在 `ValueChanged` 中完成；适合几十个标签，不适合成百上千。

**AtomUI 参考**：Select(Mode=Tags)、Tag（仅供学习行为与边界，不复用其实现）。

**风险与未决**：
- 重复项的脉冲属于 P27（强调），主数据未列。
- 复核结论（汇总评审）：新增 P27，已同步到[组件总清单](../overview.md)。

### 4.6 MentionInput（Mention input）
`slug: mention-input` · 阶段 P6 · Arc 依赖：无 · 建议基类：TemplatedControl（内含 TextBox）

**Arc 行为摘要**：支持 @人员与 #频道提及的自增高文本框，提及是不可分割的令牌，光标处弹出建议。用于需要按 id 通知的评论与回复框、链接频道或项目的聊天与任务编辑器。纯文本用 Textarea；令牌是独立列表而非句子一部分时用 TagInput；需要标题、列表、链接时用 RichTextEditor。

**Arc API 要点**：

| Arc 属性 | 类型 / 默认值 | 建议 Avalonia 属性（类型） |
|---|---|---|
| `value` / `defaultValue` / `onChange` | `MentionValue { text, mentions }`（含 start / end 偏移） | `Value`（MentionValue，双向）+ `ValueChanged` 事件 |
| `people` / `channels` | `MentionPerson[]` / `MentionChannel[]`；省略即关闭该类提及 | `People` / `Channels`（IEnumerable?） |
| `onMentionAdd` | `(mention) => void` | `MentionAdded` 事件 |
| `submitOnEnter` / `onSubmit` | `false` / `(value) => void` | `IsSubmitOnEnter`（bool）/ `Submitted` 事件 + `SubmitCommand` |
| `minRows` / `maxRows` | `1` / `8` | `MinLines` / `MaxLines`（int，转给内部 TextBox） |
| `placement` | `"auto" \| "top" \| "bottom"` / `"auto"` | `SuggestionPlacement`（Auto / Top / Bottom） |
| `maxSuggestions` / `placeholder` | `6` / 无 | `MaxSuggestions`（int）/ `PlaceholderText` |
| `MentionInputHandle`（ref 方法） | focus、insert、openSuggestions、clear | 公共方法 `Insert(string)`、`OpenSuggestions(MentionKind)`、`Clear()`（`Focus()` 继承） |

**交互与键盘**：
- 在空格、行首或左括号 / 引号之后键入 @ 或 # 打开建议；ArrowDown / ArrowUp 移动并循环；Enter / Tab 把高亮项变为提及；Escape 只关闭本次建议。
- 紧邻提及时一次 Backspace / Delete 删除整个令牌；ArrowLeft / ArrowRight 把提及当作一个单位跨过。
- 开启 `IsSubmitOnEnter` 后 Enter 提交、Shift+Enter 换行；输入法组字期间 Enter 从不提交。

**可访问性**：
- 文本框是 combobox（autocomplete=list），建议是名为 People 或 Channels 的 listbox；结果数量（如 "3 people"、"No matches"）以 `Polite` 播报；提及着色来自文本框背后的隐藏镜像层，辅助技术只读纯文本，选择建议走原生插入以保留撤销。

**动效**：
- 字段高度在 MinLines 与 MaxLines 之间以 smooth 跟随内容（P6）。
- 建议面板从光标处以小幅缩放、snappy 弹出（P10）；高亮在行间滑动，0.3s、bounce 0.1（P7）；结果变化时面板高度弹簧（P6）。
- Reduced：高度与高亮直接跳变，面板只淡入淡出，无缩放与位移；Off：直接显隐。

**Avalonia 实现要点**：
- 内部 TextBox 前景透明，背后叠一个字形度量完全一致的镜像 TextBlock 绘制令牌着色；每次编辑按前后缀 diff 调整提及偏移。
- 建议面板放浮层（S8），用 TextLayout 求光标矩形定位，宽 272px、不超过字段宽并夹在右边缘内；auto 时下方不足 300px 且上方更宽就翻到上方；导航用 S9，排序依次为前缀、词首、子串、角色或描述；静态 `MentionSerializer.Serialize(value, format)` 对应 serializeMentions（默认 <@id> / <#id>）。

**AtomUI 参考**：Mentions（仅供学习行为与边界，不复用其实现）。

**风险与未决**：
- TextBox 的程序化插入是否进入撤销栈、组字状态能否从公开 API 获得，需要原型验证。
- 透明文本加镜像层要求两层换行完全一致，字体回退与行高差异会造成着色错位。

### 4.7 ShortcutRecorder（Shortcut recorder）
`slug: shortcut-recorder` · 阶段 P6 · Arc 依赖：无 · 建议基类：TemplatedControl

**Arc 行为摘要**：把按键组合录成键帽，组合已被占用时先询问再接管；同一模块还导出键帽与可搜索的快捷键速查表。用于设置页里的快捷键重映射、帮助对话框里的速查表、菜单与提示中的按键提示。快捷键固定只需展示时单独用 ShortcutKbd；按名称执行动作用 Arc 的 command palette 区块；纯触屏产品不要使用。

**Arc API 要点**：

| Arc 属性 | 类型 / 默认值 | 建议 Avalonia 属性（类型） |
|---|---|---|
| `label`（必填）/ `hideLabel` | `string` / `false` | `Label` / `IsLabelVisible` |
| `value` / `defaultValue` / `onValueChange` | `string \| null`（如 "mod+shift+k"）；details.replaced | `Value`（string?，规范化串，双向）+ `ValueChanged` 事件（Replaced） |
| `resetValue` | 默认取 defaultValue | `ResetValue`（string?） |
| `bindings` | `{ shortcut, label }[]` / `[]` | `Bindings`（IList<ShortcutBinding>） |
| `warnReserved` / `requireModifier` | `true` / `true` | `IsReservedWarningEnabled` / `IsModifierRequired`（bool） |
| `platform` | `"mac" \| "other"` / 自动检测 | `Platform`（ShortcutPlatform?：Mac / Other） |
| `placeholder` / `description` | `"Record shortcut"` / 无 | `PlaceholderText` / `Description` |

**交互与键盘**：
- Enter / Space 开始录制；录制中按住的修饰键即时显示为键帽，完成的组合被记录；Escape 取消录制；Tab 停止录制并移走焦点（不记录 Tab）。
- 录制中或聚焦时 Backspace / Delete 清除快捷键；默认要求 ⌘、Ctrl 或 ⌥（功能键除外），并对 ⌘W、⌘C 等保留组合给出警告。
- 冲突时显示 Use anyway 与 Cancel 两个真实按钮，任一操作后焦点回到录制按钮。

**可访问性**：
- 录制器是带按下态的按钮（录制中 pressed），名称为标签加口语化快捷键（如 "Command Shift K"），映射为 ToggleButton 语义。
- 录制、结果、冲突与清除以 `Polite` 播报，消息行进入 `HelpText`；录制器内的键帽对辅助技术隐藏，ShortcutKeys 以图像类型 peer 带口语化名称。

**动效**：
- 键帽以小幅缩放加模糊弹出，组合完成时按 item 间隔（35ms）交错落定（P26）；组合变化时键帽以 snappy 布局弹簧滑动（P19）。
- 录制圆点持续脉冲（P21）；消息行，以及速查表中被搜索过滤的分组与行，以高度弹簧收起（P6）。
- Reduced：去掉缩放、交错、布局滑动与高度弹簧，键帽与消息只淡入淡出（Arc 未提脉冲，按 Reduced 策略停止循环）；Off：直接到终态。

**Avalonia 实现要点**：
- 录制期间以隧道路由拦截 KeyDown / KeyUp，按 `PhysicalKey` 比较（⌥K 对应 alt+k 而不是 ˚）；mod 在 macOS 为 Command、其他平台为 Ctrl，取自平台快捷键配置，可被 `Platform` 覆盖。
- 静态 `Shortcut.Matches` 与 `Shortcut.Format`（返回文本与口语形式）对应 matchesShortcut / formatShortcut；按住修饰键的高亮只在启用时于 TopLevel 监听按键。

**AtomUI 参考**：无（AtomUI 没有同类控件）。

**风险与未决**：
- Arc 同时导出 ShortcutList、ShortcutKeys、ShortcutKbd，它们不在 110 组件清单内；作为同目录附属公开类型（各需 Token）还是内部 presenter，需主笔决定。菜单（P5）若要显示键帽，会早于本组件需要 ShortcutKbd。
- 部分系统级组合（macOS 的 ⌘Tab、Windows 的 Alt+Tab 与 Win 键组合）由操作系统先行处理，录制器收不到。

## 5. 实现顺序与依赖
- **P6**：NumberField 先行（P22 的首个使用者），沉淀数值类共享件：Spinner peer、逐位滚轮、越界拉伸与抖动、S11 格式化、S12 连发计时器；P13 由同阶段的 PasswordStrength 首先落地，两者需协调先后。MoneyInput 复用这些件并加币种菜单（S8、S9）。TagInput 用 P19（DatePicker P2）与 P26（Avatar P1）；MentionInput 用 P10（Tooltip P1）；ShortcutRecorder 用 P21（Button P0）。PhoneInput 的 P9 来自 ExpandableCard（P4），其表面形变 presenter 与 MorphSelect 同阶段共建。
- **P7**：ScrubInput，依赖 P12（UserMenu P5 落地）与 S10 手势，复用 NumberField 的拉伸与抖动实现。
- 跨大类：壳体与消息行来自 [文本输入](../text-fields/overview.md)；PhoneInput 与 [选择器](../selects/overview.md) 的 MorphSelect 共用表面形变；ShortcutKbd 可被 [菜单](../menus/overview.md) 复用（见 4.7 风险）。

## 6. 相关文档
- [组件总清单](../overview.md) · [规格模板](../spec-template.md) · [路线图](../../roadmap/overview.md)
- [字段通用结构](../../architecture/forms-and-fields.md) · [浮层与输入](../../architecture/overlays-and-input.md)
- [原语目录](../../architecture/motion/primitives-catalog.md) · [Token 目录](../../architecture/theming/token-catalog.md)
- 相邻大类：[文本输入](../text-fields/overview.md) · [选择器](../selects/overview.md) · [滑块](../sliders/overview.md) · [选取器](../pickers/overview.md)
