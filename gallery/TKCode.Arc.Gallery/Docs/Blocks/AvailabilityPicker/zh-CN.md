---
title: Availability picker
summary: 在多人的日历中找出会议时间，查看每个时段谁有空，并预订合适的那一个。
source-hash: 5ab1af858d62
---

<Demo name="Preview" />

## 何时使用

`AvailabilityPicker` 帮助一个人为一个群体选择会议时间。一排日期卡片显示每天有多少个候选时间适合所有人；所选日期会列出它的时间，每个时间为每位参会人显示一个标记，空闲时为绿色，忙碌时为灰色；页脚概括当前的选择并完成预订。选择另一天时，高亮会滑到那里，时间列表从那一侧滑入，并自动选中第一个对所有人都合适的时间。

- **从应用自身的数据安排会议。** 面试安排、设计评审或客户电话，你的服务已经知道参会人的日历，并提出几个候选时间。
- **带确认步骤的预订流程。** 读者比较几天，选定一个时间并确认；更改选择会撤销确认，因此改变主意无需额外按钮。
- **可能让某人错过会议的时间。** 有忙碌参会人的时间仍然可以预订。该行会标明谁忙碌，页脚会说明谁需要调整会议。

## 何时不用

- **选择单个日期。** 请使用 `Calendar` 或 `DatePicker`。
- **输入任意时间。** 请使用 `TimePicker`；选择器只提供你给出的时间。
- **回复邀请。** 选择器为一个群体预订时间；显示并回复单个邀请是另一种界面。
- **长时间范围。** 日期条以等宽的列显示每一天，适合一个工作周（五到七天）。若要显示一个月，请让读者在别处选择周，再把那一周交给选择器。

## 用法

### 日期、时间和参会人

`Attendees` 是受邀的人，类型为 `ProjectPerson`：标题显示他们的头像，每个时间按此顺序为每位参会人显示一个标记。`Days` 是 `AvailabilityDay`，包含 `Date` 和它的 `Slots`。每个 `AvailabilitySlot` 有 `Start`（一天中的时刻）、可选的 `End`（否则为开始时间加上 `Duration`），以及该时间的 `BusyAttendees`。时间按开始时刻排序显示。一天的 `OpenCount` 显示在它的卡片上，统计对所有人都合适、可以预订的时间数量。

```csharp
var noor = new ProjectPerson("Noor Haddad", "noor");
var tomas = new ProjectPerson("Tomás Ferreira", "tomas");
var ingrid = new ProjectPerson("Ingrid Solberg", "ingrid");
picker.Attendees.AddRange([noor, tomas, ingrid]);

var monday = new DateOnly(2026, 3, 9);
picker.Days.Add(new AvailabilityDay(monday,
    new AvailabilitySlot(new TimeSpan(9, 0, 0)),
    new AvailabilitySlot(new TimeSpan(11, 0, 0)),
    new AvailabilitySlot(new TimeSpan(13, 30, 0), tomas),
    new AvailabilitySlot(new TimeSpan(15, 30, 0))));
```

```xml
<arc:AvailabilityPicker x:Name="Picker"
                        Title="Roadmap check-in"
                        Duration="0:30:00"
                        TimeZoneName="Lisbon time" />
```

`Title` 是标题；标题下方的一行是时长和 `TimeZoneName`（"30 min · Lisbon time"），或者你自己的 `Subtitle`。对于你自己管理的集合，请使用 `DaysSource` 和 `AttendeesSource`：`ObservableCollection` 会被监听，对某一天、某个时间或某个人的修改会立即显示。时间的 `Note` 会替换它自动生成的那一行；`IsEnabled="False"` 会让无法预订的时间变暗（已被占用、已过去），并使其不能被选中。

### 选择

`SelectedDay` 和 `SelectedSlot` 是双向的。起初选择器选中第一天（或 `SelectedSlot` 所在的那一天）；除非你设置了时间，它还会自动选中一个时间。当日期改变且所选时间不在该日时，`AutoSelectSlot` 决定如何处理：`FirstEveryoneFree`（默认）选择第一个对所有人都合适的时间，若没有则选择第一个可预订的时间；`First` 选择第一个可预订的时间；`None` 则把选择交给读者。选择不会按天分别记忆。

将 `SelectedSlot` 设为另一天的时间会同时选中那一天。`SelectDay` 和 `SelectSlot` 在代码中做同样的事；`SelectDay(day, animate: false)` 则无动效地移动。

`SelectedDayChanged` 报告旧日期和新日期、`Direction`（1 表示更晚，−1 表示更早）以及随之选中的时间；`SelectedSlotChanged` 报告旧时间和新时间。两者都会说明这一改变是否由读者发起（`IsUserInitiated`）。

<Demo name="Bound" />

### 预订

按钮用于预订所选项：先触发 `ConfirmRequested`，它可以通过 `Cancel` 取消（例如可用性检查失败、缺少权限）；然后 `IsConfirmed` 变为 true，触发 `Confirmed`，并执行 `ConfirmCommand`。它们的参数包含日期、时间、实际的 `Start` 和 `End`、`Conflicts`（忙碌的参会人），以及页脚显示的文字。预订后，按钮显示带对勾的 "Booked"，仍可获得焦点，但不执行任何操作；选择另一天或另一时间会撤销预订并触发 `ConfirmationReset`。`Confirm` 和 `ResetConfirmation` 在代码中做同样的事。

```csharp
picker.ConfirmRequested += (_, e) =>
{
    // Someone took the room in the meantime: keep the selection unbooked.
    e.Cancel = !IsRoomFree(e.Start, e.End);
};
picker.Confirmed += (_, e) => SendInvites(e.Start, e.End, e.Conflicts);

static bool IsRoomFree(DateTime start, DateTime end) => true;
static void SendInvites(DateTime start, DateTime end, IReadOnlyList<ProjectPerson> conflicts) { }
```

在代码中设置 `IsConfirmed` 会显示已预订的状态，但不会播报，这适合恢复之前完成的预订。

### 带框或扁平

`IsFramed`（默认值）会为选择器绘制带边框的圆角表面，时间上方有一条线，页脚使用弱化的颜色，适合与其他内容并列的选择器。当它占满整页或独立的面板时，请将其设为 false。

<Demo name="Flat" />

### 窄容器

选择器读取的是它自身的宽度，而不是窗口的宽度。宽度低于 520 时，内边距收紧，日期卡片变小，时间变为单列，页脚堆叠在全宽按钮之上；宽度为 360 及以下时，图例和卡片上的数量会被隐藏。`IsLegendVisible` 可以在任何宽度下隐藏图例。

<Demo name="Narrow" />

### 更复杂的数据

当某一天的每个时间都与某人冲突时，选择器会选中该天第一个可预订的时间，页脚会说明谁需要调整会议。多名忙碌的参会人会以简短的名字列出（"Noor and Ingrid are busy"）。行数较少的日期，列表高度会以弹簧动画变为新的尺寸；没有时间的日期则以一行文字代替列表。

<Demo name="TrickyData" />

### 文本

每段文字都有英文默认值，也都有对应属性：按钮（`ConfirmText`、`ConfirmedText`）、各行文字（`EveryoneFreeText`、`BusyFormat`、`BusyManyFormat`、`ConflictFormat`、`ConfirmedLabelText`、`NoSlotText`、`NoSlotsText`）、数量和图例（`OpenCountFormat`、`FreeLegendText`、`BusyLegendText`、`AndWord`）、日期和时间格式（`DayNameFormat`、`DayNumberFormat`、`DayLabelFormat`、`TimeFormat`、`RangeFormat`、`SelectionFormat`、`DurationFormat`、`SubtitleFormat`），以及屏幕阅读器朗读的内容（`AttendeesAutomationFormat`、`WeekAutomationFormat`、`DayAutomationFormat`、`TimesAutomationFormat`、`SlotAutomationFormat`、`ConfirmedAnnouncementFormat`）。日期和时间使用当前文化进行格式化。

```xml
<arc:AvailabilityPicker Title="Point d’équipe"
                        ConfirmText="Réserver ce créneau"
                        ConfirmedText="Réservé"
                        EveryoneFreeText="Tout le monde est libre"
                        BusyFormat="{}{0} est occupé"
                        DayLabelFormat="{}{0:dddd d MMMM}"
                        RangeFormat="{}{0} à {1}" />
```

## API 参考

<!-- api: AvailabilityPicker -->

<!-- api: AvailabilityDay -->

<!-- api: AvailabilitySlot -->

<!-- api: AvailabilityDayChangedEventArgs -->

<!-- api: AvailabilitySlotChangedEventArgs -->

<!-- api: AvailabilitySelectionEventArgs -->

<!-- api: AvailabilityConfirmRequestedEventArgs -->

## 无障碍

- **键盘。** 每张日期卡片、每个时间和按钮都是 Tab 停靠点，按阅读顺序排列：日期从左到右，时间逐行，然后是按钮。Enter 或空格键选择获得焦点的日期或时间，或预订所选项；选择时焦点保持在原处，预订后焦点停留在按钮上，此时按钮不执行任何操作。不使用方向键。
- 选择器是一个以标题命名的群组，标题是二级标题；时间上方的日期是三级标题。
- 头像是一张图片，名称为 "Invited: Noor Haddad, Tomás Ferreira and Ingrid Solberg"。
- 日期条是一个名为 "Days from 9 March" 的群组。每张卡片是一个切换按钮，其按下状态即为所选项，名称包含日期以及适合所有人的时间数量。
- 时间是一个名为 "Times for Monday, 9 March" 的列表。每个时间是一个切换按钮，名称包含其时间范围和该行的文字（"13:30 to 14:00, Tomás is busy"）；无法预订的时间被禁用。标记和对勾不会被朗读。
- 预订会以礼貌的方式播报（"Booked Monday, 9 March, 13:30 to 14:00."）；撤销预订的新选择会清除这一播报。日期和时间的改变不作播报：获得焦点的控件已经说明了它们。

## 动效

- **日期高亮**：所有卡片下方的一块填充沿着柔和的弹簧（刚度 340，阻尼 34，无过冲）滑到所选日期。卡片的文字和边框在 240 ms 内改变颜色；被按下的卡片缩小到 97%（高亮也随之缩小）。
- **时间**：新列表从移动方向一侧 16 px 处以平滑弹簧滑入，并在 160 ms 内淡入；旧列表则向另一侧离开 16 px，同时在 160 ms 内淡出。快速切换会叠加：正在进入的列表会从当前位置转向。当新日期的行数不同时，高度以弹簧变化。
- **选择**：所选时间的填充和边框在 160 ms 内改变；它的对勾从右侧滑入，使用临界阻尼弹簧，并把标记推向一旁，同时在 160 ms 内淡入。被按下的时间缩小到 98%。
- **文字**：标题和页脚的两行仅在内容改变时切换：旧文字在 160 ms 内上升 4 px、模糊到 2 px 并淡出，新文字从下方 4 px 处上升，并从模糊中清晰显现。
- **按钮**：其文字和宽度随按钮自身的模糊切换和宽度弹簧而变化。
- 减少动效会立即移动高亮，立即移除旧列表并在 120 ms 内淡入新列表，并以 120 ms 交叉淡化文字；颜色和对勾立即改变。关闭动效则立即应用每一次改变。

## 相关组件

- `Calendar` 和 `DatePicker`，用于选择单个日期。
- `TimePicker`，用于输入时间。
- `Button`，选择器用它来预订。
- `GlidingIndicator`，日期高亮背后的基础组件。
