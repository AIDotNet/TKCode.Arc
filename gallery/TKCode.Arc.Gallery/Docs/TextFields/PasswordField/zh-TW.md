---
title: 密碼欄位
summary: 附有可見顯示切換控制項的敏感文字輸入。
source-hash: 1dbe8b8e16ad
---

<Demo name="SignIn" />

## 何時使用

`PasswordField` 用於保存一個已經存在、由使用者輸入或貼上的機密值：登入時的密碼、確認敏感變更的目前密碼，或管理員在後台頁面中輸入的密鑰。它是預設遮蔽值、外框內帶顯示切換按鈕的 `Input`，因此標籤、說明文字列、錯誤列和尺寸都與表單中的其他欄位完全一致。

- **SaaS 或帳單入口的登入**：卡片中堆疊一個電子郵件欄位和一個密碼欄位，加上一個「登入」按鈕。切換按鈕讓使用者在送出前核對在手機鍵盤上輸入的長密語。
- **敏感變更前的重新驗證**：Acme Billing 頁面上的「目前密碼」，用於修改收款帳戶或綁定的信用卡。欄位在變更生效前要求輸入機密，切換按鈕可以協助使用者避免因打錯字而被鎖在變更之外。
- **管理後台中的維運憑證**：在設定頁輸入的資料庫或 SMTP 密碼。畫面上的值是遮蔽的，但維運人員可以顯示它，確認貼上的內容正確。
- **多步驟流程中確認既有機密**：重複使用者已設定密碼的第二個欄位，例如設定頁儲存密碼變更時的「確認密碼」。
- **在共用或錄製的螢幕上輸入機密**：預設遮蔽讓值在使用者顯示之前不會出現在螢幕上，這對自助終端機、示範和支援工作階段很重要。

## 何時不用

- **需要設定並符合規則的新密碼**：用 `PasswordStrength`，它會顯示密碼強度以及符合了哪些規則。一般的 `PasswordField` 不會對值的品質提供任何回饋。
- **非機密文字**：用 `Input`。用於使用者名稱或帳號的遮蔽欄位會讓人盲打。
- **需要複製出去的機密**：產生的 API 密鑰、復原碼，或使用者要複製到其他系統的權杖。用 `Input` 搭配 `IsReadOnly` 和 `CopyButton`，因為遮蔽的值無法讀回用於複製。
- **搜尋查詢**：用 `SearchField`。它有清除控制項，並在按下 Enter 時送出；遮蔽的搜尋框會隱藏使用者正在尋找的內容。
- **驗證器應用程式產生的一次性驗證碼**：用 `Input`。驗證碼很短，依原樣顯示，且常被貼上，圓點和顯示切換只會拖慢使用者。

## 用法

欄位內的眼睛按鈕可以顯示或隱藏文字。它的名稱會被輔助技術讀取。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:PasswordField Label="密碼"
                       Placeholder="請輸入密碼"
                       Description="點選顯示按鈕可核對你輸入的內容。"
                       Width="360"
                       HorizontalAlignment="Left" />
</UserControl>
```

`PasswordField` 與 `Input` 衍生自同一個共用欄位基底類別，該基底類別衍生自 Avalonia 的 `TextBox`。文字方塊提供值、游標、選取、輸入法組字、復原和剪貼簿快速鍵；共用基底提供標籤、外框、說明文字列和錯誤列、尺寸以及無效狀態。`PasswordField` 另外加上遮蔽（預設使用 `•` 作為密碼字元，來自文字方塊的 `PasswordChar`）、顯示切換按鈕，以及切換按鈕內的眼睛圖示。兩者共有的部分（如驗證時機和訊息列）請參閱 `Input` 頁面。

### 結構

1. **標籤**（`Label`）：14 px、中等字重，位於外框上方。它同時是欄位的無障礙名稱。`IsLabelVisible` 可以在視覺上隱藏它，名稱仍然保留。
2. **外框**：1 px 邊框，帶控制項圓角，表面色背景。懸停與聚焦只加深邊框；出現錯誤時邊框變為危險色。
3. **前綴**（`Prefix`）：文字前的可選內容。顯示切換按鈕佔據外框的後綴位置，因此繼承的 `Suffix` 屬性在密碼欄位上不會顯示。
4. **遮蔽文字**：輸入的值以 `PasswordChar`（`•`）顯示。當 `IsRevealed` 為 `True` 時，同一段文字顯示為字元。值為空時，`Placeholder` 以弱化文字顏色顯示。
5. **顯示切換**：中號和大號下為 30 px 的圓形按鈕，小號為 26 px；中號下距上、下和末端邊緣各 7 px。它包含一個 18 px 的帶斜線眼睛圖示。懸停時填入弱化表面色，鍵盤聚焦時顯示 2 px 焦點環。
6. **說明文字列**（`Description`）：外框下方 12 px 的輔助文案。
7. **錯誤列**（`ErrorMessage`）：12 px、危險色文案。只要它有內容，欄位即處於無效狀態。

外框內距隨尺寸變化：中號時文字前 12 px、切換按鈕後 6 px；小號為 10 px 和 4 px；大號為 14 px 和 9 px。中號和小號的文字使用內文字級（14 px），大號使用基礎字級。

### 繫結值

`Text` 是雙向屬性，可以繫結到檢視模型。使用 `IsRevealed` 讀取或設定可見狀態。

```xml
<arc:PasswordField Label="密碼"
                   Text="{Binding Password}"
                   ErrorMessage="{Binding PasswordError}" />
```

```csharp
signIn.PropertyChanged += (_, e) =>
{
    if (e.Property == PasswordField.IsRevealedProperty)
    {
        state.Text = signIn.IsRevealed ? "已顯示" : "已隱藏";
    }
};
```

`IsRevealed` 同樣是雙向的。從程式碼設定它會顯示或遮蔽值，切換按鈕隨之變化。欄位也會讓文字方塊本身的 `RevealPassword` 與 `IsRevealed` 保持同步，因此讀取 `RevealPassword` 的程式碼會看到相同的狀態。

### 繫結到檢視模型

登入表單把密碼與其他欄位一起保存在檢視模型中，並透過命令送出。不要把密碼寫入記錄檔、分析事件或任何持久化狀態；檢視模型只在請求期間持有它。

```csharp
public partial class SignInViewModel : ObservableObject
{
    private readonly IAuthService _auth;

    public SignInViewModel(IAuthService auth)
    {
        _auth = auth;
    }

    [ObservableProperty]
    private string _email = string.Empty;

    [ObservableProperty]
    private string _password = string.Empty;

    [ObservableProperty]
    private string? _passwordError;

    [RelayCommand]
    private async Task SignInAsync()
    {
        PasswordError = null;
        if (string.IsNullOrEmpty(Password))
        {
            PasswordError = "請輸入密碼。";
            return;
        }

        var result = await _auth.SignInAsync(Email, Password);
        Password = string.Empty;
        if (!result.IsSuccess)
        {
            PasswordError = "電子郵件或密碼不正確。";
        }
    }
}
```

```xml
<StackPanel Width="360" Spacing="16">
    <arc:Input Label="工作電子郵件" Text="{Binding Email}" />
    <arc:PasswordField Label="密碼"
                       Text="{Binding Password}"
                       ErrorMessage="{Binding PasswordError}" />
    <arc:Button Content="登入" Command="{Binding SignInCommand}" IsDefault="True" />
</StackPanel>
```

- `Text` 和 `IsRevealed` 是雙向的。`ErrorMessage` 是單向的：錯誤訊息由檢視模型持有，並在使用者編輯欄位或下一次送出開始時清除。
- 登入按鈕上的 `IsDefault="True"` 讓任一欄位中按下 Enter 都會送出表單。欄位本身不處理 Enter。
- 檢視模型在請求結束後清空 `Password`，無論成功與否，使機密在記憶體中停留的時間不超過請求所需。

### 驗證

登入只驗證是否已填寫以及請求的結果，而不驗證密碼的格式。一個在建立時有效的密碼，可能比今天的規則更短；登入時加上格式規則，會把使用舊密碼的使用者鎖在門外。

| 規則 | 何時檢查 | 顯示在哪裡 |
|---|---|---|
| 必填 | 送出時 | `ErrorMessage`：「請輸入密碼。」，並把焦點移到該欄位 |
| 電子郵件或密碼錯誤 | 伺服器拒絕請求後 | `ErrorMessage`，兩種情況使用同一句通用文案 |
| 帳號被鎖定或請求過於頻繁 | 伺服器回應後 | `ErrorMessage`，附上下一步提示（「請 15 分鐘後再試。」） |
| 長度或字元規則 | 僅在註冊或變更密碼時（使用 `PasswordStrength`） | 登入時不檢查 |
| 與確認欄位一致 | 確認欄位失去焦點時，或送出時 | 確認欄位上的 `ErrorMessage` |

使用者編輯欄位時清除 `ErrorMessage`，使訊息不會比它所描述的值存在得更久。不要說明是電子郵件還是密碼錯誤：通用訊息可以避免向攻擊者透露哪些帳號存在。

### 確認

當兩個值不同時，從第二個欄位設定 `ErrorMessage`。將其設為 `null` 即可清除。

<Demo name="Confirm" />

範例在每次按鍵時檢查是否一致，因此使用者還在輸入第二個值時錯誤就會出現。在應用程式中，應在確認欄位失去焦點或表單送出時檢查一致性，並在兩個值相等時立即清除訊息。這樣訊息會在使用者輸完一個值之後才出現，而不是逐字元出現。

### 切換按鈕名稱

切換按鈕的名稱來自 `ShowPasswordText` 與 `HidePasswordText`。設定它們即可翻譯文案。

```xml
<arc:PasswordField Label="Mot de passe"
                   ShowPasswordText="Afficher"
                   HidePasswordText="Masquer" />
```

切換按鈕預設的名稱為「顯示密碼」或「隱藏密碼」，並隨 `IsRevealed` 切換。請與標籤一起翻譯這兩個名稱，使螢幕閱讀器使用者在整個表單中聽到同一種語言。

### 尺寸與狀態

`Size` 決定外框高度：`Small` 為 36，`Medium`（預設值）為 44，`Large`（50）適合單一醒目的欄位。

<Demo name="SizesAndStates" />

與 `Input` 一樣，同一個表單只使用一種尺寸。`Small` 適合密集的管理頁面，此時切換按鈕也會縮小（26 px），以維持列的比例。停用與無效狀態與 `Input` 相同：停用的欄位會淡化並離開 Tab 順序，無效的欄位顯示危險色邊框和錯誤列。

### 範例：登入卡片

帳單入口的登入卡片。卡片標明產品名稱，電子郵件欄位是一般的 `Input`，密碼欄位顯示登入失敗時產生的錯誤：一句不說明哪一部分有誤的通用文案。該值處於遮蔽的預設狀態，因此圖片展示的是使用者在顯示任何內容之前看到的樣子。按鈕是表單的預設按鈕，因此在密碼欄位中按下 Enter 即可送出表單。

![帶電子郵件欄位和遮蔽密碼欄位並顯示通用錯誤的登入卡片](images/SignInCard.png)

```xml
<Border Width="560"
        Padding="32"
        CornerRadius="16"
        BorderThickness="1"
        BorderBrush="{arc:ArcDesignTokenResource Border}"
        Background="{arc:ArcDesignTokenResource Surface}">
    <StackPanel Width="360"
                Spacing="18"
                HorizontalAlignment="Center">
        <StackPanel Spacing="4">
            <TextBlock Text="Sign in to Acme Billing"
                       FontSize="20"
                       FontWeight="SemiBold"
                       Foreground="{arc:ArcDesignTokenResource Foreground}" />
            <TextBlock Text="Use your work email and password."
                       FontSize="14"
                       TextWrapping="Wrap"
                       Foreground="{arc:ArcDesignTokenResource TextSecondary}" />
        </StackPanel>
        <arc:Input Label="Work email"
                   Text="ada@acme.com" />
        <arc:PasswordField Label="Password"
                           Text="correct-horse-battery"
                           ErrorMessage="The email or password is incorrect." />
        <arc:Button Content="Sign in"
                    IsDefault="True"
                    HorizontalAlignment="Stretch" />
        <TextBlock Text="Forgot your password?"
                   FontSize="14"
                   HorizontalAlignment="Center"
                   Foreground="{arc:ArcDesignTokenResource TextSecondary}" />
    </StackPanel>
</Border>
```

### 文案規範

- **標籤**：表示機密的名詞，使用句子式大小寫：「密碼」「目前密碼」「確認密碼」。不要把「請輸入密碼」用作標籤；那是預留位置文字或錯誤提示的內容。
- **預留位置文字**：可選。僅在欄位有簡短的格式提示時使用（「至少 12 個字元」應放在 `PasswordStrength` 上，而不是這裡）。不要重複標籤。
- **說明文字**：一句話，解釋後果或協助使用者核對輸入（「點選顯示按鈕可核對你輸入的內容。」）。
- **錯誤提示**：一句話，用詞與標籤保持一致。對於登入，一句通用文案即可涵蓋電子郵件錯誤或密碼錯誤。
- **切換按鈕名稱**：預設的「顯示密碼」與「隱藏密碼」已經足夠清楚。請與表單的其他內容一起翻譯。

| 不要寫 | 改為 |
|---|---|
| 請輸入你的密碼！！！ | 請輸入密碼。 |
| 憑證無效 | 電子郵件或密碼不正確。 |
| 密碼錯誤 | 電子郵件或密碼不正確。 |
| 重新輸入密碼 | 確認密碼 |
| 你的密碼不能太弱 | 至少使用 12 個字元，並包含一個數字和一個符號。 |

### 最佳做法

**建議**

- 預設保持值被遮蔽，讓使用者透過切換按鈕自行顯示。預設遮蔽可以在共用螢幕和螢幕錄影中保護該值。
- 設定新密碼的頁面使用 `PasswordStrength`，使用既有密碼的頁面使用 `PasswordField`，讓規則只出現在使用者正在選擇值的地方。
- 登入失敗時只顯示一句通用訊息，使表單不會暴露哪些帳號存在。
- 請求結束後從檢視模型中清除密碼，並確保它不會進入記錄檔和分析資料。
- 標籤、說明文字和錯誤文案使用同一種語言，並同步設定 `ShowPasswordText` 與 `HidePasswordText`。

**避免**

- 把 `PasswordField` 用於必須複製出去的值，例如產生的密鑰。遮蔽的值無法可靠地讀回用於複製；請改用 `Input` 搭配 `IsReadOnly`。
- 登入時驗證密碼的長度或字元類別。舊密碼可能不符合新規則，使用者會因為一條在設定密碼時並不適用的規則而被鎖定。
- 因為使用者覺得圓點難以辨認就把遮蔽預設關閉。應提供切換按鈕，並保留預設值。
- 使用看起來像真實值的預留位置文字，例如「••••••••」。使用者可能會把它誤認為已儲存的密碼。
- 把欄位放在密集的工具列或頁首中。密碼是表單值；搜尋或篩選欄位才應放在那裡。

### 常見問題

**如何更改遮蔽字元？** `PasswordChar`（繼承自文字方塊）用於設定字元，預設為 `•`。只有當平台或品牌需要不同符號時才更改；眼睛切換的運作方式不受影響。

**如何知道值是否已顯示？** 讀取 `IsRevealed`，或像範例那樣監聽 `PasswordField.IsRevealedProperty` 的變化。從程式碼設定 `IsRevealed` 會顯示或隱藏值，並移動切換按鈕。

**能否只翻譯切換按鈕名稱而不改變標籤？** 可以。在每個欄位上設定 `ShowPasswordText` 和 `HidePasswordText`。標籤和訊息是獨立的屬性，因此翻譯它們互不影響。

**新密碼的長度規則應放在哪裡？** 放在設定密碼的頁面上，使用 `PasswordStrength`，它會在使用者輸入時顯示強度。`PasswordField` 只用於使用既有密碼的頁面。

## API 參考

<!-- api: PasswordField -->

## 無障礙

- `Label` 為欄位命名。`Description` 與 `ErrorMessage` 會合併為它的說明文字，因此螢幕閱讀器先讀名稱，再讀取值，最後讀說明資訊。
- 顯示切換按鈕是帶按下狀態的切換按鈕。它的名稱會在「顯示密碼」與「隱藏密碼」之間切換，並透過 `IsOn` 回報按下狀態。
- 密碼預設會被遮蔽。游標、選取、輸入法、復原與剪貼簿快速鍵都是原生 `TextBox` 的行為。
- 顯示錯誤列時，錯誤會作為欄位的錯誤被播報，同時欄位被標記為無效。
- 顯示值時，切換按鈕保持完整對比度。

### 鍵盤

| 按鍵 | 操作 |
|---|---|
| `Tab` / `Shift+Tab` | 在欄位、其顯示切換按鈕和下一個控制項之間移動 |
| 切換按鈕上的 `Enter` 或 `Space` | 顯示密碼；若已顯示則隱藏 |
| `Left` / `Right`、`Home` / `End` | 依字元移動游標，或移到開頭、結尾 |
| `Shift` + 以上任一按鍵 | 擴充選取範圍 |
| `Ctrl+A`、`Ctrl+C`、`Ctrl+X`、`Ctrl+V`（macOS 上為 `Cmd`） | 全選、複製、剪下、貼上 |
| `Ctrl+Z` / `Ctrl+Y`（macOS 上為 `Cmd+Z` / `Cmd+Shift+Z`） | 復原、重做 |
| 欄位中的 `Enter` | 欄位本身不處理；與 `Input` 一樣，由視窗中的預設按鈕接收 |

## 動態效果

- 狀態變化時，眼睛上的斜線沿圖示繪製出來；眼睛圖示從不被替換為第二個圖示。
- 在圓點與字元之間切換時，文字在原位完成過渡：起始為 35% 不透明度並帶輕微模糊，然後變為完全不透明且清晰對焦。
- 按下切換按鈕時它會輕微縮小。說明列與錯誤列的表現與 `Input` 相同。
- 減少動態效果時，斜線立即繪製完成，並移除模糊與按下縮放。關閉動態效果時同樣如此。

| 變化 | 完整 | 減少 | 關閉 |
|---|---|---|---|
| 顯示或遮蔽 | 斜線以標準補間動畫劃過眼睛，其下方的輪廓被切開，斜線離開時輪廓恢復 | 斜線立即繪製或移除 | 立即完成 |
| 圓點與字元切換 | 文字從 35% 不透明度與 2 px 模糊過渡到清晰 | 無過渡效果 | 無過渡效果 |
| 切換按鈕按下 | 按住時縮放至 96% | 無縮放 | 無縮放 |
| 說明列與錯誤列 | 與 `Input` 相同：該列以平滑彈簧展開，變化的詞向上浮現 | 與 `Input` 相同 | 與 `Input` 相同 |

## 相關元件

- `PasswordStrength`：用於設定新密碼並顯示其強度。
- `Input`：非機密文字。
- `SearchField`：附有清除控制項的查詢。

| 元件 | 適用情境 |
|---|---|
| `PasswordStrength` | 使用者設定新密碼，需要查看其強度和規則 |
| `Input` | 值不是機密，或機密需要讀回並複製 |
| `SearchField` | 文字是用於篩選頁面內容的查詢 |
| `CopyButton` | 顯示唯讀的產生值，並由使用者複製 |
| `Textarea` | 值是較長的文字，而不是簡短的機密 |
