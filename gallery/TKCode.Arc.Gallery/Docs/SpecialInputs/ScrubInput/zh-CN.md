---
title: 拖动数值
summary: Figma 风格的数字字段：拖动标签即可调整，也可以键入数值、单位与算术表达式。
source-hash: 5df7f3b92dba
---

<Demo name="Inspector" />

## 何时使用

- 带有位置、尺寸、旋转与不透明度等数值的属性检查器。
- 用户既需要键入精确值、也需要拖动调整的密集设置。
- 带单位且用户可以更改单位的数值，例如 `px` 或 `%`。

## 何时不用

- 带微调按钮的普通表单数字：用 `NumberField`。
- 从范围中选取、且位置比具体数字更重要的值：用 `Slider`。
- 货币金额：用 `MoneyInput`。
- 没有数值含义的单个文本值：用 `Input`。

## 用法

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:ScrubInput Label="X" Value="120" Unit="px" />
</UserControl>
```

标签即为拖动手柄。左右拖动标签即可更改数值，点击字段则可以键入。

### 限值与循环

`Minimum` 与 `Maximum` 限制数值范围。超过限值时，数值会以橡皮筋效果回弹。`IsWrapping` 使数值在限值之间循环；它要求两个限值都是有限值。

```xml
<arc:ScrubInput Label="宽" Value="320" Minimum="0" Unit="px" ResetValue="320" />
<arc:ScrubInput Label="不透明度" Value="80" Minimum="0" Maximum="100" Unit="%" ResetValue="100" />
```

```csharp
var rotation = new ScrubInput
{
    Label = "旋转",
    Value = 15,
    Minimum = 0,
    Maximum = 360,
    IsWrapping = true,
    IsTrackVisible = false,
    Unit = "°",
    ResetValue = 0
};
```

`ResetValue` 是双击标签后恢复的值。当两个限值都是有限值时，`IsTrackVisible` 默认显示轨道。

### 步长

`Step` 为每次方向键的变化量。`FineStep`（Alt）与 `CoarseStep`（Shift）默认分别为 `Step` 的十分之一与十倍。`PixelsPerStep` 设定拖动多少像素对应一个步长。

```xml
<arc:ScrubInput Label="间距" Value="1.5" Step="0.1" Minimum="0" Unit="em" />
```

### 多个单位

设置多个 `Units` 后，单位会变为一个按钮，点击即可在各单位之间切换。键入单位（例如 `50%`）会切换到该单位。无法识别的单位会被拒绝。

<Demo name="CyclingUnits" />

### 尺寸

`Size` 设定高度：`Small` 为 30px，`Medium`（默认）为 36px。

<Demo name="Sizes" />

### 绑定与事件

`Value` 为双向绑定。`ValueChanged` 会在每次变化时触发，`ValueCommitted` 则在字段中的一次修改稳定后触发一次。两者都会报告旧值、新值以及 `ChangeSource`。

<Demo name="Events" />

```xml
<arc:ScrubInput Label="不透明度" Value="{Binding Opacity, Mode=TwoWay}" Minimum="0" Maximum="100" Unit="%" />
```

```csharp
amount.ValueCommitted += (_, e) =>
{
    Console.WriteLine($"{e.OldValue} -> {e.NewValue} {e.Unit} ({e.ChangeSource})");
};
```

### 图标标签

标签可以用一个 14px 的图标代替。请为字段设置无障碍名称，双击标签即可重置数值。

<Demo name="GlyphLabels" />

### 状态

禁用的字段会变暗至 50% 并忽略输入。无法识别的输入（例如 `abc`）会被拒绝：边缘变红 900ms 并短暂抖动，旧值保持不变。

<Demo name="States" />

## API 参考

<!-- api: ScrubInput -->

## 无障碍

- 字段是一个微调按钮。其值文字会朗读数值及单位，百分比与角度也会以相应方式朗读。
- 标签是真正的标签：点击会聚焦字段并选中其文字。ArrowUp 与 ArrowDown 逐步调整，Enter 确认键入的内容，Escape 恢复上一个值或取消拖动。
- 无效输入会在短时间内将字段设为无效状态，并保留旧值。该提示会被播报。
- 单位按钮命名为 "单位：<unit>。更改单位。"

## 动效

- 超过限值时，字段会带阻力地拉伸，松开后弹回，数字随拉伸倾斜。
- 无效输入或在限值处的步进会使字段抖动一次。
- 单位更改时带着轻微模糊升起。拖动时轨道填充跟随指针，键入或步进时则滑动到新值。
- 减弱动效时，移除拉伸、抖动与滑动效果。单位原地淡入。关闭动效时直接跳到终态。

## 相关组件

- `NumberField`：带微调按钮与限值提示的表单数字。
- `Slider`：当范围中的位置比精确数值更重要时使用。
- `MoneyInput`：货币数值。
