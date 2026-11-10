---
title: 吸顶
summary: 让滚动页面里的任意元素滚到顶部后停住，像 CSS sticky 一样：工具栏始终可见，分组标题依次把上一个推走。
source-hash: 4472abbe401d
---

<Demo name="Preview" />

## 何时使用

`Sticky` 是一组附加属性，不是控件：在 `ScrollViewer` 内的任意元素上设置 `arc:Sticky.IsEnabled="True"`，它会正常滚动，
直到到达视口顶部后停在那里，效果与 CSS `position: sticky; top: 0` 相同。

- **工具栏、筛选栏**：在长页面里始终触手可及。
- **分组列表的分组标题**：每个标题占据顶部，直到下一组把它推走。
- **摘要信息**：例如随读者向下填写表单的购物车合计。

## 何时不用

- **元素根本不应滚动**：把它放在 `ScrollViewer` 之外，例如放在其上方的 `DockPanel` 中。
- **横向滚动**：`Sticky` 只跟随纵向偏移；横向滚动时保持不动的列需要带冻结列的表格。
- **元素已经有 `RenderTransform`**：`Sticky` 用自己的渲染平移来移动元素（见下文）。

## 用法

在滚动内容中的元素上设置附加属性即可，不需要其他东西：不需要占位元素，也不用改动 `ScrollViewer`。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <ScrollViewer Height="300">
        <StackPanel>
            <TextBlock Text="Title" Margin="16" />
            <Border arc:Sticky.IsEnabled="True" Background="White" Padding="16,8">
                <TextBlock Text="Toolbar" />
            </Border>
            <TextBlock Text="Long content" Height="1000" />
        </StackPanel>
    </ScrollViewer>
</UserControl>
```

工作原理：

1. 元素监听自己的有效视口（`ScrollViewer` 显示出来的那部分）。视口上沿越过元素后，元素按差值向下平移，从而停在顶部。
2. 只改变元素的 `RenderTransform`，从不改变布局：内容高度不变，下方内容也不会跳动。
3. 元素不会离开它的父容器。父容器的下沿到达元素时，元素随父容器一起滚走。
4. 元素上方或周围的内容稍后加载或改变尺寸时，会在下一次布局中重新计算。

### 吸顶状态的样式

元素停在顶部期间 `Sticky.IsStuck` 为 true。用选择器选中它来加底色、边线或阴影：

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <UserControl.Styles>
        <Style Selector="Border.toolbar[(arc|Sticky.IsStuck)=True]">
            <Setter Property="BoxShadow" Value="{arc:ArcDesignTokenResource ShadowResting}" />
        </Style>
    </UserControl.Styles>
    <!-- ... -->
</UserControl>
```

内容会从元素下方滚过，所以请给元素设置不透明的背景。每当 `IsStuck` 变化，元素上会触发 `StuckChanged` 事件（冒泡，
参数为 `StickyStuckChangedEventArgs.IsStuck`）；在代码中使用 `Sticky.AddStuckChangedHandler` 与
`Sticky.RemoveStuckChangedHandler`。

### 分组列表

把每个标题和它的各行放在各自的面板中。标题只在本组可见时吸顶，所以下一组的标题到来时会把它推走，就像通讯录一样。

<Demo name="GroupedList" />

### 偏移

`Sticky.Offset` 在视口上沿与吸顶元素之间留出间距，例如让悬浮的胶囊留些空隙，或停在另一条栏的下方。

<Demo name="Offset" />

### 限制

- **RenderTransform**：元素不能有自己的 `RenderTransform`。如果设置 `Sticky.IsEnabled` 时元素已有 `RenderTransform`，
  `Sticky` 不会改动该元素，并记录一条警告。请把元素包在一个 `Border` 里，改为让外层吸顶。
- **ZIndex**：吸顶期间元素的 `ZIndex` 提升为 `Sticky.ZIndex`（默认 1），使它绘制在下方滚过的内容之上，离开后恢复原值。
  `ZIndex` 只对同一面板中的兄弟元素排序，所以嵌在某个面板深处的吸顶元素仍可能被后面兄弟面板的内容盖住。
  请让吸顶元素作为滚动内容的直接子元素，或给包含它的面板设置 `ZIndex`。

## API 参考

<!-- api: Sticky -->

## 无障碍

- `Sticky` 只在屏幕上移动元素。阅读顺序与 Tab 顺序仍是内容本身的顺序，元素里的控件在吸顶时照常可用。
- 吸顶的栏会盖住从下方滚过的内容。键盘焦点移到栏下方的控件时，`ScrollViewer` 会把它滚到顶部，栏可能会挡住它；
  在需要的地方用 `Sticky.Offset` 或内容的上边距留出空间。

## 动效

- 吸顶直接跟随滚动位置，本身没有动画。
- 你附加在 `IsStuck` 上的变化（阴影、底色）可以渐变：像 Preview 那样在 `:motion-full` 下声明过渡，并在元素上设置
  `arc:ArcMotion.Tracks="True"`，这样减少动效时它们会关闭。

## 响应式

- 元素保持自己的宽度与布局；吸顶时只改变它的纵向位置。
- 视口或内容尺寸改变时，会在下一次布局中重新计算位置。

## 相关组件

- `ScrollArea`：Arc 风格的滚动区域。
- `TocProgress`：随读者向下阅读而跟进的目录。
