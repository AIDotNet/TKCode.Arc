---
title: 指令面板
summary: 完整的鍵盤操作面板，支援搜尋、分組結果與快速鍵。
source-hash: 0e434befd364
---

<Demo name="Preview" />

## 何時使用

- 面向全產品的操作與導覽，使用者以鍵盤存取。
- 指令分散在多個選單與頁面中的應用程式，輸入一個字詞比逐一瀏覽更快。
- 同時顯示每個指令快速鍵的啟動器。

## 何時不用

- 搜尋含摘錄與篩選條件的文件或紀錄：使用 `SearchResults`。
- 頁首中顯示少量符合項的搜尋欄位：使用 `ExpandingSearch`。
- 為表單選擇一個值：使用 `Combobox`。
- 針對單一物件的少量操作：使用 `DropdownMenu`。

## 用法

指令面板是位於分組指令清單之上的搜尋欄位。加入 `CommandPaletteItem` 子元素或設定 `ItemsSource`。輸入會篩選清單，方向鍵與 Enter 則可不用滑鼠執行指令。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:CommandPalette MaxWidth="560">
        <arc:CommandPaletteItem Id="dashboard" Label="前往儀表板" Group="導覽" Shortcut="G D" />
        <arc:CommandPaletteItem Id="new-project" Label="建立專案" Description="從空白看板開始" Group="操作" />
        <arc:CommandPaletteItem Id="theme" Label="切換主題" Group="設定" />
    </arc:CommandPalette>
</UserControl>
```

### 指令

每個 `CommandPaletteItem` 都有作為標題的 `Label`，另可選擇設定 `Description`、`Icon`、`Shortcut` 與 `Group`。指令會列在其所屬群組之下，群組順序依其首次出現的順序排列。沒有群組的指令會放在 `DefaultGroup`（預設為 “Actions”）之下。`Icon` 接受 24×24 的 Lucide 圖形，例如 `ArcIcons` 中的圖示，也可以是任何控制項；預設圖示為 Lucide 的 Command 圖形。將 `IsEnabled` 設為 false，可顯示目前無法執行的指令。

搜尋會比對標題、描述、群組與 `Keywords`，且不分大小寫。為每個指令設定穩定的 `Id`，這樣清單變動時，該列會維持原位。

面板寬度為 480 像素或更小時，會隱藏快速鍵鍵帽並收緊內距。本例的寬度為 400 像素。

<Demo name="InlineItems" />

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:generic="using:System.Collections.Generic"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:CommandPalette>
        <arc:CommandPaletteItem Id="theme" Label="切換主題" Group="設定" Shortcut="T">
            <arc:CommandPaletteItem.Keywords>
                <generic:List x:TypeArguments="x:String">
                    <x:String>dark mode</x:String>
                    <x:String>appearance</x:String>
                </generic:List>
            </arc:CommandPaletteItem.Keywords>
        </arc:CommandPaletteItem>
    </arc:CommandPalette>
</UserControl>
```

### 執行指令

按 Enter 執行醒目提示的指令。輸入後若沒有醒目提示的項目，按 Enter 會執行最前面的符合項。點按會執行指標下方的指令。接著面板會清除查詢、將焦點保留在搜尋欄位中，並引發 `ItemSelected`。它也會以所選項目為參數執行 `SelectCommand`，並執行該項目自身的 `Command`，參數為其 `CommandParameter`。

```csharp
palette.ItemSelected += (_, e) => Navigate(e.Item.Id);
```

### 資料繫結

將 `ItemsSource` 繫結到 `CommandPaletteItem` 物件的集合。面板會跟隨集合與其中項目的變化。保留的列會移到新位置，新增的列會淡入，面板外框高度隨之改變。繫結 `SelectCommand`，即可以所選項目執行檢視模型中的指令。

<Demo name="DataBinding" />

```xml
<arc:CommandPalette ItemsSource="{Binding Commands}"
                    SelectCommand="{Binding RunCommand}" />
```

### 按需開啟

若要在頁面上方開啟面板，請將它放入覆蓋層中。設定 `AutoFocus`，讓面板出現時搜尋欄位取得焦點；設定 `ShowCloseButton` 以顯示 “Esc” 按鈕。在空白的搜尋欄位中按 Escape，或點按 “Esc” 按鈕，都會引發 `CloseRequested`；請在該處關閉覆蓋層，並在 `ItemSelected` 之後同樣關閉。本示範使用 Core 的 `ModalPresence`，它會加入遮罩、將焦點保持在覆蓋層內，並在關閉後把焦點還給開啟它的按鈕。面板自帶進場動態效果，因此模態層只做淡入。

<Demo name="OnDemand" />

```csharp
var palette = new CommandPalette { ItemsSource = commands, AutoFocus = true, ShowCloseButton = true, MaxWidth = 560 };
var modal = new ModalPresence(palette, anchor: this) { Entrance = ModalEntrance.Fade };
palette.CloseRequested += (_, _) => modal.Close();
palette.ItemSelected += (_, e) => modal.Close(DismissReason.Action);
modal.Open();
```

### 鍵盤快速鍵

在 macOS 上按 Cmd+K，其他平台按 Ctrl+K，即可從視窗中的任何位置將焦點移到搜尋欄位。面板會先於視窗處理此按鍵，因此面板顯示期間，它會優先於其他 Cmd/Ctrl+K 處理常式。頁面顯示多個面板時，請在除其中一個之外的所有面板上將 `IsShortcutEnabled` 設為 false。`ShortcutHint` 會改變搜尋列末端的鍵帽。

```xml
<arc:CommandPalette IsShortcutEnabled="False" />
<arc:CommandPalette ShortcutHint="Ctrl K" />
```

### 文字

所有文字都有英文預設值。`PlaceholderText` 是空白搜尋欄位中的提示，`Label` 為搜尋欄位命名。`EmptyTitle` 與 `EmptyDescription` 用來填入空狀態。`ClearButtonLabel`、`CloseButtonLabel` 與 `ResultsLabel` 分別為清除按鈕、關閉按鈕，以及供輔助技術使用的清單命名。

```xml
<arc:CommandPalette PlaceholderText="跳到頁面"
                    EmptyTitle="找不到頁面"
                    EmptyDescription="請檢查拼字，或試試其他字詞。" />
```

## API 參考

<!-- api: CommandPalette -->

<!-- api: CommandPaletteItem -->

## 無障礙

- 搜尋欄位的名稱由 `Label` 提供。結果清單的名稱由 `ResultsLabel` 提供，每一列則以其標題與描述命名。
- 醒目提示的列會向輔助技術回報為已選取，搜尋欄位的說明文字即為 `Label`。
- ArrowDown 與 ArrowUp 會移動醒目提示，並停在第一個與最後一個指令。Home 與 End 會直接跳到它們。已停用的指令會被略過。
- Enter 執行醒目提示的指令；輸入後則執行最前面的符合項。Escape 會清除查詢；在空白的搜尋欄位中，則會引發 `CloseRequested`。
- Cmd/Ctrl+K 會將焦點移到搜尋欄位。清除按鈕與 “Esc” 按鈕可用 Tab 鍵到達，並會顯示焦點框。
- 空狀態為純文字，因此螢幕閱讀器可以讀出。

## 動態效果

- 面板以淡入方式進場，並從稍小的縮放比例上移 6 像素。
- 只有一個醒目提示會在各列之間移動：方向鍵使用短距離滑動，指標則使用快速彈簧效果。醒目提示首次出現的位置會淡入，沒有任何列被醒目提示時則淡出。
- 輸入時，保留的列會平滑滑到新位置，離開的列淡出，新增的列淡入。面板外框的高度會以平滑的彈簧效果跟隨清單變化。若列需要移動超過六個位置，則會直接一次到位。
- 結果先淡出，空狀態再升起出現，因此外框只改變一次高度。
- 清除按鈕會從較小的縮放比例與柔和的模糊中彈出。
- 動態效果等級為 Reduced 時，只保留淡入淡出。等級為 Off 時，所有變更立即生效。

## 相關元件

- `SearchResults`：用於具備篩選與摘錄的完整搜尋頁面。
- `ExpandingSearch`：用於頁首中的精簡搜尋欄位。
- `DropdownMenu`：用於單一物件的操作。
- `Combobox`：用於在表單中選擇一個值。
