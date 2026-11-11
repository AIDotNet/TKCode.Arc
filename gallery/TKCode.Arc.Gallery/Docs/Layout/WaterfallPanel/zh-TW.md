---
title: 瀑布流面板
summary: 瀑布流（Masonry）面板：高度不一的卡片流入等寬的欄，每張都放進目前最短的那一欄，排列緊密、沒有空隙。
source-hash: e167720d49f0
---

<Demo name="Preview" />

## 何時使用

`WaterfallPanel` 把高度不一的子元素排進等寬的欄（或把寬度不一的子元素排進等高的列），不會像 `WrapPanel` 或
`UniformGrid` 那樣在列與列之間留下空隙。每個子元素都放到目前最短那條通道的末端，所以各欄長度保持接近。

- **圖片牆、靈感板**：圖片保持各自的長寬比。
- **筆記、卡片清單**：卡片裡的文字長短不一。
- **資源瀏覽器、儀表板**：卡片高度各不相同。

## 何時不用

- **需要橫跨各列的閱讀順序**：瀑布流是沿著欄往下讀，不是橫著讀。請用 `WrapPanel` 或格線。
- **需要橫跨多欄的磁磚**：請用 `TilePanel`。
- **成千上萬個項目**：面板會建立每一個子元素，不做虛擬化。

## 用法

`WaterfallPanel` 衍生自 Avalonia 的 `Panel`。可以直接放子元素，也可以作為 `ItemsControl` 的 `ItemsPanel`。
`Orientation="Vertical"`（預設）時，寬度被分成 `Columns` 欄（預設 3 欄），欄間距為 `Spacing`（12 px）；每個子元素依欄寬測量，
保留自己的高度。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <ScrollViewer>
        <arc:WaterfallPanel Columns="3" Spacing="10">
            <Border Height="120" Background="SteelBlue" />
            <Border Height="200" Background="Orange" />
            <Border Height="150" Background="SeaGreen" />
            <Border Height="90" Background="Purple" />
        </arc:WaterfallPanel>
    </ScrollViewer>
</UserControl>
```

依子元素順序，最短通道優先：子元素放進末端最靠上的那一欄，兩欄一樣短時取較前面的一欄。因此同樣的子元素總是得到同樣的版面，
在末尾加入子元素也不會移動前面的元素。尺寸預設來自子元素自己的測量結果；`ItemSizing="Varied"` 改由面板決定
（見[錯落尺寸](#varied-sizes)）。面板的高度等於最長的那一欄，所以放在 `ScrollViewer` 裡能正確捲動。

摺疊的子元素（`IsVisible="False"`）不佔位置。新增或移除子元素、子元素尺寸改變或修改任何屬性，面板都會重新配置。

### 橫向

`Orientation="Horizontal"` 交換兩個軸：高度被分成 `Rows` 列（預設 3 列），子元素保留自己的寬度，放到最短那一列的末端。
請把它放進橫向捲動的 `ScrollViewer`。

<Demo name="Horizontal" />

### 通道數

`Columns`（橫向時為 `Rows`）設定通道數。設為 `0` 時依寬度放下盡可能多的通道，每條至少 `MinLaneSize`（預設 200 px）寬：
窄視窗一兩欄，寬視窗五六欄。`LaneCount` 回報上一次配置實際使用的通道數。

<Demo name="Columns" />

### 子元素尺寸限制

`MinItemSize` 和 `MaxItemSize` 限制每個子元素沿排列方向的尺寸（欄中即高度）。子元素在 `MaxItemSize` 之內測量，
所以換行的文字會依實際能得到的空間排版。子元素自己的 `MinHeight`（列中為 `MinWidth`）一律優先於 `MaxItemSize`，
內容不會被裁到它要求的尺寸以下。

<Demo name="ItemSize" />

### 錯落尺寸

所有資料項使用同一個範本時，它們的高度相同，面板排出來就是整齊的格線。設定 `ItemSizing="Varied"` 後面板不再看內容的尺寸：
它在 `MinItemSize` 與 `MaxItemSize` 之間為每個子元素分配高度（列中為寬度），同一範本的卡片也能排出瀑布流。子元素在分到的尺寸內測量，
所以範本應當可以拉伸（鋪滿卡片的圖片或色塊），而不是寫死高度。

- **結果確定。** 尺寸只取決於子元素的索引和 `Seed`（預設 0）。配置時不使用隨機數：同樣的資料項、種子和通道數，
  排出來永遠一樣，改變視窗大小也不會打亂排列。換一種排列就換一個 `Seed`。
- **依索引計算。** 在末尾加入資料項不會改變前面各項的尺寸；在中間插入或刪除一項，會改變它後面各項的尺寸。
  隱藏子元素（`IsVisible="False"`）不影響其他子元素的尺寸。
- **上下限。** 同時設定 `MinItemSize` 和 `MaxItemSize` 得到固定的區間。沒有設定的一端跟隨通道尺寸：
  最小值為半條通道，最大值為一條半通道。子元素自己的 `MinHeight`（或 `MinWidth`）仍然優先。

<Demo name="VariedSizes" />

### 填滿末端元素

`FillLastItem` 把每條通道的最後一個子元素拉伸到面板末端，讓所有通道一起結束。末端是最長的那一欄；如果父項給面板的空間更大
（固定的 `Height`、可拉伸的 `Grid` 列），則是面板的排列尺寸。在 `ScrollViewer` 裡末端一律是最長的那一欄，所以不會無限拉伸。
只改變每條通道最後一個可見的子元素，摺疊的子元素永遠不會被拉伸。

<Demo name="FillLastItem" />

### 在 ItemsControl 中使用

作為 `ItemsPanel` 時，面板排列的是項目容器，因此 `ObservableCollection` 中新增或移除的項目會就地出現或消失。這個範例還用了
`Columns="0"`，欄數隨寬度變化。

<Demo name="ItemsControl" />

## API 參考

<!-- api: WaterfallPanel -->

## 協助工具

- 面板本身不增加語意；子元素的順序就是閱讀和 Tab 順序，在瀑布流中它是依放置順序沿欄往下，而不是由左到右。請讓子元素的順序有意義。
- 可互動的卡片請提供各自的協助工具名稱（`AutomationProperties.Name`）。

## 動態效果

- 面板不做動畫：寬度、通道數或子元素改變時，子元素在一次配置中直接移到新位置。

## 響應式

- `Columns` 固定時，各欄平分面板得到的寬度，隨之變寬變窄。
- `Columns="0"` 時欄數隨寬度變化：放下盡可能多、至少 `MinLaneSize` 寬的欄，至少一欄。
- 在橫向 `ScrollViewer` 中（寬度無界）使用 `Orientation="Vertical"` 時，每欄與最寬的子元素一樣寬；建議改用縱向 `ScrollViewer`。

## 相關元件

- `TilePanel`：磁磚可跨列跨欄的格線。
- `CyclicPanel`：首尾無限循環的清單。
