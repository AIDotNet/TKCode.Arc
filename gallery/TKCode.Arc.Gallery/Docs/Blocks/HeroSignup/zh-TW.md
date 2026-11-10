---
title: Hero signup
summary: 登陸頁首屏：大標題加一個電子郵件輸入框，就地驗證、提交並確認，背後是安靜的點陣，下方有一排頭像作為證明。
source-hash: 93e40607d751
---

<Demo name="Preview" />

## 何時使用

`HeroSignup` 是登陸頁的第一屏，唯一的目的是收集電子郵件地址：候補名單、內測、搶先體驗。圓角小標籤說明當下的階段（「內測中」），大標題給出承諾，一句話加以說明，一個輸入框和一個按鈕完成剩下的事。地址會在輸入時驗證，透過你的處理函式提交，然後表單原地換成簡短的確認，訪客無需離開頁面頂部。表單下方是一排生成的頭像和一行證明文字（「已有 2,400 個產品團隊加入」），背後淡淡的點陣和柔和的強調色光暈讓這一屏有層次，又不需要圖片。

- **候補名單或內測頁**，電子郵件地址就是轉化。
- **發布前的產品頁**，在上線前收集意向。
- **活動首屏**，旁邊配一張圖片，內容靠起始邊對齊。

## 何時不用

- **登錄或注冊帳戶**：請使用 Auth 分組的區塊，它們處理密碼和登錄方式。
- **有往期內容可展示的電子報**：請使用 `NewsletterSignup`。
- **有多個行動按鈕、沒有表單的首屏**：請使用 `CtaSection`。

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

沒有處理函式時會模擬提交（900 毫秒，總是成功）。`Phase` 報告 `Idle`、`Sending` 或 `Done`；`SubmittedEmail` 保存已接受的地址，`Reset()`（「Use a different email」按鈕）恢復空表單。`Submit()` 可在程式碼中驗證並提交地址。

### 驗證

輸入框為空時在下方顯示 `EmptyMessage`，地址格式不對時顯示 `FormatMessage`。第一次提交之前，繼續輸入會清除錯誤；之後每次按鍵都會重新驗證。提交失敗時保留地址並顯示 `FailedMessage`。

### 對齊

<Demo name="StartAligned" />

`Alignment="Start"` 讓小標籤、標題、說明、表單和證明行都靠起始邊對齊，適合旁邊有圖片的首屏。預設是 `Center`。

### 證明行

`ProofText` 是頭像旁的文字；`ProofNames` 列出畫成生成頭像的人（最多五個）。把 `ProofText` 設為 null 可隱藏這一行，把 `IsBackdropVisible` 設為 false 可去掉點陣。

### 文案與本地化

所有文字都是帶英文預設值的屬性：`Eyebrow`、`Title`、`Description`、`Placeholder`、`EmailLabel`、`ButtonText`、`SendingText`、`Note`、`EmptyMessage`、`FormatMessage`、`FailedMessage`、`DoneTitle`、`DoneFormat`、`ResetText` 和 `ProofText`。

## API 參考

<!-- api: HeroSignup -->

## 無障礙

- 整個區塊以標題命名，大標題是一級標題。
- 輸入框的螢幕閱讀器名稱是 `EmailLabel`；錯誤出現時會播報，地址被接受時會播報確認。
- 頭像和點陣僅作裝飾，對螢幕閱讀器隱藏。

### 鍵盤

| 按鍵 | 操作 |
|---|---|
| `Tab` / `Shift+Tab` | 在輸入框和按鈕之間移動；提交成功後移到「Use a different email」。 |
| 輸入框中的 `Enter` | 驗證並提交地址。 |
| 按鈕上的 `Enter` / `Space` | 按下按鈕。 |

提交成功後焦點移到「Use a different email」；重置後回到輸入框。

## 動效

| 變化 | 完整 | 減弱 | 關閉 |
|---|---|---|---|
| 表單換成確認 | 表單淡出，確認上升出現 | 淡入淡出 | 立即 |
| 輸入框下的錯誤 | 彈簧展開 | 淡入 | 立即 |
| 提交中的按鈕 | 轉圈並切換文字 | 轉圈，淡入 | 轉圈 |

點陣和光暈是靜止的，從不動畫。

## 響應式

- 斷點按區塊自身的寬度計算。小於 760（`:compact`）時標題從 52 降到 36，邊距收緊；小於 480（`:narrow`）時標題為 28，輸入框和按鈕上下排列並占滿寬度。
- 表單最寬 500，說明最寬 600，寬螢幕上文字行不會拉得過長。
- 讓區塊隨頁面拉伸，不要給它固定的 `Width`。

## 相關元件

- `NewsletterSignup`：展示往期內容的訂閱表單。
- `CtaSection`：收尾的行動號召。
- `Input` 和 `Button`：表單由它們組成。
