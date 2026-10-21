---
title: 筛选工具栏
summary: 让集合筛选条件保持在近处，并便于重置。
source-hash: 8db4320fbe67
---

<Demo name="Filters" />

## 何时使用

`FilterToolbar` 是位于表格或列表上方的一条工具栏，它显示哪些筛选条件正在缩小行的范围，允许人们逐个移除它们，并能一起重置。它以可移除的筛选标签呈现已应用的筛选，提供添加下一个筛选的「添加筛选」菜单，还可包含一个可选的搜索字段以及页面所需的任何操作。这样列表页面就只有一个统一的页头，而不是一组零散的下拉框。

- **订单列表中的分面筛选**：履约团队按状态、承运商和地区筛选订单，看到「状态：已发货」和「地区：EU」以标签呈现，换班时用一个操作即可全部清除。
- **事件与工单队列**：值班视图按严重程度、服务和负责人筛选事件。当有人在审查中分享链接或截图时，筛选标签让当前视图一目了然。
- **发票与账单列表**：财务人员按状态和客户筛选发票，然后通过放在「添加筛选」触发器旁边的操作，导出筛选后的行。
- **客户与 CRM 表格**：搜索框与两三个分面协同工作的场景。搜索缩小文字范围，筛选标签缩小分组，下方的表格显示剩余的内容。
- **窄窗口中的高密度管理控制台**：宽度低于 520 px 时，筛选标签堆叠在操作之上，因此同一个工具栏无需单独布局即可用于侧边面板。

## 何时不用

- **以开关形式呈现的一组固定选项**，例如清单的三种视图（打开、关闭、全部）：用 `ChipGroup`，它会同时显示所有选项，无需菜单。
- **没有分面、只有自由文本搜索**：用 `SearchField`。工具栏的搜索框是为了与筛选条件并列而存在的；没有筛选的页面不会从筛选标签中获得任何好处。
- **需要接受多个值的表单字段**，例如编辑器中任务的负责人：用 `MultiSelect`。筛选工具栏将值应用于列表；表单字段则将值保存到记录中。
- **从长列表中选择一个值**，例如从数千名客户中选择一位：用带有自身搜索的 `Combobox`。「添加筛选」菜单会列出每个选项，因此只适合较短的列表。
- **对表格的行进行排序**：使用 `SortableDataTable` 的列标题。筛选会移除行，排序则决定行的顺序，两者应放在不同的位置。

## 用法

每个筛选都是一个 `FilterChip`，包含 `Id`、`Label` 与 `Value`。「添加筛选」菜单的字段放入 `Fields`。每个字段都有选项，人们在第二步中进行选择。

```xml
<arc:FilterToolbar Label="当前筛选"
                   ActiveFilters="{Binding Filters}"
                   MenuAlignment="Start">
    <arc:FilterToolbar.Fields>
        <arc:FilterField Id="status" Label="状态">
            <arc:FilterField.Options>
                <arc:FilterOption Value="待处理" />
                <arc:FilterOption Value="处理中" />
                <arc:FilterOption Value="已关闭" />
            </arc:FilterField.Options>
        </arc:FilterField>
        <arc:FilterField Id="owner" Label="负责人">
            <arc:FilterField.Options>
                <arc:FilterOption Value="Mina" />
                <arc:FilterOption Value="Theo" />
            </arc:FilterField.Options>
        </arc:FilterField>
    </arc:FilterToolbar.Fields>
</arc:FilterToolbar>
```

`FilterToolbar` 派生自 Avalonia 的 `TemplatedControl`，并作为辅助技术中的一个分组。它的组成部分包括标签区域、搜索框、「添加筛选」触发器、「全部清除」按钮以及任何操作，并排布局在一行中。工具栏将已应用的筛选保存在 `ActiveFilters` 中，该属性为双向绑定，因此你绑定的列表是唯一的数据来源，筛选标签只是它的视图。

### 结构

1. **标签区域**（`ActiveFilters`）：每个已应用的筛选对应一个标签，顺序与列表一致。标签显示名称、中间点与值，例如“状态 · 待处理”。没有 `Value` 的标签只显示名称。
2. **标签移除按钮**：每个标签末端有一个 24 px 的点击区域，内含 14 px 的关闭图标。它的名称是标签的文字，例如“移除 状态：待处理”。标签带有 1 px 边框、弱化的表面色与胶囊形圆角，彼此相距 8 px。
3. **空状态提示**（`EmptyText`）：没有设置任何筛选时，标签区域起始端显示弱化文字颜色的“未应用任何筛选”。最后一个标签移除时，它会升起出现。
4. **搜索框**（`IsSearchVisible`、`SearchText`、`SearchPlaceholder`）：宽 200 px，高度与小尺寸控件相同，带有搜索图标、1 px 边框与控件圆角。获得焦点时绘制较深的边框颜色与焦点环。
5. **添加筛选触发器**（`AddFilterLabel`）：一个加号图标与中等字重的文字，高度至少与小尺寸控件相同。当 `Fields` 至少有一个字段时出现，点击后打开两步菜单。
6. **操作**（`Actions`）：你添加的任何控件，放在「添加筛选」触发器之后，与其他操作相距 12 px。
7. **全部清除**（`ClearAllLabel`）：弱化文字颜色的幽灵按钮。只有在设置了任一筛选时才出现。
8. **框体**：表面色背景、1 px 边框、面板圆角，水平内距 16 px，垂直内距 12 px。标签区域与操作之间相隔 16 px。

宽度低于 520 px（堆叠断点）时，工具栏会堆叠：标签占据第一行，操作移到第二行并靠末端对齐。

「添加筛选」菜单会打开一个宽 256 px（16 rem）的面板，内边距为 6 px。字段与值列表的每一行高 36 px，标题行高 32 px。高度超过 320 px 的值列表会滚动。将 `MenuAlignment` 设为 `Start` 或 `End`，可选择面板与触发器对齐的边缘；没有足够空间时，面板会翻转到另一侧。

### 通过代码设置筛选

将当前生效的筛选创建为 `FilterChip` 列表。每个字段只保留一个标签：添加与已有标签 `Id` 相同的标签时，会替换原有的标签。

```csharp
var toolbar = new FilterToolbar
{
    ActiveFilters = new ObservableCollection<FilterChip>
    {
        new("status", "状态", "待处理"),
        new("owner", "负责人", "Mina")
    }
};
toolbar.AddFilter(new FilterChip("priority", "优先级", "高"));
toolbar.RemoveFilter("owner");
toolbar.ClearFilters();
```

`AddFilter`、`RemoveFilter` 与 `ClearFilters` 是工具栏自身的操作，它们会触发 `FiltersChanged`。当没有标签的 id 与之匹配时，`RemoveFilter` 不执行任何操作；`AddFilter` 添加完全相同的标签时也不执行任何操作。

### 响应变化

`FiltersChanged` 报告变化的内容（`Kind`）、涉及的标签，以及全部已应用的筛选。用它重新加载工具栏所筛选的数据。

```csharp
toolbar.FiltersChanged += (_, e) => Refilter(e.ActiveFilters);
```

`FilterChangeKind` 有四个值：`Added`、`Changed`、`Removed` 与 `Cleared`。当字段获得新值时触发 `Changed`，`Previous` 保存被替换的标签。当变化来自「添加筛选」菜单时，会设置 `Field`。

该事件针对通过工具栏所做的变化触发：筛选标签的移除按钮、全部清除、「添加筛选」菜单以及上述三个操作。当你的代码直接修改 `ActiveFilters` 列表时，它不会触发。筛选标签仍会更新，变化也会被播报，因此对于直接修改列表的情况，应改为订阅列表的 `CollectionChanged`。

### 搜索

`IsSearchVisible` 会在工具栏中添加搜索框。`SearchText` 为双向绑定，因此表格可以对其作出响应。

<Demo name="SearchAndTable" />

```xml
<arc:FilterToolbar IsSearchVisible="True"
                   SearchPlaceholder="搜索问题"
                   SearchText="{Binding Query, Mode=TwoWay}" />
```

`SearchPlaceholder` 设置搜索框为空时显示的提示；默认值为“搜索”。搜索不是筛选标签，因此不会出现在 `ActiveFilters` 中，也不属于 `FiltersChanged` 事件。行的筛选应同时基于 `SearchText` 与筛选标签。

### 额外操作

`Actions` 可将其他控件（例如导出按钮）放在「添加筛选」触发器旁边。

```xml
<arc:FilterToolbar>
    <arc:FilterToolbar.Actions>
        <Button Content="导出" />
    </arc:FilterToolbar.Actions>
</arc:FilterToolbar>
```

### 没有筛选

没有生效的筛选时，空状态提示位于起始端。将 `MenuAlignment` 设为 `Start`，可使菜单与触发器的起始边缘对齐。

<Demo name="NoFilters" />

### 窄宽度

宽度低于 520px 时，工具栏会堆叠：先是筛选标签，然后是靠末端对齐的操作。

<Demo name="Narrow" />

### 单独使用筛选菜单

`FilterMenu` 就是「添加筛选」触发器及其两步面板。在触发器上按 Down 会在第一个字段上打开菜单，Right 进入字段，Left 返回。

<Demo name="FilterMenu" />

`FilterMenu` 也可以脱离工具栏使用。它接受自己的 `Fields`，通过 `ActiveFilters` 显示每个字段已有的值，并在选中某个值时触发 `FilterSelected`。该事件中的标签带有字段的 `Id`，因此你的处理程序可以使用工具栏所用的同一 id 替换规则来应用它。

### 绑定到视图模型

在 MVVM 应用中，视图模型拥有已应用的筛选、搜索文字以及它们所缩小的行。工具栏编辑的是它所获得的列表，因此视图模型始终是唯一的数据来源。使用 CommunityToolkit.Mvvm 时：

```csharp
public partial class InvoicesViewModel : ObservableObject
{
    public InvoicesViewModel()
    {
        ActiveFilters.CollectionChanged += (_, _) => ApplyFilters();
    }

    public ObservableCollection<FilterChip> ActiveFilters { get; } = new();

    [ObservableProperty]
    private string? _searchText;

    partial void OnSearchTextChanged(string? value) => ApplyFilters();

    [RelayCommand]
    private Task ExportAsync() =>
        File.WriteAllLinesAsync("invoices.csv", VisibleInvoices.Select(invoice => invoice.Id));

    private void ApplyFilters()
    {
        // 根据 ActiveFilters 与 SearchText 筛选行，然后刷新 VisibleInvoices。
    }
}
```

```xml
<StackPanel Spacing="16">
    <arc:FilterToolbar ActiveFilters="{Binding ActiveFilters}"
                       IsSearchVisible="True"
                       SearchPlaceholder="搜索发票"
                       SearchText="{Binding SearchText}">
        <arc:FilterToolbar.Fields>
            <arc:FilterField Id="status" Label="状态">
                <arc:FilterOption Value="草稿" />
                <arc:FilterOption Value="已发送" />
                <arc:FilterOption Value="已逾期" />
            </arc:FilterField>
        </arc:FilterToolbar.Fields>
        <arc:FilterToolbar.Actions>
            <arc:Button Content="导出 CSV" Variant="Secondary" Command="{Binding ExportCommand}" />
        </arc:FilterToolbar.Actions>
    </arc:FilterToolbar>
    <arc:SortableDataTable ItemsSource="{Binding VisibleInvoices}" />
</StackPanel>
```

- `ActiveFilters` 与 `SearchText` 都是双向绑定，这也是它们的默认值。在工具栏中移除的标签会更新集合，视图模型通过 `CollectionChanged` 即可得知。
- `ActiveFilters` 也接受只读列表（例如数组）。此时工具栏会用一个新列表替换它并写回，因此当视图模型需要响应每一次变化时，应使用 `ObservableCollection`。
- 工具栏中的 `Fields` 是只读集合，因此应在 XAML 中定义字段，或在代码中通过 `toolbar.Fields.Add(...)` 添加一次。它们不与视图模型绑定。
- 只有当变化需要事件的 `Kind` 或 `Previous` 时，才在代码后置中订阅 `FiltersChanged`。对于单纯的重新筛选，上面的 `CollectionChanged` 处理程序已经足够，因为它还涵盖工具栏不会报告的变化。

### 数据结构与性能

菜单会列出字段的每一个选项。选项不做虚拟化，因此应保持每个值列表简短，大约几十个值。拥有数百个值的字段应使用带有自身搜索的 `Combobox`，工具栏只显示所选的值。

标签保存选项的文字：选项有 `Label` 时取其 `Label`，否则取 `Value`；标签的 `Value` 即为该文字。应当基于标签的值进行筛选，因此要保持文字与查询所用的数据一致。当人们阅读的文字必须与数据不同时，应在视图模型中将标签的 `Id` 与 `Value` 映射到查询。

`FilterOption.Hint` 位于行末，适合显示“打开 4”之类的计数。

### 焦点与关闭

「添加筛选」菜单会保持打开，直到有人选择一个值或按下 Escape。无论是通过 Escape 关闭，还是选中某个值之后关闭，面板关闭时触发器都会重新获得焦点。

移除标签时，焦点会移到下一个标签的移除按钮；如果被移除的是最后一个标签，则移到前一个标签的移除按钮。最后一个标签被移除后，如果「添加筛选」触发器可见，焦点会移到它上面。

### 示例：账单控制台中的发票列表

财务团队审查逾期发票。已应用两个筛选，「添加筛选」触发器已准备好添加下一个分面。因为至少设置了一个筛选，所以「全部清除」会出现。工具栏下方的表格显示同时符合两个标签的行；团队开始新一轮审查时，只需一步即可清除所有标签。

![带有状态与负责人筛选标签以及添加筛选触发器的筛选工具栏](images/InvoiceFilters.png)

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc"
             xmlns:collections="using:System.Collections.ObjectModel">
    <StackPanel Width="600">
        <arc:FilterToolbar>
            <arc:FilterToolbar.ActiveFilters>
                <collections:ObservableCollection x:TypeArguments="arc:FilterChip">
                    <arc:FilterChip Id="status" Label="Status" Value="Overdue" />
                    <arc:FilterChip Id="owner" Label="Owner" Value="Mina" />
                </collections:ObservableCollection>
            </arc:FilterToolbar.ActiveFilters>
            <arc:FilterField Id="status" Label="Status" Icon="{x:Static arc:ArcIcons.CircleDot}">
                <arc:FilterOption Value="Draft" Hint="3" />
                <arc:FilterOption Value="Sent" Hint="18" />
                <arc:FilterOption Value="Overdue" Hint="6" />
            </arc:FilterField>
            <arc:FilterField Id="owner" Label="Owner" Icon="{x:Static arc:ArcIcons.User}">
                <arc:FilterOption Value="Mina" />
                <arc:FilterOption Value="Theo" />
                <arc:FilterOption Value="Ana" />
            </arc:FilterField>
            <arc:FilterField Id="region" Label="Region" Icon="{x:Static arc:ArcIcons.Flag}">
                <arc:FilterOption Value="EU" />
                <arc:FilterOption Value="US" />
                <arc:FilterOption Value="APAC" />
            </arc:FilterField>
        </arc:FilterToolbar>
    </StackPanel>
</UserControl>
```

每个字段只有一个标签，因此选择另一位负责人会替换标签，而不是新增第二个。字段选项带有用于状态计数的 `Hint`，因此菜单在团队选择之前就会显示每种状态下有多少张发票。

### 文案规范

- **标签**（字段名称）：与其所筛选的列对应的简短名词，句子式大小写：“状态”“负责人”“地区”。不要使用动词，也不要重复页面标题。
- **值**：人们在表格中认得的文字：“已逾期”“Mina”“EU”。与数据使用相同的拼写。
- **添加筛选**：保留默认值，或使用描述该操作的动词短语。“添加筛选”是默认值，适用于大多数页面。
- **空状态提示**：用平实的文字说明情况：“未应用任何筛选”，而不是“没有内容可显示”。
- **搜索提示**：说明搜索的对象：“搜索发票”“按名称搜索客户”。

| 不要写 | 改为 |
|---|---|
| 筛选 | 状态 |
| 选择一个值... | 搜索发票 |
| 清除 | 全部清除 |
| 0 个筛选 | 未应用任何筛选 |
| STATUS = OVERDUE | 状态：已逾期 |

### 最佳实践

**推荐**

- 让筛选标签作为过滤行的记录。从 `ActiveFilters` 与 `SearchText` 推导行，使重新加载、保存的视图或分享的链接所显示的结果，与标签所描述的一致。
- 为每个字段设置一个 `Id`，并在各个版本中保持不变。id 决定新值替换哪个标签。
- 当搜索框与筛选标签缩小的是同一批行时，把它们放在同一个工具栏中。人们会将它们视为同一组筛选。
- 保持值列表简短，并把最有用的值放在最前面。

**避免**

- 添加不会改变结果的筛选，例如只有一个选项的字段。标签会留在屏幕上，却不能向人们传达任何信息。
- 在工具栏旁边再添加一个「全部清除」按钮。工具栏在设置了任一筛选时已会显示自己的按钮，两个重置按钮会让人们分不清哪一个会清除行。
- 从后台线程修改 `ActiveFilters`。控件必须在 UI 线程上变化，因此应先用 `Dispatcher.UIThread` 将修改切换到 UI 线程。
- 依赖 `FiltersChanged` 来处理直接修改列表的情况，因为直接修改不会触发它。应改为从列表的 `CollectionChanged` 重新筛选。

### 常见问题

**为什么「添加筛选」触发器不显示？** 只有当 `Fields` 至少有一个字段时才会显示。请在 XAML 中添加字段，或在工具栏显示之前在代码中用 `Fields.Add` 添加。

**如何为已保存的视图预先选择筛选？** 在视图打开时，于 XAML 或视图模型中将 `ActiveFilters` 设为已保存的标签。初始值不会触发任何事件，因此应在同一步骤中对行应用相同的筛选。

**为什么选择第二个值会替换标签，而不是新增一个？** 每个标签的 id 就是字段的 id，工具栏对每个字段只保留一个标签。为已有标签的字段选择一个值会更改该标签，`FiltersChanged` 会以 `Kind` 为 `Changed`、`Previous` 为旧标签的方式报告此变化。

**如何显示没有值的筛选？** 创建不带 `Value` 的标签，例如 `new FilterChip("archived", "Archived")`。标签只显示名称，移除按钮也以名称命名。

**如何在代码中重置所有筛选？** 调用 `ClearFilters()`，它会以 `Kind` 为 `Cleared` 触发 `FiltersChanged`。直接用 `ActiveFilters.Clear()` 清空列表会更新标签，但不触发事件，因此应选择与你的处理程序所需相符的调用方式。

## API 参考

<!-- api: FilterToolbar -->

<!-- api: FilterChip -->

<!-- api: FilterField -->

## 无障碍

- 工具栏默认是一个名为“当前筛选”的分组。设置 `Label` 可为其指定适合页面的名称。
- 每个筛选标签都有一个移除按钮，名称形如“移除 状态：待处理”。移除后，焦点会移到相邻的标签，或「添加筛选」触发器。
- 添加和移除筛选会在状态区域中播报。
- 「添加筛选」菜单是一个带名称的对话框，其选项为可勾选的菜单项。
- 按 Down 或 Up 可从触发器打开菜单。方向键在列表中移动，Right 进入字段的值，Left 返回，输入字符可跳转到对应选项。
- Escape 关闭菜单，并将焦点返回到触发器。

### 键盘

| 按键 | 操作 |
|---|---|
| `Tab` / `Shift+Tab` | 在筛选标签的移除按钮、搜索框、「添加筛选」触发器、操作与「全部清除」之间移动 |
| 标签的移除按钮上的 `Enter` 或 `Space` | 移除该筛选；焦点移到相邻的标签或「添加筛选」触发器 |
| 搜索框中的 `Escape` | 搜索文字不为空时将其清空 |
| 「添加筛选」触发器上的 `Enter`、`Space` 或 `Down` | 在第一个字段上打开菜单 |
| 「添加筛选」触发器上的 `Up` | 在最后一个字段上打开菜单 |
| 菜单中的 `Down` / `Up` | 移到下一行或上一行；列表会循环 |
| 菜单中的 `Home` / `End` | 移到第一行或最后一行 |
| 字段上的 `Right` | 显示该字段的值 |
| 值列表中的 `Left` | 返回字段列表 |
| 值上的 `Enter` 或 `Space` | 应用该值并关闭菜单 |
| 菜单中输入字母 | 跳到标签以所输入字母开头的下一行 |
| 菜单中的 `Escape` | 关闭菜单，并将焦点返回到触发器 |

## 动效

- 标签在自己的位置展开与收起，相邻的标签滑动让出空间。
- 值变化时以柔和的模糊在原位切换。设置了任一筛选时，「全部清除」即会显示。
- 「添加筛选」触发器会展开为菜单面板，字段与值的步骤则朝各自的方向滑入。
- 减弱动效时，变化立即切换，并伴随约 160 ms 的短暂淡入淡出。关闭动效时直接跳到最终状态。

| 变化 | 完整 | 减弱 | 关闭 |
|---|---|---|---|
| 添加筛选 | 其标签在弹性动画中展开出一个位置，之后的标签滑动让出空间 | 约 160 ms 的短暂淡入淡出 | 立即 |
| 移除筛选 | 其位置收起，之后的标签滑动回位 | 约 160 ms 的短暂淡入淡出 | 立即 |
| 值变化 | 值在原位以柔和的模糊切换 | 约 160 ms 的短暂淡入淡出 | 立即 |
| 「全部清除」出现或消失 | 其位置展开或收起 | 约 160 ms 的短暂淡入淡出 | 立即 |
| 菜单打开 | 触发器展开为面板，打开时带有轻微的过冲，关闭时平滑 | 约 160 ms 的短暂淡入淡出 | 立即 |
| 从字段到值列表 | 面板沿行进方向滑动 8 px | 约 160 ms 的短暂淡入淡出 | 立即 |

## 相关组件

- `ChipGroup`：一小组固定的切换项。
- `SearchField`：不带筛选的自由文本搜索。
- `SortableDataTable`：被筛选缩小范围的表格。
- `MultiSelect`：需要接收多个值的表单字段。

| 组件 | 适用场景 |
|---|---|
| `ChipGroup` | 选项是一小组固定的值，人们在其间切换，且全部可见 |
| `SearchField` | 页面只搜索文字，没有需要缩小范围的分面 |
| `SortableDataTable` | 筛选后剩余的行以表格形式显示并可排序 |
| `MultiSelect` | 表单在一条记录上保存多个值 |
| `Combobox` | 必须从需要自身搜索的长列表中选择一个值 |
