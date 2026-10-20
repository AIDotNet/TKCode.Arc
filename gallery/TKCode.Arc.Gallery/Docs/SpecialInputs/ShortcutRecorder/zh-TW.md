---
title: 快速鍵錄入
summary: 將按鍵組合錄入為鍵帽，支援衝突警告、Kbd 與可搜尋的速查表。
source-hash: c8b8d9279103
---

## 何時使用

- 允許使用者重新對應操作的鍵盤快速鍵設定。
- 顯示某個操作目前綁定之快速鍵的提示。
- 鍵盤操作是主要路徑的桌面與網頁應用程式。

## 何時不用

- 快速鍵固定、只需顯示的情境：請使用你自己的文字呈現。
- 以名稱執行操作：請改用命令面板模式。
- 僅支援觸控的產品：沒有可錄入的實體鍵盤。

## 用法

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:ShortcutRecorder Label="命令面板"
                          Description="在應用程式的任何位置開啟命令面板。" />
</UserControl>
```

點擊欄位或按 Enter，然後按下組合鍵。錄入器會儲存一個 `KeyGesture`。

### 值與重設

`Value` 保存錄入的 `KeyGesture`，沒有快速鍵時為 `null`。`ResetValue` 是重設按鈕還原的值。範例中它被設為預設快速鍵。

```csharp
var palette = new ShortcutRecorder
{
    Label = "命令面板",
    Value = ShortcutFormatter.Parse("mod+k", ShortcutPlatform.Other),
    ResetValue = ShortcutFormatter.Parse("mod+k", ShortcutPlatform.Other)
};
```

`mod` 在 macOS 上代表 Command，在其他平台上代表 Control。使用 `ShortcutFormatter.Parse` 或 `TryParse` 可依據該文字建立手勢。

### 已知快速鍵與衝突

`Bindings` 列出已在使用的快速鍵。錄入其中一個時，會出現衝突提示，並帶有「仍要使用」與「取消」按鈕。「仍要使用」會取代原有綁定，並在事件中回報。

```csharp
var quickSearch = new ShortcutRecorder { Label = "快速搜尋" };
quickSearch.Bindings.Add(new ShortcutBinding(ShortcutFormatter.Parse("mod+s", ShortcutPlatform.Other), "儲存"));
quickSearch.Bindings.Add(new ShortcutBinding(ShortcutFormatter.Parse("mod+f", ShortcutPlatform.Other), "尋找"));
```

關閉分頁或複製等保留組合同樣會發出警告。將 `IsReservedWarningEnabled` 設為 `False` 即可關閉此警告。

### 修飾鍵與平台

`IsModifierRequired="False"` 允許單一按鍵。功能鍵一律可用。`Platform` 設定鍵帽樣式為 `Mac` 或 `Other`。預設情況下，錄入器使用目前執行的平台。

```xml
<arc:ShortcutRecorder Label="下一個對話" IsModifierRequired="False" />
<arc:ShortcutRecorder Label="Mac" Platform="Mac" />
```

### 事件

`ValueChanged` 會回報舊手勢與新手勢。按下「仍要使用」之後，`Replaced` 會保存被取代的綁定。

```csharp
recorder.ValueChanged += (_, e) =>
{
    var replaced = e.Replaced is { } binding ? $" (replaced {binding.Label})" : string.Empty;
    Console.WriteLine($"{e.OldValue} -> {e.NewValue}{replaced}");
};
```

## API 參考

<!-- api: ShortcutRecorder -->

## 無障礙

- 錄入器是一個切換按鈕，名稱由其標籤與朗讀的快速鍵組成，例如 "Command Shift K"。
- 錄入狀態、結果、衝突與清除都會以禮貌方式播報。說明文字會隨欄位一併朗讀。
- 錄入器內的鍵帽對輔助技術隱藏，朗讀的名稱改由快速鍵本身提供。
- Enter 或 Space 開始錄入。Escape 取消錄入；Tab 會停止錄入並移動焦點，且不錄入 Tab。
- 錄入期間，按鍵不會觸發視窗快速鍵。

## 動態效果

- 鍵帽以輕微縮放與模糊彈出。組合鍵完成後，鍵帽依序落定。
- 組合鍵變更時，鍵帽滑動到新的位置。
- 錄入時，錄製指示點會脈衝閃爍。訊息列以高度彈簧展開與收合。
- 減少動態效果時，移除縮放、錯開與滑動，鍵帽與訊息僅淡入淡出。關閉動態效果時直接到最終狀態。

## 相關元件

- `Input` 用於非快速鍵的自由文字。
- `Button` 用於快速鍵所觸發的命令。
- `Field` 與 `FieldFrame` 可用於建立帶自訂編輯器的類似欄位。
