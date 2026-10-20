---
title: 华夫图
summary: 十乘十的单位图表，每个方格代表百分之一；数据变化时，方格会飞到各自的新分组。
source-hash: cec4f2f81214
---

## 何时使用

- 需要让人数清楚的占比，例如答案占比或以百分比表示的能源结构。
- 在两组数据之间比较同一批类别，原地不动的方格说明了哪些发生了变化。
- 占比很小却必须保持可见的场景；饼图或环形图只会显示一条细线。

## 何时不用

- 占比随时间变化：用 `LineChart` 或 `Streamgraph`。
- 类别很多且需要精确比较：用 `BarChart`。
- 嵌套整体中需要下钻的某一部分：用 `Treemap`。

## 用法

每个 `WaffleCategory` 是一组方格。数值为权重：方格按其比例分配。第一个类别从左下角开始填充，逐列进行。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:WaffleChart Label="发电量"
                     Unit="TWh"
                     ItemsSource="{Binding Mix}" />
</UserControl>
```

也可以通过 `Items` 直接添加类别。

```csharp
var neutral = new WaffleChart { Label = "浏览器占比", AccentKey = WaffleChart.NoAccent };
neutral.Items.Add(new WaffleCategory("chrome", "Chrome", 65.4));
neutral.Items.Add(new WaffleCategory("safari", "Safari", 18.6));
neutral.Items.Add(new WaffleCategory("other", "其他", 16.0));
```

### 数据集

替换 `ItemsSource` 即可切换数据集。保留所在分组的方格原地不动，其余方格飞到各自的新区块。

```csharp
energy.ItemsSource = Mix2015;
```

### 选择器与非类别数据

当数据源不是 `WaffleCategory` 时，设置 `KeySelector` 与 `ValueSelector`。`LabelSelector` 读取每个项的标签，`Decimals` 设置占比显示的小数位数。

```csharp
var survey = new WaffleChart
{
    Label = "问卷回答",
    ItemsSource = answers,
    KeySelector = item => ((Source)item).Name,
    ValueSelector = item => ((Source)item).Weight,
    Decimals = 1,
    MaxWidth = 300,
    IsLegendVisible = false
};
```

### 网格与强调色

`Rows` 与 `Columns` 设置网格，默认为十乘十。`AccentKey` 指定使用第一个系列颜色的类别；其他类别依次使用次要颜色，然后是中性色调。将 `AccentKey` 设为 `NoAccent`，则只使用中性色调。

```xml
<arc:WaffleChart Label="配额使用情况" Rows="5" Columns="20" AccentKey="used" ItemsSource="{Binding Quota}" />
```

### 固定类别

固定图例中的某一行后，其方格会保持高亮。`ActiveKey` 保存被固定的键，且为双向绑定。

```csharp
energy.ActiveKeyChanged += (_, e) => pinned.Text = $"已固定：{e.Category?.Label ?? "无"}";
```

## API 参考

<!-- api: WaffleChart -->

<!-- api: WaffleCategory -->

## 无障碍

- `Label` 为图表命名。网格是一个可聚焦的分组，带有角色描述以及按键说明。
- Tab 键先将焦点移到网格，然后依次移到每个图例行。每个图例行都是带文字名称的切换按钮。
- 在网格中，方向键每次将读数移动一个方格。PageUp 与 PageDown 跳到上一个或下一个类别的第一个方格；Home 与 End 分别跳到第一个和最后一个已填充的方格。Escape 清除读数。
- 在图例行上按 Enter 或空格键，可固定或释放其类别。
- 礼貌型实时区域会朗读读数下方的类别、占比与数值。一个隐藏的数据表列出每个类别的数值、占比与方格数量。
- 方格对辅助技术隐藏。屏幕上会缩短较长的图例名称，但完整名称保留在工具提示、实时区域和数据表中。

## 动效

- 方格尽量保留原位。其余方格以错落的波浪形式飞到新区块，飞行中的方格会略微缩小并改变颜色。
- 图例数值以临界阻尼弹簧平滑计数到新值。
- 首次显示时，方格从左下角依次出现。
- 减弱动效时，方格与占比立即定位到位，并取消缩小、出现与错落的效果。关闭动效的效果与减弱动效相同。

## 相关组件

- `DonutChart`：单个环形中的占比，中心显示总计。
- `BarChart`：类别之间的精确比较。
- `Progress`：单项任务的完成进度；`UsageMeter`：配额与上限的对比。
- `Treemap`：类别呈嵌套结构且需要下钻时使用。
