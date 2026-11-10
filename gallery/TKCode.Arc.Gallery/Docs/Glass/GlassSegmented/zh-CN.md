---
title: Glass segmented
summary: 一个磨砂玻璃上的分段控件：选中的透镜在分段之间滑动时会拉伸，也可以直接拖动。
source-hash: cc5d6140595f
---

<Demo name="Preview" />

## 何时使用

`GlassSegmented` 在一条浮在图片或彩色页头上的磨砂玻璃轨道上，切换两到五个相关的视图或模式。选中的分段位于一块更亮的玻璃透镜中。选择变化时透镜滑过去：前沿先走，后沿跟上，所以它在途中被拉长，落下时再收拢，像一滴液体。按住选中的分段，透镜会在手指下微微鼓起；拖动它，它就跟着走，松开时选中它所在的分段。

- **图表或地图上的时间范围**：Day、Week、Month、Year。
- **媒体页头上的视图切换**：照片与相册、地图与列表。
- **玻璃工具栏上的模式**：查看器里的浅色、深色和屏幕。

## 何时不用

- **普通页面上**：使用更轻的 `SegmentedControl`。
- **超过五个选项，或文字较长**：使用 `Tabs` 或 `Select`。
- **打开或关闭一件事**：使用 `Switch`。
- **应用的顶层导航**：使用 `GlassTabBar` 或 `Tabs`。

## 用法

直接写出 `GlassSegment` 分段（文字和可选的 `Icon`），或给控件设置 `ItemsSource`。总有一个分段被选中；读取或绑定 `SelectedIndex` 或 `SelectedItem`，并响应 `SelectionChanged`。

```xml
<Panel arc:GlassBackdrop.Source="{Binding #Scene}">
    <Border Name="Scene" Background="#2B3A55" /><!-- your picture or page -->
    <arc:GlassSegmented SelectedIndex="1" HorizontalAlignment="Center">
        <arc:GlassSegment Content="Day" />
        <arc:GlassSegment Content="Week" />
        <arc:GlassSegment Content="Month" />
    </arc:GlassSegmented>
</Panel>
```

`GlassSegmented` 派生自 Avalonia 的 `SelectingItemsControl`，位于 Special 包中：在 `UseArc()` 旁调用 `UseArcSpecial()` 注册。磨砂效果与所有玻璃表面相同：它如何用公开的 Avalonia API 实现，见 Glass card 页面。

### 图标与禁用的分段

`Icon` 接受任何 24×24 的描边图形，例如 `ArcIcons` 中的图标。禁用的分段不能被选中；方向键会跳过它，透镜也不会停在上面。

<Demo name="Icons" />

### 来自数据的选项

`ItemsSource` 中的项显示为它们的文字。

<Demo name="Bound" />

### 手势

| 输入 | 效果 |
|---|---|
| 点击或点按一个分段 | 选中它；透镜滑过去 |
| 按住选中的分段 | 透镜鼓起 |
| 从选中的分段开始拖动 | 透镜沿轨道跟随，在宽度不同的分段之间改变宽度 |
| 拖动后松开 | 选中离透镜最近的分段 |

### 内容建议

- 每个分段的文字只用一个短词，并让各分段长度相近。
- 要么每个分段都有图标，要么都没有。
- 把控件放在背景较平静的位置；轨道下面细节太多会让文字难以阅读。

## API 参考

<!-- api: GlassSegmented -->

<!-- api: GlassSegment -->

## 无障碍

- 控件是一个标签列表，每个分段是一个标签，以文字为名称并带有选中状态；屏幕阅读器可以直接选中一个分段。
- 只有选中的分段在 Tab 顺序中；方向键同时移动选择和焦点。
- 键盘焦点在获得焦点的分段周围显示 2px 的焦点环；指针操作不显示。

### 键盘

| 按键 | 操作 |
|---|---|
| `Tab` | 进入控件（选中的分段）或离开 |
| `Right` / `Down` | 下一个分段，循环（从右到左的布局中方向相反） |
| `Left` / `Up` | 上一个分段，循环 |
| `Home` / `End` | 第一个或最后一个分段 |

## 动效

- 透镜的前沿以约 0.28 秒、略有弹性的弹簧移动，后沿以约 0.46 秒、更平静的弹簧移动，所以透镜朝新分段拉长，再收回到它的宽度。
- 按住时，透镜以灵敏弹簧在每一边鼓起 2px；拖动时以约 0.18 秒的紧弹簧跟随指针。
- 文字颜色在 160ms 内变化。

| 变化 | 完整 | 减弱 | 关闭 |
|---|---|---|---|
| 选择变化 | 透镜滑动并拉伸 | 透镜在新分段淡出再淡入 | 透镜立即移动 |
| 按住和拖动 | 透镜鼓起并跟随 | 透镜跟随，不鼓起 | 透镜跟随，不鼓起 |
| 文字颜色 | 160ms | 120ms | 立即 |

## 响应式

- 控件与它的分段一样宽，不会滚动。保持最多五个短选项，或在窄布局中改用 `Select`。
- 调整大小时，透镜直接落到它的分段上，不播放动画。
- 触摸与鼠标相同：点按选中，按住并拖动选中的分段来移动透镜。

## 相关组件

- `SegmentedControl`：普通表面上的同类选择。
- `GlassTabBar`：玻璃上的应用导航。
- `GlassCard` 和 `GlassMenu`：其他玻璃表面。

| 组件 | 适用场景 |
|---|---|
| `GlassSegmented` | 在图片或彩色页头上切换少量视图或模式 |
| `SegmentedControl` | 普通页面上的同类选择 |
| `Tabs` | 视图较多，或视图名称较长 |
