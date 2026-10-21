---
title: 上下文菜单
summary: 将次要操作放在所选对象附近。
source-hash: 118edf57bbce
---

<Demo name="FileTile" />

## 何时使用

上下文菜单把一个对象的次要操作放在该对象旁边。文件卡片、仪表板卡片或表格中的一行负责展示内容，当有人请求时，操作会出现在指针处或目标下方。主要操作仍留在页面的工具栏中；上下文菜单只放置人们对单个项目执行的操作。

- **共享文件夹中的文件与资源**：工作区库中文件卡片上的“复制链接”“重命名”“移到文件夹”和“移到回收站”。管理文件的人很快就会熟悉这种手势，而工具栏则保留给上传和新建文件夹。
- **分析仪表板中的卡片**：在“按地区划分的营收”这类指标卡片上提供“复制图表”“导出 CSV”和“从仪表板移除”。卡片本身不是按钮，因此菜单可以添加操作，而不必在每张卡片上都放一个可见的菜单按钮。
- **账单或订单控制台中的记录**：发票列表的一行上提供“复制发票编号”“重新发送收据”和“作废发票”。该行保留打开记录的点击目标，菜单用于较少见的操作。
- **开发者与日志工具**：JSON 或日志查看器的节点上提供“复制值”“复制路径”和“按此键筛选”。对象即是数据，操作也是针对数据的。
- **简短、扁平且含一个破坏性行的列表**：对同一对象的少量操作，破坏性行放在最后，并以分隔线隔开。

## 何时不用

- **用户必须不靠右键或点按就能找到的操作**：使用带可见触发器的 `DropdownMenu`。通过手势打开的菜单容易被忽略，键盘和屏幕阅读器用户需要一个有标签的控件作为起点。
- **以触屏为主、通过滑动显示操作的列表**：使用 `SwipeActions`，它与手机用户在行上期望的滑动手势一致。
- **目标需要自己的点击**：在其旁边使用 `Button`。对被包裹内容的普通点击或点按也会打开菜单，因此点击后打开记录的卡片不应被包裹在 `ContextMenu` 中。
- **一个默认操作加几个备选项**：使用 `SplitButton`。它让默认操作保持在一次点击之内，并把备选项放入其菜单。
- **为表单提供选择的列表**：使用 `Select`、`Combobox` 或 `RadioGroup`。它们承载值、标签和验证状态，菜单则不具备这些。
- **账户设置与退出登录操作**：使用 `UserMenu`，它位于顶栏的头像之后。

## 用法

`ContextMenu` 派生自 Avalonia 的 `ContentControl`。内容即为目标：任何单个元素都可以，它保留自己的外观和布局。控制项会添加一个菜单表面（在 Context 变体中为 `MenuSurface`）、行集合 `Items`，以及在右键、ContextMenu 键、Shift+F10 和普通点击或点按时打开菜单的连接逻辑。由于它是 `ContentControl`，常用的内容属性（`Content`、`ContentTemplate`、`Padding`）都适用于目标。

用 `ContextMenu` 包裹目标并设置其 `Label`。内容即为目标。在代码中向 `Items` 添加行。下面的代码片段中，`file` 即为该 `ContextMenu`。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:ContextMenu Label="Q3 report.pdf 的操作">
        <Border Width="220" Height="120" Padding="16" CornerRadius="18" BorderThickness="1">
            <TextBlock Text="Q3 report.pdf" />
        </Border>
    </arc:ContextMenu>
</UserControl>
```

```csharp
file.Items.Add(new MenuEntry { Header = "复制链接", Icon = ArcIcons.Copy });
file.Items.Add(new MenuEntry { Header = "删除文件", Icon = ArcIcons.Trash2, IsDestructive = true });
```

同样的行也可以在 XAML 的 `<arc:ContextMenu.Items>` 下声明，本页的演示即是这样编写的。

### 结构

1. **目标**（`Content`）：菜单所属的元素。主题将其背景设为透明并移除焦点装饰，因此菜单是对按下操作的唯一响应。应为目标提供自己可见的形状（边框、背景和圆角），如演示所示，让人们看到自己操作的对象。
2. **菜单面板**（`Menu`）：承载各行的表面，以 `Label` 播报。在 Context 变体中，行高为 34 px，面板宽度至少为 208 px（13 rem），高度永远不超过 300 px；较长的列表在面板内滚动。
3. **图标位**（`MenuEntry` 上的 `Icon`）：24 × 24 的 Lucide 几何图形，绘制为 16 px 描边的图标。没有图标的行，在 `IsChecked` 为 true 时显示对勾，为 false 时显示一个低调的省略号，使每一行保持相同的左边缘。
4. **标签**（`Header`）：行的文字。过长的标签以省略号结尾，而不是换行。`Header` 也是类型匹配（typeahead）所比对的文字。
5. **快捷键提示**（`InputGestureText` 或 `InputGesture`）：行末尾的低调文字。它仅用于显示；应用自己绑定按键。
6. **破坏性行**（`IsDestructive`）：文字和图标采用危险色，被高亮时高亮底色也带危险色调。
7. **分隔线与分组标签**（`HasSeparatorBefore`、`MenuEntrySeparator`、`MenuEntryLabel`）：行上方的分隔线，或将其下方各行归为一组、不可选中的标题。
8. **高亮**：一个共享的高亮覆盖指针或键盘所在的行。各行不绘制自己的悬停填充。

### 处理选择

激活一行会引发该行的 `Click` 事件、执行其 `Command`、引发菜单的 `ItemClicked`，然后关闭菜单，除非该行设置了 `StaysOpenOnClick`。`ItemClicked` 通过 `Entry` 报告被激活的行，通过 `Item` 报告其背后的数据项，并通过 `Header` 报告其标题。菜单自身的 `Opened` 与 `Closed` 事件在打开与关闭动效前后触发。

```csharp
file.ItemClicked += (_, e) => status.Text = $"Q3 report.pdf：{e.Header}";
```

当一个处理程序服务所有行时使用 `ItemClicked`；当某一行需要自己的逻辑时，使用该行的 `Click` 事件。处理程序应保持简短。菜单会在处理程序返回后关闭，因此耗时的工作应放在命令中，或由处理程序启动的异步方法中。

### 勾选行

没有图标的行在 `IsChecked` 为 true 时显示对勾。当该行是开关时，设置 `ToggleType="CheckBox"`：激活时由控件替你翻转 `IsChecked`，行上会显示对勾。下面示例中的行是普通行，由处理程序自行切换。

```csharp
var pinned = new MenuEntry { Header = "已固定", IsChecked = true };
pinned.Click += (_, _) => pinned.IsChecked = !pinned.IsChecked;
image.Items.Add(pinned);
```

二者任选其一，即处理程序或 `ToggleType`，不要同时使用，否则值会翻转两次。当需要在更改多个选项时保持菜单打开，则设置 `StaysOpenOnClick="True"`。

### 数据驱动的行

`ItemsSource` 为每个条目创建一行。`MenuEntry` 实例会被原样使用；其他数据需要 `ItemTemplate`，由它渲染每个生成行的标题。应在位于页面树中的 `ContextMenu` 本身上绑定集合，并在 `ItemClicked` 中读取数据项。

```xml
<arc:ContextMenu Label="文件操作" ItemsSource="{Binding FileActions}">
    <arc:ContextMenu.ItemTemplate>
        <DataTemplate>
            <TextBlock Text="{Binding Name}" />
        </DataTemplate>
    </arc:ContextMenu.ItemTemplate>
    <Border Width="220" Height="96" CornerRadius="18" BorderThickness="1" />
</arc:ContextMenu>
```

### 为任意控件附加菜单

`arc:ContextMenu.Menu` 无需包裹即可为任意控件提供右键菜单。除非设置了 `Variant`，附加的表面会成为 Context 菜单；除非设置了 `ClosesOnViewportChange`，它会在滚动或调整大小时关闭。当元素已有模板，或你不希望改变其父级时，使用这种写法。

<Demo name="Attached" />

```xml
<Border Width="220" Height="96" CornerRadius="18" BorderThickness="1">
    <arc:ContextMenu.Menu>
        <arc:MenuSurface>
            <arc:MenuEntry Header="新建文件夹" Icon="{x:Static arc:ArcIcons.Folder}" />
            <arc:MenuEntry Header="粘贴" Icon="{x:Static arc:ArcIcons.Copy}" InputGestureText="⌘V" />
            <arc:MenuEntry Header="刷新" Icon="{x:Static arc:ArcIcons.RefreshCw}" />
        </arc:MenuSurface>
    </arc:ContextMenu.Menu>
    <TextBlock Text="在此处右键单击" HorizontalAlignment="Center" VerticalAlignment="Center" />
</Border>
```

### 通过代码打开和关闭

`Open` 在目标下方显示菜单。传入目标坐标系中的一个点，可在该位置打开菜单，例如在画布中用户点击的位置。`Close` 关闭菜单，且不移动焦点。`IsOpen` 是双向的，因此视图模型可以通过设置它来打开菜单。

```csharp
file.Open(); // 在目标下方
file.Open(new Point(120, 40), MenuOpenFocus.First); // 在某点，并高亮第一行
file.Close();
```

只有当目标已附加到窗口后，`IsOpen` 才会打开菜单。在此之前将其设为 `True` 不会产生可见效果，因此应在页面加载后再设置。

### 绑定到视图模型

上下文菜单通常作用于集合中的某个项，因此视图模型拥有项与命令，处理程序把被激活的行转交给命令。菜单负责显示项；执行则由视图模型完成。

```csharp
public sealed record FileAction(string Name, bool IsDestructive);

public partial class FileListViewModel : ObservableObject
{
    public ObservableCollection<FileAction> FileActions { get; } =
    [
        new FileAction("复制链接", IsDestructive: false),
        new FileAction("移到回收站", IsDestructive: true)
    ];

    [RelayCommand]
    private void RunFileAction(FileAction action)
    {
        // 对此菜单所属的文件执行该操作。
    }
}
```

```csharp
file.ItemClicked += (_, e) =>
{
    if (e.Item is FileAction action)
    {
        viewModel.RunFileActionCommand.Execute(action);
    }
};
```

在 XAML 中绑定 `ItemsSource`（见数据驱动的行），并如上所示通过 `ItemClicked` 转交被激活的项。对于需要命令的内联声明行，应在代码中创建 `MenuEntry` 时设置其 `Command`。视图模型使用 CommunityToolkit.Mvvm 的 `ObservableObject` 和 `[RelayCommand]`，且不持有任何 UI 类型。

### 焦点与关闭

菜单在人们操作的位置打开。右键在指针处打开菜单。点击或点按目标，或在已获得焦点的目标上按 ContextMenu 键或 Shift+F10，则会在目标下方打开菜单，距其左边缘 12 px、距其下边缘 8 px。通过指针打开时，焦点位于菜单表面上，且没有高亮行；通过键盘打开时，高亮会落在第一行上。

菜单会关闭而不是跟随。它在外部按下、任何祖先滚动视图的滚动、窗口大小调整，以及目标离开可视树时关闭。菜单打开期间在目标上的主按钮按下会将其关闭，随后的点击不会再次打开它。按 Escape 或 Tab 会关闭菜单并将焦点交还目标。当菜单靠近窗口边缘时，Avalonia 的定位会将其翻转到另一侧。

### 示例：收入仪表板卡片

仪表板卡片带有四个操作，破坏性操作位于最后，并由分隔线隔开。卡片保留自己的边框和标题，因此菜单仍属于该对象，页面的工具栏则保留页面级操作。标签写明图表名称，因为屏幕阅读器只会朗读目标的名称。“固定到主页”行是一个切换项，因此它的勾选状态会向辅助技术播报。快捷键提示仅用于显示；应用必须自行绑定该按键。

```xml
<arc:ContextMenu Label="Revenue by region 的操作">
    <arc:ContextMenu.Items>
        <arc:MenuEntry Header="复制图表"
                       Icon="{x:Static arc:ArcIcons.Copy}"
                       InputGestureText="⌘D" />
        <arc:MenuEntry Header="导出 CSV" Icon="{x:Static arc:ArcIcons.Download}" />
        <arc:MenuEntry Header="固定到主页"
                       ToggleType="CheckBox"
                       IsChecked="True" />
        <arc:MenuEntry Header="从仪表板移除"
                       Icon="{x:Static arc:ArcIcons.Trash2}"
                       IsDestructive="True"
                       HasSeparatorBefore="True" />
    </arc:ContextMenu.Items>
    <Border Width="300"
            Padding="16"
            CornerRadius="16"
            BorderThickness="1"
            Background="{arc:ArcDesignTokenResource SurfaceMuted}"
            BorderBrush="{arc:ArcDesignTokenResource BorderSubtle}">
        <StackPanel Spacing="6">
            <TextBlock Text="按地区划分的营收"
                       FontSize="14"
                       FontWeight="Medium"
                       Foreground="{arc:ArcDesignTokenResource Foreground}" />
            <TextBlock Text="$1.84M"
                       FontSize="26"
                       FontWeight="SemiBold"
                       Foreground="{arc:ArcDesignTokenResource Foreground}" />
            <TextBlock Text="较 2026 年第二季度增长 12.6%"
                       FontSize="12"
                       Foreground="{arc:ArcDesignTokenResource TextMuted}" />
        </StackPanel>
    </Border>
</arc:ContextMenu>
```

### 文案规范

各行都是简短的命令，菜单只需一眼就能读懂，因此每一行都应写成“动词加对象”。`Label` 应保持具体，因为它是屏幕阅读器给目标的唯一名称。

- 行的标签采用句子式大小写，动词在前：“复制链接”，而不是“链接”。
- 破坏性行中要写明对象，让人知道会删除什么：文件用“移到回收站”，卡片用“从仪表板移除”。
- 每行控制在约 24 个字符以内。较长的标签会以省略号结尾，因此重要的词应放在前面。
- 行末不要加冒号或句号，标签文字中也不要放快捷键；快捷键应使用 `InputGestureText`。
- 动作使用产品自身的用词，例如产品会作废发票时，就用“作废发票”。

| 不要写 | 改为 |
|---|---|
| 链接 | 复制链接 |
| 删除 | 删除文件 |
| 操作 | Q3 report.pdf 的操作 |
| 移除 | 移到回收站 |
| 点击此处导出 | 导出 CSV |
| 复制（Ctrl+D） | 复制图表，快捷键放在 `InputGestureText` 中 |

### 最佳实践

**推荐**

- 将列表保持在三到七行。更长的列表应放在带可见触发器的 `DropdownMenu` 中，或放在页面上。
- 为目标提供可见的形状，例如边框、背景或圆角。目标本身没有轮廓，因此菜单是唯一的反馈，人们需要看到自己操作的对象。
- 将破坏性行放在最后，并设置 `HasSeparatorBefore="True"`，避免误触。
- 同时在可见的控件上提供重要操作。右键、点按目标和 Shift+F10 都不易被发现，触屏和键盘用户需要一个有标签的入口。
- 用 `Label` 以对象命名目标，让屏幕阅读器播报这些操作所属的对象。

**避免**

- 包裹一个需要自己点击的目标。普通点击或点按也会打开菜单，目标的操作与菜单会争抢同一次按下。当目标只需要右键菜单时，将 `OpensOnClick` 设为 `False`。
- 依赖换行的长标签。行会把长标签以省略号截断，因此含义可能被截掉。
- 仅用颜色标示破坏性行。该行只靠危险色标示，因此其 `Header` 必须写明将被移除的内容。
- 期望菜单在页面滚动时跟随目标。它会在滚动时关闭，人们需要重新打开。
- 把通往某个操作的唯一路径放在菜单里。对最重要的操作，应保留一个可见的入口。

### 常见问题

**如何阻止左键点击打开菜单？** `OpensOnClick` 控制普通点击与点按。在 `ContextMenu` 上将其设为 `False`，即可保留右键、ContextMenu 键和 Shift+F10。默认值为 `True`。

**Avalonia 也有一个 `ContextMenu` 类型，如何区分？** 两个命名空间都定义了名为 `ContextMenu` 的类型。当文件同时使用 `Avalonia.Controls` 和 `TKCode.Arc` 时，请添加别名，例如 `using ArcContextMenu = TKCode.Arc.ContextMenu;`，并在代码中使用该别名。在 XAML 中，`arc:` 前缀已经指定了 Arc 类型。

**如何在不包裹控件的情况下为它添加菜单？** 在该控件上将 `arc:ContextMenu.Menu` 设为一个 `MenuSurface`（见为任意控件附加菜单）。除非你自己设置其变体，否则该表面采用 Context 变体。

**能否在用户点击的位置打开菜单？** 可以。在目标坐标系中传入一个点调用 `Open`，例如在指针处理程序中调用 `file.Open(e.GetPosition(file))`。不传点时，菜单在目标下方打开。

**为什么只有一行显示对勾，其他行没有？** 没有图标的行在 `IsChecked` 为 true 时显示对勾，为 false 时显示省略号，因此各行保持同一左边缘。有图标的行则显示图标。若某行的状态应在激活时翻转，请在该行上设置 `ToggleType="CheckBox"`。

## API 参考

<!-- api: ContextMenu -->

<!-- api: MenuEntry -->

## 无障碍

- 目标可以获得焦点，菜单以 `Label` 播报，该标签同时为目标与菜单命名。
- 目标暴露一个可展开和收起的菜单，因此辅助技术能报告菜单是否打开。
- 菜单行具有菜单项角色。Enter 与 Space 选择一行，Escape 关闭菜单并将焦点交还目标。
- Shift+F10 与 ContextMenu 键可在已获得焦点的目标下方打开菜单，并高亮第一行。
- 没有图标的已勾选行会显示对勾，但勾选状态本身不会被播报。使用 `ToggleType="CheckBox"` 或 `ToggleType="Radio"` 的行会向辅助技术公开其状态，因此状态重要的行应使用切换类型，或把状态写进 `Header`。
- 破坏性行只以颜色标示，因此应在 `Header` 中写明将被移除的内容。
- `Label` 应针对对象写得具体，例如“Q3 report.pdf 的操作”。它是屏幕阅读器给目标的唯一名称。

### 键盘

已获得焦点的目标可以通过键盘打开菜单。菜单打开后，各行遵循菜单的键盘模型。

| 按键 | 操作 |
|---|---|
| ContextMenu 键、`Shift+F10` | 在已获得焦点的目标下方打开菜单，并高亮第一行 |
| `ArrowDown` / `ArrowUp` | 将高亮移到下一行或上一行；列表循环，并跳过禁用的行 |
| `Home` / `End` | 将高亮移到第一行或最后一行 |
| 字母键 | 类型匹配会移到文字以所输入字母开头的行 |
| `Enter` / `Space` | 激活高亮的行，然后关闭菜单，除非该行保持打开 |
| `Escape` / `Tab` | 关闭菜单并将焦点交还目标 |

## 动效

- 右键时，菜单以快速弹簧从指针处展开，并伴随短暂淡入。菜单行以短促的错峰方式依次淡入。
- 点击、点按或 Shift+F10 会在目标下方以相同的动效打开菜单。
- 滚动或调整大小会关闭菜单，菜单不会随目标移动。
- 减弱动效时去掉缩放与滑动，仅保留快速淡入。关闭动效时，菜单立即显示或隐藏。

| 变化 | 完整 | 减弱 | 关闭 |
|---|---|---|---|
| 右键打开 | 菜单从指针处展开，以灵敏的弹簧从 0.96 放大到完整尺寸，并伴随 160 ms 淡入 | 快速淡入，无缩放 | 立即完成 |
| 点击、点按或 Shift+F10 打开 | 菜单从目标下方的点展开，使用相同的弹簧与淡入 | 快速淡入 | 立即完成 |
| 菜单关闭 | 菜单缩放到 0.98 并在 120 ms 内淡出 | 快速淡入 | 立即完成 |
| 高亮移动 | 指针高亮以灵敏的弹簧滑动；键盘高亮直接跳转 | 高亮直接跳转 | 立即完成 |
| 行出现 | 每行在 240 ms 内淡入，前五行之间错开 35 ms | 快速淡入 | 立即完成 |
| 行被按下 | 该行着色，不缩放 | 该行着色 | 立即完成 |

## 相关组件

- 操作需要用户能找到的可见触发器时，使用 `DropdownMenu`。
- 以触屏为主、通过滑动显示操作的行，使用 `SwipeActions`。
- 一个操作为默认、菜单承载备选项时，使用 `SplitButton`。
- `MenuSurface` 是共享的菜单弹层，用于将菜单锚定到任意控件。

| 组件 | 适用场景 |
|---|---|
| `DropdownMenu` | 操作需要一个人们能看到并找到的、带标签的触发器 |
| `SwipeActions` | 以触屏为主的行，通过滑动显示其操作 |
| `SplitButton` | 一个操作为默认，菜单承载备选项 |
| `UserMenu` | 菜单属于顶栏中头像背后的账户 |
| `Dialog` | 破坏性行在移除任何内容之前必须先经过确认 |
| `MenuSurface` | 需要为控件提供菜单，但不使用右键手势，或从代码打开 |
