---
title: 轮播
summary: 通过拖动、轻扫或方向键浏览一排幻灯片。
source-hash: 89c16c259bca
---

## 何时使用

- 浏览一小组相似的条目，例如套房、优惠或照片。
- 提示还有更多条目：每侧的相邻项会暗示这一排的其余内容。
- 通过可选的自动播放轮换展示精选内容。

## 何时不用

- 需要逐项做出决定：用 `CardStack`。
- 比较两张图片：用 `ImageCompare`。
- 阅读者需要扫读或排序的长记录列表：用 `SortableDataTable`。

## 用法

每个子元素对应一张幻灯片。`Label` 为轮播命名，供辅助技术使用。

```xml
<arc:Carousel Label="港湾套房精选">
    <Border Height="200" Background="#2F4858" CornerRadius="12" />
    <Border Height="200" Background="#D9D2C5" CornerRadius="12" />
    <Border Height="200" Background="#8AA399" CornerRadius="12" />
</arc:Carousel>
```

### 绑定条目

当幻灯片来自数据时，将 `ItemsSource` 与 `ItemTemplate` 配合使用。

```xml
<arc:Carousel Label="港湾套房" ItemsSource="{Binding Suites}">
    <arc:Carousel.ItemTemplate>
        <DataTemplate>
            <Border Height="200" Background="#2F4858" CornerRadius="12">
                <TextBlock Text="{Binding Name}" Margin="18" />
            </Border>
        </DataTemplate>
    </arc:Carousel.ItemTemplate>
</arc:Carousel>
```

### 幻灯片尺寸与标签

幻灯片宽度为轮播宽度的 `SlideWidthRatio`（默认 0.8），上限为 `MaxSlideWidth`（340）。`SlideGap` 设置幻灯片之间的间距。`SlideLabelFormatter` 生成每张幻灯片播报时使用的文字。

```csharp
carousel.SlideLabelFormatter = (index, count) => $"档案照片 {index + 1}，共 {count}";
```

### 自动播放

设置 `Interval` 会添加播放控件。`IsAutoPlay` 开始轮换，控件可切换其状态。轮换向前进行，播放到最后一张后回到第一张。幻灯片显示期间，指示器会逐渐耗尽。

```xml
<arc:Carousel Label="季节性优惠" Interval="0:0:4" IsAutoPlay="True">
    <Border Height="200" Background="#2F4858" CornerRadius="12" />
    <Border Height="200" Background="#D9D2C5" CornerRadius="12" />
</arc:Carousel>
```

### 通过代码导航

```csharp
carousel.SelectionChanged += (_, _) => readout.Text = $"第 {carousel.SelectedIndex + 1} 张，共 {count} 张";

carousel.Next();
carousel.Previous();
carousel.GoTo(3);
```

## API 参考

<!-- api: Carousel -->

## 无障碍

- `Label` 为轮播命名。选择变化时，幻灯片会通过 `SlideLabelFormatter` 以“2 / 5”之类的形式播报。
- 左右方向键每次移动一张幻灯片。Home 与 End 分别跳到第一张与最后一张。
- Tab 依次切换幻灯片、播放控件、指示器以及上一张、下一张按钮。
- 非当前激活的幻灯片会从焦点顺序和自动化树中移除。
- 自动播放会在指针位于轮播上方、键盘焦点位于其中、正在拖动、滚出可视区域以及窗口最小化时暂停。自动播放不会播报每一张幻灯片。

## 动效

- 拖动时幻灯片跟随指针移动，并带着惯性吸附到最近的幻灯片。键盘或按钮操作会沿移动方向切换幻灯片。
- 相邻幻灯片会随与当前激活幻灯片的距离而缩小并变暗。
- 指示器会拉伸成一个跟随当前幻灯片的胶囊形状；自动播放等待期间，胶囊会逐渐耗尽。
- 减弱动效时直接跳到目标幻灯片，并停止自动播放。关闭动效时立即到达最终状态。

## 相关组件

- 每个条目都需要做出决定而不只是浏览时，用 `CardStack`。
- 同一张图片的前后对比，用 `ImageCompare`。
- 在不相似的面板之间切换时，用 `Tabs`。
