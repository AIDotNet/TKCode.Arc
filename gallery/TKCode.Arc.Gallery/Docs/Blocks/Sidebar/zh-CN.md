---
title: 侧边栏
summary: 工作区导航，包含分组、嵌套页面、数量和滑动的选中标记；可收起为图标窄栏。
source-hash: 4b3733075654
---

<Demo name="Preview" />

## 何时使用

侧边栏是页面多到顶部导航放不下的应用的主导航。它在整个会话期间都位于窗口左侧边缘，因此用户能记住每个页面的位置，无需搜索即可返回。

- **SaaS 管理后台的主菜单**：工作区的首页、收件箱、报表、账单和设置。顶层入口放在第一组中且不带标题，拥有各自页面的区域则放在下方带标题的分组中。
- **工作工具中的项目与区域**：项目列表，每个项目下有一层页面（Atlas、Beacon）。父行会打开或收起其页面，而不是显示自己的视图。
- **邮箱与文档站点**：带数量和彩色标签的文件夹，或是一棵可以随输入由筛选框实时缩小的指南树。
- **账户与账单区域**：账单分组中包含发票和套餐，旁边是带有新事件数量的用量页面。用户在其他区域工作时，该数量仍保持可见。
- **页面很多的高密度内部工具**：运维控制台中，筛选框可按页面名称的开头字母找到页面；窗口较窄时，图标窄栏仍能让页面可达。
- **需要留在屏幕上、但按需占用更少空间的导航**：侧边栏可收成 64 px 的图标窄栏，每个页面都保留提示和菜单，因此内容区域保持宽度不变。

## 何时不用

- **同一对象的两到五个视图**，例如某个订单的概览、活动和设置：使用 `Tabs`，它位于内容上方，使对象始终可见。
- **序列中的步骤，或层级结构中的路径**：向导使用 `Stepper`，深层结构中页面所处的位置使用 `Breadcrumb`。
- **深层的文件夹与文件树**：使用 `TreeView`。侧边栏只显示一层嵌套页面；文件树需要在每一层级上展开和收起。
- **操作而非位置**：命令菜单使用 `DropdownMenu`，跨页面与操作的键盘搜索使用 `CommandPalette`。属于列表本身的少数几个操作（“New project”“More”）可以作为操作行放在其中，但侧边栏的行大多应当打开页面。
- **已登录的用户与工作区切换**：账户菜单请在侧边栏的头部或底部使用 `UserMenu`，侧边栏本身只用于页面导航。

## 用法

在侧边栏中放入 `SidebarSection` 元素，并在分组中放入 `SidebarItem` 元素。没有 `Header` 的分组不显示标题，适合作为第一组（首页、收件箱）。为每个页面设置 `Key`，并将 `SelectedKey` 绑定到你的路由。侧边栏的宽度由主题决定（展开时为 240，窄栏时为 64），因此请把它放在 `DockPanel` 或 `Auto` 网格列中，不要设置 `Width`。

```xml
<DockPanel xmlns="https://github.com/avaloniaui"
           xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
           xmlns:arc="https://github.com/AIDotNet/TKCode.Arc"
           Height="420">
    <arc:Sidebar DockPanel.Dock="Left" SelectedKey="home" IsFilterVisible="True">
        <arc:SidebarSection>
            <arc:SidebarItem Key="home" Label="首页" Icon="{x:Static arc:ArcIcons.House}" />
            <arc:SidebarItem Key="inbox" Label="收件箱" Icon="{x:Static arc:ArcIcons.Inbox}" Badge="4" />
        </arc:SidebarSection>
        <arc:SidebarSection Header="工作区">
            <arc:SidebarItem Key="projects" Label="项目" Icon="{x:Static arc:ArcIcons.Folder}" IsExpanded="True">
                <arc:SidebarItem Key="projects/atlas" Label="Atlas" />
                <arc:SidebarItem Key="projects/beacon" Label="Beacon" />
            </arc:SidebarItem>
        </arc:SidebarSection>
    </arc:Sidebar>
    <TextBlock Margin="24" Text="页面内容" />
</DockPanel>
```

`Sidebar` 派生自 Avalonia 的 `TemplatedControl`。它本身不可获得焦点：其中的行列表接收焦点，因此整个导航只占一个 Tab 停靠点。它的内容是 `Sections` 集合（XAML 内容）；每个 `SidebarSection` 在 `Items` 中保存其页面，而 `SidebarItem` 在自己的 `Items` 中保存子页面。模型刻意保持精简：两层页面、一个选中页面，其余一切都是数量、图标或标志。

选中的页面背后是一块柔和的区块，图标变为强调色，区块在各行之间滑动。页面顶部的预览是应用窄栏布局：头部是徽标和两行标题，页面自己的顶栏负责切换侧边栏，底部是已登录的用户。下面的布局再给出三种。

### 结构

1. **头部**（`Header`）：侧边栏顶部的内容，通常是徽标加标题或工作区切换器。两侧各有 12 px 边距，并与收起按钮共用一行。在窄栏上，头部会让位给收起按钮；没有收起按钮时，头部保留，并被裁剪到窄栏宽度。
2. **收起按钮**（`IsCollapseButtonVisible`、`CollapseButtonPlacement`、`CollapseButtonText`）：一个 40 px 宽的幽灵图标按钮，显示 PanelLeft 图形。它位于头部行末尾，或在筛选框旁边。默认显示。
3. **筛选**（`IsFilterVisible`、`FilterText`、`FilterPlaceholder`）：头部下方 32 px 高的搜索框，两侧各有 12 px 边距。在窄栏上它会变成一个 40 px 的搜索按钮。
4. **分组标题**（`SidebarSection.Header`）：28 px 高的一行，使用弱化文字颜色的小号文字，第一个分组之后，它的上方留 10 px。可收起的标题末尾有一个 12 px 的箭头。没有 `Header` 的分组不显示标题；在第一个分组之后，它会留出一个 16 px 的间距。
5. **页面行**（`SidebarItem`）：32 px 高，圆角 10 px，两侧内边距 12 px。包含弱化色的 16 px 图标（`Icon`）、标签（`Label`，13 px，次要文字颜色）前 10 px 的间距，以及末尾可选的数量（`Badge`）。行与行之间相距 1 px。
6. **选中区块**：选中行背后一块前景色的柔和淡色，标签使用完整对比度，图标使用强调色。没有强调色竖条。它是一个共享元素，在各行之间滑动。
7. **未读点与操作行**：`IsUnread` 会在行末添加一个 6 px 的强调色圆点，并让标签更醒目。`IsAction` 行颜色柔和，永远不会被选中。
8. **嵌套页面**：子行 30 px 高，缩进父行下方 26 px，挂在一条 1 px 的引导线上，引导线使用细边框颜色。子项的子项不会显示。
9. **父级箭头**：父行末尾一个 14 px 的箭头。父级收起时朝右，展开时朝下。
10. **页脚**（`Footer`）：底部的内容，通常是已登录的用户，上方有一条 1 px 的分隔线。两侧各有 12 px 边距，在窄栏上保持展开时的布局，并裁剪到窄栏宽度。

窗格比旁边的页面略深一点（表面色与弱化表面色的混合），右侧有一条 1 px 的边框。展开时侧边栏宽 240 px，窄栏宽 64 px；两者均由主题提供。`Background`、`BorderThickness` 和 `CornerRadius` 都是普通属性，因此侧边栏也可以作为圆角卡片浮动。

### 布局

侧边栏只是一个控件；它周围的布局来自放进头部、筛选框和底部的内容、你给它的外框，以及它旁边的页面。下面四种布局展示了它的适用范围，每一种都可以收成窄栏。页面顶部的预览是第一种，**应用窄栏**：头部是徽标和两行标题，侧边栏自己不显示收起按钮（`IsCollapseButtonVisible="False"`），因为页面的顶栏上有一个调用 `ToggleCollapsed()` 的切换按钮，底部是用户区。

**工作区。** 侧边栏以内嵌卡片的形式浮动：用 `CornerRadius` 和 `BorderThickness` 给它自己的圆角边框。头部是工作区切换器，收起按钮位于筛选框旁边（`CollapseButtonPlacement="Filter"`）。页面用彩色圆点作为图标，`IsUnread` 标出有新动态的项目，列表以一个操作行结尾。底部有存储用量条（`Progress`）和已登录的用户（带状态点的 `Avatar`）。

<Demo name="Workspace" />

**收件箱。** 头部是占满整行的“Compose”按钮（在窄栏上只保留它的图标），下面是带数量的文件夹、带彩色圆点且可收起的“Labels”分组，以及一个柔和的“More”行（带 `TrailingIcon` 的 `IsAction`）。页面自己的顶栏负责切换侧边栏。

<Demo name="Inbox" />

**文档。** 头部是标题和版本按钮，筛选框旁边是收起按钮。页面是带图标和箭头的分组树；在筛选框中输入会实时缩小这棵树，并展开匹配的分组。旁边的页面是一篇文章，带有“On this page”大纲：示例的 code-behind 监听滚动位置，用 `GlidingIndicator` 让 2 px 的强调色竖条滑到当前标题。

<Demo name="Docs" />

四种布局都使用 `ResizeMode="Overlay"`（见“侧边栏旁边的页面”）。文档布局中侧边栏自己的设置很简短：

```xml
<arc:Sidebar x:Name="Nav"
             IsFilterVisible="True"
             CollapseButtonPlacement="Filter"
             FilterPlaceholder="搜索文档"
             ResizeMode="Overlay">
    <arc:Sidebar.Header>
        <TextBlock Text="Tidewater 文档" />
    </arc:Sidebar.Header>
    <arc:SidebarSection>
        <arc:SidebarItem Key="start" Label="入门" Icon="{x:Static arc:ArcIcons.Sparkles}" IsExpanded="True">
            <arc:SidebarItem Key="start/introduction" Label="简介" />
            <arc:SidebarItem Key="start/quickstart" Label="快速开始" />
        </arc:SidebarItem>
    </arc:SidebarSection>
</arc:Sidebar>
```

### 分组与嵌套页面

带有子项的 `SidebarItem` 是父级。父级永远不会被选中：选择它会展开或收起其页面，这些页面挂在一条细引导线下。需要落地页的父级应为该页面添加一个子项（例如“报表”下的“概览”）。父级的 `IsExpanded` 是双向的。在带有 `Header` 的 `SidebarSection` 上设置 `IsCollapsible`，即可让用户把整个分组收起；此时标题末尾会出现一个小箭头，该分组自身的 `IsExpanded` 同样是双向的。不可收起的分组始终显示其页面。只显示一层嵌套。

`IsEnabled="False"` 会让页面变暗，方向键会跳过它；被禁用的父级也无法展开。每个项目还可以带有 `Tag` 和 `Command`（以及 `CommandParameter`）。

### 数量、未读点、操作行与图标

`Badge` 会在行尾显示一个简短的数量，例如未读消息的“12”；`Badge` 为 `null` 时不显示任何内容。`IsUnread="True"` 则在行尾显示一个小的强调色圆点，让标签更醒目，并在该行的无障碍名称中加上“未读”。数字有意义时用数量，只需要表示“有新内容”时用圆点。在窄栏上，两者都会变成图标上的圆点。

`Icon` 接受 24 × 24 的 Lucide 图形（例如 `ArcIcons` 中的字形），也可以是任何能放入 16 px 图标槽的控件。用一个小的 `Ellipse` 就能做出彩色圆点图标，并与其他行的图标对齐。作为图标的控件只能放在一行中，因此请为每个项目各创建一个。`TrailingIcon` 会在标签之后紧跟一个图标，颜色与标签的弱化色相同；适合用作提示，例如“More”行的箭头。

`IsAction="True"` 会把一行变成操作：颜色柔和，永远不会被选中。选择它会触发 `ItemInvoked` 并执行其命令，但 `SelectedItem` 保持不变。适用于“New project”或“More”。

```xml
<arc:Sidebar xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:SidebarSection Header="项目">
        <arc:SidebarItem Key="projects/spring" Label="春季发布">
            <arc:SidebarItem.Icon>
                <Ellipse Width="8" Height="8" Fill="{arc:ArcDesignTokenResource Series1}" />
            </arc:SidebarItem.Icon>
        </arc:SidebarItem>
        <arc:SidebarItem Key="projects/partner" Label="合作伙伴套件" IsUnread="True" />
        <arc:SidebarItem Key="projects/new" Label="新建项目" IsAction="True" Icon="{x:Static arc:ArcIcons.Plus}" />
        <arc:SidebarItem Key="projects/more" Label="更多" IsAction="True" TrailingIcon="{x:Static arc:ArcIcons.ChevronRight}" />
    </arc:SidebarSection>
</arc:Sidebar>
```

### 头部、筛选与底部

`Header` 接受任意内容：徽标加两行标题、工作区切换器、版本标签、占满整行的按钮。`Footer` 位于一条细分隔线下，可放已登录的用户、存储用量条，或两者都放。两者都会保持侧边栏展开时的布局，并被裁剪到窄栏宽度，因此位于起始边缘的徽标或头像仍然可见。像上面的各个布局那样，用 `:collapsed` 伪类淡出其余内容。

`IsFilterVisible` 会在头部下方显示筛选框（见“筛选”）。收起按钮通常位于头部行的末端；`CollapseButtonPlacement="Filter"` 则把它放到筛选框旁边，如工作区和文档布局所示。没有可见的筛选框时，按钮仍留在头部行。在窄栏上它总是移到顶部。当页面有自己的切换按钮时（如应用窄栏和收件箱），用 `IsCollapseButtonVisible="False"` 去掉它。

### 选中与路由

`SelectedItem` 与 `SelectedKey` 是双向的，并保持同步。`SelectedKey` 与页面的 `Key` 匹配，是绑定到路由的合适属性，因为路由本身就是字符串。在页面存在之前设置 `SelectedKey` 也没有问题：页面出现后，侧边栏会选中它，因此视图模型可以先设置路由。与任何页面都不匹配的键，以及父级或操作行的键，不会选中任何东西，并保留其值。

`ItemInvoked` 是一个路由事件，每次选择页面或操作时都会触发：无论是点击、按 Enter 或 Space，还是窄栏上的菜单。即使该页面已经被选中，它也会触发，而绑定到 `SelectedKey` 无法告诉你这一点。对于每次选择都必须发生的导航（例如把页面滚回顶部），请使用它。事件之后，该项自己的 `Command` 会执行，然后以该项为参数执行 `SelectCommand`，两者都只在可以执行时才执行。

<Demo name="DataBinding" />

```csharp
sidebar.ItemInvoked += (_, e) => router.Navigate(e.Item.Key!);
```

### 绑定到视图模型

侧边栏没有 `ItemsSource`：它的分组是 XAML 内容，因此行要么在 XAML 中编写，要么在代码中添加和移除（用于会变化的数据）。视图模型保存路由和收起状态；侧边栏通过双向绑定与之关联，并为每次选择执行一个命令。

```csharp
public partial class WorkspaceShellViewModel : ObservableObject
{
    private readonly INavigationService _navigation;

    public WorkspaceShellViewModel(INavigationService navigation) => _navigation = navigation;

    [ObservableProperty]
    private string? _selectedKey = "home";

    [ObservableProperty]
    private bool _isNavigationCollapsed;

    [RelayCommand]
    private void OpenPage(SidebarItem item)
    {
        // 每次选择都会执行，包括已经选中的页面。
        _navigation.Navigate(item.Key!);
    }
}
```

```xml
<arc:Sidebar SelectedKey="{Binding SelectedKey, Mode=TwoWay}"
             IsCollapsed="{Binding IsNavigationCollapsed, Mode=TwoWay}"
             SelectCommand="{Binding OpenPageCommand}"
             Label="Main navigation"
             IsFilterVisible="True" />
```

- `SelectedKey` 与 `IsCollapsed` 是双向的，因此应用中其他地方的路由变化（页面中的链接、后退按钮）会选中对应的行，而窄栏按钮的状态会写回视图模型。
- 把导航的副作用放在 `SelectCommand` 或 `ItemInvoked` 中执行，不要放在 `SelectedKey` 的处理程序中。路由由代码设置时选中项也会改变，而这并不总是用户的选择。
- 窗口变窄时，根据你自己的窗口宽度检查来设置 `IsNavigationCollapsed`；侧边栏不会替你监视窗口。
- 当数据驱动菜单时，请在代码中构建分组，并保留对数量会变化的项目的引用：

```csharp
var inbox = new SidebarItem { Key = "inbox", Label = "收件箱", Icon = ArcIcons.Inbox };
sidebar.Sections.Add(new SidebarSection
{
    Items = { new SidebarItem { Key = "home", Label = "首页", Icon = ArcIcons.House }, inbox }
});

// 数量变化时该行会原地更新：无需重建，也不会丢失焦点。
inbox.Badge = unreadCount > 0 ? unreadCount.ToString(CultureInfo.InvariantCulture) : null;
```

### 图标窄栏

`IsCollapsed` 会把侧边栏收成 64 px 的窄栏，它是双向的，因此页面中任何位置的按钮都可以通过绑定或调用 `ToggleCollapsed()` 来驱动它。侧边栏里的收起按钮，以及焦点位于侧边栏内部时的 Cmd+B（在 Windows 和 Linux 上为 Ctrl+B），都会执行相同的操作，即使筛选框有焦点也是如此。

在窄栏上，每个页面右侧都会显示带有其标签的提示。数量或未读标记会变成图标上一个 6 px 的圆点，分组标题变为细分隔线，筛选框变为搜索按钮，点击后会展开侧边栏并把光标放入筛选框；父级则会在右侧的菜单中打开其页面。菜单先列出父级的标签，然后将其页面列为单选项：当前选中的页面显示为已勾选，被禁用的页面显示为禁用，数量则作为快捷键文字显示。没有图标的页面显示其首字母。

头部和底部保持展开时的布局，并被裁剪到窄栏宽度，因此位于起始边缘的徽标或头像仍然可见。与各个布局一样，使用 `:collapsed` 伪类淡出其余内容。收起按钮可见时，窄栏上的头部会让位给它。

<Demo name="Rail" />

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <UserControl.Styles>
        <Style Selector="arc|Sidebar:collapsed TextBlock.rail-fade">
            <Setter Property="Opacity" Value="0" />
        </Style>
    </UserControl.Styles>
    <arc:Sidebar IsCollapsed="True" Height="320" />
</UserControl>
```

### 侧边栏旁边的页面

默认情况下（`ResizeMode="Overlay"`），侧边栏的布局宽度会立即改变，页面只布局一次，只有窗格做弹簧动画，并绘制在页面上方。把页面的 `RenderTransform` 绑定到 `PageShift`，页面就会随窗格边缘一起滑动，而不是跳变；不绑定时页面仍然布局正确，只是会跳到新位置。设置 `ResizeMode="Push"` 则让布局宽度做弹簧动画，因此它旁边的页面每一帧都会重新布局。这在任何宿主中都不需要绑定就能工作，对轻量页面也没有问题。

```xml
<DockPanel xmlns="https://github.com/avaloniaui"
           xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
           xmlns:arc="https://github.com/AIDotNet/TKCode.Arc"
           ClipToBounds="True">
    <arc:Sidebar x:Name="Nav" DockPanel.Dock="Left" ResizeMode="Overlay" />
    <Grid RenderTransform="{Binding #Nav.PageShift}">
        <TextBlock Text="页面内容" />
    </Grid>
</DockPanel>
```

`PageShift` 是只读的变换，在 `Push` 模式下始终为零。页面滑动的距离最多等于展开宽度与窄栏宽度之差，所以请给它外面的框加上裁剪（`ClipToBounds="True"`），如上面的各个布局所示。如果省略这个变换，页面仍然会正确布局，只是会跳到新位置，而不是滑过去。

### 筛选

`IsFilterVisible` 会在头部下方显示一个筛选框。筛选框会将每个页面的 `Label` 作为不区分大小写的子串进行匹配，并忽略首尾空格；操作行也会参与匹配。匹配的页面保留；含有匹配子页面的父级会展开；没有匹配页面的分组会隐藏。没有任何匹配时，侧边栏会以 `NoMatchesText` 取代行列表显示。`FilterText` 是双向的，`FilterPlaceholder` 为筛选框命名。

筛选框就是一个搜索框：按 Escape 清空，按 Down 把焦点移到剩下的第一个页面，按 Enter 选中剩下的第一个页面。筛选只决定哪些行可见，因此即使被筛选掉，选中的页面仍保持选中。在窄栏上不应用筛选。

### 数据结构与性能

让模型只包含导航所需的内容：`Key` 在整个侧边栏中必须唯一（`SelectedKey` 会找到第一个匹配的页面，而父级的键永远不会被匹配）、简短的 `Label`、一个图标以及可选的数量。层级超过一层的页面应放在 `TreeView` 中。

- **合并且增量。** 对 `Sections` 和各项目集合的修改会被合并处理：各行会在当前批次之后统一更新一次，因此一次添加多个页面只需一次更新。只有发生变化的部分会被处理：新页面获得新的行，被移除的页面失去它的行，仍然存在的页面的行保留其控件、焦点和选中状态。
- **属性变化开销很小。** 修改 `Badge`、`IsUnread`、`IsAction`、`Icon` 或 `TrailingIcon` 只会更新该行；修改 `Label` 或 `IsEnabled` 还会刷新筛选和被禁用的页面。
- **收起的父级不参与布局。** 已经收起并静止的父级或可收起分组会完全退出布局和渲染，因此有许多收起父级的树，布局和绘制都很轻。
- **不在屏幕上时不持有任何东西。** 侧边栏不在屏幕上时，它不持有任何行，也不订阅你的项，因此共享的或静态的项不会让它一直存活；回到屏幕上时会重新构建各行。
- **宽度弹簧。** 在 `Push` 模式下，收起过程的每一帧都会让页面重新布局；在 `Overlay` 模式下，页面只布局一次，只有窗格的排列会变化（见“侧边栏旁边的页面”）。

这些行都是真正的控件，因此侧边栏适合几十个页面；更大的目录应分组，或使用 `CommandPalette` 搜索。

### 实时数据

侧边栏会跟随其数据变化：修改项目上的 `Badge`、`IsUnread`、`Label`、`Icon` 或 `IsEnabled`，或增删、移动项目和分组，只有相关的行会变化。新增页面会让所在分组以与展开相同的弹簧增高。

<Demo name="LiveUpdates" />

```csharp
// 每一行代码只会触及相关的行：其他行保持原来的控件、焦点和选中状态。
inbox.Badge = "3";
inbox.IsUnread = true;
pinned.Items.Add(new SidebarItem { Key = "pinned/roadmap", Label = "Roadmap", Icon = ArcIcons.Flag });
pinned.Items.RemoveAt(0);
```

### 示例：管理后台导航

这是一个 SaaS 分析工作区的导航。收件箱上显示未读数量，财务用户可以把账单折叠起来，有新动态的项目带有未读点，项目列表以一个操作行结尾。头部标明工作区，底部标明已登录的用户，因此用户随时知道自己处于哪个账户中。

![管理后台导航，包含选中的报表、可折叠的账单分组和项目列表](images/AdminNavigation.png)

```xml
<DockPanel Width="720" Height="600">
    <arc:Sidebar DockPanel.Dock="Left"
                 SelectedKey="reports"
                 IsFilterVisible="True"
                 CollapseButtonPlacement="Filter"
                 FilterPlaceholder="Search">
        <arc:Sidebar.Header>
            <StackPanel Spacing="2" Margin="6,0,0,0">
                <TextBlock Text="Acme Analytics"
                           FontSize="{arc:ArcDesignTokenResource TypeSM}"
                           FontWeight="{arc:ArcDesignTokenResource WeightMedium}"
                           Foreground="{arc:ArcDesignTokenResource Foreground}" />
                <TextBlock Text="Production workspace"
                           FontSize="{arc:ArcDesignTokenResource TypeXS}"
                           Foreground="{arc:ArcDesignTokenResource TextMuted}" />
            </StackPanel>
        </arc:Sidebar.Header>
        <arc:SidebarSection>
            <arc:SidebarItem Key="home"
                             Label="Home"
                             Icon="{x:Static arc:ArcIcons.House}" />
            <arc:SidebarItem Key="inbox"
                             Label="Inbox"
                             Icon="{x:Static arc:ArcIcons.Inbox}"
                             Badge="12" />
            <arc:SidebarItem Key="reports"
                             Label="Reports"
                             Icon="{x:Static arc:ArcIcons.ChartBar}" />
        </arc:SidebarSection>
        <arc:SidebarSection Header="Billing" IsCollapsible="True">
            <arc:SidebarItem Key="usage"
                             Label="Usage"
                             Icon="{x:Static arc:ArcIcons.Activity}" />
            <arc:SidebarItem Key="invoices"
                             Label="Invoices"
                             Icon="{x:Static arc:ArcIcons.CreditCard}" />
        </arc:SidebarSection>
        <arc:SidebarSection Header="Workspace">
            <arc:SidebarItem Key="projects"
                             Label="Projects"
                             Icon="{x:Static arc:ArcIcons.Folder}"
                             IsExpanded="True">
                <arc:SidebarItem Key="projects/atlas" Label="Atlas" />
                <arc:SidebarItem Key="projects/beacon" Label="Beacon" IsUnread="True" />
            </arc:SidebarItem>
            <arc:SidebarItem Key="team"
                             Label="Team"
                             Icon="{x:Static arc:ArcIcons.Users}" />
            <arc:SidebarItem Key="new-project"
                             Label="New project"
                             IsAction="True"
                             Icon="{x:Static arc:ArcIcons.Plus}" />
        </arc:SidebarSection>
        <arc:Sidebar.Footer>
            <StackPanel Orientation="Horizontal" Spacing="10" Margin="6,0,0,0">
                <arc:Avatar DisplayName="Maya Chen" Size="Small" Status="Online" />
                <StackPanel VerticalAlignment="Center">
                    <TextBlock Text="Maya Chen"
                               FontSize="{arc:ArcDesignTokenResource TypeSM}"
                               Foreground="{arc:ArcDesignTokenResource Foreground}" />
                    <TextBlock Text="Owner"
                               FontSize="{arc:ArcDesignTokenResource TypeXS}"
                               Foreground="{arc:ArcDesignTokenResource TextMuted}" />
                </StackPanel>
            </StackPanel>
        </arc:Sidebar.Footer>
    </arc:Sidebar>
    <StackPanel Margin="32,28" Spacing="6">
        <TextBlock Text="Reports"
                   FontSize="{arc:ArcDesignTokenResource TypeXL}"
                   FontWeight="{arc:ArcDesignTokenResource WeightMedium}"
                   Foreground="{arc:ArcDesignTokenResource Foreground}" />
        <TextBlock Text="Revenue by product, last 30 days"
                   FontSize="{arc:ArcDesignTokenResource TypeSM}"
                   Foreground="{arc:ArcDesignTokenResource TextMuted}" />
    </StackPanel>
</DockPanel>
```

保持键稳定：`projects/atlas` 这样的键是书签或深层链接所保存的内容，因此修改标签是安全的，而修改键则不安全。

### 文案规范

- **标签**：句式大小写的简短名词或名词短语，例如“发票”或“团队成员”。使用页面自身标题中的词，使行与页面标题一致。
- **操作行**：动词加对象，句式大小写，例如“新建项目”。侧边栏中的其他内容都是名词，所以动词会告诉用户这一行会执行操作，而不是打开页面。
- **分组标题**：句式大小写的分组名称，不含动词。当分组只有一个明显用途时（如首页和收件箱），可以省略标题。
- **数量**：只使用数字，而且只用于等待用户处理的事项：未读消息、未处理告警、待审核项。永远不变的数量就只是一个标签。数字无关紧要时，请使用未读点。

| 不要写 | 改为 |
|---|---|
| 首页页面 | 首页 |
| 点击这里查看您的发票 | 发票 |
| 报表（新！！） | 报表 |
| 团队与权限管理 | 团队成员 |
| 可以更改的东西 | 设置 |
| 在您的工作区中添加一个新项目 | 新建项目 |

### 最佳实践

**推荐**

- 把用户最常访问的页面放在第一组中且不带标题；带标题的分组只用于有明确名称的群组。第一组是眼睛最先读到的内容。
- 将 `SelectedKey` 绑定到路由，并保持键稳定，使深层链接、书签和选中行保持一致。
- 只为等待用户处理的工作显示数量或未读点，并原地更新；陈旧的标记会让用户学会忽略它。
- 当侧边栏旁边的页面内容很多时，设置 `ResizeMode="Overlay"`，把页面的 `RenderTransform` 绑定到 `PageShift`，并给外框加上裁剪。
- 用 `:collapsed` 伪类淡出放在头部和底部的内容，并在起始边缘保留一个徽标或头像，使窄栏上仍留有可辨认的东西。

**避免**

- 让父级成为到达页面的唯一途径。父级会展开和收起，但永远不会被选中，因此没有落地页的父级会把用户带进死胡同。请改为添加一个作为落地页的子项。
- 把操作行用得超过一两处。“New project”和“More”适合放在它们所结尾的列表中；“退出登录”应放在菜单或按钮中，因为改变页面的行会让用户期待一个位置。
- 嵌套超过一层，或在其中构建文件夹树。只显示一层子项；更深的树既难以浏览，也难以用方向键导航。
- 使用过长的标签。标签与图标、数量共享 216 px 宽的行，窄栏的提示显示同样的文字，而筛选也针对它匹配，因此简短而独特的标签在每种模式下都更易找到。
- 在侧边栏上设置 `Width`。展开和窄栏宽度由主题设定，收起时在两者之间以弹簧过渡；局部宽度会覆盖两者，并使收起时宽度不再变化。
- 在多行之间共用一个控件作为图标。一个控件只能放在一行中；请为每个项目各准备一个，或改用图形。

### 常见问题

**为什么选择父级不会打开页面？** 父级永远不会被选中，选择它只会展开或收起其页面。请为父级添加一个作为其落地视图的子页面，并把 `SelectedKey` 绑定到该子项的键。

**如何隐藏页面而不是禁用它？** 从其父级或分组的 `Items` 中移除该项目。行会原地更新，重新添加时页面会再次出现。`IsEnabled="False"` 会让页面保持可见但变暗，并移出方向键的顺序。

**当 `SelectedKey` 指向一个不存在的页面时会怎样？** 不会选中任何东西，键保留其值。之后添加该键对应的页面时，侧边栏会选中它。这使得启动时设置的路由可以在导航数据加载完成之前就生效。

**操作行可以被选中吗？** 不可以。`IsAction` 行会触发 `ItemInvoked` 并执行其命令，但 `SelectedItem` 和 `SelectedKey` 保持不变，因此用户当前所在的页面仍保留其标记。

**为什么侧边栏旁边的页面在收起时会卡顿？** 在 `Push` 模式下，宽度弹簧的每一帧都会让页面重新布局。请使用默认的 `ResizeMode="Overlay"`，并把页面的 `RenderTransform` 绑定到 `PageShift`。

**为什么 Cmd+B 或 Ctrl+B 不能收起侧边栏？** 该快捷键只在焦点位于侧边栏内部时生效，即焦点在其某一行或筛选框中。请先点击一行，或将 `IsCollapsed` 绑定到窗口其他位置的切换按钮。

## API 参考

<!-- api: Sidebar -->

<!-- api: SidebarSection -->

<!-- api: SidebarItem -->

## 无障碍

- 侧边栏是导航地标，名称由 `Label` 提供（默认为“Sidebar”）。当页面有多个导航区域时，请使用具体的名称，例如“主导航”。
- 页面构成一棵树。每一行的无障碍名称由其标签、数量，以及带有 `IsUnread` 时的“未读”组成（例如“收件箱，12”），并报告是否被选中；父级还会报告是否已展开。`UnreadText` 可用来本地化这个词。
- 可收起的分组标题是展开按钮，其他标题只是文字，不在 Tab 顺序中。
- 收起按钮和窄栏上的搜索按钮，其无障碍名称分别由 `CollapseButtonText`、`ExpandButtonText` 与 `FilterPlaceholder` 提供。替换它们即可本地化。被隐藏的按钮（收起按钮位于筛选框旁边时，头部中的那个）不会获得焦点。
- 在窄栏上，每个页面都保留其标签作为提示，因此使用指针的用户仍能读到图标的名称。父级的菜单可用 Right 或 Enter 从键盘打开，并将其页面列为单选项。
- 键盘焦点会在行周围显示 2 px 的焦点环。被禁用的页面会变暗，并被方向键跳过。
- 不要只依赖颜色：未读点同时配有更醒目的标签，以及无障碍名称中的“未读”一词，而数量是名称的一部分。

### 键盘

侧边栏遵循 WAI-ARIA 树形模型进行导航。列表只占一个 Tab 停靠点，并从选中的页面开始；若没有选中的页面，则从第一个页面开始。

| 按键 | 操作 |
|---|---|
| `Tab` / `Shift+Tab` | 进入列表，停在已聚焦或已选中的行上，或离开列表 |
| `Up` / `Down` | 移到上一个或下一个可见行；被禁用的页面会被跳过 |
| `Home` / `End` | 移到第一个或最后一个可见行 |
| `Page Up` / `Page Down` | 按大约一个可见页面的行数移动 |
| `Right` | 展开父级，或在其已展开时移入其第一个页面；在窄栏上打开其菜单 |
| `Left` | 收起已展开的父级，或从子页面移到其父级；在可收起分组的页面上，移到其标题 |
| `Enter` / `Space` | 选择页面或操作；展开或收起父级，或折叠可收起的分组标题 |
| 字母键 | 跳到下一个标签以所输入文字开头的可见页面 |
| `Cmd+B`（Windows 和 Linux 上为 `Ctrl+B`） | 焦点位于侧边栏内时，收起或展开侧边栏 |
| 筛选框中的 `Down`、`Enter`、`Escape` | 移到第一个页面；选中剩下的第一个页面；清空筛选 |

## 动效

- 选中区块是一块没有竖条的柔和淡色，它以灵敏的弹簧在各行之间滑动；选中页面的图标会变为强调色。当选中的页面收合在父级内时，区块会停留在父级上。
- 父级和可收起的分组会以平滑的高度弹簧展开或收合，箭头同时以灵敏的弹簧旋转。已经收起并静止的容器会完全退出布局和渲染。新增页面会让所在分组以同一个弹簧增高。
- 收合为窄栏时，宽度以平滑弹簧过渡。每一行的文字（标签、尾部图标、数量、未读点和箭头）一起淡出，图标保持原位，数量和未读标记则变为图标上的圆点。在 `Overlay` 模式下，弹簧只作用于窗格，并且在途中再次切换时会保留速度；在 `Push` 模式下，则从当前宽度重新开始。
- 减弱动效时，宽度和高度会立即变化，箭头立即转动，选中区块也立即移动。各行的文字、头部、筛选框和搜索按钮仍会快速淡入淡出。
- 关闭动效时，所有变化都立即生效。

| 变化 | 完整 | 减弱 | 关闭 |
|---|---|---|---|
| 选中区块移动 | 以灵敏的弹簧在各行之间滑动 | 直接跳到该行 | 直接跳到该行 |
| 父级或可收起分组展开或收起 | 高度以平滑弹簧变化，箭头以灵敏的弹簧转动 | 高度和箭头立即变化 | 立即完成 |
| 分组中新增页面 | 分组以高度弹簧（平滑）增高 | 立即增高 | 立即完成 |
| 侧边栏收起或展开 | 宽度以平滑弹簧变化；在 `Overlay` 模式下只有窗格做弹簧动画 | 宽度立即变化 | 立即完成 |
| 窄栏上各行的文字和分组标题 | 快速淡出（快速） | 快速淡出（快速） | 立即完成 |
| 窄栏上的头部、筛选框和搜索按钮 | 快速淡出（快速） | 快速淡出（快速） | 立即完成 |
| 行的悬停和按下 | 背景与文字颜色交叉渐变（快速） | 立即变化 | 立即完成 |

## 相关组件

- `Drawer` 在窄窗口中承载侧边栏，此时固定的侧边栏会占用过多空间。
- `Tabs` 用于在同一页面的视图之间切换。
- `TreeView` 用于深层的文件夹树。
- `CommandPalette` 用于通过键盘搜索跨页面的内容与操作。
- `UserMenu` 或 `DropdownMenu` 放在头部或底部，用于工作区切换器或账户菜单。

| 组件 | 适用场景 |
|---|---|
| `Tabs` | 两到五个视图共享同一对象，且其头部始终可见 |
| `Breadcrumb` / `Stepper` | 层级中的位置或序列中的步骤就是重点 |
| `TreeView` | 页面或文件的嵌套层级超过一层 |
| `CommandPalette` | 用户按名称搜索页面或操作，而不是浏览 |
| `Drawer` | 窗口太窄，无法容纳固定侧边栏，导航应当滑入 |
| `UserMenu` | 已登录用户需要在头部或底部放置一个账户操作菜单 |
