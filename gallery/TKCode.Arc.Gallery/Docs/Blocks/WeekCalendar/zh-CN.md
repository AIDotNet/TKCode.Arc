---
title: Week calendar
summary: 一个以周为单位的规划视图，可拖动来新增、移动和调整日程，并撤销上一次更改；网格旁有月历和日历开关，还有全天行；窄容器中显示三日视图和议程视图。
source-hash: 95480e02214a
---

<Demo name="Preview" />

## 何时使用

`WeekCalendar` 是一个以周为单位的完整规划界面，与桌面日历应用显示一周的方式相同。工具栏用于在周之间切换并切换视图；侧边栏提供一个可用于跳转的月历，以及用于显示或隐藏的日历列表；网格包含日期标题行、全天行和各个小时，同一天中重叠的日程会并排排列，当前时间线画在今天的列上，已过去的时间则变暗。用户可以直接编辑：在空白时间上拖动即可新增日程并为其命名，拖动日程可在日期和时间之间移动，拖动其底边可更改时长，单击日程可编辑其标题、日期、时间和日历，并可通过提示条或按 Ctrl+Z 撤销上一次更改。

- **排程与时间块规划。** 用户可以在一周内看到会议、专注时段和出行安排，了解每一天的排满情况，并通过拖动重新安排。
- **团队与资源日历。** 每个日历（团队、会议室或个人）都有自己的颜色和开关，读者无需离开页面即可进行比较或缩小范围。
- **以另一地点的时间查看预订。** 日程是绝对时刻；`TimeZone` 会让它们按任意时钟显示，标题栏的角落会说明当前是哪个时区。

## 何时不用

- **选择单个日期。** 请使用 `Calendar` 或 `DatePicker`；本区块关注的是每一天发生的事，而不是选择某一天。
- **月和年。** 本区块一次显示一到七天。以月网格显示日程或以年度总览显示，属于另一种设计。
- **简短的近期日程列表。** 对于大部分时段为空的网格，普通列表或 `Timeline` 更易于阅读。
- **同时显示数千个日程。** 只传入当前显示周前后的几周；本区块会为它收到的每个日程建立索引。

## 用法

### 日程与日历

每个 `WeekCalendarEvent` 具有 `Id`、`Title`、`Start` 和 `End`（不包含结束时刻），`Start` 与 `End` 均为 `DateTimeOffset` 时刻；还具有 `CalendarId`，以及可选的 `Location` 和 `Attendees`（`ProjectPerson` 对象；没有图片时会生成头像）。将 `IsAllDay` 设为 true 的日程则改为覆盖整个日期：按书写方式取 `Start` 和 `End` 的日期，`End` 不包含在内，因此从周三开始、住三晚的酒店入住会在周五之后的午夜结束。每个 `WeekCalendarSource` 具有 `Id`、`Name`、`Color`（`Blue`、`Green`、`Violet`、`Amber`、`Rose`、`Teal`、`Neutral`，或带有 `CustomColor` 的 `Custom`）以及 `IsVisible`，侧边栏中的开关会改变它的值。未知日历的日程显示为灰色。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:WeekCalendar x:Name="Planner">
        <arc:WeekCalendar.Calendars>
            <arc:WeekCalendarSource Id="studio" Name="Studio" Color="Blue" />
            <arc:WeekCalendarSource Id="home" Name="Home" Color="Green" />
            <arc:WeekCalendarSource Id="health" Name="Health" Color="Rose" IsVisible="False" />
        </arc:WeekCalendar.Calendars>
    </arc:WeekCalendar>
</UserControl>
```

```csharp
var zone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Berlin");
DateTimeOffset At(int day, int hour, int minute = 0) =>
    new(new DateTime(2026, 10, day, hour, minute, 0), zone.GetUtcOffset(new DateTime(2026, 10, day, hour, minute, 0)));

Planner.TimeZone = zone;
Planner.Events.Add(new WeekCalendarEvent("brief", "Write launch brief", At(19, 9, 30), At(19, 11, 30), "studio"));
Planner.Events.Add(new WeekCalendarEvent("lunch", "Lunch with Noor", At(19, 12), At(19, 13), "home") { Location = "Corner Bistro" });
Planner.Events.Add(new WeekCalendarEvent("hotel", "Hotel: Casa Azul",
    new DateTimeOffset(2026, 10, 21, 0, 0, 0, TimeSpan.Zero), new DateTimeOffset(2026, 10, 24, 0, 0, 0, TimeSpan.Zero), "home")
    { IsAllDay = true });
```

对于你自己拥有的集合，请使用 `EventsSource` 和 `CalendarsSource`；`ObservableCollection` 会被监听，其中每个事件和日历的每个属性也都会被监听。同一时刻发生的更改会一起应用，网格会原地更新。

```csharp
var events = new ObservableCollection<WeekCalendarEvent>();
calendar.EventsSource = events;

// Later, when a booking comes in, it appears at once and the day's events make room for it.
var start = new DateTimeOffset(DateTime.Today.AddHours(15));
events.Add(new WeekCalendarEvent("call-42", "Client call", start, start.AddMinutes(45), "studio") { Location = "Video room" });
```

### 编辑

除非设置了 `IsReadOnly`，否则可以编辑。所有操作都对齐到 15 分钟，且最短持续 15 分钟。

- **绘制日程。** 在空白时间上按下并拖动（拖动 4 像素即开始）。你按下的那个 15 分钟时段是锚点，移动的一端会取最近的一刻钟，因此向上绘制也可以；草稿保留在你按下的那一天。松开鼠标后，快速创建弹层会出现在草稿旁边：输入标题并按 Enter（空标题会保存为 "Untitled event"），或先选择日历。在空白时间上单击，会从指针下方最近的半点开始创建一个小时的草稿。新日程会使用 `DefaultCalendarId`，若未设置则使用第一个显示的日历。
- **移动日程。** 拖动它：它每次跳动一个时段，并始终保持你抓住的那一点位于指针下方，跨天时同样如此。放下时，当天的其他日程会让出位置。如果拖动结束时的位置与起始位置相同，则不做任何更改。
- **调整日程长度。** 拖动其底边（悬停时会显示把手）；结束时间会取最近的一刻钟。
- **编辑日程。** 单击它（或在其上按 Enter）打开详情：标题、日期、以 15 分钟为步长的开始和结束时间、地点、参与人和日历。时间、日期和日历会在你选择时即时生效；标题和地点则在弹层关闭时生效。全天日程以文字显示日期，并且可以更改标题和日历。
- **删除与撤销。** 对焦点或选中的日程按 Delete 或 Backspace，或在其详情中按 Delete，即可删除它。日历底部会弹出一条提示，带有 Undo，显示六秒（`UndoDuration`）；Ctrl+Z（Mac 上为 Cmd+Z）效果相同。撤销只有一层，涵盖上一次新建、移动、调整长度、时间、日期或日历的更改，或删除操作；对同一日程的一连串键盘移动，或同一个详情弹层中所做的更改，会一起撤销。`CanUndo` 表示是否存在可撤销的操作。
- **网格会滚动。** 当你拖动到它的顶部或底部附近时，网格会滚动，越靠近边缘滚动越快；指针静止时，滚动也会持续进行。
- 在已打开的弹层之外按下，只会关闭该弹层。Escape 会关闭弹层、取消拖动（日程会弹回原位），然后清除选择。`SelectedEvent` 是被选中的日程，且为双向绑定。

<Demo name="Editing" />

### 保存更改

日历会直接编辑你的数据：它会在 `WeekCalendarEvent` 对象上写入 `Start`、`End`、`CalendarId`、`Title` 和 `Location`，并在 `Events` 中添加或移除日程；如果 `EventsSource` 是可写列表，则改为在其中操作（`ObservableCollection` 会被监听；普通 `List` 则会被重新读取）。随后它会引发 `EventCreated`（撤销恢复已删除的日程时也会引发）、`EventUpdated`（其中 `Previous` 是更改前日程的副本；撤销时会再次引发，并交换两个值）或 `EventDeleted`（撤销移除新日程时也会引发）。请在这些处理程序中进行持久化。

```csharp
calendar.EventCreated += async (_, e) => await api.CreateAsync(e.Event);
calendar.EventUpdated += async (_, e) => await api.UpdateAsync(e.Event, e.Previous);
calendar.EventDeleted += async (_, e) => await api.DeleteAsync(e.Event.Id);
```

只读的 `EventsSource`（数组、`ReadOnlyObservableCollection`）不能增加或减少日程：日历仍会移动并编辑日程对象，但对于新建或删除的日程，它只会引发事件，由你的处理程序更改它所暴露的集合。将 `IsReadOnly` 设为 true 可完全关闭编辑：不能绘制、拖动、快速创建、删除或撤销，详情则以文字显示。新建日程（及其 `NewEventRequested` 事件）会在聚焦日从 9 点起的下一个空闲整点创建草稿；将 `Handled` 设为 true，则可以打开你自己的表单。`BeginCreate`、`OpenDetails`、`CloseEditor`、`DeleteEvent`、`Undo` 和 `CancelDrag` 可从代码中执行相同的操作。

```xml
<arc:WeekCalendar IsReadOnly="{Binding IsArchived}" DefaultCalendarId="studio" UndoDuration="0:0:8" />
```

### 触控

在触摸屏上，长按（约 380 毫秒且不移动）会在空白时间上开始绘制，或将日程抬起以便移动；否则，垂直滑动会滚动小时。宽度小于 640 时，横向滑动会切换日期：相邻日期会随手指一起移动；松开时如果超过宽度的五分之一（或快速轻扫），则直接过渡到相邻日期，不会跳动；较短的滑动则会弹回。在该宽度下，弹层会在日程下方打开（靠近底部时则在其上方），并与两侧保持 8 像素的距离。

### 视图与导航

`View` 可为 `Week`（从 `FirstDayOfWeek` 开始的七天，默认为周一）、`WorkWeek`（周一至周五）或 `Day`；它是双向绑定的。`Date` 是聚焦日，同样为双向绑定：所显示的范围始终包含它，将其设为范围之外的某一天，会将日历移动到那一天。今天、上一个和下一个会按一周移动（在日视图中为一天，在三日视图中为三天），并保持聚焦的星期几；侧边栏中的月历会跳转到包含任意一天的范围。`GoToToday`、`Next`、`Previous`、`GoTo`、`FocusDay` 和 `ScrollToTime` 可从代码中执行相同的操作。`RangeStart`、`RangeEnd`、`RangeTitle` 和 `EffectiveView` 报告当前显示的内容；`RangeChanged` 会说明是什么使其变化，以及朝哪个方向变化。

网格会在视图中第一个日程的时刻打开（若没有日程，则为上午 8 点），或在 `InitialScrollTime` 处打开。今天还会将当前时间滚动到距顶部三分之一处。`StartHour` 和 `EndHour` 用于限制显示的小时；超出范围的日程会在边缘处被截断。

<Demo name="WorkHours" />

### 窄容器

日历读取的是它自身的宽度。宽度为 1100 及以上时显示侧边栏（`IsSidebarVisible` 可强制开启或关闭）；低于此宽度时，日历列表会移入工具栏中的 Calendars 弹层。宽度低于 900 时，标题和标题栏日期会变小，New event 只显示加号；低于 760 时，Calendars 按钮只显示颜色；低于 680 时，工具栏分为两行。宽度低于 640 时，视图变为 3 days、Day 和 Agenda：周视图显示从聚焦日起的三天，议程则按天列出本周，全天日程排在最前，空闲日显示 "Nothing planned"，每一天的标题在其日程滚动时保持在顶部。随着尺寸变小，日程块会去掉地点、头像和时间；有足够高度时，标题会换行显示为三行。

<Demo name="Phone" />

### 时区

日程是绝对时间；`TimeZone`（null 表示计算机所在的时区）决定它们按哪种时钟绘制、当前时间线以及"今天"的判断。标题栏的角落会显示该时区的偏移量（"GMT+8"、"GMT+5:30"、"GMT"）。`Culture` 设置日期和时间的语言：英文会合并范围两端共有的部分（"Sep 28 to Oct 4, 2026"、"9:30 – 11:30 AM"）；中文则使用年-月-日标题和 24 小时制。`Now` 用于固定当前时间，便于截图和测试。

<Demo name="TimeZones" />

### 全天行

全天日程是跨越其所在日期的色条，并被排列到若干泳道中。当所需泳道超过 `AllDayLaneLimit`（3）条时，最后一条泳道会在每个有隐藏日程的日期上显示 "+N more"，"All day" 下方的箭头可展开该行；`IsAllDayExpanded` 是双向绑定的状态。

<Demo name="BusyAllDay" />

### 文本

每个词都有 `WeekCalendarLabels` 中的英文默认值：工具栏、视图名称、日、日程和全天色条的屏幕阅读器名称（这些是函数，因此其各部分的顺序可以改变），以及各种播报。替换 `Labels` 即可翻译；其中的日期和时间来自 `Culture`。

```csharp
calendar.Culture = CultureInfo.GetCultureInfo("de-DE");
calendar.Labels = new WeekCalendarLabels
{
    Today = "Heute",
    WeekView = "Woche",
    WorkWeekView = "Arbeitswoche",
    DayView = "Tag",
    AllDay = "Ganztägig",
    NewEvent = "Neuer Termin",
    TimedEvent = (title, day, start, end, calendar, place) => $"{title}, {day}, {start} bis {end}, {calendar}",
};
```

### 性能

- 每个日程块、单元格和标题日都是自行绘制并缓存的；范围滑动时不进行任何布局，只做移动。
- 拖动时只移动被拖动的块；当天的其他日程只在放下时重新布局一次，并通过变换过渡到位。
- 范围被绘制到至多三个池化的窗格中；滑出的窗格会被复用于下一个范围，并带上其中的块，因此翻页浏览各周时不会创建任何新内容。
- 当前时间每分钟移动一次，计时器会在日历离开窗口或设置了 `Now` 时停止。

## API 参考

<!-- api: WeekCalendar -->

<!-- api: WeekCalendarEvent -->

<!-- api: WeekCalendarSource -->

<!-- api: WeekCalendarLabels -->

<!-- api: WeekCalendarEventUpdatedEventArgs -->

### 事件

| 事件 | 引发时机 | 参数 |
|---|---|---|
| `RangeChanged` | 显示的日期已变化 | `OldStart`, `OldEnd`, `NewStart`, `NewEnd`, `Direction`, `Reason` |
| `ViewChanged` | `View` 已变化 | `OldView`, `NewView`, `EffectiveView` |
| `CalendarVisibilityChanged` | 使用了某个日历的开关 | `Calendar`, `IsVisible` |
| `EventInvoked` | 日程被单击，或用 Enter 或空格键激活 | `Event` |
| `NewEventRequested` | 按下了 New event；`Handled` 会阻止日历创建草稿 | `Date`（聚焦日） |
| `EventCreated` | 新日程已保存，或撤销带回了一个已删除的日程 | `Event` |
| `EventUpdated` | 日程被移动、调整大小或编辑，或某次更改被撤销 | `Event`, `Previous` |
| `EventDeleted` | 日程被删除，或撤销移除了一个新日程 | `Event` |

## 无障碍

- **键盘。** Tab 依次到达工具栏（Today、上一个、下一个、视图切换、日历、New event）、月历（只有一个 Tab 停靠点；方向键在日期之间移动，Page Up 和 Page Down 在月份之间移动，Enter 选择一天）、日历开关、全天色条、网格（只有一个 Tab 停靠点：聚焦日），然后按时间顺序到达每个日程。
- **在网格中**，左右方向键移动到上一天或下一天；超过第一天或最后一天时，下一个范围会滑入，焦点落在其第一天或最后一天。在日程上，上下方向键移动到上一个或下一个日程，左右方向键移动到前一天或后一天。T 跳到今天，D 打开聚焦日，W 打开其所在的周。按 T、D 和 W 之后，以及范围滑动之后，焦点仍停留在聚焦日上。
- **编辑按键。** N 会在聚焦日的下一个空闲整点创建日程草稿，并打开快速创建弹层；在某一天上按 Enter 同样如此，在日程上按 Enter 会打开其详情，在快速创建的标题中按 Enter 则会保存。Delete 或 Backspace 会删除聚焦或选中的日程。Alt 配合上下方向键可将聚焦日程移动 15 分钟，Alt 配合左右方向键则移动一天；再加上 Shift 配合上下方向键，则改变其长度。焦点保留在该日程上。Ctrl+Z（Cmd+Z）撤销上一次更改。Escape 关闭弹层、取消拖动，然后清除选择。
- **弹层**是名为 "New event" 和 "Event details" 的对话框。焦点移入标题，Tab 会保持在弹层内，直到按下 Escape、Done 或 Cancel；之后焦点回到日程（若是已取消的草稿，则回到其所在的日期）。日历选择是一组单选按钮，只有一个 Tab 停靠点，方向键用于选择日历。删除后，焦点移到该日程所在的日期；在提示条上使用 Undo 之后，焦点回到日历。
- 网格是一个以其范围命名的表格（"Oct 19 to 25, 2026"），附有关于按键的帮助文字；每一天是一个单元格，名称如 "Thursday, October 22, today, 6 events"。日程是按钮，名称为 "title, day, start to end, calendar, place"；全天色条为 "title, all day, first day to last day, calendar"；议程行省略标题已说明的那一天。标题栏中的日期是名为 "Show Monday, October 19" 的按钮；月历中的日期则以 "Today" 作为状态。
- 日历开关是以日历名称命名的复选框；侧边栏名为 "Calendar navigation"。
- 一个礼貌式实时区域会播报范围变化（"Oct 26 to Nov 1, 2026"）和视图变化（"Work week, Oct 19 to 23, 2026"、"Day view, Thursday, October 22"），以及每一次编辑：新增、移动、调整大小或重新安排日程，新的日历或标题，删除、恢复、撤销、已取消的拖动和已清除的选择。提示条显示的也是同样的文字。所有这些文字都在 `WeekCalendarLabels` 中。
- 日历颜色从来不是唯一的提示：每个日程都会说明其日历的名称。

## 动效

- **范围**：标题行、全天行和网格在同一个弹簧上一起滑动一个宽度（临界阻尼，约 340 毫秒后剩余约 10%，约一秒后静止）。滑动过程中再次按下 next 时，每个窗格都会从其当前位置和速度继续运动；屏幕上同时最多有三个范围。跨越数周的跳转同样滑动一个宽度。
- **视图**：旧视图在 160 毫秒内淡出，新视图在 240 毫秒内淡入，均在原位完成。
- **全天行**在展开或折叠时，会在 240 毫秒内增高或收起。
- **悬停与焦点**：日程的填充、指针下方的半小时时段，以及标题日和月历中的日期，会在 160 毫秒内淡入。视图切换器的高亮在各选项之间滑动过渡。
- **拖动**：被拖动的日程在 15 分钟时段和日期之间跳动，使用快速的临界阻尼弹簧（约三分之一秒内静止），每次跳动都从其当前绘制位置重新开始；调整大小时长度逐个时段变化，没有动效。放下之后、在详情中编辑之后、键盘移动之后、撤销或删除之后，发生移动的日程会在 Smooth 弹簧上归位；已取消的拖动也会在该弹簧上弹回。
- **弹层**在 160 毫秒内淡入，同时从 0.96 开始在 Smooth 弹簧上放大，从面向其日程的那一边展开；关闭时在 120 毫秒内消失。**撤销提示条**在 Smooth 弹簧上上升 12 像素，并在 160 毫秒内离开，下沉 8 像素；新的更改会替换它，两者同时运动。触摸屏上的滑动会从手指松开的位置继续，沿范围弹簧运动。
- 减少动效会把范围滑动变为相同的交叉淡化，立即移动视图切换器的高亮，取消日程的跳动和弹簧（它们立即移动到位），并只保留弹层和提示条的淡入淡出；"Off" 则立即应用每一次更改。

## 相关组件

- `Calendar` 和 `DatePicker`，用于选择日期。
- `SegmentedControl`，单独用于视图切换。
- `Popover`，用于窄宽度下的日历列表。
- `Select`、`DatePicker` 和 `Input`，单独用于详情弹层中的字段。
- `Timeline`，用于按时间顺序排列的事件流。
