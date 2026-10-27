---
title: 数据表格
summary: 电子表格式的表格：范围选择、就地编辑、填充柄、剪贴板、撤销、排序、筛选、分组、合计与导出。
source-hash: 6f5dd7d44970
---

<Demo name="Renewals" />

## 何时使用

`DataGrid` 是让人就地编辑表格的控件，用起来像电子表格。它自带工具栏、表头菜单、筛选行、合计行和状态栏，因此表格本身就是工作区域。

- **财务工具中的预算与预测规划**：规划人员按季度填写明细项，从电子表格粘贴数据，跨月份填充，粘贴出错时撤销。数值变化时，合计行保持最新。
- **客户成功控制台中的续约管线**：数百个客户账户，包含负责人、区域、阶段、席位和 ARR。管理者可以筛选到某一区域，按续约日期排序，并直接在表格中推进阶段。
- **电商后台的价目表与商品目录**：批量维护价格、毛利和生效日期，复制粘贴的行为与 Excel 和 Google Sheets 一致。
- **金融科技或计费中的日记账复核**：按账户和期间分组的分录，带小计，导出为保留大纲结构和 SUBTOTAL 公式的工作簿。
- **服务端的大量运营历史**，例如订单、付款或同步日志：`DataSource` 只加载可视范围内的数据块。

## 何时不用

- **带排序与行选择的只读记录**，例如客服工单或用户：用 `Table`，它不需要编辑相关机制。
- **一次只编辑一条记录**，例如客户资料或发票抬头：用由 `Input`、`Select` 和 `DatePicker` 组成的表单，它们会逐个字段校验。
- **含图片、长文本或操作的行**：用 `Card` 或 `ReorderableList`，每个项目都有容纳内容的空间。
- **文件夹或会计科目表等层级数据**：用 `TreeView`。分组按键值折叠行，不会根据父子记录构建树。
- **仅在仪表板上汇总数字的表格**：用 `MetricCard` 或 `BarChart`。对于汇总展示来说，工具栏和状态栏过于繁杂。

## 用法

列通过 `Key` 映射到行的字段。行是 `DataGridRow` 对象（一个键加上命名字段），或者任何可通过列绑定或访问器读取的类。每行都需要唯一且稳定的键。存储原始值：数字存为数字，日期存为 `DateOnly`，不要存格式化后的文本，这样排序、合计、筛选和导出都能作用于数值本身。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:DataGrid Label="续约" MaxViewportHeight="440" ExportFormats="All">
        <arc:DataGridColumn Key="account" Header="客户" Width="188" IsPinned="True" />
        <arc:DataGridColumn Key="stage" Header="阶段" Type="Select" />
        <arc:DataGridColumn Key="arr" Header="ARR" Type="Currency" />
        <arc:DataGridColumn Key="health" Header="健康度" Type="Percent" />
        <arc:DataGridColumn Key="renewal" Header="续约" Type="Date" />
    </arc:DataGrid>
</UserControl>
```

```csharp
grid.ItemsSource = new ObservableCollection<DataGridRow>
{
    new("acct-1") { { "account", "Lumen Labs" }, { "stage", "Proposal" }, { "arr", 48000m }, { "renewal", new DateOnly(2026, 9, 30) } }
};
```

`DataGrid` 派生自 `TemplatedControl`，只绘制可视范围内的行和列。它的视图状态（排序、筛选、搜索、已勾选行、隐藏与固定的列、密度和分组）都是双向的，因此视图模型可以保存和还原它。

### 结构

1. **工具栏**（`ShowToolbar`）：高 52 px，按钮 32 px，搜索框 220 px。提供搜索、撤销与重做、筛选、列、密度和导出。勾选行后工具栏切换为批量状态。宽度低于 640 px 时按钮仅显示图标；低于 460 px 时隐藏行数以及撤销与重做。
2. **表头行**：40 px。每个单元格显示 `Header`，以及排序按钮和菜单按钮，菜单可执行排序、筛选、分组、固定和隐藏。数值列的表头右对齐。
3. **筛选行**（`IsFilterRowOpen`）：40 px，从“筛选”或列菜单打开。无效的表达式会标记所在单元格。
4. **边栏**（`ShowRowNumbers`、`IsRowCheckEnabled`）：48 px，显示行号和复选框。设置 `ShowRowNumbers="False"` 后宽度缩为 40 px，复选框仍然可见。
5. **数据行**（`Density`）：`Compact` 下为 32 px（13 px 文字），`Standard` 下为 40 px，`Comfortable` 下为 48 px，行间以 1 px 的弱化分隔线隔开。
6. **选区、活动单元格与填充柄**：选区以 9% 不透明度的强调色填充，并带 1 px 强调色轮廓；活动单元格有 2 px 强调色环。填充柄是位于选区右下角的 8 px 强调色方块，外围有 2 px 表面色环。
7. **合计行**（`ShowTotals`）：40 px，粘附在底部。它统计筛选保留的每一行，包括折叠分组中的行。
8. **状态栏**：34 px。显示选区地址以及求和、平均值和计数（`ShowSelectionStats`），显示行数（例如筛选时显示“120 行中的 12 行”），以及消息。提供“撤销”的消息停留 6 秒，其他消息停留 2.4 秒。
9. **固定列**（`IsPinned`）：表体横向滚动时固定在左边缘，带 10 px 的边缘阴影。

### 列类型

`Type` 决定列如何显示、呈现、解析和统计其值。数字、货币和百分比列右对齐，并能解析带货币符号、百分号、千分位和会计括号的输入，例如 `(12)` 表示 -12。

| 类型 | 存储 | 显示 | 默认合计 | 输入接受 |
|---|---|---|---|---|
| `Text` | `string` | 文本 | 无 | 任意文本 |
| `Number` | `decimal` | 按 `Decimals` 位数显示的分组数字 | 求和 | 数字 |
| `Currency` | `decimal` | `CurrencySymbol`（默认 `$`）及分组数字 | 求和 | 数字，可带或不带符号 |
| `Percent` | `decimal` | 数值加百分号 | 平均值 | 数字，可带或不带百分号 |
| `Select` | `string` | 选项文本 | 无 | `Options` 中的一项，忽略大小写 |
| `Date` | `DateOnly` | 所在语言的月份名称，或 `DateFormat` | 无 | 日历选择、ISO 日期、月份名称 |
| `Boolean` | `bool` | 复选框 | 无 | yes、no、true、false、1、0、是、否 |

`Select` 按 `Options` 的顺序排序，而不是按字母顺序。`Date` 拒绝 01/02/2026 这类纯数字格式，因为不同国家的读法不同；请写 `2026-02-01` 或使用月份名称。货币符号不随界面语言改变，因此切换语言后欧元列仍以欧元显示。

`Aggregate` 可取 `None`、`Sum`、`Average`、`Min`、`Max` 或 `Count`（非空单元格），例如利润率审查中使用 `Min`。

`Boolean` 列在单元格中间画一个复选框。点击它，或按空格、Enter、F2，即可翻转值；输入 yes 或 no 会直接设置。翻转属于普通编辑：`CellEditEnding` 可以拒绝，每次翻转是一步撤销，只读列不会翻转。复制和 CSV 写出 TRUE 与 FALSE，Excel 导出写成布尔单元格，筛选提供“是”和“否”。

### 单元格渲染器

默认情况下，列把值画成文字。设置 `Renderer` 可以画出更丰富的内容：

| 渲染器 | 绘制内容 | 设置 |
|---|---|---|
| `BadgeRenderer` | 按值着色的小药丸，适合阶段或状态 | `ToneMap`（或 `Tones`）、`DefaultTone` |
| `ProgressRenderer` | 一根进度条，值的文字显示在右端 | `Minimum`、`Maximum`、`ShowText`、`Tone` |
| `LinkRenderer` | 强调色链接；点击会触发 `CellInvoked`，并且不会选中该单元格 | `Part` |
| `IconTextRenderer` | 按值选择的图标，后面跟着文字 | `Icons`、`DefaultIcon`、`Tones`、`ShowText` |
| `CheckboxRenderer` | 居中的复选框；点击会翻转值（Boolean 列默认使用） | 无 |
| `ActionsRenderer` | 一排图标按钮，例如行尾的编辑和删除；点击会带着该行运行动作的 `Command`，并触发 `CellInvoked` | `Actions` |

<Demo name="CustomCells" />

```xml
<arc:DataGrid Label="Deals">
    <arc:DataGridColumn Key="stage" Header="Stage" Type="Select">
        <arc:DataGridColumn.Renderer>
            <arc:BadgeRenderer ToneMap="Won=Success, Proposal=Warning, Lost=Danger" />
        </arc:DataGridColumn.Renderer>
    </arc:DataGridColumn>
</arc:DataGrid>
```

表格会像绘制文字一样，把渲染器和行的其余部分一起绘制，所以有数千行的表格依然流畅；渲染器不是控件。渲染器只改变单元格的外观，不改变它保存的内容：排序、筛选、合计、导出和编辑仍然作用于值，复制和屏幕阅读器读取的是 `GetText`。颜色来自 `DataGridTone`（`Neutral`、`Accent`、`Success`、`Warning`、`Danger`），在明暗主题下都会跟随主题和强调色。

要绘制其他内容，请派生 `DataGridCellRenderer` 并重写 `Render`。一个渲染器由所在列的所有单元格共用，因此不要为单个单元格保存状态。

```csharp
public sealed class RatingRenderer : DataGridCellRenderer
{
    public override void Render(in DataGridCellRenderContext context)
    {
        var stars = context.Value is decimal value ? (int)value : 0;
        context.DrawText(new string('★', stars), context.Bounds.X, context.Bounds.Width, context.Palette.Accent);
    }

    public override string GetText(object? value, DataGridCellFormat format, CultureInfo culture) =>
        value is decimal d ? $"{d:0} of 5" : string.Empty;
}
```

用 `DrawText` 绘制文字，它会在帧与帧之间保留每一行的排版；用相同的参数调用 `MeasureText` 来计算周围元素的尺寸，画刷请取自 `Palette`。重写 `OnPointerPressed`（以及用于手形光标的 `IsInteractiveAt`）来响应点击，并调用 `Invoke` 触发 `CellInvoked`。

要用自己的控件编辑某列的单元格，请设置 `EditTemplate`。模板为 `DataGridEditContext` 构建内容：读写它的 `Value`，Enter、Tab 或 Escape 会提交或取消编辑（也可以调用 `Commit()` 与 `Cancel()`）。只有正在编辑的单元格是真正的控件，所以表格依然很快。值与键入走同一条路径：必须符合列的类型，`CellEditEnding` 可以拒绝它。

```csharp
column.EditTemplate = new FuncDataTemplate<DataGridEditContext>((context, _) =>
{
    var slider = new Slider { Minimum = 0, Maximum = 100, Value = context.Value is decimal v ? (double)v : 0 };
    slider.ValueChanged += (_, e) => context.Value = Math.Round((decimal)e.NewValue);
    return slider;
});
```

### 编辑、剪贴板与撤销

直接输入即可替换值；按 Enter、F2 或双击可进入编辑并保留原值，光标位于末尾。Enter 提交并向下移动，Shift+Enter 向上移动，Tab 提交并向右移动。Escape 取消。无法解析的值会保留编辑器，文字以危险色显示并轻微抖动。点击表格外部时，有效值会被提交，无效值会被丢弃。

复制时会写入以制表符分隔的文本和 HTML 表格，因此 Excel 和 Google Sheets 能识别输入的数字。粘贴可读取两种格式；如果选区是剪贴板内容的整数倍，则平铺填充。拖动填充柄可延续序列：数字按步长，日期按天或按月。Ctrl+D 向下填充。撤销保留 `UndoLimit` 步（默认 100 步；设为 0 则关闭撤销）。对于计算列或锁定列，请设置 `IsEditable="False"`：编辑、粘贴、填充和清除都会跳过它们。粘贴内容超过 `MaxPasteCells` 个单元格（默认 1,000,000）时会被拒绝并给出提示，因此过大或恶意构造的表格不会耗尽内存。

<Demo name="Budget" />

`CellEditEnding` 在写入值之前触发，应用可以据此拒绝修改。`CellsChanged` 在每次写入后触发（包括撤销和重做），事件参数包含每个单元格的旧值和新值，以及 `Reason`（`Edit`、`Paste`、`Cut`、`Clear`、`Fill`、`FillDown`、`Undo` 或 `Redo`）。编辑不会重新排序或重新筛选，因此行保持原位。

```csharp
grid.CellEditEnding += (_, e) =>
{
    if (e.NewValue is decimal amount && amount < 0)
    {
        e.Cancel = true;
        e.ErrorMessage = "ARR 不能为负数";
    }
};
grid.CellsChanged += (_, e) => SaveChanges(e.Changes);
```

### 校验、锁定单元格与条件样式

给列设置 `Rules` 来检查用户键入、粘贴或填充的内容。内置规则有 `RequiredRule`、`RangeRule`（`Minimum`、`Maximum`）、`MaxLengthRule` 和 `PatternRule`；`DataGridRule.Custom` 接收一个函数，返回错误消息，通过时返回 null。消息跟随界面语言，也可以设置 `Message`。

默认（`ValidationMode="Block"`）会拒绝不通过的值：编辑器保持打开并显示消息，粘贴或填充时不通过的单元格会被跳过，并计入状态栏。清除单元格、撤销和重做从不被阻止。设为 `ValidationMode="Flag"` 时，值会被接受，单元格以红色轮廓和一个红点标出；工具栏显示被标出的数量（点击它或按 F8 跳到下一个），选中被标出的单元格会播报其消息。`ValidateAll()` 会检查每一行，适用于一开始就有错误的数据，`GetErrors()` 列出被标出的单元格。

<Demo name="Validation" />

```xml
<arc:DataGrid Label="Orders">
    <arc:DataGridColumn Key="qty" Header="Qty" Type="Number">
        <arc:DataGridColumn.Rules>
            <arc:RequiredRule />
            <arc:RangeRule Minimum="1" Maximum="99" />
        </arc:DataGridColumn.Rules>
    </arc:DataGridColumn>
</arc:DataGrid>
```

`IsEditable` 锁定整列。要锁定行或单个单元格，请设置 `CanEditCell`，它是单元格（行、键、列和值）的函数：编辑、粘贴、填充和清除会跳过它拒绝的单元格。

要突出某些值，请给列设置 `FormatRules`。每条规则测试值（`GreaterThan`、`LessThan`、`Between`、`Equal`、`Contains`、`IsEmpty`、`Negative` 等），并用色调（`Neutral`、`Accent`、`Success`、`Warning` 或 `Danger`）给单元格着色，还可以加粗；第一条匹配的规则生效。被比较的值按键入该列的写法书写，例如 `$1,000` 或 `2026-06-01`。

```xml
<arc:DataGrid Label="Orders">
    <arc:DataGridColumn Key="due" Header="Due" Type="Date">
        <arc:DataGridColumn.FormatRules>
            <arc:DataGridFormatRule Condition="LessThan" Value="2026-06-01" Tone="Danger" />
        </arc:DataGridColumn.FormatRules>
    </arc:DataGridColumn>
</arc:DataGrid>
```

规则无法表达的逻辑，请设置 `CellStyleSelector`（按单元格）或 `RowStyleSelector`（按行）。它们每一帧都会为视口内的每个单元格运行，所以要保持轻量。

### 添加行

设置 `CanInsertRows` 后，工具栏会显示“添加行”，并允许调用 `AddRow()` 和 `InsertRow()`；Ctrl+Shift+加号在活动行上方插入。`DataGridRow` 类型的行无需其他设置；自有类的行需要一个 `RowFactory`，返回带有自己的键的新对象，并且 `ItemsSource` 必须是可写的列表。添加一行是一步撤销，`RowsInserted` 会报告它，撤销时会触发 `RowsDeleted`。设置 `PasteOverflow="AppendRows"`，超出最后一行的粘贴会添加所需的行，而不是丢弃。

```csharp
grid.CanInsertRows = true;
grid.RowFactory = () => new Account(Guid.NewGuid().ToString("N"));
```

### 列、已保存的视图与单元格事件

拖动表头标签即可移动该列，一条线会显示它将落在哪里。在表头上按 Alt+Shift+Left 或 Alt+Shift+Right 可以用键盘完成同样的操作。固定列只在固定列之间重排。`ColumnOrder` 保存按显示顺序排列的列键，`ColumnWidths` 保存用户设置的宽度；与其他视图状态一样，两者都是双向的。双击表头右边缘，或在列菜单中选择“适应内容宽度”，会把该列调整到最宽单元格和表头所需的宽度（测量前 1,000 行）。`AutoFitColumns()` 处理所有列，`ResetColumnWidths()` 撤销这些宽度。

要保存视图，`GetLayoutState()` 返回 `DataGridLayoutState`（列顺序与宽度、固定与隐藏的列、排序、筛选、分组和行密度），`ToJson()` 用对裁剪与 AOT 安全的序列化器把它写成 JSON。`SetLayoutState()` 恢复它：状态中缺少的字段回到默认值，已不存在的列的键会被忽略，所以列变化后，保存的视图依然可用。

```csharp
File.WriteAllText(path, grid.GetLayoutState().ToJson());

// later, when the window opens:
if (DataGridLayoutState.FromJson(File.ReadAllText(path)) is { } state)
{
    grid.SetLayoutState(state);
}
```

表格会为用户触碰的单元格触发事件。`CellClick` 在用主按钮按下主体单元格时触发，此时选区已经移动。`CellDoubleClick` 在编辑器打开之前触发；设置 `Handled` 可以让编辑器保持关闭。`RowActivated` 在对不可编辑的单元格按 Enter 时触发，也在双击行号时触发，这就是“打开这一行”的手势。`CellContextRequested` 在右键单击、Shift+F10、菜单键和长按时触发；设置 `Handled` 可以显示你自己的菜单。

否则表格会选中该单元格（当它在选区之外时）并打开菜单：剪切、复制、复制（含表头）和粘贴；在上方或下方插入行（需要 `CanInsertRows`）和删除行（需要 `CanDeleteRows`）；清除内容和向下填充；按此值筛选、排序和分组。用 `ContextMenuItems` 添加条目，每个条目会带着被点击的行运行它的 `Command`；用 `ContextMenuOpening`（`Items`、`Cancel`）为某一次打开修改它们；或设置 `ShowContextMenu="False"` 关闭菜单。

```xml
<arc:DataGrid Label="Accounts">
    <arc:DataGrid.ContextMenuItems>
        <arc:DataGridMenuItem Label="Open account" />
    </arc:DataGrid.ContextMenuItems>
</arc:DataGrid>
```

### 查找与替换

Ctrl+F 会在主体右上角打开查找栏，Ctrl+H 会再加上替换行（也可以点箭头）。它在用户看到的文字中查找，所以 `1,200` 能找到显示为 `$1,200` 的单元格。查找栏打开时，所有匹配的单元格都会着色，查找栏会给出计数（“第 3 个，共 27 个”）。Enter 与 Shift+Enter，或 F3 与 Shift+F3，跳到下一个和上一个匹配，到头会循环；Escape 关闭查找栏。“区分大小写”和“匹配整个单元格”可以缩小范围。

“替换”会替换当前匹配中的文字并跳到下一个；“全部替换”把所有匹配作为一步撤销一次改完。新文字会按键入的方式读回，所以在货币或日期单元格里替换时，只要结果仍是有效值就能成功；会破坏单元格的替换（不存在的选项、数字列里的文字）会被跳过，并计入状态栏。只读单元格也会被跳过。使用 `DataSource` 时，Ctrl+F 改为聚焦工具栏搜索，因为只有视口内的块被加载。

```csharp
grid.FindText = "labs";
grid.FindMatchCase = false;
grid.ReplaceAll("Group");   // one undo step; cells whose new text does not parse are skipped
```

### 使用自有类的行

为列指定已编译的 `Binding`（编辑会双向写回），或使用 `DataGridColumn.Create` 创建列：它使用访问器，不需要绑定，并且对 AOT 友好。没有 setter 的列为只读。两种方式接收的都是规范化后的值：字符串、`decimal`、`DateOnly` 或 null。

<Demo name="TypedRows" />

```csharp
public sealed class Account { public decimal Arr { get; set; } public int Seats { get; set; } }

grid.Columns.Add(DataGridColumn.Create<Account>("arr", "ARR", a => a.Arr, (a, v) => a.Arr = v as decimal? ?? 0,
    DataGridColumnType.Currency));
grid.Columns.Add(DataGridColumn.Create<Account>("total", "Total", a => a.Arr * a.Seats, type: DataGridColumnType.Currency));
```

### 排序、筛选与搜索

单击表头可依次切换为升序、降序和取消排序。Shift+单击会将该列添加为下一个排序键；若它已是排序键，则反转其方向。无论哪种方式，空值都会沉到底部。筛选行接受以下表达式：

- **文本**：`abc` 表示包含该文字，`=abc` 表示完全匹配，`!abc` 表示排除。匹配忽略大小写和重音符号。
- **数字、货币和百分比**：`>100`、`<5`、`=12`、`!=0`，或闭区间如 `10..50`。
- **日期**：`2026-01-31` 表示某一天，`2026-03` 表示某个月，`2026` 表示某一年。像 `> 2026-03` 这样带前缀的写法按边界比较，即表示 2026 年 3 月之后。区间写作 `2026-01-01..2026-03-31`。
- **选择列**：一个值的复选列表；超过七个值时上方会出现搜索框，列表中还有一项用于空白单元格。

无法读取的表达式会将所在单元格标记为无效，并且不生效。隐藏列上的筛选仍然生效，并仍会计入“筛选”按钮的数量。快速搜索（`SearchText`）会把文本拆分为多个词；每个词都必须出现在某个可见列中。

### 视图状态

排序、筛选、搜索、已勾选行、隐藏与固定的列、密度和分组都是双向属性，因此视图模型可以保存、还原或重置视图。`ClearFilters()` 会清除列筛选和快速搜索。

<Demo name="ControlledState" />

```xml
<arc:DataGrid SortDescriptions="{Binding Sort}"
              Filters="{Binding Filters}"
              CheckedRowKeys="{Binding Checked}"
              Density="{Binding Density}" />
```

### 分组与合计

`GroupBy` 接受列键，最外层的在前。分组行显示一个展开或折叠开关、分组值、行数，以及每个聚合不为 `None` 的列下的小计。应按取值较少的列分组；数字列默认不可分组，设置 `IsGroupable="True"` 可允许对某一数字列分组。

<Demo name="Ledger" />

Excel 导出会把分组写为带大纲的行，并附带 SUBTOTAL 公式，因此读者在 Excel 中也能折叠它们。

### 勾选行与批量操作

在边栏中勾选行：Shift+单击可勾选一段连续的行，Shift+Space 勾选选区内的行。勾选行后，工具栏会提供“复制”“导出”、你的 `BulkActions`，以及“删除”（需开启 `CanDeleteRows`）。删除会记录行的位置，因此撤销可将行按原顺序放回；状态栏同样会在 6 秒内提供撤销。

```xml
<arc:DataGrid Label="账户">
    <arc:DataGrid.BulkActions>
        <arc:DataGridBulkAction Label="归档" Icon="{x:Static arc:ArcIcons.Archive}" Command="{Binding ArchiveCommand}" />
        <arc:DataGridBulkAction Label="移出计划" IsDanger="True" Command="{Binding RemoveCommand}" />
    </arc:DataGrid.BulkActions>
</arc:DataGrid>
```

命令接收的是按数据顺序排列的已勾选行，类型为 `IReadOnlyList<object>`。网格还会触发 `BulkActionInvoked`，携带该操作和这些行。网格宽度低于 640 px 时标签会隐藏，因此请为每个操作都提供图标。

### 导出

`ExportFormats` 可列出 `Csv`、`Xlsx` 或两者（`All`）；选择多种格式时，“导出”会打开一个菜单。导出会按视图顺序写出筛选后保留的每一行；批量导出只写出已勾选的行。`Exporting` 可以重命名或取消文件，`ExportAsync` 则可从代码导出。宿主无法显示保存对话框时，导出被禁用。

用 Excel 或 Sheets 打开 CSV 时，以 `=`、`+`、`-` 或 `@` 开头的文本会被当作公式执行，恶意的单元格因此可以读取数据或启动程序（CSV 注入）。所以表格写出这类文本时会在前面加一个单引号（`'=1+1`），单引号会显示在单元格里。数字和日期不会加前缀，Excel 导出则始终把文本存为文本。只有在文件不会进入电子表格、并且文本必须逐字节往返时，才设置 `NeutralizeCsvFormulas="False"`。

```csharp
grid.Exporting += (_, e) => e.FileName = $"renewals-{DateTime.Today:yyyy-MM-dd}";
await grid.ExportAsync(DataGridExportFormat.Xlsx);
```

写入器、剪贴板辅助类和解析器都是公共静态类（`DataGridCsv`、`DataGridXlsx`、`DataGridClipboardText`、`DataGridValueParser`、`DataGridFilterExpression`、`DataGridSorting`），服务端可以复用它们。

### 服务端数据

数据量过大、无法一次发送时，将 `DataSource` 设为 `IDataGridDataSource`。网格会请求覆盖可视行的数据块，在加载期间显示占位行，并最多保留 40 个数据块。排序、筛选或搜索变化时，它会重新开始并取消正在进行的请求。此时网格为只读，不分组，隐藏“全选”，导出时仅导出已勾选的行。还需要设置 `RowKeySelector`，使用数据源时它是必需的。

<Demo name="ServerData" />

### 加载与空状态

`IsLoading` 显示六个占位行，合计行显示“加载中”。当没有任何行时，`EmptyMessage` 会替换内置的“No rows yet”（暂无行）。当筛选隐藏了所有行时，网格会说明这一点，并提供“清除筛选”。

<Demo name="States" />

### 绑定到视图模型

在 MVVM 应用中，视图模型负责管理行、视图状态和命令。使用 CommunityToolkit.Mvvm 时，行是 `ObservableCollection<DataGridRow>`，视图状态是可观察属性，网格会将变化写回这些属性。

```csharp
public partial class RenewalsViewModel : ObservableObject
{
    private readonly IRenewalService _renewals;

    public RenewalsViewModel(IRenewalService renewals)
    {
        _renewals = renewals;
    }

    public ObservableCollection<DataGridRow> Renewals { get; } = new();

    [ObservableProperty]
    private IReadOnlyList<DataGridSort> _sortDescriptions = [];

    [ObservableProperty]
    private IReadOnlyDictionary<string, DataGridFilter> _filters = new Dictionary<string, DataGridFilter>();

    [ObservableProperty]
    private IReadOnlyList<object> _checkedRowKeys = [];

    [ObservableProperty]
    private DataGridDensity _density = DataGridDensity.Standard;

    [RelayCommand]
    private async Task ArchiveAsync(IReadOnlyList<object> rows)
    {
        await _renewals.ArchiveAsync(rows.Cast<DataGridRow>().Select(r => r.Key));
    }
}
```

```xml
<arc:DataGrid Label="续约"
              ItemsSource="{Binding Renewals}"
              SortDescriptions="{Binding SortDescriptions}"
              Filters="{Binding Filters}"
              CheckedRowKeys="{Binding CheckedRowKeys}"
              Density="{Binding Density}">
    <arc:DataGrid.BulkActions>
        <arc:DataGridBulkAction Label="归档" Command="{Binding ArchiveCommand}" />
    </arc:DataGrid.BulkActions>
</arc:DataGrid>
```

- `SortDescriptions`、`Filters`、`CheckedRowKeys`、`Density`、`SearchText` 和 `GroupBy` 都是双向的：表头点击会更新视图模型，视图模型中的变化也会更新网格。
- `ItemsSource` 是单向的。编辑会写入行对象，网格对每次写入都会触发 `CellsChanged`。请在代码后置文件中的 `CellsChanged` 处理程序里保存更改，因为它是事件而非命令。

### 数据结构与性能

每一行都需要稳定的键（`DataGridRow.Key`，或用于强类型行的 `RowKeySelector`），且键不应改变，因为它用来标识已勾选的行和滚动目标。设计目标是：客户端模式下约 10,000 行流畅，约 50,000 行可用；超过此规模时请使用 `DataSource`。数字存为 `decimal`，日期存为 `DateOnly`；对于属于固定地区的数字，请设置 `Culture`。

### 示例：客户成功团队的续约评审

客户成功经理评审未来两个季度的续约。固定的客户列让名称始终可见。阶段是带有有序选项列表的 `Select`，因此排序会按销售管线的顺序进行。ARR 和区域为只读，因为账单导出负责这些数据；在此编辑会在下次导入时被覆盖。`Min` 健康度合计显示可视范围内最弱的账户，而 `CanDeleteRows="False"` 则保证账户在账单关闭前不会被删除。

```xml
<arc:DataGrid Label="2026 年第四季度续约"
              MaxViewportHeight="440"
              ExportFormats="All"
              ExportFileName="renewals-q4-2026"
              CanDeleteRows="False">
    <arc:DataGridColumn Key="account" Header="客户" Width="188" MinWidth="120" IsPinned="True" IsEditable="False" />
    <arc:DataGridColumn Key="owner" Header="负责人" Width="148" />
    <arc:DataGridColumn Key="region" Header="区域" Type="Select" Width="144" IsEditable="False" />
    <arc:DataGridColumn Key="stage" Header="阶段" Type="Select" Width="140" Options="{Binding StageOptions}" />
    <arc:DataGridColumn Key="seats" Header="席位" Type="Number" Width="96" Aggregate="Sum" />
    <arc:DataGridColumn Key="arr" Header="ARR" Type="Currency" CurrencySymbol="$" Width="128" IsEditable="False" />
    <arc:DataGridColumn Key="health" Header="健康度" Type="Percent" Width="104" Aggregate="Min" />
    <arc:DataGridColumn Key="renewal" Header="续约" Type="Date" DateFormat="yyyy-MM-dd" Width="132" />
</arc:DataGrid>
```

### 文案规范

- **表头**：句子式大小写的简短名词短语，例如“续约”或“ARR”。当单元格不显示单位时，加上单位（“ARR（美元）”）。表头是该列的无障碍名称，也是导出时的列标题。
- **标签**：为打开表格的人命名。它是无障碍名称，也是默认的导出文件名。
- **批量操作**：使用动词，例如“归档”。破坏性操作使用 `IsDanger`；当记录仍保留在其他系统中时，使用“移出计划”。

| 不要写 | 改为 |
|---|---|
| 金额 | ARR（美元） |
| 保存文件 | 导出为 Excel |
| 错误 | ARR 不能为负数 |
| 立即全部删除 | 归档已勾选的账户 |

### 最佳实践

**推荐**

- 存储原始值（decimal、`DateOnly`、选项字符串），让列负责格式化，这样排序、合计和导出都与用户看到的一致。
- 为每一行设置唯一且稳定的值作为键。排序或删除会移动行，只有稳定的键才能让勾选始终对应正确的行。
- 固定用于标识行的列，通常是名称，使其在网格横向滚动时仍然可见。
- 将导入或计算得到的列标记为 `IsEditable="False"`，而不是指望人们不去修改它们。
- 在 `CellEditEnding` 中用具体的消息进行校验。被拒绝的编辑会保留编辑器，让用户就地修正。

**避免**

- 对几百行数据使用 `DataSource`。它会使网格变为只读并禁用分组，而客户端模式在这个规模下是支持分组的。
- 在数值到达网格之前将其格式化为字符串。字符串会按文本排序，而且合计会忽略它。
- 同一屏幕上有多个带批量操作的可编辑网格。每个网格都有各自的勾选集合，人们很难分清批量操作会影响哪一个。

### 常见问题

**为什么我修改了已排序的值后，行仍留在原位？** 编辑不会重新排序或重新筛选，因此行会保持位置，直到排序或筛选条件改变。再次点击表头，或修改筛选，即可重新排序。

**如何在代码中撤销批量删除？** 调用 `Undo()`，它会还原上一次编辑、填充、粘贴、清除或删除行的操作。`CanUndo` 表示是否存在可撤销的步骤，`ClearHistory()` 会清除所有步骤，例如在保存成功后调用。`UndoLimit` 设置保留的步数。

## API 参考

<!-- api: DataGrid -->

<!-- api: DataGridColumn -->

<!-- api: DataGridBulkAction -->

<!-- api: DataGridCellRenderer -->

<!-- api: BadgeRenderer -->

<!-- api: ProgressRenderer -->

<!-- api: LinkRenderer -->

<!-- api: IconTextRenderer -->

<!-- api: CheckboxRenderer -->

<!-- api: DataGridCellAction -->

<!-- api: ActionsRenderer -->

<!-- api: DataGridEditContext -->

<!-- api: RequiredRule -->

<!-- api: RangeRule -->

<!-- api: MaxLengthRule -->

<!-- api: PatternRule -->

<!-- api: DataGridRule -->

<!-- api: DataGridFormatRule -->

<!-- api: DataGridCellContext -->

<!-- api: DataGridMenuItem -->

<!-- api: DataGridCellEventArgs -->

<!-- api: DataGridContextMenuOpeningEventArgs -->

<!-- api: DataGridLayoutState -->

<!-- api: DataGridFilterState -->

<!-- api: DataGridCellRenderContext -->

<!-- api: DataGridCellPointerContext -->

<!-- api: DataGridRenderPalette -->

<!-- api: DataGridCellInvokedEventArgs -->

服务端数据来自 `IDataGridDataSource`。网格会为每个需要的数据块调用 `GetRowsAsync`。

| 成员 | 说明 |
|---|---|
| `int BlockSize` | 每次请求的行数。默认为 100。 |
| `Task<DataGridRowsResult> GetRowsAsync(DataGridRowsRequest request, CancellationToken cancellation)` | 返回一个数据块，其中包含已排序、已筛选和已搜索的行。 |

`DataGridRowsRequest` 包含 `Start`（含）、`End`（不含）、`Sort`、`Filters` 和 `Search`。结果包含 `Rows`、`RowCount`（整个筛选后的集合）和 `Totals`（按列键；为 null 时保留之前的合计）。

## 无障碍

- 网格主体是一个可获得焦点的元素。方向键、Shift 加方向键、Home、End、PageUp 和 PageDown 用于移动和扩展选区；Tab 离开网格。
- 主体的无障碍名称跟随活动单元格（“列，行：值”），其帮助文字给出地址，因此屏幕阅读器会播报每次移动。Avalonia 没有表格自动化模式，因此行和单元格不是独立节点。
- 编辑、排序、勾选、复制、粘贴、填充、撤销和重做都会在礼貌型实时区域中播报。
- 表头排序按钮会说明方向和优先级；全选框是三态复选框。
- 键入会以输入的文字开始编辑。对于中文或日文等输入法，请先按 Enter、F2 或双击，然后在编辑器中组字。
- Narrator、NVDA 和 VoiceOver 尚未针对此控件测试；请在每个平台上验证播报内容。

### 键盘

表格主体是焦点停留点。以下按键在未打开编辑器时生效；在 macOS 上 Ctrl 即 Cmd。

| 按键 | 操作 |
|---|---|
| 方向键 | 移动活动单元格，并将选区收缩到该单元格 |
| `Shift` + 方向键 | 从锚点单元格扩展选区 |
| `Ctrl` + 方向键 | 跳到该方向上的第一行或最后一行、第一列或最后一列；加上 Shift 可扩展选区 |
| `Home` / `End` | 移到该行的第一列或最后一列；`Ctrl+Home` 与 `Ctrl+End` 移到第一个和最后一个单元格 |
| `Ctrl+Space` | 将选区扩展到整列 |
| `Shift+Space` | 勾选选区内的行；若全部已勾选，则取消勾选 |
| `Enter` / `F2` | 编辑活动单元格并保留其值 |
| 可打印字符 | 开始编辑，并用输入的文字替换值 |
| `Alt+Down` | 在 `Select` 或 `Date` 列上打开选项列表或日历 |
| `Alt+Left` / `Alt+Right` | 在分组网格中，折叠或展开活动行所在的分组 |
| `F8` | 选中下一个被校验标出的单元格 |
| `Ctrl+Shift+加号` | 在活动行上方插入一行（需要 `CanInsertRows`） |
| `Shift+F10` / 菜单键 | 在活动单元格上打开右键菜单 |
| `Escape` | 将多单元格选区收缩为活动单元格；否则清除行的勾选 |
| `Delete` / `Backspace` | 清除选区；只读单元格会被跳过 |
| `Ctrl+D` | 从选区的第一行向下填充 |
| `Ctrl+F` / `Ctrl+H` | 打开查找，或查找与替换 |
| `F3` / `Shift+F3` | 下一个或上一个匹配 |
| `Ctrl+C` / `Ctrl+X` / `Ctrl+V` | 复制、剪切和粘贴 |
| `Ctrl+Z` / `Ctrl+Shift+Z` 或 `Ctrl+Y` | 撤销和重做 |
| `Tab` / `Shift+Tab` | 离开网格 |

编辑器打开时，Enter 与 Shift+Enter 提交并向下或向上移动，Tab 与 Shift+Tab 提交并向右或向左移动，Escape 取消。在表头上，Alt+Left 与 Alt+Right 以 16 px 为步长缩窄和加宽该列，Alt+Shift+Left 与 Alt+Shift+Right 移动该列。

## 动效

- 排序之后，行会在 700 ms 内滑动到新位置；从视图外进入的行会淡入。

| 变化 | 完整 | 减弱 | 关闭 |
|---|---|---|---|
| 选区与活动单元格 | 在范围之间弹簧式移动 | 直接跳到新范围 | 直接跳到新范围 |
| 排序后的行 | 在 700 ms 内滑到新位置 | 直接跳到新位置 | 直接跳到新位置 |
| 粘贴、填充、撤销或重做的单元格 | 0.9 秒的高亮波纹，之后淡出 | 短暂淡入 | 立即完成 |
| 工具栏批量状态与状态消息 | 模糊并上下切换 | 仅淡入 | 立即完成 |
| 合计、选区统计与已勾选数量 | 数字滚动 | 按计数规则滚动数字 | 立即完成 |
| 筛选行与分组箭头 | 弹簧展开，箭头旋转 | 高度直接变化，不透明度淡入 | 立即完成 |

## 相关组件

| 组件 | 适用场景 |
|---|---|
| `Table` | 记录是只读的，人们需要对其排序、选择和翻页 |
| `Input` / `Select` / `DatePicker` | 人们一次编辑一条记录，并需要逐个字段校验 |
| `Card` / `ReorderableList` | 每个项目都有自己的内容、图片或操作 |
| `TreeView` | 数据是层级结构，人们需要展开分支 |
| `BarChart` / `MetricCard` | 重点是数字或趋势，而不是单个行 |
