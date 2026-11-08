---
title: 数字筛选框
summary: 一个数字筛选条件：用复选框启用，操作符以符号显示，再填一个值，可设比较容差。
source-hash: d8035c1b0666
---

<Demo name="Basic" />

## 何时使用

`NumberFilterBox` 是高级筛选中的一行：启用条件的复选框、字段名、以符号显示的操作符（= ≠ < ≤ > ≥）和值。在用户需要对列表的数值字段设定条件时使用：

- **商品列表中的价格、评分与测量值**：“价格 ≤ 1000”。
- **库存或日志视图中的计数**：“库存 < 50”、“错误 > 0”。
- **同时设多个条件**：与 `TextFilterBox` 叠放在筛选面板中，每个条件单独启用或关闭。

筛选框只保存条件，从不过滤数据：由视图模型读取 `IsActive`、`Operator` 和 `Value`，再按合适的方式过滤（LINQ、数据库查询、远程 API，实时或点按钮后）。

## 何时不用

- **快速切换的分面**（“有库存”“促销中”）：使用 `ChipGroup` 或 `FilterToolbar`。
- **有上下两端的范围**：使用范围模式的 `Slider`，或两个筛选框（“≥ 10”和“≤ 50”）。
- **表单里的普通数字**：使用 `NumberField`。

## 用法

`NumberFilterBox` 派生自 `FilterBox`（一个 Avalonia `TemplatedControl`）。它的各部分都是 Arc 控件：带标签和说明的 `Checkbox`、选择操作符的 `Select` 和输入值的 `NumberField`。状态以双向绑定：

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:NumberFilterBox Label="Price"
                         Description="Products at or above this price."
                         Operator="GreaterThanOrEqual"
                         Value="100"
                         NumberMode="Decimal"
                         DecimalPlaces="2"
                         Minimum="0"
                         Maximum="99999"
                         Tolerance="0.01" />
</UserControl>
```

`IsActive`、`Operator` 和 `Value` 默认就是双向绑定，`{Binding PriceActive}` 这类绑定不写 `Mode=TwoWay` 也会写回。

### 整数与小数

`NumberMode` 为 `Integer`（整数）或 `Decimal`（默认，小数位数由 `DecimalPlaces` 决定，默认 2）。`SmallChange` 是方向键和步进按钮的一步，也是输入值对齐的网格（从 `Minimum` 起算）；默认整数为 1，小数为最后一位小数的一个单位。`LargeChange` 是 Page Up、Page Down 和 Shift+方向键的步长。`Minimum` 默认为 0：需要负数时请设置它。

`Tolerance` 属于条件而不属于输入框：相差不超过 `Tolerance` 的两个值视为相等。它不影响能输入什么。

<Demo name="Modes" />

### 关闭筛选

取消勾选（或把 `IsActive` 设为 `False`）会禁用操作符、值和 `ExtraContent`，但保留它们：再次勾选即恢复原来的条件。复选框本身始终可用，点击标签也能切换。

<Demo name="Active" />

### 限定操作符

`Operators` 把菜单限定为一个列表，顺序由你决定。符号及其朗读名称来自 `FilterOperators`，它是一张普通的对照表，不使用反射，因此兼容 AOT 和裁剪。条件有任何变化后都会引发 `FilterChanged`。

<Demo name="Operators" />

### 在视图模型中过滤

`Condition` 是当前条件，类型为 `NumberFilterCondition` 记录：操作符、值和容差。它不含界面，视图模型可以用自己的属性构造一个，再用 `Matches` 逐项判断。筛选关闭时，筛选框上的 `Matches` 总是返回 true。

```csharp
var price = new NumberFilterCondition(PriceOperator, PriceValue, Tolerance: 0.01m);
var visible = products.Where(p => !PriceActive || price.Matches(p.Price));
```

容差放宽了相等：`Equal` 接受容差以内的值，`NotEqual` 接受容差以外的值，`<` 和 `>` 必须超出容差，`≤` 和 `≥` 允许差一个容差。值的类型是 `decimal`；`double` 请用 `(decimal)value` 转换。

<Demo name="Combined" />

### 对齐叠放的筛选框

给每个筛选框设相同的 `LabelSizeGroup`，并在容器上设 `Grid.IsSharedSizeScope="True"`：标签列取最长标签的宽度，操作符因此对齐。也可以用 `LabelWidth` 设固定宽度。`Size` 设置操作符与值输入框的高度（Small 36、Medium 44、Large 50）。

```xml
<StackPanel xmlns="https://github.com/avaloniaui"
            xmlns:arc="https://github.com/AIDotNet/TKCode.Arc"
            Grid.IsSharedSizeScope="True" Spacing="8">
    <arc:NumberFilterBox Label="Price" LabelSizeGroup="FilterLabel" />
    <arc:TextFilterBox Label="Product name" LabelSizeGroup="FilterLabel" />
</StackPanel>
```

## API 参考

<!-- api: NumberFilterBox -->

<!-- api: NumberFilterCondition -->

<!-- api: NumberOperator -->

<!-- api: NumberFilterMode -->

## 无障碍

- 复选框以标签命名，以 `Description` 作说明；按空格切换。
- 操作符字段命名为“Price operator”（标签加 `OperatorText`），帮助文本是操作符的名称，如“Greater than or equal to”；同样的名称也显示为工具提示。
- 值输入框以标签命名。

### 键盘

| 按键 | 时机 | 操作 |
|---|---|---|
| Tab | 任意位置 | 在复选框、操作符和值之间移动（已禁用的部分会跳过） |
| 空格 | 复选框 | 启用或关闭筛选 |
| Enter、空格、上、下 | 操作符 | 打开菜单；之后的移动与选择同 `Select` |
| 符号键 | 操作符 | 跳到该符号对应的操作符 |
| 上、下 | 值 | 一个 `SmallChange` |
| Page Up、Page Down | 值 | 一个 `LargeChange` |

## 动效

- 各部分沿用各自控件的动效：勾选标记自行绘出，操作符菜单从字段下方展开，值的数字滚动。整行本身没有动画。
- 减少动效或关闭动效时，各部分按各自的减弱方式表现。

## 响应式

- 操作符 84 px，值 168 px。标签列与标签同宽（或取 `LabelWidth`，或取其尺寸组的宽度），过长的标签或说明会换行，为输入框留出空间。
- 连 120 px 的标签都放不下时（约 400 px 以下），标签移到输入框上方单独一行，此时该筛选框不再使用共享的 `LabelSizeGroup`。固定的 `LabelWidth` 放不下时同样如此。
- 所有输入框至少 36 px 高（`Size="Small"`），复选框在任何尺寸下都保留 44 px 的点击区域。

## 相关组件

- `TextFilterBox`：同样的一行，用于文本，支持包含、开头、结尾、等于和正则表达式。
- `FilterToolbar`：列表上方几个快速分面的标签。
- `NumberField`：表单中的数字，不带操作符。
