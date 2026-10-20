---
title: 密码框
summary: 带有可见显示切换控件的敏感文本输入。
source-hash: e883e07b8a43
---

## 何时使用

- 登录表单以及用户输入已有密码等场景。
- 需要与第一次输入进行比对的确认字段。
- 默认隐藏、但可按需查看的值。

## 何时不用

- 用户正在设置的新密码：用 `PasswordStrength`，它会显示密码强度。
- 非机密文本：用 `Input`。
- 搜索关键词：用 `SearchField`。

## 用法

字段内的眼睛按钮可以显示或隐藏文本。它的名称会被辅助技术读取。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:PasswordField Label="密码"
                       Placeholder="请输入密码"
                       Description="点击显示按钮可核对你输入的内容。"
                       Width="360"
                       HorizontalAlignment="Left" />
</UserControl>
```

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

### 确认

当两个值不一致时，从第二个字段设置 `ErrorMessage`。将其设为 `null` 即可清除。

```csharp
void Check() => confirm.ErrorMessage = !string.IsNullOrEmpty(confirm.Text) && confirm.Text != first.Text
    ? "两次输入的密码还不一致。"
    : null;

first.TextChanged += (_, _) => Check();
confirm.TextChanged += (_, _) => Check();
```

### 切换按钮名称

切换按钮的名称来自 `ShowPasswordText` 和 `HidePasswordText`。设置它们即可翻译文案。

```xml
<arc:PasswordField Label="Mot de passe"
                   ShowPasswordText="Afficher"
                   HidePasswordText="Masquer" />
```

### 尺寸与状态

`Size` 决定外框高度：`Small` 为 36，`Medium`（默认值）为 44。

```xml
<arc:PasswordField Label="小" Size="Small" Text="secret" />
<arc:PasswordField Label="禁用" Text="secret" IsEnabled="False" />
```

## API 参考

<!-- api: PasswordField -->

## 无障碍

- `Label` 是字段的名称。`Description` 与 `ErrorMessage` 合并为它的辅助文字。
- 显示切换按钮是带按下状态的切换按钮，其名称在“显示密码”和“隐藏密码”之间切换。
- 切换按钮上按 Enter 或空格可显示或隐藏密码。Tab 键可将焦点移入和移出该按钮。
- 密码默认被遮蔽。光标、选择、输入法、撤销与剪贴板快捷键都是原生 `TextBox` 的行为。

## 动效

- 状态切换时，眼睛图标上的斜线会沿图标绘制出来。
- 首次切换之后，文字从轻微模糊逐渐变清晰。
- 按下切换按钮时会轻微缩小。辅助文字行与错误行的行为与 `Input` 相同。
- 减弱动效时，斜线立即绘制完成，模糊与按下缩放都会移除。关闭动效时同样如此。

## 相关组件

- `PasswordStrength`：用于设置新密码并显示其强度。
- `Input`：非机密文本。
- `SearchField`：带清除控件的搜索。
</content>
