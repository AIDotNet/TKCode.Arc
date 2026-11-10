---
title: Product detail
summary: 商品詳情頁：按顏色切換的圖片和縮略圖、價格、顏色樣本、帶庫存的尺碼、數量、加入購物車和收藏，以及通過切換顯示的詳情和評價。
source-hash: ff4b65face68
---

<Demo name="Preview" />

## 何時使用

`ProductDetail` 是一件商品的頁面。圖片隨所選顏色變化，縮略圖用來在不同視角之間切換。旁邊是品牌、名稱、評分、價格（以及降價前的價格）、顏色、帶庫存的尺碼、數量、加入購物車和收藏。下方的切換顯示商品詳情，或顯示評價以及按星級的分布。

- **帶顏色和尺碼的商品頁。**
- **從列表打開的快速查看。**
- **後台的商品預覽。**

## 何時不用

- **瀏覽很多商品**：使用 Product listing 區塊。
- **需要很多選項來配置的商品**：用表單做分步配置。

## 用法

該區塊衍生自 Avalonia 的 `TemplatedControl`。設定商品（`Brand`、`ProductName`、`Price`、`CompareAtPrice`、`Rating`、`ReviewCount`、`Description`、`Details`），並填充 `Colors`（每個都是帶圖片的 `ProductColorOption`）、`Sizes`（帶庫存的 `ProductSizeOption`）和 `Reviews`。`ProductDetailSamples.Apply` 會用預覽中虛構的托特包填充區塊。

```csharp
detail.Colors.Add(new ProductColorOption { Name = "Sand", Swatch = Color.Parse("#C9B79C"), Images = sandPictures });
detail.Sizes.Add(new ProductSizeOption("M", stock: 2));
```

### 選擇與加入

沒有庫存的尺碼會加刪除線且不能選擇；只剩三件或更少時顯示 "Only 2 left"。數量保持在 1 到所選尺碼的庫存之間（最多 10）。選擇尺碼之前，按鈕顯示 "Choose a size"；此時按下會顯示 "Choose a size first." 並把焦點移到第一個尺碼。否則 `AddedToCart` 會帶上顏色、尺碼和數量，按鈕在 1.6 秒內顯示 "Added to cart"。收藏會切換 `IsSaved`。

<Demo name="Narrow" />

## API 參考

<!-- api: ProductDetail -->

<!-- api: ProductColorOption -->

<!-- api: ProductSizeOption -->

<!-- api: ProductReview -->

## 無障礙

- 主圖可以獲得焦點；**Left** 和 **Right** 在圖片之間移動，名稱說明是第幾張（"Picture 2 of 4"）。
- 縮略圖、顏色樣本和尺碼都是按鈕，以圖片、顏色或尺碼命名（"M"、"XS, sold out"）；方向鍵沿每一行移動，並跳過已售完的尺碼。
- 評分名為 "Rated 4.7 out of 5"。
- 禮貌的即時區域會讀出加入的內容和尺碼提示。

## 動效

- 選中時，選項的邊框和填充在 160 ms 內交叉淡化。
- 圖片在原位切換；加入按鈕的文字使用按鈕自身的切換效果。
- **減弱動效**和**關閉**：所有變化立即生效。

## 響應式

- 低於 820 px 時，圖片放在資訊上方。
- 在窄區塊中，選項會換到更多行。

## 相關組件

- Product listing 區塊：商品目錄頁。
- Cart drawer 區塊：加入購物車之後的流程。
