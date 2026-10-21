---
title: 滑块
summary: 在轨道上选取一个值或一个范围，跟随指针移动。
source-hash: ff8a369cbea5
---

<Demo name="SingleValue" />

## 何时使用

- 近似值，位置比精确数字更重要，例如音量、透明度或价格区间。
- 带两个滑块的范围选择，带有标注刻度和格式化读数。
- 用户在轨道上观察时数值会变化的场景，例如播放进度或亮度。

## 何时不用

- 用户需要键入的精确数字：用 `NumberField`。
- 用户无法更改的值：用 `Progress`。
- 用户为了手感拖动的单一设置，且轨道会越过两端拉伸：用 `ElasticSlider`。

## 用法

基本用法是单个值。`Value` 是滑块在轨道上的位置，`Minimum` 和 `Maximum` 设置其范围。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:Slider Label="音量"
                Value="64"
                Width="360" />
</UserControl>
```

### 起始与结束内容

`StartContent` 和 `EndContent` 分别位于轨道之前和之后，可放置图标或静音按钮。

```xml
<arc:Slider Label="音量"
            Value="64"
            Width="360">
    <arc:Slider.StartContent>
        <TextBlock Text="-" />
    </arc:Slider.StartContent>
    <arc:Slider.EndContent>
        <TextBlock Text="+" />
    </arc:Slider.EndContent>
</arc:Slider>
```

### 范围

将 `IsRange` 设为 `True` 即可显示两个滑块。通过 `LowerValue` 和 `UpperValue` 读写两端的值，并用 `MinStepsBetweenThumbs` 保持两个滑块之间的间距。`ValueFormatter` 用于格式化读数、气泡和朗读的数值。

<Demo name="Range" />

```xml
<arc:Slider Label="价格"
            IsRange="True"
            Minimum="0"
            Maximum="1000"
            SmallChange="10"
            LowerValue="{Binding MinPrice, Mode=TwoWay}"
            UpperValue="{Binding MaxPrice, Mode=TwoWay}"
            MinStepsBetweenThumbs="5"
            LowerThumbName="最低价格"
            UpperThumbName="最高价格"
            ValueFormatter="{Binding PriceFormatter}" />
```

### 步长与刻度

`SmallChange` 是数值吸附的步长。`Marks` 会在轨道上添加刻度。带标签的刻度会在轨道下方显示标签，点击标签会将最近的滑块移动到该位置。

<Demo name="StepsAndMarks" />

### 事件

`ValueCommitted` 在每次松开时、每次按键导致数值变化时，以及每次点击刻度标签时触发一次。将耗时操作（例如网络请求）放在此事件中执行。`RangeChanged` 在范围被拖动或通过键盘修改时，每一步都会触发。

```csharp
price.ValueCommitted += (_, e) => SavePriceFilter(e.LowerValue, e.UpperValue);
price.RangeChanged += (_, e) => committed.Text = $"${e.LowerValue:N0} – ${e.UpperValue:N0}";
```

### 禁用状态

禁用的滑块会移出 Tab 顺序。

<Demo name="Disabled" />

## API 参考

<!-- api: Slider -->

<!-- api: SliderMark -->

## 无障碍

- `Label` 是滑块的名称。在范围模式下，每个滑块的名称来自 `LowerThumbName` 和 `UpperThumbName`，若未设置，则由 `Label` 加上“最小”和“最大”组成。
- 每个滑块都会以文本形式报告其数值，因此朗读的数值遵循 `ValueFormatter`。
- 方向键按 `SmallChange` 步进；按住 Shift 加方向键、PageUp 或 PageDown 可进行大步长调整；Home 和 End 分别移动到允许的最小值和最大值。
- 读数、气泡和刻度标签对辅助技术隐藏。点击刻度是指针操作的快捷方式，同样的数值也可通过键盘到达。
- 焦点环在按键后出现，而不是在指针按下后出现。

## 动效

- 滑块与指针一比一跟随。松开时，它会吸附到最近的步长，并保留拖动的惯性，因此快速轻扫可能多移动一步。
- 按下轨道时，最近的滑块会以弹簧动画移向指针。在极限值处按键时，滑块会朝按下的方向拉伸，然后弹回。
- 数值气泡会在活动滑块上方弹出，数值变化时数字逐位滚动，松开后保留 700 毫秒。被拖动的滑块会略微放大。
- 减少动效时，取消惯性、拉伸和数字滚动。滑块直接移动到目标值，气泡仅淡入淡出。关闭动效时直接跳到最终状态。

## 相关组件

- `ElasticSlider`：用于单一设置，可越过两端拉伸并卡入刻度档位。
- `NumberField`：用户需要键入精确数字时使用。
- `Progress`：用于显示但不由用户更改的数值。
