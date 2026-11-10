---
title: Spotlight grid
summary: 由一束柔光照亮的功能卡片：光随指针或键盘焦点在整个网格上移动，经过之处卡面变暖、边框发亮。
source-hash: 03d439378de3
---

<Demo name="Preview" />

## 何时使用

`SpotlightGrid` 用安静的卡片展示一组功能（图标块、标题、一句说明和可选的标记），并加上一个效果：一束强调色的柔光随指针在整个网格上移动。光下的卡面略微变暖，卡片边框发亮，相邻卡片的边缘也会亮起，看起来像同一个光源在页面上移动，而不是每张卡片各自的悬停效果。指针离开时光会淡出；键盘把焦点移到某张卡片时，光会移到那张卡片上。

- **落地页的功能区**，三到九个功能。
- **深色营销页**，用一点细微的互动让网格更有生气。

## 何时不用

- **自带配图的功能**：请使用 `FeatureBento`，它的每张卡片都有动态绘图。
- **静止时就要看起来可点的导航卡片**：请使用带明确行动按钮的 `Card`。
- **密集的数据**：请使用表格或指标区块。

## 用法

```xml
<arc:SpotlightGrid Title="Everything in one place" MaxColumns="3">
    <arc:SpotlightGridItem Title="Instant search" Description="Every document answers in under 50 ms." Icon="{x:Static arc:ArcIcons.Search}" />
    <arc:SpotlightGridItem Title="Branch previews" Description="Every change gets its own link." Badge="New" />
</arc:SpotlightGrid>
```

没有项目时网格显示六个虚构的功能；把 `ShowsSampleItems` 设为 false 则保持为空。`Icon` 接受任何 24×24 的描边图形，例如 `ArcIcons`。

### 只有网格

<Demo name="CustomItems" />

没有小标签、标题和说明时，标题区会省略，只留下网格。只要每列不窄于 `MinColumnWidth`（260）就会增加列，最多 `MaxColumns`（3）列。`IsSpotlightEnabled` 可关闭光效。`ItemInvoked` 报告在卡片上的点击、轻点、Enter 或 Space。

## API 参考

<!-- api: SpotlightGrid -->

## 无障碍

- 区块以标题命名，标题是二级标题。
- 每张卡片都可聚焦，以标题命名，说明作为帮助文本；键盘焦点环在卡片外 4 像素。
- 光效只是装饰：没有任何内容依赖它，开关光效对比度都不变。

### 键盘

| 按键 | 操作 |
|---|---|
| `Tab` | 依次经过各张卡片；光跟随焦点。 |
| 方向键 | 按行列在卡片之间移动。 |
| `Home` / `End` | 第一张或最后一张卡片。 |
| `Enter` / `Space` | 触发 `ItemInvoked`。 |

## 动效

| 变化 | 完整 | 减弱 | 关闭 |
|---|---|---|---|
| 光跟随指针 | 没有回弹的快速弹簧 | 直接跟随 | 直接跟随 |
| 光出现和淡出 | 160 毫秒出现，180 毫秒淡出 | 相同 | 立即 |

跟随指针不分配内存：光的渐变已缓存，每一帧只移动绘制的原点。

## 响应式

- 列数随宽度变化：宽页面三列，平板两列，手机一列。
- 区块自身宽度小于 720（`:compact`）时，标题变小，边距收紧。
- 在触屏上，手指按下时光出现在手指下，抬起时淡出。

## 相关组件

- `FeatureBento`：带动态绘图的卡片网格。
- `Card`：单张内容卡片。
