---
title: Orbit menu
summary: 一个圆形按钮，把它的操作沿圆弧依次展开在自己周围，可以用方向键沿圆环移动。
source-hash: e91e24dd53ba
---

<Demo name="Preview" />

## 何时使用

`OrbitMenu` 是一个圆形触发按钮，把几个相关的操作收在里面。按下它，操作会依次飞出，各自在弹簧驱动下到达触发按钮周围圆弧上的位置，同时触发按钮上的加号变成关闭标记。选择某个操作、在外部按下或按 Escape，操作都会收回。因为每个操作离拇指或指针的距离相同，选择既快又容易够到。

- **创建菜单**：一个浮动的加号，提供照片、语音、文字或文件。
- **快速回应**：对一条消息或一篇帖子，用完整的圆环展开。
- **画布上的工具面板**：在手所在的位置展开。

## 何时不用

- **操作超过大约八个**：圆弧会变得拥挤；请用带文字的菜单。
- **需要名称才能看懂的操作**：圆环只显示图标，名称只出现在提示中。
- **主导航**：要去的地方应放在标签栏或导航里，而不是藏在按钮后面。

## 用法

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:OrbitMenu Label="Create">
        <arc:OrbitMenuAction Glyph="{x:Static arc:ArcIcons.Camera}" Label="Photo" />
        <arc:OrbitMenuAction Glyph="{x:Static arc:ArcIcons.Mic}" Label="Voice note" />
        <arc:OrbitMenuAction Glyph="{x:Static arc:ArcIcons.PenLine}" Label="Text note" />
    </arc:OrbitMenu>
</UserControl>
```

`OrbitMenu` 在 Special 包中：请在 `UseArcControls()` 旁边用 `UseArcSpecial()` 注册。它的子项是 `OrbitMenuAction` 元素，每个有一个 `Glyph` 和一个 `Label`（既是提示文字，也是无障碍名称），也可以通过 `ItemsSource` 绑定。操作会触发自己的 `Click` 并执行它的 `Command`；菜单会带着该操作触发 `ActionInvoked` 并收回，除非 `ClosesOnInvoke` 为 false。`IsOpen`（双向）、`Open()`、`Close()` 和 `Toggle()` 可以在代码中控制它；`Label` 为触发按钮命名，`Glyph` 更改它的图标。

菜单只占触发按钮的空间。操作绘制在周围内容之上，所以要给它们留出空间：`Radius`（默认 88 px）加上展开一侧半个操作的大小。

### 圆弧

`StartAngle` 是第一个操作所在的位置，`SweepAngle` 是其余操作沿圆弧跟随的角度范围，单位是度，从右侧起顺时针计算（-90 是正上方，180 是左边）。默认情况下，操作在上方从左到右展开。扫过 360 时，操作会均匀分布在整个圆环上。

<Demo name="FullRing" />

### 最佳实践

**建议**

- 给每个操作一个简短的 `Label`：这是辅助技术拥有的唯一名称。
- 让圆弧朝向空白区域，例如触发按钮在屏幕底部时向上展开。
- 保持三到六个操作。

**避免**

- 把唯一能完成重要事情的方式藏在圆环后面。
- 把触发按钮放在圆弧会超出窗口的位置。

## API 参考

<!-- api: OrbitMenu -->

<!-- api: OrbitMenuAction -->

## 无障碍

- 触发按钮是一个按钮，名称取自 `Label`，并会报告圆环是否已展开。
- 每个操作会被读作一个菜单项，名称取自其 `Label`，可以通过辅助技术触发。收起的操作对辅助技术和 Tab 顺序都是隐藏的。
- 通过键盘打开时，焦点移到第一个操作；关闭时回到触发按钮。使用鼠标或触摸时不显示焦点环；使用键盘时，焦点环显示在触发按钮或操作周围。

### 键盘交互

| 按键 | 操作 |
|---|---|
| 在触发按钮上按 Enter 或 Space | 打开或关闭圆环。 |
| Right 或 Down | 沿圆环移到下一个操作，到末尾后回到开头。 |
| Left 或 Up | 移到上一个操作，到开头后回到末尾。 |
| Home / End | 移到第一个或最后一个操作。 |
| 在操作上按 Enter 或 Space | 执行该操作并收起圆环。 |
| Escape | 收起圆环，并让焦点回到触发按钮。 |
| Tab 离开 | 收起圆环。 |

## 动效

- 展开时，每个操作比前一个晚 35ms 离开触发按钮，从自身大小的 40% 开始变大，并带着轻微的回弹弹到自己的位置。收起时它们按相反顺序，用快速、无回弹的弹簧回来。
- 触发按钮的图标在一个干脆的弹簧上转动 45 度，于是加号变成关闭标记。
- 操作只通过渲染变换移动；移动过程中不会触发布局。

| 变化 | 完整 | 减弱 | 关闭 |
|---|---|---|---|
| 操作展开 | 从触发按钮出发的依次弹簧 | 原地淡入，160ms | 直接出现 |
| 操作收回 | 按相反顺序快速弹回 | 淡出，180ms | 直接消失 |
| 触发按钮图标 | 在弹簧上转动 | 立即转动 | 立即转动 |

## 响应式

- 触发按钮在任何宽度下都保持大小（52 px），拇指操作很舒适。
- 圆弧保持它的 `Radius`；在手机上，请让它朝向屏幕内部（例如触发按钮在底部时向上），并留出所需的空间。
- 操作宽 44 px，是舒适的触摸目标。

## 相关组件

- `DropdownMenu` 列出带名称的操作。
- `ActionMorph` 把一个操作展开成一个小面板。

| 组件 | 适用场景 |
|---|---|
| `OrbitMenu` | 几个熟悉的操作，一伸手就够得到 |
| `DropdownMenu` | 操作需要名称，或者数量很多 |
| `ActionMorph` | 一个操作需要一个输入框或几个选项 |
