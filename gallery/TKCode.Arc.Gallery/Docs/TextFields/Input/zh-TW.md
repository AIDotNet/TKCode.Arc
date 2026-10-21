---
title: 輸入框
summary: 標籤清楚、狀態實用的單行輸入框。
source-hash: 92e9f7d582c4
---

<Demo name="Basic" />

## 何時使用

`Input` 是讓使用者輸入一段簡短自由文字的預設控制項。它自帶完整的欄位結構（可見標籤、外框、輔助文字與錯誤列），因此一個表單就是一欄輸入框，不需要在外面另外排版。

- **身分與聯絡資料**：註冊、結帳、個人資料表單中的姓名、公司電子郵件、職稱、公司名稱。這類值沒有固定格式，用一個搭配好驗證的一般文字欄位最合適。
- **管理後台與開發者主控台中的短識別碼**：專案 slug、Webhook URL、API 金鑰名稱、DNS 記錄值。用 `Prefix` 和 `Suffix` 顯示固定的部分（`https://`、`.acme.dev`），讓使用者只輸入會變動的部分。
- **需要即時回饋的值**：有長度與字元規則的使用者名稱，或在輸入時檢查是否可用的工作區網址。輔助文字列只改寫變動的詞、數字逐位捲動，持續的回饋也顯得平穩而不閃爍。
- **需要複製的唯讀值**：設定表單中的工作區 ID 或帳號，與可修改的欄位放在一起，`IsReadOnly` 維持相同的外框與對齊。

## 何時不用

- **多行文字**，例如說明、備註、客服訊息或整段填寫的郵寄地址：用 `Textarea`，它會換行並可調整高度。
- **密碼等機密資訊**：用 `PasswordField`（遮罩顯示，附顯示切換）；讓使用者設定新密碼並需要列出規則時用 `PasswordStrength`。
- **有固定形態的值**：需要步進或拖曳調整的數字（`NumberField`、`ScrubInput`）、金額（`MoneyInput`）、電話號碼（`PhoneInput`）、標籤或收件者等短詞元（`TagInput`）以及 @提及（`MentionInput`）。這些控制項會解析並格式化值，而 `Input` 只保存一個字串。
- **從已知清單中取值**：用 `Select` 或 `Combobox`；日期與時間用 `DatePicker` 和 `TimePicker`。
- **搜尋與篩選**：用 `SearchField`（清除按鈕、Escape 清空、`SearchSubmitted`），頁首中用 `ExpandingSearch`。
- **在頁面上就地重新命名標題**：用 `InlineEdit`，它在編輯前看起來就是一般文字。

## 用法

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:Input Label="電子郵件"
               Placeholder="you@example.com"
               Description="我們不會公開你的電子郵件。" />
</UserControl>
```

`Input` 衍生自 Avalonia 的 `TextBox`，文字方塊的能力全部保留：雙向的 `Text`、`MaxLength`、`IsReadOnly`、`TextAlignment`、`CaretIndex`、`SelectAll()`、復原、輸入法組字以及剪貼簿快速鍵。欄位在此之上加入標籤、訊息列與 Arc 外框。

### 結構

1. **標籤**（`Label`）：14 px、中等字重，位於外框上方 8 px。它同時是欄位的無障礙名稱。只有當其他可見元素已經為欄位命名時，才用 `IsLabelVisible="False"` 隱藏它。
2. **外框**：1 px 邊框、控制項圓角、表面色背景。懸停與焦點只加深邊框顏色，出錯時邊框變為危險色，尺寸始終不變。
3. **前綴**（`Prefix`）：文字前的選用內容，例如通訊協定、貨幣符號或圖示。
4. **文字與預留位置文字**：可編輯的文字；文字為空時以淡化顏色顯示 `Placeholder`。
5. **後綴**（`Suffix`）：文字後的選用內容，例如單位或一個小操作。
6. **輔助文字列**（`Description`）：外框下方 12 px 的淡化色說明文案。
7. **錯誤列**（`ErrorMessage`）：12 px 的危險色文案。只要它有內容，欄位就處於無效狀態。

### 輔助文字與錯誤提示

文案應由狀態推導，直接修改字串即可：只有變動的詞會移動，數字逐位捲動。不要在幾則獨立的訊息之間來回切換。

<Demo name="HelperAndError" />

```xml
<arc:Input Label="使用者名稱"
           Text="{Binding UserName}"
           Description="{Binding UserNameHint}"
           ErrorMessage="{Binding UserNameError}" />
```

`ErrorMessage` 不為空時欄位即為無效狀態；為空時顯示 `Text` 繫結的驗證錯誤。

畫面上實際顯示的錯誤是 `EffectiveErrorMessage`：`ErrorMessage` 有內容時取它，否則取繫結驗證錯誤的訊息。只要兩者之一存在，`IsInvalid` 就會被強制為 `True`；只有在需要顯示危險色邊框而不顯示訊息時才手動設定它，例如一組一起驗證失敗的欄位。

### 繫結到檢視模型

在 MVVM 應用程式中，值和規則都歸檢視模型所有，輸入框只負責呈現。使用 CommunityToolkit.Mvvm 時，`ObservableValidator` 會在屬性指派時驗證，並透過 `INotifyDataErrorInfo` 回報錯誤；Avalonia 把錯誤傳給 `Text` 繫結，輸入框顯示其中第一則。

```csharp
public partial class ProfileViewModel : ObservableValidator
{
    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Required(ErrorMessage = "請輸入姓名。")]
    [StringLength(80, ErrorMessage = "最多 80 個字元。")]
    private string _fullName = string.Empty;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Required(ErrorMessage = "請輸入公司電子郵件。")]
    [EmailAddress(ErrorMessage = "請輸入完整的電子郵件地址，例如 name@company.com。")]
    private string _email = string.Empty;

    [RelayCommand]
    private async Task SaveAsync()
    {
        ValidateAllProperties();
        if (HasErrors)
        {
            return;
        }
        await _profiles.SaveAsync(FullName, Email);
    }
}
```

```xml
<StackPanel Spacing="16">
    <arc:Input Label="姓名" Text="{Binding FullName}" />
    <arc:Input Label="公司電子郵件"
               Text="{Binding Email, UpdateSourceTrigger=LostFocus}"
               Placeholder="name@company.com" />
    <arc:Button Content="儲存資料" Command="{Binding SaveCommand}" IsDefault="True" />
</StackPanel>
```

- `UpdateSourceTrigger=LostFocus` 在使用者離開欄位時才把電子郵件寫回檢視模型，因此格式錯誤會在輸入完成後出現，而不是在按下第一個字元時。輔助文字需要隨輸入變化的值（例如字元計數）保留預設的 `PropertyChanged`。
- 只保留一個驗證來源。檢視模型驗證 DataAnnotations 屬性、而 Avalonia 內建的 DataAnnotations 外掛程式也檢查同一組屬性時，每條規則會執行兩次；Avalonia 的 MVVM 應用程式範本正是因此移除了該外掛程式。
- 編譯繫結需要在檢視上設定 `x:DataType`（為求簡潔，這裡的範例省略了它）。

### 驗證時機

好的表單會在使用者能夠處理問題的時刻檢查對應規則。觸發太早像是在指責使用者，觸發太晚則白白浪費一次送出。

| 規則 | 何時檢查 | 顯示在哪裡 |
|---|---|---|
| 長度限制與允許的字元 | 輸入時 | `Description` 倒數計數（「還剩 12 個」）；超出限制後才用 `ErrorMessage` |
| 格式（電子郵件、URL、slug） | 欄位失去焦點時 | `ErrorMessage`（搭配 `LostFocus` 的繫結驗證） |
| 必填 | 送出時，之後在值仍為空期間持續檢查 | `ErrorMessage`；把焦點移到第一個無效欄位 |
| 是否可用或唯一（伺服器端） | 停止輸入片刻後，或失去焦點時 | 檢查期間用 `Description`（「正在檢查…」），已被使用時用 `ErrorMessage` |
| 儲存時伺服器回傳的錯誤 | 請求結束後 | 在錯誤所指的欄位上用 `ErrorMessage`；值變動後清除 |

```csharp
// 以伺服器為準：把它的訊息顯示在對應的欄位上，值一變動就清除。
var result = await api.CreateWebhookAsync(endpoint.Text);
endpoint.ErrorMessage = result.IsSuccess ? null : result.Error;

endpoint.TextChanged += (_, _) => endpoint.ErrorMessage = null;
```

### 尺寸

`Size` 決定外框高度：`Small` 為 36，`Medium` 為 44（Arc 的預設外觀），`Large` 為 50。

<Demo name="Sizes" />

同一個表單只用一種尺寸。`Small` 適合密集的管理表格與工具列，`Medium` 是表單和設定頁的預設值，`Large` 適合單一醒目的欄位，例如登入頁的電子郵件。挑選器、選擇器與特殊輸入框使用相同的 `Size` 值，混合使用時統一尺寸即可對齊。

### 前綴與後綴

<Demo name="PrefixAndSuffix" />

`Prefix` 與 `Suffix` 用於值的上下文而非值本身：網域前的 `https://`、價格前的 `$`、數字後的 `USD` 或 `GB`、子網域後的 `.acme.dev`。用淡化文字顏色顯示它們，讓輸入的文字維持最醒目。它們不屬於 `Text`，檢視模型只會收到使用者輸入的內容。需要以數字或金額解析的值，請用 `NumberField` 或 `MoneyInput`，而不是替一般輸入框加前綴。

### 狀態

欄位可以處於停用、唯讀或無效狀態。

<Demo name="States" />

- **唯讀**：值仍可取得焦點、選取和複製，輔助技術也照常朗讀。用於需要檢視和複製、但在此處不能修改的值，例如 ID。
- **停用**：欄位淡化到 50% 並移出 Tab 順序。只在值暫時不適用時使用，並在附近說明原因，例如在開啟該功能的開關的說明中。
- **無效**：顯示危險色邊框與錯誤列。錯誤應保留到值被修正為止，不要在取得焦點時清除。

### Field 基礎

`Field` 在 `FieldFrame` 外加上標籤與訊息列。挑選器與特殊輸入框會重複使用這兩者。

<Demo name="FieldFoundation" />

自行實作觸發器或複合編輯器、又希望它與其他欄位外觀和行為一致時（相同的標籤、外框狀態、訊息列與尺寸），使用 `Field` 和 `FieldFrame`。`Field.MessageSwap` 決定訊息列如何變化：`Words`（預設）只移動變動的詞，`Whole` 整列替換。

### 範例：工作區設定表單

SaaS 管理後台中的一個設定表單。標籤是簡短的名詞，輔助文字說明後果（「顯示在發票上」），工作區 ID 設為唯讀以便複製，URL 欄位把網址的固定部分顯示為前綴，使用者只需輸入屬於自己工作區的部分。電子郵件欄位示範了表單應給出的錯誤：說明哪裡有問題以及如何修正。

![含唯讀 ID、帶前綴的 URL 欄位與電子郵件錯誤的工作區設定表單](images/WorkspaceForm.png)

```xml
<StackPanel Width="560" Spacing="20">
    <arc:Input Label="Workspace name"
               Text="Acme Analytics"
               Description="Shown on invoices and in emails to your team." />
    <arc:Input Label="Workspace ID"
               Text="ws_7f3a9c21"
               IsReadOnly="True"
               Description="Use it when you contact support." />
    <arc:Input Label="Workspace URL"
               Text="acme-analytics"
               Description="Lowercase letters, numbers and hyphens.">
        <arc:Input.Prefix>
            <TextBlock Text="app.acme.dev/"
                       Foreground="{arc:ArcDesignTokenResource TextMuted}"
                       VerticalAlignment="Center" />
        </arc:Input.Prefix>
    </arc:Input>
    <arc:Input Label="Billing email"
               Text="billing@acme"
               ErrorMessage="Enter a full email address, such as billing@acme.com." />
    <StackPanel Orientation="Horizontal" Spacing="8" HorizontalAlignment="Right">
        <arc:Button Content="Cancel" Variant="Secondary" />
        <arc:Button Content="Save changes" />
    </StackPanel>
</StackPanel>
```

### 文案規範

- **標籤**：句首大寫的簡短名詞片語，不加冒號，例如「公司電子郵件」，而不是「請輸入您的公司電子郵件：」。標籤在輸入時始終可見，所以由它承載含義；預留位置文字做不到。
- **預留位置文字**：提供預期格式的範例（「name@company.com」「acme-analytics」），不要把它當作唯一的說明，也不要重複標籤。開始輸入後它就會消失。
- **輔助文字**：用一句話說明限制或後果，例如「3 到 20 個字元」「顯示在發票上」。數字用阿拉伯數字書寫，計數才能平順捲動。
- **錯誤提示**：說明哪裡有問題以及如何修正，用詞與輔助文字保持一致。避免指責和術語。

| 不要寫 | 改為 |
|---|---|
| 輸入無效。 | 請輸入電子郵件地址，例如 name@company.com。 |
| 錯誤：欄位必填 | 請輸入姓名。 |
| 使用者名稱太短！！ | 至少使用 3 個字元。 |
| 正規表示式不符 | 只能使用小寫字母、數字和連字號。 |

### 最佳做法

**建議**

- 每個輸入框都要有可見標籤，精簡版面也一樣；只有當可見的標題或欄標題已經為它命名時，才使用 `IsLabelVisible="False"`。
- 每條訊息列維持一個長期存在的字串並更新它，讓動態效果說明發生了什麼變化。
- 送出失敗時把焦點移到出錯的欄位上，並保留使用者已輸入的值。
- 欄位寬度與預期的值相符：郵遞區號欄位不應橫跨整頁。

**避免**

- 用預留位置文字取代標籤，或在開始輸入後就消失的說明。
- 格式類規則在按下第一個字元時就報錯；應等到失去焦點或送出時。
- 在表單有效之前停用送出按鈕；使用者無從得知缺了什麼。應在送出時驗證，並指向第一個問題。
- 依計時器在幾則不同的訊息之間切換；這樣訊息列永遠無法穩定下來。

### 常見問題

**繫結有驗證錯誤，但欄位顯示的是另一則訊息。** `ErrorMessage` 優先於繫結的錯誤。把 `ErrorMessage` 清空（設為 `null` 或空字串）後，驗證訊息就會顯示。

**如何按 Enter 送出表單？** `Input` 本身不處理 Enter。替表單的主要 `Button` 設定 `IsDefault="True"`，在任何欄位中按 Enter 都會觸發它。

**如何要求電子郵件鍵盤或自動填入提示？** 使用 Avalonia 的附加屬性 `TextInputOptions.ContentType`，例如 `TextInputOptions.ContentType="Email"`。螢幕小鍵盤與輸入法會據此調整。

**為什麼沒有右鍵選單？** Arc 輸入框不提供內容選單；剪下、複製、貼上、復原與全選都可以用鍵盤完成。

**逐詞改寫對中文或日文文案有效嗎？** 訊息列以空格分詞。沒有空格的文案會作為一個整體變化，仍有動畫，但會整列改寫。

## API 參考

<!-- api: Input -->

## 無障礙

- `Label` 是欄位的名稱。`IsLabelVisible="False"` 時標籤不顯示，但仍作為名稱。
- 輔助文字與錯誤提示合併為欄位的說明文字，在名稱和值之後朗讀。
- 錯誤列是一個 assertive 即時區域：新的錯誤會立即播報，焦點不會移動。
- 帶動畫的文字對輔助技術隱藏，輔助技術讀取的是一份純文字副本。
- 游標、選取、輸入法、復原與剪貼簿快速鍵都是原生 `TextBox` 的行為。
- 懸停與焦點只改變邊框顏色；取得焦點的欄位是邊框較深且有游標的那一個。在密集的表單中焦點不易辨認時，改用 `Large` 或加大欄位間距，而不是拿掉標籤。

### 鍵盤

| 按鍵 | 操作 |
|---|---|
| `Tab` / `Shift+Tab` | 移到下一個或上一個欄位；唯讀欄位仍會停留，停用欄位不會 |
| `Left` / `Right`、`Home` / `End` | 依字元移動游標，或移到開頭、結尾 |
| `Ctrl+Left` / `Ctrl+Right`（macOS 上為 `Option`） | 依詞移動游標 |
| `Shift` + 以上任一按鍵 | 擴大選取範圍 |
| `Ctrl+A`、`Ctrl+C`、`Ctrl+X`、`Ctrl+V`（macOS 上為 `Cmd`） | 全選、複製、剪下、貼上 |
| `Ctrl+Z` / `Ctrl+Y`（macOS 上為 `Cmd+Z` / `Cmd+Shift+Z`） | 復原、重做 |
| `Enter` | 輸入框本身不處理；視窗有預設按鈕時觸發該按鈕 |

## 動態效果

- 輔助文字列與錯誤列以平滑彈簧展開高度；變動的詞帶著模糊升起並變清晰，數字逐位捲動。
- 減少動態效果時，訊息列直接以完整高度出現，換詞改為瞬間淡入；關閉動態效果時直接到最終狀態。
- 懸停與焦點只加深邊框顏色，尺寸不變。

| 變化 | 完整 | 減少 | 關閉 |
|---|---|---|---|
| 訊息列出現或消失 | 高度以平滑彈簧展開，文字淡入 | 直接以完整高度顯示 | 立即完成 |
| 訊息內容變化 | 只有變動的詞帶著柔和模糊從下方升起 | 瞬間淡入 | 立即完成 |
| 文案中的數字變化 | 只有變動的位數捲動，變大向上、變小向下 | 瞬間淡入 | 立即完成 |
| 懸停、焦點、無效 | 邊框顏色漸變 | 邊框顏色漸變 | 立即完成 |

## 相關元件

- 多行文字用 `Textarea`，密碼用 `PasswordField`，搜尋用 `SearchField`。
- `Field` 與 `FieldFrame` 為任何編輯器或觸發器提供相同的標籤、外框與訊息列。

| 元件 | 適用情境 |
|---|---|
| `Textarea` | 文字跨多行，或使用者會貼上整段內容 |
| `PasswordField` / `PasswordStrength` | 值是機密資訊，或是帶規則的新密碼 |
| `SearchField` / `ExpandingSearch` | 文字用於篩選或搜尋，而不是儲存 |
| `NumberField` / `MoneyInput` / `PhoneInput` | 值是需要解析的數字、金額或電話號碼 |
| `TagInput` / `MentionInput` | 值是一組詞元，或帶 @提及的文字 |
| `InlineEdit` | 在標題或名稱顯示的位置直接重新命名 |
| `Field` + `FieldFrame` | 自行實作編輯器或觸發器，並希望與其他欄位一致 |
