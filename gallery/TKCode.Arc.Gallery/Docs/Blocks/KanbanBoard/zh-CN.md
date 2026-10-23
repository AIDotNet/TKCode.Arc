---
title: Kanban board
summary: 一块团队看板，卡片被拿起后会随指针倾斜，并弹入为它打开的槽位；支持按人分泳道、实时在制品上限、快速添加和详情抽屉。
source-hash: 103005c79514
---

<Demo name="Preview" />

## 何时使用

`KanbanBoard` 是一块完整的看板，适用于按固定的一组阶段推进、且同一阶段内顺序很重要的工作。卡片可以用鼠标、触控笔、手指（短按后）或键盘拿起；卡片被拿起时，它将落入的位置会打开一个槽位，周围的卡片会让出空间，各列的计数也会显示卡片最终会落在哪里。列可以设置上限，超出上限时该列会变为琥珀色。

- **团队的问题或任务看板。** 从你的跟踪工具加载列、人员和卡片，并保存 `CardsChanged` 在每次移动或编辑后交出的有序列表。
- **需要手动排序的流水线。** 销售流水线、招聘漏斗或内容日历，其中阶段顶部的卡片就是下一个要处理的。
- **需要按人查看的看板。** `GroupBy="Assignee"` 为每个人提供一条使用相同列的泳道；把卡片放到另一人的泳道中即可将其转交。

## 何时不用

- **按日期规划的工作。** 当问题是*何时*而不是*处于哪个阶段*时，请使用周日程或时间线。
- **只读的状态汇总。** 带状态列的表格或列表更便于浏览和排序；只有当共享视图还需要人编辑时，才设置 `IsReadOnly`。
- **单列中有数百张卡片。** 每张卡片都是一个实时元素，以便它能够滑动；本看板在卡片总数为几百张以内时运行流畅。大量待办请分页或筛选，或使用 `DataGrid`。
- **自由排布。** 卡片总是贴合到有序的列中；便利贴式的布局请使用画布。

## 用法

### 列、人员、标签和卡片

看板接受四个列表。`Columns` 是按顺序排列的阶段；为某列设置 `Limit`，即可在卡片数量超出时发出警告。`People` 是 `ProjectPerson` 对象：它们负责绘制头像、泳道和负责人菜单（没有图片时会生成姓名首字母）。`Labels` 为卡片提供彩色圆点和名称。`Cards` 是一个扁平列表：列表的顺序就是每一列内部的顺序，保存时也按此顺序。

卡片通过 `ColumnKey`、`AssigneeKey` 和 `LabelKey` 引用其列、负责人和标签，因此可以直接来自跟踪工具的 JSON。所在列不在看板上的卡片不会显示；在泳道中，负责人不在 `People` 里的卡片会被归入末尾的 "Unassigned" 泳道。

```xml
<arc:KanbanBoard Title="Sprint 18" Height="640">
    <arc:KanbanBoard.Columns>
        <arc:KanbanColumn Key="todo" Name="To do" />
        <arc:KanbanColumn Key="doing" Name="Doing" Limit="3" />
        <arc:KanbanColumn Key="done" Name="Done" />
    </arc:KanbanBoard.Columns>
    <arc:KanbanBoard.People>
        <arc:ProjectPerson Key="ines" Name="Ines Duarte" />
        <arc:ProjectPerson Key="tom" Name="Tomasz Bielski" />
    </arc:KanbanBoard.People>
    <arc:KanbanCard Key="SP-41" Title="Export the audit log as CSV" ColumnKey="todo" AssigneeKey="ines" Points="3" />
    <arc:KanbanCard Key="SP-42" Title="Rate limit the sign-in form" ColumnKey="doing" AssigneeKey="tom" Points="2" />
</arc:KanbanBoard>
```

将 `CardsSource` 绑定到视图模型持有的集合。可写的列表（`ObservableCollection` 或 `AvaloniaList`）会在每次拖放和每张新卡片时原地重新排序，因此始终与看板一致；只读集合则保持不变，看板使用它自己的顺序。来自外部的变化，例如新列表或 `ColumnKey` 已改变的卡片，会被一并应用，卡片会滑动到新的位置。

### 保存更改

`CardsChanged` 会在每次移动、编辑、检查项变化和新建卡片之后触发。其中的 `Reason` 说明发生了什么，`Cards` 是按看板顺序排列的整个列表快照：原样保存即可。`CardMoved` 提供移动的详细信息：移动前后的列和负责人、卡片在列表中移动前后的位置，以及 `ChangeSource`（指针、触摸、键盘、抽屉或方法调用）。

```csharp
var saved = new List<(string Key, string Column, string? Owner)>();
board.CardsChanged += (_, e) =>
{
    // Store the snapshot as it is: its order is the order inside every column.
    saved = e.Cards.Select(c => (c.Key, c.ColumnKey, c.AssigneeKey)).ToList();
};
board.CardMoved += (_, e) => Debug.WriteLine($"{e.Card.Key}: {e.FromColumn?.Name} → {e.ToColumn?.Name}");
```

<Demo name="Controlled" />

### 泳道

`GroupBy` 在单行列和每人一条泳道之间切换；工具栏中的切换开关也能做到同样的事，且每张卡片都会飞到新的位置。看板水平滚动时，泳道标题停留在左侧边缘；垂直向下滚动时，列标题停留在顶部。列的计数和上限始终统计所有泳道；每个泳道标题统计自己的卡片。把卡片放到另一人的泳道中，会改变它的 `AssigneeKey`。

<Demo name="Swimlanes" />

### 在制品上限

当某列的卡片数量超过 `Limit` 时，该列才算超限（刚好装满是允许的）。它的计数变为琥珀色，出现 "Above limit" 标签，其单元格带上暖色调；即将放入该列的卡片所对应的槽位也会注明这一点。拖动卡片时计数会实时跟随，因此警告在放下之前就会出现。看板只发出警告：如果必须保证上限，请处理 `CardMoving` 并设置 `Cancel`，将卡片退回。`ColumnLimitStateChanged` 会报告某列超限或恢复正常。

```csharp
board.CardMoving += (_, e) => e.Cancel = e.IsTargetOverLimit && e.From.ColumnKey != e.To.ColumnKey;
```

<Demo name="WipLimits" />

### 快速添加和详情抽屉

每列列头中的 "+"，以及每列下方的 "Add card" 行，会在该列末尾打开一个编辑框（在泳道中，则在 `CurrentUser` 所在的泳道打开，默认是第一个人）。按 Enter 添加卡片并保持编辑框打开，以便添加下一张；Shift+Enter 换行，Escape 关闭编辑框。新卡片的编号是看板中现有最大编号之后的下一个（"FLD-244" → "FLD-245"）；设置 `CardKeyFactory` 可以使用你自己的编号，处理 `CardCreating` 则可以在卡片添加之前修改或拒绝它。

点击卡片，或在聚焦的卡片上按 Enter，会触发 `CardInvoked` 并打开详情抽屉：其中包含标题、状态、负责人、标签、截止日期、估时、备注和检查清单。编辑会在输入时立即应用到卡片上。更改状态时，卡片在列表中的位置保持不变，因此它会落在较后一列的顶部，或较前一列的底部。设置 `OpenedCard`（或调用 `OpenCard`）可以用代码打开抽屉；在 `CardInvoked` 中设置 `Handled`，或关闭 `IsDetailsEnabled`，则可以改为显示你自己的视图。

### 窄容器和只读看板

看板读取的是它自身的宽度。各列平分宽度，直到 `MinColumnWidth`（251），之后看板横向滚动。在 `CompactWidth`（560）及以下时，各列宽度为 `CompactColumnWidth`（235），内边距收紧，看板在横向滚动后会吸附到最近的一列，且第一列紧贴左侧边缘。抽屉的宽度永远不超过看板宽度减 16，因此它能覆盖在窄看板上；其状态选项每行两个并换行显示。

<Demo name="Narrow" />

`IsReadOnly` 会让卡片保持原位：不能拖动、不能快速添加、不能编辑，但抽屉仍然可以打开以显示卡片。`IsDragEnabled` 和 `IsQuickAddEnabled` 则可分别单独关闭这两项功能。

<Demo name="ReadOnly" />

### 文本

看板显示或朗读的每个词都放在 `Strings` 中，这是一个带英文默认值的 `KanbanBoardLabels` 对象。带有数值的文本是函数，因此翻译时可以调整词序。

```csharp
board.Strings = new KanbanBoardLabels
{
    StatusView = "Statut",
    LanesView = "Par personne",
    AddCard = "Ajouter une carte",
    CardsWord = n => n == 1 ? "carte" : "cartes",
    Dropped = (title, column) => $"{title} déposée dans {column}."
};
```

## API 参考

<!-- api: KanbanBoard -->

<!-- api: KanbanColumn -->

<!-- api: KanbanCard -->

<!-- api: KanbanLabel -->

<!-- api: KanbanChecklistItem -->

<!-- api: KanbanBoardLabels -->

<!-- api: KanbanCardsChangedEventArgs -->

<!-- api: KanbanCardMovedEventArgs -->

## 无障碍

- **键盘。** Tab 依次到达视图切换器、每个 "+"，然后是每列的卡片（从上到下），最后是该列的 "Add card" 行。在卡片上按 Enter 打开其详情。按空格键拿起卡片：上下方向键在列内移动它，左右方向键移动到相邻列的同一位置，Shift 加上上下方向键则移动到相邻泳道；按空格或 Enter 放下，按 Escape 将其放回原处。将焦点移离被拿起的卡片，也会将其放回原处。
- 每次拿起、移动和放下都会立即播报（"Signature pad on the handover form lifted. Building, place 1."），卡片换泳道时还会播报负责人。新卡片、抽屉中的状态更改以及列超出上限，都会以礼貌的方式播报。
- 卡片是按钮，名称为 "title, id, owner"，并由其标签、截止日期、检查项进度和估时加以说明。
- 上限不仅以颜色表示，也以文字表示：计数读作 "4 of 3 allowed"，标签和槽位会说明已超出上限。
- 抽屉在看板内是模态的：焦点移到其关闭按钮，Tab 键停留在抽屉内；按 Escape、点击关闭按钮，或点击变暗的看板，都会关闭抽屉，并将焦点返回卡片。在负责人菜单中按 Escape 只关闭菜单。文本框会显示焦点环。关闭编辑框会把焦点返回打开它的按钮。
- 视图切换器和抽屉中的状态是单选组，只有一个 Tab 停靠点；方向键改变选择。

## 动效

- **拿起**：指针移动超过 `DragThreshold`（5px）后，或触摸停留了 `TouchHoldDelay`（250ms）后，卡片被拿起；较快的轻扫则滚动看板。卡片从被抓取的位置开始跟随指针，以快速弹簧放大到 1.035 倍，并随水平速度倾斜（每像素每秒 0.0064°，最多 7°）；指针停止时，倾斜会回到水平。卡片绘制在整个看板之上，因此它可以越过列头和标题移动而不被裁切。
- **让出空间**：槽位出现在卡片原来的位置并淡入；周围的卡片以及槽位本身沿布局弹簧（临界阻尼，约 0.6 秒）滑动，目标变化时保持其速度。
- **放下**：卡片以小弹簧（刚度 690，阻尼 51）落入槽位，起始速度为抛出时的速度；如果槽位仍在滑动，卡片会跟随它。其阴影和边框分别在 240ms 和 160ms 内缓和回落。Escape 会以同样的方式让卡片飞回原处。
- **边缘**：靠近看板边缘（64px）时，两个方向都会滚动，指针越靠近边缘滚动越快，最快可达 1200px/s；指针静止时，槽位会跟随在它下方移动的内容。
- **切换视图、键盘移动和抽屉更改**会使每张受影响的卡片沿布局弹簧移动。抽屉在同一弹簧上滑入，同时看板在 240ms 内变暗；编辑框随之展开和收起。计数会滚动变化。
- 减少动效会保留槽位、计数和短促的淡入，但去掉倾斜和所有飞行动画：卡片立即落位。关闭动效则会立即应用所有变化。

## 相关组件

- `ReorderableList`，用于对单个列表重新排序。
- `ProjectPerson` 以及其他 Projects 区块，用于看板上的人员。
- `SegmentedControl`、`DropdownMenu` 和 `RollingDigits`，是看板工具栏、负责人菜单和计数所使用的组件。
