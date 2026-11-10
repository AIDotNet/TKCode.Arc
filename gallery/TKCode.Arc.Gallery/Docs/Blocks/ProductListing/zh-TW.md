---
title: Product listing
summary: 商品列表頁：分類標籤、價格區間、只看有貨開關和排序，下方是響應式的商品卡片網格，帶評分、降價價格、顏色和快速加入購物車。
source-hash: 8b065db1f18a
---

<Demo name="Preview" />

## 何時使用

`ProductListing` 是商店的商品目錄頁。篩選按分類、價格和庫存縮小範圍，排序決定順序，網格中每件商品一張卡片，包含圖片、標籤（新品、熱銷或降價幅度）、名稱、評分、價格和降價前的價格，以及可選的顏色。每張卡片都有快速加入按鈕，頁頭的購物車按鈕記錄已加入的數量。

- **商店的分類頁和搜索結果頁。**
- **小型目錄**：每頁最多幾百件商品。
- **促銷頁面**：降價和評分很重要的場合。

## 何時不用

- **單件商品**：使用 Product detail 區塊。
- **查看購物車**：使用 Cart drawer 區塊。
- **一次顯示上千件商品**：網格不做虛擬化，請分頁顯示結果。

## 用法

該區塊衍生自 Avalonia 的 `TemplatedControl`。用 `ListingProduct` 填充 `Products`，或綁定 `ProductsSource`。`ProductListingSamples.CreateProducts` 返回十二件虛構商品，圖片由 `ProductListingArtwork` 繪製。

```xml
<arc:ProductListing Title="Everyday goods" ProductsSource="{Binding Products}" Sort="TopRated" />
```

### 篩選與排序

分類標籤列出商品的所有分類；一個都不選時顯示全部。價格區間從最便宜的商品開始，到最貴的商品結束；滑塊停在端點時 `MinPrice` 和 `MaxPrice` 為 null。`InStockOnly` 隱藏已售完的商品。`Sort` 可以按給定順序、價格升序或降序、評分（評價多者優先）或最新排序。`VisibleProducts` 保存結果，`ClearFilters` 重置所有篩選；結果為空時也會提供同樣的按鈕。

### 加入購物車

快速加入會引發 `AddedToCart`，讓 `CartCount` 加一，並在 1.4 秒內顯示 "Added"。已售完的商品保留卡片，按鈕顯示 Sold out 並被禁用。點按卡片會引發 `ProductInvoked`，購物車按鈕會引發 `CartInvoked`。

### 窄區塊

網格按寬度放入盡可能多、每列至少 160 px 的列，最多 `MaxColumns`（4）列。低於 760 px 時，篩選收進 Filters 按鈕後面。

<Demo name="Narrow" />

## API 參考

<!-- api: ProductListing -->

<!-- api: ListingProduct -->

## 無障礙

- 每張卡片的主體是一個按鈕，名稱包含商品、價格和評分；快速加入按鈕名為 "Add Market tote to cart"。
- 方向鍵按閱讀順序在卡片之間移動（**Up** 和 **Down** 按行移動），**Home** 和 **End** 跳到第一張和最後一張卡片。
- 購物車按鈕的名稱包含數量（"Cart, 2 items"）。
- 禮貌的即時區域會讀出每次加入的商品，以及篩選變化後的新結果數。**Escape** 關閉收起的篩選。

## 動效

- 篩選變化後新加入網格的卡片在 240 ms 內淡入；留下的卡片保持原位。
- 指標懸停時圖片以 snappy 彈簧放大 1.5%。
- **減弱動效**和**關閉**：卡片立即出現，圖片不放大。

## 響應式

- 列數跟隨區塊寬度，而不是窗口寬度；低於 760 px 時篩選收起。
- 名稱以省略號結尾；價格和評分保持一行。

## 相關組件

- Product detail 區塊：單件商品。
- Cart drawer 區塊：帶結帳的購物車。
- `ChipGroup`、`Slider`、`Switch` 和 `Select`：篩選所用的控制項。
