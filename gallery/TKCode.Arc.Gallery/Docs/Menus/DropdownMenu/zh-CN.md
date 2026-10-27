---
title: 下拉菜单
summary: 锚定在触发器上的聚焦操作列表。
source-hash: b709b001eb3d
---

<Demo name="Actions" />

## 何时使用

下拉菜单把一小组命令收纳在带标签的触发器之后。触发器是有名称的可见控件，人们无需手势就能找到这些操作；选择之后菜单会关闭，页面保持原位。它适用于一个对象或一个视图拥有多个命令、而这些命令又不值得各自占一个按钮的场合。

- **数据表格中的行操作**：在 `Table` 中，发票或客户行的“更多”按钮之后放着“重命名”“复制”“归档”和“删除”。每行一个触发器，破坏性操作放在最后，并用分隔线与其他操作隔开。
- **管理后台页头中的操作**：报表页面的“导出”“分享”和“打印”。页面为次要命令保留一个可见入口，主要操作则以按钮的形式保留在旁边。
- **列表工具栏中的排序与视图选项**：订单列表工具栏中，“最新”“最旧”或“名称”以单选行呈现，“视图选项”以复选行呈现。触发器标签显示当前选择，例如“排序：最新”。
- **一次访问中会切换的显示设置**：“显示侧边栏”“显示缩略图”和“自动换行”以复选行呈现，并保持菜单打开，因此可以一次改变多个选项，而无需重新打开菜单。
- **账单或报表页面中的导出与分享**：“导出 CSV”“导出 PDF”和“发送到财务系统”归为一组，不可逆的操作放在最后。

## 何时不用

- **有默认操作和少数备选项**：用 `SplitButton`。默认操作位于主按钮上，菜单只放不常用的选择，因此主要操作一键即达。
- **针对某段内容的右键操作**：用 `ContextMenu`，它在指针处打开、作用于内容本身，没有可见的触发器。
- **头像背后包含身份与主题的账户菜单**：用 `UserMenu`，它带有个人资料头部、主题切换和退出登录。
- **在表单中从列表选择一个值**：用 `Select` 或 `Combobox`。它们持有值、标签和校验状态，菜单则没有这些。
- **始终可见、需要并排比较的选项**：少量互斥的视图用 `SegmentedControl`，相互独立的选项用 `Checkbox` 组。菜单会在打开前隐藏选项。

## 用法

`DropdownMenu` 派生自 Avalonia 的 `TemplatedControl`。它内置的触发器是 Arc `Button`（Secondary，Small），其菜单行位于它自行创建并持有的 `MenuSurface` 中。控件负责标签变形、箭头、键盘模型和动效；菜单行与其他 Arc 菜单使用相同的 `MenuEntry` 项。直接在行内声明菜单行，并把 `Label` 设为触发器上显示的文字。

直接声明菜单行。命令用 `MenuEntry`，分隔线用 `MenuEntrySeparator`，分组标题用 `MenuEntryLabel`。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:DropdownMenu Label="操作">
        <arc:MenuEntry Header="重命名" />
        <arc:MenuEntry Header="复制" />
        <arc:MenuEntry Header="删除"
                       IsDestructive="True"
                       HasSeparatorBefore="True" />
    </arc:DropdownMenu>
</UserControl>
```

通过代码用 `ArcIcons` 设置图标，它是 24x24 的 Lucide 几何图形。

```csharp
var rename = (MenuEntry)menu.Items[0];
rename.Icon = ArcIcons.Pencil;
```

### 结构

1. **触发器**（内置的 `arc:Button`，或 `Trigger` 中的控件）：高 36 px，即 Secondary 小号按钮。悬停与打开状态会把边框变为强边框颜色；打开状态还会使用弱化的表面色。按下时触发器从不缩放。
2. **前导图标**（`Icon`）：16 px 的图形，使用次要文字颜色。`Icon` 为 null 时隐藏。
3. **标签**（`Label`）：触发器的文字，超出最大宽度时以省略号截断。修改它时触发器会变形：新标签上升出现，触发器宽度则以弹簧方式调整至合适的宽度。
4. **箭头**：15 px，描边宽 1.8 px，使用弱化文字颜色。菜单打开时旋转 180 度。
5. **菜单面板**：宽度至少 192 px（12 rem），与触发器相距 6 px。`Alignment` 决定与哪一边对齐，默认为 `End`。`Side` 决定从哪一边展开：默认为 `Bottom`，也可为 `Top`、`Left` 或 `Right`。空间不足时菜单会翻转到另一侧。
6. **菜单行**（`MenuEntry`）：下拉变体中高 36 px。一行显示图标、标题、末尾可选的快捷键提示，以及切换行的勾选或圆点。
7. **分隔线与分组标签**（`HasSeparatorBefore`、`MenuEntrySeparator`、`MenuEntryLabel`）：行上方的一条线，或一个不可选的标题，用来为其下的行分组。

对于标准的操作组，保留内置触发器。当触发器必须是图标按钮、列表行的“更多”按钮，或页面上已有的控件时，改用 `Trigger`；菜单随即锚定到该控件。

### 选择触发器、方向与对齐

- 当操作组需要一个名称时，使用带文字 `Label` 的内置触发器，例如“操作”或“排序：最新”。可见文字在菜单打开前就告诉键盘用户它的用途。
- 行内的“更多”菜单使用幽灵按钮和省略号图标作为 `Trigger`。`Label` 只为内置触发器命名，因此需要在自定义按钮上设置 `AutomationProperties.Name`，说明它属于哪一行。
- 页面顶部的工具栏保持 `Side` 为 `Bottom`。窗口底部的控件使用 `Top`；只有位于侧边栏边缘的控件才使用 `Right` 或 `Left`。
- 工具栏右侧的触发器保持 `Alignment` 为 `End`，使菜单朝内容方向展开。位于左边缘的触发器使用 `Start`。

### 处理选择

`ItemClicked` 会报告被激活的行。除非该行设置了 `StaysOpenOnClick`，否则事件之后菜单会关闭。

```csharp
menu.ItemClicked += (_, e) => status.Text = $"已选择：{e.Header}";
```

对用途相同的一组行使用同一个处理程序，并读取 `e.Entry` 来区分各行。当某一行有自己的行为时，使用该行自身的 `Click` 事件。生成行背后的数据项位于 `e.Item` 中。

### 实时标签与单选行

通过代码设置 `Label` 可以更改触发器的文字。具有相同 `GroupName` 的单选行互斥，选中其中一行即关闭菜单。设置了 `StaysOpenOnClick="True"` 的复选行（`ToggleType="CheckBox"`）会保持菜单打开，使人能在一次访问中切换多个选项。

<Demo name="LabelAndToggles" />

当恰好一个选项适用时使用单选行，例如排序方式：触发器标签随之说明当前的选择。当每个选项相互独立时使用复选行，例如要显示哪些列。单选组保持在五个左右的选项；更长的选择应放进 `Select`。

### 数据驱动的行与自定义触发器

`ItemsSource` 会为每个条目创建一行。`Trigger` 可用任意控件替换内置按钮。菜单会锚定到该控件，`Button` 点击后即可打开菜单。

<Demo name="CustomTrigger" />

```xml
<arc:DropdownMenu ItemsSource="{Binding FileActions}" Side="Bottom">
    <arc:DropdownMenu.Trigger>
        <Button Content="..." />
    </arc:DropdownMenu.Trigger>
</arc:DropdownMenu>
```

当条目不是 `MenuEntry` 对象时，数据驱动的行可通过 `ItemTemplate` 设置其标题。每行背后的条目通过 `ItemClicked` 中的 `e.Item` 报告，因此处理程序可以针对该条目执行命令。

### 通过代码打开

```csharp
menu.Open(focusFirst: true);
menu.Close();
```

`Open(true)` 会打开菜单并高亮第一行，效果与在触发器上按 Enter 或 ArrowDown 相同。`IsOpen` 是双向的，视图模型可以通过设置它来打开菜单。在 XAML 中设置了 `IsOpen` 而启动即打开的菜单不会获得焦点。

### MenuSurface

`MenuSurface` 是每个 Arc 菜单背后可复用的菜单弹层。可在代码中针对任意锚点将其打开。它的菜单行、键盘模型和动效都与下拉菜单相同，但它本身没有触发器。

<Demo name="MenuSurface" />

当多个控件共用一个菜单时，把 `MenuSurface` 声明为资源，并通过 `Open(anchor)` 传入锚点。单独的弹层不会响应锚点的点击；当控件应自行切换菜单时，用 `DropdownMenu.Menu` 将其附加上去。

### 绑定到视图模型

下拉菜单通常会改变它所在的页面。视图模型持有标签、所选项与命令；菜单负责显示它们，并把所选项回传。在 XAML 中绑定标签与条目，在代码中设置命令。

```csharp
public interface IExportService
{
    Task StartCsvAsync(string filter);
}

public partial class InvoiceListViewModel : ObservableObject
{
    private readonly IExportService _exports;

    public InvoiceListViewModel(IExportService exports) => _exports = exports;

    [ObservableProperty]
    private string _sortLabel = "排序：最新";

    [ObservableProperty]
    private string _filter = "status:open";

    [RelayCommand]
    private async Task ExportCsvAsync()
    {
        // 以当前筛选条件开始导出。
        await _exports.StartCsvAsync(Filter);
    }
}
```

```xml
<arc:DropdownMenu Label="{Binding SortLabel}"
                  Icon="{x:Static arc:ArcIcons.ArrowUpDown}"
                  Alignment="Start">
    <arc:MenuEntryLabel Text="排序方式" />
    <arc:MenuEntry Header="最新" ToggleType="Radio" GroupName="sort" IsChecked="True" />
    <arc:MenuEntry Header="最旧" ToggleType="Radio" GroupName="sort" />
</arc:DropdownMenu>
```

```csharp
menu.Items.Add(new MenuEntry { Header = "导出 CSV", Command = viewModel.ExportCsvCommand });
menu.ItemClicked += (_, e) => viewModel.SortLabel = $"排序：{e.Header}";
```

- `Label` 为单向绑定：视图模型修改它，触发器随之变形。由于单选行的 `IsChecked` 由菜单自己修改，应使用 `ItemClicked` 把所选项写回。
- 行的 `Command` 在该行被激活时执行；当命令无法执行时，该行会被禁用。使用 CommunityToolkit.Mvvm 的 `[RelayCommand]` 及其 `CanExecute` 来禁用不适用的行。
- 在代码中创建的行可在代码中设置 `Command`。在 XAML 中声明的行是菜单的一部分，因此把它们的逻辑保留在 `ItemClicked` 中。

### 焦点与关闭

触发器是菜单的锚点，也是焦点返回的位置。通过键盘打开时，高亮落在一行上；通过指针打开时，则不设高亮。菜单会在按下 Escape 或 Tab（焦点返回触发器）、选中了不保持打开的行、外部按下，以及触发器离开可视树时关闭。

只有行自己声明时，状态会变化的行才会让菜单保持打开。人们会多次切换的复选行应设置 `StaysOpenOnClick="True"`。单选行会关闭菜单，因为选择是最终的。下拉菜单只适合短列表：其行不做虚拟化，因此数百项的列表应使用 `Combobox`，或放在带搜索的页面中。

### 示例：发票列表页页头

发票页页头有一个主要操作、一个可见的“导出”菜单，以及一个在标签中写明当前顺序的“排序”菜单。导出菜单把文件格式归在一起，并把不可逆的操作放在最后，用分隔线与其他操作隔开。导出触发器的文字写明了操作，因此人们在打开前就知道菜单的用途。命令从视图模型执行，标签随状态变化。

```xml
<DockPanel>
    <TextBlock DockPanel.Dock="Left"
               Text="发票"
               FontSize="22"
               FontWeight="SemiBold"
               VerticalAlignment="Center"
               Foreground="{arc:ArcDesignTokenResource Foreground}" />
    <StackPanel Orientation="Horizontal" Spacing="8" HorizontalAlignment="Right">
        <arc:DropdownMenu Label="排序：最新"
                          Icon="{x:Static arc:ArcIcons.ArrowUpDown}"
                          Alignment="End">
            <arc:MenuEntryLabel Text="排序方式" />
            <arc:MenuEntry Header="最新" ToggleType="Radio" GroupName="sort" IsChecked="True" />
            <arc:MenuEntry Header="最旧" ToggleType="Radio" GroupName="sort" />
            <arc:MenuEntry Header="金额" ToggleType="Radio" GroupName="sort" />
        </arc:DropdownMenu>
        <arc:DropdownMenu Label="导出" Icon="{x:Static arc:ArcIcons.Download}">
            <arc:MenuEntry Header="导出 CSV" />
            <arc:MenuEntry Header="导出 PDF" />
            <arc:MenuEntry Header="发送到财务系统" />
            <arc:MenuEntry Header="作废所选发票"
                           Icon="{x:Static arc:ArcIcons.Trash2}"
                           IsDestructive="True"
                           HasSeparatorBefore="True" />
        </arc:DropdownMenu>
    </StackPanel>
</DockPanel>
```

### 文案规范

触发器标签是一个简短的短语，说明菜单做什么，或当前显示什么。每一行是一个动词加其对象，或分组中带标题的一个选项。

- 菜单执行动作时，使用一个动词或名词，例如“导出”或“操作”。标签显示状态时，使用“名称：值”，例如“排序：最新”。
- 菜单行使用句子式大小写，动词在前：写“导出 CSV”，而不是“CSV 导出”。
- 破坏性行要写明对象：“作废所选发票”“删除文件”。
- 触发器标签保持在约 18 个字符以内；触发器是工具栏控件，过长的标签会挤压工具栏。
- 为每组单选行使用 `MenuEntryLabel` 标题，让这一组有名称。

| 不要写 | 改为 |
|---|---|
| 排序 | 排序：最新 |
| 更多选项 | 操作 |
| 导出数据 | 导出 CSV |
| 删除 | 删除文件 |
| 点击切换视图 | 视图 |

### 最佳实践

**推荐**

- 每组保持两到七行。更长的列表需要搜索，而菜单不提供搜索。
- 将破坏性行放在最后，并设置 `HasSeparatorBefore="True"`，在其标题中写明它将删除什么。
- 单选行用于对整个页面生效的一个选择，复选行用于相互独立的选项。
- 让触发器标签与状态保持一致，使菜单打开前页面就显示当前的排序或视图。
- 为自定义触发器用 `AutomationProperties.Name` 设置与其操作相同的名称，因为 `Label` 只为内置触发器命名。

**避免**

- 在下拉菜单中放置表单值。为表单提供值的内容需要 `Select` 或 `Combobox`，它们持有值、标签和校验状态。
- 隐藏唯一的操作入口。主要操作应放在可见的按钮上，因为菜单多了一步。
- 菜单打开时改变行。行在打开时读取；请在触发器打开菜单之前更新列表。
- 用一个菜单承载两个操作。两个操作用两个按钮或 `ButtonGroup` 更易读。
- 仅靠颜色标示破坏性行。它的标题写明了对象，因此不依赖颜色也能理解。

### 常见问题

**为什么我的复选行会关闭菜单？** 除非行设置了 `StaysOpenOnClick="True"`，否则选择之后菜单会关闭。对于人们可能多次切换的每一个复选行，都添加该属性。

**选择之后如何更改触发器文字？** 在 `ItemClicked` 处理程序中设置 `Label`，或将其绑定到视图模型属性。新标签会随变形动效升起，宽度以弹簧方式调整。

**能否通过代码打开菜单？** 可以。调用 `Open()` 会在触发器下方显示菜单，调用 `Open(focusFirst: true)` 则以高亮第一行开始。两者都要等触发器成为窗口的一部分后才有效。

**如何使用自定义控件作为触发器？** 把 `Trigger` 设为任意控件。菜单会锚定到该控件，`Button` 点击后切换菜单。对于页面上已有的控件，改用 `DropdownMenu.Menu` 附加菜单。

**没有空间时，菜单会朝哪一侧打开？** 它会翻转到另一侧。`Side` 是首选的边缘，当窗口没有足够空间时菜单会移动。

## API 参考

<!-- api: DropdownMenu -->

<!-- api: MenuEntry -->

## 无障碍

- 触发器以其 `Label` 播报，并报告菜单是否展开。
- 菜单行具有菜单项角色。输入首字母可按字母快速定位到对应行，禁用的行会被跳过。
- 在触发器上按 Enter、Space 或 ArrowDown 会打开菜单并定位到第一行。Escape 关闭菜单，并将焦点交还触发器。
- 破坏性操作行仅以颜色标示，因此其 `Header` 应写明将被删除的内容。
- 图标与高亮对辅助技术隐藏。
- 为菜单取名时，`Label` 应写明菜单的作用，例如“排序：最新”或“导出”。没有文字的自定义触发器需要自己的 `AutomationProperties.Name`，因为 `Label` 只为内置触发器命名。

### 键盘

| 按键 | 操作 |
|---|---|
| 触发器上的 `Enter`、`Space` 或 `ArrowDown` | 打开菜单，并高亮第一行 |
| 触发器上的 `ArrowUp` | 打开菜单，并高亮最后一行 |
| 在触发器上点击或轻触 | 切换菜单；菜单打开时不高亮任何行 |
| `ArrowDown` / `ArrowUp` | 将高亮移到下一行或上一行；列表循环，并跳过禁用的行 |
| `Home` / `End` | 将高亮移到第一行或最后一行 |
| 字母键 | 输入首字母，移动到文字以所输字母开头的行 |
| 菜单中的 `Enter` / `Space` | 激活高亮的行，然后关闭菜单，除非该行保持打开 |
| `Escape` / `Tab` | 关闭菜单，并将焦点交还触发器 |

## 动效

- 菜单以弹簧从触发器边缘展开，并伴随短暂的淡入。关闭时更快、幅度更小。
- 指针移动时，一个高亮块在行之间滑动；键盘操作时则直接跳转。菜单行以短促的错峰方式依次淡入。
- 新的 `Label` 会带着轻微模糊升起，触发器宽度随之调整。菜单打开时，箭头会旋转。
- 减弱动效时去掉缩放与滑动，仅保留快速淡入。关闭动效时菜单立即显示。

| 变化 | 完整 | 减弱 | 关闭 |
|---|---|---|---|
| 菜单打开 | 菜单从触发器边缘展开，起始偏移 5 px、缩放 0.97，以 580 ms 的弹簧和 160 ms 淡入进行 | 快速淡入，无缩放 | 立即 |
| 菜单关闭 | 更快、更小：130 ms，偏移减半，缩放至 0.985 | 快速淡入 | 立即 |
| 高亮移动 | 指针高亮以灵敏的弹簧滑动；键盘高亮直接跳转 | 高亮直接跳转 | 立即 |
| 行出现 | 每行在 240 ms 内淡入，相隔 35 ms 错峰 | 快速淡入 | 立即 |
| `Label` 变化 | 新标签以 0.3 em 升起并带轻微模糊，触发器宽度以弹簧调整 | 瞬时淡入 | 立即 |
| 箭头 | 随菜单打开以弹簧旋转 180 度 | 立即转向 | 立即 |

## 相关组件

- `ContextMenu`：针对选中对象的次要操作，通过右键打开。
- `SplitButton`：一个操作为默认、菜单提供备选项时使用。
- `ButtonGroup`：命令是一排相关按钮，而非隐藏的列表时使用。
- `UserMenu`：应用顶栏中的账户入口。
- `MenuSurface`：共享的菜单弹层，可将菜单锚定到任意控件。

| 组件 | 适用场景 |
|---|---|
| `ContextMenu` | 操作属于单个对象，应在指针处打开 |
| `SplitButton` | 一个操作为默认，菜单提供备选项 |
| `ButtonGroup` | 命令是一排相关按钮，而非隐藏的列表 |
| `UserMenu` | 菜单是头像背后的账户，包含身份与主题 |
| `Select` / `Combobox` | 菜单选取的值用于表单 |
| `MenuSurface` | 需要在非下拉触发器的控件上显示菜单 |
