---
title: 可排序数据表格
summary: 通过可排序的列比较结构化记录。
source-hash: 8cb87c974275
---

<Demo name="Projects" />

## 何时使用

`SortableDataTable` 用于展示字段相同的记录，并让人们按列进行比较。它适合记录本身就是工作内容的页面：账单记录、管理后台中的项目列表、用户目录。每一列都可以单独排序，各行可以带复选框以便批量操作，计数行会说明共有多少行、选中了多少行。

- **发票与账单记录**：在账单页面按开票日期或金额排序，找到最近或最大的一笔费用，然后选中行下载或重新发送。
- **管理后台中的项目与工作流**：按负责人、状态或预算排序，看出哪些工作需要关注；状态徽标放在 `CellTemplate` 列中。
- **内部目录中的用户**：用复选框列选中多人，然后执行一项批量操作，例如停用账号。准备操作时，计数行会显示“已选中 3 项”。
- **后台中的订单**：按总额排序以找到大额订单，并用 Shift + 点击选中一段连续的行，以进行批量退款或导出。
- **开发者工具中的导出与任务历史**：列出过去的运行记录及其状态和时长，按用户关心的列排序，无需编辑。

## 何时不用

- **需要人工设定顺序的记录**，例如发布队列或手动置顶的优先级列表：用 `ReorderableList`，因为列排序无法表达自定义顺序。
- **按时间先后排列的事件**，例如审计日志或事故记录：用 `Timeline`，它能展示事件的先后顺序和间隔。
- **按字段和搜索文字缩小范围**：在表格上方放置 `FilterToolbar`，并只传入匹配的行。表格只对收到的行排序，不负责筛选。
- **嵌套的记录**，例如包含文件的文件夹：用 `TreeView`，它可以逐级展开和收起。
- **像电子表格那样原地编辑单元格**：本表格为只读。请用 `Input` 和 `Select` 组成的表单。

## 用法

列在 XAML 中声明。每一列通过 `Binding` 读取其单元格的值；除非该列设置了单独的排序值，表格会按该值排序。

```xml
<arc:SortableDataTable Caption="项目"
                       ItemsSource="{Binding Projects}"
                       ItemNameOne="项目"
                       ItemNameOther="项目"
                       SortKey="Budget"
                       SortDirection="Descending">
    <arc:SortableDataTable.Columns>
        <arc:DataTableColumn Header="项目" Key="Name" Binding="{Binding Name}" />
        <arc:DataTableColumn Header="负责人" Binding="{Binding Owner}" IsSortable="False" />
        <arc:DataTableColumn Header="预算" Key="Budget" Binding="{Binding Budget}" IsNumeric="True" />
    </arc:SortableDataTable.Columns>
</arc:SortableDataTable>
```

`SortableDataTable` 派生自 Avalonia 的 `TemplatedControl`。它不基于 `ItemsControl` 或 `DataGrid`，而是自行绘制表头、行和计数行。单元格默认为文本，这让每一行的绘制开销很低；需要超出文本的列则设置 `CellTemplate`。

### 结构

1. **标题**（`Caption`）：表格的无障碍名称。它不会被绘制出来，因此应写成名词短语，例如“2026 年 10 月的发票”。
2. **表头行**：高 44 px（含 1 px 分隔线）。表头文字使用中等字重。可排序的表头是按钮；`IsSortable="False"` 的列则是普通标签。已排序列的单元格带有弱化的列底色。
3. **全选复选框**（`IsSelectable`、`SelectAllLabel`）：表头起始处宽 44 px 的复选框列，第一列前额外留 8 px 的内边距。只选中部分行时显示混合状态。
4. **数据行**：高 52 px，带 1 px 分隔线（使用次级边框色）和 16 px 的单元格内边距。`IsNumeric` 列右对齐。悬停时整行填充，完整动效下选中背景以标准补间动画淡入。
5. **计数行**（`ItemNameOne`、`ItemNameOther`、`SelectedNoun`）：仅当 `IsSelectable` 为 true 时显示的 44 px 页脚。未选中时读作“6 个项目”，选中一行时为“已选中 2 项”。未选中行时文字为弱化色。
6. **清除选择**（`ClearSelectionLabel`）：页脚中 30 px 的幽灵按钮，仅在有选中行时显示。
7. **空行**（`EmptyMessage`）：高 120 px，使用弱化的文字颜色。
8. **加载遮罩**（`IsLoading`、`LoadingMessage`）：以表面色、60% 不透明度覆盖表头下方的行，中间为 18 px 的加载指示器，并显示弱化色的文字。
9. **外框**：表面背景、1 px 边框（边框色）和控件圆角。表格宽度低于 560 px 时，表格会横向滚动，而不是压缩列宽。

### 列与排序

表头默认作为排序标识，因此除非设置了 `Key`，`SortKey` 和排序事件报告的都是 `Header`。根据数据选择合适的属性：

| 列属性 | 适用情况 |
|---|---|
| `Binding` | 值是行项目的属性，绑定即可满足需要。 |
| `ValueSelector` 或 `DataTableColumn.Create<T>` | 应用以 AOT 或裁剪方式发布，或希望使用不含绑定的类型化代码。 |
| `SortBinding` 或 `SortValueSelector` | 显示的文本不是用于排序的值，例如应按数字排序的格式化金额。 |
| `Comparer` | 排序规则与业务相关，例如发布版本号。它按升序排列；空值仍排在最后。 |
| `IsSortable="False"` | 该列仅作描述，例如自由文本备注。 |
| `IsNumeric="True"` | 该列为金额或计数，应右对齐。不设置时会自动检测数字列。 |
| `Width` | 像素宽度（`Width="120"`）使列宽保持稳定；`*` 则占用剩余空间。 |

文本按自然顺序排序（连续的数字按数值比较，因此“项目 2”排在“项目 10”之前），数字按数值排序，日期按其自身的顺序排序。两个方向上，空值都排在最后。

### 选择

设置 `IsSelectable` 即可显示复选框列。点击一行会选中它，Shift + 点击会选中上次切换的行与被点击行之间的所有行，表头的复选框则选中或清除所有行。`SelectedItems` 为双向绑定，表格会就地更新绑定的可变列表。

```xml
<arc:SortableDataTable ItemsSource="{Binding Projects}"
                       IsSelectable="True"
                       SelectedItems="{Binding SelectedProjects, Mode=TwoWay}">
    <arc:SortableDataTable.Columns>
        <arc:DataTableColumn Header="项目" Binding="{Binding Name}" />
    </arc:SortableDataTable.Columns>
</arc:SortableDataTable>
```

选中状态跟随行项目；设置了 `RowKeySelector` 时则跟随它返回的键。当刷新会为同一批记录创建新对象时，请设置 `RowKeySelector`，这样选中状态和行动效就会始终跟随同一条记录。

### 单元格内容与格式

文本使用 `Formatter`，自定义内容（例如状态徽标）使用 `CellTemplate`。`Formatter` 接收原始值并返回文本，因此对金额和日期应传入区域性信息。`StringFormat` 是更简单的选项，适用于标准或复合格式。两者同时设置时，`Formatter` 优先；值为 null 时显示一个 en 破折号（–）。

```csharp
var table = new SortableDataTable { ItemsSource = projects, Caption = "项目" };
table.Columns.Add(DataTableColumn.Create<Project>("项目", p => p.Name));
table.Columns.Add(DataTableColumn.Create<Project>("预算", p => p.Budget, key: "Budget"));
table.Columns[1].Formatter = value => value is decimal amount ? amount.ToString("C0", CultureInfo.GetCultureInfo("en-US")) : "-";
table.Columns.Add(new DataTableColumn("状态", p => ((Project)p).Status)
{
    CellTemplate = new FuncDataTemplate<Project>((_, _) => new StatusPill())
});
```

行会随滚动被回收，因此模板控件必须在数据上下文变化时重新读取其内容。示例中的状态徽标在 `OnDataContextChanged` 中完成这项工作：

```csharp
public sealed record Project(string Name, string Owner, string Status, decimal Budget, DateOnly Due);

private sealed class StatusPill : Border
{
    private readonly TextBlock _text = new() { FontSize = 12 };

    public StatusPill()
    {
        Child = _text;
        Padding = new Thickness(8, 2);
        CornerRadius = new CornerRadius(999);
        HorizontalAlignment = HorizontalAlignment.Left;
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        _text.Text = (DataContext as Project)?.Status ?? string.Empty;
    }
}
```

### 排序与事件

单击可排序的表头即升序排序，再次单击为降序；新列总是从升序开始。启用 `IsSortClearable` 后，第三次单击会清除排序，行回到源数据的顺序。`SortChanged` 报告每一次用户排序以及每一次对 `Sort` 的调用，`SelectionChanged` 报告用户对选择的更改。`SortColumn`、`SortKey` 与 `SortDirection` 均为双向绑定，因此视图模型可以设置或读取排序状态。

<Demo name="SortedFromCode" />

```csharp
table.SortChanged += (_, e) => status.Text = e.Column is null
    ? "已清除排序"
    : $"按 {e.Column.Header} 排序，{e.Direction}";
table.SelectionChanged += (_, e) => status.Text = $"已选中 {e.SelectedItems.Count} 项";
table.Sort(table.Columns[1], DataTableSortDirection.Ascending);
```

### 绑定到视图模型

视图模型拥有行数据、排序与选择，表格只负责呈现它们。表格对收到的行进行排序，因此视图模型应设置 `SortKey` 与 `SortDirection`，并响应选择的变化，而不是自行对集合排序。示例使用 CommunityToolkit.Mvvm。

```csharp
public sealed record Invoice(string Number, string Customer, decimal Amount, DateOnly Issued);

public interface IInvoiceService
{
    Task<IReadOnlyList<Invoice>> ListAsync();
    Task ResendAsync(IReadOnlyList<string> numbers);
}

public partial class InvoicesViewModel : ObservableObject
{
    private readonly IInvoiceService _invoices;

    public InvoicesViewModel(IInvoiceService invoices) => _invoices = invoices;

    [ObservableProperty]
    private ObservableCollection<Invoice> _rows = [];

    [ObservableProperty]
    private ObservableCollection<Invoice> _selectedRows = [];

    [ObservableProperty]
    private string? _sortKey = "Issued";

    [ObservableProperty]
    private DataTableSortDirection _sortDirection = DataTableSortDirection.Descending;

    [ObservableProperty]
    private bool _isBusy;

    [RelayCommand]
    private async Task RefreshAsync()
    {
        IsBusy = true;
        try
        {
            Rows = new ObservableCollection<Invoice>(await _invoices.ListAsync());
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private Task ResendAsync() => _invoices.ResendAsync(SelectedRows.Select(r => r.Number).ToList());

    private bool HasSelection => SelectedRows.Count > 0;
}
```

```xml
<arc:SortableDataTable Caption="2026 年 10 月的发票"
                       ItemsSource="{Binding Rows}"
                       IsLoading="{Binding IsBusy}"
                       IsSelectable="True"
                       ItemNameOne="发票"
                       ItemNameOther="发票"
                       SortKey="{Binding SortKey, Mode=TwoWay}"
                       SortDirection="{Binding SortDirection, Mode=TwoWay}"
                       SelectedItems="{Binding SelectedRows, Mode=TwoWay}">
    <arc:SortableDataTable.Columns>
        <arc:DataTableColumn Header="编号" Key="Number" Binding="{Binding Number}" />
        <arc:DataTableColumn Header="客户" Binding="{Binding Customer}" />
        <arc:DataTableColumn Header="金额" Key="Amount" Binding="{Binding Amount}" IsNumeric="True"
                             StringFormat="C0" />
        <arc:DataTableColumn Header="开票日期" Key="Issued" Binding="{Binding Issued}" StringFormat="d" />
    </arc:SortableDataTable.Columns>
</arc:SortableDataTable>
```

- `SortKey` 与 `SortDirection` 为双向绑定：用户选择的排序会写回视图模型，视图模型中默认的“Issued”降序会在表格加载时生效。
- `SelectedItems` 为双向绑定，需要 `ObservableCollection<Invoice>` 作为目标。`ResendAsync` 读取的是同一个列表。
- 将 `IsLoading` 绑定到忙碌标志，使请求进行期间遮罩覆盖各行。
- 刷新时重建集合，或修改现有的 `ObservableCollection<T>`。选择跟随行的键，因此当行对应的仍是同一对象或 `RowKeySelector` 返回相同的键时，选择在刷新后得以保留。
- 编译绑定需要在视图上设置 `x:DataType`（示例为求简洁省略了它）。

### 数据结构与性能

每一行都是一条记录，每一列的字段都相同。表格在客户端对收到的行进行排序，因此它适合已经加载完成的结果集。只有视口附近的行会被实例化，这使下面 5,000 行的演示仍能流畅滚动；排序只重排索引，而不是重排行控件。

排序会在每次源数据或排序条件变化时运行，因此排序值应保持轻量：按已存储的数字或 `DateOnly` 排序，而不是按多个字段拼接出的字符串排序。对于数万条记录的结果集，请每次只加载一页，并在表格下方显示 `Pagination`，这样表格持有的行数永远不会超过一页所需。

### 空状态与加载状态

没有行时显示 `EmptyMessage`；`IsLoading` 会以遮罩覆盖各行，遮罩文字由 `LoadingMessage` 指定。空状态文案应写明原因和下一步，尤其是在筛选之后。

<Demo name="EmptyAndLoading" />

```xml
<arc:SortableDataTable EmptyMessage="没有符合这些筛选条件的项目"
                       IsLoading="{Binding IsBusy}" />
```

### 五千行

5,000 行的演示设置了 `IsSortClearable` 和 420 px 的最大高度，因此表格在自己的外框内滚动，表头保持固定。

<Demo name="FiveThousandRows" />

### 窄宽度

宽度低于 620 px 时，各行会折叠为两行，行高增至 64 px，表头变成一排高 32 px 的排序胶囊，“全选”固定在起始位置。点击胶囊会按其列排序，并把该胶囊滚动到可见区域。

<Demo name="Narrow" />

### 示例：带选择的导出历史

开发者工具列出团队生成的导出文件。每个导出项是一个文件名，操作者选中要下载或删除的文件。行是普通字符串，因此该列没有 `Binding`，每个单元格直接显示行项目本身；在实际应用中，该列会绑定到 `FileName` 之类的属性。名称按升序排列，使导出按类型分组；选中了两行，因此计数行显示“已选中 2 项”，并出现清除选择按钮。

![带有两个选中行和清除选择操作的导出历史表格](images/ExportHistory.png)

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc"
             xmlns:generic="using:System.Collections.Generic"
             Width="560">
    <arc:SortableDataTable Caption="Export history"
                           ItemNameOne="export"
                           ItemNameOther="exports"
                           IsSelectable="True"
                           SortKey="Export"
                           SortDirection="Ascending">
        <arc:SortableDataTable.ItemsSource>
            <generic:List x:TypeArguments="x:String">
                <x:String>invoices_2026_08.csv</x:String>
                <x:String>invoices_2026_09.csv</x:String>
                <x:String>usage_2026_09.csv</x:String>
                <x:String>payouts_2026_q3.csv</x:String>
                <x:String>audit_log_2026_10_14.csv</x:String>
            </generic:List>
        </arc:SortableDataTable.ItemsSource>
        <arc:SortableDataTable.SelectedItems>
            <generic:List x:TypeArguments="x:Object">
                <x:String>invoices_2026_09.csv</x:String>
                <x:String>usage_2026_09.csv</x:String>
            </generic:List>
        </arc:SortableDataTable.SelectedItems>
        <arc:SortableDataTable.Columns>
            <arc:DataTableColumn Header="Export" Width="*" />
        </arc:SortableDataTable.Columns>
    </arc:SortableDataTable>
</UserControl>
```

### 内容规范

表格中的文字是用来扫读的，而不是通读的。表头保持在一两个名词以内，让细节由单元格承载。

| 不要写 | 改为 |
|---|---|
| 含税的美元总额 | 总计 |
| 发票开具的日期 | 开票日期 |
| 当前已选中 5 项 | 已选中 5 项 |
| 没有数据 | 没有符合这些筛选条件的发票 |
| 正在加载，请稍候…… | 正在加载发票 |
| 选择本页的所有记录 | 全选发票 |

### 最佳实践

**推荐**

- 表头为展示标签的列设置 `Key`，这样事件和视图模型保存的排序标识在标签被翻译后仍保持稳定。
- 金额与计数列设置 `IsNumeric="True"`，使数字右对齐。
- 刷新会为同一批记录创建新对象时，设置 `RowKeySelector`，使选择在刷新后得以保留。
- 为表格至少留出 560 px 宽度（低于此宽度时表格会横向滚动），并在 620 px 以下进行测试，那里会切换为紧凑布局。

**避免**

- 按带格式的字符串（例如“$1,200”）排序，却不设置 `SortBinding` 或 `SortValueSelector`：文本按数字片段比较，因此“$1,200”会排在“$900”之前。
- 在没有 `Pagination` 的情况下加载超大数据集：所有行都会驻留内存，且每次点击排序都要处理全部数据。
- 只靠颜色表示状态。请把徽标文字保留在单元格中，使状态不依赖颜色也能读懂。
- 期望表格可以编辑行。它是只读的，也不跟踪编辑；请改为打开表单。

### 常见问题

**为什么数字列按文本排序？** 单元格的值是字符串，通常是因为绑定返回了格式化后的值。请绑定数字本身，或将 `SortBinding`（或 `SortValueSelector`）设置为数字属性。

**如何在代码中清除排序？** 调用 `Sort(null, DataTableSortDirection.None)`。行会恢复到源数据的顺序，且 `SortChanged` 会报告一个 null 列。

**如何选中符合筛选条件的行？** 将 `ItemsSource` 设为筛选后的行并调用 `SelectAll()`，或将 `SelectedItems` 绑定到匹配的项。`ClearSelection()` 会清除选择，并把焦点移到全选复选框。

**表格能筛选行吗？** 不能。它只对收到的行进行排序和选择。请在视图模型中筛选，或使用 `FilterToolbar`，并将筛选后的集合作为 `ItemsSource` 传入。

## API 参考

<!-- api: SortableDataTable -->

<!-- api: DataTableColumn -->

## 无障碍

- 表格以其 `Caption` 作为名称，并公开表格、表头和行的角色。
- 可排序的表头是一个按钮，名称类似“按预算排序，当前为升序”。排序方向属于名称的一部分，表头还会以项状态（升序、降序或无）报告。
- 只选中部分行时，全选复选框显示混合状态。其名称为 `SelectAllLabel`，默认为“选择所有行”。
- 排序与选择的变化会在礼貌型实时区域中播报，且不打断当前朗读，例如“按预算排序，升序”“已清除排序”“已选中 5 项中的 2 项”“已清除选择”。
- 表格只为键盘焦点绘制自己的焦点环，并会隐藏窗口默认的焦点框。

### 键盘

| 按键 | 操作 |
|---|---|
| `Left` / `Right` 在表头中 | 移到上一个或下一个表头控件；启用选择时，全选为第一个 |
| `Home` / `End` 在表头中 | 移到第一个或最后一个表头控件 |
| `Down` 在全选上 | 移到第一行的复选框（仅当 `IsSelectable` 为 true 且有行时） |
| `Enter` / `Space` 在可排序的表头上 | 升序，再降序；启用 `IsSortClearable` 时，第三次按下清除排序 |
| `Enter` / `Space` 在全选上 | 选中所有行；若所有行均已选中，则清除选择 |
| `Up` / `Down` 在行中 | 移到上一行或下一行；从第一行按 `Up` 返回全选 |
| `Home` / `End` 在行中 | 移到第一行或最后一行 |
| `PageUp` / `PageDown` 在行中 | 大约移动一屏的行数 |
| `Enter` / `Space` 在行上 | 切换该行的选中状态（仅当 `IsSelectable` 为 true 时） |
| `Shift` + `Enter` / `Space` 在行上 | 切换该行，并将上次切换的行到此行之间的各行设为同一新状态 |
| `Shift` + 点击行 | 将上次切换的行到被点击行之间的各行设为被点击行的新状态 |
| `Escape` | 有选中行时清除选择；若焦点在“清除选择”上，则焦点返回全选 |

## 动效

- 排序变化时，留存的行会以平滑弹簧从原位滑动到新位置，排序箭头以灵敏的弹簧翻转。
- 选中计数滚动到新值，清除选择按钮带着轻微模糊淡入。
- 减弱动效时行立即重新排序，计数仅淡入淡出。关闭动效时直接到达最终状态。

| 变化 | 完整 | 减弱 | 关闭 |
|---|---|---|---|
| 排序变化 | 行以平滑弹簧滑动到新位置；排序箭头翻转 | 行立即重新排序 | 立即完成 |
| 选中计数变化 | 计数滚动到新值 | 计数淡入淡出 | 立即完成 |

## 相关组件

- `TreeView` 用于可嵌套的记录，例如带子项的文件夹。
- `ReorderableList` 用于用户通过拖动而非列来设置顺序的场景。
- `FilterToolbar` 可在行进入表格之前，按字段和搜索缩小范围。
- `Pagination` 用于将大量记录拆分为多页。

| 组件 | 适用场景 |
|---|---|
| `ReorderableList` | 人们通过拖动排列项目，且没有列能描述顺序 |
| `Timeline` | 记录是按时间排列的事件，顺序比字段更重要 |
| `FilterToolbar` | 行在比较前需要上方的筛选器或搜索字段 |
| `Pagination` | 结果集太大无法一次加载，每一页都是单独的查询 |
| `TreeView` | 记录会嵌套，人们需要逐级展开和收起 |
| `CodeBlock` | 该值是供阅读和复制的代码片段，而不是可比较的记录集 |
