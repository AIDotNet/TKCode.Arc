---
title: 兩步驟驗證設定
summary: 開啟兩步驟驗證登入：選擇驗證器應用程式或簡訊，掃描真實的 QR 碼，輸入六位驗證碼，並儲存備用碼。
source-hash: 93b6f679f201
---

<Demo name="Preview" />

## 何時使用

`TwoFactorSetup` 是使用者開啟兩步驟驗證登入時開啟的卡片。它分四步完成，頂部有進度條：選擇驗證器應用程式或簡訊；掃描 QR 碼（或輸入 QR 碼下方的金鑰），或輸入手機號碼；輸入六位驗證碼以證明可用；儲存備用碼。QR 碼是真實的：它包含一個帶發行者、帳號和金鑰的 `otpauth://` 位址，由本區塊中的編碼器繪製，任何驗證器應用程式都能掃描。沒有 `CodeVerifier` 時，卡片自己依以時間為基礎的驗證碼檢查（RFC 6238，每步 30 秒，前後各允許一步的時間偏差）。

- **帳號的安全設定**：與密碼和已登入裝置放在一起。
- **註冊後必須完成的設定**：適用於要求每位成員使用兩步驟驗證的團隊。
- **管理主控台**：方式必須先驗證再儲存。

## 何時不用

- **登入時要求輸入驗證碼**：在登入頁使用 `OtpInput`。
- **顯示兩步驟驗證是否已開啟**：使用 `SecuritySettings`，它列出各種方式及其狀態。
- **無需密碼的登入**：使用 `MagicLinkSignIn`。

## 用法

```csharp
setup.Issuer = "Lumenfield";
setup.AccountName = user.Email;
setup.Secret = server.CreateSecret(user);
setup.CodeVerifier = request => server.VerifyAsync(user, request.Code);
setup.TextMessageRequested += (_, e) => sms.SendCode(e.PhoneNumber);
setup.Completed += (_, e) => server.Enable(user, e.Method, e.BackupCodes);
```

`ComputeCode`、`GenerateSecret` 和 `GenerateBackupCodes` 可在伺服器端或測試中使用。「Download」會開啟儲存對話框，把備用碼存為文字檔；處理 `BackupCodesSaving` 可以自行儲存。

<Demo name="AppOnly" />

## API 參考

<!-- api: TwoFactorSetup -->

## 無障礙

- 每次切換步驟後，焦點移到接下來要操作的地方：驗證碼輸入框、手機號碼輸入框或步驟標題，並讀出目前步驟（「Step 2 of 4: Scan this code with your app」）。
- QR 碼是一張以發行者和帳號命名的圖片；下方的金鑰可以選取並複製。
- Enter 執行目前步驟的主要操作，Alt+左方向鍵返回；輸入錯誤的驗證碼時會讀出提示，提醒檢查裝置時間。

## 動效

- 步驟依前進方向滑入，卡片高度以彈簧跟隨。QR 碼從左上角起以斜向條帶淡入，最後一步的勾號會自己畫出來。
- 減少動效時步驟交叉淡化，QR 碼和勾號立即顯示。關閉動效時一切立即改變。

## 響應式

- 卡片最寬 560 px。小於 460 px 時，QR 碼移到說明上方，內距收緊，按鈕佔滿整列。
- 無論明暗主題，QR 碼都保持整像素的模組和淺色的留白區，方便相機讀取。

## 相關元件

- `OtpInput`、`RadioCards`、`Stepper`、`Checkbox` 和 `CopyButton`：卡片由它們構成。
- `SecuritySettings` 用於總覽頁面，`MagicLinkSignIn` 用於無需密碼的登入。
