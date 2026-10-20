# 组件总清单

> Arc 侧边栏 22 个大类、110 个免费组件的完整清单。这是组件范围、类名、阶段分配的**唯一来源**；大类 overview 与路线图都以本表为准。

## 1. 说明

- 范围依据：uiarc.dev 侧边栏快照（2026-10-20），排除 13 个 Pro 组件，见 [站点快照](arc-catalog-snapshot.md)。
- 类名：Arc 标题的 PascalCase，无前缀；唯一例外是 "Theme switcher" → `ThemeSwitcher`（见 [ADR 0004](../decisions/0004-naming-and-namespaces.md)）。
- 阶段：P0–P9，定义见 [路线图](../roadmap/overview.md)。
- Arc 依赖：Arc 源码中对其他 Arc 组件的直接依赖，必须排在更早的阶段。
- 建议基类：设计草案，以组件规格为准。
- 原语：使用的动效原语编号，见 [原语目录](../architecture/motion/primitives-catalog.md)。
- AtomUI 参考：AtomUI 中可供学习的同类实现，不继承、不包装（[ADR 0002](../decisions/0002-independent-controls.md)）。
- 状态：`规划中` → `规格已评审` → `实现中` → `已实现` → `已验收`。

## 2. 汇总

| 大类 | 代码目录 | 数量 | 文档 |
|---|---|---|---|
| 按钮 | `Buttons` | 10 | [buttons/overview.md](buttons/overview.md) |
| 手势 | `Gestures` | 3 | [gestures/overview.md](gestures/overview.md) |
| 菜单 | `Menus` | 3 | [menus/overview.md](menus/overview.md) |
| 文本输入 | `TextFields` | 7 | [text-fields/overview.md](text-fields/overview.md) |
| 特殊输入 | `SpecialInputs` | 7 | [special-inputs/overview.md](special-inputs/overview.md) |
| 选择器 | `Selects` | 5 | [selects/overview.md](selects/overview.md) |
| 开关与选项 | `Toggles` | 6 | [toggles/overview.md](toggles/overview.md) |
| 滑块 | `Sliders` | 2 | [sliders/overview.md](sliders/overview.md) |
| 选取器 | `Pickers` | 5 | [pickers/overview.md](pickers/overview.md) |
| 编辑器 | `Editors` | 3 | [editors/overview.md](editors/overview.md) |
| 导航 | `Navigation` | 3 | [navigation/overview.md](navigation/overview.md) |
| 展开与滚动 | `Expand` | 5 | [expand/overview.md](expand/overview.md) |
| 浮层 | `Overlays` | 6 | [overlays/overview.md](overlays/overview.md) |
| 消息 | `Messages` | 5 | [messages/overview.md](messages/overview.md) |
| 进度 | `Progress` | 5 | [progress/overview.md](progress/overview.md) |
| 头像与徽标 | `Avatars` | 3 | [avatars/overview.md](avatars/overview.md) |
| 卡片 | `Cards` | 3 | [cards/overview.md](cards/overview.md) |
| 图表 | `Charts` | 13 | [charts/overview.md](charts/overview.md) |
| 表格与数据 | `Tables` | 6 | [tables/overview.md](tables/overview.md) |
| 动态与会话 | `Activity` | 3 | [activity/overview.md](activity/overview.md) |
| 媒体 | `Media` | 3 | [media/overview.md](media/overview.md) |
| 文字效果 | `TextEffects` | 4 | [text-effects/overview.md](text-effects/overview.md) |
| **合计** | | **110** | |

| 阶段 | P0 | P1 | P2 | P3 | P4 | P5 | P6 | P7 | P8 | P9 | 合计 |
|---|---|---|---|---|---|---|---|---|---|---|---|
| 组件数 | 2 | 6 | 7 | 19 | 8 | 13 | 17 | 17 | 12 | 9 | 110 |

## 3. 清单

### 3.1 按钮（`Buttons`，10）

大类文档：[buttons/overview.md](buttons/overview.md)

| # | 类名 | Arc 组件 | 阶段 | Arc 依赖 | 建议基类 | 原语 | AtomUI 参考 | 状态 |
|---|---|---|---|---|---|---|---|---|
| 1 | `Button` | [Button](https://uiarc.dev/components/button) | P0 | — | Avalonia Button | P1 P2 P5 P21 P29 | Button | 已实现 |
| 2 | `ActionButton` | [Action button](https://uiarc.dev/components/action-button) | P3 | — | Arc Button | P1 P2 P4 P5 P15 P21 | Button(IsLoading)、PopupConfirm | 已实现 |
| 3 | `SplitButton` | [Split button](https://uiarc.dev/components/split-button) | P5 | — | TemplatedControl | P1 P2 P4 P5 P10 P19 | SplitButton | 规划中 |
| 4 | `ButtonGroup` | [Button group](https://uiarc.dev/components/button-group) | P5 | — | ItemsControl | P1 P2 P5 P7 P10 | CompactSpace、OptionButtonGroup | 规划中 |
| 5 | `FloatingButtonGroup` | [Floating button group](https://uiarc.dev/components/floating-button-group) | P2 | `Tooltip` | ItemsControl | P1 P2 P5 P7 P10 | FloatButtonGroup | 已实现 |
| 6 | `ExpandingButtonGroup` | [Expanding button group](https://uiarc.dev/components/expanding-button-group) | P7 | — | ItemsControl | P1 P2 P5 P15 | FloatButtonGroup | 已实现 |
| 7 | `CopyButton` | [Copy button](https://uiarc.dev/components/copy-button) | P1 | — | Avalonia Button | P1 P2 P4 P15 | — | 已实现 |
| 8 | `IconMorph` | [Icon morph](https://uiarc.dev/components/icon-morph) | P3 | — | Control（自绘） | P15 P16 P26 P29 | — | 已实现 |
| 9 | `ConfirmMorph` | [Confirm morph](https://uiarc.dev/components/confirm-morph) | P7 | — | TemplatedControl | P2 P5 P9 P14 P15 P20 P21 P22 P26 P29 | PopupConfirm | 已实现 |
| 10 | `ThemeSwitcher` | [Theme switcher](https://uiarc.dev/components/theme-switch) | P2 | `Button` | TemplatedControl | P1 P2 P29 | —（ThemeManager API） | 已实现 |

### 3.2 手势（`Gestures`，3）

大类文档：[gestures/overview.md](gestures/overview.md)

| # | 类名 | Arc 组件 | 阶段 | Arc 依赖 | 建议基类 | 原语 | AtomUI 参考 | 状态 |
|---|---|---|---|---|---|---|---|---|
| 11 | `HoldToConfirm` | [Hold to confirm](https://uiarc.dev/components/hold-to-confirm) | P7 | — | Avalonia Button | P1 P2 P5 P15 P22 P29 | CircleProgress（参考） | 已实现 |
| 12 | `SwipeActions` | [Swipe actions](https://uiarc.dev/components/swipe-actions) | P7 | — | ContentControl | P6 P10 P12 P19 | Carousel 滑动逻辑（参考） | 规划中 |
| 13 | `Reactions` | [Reactions](https://uiarc.dev/components/reactions) | P5 | — | ItemsControl | P5 P6 P7 P10 P14 P19 P26 P27 | Rate、FlyoutHost（参考） | 规划中 |

### 3.3 菜单（`Menus`，3）

大类文档：[menus/overview.md](menus/overview.md)

| # | 类名 | Arc 组件 | 阶段 | Arc 依赖 | 建议基类 | 原语 | AtomUI 参考 | 状态 |
|---|---|---|---|---|---|---|---|---|
| 14 | `DropdownMenu` | [Dropdown menu](https://uiarc.dev/components/dropdown-menu) | P5 | — | TemplatedControl + MenuSurface | P2 P5 P7 P10 P19 | DropdownButton、MenuFlyout | 已实现 |
| 15 | `ContextMenu` | [Context menu](https://uiarc.dev/components/context-menu) | P5 | — | ContentControl + MenuSurface | P7 P10 P19 | ContextMenu | 已实现 |
| 16 | `UserMenu` | [User menu](https://uiarc.dev/components/user-menu) | P5 | — | TemplatedControl + MenuSurface | P7 P10 P11 P12 P19 | DropdownButton、Avatar | 已实现 |

### 3.4 文本输入（`TextFields`，7）

大类文档：[text-fields/overview.md](text-fields/overview.md)

| # | 类名 | Arc 组件 | 阶段 | Arc 依赖 | 建议基类 | 原语 | AtomUI 参考 | 状态 |
|---|---|---|---|---|---|---|---|---|
| 17 | `Input` | [Input](https://uiarc.dev/components/input) | P3 | — | Avalonia TextBox | P4 P6 P14 | LineEdit | 已实现 |
| 18 | `Textarea` | [Textarea](https://uiarc.dev/components/textarea) | P3 | — | Avalonia TextBox | P4 P6 P14 | TextArea | 已实现 |
| 19 | `PasswordField` | [Password field](https://uiarc.dev/components/password-field) | P3 | — | Avalonia TextBox | P1 P2 P6 P15 P29 | LineEdit(IsEnableRevealButton) | 已实现 |
| 20 | `PasswordStrength` | [Password strength](https://uiarc.dev/components/password-strength) | P6 | — | Avalonia TextBox | P3 P6 P13 P14 P15 P23 | StepsProgressBar（参考） | 已实现 |
| 21 | `SearchField` | [Search field](https://uiarc.dev/components/search-field) | P3 | — | Avalonia TextBox | P1 P26 | SearchEdit | 已实现 |
| 22 | `ExpandingSearch` | [Expanding search](https://uiarc.dev/components/expanding-search) | P6 | — | TemplatedControl | P6 P7 P9 P10 P19 | SearchEdit | 已实现 |
| 23 | `InlineEdit` | [Inline edit](https://uiarc.dev/components/inline-edit) | P6 | — | TemplatedControl | P3 P5 P6 P15 P21 | — | 已实现 |

### 3.5 特殊输入（`SpecialInputs`，7）

大类文档：[special-inputs/overview.md](special-inputs/overview.md)

| # | 类名 | Arc 组件 | 阶段 | Arc 依赖 | 建议基类 | 原语 | AtomUI 参考 | 状态 |
|---|---|---|---|---|---|---|---|---|
| 24 | `NumberField` | [Number field](https://uiarc.dev/components/number-field) | P6 | — | TemplatedControl | P2 P6 P12 P13 P14 | NumericUpDown | 规划中 |
| 25 | `MoneyInput` | [Money input](https://uiarc.dev/components/money-input) | P6 | — | TemplatedControl | P1 P3 P5 P10 P13 P14 | NumericUpDown | 规划中 |
| 26 | `PhoneInput` | [Phone input](https://uiarc.dev/components/phone-input) | P6 | — | TemplatedControl | P3 P6 P7 P9 P26 | Select + LineEdit（参考） | 规划中 |
| 27 | `ScrubInput` | [Scrub input](https://uiarc.dev/components/scrub-input) | P7 | — | TemplatedControl | P2 P12 P13 P23 | — | 规划中 |
| 28 | `TagInput` | [Tag input](https://uiarc.dev/components/tag-input) | P6 | — | TemplatedControl | P6 P7 P19 P26 P27 | Select(Mode=Tags)、Tag | 规划中 |
| 29 | `MentionInput` | [Mention input](https://uiarc.dev/components/mention-input) | P6 | — | TemplatedControl（内含 TextBox） | P6 P7 P10 | Mentions | 规划中 |
| 30 | `ShortcutRecorder` | [Shortcut recorder](https://uiarc.dev/components/shortcut-recorder) | P6 | — | TemplatedControl | P6 P19 P21 P26 | — | 规划中 |

### 3.6 选择器（`Selects`，5）

大类文档：[selects/overview.md](selects/overview.md)

| # | 类名 | Arc 组件 | 阶段 | Arc 依赖 | 建议基类 | 原语 | AtomUI 参考 | 状态 |
|---|---|---|---|---|---|---|---|---|
| 31 | `Select` | [Select](https://uiarc.dev/components/select) | P5 | — | SelectingItemsControl | P3 P10 | Select | 已实现 |
| 32 | `Combobox` | [Combobox](https://uiarc.dev/components/combobox) | P6 | — | SelectingItemsControl | P2 P6 P10 P26 | AutoComplete、ComboBox | 已实现 |
| 33 | `MultiSelect` | [Multi-select](https://uiarc.dev/components/multi-select) | P6 | — | SelectingItemsControl | P5 P10 P14 P15 P26 | Select(Mode=Multiple) | 已实现 |
| 34 | `MorphSelect` | [Morph select](https://uiarc.dev/components/morph-select) | P6 | — | SelectingItemsControl | P3 P5 P7 P8 P9 | Select | 已实现 |
| 35 | `ChipGroup` | [Chip group](https://uiarc.dev/components/chip-group) | P6 | — | SelectingItemsControl | P1 P5 P6 P19 P26 | CheckableTagGroup | 已实现 |

### 3.7 开关与选项（`Toggles`，6）

大类文档：[toggles/overview.md](toggles/overview.md)

| # | 类名 | Arc 组件 | 阶段 | Arc 依赖 | 建议基类 | 原语 | AtomUI 参考 | 状态 |
|---|---|---|---|---|---|---|---|---|
| 36 | `RadioCards` | [Radio cards](https://uiarc.dev/components/radio-cards) | P3 | — | SelectingItemsControl | P7 P26 | RadioButtonGroup | 已实现 |
| 37 | `BillingToggle` | [Billing toggle](https://uiarc.dev/components/billing-toggle) | P2 | `AnimatedCounter` | TemplatedControl | P2 P5 P7 P14 | Segmented（参考） | 已实现 |
| 38 | `Checkbox` | [Checkbox](https://uiarc.dev/components/checkbox) | P3 | — | Avalonia ToggleButton | P1 P15 P16 P26 | CheckBox | 已实现 |
| 39 | `RadioGroup` | [Radio group](https://uiarc.dev/components/radio-group) | P3 | — | SelectingItemsControl | P7 P26 | RadioButtonGroup | 已实现 |
| 40 | `Switch` | [Switch](https://uiarc.dev/components/switch) | P0 | — | Avalonia ToggleButton | P24 | ToggleSwitch | 已实现 |
| 41 | `SegmentedControl` | [Segmented control](https://uiarc.dev/components/segmented-control) | P3 | — | SelectingItemsControl | P7 | Segmented | 已实现 |

### 3.8 滑块（`Sliders`，2）

大类文档：[sliders/overview.md](sliders/overview.md)

| # | 类名 | Arc 组件 | 阶段 | Arc 依赖 | 建议基类 | 原语 | AtomUI 参考 | 状态 |
|---|---|---|---|---|---|---|---|---|
| 42 | `Slider` | [Slider](https://uiarc.dev/components/slider) | P7 | — | RangeBase | P12 P13 P14 P26 | Slider | 已实现 |
| 43 | `ElasticSlider` | [Elastic slider](https://uiarc.dev/components/elastic-slider) | P7 | — | RangeBase | P12 P13 P24 P26 | Slider | 已实现 |

### 3.9 选取器（`Pickers`，5）

大类文档：[pickers/overview.md](pickers/overview.md)

| # | 类名 | Arc 组件 | 阶段 | Arc 依赖 | 建议基类 | 原语 | AtomUI 参考 | 状态 |
|---|---|---|---|---|---|---|---|---|
| 44 | `Calendar` | [Calendar](https://uiarc.dev/components/calendar) | P1 | — | TemplatedControl | P3 P7 P25 | Calendar | 已实现 |
| 45 | `DatePicker` | [Date picker](https://uiarc.dev/components/date-picker) | P2 | `Calendar` | TemplatedControl | P3 P10 P19 | DatePicker | 规划中 |
| 46 | `DateRangePicker` | [Date range picker](https://uiarc.dev/components/date-range-picker) | P6 | — | TemplatedControl | P3 P7 P8 P9 P25 | RangeDatePicker | 规划中 |
| 47 | `TimePicker` | [Time picker](https://uiarc.dev/components/time-picker) | P6 | — | TemplatedControl | P3 P10 | TimePicker | 规划中 |
| 48 | `ColorPicker` | [Color picker](https://uiarc.dev/components/color-picker) | P6 | `TextMorph` | TemplatedControl | P4 P9 P12 P13 P14 P19 P26 P29 | ColorPicker（独立包） | 规划中 |

### 3.10 编辑器（`Editors`，3）

大类文档：[editors/overview.md](editors/overview.md)

| # | 类名 | Arc 组件 | 阶段 | Arc 依赖 | 建议基类 | 原语 | AtomUI 参考 | 状态 |
|---|---|---|---|---|---|---|---|---|
| 49 | `RichTextEditor` | [Rich text editor](https://uiarc.dev/components/rich-text-editor) | P9 | — | TemplatedControl（块编辑器） | P6 P7 P9 P10 P25 | — | 规划中 |
| 50 | `SignaturePad` | [Signature pad](https://uiarc.dev/components/signature-pad) | P9 | — | Control（自绘） | P2 P7 P15 P29 | — | 规划中 |
| 51 | `FileDropzone` | [File dropzone](https://uiarc.dev/components/file-dropzone) | P9 | — | TemplatedControl | P6 P13 P15 P19 P21 P23 P27 | Upload、UploadDropZone | 规划中 |

### 3.11 导航（`Navigation`，3）

大类文档：[navigation/overview.md](navigation/overview.md)

| # | 类名 | Arc 组件 | 阶段 | Arc 依赖 | 建议基类 | 原语 | AtomUI 参考 | 状态 |
|---|---|---|---|---|---|---|---|---|
| 52 | `Tabs` | [Tabs](https://uiarc.dev/components/tabs) | P4 | — | SelectingItemsControl | P6 P7 P25 P30 | TabControl、TabStrip | 已实现 |
| 53 | `Breadcrumb` | [Breadcrumb](https://uiarc.dev/components/breadcrumb) | P4 | — | ItemsControl | P19 | Breadcrumb | 已实现 |
| 54 | `Pagination` | [Pagination](https://uiarc.dev/components/pagination) | P4 | — | TemplatedControl | P7 P19 P25 | Pagination | 已实现 |

### 3.12 展开与滚动（`Expand`，5）

大类文档：[expand/overview.md](expand/overview.md)

| # | 类名 | Arc 组件 | 阶段 | Arc 依赖 | 建议基类 | 原语 | AtomUI 参考 | 状态 |
|---|---|---|---|---|---|---|---|---|
| 55 | `ScrollArea` | [Scroll area](https://uiarc.dev/components/scroll-area) | P4 | — | Avalonia ScrollViewer | P27 P29 P30 | ScrollViewer | 已实现 |
| 56 | `Accordion` | [Accordion](https://uiarc.dev/components/accordion) | P4 | — | ItemsControl | P6 | Collapse(IsAccordion) | 已实现 |
| 57 | `ExpandableCard` | [Expandable card](https://uiarc.dev/components/expandable-card) | P4 | — | HeaderedContentControl | P4 P6 P9 | Expander、Card | 已实现 |
| 58 | `ResizablePanels` | [Resizable panels](https://uiarc.dev/components/resizable-panels) | P7 | — | TemplatedControl（自定义 Panel） | P12 P26 | Splitter | 已实现 |
| 59 | `ExpandableText` | [Expandable text](https://uiarc.dev/components/expandable-text) | P4 | — | TemplatedControl | P2 P6 P29 P30 | — | 已实现 |

### 3.13 浮层（`Overlays`，6）

大类文档：[overlays/overview.md](overlays/overview.md)

| # | 类名 | Arc 组件 | 阶段 | Arc 依赖 | 建议基类 | 原语 | AtomUI 参考 | 状态 |
|---|---|---|---|---|---|---|---|---|
| 60 | `Dialog` | [Dialog](https://uiarc.dev/components/dialog) | P5 | — | ContentControl（Overlay 宿主） | P2 P11 | Dialog | 已实现 |
| 61 | `Drawer` | [Drawer](https://uiarc.dev/components/drawer) | P5 | — | ContentControl（Overlay 宿主） | P2 P11 P12 | Drawer | 已实现 |
| 62 | `BottomSheet` | [Bottom sheet](https://uiarc.dev/components/bottom-sheet) | P5 | — | ContentControl（Overlay 宿主） | P11 P12 | Drawer(Placement=Bottom) | 已实现 |
| 63 | `Popover` | [Popover](https://uiarc.dev/components/popover) | P5 | — | ContentControl + AtomUI Popup | P10 | FlyoutHost、Flyout | 已实现 |
| 64 | `HoverCard` | [Hover card](https://uiarc.dev/components/hover-card) | P5 | — | ContentControl + AtomUI Popup | P10 P19 | FlyoutHost(Trigger=Hover) | 已实现 |
| 65 | `Tooltip` | [Tooltip](https://uiarc.dev/components/tooltip) | P1 | — | 附加属性服务 + TemplatedControl | P2 P5 P10 | ToolTip | 已实现 |

### 3.14 消息（`Messages`，5）

大类文档：[messages/overview.md](messages/overview.md)

| # | 类名 | Arc 组件 | 阶段 | Arc 依赖 | 建议基类 | 原语 | AtomUI 参考 | 状态 |
|---|---|---|---|---|---|---|---|---|
| 66 | `Alert` | [Alert](https://uiarc.dev/components/alert) | P3 | — | ContentControl | P2 P6 | Alert | 已实现 |
| 67 | `Toast` | [Toast](https://uiarc.dev/components/toast) | P7 | — | TemplatedControl + ToastManager 服务 | P2 P6 P11 P12 P15 | WindowMessageManager | 已实现 |
| 68 | `ToastStack` | [Toast stack](https://uiarc.dev/components/toast-stack) | P7 | — | TemplatedControl（宿主） | P2 P5 P6 P12 P13 P19 P21 P22 P28 | WindowNotificationManager | 已实现 |
| 69 | `AnnouncementBar` | [Announcement bar](https://uiarc.dev/components/announcement-bar) | P7 | — | TemplatedControl | P3 P6 P14 P22 P25 | Alert、MarqueeLabel | 已实现 |
| 70 | `CookieConsent` | [Cookie consent](https://uiarc.dev/components/cookie-consent) | P7 | `Button`、`Switch` | TemplatedControl | P2 P9 P11 | — | 已实现 |

### 3.15 进度（`Progress`，5）

大类文档：[progress/overview.md](progress/overview.md)

| # | 类名 | Arc 组件 | 阶段 | Arc 依赖 | 建议基类 | 原语 | AtomUI 参考 | 状态 |
|---|---|---|---|---|---|---|---|---|
| 71 | `Progress` | [Progress](https://uiarc.dev/components/progress) | P3 | — | RangeBase | P14 P23 P26 | ProgressBar | 已实现 |
| 72 | `Skeleton` | [Skeleton](https://uiarc.dev/components/skeleton) | P3 | — | TemplatedControl | P6 P21 | Skeleton | 已实现 |
| 73 | `Stepper` | [Stepper](https://uiarc.dev/components/stepper) | P4 | — | ItemsControl | P2 P6 P15 P16 P23 P26 | Steps | 已实现 |
| 74 | `Countdown` | [Countdown](https://uiarc.dev/components/countdown) | P7 | — | TemplatedControl | P2 P5 P6 P14 P18 P21 P22 P29 | TimerStatistic | 已实现 |
| 75 | `UsageMeter` | [Usage meter](https://uiarc.dev/components/usage-meter) | P3 | — | TemplatedControl | P5 P14 P18 P23 P29 | ProgressBar | 已实现 |

### 3.16 头像与徽标（`Avatars`，3）

大类文档：[avatars/overview.md](avatars/overview.md)

| # | 类名 | Arc 组件 | 阶段 | Arc 依赖 | 建议基类 | 原语 | AtomUI 参考 | 状态 |
|---|---|---|---|---|---|---|---|---|
| 76 | `Avatar` | [Avatar](https://uiarc.dev/components/avatar) | P1 | — | TemplatedControl | P2 P26 | Avatar、AsyncImage | 已实现 |
| 77 | `AvatarGroup` | [Avatar group](https://uiarc.dev/components/avatar-group) | P2 | `Avatar` | ItemsControl | P5 P14 P19 P27 P28 | AvatarGroup | 已实现 |
| 78 | `Badge` | [Badge](https://uiarc.dev/components/badge) | P3 | — | ContentControl | P2 P5 | CountBadge、DotBadge | 已实现 |

### 3.17 卡片（`Cards`，3）

大类文档：[cards/overview.md](cards/overview.md)

| # | 类名 | Arc 组件 | 阶段 | Arc 依赖 | 建议基类 | 原语 | AtomUI 参考 | 状态 |
|---|---|---|---|---|---|---|---|---|
| 79 | `Card` | [Card](https://uiarc.dev/components/card) | P5 | — | ContentControl | P8 P11 P20 P27 | Card | 规划中 |
| 80 | `MetricCard` | [Metric card](https://uiarc.dev/components/metric-card) | P2 | `AnimatedCounter` | TemplatedControl | P3 P5 P14 P18 | Statistic | 已实现 |
| 81 | `EmptyState` | [Empty state](https://uiarc.dev/components/empty-state) | P3 | — | TemplatedControl | P2 P3 P6 P26 | Empty | 已实现 |

### 3.18 图表（`Charts`，13）

大类文档：[charts/overview.md](charts/overview.md)

| # | 类名 | Arc 组件 | 阶段 | Arc 依赖 | 建议基类 | 原语 | AtomUI 参考 | 状态 |
|---|---|---|---|---|---|---|---|---|
| 82 | `LineChart` | [Line chart](https://uiarc.dev/components/line-chart) | P8 | — | Control（自绘） | P15 P16 P17 P18 P21 P29 | — | 已实现 |
| 83 | `BarChart` | [Bar chart](https://uiarc.dev/components/bar-chart) | P8 | — | Control（自绘） | P3 P14 P16 P17 P18 | — | 已实现 |
| 84 | `DonutChart` | [Donut chart](https://uiarc.dev/components/donut-chart) | P8 | — | Control（自绘） | P3 P14 P16 P17 P18 P27 | — | 已实现 |
| 85 | `Streamgraph` | [Streamgraph](https://uiarc.dev/components/streamgraph) | P8 | — | Control（自绘） | P16 P17 P18 P27 | — | 已实现 |
| 86 | `BrushChart` | [Brush chart](https://uiarc.dev/components/brush-chart) | P8 | — | Control（自绘） | P2 P12 P15 P16 P17 P18 P29 | — | 已实现 |
| 87 | `WaffleChart` | [Waffle chart](https://uiarc.dev/components/waffle-chart) | P8 | — | Control（自绘） | P14 P16 P17 P18 P19 P26 | — | 已实现 |
| 88 | `SlopeChart` | [Slope chart](https://uiarc.dev/components/slope-chart) | P8 | — | Control（自绘） | P14 P15 P16 P17 P18 P27 | — | 已实现 |
| 89 | `Sparkline` | [Sparkline](https://uiarc.dev/components/sparkline) | P8 | — | Control（自绘） | P3 P15 P17 P18 P29 | — | 已实现 |
| 90 | `Gauge` | [Gauge](https://uiarc.dev/components/gauge) | P8 | — | Control（自绘） | P14 P15 P18 P23 | DashboardProgress（参考） | 已实现 |
| 91 | `ActivityHeatmap` | [Activity heatmap](https://uiarc.dev/components/activity-heatmap) | P8 | — | Control（自绘） | P3 P14 P17 P18 | — | 已实现 |
| 92 | `AnimatedCounter` | [Animated counter](https://uiarc.dev/components/animated-counter) | P1 | — | Control（RollingDigits） | P2 P5 P14 P18 | StatisticCountUp | 已实现 |
| 93 | `Ridgeline` | [Ridgeline](https://uiarc.dev/components/ridgeline) | P8 | — | Control（自绘） | P16 P17 P18 P27 | — | 已实现 |
| 94 | `Treemap` | [Treemap](https://uiarc.dev/components/treemap) | P8 | — | Control（自绘） | P14 P16 P17 P19 | — | 已实现 |

### 3.19 表格与数据（`Tables`，6）

大类文档：[tables/overview.md](tables/overview.md)

| # | 类名 | Arc 组件 | 阶段 | Arc 依赖 | 建议基类 | 原语 | AtomUI 参考 | 状态 |
|---|---|---|---|---|---|---|---|---|
| 95 | `SortableDataTable` | [Sortable data table](https://uiarc.dev/components/sortable-data-table) | P9 | — | TemplatedControl | P2 P14 P19 | DataGrid（参考） | 已实现 |
| 96 | `TreeView` | [Tree view](https://uiarc.dev/components/tree-view) | P9 | — | Avalonia TreeView | P6 P7 P19 | TreeView | 已实现 |
| 97 | `ReorderableList` | [Reorderable list](https://uiarc.dev/components/reorderable-list) | P7 | — | ItemsControl | P1 P12 P14 P19 | TabReorder、DataGrid 行拖拽（参考） | 已实现 |
| 98 | `FilterToolbar` | [Filter toolbar](https://uiarc.dev/components/filter-toolbar) | P6 | — | TemplatedControl | P3 P5 P9 P19 P25 | — | 已实现 |
| 99 | `CodeBlock` | [Code block](https://uiarc.dev/components/code-block) | P2 | `CopyButton` | TemplatedControl | P2 P3 P6 | GalleryCodeViewer（参考） | 已实现 |
| 100 | `JsonViewer` | [JSON viewer](https://uiarc.dev/components/json-viewer) | P9 | — | TemplatedControl | P2 P6 P7 P26 P30 | TreeView（参考） | 已实现 |

### 3.20 动态与会话（`Activity`，3）

大类文档：[activity/overview.md](activity/overview.md)

| # | 类名 | Arc 组件 | 阶段 | Arc 依赖 | 建议基类 | 原语 | AtomUI 参考 | 状态 |
|---|---|---|---|---|---|---|---|---|
| 101 | `Timeline` | [Timeline](https://uiarc.dev/components/timeline) | P9 | — | ItemsControl | P6 P15 P18 P19 P26 | Timeline | 规划中 |
| 102 | `CommentThread` | [Comment thread](https://uiarc.dev/components/comment-thread) | P9 | — | ItemsControl | P6 P9 P10 P14 P19 P25 | — | 规划中 |
| 103 | `ChatThread` | [Chat thread](https://uiarc.dev/components/chat-thread) | P9 | — | ItemsControl | P2 P3 P6 P8 P10 P14 P19 P21 P26 P30 | — | 规划中 |

### 3.21 媒体（`Media`，3）

大类文档：[media/overview.md](media/overview.md)

| # | 类名 | Arc 组件 | 阶段 | Arc 依赖 | 建议基类 | 原语 | AtomUI 参考 | 状态 |
|---|---|---|---|---|---|---|---|---|
| 104 | `ImageCompare` | [Image compare](https://uiarc.dev/components/image-compare) | P7 | — | TemplatedControl | P12 P13 P16 P24 P29 | — | 规划中 |
| 105 | `Carousel` | [Carousel](https://uiarc.dev/components/carousel) | P7 | — | SelectingItemsControl | P7 P12 P22 P25 P27 P28 | Carousel | 规划中 |
| 106 | `CardStack` | [Card stack](https://uiarc.dev/components/card-stack) | P7 | — | ItemsControl | P8 P12 P28 | — | 规划中 |

### 3.22 文字效果（`TextEffects`，4）

大类文档：[text-effects/overview.md](text-effects/overview.md)

| # | 类名 | Arc 组件 | 阶段 | Arc 依赖 | 建议基类 | 原语 | AtomUI 参考 | 状态 |
|---|---|---|---|---|---|---|---|---|
| 107 | `TextReveal` | [Text reveal](https://uiarc.dev/components/text-reveal) | P3 | — | Control（逐词/逐行排版） | P20 P29 | — | 已实现 |
| 108 | `InViewTitle` | [In-view title](https://uiarc.dev/components/in-view-title) | P3 | — | Control（逐词/逐行排版） | P18 P20 P29 | — | 已实现 |
| 109 | `TextMorph` | [Text morph](https://uiarc.dev/components/text-morph) | P1 | — | Control（逐字形排版） | P4 P5 | — | 已实现 |
| 110 | `TextShimmer` | [Text shimmer](https://uiarc.dev/components/text-shimmer) | P3 | — | Control | P2 P21 | Skeleton(IsActive)（参考） | 已实现 |

## 4. 本目录其他文档

| 文档 | 内容 |
|---|---|
| [arc-catalog-snapshot.md](arc-catalog-snapshot.md) | Arc 站点快照：总数、Pro 排除项、各类计数、URL 规则 |
| [spec-template.md](spec-template.md) | 组件详细规格模板 |
| [charts/chart-foundation.md](charts/chart-foundation.md) | 图表共享基础：比例尺、坐标轴、系列色、路径形变、指针与可访问数据 |

## 5. 维护规则

- 新增、移除组件或调整阶段时，先改本表，再同步大类 overview、路线图与原语目录的统计。
- 组件进入新状态时只改本表的"状态"列。
- [文档校验](../engineering/docs-verification.md) 检查本表的行数、各大类计数、阶段计数与依赖顺序。
