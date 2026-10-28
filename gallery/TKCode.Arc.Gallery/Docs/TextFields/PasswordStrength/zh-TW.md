---
title: 密碼強度
summary: 在輸入新密碼時顯示其強度。
source-hash: 1b9e076740df
---

<Demo name="Create" />

## 何時使用

`PasswordStrength` 是用於讓使用者設定新密碼的欄位：SaaS 產品的註冊步驟、帳號設定中的變更密碼頁面，或從電子郵件連結開啟的重設表單。它是一個遮罩的密碼欄位，附帶四段式強度計、強度文字和規則清單，因此使用者在輸入時就能看到還缺什麼，而不是在第一次送出失敗之後才知道。

- **工作區或客戶入口網站註冊**：新的擁有者為負責帳單和發票的帳號選擇密碼。強度計和規則清單在第一次送出之前就說明了政策，從而減少被拒絕的表單。
- **在帳號設定中變更密碼**：使用者已經知道目前的密碼，因此只有新密碼處才需要規則。目前的密碼保留在一般的 `PasswordField` 中，這裡則展示規則。
- **透過電子郵件連結重設密碼**：使用者到達頁面時沒有上下文，規則清單是頁面上唯一的指引。與註冊表單使用同一套規則清單，使政策在各處的表述保持一致。
- **管理員設定臨時密碼**：建立團隊成員帳號的管理主控台，可以在臨時密碼儲存到目錄之前，顯示它是否符合政策。
- **規則更嚴格的內部工具**：要求 16 個字元的金融後台，可以傳入自己的 `Rules` 和 `ShortLength`，使規則清單與伺服器執行的規則一致。

## 何時不用

- **登入既有帳號**：用 `PasswordField`。使用者已經有密碼，強度計和規則清單只會增加干擾，還會把政策暴露給正在猜測密碼的人。
- **並非密碼的測量量**，例如匯出或備份的進度：用 `Progress`。
- **非機密文字**，例如工作區名稱或金鑰標籤：用 `Input`。強度計對不是密碼的值沒有意義。
- **系統產生的機密**，例如只顯示一次的 API 權杖：用唯讀的 `Input` 加上複製操作來顯示。使用者從不選擇這個值，因此無從評分。
- **需要外洩或字典檢查的政策**：內建估算器只計算規則，並在裝置上執行。可以透過 `Scorer` 傳入 `IPasswordStrengthScorer` 進行本機檢查，或在伺服器端執行檢查，並用 `ErrorMessage` 顯示其結果。

## 用法

強度計、強度文字和規則清單會在每次按鍵時更新。評分在裝置上以預設規則進行：至少 12 個字元，包含大寫與小寫字母、數字和符號。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:PasswordStrength Label="新密碼"
                          Placeholder="請設定密碼"
                          Width="400"
                          HorizontalAlignment="Left" />
</UserControl>
```

`PasswordStrength` 衍生自 `PasswordField`，而 `PasswordField` 衍生自基於 Avalonia `TextBox` 的 Arc 欄位基底類別。它從文字欄位繼承了遮罩文字、顯示切換（`IsRevealed`）、`Label`、`Description`、`ErrorMessage`、`Size` 和 Arc 外框，並在欄位下方加入強度計、強度文字和規則清單。游標、選取、復原、輸入法組字和剪貼簿快速鍵都是原生 `TextBox` 的行為。

### 結構

1. **標籤**（`Label`）：14 px、中等字重，位於外框上方 8 px，與 `Input` 相同。它是欄位的無障礙名稱。只有當可見的標題已經為欄位命名時，才用 `IsLabelVisible="False"` 隱藏它。
2. **外框**：Arc 外框，左側內距 16 px，右側 5 px。前綴、文字和後綴之間相距 4 px。
3. **文字與預留位置文字**：遮罩的密碼。欄位為空時，`Placeholder` 以淡化的文字顏色顯示。
4. **顯示切換按鈕**：位於外框末端的 34 px 方形按鈕，圓角 13 px，內含 18 px 的眼睛圖示。按下時縮放至 94%；顯示密碼時，它會回報其開啟狀態。
5. **說明列與錯誤列**（`Description`、`ErrorMessage`）：與 `Input` 共用的輔助列和錯誤列，位於外框下方，12 px 文案。錯誤列會整則替換其訊息。
6. **強度計列**：位於欄位或其錯誤列下方 12 px。四段等寬的 4 px 高色段，間隔 5 px，底色為邊框顏色。
7. **強度文字**：強度計列末端至少 72 px 的區域，隨本地化文字增寬，與強度計相距 12 px，靠右對齊，使用小號內文的中等字重。欄位為空時它也會是空的。
8. **規則清單**（`Rules`）：位於強度計下方 16 px，規則之間間隔 6 px。每列有一個 16 px 的圓圈，規則符合時圓圈帶淡淡的成功色並顯示勾號，之後是規則文字。未符合且帶有 `Remaining` 函式的規則，還會以淡化顏色顯示滾動計數，例如「還差 4 個」。

### 強度等級

等級由符合規則的比例決定，分為四個檔位。在 `ShortLength` 個字元以下，無論規則如何，等級都固定為 1，因此過短的密碼永遠不會顯得很強。

| 等級 | 強度文字 | 強度計 | 色調 |
|---|---|---|---|
| 0 | 無（欄位為空） | 無色段；狀態顯示「尚未輸入密碼」 | 底色與空色段 |
| 1 | `弱`，或在 `ShortLength` 以下時為 `太短` | 1 段 | 危險色 |
| 2 | `尚可` | 2 段 | 警告色 |
| 3 | `良好` | 3 段 | 成功色 |
| 4 | `很強` | 4 段 | 成功色 |

使用預設的四條規則時，符合一條得等級 1，符合兩條得等級 2，符合三條得等級 3，全部符合得等級 4。若自訂規則為五條，則 1 對應 1、2 對應 2、3 對應 2、4 對應 3、5 對應 4，因為比例會四捨五入到最接近的檔位。`良好` 與 `很強` 使用同一種成功色，因此強度計由紅變黃再變綠，不會用顏色區分最上面兩個檔位。

當政策的最小長度高於預設值 8 時，將 `ShortLength` 設為該最小長度。這樣，長度介於 8 個字元與政策最小值之間的密碼會顯示 `弱`，且長度規則仍未符合，這不如 `太短` 清楚。

### 繫結值

`Text` 是雙向屬性。將它繫結到檢視模型中的字串，並把同一個檢視模型中的錯誤繫結到 `ErrorMessage`。當使用者變更密碼時，檢視模型會清除該錯誤，以免舊的伺服器訊息一直留在螢幕上。

```csharp
public partial class PasswordChangeViewModel : ObservableObject
{
    // 儲存成功時回傳 null，否則回傳要在欄位上顯示的訊息。
    private readonly Func<string, Task<string?>> _changePassword;

    public PasswordChangeViewModel(Func<string, Task<string?>> changePassword)
    {
        _changePassword = changePassword;
    }

    [ObservableProperty]
    private string _newPassword = string.Empty;

    [ObservableProperty]
    private string? _newPasswordError;

    partial void OnNewPasswordChanged(string value) => NewPasswordError = null;

    [RelayCommand]
    private async Task SetPasswordAsync()
    {
        NewPasswordError = await _changePassword(NewPassword);
    }
}
```

```xml
<StackPanel Spacing="16">
    <arc:PasswordStrength Label="新密碼"
                          Text="{Binding NewPassword, Mode=TwoWay}"
                          ErrorMessage="{Binding NewPasswordError}"
                          Width="400"
                          HorizontalAlignment="Left" />
    <arc:Button Content="更新密碼"
                Command="{Binding SetPasswordCommand}"
                IsDefault="True"
                HorizontalAlignment="Left" />
</StackPanel>
```

`Text` 預設為雙向。`IsRevealed` 只有在繫結中指定 `Mode=TwoWay` 時才是雙向的，這樣檢視模型可以記住使用者是否選擇顯示密碼。唯讀的值 `Level`、`StrengthLabel` 和 `Score` 是單向的：應在程式碼中讀取，而不要放在會寫回的繫結中。`StrengthChanged` 是唯一的事件；此控制項沒有命令。

### 回應強度變化

每次有評分的變更之後都會觸發 `StrengthChanged`。`Level` 的範圍從 0（空白）到 4（很強），`Label` 為強度文字，`Score.MetRuleIds` 列出已符合的規則。只有當等級、短密碼狀態或符合的規則集合發生變化時，事件才會觸發。在一個已符合大小寫規則的密碼後面再輸入一個小寫字母，不會觸發任何事件；如果需要每次按鍵的結果，請在自己的處理常式中讀取 `Score` 或 `Level`。

```csharp
create.StrengthChanged += (_, e) =>
{
    result.Text = e.Level == 0
        ? "強度：尚無輸入"
        : $"強度：{e.Label}（等級 {e.Level}，已符合 {e.Score.MetRuleIds.Count} 條規則）";
};
```

### 自訂規則

將規則清單傳給 `Rules`。每個 `PasswordRule` 都有一個 id（會在 `Score.MetRuleIds` 中回報）、清單中顯示的文字、一個判定函式，以及可選的 `Remaining` 函式。列的順序與清單順序一致。指派 `Rules` 會重建清單並重新評分，因此應在頁面載入時指派一次，而不是每次按鍵都指派。

`PasswordRules.Create` 會以你自己的最小長度、文字和字元計數來建立內建規則。當政策只改變數字時使用它；當某條規則性質不同時，則自己撰寫 `PasswordRule`。

<Demo name="CustomRules" />

```csharp
var rules = new List<PasswordRule>
{
    new("length", "至少 10 個字元", p => p.Length >= 10),
    new("digit", "包含數字", p => p.Any(char.IsDigit))
};
```

`remaining` 參數是可選的。不傳入時，未符合的規則只顯示其文字；傳入後，只要函式回傳值大於 0，該列就會顯示滾動計數。內建的長度規則使用了它，因此在密碼足夠長之前，清單會顯示「還差 4 個」。

### 驗證

強度計和規則清單在使用者輸入時提供指引，它們從不阻擋輸入，也不是錯誤。決定密碼能否被接受的政策檢查應放在送出時進行，並用 `ErrorMessage` 顯示其結果。

| 規則 | 何時檢查 | 顯示在哪裡 |
|---|---|---|
| 強度與規則 | 輸入時 | 強度計、強度文字和規則清單 |
| 最小長度與字元類別 | 輸入時；欄位為空時在送出時檢查 | 規則清單；只在送出後才用 `ErrorMessage` |
| 與第一次輸入的密碼相符的確認 | 確認欄位失去焦點時 | 確認 `Input` 上的 `ErrorMessage` |
| 重複使用目前的密碼，或已外洩的密碼 | 送出時，由伺服器判斷 | 新密碼欄位上的 `ErrorMessage` |
| 伺服器拒絕變更 | 請求回傳後 | `ErrorMessage`；值變更時清除 |

```csharp
// 在對應的欄位上顯示伺服器的回應，使用者編輯該值後清除。
newPassword.ErrorMessage = result.IsSuccess ? null : result.Error;
newPassword.TextChanged += (_, _) => newPassword.ErrorMessage = null;
```

### 錯誤

設定 `ErrorMessage` 可將欄位標記為無效。與目前已顯示內容不同的新訊息會使欄位晃動一次。若再次送出相同的文字，欄位不會晃動，因此當使用者再次送出並需要回饋時，應設定不同的訊息，或先清除錯誤再重新設定。

<Demo name="Errors" />

### 使用自訂評分器評分

`Scorer` 接受一個 `IPasswordStrengthScorer`。`Text` 每次變更時，控制項都會呼叫 `ScoreAsync` 並傳入目前的規則，然後顯示回傳的 `PasswordStrengthScore`。設定了 `Scorer` 後，控制項不再使用 `ShortLength` 和 `CharacterCounter`；由評分器決定等級、短密碼狀態和符合的規則。如果某個結果在使用者新的按鍵之後才回傳，它會被忽略，因此較慢的評分器不會顯示較早密碼的強度。

```csharp
public sealed class BreachCheckedScorer : IPasswordStrengthScorer
{
    private readonly Func<string, CancellationToken, ValueTask<bool>> _isKnownBreached;

    public BreachCheckedScorer(Func<string, CancellationToken, ValueTask<bool>> isKnownBreached)
    {
        _isKnownBreached = isKnownBreached;
    }

    public async ValueTask<PasswordStrengthScore> ScoreAsync(
        string password, IReadOnlyList<PasswordRule> rules, CancellationToken cancellationToken)
    {
        var score = PasswordStrengthEstimator.Default.Score(password, rules);
        if (score.Level > 0 && await _isKnownBreached(password, cancellationToken))
        {
            return new PasswordStrengthScore(1, score.MetRuleIds, false);
        }
        return score;
    }
}

var knownBreached = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Welcome-2026", "Password-123" };
field.Scorer = new BreachCheckedScorer((password, _) => ValueTask.FromResult(knownBreached.Contains(password)));
```

委派是應用程式自己的檢查；上面的集合充當本機外洩清單的替身。若評分器在 UI 執行緒上執行，請保持它快速：控制項會等待結果，因此阻塞呼叫會延遲觸發它的那次按鍵。只在停頓之後再呼叫伺服器，或將檢查保留在伺服器端，並用 `ErrorMessage` 顯示其結果。

### 訊息與在地化

控制項顯示的每個字串都在 `Messages` 中，它是一個附英文預設值的 `PasswordStrengthMessages` 記錄。替換整個記錄，或用 `with` 修改其中一個值，即可翻譯強度文字、顯示切換按鈕的名稱、無障礙名稱以及螢幕閱讀器播報的摘要。

```csharp
field.Messages = field.Messages with
{
    Weak = "Schwach",
    Fair = "Mittel",
    Good = "Gut",
    Strong = "Stark",
    TooShort = "Zu kurz",
    RemainingSuffix = "fehlen",
    SummaryFormat = "Stärke: {0}. {1} von {2} Anforderungen erfüllt."
};
```

`SummaryFormat` 依序接受三個預留位置：強度文字、符合的規則數和規則總數。`RemainingSuffix` 接在規則清單的滾動計數之後，因此在範例中，「4 more」會變成「4 fehlen」。

### 範例：變更密碼表單

使用者在設定頁面中變更工作區密碼的畫面。欄位在其說明中展示了政策，規則清單在使用者輸入第一個字母之前就可見；截圖捕捉的是部分輸入後的狀態：四條規則中符合了三條，長度規則仍顯示「還差 1 個」。送出按鈕寫明了動作，位於表單末尾，按鈕文字包含動詞和對象。強度計不會阻擋按鈕；送出時由伺服器決定。

![變更密碼表單，密碼良好，有一條長度規則未符合](images/PasswordChangeForm.png)

```xml
<StackPanel Width="560" Spacing="20">
    <TextBlock Text="Change password"
               FontSize="18"
               FontWeight="SemiBold"
               Foreground="{arc:ArcDesignTokenResource Foreground}" />
    <TextBlock Text="Use this password for the Acme Analytics workspace."
               FontSize="14"
               TextWrapping="Wrap"
               Foreground="{arc:ArcDesignTokenResource TextSecondary}" />
    <arc:PasswordStrength Label="New password"
                          Text="Ledger-2026"
                          Description="Use at least 12 characters, with a symbol."
                          Placeholder="Choose a password" />
    <StackPanel Orientation="Horizontal"
                HorizontalAlignment="Right"
                Spacing="12">
        <arc:Button Content="Cancel" Variant="Secondary" />
        <arc:Button Content="Update password" />
    </StackPanel>
</StackPanel>
```

### 文案規範

用欄位的用途為其命名，並以預設標籤所用的肯定形式陳述每條規則。錯誤提示要說明應如何修改。

| 不要寫 | 改為 |
|---|---|
| 密碼 | 新密碼 |
| 密碼無效 | 至少使用 12 個字元，並包含一個符號。 |
| 弱密碼 | 加入一個符號，讓密碼更強。 |
| 請輸入強密碼 | 請設定至少 12 個字元的密碼。 |
| 送出 | 更新密碼 |

使用阿拉伯數字表示計數，不用驚嘆號。說明文字只用一句話陳述政策。

### 最佳做法

**建議**

- 從使用者第一次按鍵起就顯示規則清單。使用者需要在猜測之前知道規則；只在送出失敗後才出現的規則，會多耗一次往返。
- 用 `PasswordRules.Create` 建立一套規則清單，並將它指派給產品中的每個密碼表單。若強度計接受而伺服器拒絕某條規則，就會導致送出失敗。
- 在伺服器端執行相同的規則。強度計是裝置端的估算，用戶端檢查可以被繞過。
- 當政策的最小長度高於 8 時，把 `ShortLength` 設為該最小長度，使短密碼顯示「太短」而不是「弱」。

**避免**

- 因強度計顯示「弱」而停用送出按鈕。應在送出時驗證，並在欄位上顯示原因。
- 在登入中使用此控制項。此時規則只會向猜測密碼的人暴露政策。
- 每次按鍵都指派 `Rules` 或 `Messages`。指派 `Rules` 會重建清單並重新評分。
- 阻塞 UI 執行緒的評分器。控制項會在每次變更時等待 `ScoreAsync`，因此阻塞呼叫會延遲觸發它的那次按鍵。

### 常見問題

**三條規則已符合，為什麼強度計仍顯示「太短」？** 在 `ShortLength`（預設為 8）以下，無論符合哪些規則，等級都固定為 1，而規則清單的各列仍顯示各自的狀態。要改變門檻，請修改 `ShortLength`。

**為什麼我最後一次按鍵時 `StrengthChanged` 沒有觸發？** 它只在等級、短密碼狀態或符合的規則集合發生變化時才觸發。需要每次按鍵的結果時，請在你自己的 `TextChanged` 處理常式中讀取 `Score` 或 `Level`。

**長度規則與 `CharacterCounter` 的結果不一致。** `CharacterCounter` 只會改變 `ShortLength` 以及控制項預設建立的估算器。預設規則以碼位計數。請用 `PasswordRules.Create(countCharacters: ...)` 建立規則清單，使規則的計數方式與之一致。

**`Scorer` 會覆寫 `ShortLength` 嗎？** 會。設定了 `Scorer` 後，控制項直接使用它，因此 `ShortLength` 與 `CharacterCounter` 不再生效。回傳的 `PasswordStrengthScore` 決定等級和「太短」狀態。

## API 參考

<!-- api: PasswordStrength -->

### 配套型別

`StrengthChanged` 傳遞 `PasswordStrengthChangedEventArgs`。`Score` 與事件引數都攜帶此結果：

| 成員 | 型別 | 含義 |
|---|---|---|
| `Level` | `int` | 空密碼為 0，之後從 1（弱或太短）到 4（很強） |
| `MetRuleIds` | `IReadOnlyList<string>` | 密碼符合的規則的 id |
| `IsShort` | `bool` | 密碼短於 `ShortLength` 時為 True；強度計停留在等級 1 |

`PasswordRule` 由一個 id、一段文字、一個判定函式以及一個可選的剩餘數量組成。

| 建構函式引數 | 型別 | 含義 |
|---|---|---|
| `id` | `string` | 在 `MetRuleIds` 中回報的穩定 id |
| `label` | `string` | 規則清單中顯示的文字 |
| `test` | `Func<string, bool>` | 密碼符合規則時為 True |
| `remaining` | `Func<string, int>?` | 仍缺少的字元數，以滾動計數顯示；可選 |

## 無障礙

- `Label` 為欄位命名。規則清單和錯誤是其說明文字的一部分。
- 強度計以唯讀進度列的形式呈現，值從 0 到 4。其名稱為 `Messages.MeterLabel`，其項目狀態為強度文字；欄位為空時為「尚未輸入密碼」。
- 每個規則列都以其文字命名，後接「，已符合」或「，未符合」。規則清單本身的名稱為 `Messages.RulesLabel`。
- 每次變更後，強度與符合規則的摘要會以禮貌的方式播報，例如「強度：良好。已符合 3 條規則，共 4 條。」
- 顯示切換按鈕在兩種狀態下都保持同一名稱「顯示密碼」。其開啟狀態告訴輔助技術密碼正在顯示。
- 錯誤列與 `Input` 相同。其訊息整則替換，因此螢幕閱讀器讀出的是一則完整的訊息。
- 游標、選取、輸入法、復原與剪貼簿快速鍵都是原生 `TextBox` 的行為。

### 鍵盤

| 按鍵 | 操作 |
|---|---|
| `Tab` / `Shift+Tab` | 在欄位與顯示切換按鈕之間移動，二者依文字在 Tab 順序中的位置排列 |
| 顯示切換按鈕上的 `Enter` 或 `Space` | 顯示或隱藏密碼 |
| 文字按鍵、`Ctrl+Z` / `Ctrl+Y`（macOS 上為 `Cmd`） | 與 `TextBox` 一樣進行編輯、復原和重做 |

## 動態效果

- 四段強度計以波浪順序依次填滿並變換色調。強度文字隨等級上升或下降。
- 規則符合時會以小幅放大出現，並在其標記上畫出一條線。剩餘規則的計數會滾動。
- 新錯誤出現時，欄位晃動一次。
- 減少動態效果時，晃動、波浪和滾動計數都會移除，數值立即變化。關閉動態效果時同樣如此。

| 變化 | 完整 | 減少 | 關閉 |
|---|---|---|---|
| 強度計色段填滿或清空 | 每段以平滑彈簧滑入，依波浪順序交錯出現 | 立即完成 | 立即完成 |
| 強度文字 | 強度提升時上升，下降時下落，從柔和的模糊中出現 | 交叉淡化 | 立即完成 |
| 規則標記 | 圓圈以靈敏彈簧放大，勾號稍後描繪；取消符合時收回更快 | 立即完成 | 立即完成 |
| 規則文字 | 規則符合時，其顏色漸變為前景色 | 立即完成 | 立即完成 |
| 剩餘計數 | 數字滾動到新值 | 立即完成 | 立即完成 |
| 新錯誤 | 欄位晃動一次，並以彈簧穩定下來 | 不晃動 | 不晃動 |

## 相關元件

- `PasswordField`：用於登入，此時密碼是核對而非設定。
- `Input`：用於非機密文字。
- `Progress`：用於並非密碼的測量值。

| 元件 | 適用情境 |
|---|---|
| `PasswordField` | 使用者以已有的密碼登入 |
| `Input` | 值是非機密文字，例如名稱或金鑰標籤 |
| `Progress` | 需要進度列來顯示測量值，例如匯出或上傳 |
| `Field` + `FieldFrame` | 你要自己建構編輯器，並希望它具有相同的標籤、外框和訊息列 |
| `Textarea` | 長篇備註或說明需要多行書寫 |
| `Button` | 表單需要送出動作；用 `IsDefault="True"` 支援 Enter |
