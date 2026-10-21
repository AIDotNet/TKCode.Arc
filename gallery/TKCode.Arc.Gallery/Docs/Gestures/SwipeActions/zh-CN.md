---
title: 滑动操作
summary: 通过滑动显示行操作，或从菜单中的同一组操作执行。
source-hash: 1717751e77c9
---

<Demo name="Inbox" />

## 何时使用

`SwipeActions` 是一个列表，其中的行被横向滑动时会露出操作。它为触摸优先的分拣而设计：人逐条处理长列表，每一项通常只需要一两个快速决定。操作位于行的下方，因此决定就在阅读该项的地方做出；完整滑动会移除该行并填补空隙，无需单独确认。

- **平板上的邮件分拣**：在支持或销售收件箱中，向左滑动可归档会话，向右滑动可将其标为未读。两个操作都只需从行上点按一下，归档后列表会填补空隙。
- **手机上的外勤服务工单列表**：技术人员向右滑动以标记工单完成，向左滑动以改期。工单卡片在操作下方仍可读，因此无需打开工单即可做出决定。
- **手机上的审批**：经理用强调色滑动批准采购申请，用危险色滑动拒绝。拒绝行关闭了完整滑动，因此一次长拖动不会误拒申请。
- **配套应用中的通知中心**：登录提醒以危险色滑动关闭，而耗时较长的导出则通过一个让行保持原位的操作静音。

## 何时不用

- **桌面表格或主要用鼠标操作的列表**：在行上使用 `ContextMenu`，或在行按钮上使用 `DropdownMenu`。滑动手势不适合作为鼠标的主要操作路径，而且每一行本来就有菜单。
- **通过上下拖动调整行顺序**：用 `ReorderableList`。滑动手势锁定在水平方向，永远不会改变行的顺序。
- **大量列、分页或排序的长数据集**：用 `SortableDataTable`。滑动行不做虚拟化，因此数千行的列表在此显示前应先分页。
- **列表之外的单个破坏性操作**，例如在设置页面删除项目：用 `HoldToConfirm`，它需要刻意的长按，而不是行上可能被误触发的手势。
- **一侧超过两个快捷操作**：露出的宽度会随每个操作增加。把两个最常用的操作放在滑动上，其余的移入该行的“更多操作”菜单，或用 `ContextMenu` 显示完整列表。

## 用法

`SwipeActions` 即列表本身。每一项是一个 `SwipeActionsRow`，或是通过 `ItemTemplate` 显示的数据项。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:SwipeActions Label="收件箱" ItemsSource="{Binding Messages}">
        <arc:SwipeActions.LeadingActions>
            <arc:SwipeAction Label="未读" Tone="Accent" KeepsRow="True" />
        </arc:SwipeActions.LeadingActions>
        <arc:SwipeActions.TrailingActions>
            <arc:SwipeAction Label="归档" />
            <arc:SwipeAction Label="删除" Tone="Danger" />
        </arc:SwipeActions.TrailingActions>
        <arc:SwipeActions.ItemTemplate>
            <DataTemplate>
                <TextBlock Text="{Binding Subject}" />
            </DataTemplate>
        </arc:SwipeActions.ItemTemplate>
    </arc:SwipeActions>
</UserControl>
```

前导与尾随操作由每一行共用。某一侧最外层的操作即为完整滑动时执行的操作。

`SwipeActions` 派生自 Avalonia 的 `ItemsControl`，因此 `ItemsSource`、`ItemTemplate` 与项容器的工作方式与普通列表一致。每个容器都是 `SwipeActionsRow`，它派生自 `ContentControl`，因此行也可以单独使用。每个 `SwipeAction` 都是普通的模型对象，而不是控件：它保存标签、图标、色调和命令，由行负责绘制。

前导操作位于左边缘下方，向右滑动时露出。尾随操作位于右边缘下方，向左滑动时露出。每一侧可以容纳任意数量的操作，但露出的宽度会随每个操作增加，因此实际上每侧两个是上限。

### 结构

1. **列表外框**（`Label` 为其命名）：所有行外围的一个表面，行与行之间有一条细分隔线。分隔线从左边缘向内 16 px 处开始（`SeparatorInset`）。
2. **行主体**（`MinRowHeight` 为 64 px）：你提供的内容，左侧内边距 16 px，上下各 12 px，右侧在“更多操作”按钮前为 8 px。内容更高时，行会随之变高。
3. **更多操作按钮**：32 px 的方块，圆角 12 px，省略号图标 18 px。它是通往相同操作的键盘与屏幕阅读器路径，因此每一行都始终存在。
4. **露出的操作**：每个操作静止时宽 76 px，图标为 20 px 的字形，位于标签上方。图标与标签相距 3 px。中性操作用前景色按 20% 混入表面色作为填充；强调与危险操作使用主题色。
5. **完整滑动拉伸**：滑动越过确认阈值时，最外层的操作会横向拉伸铺满整行。
6. **更多操作菜单**：宽度至少 176 px 的菜单，位于“更多操作”按钮下方 6 px。它按滑动的顺序列出该行的操作，并标记破坏性操作，使其一望即知。

### 色调

`Tone` 设置露出操作的填充。应根据结果选择色调，而不是根据它在行上的位置。

| 色调 | 填充 | 用于 |
|---|---|---|
| `Neutral`（默认） | 平静的中性填充 | 归档、稍后提醒、移动，以及其他可逆的整理操作 |
| `Accent` | 强调色 | 完成、批准、标为未读：正面或主要的结果 |
| `Danger` | 危险色 | 删除、拒绝、关闭：移除或拒绝某物的结果 |

将 `Danger` 操作与一个说明结果的标签配对，例如“删除”或“拒绝”，使颜色不是唯一的提示。每侧至多保留一个 `Danger` 操作，并把它放在最外层，使完整滑动只能在刻意的情况下触及它。

### 执行操作

选中操作时，`Command` 会执行，可以通过完整滑动、点按露出的操作或“更多操作”菜单触发。`SwipeAction` 上的 `Invoked` 以及列表或行上的 `ActionInvoked` 会告诉你是哪一行、哪一项。操作的 `Invoked` 处理程序先执行，然后执行其 `Command`。未设置 `CommandParameter` 时，默认为该行的数据项。

`KeepsRow="True"` 会执行操作并让行回到原位，而不是将其移除。适用于未读、静音、标记等切换类操作，因为这些情况下行应保留在列表中。

```csharp
inbox.ActionInvoked += (_, e) =>
{
    if (!e.Action.KeepsRow)
    {
        messages.Remove((Message)e.Item!);
    }
};
```

在处理程序中移除数据项。不保留行的操作要等到行滑出并收起后才执行，因此列表填补空隙时不会跳动。处理程序可以直接移除该项。如果操作执行后 1.4 秒该项仍在列表中，行会滑回原位，因此从不移除其项目的操作不会留下空白带。

收件箱示例保留被移除的邮件并提供撤销，这是归档或删除应遵循的模式。当人点按“撤销”时，被移除的行会插回原来的索引。

### 行自带操作

`SwipeActionsRow` 可以声明自己的操作。声明了操作的行会忽略列表的前导与尾随操作。行上的 `IsFullSwipeEnabled` 会覆盖列表的设置：`False` 会阻止该行在完整滑动时确认，因此人必须点按露出的操作；`True` 则允许完整滑动，即使列表将其关闭；`null`（默认值）则跟随列表。

<Demo name="OwnActions" />

### 保持展开

`Open(SwipeActionSide.Trailing)` 无需手势即可显示行的操作，使行弹回到其静止宽度。露出一个操作的行静止在 76 px；露出两个的行静止在 152 px。列表一次只保持一行展开：打开一行会收起之前展开的那一行。`Close()` 收起已展开的行，`SwipeActions.CloseRows()` 关闭列表中已展开的行。`OpenRow` 返回已展开的行，若没有则返回 `null`。

```csharp
// 通知页面打开时显示导出行的操作。
export.Open(SwipeActionSide.Trailing);
```

用保持展开的行来表明可以滑动，例如人第一次打开通知列表时。不要让行永久保持展开，因为它会遮住行的内容，让列表看起来像是坏了。

<Demo name="RestingOpen" />

### 完整滑动

完整滑动会执行某一侧最外层的操作。阈值取两个值中较大的一个：该侧的展开宽度加 48 px，或行宽的 56%。行越过阈值后会保持待命状态，直到拖动回退 20 px，因此轻微抖动不会取消执行。速度超过 900 px/s 的快速轻扫，若沿其方向带动行越过阈值，同样会执行。

对于误拖代价高昂的操作，例如拒绝或付款，在列表或行上设置 `IsFullSwipeEnabled="False"`。露出的操作仍可通过点按执行。

### 绑定到视图模型

在 MVVM 应用中，视图模型持有集合以及移除项目的规则。列表绑定到该集合，而操作在代码中连接，因为 `SwipeAction` 是普通的模型对象，不是可视树中的控件。

```csharp
public partial class InboxViewModel : ObservableObject
{
    private readonly IMailService _mail;

    public InboxViewModel(IMailService mail)
    {
        _mail = mail;
    }

    public ObservableCollection<MailThread> Threads { get; } = [];

    [RelayCommand]
    private async Task ArchiveAsync(MailThread thread)
    {
        await _mail.ArchiveAsync(thread.Id);
        Threads.Remove(thread);
    }

    [RelayCommand]
    private void ToggleUnread(MailThread thread)
    {
        thread.IsUnread = !thread.IsUnread;
    }
}
```

```xml
<arc:SwipeActions Label="收件箱"
                  ItemsSource="{Binding Threads}"
                  RowLabelBinding="{Binding Subject}">
    <arc:SwipeActions.ItemTemplate>
        <DataTemplate>
            <StackPanel Spacing="2">
                <TextBlock Text="{Binding Sender}" FontWeight="SemiBold" />
                <TextBlock Text="{Binding Subject}" />
                <TextBlock Text="{Binding Preview}" Foreground="{arc:ArcDesignTokenResource TextSecondary}" />
            </StackPanel>
        </DataTemplate>
    </arc:SwipeActions.ItemTemplate>
</arc:SwipeActions>
```

```csharp
// 在视图的代码后置文件中，即视图模型可用的地方：
var archive = new SwipeAction { Label = "归档", Icon = ArcIcons.Archive, Command = viewModel.ArchiveCommand };
var unread = new SwipeAction { Label = "未读", Icon = ArcIcons.Mail, Tone = SwipeActionTone.Accent, KeepsRow = true, Command = viewModel.ToggleUnreadCommand };
inbox.TrailingActions.Add(archive);
inbox.LeadingActions.Add(unread);
```

- 列表读取 `ItemsSource`，视图模型通过改变集合来增加或移除行。请使用 `ObservableCollection<T>`，这样移除项目时列表会更新。
- `RowLabelBinding` 为每个生成的行命名，供其“更多操作”按钮使用，例如“更多操作：发票 #2041”。
- 命令接收该行的数据项作为参数，因此一个命令即可服务于所有行。
- 编译绑定需要在视图和数据模板上设置 `x:DataType`（为简洁起见，这里的示例省略了它）。

### 示例：平板上的邮件分拣

平板上的支持收件箱。主题是行的第一行并加粗，发件人与时间提供背景，预览让人判断该会话是否需要自己处理。前导操作用强调色把会话标为未读，尾随操作则用于归档或删除。本场景中的行没有声明自己的操作，因此每一行都使用列表的操作。截图展示的是静止的列表；只有在行被滑动或打开时，露出的操作才会出现。

![带有三行的收件箱列表，每行都有发件人、主题和预览](images/InboxTriage.png)

```xml
<Border Width="560"
        CornerRadius="12"
        BorderThickness="1"
        BorderBrush="{arc:ArcDesignTokenResource Border}"
        Background="{arc:ArcDesignTokenResource Surface}"
        ClipToBounds="True">
    <arc:SwipeActions Label="Support inbox">
        <arc:SwipeActions.LeadingActions>
            <arc:SwipeAction Label="Unread" Icon="{x:Static arc:ArcIcons.Mail}" Tone="Accent" KeepsRow="True" />
        </arc:SwipeActions.LeadingActions>
        <arc:SwipeActions.TrailingActions>
            <arc:SwipeAction Label="Archive" Icon="{x:Static arc:ArcIcons.Archive}" />
            <arc:SwipeAction Label="Delete" Icon="{x:Static arc:ArcIcons.Trash2}" Tone="Danger" />
        </arc:SwipeActions.TrailingActions>
        <arc:SwipeActionsRow Label="Invoice #2041 is overdue">
            <StackPanel Spacing="2" VerticalAlignment="Center">
                <TextBlock Text="Northwind Finance" FontSize="14" FontWeight="SemiBold"
                           Foreground="{arc:ArcDesignTokenResource Foreground}" />
                <TextBlock Text="Invoice #2041 is overdue" FontSize="13"
                           Foreground="{arc:ArcDesignTokenResource Foreground}" />
                <TextBlock Text="Payment was due 14 October 2026. Reply with a date to keep the account active."
                           FontSize="13" TextWrapping="Wrap"
                           Foreground="{arc:ArcDesignTokenResource TextSecondary}" />
            </StackPanel>
        </arc:SwipeActionsRow>
        <arc:SwipeActionsRow Label="Acme Analytics renewal">
            <StackPanel Spacing="2" VerticalAlignment="Center">
                <TextBlock Text="Acme Analytics" FontSize="14"
                           Foreground="{arc:ArcDesignTokenResource Foreground}" />
                <TextBlock Text="Renewal quote for 2027" FontSize="13"
                           Foreground="{arc:ArcDesignTokenResource Foreground}" />
                <TextBlock Text="The seat count moved from 40 to 52. Review before Friday."
                           FontSize="13" TextWrapping="Wrap"
                           Foreground="{arc:ArcDesignTokenResource TextSecondary}" />
            </StackPanel>
        </arc:SwipeActionsRow>
        <arc:SwipeActionsRow Label="Team lunch on Thursday">
            <StackPanel Spacing="2" VerticalAlignment="Center">
                <TextBlock Text="Maya Chen" FontSize="14"
                           Foreground="{arc:ArcDesignTokenResource Foreground}" />
                <TextBlock Text="Team lunch on Thursday" FontSize="13"
                           Foreground="{arc:ArcDesignTokenResource Foreground}" />
                <TextBlock Text="Table for six at 12:30. Tell me if you cannot make it."
                           FontSize="13" TextWrapping="Wrap"
                           Foreground="{arc:ArcDesignTokenResource TextSecondary}" />
            </StackPanel>
        </arc:SwipeActionsRow>
    </arc:SwipeActions>
</Border>
```

这三行展示了分拣列表需要的三类内容：一个需要采取行动的主题、一个只供参考的会话，以及一条个人消息。列表使用同一组操作，因此同一种滑动在每一行上的含义都相同。危险操作位于最外层，因此只有刻意的完整滑动才能触及它，而“更多操作”菜单则为键盘用户提供相同的操作。

### 内容规范

- **操作标签**：一个简短的词，例如“归档”“删除”“完成”或“未读”。标签位于 76 px 宽的列中、20 px 图标的下方，较长的短语会被截断。较长的动词留给菜单，那里有完整的宽度。
- **行标签**：主题或项目名称，例如“发票 #2041 已逾期”。它为“更多操作”按钮命名这一行，因此要写成屏幕阅读器用户能够识别的句子。
- **列表标签**：集合的名称，例如“收件箱”“任务”“通知”。它是列表的无障碍名称。
- **结果用语**：说明结果，而不是手势。“删除”描述发生了什么；“滑动删除”描述如何达到它，而且滑动并不是唯一的途径。

| 不要写 | 改为 |
|---|---|
| 滑动归档 | 归档 |
| 将此会话移到归档文件夹 | 归档 |
| 移除 | 删除 |
| 标为未读 | 未读 |
| 彻底删除 | 删除 |

### 最佳实践

**推荐**

- 把完整滑动应执行的操作放在某一侧的最外层，因为该位置的操作会被确认执行。除非提供了撤销，否则不要把破坏性操作放在该位置。
- 对于改变行状态的切换操作（例如未读或标记），使用 `KeepsRow="True"`；只有在操作结束时项目也随之结束的情况下，才移除项目。切换操作若移除该行，人想要撤销时会很困惑。
- 通过 `RowLabelBinding` 为每一行设置 `Label`，让“更多操作”按钮指明它所属的行。
- 在移除类操作之后提供撤销路径，例如按钮或轻提示，因为完整滑动很快，也容易被误触。
- 同一列表中的所有行使用相同的操作，除非某一行确有不同的理由，例如被锁定的项目。

**避免**

- 仅用手势作为操作方式。“更多操作”按钮始终存在，它服务于无法滑动的键盘和屏幕阅读器用户。
- 在行的两侧都放危险操作，这会使任一方向的误滑动都具有破坏性。
- 露出操作使用过长的标签。标签会被截断到 76 px 的列宽内，因此含义必须能用一个词表达。
- 在每一行上都关闭完整滑动。对于可逆的操作保持开启，只在拒绝或付款这类场景关闭，因为额外的一次点按正是安全保障。
- 一个列表中有数千行。行不做虚拟化，列表会构建每一个容器，因此应对数据分页。

### 常见问题

**为什么我的处理程序在行滑出后才运行？** 移除其项目的操作会在行滑出并收起之后运行，因此列表填补空隙时不会出现可见的跳动。如果需要操作立即执行，请设置 `KeepsRow="True"`，并稍后移除项目，例如在服务端确认更改之后。

**如果项目没有被移除会怎样？** 如果操作执行后 1.4 秒项目仍在列表中，行会滑回原位。请在处理程序中移除项目，或在行应保留时设置 `KeepsRow`。

**如何阻止完整滑动执行破坏性操作？** 在行上将 `IsFullSwipeEnabled` 设为 `False`。人仍可露出操作并点按，但长拖动只会展开该行。

**为什么我的行的操作忽略了列表的操作？** 声明了自己前导或尾随操作的行只使用那些操作。没有声明操作的行则以列表的操作为默认值。

**可以在没有列表的情况下单独使用一行吗？** 可以。`SwipeActionsRow` 可以独立工作，拥有自己的操作以及自己的 `Open`、`Close` 和 `Run` 方法。它没有列表，因此单一展开规则与列表的完整滑动设置都不适用。

## API 参考

<!-- api: SwipeActions -->

<!-- api: SwipeAction -->

<!-- api: SwipeActionsRow -->

### 色调与侧边值

| 类型 | 值 | 含义 |
|---|---|---|
| `SwipeActionTone` | `Neutral` | 平静的中性填充（默认） |
| `SwipeActionTone` | `Accent` | 强调色填充 |
| `SwipeActionTone` | `Danger` | 危险色填充，用于破坏性或拒绝性的结果 |
| `SwipeActionSide` | `None` | 行已收起 |
| `SwipeActionSide` | `Leading` | 位于左边缘下方，向右滑动时露出 |
| `SwipeActionSide` | `Trailing` | 位于右边缘下方，向左滑动时露出 |

## 无障碍

- 列表的名称来自 `Label`，每一行都是列表项。每一行都有一个“更多操作”按钮，名称为“更多操作：”加上该行的 `Label`。
- 鼠标与键盘用户无需使用手势：按 Tab 移到“更多操作”按钮，按 Enter 或 Space 打开菜单，再用方向键移动。
- 按 Esc 关闭菜单或收起已展开的行。在其他位置按下时也会收起已展开的行。
- 露出的操作对辅助技术隐藏，且不在 Tab 顺序中。
- 当某一行通过菜单被移除时，焦点移到相邻行的“更多操作”按钮。最后一行离开时，焦点落在列表本身上。
- 菜单列出与滑动相同的操作，破坏性条目会被标记为破坏性，因此屏幕阅读器用户会在操作执行前听到风险提示。

### 键盘

| 按键 | 操作 |
|---|---|
| `Tab` / `Shift+Tab` | 在各行的可聚焦内容与“更多操作”按钮之间移动；露出的操作会被跳过 |
| “更多操作”按钮上的 `Enter` 或 `Space` | 打开该行的菜单，并高亮第一项 |
| “更多操作”按钮上的 `Up` 或 `Down` | 菜单关闭时，打开菜单并定位到最后一项或第一项 |
| 菜单中的 `Up` / `Down` | 在各操作之间移动 |
| 菜单中的 `Enter` | 执行高亮的操作 |
| `Escape` | 关闭菜单，或收起已展开的行 |

## 动效

- 行跟随指针移动。超过最后一个停靠点后会产生阻力，因此行会稍微拉伸，然后回弹。
- 松开后，行以保留松开速度的弹簧停靠到最近的停靠点。快速轻扫会被视为滑向对应方向的停靠点。
- 完整滑动时，最外层的操作会横向拉伸铺满整行，行随后滑出，列表再填补空隙。
- 减弱动效时，行直接跳到停靠点，行的出现与消失改为淡入淡出，不做高度动画。关闭动效时，所有内容一次移动到位。

| 变化 | 完整 | 减弱 | 关闭 |
|---|---|---|---|
| 行跟随指针 | 跟随指针移动，超过最后一个停靠点后有橡皮筋式阻力 | 不跟随；行直接跳到停靠点 | 不跟随；行直接跳到停靠点 |
| 松开 | 以松开速度弹簧停靠：关闭时平滑，打开时利落 | 直接跳到停靠点 | 立即完成 |
| 完整滑动 | 最外层的操作横向拉伸铺满整行，行随后滑出 | 淡出 | 立即完成 |
| 行被移除 | 行的高度在 120 ms 后以平滑弹簧收起，列表填补空隙 | 淡出，无高度动画 | 立即完成 |
| 行被添加 | 行以平滑高度弹簧与淡入增长出现 | 淡入，无高度动画 | 立即完成 |
| “更多操作”菜单 | 随菜单面板的过渡开合 | 菜单面板的减弱过渡 | 菜单面板的关闭过渡 |

滑动本身是人唯一能控制的动效，因此在每一种动效等级下都应保持直接而灵敏。松开时使用弹簧来显示行将停靠的位置，并省去周围列表上的装饰性移动。

## 相关组件

- `ContextMenu` 与 `DropdownMenu`：桌面端的行操作。
- `ReorderableList`：通过拖动把行重新排序。
- `HoldToConfirm`：列表之外的单个破坏性操作。
- `SortableDataTable`：大型、可排序的数据集。

| 组件 | 适用场景 |
|---|---|
| `ContextMenu` / `DropdownMenu` | 主要输入方式是鼠标，或行上有很多操作 |
| `ReorderableList` | 人通过拖动改变行的顺序 |
| `SortableDataTable` | 数据有列、排序或数量很多的行 |
| `HoldToConfirm` | 一个破坏性操作需要刻意长按，且不在列表中 |
| `Dialog` | 操作执行前必须先阅读后果 |
| `Toast` | 操作已执行，应报告结果并提供撤销 |
