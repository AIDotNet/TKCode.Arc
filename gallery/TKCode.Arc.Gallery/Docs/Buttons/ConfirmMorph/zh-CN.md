---
title: 确认变形
summary: 一个破坏性按钮，可变形为就地确认、加载指示与带撤销的结果。
source-hash: 25d51b177954
---

<Demo name="Press" />

## 何时使用

`ConfirmMorph` 是针对破坏性或重要操作的按钮，它先在原地询问。按下后，同一个药丸形按钮会变为一句问题，接着在操作运行时变为加载指示器，最后变为结果，并可选择提供撤销。它适用于一个问题就能说明清楚后果的操作，让用户无需离开表格、卡片或编辑器即可作答。

- **删除列表中选中的项**：在文件浏览器或订单列表中，工具栏询问“删除 3 个文件？”。问题写明了数量，因此用户能准确看到将被删除的内容，而不会被弹窗打断选择。
- **移除成员或撤销访问权限**：团队页面上的行操作“移除成员”一按即可作答，结果（“已移除”）在原处确认变更，不会有用户可能错过的轻提示。
- **放弃未保存的草稿**：编辑器中一个简短的倒计时让用户有几秒钟改变主意，之后才执行放弃操作，适合无法恢复的操作。
- **带宽限期的可撤销操作**：归档项目或关闭支持工单。服务器接受变更后的一段时间内，结果会提供撤销。
- **可能失败的服务器调用**：等待界面显示工作正在进行；调用失败时则变为带重试的错误界面，用户无需重新找到该控件。

## 何时不用

- **后果需要多行说明或输入名称才能确认**，例如删除账号或生产数据库：用 `Dialog`，它可以容纳详细说明、受影响内容的列表和文本输入框。
- **没有确认问题、安全的异步提交**：用 `ActionButton`，它会显示等待与成功状态，无需先行询问。
- **绝不能因单击而触发的操作**，例如设备控制台中的恢复出厂设置：用 Gestures 中专为此设计的 `HoldToConfirm`。
- **没有单一默认操作的命令列表**，例如同一行中的复制、移动、导出和删除：用 Menus 中的 `DropdownMenu`。
- **必须阻止页面其余部分的决定**，例如导航前的未保存更改：用 `Dialog`。变形不会阻止其他位置的输入；在外部按下会以“否”回答问题。

## 用法

将 `Label` 设为静止时显示的文字。问题由 `Prompt` 提供；未设置 `Prompt` 时，使用带问号的 `Label`。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:ConfirmMorph Label="删除项目" Prompt="删除此项目？" />
</UserControl>
```

`ConfirmMorph` 派生自 Avalonia 的 `TemplatedControl`，而不是 `Button`。它的模板声明了所有界面（静止的药丸、问题、倒计时、等待指示器、结果与错误），控件会随 `State` 的变化在它们之间切换。静止的药丸与回答按钮都是模板中普通的 Avalonia 按钮，因此它们可以获得焦点，并像其他按钮一样响应 Enter 与 Space。控件本身可以获得焦点，但不是 Tab 停靠点：Tab 在当前显示界面的按钮之间移动。

状态机有五个值：`Idle`、`Confirming`、`Pending`、`Done` 与 `Error`。`State` 读写该值，默认为双向绑定，因此视图模型可以观察流程、启动流程或强制切换界面，而无需执行任何操作。

### 结构

1. **静止的药丸**（`Label`、`Icon`）：高 36 px 的药丸，位于抬升表面色上，文字为小号、中等字重。`Icon` 是位于文字之前的可选 16 px 图标。药丸左侧内边距 14 px，右侧 16 px。悬停、按下和键盘焦点分别叠加 5%、9% 和 6% 的前景色。
2. **问题**（`Prompt`）：问题文字，最宽 256 px，左侧内边距 12 px，右侧 8 px。
3. **取消**（`CancelLabel`）：次要回答，使用次要文字颜色。悬停、按下和焦点时的填充分别为前景色的 7%、11% 和 8%。
4. **确认**（`ConfirmLabel`、`Tone`）：执行操作的回答。危险色调静止时使用危险色 13% 的填充，悬停时为 18%，按下时为 22%，文字为危险色。中性色调使用强调色填充与强调前景色。
5. **倒计时**（`CountdownSeconds`、`CountdownLabel`）：随倒计时消退的圆环、剩余秒数，以及它自己的取消按钮。
6. **等待**（`PendingLabel`、`UndoingLabel`）：`ConfirmAction` 运行时的加载指示器与文字。`UndoAction` 运行时显示 `UndoingLabel`。
7. **结果**（`DoneLabel`、`UndoLabel`）：自行绘制的对勾与完成文字；设置了 `UndoAction` 时还有撤销按钮。
8. **错误**（`ErrorLabel`、`RetryLabel`）：危险色的错误文字与重试按钮。

回答按钮高 28 px，即药丸高度减去 8 px，因此每个界面都能放进药丸内。每个界面保持其自然宽度，只有药丸的宽度在界面之间变化。

### 操作与撤销

问题被回答时运行 `ConfirmAction`。任务未完成期间，等待界面保持显示。任务抛出异常时，控件切换到错误界面，重试会再次运行 `ConfirmAction`。任务完成后，结果界面显示 `DoneLabel`。

`UndoAction` 会在结果中加入撤销。按下撤销会运行它，等待界面显示 `UndoingLabel`，完成后控件回到静止状态。没有设置 `UndoAction` 时，结果中没有撤销按钮。

<Demo name="ErrorAndRetry" />

```csharp
delete.ConfirmAction = () => documentService.DeleteAsync(selected);
delete.UndoAction = () => documentService.RestoreAsync(selected);
```

上面的示例每隔一次尝试就会失败，因此演示中会出现错误界面和重试按钮。在实际应用中，任务应当只为用户可以重试的失败（例如网络错误）抛出异常。需要用户自行修正的校验错误，最好直接显示在页面中，而不是藏在重试按钮之后。

### 选择确认模式

`ConfirmMode` 决定如何回答问题。默认为 `Press`。

| 模式 | 回答方式 | 适用场景 |
|---|---|---|
| `Press`（默认） | 按一次确认按钮，或在问题的任何位置按 Enter | 问题清楚，且结果可以撤销，或操作重复执行的成本很低 |
| `Hold` | 按住确认按钮直到其填充完成；`HoldDuration` 设置所需时间（默认为 1 秒） | 轻点很容易误触，例如从共享工作区中移除成员 |
| `Countdown` | 确认会启动 `CountdownSeconds` 倒计时（默认为 3 秒），取消或 Escape 可以停止它 | 用户在决定之后可能需要最后的机会，例如刚关闭的草稿要放弃 |

### Hold 模式

在 `Hold` 模式下，指针在确认按钮上按下，或确认按钮获得焦点时按下 Enter 或 Space，会从其前缘开始填充确认按钮。填充完成时运行操作。提前松开会使填充弹回为空，并播报需要更长的按住时间。确认按钮的帮助文字为 “Press and hold”。

<Demo name="Hold" />

```xml
<arc:ConfirmMorph Label="移除成员"
                  ConfirmLabel="移除"
                  ConfirmMode="Hold"
                  HoldDuration="00:00:01" />
```

### Countdown 模式

在 `Countdown` 模式下，回答确认会启动倒计时，而不是执行操作。圆环消退，秒数递减。倒计时归零时执行操作；倒计时期间按下 Enter 则立即执行。取消、Escape 键或在外部按下都会停止倒计时，操作不会执行。

<Demo name="Countdown" />

```xml
<arc:ConfirmMorph Label="放弃草稿"
                  ConfirmLabel="放弃"
                  ConfirmMode="Countdown"
                  CountdownSeconds="3" />
```

`CountdownLabel` 设置秒数之前的文字。未设置时，控件使用等待文字，后接 “in”。

### 文字与色调

八个文字属性（`ConfirmLabel`、`CancelLabel`、`PendingLabel`、`DoneLabel`、`ErrorLabel`、`RetryLabel`、`UndoLabel` 与 `UndoingLabel`）分别设置各界面的文字。它们的默认值为 “Delete”、“Cancel”、“Deleting”、“Deleted”、“Couldn’t finish”、“Retry”、“Undo” 与 “Restoring”，因此即使没有设置任何文字属性，控件仍然像一个删除操作。请设置操作所改变的每一项文字，因为默认值是英文的删除用语。

`Tone="Neutral"` 会以强调色为确认回答着色，适用于可以撤销的重要操作。默认的 `Danger` 适用于破坏性操作。

<Demo name="NeutralTone" />

### 受控状态与超时

`State` 为双向绑定。从代码设置它会将控件切换到对应界面，但不会执行操作，因此视图模型可以还原先前会话的结果，或在其自身请求进行期间显示等待界面。`StateChanged` 会在每次切换时报告 `OldState` 与 `NewState`；取消问题或倒计时时会引发 `Cancelled`。

`ConfirmTimeout` 会将未回答的问题恢复为静止状态（默认 6 秒），`ResultTimeout` 会移除结果（默认 5 秒）。指针停留在控件上时，两个计时器都会暂停；窗口最小化时也是如此，因此用户在查看控件时不会被催促。将任一超时设为零即可关闭它。

```csharp
confirm.StateChanged += (_, e) => log.Text = $"状态：{e.NewState}";
confirm.Cancelled += (_, _) => log.Text = "已取消";
confirm.State = ConfirmMorphState.Idle;
```

将 `State` 设为 `Pending` 或 `Done` 会直接显示该界面，而不运行 `ConfirmAction` 或 `UndoAction`。调用 `Ask()` 会像按下静止按钮那样打开问题，`Cancel()` 则关闭已打开的问题或倒计时。除非控件处于 `Idle` 且已启用，否则 `Ask()` 不起作用。

### 焦点与关闭

控件随变形移动焦点，使键盘始终有合理的落点。问题打开时焦点在确认按钮上；倒计时或等待任务运行时焦点在控件本身上；结果或错误出现时焦点在撤销或重试按钮上；控件回到静止时，焦点重新回到静止的药丸上。

打开的问题可以通过以下方式关闭：

- 取消，或按 Escape 键；
- 在控件外按下，这会以“否”回答问题（将 `IsOutsidePressCancelEnabled` 设为 `False`，可让问题在外部按下时保持打开）；
- `ConfirmTimeout`，即用户未作答时。

`ResultTimeout` 也会在用户未对结果采取行动时将其恢复为静止状态。

### 禁用

`IsEnabled` 为 `False` 时，静止的药丸会去掉阴影，`Ask()` 也会忽略该请求。请在附近的行说明中写明原因。

<Demo name="Disabled" />

### 示例：项目危险区

一个设置页面，其中包含两项轻重不同的操作。“归档项目”可以撤销，因此使用中性色调，并在下方的标记中保持打开的问题，以便显示其状态。“删除项目”是永久性的，因此保留危险色调，并以药丸形态静止，直到用户主动操作。每一行都在控件旁用一句话说明后果，因此问题本身可以保持简短。

![危险区，其中归档问题处于打开状态，删除按钮处于静止状态](images/DangerZone.png)

```xml
<StackPanel Width="640" Spacing="16">
    <TextBlock Text="Danger zone"
               FontSize="{arc:ArcDesignTokenResource TypeLG}"
               FontWeight="{arc:ArcDesignTokenResource WeightMedium}"
               Foreground="{arc:ArcDesignTokenResource Foreground}" />
    <Border Background="{arc:ArcDesignTokenResource Surface}"
            BorderBrush="{arc:ArcDesignTokenResource Border}"
            BorderThickness="1"
            CornerRadius="{arc:ArcDesignTokenResource RadiusPanel}"
            Padding="20">
        <StackPanel Spacing="20">
            <Grid ColumnDefinitions="*,Auto">
                <StackPanel Grid.Column="0" Spacing="4" VerticalAlignment="Center">
                    <TextBlock Text="Archive Acme Analytics"
                               FontWeight="{arc:ArcDesignTokenResource WeightMedium}"
                               Foreground="{arc:ArcDesignTokenResource Foreground}" />
                    <TextBlock Text="Archived projects can be restored for 30 days."
                               TextWrapping="Wrap"
                               FontSize="{arc:ArcDesignTokenResource TypeSM}"
                               Foreground="{arc:ArcDesignTokenResource TextSecondary}" />
                </StackPanel>
                <arc:ConfirmMorph Grid.Column="1"
                                  Label="Archive project"
                                  Prompt="Archive this project?"
                                  ConfirmLabel="Archive"
                                  PendingLabel="Archiving"
                                  DoneLabel="Archived"
                                  Tone="Neutral"
                                  State="Confirming"
                                  VerticalAlignment="Center" />
            </Grid>
            <Border Height="1" Background="{arc:ArcDesignTokenResource BorderSubtle}" />
            <Grid ColumnDefinitions="*,Auto">
                <StackPanel Grid.Column="0" Spacing="4" VerticalAlignment="Center">
                    <TextBlock Text="Delete Acme Analytics"
                               FontWeight="{arc:ArcDesignTokenResource WeightMedium}"
                               Foreground="{arc:ArcDesignTokenResource Foreground}" />
                    <TextBlock Text="All 42 dashboards and their data are removed for good."
                               TextWrapping="Wrap"
                               FontSize="{arc:ArcDesignTokenResource TypeSM}"
                               Foreground="{arc:ArcDesignTokenResource TextSecondary}" />
                </StackPanel>
                <arc:ConfirmMorph Grid.Column="1"
                                  Label="Delete project"
                                  Icon="{x:Static arc:ArcIcons.Trash2}"
                                  Prompt="Delete this project?"
                                  VerticalAlignment="Center" />
            </Grid>
        </StackPanel>
    </Border>
</StackPanel>
```

### 绑定到视图模型

使用 CommunityToolkit.Mvvm 时，视图模型拥有操作的状态及其运行的工作。`ConfirmAction` 与 `UndoAction` 是 `Func<Task>` 属性，因此视图模型将它们作为该类型的属性公开并绑定。`State` 为双向绑定，因此视图模型也可以移动控件。

```csharp
public interface IProjectService
{
    Task ArchiveAsync(string projectId);

    Task RestoreAsync(string projectId);

    Task DeleteAsync(string projectId);
}

public partial class ProjectDangerViewModel : ObservableObject
{
    private readonly IProjectService _projects;

    public ProjectDangerViewModel(IProjectService projects)
    {
        _projects = projects;
    }

    [ObservableProperty]
    private ConfirmMorphState _archiveState = ConfirmMorphState.Idle;

    [ObservableProperty]
    private string _projectName = "Acme Analytics";

    public Func<Task> ArchiveAction => () => _projects.ArchiveAsync(ProjectId);

    public Func<Task> UndoArchiveAction => () => _projects.RestoreAsync(ProjectId);

    public Func<Task> DeleteAction => () => _projects.DeleteAsync(ProjectId);

    public string ProjectId { get; } = "prj_2026_0412";
}
```

```xml
<StackPanel Spacing="12">
    <arc:ConfirmMorph Label="归档项目"
                      Prompt="归档此项目？"
                      ConfirmLabel="归档"
                      Tone="Neutral"
                      State="{Binding ArchiveState, Mode=TwoWay}"
                      ConfirmAction="{Binding ArchiveAction}"
                      UndoAction="{Binding UndoArchiveAction}" />
    <arc:ConfirmMorph Label="删除项目"
                      Icon="{x:Static arc:ArcIcons.Trash2}"
                      ConfirmAction="{Binding DeleteAction}" />
</StackPanel>
```

- `State` 为双向绑定，它是唯一需要 `Mode=TwoWay` 的属性（这已是它的默认值）。读取它可以让其他控件保持同步，例如在归档进行时禁用该行的其他操作。
- `ConfirmAction` 与 `UndoAction` 为单向绑定：视图模型提供工作，控件负责调用它。
- 事件 `StateChanged` 适用于记录日志，但绑定了 `State` 之后，视图模型不应需要它。
- 编译绑定需要在视图上设置 `x:DataType`（为简洁起见，示例省略了它）。

### 文案规范

- **标签**：句首大写的动词加宾语，例如“删除项目”“归档项目”。标签是用户在决定之前看到的内容，因此必须说明操作会做什么。
- **问题**：包含数量或名称并以问号结尾，例如“删除 3 个文件？”。写明对象的问题会告诉用户他们正在回答什么。
- **确认文字**：只用动词，例如“删除”“归档”“吊销”。它用与标签相同的词回答问题，因此从不出现“是”。
- **完成文字**：动词的过去式，例如“已删除”“已归档”。保持简短，使结果能放进药丸中。
- **错误文字**：说明操作未能完成，而不是用技术术语描述哪里出错。下一步由重试按钮处理。

| 不要写 | 改为 |
|---|---|
| 是 | 删除 |
| 确定要删除此项目吗？ | 删除 3 个文件？ |
| 哎呀，出错了！ | 未能完成 |
| 完成！ | 已删除 |
| 撤销上一步操作 | 撤销 |

### 最佳实践

**推荐**

- 在标签或问题中写明对象，使问题在语境中即可回答：“删除 3 个文件？”无需阅读页面的其他部分即可理解。
- 只有可撤销的操作才使用 `Tone="Neutral"`。用户无法收回的操作保留危险色调，使颜色保持其含义。
- 当服务器能在一段时间内还原对象时，提供 `UndoAction`。结果会保持足够长的时间，让用户在 `ResultTimeout` 将控件恢复为静止之前按下撤销。
- 设置操作所需的每一个文字属性。默认值描述的是删除；若归档操作上显示通用的“Deleting”标签，就会像是一个错误。
- 对于重复执行成本较高的操作使用 `Countdown` 或 `Hold`，其余使用 `Press`。每个屏幕只用一种模式，使各个回答保持一致。

**避免**

- 将控件用于需要多行说明或输入名称的后果。问题界面只有一行；应把详细内容放在 `Dialog` 中。
- 过长的问题。问题界面按一行短文字设计，问题的最大宽度为 256 px；请保持为关于对象的一个问题。
- 为用户必须阅读的问题把 `ConfirmTimeout` 设得过短。6 秒足够一瞥；远少于此，问题会在被阅读之前消失。
- 默认关闭 `IsOutsidePressCancelEnabled`。外部按下却没有任何反应，会让人觉得控件卡住了；除非问题必须在用户于别处工作时保持打开，否则请保留默认值。
- 在 `ConfirmAction` 中运行耗时操作却没有等待文字。加载指示器与文字是工作正在进行的唯一信号。

### 常见问题

**为什么打开问题的那个 Enter 键不能立即作答？** 问题打开后的前 280 ms 内的按下会被忽略，Enter 的自动重复也会被忽略。这样可以防止双击或按住的按键在用户尚未读到问题时就作答。

**操作失败后，控件会处于损坏状态吗？** 不会。抛出异常的 `ConfirmAction` 任务会使控件切换到 `Error`，显示 `ErrorLabel` 与重试按钮。重试会再次运行 `ConfirmAction`，而 Escape 会使控件回到静止状态，且不运行操作。

**如何从键盘快捷键或代码打开问题？** 调用 `Ask()`。它的效果与按下静止的药丸相同；除非控件处于 `Idle` 且已启用，否则该调用会被忽略。

**如何知道操作何时完成？** 监视 `State` 是否变为 `Done`，或处理 `StateChanged`。`ConfirmAction` 完成后控件切换到 `Done`，抛出异常时切换到 `Error`，因此视图模型在两种情况下都能刷新其列表。

## API 参考

<!-- api: ConfirmMorph -->

## 无障碍

- 问题是一个以其提示命名的组。静止的按钮保留其标签的名称。
- 焦点随变形移动：问题打开时在确认按钮上，倒计时或等待工作运行时在控件本身上，结果或错误出现时在撤销或重试按钮上，控件回到静止时回到静止的药丸上。
- 问题打开后的前 280 ms 内的按下会被忽略，因此双击或按住 Enter 不会意外作答。
- 在 `Hold` 模式下，帮助文字为 “Press and hold”。提前松开会播报需要更长的按住时间。
- 提示、等待与结果的文字，以及 “Undo is available”，都会通过状态区域礼貌地播报。
- Enter 与 Space 会按下当前界面上的按钮。Escape 会取消问题或倒计时，并使结果或错误回到静止状态。
- 键盘焦点以按钮背景的色调呈现（依色调与状态为 6% 至 20%），而不是焦点环。

### 键盘

| 按键 | 操作 |
|---|---|
| `Enter` / `Space`（静止的药丸上） | 打开问题 |
| `Tab` / `Shift+Tab` | 在当前显示界面的按钮之间移动，例如取消与确认 |
| `Enter`（`Press` 模式） | 在问题的任何位置回答“是”，取消按钮获得焦点时除外 |
| `Enter` / `Space`（确认按钮上，`Hold` 模式） | 按住时填充确认按钮；填充完成时运行操作 |
| `Enter` / `Space`（确认按钮上，`Countdown` 模式） | 启动倒计时 |
| 倒计时期间按 `Enter` | 立即运行操作 |
| 取消、撤销或重试按钮上的 `Enter` / `Space` | 按下该按钮 |
| `Escape` | 取消问题或倒计时；将结果或错误恢复为静止状态 |

## 动效

- 只有药丸的宽度会变化。它以弹簧方式展开，收缩则更平顺，文字不会缩放。
- 每个新界面的各个部分按阅读顺序依次清除轻微模糊，旧界面则原地淡出。
- 结果的对勾会自行绘制。倒计时圆环消退、数字滚动，等待指示器旋转。
- 在 `Hold` 模式下，按住确认时，危险色的填充会从前缘显露；提前松开则以弹簧回弹。
- 减弱动效时，取消宽度弹簧与模糊，界面之间交叉淡化，并放慢指示器旋转。按住填充与倒计时圆环仍会显示进度。关闭动效时，界面立即切换。

| 变化 | 完整 | 减弱 | 关闭 |
|---|---|---|---|
| 问题打开或关闭 | 药丸宽度以弹簧展开，收缩更平顺 | 没有宽度弹簧；界面交叉淡化 | 立即完成 |
| 界面切换 | 各部分按阅读顺序每隔 35 ms 清除轻微模糊；旧界面在 120 ms 内淡出 | 交叉淡化 | 立即完成 |
| 结果 | 对勾自行绘制 | 对勾直接出现，不绘制 | 立即完成 |
| 倒计时 | 圆环消退，秒数滚动 | 圆环仍然消退 | 立即完成 |
| 等待 | 指示器旋转 | 指示器放慢旋转 | 立即完成 |
| `Hold` 填充 | 填充从前缘显露，提前松开时弹回 | 填充仍显示进度 | 立即完成 |

## 相关组件

- `Dialog`：后果需要更多说明或输入名称时使用。
- `ActionButton`：没有确认问题的异步提交。
- `HoldToConfirm`：绝不能在单击时触发的操作。
- `DropdownMenu`：没有单一默认操作的命令列表。

| 组件 | 适用场景 |
|---|---|
| `Dialog` | 后果需要多行说明、受影响项目的列表或输入名称 |
| `ActionButton` | 操作安全且是异步的，不应先行询问 |
| `HoldToConfirm`（Gestures） | 单次按住就是全部交互，且操作绝不能因轻点而触发 |
| `DropdownMenu`（Menus） | 行或工具栏提供多个命令，且其中没有默认操作 |
| `CopyButton` | 操作将值复制到剪贴板，并在原处显示结果 |
| `ButtonGroup` | 多个按钮组成一个工具栏，且它们都不需要自己的询问 |
