---
title: 轻提示
summary: 已完成后台操作的简短确认。
source-hash: 77303346d233
---

<Demo name="LocalToast" />

## 何时使用

`Toast` 是由页面自己掌控的单条确认：一段简短的消息从边缘升起，停留几秒后消失。当页面本身知道某件事已经完成时（例如表单保存之后），而用户仍停留在同一屏幕上，它是合适的工具。页面负责放置轻提示、设置文字并决定何时打开，因此消息总与它所在的视图保持一致。

- **设置页与个人资料页的保存确认**：请求成功后，在账单地址表单下方显示“更改已保存”。用户仍停留在页面上，因此一条简短、会自行关闭的消息就够了。
- **复制与导出反馈**：在开发者控制台中，密钥旁显示“API 密钥已复制”，或在发票详情页显示“发票链接已复制”。轻提示确认了操作，又不会抢走用户正在编辑的字段的焦点。
- **同一屏幕上的后台同步结果**：CRM 备注编辑器中草稿保存后显示“已同步到所有设备”。页面在保存完成时设置 `IsOpen`，其他部分无需知道这件事。
- **列表页的批量操作**：表格下方显示“已归档 3 位联系人”。当它遮住用户需要的行时，用户可以将其滑走，而且不会留下一队等待清除的旧消息。

## 何时不用

- **可能同时打开多条消息，或结果来自应用的任何位置**：用 `ToastStack`。它会排队、带类型、可更新并堆叠通知，也可以通过 `Toast.Show` 从任何控件调用。
- **需要一直保留、直到有人处理的消息**，例如付款失败或配额警告：在页面内使用 `Alert`，它会一直显示到问题解决。
- **必须先回应才能继续的消息**：用 `Dialog`，它会获得焦点并阻止页面操作，直到用户做出选择。
- **网站顶部轮播的横幅**，例如带倒计时的维护通知：用 `AnnouncementBar`。

## 用法

`Toast` 显示其 `Title` 与可选的 `Description`。`IsOpen` 为 `True` 时可见，并在 `Duration`（默认 4.5 秒）后关闭。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:Toast Title="更改已保存"
               Description="已同步到你的所有设备。"
               VerticalAlignment="Bottom" />
</UserControl>
```

`Toast` 派生自 Avalonia 的 `TemplatedControl`，因此它的外观位于 `ControlTheme` 中，可以用 Avalonia 常用的机制重新设置样式。它额外提供消息与动效行为：进入与退出、计时器、滑动手势以及 `Closed` 事件。它不排队。页面中的每个 `Toast` 都是独立的，因此把它放在消息所属的位置即可；当多条消息可能同时打开时，改用 `ToastStack`。

### 结构

1. **表面**（`PART_Root` 与 `PART_Surface`）：卡片本身。宽度为可用宽度，最大到 `MaxToastWidth`（416 px，26rem），更窄的容器会使它收窄。它有一条颜色为边框色的 1 px 边框、面板圆角、浮动阴影，以及在页面背景上混入 6% 颜色的表面色。内边距左侧为 16 px，其余三侧为 14 px。
2. **图标圆盘**（`PART_Icon`）：30 px 的圆，之后有 12 px 的间距。填充色为表面上 10% 的成功色，外圈是 1 px 的环，颜色为边框上 24% 的成功色。每条轻提示的圆盘都相同。
3. **对勾**（`PART_Check`）：16 px 的成功色对勾，描边宽 2.25。进入动效开始后它会自行绘制。对勾只是装饰，不向辅助技术公开。
4. **标题**（`PART_Title`）：`Title`，14 px、中等字重，单行显示。过长的标题以省略号结尾。其行高为 19.6 px，顶部内边距使第一行与 30 px 的圆盘居中对齐。
5. **描述**（`PART_Description`）：`Description`，14 px，使用次要文字颜色，自动换行。它位于标题下方 2 px，为空时不占用间距。
6. **关闭按钮**（`PART_Close`）：32 px 的圆形幽灵按钮，带 2 px 的 X 图形。静止时颜色较淡，悬停时变为前景色。其无障碍名称为 "Dismiss notification"。

### 显示与隐藏

关闭之后，将 `IsOpen` 设为 `True` 即可再次显示轻提示。关闭、滑动和超时都会把 `IsOpen` 设为 `False`，因此该属性始终反映屏幕上的实际状态。轻提示打开时修改 `Title` 或 `Description`，新文字会向上浮现，高度随之变化；计时器继续运行。对已经打开的轻提示设置 `IsOpen` 为 `True` 没有任何效果。

```csharp
toast.Title = "草稿已发布";
toast.Description = "读者几秒内即可看到。";
toast.IsOpen = true;
```

在同一个消息位置只保留一个轻提示。当同一条消息需要再次出现时，更新它的文字并打开它，而不是每次都新建一个 `Toast`。

### 持续时间

`Duration` 设置轻提示自动关闭前的时间，默认为 4.5 秒。值为零或更小时，轻提示保持打开，直到用户将其关闭，或页面把 `IsOpen` 设为 `False`。计时器在轻提示打开时启动，轻提示打开期间修改 `Duration` 时计时器会重新开始。悬停时计时器不会暂停，因此文案必须在给定的时间内读完。

```xml
<arc:Toast Title="上传完成" Duration="0:0:8" />
<arc:Toast Title="仍在同步" Duration="0" />
```

根据文案的长度选择持续时间。单行确认适合默认值。带描述的消息需要更多阅读时间，因此应设置更长的 `Duration`，例如 8 秒。只有当页面自己关闭轻提示时才使用 0，并确保工作结束时页面一定会把 `IsOpen` 设为 `False`。

### 关闭事件

`Closed` 在轻提示开始关闭时引发，共有三种原因。`ToastCloseReason` 用于区分它们：`Dismissed` 表示点击了关闭按钮或调用了 `Close()`，`Swiped` 表示滑动超过了距离或速度阈值，`TimedOut` 表示 `Duration` 已经到时。

```csharp
toast.Closed += (_, e) =>
{
    if (e.Reason == ToastCloseReason.TimedOut)
    {
        log.Text = "消息已自动关闭。";
    }
};
```

`Closed` 只针对由轻提示自身发起的关闭而引发。从代码中把 `IsOpen` 设为 `False` 会关闭轻提示，但不会引发该事件，因为页面已经知道自己设置的状态。当页面希望获得与关闭按钮相同的结果（包括事件）时，使用 `Close()`。

### 应用级轻提示

`Toast.Show` 可以从窗口中的任何控件，把轻提示发送到窗口的 `ToastStack`，它提供的正是 `ToastStack` 的同一服务。当消息来自不拥有 `Toast` 控件的代码时使用它，例如共享的导出命令、剪贴板辅助方法或后台任务。`ToastKind` 决定图标与颜色：`Success`、`Info`、`Warning`、`Error` 或 `Loading`。`Toast.Show` 返回一个 id，`Toast.Update` 与 `Toast.Dismiss` 接收该 id。

<Demo name="FromAnywhere" />

```csharp
var id = Toast.Show(this, "导出已开始", ToastKind.Loading, "Acme Analytics 正在生成 CSV。");

// 稍后，导出完成时：
Toast.Update(this, id, new ToastUpdate
{
    Kind = ToastKind.Success,
    Title = "导出已就绪",
    Description = "请到导出页面下载。"
});
```

`owner` 参数可以是附加到窗口的任何 `Visual`。如果窗口没有 `ToastStack`，第一次调用会在窗口的浮层上创建一个。尚未附加的 owner 会引发 `InvalidOperationException`，因此应在视图加载后再调用这些方法。应用级 API 的完整说明见 `ToastStack` 页面。

### 绑定到视图模型

`IsOpen` 默认为双向绑定，因此绑定无需指定 `Mode`。标题与描述是单向的：文字由视图模型持有，轻提示只负责显示。下面的视图模型采用 CommunityToolkit.Mvvm 风格编写。

```csharp
public partial class ProfileViewModel : ObservableObject
{
    private readonly IProfileService _profiles;

    public ProfileViewModel(IProfileService profiles) => _profiles = profiles;

    [ObservableProperty]
    private bool _isSavedToastOpen;

    [ObservableProperty]
    private string _savedToastTitle = "更改已保存";

    [ObservableProperty]
    private string? _savedToastDescription;

    [RelayCommand]
    private async Task SaveAsync()
    {
        await _profiles.SaveAsync(Draft);
        SavedToastTitle = "更改已保存";
        SavedToastDescription = "已同步到你的所有设备。";
        IsSavedToastOpen = true;
    }
}
```

```xml
<Grid RowDefinitions="*,Auto">
    <!-- 表单内容放在这里。 -->
    <arc:Toast Grid.Row="1"
               Title="{Binding SavedToastTitle}"
               Description="{Binding SavedToastDescription}"
               IsOpen="{Binding IsSavedToastOpen}"
               HorizontalAlignment="Right"
               VerticalAlignment="Bottom"
               Margin="0,0,0,8" />
</Grid>
```

`IsOpen` 是唯一的双向属性，因此无论关闭的原因是什么，视图模型都能感知到每一次关闭。如果视图需要根据原因做出反应，在代码后置中订阅 `Closed`。每次保存时，先设置标题与描述，再把 `IsSavedToastOpen` 设为 `True`：已关闭的轻提示会以新文字打开，已打开的则原地更新文字。

### 示例：CRM 中的个人资料保存

CRM 中的联系人记录在底部有一个保存按钮。保存成功后，表单左下方会升起一条确认消息，说明发生了什么，并指出结果在哪里。轻提示不会阻挡下一次编辑，并会在默认的 4.5 秒后自动关闭，用户可以继续工作。保存失败时改用 `Alert`，因为失败必须一直可见，直到用户解决问题。

```xml
<Grid Width="640" Height="220">
    <Border Background="{arc:ArcDesignTokenResource SurfaceRaised}"
            BorderBrush="{arc:ArcDesignTokenResource Border}"
            BorderThickness="1"
            CornerRadius="{arc:ArcDesignTokenResource RadiusPanel}" />
    <arc:Toast Title="更改已保存"
               Description="Northwind Logistics 已在你的所有视图中更新。"
               HorizontalAlignment="Left"
               VerticalAlignment="Bottom"
               Margin="24" />
</Grid>
```

### 文案规范

- **标题**：一句简短的过去时陈述，说明什么已经完成：“更改已保存”“发票已发送”。它保持单行显示，因此英文文案应控制在约 30 个字符以内。
- **描述**：一句话，说明结果在哪里或接下来会发生什么：“已同步到所有设备。”“读者几秒内即可看到。”标题已说明一切时省略描述。
- **不要使用行动动词**：轻提示除关闭按钮外没有其他按钮，因此不要在文案中要求用户去做什么。如果下一步很重要，把它放在页面中，或使用 `Alert`。
- **句子式大小写，不用感叹号**：确认消息陈述事实，不应庆祝。

| 不要写 | 改为 |
|---|---|
| 成功！您的更改已成功保存！！！ | 更改已保存 |
| 已保存。点击此处查看。 | 更改已保存。可在“活动”中找到。 |
| 保存表单时出错 | 无法保存，请重试。 |
| 该项已从您的账户中成功删除 | 联系人已删除 |

### 最佳实践

**推荐**

- 用 `Toast` 显示页面掌控的一条确认，并把文字放在视图模型中，使页面状态与消息保持一致。
- 保持标题足够短，能放在一行内。过长的标题会以省略号结尾，从而隐藏结果。
- 把轻提示放在用户正在关注的位置，通常是表单或页面的底部，使确认消息靠近引发它的操作。
- 将 `IsOpen` 绑定到视图模型，使打开状态与页面已持有的数据放在一起，而不只存在于视图中。

**避免**

- 必须阅读的消息，例如付款失败或权限问题。计时器不会暂停，这类消息应放在 `Alert` 或 `Dialog` 中，直到问题解决前一直保留。
- 用 `Toast` 连续显示多条通知。每个 `Toast` 都是独立的，不排队，每条消息都需要自己在页面上的位置，且彼此互不知晓；应改用 `ToastStack`。
- 持续时间为零且页面不负责关闭。这样的轻提示永远不会超时，也不会被页面关闭，会在整个会话期间一直停留在屏幕上。
- 只依赖动效传达信息。结果应由文字承载；升起与对勾只起强调作用。

### 常见问题

**为什么轻提示在我读完之前就关闭了？** 默认持续时间为 4.5 秒，而且指针停在轻提示上时计时器也不会暂停。文案较长时，请设置更长的 `Duration`；如果消息必须保留到问题解决，请使用 `Alert`。

**如何知道轻提示关闭的原因？** 订阅 `Closed` 并读取 `e.Reason`。`Dismissed` 表示点击了关闭按钮或调用了 `Close()`，`Swiped` 表示滑动，`TimedOut` 表示持续时间已到。从代码中把 `IsOpen` 设为 `False` 不会引发该事件。

**如何再次显示同一条消息？** 轻提示关闭后，将 `IsOpen` 设为 `True`。先设置新的 `Title` 或 `Description`，使轻提示以新文字打开。已打开的轻提示在文字变化时会保留当前计时器，对已打开的轻提示设置 `IsOpen` 为 `True` 也不会有任何作用。

**视图模型能否在没有视图引用的情况下显示轻提示？** 可以。在接收到的代码中，把窗口的 `Visual`（例如视图本身）传给 `Toast.Show`；或者使用 `ToastHost.GetService` 获取窗口的 `IToastService`，一次获取后交给视图模型。

**轻提示会抢走焦点吗？** 不会。轻提示以礼貌方式播报，且不移动焦点。键盘焦点仍停留在用户离开的位置，通常是发起这项工作的按钮。

## API 参考

<!-- api: Toast -->

配套类型。`ToastCloseReason` 是 `ToastClosedEventArgs.Reason` 中的原因：

| 成员 | 含义 |
|---|---|
| `Dismissed` | 关闭按钮或 `Close()` 关闭了轻提示 |
| `Swiped` | 滑动超过了距离（80 px）或速度（480 px/s）阈值 |
| `TimedOut` | `Duration` 已到时 |

## 无障碍

- `Title` 是轻提示的无障碍名称，`Description` 是其帮助文字，因此轻提示出现时屏幕阅读器会同时朗读两者。
- 轻提示是礼貌的实时区域：它会在当前语音之后播报，不会打断它。
- 关闭按钮的名称为 "Dismiss notification"。Tab 可以移动到它，按 Enter 或空格键即可关闭轻提示。
- 对勾是装饰，不向辅助技术公开。
- 轻提示不会获得焦点，因此不会把用户从正在使用的字段或按钮上移开。
- 计时器不会因悬停或聚焦而暂停。文字应保持简短；如果用户可能需要更多时间阅读，请使用 `Alert`。
- 滑动是指针与触控手势。关闭按钮可以通过键盘得到相同的结果。

### 键盘

| 按键 | 操作 |
|---|---|
| `Tab` / `Shift+Tab` | 将焦点移到关闭按钮或从关闭按钮移出；轻提示没有其他可聚焦的控件 |
| `Enter` 或 `Space` | 按下关闭按钮：轻提示以 `Dismissed` 原因关闭 |

只有指针拖动可以滑动。从关闭按钮开始的按下操作不会触发拖动。

## 动效

- 轻提示以 96% 的缩放比例从下方升起 16 px，并通过弹簧运动；其对勾在 0.12 秒后开始绘制。
- 关闭时，轻提示下沉 8 px，缩小到 97% 并淡出。关闭后的轻提示不占用空间。
- 变化的文字向上浮现，高度通过弹簧适应新的文案。
- 滑动会跟随指针；松开且超过阈值时，它会以松开时的速度继续移动并淡出。短距离滑动会弹回。
- 减弱动效时，轻提示只做淡入淡出，并关闭滑动。关闭动效时，轻提示立即显示与隐藏。

| 变化 | 完整 | 减弱 | 关闭 |
|---|---|---|---|
| 轻提示打开 | 以 96% 的缩放比例升起 16 px，并在变形弹簧上淡入；对勾在 0.12 秒后绘制 | 在最终位置淡入，对勾已绘制完成 | 立即显示 |
| 轻提示关闭（按钮、计时器或 `Close()`） | 下沉 8 px，缩小到 97%，并在退出时长内淡出 | 以瞬时预设淡出 | 立即移除 |
| 滑走 | 跟随指针，然后以松开时的速度甩出并淡出 | 不可用：滑动已关闭 | 不可用：滑动已关闭 |
| 标题或描述变化 | 旧文字向上移出，新文字升起，高度通过弹簧适应 | 瞬时淡入 | 立即完成 |

## 相关组件

- `ToastStack`：用于应用任何位置发出、可排队、带类型且可更新的通知。
- `Alert`：用于在问题解决前一直保留在页面上的消息。
- `Dialog`：用于必须在页面继续前做出回应的决定。
- `AnnouncementBar`：带倒计时的轮播横幅。

| 组件 | 适用场景 |
|---|---|
| `ToastStack` | 同时可能打开多条结果，或消息来自页面之外的代码 |
| `Alert` | 消息保留在页面上，直到用户或数据将其解决 |
| `Dialog` | 用户必须做出选择，页面才能继续 |
| `AnnouncementBar` | 站点级通知轮流展示多条消息，或需要倒计时 |
| `Toast` + `IsOpen` | 页面掌控一条简短确认及其文字，且无需排队 |
