---
title: 复选框
summary: 二元选择，状态精确且清晰易读。
source-hash: 352309eb57b1
---

<Demo name="LabelAndDescription" />

## 何时使用

`Checkbox` 用于表单、设置面板或表格工具栏中，人做出的独立“是或否”选择。它自带标签与辅助文字，并提供半选状态，供子项只部分选中的父级行使用。凡是通过提交或保存确认选择、且勾选框的变化不会立即生效的地方，都适合使用它。

- **注册与结账中的同意条款**：“接受数据处理条款”，辅助文字说明同意涵盖什么。在点击“创建账号”继续之前，必须勾选该项。
- **管理控制台中的通知渠道**：父级行“全部渠道”下有邮件、Slack 和短信子项。当部分渠道开启时，父级显示半选状态，使汇总与明细一致。
- **数据表格中的行选择**：表头的复选框选中所有可见的发票；只选中部分行时显示为半选。表格在人执行批量操作时才应用选择。
- **设置表单中的可选项**：“将每张发票的副本发送给账单联系人”。该值随表单一起保存，辅助文字用一行说明其后果。
- **变更在保存时生效的列表筛选**：导出对话框中的一组状态，人可以勾选多个然后点击“导出”。

## 何时不用

- **立即生效的设置**，例如开启双因素登录或暂停发票邮件：使用 `Switch`，它让人感觉是即时的开或关。
- **从一组选项中恰好选择一个**，例如账单国家或套餐周期：使用 `RadioGroup`，选中一个会清除其他项。
- **以一排标签形式呈现、经常切换的筛选条件**，例如表格上方的“未结”“逾期”“已付”：使用 `ChipGroup`。标签紧凑，读起来像筛选器，而不是表单字段。
- **在两种状态之间切换、且会改变价格或内容的周期或视图**，例如按月和按年：使用 `BillingToggle` 或 `SegmentedControl`。
- **多个各自需要长描述和价格的独立选择**，例如套餐的附加项：人只选一个套餐时使用 `RadioCards`；选项彼此独立时，则在设置表单中使用一组带描述的复选框。

## 用法

标签即控件的内容。`Description` 会在标签下方添加一行辅助文字。该控件派生自 Avalonia 的 `CheckBox`，因此 `IsChecked`（`bool?` 类型，其中 `null` 表示半选）和 `IsThreeState` 的行为与 Avalonia 中一致。Arc 新增的部分包括 `CheckedState` 枚举、描述、无效与必填状态，以及 `CheckedStateChanged` 事件。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:Checkbox Content="同意条款和条件"
                  Description="你同意我们的服务条款与隐私政策。" />
</UserControl>
```

### 结构

1. **点击区域**：44 px 的方形（Arc 的控件尺寸）。整个方形都响应点击或触摸，它是该行的指针目标，即使绘制的方框更小。点击区域位于行的顶部，因此标签的第一行与方框对齐。
2. **方框**：18 px 的方形，圆角 5 px，边框 1 px。它在点击区域中居中。未勾选时，边框使用强边框色；悬停时加深为弱化文字色；无效（且未勾选）时使用危险色。
3. **填充与标记**：勾选或半选的方框以控件开启色填充。对勾或短横线是 1.75 px 的描边，颜色为控件图形色，使用圆形端点与连接。
4. **标签**（`Content`）：中等字重，可为一行或多行，行高 19.6 px。它位于点击区域之后，并为辅助技术提供控件名称。
5. **辅助文字**（`Description`）：极小号的弱化色文字，行高 16.8 px，位于标签下方 2 px。描述为空时隐藏。
6. **焦点环**：绕点击区域绘制的 2 px 环，用于键盘焦点。指针焦点时隐藏。

Arc 复选框没有尺寸属性：方框保持 18 px。复选框只会随标签与辅助文字变高，例如辅助文字为两行时。

### 必填与无效

`IsRequired` 在表单中将该字段标记为必填。它会向辅助技术报告为必填，但不绘制任何内容。`IsInvalid` 显示错误状态：方框未勾选时，其边框使用危险色。已勾选的方框永远不显示错误，因此人一勾选就应清除 `IsInvalid`，或保持其设置状态，让勾选后隐藏危险色边框。

<Demo name="States" />

```xml
<arc:Checkbox Content="同意条款和条件"
              Description="勾选此项以继续。"
              IsRequired="True"
              IsInvalid="True" />
```

### 校验

在大多数表单中，复选框只有一条规则：保存表单前必须勾选。控件会报告必填要求和危险边框，但何时显示它们由应用决定。在人能够处理问题的时刻显示必填状态，并把消息放在辅助文字中，让布局不会移动。

| 规则 | 何时检查 | 显示在哪里 |
|---|---|---|
| 必须同意 | 提交时，之后随方框变化而检查 | `IsInvalid`（危险边框）以及“勾选此项以继续。”之类的辅助文字 |
| 父子项保持一致 | 任一子项变化时 | 父级的 `CheckedState`；这不是错误 |
| 服务端拒绝该选择 | 请求结束后 | 辅助文字，方框变化时清除 |

```csharp
// 提交时：标记未勾选的方框并把焦点移到它上面。
acceptTerms.IsInvalid = acceptTerms.IsChecked != true;
if (acceptTerms.IsInvalid)
{
    acceptTerms.Focus();
    return;
}

// 人一勾选就清除标记。
acceptTerms.IsCheckedChanged += (_, _) => acceptTerms.IsInvalid = false;
```

### 半选的父项

`CheckedState` 是一个枚举，包含 `Unchecked`、`Checked` 和 `Indeterminate`。`IsChecked` 是同一个值的可空布尔形式，其中 `null` 表示半选。当部分子项被勾选时，将父项设为半选。

<Demo name="IndeterminateParent" />

```xml
<arc:Checkbox Content="全部渠道" CheckedState="Indeterminate" />
```

若要让点击父项时设置所有子项，请在父项上处理 `CheckedStateChanged`，并把每个子项的 `IsChecked` 设为新状态。在该处理程序中跳过半选状态，因为父项无法把子项设为“部分选中”。上面的示例还会根据子项更新父项，并用一个标志位保护两个处理程序，使它们不会相互调用。

点击半选的父项会将其勾选，这与 Radix 一致；点击已勾选的父项则会清除它。因此，像示例中那样设置子项的父项，在下一次点击时会把每个子项都设置好。当父项是表格或列表的“全选”控件时，应采用这种顺序。

### 三态循环

`IsThreeState="True"` 让点击在未勾选、勾选和半选之间循环，与 Avalonia 的 `CheckBox` 一致。只有当半选状态本身就是一个选择时才使用循环，例如列表中表示“两者皆可”的筛选。对于仅汇总子项的父项，保留默认的两态行为，即点击半选时勾选该方框。

<Demo name="TriState" />

### 绑定到视图模型

在 MVVM 应用中，将 `IsChecked` 绑定到 `bool?` 属性；当视图模型更偏好三个具名值时，则将 `CheckedState` 绑定到该枚举。两者都是双向的。半选的父项是一个计算值，因此视图模型应从子项推导它，而不是存储它。

```csharp
public partial class NotificationsViewModel : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AllChannels))]
    private bool _email = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AllChannels))]
    private bool _slack = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AllChannels))]
    private bool _sms;

    [ObservableProperty]
    private bool _acceptTerms;

    public CheckboxState AllChannels => (Email, Slack, Sms) switch
    {
        (true, true, true) => CheckboxState.Checked,
        (false, false, false) => CheckboxState.Unchecked,
        _ => CheckboxState.Indeterminate
    };

    [RelayCommand]
    private void SetAllChannels(CheckboxState state)
    {
        if (state == CheckboxState.Indeterminate)
        {
            return;
        }
        Email = Slack = Sms = state == CheckboxState.Checked;
    }
}
```

```xml
<StackPanel Spacing="8">
    <arc:Checkbox Content="全部渠道"
                  CheckedState="{Binding AllChannels, Mode=OneWay}" />
    <arc:Checkbox Content="邮件" IsChecked="{Binding Email}" Margin="28,0,0,0" />
    <arc:Checkbox Content="Slack" IsChecked="{Binding Slack}" Margin="28,0,0,0" />
    <arc:Checkbox Content="短信" IsChecked="{Binding Sms}" Margin="28,0,0,0" />
    <arc:Checkbox Content="接受条款和条件"
                  IsChecked="{Binding AcceptTerms}"
                  IsRequired="True" />
</StackPanel>
```

- 父项从 `AllChannels` 单向读取计算值。人点击父项只会改变控件本身，因此需要在代码后置中处理 `CheckedStateChanged`，并把 `e.NewState` 传给 `SetAllChannelsCommand`；随后子项更新视图模型。上面的示例在代码后置中完成了同样的工作。
- `IsChecked` 是双向的，因此子项方框会直接修改视图模型，`AllChannels` 通过通知随之更新。
- 编译绑定需要在视图上设置 `x:DataType`（为简洁起见，示例省略了它）。

### 事件

`CheckedStateChanged` 报告变化前后的状态。只有状态真正改变时才会触发，并且会冒泡，因此父面板上的处理程序也会收到它。`IsCheckedChanged`（继承自 Avalonia 复选框）会在每次 `IsChecked` 改变时触发，包括从 `true` 变为 `null` 的情况。

```csharp
checkbox.CheckedStateChanged += (_, e) =>
{
    Console.WriteLine($"{e.OldState} -> {e.NewState}");
};
```

### 示例：发票提醒的通知设置

SaaS 管理控制台中的账单设置页面。父级行“全部渠道”为半选状态，因为邮件和 Slack 已开启而短信已关闭，因此汇总与子项一致。条款复选框是必填项，人尚未接受，因此显示危险边框；审计日志这一行已禁用，因为产品始终会记录它。每个标签都说明勾选后会开启什么，每条辅助文字用一行说明其后果。

![包含半选父项复选框、必填条款复选框和禁用复选框的通知设置表单](images/InvoiceReminders.png)

```xml
<StackPanel Width="560" Spacing="14">
    <TextBlock Text="Invoice reminders"
               FontSize="16"
               FontWeight="SemiBold"
               Foreground="{arc:ArcDesignTokenResource Foreground}" />
    <arc:Checkbox Content="All channels"
                  CheckedState="Indeterminate"
                  Description="Email and Slack are on. SMS is off." />
    <StackPanel Margin="28,0,0,0" Spacing="6">
        <arc:Checkbox Content="Email" IsChecked="True" />
        <arc:Checkbox Content="Slack" IsChecked="True" />
        <arc:Checkbox Content="SMS" />
    </StackPanel>
    <arc:Checkbox Content="Send a copy to the billing contact"
                  Description="Copies go to billing@northwind.com."
                  IsChecked="True" />
    <arc:Checkbox Content="Accept the data processing terms"
                  Description="Check this box to turn on invoice reminders."
                  IsRequired="True"
                  IsInvalid="True" />
    <arc:Checkbox Content="Keep the audit log"
                  Description="Always on for this workspace."
                  IsChecked="True"
                  IsEnabled="False" />
</StackPanel>
```

### 文案规范

- **标签**：说明勾选后会开启或同意什么，使用句首大写的简短短语：“通过邮件发送发票提醒”“接受数据处理条款”。当复选框做的事情更具体时，避免只用“邮件”这样的单个名词。
- **辅助文字**：一句话说明后果或要求：“副本将发送至 billing@northwind.com”“勾选此项以继续”。数字用阿拉伯数字书写。
- **无效提示**：说明该做什么，而不是说某件事失败了。把它放在辅助文字中，因为 `IsInvalid` 只绘制危险边框。
- **父级标签**：使用子项的范围，例如“全部渠道”或“本页所有发票”，让父级读起来像是子项的汇总。

| 不要写 | 改为 |
|---|---|
| 邮件 | 通过邮件发送发票提醒 |
| 条款 | 接受数据处理条款 |
| 必填项！！！ | 勾选此项以继续。 |
| 是 / 否 | 将副本发送给账单联系人 |

### 最佳实践

**推荐**

- 对于随表单一起保存的独立选择使用复选框；对于立即生效的设置使用 `Switch`。人会预期复选框要等到保存时才生效。
- 标签与辅助文字要足够短，保持在一到两行内，让点击区域与标签对齐。
- 只要子项部分选中，就在父级行上显示半选状态，让汇总保持正确。
- 只在提交时标记必填的方框，并把焦点移到它上面，避免告诉人一个他们还没走到的字段。

**避免**

- 用复选框表示两个互斥选项之一。一对不能同时开启的复选框应改用 `RadioGroup`。
- 只用危险色说明错误。没有文字的红色边框不会告诉人任何信息。
- 无理由地禁用复选框。应在辅助文字中说明它为什么不能改变，就像审计日志那一行所做的。
- 在同一轮更新中既从父项自己的处理程序改变父项状态，又从子项改变父项状态；两次更新会相互调用。应像示例那样用标志位保护其中一个方向。

### 常见问题

**为什么点击半选的方框是勾选它而不是清除它？** 这遵循 Radix 和 WAI-ARIA 模式：半选的方框点击后会变为勾选。若父项应把子项设为与汇总相反的状态，就必须处理 `CheckedStateChanged` 并自行决定下一个状态。

**为什么按 Enter 不会切换复选框？** WAI-ARIA 复选框模式使用空格键。复选框不处理 Enter，因此该按键会到达窗口，由表单的默认按钮响应。

**`IsRequired` 与 `IsInvalid` 有什么区别？** `IsRequired` 告诉辅助技术该字段必须在表单提交前勾选，且不绘制任何内容。`IsInvalid` 在方框未勾选时绘制危险边框。应根据你自己的提交逻辑设置 `IsInvalid`。

**如何显示“是、否、任意”这样的三态选项？** 设置 `IsThreeState="True"`，使点击在未勾选、勾选和半选之间循环。用 `CheckedState` 存储该值，让视图模型读取这三个名称。

**方框没有标签时，它会报告什么名称？** 当 `Content` 为空且未设置名称时，复选框会报告“复选框”。对于没有可见标签的方框，例如表格中的行选择器，请设置 `AutomationProperties.Name` 为其命名。

## API 参考

<!-- api: Checkbox -->

### CheckboxState

| 成员 | 说明 |
|---|---|
| `Unchecked` | 方框为空。 |
| `Checked` | 方框显示对勾。 |
| `Indeterminate` | 部分但不是全部子项被勾选；对勾会变形为短横线。 |

### CheckboxStateChangedEventArgs

| 属性 | 类型 | 说明 |
|---|---|---|
| `OldState` | `CheckboxState` | 变化前的状态。 |
| `NewState` | `CheckboxState` | 变化后的状态。 |

## 无障碍

- 标签即复选框的名称。没有标签时，设置 `AutomationProperties.Name`；名称会回退为“复选框”。
- `Description` 是辅助文字。辅助技术会在名称之后朗读它。
- `IsRequired` 会向辅助技术报告该表单字段为必填。这不需要视觉标记。
- 半选状态会报告为混合状态。绘制的对勾与短横线不会单独暴露。
- 控件暴露复选框模式：`IsChecked` 为 null 时报告为半选，因此屏幕阅读器会把父级行读作半选。
- 空格键切换方框。与 WAI-ARIA 复选框模式一致，Enter 不切换。
- 键盘焦点绘制 2px 的轮廓，并偏移 2px。指针焦点不绘制。
- 无效边框只是视觉状态。应搭配说明需要修正什么的辅助文字，使消息不只依赖颜色。

### 键盘

| 按键 | 操作 |
|---|---|
| `Tab` / `Shift+Tab` | 将焦点移到下一个或上一个控件。禁用的复选框会被跳过。 |
| `Space` | 切换方框：未勾选变为勾选，勾选变为未勾选，半选变为勾选。使用 `IsThreeState` 时，循环顺序为未勾选、勾选、半选。 |
| `Enter` | 复选框上没有任何效果。该按键不被处理，会到达窗口，表单的默认按钮仍会运行。 |

## 动效

- 方框勾选时，填充从略小的尺寸放大出现；取消勾选时则快速淡出。
- 对勾逐步绘制出来。切换为半选时，对勾会变形为短横线，切回时则反向变形。
- 按下方框时会略微缩小，松开后弹回。
- 减弱动效时，所有状态变化立即生效，并取消按下缩放。关闭动效时效果相同。

| 变化 | 完整 | 减弱 | 关闭 |
|---|---|---|---|
| 方框被勾选 | 填充以灵敏的弹簧放大出现，对勾逐步绘制 | 立即生效 | 立即生效 |
| 方框被取消勾选 | 填充快速淡出 | 立即生效 | 立即生效 |
| 勾选与半选之间切换 | 对勾变形为短横线，或反向变形 | 立即生效 | 立即生效 |
| 方框被按下 | 方形立即缩放至 95%；松开后弹回 | 无按下缩放 | 无按下缩放 |
| 边框颜色变化（悬停、无效） | 边框颜色快速缓动 | 立即 | 立即 |

## 相关组件

- `Switch`：用于立即生效的设置。
- `RadioGroup`：用于从一组选项中只选一个。
- `ChipGroup`：用于用户经常切换的一组筛选条件。

| 组件 | 适用场景 |
|---|---|
| `Switch` | 人改变单个设置时，该设置即时生效 |
| `RadioGroup` | 从一组选项中选择恰好一个 |
| `RadioCards` | 恰好选择一个套餐，且每个选项都需要价格和描述 |
| `ChipGroup` | 一排筛选条件位于列表或表格上方，且经常切换 |
| `BillingToggle` | 以节省说明选择计费周期 |
| `SegmentedControl` | 选项切换视图，且没有需要保存的值选择 |
