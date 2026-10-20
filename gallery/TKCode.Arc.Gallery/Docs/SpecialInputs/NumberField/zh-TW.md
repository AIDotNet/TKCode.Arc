---
title: 數字輸入框
summary: 透過清楚的增減控制項輸入有範圍限制的數字。
source-hash: ff28c2fe4eb2
---

## 何時使用

- 帶範圍的精確數字，例如座位數、數量或價格。
- 使用者需要逐步增減的值，並且單位或前綴隨數字一起顯示。
- 開啟 `IsScrubEnabled` 後，透過拖曳標籤來調整數值。

## 何時不用

- 近似值，且位置比精確數字更重要：用 `Slider`。
- 電話號碼或代碼等數字字串：用 `Input`。
- 帶貨幣代碼的金額：用 `MoneyInput`。
- 需要拖曳調整多個數值的畫布式屬性面板：用 `ScrubInput`。

## 用法

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:NumberField Label="座位數"
                     Minimum="1"
                     Maximum="10"
                     Value="3"
                     Description="每個團隊最多 10 個。" />
</UserControl>
```

### 前綴、後綴與步長

`Step` 設定增減幅度，其精度決定預設的小數位數。`SuffixFormatter` 可讓單位隨數值變化，例如單數與複數形式。

```xml
<arc:NumberField Label="價格" Prefix="NT$" Step="0.5" Maximum="999" Value="24.5" />
<arc:NumberField Label="溫度" Minimum="-20" Maximum="40" Value="21" Suffix="°C" />
```

```csharp
var seats = new NumberField
{
    Label = "座位數",
    Minimum = 1,
    Maximum = 10,
    Value = 3,
    SuffixFormatter = n => n == 1 ? " 個座位" : " 個座位"
};
```

最小值小於零時，使用者輸入時即可接受負號。

### 尺寸

`Size` 設定寬度與微調按鈕的尺寸：`Small` 寬 164px，`Medium`（預設）寬 196px，`Large` 寬 228px。

```xml
<arc:NumberField Label="小" Size="Small" Value="2" Maximum="99" />
<arc:NumberField Label="大" Size="Large" Value="48" Maximum="99" />
```

### 限值提示與錯誤

`IsLimitHintEnabled` 在達到限值時顯示提示。`LimitHintFormatter` 負責產生提示文字。`ErrorMessage` 會將欄位標記為無效。

```xml
<arc:NumberField Label="數量" Value="0" Maximum="5" ErrorMessage="請至少新增一項。" />
```

```csharp
var guests = new NumberField
{
    Label = "賓客",
    Minimum = 1,
    Maximum = 8,
    Value = 8,
    LimitHintFormatter = (edge, limit) => edge == NumberFieldLimitEdge.Maximum
        ? $"容量為 {limit}"
        : $"至少 {limit}"
};
```

### 繫結與事件

`Value` 為雙向繫結。`ValueChanged` 會回報舊值、新值以及 `ChangeSource`。

```xml
<arc:NumberField Label="項目" Minimum="0" Maximum="50" Value="{Binding ItemCount, Mode=TwoWay}" />
```

```csharp
field.ValueChanged += (_, e) =>
{
    Console.WriteLine($"{e.OldValue} -> {e.NewValue} ({e.ChangeSource})");
};
```

## API 參考

<!-- api: NumberField -->

## 無障礙

- `Label` 為欄位命名。`IsLabelVisible="False"` 時標籤隱藏，但仍作為名稱。
- 欄位是一個微調按鈕。其值文字包含前綴與後綴，微調按鈕分別命名為 "增加 <label>" 與 "減少 <label>"。
- ArrowUp 與 ArrowDown 逐步調整，Shift 或 PageUp 與 PageDown 使用大步長，Home 與 End 跳到限值。
- 修改會以禮貌方式播報。被限制到邊界時，會播報所達到的限值。
- 焦點環僅在鍵盤聚焦時顯示。

## 動態效果

- 數字沿變化方向像里程表一樣滾動。按住微調按鈕時會連續重複並逐漸加速。
- 在限值處按下會將數值回彈，抖動微調按鈕，並顯示簡短的限值提示。
- 減少動態效果時，數值立即設定，並以警告顏色閃爍限值。關閉動態效果時同樣如此。

## 相關元件

- `Slider`：當範圍中的位置比精確數字更重要時使用。
- `Input`：沒有微調按鈕的數字字串。
- `MoneyInput`：貨幣金額；`ScrubInput`：屬性面板中密集的數值調整。
