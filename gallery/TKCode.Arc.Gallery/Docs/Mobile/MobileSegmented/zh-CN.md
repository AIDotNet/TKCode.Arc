---
title: 移动端分段控件
summary: 一排共享宽度的分段，选中其中一个，用于在同一内容的几个视图之间切换。
source-hash: e4bee4b6bafb
---

<Demo name="Basic" />

## 何时使用

`Segmented` 在同一事物的几个视图之间切换：时间范围、排序方式、同一批地点的列表和地图。

- **两到四个选项**，标签很短，同时可见。
- **同一内容的不同视图**：下面的内容变化，屏幕不变。

实时示例在手机外框里运行，那是桌面上的模拟；真机上的触摸反馈这里没有验证。

## 何时不用

- **应用的顶层区域**：用 `Tabbar`。
- **超过四个选项**或标签很长：用列表或面板。
- **同时选多项**：用复选框。

## 用法

把 `SegmentedItem` 放进控件，并设置 `SelectedIndex`。`Segmented` 派生自 Avalonia 的 `ListBox`，所以 `SelectedItem`、`SelectionChanged` 和绑定到 `ItemsSource` 都和那里一样。

```xml
<arcm:Segmented xmlns="https://github.com/avaloniaui"
                xmlns:arcm="https://github.com/AIDotNet/TKCode.Arc.Mobile"
                SelectedIndex="1">
    <arcm:SegmentedItem Content="日" />
    <arcm:SegmentedItem Content="周" />
    <arcm:SegmentedItem Content="月" />
</arcm:Segmented>
```

各分段平分宽度。选中的一个浮在带描边的 Surface 填充上，用前景色；其他的用次要文字色。手指按在未选中的分段上时会变暗；没有悬停状态。

### Token

`SegmentedToken` 保存轨道的几何：`TrackPadding`（2）和 `TrackRadius`（10）。`SegmentedItemToken` 保存分段的几何：`ItemHeight`（36）、`ItemPadding`、`ItemRadius`（8）和 `PressedOpacity`（0.6）。加上内边距，控件高 40。颜色和字体来自 ArcDesign token。

## API 参考

<!-- api: MobileSegmented -->

<!-- api: MobileSegmentedItem -->

## 无障碍

分段是可选择的列表项，名称来自标签。标签要简短且互不相同。分段高 36 px，宽度共享，所以四个选项时窄屏手机上每个分段仍有可用的触摸目标。

## 动效

填充、描边和文字颜色按快速曲线变化；减弱动效时立即变化。

## 相关组件

- `MobileTabbar`：用于顶层区域。
- `MobileNavbar`：分段控件常放在它下面。
- `MobilePage`：它所在的屏幕。
