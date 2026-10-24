---
title: Login
summary: Sign in and sign up pages, from a compact code card to full screen split and immersive layouts.
---

<Demo name="SignInPreview" />

Login is one block with five layouts, each a control of its own with the same flow vocabulary: a passwordless card that morphs from an email address to a six digit code to the signed in account (`SignIn`), a centered passkey first layout over a ring backdrop (`LoginCentered`), a full screen layout with the form beside a picture (`LoginSplit`), a full screen picture with a glass card (`LoginImmersive`) and a create account form with a password strength meter (`SignupForm`). Every layout runs a simulated flow out of the box, so nothing is sent anywhere until a host wires an async handler; `Login` hosts any of them behind a `Variant` property.

## When to use

### Email code

- The entry screen of an app that signs people in with a code sent by email.
- A sign in card that also offers passkeys and single sign-on.
- Passwordless flows that should confirm the account before continuing.

### Centered

- A full-window sign in or sign up page where passkeys are the main method.
- Auth flows that fall back to a six digit email code.
- Products that want one card for signing in and creating an account.

### Split

- The sign-in page of a product with a brand picture or illustration to show beside the form.
- Email-first sign-in, where the account (name, avatar) is known before the password is typed.
- Password sign-in with single sign-on as an alternative, and a self-service password reset.

### Immersive

- **A product without passwords**: the person types an address, follows the link in their inbox and is in. The
  page explains each beat and keeps the address visible so a typo is easy to catch.
- **A branded entry point**: the picture fills the page, so the sign-in screen carries the product's mood (a
  landscape, the team's office, the product itself). Use `Photo`; without one the page draws a dusk landscape.
- **Sign in and sign up on one page**: the header switch flips the heading and the account-creation copy; the flow
  (send a link, confirm it) is the same for both.

### Sign up

`SignupForm` is the client side of an email and password sign-up: provider buttons, a full name, a work email, a password
with a strength meter, a product updates opt-in, and an in-place completion panel once the account exists.

- Sign-up screens that should show password strength and a product updates opt-in.
- Flows that want the success panel in place instead of a redirect.
- Sign-ups that offer Google, Apple and GitHub beside the form.

## When not to use

### Email code

- **Password sign in**: use `LoginCentered`, `LoginSplit` or `LoginImmersive`.
- **Creating an account**: use `SignupForm`.
- **Only the code step inside your own form**: use `OtpInput` on its own.

### Centered

- Password sign in beside a brand photo: use `LoginSplit`.
- Magic link sign in over a full-bleed photo: use `LoginImmersive`.
- A sign in form inside an existing page: use `SignIn`.

### Split

- Passkey-first sign-in, or a six-digit code by email: use `LoginCentered` or `SignIn`.
- Sign-in by magic link over a full-bleed photo: use `LoginImmersive`.
- Creating an account: use `SignupForm`.
- A sign-in form inside an existing page or dialog: use `SignIn`; this block is a whole page.

### Immersive

- **Password sign-in**: use `LoginSplit`, which asks for a password after the email and handles the reset link.
- **One-time codes typed back into the app**: use `SignIn` or `LoginCentered`, which pair the email step with an
  `OtpInput`.
- **Inside a busy screen**: the block expects to be the page (or a large panel). For a small sign-in box inside an
  app, use a dialog with `Input` and `Button`.
- **Pictures with detail in the middle**: the card covers the centre. Choose a picture whose subject sits around the
  card, and move the crop with `PhotoAlignment`.

### Sign up

- **Returning users**: use the Email code (`SignIn`) or Centered (`LoginCentered`) variants.
- **A password inside a form you already have**: use `PasswordField`, or `PasswordStrength` for a field with a rule list.
- **Passwordless or code-based sign-up**: use `OtpInput`.

## Usage

### Email code

##### Email code (SignIn)

`SignIn` is a sign in card that moves from an email address to a six digit code to the signed in account, with a passkey
button and single sign-on buttons under the form. Each step slides in from the side it is heading to while the card's
height follows on a spring; the account's avatar grows from the code step into the signed in state, where a ring draws
around it and a check pops in. Everything in the preview is simulated: type any address, a wrong code, then `123456`.


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

The card does no authentication itself. Connect three handlers to your auth provider:

- `SendCodeAsync` runs on Continue (and on Resend) with the trimmed address. Return the account to show on the code
  step, or null to derive it from the address (see Accounts below). Throw to stay on the email step with
  `SendFailedMessage` under the field.
- `VerifyCodeAsync` runs with a complete code. Return true to sign in, false for a wrong code. Throwing shows
  `VerifyFailedMessage`.
- `SignInWithAsync` runs for the passkey button and every provider with a `SignInMethod` (`Kind` is `Passkey` or
  `Provider`, `Provider` names it). Return the account, or null for the first known account. Throwing keeps the email
  step and says so in the status line.

Every token is cancelled when `Reset()` is called or the card leaves the visual tree; a result that arrives after that is
ignored. Without handlers the card simulates Arc's preview: a send takes 800 ms, a check 700 ms and accepts `DemoCode`
(`"123456"`), a passkey 1100 ms and a provider 900 ms.

`SignedIn` reports the account and the method (`SignInSignedInEventArgs.Account`, `.Method`) and `SignedInCommand` runs
with the account. `SignedOut` follows the signed in step's Sign out button. `Step`, `Account`, `Method`, `IsBusy` and
`Status` are read-only properties you can bind to; `Email` is two-way, so you can prefill the field.

###### Connecting your auth provider

<Demo name="SignInHandlers" />

The demo answers from a fake service: the code is `424242`, the passkey and the providers sign in as Robin Park, and the
log lists the calls and events. `CodeHint` replaces the demo's "For this demo, the code is 123456." helper copy (with a
`VerifyCodeAsync` handler and no `CodeHint` the code field has no helper copy). The demo handles `SignUpRequested`, so
the card does not show its own "Sign up opens in your app" status.

###### The steps

- **Email.** The address is checked as Arc does: once the field loses focus with text in it, or from the first Continue
  on; after that the message follows every keystroke ("Enter your email address." for an empty field, "Enter a full
  address, like name@example.com." otherwise). A common domain typo (`gmial.com`, `hotmial.com`, `outlok.com`, …) opens
  a "Did you mean …?" line under the field; its button puts the fixed address in the field. While a code is being sent,
  Continue shows a spinner and the field is read-only.
- **Code.** The account row shows the avatar, the address and Change (back to the email, with the address selected). The
  code field is an `OtpInput` that takes focus; filling the last slot checks the code, and so do Verify and Enter. A short
  code asks for all six digits; a wrong one clears the slots, shakes them and says so, and typing again clears the error.
  "Resend code in 0:30" counts down (the seconds roll); once it reaches zero, Resend clears the code, sends a new one and
  restarts the timer (`ResendSeconds`).
- **Signed in.** The avatar with a success ring and a check, "Welcome back, Emma", the address, how the person signed in
  and "Remembered for 30 days", and Sign out (back to the email step with the address filled in and selected).

###### Accounts and providers

```xml
<arc:SignIn>
    <arc:SignInAccount Name="Robin Park" Email="robin@park.dev" />
    <arc:SignIn.Providers>
        <arc:SignInProvider Name="Google" Mark="Google" />
        <arc:SignInProvider Name="Okta" />
    </arc:SignIn.Providers>
</arc:SignIn>
```

`Accounts` are the known people (Arc's teammates). When `SendCodeAsync` returns no account, the card follows Arc's
`accountFor`: the part before the @ is split at dots, underscores, plus and minus signs; a known account whose first name
matches the first piece keeps its name and photo, anyone else gets a name from the first two pieces ("jane.doe@acme.io"
is Jane Doe) and initials. `SignInAccount.ForEmail` exposes the same rule. Empty `Accounts` use Arc's five sample
teammates without photos (Arc's portraits are not part of its MIT registry); the preview draws its own.
`SignInAccount.Photo` takes any `IImage`.

`Providers` are the single sign-on buttons, in equal columns. Empty uses Arc's three: Google (its four colour mark),
Apple and GitHub (their marks in the text color). `Mark` picks a built-in mark; `Icon` takes a mark of your own on a 24 by
24 grid, filled in the text color; with neither the button shows the name alone. `AccountsSource` and `ProvidersSource`
take any collection and win over the lists. `IsPasskeyVisible`, `IsProvidersVisible` and `IsSignUpVisible` hide those
parts; with neither a passkey nor providers the "or" divider goes too.

###### Custom copy and providers

<Demo name="SignInCustom" />

Every visible string, accessible name and status line is a property with Arc's English text as its default: `Title`,
`Description`, `ContinueLabel`, `CodeTitle`, `WelcomeFormat` ("Welcome back, {0}"), `SignedInStatusFormat` and so on (see
the API table). Format properties take `{0}`.

###### Narrow containers

<Demo name="SignInNarrow" />

The card follows the width it is given, not the window. It is at most 432 wide. At 380 or less (Arc's container query)
the steps' padding drops to 24 by 20, the heading to 28 px, the account row tightens and the provider buttons use 12 px
text; the footer puts the status line above "No account? Sign up" when they do not fit side by side. Offered 448 or less
(Arc's 480 px phone breakpoint less two 16 px gutters) the card's corners soften to the panel radius.

### Centered

<Demo name="LoginCenteredPreview" />

`LoginCentered` is a passkey-first sign in and sign up screen: a card on a quiet backdrop of concentric rings, with the
product mark and a sign in / sign up switch in the top bar and a status line with Terms and Privacy in the footer. The card
starts on the passkey view (a passkey button, "Use email instead" and Google, Apple and GitHub), falls back to a six digit
code sent by email, and ends on the signed-in account. One control covers both sign in and sign up.

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

Set `IsFullScreen` when the block is the whole window: it then fills its host with no border and square corners. Without
it the block draws its own frame (a 1px border, the panel radius) and is at least 640px high.

<Demo name="LoginCenteredFramed" />

With no handlers the flow is simulated exactly like Arc's preview: the passkey resolves to `DemoEmail`
("maya@northwind.studio") after 1.7 s, a provider after 0.9 s, a code is "sent" in 0.8 s and checked in 0.7 s, and
`DemoCode` ("482913") is the right one (the code field shows it as helper copy). `SignedIn` (and `SignedInCommand`)
reports the address, the method and the mode once the card reaches the signed-in view. Sign out returns to the passkey
view in sign-in mode and raises `SignedOut`.

#### Route sign ups to onboarding

<Demo name="LoginCenteredSignUp" />

`Mode` is two-way. Set `Mode="SignUp"` to open the block on the "Create your account" form (for a `/sign-up` route);
setting it later moves the card the way the top bar's switch does. The sign-up flow asks for a work email, verifies the
code ("Verify and create account") and ends on "Welcome to Arc"; `SignedIn` then has `Mode == SignUp`.

#### Connecting your auth service

<Demo name="LoginCenteredConnected" />

Replace the simulation with four asynchronous handlers. Each one receives a `CancellationToken` that is cancelled when the
person moves on (Cancel, Change, the mode switch, Sign out).

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

| Handler | Without it (Arc's preview) | When it throws |
|---|---|---|
| `PasskeyAsync` | 1.7 s, then `DemoEmail` | `OperationCanceledException`: "Passkey request canceled"; anything else: "Your passkey could not be used" |
| `SignInWithProviderAsync` | 0.9 s, then `DemoEmail` | The provider's button stops spinning; the status says the sign in did not finish |
| `SendCodeAsync` | 0.8 s | The email field shows "We couldn't send a code. Try again." (also used by Resend code) |
| `VerifyCodeAsync` | 0.7 s, accepts `DemoCode` | The code field shows "We couldn't check the code. Try again." |

While a request runs (`IsBusy`), the button that started it shows a spinner and keeps focus, and the other actions are
ignored rather than disabled, as Arc's `aria-disabled` buttons are. After a passkey or a code is accepted the card pauses
(0.64 s and 0.6 s) on "Passkey verified" or "Verified" before it moves on.

The footer links raise `Navigate` with the link and its address. Handle it to open your own page; otherwise an absolute
`http`, `https` or `mailto` address opens with the platform launcher, and an in-page address such as Arc's default
`#terms` only says in the status line where it would go.

#### The flow

| View | Content | Moves to |
|---|---|---|
| Passkey | "Sign in to Arc", the passkey button, "Use email instead", "or", Google / Apple / GitHub | Done (passkey, provider) or Email |
| Email | "Sign in with email" or "Create your account", the email field, Send code / Create account, "Use a passkey instead" (sign in only) | Code |
| Code | "Check your email" with the address and Change, the six digit code, Verify, Resend code (counts down from `ResendSeconds`) | Done, or Email (Change) |
| Done | The drawn check, "Welcome back" or "Welcome to Arc", the account, the method and "Remembered for 30 days", Sign out | Passkey |

The email is checked with Arc's pattern (something, `@`, something, a dot and two more characters). An empty field says
"Enter your email address."; an incomplete one "Enter a full address, like name@example.com.". The error appears once the
person submits, or leaves a field that has text in it, and then follows each keystroke. The code is checked when the last
digit is typed (or on Verify / Enter); a short code says "Enter all 6 digits.", a wrong one clears the field and shakes it.
`Step`, `PasskeyPhase`, `IsBusy`, `Account`, `Method` and `Status` are read-only properties you can bind to; `StartPasskey`,
`CancelPasskey`, `SignInWith`, `SwitchMode` and `SignOut` do what the matching controls do.

#### Narrow screens

<Demo name="LoginCenteredNarrow" />

The block follows its own width, not the window's. At 520px or less the bars and the stage tighten to 16px, the card's
corners soften to the panel radius and its padding drops to 24px. At 400px or less the title drops from 36px to 28px, the
card's sides to 20px and the provider buttons tighten to 12px text so all three stay on one row.

#### Text and localization

Every string is in `LoginCenteredLabels` (Arc's English copy by default). Replace the whole object to localize or reword
it; sentences that carry a value are functions:

```csharp
Login.Labels = new LoginCenteredLabels
{
    PasskeyTitle = "Sign in to Northwind",
    WelcomeNew = "Welcome to Northwind",
    ResendInName = seconds => $"Resend available in {seconds} s"
};
```

### Split

<Demo name="LoginSplitPreview" />

`LoginSplit` is a two-column sign-in page. The left column holds the product's brand and a "Sign up" link at the top, the
sign-in steps in the middle and a status line with Terms and Privacy at the bottom; the right column is a full-height
picture. The flow asks for the email first, looks the account up, shows it as a chip above the password field and, once
the password is accepted, opens the account with a progress line before greeting the person by name. Google, Apple and
GitHub buttons skip the password. "Forgot password?" sends a reset link and starts a resend countdown.

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

Set `IsFullScreen` when the block is the whole window: it fills its host with square corners and no border. Without it
the block draws its own frame (a 1 px border and 26 px corners) and is at least 640 px high; the preview above is the
framed block at the width of the page, where the picture column is hidden.

<Demo name="LoginSplitFullScreen" />

##### The flow

With no handlers the block simulates a back end with fixed pauses, so it can be tried as it is:

| Step | What happens | Simulated pause |
|---|---|---|
| Email | Continue checks the address (required, then a complete address), then looks the account up: the button shows a spinner, the field turns read-only, the status line says "Finding your account" | 650 ms |
| Password | The chip shows the account; the field's hint shows the demo password (`DemoPassword`, "fieldnote-demo"). A wrong password shows an error and selects what was typed, so the next key replaces it | 800 ms check |
| Accepted | Sign in turns into a check and narrows to a 44 px circle | 775 ms |
| Signing | The account row, "Opening Fieldnote…" and a progress line that fills over 1.4 s | 1.7 s (400 ms with reduced motion) |
| Done | The heading and sentence change in place to the greeting ("All set, Rowan") and how the person signed in; Sign out appears. `SignedIn` is raised | — |

Single sign-on skips the password: the provider's button shows a spinner for 1 s, then the account opens. It signs in the
account of the address in the email field when that address is valid, otherwise `DefaultAccount` (or the first of
`Accounts`). The account chip and Sign out both go back to the email step with the address filled in; `SignOut()` and
`Reset()` do the same from code (`Reset` also clears the address and cancels whatever runs).

`Step` (`Email`, `Password`, `Signing`, `Done`), `Account`, `IsBusy` and `StatusText` are read-only properties, and
`StepChanged` reports every change of step.

##### Accounts

Known accounts are matched on the email without regard to case and show their own name and picture:

```xml
<arc:LoginSplit>
    <arc:LoginSplit.Accounts>
        <arc:LoginSplitAccount Name="Rowan Hale" Email="rowan@fieldnote.app" />
        <arc:LoginSplitAccount Name="Iris Vance" Email="iris@fieldnote.app" />
    </arc:LoginSplit.Accounts>
</arc:LoginSplit>
```

Any other address becomes an account named after the part before the @, split at dots, underscores, plus signs and
hyphens ("ada.lovelace@example.com" signs in as "Ada Lovelace", initials "AL", greeted as "Ada").
`LoginSplitAccount.FromEmail` gives you the same account in code. Bind `AccountsSource` instead of filling `Accounts` when
a view model owns the list.

<Demo name="LoginSplitEvents" />

##### Connecting your back end

Four asynchronous handlers replace the simulation, one per action. Each receives a `CancellationToken` that is cancelled
when the person goes back or `Reset()` is called. Throwing shows "Something went wrong. Try again." and ends the busy
state.

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

With `VerifyPasswordAsync` set, the demo password hint under the field disappears. The other pauses of the sequence
(the accepted check, the progress line) stay and are set in `Timings`.

<Demo name="LoginSplitHandlers" />

##### Forgot password

"Forgot password?" changes to "Sending…" while the link goes out, then to a green check and "Link sent". A line under the
field says where the link went and offers "Resend in 0:30"; the seconds roll down and, at zero, the button reads "Resend
link". Pressing it sends another link at once and restarts the countdown (`ResendSeconds`, 30 by default).
`ResetLinkSent` is raised for the first link and for every resend (`IsResend`).

##### Links

"Sign up", "Terms" and "Privacy" raise `Navigate` (and run `NavigateCommand`) with the `LoginSplitLink` that was used.
Handle the event to navigate yourself. Otherwise a link with an address (`SignUpHref`, `TermsHref`, `PrivacyHref`) opens
it in the browser, and a link without one says in the status line where it would go in your app.

##### Brand, picture and words

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

- `BrandName` names the product beside the mark, in the signing heading ("Opening Fieldnote…") and in the block's
  accessible name. `BrandMark` replaces the built-in notebook mark with any 24 × 24 stroked geometry.
- `Photo` fills the right column like a cover photo, cropped around `PhotoAlignment` (fractions of the picture's size;
  0.5, 0.58 by default). Without a photo the block draws a brushed-metal facade under a pale sky.
- `Labels` (`LoginSplitLabels`) holds every text: headings, field labels, buttons, errors, the status line and what screen
  readers hear. Texts with values are functions, so word order can change in other languages.
- `Providers` lists the single sign-on buttons, one equal column each. `LoginSplitProvider` has a name, a built-in mark
  (`Google`, `Apple`, `GitHub`) or a custom `Icon` filled in the text color. An empty list hides the divider and the
  buttons; `ProvidersSource` wins when set.

##### Narrow screens

<Demo name="LoginSplitNarrow" />

### Immersive

<Demo name="LoginImmersivePreview" />

`LoginImmersive` is a whole sign-in page laid over a picture: the product's mark and a sign-in / sign-up switch along
the top, a frosted glass card in the middle and a note with the legal links along the bottom. It signs people in with
a one-time link sent by email (a magic link) and offers single sign-on as the alternative. The card walks through
three steps: the email form, the Sent step (an envelope folds shut over the address, Open mail app and a resend
countdown) and the Done step (a drawn check, the account and the method, Sign out).

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

`LoginImmersive` derives from `TemplatedControl`. With no handlers it runs a demo on fixed delays (`Timings`):
sending takes 900 ms, the button then says Link sent for 625 ms before the Sent step appears, Open mail app follows
the link for you after a second, and a provider answers after 900 ms. Each handler replaces one beat:

| Handler | Called when | Return |
|---|---|---|
| `SendLinkAsync` | Email me a link with a valid address, and Send again | Complete to continue; throw to show an error under the field |
| `OpenMailAppAsync` | Open mail app | `true` once the link was followed (the Done step), `false` to stay on Sent |
| `SignInWithProviderAsync` | A single sign-on button; the second argument is the valid address in the field, or null | The signed-in address, or null when the person cancelled |

Without `OpenMailAppAsync` but with `MailAppUri` (for example `mailto:`), Open mail app raises `Navigate` with
`LoginImmersiveLink.MailApp` and, unless handled, opens the address; the page then waits on the Sent step until the app
calls `CompleteSignIn()` (for example when its server sees the link followed). Every handler gets a cancellation
token: it is cancelled when the person goes back, switches mode, signs out or the block leaves the screen.

<Demo name="LoginImmersiveEvents" />

#### Anatomy

1. **Picture**: `Photo` cropped to fill (like CSS `object-fit: cover`) and placed by `PhotoAlignment` (default 50 %
   across, 58 % down), under a flat scrim that keeps the white header and footer readable (30 % dark in the light
   theme, 50 % in the dark). Without a photo: a drawn dusk over layered ridges.
2. **Header** (64 px, padding 12 24): the mark (20 px, `BrandMark`) and `BrandName`, and the switch, a 36 px pill
   with "No account? Sign up" or "Have an account? Sign in".
3. **Card**: at most 420 px wide, 34 px corners, a 1 px edge in the strong border color at 70 %, the floating shadow.
   Its fill is frosted glass: the picture behind it, blurred and saturated, under the surface color at 80 % (74 % in
   the dark theme).
4. **Form step** (padding 32): heading and sentence, the Email field, Email me a link, "or" between two rules, and one
   equal button per provider.
5. **Sent step**: the envelope, "Look in your inbox", the address in an outlined box with Edit, Open mail app, and
   "Didn't get it?" with the resend button ("Resend in 0:30", then "Send again").
6. **Done step**: the success mark (a ring and a check), the heading and sentence, two detail rows (Account, Method)
   between rules, and Sign out.
7. **Footer** (56 px, padding 12 24): a note on the left (the demo explanation on the Sent step, or where a legal
   link would go) and Terms and Privacy on the right.

#### States

| State | What the card shows |
|---|---|
| Form | Heading for the mode, empty field, three providers |
| Invalid address | The field's error: "Type your email to continue." or "That doesn't look like a full email address."; checked again as the person types once they tried to send, or left a non-empty field |
| Sending | Email me a link shows a spinner; the field is read-only; other buttons ignore presses but keep focus |
| Link sent | The main button says ✓ Link sent for 625 ms |
| Sent | The envelope seals; the countdown runs from `ResendSeconds` (30) |
| Resend ready | "Send again" is enabled; pressing it sends another link at once, restarts the countdown and plays the envelope again |
| Opening mail app / a provider | That button shows a spinner |
| Done | The check draws itself; Account and Method ("Magic link" or the provider's name) |
| Sign-up mode | "Start your Fieldnote" and the sign-up sentence; Done says "Account ready" |

<Demo name="LoginImmersiveSignUp" />

`Step` (Form, Sent, Done), `Account`, `Method`, `Provider` and `IsBusy` are read-only properties; `Mode` and `Email`
are two-way. `Reset()` returns to an empty form without events, `SignOut()` leaves the Done step as Sign out does.

#### Providers, text and links

`Providers` is a list of `LoginImmersiveProvider` items (a name and a 24-unit mark); while it is empty the page shows
Google, Apple and GitHub. `ProvidersSource` takes them from a view model, and `IsProvidersVisible="False"` hides "or"
and the row. Every text, including the screen-reader sentences, comes from `Labels` (`LoginImmersiveLabels`);
sentences with a value are functions:

```csharp
login.Providers.Add(new LoginImmersiveProvider("Okta", Geometry.Parse(OktaMarkPath)));
login.Labels = new LoginImmersiveLabels
{
    SignInTitle = product => $"Welcome back to {product}",
    SentDescription = "The link works on this device for 10 minutes.",
    ResendWaitingName = seconds => $"Send again in {seconds} seconds"
};
```

Terms and Privacy raise `Navigate`; unless a handler sets `Handled`, a `TermsHref` / `PrivacyHref` that is an absolute
web or mail address opens in the browser, and a link without an address shows "In your app, this opens the terms of
service." in the footer.

#### Narrow layouts

Breakpoints follow the block's own width. At 520 px and below the header and footer pad 16 and the space under the
card shrinks to 24. When the card's inside is narrower than 380 px (a block under about 414 px), its steps pad 24 20,
the heading drops to 28 px and the provider names to 12 px. The footer note wraps beside the links, and the links drop
below it when the note has less than 220 px.

<Demo name="LoginImmersiveNarrow" />

<Demo name="LoginImmersiveFullScreen" />

#### Content guidelines

- **Headings** say what happens next, in a few words: "Sign in to Fieldnote", "Look in your inbox", "Signed in".
- **The Sent sentence** says where the link works and for how long; match it to your server's rules.
- **Keep the address visible** until the person is in: the Sent step shows it with Edit, the Done step lists it.
- **The demo note** ("Demo: Open mail app signs you in…") appears only while Open mail app is simulated; it disappears
  as soon as you set `OpenMailAppAsync` or `MailAppUri`.

#### Common questions

**Why is the glass a copy of the picture?** Avalonia cannot blur live content behind a control. The picture never
moves, so the page renders it once at a quarter of its size, blurs (28 px) and saturates (×1.6) it, and draws the part
under the card, aligned with the picture. The copy is rebuilt when the size, the picture or the theme changes; while
the block is being resized the previous copy is stretched. When the platform cannot read pixels back, or the
reduce-transparency setting is on, the card is a solid surface.

**Can I use a video or an animated picture?** No: the frosted copy is made once. Use a still picture.

**Does the page remember the person?** No. It reports `SignedIn`; keeping the session is the app's job.

<!-- api: LoginImmersive -->

<!-- api: LoginImmersiveProvider -->

<!-- api: LoginImmersiveLabels -->

<!-- api: LoginImmersiveTimings -->

<!-- api: LoginImmersiveSignedInEventArgs -->

<!-- api: LoginImmersiveLinkSentEventArgs -->

<!-- api: LoginImmersiveNavigateEventArgs -->

### Sign up

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

With nothing wired up the card behaves like Arc's preview: a valid form completes after 700 ms, and a provider button
simulates its redirect for 900 ms, filling in a sample person ("Jasmine Brooks", "jasmine@northwind.studio") when the
fields are empty. `SubmitAsync` receives a `SignupFormDetails` (the trimmed name and email, the password and the opt-in)
and a token that is cancelled when the card is reset or removed. Return to show the completion panel; throw to show the
exception's message above **Create account** (an empty message shows `SignupFormLabels.SubmitFailed`). The card stays busy
for at least `SubmitMinimumDuration` (700 ms), so a fast API does not flash the spinner.

<Demo name="SignupFormHandlers" />

#### Validation and strength

A field is checked once it is left with text in it, and from then on with every keystroke; **Create account** checks all
three and focuses the first wrong one. The name needs 2 characters, the address must look like name@example.com, and the
password needs 8 characters. Name and email show their error under the field; the password field turns red and its line
says "Use at least 8 characters.". While a password is typed the line reads "Weak", "Fair", "Good" or "Strong password"
and as many bars fill (red, amber, the accent, green): one point each for 8 characters, 12 characters, both cases, and a
digit or symbol. The static `SignupForm.ScorePassword` and `SignupForm.IsValidEmail` are the same rules, for your side.

#### States

| State | What shows |
|---|---|
| Form | Empty fields, "Use 8 or more characters." under the password and four empty bars. |
| Busy | The fields are read-only and the opt-in is disabled; **Create account** (or the chosen provider) shows a spinner and the other buttons ignore presses. The footer says "Creating your account" or "Opening Google". |
| Error | The message opens above **Create account**; the footer says "Account not created". |
| Done | The drawn check, "Welcome, Jasmine", where the confirmation went, how the account was made and whether product updates are on. **Start over** empties the form and goes back. |

`Step`, `IsBusy`, `BusyMethod`, `SignedUpWith`, `PasswordScore`, `Status` and `ShownError` are read-only properties;
`FullName`, `Email`, `Password` and `ProductUpdates` are two-way. `Submit()`, `ContinueWith(provider)`, `Reset()`,
`OpenLink(link)` and `FocusName()` do what the buttons do.

#### Providers

Pressing a provider raises `ProviderSelected` and runs `ProviderCommand` (its parameter is the `SignupFormMethod`), then
awaits `ProviderAsync` when it is set: return to show the completion panel, throw to show the error. Without a handler the
redirect is simulated for `ProviderSimulationDuration`. Set `AreProvidersVisible` to false for an email-only card.

<Demo name="SignupFormEmailOnly" />

#### Server errors

`ServerError` shows a message from your API above **Create account** (Arc's `serverError`); an error thrown by
`SubmitAsync` takes its place until the next attempt. `ShownError` reports what is on screen.

<Demo name="SignupFormServerError" />

```xml
<arc:SignupForm ServerError="That email is already registered." />
```

#### Links and text

**Terms**, **Privacy policy** and **Sign in** raise `Navigate` (and run `NavigateCommand`) with a `SignupFormLink`. Mark the
event handled when you open the page; otherwise the footer says where the link would go ("Terms open in your app"), as in
Arc's preview. Every string, including the validation messages, the strength words and the status lines, is in `Labels`
(`SignupFormLabels`, Arc's English copy). Replace the whole object to localize it; sentences with a value are functions.

```csharp
Signup.Labels = new SignupFormLabels
{
    Title = "Créez votre compte",
    Submit = "Créer le compte",
    Welcome = firstName => $"Bienvenue, {firstName}",
};
```

#### Narrow containers

<Demo name="SignupFormNarrow" />

The card is at most 432 px wide and follows its own width, not the window's. At 380 px or less the padding goes from 32 to
24 × 20, the heading from 36 to 28 px, the provider labels to 12 px and the strength bars from 20 to 14 px. Narrower than
432 the corners take the panel radius (26 instead of 34). When the footer has no room for the status beside "Have an
account? Sign in", the prompt moves to its own line.

## API reference

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


## Accessibility

### Email code

- The card is a region named "Sign in"; each step's heading is a level 2 heading.
- Step changes, sends, checks and errors are announced politely through a hidden live region and shown in the footer's
  status line ("Code sent", "Code didn't match", "Signed in with an email code").
- The email field and the code field have visible labels; errors are their help text. The code's slots are named like
  "Verification code, digit 1 of 6".
- The provider buttons are a group named "Single sign-on"; each is named "Continue with Google". Their marks and the
  passkey icon are decorative.
- Change is named "Change email"; the suggestion button "Use emma@gmail.com"; the resend button "Resend code, available
  in 29 seconds" while it waits (it stays focusable but does nothing until the timer ends).
- After signing in the focus moves to the welcome heading (focusable from code, not a Tab stop); after Change and Sign
  out it moves to the email field with the address selected; the code step focuses the first slot.
- Keyboard focus rings are 2 px outside each control; pointer focus shows none. Hover styles apply to a mouse or a pen.

##### Keyboard

| Key | Where | Action |
|---|---|---|
| `Tab` / `Shift+Tab` | Anywhere | Moves through the field, Continue, the passkey and provider buttons and Sign up; leaving the field with text in it checks the address, and a typo suggestion that appears is the next stop. |
| `Enter` | Email field | Continue. |
| Digits, `Backspace`, `Delete`, arrows, paste | Code | As in `OtpInput`; the last digit checks the code. |
| `Enter` | Code | Verify what is typed. |
| `Enter` / `Space` | Buttons | Activate. |

### Centered

- The block is named "Sign in" or "Create an account" after its mode; the footer links are a navigation landmark named
  "Legal", and the provider buttons a group named "Single sign-on", each named "Continue with Google" and so on.
- A polite live region announces every status ("Waiting for your passkey", "Code sent to …", "Code didn't match", "Signed
  in with a passkey"); the visible status line repeats it.
- Busy buttons keep focus and ignore activation instead of being disabled, so focus never jumps.
- Focus follows the flow: the passkey button, the email field or the first code slot when their view arrives, the title of
  the signed-in view. The resend button's name includes the seconds left.
- Only the active prompt and switch label are exposed; the hidden one is not read.

#### Keyboard

| Keys | Action |
|---|---|
| `Tab` / `Shift+Tab` | Move through the switch, the card's controls and the footer links. |
| `Enter` / `Space` | Activate the focused button. |
| `Enter` in the email field | Send the code (or show the field's error). |
| `Enter` in the code field | Verify the code. |
| Code field keys | As `OtpInput`: digits, arrows, `Backspace`, `Delete`, paste. |

### Split

- The block is a region named "Sign in to Fieldnote" (from `BrandName`). Each step's heading is a level-two heading.
- One polite live region reads every status as it changes: "Finding your account", "Password for rowan@fieldnote.app",
  "Checking password", "Password OK", "Signing in as …", "Signed in with your password", "Reset link sent to …". The
  visible status line in the footer shows the same text and is hidden from screen readers so it is not read twice.
- Errors are announced through the fields (the email and password fields report them as their help text and turn
  invalid).
- Focus follows the flow: the password field on the password step, the heading when the account opens (it stays there
  when the greeting replaces it), the email field after the chip or Sign out. After a wrong password the focus stays in
  the field with the text selected.
- While an action runs, the other buttons stay focusable but do nothing, so the focus never jumps.
- The account chip is named "rowan@fieldnote.app, change email"; the resend button is named "Resend link, available in 12
  seconds" while it counts down; the single sign-on buttons are named "Continue with Google" and grouped as "Single
  sign-on"; Terms and Privacy are a navigation landmark named "Legal". The picture is decorative.

#### Keyboard

| Keys | Action |
|---|---|
| `Tab` / `Shift+Tab` | Sign up, then the step (email field, Continue, Google, Apple, GitHub; or the chip, the password field, the show-password button, Forgot password?, the resend button, Sign in; or Sign out), then Terms and Privacy |
| `Enter` | In the email or password field: submit the step. On a button: press it |
| `Space` | Press the focused button |

Escape does nothing: going back is the chip's job.

### Immersive

- The block is a region named by the current heading ("Sign in to Fieldnote" / "Start your Fieldnote").
- When the Sent and Done steps open, focus moves to their heading (focusable, not a Tab stop, no visible ring), so a
  screen reader starts reading there. Edit and Sign out return focus to the email field; Send again keeps focus.
- Every beat is announced through a polite live region: "Sending your link", "Link sent to …", "New link sent",
  "Opening your mail app", "Signed in with your link", "Opening GitHub", "Signed in with GitHub", "Change your email",
  "Signed out", "Create an account", "Sign in", and the footer notes.
- Busy buttons keep focus and ignore presses instead of becoming disabled. The waiting resend button is named "Send
  again, available in 27 seconds" and updates every second.
- The switch is one button named by its visible sentence; the providers are a group named "Continue with another
  account", each button "Continue with Google". Edit is named "Edit the address iris@fieldnote.app".
- The envelope, the success mark, the picture and the glass are decorative.
- Keyboard focus rings are 2 px, 2 px outside the control; on the picture (the switch, Terms, Privacy) they are near
  white so they stay visible.

#### Keyboard

| Keys | Action |
|---|---|
| `Tab` / `Shift+Tab` | Form: the switch, the field, Email me a link, each provider, Terms, Privacy. Sent: Edit, Open mail app, the resend button, then the footer. Done: Sign out, then the footer |
| `Enter` (in the field) | Email me a link |
| `Enter` / `Space` | Press the focused button |
| `Escape` | Nothing: the page never closes or goes back on its own |

### Sign up

- The card is a region named "Create an Arc account" (`SignupFormLabels.RegionName`); both headings are level 2.
- The provider buttons are a group named "Sign up with", each named "Sign up with Google" and so on.
- Fields are named by their labels. Name and email errors are announced by the fields; the password's error line and a
  submit error are announced assertively, and the password's line (strength or error) is its help text. Strength is said
  in words, never only by color: the bars are decorative.
- The footer status is a polite live region: "Creating your account", "Account created with your email", "Start again with
  new details".
- When the account is created the focus moves to the "Welcome" heading (focusable, not a tab stop), so a screen reader
  lands on it; **Start over** returns the focus to the name field.
- Keyboard focus rings are 2 px outside the element (square around the text links); hover styles apply to a mouse or a
  pen only.

#### Keyboard

| Keys | Action |
|---|---|
| `Tab` / `Shift+Tab` | Providers, name, email, password, show password, product updates, Create account, Terms, Privacy policy, Sign in. On the completion panel: Start over, Sign in. |
| `Enter` in a field | Submit the form, as an HTML form does. |
| `Enter` / `Space` on a button | Press it. `Space` toggles the opt-in. |

## Motion

### Email code

- **Steps.** The new step comes in from 28 px on the side it is heading to (from the right going forward, from the left
  going back) on the smooth spring, fading in over 240 ms on the enter curve after 50 ms and sharpening from a 4 px blur.
  The old step leaves the layout at once and slides 20 px the other way on the same spring while it fades over 180 ms on
  the standard curve into a 4 px blur. The signed in step does not slide: it is simply there.
- **Height.** While the step changes, the card's height follows the new step on the smooth spring from the height it had;
  afterwards it follows its content directly, so messages that open later move nothing but themselves.
- **Avatar.** After a code, one box flies from the 32 px avatar in the account row to the 88 px avatar of the signed in
  step on the morph spring; the new avatar fades in over the first half of the flight and the old one fades with its step.
  After a passkey or a provider the avatar grows from 70 % as it fades in on the same spring.
- **Signed in.** The success ring draws itself clockwise from 12 o'clock over 600 ms (in-out, after 220 ms) as it fades
  in; the check pops in on the snappy spring after 780 ms; the welcome, the details and Sign out rise 10 px out of a 4 px
  blur, 140, 220 and 300 ms late.
- **Small things.** The resend seconds roll: the new digit drops in from 0.7 em above while the old one drops below (snappy
  spring, 160 ms fade and 2 px blur). The status line and the resend label swap in place: the new text rises 6 px out of a
  4 px blur over 240 ms, the old one lifts 4 px and fades over 160 ms. The typo suggestion opens on the smooth spring and
  fades in over 160 ms. A wrong code shakes the slots (`OtpInput`).
- **Reduced motion.** Steps fade in over 120 ms and the old step goes at once; the height, the avatar, the ring and the
  check do not move; the signed in groups, the new status line and the new resend label fade in over 120 ms while the
  old text goes at once; the seconds change at once. With motion off everything changes at once.

### Centered

- Steps rise into place out of a small blur, in the direction of travel, while the card's height follows on a spring;
  going back, they settle from above. The height only animates while the view changes, so field messages open at once.
- The status line and the resend label swap with a short rise and blur; the resend seconds roll down each second and back
  up when a new code is sent.
- The passkey button's label cross-fades, and two rings leave the fingerprint once while the device is asked.
- The signed-in check draws its ring, then its tick.
- The ring backdrop drifts a little with a mouse moving over the block (outer rings further), and stays still for touch.

| Change | Full | Reduced | Off |
|---|---|---|---|
| A step arrives | Rises 12px on the smooth spring; fades in over 240 ms after 60 ms; unblurs from 4px after 40 ms | Fades in, 120 ms | Immediate |
| A step leaves | Falls 8px the other way, fades and blurs to 4px, 180 ms | Immediate | Immediate |
| Card height | Smooth spring (visual duration 0.4 s, no bounce) | Immediate | Immediate |
| Status line, resend label | New line rises 6px and unblurs, 240 ms; old line rises 4px and fades, 160 ms | Fades in, 120 ms | Immediate |
| Resend seconds | Roll 0.7 em on a spring (0.26 s) with a 160 ms fade | Immediate | Immediate |
| Mode switch labels | Cross-fade 240 ms, the leaving one rising 6px into a 2px blur | Cross-fade 120 ms | Immediate |
| Passkey rings | Two rings, 1.2 s each, the second 0.6 s later; once | None | None |
| Success check | Ring 480 ms after 160 ms, tick 320 ms after 520 ms | Drawn at once | Drawn at once |
| Backdrop drift | Spring (visual duration 0.9 s), mouse only | None | None |
| Pill buttons (hover) | Color and fill, 160 ms | Immediate | Immediate |

#### Responsive behavior

Two breakpoints on the block's own width: 520px and 400px (see Narrow screens). The card is at most 420px wide and stays
centered in the stage between the 64px bars, so its center is always the backdrop's center.

### Split

| Change | Full | Reduced | Off |
|---|---|---|---|
| The step changes | The new step rises from 14 px below (going back: drops from 14 px above) on a smooth spring while it fades in and sharpens from a 4 px blur over 240 ms after 60 ms; the old step drifts 8 px the other way, fades out in 180 ms and blurs to 2 px. The height follows on the same spring, and the card stays centered | The old step goes at once; the new one fades in over 120 ms | Immediate |
| The password is accepted | The spinner leaves, Sign in narrows to a 44 px circle on a spring without bounce (about 0.6 s) and its corners round off; the check grows from 60 % and draws in over 340 ms after 150 ms | The button narrows at once; the check is drawn complete | Immediate |
| The account opens | The progress line fills from the left over 1.4 s, 260 ms after the step appears (in-out curve) | The line fills within the shorter 400 ms wait | Full at once |
| The greeting appears | Heading and sentence change in place (the new text rises 6 px out of a 4 px blur in 240 ms; the old lifts 4 px and blurs out in 160 ms); the progress line closes and Sign out opens on a smooth spring | The texts fade in over 120 ms; the rows open and close at once | Immediate |
| Status line, Forgot password? label, resend label | The same in-place change as the greeting | Fade in over 120 ms | Immediate |
| The reset notice | Opens on a smooth spring while it fades in | Opens at once, fades in | Immediate |
| The countdown | Each changed digit rolls: the new one drops in from 0.7 em above on a quick spring while the old one rolls down and blurs out | Digits change at once | Immediate |
| The account chip | Hover fills it and darkens its border; a press scales it to 97 % | Colors change at once, no scale | Immediate |

There is no motion at rest and no entrance animation: the picture is still, and timers run only while an action or the
countdown runs.

<Demo name="LoginSplitReducedMotion" />

#### Responsive behavior

The layout follows the block's own width, not the window's:

| Width | Layout |
|---|---|
| 1100 and up | Form and picture columns at 5 : 6 |
| 760 to 1099 | Two equal columns; when the status line and the legal links do not fit side by side, the links move under it |
| Below 760 | The form column alone (the picture hides) |
| Below 480 | 20 px around the header and footer instead of 24 and 32; the sign-on buttons share the width |

The steps keep a 360 px column (narrower when the space is), centered in the form column.

### Immersive

| Change | Full | Reduced | Off |
|---|---|---|---|
| The step changes | The old step leaves layout at once, lifts 8 px and blurs out (opacity 120 ms, lift 180 ms); the new one rises from 12 px below on the smooth spring, fading in from 60 ms over 240 ms out of a 4 px blur. The card's height follows on the smooth spring while the steps change | The old step goes at once; the new one fades in over 120 ms; the height jumps | Immediate |
| The mode changes | The heading and sentence swap the same way (old out in 130 ms); the header's two sentences cross in 240 ms, the leaving one lifting 6 px into a 2 px blur | Fades | Immediate |
| Sent opens (and every resend) | The letter waits 170 ms, then drops 30 units into the envelope on the smooth spring while fading in; from 575 ms the flap folds from open to shut on a 0.42 s spring without bounce | Shown sealed | Shown sealed |
| Done opens | The ring draws clockwise from twelve o'clock (575 ms after 140 ms); from 580 ms the check fades in and draws on a 0.26 s spring | Drawn whole | Drawn whole |
| The countdown ticks | Each changed digit drops in from 0.7 em above while the old one falls 0.7 em, on a 0.26 s spring, fading over 160 ms | Changes at once | Changes at once |
| Footer note, resend label | The new line rises 6 px out of a 4 px blur in 240 ms; the old one lifts 4 px and fades in 160 ms | The new line fades in | Immediate |
| Buttons | Spinner and label changes as in `Button` | As in `Button` | Immediate |

<Demo name="LoginImmersiveReducedMotion" />

The page has no motion at rest: the picture is still, and timers run only for the demo's beats and the countdown.

### Sign up

- The card's height glides on the smooth spring (no overshoot) only while the step changes; at rest it follows its content
  at once, so field errors open without lag.
- Steps cross over sideways: the old one drifts 16 px back on the smooth spring while it fades (180 ms) into a 4 px blur;
  the new one comes from 24 px out of the same blur and fades in after 50 ms (240 ms, enter curve). **Start over** runs it
  the other way.
- The completion ring draws itself clockwise from 12 o'clock (480 ms, in-out, after 160 ms), then the check (320 ms, enter
  curve, after 500 ms).
- The password line and the footer status change in place: the new line rises 6 px out of a 4 px blur (240 ms) while the
  old one lifts 4 px away (160 ms). The strength bars crossfade their color over 160 ms.
- A submit error opens its row from zero on the smooth spring and fades in over 160 ms.
- Reduced motion drops the offsets and blur: the new step and lines only fade in (120 ms), the old ones go at once, the
  height and the error row jump, and the check is drawn at once. With motion off everything changes at once.

## Related

### Email code

- [OTP input](../OtpInput/en-US.md): the code field on its own.
- `LoginCentered`, `LoginSplit`, `LoginImmersive`: password sign in layouts.
- `SignupForm`: account creation.

### Centered

- `OtpInput`, the code field of the Code view.
- `SignIn` for an embedded email code card.
- `LoginSplit` and `LoginImmersive` for full-screen password and magic link layouts.

### Split

- `SignIn` for email-code sign-in in a card.
- `LoginCentered` for passkey-first sign-in on a quiet backdrop.
- `LoginImmersive` for magic-link sign-in over a full-bleed photo.
- `SignupForm` to create an account.
- `OtpInput` for one-time codes.
- `PasswordField` and `Input`, the fields this page is built from.

### Sign up

- `PasswordField` and `PasswordStrength` for the password input on its own.
- `OtpInput` for code-based verification.
