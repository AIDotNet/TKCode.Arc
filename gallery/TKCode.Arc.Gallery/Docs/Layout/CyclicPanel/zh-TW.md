---
title: 循環面板
summary: 首尾無縫循環的清單：最後一個項目之後又是第一個，可用滾輪、觸控板或程式碼捲動，並回報目前可見的項目。
source-hash: 2baee2bd3be5
---

<Demo name="Preview" />

## 何時使用

`CyclicPanel` 把子元素顯示成無盡的循環：捲過最後一個子元素會回到第一個，兩個方向都可以，沒有跳動，也不需要複製資料。
可視區域裡有固定數量、大小相等的槽位。

- **滾輪選擇器**：小時、分鐘、月份等循環的值。
- **輪轉的條帶**：卡片、媒體或捷徑。
- **時間軸、日期條**：在一週裡循環。

## 何時不用

- **有明確開頭與結尾的清單**：請用 `ScrollViewer` 或 `ScrollArea`。
- **帶分頁圓點和自動播放的分頁投影片**：請用 `Carousel`。
- **成千上萬個項目**：面板會建立每一個子元素，不做虛擬化。

## 用法

`CyclicPanel` 衍生自 Avalonia 的 `Panel`，自己處理捲動，不需要 `ScrollViewer`。它把長度分成 `VisibleItemCount` 個槽位（預設 5 個），
槽位之間的間距為 `Spacing`（0）；400 px 高、五個槽位的面板，每個項目得到 80 px。子元素在橫向上填滿面板。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:CyclicPanel Orientation="Vertical" VisibleItemCount="5" Height="400">
        <TextBlock Text="Item 0" />
        <TextBlock Text="Item 1" />
        <TextBlock Text="Item 2" />
        <TextBlock Text="Item 3" />
        <TextBlock Text="Item 4" />
        <TextBlock Text="Item 5" />
    </arc:CyclicPanel>
</UserControl>
```

捲動位置是 `Offset`，單位是項目：2.5 表示前緣位於第三個項目的一半。靜止時它一律被折回 0 到項目數之間，所以無論捲動多久都不會越來越大。

每個子元素只放一次，放在離可視區域最近的位置。一個控制項不能同時出現在兩處，所以至少有 `VisibleItemCount` + 1 個可見子元素時循環才是無縫的。
少於這個數量時，每個子元素仍然顯示一次，其餘槽位留空。摺疊的子元素（`IsVisible="False"`）不參與循環。

### 橫向

`Orientation="Horizontal"` 讓槽位由左到右排列。觸控板可以橫向捲動它，垂直方向的滾輪也能捲動它。

<Demo name="Horizontal" />

### 可見項目數

修改 `VisibleItemCount` 會重新計算槽位大小，捲動位置維持不變。

<Demo name="VisibleItemCount" />

### 用程式碼捲動

- `ScrollToIndex(index)` 把子元素捲到前緣（頂端或左側）。
- `ScrollToIndexCentered(index)` 把它捲到中間的槽位；`VisibleItemCount` 為偶數時，它置中於中間兩個槽位之間。
- `ScrollNext()` 和 `ScrollPrevious()` 移動一個項目，`ScrollBy(items)` 移動任意數量。捲動進行中再次呼叫會累加到它的目標上，
  所以快速連續呼叫不會遺失。

每次捲動都走循環中較短的那條路：八個項目中從第 0 個到第 7 個是往回退一個，而不是往前走七個。索引超出子元素範圍或子元素已摺疊時，
索引方法傳回 `false` 且不做任何事。傳入 `animated: false` 可以直接跳轉。

<Demo name="ScrollToIndex" />

### 滾輪與觸控板

滑鼠滾輪每轉一格移動一個項目。觸控板或平滑滾輪跟隨手指移動，手指停下後面板停靠到最近的項目（`SnapsToItems`，預設為 true）。
`IsWheelEnabled="False"` 把滾輪留給父項。

### 可見項目

`VisibleItemIndices` 依顯示順序（前緣在前）列出可視區域中子元素的索引；只要槽位有任何部分在面板內就算可見，所以捲到一半時會多回報一個。
它是 Avalonia 屬性，只在清單改變時才改變，可以繫結或監看它來預先載入圖片、追蹤目前項目。

```csharp
panel.PropertyChanged += (_, e) =>
{
    if (e.Property == CyclicPanel.VisibleItemIndicesProperty)
    {
        Preload(panel.VisibleItemIndices);
    }
};
```

### 動畫

程式碼發起的捲動與滾輪格捲動會以 `AnimationEasing`（Arc 標準緩出曲線）在 `AnimationDuration`（240 ms）內平順過渡。
`IsAnimationEnabled="False"` 讓它們直接跳轉。`AnimationEasing` 接受任何 Avalonia 緩動，例如 `CubicEaseInOut`，也接受三次 Bézier 曲線的控制點。

<Demo name="Animation" />

### 在 ItemsControl 中使用

作為 `ItemsPanel` 時，面板循環的是項目容器，索引就是項目索引。`ObservableCollection` 中新增或移除的項目會加入或離開循環，捲動位置維持在循環之內。

<Demo name="ItemsControl" />

## API 參考

<!-- api: CyclicPanel -->

## 協助工具

- 面板本身不增加語意，也不處理鍵盤；它透過滾輪、觸控板與自身的方法捲動。要做鍵盤操作的選擇器，請在自己的按鍵處理中呼叫
  `ScrollNext()` 和 `ScrollPrevious()`（例如在取得焦點的項目上按上、下方向鍵）。
- 捲出可視區域的項目仍是子元素，仍在 Tab 順序中；可互動的項目請提供各自的協助工具名稱（`AutomationProperties.Name`）。

## 動態效果

- 捲動使用動態效果引擎的補間，以 `AnimationEasing` 在 `AnimationDuration` 內過渡，走循環中較短的那條路；新的捲動從目前進行中的位置開始。
- 觸控板捲動直接跟隨手指，停下後平順停靠到最近的項目。
- 減少動態效果或關閉動態效果時，所有捲動都直接跳轉：平順捲動會移動內容，而減少動態效果會去掉移動。

## 響應式

- 槽位一律平分面板的長度，項目隨之變大變小；槽位數維持為 `VisibleItemCount`。
- 長度無界時（例如在沿捲動方向排列的 `StackPanel` 中），每個槽位與最長的項目一樣長。請給面板一個尺寸，或把它放進 `Grid` 儲存格。

## 相關元件

- `Carousel`：帶圓點、箭頭和自動播放的分頁投影片。
- `WaterfallPanel`：子元素高度不限的等寬欄。
- `TilePanel`：磁磚可跨列跨欄的格線。
