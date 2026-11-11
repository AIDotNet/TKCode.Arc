---
title: 視窗標題列按鈕
summary: 內容延伸到頂端的視窗所用的最小化、最大化和關閉按鈕：放進你自己的頁首，按鈕跟隨視窗狀態，並在 Windows 上開啟貼齊版面配置。
source-hash: 4f02708a501c
---

<Demo name="Preview" />

## 何時使用

`WindowCaptionButtons` 把 Arc 標題列的最小化、最大化和關閉按鈕做成了一個獨立的控制項。視窗內容延伸到頂邊、由應用程式自己繪製頁首時使用它：例如 `IsTitleBarVisible="False"` 的 `ArcWindow` 中側邊欄一直延伸到頂端，頁首同時充當標題列，或是用一排索引標籤取代標題列。

這些按鈕像系統按鈕一樣控制所在的視窗：

- 最小化、最大化和關閉視窗，視窗最大化時最大化按鈕顯示為還原；
- `CanResize` 或 `CanMaximize` 為 false 時最大化按鈕停用，`CanMinimize` 為 false 時最小化按鈕停用；
- 在 Windows 上，它們會告訴系統自己是什麼按鈕，因此指向最大化按鈕會開啟 Windows 11 的貼齊版面配置。

## 何時不用

- **保留 Arc 標題列的視窗**：`ArcWindow` 的 `TitleBar` 已經繪製同樣的按鈕。每個視窗只用一組。
- **macOS**：系統在每個視窗左上角保留紅綠燈按鈕。在 macOS 上隱藏這些按鈕（見「平台」）。
- **瀏覽器和行動應用程式**：它們沒有視窗，把內容放在單一檢視中即可。
- **關閉面板或對話方塊**：使用 `Dialog` 或 `Drawer` 的關閉按鈕，或一個圖示 `Button`。

## 用法

隱藏 `ArcWindow` 的標題列，把按鈕放在你自己頁首的右端。視窗仍把工作區延伸到裝飾區域，不繪製自己的標題列，並可從邊緣調整大小。

```xml
<arc:ArcWindow xmlns="https://github.com/avaloniaui"
               xmlns:arc="https://github.com/AIDotNet/TKCode.Arc"
               Title="Atlas" IsTitleBarVisible="False">
    <DockPanel>
        <Grid DockPanel.Dock="Top" Height="48" ColumnDefinitions="*,Auto">
            <TextBlock Text="Inbox" Margin="20,0" VerticalAlignment="Center" />
            <arc:WindowCaptionButtons Grid.Column="1" Margin="0,0,8,0" />
        </Grid>
        <TextBlock Text="Content" />
    </DockPanel>
</arc:ArcWindow>
```

使用設定了 `ExtendClientAreaToDecorationsHint` 的一般 Avalonia `Window` 也可以，但在 Windows 和 Linux 上，Avalonia 會在你的內容之上再繪製一個自帶標題列按鈕的標題列。`ArcWindow` 會關閉這個標題列。

預覽下方的「Open a window」會開啟這樣一個視窗：頁首裡有搜尋框和這些按鈕，內容放在覆蓋層宿主中。

### 選擇按鈕

`ShowMinimizeButton`、`ShowMaximizeButton` 和 `ShowCloseButton` 預設都為 `True`。把其中一個設為 `False` 即隱藏對應按鈕；其餘按鈕靠攏，隱藏的按鈕不佔空間。尺寸固定的視窗顯示最小化和關閉按鈕，工具視窗只顯示關閉按鈕。

<Demo name="Buttons" />

### 移動視窗

這些按鈕不會移動視窗，由你的頁首負責。在 Windows 上，為頁首設定 `TitleBar` 元素角色，為頁首裡的控制項設定 `User` 角色；頁首就成為系統標題列：拖曳它移動視窗，按兩下最大化，按右鍵開啟系統選單，拖到螢幕邊緣貼齊。為頁首設定背景（`Transparent` 即可），讓它的空白部分能被點擊到。在 macOS 和 Linux 上，改為在 `PointerPressed` 處理常式中移動視窗：在那裡系統只會對直接點擊到的元素生效，或者吞掉按兩下。

```csharp
if (OperatingSystem.IsWindows())
{
    WindowDecorationProperties.SetElementRole(header, WindowDecorationsElementRole.TitleBar);
    WindowDecorationProperties.SetElementRole(search, WindowDecorationsElementRole.User);
}
header.PointerPressed += (_, e) =>
{
    // On Windows a press on the caption never gets here.
    if (e.Handled || !e.GetCurrentPoint(header).Properties.IsLeftButtonPressed)
    {
        return;
    }
    if (e.ClickCount == 2)
    {
        window.WindowState = window.WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    }
    else
    {
        window.BeginMoveDrag(e);
    }
    e.Handled = true;
};
```

`WindowDecorationProperties` 位於 `Avalonia.Controls.Chrome`，`WindowDecorationsElementRole` 位於 `Avalonia.Input`。`PageHeader`（在 `TKCode.Arc.Blocks` 中）在 `IsTitleBar` 為 `True` 時會完成以上全部工作。

### 讓模態面板留在標題列下方

`Dialog`、`Drawer`、`BottomSheet` 等模態面板在視窗的覆蓋層上開啟，覆蓋整個視窗，也包括你的頁首，因此模態面板開啟期間，標題列按鈕和拖曳區域都在遮罩之下。要讓它們始終可用，把應用程式內容放進 `ArcOverlayHost`，把頁首放在它旁邊、宿主之外。這樣從內容中開啟的模態面板只覆蓋宿主，永遠不會蓋住頁首。

```xml
<arc:ArcWindow xmlns="https://github.com/avaloniaui"
               xmlns:arc="https://github.com/AIDotNet/TKCode.Arc"
               Title="Atlas" IsTitleBarVisible="False">
    <DockPanel>
        <!-- Outside the host: never under a scrim. -->
        <Grid DockPanel.Dock="Top" Height="48" ColumnDefinitions="*,Auto">
            <TextBlock Text="Atlas" Margin="20,0" VerticalAlignment="Center" />
            <arc:WindowCaptionButtons Grid.Column="1" Margin="0,0,8,0" />
        </Grid>
        <!-- The app: dialogs, drawers and sheets opened in here cover this area only. -->
        <arc:ArcOverlayHost>
            <TextBlock Text="Content" />
        </arc:ArcOverlayHost>
    </DockPanel>
</arc:ArcWindow>
```

應用程式需要始終顯示在模態面板之上的其他內容（例如啟動畫面）也用同樣的方式放在宿主旁邊。快顯視窗、選單、工具提示和通知仍在視窗的覆蓋層上開啟。在預覽開啟的視窗中選擇「Open a dialog」即可看到效果。

### 平台

- **Windows**：按鈕由應用程式繪製，但行為與系統按鈕相同：按下它們就是系統的最小化、最大化或關閉，指向最大化按鈕會開啟 Windows 11 的貼齊版面配置。
- **macOS**：系統保留紅綠燈按鈕。在 macOS 上隱藏這些按鈕，並在頁首左側為紅綠燈留出約 80 px。
- **Linux**：按鈕的行為與 Windows 相同，但沒有貼齊版面配置。

```xml
<arc:WindowCaptionButtons xmlns="https://github.com/avaloniaui"
                          xmlns:arc="https://github.com/AIDotNet/TKCode.Arc"
                          IsVisible="{OnPlatform True, macOS=False}" />
```

### 每個視窗一組按鈕

一個視窗只顯示一組標題列按鈕。如果有兩組最大化和關閉按鈕，Windows 對標題列按鈕和貼齊版面配置的點擊測試就無法分辨哪一組屬於視窗。`ArcWindow` 的 `TitleBar` 在 `CaptionButtons` 為 `System` 或 `None` 時會放棄其按鈕的角色，用 `IsTitleBarVisible="False"` 隱藏的標題列完全不參與點擊測試，因此在這些視窗中你的按鈕是唯一的一組。放在 `CaptionButtons="None"` 的標題列的 `RightContent` 中的按鈕同樣會控制視窗。

### 預覽

`IsPreview="True"` 只繪製按鈕而不控制視窗：它們不會最小化、最大化或關閉任何視窗，系統也不會把它們當作標題列按鈕。本頁的示意圖都使用了它，因此不會影響 Gallery 視窗。

## API 參考

<!-- api: WindowCaptionButtons -->

## 協助工具

- 每個按鈕都帶有工具提示和協助工具名稱，使用應用程式目前的語言：最小化、最大化或還原、關閉。
- 與系統標題列一樣，這些按鈕不在 Tab 順序中。鍵盤可以透過系統使用相同的命令：Windows 上 Alt+Space 開啟系統選單，Alt+F4 關閉視窗，Windows+向上鍵最大化視窗。
- 視窗不允許的操作（不能調整大小或最小化的視窗）對應的按鈕處於停用狀態，圖示變暗。

## 動態效果

- 指標停留時，按鈕的填色和圖示顏色漸變；關閉按鈕變為危險色。
- 減少動效或關閉動效時，顏色立即變化。
- 最小化、最大化和貼齊使用系統自己的動畫。

## 響應式

- 按鈕為 30 px 的正方形，間距 4 px（WindowCaptionButtonsToken 中的 `ButtonSize` 和 `ButtonSpacing`）：三個按鈕共 98 px。在任何寬度下都要在頁首末端留出這些空間，讓頁首自己的內容先讓位。
- 隱藏的按鈕不佔空間，所以控制項的寬度就是它顯示的按鈕的寬度。
- 按鈕太小，不要把它們當作觸控目標依賴。手機和瀏覽器沒有視窗，只在桌面應用程式中顯示它們。

## 相關元件

- `ArcWindow`：搭配使用的視窗，設定 `IsTitleBarVisible="False"`。
- `PageHeader`（TKCode.Arc.Blocks）：`IsTitleBar="True"` 讓頁首成為視窗的標題列。
- `Dialog`、`Drawer` 和 `BottomSheet`：覆蓋最近的 `ArcOverlayHost` 或整個視窗的模態面板。

| 元件 | 適用情境 |
|---|---|
| `ArcWindow` | 視窗保留 Arc 標題列，標題列在你的控制項旁繪製同樣的按鈕 |
| `PageHeader` | 帶麵包屑、標題和操作的頁首同時充當視窗的標題列 |
| `Dialog` | 任務會打斷頁面；放在 `ArcOverlayHost` 中時不會蓋住標題列 |
| `Button` | 面板或卡片需要自己的關閉按鈕 |
