---
title: 拖动数值
summary: Figma 风格的数字字段：拖动标签即可调整，也可以键入数值、单位与算术表达式。
source-hash: d1376e8d8e40
---

<Demo name="Inspector" />

## 何时使用

`ScrubInput` 是为属性检视器和属性面板设计的紧凑数字字段。它的标签兼作拖动手柄：用户可以左右拖动标签来微调数值，也可以点击字段输入精确的数值。一组这样的字段读起来就像设计工具的检视器，因此适用于任何需要反复调整、并凭视觉检查一行数字的界面。

- **仪表板编辑器中的组件布局**：图表卡片的 X、Y、W 与 H。设计师拖动 W 查看网格的反应，然后输入 `320` 使其对齐到网格单位。
- **报告或文档编辑器中的视觉设置**：不透明度、旋转与缩放。拖动可以快速反馈，输入则能得到品牌规范要求的精确数值。
- **可由用户更改单位的设置**：设计师以 `px`、`pt` 或 `em` 输入描边宽度。单位按钮会在 `Units` 之间循环，输入 `50%` 这类带单位的值也会切换单位。
- **规则或阈值面板中的限值**：0 到 100 百分比之间的告警阈值，或最小为 1 秒的超时时间。`Minimum` 与 `Maximum` 在用户拖动时保持数值在范围内。
- **内部工具中的主题与令牌编辑器**：间距、圆角与阴影的数值会立即改变预览，`ValueChanged` 驱动预览，`ValueCommitted` 写入已保存的主题。

## 何时不用

- **用户用按钮步进的表单数字**：用 `NumberField`，它带有步进按钮与限值提示，适用于结账数量或席位数等场景。
- **位置比精确数字更重要的范围取值**：用 `Slider`，例如音量或亮度控制。
- **货币金额**：用 `MoneyInput`，它负责货币符号、分组与金额取整。
- **表格中的大量数值**：用 `DataGrid` 展示数字表格。`ScrubInput` 设计用于一个面板中的少量字段，不适用于列表中的每个单元格。
- **没有数值含义的文本**：用 `Input`。`ScrubInput` 只接受数字、算术与单位。

## 用法

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:ScrubInput Label="X" Value="120" Unit="px" />
</UserControl>
```

`ScrubInput` 派生自 `TemplatedControl`，而不是文本框。控件自行绘制外壳（背景、1 px 边框、轨道与拉伸效果），并承载一个无边框的文本编辑器用于输入。因此拖动、拉伸与抖动都发生在外壳上，而数字仍是普通的可编辑文本。标签就是拖动手柄，因此可见的标签也是控件唯一的鼠标操作手柄。

### 结构

1. **标签**（`Label`）：以 500 字重绘制的文字，或是一个控件，例如 14 px 的 `ArcGlyph`。标签宽度至少 30 px，前面留 10 px 内边距，后面留 4 px。拖动标签即可调整数值，双击则将数值恢复为 `ResetValue`。
2. **外壳**：带弱化表面色的外壳，1 px 边框，中等尺寸下圆角为 12 px（小尺寸为 9 px）。`Medium` 时高 36 px，`Small` 时高 30 px。
3. **轨道**（`IsTrackVisible`）：一层轻柔的底色，前景色的 6%，按数值在两个限值之间的比例填充外壳。仅在两个限值都是有限值时显示，拖动数值时会变亮。
4. **数字**（编辑器）：字段显示的数值，按有效精度取整。左侧留 4 px 内边距，获得焦点时全选其文字。
5. **单位**（`Unit`、`Units`）：数字之后的单位，间距 2 px。只有一个单位时显示为普通文字。有多个单位时变为按钮，循环切换各单位，圆角 6 px，悬停时出现前景色 7% 的底色。
6. **修饰键标签**：拖动过程中按住 Shift 时显示 "×10"、按住 Alt 时显示 "×0.1" 的 20 px 胶囊标签，位于字段上方约 6 px 处。只有按住修饰键时才显示。

### 拖动与输入

在标签上按下并水平移动 3 px 或以上即开始拖动。每移动 `PixelsPerStep` 像素（默认 2）改变一个 `Step`。拖动过程中数值跟随指针，松开后提交。点击数字即可聚焦字段并选中其文字。输入时数值不会改变，直到按 Enter、字段失去焦点或按方向键时才提交。

Shift 与 Alt 在拖动中改变步长，标签会显示倍数。Escape 取消拖动，并将数值恢复为拖动开始时的值。触摸同样可用：水平拖动调整数值，垂直触摸则滚动父面板，因此很长的检视器仍可用手指滚动。

### 限值与循环

`Minimum` 与 `Maximum` 定义范围。默认值为负无穷与正无穷，即没有限制。拖动超过限值时，字段最多会拉伸 12 px 并带有阻力，松开后弹回。输入或步进超过限值时，数值会被钳制到限值并提交；处于限值时的步进不会改变数值，并给出一个小的反弹。

`IsWrapping` 使数值从最大值循环回最小值，而不是停止。它需要两个限值，并且最大值必须大于最小值。缺少任一有限限值时此设置无效，因此无界的字段永远不会循环。

```xml
<arc:ScrubInput Label="W" Value="320" Minimum="0" Unit="px" ResetValue="320" />
<arc:ScrubInput Label="O" Value="80" Minimum="0" Maximum="100" Unit="%" ResetValue="100" />
```

```csharp
var rotation = new ScrubInput
{
    Label = "R",
    Value = 15,
    Minimum = 0,
    Maximum = 360,
    IsWrapping = true,
    IsTrackVisible = false,
    Unit = "°",
    ResetValue = 0
};
```

`ResetValue` 是双击标签后恢复的值。`IsTrackVisible` 默认在两个限值都是有限值时显示轨道；对于角度，0 到 360 的轨道意义不大，可将其设为 `False`。

### 步长与精度

`Step` 是每次方向键和每步拖动的变化量，默认为 1。`FineStep`（按住 Alt）默认为 `Step` 的十分之一，`CoarseStep`（按住 Shift）默认为 `Step` 的十倍。粗步长落在粗网格上，因此按住 Shift 时从 13 步进会得到 20（步长为 10），而不是 23。

`Precision` 设置小数位数。未设置时，字段保留 `Step` 与精细步长中较多的小数位，因此 `Step` 为 0.1 时保留两位小数。显示文本会去掉末尾的零，因此 1.5 显示为 `1.5`。

```xml
<arc:ScrubInput Label="间距" Value="1.5" Step="0.1" Minimum="0" Unit="em" />
```

### 输入语法

编辑器接受算术、数字后的单位，以及作为小数点的逗号。`ScrubExpression.Evaluate` 运行相同的语法，因此字段与设置导入对数值的含义保持一致。

| 输入 | 结果 |
|---|---|
| `12*2`、`12 × 2`、`24/2`、`12+3-1` | 算术运算；`×`、`÷` 与 `−` 也可作为运算符 |
| `(100-24)/2` | 括号用于分组运算 |
| `1,5em` | `1.5`，单位为 `em`；逗号是小数点 |
| `50%` | `50`，单位为 `%`；若 `%` 在 `Units` 中，则切换到该单位 |
| `15deg`、`15°` | `15`，单位为角度，与 `°` 相同 |
| `abc`、`Units` 中没有 `px` 时的 `12px`、`4/0` | 被拒绝：保留旧值，字段将输入标记为无效 |

单位不区分字母大小写。单位不在单位列表中的输入会被拒绝，结果不是有限值（例如除以零）时也会被拒绝。

### 多个单位

设置多个 `Units` 后，单位变为按钮。点击会切换到下一个单位，数值保持不变。在输入框中键入单位也会切换单位：在单位为 `px` 的字段中输入 `50%`，会把单位设为 `%`，数值设为 50。只有一个单位，或未设置 `Units` 的字段，单位以普通文字显示，单位按钮不是 Tab 停靠点。

<Demo name="CyclingUnits" />

### 尺寸

`Size` 设置高度。`Small` 为 30 px，文字 12 px，圆角 9 px。`Medium` 为默认值：36 px，文字 14 px，圆角 12 px。`Large` 没有自己的尺寸，渲染为 `Medium`。密集的检视器列使用 `Small`；与其他 36 px 控件混用的表单或面板使用 `Medium`。

<Demo name="Sizes" />

### 绑定与事件

`Value` 与 `Unit` 是双向的。`ValueChanged` 在每次变化时触发，包括拖动的每一步以及代码中的每次变化。`ValueCommitted` 在变化稳定后触发：拖动结束时、按 Enter 或失去焦点提交输入时、按键步进落定时，或执行重置时。两者都会报告旧值、新值、单位以及 `ChangeSource`。

`ChangeSource` 说明变化来自何处。`Programmatic` 标记由代码或绑定设置的值，它只触发 `ValueChanged`，不提供反馈；`Scrub`、`Type`、`Key` 与 `Reset` 分别标记用户改变数值的四种方式。保存数值的处理程序应使用 `ValueCommitted`，以免在拖动的每一步都写入。

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

`ValueCommitted` 在提交未改变数值时也会触发，例如被钳制到限值的输入。若保存的代价较高，保存前请比较 `e.OldValue` 与 `e.NewValue`。

### 绑定到视图模型

布局检视器把每个字段绑定到所选组件的一个属性。视图模型持有数值并负责保存，字段负责显示。示例使用 CommunityToolkit.Mvvm。

```csharp
public partial class WidgetLayoutViewModel : ObservableObject
{
    [ObservableProperty]
    private double _x = 120;

    [ObservableProperty]
    private double _width = 320;

    [ObservableProperty]
    private double _opacity = 80;

    [RelayCommand]
    private void ResetLayout()
    {
        X = 0;
        Width = 320;
        Opacity = 100;
    }
}
```

```xml
<StackPanel Spacing="12">
    <arc:ScrubInput Label="X"
                    Value="{Binding X, Mode=TwoWay}"
                    Unit="px" />
    <arc:ScrubInput Label="W"
                    Value="{Binding Width, Mode=TwoWay}"
                    Minimum="0"
                    Unit="px"
                    ResetValue="320" />
    <arc:ScrubInput Label="不透明度"
                    Value="{Binding Opacity, Mode=TwoWay}"
                    Minimum="0"
                    Maximum="100"
                    Unit="%"
                    ResetValue="100" />
</StackPanel>
```

`Value` 与 `Unit` 是双向属性。`ValueChanged` 与 `ValueCommitted` 是事件而不是命令，因此需要在代码后置或行为中连接，并在 `ValueCommitted` 中写入已保存的文档。`Minimum`、`Maximum`、`Step` 与 `ResetValue` 是字段的单向设置。

### 图标标签

14 px 的图标可以代替文字标签，使密集的面板保持平静。图标没有可供辅助技术读取的文字，因此请用 `AutomationProperties.Name` 为字段设置无障碍名称。双击图标仍会重置数值。

<Demo name="GlyphLabels" />

### 状态

禁用的字段会淡化到 50% 并忽略输入，因此不能被拖动或输入。无法读取的输入（例如 `abc`）会被拒绝：边缘变为红色 900 ms，并伴随短暂的抖动，旧值保留。被拒绝的输入是字段唯一显示错误的方式；字段没有消息行，因此当有效范围很重要时，请在面板旁边放一条可见的提示。

<Demo name="States" />

### 示例：仪表板卡片检视器

仪表板编辑器的布局面板，显示所选卡片 "Revenue by region"。设计师拖动 W 与 H 标签来感受网格，并在品牌规范要求时输入精确的尺寸。旋转没有轨道，因为 0 到 360 度的轨道意义不大。拖动超过 0 或 100% 时，不透明度字段会拉伸并回弹。卡片的数值是唯一的数据来源；面板在 `ValueCommitted` 时写回。

![仪表板卡片的布局检视器，含 X、Y、W、H、旋转与不透明度字段](images/TileInspector.png)

```xml
<StackPanel Width="600" Spacing="16">
    <TextBlock Text="Revenue by region"
               FontSize="16"
               FontWeight="SemiBold"
               Foreground="{arc:ArcDesignTokenResource Foreground}" />
    <Grid ColumnDefinitions="*,*" RowDefinitions="Auto,Auto,Auto" ColumnSpacing="12" RowSpacing="12">
        <arc:ScrubInput Grid.Row="0" Grid.Column="0" Label="X" Value="120" Unit="px" />
        <arc:ScrubInput Grid.Row="0" Grid.Column="1" Label="Y" Value="48" Unit="px" />
        <arc:ScrubInput Grid.Row="1" Grid.Column="0" Label="W" Value="320" Minimum="0"
                        Unit="px" ResetValue="320" />
        <arc:ScrubInput Grid.Row="1" Grid.Column="1" Label="H" Value="180" Minimum="0"
                        Unit="px" ResetValue="180" />
        <arc:ScrubInput Grid.Row="2" Grid.Column="0" Label="Rotation" Value="15" Minimum="0"
                        Maximum="360" IsWrapping="True" IsTrackVisible="False" Unit="°"
                        ResetValue="0" />
        <arc:ScrubInput Grid.Row="2" Grid.Column="1" Label="Opacity" Value="80" Minimum="0"
                        Maximum="100" Unit="%" ResetValue="100" />
    </Grid>
    <TextBlock Text="Drag a label to scrub. Double click a label to reset it."
               FontSize="12"
               Foreground="{arc:ArcDesignTokenResource TextMuted}" />
</StackPanel>
```

### 文案规范

- **标签**：密集面板中用简短的轴或属性名（"X"、"W"、"旋转"、"不透明度"），表单中用完整的词（"不透明度"，而不是缩写）。单位放在 `Unit` 中，绝不写在标签里。
- **单位**：数值的度量单位：`px`、`%`、`em`、`°`。在密集的行中使用符号，而不是文字。
- **限值**：将 `Minimum` 与 `Maximum` 设为物理或业务上的限制，使拖动不能越过产品拒绝的数值。只有在数值没有自然边界时才留空。
- **帮助文字**：`ScrubInput` 没有消息行。应在附近的标签、标题或面板的工具提示中说明有效范围，例如 "不透明度 0 到 100 百分比"。

| 不要写 | 改为 |
|---|---|
| 标签 "Width (px)"，单位 "px" | 标签 "W"，单位 "px" |
| 标签 "Opacity %" | 标签 "Opacity"，单位 "%" |
| 标签 "Rot" | 标签 "Rotation"，单位 "°" |
| 单位 "pixels" | 单位 "px" |

### 最佳实践

**推荐**

- 为每个有默认值的字段设置 `ResetValue`。双击即可把设计恢复到默认值，比菜单更快，也符合设计工具用户的习惯。
- 从 `ValueCommitted` 保存，而不是从 `ValueChanged`。拖动会在每一步触发 `ValueChanged`，在每一步都保存会每秒写入文档几十次。
- 将 `Step` 设为产品使用的粒度，例如布局用 1 px、排版用 0.1 em。精细与粗步长由它推导。
- 字段宽度与面板的列宽保持一致，密集的检视器列使用 `Size="Small"`。

**避免**

- 用于货币。拖动得到的金额没有取整规则，而 `MoneyInput` 会按区域设置格式化货币、分组与小数。
- 只靠拖动来设定用户必须精确到达的值。输入是精确的途径，应保持字段可编辑。
- 在长表格中放置大量 `ScrubInput`。每个字段都是带有自己指针跟踪的模板；数字的行请使用 `DataGrid`。
- 在没有有限限值时设置 `IsWrapping`。循环需要同时设置 `Minimum` 与 `Maximum`，否则该设置没有任何作用。
- 通过禁用字段来表示错误。禁用的字段会变暗并忽略输入；被拒绝的输入才是错误信号。

### 常见问题

**为什么我输入的数值跳回了旧值？** 输入被拒绝了。未知单位、格式错误的表达式或除以零会保留旧值，将 `IsInvalid` 设为 900 ms，并使字段抖动。超出限值的值不会被拒绝；它会被钳制到限值并提交。

**步长为 0.1 时，为什么输入的 `1.5` 显示为 `1.5`？** 没有设置 `Precision`，因此字段保留 `Step` 与精细步长（`Step` 的十分之一）中较多的小数位。末尾的零会被去掉。设置 `Precision` 可为所有数值固定小数位数。

**如何从代码更改单位？** 将 `Unit` 设为一个字符串。如果用户可以循环或键入该单位，也要把它放进 `Units`，使单位按钮与输入框都接受它。`Unit` 的变化是双向的，因此绑定会收到它。

**支持触摸吗？** 支持。水平触摸拖动会调整数值，垂直触摸会滚动父面板，因此很长的检视器仍可用手指滚动。

**可以不用标签吗？** 可以，通过图标标签实现，图标标签可以是任何控件。`Label` 是 `object`，因此 `ArcGlyph` 可以使用。请为字段设置 `AutomationProperties.Name`，因为图标没有可供辅助技术读取的文字。

## API 参考

<!-- api: ScrubInput -->

配套类型。`ScrubInputChangeSource` 是事件参数中的 `ChangeSource`：

| 成员 | 含义 |
|---|---|
| `Programmatic` | 由代码或绑定设置的 `Value`；只触发 `ValueChanged` |
| `Scrub` | 在标签上拖动；也包括按 Escape 取消的拖动 |
| `Type` | 用 Enter、失去焦点、方向键或辅助技术提交的输入 |
| `Key` | ArrowUp 或 ArrowDown 步进 |
| `Reset` | 双击标签，恢复到 `ResetValue` |

`ScrubInputValueChangedEventArgs` 是 `ValueChanged` 与 `ValueCommitted` 的参数：

| 成员 | 含义 |
|---|---|
| `OldValue` | 变化前的值；拖动后 `ValueCommitted` 报告拖动开始时的值 |
| `NewValue` | 变化后的值 |
| `ChangeSource` | 产生变化的 `ScrubInputChangeSource` |
| `Unit` | 变化后生效的单位；字段没有单位时为 `""` |

`ScrubExpression.Evaluate(text, units)` 为公开方法，返回 `ScrubExpressionResult`（`Value`、`Unit`），被拒绝的输入返回 `null`。在导入或命令行中读取输入的值时使用它，其规则与字段相同。

## 无障碍

- 数字是微调按钮。其无障碍名称为标签，其值文字会朗读带单位的数值（例如 "50 percent" 或 "90 degrees"），并以范围形式公开最小值、最大值与当前值。
- 标签是真正的标签：点击会聚焦字段并选中其数字。若标签是图标，则需要 `AutomationProperties.Name`。
- 无效输入会在短时间内将字段设为无效状态，并保留旧值。该消息会被播报。
- 单位按钮的名称为 "Unit: <unit>. Change unit"，只有存在多个单位时才是 Tab 停靠点。
- 通过范围提供程序设置数值时，与输入一样会被钳制到限值。

### 键盘

字段本身不是焦点目标：数字编辑器获得焦点，标签仅是指针手柄。

| 按键 | 操作 |
|---|---|
| `Tab` / `Shift+Tab` | 将焦点移到数字；有多个单位时也会移到单位按钮 |
| 点击数字 | 聚焦并选中其文字 |
| `ArrowUp` / `ArrowDown` | 按 `Step` 步进数值；若有未提交的输入，先提交 |
| `Shift` + `ArrowUp` / `ArrowDown` | 按 `CoarseStep` 步进，落在粗网格上 |
| `Alt` + `ArrowUp` / `ArrowDown` | 按 `FineStep` 步进 |
| `Enter` | 提交输入，并选中全部文字 |
| `Escape`（输入未提交时） | 将输入恢复为上一次的值，并全选 |
| `Escape`（拖动中） | 取消拖动，并将数值恢复为拖动开始时的值 |
| 焦点离开数字 | 提交未完成的输入 |
| 拖动中按住 `Shift` 或 `Alt` | 立即切换步长，并显示倍数标签 |
| 双击标签 | 将数值恢复为 `ResetValue` |

## 动效

- 超过限值时，字段带阻力拉伸，松开后弹回。数字随拉伸倾斜。
- 无效输入或处于限值的步进会使字段抖动一次。
- 单位变化时带着轻微模糊升起。拖动时轨道填充跟随指针；输入或步进时，则滑动到新值。

| 变化 | 完整 | 减弱 | 关闭 |
|---|---|---|---|
| 超过限值拖动 | 字段拉伸至多 12 px，松开后弹回 | 不拉伸 | 立即完成 |
| 被拒绝的输入或处于限值的步进 | 字段抖动一次；边缘红色保持 900 ms | 不抖动；红色边缘仍显示 900 ms | 立即完成；红色边缘显示 900 ms |
| 单位变化 | 单位带着轻微模糊升起 | 单位原地淡入 | 立即完成 |
| 轨道填充 | 拖动时跟随指针；输入或步进时滑动到新值 | 跳到新值 | 立即完成 |

## 相关组件

- `NumberField`：带步进按钮与限值提示的表单数字。
- `Slider`：范围上的位置比精确数值更重要时使用。
- `MoneyInput`：货币数值。
- `Input`：自由文本；`DataGrid`：数字表格。

| 组件 | 适用场景 |
|---|---|
| `NumberField` | 数值是用户通过按钮步进的表单值，例如数量 |
| `Slider` | 用户在范围上选择一个位置，精确数值没那么重要 |
| `MoneyInput` | 值是带货币与取整规则的金额 |
| `DataGrid` | 数字是表格中的行与列，而不是面板中的几个字段 |
| `Input` | 值是没有数字含义的文本 |
