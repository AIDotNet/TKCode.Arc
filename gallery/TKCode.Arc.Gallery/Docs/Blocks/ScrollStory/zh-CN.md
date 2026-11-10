---
title: Scroll story
summary: 随页面滚动讲述的故事：文字步骤依次经过阅读线，旁边固定在视野中的舞台随之切换到当前步骤的画面。
source-hash: b1a8c4d387d0
---

<Demo name="Preview" />

## 何时使用

`ScrollStory` 在读者滚动时讲解一个流程。文字步骤排在一列，每一步占一个较高的位置；另一列的舞台始终留在视野中：区块经过窗口时，舞台保持在可见部分的中间，并在区块的顶部和底部停住。越过阅读线（窗口中间略偏上）的步骤成为当前步骤：它的文字完全显示，其他步骤变暗，舞台交叉淡化到这一步的画面。画面是内置的小应用窗口绘图（收件箱、看板、审阅中的文档、上升的图表），也可以换成你自己的图片。

- **落地页的“工作方式”区**，三到六个步骤。
- **产品导览**，每个功能配一张画面。
- **讲究阅读顺序的故事页**。

## 何时不用

- **需要并排比较的内容**：请使用 `FeatureBento` 或 `SpotlightGrid`。
- **不会滚动的页面**：只有祖先元素滚动时舞台才会移动。
- **长文章**：请使用 `Markdown`；这里的步骤位置是有意做得很高的。

## 用法

```xml
<arc:ScrollStory Title="From request to release">
    <arc:ScrollStoryStep Kicker="Capture" Title="Every request lands in one inbox" Scene="Capture" Body="Forms, email and chat feed the same list." />
    <arc:ScrollStoryStep Kicker="Launch" Title="Ship it and watch it land" Scene="Launch" Body="Release and follow adoption." />
</arc:ScrollStory>
```

把区块放在会滚动的页面中（任何祖先 `ScrollViewer`）。没有步骤时它讲一个虚构的四步故事；把 `ShowsSampleSteps` 设为 false 则保持为空。步骤的 `Scene` 选择内置画面（`Capture`、`Arrange`、`Review`、`Launch`），`Image` 可替换它。`Kicker` 默认是步骤编号。

`ActiveIndex` 报告舞台上的步骤，变化时触发 `ActiveStepChanged`。`ScrollToStep(index)` 把某一步滚动到视野中。

## API 参考

<!-- api: ScrollStory -->

## 无障碍

- 区块以标题命名，标题是二级标题。
- 每一步都可聚焦，以标题命名，正文作为帮助文本。聚焦某一步会把它滚动到视野中并设为当前步骤，所以舞台也跟随键盘。
- 舞台以当前步骤命名；画面本身只作装饰。

### 键盘

| 按键 | 操作 |
|---|---|
| `Tab` / `Shift+Tab` | 下一步或上一步。 |
| `Down` / `Up` | 下一步或上一步。 |
| `Home` / `End` | 第一步或最后一步。 |

## 动效

| 变化 | 完整 | 减弱 | 关闭 |
|---|---|---|---|
| 舞台跟随滚动 | 随页面移动并固定在视野中（自身没有动画） | 相同 | 相同 |
| 画面切换 | 240 毫秒交叉淡化，新画面上升 10 像素 | 交叉淡化 | 立即 |
| 步骤变暗和变亮 | 240 毫秒透明度变化 | 160 毫秒 | 立即 |

固定效果直接跟随滚动位置：视口变化时只更新渲染变换，不会触发布局。

## 响应式

- 区块自身宽度小于 760（`:compact`）时，舞台隐藏，步骤排成一列，每一步在文字下方显示自己 200 高的画面，并且全部清晰显示。
- 宽屏上两列平分宽度，间隔 56，每一步的位置至少 380 高，步骤逐一经过阅读线。

## 相关组件

- `FeatureBento` 和 `SpotlightGrid`：一起展示的功能。
- `Stepper`：应用中由用户逐步完成的流程。
