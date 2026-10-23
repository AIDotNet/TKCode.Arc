---
title: 日历
summary: 以清晰紧凑的月视图浏览日期。
source-hash: 63a0570276ad
---

<Demo name="SingleDate" />

## 何时使用

`Calendar` 在页面上显示一个月的日期，选中的日期、导航与限制始终可见。它适用于日历本身就是内容的屏幕：预订页、可用性视图，或让人在选定某一天之前比较多天的排期面板。会弹出的日期字段属于另一类工作，由 `DatePicker` 负责。

- **酒店预订页上的入住日期**：客人在酒店或租赁流程中选择入住日。整个月都留在屏幕上，客人无需打开任何东西，就能看到周末、售罄的夜晚以及预订窗口的边界。
- **诊所或服务控制台中的预约时段**：运营人员先选日期，再选时间。月视图显示哪些日期可约，然后才加载所选日期的时间列表。
- **可用性与休假规划**：内部工具中的经理为某天标记排班或休假申请。当人在编辑页面其余部分时，月视图保持可见，而弹出层会把它遮住。
- **从单一日期开始的报表与导出范围**：财务人员为一次发票批处理选择对账单日期。`MaxDate` 等限制让选择保持在账目的结算期之内。
- **以键盘为主的管理后台表单**：网格只有一个 Tab 停靠点，并支持方向键以及按月、按年翻页的 Page 键，因此从不使用鼠标的操作员也能快速浏览一个月。

## 何时不用

- **表单中的日期字段**：用 `DatePicker`。它是一个在弹出层中打开日历的字段，因此表单保持高度，标签、说明与错误行仍与其他字段对齐。
- **开始与结束日期**：用 `DateRangePicker`。它保存两个日期，提供“最近 7 天”等预设，并且只在用户确认后才应用范围。
- **一天中的时间或带时间的日期**：在 `DatePicker` 旁使用 `TimePicker`。`Calendar` 只选择一天，它的值没有时间部分。
- **以记录形式列出的日期**：发票、订单或按日期列排序的发货记录用 `DataGrid`。日历一次只显示一个月，无法列出许多记录的细节。
- **从少数已知日期中快速选取**：当选项是“本周”或“下一个账单周期”这类具名值，而不是网格中的日期时，用 `Select` 或 `RadioGroup`。

## 用法

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:Calendar ShowToday="True" />
</UserControl>
```

`Calendar` 派生自 Avalonia 的 `TemplatedControl`，因此它支持常见的布局、`IsEnabled`、`Focusable` 与样式属性。它的月份与选中动效、键盘模型以及限制规则都是本库的一部分。每个公共属性、方法与事件都列在 API 参考中。

### 结构

1. **月份标题**：显示月份与年份的名称，采用文化的年月格式（例如 `en-US` 下的 "October 2026"）。它使用基础字号与中等字重，位于标题行左侧，标题行至少 34 px 高，下方留 14 px。
2. **今天按钮**（`ShowToday`、`TodayLabel`）：带文字的幽灵胶囊按钮，高 32 px，左右各有 11 px 内边距。只有当 `ShowToday` 为 `True` 时才可见。
3. **上一月与下一月按钮**：两个 32 px 的圆形幽灵按钮，内含 16 px 的箭头图标。它们位于标题右侧，相距 2 px。会越过 `MinDate` 或 `MaxDate` 的按钮会被禁用，并以 36% 的不透明度绘制。
4. **星期行**：简短的星期名称，使用弱化文字颜色，采用超小字号与中等字重。它从 `FirstDayOfWeek` 开始，下方留 6 px。
5. **日期网格**：六行，每行七天，行间距 4 px。每一天都是小字号的 14 px 文字。六行始终存在，因此只有五周的月份会在最后一行显示相邻月份的日期。
6. **选中圆点**：位于选中日期背后、填充强调色的圆形，其数字使用强调前景色。鼠标悬停在未选中的日期上时，它会填充为表面弱化色；悬停在选中日期上时使用更醒目的强调色。
7. **焦点环**：围绕获得键盘焦点的日期的 2 px 焦点色环。

宽度为 `min(100%, 328px)`：日历永远不会超过 328 px，并在其容器中左对齐。由于高度由六行固定，日历在每个月占用的空间都相同，导航时它下方的内容也不会移动。

### 选中日期

`SelectedDate` 是 `DateOnly?`，只使用其日期部分。它默认为双向绑定，因此绑定的值会跟随用户选中的日期，代码中设置的值也会移动选择。使用 `null` 表示没有选中：此时日历没有选中的日期，其可聚焦的 Tab 停靠点会移到今天，或当月第一个可用日期。

只有用户选择某一天时才会触发 `DateSelected`：点击、在聚焦的日期上按下 Enter 或 Space，或点击“今天”按钮。从代码设置 `SelectedDate` 不会触发它。当代码变化也需要触发某个反应时，订阅绑定的属性；当反应只针对用户操作时，才订阅 `DateSelected`。

```xml
<arc:Calendar SelectedDate="{Binding StayDate, Mode=TwoWay}" />
```

```csharp
calendar.DateSelected += (_, e) => status.Text = $"已选择 {e.Date:D}";
```

事件只携带一个值：

| 成员 | 类型 | 含义 |
|---|---|---|
| `Date` | `DateOnly` | 用户选中的日期。它永远不会是禁用的日期。 |

### 限制与禁用日期

`MinDate` 与 `MaxDate` 是可选择的最早与最晚日期。`DisabledDates` 是一个 `Func<DateOnly, bool>`，对每个不能选择的日期返回 `true`，例如周末、已订满的夜晚或公共假日。只要三条规则中有一条适用，该日期即被禁用。

<Demo name="LimitsAndDisabledDays" />

这些规则在三个地方生效：被禁用的日期不能被点击，也不能通过 Enter 或 Space 激活；方向键与 Page 键永远不会落在它上面；导航按钮会停在包含限制的月份。当键盘移动会落到禁用日期上时，焦点会沿移动方向跨过它：当周六和周日被禁用时，在周五按右方向键会把焦点移到周一。

### 显示月份

`DisplayMonth` 是 `DateOnly?`，用于指定显示的月份。该月中的任意一天都可以；日历会把值规范化为该月的第一天。它是双向绑定的：当用户用按钮或键盘切换到另一个月时，`DisplayMonth` 会随之更新，因此绑定的视图模型总是知道屏幕上显示的是哪个月。

```xml
<arc:Calendar DisplayMonth="{Binding ViewMonth, Mode=TwoWay}" />
```

未设置 `DisplayMonth` 时，日历从 `SelectedDate` 所在的月份开始；未选中任何日期时，从当前月份开始。控件启动之后，从代码修改 `SelectedDate` 不会把视图转到该日期的月份；如需如此，请设置 `DisplayMonth` 或调用 `GoToMonth`。`GoToMonth(DateOnly date, bool raise = true)` 会滑动到包含 `date` 的月份，并且在 `raise` 保持默认值时也会更新 `DisplayMonth`。

### 标签与文化

`Culture` 设置月份名称、星期名称以及无障碍名称中长日期的语言。未设置时使用当前 UI 文化，因此跟随操作系统语言的应用无需任何设置。`FirstDayOfWeek` 选择第一列；默认为 `Sunday`，欧洲或 ISO 版式通常设为 `Monday`。

`TodayLabel`、`PreviousMonthLabel` 与 `NextMonthLabel` 替换文字与名称。今天标签是可见文字；上一月与下一月标签只是无障碍名称，因为这些按钮显示的是箭头。请把三者一起翻译，避免屏幕阅读器在控件中途切换语言。

```xml
<arc:Calendar Culture="de-DE"
              FirstDayOfWeek="Monday"
              TodayLabel="Heute"
              PreviousMonthLabel="Vorheriger Monat"
              NextMonthLabel="Nächster Monat"
              ShowToday="True" />
```

### 焦点与漫游 Tab 停靠点

网格只有一个 Tab 停靠点：最后获得焦点的日期；否则是选中的日期；否则是今天；再否则是所显示月份中第一个可用的日期。Tab 在该日期处进入网格，并从网格移到下一个控件，因此长表单不需要四十二个停靠点。`FocusTabbableDay(NavigationMethod method = NavigationMethod.Directional)` 会把焦点移到该日期，并在没有任何日期能获得焦点时返回 `false`，例如控件尚未显示，或该月每一天都被禁用。把它承载在弹出层或对话框中，并在控件挂接之后调用，这样方向键会立即生效。

### 绑定到视图模型

预订页在视图模型中持有日期与可用性规则。日历绑定到日期，并以函数的形式接收规则，因此规则可以读取视图模型中的数据。

```csharp
public partial class StayViewModel : ObservableObject
{
    private readonly HashSet<DateOnly> _bookedNights;

    public StayViewModel(IReadOnlySet<DateOnly> bookedNights)
    {
        _bookedNights = bookedNights.ToHashSet();
        MinStayDate = DateOnly.FromDateTime(DateTime.Today);
    }

    [ObservableProperty]
    private DateOnly? _stayDate;

    [ObservableProperty]
    private DateOnly? _viewMonth;

    [ObservableProperty]
    private string? _reservationNotice;

    public DateOnly MinStayDate { get; }

    public DateOnly MaxStayDate => MinStayDate.AddMonths(6);

    public Func<DateOnly, bool> IsBooked => date => _bookedNights.Contains(date);

    [RelayCommand]
    private void Continue()
    {
        if (StayDate is { } checkIn)
        {
            ReservationNotice = $"已为 {checkIn:MMM d, yyyy} 办理入住保留 15 分钟。";
        }
    }
}
```

```xml
<StackPanel Width="328" Spacing="12">
    <arc:Calendar SelectedDate="{Binding StayDate, Mode=TwoWay}"
                  DisplayMonth="{Binding ViewMonth, Mode=TwoWay}"
                  MinDate="{Binding MinStayDate}"
                  MaxDate="{Binding MaxStayDate}"
                  DisabledDates="{Binding IsBooked}" />
    <arc:Button Content="继续" Command="{Binding ContinueCommand}" />
    <TextBlock Text="{Binding ReservationNotice}" />
</StackPanel>
```

`SelectedDate` 与 `DisplayMonth` 是双向的，因此视图模型能收到每一次选择和每一次月份切换。`MinDate`、`MaxDate` 与 `DisabledDates` 是单向输入，视图模型在规则变化时设置它们。只要为属性设置新值即可：当限制变化时，日历会刷新网格和导航按钮。日历本身没有命令。`继续` 按钮调用视图模型的命令，该命令读取 `StayDate` 并设置 `ReservationNotice`，一个字符串，按钮下方的 `TextBlock` 可以绑定到它。

日期规则只在用户输入时检查。通过绑定或代码设置的 `SelectedDate` 不会与 `MinDate`、`MaxDate` 或 `DisabledDates` 比较，因此视图模型不能设置超出它们范围的日期。请在视图模型中保持限制与值的一致。

### 示例：酒店入住日期

Northwind Lodge 的租赁预订页。客人在 2026 年 11 月选择入住日。日历从所选夜晚所在的月份开始，最早的日期是 11 月 1 日，因此上一月按钮被禁用，下方的摘要行用简单的文字说明该决定。在这个场景中日历没有可见的今天按钮，因为该场景固定在某个日期上，应该在每台机器上都看起来一样。

月视图就是控件的全部：无需单独的标题或图例，因为页眉已经写明月份，圆点则显示选择。

![带有 2026 年 11 月日历、已选中入住日和继续按钮的入住日期卡片](images/StayDates.png)

```xml
<Border Width="560"
        Padding="24"
        Background="{arc:ArcDesignTokenResource Surface}"
        BorderBrush="{arc:ArcDesignTokenResource BorderSubtle}"
        BorderThickness="1"
        CornerRadius="{arc:ArcDesignTokenResource RadiusPanel}">
    <StackPanel Spacing="16">
        <StackPanel Spacing="4">
            <TextBlock Text="Stay dates"
                       FontSize="{arc:ArcDesignTokenResource TypeLG}"
                       FontWeight="{arc:ArcDesignTokenResource WeightMedium}"
                       Foreground="{arc:ArcDesignTokenResource Foreground}" />
            <TextBlock Text="Harbor View Suite, room 412"
                       FontSize="{arc:ArcDesignTokenResource TypeSM}"
                       Foreground="{arc:ArcDesignTokenResource TextMuted}" />
        </StackPanel>
        <arc:Calendar SelectedDate="2026-11-12"
                      MinDate="2026-11-01"
                      MaxDate="2026-12-31" />
        <Grid ColumnDefinitions="*,Auto">
            <TextBlock Text="Check in Nov 12, 2026, 3 nights"
                       VerticalAlignment="Center"
                       FontSize="{arc:ArcDesignTokenResource TypeSM}"
                       Foreground="{arc:ArcDesignTokenResource TextSecondary}" />
            <arc:Button Grid.Column="1" Content="Continue" />
        </Grid>
    </StackPanel>
</Border>
```

这个示例中的决定都是有意为之。限制来自预订窗口，因此客人无法选择过去的夜晚，也无法选择年末之后的夜晚。摘要用文字重复了选择，这对屏幕阅读器以及向上滚动回到页面的客人都很重要。按钮是唯一的主要操作；日历本身没有操作按钮。

### 文案规范

- **月份标题**：日历会按文化的格式为你写出它；不要在同一卡片中再加一个写着同一月份的标题。请在日历上方的标题中说明用途，例如“入住日期”或“休假申请”。
- **今天标签**：英文界面保留默认的 “Today”，或使用产品为当天所用的词。它是可见按钮，因此只用一个词。
- **上一月与下一月标签**：说明目的地，即 “Previous month” 或 “Next month”，因为按钮只通过名称被播报。
- **日历下方的摘要文字**：完整写出结果，日期使用数字，月份名称采用文化格式，例如 “Check in Nov 12, 2026, 3 nights”。说明结果，而不是机制。
- **禁用日期**：当整类日期都不可用时，在附近加以说明，例如“周末不可预订”。原因应写在页面上，因为网格本身不会说明某天为何被禁用。

| 不要写 | 改为 |
|---|---|
| Date | Stay dates |
| Choose a date | Check-in date |
| Next | Next month |
| Selected: 12 | Check in Nov 12, 2026 |
| Sat, Sun unavailable | Weekends are not bookable for this suite. |

### 最佳实践

**推荐**

- 把 `MinDate` 与 `MaxDate` 设为真实的预订或报表窗口。导航会停在边界上，人不会翻到一个没有可选日期的月份。
- 对于属于数据的规则使用 `DisabledDates`，例如已订满的夜晚；对于窗口使用 `MinDate` 与 `MaxDate`。这两类规则在网格和键盘模型中的表现方式一致。
- 将 `SelectedDate` 设为双向绑定，并在视图模型中对绑定的值作出反应，使摘要和保存操作总能看到与网格相同的日期。
- 根据产品所在的地区选择 `FirstDayOfWeek`，因为人们首先扫视的是第一列。
- 每个决定只放一个日历。同一页面上并排的两个日历会让人同时比较两个月，这种情况更适合 `DateRangePicker`。

**避免**

- 把 `DateSelected` 当作值的唯一来源。它只为用户操作而触发，因此在代码中设置的值永远不会到达等待该事件的视图模型。
- 在代码中设置会破坏 `MinDate`、`MaxDate` 或 `DisabledDates` 的 `SelectedDate`。日历不会修正它，因此网格可能显示一个人永远无法选择的选中日期。
- 把日历放进同时带有日期字段的表单。同一个值的两个日期输入会让人不清楚哪一个是当前值；表单中应使用 `DatePicker`。
- 为窗口不包含今天的日历开启 `ShowToday`。这个按钮会把人带到限制之外。
- 在日历上方再加一个重复的月份标题。页眉已经写明月份，而重复的文字不会随人导航而改变。

### 常见问题

**为什么网格总是显示六周？** 每个月使用六行，因此网格有 42 天，相邻月份的日期填满空白格。高度在导航时从不改变，因此日历下方的内容不会随月份切换而跳动。

**为什么我在代码中设置 `SelectedDate` 时处理程序没有运行？** `DateSelected` 只为用户的选择而触发：点击、在聚焦的日期上按下 Enter 或 Space，或点击“今天”按钮。对于代码中的修改，请使用绑定的 `SelectedDate` 属性；只对用户所采取的操作使用 `DateSelected`。

**如何把视图切换到某个日期所在的月份？** 对于带动画的导航，调用 `GoToMonth(new DateOnly(2027, 3, 1))`；或者把 `DisplayMonth` 设为该月中的某一天。除非传入 `raise: false`，否则 `GoToMonth` 也会更新 `DisplayMonth`。

**日历打开期间限制能变化吗？** 能。设置 `MinDate`、`MaxDate`、`DisabledDates`、`FirstDayOfWeek` 或 `Culture` 都会刷新网格，导航按钮也会随之更新。现在已被禁用的选择会保持选中，直到用户选择另一天，因此当规则使其失效时，请在代码中清除它。

**日历保持打开并跨过午夜会怎样？** 日历跟随本地日期。日期变化时，高亮的今天和“今天”按钮会更新，因此整夜留在屏幕上的日历不会把旧的一天显示为今天。

## API 参考

<!-- api: Calendar -->

## 无障碍

- 日历以其月份标题命名，例如 “October 2026”，因此焦点进入网格时，人能听到网格显示的内容。
- 每一天都是一个按钮，名称为文化长格式的完整日期，例如 “Tuesday, October 20, 2026”。选中的日期带有帮助文字 “selected”，今天则带有 “today”。
- 上一月、下一月和“今天”按钮都有名称：即 `PreviousMonthLabel`、`NextMonthLabel`，以及后接今天完整日期的今天标签。上一月与下一月按钮在包含 `MinDate` 或 `MaxDate` 的月份中被禁用，因此在那里它们会被播报为不可用。
- 同一时间只有一天在 Tab 顺序中。方向键、Home、End 与 Page 键在网格内移动焦点；Tab 则离开网格，进入下一个控件。
- 月份切换以礼貌的方式播报。滑出视野的月份不可聚焦，并对辅助技术隐藏，因此焦点永远不会落在人看不见的日期上。
- 禁用的日期保留在网格中，因此人能看到该日期存在，但无法选择它。

### 键盘

| 按键 | 操作 |
|---|---|
| `Left` / `Right` | 焦点向前或向后移动一天；跨入相邻月份 |
| `Up` / `Down` | 焦点向前或向后移动一周 |
| `Home` / `End` | 移到所在周的第一天或最后一天，依据 `FirstDayOfWeek` |
| `PageUp` / `PageDown` | 焦点向前或向后移动一个月；保留日期，并限制在该月的长度之内 |
| `Shift` + `PageUp` / `PageDown` | 焦点向前或向后移动一年 |
| `Enter` / `Space` | 选择聚焦的日期；禁用的日期会被忽略 |

移动会在 `MinDate` 与 `MaxDate` 处停止，并沿移动方向跨过禁用的日期。移动到新的月份时，会改变 `DisplayMonth`，并在该月开始滑动的同一帧移动焦点。Tab 与 Shift+Tab 离开网格；只有焦点位于某一天上时，日历才处理方向键。

## 动效

- 月份沿移动方向在同一条带上滑动。标题随之滑动，选中的圆点从一天滑向另一天。圆点经过时，数字会改变颜色。
- 长距离跳转（例如“今天”或 Shift + PageDown）从目标月份旁边的月份开始，不会经过中间的各个月份。
- 减弱动效时，月份与圆点直接跳到最终状态，标题原地替换。关闭动效时同样如此。

| 变化 | 完整 | 减弱 | 关闭 |
|---|---|---|---|
| 移动一个月（按钮、PageUp、PageDown） | 月份条沿移动方向以短暂、无回弹的弹簧滑动 | 新月份立即出现 | 立即 |
| 月份标题 | 旧标题沿移动方向滑出，新标题淡入 | 标题原地替换 | 立即 |
| 选中圆点在日期之间移动 | 沿直线滑过网格 | 跳到新日期 | 跳到新日期 |
| 选择首次出现 | 从 60% 的比例放大，并以快速弹簧淡入 | 立即显示 | 立即 |
| 圆点下方的日期数字 | 圆点经过时改变颜色 | 立即改变颜色 | 立即 |
| 长距离跳转（今天、Shift + 翻页键） | 从目标旁边的月份开始，跳过中间的月份 | 直接跳转 | 立即 |

动效从不延迟键盘操作。在月份条仍在滑动时再次按下方向键，会为同一个弹簧重新设定目标，因此日历会跟随最新的位置，而不会排队执行较早的移动。

## 相关组件

- `DatePicker`：用于日期字段，点击后在弹出层中打开日历，通常位于表单中。
- `DateRangePicker`：用于带预设的开始与结束日期。
- `TimePicker`：用于选择一天中的时间。
- `DataGrid`：用于需要列与排序的带日期记录。

| 组件 | 适用场景 |
|---|---|
| `DatePicker` | 表单需要一个带标签、说明与错误行的日期字段 |
| `DateRangePicker` | 用户选择开始与结束日期，通常带有“最近 30 天”等预设 |
| `TimePicker` | 值是一天中的时间，或时间与日期配对 |
| `DataGrid` | 日期属于许多需要排序与列的记录 |
| `Select` | 选项是具名的选项，而不是日期 |
