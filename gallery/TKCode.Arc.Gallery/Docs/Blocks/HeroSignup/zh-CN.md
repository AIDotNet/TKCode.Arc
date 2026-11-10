---
title: Hero signup
summary: 落地页首屏：大标题加一个邮箱输入框，就地校验、提交并确认，背后是安静的点阵，下方有一排头像作为证明。
source-hash: 93e40607d751
---

<Demo name="Preview" />

## 何时使用

`HeroSignup` 是落地页的第一屏，唯一的目的是收集邮箱地址：候补名单、内测、抢先体验。圆角小标签说明当下的阶段（“内测中”），大标题给出承诺，一句话加以说明，一个输入框和一个按钮完成剩下的事。地址会在输入时校验，通过你的处理函数提交，然后表单原地换成简短的确认，访客无需离开页面顶部。表单下方是一排生成的头像和一行证明文字（“已有 2,400 个产品团队加入”），背后淡淡的点阵和柔和的强调色光晕让这一屏有层次，又不需要图片。

- **候补名单或内测页**，邮箱地址就是转化。
- **发布前的产品页**，在上线前收集意向。
- **活动首屏**，旁边配一张图片，内容靠起始边对齐。

## 何时不用

- **登录或注册账户**：请使用 Auth 分组的区块，它们处理密码和登录方式。
- **有往期内容可展示的电子报**：请使用 `NewsletterSignup`。
- **有多个行动按钮、没有表单的首屏**：请使用 `CtaSection`。

## 用法

```xml
<arc:HeroSignup x:Name="Hero" Title="Release notes your whole team reads" ButtonText="Join the waitlist" />
```

```csharp
Hero.SubmitAsync = async (email, token) =>
{
    await http.PostAsJsonAsync("/waitlist", new { email }, token);   // throw to show FailedMessage and keep the address
};
Hero.Submitted += (_, e) => analytics.Track("waitlist", e.Email);
```

没有处理函数时会模拟提交（900 毫秒，总是成功）。`Phase` 报告 `Idle`、`Sending` 或 `Done`；`SubmittedEmail` 保存已接受的地址，`Reset()`（“Use a different email”按钮）恢复空表单。`Submit()` 可在代码中校验并提交地址。

### 校验

输入框为空时在下方显示 `EmptyMessage`，地址格式不对时显示 `FormatMessage`。第一次提交之前，继续输入会清除错误；之后每次按键都会重新校验。提交失败时保留地址并显示 `FailedMessage`。

### 对齐

<Demo name="StartAligned" />

`Alignment="Start"` 让小标签、标题、说明、表单和证明行都靠起始边对齐，适合旁边有图片的首屏。默认是 `Center`。

### 证明行

`ProofText` 是头像旁的文字；`ProofNames` 列出画成生成头像的人（最多五个）。把 `ProofText` 设为 null 可隐藏这一行，把 `IsBackdropVisible` 设为 false 可去掉点阵。

### 文案与本地化

所有文字都是带英文默认值的属性：`Eyebrow`、`Title`、`Description`、`Placeholder`、`EmailLabel`、`ButtonText`、`SendingText`、`Note`、`EmptyMessage`、`FormatMessage`、`FailedMessage`、`DoneTitle`、`DoneFormat`、`ResetText` 和 `ProofText`。

## API 参考

<!-- api: HeroSignup -->

## 无障碍

- 整个区块以标题命名，大标题是一级标题。
- 输入框的读屏名称是 `EmailLabel`；错误出现时会播报，地址被接受时会播报确认。
- 头像和点阵只作装饰，对读屏隐藏。

### 键盘

| 按键 | 操作 |
|---|---|
| `Tab` / `Shift+Tab` | 在输入框和按钮之间移动；提交成功后移到“Use a different email”。 |
| 输入框中的 `Enter` | 校验并提交地址。 |
| 按钮上的 `Enter` / `Space` | 按下按钮。 |

提交成功后焦点移到“Use a different email”；重置后回到输入框。

## 动效

| 变化 | 完整 | 减弱 | 关闭 |
|---|---|---|---|
| 表单换成确认 | 表单淡出，确认上升出现 | 淡入淡出 | 立即 |
| 输入框下的错误 | 弹簧展开 | 淡入 | 立即 |
| 提交中的按钮 | 转圈并切换文字 | 转圈，淡入 | 转圈 |

点阵和光晕是静止的，从不动画。

## 响应式

- 断点按区块自身的宽度计算。小于 760（`:compact`）时标题从 52 降到 36，边距收紧；小于 480（`:narrow`）时标题为 28，输入框和按钮上下排列并占满宽度。
- 表单最宽 500，说明最宽 600，宽屏上文字行不会拉得过长。
- 让区块随页面拉伸，不要给它固定的 `Width`。

## 相关组件

- `NewsletterSignup`：展示往期内容的订阅表单。
- `CtaSection`：收尾的行动号召。
- `Input` 和 `Button`：表单由它们组成。
