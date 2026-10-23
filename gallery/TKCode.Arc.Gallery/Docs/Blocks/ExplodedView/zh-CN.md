---
title: 爆炸视图
summary: 随滚动而分解的屏幕：图层向后倾斜，分开并附上标签，轮流成为焦点并各自配有说明，最后重新合成一个屏幕。
source-hash: ef8070d398d7
---

<Demo name="Preview" />

## 何时使用

`ExplodedView` 是用于发布或营销页面的一个板块，用来展示一个屏幕是如何构成的。一叠大小相同的图层，每层一张图，开始时是一个平面的屏幕。随着用户滚动，这叠图层向后倾斜并转动，各层在深度上分开，每层旁边的标签随之画出。接着各层轮流成为焦点：其他图层变暗，焦点图层采用强调色，舞台下方的说明解释该图层。最后图层收拢，屏幕重新立起。一排停靠按钮可以直接跳到整个屏幕、任意一层，或是重新合成后的屏幕。

- **设计系统或 UI 套件页面**，其中的图层是一个示例屏幕的令牌、网格、组件和浮层，每段说明讲述该图层负责什么。
- **产品发布**，从背景到漂浮的提醒逐层讲解一个重新设计的屏幕，每层讲一个论点。
- **带屏幕的硬件**，例如手表或车载显示屏，图层分别是玻璃、界面以及其背后的传感器。
- **案例研究**，展示一个团队如何把一个繁忙的屏幕拆分成各司其职的平静图层。

## 何时不用

- **需要用户阅读或操作的内容。** 舞台是一幅图片：它不接受输入，屏幕阅读器会跳过它（图层会以文字列表的形式呈现）。真正的控件应放在页面本身上。
- **需要用户完成的一系列步骤。** 任务进度请使用 `Stepper`。
- **需要浏览的一组幻灯片或图片。** 使用 `Carousel`，它一次只显示一项，并通过按钮和滑动翻页。
- **一篇配有几张插图的长文。** 滚动驱动的板块会吸引注意力；每页一个最为合适。
- **超过八个图层。** 标签会在右侧堆叠，停靠按钮也会过于拥挤；请把内容拆成两个板块。

## 用法

从下往上，每层添加一个 `ExplodedLayer`。每层的 `Content` 是任意控件，按 `LayerSize`（默认 280 × 620，即手机屏幕尺寸）布局。`Name` 是标签的第一行、说明的标题以及停靠按钮的名称；`Note` 是标签的第二行；`Body` 是该图层获得焦点时显示的说明。`StartCaption` 和 `EndCaption` 分别是图层堆叠打开之前和收拢之后的说明。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:ExplodedView Height="760"
                      Title="Made of layers"
                      Description="Every layer does one job, and together they stay quiet.">
        <arc:ExplodedView.StartCaption>
            <arc:ExplodedCaption Title="A single screen" Body="Scroll to lift it apart, layer by layer." />
        </arc:ExplodedView.StartCaption>
        <arc:ExplodedLayer Name="Base" Note="Two neutral tokens" Kind="Plate"
                           Body="One quiet base layer under everything else.">
            <Panel />
        </arc:ExplodedLayer>
        <arc:ExplodedLayer Name="Content" Note="Fixed-width numbers"
                           Body="The numbers, the chart and the list.">
            <TextBlock Margin="16,52" Text="38.6 km" FontSize="34" />
        </arc:ExplodedLayer>
    </arc:ExplodedView>
</UserControl>
```

`ExplodedView` 派生自 Avalonia 的 `TemplatedControl`。场景是 `Progress` 的函数，从 0（整个屏幕）到 1（重新合成），它跟随滚动位置变化；`SmoothedProgress` 是该值经过柔和的过阻尼弹簧之后的结果，舞台绘制的正是它。`CurrentStop` 为 0 时表示整个屏幕，1 到 N 表示各图层，N + 1 表示重新合成后的屏幕；每当它变化时都会引发 `StopChanged`。控件会设置 `:exploded`、`:wide`、`:compact`、`:tall-stage`、`:framed` 和 `:region-focus` 这几个伪类，供样式使用。

### 结构

1. **引言**（`Title`、`Description`）：标题使用展示字体，随控件变宽从 28 px 增至 52 px，标题下方是一到两句话。
2. **舞台**：各图层，以透视方式绘制。`Plate` 图层是不透明的，带 1 px 边框；`Sheet` 图层只显示其内容，在图层分开时显示虚线轮廓；`Grid` 图层是一种 Sheet，其内容随分开而淡入，用于只在爆炸视图中存在的参考线。所有图层共用 `LayerCornerRadius`。
3. **标签**：每层右侧一个标签，在图层右边缘有一个圆点（距底部为 `AnchorFromBottom`），一条 24 px 的引线，以及名称和备注。图层堆叠打开时，标签从下往上依次画出。
4. **说明**：当前停靠点的标题和正文。其区域与最高的说明一样高，因此页面不会跳动。
5. **停靠按钮**：每个停靠点一个 36 × 44 的按钮，带一个刻度；当前刻度更粗，并采用强调色。
6. **边框**（`IsFramed`）：1 px 边框、表面圆角和弱化的填充色。在容器模式下，它就是滚动区域。

### 滚动模式

`ScrollMode` 决定进度从何而来：

- `Container`（默认）：控件在自己的边框内滚动。边框内有一个空的轨道，高度为 `Length` 个边框（默认 4 个），舞台在轨道经过时保持固定。触摸和滚动键由控件原生滚动，滚轮每格移动 100 px，与浏览器一致；滚动到任一端时，边框会继续接收滚轮，而不是滚动页面。边框的高度取自 `Height`，或取窗口高度的 88%，范围在 620 至 860 px 之间。
- `Page`：控件是页面自己的 `ScrollViewer` 中一条高度为 `Length` 个视口的轨道，舞台在视口中保持固定，直到轨道经过。此模式下请关闭边框。
- `External`：没有任何滚动。由你自己设置 `Progress`，可以来自滑块、时间轴或你自己的滚动位置。

<Demo name="PageScroll" />

### 驱动进度

在外部模式下，`Progress` 是唯一的输入；绑定或设置它，舞台就会沿着它的弹簧跟随。`GoToStop(n)` 在任何模式下都能滑动到某个停靠点，`Reset()` 则回到整个屏幕。`GetStopName(n)` 和 `GetStopCaption(n)` 返回某个停靠点的文字，例如用于在页面其他位置显示。

<Demo name="SliderProgress" />

```csharp
view.StopChanged += (_, e) =>
{
    // e.Stop: 0 whole screen, 1..N a layer, N + 1 put back together. e.Layer is null at the ends.
    analytics.Track("exploded-stop", e.Stop);
};
```

### 自定义图层

图层就是普通控件，因此任何图片都可以：一张卡片、一个仪表板、一台设备。将 `LayerSize` 改为它们的设计尺寸，将 `LayerCornerRadius` 改为它们的圆角。无论舞台是什么尺寸，这种适配都能让爆炸后的堆叠及其标签保持在舞台之内。

<Demo name="CustomLayers" />

### 窄容器

布局跟随控件自身的宽度，而不是窗口的宽度。宽度从 880 px 起，引言和说明位于一个 380 px 的列中，与全高的舞台并排。宽度在 560 至 879 px 之间时，引言位于顶部，说明位于停靠按钮旁边。宽度低于 560 px 时，停靠按钮共用说明上方的一行，标签只显示名称；当舞台高于宽时，堆叠的转动幅度更小、展开得更开（`TallMaxTilt`、`TallMaxTurn`、`TallLayerGap`），以便标签仍能放下。

<Demo name="Narrow" />

### 减少动效

开启减少动效时，堆叠不会逐渐倾斜和分开：两端显示平面的屏幕，每个图层停靠点都显示完全分开的堆叠。停靠点立即随滚动位置切换，说明和颜色切换时不淡入，停靠按钮则直接跳转而不滑动。

<Demo name="ReducedMotion" />

### 调整场景

`MaxTilt`（60°）、`MaxTurn`（−40°）和 `LayerGap`（136 px）设定完全爆炸后的姿态；`Perspective`（2400）设定透视的强度；`DimmedLayerOpacity`（0.5）设定有一层获得焦点时其他图层淡化的程度。进度时间线是固定的：堆叠在 0.04 至 0.22 之间倾斜，在 0.08 至 0.28 之间分开，各图层停靠点在 0.28 至 0.80 之间均匀分布，收拢与展开的过程相反。

## API 参考

<!-- api: ExplodedView -->

<!-- api: ExplodedLayer -->

<!-- api: ExplodedCaption -->

## 无障碍

- 在容器模式下，边框是一个可获得焦点的滚动区域，名称取自 `Title`，并使用 `RegionLabelFormat`（“… Scroll to take it apart, one layer at a time.”）。键盘焦点环在边框外侧显示 2 px；指针聚焦时不显示。
- 舞台及其标签对辅助技术隐藏。一个名为 `LayerListLabel` 的隐藏列表会从下往上，把每个图层以“名称: 正文”的形式读出。
- 停靠按钮是一组，名称为 `StopsLabel`。每个按钮以其停靠点命名（`AssembledText`、图层名称或 `ReassembledText`），当前按钮把 `CurrentStopText` 作为其状态报告。当前刻度既更粗也有颜色，因此不只依赖颜色来区分。
- 停靠点每次变化时，说明都会以礼貌的方式被朗读。

### 键盘

| 按键 | 位置 | 操作 |
|---|---|---|
| `ArrowUp` / `ArrowDown` | 边框 | 滚动 40 px |
| `PageUp` / `PageDown`、`Shift+Space` / `Space` | 边框 | 滚动边框高度减去 40 px |
| `Home` / `End` | 边框 | 滚动到开头或末尾 |
| `ArrowLeft` / `ArrowRight` | 边框 | 无 |
| `Tab` | 边框、停靠按钮 | 从边框依次移动到每个停靠按钮 |
| `Enter` 或 `Space` | 停靠按钮 | 滚动到该停靠点；焦点保留在按钮上 |

## 动效

- 舞台以过阻尼弹簧（刚度 330，阻尼 60）跟随进度：它在滚动后的一帧之内开始移动，约 130 ms 时到达一半，约 750 ms 时稳定下来，没有过冲。中途反向时速度保持不变，因此来回滚动依然流畅。
- 展开时，堆叠先向后倾斜并转动，然后各图层分开，标签从下往上画出；收拢时，图层先合拢，标签从上往下消失，然后屏幕重新立起。
- 停靠点变化时，旧说明在 180 ms 内向上离开，新说明在 480 ms 内上升 10 px 就位。各图层的变暗在 480 ms 内缓动；轮廓、标签颜色和刻度的颜色在 240 ms 内变化；新刻度以有弹性的 580 ms 曲线变粗。图层边框和虚线轮廓则立即切换。
- 停靠按钮会把滚动位置滑动到该停靠点，距离越远时间越长（最长 700 ms）。
- 静止时没有任何动画。

| 变化 | 完整 | 减少 | 关闭 |
|---|---|---|---|
| 滚动 | 舞台沿弹簧跟随 | 两端为平面，每个图层处完全分开 | 与减少相同 |
| 停靠点变化 | 说明淡入淡出，变暗和颜色缓动 | 全部立即切换 | 立即 |
| 停靠按钮 | 滑动到该停靠点 | 直接跳转 | 直接跳转 |

## 相关组件

- `Stepper`，用于任务步骤的进度。
- `Carousel`，用于通过按钮和滑动浏览幻灯片。
- `ScrollArea`，用于带 Arc 滚动条的普通滚动区域。
- `SegmentedControl` 和 `Avatar`，示例屏幕即由它们构成。

| 组件 | 适用场景 |
|---|---|
| `Stepper` | 用户需要逐步完成一项任务 |
| `Carousel` | 多张图片或幻灯片需要逐一浏览 |
| `Tabs` | 故事的每个部分都是各自的视图，通过名称选择 |
| `ScrollArea` | 内容只需要滚动 |
