---
title: 数字输入框
summary: 通过清晰的增减控件输入有范围限制的数字。
source-hash: 0120ea6d8624
---

<Demo name="Default" />

## 何时使用

`NumberField` 是两侧各带一个步进器的有界数字输入框。它适合用户精确设定、且经常按一个步长调整的值：套餐的席位数、购物车中的商品数量、保留天数、单价。字段在用户输入以及按下步进器时都会执行范围限制，因此表单永远不会保存超出所显示限制的值。

- **SaaS 管理后台中的席位与许可证数量**：管理员用加号步进器把席位数从 3 调到 4，“每个团队最多 10 个”的标签在限制生效期间保持可见。
- **订单或购物车中的数量**：买家把某一行商品的数量在 1 到 99 之间调整，限制提示说明加号步进器为何停在最大值。
- **开发者或运维工具中的重试与阈值设置**：工程师设置重试次数、以秒为单位的超时时间或百分比阈值，单位后缀随数值变化。
- **固定步长的价格与单位成本**：商品目录编辑器以 $0.50 为步长调整价格，或以 $0.01 为步长调整成本，步长决定字段显示的小数位数。
- **可就地调整数值的紧凑设置面板**：设计工具中的宽度或不透明度字段，开启 `IsScrubEnabled` 后拖动标签即可改变数值。

## 何时不用

- **位置比精确数字更重要的近似值**：用 `Slider`，它展示完整的范围，让用户判断位置。
- **电话号码、邮政编码或验证码等数字串**：用 `Input`。步进器对标识符没有意义，字段还会改变其格式。
- **带货币代码的金额**：用 `MoneyInput`，它为金额分组、显示货币并输出最小单位值。
- **需要拖动调整多个数值的画布式属性面板**：整个面板围绕拖动构建时，用 `ScrubInput`。
- **从固定选项列表中选择的数字**：用 `Select` 或 `Combobox`。步进器暗示数值是连续的。

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

`NumberField` 派生自 `FieldTextBox`，因此它是带有 Arc 标签、辅助文字与错误行的 Avalonia `TextBox`。文本框就是草稿编辑器：当字段未处于编辑状态时，它的文字是透明的，数值以滚动数字的形式绘制。输入会打开一个草稿；草稿只要有效、在范围内并且落在步长网格上，就会实时生效，并在用户按下 Enter 或离开字段时提交。`Value` 是 `decimal`，因此价格和小数的运算是精确的。

### 结构

1. **标签**（PART_Label）：字段的标签，位于外壳上方的标题行中，标题行与外壳之间相距 8 px。
2. **限制提示**（PART_LimitNote）：标签旁边 12 px 的提示，例如“最多 10 个座位”，颜色由前景色与警告色混合而成的警示色调。按压碰到限制时出现。
3. **减少步进器**（PART_DecreaseButton）：按尺寸为 28、34 或 40 px 的方形，带 16 px 的减号图标，圆角 14 px。
4. **数值**（PART_ValueGroup、PART_Digits）：数值及其前缀与后缀，位于两个步进器之间，两侧各留 2 px 间距。
5. **增加步进器**（PART_IncreaseButton）：与减少步进器相同的方形，带 16 px 的加号图标。
6. **外壳**：字段外框，四周内缩 3 px（大尺寸时为 4 px）。除非设置了 `Width`，否则小、中、大三种尺寸的宽度分别为 164、196 与 228 px；中等尺寸的外壳高 44 px。
7. **辅助文字与错误行**：外壳下方的字段辅助文字与 `ErrorMessage`。

步进器到达限制时淡化到 32%，拒绝按压时淡化到 60%，字段禁用时淡化到 35%。位于限制处的步进器仍可按压，因此按压时依然会显示限制。

### 前缀、后缀与步长

`Step` 设置增量，其精度决定默认的小数位数：步长 `0.5` 显示一位小数，步长 `0.01` 显示两位。`Prefix` 与 `Suffix` 是数值之前与之后的文字。`SuffixFormatter` 是数值的函数，因此单位可以随数值变化，例如单数与复数形式。

<Demo name="Price" />

```csharp
var seats = new NumberField
{
    Label = "座位数",
    Minimum = 1,
    Maximum = 10,
    Value = 3,
    SuffixFormatter = n => n == 1 ? " seat" : " seats"
};
```

`PrefixFormatter` 对前缀的作用相同。两者都是函数，因此无法在 XAML 中使用；请像上面的示例那样在代码后置文件中设置。`Suffix` 字符串是静态形式。

用户输入时，小于零的最小值会接受负号，因此 -20 到 40 这样的范围无需特殊处理。

<Demo name="NegativeRange" />

### 格式与区域文化

`Culture` 设置分组分隔符与小数分隔符，默认值为 `en-US` 而不是当前区域文化，因此同一个数值在每台机器上的读法都一致。`UseGrouping` 用于开启或关闭分组分隔符。`MinimumFractionDigits` 与 `MaximumFractionDigits` 会覆盖步长所给出的小数位数。当数值有固定的显示格式时使用它们，例如以整数步进但需要显示两位小数的费率。

### 尺寸

`Size` 设置宽度与步进器尺寸：`Small` 宽 164 px，步进器为 28 px；`Medium`（默认值）宽 196 px，步进器为 34 px；`Large` 宽 228 px，步进器为 40 px。显式设置的 `Width` 会把外壳拉伸到该宽度，而不是尺寸所对应的宽度。

<Demo name="Sizes" />

同一个表单只用一种尺寸。`Small` 适合密集的表格或工具栏，`Medium` 是设置页与表单的默认值，`Large` 适合页面上单个醒目的数字，例如商品页上的数量。

### 限制提示与错误

`IsLimitHintEnabled`（默认开启）在按压碰到限制或输入的值越界时，在标签旁显示提示。默认文字为“最多 10 个座位”或“最少 1 个座位”。`LimitHintFormatter` 根据触及的边界及其限制值生成提示。

<Demo name="LimitHint" />

`ErrorMessage` 会把字段标记为无效。它用于范围无法表达的规则，例如订单中有商品时数量至少为一。

<Demo name="States" />

### 校验

范围规则由字段负责，业务规则由视图模型或表单负责。下表说明每项检查何时执行。

| 规则 | 何时检查 | 显示在哪里 |
|---|---|---|
| 超过 `Maximum` | 输入时草稿变为警告色调；在限制处按压会使其抖动并说明限制 | 限制提示与实时播报（“10 个座位，已达最大值”） |
| 低于 `Minimum` | 输入时，等待短暂时间（700 ms）后检查，使用户能够输入较大数字的第一位 | 外壳变为警告色调，然后显示限制提示 |
| 落在步长网格上 | 草稿提交时（按 Enter 或离开字段） | 数值会对齐到步长网格并限制在范围内 |
| 业务规则，例如至少一件商品 | 提交时，由视图模型检查 | `ErrorMessage`；数值变化时清除 |

通过代码设置 `Value` 永远不会显示抖动或提示。它会安静地更新显示，因此加载的表单不会闪现警告。

### 绑定与事件

`Value` 为双向绑定。`ValueChanged` 报告旧值、新值以及 `ChangeSource`，后者说明数值来自代码、步进器、按键、滚轮、拖动还是输入。

<Demo name="BindingAndEvents" />

```xml
<arc:NumberField Label="项目数" Minimum="0" Maximum="50" Value="{Binding ItemCount, Mode=TwoWay}" />
```

```csharp
field.ValueChanged += (_, e) =>
{
    Console.WriteLine($"{e.OldValue} -> {e.NewValue} ({e.ChangeSource})");
};
```

`StepUp` 与 `StepDown` 从代码中按一个步长移动数值。它们会先提交已打开的草稿，并且不做任何播报，也不产生抖动，因此应用中的“加一”按钮不会闪现限制。

### 绑定到视图模型

将数值及其限制保存在视图模型中，并让字段执行它所显示的限制。来自字段的变化会以 `Value` 更新的形式到达，因此视图模型无需额外的处理程序。

```csharp
public partial class SeatPlanViewModel : ObservableObject
{
    public const int MaxSeats = 10;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SeatCost))]
    private decimal _seats = 3;

    [ObservableProperty]
    private decimal _pricePerSeat = 12.5m;

    public decimal SeatCost => Seats * PricePerSeat;

    [RelayCommand]
    private async Task SavePlanAsync()
    {
        await _plans.SaveAsync("plan_team_2026", (int)Seats);
    }
}
```

```xml
<StackPanel Spacing="16">
    <arc:NumberField Label="座位数"
                     Value="{Binding Seats, Mode=TwoWay}"
                     Minimum="1"
                     Maximum="10"
                     Suffix=" 个座位"
                     Description="每个团队最多 10 个。" />
    <arc:Button Content="保存套餐" Command="{Binding SavePlanCommand}" />
</StackPanel>
```

- 在 XAML 中保留 `Minimum` 与 `Maximum`，使限制在标记中可见，只绑定 `Value`。
- 只在边界处转换为整数，因为字段保存的是 `decimal`，而席位数是整数。

### 拖动标签

开启 `IsScrubEnabled` 后，左右拖动标签即可改变数值：每移动 6 px 改变一个步长。越过限制后，数值会带阻力地跟随指针，松开指针时停在限制处。只有当标签是明显的拖动手柄时（例如设计面板）才启用拖动。在其他地方，无意的拖动会改变数值。

<Demo name="ScrubLabel" />

### 键盘

先让字段获得焦点，再使用键盘。按键列在字段下方。

<Demo name="Keyboard" />

### 示例：管理后台中的席位套餐表单

管理员在管理后台中为团队套餐设置席位数。席位字段在辅助文字中说明限制，并且加号步进器到达最大值后，限制提示也会出现。每个席位的价格使用相同的步长规则，带有前缀并保留两位小数，使价格保持易读。支持时长字段的值为 0，业务规则不允许这样做，因此它带有一条说明该规则的 `ErrorMessage`。主操作按钮说明了接下来会发生什么。

![座位数已达最大值、含有每席价格并带有数量错误的套餐表单](images/SeatPlanForm.png)

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <StackPanel Width="620" Spacing="20">
        <TextBlock Text="Team plan: Acme Analytics"
                   FontSize="14"
                   FontWeight="SemiBold"
                   Foreground="{arc:ArcDesignTokenResource Foreground}" />
        <arc:NumberField Label="Seats"
                         Value="10"
                         Minimum="1"
                         Maximum="10"
                         Suffix=" seats"
                         Width="360"
                         HorizontalAlignment="Left"
                         Description="Up to 10 per team. Changes apply from the next invoice." />
        <arc:NumberField Label="Price per seat"
                         Value="12.5"
                         Prefix="$"
                         Step="0.5"
                         MinimumFractionDigits="2"
                         Minimum="0"
                         Maximum="999"
                         Width="360"
                         HorizontalAlignment="Left"
                         Description="Billed monthly in USD." />
        <arc:NumberField Label="Support hours"
                         Value="0"
                         Minimum="0"
                         Maximum="40"
                         Suffix=" hours"
                         Width="360"
                         HorizontalAlignment="Left"
                         ErrorMessage="Add at least 1 support hour, or turn support off." />
        <StackPanel Orientation="Horizontal" Spacing="8" HorizontalAlignment="Right">
            <arc:Button Content="Discard" Variant="Secondary" />
            <arc:Button Content="Save plan" />
        </StackPanel>
    </StackPanel>
</UserControl>
```

三个字段共用同一宽度，因此数值在一列中对齐。限制提示在按压到达限制之前保持隐藏，这让界面在有内容要说之前保持安静。

### 文案规范

- **标签**：为数量或设置命名的名词，例如“座位数”“重试次数”“超时”。标签始终可见；步进器的名称也包含标签，因此请保持简短。
- **后缀**：当单位随数字变化时，用 `SuffixFormatter` 分别给出单数与复数形式，例如“ seat”与“ seats”。后缀以空格开头，使数字与单位不会紧贴在一起。
- **辅助文字**：说明限制或后果，例如“每个团队最多 10 个”“变更从下一张发票开始生效”。数字用阿拉伯数字书写。
- **限制提示**：用字段的术语说明用户触及的限制，例如“最多 10 个座位”“最少 1 个座位”。保持为一个简短的短语。
- **错误提示**：说明应该输入什么，而不是哪里错了，例如“请至少添加 1 个支持小时，或关闭支持。”

| 不要写 | 改为 |
|---|---|
| 数值无效 | 请输入 1 到 10 之间的数字。 |
| 数量 | 本订单中的商品 |
| 已达上限！ | 最多 10 个座位 |
| 座位数： | 座位数 |

### 最佳实践

**推荐**

- 根据业务规则设置 `Minimum` 与 `Maximum`，使步进器停在规则所允许的位置，并由限制提示说明原因。
- 把 `Step` 设为用户应做出的最小变化：半小时用 `0.5`，分用 `0.01`。步长也决定小数位数，因此显示的值永远不会是步长无法产生的值。
- “加一”按钮使用 `StepUp` 与 `StepDown`，因为在限制处使用按钮时它们不会闪现限制。
- 在视图模型中以 `decimal` 保存数值，只在与整数或支付接口的边界处取整。
- 在表单中关闭 `IsScrubEnabled`，避免在标签上的拖动意外改变数值。

**避免**

- 把字段用于标识符或代码，因为步进器与格式化会改变字符。
- 保存字段会拒绝的数字，例如超过 `Maximum` 的席位数。这样表单就会保存一个界面从未显示为有效的值。
- 在用户仍在输入有效数字时就显示 `ErrorMessage`。应在提交时或字段失去焦点后再设置。
- 在大范围上使用很小的 `Step`。在 0 到 999 之间以 0.01 为步长需要按很多次，因此使用 `MoneyInput` 或 `Input` 直接输入会更快。
- 当限制从上下文中并不明显时，用 `IsLimitHintEnabled="False"` 隐藏限制提示。没有提示时，停住的步进器看起来像是坏了。

### 常见问题

**为什么加号步进器看起来变暗了？** 数值已达最大值。步进器会淡化到 32%，但仍可按压，因此按压时依然会显示限制提示与抖动。

**如何显示随数字变化的单位？** 在代码中设置 `SuffixFormatter`：`n => n == 1 ? " seat" : " seats"`。它是函数，因此不是 XAML 属性。

**为什么输入的数字会对齐到步长？** 草稿在按下 Enter 或字段失去焦点时提交，提交时会把数值对齐到从 `Minimum` 开始的步长网格上，然后限制在范围内。

**如何知道数值是因步进还是输入而改变的？** 读取 `ValueChanged` 中的 `ChangeSource`。`Key` 表示方向键或翻页键，`Button` 表示步进器，`Wheel` 表示字段有焦点时的鼠标滚轮，`Scrub` 表示拖动标签，`Type` 表示输入，`Programmatic` 表示代码。

**可以使用鼠标滚轮吗？** 可以，字段有键盘焦点时，每一格滚轮都会步进数值。这是 Arc 设计的扩展，不希望使用它的应用可以避免让字段获得焦点。

## API 参考

<!-- api: NumberField -->

变化来源与限制边界是公共枚举，事件与限制提示会使用它们。

| 类型 | 成员 |
|---|---|
| `NumberFieldValueChangedEventArgs` | `OldValue`、`NewValue`（decimal）、`ChangeSource`（`NumberFieldChangeSource`） |
| `NumberFieldChangeSource` | `Programmatic`、`Button`、`Key`、`Wheel`、`Scrub`、`Type` |
| `NumberFieldLimitEdge` | `Minimum`、`Maximum` |

## 无障碍

- `Label` 为字段命名。`IsLabelVisible="False"` 时标签隐藏，但仍作为名称。
- 字段是带范围的微调按钮：其最小值、最大值、小步长（1）与大步长（10）都会暴露，值文字为带前缀与后缀的数值，例如“3 个座位”。
- 步进器根据 `DecreaseText`、`IncreaseText` 与标签命名为“Decrease Seats”和“Increase Seats”。它们在限制处仍可获得焦点，因此屏幕阅读器可以读到限制提示。
- 变化以礼貌方式播报。限制生效时播报所到达的限制（“10 个座位，已达最大值”），超出范围的输入值会被加入帮助文本。
- 焦点环只在键盘焦点时出现。使用指针按压时不绘制焦点环。

### 键盘

| 按键 | 操作 |
|---|---|
| `Up` / `Down` | 按 `Step` 步进；按住按键会重复步进，400 ms 后开始，并逐渐加快 |
| `Shift` + `Up` / `Down`，`PageUp` / `PageDown` | 按 `LargeStep` 步进（默认为十个步长） |
| `Home` / `End` | 没有打开草稿时移到 `Minimum` / `Maximum`（`End` 需要有限的最大值） |
| 数字、`-`（当 `Minimum` 小于零时）、小数分隔符 | 输入草稿；有效的草稿实时生效 |
| `Enter` | 提交已打开的草稿（对齐并限制范围）；没有草稿时选中整个数字 |
| `Escape` | 把已打开的草稿恢复为编辑前的值 |
| 步进器获得焦点时的 `Enter` | 步进一次 |
| `Tab` | 从编辑器移到减少与增加步进器，然后继续向后 |

字段有键盘焦点时，鼠标滚轮会步进数值。按在步进器上的按键属于步进器，而不属于草稿。

## 动效

- 数字沿变化方向像里程表一样滚动，从 9 回绕到 0。被另一次步进打断的步进会在滚动途中改变目标。
- 在限制处按压会把数值弹回，使被拒绝的步进器抖动，并显示限制提示约 1.5 秒。
- 减少动效时数值立即设定，并以警告色闪烁限制。关闭动效时同样如此。

| 变化 | 完整 | 减弱 | 关闭 |
|---|---|---|---|
| 步进器或方向键步进 | 数字沿变化方向滚动 | 数值立即变化 | 数值立即变化 |
| 在限制处按压 | 数值弹回，步进器抖动，限制提示上升 | 数字以警告色闪烁 | 数字以警告色闪烁 |
| 输入的草稿超出限制 | 外壳变为警告色调 | 外壳变为警告色调 | 外壳变为警告色调 |

## 相关组件

- `Slider`：当范围上的位置比精确数字更重要时使用。
- `Input`：用于没有步进器的数字串。
- `MoneyInput` 用于货币金额，`ScrubInput` 用于紧凑的属性面板。

| 组件 | 适用场景 |
|---|---|
| `Slider` | 用户在范围内判断位置，精确数字不那么重要 |
| `MoneyInput` | 数字是带货币代码与最小单位的金额 |
| `ScrubInput` | 紧凑面板中的多个数值通过拖动调整 |
| `Input` | 值是标识符或代码，而不是数量 |
| `Select` / `Combobox` | 值是固定选项列表中的一项 |
