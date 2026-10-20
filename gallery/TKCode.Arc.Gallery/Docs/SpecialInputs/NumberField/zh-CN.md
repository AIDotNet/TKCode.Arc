---
title: 数字输入框
summary: 通过清晰的增减控件输入有范围限制的数字。
source-hash: ff28c2fe4eb2
---

## 何时使用

- 带范围的精确数字，例如座位数、数量或价格。
- 用户需要逐步增减的值，并且单位或前缀随数字一起显示。
- 开启 `IsScrubEnabled` 后，通过拖动标签来调整数值。

## 何时不用

- 近似值，且位置比精确数字更重要：用 `Slider`。
- 电话号码或代码等数字字符串：用 `Input`。
- 带货币代码的金额：用 `MoneyInput`。
- 需要拖动调整多个数值的画布式属性面板：用 `ScrubInput`。

## 用法

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:NumberField Label="座位数"
                     Minimum="1"
                     Maximum="10"
                     Value="3"
                     Description="每个团队最多 10 个。" />
</UserControl>
```

### 前缀、后缀与步长

`Step` 设置增减幅度，其精度决定默认的小数位数。`SuffixFormatter` 可让单位随数值变化，例如单数与复数形式。

```xml
<arc:NumberField Label="价格" Prefix="¥" Step="0.5" Maximum="999" Value="24.5" />
<arc:NumberField Label="温度" Minimum="-20" Maximum="40" Value="21" Suffix="°C" />
```

```csharp
var seats = new NumberField
{
    Label = "座位数",
    Minimum = 1,
    Maximum = 10,
    Value = 3,
    SuffixFormatter = n => n == 1 ? " 个座位" : " 个座位"
};
```

最小值小于零时，用户输入时即可接受负号。

### 尺寸

`Size` 设置宽度与微调按钮的尺寸：`Small` 宽 164px，`Medium`（默认）宽 196px，`Large` 宽 228px。

```xml
<arc:NumberField Label="小" Size="Small" Value="2" Maximum="99" />
<arc:NumberField Label="大" Size="Large" Value="48" Maximum="99" />
```

### 限值提示与错误

`IsLimitHintEnabled` 在达到限值时显示提示。`LimitHintFormatter` 负责生成提示文字。`ErrorMessage` 会将字段标记为无效。

```xml
<arc:NumberField Label="数量" Value="0" Maximum="5" ErrorMessage="请至少添加一项。" />
```

```csharp
var guests = new NumberField
{
    Label = "宾客",
    Minimum = 1,
    Maximum = 8,
    Value = 8,
    LimitHintFormatter = (edge, limit) => edge == NumberFieldLimitEdge.Maximum
        ? $"容量为 {limit}"
        : $"至少 {limit}"
};
```

### 绑定与事件

`Value` 为双向绑定。`ValueChanged` 会报告旧值、新值以及 `ChangeSource`。

```xml
<arc:NumberField Label="项目" Minimum="0" Maximum="50" Value="{Binding ItemCount, Mode=TwoWay}" />
```

```csharp
field.ValueChanged += (_, e) =>
{
    Console.WriteLine($"{e.OldValue} -> {e.NewValue} ({e.ChangeSource})");
};
```

## API 参考

<!-- api: NumberField -->

## 无障碍

- `Label` 为字段命名。`IsLabelVisible="False"` 时标签隐藏，但仍作为名称。
- 字段是一个微调按钮。其值文字包含前缀与后缀，微调按钮分别命名为 "增加 <label>" 与 "减少 <label>"。
- ArrowUp 与 ArrowDown 逐步调整，Shift 或 PageUp 与 PageDown 使用大步长，Home 与 End 跳到限值。
- 修改会以礼貌方式播报。被限制到边界时，会播报所达到的限值。
- 焦点环仅在键盘聚焦时显示。

## 动效

- 数字沿变化方向像里程表一样滚动。按住微调按钮时会连续重复并逐渐加速。
- 在限值处按下会将数值回弹，抖动微调按钮，并显示简短的限值提示。
- 减弱动效时，数值立即设定，并以警告颜色闪烁限值。关闭动效时同样如此。

## 相关组件

- `Slider`：当范围中的位置比精确数字更重要时使用。
- `Input`：没有微调按钮的数字字符串。
- `MoneyInput`：货币金额；`ScrubInput`：属性面板中密集的数值调整。
