---
title: Product listing
summary: 商品列表页：分类标签、价格区间、只看有货开关和排序，下方是响应式的商品卡片网格，带评分、降价价格、颜色和快速加入购物车。
source-hash: 8b065db1f18a
---

<Demo name="Preview" />

## 何时使用

`ProductListing` 是商店的商品目录页。筛选按分类、价格和库存缩小范围，排序决定顺序，网格中每件商品一张卡片，包含图片、标签（新品、热销或降价幅度）、名称、评分、价格和降价前的价格，以及可选的颜色。每张卡片都有快速加入按钮，页头的购物车按钮记录已加入的数量。

- **商店的分类页和搜索结果页。**
- **小型目录**：每页最多几百件商品。
- **促销页面**：降价和评分很重要的场合。

## 何时不用

- **单件商品**：使用 Product detail 区块。
- **查看购物车**：使用 Cart drawer 区块。
- **一次显示上千件商品**：网格不做虚拟化，请分页显示结果。

## 用法

该区块派生自 Avalonia 的 `TemplatedControl`。用 `ListingProduct` 填充 `Products`，或绑定 `ProductsSource`。`ProductListingSamples.CreateProducts` 返回十二件虚构商品，图片由 `ProductListingArtwork` 绘制。

```xml
<arc:ProductListing Title="Everyday goods" ProductsSource="{Binding Products}" Sort="TopRated" />
```

### 筛选与排序

分类标签列出商品的所有分类；一个都不选时显示全部。价格区间从最便宜的商品开始，到最贵的商品结束；滑块停在端点时 `MinPrice` 和 `MaxPrice` 为 null。`InStockOnly` 隐藏已售完的商品。`Sort` 可以按给定顺序、价格升序或降序、评分（评价多者优先）或最新排序。`VisibleProducts` 保存结果，`ClearFilters` 重置所有筛选；结果为空时也会提供同样的按钮。

### 加入购物车

快速加入会引发 `AddedToCart`，让 `CartCount` 加一，并在 1.4 秒内显示 "Added"。已售完的商品保留卡片，按钮显示 Sold out 并被禁用。点按卡片会引发 `ProductInvoked`，购物车按钮会引发 `CartInvoked`。

### 窄区块

网格按宽度放入尽可能多、每列至少 160 px 的列，最多 `MaxColumns`（4）列。低于 760 px 时，筛选收进 Filters 按钮后面。

<Demo name="Narrow" />

## API 参考

<!-- api: ProductListing -->

<!-- api: ListingProduct -->

## 无障碍

- 每张卡片的主体是一个按钮，名称包含商品、价格和评分；快速加入按钮名为 "Add Market tote to cart"。
- 方向键按阅读顺序在卡片之间移动（**Up** 和 **Down** 按行移动），**Home** 和 **End** 跳到第一张和最后一张卡片。
- 购物车按钮的名称包含数量（"Cart, 2 items"）。
- 礼貌的实时区域会读出每次加入的商品，以及筛选变化后的新结果数。**Escape** 关闭收起的筛选。

## 动效

- 筛选变化后新加入网格的卡片在 240 ms 内淡入；留下的卡片保持原位。
- 指针悬停时图片以 snappy 弹簧放大 1.5%。
- **减弱动效**和**关闭**：卡片立即出现，图片不放大。

## 响应式

- 列数跟随区块宽度，而不是窗口宽度；低于 760 px 时筛选收起。
- 名称以省略号结尾；价格和评分保持一行。

## 相关组件

- Product detail 区块：单件商品。
- Cart drawer 区块：带结账的购物车。
- `ChipGroup`、`Slider`、`Switch` 和 `Select`：筛选所用的控件。
