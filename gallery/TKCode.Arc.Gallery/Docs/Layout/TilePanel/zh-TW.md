---
title: 磁磚面板
summary: 開始畫面式的磁磚格線：磁磚可跨列跨欄，依序放到第一個放得下的位置，互不重疊，並隨面板縮放。
source-hash: 6ede02475c5e
---

<Demo name="Preview" />

## 何時使用

`TilePanel` 把區域分成相等的儲存格，每個子元素可以涵蓋其中幾格：1×1、1×2、2×2 等。子元素依序放到第一個放得下的空位，
大小混合的磁磚會排成整齊的格線，不必自己計算任何位置。

- **儀表板**：重要的指標用較大的磁磚。
- **啟動器、開始畫面**：應用程式與捷徑。
- **圖庫**：少數重點項目較大。

## 何時不用

- **要把每個子元素放到指定的列與欄**：請用 Avalonia 的 `Grid`。
- **子元素高度不固定**：請用 `WaterfallPanel`。
- **磁磚大小全都相同**：Avalonia 的 `UniformGrid` 或 `WrapPanel` 比較簡單。

## 用法

`TilePanel` 衍生自 Avalonia 的 `Panel`。用附加屬性 `TilePanel.RowSpan` 和 `TilePanel.ColumnSpan`（預設皆為 1）設定子元素涵蓋的儲存格數。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:TilePanel Columns="6" Spacing="6">
        <Border arc:TilePanel.RowSpan="2" arc:TilePanel.ColumnSpan="2" Background="SteelBlue" />
        <Border arc:TilePanel.ColumnSpan="2" Background="Orange" />
        <Border Background="SeaGreen" />
    </arc:TilePanel>
</UserControl>
```

放置採用第一個可用位置（first fit），結果完全確定：

1. 子元素依序放置。摺疊的子元素（`IsVisible="False"`）不佔儲存格。
2. 子元素的跨度會被限制在 `MaxRowSpan` 和 `MaxColumnSpan` 之內（0 為預設值，表示不限制），也不會超出格線。
3. 子元素放到第一個所有儲存格都空著的位置：由上往下逐列掃描，每列由左往右。因此後面的小磁磚可以填上前面寬磁磚留下的空洞。
4. 磁磚永不重疊；格線會逐列增長，直到每個磁磚都有位置，不會遺漏任何磁磚。

`GetPlacement(child)` 傳回子元素得到的列、欄與跨度，`RowCount` / `ColumnCount` 傳回格線大小。

### 格線大小

`Columns` 固定欄數，各欄平分寬度。`Rows` 讓各列平分高度（高度有界時）；放不進這麼多列的磁磚會繼續排在下方同樣大小的新列。
沒有被設定的一邊沿用另一邊的尺寸，因此除非兩邊都設定，儲存格都是正方形。

<Demo name="FixedGrid" />

`Columns="0"`（預設）時，面板依寬度放下盡可能多、至少 `MinCellSize`（96 px）寬的欄，寬度改變時磁磚重新排列。
寬度無界時（例如在橫向 `ScrollViewer` 中）請改設 `Rows`：列數固定，磁磚由上到下填滿一欄再到下一欄，格線向右增長。

<Demo name="AutoColumns" />

### 跨度上限

`MaxRowSpan` 和 `MaxColumnSpan` 限制任何磁磚能涵蓋的範圍，不論它要求多少。

<Demo name="SpanLimits" />

### 自動填滿

`IsAutoFill`（預設關閉）是放置之後的一個選用步驟，讓磁磚延伸到空的儲存格。規則固定，結果可以預測：

- 依子元素順序逐一處理磁磚。
- 磁磚先向右延伸，每次一欄，條件是它旁邊下一欄的每個儲存格都是空的，最多到 `MaxColumnSpan` 和最後一欄。
- 接著以同樣方式向下延伸，每次一列，最多到 `MaxRowSpan` 和格線高度：`Rows` 與磁磚已用列數中較大的那個。
- 在跨度上限內沒有磁磚能到達的空洞維持空白。磁磚從不移動、重疊或縮小。

<Demo name="AutoFill" />

### 在 ItemsControl 中使用

面板的子元素是項目容器，所以跨度要設在容器上，例如用一個樣式從資料項目讀取。取代某個項目會以新的跨度重建它的容器。

<Demo name="ItemsControl" />

## API 參考

<!-- api: TilePanel -->

## 協助工具

- 面板本身不增加語意；子元素的順序就是閱讀和 Tab 順序，與放置順序一致（由左到右、由上到下，小磁磚可能填到較前面的空洞）。請讓順序有意義。
- 可互動的磁磚請提供各自的協助工具名稱（`AutomationProperties.Name`）。

## 動態效果

- 面板不做動畫：寬度、格線或子元素改變時，磁磚在一次配置中直接移到新的儲存格。

## 響應式

- `Columns` 固定時，放置結果不變，只有儲存格尺寸隨寬度變化。
- `Columns="0"` 時欄數隨寬度變化（放下盡可能多、至少 `MinCellSize` 寬的欄），磁磚重新排列。
- 比格線還寬的跨度會被裁成欄數，所以窄寬度下磁磚也不會超出面板。

## 相關元件

- `WaterfallPanel`：子元素高度不限的等寬欄。
- `CyclicPanel`：首尾無限循環的清單。
