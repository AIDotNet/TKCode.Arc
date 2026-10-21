---
title: 快速鍵錄入
summary: 將按鍵組合錄入為鍵帽，支援衝突警告、Kbd 與可搜尋的速查表。
source-hash: d4863ab0dc45
---

<Demo name="Default" />

## 何時使用

`ShortcutRecorder` 讓使用者按下按鍵組合來設定快速鍵，而不必輸入按鍵名稱。它放在設定頁中，與其他偏好設定並列，並以符合目前平台的鍵帽顯示結果。在欄位背後，它會把按鍵組合與應用程式已在使用的快速鍵，以及瀏覽器和作業系統保留的組合進行比對，因此重新對應不會悄悄破壞某個命令。

- **生產力應用程式中的快速鍵設定**：使用者把「開啟檔案」從 Ctrl+O 改為 Ctrl+Shift+O。錄入器會記錄該組合；如果 Ctrl+Shift+O 已被「匯出報表」占用，它會發出警告，並把結果儲存為 `KeyGesture`。
- **開發者工具或 IDE 中的鍵盤對應**：終端機、編輯器或查詢主控台中，使用者重新繫結「執行查詢」或「註解列」。`Bindings` 保存應用程式目前的對應，因此每個衝突都會在錄入時被發現。
- **顯示即時繫結的說明對話方塊與速查表**：命令旁的快速鍵文字來自已儲存的 `Value`，因此重新對應後的快速鍵在各處都會顯示新的按鍵。
- **以鍵盤操作為主的管理主控台**：調度或客服工具中，專員用快速鍵進行分流。錄入器讓主管無需借助編輯器中的按鍵名稱，就能修改這些快速鍵。
- **偏好設定頁中還原預設值**：`ResetValue` 是預設快速鍵，值不同時，重設按鈕會把欄位還原為它。

## 何時不用

- **只用於顯示、不可修改的固定快速鍵**：用自己的文字或鍵帽控制項顯示它，因為沒有人會重新對應它。
- **依名稱執行動作**：使用 `Blocks` 與命令類元件中的命令面板模式，它依動作名稱尋找，不依賴按鍵組合。
- **僅觸控的產品**：沒有可錄入的實體鍵盤，點一下也永遠不會產生按鍵組合。
- **沒有按鍵語意的文字值**：使用 `Input`，例如巨集名稱、標籤或關於快速鍵的備註。
- **使用者在搜尋框中輸入的自由文字**：使用 `SearchField`；錄入器接收的是整個按鍵組合，不編輯文字。

## 用法

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:ShortcutRecorder Label="命令面板"
                          Description="在應用程式的任何位置開啟命令面板。" />
</UserControl>
```

按一下欄位或按 Enter，然後按下按鍵組合。錄入器會儲存一個 `KeyGesture`，並將它繪製為鍵帽。

`ShortcutRecorder` 衍生自 `TemplatedControl`。它的外框是 Arc 欄位外框，與 `Input` 相同；其中的錄入區域是一個按鈕，自行繪製鍵帽、錄入指示燈與預留位置文字。因此，錄入器是帶有欄位標籤、外框與訊息列的值編輯器，同時具備切換按鈕的錄入行為。

### 結構

1. **標籤**（`Label`）：欄位名稱，位於外框上方。`IsLabelVisible="False"` 會將其隱藏，但輔助技術仍會朗讀它。
2. **外框**：中等控制項高度（預設主題下為 44 px）的 Arc 欄位外框。懸停與焦點時，外框以前景色描邊。錄入時，錄入區域帶有 4% 的強調色底色；發生衝突時使用警告色邊緣。
3. **錄入區域**：填滿整列，右側內距 6 px，左側 9 px。它是取得焦點並開始錄入的元素。
4. **鍵帽**：快速鍵的每個按鍵對應一個鍵帽，最小尺寸 26 × 26 px，圓角 8 px，鍵帽之間間距 4 px。在 Mac 上，鍵帽顯示圖形符號（⌃ ⌥ ⇧ ⌘）；其他平台顯示名稱（Ctrl、Alt、Shift）。
5. **錄入指示燈與預留位置文字**：錄入時，在 `RecordingPlaceholder`（預設為「按下按鍵」）旁顯示一個 7 px 的圓點。沒有快速鍵時顯示 `Placeholder`（「錄入快速鍵」）。
6. **清除按鈕**（`ClearText`，「清除快速鍵」）：32 px 的方形按鈕，內含 16 px 圖示。設定了快速鍵且未在錄入時顯示。
7. **重設按鈕**：32 px 的方形按鈕，圖示相同。值與 `ResetValue` 不同時顯示，點選後還原為 `ResetValue`。
8. **訊息列**：外框下方 12 px 的文案。一次只顯示一則訊息，順序如下：衝突（帶 6 px 警告圓點，以及 `UseAnywayText` 和 `CancelText` 兩個連結）、修飾鍵提示、錄入提示（「按 Esc 取消，按 Backspace 清除」），最後是 `Description`。

### 值與重設

`Value` 是錄入到的 `KeyGesture`；沒有快速鍵時為 `null`。它是雙向的。`ResetValue` 是重設按鈕還原的值。Avalonia 沒有預設值，因此除非你設定，否則 `ResetValue` 為 `null`；值已設定而重設值為 `null` 的欄位，其重設按鈕顯示為「重設為無」。

<Demo name="Empty" />

```csharp
var palette = new ShortcutRecorder
{
    Label = "命令面板",
    Value = ShortcutFormatter.Parse("mod+k", ShortcutPlatform.Other),
    ResetValue = ShortcutFormatter.Parse("mod+k", ShortcutPlatform.Other)
};
```

`mod` 在 macOS 上表示 Command，在其他平台上表示 Control。使用 `ShortcutFormatter.Parse` 從這段文字建立手勢；當文字來自設定檔且可能無效時，改用 `ShortcutFormatter.TryParse`。對於不是恰好包含一個按鍵的快速鍵文字，`Parse` 會擲出 `FormatException`。

### 已知快速鍵與衝突

`Bindings` 列出應用程式已在使用的快速鍵，每項都是一個帶有 `Gesture` 和 `Label` 的 `ShortcutBinding` 物件。錄入其中某個快速鍵時，會顯示衝突，並提供兩個真正的按鈕：仍然使用和取消。「仍然使用」會採用該快速鍵，並在 `ValueChanged` 中回報其來源的繫結，因此應用程式可以從自己的對應中移除該繫結。「取消」則保留先前的快速鍵。

<Demo name="Conflicts" />

保留的組合同樣會發出警告，例如用於關閉分頁的 Ctrl+W、新增分頁的 Ctrl+T、複製與貼上的 Ctrl+C 和 Ctrl+V、復原的 Ctrl+Z，以及全選的 Ctrl+A。它們在 `Bindings` 之後檢查。若保留組合已被占用，使用者選擇「仍然使用」，回報的被取代繫結為空，因為應用程式中沒有任何東西擁有它。只有當應用程式確實擁有這些組合，且警告只會造成干擾時，才將 `IsReservedWarningEnabled` 設為 `False`。

<Demo name="Reserved" />

### 修飾鍵與平台

`IsModifierRequired` 預設為 `True`：單獨的字母不是快速鍵，因此錄入器會顯示提示「請包含 Ctrl 或 Alt」（在 Mac 上為「請包含 ⌘、⌃ 或 ⌥」），並等待修飾鍵。功能鍵一律允許。對於單鍵快速鍵，例如「?」說明鍵，或用來切換檢視的數字鍵，將其設為 `False`。

`Platform` 設定鍵帽樣式以及 `mod` 的意義：`ShortcutPlatform.Mac` 顯示圖形符號，並以 Command 作為 `mod`；`ShortcutPlatform.Other` 顯示名稱，並以 Control 作為 `mod`。保持為 `null` 時會偵測正在執行的平台，這正是桌面應用程式通常需要的。若設定頁必須在每台電腦上顯示相同的鍵帽，或用於截圖，則應明確設定它。

<Demo name="NoModifier" />

<Demo name="Platforms" />

### 事件

`ValueChanged` 在 `Value` 變動時引發：錄入了快速鍵、清除了快速鍵、重設，或從程式碼設定了值。它的引數包含舊的與新的 `KeyGesture`，以及 `Replaced` 繫結。`Replaced` 保存「仍然使用」所取自的那個繫結，在其他所有變動中為 `null`。

<Demo name="Events" />

```csharp
recorder.ValueChanged += (_, e) =>
{
    var replaced = e.Replaced is { } binding ? $" (replaced {binding.Label})" : string.Empty;
    Console.WriteLine($"{e.OldValue} -> {e.NewValue}{replaced}");
};
```

### 繫結到檢視模型

設定頁的檢視模型擁有快速鍵對應。每一列把 `Value` 雙向繫結到自己的手勢，而錄入器的 `Bindings` 清單則是對應中的其他快速鍵。範例使用 CommunityToolkit.Mvvm。

```csharp
public partial class ShortcutSettingsViewModel : ObservableObject
{
    public ShortcutSettingsViewModel()
    {
        Shortcuts.Add(new ShortcutBinding(ShortcutFormatter.Parse("ctrl+p", ShortcutPlatform.Other), "快速開啟"));
        Shortcuts.Add(new ShortcutBinding(ShortcutFormatter.Parse("ctrl+s", ShortcutPlatform.Other), "儲存"));
    }

    public ObservableCollection<ShortcutBinding> Shortcuts { get; } = new();

    [ObservableProperty]
    private KeyGesture? _openFile = ShortcutFormatter.Parse("ctrl+o", ShortcutPlatform.Other);

    [RelayCommand]
    private void ResetOpenFile() => OpenFile = ShortcutFormatter.Parse("ctrl+o", ShortcutPlatform.Other);
}
```

```xml
<arc:ShortcutRecorder Label="開啟檔案"
                      Value="{Binding OpenFile, Mode=TwoWay}"
                      Bindings="{Binding Shortcuts}"
                      ResetValue="Ctrl+O"
                      Platform="Other" />
```

`Value` 是雙向的。`Bindings` 是欄位在使用者錄入時讀取的清單，因此應將它繫結到應用程式所用的對應。`ValueChanged` 是事件，當應用程式必須從自己的對應中移除被取代的繫結時，在程式碼後置中連接它。`IsRecording` 是唯讀的，`StartRecording()` 與 `StopRecording()` 用於從程式碼啟動和停止錄入器。

### 隱藏標籤

`IsLabelVisible="False"` 只為螢幕閱讀器保留標籤。在表格列或清單中，若該列已經為動作命名，可以使用它，無障礙一節對此有說明。

<Demo name="HiddenLabel" />

### 說明

`Description` 是欄位下方的輔助文字。在錄入提示、修飾鍵提示或衝突生效時，它們會取代該位置，因此當其他內容都不需要這一列時，說明即描述該快速鍵。

<Demo name="Description" />

### 停用

停用的錄入器會在淡化的表面色上顯示，不會錄入，也不會取得焦點。

<Demo name="Disabled" />

### 範例：設定頁中的鍵盤快速鍵

一個桌面生產力應用程式的偏好設定頁。每一列以句首大寫的形式命名一個動作，錄入器顯示目前的快速鍵。「開啟檔案」已從 Ctrl+O 改為 Ctrl+Shift+O，因此它的重設按鈕可見，點選即可還原預設值。第一列的說明解釋了該快速鍵的作用，平台固定為「Other」，因此截圖在任何電腦上都顯示 Ctrl 名稱。

![帶四個錄入器的鍵盤快速鍵設定，其中一個已重新對應並帶有重設按鈕](images/ShortcutSettings.png)

```xml
<StackPanel Width="600" Spacing="16">
    <TextBlock Text="Keyboard shortcuts"
               FontSize="16"
               FontWeight="SemiBold"
               Foreground="{arc:ArcDesignTokenResource Foreground}" />
    <arc:ShortcutRecorder Label="Command palette"
                          Platform="Other"
                          Value="Ctrl+K"
                          ResetValue="Ctrl+K"
                          Description="Opens the command palette from any screen." />
    <arc:ShortcutRecorder Label="Open file"
                          Platform="Other"
                          Value="Ctrl+Shift+O"
                          ResetValue="Ctrl+O"
                          Description="Reset returns the shortcut to Ctrl+O." />
    <arc:ShortcutRecorder Label="Toggle sidebar"
                          Platform="Other"
                          Value="Ctrl+B"
                          ResetValue="Ctrl+B" />
    <arc:ShortcutRecorder Label="Save report"
                          Platform="Other"
                          Value="Ctrl+S"
                          ResetValue="Ctrl+S" />
</StackPanel>
```

### 文案規範

- **標籤**：該快速鍵所執行的動作，使用句首大寫的簡短名詞片語：「命令面板」「開啟檔案」「切換側邊欄」。絕不要用按鍵作為標籤：當值變動時，標籤為「Ctrl+O」對使用者毫無意義。
- **說明**：一句話說明該快速鍵的作用，或它與什麼衝突，例如「在任何畫面開啟命令面板。」。這句話對每個預設值都必須成立；其餘內容由衝突訊息補充。
- **衝突文案**：由錄入器根據繫結的標籤產生。點名該快速鍵已經執行的動作，並讓兩個選項使用相同的術語：「仍然使用」和「取消」。
- **提示**：快速鍵需要修飾鍵，因此提示應點名需要加入的修飾鍵。不要寫泛泛的錯誤訊息。

| 不要寫 | 改為 |
|---|---|
| Keybinding | 快速鍵 |
| Ctrl+O（作為標籤） | 開啟檔案 |
| Shortcut invalid | 請包含 Ctrl 或 Alt |
| Shortcut already taken | Ctrl+K 已被命令面板使用。仍然使用，或錄入另一個。 |
| Set key | 錄入快速鍵（預設預留位置文字，請保留） |

### 最佳做法

**建議**

- 在 `Bindings` 中傳入應用程式目前的對應。只有欄位知道的快速鍵才會被偵測為衝突，因此空清單會讓所有衝突都看不見。
- 為每個有預設值的欄位設定 `ResetValue`。沒有它，重設按鈕會顯示「重設為無」，且不會還原任何有用的內容。
- 處理 `ValueChanged`，當「仍然使用」採用某個快速鍵時，從應用程式自己的對應中移除 `Replaced`。錄入器不會替你解除另一個動作的繫結。
- 用 `ShortcutFormatter.Matches` 在視窗的按鍵處理中比對快速鍵。錄入器記錄的是 `KeyGesture`，而 `Matches` 依實體按鍵與精確的修飾鍵進行比較，因此同一手勢執行同一動作。

**避免**

- 用於僅觸控的螢幕，因為無法按下任何按鍵組合。這些螢幕應改用其他控制項。
- 在一般桌面應用程式中關閉 `IsReservedWarningEnabled`。該警告能防止重新對應破壞複製、貼上或關閉，這些操作在其他應用程式中都被廣泛依賴。
- 把快速鍵作為觸達某個命令的唯一途徑。同時把命令放入功能表或命令面板，使其可以被發現。
- 在開啟 `IsModifierRequired` 的快速鍵欄位中錄入單獨的字母。提示會拒絕它，而且單一字母會在使用者向任何文字欄位輸入時被觸發。

### 常見問題

**為什麼錄入器看不到 Tab 或 Alt+Tab？** Tab 永遠不會被錄入：它會停止錄入並移動焦點。作業系統保留的按鍵組合（Alt+Tab、Ctrl+Alt+Delete、大多數 Windows 組合以及 macOS 截圖快速鍵）永遠不會到達應用程式，因此無法被錄入。

**如何執行快速鍵繫結的動作？** 在視窗的 `KeyDown` 處理常式中，使用保存的手勢呼叫 `ShortcutFormatter.Matches(e, gesture, platform)`。當實體按鍵與精確的修飾鍵都相符時，它會傳回 `true`。

**為什麼沒有重設按鈕？** 當值與 `ResetValue` 不同時，重設按鈕才會顯示。若兩者都為 `null`，就沒有可以還原的內容，因此不顯示按鈕。將 `ResetValue` 設為預設快速鍵即可顯示它。

**在程式碼中設定值會引發 `ValueChanged` 嗎？** 會。錄入、清除、重設的值，以及從程式碼或繫結設定的值，都會引發該事件。如果儲存只應在使用者的操作時進行，請在處理常式中比較新舊手勢。

## API 參考

<!-- api: ShortcutRecorder -->

配套型別。`ShortcutPlatform` 設定鍵帽樣式以及 `mod` 的意義：

| 成員 | 意義 |
|---|---|
| `Mac` | Apple 鍵盤：⌃ ⌥ ⇧ ⌘ 圖形符號，`mod` 為 Command |
| `Other` | Windows 與 Linux 鍵盤：顯示 Ctrl、Alt、Shift 和 Win 名稱，`mod` 為 Control |

`ShortcutBinding` 是 `Bindings` 的一項。它有供 XAML 使用的無參數建構函式，以及接收手勢與標籤的建構函式。它的 `Gesture` 是一個 `KeyGesture`，`Label` 是衝突訊息中顯示的動作名稱。

`ShortcutRecorderValueChangedEventArgs` 是 `ValueChanged` 的引數：

| 成員 | 意義 |
|---|---|
| `OldValue` | 先前的快速鍵，沒有時為 `null` |
| `NewValue` | 新的快速鍵，被清除時為 `null` |
| `Replaced` | 在「仍然使用」之後，該快速鍵所取自的繫結；在其他所有變動中為 `null`，包括保留的快速鍵 |

`ShortcutFormatter` 是錄入器使用的靜態輔助類別。它是公開的，以便應用程式使用相同的規則顯示與比對手勢：

| 成員 | 意義 |
|---|---|
| `CurrentPlatform` | 程序所在的平台：在 macOS 與 iOS 上為 `Mac` |
| `Format(gesture, platform)` | 文字（Mac 上為 “⌘⇧K”，其他平台為 “Ctrl+Shift+K”）以及朗讀形式（“Command Shift K”） |
| `Tokens(gesture, platform)` | 依顯示順序排列的鍵帽，每個都帶有標籤、識別碼和朗讀名稱 |
| `Matches(e, gesture, platform)` | 當按鍵事件與手勢的實體按鍵和精確修飾鍵一致時傳回 `true` |
| `FromKeyEvent(e)` | 將按下的按鍵讀取為快速鍵；僅按下修飾鍵時傳回 `null` |
| `Parse(text, platform)` | 解析 “mod+shift+k”、“alt+f4” 或 “ctrl+/”；對無效快速鍵擲出 `FormatException` |
| `TryParse(text, platform, out gesture)` | 相同的解析，但不擲出例外 |

## 無障礙

- 錄入器是一個切換按鈕，以其標籤和朗讀形式的快速鍵命名，例如「命令面板 Command Shift K」。錄入時它處於按下狀態，其說明文字即為訊息列。
- 錄入、結果、衝突和清除都會透過即時區域禮貌地播報。播報使用與訊息列相同的文字。
- 錄入器內的鍵帽對輔助技術隱藏，朗讀名稱改由按鈕提供。
- 重設與清除按鈕分別命名為「重設為 <快速鍵>」（或「重設為無」）和「清除快速鍵」。
- 錄入時，按鍵組合歸錄入器所有，因此視窗快速鍵不會被觸發。
- `IsLabelVisible="False"` 會隱藏標籤，但標籤仍會為輔助技術命名錄入器。

### 鍵盤

| 按鍵 | 操作 |
|---|---|
| 錄入器上的 `Enter` 或 `Space` | 開始錄入 |
| 錄入時按下按鍵組合 | 從實體按鍵錄入組合；`Alt+K` 錄入的是 K，而不是輸入的符號 |
| 錄入時按 `Escape` | 取消，先前的快速鍵保持不變 |
| 錄入時按 `Backspace` 或 `Delete` | 清除快速鍵 |
| 錄入器上未在錄入時按 `Backspace` 或 `Delete` | 設定了快速鍵時，清除快速鍵 |
| 錄入時按 `Tab` | 停止錄入並移動焦點，不錄入 Tab |
| 焦點離開錄入器 | 停止錄入，並清除提示 |
| 沒有修飾鍵的單一字母 | 顯示提示並繼續錄入；功能鍵無需修飾鍵即可錄入 |
| 以 `Tab` 進入衝突提示 | 依序到達「仍然使用」和「取消」；兩者都會將焦點傳回錄入器 |

## 動態效果

- 鍵帽以小幅縮放和模糊彈入。按鍵組合完成後，鍵帽依序落定，每個間隔 35 ms。
- 按鍵組合變動時，鍵帽滑動到新位置。
- 錄入時錄入指示燈會脈動。訊息列以高度彈簧展開和收合。
- 減少動態效果時會去掉縮放、錯峰與滑動。鍵帽和訊息只做淡入淡出，錄入指示燈停止脈動。關閉動態效果時立即到達最終狀態。

| 變化 | 完整 | 減少 | 關閉 |
|---|---|---|---|
| 錄入時鍵帽出現 | 以小幅縮放和模糊彈入 | 短暫淡入 | 立即完成 |
| 按鍵組合完成 | 鍵帽依序落定，每個間隔 35 ms | 短暫淡入 | 立即完成 |
| 按鍵組合變動 | 鍵帽以靈敏的彈簧滑動到新位置 | 短暫淡入 | 立即完成 |
| 錄入指示燈 | 錄入時脈動 | 保持穩定，不脈動 | 保持穩定 |
| 訊息列變化 | 以高度彈簧展開和收合；新文字帶著柔和模糊升起 | 短暫淡入 | 立即完成 |

## 相關元件

- `Input` 用於不是快速鍵的自由文字。
- `Button` 用於快速鍵所觸發的命令。
- `Field` 與 `FieldFrame` 用於搭配自訂編輯器建構類似的欄位。
- `SearchField` 用於篩選清單的文字輸入。

| 元件 | 適用情境 |
|---|---|
| `ShortcutRecorder` | 使用者按下按鍵設定快速鍵，並需要衝突檢查 |
| `Input` | 值是文字，例如巨集名稱或備註 |
| `SearchField` | 使用者輸入文字以篩選清單或功能表 |
| `Button` | 快速鍵執行一個命令，而該命令需要自己的控制項 |
| `Field` + `FieldFrame` | 你要建構自己的編輯器，並希望擁有相同的標籤、外框與訊息列 |
