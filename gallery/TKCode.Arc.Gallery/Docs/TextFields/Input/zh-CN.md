---
title: 输入框
summary: 标签清晰、状态实用的单行输入框。
source-hash: 92e9f7d582c4
---

<Demo name="Basic" />

## 何时使用

`Input` 是让人输入一段简短自由文本的默认控件。它自带完整的字段结构（可见标签、外框、辅助文字与错误行），因此一个表单就是一列输入框，不需要在外面再搭布局。

- **身份与联系信息**：注册、结账、个人资料表单中的姓名、工作邮箱、职位、公司名称。这类值没有固定格式，用一个配好校验的普通文本字段最合适。
- **管理后台与开发者控制台中的短标识**：项目 slug、Webhook URL、API 密钥名称、DNS 记录值。用 `Prefix` 和 `Suffix` 显示固定部分（`https://`、`.acme.dev`），让人只输入会变化的部分。
- **需要实时反馈的值**：有长度与字符规则的用户名，或在输入时检查是否可用的工作区地址。辅助文字行只改写变化的词、数字逐位滚动，持续的反馈也显得平稳而不闪烁。
- **需要复制的只读值**：设置表单中的工作区 ID 或账号，与可修改的字段放在一起，`IsReadOnly` 保持相同的外框与对齐。

## 何时不用

- **多行文本**，例如描述、备注、工单内容或整段填写的邮寄地址：用 `Textarea`，它会换行并可调整高度。
- **密码等机密信息**：用 `PasswordField`（掩码显示，带显示切换）；让人设置新密码并需要展示规则时用 `PasswordStrength`。
- **有固定形状的值**：需要步进或拖动调整的数字（`NumberField`、`ScrubInput`）、金额（`MoneyInput`）、电话号码（`PhoneInput`）、标签或收件人等短词元（`TagInput`）以及 @提及（`MentionInput`）。这些控件会解析并格式化值，而 `Input` 只保存一个字符串。
- **从已知列表中取值**：用 `Select` 或 `Combobox`；日期与时间用 `DatePicker` 和 `TimePicker`。
- **搜索与筛选**：用 `SearchField`（清除按钮、Escape 清空、`SearchSubmitted`），页头中用 `ExpandingSearch`。
- **在页面上原位重命名标题**：用 `InlineEdit`，它在编辑前看起来就是普通文字。

## 用法

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:Input Label="邮箱"
               Placeholder="you@example.com"
               Description="我们不会公开你的邮箱。" />
</UserControl>
```

`Input` 派生自 Avalonia 的 `TextBox`，文本框的能力全部保留：双向的 `Text`、`MaxLength`、`IsReadOnly`、`TextAlignment`、`CaretIndex`、`SelectAll()`、撤销、输入法组字以及剪贴板快捷键。字段在此之上加入标签、消息行与 Arc 外框。

### 结构

1. **标签**（`Label`）：14 px、中等字重，位于外框上方 8 px。它同时是字段的无障碍名称。只有当其他可见元素已经为字段命名时，才用 `IsLabelVisible="False"` 隐藏它。
2. **外框**：1 px 边框、控件圆角、表面色背景。悬停与聚焦只加深边框颜色，出错时边框变为危险色，尺寸始终不变。
3. **前缀**（`Prefix`）：文字前的可选内容，例如协议、货币符号或图标。
4. **文字与占位文字**：可编辑的文字；文字为空时以弱化颜色显示 `Placeholder`。
5. **后缀**（`Suffix`）：文字后的可选内容，例如单位或一个小操作。
6. **辅助文字行**（`Description`）：外框下方 12 px 的弱化色帮助文案。
7. **错误行**（`ErrorMessage`）：12 px 的危险色文案。只要它有内容，字段就处于无效态。

### 辅助文字与错误提示

文案应由状态推导，直接修改字符串即可：只有变化的词会移动，数字逐位滚动。不要在几条独立的消息之间来回切换。

<Demo name="HelperAndError" />

```xml
<arc:Input Label="用户名"
           Text="{Binding UserName}"
           Description="{Binding UserNameHint}"
           ErrorMessage="{Binding UserNameError}" />
```

`ErrorMessage` 非空时字段即为无效态；为空时显示 `Text` 绑定的校验错误。

屏幕上实际显示的错误是 `EffectiveErrorMessage`：`ErrorMessage` 有内容时取它，否则取绑定校验错误的消息。只要两者之一存在，`IsInvalid` 就被强制为 `True`；只有在需要显示危险色边框而不显示消息时才手动设置它，例如一组一起校验失败的字段。

### 绑定到视图模型

在 MVVM 应用中，值和规则都归视图模型所有，输入框只负责呈现。使用 CommunityToolkit.Mvvm 时，`ObservableValidator` 会在属性赋值时校验，并通过 `INotifyDataErrorInfo` 报告错误；Avalonia 把错误传给 `Text` 绑定，输入框显示其中第一条。

```csharp
public partial class ProfileViewModel : ObservableValidator
{
    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Required(ErrorMessage = "请输入姓名。")]
    [StringLength(80, ErrorMessage = "最多 80 个字符。")]
    private string _fullName = string.Empty;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Required(ErrorMessage = "请输入工作邮箱。")]
    [EmailAddress(ErrorMessage = "请输入完整的邮箱地址，例如 name@company.com。")]
    private string _email = string.Empty;

    [RelayCommand]
    private async Task SaveAsync()
    {
        ValidateAllProperties();
        if (HasErrors)
        {
            return;
        }
        await _profiles.SaveAsync(FullName, Email);
    }
}
```

```xml
<StackPanel Spacing="16">
    <arc:Input Label="姓名" Text="{Binding FullName}" />
    <arc:Input Label="工作邮箱"
               Text="{Binding Email, UpdateSourceTrigger=LostFocus}"
               Placeholder="name@company.com" />
    <arc:Button Content="保存资料" Command="{Binding SaveCommand}" IsDefault="True" />
</StackPanel>
```

- `UpdateSourceTrigger=LostFocus` 在用户离开字段时才把邮箱写回视图模型，因此格式错误在输入完成后出现，而不是在敲下第一个字符时。辅助文字需要随输入变化的值（例如字符计数）保留默认的 `PropertyChanged`。
- 只保留一个校验来源。视图模型校验 DataAnnotations 特性、而 Avalonia 自带的 DataAnnotations 插件也检查同一组特性时，每条规则会执行两次；Avalonia 的 MVVM 应用模板正是因此移除了该插件。
- 编译绑定需要在视图上设置 `x:DataType`（为简洁起见，这里的示例省略了它）。

### 校验时机

好的表单会在用户能够处理问题的时刻检查对应规则。触发过早像是在指责用户，触发过晚则白白浪费一次提交。

| 规则 | 何时检查 | 显示在哪里 |
|---|---|---|
| 长度限制与允许的字符 | 输入时 | `Description` 倒数计数（“还剩 12 个”）；超出限制后才用 `ErrorMessage` |
| 格式（邮箱、URL、slug） | 字段失去焦点时 | `ErrorMessage`（配合 `LostFocus` 的绑定校验） |
| 必填 | 提交时，之后在值仍为空期间持续检查 | `ErrorMessage`；把焦点移到第一个无效字段 |
| 是否可用或唯一（服务端） | 停止输入片刻后，或失去焦点时 | 检查期间用 `Description`（“正在检查…”），已被占用时用 `ErrorMessage` |
| 保存时服务端返回的错误 | 请求结束后 | 在错误所指的字段上用 `ErrorMessage`；值变化后清除 |

```csharp
// 以服务端为准：把它的消息显示在对应的字段上，值一变化就清除。
var result = await api.CreateWebhookAsync(endpoint.Text);
endpoint.ErrorMessage = result.IsSuccess ? null : result.Error;

endpoint.TextChanged += (_, _) => endpoint.ErrorMessage = null;
```

### 尺寸

`Size` 决定外框高度：`Small` 为 36，`Medium` 为 44（Arc 的默认外观），`Large` 为 50。

<Demo name="Sizes" />

同一个表单只用一种尺寸。`Small` 适合密集的管理表格与工具栏，`Medium` 是表单和设置页的默认值，`Large` 适合单个醒目的字段，例如登录页的邮箱。选取器、选择器与特殊输入框使用相同的 `Size` 取值，混合使用时统一尺寸即可对齐。

### 前缀与后缀

<Demo name="PrefixAndSuffix" />

`Prefix` 与 `Suffix` 用于值的上下文而非值本身：域名前的 `https://`、价格前的 `$`、数字后的 `USD` 或 `GB`、子域名后的 `.acme.dev`。用弱化文字颜色显示它们，让输入的文字保持最醒目。它们不属于 `Text`，视图模型只会收到用户输入的内容。需要按数字或金额解析的值，请用 `NumberField` 或 `MoneyInput`，而不是给普通输入框加前缀。

### 状态

字段可以处于禁用、只读或无效状态。

<Demo name="States" />

- **只读**：值仍可获得焦点、选中和复制，辅助技术也照常朗读。用于需要查看和复制、但在此处不能修改的值，例如 ID。
- **禁用**：字段淡化到 50% 并移出 Tab 顺序。只在值暂时不适用时使用，并在附近说明原因，例如在开启该功能的开关的描述中。
- **无效**：显示危险色边框与错误行。错误应保留到值被修正为止，不要在获得焦点时清除。

### Field 基础

`Field` 在 `FieldFrame` 外加上标签与消息行。选择器与特殊输入框会复用这两者。

<Demo name="FieldFoundation" />

自己实现触发器或复合编辑器、又希望它与其他字段外观和行为一致时（相同的标签、外框状态、消息行与尺寸），使用 `Field` 和 `FieldFrame`。`Field.MessageSwap` 决定消息行如何变化：`Words`（默认）只移动变化的词，`Whole` 整行替换。

### 示例：工作区设置表单

SaaS 管理后台中的一个设置表单。标签是简短的名词，辅助文字说明后果（“显示在发票上”），工作区 ID 设为只读以便复制，URL 字段把地址的固定部分显示为前缀，用户只需输入属于自己工作区的部分。邮箱字段展示了表单应给出的错误：说明哪里有问题以及如何修正。

![带只读 ID、带前缀的 URL 字段和邮箱错误的工作区设置表单](images/WorkspaceForm.png)

```xml
<StackPanel Width="560" Spacing="20">
    <arc:Input Label="Workspace name"
               Text="Acme Analytics"
               Description="Shown on invoices and in emails to your team." />
    <arc:Input Label="Workspace ID"
               Text="ws_7f3a9c21"
               IsReadOnly="True"
               Description="Use it when you contact support." />
    <arc:Input Label="Workspace URL"
               Text="acme-analytics"
               Description="Lowercase letters, numbers and hyphens.">
        <arc:Input.Prefix>
            <TextBlock Text="app.acme.dev/"
                       Foreground="{arc:ArcDesignTokenResource TextMuted}"
                       VerticalAlignment="Center" />
        </arc:Input.Prefix>
    </arc:Input>
    <arc:Input Label="Billing email"
               Text="billing@acme"
               ErrorMessage="Enter a full email address, such as billing@acme.com." />
    <StackPanel Orientation="Horizontal" Spacing="8" HorizontalAlignment="Right">
        <arc:Button Content="Cancel" Variant="Secondary" />
        <arc:Button Content="Save changes" />
    </StackPanel>
</StackPanel>
```

### 文案规范

- **标签**：句子式大小写的简短名词短语，不加冒号，例如“工作邮箱”，而不是“请输入您的工作邮箱：”。标签在输入时始终可见，所以由它承载含义；占位文字做不到。
- **占位文字**：给出期望格式的示例（“name@company.com”“acme-analytics”），不要把它当作唯一的说明，也不要重复标签。开始输入后它就会消失。
- **辅助文字**：用一句话说明约束或后果，例如“3 到 20 个字符”“显示在发票上”。数字用阿拉伯数字书写，计数才能平滑滚动。
- **错误提示**：说明哪里有问题以及如何修正，用词与辅助文字保持一致。避免指责和术语。

| 不要写 | 改为 |
|---|---|
| 输入无效。 | 请输入邮箱地址，例如 name@company.com。 |
| 错误：字段必填 | 请输入姓名。 |
| 用户名太短！！ | 至少使用 3 个字符。 |
| 正则不匹配 | 只能使用小写字母、数字和连字符。 |

### 最佳实践

**推荐**

- 每个输入框都要有可见标签，紧凑布局也一样；只有当可见的标题或列头已经为它命名时，才使用 `IsLabelVisible="False"`。
- 每条消息行保持一个长期存在的字符串并更新它，让动效说明发生了什么变化。
- 提交失败时把焦点移到出错的字段上，并保留用户已输入的值。
- 字段宽度与预期的值相匹配：邮政编码字段不应横跨整页。

**避免**

- 用占位文字代替标签，或在开始输入后就消失的说明。
- 格式类规则在敲下第一个字符时就报错；应等到失去焦点或提交时。
- 在表单有效之前禁用提交按钮；用户无从得知缺了什么。应在提交时校验，并指向第一个问题。
- 按定时器在几条不同的消息之间切换；这样消息行永远无法稳定下来。

### 常见问题

**绑定有校验错误，但字段显示的是另一条消息。** `ErrorMessage` 优先于绑定的错误。把 `ErrorMessage` 清空（设为 `null` 或空字符串）后，校验消息就会显示。

**如何按 Enter 提交表单？** `Input` 本身不处理 Enter。给表单的主 `Button` 设置 `IsDefault="True"`，在任何字段中按 Enter 都会触发它。

**如何请求邮箱键盘或自动填充提示？** 使用 Avalonia 的附加属性 `TextInputOptions.ContentType`，例如 `TextInputOptions.ContentType="Email"`。屏幕键盘与输入法会据此调整。

**为什么没有右键菜单？** Arc 输入框不提供上下文菜单；剪切、复制、粘贴、撤销与全选都可以用键盘完成。

**逐词改写对中文或日文文案有效吗？** 消息行按空格分词。没有空格的文案会作为一个整体变化，仍有动画，但会整行改写。

## API 参考

<!-- api: Input -->

## 无障碍

- `Label` 是字段的名称。`IsLabelVisible="False"` 时标签不显示，但仍作为名称。
- 辅助文字与错误提示合并为字段的帮助文本，在名称和值之后朗读。
- 错误行是一个 assertive 实时区域：新的错误会立即播报，焦点不会移动。
- 带动画的文字对辅助技术隐藏，辅助技术读取的是一份纯文本副本。
- 光标、选择、输入法、撤销与剪贴板快捷键都是原生 `TextBox` 的行为。
- 悬停与聚焦只改变边框颜色；获得焦点的字段是边框更深且有光标的那一个。在密集的表单中焦点不易辨认时，改用 `Large` 或加大字段间距，而不是去掉标签。

### 键盘

| 按键 | 操作 |
|---|---|
| `Tab` / `Shift+Tab` | 移到下一个或上一个字段；只读字段仍会停留，禁用字段不会 |
| `Left` / `Right`、`Home` / `End` | 按字符移动光标，或移到开头、结尾 |
| `Ctrl+Left` / `Ctrl+Right`（macOS 上为 `Option`） | 按词移动光标 |
| `Shift` + 以上任一按键 | 扩展选区 |
| `Ctrl+A`、`Ctrl+C`、`Ctrl+X`、`Ctrl+V`（macOS 上为 `Cmd`） | 全选、复制、剪切、粘贴 |
| `Ctrl+Z` / `Ctrl+Y`（macOS 上为 `Cmd+Z` / `Cmd+Shift+Z`） | 撤销、重做 |
| `Enter` | 输入框本身不处理；窗口有默认按钮时触发该按钮 |

## 动效

- 辅助文字行与错误行以平滑弹簧展开高度；变化的词带着模糊升起并变清晰，数字逐位滚动。
- 减弱动效时，消息行直接以完整高度出现，换词改为瞬时淡入；关闭动效时直接到终态。
- 悬停与聚焦只加深边框颜色，尺寸不变。

| 变化 | 完整 | 减弱 | 关闭 |
|---|---|---|---|
| 消息行出现或消失 | 高度以平滑弹簧展开，文字淡入 | 直接以完整高度显示 | 立即完成 |
| 消息内容变化 | 只有变化的词带着柔和模糊从下方升起 | 瞬时淡入 | 立即完成 |
| 文案中的数字变化 | 只有变化的位滚动，变大向上、变小向下 | 瞬时淡入 | 立即完成 |
| 悬停、聚焦、无效 | 边框颜色渐变 | 边框颜色渐变 | 立即完成 |

## 相关组件

- 多行文本用 `Textarea`，密码用 `PasswordField`，搜索用 `SearchField`。
- `Field` 与 `FieldFrame` 为任何编辑器或触发器提供相同的标签、外框与消息行。

| 组件 | 适用场景 |
|---|---|
| `Textarea` | 文字跨多行，或用户会粘贴整段内容 |
| `PasswordField` / `PasswordStrength` | 值是机密信息，或是带规则的新密码 |
| `SearchField` / `ExpandingSearch` | 文字用于筛选或搜索，而不是保存 |
| `NumberField` / `MoneyInput` / `PhoneInput` | 值是需要解析的数字、金额或电话号码 |
| `TagInput` / `MentionInput` | 值是一组词元，或带 @提及的文字 |
| `InlineEdit` | 在标题或名称显示的位置直接重命名 |
| `Field` + `FieldFrame` | 自己实现编辑器或触发器，并希望与其他字段一致 |
