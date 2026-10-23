---
title: 按钮组
summary: 将相关操作合并到一个带细分隔线的整体中：悬停高亮在各段之间滑动，按下的段就地响应，附加菜单还可以收起整排按钮。
source-hash: a2540af58a90
---

<Demo name="DocumentActions" />

## 何时使用

`ButtonGroup` 把两到五个作用于同一对象的相关操作合并为一个带细分隔线的表面。适用于操作彼此相关且作用于同一事物的场景：文档页眉、缩放控件、格式工具栏、消息线程。一个高亮会随指针或键盘焦点滑动，让用户始终看到当前激活的是哪一段；可选的雪佛龙段则可以收纳较少用的操作，而不会让这一行变长。

- **文件查看器中的文档操作**：在一份幻灯片或合同的页眉上放置“分享”“复制”和“归档”，并用雪佛龙菜单收纳“置顶”“移动到”和“删除”。
- **报表或图表查看器中的缩放控件**：“缩小”、一个显示 100% 并可重置缩放的实时值，以及“放大”。数值位于两个按钮之间，用户在改变缩放的同时就能读到当前级别。
- **支持收件箱中的回复操作**：工单线程底部的“回复”“全部回复”和“转发”。三个相关操作放在一起，读起来就是一个决定。
- **内部知识库或 CMS 编辑器中的格式工具栏**：“加粗”“斜体”和“下划线”作为纯图标段放在文本块上方。熟悉的图标无需文字，工具提示会说明每一项。
- **垂直侧边工具栏中的插入块**：“图片”“表格”和“代码块”在编辑器旁边纵向堆叠，较少用的块放在末尾的菜单中。

## 何时不用

- **需要保持选中的选项，例如视图模式或时间范围**：使用 Toggles 中的 `SegmentedControl`。按钮组中的一段只执行一次操作，不会记住它曾被选中。
- **一个主要操作加上几个相近的替代操作**：使用 `SplitButton`，它让默认操作保持醒目，并把替代操作放在它的菜单中。
- **在视图或面板之间切换**：使用 Navigation 中的 `Tabs`，它会显示当前打开的是哪个面板。
- **很长的操作列表**：列表使用 Menus 中的 `DropdownMenu`，按钮组中只保留最常用的少数操作。十个段的一排会折叠或滚动，并失去意义。
- **托盘中独立的浮动按钮**：使用 `FloatingButtonGroup`，它会为每个按钮提供各自柔和的表面。

## 用法

每一段都是一个 `ButtonGroupItem`。为按钮组设置 `Label`，它就是该组的无障碍名称。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:ButtonGroup Label="文档操作">
        <arc:ButtonGroupItem Label="分享" />
        <arc:ButtonGroupItem Label="复制" />
        <arc:ButtonGroupItem Label="归档" />
    </arc:ButtonGroup>
</UserControl>
```

`ButtonGroup` 是一个 `ItemsControl`，因此段既可以内联声明，也可以由 `ItemsSource` 生成。每个 `ButtonGroupItem` 都派生自 Avalonia 的 `Button`：`Click`、`Command`、`CommandParameter` 和 `IsEnabled` 在段上的工作方式与在按钮上一样。按钮组本身负责表面、高亮、分隔线、雪佛龙菜单和行布局。

`ItemsSource` 中的普通值会变成以其文本为标签的段。这适合固定的一组选项，例如导出格式：

```xml
<arc:ButtonGroup Label="导出格式" ItemsSource="{Binding ExportFormats}" />
```

### 结构

1. **外框**：1 px 边框，圆角为控件圆角，填充为表面色。按钮组的 `Label` 用于为辅助技术命名，它不会被绘制出来。
2. **段**：段高 42 px，宽度至少为 42 px（位于边框内），内边距 14 px。图标与标签相距 8 px。`Small` 的按钮组使用 34 px 的段、12 px 的内边距和 6 px 的间距。
3. **分隔线**：相邻段之间 1 px 的细线，上下各内缩 11 px（`Small` 时为 9 px）。在 `Solid` 按钮组中，它们是页面颜色与前景色的混合。
4. **高亮**：表面弱化色的圆角矩形，位于段内侧 3 px 处，圆角为 14 px。它滑向指针或键盘焦点，并在段被按下时加深。
5. **雪佛龙段**：宽 38 px、高 42 px，内含 16 px 的雪佛龙图标。仅在设置了 `Menu` 时出现，菜单打开时会翻转。`Small` 的按钮组使用 30 × 34 px。
6. **状态区域**：一个隐藏的礼貌区域，用于播报标签的变化。它没有可见文字。

### 图标与纯图标段

将 `Icon` 设为 24 × 24 的 Lucide 几何图形，例如 `ArcIcons.Share2`。图标以 16 px 绘制在标签旁边。`IsIconOnly` 会隐藏标签，此时标签会成为工具提示和无障碍名称，因此纯图标段仍有清晰的名称。

```xml
<arc:ButtonGroup Label="缩放" Size="Small">
    <arc:ButtonGroupItem Label="缩小" IsIconOnly="True" />
    <arc:ButtonGroupItem Label="放大" IsIconOnly="True" />
</arc:ButtonGroup>
```

只对人人都认识的图形使用纯图标段，例如加粗、放大和撤销。对于图标含义不明确的操作，应保留可见标签，让段说明它的作用。

### 实时值与标签变化

`Value` 会在标签位置显示内容，例如缩放级别。文字值使用等宽数字，因此数值变化时数字不会移动。控件值按原样显示。`Label` 仍是无障碍名称。

按下之后标签也可以变化。在 `ReserveLabels` 中列出其他标签，段就会保持最宽标签的宽度，旁边的内容也不会移动。

<Demo name="LiveValue" />

```csharp
var zoomValue = new ButtonGroupItem { Label = "重置缩放", Value = "100%" };
zoomValue.Click += (_, _) => zoomValue.Value = "100%";

var share = new ButtonGroupItem { Label = "分享", ReserveLabels = ["已复制"] };
share.Click += async (_, _) =>
{
    share.Label = "已复制";
    await Task.Delay(1600);
    share.Label = "分享";
};
```

标签在按下后一秒内变化时，只会播报一次。回到原标签的安静变化不会被播报，因此用户只会听到一次“已复制”，不会再听到“分享”。

### 附加菜单

`Menu` 会在这一排的末尾添加一个雪佛龙段。它接受一个 `MenuSurface`，`MenuLabel` 为雪佛龙命名（默认为“更多操作”）。菜单会在雪佛龙下方 6 像素处打开，与按钮组的末端对齐，最小宽度为 208 px。`MenuItemClicked` 会报告所选的行。菜单行不会触发 `ActionInvoked`，因此要在 `MenuItemClicked` 中处理它们。

```xml
<arc:ButtonGroup Label="文档操作" MenuLabel="更多文档操作">
    <arc:ButtonGroupItem Label="分享" />
    <arc:ButtonGroupItem Label="归档" />
    <arc:ButtonGroup.Menu>
        <arc:MenuSurface>
            <arc:MenuEntry Header="置顶" />
            <arc:MenuEntry Header="删除" IsDestructive="True" HasSeparatorBefore="True" />
        </arc:MenuSurface>
    </arc:ButtonGroup.Menu>
</arc:ButtonGroup>
```

把破坏性操作放在雪佛龙后面的菜单中，并用 `HasSeparatorBefore` 与其他操作分开。一排用来删除东西的段很容易误触；菜单只增加一步，却不会隐藏该操作。

### 变体、方向与事件

`Variant` 默认为 `Outline`，即带边框的表面。`Variant="Solid"` 使用主要填充色来承载页面的主要操作，最好每个页面最多使用一次。`Orientation="Vertical"` 将段纵向堆叠；此时菜单会对齐到起始边。`Size` 为 `Small`（36 px 的按钮组，34 px 的段）或 `Medium`（44 px，默认）。`Large` 按 `Medium` 处理。

`ActionInvoked` 在段自身的点击处理程序之后运行，它在 `Item` 中报告该段，在 `Data` 中报告源对象。对于内联段，`Data` 是段本身；对于 `ItemsSource`，它是数据对象。

<Demo name="Solid" />

<Demo name="Vertical" />

```csharp
document.ActionInvoked += (_, e) => log.Text = $"操作：{e.Item.Label}";
```

### 标签折叠

当水平排列的一行放不进所给的宽度时，带图标的段会去掉标签、只保留图标。标签会移到工具提示和无障碍名称中。当宽度再次足以放下完整的一行时，标签会恢复；按钮组会记住触发折叠的宽度，因此不会在两种状态之间闪烁。没有图标的段保留标签。纵向按钮组不会折叠。

如果折叠后这一行仍然放不下，它会以隐藏的滚动条横向滚动。将 `IsLabelCollapseEnabled` 设为 `False`，即可保留标签并接受滚动。

<Demo name="CollapsingLabels" />

### 禁用

设置 `IsDisabled="True"` 的段仍可获得焦点，并向外报告自己不可用，因此键盘用户能找到它，并听到它不可用。激活操作会被吞掉，内容淡化到 40%。在禁用的段上悬停不会显示高亮。若要整体淡化按钮组并移除它的所有停靠点，请将按钮组的 `IsEnabled` 设为 `False`。

<Demo name="Disabled" />

### 绑定到视图模型

按钮组有命令，有时还有一个项目列表，因此工作由视图模型负责，按钮组负责显示。下例使用 CommunityToolkit.Mvvm 以及一个执行文档操作的应用服务。

```csharp
public interface IDocumentService
{
    Task OpenShareDialogAsync(string documentId);
    Task DuplicateAsync(string documentId);
    Task ArchiveAsync(string documentId);
}

public partial class DocumentViewModel : ObservableObject
{
    private readonly IDocumentService _documents;

    public DocumentViewModel(IDocumentService documents)
    {
        _documents = documents;
    }

    [ObservableProperty]
    private bool _isArchived;

    [RelayCommand]
    private Task ShareAsync() => _documents.OpenShareDialogAsync("doc_2026_0931");

    [RelayCommand]
    private Task DuplicateAsync() => _documents.DuplicateAsync("doc_2026_0931");

    [RelayCommand]
    private Task ArchiveAsync() => _documents.ArchiveAsync("doc_2026_0931");
}
```

```xml
<arc:ButtonGroup Label="文档操作" MenuLabel="更多文档操作">
    <arc:ButtonGroupItem Label="分享"
                         Icon="{x:Static arc:ArcIcons.Share2}"
                         Command="{Binding ShareCommand}" />
    <arc:ButtonGroupItem Label="复制"
                         Icon="{x:Static arc:ArcIcons.Copy}"
                         Command="{Binding DuplicateCommand}" />
    <arc:ButtonGroupItem Label="归档"
                         Icon="{x:Static arc:ArcIcons.Archive}"
                         Command="{Binding ArchiveCommand}"
                         IsDisabled="{Binding IsArchived}" />
</arc:ButtonGroup>
```

所有绑定都是单向的。`Label`、`Icon`、`Command` 和 `IsDisabled` 来自视图模型，按钮组不会写回任何值，因为按钮组没有可报告的选中项。`ItemsSource` 同样使用单向绑定。`ActionInvoked` 和 `MenuItemClicked` 是运行属于视图的代码的地方，例如记录日志或关闭弹出层。编译绑定需要在视图上设置 `x:DataType`；为简洁起见，示例中省略了它。

### 示例：文件查看器中的文档页眉

文档查看器中一份董事会幻灯片的页眉。标题与最后编辑的信息用于标识文档；右侧的按钮组放着所有者最常用的三个操作：分享、复制和归档。置顶与删除放在雪佛龙菜单中，使页眉保持简短，并让破坏性操作与常用操作之间只隔一步。分享同时有图标和标签，因为它是页面上最常用的操作；按钮组与标题区块居中对齐，让这些操作看起来是针对这份文档的，而不是针对页面的。

![文档页眉，包含标题和一个带雪佛龙菜单的三段式文档操作按钮组](images/DocumentHeader.png)

```xml
<Grid Width="680" ColumnDefinitions="*,Auto">
    <StackPanel Spacing="4" VerticalAlignment="Center">
        <TextBlock Text="Q2 board deck"
                   FontSize="20"
                   FontWeight="SemiBold"
                   Foreground="{arc:ArcDesignTokenResource Foreground}" />
        <TextBlock Text="Edited by Priya Nair, 12 minutes ago"
                   FontSize="13"
                   Foreground="{arc:ArcDesignTokenResource TextSecondary}" />
    </StackPanel>
    <arc:ButtonGroup Grid.Column="1" Label="Document actions" MenuLabel="More document actions">
        <arc:ButtonGroupItem Label="Share" Icon="{x:Static arc:ArcIcons.Share2}" />
        <arc:ButtonGroupItem Label="Duplicate" Icon="{x:Static arc:ArcIcons.Copy}" />
        <arc:ButtonGroupItem Label="Archive" Icon="{x:Static arc:ArcIcons.Archive}" />
        <arc:ButtonGroup.Menu>
            <arc:MenuSurface>
                <arc:MenuEntry Header="Pin" Icon="{x:Static arc:ArcIcons.Bookmark}" />
                <arc:MenuEntry Header="Delete"
                               Icon="{x:Static arc:ArcIcons.Trash2}"
                               IsDestructive="True"
                               HasSeparatorBefore="True" />
            </arc:MenuSurface>
        </arc:ButtonGroup.Menu>
    </arc:ButtonGroup>
</Grid>
```

### 文案规范

- **段标签**：表示操作的简短动词或名词：“分享”“复制”“放大”。最多两个词，句子式大小写。
- **组标签**：一个名词短语，说明该组作用的对象或所属的集合，例如“文档操作”或“缩放”。辅助技术会先读它，因此它应说明该组作用于什么。
- **菜单标签**：说明菜单的范围：“更多文档操作”，而不是“更多”。
- **结果词**：确认时使用的单个词，例如“已复制”。它会短暂出现，然后回到原标签。
- **实时值**：只写数值及其单位：“100%”，而不是“缩放为 100%”。

| 不要写 | 改为 |
|---|---|
| 点击分享 | 分享 |
| 操作 | 文档操作 |
| 更多 | 更多文档操作 |
| 缩放为 100% | 100% |
| 将链接复制到剪贴板 | 复制链接 |

### 最佳实践

**推荐**

- 每个按钮组保持两到五个段。段数更多时会折叠成图标或滚动，用户就无法一眼看到整组操作。
- 为每个按钮组设置 `Label`，因为它是无障碍名称，且不会绘制在屏幕上。
- 为每个纯图标段设置清晰的 `Label`，因为标签会成为它的工具提示和无障碍名称。
- 对标签会变化的段使用 `ReserveLabels`，使标签变化时按钮组不会调整大小。
- 把少用的和破坏性的操作移入雪佛龙菜单，常用操作保留在这一排中。

**避免**

- 把按钮组当作选择控件使用。被按下的段不会保持选中，用户无法分辨哪个视图是当前的。这种情况应使用 `SegmentedControl`。
- 每个页面有多个按钮组使用 `Solid`。主要填充色只用于页面的主要操作；多个实心按钮组会彼此争抢，也会与主要按钮争抢。
- 依赖标签折叠来呈现必须显示文字的布局。只要这一行比完整宽度窄，标签就会折叠，因此应为你需要的标签预留足够的容器宽度。
- 在一排常用操作旁边放置破坏性操作。一次误触就会删除数据。
- 在窄列中放置带长标签的纵向按钮组。纵向按钮组永不折叠，因此标签必须按原样放进该列。

### 常见问题

**为什么我的标签消失了？** 这一行比它的完整宽度窄，因此带图标的段已折叠为图标。请增加容器宽度、缩短标签，或将 `IsLabelCollapseEnabled` 设为 `False` 以保留标签，并让这一行滚动。没有图标的段永远不会折叠。

**如何在段上显示“已复制”这样的结果，同时不让这一行移动？** 在点击处理程序中更改 `Label`，并在 `ReserveLabels` 中列出结果词。段会保持最宽标签的宽度，且变化只会播报一次。

**菜单行会触发 `ActionInvoked` 吗？** 不会。`ActionInvoked` 只为段而触发。请在 `MenuItemClicked` 中处理雪佛龙菜单的各行，并用 `MenuEntryClickedEventArgs.Header` 来区分它们。

**段能打开链接而不是执行代码吗？** 可以。设置 `NavigateUri`，段就会通过平台的启动器打开该地址，并向辅助技术报告自己是一个超链接。

**如何禁用其中一个操作而保留其他操作？** 在该段上设置 `IsDisabled`。被禁用的段仍可获得焦点并淡化，因此用户仍能找到它，并了解它不可用。只有当所有操作都必须停止时，才在按钮组上设置 `IsEnabled`。

## API 参考

<!-- api: ButtonGroup -->

本页还用到的配套类型：

| `ButtonGroupItem` 属性 | 含义 |
|---|---|
| `Label` | 可见文字和无障碍名称。段为纯图标或被折叠时，它会成为工具提示。 |
| `Icon` | 24 × 24 的 Lucide 几何图形，以 16 px 绘制。 |
| `IsIconOnly` | 隐藏标签；标签会成为名称和工具提示。 |
| `Value` | 在标签位置显示的内容，例如缩放级别。 |
| `ReserveLabels` | 段可以显示的其他标签；它会保持最宽标签的宽度。 |
| `NavigateUri` | 通过平台启动器打开该地址，并将段报告为超链接。 |
| `IsDisabled` | 不可用但可获得焦点：激活会被吞掉，内容淡化。 |

## 无障碍

- 按钮组具有 Group 角色，并由 `Label` 命名。
- 每个段都是 Tab 停靠点，包括被禁用的段。方向键沿方向在各段之间移动，并在两端循环。Home 与 End 移到第一个和最后一个段。
- Enter 或空格键激活一个段。在菜单段上，Enter、空格键或 ArrowDown 会打开菜单。
- 纯图标段和被折叠的段以其标签作为名称和工具提示，它们的图标则被隐藏。
- 按下后不久发生变化的标签只会播报一次。状态从不只靠颜色来表示。
- 被禁用的段会向外报告自己不可用，因此用户会听到它的存在，但无法使用它。

### 键盘

| 按键 | 操作 |
|---|---|
| `Tab` / `Shift+Tab` | 将焦点移入或移出按钮组。每个段和雪佛龙都是停靠点。 |
| `Right` / `Left`（纵向按钮组中为 `Down` / `Up`） | 移到下一个或上一个段，在两端循环。在从右到左的布局中会镜像。 |
| `Home` / `End` | 移到第一个或最后一个段。 |
| `Enter` / `Space` | 激活获得焦点的段。在雪佛龙上则打开菜单。 |
| 雪佛龙上的 `ArrowDown` | 打开菜单，并高亮其第一行。 |
| 菜单中的 `Up` / `Down` | 在各行之间移动，在两端循环，并跳过被禁用的行。 |
| 菜单中的 `Escape` | 关闭菜单，并将焦点返回到雪佛龙。 |

## 动效

- 一个高亮会滑向指针或键盘焦点。它经过时，旁边的分隔线会淡出。
- 被按下的段会让内容轻微下沉，然后弹回。按钮组的外框及其相邻的段不会移动。
- 标签发生变化的段会沿弹簧调整大小；若它预留了宽度，则改为交叉淡化。
- 菜单从按钮组的边缘展开，雪佛龙在菜单打开时翻转。
- 减弱动效会让高亮直接跳到目标位置，去掉按下下沉和雪佛龙翻转，并交叉淡化标签与菜单。关闭动效则立即进入最终状态。

| 变化 | 完整 | 减弱 | 关闭 |
|---|---|---|---|
| 高亮移向指针或焦点 | 以灵敏的弹簧滑动；从按钮组外部出现时淡入 | 直接跳到目标 | 立即完成 |
| 段被按下 | 内容下沉到 97%（仅图标时为 90%）后弹回；高亮立即加深 | 无下沉；高亮加深 | 无下沉 |
| 预留宽度的段标签变化 | 在固定的框内交叉淡化 | 交叉淡化 | 立即完成 |
| 未预留宽度的段标签变化 | 段宽度沿弹簧适配新标签 | 交叉淡化 | 立即完成 |
| 菜单打开 | 从按钮组的边缘展开，伴随短暂位移；雪佛龙翻转 | 交叉淡化；雪佛龙不翻转 | 立即完成 |

## 相关组件

- `SegmentedControl`：用于保持选中的选择。
- `SplitButton`：用于一个默认操作加上一个替代操作菜单。
- `FloatingButtonGroup`：用于托盘中各自独立的柔和按钮。
- `ExpandingButtonGroup`：用于紧凑的图标按钮，标签在获得焦点时显示。

| 组件 | 适用场景 |
|---|---|
| `SegmentedControl` | 选择会保持选中，例如视图模式 |
| `SplitButton` | 一个默认操作比其附近的替代操作更重要 |
| `Button` | 单个操作独立地位于表面上 |
| `Tabs` | 各段在不同的内容面板之间切换 |
| `FloatingButtonGroup` | 操作是各自独立的浮动按钮，而不是一个连成一体的表面 |
| `ExpandingButtonGroup` | 紧凑的图标按钮应在获得焦点时显示标签 |
| `DropdownMenu` | 操作构成一个长列表，无法放进一行 |
