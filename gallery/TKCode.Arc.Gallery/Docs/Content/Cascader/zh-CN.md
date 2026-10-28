---
title: 级联选择器
summary: 按层级逐级选择一个节点，如省、市、区，字段和菜单与 Select 一致。
source-hash: cf03d1ad0529
---

<Demo name="Basic" />

## 何时使用

`Cascader` 在层级数据中选择一个节点。打开后先显示第一级；选中一个父节点，它的子级会在右侧的一列中展开，最后选中的是叶子节点。字段随后显示完整路径。适用于层级固定、共 2 到 5 级、并且用户需要看到每一级兄弟项的场景。

- **地址表单中的省、市、区**：三级合起来才是一个答案，保存的就是完整路径。
- **商品目录编辑器中的分类**：例如“电子产品 / 手机 / 智能手机”。
- **组织架构中的职位**：在一个字段里选出部门和小组。

## 何时不用

- **很深的树，或某一级有上千个节点**：把 `TreeView` 放进 `Popover`，或使用可搜索的 `Combobox`。
- **只有一级**：使用 `Select` 或 `Combobox`。
- **一次选多个值**：多选版本不属于 `Cascader`。
- **跨层级搜索，或按需加载子级**：本版本不支持。

## 用法

`Cascader` 派生自 Avalonia 的 `TemplatedControl`。把根节点交给 `ItemsSource`，再用委托告诉它如何读取节点：`ChildrenSelector`（与 `TreeView` 的签名相同）、`TextSelector` 和 `ValueSelector`。不使用反射，因此兼容 AOT 和裁剪。

```csharp
public sealed record Region(string Code, string Name, IReadOnlyList<Region>? Children = null);

cascader.ItemsSource = regions;
cascader.ChildrenSelector = node => ((Region)node).Children;
cascader.TextSelector = node => ((Region)node).Name;
cascader.ValueSelector = node => ((Region)node).Code;
```

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:Cascader Label="Region"
                  Description="Pick a province, a city and a district."
                  Placeholder="Select a district"
                  Width="320" />
</UserControl>
```

没有子节点的节点，或被 `IsLeafSelector` 判定为叶子的节点，就是叶子。字段、菜单表面、滑动高亮和清除按钮与 `Select`、`Combobox` 相同。

### 选择父节点

默认只能选叶子：点击父节点只会展开它的子级。把 `LeafOnly` 设为 `False`，任何一级都可以被选中。此时点击父节点会选中它，并保持菜单打开，用户仍然可以继续往下选。

<Demo name="ParentSelectable" />

### 字段显示什么

`ShowFullPath`（默认为 true）显示完整路径，节点之间用 `PathSeparator`（" / "）连接。设为 `False` 则只显示选中的节点。`SelectedText` 就是字段显示的文本。

<Demo name="LeafName" />

### 悬停展开

`ExpandTrigger` 决定父节点如何展开子级。`Click`（默认）通过点击或键盘展开；`Hover` 在指针停在父节点上时也会展开。

<Demo name="HoverExpand" />

### 绑定到视图模型

`SelectedItem`、`SelectedValue` 和 `SelectedPath` 始终保持一致，前两个是双向的。值通过对 `ValueSelector` 返回的内容调用 `Equals` 来比较，所以同名的两个节点靠值区分。**值必须在整棵树中唯一**：每个省下面都从 01 开始编号的代码不是唯一的。如果两个节点的值相同，取第一个，Debug 构建会输出一条警告。

数据里还没有的值会被保留。数据加载期间字段显示占位文本，`SelectedValue` 保持写入时的样子；当 `ItemsSource` 或集合发出变化通知时，控件会找到对应节点，并补全 `SelectedItem` 和 `SelectedPath`。把数据换成不含该节点的集合，会去掉选中项和路径，但保留值，所以重新加载不会冲掉视图模型里的值。

<Demo name="Binding" />

根集合发出 `CollectionChanged` 时会被监听；它下面各级集合的变化在菜单下次打开时才会显示。

### 禁用与只读

`DisabledSelector` 标记显示但不能选择或展开的节点。`IsReadOnly` 让字段可以聚焦、可以打开查看，但不能修改或清除。`IsEnabled="False"` 禁用整个字段。

<Demo name="Disabled" />

### 清除

有值时，`IsClearable`（默认为 true）会显示清除按钮；点击它会清除值，并且不会打开菜单。代码中可以调用 `Clear()`，字段获得焦点时按 Delete 或 Backspace 也一样。

## API 参考

<!-- api: Cascader -->

<!-- api: CascaderSelectionChangedEventArgs -->

<!-- api: CascaderExpandTrigger -->

## 无障碍

- 字段对辅助技术表现为可展开、可收起的组合框，它的值是 `SelectedText`。`Label` 是无障碍名称，`Description` 是帮助文本。
- 高亮移动时会播报节点名称、所在层级和该列的节点数量。
- 关闭菜单后，焦点回到字段上。

### 键盘

| 按键 | 条件 | 行为 |
|---|---|---|
| Enter、Space、Down、Up | 菜单关闭 | 打开菜单并定位到已选路径 |
| Delete、Backspace | 菜单关闭且有值 | 清除值 |
| Up、Down | 菜单打开 | 在当前列移动，跳过禁用的节点 |
| Home、End、Page Up、Page Down | 菜单打开 | 在当前列内跳转 |
| Right | 菜单打开 | 展开高亮的父节点，并移到它的第一个子节点 |
| Left | 菜单打开 | 回到上一列，高亮停在它的父节点 |
| Enter、Space | 菜单打开 | 选中叶子；展开父节点（`LeafOnly` 为 false 时选中它） |
| Escape | 菜单打开 | 关闭，不改变值 |
| Tab | 菜单打开 | 关闭，不改变值，并移开焦点 |
| 字母键 | 菜单打开 | 跳到当前列中下一个以该字母开头的节点 |

## 动效

- 菜单的打开和关闭与 `Select` 相同。
- 列出现或消失时，菜单宽度用弹簧过渡到新的宽度，正在进行的弹簧会被改向，而不是等它结束。新的一列淡入，同时从左侧滑入 8 px。菜单打开时，已选路径上的所有列会一次摆好，不会逐列播放动画。
- 高亮在行之间滑动，选择变化时字段中的文本会滚动。
- 减少动效时，宽度不做动画，新列只淡入；关闭动效时一切立即完成。

## 相关组件

- `Select`：单级选项。
- `Combobox`：单级，通过输入来搜索。
- `TreeView`：在页面上显示整棵树。
