---
title: Product detail
summary: 商品详情页：按颜色切换的图片和缩略图、价格、颜色样本、带库存的尺码、数量、加入购物车和收藏，以及通过切换显示的详情和评价。
source-hash: ff4b65face68
---

<Demo name="Preview" />

## 何时使用

`ProductDetail` 是一件商品的页面。图片随所选颜色变化，缩略图用来在不同视角之间切换。旁边是品牌、名称、评分、价格（以及降价前的价格）、颜色、带库存的尺码、数量、加入购物车和收藏。下方的切换显示商品详情，或显示评价以及按星级的分布。

- **带颜色和尺码的商品页。**
- **从列表打开的快速查看。**
- **后台的商品预览。**

## 何时不用

- **浏览很多商品**：使用 Product listing 区块。
- **需要很多选项来配置的商品**：用表单做分步配置。

## 用法

该区块派生自 Avalonia 的 `TemplatedControl`。设置商品（`Brand`、`ProductName`、`Price`、`CompareAtPrice`、`Rating`、`ReviewCount`、`Description`、`Details`），并填充 `Colors`（每个都是带图片的 `ProductColorOption`）、`Sizes`（带库存的 `ProductSizeOption`）和 `Reviews`。`ProductDetailSamples.Apply` 会用预览中虚构的托特包填充区块。

```csharp
detail.Colors.Add(new ProductColorOption { Name = "Sand", Swatch = Color.Parse("#C9B79C"), Images = sandPictures });
detail.Sizes.Add(new ProductSizeOption("M", stock: 2));
```

### 选择与加入

没有库存的尺码会加删除线且不能选择；只剩三件或更少时显示 "Only 2 left"。数量保持在 1 到所选尺码的库存之间（最多 10）。选择尺码之前，按钮显示 "Choose a size"；此时按下会显示 "Choose a size first." 并把焦点移到第一个尺码。否则 `AddedToCart` 会带上颜色、尺码和数量，按钮在 1.6 秒内显示 "Added to cart"。收藏会切换 `IsSaved`。

<Demo name="Narrow" />

## API 参考

<!-- api: ProductDetail -->

<!-- api: ProductColorOption -->

<!-- api: ProductSizeOption -->

<!-- api: ProductReview -->

## 无障碍

- 主图可以获得焦点；**Left** 和 **Right** 在图片之间移动，名称说明是第几张（"Picture 2 of 4"）。
- 缩略图、颜色样本和尺码都是按钮，以图片、颜色或尺码命名（"M"、"XS, sold out"）；方向键沿每一行移动，并跳过已售完的尺码。
- 评分名为 "Rated 4.7 out of 5"。
- 礼貌的实时区域会读出加入的内容和尺码提示。

## 动效

- 选中时，选项的边框和填充在 160 ms 内交叉淡化。
- 图片在原位切换；加入按钮的文字使用按钮自身的切换效果。
- **减弱动效**和**关闭**：所有变化立即生效。

## 响应式

- 低于 820 px 时，图片放在信息上方。
- 在窄区块中，选项会换到更多行。

## 相关组件

- Product listing 区块：商品目录页。
- Cart drawer 区块：加入购物车之后的流程。
