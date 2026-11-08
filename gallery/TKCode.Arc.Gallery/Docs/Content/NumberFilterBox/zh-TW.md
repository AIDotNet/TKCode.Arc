---
title: 數字篩選框
summary: 一個數字篩選條件：以核取方塊啟用，運算子以符號顯示，再填一個值，可設比較容差。
source-hash: d8035c1b0666
---

<Demo name="Basic" />

## 何時使用

`NumberFilterBox` 是進階篩選中的一列：啟用條件的核取方塊、欄位名稱、以符號顯示的運算子（= ≠ < ≤ > ≥）和值。在使用者需要對清單的數值欄位設定條件時使用：

- **商品清單中的價格、評分與量測值**：「價格 ≤ 1000」。
- **庫存或記錄檢視中的計數**：「庫存 < 50」、「錯誤 > 0」。
- **同時設多個條件**：與 `TextFilterBox` 疊放在篩選面板中，每個條件各自啟用或關閉。

篩選框只保存條件，從不過濾資料：由檢視模型讀取 `IsActive`、`Operator` 和 `Value`，再依合適的方式過濾（LINQ、資料庫查詢、遠端 API，即時或按下按鈕後）。

## 何時不用

- **快速切換的分面**（「有庫存」「特價中」）：使用 `ChipGroup` 或 `FilterToolbar`。
- **有上下兩端的範圍**：使用範圍模式的 `Slider`，或兩個篩選框（「≥ 10」和「≤ 50」）。
- **表單裡的一般數字**：使用 `NumberField`。

## 用法

`NumberFilterBox` 衍生自 `FilterBox`（一個 Avalonia `TemplatedControl`）。它的各部分都是 Arc 控制項：帶標籤和說明的 `Checkbox`、選擇運算子的 `Select` 和輸入值的 `NumberField`。狀態以雙向繫結：

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:NumberFilterBox Label="Price"
                         Description="Products at or above this price."
                         Operator="GreaterThanOrEqual"
                         Value="100"
                         NumberMode="Decimal"
                         DecimalPlaces="2"
                         Minimum="0"
                         Maximum="99999"
                         Tolerance="0.01" />
</UserControl>
```

`IsActive`、`Operator` 和 `Value` 預設就是雙向繫結，`{Binding PriceActive}` 這類繫結不寫 `Mode=TwoWay` 也會寫回。

### 整數與小數

`NumberMode` 為 `Integer`（整數）或 `Decimal`（預設，小數位數由 `DecimalPlaces` 決定，預設 2）。`SmallChange` 是方向鍵和步進按鈕的一步，也是輸入值對齊的格線（從 `Minimum` 起算）；預設整數為 1，小數為最後一位小數的一個單位。`LargeChange` 是 Page Up、Page Down 和 Shift+方向鍵的步長。`Minimum` 預設為 0：需要負數時請設定它。

`Tolerance` 屬於條件而不屬於輸入框：相差不超過 `Tolerance` 的兩個值視為相等。它不影響能輸入什麼。

<Demo name="Modes" />

### 關閉篩選

取消勾選（或把 `IsActive` 設為 `False`）會停用運算子、值和 `ExtraContent`，但保留它們：再次勾選即恢復原來的條件。核取方塊本身始終可用，點選標籤也能切換。

<Demo name="Active" />

### 限定運算子

`Operators` 把選單限定為一份清單，順序由你決定。符號及其朗讀名稱來自 `FilterOperators`，它是一張一般的對照表，不使用反射，因此相容 AOT 與裁剪。條件有任何變化後都會引發 `FilterChanged`。

<Demo name="Operators" />

### 在檢視模型中過濾

`Condition` 是目前的條件，型別為 `NumberFilterCondition` 記錄：運算子、值和容差。它不含介面，檢視模型可以用自己的屬性建構一個，再用 `Matches` 逐項判斷。篩選關閉時，篩選框上的 `Matches` 一律回傳 true。

```csharp
var price = new NumberFilterCondition(PriceOperator, PriceValue, Tolerance: 0.01m);
var visible = products.Where(p => !PriceActive || price.Matches(p.Price));
```

容差放寬了相等：`Equal` 接受容差以內的值，`NotEqual` 接受容差以外的值，`<` 和 `>` 必須超出容差，`≤` 和 `≥` 允許差一個容差。值的型別是 `decimal`；`double` 請用 `(decimal)value` 轉換。

<Demo name="Combined" />

### 對齊疊放的篩選框

為每個篩選框設相同的 `LabelSizeGroup`，並在容器上設 `Grid.IsSharedSizeScope="True"`：標籤欄取最長標籤的寬度，運算子因此對齊。也可以用 `LabelWidth` 設固定寬度。`Size` 設定運算子與值輸入框的高度（Small 36、Medium 44、Large 50）。

```xml
<StackPanel xmlns="https://github.com/avaloniaui"
            xmlns:arc="https://github.com/AIDotNet/TKCode.Arc"
            Grid.IsSharedSizeScope="True" Spacing="8">
    <arc:NumberFilterBox Label="Price" LabelSizeGroup="FilterLabel" />
    <arc:TextFilterBox Label="Product name" LabelSizeGroup="FilterLabel" />
</StackPanel>
```

## API 參考

<!-- api: NumberFilterBox -->

<!-- api: NumberFilterCondition -->

<!-- api: NumberOperator -->

<!-- api: NumberFilterMode -->

## 協助工具

- 核取方塊以標籤命名，以 `Description` 作說明；按空白鍵切換。
- 運算子欄位命名為「Price operator」（標籤加 `OperatorText`），說明文字是運算子的名稱，如「Greater than or equal to」；同樣的名稱也顯示為工具提示。
- 值輸入框以標籤命名。

### 鍵盤

| 按鍵 | 時機 | 動作 |
|---|---|---|
| Tab | 任意位置 | 在核取方塊、運算子和值之間移動（已停用的部分會略過） |
| 空白鍵 | 核取方塊 | 啟用或關閉篩選 |
| Enter、空白鍵、上、下 | 運算子 | 開啟選單；之後的移動與選擇同 `Select` |
| 符號鍵 | 運算子 | 跳到該符號對應的運算子 |
| 上、下 | 值 | 一個 `SmallChange` |
| Page Up、Page Down | 值 | 一個 `LargeChange` |

## 動態效果

- 各部分沿用各自控制項的動態效果：勾選標記自行繪出，運算子選單從欄位下方展開，值的數字滾動。整列本身沒有動畫。
- 減少動態效果或關閉動態效果時，各部分依各自的減弱方式呈現。

## 響應式

- 運算子 84 px，值 168 px。標籤欄與標籤同寬（或取 `LabelWidth`，或取其尺寸群組的寬度），過長的標籤或說明會換行，為輸入框留出空間。
- 連 120 px 的標籤都放不下時（約 400 px 以下），標籤移到輸入框上方單獨一行，此時該篩選框不再使用共用的 `LabelSizeGroup`。固定的 `LabelWidth` 放不下時同樣如此。
- 所有輸入框至少 36 px 高（`Size="Small"`），核取方塊在任何尺寸下都保留 44 px 的點擊區域。

## 相關元件

- `TextFilterBox`：同樣的一列，用於文字，支援包含、開頭、結尾、等於和規則運算式。
- `FilterToolbar`：清單上方幾個快速分面的標籤。
- `NumberField`：表單中的數字，不帶運算子。
