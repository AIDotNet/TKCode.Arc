---
title: 金额输入框
summary: 带有实时分组、宽度稳定、数字滚动与最小单位输出的货币输入框。
source-hash: 464ecb4deb4f
---

<Demo name="Default" />

## 何时使用

`MoneyInput` 是用于手动输入金额的字段。用户输入时它会实时为数字分组，把缺少的角分显示为淡色数字，金额增长时保持宽度稳定，并同时输出支付与账务接口所需的主单位值（$12.50 对应 `12.5`）和最小单位值（`1250`）。凡是用户需要确定将要收取、退款、预算或转账的金额，且货币可能需要更改的地方，都适合使用它。

- **支付与结账表单**：客户输入捐款、小费或自定义金额，并通过快速添加标签选择常用数值，最大值与卡片限额或订单总额保持一致。
- **账单控制台中的退款与额度页面**：客服输入不超过已付金额的退款金额，字段下方的范围文字会在输入前说明允许的区间（“$5.00 to $120.00”）。
- **货币可更改的发票与报销单**：金额保持为数字，切换货币菜单即可更改代码，1,250.00 USD 的发票改为 EUR 时无需重新输入数字。
- **金融应用中的预算与转账**：用户设置每月限额，最小值在字段失去焦点时检查，因此输入 0.50 会在离开字段后才提示，而不是在输入过程中提示。
- **需要最小单位的存储与支付接口**：字段提供 `MinorUnits`，应用为 $12.50 发送 1250，永远不需要处理二进制浮点金额。

## 何时不用

- **不带货币的数量与计数**：用 `NumberField`，它有步进按钮，单位不做货币格式化。
- **通过范围滑动选取金额**：用 `Slider`，它一次展示完整的区间。
- **需要拖动就地调整数值的紧凑属性面板**：用 `ScrubInput`，它通过拖动标签来改变数字。
- **看起来像数字的自由文本，例如账户代码或发票号**：用 `Input`。金额字段会为数字分组并丢弃字母，这会破坏标识符。
- **从产品列表中选择价格**：用 `Select` 或 `Combobox`。用户选择的是一条记录，而不是一个金额。

## 用法

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:MoneyInput Label="金额"
                    Currency="USD"
                    Minimum="5"
                    Maximum="1000"
                    Description="方向键每次调整一个单位。" />
</UserControl>
```

`MoneyInput` 派生自 `FieldTextBox`，即带有 Arc 标签、辅助文字与错误行的 Avalonia `TextBox`。文本框只负责光标与选区：它自身的文字是透明的，金额由字段按列逐位绘制在同一原点上。这就是数字能够滚动和增长、而光标不会移动的原因。文本框同样保留 Avalonia 的光标、选区与剪贴板输入行为，但撤销已关闭，因为金额是受控的，不能与 `Value` 产生偏差。

### 结构

1. **外壳**（PART_Frame）：高 76 px 的外框，采用面板圆角，左侧内边距 18 px，右侧 8 px。金额本身获得焦点时边框加深；货币切换按钮获得焦点时不加深。
2. **符号与金额**（PART_Amount）：展示字体 36 px、中等字重，使用等宽数字且不做字距调整。字母间距为 -0.03 em。缺少的小数位以弱化颜色、55% 不透明度的淡色数字显示。
3. **前缀或后缀符号**（PART_PrefixSymbol、PART_SuffixSymbol）：货币符号位于数字之前或之后，遵循区域文化的正数格式。`de-DE` 中的欧元符号位于金额之后。
4. **货币按钮**（PART_CurrencyButton）：高 36 px 的按钮，显示 ISO 代码与 16 px 的箭头。点击后打开货币菜单。`Currencies` 中只有一个货币时，代码改为静态文字显示（PART_StaticCode）。
5. **范围文字**（PART_Range）：外壳下方 12 px 的弱化文字，例如“$5.00 to $1,000.00”。拒绝修改时以警告色闪烁。
6. **快速添加标签**（PART_Chips）：默认显示为“+$10”“+$50”和“+$100”的 30 px 高标签，间距 6 px，与范围文字位于同一行。
7. **货币菜单**：宽 256 px，行高至少 36 px，列表最高 288 px，与外壳相距 8 px。
8. **辅助文字与错误行**：来自 `Field` 的标签、辅助文字与 `ErrorMessage` 行。

### 货币与快速添加标签

`Currencies` 设置货币菜单中的代码。默认包含八个代码：USD、EUR、GBP、JPY、CAD、AUD、CHF 与 INR。请使用与页面服务市场相符的较短列表，因为过长的菜单会拖慢选择。只有一个代码时，字段以静态文字显示该代码，且不会打开菜单。

`QuickAddAmounts` 以主单位设置标签。默认值为 10、50 与 100。每个标签都会把其金额加到当前值上。列表为空时隐藏标签。标签应与金额场景相符：小费和捐款使用较小的值，退款则使用账户中订单的金额。达到最大值时，标签呈现为禁用样式。

日元没有最小单位，因此字段不显示角分，最小单位值等于主单位值。

<Demo name="SingleCurrency" />

<Demo name="NoQuickAdd" />

<Demo name="NoDecimals" />

### 区域文化

`Culture` 设置分组分隔符、小数分隔符与符号位置。默认值为 `en-US`，而且字段不跟随当前线程的区域文化，因此不同地区的用户在每个界面上看到的格式都一致。请把 `Culture` 设为金额受众所在的区域，例如德国发票使用 `de-DE`。

<Demo name="Culture" />

### 错误与辅助文字

`Description` 在字段下方提供辅助文字。`ErrorMessage` 会把字段标记为无效，并以该消息替换辅助文字。未设置明确消息且金额超出范围时，会显示内置的范围错误。

<Demo name="DescriptionAndError" />

### 绑定与事件

`Value` 是双向绑定，使用主单位，因此 `12.50` 即为 `12.5`。`Currency` 同样是双向绑定。`ValueChanged` 报告旧值与新值、货币、最小单位值、格式化字符串以及 `ChangeSource`。`CurrencyChanged` 报告旧代码与新代码，以及是菜单还是代码修改了它。

<Demo name="BindingAndEvents" />

```xml
<arc:MoneyInput Label="金额" Value="{Binding Amount}" Currency="{Binding Currency}" />
```

```csharp
amount.ValueChanged += (_, e) =>
{
    Console.WriteLine($"{e.Formatted} ({e.Currency}), minor units {e.MinorUnits}, from {e.ChangeSource}");
};

amount.OpenCurrencyMenu();
```

`OpenCurrencyMenu` 需要 `Currencies` 中至少有两个代码；`CloseCurrencyMenu` 会关闭菜单并把焦点返回到按钮。`IsCurrencyMenuOpen` 报告菜单的状态。

### 绑定到视图模型

视图模型以 `decimal?` 保存主单位的金额，并以 ISO 代码保存货币。只在与支付服务的边界处保存最小单位值。

```csharp
public partial class RefundViewModel : ObservableValidator
{
    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Required(ErrorMessage = "请输入退款金额。")]
    [Range(typeof(decimal), "0.01", "120", ErrorMessage = "请输入 $0.01 到 $120.00 之间的金额。")]
    private decimal? _amount = 84.50m;

    [ObservableProperty]
    private string _currency = "USD";

    [RelayCommand]
    private async Task RefundAsync()
    {
        ValidateAllProperties();
        if (HasErrors || Amount is not { } major)
        {
            return;
        }
        long minorUnits = decimal.ToInt64(major * 100m);
        await _payments.RefundAsync("ch_2026_0412", minorUnits, Currency);
    }
}
```

```xml
<arc:MoneyInput Label="退款金额"
                Value="{Binding Amount, Mode=TwoWay}"
                Currency="{Binding Currency, Mode=TwoWay}"
                Minimum="0.01"
                Maximum="120" />
```

- `Value` 与 `Currency` 默认为双向绑定，因此视图模型会收到每一次变化，包括从菜单中做出的更改。
- 依赖变化来源的副作用应使用 `ValueChanged`。视图模型的程序化赋值会触发 `ValueChanged`，其 `ChangeSource` 为 `Programmatic`，且不做任何播报，也不产生推动效果。

### 最小值与最大值

`Minimum` 与 `Maximum` 以主单位设置范围。输入超过最大值会被拒绝：金额向按下的方向推动，范围文字闪烁，并播报该上限。最小值在字段失去焦点时检查，因此用户可以先输入较短的数值，再逐步输入更大的数值。

<Demo name="Limits" />

### 校验

在用户能够处理某条规则的时刻检查它。字段负责范围，其余规则由表单决定金额的含义。

| 规则 | 何时检查 | 显示在哪里 |
|---|---|---|
| 超过 `Maximum` | 输入时，用户键入时即被拒绝 | 范围文字闪烁并播报上限；数值不变 |
| 低于 `Minimum` | 字段失去焦点时 | 显示 `ErrorMessage`，内容为“最小值为 $5.00”（即 `MinimumErrorFormat` 文字），外框变为无效态 |
| 必填或业务规则 | 提交时，由视图模型检查 | `ErrorMessage`；明确的消息会替换内置的范围错误 |
| 服务端规则，例如卡片限额 | 请求结束后 | 字段上的 `ErrorMessage`；金额变化时清除 |

把 `ErrorMessage` 设为 `null` 即可移除明确的消息，并重新显示内置的范围错误。内置文字来自 `MinimumErrorFormat` 与 `MaximumErrorFormat`，范围文字来自 `RangeFormat`（"{0} to {1}"）、`MaximumOnlyFormat`（"Up to {0}"）与 `MinimumOnlyFormat`（"At least {0}"）。可以按产品的措辞修改它们，但要保留 `{0}` 占位符。

### 隐藏标签

`IsLabelVisible="False"` 会隐藏标签，但仍保留给屏幕阅读器。只有当字段上方的标题已经为金额命名时，才这样做。

<Demo name="HiddenLabel" />

### 禁用

禁用的字段保留其金额，并忽略输入。请在辅助文字中说明金额被锁定的原因，例如“发票发出后锁定”。

<Demo name="Disabled" />

### 示例：账单控制台中的退款表单

客服在账单控制台中为一笔付款退还一部分金额。退款字段在外壳下方显示范围（“$5.00 to $120.00”），因此客服在输入前就能看到上限。两个快速添加标签（+$10 与 +$25）可以让客服快速加上常用金额。结算金额字段为空，并带有明确的错误，说明缺少什么以及去哪里查找，因为银行尚未确认该金额。辅助文字说明了后果（“3 到 5 个工作日内退回银行卡”），这正是用户需要阅读该字段的原因。

![含有有效金额、范围文字、快速添加标签以及空结算金额错误的退款表单](images/RefundForm.png)

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc"
             xmlns:generic="using:System.Collections.Generic">
    <StackPanel Width="620" Spacing="20">
        <TextBlock Text="Refund for payment ch_2026_0412"
                   FontSize="14"
                   FontWeight="SemiBold"
                   Foreground="{arc:ArcDesignTokenResource Foreground}" />
        <arc:MoneyInput Label="Refund amount"
                        Value="84.5"
                        Currency="USD"
                        Minimum="5"
                        Maximum="120"
                        Description="Returned to the card in 3 to 5 business days.">
            <arc:MoneyInput.QuickAddAmounts>
                <generic:List x:TypeArguments="x:Decimal">
                    <x:Decimal>10</x:Decimal>
                    <x:Decimal>25</x:Decimal>
                </generic:List>
            </arc:MoneyInput.QuickAddAmounts>
        </arc:MoneyInput>
        <arc:MoneyInput Label="Settlement amount"
                        Currency="EUR"
                        Culture="de-DE"
                        ErrorMessage="Enter the amount the bank confirmed on the settlement report." />
        <StackPanel Orientation="Horizontal" Spacing="8" HorizontalAlignment="Right">
            <arc:Button Content="Cancel" Variant="Secondary" />
            <arc:Button Content="Issue refund" />
        </StackPanel>
    </StackPanel>
</UserControl>
```

这两个字段共用一列与同一种标签样式，因此视觉上它们属于同一个表单。第二个字段的错误使用明确的消息，它替换了内置的范围错误，因为该字段为空，不适用范围错误。

### 文案规范

- **标签**：用名词短语说明金额的角色，例如“退款金额”“小费”“每月限额”。在一个有多个金额的界面上，不要只写“金额”。
- **辅助文字**：说明后果或限制，例如“3 到 5 个工作日内退回银行卡”“最多为已付金额”。数字用阿拉伯数字书写。
- **错误提示**：说明哪里有问题以及应该输入什么，用词与辅助文字的表述保持一致，例如“请输入 $0.01 到 $120.00 之间的金额。”避免使用“金额无效”。
- **操作按钮**：按钮用动词加对象说明结果，例如“发起退款”“保存限额”。不要写“提交”。

| 不要写 | 改为 |
|---|---|
| 金额无效 | 请输入 $0.01 到 $120.00 之间的金额。 |
| 金额 | 退款金额 |
| 数值太大！ | 最大值为 $120.00。 |
| 提交 | 发起退款 |

### 最佳实践

**推荐**

- 根据业务规则设置 `Minimum` 与 `Maximum`，这样字段会拒绝不可能的输入，范围文字也能在用户输入前告知区间。
- 把 `MinorUnits` 存储并发送给支付接口，视图模型中的 `Value` 保持主单位，用于显示。
- 为界面的受众设置 `Culture`，而不是使用操作系统的区域文化，这样同一个金额在每台机器上的读法都一致。
- 把标签限制在两到三个与该界面工作相符的值，标签过多会挤占这一行。
- 只有当金额可能更改货币时才显示货币菜单。单一代码以静态文字显示是有原因的。

**避免**

- 把 `Value` 转换为 `double` 或 `float`。decimal 类型能精确保留角分，而二进制浮点数不能。
- 把 `MoneyInput` 用于数量、计数或百分比。货币符号和角分会误导用户。
- 在输入第一个字符时就对最小值报错。最小值在失去焦点时检查，让用户先输完内容。
- 金额修正后仍保留过时的 `ErrorMessage`。请把它设为 `null`，让内置的范围错误或辅助文字重新出现。
- 在金融界面上依赖当前区域文化。机器的区域设置变化会改变分组与符号。

### 常见问题

**为什么空字段显示 0.00？** 那是幽灵数字，用来标示角分的位置。此时 `Value` 为 `null`，`FormattedValue` 为空字符串，因此应检查 `Value` 是否为 `null`，而不是检查显示的数字。

**如何获取支付接口所需的金额？** 读取 `MinorUnits`（类型为 `long?`）。对于 USD，它是分；对于 JPY，它是日元。字段为空时其值为 `null`，因此应在发送请求前进行校验。

**为什么货币名称是英文的？** 字段使用内置的 ISO 4217 代码表与英文名称，因为 .NET 并未为所有货币提供本地化的显示名称。符号与数字格式仍遵循 `Culture`。

**为什么输入超过最大值时金额会移动？** 这次拒绝是一次推动：金额向按下的方向滑动后弹回，让用户看到这次更改被拒绝了。减少动效时取消推动，但范围文字仍会闪烁。

**更改货币时金额会怎样？** 主单位金额保持不变（12.5 仍为 12.5），最小单位值按新货币重新计算。从菜单更改时，先触发 `CurrencyChanged`（来源为 `Menu`），再触发 `ValueChanged`，其 `ChangeSource` 为 `Currency`。

## API 参考

<!-- api: MoneyInput -->

事件参数与变化来源是事件所使用的公共类型。

| 类型 | 成员 |
|---|---|
| `MoneyInputValueChangedEventArgs` | `OldValue`、`NewValue`（decimal?）、`Currency`、`MinorUnits`（long?）、`Formatted`、`ChangeSource` |
| `MoneyInputCurrencyChangedEventArgs` | `OldCurrency`、`NewCurrency`、`ChangeSource` |
| `MoneyInputChangeSource` | `Programmatic`、`Type`、`Paste`、`Key`、`Chip`、`Currency`、`Automation` |
| `MoneyInputCurrencyChangeSource` | `Programmatic`、`Menu` |

## 无障碍

- `Label` 为字段命名。`IsLabelVisible="False"` 时标签隐藏，但仍作为名称。
- 字段是一个微调按钮。其数值为主单位数值，值文字为完整的货币字符串，例如“$12.50”；没有金额时为“空”。范围显示为微调按钮的最小值与最大值。
- 辅助文字、范围与错误合并为字段的帮助文本，在名称和值之后朗读。
- 按钮名称为“Currency, US Dollar”，它会打开一个支持键入查找的列表。
- 被拒绝的修改（“最大值为 $100.00”）、键盘与标签步进以及货币更改都会以礼貌方式播报。键入不做播报，因此屏幕阅读器不会逐个重复数字。
- 带动画的数字对辅助技术隐藏，辅助技术读取字段的真实文字。

### 键盘

| 按键 | 操作 |
|---|---|
| 数字与小数分隔符 | 输入金额；字母会被丢弃，光标保持不动 |
| `Up` / `Down` | 增加或减少一个主单位，或 `Step` 设定的值 |
| `Shift` + `Up` / `Down`，`PageUp` / `PageDown` | 步进幅度为十倍 |
| `Enter` | 确定小数位（12.5 变为 12.50），并把光标移到末尾 |
| 在分组分隔符上按 `Backspace` / `Delete` | 删除分隔符旁边的数字，而不是什么都不做 |
| `Ctrl+V`（macOS 上为 `Cmd+V`） | 粘贴 "$1,234.56"、"1.234,56 €" 或 "1234.5" 之类的值；整个字段被选中时替换，否则在光标处插入 |
| `Tab` | 把焦点移到下一个控件 |
| 货币按钮上的 `Down` / `Up` | 打开货币菜单 |
| 菜单中的 `Up` / `Down`、`Home` / `End` | 在货币之间移动高亮 |
| 菜单中的字母 | 跳到代码或名称以所键入文字开头的货币（600 ms 后重置键入查找） |
| 菜单中的 `Enter` / `Space` | 选择高亮的货币，并保留主单位金额 |
| 菜单中的 `Escape` | 关闭菜单，并把焦点返回到货币按钮 |
| 菜单中的 `Tab` | 关闭菜单，并把焦点移到下一个控件 |

`Enter` 会确定小数位，但不会提交表单。按键仍会传递给表单，因此默认按钮可以提交它。

## 动效

- 步进或快速添加标签改变金额时，数字沿变化方向滚动。在完整动效下，用户键入的数字会立即落定，因此光标永远不会落后于按键。
- 被拒绝的修改会以弹簧效果把金额横向推动，并使范围文字闪烁。
- 更改货币时，符号滚动到新的位置；过长的金额会缩放以适应宽度。

| 变化 | 完整 | 减弱 | 关闭 |
|---|---|---|---|
| 步进按键或快速添加标签 | 数位列沿变化方向滚动 | 数字淡入，尺寸直接跳变 | 数字淡入，尺寸直接跳变 |
| 被拒绝的修改（超过最大值） | 金额以弹簧效果横向推动；范围文字闪烁 | 无推动；范围文字仍闪烁 | 无推动；范围文字仍闪烁 |
| 出现新的数位列 | 列宽以弹簧效果增长 | 宽度直接跳变 | 宽度直接跳变 |
| 更改货币 | 符号滚动到新位置 | 符号直接变化，无滚动 | 符号直接变化，无滚动 |

## 相关组件

- `NumberField`：用于数量及其他不涉及货币的数字。
- `ScrubInput` 用于拖动调整的数值设置，`Slider` 用于从范围中选取。
- `Input` 用于没有金额结构的自由文本。

| 组件 | 适用场景 |
|---|---|
| `NumberField` | 数字是数量、计数或带单位的值，而不是金额 |
| `ScrubInput` | 在紧凑面板中通过拖动调整数字设置 |
| `Slider` | 用户在范围内选取一个值，精确数字不那么重要 |
| `Input` | 值是标识符或自由文本，只是恰好含有数字 |
| `Select` / `Combobox` | 用户从列表中选择一个带价格的项目或记录 |
