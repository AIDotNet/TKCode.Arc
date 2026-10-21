---
title: 日期范围选择器
summary: 从触发器展开为两个月份面板的范围选择器，附带预设项和随选区伸展的高亮。
source-hash: baaba86e68df
---

<Demo name="Report" />

## 何时使用

`DateRangePicker` 把一段以整天为单位的范围作为一个值来选择：一个开始日和一个结束日。它位于筛选栏、预订表单或工具栏中。关闭状态下，触发器以一行简短文字显示已应用的范围；点击后打开面板，其中有预设项列表和一个或两个月份，因此人们既可以一键选取常用时段，也可以精确选定一段日期。

- **分析仪表板中的报表筛选**：收入或转化率报表，带有“最近 7 天”“最近 30 天”“年初至今”等预设项。预设项覆盖常用时段，日历负责其余部分。
- **住宿与请假申请**：HR 门户中的酒店预订或请假申请需要入住日和退房日。把 `MinDate` 设为今天，就不能选择过去的夜晚；把 `MaxDate` 设为最后一个可预订的日期。
- **账单周期与发票导出**：管理后台按 `Oct 1 – 31, 2026` 这样的周期导出发票。值只在点击“应用”后才会变化，因此未选完的范围不会进入导出查询。
- **紧凑工具栏与侧边面板**：日志查看器或事件时间线的筛选栏，在被修改之前只显示一个时间窗口标签。当工具栏位于侧边列中时，`MonthCount="One"` 可以让面板保持窄小。

## 何时不用

- **单日，例如截止日期或出生日期**：用 `DatePicker`。它只保存一天，没有预设项列表，也没有范围高亮。
- **需要常驻页面的月份**：当人们要扫视可用情况，或比较一个月内的多个日期时，用 `Calendar`。范围选择器只在需要时才打开，因此面板打开时，页面上的其他内容都无法同时看到。
- **一天中的时刻，例如班次开始或会议时段**：每个时刻都用 `TimePicker`。`DateRange` 保存的是 `DateOnly` 类型的两端，因此范围选择器从不携带时间。
- **只有固定的命名时段列表，没有自定义范围**：用 `Select`。预设项列表是本控件的一部分；如果从不使用自定义日历，普通列表更简单。
- **自由输入日期范围**：触发器没有文字输入，因此人们无法输入 `10/05 - 10/09`。如果产品需要手动输入日期，就给表单两个 `DatePicker` 字段，并校验结束日期晚于开始日期。

## 用法

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:DateRangePicker Label="日期范围" />
</UserControl>
```

```csharp
var today = DateOnly.FromDateTime(DateTime.Today);
picker.SelectedRange = new DateRange(today.AddDays(-6), today);
```

`DateRangePicker` 派生自 Avalonia 的 `TemplatedControl`，因此它的外观来自 ControlTheme，而面板是由控件自己拥有的弹层。它不是文本框，也不是 Avalonia 的日期选择器：触发器中不输入任何内容，因此没有光标、没有输入法，也没有日期解析。字段在与其他表单控件相同的 `Field` 基础之上添加标签、说明与错误行，面板则用 Arc 的月份网格绘制。`DateRange` 是由两个 `DateOnly` 组成的记录结构体，两端都包含在内，因此 `Days` 也会计入两端。

### 结构

1. **标签**（`Label`、`IsLabelVisible`）：触发器与面板的无障碍名称。标签默认隐藏；设置 `IsLabelVisible="True"` 后，它会以字段标签的样式显示在触发器上方。
2. **触发器**（由 `DateRangePicker.TriggerPart` 指定的模板部件）：高 44 px（Medium 控件高度），圆角 18 px，1 px 边框位于抬起的表面上，并带有静止阴影。内容前的内边距为 14 px，内容后为 12 px，各部分之间相距 10 px。悬停时，触发器以 3% 的前景色浮层填充，边框加深为强边框色。
3. **日历图标**：16 px，次要文字颜色。
4. **值**（由 `DateRangePicker.ValuePart` 指定的模板部件）：已应用的范围，中等字重，例如 `Oct 5 – 9, 2026`。没有范围时，以次要文字颜色显示 `Placeholder`。变化的词会在原位滚动。
5. **箭头**：16 px，弱化文字颜色，位于触发器末端。
6. **说明**（`Description`）与**错误行**（`ErrorMessage`）：触发器下方的帮助文案与错误文案，来自字段基础。非空的 `ErrorMessage` 即为错误文案；控件没有单独的无效标志。
7. **面板**（打开时）：内边距为 10 px（紧凑布局为 8 px）。双月布局：148 px（9.25 rem）宽的预设项列，与月份之间有 10 px 间距。紧凑布局：预设项变为分隔线下方的一行水平列表。
8. **月份导航**：两个 32 px 的圆形按钮，分别是上一月和下一月。当按钮要翻到的月份会超出 `MinDate` 或 `MaxDate` 时，该按钮禁用；禁用状态的不透明度为 35%。
9. **页脚**：一条 1 px 分隔线，下方是一行：左侧为摘要（天数，例如 `12 days`；第一次点击后显示 `Pick an end date`），右侧为取消与应用按钮。两个按钮高 36 px。应用按钮至少 76 px 宽，没有草稿时禁用。

### 布局：一个或两个月份

`MonthCount` 决定面板的形式。`Auto`（默认）在边界宽度至少为 712 px 时，在预设项列表旁显示两个月份；否则在可水平滚动的预设项行下显示一个月份。`One` 始终使用紧凑面板，`Two` 则始终在预设项列表旁显示两个月份。

<Demo name="OneMonth" />

在随窗口伸缩的页面和对话框中使用 `Auto`，让面板展示尽可能多的上下文。当窗口调整大小并跨过 712 px 的边界时，面板会在一个月与两个月之间切换，表面也会以弹簧动画过渡到新的形状。在较窄的侧边列和移动端宽度的布局中使用 `One`，以免第二个月被截掉。只有在确知页面足够宽时才使用 `Two`，因为 `Two` 不会回退到紧凑面板。

面板始终位于 `Boundary` 之内，默认为窗口。当触发器靠近可滚动页面的底部时，页面会滚动，使面板可见，面板仍然从触发器处打开。

### 预设项

`Presets` 会替换默认的八个预设项列表。预设项由一个标签和一个函数组成，该函数根据今天的日期返回一个 `DateRange`。函数接收的是查看者本地的今天，因此像“最近 7 天”这样的预设项无需页面中的任何代码，就会随日历变化。`DateRangePresets.Default` 保存默认列表，因此自定义列表可以以它为起点，再添加自己的条目。

| 预设项 | 范围 |
|---|---|
| 今天 | 今天到今天 |
| 昨天 | 昨天到昨天 |
| 最近 7 天 | 今天之前的六天到今天 |
| 最近 30 天 | 今天之前的二十九天到今天 |
| 本月 | 本月第一天到今天 |
| 上个月 | 上一个完整的自然月 |
| 本季度 | 本季度第一天到今天 |
| 年初至今 | 1 月 1 日到今天 |

```csharp
picker.Presets =
[
    new DateRangePreset("最近 7 天", t => new DateRange(t.AddDays(-6), t)),
    new DateRangePreset("未来 14 天", t => new DateRange(t, t.AddDays(13))),
    new DateRangePreset("过去三个月", t => new DateRange(t.AddMonths(-3), t.AddDays(-1))),
];
```

当草稿与某个预设项的范围相同时，该预设项会被高亮，因此无论人们选择的是预设项，还是手动选中了相同的日期，列表都会显示当前的选择。高亮会从一个预设项平滑地滑动到下一个。请把列表保持在六项左右：预设项列表每行 32 px，在紧凑布局中，该行会横向滚动。

### 限制与每周起始日

`MinDate` 与 `MaxDate` 会禁用范围之外的日期，以及会翻到范围之外的月份按钮。键盘同样受限：方向键和翻页键不会把焦点移到被禁用的日期上。`FirstDayOfWeek` 设置网格的第一列，默认为星期日，也可以设为星期一。

<Demo name="Booking" />

当限制来自业务规则（例如六个月的预订窗口）时，在视图模型中设置它们，并把预设项限制在该窗口之内。超出 `MaxDate` 的预设项不会被自动裁剪，因此当范围必须保持在窗口之内时，应基于同一限制来计算预设项。

### 区域文化

`Culture` 设置月份标题、星期标题、范围标签和天数的文字。为 null 时使用当前的 UI 区域文化。控件显示的文字都位于属性之中，因此本地化页面需要像表单的其他部分一样，为这些属性提供翻译。

<Demo name="Culture" />

| 属性 | 默认值 | 用于 |
|---|---|---|
| `Placeholder` | `Select dates` | 未应用范围时的触发器 |
| `ApplyText` / `CancelText` | `Apply` / `Cancel` | 页脚按钮 |
| `NoDatesText` | `No dates` | 草稿为空时的触发器和摘要 |
| `PickEndText` | `Pick an end date` | 第一次点击之后、选定结束日期之前的摘要 |
| `DayCountFormat` | `{0} day` | 一天的天数；只有数量为 1 时才使用单数形式 |
| `DaysCountFormat` | `{0} days` | 两天或更多天的天数；`{0}` 为数字 |
| `PreviousMonthLabel` / `NextMonthLabel` | `Previous month` / `Next month` | 导航按钮的无障碍名称 |

为每种语言同时设置两种天数格式，因为控件只有在数量为 1 时才使用单数形式。`FirstDayOfWeek` 与 `Culture` 相互独立：以星期一作为每周起始日是一项业务选择，因此示例显式地设置它，而不是依赖区域文化。

### 由代码驱动

`SelectedRange` 是值，`IsDropDownOpen` 用于打开或关闭面板。面板关闭时设置 `SelectedRange`，会立即更新触发器。`FormattedValue` 返回触发器显示的文字，未应用范围时为占位文字，因此状态行或导出标签可以复用它。

```csharp
picker.SelectedRangeChanged += (_, e) =>
{
    status.Text = e.NewRange is { } range ? $"{range.Days} 天" : "无范围";
};
```

在按下应用之前，所选内容都只是草稿。`SelectedRangeChanged` 仅在点击应用时触发，其事件参数包含 `OldRange` 和 `NewRange`。取消、Escape 以及面板外的按下都不会触发它。

### 绑定到视图模型

在 MVVM 应用中，视图模型拥有范围，页面以双向方式绑定它。使用 CommunityToolkit.Mvvm 时：

```csharp
public partial class ReportFilterViewModel : ObservableObject
{
    private readonly IReportService _reports;

    public ReportFilterViewModel(IReportService reports)
    {
        _reports = reports;
        _period = new DateRange(new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 21));
    }

    [ObservableProperty]
    private DateRange? _period;

    [ObservableProperty]
    private string? _periodError;

    partial void OnPeriodChanged(DateRange? value)
    {
        PeriodError = value is { Days: > 90 } ? "最多选择 90 天。" : null;
    }

    [RelayCommand]
    private Task RunReportAsync()
    {
        if (Period is not { } range || PeriodError is not null)
        {
            return Task.CompletedTask;
        }
        return _reports.LoadAsync(range.Start, range.End);
    }
}
```

```xml
<StackPanel Spacing="16" Width="360">
    <arc:DateRangePicker Label="报表周期"
                         SelectedRange="{Binding Period, Mode=TwoWay}"
                         ErrorMessage="{Binding PeriodError}" />
    <arc:Button Content="运行报表" Command="{Binding RunReportCommand}" />
</StackPanel>
```

`SelectedRange` 是双向绑定的。它只在人们按下应用时才会写入，因此 `Period` 永远不会保存草稿。选择器本身没有命令：表单中的按钮负责运行报表，只有当页面需要对每次应用作出响应（例如更新状态行）时，才需要 `SelectedRangeChanged`。编译绑定需要在视图上设置 `x:DataType`；示例为简洁起见省略了它。

### 校验

控件只强制执行它所知道的限制：`MinDate`、`MaxDate` 以及被禁用的日期。关于范围的规则属于视图模型，结果交给 `ErrorMessage`。

| 规则 | 何时检查 | 显示位置 |
|---|---|---|
| 超出 `MinDate` 或 `MaxDate` 的日期 | 选择时（日期被禁用） | 无：该日期无法选中 |
| 范围超过报表允许的长度 | 应用时，通过 `Period` 的变化 | 触发器下方的 `ErrorMessage` |
| 表单要求填写周期 | 提交时 | `ErrorMessage`；把焦点移到选择器 |
| 服务器拒绝该周期 | 请求之后 | `ErrorMessage`；`Period` 变化时清除 |

应用会把面板中的草稿挡在视图模型之外，因此规则作用于已提交的值。人们应用新范围时清除错误；上面的 `OnPeriodChanged` 处理函数会这样做，因为每次提交的变化都会经过它。

### 焦点与关闭

- **打开**：在触发器上按 Enter 或 Space 会打开面板，并把焦点放在已提交范围的开始日。没有范围时，焦点从今天开始。
- **应用**：应用会提交草稿、关闭面板，并把焦点还给触发器。存在草稿时，应用按钮才可用。
- **取消**：取消、Escape 或面板外的按下都会丢弃草稿。取消与 Escape 会把焦点还给触发器；面板外的按下不会移动焦点，因为人已经自己移动了焦点。
- **未选完的范围**：第一次点击之后，应用会把开始日当天作为一天的范围提交。此时摘要显示 `Pick an end date`，因此人们在应用之前就能看到自己的选择。
- **Tab 离开**：焦点移出面板会关闭面板并丢弃草稿，因此面板永远不会在其他控件后面保持打开。

### 示例：预订表单中的酒店入住

某酒店集团的预订表单。入住信息是一个字段，入住时段限制在未来六个月之内，说明文字告诉人们这些日期的作用。表单只在应用之后才读取入住信息，因此价格报价始终与已提交的日期一致。

```xml
<StackPanel Width="560" Spacing="20">
    <arc:DateRangePicker Label="入住"
                         IsLabelVisible="True"
                         Description="从今天起可办理入住，最远到未来六个月。"
                         Culture="en-US"
                         FirstDayOfWeek="Monday"
                         Placeholder="选择入住和退房日期" />
    <arc:Input Label="住客姓名" Placeholder="与护照上的姓名一致" />
    <arc:Button Content="继续付款" IsDefault="True" />
</StackPanel>
```

`MinDate` 与 `MaxDate` 在代码中设置，取值与视图模型使用的今天相同，因此预订窗口和被禁用的日期永远不会与服务器的规则相互矛盾。

### 文案规范

以业务含义为字段命名：`Report period`、入住、`Billing period`。预设项标签应简短，并采用句子式大小写。按钮用动词加宾语的形式书写，日期数量则遵循产品所用的格式。

| 不要写 | 改为 |
|---|---|
| `Date`（用于范围） | `Report period`、入住、`Billing period` |
| `Last 7 Days` | `Last 7 days` |
| `OK` | `Apply`（当按钮用于筛选列表时，用 `Apply filter`） |
| `Pick end date` | `Pick an end date`（默认文字） |
| `1 days` | 为一天设置 `DayCountFormat`，为更多天设置 `DaysCountFormat` |
| `Select a date range from the calendar below` | `Select dates` |

占位文字要保持简短，使它能与 44 px 触发器中的日历图标和箭头并排显示。

### 最佳实践

**推荐**

- 把 `Label` 设为业务含义，并在表单中用 `IsLabelVisible="True"` 显示它。隐藏的标签仍会为辅助技术命名该字段，但浏览表单的人需要可见的名称。
- 双向绑定 `SelectedRange`，并且只读取已提交的值。草稿只在面板打开期间存在，因此页面的其余部分永远看不到未选完的范围。
- 放入报表或预订常用的预设项，并保持列表简短。人们选预设项比滚动日历更快。
- 根据业务规则设置 `MinDate` 与 `MaxDate`。被禁用的日期本身就能说明限制，无需消息。
- 在较窄的列中使用 `MonthCount="One"`，以免第二个月被截掉。

**避免**

- 用选择器来选择单个日期。范围高亮和预设项会增加单个日期并不需要的步骤。
- 期望面板打开时 `SelectedRange` 会跟随键盘或指针变化。值只在应用时才会变化，因此对每个选中的日期作出反应的代码永远不会执行；应改为订阅 `SelectedRangeChanged`。
- 在本地化产品中依赖默认的英文文字。请同时设置 `Placeholder`、`ApplyText`、`CancelText` 以及两种天数格式，使触发器和页脚使用同一种语言。
- 在包含大量行的可滚动列表中使用范围选择器。每一行都会打开自己的面板，难以浏览；应把筛选移到列表的页头。

### 常见问题

**我选了新的日期，为什么触发器仍显示旧范围？** 在应用之前，选择都只是草稿。按下应用即可提交；取消、Escape 或面板外的按下会丢弃草稿，并保留之前的 `SelectedRange`。

**可以从代码打开面板吗？** 可以。把 `IsDropDownOpen` 设为 `True`，面板会打开，焦点位于已提交的开始日，与用键盘打开时一致。把它设回 `False` 即可关闭面板，而不应用草稿。

**如果第二天早于第一天会怎样？** 选择器会对两天排序，范围从较早的日期到较晚的日期。在代码中同样如此：`DateRange.Ordered(a, b)` 会按顺序返回范围。

**范围一定有结束日吗？** `DateRange` 总是包含两天，且两端都计入。如果人们只选了开始日就应用，范围就是那一天。若要求必须选两天，请在视图模型中检查 `range.Days > 1`。

**如何计算住宿的晚数？** `Days` 计入两端，因此从 3 月 3 日到 3 月 5 日的入住有三天、两晚。若预订按晚收费，晚数为 `range.Days - 1`。

## API 参考

<!-- api: DateRangePicker -->

<!-- api: DateRange -->

| 配套类型 | 成员 | 摘要 |
|---|---|---|
| `DateRangePreset` | `DateRangePreset(string label, Func<DateOnly, DateRange> range)` | 根据标签和“今天”的函数创建预设项 |
| `DateRangePreset` | `Label` | 按钮文字，例如 `Last 7 days` |
| `DateRangePreset` | `Range` | 给定“今天”时的范围 |
| `DateRangePresets` | `Default` | Arc 的八个预设项，从今天到年初至今 |

## 无障碍

- 触发器会暴露其展开状态，其名称包含当前范围。`Label` 是字段名称。
- 每个月份都是以其标题命名的网格。范围内的日期会被暴露为已选中，今天会被标记。每个日期都以完整的长日期命名。
- 在某一天上按 Enter 或 Space 会先选择开始日，再选择结束日。选定开始日之后，方向键会扩展范围预览。
- 预设项列表用 ArrowUp 和 ArrowDown 移动。紧凑布局中，预设项行用 ArrowLeft 和 ArrowRight 移动。
- 取消、Escape 以及指针在面板外的按下都会丢弃草稿，并把焦点还给触发器。应用会提交草稿，并通过礼貌型实时区域播报开始日、范围和天数。
- 导航按钮由 `PreviousMonthLabel` 和 `NextMonthLabel` 命名，因此请用页面所用的语言设置它们。

### 键盘

| 按键 | 操作 |
|---|---|
| `Enter` / `Space`（在触发器上） | 打开面板，并把焦点放在已提交范围的开始日，或今天 |
| `Enter` / `Space`（在某一天上） | 第一次按下选择开始日；第二次按下选择结束日并生成草稿 |
| `Left` / `Right` | 把焦点移动一天；已选开始日时，预览会延伸到该日 |
| `Up` / `Down` | 把焦点移动一周 |
| `Home` / `End` | 移到所在周的第一天或最后一天，周的起始由 `FirstDayOfWeek` 决定 |
| `PageUp` / `PageDown` | 把焦点移动一个月；按住 `Shift` 则移动一年 |
| 在预设项上的 `Up` / `Down`（紧凑布局中为 `Left` / `Right`） | 在预设项之间移动 |
| 在预设项、取消或应用上的 `Enter` / `Space` | 激活获得焦点的按钮 |
| `Tab` / `Shift+Tab` | 在面板内移动；离开面板会关闭它并丢弃草稿 |
| `Escape` | 关闭面板、丢弃草稿，并把焦点还给触发器 |

键盘焦点永远不会落在被禁用的日期上：方向键和翻页键会在 `MinDate` 和 `MaxDate` 处停止。

## 动效

- 触发器表面会展开为面板，其圆角和尺寸同时变化。应用之后，表面缩回触发器，范围标签飞回原位。
- 范围高亮会逐日伸展，两端的端帽滑动到新的日期。活动的预设项会在预设项之间滑动。
- 月份朝行进方向滑入，并在身后淡出。标签中的词朝时间的方向滚动。
- 减弱动效时，表面尺寸直接跳变，并省去共享元素与滑动。内容在 0.08 到 0.12 秒内淡入淡出。关闭动效时直接到达终态。

| 变化 | 完整 | 减弱 | 关闭 |
|---|---|---|---|
| 面板打开 | 触发器以弹簧动画展开为面板，耗时 0.48 s | 尺寸直接跳变；内容在 0.08 到 0.12 秒内淡入 | 立即完成 |
| 应用后面板关闭 | 面板在 0.38 s 内缩回触发器，标签飞回原位 | 尺寸直接跳变；摘要淡出 | 立即完成 |
| 月份变化 | 新月份从行进方向滑入，耗时 0.4 s | 内容在 0.08 到 0.12 秒内交叉淡化 | 立即完成 |
| 触发器的值变化 | 文字朝时间的方向滚动 | 内容在 0.08 到 0.12 秒内交叉淡化 | 立即完成 |

## 相关组件

- `DatePicker`：表单字段中的单个日期。
- `Calendar`：需要常驻页面的月份。
- `TimePicker`：一天中的时刻，当日期范围需要时间时使用。

| 组件 | 适用场景 |
|---|---|
| `DatePicker` | 值是一天，例如截止日期或出生日期 |
| `Calendar` | 月份必须保持可见以便浏览，例如可用性 |
| `TimePicker` | 值是一天中的时刻，例如班次开始 |
| `Select` | 选项是固定的短列表，只有命名时段，没有自定义范围 |
| `Field` + `FieldFrame` | 你要自建触发器，并希望它与标签和消息行保持一致 |
