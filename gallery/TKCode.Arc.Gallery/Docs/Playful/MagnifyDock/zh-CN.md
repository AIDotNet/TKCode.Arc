---
title: Magnify dock
summary: 一个应用程序坞：图标在指针下像透镜一样放大，把邻居推开，显示名称，启动时还会弹跳。
source-hash: 53aa2e91e84b
---

<Demo name="Preview" />

## 何时使用

`MagnifyDock` 是搁板上的一排应用图标。指针沿着它移动时，指针附近的图标会像放大镜一样变大，正下方的最大，整排会变宽来腾出空间，所以不会有东西重叠。指针下的图标会在上方显示它的名称。点击图标即启动：它会弹跳几下，一次比一次小，下方的一个圆点表示它正在运行。

- **桌面风格的应用启动器**：用在网页桌面、展台或演示环境里。
- **作品集与产品页面**：把工具或项目列成图标。
- **有趣的工具栏**：让少数几个常用操作有触感。

## 何时不用

- **密集的工具栏和编辑器**：在指针下移动的图标更难点中。请用普通工具栏。
- **应用布局里的停靠面板**：那是 `DockLayout`。
- **很长的列表**：图标保持在十来个；更多的话手机放不下。

## 用法

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:MagnifyDock Label="Apps">
        <arc:MagnifyDockItem Label="Files" IsRunning="True">
            <arc:ArcGlyph Data="{x:Static arc:ArcIcons.Folder}" Size="24" />
        </arc:MagnifyDockItem>
        <arc:MagnifyDockItem Label="Mail">
            <arc:ArcGlyph Data="{x:Static arc:ArcIcons.Mail}" Size="24" />
        </arc:MagnifyDockItem>
    </arc:MagnifyDock>
</UserControl>
```

`MagnifyDock` 是 Special 包里的 `ItemsControl`。在 `UseArcControls()` 旁边用 `UseArcSpecial()` 注册这个包。每一项是一个 `MagnifyDockItem`，它是一个以图标为内容的按钮：可以直接写在里面，或绑定 `ItemsSource`，用 `ItemTemplate` 提供图标，并通过 `ItemContainerTheme` 设置每一项的 `Label` 和 `IsRunning`。给某一项设置自己的 `Background` 可以给它的底块上色；默认底块使用柔和的表面色。程序坞会自己居中，并预留放大后最宽的一排和文字所需的空间，所以它放大时页面不会移动。

### 尺寸与透镜

`ItemSize` 是静止时图标的边长（48 px），`Gap` 是图标之间的间距（8 px）。`Magnification` 是指针正下方图标的缩放倍数（1.8；设为 1 就关闭透镜），`Reach` 是离指针多远的图标仍会变大（150 px）。`ShelfPadding` 是搁板边缘与图标之间的空隙（8 px）。

<Demo name="Launch" />

### 启动

点击、轻触图标，或在图标上按 Enter 或 Space 即启动：它会弹跳，触发 `ItemLaunched`，带上它的序号、容器和数据项；`MarksLaunched` 开启时（默认），它的 `IsRunning` 会变为开启。`Launch(index)` 在代码中做同样的事。每一项都是按钮，所以 `Command` 和 `Click` 也可以用在它上面。应用关闭时请把 `IsRunning` 设为 false。

### 最佳实践

**建议**

- 给每一项一个简短的 `Label`；它既是图标上方的名称，也是它的无障碍名称。
- 在有色底块上使用简洁、高对比的图标，这样它们在 48 px 时易读，放大到 1.8 倍时依然清晰。

**避免**

- 把程序坞放在可能被盖住的位置：放大后的图标和文字会升到搁板上方约 80 px。
- 用它来做人们会快速重复的操作，比如设置文字格式。

### 常见问题

**程序坞放大时会让页面移动吗？** 不会。它在布局时按完整高度预留最宽的一排，放大只是对图标做变换。

**可以在分组之间放分隔线吗？** 暂时不行。请并排用两个程序坞，或加大 `Gap`。

## API 参考

<!-- api: MagnifyDock -->

<!-- api: MagnifyDockItem -->

<!-- api: MagnifyDockItemEventArgs -->

## 无障碍

- 程序坞是一个由 `Label`（默认为“Dock”）命名的工具栏。每个图标是一个由它的 `Label` 命名的按钮。
- 程序坞是一个 Tab 停靠点。方向键在图标之间移动（在两端循环），Home 和 End 跳到第一个和最后一个，获得焦点的图标会放大，并像指针在它上面一样显示它的名称。
- 键盘聚焦时，图标周围显示焦点环，并随图标一起放大；指针聚焦不显示。
- 运行中的圆点只是装饰；如果重要，请在你自己的界面里说明哪些应用是打开的。

### 键盘交互

| 按键 | 操作 |
|---|---|
| `Tab` | 把焦点移入程序坞，到上一次获得焦点的图标 |
| `Left` / `Right` | 移到上一个或下一个图标 |
| `Home` / `End` | 移到第一个或最后一个图标 |
| `Enter` / `Space` | 启动获得焦点的图标 |

## 动效

- 每个图标的大小围绕指针按一条平滑的钟形曲线变化，各自用自己的弹簧（偏硬、略欠阻尼），所以快速划过时会沿着这一排泛起涟漪，并在没有明显晃动的情况下稳定下来。
- 这一排围绕变大的图标按相同的间距重新排布并保持居中；搁板随之变宽。
- 名称出现在当前图标上方，在 120 ms 内淡入，并跟随图标变大和弹跳。
- 一次启动让图标弹跳三次，高度依次为 62%、30% 和 12%，每次 400 ms。
- 放大是每个图标的渲染变换，每帧写入一次；布局不会改变。所有图标都静止时停止帧，程序坞离开视野时暂停。

| 变化 | 完整 | 减弱 | 关闭 |
|---|---|---|---|
| 指针在这一排上 | 图标像透镜一样放大 | 图标保持大小 | 图标保持大小 |
| 名称 | 在 120 ms 内淡入 | 在 120 ms 内淡入 | 直接出现 |
| 启动 | 弹跳三次 | 只显示运行圆点 | 只显示运行圆点 |

## 响应式行为

- 程序坞的宽度等于图标完全放大时的宽度，并且自己居中；它不会换行。在手机上，请用更少的图标或更小的 `ItemSize`，约 40 px。
- 触屏没有悬停，所以在触屏上图标不会放大；轻触图标会像点击一样启动它。
- 名称在程序坞预留的高度内升到它上方，所以不会被程序坞自己的框裁掉。

## 相关组件

- `DockLayout` 排布应用的面板，那是另一种程序坞。
- `SegmentedControl` 在一排紧凑的按钮里切换几个视图。

| 组件 | 何时选择 |
|---|---|
| `MagnifyDock` | 几个应用或工具要让人觉得打开时有触感 |
| `DockLayout` | 应用需要可停靠、可调整大小的面板 |
| `SegmentedControl` | 人们在几个视图之间切换 |
