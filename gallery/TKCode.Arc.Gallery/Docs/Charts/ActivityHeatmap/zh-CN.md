---
title: 活动热力图
summary: 一眼看清一整年的活动，每天一个方块。
source-hash: d373449b219c
---

<Demo name="Contributions" />

## 何时使用

- 长时间段内的每日节奏，例如一年中的贡献或训练记录。
- 连续性和间断比精确比较更重要：连续天数与空闲周才是重点。
- 日历形态的总览，每一天都是一个可读取、可选择的方块。

## 何时不用

- 需要精确比较各天数值：用 `BarChart`。
- 为输入挑选日期：用 `Calendar`。
- 观察某个数值随时间的趋势：用 `LineChart`。

## 用法

每个 `ActivityDay` 是一个方块，包含 `Date` 和 `Count`。缺失的日期按零计算。`Period` 用于补全摘要，例如“2025 年 1,284 次贡献”。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:ActivityHeatmap Label="2025 年的贡献"
                         Period="2025"
                         ItemsSource="{Binding Days}" />
</UserControl>
```

### 年份与控件

切换年份时，请同时替换 `ItemsSource`、`Period` 和 `Label`。`Actions` 会把控件放在摘要旁边，例如年份切换按钮。网格以扫过的方式重新着色，而不是整体重绘。

```xml
<arc:ActivityHeatmap Label="2025 年的贡献" Period="2025" ItemsSource="{Binding Days}">
    <arc:ActivityHeatmap.Actions>
        <StackPanel Orientation="Horizontal" Spacing="4">
            <arc:Button Content="2024" Variant="Ghost" Size="Small" Command="{Binding ShowPrevious}" />
            <arc:Button Content="2025" Variant="Ghost" Size="Small" Command="{Binding ShowCurrent}" />
        </StackPanel>
    </arc:ActivityHeatmap.Actions>
</arc:ActivityHeatmap>
```

### 单位与周

`UnitOne` 和 `UnitOther` 设置计数的单位名词。`FirstDayOfWeek` 设置每个周列的第一行，默认为周日。`Culture` 用于格式化日期和数字。

<Demo name="CustomUnit" />

```xml
<arc:ActivityHeatmap Label="训练次数，最近六个月"
                     Period="最近六个月"
                     UnitOne="次训练"
                     UnitOther="次训练"
                     FirstDayOfWeek="Monday"
                     MaxWidth="520"
                     ItemsSource="{Binding Workouts}" />
```

### 阈值

`Thresholds` 保存第一到第三级的上限。超过最后一个上限的计数为第四级。默认情况下，上限取最忙那一天的四分位数。

```csharp
workouts.Thresholds = [1, 2, 3];
```

### 选择某一天

`SelectedDate` 为当前选中的日期，支持双向绑定。`DateSelected` 会在用户通过点击、Enter 或 Space 选中某天时报告。

```csharp
heatmap.DateSelected += (_, e) => selected.Text =
    $"已选择 {e.Date:MMM d, yyyy}：{e.Count} 次贡献（第 {e.Level} 级）";
```

## API 参考

<!-- api: ActivityHeatmap -->

<!-- api: ActivityDay -->

## 无障碍

- `Label` 为网格命名，例如“2025 年的贡献”。摘要会以总数和时间段补全名称。
- 上下方向键每次移动一天，左右方向键每次移动一周。Home 和 End 分别跳到第一天和最后一天，Enter 或 Space 选中当前焦点所在的日期。
- Esc 隐藏提示。单个提示框会在各天之间平滑移动，而不是为每个方块重复显示。
- 每一天都是一个带名称的网格单元。单元格作为图形对辅助技术隐藏，信息由摘要和网格结构承载。
- 总数会通过状态区域播报，指针或焦点所在的日期会以礼貌方式播报。图例是一排开关，每个级别一个，因此级别不只靠颜色区分。

## 动效

- 首次显示时，方块依次波浪式入场一次，总时长有上限，因此大型网格也能保持迅速。
- 切换年份或范围时，网格以对角扫过的方式重新着色，方块按列错峰进行。
- 提示框以灵敏的弹簧在方块之间滑动，文字滚动切换到新的日期。
- 总数以平滑弹簧计数到新值。
- 减弱动效时，移除波浪、扫过和滑动效果，改用 160 毫秒的短淡入。关闭动效时直接到最终状态。

## 相关组件

- `BarChart`：用于精确比较不同时段。
- `Calendar`：用于选择日期。
- `LineChart`：用于观察数值随时间的趋势。
- `Sparkline`：用于数值旁边的紧凑趋势。
