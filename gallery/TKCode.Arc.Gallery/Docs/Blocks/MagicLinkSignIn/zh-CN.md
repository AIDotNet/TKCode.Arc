---
title: 邮件链接登录
summary: 通过邮件链接免密码登录：输入邮箱地址，在“查看收件箱”页面等待重发倒计时，链接过期后发送新链接。
source-hash: 6d46a74e6366
---

<Demo name="Preview" />

## 何时使用

`MagicLinkSignIn` 让人无需密码即可登录。用户输入邮箱并请求链接；卡片随即变成“Check your inbox”，显示他们的地址、一个在小倒计时圆环上等待 `ResendDelay` 的重发按钮，以及返回修改地址的入口。当你的应用打开一个过期的链接时，显示过期状态：一个按钮即可向同一地址发送新链接。你的应用在 `LinkRequested` 中发送邮件，或通过异步的 `LinkSender` 发送；后者会让按钮显示加载圈，发送失败时显示失败信息。

- **大多数人很少登录的产品**：人们常常忘记密码。
- **邀请和首次登录**：无论如何都需要确认邮箱地址。
- **另一种登录方式**：与密码或单点登录按钮并列。

## 何时不用

- **一天使用很多次的应用**：密码或通行密钥比每次打开邮箱更快。
- **在同一屏幕上输入验证码**：使用 `OtpInput`。
- **带第三方登录和密码的完整登录页**：使用 `Login` 或 `SignIn`。

## 用法

```csharp
signIn.LinkSender = async request =>
{
    var response = await api.SendMagicLinkAsync(request.Email);
    return response.IsSuccessStatusCode;
};
// When the app opens a link that is too old:
signIn.ShowExpired();
```

`State` 是双向的，路由也可以绑定它。地址会在用户离开输入框或按下按钮时检查，而不是在输入过程中。

<Demo name="Expired" />

## API 参考

<!-- api: MagicLinkSignIn -->

## 无障碍

- 在邮箱输入框中按 Enter 发送链接；在过期状态下按 Enter 发送新链接。
- 每次状态改变后，焦点移到该状态的标题（或回到输入框），并读出标题。错误和“A new link is on its way”也会被读出。
- 重发按钮在等待期间被禁用，按钮文字说明还剩多久。

## 动效

- 状态之间以短暂上移切换，卡片高度以弹簧跟随；“Check your inbox”出现时纸飞机从左下方飞入，倒计时圆环随等待结束而变空。
- 减少动效时状态交叉淡化，纸飞机直接出现。关闭动效时一切立即改变。

## 响应式

- 卡片最宽 420 px，在手机上占满宽度。小于 360 px 时内边距收紧。
- 按钮始终占满卡片宽度，方便点按。

## 相关组件

- `OtpInput` 用于输入验证码，`TwoFactorSetup` 用于登录时的第二步验证。
- `Login`、`LoginCentered` 和 `LoginImmersive` 用于完整的登录页。
