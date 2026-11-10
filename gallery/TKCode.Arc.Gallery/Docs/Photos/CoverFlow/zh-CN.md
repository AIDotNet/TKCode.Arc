---
title: Cover flow
summary: 一排带透视效果的专辑或书籍封面：中间的封面正对着你，其余封面转向它，快速滑动后会继续滑行并停在一张封面上。
source-hash: 370d49126271
---

<Demo name="Preview" />

## 何时使用

`CoverFlow` 用大家熟悉的封面来展示一组内容。中间的封面正对前方，下面显示标题和副标题；两侧的封面转向它，向远处层层排开，站在会映出倒影的光亮地面上。拖动这一排时，它跟着手移动；松手后按快速滑动的惯性继续滑行，然后让一张封面停在中间，所以一次就能划过很长的一排。点击两侧的封面会把它移到中间，点击中间的封面会打开它。

- **音乐和有声书**：按封面挑选专辑、歌单和系列。
- **书籍和杂志**：阅读清单或一排期刊。
- **推荐内容**：首页上的一小组游戏、电影或模板。

## 何时不用

- **按名称查找**：可以搜索的列表比翻封面更快。
- **大量的小内容**：同时只能看到几张封面，请用网格。
- **不是封面的图片**：形状各异的照片在透视中显得拥挤，请用 `PhotoGrid` 或 `LightboxGallery`。

## 用法

```xml
<arc:CoverFlow Label="Albums" SelectedIndex="1">
    <arc:CoverFlowItem Source="https://example.com/covers/lanterns.jpg" Title="Paper Lanterns" Subtitle="Mira Vale" />
    <arc:CoverFlowItem Source="https://example.com/covers/copper.jpg" Title="Copper Hours" Subtitle="The Lindens" />
    <arc:CoverFlowItem Source="https://example.com/covers/north.jpg" Title="North of Nowhere" Subtitle="Oskar Fen" />
</arc:CoverFlow>
```

`CoverFlow` 位于 Special 包中：在 `UseArcControls()` 旁边用 `UseArcSpecial()` 注册。可以直接写 `CoverFlowItem`，也可以把 `ItemsSource` 绑定到由它们、`ImageSource` 或地址组成的列表。`Title` 和 `Subtitle` 显示在中间封面的下方，`Tag` 可以带上你自己的任意对象。`SelectedIndex` 是中间的封面，支持双向绑定；`SelectedItem` 就是这张封面。

### 其他形状的封面

`CoverWidth` 设置封面的宽度，`CoverAspectRatio` 设置它的形状（默认为正方形，书籍约为 0.67）。`TiltAngle` 设置两侧封面转动的角度，`ShowsReflection="False"` 会去掉地面倒影。

<Demo name="Books" />

### 打开封面

点击或轻触中间的封面，或按 Enter 或 Space 时，会触发 `ItemInvoked`。它的参数带有 `Index` 和 `Item`。

<Demo name="Invoked" />

### 最佳实践

**建议**

- 为每张封面写 `Title`：它显示在中间封面下方，读屏软件也会读出它。
- 内容没有明显的第一项时，从中间的一张开始，两侧都能看到封面。
- 一排最好只有几十张封面；更多时请提供搜索或网格。

**避免**

- 以小字为主的封面：两侧的封面是转过去并变暗的。
- 放在横向拖动还要滚动其他内容的地方。

## API 参考

<!-- api: CoverFlow -->

## 无障碍

- 整排封面是一个 Tab 停靠点，是一个名称来自 `Label` 的列表。它的帮助文本和一条礼貌模式的状态文字会说明中间的封面：“Night Ferry, Ilse Marrow, 10 of 12”。`ItemNameFormat` 可以改变位置部分的说法。
- 方向键移动一张封面，Page Up 和 Page Down 移动五张，Home 和 End 移到两端；Enter 或 Space 打开中间的封面。
- 指针获得焦点时不显示焦点环；键盘获得焦点时，在整排周围显示统一的焦点环。

### 键盘交互

| 按键 | 操作 |
|---|---|
| Left / Right | 把上一张或下一张封面移到中间。 |
| Page Up / Page Down | 移动五张封面。 |
| Home / End | 移到第一张或最后一张封面。 |
| Enter 或 Space | 打开中间的封面（`ItemInvoked`）。 |

## 动效

- 所有封面都由同一个位置决定，也就是中间的序号。在中间和第一张相邻的封面之间，封面的转动、移动和缩小都是连续的，所以慢慢拖动时，封面会一张一张地转过来。
- 拖动跟随手的动作，默认每 73 px 移动一张封面，超过两端时有阻力。松手时，按每毫秒 0.995 的衰减估算快速滑动会停在哪张封面，再用 0.5 s 的临界阻尼弹簧从松手时的速度开始停到那里。每秒超过三张封面的快速滑动至少会移动一张。
- 鼠标滚轮每格移动一张封面；触控板横向滚动这一排，停下 140 ms 后对齐到最近的封面。
- 超出边缘的封面会淡出。每一帧为每张可见的封面写一个透视变换；不会重新布局。

| 变化 | 完整 | 减弱 | 关闭 |
|---|---|---|---|
| 换一张封面 | 这一排以弹簧滑动 | 这一排直接移动 | 这一排直接移动 |
| 拖动和快速滑动 | 跟随手的动作，再滑行并停下 | 跟随手的动作，再直接停下 | 跟随手的动作，再直接停下 |
| 标题文字 | 从对应的一侧滑入 | 交叉淡入淡出 | 直接切换 |

## 响应式行为

- 这一排占满给定的宽度；变窄时两侧显示的封面就少一些。高度由 `CoverWidth`、`CoverAspectRatio` 和倒影决定。
- 在手机上请用较小的 `CoverWidth`（约 120），让两侧各露出一张封面。
- 横向开始的触摸拖动会移动这一排；上下拖动仍然滚动页面，触控板的上下滚动也一样。

## 相关组件

- `Carousel` 一次翻一页内容。
- `LightboxGallery` 全屏打开照片。
- `HoloCard` 用透视效果展示一张卡片。

| 组件 | 适合的场景 |
|---|---|
| `CoverFlow` | 翻看一排封面 |
| `Carousel` | 带内容的页面，一次一页 |
| `LightboxGallery` | 需要仔细查看的照片 |
