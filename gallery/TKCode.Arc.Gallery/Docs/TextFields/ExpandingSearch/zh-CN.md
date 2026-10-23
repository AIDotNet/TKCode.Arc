---
title: 展开式搜索
summary: 图标展开为搜索框，并在下方显示结果。
source-hash: a65b1d0f5fb6
---

<Demo name="HeaderSearch" />

## 何时使用

`ExpandingSearch` 用于页头或工具栏空间紧张的搜索。静止时它是一个 44 px 的圆形按钮，带搜索图标，所占空间并不比图标按钮多。用户激活后，圆形会展开为文本框，匹配的结果按分组显示在其下方。它适用于完整搜索框会占用页面导航、标题和操作所需空间的场景，也适用于搜索只是偶尔用来跳转、而不是主要任务的场景。

- **SaaS 控制台页头中的项目与文档跳转**：Northwind 工作区页头，带通知铃铛和头像。输入“atlas”会打开 Atlas 重新设计项目，两次按键即可到达目标位置。
- **输入前展示最近项**：`Suggestions` 会在“最近”标题下显示用户最近打开的三个文档或仪表板，因此字段在输入任何文字前就已经有用。
- **分析应用中切换仪表板与报表**：工具栏上已有日期范围和导出按钮。搜索可以按名称、关键词或所属团队找到 200 个仪表板中的一个，无需再增加一行控件。
- **支持与运营工具**：内部控制台顶栏中的工单、客户或订单查询。每个结果都有标题和一行元信息（例如“Order · Paid · 12 Mar 2026”），用来区分两个相似的结果。
- **设置与管理导航**：从任何界面快速进入设置页面的紧凑方式，页面列表足够短，可以按标题和关键词匹配。

## 何时不用

- **过滤页面上已有的表格、列表或网格**：用 `SearchField`，它原位过滤，带清除按钮和 `SearchSubmitted`。展开式字段把过滤藏在一次点击之后，人们看不出列表已经被缩小。
- **为表单中保存的值做选择**：用 `Combobox` 或 `Select`。它们把所选值保存为表单的字段并支持校验；本控件只报告被选中的项。
- **执行操作的全局命令面板**：用展示库中的 `CommandPalette` 区块，它面向带快捷键和分类的命令。`ExpandingSearch` 用于查找记录和位置。
- **通过服务端分页搜索数千条记录**：控件每次按键都会在内存中对传入的项排序。应使用 `SearchField` 查询服务端，并把分页后的结果显示在 `Table` 或 `DataGrid` 中。
- **必须保持可见的搜索**：需要逐行阅读的结果列表（例如带多行摘要的消息搜索）需要空间。应把它放在带 `SearchField` 和结果列表的页面上。
- **页头中单一的固定选择**，例如工作区切换器：用 `DropdownMenu` 或 `Select`，它们会始终显示带标签的选项。

## 用法

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:ExpandingSearch Label="搜索项目与文档"
                         MaxResults="6"
                         ExpandedWidth="360" />
</UserControl>
```

`ExpandingSearch` 派生自 Avalonia 的 `TemplatedControl`。它的模板由三部分组成：覆盖收起圆形的按钮、随字段一起增长的外壳，以及基于 Avalonia `TextBox` 构建的字段。因此光标、选择、输入法组字和剪贴板快捷键的行为与文本框一致。结果列表是锚定在字段上的弹出层，因此不会被它所在的页头裁切。控件自身负责状态机：展开、收起、高亮和选择。

### 结构

1. **触发按钮**：覆盖收起圆形的透明按钮，其名称为 `Label`。圆形为 44 px，即中等控件尺寸的高度。
2. **搜索图标**：18 px 的 Lucide 搜索图标，位于圆形前缘内侧 12 px 处。静止时使用次要文字颜色；字段展开或获得焦点时使用前景色。
3. **外壳**：1 px 边框，表面色背景。静止时使用默认边框色，展开时使用强边框色，字段或按钮有焦点时使用前景色。外壳的圆角随宽度增加，从圆形过渡到控件圆角。
4. **焦点环**：外壳外侧 3 px 的焦点色光晕，仅在键盘焦点时显示。
5. **字段文字与占位文字**：文字为正文字号（14 px），前缘留出 38 px，后缘留出 40 px，使文字避开清除按钮。字段为空时，`PlaceholderText` 以弱化文字颜色显示；未设置时，占位文字即为 `Label`。
6. **清除按钮**：28 px 的圆形按钮，位于后缘 7 px 处，内含 16 px 的关闭图标。仅在字段有文字时出现。它不可聚焦；键盘通过 `Escape` 清空查询，同时收起字段。
7. **结果面板**：浮动表面，使用抬起表面色、浮动阴影和面板圆角。位于字段下方 8 px，宽度跟随字段，高度不超过 352 px 或窗口高度的 60%（取较小者）。较长的列表在面板内滚动。
8. **分组标题**：分组名称（`Group`），弱化颜色，12 px，中等字重，位于其结果行上方。
9. **结果行**：高度至少 48 px，水平内边距 12 px、垂直内边距 6 px，圆角 20 px。包含可选的 18 px 图标（`Icon`）、14 px 的标题（匹配的文字为中等字重），以及可选的 12 px 元信息行（弱化颜色）。
10. **空状态**：标题为“未找到结果”，其后是加引号的查询内容，下方是弱化颜色的 `EmptyHint` 行。

### 项与建议

为 `ItemsSource` 填入 `ExpandingSearchItem` 对象。每一项都有 `Id`（稳定的键，为空时回退到标题）和 `Title`，还可以设置可选的 `Meta`、`Group`、`Icon`、`Keywords` 和 `Tag`。`Keywords` 是用来查找该项的额外词语，但不会显示。`Tag` 可以是任意对象，在选中该项时返回，例如记录本身或它的路由。在 XAML 中直接添加的 `Items` 用法相同：控件会监听该列表的变化。

`Suggestions` 是另一个列表，在用户输入任何内容之前显示，标题为 `SuggestionsLabel`（默认标题为“最近”）。建议项不会被查询过滤；用户一开始输入，它们就会被结果替换。

```csharp
var items = new List<ExpandingSearchItem>
{
    new() { Id = "q3", Title = "季度报告", Meta = "文档 · 2 小时前编辑", Group = "文档" },
    new() { Id = "atlas", Title = "Atlas重新设计", Meta = "项目 · 12 个待办任务", Group = "项目" },
    new() { Id = "billing", Title = "账单迁移", Group = "项目", Keywords = ["invoices", "payments"] }
};

header.ItemsSource = items;
header.Suggestions = new[] { items[0], items[1] };
header.SuggestionsLabel = "最近";
```

#### 结果的排序方式

匹配不区分大小写，并使用去除首尾空白后的查询。每个项取四个匹配层级中最好的一个，层级数字越小排得越靠前：

| 层级 | 查询匹配 | 以 "report" 为例 |
|---|---|---|
| 1 | 标题的开头 | "Report automation" |
| 2 | 标题中某个词的开头（位于空格、`-` 或 `/` 之后） | "Quarterly report" |
| 3 | 标题中某个词的内部 | "Subreport tools" |
| 4 | 标题不匹配时，元信息行或关键词匹配 | "Revenue"，元信息为 "Monthly report" |

没有匹配的项会被丢弃。同一层级中的项保持提供时的顺序。`MaxResults`（默认为 6）在排序之后生效，因此截断列表时保留的是最佳匹配。之后，结果会按 `Group` 标题分组，分组的显示顺序是其第一项在源列表中出现的顺序，而不是最佳匹配的顺序。每个分组中只显示匹配的项。

由于层级优先，以查询开头的短项会胜过只包含该查询的长项。标题应使用人们真正会输入的词，因为标题开头的权重最大。

### 选择

用户有两种方式选中结果：在高亮的结果上按 `Enter`，或用鼠标左键点击某一行。无论哪种方式，选择都按以下顺序进行：

1. 字段收回为图标，查询内容被清空。
2. 焦点返回触发按钮。
3. 以选中项作为 `e.Item` 触发 `ItemSelected`。该事件会冒泡，因此树中更上层的处理程序也能收到。
4. 若该命令可以执行，则以选中项作为参数执行 `SelectCommand`。

在 MVVM 应用中使用 `SelectCommand`；当代码后置需要直接处理该项时，使用事件。

```csharp
header.ItemSelected += (_, e) => Open(e.Item.Id);
```

```xml
<arc:ExpandingSearch Label="搜索"
                     ItemsSource="{Binding Items}"
                     SelectCommand="{Binding OpenCommand}" />
```

### 绑定到视图模型

视图模型拥有项、查询和导航。控件负责显示结果并报告选择。`Query` 与 `IsExpanded` 是双向的；`ItemsSource`、`Suggestions` 和 `SelectCommand` 是单向的。

```csharp
public partial class HeaderViewModel : ObservableObject
{
    private readonly INavigationService _navigation;

    public HeaderViewModel(INavigationService navigation)
    {
        _navigation = navigation;
    }

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private bool _isSearchOpen;

    public ObservableCollection<ExpandingSearchItem> SearchItems { get; } = new();

    public ObservableCollection<ExpandingSearchItem> RecentItems { get; } = new();

    [RelayCommand]
    private void OpenResult(ExpandingSearchItem? item)
    {
        if (item is null)
        {
            return;
        }
        IsSearchOpen = false;
        _navigation.Open(item.Id);
    }
}
```

```xml
<arc:ExpandingSearch Label="搜索项目与文档"
                     ItemsSource="{Binding SearchItems}"
                     Suggestions="{Binding RecentItems}"
                     SuggestionsLabel="最近"
                     Query="{Binding SearchText, Mode=TwoWay}"
                     IsExpanded="{Binding IsSearchOpen, Mode=TwoWay}"
                     SelectCommand="{Binding OpenResultCommand}" />
```

- `Query` 是双向的，随每次按键更新。通过 `Escape` 或选择结果收起字段时，它会被清空，视图模型会收到空字符串。从视图模型设置它会更新字段。
- `IsExpanded` 是双向的。将其设为 `True` 会展开字段并使其获得焦点；字段收起时，控件会把它设回 `False`。若要用窗口中的键盘快捷键打开搜索，请使用它。
- 如果搜索必须随输入进行，请在 `Query` 变化的地方（其 setter 或属性变更处理程序中）执行搜索。
- 每次查询变化都会重新读取 `ItemsSource`。对 `ObservableCollection` 的修改会在下一次按键时生效；若给 `ItemsSource` 赋一个新集合，则立即生效。
- 编译绑定需要在视图上设置 `x:DataType`（为简洁起见，这里的示例省略了它）。

### 在代码中打开与关闭

`Expand` 会打开字段并把焦点移入其中。`Collapse` 会将其收回为图标，并清空查询。默认情况下，`Collapse` 会把焦点返回触发按钮；当调用方要把焦点移到别处（例如移到该项打开的页面）时，传入 `returnFocus: false`。

```csharp
search.Expand();
search.Collapse();
search.Collapse(returnFocus: false);
```

`IsExpanded` 是双向的，因此也可以绑定到视图模型。另外有三个只读成员报告列表的状态：`IsPanelOpen`（结果是否正在显示）、`VisibleItems`（按顺序排列的可见项，即建议或结果）以及 `ActiveIndex`（`VisibleItems` 中高亮的位置，无则为 -1）。字段中的文字请读取 `Query`。

### 锚点与宽度

`Anchor` 设置按钮所在的边缘。字段从该边缘向外展开，最宽至 `ExpandedWidth`（默认为 360）。请把控件放在能容纳完整宽度的区域中：控件会填满其区域，而字段绝不会超过该区域或 `ExpandedWidth`，取两者中较小的一个。在较窄的页头上，字段会收缩到区域宽度，而不是挤开页头中的其他项。

当控件位于页头或面板的左边缘时，使用 `Anchor="Start"`，这样圆形会保持在用户预期的位置，字段则朝内容方向展开。控件位于工具栏右边缘（常见情况）时，保留默认的 `End`。

<Demo name="AnchoredAtStart" />

### 焦点与关闭

打开字段会把焦点移入其中。结果面板只在字段有焦点时打开，并一直保持打开，直到字段失去焦点或字段收起。点击面板外部不会关闭它，因为轻触关闭已被禁用；面板会在焦点离开字段时关闭。

- **焦点离开空字段**：字段立即收回为图标，焦点不移动。
- **焦点离开有文字的字段**：结果关闭，字段保持展开并保留其文字。通过点击或键盘移动焦点回到字段时，结果会重新打开。
- **切换到其他窗口**：字段保持其状态，窗口失去激活时不会收起。
- **Escape**：收起字段，清空查询，并把焦点返回触发按钮。

### 示例：控制台页头中的项目搜索

分析控制台的页头。工作区名称和头像分别位于左侧和右侧，搜索以 44 px 圆形居中显示，即用户使用前的样子。页头为字段展开时需要的空间预留了位置，因此用户激活搜索时，标题和头像不会移动。字段的结果不在图中：它们会在输入时于浮动面板中打开，如上面的面板说明所述。在实际应用中，页头还会设置 `Suggestions`，使面板在字段打开时即提供最近项。

![控制台页头，显示工作区名称、收起的项目搜索圆形按钮和头像](images/ProjectSearchHeader.png)

```xml
<Border Width="680"
        Padding="16,12"
        CornerRadius="16"
        BorderThickness="1"
        BorderBrush="{arc:ArcDesignTokenResource Border}"
        Background="{arc:ArcDesignTokenResource Surface}">
    <DockPanel>
        <Border DockPanel.Dock="Right"
                Width="32"
                Height="32"
                CornerRadius="16"
                Margin="12,0,0,0"
                Background="{arc:ArcDesignTokenResource AccentSubtle}">
            <TextBlock Text="AL"
                       FontSize="12"
                       FontWeight="Medium"
                       HorizontalAlignment="Center"
                       VerticalAlignment="Center"
                       Foreground="{arc:ArcDesignTokenResource Foreground}" />
        </Border>
        <TextBlock DockPanel.Dock="Left"
                   Text="Acme Analytics"
                   FontSize="16"
                   FontWeight="Medium"
                   VerticalAlignment="Center"
                   Foreground="{arc:ArcDesignTokenResource Foreground}" />
        <arc:ExpandingSearch Label="Search projects and docs"
                             ExpandedWidth="360"
                             HorizontalAlignment="Right" />
    </DockPanel>
</Border>
```

### 文案规范

- **标签**：简短的名词短语，说明搜索的对象，例如“搜索项目与文档”或“搜索订单”。它是按钮和字段的无障碍名称，因此单独看也必须说得通。
- **占位文字**：只有当默认值（标签）不够清楚时才设置 `PlaceholderText`。它应说明能找到什么，而不是下指令，例如“项目、文档、仪表板”。
- **项标题**：用户会认得的名称：项目、文档、客户。标题尽量控制在 40 个字符以内，避免结果行被截断。
- **元信息行**：一行简短的事实，用来区分相似的项：类型、所有者或最近修改时间，数字用阿拉伯数字（“12 个待办任务”）。
- **空状态提示**：说明下一步（默认为“请尝试更短的词，或检查拼写。”）。不要责怪用户。
- **分组名称**：复数名词（“项目”“文档”“仪表板”）。
- **建议标题**：一个简短的标题，说明为什么显示这些项（“最近”“已固定”）。

| 不要写 | 改为 |
|---|---|
| 搜索… | 搜索项目与文档 |
| 没有找到任何匹配项！！！ | 未找到 "forecast" 的结果 |
| 结果 | 项目 |
| 输入你要找的项目名称 | 项目、文档、仪表板 |

### 最佳实践

**推荐**

- 按字段要查找的内容为其命名（“搜索项目与文档”）。`Label` 是按钮、字段和结果的无障碍名称，笼统的标签会让每个结果列表都含糊不清。
- 为用户最常打开的目的地设置 `Suggestions`。这样面板在输入前就有有用的内容，第一次按键只会缩小一个较短的列表，而不是从零开始。
- 为每一项设置稳定的 `Id` 和 `Group`。`Id` 使选中的项可以追溯到其记录，分组则让较长的结果列表便于浏览。
- 把控件放在能容纳 `ExpandedWidth` 的区域中。字段永远不会超出其区域，因此过窄的区域会导致搜索拥挤、结果行被裁切。
- 在 MVVM 应用中绑定 `SelectCommand`。该命令在字段收回之后执行，因此导航从稳定的页头开始。

**避免**

- 搜索页面上已有的列表。应使用 `SearchField`，它原位过滤，且不会把过滤藏在点击之后。
- 在 `ItemsSource` 中填入数千条记录。每次按键都要对所有项排序，因此应先按权限过滤，并让服务端处理大型查询。
- 依赖查询为空时隐藏的结果。没有查询也没有 `Suggestions` 时，面板什么也不显示，对尚未输入的用户来说，字段看起来像是坏了。
- 字段打开时替换项，却不发起新的查询。面板只在查询变化或属性被重新赋值时才重新读取 `ItemsSource`，因此过时的列表会一直显示到下一次按键。
- 用控件保存表单中的值。`Combobox` 会保存该值并进行校验；本控件只报告被选中的项。

### 数据结构与性能

每次按键都会对所有项执行排序，然后构建结果行。对于页头中的数百项，这很快；对于数千项则会变慢。对于大型目录，应把 `ItemsSource` 限制为用户在本工作区中可以打开的项（绑定之前先按权限过滤），并在列表过长、内存放不下时让服务端处理查询。保持 `MaxResults` 较小（6 到 8），因为面板本就设计得较短，过长的列表只会滚动。

每个会话构建一次项并复用。为每条记录创建新的 `ExpandingSearchItem` 时都应设置稳定的 `Id`，使控件和视图模型能把选中的项对应回其记录。当路由需要的信息超过 `Id` 时，把记录本身放入 `Tag`。

用应用的文化设置来格式化元信息行中的数字和日期，例如金额使用 `amount.ToString("C", CultureInfo.CurrentUICulture)`，使中文或德文用户看到本地格式。控件不会替你格式化数值，它会按原样显示元信息字符串。

### 常见问题

**我点击别处时字段为什么会收起？** 空字段在失去焦点时会收起，这是设计如此，以免闲置的搜索占用页头空间。有文字的字段会保持展开并保留查询，方便用户回来继续。

**如何用键盘快捷键打开搜索？** 在窗口中把快捷键绑定到一个命令，该命令将视图模型的 `IsSearchOpen` 设为 `True`；或在代码后置中对控件调用 `Expand`。焦点会移入字段，用户可以立即输入。

**能否只用鼠标选择结果？** 可以。用左键按下某一行时，焦点仍留在字段中；在同一行上松开按钮即选中它。指针移到某一行上时，该行也会获得高亮，因此键盘用户和鼠标用户看到的高亮是一致的。

**输入时为什么第一个结果已经高亮？** 查询一有文字，第一个结果就会高亮，因此 `Enter` 会选中它。建议项不带高亮显示；按 `Down` 会选中第一个建议。

**为什么我的项没有列在我设置的分组下？** 分组按其第一项在 `ItemsSource` 中出现的顺序显示，分组内的项保持排序后的顺序。把你想要的分组放在列表顶部，并检查 `Group` 是否完全一致，因为分组按书写的文字进行比较。

## API 参考

<!-- api: ExpandingSearch -->

<!-- api: ExpandingSearchItem -->

## 无障碍

- `Label` 为触发按钮和字段命名。占位文字会回退为 `Label`，因此字段为空时名称仍然可见。
- 高亮结果的标题是字段的帮助文字。屏幕阅读器在读出字段时，还会在用户浏览列表时说明哪个结果被高亮。
- 每个结果行以其标题命名，如果有元信息行，则随后读出（“账单迁移，项目 · 已暂停”）。
- 结果数量、带查询内容的“未找到结果”或建议数量会以礼貌的方式播报。只有在数量或状态变化时才会播报。
- 收起的字段处于惰性状态：不可聚焦、不接收指针输入，也不在 Tab 顺序中，因此不会挡住其下方的内容。
- 清除按钮不在 Tab 顺序中。键盘用户可以选中文字后删除来清空，或用 `Escape` 收起整个字段。
- 按钮的焦点环只在键盘焦点（Tab 或方向键导航）时出现，因此指针按下会打开字段而不显示焦点环。字段在任何焦点下都会显示其焦点边框。
- 结果行不在 Tab 顺序中。字段有焦点时，键盘用户用 `Down` 与 `Up` 到达它们。

### 键盘

| 按键 | 操作 |
|---|---|
| `Tab` / `Shift+Tab` | 在页头的控件之间移动；收起的按钮是停靠点，展开的字段会取代其位置 |
| 按钮上的 `Enter` 或 `Space` | 打开字段并把焦点移入其中 |
| 输入文字 | 更新 `Query`，列出结果或空状态；有文字后高亮第一个结果 |
| `Down` / `Up` | 把高亮移到下一个或上一个结果，并在两端循环。没有高亮时，`Down` 选中第一个，`Up` 选中最后一个 |
| `Enter` | 选中高亮的结果，收起字段并把焦点返回按钮。没有高亮时控件不处理它 |
| `Escape` | 将字段收回为图标，清空查询，并把焦点返回按钮 |
| `Ctrl+A`、`Ctrl+C`、`Ctrl+X`、`Ctrl+V`（macOS 上为 `Cmd`） | 在字段中全选、复制、剪切和粘贴 |

## 动效

- 展开时宽度以带轻微过冲的弹簧变形，圆形图标变为圆角字段。圆角与宽度共用同一弹簧，因此两者不会错位。
- 收起时宽度以更平滑、无过冲的弹簧收回，因此圆形不会缩小到低于其尺寸。
- 结果面板以平滑弹簧从零高度展开，并淡入其文字。减弱动效时只做淡入。
- 高亮以灵敏弹簧在结果之间滑动，并保持上一次移动的速度。新的结果集在其行上淡入，没有位移。
- 按下按钮时缩放至 97%，直到松开；松开时由变形接管。

| 变化 | 完整 | 减弱 | 关闭 |
|---|---|---|---|
| 字段展开 | 宽度以弹簧变形；形状开始移动后，占位文字与文字淡入 | 宽度立即变化；文字在 150 ms 内淡入 | 立即完成 |
| 字段收起 | 宽度以平滑弹簧收回；文字先淡出 | 宽度立即变化；文字在 100 ms 内淡出 | 立即完成 |
| 结果面板展开或收起 | 高度以平滑弹簧展开；文字淡入 | 仅淡入淡出 | 立即完成 |
| 高亮移动 | 以灵敏弹簧在行之间滑动，保持速度 | 不滑动；高亮原地移动 | 立即完成 |
| 按钮按下 | 缩放至 97%，直到松开 | 不缩放 | 立即完成 |
| 悬停与焦点颜色 | 边框、背景和图标颜色渐变；焦点环淡入 | 立即完成 | 立即完成 |

## 相关组件

- `SearchField`：用于过滤页面上已有内容的字段。
- `Combobox`：用于选择表单中保存的值。
- `Input`：通用单行输入框；搜索字段的文本框即以它为基础。

| 组件 | 适用场景 |
|---|---|
| `SearchField` | 搜索在页面上原位过滤表格、列表或页面 |
| `Combobox` / `Select` | 所选的值保存为表单的字段 |
| `DropdownMenu` | 短小固定的操作或选项列表需要始终以菜单形式显示 |
| `Input` | 单行值需要输入并保存，而不是搜索 |
| `CommandPalette` 区块 | 用户通过键盘快捷键在应用中运行命令 |
| `Breadcrumb` | 用户需要看清自己所在的位置，并向上移动层级 |
