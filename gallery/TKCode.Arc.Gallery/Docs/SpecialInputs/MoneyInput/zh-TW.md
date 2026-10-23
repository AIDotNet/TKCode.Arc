---
title: 金額輸入框
summary: 帶有即時分組、寬度穩定、數字滾動與最小單位輸出的貨幣輸入框。
source-hash: 464ecb4deb4f
---

<Demo name="Default" />

## 何時使用

`MoneyInput` 是用於手動輸入金額的欄位。使用者輸入時它會即時為數字分組，把缺少的角分顯示為淡色數字，金額增長時保持寬度穩定，並同時輸出支付與帳務介面所需的主單位值（$12.50 對應 `12.5`）和最小單位值（`1250`）。凡是使用者需要確定將要收取、退款、預算或轉帳的金額，且貨幣可能需要更改的地方，都適合使用它。

- **付款與結帳表單**：客戶輸入捐款、小費或自訂金額，並透過快速新增標籤選擇常用數值，最大值與卡片限額或訂單總額保持一致。
- **帳單主控台中的退款與額度頁面**：客服輸入不超過已付金額的退款金額，欄位下方的範圍文字會在輸入前說明允許的區間（“$5.00 to $120.00”）。
- **貨幣可更改的發票與報支單**：金額保持為數字，切換貨幣選單即可更改代碼，1,250.00 USD 的發票改為 EUR 時無需重新輸入數字。
- **金融應用程式中的預算與轉帳**：使用者設定每月限額，最小值在欄位失去焦點時檢查，因此輸入 0.50 會在離開欄位後才提示，而不是在輸入過程中提示。
- **需要最小單位的儲存與支付介面**：欄位提供 `MinorUnits`，應用程式為 $12.50 傳送 1250，永遠不需要處理二進位浮點金額。

## 何時不用

- **不帶貨幣的數量與計數**：用 `NumberField`，它有步進按鈕，單位不做貨幣格式化。
- **透過範圍滑動選取金額**：用 `Slider`，它一次展示完整的區間。
- **需要拖曳就地調整數值的精簡屬性面板**：用 `ScrubInput`，它透過拖曳標籤來改變數字。
- **看起來像數字的自由文字，例如帳戶代碼或發票號碼**：用 `Input`。金額欄位會為數字分組並捨棄字母，這會破壞識別碼。
- **從產品清單中選擇價格**：用 `Select` 或 `Combobox`。使用者選擇的是一筆記錄，而不是一個金額。

## 用法

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:MoneyInput Label="金額"
                    Currency="USD"
                    Minimum="5"
                    Maximum="1000"
                    Description="方向鍵每次調整一個單位。" />
</UserControl>
```

`MoneyInput` 衍生自 `FieldTextBox`，即帶有 Arc 標籤、輔助文字與錯誤列的 Avalonia `TextBox`。文字方塊只負責游標與選取範圍：它自身的文字是透明的，金額由欄位依欄逐位繪製在同一原點上。這就是數字能夠滾動和增長、而游標不會移動的原因。文字方塊同樣保留 Avalonia 的游標、選取範圍與剪貼簿輸入行為，但復原已關閉，因為金額是受控的，不能與 `Value` 產生偏差。

### 結構

1. **外殼**（PART_Frame）：高 76 px 的外框，採用面板圓角，左側內距 18 px，右側 8 px。金額本身取得焦點時邊框加深；貨幣切換按鈕取得焦點時不加深。
2. **符號與金額**（PART_Amount）：展示字型 36 px、中等字重，使用等寬數字且不做字距調整。字母間距為 -0.03 em。缺少的小數位以弱化顏色、55% 不透明度的淡色數字顯示。
3. **前綴或後綴符號**（PART_PrefixSymbol、PART_SuffixSymbol）：貨幣符號位於數字之前或之後，遵循地區文化的正數格式。`de-DE` 中的歐元符號位於金額之後。
4. **貨幣按鈕**（PART_CurrencyButton）：高 36 px 的按鈕，顯示 ISO 代碼與 16 px 的箭頭。點擊後開啟貨幣選單。`Currencies` 中只有一種貨幣時，代碼改為靜態文字顯示（PART_StaticCode）。
5. **範圍文字**（PART_Range）：外殼下方 12 px 的淡化文字，例如“$5.00 to $1,000.00”。拒絕修改時以警告色閃爍。
6. **快速新增標籤**（PART_Chips）：預設顯示為“+$10”“+$50”和“+$100”的 30 px 高標籤，間距 6 px，與範圍文字位於同一列。
7. **貨幣選單**：寬 256 px，列高至少 36 px，清單最高 288 px，與外殼相距 8 px。
8. **輔助文字與錯誤列**：來自 `Field` 的標籤、輔助文字與 `ErrorMessage` 列。

### 貨幣與快速新增標籤

`Currencies` 設定貨幣選單中的代碼。預設包含八個代碼：USD、EUR、GBP、JPY、CAD、AUD、CHF 與 INR。請使用與頁面服務市場相符的較短清單，因為過長的選單會拖慢選擇。只有一個代碼時，欄位以靜態文字顯示該代碼，且不會開啟選單。

`QuickAddAmounts` 以主單位設定標籤。預設值為 10、50 與 100。每個標籤都會把其金額加到目前的值上。清單為空時隱藏標籤。標籤應與金額情境相符：小費和捐款使用較小的值，退款則使用帳戶中訂單的金額。達到最大值時，標籤呈現為停用樣式。

日圓沒有最小單位，因此欄位不顯示角分，最小單位值等於主單位值。

<Demo name="SingleCurrency" />

<Demo name="NoQuickAdd" />

<Demo name="NoDecimals" />

### 地區文化

`Culture` 設定分組分隔符、小數分隔符與符號位置。預設值為 `en-US`，而且欄位不跟隨目前執行緒的地區文化，因此不同地區的使用者在每個畫面上看到的格式都一致。請把 `Culture` 設為金額受眾所在的地區，例如德國發票使用 `de-DE`。

<Demo name="Culture" />

### 錯誤與輔助文字

`Description` 在欄位下方提供輔助文字。`ErrorMessage` 會把欄位標記為無效，並以該訊息取代輔助文字。未設定明確訊息且金額超出範圍時，會顯示內建的範圍錯誤。

<Demo name="DescriptionAndError" />

### 繫結與事件

`Value` 是雙向繫結，使用主單位，因此 `12.50` 即為 `12.5`。`Currency` 同樣是雙向繫結。`ValueChanged` 回報舊值與新值、貨幣、最小單位值、格式化字串以及 `ChangeSource`。`CurrencyChanged` 回報舊代碼與新代碼，以及是選單還是代碼修改了它。

<Demo name="BindingAndEvents" />

```xml
<arc:MoneyInput Label="金額" Value="{Binding Amount}" Currency="{Binding Currency}" />
```

```csharp
amount.ValueChanged += (_, e) =>
{
    Console.WriteLine($"{e.Formatted} ({e.Currency}), minor units {e.MinorUnits}, from {e.ChangeSource}");
};

amount.OpenCurrencyMenu();
```

`OpenCurrencyMenu` 需要 `Currencies` 中至少有兩個代碼；`CloseCurrencyMenu` 會關閉選單並把焦點返回到按鈕。`IsCurrencyMenuOpen` 回報選單的狀態。

### 繫結到檢視模型

檢視模型以 `decimal?` 保存主單位的金額，並以 ISO 代碼保存貨幣。只在與支付服務的邊界處保存最小單位值。

```csharp
public partial class RefundViewModel : ObservableValidator
{
    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Required(ErrorMessage = "請輸入退款金額。")]
    [Range(typeof(decimal), "0.01", "120", ErrorMessage = "請輸入 $0.01 到 $120.00 之間的金額。")]
    private decimal? _amount = 84.50m;

    [ObservableProperty]
    private string _currency = "USD";

    [RelayCommand]
    private async Task RefundAsync()
    {
        ValidateAllProperties();
        if (HasErrors || Amount is not { } major)
        {
            return;
        }
        long minorUnits = decimal.ToInt64(major * 100m);
        await _payments.RefundAsync("ch_2026_0412", minorUnits, Currency);
    }
}
```

```xml
<arc:MoneyInput Label="退款金額"
                Value="{Binding Amount, Mode=TwoWay}"
                Currency="{Binding Currency, Mode=TwoWay}"
                Minimum="0.01"
                Maximum="120" />
```

- `Value` 與 `Currency` 預設為雙向繫結，因此檢視模型會收到每一次變化，包括從選單做出的更改。
- 依賴變化來源的副作用應使用 `ValueChanged`。檢視模型的程式化指派會觸發 `ValueChanged`，其 `ChangeSource` 為 `Programmatic`，且不做任何播報，也不產生推動效果。

### 最小值與最大值

`Minimum` 與 `Maximum` 以主單位設定範圍。輸入超過最大值會被拒絕：金額向按下的方向推動，範圍文字閃爍，並播報該上限。最小值在欄位失去焦點時檢查，因此使用者可以先輸入較短的數值，再逐步輸入更大的數值。

<Demo name="Limits" />

### 驗證

在使用者能夠處理某條規則的時刻檢查它。欄位負責範圍，其餘規則由表單決定金額的意義。

| 規則 | 何時檢查 | 顯示在哪裡 |
|---|---|---|
| 超過 `Maximum` | 輸入時，使用者鍵入時即被拒絕 | 範圍文字閃爍並播報上限；數值不變 |
| 低於 `Minimum` | 欄位失去焦點時 | 顯示 `ErrorMessage`，內容為「最小值為 $5.00」（即 `MinimumErrorFormat` 文字），外框變為無效狀態 |
| 必填或業務規則 | 送出時，由檢視模型檢查 | `ErrorMessage`；明確的訊息會取代內建的範圍錯誤 |
| 伺服器端規則，例如卡片限額 | 請求結束後 | 欄位上的 `ErrorMessage`；金額變動時清除 |

把 `ErrorMessage` 設為 `null` 即可移除明確的訊息，並重新顯示內建的範圍錯誤。內建文字來自 `MinimumErrorFormat` 與 `MaximumErrorFormat`，範圍文字來自 `RangeFormat`（"{0} to {1}"）、`MaximumOnlyFormat`（"Up to {0}"）與 `MinimumOnlyFormat`（"At least {0}"）。可以依產品的措辭修改它們，但要保留 `{0}` 佔位符。

### 隱藏標籤

`IsLabelVisible="False"` 會隱藏標籤，但仍保留給螢幕閱讀器。只有當欄位上方的標題已經為金額命名時，才這樣做。

<Demo name="HiddenLabel" />

### 停用

停用的欄位會保留其金額，並忽略輸入。請在輔助文字中說明金額被鎖定的原因，例如「發票寄出後鎖定」。

<Demo name="Disabled" />

### 範例：帳單主控台中的退款表單

客服在帳單主控台中為一筆付款退還一部分金額。退款欄位在外殼下方顯示範圍（“$5.00 to $120.00”），因此客服在輸入前就能看到上限。兩個快速新增標籤（+$10 與 +$25）可以讓客服快速加上常用金額。結算金額欄位為空，並帶有明確的錯誤，說明缺少什麼以及去哪裡查找，因為銀行尚未確認該金額。輔助文字說明了後果（「3 到 5 個工作天內退回銀行卡」），這正是使用者需要閱讀該欄位的原因。

![含有有效金額、範圍文字、快速新增標籤以及空結算金額錯誤的退款表單](images/RefundForm.png)

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc"
             xmlns:generic="using:System.Collections.Generic">
    <StackPanel Width="620" Spacing="20">
        <TextBlock Text="Refund for payment ch_2026_0412"
                   FontSize="14"
                   FontWeight="SemiBold"
                   Foreground="{arc:ArcDesignTokenResource Foreground}" />
        <arc:MoneyInput Label="Refund amount"
                        Value="84.5"
                        Currency="USD"
                        Minimum="5"
                        Maximum="120"
                        Description="Returned to the card in 3 to 5 business days.">
            <arc:MoneyInput.QuickAddAmounts>
                <generic:List x:TypeArguments="x:Decimal">
                    <x:Decimal>10</x:Decimal>
                    <x:Decimal>25</x:Decimal>
                </generic:List>
            </arc:MoneyInput.QuickAddAmounts>
        </arc:MoneyInput>
        <arc:MoneyInput Label="Settlement amount"
                        Currency="EUR"
                        Culture="de-DE"
                        ErrorMessage="Enter the amount the bank confirmed on the settlement report." />
        <StackPanel Orientation="Horizontal" Spacing="8" HorizontalAlignment="Right">
            <arc:Button Content="Cancel" Variant="Secondary" />
            <arc:Button Content="Issue refund" />
        </StackPanel>
    </StackPanel>
</UserControl>
```

這兩個欄位共用一欄與同一種標籤樣式，因此視覺上它們屬於同一個表單。第二個欄位的錯誤使用明確的訊息，它取代了內建的範圍錯誤，因為該欄位為空，不適用範圍錯誤。

### 文案規範

- **標籤**：用名詞片語說明金額的角色，例如「退款金額」「小費」「每月限額」。在一個有多個金額的畫面上，不要只寫「金額」。
- **輔助文字**：說明後果或限制，例如「3 到 5 個工作天內退回銀行卡」「最多為已付金額」。數字用阿拉伯數字書寫。
- **錯誤提示**：說明哪裡有問題以及應該輸入什麼，用詞與輔助文字的表述保持一致，例如「請輸入 $0.01 到 $120.00 之間的金額。」避免使用「金額無效」。
- **操作按鈕**：按鈕用動詞加對象說明結果，例如「發起退款」「儲存限額」。不要寫「送出」。

| 不要寫 | 改為 |
|---|---|
| 金額無效 | 請輸入 $0.01 到 $120.00 之間的金額。 |
| 金額 | 退款金額 |
| 數值太大！ | 最大值為 $120.00。 |
| 送出 | 發起退款 |

### 最佳做法

**建議**

- 根據業務規則設定 `Minimum` 與 `Maximum`，這樣欄位會拒絕不可能的輸入，範圍文字也能在使用者輸入前告知區間。
- 把 `MinorUnits` 儲存並傳送給支付介面，檢視模型中的 `Value` 保持主單位，用於顯示。
- 為畫面的受眾設定 `Culture`，而不是使用作業系統的地區文化，這樣同一個金額在每台機器上的讀法都一致。
- 把標籤限制在兩到三個與該畫面工作相符的值，標籤過多會擠占這一列。
- 只有當金額可能更改貨幣時才顯示貨幣選單。單一代碼以靜態文字顯示是有原因的。

**避免**

- 把 `Value` 轉換為 `double` 或 `float`。decimal 型別能精確保留角分，而二進位浮點數不能。
- 把 `MoneyInput` 用於數量、計數或百分比。貨幣符號和角分會誤導使用者。
- 在輸入第一個字元時就對最小值報錯。最小值在失去焦點時檢查，讓使用者先輸完內容。
- 金額修正後仍保留過時的 `ErrorMessage`。請把它設為 `null`，讓內建的範圍錯誤或輔助文字重新出現。
- 在金融畫面上依賴目前的地區文化。機器的地區設定變化會改變分組與符號。

### 常見問題

**為什麼空白欄位顯示 0.00？** 那是幽靈數字，用來標示角分的位置。此時 `Value` 為 `null`，`FormattedValue` 為空字串，因此應檢查 `Value` 是否為 `null`，而不是檢查顯示的數字。

**如何取得支付介面所需的金額？** 讀取 `MinorUnits`（型別為 `long?`）。對於 USD，它是分；對於 JPY，它是日圓。欄位為空時其值為 `null`，因此應在送出請求前進行驗證。

**為什麼貨幣名稱是英文的？** 欄位使用內建的 ISO 4217 代碼表與英文名稱，因為 .NET 並未為所有貨幣提供在地化的顯示名稱。符號與數字格式仍遵循 `Culture`。

**為什麼輸入超過最大值時金額會移動？** 這次拒絕是一次推動：金額向按下的方向滑動後彈回，讓使用者看到這次更改被拒絕了。減少動態效果時取消推動，但範圍文字仍會閃爍。

**更改貨幣時金額會怎樣？** 主單位金額保持不變（12.5 仍為 12.5），最小單位值依新貨幣重新計算。從選單更改時，先觸發 `CurrencyChanged`（來源為 `Menu`），再觸發 `ValueChanged`，其 `ChangeSource` 為 `Currency`。

## API 參考

<!-- api: MoneyInput -->

事件參數與變化來源是事件所使用的公用型別。

| 型別 | 成員 |
|---|---|
| `MoneyInputValueChangedEventArgs` | `OldValue`、`NewValue`（decimal?）、`Currency`、`MinorUnits`（long?）、`Formatted`、`ChangeSource` |
| `MoneyInputCurrencyChangedEventArgs` | `OldCurrency`、`NewCurrency`、`ChangeSource` |
| `MoneyInputChangeSource` | `Programmatic`、`Type`、`Paste`、`Key`、`Chip`、`Currency`、`Automation` |
| `MoneyInputCurrencyChangeSource` | `Programmatic`、`Menu` |

## 無障礙

- `Label` 為欄位命名。`IsLabelVisible="False"` 時標籤隱藏，但仍作為名稱。
- 欄位是一個微調按鈕。其數值為主單位數值，值文字為完整的貨幣字串，例如「$12.50」；沒有金額時為「空」。範圍顯示為微調按鈕的最小值與最大值。
- 輔助文字、範圍與錯誤合併為欄位的說明文字，在名稱和值之後朗讀。
- 按鈕名稱為「Currency, US Dollar」，它會開啟一個支援鍵入尋找的清單。
- 被拒絕的修改（「最大值為 $100.00」）、鍵盤與標籤步進以及貨幣更改都會以禮貌方式播報。鍵入不做播報，因此螢幕閱讀器不會逐個重複數字。
- 帶動畫的數字對輔助技術隱藏，輔助技術讀取欄位的真實文字。

### 鍵盤

| 按鍵 | 操作 |
|---|---|
| 數字與小數分隔符 | 輸入金額；字母會被捨棄，游標保持不動 |
| `Up` / `Down` | 增加或減少一個主單位，或 `Step` 設定的值 |
| `Shift` + `Up` / `Down`，`PageUp` / `PageDown` | 步進幅度為十倍 |
| `Enter` | 確定小數位（12.5 變為 12.50），並把游標移到末尾 |
| 在分組分隔符上按 `Backspace` / `Delete` | 刪除分隔符旁邊的數字，而不是什麼都不做 |
| `Ctrl+V`（macOS 上為 `Cmd+V`） | 貼上 "$1,234.56"、"1.234,56 €" 或 "1234.5" 之類的值；整個欄位被選取時取代，否則在游標處插入 |
| `Tab` | 把焦點移到下一個控制項 |
| 貨幣按鈕上的 `Down` / `Up` | 開啟貨幣選單 |
| 選單中的 `Up` / `Down`、`Home` / `End` | 在貨幣之間移動反白 |
| 選單中的字母 | 跳到代碼或名稱以所鍵入文字開頭的貨幣（600 ms 後重置鍵入尋找） |
| 選單中的 `Enter` / `Space` | 選擇反白的貨幣，並保留主單位金額 |
| 選單中的 `Escape` | 關閉選單，並把焦點返回到貨幣按鈕 |
| 選單中的 `Tab` | 關閉選單，並把焦點移到下一個控制項 |

`Enter` 會確定小數位，但不會送出表單。按鍵仍會傳遞給表單，因此預設按鈕可以送出它。

## 動態效果

- 步進或快速新增標籤改變金額時，數字沿變化方向滾動。在完整動態效果下，使用者鍵入的數字會立即定位，因此游標永遠不會落後於按鍵。
- 被拒絕的修改會以彈簧效果把金額橫向推動，並使範圍文字閃爍。
- 更改貨幣時，符號滾動到新的位置；過長的金額會縮放以適應寬度。

| 變化 | 完整 | 減少 | 關閉 |
|---|---|---|---|
| 步進按鍵或快速新增標籤 | 數位欄沿變化方向滾動 | 數字淡入，尺寸直接跳變 | 數字淡入，尺寸直接跳變 |
| 被拒絕的修改（超過最大值） | 金額以彈簧效果橫向推動；範圍文字閃爍 | 無推動；範圍文字仍閃爍 | 無推動；範圍文字仍閃爍 |
| 出現新的數位欄 | 欄寬以彈簧效果增長 | 寬度直接跳變 | 寬度直接跳變 |
| 更改貨幣 | 符號滾動到新位置 | 符號直接變化，無滾動 | 符號直接變化，無滾動 |

## 相關元件

- `NumberField`：用於數量及其他不涉及貨幣的數字。
- `ScrubInput` 用於拖曳調整的數值設定，`Slider` 用於從範圍中選取。
- `Input` 用於沒有金額結構的自由文字。

| 元件 | 適用情境 |
|---|---|
| `NumberField` | 數字是數量、計數或帶單位的值，而不是金額 |
| `ScrubInput` | 在精簡面板中透過拖曳調整數字設定 |
| `Slider` | 使用者在範圍內選取一個值，精確數字不那麼重要 |
| `Input` | 值是識別碼或自由文字，只是恰好含有數字 |
| `Select` / `Combobox` | 使用者從清單中選擇一個帶價格的項目或記錄 |
