---
title: 浮動按鈕群組
summary: 將柔和按鈕分隔放置在安靜的托盤中，一個共用的反白區塊隨游標在按鈕之間變形移動，按下狀態則原地回落。
source-hash: 17bd9cf8be73
---

<Demo name="MutedTray" />

## 何時使用

- 浮動編輯器或畫布上的操作，例如復原、重做、分享和簡報。
- 出現在文字上方的選取工具列，包含僅有圖示的按鈕與快速鍵提示。
- 畫布旁的垂直工具列。

## 何時不用

- 保持選取狀態的選擇，例如文字對齊方式：用 Toggles 中的 `SegmentedControl`。
- 放不進所在列的工具列：用 Menus 中的 `DropdownMenu` 承載溢出的內容。
- 用分隔線連接成同一表面的相關操作：用 `ButtonGroup`。
- 單一操作：用 `Button`。

## 用法

每個按鈕都是一個 `FloatingButtonGroupItem`。為托盤設定 `Label`，它就是托盤的無障礙名稱。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:FloatingButtonGroup Label="看板操作">
        <arc:FloatingButtonGroupItem Label="復原" Shortcut="⌘Z" IsIconOnly="True" />
        <arc:FloatingButtonGroupItem Label="重做" Shortcut="⇧⌘Z" IsIconOnly="True" />
        <arc:FloatingButtonGroupSeparator />
        <arc:FloatingButtonGroupItem Label="分享" />
    </arc:FloatingButtonGroup>
</UserControl>
```

### 圖示與僅圖示按鈕

在程式碼中將 `Icon` 設為 24 x 24 的 Lucide 圖形。項目上的 `IsIconOnly` 會隱藏其標籤。在托盤上設定 `IsIconOnly`，可讓每個按鈕都只顯示圖示。項目自身的設定優先於托盤的設定。僅圖示的按鈕以及帶有 `Shortcut` 的按鈕，會在工具提示中顯示其標籤。

<Demo name="IconOnly" />

```csharp
bold.Icon = ArcIcons.Bold;
link.Icon = ArcIcons.Link;
```

`Shortcut` 只顯示提示。控制項不會綁定按鍵；請自行處理按鍵。

### 標籤變化與保留寬度

將 `ReserveLabels` 設為按鈕可能顯示的其他標籤，例如 "已複製"。按鈕隨後會保持最寬標籤的寬度，因此相鄰的按鈕不會移動。

```csharp
var share = new FloatingButtonGroupItem { Label = "分享", ReserveLabels = ["已複製"] };
share.Click += async (_, _) =>
{
    share.Label = "已複製";
    await Task.Delay(1600);
    share.Label = "分享";
};
```

### 來自資料的項目與垂直配置

`ItemsSource` 可將一般資料轉為按鈕。`Orientation="Vertical"` 會將按鈕垂直堆疊成一欄，此時工具提示預設向右展開。設定 `TooltipPlacement` 可更改展開的方向。

<Demo name="FromItemsSource" />

<Demo name="Vertical" />

```xml
<arc:FloatingButtonGroup Label="縮放" Size="Small" ItemsSource="{Binding ZoomLevels}" />

<arc:FloatingButtonGroup Label="工具" Orientation="Vertical" IsIconOnly="True">
    <arc:FloatingButtonGroupItem Label="選取" Shortcut="V" />
    <arc:FloatingButtonGroupItem Label="畫筆" Shortcut="P" />
</arc:FloatingButtonGroup>
```

### 事件

`ActionInvoked` 會在項目自身的 `Click` 處理常式與命令之後執行。`e.Item` 是執行的按鈕，`e.Data` 則是來源項目。

```csharp
board.ActionInvoked += (_, e) => log.Text = $"操作：{e.Item.Label}";
```

## API 參考

<!-- api: FloatingButtonGroup -->

## 無障礙

- 托盤具有 ToolBar 角色，由 `Label` 命名，並回報其方向。
- 托盤是單一的 Tab 停駐點。焦點會回到上一次取得焦點的按鈕。
- 方向鍵在可用的按鈕之間移動，並且會循環。Home 與 End 分別移到第一個與最後一個按鈕。
- Enter 與空白鍵啟動按鈕。已勾選的按鈕會回報其切換狀態。
- 每個按鈕都以其標籤命名。分隔符會被朗讀為分隔符。快速鍵提示會作為說明文字公開。
- 停用的按鈕仍可讀取，方向鍵會略過它們。

## 動態效果

- 一個反白區塊會在位置與尺寸上變形，移到游標或鍵盤焦點所在的按鈕。當游標跨過間隙或分隔符時，反白區塊保持不動。
- 托盤出現時，反白區塊會從按鈕中長出來。游標離開托盤時，反白區塊淡出。
- 按下的按鈕會使反白區塊變暗，反白區塊隨之略微收縮。按鈕本身不縮放。
- 標籤變化在原地交叉淡化。帶有保留標籤的按鈕會維持寬度；否則反白區塊會跟隨新的寬度變化。
- 減少動態效果時，反白區塊以短暫的淡入移到目標位置，標籤則在不位移的情況下交叉淡化。關閉動態效果時，所有內容立即到達最終位置。

## 相關元件

- `ButtonGroup`：用分隔線連接成同一表面的相關操作。
- `ExpandingButtonGroup`：緊湊的圖示按鈕，標籤在取得焦點時展開。
- `SegmentedControl`：保持選取狀態的選擇。
- `Button`：單一操作。
