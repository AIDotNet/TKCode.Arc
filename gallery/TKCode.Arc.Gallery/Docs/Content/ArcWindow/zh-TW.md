---
title: 視窗
summary: 帶 Arc 標題列的桌面視窗：左、中、右三個區域放置你的控制項，保留原生拖曳、貼齊和縮放，標題列按鈕跟隨主題。
source-hash: b87bd013c4a2
---

<Demo name="Basic" />

## 何時使用

`ArcWindow` 用在視窗工作區內繪製的 Arc `TitleBar` 取代系統標題列，作用類似 WPF 的 `WindowChrome`。標題列使用主題的淺色和深色配色，你還可以在其中放置自己的控制項。視窗的行為仍和系統視窗一致：

- 拖曳標題列移動視窗，按兩下最大化或還原；
- 拖曳邊緣調整大小；
- 在 Windows 上，標題列就是系統標題列：按右鍵開啟系統選單，拖到螢幕邊緣貼齊，指向最大化按鈕開啟 Windows 11 的貼齊版面配置；
- 在 macOS 上保留紅綠燈按鈕，標題列為它們留出位置。

適用於桌面應用程式的主視窗：從頂邊開始是一整塊介面，標題列裡放選單、索引標籤、搜尋框或工具按鈕。

## 何時不用

- **瀏覽器和行動應用程式**：它們沒有視窗，把內容放在單一檢視中即可。
- **視窗內的對話方塊**：使用 `Dialog`、`Drawer` 或 `BottomSheet`，它們在視窗的覆蓋層中開啟。
- **需要和系統中其他視窗外觀一致的視窗**：一般的 Avalonia `Window` 保留系統標題列。

## 用法

讓主視窗衍生自 `ArcWindow` 而不是 `Window`。不做其他設定時，標題列顯示視窗的 `Title`，在 Windows 和 Linux 上還顯示最小化、最大化和關閉按鈕。

```xml
<arc:ArcWindow xmlns="https://github.com/avaloniaui"
               xmlns:arc="https://github.com/AIDotNet/TKCode.Arc"
               Title="Atlas" Width="1200" Height="800">
    <TextBlock Text="Content" />
</arc:ArcWindow>
```

```csharp
public partial class MainWindow : ArcWindow
{
    public MainWindow() => InitializeComponent();
}
```

視窗把工作區延伸到裝飾區域（`ExtendClientAreaToDecorationsHint` 為 true），並且在 Avalonia 原本會繪製標題列的平台上不再繪製（使用空的 `WindowDecorationsTheme`）。它向系統要求與 Arc 標題列等高的標題列，因此 macOS 會把紅綠燈按鈕垂直置中。內容從標題列下方開始。快顯層、工具提示和通知會顯示在標題列和內容之上，與其他 Arc 視窗相同。

### 在標題列中放置控制項

把視窗的 `TitleBar` 設為你自己的 `TitleBar`（在 XAML 中寫在 `<arc:ArcWindow.TitleBar>` 內），並填入它的三個區域：

- `LeftContent` 位於圖示和標題之後（選單、導覽）；
- `CenterContent` 在兩側放得下時置中於整個標題列（搜尋框、索引標籤）；
- `RightContent` 位於標題列按鈕之前（工具按鈕、主題切換）。

區域中的控制項自己處理點按；它們周圍的空白部分仍可用來移動視窗。

<Demo name="Areas" />

```xml
<arc:TitleBar>
    <arc:TitleBar.LeftContent>
        <arc:Button Content="File" Variant="Ghost" Size="Small" />
    </arc:TitleBar.LeftContent>
    <arc:TitleBar.CenterContent>
        <arc:Input Placeholder="Search" Size="Small" Width="220" />
    </arc:TitleBar.CenterContent>
    <arc:TitleBar.RightContent>
        <arc:Button Variant="Ghost" Size="Small">
            <arc:ArcGlyph Data="{x:Static arc:ArcIcons.Settings}" Size="15" />
        </arc:Button>
    </arc:TitleBar.RightContent>
</arc:TitleBar>
```

視窗變窄時，右側區域保持寬度，左側區域先讓出空間，中間區域向較寬的一側移動，然後縮小到兩側之間的空隙。請為中間內容設定 `Width` 或 `MaxWidth`。

`Icon` 顯示在標題之前，例如應用程式標誌的 `Image` 或一個 `ArcGlyph`。`Title` 跟隨視窗的 `Title`，除非你在標題列上另行設定。

### 高度

標題列高 40 px。在標題列上設定 `Height` 可以改變高度，視窗會把它告知系統。

```xml
<arc:TitleBar Height="48" />
```

### 標題列按鈕

`CaptionButtons` 決定由誰繪製最小化、最大化和關閉按鈕：

| 值 | 按鈕 |
|---|---|
| `Auto`（預設） | 在 macOS 上為 `System`，其他系統為 `Drawn` |
| `Drawn` | 由標題列在右端繪製。指標停留時關閉按鈕變紅。 |
| `System` | 由系統繪製（macOS 紅綠燈）；標題列在左側為它們留出位置 |
| `None` | 都不繪製 |

<Demo name="CaptionButtons" />

視窗最大化時，最大化按鈕顯示為還原。`CanResize` 或 `CanMaximize` 為 false 時最大化按鈕停用，`CanMinimize` 為 false 時最小化按鈕停用。

### 視窗置頂

在 `TitleBar` 上將 `ShowTopmostButton` 設為 `True`，標題列按鈕之前會出現一個圖釘開關。視窗的 `Topmost` 為 true 時開關處於選取狀態，切換開關會設定 `Topmost`，因此在程式碼中修改 `Topmost` 也會更新按鈕。預設關閉，且在任何 `CaptionButtons` 值下都會顯示。

```xml
<arc:TitleBar ShowTopmostButton="True" />
```

### 不顯示標題列

把 `IsTitleBarVisible` 設為 `False`（或把 `TitleBar` 設為 null），內容會延伸到視窗頂端；視窗仍延伸到裝飾區域，並可從邊緣調整大小。此時需要由你的內容來移動視窗，例如呼叫 `BeginMoveDrag`。同時把 `ExtendClientAreaToDecorationsHint` 設為 `False` 可以恢復系統標題列。

需要在自己的頁首裡放最小化、最大化和關閉按鈕時，使用 `WindowCaptionButtons`，並把內容放進頁首旁邊的 `ArcOverlayHost`，讓對話方塊和抽屜不會蓋住頁首（見「視窗標題列按鈕」）。以這種方式隱藏的標題列不參與點擊測試，它的按鈕不會與你的按鈕衝突。

### 視窗邊框

Windows 11 由 DWM 在視窗的左、右、下邊緣畫 1px 邊框，但用戶區延伸到標題列後不再畫頂邊。`ArcWindow` 在 Windows 上、視窗處於一般狀態時（最大化、全螢幕或 `WindowDecorations="None"` 時不畫）設定虛擬類別 `:top-edge`，自行畫出頂邊（顏色 `Border`），四邊保持一致。不需要這條線時用樣式覆寫：

```xml
<arc:ArcWindow xmlns="https://github.com/avaloniaui"
               xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:ArcWindow.Styles>
        <Style Selector="arc|ArcWindow:top-edge /template/ Border#PART_Root">
            <Setter Property="BorderThickness" Value="0" />
        </Style>
    </arc:ArcWindow.Styles>
    <!-- ... -->
</arc:ArcWindow>
```

### 預覽

只有作為 `ArcWindow` 的 `TitleBar` 時，`TitleBar` 才會控制視窗。放在其他位置的標題列（例如本頁的預覽）外觀相同，但不會移動、最大化或關閉任何視窗。

## API 參考

<!-- api: ArcWindow -->

<!-- api: TitleBar -->

<!-- api: TitleBarCaptionButtons -->

## 協助工具

- 標題列按鈕帶有工具提示和協助工具名稱（最小化、最大化或還原、關閉），使用應用程式目前的語言。
- 與系統標題列一樣，它們不在 Tab 順序中。鍵盤可以透過系統使用相同的命令（Windows 上 Alt+Space 開啟系統選單，Alt+F4 關閉視窗）。
- 其他視窗處於使用中狀態時，標題變暗。
- 置頂開關具有工具提示和無障礙名稱（置頂、取消置頂），位於 Tab 順序中，可用空白鍵或 Enter 切換。

## 動態效果

- 指標停留時，標題列按鈕的填色和圖示顏色漸變。
- 減少動效或關閉動效時，顏色立即變化。
- 最大化、還原和貼齊使用系統自己的動畫。

## 響應式

- `ArcWindow` 是桌面視窗，沒有斷點。它的 `MinWidth` 為 360 px（來自 ArcWindowToken），是視窗能夠縮小到的最窄寬度，此時標題與視窗按鈕仍能並排放下。
- 標題列寬度不足時，右側包含視窗按鈕的區域保持寬度，左側區域先讓位，中間區域先朝較寬的一側移動再收縮。請為中間內容設定 `Width` 或 `MaxWidth`；標題以省略號截斷。
- 視窗按鈕為 30 px（`CaptionButtonSize`），不要把它們當作觸控目標依賴。
- 手機與瀏覽器沒有視窗：應在那裡以單一檢視呈現內容，`ArcWindow` 留給桌面應用程式。

## 相關元件

- `Dialog`、`Drawer` 和 `BottomSheet`：在視窗之上開啟的內容。
- `WindowCaptionButtons`：內容延伸到頂端的視窗所用的最小化、最大化和關閉按鈕。
- `DropdownMenu`：標題列左側區域的選單。
- `Tabs`：標題列中間區域的索引標籤。
