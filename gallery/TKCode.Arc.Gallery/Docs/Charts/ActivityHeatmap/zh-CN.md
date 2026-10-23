---
title: 活动热力图
summary: 一眼看清一整年的活动，每天一个方块。
source-hash: 96bdcf76df9d
---

<Demo name="Contributions" />

## 何时使用

`ActivityHeatmap` 展示一个计数在各天中的分布，每天一个方块，以周为列。它回答的是节奏与连续性的问题：团队是否每周都在发布，八月的沉寂持续了多久，哪些工作日比较忙。它适合放在仪表板面板或个人资料页中，此时整个周期比任何单个数值都更重要。

- **开发者控制台中的部署与发布频率**：平台团队按天查看最近一个季度的生产部署。连续与空档是重点，没有部署的那一天应被看作空档，而不是缺失的数据。
- **SaaS 管理仪表板上的参与度**：某个工作区每天的活跃用户数或完成的入门步骤数，与该账号的套餐放在一起，让团队看出全年的使用量是否增长。
- **消费类应用中的训练、学习或阅读**：个人资料页上每天一个方块，连续记录一目了然。图例无需单独的图表就能说明五个等级。
- **运维工具中的事件或工单量**：一个季度内每天新开的工单数。繁忙的一周会很醒目，选中的那一天还能打开当天的工单列表。
- **内部工具中的活动审计**：合规审查中每天的登录或审批次数，审查人员先选中某一天，读出其计数，再打开详情视图。

## 何时不用

- **精确比较不同天或不同时段**：用 `BarChart`，每根柱子的长度可以被精确比较。
- **为输入选择日期**：用 `Calendar` 选择预订、到期日或报表区间等由人挑选的日期。`ActivityHeatmap` 是为了查看而选中某一天，而不是用于数据输入。
- **某个值随时间的趋势**：当连续值的形态很重要（例如收入或延迟），并且每个点的确切值很关键时，用 `LineChart`。
- **单个数字旁边的紧凑趋势**：用 `Sparkline`，它小到可以放进表格行或 KPI 卡片中，展示的是数值的方向，而不是每一天。
- **不足几周的周期**：只有寥寥几天时，方块太少，无法呈现规律，带标签数值的 `BarChart` 更易读。
- **并排展示多年数据**：跨两年的网格宽度会翻倍，并需要横向滚动。请每年使用一个 `ActivityHeatmap`，或用 `BarChart` 比较各年的总计。

## 用法

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:ActivityHeatmap Label="2025 年的贡献"
                         Period="2025"
                         ItemsSource="{Binding Days}" />
</UserControl>
```

`ActivityHeatmap` 派生自 Avalonia 的 `Control`，而不是项控件（items control）。它自行绘制摘要行、月份与星期标签、日期网格、提示框和图例，因此一个控件就包含整个日历，无需任何项模板。由于网格是一个可聚焦的控件，并通过算术方式进行命中测试，它处理一整年约 370 个方块时，无需为每个方块创建可视对象。

### 结构

1. **摘要行**：总计数以动画数字显示，后接单位名词，再接“在”和周期。总数以 14 px 的正文字号呈现，数据变化时滚动到新值。`Period` 为空时会隐藏“在”和周期。
2. **操作**（`Actions`）：任意控件，放在摘要行的右端。当摘要与操作无法并排放下时，操作会移到摘要下方自己的一行。
3. **星期列**：网格左侧 30 px 宽的星期标签列。网格较窄需要横向滚动时，它保持在原位。
4. **月份标签**：每个月第一周上方的月份缩写，按 `Culture` 格式化。如果下一个月在三周之内开始，首个不完整的月份会失去标签，因此标签不会相互重叠。
5. **日期网格**：每个日历日一个方块，以周为列。方块之间相距 3 px。方块大小随容器宽度变化，保持在 9 px 到 15 px 之间。数据范围之外的日期为空单元格。
6. **提示框**：整个网格共用一个气泡，显示指针或键盘焦点所在那一天的计数和日期。
7. **图例**：一段说明文字，指出处于高亮等级的天数，然后是“较少”、五个色块（等级 0 到 4）和“较多”。悬停或聚焦某个色块会在网格上预览其等级，点击则固定该等级，高亮该等级的方块。再次点击即可取消固定。

头部、网格与图例之间相隔 16 px。五种色调由强调色推导：等级 0 是前景色以 6.5% 混入表面色，等级 1、2、3 分别是强调色以 26%、50%、76% 混入该底色。等级 4 为完整的强调色。

### 切换年份与控件

切换年份时，请同时替换 `ItemsSource`、`Period` 和 `Label`，使摘要、网格以及网格的无障碍名称保持一致。`Actions` 把控件放在摘要旁，例如年份切换。网格通过扫过式的重新着色来更新，而不是整体重绘，因此切换年份时页面保持平稳。

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

网格始终跨越数据中最早到最晚的日期。查看单一年份时，只传入一个自然年的数据。如果数据包含两年，网格宽度会翻倍并需要横向滚动，这通常不是读者想要的效果，因此应按年份拆分数据。

### 单位与周

`UnitOne` 与 `UnitOther` 设置计数的单数与复数名词。只有当总数恰好为 1 时才使用单数，因此“1 workout”和“2 workouts”都读起来正确。`FirstDayOfWeek` 设置每个周列的第一行，默认为星期日；工作日历可以选择星期一。`Culture` 用于格式化日期和数字，默认为 en-US。

<Demo name="CustomUnit" />

```xml
<arc:ActivityHeatmap Label="最近六个月的训练"
                     Period="最近六个月"
                     UnitOne="workout"
                     UnitOther="workouts"
                     FirstDayOfWeek="Monday"
                     MaxWidth="520"
                     ItemsSource="{Binding Workouts}" />
```

`MaxWidth` 让六个月的网格不会在宽页面上被拉伸。方块最大不超过 15 px，因此容器很宽时，右侧会留出空白。

### 阈值

`Thresholds` 保存等级一到三的上限值；只使用前三个值。计数为零时为等级 0，无色调；计数不超过第一个上限为等级 1，不超过第二个为等级 2，不超过第三个为等级 3，超过第三个上限则为等级 4。

未设置 `Thresholds` 时，控件会根据数据范围内最忙的那一天推导上限：第一个上限为该天计数的四分之一（向上取整，至少为 1），第二个为其一半（至少为 2），第三个为其四分之三（至少为 3）。因此，某年有一天特别突出、部署了 31 次，上限就会设为 8、16 和 24，而大多数普通日子都落在等级 1。当读者要比较不同年份或团队时，应设置固定上限，因为推导出的上限会随每组数据变化。

```csharp
workouts.Thresholds = [1, 2, 3];
```

设置 `Thresholds` 也会固定图例的范围。`EffectiveThresholds` 返回正在使用的三个上限。

### 选择某一天

`SelectedDate` 是选中的日期，并且是双向的。选择环会滑动到该日期，并在首次落在该日期时淡入。`DateSelected` 报告通过点击、Enter 或 Space 选择的日期。其参数包含 `Date`、该日的 `Count`，以及从 0（无）到 4（最忙）的 `Level`，因此处理程序无需重新查询数据，即可显示摘要或打开详情视图。

```csharp
heatmap.DateSelected += (_, e) => selected.Text =
    $"已选择 {e.Date:MMM d, yyyy}：{e.Count} 次贡献（等级 {e.Level}）";
```

在代码中设置 `SelectedDate` 会移动选择环，但不会触发 `DateSelected`，因为该事件报告的是人所做的选择。恢复选择时（例如页面重新打开），使用该属性；需要对选择作出反应时，使用事件。

### 绑定到视图模型

视图模型拥有日期、周期和选中日期。使用 CommunityToolkit.Mvvm 风格的属性，并将热力图绑定到它们。

```csharp
public sealed record DailyDeploys(DateOnly Date, double Deploys);

public interface IDeployHistory
{
    Task<IReadOnlyList<DailyDeploys>> DailyCountsAsync(int year);
}

public partial class ActivityViewModel : ObservableObject
{
    private readonly IDeployHistory _history;

    public ActivityViewModel(IDeployHistory history) => _history = history;

    [ObservableProperty]
    private ObservableCollection<ActivityDay> _days = [];

    [ObservableProperty]
    private string _periodLabel = "过去一年";

    [ObservableProperty]
    private DateOnly? _selectedDate;

    [ObservableProperty]
    private string _selectedSummary = "选择一天以查看其部署次数。";

    [RelayCommand]
    private async Task ShowYearAsync(string year)
    {
        var counts = await _history.DailyCountsAsync(int.Parse(year));
        Days = new ObservableCollection<ActivityDay>(
            counts.Select(c => new ActivityDay(c.Date, c.Deploys)));
        PeriodLabel = year;
    }

    partial void OnSelectedDateChanged(DateOnly? value)
    {
        SelectedSummary = value is { } date
            ? $"{date:MMM d, yyyy} 已选中。"
            : "选择一天以查看其部署次数。";
    }
}
```

```xml
<StackPanel Width="620" Spacing="16">
    <arc:ActivityHeatmap Label="生产环境部署"
                         Period="{Binding PeriodLabel}"
                         UnitOne="deploy"
                         UnitOther="deploys"
                         ItemsSource="{Binding Days}"
                         SelectedDate="{Binding SelectedDate, Mode=TwoWay}">
        <arc:ActivityHeatmap.Actions>
            <arc:Button Content="2025" Variant="Ghost" Size="Small"
                        Command="{Binding ShowYearCommand}" CommandParameter="2025" />
        </arc:ActivityHeatmap.Actions>
    </arc:ActivityHeatmap>
    <TextBlock Text="{Binding SelectedSummary}"
               Foreground="{arc:ArcDesignTokenResource TextMuted}" />
</StackPanel>
```

- `ItemsSource` 是单向的：热力图读取集合并重建网格。`ObservableCollection` 会被监视，因此对它的修改会立即反映出来。像命令那样赋值一个新集合，是刷新整年数据最简单的方法。
- `SelectedDate` 是双向的。在网格上所做的选择会更新视图模型，而视图模型设置的值会移动选择环，但不会触发 `DateSelected`。
- `Period` 与 `Label` 是普通属性；请与 `ItemsSource` 在同一次更新中绑定，使摘要和网格的名称永远不会描述不同的数据。
- `DateSelected` 是事件，而不是命令。请在代码后置中附加它，让处理程序读取事件参数中的 `Date`、`Count` 和 `Level`，并把这些值复制到视图模型自己的属性中。

### 数据结构与性能

`ActivityDay` 包含一个 `DateOnly` 和一个 `double` 类型的计数。控件会在最早与最晚日期之间为每个日历日构建一条连续的记录，因此缺失的日期视为零，无需行数据。同一日期的两条数据会相加，负计数视为零。一年约 370 个方块，网格会一次绘制完成；跨多年的范围会成倍增加列数并需要横向滚动。

当数据源不是 `ActivityDay` 时，可用 `DateSelector` 与 `CountSelector` 读取每一项，或用 `DateMemberBinding` 与 `CountMemberBinding`。成员绑定读取 `DateOnly`、`DateTime` 或 `yyyy-MM-dd` 格式的字符串。两者同时设置时，`DateSelector` 优先于 `DateMemberBinding`。

```csharp
var heatmap = new ActivityHeatmap
{
    ItemsSource = rows,
    DateSelector = row => DateOnly.ParseExact(((Row)row).Day, "yyyy-MM-dd", CultureInfo.InvariantCulture),
    CountSelector = row => ((Row)row).Deploys
};
```

当日期或数字必须跟随读者的语言时，设置 `Culture`。总计使用该区域性下的 `#,0.##` 格式，因此小数计数最多保留两位小数，较大的总数会使用本地的千位分隔符。

更新数据时，替换集合，或修改热力图所监视的集合中的项。日历是对天数的汇总，因此应按数据变化的节奏安排刷新，例如每次页面加载或每小时一次，而不是每个事件都刷新。

### 示例：平台团队的部署频率

平台团队过去五周的部署记录，显示在工程概览页上。标签说明了度量与周期，单位名词读作“deploys”，每周从星期一开始，即团队工作周的第一天。导出按钮作为操作放在摘要旁。周末大多比较安静，因此无需额外标签，也能看出工作日的节奏。

![过去五周生产部署的热力图，周末较为安静](images/DeployHeatmap.png)

```xml
<StackPanel Width="560" Spacing="16">
    <arc:ActivityHeatmap Label="Production deploys, last five weeks"
                         Period="the last five weeks"
                         UnitOne="deploy"
                         UnitOther="deploys"
                         FirstDayOfWeek="Monday">
        <arc:ActivityHeatmap.Items>
            <arc:ActivityDay Date="2026-09-14" Count="4" />
            <arc:ActivityDay Date="2026-09-15" Count="5" />
            <arc:ActivityDay Date="2026-09-16" Count="3" />
            <arc:ActivityDay Date="2026-09-17" Count="6" />
            <arc:ActivityDay Date="2026-09-18" Count="4" />
            <arc:ActivityDay Date="2026-09-19" Count="0" />
            <arc:ActivityDay Date="2026-09-20" Count="1" />
            <arc:ActivityDay Date="2026-09-21" Count="3" />
            <arc:ActivityDay Date="2026-09-22" Count="5" />
            <arc:ActivityDay Date="2026-09-23" Count="0" />
            <arc:ActivityDay Date="2026-09-24" Count="4" />
            <arc:ActivityDay Date="2026-09-25" Count="2" />
            <arc:ActivityDay Date="2026-09-26" Count="0" />
            <arc:ActivityDay Date="2026-09-27" Count="0" />
            <arc:ActivityDay Date="2026-09-28" Count="5" />
            <arc:ActivityDay Date="2026-09-29" Count="6" />
            <arc:ActivityDay Date="2026-09-30" Count="4" />
            <arc:ActivityDay Date="2026-10-01" Count="5" />
            <arc:ActivityDay Date="2026-10-02" Count="3" />
            <arc:ActivityDay Date="2026-10-03" Count="1" />
            <arc:ActivityDay Date="2026-10-04" Count="0" />
            <arc:ActivityDay Date="2026-10-05" Count="4" />
            <arc:ActivityDay Date="2026-10-06" Count="2" />
            <arc:ActivityDay Date="2026-10-07" Count="5" />
            <arc:ActivityDay Date="2026-10-08" Count="6" />
            <arc:ActivityDay Date="2026-10-09" Count="4" />
            <arc:ActivityDay Date="2026-10-10" Count="0" />
            <arc:ActivityDay Date="2026-10-11" Count="1" />
            <arc:ActivityDay Date="2026-10-12" Count="3" />
            <arc:ActivityDay Date="2026-10-13" Count="4" />
            <arc:ActivityDay Date="2026-10-14" Count="2" />
            <arc:ActivityDay Date="2026-10-15" Count="5" />
            <arc:ActivityDay Date="2026-10-16" Count="3" />
            <arc:ActivityDay Date="2026-10-17" Count="0" />
            <arc:ActivityDay Date="2026-10-18" Count="0" />
        </arc:ActivityHeatmap.Items>
        <arc:ActivityHeatmap.Actions>
            <arc:Button Content="Export" Variant="Ghost" Size="Small" />
        </arc:ActivityHeatmap.Actions>
    </arc:ActivityHeatmap>
</StackPanel>
```

### 文案规范

- **标签**：用句子式大小写说明度量与周期，例如“生产环境部署，最近五周”。标签是网格的无障碍名称，因此即使没有图片也必须说得通。
- **周期**：用一个简短短语补全“……中的贡献”：“2025”“最近六个月”“2026 年第三季度”。只有当标签已经说明周期时才可省略。
- **单位**：将 `UnitOne` 与 `UnitOther` 设为人们描述该计数时使用的名词，例如“部署”或“训练”。当计数有精确含义时，避免使用“项”这类泛称。
- **阈值**：设置阈值时，在说明文字或注释中加以描述，例如“颜色越深表示部署越多”，让读者知道色调是相对于上限而言的。
- **操作**：保持一到两个小按钮，使用简短的动词或年份，例如“导出”或“2025”。

| 不要写 | 改为 |
|---|---|
| 活动数据 | 生产环境部署，最近五周 |
| 2025 | 2025 年的贡献 |
| 1 contributions | 1 contribution（名词随总数单复数变化） |
| 热力图，内容 | 最近六个月的训练 |
| 点击此处查看详情 | 选择一天以查看其部署次数 |

### 最佳实践

**推荐**

- 为每个热力图设置 `Label` 和 `Period`。标签是屏幕阅读器为网格提供的唯一名称，周期则补全摘要，让读者永远看不到孤零零的总数。
- 每个视图都传入完整的自然年或固定范围。从随机日期开始的网格会让两年的数据难以比较。
- 比较不同时段或团队时设置 `Thresholds`。推导出的上限会跟随最忙的那一天，因此同样的计数在一年中可能是等级 2，在另一年中却是等级 1。
- 将 `FirstDayOfWeek` 设为符合受众日历的值：工作周用星期一，个人活动用星期日。
- 用 `DateSelected` 打开某一天的详情。该事件携带日期、计数和等级，处理程序无需再次查找该日数据。

**避免**

- 用热力图展示精确数值。方块展示的是色调而非数字，需要精确计数的读者必须选中该日，或查看表格。
- 在一个热力图中传入多年数据。网格会变得很宽，读者必须横向滚动才能看到最近几周。
- 为色调赋予好坏的含义。等级只表示某天的繁忙程度，单一的强调色并不是状态信号，因此应在标签或说明文字中解释其含义。
- 用比数据变化还短的计时器刷新数据。总数和色调每天变化，频繁更新只会让页面晃动而不增加信息。
- 将热力图放在狭窄的列中。宽度不超过 440 px 时，图例说明只显示计数；方块达到 9 px 的最小值后，网格会从最新一周开始横向滚动，因此应给它面板的全部宽度。

### 常见问题

**为什么网格从一周的中间开始？** 网格从数据中最早的日期开始，并从 `FirstDayOfWeek` 开始填充第一个周列。该列中开始日期之前的日子为空，因此第一个方块可能位于任意一行。

**为什么我的大多数日子都是同一种浅色？** 默认的上限是最忙那一天的四分之一等分。一个异常值会抬高上限，因此大多数普通日子都落在等级 1。请将 `Thresholds` 设为与典型范围相符的固定值。

**为什么设置 `SelectedDate` 不会触发我的处理程序？** `DateSelected` 只在通过点击、Enter 或 Space 做出选择时触发。设置该属性只会移动选择环，而不会引发事件，因此需要时请在 setter 或视图模型中更新你自己的状态。

**如何显示哪些天处于某一等级？** 图例已经提供了这一功能。悬停或聚焦某个色块可预览其等级，点击色块则将其固定。此时说明文字会统计该等级的天数，例如“96 天”；当图例宽度超过 440 px 时，还会附上该等级的范围，并且对应的方块会被突出显示。

## API 参考

<!-- api: ActivityHeatmap -->

<!-- api: ActivityDay -->

## 无障碍

- `Label` 为网格命名，例如“2025 年的贡献”。摘要用总数和周期补全名称。
- 上下方向键每次移动一天，左右方向键每次移动一周。Home 与 End 移到第一天和最后一天，Enter 或 Space 选中当前聚焦的那一天。
- Escape 隐藏提示框。单个提示框在各天之间平滑滑动，不会为每个方块重复播报。
- 每一天都是一个有名称的网格单元格。这些单元格作为图形对辅助技术隐藏，信息由摘要与网格结构承载。
- 总数会在状态区域中播报，指针或焦点所在的那一天会以礼貌方式播报。图例显示一段说明，给出处于高亮等级的天数，因此不会仅凭颜色来识别等级。
- 聚焦的那一天会被读作其计数、星期和日期，例如“31 次贡献，星期三，2025 年 12 月 31 日”。
- 摘要是整个控件的帮助文本，因此打开控件描述的读者会先听到总数和周期。

### 键盘

网格作为一个 Tab 停靠点获得焦点。通过键盘获得焦点时，首先聚焦的是最新的那一天。

| 按键 | 操作 |
|---|---|
| `Tab` / `Shift+Tab` | 将焦点移入网格并定位到最新的一天，然后移到图例或下一个控件 |
| `Up` / `Down` | 向前或向后移动一天 |
| `Left` / `Right` | 向前或向后移动一周 |
| `Home` / `End` | 移到范围内的第一天或最后一天 |
| `Enter` / `Space` | 选中聚焦的那一天并引发 `DateSelected` |
| `Escape` | 隐藏提示框 |

图例是第二个 Tab 停靠点。`Left` 与 `Right` 在五个色块之间移动并预览其等级，`Home` 与 `End` 移到第一个和最后一个色块，`Enter` 或 `Space` 固定一个等级，`Escape` 清除预览。在已固定的等级上再次按下 `Enter` 或 `Space` 即可取消固定。`Escape` 清除预览但保留固定。

## 动效

- 首次显示时，方块会一次性波浪式浮现，总时长有上限，因此大网格仍然很快。
- 新的年份或范围会以对角扫过的方式重新着色，各方块按列错开时间。
- 提示框以灵敏的弹簧在方块之间滑动，其文字也滚动到新的那一天。
- 总数以平滑弹簧计数到新值。
- 减弱动效会去掉波浪、扫过和滑动，改用 160 ms 的短淡入。关闭动效则直接跳到最终状态。

| 变化 | 完整 | 减弱 | 关闭 |
|---|---|---|---|
| 首次显示 | 方块一次性波浪式浮现，总时长有上限 | 160 ms 的短淡入 | 立即完成 |
| 新范围或年份 | 对角扫过，按列错开 | 160 ms 的短淡入 | 立即完成 |
| 提示框移到另一天 | 以灵敏弹簧在方块之间滑动 | 160 ms 的短淡入 | 立即完成 |

扫过效果只保留给范围变化，例如切换年份。波浪只在首次显示时运行一次，因此重新加载同一年份的页面不会从中获得任何收益。

## 相关组件

- `BarChart` 用于不同时段之间的精确比较。
- `Calendar` 用于选择日期。
- `LineChart` 用于展示某个值随时间的趋势。
- `Sparkline` 用于在数值旁边展示紧凑的趋势。

| 组件 | 适用场景 |
|---|---|
| `BarChart` | 读者需要比较不同天、周或类别之间的精确值 |
| `Calendar` | 人需要为预订或到期日等输入选择日期 |
| `LineChart` | 连续值随时间的变化比单日的节奏更重要 |
| `Sparkline` | 在表格行或 KPI 卡片中，数字旁边需要一个小趋势 |
| `DatePicker` | 表单字段需要一个单独的日期，而不是活动的视图 |
