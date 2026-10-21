---
title: 密码强度
summary: 在输入新密码时显示其强度。
source-hash: e5826be75116
---

<Demo name="Create" />

## 何时使用

- 注册表单，以及用户选择新密码的任何界面。
- 修改密码表单，新密码必须满足一组规则。
- 用户需要看到哪些规则尚未满足的场景。

## 何时不用

- 登录已有账户：用 `PasswordField`，它没有强度计和规则列表。
- 不是密码的测量量，例如上传进度：用 `Progress`。
- 非机密文本：用 `Input`。

## 用法

强度计、强度文字和规则列表会在每次按键时更新。评分在本地进行，默认规则为：最少 12 个字符、包含大写与小写字母、数字和符号。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:PasswordStrength Label="新密码"
                          Placeholder="请设置密码"
                          Width="400"
                          HorizontalAlignment="Left" />
</UserControl>
```

### 绑定值

`Text` 是双向属性。绑定 `ErrorMessage` 可显示服务端或表单级别的错误。

```xml
<arc:PasswordStrength Label="新密码"
                      Text="{Binding NewPassword}"
                      ErrorMessage="{Binding NewPasswordError}" />
```

### 响应强度变化

每次评分后都会触发 `StrengthChanged`。`Level` 的取值从 0（为空）到 4（很强），`Score.MetRuleIds` 列出已满足的规则。

```csharp
create.StrengthChanged += (_, e) =>
{
    result.Text = e.Level == 0
        ? "强度：尚无输入"
        : $"强度：{e.Label}（等级 {e.Level}，已满足 {e.Score.MetRuleIds.Count} 条规则）";
};
```

### 自定义规则

将规则列表传给 `Rules`。可以用不同的最小长度构建默认规则，也可以编写自己的 `PasswordRule`，包含判定条件以及可选的剩余数量。

<Demo name="CustomRules" />

```csharp
var rules = new List<PasswordRule>
{
    new("length", "至少 10 个字符", p => p.Length >= 10),
    new("digit", "包含数字", p => p.Any(char.IsDigit))
};
```

少于 `ShortLength` 个字符时，强度计停留在第一段，并显示“太短”。

### 错误

设置 `ErrorMessage` 可将字段标记为无效。每出现一个新错误，字段会抖动一次。

<Demo name="Errors" />

## API 参考

<!-- api: PasswordStrength -->

## 无障碍

- `Label` 是字段的名称。规则列表与错误是它辅助文字的一部分。
- 强度计以 0 到 4 的数值报告，并以强度文字作为其文本。
- 用户输入时，强度与已满足规则的摘要会以礼貌方式播报。
- 显示切换按钮上按 Enter 或空格可显示或隐藏密码。光标、选择、输入法、撤销与剪贴板快捷键都是原生 `TextBox` 的行为。

## 动效

- 四段强度计按波浪顺序依次填充并变换色调。强度文字随等级上升或下降。
- 规则满足时会以小幅放大出现并在标记上划线；剩余规则的数量会滚动变化。
- 新错误出现时，字段抖动一次。
- 减弱动效时，抖动、波浪与数字滚动都会移除，数值立即变化。关闭动效时同样如此。

## 相关组件

- `PasswordField`：用于登录，此时密码是核对而非设置。
- `Input`：非机密文本。
- `Progress`：不是密码的测量值。
</content>
