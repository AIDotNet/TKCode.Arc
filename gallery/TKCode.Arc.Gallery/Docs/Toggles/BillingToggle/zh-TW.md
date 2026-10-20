---
title: 計費切換
summary: 按月與按年切換，帶有節省標章，價格會滾動至新金額。
source-hash: 7cbb30d851d5
---

## 何時使用

- 定價頁頂端的計費週期切換。
- 價格隨所選週期或方案變動，並需要滾動至新金額的情境。
- 帶有節省說明的小型方案選擇，例如按年或終身。

## 何時不用

- 不會改變價格的檢視：使用 `SegmentedControl`。
- 需要說明文字或垂直排版的選項：使用 `RadioGroup`。
- 單一開啟或關閉的設定：使用 `Switch`。

## 用法

預設選項為「按月」與「按年」，「按年」帶有標章「省 20%」。將 `Value` 設為所選的選項。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:BillingToggle Value="yearly" />
</UserControl>
```

### 自訂選項

可以直接內嵌撰寫選項，也可以繫結 `ItemsSource`。`Badge` 會顯示在選項上。選取該選項時，`ActiveBadge` 會取代 `Badge`，因此說明可以原地變化，而不會移動周圍的內容。

```xml
<arc:BillingToggle Value="monthly" Size="Large">
    <arc:BillingToggleOption Value="monthly" Label="按月" />
    <arc:BillingToggleOption Value="yearly" Label="按年" Badge="省 20%" ActiveBadge="您可省 $48" />
    <arc:BillingToggleOption Value="lifetime" Label="終身" Badge="最划算" />
</arc:BillingToggle>
```

### 價格

`BillingPrice` 會顯示一個金額。`Currency` 預設為 `$`，`Decimals` 預設為 0。當 `WasAmount` 高於 `Amount` 時，會以刪除線繪製舊價格。`Period` 為金額後方的文字。

```xml
<arc:BillingPrice Amount="24" WasAmount="30" Period="每月，按年計費" />
```

```csharp
var toggle = new BillingToggle { Value = "yearly" };
var price = new BillingPrice { Amount = 24, WasAmount = 30, Period = "每月，按年計費" };

toggle.ValueChanged += (_, e) =>
{
    var yearly = e.NewValue == "yearly";
    price.Amount = yearly ? 24 : 30;
    price.WasAmount = yearly ? 30 : null;
    price.Period = yearly ? "每月，按年計費" : "每月";
};
```

### 尺寸與強調色

`Size` 設定選項高度：`Medium` 為 34，`Large` 為 42。`BadgeAccent` 會為選取的標章上色，預設為 Arc 的成功色。

```xml
<arc:BillingToggle Size="Large" Value="yearly" />
```

### 繫結

`Value` 為雙向，並與 `SelectedValue` 保持同步。

```xml
<arc:BillingToggle Value="{Binding BillingPeriod}" />
```

## API 參考

<!-- api: BillingToggle -->
<!-- api: BillingPrice -->
<!-- api: BillingToggleOption -->

## 無障礙

- 預設情況下，該群組的名稱為「計費週期」。設定 `AutomationProperties.Name` 可改用其他名稱。
- 每個選項都是一個單選按鈕。其名稱由標籤與目前的標章組成，例如「按年 省 20%」。
- 滑塊對輔助技術隱藏。`BillingPrice` 會朗讀為金額、舊價格與週期，例如「$24，原為 $30，每月」。
- Tab 會將焦點移至選取的選項。方向鍵會選取並聚焦前一個或下一個選項，並會循環。Home 與 End 分別選取第一個與最後一個選項。
- 鍵盤焦點會繪製 2px 的外框，並與控制項保持 2px 的間距。指標焦點則不顯示。

## 動態效果

- 滑塊以約 0.34 秒的臨界阻尼彈簧滑動到選取的選項下方，沒有回彈。
- 選取選項的標章採用強調色，其文字以模糊轉場切換。
- 價格會滾動至新金額。舊價格收合消失，週期文字在原地切換。
- 減少動態效果時，滑塊立即就位，未選取的選項不做按下縮放。關閉動態效果時效果相同。

## 相關元件

- `SegmentedControl`：不涉及價格、只切換內容的檢視。
- `RadioGroup`：需要說明文字或垂直排版的選項。
- `RadioCards`：需要價格、說明，以及不可用原因的方案。
- `Switch`：立即生效的單一設定。
