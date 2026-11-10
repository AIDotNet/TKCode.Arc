---
title: 郵件連結登入
summary: 透過郵件連結免密碼登入：輸入電子郵件地址，在「查看收件匣」頁面等待重寄倒數，連結過期後寄送新連結。
source-hash: 6d46a74e6366
---

<Demo name="Preview" />

## 何時使用

`MagicLinkSignIn` 讓人無需密碼即可登入。使用者輸入電子郵件並請求連結；卡片隨即變成「Check your inbox」，顯示他們的地址、一個在小倒數圓環上等待 `ResendDelay` 的重寄按鈕，以及返回修改地址的入口。當你的應用程式開啟一個過期的連結時，顯示過期狀態：一個按鈕即可向同一地址寄送新連結。你的應用程式在 `LinkRequested` 中寄送郵件，或透過非同步的 `LinkSender` 寄送；後者會讓按鈕顯示載入圈，寄送失敗時顯示失敗訊息。

- **大多數人很少登入的產品**：人們常常忘記密碼。
- **邀請和首次登入**：無論如何都需要確認電子郵件地址。
- **另一種登入方式**：與密碼或單一登入按鈕並列。

## 何時不用

- **一天使用很多次的應用程式**：密碼或通行金鑰比每次開啟信箱更快。
- **在同一畫面上輸入驗證碼**：使用 `OtpInput`。
- **帶第三方登入和密碼的完整登入頁**：使用 `Login` 或 `SignIn`。

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

`State` 是雙向的，路由也可以繫結它。地址會在使用者離開輸入框或按下按鈕時檢查，而不是在輸入過程中。

<Demo name="Expired" />

## API 參考

<!-- api: MagicLinkSignIn -->

## 無障礙

- 在電子郵件輸入框中按 Enter 寄送連結；在過期狀態下按 Enter 寄送新連結。
- 每次狀態改變後，焦點移到該狀態的標題（或回到輸入框），並讀出標題。錯誤和「A new link is on its way」也會被讀出。
- 重寄按鈕在等待期間被停用，按鈕文字說明還剩多久。

## 動效

- 狀態之間以短暫上移切換，卡片高度以彈簧跟隨；「Check your inbox」出現時紙飛機從左下方飛入，倒數圓環隨等待結束而變空。
- 減少動效時狀態交叉淡化，紙飛機直接出現。關閉動效時一切立即改變。

## 響應式

- 卡片最寬 420 px，在手機上佔滿寬度。小於 360 px 時內距收緊。
- 按鈕始終佔滿卡片寬度，方便點按。

## 相關元件

- `OtpInput` 用於輸入驗證碼，`TwoFactorSetup` 用於登入時的第二步驗證。
- `Login`、`LoginCentered` 和 `LoginImmersive` 用於完整的登入頁。
