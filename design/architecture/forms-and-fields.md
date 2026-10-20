# 字段通用结构

> TextFields、SpecialInputs、Selects、Pickers 等输入类组件共享的字段结构：标签、描述、错误、尺寸、状态、校验与动效。各组件的 overview 只描述与本文的差异。

## 1. 适用范围

| 大类 | 组件 |
|---|---|
| TextFields | Input、Textarea、PasswordField、PasswordStrength、SearchField、InlineEdit（编辑态）、ExpandingSearch（展开态） |
| SpecialInputs | NumberField、MoneyInput、PhoneInput、ScrubInput、TagInput、MentionInput、ShortcutRecorder |
| Selects | Select、Combobox、MultiSelect、MorphSelect 的触发器 |
| Pickers | DatePicker、DateRangePicker、TimePicker、ColorPicker 的触发器 |

## 2. 结构

```
┌ Label（必填，可视觉隐藏）
├ 输入框（Frame：边框 + 背景 + 前后缀 + 内容）
├ Description（可选，帮助文字）
└ Error（可选，错误文字；出现时替换或叠加在描述之下）
```

Arc 的 Input API 是这种结构的最小形态：`label`（必填）、`description`、`error`。

| 部件 | Arc 行为 | Avalonia 实现 |
|---|---|---|
| Label | 可见标签，与输入框关联；部分组件支持 `hideLabel`，只保留给辅助技术 | `Label` 属性（string）；模板中的 TextBlock；`AutomationProperties.Name` 取标签文本；`IsLabelVisible` 控制可见性 |
| Frame | 输入框外框：`RadiusControl` 圆角，`BorderStrong` 边框，`Surface` 背景 | 共享 presenter `FieldFrame`（Arc 程序集的 `Primitives/FieldFrame`，internal），提供前缀 / 后缀槽 |
| Description | 帮助文字，通过 aria-describedby 关联 | `Description` 属性；`AutomationProperties.HelpText` |
| Error | 设置 aria-invalid，并在 role="alert" 行中播报 | `ErrorText` 属性；非空时进入 `:error` 伪类（与 Avalonia `DataValidationErrors` 使用的伪类一致）；`AutomationProperties.LiveSetting = Assertive` |

## 3. 尺寸

| 尺寸 | 高度 Token | 说明 |
|---|---|---|
| Small | `ControlSizeSM`（36） | 紧凑布局 |
| Medium | `ControlSizeMD`（44） | 默认，触屏可点击的最小尺寸 |
| Large | `ControlSizeLG`（50） | 重要输入 |

- 内部间距不随窗口宽度变化，只有外部留白变化（Arc 规则）。
- 输入文字 16px（`TypeBase`）或 14px（`TypeSM`），字重 400；标签 14px，字重 500。精确值以各组件规格为准。

## 4. 状态

| 状态 | 视觉 | 伪类 |
|---|---|---|
| 默认 | `BorderStrong` 边框 | — |
| 悬停 | 边框加深（规格中给出 Token） | `:pointerover` |
| 聚焦 | 边框变为 accent 系颜色并显示光标；**不画焦点环**（文本框在鼠标点击时也匹配 focus-visible，Arc 用边框与光标表示焦点） | `:focus-within` |
| 无效 | `Danger` 边框，显示错误行 | `:error` |
| 禁用 | `SurfaceMuted` 填充，`TextMuted` 文字，保留边框以便仍能识别为输入框 | `:disabled` |
| 只读 | 不显示光标与悬停效果，可选中文字 | `:readonly` |

## 5. 校验集成

- 与 Avalonia 数据校验集成：绑定产生的 `DataValidationErrors` 映射为 `ErrorText` 与 `:error`（应用显式设置 `ErrorText` 时，以显式值为准）。
- 可选：实现 AtomUI 的表单接口（如 `IFormItemAware`），以便放进 AtomUI 的 `Form` 中使用。需要确认这些接口是否公开且稳定，在 Input 规格（P3）中决定；不得依赖 internal 成员。
- 错误文案的写法遵循 Arc 规则：说明发生了什么、如何修复，放在字段旁边。

## 6. 动效

| 场景 | 行为 | 原语 / Token |
|---|---|---|
| 描述 / 错误行出现与消失 | 行高用 smooth 弹簧展开或收起 | P6，smooth |
| 描述 / 错误文案改变 | 变化的词升起并从模糊变清晰；数字逐位滚动 | P4（词级）、P14 |
| 边框颜色变化 | 补间 | `DurationFast` + `EaseStandard` |
| Reduced | 行以完整高度直接出现；词替换改为瞬时淡入 | — |
| Off | 立即 | — |

辅助技术读取的是完整的纯文本；动画中的逐词元素对辅助技术隐藏（对应 Arc 的 aria-hidden 做法）。

## 7. 实现形态

- 文本类组件（Input、Textarea、PasswordField、SearchField）派生自 Avalonia `TextBox`，保留其文本编辑能力（选择、IME、撤销、剪贴板）。模板中加入 Label、FieldFrame、Description 与 Error。
- 复合输入（NumberField、PhoneInput、TagInput 等）派生自 `TemplatedControl`，内部包含一个或多个 TextBox。
- 选择类与选取类组件的触发器复用 FieldFrame 与 Label 结构，弹层部分见 [浮层与输入](overlays-and-input.md)。
- `FieldFrame` 分两步落地：
  1. DatePicker（P2）的触发器是首个使用者。Arc 的 DatePicker 有 `label` 与 `description`，没有 `error`，也没有行高动画，所以 P2 落地 Label、静态 Description 与外框。
  2. Input（P3）补齐 Error 行、描述 / 错误行的高度动画（P6）与逐词切换（P4），以及文本输入相关的部分（前缀 / 后缀、清除、字符计数）。
  两个规格共同定义它的完整 API。

## 8. 待定事项

| 事项 | 在哪个规格中决定 |
|---|---|
| 标签、描述、错误的精确字号、间距、颜色 | DatePicker（P2），Input（P3）复核 |
| `FieldFrame` 是否公开，供第三方组合使用 | DatePicker（P2） |
| 是否实现 AtomUI 表单接口 | Input（P3） |
| 字符计数器的位置与滚动数字 | Textarea（P3） |
