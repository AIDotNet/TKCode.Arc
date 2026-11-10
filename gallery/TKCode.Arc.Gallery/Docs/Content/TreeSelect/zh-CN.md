---
title: 树选择
summary: 在下拉树中选择一个节点或勾选多个节点，搜索时保留每个匹配项的路径，勾选父节点即勾选整个分支。
source-hash: 984cb4be884c
---

<Demo name="Preview" />

## 何时使用

- **文件或文件夹**：用于评审、上传目标或权限规则。
- **部门、团队和人员**放在一个字段里，勾选团队即选中其中所有人。
- **任意深度的分类**：用户输入关键字查找节点，而不是一级一级展开。

## 何时不用

- **简短的扁平列表**：使用 `Select` 或 `MultiSelect`。
- **固定 2 到 5 级、只有一个答案、需要看到同级项**：使用 `Cascader`。
- **树本身就是页面的主要内容**：使用 `TreeView`。
- **按需从服务器加载子节点**：本版本不支持。

## 用法

`TreeSelect` 派生自 Avalonia 的 `TemplatedControl`。把根节点交给 `ItemsSource`，并用委托读取每个节点，形式与 `Cascader`、`TreeView` 相同。不使用反射，因此支持 AOT 与裁剪。

```csharp
public sealed record FileNode(string Path, string Name, IReadOnlyList<FileNode>? Children = null, bool IsLocked = false)
{
    public bool IsFolder => Children is not null;
}

treeSelect.ItemsSource = files;
treeSelect.ChildrenSelector = node => ((FileNode)node).Children;
treeSelect.TextSelector = node => ((FileNode)node).Name;
treeSelect.ValueSelector = node => ((FileNode)node).Path;   // unique across the tree
treeSelect.IconSelector = node => ((FileNode)node).IsFolder ? ArcIcons.Folder : ArcIcons.File;
treeSelect.DisabledSelector = node => ((FileNode)node).IsLocked;
```

### 选择一个节点

`SelectionMode="Single"`（默认）选中一个节点后关闭。`SelectedItem` 与 `SelectedValue` 都是双向的；`ShowFullPath` 显示以 `PathSeparator` 连接的完整路径。

<Demo name="Single" />

### 只选叶子

`LeafOnly="True"`：点击父节点只会展开或收起它。

<Demo name="LeafOnly" />

### 勾选多个节点

`SelectionMode="Multiple"` 为每一行加上复选框，菜单保持打开。勾选父节点会勾选其所有可用的后代；部分勾选的父节点显示短横线。禁用节点保持原状态。`CheckStrategy` 决定 `SelectedItems` 与 `SelectedValues` 报告哪些节点：

| `CheckStrategy` | 报告 |
|---|---|
| `Leaves`（默认） | 已勾选的叶子 |
| `Parents` | 用全选的父节点代替其整个子树 |
| `All` | 所有全选的节点 |

<Demo name="CheckStrategy" />

`SelectedValues` 是双向的：设置它会勾选对应节点（父节点的值会勾选其可用的后代），之后它保存按策略报告的结果。数据到达之前设置的值会在数据到达时生效。选中的节点显示为标签，超过 `MaxVisibleChips`（3）时折叠为 "+N"。

### 搜索

`IsSearchable`（默认 true）：在打开的字段中输入即可过滤树。匹配项保留并展开其祖先，匹配的文字会被标记；匹配的父节点保留其子节点。清空搜索后恢复原来的展开状态。`FilterPredicate` 可替换默认的不区分大小写的包含匹配。`SearchText` 是输入的文字（双向）。

### 大型树

行是虚拟化的：只创建屏幕上的行。`ExpandDepth` 设置数据到达时展开的层数（`-1` 全部展开）。

<Demo name="LargeTree" />

## API 参考

<!-- api: TreeSelect -->

<!-- api: TreeSelectSelectionChangedEventArgs -->

<!-- api: TreeSelectMode -->

<!-- api: TreeSelectCheckStrategy -->

## 无障碍

- 字段是可展开、收起的组合框；`Label` 是名称，`Description` 是帮助文本。它的值是 `SelectedText`（单选）或"已选 N 项"（多选）。
- 行是树项，支持展开 / 收起、选择，多选时还有三态切换。
- 移动高亮时会朗读节点、层级和勾选状态。
- "无匹配项"、"清除选择"和朗读内容跟随应用语言（en-US、zh-CN、zh-TW）。

### 键盘

| 按键 | 时机 | 操作 |
|---|---|---|
| Enter、Space、Down、Up | 关闭时 | 打开并定位到选中项 |
| 任意字符 | 可搜索时 | 搜索（打开菜单） |
| Delete、Backspace | 关闭且有选中项时 | 清除 |
| Up、Down、Home、End、Page Up、Page Down | 打开时 | 移动高亮 |
| Right | 打开时 | 展开父节点，或移到其第一个子节点 |
| Left | 打开时 | 收起父节点，或移到父节点 |
| Enter | 打开时 | 选中（单选）或切换勾选（多选） |
| Space | 打开且搜索为空时 | 选中（单选）或切换勾选（多选） |
| Backspace | 打开、多选且搜索为空时 | 删除最后一个标签 |
| Escape | 打开时 | 先清空搜索，再关闭 |

## 动效

- 菜单的打开与关闭与 `Cascader` 相同；标签的进出与 `MultiSelect` 相同。
- 父节点的行依次展开、展开箭头旋转，与 `TreeView` 相同；勾选标记自行绘制并变形为短横线，与 `Checkbox` 相同。高亮在行之间滑动。
- 减弱动效时展开箭头不旋转，行和勾选状态立即变化。搜索结果总是立即出现。

## 响应式

- 没有断点。字段占用给定的宽度（至少 200 px）。
- 菜单与字段同宽，至少 240 px；过长的标签以省略号截断。
- 树在 `MaxDropDownHeight`（320 px）内滚动。下方空间不足时菜单翻到字段上方。

## 相关组件

- `Cascader`：逐级选择一个节点。
- `MultiSelect`：从扁平列表中选择多个值。
- `TreeView`：页面上的树。
