---
title: 侧边栏
summary: 工作区导航，包含分组、嵌套页面、数量和滑动的选中标记；可收起为图标窄栏。
source-hash: 8399d3f503f7
---

<Demo name="Preview" />

## 何时使用

- 顶层页面超过几个的应用主导航，例如工作区、仪表盘或管理工具。
- 页面分属不同分组，并带有一级子页面（项目、报表、设置区域）。
- 导航需要一直显示在屏幕上，但需要时可以少占空间：侧边栏会收成 64 像素的图标窄栏。

## 何时不用

- 同一对象的两到五个视图：使用 `Tabs`。
- 序列中的步骤，或层级结构中的路径：使用 `Stepper` 或 `Breadcrumb`。
- 深层的文件夹与文件树：使用 `TreeView`。侧边栏只显示一级嵌套页面。
- 操作而非位置：使用 `DropdownMenu` 或 `CommandPalette`。

## 用法

在侧边栏中放入 `SidebarSection` 元素，并在分组中放入 `SidebarItem` 元素。没有 `Header` 的分组不显示标题，适合作为第一组（首页、收件箱）。为每个页面设置 `Key`，并将 `SelectedKey` 绑定到你的路由。侧边栏的宽度由主题决定（展开时为 260，窄栏时为 64），因此请把它放在 `DockPanel` 或 `Auto` 网格列中，不要设置 `Width`。

```xml
<DockPanel xmlns="https://github.com/avaloniaui"
           xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
           xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc"
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

### 分组与嵌套页面

带有子项的项是父级，父级永远不会被选中：选择它会展开或收起其下的页面，这些页面挂在一条细引导线下。`IsExpanded` 是双向的。若要让用户收起整个分组，请在带有 `Header` 的分组上设置 `IsCollapsible`；该分组的 `IsExpanded` 同样是双向的。只显示一级嵌套。

`Badge` 会在行尾显示一个简短的数量。`IsEnabled="False"` 会让页面变暗，方向键会跳过它。`Icon` 接受 24×24 的 Lucide 图形（例如 `ArcIcons` 中的字形），也可以是任意控件。

### 选中与路由

`SelectedItem` 与 `SelectedKey` 是双向的，并保持同步。在页面出现之前设置 `SelectedKey` 也没有问题：页面出现后，侧边栏会选中它。每次选择页面时都会触发 `ItemInvoked`，即使该页面已被选中；`SelectCommand` 会以该项作为参数执行。每个项还可以各自带有 `Command` 和 `Tag`。

<Demo name="DataBinding" />

```csharp
sidebar.ItemInvoked += (_, e) => router.Navigate(e.Item.Key!);
```

### 图标窄栏

`IsCollapsed` 会将侧边栏收成 64 像素的窄栏，它是双向的。头部区域中的收起按钮、`ToggleCollapsed()` 方法，以及焦点位于侧边栏中时的 Ctrl+B（macOS 上为 ⌘B），都会执行相同的操作。在窄栏上，每个页面右侧都会显示提示，数量会变成图标上的圆点，分组标题变为细线，过滤框变为一个搜索按钮，点击后打开侧边栏；父级则会在菜单中打开其页面。没有图标的页面会显示其首字母。

头部和底部会保持侧边栏展开时的布局，并被裁剪到窄栏宽度，因此位于起始边缘的徽标或头像仍然可见。如预览所示，使用 `:collapsed` 伪类淡出其余内容。收起按钮可见时，窄栏上的头部会让位给它。

<Demo name="Rail" />

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <UserControl.Styles>
        <Style Selector="arc|Sidebar:collapsed TextBlock.rail-fade">
            <Setter Property="Opacity" Value="0" />
        </Style>
    </UserControl.Styles>
    <arc:Sidebar IsCollapsed="True" Height="320" />
</UserControl>
```

### 过滤

`IsFilterVisible` 会在头部下方显示过滤框。名称包含该文本的页面会保留，含有匹配页面的父级会展开，没有匹配项的分组会隐藏。按 Escape 清空过滤框，按 ↓ 移到第一个页面，按 Enter 选择它。`FilterText` 是双向的，`NoMatchesText` 用于替换默认的“No matches”。

### 实时数据

侧边栏会跟随其数据变化：修改项上的 `Badge`、`Label`、`Icon` 或 `IsEnabled`，或增删项和分组，行都会就地更新。

<Demo name="LiveUpdates" />

### 键盘

页面列表只占一个 Tab 停靠点，焦点从选中的页面开始。

| 按键 | 操作 |
|---|---|
| ↑ / ↓ | 移到上一个或下一个可见行 |
| Home / End | 移到第一行或最后一行 |
| → | 展开父级；若已展开，则移入其页面。在窄栏上，打开其菜单 |
| ← | 收起父级；若位于页面上，则移到其父级或分组标题 |
| Enter / Space | 选择页面，或展开、收起父级或分组 |
| 字母键 | 跳到下一个以所输入文字开头的页面 |
| Ctrl+B / ⌘B | 收起或展开侧边栏 |

## API 参考

<!-- api: Sidebar -->

<!-- api: SidebarSection -->

<!-- api: SidebarItem -->

## 无障碍

- 侧边栏是导航地标，名称由 `Label` 提供（默认为“Sidebar”）。
- 页面构成一棵树。每一行的无障碍名称由其名称和数量组成（例如“收件箱，12”），并报告是否被选中；父级还会报告是否已展开。
- 可收起的分组标题是展开按钮，其他标题只是文本。
- 收起按钮和窄栏上的搜索按钮，其无障碍名称分别由 `CollapseButtonText`、`ExpandButtonText` 与 `FilterPlaceholder` 提供。替换它们即可本地化。
- 键盘焦点会显示 2 像素的焦点环。已禁用的页面会变暗，并被跳过。

## 动效

- 选中标记是一个带有强调色竖条的柔和区块，它以迅捷的弹簧在各行之间滑动。当选中的页面收在父级内时，标记会停留在父级上。
- 父级和可收起的分组会以平滑的高度弹簧展开或收起，箭头同时旋转。
- 收起为窄栏时，宽度以平滑弹簧过渡。名称、数量和标题会快速淡出，图标保持原位，数量则变为圆点。
- 动效等级为 Reduced 时，宽度和高度立即变化，选中标记也立即移动。文字仍会淡入淡出。
- 动效等级为 Off 时，所有变化立即生效。

## 相关组件

- `Drawer`：用于在窄窗口中承载侧边栏，固定侧边栏在这种情况下会占用过多空间。
- `Tabs`：在同一页面的视图之间切换。
- `TreeView`：用于深层的文件夹树。
- `CommandPalette`：用于通过键盘搜索页面与操作。
- `UserMenu` 或 `DropdownMenu`：放在头部和底部，用于工作区切换或账户菜单。
