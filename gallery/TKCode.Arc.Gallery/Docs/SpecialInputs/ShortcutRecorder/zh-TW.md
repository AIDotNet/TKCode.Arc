---
title: 快速鍵錄入
summary: 將按鍵組合錄入為鍵帽，支援衝突警告、Kbd 與可搜尋的速查表。
source-hash: 71acd8b4e49f
---

<Demo name="Default" />

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

<Demo name="Empty" />

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

<Demo name="Conflicts" />

關閉分頁或複製等保留組合同樣會發出警告。將 `IsReservedWarningEnabled` 設為 `False` 即可關閉此警告。

<Demo name="Reserved" />

### 修飾鍵與平台

`IsModifierRequired="False"` 允許單一按鍵。功能鍵一律可用。`Platform` 設定鍵帽樣式為 `Mac` 或 `Other`。預設情況下，錄入器使用目前執行的平台。

<Demo name="NoModifier" />

<Demo name="Platforms" />

### 事件

`ValueChanged` 會回報舊手勢與新手勢。按下「仍要使用」之後，`Replaced` 會保存被取代的綁定。

<Demo name="Events" />

```csharp
recorder.ValueChanged += (_, e) =>
{
    var replaced = e.Replaced is { } binding ? $" (replaced {binding.Label})" : string.Empty;
    Console.WriteLine($"{e.OldValue} -> {e.NewValue}{replaced}");
};
```

### 隱藏標籤

`IsLabelVisible="False"` 會讓標籤僅供螢幕閱讀器使用。

<Demo name="HiddenLabel" />

### 說明文字

`Description` 是欄位下方的輔助文字。錄入提示、修飾鍵提醒與衝突提示在出現時會取代它。

<Demo name="Description" />

### 停用

停用的錄入器會變淡，顯示在柔和的背景上，並且不在 Tab 順序中。

<Demo name="Disabled" />

### 鍵盤

- Enter 或 Space 開始錄入。Escape 取消。Backspace 或 Delete 清除快速鍵，未在錄入時同樣有效。
- Tab 會停止錄入並移動焦點，且不錄入 Tab。離開欄位也會停止錄入。
- 按鍵讀取自實體按鍵，因此 Alt+K 錄入的是 K，而不是鍵入的符號。系統保留的組合鍵，例如 ⌘Tab、Alt+Tab 和 Win 鍵組合，永遠不會到達應用程式。

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
