---
title: 选择工具栏
summary: 选中列表或表格中的项目时从底部升起的浮动栏：显示数量、全选、批量操作和清除选择。
source-hash: ad3c171efa01
---

<Demo name="Preview" />

## 何时使用

`SelectionToolbar` 包住一个列表或表格，并为其中选中的内容加上一条浮动栏。把列表放进 `Content`，并让 `SelectedCount` 与它的选择保持一致。只要有内容被选中，就会有一条胶囊形的栏从底部边缘升起，显示数量、用于选中其余项目的“Select all”、批量操作和清除按钮；选择清空时它会落下消失。具有破坏性的操作使用危险色并放在最后。

- **文件列表和表格**：人们勾选多行后一次归档、移动或删除。
- **收件箱和工单队列**：许多项目要设置相同的标签、负责人或状态。
- **管理后台的表格**：选择之后进行导出或批量修改。

## 何时不用

- **对单个项目的单个操作**：把操作放在行上，或放进 `ContextMenu`。
- **与选择无关的操作**：使用页面标题中的工具栏（`PageHeader`）。
- **很长的操作列表**：栏上保持四五个操作，其余放进页面上的菜单。

## 用法

```csharp
toolbar.TotalCount = files.Count;
list.SelectionChanged += (_, _) => toolbar.SelectedCount = list.SelectedItems.Count;
toolbar.SelectAllRequested += (_, _) => list.SelectAll();
toolbar.Cleared += (_, _) => list.UnselectAll();
toolbar.ActionInvoked += (_, e) => Run(e.Action.Key, e.SelectedCount);
```

清除按钮和 Esc 会把 `SelectedCount` 设为 0 并引发 `Cleared`，在那里取消行的选择。每个操作也可以带一个 `Command`，它会收到选中的数量。没有 `Actions` 时，栏上提供 Archive、Move、Share 和 Delete。

<Demo name="CustomActions" />

## API 参考

<!-- api: SelectionToolbar -->

## 无障碍

- 这条栏是名为“Bulk actions”的工具栏。Tab 可以到达它；左右方向键在按钮之间移动，Home 和 End 跳到两端。
- 在控件内任意位置按 Esc 都会清除选择；如果焦点原本在栏上，会回到列表。
- 数量每次变化都会被礼貌地读出。清除按钮名为“Clear selection”。

## 动效

- 栏在淡入时升起 16 px，淡出时落下。数量增加时数字向上滚动，减少时向下滚动。
- 减少动效时只保留淡入淡出；关闭动效时栏立即出现和消失。

## 响应式

- 栏在底部居中，两侧各留 16 px，下方留 18 px。在最后一行下方留出空间（列表底部的内边距），免得栏把它挡住。
- 宽度小于 `CompactWidth`（560 px）时，操作只保留图标，并以名称作为提示。

## 相关组件

- `FloatingButtonGroup`：与选择无关的浮动工具组。
- `Checkbox` 用于行的选择，`DataGrid` 用于大型表格。
- `InboxSidebar`：它的邮件列表常与这条栏一起使用。
