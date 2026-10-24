---
title: Login
summary: 登录与注册页面，从紧凑的验证码卡片到全屏分栏与沉浸式布局。
source-hash: e90efb44eaf6
---

<Demo name="SignInPreview" />

Login 是一个包含五种布局的区块，每种布局都是独立控件，共用同一套流程用语：无密码卡片从邮箱地址变为六位数验证码，再变为已登录的账号（`SignIn`）；以通行密钥为先的居中布局，背景是环形底纹（`LoginCentered`）；全屏布局，表单与图片并排（`LoginSplit`）；全屏图片配毛玻璃卡片（`LoginImmersive`）；以及带密码强度条的创建账号表单（`SignupForm`）。每种布局开箱即用都会运行一个模拟流程，因此在宿主接入异步处理程序之前，不会向任何地方发送任何内容。`Login` 通过 `Variant` 属性承载其中任意一种布局。

## 何时使用

### 邮箱验证码

- 使用邮箱发送验证码进行登录的应用的入口页面。
- 同时提供通行密钥和单点登录的登录卡片。
- 需要在继续之前确认账号的无密码流程。

### 居中

- 以通行密钥为主要登录方式的全窗口登录或注册页面。
- 退回到六位数邮箱验证码的认证流程。
- 希望用同一张卡片完成登录和注册的产品。

### 分栏

- 产品的登录页面，表单旁边需要展示品牌图片或插画。
- 邮箱优先的登录方式：在输入密码之前已知账号（姓名、头像）。
- 以密码登录为主，单点登录作为备选，并提供自助重置密码。

### 沉浸式

- **无密码的产品**：用户输入地址，点击收件箱中的链接即可进入。页面会解释每一步，并让地址始终可见，便于发现拼写错误。
- **有品牌特色的入口**：图片铺满页面，因此登录界面带有产品的氛围（风景、团队办公室或产品本身）。使用 `Photo`；没有图片时，页面会绘制一幅黄昏风景。
- **在同一页面登录和注册**：顶部的切换按钮会切换标题和创建账号的文案；两种模式的流程（发送链接、确认链接）相同。

### 注册

`SignupForm` 是邮箱和密码注册的客户端部分：包含第三方登录按钮、全名、工作邮箱、带密码强度条的密码、产品更新订阅选项，以及账号创建后原地显示的完成面板。

- 需要展示密码强度和产品更新订阅选项的注册页面。
- 希望成功面板原地显示而不是跳转的流程。
- 在表单旁提供 Google、Apple 和 GitHub 登录的注册页面。

## 何时不用

### 邮箱验证码

- **密码登录**：使用 `LoginCentered`、`LoginSplit` 或 `LoginImmersive`。
- **创建账号**：使用 `SignupForm`。
- **只需要在你自己的表单中放验证码步骤**：单独使用 `OtpInput`。

### 居中

- 在品牌照片旁进行密码登录：使用 `LoginSplit`。
- 在铺满的照片上使用魔术链接登录：使用 `LoginImmersive`。
- 在现有页面中嵌入登录表单：使用 `SignIn`。

### 分栏

- 以通行密钥为先的登录，或通过邮箱发送六位数验证码：使用 `LoginCentered` 或 `SignIn`。
- 在铺满的照片上通过魔术链接登录：使用 `LoginImmersive`。
- 创建账号：使用 `SignupForm`。
- 在现有页面或对话框中嵌入登录表单：使用 `SignIn`；本区块是整页。

### 沉浸式

- **密码登录**：使用 `LoginSplit`，它在邮箱之后询问密码，并处理重置链接。
- **需要把一次性验证码输回应用**：使用 `SignIn` 或 `LoginCentered`，它们将邮箱步骤与 `OtpInput` 配对。
- **放在繁忙的界面中**：本区块预期独占页面（或一个大面板）。若要在应用内放一个小型登录框，请使用对话框，配合 `Input` 和 `Button`。
- **中间有细节的图片**：卡片会遮住中央区域。请选择主体位于卡片周围的图片，并用 `PhotoAlignment` 移动裁剪位置。

### 注册

- **回头客**：使用邮箱验证码（`SignIn`）或居中（`LoginCentered`）变体。
- **已有表单中的密码输入**：使用 `PasswordField`；如果字段需要规则列表，使用 `PasswordStrength`。
- **无密码或基于验证码的注册**：使用 `OtpInput`。

## 用法

### 邮箱验证码

##### 邮箱验证码（SignIn）

`SignIn` 是一张登录卡片，依次经历邮箱地址、六位数验证码，最后到已登录的账号；表单下方有通行密钥按钮和单点登录按钮。每个步骤都会从它即将前往的一侧滑入，卡片的高度随一个弹簧动画跟随变化；账号头像从验证码步骤放大进入已登录状态，届时头像周围会画出一个圆环，并弹出一个勾选标记。预览中的一切都是模拟的：输入任意地址，先输入一个错误的验证码，然后输入 `123456`。


```xml
<arc:SignIn x:Name="SignIn" HorizontalAlignment="Center" />
```

```csharp
SignIn.SendCodeAsync = SendCodeAsync;
SignIn.VerifyCodeAsync = CheckCodeAsync;
SignIn.SignInWithAsync = SignInWithAsync;
SignIn.SignedIn += (_, e) => OpenDashboard(e.Account);

// Stand-ins for your auth provider: no account from the send (the card derives one), 123456 is right, and a passkey
// or a provider signs in as Robin.
static Task<SignInAccount?> SendCodeAsync(string email, CancellationToken token) => Task.FromResult<SignInAccount?>(null);
static Task<bool> CheckCodeAsync(SignInAccount account, string code, CancellationToken token) => Task.FromResult(code == "123456");
static Task<SignInAccount?> SignInWithAsync(SignInMethod method, CancellationToken token) =>
    Task.FromResult<SignInAccount?>(new SignInAccount("Robin Park", "robin@park.dev"));
static void OpenDashboard(SignInAccount account) { }
```

卡片本身不做任何身份验证。请将三个处理程序连接到你的认证服务商：

- `SendCodeAsync` 在点击"继续"（以及重新发送）时运行，传入去除首尾空白的地址。返回要在验证码步骤中显示的账号，或返回 null 以根据地址推导账号（见下文"账号"）。抛出异常则停留在邮箱步骤，并在输入框下方显示 `SendFailedMessage`。
- `VerifyCodeAsync` 在收到完整验证码时运行。返回 true 则登录，返回 false 表示验证码错误。抛出异常会显示 `VerifyFailedMessage`。
- `SignInWithAsync` 为通行密钥按钮以及每个带有 `SignInMethod` 的第三方登录运行（`Kind` 为 `Passkey` 或 `Provider`，`Provider` 指定服务商名称）。返回账号，或返回 null 以使用第一个已知账号。抛出异常会保留邮箱步骤，并在状态行中说明。

调用 `Reset()` 或卡片离开可视树时，所有令牌都会被取消；之后到达的结果会被忽略。未设置处理程序时，卡片会模拟 Arc 预览：发送耗时 800 ms，校验耗时 700 ms，并接受 `DemoCode`（`"123456"`），通行密钥耗时 1100 ms，第三方登录耗时 900 ms。

`SignedIn` 报告账号和登录方式（`SignInSignedInEventArgs.Account`、`.Method`），`SignedInCommand` 会携带账号运行。`SignedOut` 在已登录步骤的 Sign out 按钮被点击时触发。`Step`、`Account`、`Method`、`IsBusy` 和 `Status` 是可绑定的只读属性；`Email` 是双向的，因此你可以预填输入框。

###### 连接你的认证服务商

<Demo name="SignInHandlers" />

该演示从一个模拟服务中获取结果：验证码是 `424242`，通行密钥和第三方登录都以 Robin Park 的身份登录，日志会列出调用和事件。`CodeHint` 会替换演示中的提示文字 "For this demo, the code is 123456."（有 `VerifyCodeAsync` 处理程序却没有 `CodeHint` 时，验证码输入框不显示提示文字）。演示处理了 `SignUpRequested`，因此卡片不会显示它自己的 "Sign up opens in your app" 状态文字。

###### 步骤

- **邮箱。** 按 Arc 的方式检查地址：输入框失去焦点且有文字时，或从第一次点击"继续"起进行检查；此后每次按键都会更新提示（空输入框显示 "Enter your email address."，否则显示 "Enter a full address, like name@example.com."）。常见域名拼写错误（`gmial.com`、`hotmial.com`、`outlok.com` 等）会在输入框下方显示一行 "Did you mean …?"；点击其按钮会把修正后的地址填入输入框。发送验证码时，"继续"按钮显示加载指示器，输入框变为只读。
- **验证码。** 账号行显示头像、地址，以及 Change（返回邮箱步骤，地址被选中）。验证码输入框是一个 `OtpInput`，会获得焦点；填满最后一格时会校验验证码，"Verify" 和 Enter 也会触发校验。验证码不足六位时会要求输入全部六位数字；错误的验证码会清空各格、使其抖动并给出提示，重新输入即清除错误。"Resend code in 0:30" 倒计时（秒数逐位滚动）；倒计时归零后，Resend 会清空验证码、发送新验证码并重新开始计时（`ResendSeconds`）。
- **已登录。** 带成功圆环和勾选标记的头像、"Welcome back, Emma"、地址、登录方式、"Remembered for 30 days"，以及 Sign out（返回邮箱步骤，地址已填入并被选中）。

###### 账号与第三方登录

```xml
<arc:SignIn>
    <arc:SignInAccount Name="Robin Park" Email="robin@park.dev" />
    <arc:SignIn.Providers>
        <arc:SignInProvider Name="Google" Mark="Google" />
        <arc:SignInProvider Name="Okta" />
    </arc:SignIn.Providers>
</arc:SignIn>
```

`Accounts` 是已知用户（Arc 的团队成员）。当 `SendCodeAsync` 没有返回账号时，卡片会遵循 Arc 的 `accountFor`：@ 之前的部分会按点、下划线、加号和减号拆分；如果已知账号的名字与第一段相匹配，则保留其姓名和照片；其他人则根据前两段生成姓名（"jane.doe@acme.io" 为 Jane Doe）并生成首字母头像。`SignInAccount.ForEmail` 暴露相同的规则。`Accounts` 为空时，使用 Arc 的五位示例团队成员（无照片，因为 Arc 的肖像不在其 MIT 注册表中）；预览会绘制它自己的头像。`SignInAccount.Photo` 接受任意 `IImage`。

`Providers` 是单点登录按钮，等宽排列。为空时使用 Arc 的三个：Google（四色标识）、Apple 和 GitHub（其标识使用文字颜色）。`Mark` 选择内置标识；`Icon` 接受你自己的标识，基于 24×24 网格，以文字颜色填充；两者都没有时，按钮只显示名称。`AccountsSource` 和 `ProvidersSource` 可接受任意集合，并优先于列表。`IsPasskeyVisible`、`IsProvidersVisible` 和 `IsSignUpVisible` 用于隐藏这些部分；若通行密钥和第三方登录都不显示，"or" 分隔线也会一并隐藏。

###### 自定义文案与第三方登录

<Demo name="SignInCustom" />

每个可见字符串、无障碍名称和状态行都是一个属性，默认值为 Arc 的英文文案：`Title`、`Description`、`ContinueLabel`、`CodeTitle`、`WelcomeFormat`（"Welcome back, {0}"）、`SignedInStatusFormat` 等（见 API 表）。格式属性接受 `{0}`。

###### 窄容器

<Demo name="SignInNarrow" />

卡片跟随其所获得的宽度，而不是窗口宽度，最宽为 432。宽度为 380 或更小时（Arc 的容器查询），各步骤的内边距降为 24×20，标题为 28 px，账号行收紧，第三方登录按钮使用 12 px 文字；若无法并排放置，页脚会把状态行放在 "No account? Sign up" 之上。给定宽度为 448 或更小时（Arc 的 480 px 手机断点减去两侧各 16 px 边距），卡片的圆角会变为面板圆角。

### 居中

<Demo name="LoginCenteredPreview" />

`LoginCentered` 是以通行密钥为先的登录和注册页面：一张卡片位于由同心环构成的安静背景上，顶栏有产品标识和登录/注册切换，页脚有一行状态文字，包含服务条款和隐私政策链接。卡片从通行密钥视图开始（一个通行密钥按钮、"Use email instead" 以及 Google、Apple 和 GitHub），退回到通过邮箱发送的六位数验证码，最后停在已登录的账号上。一个控件同时涵盖登录和注册。

```xml
<arc:LoginCentered x:Name="Login"
                   IsFullScreen="True"
                   TermsHref="https://example.com/terms"
                   PrivacyHref="https://example.com/privacy" />
```

```csharp
Login.SignedIn += (_, e) =>
{
    // e.Email, e.Method (Passkey, EmailCode, Google, Apple, GitHub) and e.Mode (SignIn or SignUp)
    NavigateTo(e.Mode == LoginCenteredMode.SignUp ? "/welcome" : "/app");
};

static void NavigateTo(string route) => Console.WriteLine(route); // your router
```

设置 `IsFullScreen` 表示区块占据整个窗口：此时它会填满宿主，没有边框，圆角为直角。不设置时，区块会绘制自己的边框（1px 边框，面板圆角），且高度至少为 640px。

<Demo name="LoginCenteredFramed" />

未设置处理程序时，流程与 Arc 预览完全一致：通行密钥在 1.7 秒后解析为 `DemoEmail`（"maya@northwind.studio"），第三方登录在 0.9 秒后完成，验证码在 0.8 秒后"发送"，并在 0.7 秒后校验；`DemoCode`（"482913"）是正确的验证码（验证码输入框会将其显示为提示文字）。卡片进入已登录视图后，`SignedIn`（以及 `SignedInCommand`）会报告地址、登录方式和模式。退出登录会返回登录模式下的通行密钥视图，并触发 `SignedOut`。

#### 将注册引导至入门流程

<Demo name="LoginCenteredSignUp" />

`Mode` 是双向的。设置 `Mode="SignUp"` 可以让区块打开 "Create your account" 表单（用于 `/sign-up` 路由）；之后再设置它，卡片会按照顶栏切换按钮的方式切换。注册流程要求输入工作邮箱，校验验证码（"Verify and create account"），并以 "Welcome to Arc" 结束；随后 `SignedIn` 中的 `Mode == SignUp`。

#### 连接你的认证服务

<Demo name="LoginCenteredConnected" />

用四个异步处理程序替换模拟。每个处理程序都会收到一个 `CancellationToken`，当用户离开（取消、更改、切换模式或退出登录）时该令牌会被取消。

```csharp
Login.PasskeyAsync = RequestPasskeyAsync;               // WebAuthn: returns the account's address
Login.SignInWithProviderAsync = SignInWithOAuthAsync;   // OAuth: returns the account's address
Login.SendCodeAsync = SendCodeAsync;                    // your mailer
Login.VerifyCodeAsync = CheckCodeAsync;                 // true accepts the code

// Stand-ins for your auth service.
static Task<string> RequestPasskeyAsync(CancellationToken token) => Task.FromResult("robin@park.dev");
static Task<string> SignInWithOAuthAsync(LoginCenteredProvider provider, CancellationToken token) => Task.FromResult("robin@park.dev");
static Task SendCodeAsync(string email, LoginCenteredMode mode, CancellationToken token) => Task.Delay(600, token);
static Task<bool> CheckCodeAsync(string email, string code, CancellationToken token) => Task.FromResult(code == "424242");
```

| 处理程序 | 未设置时（Arc 预览） | 抛出异常时 |
|---|---|---|
| `PasskeyAsync` | 1.7 s，然后为 `DemoEmail` | `OperationCanceledException`："Passkey request canceled"；其他异常："Your passkey could not be used" |
| `SignInWithProviderAsync` | 0.9 s，然后为 `DemoEmail` | 第三方登录按钮停止旋转，状态文字提示登录未完成 |
| `SendCodeAsync` | 0.8 s | 邮箱输入框显示 "We couldn't send a code. Try again."（也用于 Resend code） |
| `VerifyCodeAsync` | 0.7 s，接受 `DemoCode` | 验证码输入框显示 "We couldn't check the code. Try again." |

请求进行期间（`IsBusy`），发起该请求的按钮会显示加载指示器并保持焦点，其他操作会被忽略而不是被禁用，与 Arc 的 `aria-disabled` 按钮一致。通行密钥或验证码被接受后，卡片会暂停（0.64 秒和 0.6 秒），显示 "Passkey verified" 或 "Verified"，然后再继续。

页脚链接会以该链接及其地址触发 `Navigate`。处理该事件可以打开你自己的页面；否则，绝对 `http`、`https` 或 `mailto` 地址会通过平台启动器打开，而页内地址（如 Arc 默认的 `#terms`）只会在状态行中说明它将前往的位置。

#### 流程

| 视图 | 内容 | 转到 |
|---|---|---|
| Passkey | "Sign in to Arc"、通行密钥按钮、"Use email instead"、"or"、Google / Apple / GitHub | 完成（通行密钥、第三方登录）或邮箱 |
| Email | "Sign in with email" 或 "Create your account"、邮箱输入框、Send code / Create account、"Use a passkey instead"（仅登录） | 验证码 |
| Code | "Check your email"，附地址和 Change、六位数验证码、Verify、Resend code（从 `ResendSeconds` 开始倒计时） | 完成，或邮箱（Change） |
| Done | 绘制的勾选标记、"Welcome back" 或 "Welcome to Arc"、账号、登录方式和 "Remembered for 30 days"、Sign out | 通行密钥 |

邮箱按照 Arc 的格式检查（若干字符、`@`、若干字符、一个点和另外两个字符）。空输入框提示 "Enter your email address."；不完整的则提示 "Enter a full address, like name@example.com."。错误会在用户提交或离开有文字的输入框后出现，之后随每次按键更新。验证码在输入最后一位数字时（或点击 Verify / 按 Enter）进行检查；验证码不足六位时提示 "Enter all 6 digits."，错误的验证码会清空输入框并使其抖动。`Step`、`PasskeyPhase`、`IsBusy`、`Account`、`Method` 和 `Status` 是可绑定的只读属性；`StartPasskey`、`CancelPasskey`、`SignInWith`、`SwitchMode` 和 `SignOut` 的作用与对应控件相同。

#### 窄屏幕

<Demo name="LoginCenteredNarrow" />

区块跟随自身宽度，而不是窗口宽度。宽度为 520px 或更小时，顶栏和舞台收紧为 16px，卡片圆角变为面板圆角，内边距降为 24px。宽度为 400px 或更小时，标题从 36px 降为 28px，卡片两侧收为 20px，第三方登录按钮收紧为 12px 文字，以便三个按钮保持在一行。

#### 文本与本地化

所有字符串都在 `LoginCenteredLabels` 中（默认为 Arc 的英文文案）。替换整个对象即可本地化或改写文案；包含数值的句子则使用函数：

```csharp
Login.Labels = new LoginCenteredLabels
{
    PasskeyTitle = "Sign in to Northwind",
    WelcomeNew = "Welcome to Northwind",
    ResendInName = seconds => $"Resend available in {seconds} s"
};
```

### 分栏

<Demo name="LoginSplitPreview" />

`LoginSplit` 是一个两栏登录页面。左栏顶部是产品品牌和一个 "Sign up" 链接，中间是登录步骤，底部是一行带服务条款和隐私政策的状态文字；右栏是全高的图片。流程先要求输入邮箱，查找账号，并以芯片的形式在密码输入框上方显示该账号；密码通过后，账号打开并显示进度条，然后用名字向用户问候。Google、Apple 和 GitHub 按钮可以跳过密码。"Forgot password?" 会发送重置链接，并开始重新发送倒计时。

```xml
<arc:LoginSplit x:Name="Login"
                IsFullScreen="True"
                BrandName="Fieldnote"
                SignUpHref="https://example.com/signup"
                TermsHref="https://example.com/terms"
                PrivacyHref="https://example.com/privacy" />
```

```csharp
Login.SignedIn += (_, e) =>
{
    // e.Account (Name, Email, Picture), e.Method (Password or Provider) and e.Provider for single sign-on.
    OpenWorkspace(e.Account.Email);
};

static void OpenWorkspace(string email) => Console.WriteLine(email); // your navigation
```

设置 `IsFullScreen` 表示区块占据整个窗口：它会填满宿主，圆角为直角，没有边框。不设置时，区块会绘制自己的边框（1 px 边框，26 px 圆角），且高度至少为 640 px；上面的预览是页面宽度下的带边框区块，其中图片栏被隐藏。

<Demo name="LoginSplitFullScreen" />

##### 流程

未设置处理程序时，区块会以固定的停顿模拟后端，因此可以直接试用：

| 步骤 | 发生的事情 | 模拟停顿 |
|---|---|---|
| Email | 点击"继续"后先检查地址（必填，然后是完整地址），再查找账号：按钮显示加载指示器，输入框变为只读，状态行显示 "Finding your account" | 650 ms |
| Password | 芯片显示账号；输入框的提示显示演示密码（`DemoPassword`，"fieldnote-demo"）。密码错误会显示错误，并选中已输入的内容，因此下一次按键会将其替换 | 800 ms 校验 |
| Accepted | 登录按钮变为勾选标记，并收缩为 44 px 的圆形 | 775 ms |
| Signing | 账号行、"Opening Fieldnote…" 以及在 1.4 秒内填满的进度条 | 1.7 s（减少动效时为 400 ms） |
| Done | 标题和句子原地变为问候语（"All set, Rowan"）以及用户的登录方式；出现 Sign out。触发 `SignedIn` | — |

单点登录会跳过密码：第三方登录按钮显示 1 秒的加载指示器，然后打开账号。当邮箱输入框中的地址有效时，它会登录该地址对应的账号；否则使用 `DefaultAccount`（或 `Accounts` 中的第一个账号）。账号芯片和 Sign out 都会返回邮箱步骤，并将地址填入输入框；`SignOut()` 和 `Reset()` 可以在代码中执行同样的操作（`Reset` 还会清空地址并取消正在进行的操作）。

`Step`（`Email`、`Password`、`Signing`、`Done`）、`Account`、`IsBusy` 和 `StatusText` 是只读属性，每次步骤变化时 `StepChanged` 都会报告。

##### 账号

已知账号按邮箱匹配，不区分大小写，并显示它们自己的姓名和图片：

```xml
<arc:LoginSplit>
    <arc:LoginSplit.Accounts>
        <arc:LoginSplitAccount Name="Rowan Hale" Email="rowan@fieldnote.app" />
        <arc:LoginSplitAccount Name="Iris Vance" Email="iris@fieldnote.app" />
    </arc:LoginSplit.Accounts>
</arc:LoginSplit>
```

其他任何地址都会成为一个账号，以 @ 之前的部分命名，并在点、下划线、加号和连字符处拆分（"ada.lovelace@example.com" 以 "Ada Lovelace" 登录，首字母为 "AL"，问候语为 "Ada"）。`LoginSplitAccount.FromEmail` 可以在代码中提供相同的账号。当视图模型持有列表时，请绑定 `AccountsSource`，而不是填充 `Accounts`。

<Demo name="LoginSplitEvents" />

##### 连接你的后端

四个异步处理程序替换模拟，每个操作对应一个。每个处理程序都会收到一个 `CancellationToken`，当用户返回或调用 `Reset()` 时该令牌会被取消。抛出异常会显示 "Something went wrong. Try again." 并结束忙碌状态。

```csharp
Login.LookupAccountAsync = FindAccountAsync;
Login.VerifyPasswordAsync = CheckPasswordAsync;
Login.SignInWithProviderAsync = SignInWithAsync;
Login.SendResetLinkAsync = SendResetAsync;

// Your back end. A null account from the lookup or single sign-on means "make one from the address".
static Task<LoginSplitAccount?> FindAccountAsync(string email, CancellationToken token) =>
    Task.FromResult<LoginSplitAccount?>(new LoginSplitAccount("Rowan Hale", email));

static Task<bool> CheckPasswordAsync(LoginSplitAccount account, string password, CancellationToken token) =>
    Task.FromResult(password.Length >= 12);

static Task<LoginSplitAccount?> SignInWithAsync(LoginSplitProvider provider, string? email, CancellationToken token) =>
    Task.FromResult<LoginSplitAccount?>(null);

static Task SendResetAsync(LoginSplitAccount account, CancellationToken token) => Task.CompletedTask;
```

设置 `VerifyPasswordAsync` 后，字段下方的演示密码提示会消失。序列中的其他停顿（通过的勾选标记、进度条）保留，并在 `Timings` 中设置。

<Demo name="LoginSplitHandlers" />

##### 忘记密码

链接发送期间，"Forgot password?" 会变为 "Sending…"，随后变为绿色勾选标记和 "Link sent"。输入框下方的一行文字会说明链接发送到了哪里，并提供 "Resend in 0:30"；秒数逐位递减，归零时按钮显示 "Resend link"。按下该按钮会立即发送另一个链接，并重新开始倒计时（`ResendSeconds`，默认为 30）。对于第一个链接以及每次重新发送，都会触发 `ResetLinkSent`（`IsResend`）。

##### 链接

"Sign up"、"Terms" 和 "Privacy" 会触发 `Navigate`（并运行 `NavigateCommand`），参数为所用的 `LoginSplitLink`。处理该事件即可自行导航。否则，带有地址的链接（`SignUpHref`、`TermsHref`、`PrivacyHref`）会在浏览器中打开；没有地址的链接则会在状态行中说明它在你的应用中将前往的位置。

##### 品牌、图片与文案

<Demo name="LoginSplitCustom" />

```csharp
Login.BrandName = "Northdesk";
Login.Photo = new Bitmap(AssetLoader.Open(new Uri("avares://MyApp/Assets/lobby.jpg")));
Login.PhotoAlignment = new Point(0.5, 0.45);
Login.Labels = new LoginSplitLabels
{
    EmailTitle = "Back to the queue",
    DoneTitle = first => $"Welcome back, {first}"
};
Login.Providers.Clear();
Login.Providers.Add(LoginSplitProvider.Google());
Login.Providers.Add(new LoginSplitProvider("Work SSO") { Icon = Geometry.Parse("M4 4h16v16H4z") });
```

- `BrandName` 在标识旁、登录标题（"Opening Fieldnote…"）中以及区块的无障碍名称中给出产品名称。`BrandMark` 可以用任意 24 × 24 描边几何图形替换内置的笔记本标识。
- `Photo` 像封面图一样填充右栏，并以 `PhotoAlignment` 为中心裁剪（为图片尺寸的比例；默认为 0.5、0.58）。没有图片时，区块会在淡蓝色天空下绘制一面拉丝金属立面。
- `Labels`（`LoginSplitLabels`）包含所有文字：标题、字段标签、按钮、错误、状态行以及屏幕阅读器读出的内容。包含数值的文字使用函数，因此在其他语言中可以调整词序。
- `Providers` 列出单点登录按钮，每个占一个等宽列。`LoginSplitProvider` 有名称、内置标识（`Google`、`Apple`、`GitHub`）或以文字颜色填充的自定义 `Icon`。列表为空时会隐藏分隔线和按钮；设置 `ProvidersSource` 后则以它为准。

##### 窄屏幕

<Demo name="LoginSplitNarrow" />

### 沉浸式

<Demo name="LoginImmersivePreview" />

`LoginImmersive` 是覆盖在图片上的整页登录界面：顶部是产品标识和登录/注册切换，中间是一张磨砂玻璃卡片，底部是带有法律链接的说明。它通过邮件发送的一次性链接（魔术链接）登录用户，并提供单点登录作为替代方式。卡片依次经历三个步骤：邮箱表单、Sent 步骤（信封在地址上方合上，Open mail app 和重新发送倒计时）以及 Done 步骤（绘制的勾选标记、账号和登录方式、Sign out）。

```xml
<arc:LoginImmersive IsFullScreen="True"
                    BrandName="Fieldnote"
                    Photo="{Binding SignInPicture}"
                    TermsHref="https://fieldnote.app/terms"
                    PrivacyHref="https://fieldnote.app/privacy" />
```

```csharp
var http = new HttpClient { BaseAddress = new Uri("https://fieldnote.app") };
// Your server emails the link (throwing shows an error under the field).
login.SendLinkAsync = (email, mode, token) =>
    http.PostAsync($"/auth/link?signUp={mode == LoginImmersiveMode.SignUp}", new StringContent(email), token);
// True once the person followed the link; false keeps the Sent step.
login.OpenMailAppAsync = async (email, token) => await http.GetStringAsync($"/auth/link/followed?email={email}", token) == "yes";
// Run the provider's flow and return the address it signed in (null: cancelled).
login.SignInWithProviderAsync = async (provider, email, token) => await http.GetStringAsync($"/auth/{provider.Name}", token);
login.SignedIn += (_, e) => Debug.WriteLine($"Signed in as {e.Email} ({e.Method})");
```

`LoginImmersive` 派生自 `TemplatedControl`。未设置处理程序时，它会以固定延迟（`Timings`）运行演示：发送耗时 900 ms，然后按钮显示 Link sent 625 ms，之后才出现 Sent 步骤；Open mail app 会在一秒后自动为你跟随链接；第三方登录在 900 ms 后返回。每个处理程序替换其中一个环节：

| 处理程序 | 调用时机 | 返回值 |
|---|---|---|
| `SendLinkAsync` | 以有效地址点击 Email me a link，以及 Send again | 完成则继续；抛出异常则在输入框下方显示错误 |
| `OpenMailAppAsync` | Open mail app | 链接被跟随后返回 `true`（进入 Done 步骤），返回 `false` 则停留在 Sent |
| `SignInWithProviderAsync` | 单点登录按钮；第二个参数为输入框中的有效地址，否则为 null | 已登录的地址；如果用户取消则为 null |

若没有 `OpenMailAppAsync` 但设置了 `MailAppUri`（例如 `mailto:`），Open mail app 会触发 `Navigate`，参数为 `LoginImmersiveLink.MailApp`，除非事件被处理，否则会打开该地址；随后页面会停留在 Sent 步骤，直到应用调用 `CompleteSignIn()`（例如服务器确认链接已被跟随时）。每个处理程序都会获得一个取消令牌：当用户返回、切换模式、退出登录或区块离开屏幕时，该令牌会被取消。

<Demo name="LoginImmersiveEvents" />

#### 结构

1. **图片**：`Photo` 以填充方式裁剪（类似 CSS 的 `object-fit: cover`），并由 `PhotoAlignment` 定位（默认水平 50%、垂直 58%），上方覆盖一层平整的遮罩，使白色的页眉和页脚保持可读（浅色主题为 30% 深色，深色主题为 50%）。没有图片时：绘制一幅覆盖在分层山脊之上的黄昏景色。
2. **页眉**（64 px，内边距 12 24）：标识（20 px，`BrandMark`）和 `BrandName`，以及切换按钮，一个 36 px 高的胶囊，显示 "No account? Sign up" 或 "Have an account? Sign in"。
3. **卡片**：最宽 420 px，34 px 圆角，1 px 边缘使用强边框颜色（70% 不透明度），带浮动阴影。其填充为磨砂玻璃：图片位于其后，经过模糊和饱和度处理，并覆盖一层 80% 不透明度的表面颜色（深色主题为 74%）。
4. **表单步骤**（内边距 32）：标题和句子、邮箱输入框、Email me a link、两条线之间的 "or"，以及每个第三方登录各一个等宽按钮。
5. **Sent 步骤**：信封、"Look in your inbox"、带 Edit 的轮廓框中的地址、Open mail app，以及 "Didn't get it?" 和重新发送按钮（"Resend in 0:30"，然后为 "Send again"）。
6. **Done 步骤**：成功标记（圆环和勾选标记）、标题和句子、两条线之间的两行详情（Account、Method），以及 Sign out。
7. **页脚**（56 px，内边距 12 24）：左侧为说明（Sent 步骤上的演示说明，或法律链接将出现的位置），右侧为 Terms 和 Privacy。

#### 状态

| 状态 | 卡片显示的内容 |
|---|---|
| 表单 | 与模式对应的标题，空输入框，三个第三方登录 |
| 无效地址 | 输入框的错误："Type your email to continue." 或 "That doesn't look like a full email address."；用户尝试发送后或离开非空输入框后，会随输入重新检查 |
| 发送中 | Email me a link 显示加载指示器；输入框只读；其他按钮忽略点击但保持焦点 |
| 链接已发送 | 主按钮显示 ✓ Link sent 625 ms |
| Sent | 信封封口；倒计时从 `ResendSeconds`（30）开始 |
| 可重新发送 | "Send again" 可用；按下后会立即发送另一个链接，重新开始倒计时，并再次播放信封动画 |
| 正在打开邮件应用 / 第三方登录 | 该按钮显示加载指示器 |
| Done | 勾选标记原地绘制；Account 和 Method（"Magic link" 或第三方登录名称） |
| 注册模式 | "Start your Fieldnote" 以及注册句子；Done 显示 "Account ready" |

<Demo name="LoginImmersiveSignUp" />

`Step`（Form、Sent、Done）、`Account`、`Method`、`Provider` 和 `IsBusy` 是只读属性；`Mode` 和 `Email` 是双向的。`Reset()` 返回空白表单且不触发事件；`SignOut()` 则与 Sign out 一样离开 Done 步骤。

#### 第三方登录、文案与链接

`Providers` 是 `LoginImmersiveProvider` 项的列表（包含名称和一个 24 单位的标识）；列表为空时，页面显示 Google、Apple 和 GitHub。`ProvidersSource` 可以从视图模型中获取它们；`IsProvidersVisible="False"` 会隐藏 "or" 和该行。所有文字（包括屏幕阅读器读出的句子）都来自 `Labels`（`LoginImmersiveLabels`）；包含数值的句子使用函数：

```csharp
login.Providers.Add(new LoginImmersiveProvider("Okta", Geometry.Parse(OktaMarkPath)));
login.Labels = new LoginImmersiveLabels
{
    SignInTitle = product => $"Welcome back to {product}",
    SentDescription = "The link works on this device for 10 minutes.",
    ResendWaitingName = seconds => $"Send again in {seconds} seconds"
};
```

Terms 和 Privacy 会触发 `Navigate`；除非处理程序设置了 `Handled`，否则当 `TermsHref` / `PrivacyHref` 是绝对网页或邮件地址时，会在浏览器中打开；没有地址的链接则会在页脚显示 "In your app, this opens the terms of service."。

#### 窄布局

布局断点跟随区块自身的宽度。宽度为 520 px 及以下时，页眉和页脚的内边距为 16，卡片下方的间距缩减为 24。当卡片内部宽度小于 380 px（区块宽度约在 414 px 以下）时，其步骤的内边距为 24 20，标题降为 28 px，第三方登录名称降为 12 px。页脚的说明文字与链接并排换行；当说明的宽度不足 220 px 时，链接会移到说明下方。

<Demo name="LoginImmersiveNarrow" />

<Demo name="LoginImmersiveFullScreen" />

#### 内容准则

- **标题**用几个词说明接下来会发生什么："Sign in to Fieldnote"、"Look in your inbox"、"Signed in"。
- **Sent 句子**说明链接在哪里有效以及有效多久；请与你的服务器规则保持一致。
- **在用户登录之前保持地址可见**：Sent 步骤通过 Edit 显示地址，Done 步骤会列出地址。
- **演示说明**（"Demo: Open mail app signs you in…"）只在 Open mail app 被模拟时出现；一旦设置了 `OpenMailAppAsync` 或 `MailAppUri`，它就会消失。

#### 常见问题

**为什么玻璃效果是图片的副本？** Avalonia 无法模糊控件后方的实时内容。图片本身从不移动，因此页面会以四分之一的尺寸渲染一次图片，对其进行模糊（28 px）和饱和度处理（×1.6），并绘制卡片下方对应图片位置的部分。当尺寸、图片或主题变化时会重建该副本；在区块调整大小期间，会拉伸之前的副本。当平台无法读回像素，或开启了减少透明度设置时，卡片会是纯色表面。

**可以使用视频或动态图片吗？** 不可以：磨砂副本只制作一次。请使用静态图片。

**页面会记住用户吗？** 不会。它会报告 `SignedIn`；保持会话是应用自己的职责。

<!-- api: LoginImmersive -->

<!-- api: LoginImmersiveProvider -->

<!-- api: LoginImmersiveLabels -->

<!-- api: LoginImmersiveTimings -->

<!-- api: LoginImmersiveSignedInEventArgs -->

<!-- api: LoginImmersiveLinkSentEventArgs -->

<!-- api: LoginImmersiveNavigateEventArgs -->

### 注册

<Demo name="SignupFormPreview" />

```xml
<arc:SignupForm x:Name="Signup" />
```

```csharp
Signup.SubmitAsync = async (details, token) =>
{
    if (await IsTakenAsync(details.Email, token))
    {
        throw new InvalidOperationException("That email is already registered.");   // shown above Create account
    }
    // Create the account with details.Name, details.Email, details.Password and details.ProductUpdates.
};
Signup.ProviderSelected += (_, e) => StartOAuth(e.Provider);   // Google, Apple or GitHub
Signup.AccountCreated += (_, e) => Track($"Signed up {e.Email} with {e.Method}");

static Task<bool> IsTakenAsync(string email, CancellationToken token) => Task.FromResult(email == "taken@northwind.studio");
static void StartOAuth(SignupFormMethod provider) { }
static void Track(string text) { }
```

未连接任何处理程序时，卡片的行为与 Arc 预览一致：表单有效时 700 ms 后完成；第三方登录按钮模拟其跳转耗时 900 ms，并在字段为空时填入一个示例用户（"Jasmine Brooks"、"jasmine@northwind.studio"）。`SubmitAsync` 接收一个 `SignupFormDetails`（去除首尾空白的姓名和邮箱、密码和订阅选项），以及一个在卡片被重置或移除时取消的令牌。返回即可显示完成面板；抛出异常则在 **Create account** 上方显示该异常的消息（消息为空时显示 `SignupFormLabels.SubmitFailed`）。卡片至少会保持忙碌状态 `SubmitMinimumDuration`（700 ms），因此快速的接口不会让加载指示器一闪而过。

<Demo name="SignupFormHandlers" />

#### 校验与强度

字段失去焦点且有文字后会进行检查，此后每次按键都会检查；**Create account** 会检查全部三个字段，并将焦点移到第一个有错误的字段。姓名需要 2 个字符，地址必须符合 name@example.com 的格式，密码需要 8 个字符。姓名和邮箱的错误显示在字段下方；密码字段变红，其提示行显示 "Use at least 8 characters."。输入密码时，提示行会显示 "Weak"、"Fair"、"Good" 或 "Strong password"，并填满相应数量的强度条（红色、琥珀色、强调色、绿色）：8 个字符、12 个字符、同时包含大小写、以及包含数字或符号，各得一分。静态的 `SignupForm.ScorePassword` 和 `SignupForm.IsValidEmail` 采用相同的规则，供你在自己的一侧使用。

#### 状态

| 状态 | 显示内容 |
|---|---|
| 表单 | 空字段，密码下方显示 "Use 8 or more characters."，四个空的强度条。 |
| 忙碌 | 字段只读，产品更新订阅选项被禁用；**Create account**（或所选的第三方登录）显示加载指示器，其他按钮忽略点击。页脚显示 "Creating your account" 或 "Opening Google"。 |
| 错误 | 消息显示在 **Create account** 上方；页脚显示 "Account not created"。 |
| 完成 | 绘制的勾选标记、"Welcome, Jasmine"、确认邮件的发送位置、账号的创建方式以及产品更新是否开启。**Start over** 会清空表单并返回。 |

`Step`、`IsBusy`、`BusyMethod`、`SignedUpWith`、`PasswordScore`、`Status` 和 `ShownError` 是只读属性；`FullName`、`Email`、`Password` 和 `ProductUpdates` 是双向的。`Submit()`、`ContinueWith(provider)`、`Reset()`、`OpenLink(link)` 和 `FocusName()` 的作用与对应按钮相同。

#### 第三方登录

按下第三方登录按钮会触发 `ProviderSelected` 并运行 `ProviderCommand`（其参数为 `SignupFormMethod`），若设置了 `ProviderAsync`，则会等待它：返回即显示完成面板，抛出异常则显示错误。没有处理程序时，跳转会模拟 `ProviderSimulationDuration` 的时长。将 `AreProvidersVisible` 设为 false 可得到仅使用邮箱的卡片。

<Demo name="SignupFormEmailOnly" />

#### 服务器错误

`ServerError` 会在 **Create account** 上方显示来自你的 API 的消息（Arc 的 `serverError`）；`SubmitAsync` 抛出的错误会取代它，直到下一次尝试。`ShownError` 报告当前显示的内容。

<Demo name="SignupFormServerError" />

```xml
<arc:SignupForm ServerError="That email is already registered." />
```

#### 链接与文字

**Terms**、**Privacy policy** 和 **Sign in** 会以 `SignupFormLink` 触发 `Navigate`（并运行 `NavigateCommand`）。打开页面时请将事件标记为已处理；否则页脚会说明链接将前往的位置（"Terms open in your app"），与 Arc 预览一致。每个字符串（包括校验消息、强度词和状态行）都在 `Labels`（`SignupFormLabels`，默认为 Arc 的英文文案）中。替换整个对象即可本地化；包含数值的句子使用函数。

```csharp
Signup.Labels = new SignupFormLabels
{
    Title = "Créez votre compte",
    Submit = "Créer le compte",
    Welcome = firstName => $"Bienvenue, {firstName}",
};
```

#### 窄容器

<Demo name="SignupFormNarrow" />

卡片最宽为 432 px，跟随自身的宽度，而不是窗口宽度。宽度为 380 px 或更小时，内边距从 32 变为 24 × 20，标题从 36 变为 28 px，第三方登录标签变为 12 px，强度条从 20 变为 14 px。宽度小于 432 时，圆角采用面板圆角（26 而不是 34）。当页脚没有足够空间将状态与 "Have an account? Sign in" 并排放置时，该提示会移到单独一行。

## API 参考

<!-- api: Login -->

<!-- api: LoginFormChangedEventArgs -->

<!-- api: SignIn -->

<!-- api: SignInAccount -->

<!-- api: SignInProvider -->

<!-- api: SignInSignedInEventArgs -->

<!-- api: SignInSignedOutEventArgs -->

<!-- api: LoginCentered -->

<!-- api: LoginCenteredSignedInEventArgs -->

<!-- api: LoginCenteredNavigateEventArgs -->

<!-- api: LoginSplit -->

<!-- api: LoginSplitAccount -->

<!-- api: LoginSplitProvider -->

<!-- api: LoginSplitLabels -->

<!-- api: LoginSplitTimings -->

<!-- api: LoginSplitSignedInEventArgs -->

<!-- api: LoginSplitSignedOutEventArgs -->

<!-- api: LoginSplitStepChangedEventArgs -->

<!-- api: LoginSplitNavigateEventArgs -->

<!-- api: LoginSplitResetLinkSentEventArgs -->

<!-- api: SignupForm -->

<!-- api: SignupFormDetails -->

<!-- api: SignupFormLabels -->

<!-- api: SignupFormCompletedEventArgs -->

<!-- api: SignupFormProviderEventArgs -->

<!-- api: SignupFormNavigateEventArgs -->


## 无障碍

### 邮箱验证码

- 卡片是一个名为 "Sign in" 的区域；每个步骤的标题都是二级标题。
- 步骤变化、发送、校验和错误会通过隐藏的实时区域以礼貌方式播报，并显示在页脚的状态行中（"Code sent"、"Code didn't match"、"Signed in with an email code"）。
- 邮箱输入框和验证码输入框都有可见标签；错误显示为它们的帮助文字。验证码的各格命名如 "Verification code, digit 1 of 6"。
- 第三方登录按钮是一个名为 "Single sign-on" 的组；每个按钮命名为 "Continue with Google"。它们的标识和通行密钥图标是装饰性的。
- Change 的名称为 "Change email"；建议按钮为 "Use emma@gmail.com"；重新发送按钮在等待时为 "Resend code, available in 29 seconds"（它仍可获得焦点，但在计时结束前不执行任何操作）。
- 登录后焦点移到欢迎标题（可从验证码获得焦点，但不是 Tab 停靠点）；点击 Change 和 Sign out 后，焦点移到邮箱输入框，并选中地址；验证码步骤则聚焦第一个验证码格。
- 键盘焦点环为每个控件外侧 2 px；鼠标点击时不显示。悬停样式仅适用于鼠标或触控笔。

##### 键盘

| 按键 | 位置 | 操作 |
|---|---|---|
| `Tab` / `Shift+Tab` | 任何位置 | 在输入框、Continue、通行密钥和第三方登录按钮以及 Sign up 之间移动；离开有文字的输入框会检查地址，出现的拼写建议会成为下一个停靠点。 |
| `Enter` | 邮箱输入框 | Continue。 |
| 数字、`Backspace`、`Delete`、方向键、粘贴 | 验证码 | 与 `OtpInput` 相同；输入最后一位数字即校验验证码。 |
| `Enter` | 验证码 | 校验已输入的内容。 |
| `Enter` / `Space` | 按钮 | 激活。 |

### 居中

- 该区块根据其模式被命名为 "Sign in" 或 "Create an account"；页脚链接是一个名为 "Legal" 的导航地标，第三方登录按钮是一个名为 "Single sign-on" 的组，每个按钮命名为 "Continue with Google" 等。
- 一个礼貌的实时区域会播报每条状态（"Waiting for your passkey"、"Code sent to …"、"Code didn't match"、"Signed in with a passkey"）；可见的状态行会重复播报。
- 忙碌的按钮保持焦点并忽略激活，而不是被禁用，因此焦点不会跳走。
- 焦点跟随流程：通行密钥按钮、邮箱输入框或第一个验证码格会在其视图出现时获得焦点，已登录视图则聚焦其标题。重新发送按钮的名称包含剩余秒数。
- 只暴露当前激活的提示和切换标签；隐藏的部分不会被读出。

#### 键盘

| 按键 | 操作 |
|---|---|
| `Tab` / `Shift+Tab` | 在切换按钮、卡片控件和页脚链接之间移动。 |
| `Enter` / `Space` | 激活已获得焦点的按钮。 |
| 邮箱输入框中的 `Enter` | 发送验证码（或显示该字段的错误）。 |
| 验证码输入框中的 `Enter` | 校验验证码。 |
| 验证码输入框的按键 | 与 `OtpInput` 相同：数字、方向键、`Backspace`、`Delete`、粘贴。 |

### 分栏

- 该区块是一个名为 "Sign in to Fieldnote"（来自 `BrandName`）的区域。每个步骤的标题都是二级标题。
- 一个礼貌的实时区域会在状态变化时读出每条状态："Finding your account"、"Password for rowan@fieldnote.app"、"Checking password"、"Password OK"、"Signing in as …"、"Signed in with your password"、"Reset link sent to …"。页脚中可见的状态行显示相同的文字，并对屏幕阅读器隐藏，以免重复朗读。
- 错误通过字段进行播报（邮箱和密码字段将其作为帮助文字报告，并标记为无效）。
- 焦点跟随流程：密码步骤中焦点在密码输入框；账号打开时焦点在标题上（问候语替换标题后焦点仍停留在那里）；芯片或 Sign out 之后焦点在邮箱输入框。密码错误后，焦点保留在输入框中，且文字被选中。
- 执行操作期间，其他按钮仍可获得焦点，但不执行任何操作，因此焦点不会跳走。
- 账号芯片的名称为 "rowan@fieldnote.app, change email"；重新发送按钮在倒计时期间命名为 "Resend link, available in 12 seconds"；单点登录按钮命名为 "Continue with Google" 并归为 "Single sign-on" 组；Terms 和 Privacy 是一个名为 "Legal" 的导航地标。图片是装饰性的。

#### 键盘

| 按键 | 操作 |
|---|---|
| `Tab` / `Shift+Tab` | Sign up，然后是步骤中的控件（邮箱输入框、Continue、Google、Apple、GitHub；或芯片、密码输入框、显示密码按钮、Forgot password?、重新发送按钮、Sign in；或 Sign out），最后是 Terms 和 Privacy |
| `Enter` | 在邮箱或密码输入框中：提交该步骤。在按钮上：按下该按钮 |
| `Space` | 按下已获得焦点的按钮 |

Escape 不做任何事：返回是芯片的职责。

### 沉浸式

- 该区块是一个以当前标题命名的区域（"Sign in to Fieldnote" / "Start your Fieldnote"）。
- 当 Sent 和 Done 步骤打开时，焦点移到它们的标题上（可获得焦点，但不是 Tab 停靠点，没有可见焦点环），以便屏幕阅读器从那里开始朗读。Edit 和 Sign out 会把焦点返回邮箱输入框；Send again 则保持焦点。
- 每个环节都通过礼貌的实时区域播报："Sending your link"、"Link sent to …"、"New link sent"、"Opening your mail app"、"Signed in with your link"、"Opening GitHub"、"Signed in with GitHub"、"Change your email"、"Signed out"、"Create an account"、"Sign in"，以及页脚的说明。
- 忙碌的按钮保持焦点并忽略点击，而不是被禁用。等待中的重新发送按钮命名为 "Send again, available in 27 seconds"，并每秒更新。
- 切换按钮是一个按钮，以其可见的句子命名；第三方登录是一个名为 "Continue with another account" 的组，每个按钮为 "Continue with Google"。Edit 的名称为 "Edit the address iris@fieldnote.app"。
- 信封、成功标记、图片和玻璃效果都是装饰性的。
- 键盘焦点环为 2 px，位于控件外侧 2 px；在图片上（切换按钮、Terms、Privacy）焦点环接近白色，以保持可见。

#### 键盘

| 按键 | 操作 |
|---|---|
| `Tab` / `Shift+Tab` | 表单：切换按钮、输入框、Email me a link、每个第三方登录、Terms、Privacy。Sent：Edit、Open mail app、重新发送按钮，然后是页脚。Done：Sign out，然后是页脚 |
| `Enter`（在输入框中） | Email me a link |
| `Enter` / `Space` | 按下已获得焦点的按钮 |
| `Escape` | 无操作：页面从不会自行关闭或返回 |

### 注册

- 卡片是一个名为 "Create an Arc account"（`SignupFormLabels.RegionName`）的区域；两个标题都是二级标题。
- 第三方登录按钮是一个名为 "Sign up with" 的组，每个按钮命名为 "Sign up with Google" 等。
- 字段以其标签命名。姓名和邮箱的错误由字段播报；密码的错误行和提交错误会被立即播报，密码的行（强度或错误）即其帮助文字。强度以文字表达，从不仅依靠颜色：强度条是装饰性的。
- 页脚状态是一个礼貌的实时区域："Creating your account"、"Account created with your email"、"Start again with new details"。
- 账号创建后，焦点移到 "Welcome" 标题上（可获得焦点，但不是 Tab 停靠点），以便屏幕阅读器停在那里；**Start over** 会将焦点返回姓名输入框。
- 键盘焦点环位于元素外侧 2 px（文字链接则为围绕文字的方框）；悬停样式仅适用于鼠标或触控笔。

#### 键盘

| 按键 | 操作 |
|---|---|
| `Tab` / `Shift+Tab` | 第三方登录、姓名、邮箱、密码、显示密码、产品更新、Create account、Terms、Privacy policy、Sign in。在完成面板上：Start over、Sign in。 |
| 字段中的 `Enter` | 提交表单，与 HTML 表单的行为相同。 |
| 按钮上的 `Enter` / `Space` | 按下该按钮。`Space` 切换订阅选项。 |


## 动效

### 邮箱验证码

- **步骤。** 新步骤从它即将前往的一侧 28 px 处滑入（前进时从右侧，后退时从左侧），使用平滑弹簧，并在 50 ms 后以 240 ms 的入场曲线淡入，同时从 4 px 模糊中变清晰。旧步骤立即离开布局，并沿同一弹簧向相反方向滑动 20 px，同时以 180 ms 的标准曲线淡出，并模糊到 4 px。已登录步骤不滑动：它直接出现。
- **高度。** 步骤切换期间，卡片的高度在平滑弹簧上从原有高度跟随新步骤变化；之后则直接跟随其内容，因此之后打开的消息只会移动它们自身。
- **头像。** 输入验证码后，一个方框会在变形弹簧上从账号行中 32 px 的头像飞到已登录步骤中 88 px 的头像；新头像在飞行的前半段淡入，旧头像随其步骤淡出。通过通行密钥或第三方登录后，头像从 70% 放大并在同一弹簧上淡入。
- **已登录。** 成功圆环在淡入的同时从 12 点钟方向顺时针绘制，耗时 600 ms（先慢后快，220 ms 后开始）；勾选标记在活泼弹簧上于 780 ms 后弹出；欢迎语、详情和 Sign out 从 4 px 模糊中上升 10 px，分别晚 140、220 和 300 ms 出现。
- **细节。** 重新发送的秒数滚动：新数字从上方 0.7 em 处落下，旧数字向下落出（活泼弹簧，160 ms 淡入和 2 px 模糊）。状态行和重新发送标签原地切换：新文字从 4 px 模糊中上升 6 px，耗时 240 ms；旧文字上移 4 px 并在 160 ms 内淡出。拼写建议在平滑弹簧上展开，并在 160 ms 内淡入。错误的验证码会使各格抖动（`OtpInput`）。
- **减少动效。** 步骤在 120 ms 内淡入，旧步骤立即消失；高度、头像、圆环和勾选标记均不移动；已登录的各组、新的状态行和新的重新发送标签在 120 ms 内淡入，而旧文字立即消失；秒数立即变化。关闭动效时，所有内容都会立即变化。

### 居中

- 步骤会从小模糊中向行进方向升入就位，同时卡片高度在弹簧上跟随变化；后退时，步骤则从上方落定。高度仅在视图切换时才做动画，因此字段消息会立即打开。
- 状态行和重新发送标签以短暂的上升和模糊进行切换；每秒重新发送的秒数向下滚动一次，发送新验证码时则向上滚动。
- 通行密钥按钮的标签交叉淡入，在设备等待验证时，指纹周围会一次性散出两圈圆环。
- 已登录的勾选标记先绘制圆环，然后绘制对勾。
- 当鼠标在区块上移动时，环形底纹会轻微漂移（外圈移动幅度更大），触摸时则保持静止。

| 变化 | 完整 | 减少 | 关闭 |
|---|---|---|---|
| 步骤出现 | 在平滑弹簧上上升 12px；60 ms 后在 240 ms 内淡入；40 ms 后从 4px 模糊中清晰化 | 淡入，120 ms | 立即 |
| 步骤离开 | 向相反方向下落 8px，淡出并模糊到 4px，180 ms | 立即 | 立即 |
| 卡片高度 | 平滑弹簧（视觉时长 0.4 s，无回弹） | 立即 | 立即 |
| 状态行、重新发送标签 | 新行上升 6px 并清晰化，240 ms；旧行上升 4px 并淡出，160 ms | 淡入，120 ms | 立即 |
| 重新发送秒数 | 在弹簧上滚动 0.7 em（0.26 s），并伴随 160 ms 淡入 | 立即 | 立即 |
| 模式切换标签 | 交叉淡入 240 ms，离开的标签上升 6px 并进入 2px 模糊 | 交叉淡入 120 ms | 立即 |
| 通行密钥圆环 | 两圈圆环，每圈 1.2 s，第二圈晚 0.6 s；仅一次 | 无 | 无 |
| 成功勾选 | 圆环在 160 ms 后用 480 ms 绘制，对勾在 520 ms 后用 320 ms 绘制 | 立即绘制完成 | 立即绘制完成 |
| 背景漂移 | 弹簧（视觉时长 0.9 s），仅鼠标 | 无 | 无 |
| 胶囊按钮（悬停） | 颜色和填充，160 ms | 立即 | 立即 |

#### 响应式行为

以区块自身的宽度为准的两个断点：520px 和 400px（见窄屏幕）。卡片最宽为 420px，并在两个 64px 的顶栏之间的舞台中居中，因此其中心始终与背景的中心对齐。

### 分栏

| 变化 | 完整 | 减少 | 关闭 |
|---|---|---|---|
| 步骤切换 | 新步骤在平滑弹簧上从下方 14 px 处升起（后退时从上方 14 px 处落下），同时在 60 ms 后的 240 ms 内淡入，并从 4 px 模糊中清晰化；旧步骤向相反方向漂移 8 px，在 180 ms 内淡出并模糊到 2 px。高度在同一弹簧上跟随变化，卡片保持居中 | 旧步骤立即消失；新步骤在 120 ms 内淡入 | 立即 |
| 密码通过 | 加载指示器消失，Sign in 在约 0.6 s 的无回弹弹簧上收缩为 44 px 的圆形，其圆角变圆；勾选标记从 60% 放大，并在 150 ms 后的 340 ms 内绘制完成 | 按钮立即收缩；勾选标记直接绘制完成 | 立即 |
| 账号打开 | 进度条在 260 ms 后（步骤出现之后）从左侧填满，耗时 1.4 s（in-out 曲线） | 进度条在较短的 400 ms 等待内填满 | 立即完整显示 |
| 问候语出现 | 标题和句子原地切换（新文字从 4 px 模糊中上升 6 px，耗时 240 ms；旧文字上移 4 px 并在 160 ms 内模糊淡出）；进度条收起，Sign out 在平滑弹簧上展开 | 文字在 120 ms 内淡入；各行立即展开和收起 | 立即 |
| 状态行、Forgot password? 标签、重新发送标签 | 与问候语相同的原地切换 | 在 120 ms 内淡入 | 立即 |
| 重置通知 | 在平滑弹簧上展开，同时淡入 | 立即展开，淡入 | 立即 |
| 倒计时 | 每个变化的数字都会滚动：新数字在快速弹簧上从上方 0.7 em 落下，旧数字向下滚出并模糊消失 | 数字立即变化 | 立即 |
| 账号芯片 | 悬停时填充并加深边框；按下时缩放至 97% | 颜色立即变化，无缩放 | 立即 |

静止时没有任何动效，也没有入场动画：图片保持静止，计时器仅在操作或倒计时进行时运行。

<Demo name="LoginSplitReducedMotion" />

#### 响应式行为

布局跟随区块自身的宽度，而不是窗口的宽度：

| 宽度 | 布局 |
|---|---|
| 1100 及以上 | 表单栏与图片栏比例为 5 : 6 |
| 760 至 1099 | 两个等宽列；当状态行和法律链接无法并排放置时，链接会移到其下方 |
| 低于 760 | 仅保留表单栏（图片隐藏） |
| 低于 480 | 页眉和页脚的边距为 20 px（而非 24 和 32）；单点登录按钮平分宽度 |

步骤保持 360 px 的列宽（空间不足时更窄），并在表单栏中居中。

### 沉浸式

| 变化 | 完整 | 减少 | 关闭 |
|---|---|---|---|
| 步骤切换 | 旧步骤立即离开布局，上移 8 px 并模糊消失（不透明度 120 ms，上移 180 ms）；新步骤在平滑弹簧上从下方 12 px 处升起，60 ms 后在 240 ms 内从 4 px 模糊中淡入。卡片高度在切换步骤时于平滑弹簧上跟随变化 | 旧步骤立即消失；新步骤在 120 ms 内淡入；高度直接跳变 | 立即 |
| 模式切换 | 标题和句子以相同方式切换（旧内容在 130 ms 内淡出）；页眉的两句话在 240 ms 内交叉切换，离开的一句上移 6 px 并进入 2 px 模糊 | 淡入淡出 | 立即 |
| Sent 打开（以及每次重新发送） | 信件等待 170 ms，然后在平滑弹簧上落入信封 30 个单位，同时淡入；从 575 ms 起，信封封盖在 0.42 s 无回弹弹簧上从打开折为合上 | 以合上状态显示 | 以合上状态显示 |
| Done 打开 | 圆环从 12 点钟方向顺时针绘制（在 140 ms 后的 575 ms 内）；从 580 ms 起，勾选标记在 0.26 s 弹簧上淡入并绘制 | 直接完整绘制 | 直接完整绘制 |
| 倒计时跳动 | 每个变化的数字从上方 0.7 em 落下，旧数字向下落 0.7 em，在 0.26 s 弹簧上进行，并在 160 ms 内淡出 | 立即变化 | 立即变化 |
| 页脚说明、重新发送标签 | 新行在 240 ms 内从 4 px 模糊中上升 6 px；旧行上移 4 px 并在 160 ms 内淡出 | 新行淡入 | 立即 |
| 按钮 | 加载指示器和标签的变化与 `Button` 相同 | 与 `Button` 相同 | 立即 |

<Demo name="LoginImmersiveReducedMotion" />

页面静止时没有动效：图片保持静止，计时器仅在演示的各个环节和倒计时期间运行。

### 注册

- 卡片的高度仅在步骤切换期间于平滑弹簧（无过冲）上滑动；静止时则立即跟随其内容，因此字段错误会立即展开，没有延迟。
- 步骤横向交叉切换：旧步骤在平滑弹簧上向后漂移 16 px，同时在 180 ms 内淡入 4 px 模糊；新步骤从 24 px 外、同一模糊中出现，并在 50 ms 后淡入（240 ms，入场曲线）。**Start over** 则以相反方向运行。
- 完成圆环从 12 点钟方向顺时针绘制（480 ms，in-out 曲线，160 ms 后开始），随后是勾选标记（320 ms，入场曲线，500 ms 后开始）。
- 密码提示行和页脚状态原地变化：新行从 4 px 模糊中上升 6 px（240 ms），旧行向上移开 4 px（160 ms）。强度条在 160 ms 内交叉淡变颜色。
- 提交错误的行在平滑弹簧上从零展开，并在 160 ms 内淡入。
- 减少动效会去掉偏移和模糊：新步骤和新行只淡入（120 ms），旧的立即消失，高度和错误行直接跳变，勾选标记直接绘制完成。关闭动效时，所有内容都会立即变化。

## 相关内容

### 邮箱验证码

- [OTP 输入](../OtpInput/zh-CN.md)：单独的验证码输入框。
- `LoginCentered`、`LoginSplit`、`LoginImmersive`：密码登录布局。
- `SignupForm`：创建账号。

### 居中

- `OtpInput`，验证码视图中的验证码输入框。
- `SignIn`，嵌入式邮箱验证码卡片。
- `LoginSplit` 和 `LoginImmersive`，用于全屏的密码登录和魔术链接布局。

### 分栏

- `SignIn`，用于卡片中的邮箱验证码登录。
- `LoginCentered`，用于安静背景上以通行密钥为先的登录。
- `LoginImmersive`，用于铺满图片上的魔术链接登录。
- `SignupForm`，用于创建账号。
- `OtpInput`，用于一次性验证码。
- `PasswordField` 和 `Input`，本页面所用的字段。

### 注册

- `PasswordField` 和 `PasswordStrength`，单独用于密码输入。
- `OtpInput`，用于基于验证码的验证。
