---
title: Product gallery
summary: 商品页的图片区：一张大图加一排缩略图，鼠标移到图上时跟着指针放大，在触屏上则可左右滑动。
source-hash: e5c7b753f07b
---

<Demo name="Preview" />

## 何时使用

`ProductGallery` 是商品页顶部的图片区。一张图片占满方形的展示区，其余图片以缩略图排在下方或左侧。购物的人想在下单前看清细节，所以鼠标或笔移到图片上时，会放大指针下方的位置，并随指针移动：布料的纹理、标签上的小字不用打开任何东西就能看清。在手机上则改为左右滑动图片，手指松开时保留滑动的速度。箭头按钮、方向键和缩略图都可以切换到另一张图片。

- **商品详情页**：商品的各个角度、材质特写、使用场景。
- **单个物品的信息页**：出租的房间、二手自行车、手工作品，每张图片都重要。
- **不同款式**：选择颜色或款式时换一组图片，展示区保持同一个角度。

## 何时不用

- **浏览大量照片**：相册更适合能全屏打开的网格，请用 `LightboxGallery`。
- **文章配图**：故事里的照片按各自比例排列更好读，请用 `PhotoGrid`。
- **只有一张图片**：圆角框里的 `ArcImage` 就够了；只有一张时，图片区本来也会隐藏缩略图和箭头。

## 用法

```xml
<arc:ProductGallery Label="Speckled stoneware mug" MaxWidth="440">
    <arc:ProductGalleryImage Source="https://example.com/mug/front.jpg" Description="Front, handle on the right" />
    <arc:ProductGalleryImage Source="https://example.com/mug/side.jpg" Description="Handle side" />
    <arc:ProductGalleryImage Source="https://example.com/mug/glaze.jpg" Description="Close-up of the glaze" />
</arc:ProductGallery>
```

`ProductGallery` 位于 Special 包中：在 `UseArcControls()` 旁边用 `UseArcSpecial()` 注册。可以直接写 `ProductGalleryImage`，也可以把 `ItemsSource` 绑定到由它们、`ImageSource` 或地址组成的列表。请为每张图片写 `Description`：读屏软件会读出它。`Thumbnail` 可以指向缩略图用的小图。`SelectedIndex` 是展示区当前的图片，支持双向绑定。

### 缩略图放在左侧，放大更多

`ThumbnailPlacement="Left"` 把缩略图排在左侧，适合较宽的页面。`AspectRatio` 决定展示区的形状（宽除以高，默认为正方形），`Stretch` 决定图片如何填充：`Uniform` 在浅色底上显示完整商品，`UniformToFill` 裁切到展示区大小。`ZoomScale` 决定悬停放大的倍数；设为 1 或 `IsHoverZoomEnabled="False"` 会关闭放大。

<Demo name="ThumbnailsLeft" />

### 不同款式

选择颜色或款式时，给图片区一个新的 `ItemsSource`。再把 `SelectedIndex` 设回原来的角度，展示区就保持不变。`SelectedImage` 是展示区当前的图片。

<Demo name="Variants" />

### 最佳实践

**建议**

- 把最简单的角度放在第一张：没人操作时显示的就是它。
- 图片至少是展示区的两倍大，放大时才有细节可看。
- 各个款式的图片保持相同的角度和顺序。

**避免**

- 混用不同比例的图片：同一个展示区里会留下大小不一的空白。
- 只出现在图片里的文字，比如尺码表；请在页面上也写出来。

## API 参考

<!-- api: ProductGallery -->

## 无障碍

- 整个图片区是一个 Tab 停靠点，名称来自 `Label`。它的帮助文本和一条礼貌模式的状态文字会说明展示区当前的图片：“Image 2 of 5: Handle side”。`ImageNameFormat` 可以改变说法。
- 缩略图是按钮，名称相同。缩略图条是另一个 Tab 停靠点，落在选中的缩略图上，用方向键在其中移动。
- 上一张和下一张按钮不是 Tab 停靠点，因为方向键能做同样的事；读屏软件用 `PreviousLabel` 和 `NextLabel` 读出它们的名称。
- 指针获得焦点时不显示焦点环；键盘获得焦点时，在展示区或缩略图周围显示统一的焦点环。

### 键盘交互

| 按键 | 操作 |
|---|---|
| Left / Right | 切换到上一张或下一张（缩略图在左侧时为 Up / Down）。 |
| Home / End | 切换到第一张或最后一张。 |
| Tab | 在展示区和缩略图条之间移动。 |
| Enter 或 Space | 在缩略图上时，显示它的图片。 |

## 动效

- 新图片从它所在的一侧滑入，使用临界阻尼弹簧。一次跨过多张时，只在看得到的两张之间滑动，不会经过中间的图片。
- 滑动时图片 1:1 跟随手指，超过第一张和最后一张时有阻力。松手时，若滑过一半或速度超过 360 px/s，就切换到下一张，弹簧从手指的速度开始。
- 悬停放大在 240 ms 内朝指针下方的位置放大，并以短弹簧跟随指针；指针离开时在 160 ms 内恢复。放大时箭头和计数会淡出。
- 图片移动时不会重新布局：每一帧只写三个变换。

| 变化 | 完整 | 减弱 | 关闭 |
|---|---|---|---|
| 新图片 | 以弹簧滑入 | 在 160 ms 内淡入 | 直接出现 |
| 悬停放大 | 逐渐放大并跟随指针 | 直接放大并跟随 | 直接放大并跟随 |
| 箭头和计数 | 淡入淡出 | 直接切换 | 直接切换 |

## 响应式行为

- 展示区占满给定的宽度，高度按 `AspectRatio` 计算；高度固定时，宽度由高度决定。
- 缩略图放不下时，缩略图条可以滚动。左侧的缩略图适合宽页面；在手机上请改到下方。
- 触屏不会悬停放大：触摸拖动用来切换图片，上下拖动仍然滚动页面。

## 相关组件

- `LightboxGallery` 可以全屏打开大量照片。
- `PhotoGrid` 按各自比例排列照片。
- `ImageCompare` 在同一个展示区里对比两张图片。

| 组件 | 适合的场景 |
|---|---|
| `ProductGallery` | 一件商品、几个角度，下单前看清细节 |
| `LightboxGallery` | 大量照片，逐张全屏查看 |
| `PhotoGrid` | 按各自形状展示一组照片 |
