---
title: Photo grid
summary: 照片排成同样高度的行，或排成多列。选中的照片在原地展开变大，网格变化时每张照片都会平滑移到新位置。
source-hash: 193b003c39d5
---

<Demo name="Preview" />

## 何时使用

`PhotoGrid` 把照片一张挨一张地排列，不裁切照片。默认的等高行模式中，每一行高度相同：一行不断加入照片，直到按目标高度排列会超出网格宽度，然后把这一行的照片一起缩放，正好占满宽度。多列模式中，每张照片放进当前最短的一列。选中一张照片（点击、Enter 或 Space）会让它在单独的一行里展开变大，周围的照片随之让开。只要布局发生变化，比如窗口大小改变、照片增加或删除、选中某张照片，每张照片都会从原来的位置平滑移到新位置，而不是直接跳过去，眼睛能跟得上。

- **相册和手机照片**：很多形状不同的照片，完整显示。
- **作品集和灵感板**：每张图片的形状都很重要的作品。
- **图片搜索结果**：在浏览过程中不断增加和变化的列表。

## 何时不用

- **全屏打开照片**：能打开灯箱的网格更合适，请用 `LightboxGallery`。
- **带文字的同样大小的方块**：有标题和操作的卡片应该放在卡片网格里。
- **成千上万张照片**：每张照片都是一个控件；请分页显示，或使用虚拟化列表。

## 用法

```xml
<arc:PhotoGrid Label="Trip photos" RowHeight="160">
    <arc:PhotoGridPhoto Source="https://example.com/trip/ridge.jpg" AspectRatio="1.5" Description="First light on the ridge" />
    <arc:PhotoGridPhoto Source="https://example.com/trip/tower.jpg" AspectRatio="0.75" Description="The old signal tower" />
    <arc:PhotoGridPhoto Source="https://example.com/trip/dunes.jpg" AspectRatio="1.78" Description="Dunes at noon" />
</arc:PhotoGrid>
```

`PhotoGrid` 位于 Special 包中：在 `UseArcControls()` 旁边用 `UseArcSpecial()` 注册。可以直接写 `PhotoGridPhoto`，也可以把 `ItemsSource` 绑定到由它们、`ImageSource` 或地址组成的列表。知道比例时请给出 `AspectRatio`（宽除以高）：这样在图片加载之前布局就已确定。不给出时，照片先按 3:2 排列，加载完成后再变成自己的形状。`RowHeight` 是每行的目标高度；为了占满宽度，实际会稍高一些。`Spacing` 设置横向和纵向的间距。

### 多列

`Layout="Masonry"` 把照片排成多列，每列宽度由 `ColumnWidth` 决定。选中的照片会跨过所有列。

<Demo name="Masonry" />

### 平滑的变化

把 `ItemsSource` 绑定到 `ObservableCollection`，网格就会跟着它变化：新照片在自己的位置上变大出现，删除的照片在原处淡出，其余照片平滑移到新位置。切换 `Layout`、修改 `RowHeight` 或改变大小时，也会同样平滑地移动。

<Demo name="Reflow" />

### 选中

`SelectedIndex` 是展开变大的照片（没有时为 -1），支持双向绑定。再次点击它或按 Escape 会收起。`SelectedMaxHeight` 限制它的高度。设置 `IsSelectionEnabled="False"` 可以只做普通布局。

### 最佳实践

**建议**

- 尽量为每张照片给出 `AspectRatio`，图片加载时就不会有东西移动。
- 选一个在常用宽度下每行能放几张照片的 `RowHeight`。
- 为每张照片写 `Description`：读屏软件会读出它。

**避免**

- 把很高或很宽的全景照片和小照片混在一起：会让某些行非常矮或非常高。
- 在照片上放文字；请放在网格旁边。

## API 参考

<!-- api: PhotoGrid -->

## 无障碍

- 网格是一个列表，名称来自 `Label`；每张照片是一个列表项，名称如 “Photo 4 of 18: description”（`ImageNameFormat` 可以改变说法），选中的照片会说明自己已选中。
- 网格是一个 Tab 停靠点。Left 和 Right 按顺序移动，Up 和 Down 移到最近一行中上方或下方的照片，Home 和 End 移到第一张和最后一张。
- 指针获得焦点时不显示焦点环；键盘获得焦点时，在照片周围显示统一的焦点环。

### 键盘交互

| 按键 | 操作 |
|---|---|
| Left / Right | 移到上一张或下一张照片。 |
| Up / Down | 移到上方或下方的照片。 |
| Home / End | 移到第一张或最后一张照片。 |
| Enter 或 Space | 选中照片；已选中时收起。 |
| Escape | 收起选中的照片。 |

## 动效

- 每次布局变化都是一次重新排列：每张照片从当前所在的位置移到新位置，使用 0.5 s 的临界阻尼弹簧，位置和大小一起变化。重新排列途中再次变化时，会从照片当前的位置开始，不会跳动。
- 新照片从 92% 的大小变大并淡入；删除的照片在原处淡出。
- 重新排列时，每一帧为每张照片写一个变换；照片移动时不会重新布局。

| 变化 | 完整 | 减弱 | 关闭 |
|---|---|---|---|
| 改变大小、选中、增加、删除 | 照片平滑移到新位置 | 移动的照片在新位置淡入（240 ms） | 照片直接移动 |
| 新照片 | 变大并淡入 | 淡入 | 直接出现 |
| 删除的照片 | 淡出 | 淡出 | 直接消失 |

## 响应式行为

- 网格占满给定的宽度，高度由照片决定；照片很多时请放进 `ScrollViewer`。
- 等高行在任何宽度下都保持目标高度，窗口变窄时每行的照片就少一些。多列模式在变窄时减少列数。
- 选中的照片在网格允许的范围内尽量宽，最高为 `SelectedMaxHeight`，并会滚动到可见位置。

## 相关组件

- `LightboxGallery` 从同样大小的方块网格中全屏打开照片。
- `ProductGallery` 展示一件商品的几个角度。
- `CardToArticle` 排列可以展开成文章的故事卡片。

| 组件 | 适合的场景 |
|---|---|
| `PhotoGrid` | 形状各异的照片，完整显示 |
| `LightboxGallery` | 逐张放大查看照片 |
| `ProductGallery` | 一件商品的几个角度 |
