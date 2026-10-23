---
title: 滚动区域
summary: 原生滚动容器，配有细长的浮层滚动条，仅在内容溢出时才显示边缘渐隐。
source-hash: 066258a5ccd4
---

<Demo name="Vertical" />

## 何时使用

`ScrollArea` 为固定尺寸的区域提供它自己的滚动，使面板可以容纳超出其空间的内容，同时不改变周围页面的布局。它位于外壳、侧边面板或对话框中，外框已经确定，其中的内容随数据增长。

- **侧边面板与检查器**：客户记录的活动时间线或订单的明细行，与一个必须保持可见的列表并排。面板保持高度，时间线在其中滚动。
- **卡片或标签的横向条带**：账单页上的一排套餐卡片，或表格上方的一排筛选标签。`Horizontal` 配合强制吸附，能在用户使用滚轮或触控板滚动时让每张卡片保持对齐。
- **需要加载更多的信息流**：CRM 活动动态或投递日志，当底边报告其后已无内容时加载下一页。`EdgesChanged` 提供这一信号。
- **开发者工具中的载荷与日志**：固定高度面板中的 Webhook 请求体或部署日志，用户从顶部开始阅读，渐隐则表明后面还有更多文字。
- **面板或弹出框中的选项列表**：一个放不进浮层的长列表，例如地区或货币。

## 何时不用

- **逐项翻页**：用 `Carousel`，它在幻灯片之间切换并显示位置。
- **应当缩短的长文本**：用 `ExpandableText`，它截断到几行并提供“阅读更多”。
- **带列和排序的表格数据**：用 `SortableDataTable`，数据量大时用 `DataGrid`。把 `ScrollArea` 套在普通 `StackPanel` 外面会测量每一行，因此数千行的表格会很慢。
- **带键盘导航的嵌套树**：用 `TreeView`，它处理展开与层级之间的方向键移动。
- **从长列表中选择一个值**：用 `Select` 或 `Combobox`，它们自己管理列表、筛选与键盘模型。

## 用法

用 `Orientation` 设置轴向。默认为 `Vertical`。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:ScrollArea Width="360"
                    Height="260"
                    Label="发布说明"
                    CornerRadius="18">
        <StackPanel Spacing="0">
            <TextBlock Text="v2.4 · 浮层滚动条" Margin="16,12" />
            <TextBlock Text="v2.3 · 冷启动更快" Margin="16,12" />
            <TextBlock Text="v2.2 · 键盘快捷键" Margin="16,12" />
        </StackPanel>
    </arc:ScrollArea>
</UserControl>
```

`ScrollArea` 派生自 Avalonia 的 `ScrollViewer`，因此偏移量、范围与视口、吸附点属性、触摸惯性、`ScrollChanged` 以及滚动自动化模式的工作方式都与滚动视图相同。Arc 用浮层轨道替换原生滚动条，增加边缘渐隐，并改变默认的焦点与滚动条行为。区域会把内容裁剪到其边界内，因此内容绝不会绘制到外框之外。

`Label` 为滚动区域命名。具名区域会被播报为一个区域。没有标签时，区域周围的内容必须说明它容纳的是什么。

### 结构

1. **内容**（`Content`）：被滚动的子元素。它保持自然尺寸；区域的 `Width` 与 `Height` 决定可见窗口。
2. **边缘渐隐**：每条边 28 px 的渐变（`FadeLength`），让被截断处的内容变暗。渐隐只出现在内容继续延伸到该边之外的地方，其长度等于被隐藏的内容，但不超过最大值。
3. **垂直轨道**：一个 11 px 的命中区域（`TrackThickness` 令牌），包住一个 5 px 的圆条，距末端边缘 3 px。指针悬停或拖动时，圆条增粗到 7 px，并移动到距边缘 2 px 处。
4. **滑块颜色**：前景色、30% 不透明度，指针悬停在轨道上时升至 46%。滑块长度不短于 28 px，位置随偏移量变化。
5. **水平轨道**：沿底边的同款圆条，高 11 px。
6. **角落**：使用 `Both` 时，每条轨道在距角落 13 px 处停止，因此两条轨道永不交叉。
7. **焦点环**：2 px 的焦点色环，绘制在区域内部，因此裁剪不会将其切掉。仅在键盘焦点时显示。

没有溢出时，轨道与渐隐都不会出现。短区域没有渐隐、没有轨道，也没有 `:overflow` 伪类，因此列表增减内容时，提示会随之开启或关闭。

### 轴向与浮层滚动条

先选轴向，再选滚动条行为。

- `Vertical` 是列表、信息流、日志和侧边面板的默认值。横向滚动条隐藏。
- `Horizontal` 适用于卡片、标签或筛选项的条带。垂直滚轮会将其横向滚动（见下文）。
- `Both` 适用于需要双向滚动的网格和宽表格。谨慎使用，因为两条轨道和两处渐隐会与内容争夺注意力。

运行时修改 `Orientation` 会更新原生滚动条的可见性和轨道边距，因此轴向可以跟随视图模式变化。

`OverlayScrollbarMode` 决定滚动条何时显示。`Auto`（默认）在内容滚动或鼠标悬停于区域上时显示，并在滚动停止后保留 `HideDelay` 的时长。悬停只对鼠标输入显示滚动条，因此触摸屏只在用户滚动时才显示。`Always` 在内容溢出时始终保持可见。对于必须让用户看到还有更多内容的信息流、日志或载荷，使用 `Always`；对于装饰性或次要面板，使用 `Auto`。

<Demo name="BothAxes" />

### 横向条带

垂直滚轮会让横向区域左右滚动，直到无法再滚动，然后把滚轮交还给页面。设置 `HorizontalSnapPointsType` 可使各项吸附。

<Demo name="HorizontalStrip" />

滚轮转换由 `IsWheelToHorizontalEnabled` 控制（默认 `True`）。仅当滚轮移动主要为垂直方向、未按住 `Ctrl`、且条带在滚轮方向上仍有空间时才生效。每一格滚轮大约移动条带 50 px。对于必须停在边缘的卡片，设置 `HorizontalSnapPointsType="Mandatory"`；对于自由滚动，使用 `None`（默认）。

### 边缘渐隐与隐藏延迟

`FadeLength` 以像素为单位设置每条边缘渐隐的长度（默认 28）。将其设为 `0` 可关闭渐隐。`HideDelay` 设置滚动停止后滚动条保持可见的时长（默认 900 ms）。

```xml
<arc:ScrollArea FadeLength="0"
                HideDelay="0:0:1.5" />
```

### 由代码驱动

`ScrollArea` 没有可以绑定的值，因此由代码驱动。`Edges` 是四条边的当前状态，`EdgesChanged` 在每次变化时报告。`ScrollBy` 与 `ScrollToOffset` 滚动区域。在完整动效下它们以弹簧动画过渡；在减弱或关闭动效下则直接跳到目标位置。

```csharp
area.EdgesChanged += (_, e) =>
{
    if (!e.Edges.Bottom)
    {
        LoadMoreItems();
    }
};
```

`EdgesChanged` 仅在某条边在“有超出内容”与“没有超出内容”之间切换时触发，因此它是加载更多请求的恰当触发器。需要在其他时候获知状态时，读取 `Edges`。

```csharp
area.ScrollBy(new Vector(0, 240));
area.ScrollToOffset(new Vector(0, 0));
```

`ScrollBy` 与 `ScrollToOffset` 会把目标限制在内容范围内，因此过大的位移会停在末端。要让某一项进入视野，对该项调用 `BringIntoView()`，这是每个控件都有的 Avalonia 方法。

### 示例：客户记录旁的活动动态

CRM 中一条客户记录，旁边是固定高度的活动面板。标题写明账户与排序方式，动态在其外框内滚动。滚动条设为 `Always`，因为动态很长，用户必须看到更早的事件还在后面。底边的渐隐也以不用滚动条的方式传达同样的信息；顶边没有渐隐，因为动态从最新的事件开始。

![固定面板中的活动动态，滚动条始终可见，底边有渐隐](images/ActivityFeed.png)

```xml
<StackPanel Width="560" Spacing="12">
    <TextBlock Text="Account activity"
               FontSize="16"
               FontWeight="Medium"
               Foreground="{arc:ArcDesignTokenResource Foreground}" />
    <TextBlock Text="Northwind · latest first"
               FontSize="13"
               Foreground="{arc:ArcDesignTokenResource TextMuted}" />
    <arc:ScrollArea Height="260"
                    Label="Account activity"
                    OverlayScrollbarMode="Always"
                    CornerRadius="12"
                    Background="{arc:ArcDesignTokenResource SurfaceMuted}">
        <StackPanel>
            <Border Padding="16,12"
                    BorderThickness="0,0,0,1"
                    BorderBrush="{arc:ArcDesignTokenResource Border}">
                <StackPanel Spacing="4">
                    <TextBlock Text="Oct 20, 2026  ·  Billing"
                               FontSize="12"
                               Foreground="{arc:ArcDesignTokenResource TextMuted}" />
                    <TextBlock Text="Invoice INV-2026-0412 paid, 4,800 USD"
                               FontSize="14"
                               Foreground="{arc:ArcDesignTokenResource Foreground}" />
                </StackPanel>
            </Border>
            <Border Padding="16,12"
                    BorderThickness="0,0,0,1"
                    BorderBrush="{arc:ArcDesignTokenResource Border}">
                <StackPanel Spacing="4">
                    <TextBlock Text="Oct 18, 2026  ·  Seats"
                               FontSize="12"
                               Foreground="{arc:ArcDesignTokenResource TextMuted}" />
                    <TextBlock Text="Seat count raised from 40 to 48"
                               FontSize="14"
                               Foreground="{arc:ArcDesignTokenResource Foreground}" />
                </StackPanel>
            </Border>
            <Border Padding="16,12"
                    BorderThickness="0,0,0,1"
                    BorderBrush="{arc:ArcDesignTokenResource Border}">
                <StackPanel Spacing="4">
                    <TextBlock Text="Oct 16, 2026  ·  Integrations"
                               FontSize="12"
                               Foreground="{arc:ArcDesignTokenResource TextMuted}" />
                    <TextBlock Text="Webhook endpoint updated by Lena Ortiz"
                               FontSize="14"
                               Foreground="{arc:ArcDesignTokenResource Foreground}" />
                </StackPanel>
            </Border>
            <Border Padding="16,12"
                    BorderThickness="0,0,0,1"
                    BorderBrush="{arc:ArcDesignTokenResource Border}">
                <StackPanel Spacing="4">
                    <TextBlock Text="Oct 12, 2026  ·  Renewal"
                               FontSize="12"
                               Foreground="{arc:ArcDesignTokenResource TextMuted}" />
                    <TextBlock Text="Renewal quote for 12 months sent to finance"
                               FontSize="14"
                               Foreground="{arc:ArcDesignTokenResource Foreground}" />
                </StackPanel>
            </Border>
            <Border Padding="16,12">
                <StackPanel Spacing="4">
                    <TextBlock Text="Oct 09, 2026  ·  Support"
                               FontSize="12"
                               Foreground="{arc:ArcDesignTokenResource TextMuted}" />
                    <TextBlock Text="Ticket 5521 resolved after a token rotation"
                               FontSize="14"
                               Foreground="{arc:ArcDesignTokenResource Foreground}" />
                </StackPanel>
            </Border>
        </StackPanel>
    </arc:ScrollArea>
</StackPanel>
```

### 文案规范

- **标签**：一个名词短语，说明区域容纳的内容，采用句首大写：“账户活动”“发布说明”。屏幕阅读器会在内容之前播报它，因此“可滚动区域”什么也没有告诉用户。
- **标题与日期**：当标题必须在内容滚动时保持可见，把它放在区域之外。日期的写法与产品其他地方一致，数量用阿拉伯数字书写。
- **溢出提示**：不要添加“向下滚动查看更多”之类的文字。渐隐与滚动条已经传达了这一点，而文字还会占用内容的空间。

| 不要写 | 改为 |
|---|---|
| Label="可滚动区域" | Label="账户活动" |
| Label="列表" | Label="发布说明" |
| 下方“向下滚动查看更多” | （无文字：由渐隐与滚动条表达） |

### 最佳实践

**推荐**

- 当内容值得被屏幕阅读器找到时，为每个区域设置 `Label`，因为具名区域会被播报为一个区域。
- 从父元素为区域设定有界尺寸。滚动需要视口小于内容；随内容增长的视口没有可滚动的空间。
- 对于必须让用户看到还有更多内容的信息流、日志和载荷，使用 `OverlayScrollbarMode="Always"`。
- 对于加载更多的触发，使用 `EdgesChanged`，而不是监视偏移量，因为它只在每次变化时触发一次，而不是每一步滚动都触发。

**避免**

- 嵌套两个同方向滚动的区域。滚轮只有在内层区域到达边缘时才会离开它，因此外层区域会出现意外的跳动。只有轴向不同时才嵌套。
- 在以触摸为主的屏幕上依赖 `Auto` 滚动条。悬停只为鼠标显示滚动条，因此触摸用户只在滚动时才看到它们，而闲置的面板不会显示它可以滚动。
- 在信息流上设置 `FadeLength="0"`。没有渐隐，被截断的线条看起来像是内容的末尾，用户会停止滚动。
- 在区域内的普通 `StackPanel` 中放入数千行。区域会测量所有行，大列表会很慢；请使用表格或自行管理行的列表控件。
- 包裹整个页面。页面布局应当负责屏幕的滚动，使其页头与页脚保持不动；包住整个屏幕的区域会锁住滚动。

### 常见问题

**为什么键盘不能滚动我的区域？** 按键只有在区域本身获得焦点时才会传到区域。区域内获得焦点的 `TextBox` 或 `ListBox` 会先处理方向键。用 `Tab` 或点击区域背景把焦点移到区域上。

**为什么触摸屏上看不到滚动条？** 在 `Auto` 模式下，悬停只为鼠标输入显示滚动条。触摸在滚动时显示它们，并在 `HideDelay` 之后隐藏。当滚动条是必要的提示时，设置 `OverlayScrollbarMode="Always"`。

**为什么滚轮在横向条带的末端会离开？** 这是有意为之。条带在任一端都没有滚轮方向上的剩余空间，因此滚轮交给页面，由页面滚动。

**新行到达时，如何让日志视图保持在底部？** 在追加一行后，用较大的 `Y` 调用 `ScrollToOffset`，例如 `area.ScrollToOffset(new Vector(0, area.Extent.Height));`，它会被限制到末端，但只在用户已经位于底部时才这样做。否则滚动会把用户拉离他正在阅读的那一行。

## API 参考

<!-- api: ScrollArea -->

| 类型 | 成员 | 含义 |
|---|---|---|
| `ScrollAreaOrientation` | `Vertical`, `Horizontal`, `Both` | 区域滚动的轴向。 |
| `ScrollAreaScrollbarMode` | `Auto`, `Always` | 浮层滚动条何时显示。`Auto` 在滚动或悬停时显示。 |
| `ScrollAreaEdges` | `Top`, `Bottom`, `Left`, `Right` | 内容在该边之外继续延伸时为 `True`。 |
| `ScrollAreaEdgesChangedEventArgs` | `Edges` | 新的边缘状态。 |

## 无障碍

- `Label` 设置区域的无障碍名称，具名区域会被播报为一个区域。没有标签时，周围的内容必须说明其用途。
- 区域可获得焦点，并位于 Tab 顺序中。方向键、Page Up 与 Page Down、Home、End 以及 Space 原生地滚动它。
- 滚动保留 `ScrollViewer` 的自动化模式。浮层滚动条仅为视觉呈现，因此屏幕阅读器用户通过滚动浏览内容，而不需要操作滚动条。
- 在减弱动效下，轨道翻页直接跳转，不做动画。

### 键盘

按键只在区域本身获得焦点时生效。`Shift` 只改变 `Space` 的方向。

| 按键 | 操作 |
|---|---|
| `Tab` / `Shift+Tab` | 将焦点移入或移出区域 |
| `Up` / `Down` | 以 40 px 滚动垂直区域 |
| `Left` / `Right` | 以 40 px 滚动水平区域 |
| `Page Up` / `Page Down` | 沿主轴以视口的 90% 滚动 |
| `Space` / `Shift+Space` | 以视口的 90% 向前或向后滚动 |
| `Home` / `End` | 滚动到轴的起点或终点。在 `Both` 中，它们移动垂直轴 |

## 动效

- 内容滚动或指针悬停于区域上时，滚动条淡入。滚动停止后保留 `HideDelay`（900 ms），然后用 480 ms 淡出。
- 指针悬停时滑块变粗，并且可以拖动。拖动滑块时关闭吸附，直到拖动结束。
- 每条边缘渐隐随最初几像素的滚动而增长，且永远不会长于被隐藏的内容。
- 减弱动效会移除滚动条的淡入淡出，并使轨道翻页即时完成。

| 变化 | 完整 | 减弱 | 关闭 |
|---|---|---|---|
| 滚动条出现 | 以快速时长淡入 | 立即出现 | 立即出现 |
| 滚动条消失 | 在 `HideDelay` 之后，以 480 ms 淡出 | 立即隐藏 | 立即隐藏 |
| 指针悬停时的滑块 | 从 5 px 增粗到 7 px，并在快速时长内变深 | 立即变化 | 立即变化 |
| 按键、代码或轨道滚动 | 平滑弹簧过渡到目标 | 直接跳到目标 | 直接跳到目标 |
| 边缘渐隐 | 长度随用户滚动而跟随被隐藏的内容变化 | 同完整 | 同完整 |

## 相关组件

- `Carousel` 逐项翻页，并显示位置。
- `ExpandableText` 将一段文字截断为几行，而不是滚动它。
- `ResizablePanels` 在窗格之间分配空间；当某个窗格可能溢出时，在其中放入 `ScrollArea`。

| 组件 | 适用场景 |
|---|---|
| `Carousel` | 用户逐项切换内容，并需要位置指示器 |
| `ExpandableText` | 一段文字较长，应先显示几行并提供“阅读更多” |
| `ResizablePanels` | 用户需要决定每个窗格获得多少空间 |
| `SortableDataTable` / `DataGrid` | 内容是用户需要排序或浏览的行与列 |
| `TreeView` | 条目有层级，用户用键盘在层级之间移动 |
