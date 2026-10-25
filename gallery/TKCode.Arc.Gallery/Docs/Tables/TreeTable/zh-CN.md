---
title: 树表格
summary: 带嵌套行、三态选择、保持层级的排序和按需加载子行的表格。
source-hash: e68b6051b4a0
---

<Demo name="Budget" />

## 何时使用

`TreeTable` 用来展示可以嵌套、并共用同一组列的记录。一行可以展开显示它的子行，每一行保持相同的列，父行还能汇总其下的内容。人们展开自己关心的分支，在分支内部排序，并用一个复选框选中整个分支。

- **带有大小、所有者和日期列的文件列表**：文件夹展开后显示其中的文件，按大小排序时，每个文件夹内部的文件被重新排列，但不会被拉出所属的文件夹。
- **逐级汇总的预算与成本报表**：部门包含团队，团队包含明细项，页脚可以汇总用户选中的行。
- **组织架构与权限矩阵**：团队包含成员，每一行带有所有者、角色或数量。
- **按需加载的大型树**：可以把一行标记为“有尚未加载的子行”，表格在该行第一次展开时获取子行，请求失败时显示重试行。
- **对层级进行批量操作**：勾选父行会勾选其下的所有内容，只勾选了部分子行时，父行显示混合状态。

## 何时不用

- **没有列的导航树**：使用 `TreeView`，它每个节点只绘制一个标签，并能虚拟化数千行。
- **按列比较的扁平记录**：使用 `SortableDataTable`。在没有嵌套时，树表格给第一列增加缩进和箭头没有任何好处。
- **逐个单元格编辑的电子表格**：使用 `DataGrid`，它支持范围选择和行内编辑。
- **展开后是自由格式内容的条目**：使用 `Accordion`。树表格展开后得到的是更多具有相同列的行。
- **同时展开几千行**：每一个可见的行都真实存在并带有动画。展开的树请保持在几百行以内，更深的层级按需加载。

## 用法

把 `ItemsSource` 设为顶层行，把 `ChildrenSelector`（一个委托，在代码中设置）设为返回某一行的子行。选择器返回空列表的行是叶子。向 `Columns` 添加列；显示出来的第一列承载树。

```xml
<arc:TreeTable Caption="Files"
               ItemsSource="{Binding Files}"
               IsSelectable="True"
               MaxHeight="480">
    <arc:TreeTableColumn Header="Name" Binding="{Binding Name}" />
    <arc:TreeTableColumn Header="Size" Binding="{Binding SizeMb}" StringFormat="{}{0} MB"
                         Width="96" IsEndAligned="True" />
    <arc:TreeTableColumn Header="Owner" Binding="{Binding Owner}" HideBelow="520" />
</arc:TreeTable>
```

`TreeTable` 派生自 Avalonia 的 `TemplatedControl`。与 Arc 一样，它不做虚拟化：每个可见行都是真实的行，会长出、离开并带有动画。`ChildrenSelector`、`KeySelector` 这类选择器接收委托，因此在代码中设置；表格的其余部分可以用 XAML 编写。

```csharp
var table = new TreeTable
{
    ItemsSource = files,
    ChildrenSelector = item => ((FileNode)item).Children,
    KeySelector = item => ((FileNode)item).Path,
    RowLabelSelector = item => ((FileNode)item).Name,
    IsSelectable = true
};
table.Columns.Add(TreeTableColumn.Create<FileNode>("Name", f => f.Name));
table.Columns.Add(new TreeTableColumn { Header = "Size", ValueSelector = f => ((FileNode)f).SizeMb, IsEndAligned = true });
```

### 构成

1. **外框**：表面背景，1 px 的边框色边框和面板圆角。表格填满可用宽度。`MaxHeight` 限制整个表格的高度：表头保持固定，表体滚动。
2. **表头**：40 px 高，使用小号字体和弱化文字色，下方有一条 1 px 的分隔线。可排序的表头是一个按钮；第一列的表头还包含“全部展开”开关。
3. **行**：44 px 高，下方有一条细线。悬停和聚焦时行用弱化表面色填充，选中的行使用淡强调色。
4. **树单元格**：显示出来的第一列。它从 10 px 处开始，每一级缩进 20 px，内容前有一个 24 px 的箭头位。叶子保留这个空位，让标签对齐。
5. **缩进引导线**：穿过每个祖先箭头列的 1 px 线。指针下的分支或获得焦点的行所在的分支，其引导线会变深。
6. **单元格**：每个其他显示的列一个，左右内边距 12 px。右对齐的列把文字靠右，并使用等宽数字。
7. **复选框列**（`IsSelectable`）：44 px 宽，表头中有“全选”复选框。
8. **加载行与重试行**：在正在获取子行的父行下方，显示一行带旋转图标的加载行，其他列里是骨架条；或者显示“Couldn't load”行与重试按钮。

### 列

`TreeTableColumn` 定义一列。单元格的值来自 `ValueSelector`（委托，适合 AOT）或 `Binding`，再由 `Formatter` 或 `StringFormat` 转为文字。需要文字以外的内容时设置 `CellTemplate`；它的数据上下文是行项。

| 属性 | 用途 |
|---|---|
| `Header`、`Key` | 给列命名，并为 `SortKey` 提供名称（未设置 `Key` 时以表头文字作为键） |
| `Width` | 设置像素宽度（`96`）或剩余空间的份额（`2*`）；默认是 `1*`，它会收缩并截断 |
| `IsEndAligned` | 让数字和金额右对齐 |
| `HideBelow` | 当表格窄于这个像素值时隐藏该列 |
| `SortValueSelector`、`IsSortable` | 按显示文字以外的值排序，或关闭排序 |

各列共用同一组轨道，因此在每一级都对齐。当表格比某列的 `HideBelow` 更窄时，该列会从表头和每一行中移除；树所在的列始终保留。

### 行标识

`KeySelector` 返回一行的标识。`ExpandedKeys` 和 `SelectedKeys` 保存的就是这些键，所以请使用稳定的值，例如 id 或路径。未设置 `KeySelector` 时，行项本身就是键。

### 展开与收起

点击父行、点击它的箭头、按 Enter 以及方向键，都会切换它的展开状态。`ExpandedKeys` 是双向的：设置它可以在代码中展开行，也可以绑定到视图模型；读取它可以保存展开状态。表头的开关会展开所有已加载子行的行，或者全部收起。

<Demo name="Controlled" />

```csharp
table.Expand(item);
table.Collapse(item);
table.ExpandAll();          // every row whose children are known
table.CollapseAll();
table.FocusItem(item);      // moves keyboard focus to a visible row
```

### 选择

设置 `IsSelectable` 后，每一行都有一个复选框。勾选父行会勾选其下的所有内容，子行只被勾选一部分的父行显示混合状态。`SelectedKeys` 始终保存规范化后的集合：当且仅当某个父行的所有已知子行都被选中时，它才在集合中。只选中部分行时，全选复选框为混合状态。

`SelectionChanged` 报告新的键和已加载的行项。父行会汇总其子行，因此对选中内容求和时只应统计叶子。

```csharp
table.SelectionChanged += (_, e) =>
{
    var leaves = e.SelectedItems.Cast<Spend>().Where(s => s.Children is not { Count: > 0 });
    footer.Text = $"{leaves.Sum(s => s.Monthly):C0} per month";
};
```

### 排序

带有值的表头依次循环：升序、降序、关闭。排序对层级是安全的：每个父行的兄弟行在该父行内部排序，所以子行永远不会离开自己的分支。数字按数值比较，文字按自然顺序比较，空值在两个方向上都排在最后。`SortKey` 和 `SortDirection` 是双向的，因此可以在代码中设置或保存排序。

<Demo name="Files" />

### 按需加载子行

用 `HasChildrenSelector` 标记子行尚未加载的行，并给表格设置 `ChildrenLoader`。这样的行第一次展开时，表格调用加载器，并在其下方显示一行加载行。任务完成后，第一个子行接替加载行的位置。任务出错时，会出现带“重试”按钮的“Couldn't load”行，同时触发 `ChildrenLoadFailed`，在父行上按 Enter 也会重试。在已勾选的父行下到达的子行默认处于勾选状态。一行只加载一次：再次展开不会重新获取。

<Demo name="LazyLoading" />

```csharp
table.HasChildrenSelector = item => ((Account)item).HasChildren;
table.ChildrenLoader = async item => await api.GetChildrenAsync(((Account)item).Id);
```

### 激活一行

点击没有子行的行或在其上按 Enter 会触发 `RowActivated`，你可以在这里打开文件或记录。父行则会切换展开状态。

### 响应式列

表格适应的是它所得到的宽度，而不是窗口宽度。给最不重要的列（例如所有者或修改日期）设置 `HideBelow`，表格变窄时它们会依次消失。

### 示例：按团队统计的云支出

顶部的示例把支出逐级汇总了三层。所有者列在窄于 560 px 时隐藏，资源数列在窄于 420 px 时隐藏，月度列右对齐并格式化为货币，页脚统计选中的明细行。

### 内容准则

- 保持树所在列的文字简短。它会被截断，而第一列是人们寻找行的地方。
- 把人们要比较的数字放在右对齐的列里，并在表头或格式中写明单位，例如 `{0} MB`。
- 用 `RowLabelSelector` 为辅助技术命名行，例如文件名或团队名；它也是每个复选框的名称。
- 用 `EmptyMessage` 说明缺少什么，例如“此文件夹中没有文件”。

### 最佳实践

- 为每一行提供稳定的 `KeySelector`。基于索引的键会在数据重新排序时破坏展开状态。
- 用 `ChildrenLoader` 按需加载更深的层级，而不是预先加载数千行。
- 层级的汇总值请自己放在父行上；表格不会计算它们。
- 只在次要的列上使用 `HideBelow`，这样表格不会丢掉人们最先阅读的列。
- 对可能变得很高的表格设置 `MaxHeight`，让表头始终可见。

### 常见问题

**为什么父行无法展开？** 当选择器至少返回一个子行，或者 `HasChildrenSelector` 为 true 且设置了 `ChildrenLoader` 时，行才可以展开。空列表表示叶子。

**列可以把树排成平铺吗？** 不可以。排序只对每个父行内部的兄弟行排序，层级永远不会被破坏。平铺列表请使用 `SortableDataTable`。

**为什么重新加载数据后展开状态丢失了？** `ExpandedKeys` 保存的是键。如果 `KeySelector` 每次加载都返回新值，行就对不上了。请使用重新加载后依然不变的 id。

**表格会统计选中的内容吗？** 不会。请用 `SelectionChanged` 并自行累加需要的项。

## API 参考

<!-- api: TreeTable -->

<!-- api: TreeTableColumn -->

### 事件参数

| 类型 | 成员 | 含义 |
|---|---|---|
| `TreeTableExpandedChangedEventArgs` | `ExpandedKeys` | 所有已展开行的键 |
| `TreeTableSelectionChangedEventArgs` | `SelectedKeys`、`SelectedItems` | 选中的键，以及它们对应的已加载行项 |
| `TreeTableSortChangedEventArgs` | `Column`、`Direction` | 被排序的列（清除时为 null）与方向 |
| `TreeTableRowEventArgs` | `Item`、`Key`、`Error` | 对应的行；加载失败时设置 `Error` |

## 无障碍

- 表格默认名称为“Tree table”。设置 `Caption` 可以按你的内容命名。
- 行会提供名称、在集合中的位置、集合大小和展开状态。可排序的表头会报告排序方向。
- Tab 顺序中只有一行，方向键在行之间移动。
- 每个复选框都以 `RowLabelSelector` 命名，例如“Select Design”，并提供混合状态。
- 加载、加载失败、排序、全部展开和选择变化都通过礼貌型实时区域播报。
- 如果获得焦点的行因为其父行收起而消失，焦点会移到最近的可见祖先。
- 悬停填充只对鼠标指针生效；触摸点按会直接切换并激活行。

### 键盘

| 按键 | 操作 |
|---|---|
| `Up` / `Down` | 在可见行之间移动 |
| `Right` | 展开一行；在已展开的行上，移到它的第一个子行 |
| `Left` | 收起一行；否则移到它的父行 |
| `Home` / `End` | 移到第一个或最后一个可见行 |
| `PageUp` / `PageDown` | 移动十行 |
| `*` | 展开获得焦点的行的所有兄弟行 |
| `Space` | 切换该行的选择状态（需要 `IsSelectable`） |
| `Enter` | 切换父行、重试失败的加载，或激活叶子 |
| `Ctrl` / `Cmd` + `A` | 选中所有行，或清除选择（需要 `IsSelectable`） |

## 动效

- 行从零高度在平滑弹簧上长出并淡入，兄弟行之间有 18 ms 的错开；父行收起时它们会缩小消失。
- 加载行把自己的位置交给第一个到达的子行，所以不会出现行相互挤压的情况。
- 排序时每一行在弹簧上移动到新位置。
- 箭头在灵敏的弹簧上转动四分之一圈，排序箭头翻转，“全部展开”图标带着转动切换。
- 悬停与选中的填充以 160 ms 淡入。

| 变化 | Full | Reduced | Off |
|---|---|---|---|
| 一行展开或收起 | 高度弹簧、错开与淡入 | 短暂淡入，没有高度弹簧和错开 | 立即完成 |
| 排序 | 行在弹簧上移到新位置 | 行直接跳到新位置 | 立即完成 |
| 箭头与排序箭头 | 弹簧转动 | 立即完成 | 立即完成 |

## 相关组件

- `TreeView`：没有列的导航树。
- `SortableDataTable`：带可排序列的扁平记录。
- `DataGrid`：支持范围选择和行内编辑的电子表格式网格。
- `Checkbox`：每一行选择背后的二元控件。

| 组件 | 何时选择 |
|---|---|
| `TreeView` | 层级是导航，每个节点只有一个标签 |
| `SortableDataTable` | 记录是扁平的，并按列比较 |
| `DataGrid` | 用户要编辑单元格并选择范围 |
| `Accordion` | 少量区块展开后是自由格式的内容 |
