---
title: 安全性設定
summary: 安全檢查、變更密碼、兩步驟驗證和登入工作階段，均可原地處理。
source-hash: 53dd97448a3c
---

<Demo name="Overview" />

## 何時使用

`SecuritySettings` 是帳號的安全性頁面。它位於產品的設定區域，彙集了使用者會檢查的四項內容：帳號的保護程度、密碼、兩步驟驗證方式，以及已登入的裝置。每項操作（變更密碼、開啟某種驗證方式、登出某台裝置）都在頁面上完成，因此使用者不必在多個畫面之間切換，就能修正安全檢查指出的問題。

- **SaaS 產品或銀行入口網站中的客戶帳號頁面。** 安全檢查會告訴使用者四項防護中開啟了幾項，以及接下來該新增什麼；密碼列則顯示密碼上次變更的時間。
- **內部工具中管理員自己的安全性設定**，管理員在管理他人帳號之前，先管理自己帳號的通行金鑰和復原碼。
- **開發者平台的帳號頁面**，列出持有 API 工作階段的裝置，開發者可以登出一台已經不再使用的筆記型電腦。
- **要求第二步驟驗證的金融應用程式**，可透過驗證器應用程式或簡訊完成驗證。開關與隱藏部分號碼的電話號碼，清楚顯示哪一種方式用於登入。
- **遺失手機後的復原流程**，使用者只需一次確認的操作，即可登出其他所有工作階段。

## 何時不用

- **登入或建立帳號。** 使用由 `Input` 和 `PasswordField` 組成的登入表單；本區塊假設使用者已經登入。
- **全工作區的安全性政策**，例如強制兩步驟驗證或允許的登入方式。這些屬於管理員設定：請在設定頁面中以 `SettingsGroup` 和 `SettingsRow` 列的形式撰寫。
- **其他表單中的單一密碼欄位**，例如結帳流程中的變更請求。直接使用 `PasswordField` 搭配 `PasswordStrength`。
- **檢視事件的稽核紀錄**（誰在何時、從何處登入，以及改動了什麼）。歷史紀錄請用 `Timeline`，本區塊只保留目前狀態及對其的操作。
- **從清單中選擇一項安全性設定**，例如工作階段逾時時間。在設定表單中使用 `Select`。

## 用法

設定帳號的狀態後，本區塊會以四張卡片呈現：安全檢查、密碼、兩步驟驗證和已登入的工作階段。所有操作都在原地進行。兩個開關會回寫 `IsAuthenticatorEnabled` 和 `IsTextMessageEnabled`（雙向），密碼表單成功時會回寫 `PasswordChangedAt` 和 `HasPassword`。"Add passkey"、"View codes" 或 "Create codes" 以及登出，都會以路由事件的形式拋給宿主處理。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
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

`SecuritySettings` 衍生自 Avalonia 的 `TemplatedControl`。其卡片是設定區塊中的 `SettingsGroup` 與 `SettingsRow`，開關是 Arc 的 `Switch`，密碼欄位是搭配 `PasswordStrength` 的 `PasswordField`，登出的確認則是 `ConfirmMorph`。

時間相對於 `Clock` 顯示，`Clock` 是一個預設使用系統時鐘的 `TimeProvider`。本區塊在顯示期間每 30 秒重新讀取一次，因此 "2 hours ago" 之類的文字會保持最新。測試中可以傳入固定的時鐘：

```csharp
sealed class FixedClock(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now.ToUniversalTime();
}

security.Clock = new FixedClock(new DateTimeOffset(2026, 10, 20, 12, 0, 0, TimeSpan.FromHours(2)));
```

### 結構

1. **安全檢查卡片**（`IsSummaryVisible`）：一個 44 px 的盾牌圖塊（22 px 圖示）、標題、數量列，以及右側的四段式進度條。每段 28 × 6 px，段與段之間相距 4 px，每項防護對應一段。
2. **密碼列**（`HasPassword`、`PasswordChangedAt`）：標題 "Password"、描述（"Last changed 3 months ago"、"Never changed" 或 "Updated just now"），以及一個按鈕："Change password"；沒有密碼時則為 "Set password"。
3. **密碼表單**：在該列下方展開的內嵌表單，寬度不超過 400 px，並與列內文字對齊。包含目前密碼、新密碼（附強度指示條與要求清單）、Cancel 以及 "Update password"。
4. **兩步驟驗證列**（`IsAuthenticatorEnabled`、`IsTextMessageEnabled`、`PhoneNumber`、`PasskeyCount`、`RecoveryCodesCreatedAt`）：驗證器應用程式開關、附隱藏號碼的簡訊開關、附 "Add passkey" 的通行金鑰列，以及附 "View codes" 或 "Create codes" 的復原碼列。
5. **工作階段卡片**（`Sessions`）：標題列包含 "Sign out other sessions" 的確認操作；每個工作階段一列，包括裝置圖示、"Device · Client"、"Location · 相對時間"，以及 "This device" 標記或 "Sign out" 按鈕之一。

### 安全檢查

安全檢查統計四項防護：密碼（`HasPassword`）、任一方式的兩步驟驗證、至少一把通行金鑰（`PasskeyCount`），以及復原碼（`RecoveryCodesCreatedAt`）。`ProtectionScore` 即為這個計數，取值 0 到 4，且為唯讀。每項防護會填滿各自的進度條段。盾牌採用分數對應的色調：4 分時為成功色，2 或 3 分時為強調色，0 或 1 分時為警告色。4 分時標題變為 "Your account is well protected"，否則顯示 "Add another safeguard"。

使用安全檢查是為了顯示還缺少什麼，而不是向使用者施壓。開啟某種驗證方式或新增通行金鑰會填滿進度條，因此頁面會隨使用者的操作說明自身的結果。

<Demo name="Checkup" />

`Sections` 用於選擇要顯示的卡片，可以組合 `Password, Sessions` 這樣的旗標。`IsSummaryVisible` 只隱藏安全檢查；當頁面已有自己的摘要時使用它，如下方的示範所示。

### 變更密碼

點選 "Change password" 會在該列下方開啟表單。表單要求輸入目前密碼與新密碼。"Update password" 需要兩者同時滿足：目前密碼不為空，且新密碼至少達到 fair 強度（即 `MinimumPasswordLevel` 常數，等級 2）。按鈕啟用時按 Enter 會送出表單，按 Escape 則取消。

送出時會引發 `PasswordChangeRequested`，並附帶 `CurrentPassword` 與 `NewPassword`。宿主核對密碼後再作出決定：

- **接受**：保持 `ErrorMessage` 為空。表單關閉，`PasswordChangedAt` 變為時鐘的目前時間，該列顯示 "Updated just now"。
- **拒絕**：設定 `ErrorMessage`。訊息顯示在目前密碼欄位下方，表單保持開啟，焦點回到該欄位。沒有密碼時，訊息顯示在新密碼欄位下方。

沒有密碼時（`HasPassword="False"`），該列提供 "Set password"，表單只要求輸入新密碼。變更成功後 `HasPassword` 會變為 true。`OpenPasswordForm()`、`ClosePasswordForm()` 與 `SubmitPasswordChange()` 可以在程式碼中完成同樣的操作。

<Demo name="ChangePassword" />

### 已登入的工作階段

工作階段依目前裝置優先排列，其後依最近活動排序。目前裝置帶有 "This device" 標記，且不能在此處登出。其他每個工作階段都有一個 "Sign out" 按鈕。點選後，該列會淡出並收合，在清單可修改時將該工作階段從 `Sessions` 中移除，並引發 `SessionSignedOut`。

"Sign out other sessions" 是一個 `ConfirmMorph`。只有在存在其他工作階段時才會啟用，並且會先詢問：問題中包含數量（"Sign out 3 sessions?"），按 Escape 或 Cancel 則保留所有工作階段。使用者確認後，各列會依序離開，彼此相隔 35 ms，`OtherSessionsSignedOut` 會攜帶被移除的工作階段。若清單為唯讀（例如陣列），則各列會被隱藏而非移除。

`SignOut(session)` 與 `SignOutOtherSessions()` 可以在程式碼中完成同樣的操作。

<Demo name="Sessions" />

在窄版頁面上，進度條會移到安全檢查文字下方，"Sign out other sessions" 則移到清單下方。帶有小型控制項（如標記或 "Sign out"）的列會把它保留在文字旁邊；帶有較寬按鈕的列則把按鈕堆疊在文字下方。

### 繫結到檢視模型

檢視模型持有安全性狀態及其規則。本區塊負責顯示狀態、引發請求，並套用檢視模型設定的結果。

```csharp
public partial class AccountSecurityViewModel : ObservableObject
{
    private readonly IAccountSecurity _security;

    public AccountSecurityViewModel(IAccountSecurity security)
    {
        _security = security;
    }

    [ObservableProperty]
    private bool _isAuthenticatorEnabled;

    [ObservableProperty]
    private DateTimeOffset? _passwordChangedAt;

    [ObservableProperty]
    private int _passkeyCount;

    [ObservableProperty]
    private DateTimeOffset? _recoveryCodesCreatedAt;

    public ObservableCollection<SecuritySession> Sessions { get; } = [];

    public async Task<string?> ChangePasswordAsync(string current, string next)
    {
        var result = await _security.ChangePasswordAsync(current, next);
        return result.IsSuccess ? null : result.Error;
    }
}
```

```xml
<arc:SecuritySettings IsAuthenticatorEnabled="{Binding IsAuthenticatorEnabled, Mode=TwoWay}"
                      PasswordChangedAt="{Binding PasswordChangedAt, Mode=TwoWay}"
                      Sessions="{Binding Sessions}"
                      PasskeyCount="{Binding PasskeyCount}"
                      RecoveryCodesCreatedAt="{Binding RecoveryCodesCreatedAt}" />
```

- `IsAuthenticatorEnabled`、`IsTextMessageEnabled`、`HasPassword` 與 `PasswordChangedAt` 是雙向的，因此檢視模型能收到開關或表單的變化。
- 變更密碼沒有命令。請處理 `PasswordChangeRequested`，呼叫 `ChangePasswordAsync`，並將回傳的訊息設為事件引數的 `ErrorMessage`。
- `Sessions` 是一個 `IList<SecuritySession>`，登出工作階段時本區塊會更新它。請將其繫結到 `ObservableCollection<SecuritySession>`，使清單與伺服器保持同步。
- `PasskeyCount` 與 `RecoveryCodesCreatedAt` 是單向的。在宿主完成 "Add passkey" 或 "Create codes" 之後再更新它們。

### 範例：銀行客戶的帳號安全

銀行入口網站向客戶展示其帳號安全，但不包含工作階段卡片（工作階段的示範已涵蓋該卡片）。客戶已設定驗證器應用程式和兩把通行金鑰，但尚未產生復原碼，因此安全檢查顯示 4 項中的 3 項，復原碼列提供 "Create codes"。此場景沒有展開的表單，所以截圖展示的是靜止狀態。

![帳號安全頁面：安全檢查為 4 項中的 3 項，帶兩步驟驗證方式和兩台已登入裝置](images/SecuritySettings.png)

```xml
<arc:SecuritySettings Width="640"
                      Sections="Summary, Password, TwoStep"
                      PasswordChangedAt="2026-07-14T09:00:00+02:00"
                      IsAuthenticatorEnabled="True"
                      IsTextMessageEnabled="False"
                      PhoneNumber="+31 6 2045 4821"
                      PasskeyCount="2" />
```

### 文案規範

本區塊的標籤簡短，是為帳號擁有者撰寫的。請使用句首大寫和預設文案所用的動詞，並在列內描述狀態，而不是使用提醒框。描述一種驗證方式時，應說明它為使用者做什麼（"驗證碼傳送至 •••• 4821"），而不是說明它的通訊協定名稱。

| 不要寫 | 改為 |
|---|---|
| 雙因素認證 | 兩步驟驗證 |
| 密碼已在 3 個月前變更 | 上次變更於 3 個月前 |
| 移除工作階段 | 每列的登出按鈕，以及清單的「登出其他工作階段」 |
| 你確定嗎？ | 登出 3 個工作階段？（數量寫在問題中） |
| 2FA：關閉 | 簡訊開關及其列說明 |
| 錯誤：密碼錯誤 | 這不是你目前的密碼。（原因，顯示在欄位下方） |

在地化時請透過 `SecuritySettingsLabels` 提供標籤。日期使用 `Culture` 屬性，因為相對時間與日期都依它而變。

### 最佳做法

**建議**

- 為每個帳號頁面開啟安全檢查，使進度條與下一步始終可見。只有當頁面已有自己的摘要時才隱藏它。
- 在顯示頁面之前，先從伺服器設定 `RecoveryCodesCreatedAt` 與 `PasskeyCount`。卡片的列與分數都依賴它們，過期的值會給出錯誤的安全檢查結果。
- 對伺服器拒絕的任何密碼設定 `ErrorMessage`，並用使用者能理解並據以行動的文字說明伺服器的原因。表單保持開啟，因此使用者無需重新輸入全部內容即可修正。
- 將 `Sessions` 繫結到伺服器的清單。只有清單可寫時，本區塊登出工作階段才會把它從清單中移除，因此伺服器呼叫應放在 `SessionSignedOut` 處理常式中。

**避免**

- 完整顯示電話號碼。本區塊只顯示最後四位數字，應用程式在頁面其他地方也不應顯示更多。
- 把工作階段卡片用於登入歷史。它列出的是目前開啟的工作階段，而非過去的事件。
- 為了修改單一詞而替換整個 `Labels` 物件。請建立新的 `SecuritySettingsLabels`，只設定需要修改的屬性，使其他文案保持預設值。

### 常見問題

**新密碼看起來沒問題，為什麼 "Update password" 仍是停用的？** 按鈕需要同時滿足兩個條件：目前密碼不為空，且新密碼至少達到 fair 強度（等級 2）。

**如何處理伺服器拒絕的登出？** 該列會在 `SessionSignedOut` 引發之前離開，因此宿主無法阻止它。伺服器拒絕時，請把該工作階段重新加回 `Sessions`，並顯示你自己的提示。

**為什麼簡訊開關保持關閉？** 在 `PhoneNumber` 有值之前它都是停用的，其列會顯示 "Add a phone number first"。設定號碼即可啟用。

**能否只在自己的頁面上顯示工作階段？** 可以。設定 `Sections="Sessions"`，或組合 `Password, Sessions` 這樣的旗標。未選取的卡片根本不會顯示。

## API 參考

<!-- api: SecuritySettings -->

**SecuritySession**（一般物件，可以在 XAML 中撰寫）

| 屬性 | 類型 | 說明 |
|---|---|---|
| `Device` | `string` | 裝置名稱，例如 "MacBook Air"。 |
| `Client` | `string?` | 瀏覽器或應用程式，顯示在裝置名稱之後。 |
| `Location` | `string?` | 裝置登入的位置，例如 "Rotterdam, NL"。 |
| `LastActive` | `DateTimeOffset` | 工作階段最後一次使用的時間；相對於 `Clock` 顯示。 |
| `Kind` | `SecurityDeviceKind` | `Desktop`、`Laptop`、`Phone`、`Tablet` 或 `Other`；決定使用的圖示。 |
| `IsCurrent` | `bool` | 正在使用的裝置：排在最前，標示 "This device"，且不能在此處登出。 |

**事件引數**

| 類型 | 屬性 | 說明 |
|---|---|---|
| `SecurityPasswordChangeEventArgs` | `CurrentPassword` | 輸入的目前密碼（沒有密碼時為空）。 |
| | `NewPassword` | 新密碼。 |
| | `ErrorMessage` | 設定後拒絕變更；表單保持開啟並顯示該訊息。 |
| `SecuritySessionEventArgs` | `Session` | 被登出的工作階段。 |
| `SecuritySessionsEventArgs` | `Sessions` | 透過 "Sign out other sessions" 登出的工作階段，依清單順序排列。 |

**SecuritySettingsSections**（旗標）：`Summary`、`Password`、`TwoStep`、`Sessions`、`All`（預設）、`None`。

**SecuritySettingsLabels**：`Culture` 以及每個內建文字對應的一個屬性，例如 `SummaryProtected`、`SummaryCount`、`PasswordChanged`、`ChangePassword`、`UpdatePassword`、`TextMessageDescription`、`PasskeyCount`、`SessionsHeader`、`SignOut`、`SignOutSession`、`SignOutOthers` 與 `SignOutOthersPrompt`。

## 無障礙

- 每個開關與按鈕都有名稱。開關以其列的標題（"Authenticator app"）與描述命名。工作階段的按鈕以其裝置命名，例如 "Sign out ThinkPad X1"，標記讀作 "This device"。
- 安全檢查進度條命名為 "{n} of 4 safeguards on"。安全檢查標題與密碼列的描述是禮貌式即時區域，因此 "Your account is well protected" 與 "Updated just now" 在變化時會被播報。
- 開啟表單時焦點移到第一個欄位。"Update password" 啟用時按 Enter 送出，按 Escape 取消。關閉後焦點回到 "Change password"。被拒絕的變更會讓焦點移到帶有錯誤的欄位。
- 獲得焦點的 "Sign out" 列離開時，焦點移到下一個工作階段的按鈕，若沒有則移到上一個。
- "Sign out other sessions" 遵循 `ConfirmMorph`：它會先詢問，按 Escape 或 Cancel 則保留工作階段。沒有其他工作階段時，該按鈕會被停用。
- Tab 順序從上到下依序經過各卡片。沒有電話號碼時，簡訊開關會被停用，且其列會說明原因。

### 鍵盤

| 按鍵 | 位置 | 操作 |
|---|---|---|
| `Tab` / `Shift+Tab` | 任何位置 | 從上到下依序經過各卡片與列 |
| `Enter` | "Change password" 或 "Set password" | 開啟密碼表單；焦點移到目前密碼欄位 |
| `Enter` | 密碼表單 | "Update password" 啟用時送出 |
| `Escape` | 密碼表單 | 取消：不儲存並關閉表單，焦點回到 "Change password" |
| `Escape` 或 Cancel | "Sign out other sessions" 的詢問 | 保留所有工作階段 |

## 動態效果

- 工作階段列以平滑彈簧淡出並收合高度，之後才移除該工作階段。"Sign out other sessions" 讓每列比上一列晚 35 ms 開始離開。
- 密碼表單在其列下方以彈簧展開與收合。密碼列的描述以模糊切換變化。"Sign out other sessions" 在原地變形為其詢問與結果。
- 安全檢查標題以模糊過渡到新文字。數量隨分數向上或向下滾動。進度條段與盾牌淡入其新顏色。
- 減少動態效果時保留短促的淡入淡出，去掉彈簧、模糊與滾動。關閉動態效果時所有變化立即完成，已登出的列也立即離開清單。

| 變化 | 完整 | 減少 | 關閉 |
|---|---|---|---|
| 工作階段列離開 | 以平滑彈簧淡出並收合 | 淡出 | 立即離開 |
| 密碼表單展開或收合 | 以彈簧展開或收合 | 以短促淡入淡出顯示或隱藏 | 立即完成 |
| 密碼描述變化 | 模糊切換到新文字 | 短促淡入淡出 | 立即完成 |
| 安全檢查數量與進度條 | 數字滾動；段落淡入其顏色 | 顏色淡入 | 立即完成 |

## 相關元件

- `SettingsGroup` 與 `SettingsRow` 用於建構卡片與列。其他設定區塊也使用它們：設定頁面、設定命令、帳單概覽、用量帳單與取消流程。
- `PasswordField` 與 `PasswordStrength` 用於密碼表單，`Switch` 用於兩步驟驗證方式，`Badge` 用於標示目前裝置，`Button` 用於列內操作，`ConfirmMorph` 用於登出其他所有工作階段。

| 元件 | 適用情境 |
|---|---|
| `SettingsPage` | 安全卡片位於帶導覽清單的設定頁面中 |
| `PasswordField` / `PasswordStrength` | 你只需要一個密碼欄位，或帶規則的密碼，且不在本頁面中 |
| `Switch` | 帶標籤與說明的單一開或關設定 |
| `ConfirmMorph` | 單一破壞性操作需要就地確認 |
| `Timeline` | 使用者需要帳號事件的歷史，而非目前狀態 |
