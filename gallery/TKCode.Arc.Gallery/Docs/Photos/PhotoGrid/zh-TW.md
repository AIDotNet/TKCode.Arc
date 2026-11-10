---
title: Photo grid
summary: 照片排成同樣高度的行，或排成多列。選取的照片在原地展開變大，網格變化時每張照片都會平滑移到新位置。
source-hash: 193b003c39d5
---

<Demo name="Preview" />

## 何時使用

`PhotoGrid` 把照片一張挨一張地排列，不裁切照片。預設的等高行模式中，每一行高度相同：一行不斷加入照片，直到按目標高度排列會超出網格寬度，然後把這一行的照片一起縮放，正好佔滿寬度。多列模式中，每張照片放進目前最短的一列。選取一張照片（點擊、Enter 或 Space）會讓它在單獨的一行裡展開變大，周圍的照片隨之讓開。只要版面配置發生變化，比如窗口大小改變、照片增加或刪除、選取某張照片，每張照片都會從原來的位置平滑移到新位置，而不是直接跳過去，眼睛能跟得上。

- **相簿和手機照片**：很多形狀不同的照片，完整顯示。
- **作品集和靈感板**：每張圖片的形狀都很重要的作品。
- **圖片搜尋結果**：在瀏覽過程中不斷增加和變化的清單。

## 何時不用

- **全螢幕打開照片**：能打開燈箱的網格更合適，請用 `LightboxGallery`。
- **帶文字的同樣大小的方塊**：有標題和操作的卡片應該放在卡片網格裡。
- **成千上萬張照片**：每張照片都是一個控制項；請分頁顯示，或使用虛擬化清單。

## 用法

```xml
<arc:PhotoGrid Label="Trip photos" RowHeight="160">
    <arc:PhotoGridPhoto Source="https://example.com/trip/ridge.jpg" AspectRatio="1.5" Description="First light on the ridge" />
    <arc:PhotoGridPhoto Source="https://example.com/trip/tower.jpg" AspectRatio="0.75" Description="The old signal tower" />
    <arc:PhotoGridPhoto Source="https://example.com/trip/dunes.jpg" AspectRatio="1.78" Description="Dunes at noon" />
</arc:PhotoGrid>
```

`PhotoGrid` 位於 Special 包中：在 `UseArcControls()` 旁邊用 `UseArcSpecial()` 註冊。可以直接寫 `PhotoGridPhoto`，也可以把 `ItemsSource` 繫結到由它們、`ImageSource` 或地址組成的清單。知道比例時請給出 `AspectRatio`（寬除以高）：這樣在圖片載入之前版面配置就已確定。不給出時，照片先按 3:2 排列，載入完成後再變成自己的形狀。`RowHeight` 是每行的目標高度；為了佔滿寬度，實際會稍高一些。`Spacing` 設定橫向和縱向的間距。

### 多列

`Layout="Masonry"` 把照片排成多列，每列寬度由 `ColumnWidth` 決定。選取的照片會跨過所有列。

<Demo name="Masonry" />

### 平滑的變化

把 `ItemsSource` 繫結到 `ObservableCollection`，網格就會跟著它變化：新照片在自己的位置上變大出現，刪除的照片在原處淡出，其餘照片平滑移到新位置。切換 `Layout`、修改 `RowHeight` 或改變大小時，也會同樣平滑地移動。

<Demo name="Reflow" />

### 選取

`SelectedIndex` 是展開變大的照片（沒有時為 -1），支援雙向繫結。再次點擊它或按 Escape 會收起。`SelectedMaxHeight` 限制它的高度。設定 `IsSelectionEnabled="False"` 可以只做普通版面配置。

### 最佳實務

**建議**

- 盡量為每張照片給出 `AspectRatio`，圖片載入時就不會有東西移動。
- 選一個在常用寬度下每行能放幾張照片的 `RowHeight`。
- 為每張照片寫 `Description`：讀屏軟體會讀出它。

**避免**

- 把很高或很寬的全景照片和小照片混在一起：會讓某些行非常矮或非常高。
- 在照片上放文字；請放在網格旁邊。

## API 參考

<!-- api: PhotoGrid -->

## 無障礙

- 網格是一個清單，名稱來自 `Label`；每張照片是一個清單項目，名稱如 “Photo 4 of 18: description”（`ImageNameFormat` 可以改變說法），選取的照片會說明自己已選取。
- 網格是一個 Tab 停駐點。Left 和 Right 按順序移動，Up 和 Down 移到最近一行中上方或下方的照片，Home 和 End 移到第一張和最後一張。
- 指標獲得焦點時不顯示焦點環；鍵盤獲得焦點時，在照片周圍顯示統一的焦點環。

### 鍵盤互動

| 按鍵 | 操作 |
|---|---|
| Left / Right | 移到上一張或下一張照片。 |
| Up / Down | 移到上方或下方的照片。 |
| Home / End | 移到第一張或最後一張照片。 |
| Enter 或 Space | 選取照片；已選取時收起。 |
| Escape | 收起選取的照片。 |

## 動效

- 每次版面配置變化都是一次重新排列：每張照片從目前所在的位置移到新位置，使用 0.5 s 的臨界阻尼彈簧，位置和大小一起變化。重新排列途中再次變化時，會從照片目前的位置開始，不會跳動。
- 新照片從 92% 的大小變大並淡入；刪除的照片在原處淡出。
- 重新排列時，每一個影格為每張照片寫一個變換；照片移動時不會重新配置版面。

| 變化 | 完整 | 減弱 | 關閉 |
|---|---|---|---|
| 改變大小、選取、增加、刪除 | 照片平滑移到新位置 | 移動的照片在新位置淡入（240 ms） | 照片直接移動 |
| 新照片 | 變大並淡入 | 淡入 | 直接出現 |
| 刪除的照片 | 淡出 | 淡出 | 直接消失 |

## 響應式行為

- 網格佔滿給定的寬度，高度由照片決定；照片很多時請放進 `ScrollViewer`。
- 等高行在任何寬度下都保持目標高度，窗口變窄時每行的照片就少一些。多列模式在變窄時減少列數。
- 選取的照片在網格允許的範圍內盡量寬，最高為 `SelectedMaxHeight`，並會捲動到可見位置。

## 相關元件

- `LightboxGallery` 從同樣大小的方塊網格中全螢幕打開照片。
- `ProductGallery` 展示一件商品的幾個角度。
- `CardToArticle` 排列可以展開成文章的故事卡片。

| 元件 | 何時選擇 |
|---|---|
| `PhotoGrid` | 形狀各異的照片，完整顯示 |
| `LightboxGallery` | 逐張放大查看照片 |
| `ProductGallery` | 一件商品的幾個角度 |
