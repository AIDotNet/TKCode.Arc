---
title: OTP input
summary: 六位数字验证流程，支持粘贴和键盘导航。
source-hash: f4f04a455025
---

<Demo name="Preview" />

## 何时使用

`OtpInput` 用于你向某人发送一次性验证码之后的那一步：一排单字符格子，每个字符占一个格子，上方有可见的标签，下方有辅助文字。输入会填充一个格子并移到下一格，粘贴的验证码会分散到各个格子中，方向键和 Backspace 会以人们习惯的方式在这一排中移动。请在你的服务器上校验验证码，该控件只负责收集。

- **验证码**，通过电子邮件或短信发送。
- **双因素认证**的验证码，来自身份验证器应用。
- **短 PIN 码和恢复码**，可以通过设置其他 `Length` 或 `InputMode="Text"` 来实现。

## 何时不用

- **需要用户凭记忆输入的机密信息**（密码、口令）：请使用 `PasswordField`。
- **其他任何文本**：请使用 `Input`。

## 用法

```xml
<arc:OtpInput x:Name="Code"
              Description="We sent a 6-digit code to your email."
              AutoFocus="True" />
```

```csharp
Code.Completed += async (_, e) =>
{
    Code.IsReadOnly = true;                       // keep focus, ignore typing while the code is checked
    var ok = await CheckCodeAsync(e.Code);       // your server call
    Code.IsReadOnly = false;
    if (!ok)
    {
        Code.Value = "";
        Code.ErrorMessage = "That code didn't match. Check the latest email and try again.";
        Code.FocusSlot(0);
    }
};

static Task<bool> CheckCodeAsync(string code) => Task.FromResult(code == "123456");
```

`Value` 保存验证码，并且是双向绑定。每次编辑后若所有格子都已填满（输入最后一个字符，或粘贴），就会触发 `Completed`，验证码通过 `OtpInputCompletedEventArgs.Code` 传递；`CompletedCommand` 会以同一验证码执行。从代码设置 `Value` 永远不会触发 `Completed`，因此重置格子不会启动另一次校验。`ValueChanged` 会报告每一次变化及其 `ChangeSource`：读者的编辑为 `Typing`、`Paste` 和 `Deletion`，你的代码则为 `Programmatic`。

未作任何配置时，该字段有六个数字格子，标签为 "Verification code"。`Label` 同时也是屏幕阅读器读到的格子组名称，因此即使页面周围已经说明了验证码的用途，也请设置它。

### 校验验证码

<Demo name="Verify" />

演示会在最后一个格子填满时校验验证码（模拟，耗时 700 ms）。验证码错误时，格子会被清空，显示错误信息，并且整排格子会晃动；重新输入时错误信息会消失，与 Arc 登录中验证码步骤的做法一致：

```csharp
Code.ValueChanged += (_, e) =>
{
    if (e.IsUserEdit && e.NewValue.Length > 0)
    {
        Code.ErrorMessage = null;
    }
};
```

### 错误

<Demo name="Error" />

非空的 `ErrorMessage` 会显示在辅助文字下方，使每个格子和焦点环变为危险色，并使 `IsInvalid` 变为 true。每出现一个新错误，整排格子就会晃动一次，因此被拒绝的验证码会被读作对这次尝试的回应；清除错误后再设置相同的文字，也会再次晃动。若只想要危险样式而不显示信息，请单独设置 `IsInvalid`；若想自行触发一次晃动，请调用 `Shake()`。

### 成功

<Demo name="Success" />

`IsSuccess` 会让格子和焦点环变为成功色。Arc 的该字段没有成功样式（其流程会直接进入下一步）；当字段在验证码正确后仍保留在屏幕上时，再使用它。错误优先于成功。

### 禁用

<Demo name="Disabled" />

`IsEnabled="False"` 会禁用每一个格子：以 48 % 的不透明度显示在弱化表面上，`Tab` 会跳过它们，且没有焦点环。`IsReadOnly` 则用于忙碌状态：格子保持焦点，方向键仍可使用，但输入、粘贴和删除都会被忽略。

### 长度和字符

<Demo name="Length" />

```xml
<arc:OtpInput Label="PIN" Length="4" />
<arc:OtpInput Label="Recovery code" Length="8" InputMode="Text" />
```

`Length` 设置格子数量（1 到 32）。`InputMode="Numeric"`（默认值）会丢弃所有非数字字符，无论是输入还是粘贴，并且让触摸键盘显示数字键盘。`InputMode="Text"` 则接受任何字符。

### 验证码如何填入格子

验证码和 Arc 中一样是一个字符串：格子 `i` 显示 `Value[i]`。清除中间的某个格子（Delete）会让其后的字符向左移动一格；在验证码末尾之后的格子中输入的字符，会成为验证码的下一个字符。已填满的格子在其字符被选中（双击，或用 `Tab` 移动到它）或被删除之前不再接受输入。粘贴会从获得焦点的格子开始填充，超出的部分会被忽略；焦点移到最后一个被粘贴字符之后的格子。输入法或平台短信自动填充一次插入多个字符时，同样会将它们分散到各个格子中。

### 窄容器

<Demo name="Narrow" />

字段会跟随它所获得的宽度，而不是窗口宽度。在 328 px 或以下时（即 Arc 的 360 px 手机断点减去两侧各 16 px 的边距），格子之间的间距变为 6 px，高度降为 44 px。当即使如此仍放不下时，每个格子会按相同比例缩小，间距保持不变。未拉伸的字段（`HorizontalAlignment` 不是 `Stretch`）宽度与其最宽的部分相同，与 Arc 居中的预览一致。

### 文本和本地化

`Label`、`Description` 和 `ErrorMessage` 都是纯文本。每个格子的无障碍名称来自 `SlotLabelFormat`（默认值为 `"{0}, digit {1} of {2}"`：标签、格子的位置以及格子的总数）。

## API 参考

<!-- api: OtpInput -->

<!-- api: OtpInputValueChangedEventArgs -->

<!-- api: OtpInputCompletedEventArgs -->

## 无障碍

- 格子组成一个群组，名称由 `Label` 指定；每个格子的名称类似 "Verification code, digit 1 of 6"。
- 辅助文字和错误信息是每个格子的帮助文字。错误出现时会立即播报。
- 焦点环是这一排格子共用的一个滑动焦点环，键盘和指针操作都一样。悬停样式只适用于鼠标或触控笔。
- 当 `InputMode="Numeric"` 时，触摸键盘会显示数字键盘。

### 键盘

| 按键 | 操作 |
|---|---|
| `0`-`9` / 字符 | 填入格子并移到下一格。已填满的格子在其字符被选中之前忽略输入。 |
| `ArrowLeft` / `ArrowRight` | 移到上一格或下一格。 |
| `Backspace` | 在空格子上，清除前一格并向后移动；在已填满的格子上，删除光标前的字符。 |
| `Delete` | 清除当前格子。 |
| `Ctrl+V` / `Cmd+V` | 从获得焦点的格子开始，把验证码粘贴到各个格子中。 |
| `Tab` / `Shift+Tab` | 逐格移动；用 `Tab` 移到的格子，其字符会被选中。 |

## 动效

- 输入的字符会从柔和的模糊中升起进入格子；粘贴则以从左到右的短促波浪落入。
- 焦点停留在这一排格子之内时，一个焦点环会在格子之间滑动；焦点从其他地方移入时，焦点环则淡入。
- 新错误会让整排格子左右晃动一次。辅助文字和错误行会展开高度，并且只改写发生变化的文字。

| 变化 | 完整 | 减少 | 关闭 |
|---|---|---|---|
| 字符出现 | 上升 0.35 em 并去除模糊，240 ms；粘贴时每个格子等待 35 ms | 淡入，120 ms | 立即 |
| 字符消失 | 淡出，缩小到 90 % 并模糊，120 ms | 立即 | 立即 |
| 焦点环移动 | 弹簧（视觉时长 0.26 s，无回弹） | 立即 | 立即 |
| 焦点环出现或消失 | 160 ms 内淡入淡出，并从 94 % 缩放 | 立即 | 立即 |
| 格子边框和填充 | 160 ms | 立即 | 立即 |
| 晃动 | 360 ms | 无 | 无 |

### 响应式行为

只有一个断点，依据字段所获得的宽度：328 px。格子宽 42 px，当一排放不下时会缩小。

## 相关组件

- `Input`：带标签和消息的单行文本。
- `PasswordField`：用于用户凭记忆输入的机密信息。
- Login 区块，其邮件验证码步骤使用本字段。
