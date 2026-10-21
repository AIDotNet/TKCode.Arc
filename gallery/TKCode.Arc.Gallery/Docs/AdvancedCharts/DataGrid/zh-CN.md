---
title: 数据表格
summary: 电子表格式的表格：范围选择、就地编辑、填充柄、剪贴板、撤销、排序、筛选、分组、合计与导出。
source-hash: 81a7289e5a82
---

<Demo name="Renewals" />

## 何时使用

- 像电子表格一样就地修改大量数值，例如预算、价目表或库存。
- 需要通过复制粘贴与 Excel 或 Google Sheets 往来的数据。
- 需要范围选择、填充柄、向下填充和撤销的工作。
- 数万行以内、需要筛选、排序、分组小计和合计的表格。
- 无法一次加载的服务端数据，通过数据源按块读取。
- 对勾选行的批量操作：复制、导出、删除或自定义操作。

## 何时不用

- 只读记录，只需排序和行选择：用 `SortableDataTable`。
- 一次编辑一条记录：用 `Input`、`Select` 等字段组成的表单。
- 每条记录需要图片或长文本等丰富内容：用卡片或列表。

## 用法

列通过 `Key` 对应行的字段。行可以是 `DataGridRow` 对象（一个键加若干具名字段），也可以是任意类，通过列的绑定或访问器读取。每一行都需要唯一且稳定的键。请保存原始值：数字存为数字，日期存为 `DateOnly`，不要存格式化后的文字。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:DataGrid Label="Renewals" MaxViewportHeight="440" ExportFormats="All">
        <arc:DataGridColumn Key="account" Header="Account" Width="188" IsPinned="True" />
        <arc:DataGridColumn Key="stage" Header="Stage" Type="Select" />
        <arc:DataGridColumn Key="arr" Header="ARR" Type="Currency" />
        <arc:DataGridColumn Key="health" Header="Health" Type="Percent" />
        <arc:DataGridColumn Key="renewal" Header="Renews" Type="Date" />
    </arc:DataGrid>
</UserControl>
```

```csharp
grid.ItemsSource = new ObservableCollection<DataGridRow>
{
    new("acct-1") { { "account", "Lumen Labs" }, { "stage", "Proposal" }, { "arr", 48000m }, { "renewal", new DateOnly(2026, 9, 30) } }
};
```

### 列类型

`Type` 决定一列如何读取、显示和汇总它的值。`Number`、`Currency` 和 `Percent` 右对齐，能解析输入的数字（可带货币符号、百分号、分组符和会计括号），并在合计行中求和（百分比求平均）。`Select` 从 `Options` 中选择，选项顺序也是排序顺序。`Date` 保存 `DateOnly`，用界面语言的月份名称或 `DateFormat` 显示，用日历编辑，并拒绝 01/02/2026 这类各国读法不同的纯数字写法。`Decimals` 和 `CurrencySymbol` 控制数字格式；货币符号在任何界面语言下都保持不变。

```xml
<arc:DataGrid Label="Price list">
    <arc:DataGridColumn Key="price" Header="Price" Type="Currency" CurrencySymbol="€" Decimals="2" />
    <arc:DataGridColumn Key="margin" Header="Margin" Type="Percent" Decimals="1" Aggregate="Min" />
    <arc:DataGridColumn Key="due" Header="Due" Type="Date" DateFormat="yyyy-MM-dd" />
</arc:DataGrid>
```

### 编辑、剪贴板与撤销

直接输入会替换原值；按 Enter 或 F2（或双击）则保留原值进入编辑。Enter 和 Tab 提交并移动；无法解析的值会让编辑器保持打开并抖动一下。复制会同时写入制表符分隔的文本和 HTML 表格，因此 Excel 和 Google Sheets 能识别其中的数字；粘贴两种格式都能读取，选区恰好是剪贴板内容整数倍时会平铺填满。拖动填充柄可以延续序列（数字按步长，日期按天或按月），按 Ctrl+D（Cmd+D）向下填充。撤销保留 `UndoLimit` 步（默认 100）。计算列或锁定的列设为 `IsEditable="False"`：编辑、粘贴、填充和清空都会跳过它们。

<Demo name="Budget" />

`CellEditEnding` 让应用可以拒绝某个值，`CellsChanged` 报告每个被写入的单元格及其新旧值和写入原因。编辑值不会重新排序或筛选，行会停在原位，直到排序或筛选改变。

```csharp
grid.CellEditEnding += (_, e) =>
{
    if (e.NewValue is decimal amount && amount < 0)
    {
        e.Cancel = true;
        e.ErrorMessage = "ARR cannot be negative";
    }
};
grid.CellsChanged += (_, e) => SaveChanges(e.Changes);
```

### 使用自己的类作为行

给列设置编译绑定 `Binding`（编辑会双向写回），或用 `DataGridColumn.Create` 提供访问器，后者兼容 AOT、不需要绑定。只有取值函数、没有赋值函数的列是只读的。

<Demo name="TypedRows" />

```csharp
grid.Columns.Add(DataGridColumn.Create<Account>("arr", "ARR", a => a.Arr, (a, v) => a.Arr = v as decimal? ?? 0,
    DataGridColumnType.Currency));
grid.Columns.Add(DataGridColumn.Create<Account>("total", "Total", a => a.Arr * a.Seats, type: DataGridColumnType.Currency));
```

### 排序、筛选与搜索

单击表头依次升序、降序、取消排序；按住 Shift 单击会把该列追加为下一个排序键。列菜单可以排序、筛选、分组、固定和隐藏。筛选行接受文本（`=` 表示完全相等，`!` 表示排除）、数字（`>100`、`10..50`）、日、月或年（`> 2026-03`、`2026-01-01..2026-03-31`），枚举列则用值勾选清单。快速搜索要求每个词都出现在某个可见列中。

### 视图状态

排序、筛选、搜索、勾选行、隐藏和固定的列、行密度、分组以及筛选行的开合都是双向属性，视图模型可以保存和恢复视图，也可以从外部设置。

<Demo name="ControlledState" />

```xml
<arc:DataGrid SortDescriptions="{Binding Sort}"
              Filters="{Binding Filters}"
              CheckedRowKeys="{Binding Checked}"
              Density="{Binding Density}" />
```

### 分组与合计

`GroupBy` 接受列键，外层在前。分组行显示折叠按钮、分组值、行数，以及汇总方式不为 `None` 的各列小计；同一汇总方式也用于合计行，合计行始终统计筛选后保留的全部行。导出 Excel 时，分组写成带 SUBTOTAL 公式的大纲行。

<Demo name="Ledger" />

### 勾选行与批量操作

在序号列勾选行（按住 Shift 单击可勾选一段，Shift+Space 勾选选区所在的行）。工具栏随后提供复制、导出、你的 `BulkActions` 和删除；删除可以在状态栏撤销。

```xml
<arc:DataGrid Label="Accounts">
    <arc:DataGrid.BulkActions>
        <arc:DataGridBulkAction Label="Archive" Icon="{x:Static arc:ArcIcons.Archive}" Command="{Binding ArchiveCommand}" />
    </arc:DataGrid.BulkActions>
</arc:DataGrid>
```

### 导出

`ExportFormats` 列出可选的 CSV 和 Excel。导出按视图顺序写出筛选后保留的全部行（批量操作里的导出只写勾选行）；`Exporting` 可以改文件名或取消导出。`ExportAsync` 用于从代码导出。CSV、xlsx、剪贴板、解析和筛选函数都是公开的静态辅助类（`DataGridCsv`、`DataGridXlsx`、`DataGridClipboardText`、`DataGridValueParser`、`DataGridFilterExpression`、`DataGridSorting`），也可以在服务端使用。

```csharp
grid.Exporting += (_, e) => e.FileName = $"renewals-{DateTime.Today:yyyy-MM-dd}";
await grid.ExportAsync(DataGridExportFormat.Xlsx);
```

### 服务端数据

数据太大、无法一次传来时，设置 `DataSource`：表格会请求覆盖可见行的数据块（加载中显示占位行），最多缓存 40 块，并在排序、筛选或搜索变化时取消进行中的请求、重新开始。此时表格只读，也不分组。

<Demo name="ServerData" />

### 加载与空状态

`IsLoading` 显示六行占位行。`EmptyMessage` 替换"暂无数据"；筛选把所有行都隐藏时，表格会提示并提供"清除筛选"。

<Demo name="States" />

## API 参考

<!-- api: DataGrid -->

<!-- api: DataGridColumn -->

<!-- api: DataGridBulkAction -->

## 无障碍

- 表格主体是一个可聚焦元素。方向键、Shift+方向键、Ctrl+方向键（macOS 上为 Cmd）、Home、End、PageUp 和 PageDown 移动和扩展选区；Tab 离开表格。
- 主体的无障碍名称跟随活动单元格（"列名，行名：值"），帮助文本给出地址，因此屏幕阅读器会播报每次移动。Avalonia 没有表格类的自动化模式，所以行和单元格不是独立的自动化元素。
- 编辑、排序、勾选、复制、粘贴、填充、撤销和重做都会在礼貌型实时区域播报。
- 分组时，Alt+← 折叠活动行所在的分组，Alt+→ 再次展开；会播报分组及其行数。
- 表头的排序按钮名称包含排序方向、优先级和调整列宽的按键（Alt+方向键）。全选框是三态复选框。
- 直接输入会带着输入的文字开始编辑。使用输入法（中文、日文）时，请先按 Enter、F2 或双击进入编辑，再在编辑器里组字。

## 动效

- 选区和活动单元格在不同范围之间用弹簧移动；滚动和调整列宽时直接到位。
- 排序后，行滑到新位置，从视图外进来的行淡入。
- 粘贴、填充、撤销和重做的单元格闪一下，并在 0.9 秒内淡出。
- 工具栏切换到批量操作时短暂上升并淡入；合计、统计和勾选数用滚动数字显示。
- 无效编辑抖动一次，筛选行用弹簧展开，分组箭头随之旋转。
- 减弱动效时选区直接到位，去掉行滑动和抖动，保留短暂的淡入淡出。关闭动效时所有变化立即显示。

## 相关组件

- `SortableDataTable`：只读记录。
- `FilterToolbar`：列表或表格上方的筛选工具栏。
- `DatePicker` 和 `Calendar`：日期编辑器使用的日历。
- `AnimatedCounter`：合计使用的滚动数字。
