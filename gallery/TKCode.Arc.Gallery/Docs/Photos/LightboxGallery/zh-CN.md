---
title: Lightbox gallery
summary: 一个可以全屏打开的照片网格：方块放大成灯箱，用箭头或滑动切换照片，用滚轮或双指放大。
source-hash: 49a636df5ef7
---

<Demo name="Preview" />

## 何时使用

`LightboxGallery` 把一组照片排成大小相同的方块，任意一张都能在整个窗口上打开。被选中的方块会放大成灯箱：照片从方块里的裁切画面展开成完整画面；关闭时又缩回它的方块，即使中途已经切换到别的照片，也会回到那张照片的方块里。在灯箱中，照片按自己的比例占满窗口；箭头按钮、方向键和左右滑动可以切换照片，滚轮、触控板或触屏的双指缩放、双击和放大按钮都会围绕指针放大照片，放大后可以拖动查看。Escape、关闭按钮、点击照片旁边，或把照片向上或向下拉开，都会关闭灯箱。

- **相册和旅行照片**：很多照片，大家想一张一张地放大看。
- **作品集和项目页面**：需要足够空间才能看清的作品。
- **附件**：工单、信息页或聊天里的图片。

## 何时不用

- **一件商品的几个角度**：商品页需要图片挨着价格，请用 `ProductGallery`。
- **形状各异、在网格中也想保持原样的照片**：请用 `PhotoGrid`，它按各自的比例排列照片。
- **文件和扫描件**：页面需要翻页和按宽度适应，请用 `PageViewer`。

## 用法

```xml
<arc:LightboxGallery Label="Trip photos" MinTileWidth="140">
    <arc:LightboxGalleryImage Source="https://example.com/trip/ridge.jpg" Caption="First light on the ridge" />
    <arc:LightboxGalleryImage Source="https://example.com/trip/harbour.jpg" Caption="Boats coming home" />
    <arc:LightboxGalleryImage Source="https://example.com/trip/valley.jpg" Caption="Pine valley after the rain" />
</arc:LightboxGallery>
```

`LightboxGallery` 位于 Special 包中：在 `UseArcControls()` 旁边用 `UseArcSpecial()` 注册。可以直接写 `LightboxGalleryImage`，也可以把 `ItemsSource` 绑定到由它们、`ImageSource` 或地址组成的列表。`Caption` 显示在灯箱中照片的下方。`Thumbnail` 可以指向方块用的小图，在完整照片加载完成前，灯箱也先显示它；`AspectRatio` 让灯箱在加载前就有正确的形状。

### 方块

网格按 `MinTileWidth` 放下尽可能多的列，或者正好 `Columns` 列。`TileAspectRatio` 决定每个方块的形状（默认为正方形），`Spacing` 设置间距。方块会裁切照片，灯箱则总是显示完整照片。`MaxZoom` 限制照片最多放大多少倍。

<Demo name="WideTiles" />

### 用代码打开

`Open(index)` 在指定照片上打开灯箱，`Close()` 关闭它。`OpenIndex` 是正在显示的照片，灯箱关闭时为 -1；它支持双向绑定，所以设置它也能打开、切换或关闭灯箱。`IsOpen` 和 `IsZoomed` 报告当前状态。

<Demo name="OpenFromCode" />

### 最佳实践

**建议**

- 为每张照片写说明文字；不需要说明时写 `Description`：读屏软件会读出它。
- 知道比例时请给出 `AspectRatio`，灯箱一打开就是正确的形状。
- 照片很多时传入较小的缩略图，网格会加载得更快。

**避免**

- 在照片上放按钮或链接：整个方块就是一个打开灯箱的按钮。
- 太小的方块：小于约 96 px 时很难认出照片。

## API 参考

<!-- api: LightboxGallery -->

## 无障碍

- 网格是一个列表，名称来自 `Label`；每个方块是一个按钮，名称如 “Photo 3 of 12: caption”（`ImageNameFormat` 可以改变说法）。网格是一个 Tab 停靠点，用方向键在方块之间移动。
- 灯箱是一个对话框：焦点移入其中，打开期间留在里面，关闭时回到当时显示的照片的方块。
- 每张照片出现时，一条礼貌模式的状态文字会读出它。
- 灯箱中按钮的名称来自 `CloseLabel`、`PreviousLabel`、`NextLabel`、`ZoomInLabel` 和 `ZoomOutLabel`。

### 键盘交互

| 按键 | 操作 |
|---|---|
| 方向键 | 在网格中，在方块之间移动。 |
| Enter 或 Space | 在当前方块上打开灯箱。 |
| Left / Right | 在灯箱中，显示上一张或下一张照片；放大时移动照片。 |
| Up / Down | 放大时移动照片。 |
| + / - | 围绕中心放大或缩小。 |
| 0 | 恢复显示完整照片。 |
| Home / End | 显示第一张或最后一张照片。 |
| Escape | 关闭灯箱。 |

## 动效

- 打开时，方块以 0.46 s 的临界阻尼弹簧飞入灯箱，背景同时变暗。每一帧都按拉伸后的形状重新裁切照片，让画面不变形：照片是从方块的裁切中展开，而不是被压扁，圆角也从方块的大小逐渐变成直角。
- 关闭时，照片飞回当时显示的照片的方块（0.4 s），背景同时变亮。照片在灯箱中时，它的方块会隐藏。
- 滑动跟随手指，松手时保留速度；箭头和按键以弹簧滑入下一张照片。
- 向上或向下拉动时，照片稍微变小，背景随之变淡。超过高度的 18%，或速度达到 700 px/s 时关闭；否则弹回原位。
- 用按钮、按键或双击放大时，在两个视图之间做动画，指针下方的点保持不动；滚轮和双指缩放则直接跟随手的动作。

| 变化 | 完整 | 减弱 | 关闭 |
|---|---|---|---|
| 打开和关闭 | 方块飞入灯箱再飞回 | 灯箱淡入淡出（160 ms） | 直接出现和消失 |
| 下一张照片 | 以弹簧滑入 | 在 160 ms 内淡入 | 直接出现 |
| 用按钮和按键放大 | 围绕固定点做动画 | 直接改变 | 直接改变 |

## 响应式行为

- 网格变窄时减少列数（`MinTileWidth`）；方块始终保持 `TileAspectRatio`。
- 灯箱覆盖整个窗口，或覆盖最近的 `ArcOverlayHost`。宽度小于 640 px 时，照片左右占满，侧边箭头让位给滑动。
- 横向开始的拖动用来切换照片，上下开始的拖动用来拉开关闭；放大后，任何拖动都用来移动照片。

## 相关组件

- `ProductGallery` 展示一件商品的几个角度，并支持悬停放大。
- `PhotoGrid` 按各自比例排列照片。
- `CardToArticle` 用同样的飞行动画把卡片展开成完整文章。

| 组件 | 适合的场景 |
|---|---|
| `LightboxGallery` | 很多照片，逐张放大查看 |
| `ProductGallery` | 一件商品的几个角度，挨着其他内容 |
| `PhotoGrid` | 照片本身就是整个页面 |
