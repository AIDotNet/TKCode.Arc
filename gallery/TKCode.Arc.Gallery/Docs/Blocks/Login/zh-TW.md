---
title: Login
summary: 登入與註冊頁面，從精簡的驗證碼卡片到全螢幕的分欄與沉浸式版面。
source-hash: e90efb44eaf6
---

<Demo name="SignInPreview" />

Login 是一個區塊，內含五種版面，每種都是獨立的控制項，並使用相同的流程用語：免密碼卡片（`SignIn`）會從電子郵件地址變成六位數驗證碼，再變成已登入的帳號；置中、以密碼金鑰為優先的版面（`LoginCentered`）疊在環形背景上；全螢幕版面（`LoginSplit`）讓表單與圖片並排；全螢幕圖片搭配毛玻璃卡片（`LoginImmersive`）；附密碼強度條的建立帳號表單（`SignupForm`）。每種版面開箱即用，都會執行模擬流程，因此在宿主接上非同步處理函式之前，資料不會送到任何地方；`Login` 則透過 `Variant` 屬性承載其中任一種版面。

## 何時使用

### 電子郵件驗證碼

- 以電子郵件寄送驗證碼來登入使用者的應用程式入口畫面。
- 登入卡片同時提供密碼金鑰與單一登入。
- 免密碼流程，需要先確認帳號才能繼續。

### 置中

- 以密碼金鑰為主要方式的全視窗登入或註冊頁面。
- 會退回到六位數電子郵件驗證碼的驗證流程。
- 希望同一張卡片同時處理登入與建立帳號的產品。

### 分欄

- 產品的登入頁面，表單旁需要搭配品牌圖片或插畫。
- 先輸入電子郵件的登入方式，在輸入密碼之前就已知道帳號（名稱、頭像）。
- 密碼登入，並以單一登入作為替代方式，另有自助式重設密碼。

### 沉浸式

- **不使用密碼的產品**：使用者輸入地址，點擊收件匣中的連結後即完成登入。頁面會說明每一步，並保留地址的顯示，方便發現打錯的字。
- **品牌化的入口**：圖片會填滿整個頁面，讓登入畫面帶有產品的氛圍（一片風景、團隊的辦公室，或產品本身）。請使用 `Photo`；沒有圖片時，頁面會繪製一個黃昏風景。
- **同一頁面登入與註冊**：標題列的切換會改變標題與建立帳號的文字；流程（寄送連結、確認連結）對兩者都相同。

### 註冊

`SignupForm` 是電子郵件與密碼註冊的用戶端，包含第三方登入按鈕、全名、工作電子郵件、附強度條的密碼、產品更新的選擇加入，以及帳號建立後在原處顯示的完成面板。

- 需要顯示密碼強度與產品更新選擇加入的註冊畫面。
- 希望成功面板直接顯示在原處、而不是導向其他頁面的流程。
- 在表單旁提供 Google、Apple 與 GitHub 第三方登入的註冊流程。

## 何時不用

### 電子郵件驗證碼

- **密碼登入**：請使用 `LoginCentered`、`LoginSplit` 或 `LoginImmersive`。
- **建立帳號**：請使用 `SignupForm`。
- **只需要在你自己的表單中放入驗證碼步驟**：請單獨使用 `OtpInput`。

### 置中

- 旁邊有品牌照片的密碼登入：請使用 `LoginSplit`。
- 全幅照片上的魔術連結登入：請使用 `LoginImmersive`。
- 既有頁面中的登入表單：請使用 `SignIn`。

### 分欄

- 以密碼金鑰為優先的登入，或以電子郵件寄送六位數驗證碼：請使用 `LoginCentered` 或 `SignIn`。
- 全幅照片上的魔術連結登入：請使用 `LoginImmersive`。
- 建立帳號：請使用 `SignupForm`。
- 既有頁面或對話框中的登入表單：請使用 `SignIn`；本區塊是整個頁面。

### 沉浸式

- **密碼登入**：請使用 `LoginSplit`，它會在電子郵件之後要求密碼，並處理重設連結。
- **將一次性驗證碼輸回應用程式**：請使用 `SignIn` 或 `LoginCentered`，它們會把電子郵件步驟與 `OtpInput` 配對。
- **在忙碌的畫面中**：此區塊預期自己就是頁面（或大型面板）。若要在應用程式中放一個小型登入框，請使用含 `Input` 與 `Button` 的對話框。
- **圖片的細節位於中央**：卡片會遮住中央。請選擇主體位於卡片周圍的圖片，並用 `PhotoAlignment` 移動裁切位置。

### 註冊

- **回頭的使用者**：請使用電子郵件驗證碼（`SignIn`）或置中（`LoginCentered`）版面。
- **你既有表單中的密碼**：請使用 `PasswordField`；若欄位需要規則清單，請使用 `PasswordStrength`。
- **免密碼或以驗證碼註冊**：請使用 `OtpInput`。

## 用法

### 電子郵件驗證碼

##### 電子郵件驗證碼 (SignIn)

`SignIn` 是一張登入卡片，會依序從電子郵件地址、六位數驗證碼到已登入的帳號，表單下方並有密碼金鑰按鈕與單一登入按鈕。每個步驟都會從它前往的那一側滑入，同時卡片的高度沿著彈簧跟隨變化；帳號頭像會從驗證碼步驟放大進入已登入狀態，在那裡頭像周圍會畫出一個圓環，並彈出勾選標記。預覽中的一切都是模擬的：輸入任何地址、先輸入錯誤的驗證碼，然後輸入 `123456`。


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

卡片本身不做任何驗證。請將三個處理函式接到你的驗證服務：

- `SendCodeAsync` 會在按下 "Continue"（繼續）時執行（重新傳送 "Resend"（重新傳送）時也會），帶入去除前後空白的地址。回傳要顯示在驗證碼步驟的帳號；若回傳 null，則由地址推導帳號（見下方的帳號）。拋出例外會停留在電子郵件步驟，並在欄位下方顯示 `SendFailedMessage`。
- `VerifyCodeAsync` 會在輸入完整驗證碼時執行。回傳 true 即登入，回傳 false 表示驗證碼錯誤。拋出例外會顯示 `VerifyFailedMessage`。
- `SignInWithAsync` 會為密碼金鑰按鈕，以及每個帶有 `SignInMethod` 的第三方登入執行（`Kind` 為 `Passkey` 或 `Provider`，`Provider` 則為其名稱）。回傳帳號；回傳 null 則使用第一個已知帳號。拋出例外會停留在電子郵件步驟，並在狀態列說明。

呼叫 `Reset()` 或卡片離開視覺樹時，所有權杖都會被取消；之後才到達的結果會被忽略。沒有設定處理函式時，卡片會模擬 Arc 的預覽：寄送需 800 ms，檢查需 700 ms 並接受 `DemoCode`（`"123456"`），密碼金鑰需 1100 ms，第三方登入需 900 ms。

`SignedIn` 會回報帳號與登入方式（`SignInSignedInEventArgs.Account`、`.Method`），而 `SignedInCommand` 會以該帳號執行。`SignedOut` 會在已登入步驟的「Sign out」（登出）按鈕被按下時觸發。`Step`、`Account`、`Method`、`IsBusy` 與 `Status` 為唯讀屬性，可供繫結；`Email` 為雙向，因此你可以預先填入欄位。

###### 連接你的驗證服務

<Demo name="SignInHandlers" />

示範會由假服務回應：驗證碼為 `424242`，密碼金鑰與第三方登入都會以 Robin Park 登入，記錄則列出所有呼叫與事件。`CodeHint` 會取代示範中 "For this demo, the code is 123456."（本示範的驗證碼為 123456。）的提示文字（若有 `VerifyCodeAsync` 處理函式卻沒有 `CodeHint`，驗證碼欄位就不會有提示文字）。示範會處理 `SignUpRequested`，因此卡片不會顯示自己的 "Sign up opens in your app"（註冊會在你的應用程式中開啟）狀態。

###### 步驟

- **電子郵件。** 地址的檢查方式與 Arc 相同：欄位有文字且失去焦點時檢查，或從第一次按下 "Continue" 起檢查；之後訊息會隨每次按鍵更新（欄位為空時顯示 "Enter your email address."，否則顯示 "Enter a full address, like name@example.com."）。常見的網域拼字錯誤（`gmial.com`、`hotmial.com`、`outlok.com`……）會在欄位下方開啟一行 "Did you mean …?"；按下其按鈕會將修正後的地址填入欄位。寄送驗證碼期間，"Continue" 會顯示載入指示器，欄位為唯讀。
- **驗證碼。** 帳號列會顯示頭像、地址，以及 Change（返回電子郵件步驟，並選取地址）。驗證碼欄位是會取得焦點的 `OtpInput`；填滿最後一格即會檢查驗證碼，按下 Verify 與 Enter 也會檢查。驗證碼不足時會要求全部六位數；錯誤時會清空各格、讓它們晃動並提示錯誤，重新輸入即清除錯誤。"Resend code in 0:30"（0:30 後重新傳送驗證碼）會開始倒數（秒數逐位滾動）；倒數到零後，Resend 會清除驗證碼、寄送新的驗證碼並重新開始計時（`ResendSeconds`）。
- **已登入。** 頭像配上成功圓環與勾選標記、"Welcome back, Emma"（歡迎回來，Emma）、地址、使用者的登入方式與 "Remembered for 30 days"（記住 30 天），以及「Sign out」（登出）按鈕（返回電子郵件步驟，地址已填入並選取）。

###### 帳號與第三方登入

```xml
<arc:SignIn>
    <arc:SignInAccount Name="Robin Park" Email="robin@park.dev" />
    <arc:SignIn.Providers>
        <arc:SignInProvider Name="Google" Mark="Google" />
        <arc:SignInProvider Name="Okta" />
    </arc:SignIn.Providers>
</arc:SignIn>
```

`Accounts` 是已知的使用者（Arc 的團隊成員）。當 `SendCodeAsync` 沒有回傳帳號時，卡片會遵循 Arc 的 `accountFor`：@ 之前的部分會以點、底線、加號與減號切開；若已知帳號的名字與第一段相符，就保留其名稱與照片，其他人則以前兩段產生名稱（"jane.doe@acme.io" 即為 Jane Doe）與姓名縮寫。`SignInAccount.ForEmail` 提供相同的規則。`Accounts` 為空時，會使用 Arc 的五位範例團隊成員（無照片；Arc 的肖像不在其 MIT 授權清單內）；預覽則會自行繪製。`SignInAccount.Photo` 接受任何 `IImage`。

`Providers` 是單一登入按鈕，以等寬欄位排列。為空時使用 Arc 的三個：Google（其四色標記）、Apple 與 GitHub（標記以文字顏色呈現）。`Mark` 選擇內建標記；`Icon` 接受你自己在 24 × 24 網格上繪製的標記，並以文字顏色填滿；兩者都沒有時，按鈕只顯示名稱。`AccountsSource` 與 `ProvidersSource` 接受任何集合，且優先於上述清單。`IsPasskeyVisible`、`IsProvidersVisible` 與 `IsSignUpVisible` 會隱藏這些部分；若密碼金鑰與第三方登入都沒有，"or" 分隔線也會一併隱藏。

###### 自訂文字與第三方登入

<Demo name="SignInCustom" />

每一段可見的文字、無障礙名稱與狀態列，都是一個屬性，預設值為 Arc 的英文文字：`Title`、`Description`、`ContinueLabel`、`CodeTitle`、`WelcomeFormat`（"Welcome back, {0}"）、`SignedInStatusFormat` 等（見 API 表格）。格式屬性接受 `{0}`。

###### 窄版容器

<Demo name="SignInNarrow" />

卡片依其取得的寬度調整，而非視窗的寬度。寬度最多為 432。寬度在 380 以下時（Arc 的容器查詢），步驟的內距縮為 24 × 20，標題為 28 px，帳號列變得更緊湊，第三方登入按鈕改用 12 px 文字；若狀態列與 "No account? Sign up"（沒有帳號？註冊）無法並排，頁尾會將狀態列放在其上方。提供的寬度為 448 以下時（Arc 的 480 px 手機斷點減去兩側各 16 px 的邊距），卡片的圓角會柔化為面板的圓角。

### 置中

<Demo name="LoginCenteredPreview" />

`LoginCentered` 是以密碼金鑰為優先的登入與註冊畫面：卡片位於由同心環組成的安靜背景上，頂列有產品標誌與登入／註冊切換，頁尾則有狀態列以及條款與隱私權連結。卡片會先顯示密碼金鑰檢視（一個密碼金鑰按鈕、"Use email instead"（改用電子郵件），以及 Google、Apple 與 GitHub），若不使用密碼金鑰，則退回以電子郵件寄送的六位數驗證碼，最後結束於已登入的帳號。同一個控制項同時涵蓋登入與註冊。

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

設定 `IsFullScreen` 代表此區塊就是整個視窗：此時它會以方角、無邊框的方式填滿宿主。沒有設定時，區塊會自行繪製外框（1px 邊框、面板圓角），且高度至少為 640px。

<Demo name="LoginCenteredFramed" />

沒有處理函式時，流程會與 Arc 預覽完全相同地模擬：密碼金鑰在 1.7 s 後解析為 `DemoEmail`（"maya@northwind.studio"），第三方登入在 0.9 s 後完成，驗證碼在 0.8 s 後「寄出」並在 0.7 s 後檢查，而 `DemoCode`（"482913"）為正確的驗證碼（驗證碼欄位會以提示文字顯示）。卡片進入已登入檢視後，`SignedIn`（以及 `SignedInCommand`）會回報地址、登入方式與模式。按下「Sign out」（登出）會返回登入模式的密碼金鑰檢視，並引發 `SignedOut`。

#### 將註冊導向入門流程

<Demo name="LoginCenteredSignUp" />

`Mode` 為雙向。設定 `Mode="SignUp"` 會讓區塊直接開啟 "Create your account"（建立你的帳號）表單（適用於 `/sign-up` 路由）；之後再設定時，卡片會依頂列切換的方式移動。註冊流程會要求工作用電子郵件、驗證驗證碼（"Verify and create account"（驗證並建立帳號）），最後停在 "Welcome to Arc"（歡迎來到 Arc）；此時 `SignedIn` 的 `Mode == SignUp`。

#### 連接你的驗證服務

<Demo name="LoginCenteredConnected" />

以四個非同步處理函式取代模擬。每個函式都會收到一個 `CancellationToken`，當使用者離開目前的動作（Cancel、Change、切換模式、登出）時會被取消。

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

| 處理函式 | 未設定時（Arc 預覽） | 拋出例外時 |
|---|---|---|
| `PasskeyAsync` | 1.7 s，然後為 `DemoEmail` | `OperationCanceledException`：顯示 "Passkey request canceled"（已取消密碼金鑰要求）；其他例外：顯示 "Your passkey could not be used"（無法使用你的密碼金鑰） |
| `SignInWithProviderAsync` | 0.9 s，然後為 `DemoEmail` | 第三方登入的按鈕會停止載入，狀態列說明登入未完成 |
| `SendCodeAsync` | 0.8 s | 電子郵件欄位顯示 "We couldn't send a code. Try again."（無法寄送驗證碼，請再試一次。）（重新傳送驗證碼也會使用此處理函式） |
| `VerifyCodeAsync` | 0.7 s，接受 `DemoCode` | 驗證碼欄位顯示 "We couldn't check the code. Try again."（無法檢查驗證碼，請再試一次。） |

請求執行期間（`IsBusy`），發起請求的按鈕會顯示載入指示器並保有焦點，其他動作會被忽略而非停用，與 Arc 的 `aria-disabled` 按鈕相同。密碼金鑰或驗證碼被接受後，卡片會在 "Passkey verified"（已驗證密碼金鑰）或 "Verified"（已驗證）上暫停（0.64 s 與 0.6 s），然後才前進。

頁尾連結會以該連結及其地址引發 `Navigate`。若要開啟你自己的頁面，請處理此事件；否則，絕對的 `http`、`https` 或 `mailto` 地址會以平台的啟動器開啟，而頁面內的地址（例如 Arc 預設的 `#terms`）則只會在狀態列說明它會前往的位置。

#### 流程

| 檢視 | 內容 | 前往 |
|---|---|---|
| 密碼金鑰 | "Sign in to Arc"（登入 Arc）、密碼金鑰按鈕、"Use email instead"（改用電子郵件）、"or"、Google / Apple / GitHub | 完成（密碼金鑰、第三方登入）或電子郵件 |
| 電子郵件 | "Sign in with email"（以電子郵件登入）或 "Create your account"（建立你的帳號）、電子郵件欄位、Send code（寄送驗證碼）／Create account（建立帳號）、"Use a passkey instead"（改用密碼金鑰，僅限登入） | 驗證碼 |
| 驗證碼 | "Check your email"（請檢查你的電子郵件）與地址及 Change、六位數驗證碼、Verify（驗證）、Resend code（重新傳送驗證碼，從 `ResendSeconds` 開始倒數） | 完成，或電子郵件（Change） |
| 完成 | 繪製的勾選標記、"Welcome back"（歡迎回來）或 "Welcome to Arc"（歡迎來到 Arc）、帳號、登入方式與 "Remembered for 30 days"（記住 30 天）、Sign out | 密碼金鑰 |

電子郵件會以 Arc 的格式檢查（某些字元、`@`、某些字元、一個點，以及再兩個字元）。欄位為空時顯示 "Enter your email address."（請輸入你的電子郵件地址。）；未填完整時顯示 "Enter a full address, like name@example.com."（請輸入完整地址，例如 name@example.com。）。錯誤會在使用者送出後，或離開一個有文字的欄位後才出現，之後會隨每次按鍵更新。驗證碼會在輸入最後一位數字時檢查（或按下 Verify／Enter 時）；驗證碼不足時顯示 "Enter all 6 digits."（請輸入全部 6 位數。），錯誤時會清空欄位並使其晃動。`Step`、`PasskeyPhase`、`IsBusy`、`Account`、`Method` 與 `Status` 為可供繫結的唯讀屬性；`StartPasskey`、`CancelPasskey`、`SignInWith`、`SwitchMode` 與 `SignOut` 的作用與對應的控制項相同。

#### 窄螢幕

<Demo name="LoginCenteredNarrow" />

區塊依其自身的寬度調整，而非視窗的寬度。寬度在 520px 以下時，列與舞台會收緊為 16px，卡片的圓角柔化為面板圓角，內距縮為 24px。寬度在 400px 以下時，標題從 36px 縮為 28px，卡片兩側改為 20px，第三方登入按鈕改用 12px 文字，讓三個按鈕仍能排在同一列。

#### 文字與在地化

每段文字都存放在 `LoginCenteredLabels` 中（預設為 Arc 的英文文案）。若要在地化或改寫，請替換整個物件；帶有數值的句子則為函式：

```csharp
Login.Labels = new LoginCenteredLabels
{
    PasskeyTitle = "Sign in to Northwind",
    WelcomeNew = "Welcome to Northwind",
    ResendInName = seconds => $"Resend available in {seconds} s"
};
```

### 分欄

<Demo name="LoginSplitPreview" />

`LoginSplit` 是兩欄的登入頁面。左欄頂端是產品品牌與 "Sign up" 連結（註冊），中間是登入步驟，底部是帶有條款與隱私權的狀態列；右欄是滿高的圖片。流程會先要求電子郵件、查詢帳號，並在密碼欄位上方以標籤顯示帳號；密碼被接受後，會以進度列開啟帳號，然後以名字向使用者問候。Google、Apple 與 GitHub 按鈕會略過密碼。"Forgot password?"（忘記密碼？）會寄出重設連結，並開始重新傳送的倒數。

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

區塊就是整個視窗時，請設定 `IsFullScreen`：它會以方角、無邊框的方式填滿宿主。沒有設定時，區塊會自行繪製外框（1 px 邊框與 26 px 圓角），高度至少為 640 px；上方的預覽是頁面寬度下的有框區塊，此時圖片欄會隱藏。

<Demo name="LoginSplitFullScreen" />

##### 流程

沒有設定處理函式時，區塊會以固定的停頓模擬後端，因此可以直接試用：

| 步驟 | 發生的事 | 模擬停頓 |
|---|---|---|
| 電子郵件 | "Continue"（繼續）會檢查地址（必填，接著須為完整地址），再查詢帳號：按鈕顯示載入指示器，欄位變為唯讀，狀態列顯示 "Finding your account"（正在尋找你的帳號） | 650 ms |
| 密碼 | 晶片顯示帳號；欄位的提示顯示示範密碼（`DemoPassword`，"fieldnote-demo"）。密碼錯誤時會顯示錯誤並選取已輸入的內容，因此下一次按鍵會將其取代 | 800 ms 檢查 |
| 已接受 | 登入轉為勾選標記，並收窄為 44 px 的圓形 | 775 ms |
| 登入中 | 帳號列、"Opening Fieldnote…"（正在開啟 Fieldnote…），以及一條在 1.4 s 內填滿的進度列 | 1.7 s（減少動態效果時為 400 ms） |
| 完成 | 標題與說明會原地變更為問候語（"All set, Rowan"（一切就緒，Rowan））以及使用者的登入方式；出現 Sign out。會引發 `SignedIn` | — |

單一登入會略過密碼：第三方登入按鈕會旋轉 1 s，然後開啟帳號。若電子郵件欄位中的地址有效，會登入該地址的帳號；否則使用 `DefaultAccount`（或 `Accounts` 中的第一個）。帳號晶片與 Sign out 都會返回電子郵件步驟，並填入地址；`SignOut()` 與 `Reset()` 則可從程式碼執行同樣的動作（`Reset` 另外會清除地址，並取消所有正在執行的動作）。

`Step`（`Email`、`Password`、`Signing`、`Done`）、`Account`、`IsBusy` 與 `StatusText` 為唯讀屬性，`StepChanged` 會回報每一次步驟的變化。

##### 帳號

已知帳號會以電子郵件比對（不區分大小寫），並顯示其自己的名稱與照片：

```xml
<arc:LoginSplit>
    <arc:LoginSplit.Accounts>
        <arc:LoginSplitAccount Name="Rowan Hale" Email="rowan@fieldnote.app" />
        <arc:LoginSplitAccount Name="Iris Vance" Email="iris@fieldnote.app" />
    </arc:LoginSplit.Accounts>
</arc:LoginSplit>
```

其他任何地址都會成為以 @ 之前部分命名的帳號，並以點、底線、加號與連字號切開（"ada.lovelace@example.com" 會以 "Ada Lovelace" 登入，姓名縮寫為 "AL"，問候語為 "Ada"）。`LoginSplitAccount.FromEmail` 會在程式碼中提供相同的帳號。當檢視模型擁有此清單時，請繫結 `AccountsSource`，而不要填入 `Accounts`。

<Demo name="LoginSplitEvents" />

##### 連接你的後端

四個非同步處理函式會取代模擬，每個動作一個。每個函式都會收到一個 `CancellationToken`，當使用者返回或呼叫 `Reset()` 時會被取消。拋出例外會顯示 "Something went wrong. Try again."（發生錯誤，請再試一次。）並結束忙碌狀態。

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

設定 `VerifyPasswordAsync` 後，欄位下方的示範密碼提示會消失。流程中其他的停頓（已接受的檢查、進度列）仍會保留，並在 `Timings` 中設定。

<Demo name="LoginSplitHandlers" />

##### 忘記密碼

"Forgot password?"（忘記密碼？）會在連結寄出期間變為 "Sending…"（寄送中…），然後變為綠色勾選標記與 "Link sent"（連結已寄出）。欄位下方的一行會說明連結寄到哪裡，並提供 "Resend in 0:30"（0:30 後重新傳送）；秒數會逐一倒數，倒數到零時按鈕會顯示 "Resend link"（重新傳送連結）。按下後會立即寄出另一個連結，並重新開始倒數（`ResendSeconds`，預設為 30）。第一個連結以及每次重新傳送時，都會引發 `ResetLinkSent`（`IsResend`）。

##### 連結

"Sign up"、"Terms" 與 "Privacy" 會以所使用的 `LoginSplitLink` 引發 `Navigate`（並執行 `NavigateCommand`）。請處理此事件以自行導覽。否則，帶有地址的連結（`SignUpHref`、`TermsHref`、`PrivacyHref`）會在瀏覽器中開啟；沒有地址的連結則會在狀態列說明它在你的應用程式中會前往的位置。

##### 品牌、圖片與文字

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

- `BrandName` 在品牌標誌旁為產品命名，也會出現在登入標題（"Opening Fieldnote…"）與區塊的無障礙名稱中。`BrandMark` 可以用任一 24 × 24 的線條幾何圖形取代內建的筆記本標誌。
- `Photo` 會像封面照片一樣填滿右欄，並以 `PhotoAlignment` 為中心裁切（為圖片尺寸的比例；預設為 0.5、0.58）。沒有照片時，區塊會在淡色天空下繪製一道拉絲金屬立面。
- `Labels`（`LoginSplitLabels`）保存所有文字：標題、欄位標籤、按鈕、錯誤、狀態列，以及螢幕閱讀器所朗讀的內容。帶有數值的文字為函式，因此其他語言可以調整字詞順序。
- `Providers` 列出單一登入按鈕，每個佔一個等寬欄位。`LoginSplitProvider` 有名稱、內建標記（`Google`、`Apple`、`GitHub`），或以文字顏色填滿的自訂 `Icon`。清單為空時會隱藏分隔線與按鈕；設定 `ProvidersSource` 時則以它為準。

##### 窄螢幕

<Demo name="LoginSplitNarrow" />

### 沉浸式

<Demo name="LoginImmersivePreview" />

`LoginImmersive` 是覆蓋在圖片上的完整登入頁面：沿頂部有產品標誌與登入／註冊切換，中間是毛玻璃卡片，沿底部則是附法律連結的註記。它會以電子郵件寄出的一次性連結（魔術連結）讓使用者登入，並提供單一登入作為替代方式。卡片會依序經過三個步驟：電子郵件表單、已寄出步驟（信封會在地址上闔上，並提供 "Open mail app"（開啟郵件應用程式）與重新傳送倒數），以及完成步驟（繪製的勾選標記、帳號與登入方式，以及 Sign out）。

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

`LoginImmersive` 衍生自 `TemplatedControl`。沒有處理函式時，它會以固定延遲（`Timings`）執行示範：寄送需 900 ms，接著按鈕會顯示 "Link sent"（連結已寄出）625 ms，然後才出現已寄出步驟；"Open mail app" 會在一秒後替你跟隨連結；第三方登入則會在 900 ms 後回應。每個處理函式都會取代其中一個步驟：

| 處理函式 | 呼叫時機 | 回傳 |
|---|---|---|
| `SendLinkAsync` | 以有效地址按下 "Email me a link"（以電子郵件寄送連結），以及按下 "Send again"（再寄一次） | 完成即繼續；拋出例外則在欄位下方顯示錯誤 |
| `OpenMailAppAsync` | 按下 "Open mail app" | 連結被跟隨後回傳 `true`（進入完成步驟），回傳 `false` 則停留在已寄出步驟 |
| `SignInWithProviderAsync` | 按下單一登入按鈕；第二個引數為欄位中有效的地址，否則為 null | 已登入的地址；若使用者取消則為 null |

若沒有設定 `OpenMailAppAsync`，但設定了 `MailAppUri`（例如 `mailto:`），"Open mail app" 會以 `LoginImmersiveLink.MailApp` 引發 `Navigate`；除非被處理，否則會開啟該地址。之後頁面會停留在已寄出步驟，直到應用程式呼叫 `CompleteSignIn()`（例如伺服器看到連結已被跟隨時）。每個處理函式都會取得一個取消權杖：當使用者返回、切換模式、登出，或區塊離開畫面時，權杖會被取消。

<Demo name="LoginImmersiveEvents" />

#### 結構

1. **圖片**：`Photo` 以填滿的方式裁切（如同 CSS `object-fit: cover`），並依 `PhotoAlignment` 放置（預設為水平 50 %、垂直 58 %），上方覆蓋一層平面遮罩，讓白色的頁首與頁尾保持清楚可讀（淺色主題為 30 % 深色，深色主題為 50 %）。沒有照片時：在層疊的山脊上繪製黃昏景色。
2. **頁首**（64 px，內距 12 24）：標誌（20 px，`BrandMark`）與 `BrandName`，以及切換按鈕；切換按鈕是 36 px 的膠囊，顯示 "No account? Sign up"（沒有帳號？註冊）或 "Have an account? Sign in"（已有帳號？登入）。
3. **卡片**：最寬 420 px，圓角 34 px，1 px 邊緣使用強烈邊框顏色的 70 %，並有浮動陰影。其填色為毛玻璃：背後的圖片經過模糊與飽和處理，上方覆蓋表面顏色的 80 %（深色主題為 74 %）。
4. **表單步驟**（內距 32）：標題與說明、電子郵件欄位、"Email me a link"（以電子郵件寄送連結）、兩條線之間的 "or"，以及每個第三方登入各一個等寬按鈕。
5. **已寄出步驟**：信封、"Look in your inbox"（請查看你的收件匣）、外框盒中的地址（附 Edit）、"Open mail app"，以及 "Didn't get it?"（沒收到？）與重新傳送按鈕（先是 "Resend in 0:30"（0:30 後重新傳送），然後是 "Send again"（再寄一次））。
6. **完成步驟**：成功標記（圓環與勾選）、標題與說明、位於兩條線之間的兩列細節（Account、Method），以及 Sign out。
7. **頁尾**（56 px，內距 12 24）：左側為註記（已寄出步驟上為示範說明，或法律連結將出現的位置），右側為 Terms 與 Privacy。

#### 狀態

| 狀態 | 卡片顯示 |
|---|---|
| 表單 | 依模式而定的標題、空白欄位、三個第三方登入 |
| 無效的地址 | 欄位的錯誤："Type your email to continue."（請輸入電子郵件以繼續。）或 "That doesn't look like a full email address."（這看起來不像完整的電子郵件地址。）；使用者嘗試寄送後，或離開一個有文字的欄位後，會隨輸入再次檢查 |
| 寄送中 | "Email me a link" 顯示載入指示器；欄位為唯讀；其他按鈕忽略按下但保有焦點 |
| 連結已寄出 | 主要按鈕顯示 ✓ Link sent 625 ms |
| 已寄出 | 信封闔上；倒數從 `ResendSeconds`（30）開始 |
| 可重新傳送 | "Send again" 已啟用；按下後會立即寄出另一個連結、重新開始倒數，並再次播放信封動畫 |
| 開啟郵件應用程式／第三方登入 | 該按鈕顯示載入指示器 |
| 完成 | 勾選標記自行繪製；Account 與 Method（"Magic link"（魔術連結）或第三方登入的名稱） |
| 註冊模式 | "Start your Fieldnote"（開始使用 Fieldnote）與註冊的說明；完成步驟顯示 "Account ready"（帳號已就緒） |

<Demo name="LoginImmersiveSignUp" />

`Step`（Form、Sent、Done）、`Account`、`Method`、`Provider` 與 `IsBusy` 為唯讀屬性；`Mode` 與 `Email` 為雙向。`Reset()` 會返回空白表單，且不引發事件；`SignOut()` 則會如同 Sign out 一樣離開完成步驟。

#### 第三方登入、文字與連結

`Providers` 是 `LoginImmersiveProvider` 項目的清單（一個名稱與 24 單位的標記）；清單為空時，頁面會顯示 Google、Apple 與 GitHub。`ProvidersSource` 可從檢視模型提供它們，而 `IsProvidersVisible="False"` 會隱藏 "or" 與該列。每一段文字，包括給螢幕閱讀器的句子，都來自 `Labels`（`LoginImmersiveLabels`）；帶有數值的句子為函式：

```csharp
login.Providers.Add(new LoginImmersiveProvider("Okta", Geometry.Parse(OktaMarkPath)));
login.Labels = new LoginImmersiveLabels
{
    SignInTitle = product => $"Welcome back to {product}",
    SentDescription = "The link works on this device for 10 minutes.",
    ResendWaitingName = seconds => $"Send again in {seconds} seconds"
};
```

Terms 與 Privacy 會引發 `Navigate`；除非處理函式設定了 `Handled`，否則為絕對網址或郵件地址的 `TermsHref` / `PrivacyHref` 會在瀏覽器中開啟；沒有地址的連結則會在頁尾顯示 "In your app, this opens the terms of service."（在你的應用程式中，這會開啟服務條款。）。

#### 窄版配置

斷點依區塊自身的寬度而定。在 520 px 及以下，頁首與頁尾的內距為 16，卡片下方的間距縮為 24。當卡片內部寬度小於 380 px（區塊約在 414 px 以下）時，步驟的內距為 24 20，標題縮為 28 px，第三方登入名稱為 12 px。頁尾註記會與連結並排換行；當註記少於 220 px 時，連結會移到其下方。

<Demo name="LoginImmersiveNarrow" />

<Demo name="LoginImmersiveFullScreen" />

#### 內容指引

- **標題**應以幾個字說明接下來會發生什麼，例如 "Sign in to Fieldnote"（登入 Fieldnote）、"Look in your inbox"（請查看你的收件匣）、"Signed in"（已登入）。
- **已寄出的說明**應說明連結在哪裡有效、有效多久；請與你伺服器的規則相符。
- **在使用者登入之前，請保持地址可見**：已寄出步驟會連同 Edit 一起顯示地址，完成步驟則會列出地址。
- **示範註記**（"Demo: Open mail app signs you in…"（示範：Open mail app 會讓你登入……））只會在 Open mail app 為模擬時顯示；一旦你設定 `OpenMailAppAsync` 或 `MailAppUri`，它就會消失。

#### 常見問題

**為什麼毛玻璃是圖片的複本？** Avalonia 無法模糊控制項背後的即時內容。圖片本身從不移動，因此頁面會將它以四分之一的尺寸渲染一次，模糊（28 px）並飽和（×1.6），再繪製卡片下方、與圖片對齊的部分。當尺寸、圖片或主題改變時，複本會重建；在區塊調整大小期間，會沿用先前的複本並加以拉伸。當平台無法讀回像素，或「減少透明度」設定開啟時，卡片會是實心表面。

**可以使用影片或動態圖片嗎？** 不行：毛玻璃複本只會製作一次。請使用靜態圖片。

**頁面會記住使用者嗎？** 不會。它會回報 `SignedIn`；保留工作階段是應用程式的工作。

<!-- api: LoginImmersive -->

<!-- api: LoginImmersiveProvider -->

<!-- api: LoginImmersiveLabels -->

<!-- api: LoginImmersiveTimings -->

<!-- api: LoginImmersiveSignedInEventArgs -->

<!-- api: LoginImmersiveLinkSentEventArgs -->

<!-- api: LoginImmersiveNavigateEventArgs -->

### 註冊

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

未接上任何處理函式時，卡片的行為與 Arc 預覽相同：有效的表單在 700 ms 後完成；第三方登入按鈕會模擬 900 ms 的跳轉，並在欄位為空時填入一位範例人物（"Jasmine Brooks"、"jasmine@northwind.studio"）。`SubmitAsync` 會收到一個 `SignupFormDetails`（去除前後空白的名稱與電子郵件、密碼與產品更新的選擇加入），以及一個權杖，當卡片被重設或移除時會被取消。回傳即顯示完成面板；拋出例外則會在 **Create account**（建立帳號）上方顯示該例外的訊息（若訊息為空，則顯示 `SignupFormLabels.SubmitFailed`）。卡片至少會忙碌 `SubmitMinimumDuration`（700 ms），因此很快的 API 不會讓載入指示器一閃而過。

<Demo name="SignupFormHandlers" />

#### 驗證與強度

欄位離開且有文字時會被檢查，之後每次按鍵都會檢查；**Create account**（建立帳號）會檢查全部三個欄位，並將焦點移到第一個錯誤的欄位。名稱需要 2 個字元，地址必須看起來像 name@example.com，密碼需要 8 個字元。名稱與電子郵件會在欄位下方顯示錯誤；密碼欄位會變成紅色，其文字為 "Use at least 8 characters."（請至少使用 8 個字元。）。輸入密碼時，該行會顯示 "Weak"（弱）、"Fair"（普通）、"Good"（良好）或 "Strong password"（強密碼），並會填滿相應數量的強度條（紅、琥珀、強調色、綠）：8 個字元、12 個字元、大小寫並存，以及一個數字或符號，各得一分。靜態的 `SignupForm.ScorePassword` 與 `SignupForm.IsValidEmail` 採用相同的規則，供你的一側使用。

#### 狀態

| 狀態 | 顯示內容 |
|---|---|
| 表單 | 空白欄位，密碼下方為 "Use 8 or more characters."（請使用 8 個或以上的字元。），以及四個空白的強度條。 |
| 忙碌 | 欄位為唯讀，選擇加入停用；**Create account**（建立帳號）或所選的第三方登入會顯示載入指示器，其他按鈕忽略按下。頁尾顯示 "Creating your account"（正在建立你的帳號）或 "Opening Google"（正在開啟 Google）。 |
| 錯誤 | 訊息會在 **Create account**（建立帳號）上方開啟；頁尾顯示 "Account not created"（未建立帳號）。 |
| 完成 | 繪製的勾選標記、"Welcome, Jasmine"（歡迎，Jasmine）、確認信寄送的位置、帳號的建立方式，以及產品更新是否開啟。**Start over**（重新開始）會清空表單並返回。 |

`Step`、`IsBusy`、`BusyMethod`、`SignedUpWith`、`PasswordScore`、`Status` 與 `ShownError` 為唯讀屬性；`FullName`、`Email`、`Password` 與 `ProductUpdates` 為雙向。`Submit()`、`ContinueWith(provider)`、`Reset()`、`OpenLink(link)` 與 `FocusName()` 的作用與按鈕相同。

#### 第三方登入

按下第三方登入會引發 `ProviderSelected` 並執行 `ProviderCommand`（其參數為 `SignupFormMethod`），然後在設定了 `ProviderAsync` 時等待它：回傳即顯示完成面板，拋出例外則顯示錯誤。沒有處理函式時，跳轉會模擬 `ProviderSimulationDuration` 的時間。若要使用只有電子郵件的卡片，請將 `AreProvidersVisible` 設為 false。

<Demo name="SignupFormEmailOnly" />

#### 伺服器錯誤

`ServerError` 會在 **Create account**（建立帳號）上方顯示來自你 API 的訊息（Arc 的 `serverError`）；由 `SubmitAsync` 拋出的錯誤會取代其位置，直到下一次嘗試為止。`ShownError` 會回報畫面上顯示的內容。

<Demo name="SignupFormServerError" />

```xml
<arc:SignupForm ServerError="That email is already registered." />
```

#### 連結與文字

**Terms**（條款）、**Privacy policy**（隱私權政策）與 **Sign in**（登入）會以 `SignupFormLink` 引發 `Navigate`（並執行 `NavigateCommand`）。開啟頁面時請將事件標示為已處理；否則頁尾會說明連結會前往的位置（"Terms open in your app"（條款會在你的應用程式中開啟）），與 Arc 預覽相同。每一段文字，包括驗證訊息、強度用語與狀態列，都在 `Labels` 中（`SignupFormLabels`，即 Arc 的英文文案）。若要在地化，請替換整個物件；帶有數值的句子則為函式。

```csharp
Signup.Labels = new SignupFormLabels
{
    Title = "Créez votre compte",
    Submit = "Créer le compte",
    Welcome = firstName => $"Bienvenue, {firstName}",
};
```

#### 窄版容器

<Demo name="SignupFormNarrow" />

卡片最寬為 432 px，依其自身寬度調整，而非視窗的寬度。在 380 px 以下，內距從 32 改為 24 × 20，標題從 36 縮為 28 px，第三方登入標籤縮為 12 px，強度條從 20 縮為 14 px。寬度小於 432 時，圓角採用面板圓角（26 而非 34）。當頁尾沒有足夠空間將狀態列與 "Have an account? Sign in"（已有帳號？登入）並排時，提示會移到自己的一行。

## API 參考

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



## 無障礙

### 電子郵件驗證碼

- 卡片是名為 "Sign in" 的區域；每個步驟的標題都是二級標題。
- 步驟變更、寄送、檢查與錯誤會透過隱藏的即時區域以禮貌方式通報，並顯示在頁尾的狀態列中（"Code sent"（驗證碼已寄出）、"Code didn't match"（驗證碼不符）、"Signed in with an email code"（已以電子郵件驗證碼登入））。
- 電子郵件欄位與驗證碼欄位都有可見的標籤；錯誤即為其說明文字。驗證碼的各格命名如 "Verification code, digit 1 of 6"（驗證碼，第 1 位，共 6 位）。
- 第三方登入按鈕是名為 "Single sign-on"（單一登入）的群組；每個按鈕命名為 "Continue with Google"（以 Google 繼續）。它們的標記與密碼金鑰圖示僅作裝飾。
- Change 命名為 "Change email"（變更電子郵件）；建議按鈕為 "Use emma@gmail.com"（使用 emma@gmail.com）；等待期間，重新傳送按鈕為 "Resend code, available in 29 seconds"（重新傳送驗證碼，29 秒後可用）（它仍可取得焦點，但在計時結束前不會有任何動作）。
- 登入後，焦點會移到歡迎標題（可由程式碼取得焦點，但不是 Tab 停駐點）；按下 Change 與 Sign out 後，焦點會移到已選取地址的電子郵件欄位；驗證碼步驟會將焦點放在第一格。
- 鍵盤焦點環位於每個控制項外側 2 px；滑鼠指標的焦點不顯示焦點環。懸停樣式僅套用於滑鼠或觸控筆。

##### 鍵盤操作

| 按鍵 | 位置 | 動作 |
|---|---|---|
| `Tab` / `Shift+Tab` | 任何地方 | 依序移動於欄位、"Continue"（繼續）、密碼金鑰與第三方登入按鈕，以及 Sign up（註冊）；離開有文字的欄位會檢查地址，出現的拼字建議會是下一個停駐點。 |
| `Enter` | 電子郵件欄位 | "Continue"（繼續）。 |
| 數字、`Backspace`、`Delete`、方向鍵、貼上 | 驗證碼 | 與 `OtpInput` 相同；最後一位數字會檢查驗證碼。 |
| `Enter` | 驗證碼 | 驗證目前已輸入的內容。 |
| `Enter` / `Space` | 按鈕 | 啟動。 |

### 置中

- 此區塊依其模式命名為 "Sign in"（登入）或 "Create an account"（建立帳號）；頁尾連結是名為 "Legal"（法律）的導覽地標，第三方登入按鈕則是名為 "Single sign-on"（單一登入）的群組，每個按鈕命名為 "Continue with Google"（以 Google 繼續）等。
- 一個禮貌的即時區域會通報每個狀態（"Waiting for your passkey"（等待你的密碼金鑰）、"Code sent to …"（驗證碼已寄至……）、"Code didn't match"（驗證碼不符）、"Signed in with a passkey"（已以密碼金鑰登入））；可見的狀態列會重複同樣的內容。
- 忙碌中的按鈕會保留焦點並忽略啟動，而不是被停用，因此焦點不會跳動。
- 焦點會依流程移動：密碼金鑰按鈕、電子郵件欄位，或第一格驗證碼會在其檢視出現時取得焦點；已登入檢視則為其標題。重新傳送按鈕的名稱會包含剩餘秒數。
- 只會公開目前作用中的提示與切換標籤；隱藏的內容不會被朗讀。

#### 鍵盤操作

| 按鍵 | 動作 |
|---|---|
| `Tab` / `Shift+Tab` | 在切換、卡片的控制項與頁尾連結之間移動。 |
| `Enter` / `Space` | 啟動已聚焦的按鈕。 |
| 電子郵件欄位中的 `Enter` | 寄送驗證碼（或顯示欄位的錯誤）。 |
| 驗證碼欄位中的 `Enter` | 驗證驗證碼。 |
| 驗證碼欄位的按鍵 | 與 `OtpInput` 相同：數字、方向鍵、`Backspace`、`Delete`、貼上。 |

### 分欄

- 此區塊是名為 "Sign in to Fieldnote"（登入 Fieldnote）的區域（來自 `BrandName`）。每個步驟的標題都是二級標題。
- 一個禮貌的即時區域會在狀態變更時朗讀每個狀態："Finding your account"（正在尋找你的帳號）、"Password for rowan@fieldnote.app"（rowan@fieldnote.app 的密碼）、"Checking password"（正在檢查密碼）、"Password OK"（密碼正確）、"Signing in as …"（正在以……登入）、"Signed in with your password"（已以你的密碼登入）、"Reset link sent to …"（重設連結已寄至……）。頁尾可見的狀態列會顯示相同文字，並對螢幕閱讀器隱藏，以免重複朗讀。
- 錯誤會透過欄位通報（電子郵件與密碼欄位會將錯誤作為說明文字，並標示為無效）。
- 焦點會依流程移動：密碼步驟上是密碼欄位；帳號開啟時是標題（當問候語取代標題時，焦點會留在那裡）；在帳號晶片或 Sign out 之後則是電子郵件欄位。輸入錯誤的密碼後，焦點會留在欄位中，且已輸入的文字會被選取。
- 動作執行期間，其他按鈕仍可取得焦點但不會有動作，因此焦點不會跳動。
- 帳號晶片命名為 "rowan@fieldnote.app, change email"（rowan@fieldnote.app，變更電子郵件）；重新傳送按鈕在倒數期間命名為 "Resend link, available in 12 seconds"（重新傳送連結，12 秒後可用）；單一登入按鈕命名為 "Continue with Google"（以 Google 繼續）並歸入 "Single sign-on"（單一登入）群組；Terms 與 Privacy 是名為 "Legal"（法律）的導覽地標。圖片僅作裝飾。

#### 鍵盤操作

| 按鍵 | 動作 |
|---|---|
| `Tab` / `Shift+Tab` | 依序為 "Sign up"（註冊）、步驟（電子郵件欄位、"Continue"（繼續）、Google、Apple、GitHub；或晶片、密碼欄位、顯示密碼按鈕、"Forgot password?"（忘記密碼？）、重新傳送按鈕、Sign in；或 Sign out），然後是 Terms 與 Privacy |
| `Enter` | 在電子郵件或密碼欄位中：送出該步驟。在按鈕上：按下它 |
| `Space` | 按下已聚焦的按鈕 |

「Escape」不會有任何作用：返回是帳號晶片的工作。

### 沉浸式

- 此區塊是以目前標題命名的區域（"Sign in to Fieldnote" / "Start your Fieldnote"（開始使用 Fieldnote））。
- Sent 與 Done 步驟開啟時，焦點會移到它們的標題（可取得焦點，但不是 Tab 停駐點，也沒有可見的焦點環），因此螢幕閱讀器會從那裡開始朗讀。Edit 與 Sign out 會將焦點返回電子郵件欄位；Send again 則保留焦點。
- 每個動作都會透過禮貌的即時區域通報："Sending your link"（正在寄送你的連結）、"Link sent to …"（連結已寄至……）、"New link sent"（已寄出新連結）、"Opening your mail app"（正在開啟你的郵件應用程式）、"Signed in with your link"（已以你的連結登入）、"Opening GitHub"（正在開啟 GitHub）、"Signed in with GitHub"（已以 GitHub 登入）、"Change your email"（變更你的電子郵件）、"Signed out"（已登出）、"Create an account"（建立帳號）、"Sign in"（登入），以及頁尾的註記。
- 忙碌中的按鈕會保留焦點並忽略按下，而不是變成停用。等待中的重新傳送按鈕命名為 "Send again, available in 27 seconds"（再寄一次，27 秒後可用），並每秒更新。
- 切換按鈕是一個按鈕，以其可見的句子命名；第三方登入是名為 "Continue with another account"（以其他帳號繼續）的群組，每個按鈕為 "Continue with Google"（以 Google 繼續）。Edit 命名為 "Edit the address iris@fieldnote.app"（編輯地址 iris@fieldnote.app）。
- 信封、成功標記、圖片與毛玻璃僅作裝飾。
- 鍵盤焦點環為 2 px，位於控制項外側 2 px；在圖片上（切換、Terms、Privacy）焦點環接近白色，以確保可見。

#### 鍵盤操作

| 按鍵 | 動作 |
|---|---|
| `Tab` / `Shift+Tab` | 表單：切換、欄位、"Email me a link"（以電子郵件寄送連結）、每個第三方登入、Terms、Privacy。已寄出：Edit、"Open mail app"（開啟郵件應用程式）、重新傳送按鈕，然後是頁尾。完成：Sign out，然後是頁尾 |
| `Enter`（在欄位中） | "Email me a link"（以電子郵件寄送連結） |
| `Enter` / `Space` | 按下已聚焦的按鈕 |
| `Escape` | 沒有任何作用：頁面不會自行關閉或返回 |

### 註冊

- 卡片是名為 "Create an Arc account"（建立 Arc 帳號）的區域（`SignupFormLabels.RegionName`）；兩個標題都是二級標題。
- 第三方登入按鈕是名為 "Sign up with"（以……註冊）的群組，每個按鈕命名為 "Sign up with Google"（以 Google 註冊）等。
- 欄位以其標籤命名。名稱與電子郵件的錯誤由欄位通報；密碼的錯誤行與提交錯誤會以強勢方式通報，密碼的行（強度或錯誤）即為其說明文字。強度以文字表達，而不只靠顏色：強度條僅作裝飾。
- 頁尾狀態是禮貌的即時區域："Creating your account"（正在建立你的帳號）、"Account created with your email"（已以你的電子郵件建立帳號）、"Start again with new details"（請以新的資料重新開始）。
- 帳號建立後，焦點會移到 "Welcome" 標題（可取得焦點，但不是 Tab 停駐點），讓螢幕閱讀器停在那裡；**Start over**（重新開始）會將焦點返回名稱欄位。
- 鍵盤焦點環位於元素外側 2 px（文字連結則為方形）；懸停樣式僅套用於滑鼠或觸控筆。

#### 鍵盤操作

| 按鍵 | 動作 |
|---|---|
| `Tab` / `Shift+Tab` | 第三方登入、名稱、電子郵件、密碼、顯示密碼、產品更新、Create account（建立帳號）、Terms、Privacy policy（隱私權政策）、Sign in（登入）。完成面板上：Start over（重新開始）、Sign in（登入）。 |
| 欄位中的 `Enter` | 送出表單，如同 HTML 表單。 |
| 按鈕上的 `Enter` / `Space` | 按下它。`Space` 會切換產品更新的選擇加入。 |

## 動態效果

### 電子郵件驗證碼

- **步驟。** 新步驟會從它前往的那一側、距離 28 px 處進入（前進時從右側，後退時從左側），使用平滑彈簧，在 50 ms 後以進入曲線於 240 ms 內淡入，並從 4 px 的模糊中變得清晰。舊步驟會立即離開版面，並以同一彈簧向另一側滑動 20 px，同時在標準曲線上於 180 ms 內淡出並模糊至 4 px。已登入的步驟不會滑動：它就只是在那裡。
- **高度。** 步驟變更期間，卡片的高度會以平滑彈簧從原有高度跟隨新步驟；之後會直接跟隨其內容，因此之後才開啟的訊息只會移動它們自己。
- **頭像。** 驗證碼之後，一個方塊會以變形彈簧，從帳號列中 32 px 的頭像飛到已登入步驟中 88 px 的頭像；新頭像在飛行的前半段淡入，舊頭像則隨其步驟淡出。密碼金鑰或第三方登入之後，頭像會以同一彈簧從 70 % 放大並淡入。
- **已登入。** 成功圓環在淡入的同時，從 12 點方向順時針繪製，耗時 600 ms（緩入緩出，220 ms 後開始）；勾選標記在 780 ms 後以俐落彈簧彈出；歡迎語、細節與 Sign out 則從 4 px 的模糊中上升 10 px，分別延遲 140、220 與 300 ms。
- **細節。** 重新傳送的秒數會滾動：新數字從上方 0.7 em 落入，舊數字則向下落出（俐落彈簧，160 ms 淡出與 2 px 模糊）。狀態列與重新傳送標籤會原地替換：新文字在 240 ms 內從 4 px 模糊中上升 6 px，舊文字則上移 4 px 並在 160 ms 內淡出。拼字建議會以平滑彈簧開啟，並在 160 ms 內淡入。錯誤的驗證碼會使各格晃動（`OtpInput`）。
- **減少動態效果。** 步驟在 120 ms 內淡入，舊步驟立即消失；高度、頭像、圓環與勾選標記都不移動；已登入的群組、新狀態列與新的重新傳送標籤在 120 ms 內淡入，舊文字則立即消失；秒數立即變更。關閉動態效果時，所有內容都立即變更。

### 置中

- 步驟會沿行進方向，從輕微的模糊中升入定位，同時卡片的高度沿彈簧跟隨；返回時，步驟會從上方落定。高度只會在檢視變更時做動畫，因此欄位訊息會立即開啟。
- 狀態列與重新傳送標籤以短促的上升與模糊進行替換；重新傳送的秒數每秒向下滾動，並在寄出新驗證碼時向上回捲。
- 密碼金鑰按鈕的標籤會交叉淡化；在要求裝置驗證時，指紋會向外擴散兩圈圓環，僅一次。
- 已登入的勾選會先繪製圓環，再繪製勾號。
- 滑鼠在區塊上移動時，環形背景會稍微飄移（外圈移動較多），觸控時則保持不動。

| 變化 | 完整 | 減少 | 關閉 |
|---|---|---|---|
| 步驟進入 | 以平滑彈簧上升 12px；在 60 ms 後以 240 ms 淡入；在 40 ms 後從 4px 模糊中變清晰 | 淡入，120 ms | 立即 |
| 步驟離開 | 向另一側下落 8px，淡出並模糊至 4px，180 ms | 立即 | 立即 |
| 卡片高度 | 平滑彈簧（視覺時長 0.4 s，無彈跳） | 立即 | 立即 |
| 狀態列、重新傳送標籤 | 新的一行上升 6px 並變清晰，240 ms；舊的一行上移 4px 並淡出，160 ms | 淡入，120 ms | 立即 |
| 重新傳送秒數 | 以彈簧（0.26 s）滾動 0.7 em，並以 160 ms 淡出 | 立即 | 立即 |
| 模式切換標籤 | 以 240 ms 交叉淡化，離開的標籤上升 6px 並進入 2px 模糊 | 交叉淡化，120 ms | 立即 |
| 密碼金鑰圓環 | 兩個圓環，各 1.2 s，第二個晚 0.6 s；僅一次 | 無 | 無 |
| 成功勾選 | 圓環在 160 ms 後開始，耗時 480 ms；勾號在 520 ms 後開始，耗時 320 ms | 立即完整繪製 | 立即完整繪製 |
| 背景飄移 | 彈簧（視覺時長 0.9 s），僅限滑鼠 | 無 | 無 |
| 膠囊按鈕（懸停） | 顏色與填滿，160 ms | 立即 | 立即 |

#### 響應式行為

依區塊自身的寬度有兩個斷點：520px 與 400px（見窄螢幕）。卡片最寬為 420px，並在兩條 64px 列之間的舞台中保持置中，因此它的中心永遠是背景的中心。

### 分欄

| 變化 | 完整 | 減少 | 關閉 |
|---|---|---|---|
| 步驟變更 | 新步驟在平滑彈簧上從下方 14 px 升起（後退時從上方 14 px 落下），並在 60 ms 後以 240 ms 淡入，從 4 px 模糊中變清晰；舊步驟則向另一側漂移 8 px，在 180 ms 內淡出並模糊至 2 px。高度以同一彈簧跟隨，卡片保持置中 | 舊步驟立即消失；新步驟在 120 ms 內淡入 | 立即 |
| 密碼被接受 | 載入指示器消失，Sign in 以無彈跳的彈簧（約 0.6 s）收窄為 44 px 的圓形，並使其角落變圓；勾號從 60 % 放大，在 150 ms 後以 340 ms 繪製 | 按鈕立即收窄；勾號直接完整繪製 | 立即 |
| 帳號開啟 | 步驟出現 260 ms 後，進度列從左側在 1.4 s 內填滿（緩入緩出曲線） | 進度列在較短的 400 ms 等待內填滿 | 立即完整顯示 |
| 問候語出現 | 標題與句子原地替換（新文字在 240 ms 內從 4 px 模糊中上升 6 px；舊文字上移 4 px 並在 160 ms 內模糊淡出）；進度列收起，Sign out 以平滑彈簧展開 | 文字在 120 ms 內淡入；各列立即展開與收起 | 立即 |
| 狀態列、Forgot password? 標籤、重新傳送標籤 | 與問候語相同的原地替換 | 在 120 ms 內淡入 | 立即 |
| 重設通知 | 以平滑彈簧開啟，同時淡入 | 立即開啟，並淡入 | 立即 |
| 倒數 | 每個變化的數字都會滾動：新數字以快速彈簧從上方 0.7 em 落入，舊數字向下滾動並模糊淡出 | 數字立即變更 | 立即 |
| 帳號晶片 | 懸停時填滿並加深邊框；按下時縮放至 97 % | 顏色立即變更，不縮放 | 立即 |

靜止時沒有任何動態，也沒有進場動畫：圖片靜止不動，計時器只會在動作或倒數執行時運作。

<Demo name="LoginSplitReducedMotion" />

#### 響應式行為

版面依區塊自身的寬度調整，而非視窗的寬度：

| 寬度 | 版面 |
|---|---|
| 1100 以上 | 表單與圖片欄以 5 : 6 的比例排列 |
| 760 至 1099 | 兩個等寬欄；若狀態列與法律連結無法並排，連結會移到其下方 |
| 760 以下 | 僅有表單欄（圖片隱藏） |
| 480 以下 | 頁首與頁尾周圍為 20 px，而非 24 與 32；第三方登入按鈕共用寬度 |

步驟會保持 360 px 的欄寬（空間不足時會更窄），並在表單欄中置中。

### 沉浸式

| 變化 | 完整 | 減少 | 關閉 |
|---|---|---|---|
| 步驟變更 | 舊步驟立即離開版面，上移 8 px 並模糊淡出（透明度 120 ms，上移 180 ms）；新步驟在平滑彈簧上從下方 12 px 升起，在 60 ms 後以 240 ms 從 4 px 模糊中淡入。步驟變更期間，卡片的高度以平滑彈簧跟隨 | 舊步驟立即消失；新步驟在 120 ms 內淡入；高度直接跳變 | 立即 |
| 模式變更 | 標題與句子以相同方式替換（舊的在 130 ms 內離開）；頁首的兩句以 240 ms 交叉切換，離開的那句上移 6 px 並進入 2 px 模糊 | 淡化 | 立即 |
| 已寄出開啟（以及每次重新傳送） | 信件等待 170 ms，接著以平滑彈簧下落 30 個單位進入信封，同時淡入；於 575 ms 起，信封蓋以 0.42 s 無彈跳的彈簧由開啟折為闔上 | 直接以闔上的狀態顯示 | 直接以闔上的狀態顯示 |
| 完成開啟 | 圓環從 12 點方向順時針繪製（於 140 ms 後的 575 ms 時）；於 580 ms 起，勾號淡入並以 0.26 s 彈簧繪製 | 直接完整繪製 | 直接完整繪製 |
| 倒數跳動 | 每個變化的數字從上方 0.7 em 落入，舊數字下落 0.7 em，使用 0.26 s 的彈簧，並在 160 ms 內淡出 | 立即變更 | 立即變更 |
| 頁尾註記、重新傳送標籤 | 新的一行在 240 ms 內從 4 px 模糊中上升 6 px；舊的一行上移 4 px 並在 160 ms 內淡出 | 新的一行淡入 | 立即 |
| 按鈕 | 載入指示器與標籤的變化與 `Button` 相同 | 與 `Button` 相同 | 立即 |

<Demo name="LoginImmersiveReducedMotion" />

頁面靜止時沒有任何動態：圖片靜止不動，計時器只會為示範的節拍與倒數運作。

### 註冊

- 卡片的高度只在步驟變更時以平滑彈簧滑動（無過衝）；靜止時會立即跟隨其內容，因此欄位錯誤會無延遲地開啟。
- 步驟會左右交錯：舊步驟以平滑彈簧向後漂移 16 px，同時在 180 ms 內淡出並模糊至 4 px；新步驟則從相同的模糊中、距離 24 px 處進入，並在 50 ms 後以 240 ms（進入曲線）淡入。**Start over**（重新開始）則以相反方向執行。
- 完成圓環從 12 點方向順時針繪製（160 ms 後開始，480 ms，緩入緩出），接著是勾號（500 ms 後開始，320 ms，進入曲線）。
- 密碼行與頁尾狀態會原地替換：新的一行在 240 ms 內從 4 px 模糊中上升 6 px，舊的一行則在 160 ms 內上移 4 px。強度條會在 160 ms 內交叉淡化其顏色。
- 提交錯誤會以平滑彈簧從零展開其列，並在 160 ms 內淡入。
- 減少動態效果時會取消位移與模糊：新步驟與新的行只會淡入（120 ms），舊的立即消失，高度與錯誤列直接跳變，勾號立即繪製完成。關閉動態效果時，所有內容都立即變更。

## 相關元件

### 電子郵件驗證碼

- [OTP input](../OtpInput/zh-TW.md)：單獨使用的驗證碼欄位。
- `LoginCentered`、`LoginSplit`、`LoginImmersive`：密碼登入版面。
- `SignupForm`：建立帳號。

### 置中

- `OtpInput`：驗證碼檢視中的驗證碼欄位。
- `SignIn`：嵌入式電子郵件驗證碼卡片。
- `LoginSplit` 與 `LoginImmersive`：全螢幕密碼登入與魔術連結版面。

### 分欄

- `SignIn`：卡片中的電子郵件驗證碼登入。
- `LoginCentered`：安靜背景上以密碼金鑰為優先的登入。
- `LoginImmersive`：全幅照片上的魔術連結登入。
- `SignupForm`：建立帳號。
- `OtpInput`：一次性驗證碼。
- `PasswordField` 與 `Input`：組成本頁的欄位。

### 註冊

- `PasswordField` 與 `PasswordStrength`：單獨使用的密碼輸入。
- `OtpInput`：以驗證碼進行驗證。
