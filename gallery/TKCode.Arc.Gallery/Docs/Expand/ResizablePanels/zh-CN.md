---
title: 可调整大小的面板
summary: 拖动窗格之间的分隔条，在各窗格之间调配空间。
source-hash: d4be2dc79fa5
---

<Demo name="Default" />

## 何时使用

`ResizablePanels` 将一个区域分割为并排的窗格，并由用户决定每个窗格占用多少空间。它适用于应用程序的工作区：页头下方的主体区域，其中列表、详情视图和次要窗格需要共享同一窗口。该组件需要父级提供确定的高度，因此应放在外壳的内容区域中，而不是放在自动调整大小的行里。

- **邮件与消息客户端**：文件夹列表、邮件列表和阅读窗格。阅读长线程时，用户会拉宽阅读窗格；需要空间时，则折叠文件夹。
- **管理后台中的工单与订单分诊**：左侧是队列，右侧是选中的工单或订单。客服人员缩窄队列以便密集阅读，整理积压工单时再拉宽它。
- **分析工具中的查询与代码编辑器**：架构浏览器、编辑器和结果窗格。编写 SQL 或 DAX 查询时，编辑器需要大部分宽度。
- **带侧边检查器的仪表板**：图表窗格与筛选或详情检查器并排。检查器不需要时会折叠，使图表获得全宽。
- **用户调整一次后即保留的布局**：尺寸通过 `Sizes` 保存，并在下次会话中恢复，因此团队偏好的分割在重启后依然保留。

## 何时不用

- **覆盖页面的面板**：用 `Drawer` 作为从内容上方滑出、必须在用户返回页面前关闭的窗格。
- **同一页面的堆叠分节**：`ResizablePanels` 只进行水平分割。上下排列、依次展开收起的分节用 `Accordion`。
- **每次只显示一个窗格**：当用户在多个视图间切换，且从不需要同时看到两个时，用 `Tabs`。
- **窄屏或手机布局**：窗格永远不会堆叠。在窄宽度下使用 `Tabs`，或让页面改变结构。
- **无人调整的固定分割**：使用普通的 Avalonia `Grid` 并设置 star 尺寸的列更简单，也没有需要操作的分隔条。
- **内容比窗格更长**：保持分割，并在窗格内放置 `ScrollArea`。该组件不会滚动其窗格。

## 用法

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:ResizablePanels Label="邮件布局" Height="320">
        <arc:ResizablePanel Id="folders"
                            Label="文件夹"
                            DefaultSize="22"
                            MinSize="140"
                            IsCollapsible="True">
            <TextBlock Text="收件箱、草稿、已发送、归档" />
        </arc:ResizablePanel>
        <arc:ResizablePanel Id="messages"
                            Label="邮件列表"
                            DefaultSize="33"
                            MinSize="200">
            <TextBlock Text="42 个会话" />
        </arc:ResizablePanel>
        <arc:ResizablePanel Id="reading"
                            Label="阅读窗格"
                            DefaultSize="45"
                            MinSize="220"
                            IsCollapsible="True">
            <TextBlock Text="季度复盘" />
        </arc:ResizablePanel>
    </arc:ResizablePanels>
</UserControl>
```

`ResizablePanels` 派生自 Avalonia 的 `Control`，`ResizablePanel` 派生自 `ContentControl`。组件将其 `Panels` 集合作为内容，因此窗格直接写在其中即可。组件自行布局各窗格：每个窗格的宽度由其占比和限制决定，而不是你为它设置的 `Width`。窗格之间的分隔条由组件创建，并且可以获得焦点，因此无需额外标记即可用键盘操作。

`Label` 为辅助技术提供组件的名称。每个窗格的 `Label` 为其后的分隔条命名。

### 结构

1. **组件**（`ResizablePanels`）：裁剪到自身边界内，从左到右布局。除分隔条和恢复标签外，它没有自身的视觉元素。组件内的文字使用正文字体，字号为 `TypeSM`，颜色为前景色，除非某个窗格另有设置。
2. **窗格**（`ResizablePanel`）：内容拉伸以填满窗格。当宽度低于窗格的有效最小值时，内容保持该最小宽度，并锚定在移动的边缘上，因此关闭的窗格会滑出，而不是被挤压。
3. **分隔线**：1 px 的线，使用边框颜色。指针悬停、正在拖动或分隔条获得键盘焦点时，变为强边框颜色。
4. **抓取区域**：线两侧各 6 px，共 13 px。在此区域内按下即开始拖动。
5. **抓手**：以线为中心的 4 × 32 px 圆角胶囊。悬停、聚焦或拖动分隔条时出现，使用强边框颜色；拖动时变为前景色。
6. **恢复标签**：距离分隔线 5 px、18 × 40 px 的胶囊，位于被隐藏窗格返回的一侧。相邻窗格被隐藏时出现。它使用表面填充、悬停或聚焦时加深的边框描边、次要文字颜色的箭头以及静止时的阴影。隐藏分隔条的命中区域会延伸 30 px 进入可见窗格，使恢复标签更容易按下。
7. **光标**：在展开的分隔条上为水平调整大小光标，在隐藏的分隔条上为手型光标。

### 窗格尺寸

每个窗格都有 `DefaultSize`，它是占比而不是宽度。各占比是相对的：`22`、`33` 和 `45` 与 `2.2`、`3.3`、`4.5` 产生相同的布局，它们无需加起来等于 100。组件按占比分配空间，然后让每个窗格遵守其限制。若某个窗格会突破 `MinSize` 或 `MaxSize`，它会被固定在该限制处，剩余空间再在其他窗格之间重新分配。

| 属性 | 默认值 | 含义 |
|---|---|---|
| `DefaultSize` | `1` | 初始占比。也是调用 `Reset()` 时组件返回的尺寸。 |
| `MinSize` | `80` px | 窗格开始产生阻力的宽度。其内容永远不会在此宽度以下重排。 |
| `MaxSize` | 无限制 | 以 px 为单位的宽度上限。值为零或小于零表示无限制。 |
| `IsCollapsible` | `False` | 允许通过分隔条、键盘或甩动将窗格隐藏。 |
| `IsCollapsed` | `False` | 使窗格以隐藏状态开始。仅在 `IsCollapsible="True"` 时生效。双向绑定。 |
| `Id` | 无 | 窗格的稳定键。当 `Label` 为空时用于为分隔条命名。 |

当组件宽度小于所有窗格最小值之和时，最小值会按比例缩小，使所有窗格保持可见。

### 默认折叠

在可折叠的窗格上设置 `IsCollapsed="True"`，使其以隐藏状态开始。此时其分隔条会显示一个恢复标签，点击即可将窗格恢复；在窗格恢复之前，它会从命中测试与 Tab 顺序中移除。

<Demo name="CollapsedAndPersisted" />

从代码折叠与拖动或 Enter 键使用相同的路径。将 `IsCollapsed` 设为 `True` 会通过分隔条隐藏窗格，设为 `False` 则恢复它。对于不可折叠的窗格，两者都会被忽略。

### 限制

`MinSize` 是窗格开始产生阻力的像素宽度。`MaxSize` 限制宽度上限，默认无限制。超过限制后，分隔条会停在限制处，并产生最多 44 px 的橡皮筋回弹阻力，之后便不再移动，让用户知道已达到限制。

```xml
<arc:ResizablePanel Id="code" Label="代码" DefaultSize="50" MinSize="240" MaxSize="800" />
```

选择 `MinSize` 时，应取窗格内容仍能清晰阅读的最窄宽度。邮件列表需要容纳发件人和主题，文件夹列表需要容纳最长的标签，阅读窗格需要容纳一行正文。默认的 80 px 只适用于只放图标或短值的窗格。

### 绑定到视图模型

组件的 `Sizes` 属性即是布局，因此布局由视图模型持有。`Sizes` 与每个窗格的 `IsCollapsed` 默认为双向绑定，这意味着视图模型会收到每一次拖动或键盘操作结束后的尺寸变化，以及每一次折叠，并可以设置它们来恢复布局。

```csharp
public interface IUserSettings
{
    IReadOnlyList<double>? MailPaneSizes { get; set; }
    bool MailFoldersCollapsed { get; set; }
}

public partial class MailViewModel : ObservableObject
{
    private readonly IUserSettings _settings;

    public MailViewModel(IUserSettings settings)
    {
        _settings = settings;
        _paneSizes = settings.MailPaneSizes;
        _isFoldersCollapsed = settings.MailFoldersCollapsed;
    }

    [ObservableProperty]
    private IReadOnlyList<double>? _paneSizes;

    [ObservableProperty]
    private bool _isFoldersCollapsed;

    partial void OnPaneSizesChanged(IReadOnlyList<double>? value) => _settings.MailPaneSizes = value?.ToArray();

    partial void OnIsFoldersCollapsedChanged(bool value) => _settings.MailFoldersCollapsed = value;
}
```

```xml
<arc:ResizablePanels Label="邮件布局" Sizes="{Binding PaneSizes}" Height="480">
    <arc:ResizablePanel Id="folders"
                        Label="文件夹"
                        DefaultSize="22"
                        MinSize="140"
                        IsCollapsible="True"
                        IsCollapsed="{Binding IsFoldersCollapsed}">
        <!-- 文件夹列表 -->
    </arc:ResizablePanel>
    <arc:ResizablePanel Id="messages" Label="邮件列表" DefaultSize="33" MinSize="200">
        <!-- 邮件列表 -->
    </arc:ResizablePanel>
    <arc:ResizablePanel Id="reading" Label="阅读窗格" DefaultSize="45" MinSize="220">
        <!-- 邮件详情 -->
    </arc:ResizablePanel>
</arc:ResizablePanels>
```

- `Sizes` 按顺序为每个窗格保存一个值，单位为可用空间的百分比，各值之和为 100。隐藏的窗格值为 0。视图模型会在尺寸变化稳定后收到新值（`LayoutChanged` 与之同时触发），而不是在每次指针移动时收到，因此在属性变更处理程序中保存时，每次操作只会写入存储一次。
- 将布局与其所属的窗格集合一同保存。组件在整个生命周期内应使用相同的窗格，因此当窗格列表变化时应丢弃已保存的数组，并使用默认值。
- `Reset()` 是方法而不是属性。请在视图的代码后置文件中，从按钮的 Click 处理程序调用它，因为它会将每个窗格恢复为默认占比和默认折叠状态。

### 拖动、甩动与双击

拖动分隔条时，窗格会与指针一一对应地移动。当拖动越过限制时，以下规则决定用户所看到的效果：

- **限制**：超过窗格的最小值或最大值后，分隔条会产生阻力；若指针在橡皮筋区域内释放，分隔条会停在限制处。
- **拖动折叠**：可折叠窗格的宽度低于其最小值减去“最小值的一半”与 40 px 中较大者时，会自动收起。对于默认的 80 px 最小值，该阈值为 40 px；对于 200 px 的最小值，则为 100 px。
- **甩动**：释放时，组件会将指针的速度沿运动方向投射一小段距离。朝边缘甩动会折叠窗格，朝外甩动则恢复窗格。没有甩动时，分隔条停留在释放的位置。
- **释放动效**：窗格会以保留指针速度的弹簧稳定下来，因此快速甩动会自然地延续到稳定过程中。
- **按下恢复**：按下隐藏窗格的分隔条而不移动，即可恢复该窗格。
- **双击**：双击任一分隔条会调用 `Reset()`，使整个组件恢复默认值。

这些规则是针对鼠标与触控板调校的。触摸用户可以拖动分隔条，但甩动阈值是根据指针采样测量的，因此请在你支持的设备上测试折叠手势。

### 窄窗口

窗格永远不会堆叠。窗口变窄时，最小值会按比例缩小；低于其最小值的窗格会随宽度淡出，其内容保持在最小宽度并随之滑出。手机宽度的布局需要不同的结构：在该宽度下切换为 `Tabs`，或改变页面布局。

### 示例：邮件工作区

以阅读为中心的邮件视图。文件夹列表默认隐藏，使邮件列表与阅读窗格获得全部窗口空间；左边缘的恢复标签展示了如何把文件夹重新显示出来。邮件列表的每一行显示发件人和主题，阅读窗格则提供所打开邮件的操作。标签与可见标题保持一致，使分隔条的名称与用户所见相符。

![邮件工作区：文件夹窗格折叠为恢复标签，并显示邮件列表与阅读窗格](images/MailWorkspace.png)

```xml
<Border Width="720"
        Height="340"
        BorderThickness="1"
        CornerRadius="18"
        ClipToBounds="True"
        BorderBrush="{arc:ArcDesignTokenResource Border}">
    <arc:ResizablePanels Label="Mail workspace">
        <arc:ResizablePanel Id="folders"
                            Label="Folders"
                            DefaultSize="22"
                            MinSize="140"
                            IsCollapsible="True"
                            IsCollapsed="True">
            <Border Padding="16" Background="{arc:ArcDesignTokenResource SurfaceMuted}">
                <StackPanel Spacing="10">
                    <TextBlock Text="Folders"
                               FontSize="14"
                               FontWeight="Medium"
                               Foreground="{arc:ArcDesignTokenResource Foreground}" />
                    <TextBlock Text="Inbox  12"
                               FontSize="13"
                               Foreground="{arc:ArcDesignTokenResource TextSecondary}" />
                    <TextBlock Text="Drafts  2"
                               FontSize="13"
                               Foreground="{arc:ArcDesignTokenResource TextSecondary}" />
                </StackPanel>
            </Border>
        </arc:ResizablePanel>
        <arc:ResizablePanel Id="messages"
                            Label="Messages"
                            DefaultSize="33"
                            MinSize="200">
            <StackPanel Background="{arc:ArcDesignTokenResource Surface}">
                <Border Padding="36,12,16,12" BorderBrush="{arc:ArcDesignTokenResource Border}"
                        BorderThickness="0,0,0,1">
                    <StackPanel Spacing="4">
                        <TextBlock Text="Priya Raman  ·  Oct 19"
                                   FontSize="12"
                                   Foreground="{arc:ArcDesignTokenResource TextMuted}" />
                        <TextBlock Text="Invoice INV-2026-0412 is overdue"
                                   FontSize="14"
                                   FontWeight="Medium"
                                   Foreground="{arc:ArcDesignTokenResource Foreground}" />
                    </StackPanel>
                </Border>
                <Border Padding="36,12,16,12">
                    <StackPanel Spacing="4">
                        <TextBlock Text="Marco Lind  ·  Oct 18"
                                   FontSize="12"
                                   Foreground="{arc:ArcDesignTokenResource TextMuted}" />
                        <TextBlock Text="Q3 seat count for Northwind"
                                   FontSize="14"
                                   Foreground="{arc:ArcDesignTokenResource Foreground}" />
                    </StackPanel>
                </Border>
            </StackPanel>
        </arc:ResizablePanel>
        <arc:ResizablePanel Id="reading"
                            Label="Reading pane"
                            DefaultSize="45"
                            MinSize="300">
            <Border Padding="20" Background="{arc:ArcDesignTokenResource SurfaceMuted}">
                <StackPanel Spacing="12">
                    <TextBlock Text="Invoice INV-2026-0412 is overdue"
                               FontSize="16"
                               FontWeight="Medium"
                               TextWrapping="Wrap"
                               Foreground="{arc:ArcDesignTokenResource Foreground}" />
                    <TextBlock Text="From Priya Raman, Finance. Sent Oct 19, 2026."
                               FontSize="12"
                               TextWrapping="Wrap"
                               Foreground="{arc:ArcDesignTokenResource TextMuted}" />
                    <TextBlock Text="The March invoice is 14 days past due. Payment is due by Oct 31."
                               FontSize="13"
                               TextWrapping="Wrap"
                               Foreground="{arc:ArcDesignTokenResource TextSecondary}" />
                    <StackPanel Orientation="Horizontal" Spacing="8">
                        <arc:Button Content="Reply" Variant="Secondary" Size="Small" />
                        <arc:Button Content="Archive" Variant="Ghost" Size="Small" />
                    </StackPanel>
                </StackPanel>
            </Border>
        </arc:ResizablePanel>
    </arc:ResizablePanels>
</Border>
```

### 文案规范

- **窗格标签**：简短的名词短语，使用句子式大小写，与窗格可见的标题一致：“文件夹”“邮件列表”“阅读窗格”。该标签是分隔条对辅助技术的名称，因此“窗格 2”这类泛称会让屏幕阅读器用户无从下手。
- **组件标签**：说明布局是什么的名词短语：“邮件工作区”“查询编辑器”。命名布局本身，而不是其机制，使标签中不出现“可调整大小”。
- **恢复文案**：窗格被隐藏时，分隔条会播报“文件夹已隐藏，按 Enter 显示”。保持窗格标签简短，使这句话能完整读完。
- **窗格中的数字**：数量使用阿拉伯数字，例如“42 个会话”，使数值能一眼扫读。

| 不要写 | 改为 |
|---|---|
| Label="窗格 2" | Label="邮件列表" |
| Label="可调整大小的区域" | Label="邮件工作区" |
| Label="阅读"（仅用于层级很深、布局密集的场景） | Label="阅读窗格" |
| Label="右侧的内容" | Label="查询结果" |

### 最佳实践

**推荐**

- 为每个窗格提供稳定的 `Id` 与可见的 `Label`。`Id` 用于在已保存的设置中标识窗格，标签则是分隔条播报的内容。
- 将 `MinSize` 设为窗格内容仍能清晰阅读的最窄宽度；仅当更宽的窗格会浪费空间时才设置 `MaxSize`，例如代码窗格在 800 px 处停止。
- 让组件的高度是确定的，由页面外壳决定。放在自动调整大小行中的组件没有可分配的高度，将不显示任何内容。
- 在拖动结束后保存 `Sizes`，而不是在每次指针移动时保存；视图模型加载时从已保存的设置中设置它。

**避免**

- 折叠包含主要操作的窗格。无法找到恢复标签的用户将无法再次执行该操作，因此应将主要操作放在保持展开的窗格中。
- 为一个隐藏后会遗漏用户需要注意的信息的窗格设置 `IsCollapsible`，例如错误列表或待审批事项。
- 在组件使用期间更改窗格集合。组件是为固定的窗格集合构建的，来自另一集合的已保存布局无法描述它。
- 在窄窗格中嵌套组件，因为外层与内层的最小值会争夺同样少的像素。
- 期望垂直分割。组件只支持水平分割，因此堆叠的部分应放在组件之外构建。

### 常见问题

**为什么组件什么都不显示？** 它需要父级提供确定的高度。在 `StackPanel` 或自动调整大小的行中，组件没有可分配的高度，因此请设置 `Height`，或将其放入高度为 star 的 `Grid` 行中。

**为什么拖动分隔条时我的窗格没有折叠？** 只有 `IsCollapsible="True"` 的窗格才能折叠。拖动只有在超过折叠阈值后才会折叠窗格；较短的拖动会停在窗格的 `MinSize` 处并显示橡皮筋效果。

**如何从工具栏按钮隐藏窗格？** 将窗格的 `IsCollapsed` 绑定到视图模型属性，并修改该属性。设为 `True` 会通过分隔条折叠窗格，设为 `False` 则恢复。当用户用鼠标或键盘折叠窗格时，同一绑定也会收到变化。

**窗格隐藏时 `Sizes` 包含什么？** 隐藏窗格的值为 0，其余窗格分享 100%。

## API 参考

<!-- api: ResizablePanels -->

<!-- api: ResizablePanel -->

| 事件参数成员 | 类型 | 含义 |
|---|---|---|
| `ResizablePanelsLayoutChangedEventArgs.Sizes` | `IReadOnlyList<double>` | 每个窗格占组件的百分比份额；隐藏的窗格为 0。 |

## 无障碍

- `Label` 设置组件的无障碍名称，并以组的形式播报。
- 每个分隔条都是可聚焦的滑块。其值为窗格的占比（百分比），其范围为该窗格对能够达到的占比限制。隐藏的窗格会附带“按 Enter 显示”的提示。
- 分隔条使用其前一个窗格的标签；若标签为空，则使用该窗格的 `Id`。
- 折叠的窗格会从命中测试与 Tab 顺序中移除，并在无障碍树的控件视图中隐藏。
- 左右方向键以小步长调整分隔条大小；按住 Shift 加方向键则以大步长调整。
- Home 与 End 将分隔条移到最小与最大位置。Enter 隐藏或恢复可折叠的相邻窗格。
- 指针拖动过程中按 `Escape`，会将分隔条返回到拖动开始的位置。

### 键盘

每个分隔条都是一个 Tab 停靠点。以下按键在分隔条获得焦点且没有指针拖动进行时生效。

| 按键 | 操作 |
|---|---|
| `Tab` / `Shift+Tab` | 移到下一个或上一个分隔条或可聚焦控件 |
| `Left` / `Right` | 将分隔条移动 16 px。到达限制时，若该侧的窗格可折叠，则将其折叠 |
| `Shift` + `Left` / `Right` | 将分隔条移动 64 px |
| `Left` / `Right` 朝向隐藏的窗格 | 恢复被隐藏的窗格 |
| `Home` / `End` | 将分隔条移到最小或最大位置。相邻窗格被隐藏时忽略 |
| `Enter` | 恢复被隐藏的相邻窗格。否则，若分隔条前的窗格可折叠则折叠它，否则折叠其后的窗格 |
| `Escape`（拖动过程中） | 将分隔条返回到拖动开始的位置 |

## 动效

- 拖动直接跟随指针。超过窗格的最小值后，分隔条会以短促的橡皮筋效果产生阻力。
- 释放时，窗格会以保留指针速度的弹簧稳定下来。将可折叠的窗格拖到其最小值之外较远处，或朝边缘甩动，即可折叠它。
- 键盘调整大小使用灵敏的弹簧。
- 窗格折叠时，恢复标签弹出，其内容随窗格收缩而淡出。
- 减弱动效时，窗格直接移动到目标位置，恢复标签只淡入。

| 变化 | 完整 | 减弱 | 关闭 |
|---|---|---|---|
| 指针拖动 | 分隔条跟随指针，越过限制时出现橡皮筋效果 | 同上 | 同上 |
| 释放、拖动折叠或恢复 | 保留指针速度的平滑弹簧 | 窗格直接跳到目标位置 | 窗格直接跳到目标位置 |
| 键盘步进 | 灵敏的弹簧 | 窗格直接跳到目标位置 | 窗格直接跳到目标位置 |
| 双击重置 | 平滑弹簧回到默认占比 | 窗格直接跳到目标位置 | 窗格直接跳到目标位置 |
| 恢复标签出现 | 以灵敏的弹簧从 60% 缩放并淡入 | 仅淡入 | 立即完成 |
| 恢复标签消失 | 淡出并缩放到 60% | 仅淡出 | 立即完成 |
| 悬停或聚焦时抓手出现 | 以类似 CSS 的弹簧从 60% 缩放 | 立即完成 | 立即完成 |

## 相关组件

- `Drawer`：用于以覆盖层形式盖住页面的窗格。
- `Tabs`：用于每次只显示一个窗格的场景，或窄布局。
- `ScrollArea`：用于溢出窗格的内容。

| 组件 | 适用场景 |
|---|---|
| `Drawer` | 窗格从页面上方滑出，并在用户返回页面前关闭 |
| `Tabs` | 用户在多个视图间切换，从不需要同时看到两个，或窗口较窄 |
| `Accordion` | 分节纵向堆叠，一次展开一个或同时展开多个 |
| `ScrollArea` | 内容溢出窗格，需要自己的滚动条 |
