---
title: 密码强度
summary: 在输入新密码时显示其强度。
source-hash: 1b9e076740df
---

<Demo name="Create" />

## 何时使用

`PasswordStrength` 是用于让用户设置新密码的字段：SaaS 产品的注册步骤、账户设置中的修改密码页面，或从邮件链接打开的重置表单。它是一个带掩码的密码字段，附带四段式强度计、强度文字和规则清单，因此用户在输入时就能看到还缺什么，而不是在第一次提交失败之后才知道。

- **工作区或客户门户注册**：新的所有者为负责账单和发票的账户选择密码。强度计和规则清单在首次提交之前就说明了策略，从而减少被拒绝的表单。
- **在账户设置中修改密码**：用户已经知道当前密码，因此只有新密码处才需要规则。当前密码保留在普通的 `PasswordField` 中，这里展示规则。
- **通过邮件链接重置密码**：用户到达页面时没有上下文，规则清单是页面上唯一的指引。与注册表单使用同一套规则列表，使策略在各处的表述保持一致。
- **管理员设置临时密码**：创建团队成员账户的管理控制台，可以在临时密码保存到目录之前显示它是否符合策略。
- **规则更严格的内部工具**：要求 16 个字符的金融后台可以传入自己的 `Rules` 和 `ShortLength`，使规则清单与服务端执行的规则一致。

## 何时不用

- **登录已有账户**：用 `PasswordField`。用户已经有密码，强度计和规则清单只会增加干扰，还会把策略暴露给正在猜测密码的人。
- **并非密码的测量量**，例如导出或备份的进度：用 `Progress`。
- **非机密文本**，例如工作区名称或密钥标签：用 `Input`。强度计对不是密码的值没有意义。
- **系统生成的机密**，例如只显示一次的 API 令牌：用只读的 `Input` 加一个复制操作来显示。用户从不选择这个值，因此无从评分。
- **需要泄露或字典检查的策略**：内置估算器只统计规则，并在设备上运行。可以通过 `Scorer` 传入 `IPasswordStrengthScorer` 进行本地检查，或在服务端执行检查，并用 `ErrorMessage` 显示其结果。

## 用法

强度计、强度文字和规则清单会在每次按键时更新。评分在设备上使用默认规则进行：最少 12 个字符，包含大写与小写字母、数字和符号。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:PasswordStrength Label="新密码"
                          Placeholder="请设置密码"
                          Width="400"
                          HorizontalAlignment="Left" />
</UserControl>
```

`PasswordStrength` 派生自 `PasswordField`，而 `PasswordField` 派生自基于 Avalonia `TextBox` 的 Arc 字段基类。它从文本字段继承了掩码文本、显示切换（`IsRevealed`）、`Label`、`Description`、`ErrorMessage`、`Size` 和 Arc 外框，并在字段下方增加强度计、强度文字和规则列表。光标、选择、撤销、输入法组字和剪贴板快捷键都是原生 `TextBox` 的行为。

### 结构

1. **标签**（`Label`）：14 px、中等字重，位于外框上方 8 px，与 `Input` 相同。它是字段的无障碍名称。只有当可见的标题已经为字段命名时，才用 `IsLabelVisible="False"` 隐藏它。
2. **外框**：Arc 外框，左侧内边距 16 px，右侧 5 px。前缀、文字和后缀之间相距 4 px。
3. **文字与占位文字**：掩码的密码。字段为空时，`Placeholder` 以弱化文字颜色显示。
4. **显示切换按钮**：位于外框末端的 34 px 方形按钮，圆角 13 px，内含 18 px 的眼睛图标。按下时缩放至 94%；显示密码时，它会报告其开启状态。
5. **说明行与错误行**（`Description`、`ErrorMessage`）：与 `Input` 共用的辅助行和错误行，位于外框下方，12 px 文案。错误行会整体替换其消息。
6. **强度计行**：位于字段或其错误行下方 12 px。四段等宽的 4 px 高色段，间隔 5 px，底色为边框颜色。
7. **强度文字**：强度计行末端至少 72 px 的区域，随本地化文字增宽，与强度计相距 12 px，右对齐，使用小号正文的中等字重。字段为空时它也为空。
8. **规则列表**（`Rules`）：位于强度计下方 16 px，规则之间间隔 6 px。每行有一个 16 px 的圆圈，规则满足时圆圈带淡淡的成功色并显示对勾，之后是规则文字。未满足且带有 `Remaining` 函数的规则，还会以弱化颜色显示滚动计数，例如“还差 4 个”。

### 强度等级

等级由满足规则的比例决定，分为四个档位。在 `ShortLength` 个字符以下，无论规则如何，等级都固定为 1，因此过短的密码永远不会显得很强。

| 等级 | 强度文字 | 强度计 | 色调 |
|---|---|---|---|
| 0 | 无（字段为空） | 无色段；状态显示“尚未输入密码” | 底色与空色段 |
| 1 | `弱`，或在 `ShortLength` 以下时为 `太短` | 1 段 | 危险色 |
| 2 | `一般` | 2 段 | 警告色 |
| 3 | `良好` | 3 段 | 成功色 |
| 4 | `很强` | 4 段 | 成功色 |

使用默认的四条规则时，满足一条得等级 1，满足两条得等级 2，满足三条得等级 3，全部满足得等级 4。若自定义规则为五条，则 1 对应 1、2 对应 2、3 对应 2、4 对应 3、5 对应 4，因为比例会四舍五入到最接近的档位。`良好` 与 `很强` 使用同一种成功色，因此强度计由红变黄再变绿，不会用颜色区分最上面两档。

当策略的最小长度高于默认值 8 时，将 `ShortLength` 设为该最小长度。这样，长度在 8 个字符与策略最小值之间的密码会显示 `弱`，且长度规则仍未满足，这不如 `太短` 清楚。

### 绑定值

`Text` 是双向属性。将其绑定到视图模型中的字符串，并把同一视图模型中的错误绑定到 `ErrorMessage`。当用户修改密码时，视图模型会清除该错误，以免旧的服务端消息一直留在屏幕上。

```csharp
public partial class PasswordChangeViewModel : ObservableObject
{
    // 保存成功时返回 null，否则返回要在字段上显示的消息。
    private readonly Func<string, Task<string?>> _changePassword;

    public PasswordChangeViewModel(Func<string, Task<string?>> changePassword)
    {
        _changePassword = changePassword;
    }

    [ObservableProperty]
    private string _newPassword = string.Empty;

    [ObservableProperty]
    private string? _newPasswordError;

    partial void OnNewPasswordChanged(string value) => NewPasswordError = null;

    [RelayCommand]
    private async Task SetPasswordAsync()
    {
        NewPasswordError = await _changePassword(NewPassword);
    }
}
```

```xml
<StackPanel Spacing="16">
    <arc:PasswordStrength Label="新密码"
                          Text="{Binding NewPassword, Mode=TwoWay}"
                          ErrorMessage="{Binding NewPasswordError}"
                          Width="400"
                          HorizontalAlignment="Left" />
    <arc:Button Content="更新密码"
                Command="{Binding SetPasswordCommand}"
                IsDefault="True"
                HorizontalAlignment="Left" />
</StackPanel>
```

`Text` 默认为双向。`IsRevealed` 只有在绑定中指定 `Mode=TwoWay` 时才是双向的，这样视图模型可以记住用户是否选择了显示密码。只读值 `Level`、`StrengthLabel` 和 `Score` 是单向的：应在代码中读取，而不要放在会写回的绑定中。`StrengthChanged` 是唯一的事件；该控件没有命令。

### 响应强度变化

每次评分后的变化都会触发 `StrengthChanged`。`Level` 的取值从 0（为空）到 4（很强），`Label` 为强度文字，`Score.MetRuleIds` 列出已满足的规则。只有当等级、短密码状态或满足的规则集合发生变化时，事件才会触发。在一个已满足大小写规则的密码后面再输入一个小写字母，不会触发任何事件；如果需要每次按键的结果，请在自己的处理程序中读取 `Score` 或 `Level`。

```csharp
create.StrengthChanged += (_, e) =>
{
    result.Text = e.Level == 0
        ? "强度：尚无输入"
        : $"强度：{e.Label}（等级 {e.Level}，已满足 {e.Score.MetRuleIds.Count} 条规则）";
};
```

### 自定义规则

将规则列表传给 `Rules`。每个 `PasswordRule` 都有一个 id（会在 `Score.MetRuleIds` 中报告）、清单中显示的文字、一个判定函数，以及可选的 `Remaining` 函数。行的顺序与列表顺序一致。赋值 `Rules` 会重建清单并重新评分，因此应在页面加载时赋值一次，而不是每次按键都赋值。

`PasswordRules.Create` 使用你自己的最小长度、文字和字符计数来构建内置规则。当策略只改变数字时使用它；当某条规则性质不同时，则自己编写 `PasswordRule`。

<Demo name="CustomRules" />

```csharp
var rules = new List<PasswordRule>
{
    new("length", "至少 10 个字符", p => p.Length >= 10),
    new("digit", "包含数字", p => p.Any(char.IsDigit))
};
```

`remaining` 参数是可选的。不传时，未满足的规则只显示其文字；传入后，只要函数返回值大于 0，该行就会显示滚动计数。内置的长度规则使用了它，因此在密码足够长之前，清单会显示“还差 4 个”。

### 校验

强度计和规则清单在用户输入时提供指引，它们从不阻止输入，也不是错误。决定密码能否被接受的策略检查应放在提交时进行，并用 `ErrorMessage` 显示其结果。

| 规则 | 何时检查 | 显示在哪里 |
|---|---|---|
| 强度与规则 | 输入时 | 强度计、强度文字和规则清单 |
| 最小长度与字符类别 | 输入时；字段为空时在提交时检查 | 规则清单；只在提交后才用 `ErrorMessage` |
| 与第一次输入的密码一致的确认 | 确认字段失去焦点时 | 确认 `Input` 上的 `ErrorMessage` |
| 重复使用当前密码或已泄露的密码 | 提交时，由服务端判断 | 新密码字段上的 `ErrorMessage` |
| 服务端拒绝修改 | 请求返回后 | `ErrorMessage`；值变化时清除 |

```csharp
// 在对应字段上显示服务端的回应，用户编辑该值后清除。
newPassword.ErrorMessage = result.IsSuccess ? null : result.Error;
newPassword.TextChanged += (_, _) => newPassword.ErrorMessage = null;
```

### 错误

设置 `ErrorMessage` 可将字段标记为无效。与当前已显示内容不同的新消息会使字段晃动一次。若再次提交相同的文字，字段不会晃动，因此当用户再次提交并需要反馈时，应设置不同的消息，或先清除错误再重新设置。

<Demo name="Errors" />

### 使用自定义评分器评分

`Scorer` 接受一个 `IPasswordStrengthScorer`。`Text` 每次变化时，控件都会调用 `ScoreAsync` 并传入当前规则，然后显示返回的 `PasswordStrengthScore`。设置了 `Scorer` 后，控件不再使用 `ShortLength` 和 `CharacterCounter`；由评分器决定等级、短密码状态和满足的规则。如果某个结果在用户新的按键之后才返回，它会被忽略，因此较慢的评分器不会显示较早密码的强度。

```csharp
public sealed class BreachCheckedScorer : IPasswordStrengthScorer
{
    private readonly Func<string, CancellationToken, ValueTask<bool>> _isKnownBreached;

    public BreachCheckedScorer(Func<string, CancellationToken, ValueTask<bool>> isKnownBreached)
    {
        _isKnownBreached = isKnownBreached;
    }

    public async ValueTask<PasswordStrengthScore> ScoreAsync(
        string password, IReadOnlyList<PasswordRule> rules, CancellationToken cancellationToken)
    {
        var score = PasswordStrengthEstimator.Default.Score(password, rules);
        if (score.Level > 0 && await _isKnownBreached(password, cancellationToken))
        {
            return new PasswordStrengthScore(1, score.MetRuleIds, false);
        }
        return score;
    }
}

var knownBreached = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Welcome-2026", "Password-123" };
field.Scorer = new BreachCheckedScorer((password, _) => ValueTask.FromResult(knownBreached.Contains(password)));
```

委托是应用自己的检查；上面的集合充当本地泄露列表的替身。若评分器在 UI 线程上运行，请保持其快速：控件会等待结果，因此阻塞调用会延迟触发它的那次按键。只在停顿之后再调用服务端，或将检查保留在服务端，并用 `ErrorMessage` 显示其结果。

### 消息与本地化

控件显示的每个字符串都在 `Messages` 中，它是一个带英文默认值的 `PasswordStrengthMessages` 记录。替换整个记录，或用 `with` 修改其中一个值，即可翻译强度文字、显示切换按钮的名称、无障碍名称以及屏幕阅读器播报的摘要。

```csharp
field.Messages = field.Messages with
{
    Weak = "Schwach",
    Fair = "Mittel",
    Good = "Gut",
    Strong = "Stark",
    TooShort = "Zu kurz",
    RemainingSuffix = "fehlen",
    SummaryFormat = "Stärke: {0}. {1} von {2} Anforderungen erfüllt."
};
```

`SummaryFormat` 按顺序接受三个占位符：强度文字、满足的规则数和规则总数。`RemainingSuffix` 接在规则清单的滚动计数之后，因此在示例中，“4 more”会变成“4 fehlen”。

### 示例：修改密码表单

用户在设置页面中修改工作区密码的界面。字段在其说明中展示了策略，规则清单在用户输入第一个字母之前就可见；截图捕捉的是部分输入后的状态：四条规则中满足了三条，长度规则仍显示“还差 1 个”。提交按钮写明了操作，位于表单末尾，按钮文字包含动词和对象。强度计不会阻止按钮；提交时由服务端决定。

![修改密码表单，密码良好，有一条长度规则未满足](images/PasswordChangeForm.png)

```xml
<StackPanel Width="560" Spacing="20">
    <TextBlock Text="Change password"
               FontSize="18"
               FontWeight="SemiBold"
               Foreground="{arc:ArcDesignTokenResource Foreground}" />
    <TextBlock Text="Use this password for the Acme Analytics workspace."
               FontSize="14"
               TextWrapping="Wrap"
               Foreground="{arc:ArcDesignTokenResource TextSecondary}" />
    <arc:PasswordStrength Label="New password"
                          Text="Ledger-2026"
                          Description="Use at least 12 characters, with a symbol."
                          Placeholder="Choose a password" />
    <StackPanel Orientation="Horizontal"
                HorizontalAlignment="Right"
                Spacing="12">
        <arc:Button Content="Cancel" Variant="Secondary" />
        <arc:Button Content="Update password" />
    </StackPanel>
</StackPanel>
```

### 文案规范

用字段的用途为其命名，并以默认标签所用的肯定形式陈述每条规则。错误提示要说明应如何修改。

| 不要写 | 改为 |
|---|---|
| 密码 | 新密码 |
| 密码无效 | 至少使用 12 个字符，并包含一个符号。 |
| 弱密码 | 添加一个符号，让密码更强。 |
| 请输入强密码 | 请设置至少 12 个字符的密码。 |
| 提交 | 更新密码 |

使用阿拉伯数字表示计数，不用感叹号。说明文字只用一句话陈述策略。

### 最佳实践

**推荐**

- 从用户第一次按键起就显示规则清单。用户需要在猜测之前知道规则；只在提交失败后才出现的规则，会多耗一次往返。
- 用 `PasswordRules.Create` 构建一套规则列表，并将其赋给产品中的每个密码表单。若强度计接受而服务端拒绝某条规则，就会导致提交失败。
- 在服务端执行相同的规则。强度计是设备端的估算，客户端检查可以被绕过。
- 当策略的最小长度高于 8 时，把 `ShortLength` 设为该最小长度，使短密码显示“太短”而不是“弱”。

**避免**

- 因强度计显示“弱”而禁用提交按钮。应在提交时校验，并在字段上显示原因。
- 在登录中使用本控件。此时规则只会向猜测密码的人暴露策略。
- 每次按键都赋值 `Rules` 或 `Messages`。赋值 `Rules` 会重建清单并重新评分。
- 阻塞 UI 线程的评分器。控件会在每次变化时等待 `ScoreAsync`，因此阻塞调用会延迟触发它的那次按键。

### 常见问题

**三条规则已满足，为什么强度计仍显示“太短”？** 在 `ShortLength`（默认为 8）以下，无论满足哪些规则，等级都固定为 1，而规则清单的各行仍显示各自的状态。要改变阈值，请修改 `ShortLength`。

**为什么我最后一次按键时 `StrengthChanged` 没有触发？** 它只在等级、短密码状态或满足的规则集合发生变化时才触发。需要每次按键的结果时，请在你自己的 `TextChanged` 处理程序中读取 `Score` 或 `Level`。

**长度规则与 `CharacterCounter` 的结果不一致。** `CharacterCounter` 只改变 `ShortLength` 以及控件默认构建的估算器。默认规则按码位计数。请用 `PasswordRules.Create(countCharacters: ...)` 构建规则列表，使规则的计数方式与之一致。

**`Scorer` 会覆盖 `ShortLength` 吗？** 会。设置了 `Scorer` 后，控件直接使用它，因此 `ShortLength` 与 `CharacterCounter` 不再生效。返回的 `PasswordStrengthScore` 决定等级和“太短”状态。

## API 参考

<!-- api: PasswordStrength -->

### 配套类型

`StrengthChanged` 传递 `PasswordStrengthChangedEventArgs`。`Score` 与事件参数都携带此结果：

| 成员 | 类型 | 含义 |
|---|---|---|
| `Level` | `int` | 空密码为 0，之后从 1（弱或太短）到 4（很强） |
| `MetRuleIds` | `IReadOnlyList<string>` | 密码满足的规则的 id |
| `IsShort` | `bool` | 密码短于 `ShortLength` 时为 True；强度计停留在等级 1 |

`PasswordRule` 由一个 id、一段文字、一个判定函数以及一个可选的剩余数量构成。

| 构造参数 | 类型 | 含义 |
|---|---|---|
| `id` | `string` | 在 `MetRuleIds` 中报告的稳定 id |
| `label` | `string` | 规则清单中显示的文字 |
| `test` | `Func<string, bool>` | 密码满足规则时为 True |
| `remaining` | `Func<string, int>?` | 仍缺少的字符数，以滚动计数显示；可选 |

## 无障碍

- `Label` 为字段命名。规则清单和错误是其辅助文字的一部分。
- 强度计以只读进度条的形式呈现，值从 0 到 4。其名称为 `Messages.MeterLabel`，其项状态为强度文字；字段为空时为“尚未输入密码”。
- 每个规则行以其文字命名，后接“，已满足”或“，未满足”。规则清单本身的名称为 `Messages.RulesLabel`。
- 每次变化后，强度与满足规则的摘要会以礼貌的方式播报，例如“强度：良好。已满足 3 条规则，共 4 条。”
- 显示切换按钮在两种状态下都保持同一名称“显示密码”。其开启状态告诉辅助技术密码正在显示。
- 错误行与 `Input` 相同。其消息整体替换，因此屏幕阅读器读出的是一条完整的消息。
- 光标、选择、输入法、撤销与剪贴板快捷键都是原生 `TextBox` 的行为。

### 键盘

| 按键 | 操作 |
|---|---|
| `Tab` / `Shift+Tab` | 在字段与显示切换按钮之间移动，二者按文字在 Tab 顺序中的位置排列 |
| 显示切换按钮上的 `Enter` 或 `Space` | 显示或隐藏密码 |
| 文本按键、`Ctrl+Z` / `Ctrl+Y`（macOS 上为 `Cmd`） | 与 `TextBox` 一样进行编辑、撤销和重做 |

## 动效

- 四段强度计以波浪顺序填充并变换色调。强度文字随等级上升或下降。
- 规则满足时会以小幅放大出现，并在其标记上画出一条线。剩余规则的计数会滚动。
- 新错误出现时，字段晃动一次。
- 减弱动效时，晃动、波浪和滚动计数都会移除，数值立即变化。关闭动效时同样如此。

| 变化 | 完整 | 减弱 | 关闭 |
|---|---|---|---|
| 强度计色段填充或清空 | 每段以平滑弹簧滑入，按波浪顺序交错出现 | 立即完成 | 立即完成 |
| 强度文字 | 强度提升时上升，下降时下落，从柔和的模糊中出现 | 交叉淡化 | 立即完成 |
| 规则标记 | 圆圈以灵敏弹簧放大，对勾稍后描绘；取消满足时收回更快 | 立即完成 | 立即完成 |
| 规则文字 | 规则满足时，其颜色渐变为前景色 | 立即完成 | 立即完成 |
| 剩余计数 | 数字滚动到新值 | 立即完成 | 立即完成 |
| 新错误 | 字段晃动一次，并以弹簧稳定下来 | 不晃动 | 不晃动 |

## 相关组件

- `PasswordField`：用于登录，此时密码是核对而非设置。
- `Input`：用于非机密文本。
- `Progress`：用于不是密码的测量值。

| 组件 | 适用场景 |
|---|---|
| `PasswordField` | 用户用已有的密码登录 |
| `Input` | 值是非机密文本，例如名称或密钥标签 |
| `Progress` | 需要进度条来显示测量值，例如导出或上传 |
| `Field` + `FieldFrame` | 你要自己构建编辑器，并希望它具有相同的标签、外框和消息行 |
| `Textarea` | 长备注或描述需要多行书写 |
| `Button` | 表单需要提交操作；用 `IsDefault="True"` 支持 Enter |
