---
title: 树视图
summary: 浏览嵌套的文件夹与结构化内容。
source-hash: e2907dd4fd1e
---

<Demo name="FileTree" />

## 何时使用

- 文件夹、文件以及其他可展开、可选中的嵌套分类。
- 选中项驱动旁边内容显示的导航，例如 `CodeBlock`。
- 大型层级结构。行会被池化，只创建视口附近的行。

## 何时不用

- 少量可折叠的分区：用 `Accordion`。
- 页面层级中的当前位置：用 `Breadcrumb`。
- 一份 JSON 文档的键与值：用 `JsonViewer`。
- 用户可重新排序的扁平列表：用 `ReorderableList`。

## 用法

节点可以直接在 XAML 中内联声明。带有子项的 `TreeViewItem` 即为文件夹。

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

### 使用子项选择器绑定数据

绑定 `ItemsSource`，并在代码中设置选择器。`ChildrenSelector` 返回节点的子项，带有子项的节点即为文件夹。

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

### 图标

`TreeViewItem` 上的 `Icon` 接受一个 `Geometry`。`ShowIcons` 控制整棵树是否显示图标，`IconSelector` 为数据节点提供图标。

```csharp
tree.ShowIcons = true;
tree.IconSelector = item => ((FileNode)item).Children is null ? fileGlyph : folderGlyph;
```

### 从代码展开与折叠

```csharp
tree.ExpandAll();
tree.CollapseAll();
tree.ToggleExpanded(folderNode);
tree.ScrollIntoView(fileNode);
tree.FocusItem(fileNode);
```

### 事件

`ItemInvoked` 在一行被点击或按 Enter 激活时触发。`ItemExpanded` 和 `ItemCollapsed` 报告展开与折叠的文件夹。

```csharp
tree.ItemInvoked += (_, e) => OpenFile(e.Item);
tree.ItemExpanded += (_, e) => log.Text = $"已打开 {e.Item}";
```

### 数千个节点

这棵树有 40 个文件夹，每个文件夹含 250 个文件。行会被虚拟化并池化，只有视口附近的行会播放动画。

<Demo name="ThousandsOfNodes" />

## API 参考

<!-- api: TreeView -->

<!-- api: TreeViewItem -->

## 无障碍

- 树默认名称为"文件树"。设置 `AutomationProperties.Name` 可以按你的内容为其命名。
- 各行公开树项角色，并提供层级、在同级中的位置、展开状态和选中状态。
- 只有获得焦点的行处于 Tab 顺序中。方向键、Home 与 End、PageUp 与 PageDown 在行之间移动。
- 右方向键展开文件夹或进入其第一个子项，左方向键折叠文件夹或移到其父项。
- Enter 或 Space 选中该行并切换文件夹的展开状态。键入名称会移到下一个以该文字开头的行。
- 焦点环仅在键盘聚焦时绘制。图标与缩进参考线不会被播报。

## 动效

- 展开文件夹时，其空间以弹簧方式增长，新行从左侧依次错落进入。
- 选中高亮在各行之间平滑滑动，文件夹箭头以灵敏的弹簧转动。
- 折叠文件夹时，其行平滑收起。
- 减弱动效时行立即显示或隐藏。关闭动效时直接到达最终状态。

## 相关组件

- `Accordion`：用于少量可展开与收起的内容分区。
- `Breadcrumb`：显示当前页面在层级中的位置。
- `JsonViewer`：用于供人检查的嵌套数据，支持搜索与复制。
- `CodeBlock`：显示树中所选文件的源代码。
