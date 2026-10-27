---
title: 通知中心
summary: 集中查看通知，支持已读状态、分组信息和带动画的展开与收起。
source-hash: bf8420de3c62
---

<Demo name="Preview" />

## 何时使用

通知中心是更新等待用户处理的地方。它是应用头部中带未读数量的铃铛，以及一个列出更新的面板，面板中有每条更新的已读状态、详情和批量操作。它位于外壳头部的用户菜单和搜索旁边，并且会在时机过去之后保留更新：轻提示会自行消失，而通知会一直留着，直到用户阅读或关闭它。

- **协作工具中的提及与评论**：同事在 "Checkout redesign" 讨论串中提到你，而你正在另一个项目里。更新会留在铃铛中，角标告诉你它仍未处理。
- **分析产品中已完成的后台任务**：报表导出或数据刷新在用户浏览仪表板时完成。"Your export is ready to download" 会连同时间留在面板中，方便之后再找到。
- **运营控制台中的审批与指派**：一张采购单或一次部署正在等待决定。用户看过后可以标记为已读，决定做出后再关闭。
- **SaaS 管理后台中的账号与账单警告**：套餐存储已用 85%，或一张卡下个月到期。`Warning` 语气会以警告色绘制图标，使其与日常更新区分开来。
- **值班人员的故障与状态变化**："Incident resolved" 及其时间，以 `Success` 语气显示，供故障发生时不在状态页前的工程师查看。

## 何时不用

- **无需跟进的确认**，例如 "Invoice saved"：用 `Toast`，它会自行消失。
- **当前页面上用户必须立即修复的问题**，例如结账表单中的支付失败：在问题所在处用 `Alert`，使它在页面被修正前一直可见。
- **可搜索、可筛选的事件历史**，例如审计日志：在页面上用 `Table`。单条记录的有序事件用 `Timeline`。
- **带回复的对话**：用 `ChatThread`，它有输入框和回复流程。面板对一条更新只提供 "Mark read"、"Mark unread" 和 "Dismiss"。
- **待办工作的计数**，例如导航项上的 14 个未完成任务：用 `Badge`。面板列出的是更新，而不是计数器。

## 用法

`NotificationCenter` 派生自 Avalonia 的 `TemplatedControl`。它的模板包含铃铛按钮、未读角标和面板，面板在弹出层中打开。`NotificationItem` 对象是数据：每个都是带有面板所读属性的 `AvaloniaObject`。可以把它们作为子元素添加，也可以把 `ItemsSource` 绑定到你自己的集合。每一项都有 `Title`、可选的 `Description`、`Time` 标签和 `IsRead` 状态。人发出的更新会设置 `ActorName` 和 `ActorSource`，并显示其头像。系统更新显示其 `Tone` 对应的图标：`Info`、`Success` 或 `Warning`。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:NotificationCenter>
        <arc:NotificationItem Id="deploy" Title="部署已完成" Description="2.4.1 版本已上线。" Time="18m" Tone="Success" />
        <arc:NotificationItem Id="mention" Title="Mia Chen 提到了你" Time="2m" ActorName="Mia Chen" />
        <arc:NotificationItem Id="digest" Title="你的每周摘要已就绪" Time="昨天" IsRead="True" />
    </arc:NotificationCenter>
</UserControl>
```

铃铛显示未读数量，最多显示到 "9+"，没有未读项时隐藏。点击它打开面板。点击某一行可以展开其详情，并使用 "Mark read"（或 "Mark unread"）和 "Dismiss"。同一时间只展开一行。

### 结构

1. **铃铛**：42 px 的方形按钮，圆角 12 px，表面色上有 1 px 边框。铃铛图形为 19 px。悬停、按下和打开时使用较强的边框与较淡的表面色；任何状态都不缩放。
2. **未读角标**：20 px 高的胶囊，左右内边距 5 px，表面色的 2 px 环。它位于铃铛右上角外侧 5 px 处，显示 `UnreadCount`，从 10 起显示为 "9+"。当所有项都已读时不显示。
3. **面板**：宽 424 px，或窗口宽度减 24 px，高度最多 590 px。它使用抬起的表面色、1 px 边框、面板圆角和浮动阴影。它锚定在铃铛下方，间距 12 px。
4. **标题**（`Label`）：特大字号、中等字重，旁边是一个 21 px 的计数标签，显示未读数量。
5. **摘要**：标题下方一行，内容来自 `Labels.Summary`，使用次要文字颜色。
6. **关闭按钮**：30 px 的方形按钮，圆角 8 px，名称由 `Labels.Close` 提供。
7. **工具栏**：All 与 Unread 两个视图按钮，高 31 px，宽至少 61 px，彼此相距 3 px，高亮块在两者之间滑动。"Mark all read" 位于末尾，仅在有未读项时出现。
8. **行**：列表的内边距为 9 px、11 px 和 10 px。每行显示发出者的头像或 36 px 的语气图标、中等字重的标题（已读时为次要颜色）、6 px 的未读圆点、一行预览文字，以及弱化文字颜色的时间标签。
9. **详情**：展开的行显示完整描述，以及 "Mark read" 或 "Mark unread" 和 "Dismiss" 操作。操作按钮高 30 px，圆角 10 px，缩进到文字列。
10. **空状态**：高度至少 190 px，带有一个勾选图标，标题和描述来自标签。
11. **页脚**：至少有一项已读时出现。显示已读数量（"3 read"）和 "Clear read"。

### 已读状态与你的数据

控件会修改项上的 `IsRead` 并触发 `ReadChanged`，其参数包含 `Item` 和新的 `IsRead`。"Mark all read" 会为它标记的每一项各触发一次。"Dismiss" 会从 `Items` 中移除该项；如果 `ItemsSource` 是可修改的列表，则从 `ItemsSource` 中移除，并触发 `Dismissed`，参数为 `Item`。"Clear read" 会为每个已读项各触发一次。事件运行时该项已经改变，因此请在处理程序中把修改保存到你的存储。当你的存储发生变化时，铃铛和面板会跟随项和集合更新。

<Demo name="ReadState" />

```csharp
center.ItemsSource = store;   // 一个 ObservableCollection<NotificationItem>
center.ReadChanged += (_, e) => _vm.SaveReadStateAsync(e.Item);
center.Dismissed += (_, e) => _vm.DismissAsync(e.Item);
```

只读数据源会保留其项，控件则隐藏已关闭的项，直到再次设置 `ItemsSource`。

### 绑定到视图模型

视图模型拥有更新及其持久化。控件只负责显示它们，并报告用户的更改。视图模型持有一个 `ObservableCollection<NotificationItem>` 以及面板的打开状态。

```csharp
public abstract class ActivityStore
{
    public abstract IEnumerable<NotificationItem> LoadRecent();
    public abstract Task SetReadAsync(string id, bool isRead);
    public abstract Task DismissAsync(string id);
}

public partial class ActivityViewModel : ObservableObject
{
    private readonly ActivityStore _store;

    public ActivityViewModel(ActivityStore store)
    {
        _store = store;
        foreach (var update in store.LoadRecent())
        {
            Updates.Add(update);
        }
    }

    // Dismiss 会从此集合中移除一项；对于可写的数据源，由控件执行这一步。
    public ObservableCollection<NotificationItem> Updates { get; } = new();

    [ObservableProperty]
    private bool _isActivityOpen;

    public Task SaveReadStateAsync(NotificationItem item) => _store.SetReadAsync(item.Id, item.IsRead);

    public Task DismissAsync(NotificationItem item) => _store.DismissAsync(item.Id);
}
```

```xml
<arc:NotificationCenter x:Name="Activity"
                        Label="活动"
                        ItemsSource="{Binding Updates}"
                        IsOpen="{Binding IsActivityOpen, Mode=TwoWay}" />
```

```csharp
// 在代码后置中，控件名为 Activity：控件没有命令，因此使用它的事件。
Activity.ReadChanged += (_, e) => _ = _vm.SaveReadStateAsync(e.Item);
Activity.Dismissed += (_, e) => _ = _vm.DismissAsync(e.Item);
```

- `IsOpen` 和 `View` 是双向的。当其他界面（例如头部的快捷入口）要打开面板时，绑定 `IsOpen`。
- `NotificationItem.IsRead` 也是双向的。视图模型更新它时，铃铛随之变化；用户标记某项时，控件会设置它。
- 控件没有命令。如上所示，在 `ReadChanged` 和 `Dismissed` 中保存更改，并保持处理程序简短。

### 数据结构与更新

- **稳定的 `Id`**：行通过 `Id` 匹配。用带有相同 id 的新 `NotificationItem` 对象刷新列表时，行保持原位，展开的行也保持展开。没有 id 的项通过对象引用匹配。
- **用 `Tag` 保存你的记录**：`Tag` 可以保存任何值，例如你的存储中的记录键。事件会提供 `Item`，因此读取 `e.Item.Tag`，而不是按标题查找。
- **`Time` 是显示文本**：控件按原样显示字符串，例如 "2m"。它不会随时间推移更新该值，因此请在视图模型中格式化它，并在重新加载时刷新。
- **实时更新**：控件跟随集合，以及每一项的 `Title`、`Description`、`IsRead` 和其他属性（`Tag` 除外）。新增更新或已读状态变化会立即刷新铃铛和面板。
- **批量操作作为一个批次执行**："Mark all read" 和 "Clear read" 只会刷新一次计数和列表，然后为每一项各触发一次事件。
- **保持列表简短**：面板最高 590 px，而 Arc 设计没有列表滚动条。数据存储只保留最近的更新，其余内容引导用户前往历史页面。
- **数字格式**：角标使用当前文化。请使用应用的文化（例如 `CultureInfo.CurrentCulture`）格式化项文本中的数量和日期。

### 未读视图与空状态

面板列出所有更新，或只列出未读更新。`View` 是双向的。列表为空时，面板会说明这一点：All 视图中为 "All clear"，Unread 视图中为 "Nothing unread"。存在已读更新时，会有一个链接返回 All 视图。

<Demo name="UnreadView" />

### 在应用头部中

面板在铃铛下方 12 像素处打开，并与窗口边缘保持 12 像素的距离，因此铃铛可以位于头部的边缘。未读角标会超出铃铛右上角 5 像素，因此在会裁剪内容的容器中，铃铛周围要留出这么多空间。在宽 380 像素或更窄的窗口中，面板切换为窄版布局：更紧凑的头部与行内边距、较小的标题，且 "Mark all read" 只显示图标。

<Demo name="AppHeader" />

### 焦点与关闭

- **打开**时焦点移到关闭按钮，因此从面板按下第一次 Tab 会到达 All，之后列表跟随。
- **按 Escape 或点击关闭按钮**会关闭面板，并把焦点返回铃铛。
- **在面板外按下**会关闭它。只有焦点原本在面板内时，焦点才返回铃铛。
- **铃铛**切换面板：再次点击即关闭。
- **离开窗口**：面板打开时若铃铛离开窗口，面板会跳过退出动画直接关闭；若铃铛返回且 `IsOpen` 仍为 true，则重新打开。
- **在 Unread 视图中关闭或标记已读**时，焦点依次移到下一行、上一行，然后是 Unread 按钮。

### 文案

`Label` 为面板和铃铛命名。`Labels` 保存其余所有文字，并带有英文默认值，类型为 `NotificationCenterLabels` 对象（见 API 参考）。赋值一个新实例即可修改它们。在 `ReadCount` 中，`{0}` 是滚动的数字。带数量的文字（例如标题下方的摘要）是可以在代码中设置的函数，因此应用可以套用自己的复数规则。

<Demo name="CustomWording" />

```csharp
center.Labels = new NotificationCenterLabels
{
    Summary = unread => unread > 0 ? $"{unread} 条新通知" : "没有新通知",
    Trigger = (label, unread) => $"{label}，{unread} 条新通知"
};
```

### 从代码打开

`IsOpen` 是双向的。`Open()` 和 `Close()` 作用相同。`MarkAllRead()` 和 `ClearRead()` 执行批量操作，并触发与按钮相同的事件。在铃铛显示到窗口中之前设置 `IsOpen`，会在窗口显示后打开面板。`Open()` 会把焦点移入面板，因此应在用户预期时调用，而不是在每次后台更新时调用。

```csharp
center.Open();
center.MarkAllRead();
```

### 示例：支付后台头部中的活动铃铛

一个支付团队的管理控制台。头部包含产品名称、活动铃铛和用户头像。铃铛显示两条未读更新。第一条有发出者，因此显示此人的头像，标题中也写出了其姓名。第二条是使用 `Success` 语气的系统更新。第三条已读，因此面板中会显示它，但没有未读圆点。头部下方的页面使用相同的表面色与字体令牌，因此头部看起来就是产品的一部分。

![管理后台头部中的活动铃铛，显示两条未读更新，下方为页面标题](images/ActivityHeader.png)

```xml
<StackPanel Width="680" Spacing="16">
    <Border CornerRadius="20"
            Padding="16,12"
            BorderThickness="1"
            BorderBrush="{arc:ArcDesignTokenResource Border}"
            Background="{arc:ArcDesignTokenResource Surface}">
        <DockPanel>
            <StackPanel DockPanel.Dock="Right" Orientation="Horizontal" Spacing="12">
                <arc:NotificationCenter Label="Activity">
                    <arc:NotificationItem Id="assigned"
                                          Title="Priya Shah assigned you Fix checkout totals"
                                          Time="9m"
                                          ActorName="Priya Shah" />
                    <arc:NotificationItem Id="incident"
                                          Title="Incident resolved"
                                          Description="Payments API latency is back to normal."
                                          Time="40m"
                                          Tone="Success" />
                    <arc:NotificationItem Id="sso"
                                          Title="SSO certificate expires in 7 days"
                                          Description="Upload a new certificate in Security settings."
                                          Time="2h"
                                          Tone="Warning"
                                          IsRead="True" />
                </arc:NotificationCenter>
                <arc:Avatar DisplayName="Jordan Lee" VerticalAlignment="Center" />
            </StackPanel>
            <TextBlock Text="Northline"
                       VerticalAlignment="Center"
                       FontSize="{arc:ArcDesignTokenResource TypeLG}"
                       FontWeight="{arc:ArcDesignTokenResource WeightMedium}"
                       Foreground="{arc:ArcDesignTokenResource Foreground}" />
        </DockPanel>
    </Border>
    <StackPanel Spacing="6">
        <TextBlock Text="Payments overview"
                   FontSize="{arc:ArcDesignTokenResource TypeXL}"
                   FontWeight="{arc:ArcDesignTokenResource WeightMedium}"
                   Foreground="{arc:ArcDesignTokenResource Foreground}" />
        <TextBlock Text="Checkout conversion and open incidents for the payments team."
                   FontSize="{arc:ArcDesignTokenResource TypeSM}"
                   Foreground="{arc:ArcDesignTokenResource TextSecondary}" />
    </StackPanel>
</StackPanel>
```

### 文案规范

- **标题**：一行，点明对象与结果，例如 "Invoice inv_2026_0412 was paid"。标题会以省略号截断，因此把关键词放在前面。来自人的更新要写出发出者的姓名。
- **描述**：一句话，包含用户做决定所需的细节：金额、版本或数量。它在标题下方预览，展开行时完整显示。
- **时间**：简短的标签，例如 "2m"、"40m"、"昨天" 或 "周一"。所有项使用相同的单位。
- **标签**：句子式大小写，操作标签使用动词加对象（"Mark read"、"Clear read"）。
- **Dismiss** 只会从此列表中移除更新。当删除操作同时会删除数据时，要在产品文案中说明。

| 不要写 | 改为 |
|---|---|
| 新通知！ | Priya Shah 把 "Fix checkout totals" 指派给了你 |
| 错误 | 发票 inv_2026_0412 支付失败 |
| 点击这里 | 查看更新详情 |
| 删除 | Dismiss |
| 发生了一些事情 | 生产环境部署已完成 |

### 最佳实践

**推荐**

- 为每一项设置稳定的 `Id`，刷新列表时保持同一个项对象。行通过 id 匹配，因此展开的行和键盘位置在刷新后得以保留。
- 把 `ReadChanged` 和 `Dismissed` 保存到你的存储中。控件只改变内存中的状态，因此若不处理这些事件，重新加载时会显示旧的已读状态。
- 把记录键保存在 `Tag` 中，使事件无需按标题查找即可提供记录。
- 需要决定的更新用 `Warning`，已完成的工作用 `Success`。语气图标本身承载含义，因此让每种语气只对应一类更新，能让列表更易读。
- 将 `Label` 设为产品中对列表的称呼（"Activity"、"Inbox"）；它为铃铛和面板命名。

**避免**

- 为会改变数据的工作放置操作按钮，例如批准请求。面板只提供 "Mark read"、"Mark unread" 和 "Dismiss"；应在你自己的页面中链接到该记录。
- 每次更新都调用 `Open()`。它会把焦点移入面板，可能打断用户在其他字段中的输入。
- 代替用户关闭未读更新。关闭会把更新从列表中移除，用户会失去待处理的项。
- 仅用角标作为故障或支付失败的唯一提示。应使用 `Alert` 在页面上显示这些信息，让用户能在那里采取行动。
- 包含多句话的长标题。它们会被截断为一行，其余内容只在详情中可见。

### 常见问题

**为什么 Dismiss 会从我的列表中移除该项？** 控件在触发 `Dismissed` 之前，会从 `Items` 中移除该项；当 `ItemsSource` 是可写的 `IList` 或 `ICollection` 时，则从中移除。只读数据源会保留该项，控件则将其隐藏。若希望删除是永久的，请在 `Dismissed` 处理程序中删除记录。

**为什么角标计数与我的存储不一致？** `UnreadCount` 统计控件所显示的项：即 `ItemsSource`（或 `Items`）中的项，减去控件从只读数据源中隐藏的项。请从控件读取计数，而不是从你自己的列表读取，这样铃铛与面板才会一致。

**点击一行会打开相关页面吗？** 不会。一行只会展开或收起其详情，面板没有链接操作。请使用项的 `Tag` 值，从你自己的页面引导用户前往该记录。

**如何让发出者的照片显示出来？** 将 `ActorSource` 设为 `ImageSource`；在 XAML 中使用 URL 即可。图片加载期间或加载失败时，显示的是姓名首字母。

**可以更改面板的大小或位置吗？** 没有公开属性可以设置它们。面板宽 424 px（或窗口宽度减 24 px），最高 590 px，位于铃铛下方 12 px 处。请把铃铛放在面板可以向下打开的位置。

## API 参考

<!-- api: NotificationCenter -->

<!-- api: NotificationItem -->

### NotificationCenterLabels

`NotificationCenterLabels` 保存控件显示或朗读的所有文字（`Label` 除外），每项都带有 Arc 的英文默认值。将新实例赋给 `NotificationCenter.Labels`。

| 属性 | 默认值 | 显示位置 |
|---|---|---|
| `Trigger`（标签与未读数的函数） | "Notifications, 3 unread" | 铃铛的无障碍名称 |
| `Summary`（未读数的函数） | "3 updates waiting for you"、"You’re all caught up" | 标题下方的一行 |
| `Close` | "Close notifications" | 关闭按钮的无障碍名称 |
| `ViewSwitch` | "Show notifications" | All / Unread 切换的无障碍名称 |
| `All` / `Unread` | "All" / "Unread" | 视图按钮 |
| `AllList` / `UnreadList` | "All notifications" / "Unread notifications" | 列表的无障碍名称 |
| `MarkAllRead` | "Mark all read" | 工具栏中的批量操作 |
| `UnreadState` | "unread" | 添加到未读行的无障碍名称中 |
| `ShowDetails` / `HideDetails` | "Show details" / "Hide details" | 行的无障碍名称末尾 |
| `FromActor`（姓名的函数） | "From Mia Chen" | 没有描述的人发出的更新的预览 |
| `ViewDetails` | "View update details" | 没有描述的系统更新的预览 |
| `SharedUpdate`（姓名的函数） | "Mia Chen shared an update with you." | 没有描述的人发出的更新的详情 |
| `ReadyToReview` | "This update is ready to review." | 没有描述的系统更新的详情 |
| `MarkRead` / `MarkUnread` | "Mark read" / "Mark unread" | 行上的已读状态操作 |
| `Dismiss` | "Dismiss" | 移除更新的行操作 |
| `EmptyTitle` / `EmptyDescription` | "All clear" / "New updates will appear here." | 空的 All 视图 |
| `UnreadEmptyTitle` / `UnreadEmptyDescription` | "Nothing unread" / "You’ve seen every update." | 空的 Unread 视图 |
| `ViewAll` | "View all updates" | 空的 Unread 视图中返回 All 视图的链接 |
| `ReadCount` | "{0} read" | 页脚计数；`{0}` 为滚动的数字 |
| `ClearRead` | "Clear read" | 页脚中移除已读更新的操作 |

## 无障碍

- 铃铛是一个按钮，名称由 `Label` 和未读数量组成，例如 "Notifications, 3 unread"。它会报告面板是否展开。
- 打开面板时，焦点移到其关闭按钮。Tab 依次经过关闭、All、Unread、"Mark all read"、各行、已展开行的按钮和 "Clear read"，并在面板内部循环。
- Escape 和关闭按钮会关闭面板，并把焦点返回铃铛。
- All 与 Unread 是切换按钮。每一行是一个按钮，其名称由标题与状态组成，例如 "Deploy finished, unread. Show details"，并报告它是否展开。
- 在 Unread 视图中把某行标记为已读，或关闭某行之后，焦点依次移到下一行、上一行，然后是 Unread 按钮。在 "Mark all read" 之后，焦点返回当前视图按钮。
- 标题下方的摘要是礼貌的实时区域，因此新的计数会被朗读出来。
- `Label` 同时为铃铛和面板标题命名，因此请选择一个单独使用也有意义的词，例如 "Activity" 或 "Inbox"。

### 键盘

| 按键 | 操作 |
|---|---|
| 铃铛上的 `Enter` 或 `Space` | 打开面板，并把焦点移到其关闭按钮 |
| 面板打开时，铃铛上的 `Enter` 或 `Space` | 关闭面板，并把焦点返回铃铛 |
| `Escape` | 关闭面板，并把焦点返回铃铛 |
| `Tab` / `Shift+Tab` | 依次经过关闭、All、Unread、"Mark all read"、各行、已展开行的按钮和 "Clear read" |
| All 或 Unread 上的 `Enter` 或 `Space` | 切换视图；已展开的行会收起 |
| 行标题上的 `Enter` 或 `Space` | 显示或隐藏该行的详情；同一时间只保持一行展开 |
| "Mark read"、"Mark unread" 或 "Dismiss" 上的 `Enter` 或 `Space` | 更改该行，然后按无障碍一节所述移动焦点 |
| "Mark all read" 上的 `Enter` 或 `Space` | 将每条未读更新标记为已读；焦点返回当前视图按钮 |
| "Clear read" 上的 `Enter` 或 `Space` | 关闭每条已读更新；焦点移到 All 按钮 |

## 动效

- 面板从铃铛处生长出来：起始时朝铃铛方向偏移 6 px、缩放为 0.97，并在 240 ms 内淡入。关闭时在 120 ms 内以一半的距离和 0.98 的缩放离开。
- 面板打开时，铃铛会倾斜。角标弹入与弹出，其数字向上或向下滚动。
- 面板打开或视图添加行时，行从下方 4 px 处落定，每行间隔 35 ms。离开的行淡出，并在平滑弹簧上收缩高度。批量操作之后，它们依次离开，与第一行的间隔最多 200 ms。
- 计数滚动，摘要上移变为新的措辞，All 与 Unread 的高亮在按钮之间滑动。详情、页脚和空状态以相同的弹簧展开与收起高度。
- 减弱动效只保留 120 ms 的短淡入淡出，没有位移、缩放或模糊。面板立即显示和隐藏。关闭动效时，所有变化立即完成。

| 变化 | 完整 | 减弱 | 关闭 |
|---|---|---|---|
| 面板打开 | 从铃铛处生长出来，6 px 与 0.97 缩放，240 ms | 立即显示 | 立即完成 |
| 面板关闭 | 120 ms 内以一半距离离开 | 立即隐藏 | 立即完成 |
| 行进入 | 上升 4 px 并淡入，每行间隔 35 ms | 短淡入，无位移 | 立即完成 |
| 批量操作后行离开 | 高度在平滑弹簧上收缩，级联最多 200 ms | 短淡出，无收缩 | 立即完成 |
| 打开时的铃铛 | 倾斜，面板关闭后复位 | 保持直立 | 立即完成 |
| 未读角标 | 弹入与弹出；数字滚动 | 短淡入 | 立即完成 |
| 视图高亮 | 在 All 与 Unread 之间滑动 | 短淡入 | 立即完成 |

## 相关组件

- `Toast` 用于自行消失的简短消息。
- `Popover` 用于锚定在按钮上的其他内容。
- `Badge` 用于其他控件上的计数或状态。

| 组件 | 适用场景 |
|---|---|
| `Toast` / `ToastStack` | 简短结果只需出现一次，无需操作即可消失 |
| `Alert` | 当前页面的问题必须保持可见，直到修复 |
| `Popover` | 其他内容需要锚定在按钮上，例如筛选表单 |
| `Badge` | 计数或状态附着在其他控件上，且不需要更新列表 |
| `Timeline` | 单条记录的事件需要按顺序阅读 |
| `ChatThread` | 用户在对话中相互回复 |
