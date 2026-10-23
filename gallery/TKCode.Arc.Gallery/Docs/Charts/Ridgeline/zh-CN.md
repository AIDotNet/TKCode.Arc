---
title: 山脊图
summary: 重叠的分布，每组一条山脊：悬停可抬起山脊并读取其四分位数，切换数据集时每条曲线都会变形。
source-hash: af053f192692
---

<Demo name="DistributionsByMonth" />

## 何时使用

`Ridgeline` 并排比较几组分布的形状。每组是一条山脊：一条平滑的密度曲线，绘制在上一行之上，因此读者能看到单一平均值会掩盖的离散程度、偏斜以及第二个峰。它适用于运营或分析页面，其问题在于各组的分布有何不同，而不只是平均值有何不同。

- **按地区划分的响应时间**，用于状态或可观测性页面。三条山脊显示亚太地区在 250 ms 附近有一组较慢的响应，仅看中位数会把它掩盖。
- **按月划分的每日最高气温**，用于天气或能源需求仪表板。山脊显示季节性的偏移，以及冬季的离散程度是否变大。
- **按客户群划分的订单金额**，用于电商复盘。具有双峰的客群（小额单品与大宗订单）会呈现为两条山脊的形状，而不是误导性的均值。
- **按团队划分的解决时长**，用于支持或 CRM 报告。每个团队的那一行都能看出慢工单的长尾。
- **按承运商划分的配送时长**，用于物流控制台，此时准时率比平均值更重要。

## 何时不用

- **每组只有一个汇总数值**，例如按地区的平均收入：用 `BarChart`，它能精确比较数值。
- **每组观测值很少**：由少量数值画出的曲线只是猜测。当某组少于约 20 个数值时，用汇总数值的表格，或用 `BarChart` 显示汇总数字。
- **单一数值随时间的变化**：用 `LineChart`，其横轴为时间。
- **精确比较少量数值**：用 `BarChart` 或表格。山脊展示的是形状，而不是精确的差值。
- **部分与整体的关系**，例如按来源划分的流量占比：用 `DonutChart`。
- **按天和小时的密度**，例如按星期和小时的活动：用 `ActivityHeatmap`，它以计数显示每个单元格。

## 用法

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:Ridgeline Label="苏黎世每日最高气温"
                   Unit="°C"
                   DomainMinimum="-5"
                   DomainMaximum="35"
                   ItemsSource="{Binding Months}" />
</UserControl>
```

每个 `RidgelineSeries` 是一组数据：一个 `Id`、一个 `Label` 和它的原始 `Values`。图表会为每组计算密度，并按系列顺序从上到下绘制山脊。`Ridgeline` 派生自 Avalonia 的 `Control`，而不是模板化的图表，因此整个框体都接收指针，山脊、标签与刻度都由控件自身绘制。只有在存在可供读取的观测值时，它才会获得键盘焦点。

### 结构

1. **行标签**（`RidgelineSeries.Label`）：绘制在左侧槽位中，宽 52 px（图表窄于 420 px 时为 36 px），位于每行基线之上。放不下的标签会被缩短。
2. **山脊**：每组一条填充曲线，在所有组共享的定义域上取 96 个采样点。开启 `IsTinted` 时，每条山脊按其中位数着色；关闭时则使用第一个系列的颜色。
3. **行与重叠**：默认每行高 30 px（`RowHeight`）。最高的山脊会向上方的行延伸 2.4 个行高（`Overlap`），因此顶部的山脊不会被裁切。
4. **数值轴**：最后一行下方 28 px 的区域，刻度标签间隔约 72 px，并使用 `Unit` 格式化。
5. **中位数色阶**（`IsTinted`）：坐标轴下方右对齐的“中位数”标签、最低与最高值，以及五个颜色色块。
6. **被抬起的山脊**：绘制在其他山脊之上的一份副本，抬高 6 px，中间一半加上阴影，并用一条短线标出中位数。
7. **提示**：系列标签、中位数、中间一半的范围与取值区间；读数光标移动后还会显示小于或等于该读数的数值所占的比例。提示宽度至少为 170 px，并始终位于图表之内。

### 数据集

替换 `ItemsSource` 即可切换数据集。每条曲线都会从当前形状变形到新形状，而山脊的顺序保持不变。

```csharp
// 将每个数据集保存为数组，然后赋值给用户选中的那一个。
chart.ItemsSource = selectedDataset == "zurich" ? zurichMonths : lisbonMonths;
```

山脊通过 `Id` 进行匹配，因此在两个数据集中具有相同 id 的组会变形为其新形状。新出现的组会单独占一行。

### 项目与数值格式

如果分组在代码中创建，通过 `Items` 添加；若用于视图模型，则绑定 `ItemsSource`。`ValueFormatter` 设置坐标轴刻度、中位数色阶、摘要与提示的格式。

<Demo name="Untinted" />

### 重叠与平滑度

`Overlap` 设定山脊向上一行延伸的程度，以行高的倍数表示。`RowHeight` 设定一行的高度。`Bandwidth` 以数值的单位设定平滑程度：较大的值得到更平滑、更概括的形状，较小的值则保留数据中更多的起伏。默认情况下，每组使用 Silverman 经验法则，它根据该组自身的离散程度与数量选取带宽。

```xml
<arc:Ridgeline Label="延迟" Overlap="2.4" RowHeight="30" Bandwidth="4" ItemsSource="{Binding Regions}" />
```

### 色调与定义域

`IsTinted` 按每条山脊的中位数着色，颜色从浅色渐变到第一个系列的颜色，并在坐标轴下方显示中位数色阶。当各组应被视为同一族，或颜色会暗示一种与图表重点无关的排名时，请将其关闭。`DomainMinimum` 与 `DomainMaximum` 固定数值轴，使其不会随数据集变化而移动。保持它们未设置（NaN），坐标轴会覆盖数据，并为平滑留出余量，刻度取整为易读的步长。

```xml
<arc:Ridgeline Label="延迟" IsTinted="False" DomainMinimum="0" DomainMaximum="300" ItemsSource="{Binding Regions}" />
```

### 读取山脊

`ActiveId` 是被抬起的山脊，且为双向绑定。`ActiveIdChanged` 报告每次变化，被抬起的系列位于 `Series` 中。

```csharp
chart.ActiveIdChanged += (_, e) => readout.Text = e.Series is null
    ? string.Empty
    : $"{e.Series.Label}: {e.Series.Values.Count} 个观测值";
```

提示与无障碍文本包含中位数、中间一半与取值区间。如果页面需要自己的数字，可在处理程序中根据 `Series` 计算，例如延迟面板的第 95 百分位数。

### 绑定到视图模型

使用 CommunityToolkit.Mvvm 时，视图模型负责分组与选中的组。每个组都是一个 `RidgelineSeries`，切换数据集时视图模型替换整个数组。图表把被抬起的组保存在 `ActiveId` 中。

```csharp
public partial class LatencyViewModel : ObservableObject
{
    [ObservableProperty]
    private RidgelineSeries[] _regions = [];

    [ObservableProperty]
    private string? _activeRegion;

    public void Load(IReadOnlyDictionary<string, double[]> samplesByRegion)
    {
        Regions = samplesByRegion
            .Select(pair => new RidgelineSeries(pair.Key, pair.Key, pair.Value))
            .ToArray();
    }
}
```

```xml
<arc:Ridgeline Label="各地区响应时间"
               Unit="ms"
               ItemsSource="{Binding Regions}"
               ActiveId="{Binding ActiveRegion, Mode=TwoWay}" />
```

- `ActiveId` 是双向的：山脊被抬起时图表会写入它，视图模型也可以从代码中抬起某条山脊。
- 不是 `RidgelineSeries` 的项，在设置 `KeySelector`、`LabelSelector` 与 `ValuesSelector`，或对应的成员绑定（`KeyMemberBinding`、`LabelMemberBinding` 与 `ValuesMemberBinding`）时同样可用。两者都设置时以选择器为准。
- 编译绑定需要在视图上设置 `x:DataType`；示例为简洁起见使用普通的 `Binding`。

### 数据结构与性能

传入原始观测值，而不是直方图。非有限值（NaN 与无穷大）会被忽略。如果两个系列共享同一个 `Id`，则绘制第一个，后面的会被丢弃，因此请保证同一数据集内的 id 唯一。

密度在每次数据变化时为每组计算一次，计算在共享定义域上进行，动画只对采样后的曲线进行插值。组数保持在约二十组以内，每组观测值至少二十个，使每条曲线都具有真实的形状。高度共享同一刻度：所有组中最高的峰会升到重叠高度。观测值更多的组看起来并不更高。又高又窄的山脊表示其数值集中。

使用 `ValueFormatter` 或 `Unit` 格式化数值。默认格式会为千位分组，最多保留一位小数，并使用真正的减号，然后追加 `Unit`，因此 -3 °C 显示为“−3°C”。

### 示例：按地区划分的响应时间

Acme Analytics 的运营页面展示了一周内三个地区响应时间的差异。仅看中位数，会让人以为亚太地区只比美洲慢一点。山脊则显示，在 250 ms 附近有一组较慢的响应，这正是值班工程师需要看到的部分。按中位数着色的山脊与中位数色阶说明了排名顺序；固定的定义域使页面切换到其他周时坐标轴保持不动。

![按中位数着色、含三个地区响应时间的山脊图，亚太地区有第二组较慢的响应](images/ResponseTimeRidges.png)

```xml
<Border xmlns="https://github.com/avaloniaui"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:arc="https://github.com/AIDotNet/TKCode.Arc"
        xmlns:collections="clr-namespace:Avalonia.Collections;assembly=Avalonia.Base"
        xmlns:sys="clr-namespace:System;assembly=System.Runtime"
        Width="680"
        Padding="24"
        CornerRadius="12"
        Background="{arc:ArcDesignTokenResource Surface}"
        BorderBrush="{arc:ArcDesignTokenResource Border}"
        BorderThickness="1">
    <StackPanel Spacing="16">
        <StackPanel Spacing="4">
            <TextBlock Text="Response time by region" FontSize="16" FontWeight="SemiBold" />
            <TextBlock Text="Samples per request, Sep 4 to Sep 10, 2026. APAC has a second group near 250 ms."
                       TextWrapping="Wrap"
                       Foreground="{arc:ArcDesignTokenResource TextSecondary}" />
        </StackPanel>
        <arc:Ridgeline Label="Response time by region"
                       Unit="ms"
                       RowHeight="36"
                       Overlap="1.8"
                       DomainMinimum="0"
                       DomainMaximum="400">
            <arc:Ridgeline.Items>
                <arc:RidgelineSeries Id="eu" Label="EU">
                    <arc:RidgelineSeries.Values>
                        <collections:AvaloniaList x:TypeArguments="sys:Double">
                            <sys:Double>108</sys:Double>
                            <sys:Double>112</sys:Double>
                            <sys:Double>115</sys:Double>
                            <sys:Double>118</sys:Double>
                            <sys:Double>119</sys:Double>
                            <sys:Double>121</sys:Double>
                            <sys:Double>122</sys:Double>
                            <sys:Double>123</sys:Double>
                            <sys:Double>124</sys:Double>
                            <sys:Double>125</sys:Double>
                            <sys:Double>126</sys:Double>
                            <sys:Double>128</sys:Double>
                            <sys:Double>129</sys:Double>
                            <sys:Double>131</sys:Double>
                            <sys:Double>133</sys:Double>
                            <sys:Double>136</sys:Double>
                            <sys:Double>139</sys:Double>
                            <sys:Double>142</sys:Double>
                            <sys:Double>156</sys:Double>
                        </collections:AvaloniaList>
                    </arc:RidgelineSeries.Values>
                </arc:RidgelineSeries>
                <arc:RidgelineSeries Id="us" Label="US">
                    <arc:RidgelineSeries.Values>
                        <collections:AvaloniaList x:TypeArguments="sys:Double">
                            <sys:Double>138</sys:Double>
                            <sys:Double>145</sys:Double>
                            <sys:Double>149</sys:Double>
                            <sys:Double>152</sys:Double>
                            <sys:Double>155</sys:Double>
                            <sys:Double>156</sys:Double>
                            <sys:Double>158</sys:Double>
                            <sys:Double>160</sys:Double>
                            <sys:Double>161</sys:Double>
                            <sys:Double>162</sys:Double>
                            <sys:Double>163</sys:Double>
                            <sys:Double>165</sys:Double>
                            <sys:Double>166</sys:Double>
                            <sys:Double>168</sys:Double>
                            <sys:Double>171</sys:Double>
                            <sys:Double>174</sys:Double>
                            <sys:Double>178</sys:Double>
                            <sys:Double>185</sys:Double>
                            <sys:Double>196</sys:Double>
                        </collections:AvaloniaList>
                    </arc:RidgelineSeries.Values>
                </arc:RidgelineSeries>
                <arc:RidgelineSeries Id="ap" Label="APAC">
                    <arc:RidgelineSeries.Values>
                        <collections:AvaloniaList x:TypeArguments="sys:Double">
                            <sys:Double>178</sys:Double>
                            <sys:Double>182</sys:Double>
                            <sys:Double>186</sys:Double>
                            <sys:Double>189</sys:Double>
                            <sys:Double>192</sys:Double>
                            <sys:Double>195</sys:Double>
                            <sys:Double>197</sys:Double>
                            <sys:Double>201</sys:Double>
                            <sys:Double>204</sys:Double>
                            <sys:Double>207</sys:Double>
                            <sys:Double>212</sys:Double>
                            <sys:Double>236</sys:Double>
                            <sys:Double>248</sys:Double>
                            <sys:Double>255</sys:Double>
                            <sys:Double>259</sys:Double>
                            <sys:Double>262</sys:Double>
                            <sys:Double>268</sys:Double>
                            <sys:Double>274</sys:Double>
                            <sys:Double>281</sys:Double>
                        </collections:AvaloniaList>
                    </arc:RidgelineSeries.Values>
                </arc:RidgelineSeries>
            </arc:Ridgeline.Items>
        </arc:Ridgeline>
    </StackPanel>
</Border>
```

说明文字写明了时段与结论，因此不悬停也能读懂卡片。定义域固定为 0 到 400 ms，页面展示的每一周都相同。山脊按中位数着色，因此坐标轴下方的中位数色阶解释了颜色。截图中没有抬起任何山脊：只有悬停或使用键盘时才会抬起。

### 文案规范

- **图表标签**：度量指标与范围：“按地区划分的响应时间”“苏黎世每日最高气温”。标签为辅助技术和摘要命名图表。
- **行标签**：组的简短名称，例如“EU”“APAC”或“1 月”。槽位大约能容纳 40 px 的文字，较长的名称会以省略号截断。
- **单位**：紧跟数字、中间没有空格的符号，例如“°C”或“ms”。刻度与提示使用相同的单位。
- **卡片说明**：用文字写出结论，数字使用阿拉伯数字，例如“亚太地区在 250 ms 附近有第二组数据。”

| 不要写 | 改为 |
|---|---|
| 分布 | 9 月 4 日至 9 月 10 日各地区的响应时间 |
| 第 1 组 | 欧洲 |
| 数值 | 每次请求的采样数 |
| 亚洲有问题 | 亚太地区在 250 ms 附近有第二组数据 |

### 最佳实践

**推荐**

- 在读者会切换数据集的页面上，保持固定的 `DomainMinimum` 与 `DomainMaximum`，使坐标轴不会在读者眼前移动。
- 让每组至少有约二十个观测值，使每条山脊都有真实的形状；组样本较少时需明确说明。
- 将组数保持在约二十组以内，并按有意义的顺序排列，例如按中位数或按地区，使这一叠山脊能被读作比较。
- 当中位数排名很重要时使用 `IsTinted`；各组地位相同时关闭它。

**避免**

- 将预先分箱的直方图作为观测值传入。密度是从原始数值计算的，分箱后的计数会产生失真的曲线。
- 把山脊的高度读作计数。高度反映的是共享刻度上的集中程度，而不是数值的数量。
- 用山脊图展示每组的单一平均值。只有一个数字时，柱状图或表格更清楚。
- 同一数据集中出现重复的 id。同 id 的第二组会被丢弃，因此它的数据会从图表中缺失。

### 常见问题

**为什么某条山脊看起来太波浪或太平？** 默认带宽来自该组的 Silverman 规则。对于小样本组，它可能过于粗糙或过于平滑。用数值单位设置 `Bandwidth`，即可让所有组共享同一种平滑度，并对照原始数值检查结果。

**为什么无法用 Tab 切换到图表？** 只有存在观测值时，图表才会获得焦点。空图表会显示其 `EmptyLabel`（默认为“暂无数据。”），并在数据到达之前被 Tab 跳过。

**为什么最高的山脊在各组之间高度相同？** 所有组共享一个刻度，所有组中最高的峰会升到重叠高度。山脊的高度与其密度的集中程度相称，因此只应在同一图表内比较高度。

## API 参考

<!-- api: Ridgeline -->

<!-- api: RidgelineSeries -->

## 无障碍

- `Label` 为图表命名。摘要会列出每一组。绘图区是可聚焦的分组。
- 上、下方向键抬起前一条或后一条山脊。左、右方向键以一个坐标轴刻度步长移动读数光标。Home 与 End 分别抬起第一条与最后一条山脊，Escape 放下山脊并清除读数。
- 触摸按下会抬起手指所在的山脊，并读取该处的数值。
- 礼貌的实时区域会播报被抬起山脊的中位数、中间一半的范围、取值区间与数量。
- 隐藏的数据表列出每组的中位数、四分位数、极值与数量。每一行都有文字标签，因此并非只能通过颜色读取中位数。

### 键盘

| 按键 | 操作 |
|---|---|
| `Tab` / `Shift+Tab` | 移入或移出图表。没有观测值时跳过该图表 |
| `Up` | 抬起前一条山脊。处于静止状态时，抬起第一条山脊 |
| `Down` | 抬起后一条山脊。处于静止状态时，抬起第一条山脊 |
| `Left` / `Right` | 沿数值方向将读数光标移动一个坐标轴刻度步长。处于静止状态时，抬起第一条山脊 |
| `Home` / `End` | 抬起第一条或最后一条山脊 |
| `Escape` | 放下被抬起的山脊并清除读数 |

## 动效

- 数据集或定义域变化时，每条曲线都会在共享网格上重新采样，并对每个点进行插值。山脊以相同的平滑弹簧膨胀、滑动并分裂。
- 首次显示时，山脊从顶部开始逐行滚入。
- 被抬起的山脊会以副本的形式绘制在最上层。它上升 6 px，中间一半会加上阴影，并标出中位数。提示跟随指针移动，并保持在图表之内。
- 减弱动效时直接绘制最终状态。关闭动效时的表现与减弱动效相同。

| 变化 | 完整 | 减弱 | 关闭 |
|---|---|---|---|
| 数据集或定义域变化 | 每条曲线以 0.7 s 的弹簧变形；山脊膨胀、滑动并分裂 | 直接绘制最终状态 | 与减弱动效相同 |
| 首次显示 | 山脊从顶部开始逐行滚入 | 直接绘制最终状态 | 与减弱动效相同 |

## 相关组件

- `BarChart`：每组只有一个汇总数值时使用。
- `LineChart`：单一数值如何随时间变化。
- `Streamgraph`：多个层的总量如何随时间变化。
- `BrushChart`：单一的高密度时间序列。

| 组件 | 适用场景 |
|---|---|
| `BarChart` | 每组有一个可精确比较的汇总数值 |
| `LineChart` | 横轴为时间，读者需要追踪某个数值随时间的变化 |
| `Streamgraph` | 多个层应被读作随时间变化的堆叠总量的各部分 |
| `BrushChart` | 一条高密度时间序列需要缩放并选取范围 |
| `DonutChart` | 数据是一个整体的几个部分 |
| `ActivityHeatmap` | 数据是按天或按小时的计数网格 |
