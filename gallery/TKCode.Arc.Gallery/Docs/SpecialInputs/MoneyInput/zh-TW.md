---
title: 金額輸入框
summary: 帶有即時分組、寬度穩定、數字滾動與最小單位輸出的貨幣輸入框。
source-hash: f28fa88adbe5
---

<Demo name="Default" />

## 何時使用

- 使用者手動輸入的金額，例如付款、小費、轉帳與預算。
- 使用者可以更改的貨幣，例如發票或報支單。
- 需要最小單位值（分、日圓）用於儲存或支付介面的金額。

## 何時不用

- 數量及其他非貨幣數字：用 `NumberField`。
- 從一個範圍中選取金額：用 `Slider`。
- 透過拖曳調整數值的精簡屬性面板：用 `ScrubInput`。
- 外觀像數字的自由文字，例如帳戶代碼：用 `Input`。

## 用法

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:MoneyInput Label="金額"
                    Currency="USD"
                    Minimum="5"
                    Maximum="1000"
                    Description="方向鍵每次調整一個單位。" />
</UserControl>
```

### 貨幣與快速新增晶片

`Currencies` 設定貨幣選單中的代碼。只有一個代碼時，該代碼以靜態文字顯示。`QuickAddAmounts` 設定以主單位表示的晶片。清單為空時隱藏晶片。

日圓沒有最小單位，因此該欄位不顯示角分。

<Demo name="SingleCurrency" />

<Demo name="NoQuickAdd" />

<Demo name="NoDecimals" />

### 地區文化

`Culture` 設定分組方式與符號位置。預設值為 `en-US`。

<Demo name="Culture" />

### 錯誤與說明

`ErrorMessage` 不為空時，欄位標記為無效並取代說明文字。`Description` 提供輔助文字。

<Demo name="DescriptionAndError" />

### 繫結與事件

`Value` 為雙向繫結，使用主單位，因此 `12.50` 即為 `12.5`。`ValueChanged` 會回報舊值與新值、貨幣、最小單位、格式化字串以及 `ChangeSource`。

<Demo name="BindingAndEvents" />

```xml
<arc:MoneyInput Label="金額" Value="{Binding Amount, Mode=TwoWay}" Currency="{Binding Currency, Mode=TwoWay}" />
```

```csharp
amount.ValueChanged += (_, e) =>
{
    Console.WriteLine($"{e.Formatted} ({e.Currency}), minor units {e.MinorUnits}, from {e.ChangeSource}");
};

amount.OpenCurrencyMenu();
```

### 最小值與最大值

`Minimum` 與 `Maximum` 以主單位設定範圍。輸入超過最大值會被拒絕並推動金額，最小值則在欄位失去焦點時檢查。

<Demo name="Limits" />

### 隱藏標籤

`IsLabelVisible="False"` 會隱藏標籤，但仍保留給螢幕閱讀器。

<Demo name="HiddenLabel" />

### 停用

停用的欄位會保留其金額，並忽略輸入。

<Demo name="Disabled" />

## API 參考

<!-- api: MoneyInput -->

## 無障礙

- `Label` 為欄位命名。`IsLabelVisible="False"` 時標籤隱藏，但仍作為名稱。
- 欄位是一個微調按鈕。其值為主單位數值，值文字為完整的貨幣字串。
- ArrowUp 與 ArrowDown 每次調整一個單位，Shift 或 PageUp 與 PageDown 每次調整十個步進，Enter 完成小數輸入。
- 貨幣按鈕會開啟支援鍵入尋找的清單。方向鍵移動，Enter 或 Space 選定，Escape 返回按鈕。
- 範圍、提示與錯誤會被播報，並顯示為輔助文字。

## 動態效果

- 數字沿變化方向滾動，新增的位數會擴展寬度。鍵入的數字立即定位。
- 被拒絕的修改會使金額橫向推動，並閃爍範圍提示。更換貨幣時符號會滾動切換。
- 減少動態效果時，移除滾動、推動與寬度彈簧效果：數字淡入，尺寸直接跳轉。關閉動態效果時同樣如此。

## 相關元件

- `NumberField`：數量及其他不涉及貨幣的數字。
- `ScrubInput`：拖曳調整的數值設定；`Slider`：從範圍中選取。
- `Input`：沒有金額結構的自由文字。
