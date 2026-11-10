---
title: 范围直方图滑块
summary: 叠在数据实时直方图上的价格范围，范围内的柱子会点亮，滑块会停靠在柱子边缘。
source-hash: 5b9fb270402a
---

<Demo name="PriceFilter" />

## 何时使用

`RangeHistogramSlider` 用来选取下限和上限，同时显示数据在两者之间的分布。使用者在选择之前就能看到数值集中在哪里，因此很少会筛选出空结果。

- **目录或预订搜索中的价格筛选**：柱子显示大多数房间约 $140，另一组集中在 $380 附近。
- **规模或时长筛选**：公司员工数、文件大小、会话时长，或其他带长尾的数据。
- **分析视图中的范围**：响应时间或订单金额，数据的形状会告诉人们哪一段值得查看。

## 何时不用

- **背后没有数据的范围**，例如营业时间段：使用带 `IsRange` 的 `Slider`。
- **单个值**：使用 `Slider`；需要精细控制时使用 `RulerSlider`。
- **必须输入的精确数字**：使用 `NumberField`。
- **只供阅读、不用来筛选的图表**：使用 Charts 分组中的图表。

## 用法

设置 `Label`，并把原始数值传给 `Data`。控件把它们统计到 `Bins` 根柱子中。`Data` 可在代码中或通过绑定接收列表；数据变化时请赋一个新列表，因为列表内部的修改不会被监听。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:RangeHistogramSlider Label="Price per night"
                              Data="{Binding Prices}"
                              LowerValue="{Binding From}"
                              UpperValue="{Binding To}"
                              Step="5" />
</UserControl>
```

`LowerValue` 和 `UpperValue` 是双向的，可以为 null，表示刻度的端点。`RangeChanged` 在每次变化时触发；`ValueCommitted` 在拖动释放或按键改变范围时触发一次，这正是发起搜索的时机。

刻度来自数据：`Minimum` 是向下取整的最小值，且不大于 0；`Maximum` 是向上取整的最大值。如果数据变化时刻度应保持不变，请显式设置它们。

### 柱子边缘与滑块间距

`SnapToBins` 开启时（默认），释放后落在柱子边缘 9 px 以内的滑块会停靠到该边缘，因此范围不会把一根柱子切成两半。如果 `Step` 网格上的任何值都同样合适，请关闭它。`MinStepsBetweenThumbs` 让两个滑块保持距离，以步长计。默认为 1，因此两个滑块不会重合。

<Demo name="FreeThumbs" />

### 禁用

`IsEnabled="False"` 让柱子和范围以一半的不透明度保持可见，并把滑块移出 Tab 顺序。

<Demo name="Disabled" />

## API 参考

<!-- api: RangeHistogramSlider -->

## 无障碍

- 每个滑块都是一个独立的滑杆，有自己的制表位。它们的名称由 `Label` 生成，如“Price per night, minimum”和“Price per night, maximum”，除非设置了 `LowerThumbName` 和 `UpperThumbName`。
- 每个滑块报告自己能到达的范围（止于另一个滑块），并通过 `ValueFormatter` 朗读其值。
- 图表对辅助技术而言只是装饰，它能提供的数字都在滑块和读数中。
- 焦点环在键盘聚焦时和按键之后出现。

### 键盘

| 按键 | 操作 |
|---|---|
| `Right` / `Up` | 把当前滑块提高一个 `Step` |
| `Left` / `Down` | 把当前滑块降低一个 `Step` |
| `Shift` + 方向键、`PageUp` / `PageDown` | 移到该方向上的下一个柱子边缘 |
| `Home` / `End` | 转到此滑块能到达的最小值或最大值 |
| 在极限处按键 | 值不变；滑块朝按键方向用力后弹回 |

## 动效

- 点亮的柱子和轨道填充共用一个裁剪，因此它们在同一帧切在滑块处，即使切在一根柱子中间。
- 被按住的滑块以 snappy 弹簧放大到 1.18 倍。一条细线从滑块升起穿过图表：在 160 ms 内淡入，并在 240 ms 内从 60% 长到全高。
- 超过极限时，滑块最多多走 10 px 后弹回。释放时，速度最多能再带它走一根柱子，然后 snappy 弹簧保持这一速度把它带到目标值。
- 在滑块以外的地方按下时，最近的滑块在数值上立即到位，屏幕上则以 snappy 弹簧移动过去。
- 减弱动效时没有拉伸、弹簧和惯性：滑块直接到达目标值，细线以全高直接出现，不淡入。关闭动效时一切立即完成。

## 响应式

控件填满父级给出的宽度，没有断点。柱子以 2 px 间距平分宽度，可能变得很窄，因此在狭窄的列中请减小 `Bins`。空间不足时先用省略号截断标签，而不是读数。父级高度不够时，图表让出高度，36 px 的控制行保持不变。触摸与鼠标的滑块目标一样是 44 px，在图表任意位置按下也有效；控件不依赖悬停。

## 相关组件

| 组件 | 适用场景 |
|---|---|
| `Slider` | 范围没有数据可显示，或只有一个值 |
| `RulerSlider` | 单个值需要在刻度上精细控制 |
| `ElasticSlider` | 单个值凭手感设置 |
| `NumberField` | 必须输入精确数字 |
