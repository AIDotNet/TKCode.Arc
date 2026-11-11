---
title: 切換按鈕組
summary: 一排切換按鈕，用於單選或多個相互獨立的選項。
source-hash: bbe990e3c809
---

<Demo name="TextStyle" />

## 何時使用

切換按鈕組把幾個緊密相關的開關放在同一個扁平的列中。按下的選項沉入帶色調的填色裡，因此看起來是表面上的工具，而不是頁面之間的切換。

- **編輯器工具列的文字格式**：粗體、斜體和底線各自切換，`Mode="Multiple"` 讓相鄰且都按下的選項連成一個形狀。
- **對齊或版面這類顯示選項**：一次一個選項，填色滑向新的選擇。
- **清單上方的快速篩選**：可以同時生效多個，每個都只是一個短詞或一個圖示。
- **緊湊的工具面板**：`Size="Small"` 提供 28 px 的選項，仍能與小尺寸控制項對齊。

## 何時不用

- **在內容的不同檢視之間切換**：使用 `SegmentedControl`，凹陷軌道上凸起的滑塊表達「這個檢視」。
- **單個開或關的設定**：使用 `Switch`，它立即生效，不需要分組。
- **每個選項都需要說明的選擇**：使用 `RadioGroup` 或 `RadioCards`，它們有放說明的位置。
- **來自資料集的大量選項或篩選**：使用 `ChipGroup`，它可以換到多行。

## 用法

把選項寫在組內，或把 `ItemsSource` 繫結到 `ToggleGroupOption` 清單。在預設的單選模式下，`SelectedValue` 保存按下選項的 `Value`；再次按下時保持不變，與單選組一樣。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:ToggleGroup Label="Alignment" SelectedValue="{Binding Alignment}">
        <arc:ToggleGroupOption Value="start" Label="Start" />
        <arc:ToggleGroupOption Value="center" Label="Center" />
        <arc:ToggleGroupOption Value="end" Label="End" />
    </arc:ToggleGroup>
</UserControl>
```

`ValueChanged` 在使用者切換選項時觸發，程式碼設定值時不觸發。它的參數帶有被切換的 `Value`、該選項現在是否 `IsPressed`，以及全部 `Values`。

### 圖示與文字

`Icon` 接受 24×24 的描邊圖形（例如 `ArcIcons.List`），並以合適的尺寸繪製在內容之前。選項的內容就是它的可見文字。只有圖示的選項必須提供 `Label`：它會成為無障礙名稱和工具提示，工具提示需要不同文字時用 `Title` 替換。既無圖示也無內容的選項把 `Label` 顯示為文字，不帶工具提示。

<Demo name="Layout" />

### 同時多個選項

`Mode="Multiple"` 讓每個選項各自切換。`SelectedValues` 按按下的先後順序列出已按下的值；在 XAML 中寫成逗號分隔的清單。還沒有對應選項的值會保留，等選項出現時生效。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:ToggleGroup Label="Text style" Mode="Multiple" SelectedValues="bold,italic">
        <arc:ToggleGroupOption Value="bold" Label="Bold" />
        <arc:ToggleGroupOption Value="italic" Label="Italic" />
        <arc:ToggleGroupOption Value="underline" Label="Underline" />
    </arc:ToggleGroup>
</UserControl>
```

### 尺寸

`Size` 設定選項高度：`Small` 為 28 px，圖示 14 px；預設的 `Medium` 為 36 px，圖示 16 px。`Large` 與 `Medium` 外觀相同。

<Demo name="Sizes" />

### 停用選項

選項上的 `IsDisabled` 讓它不再回應鍵盤和指標，但仍保留在列中的位置。組上的 `IsEnabled="False"` 停用所有選項並讓組變淡。

<Demo name="Disabled" />

### 資料繫結的項目

選項不必是 `ToggleGroupOption` 物件。把 `ItemsSource` 繫結到你自己的項目，並為群組提供用於內容的 `ItemTemplate`。每個選項都是公開的 `ToggleGroupItem`，其 `DataContext` 就是它的項目，因此像 Avalonia 的 `ListBoxItem` 一樣，可以用針對 `arc|ToggleGroupItem` 的樣式把選項的其餘部分繫結到項目：`Icon`、`IsEnabled`、`AutomationProperties.Name` 和 `arc:Tooltip.Tip`。把 `SelectedValueBinding` 設為識別項目的屬性；未設定時，值就是項目本身。

```xml
<arc:ToggleGroup Label="View"
                 ItemsSource="{Binding Views}"
                 SelectedValue="{Binding ViewKey}"
                 SelectedValueBinding="{Binding Key}">
    <arc:ToggleGroup.ItemTemplate>
        <DataTemplate>
            <TextBlock Text="{Binding Title}" />
        </DataTemplate>
    </arc:ToggleGroup.ItemTemplate>
    <arc:ToggleGroup.Styles>
        <Style Selector="arc|ToggleGroupItem">
            <Setter Property="Icon" Value="{Binding Glyph}" />
            <Setter Property="AutomationProperties.Name" Value="{Binding Title}" />
        </Style>
    </arc:ToggleGroup.Styles>
</arc:ToggleGroup>
```

- 使用編譯繫結時，為 `DataTemplate` 和 `Style` 設定你的項目型別作為 `x:DataType`。
- 只顯示圖示的選項需要從樣式取得 `AutomationProperties.Name` 和工具提示（`arc:Tooltip.Tip`），就像 `Label` 為 `ToggleGroupOption` 提供它們一樣。
- 也可以使用 `ItemContainerTheme`。讓它基於選項的主題 `BasedOn="{StaticResource {x:Type arc:ToggleGroupItem}}"`，這樣選項會保留填色、按壓效果和焦點環。
- `ToggleGroupOption` 會自行設定所在選項的內容、圖示、名稱和工具提示，這些值優先於樣式。

## API 參考

<!-- api: ToggleGroup -->
<!-- api: ToggleGroupOption -->
<!-- api: ToggleGroupItem -->

## 無障礙

| 按鍵 | 操作 |
|---|---|
| Right、Down | 把焦點移到下一個選項，並循環 |
| Left、Up | 把焦點移到上一個選項，並循環 |
| Home、End | 把焦點移到第一個或最後一個可用選項 |
| Space、Enter | 切換取得焦點的選項 |

方向鍵只移動焦點，因此使用者可以到達某個選項而不改變任何內容。整個組是一個定位停駐點：上次取得焦點的選項，否則是第一個按下的選項，否則是第一個可用選項。`Label` 為組命名。單選模式下，選項回報為單選按鈕；多選模式下，選項回報為帶按下狀態的按鈕。`ToggleGroupOption` 以其 `Label` 命名；資料繫結的選項需要透過針對 `arc|ToggleGroupItem` 的樣式設定 `AutomationProperties.Name`。

## 動效

單選模式下，一個填色以 morph 彈簧在選項之間滑動。多選模式下，填色在 160 ms 內淡入，同時在 360 ms 內從 86% 縮放穩定下來；連接時在 240 ms 內跨過 2 px 間隙並把相接的圓角變成直角。按下選項時內容在 90 ms 內縮小到 92%，並在 320 ms 內恢復（`ToggleGroupItem` 的 Token `PressScale`、`PressDownDuration` 和 `PressUpDuration`）；選項的方框從不縮放，因此相鄰選項不會移動。減弱動效時，填色直接跳轉或在 120 ms 內淡入淡出，沒有縮放，顏色在 120 ms 內變化。

## 響應式

選項保持自身尺寸，不換行也不收縮，因此要讓這一列在最窄的版面中也放得下，或把較長的集合改用 `ChipGroup`。懸停顏色只對能夠懸停的指標出現，觸控時不出現。在手機上使用短標籤或圖示，中等尺寸提供 36 px 的目標。

## 相關元件

| 元件 | 適用情境 |
|---|---|
| `SegmentedControl` | 選擇會切換旁邊的檢視 |
| `Switch` | 單個設定的開或關 |
| `RadioGroup` | 每個選項都需要標籤和說明 |
| `ChipGroup` | 大量篩選項需要換到多行 |
