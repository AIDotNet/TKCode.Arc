---
title: 树视图
summary: 浏览嵌套的文件夹与结构化内容。
source-hash: 3a486b8c5441
---

<Demo name="FileTree" />

## 何时使用

`TreeView` 用于展示人们可以展开、收起和选中的嵌套记录：文件夹与文件、会计科目表、分类层级。它位于页面的导航区域，树中的选择通常会驱动旁边的内容，例如预览、表单或代码查看器。行由扁平的虚拟化列表绘制，因此大型层级结构依然保持流畅。

- **开发者工具中的仓库与文件浏览器**：文件夹与文件可以展开和收起，选中的文件显示在树旁边的 `CodeBlock` 中。
- **内部后台中的文档与知识库导航**：章节可嵌套多层，选中某个页面后会在编辑区加载它。
- **财务应用中的会计科目与成本中心**：父科目展开后显示子科目，选中的科目驱动分类账视图。
- **人事或管理后台中的组织与团队结构**：部门包含团队，团队包含成员，选中成员后打开其个人资料。
- **包含数千个文件的文件夹的媒体与素材库**：只有视口附近的行会被创建，因此树仍然可以流畅使用。

## 何时不用

- **几段需要从上到下阅读的可折叠内容**：用 `Accordion`。
- **页面层级中的当前位置**：用 `Breadcrumb`，它显示路径并可返回上级。
- **一份 JSON 文档的键与值**：用 `JsonViewer`，它在键旁显示值，并支持搜索和复制。
- **用户会重新排序的扁平列表**：用 `ReorderableList`。树的同级节点之间没有拖动排序。
- **人们按列比较、字段相同的记录**：用 `Table`。树每个节点只显示一个标签，没有列。

## 用法

节点可以直接在 XAML 中内联声明。带子项的 `TreeViewItem` 即为文件夹。

```xml
<arc:TreeView MinWidth="260" ShowIndentGuides="True">
    <arc:TreeView.Items>
        <arc:TreeViewItem Header="src" IsExpanded="True">
            <arc:TreeViewItem.Items>
                <arc:TreeViewItem Header="components">
                    <arc:TreeViewItem.Items>
                        <arc:TreeViewItem Header="button.tsx" />
                        <arc:TreeViewItem Header="tree-view.tsx" />
                    </arc:TreeViewItem.Items>
                </arc:TreeViewItem>
                <arc:TreeViewItem Header="lib" />
            </arc:TreeViewItem.Items>
        </arc:TreeViewItem>
        <arc:TreeViewItem Header="README.md" />
    </arc:TreeView.Items>
</arc:TreeView>
```

`TreeView` 派生自 Avalonia 的 `TemplatedControl`。它不使用 Avalonia 的嵌套项容器：它保留一个扁平的池化行列表，因此展开文件夹只需为其展示的行付出开销，树的其余部分不会重新布局。内联 `Items` 适合你在 XAML 中编写的固定导航树。对于来自服务或数据库的数据，请绑定 `ItemsSource`；两者同时设置时，`ItemsSource` 优先。

### 结构

1. **外框**：表面背景，1 px 边框（边框色），16 px 圆角，内边距为 8、6、8、8 px。将 `HorizontalAlignment` 设为 `Stretch`（默认值）时，树会填满其宽度。
2. **行**：高 36 px，下方 2 px 间距，因此每行占用 38 px 的空间。行的圆角为 10 px，左右内边距为 10 px。
3. **缩进**：每级 18 px。设置 `ShowIndentGuides` 后，一条使用边框色的短分支线会把嵌套行连接到其父级。
4. **箭头**：文件夹上的箭头，文件夹展开时箭头会转动。
5. **图标**（`ShowIcons`、`IconSelector`、`TreeViewItem.Icon`）：标签前 16 px 的位置。文件夹显示展开状态的文件夹图标；文件显示文件图标。`Icon` 与 `IconSelector` 接受 24 × 24 的图形，例如 `ArcIcons` 的成员。`ShowIcons` 为 false 时，标签从缩进处开始。
6. **标签**（`Header`、`HeaderSelector`）：行文字，使用次要文字颜色。当前活动的行使用前景色。
7. **行填充**：悬停填充与选中填充，均使用弱化的表面色。选中填充会从一行平滑滑向另一行。
8. **文件色调**：`.ts` 与 `.tsx` 文件，以及 `.css`、`.md`、`.json` 文件，带有文件类型色调。其他文件使用弱化的文字颜色。
9. **焦点环**：由控件绘制，仅用于键盘焦点。

### 选择内联项或数据源

| 来源 | 适用情况 | 子项 |
|---|---|---|
| 内联 `Items` | 树是固定的导航，规模较小，并在 XAML 中编写 | 嵌套的 `TreeViewItem` 元素 |
| `ItemsSource` | 节点来自数据，并在运行时变化 | `ChildrenSelector` 返回每个节点的子项 |

### 使用子项选择器绑定数据

绑定 `ItemsSource`，并在代码中设置选择器。`ChildrenSelector` 返回节点的子项，带子项的节点即为文件夹。

<Demo name="DataWithChildrenSelector" />

```csharp
var tree = new TreeView
{
    ItemsSource = nodes,
    ChildrenSelector = item => ((FileNode)item).Children,
    HeaderSelector = item => ((FileNode)item).Name,
    Width = 300
};
tree.SelectionChanged += (_, e) => selected.Text = $"已选中项：{e.NewItem}";
tree.SelectedItem = nodes[0].Children![2];   // 会展开该节点的祖先节点
```

`SelectedItem` 为双向绑定，因此可以绑定到视图模型：

```xml
<arc:TreeView ItemsSource="{Binding Files}"
              SelectedItem="{Binding SelectedFile, Mode=TwoWay}" />
```

### 绑定到视图模型

在 MVVM 应用中，视图模型拥有节点数据与选择，树只负责呈现它们。示例使用 CommunityToolkit.Mvvm。节点是普通的记录，视图模型负责加载树并保存选中的节点。

```csharp
public sealed record FileNode(string Name, IReadOnlyList<FileNode>? Children = null)
{
    public override string ToString() => Name;
}

public interface IRepositoryService
{
    Task<IReadOnlyList<FileNode>> GetTreeAsync();
}

public partial class WorkspaceViewModel : ObservableObject
{
    private readonly IRepositoryService _repository;

    public WorkspaceViewModel(IRepositoryService repository) => _repository = repository;

    [ObservableProperty]
    private ObservableCollection<FileNode> _files = [];

    [ObservableProperty]
    private FileNode? _selectedFile;

    [RelayCommand]
    private async Task LoadAsync() => Files = new ObservableCollection<FileNode>(await _repository.GetTreeAsync());
}
```

```xml
<arc:TreeView ItemsSource="{Binding Files}"
              SelectedItem="{Binding SelectedFile, Mode=TwoWay}"
              ShowIcons="True" />
```

选择器是函数，XAML 无法表达，因此请在视图的代码后置文件中设置一次：

```csharp
Tree.ChildrenSelector = item => ((FileNode)item).Children;
Tree.HeaderSelector = item => ((FileNode)item).Name;
```

- `SelectedFile` 为双向绑定。从视图模型设置它会选中该节点并展开其祖先节点，因此指向某个文件的深层链接会显示该文件。
- 刷新后请使用相同的节点实例，否则选择与展开的文件夹将与行不再一致。当服务返回新对象时，按路径找到选中的节点，并赋值为新的实例。
- 编译绑定需要在视图上设置 `x:DataType`（示例为求简洁省略了它）。

### 图标

`ShowIcons` 控制整棵树是否显示图标，`IconSelector` 为每个数据节点提供图标。`TreeViewItem` 则使用它自己的 `Icon`。两者都接受 24 × 24 的描边几何图形，`ArcIcons` 的成员通常是它们的来源。

```csharp
tree.ShowIcons = true;
tree.IconSelector = item => ((FileNode)item).Children is null ? ArcIcons.File : ArcIcons.Folder;
```

### 从代码展开与折叠

```csharp
tree.ExpandAll();
tree.CollapseAll();
tree.ToggleExpanded(folderNode);
tree.ScrollIntoView(fileNode);
tree.FocusItem(fileNode);
```

`ScrollIntoView` 与 `FocusItem` 会先展开节点的祖先，使人们尚不可见的节点变得可见。`IsItemExpanded` 报告文件夹是否已展开。对于内联的 `TreeViewItem`，`IsExpanded` 为双向绑定，树会保持它同步。

### 事件

`ItemInvoked` 在一行被点击、按 Enter 或 Space 激活时触发，包括已被选中的行。`SelectionChanged` 报告选中节点的变化。`ItemExpanded` 与 `ItemCollapsed` 报告展开与收起的文件夹。激活文件夹会选中它并切换其展开状态，因此通过点击展开的文件夹同样会触发 `ItemExpanded`。

```csharp
tree.ItemInvoked += (_, e) => OpenFile(e.Item);
tree.ItemExpanded += (_, e) => log.Text = $"已打开 {e.Item}";
```

### 数千个节点

这棵树有 40 个文件夹，每个文件夹含 250 个文件。行会被虚拟化并池化，只有视口附近的行会被实例化，因此展开一个文件夹只需付出约一屏行的开销，树的其余部分不会被构建。

<Demo name="ThousandsOfNodes" />

### 示例：文档仓库浏览器

文档团队浏览其组件文档的仓库。树位于左侧窗格，右侧窗格中的 `CodeBlock` 显示选中的文件，如页面顶部的文件树示范所示。默认展开两个文件夹，使读者一眼看出结构，而团队很少编辑的文件夹保持收起。文件色调无需额外的列，即可显示哪些文件是代码、样式、文档和配置。

![带有两个展开文件夹和多种类型文件的文档仓库树](images/DocsRepository.png)

```xml
<arc:TreeView Width="320"
              HorizontalAlignment="Left"
              ShowIndentGuides="True"
              ShowIcons="True">
    <arc:TreeViewItem Header="acme-docs" IsExpanded="True">
        <arc:TreeViewItem.Items>
            <arc:TreeViewItem Header="guides" IsExpanded="True">
                <arc:TreeViewItem.Items>
                    <arc:TreeViewItem Header="getting-started.md" />
                    <arc:TreeViewItem Header="theming.md" />
                </arc:TreeViewItem.Items>
            </arc:TreeViewItem>
            <arc:TreeViewItem Header="api" IsExpanded="True">
                <arc:TreeViewItem.Items>
                    <arc:TreeViewItem Header="tree-view.tsx" />
                    <arc:TreeViewItem Header="tree-view.module.css" />
                    <arc:TreeViewItem Header="motion-tokens.ts" />
                </arc:TreeViewItem.Items>
            </arc:TreeViewItem>
            <arc:TreeViewItem Header="examples">
                <arc:TreeViewItem.Items>
                    <arc:TreeViewItem Header="basic-tree.tsx" />
                </arc:TreeViewItem.Items>
            </arc:TreeViewItem>
            <arc:TreeViewItem Header="package.json" />
            <arc:TreeViewItem Header="README.md" />
        </arc:TreeViewItem.Items>
    </arc:TreeViewItem>
</arc:TreeView>
```

### 内容规范

节点标签是人们会搜索的名称，因此应按源数据中保存的原样显示。

| 不要写 | 改为 |
|---|---|
| 未命名的文件夹 | 文件夹的真实名称；创建过程中显示“新建文件夹” |
| 文档（42 个文件） | 文档；数量显示在页面的其他位置 |
| 点击以打开文件 | 无文字；行本身就是控件 |
| 文件 | 项目文件，或仓库的名称 |
| 项目 1 | 文件名，例如 theming.md |

- 标签保持为存储的名称，包括大小写与扩展名，使人们能够与仓库中的名称对应。
- 树的无障碍名称应具体，例如“仓库文件”或“会计科目表”，而不是“树”。

### 最佳实践

**推荐**

- 默认展开前一到两级，使人们无需寻找即可看到结构。
- 层级超过两级的树使用 `ShowIndentGuides`，使每一行的父级易于辨认。
- 刷新时保持相同的节点实例，使选择和展开的文件夹与行保持一致。
- 将 `SelectedItem` 设为双向绑定，使深层链接或搜索结果能从视图模型选中节点。
- 当树位于包含其他内容的页面中时，用 `MaxHeight` 限制其高度，使滚动区域就是树本身。

**避免**

- 用树来展示几段文章：`Accordion` 的阅读体验更好，也不需要键盘模型。
- 把大小和日期等数据列放进标签中。记录请使用 `Table`。
- 期望多选。树只有一个 `SelectedItem`，修饰键也不会选中一个范围。
- 默认展开过多层级。同时展开的深层文件夹会使结构难以扫视。
- 每次按键都从数据的新副本重建树。应在明确的操作后刷新。

### 常见问题

**为什么有子项的节点不显示其子项？** `ItemsSource` 需要 `ChildrenSelector`。没有它时，每个节点都会被当作文件。从选择器返回子项集合；对于文件，返回 null 或空列表。

**如何将树打开到某个特定文件？** 将 `SelectedItem` 设为该节点。树会展开其祖先并将该行滚动到可见位置。若只想显示节点而不选中它，请使用 `ScrollIntoView`。

**树能选中多个节点吗？** 不能。`SelectedItem` 只保存一个节点，键盘会忽略 Control、Alt 和 Meta 等修饰键。当人们必须选择多条记录时，请使用设置了 `IsSelectable` 的 `Table`。

**树会对节点排序吗？** 不会。它按你提供的顺序显示节点，因此绑定前请先对源数据排序。

## API 参考

<!-- api: TreeView -->

<!-- api: TreeViewItem -->

### 事件参数

| 类型 | 成员 | 含义 |
|---|---|---|
| `TreeViewItemEventArgs` | `Item` | 节点：一个 `TreeViewItem` 或 `ItemsSource` 中的一项 |
| `TreeViewSelectionChangedEventArgs` | `OldItem`、`NewItem` | 先前选中的节点和新选中的节点 |

## 无障碍

- 树默认名称为“文件树”。设置 `AutomationProperties.Name` 可按你的内容为其命名。
- 各行公开树项角色，并提供层级、在同级中的位置、展开状态和选中状态。
- 只有获得焦点的行处于 Tab 顺序中。方向键、Home 与 End、PageUp 与 PageDown 在行之间移动。
- 右方向键展开文件夹或进入其第一个子项，左方向键收起文件夹或移到其父行。
- Enter 或 Space 选中该行并切换文件夹的展开状态。键入名称会移到下一个以该文字开头的行。
- 焦点环仅为键盘焦点而绘制。图标和缩进参考线不会被朗读。

### 键盘

| 按键 | 操作 |
|---|---|
| `Up` / `Down` | 移到上一行或下一行可见行 |
| `Right` | 展开已收起的文件夹；在已展开的文件夹上，移到其第一个子项 |
| `Left` | 收起已展开的文件夹；否则移到父行 |
| `Home` / `End` | 移到第一行或最后一行可见行 |
| `PageUp` / `PageDown` | 大约移动一屏的行数 |
| `Enter` / `Space` | 选中该行，触发 `ItemInvoked`，并切换文件夹的展开状态 |
| 字母键 | 移到下一个标签以所键入文字开头的行 |

树会忽略使用 Control、Alt 或 Meta 的按键，因此窗口的快捷键仍然有效。

## 动效

- 展开文件夹时，其空间以弹簧增长，新行从左侧依次错落进入。
- 选中高亮在各行之间平滑滑动，文件夹箭头以灵敏的弹簧转动。
- 收起文件夹时，其行平滑收拢。
- 减弱动效时，行立即显示或隐藏。关闭动效时直接到达最终状态。

| 变化 | 完整 | 减弱 | 关闭 |
|---|---|---|---|
| 文件夹展开或收起 | 空间以弹簧增长或收拢；新行错落进入 | 行立即显示或隐藏 | 立即完成 |

选中高亮仅在完整动效下于各行之间滑动。

## 相关组件

- `Accordion` 用于可展开与收起的少量内容分区。
- `Breadcrumb` 用于显示当前页面在层级中的位置。
- `JsonViewer` 用于供人检查的嵌套数据，支持搜索与复制。
- `CodeBlock` 用于显示树中所选文件的源代码。

| 组件 | 适用场景 |
|---|---|
| `Accordion` | 少量文章分区需要展开和收起，且顺序固定 |
| `Breadcrumb` | 页面需显示其在层级中的位置，并可返回上级 |
| `JsonViewer` | 嵌套数据是人们需要检查和复制的一份文档 |
| `CodeBlock` | 所选文件的源代码需与树并排显示 |
| `Table` | 记录的字段相同，人们需要按列比较 |
| `ReorderableList` | 人们通过拖动对扁平列表排序 |
