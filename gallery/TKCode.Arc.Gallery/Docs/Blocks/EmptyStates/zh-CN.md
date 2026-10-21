---
title: 空状态集
summary: 同一幅插图中的四种空状态，切换标签页时，图形会在各场景之间变形。
source-hash: 3680320a4c58
---

<Demo name="Preview" />

## 何时使用

- 页面或面板可能因多种原因为空：没有搜索结果、没有连接、没有待读内容，或页面不存在。
- 空状态有一个明确的下一步操作，且该操作需要一点时间，例如重试、刷新或搜索。
- 设置页或帮助页中并排展示某个产品的各种空状态。

## 何时不用

- 只有一个带图标和按钮的空列表：使用 `EmptyState`。
- 内容仍在加载：使用 `Skeleton`。
- 页面已显示内容时出现错误：使用 `Alert`。

## 用法

没有任何项时，区块会显示 Arc 的四个场景及其文案。处理 `ActionInvoked` 并设置 `Phase`，即可显示操作的结果。区块本身不执行任何操作。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:EmptyStates Label="工作区" />
</UserControl>
```

### 场景与文案

每个 `EmptyStatesItem` 对应一个标签页。`Scene` 决定图形，可选值为 `NoResults`、`Offline`、`CaughtUp` 或 `NotFound`。为 Idle 阶段设置 `Header`、`Title`、`Description` 与 `ActionLabel`，并为结果设置 `Done` 版本。未设置的文字使用 Arc 为该场景提供的文案。只有一项时，会隐藏标签栏。

<Demo name="CustomItems" />

```xml
<arc:EmptyStates>
    <arc:EmptyStatesItem Scene="Offline"
                         Title="同步已暂停"
                         ActionLabel="恢复同步" />
    <arc:EmptyStatesItem Scene="NotFound" />
</arc:EmptyStates>
```

### 操作与阶段

按下按钮会引发 `ActionInvoked`，参数为该项以及按下时所处的阶段，随后以相同参数执行 `ActionCommand`。工作进行期间将 `Phase` 设为 `Loading`：按钮显示加载指示器，描述变为 `LoadingDescription`，标题与按钮文字保持不变。成功时设为 `Done`，失败时设为 `Idle`。加载期间，按下操作会被忽略。

<Demo name="AsyncRetry" />

```csharp
states.ActionInvoked += async (_, e) =>
{
    if (e.Phase == EmptyStatesPhase.Done)
    {
        states.Phase = EmptyStatesPhase.Idle;
        return;
    }
    states.Phase = EmptyStatesPhase.Loading;
    var ok = await sync.RetryAsync();
    states.Phase = ok ? EmptyStatesPhase.Done : EmptyStatesPhase.Idle;
};
```

要在失败的尝试之后更改文案，请在阶段仍为 `Loading` 时设置项的 `Description`。新文字会随切换回 `Idle` 一同出现。

### 选择

`SelectedIndex` 与 `SelectedItem` 是双向的，并保持同步。选择另一个标签页会将 `Phase` 恢复为 `Idle`。若要在打开时就显示某个结果，请与选中项一同设置 `Phase`。

```xml
<arc:EmptyStates SelectedIndex="1" Phase="Done" />
```

### 窄屏布局

区块跟随自身宽度变化。宽度在 420 及以下时，标签页使用更小的文字和内边距。宽度在 560 及以上时，标题变大。放不下的标签保持其宽度，标签栏会滑动，以保持选中的标签页可见。

<Demo name="Narrow" />

## API 参考

<!-- api: EmptyStates -->

<!-- api: EmptyStatesItem -->

## 无障碍

- 区块是一个由 `Label` 命名的区域。标签栏是一个由 `TabListLabel` 命名的标签页列表，每个标签页都会报告自己是否被选中。
- 只有选中的标签页可通过 Tab 键获得焦点。ArrowLeft 与 ArrowRight 在标签页之间移动，并首尾循环。Home 与 End 跳到第一个和最后一个标签页。移动即选中该标签页。
- 图形是一幅以其所示内容命名的图像，例如“一朵云通过一条断开的线连接到一台设备”。设置 `IllustrationLabel`、`LoadingIllustrationLabel` 与 `DoneIllustrationLabel`，即可用其他语言为图形命名。
- 每次阶段变化后，新的标题与描述，或加载文字，都会以礼貌式播报。

## 动效

- 切换标签页时，图形的每个形状都会以弹簧动画过渡到下一个场景。轮廓逐点变形，线条稍后再逐步绘出。颜色在 480 毫秒内交叉过渡。
- 阶段变化时，以更短的延迟轻推同一批形状。
- 旧文案带着模糊向上移开，新的标题与描述随之升起。文案高度以弹簧方式调整以适应内容，因此按钮不会跳动。标签页的高亮会滑动到选中的标签页。
- 使用鼠标时，图形的各层会略微朝指针方向倾斜。
- 动效等级为 Reduced 时，旧图形交叉淡化为新图形，文案淡入淡出。等级为 Off 时，立即切换。

## 相关组件

- `EmptyState`：用于带图标、文字说明与操作的单个空列表。
- `Skeleton`：内容仍在加载时使用。
- `Alert`：已显示内容的页面中出现错误时使用。
