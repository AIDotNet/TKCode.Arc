---
title: 开关
summary: 用于即时生效设置的触感式开关。
source-hash: 04692a5a319a
---

<Demo name="SettingsList" />

## 何时使用

开关用于打开或关闭一项设置，使用者一做出更改，设置立即生效。它属于设置页、偏好设置面板或账号区域，其中每一行都是可以单独修改的设置，无需等待保存按钮。滑轨与滑块一眼即可显示状态，使用者无需两次阅读标签就能看清当前设置。

- **管理控制台中的通知偏好**：“支付失败时发邮件通知我”或“发送每周营收摘要”。每个开关都会立即应用其设置，页面无需确认步骤。
- **SaaS 工作区中的功能可用性**：“启用测试版报告”或“显示已归档项目”。开关是该行中唯一的控件，因此标签说明了改变的内容。
- **账号页面中的隐私与数据共享**：“与产品团队共享使用数据”。开关为使用者提供清晰的开或关选择，并显示当前状态。
- **每行一个开关的设置列表**：通知页面中，每个渠道（例如邮件、短信或应用内通知）各有一个开关和各自的描述。
- **开发者工具中的运维开关**：“暂停此项目的部署”或“将日志镜像到审计流”。效果会立即开始，因此不能用于需要审核步骤的场景。

## 何时不用

- **需要通过提交按钮确认的选择**，例如接受条款或在结账时选择加入某个套餐：用 `Checkbox`，其值只在表单提交时才保存。
- **在具名选项之间做选择**，例如日、周或月：短视图用 `SegmentedControl`，每个选项都需要描述时用 `RadioGroup`。
- **使用者可以选择多个选项的情况**：为每个选项使用一个 `Checkbox`，让表单读起来是一组选择，而不是一组含相互依赖含义的开关。
- **立即执行的操作**，例如“重启服务”或“发送发票”：用 `Button`。开关表示一种状态，按钮表示一个动作。
- **在生效前需要确认或说明理由的设置**，例如停用单点登录：由执行变更的操作弹出对话框，而不是用一个立即应用变更的开关。

## 用法

`Content` 是开关的标签。整个标签都属于点击区域。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:Switch Content="邮件通知" IsChecked="True" />
</UserControl>
```

`Switch` 派生自 Avalonia 的 `ToggleButton`，因此 `IsChecked`、`IsCheckedChanged`、`Content` 以及切换按钮的点击和键盘行为都保留着。控件在此之上增加了滑轨与滑块的设计、滑块的按压拉伸以及 Arc 动效。状态是二值的：`IsChecked` 为 `true` 或 `false`，开关没有混合状态。

### 结构

1. **滑轨**（42 × 24 px，全圆角）：滑块移动的底面。关闭时为控件的滑轨颜色，关闭时悬停为悬停滑轨颜色，开启时为强调色（`ControlOn`）。状态变化时淡变为强调色。
2. **滑块**（18 px，全圆角，带 Arc 滑块阴影）：位于滑轨内侧 3 px。从关闭位置到开启位置移动 18 px。关闭时为滑块颜色，开启时为开启滑块颜色。
3. **按下的滑块**：指针或空格键按住时，滑块向另一侧加宽到 23 px，其远端边缘保持不动。这是按下的反馈，并显示滑块将往哪一侧移动。
4. **标签**（`Content`）：位于滑轨右侧 12 px，为小号字号和前景色。`Content` 为空时隐藏。
5. **焦点环**：仅用于键盘焦点的 2 px 环，其内边缘位于滑轨外侧 2 px 处。

开关的最小高度为 Arc 中号控件尺寸。禁用的开关淡化为 50% 不透明度，并显示禁止光标。

### 设置列表

将开关堆叠起来，即形成一个设置列表。每个开关的标签都说明设置的名称，各行之间靠列表的间距分隔，而不是用线条。

```xml
<StackPanel Spacing="4">
    <arc:Switch Content="邮件通知" IsChecked="True" />
    <arc:Switch Content="每周摘要" />
    <arc:Switch Content="产品更新" />
</StackPanel>
```

标签应保持为设置的名称，而不是描述其状态的句子。“每周摘要”在两种状态下都清楚，而“开启后发送每周摘要”在开关关闭时读起来就很别扭。当效果需要解释时，在标签下方的文本块中放一段简短说明，并让它与同一标签保持在同一列中。

### 绑定

将 `IsChecked` 绑定到视图模型属性。绑定 `IsEnabled` 可锁定开关。

```xml
<arc:Switch Content="邮件通知"
            IsChecked="{Binding EmailEnabled}"
            IsEnabled="{Binding CanEditSettings}" />
```

`IsChecked` 是双向的，因此指针或键盘做出的更改会立即更新该属性。将 `IsEnabled` 绑定到权限，或绑定到阻止更改的状态（例如由组织管理的设置），并在附近说明原因。

### 绑定到视图模型

使用 CommunityToolkit.Mvvm 时，视图模型将每项设置保存为一个属性，修改即保存。开关本身不保存任何内容，由处理程序保存使用者设置的值。

```csharp
public interface ISettingsService
{
    Task SetAsync(string key, bool enabled);
}

public partial class NotificationSettingsViewModel : ObservableObject
{
    private readonly ISettingsService _settings;

    public NotificationSettingsViewModel(ISettingsService settings)
    {
        _settings = settings;
    }

    [ObservableProperty]
    private bool _emailNotifications;

    [ObservableProperty]
    private bool _canEditSettings = true;

    partial void OnEmailNotificationsChanged(bool value)
    {
        _ = _settings.SetAsync("notifications.email", value);
    }
}
```

```xml
<arc:Switch Content="邮件通知"
            IsChecked="{Binding EmailNotifications}"
            IsEnabled="{Binding CanEditSettings}" />
```

- `IsChecked` 是要绑定的值。请使用普通的 `bool` 属性，因为开关从不产生 null 值。
- 只要属性变化，生成的 `OnEmailNotificationsChanged` 方法就会运行，包括已保存的值被加载进来的时候。请在显示页面之前加载已保存的值，或在值与已存储的值相同时跳过保存，这样加载不会把设置写回去。
- 将保存逻辑放在视图模型中，而不是视图中。这样，无论使用者是用鼠标、键盘还是代码更改，同一设置都会以相同方式保存。

### 禁用

禁用的开关保留其状态，且无法更改。

<Demo name="Disabled" />

当设置当前无法应用时禁用开关，并在旁边的描述中说明原因，例如“由管理员开启”。列表中被禁用的开关仍会被读作列表的一部分，因此应保留它的位置，而不是隐藏。

### 变更

每当状态变化时，无论来自指针、键盘还是代码，都会引发 `IsCheckedChanged`。在此处应用设置，这样每种更改方式的效果都相同。

```csharp
notifications.IsCheckedChanged += (_, _) =>
{
    settings.EmailEnabled = notifications.IsChecked == true;
};
```

无论是代码变更还是使用者的输入，该事件都会触发。当保存失败时，请在同一处理程序中把 `IsChecked` 设回之前的值，并在开关附近显示错误，使开关显示的始终是真正保存下来的值。

### 示例：通知设置

SaaS 管理控制台中为账单管理员准备的通知页面。每一行用一个简短的名词短语命名一项设置，其下的描述说明该设置发送什么内容以及何时发送。各行之间以线条分隔，并对齐在同一列上。发票提醒一行被禁用，因为由组织设定，页脚说明更改会立即保存，使使用者知道不会再有保存步骤。

![一张通知设置卡片，其中三个开关分别为开、关，以及一个被禁用的开关](images/NotificationSettings.png)

```xml
<Border Width="640"
        Padding="24"
        BorderBrush="{arc:ArcDesignTokenResource Border}"
        BorderThickness="1"
        CornerRadius="{arc:ArcDesignTokenResource RadiusControl}">
    <StackPanel Spacing="16">
        <StackPanel Spacing="4">
            <TextBlock Text="Billing notifications"
                       FontSize="18"
                       FontWeight="SemiBold"
                       Foreground="{arc:ArcDesignTokenResource Foreground}" />
            <TextBlock Text="Choose which billing events email the workspace owners."
                       FontSize="13"
                       TextWrapping="Wrap"
                       Foreground="{arc:ArcDesignTokenResource TextMuted}" />
        </StackPanel>
        <Grid ColumnDefinitions="*,Auto" RowDefinitions="Auto,Auto">
            <StackPanel Spacing="2" VerticalAlignment="Center">
                <TextBlock Text="Payment failures"
                           FontWeight="Medium"
                           Foreground="{arc:ArcDesignTokenResource Foreground}" />
                <TextBlock Text="Sent at once when a card payment fails."
                           FontSize="12"
                           Foreground="{arc:ArcDesignTokenResource TextMuted}" />
            </StackPanel>
            <arc:Switch Grid.Column="1"
                        AutomationProperties.Name="Payment failures"
                        IsChecked="True" />
        </Grid>
        <Border Height="1" Background="{arc:ArcDesignTokenResource Border}" />
        <Grid ColumnDefinitions="*,Auto">
            <StackPanel Spacing="2" VerticalAlignment="Center">
                <TextBlock Text="Weekly revenue digest"
                           FontWeight="Medium"
                           Foreground="{arc:ArcDesignTokenResource Foreground}" />
                <TextBlock Text="Mondays at 08:00 UTC, with the previous week's totals."
                           FontSize="12"
                           Foreground="{arc:ArcDesignTokenResource TextMuted}" />
            </StackPanel>
            <arc:Switch Grid.Column="1"
                        AutomationProperties.Name="Weekly revenue digest" />
        </Grid>
        <Border Height="1" Background="{arc:ArcDesignTokenResource Border}" />
        <Grid ColumnDefinitions="*,Auto">
            <StackPanel Spacing="2" VerticalAlignment="Center">
                <TextBlock Text="Invoice reminders"
                           FontWeight="Medium"
                           Foreground="{arc:ArcDesignTokenResource Foreground}" />
                <TextBlock Text="Set by your organization. Ask an owner to change it."
                           FontSize="12"
                           Foreground="{arc:ArcDesignTokenResource TextMuted}" />
            </StackPanel>
            <arc:Switch Grid.Column="1"
                        AutomationProperties.Name="Invoice reminders"
                        IsChecked="True"
                        IsEnabled="False" />
        </Grid>
        <TextBlock Text="Changes save as soon as you switch them."
                   FontSize="12"
                   Foreground="{arc:ArcDesignTokenResource TextMuted}" />
    </StackPanel>
</Border>
```

### 文案规范

- **标签**：设置的名称，使用句首大写，不加冒号，也不加问号。写“邮件通知”，而不是“要接收邮件通知吗？”。
- **描述**：一句话，说明该设置的作用以及何时生效。时间和数量用阿拉伯数字书写。
- **禁用原因**：说明谁可以更改该设置，例如“由组织设定”，以及如何申请更改。
- **状态文字**：没有保存按钮时，应说明更改会立即保存。例如“切换后更改即刻保存。”

| 不要写 | 改为 |
|---|---|
| 启用 | 邮件通知 |
| 切换 | 开启后即可接收每周摘要。 |
| 是否要接收邮件？ | 邮件通知 |
| 设置已禁用 | 由贵组织设定。请联系所有者更改。 |

### 最佳实践

**推荐**

- 使用者一做出更改就立即应用，若保存可能失败，则在开关附近显示结果。显示着服务器尚未接受的状态，比先等待的开关更糟。
- 在每种状态下都保持标签相同，让使用者在两种位置都能读到设置的名称。
- 将相关的开关归在同一标题下，并且每次访问页面时保持顺序不变。
- 为仅含图标的开关设置 `AutomationProperties.Name`，因为它没有标签可供命名。

**避免**

- 用开关来表示需要确认或说明理由的选择。对话框或提交按钮能让这一步骤清楚可见。
- 在视图模型之外的多个代码路径中修改状态，而没有统一的保存入口。这样设置就取决于它被修改的方式。
- 为了精简列表而隐藏禁用的开关。使用者便无从得知该设置存在，也不知道它为何关闭。
- 在标签中写入状态（“开：邮件通知”）。开关本身已经显示状态，标签重复说明会造成重复阅读。

### 常见问题

**代码修改状态时，滑块会有动画吗？** 会，在完整动效下会。并非由按压引起的变化会播放一次短暂的滑块拉伸，使代码变更可以被看见。在按压松开后 250 ms 之内发生的变化不会拉伸，因为按压本身已经展示了动效。

**开关可以没有标签吗？** 可以。没有 `Content` 时，只显示滑轨和滑块，开关没有可见的名称。请设置 `AutomationProperties.Name`，以便辅助技术读取。

**在代码中设置 `IsChecked` 会保存设置吗？** 不会。设置它只会改变状态、引发 `IsCheckedChanged` 并播放拉伸动效。保存只在你的处理程序或视图模型调用之处进行。

**保存过程中开关会显示加载状态吗？** 不会。如果在结果确定之前使用者不得再次更改，请在保存期间用 `IsEnabled` 禁用开关。

**为什么屏幕阅读器把开关识别为切换按钮？** Avalonia 没有单独的开关控件类型，因此开关会以其选中或未选中状态被报告为切换按钮。

## API 参考

<!-- api: Switch -->

## 无障碍

- `Content` 即开关的名称。只有图标的开关需要设置 `AutomationProperties.Name`。
- 空格键和 Enter 键可切换开关。按住空格键时，滑块保持拉伸的形状。
- 辅助技术会将开关报告为处于选中或未选中状态的切换按钮。Avalonia 没有单独的开关控件类型。
- 键盘焦点会绘制一个偏移 2px 的 2px 轮廓线。指针焦点不会绘制。
- 滑块对辅助技术隐藏。开和关的状态同时通过轨道颜色和滑块位置显示。

### 键盘

| 按键 | 操作 |
|---|---|
| `Tab` / `Shift+Tab` | 将焦点移到开关，再移到下一个或上一个控件 |
| `Space` | 切换开关；按住 `Space` 时滑块保持拉伸 |
| `Enter` | 切换开关 |

## 动效

- 按下开关时，滑块会向另一侧加宽，远端边缘保持不动。松开后，滑块以无弹跳的弹簧滑过去。
- 并非由按下引起的变化（例如按键或代码修改），会播放一次短暂的滑块拉伸。
- 轨道填充会淡入淡出，滑块颜色随之变化。
- 减弱动效时，取消拉伸，滑块立即移动，并关闭颜色过渡。关闭动效时效果相同。

| 变化 | 完整 | 减弱 | 关闭 |
|---|---|---|---|
| 按下后滑块移动 | 以 0.3 s 的无弹跳弹簧滑过去 | 立即移动 | 立即移动 |
| 按下时滑块加宽 | 弹到 23 px 宽；远端边缘保持不动 | 不加宽 | 不加宽 |
| 由按键或代码引起的变化 | 拉伸到 116% 再回弹，约 0.34 s | 无拉伸 | 无拉伸 |
| 轨道填充与滑块颜色 | 在标准缓动上交叉淡变 | 立即完成 | 立即完成 |

## 相关组件

- `Checkbox`：用于需要通过提交按钮确认的选择。
- `SegmentedControl`：用于在几个具名视图之间切换。
- `RadioGroup`：用于从一组选项中恰好选择一项。

| 组件 | 适用场景 |
|---|---|
| `Checkbox` | 该值随表单或提交按钮一起保存 |
| `SegmentedControl` | 选择的是同一内容的几个具名视图之一 |
| `RadioGroup` | 选择的是一组选项中的一项，且选项带有描述 |
| `Button` | 控件立即执行一个动作，而不是设置状态 |
