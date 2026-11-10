---
title: Feature bento
summary: 便当格功能网格：大小不同的卡片，每张都有一幅小动态绘图，只在出现在屏幕上时才动。
source-hash: 811dfbf4702c
---

<Demo name="Preview" />

## 何时使用

`FeatureBento` 把功能排成便当格：四种尺寸的卡片排进六列，一两个重要的功能占大卡片，其余的围绕它们填满。每张卡片顶部有一幅小动态绘图，下面是标题和一句说明。绘图是内置的，风格安静：不断画出的折线图、绕着中心转动的圆点、输入命令并给出结果的终端、起伏的柱条、来回传递数据包的两台设备、带扫描线的盾牌。它们只在卡片出现在屏幕上且动效完整时才动，其他时候保持静止。

- **平台或产品概览**，四到八个分量不同的功能。
- **落地页区块**，需要视觉节奏但没有截图。

## 何时不用

- **分量相同的功能**：请使用 `SpotlightGrid`，它的卡片大小一致。
- **按步骤讲解**：请使用 `ScrollStory`。
- **真实数据**：请使用图表和分析区块；这里的绘图只是示意。

## 用法

```xml
<arc:FeatureBento Title="One platform, every stage">
    <arc:FeatureBentoItem Title="Live metrics" Art="Pulse" Size="Large" Description="Every request lands on the chart within a second." />
    <arc:FeatureBentoItem Title="Integrations" Art="Orbit" Size="Tall" Description="Connect the tools you already use." />
    <arc:FeatureBentoItem Title="Secure by default" Art="Shield" Size="Small" Description="Single sign-on on every plan." />
</arc:FeatureBento>
```

没有项目时网格显示六张虚构的卡片；把 `ShowsSampleItems` 设为 false 则保持为空。

### 尺寸与排列

| 尺寸 | 六列 | 两列 |
|---|---|---|
| `Small` | 2 × 1 | 1 × 1 |
| `Medium` | 3 × 1 | 1 × 1 |
| `Wide` | 4 × 1 | 2 × 1 |
| `Tall` | 2 × 2 | 1 × 2 |
| `Large` | 4 × 2 | 2 × 2 |

卡片按顺序放置，每张放在第一个放得下的空位，所以高卡片旁边会留出位置给后面的卡片。只有一列时每张卡片占一行。

### 自己的卡片

<Demo name="CustomItems" />

`Art` 选择绘图（`None` 表示不画）。`CommandText` 和 `ResultText` 设置 `Typing` 绘图的两行文字。`ItemInvoked` 报告在卡片上的点击、轻点、Enter 或 Space。

## API 参考

<!-- api: FeatureBento -->

## 无障碍

- 区块以标题命名，标题是二级标题。
- 每张卡片都可聚焦，以标题命名，说明作为帮助文本；键盘焦点环在卡片外 4 像素。
- 绘图只作装饰，对读屏隐藏。

### 键盘

| 按键 | 操作 |
|---|---|
| `Tab` | 按阅读顺序经过各张卡片。 |
| 方向键 | 下一张或上一张卡片。 |
| `Home` / `End` | 第一张或最后一张卡片。 |
| `Enter` / `Space` | 触发 `ItemInvoked`。 |

## 动效

| 变化 | 完整 | 减弱 | 关闭 |
|---|---|---|---|
| 绘图 | 在屏幕上时持续运动 | 静止 | 静止 |
| 悬停的卡片 | 边框在 160 毫秒内加深 | 立即 | 立即 |

所有绘图共用一个帧时钟，卡片滚出视野或窗口隐藏时暂停，绘制时不分配内存。

## 响应式

- 区块自身宽度小于 860（`:compact`）时网格为两列；小于 520（`:narrow`）时为一列，行高略增。
- 行高固定（240），绘图保持比例；需要更多空间的文字应尽量简短。

## 相关组件

- `SpotlightGrid`：大小一致、带互动光效的卡片。
- `ScrollStory`：随滚动展开的导览。
