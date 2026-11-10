---
title: Product gallery
summary: 商品頁的圖片區：一張大圖加一排縮圖，滑鼠移到圖上時跟著指標放大，在觸控螢幕上則可左右滑動。
source-hash: e5c7b753f07b
---

<Demo name="Preview" />

## 何時使用

`ProductGallery` 是商品頁頂部的圖片區。一張圖片佔滿方形的展示區，其餘圖片以縮圖排在下方或左側。購物的人想在下單前看清細節，所以滑鼠或筆移到圖片上時，會放大指標下方的位置，並隨指標移動：布料的紋理、標籤上的小字不用打開任何東西就能看清。在手機上則改為左右滑動圖片，手指放開時保留滑動的速度。箭頭按鈕、方向鍵和縮圖都可以切換到另一張圖片。

- **商品詳情頁**：商品的各個角度、材質特寫、使用場景。
- **單個物品的資訊頁**：出租的房間、二手腳踏車、手工作品，每張圖片都重要。
- **不同款式**：選擇顏色或款式時換一組圖片，展示區保持同一個角度。

## 何時不用

- **瀏覽大量照片**：相簿更適合能全螢幕打開的網格，請用 `LightboxGallery`。
- **文章配圖**：故事裡的照片按各自比例排列更好讀，請用 `PhotoGrid`。
- **只有一張圖片**：圓角框裡的 `ArcImage` 就夠了；只有一張時，圖片區本來也會隱藏縮圖和箭頭。

## 用法

```xml
<arc:ProductGallery Label="Speckled stoneware mug" MaxWidth="440">
    <arc:ProductGalleryImage Source="https://example.com/mug/front.jpg" Description="Front, handle on the right" />
    <arc:ProductGalleryImage Source="https://example.com/mug/side.jpg" Description="Handle side" />
    <arc:ProductGalleryImage Source="https://example.com/mug/glaze.jpg" Description="Close-up of the glaze" />
</arc:ProductGallery>
```

`ProductGallery` 位於 Special 包中：在 `UseArcControls()` 旁邊用 `UseArcSpecial()` 註冊。可以直接寫 `ProductGalleryImage`，也可以把 `ItemsSource` 繫結到由它們、`ImageSource` 或地址組成的清單。請為每張圖片寫 `Description`：讀屏軟體會讀出它。`Thumbnail` 可以指向縮圖用的小圖。`SelectedIndex` 是展示區目前的圖片，支援雙向繫結。

### 縮圖放在左側，放大更多

`ThumbnailPlacement="Left"` 把縮圖排在左側，適合較寬的頁面。`AspectRatio` 決定展示區的形狀（寬除以高，預設為正方形），`Stretch` 決定圖片如何填充：`Uniform` 在淺色底上顯示完整商品，`UniformToFill` 裁切到展示區大小。`ZoomScale` 決定懸停放大的倍數；設為 1 或 `IsHoverZoomEnabled="False"` 會關閉放大。

<Demo name="ThumbnailsLeft" />

### 不同款式

選擇顏色或款式時，給圖片區一個新的 `ItemsSource`。再把 `SelectedIndex` 設回原來的角度，展示區就保持不變。`SelectedImage` 是展示區目前的圖片。

<Demo name="Variants" />

### 最佳實務

**建議**

- 把最簡單的角度放在第一張：沒人操作時顯示的就是它。
- 圖片至少是展示區的兩倍大，放大時才有細節可看。
- 各個款式的圖片保持相同的角度和順序。

**避免**

- 混用不同比例的圖片：同一個展示區裡會留下大小不一的空白。
- 只出現在圖片裡的文字，比如尺碼表；請在頁面上也寫出來。

## API 參考

<!-- api: ProductGallery -->

## 無障礙

- 整個圖片區是一個 Tab 停駐點，名稱來自 `Label`。它的幫助文字和一條禮貌模式的狀態文字會說明展示區目前的圖片：“Image 2 of 5: Handle side”。`ImageNameFormat` 可以改變說法。
- 縮圖是按鈕，名稱相同。縮圖條是另一個 Tab 停駐點，落在選取的縮圖上，用方向鍵在其中移動。
- 上一張和下一張按鈕不是 Tab 停駐點，因為方向鍵能做同樣的事；讀屏軟體用 `PreviousLabel` 和 `NextLabel` 讀出它們的名稱。
- 指標獲得焦點時不顯示焦點環；鍵盤獲得焦點時，在展示區或縮圖周圍顯示統一的焦點環。

### 鍵盤互動

| 按鍵 | 操作 |
|---|---|
| Left / Right | 切換到上一張或下一張（縮圖在左側時為 Up / Down）。 |
| Home / End | 切換到第一張或最後一張。 |
| Tab | 在展示區和縮圖條之間移動。 |
| Enter 或 Space | 在縮圖上時，顯示它的圖片。 |

## 動效

- 新圖片從它所在的一側滑入，使用臨界阻尼彈簧。一次跨過多張時，只在看得到的兩張之間滑動，不會經過中間的圖片。
- 滑動時圖片 1:1 跟隨手指，超過第一張和最後一張時有阻力。放手時，若滑過一半或速度超過 360 px/s，就切換到下一張，彈簧從手指的速度開始。
- 懸停放大在 240 ms 內朝指標下方的位置放大，並以短彈簧跟隨指標；指標離開時在 160 ms 內恢復。放大時箭頭和計數會淡出。
- 圖片移動時不會重新配置版面：每一個影格只寫三個變換。

| 變化 | 完整 | 減弱 | 關閉 |
|---|---|---|---|
| 新圖片 | 以彈簧滑入 | 在 160 ms 內淡入 | 直接出現 |
| 懸停放大 | 逐漸放大並跟隨指標 | 直接放大並跟隨 | 直接放大並跟隨 |
| 箭頭和計數 | 淡入淡出 | 直接切換 | 直接切換 |

## 響應式行為

- 展示區佔滿給定的寬度，高度按 `AspectRatio` 計算；高度固定時，寬度由高度決定。
- 縮圖放不下時，縮圖條可以捲動。左側的縮圖適合寬頁面；在手機上請改到下方。
- 觸控螢幕不會懸停放大：觸摸拖曳用來切換圖片，上下拖曳仍然捲動頁面。

## 相關元件

- `LightboxGallery` 可以全螢幕打開大量照片。
- `PhotoGrid` 按各自比例排列照片。
- `ImageCompare` 在同一個展示區裡對比兩張圖片。

| 元件 | 何時選擇 |
|---|---|
| `ProductGallery` | 一件商品、幾個角度，下單前看清細節 |
| `LightboxGallery` | 大量照片，逐張全螢幕查看 |
| `PhotoGrid` | 按各自形狀展示一組照片 |
