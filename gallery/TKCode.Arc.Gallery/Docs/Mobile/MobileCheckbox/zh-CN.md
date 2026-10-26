---
title: 移动端复选框
summary: 44 px 行里的 22 px 圆角复选框，有对勾、不确定状态的短横，没有悬停状态。
source-hash: b859cc501f34
---

<Demo name="Basic" />

## 何时使用

`Checkbox` 让用户在表单或列表里选择任意数量的选项，或者表示同意。

- **同时选多项**：筛选、兴趣、要包含的条目。
- **单项同意**：“记住此设备”、“我接受条款”。
- **全选**：用 `IsThreeState`，父行在只有部分子项被选中时显示短横。

实时示例在手机外框里运行，那是桌面上的模拟；真机上的触摸反馈这里没有验证。

## 何时不用

- **立即生效的设置**：用 `Switch`。
- **多个选项里只能选一个**：用 `Segmented` 或带对勾的行列表。
- **一个命令**：用 `Button`。

## 用法

`Checkbox` 派生自 Avalonia 的 `ToggleButton`，所以 `IsChecked`、`IsThreeState`、`Click` 和绑定都和那里一样。标签是内容，整行都是触摸目标。

```xml
<arcm:Checkbox xmlns="https://github.com/avaloniaui"
               xmlns:arcm="https://github.com/AIDotNet/TKCode.Arc.Mobile"
               Content="记住此设备"
               IsChecked="{Binding Remember}" />
```

设置 `IsThreeState`，并把 `IsChecked` 绑定到可空值，就有了不确定状态。手指按在行上时会变暗；没有悬停状态。

### Token

`CheckboxToken` 保存几何：`TouchTarget`（44）、`BoxSize`（22）、`BoxRadius`（6）、`BoxBorderWidth`（1.5）、`CheckExtent`（16）、`LabelMargin`、`PressedOpacity`（0.6）和 `DisabledOpacity`（0.5）。颜色来自 ArcDesign token。

## API 参考

<!-- api: MobileCheckbox -->

## 无障碍

这一行是一个切换按钮，名称来自它的标签，所以一定要给它内容；没有标签的复选框没有名称。这一行至少 44 px 高，复选框不是唯一的触摸目标。

## 动效

手指按下时，框用快速弹簧缩小到 0.92，松开时用同一个弹簧回弹（P1；整行和文字保持不动）。对勾从起点画到终点（P15，`StrokeDrawGlyph`，进入曲线 240ms），短横与对勾之间用快速弹簧形变（P16，`MorphGlyph`）。框的填充和边框按快速曲线交叉淡化。减少动效时，标记和颜色立即切换，框也不缩小。

## 相关组件

- `MobileSwitch`：立即生效的设置。
- `MobileListGroup`：行的 trailing 内容里可以放复选框。
- `MobileButton`：提交表单的常用方式。
