---
title: 密码框
summary: 带有可见显示切换控件的敏感文本输入。
source-hash: 1dbe8b8e16ad
---

<Demo name="SignIn" />

## 何时使用

`PasswordField` 用于保存一个已经存在、由用户输入或粘贴的机密值：登录时的密码、确认敏感变更的当前密码，或管理员在后台页面中输入的密钥。它是默认遮蔽值、外框内带显示切换按钮的 `Input`，因此标签、辅助文字行、错误行和尺寸都与表单中的其他字段完全一致。

- **SaaS 或计费门户的登录**：卡片中堆叠一个邮箱字段和一个密码字段，加一个“登录”按钮。切换按钮让用户在提交前核对在手机键盘上输入的长口令。
- **敏感变更前的重新验证**：Acme Billing 页面上的“当前密码”，用于修改收款账户或绑定的银行卡。字段在变更生效前要求输入机密，切换按钮可以帮助用户避免因拼写错误而被锁在变更之外。
- **管理后台中的运维凭据**：在设置页输入的数据库或 SMTP 密码。屏幕上的值是遮蔽的，但运维人员可以显示它，确认粘贴内容正确。
- **多步骤流程中确认已有机密**：重复用户已设定密码的第二个字段，例如设置页保存密码修改时的“确认密码”。
- **在共享或录制的屏幕上输入机密**：默认遮蔽让值在用户显示之前不会出现在屏幕上，这对自助终端、演示和支持会话很重要。

## 何时不用

- **需要设置并满足规则的新密码**：用 `PasswordStrength`，它会显示密码强度以及满足了哪些规则。普通的 `PasswordField` 不会对值的质量给出任何反馈。
- **非机密文本**：用 `Input`。用于用户名或账号的遮蔽字段会让人盲打。
- **需要复制出去的机密**：生成的 API 密钥、恢复码，或用户要复制到其他系统的令牌。用 `Input` 配合 `IsReadOnly` 和 `CopyButton`，因为遮蔽的值无法读回用于复制。
- **搜索查询**：用 `SearchField`。它有清除控件，并在按 Enter 时提交；遮蔽的搜索框会隐藏用户正在查找的内容。
- **验证器应用中的一次性验证码**：用 `Input`。验证码很短，按原样显示，且常被粘贴，圆点和显示切换只会拖慢用户。

## 用法

字段内的眼睛按钮可以显示或隐藏文字。它的名称会被辅助技术读取。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:PasswordField Label="密码"
                       Placeholder="请输入密码"
                       Description="点击显示按钮可核对你输入的内容。"
                       Width="360"
                       HorizontalAlignment="Left" />
</UserControl>
```

`PasswordField` 与 `Input` 派生自同一个共享字段基类，该基类派生自 Avalonia 的 `TextBox`。文本框提供值、光标、选择、输入法组字、撤销和剪贴板快捷键；共享基类提供标签、外框、辅助文字行和错误行、尺寸以及无效状态。`PasswordField` 额外加入遮蔽（默认使用 `•` 作为密码字符，来自文本框的 `PasswordChar`）、显示切换按钮，以及切换按钮内的眼睛图标。两者共有的部分（如校验时机和消息行）请参阅 `Input` 页面。

### 结构

1. **标签**（`Label`）：14 px、中等字重，位于外框上方。它同时是字段的无障碍名称。`IsLabelVisible` 可以在视觉上隐藏它，名称仍然保留。
2. **外框**：1 px 边框，带控件圆角，表面色背景。悬停与聚焦只加深边框；出错时边框变为危险色。
3. **前缀**（`Prefix`）：文字前的可选内容。显示切换按钮占据外框的后缀位置，因此继承的 `Suffix` 属性在密码框上不会显示。
4. **遮蔽文字**：输入的值以 `PasswordChar`（`•`）显示。当 `IsRevealed` 为 `True` 时，同一段文字显示为字符。值为空时，`Placeholder` 以弱化文字颜色显示。
5. **显示切换**：中号和大号下为 30 px 的圆形按钮，小号为 26 px；中号下距上、下和末端边缘各 7 px。它包含一个 18 px 的带斜线眼睛图标。悬停时填充弱化表面色，键盘聚焦时显示 2 px 焦点环。
6. **辅助文字行**（`Description`）：外框下方 12 px 的辅助文案。
7. **错误行**（`ErrorMessage`）：12 px、危险色文案。只要它有内容，字段即处于无效态。

外框内边距随尺寸变化：中号时文字前 12 px、切换按钮后 6 px；小号为 10 px 和 4 px；大号为 14 px 和 9 px。中号和小号的文字使用正文字号（14 px），大号使用基础字号。

### 绑定值

`Text` 是双向属性，可以绑定到视图模型。使用 `IsRevealed` 读取或设置可见状态。

```xml
<arc:PasswordField Label="密码"
                   Text="{Binding Password}"
                   ErrorMessage="{Binding PasswordError}" />
```

```csharp
signIn.PropertyChanged += (_, e) =>
{
    if (e.Property == PasswordField.IsRevealedProperty)
    {
        state.Text = signIn.IsRevealed ? "已显示" : "已隐藏";
    }
};
```

`IsRevealed` 同样是双向的。从代码设置它会显示或遮蔽值，切换按钮随之变化。字段还会让文本框自身的 `RevealPassword` 与 `IsRevealed` 保持同步，因此读取 `RevealPassword` 的代码会看到相同的状态。

### 绑定到视图模型

登录表单把密码与其他字段一起保存在视图模型中，并通过命令提交。不要把密码写入日志、分析事件或任何持久化状态；视图模型只在请求期间持有它。

```csharp
public partial class SignInViewModel : ObservableObject
{
    private readonly IAuthService _auth;

    public SignInViewModel(IAuthService auth)
    {
        _auth = auth;
    }

    [ObservableProperty]
    private string _email = string.Empty;

    [ObservableProperty]
    private string _password = string.Empty;

    [ObservableProperty]
    private string? _passwordError;

    [RelayCommand]
    private async Task SignInAsync()
    {
        PasswordError = null;
        if (string.IsNullOrEmpty(Password))
        {
            PasswordError = "请输入密码。";
            return;
        }

        var result = await _auth.SignInAsync(Email, Password);
        Password = string.Empty;
        if (!result.IsSuccess)
        {
            PasswordError = "邮箱或密码不正确。";
        }
    }
}
```

```xml
<StackPanel Width="360" Spacing="16">
    <arc:Input Label="工作邮箱" Text="{Binding Email}" />
    <arc:PasswordField Label="密码"
                       Text="{Binding Password}"
                       ErrorMessage="{Binding PasswordError}" />
    <arc:Button Content="登录" Command="{Binding SignInCommand}" IsDefault="True" />
</StackPanel>
```

- `Text` 和 `IsRevealed` 是双向的。`ErrorMessage` 是单向的：错误信息由视图模型持有，并在用户编辑字段或下一次提交开始时清除。
- 登录按钮上的 `IsDefault="True"` 让任一字段中按下 Enter 都会提交表单。字段本身不处理 Enter。
- 视图模型在请求结束后清空 `Password`，无论成功与否，使机密在内存中停留的时间不超过请求所需。

### 校验

登录只校验是否填写以及请求的结果，而不校验密码的格式。一个在创建时有效的密码，可能比今天的规则更短；登录时加上格式规则，会把使用旧密码的用户锁在门外。

| 规则 | 何时检查 | 显示在哪里 |
|---|---|---|
| 必填 | 提交时 | `ErrorMessage`：“请输入密码。”，并把焦点移到该字段 |
| 邮箱或密码错误 | 服务端拒绝请求后 | `ErrorMessage`，两种情况使用同一句通用文案 |
| 账号被锁定或请求过于频繁 | 服务端响应后 | `ErrorMessage`，附带下一步提示（“请 15 分钟后再试。”） |
| 长度或字符规则 | 仅在注册或修改密码时（使用 `PasswordStrength`） | 登录时不检查 |
| 与确认字段一致 | 确认字段失去焦点时，或提交时 | 确认字段上的 `ErrorMessage` |

用户编辑字段时清除 `ErrorMessage`，使消息不会比它所描述的值存在得更久。不要说明是邮箱还是密码错误：通用消息可以避免向攻击者透露哪些账号存在。

### 确认

当两个值不同时，从第二个字段设置 `ErrorMessage`。将其设为 `null` 即可清除。

<Demo name="Confirm" />

示例在每次按键时检查是否一致，因此用户还在输入第二个值时错误就会出现。在应用中，应在确认字段失去焦点或表单提交时检查一致性，并在两个值相等时立即清除消息。这样消息会在用户输完一个值之后才出现，而不是逐字符出现。

### 切换按钮名称

切换按钮的名称来自 `ShowPasswordText` 与 `HidePasswordText`。设置它们即可翻译文案。

```xml
<arc:PasswordField Label="Mot de passe"
                   ShowPasswordText="Afficher"
                   HidePasswordText="Masquer" />
```

切换按钮默认的名称为“显示密码”或“隐藏密码”，并随 `IsRevealed` 切换。请与标签一起翻译这两个名称，使屏幕阅读器用户在整个表单中听到同一种语言。

### 尺寸与状态

`Size` 决定外框高度：`Small` 为 36，`Medium`（默认值）为 44，`Large`（50）适合单个醒目的字段。

<Demo name="SizesAndStates" />

与 `Input` 一样，同一个表单只使用一种尺寸。`Small` 适合密集的管理页面，此时切换按钮也会缩小（26 px），以保持行的比例。禁用与无效状态与 `Input` 相同：禁用字段会淡化并离开 Tab 顺序，无效字段显示危险色边框和错误行。

### 示例：登录卡片

计费门户的登录卡片。卡片标明产品名称，邮箱字段是普通的 `Input`，密码字段显示登录失败时产生的错误：一句不说明具体哪部分有误的通用文案。该值处于遮蔽的默认状态，因此图片展示的是用户在显示任何内容之前看到的样子。按钮是表单的默认按钮，因此在密码字段中按 Enter 即可提交表单。

![带邮箱字段和遮蔽密码字段并显示通用错误的登录卡片](images/SignInCard.png)

```xml
<Border Width="560"
        Padding="32"
        CornerRadius="16"
        BorderThickness="1"
        BorderBrush="{arc:ArcDesignTokenResource Border}"
        Background="{arc:ArcDesignTokenResource Surface}">
    <StackPanel Width="360"
                Spacing="18"
                HorizontalAlignment="Center">
        <StackPanel Spacing="4">
            <TextBlock Text="Sign in to Acme Billing"
                       FontSize="20"
                       FontWeight="SemiBold"
                       Foreground="{arc:ArcDesignTokenResource Foreground}" />
            <TextBlock Text="Use your work email and password."
                       FontSize="14"
                       TextWrapping="Wrap"
                       Foreground="{arc:ArcDesignTokenResource TextSecondary}" />
        </StackPanel>
        <arc:Input Label="Work email"
                   Text="ada@acme.com" />
        <arc:PasswordField Label="Password"
                           Text="correct-horse-battery"
                           ErrorMessage="The email or password is incorrect." />
        <arc:Button Content="Sign in"
                    IsDefault="True"
                    HorizontalAlignment="Stretch" />
        <TextBlock Text="Forgot your password?"
                   FontSize="14"
                   HorizontalAlignment="Center"
                   Foreground="{arc:ArcDesignTokenResource TextSecondary}" />
    </StackPanel>
</Border>
```

### 文案规范

- **标签**：表示机密的名词，使用句子式大小写：“密码”“当前密码”“确认密码”。不要把“请输入密码”用作标签；那是占位文字或错误提示的内容。
- **占位文字**：可选。仅在字段有简短的格式提示时使用（“至少 12 个字符”应放在 `PasswordStrength` 上，而不是这里）。不要重复标签。
- **辅助文字**：一句话，解释后果或帮助用户核对输入（“点击显示按钮可核对你输入的内容。”）。
- **错误提示**：一句话，用词与标签保持一致。对于登录，一句通用文案即可涵盖邮箱错误或密码错误。
- **切换按钮名称**：默认的“显示密码”和“隐藏密码”已经足够清楚。请与表单的其他内容一起翻译。

| 不要写 | 改为 |
|---|---|
| 请输入你的密码！！！ | 请输入密码。 |
| 凭据无效 | 邮箱或密码不正确。 |
| 密码错误 | 邮箱或密码不正确。 |
| 重新输入密码 | 确认密码 |
| 你的密码不能太弱 | 至少使用 12 个字符，并包含一个数字和一个符号。 |

### 最佳实践

**推荐**

- 默认保持值被遮蔽，让用户通过切换按钮自行显示。默认遮蔽可以在共享屏幕和录屏中保护该值。
- 设置新密码的页面使用 `PasswordStrength`，使用既有密码的页面使用 `PasswordField`，让规则只出现在用户正在选择值的地方。
- 登录失败时只显示一句通用消息，使表单不会暴露哪些账号存在。
- 请求结束后从视图模型中清除密码，并确保它不进入日志和分析数据。
- 标签、辅助文字和错误文案使用同一种语言，并同步设置 `ShowPasswordText` 与 `HidePasswordText`。

**避免**

- 把 `PasswordField` 用于必须复制出去的值，例如生成的密钥。遮蔽的值无法可靠地读回用于复制；请改用 `Input` 配合 `IsReadOnly`。
- 登录时校验密码的长度或字符类别。旧密码可能不符合新规则，用户会因为一条在设置密码时并不适用的规则而被锁定。
- 因为用户觉得圆点难以辨认就把遮蔽默认关闭。应提供切换按钮，并保留默认值。
- 使用看起来像真实值的占位文字，例如“••••••••”。用户可能会把它误认为已保存的密码。
- 把字段放在密集的工具栏或页头中。密码是表单值；搜索或筛选字段才应放在那里。

### 常见问题

**如何更改遮蔽字符？** `PasswordChar`（继承自文本框）用于设置字符，默认为 `•`。只有当平台或品牌需要不同符号时才更改；眼睛切换的工作方式不受影响。

**如何知道值是否已显示？** 读取 `IsRevealed`，或像示例那样监听 `PasswordField.IsRevealedProperty` 的变化。从代码设置 `IsRevealed` 会显示或隐藏值，并移动切换按钮。

**能否只翻译切换按钮名称而不改变标签？** 可以。在每个字段上设置 `ShowPasswordText` 和 `HidePasswordText`。标签和消息是独立的属性，因此翻译它们互不影响。

**新密码的长度规则应放在哪里？** 放在设置密码的页面上，使用 `PasswordStrength`，它会在用户输入时显示强度。`PasswordField` 只用于使用既有密码的页面。

## API 参考

<!-- api: PasswordField -->

## 无障碍

- `Label` 为字段命名。`Description` 与 `ErrorMessage` 会合并为它的帮助文字，因此屏幕阅读器先读名称，再读取值，最后读帮助信息。
- 显示切换按钮是带按下状态的切换按钮。它的名称在“显示密码”与“隐藏密码”之间切换，并通过 `IsOn` 报告按下状态。
- 密码默认被遮蔽。光标、选择、输入法、撤销与剪贴板快捷键都是原生 `TextBox` 的行为。
- 显示错误行时，错误会作为字段的错误被播报，同时字段被标记为无效。
- 显示值时，切换按钮保持完整对比度。

### 键盘

| 按键 | 操作 |
|---|---|
| `Tab` / `Shift+Tab` | 在字段、其显示切换按钮和下一个控件之间移动 |
| 切换按钮上的 `Enter` 或 `Space` | 显示密码；若已显示则隐藏 |
| `Left` / `Right`、`Home` / `End` | 按字符移动光标，或移到开头、结尾 |
| `Shift` + 以上任一按键 | 扩展选区 |
| `Ctrl+A`、`Ctrl+C`、`Ctrl+X`、`Ctrl+V`（macOS 上为 `Cmd`） | 全选、复制、剪切、粘贴 |
| `Ctrl+Z` / `Ctrl+Y`（macOS 上为 `Cmd+Z` / `Cmd+Shift+Z`） | 撤销、重做 |
| 字段中的 `Enter` | 字段本身不处理；与 `Input` 一样，由窗口中的默认按钮接收 |

## 动效

- 状态变化时，眼睛上的斜线沿图标绘制出来；眼睛图标从不被替换为第二个图标。
- 在圆点与字符之间切换时，文字在原位完成过渡：起始为 35% 不透明度并带轻微模糊，然后变为完全不透明且清晰对焦。
- 按下切换按钮时它会轻微缩小。辅助行与错误行的表现与 `Input` 相同。
- 减弱动效时，斜线立即绘制完成，并移除模糊与按下缩放。关闭动效时同样如此。

| 变化 | 完整 | 减弱 | 关闭 |
|---|---|---|---|
| 显示或遮蔽 | 斜线以标准补间动画划过眼睛，其下方的轮廓被切开，斜线离开时轮廓恢复 | 斜线立即绘制或移除 | 立即完成 |
| 圆点与字符切换 | 文字从 35% 不透明度与 2 px 模糊过渡到清晰 | 无过渡效果 | 无过渡效果 |
| 切换按钮按下 | 按住时缩放至 96% | 无缩放 | 无缩放 |
| 辅助文字行与错误行 | 与 `Input` 相同：该行以平滑弹簧展开，变化的词向上浮现 | 与 `Input` 相同 | 与 `Input` 相同 |

## 相关组件

- `PasswordStrength`：用于设置新密码并显示其强度。
- `Input`：非机密文本。
- `SearchField`：带清除控件的查询。

| 组件 | 适用场景 |
|---|---|
| `PasswordStrength` | 用户设置新密码，需要查看其强度和规则 |
| `Input` | 值不是机密，或机密需要读回并复制 |
| `SearchField` | 文字是用于筛选页面内容的查询 |
| `CopyButton` | 显示只读的生成值，并由用户复制 |
| `Textarea` | 值是较长的文字，而不是简短的机密 |
