---
title: 两步验证设置
summary: 开启两步验证登录：选择验证器应用或短信，扫描真实的二维码，输入六位验证码，并保存备用码。
source-hash: 93b6f679f201
---

<Demo name="Preview" />

## 何时使用

`TwoFactorSetup` 是用户开启两步验证登录时打开的卡片。它分四步完成，顶部有进度条：选择验证器应用或短信；扫描二维码（或输入二维码下方的密钥），或输入手机号码；输入六位验证码以证明可用；保存备用码。二维码是真实的：它包含一个带发行方、账号和密钥的 `otpauth://` 地址，由本区块中的编码器绘制，任何验证器应用都能扫描。没有 `CodeVerifier` 时，卡片自己按基于时间的验证码检查（RFC 6238，每步 30 秒，前后各允许一步的时间偏差）。

- **账号的安全设置**：与密码和已登录设备放在一起。
- **注册后必须完成的设置**：适用于要求每位成员使用两步验证的团队。
- **管理控制台**：方式必须先验证再保存。

## 何时不用

- **登录时要求输入验证码**：在登录页使用 `OtpInput`。
- **显示两步验证是否已开启**：使用 `SecuritySettings`，它列出各种方式及其状态。
- **无需密码的登录**：使用 `MagicLinkSignIn`。

## 用法

```csharp
setup.Issuer = "Lumenfield";
setup.AccountName = user.Email;
setup.Secret = server.CreateSecret(user);
setup.CodeVerifier = request => server.VerifyAsync(user, request.Code);
setup.TextMessageRequested += (_, e) => sms.SendCode(e.PhoneNumber);
setup.Completed += (_, e) => server.Enable(user, e.Method, e.BackupCodes);
```

`ComputeCode`、`GenerateSecret` 和 `GenerateBackupCodes` 可在服务端或测试中使用。“Download”会打开保存对话框，把备用码存为文本文件；处理 `BackupCodesSaving` 可以自行保存。

<Demo name="AppOnly" />

## API 参考

<!-- api: TwoFactorSetup -->

## 无障碍

- 每次切换步骤后，焦点移到接下来要操作的地方：验证码输入框、手机号码输入框或步骤标题，并读出当前步骤（“Step 2 of 4: Scan this code with your app”）。
- 二维码是一张以发行方和账号命名的图片；下方的密钥可以选中并复制。
- Enter 执行当前步骤的主要操作，Alt+左方向键返回；输入错误的验证码时会读出提示，提醒检查设备时间。

## 动效

- 步骤按前进方向滑入，卡片高度以弹簧跟随。二维码从左上角起以斜向条带淡入，最后一步的对勾会自己画出来。
- 减少动效时步骤交叉淡化，二维码和对勾立即显示。关闭动效时一切立即改变。

## 响应式

- 卡片最宽 560 px。小于 460 px 时，二维码移到说明上方，内边距收紧，按钮占满整行。
- 无论明暗主题，二维码都保持整像素的模块和浅色的留白区，方便相机读取。

## 相关组件

- `OtpInput`、`RadioCards`、`Stepper`、`Checkbox` 和 `CopyButton`：卡片由它们构成。
- `SecuritySettings` 用于总览页面，`MagicLinkSignIn` 用于无需密码的登录。
