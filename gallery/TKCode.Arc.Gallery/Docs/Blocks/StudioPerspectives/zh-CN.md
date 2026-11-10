---
title: Studio perspectives
summary: 评价舞台：一次展示一条大号引言，附上说话人和公司，会自动切换，切换时旧的模糊消失、新的上升出现，并带进度条、箭头和暂停按钮。
source-hash: 18c6de3706a2
---

<Demo name="Preview" />

## 何时使用

`StudioPerspectives` 是一个评价舞台：它不是一墙小卡片，而是把整个宽度给一条引言，用展示字体大号排出，下面是说话人、职位和公司。几秒后，旧引言模糊消失，下一条上升出现，人物稍晚一拍跟着切换。每条引言对应一根细进度条，显示当前位置和距离下一条还有多久；箭头和暂停按钮让读者接手控制。指针停在舞台上或焦点进入舞台时计时会暂停，读者不会在阅读中途错过内容。

- **落地页**中介于功能和价格之间的社会证明区。
- **案例页**，轮流展示几位客户的评价。
- **宣传或活动页**，有几条有分量的引言。

## 何时不用

- **很多条需要一起浏览的短评**：请使用卡片网格。
- **评分和星级**：本区块没有评分。
- **自己团队的成员**：请使用 `TeamShowcase`。

## 用法

```xml
<arc:StudioPerspectives Title="What studios say" Interval="0:0:8">
    <arc:StudioPerspectivesQuote Text="We plan from one board now." Name="Amara Lindqvist" Role="Creative Director" Company="Fieldnote Studio" />
    <arc:StudioPerspectivesQuote Text="Nothing gets in the way." Name="Diego Ferraz" Role="Lead Engineer" Company="Harbor and Pine" />
</arc:StudioPerspectives>
```

没有引言时舞台显示四条虚构的引言；把 `ShowsSampleQuotes` 设为 false 则保持为空。引言的 `Image` 会替换生成的头像。

### 自己的引言

<Demo name="CustomQuotes" />

`SelectedIndex` 是双向的。`Next()`、`Previous()`、`Pause()` 和 `Play()` 可在代码中控制舞台；`IsPaused` 反映暂停按钮的状态。`IsAutoAdvance` 关闭计时，`Interval` 设置每条停留多久（7 秒）。`QuoteChanged` 报告每一次切换，以及是否由计时触发。

## API 参考

<!-- api: StudioPerspectives -->

## 无障碍

- 舞台以标题命名，标题是二级标题。
- 上一条、暂停和下一条按钮的名称分别是 `PreviousLabel`、`PauseLabel` 或 `PlayLabel`，以及 `NextLabel`。
- 读者自己切换时会播报（“Quote 2 of 4, Diego Ferraz, Lead Engineer · Harbor and Pine”）；计时切换不播报，不会打断读屏。
- 指针在舞台上、焦点在舞台内或按下暂停后，计时都会停止，满足可暂停移动内容的要求。

### 键盘

| 按键 | 操作 |
|---|---|
| `Tab` | 移到各个按钮。 |
| `Left` / `Right` | 焦点在舞台内时切到上一条或下一条。 |
| `Home` / `End` | 第一条或最后一条。 |
| 按钮上的 `Enter` / `Space` | 按下按钮。 |

## 动效

| 变化 | 完整 | 减弱 | 关闭 |
|---|---|---|---|
| 引言切换 | 旧引言模糊消失，新引言上升 4 像素并变清晰；高度以弹簧跟随 | 120 毫秒交叉淡化 | 立即 |
| 人物切换 | 在引言之后 80 毫秒上升出现 | 交叉淡化 | 立即 |
| 进度条 | 在 `Interval` 内填满 | 填满 | 填满 |

## 响应式

- 区块自身宽度小于 640（`:compact`）时，引言从 36 降到 22，边距收紧。
- 舞台最宽 880；长引言会换行，舞台高度以弹簧随之增长。
- 只有一条引言时，按钮和进度条会隐藏。

## 相关组件

- `TeamShowcase`：介绍自己的团队。
- `Carousel`：任意内容的通用轮播。
- `HeroSignup`：通常作为页面开头的区块。
