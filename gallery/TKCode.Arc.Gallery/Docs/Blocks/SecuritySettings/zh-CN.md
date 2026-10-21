---
title: 安全设置
summary: 安全检查、修改密码、两步验证和登录会话，均可原位处理。
source-hash: 6cf960abf5f9
---

<Demo name="Overview" />

## 何时使用

- 账户的安全页面：在一处检查账户的保护程度，并补齐缺失的项目。
- 无需离开页面即可修改密码，输入新密码时同步显示强度。
- 开启或关闭两步验证方式、添加通行密钥，以及查看恢复码。
- 查看账户在哪些设备上登录，并退出丢失或不认识的设备。

## 何时不用

- 登录或注册：使用登录表单。本区块假定用户已经登录。
- 工作区范围的策略（强制两步验证、允许的登录方式）属于管理员设置，最好在设置页中以 `SettingsGroup` 设置行的形式呈现。
- 表单中单独的一个密码输入框：直接使用 `PasswordField` 或 `PasswordStrength`。

## 用法

设置账户的状态后，区块会以四张卡片展示：安全检查、密码、两步验证和登录会话。所有操作都原位完成。开关会双向写回 `IsAuthenticatorEnabled` 与 `IsTextMessageEnabled`。密码表单、“添加通行密钥”、恢复码和退出登录都会通过路由事件通知宿主。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:SecuritySettings IsAuthenticatorEnabled="True"
                          PhoneNumber="+31 6 2045 4821"
                          PasskeyCount="2">
        <arc:SecuritySettings.Sessions>
            <arc:SecuritySession Device="MacBook Air" Client="Firefox" Location="Rotterdam, NL"
                                 Kind="Laptop" IsCurrent="True" />
            <arc:SecuritySession Device="Pixel 9" Client="Larkspur app" Location="Rotterdam, NL"
                                 Kind="Phone" LastActive="2026-10-20T08:00:00+02:00" />
        </arc:SecuritySettings.Sessions>
    </arc:SecuritySettings>
</UserControl>
```

时间以相对时间显示，计算基准为 `Clock`。`Clock` 是一个 `TimeProvider`，默认为系统时钟，区块显示期间每 30 秒重新读取一次。测试时可传入固定时钟：

```csharp
sealed class FixedClock(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now.ToUniversalTime();
}

security.Clock = new FixedClock(new DateTimeOffset(2026, 10, 20, 12, 0, 0, TimeSpan.FromHours(2)));
```

### 安全检查

安全检查统计四项保护措施：密码（`HasPassword`）、通过任一方式的两步验证、至少一个通行密钥（`PasskeyCount`），以及恢复码（`RecoveryCodesCreatedAt`）。`ProtectionScore` 即为该数量，范围为 0 到 4。每项保护措施在进度条上各占一段，盾牌的色调随分值变化：4 分为成功色，2 或 3 分为强调色，低于 2 分为警告色。`Sections` 决定显示哪些卡片；此处只显示安全检查和两步验证。开启某种验证方式或添加通行密钥后，进度条随之填充。

<Demo name="Checkup" />

页面自行显示摘要时，可用 `IsSummaryVisible="False"` 隐藏安全检查。

### 修改密码

“修改密码”会在该设置行下方展开一个表单，包含：当前密码、带强度计和规则列表的新密码，以及“取消”和“更新密码”按钮。“更新密码”需要填写当前密码，且新密码的强度至少为“中等”（`MinimumPasswordLevel`，等级 2）。按 Enter 提交，按 Escape 取消。提交时会引发 `PasswordChangeRequested`，并附带两个密码。将 `ErrorMessage` 留空即表示接受：表单关闭，`PasswordChangedAt` 设为时钟的当前时间，该设置行显示“刚刚更新”。若为其设置内容，则表示拒绝：错误信息显示在当前密码下方，焦点回到该处。本示例中当前密码为 “correct horse”。

<Demo name="ChangePassword" />

没有密码时（`HasPassword="False"`），该设置行提供“设置密码”，表单只要求输入新密码，此时被拒绝的提示显示在新密码下方。`OpenPasswordForm()`、`ClosePasswordForm()` 和 `SubmitPasswordChange()` 可在代码中执行相同的操作。

### 登录会话

会话按列表排列：当前设备排在最前，其余按最近活动时间排序。当前设备带有“此设备”徽标，且不能在此处退出登录。其他每个会话都有“退出登录”按钮。点击后，该行淡出并收起，会话从 `Sessions` 中移除，并引发 `SessionSignedOut`。“退出其他登录会话”会原位询问，确认后各行依次离开，随后 `OtherSessionsSignedOut` 携带已退出的会话发出通知。`SignOut(session)` 与 `SignOutOtherSessions()` 可在代码中执行相同的操作。只读列表（如数组）保持不变，已退出的行则改为隐藏。

<Demo name="Sessions" />

在窄版页面上，进度条会移到安全检查文字的下方，“退出其他登录会话”则移到列表下方的一条区域中。控件为开关、徽标或“退出登录”按钮的行，控件仍与文字并排。按钮较宽的行，则将控件堆叠在文字下方。

### 文本标签

`SecuritySettingsLabels` 包含所有内置文本。若要本地化，请整体替换该对象；需要带入值的文本则为函数。`Culture` 用于格式化日期。

```csharp
security.Labels = new SecuritySettingsLabels
{
    Culture = CultureInfo.GetCultureInfo("nl-NL"),
    PasswordHeader = "Wachtwoord",
    ChangePassword = "Wachtwoord wijzigen",
    SessionsHeader = "Waar je bent ingelogd",
    SignOut = "Afmelden",
    SignOutSession = device => $"{device} afmelden",
    SummaryCount = count => $"{count} van 4 beveiligingen aan"
};
```

## API 参考

<!-- api: SecuritySettings -->

**SecuritySession**（普通对象，可在 XAML 中编写）

| 属性 | 类型 | 说明 |
|---|---|---|
| `Device` | `string` | 设备名称，例如“MacBook Air”。 |
| `Client` | `string?` | 浏览器或应用，显示在设备名称之后。 |
| `Location` | `string?` | 设备登录的地点，例如“Rotterdam, NL”。 |
| `LastActive` | `DateTimeOffset` | 会话最后一次使用的时间，以相对时间显示，基准为 `Clock`。 |
| `Kind` | `SecurityDeviceKind` | `Desktop`、`Laptop`、`Phone`、`Tablet` 或 `Other`，决定显示的图标。 |
| `IsCurrent` | `bool` | 正在使用的设备：排在最前、标记为“此设备”，且不能在此处退出登录。 |

**事件参数**

| 类型 | 属性 | 说明 |
|---|---|---|
| `SecurityPasswordChangeEventArgs` | `CurrentPassword` | 输入的当前密码（没有密码时为空）。 |
| | `NewPassword` | 新密码。 |
| | `ErrorMessage` | 设置后即拒绝此次修改；表单保持打开并显示该信息。 |
| `SecuritySessionEventArgs` | `Session` | 已退出登录的会话。 |
| `SecuritySessionsEventArgs` | `Sessions` | 由“退出其他登录会话”退出的会话，按列表顺序排列。 |

**SecuritySettingsSections**（标志枚举）：`Summary`、`Password`、`TwoStep`、`Sessions`、`All`（默认）、`None`。

**SecuritySettingsLabels**：`Culture`，以及每个内置文本各一个属性，例如 `SummaryProtected`、`SummaryCount`、`PasswordChanged`、`ChangePassword`、`UpdatePassword`、`TextMessageDescription`、`PasskeyCount`、`SessionsHeader`、`SignOut`、`SignOutSession`、`SignOutOthers` 和 `SignOutOthersPrompt`。

## 无障碍

- 每个开关和按钮都有名称。开关以其设置行的标题（“身份验证器应用”）和说明作为名称。会话的按钮以其设备命名，例如“退出登录 ThinkPad X1”，徽标朗读为“此设备”。
- 安全检查进度条的名称为“已开启 {n}/4 项保护措施”。安全检查标题和密码设置行的说明都是实时区域（礼貌），因此“您的账户保护良好”和“刚刚更新”在内容变化时会被朗读。
- 打开表单时，焦点移至第一个字段。“更新密码”可用时，按 Enter 提交，按 Escape 取消。关闭后，焦点回到“修改密码”。被拒绝的修改会将焦点移至显示错误的字段，并选中其中的文字。
- 聚焦的“退出登录”行离开时，焦点移至下一个会话的按钮；若没有，则移至上一个。
- “退出其他登录会话”遵循 `ConfirmMorph`：先询问，按 Escape 或“取消”即保留会话。没有其他会话时，它会被禁用。
- Tab 停靠点按卡片从上到下排列。没有手机号码时，短信开关会被禁用，该设置行会说明原因。

## 动效

- 会话行通过 `PresenceHost` 离开：会话被移除前，它们以平滑的弹簧淡出，同时高度收起。“退出其他登录会话”让每一行比上一行晚 35 毫秒开始。
- 密码表单通过 `SpringSizeDecorator` 以弹簧方式展开和收起。密码设置行的说明以模糊切换方式变化。“退出其他登录会话”会在原位变形为询问和结果。
- 安全检查标题会模糊过渡到新文本。数字随分值向上或向下滚动。进度条的分段和盾牌会渐变为新的颜色。
- 减少动效时保留短暂的淡入淡出，取消弹簧、模糊和滚动。关闭动效时，所有内容一次性就位，已退出的行也会立即离开列表。

## 相关组件

- `SettingsGroup` 与 `SettingsRow` 用于构建卡片和设置行。其他设置区块也会使用它们：设置页、设置命令面板、账单概览、用量计费和取消订阅流程。
- 密码表单使用 `PasswordField` 与 `PasswordStrength`，两步验证方式使用 `Switch`，当前设备使用 `Badge`，设置行的操作使用 `Button`，退出其他所有会话则使用 `ConfirmMorph`。
