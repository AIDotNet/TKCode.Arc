---
title: 时间选择器
summary: 以合理的键盘行为选择时间。
source-hash: 1f9b0e384694
---

<Demo name="TimeField" />

## 何时使用

`TimePicker` 从等间隔的列表中选择一个一天中的时刻。列表每隔几分钟生成一次（默认 15 分钟），因此任一时刻与其相邻时刻之间只相差一步。它用于时刻本身就是独立值的表单中：会议时段、提醒、班次开始、取件时段。值是 `TimeOnly`，时钟则按使用者所在文化的格式显示它。

- **会议与预约时段**：日历邀请或门诊预约中，时刻是固定的步长，例如 8:00 到 18:00 之间每 15 分钟一次。列表只提供网格支持的开始时刻，因此人们选择的时间都是预约系统可以接受的。
- **提醒与定时任务**：管理后台中“在以下时间发送每周摘要”的设置。值是没有日期的一天中的时刻，选择器会以使用者所读的时钟形式显示它。
- **班次与可用时段编辑器**：人员排班工具中，班次从 06:30 开始、14:30 结束。将两个选择器配合 `MinuteStep="30"` 使用，并校验结束时间晚于开始时间。
- **日期与时间表单**：配送时段或活动开始时间，把负责日期的 `DatePicker` 与负责时刻的 `TimePicker` 组合在一起。两个值分别存储，在代码中合并。

## 何时不用

- **命名时刻的短列表，例如“早上”或“晚上”**：用 `Select`。命名列表不需要滚动的时刻列表，而且“早上”并没有唯一的 `TimeOnly` 值。
- **日期**：用 `DatePicker`；当月份必须保持在页面上可见时，用 `Calendar`。
- **日期范围**：用 `DateRangePicker`。整日的范围没有一天中的时刻，也没有步长列表。
- **需要精确到秒的自由输入时刻**：选择器是步长列表。当人们必须输入秒数或步长之间的时刻时，用 `Input` 加校验规则，并在视图模型中解析文本。
- **时长，例如 90 分钟的会议或 2 小时的计时器**：用 `NumberField` 输入分钟数。时长不是一天中的时刻，列表会暗示一个并不适用的时钟。

## 用法

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:TimePicker Label="会议时间"
                    Description="每 15 分钟一个时刻。"
                    SelectedTime="{Binding MeetingTime, Mode=TwoWay}" />
</UserControl>
```

`TimePicker` 派生自 Avalonia 的 `TemplatedControl`，并实现 Arc 选择器的自动化行为。触发器是位于 `Field` 中的 `FieldFrame`，因此标签、说明与错误行与输入框和日期选择器来自同一基础。列表是由控件自己拥有的弹层中绘制的面板。它只绘制可见的行，因此即使是 1 分钟的步长（1440 个选项）也不会创建 1440 个控件。由于控件是一个列表，它不解析输入的文字：只有选中某一行或代码设置 `SelectedTime` 时，值才会改变。

### 结构

1. **标签**（`Label`）：位于触发器上方，采用字段标签的样式。它同时是字段的无障碍名称。设置 `IsLabelVisible="False"` 时标签隐藏，但仍为字段命名。
2. **触发器外框**（由 `TimePicker.FramePart` 指定的模板部件）：字段表面上的 1 px 边框。其高度由 `Size` 决定：`Small` 为 36 px，`Medium`（默认）为 44 px，`Large` 为 50 px。悬停、按下和打开时，边框变为强边框色，背景变为弱化的表面色；无效字段显示危险色边框。
3. **时钟图标**：16 px，弱化文字颜色，与值之间有 9 px 间距。
4. **值**（由 `TimePicker.ValuePart` 指定的模板部件）：时刻以 14 px 文字显示，行高为 19.6 px（14 px × 1.4）。`Large` 使用基础字号。没有时刻时，以弱化文字颜色显示 `Placeholder`。
5. **箭头**：16 px，弱化文字颜色。列表打开时旋转 180 度。
6. **说明**（`Description`）与**错误行**（`ErrorMessage`）：外框下方的帮助文字与错误文字。非空的 `ErrorMessage` 会使字段无效，外框显示危险色边框。
7. **列表**（由 `TimePicker.PopupPart` 指定的模板部件）：在触发器下方 6 px 处打开，宽度与触发器相同。它有 1 px 边框、面板圆角、5 px 内边距和抬起的表面。高度最多为 `MaxDropDownHeight`（默认 250 px），超出后滚动。
8. **选项行**：高 36 px，内缩 12 px，并带圆角。指针或键盘所在的行获得弱化表面色。已选时刻的右侧边缘有一个 6 px 的圆点，因此任何时候只有一行会被标记为已选。

### 步长与时钟

`MinuteStep` 设置选项之间的间隔。默认为 15 分钟，Arc 支持 1、5、10、15 或 30。选项数量为 1440 除以步长后向上取整。

| `MinuteStep` | 选项数 | 适用场景 |
|---|---|---|
| 5 | 288 | 通话时段和短预约 |
| 15 | 96 | 会议、提醒和大多数日程（默认） |
| 30 | 48 | 班次开始和营业时间 |

`ClockFormat` 决定显示方式。`Auto`（默认）遵循 `Culture` 的短时间格式：若某个文化以 “H” 表示小时，则显示 24 小时制；其他文化显示 12 小时制，并带上 AM 和 PM。`TwelveHour` 显示 “9:15 AM”，`TwentyFourHour` 无论文化如何都显示 “09:15”。AM 与 PM 的文字来自文化设置；若文化没有提供，则退回为 AM 与 PM。无论显示方式如何，值始终是 `TimeOnly`。

<Demo name="StepsAndClocks" />

在服务多个地区的应用中使用 `Auto`，让每个人都以自己书写的方式读取时刻。在运营控制台、时刻表或日志视图中，若 24 小时制是标准，则强制使用 `TwentyFourHour`。只有当产品文案在所有地方都是 12 小时制时，才使用 `TwelveHour`，因为混合显示会让日程令人困惑。

### 空值与默认值

`SelectedTime` 是 `TimeOnly?`。Arc 的默认值为 09:00（属性初始为 9:00 AM），因此即使没有选择，新的选择器也会显示一个时刻。值为 `null` 时，字段显示 `Placeholder`，其默认值为 `Select a time`。对于必须由人设置的字段，使用 `null`；只有当某个时刻通常都是正确的时候，才设置真正的默认值。

```xml
<arc:TimePicker Label="提醒" Placeholder="选择时间" SelectedTime="{x:Null}" />
```

```csharp
picker.SelectedTime = new TimeOnly(18, 30);
picker.SelectedTimeChanged += (_, e) =>
{
    status.Text = e.NewTime is { } time ? $"已设为 {time:HH:mm}" : "无时间";
};
```

不在步长上的时刻会被原样保留：步长为 15 分钟时，`09:07` 在触发器上显示为 `9:07 AM`，列表则高亮位于它当前或之前的选项 09:00。只有从列表中选择的时刻才会落在步长上。

<Demo name="SizesAndStates" />

### 尺寸与状态

`Size` 设置触发器的高度：`Small` 为 36 px，`Medium` 为 44 px（Arc 的外观），`Large` 为 50 px。同一表单保持一种尺寸，使时刻字段与旁边的输入框和日期字段对齐。非空的 `ErrorMessage` 会将字段标记为无效；`IsEnabled="False"` 会禁用它：字段仍然可见并显示其值，但无法打开。

### 绑定到视图模型

视图模型拥有时刻以及与之相关的规则。使用 CommunityToolkit.Mvvm 时：

```csharp
public partial class ShiftViewModel : ObservableObject
{
    private readonly IShiftService _shifts;

    public ShiftViewModel(IShiftService shifts)
    {
        _shifts = shifts;
    }

    [ObservableProperty]
    private TimeOnly? _shiftStart = new TimeOnly(6, 30);

    [ObservableProperty]
    private TimeOnly? _shiftEnd = new TimeOnly(14, 30);

    [ObservableProperty]
    private string? _shiftEndError;

    partial void OnShiftStartChanged(TimeOnly? value) => ValidateShift();

    partial void OnShiftEndChanged(TimeOnly? value) => ValidateShift();

    private void ValidateShift()
    {
        ShiftEndError = ShiftStart is { } start && ShiftEnd is { } end && end <= start
            ? $"结束时间必须晚于开始时间 {start:HH:mm}。"
            : null;
    }

    [RelayCommand]
    private Task SaveShiftAsync()
    {
        if (ShiftStart is not { } start || ShiftEnd is not { } end || ShiftEndError is not null)
        {
            return Task.CompletedTask;
        }
        return _shifts.SaveAsync(start, end);
    }
}
```

```xml
<StackPanel Width="560" Spacing="20">
    <arc:TimePicker Label="班次开始"
                    SelectedTime="{Binding ShiftStart, Mode=TwoWay}"
                    MinuteStep="30"
                    Width="240"
                    HorizontalAlignment="Left" />
    <arc:TimePicker Label="班次结束"
                    SelectedTime="{Binding ShiftEnd, Mode=TwoWay}"
                    ErrorMessage="{Binding ShiftEndError}"
                    MinuteStep="30"
                    Width="240"
                    HorizontalAlignment="Left" />
    <arc:Button Content="保存班次" Variant="Primary" Command="{Binding SaveShiftCommand}" />
</StackPanel>
```

`SelectedTime` 是唯一承载值的属性，且默认即为双向绑定，因此显式的 `Mode=TwoWay` 可以省略。当值因选择或代码而改变时，`SelectedTimeChanged` 会携带旧时刻与新时刻触发；它适合用于更新摘要等副作用，其他情况下直接绑定值即可。控件本身没有命令。编译绑定需要在视图上设置 `x:DataType`；示例为简洁起见省略了它。

### 校验

时刻依据排班规则进行校验，而不是由选择器校验。把每条规则放在视图模型中，并把第一条消息显示在与之相关的字段下方的 `ErrorMessage` 中。

| 规则 | 何时检查 | 显示位置 |
|---|---|---|
| 结束晚于开始 | 任一时刻变化时 | 结束字段上的 `ErrorMessage` |
| 在营业时间之内 | 时刻变化时 | 时刻所属字段上的 `ErrorMessage` |
| 必填 | 提交时，之后在值为 `null` 期间持续检查 | `ErrorMessage`；把焦点移到第一个空字段 |
| 服务器拒绝该时刻 | 请求之后 | 所涉字段上的 `ErrorMessage`；值变化后清除 |

如 `OnShiftStartChanged` 与 `OnShiftEndChanged` 处理函数所示，在值变化时清除消息，使错误不会停留在已经修正的值上。

### 焦点与关闭

列表打开时，焦点保留在触发器上。列表是一个带轻触关闭的弹层，因此在它之外按下指针会关闭列表。选择某一行或按下 Escape 同样会关闭列表；无论哪种方式，焦点都保留在触发器上，其他内容不会移动。在某一行上按 Enter 或 Space 会选中它并关闭列表，因此键盘用户不需要单独的确认按钮。

### 由代码驱动

列表由 `MinuteStep` 生成，因此在选择器关闭时更改步长或文化，会重新生成列表。当页面需要选项的文字形式时，可以从代码中读取：

```csharp
var labels = new List<string>();
for (var i = 0; i < picker.OptionCount; i++)
{
    labels.Add(picker.FormatTime(picker.OptionAt(i)));
}
```

`FormatTime` 返回触发器与列表在当前 `ClockFormat` 和 `Culture` 下使用的文字，因此摘要行可以与选择器完全一致。要把时刻与 `DatePicker` 的值合并，请在应用中使用日期自身的转换方法：`date.ToDateTime(time)`。

### 示例：班次开始与结束

一个人员排班工具，经理以半小时为步长设置班次的开始与结束。两个字段共享尺寸与宽度；结束字段在不晚于开始时间时显示错误；开始字段的说明解释了步长的含义。保存按钮是唯一的操作，因此表单只有一个主按钮。

![班次开始与结束时间，结束时间带有错误提示](images/ShiftTimes.png)

```xml
<StackPanel Width="560" Spacing="20">
    <arc:TimePicker Label="Shift start"
                    Description="Shifts start on the hour or the half hour."
                    SelectedTime="06:30"
                    MinuteStep="30"
                    Width="240"
                    HorizontalAlignment="Left" />
    <arc:TimePicker Label="Shift end"
                    ErrorMessage="End must be after the start, 06:30."
                    SelectedTime="06:00"
                    MinuteStep="30"
                    Width="240"
                    HorizontalAlignment="Left" />
    <arc:Button Content="Save shift" Variant="Primary" HorizontalAlignment="Left" />
</StackPanel>
```

### 文案规范

以事件或数值为字段命名：`Meeting time`、`Shift start`、提醒。预留位置文字保持为简短的动词短语，说明文字则应陈述与步长相关的事实，而不是重复标签。

| 不要写 | 改为 |
|---|---|
| `Time` | `Meeting time`、`Shift start`、提醒 |
| `Pick a time please` | `Select a time`（默认预留位置文字） |
| `Times are 15 min apart.` | `Times every 15 minutes.` |
| `Invalid time` | `End must be after the start, 06:30.` |
| `12:00 AM` in a 24-hour product | `00:00` |

错误消息应写明规则与修正方法：说明时刻必须是什么，而不只是说它错了。

### 最佳实践

**推荐**

- 用 `MinuteStep` 表示日程的实际粒度。15 分钟的列表适合会议，而 30 分钟的列表能防止人们选择业务并不提供的时刻。
- 让 `Size` 与表单中的其他字段相同，使日期与时刻并排的一行保持对齐。
- 对于必须由人选择的值使用 `null` 并显示预留位置文字，这样默认的 09:00 就不会被误存。
- 在视图模型中校验范围，并把消息显示在与之相关的字段上，让人知道该修改哪个时刻。
- 产品服务多个地区时使用 `Auto`；领域使用 24 小时制时使用 `TwentyFourHour`。

**避免**

- 用选择器来表示命名时段的列表。“早上”或“晚上”之类的条目应使用 `Select`，因为时间选择器暗示了这些时段并不具备的时钟。
- 把时刻存为文字。值是 `TimeOnly`；只为显示而格式化它，这样比较与排序才能保持正确。
- 使用 Arc 定义之外的步长（1、5、10、15 和 30）。坚持使用它们，让列表读起来像人们熟悉的时钟。
- 只在提交时才显示错误，而规则其实可以在时刻变化时就检查。字段旁边的消息能让人立即修正班次结束时间。
- 期望日期与时刻共用一个值。控件只保存 `TimeOnly`，因此应把日期保存在 `DatePicker` 中，并在代码中用 `date.ToDateTime(time)` 将两者合并。

### 常见问题

**为什么我没有设置时刻，字段却显示 9:00 AM？** 控件的默认值是 09:00，Arc 使用它以便字段始终显示为一个时刻。把 `SelectedTime` 设为 `null`，即可改为显示预留位置文字。

**人们可以输入时刻吗？** 不可以。触发器会打开一个列表，只有选中某一行或代码设置 `SelectedTime` 时，值才会改变。需要输入时，请对输入框加上规则。

**时刻不在步长上时会怎样？** 值会保持设置时的样子，因此 `09:07` 保持为 `09:07`，显示为 `9:07 AM`。列表高亮位于该时刻当前或之前的选项，即步长为 15 分钟时的 09:00。如果业务需要精确的步长，请在视图模型中取整。

**如何把值存为 “HH:mm” 文字？** 值是 `TimeOnly`，因此文字只是它的一种格式：在应用中使用 `SelectedTime?.ToString("HH:mm")`，或用 `picker.FormatTime(time)` 获取显示文字。

**列表会滚动页面吗？** 不会。列表会自行滚动，打开时会把选中的时刻居中，页面不会移动。

## API 参考

<!-- api: TimePicker -->

## 无障碍

- `Label` 为字段命名。`Description` 与 `ErrorMessage` 是帮助文字。
- 列表打开时焦点保留在触发器上。触发器会报告其展开状态与值，因此屏幕阅读器会在列表变化时播报当前时刻。
- 触发器以组合框的形式公开，名称为标签，值为格式化后的时刻；没有时刻时则为预留位置文字。
- 按 Enter、Space 或 ArrowDown 打开列表，已选时刻位于中央并处于激活状态。ArrowUp 与 ArrowDown 在各时刻之间移动，并在两端循环。
- 按 Escape 关闭列表。在列表之外按下指针也会关闭它。
- 键盘焦点在触发器上显示焦点环。

### 键盘

| 按键 | 操作 |
|---|---|
| `Enter` / `Space`（列表关闭时） | 打开列表，并激活已选时刻 |
| `Enter` / `Space`（列表打开时） | 选中激活的时刻并关闭列表 |
| `ArrowDown` / `ArrowUp`（列表关闭时） | 打开列表 |
| `ArrowDown` / `ArrowUp`（列表打开时） | 向下或向上移动激活的时刻，从最后一个时刻回到第一个，反之亦然 |
| `Home` / `End`（列表打开时） | 移到第一个时刻（12:00 AM 或 00:00）或当天的最后一个时刻 |
| `Escape`（列表打开时） | 关闭列表，焦点保留在触发器上 |

指针的行为与键盘相同：指向某一行会使其激活，点击则选中它。

## 动效

- 值朝时间的方向滚动：较晚的时刻从下方升起，较早的时刻从上方落下。
- 列表以短促的弹簧从触发器中展开并淡入，关闭时几乎立即消失。
- 激活行会迅速改变背景，一个小圆点标记已选时刻，因此任何时候只有一行被高亮。
- 减弱动效时，值以交叉淡化切换，列表只做淡入淡出。关闭动效时直接到达终态。

| 变化 | 完整 | 减弱 | 关闭 |
|---|---|---|---|
| 值变化 | 时刻朝时间的方向滚动，并带有柔和的模糊 | 交叉淡化 | 立即完成 |
| 列表打开 | 从触发器展开，从上方 6 px、缩放 0.97 变为完整大小，使用迅捷的弹簧并伴随快速淡入 | 淡入 | 立即完成 |
| 列表关闭 | 上移 4 px、缩放至 0.98，然后几乎立即关闭 | 淡出 | 立即完成 |
| 激活行随键盘移动 | 列表以平滑弹簧滑动，使该行保持在视野之内 | 立即定位 | 立即完成 |

## 相关组件

- `DatePicker`：将日期与时刻配对。
- `Select`：短的命名选项列表。
- `DateRangePicker`：日期范围。

| 组件 | 适用场景 |
|---|---|
| `DatePicker` | 值是一天，时刻单独存储或在代码中合并 |
| `DateRangePicker` | 值是整日的范围 |
| `Select` | 选项是命名时刻或时段的短列表 |
| `Input` | 人们必须输入列表无法表达的值，并配有校验规则 |
| `NumberField` | 值是以分钟计的时长，而不是一天中的时刻 |
