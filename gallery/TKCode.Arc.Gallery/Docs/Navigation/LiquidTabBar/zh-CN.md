---
title: Liquid tab bar
summary: 用于应用主要分区的图标标签。一枚实心透镜标出所选标签并移到下一个，把它下方的标签以反色绘制，描边图标因此变成实心；也可以拖动或甩动它。
source-hash: 711ba2f97bb2
---

<Demo name="Preview" />

## 何时使用

`LiquidTabBar` 在几个同级分区之间切换，每个分区由一个图标和一个简短的文字标签表示。选中的标签上盖着一枚透镜：一块前景色的胶囊，
大小与该标签完全一致。选中新的标签时，透镜滑过去，途中宽度变成新标签的宽度，不会被拉长。透镜下方的标签会再画一遍，前景色与背景色互换，
描边图标因此变成实心，徽标的颜色也随之翻转。默认只有选中的标签显示文字，且两段文字从不重叠：透镜出发前旧文字已经消失，落定时新文字才出现。

- **应用的几个主要区域**：手机布局中的底部栏，或紧凑窗口中的顶部栏。
- **用户经常往返的少数分区**：看过一次文字后，只看图标也能认出来。
- **带计数的分区**：例如未读消息或待办任务，以徽标显示在图标上。

## 何时不用

- **只有文字的标签页**，各自在页面里带一个面板：`Tabs` 能处理，标签多时还可以滚动。
- **选择视图或筛选条件**（日、周、月）：`SegmentedControl` 更小巧。
- **网站页头里的链接菜单**：`MorphNav`。
- **超过五六个分区**：标签栏不会滚动或换行，只靠图标也不再清楚。

## 用法

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:LiquidTabBar SelectedValue="{Binding Section}" AutomationProperties.Name="App sections">
        <arc:LiquidTabBarItem Value="feed" Label="Feed" Glyph="{x:Static arc:ArcIcons.House}" />
        <arc:LiquidTabBarItem Value="chat" Label="Chat" Glyph="{x:Static arc:ArcIcons.MessageCircle}"
                              Badge="2" BadgeLabel="2 new" />
        <arc:LiquidTabBarItem Value="me" Label="Me" Glyph="{x:Static arc:ArcIcons.User}" />
    </arc:LiquidTabBar>
</UserControl>
```

`LiquidTabBar` 位于 Special 包中：在 `UseArcControls()` 旁边调用 `UseArcSpecial()` 注册它。每个标签是一个 `LiquidTabBarItem`，
带有 `Value`、`Label` 和 `Glyph`（24 × 24 的描边几何图形，例如 `ArcIcons`）。标签栏是一个可选择的列表：把 `SelectedValue`
双向绑定到当前分区的值，或者使用 `SelectedIndex` 与 `SelectionChanged`。只要有标签，就总有一个被选中；禁用的标签会被跳过。

标签栏只负责切换，不承载各分区的内容。请在它旁边按 `SelectedValue` 显示对应内容，例如放在 `ContentControl` 或 `ArcContentSwap`
中，就像预览示例那样。

使用 `ItemsSource` 时，为 `LiquidTabBarItem` 编写样式，把它的 `Label`、`Glyph` 和 `Badge` 绑定到数据项；没有 `Label` 的标签会退回
使用其内容或值的文字。没有 `Glyph` 的标签是纯文字标签，始终显示文字。请保留默认的项面板：透镜依据标签栏自己的布局移动。

### 文字显示

`Labels` 决定文字显示在哪里。`Active`（默认）只展开选中的标签；`Always` 给每个标签都显示文字，标签栏也更宽；`None` 只留下图标，适合空间紧张时。
无论哪种模式，屏幕阅读器都能读到文字。切换模式时，各标签以与切换选中项相同的弹簧展开或收起。

<Demo name="Labels" />

### 徽标

`Badge` 在标签的图标上显示一个计数。计数变化时，数字滚动到新的数值；为 0 时徽标缩小消失。`BadgeLabel` 是屏幕阅读器在文字之后读出的内容
（例如 "8 unread"）；不设置时读出数字。

<Demo name="Badges" />

### 选中时的图标

在透镜里，标签会把图标填实绘制。设置 `ActiveGlyph` 后，透镜里改画另一个图形，例如用打开的文件夹代替关闭的文件夹。

<Demo name="ActiveGlyph" />

### 拖动与甩动

从选中的标签开始拖动：透镜跟随指针，前缘略微领先；松手后停在最近的标签上。快速甩动时，速度越快它被抛得越远，而且绝不会停回起始的标签；
拉过最外侧的标签时，它的让步越来越小，像被拉伸的橡皮筋。拖动期间各标签保持大小不变，松手时才改变选中项。没有移动的短按就是普通点击。

### 最佳实践

**建议**

- 文字保持一个短词，标签不超过五个。
- 每个标签都设置 `Label`，即使使用 `Labels="None"`：它为屏幕阅读器命名该标签。
- 为徽标设置 `BadgeLabel`，说明数字计的是什么。

**避免**

- 在同一个标签栏中混用有图标和没有图标的标签。
- 用定时器改变选中项：透镜每次移动都会吸引视线。

## API 参考

<!-- api: LiquidTabBar -->

<!-- api: LiquidTabBarItem -->

## 无障碍

- 标签栏是一个标签列表，除非 `AutomationProperties.Name` 另有设置，否则名称为 "Sections"；每个标签都是一个带选中状态的标签。
- 标签的名称是它的文字，后面跟着徽标：设置了 `BadgeLabel` 时读它，否则读数字。
- 透镜下的反色只是绘制效果，不是第二组元素，所以每个标签只会被报告一次。
- 键盘焦点会在标签周围显示 2px 的焦点环；使用指针时不显示。

### 键盘交互

| 按键 | 操作 |
|---|---|
| Tab | 把焦点移到选中的标签；只有选中的标签在 Tab 顺序中。 |
| Right / Left | 焦点与选中项一起移到下一个或上一个可用标签；在最后一个上按 Right 回到第一个，在第一个上按 Left 到最后一个。 |
| Home / End | 跳到第一个或最后一个可用标签并选中它。 |
| Enter 或 Space | 选中获得焦点的标签。 |

## 动效

- 透镜由一个弹簧驱动（visual duration 0.42s，无回弹），选中项在途中再次改变时保留速度。它的边缘跟随两侧之间的标签，所以无论在哪里，
  它都恰好是一个标签的宽度。
- 标签以同一个弹簧变宽和变窄。上一个标签的文字在 50ms 内消失；下一个标签的文字在其变宽的最后十分之一里淡入，同时滑动 4px 到位。
- 鼠标悬停在其他标签上时，透镜朝向那一侧的边缘会向相邻标签伸出 14% 的距离。
- 按下时透镜内缩 2px；拖动时前缘领先；徽标数字以平滑的弹簧滚动。
- 减少动效时，透镜和文字立即就位，不滑动、不伸出、不平移；关闭动效时相同。

| 变化 | 完整 | 减少 | 关闭 |
|---|---|---|---|
| 透镜 | 沿弹簧滑动 | 跳到位 | 跳到位 |
| 标签展开与收起 | 弹簧 | 跳到位 | 跳到位 |
| 朝悬停标签伸出 | 有 | 无 | 无 |
| 悬停时的图标颜色 | 160ms | 120ms | 立即 |

## 响应式

- 只显示图标的标签宽 44px，显示文字时再加宽文字的宽度；标签栏与其标签一样宽，不会滚动。空间狭窄时，`Labels="None"` 让它保持最小。
- 标签栏的尺寸是其标签最终稳定后的尺寸，所以透镜滑动时周围的内容不会移动。
- 使用触摸时，横向拖动移动透镜，竖向滑动仍会滚动页面。只有鼠标会让透镜伸出。

## 相关组件

- `Tabs`：带各自面板的文字标签页。
- `SegmentedControl`：紧凑的筛选与视图切换。
- `MorphNav`：带菜单的站点导航。

| 组件 | 适合的情况 |
|---|---|
| `LiquidTabBar` | 少数几个带图标、经常切换的分区 |
| `Tabs` | 文字标签、标签很多，或标签带面板 |
| `SegmentedControl` | 在少数几个视图或筛选之间做紧凑的选择 |
