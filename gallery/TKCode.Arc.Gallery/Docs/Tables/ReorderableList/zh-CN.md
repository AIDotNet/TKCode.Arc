---
title: 可排序列表
summary: 通过直接拖放整理项目。
source-hash: e7d0a1b0e1f1
---

## 何时使用

- 用户决定顺序的短列表，例如优先级或播放列表。
- 同一项工作需要同时支持拖动与键盘操作的列表。
- 每行保留自己的文字。只有抓手可以开始拖动，因此文字仍可被选中。

## 何时不用

- 顺序来自用户可排序的列：用 `SortableDataTable`。
- 行需要滑动操作而非重新排序：用 `SwipeActions`。
- 长列表。行不做虚拟化，因此请保持列表简短或将其拆分。

## 用法

每一行都会显示一个抓手。拖动抓手即可移动该行。列表会在 `Reordered` 中报告新的顺序。

```xml
<arc:ReorderableList Label="优先级"
                     Width="520"
                     ItemsSource="{Binding Priorities}" />
```

`ItemsSource` 接受 `ReorderableListEntry` 列表，每项包含 `Id`、`Title`，以及可选的 `Detail`。

```csharp
var priorities = new ObservableCollection<ReorderableListEntry>
{
    new("pricing", "发布定价页", "市场部 - 周五截止"),
    new("onboarding", "审阅新手引导流程", "设计 - 3 条未解决的评论"),
    new("billing", "迁移账单 Webhook", "平台")
};
var list = new ReorderableList { ItemsSource = priorities };
```

### 响应移动

`Reordered` 提供被移动的项、其原索引与新索引。当 `ItemsSource` 为 `ObservableCollection` 且 `MovesItems` 为 `True`（默认值）时，列表会直接在集合中移动该项。将 `MovesItems` 设为 `False`，可保持集合不变，并由你自行保存顺序。

```csharp
list.Reordered += (_, e) => status.Text =
    $"已将 {((ReorderableListEntry)e.Item!).Title} 从第 {e.OldIndex + 1} 位移到第 {e.NewIndex + 1} 位";

list.MoveItem(0, 2);   // 将第一行移到第三位
```

### 自定义项模板

`ItemTemplate` 会替换默认的标题与详情。`ItemTitleSelector` 提供用于无障碍名称与播报的标题。

```csharp
var colors = new ReorderableList
{
    ItemsSource = new ObservableCollection<string> { "珊瑚色", "藏红色", "薄荷色" },
    ItemTemplate = new FuncDataTemplate<string>((name, _) => new TextBlock { Text = name }),
    ItemTitleSelector = item => item.ToString() ?? string.Empty
};
```

### 长度与滚动

当列表高于你提供的空间时，请将其放入 `ScrollViewer`。将某一行拖到滚动区域的边缘即可滚动。

```xml
<ScrollViewer Height="340">
    <arc:ReorderableList ItemsSource="{Binding Tracks}" />
</ScrollViewer>
```

## API 参考

<!-- api: ReorderableList -->

<!-- api: ReorderableListEntry -->

## 无障碍

- 每个抓手都是一个按钮。其名称包含项目标题与位置，并在每次移动后更新。
- 让抓手获得焦点后按 Up 或 Down，可将其所在行移动一个位置。焦点保留在被移动行的抓手上。
- Space 拾起一行，方向键移动它，再按 Space 放下，Escape 则取消移动。
- 用于拖动的只有抓手可以获得焦点。行中的文字是普通的可选中文本区域。
- 设置 `Label` 可为列表指定辅助技术使用的名称。默认名称为「重新排序项目」。

## 动效

- 按住抓手时，该行会轻微抬起。松开后以短促的弹簧回落。
- 拖动时，该行跟随指针移动，途经的行则以弹簧滑开让出位置。
- 位置编号朝该行移动的方向滚动。
- 减弱动效时，仍保留指针跟随，但该行立即落位，不再抬起。关闭动效的表现与减弱动效相同。

## 相关组件

- `SortableDataTable`：顺序来自列值而非人工选择时使用。
- `SwipeActions`：为行提供快捷操作，而不是重新排序。
