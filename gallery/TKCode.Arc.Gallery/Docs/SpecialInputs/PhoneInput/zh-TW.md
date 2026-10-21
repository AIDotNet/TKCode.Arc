---
title: 電話輸入框
summary: 帶國家或地區選擇器、輸入時即時格式化並輸出 E.164 格式的電話欄位。
source-hash: 99020d88eca5
---

<Demo name="Default" />

## 何時使用

`PhoneInput` 用於收集一組電話號碼以及它所屬的國家或地區。它與流程中的其他欄位並排放在表單裡，並處理一般文字欄位容易出錯的部分：國碼、本地格式、使用者在本地習慣輸入的長途前綴，以及伺服器保存的 E.164 值。使用者只需選擇一次國家或地區、輸入本地號碼，元件就會把保存的值統一為一種標準形式。

- **SaaS 產品中的註冊與帳號建立**：接收登入驗證碼的手機號碼。號碼必須完整後才能送出頁面，因此欄位在失去焦點時驗證，並顯示預期的位數。
- **電商中的結帳與配送資料**：快遞需要一個能聯絡到的號碼。用 `Country` 預先選取收件國家或地區，並用 `PreferredCountries` 列出店家配送的市場。
- **CRM 與客服工具中的聯絡表單**：每位聯絡人的客戶紀錄保存一個帶 `+` 的號碼。將 `Value` 繫結到紀錄，並原樣保存 E.164 字串，之後所有撥號與簡訊整合讀取的都是同一個值。
- **金融科技中的帳戶設定與帳單資料**：銀行或支付服務商發送提醒的號碼。`Countries` 可以把選擇器限制在產品取得授權的市場。
- **內部工具中的批次匯入畫面**：`PhoneNumbers.Parse` 在號碼顯示到欄位之前，讀取 "+44 (0)7911 123456" 或 "0044 7911..." 這類寫法，客服人員無需重新輸入即可修正某一列。

## 何時不用

- **不會被撥打的自由文字**：用 `Input`。作為標籤、編號或訂單備註保存的號碼不需要國家選擇器。
- **驗證碼**：請在你自己的介面中使用一次性驗證碼輸入。驗證碼不是電話號碼，本程式庫也不提供 OTP 輸入。
- **電信業者、號碼類型或可達性檢查**：請使用號碼中繼資料或訊息服務。`PhoneInput` 只根據內建的 49 個國家或地區表檢查號碼的長度與格式，不判斷線路是否可用。
- **沒有數字意義的固定選項清單**：當使用者從已知選項中選擇時（例如客服佇列或方案類型），用 `Select` 或 `Combobox`。
- **只用於顯示的號碼**：詳細資料頁上的值用 `Text`，或用 `IsReadOnly` 的 `Field`。在清單或表格中只需顯示 E.164 字串時，呼叫 `PhoneNumbers.Format`，無需編輯器。
- **金額或數量**：用 `MoneyInput` 或 `NumberField`。電話號碼是識別碼，它是帶格式的文字，而不是數量。

## 用法

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:PhoneInput Label="電話號碼"
                    Country="US"
                    Description="我們會向此號碼傳送登入驗證碼簡訊。" />
</UserControl>
```

`PhoneInput` 衍生自 `FieldTextBox`，而 `FieldTextBox` 衍生自 Avalonia 的 `TextBox`，因此號碼就是一般的文字編輯器：游標、選取、輸入法、復原與剪貼簿的行為都與文字方塊一致。元件在此基礎上加入國家按鈕、格式化、E.164 的 `Value`、國家清單與訊息列。`Label`、`Description` 與 `ErrorMessage` 與 `Input` 使用的屬性相同。

### 結構

1. **標籤**（`Label`）：欄位的名稱，顯示在外框上方。它也是號碼的無障礙名稱。只有當可見的標題已經為欄位命名時，才用 `IsLabelVisible="False"` 隱藏它。
2. **欄位外框**：欄位的 Arc 外框。預設主題下為中等控制項高度（44 px），帶控制項圓角與表面色。懸停時邊框加深，聚焦時變為前景色，無效時變為危險色。尺寸始終不變。
3. **國家按鈕**：高 36 px 的按鈕，在外框內縮進 3 px。它顯示國旗與國碼（例如 "+1"），國旗前留 9 px，右側為下拉箭頭保留 33 px。箭頭位於錨點右邊緣 12 px 處，一條上下各縮進 10 px 的分隔線將觸發器與號碼分開。
4. **號碼**（文字編輯器，即 `PhoneInput` 本身）：使用者輸入的數字，依所選國家或地區的格式顯示。號碼左側留 12 px 內距，右側留 36 px，為有效標記保留空間。
5. **輸入提示**：已輸入數字之後、以淡化文字顏色顯示的剩餘預期號碼，不透明度為 72%。它只是提示，不屬於值。
6. **有效標記**：距右邊緣 12 px、寬 18 px 的位置，在號碼長度對該國家或地區有效時顯示。
7. **輔助文字列**（`Description`）：外框下方的輔助文案。只有在沒有訊息時才顯示。
8. **錯誤列**（`ErrorMessage` 或內建驗證訊息）：外框下方以危險色顯示的文案。`ErrorMessage` 中的文字會取代內建訊息。

### 預設值

`Value` 接受 E.164 字串，例如 `+447400123456`。國家或地區由國碼推斷，欄位以該國家或地區的格式顯示本地號碼。在程式碼中設定的值不會自動發出提示。

<Demo name="PresetValue" />

### 限定國家或地區

`Countries` 把選擇器限制為列出的 ISO 3166-1 alpha-2 代碼。當產品或店家只在固定的市場中營運時使用它：例如一家只服務挪威、瑞典和丹麥的北歐銀行，或只接受其配送地圖上國家的運費表單。`PreferredCountries` 列出顯示在上方「建議」分組中的國家或地區。不在 `Countries` 中的建議國家會被去除，因此受限清單永遠不會提供產品不服務的國家。`PreferredCountries` 為空時，顯示依名稱排序的單一清單。

<Demo name="RestrictedCountries" />

<Demo name="NoPreferredCountries" />

給任一屬性指派一個新清單即可更改。原清單被原地修改時，選擇器不會重新整理，因此應替換整個清單。

### 驗證與錯誤

驗證在號碼失去焦點後執行，而且只在使用者輸入過數字時進行。從未輸入過內容的欄位保持安靜，使選填的號碼可以留空。號碼不完整或過長時，錯誤列顯示內建訊息，外框變為危險色。號碼完整且長度有效時，顯示有效標記。

| 情況 | 檢查時機 | 訊息 |
|---|---|---|
| 已輸入數字，位數少於該國家或地區的有效長度 | 號碼失去焦點時 | 內建訊息："Numbers in United States have 10 digits" |
| 已輸入數字，位數多於最長有效長度 | 號碼失去焦點時 | 同一則內建訊息，使用該國家或地區的長度 |
| 長度有效的完整號碼 | 從不顯示為錯誤；顯示有效標記 | 無 |
| `ErrorMessage` 有內容 | 立即 | `ErrorMessage` 的文字取代內建訊息 |
| `IsValidationEnabled` 為 `False` | 從不 | 無；只顯示 `ErrorMessage` |

`IsValidationEnabled="False"` 會關閉內建檢查，此時只顯示 `ErrorMessage`。當伺服器在送出後驗證號碼，或在允許不完整號碼的資料頁中，使用它。`ValidationTextFormat` 用於更改內建文案：其中的 `{0}` 為國家或地區名稱，`{1}` 為有效長度，例如 "10"，對有兩種長度的國家或地區則為 "10 or 11"。

<Demo name="ValidationOff" />

<Demo name="DescriptionAndError" />

`ValidationMessage` 回傳目前顯示的內建訊息，沒有時為 `null`。當表單需要把訊息當作文字使用時讀取它，例如在長表單頂端的摘要中。`Status` 以值的形式給出相同的結論：`Empty`、`Incomplete`、`Valid` 或 `TooLong`。

### 儲存與檢查號碼

儲存 `Value` 字串。它是 E.164 格式：加號、國碼，加上不含長途前綴的本地數字，例如 `+14155550132`。`PhoneNumbers` 輔助方法與選擇器使用同一份資料，因此表單、匯入器與清單可以共用一套規則，無需第二張表：

| 輔助方法 | 用途 |
|---|---|
| `PhoneNumbers.Format(e164)` | 在表格或摘要中顯示已保存的號碼，例如 "+44 7400 123456"；無法解讀時原樣回傳輸入 |
| `PhoneNumbers.Parse(input)` | 把貼上或匯入的號碼解析為國家或地區與本地數字；文字不以 "+" 或 "00" 開頭，或國碼未知時回傳 `null` |
| `PhoneNumbers.StatusOf(country, digits)` | 使用與欄位相同的長度，在伺服器端檢查本地號碼 |
| `PhoneNumbers.ToE164(country, digits)` | 根據國家或地區與本地數字產生保存的值 |
| `PhoneNumbers.Find(iso)` | 取得 ISO 代碼對應的表項，未知代碼回傳 `null` |

要在程式碼中檢查已保存的紀錄，把 `Value` 傳給 `PhoneNumbers.Parse`。它會回傳 `Country` 與供 `StatusOf` 使用的 `National` 數字：

```csharp
var parsed = PhoneNumbers.Parse(contact.Phone);
if (parsed is { } number)
{
    var status = PhoneNumbers.StatusOf(number.Country, number.National);
    // status 為 Valid、Incomplete 或 TooLong；為 Valid 時原樣保存 contact.Phone。
}
```

### 繫結與事件

`Value` 與 `Country` 是雙向的。每次編輯（輸入、貼上、選取國家或地區）都會觸發 `ValueChanged`。它會回報舊的與新的 E.164 值、國家或地區、格式化後的號碼、狀態以及 `IsValid`。即時回饋用事件，保存的值用繫結。

<Demo name="ValueChanged" />

```xml
<arc:PhoneInput Label="電話號碼"
                Value="{Binding Phone, Mode=TwoWay}"
                Country="{Binding Country, Mode=TwoWay}" />
```

```csharp
phone.ValueChanged += (_, e) =>
{
    Console.WriteLine($"{e.NewValue} ({e.Status}, valid: {e.IsValid})");
};

phone.OpenPicker();
```

在程式碼中為 `Value` 指派時同樣會觸發 `ValueChanged`。如果處理常式會把值寫回，應先比較 `e.NewValue` 與已保存的值，避免迴圈。`OpenPicker()` 與 `ClosePicker()` 用於在程式碼中開啟和關閉國家清單；`IsPickerOpen` 回報其狀態。

### 繫結到檢視模型

在 MVVM 應用程式中，號碼、國家或地區以及保存規則都由檢視模型持有。欄位負責顯示這些值，並把編輯寫回。下例使用 CommunityToolkit.Mvvm，並在呼叫儲存之前用 `PhoneNumbers` 輔助方法檢查號碼。

```csharp
public partial class ContactViewModel : ObservableObject
{
    private readonly IContactService _contacts;

    public ContactViewModel(IContactService contacts)
    {
        _contacts = contacts;
    }

    [ObservableProperty]
    private string _phone = string.Empty;

    [ObservableProperty]
    private string _country = "US";

    [ObservableProperty]
    private string? _phoneError;

    [RelayCommand]
    private async Task SaveAsync()
    {
        PhoneError = null;
        var parsed = PhoneNumbers.Parse(Phone);
        if (parsed is not { } number ||
            PhoneNumbers.StatusOf(number.Country, number.National) != PhoneNumberStatus.Valid)
        {
            PhoneError = "請輸入完整的電話號碼，例如 +1 415 555 0132。";
            return;
        }

        await _contacts.SaveAsync(Phone);
    }
}
```

```xml
<StackPanel Spacing="16">
    <arc:PhoneInput Label="手機號碼"
                    Value="{Binding Phone, Mode=TwoWay}"
                    Country="{Binding Country, Mode=TwoWay}"
                    ErrorMessage="{Binding PhoneError}" />
    <arc:Button Content="儲存聯絡人" Command="{Binding SaveCommand}" IsDefault="True" />
</StackPanel>
```

`Value` 與 `Country` 是雙向屬性。`SaveCommand` 是繫結到送出按鈕的命令，`ErrorMessage` 接收伺服器端的規則。號碼變動時清除 `PhoneError`，避免過時的訊息停留在畫面上。`ValidationMessage`、`Status`、`SelectedCountry` 與 `IsPickerOpen` 是唯讀屬性；表單需要時單向繫結即可。

### 停用與隱藏標籤

`IsEnabled="False"` 會讓欄位變淡，並阻止國家清單開啟。`IsLabelVisible="False"` 會隱藏標籤，但標籤仍為輔助技術提供號碼的名稱。只有欄標題或標題已經為欄位命名時，才應隱藏它。

<Demo name="States" />

### 鍵盤

國家按鈕在 Tab 順序中位於號碼之前。在按鈕上，ArrowDown 與 ArrowUp 會開啟清單，輸入可列印字元則開啟以該字元為初始內容的搜尋。在號碼中輸入 `+` 會以國碼搜尋開啟清單；在分隔符號上按 Backspace 或 Delete 會刪除其旁邊的數字。完整的鍵位表見無障礙部分。

<Demo name="Keyboard" />

### 範例：結帳聯絡資料

結帳流程中的聯絡資料步驟。手機號碼以 E.164 保存，並預先選取了國家或地區，因為快遞員會在配送當天撥打這個號碼。備用號碼顯示一則取代內建驗證的訊息，給出使用者可以採取行動的指引。電子郵件欄位與按鈕完成這一步，主要按鈕的文字說明了下一步會發生什麼。

![含手機號碼、帶錯誤的備用號碼與電子郵件欄位的結帳聯絡資料](images/CheckoutContact.png)

```xml
<StackPanel Width="560" Spacing="20">
    <arc:Input Label="Full name"
               Text="Maya Chen" />
    <arc:PhoneInput Label="Mobile number"
                    Value="+14155550132"
                    Description="The courier calls this number on delivery day." />
    <arc:PhoneInput Label="Alternate number"
                    Country="GB"
                    Value="+447911"
                    ErrorMessage="Enter the full number, such as 07911 123456." />
    <arc:Input Label="Delivery email"
               Text="maya.chen@northwind.com"
               Description="Order updates go to this address." />
    <StackPanel Orientation="Horizontal" Spacing="8" HorizontalAlignment="Right">
        <arc:Button Content="Back" Variant="Secondary" />
        <arc:Button Content="Continue to payment" />
    </StackPanel>
</StackPanel>
```

### 文案規範

- **標籤**：依用途為號碼命名，使用句首大寫：「手機號碼」、「配送電話」、「備用號碼」。只有表單中沒有其他號碼時才用「電話號碼」。標籤在使用者輸入時始終可見。
- **輔助文字**：用一句話說明這個號碼用於什麼，例如「我們會向此號碼傳送登入驗證碼簡訊。」只說明後果，讓使用者理解為什麼需要這個欄位。
- **錯誤提示**：說明缺少什麼，並以本地格式給出範例：「請輸入完整號碼，例如 07911 123456。」不要把驗證文字寫成規則（「號碼無效」）。
- **國家或地區名稱**：選擇器使用內建表中的英文名稱。請用 `SearchPlaceholder`、`SearchLabel`、`ListLabel`、`SuggestedLabel` 與 `AllCountriesLabel` 在地化選擇器的文字。
- **預留位置文字與範例**：預留位置文字與輸入提示來自該國家或地區的範例號碼，因此把 `Country` 設為表單所服務的市場，讓欄位顯示該國的本地格式。

| 不要寫 | 改為 |
|---|---|
| 電話 | 手機號碼 |
| 號碼無效。 | 請輸入完整號碼，例如 07911 123456。 |
| 電話號碼為必填項。 | 請輸入手機號碼，以便快遞員聯絡你。 |
| 號碼錯誤 | Numbers in United Kingdom have 10 digits.（內建訊息，保留即可） |

### 最佳做法

**建議**

- 原樣保存 `Value` 字串。E.164 是系統其他部分唯一需要的形式，簡訊、通話與 CRM 同步的整合都以它為準。
- 在使用者輸入之前，依其收貨、帳單或帳號所在的國家或地區設定 `Country`。選擇器會從使用者預期的位置開始，號碼也無需帶前綴。
- 把 `Countries` 限制在產品服務的市場內。如此選擇器永遠不會提供訂單或帳號無法使用的國家或地區。
- 用 `PhoneNumbers.StatusOf` 在伺服器端再次驗證號碼。用戶端檢查只是便利功能，伺服器不能只信任欄位。
- 註冊與結帳場景保持內建驗證開啟；否則過短的號碼會在之後悄無聲息地失敗。

**避免**

- 保存本地格式或使用者輸入的原始文字。以 "07911 123456" 保存的號碼，如果沒有所屬國家或地區就無法撥打，而每個使用端都得去猜測這個國家或地區。
- 因為選擇器看起來增加了工作量，就用 `Input` 存放電話號碼。一般文字欄位分不出 "+44" 與本地的 0，也無法保存號碼所屬的國家或地區。
- 依賴國旗 emoji 表達含義。在 Windows 上國旗會顯示為兩個字母，因此國家或地區名稱與國碼才是可靠的提示。
- 在使用者離開欄位之前就顯示驗證訊息。元件有意等待失去焦點，避免仍在輸入的號碼閃現錯誤。
- 原地修改 `Countries` 或 `PreferredCountries`。應指派一個新清單，因為選擇器是依屬性指派重建的。

### 常見問題

**我貼上的號碼選到了另一個國家或地區，這是 bug 嗎？** 不是。貼上或輸入的國際號碼（"+44 ..."、"+33 ..."）會依其國碼設定國家或地區，選擇器會播報 "Country set to ..."。當區域碼 "+1" 屬於加拿大時，會選取加拿大。

**如何知道號碼是否完整？** 讀取 `Status` 取得目前結果，或處理 `ValueChanged` 並讀取 `IsValid`。位數少於有效長度時 `Status` 為 `Incomplete`，多於最長有效長度時為 `TooLong`，長度有效時為 `Valid`。`Empty` 表示沒有任何本地數字。

**為什麼我剛輸入了一半的號碼沒有顯示錯誤？** 驗證只在號碼失去焦點、且輸入過數字之後才執行。離開欄位即可看到訊息；透過 `Value` 在程式碼中設定的號碼，要等使用者編輯並離開欄位後才會檢查。

**能否用其他語言顯示內建訊息？** 可以。用 `ValidationTextFormat` 設定，其中 `{0}` 為國家或地區名稱，`{1}` 為有效長度；並將 `NoMatchesTextFormat`、`SearchLabel`、`SearchPlaceholder` 等文字屬性設為你的字串。國家或地區名稱本身來自內建表。

## API 參考

<!-- api: PhoneInput -->

配套型別。`PhoneNumberStatus` 是 `Status` 的值，也是事件參數中的 `Status`：

| 成員 | 含義 |
|---|---|
| `Empty` | 沒有本地數字（只輸入長途前綴時仍為空） |
| `Incomplete` | 位數少於有效長度，或介於兩個有效長度之間 |
| `Valid` | 該國家或地區的有效長度 |
| `TooLong` | 位數多於最長有效長度 |

`PhoneInputValueChangedEventArgs` 是 `ValueChanged` 的參數：

| 成員 | 含義 |
|---|---|
| `OldValue` | 先前的 E.164 值，沒有數字時為 `""` |
| `NewValue` | 新的 E.164 值，例如 `+14155550132`，沒有數字時為 `""` |
| `Country` | 選取的 `PhoneCountry` |
| `Formatted` | 欄位中顯示的號碼，不含國碼；保留輸入的長途前綴 |
| `Status` | 號碼的 `PhoneNumberStatus` |
| `IsValid` | `Status` 為 `Valid` 時為 `True` |

`PhoneNumbers` 是欄位背後的靜態輔助類別。它是公開的，以便表單與匯入使用同一套規則：

| 成員 | 含義 |
|---|---|
| `Countries` | 內建表中的 49 個國家或地區，依表中順序排列 |
| `Find(iso)` | ISO 代碼對應的 `PhoneCountry`（不分大小寫），未知時為 `null` |
| `Flag(iso)` | 以區域指示字母表示的國旗；台灣、香港與澳門回傳中國的國旗 |
| `FormatNational(country, digits)` | 依輸入的樣子格式化本地數字，分隔符號僅在其後仍有數字時顯示 |
| `StatusOf(country, digits)` | 本地數字的 `PhoneNumberStatus`（允許長途前綴） |
| `ToE164(country, digits)` | E.164 格式，沒有數字時為 `""` |
| `Parse(input, pool)` | 把國際號碼解析為 `PhoneNumberParseResult`（`Country`、`National`），失敗時為 `null` |
| `Format(e164)` | 把 E.164 號碼格式化為便於顯示的形式，例如 "+44 7400 123456" |

## 無障礙

- `Label` 為欄位命名。`IsLabelVisible="False"` 時標籤隱藏，但仍作為名稱。
- `IsRequired="True"` 會為輔助技術與表單將號碼標記為必填。
- 國家按鈕的名稱為 "Country, <name> +<code>"，例如 "Country, United States +1"。搜尋方塊是組合方塊，會回報目前選取的國家或地區，清單包含 Suggested 與 All countries 兩個分組。
- ArrowDown 與 ArrowUp 在國家按鈕上開啟選擇器。在搜尋方塊內，方向鍵、PageUp 與 PageDown 用於移動，Home 與 End 跳到第一項與最後一項，Enter 選定，Escape 將焦點還給按鈕。
- 因貼上或選擇器而更改國家或地區的操作會以禮貌方式播報，內容為 "Country set to <name>" 或 "<name>, +<code>"。搜尋結果的數量以 "<n> countries" 或 "No matches" 播報。
- 號碼錯誤會在失去焦點後以輔助文字出現。有效號碼會播報為 "Valid <country> number"。
- 說明與錯誤是欄位的說明文字，在名稱與值之後朗讀。

### 鍵盤

| 按鍵 | 操作 |
|---|---|
| `Tab` | 從外部進入時移到國家按鈕；從按鈕移到號碼；從號碼離開欄位 |
| `Shift+Tab` | 向後移動：從號碼回到國家按鈕，從按鈕回到上一個控制項 |
| 國家按鈕上的 `ArrowDown` / `ArrowUp` | 開啟國家清單 |
| 國家按鈕上輸入可列印字元 | 開啟清單，並以該字元作為搜尋內容 |
| 國家按鈕上的 `Backspace` / `Delete` | 對號碼沒有作用 |
| 號碼中輸入 `+` | 開啟清單並以國碼搜尋，例如 "+44" 會找到 United Kingdom |
| 號碼中分隔符號上的 `Backspace` / `Delete` | 刪除分隔符號旁邊的數字 |
| 搜尋中的 `ArrowDown` / `ArrowUp` | 將目前國家或地區移動一列 |
| 搜尋中的 `PageDown` / `PageUp` | 每次移動 8 列 |
| 搜尋為空時的 `Home` / `End` | 移到第一列或最後一列 |
| 搜尋中的 `Enter` | 選定目前國家或地區，將數字保持在新國家或地區的長度範圍內，並返回號碼 |
| 搜尋中的 `Escape` | 關閉清單，並將焦點還給國家按鈕 |
| 搜尋中的 `Tab` / `Shift+Tab` | 關閉清單，並移到號碼或回到國家按鈕 |
| 清單外按下指標 | 關閉清單，不更改國家或地區 |

## 動態效果

- 欄位的表面從國家按鈕展開為國家清單：寬度、高度與圓角一起以彈簧變化。清單最寬 340 px，圓角從 17 px 變為 22 px。
- 國旗與國碼沿清單方向滾動到新國家或地區。反白在各列之間滑動。
- 訊息列平滑展開高度，文字帶著柔和模糊升起。有效標記會彈出。
- 減少動態效果時，表面直接跳到最終尺寸，內容以短淡入切換。關閉動態效果時立即切換。

| 變化 | 完整 | 減少 | 關閉 |
|---|---|---|---|
| 國家清單開啟 | 表面以彈簧從觸發器展開（0.4 s，輕微回彈） | 表面直接跳到最終尺寸；內容淡入 | 立即完成 |
| 國家清單關閉 | 表面以較短的彈簧收回（0.32 s，無回彈） | 表面直接跳到關閉尺寸；內容淡入 | 立即完成 |
| 國家切換 | 國旗與國碼沿清單方向滾入（0.2 s 淡入與滑動） | 短淡入 | 立即切換 |
| 反白在列間移動 | 反白滑動（0.28 s，輕微回彈） | 短淡入 | 立即切換 |
| 訊息列出現或變化 | 高度平滑展開；新文字帶著柔和模糊從下方升起 | 完整高度，短淡入 | 立即完成 |
| 有效標記出現 | 以靈敏的彈簧彈出 | 短淡入 | 立即完成 |
| 懸停、焦點、無效 | 邊框顏色漸變 | 邊框顏色漸變 | 立即完成 |

## 相關元件

- 非電話號碼的文字用 `Input`，多行文字用 `Textarea`。
- 固定值清單用 `Select`；要圍繞自己的編輯器建立類似的帶標籤欄位，用 `Field` 或 `FieldFrame`。
- 有各自格式規則的數值用 `MoneyInput` 與 `NumberField`。
- 密碼與搜尋文字用 `PasswordField` 與 `SearchField`。

| 元件 | 適用情境 |
|---|---|
| `Input` | 值是自由文字、識別碼或 URL，沒有國家或國碼 |
| `Select` / `Combobox` | 使用者從已知清單中選擇一個值，且該清單不是國家集合 |
| `NumberField` / `MoneyInput` | 值是需要格式化與取整的數量或金額 |
| `SearchField` | 文字用於篩選或搜尋內容，而不是保存為聯絡方式 |
| `Field` + `FieldFrame` | 你要自己建立編輯器，並希望與標籤、外框和訊息列保持一致 |
