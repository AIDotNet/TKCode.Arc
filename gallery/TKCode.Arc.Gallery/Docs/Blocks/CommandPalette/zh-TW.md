---
title: 指令面板
summary: 完整的鍵盤操作面板，支援搜尋、分組結果與快速鍵。
source-hash: fadb34de660a
---

<Demo name="Preview" />

## 何時使用

指令面板是透過鍵盤存取應用程式操作與頁面的入口。它屬於應用程式外殼：使用者在視窗的任何位置按下 Cmd+K 或 Ctrl+K 開啟它，輸入幾個字母，就能執行指令，無需在側邊欄或選單中逐層尋找。`CommandPaletteDialog` 會把它以彈出層的形式顯示在頁面上方，用完後再關閉。它以一個搜尋欄位加上依用途分組的指令清單，取代了冗長的選單樹。

- **大型管理主控台中的導覽**：一個有四十個頁面的 CRM，業務主管輸入「pipe」即可進入銷售管道報表。當側邊欄的項目多到無法一眼掃完時，輸入比瀏覽更快。
- **分散在多個選單中的操作**：「將發票匯出為 CSV」在帳單頁、報表頁與發票清單工具列中都有用。用一個帶有自己分組的指令把它集中在一處，選單就可以保持簡短。
- **分析或開發者工具中的進階操作**：「開啟已儲存的報表」「執行查詢」或「切換環境」。`Shortcut` 鍵帽會在指令旁邊顯示按鍵，常用的使用者由此學會快速鍵，不必再頻繁伸手去拿滑鼠。
- **偏好設定與開關**：「切換為深色模式」或「變更時區」。`Keywords` 讓使用者能依想要的結果找到指令（「外觀」「發票」），而不必記得團隊選定的選單名稱。
- **客服或履約主控台中的操作工具**：客服人員在通話期間用鍵盤執行「重新寄送收據」「開啟帳號」或「退款」，選單中每一秒的瀏覽都會被察覺。

## 何時不用

- **搜尋含摘錄與篩選條件的紀錄或文件**，例如依狀態與日期篩選的支援工單：使用 `SearchResults`，它會顯示含篩選條件與符合文字的完整結果頁。
- **頁首中的搜尋欄位，只顯示目前頁面的少量符合項**：使用 `ExpandingSearch`，它保持精簡，且不列出指令。
- **為表單選擇一個值**，例如方案、國家或成本中心：使用 `Combobox`。指令面板用於執行指令，不會設定隨表單送出的欄位。
- **針對單一物件的操作**，例如對某一筆發票執行下載、作廢與封存：使用 `DropdownMenu`，並錨定在該列上，讓使用者選擇時物件始終可見。
- **從一個很短的固定清單中選一個值**：使用 `Select`。只有三個項目的面板比下拉選取更費事。
- **請使用者做出決定或填寫簡短的表單**，例如確認刪除某個工作區：使用 `Dialog`。指令面板在使用者選定指令的那一刻就會關閉，因此無法承載一個問題。

## 用法

`CommandPaletteDialog` 是以彈出層呈現的指令面板。在檢視中的任何位置宣告一次即可。它在原位不轉譯任何內容，開啟時在頁面上方顯示一個 `CommandPalette`：使用者在視窗的任何位置按下 Cmd+K（其他平台為 Ctrl+K），或點按一個指向它的按鈕。加入 `CommandPaletteItem` 子元素，或設定 `ItemsSource`。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <Panel>
        <arc:Button Content="搜尋指令"
                    arc:CommandPaletteDialog.Opens="{Binding #Commands}" />

        <arc:CommandPaletteDialog x:Name="Commands"
                                  PlaceholderText="搜尋專案指令">
            <arc:CommandPaletteItem Id="rename" Label="重新命名看板" Description="換一個新標題" Group="看板" Shortcut="R" />
            <arc:CommandPaletteItem Id="invite" Label="邀請團隊成員" Group="看板" />
            <arc:CommandPaletteItem Id="export" Label="匯出工作項目" Group="工作區" />
        </arc:CommandPaletteDialog>
    </Panel>
</UserControl>
```

```csharp
Commands.ItemSelected += (_, e) => Run(e.Item.Id);
```

上方的預覽呈現的是應用程式中彈出層的樣貌，不過它被限制在示範舞台內（`IsModal="False"`）：頁面載入時它就是開啟的，按 Esc 關閉，點按按鈕或按 Cmd+K 可以再次開啟。在視窗中，對話方塊會以淡淡的遮罩蓋住整個視窗，「開啟手勢」一節的示範即是如此。

對話方塊顯示的面板是 `CommandPalette`，一個 `TemplatedControl`。它的範本包含搜尋列、依標題分組的結果清單與空狀態。搜尋欄位是面板自己的文字方塊，衍生自 Avalonia 的 `TextBox`，因此游標、選取、輸入法組字與剪貼簿快速鍵的行為與 `Input` 相同。面板在此之上加入篩選、醒目提示與鍵盤操作模型。它的外觀來自面板的佈景主題，因此應透過佈景主題修改外觀，而不是在程式碼中修改。`CommandPalette` 也可以單獨內嵌在頁面中使用，請見「單獨使用面板」。

在 XAML 中寫入的子元素會進入 `Items`，它是對話方塊與面板上的一個內嵌清單。設定 `ItemsSource` 後，它會取代 `Items`。固定的選單使用 `Items`，由應用程式在執行時建立或變更的清單則使用 `ItemsSource`。

### 結構

1. **搜尋列**：包含搜尋圖示、搜尋欄位與末端的控制項。高度至少 54 px，左右內距為 14 px（視窗寬度為 480 px 或更小時為 11 px）。18 px 的搜尋圖示位於 22 px 的方框中，距搜尋欄位 10 px。
2. **搜尋欄位**（`PlaceholderText`、`Label`）：14 px 文字，行高 20 px。在使用者輸入之前，預留位置文字顯示為 "Search commands"。
3. **清除按鈕**（`ClearButtonLabel`）：24 px 見方，內含 15 px 的叉號。查詢有內容後才會出現。
4. **快速鍵鍵帽**（`ShortcutHint`）：位於搜尋列末端的按鍵鍵帽，高 24 px，圓角 7 px。在對話方塊中它顯示開啟手勢。視窗寬度為 480 px 或更小時，以及 `ShortcutHint` 為空字串時，它會隱藏。
5. **關閉按鈕**（"Esc"，`ShowCloseButton`、`CloseButtonLabel`）：高 24 px，寬至少 27 px，左右內距 6 px。它有自己的焦點框，位於圓角外側 3 px。對話方塊預設會顯示它；內嵌的面板則要在 `ShowCloseButton` 為 true 時才顯示。
6. **結果清單**（`ResultsLabel`）：最高 342 px，超出後捲動。左右內距 8 px，上下內距 6 px（視窗寬度為 480 px 或更小時四周均為 6 px）。
7. **分組標題**（`Group`；沒有分組的指令使用 `DefaultGroup`）：12 px 的淡化文字，上方 6 px，下方 3 px。各分組之間相距 6 px。
8. **指令列**（`Label`、`Description`、`Icon`、`Shortcut`）：高度至少 34 px，左右內距 8 px，圓角 8 px。16 px 的圖示後接 10 px 的間距。標題為 14 px 中等字重；描述為 12 px 的淡化文字，與標題位於同一列，距標題 8 px。快速鍵鍵帽高 18 px，文字 11 px，圓角 5 px。
9. **醒目提示**：一個與列圓角相同的圓角矩形，在各列之間移動。它標示 Enter 將執行的指令。
10. **空狀態**（`EmptyTitle`、`EmptyDescription`）：32 px 的圖示框內放置 20 px 的圖形，標題與描述之間相距 4 px，上方 26 px，下方 28 px。
11. **面板表面**：浮動面板，帶有表面色、細邊框、面板圓角與浮動陰影。對話方塊把它的寬度設為 `PanelWidth`，預設 560 px；內嵌的面板則由宿主決定寬度。本文範例使用 400 px、480 px 與 560 px。
12. **遮罩**（僅對話方塊，`ScrimBrush`、`BackdropBlur`）：蓋在頁面上的近黑色層，兩種外觀下都是 32% 的不透明度，並可選擇加上背景模糊。`IsModal` 為 false 時沒有遮罩。

### 指令

每個 `CommandPaletteItem` 都有 `Label`，並可選擇設定 `Description`、`Group`、`Icon`、`Shortcut`、`Keywords` 與 `IsEnabled`。指令列在其所屬分組之下，分組依其第一個指令出現的順序排列。沒有分組的指令會歸入 `DefaultGroup`，預設為 "Actions"。如果應用程式的通用指令使用其他標題，例如導覽面板中的 "Pages"，請把 `DefaultGroup` 設為該標題。

`Icon` 接受 24 × 24 的 Lucide 圖形，例如 `ArcIcons` 中的圖示，也可以是任何控制項。面板以 16 px 繪製它。預設圖示是 Lucide 的 Command 圖形。

示範使用內嵌的面板，讓每個指令都留在畫面上。對話方塊接受相同的指令。

<Demo name="InlineItems" />

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:generic="using:System.Collections.Generic"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
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

`IsEnabled="False"` 會顯示一個目前無法執行的指令，並將其淡化。停用的指令會被醒目提示略過，也無法透過點按或 Enter 選取。請為它搭配說明原因的 `Description`，如示範中的 "Needs the Analytics plan"：使用者同時看到指令與原因，而不會疑惑它為什麼不見了。

搜尋會比對標題、描述、分組與 `Keywords`，它們以空格連接，比對時不區分大小寫。查詢會去除前後空白，只要連接後的文字中包含該子字串即為符合，因此一個查詢可以橫跨兩個欄位。請為每個指令設定穩定的 `Id`。清單被篩選或重新整理時，列會依 `Id` 維持原位，因此醒目提示會留在原本的指令上。

窄視窗會得到較緊湊的面板。視窗寬度為 480 像素或更小時，面板會隱藏快速鍵鍵帽並收緊內距；寬度為 340 像素或更小時，各列的左右邊距也會收緊。這條規則依循視窗，而不是面板本身的寬度：在一般視窗中，上方 400 像素寬的面板仍會顯示鍵帽，440 像素寬的彈出層也一樣。

### 執行指令

按 Enter 執行醒目提示的指令。輸入後若沒有醒目提示的項目，按 Enter 會執行排在最前面的符合項。點按會執行指標下方的指令。兩種方式都會先清除查詢，然後依序進行：

1. 對話方塊開始關閉（除非關閉了 `CloseOnSelect`），焦點回到頁面。
2. 引發 `ItemSelected`。它會向上冒泡，因此頁面可以監聽。
3. 以所選項目為參數執行 `SelectCommand`。
4. 以該項目的 `CommandParameter` 為參數執行該項目自身的 `Command`。

下次開啟對話方塊時，搜尋欄位是空的。內嵌的面板不會關閉，所以它讓焦點留在搜尋欄位中，並執行第 2 到 4 步。

```csharp
dialog.ItemSelected += (_, e) => Navigate(e.Item.Id);
```

導覽很單純時，在程式碼後置中使用 `ItemSelected`；在 MVVM 應用程式中，使用 `SelectCommand`，讓檢視模型決定每個指令做什麼。當一個指令只屬於一個項目時，例如開啟特定對話方塊的指令，使用該項目自身的 `Command`。用 `Tag` 在項目上攜帶應用程式物件，這樣處理常式就無需解析 `Id`。由於對話方塊在處理常式執行之前就已關閉，開啟 `Dialog` 的指令會發現焦點已經回到頁面，而那個 `Dialog` 關閉時也會把焦點還到那裡。

### 繫結到檢視模型

將 `ItemsSource` 繫結到 `CommandPaletteItem` 物件的集合，並將 `SelectCommand` 繫結到一個接收所選項目的指令。繫結 `IsOpen` 之後，檢視模型就能開啟與關閉對話方塊；它預設為雙向繫結，因此由按鍵或點按引起的關閉，檢視模型也看得到。面板會跟隨集合及其中各項目的變化。保留的列會移到新位置，新增的列淡入，面板外框隨之改變高度。請在 `ObservableCollection` 中修改項目，而不是取代整個清單，這樣保留的列能維持原位。

示範使用內嵌的面板，它還有雙向的 `Query`；`ActiveItem` 為唯讀，回報目前醒目提示的指令。對話方塊沒有 `Query`：每次開啟時搜尋欄位都是空的。

<Demo name="DataBinding" />

```csharp
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TKCode.Arc;

public partial class CommandCenterViewModel : ObservableObject
{
    private readonly IWorkspaceNavigator _navigator;

    public CommandCenterViewModel(IWorkspaceNavigator navigator)
    {
        _navigator = navigator;
        Commands = new ObservableCollection<CommandPaletteItem>
        {
            new() { Id = "billing", Label = "前往帳單", Group = "導覽", Icon = ArcIcons.CreditCard, Shortcut = "G B" },
            new() { Id = "reports", Label = "開啟報表", Group = "導覽", Icon = ArcIcons.ChartBar },
            new() { Id = "export", Label = "將發票匯出為 CSV", Description = "本月", Group = "操作", Icon = ArcIcons.FileText },
            new() { Id = "invite", Label = "邀請團隊成員", Group = "操作", Icon = ArcIcons.Users, Keywords = ["add user", "seat"] }
        };
    }

    public ObservableCollection<CommandPaletteItem> Commands { get; }

    [ObservableProperty]
    private bool _isPaletteOpen;

    [RelayCommand]
    private void OpenPalette() => IsPaletteOpen = true;

    [RelayCommand]
    private async Task RunCommandAsync(CommandPaletteItem? item)
    {
        if (item is null)
        {
            return;
        }
        await _navigator.RunAsync(item.Id);
    }
}
```

```xml
<arc:CommandPaletteDialog ItemsSource="{Binding Commands}"
                          SelectCommand="{Binding RunCommandCommand}"
                          IsOpen="{Binding IsPaletteOpen}"
                          PlaceholderText="搜尋頁面與操作" />
```

```xml
<arc:CommandPalette ItemsSource="{Binding Commands}"
                    Query="{Binding Query, Mode=TwoWay}"
                    SelectCommand="{Binding RunCommandCommand}"
                    PlaceholderText="搜尋頁面與操作" />
```

- `IsOpen` 為雙向繫結，因此檢視模型能讀到每一次關閉。把它設為 true 即可開啟對話方塊，例如透過 `OpenPaletteCommand` 這類工具列指令。
- 內嵌面板上的 `Query` 為雙向繫結，因此檢視模型可以讀取文字（例如記錄搜尋內容），或將其重設。將它設為空字串即可清空搜尋；`Clear()` 是供程式碼後置使用的面板方法，它還會重設醒目提示。
- `ItemsSource` 與 `SelectCommand` 為單向繫結。面板從不寫入集合或指令。
- 編譯繫結需要在檢視上設定 `x:DataType`。為求簡潔，這裡的範例省略了它。

### 開啟與關閉

對話方塊可以透過 `IsOpen`、`Open()`、`Toggle()`、開啟手勢或指向它的按鈕開啟，並透過 `IsOpen`、`Close()` 或下表中的某個操作關閉。`IsOpen` 預設為雙向繫結，因此檢視模型看得到每一次關閉，包括使用者以按鍵或點按發起的關閉。

| 使用者的操作 | `DismissReason` | 以哪個屬性關閉 |
|---|---|---|
| 在空白的搜尋欄位中按 Esc | `Escape` | `CloseOnEscape` |
| 按下遮罩；`IsModal` 為 false 時，按下面板之外的任何位置 | `PressOutside` | `CloseOnOutsidePress` |
| 點按 "Esc" 鍵帽 | `CloseButton` | `ShowCloseButton` 會隱藏鍵帽 |
| 選擇一個指令 | `Action` | `CloseOnSelect` |
| 對話方塊開啟時按下開啟手勢 | `Programmatic` | `IsShortcutEnabled` |

搜尋欄位中有查詢時，按 Esc 只會清除查詢，再按一次 Esc 才會關閉對話方塊。鍵帽是一個可見的按鈕，點按它是明確的操作，因此即使關閉了 `CloseOnEscape`，它也一定會關閉。`Close()`、`CloseCommand` 以及把 `IsOpen` 設為 false，都是來自程式碼的關閉，回報的原因是 `Programmatic`。

若要讓對話方塊在某一次關閉要求時維持開啟，請取消 `Closing`。表中的每一種關閉以及 `Close()` 之前都會引發它，原因在 `e.Reason` 中；把 `IsOpen` 設為 false 則直接關閉，不會詢問。對話方塊關閉時會隨後引發 `Closed`，此時結束動畫可能仍在播放，之後 `LastCloseReason` 會保留這個原因。被選取的指令在 `Closing` 被取消時仍會執行，被略過的只是關閉。

```csharp
dialog.Closing += (_, e) => e.Cancel = hasUnsavedInput && e.Reason == DismissReason.PressOutside;
dialog.Closed += (_, e) => lastCloseReason = e.Reason;
```

### 從按鈕或程式碼開啟

任何按鈕都可以透過附加屬性 `CommandPaletteDialog.Opens` 開啟對話方塊，如用法範例所示。在程式碼中，呼叫 `Open()`、`Toggle()` 或 `Close()`。若要從選單項目或工具列指令開啟，請繫結到 `OpenCommand`、`ToggleCommand` 或 `CloseCommand`，它們是對話方塊上現成的 `ICommand` 屬性。

```xml
<MenuItem Header="指令面板" Command="{Binding #Commands.OpenCommand}" />
```

除了按鍵之外，也請在使用者習慣尋找搜尋功能的地方放一個按鈕，這樣不知道 Cmd+K 的使用者同樣找得到指令面板。關閉時焦點會回到原本擁有它的元素，所以開啟對話方塊的按鈕會重新取得焦點。

### 開啟手勢

`OpenGesture` 在你設定之前是 Cmd+K 或 Ctrl+K，所有平台都一樣。對話方塊在視窗層級處理它，先於視窗及其控制項，無論焦點在哪裡，包括文字方塊中，而且它是切換：同一個按鍵會關閉已開啟的對話方塊。預設手勢只比對單獨按下 Cmd 或 Ctrl 的 K 鍵，因此 Ctrl+Shift+K 不是開啟手勢。

`IsShortcutEnabled` 關閉了手勢、對話方塊不在視窗中、已停用或位於被隱藏的頁面或標籤頁上時，以及有另一個模態層位於最上層時（例如 `Dialog` 或另一個已開啟的指令面板對話方塊），手勢都會被忽略。搜尋列中的鍵帽顯示這個手勢：`ShortcutHint` 會取代它的文字，空字串則隱藏它。`IsShortcutEnabled` 為 false 且沒有設定 `ShortcutHint` 時，鍵帽同樣隱藏，因為這個按鍵什麼也不做。

示範回應的是 Cmd+Shift+P（其他平台為 Ctrl+Shift+P），因為本頁頂部的預覽已經在回應 Cmd+K。它蓋住整個視窗，並在遮罩之下模糊頁面（`BackdropBlur`）。

<Demo name="OnDemand" />

```csharp
dialog.OpenGesture = new KeyGesture(Key.P, KeyModifiers.Control | KeyModifiers.Shift);
```

```xml
<arc:CommandPaletteDialog BackdropBlur="4" PanelWidth="640" />
```

一個按鍵只有一個回應者。當兩個對話方塊，或一個對話方塊與一個內嵌面板比對到同一個按鍵時，只有其中一個會回應，而且是哪一個並沒有定義；請讓頁面上的每個對話方塊使用各自的手勢。你自己的 Cmd/Ctrl+K 處理常式，例如側邊欄搜尋，也是如此：對話方塊在視窗層級回應這個按鍵，先於你的處理常式，因此請為對話方塊設定另一個 `OpenGesture`，或將 `IsShortcutEnabled` 設為 false，再在你的處理常式中呼叫 `Open()`。

鍵帽與每個指令的 `Shortcut` 文字都只是標籤。面板不會繫結指令的 `Shortcut`：請在應用程式中繫結真正的按鍵，並放在執行該指令的視窗層級處理常式中，這樣鍵帽就不會顯示一個無效的快速鍵。

### 位置

面板寬 `PanelWidth`（預設 560），頂端距視窗頂邊 `PanelTopRatio` 倍視窗高度（0.13，且至少 16 像素）。在窄視窗中，它與兩側各保持 16 像素的距離，而且永遠不會延伸到視窗下方：清單改為捲動。篩選會改變面板的高度，但不會改變它頂邊的位置。

`IsModal` 決定對話方塊如何對待頁面：

| | `IsModal` 為 true（預設） | `IsModal` 為 false |
|---|---|---|
| 遮罩 | 遮罩蓋住頁面（`ScrimBrush`、`BackdropBlur`） | 無 |
| 頁面 | 在遮罩之後，無法操作 | 維持可互動 |
| 焦點 | 留在面板內 | 不受限制 |
| 在外部按下 | 按下遮罩會關閉（`CloseOnOutsidePress`） | 按下面板之外的任何位置都會關閉 |

`ScrimBrush` 預設是佈景主題提供的淡淡暗化。`BackdropBlur` 是以像素為單位的半徑，預設值為 0：不模糊。在「減少透明度」設定下，模糊會關閉，遮罩保留。

### 焦點

- 開啟時，焦點移到搜尋欄位，並記住它原本的位置。關閉時焦點會回到那裡，無論對話方塊是透過按鍵、點按還是指令關閉，只要那個元素仍在畫面上，且沒有其他元素搶走焦點。
- 當對話方塊以 Esc 關閉，或該元素在對話方塊開啟時顯示著焦點框，歸還的焦點會再次顯示焦點框。
- 對話方塊為模態時，焦點停留在其中：Tab 從搜尋欄位移到清除按鈕（有查詢時）與 "Esc" 鍵帽，然後回到搜尋欄位。
- 選擇指令時，對話方塊在 `ItemSelected` 與各個指令執行之前就已關閉，因此開啟另一個對話方塊的指令會發現焦點已經回到頁面。
- `IsModal` 為 false 時，頁面維持可互動，焦點不受限制。在面板之外的任何位置按下都會關閉它。
- 文字方塊擁有焦點時也可以開啟，因為對話方塊先於文字方塊處理手勢。關閉時，文字方塊會重新取得焦點。

### 單獨使用面板

`CommandPalette` 是對話方塊裡的那個控制項。當清單需要一直顯示時，把它內嵌在頁面或側邊面板中使用。它有相同的指令、文字與按鍵；開啟與關閉由你負責。設定 `ShowCloseButton` 以顯示 "Esc" 鍵帽，處理 `CloseRequested`，並設定 `AutoFocus`，讓面板出現時搜尋欄位取得焦點。

```xml
<arc:CommandPalette x:Name="Palette" MaxWidth="560" ShowCloseButton="True" AutoFocus="True">
    <arc:CommandPaletteItem Id="dashboard" Label="前往儀表板" Group="導覽" Shortcut="G D" />
    <arc:CommandPaletteItem Id="theme" Label="切換主題" Group="設定" />
</arc:CommandPalette>
```

```csharp
Palette.CloseRequested += (_, e) => Hide(e.Reason);
```

在空白的搜尋欄位中按 Escape 或點按 "Esc" 鍵帽都會引發 `CloseRequested`，它的 `Reason` 是 `CommandPaletteCloseReason.Escape` 或 `CommandPaletteCloseReason.CloseButton`。有查詢時按 Escape 會清除查詢，並被處理。在空白的搜尋欄位中，Escape 不會被標記為已處理，因此包住面板的覆蓋層也可以在 Escape 時關閉，對話方塊就是這樣做的。讓面板隱藏的方式由你決定：在這裡關閉它，並在 `ItemSelected` 之後同樣關閉。

在 macOS 上為 Cmd+K，其他平台為 Ctrl+K，可從視窗中的任何位置把焦點移到內嵌面板的搜尋欄位；它不會切換。當另一個不包含它的模態層位於最上層時，以及另一個面板擁有焦點時，面板會退讓。當一個頁面顯示多個面板時，請在除其中一個之外的所有面板上將 `IsShortcutEnabled` 設為 false。`ShortcutHint` 修改鍵帽的文字，空字串則隱藏它；`IsShortcutEnabled` 為 false 時鍵帽仍然可見，因此也要把它隱藏。

```xml
<arc:CommandPalette IsShortcutEnabled="False" ShortcutHint="" />
<arc:CommandPalette ShortcutHint="Ctrl K" />
```

之後要把焦點移入搜尋欄位時呼叫 `FocusInput()`，例如宿主第二次被要求開啟時；再次顯示面板之前請先呼叫 `Clear()`，這樣上一次的查詢與醒目提示不會帶過來。

### 長清單

清單是虛擬化的：只有可見部分附近的列真正存在，並且在捲動或篩選時被重複使用。一千個指令的開啟、篩選與捲動和十個一樣快。各列共用同一個高度：面板會測量已經繪製的列，並以這個高度估計其餘各列，因此請讓每一列都維持一行高，標題加描述本來就是如此。

對話方塊在第一次開啟時建立它的面板，並保留給下一次使用。對話方塊關閉期間，它不為清單做任何工作，只監聽開啟手勢。第一次開啟之前 `Palette` 為 null。

### 文字

所有文字都有英文預設值。`PlaceholderText` 是空白搜尋欄位中的提示，`Label` 為搜尋欄位命名，同時也為整個對話方塊命名。`EmptyTitle` 與 `EmptyDescription` 用來填入空狀態。`ClearButtonLabel`、`CloseButtonLabel` 與 `ResultsLabel` 分別為清除按鈕、關閉按鈕與結果清單命名，供輔助技術使用。應用程式不使用英文時請設定這些屬性，因為輔助技術會朗讀這些字串。

| 屬性 | 預設值 |
|---|---|
| `PlaceholderText` | Search commands |
| `Label` | Command palette |
| `EmptyTitle` | No matching actions |
| `EmptyDescription` | Try a different word or clear the search. |
| `DefaultGroup` | Actions |
| `ClearButtonLabel` | Clear search |
| `CloseButtonLabel` | Close command palette |
| `ResultsLabel` | Command results |
| `ShortcutHint` | macOS 與 iOS 上為 "⌘ K"，其他平台為 "Ctrl K"；對話方塊上則是開啟手勢 |

```xml
<arc:CommandPaletteDialog PlaceholderText="跳到頁面"
                          Label="快速切換"
                          EmptyTitle="找不到頁面"
                          EmptyDescription="請檢查拼字，或換一個詞試試。" />
```

### 範例：帳單營運主控台

一個帳單團隊在同一個主控台中工作，其中有發票、訂閱與退款三個頁面。主控台宣告了一個 `CommandPaletteDialog`，並用團隊已經習慣的同一個按鍵開啟它。這些指令是營運人員每天要做很多次的事：在三個頁面之間切換、匯出一個月的發票、查詢某個帳號。分組是簡短的名詞，標籤以動詞開頭；對於沒有退款權限的營運人員，退款指令處於停用狀態，並附有說明原因的描述。匯出指令的標籤已包含 "invoices"，因此輸入它的營運人員能立刻找到該指令。範例中的 `IsOpen` 讓對話方塊在檢視出現時就開啟，截圖就是這樣拍的；在主控台中，它由按鍵開啟。

![帳單主控台位於變暗的遮罩之後，指令面板已開啟，退款指令處於停用狀態](images/BillingConsole.png)

```xml
<Panel Width="720" Height="480">
    <StackPanel Margin="32,24" Spacing="4">
        <TextBlock Text="Billing"
                   FontSize="{arc:ArcDesignTokenResource TypeXL}"
                   FontWeight="{arc:ArcDesignTokenResource WeightMedium}"
                   Foreground="{arc:ArcDesignTokenResource Foreground}" />
        <TextBlock Text="Invoices, subscriptions and refunds for Northwind Cloud"
                   FontSize="{arc:ArcDesignTokenResource TypeSM}"
                   Foreground="{arc:ArcDesignTokenResource TextMuted}" />
    </StackPanel>
    <TextBlock Margin="32,0,32,24"
               VerticalAlignment="Bottom"
               Text="12 open invoices · 3 past due · $4,280.00 outstanding"
               FontSize="{arc:ArcDesignTokenResource TypeSM}"
               Foreground="{arc:ArcDesignTokenResource TextMuted}" />
    <arc:CommandPaletteDialog IsOpen="True"
                              PanelWidth="480"
                              PanelTopRatio="0.2"
                              Label="Billing commands"
                              PlaceholderText="Search billing actions"
                              ResultsLabel="Billing command results"
                              EmptyTitle="No billing actions found"
                              EmptyDescription="Try refund, invoice or plan.">
        <arc:CommandPaletteItem Id="go-invoices"
                                Label="Go to invoices"
                                Group="Navigation"
                                Icon="{x:Static arc:ArcIcons.FileText}"
                                Shortcut="G I" />
        <arc:CommandPaletteItem Id="go-subscriptions"
                                Label="Go to subscriptions"
                                Group="Navigation"
                                Icon="{x:Static arc:ArcIcons.CreditCard}"
                                Shortcut="G S" />
        <arc:CommandPaletteItem Id="export-invoices"
                                Label="Export invoices as CSV"
                                Description="Current month"
                                Group="Actions"
                                Icon="{x:Static arc:ArcIcons.Download}" />
        <arc:CommandPaletteItem Id="refund"
                                Label="Refund payment"
                                Description="Needs the refund permission"
                                Group="Actions"
                                Icon="{x:Static arc:ArcIcons.Undo2}"
                                IsEnabled="False" />
        <arc:CommandPaletteItem Id="lookup-account"
                                Label="Open account"
                                Description="By account ID, such as acct_2026_0412"
                                Group="Actions"
                                Icon="{x:Static arc:ArcIcons.Search}" />
    </arc:CommandPaletteDialog>
</Panel>
```

### 文案規範

- **指令標籤**：句首大寫的動詞加物件，例如「將發票匯出為 CSV」「前往帳單」「退款」。維持在兩到四個詞之內，因為該列還會顯示描述與快速鍵鍵帽。
- **導覽標籤**：以「前往」或「開啟」開頭，讓使用者知道該指令會帶他們到某處。
- **分組**：表示工作類型的簡短名詞，例如「導覽」「操作」「設定」「帳號」。分組的數量應保持較少，避免標題比指令還多。
- **描述**：同一列上的簡短說明：範圍、後果，或指令被停用的原因。數字用阿拉伯數字書寫。
- **關鍵字**：使用者會輸入、但標籤中沒有的同義詞，例如帳單場景中的「發票」，或登出的「登出帳號」。不要在關鍵字中重複標籤或分組。
- **預留位置文字**：寫明搜尋的範圍：「搜尋帳單操作」比單純的「搜尋」更能告訴使用者面板涵蓋什麼。

| 不要寫 | 改為 |
|---|---|
| 點這裡查看帳單 | 前往帳單 |
| 匯出 | 將發票匯出為 CSV |
| 設定 > 通知 > 電子郵件 | 電子郵件通知設定 |
| 深色模式切換 | 切換為深色模式 |
| 發票清單頁面 | 開啟發票 |

### 最佳做法

**建議**

- 為每個指令設定穩定的 `Id`。資料重新整理時各列維持原位，醒目提示也留在同一個指令上，清單就不會在使用者手下跳動。
- 為顯示的每個 `Shortcut` 繫結真正的按鍵，並放在視窗層級的處理常式中，讓鍵帽教的是一個有效的按鍵。
- 用 `Keywords` 放使用者會搜尋的字詞，標籤則作為指令的名稱。使用者會透過標籤確認自己的選擇。
- 用 `IsEnabled="False"` 停用指令，並搭配說明原因的 `Description`。隱藏的指令什麼也教不了使用者；帶有原因的停用指令則告訴使用者他們需要什麼。
- 每個視窗只保留一個指令面板對話方塊。第二個對話方塊需要自己的 `OpenGesture`，與它並存的內嵌面板需要設定 `IsShortcutEnabled="False"`，否則這個按鍵會有兩個爭用者，卻只有一個會回應。
- 在按鍵之外，再提供一個開啟對話方塊的按鈕。這樣不知道 Cmd+K 的使用者也能找到指令面板。
- 在 `ItemSelected` 或 `SelectCommand` 中處理所選指令，並預期對話方塊此時已在關閉。開啟 `Dialog` 的指令就無需自己處理焦點。

**避免**

- 把每個選單項目都複製進清單。指令面板用於使用者會搜尋的指令；把整個側邊欄列出來，只會讓搜尋比它所取代的側邊欄還慢。
- 對短查詢也能符合的破壞性指令。輸入後 Enter 會執行排在最前的符合項，因此「del」可能在使用者看清之前刪除了工作區。請在指令中為破壞性操作設定確認，或將其排除在面板之外。
- 為應用程式不處理的按鍵顯示快速鍵鍵帽。使用者複製了鍵帽卻找不到任何效果，這比沒有鍵帽更糟。
- 每次變動都取代整個清單。面板會跟隨集合及其中各項目的變化，因此應更新現有的 `ObservableCollection`，讓保留的列維持原位。
- 為保護尚未儲存的輸入而關閉 `CloseOnEscape` 或 `CloseOnOutsidePress`。使用者預期這兩種操作都能關閉彈出層；必須維持開啟的那一刻，請改為取消 `Closing`。

### 常見問題

**為什麼 Enter 會執行我沒有醒目提示的指令？** 輸入後若沒有醒目提示的指令，按 Enter 會執行排在最前面的符合項。輸入會清除醒目提示。要執行其他指令，請先按方向鍵，或點按該列。

**為什麼停用的指令不會執行？** `IsEnabled="False"` 的指令會被醒目提示略過，無法透過點按或 Enter 選取。輸入後按 Enter 會執行第一個啟用的符合項。

**為什麼按 Cmd+K 或 Ctrl+K 沒有反應？** 請檢查：`IsShortcutEnabled` 是否為 false，對話方塊上是否設定了不同的 `OpenGesture`，對話方塊外層的頁面或標籤頁是否被隱藏，視窗上是否有另一個模態層（`Dialog` 或另一個已開啟的指令面板對話方塊），頁面上是否有另一個對話方塊或面板先回應了同一個按鍵。只要其中一項成立，對話方塊就會忽略這個按鍵。

**為什麼我自己的 Cmd+K 處理常式不再作用？** 頁面上的對話方塊在視窗層級回應這個按鍵，先於它下方的控制項。請為對話方塊設定另一個 `OpenGesture`，或關閉它的快速鍵，再在你的處理常式中呼叫 `Open()`。

**能變更開啟指令面板的按鍵嗎？** 可以。在對話方塊上設定 `OpenGesture`。內嵌面板的聚焦按鍵固定為 Cmd/Ctrl+K：若要用其他按鍵開啟或聚焦它，請在視窗中處理該按鍵，並在面板上呼叫 `FocusInput()`，或在對話方塊上呼叫 `Open()`。

**如何透過程式碼關閉對話方塊？** 呼叫 `Close()`，執行 `CloseCommand`，或將 `IsOpen` 設為 false。`Close()` 與 `CloseCommand` 會引發 `Closing`，它可以取消這次關閉；設定 `IsOpen` 則不會。

**開啟期間如何更新清單？** 修改項目的 `IsEnabled`，或在 `ObservableCollection` 中新增和移除項目；面板會跟隨這兩種變化。

## API 參考

<!-- api: CommandPaletteDialog -->

<!-- api: CommandPalette -->

<!-- api: CommandPaletteItem -->

### 事件參數

| 型別 | 屬性 | 說明 |
|---|---|---|
| `CommandPaletteItemSelectedEventArgs` | `Item` | 被選取的指令。 |
| `CommandPaletteCloseRequestedEventArgs` | `Reason` | 一個 `CommandPaletteCloseReason`：`Escape` 表示在空白搜尋欄位中按下 Escape，`CloseButton` 表示點按了 "Esc" 鍵帽。 |
| `OverlayClosingEventArgs` | `Reason` | 一個 `DismissReason`：對話方塊為什麼要關閉。 |
| `OverlayClosingEventArgs` | `Cancel` | 設為 true 可讓對話方塊維持開啟。 |
| `OverlayClosedEventArgs` | `Reason` | 一個 `DismissReason`：對話方塊為什麼關閉了。 |

## 無障礙

- 對話方塊向輔助技術呈現為一個視窗，由 `Label` 命名。它的搜尋欄位使用同一個名稱，搜尋欄位的說明文字是目前醒目提示指令的標題。內嵌的面板則由 `Label` 為搜尋欄位命名，沒有自己的視窗。
- 結果清單由 `ResultsLabel` 命名。每一列是一個清單項目，以其標題與描述命名，並帶有它在集合中的位置；醒目提示的列會向輔助技術回報自己已被選取。分組標題是以分組名稱命名的群組。
- ArrowDown 與 ArrowUp 會移動醒目提示，並停在第一個與最後一個指令。Home 與 End 會直接跳到它們。已停用的指令會被略過。
- Enter 執行醒目提示的指令；輸入後則執行排在最前面的符合項。Escape 會清除查詢；在空白的搜尋欄位中，則會關閉對話方塊。一切操作都不需要滑鼠。
- Cmd/Ctrl+K 可從視窗中的任何位置開啟對話方塊，焦點隨即移到搜尋欄位。關閉時焦點回到原本的位置。
- 清除按鈕與 "Esc" 鍵帽可用 Tab 鍵到達，並會顯示焦點框。鍵帽是一個按鈕，由 `CloseButtonLabel` 命名；清除按鈕由 `ClearButtonLabel` 命名。
- 模態的對話方塊會讓其後的頁面無法透過鍵盤到達。`IsModal` 為 false 時，頁面仍然可以到達。
- 空狀態為純文字，因此螢幕閱讀器可以讀出。
- 在「減少透明度」設定下，背景模糊會關閉；遮罩保留。
- 請以應用程式的語言設定 `Label`、`ResultsLabel`、`ClearButtonLabel` 與 `CloseButtonLabel`。它們是輔助技術播報的名稱；可見的提示文字無法取代它們。

### 鍵盤

除了開啟手勢之外，以下按鍵在搜尋欄位取得焦點時生效。Home 與 End 移動的是醒目提示，而不是游標，因此要在搜尋欄位內移動游標，請使用 Left 與 Right。

| 按鍵 | 操作 |
|---|---|
| `Cmd+K`（macOS）、`Ctrl+K`（其他平台） | 從視窗中的任何位置開啟對話方塊；再按一次則關閉。在內嵌面板上，則把焦點移到搜尋欄位。當 `IsShortcutEnabled` 為 true 時生效；對話方塊的 `OpenGesture` 會取代這個按鍵。 |
| `Down` / `Up` | 將醒目提示移到下一個或上一個啟用的指令。沒有醒目提示時，Down 移到第一個指令，Up 移到最後一個。醒目提示停在兩端。 |
| `Home` / `End` | 將醒目提示移到第一個或最後一個啟用的指令。 |
| `Enter` | 執行醒目提示的指令；輸入後若沒有醒目提示的項目，則執行排在最前面的符合項。隨後對話方塊關閉。 |
| `Escape` | 有查詢時清除查詢。在空白的搜尋欄位中，關閉對話方塊（內嵌的面板則引發 `CloseRequested`）。 |
| `Tab` / `Shift+Tab` | 在搜尋欄位、清除按鈕（有查詢時）與 "Esc" 鍵帽之間移動。在模態的對話方塊中，焦點會在面板內循環。 |

## 動態效果

- 遮罩淡入。面板隨之淡入，並從稍小的縮放比例以平滑彈簧上移 6 像素。關閉時兩者短暫淡出。在結束動畫中再次開啟，會從目前的位置反向進行。
- 醒目提示只有一個，在各列之間移動：方向鍵使用短距離滑動，指標則使用快速彈簧效果。醒目提示首次出現的位置會淡入，沒有任何列被醒目提示時則淡出。
- 輸入時，保留的列會平滑滑到新位置，離開的列淡出，新增的列淡入。面板外框的高度會以平滑的彈簧效果跟隨清單變化。若列需要移動超過六個位置，則會直接一次到位。
- 結果先淡出，空狀態再升起出現，因此外框只改變一次高度。
- 清除按鈕會從較小的縮放比例與柔和的模糊中彈出。
- 動態效果等級為 Reduced 時，只保留淡入淡出。等級為 Off 時，所有變更立即生效。

| 變化 | 完整 | 減少 | 關閉 |
|---|---|---|---|
| 對話方塊開啟 | 遮罩淡入；面板淡入，並以平滑彈簧從 0.98 的縮放比例上移 6 px | 僅淡入 | 立即完成 |
| 對話方塊關閉 | 遮罩與面板短暫淡出 | 僅淡入淡出 | 立即完成 |
| 內嵌面板出現 | 淡入，並以平滑彈簧從 0.98 的縮放比例上移 6 px | 僅淡入 | 立即完成 |
| 方向鍵移動醒目提示 | 短距離滑動 | 不滑動；醒目提示立即切換 | 立即完成 |
| 指標移動醒目提示 | 快速彈簧 | 無彈簧；醒目提示立即切換 | 立即完成 |
| 輸入時列發生變化 | 保留的列滑動，離開的列淡出，新增的列淡入 | 僅淡入 | 立即完成 |
| 結果外框改變高度 | 平滑彈簧 | 立即完成 | 立即完成 |
| 結果與空狀態切換 | 結果淡出，然後空狀態升起出現 | 僅淡入 | 立即完成 |
| 清除按鈕出現 | 從較小的縮放比例與柔和的模糊中彈出 | 僅淡入 | 立即完成 |

## 相關元件

- `SearchResults`：用於具備篩選與摘錄的完整搜尋頁面。
- `ExpandingSearch`：用於頁首中的精簡搜尋欄位。
- `DropdownMenu`：用於單一物件的操作。
- `Combobox`：用於在表單中選擇一個值。
- `Dialog`：用於需要使用者做出決定的專注介面。

| 元件 | 適用情境 |
|---|---|
| `SearchResults` | 使用者搜尋紀錄或文件，並需要摘錄、篩選條件與結果頁 |
| `ExpandingSearch` | 頁首中的精簡搜尋用於篩選目前頁面或一個較短的清單 |
| `SearchField` | 搜尋框位於頁面內，其結果顯示在旁邊 |
| `DropdownMenu` | 少量操作作用於單一物件，例如某一列發票 |
| `Combobox` | 使用者選擇一個會被表單儲存的值 |
| `Select` | 使用者在表單中從一個很短的固定清單中選擇一個值 |
| `Dialog` | 使用者必須先回答一個問題或填寫簡短的表單，才能回到頁面 |
