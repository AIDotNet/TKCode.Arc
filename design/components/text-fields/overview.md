# 文本输入（TextFields）

> Arc 的文本输入大类承载以自由文本为值的字段：单行、多行、密码、搜索与原位编辑，共用一套标签、消息行与弹簧改写动效。

## 0. 共享字段基座（Field foundation，已实现）

`src/TKCode.Arc/TextFields/_Shared/` 是公开的字段基座：本大类全部组件建立在它之上，特殊输入（NumberField、MoneyInput、PhoneInput……）、选取器（DatePicker、TimePicker……）与选择器的触发器直接复用，不再各自实现标签、外框和消息行。

```xml
<!-- 文本类：派生 FieldTextBox（Avalonia TextBox：光标、选择、IME、撤销、剪贴板、双向 Text 全保留） -->
<arc:Input Label="Email" Placeholder="you@example.com" Description="We never share it." />
<arc:Input Label="Price" Size="Large" Text="{Binding Price}" ErrorMessage="{Binding PriceError}">
    <arc:Input.Prefix><TextBlock Text="$" /></arc:Input.Prefix>
</arc:Input>

<!-- 复合输入或触发器：Field 负责标签与两条消息行，FieldFrame 负责外框、状态与前后缀槽 -->
<arc:Field Label="Due date" Description="Optional" ErrorMessage="{Binding DueError}">
    <arc:FieldFrame Size="Medium" Prefix="{StaticResource CalendarGlyph}">
        <TextBlock Text="Pick a date" />
    </arc:FieldFrame>
</arc:Field>
```

| 类型（均为 public，命名空间 `TKCode.Arc`） | 职责 | 关键 API |
|---|---|---|
| `FieldTextBox`（abstract，派生 Avalonia `TextBox`） | 文本类字段的基类；字段结构直接挂在控件上 | `Label`、`Description`、`ErrorMessage`、`Placeholder`（同步到 `PlaceholderText`）、`Size`、`Prefix`、`Suffix`、`IsLabelVisible`；`IsInvalid`（被强制为 true 当有错误）；只读 `EffectiveErrorMessage`（显式 `ErrorMessage` 优先，否则取 `Text` 绑定的数据校验错误）；`Label` 写入 `AutomationProperties.Name`，描述与错误合并进 `HelpText` |
| `Field`（`ContentControl`） | 标签 + 内容 + 描述行 + 错误行 | `Label`、`Description`、`ErrorMessage`、`IsInvalid`、`IsLabelVisible`（Arc 的 hideLabel）、`Size`（Small 用 12px 标签）、`MessageSwap`（Words / Whole）；独立使用时给内容设置 `LabeledBy` / `Name` / `HelpText`，点击标签聚焦内容 |
| `FieldFrame`（`ContentControl`） | Arc 输入外框：1px `BorderStrong`、`RadiusControl`、`Surface`；悬停与聚焦只变边框为 `Foreground`，无效 `Danger`，禁用 0.5 + `SurfaceMuted`，只读不响应悬停；颜色 fast 过渡 | `Prefix`、`Suffix`（进入逻辑树，模板里的 TemplateBinding 可用）、`Spacing`（默认 8）、`Size`（36 / 44 / 50 与内边距 10 / 12 / 14）、`IsInvalid`、`IsReadOnly`、`IsFocusActive`、`HasFocusRing`（3px 焦点环，从 6px 收拢）；伪类 `:focus-active` `:invalid` `:readonly` |
| `FieldMessage`（`Control`，自绘） | 描述 / 错误行（P4 + P6 + P14） | `Text`、`Kind`（Description / Error，错误行 `Assertive` 播报）、`Swap`（Words：只动变化的词、数字逐位滚动；Whole：整行按 `Direction` 升降）、`Padding`、`LineHeight`；`IsOpen` |
| `FieldIconButton`（Avalonia `Button`） | 框内图标按钮（清除、显隐、保存） | `IsToggle` + `IsOn`（IToggleProvider，aria-pressed）、`PressScale`（默认 0.96）；悬停 `SurfaceMuted`，S7 焦点环 |

共享 Token：`FieldTextBoxToken`（`TextLineHeight` 19.6、`TextLineHeightLG` 22.4、`SelectionFill` = `Foreground` 16%）供所有派生主题直接引用（`{arc:FieldTextBoxTokenResource SelectionFill}`），`FieldFrameToken` 提供各尺寸内边距与焦点环几何。

派生文本字段的模板写法（见 `Input/Themes/InputTheme.axaml`）：`arc:Field`（`ErrorMessage` 绑 `EffectiveErrorMessage`）包住名为 `PART_Frame` 的 `arc:FieldFrame`（`IsFocusActive` 绑 `IsFocused`，并绑 `IsInvalid`、`IsReadOnly`、`Size`、`Prefix`、`Suffix`），框内放 `PART_ScrollViewer` + `PART_Placeholder` + `PART_TextPresenter`。框内自带按钮放 `FieldFrame.Suffix`。注意：模板属性里直接写的 Token 资源优先级高于样式，需要被状态样式覆盖的值（边框色等）一律写在样式里。

## 1. 大类定位
- 解决"让人输入一段自由文本"：表单里的单行 / 多行值、登录与新密码、就地过滤与头部搜索、页面标题的原位改名。
- 边界：值有固定形状（数字、金额、电话、标签、提及、快捷键）归 [特殊输入](../special-inputs/overview.md)；从固定列表取值归 [选择器](../selects/overview.md)；需要标题、列表等格式归 [编辑器](../editors/overview.md) 的 RichTextEditor。
- 共同特征：标签在上，壳体只用边框颜色表达悬停、聚焦与无效；帮助和错误文案在壳体下方按弹簧展开并原位改写（只动变化的词，数字逐位滚动）。

## 2. 组件一览

| 组件 | 类名 | Arc 页面 | 阶段 | Arc 依赖 | 建议基类 | 原语 |
|---|---|---|---|---|---|---|
| Input | `Input` | [input](https://uiarc.dev/components/input) | P3 | 无 | Avalonia TextBox | P4 P6 P14 |
| Textarea | `Textarea` | [textarea](https://uiarc.dev/components/textarea) | P3 | 无 | Avalonia TextBox | P4 P6 P14 |
| Password field | `PasswordField` | [password-field](https://uiarc.dev/components/password-field) | P3 | 无 | Avalonia TextBox | P1 P2 P6 P15 P29 |
| Password strength | `PasswordStrength` | [password-strength](https://uiarc.dev/components/password-strength) | P6 | 无 | Avalonia TextBox | P3 P6 P13 P14 P15 P23 |
| Search field | `SearchField` | [search-field](https://uiarc.dev/components/search-field) | P3 | 无 | Avalonia TextBox | P1 P26 |
| Expanding search | `ExpandingSearch` | [expanding-search](https://uiarc.dev/components/expanding-search) | P6 | 无 | TemplatedControl | P6 P7 P9 P10 P19 |
| Inline edit | `InlineEdit` | [inline-edit](https://uiarc.dev/components/inline-edit) | P6 | 无 | TemplatedControl | P3 P5 P6 P15 P21 |

## 3. 共享约定
字段通用结构（标签 / 描述 / 错误 / 尺寸 / 校验态）以 [forms-and-fields](../../architecture/forms-and-fields.md) 为准；下面只列本大类的事实与偏差，数值取自 Arc 源码与 foundation。
- **尺寸**：Arc 原版只有一种规格（壳体最小高度 `ControlSizeMD` 44）。本库为与特殊输入、选取器统一，`FieldTextBox` / `FieldFrame` 额外提供 `Size`（Small 36 / Medium 44 / Large 50，默认 Medium 即 Arc 外观）；Textarea 最小高度固定 110px，不随尺寸变化；InlineEdit 不画常驻壳体，字号由 `Variant` 决定。
- **壳体**：1px `BorderStrong`、`RadiusControl`（18）、`Surface` 背景；正文 14px、行高 1.4，占位文字 `TextMuted`；标签 14px、字重 500，与壳体间距 8px。
- **状态**：鼠标悬停时边框变 `Foreground`（禁用、只读与无效态除外；PasswordStrength 悬停为 `TextMuted`）；聚焦也只把边框变为 `Foreground`，不改变尺寸；Input / PasswordField / SearchField 不画焦点环，Textarea 与 ExpandingSearch 按 Arc 源码在聚焦时加 3px `FocusRing` 外环（`FieldFrame.HasFocusRing`）；S7 焦点环用于框内按钮；无效态边框 `Danger`；禁用态不透明度 0.5（Textarea 0.6）、背景 `SurfaceMuted`。
- **消息行**：描述（`TextMuted`）与错误（`Danger`）为 12px。行高从 0 以 smooth 展开（P6），透明度 fast；文案从下方 0.35em、soft 模糊升起，standard + enter 缓动；文案变化时只替换变化的词（P4），数字逐位滚动，位移 snappy、列宽 morph、模糊 subtle（P14）；收起时高度 smooth、透明度 instant。
- **动效降级**：Reduced 下消息行以完整高度出现，换词改为 instant 淡入，去掉按下缩放；Off 直接到终态。
- **可访问性**：`Label` 写入 `AutomationProperties.LabeledBy` / `Name`，`Description` 写入 `AutomationProperties.HelpText`；`Error` 非空即无效态，错误行以 `AutomationLiveSetting.Assertive` 播报（Arc 为 role="alert"），其余状态播报用 `Polite`；动画文字对辅助技术隐藏，另保留一份纯文本。
- **框内图标按钮**（显隐、清除、保存 / 取消）：按下缩放 0.96（PasswordStrength 为 0.94），颜色 fast 过渡；可获键盘焦点并显示 S7 焦点环。
- **不映射的网页项**：`name` 与原生表单提交、`autoComplete`、iOS 聚焦缩放规避；"仅精细指针悬停"在桌面等价为鼠标悬停，触摸不触发悬停样式。
- **文案**：内置字符串（"Show password"、"Clear search"、"Recent" 等）以英文默认值的属性提供（`ShowPasswordText`、`ClearText`、`SuggestionsLabel`、`PasswordStrength.Messages`、`InlineEdit.RetryText` 等），由使用方覆盖；本库尚无 xlf 基础设施，接入后再迁移。
- **性能**：消息行、强度条、规则勾选、眼睛斜线都在 `Render` 中自绘，逐帧只改自绘状态；唯一的布局弹簧是消息行自身的高度（与 Arc 的高度动画一致）。模糊用按 0.25px 量化缓存的 `ImmutableBlurEffect`，动画中不分配。

## 4. 组件

### 4.1 Input（Input）
`slug: input` · 阶段 P3 · Arc 依赖：无 · 建议基类：Avalonia TextBox

**Arc 行为摘要**：带可见标签的单行文本字段，帮助与错误文案在下方按弹簧展开并原位改写。用于姓名、邮箱、URL 等单行值，以及随输入变化的校验文案。多行用 Textarea；值有固定形状时用 PasswordField、SearchField 或 NumberField；页面文字原位编辑用 InlineEdit。

**Arc API 要点**：

| Arc 属性 | 类型 / 默认值 | 建议 Avalonia 属性（类型） |
|---|---|---|
| `label`（必填） | `string` | `Label`（string） |
| `description` | `string` | `Description`（string?） |
| `error` | `string` | `Error`（string?；非空即无效态） |
| `value` / `onChange`（原生透传） | `string` | `Text`（继承 TextBox，双向）/ `TextChanged` 事件 |
| `placeholder`（原生透传） | `string` | `PlaceholderText`（继承） |
| `type`（原生透传） | `email`、`url` 等 | `TextInputOptions.ContentType`（附加属性） |

**交互与键盘**：
- Arc 未定义额外按键，光标、选择、剪贴板与撤销沿用原生输入框行为；受控与非受控均可，错误文案应由状态推导，改变字符串即原位改写，不要切换多条独立消息。

**可访问性**：
- 原生输入加真实标签：TextBox 自带 Edit 类型 peer，`Label` 关联为 `LabeledBy`，描述与错误合并进 `HelpText`。
- 错误行 `Assertive` 播报；动画单词对辅助技术隐藏，播报纯文本副本。

**动效**：
- 消息行展开 / 收起：高度 smooth，透明度 fast / instant（P6）。
- 改写：变化的词从 0.35em 下方带 soft 模糊升起（standard + enter），离开的词上移、subtle 模糊、exit 时长（P4）。
- 文案中的数字（如 "12 characters"）只滚动变化的位，变大向上、变小向下，位数增减时列宽 morph（P14）。
- Reduced：消息行直接以完整高度出现，换词 instant 淡入；Off：立即到终态。

**Avalonia 实现要点**：
- 派生 TextBox，以 `InputTheme` 自定义模板；保留 `PART_TextPresenter`、`PART_ScrollViewer`，壳体外加 `arc.label`、描述行、错误行语义部件。
- 消息行、逐词改写、逐位滚动做成共享内部 presenter，供本大类其余字段复用；高度弹簧依赖 S5 与 `SpringSizeDecorator`，退场保活依赖 S3；数字列用等宽数字字形。

**AtomUI 参考**：LineEdit（仅供学习行为与边界，不复用其实现）。

**风险与未决**：
- `Error` 与 Avalonia 数据校验（`DataValidationErrors`）的合流优先级需在 forms-and-fields 中统一。
- 逐词改写依赖空格分词，中文等无空格文本会退化为整句替换，需确认可接受。

### 4.2 Textarea（Textarea）
`slug: textarea` · 阶段 P3 · Arc 依赖：无 · 建议基类：Avalonia TextBox

**Arc 行为摘要**：带标签的多行字段，与 Input 共用帮助 / 错误行动效。用于简介、评论、反馈等自由多行文本，描述中的实时字数会逐位滚动。单行值用 Input；页面上原位编辑描述用 InlineEdit 的多行模式；文本实为短值列表时用 TagInput。

**Arc API 要点**：

| Arc 属性 | 类型 / 默认值 | 建议 Avalonia 属性（类型） |
|---|---|---|
| `label`（必填） | `string` | `Label`（string） |
| `description` | `string`（如 "120 characters"，计数滚动数字） | `Description`（string?） |
| `error` | `string` | `Error`（string?） |
| `value` / `onChange`（原生透传） | `string` | `Text`（继承，双向）/ `TextChanged` 事件 |
| `rows`（原生透传） | `number` | `MinLines`（int，继承） |
| `maxLength`（原生透传） | `number` | `MaxLength`（int，继承） |

**交互与键盘**：
- 原生多行行为：Enter 换行（`AcceptsReturn=true`），无额外快捷键。
- 填满所在列，最小高度 110px，提供竖向拖拽调整；不随内容自动增高。

**可访问性**：
- 与 Input 相同：真实标签、`HelpText` 合并描述与错误、错误行 `Assertive` 播报，动画文字另附纯文本。

**动效**：
- 消息行高度 smooth（P6）；变化的词带 soft 模糊升起（P4）；计数只滚动变化的位（P14）。
- Reduced：去掉滚动、模糊与高度弹簧，立即变化；Off：同上。

**Avalonia 实现要点**：
- 派生 TextBox，`AcceptsReturn=true`、`TextWrapping=Wrap`；竖向调整手柄是模板内的 `Thumb` 部件，只改 `Height`，下限 110。
- 复用 Input 的消息行 presenter；每次按键重算计数时只有变化的词重绘。

**AtomUI 参考**：TextArea（仅供学习行为与边界，不复用其实现）。

**风险与未决**：
- Avalonia TextBox 没有原生调整手柄，手柄命中区、光标形状与竖向滚动条的避让需要自绘并验证。

### 4.3 PasswordField（Password field）
`slug: password-field` · 阶段 P3 · Arc 依赖：无 · 建议基类：Avalonia TextBox

**Arc 行为摘要**：带显示 / 隐藏切换的密码字段，眼睛图标由一道绘制出来的斜线划掉，而不是换图标。用于登录与确认密码等需要核对的密文。创建或修改密码用 PasswordStrength；非密文用 Input。

**Arc API 要点**：

| Arc 属性 | 类型 / 默认值 | 建议 Avalonia 属性（类型） |
|---|---|---|
| `label`（必填） | `string` | `Label`（string） |
| `description` | `string` | `Description`（string?） |
| `messages` | `{ showPassword, hidePassword }` / "Show password"、"Hide password" | xlf 本地化键；可选覆盖 `ShowPasswordText` / `HidePasswordText`（string?） |
| `value` / `onChange`（原生透传） | `string`（默认非受控） | `Text`（继承，双向） |
| `autoComplete`（原生透传） | `current-password` / `new-password` | 不映射 |

**交互与键盘**：
- 切换按钮上 Enter / Space 显示或隐藏密码；按钮在壳体内部，窄布局下也不换行。

**可访问性**：
- 切换按钮带按下态，名称在 "Show password" 与 "Hide password" 之间切换：映射为 ToggleButton 语义（IToggleProvider）。
- 描述走 `HelpText`；Arc 要求设置 autoComplete 让密码管理器识别，桌面没有对应协议，不映射。

**动效**：
- 斜线按 pathLength 绘制（standard，透明度 instant），同时遮住其下的眼睛轮廓（P15、P29）。
- 首次切换之后，每次显隐值都会"解析"一次：从 0.35 不透明度与 2px 模糊回到清晰，standard + enter。
- 按钮 30×30，按下缩放 0.96；消息行同 Input（P6）。
- Reduced：斜线立即画完，去掉解析效果与按下缩放；Off：同上。

**Avalonia 实现要点**：
- 派生 TextBox，`PasswordChar` 掩码，显隐直接切换继承的 `RevealPassword`；按钮放 `InnerRightContent` 或模板部件 `arc.revealButton`。
- 斜线是 Lucide 眼睛图形上的独立 Path：描边绘制用 StrokeDashOffset，遮罩用 OpacityMask 或几何裁剪；解析效果用 `EffectTransition`（模糊）与不透明度过渡。

**AtomUI 参考**：LineEdit(IsEnableRevealButton)（仅供学习行为与边界，不复用其实现）。

**风险与未决**：
- 解析效果本质是模糊交叉淡化，主数据原语未列 P2；按下缩放未列 P1。是否补充由原语目录决定。
- 复核结论（汇总评审）：新增 P1、P2，已同步到[组件总清单](../overview.md)。

### 4.4 PasswordStrength（Password strength）
`slug: password-strength` · 阶段 P6 · Arc 依赖：无 · 建议基类：Avalonia TextBox

**Arc 行为摘要**：新密码字段，带四段强度条、规则清单和随强度上下形变的强度词。用于注册与修改密码，规则随输入实时勾选。登录用 PasswordField；与密码无关的可度量进度用 Progress。

**Arc API 要点**：

| Arc 属性 | 类型 / 默认值 | 建议 Avalonia 属性（类型） |
|---|---|---|
| `value` / `defaultValue` / `onValueChange` | `string` / `""`；回调附 `{ level, label, met }` | `Text`（继承，双向）+ `StrengthChanged` 事件（`PasswordStrengthResult`） |
| `rules` | `PasswordRule[]` / `defaultPasswordRules` | `Rules`（IList<PasswordRule>：Id、Label、Test、Remaining） |
| `error` | `string`（每个新错误抖动一次） | `Error`（string?） |
| `revealed` / `onRevealedChange` | `boolean` | `IsRevealed`（bool，双向） |
| `shortLength` | `number` / `8` | `ShortLength`（int） |
| `countCharacters` | `(password) => number` / 按码点计数 | `CharacterCounter`（Func<string, int>?；默认按 `Rune` 计数） |
| `messages` | 全部内置文案（levels、meterLabel、summary 等） | xlf 本地化 + 可选 `Messages` 覆盖对象 |

**交互与键盘**：
- 显隐按钮上 Enter / Space 切换；强度 = 满足规则的比例映射到四段，少于 `ShortLength` 个字符时停在第一段并显示 tooShort 词；打分在本机按键时运行，只做简单规则测试，无字典检查。

**可访问性**：
- 强度条为 role="meter"（值 0 到 4，valuetext 为强度词）：映射为只读 IRangeValueProvider 的 ProgressBar 类型 peer，`ItemStatus` 写强度词。
- 规则清单与错误进入 `HelpText`；汇总强度与满足条数的 status 区用 `Polite` 播报；显隐按钮关联输入框。

**动效**：
- 四段以交错波填充并变色，每段 smooth 平移填满、按变化顺序延迟（P23）；强度词随强度升降向上或向下形变，位移 snappy、subtle 模糊（P3）。
- 规则勾选：圆点 snappy 从 0.5 缩放到 1、逐条延迟 50ms，勾线 standard 绘制（P15）；剩余数量逐位滚动（P14）；行高 smooth（P6）。
- 每个新错误让壳体横向抖动一次：初速度 -240、刚度 900、阻尼 15 的弹簧自由回弹（P13）。
- Reduced：去掉抖动、波与滚动，立即变化（Arc 在水合后才应用，桌面端无此限制）；Off：同上。

**Avalonia 实现要点**：
- 派生 Arc PasswordField，复用壳体、显隐按钮（34×34，按下 0.94）与斜线动画；新增强度条（四段等分、间距 5px）、强度词（宽 4.5rem、右对齐）、规则清单（16px 圆环加勾线）。
- 打分抽成静态 `PasswordStrengthEstimator`（对应 Arc 的 estimateStrength、createPasswordRules），便于服务端复用同一规则；抖动用 S1 带初速度的弹簧。

**AtomUI 参考**：StepsProgressBar（参考）（仅供学习行为与边界，不复用其实现）。

**风险与未决**：
- Arc 源码中 password-strength 是独立实现（自带显隐按钮）；派生 PasswordField 后其模板与 API 变化会连带影响本组件，需在规格中固定扩展点。
- 复核结论（汇总评审）：建议基类改为 Avalonia TextBox，已同步到[组件总清单](../overview.md)。

### 4.5 SearchField（Search field）
`slug: search-field` · 阶段 P3 · Arc 依赖：无 · 建议基类：Avalonia TextBox

**Arc 行为摘要**：受控的搜索输入，清除按钮在预留槽位内缩放出现，字段宽度不变。用于就地过滤可见列表或表格、工具栏搜索。头部紧凑搜索并带结果用 ExpandingSearch；搜索要写入表单值用 Combobox；与其他筛选和排序并列时用 FilterToolbar。

**Arc API 要点**：

| Arc 属性 | 类型 / 默认值 | 建议 Avalonia 属性（类型） |
|---|---|---|
| `label`（必填） | `string` | `Label`（string） |
| `value`（必填）/ `onValueChange`（必填） | `string`；每次按键触发，清除时传空串 | `Text`（继承，双向）/ `TextChanged` 事件 |
| `placeholder`（原生透传） | `string` | `PlaceholderText`（继承） |

**交互与键盘**：
- Escape 清空字段（Arc 依赖浏览器 type="search" 的原生行为，Avalonia 需在 `OnKeyDown` 中实现）。
- 点击清除按钮后焦点回到输入框；每次按键都会通知，昂贵的过滤由调用方防抖。

**可访问性**：
- 搜索图标对辅助技术隐藏；清除按钮名称为 "Clear search"（本地化）。
- Edit 类型 peer，`Label` 作为 `Name`。

**动效**：
- 清除按钮从 0.8 缩放、带轻微模糊、snappy 出现（P26），按下缩放到 0.96（P1）。
- 槽位始终预留（24×24），按钮出现与否都不改变字段宽度。
- Reduced：按钮只淡入淡出，无缩放与模糊；Off：直接显隐。

**Avalonia 实现要点**：
- 派生 TextBox；18px 搜索图标放 `InnerLeftContent`，固定宽度的清除槽位放 `InnerRightContent`，按钮随 `Text` 是否为空出现，退场保活用 S3。
- 壳体与状态同共享约定；Arc 没有描述与错误行。

**AtomUI 参考**：SearchEdit（仅供学习行为与边界，不复用其实现）。

**风险与未决**：
- forms-and-fields 若默认带消息行，需允许本组件关闭（Arc 未提供 `description` / `error`）。

### 4.6 ExpandingSearch（Expanding search）
`slug: expanding-search` · 阶段 P6 · Arc 依赖：无 · 建议基类：TemplatedControl

**Arc 行为摘要**：图标按钮形变为搜索字段，下方展开分组、高亮匹配的结果。用于头部或工具栏这类放完整字段会拥挤的位置，以及带最近搜索的本地快速跳转。就地过滤列表用 SearchField；写入表单值用 Combobox；全局命令搜索用 Arc 的 command palette 区块（不在本库范围）。

**Arc API 要点**：

| Arc 属性 | 类型 / 默认值 | 建议 Avalonia 属性（类型） |
|---|---|---|
| `label`（必填） | `string`（同时命名按钮、字段与结果） | `Label`（string） |
| `items`（必填） | `{ id, title, meta?, group?, icon?, keywords? }[]` | `ItemsSource`（IEnumerable<ExpandingSearchItem>） |
| `suggestions` / `suggestionsLabel` | `[]` / `"Recent"` | `Suggestions`（IEnumerable?）/ `SuggestionsHeader`（string） |
| `onSelect` | `(item) => void`，随后字段收起 | `ItemSelected` 事件 + `SelectCommand`（ICommand?） |
| `onExpandedChange` | `(expanded) => void` | `IsExpanded`（bool，双向） |
| `expandedWidth` / `maxResults` | `360` / `6` | `ExpandedWidth`（double）/ `MaxResults`（int） |
| `anchor` | `"start" \| "end"` / `"end"` | `Anchor`（ExpandingSearchAnchor：Start / End） |
| `placeholder` / `emptyHint` | 默认取 label / "Try a shorter word or check the spelling." | `PlaceholderText` / `EmptyHint`（string） |

**交互与键盘**：
- 图标按钮上 Enter / Space 展开；ArrowDown / ArrowUp 在结果间移动并循环；Enter 选择当前结果；Escape 收回为图标。未输入时显示 `Suggestions`，按标题与 keywords 本地过滤，最多显示 `MaxResults` 条。

**可访问性**：
- 字段为 combobox，结果为带分组标题的 listbox：peer 用 ComboBox 类型 + IExpandCollapseProvider，结果项为 ListItem + ISelectionItemProvider，分组用 Group。
- 结果数量与空状态以 `Polite` 播报；收起时字段不可聚焦、不可命中（Arc 的 inert）。

**动效**：
- 展开时宽度 morph、收起时 smooth；圆角随宽度从圆形过渡到 `RadiusControl`，与宽度共用同一弹簧（P9）。
- 结果面板宽度跟随字段，高度 smooth（P6），淡入 fast、淡出 instant（P10）；结果行布局位置 smooth（P19）；高亮在选项间以 snappy 滑动并继承上一段速度（P7）；收起态按下缩放 0.97。
- Reduced：用淡入淡出切换状态，无宽度与位置移动；Off：直接切换。

**Avalonia 实现要点**：
- 根高度 `ControlSizeMD`，收起态是 44 的圆；展开宽度 = min(`ExpandedWidth`, 容器宽)，S5 跟随容器；空闲区域 `IsHitTestVisible=false`，不挡住下方内容。
- 结果面板放浮层（S8）锚定字段，最大高度 min(22rem, 视口 60%) 并自身滚动；高亮与键盘导航用 S9；排序对全部 items 运行并缓存，大数据集由调用方预过滤。

**AtomUI 参考**：SearchEdit（仅供学习行为与边界，不复用其实现）。

**风险与未决**：
- Arc 的结果面板在文档流内绝对定位；改放浮层后，面板宽度跟随与字段的视觉衔接需验证。
- 焦点留在 TextBox 时如何向读屏报告当前结果（Arc 用 aria-activedescendant）需由 overlays-and-input 统一方案。

### 4.7 InlineEdit（Inline edit）
`slug: inline-edit` · 阶段 P6 · Arc 依赖：无 · 建议基类：TemplatedControl

**Arc 行为摘要**：点击即编辑的文字：原地变为同度量的字段，乐观保存，失败时回滚并提供重试。用于读多改少的标题与描述（如项目名），单字段独立保存。多个字段一起保存用 Input / Textarea 组成的表单；需要一直看起来可编辑时用 Input。

**Arc API 要点**：

| Arc 属性 | 类型 / 默认值 | 建议 Avalonia 属性（类型） |
|---|---|---|
| `value`（必填） | `string`（已保存值；非编辑时外部更新会替换文字） | `Value`（string） |
| `onSave`（必填） | `(next) => void \| Promise` | `SaveHandler`（Func<string, Task>?；运行中为 saving，异常即回滚）+ `Saved` 事件 |
| `label`（必填） | `string` | `Label`（string，用于可访问名称） |
| `validate` | `(next) => string \| null` | `Validator`（Func<string, string?>?） |
| `placeholder` | `""` | `PlaceholderText`（string） |
| `multiline` | `false`；Shift+Enter 换行 | `IsMultiline`（bool） |
| `variant` | `"title" \| "body"` / `"title"` | `Variant`（InlineEditVariant：Title / Body） |
| `as` | `span`、`p`、`h1` 至 `h3` / `span` | `HeadingLevel`（int；写入 `AutomationProperties.HeadingLevel`） |

**交互与键盘**：
- 文字上 Enter / Space 进入编辑；Enter 保存（多行时 Shift+Enter 换行）；Escape 取消并回滚；Tab 离开即保存。
- 悬停提示只对鼠标生效，触摸点击直接进入编辑；文字末尾预留 66px 放保存与取消按钮（28px 圆形）。

**可访问性**：
- 静止态是名为 "<label>: <value>" 的按钮（Button 类型 peer，`Name` 拼接标签与值）；字段带 `Name` 与无效态，校验与保存错误写入 `Polite` 消息区并进入 `HelpText`，保存进度与结果用 status 区播报。

**动效**：
- 值之间滚动替换：新词从 soft 模糊中升起，旧词上移并以 subtle 模糊淡出（P3）；失败回滚方向相反（P3），并给框体 danger 淡色底。
- 框宽随草稿弹簧：输入中 snappy（0.16s、无回弹），切换状态用 morph；多行时框高 smooth（P5、P6）。
- 保存中 0.7s 线性旋转（P21）；保存落定后勾线绘制 0.32s、延迟 60ms（P15）；状态图标 snappy 缩放进入。
- Reduced：文字交叉淡化，勾线立即画完，旋转指示换成静态标记；Off：同上且无淡化。

**Avalonia 实现要点**：
- 模板含展示按钮（TextBlock，内边距 3 / 8，与编辑态字形度量一致）和叠在其上的 TextBox；进入编辑时用 TextLayout 命中测试把光标放到点击处。
- 尺寸在测量阶段确定，不出现一帧错误宽度；S5 跟随宽高；`SaveHandler` 异常走回滚分支并保留重试入口。

**AtomUI 参考**：无（AtomUI 没有同类控件）。

**风险与未决**：
- Arc 源码中的失败反馈是反向滚动加 danger 底色，并无抖动或拉伸；若 P13 只涵盖越界拉伸与抖动，此处可能多余。
- `as` 只影响语义层级，HeadingLevel 的读屏支持因平台而异。
- 复核结论（汇总评审）：移除 P13，已同步到[组件总清单](../overview.md)。

## 5. 实现顺序与依赖
- **P3**：Input → Textarea → PasswordField → SearchField。字段共享件（标签、消息行、逐词改写、逐位滚动）按 forms-and-fields 在首个使用者落地：若 DatePicker（P2）已先落地则直接复用，否则由 Input 落地。所需原语 P1、P29（Button，P0）、P4、P15（CopyButton，P1）、P6、P14（Calendar，P1）、P26（Avatar，P1）都已就绪；服务 S1 至 S5、S7。
- **P6**：PasswordStrength 先行（派生 PasswordField；它是 P13 的首个使用者，P13 随它落地，同阶段的 InlineEdit 与特殊输入复用），再做 ExpandingSearch（P9 来自 ExpandableCard P4、P10 来自 Tooltip P1、P19 来自 DatePicker P2，另需 S8、S9），最后 InlineEdit（P21 来自 Button P0）。P23 来自 Progress（P3）。
- 跨大类：特殊输入、选择器与选取器复用本大类沉淀的消息行 presenter 与壳体状态规则；ExpandingSearch 的结果列表与 Combobox、MorphSelect 共用 S9 的高亮与导航实现。

## 6. 实现说明（已落地）

- **Input**：`FieldTextBox` 的最简派生，模板 = Field + FieldFrame + TextPresenter；选中色为 `Foreground` 16%（Arc 输入类 `::selection`）。不提供 TextBox 的右键菜单（Arc 依赖浏览器原生菜单），剪贴板快捷键照常可用。
- **Textarea**：`AcceptsReturn` + 换行，最小高度 110、四边内边距 12、聚焦 3px 外环、禁用 0.6；Arc 不随内容增高，本库同样不自增高，`resize: vertical` 由右下角自绘握柄实现（`IsResizable`、`FrameHeight`），只增不低于 110。
- **PasswordField**：眼睛斜线是自绘 `PasswordEyeGlyph`：遮罩带（5 单位宽、圆头）按量化步长缓存为几何裁剪，斜线 standard 绘制、透明度 instant；首次切换后每次显隐让 TextPresenter 从 0.35 不透明度 + 2px 模糊恢复。`IsRevealed` 双向，开关为 `FieldIconButton`（IToggleProvider），名称在 Show / Hide 间切换。
- **PasswordStrength**：派生 PasswordField；打分可插拔（`IPasswordStrengthScorer.ScoreAsync`，默认 `PasswordStrengthEstimator` 同步且廉价，迟到结果被丢弃）；规则 `PasswordRules.Create(minLength, labels, countCharacters)`；强度条与勾选标记自绘，强度词用内部 `FieldSwapText`，剩余数量用 `RollingDigits` + `PresenceHost(Rise)`；新错误以初速度 -240、刚度 900、阻尼 15 的弹簧抖动一次。
- **SearchField**：Escape 清空、Enter 触发 `SearchSubmitted`（`Query`）；清除按钮在 24px 预留槽内以 `PresenceHost(Pop, 0.8)` 出现，点击后焦点回到输入框；放大镜随聚焦或有值变为 `Foreground`。
- **ExpandingSearch**：外壳宽度由 `MotionValue` 驱动（展开 morph、收起 smooth），圆角同一弹簧插值；结果面板是 `ArcPopup`，挂在宽度固定为展开宽度的隐形锚条下（Bottom 对齐），可见面板在其中随外壳宽度展开、高度 smooth；面板自带浮层阴影（关闭 AtomUI 弹层阴影，四周留 32px 阴影空间）。高亮用 `GlidingIndicator`（snappy，保持速度）；结果行的布局位置滑动（P19）未实现，结果集变化时直接重排。
- **InlineEdit**：内部 `InlineEditBox` 面板：单行时框立即取新宽、可见边框按 snappy 0.16s 无回弹（输入）或 morph（模式切换）跟随，动作槽贴边框右缘；多行时框高 smooth。显示层与编辑层共享字号、行高、内边距；点击按 TextLayout 命中测试放置光标，键盘激活全选。`SaveHandler` 抛异常即回滚（文字反向滚动 + danger 底色 1.4s 后 considered 淡出）并给出 Try again。

## 7. 相关文档
- [组件总清单](../overview.md) · [规格模板](../spec-template.md) · [路线图](../../roadmap/overview.md)
- [字段通用结构](../../architecture/forms-and-fields.md) · [浮层与输入](../../architecture/overlays-and-input.md)
- [原语目录](../../architecture/motion/primitives-catalog.md) · [Token 目录](../../architecture/theming/token-catalog.md)
- 相邻大类：[特殊输入](../special-inputs/overview.md) · [选择器](../selects/overview.md) · [编辑器](../editors/overview.md)
