---
title: 滚动区域
summary: 原生滚动容器，配有细长的浮层滚动条，仅在内容溢出时才显示边缘渐隐。
source-hash: 3a151cda56de
---

## 何时使用

- 面板、侧边栏或弹层中可能溢出的内容，同时需要保持页面布局固定。
- 通过滚轮横向滚动、并吸附到各项的卡片或标签条。
- 需要判断某一边缘之外是否还有内容的区域，例如用于加载更多项目。

## 何时不用

- 逐项翻页浏览：用 `Carousel`。
- 包裹整个页面：应由页面或 `ScrollViewer` 负责滚动。
- 网格中的大量数据：用支持行虚拟化的表格控件。

## 用法

用 `Orientation` 设置滚动轴向，默认为 `Vertical`。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:ScrollArea Width="360"
                    Height="260"
                    Label="版本说明"
                    CornerRadius="18">
        <StackPanel Spacing="0">
            <TextBlock Text="v2.4 · 浮层滚动条" Margin="16,12" />
            <TextBlock Text="v2.3 · 更快的冷启动" Margin="16,12" />
            <TextBlock Text="v2.2 · 键盘快捷键" Margin="16,12" />
        </StackPanel>
    </arc:ScrollArea>
</UserControl>
```

`Label` 为滚动区域命名。有名称的区域会被播报为一个区域。

### 横向条带

垂直滚轮在横向区域能够继续滚动时会使其横向滚动，滚到尽头后再把滚轮交还给页面。设置 `HorizontalSnapPointsType` 可让各项吸附对齐。

```xml
<arc:ScrollArea Orientation="Horizontal"
                HorizontalSnapPointsType="Mandatory"
                Label="卡片">
    <StackPanel Orientation="Horizontal" Spacing="12" Margin="16">
        <Border Width="160" Height="100" CornerRadius="18" Background="#F4F4F5" />
        <Border Width="160" Height="100" CornerRadius="18" Background="#F4F4F5" />
        <Border Width="160" Height="100" CornerRadius="18" Background="#F4F4F5" />
    </StackPanel>
</arc:ScrollArea>
```

### 双向滚动与滚动条可见性

网格需要双向滚动时使用 `Both`。`OverlayScrollbarMode="Always"` 会在内容溢出时始终显示滚动条。默认的 `Auto` 仅在滚动或悬停时显示。

```xml
<arc:ScrollArea Orientation="Both"
                OverlayScrollbarMode="Always"
                Height="240" />
```

### 边缘渐隐与隐藏延迟

`FadeLength` 以像素为单位设置每个边缘渐隐的长度（默认 28）。设为 `0` 可关闭渐隐。`HideDelay` 设置滚动停止后滚动条保持可见的时长（默认 900 ms）。

```xml
<arc:ScrollArea FadeLength="0"
                HideDelay="0:0:1.5" />
```

### 响应边缘变化

只有当某一边缘在“有内容”与“无内容”之间切换时才会触发 `EdgesChanged`。`Edges` 属性保存当前状态。

```csharp
area.EdgesChanged += (_, e) =>
{
    if (!e.Edges.Bottom)
    {
        LoadMoreItems();
    }
};
```

### 通过代码滚动

`ScrollBy` 与 `ScrollToOffset` 用于滚动区域。在完整动效下它们以弹簧动画滚动；在减弱或关闭动效时则直接跳转到目标位置。

```csharp
area.ScrollBy(new Vector(0, 240));
area.ScrollToOffset(new Vector(0, 0));
```

## API 参考

<!-- api: ScrollArea -->

## 无障碍

- `Label` 设置区域的无障碍名称。没有标签时，周围的内容必须说明该区域的用途。
- 该区域可获得焦点。方向键、Page Up 与 Page Down、Home、End 以及空格键均可原生地滚动它。
- 滚动仍保持 `ScrollViewer` 的自动化模式，浮层滚动条仅用于视觉呈现。
- 减弱动效时，轨道翻页直接跳转，不做动画。

## 动效

- 内容滚动或指针悬停在区域上时，滚动条淡入；滚动停止 900 ms 后淡出。
- 指针悬停时滑块会变粗，并且可以拖动。
- 每个边缘渐隐随滚动的最初几个像素逐步增强，且长度永远不超过被隐藏的内容。
- 减弱动效时取消滚动条的淡入淡出，轨道翻页也变为瞬时完成。

## 相关组件

- `Carousel` 逐项翻页浏览。
- `ExpandableText` 将一段文字截断为几行，而不是滚动显示。
- `ResizablePanels` 在窗格之间分配空间；当某个窗格可能溢出时，在该窗格内放置 `ScrollArea`。
