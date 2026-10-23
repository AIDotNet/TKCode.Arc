---
title: Project board
summary: 紧凑的迭代看板：箭头按钮把每张卡片推进到下一个或上一个阶段，卡片飞入它的新列；看板下方有迭代线、团队头像，以及 All work / My work 切换。
source-hash: 1eaac2413b0e
---

<Demo name="Preview" />

## 何时使用

`ProjectBoard` 是用于单个迭代或单个项目的小型看板：几个阶段作为列，几十个任务作为卡片。每张卡片上有两个箭头按钮，可以把它的任务推进到左边或右边的一个阶段；卡片会沿直线飞到新列的末尾，原来所在的列随之收拢。表头统计已完成的任务数，一条细的迭代线会随着任务到达最后一个阶段而填充，团队头像位于 All work / My work 切换旁边，点击 "Add task" 按钮会在第一个阶段添加一张新卡片。

- **团队或项目页面。** 看板让人一眼看清"所有事项都在哪里"，并让每个人无需离开页面就能移动自己的工作。
- **仪表盘卡片。** 带框时，它可以与其他卡片并排；扁平时，它会独占整个页面。
- **优先支持触控和键盘。** 移动卡片只需按一下按钮，而不是拖动：无论使用鼠标、手指、Tab 加 Enter，还是屏幕阅读器，操作方式都一样，并且每次移动都会被播报。

## 何时不用

- **大型看板。** 每张卡片都是实时元素，且没有虚拟化；请控制在几十个任务以内。对于数百个任务，需要在列之间拖放、泳道或在制品上限的情况，请使用完整的看板。
- **自由排序。** 移动总是落在相邻列的末尾；卡片不能被拖到某个位置，也不能从卡片本身跳过阶段。需要这些功能时，请用你自己的控件调用 `MoveTask` 并传入索引。
- **编辑。** 卡片只显示任务，不能编辑它。请从 `AddRequested` 或页面其他位置的选中项打开你自己的表单。
- **报表。** 燃尽图或跨迭代的对比请使用图表；迭代线只显示一个比例。

## 用法

### 阶段、任务和人员

`Stages` 是从左到右排列的列，两到六个比较合适。每个 `ProjectBoardStage` 都有一个供任务引用的 `Key` 和一个 `Header`。标记为 `IsDone` 的阶段视为已完成（如果没有标记，则最后一个阶段视为已完成）：它的任务会填充迭代线，标题变为弱化的颜色，到期标签也不再显示警告。

每个 `ProjectBoardTask` 就是一张卡片：顶部是 `Area` 和 `DueText`（`IsDueSoon` 会让到期标签以警告色显示），中间是 `Title`，然后是 `Owner` 的头像、`CommentCount`（为零时隐藏）以及两个箭头。`StageKey` 指明它所在的阶段；如果该键与任何阶段都不匹配，卡片会显示在第一列。每列按集合中的顺序显示其阶段的任务。

人员是 `ProjectPerson` 对象：包含名称和键，并可选择提供图片。没有图片时，看板会在由键选定的柔和颜色上绘制姓名首字母，因此同一个人始终保持同一种颜色。`Team` 是表头中的头像行，`CurrentUser` 表示 "me"，即当前用户：它是 My work 筛选的对象，也是新草稿的负责人。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:ProjectBoard Title="Launch prep">
        <arc:ProjectBoard.Stages>
            <arc:ProjectBoardStage Key="planned" Header="Planned" />
            <arc:ProjectBoardStage Key="progress" Header="In progress" />
            <arc:ProjectBoardStage Key="review" Header="Review" />
            <arc:ProjectBoardStage Key="done" Header="Done" IsDone="True" />
        </arc:ProjectBoard.Stages>
        <arc:ProjectBoardTask Title="Split the settings API" StageKey="progress" Area="Platform" DueText="Wed" CommentCount="6" />
        <arc:ProjectBoardTask Title="Contrast audit for receipts" StageKey="review" Area="Checkout" DueText="Today" IsDueSoon="True" />
    </arc:ProjectBoard>
</UserControl>
```

```csharp
var rhea = new ProjectPerson("Rhea Calloway", "rhea");
board.Team.AddRange([rhea, new ProjectPerson("Tomas Vierra", "tomas")]);
board.CurrentUser = rhea;
board.TasksSource = sprint.Tasks; // an ObservableCollection<ProjectBoardTask>
```

`Tasks` 用于在 XAML 中编写或在代码中添加的任务；设置 `TasksSource` 后，它会使用你自己的集合，并优先于 `Tasks`。会引发变更通知的集合会被监听；可写的集合会随移动重新排序并接收新任务，因此始终与看板显示的内容保持一致。从外部更改任务的 `StageKey` 也会移动它的卡片。

### 移动任务

右箭头把任务移到下一个阶段，左箭头移到上一个阶段；会把任务移出看板的那个箭头会被禁用。无论哪个方向，被移动的任务都会落在新列的末尾。`TaskMoving` 最先触发，可以通过 `Cancel` 让任务保持原位；`TaskMoved` 在卡片开始飞行时触发，包含移动前后的阶段和位置，因此适合在此保存更改。使用箭头移动后会运行 `MoveCommand`，参数为 `ProjectBoardMoveRequest`。

`MoveNext`、`MovePrevious` 和 `MoveTask` 可以通过代码完成同样的操作；`MoveTask` 可以移动到任意阶段，并且可以通过索引移动到该阶段中的某个位置。`FocusTask` 把键盘焦点放到一张卡片上，`ScrollToStage` 把某一列滚动到可见范围内。

<Demo name="Persisted" />

```csharp
board.TaskMoving += (_, e) =>
{
    if (e.ToStage.Key == "review" && e.Task.CommentCount == 0)
    {
        e.Cancel = true; // the app's rule: review needs a comment first
    }
};
board.TaskMoved += (_, e) => SaveStage(e.Task.Key, e.ToStage.Key, e.ToIndex);

void SaveStage(string taskKey, string stageKey, int position)
{
    // Send the change to your store; the board has already moved the card.
}
```

### All work 与 My work

有了 `CurrentUser` 之后，表头会显示切换控件。My work 只保留当前用户负责的卡片（`Filter` 为 `ProjectBoardFilter.Mine`）；其他卡片会在原位淡出，其余卡片向上收拢；切回 All work 时它们会淡入回来。每列统计自己显示的卡片数量，为空时显示 "Nothing assigned to you"；而表头和迭代线始终统计全部任务。`Filter` 是双向的，并会引发 `FilterChanged`。没有 `CurrentUser` 时，切换控件会隐藏。

### 添加任务

点击 "Add task" 会引发 `AddRequested`。把任务放进事件的 `Task` 中即可添加它；或者设置 `Handled` 而不添加任何内容，例如先打开你自己的表单，待填写完成后再调用 `AddTask(task, stageKey)`。没有处理程序时，看板会添加一张草稿：标题取自 `DraftTitles` 的下一个条目，区域为 `DraftArea`，到期标签为 `NoDateText`，负责人为当前用户。新任务会添加到 `AddStageKey` 所指阶段的末尾（默认为第一个阶段），以淡入方式出现，引发 `TaskAdded`，执行 `AddCommand`，并被播报；焦点会留在添加按钮上，便于继续添加下一个。`IsAddVisible` 可以隐藏该按钮。

### 你自己的阶段

阶段就是数据，因此看板可以遵循任何工作流程。阶段的 `EmptyText` 会替换该列的看板空白文字。

<Demo name="CustomStages" />

### 带框或扁平

`IsFramed` 会把看板绘制为一张带 1px 边框、24px 圆角的卡片，适用于与其他内容并排的看板。默认是扁平样式：看板以表面颜色填满其空间，就像一个独立的页面。`Title` 和进度文字都是可选的（`IsProgressVisible` 会同时隐藏进度文字和迭代线）。

<Demo name="Framed" />

### 窄容器

看板读取的是它自身的宽度，而不是窗口的宽度。各列平分宽度，每列最窄为 200（`MinColumnWidth`）；低于此宽度时，它们会横向滚动。宽度为 700 及以下（`NarrowWidth`）时，每列的宽度为整体宽度减去 76，使下一列露出一部分；团队行隐藏，筛选、添加和箭头按钮放大为 40px 的触控目标，而卡片高度不会增加。宽度为 400 及以下（`CompactWidth`）时，操作按钮独占一行，"添加"按钮只显示图标，其名称作为工具提示保留（即 "Add task"）。滚动停止后，如果偏移量在某一列起点三分之一列宽之内，会自动对齐到该列；被移动卡片所在的列会滚动到可见范围内，以便跟随该卡片。

<Demo name="Narrow" />

### 文本

每段文字都有英文默认值，并有对应的属性：进度文字（`ProgressFormat`，默认为 "{0} of {1} shipped this sprint"）、切换控件（`AllWorkText`、`MyWorkText`、`FilterLabel`）、`TeamLabel`、`AddText`、空白文字（`EmptyText`、`EmptyMineText`）、草稿（`DraftTitles`、`NewTaskTitle`、`DraftArea`、`NoDateText`），以及屏幕阅读器朗读的内容（`ColumnLabelFormat`、`ColumnLabelSingularFormat`、`MoveToFormat`、`MovePreviousText`、`MoveNextText`、`MovedFormat`、`AddedFormat`、`DueLabelFormat`、`OwnerLabelFormat`、`CommentsFormat`、`CommentsSingularFormat`）。在 XAML 中，以花括号开头的格式前需要加上 `{}`。

```xml
<arc:ProjectBoard Title="Lancement"
                  ProgressFormat="{}{0} sur {1} livrées ce sprint"
                  AllWorkText="Tout"
                  MyWorkText="Mes tâches"
                  AddText="Ajouter"
                  MoveToFormat="Déplacer {0} vers {1}"
                  MovedFormat="{}{0} est maintenant dans {1}." />
```

## API 参考

<!-- api: ProjectBoard -->

<!-- api: ProjectBoardStage -->

<!-- api: ProjectBoardTask -->

<!-- api: ProjectPerson -->

<!-- api: ProjectBoardMoveRequest -->

### 事件

| 事件 | 引发时机 | 参数 |
|---|---|---|
| `TaskMoving` | 任务即将更改阶段 | `Task`, `FromStage`, `ToStage`, `Cancel` |
| `TaskMoved` | 任务已更改阶段，其卡片开始飞行时 | `Task`, `FromStage`, `ToStage`, `FromIndex`, `ToIndex` |
| `AddRequested` | 按下 "Add task" 时 | `Stage`, `Task`（设置它即可添加你自己的任务）, `Handled` |
| `TaskAdded` | 看板已添加任务 | `Task`, `Stage` |
| `FilterChanged` | `Filter` 已更改 | `OldFilter`, `NewFilter` |

## 无障碍

- 键盘：`Tab` 和 `Shift+Tab` 依次经过 All work、My work、Add task，然后是每个可用的箭头，按列从左到右、每列从上到下；`Enter` 或 `Space` 按下焦点所在的按钮（选择筛选、添加任务或移动卡片）。
- 看板是一个名为 "Project board" 的群组。每一列是一个群组，以其阶段命名，并标明它所显示的卡片数量（"Review, 1 task"）；列中的卡片是列表项，其名称包含任务名、区域、到期标签、负责人和评论数（"Split the settings API, Platform, due Wed, assigned to Rhea Calloway, 6 comments"）。
- 箭头以它们将任务发往的位置命名（"Move Split the settings API to Review"）；被禁用的箭头会说明 "the previous stage" 或 "the next stage"。切换控件是一个名为 "Show" 的群组，内含两个切换按钮，会报告哪一个处于按下状态。团队头像名为 "Team"，即使只显示图标，添加按钮也名为 "Add task"。
- 移动和添加会以礼貌的方式播报（"Moved Split the settings API to Review."、"Added Draft the pricing FAQ to Planned."）；筛选则不播报，因为列名本身已经会变化。
- 移动之后，焦点停留在新列中同一张卡片的同一个箭头上；如果该箭头在边缘处被禁用，则焦点移到另一个箭头，因此再次按 Enter 即可继续移动；在窄看板上，列会滚动到可见范围内。
- 悬停样式只跟随鼠标或触控笔；手指操作时，按钮永远不会显示为悬停状态。状态从不只依赖颜色：到期标签保留文字，已完成的卡片留在已完成列中。

## 动效

- **移动**：卡片从当前绘制位置沿直线飞到新列的末尾，使用 Gentle 弹簧（刚度 340，阻尼 34，约 600 ms），并位于其他所有卡片之上。旧列中位于它下方的卡片会以同一弹簧向上滑动，两个位置都会跟随它们移动：正在收拢的位置永远不会让卡片悬在它下方。飞行途中再次按下会从卡片当前绘制的位置重新开始。
- **筛选**：高亮以临界阻尼弹簧（刚度 400，阻尼 40）滑动到所选按钮。离开的卡片在原位淡出并缩小到 98%，其余卡片向上收拢；返回的卡片淡入并从 98% 放大，全部使用 Gentle 弹簧。
- **添加**：新卡片淡入并从 98% 放大；迭代线以弹簧过渡到新的比例（任务到达或离开已完成阶段时也是如此）。
- **悬停和按下**：颜色在 160 ms 内变化；箭头按下时缩至 92%，添加按钮缩至 97%，鼠标下的添加按钮会变暗至 88%。
- 减少动效和 `AnimatesMoves="False"` 会让移动、添加和筛选立即就位，但保留颜色变化。关闭动效时，所有变化都会立即应用。看板静止时，不会运行任何动画。

## 相关

- 发布就绪、看板、周日历和可用时间选择器，以及 Projects 区块中的其他区块，与本区块共用人员、头像和进度线。
- `SegmentedControl`，用于区块之外的视图切换。
- `AvatarGroup`，用于页面其他位置的一组头像堆叠。
- `LayoutMorph`，用于在多种布局中展示同一组卡片。
