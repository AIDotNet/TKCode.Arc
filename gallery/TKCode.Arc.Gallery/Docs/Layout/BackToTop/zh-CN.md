---
title: 回到顶部
summary: 长列表、日志或信息流滚动超过阈值后出现的圆形按钮，点击后平滑滚回顶部。
source-hash: e9f4c68d5bfc
---

<Demo name="Preview" />

## 何时使用

`BackToTop` 监视一个滚动容器，读者向下滚动超过 `Threshold`（默认 200 px）后显示一个小圆形按钮。点击它，内容平滑滚回顶部；
回到上方的途中按钮会再次淡出。

- **长列表与信息流**：读者翻得很深，又想回到开头，例如搜索结果、帖子、通知。
- **日志与消息流**：内容在阅读时仍在增长。
- **很长的设置页或文档页**：放在同一个滚动区域里。

## 何时不用

- **内容很短**，一两屏就能看完：按钮没有意义。（内容无法滚动时它本来也保持隐藏。）
- **跳转到各个章节**：用目录或锚点，而不是一个"回到顶部"按钮。
- **聊天会话**：应当钉在底部最新消息上，滚动方向相反。

## 用法

有两种用法。一种是自己把 `BackToTop` 放在滚动容器上方，并把 `Target` 指向它；另一种是在容器上设置
`BackToTop.IsAttached`，由库把按钮放在视口的一角。

`Target` 可以是 `ScrollViewer`（`ScrollArea` 就是一种），也可以是模板里含有 `ScrollViewer` 的控件（`Timeline`、`Table`、
`TreeView`，或由应用主题提供滚动模板的 `ListBox`）：控件本身是 `ScrollViewer` 时直接使用它，否则在模板应用后使用其中的第一个
`ScrollViewer`。只看纵向偏移：内容可以滚动且滚动超过 `Threshold` 时显示按钮；内容变短或视口变高时隐藏。

### 放在自己的容器上

把按钮放在与容器相同的 `Grid` 单元格里、排在容器之后，用常规的对齐方式和 `Margin` 定位。按钮不在内容里面，所以不会随内容滚动。

```xml
<Grid xmlns="https://github.com/avaloniaui"
      xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
      xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <ScrollViewer x:Name="Log">
        <StackPanel />
    </ScrollViewer>
    <arc:BackToTop Target="{Binding #Log}"
                   HorizontalAlignment="Right"
                   VerticalAlignment="Bottom"
                   Margin="16" />
</Grid>
```

上方的 Preview 就是这样处理一份请求日志。平滑滚动到达顶部时，按钮触发 `ScrolledToTop`，示例用它更新说明文字；
`ScrollToTop()` 与点击按钮效果相同。

### 附加到容器

在容器上写 `arc:BackToTop.IsAttached="True"`，按钮会自动加上。它放在容器的装饰层（AdornerLayer）里，内容滚动时保持不动，
也不改变容器的布局。`BackToTop.Placement` 选择视口的哪一角（默认 `BottomRight`），`BackToTop.Inset` 设置与边缘的距离
（16 px）。把 `IsAttached` 改回 false，或把容器从窗口移除，按钮就会移除。这里把它附加到 `Timeline` 上，`Timeline` 的
`ScrollViewer` 在模板里。

<Demo name="Attached" />

### 阈值与位置

`Threshold`（或容器上的 `BackToTop.Threshold`）是内容需要滚动多远按钮才出现；设为 0 时，内容一离开顶部就显示。
选一个不会遮住内容自身操作的角。

<Demo name="ThresholdAndPlacement" />

### 尺寸与名称

`Size` 让圆形直径为 36（`Small`）、44（`Medium`，默认）或 50（`Large`）像素。`Label` 替换内置的名称与提示"回到顶部"，
否则内置文案跟随应用的语言。

```xml
<arc:BackToTop xmlns:arc="https://github.com/AIDotNet/TKCode.Arc"
               Size="Small"
               Label="Back to the newest entries" />
```

## API 参考

<!-- api: BackToTop -->

## 无障碍

- 按钮的无障碍名称与提示是 `Label`，或本地化的"回到顶部"。
- 与其他按钮一样，Enter 与 Space 触发；焦点环只在键盘聚焦时显示。
- 隐藏时按钮不接收指针输入，也不在 Tab 顺序中。隐藏时若持有焦点，焦点保持原处。
- 滚回顶部后焦点仍在按钮上，因此在读者再次向下滚动之前，再按一次不会有任何作用。

## 动效

- 出现与隐藏：完整动效下按钮淡入、从 0.86 放大到原尺寸并上升 8 px（snappy 弹簧），隐藏时反向；减弱动效只淡入淡出。
  首帧不播动画。
- 滚回顶部：完整动效下偏移从当前位置以 480 ms（in-out）缓动到顶部；减弱动效和关闭动效下直接跳到顶部。滚动途中读者用滚轮、
  滚动条或键盘滚动时，动画立即停止，偏移留在读者设定的位置，也不会触发 `ScrolledToTop`。

## 响应式

- 按钮跟随它监视的视口尺寸：窗口变高、内容不再需要滚动时，按钮隐藏。
- 容器尺寸变化时，附加的按钮保持在视口的同一角。
- 窄屏上优先用 `Size="Small"` 和更小的 `Inset`，让按钮少遮挡内容。

## 相关组件

- `ScrollArea`：带覆盖式滚动条和边缘渐隐的滚动容器，可以作为目标。
- `CyclicPanel`：无限循环的列表，没有可以回去的顶部。
