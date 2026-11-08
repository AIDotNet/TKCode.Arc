---
title: 文字篩選框
summary: 一個文字篩選條件：以核取方塊啟用，可選包含、開頭、結尾、等於或規則運算式，並可區分大小寫。
source-hash: 593051d50c97
---

<Demo name="Basic" />

## 何時使用

`TextFilterBox` 是針對文字欄位的進階篩選中的一列：啟用條件的核取方塊、欄位名稱、以符號顯示的運算子和篩選文字。在使用者需要依某個欄位搜尋清單、且需要比搜尋框更多控制時使用：

- **商品或文件清單中的名稱與標題**：「名稱 ∋ Pro」。
- **檔案與路徑**：「檔名 $ .axaml」、「路徑 ^ src/」。
- **符合某種模式的記錄行與代碼**：「訊息 .* ^ERR\d+」。

篩選框只保存條件，從不過濾資料：由檢視模型讀取 `IsActive`、`Operator`、`Text` 和 `IsCaseSensitive`，再依合適的方式過濾。

## 何時不用

- **對所有內容的一次搜尋**：使用 `SearchField`。
- **從已知值中挑選**：使用 `Select`、`MultiSelect` 或 `ChipGroup`。
- **數字**：使用 `NumberFilterBox`。

## 用法

`TextFilterBox` 衍生自 `FilterBox`（一個 Avalonia `TemplatedControl`）。它的各部分都是 Arc 控制項：帶標籤和說明的 `Checkbox`、選擇運算子的 `Select` 和輸入文字的 `Input`，後者佔據剩餘寬度。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:TextFilterBox Label="Product name"
                       Operator="Contains"
                       Text="Keyboard"
                       Placeholder="Type a product name"
                       MaxLength="80"
                       IsCaseSensitive="False" />
</UserControl>
```

`IsActive`、`Operator`、`Text` 和 `IsCaseSensitive` 預設就是雙向繫結。運算子有 `Contains`（∋，預設）、`NotContains`（∌）、`Equal`（=）、`NotEqual`（≠）、`StartsWith`（^）、`EndsWith`（$）和 `Regex`（.*）。空白文字不構成任何限制。`Clear()` 清空文字並保留運算子。

### 規則運算式

`Operator="Regex"` 時，文字是一個 .NET 規則運算式。無法剖析的模式會在輸入框下方顯示剖析器的訊息，把 `IsPatternValid` 設為 `False`、`PatternError` 設為該訊息，並加上 `:invalid` 虛擬類別；修正之前，該條件不構成任何限制。模式由規則運算式直譯器執行（執行階段不編譯也不產生程式碼，因此相容 Native AOT），一次比對超過 `MatchTimeout`（200 ms）即視為不符合。

<Demo name="Regex" />

### 附加內容

`ExtraContent` 放在輸入框之後，例如一個「區分大小寫」核取方塊或一個按鈕。它隨篩選一起停用，為 null 時不佔空間。

<Demo name="ExtraContent" />

### 在檢視模型中過濾

`Condition` 是目前的條件，型別為 `TextFilterCondition`：在自己的屬性變化時建構一個（不要每項建構一次，這樣模式只剖析一次），再用 `Matches` 逐項判斷。比較依序數進行，可區分或不區分大小寫。篩選關閉時，篩選框上的 `Matches` 一律回傳 true。

```csharp
var name = new TextFilterCondition(NameOperator, NameText, NameCaseSensitive);
var visible = products.Where(p => !NameActive || name.Matches(p.Name));
```

<Demo name="NumberFilterBox.Combined" />

### 對齊疊放的篩選框

為每個篩選框設相同的 `LabelSizeGroup`，並在容器上設 `Grid.IsSharedSizeScope="True"`：數字與文字篩選框共用一欄標籤，運算子因此對齊。也可以用 `LabelWidth` 設固定寬度。

<Demo name="Alignment" />

## API 參考

<!-- api: TextFilterBox -->

<!-- api: FilterBox -->

<!-- api: TextFilterCondition -->

<!-- api: TextOperator -->

<!-- api: FilterOperators -->

## 協助工具

- 核取方塊以標籤命名，以 `Description` 作說明；按空白鍵切換。
- 運算子欄位以標籤加 `OperatorText` 命名（「Product name operator」），說明文字是運算子的名稱，如「Starts with」；同樣的名稱也顯示為工具提示。
- 文字輸入框以標籤命名；無效的模式是它的錯誤訊息，會隨輸入框一起朗讀。

### 鍵盤

| 按鍵 | 時機 | 動作 |
|---|---|---|
| Tab | 任意位置 | 在核取方塊、運算子、文字和附加內容之間移動 |
| 空白鍵 | 核取方塊 | 啟用或關閉篩選 |
| Enter、空白鍵、上、下 | 運算子 | 開啟選單；之後的移動與選擇同 `Select` |
| 輸入 | 文字 | 編輯篩選文字 |

## 動態效果

- 各部分沿用各自控制項的動態效果：勾選標記自行繪出，運算子選單從欄位下方展開，錯誤訊息在輸入框下方滑入。整列本身沒有動畫。
- 減少動態效果或關閉動態效果時，各部分依各自的減弱方式呈現。

## 響應式

- 篩選框延展到給定寬度。84 px 的運算子保持寬度，文字輸入框佔據其餘部分，最窄 160 px。過長的標籤或說明會換行，為它們留出空間。
- 連 120 px 的標籤都放不下時（約 390 px 以下），標籤移到輸入框上方單獨一行，此時該篩選框不再使用共用的 `LabelSizeGroup`。固定的 `LabelWidth` 放不下時同樣如此。
- `ExtraContent` 在列尾佔用自身的寬度。
- 所有輸入框至少 36 px 高（`Size="Small"`），核取方塊在任何尺寸下都保留 44 px 的點擊區域。

## 相關元件

- `NumberFilterBox`：同樣的一列，用於數字，支援 = ≠ < ≤ > ≥ 和容差。
- `SearchField`：整份清單的一個搜尋框。
- `FilterToolbar`：清單上方幾個快速分面的標籤。
