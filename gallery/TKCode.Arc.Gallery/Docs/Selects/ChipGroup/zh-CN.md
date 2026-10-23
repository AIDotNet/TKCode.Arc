---
title: 标签组
summary: 用几个会随选择而变形的标签进行筛选。
source-hash: 66ee08705340
---

<Demo name="FilterChips" />

## 何时使用

`ChipGroup` 以切换标签的形式展示一小组选项，所有标签同时可见，人们无需打开菜单就能看到有哪些可选项、哪些已启用。它适合放在列表顶部的筛选栏和维度选择器中：选项已知、数量少且经常变化。每个标签都是带按下状态的按钮，整组只保留一个 Tab 停靠点，因此一排标签在键盘顺序中只占一个位置。

- **支持收件箱筛选**：帮助台的工单列表按主题（账单、退款、登录、API、Webhooks）筛选。客服每班要多次切换主题，所有主题都保持可见，无需打开菜单就能知道有哪些选项。
- **电商后台的订单状态**：订单表格中单选“打开”“已打包”“已发货”或“已退回”。设置 `IsMultiple="False"` 后，再次点击会清除筛选，这与人们退出筛选的习惯一致。
- **分析仪表板的维度**：图表上方放置地区或渠道，例如 EMEA、北美、付费搜索或邮件。标签排在它们所影响的组件上方的同一行，所选结果就是视图模型在重新加载数据时读取的状态。
- **需要选择几项兴趣的引导步骤**：在工作区设置页中，团队选择将会使用的领域（财务、销售、支持）。简短且可见的选项比下拉菜单更易扫读。
- **报表页中的长维度列表**：`MaxVisibleItems` 保留前几个标签，把其余标签折叠在“+N 更多”标签之后，因此包含二十条产品线的页面不会变得很高，已选的产品线也保持可见。

## 何时不用

- **选中的值必须放进紧凑字段或工具栏位置**：用 `MultiSelect`，它在触发器中显示所选值，并打开列表。
- **表单中每个选项都是“是”或“否”的回答**：用 `Checkbox`，它为每个选项提供标签、状态和校验消息。
- **从几个视图中选择一个**，例如表格、看板或日历：用 `SegmentedControl`，它更像模式切换而非筛选。
- **表单中从长列表或可搜索列表中选择单个值**：列表较短时用 `Select`；人们需要输入才能找到值时用 `Combobox`。
- **人们自行输入的值**：用 `TagInput`，它接受新文字作为标签。标签组只能选择已存在的选项。

## 用法

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:ChipGroup Label="主题" MaxWidth="560">
        <arc:SelectItem Content="设计" />
        <arc:SelectItem Content="工程" />
        <arc:SelectItem Content="研究" />
        <arc:SelectItem Content="市场" />
        <arc:SelectItem Content="销售" />
    </arc:ChipGroup>
</UserControl>
```

`ChipGroup` 派生自 Avalonia 的 `SelectingItemsControl`，即 `ListBox` 的基类。选项来源、`Items`、`SelectedItem`、`SelectedIndex`、`Selection` 以及 `SelectionChanged` 事件的行为与列表框一致。在此基础上，该控件把标签排布在可换行的面板中，选中时让标签变形，并把过长的选项集折叠在“+N 更多”标签之后。`SelectItem` 提供每个选项的内容与值；设置 `ItemsSource` 后，任何对象都可以作为选项。

### 结构

1. **组标签**（`Label`）：该组的无障碍名称，例如“主题”。控件本身不绘制它，因此请在组上方放置一个可见的标题。
2. **标签**：胶囊形，圆角为高度的一半。高度为 Arc 小尺寸控件的 36 px。文字使用小号字体尺寸（`TypeSM`）、中等字重（500），每侧内边距 14 px（`ChipInlinePadding`）。填充为表面色，边框为 1 px 的边框色，文字为次要文字色。
3. **选中状态**：填充色将 11% 的强调色混入表面色，边框将 42% 的强调色混入边框色。两者都在 OKLab 中计算，因此色调会跟随强调色。悬停时混合比例升至 16% 和 60%。选中状态始终带有勾选标记，因此颜色不是唯一的信号。
4. **勾选标记**（选中的标签）：14 px 的勾，颜色为强调色的加深色。它的占位为 18 px，选中时标签让出这部分空间。
5. **更多标签**（`+N 更多`）：使用弱化的表面色和弱化的文字色，高度与标签相同。显示折叠项时，它读作“收起”。
6. **间距**：标签之间以及行之间为 8 px（`ChipGap`）。
7. **焦点环**：2 px 的环，绘制在获得焦点的标签外侧 3 px 处，颜色为焦点环色。组本身不绘制焦点装饰。

### 选择模式

`IsMultiple` 默认为 `True`，因此每个标签各自切换，所选值保持选项的顺序，而不是点击的先后顺序。设置 `IsMultiple="False"` 可进行单选。点击另一个标签会替换选择，点击已选中的标签则清除选择，因此整组最终可以没有任何选择。该组没有禁止清除的设置；如果表单需要一个值，请在提交时校验。

<Demo name="SingleChoice" />

```xml
<arc:ChipGroup Label="状态" IsMultiple="False">
    <arc:SelectItem Content="打开" Value="open" />
    <arc:SelectItem Content="处理中" Value="progress" />
    <arc:SelectItem Content="已关闭" Value="closed" />
</arc:ChipGroup>
```

当标签是同一事物的互斥状态时，例如工单状态或报表周期，选择单选模式。对于可以组合的维度，例如主题或地区（“账单和登录”是有效的筛选），保持多选模式。

### 折叠长列表

`MaxVisibleItems` 显示指定数量的标签，其余标签折叠在“+N 更多”标签之后。默认不限制数量。两条规则使折叠行为可预期：

- 已选中的标签即使超出限制也会保持可见，因此生效的筛选不会被隐藏。在列表展开时选中的标签，在列表再次折叠后也保持可见。
- `IsExpanded` 为双向绑定。点击更多标签会切换它，列表展开时该标签显示“收起”。

<Demo name="Folded" />

```xml
<arc:ChipGroup Label="主题"
               MaxVisibleItems="5"
               MaxWidth="560"
               IsExpanded="{Binding ShowAllTopics, Mode=TwoWay}"
               ItemsSource="{Binding Topics}" />
```

选择一个能让第一行标签在该组宽度下保持有用的上限。桌面页面的筛选栏适合五到七个标签，较窄的侧边面板则适合更少。把最常用的选项放在前面，因为折叠的选项正是人们需要展开才能找到的。

### 绑定到视图模型

使用 CommunityToolkit.Mvvm 时，选项和选择都由视图模型持有，组只负责显示它们。将选项绑定到 `ObservableCollection<T>`，将所选项绑定到第二个集合，并在视图模型而非视图中响应变化。

```csharp
public partial class TicketFilterViewModel : ObservableObject
{
    public ObservableCollection<string> Topics { get; } = new()
    {
        "账单", "退款", "登录", "API", "Webhook", "发票", "SSO"
    };

    public ObservableCollection<string> ChosenTopics { get; } = new();

    [ObservableProperty]
    private bool _showAllTopics;

    [ObservableProperty]
    private string? _status = "处理中";

    [RelayCommand]
    private async Task ApplyFiltersAsync()
    {
        await _tickets.QueryAsync(ChosenTopics.ToArray(), Status);
    }
}
```

```xml
<StackPanel Spacing="16">
    <arc:ChipGroup Label="主题"
                   MaxVisibleItems="5"
                   ItemsSource="{Binding Topics}"
                   SelectedItems="{Binding ChosenTopics}"
                   IsExpanded="{Binding ShowAllTopics, Mode=TwoWay}" />
    <arc:ChipGroup Label="状态" IsMultiple="False">
        <arc:SelectItem Content="打开" Value="open" />
        <arc:SelectItem Content="处理中" Value="progress" />
        <arc:SelectItem Content="已关闭" Value="closed" />
    </arc:ChipGroup>
    <arc:Button Content="应用筛选" Command="{Binding ApplyFiltersCommand}" />
</StackPanel>
```

- `SelectedItems` 为双向绑定。点击标签会把对应项加入或移出 `ChosenTopics`；视图模型加入集合的项会被选中。请使用与 `ItemsSource` 相同值的集合，使两个列表按值匹配。
- `IsExpanded` 为双向绑定，因此视图模型可以展开折叠的标签，例如搜索命中其中某一个时。
- `SelectedItem` 是单选模式下的单个选择。当视图模型需要设置它时，请用 `Mode=TwoWay` 绑定。
- `SelectedValues` 为只读，按选项顺序列出每个所选选项的 `Value`。请在处理程序中或命令运行时读取它，而不要把它作为绑定源。
- 若要响应每一次变化，请在代码中处理 `SelectionChanged`，或在命令运行时查询视图模型。

对于对象而非字符串的选项，请在代码中将 `TextSelector` 设为标签，将 `ValueSelector` 设为值。两者都是强类型委托，因此无需反射，也适用于裁剪。

```csharp
topicsGroup.TextSelector = item => ((Topic)item!).Name;
topicsGroup.ValueSelector = item => ((Topic)item!).Id;
```

### 数据结构与性能

每个标签都是有自己文字布局和动效的控件，因此该组是为简短的维度列表设计的，而不是为目录设计的。选项数量应控制在一眼能看完的范围内；列表较长时，用 `MaxVisibleItems` 保持第一行简短。对于包含大量值的目录，请使用带搜索的 `MultiSelect` 或 `Combobox`。

标签行会在选项或宽度变化时重新排布，每个标签都会以弹簧效果滑到新位置。标签在重排时会移动，因此请避免在人们进行选择时修改选项列表。

### 示例：支持收件箱筛选栏

工单列表上方的筛选栏。主题为多选，超过五个标签后折叠；状态为单选，初始选中“处理中”；底部说明结果数量，使人们在应用筛选之前就能看到其效果。标签使用与工单字段一致的简短名词，折叠后的集合仍会保持当前选中的“发票”标签可见，即使它超过了上限。

![支持收件箱筛选栏，包含主题标签、折叠的溢出项和单选的状态](images/SupportFilters.png)

```xml
<StackPanel xmlns="https://github.com/avaloniaui"
            xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
            xmlns:arc="https://github.com/AIDotNet/TKCode.Arc"
            xmlns:generic="using:System.Collections.Generic"
            Width="680"
            Spacing="20">
    <TextBlock Text="Support tickets"
               FontSize="18"
               FontWeight="SemiBold"
               Foreground="{arc:ArcDesignTokenResource Foreground}" />
    <StackPanel Spacing="8">
        <TextBlock Text="Topic"
                   FontSize="13"
                   FontWeight="Medium"
                   Foreground="{arc:ArcDesignTokenResource TextSecondary}" />
        <arc:ChipGroup Label="Topics" MaxVisibleItems="5">
            <arc:ChipGroup.ItemsSource>
                <generic:List x:TypeArguments="x:String">
                    <x:String>Billing</x:String>
                    <x:String>Refunds</x:String>
                    <x:String>Login</x:String>
                    <x:String>API</x:String>
                    <x:String>Webhooks</x:String>
                    <x:String>Invoices</x:String>
                    <x:String>SSO</x:String>
                </generic:List>
            </arc:ChipGroup.ItemsSource>
            <arc:ChipGroup.SelectedItems>
                <generic:List x:TypeArguments="x:String">
                    <x:String>Billing</x:String>
                    <x:String>Invoices</x:String>
                </generic:List>
            </arc:ChipGroup.SelectedItems>
        </arc:ChipGroup>
    </StackPanel>
    <StackPanel Spacing="8">
        <TextBlock Text="Status"
                   FontSize="13"
                   FontWeight="Medium"
                   Foreground="{arc:ArcDesignTokenResource TextSecondary}" />
        <arc:ChipGroup Label="Status" IsMultiple="False" SelectedIndex="1">
            <arc:SelectItem Content="Open" Value="open" />
            <arc:SelectItem Content="In progress" Value="progress" />
            <arc:SelectItem Content="Closed" Value="closed" />
        </arc:ChipGroup>
    </StackPanel>
    <Grid ColumnDefinitions="*,Auto">
        <TextBlock Text="Showing 42 of 318 tickets"
                   VerticalAlignment="Center"
                   FontSize="13"
                   Foreground="{arc:ArcDesignTokenResource TextMuted}" />
        <StackPanel Grid.Column="1" Orientation="Horizontal" Spacing="8">
            <arc:Button Content="Clear filters" Variant="Secondary" />
            <arc:Button Content="Apply filters" />
        </StackPanel>
    </Grid>
</StackPanel>
```

### 文案规范

- **选项标签**：句首大写的简短名词或名词短语，例如“账单”“已打包”“处理中”。使用筛选所针对字段的用词，让标签与列标题一致。
- **组标签**：在组上方可见标题中显示的维度名称，例如“主题”或“状态”。`Label` 的值会为辅助技术重复这一名称，因此两者应保持一致。
- **计数与结果**：在底部说明效果，例如“显示 318 张工单中的 42 张”，数字用阿拉伯数字书写。
- **按钮**：使用动词加对象，例如“应用筛选”和“清除筛选”，而不是“确定”。

| 不要写 | 改为 |
|---|---|
| 筛选选项 | 主题 |
| 请选择你的主题 | 账单、退款 |
| 点击筛选 | 应用筛选 |
| 杂项 | 其他 |
| 所有内容 | 所有主题 |

### 最佳实践

**推荐**

- 在每个组上方使用可见标题，并保持 `Label` 与之相同，使屏幕阅读器与看得见的用户为同一个维度命名。
- 对可组合的维度保持 `IsMultiple` 为 true，只在互斥的状态中使用单选模式。
- 对较长的维度列表设置 `MaxVisibleItems`，并把最常用的选项排在前面。
- 通过命令或清除操作应用筛选，并显示结果数量，使标签不会让人觉得它们自行改变了数据。

**避免**

- 用标签表示人们输入或创建的值。该组只选择已有选项；新标签请使用 `TagInput`。
- 只依赖第二次点击作为清除筛选的唯一方式。单选时可以这样清除，但可见的“清除筛选”按钮更容易找到，且还能清除多选的结果。
- 没有折叠的长选项列表。每个标签都是带有自身动效的控件，因此很长的目录会让筛选栏变得沉重、难以扫读。
- 在表单中用标签回答“是”或“否”的问题。`Checkbox` 带有表单所需的标签、状态与校验。
- 在运行时重新排列选项。标签会随指针移动，因此人们在阅读一个不断变化的列表时会失去位置。

### 常见问题

**为什么该组不显示标题？** 控件只绘制标签和“+N 更多”标签。`Label` 是无障碍名称，因此请像示例那样，在组上方单独放置一个 `TextBlock` 作为标题。

**能否阻止人们清除单个选择？** 不能。按照设计，该组在第二次点击时会清除选择。如果表单需要一个值，请在提交时校验，并在该组旁边显示消息。

**列表折叠时，已选中的标签会被隐藏吗？** 不会。折叠时已选中的标签保持可见，列表展开时选中的标签也是如此。只有未选中的溢出项会被折叠。

**如何在代码中选择标签？** 使用 `Selection.Select(index)` 和 `Selection.Deselect(index)`，或使用与点击效果相同的 `Toggle(index)`。`Toggle` 会忽略超出范围的索引和已禁用的选项。若要一次选择多个值，请把它们加入 `SelectedItems` 列表。

**如何在 XAML 中预选标签？** 单选模式下使用 `SelectedIndex` 选择一个标签，如上面的折叠列表示例所示。对于多个标签，请把 `SelectedItems` 绑定或设置为与 `ItemsSource` 相同值的列表，如截图所示。

## API 参考

<!-- api: ChipGroup -->

<!-- api: SelectItem -->

## 无障碍

- `Label` 是该组的无障碍名称。该组以具名分组的形式对外暴露；未设置其他名称时，名称取自 `Label`。
- 每个标签都是带切换状态的按钮。屏幕阅读器会将标签播报为开或关，因此选择状态不只靠颜色传达。
- “+N 更多”标签是可展开的控件。它会报告折叠的标签是否已展开，并可通过自动化的展开与收起操作进行展开和收起。
- 该组只保留一个 Tab 停靠点，因此 Tab 进入该组一次、离开该组一次。停靠点为最后一个获得过焦点的标签；若没有，则为第一个选中的标签；再没有则为第一个标签。
- 焦点环以焦点环色绘制在获得焦点的标签上，因此在选中的标签上依然可见。

### 键盘

该组只占用一个 Tab 停靠点，方向键在组内的标签之间移动。

| 按键 | 操作 |
|---|---|
| `Tab` / `Shift+Tab` | 进入该组时停在其 Tab 停靠点标签上，离开时移出该组 |
| `Right` / `Down` | 移到下一个标签；从最后一个循环到第一个 |
| `Left` / `Up` | 移到上一个标签；从第一个循环到最后一个 |
| `Home` / `End` | 移到第一个或最后一个标签；折叠时最后一个是“+N 更多”标签 |
| `Space` 或 `Enter` | 切换获得焦点的标签；在“+N 更多”标签上，展开或收起折叠的标签 |

## 动效

- 选中的标签会从左边缘长出勾选标记，标签文字滑开至新的宽度，标签的边缘随弹簧效果一起伸展。
- 相邻的标签滑动到新位置，包括跨行时，因此标签行重排时不会跳动。
- 列表展开时，折叠的标签以短暂的错开时间出现，组的高度平滑变化。
- 按下的标签会轻微缩小，松开时以弹簧回弹。

| 变化 | 完整 | 减弱 | 关闭 |
|---|---|---|---|
| 标签被选中或取消选中 | 勾选标记长出，标签滑动，边缘以弹簧效果伸展到新宽度 | 短暂淡入淡出 | 立即完成 |
| 相邻标签移动 | 滑动到新位置，跨行时同样如此 | 短暂淡入淡出 | 立即完成 |
| 折叠的标签出现 | 错开入场，组的高度缓动变化 | 短暂淡入淡出 | 立即完成 |
| 折叠的标签消失 | 原地淡出，比出现时更快 | 短暂淡入淡出 | 立即完成 |

## 相关组件

- `MultiSelect` 用于紧凑字段中的同类选择，`Checkbox` 用于彼此独立的表单选项。
- `SegmentedControl` 用于选择一个视图，`Select` 或 `Combobox` 用于表单中从列表选择一个值。

| 组件 | 适用场景 |
|---|---|
| `MultiSelect` | 选中的值必须放进紧凑字段或工具栏位置 |
| `Checkbox` | 每个选项都是表单中的“是”或“否”的回答，并有各自的校验 |
| `SegmentedControl` | 人们从两到四个视图或模式中选择一个 |
| `Select` | 表单中从短列表选择一个值 |
| `Combobox` | 从较长的列表中选择一个值，人们需要输入来查找 |
| `TagInput` | 人们创建自己的标签，而不是选择已有选项 |
