---
title: 移动端单选
summary: 44 px 行里的 22 px 单选，强调色圆点以弹簧弹入，每组单选，支持方向键，没有悬停状态。
source-hash: 89f6a17574b0
---

<Demo name="Basic" />

## 何时使用

`Radio` 让用户在表单或设置页的一个短列表里恰好选一项。

- **几个选项里选一个**：配送速度、支付方式、排序方式。
- **每个选项都需要一段标签**：每一行有自己的文字，选项读起来像一个列表。
- **稍后再确认的选择**：单选显示当前选择，由按钮提交。

实时示例在手机外框里运行，那是桌面上的模拟；真机上的触摸反馈这里没有验证。

## 何时不用

- **同一内容的几个视图的短行**：用 `Segmented`。
- **任意数量的选项**：用 `Checkbox`。
- **立即生效的设置**：用 `Switch`。

## 用法

`Radio` 派生自 Avalonia 的 `RadioButton`，所以 `IsChecked`、`GroupName`、`Click` 和绑定都和那里一样。`GroupName` 相同的单选互斥；把同一组的单选放在同一个面板里。标签是内容，整行都是触摸目标。这个设计是带组名的 `RadioButton`，而不是带选中值的容器：单选可以放进任何行布局，选择通过每一项的 `IsChecked` 绑定。

```xml
<StackPanel xmlns="https://github.com/avaloniaui"
            xmlns:arcm="https://github.com/AIDotNet/TKCode.Arc.Mobile">
    <arcm:Radio GroupName="delivery" Content="Standard, free" IsChecked="True" />
    <arcm:Radio GroupName="delivery" Content="Express, 2 days" />
</StackPanel>
```

焦点在某个单选上时，方向键会把选择移到同组、同级的上一个或下一个可用单选，并循环。手指按在行上时会变暗；没有悬停状态。

### Token

`RadioToken` 保存几何：`TouchTarget`（44）、`CircleSize`（22）、`CircleBorderWidth`（1.5）、`DotSize`（10）、`LabelMargin`、`PressedOpacity`（0.6）和 `DisabledOpacity`（0.5）。颜色来自 ArcDesign token。

## API 参考

<!-- api: MobileRadio -->

## 无障碍

这一行是以标签命名的单选按钮，所以一定要给它内容；没有标签的单选没有名称。行高至少 44 px，圆圈不是唯一的目标。方向键在组内移动，选中状态像任何 `RadioButton` 一样暴露给辅助技术。

## 动效

圆点从小尺寸带淡入弹簧弹入，离开时同样；因为是弹簧，选择在中途反转时会保留速度。外圈颜色按快速曲线变化。减少动效时，圆点和外圈立即变化。

## 相关组件

- `MobileCheckbox`：任意数量的选择。
- `MobileSegmented`：几个视图的短行。
- `MobileListGroup`：可以在尾部内容里放单选的行。
