---
title: 长按确认
summary: 通过长按而不是点击来确认破坏性操作。
source-hash: e58caccbebc8
---

<Demo name="Default" />

## 何时使用

`HoldToConfirm` 是一个只有在用户按住一段设定时间后才执行操作的按钮。它适用于破坏性或难以撤销的操作，且其后果已经显示在屏幕上，因此用户无需再阅读单独的消息。按住期间填充会沿按钮向前推进；提前松开则会回退，因此控件会在真正提交之前准确显示操作推进到了哪里。

- **在设置页中删除项目**：项目设置底部的危险区域，在项目名称旁列出“长按删除项目”。名称就在屏幕上，因此长按就足以确认这一选择。
- **在开发者控制台中撤销 API 密钥或 Webhook 密钥**：密钥行显示其前缀与最后使用日期，撤销按钮需要按住两秒。仍在被构建流水线使用的密钥会立即失效，因此较长的按住时间可以防止误触。
- **在紧凑的账单行中取消订阅**：套餐行已经显示续费日期和即将停止的金额。长按让决定留在该行上，而不是弹出一个重复相同信息的确认对话框。
- **在内部工具中清空回收站或重置沙盒**：在共享平板上使用的仓储或支持工具中，误触“清空回收站”不能删除 300 条记录。

## 何时不用

- **安全的或容易撤销的操作，例如保存草稿或静音频道**：使用 `Button` 或 `ActionButton`。对可逆的操作长按，只会让用户白白等待。
- **需要先读清后果的操作，例如退款、带按比例计费的套餐降级或批量修改账单信息**：使用 `Dialog`，它可以显示金额、受影响的记录以及清晰的取消选项。
- **从触摸列表中移除一行，例如收件箱、订单队列或通知列表**：使用 `SwipeActions`，它在滑动时显示操作，并让操作留在该行旁边。
- **从一组中选择值或选项**：使用 `Select`、`Combobox` 或 `MorphSelect`。长按只提交一个操作。
- **确认用户在保存前在表单上所做的更改**：使用表单的主要 `Button`，并在提交时校验。长按保存按钮会让每次保存都多出一段延迟。

## 用法

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:HoldToConfirm Label="长按删除项目"
                       ConfirmedLabel="已删除"
                       Command="{Binding DeleteProject}" />
</UserControl>
```

`HoldToConfirm` 派生自 Avalonia 的 `Button`，因此 `Command`、`CommandParameter`、`IsEnabled` 以及按钮的模板和样式机制都与按钮的工作方式相同。区别在于触发方式：`Click` 事件和 `Command` 只在长按完成时才运行。普通点击、短于长按时长的点按，或自动化调用都不会有任何作用。控件在按钮之上增加了填充、完成状态、`IsHolding` 状态以及 `Confirmed` 事件。

### 结构

1. **图标**（`Icon`）：一个 24 × 24 的 Lucide 几何图形，在标签前以 18 px 绘制。默认是垃圾桶图标。长按完成时，它会被绘制的对勾取代。
2. **标签**（`Label`）：操作说明与动作，例如“长按删除项目”。完成后文字改为显示 `ConfirmedLabel`，默认为“Done”。图标与标签之间相距 8 px，并在按钮内居中。
3. **表面**：1 px 的边框使用强边框色，控件圆角（`RadiusControl`）与表面背景。最小高度为中等控件尺寸，默认主题下为 44 px。水平内边距为 20 px。
4. **悬停色调**：指针停在未确认的按钮上时，色调颜色以 6% 的比例混入表面。它只是一层色调，而不是第二个填充，且从不遮挡标签。
5. **填充**：一层色调颜色，随长按推进从左边缘显露。同样的图标和标签会在填充内部以色调的对比色再次绘制，因此文字颜色恰好在边缘处改变。
6. **焦点环**（仅键盘焦点）：2 px 的焦点色环，绘制在按钮外 4 px 处，圆角为 22 px。指针聚焦时不显示。
7. **禁用状态**：整个按钮降至 50% 不透明度，并显示“不允许”光标。

### 选择色调

`Tone` 设置填充的颜色。由于文字会在填充边缘处改变颜色，因此在长按的每个位置标签都保持可读。

| 色调 | 填充 | 适用于 |
|---|---|---|
| `Accent`（默认） | 所选的强调色 | 有意为之但非破坏性的操作，例如“长按发布” |
| `Danger` | 危险色，搭配带危险色调的边框与标签 | 不可撤销的操作：删除、撤销、清除、关闭账号 |
| `Neutral` | 前景色 | 可逆但仍需要刻意长按的操作，例如“长按归档” |

对于用户无法撤回的操作使用 `Danger`，并且每组内只保留一个 `Danger` 控件。一个面板里若有好几个红色的长按按钮，真正的危险就难以辨认。归档类操作选择 `Neutral`，使危险色保持其含义。

<Demo name="Tones" />

### 长按时长

`HoldDuration` 设置按钮必须按住多久才会完成。默认为 1.2 秒，这个时长足以忽略轻触，又不会让有意的长按显得太慢。对于影响范围广的操作，例如撤销生产系统在用的密钥，可以设置更长的值，例如两秒。同一屏幕上的所有长按保持相同的值，让用户形成一致的节奏。帮助文案中的文字由时长生成，因此修改时长后它仍然正确。

该值是一个 `TimeSpan`。在 XAML 中，两秒写作 `HoldDuration="0:0:2"`。

### 图标

`Icon` 是一个 `Geometry`。默认是 Lucide 的垃圾桶图标，适合删除操作。请选择与操作名词相符的图标，例如归档用的收纳盒，撤销用的钥匙。标签已经足够时，传入 `null`。

```csharp
var archive = new HoldToConfirm
{
    Label = "长按归档",
    Tone = HoldToConfirmTone.Neutral,
    Icon = ArcIcons.Archive,
    ConfirmedLabel = "已归档"
};
```

在 XAML 中，使用静态资源：`Icon="{x:Static arc:ArcIcons.Archive}"`。

### 长按时的提示

`HoldingChanged` 在长按开始和结束时引发。`IsHolding` 告诉你当前状态。可以用它们在按钮旁显示提示，例如填充推进时显示的“请继续按住”。

```csharp
project.HoldingChanged += (_, e) =>
{
    hint.Text = e.IsHolding
        ? "请继续按住…"
        : project.IsConfirmed ? "已删除。" : "提前松开，填充会回退。";
};
```

提前松开时，填充会以弹簧回退，且不会确认任何操作。长按完成时，`HoldingChanged` 会在 `IsConfirmed` 变为 `true` 之前报告 `IsHolding` 为 `false`。因此，上例中的判断在完成时读到的是 `false`。应改由 `Confirmed` 事件显示完成文字：

```csharp
project.Confirmed += (_, _) => hint.Text = "已删除。";
```

### 完成状态

`IsConfirmed` 是双向的。长按完成时它变为 `true`，早于 `Confirmed` 的引发和 `Command` 的运行。一旦为 `true`，按钮会显示 `ConfirmedLabel`、绘制对勾，向辅助技术报告自己已不可用，并且不能再次长按。将其设回 `false` 即可重置按钮；标签变回 `Label` 时，填充随之回退。

```xml
<arc:HoldToConfirm Label="长按删除项目"
                   IsConfirmed="{Binding IsDeleted}" />
```

```csharp
reset.Click += (_, _) => project.IsConfirmed = false;
```

由代码设置的确认状态（例如项目在另一个窗口中已被删除时）会直接填满按钮，而无需长按。

### 禁用

禁用的按钮不能被长按；按钮被禁用时，正在进行的长按会被取消。只在操作无法适用时使用禁用状态，并在旁边的文字中说明原因，例如“请先转移所有权”。

<Demo name="Disabled" />

### 取消长按

以下情况下长按会被取消，填充回退且不会确认：

- 在 `HoldDuration` 到达之前，用户松开了指针或按键。
- 长按期间，指针移到按钮边缘之外超过 24 px，这与原生按下所允许的容差相同。指针移回内部不会重新开始长按。
- 焦点移离按钮，例如通过 Tab。
- 窗口失去激活，因为窗口在长按中途失活时可能丢失按键抬起事件。
- 长按期间按钮被禁用或从可视树中移除。

要从代码中取消长按，例如对话框在页面上方打开时，调用 `CancelHold()`。

### 绑定到视图模型

在 MVVM 应用中，视图模型拥有操作的状态。使用 CommunityToolkit.Mvvm 时，视图模型持有 `IsDeleted` 标志与删除命令，按钮同时绑定到二者。命令在长按完成且 `IsDeleted` 已变为 `true` 之后运行一次。

```csharp
public partial class ProjectDangerZoneViewModel : ObservableObject
{
    private readonly IProjectService _projects;
    private readonly string _projectId;

    public ProjectDangerZoneViewModel(IProjectService projects, string projectId)
    {
        _projects = projects;
        _projectId = projectId;
    }

    [ObservableProperty]
    private bool _isDeleted;

    [ObservableProperty]
    private string? _error;

    [RelayCommand]
    private async Task DeleteProjectAsync()
    {
        var result = await _projects.DeleteAsync(_projectId);
        if (!result.IsSuccess)
        {
            // 在标志被清除之前，按钮都会显示“已删除”，因此失败时要重置它。
            IsDeleted = false;
            Error = result.Error;
        }
    }
}
```

```xml
<StackPanel Spacing="12">
    <arc:HoldToConfirm Label="长按删除项目"
                       ConfirmedLabel="已删除"
                       Tone="Danger"
                       IsConfirmed="{Binding IsDeleted}"
                       Command="{Binding DeleteProjectCommand}" />
    <TextBlock Text="{Binding Error}"
               Foreground="{arc:ArcDesignTokenResource Danger}"
               TextWrapping="Wrap" />
</StackPanel>
```

- `IsConfirmed` 默认是双向的，因此视图模型的 `IsDeleted` 标志会跟随完成的长按。从代码将其设为 `true`，按钮就会不经长按直接显示完成状态。
- `Command` 是单向的。只把 `Command` 绑定到长按完成时运行的操作；不要同时为同一项工作处理 `Confirmed`，否则操作会运行两次。
- 对于并非操作本身的副作用，例如统计或提示，使用 `Confirmed`。
- 操作失败时必须把 `IsConfirmed` 设回 `false`。否则对于一个并未发生的操作，按钮仍会显示“已删除”。
- 编译绑定需要在视图上设置 `x:DataType`（为简洁起见，这里的示例省略了它）。

### 示例：项目设置页的危险区域

SaaS 管理控制台中项目设置页底部的危险区域。每一行都用平实的文字说明后果（“删除项目及其 42 张发票”），按钮则用同样的文字说明操作。两个操作都使用 `Danger` 色调，因为两者都无法撤销。按钮位于右侧，使用户先读到文字。

![危险区域中两个并列的长按确认按钮及其后果文字](images/DangerZone.png)

```xml
<Border Width="640"
        Padding="24"
        CornerRadius="12"
        BorderThickness="1"
        BorderBrush="{arc:ArcDesignTokenResource Border}"
        Background="{arc:ArcDesignTokenResource Surface}">
    <StackPanel Spacing="20">
        <TextBlock Text="Danger zone"
                   FontSize="16"
                   FontWeight="SemiBold"
                   Foreground="{arc:ArcDesignTokenResource Danger}" />
        <Grid ColumnDefinitions="*,Auto" ColumnSpacing="24">
            <StackPanel Spacing="4">
                <TextBlock Text="Transfer project ownership"
                           FontSize="14"
                           FontWeight="Medium"
                           Foreground="{arc:ArcDesignTokenResource Foreground}" />
                <TextBlock Text="The new owner receives billing contact and admin rights."
                           FontSize="13"
                           TextWrapping="Wrap"
                           Foreground="{arc:ArcDesignTokenResource TextMuted}" />
            </StackPanel>
            <arc:HoldToConfirm Grid.Column="1"
                               Label="Hold to transfer"
                               ConfirmedLabel="Transferred"
                               Tone="Neutral"
                               Icon="{x:Static arc:ArcIcons.Users}"
                               VerticalAlignment="Center" />
        </Grid>
        <Border Height="1" Background="{arc:ArcDesignTokenResource Border}" />
        <Grid ColumnDefinitions="*,Auto" ColumnSpacing="24">
            <StackPanel Spacing="4">
                <TextBlock Text="Delete project"
                           FontSize="14"
                           FontWeight="Medium"
                           Foreground="{arc:ArcDesignTokenResource Foreground}" />
                <TextBlock Text="Deletes Acme Analytics and its 42 invoices. This cannot be undone."
                           FontSize="13"
                           TextWrapping="Wrap"
                           Foreground="{arc:ArcDesignTokenResource TextMuted}" />
            </StackPanel>
            <arc:HoldToConfirm Grid.Column="1"
                               Label="Hold to delete"
                               ConfirmedLabel="Deleted"
                               Tone="Danger"
                               VerticalAlignment="Center" />
        </Grid>
    </StackPanel>
</Border>
```

删除行适合使用长按确认。转移行使用 `Neutral`，因为转移可以通过再转移一次来逆转，因此危险色只保留给无法撤销的操作。每个操作都有自己的长按，因此用户确认的是他想要的那一行，并且每一行都在按钮旁边写明了后果。

### 文案规范

- **标签**：操作说明加对象，“长按删除项目”，让用户在长按之前就知道动词和目标。不要只写“删除”，那看起来像一个普通按钮。
- **完成标签**：过去时态的结果，“已删除”“已归档”“已撤销”。操作一旦提交，它就替换操作说明，因此必须描述现在的状态。
- **后果文字**：按钮旁的一句话，说明会失去什么；已知数量时写明数量：“删除 Acme Analytics 及其 42 张发票。”把它放在同一行中，而不要放在工具提示里，因为触摸用户看不到工具提示。
- **语气**：句首大写，不用感叹号，不写“你确定吗？”。长按本身就是确认。

| 不要写 | 改为 |
|---|---|
| 删除 | 长按删除项目 |
| 点击删除 | 长按删除项目 |
| 已删除！ | 已删除 |
| 你确定要删除这个项目吗？ | 删除项目及其 42 张发票，且无法撤销。 |
| 长按此按钮 1.2 秒即可删除 | 长按删除项目 |

### 最佳实践

**推荐**

- 把后果以可见文字放在按钮旁边。长按确认的是用户能看到的选择，因此它不能替代说明。
- 只对无法撤销的操作使用 `Danger`，其余使用 `Neutral` 或 `Accent`。每个控件都用危险色，会让危险色失去意义。
- 操作失败时把 `IsConfirmed` 设回 `false`，使按钮不会对一个并未发生的操作报告成功。
- 每行只放一个长按，不要在同一行放多个。当各行靠得很近时，误按在错误行上的长按更难被发现。
- 在表单较密集时，用可见文字告诉键盘用户如何确认，因为提示是看不见的。

**避免**

- 对可逆或安全的操作使用长按。用户要等待一段毫无保护作用的延迟，并会因此不再信任这个控件。
- 对多个选中的行执行批量操作。一次长按删除 50 张发票会隐藏数量；请使用 `Dialog` 列出受影响的记录。
- 在服务端尚未完成操作时就显示完成状态。应在保存成功后设置 `IsConfirmed`，或在失败时重置它。
- 过长的标签。按钮会随标签加宽，过长的操作说明会把后果文字挤到视野之外。
- 对同一个操作同时依赖 `Confirmed` 和 `Command`。应为该工作选定一个触发器，使其不会运行两次。

### 常见问题

**为什么单击不会运行命令？** 长按是完成操作的唯一途径。单击、短按或自动化调用都不会有任何作用。把操作方式写在标签中，“长按删除项目”，让用户知道该怎么做。

**键盘用户能确认操作吗？** 可以。按住 Space 或 Enter。按住按键时的自动重复不会重新开始填充，在长按完成前松开按键则会使填充回退。Enter 由按钮自己处理，因此控件获得焦点时，它不会触发窗口的默认按钮。

**撤销成功后，如何重置按钮？** 将 `IsConfirmed` 设为 `false`，例如 `project.IsConfirmed = false;`。标签变回 `Label` 时，填充随之回退，因此变化无需额外消息即可看到。

**如何只为某一个按钮修改长按时长？** 在该按钮上设置 `HoldDuration`。当 `HoldDuration` 变化时，供辅助技术使用的帮助文本会重新计算，因此它始终准确。

**为什么长按完成时 `IsHolding` 读出来是 false？** `HoldingChanged` 在设置 `IsConfirmed` 之前，为长按结束而引发。应在 `Confirmed` 事件中读取 `IsConfirmed` 来判断完成状态，而不是在 `HoldingChanged` 中读取。

## API 参考

<!-- api: HoldToConfirm -->

### 色调取值

| 取值 | 含义 |
|---|---|
| `Accent` | 以所选强调色填充（Arc 默认值） |
| `Danger` | 用于不可撤销的操作：危险色的文字与填充 |
| `Neutral` | 以前景色填充 |

## 无障碍

- `Label` 为按钮命名。长按完成后，名称跟随 `ConfirmedLabel`。
- 帮助文本说明如何确认：按住并保持相应时长，或用键盘按住 Space 或 Enter。它由 `HoldDuration` 生成，操作完成后即被移除。
- 完成状态通过礼貌的状态区域播报一次，按钮报告自己已不再可用，但保留焦点。
- 用 Space 或 Enter 长按使用的是键盘；按键自动重复不会重新开始长按。移开焦点会取消长按，指针离开按钮超过 24 px 也会取消。
- 文字绘制了两次：一次在表面上，一次在填充内部。它的颜色恰好在填充边缘处改变，因此在长按的每个位置对比度都成立。
- 辅助技术把长按说明读作按钮的帮助文本，因此屏幕阅读器用户在操作执行前就能听到如何确认。

### 键盘

| 按键 | 操作 |
|---|---|
| `Space` 或 `Enter`，按住 | 开始填充；长按在 `HoldDuration` 之后完成 |
| `Space` 或 `Enter`，提前松开 | 填充回退；不确认任何操作 |
| 按住时的按键自动重复 | 忽略；不会重新开始填充 |
| `Tab` / `Shift+Tab` | 移动焦点；离开按钮会取消正在进行的长按 |
| 快速按下 `Space` 或 `Enter` | 不做任何操作；长按必须持续 `HoldDuration` |

## 动效

- 长按期间，填充以稳定的速度沿按钮推进，推进时间为剩余的时长。提前松开时，它会以不带过冲的平滑弹簧回退。
- 长按完成时，标签和图标以短暂的模糊滑动切换，图标同时绘制出对勾。
- 减弱动效会移除按下时的缩放和回退弹簧。标签与图标淡入，而填充仍然跟随长按，因为它显示的是进度。
- 关闭动效时只保留填充进度，其余变化立即完成。

| 变化 | 完整 | 减弱 | 关闭 |
|---|---|---|---|
| 长按期间的填充 | 以稳定速度沿按钮推进 | 随长按推进 | 随长按推进 |
| 提前松开 | 以不带过冲的平滑弹簧回退 | 无弹簧，直接跳回 | 立即完成 |
| 长按时的按下缩放 | 以灵敏弹簧缩放到 0.97（宽于 220 px 时为 0.985） | 无 | 无 |
| 完成时的标签与图标 | 模糊滑动；对勾自行绘制 | 淡入 | 立即完成 |
| 重置时的标签与图标 | 填充回退时，标签变形回到初始状态 | 淡入 | 立即完成 |

动效用于显示进度，而不是装饰。用户长按按钮时，填充是唯一重要的元素，因此它在每个动效级别下都保留。减弱动效会移除按下缩放与弹簧，因为它们会移动按钮本身，可能让部分人感到不适。

## 相关组件

- `Button` 与 `ActionButton` 用于不需要长按的操作。
- `Dialog` 用于用户必须先阅读后果再确认的情形。
- `SwipeActions` 用于在触摸列表中删除一行。
- `ConfirmMorph` 用于按钮需要在原处提问的情形，它提供取消路径或倒计时。

| 组件 | 适用场景 |
|---|---|
| `Button` / `ActionButton` | 操作安全、可逆或常见，一次点击即可 |
| `ConfirmMorph` | 按钮需要在原处提问，并提供取消路径或倒计时，之后才执行 |
| `Dialog` | 必须阅读后果，或操作同时影响多条记录 |
| `SwipeActions` | 操作属于触摸列表中的某一行，例如收件箱或订单队列 |
| `Select` / `Combobox` | 用户从一组值中选择，而不是提交一个操作 |
| `Toast` | 操作已经执行，应在不阻塞页面的情况下报告结果 |
