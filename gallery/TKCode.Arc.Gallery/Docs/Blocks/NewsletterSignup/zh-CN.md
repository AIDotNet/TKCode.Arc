---
title: Newsletter signup
summary: 一个邮件订阅表单，旁边是一叠过往期刊；订阅后，寄给你的下一期会落在最上面。
source-hash: f68abc0e5c81
---

<Demo name="Preview" />

## 何时使用

使用 `NewsletterSignup` 收集电子报或候补名单的邮箱地址，适用于电子报本身就是最好的订阅理由的场景。表单旁边会展示一叠最近的期刊，访客在留下邮箱之前就能看到会收到什么。邮箱被接受后，寄给该访客的下一期会落在这叠期刊的最上面，读者数随之加一。

- **落地页中的一个板块**，适用于设计、工程或产品类电子报：文案和表单在一侧，期刊堆叠在另一侧。
- **独立的卡片**，可放在页脚、设置页或弹窗中，期刊堆叠放在表单上方的托盘里。
- **候补名单**，只需要表单即可（关闭期刊堆叠和读者数）。

## 何时不用

- **创建账户或登录**：请使用带密码和自有验证规则的表单。
- **没有可展示期刊的电子报**：请使用站点页脚区块中的页脚订阅，它只是单行表单。
- **多个字段**（姓名、主题、频率）：本区块只收集一个邮箱地址。

## 用法

```xml
<arc:NewsletterSignup x:Name="Signup" />
```

```csharp
Signup.SubscribeAsync = async (email, token) =>
{
    await http.PostAsJsonAsync("/subscribe", new { email }, token);   // throw to show the error and keep the address
};
Signup.Subscribed += (_, e) => analytics.Track("newsletter", e.Email);
```

使用默认配置时，区块显示 Arc 的文案、刊物 "Margins" 和 12,480 位读者，并模拟发送过程（1100 ms，总是成功）。将 `SubscribeAsync` 替换为对你的邮件服务商的调用：正常返回即显示成功状态，抛出异常则显示失败状态。当区块被重置或移除时，令牌会被取消。

### 订阅过程

| 阶段 | 显示内容 |
|---|---|
| `Idle` | 空的输入框、"Subscribe →" 和隐私说明。发送失败后，按钮变为 "Try again →"，邮箱地址保留。 |
| `Sending` | 输入框变为只读，按钮显示一个圆环和 "Subscribing"。之后的按压会被忽略。 |
| `Done` | 按钮变为绿色并带有对勾，显示 "Subscribed"，确认信息替代说明文字，下一期期刊落在堆叠的最上面。 |

`Phase` 和 `SubscribedEmail` 是只读属性。`Reset()`（"Use a different email" 按钮会调用它）会清空邮箱地址并回到 `Idle`。

### 验证

空字段和格式错误的地址各有自己的提示（`EmptyMessage`、`FormatMessage`）。字段失去焦点且其中有文字时开始验证；提交尝试过一次后，每次按键都会重新验证。在此之前，输入会清除错误。验证失败时，输入框会抖动，焦点保留在字段中。

### 变体

`Variant` 为 `Inline`（默认）或 `Card`。

<Demo name="Card" />

```xml
<arc:NewsletterSignup Variant="Card" />
```

### 刊物

`Publication` 包含名称、`Upcoming` 期刊和 `Recent` 期刊（最新在前）。最近的前三期构成堆叠。每个 `NewsletterIssue` 都有一个编号（其标识）、日期行、主题，以及最多三个 `NewsletterStory` 条目，每个条目包含标题、缩略图 `IImage` 和阅读时长。读者头像来自 `ReaderFaces`。

```csharp
var publication = new NewsletterPublication { Name = "Margins", Upcoming = next };
publication.Recent.Add(issue148);
publication.Recent.Add(issue147);
signup.Publication = publication;
signup.ReaderFaces.Add(faceBitmap);
```

没有刊物时，区块使用 Arc 的示例刊物 "Margins"，其中的文章没有缩略图。将 `IsPublicationVisible` 或 `IsReadersVisible` 设为 false 可隐藏期刊堆叠或读者信息。

### 仅表单

<Demo name="FormOnly" />

若既没有期刊堆叠也没有读者信息，该区块会变为一列居中的内容，最宽 720 px。

### 窄容器

<Demo name="Narrow" />

区块跟随它自身的宽度，而不是窗口的宽度。宽度为 860 px 或更小时变为单列。宽度为 520 时收紧间距和字体，为 400 时去掉阅读时长，并且在已寄送的期刊上只显示收件人；为 360 时，读者行可能换行。宽度小于 300 px 的表单会将字段和按钮纵向堆叠，两者都占满宽度。

### 文本与本地化

每个字符串都是一个属性，并带有 Arc 的英文默认值：`Placeholder`、`ButtonLabel`、`PrivacyNote`、`PrivacyLinkLabel`、`EmailLabel`、`RetryLabel`、`SendingLabel`、`DoneLabel`、`EmptyMessage`、`FormatMessage`、`FailedMessage`、`DoneMessage`、`DifferentEmailLabel`、`RecipientFormat`、`IssueFormat`、`MinutesFormat`、`ReadersWord`、`IncludingYou` 和 `SentToFormat`。

### 隐私链接

按下它会触发 `Navigate`（并执行 `NavigateCommand`）。当事件未被处理，且 `PrivacyLinkHref` 是绝对的 `http`、`https` 或 `mailto` 地址时，会通过平台启动器打开它。将 `IsPrivacyLinkVisible` 设为 false 可隐藏该链接。

## API 参考

<!-- api: NewsletterSignup -->

<!-- api: NewsletterPublication -->

<!-- api: NewsletterIssue -->

<!-- api: NewsletterStory -->

<!-- api: NewsletterSubscribedEventArgs -->

<!-- api: NewsletterNavigateEventArgs -->

## 无障碍

- 区块是一个以其标题命名的区域。输入框带有隐藏标签 `EmailLabel` 以及邮箱输入提示。
- 错误以强提示方式播报；确认信息以礼貌方式播报，并包含链接的去向。
- 只有最前面的期刊会向屏幕阅读器公开（"Margins, issue 148"）。其后的期刊、缩略图和头像都是装饰性的。
- 键盘焦点环位于按钮和链接外侧 2 px 处。悬停样式只适用于鼠标或触控笔。

### 键盘

| 按键 | 操作 |
|---|---|
| `Tab` / `Shift+Tab` | 在字段、按钮和链接之间移动。订阅成功后，按钮会离开 Tab 顺序。 |
| 字段中的 `Enter` | 检查并发送地址（输入法组合输入期间忽略）。 |
| 按钮上的 `Enter` / `Space` | 按下该按钮。 |

订阅成功后，焦点移到 "Use a different email"；调用 `Reset()` 后，焦点回到输入框。

## 动效

- 订阅后，下一期期刊会放到最上面：它从下方升起叠到堆叠上，其余期刊则向后退并缩小（无过冲弹簧），最旧的一期则在后方滑落。
- 按钮的文字和消息行会以短暂的模糊原地切换，对勾则逐笔画出。
- 被拒绝的地址或发送失败时，输入框会抖动。读者数向上滚动一位，", including you" 的宽度会展开。

| 变化 | Full | Reduced | Off |
|---|---|---|---|
| 期刊交换位置 | 位置和缩放使用弹簧，透明度在 120 ms 内变化 | 立即就位，透明度在 160 ms 内变化 | 立即 |
| 文字和消息切换 | 上浮并模糊，进入 240 ms，退出 160 ms | 淡入淡出，120 ms | 立即 |
| 抖动 | 360 ms | 无 | 无 |
| 对勾 | 340 ms 内画出 | 直接画出 | 直接画出 |

### 响应式行为

断点取决于区块的宽度：860、520、400 和 360 px，另外表单本身在 300 px 处设有断点。

## 相关组件

- 站点页脚区块，用于页脚内的单行电子报输入框。
- CTA 板块区块，用于引导至订阅页面的行动号召。
- `EmptyStates`，用于带有可重试操作的空视图。
