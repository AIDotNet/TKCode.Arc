---
title: 刻度尺滑块
summary: 一条在固定指针下滑动的刻度带，具有惯性、吸附和滚动读数。
source-hash: a5bafeaf46e9
---

<Demo name="Height" />

## 何时使用

`RulerSlider` 在一条从固定指针下移动的刻度上设置一个数字。刻度可以比控件长得多，因此人们能在很宽的范围内精细控制，大号读数则在刻度带移动时显示当前值。

- **引导流程或健康应用中的身体数据**：身高、体重或目标，范围很宽，而在手机上表单字段显得笨重。
- **预订或结账中的剂量或金额**：以整数步进的每月预算或贷款金额。
- **带真实单位的设置**：焦距、距离或温度，刻度本身就显示了量程。

## 何时不用

- **轨道上的位置才是重点的设置**，例如音量：使用 `Slider` 或 `ElasticSlider`，整个范围都在视野内。
- **有两端的范围**：使用 `RangeHistogramSlider`，或带 `IsRange` 的 `Slider`。
- **使用者已经知道并想直接输入的数字**：使用 `NumberField`。
- **紧凑的行和密集的表单**：此控件需要整行宽度，连同标签和读数约 150 px 高。这些场合请使用 `Slider` 或 `ScrubInput`。

## 用法

设置 `Label`、上下限和 `Value`。`Unit` 跟在读数后面，并与值一起朗读。`RulerSlider` 派生自 Avalonia 的 `RangeBase`，因此 `Value`、`Minimum`、`Maximum`、`SmallChange`、`LargeChange` 和 `ValueChanged` 的行为与任何范围控件相同。`ValueChanged` 在每个刻度经过指针时触发；`ValueCommitted` 在拖动或轻扫后刻度带静止时触发一次，点按或按键时立即触发。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:RulerSlider Label="Height"
                     Unit="cm"
                     Minimum="120"
                     Maximum="220"
                     Value="{Binding Height}" />
</UserControl>
```

### 步长、刻度与间距

`SmallChange` 是一个小刻度代表的值，刻度带总是停在刻度上。`MajorEvery` 设置带标签的刻度之间有多少个小刻度，偶数时中间会多一个较高的刻度。`TickSpacing` 设置小刻度之间的像素数：间距越宽，每个刻度的行程越长，控制也就越精细。

<Demo name="FineSteps" />

### 格式化

`ValueFormatter` 格式化读数、刻度标签和朗读的值。文本中的数字仍会滚动，货币符号等周围的文字保持不动。没有格式化函数时，刻度标签只显示其自身间距所需的小数位，因此每 5 一个标签、步长半公斤的刻度显示为 30、35、40。`LargeChange` 设置 `PageUp`、`PageDown` 和 `Shift` 加方向键的距离，默认为一个带标签的刻度。

<Demo name="Formatted" />

### 禁用

`IsEnabled="False"` 以一半的不透明度显示值，并把控件移出 Tab 顺序。

<Demo name="Disabled" />

## API 参考

<!-- api: RulerSlider -->

## 无障碍

- 控件是一个制表位，并把自己报告为滑杆，以 `Label` 作为名称，上下限作为范围。
- 朗读的值是格式化后的值加上 `Unit`，例如“172 cm”。
- 刻度带、标签和指针都绘制在控件内，不是单独的元素。
- 键盘聚焦时，焦点环出现在刻度带周围。

### 键盘

| 按键 | 操作 |
|---|---|
| `Right` / `Up` | 把值提高一个刻度 |
| `Left` / `Down` | 把值降低一个刻度 |
| `Shift` + 方向键、`PageUp` / `PageDown` | 移动 `LargeChange` |
| `Home` / `End` | 转到 `Minimum` 或 `Maximum` |

## 动效

- 刻度带 1:1 跟随拖动。超过任一端时最多多走 44 px，释放后以弹簧（0.5 s，回弹 0.1）带着抛出的速度返回。
- 轻扫会滑行：行程为其速度走 0.42 s 的距离，吸附到一个刻度，并以 260 ms 的衰减缓入。每个刻度经过指针时，值和读数随之变化。按住刻度带会让它停在当前位置。
- 点按、按键或新的值让刻度带以 snappy 弹簧滑动，并保留已有的速度。
- 读数的每一位数字各自在 240 ms 内滑动，出现或离开的数位以 morph 弹簧展开或收起。
- 减弱动效时，刻度带在两端停住并直接跳到目标值，数字立即变化。关闭动效时一切立即完成。

## 响应式

控件填满父级给出的宽度，没有断点：视野显示更多或更少的刻度，指针保持在正中。高度固定，刻度带为 72 px。刻度向两侧边缘淡出，因此狭窄的视野仍像一条延续的刻度带。触摸时，水平拖动移动刻度带，垂直拖动留给页面，因此控件可以放在滚动页面中。它不依赖悬停。

## 相关组件

| 组件 | 适用场景 |
|---|---|
| `Slider` | 在整个范围上的位置很重要，或行很紧凑 |
| `RangeHistogramSlider` | 值是数据上的一个范围 |
| `NumberField` | 输入或步进精确数字 |
| `ScrubInput` | 通过在标签上拖动来改变数字 |
