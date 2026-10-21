---
title: 金额输入框
summary: 带有实时分组、宽度稳定、数字滚动与最小单位输出的货币输入框。
source-hash: f28fa88adbe5
---

<Demo name="Default" />

## 何时使用

- 用户手动输入的金额，例如付款、小费、转账与预算。
- 用户可以更改的货币，例如发票或报销单。
- 需要最小单位值（分、日元）用于存储或支付接口的金额。

## 何时不用

- 数量及其他非货币数字：用 `NumberField`。
- 从一个范围中选取金额：用 `Slider`。
- 通过拖动调整数值的紧凑属性面板：用 `ScrubInput`。
- 外观像数字的自由文本，例如账户代码：用 `Input`。

## 用法

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:MoneyInput Label="金额"
                    Currency="USD"
                    Minimum="5"
                    Maximum="1000"
                    Description="方向键每次调整一个单位。" />
</UserControl>
```

### 货币与快速添加芯片

`Currencies` 设置货币菜单中的代码。只有一个代码时，该代码以静态文字显示。`QuickAddAmounts` 设置以主单位表示的芯片。列表为空时隐藏芯片。

日元没有最小单位，因此该字段不显示分。

<Demo name="SingleCurrency" />

<Demo name="NoQuickAdd" />

<Demo name="NoDecimals" />

### 区域文化

`Culture` 设置分组方式与符号位置。默认值为 `en-US`。

<Demo name="Culture" />

### 错误与说明

`ErrorMessage` 非空时，字段标记为无效并替换说明文字。`Description` 提供辅助文字。

<Demo name="DescriptionAndError" />

### 绑定与事件

`Value` 为双向绑定，使用主单位，因此 `12.50` 即为 `12.5`。`ValueChanged` 会报告旧值与新值、货币、最小单位、格式化字符串以及 `ChangeSource`。

<Demo name="BindingAndEvents" />

```xml
<arc:MoneyInput Label="金额" Value="{Binding Amount, Mode=TwoWay}" Currency="{Binding Currency, Mode=TwoWay}" />
```

```csharp
amount.ValueChanged += (_, e) =>
{
    Console.WriteLine($"{e.Formatted} ({e.Currency}), minor units {e.MinorUnits}, from {e.ChangeSource}");
};

amount.OpenCurrencyMenu();
```

### 最小值与最大值

`Minimum` 与 `Maximum` 以主单位设置范围。输入超过最大值会被拒绝并推动金额，最小值则在字段失去焦点时检查。

<Demo name="Limits" />

### 隐藏标签

`IsLabelVisible="False"` 会隐藏标签，但仍保留给屏幕阅读器。

<Demo name="HiddenLabel" />

### 禁用

禁用的字段保留其金额，并忽略输入。

<Demo name="Disabled" />

## API 参考

<!-- api: MoneyInput -->

## 无障碍

- `Label` 为字段命名。`IsLabelVisible="False"` 时标签隐藏，但仍作为名称。
- 字段是一个微调按钮。其值为主单位数值，值文字为完整的货币字符串。
- ArrowUp 与 ArrowDown 每次调整一个单位，Shift 或 PageUp 与 PageDown 每次调整十个步进，Enter 完成小数输入。
- 货币按钮会打开支持键入查找的列表。方向键移动，Enter 或 Space 选中，Escape 返回按钮。
- 范围、提示与错误会被播报，并显示为辅助文字。

## 动效

- 数字沿变化方向滚动，新增的数位会扩展宽度。键入的数字立即落定。
- 被拒绝的修改会使金额横向推动，并闪烁范围提示。更换货币时符号会滚动切换。
- 减弱动效时，移除滚动、推动与宽度弹簧效果：数字淡入，尺寸直接跳转。关闭动效时同样如此。

## 相关组件

- `NumberField`：数量及其他不涉及货币的数字。
- `ScrubInput`：拖动调整的数值设置；`Slider`：从范围中选取。
- `Input`：没有金额结构的自由文本。
