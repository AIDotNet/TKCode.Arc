---
title: 弹性滑块
summary: 滑块越过两端时如橡皮筋般拉伸，附带摇摆的数值标签，以及滑块会卡入的档位。
source-hash: 295f9c37751d
---

## 何时使用

- 用户凭手感调整的单一设置，例如音量、亮度或透明度。
- 轨道需要有物理感的媒体与创作工具。
- 在有实际意义的数值（例如 0、50 和 100）上设置档位，滑块会卡入这些位置。

## 何时不用

- 安静的表单或设置页面：用 `Slider`。
- 带两个滑块的范围选择：用 `Slider`，它支持 `IsRange`。
- 用户需要键入的精确数字：用 `NumberField`。

## 用法

基本用法是单个值。`Value` 是滑块在轨道上的位置，`Minimum` 和 `Maximum` 设置其范围。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:ElasticSlider Label="音量"
                       Value="40"
                       Width="360"
                       ValueFormatter="{Binding VolumeFormatter}" />
</UserControl>
```

### 图标

`StartIcon` 和 `EndIcon` 分别位于轨道之前和之后。当轨道被拉过某一端时，该端的图标会随之向外移动。

```xml
<arc:ElasticSlider Label="亮度"
                   Value="50"
                   Width="360">
    <arc:ElasticSlider.StartIcon>
        <TextBlock Text="-" />
    </arc:ElasticSlider.StartIcon>
    <arc:ElasticSlider.EndIcon>
        <TextBlock Text="+" />
    </arc:ElasticSlider.EndIcon>
</arc:ElasticSlider>
```

### 档位

将 `SliderMark` 值添加到 `Marks` 中。被拖动的滑块会卡入每一个刻度。带标签的刻度会在轨道下方显示标签，点击标签会将滑块移到该位置。

```csharp
var brightness = new ElasticSlider
{
    Label = "亮度",
    Value = 50,
    Width = 360
};
brightness.Marks.Add(new SliderMark(0, "关闭"));
brightness.Marks.Add(new SliderMark(50, "50"));
brightness.Marks.Add(new SliderMark(100, "最大"));
```

### 步长与格式

`SmallChange` 是数值吸附的步长。`ValueFormatter` 用于格式化数值标签、读数和朗读的数值。`IsValueVisible` 可隐藏标签旁的读数。拖动滑块或通过按键修改时，数值标签仍会显示。

```xml
<arc:ElasticSlider Label="缩放"
                   Minimum="0.5"
                   Maximum="3"
                   SmallChange="0.1"
                   Value="1"
                   Width="360"
                   ValueFormatter="{Binding ZoomFormatter}" />
```

### 事件

`ValueCommitted` 在每次松开时、每次按键使数值改变时，以及每次点击刻度标签时触发一次。将耗时操作（例如网络请求）放在此事件中执行。

```csharp
volume.ValueCommitted += (_, e) => SaveVolume(e.Value);
```

## API 参考

<!-- api: ElasticSlider -->

<!-- api: SliderMark -->

## 无障碍

- `Label` 是滑块的名称。朗读的数值遵循 `ValueFormatter`。
- 方向键按 `SmallChange` 步进；按住 Shift 加方向键、PageUp 或 PageDown 可进行大步长调整；Home 和 End 分别移动到最小值和最大值。
- 在极限值处按键时，轨道会回弹，数值不会改变。
- 数值标签和档位的弹出效果仅为视觉呈现。点击刻度标签是指针操作的快捷方式，同样的数值也可通过键盘到达。
- 焦点环在按键后出现，而不是在指针按下后出现。禁用的滑块会移出 Tab 顺序。

## 动效

- 越过任一端时，轨道如橡皮筋般拉伸，并依松开时的速度弹回。按住时，滑块会略微膨胀。
- 数值标签悬挂在滑块上，并随其速度摇摆，因此快速拖动时标签倾斜得更明显。
- 滑块卡入档位时，轨道上的刻度会弹出一次。
- 减少动效时，取消拉伸、摇摆和档位弹出。滑块直接移到目标值，标签仅淡入淡出。关闭动效时直接跳到最终状态。

## 相关组件

- `Slider`：用于安静的表单控件，或带两个滑块的范围选择。
- `NumberField`：用户需要键入精确数字时使用。
- `Progress`：用于显示但不由用户更改的数值。
