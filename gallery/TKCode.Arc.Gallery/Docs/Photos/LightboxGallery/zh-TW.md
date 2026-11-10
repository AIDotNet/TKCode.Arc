---
title: Lightbox gallery
summary: 一個可以全螢幕打開的照片網格：方塊放大成燈箱，用箭頭或滑動切換照片，用滾輪或雙指放大。
source-hash: 49a636df5ef7
---

<Demo name="Preview" />

## 何時使用

`LightboxGallery` 把一組照片排成大小相同的方塊，任意一張都能在整個窗口上打開。被選取的方塊會放大成燈箱：照片從方塊裡的裁切畫面展開成完整畫面；關閉時又縮回它的方塊，即使中途已經切換到別的照片，也會回到那張照片的方塊裡。在燈箱中，照片按自己的比例佔滿窗口；箭頭按鈕、方向鍵和左右滑動可以切換照片，滾輪、觸控板或觸控螢幕的雙指縮放、雙擊和放大按鈕都會圍繞指標放大照片，放大後可以拖曳查看。Escape、關閉按鈕、點擊照片旁邊，或把照片向上或向下拉開，都會關閉燈箱。

- **相簿和旅行照片**：很多照片，大家想一張一張地放大看。
- **作品集和專案頁面**：需要足夠空間才能看清的作品。
- **附件**：工單、資訊頁或聊天裡的圖片。

## 何時不用

- **一件商品的幾個角度**：商品頁需要圖片挨著價格，請用 `ProductGallery`。
- **形狀各異、在網格中也想保持原樣的照片**：請用 `PhotoGrid`，它按各自的比例排列照片。
- **文件和掃描件**：頁面需要翻頁和按寬度適應，請用 `PageViewer`。

## 用法

```xml
<arc:LightboxGallery Label="Trip photos" MinTileWidth="140">
    <arc:LightboxGalleryImage Source="https://example.com/trip/ridge.jpg" Caption="First light on the ridge" />
    <arc:LightboxGalleryImage Source="https://example.com/trip/harbour.jpg" Caption="Boats coming home" />
    <arc:LightboxGalleryImage Source="https://example.com/trip/valley.jpg" Caption="Pine valley after the rain" />
</arc:LightboxGallery>
```

`LightboxGallery` 位於 Special 包中：在 `UseArcControls()` 旁邊用 `UseArcSpecial()` 註冊。可以直接寫 `LightboxGalleryImage`，也可以把 `ItemsSource` 繫結到由它們、`ImageSource` 或地址組成的清單。`Caption` 顯示在燈箱中照片的下方。`Thumbnail` 可以指向方塊用的小圖，在完整照片載入完成前，燈箱也先顯示它；`AspectRatio` 讓燈箱在載入前就有正確的形狀。

### 方塊

網格按 `MinTileWidth` 放下盡可能多的列，或者正好 `Columns` 列。`TileAspectRatio` 決定每個方塊的形狀（預設為正方形），`Spacing` 設定間距。方塊會裁切照片，燈箱則總是顯示完整照片。`MaxZoom` 限制照片最多放大多少倍。

<Demo name="WideTiles" />

### 用程式碼打開

`Open(index)` 在指定照片上打開燈箱，`Close()` 關閉它。`OpenIndex` 是正在顯示的照片，燈箱關閉時為 -1；它支援雙向繫結，所以設定它也能打開、切換或關閉燈箱。`IsOpen` 和 `IsZoomed` 報告目前狀態。

<Demo name="OpenFromCode" />

### 最佳實務

**建議**

- 為每張照片寫說明文字；不需要說明時寫 `Description`：讀屏軟體會讀出它。
- 知道比例時請給出 `AspectRatio`，燈箱一打開就是正確的形狀。
- 照片很多時傳入較小的縮圖，網格會載入得更快。

**避免**

- 在照片上放按鈕或連結：整個方塊就是一個打開燈箱的按鈕。
- 太小的方塊：小於約 96 px 時很難認出照片。

## API 參考

<!-- api: LightboxGallery -->

## 無障礙

- 網格是一個清單，名稱來自 `Label`；每個方塊是一個按鈕，名稱如 “Photo 3 of 12: caption”（`ImageNameFormat` 可以改變說法）。網格是一個 Tab 停駐點，用方向鍵在方塊之間移動。
- 燈箱是一個對話框：焦點移入其中，打開期間留在裡面，關閉時回到當時顯示的照片的方塊。
- 每張照片出現時，一條禮貌模式的狀態文字會讀出它。
- 燈箱中按鈕的名稱來自 `CloseLabel`、`PreviousLabel`、`NextLabel`、`ZoomInLabel` 和 `ZoomOutLabel`。

### 鍵盤互動

| 按鍵 | 操作 |
|---|---|
| 方向鍵 | 在網格中，在方塊之間移動。 |
| Enter 或 Space | 在目前方塊上打開燈箱。 |
| Left / Right | 在燈箱中，顯示上一張或下一張照片；放大時移動照片。 |
| Up / Down | 放大時移動照片。 |
| + / - | 圍繞中心放大或縮小。 |
| 0 | 恢復顯示完整照片。 |
| Home / End | 顯示第一張或最後一張照片。 |
| Escape | 關閉燈箱。 |

## 動效

- 打開時，方塊以 0.46 s 的臨界阻尼彈簧飛入燈箱，背景同時變暗。每一個影格都按拉伸後的形狀重新裁切照片，讓畫面不變形：照片是從方塊的裁切中展開，而不是被壓扁，圓角也從方塊的大小逐漸變成直角。
- 關閉時，照片飛回當時顯示的照片的方塊（0.4 s），背景同時變亮。照片在燈箱中時，它的方塊會隱藏。
- 滑動跟隨手指，放手時保留速度；箭頭和按鍵以彈簧滑入下一張照片。
- 向上或向下拉動時，照片稍微變小，背景隨之變淡。超過高度的 18%，或速度達到 700 px/s 時關閉；否則彈回原位。
- 用按鈕、按鍵或雙擊放大時，在兩個檢視之間做動畫，指標下方的點保持不動；滾輪和雙指縮放則直接跟隨手的動作。

| 變化 | 完整 | 減弱 | 關閉 |
|---|---|---|---|
| 打開和關閉 | 方塊飛入燈箱再飛回 | 燈箱淡入淡出（160 ms） | 直接出現和消失 |
| 下一張照片 | 以彈簧滑入 | 在 160 ms 內淡入 | 直接出現 |
| 用按鈕和按鍵放大 | 圍繞固定點做動畫 | 直接改變 | 直接改變 |

## 響應式行為

- 網格變窄時減少列數（`MinTileWidth`）；方塊始終保持 `TileAspectRatio`。
- 燈箱覆蓋整個窗口，或覆蓋最近的 `ArcOverlayHost`。寬度小於 640 px 時，照片左右佔滿，側邊箭頭讓位給滑動。
- 橫向開始的拖曳用來切換照片，上下開始的拖曳用來拉開關閉；放大後，任何拖曳都用來移動照片。

## 相關元件

- `ProductGallery` 展示一件商品的幾個角度，並支援懸停放大。
- `PhotoGrid` 按各自比例排列照片。
- `CardToArticle` 用同樣的飛行動畫把卡片展開成完整文章。

| 元件 | 何時選擇 |
|---|---|
| `LightboxGallery` | 很多照片，逐張放大查看 |
| `ProductGallery` | 一件商品的幾個角度，挨著其他內容 |
| `PhotoGrid` | 照片本身就是整個頁面 |
