---
title: 安全性設定
summary: 安全檢查、變更密碼、兩步驟驗證和登入工作階段，均可原地處理。
source-hash: 6cf960abf5f9
---

<Demo name="Overview" />

## 何時使用

- 帳戶的安全性頁面：在同一處檢查帳戶受保護的程度，並補上缺少的項目。
- 無須離開頁面即可變更密碼，輸入新密碼時同步顯示強度。
- 開啟或關閉兩步驟驗證方式、新增通行金鑰，以及查看復原碼。
- 查看帳戶在哪些裝置上登入，並登出遺失或不認識的裝置。

## 何時不用

- 登入或註冊：請使用登入表單。本區塊假設使用者已經登入。
- 工作區範圍的政策（強制兩步驟驗證、允許的登入方式）屬於管理員設定，最好在設定頁面上以 `SettingsGroup` 設定列的形式呈現。
- 表單中單獨的一個密碼欄位：直接使用 `PasswordField` 或 `PasswordStrength`。

## 用法

設定帳戶的狀態後，區塊會以四張卡片呈現：安全檢查、密碼、兩步驟驗證和登入工作階段。所有操作都原地完成。開關會雙向寫回 `IsAuthenticatorEnabled` 與 `IsTextMessageEnabled`。密碼表單、「新增通行金鑰」、復原碼和登出，都會透過路由事件通知宿主。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:SecuritySettings IsAuthenticatorEnabled="True"
                          PhoneNumber="+31 6 2045 4821"
                          PasskeyCount="2">
        <arc:SecuritySettings.Sessions>
            <arc:SecuritySession Device="MacBook Air" Client="Firefox" Location="Rotterdam, NL"
                                 Kind="Laptop" IsCurrent="True" />
            <arc:SecuritySession Device="Pixel 9" Client="Larkspur app" Location="Rotterdam, NL"
                                 Kind="Phone" LastActive="2026-10-20T08:00:00+02:00" />
        </arc:SecuritySettings.Sessions>
    </arc:SecuritySettings>
</UserControl>
```

時間會以相對時間顯示，計算基準為 `Clock`。`Clock` 是一個 `TimeProvider`，預設為系統時鐘，區塊顯示期間每 30 秒重新讀取一次。測試時可傳入固定時鐘：

```csharp
sealed class FixedClock(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now.ToUniversalTime();
}

security.Clock = new FixedClock(new DateTimeOffset(2026, 10, 20, 12, 0, 0, TimeSpan.FromHours(2)));
```

### 安全檢查

安全檢查會計算四項防護措施：密碼（`HasPassword`）、透過任一方式的兩步驟驗證、至少一個通行金鑰（`PasskeyCount`），以及復原碼（`RecoveryCodesCreatedAt`）。`ProtectionScore` 即為該數量，範圍為 0 到 4。每項防護措施在進度條上各佔一段，盾牌的色調隨分數變化：4 分為成功色，2 或 3 分為強調色，低於 2 分為警告色。`Sections` 決定顯示哪些卡片；此處只顯示安全檢查與兩步驟驗證。開啟某種驗證方式或新增通行金鑰後，進度條隨之填滿。

<Demo name="Checkup" />

頁面自行顯示摘要時，可用 `IsSummaryVisible="False"` 隱藏安全檢查。

### 變更密碼

「變更密碼」會在該設定列下方展開一個表單，包含：目前的密碼、附強度計與規則清單的新密碼，以及「取消」和「更新密碼」。「更新密碼」需要填寫目前的密碼，且新密碼的強度至少為「尚可」（`MinimumPasswordLevel`，等級 2）。按 Enter 送出，按 Escape 取消。送出時會引發 `PasswordChangeRequested`，並附上兩組密碼。將 `ErrorMessage` 留空即表示接受：表單關閉，`PasswordChangedAt` 設為時鐘的時間，該設定列顯示「剛剛更新」。若為其設定內容，則表示拒絕：錯誤訊息會顯示在目前的密碼下方，焦點回到該處。本範例中目前的密碼為 「correct horse」。

<Demo name="ChangePassword" />

沒有密碼時（`HasPassword="False"`），該設定列會提供「設定密碼」，表單只要求輸入新密碼，此時遭拒絕的提示會顯示在新密碼下方。`OpenPasswordForm()`、`ClosePasswordForm()` 與 `SubmitPasswordChange()` 可在程式碼中執行相同的操作。

### 登入工作階段

工作階段依清單排列：目前的裝置排在最前，其餘依最近活動時間排序。目前的裝置帶有「此裝置」徽章，且無法在此登出。其他每個工作階段都有「登出」按鈕。按下後，該列會淡出並收合，工作階段會從 `Sessions` 中移除，並引發 `SessionSignedOut`。「登出其他登入工作階段」會原地詢問；確認後，各列依序離開，最後由 `OtherSessionsSignedOut` 攜帶已登出的工作階段發出通知。`SignOut(session)` 與 `SignOutOtherSessions()` 可在程式碼中執行相同的操作。唯讀清單（例如陣列）會維持原樣，已登出的列則改為隱藏。

<Demo name="Sessions" />

在窄版頁面上，進度條會移到安全檢查文字的下方，「登出其他登入工作階段」則移到清單下方的橫條區域。控制項為開關、徽章或「登出」的列，控制項會保留在文字旁邊。按鈕較寬的列，則將控制項堆疊在文字下方。

### 文字標籤

`SecuritySettingsLabels` 包含所有內建文字。若要在地化，請整體取代這個物件；需要帶入值的文字則為函式。`Culture` 用於格式化日期。

```csharp
security.Labels = new SecuritySettingsLabels
{
    Culture = CultureInfo.GetCultureInfo("nl-NL"),
    PasswordHeader = "Wachtwoord",
    ChangePassword = "Wachtwoord wijzigen",
    SessionsHeader = "Waar je bent ingelogd",
    SignOut = "Afmelden",
    SignOutSession = device => $"{device} afmelden",
    SummaryCount = count => $"{count} van 4 beveiligingen aan"
};
```

## API 參考

<!-- api: SecuritySettings -->

**SecuritySession**（一般物件，可在 XAML 中撰寫）

| 屬性 | 類型 | 說明 |
|---|---|---|
| `Device` | `string` | 裝置名稱，例如「MacBook Air」。 |
| `Client` | `string?` | 瀏覽器或應用程式，顯示在裝置名稱之後。 |
| `Location` | `string?` | 裝置登入的地點，例如「Rotterdam, NL」。 |
| `LastActive` | `DateTimeOffset` | 工作階段最後一次使用的時間，以相對時間顯示，基準為 `Clock`。 |
| `Kind` | `SecurityDeviceKind` | `Desktop`、`Laptop`、`Phone`、`Tablet` 或 `Other`，決定顯示的圖示。 |
| `IsCurrent` | `bool` | 目前使用中的裝置：排在最前、標示為「此裝置」，且無法在此登出。 |

**事件引數**

| 類型 | 屬性 | 說明 |
|---|---|---|
| `SecurityPasswordChangeEventArgs` | `CurrentPassword` | 輸入的目前密碼（沒有密碼時為空）。 |
| | `NewPassword` | 新密碼。 |
| | `ErrorMessage` | 設定後即拒絕此次變更；表單保持開啟並顯示該訊息。 |
| `SecuritySessionEventArgs` | `Session` | 已登出的工作階段。 |
| `SecuritySessionsEventArgs` | `Sessions` | 由「登出其他登入工作階段」登出的工作階段，依清單順序排列。 |

**SecuritySettingsSections**（旗標列舉）：`Summary`、`Password`、`TwoStep`、`Sessions`、`All`（預設）、`None`。

**SecuritySettingsLabels**：`Culture`，以及每個內建文字各一個屬性，例如 `SummaryProtected`、`SummaryCount`、`PasswordChanged`、`ChangePassword`、`UpdatePassword`、`TextMessageDescription`、`PasskeyCount`、`SessionsHeader`、`SignOut`、`SignOutSession`、`SignOutOthers` 與 `SignOutOthersPrompt`。

## 無障礙

- 每個開關與按鈕都有名稱。開關以其設定列的標題（「驗證器應用程式」）與說明作為名稱。工作階段的按鈕以其裝置命名，例如「登出 ThinkPad X1」，徽章會朗讀為「此裝置」。
- 安全檢查進度條的名稱為「已開啟 {n}/4 項防護措施」。安全檢查標題與密碼設定列的說明皆為即時區域（禮貌），因此內容變化時會朗讀「您的帳戶保護良好」與「剛剛更新」。
- 開啟表單時，焦點會移至第一個欄位。「更新密碼」可用時，按 Enter 送出，按 Escape 取消。關閉後，焦點回到「變更密碼」。遭拒絕的變更會將焦點移至顯示錯誤的欄位，並選取其中的文字。
- 聚焦的「登出」列離開時，焦點會移至下一個工作階段的按鈕，若沒有則移至上一個。
- 「登出其他登入工作階段」遵循 `ConfirmMorph`：先詢問，按 Escape 或「取消」即保留工作階段。沒有其他工作階段時，它會停用。
- Tab 停駐點會依卡片由上到下排列。沒有電話號碼時，簡訊開關會停用，該設定列會說明原因。

## 動態效果

- 工作階段列透過 `PresenceHost` 離開：工作階段移除前，它們會以平滑的彈簧淡出，同時高度收合。「登出其他登入工作階段」會讓每一列比上一列晚 35 毫秒開始。
- 密碼表單透過 `SpringSizeDecorator` 以彈簧方式展開與收合。密碼設定列的說明以模糊切換方式變化。「登出其他登入工作階段」會在原地變形為詢問與結果。
- 安全檢查標題會以模糊過渡到新文字。數字隨分數向上或向下滾動。進度條的分段與盾牌會漸變為新的顏色。
- 減少動態效果時保留短暫的淡入淡出，取消彈簧、模糊與滾動。關閉動態效果時，所有內容一次就位，已登出的列也會立即離開清單。

## 相關元件

- `SettingsGroup` 與 `SettingsRow` 用於建構卡片與設定列。其他設定區塊也會使用它們：設定頁面、設定命令面板、帳單概覽、用量計費與取消訂閱流程。
- 密碼表單使用 `PasswordField` 與 `PasswordStrength`，兩步驟驗證方式使用 `Switch`，目前的裝置使用 `Badge`，設定列的操作使用 `Button`，登出其他所有工作階段則使用 `ConfirmMorph`。
