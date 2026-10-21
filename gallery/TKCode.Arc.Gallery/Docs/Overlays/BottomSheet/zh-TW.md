---
title: 底部面板
summary: 停駐在預覽高度或完整高度的面板，跟隨手指移動。
source-hash: cdd9c3628e9f
---

<Demo name="Detents" />

## 何時使用

- 次要的行動裝置優先任務，例如行程詳情、篩選或分享選項。
- 先顯示一小部分內容、再展開到接近全高的內容。
- 地圖或媒體檢視中，使用者在不同高度之間拖曳面板。

## 何時不用

- 需要打斷整個螢幕的決定：用 `Dialog`。
- 寬螢幕版面中保持頁面可見的側邊面板：用 `Drawer`。
- 錨定在某個控制項上的小塊內容：用 `Popover`。

## 用法

`BottomSheet` 從底邊升起，並停駐在一個或多個高度上，這些高度稱為停駐點（detent）。預設停駐點為視窗高度的 0.45 與 0.92。

### 基本面板

```xml
<arc:BottomSheet Title="里斯本之旅"
                 Description="3 晚 · 6 月 12 日至 15 日"
                 IsOpen="{Binding ShowTrip}">
    <StackPanel Spacing="12">
        <TextBlock Text="週五 · 14:05 抵達 LIS，搭計程車前往 Alfama" />
        <TextBlock Text="週六 · 28 號電車與城堡" />
    </StackPanel>
</arc:BottomSheet>
```

### 停駐點

`Detents` 以視窗高度的比例列出停駐高度。使用 `BottomSheetDetents.Parse` 從字串設定它們，並用 `InitialDetentIndex` 選擇面板開啟時所在的停駐點。只有一個停駐點時，面板高度固定。

<Demo name="ThreeDetents" />

```csharp
share.Detents = BottomSheetDetents.Parse("0.3, 0.6, 0.95");
share.InitialDetentIndex = 1;
```

### 開啟並在停駐點之間移動

`OpenCommand` 可從按鈕開啟面板。`Expand`、`Collapse` 與 `SnapTo` 可在停駐點之間移動面板，`DetentChanged` 會回報每個停駐位置。

```csharp
openTrip.Command = trip.OpenCommand;

trip.DetentChanged += (_, e) => detent.Text = $"停在 {e.Fraction:P0}";
trip.Expand();
```

### 長內容

內容不做虛擬化。長清單請分頁載入，待面板完全展開後，由面板捲動內容。

```xml
<arc:BottomSheet Title="行程安排" IsOpen="{Binding ShowItinerary}">
    <ItemsControl ItemsSource="{Binding Items}" />
</arc:BottomSheet>
```

### 關閉

`Closed` 會回報面板關閉的原因，例如因快速滑動或向下拖曳而關閉的 `Gesture`、`PressOutside`，或 `Escape`。

```csharp
trip.Closed += (_, e) => detent.Text = $"關閉方式：{e.Reason}";
```

## API 參考

<!-- api: BottomSheet -->

<!-- api: BottomSheetDetents -->

## 無障礙

- 面板的名稱來自 `Title`，說明文字來自 `Description`。焦點首先落在面板本身。
- 拖曳把手是一個按鈕，會公開其展開狀態。其名稱為「展開面板」或「收合面板」。
- 在拖曳把手上，ArrowUp 與 ArrowDown 每次移動一個停駐點，Home 與 End 分別跳到最高與最低的停駐點，Enter 或空白鍵則在最低與最高之間切換。
- 停駐點的變化以禮貌方式播報。Escape 關閉面板。關閉按鈕預設名為「關閉」；設定 `CloseButtonName` 可更改它。

## 動態效果

- 一個動態值驅動面板。拖曳時面板跟隨指標；超過最高停駐點後，面板會以最多 120px 的橡皮筋效果拉伸。
- 放開後，面板會停駐到最近的停駐點；向下拖曳時則關閉。速度超過 320px/s 的快速滑動，一律至少移動一個停駐點。
- 面板背後的遮罩取決於面板位置。在最低停駐點時保留完整遮罩的 78%，隨面板下移而淡出。
- 減少動態效果時，停駐點直接跳轉，面板淡入淡出。關閉動態效果時，面板立即顯示或移除。

## 相關元件

- `Drawer`：寬螢幕版面中的側邊面板。
- `Dialog`：必須先回應才能進行其他操作的決定。
- `Popover`：錨定在某個控制項上的小塊內容。
