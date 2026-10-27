---
title: 可排序列表
summary: 通过直接拖放整理项目。
source-hash: 5b51862a9d33
---

<Demo name="Priorities" />

## 何时使用

`ReorderableList` 让人把一个简短的项目列表调整成他们想要的顺序。它用于由人手动决定、且顺序本身就是信息的场景：团队的上线优先级、队列中接下来要处理的任务、审核清单的步骤。它放在卡片、侧边面板或设置页中，并通过 `Reordered` 事件保存新的顺序。

- **规划视图中的上线优先级**：产品负责人在发布评审前为四到五条工作流排序。顺序就是答案，因此每项旁边的拖动抓手比数字输入框更清楚。
- **媒体或运营控制台中的播放列表和任务队列**：接下来要播放或处理的项目。人们把一项上移或下移，其余项目保持原位。
- **工作流编辑器中的审批或审核步骤**：一份清单，其顺序决定哪位审核人先看到文档，由流程的负责人编辑。
- **仪表板或导航面板中的固定快捷方式**：人们最常用的项目，按自己的喜好排序，并按账号保存。

## 何时不用

- **顺序来自列的值**，例如按到期日或金额给发票排序：用 `Table`，它按列排序，并把数据保留在表格中。
- **对某一行做快速操作**，例如归档、稍后提醒或删除，且不需要重新排序：用 `SwipeActions`，它通过滑动显示操作，而不会移动该行。
- **包含数百项的长列表**：行没有虚拟化，而且每当其他行移动时，每一行都会运行布局动画。应拆分列表，或为 `Table` 配上排序控件。
- **嵌套项目**，例如文件夹、分类或子任务：用 `TreeView`，它显示层级，并让人展开每一层。
- **从固定集合中选择**，人们只是切换而不排名，例如筛选标签或频道：用 `ChipGroup`。这些选项的顺序不由使用者决定。

## 用法

每一行都显示一个抓手。拖动抓手即可移动该行。列表通过 `Reordered` 报告新的顺序。

```xml
<arc:ReorderableList Label="优先级"
                     Width="520"
                     ItemsSource="{Binding Priorities}" />
```

`ReorderableList` 派生自 Avalonia 的 `ItemsControl`，因此 `ItemsSource`、`Items`、`ItemTemplate` 以及容器方法的用法与其他项目控件相同。`ItemsSource` 接受一个 `ReorderableListEntry` 列表，每项包含 `Id`、`Title` 和可选的 `Detail`。

```csharp
var priorities = new ObservableCollection<ReorderableListEntry>
{
    new("pricing", "发布定价页", "市场部 - 周五截止"),
    new("onboarding", "梳理引导流程", "设计 - 3 条未解决评论"),
    new("billing", "迁移账单 Webhook", "平台")
};
var list = new ReorderableList { ItemsSource = priorities };
```

### 结构

1. **抓手**（手柄）：一个 40 px 的方块，中间是 16 px 的垂直抓手图标，距离行的上、下、左边缘各 7 px，位于弱化的表面色上。它是唯一能开始拖动的部分。
2. **行**：带 Arc 边框和控件圆角的表面，高度至少 56 px。行之间相距 8 px。
3. **内容**：项目标题使用 14 px 中等字重，下方是 12 px、弱化文字颜色的详情行。默认内容来自 `ReorderableListEntry`；`ItemTemplate` 可以替换它。
4. **位置**（右侧）：两位数字，例如 `01`、`02`，12 px，弱化文字颜色。每次移动后更新，并沿该行移动的方向滚入。
5. **播报器**（不可见）：一个隐藏的礼貌区域，朗读每一步键盘和拖动操作。

### 响应移动

`Reordered` 提供该项目、它的旧索引和新索引。当 `ItemsSource` 是 `ObservableCollection` 且 `MovesItems` 为 `True`（默认值）时，列表会直接在该集合中移动项目。把 `MovesItems` 设为 `False`，集合保持不变，由你自己保存顺序。绑定到数组或其他固定数据源的列表行为相同：列表触发 `Reordered`，并保持数据源不变。

```csharp
list.Reordered += (_, e) => status.Text =
    $"已将 {((ReorderableListEntry)e.Item!).Title} 从第 {e.OldIndex + 1} 位移到第 {e.NewIndex + 1} 位";

list.MoveItem(0, 2);   // 把第一行移到第三位
```

`MoveItem` 以与拖动相同的滑动动画移动行，并且同样会触发 `Reordered`，因此代码移动和人的移动会进入同一个处理程序。

### 自定义项模板

`ItemTemplate` 替换默认的标题和详情。`ItemTitleSelector` 提供抓手的无障碍名称和播报中使用的标题；未设置时，列表使用条目的 `Title` 或项目的文本。

<Demo name="CustomItemTemplate" />

```csharp
var colors = new ReorderableList
{
    ItemsSource = new ObservableCollection<string> { "珊瑚色", "藏红花色", "薄荷色" },
    ItemTemplate = new FuncDataTemplate<string>((name, _) => new TextBlock { Text = name }),
    ItemTitleSelector = item => item.ToString() ?? string.Empty
};
```

### 长度与滚动

当列表高于你为它分配的空间时，把它放进 `ScrollViewer`。把一行拖到滚动区域的边缘会使其滚动，因此人可以把一行移到列表可见部分之外。

<Demo name="AutoScroll" />

```xml
<ScrollViewer Height="340">
    <arc:ReorderableList ItemsSource="{Binding Tracks}" />
</ScrollViewer>
```

### 绑定到视图模型

在 MVVM 应用中，视图模型拥有集合以及保存顺序的代码。使用 CommunityToolkit.Mvvm 时，由 `ObservableObject` 持有集合；当 `MovesItems` 为 `True` 时，列表会移动该集合中的项目。绑定是单向的：列表修改集合，视图模型读取它。

```csharp
public partial class LaunchPlanViewModel : ObservableObject
{
    public ObservableCollection<ReorderableListEntry> Priorities { get; } =
    [
        new("pricing", "发布定价页", "市场部，周五截止"),
        new("onboarding", "梳理引导流程", "设计，3 条未解决评论"),
        new("billing", "迁移账单 Webhook", "平台")
    ];

    [ObservableProperty]
    private string _status = "拖动抓手即可调整上线优先级的顺序。";

    [RelayCommand]
    private void SaveOrder()
    {
        // 在这里保存顺序，例如通过你的 API 客户端。
        Status = $"已保存顺序：{string.Join(", ", Priorities.Select(p => p.Id))}";
    }
}
```

```xml
<StackPanel Spacing="12">
    <arc:ReorderableList Label="上线优先级"
                         Width="560"
                         HorizontalAlignment="Left"
                         ItemsSource="{Binding Priorities}" />
    <TextBlock Text="{Binding Status}" TextWrapping="Wrap" />
</StackPanel>
```

在代码后置文件中把 `Reordered` 转发给命令，使每次移动都保存顺序：

```csharp
list.Reordered += (_, _) => vm.SaveOrderCommand.Execute(null);
```

`ItemsSource` 是单向的。不要从列表反向绑定它，因为列表不会写入该属性；新的顺序由集合本身承载。

### 数据结构与性能

列表适用于短集合，开销主要取决于行数。每当其他行移动时，每一行都会被布局并做动画，而且行没有虚拟化，因此几十行的列表移动得很流畅，而几百行的列表则不然。

- **常见情况使用 `ReorderableListEntry`。** 它带有 `Id`，供你自己的代码在保存顺序时找到该项目；`Title` 为可访问的行命名；`Detail` 是第二行文字。
- **其他数据使用 `ItemTemplate`。** 任意类型的项目都可以通过 `ItemTemplate` 显示；同时设置 `ItemTitleSelector`，使抓手名称和播报读出正确的标题。
- **把数据保存在一个集合中。** 列表原地移动项目时，集合必须是列表所绑定的那一个，而不是副本。

### 示例：上线优先级

产品负责人在评审之前为上线工作流排序。这个列表决定了整个页面的内容，因此它上方有标题，旁边有一行状态文字确认已保存的顺序。每行都有一条包含负责人或截止日期的详情，使位置读起来像计划，而不只是一个数字。

![上线优先级列表，四行有序条目，每行都有抓手和位置数字](images/LaunchPriorities.png)

```xml
<StackPanel Width="600" Spacing="14">
    <TextBlock Text="Launch priorities"
               FontSize="16"
               FontWeight="SemiBold"
               Foreground="{arc:ArcDesignTokenResource Foreground}" />
    <TextBlock Text="Ordered by the launch lead. Drag a grip, or focus one and press Up or Down."
               FontSize="13"
               TextWrapping="Wrap"
               Foreground="{arc:ArcDesignTokenResource TextMuted}" />
    <arc:ReorderableList Label="Launch priorities"
                         Width="560"
                         HorizontalAlignment="Left">
        <arc:ReorderableListEntry Id="pricing"
                                  Title="Ship pricing page"
                                  Detail="Marketing, due Friday" />
        <arc:ReorderableListEntry Id="onboarding"
                                  Title="Review onboarding flow"
                                  Detail="Design, 3 open comments" />
        <arc:ReorderableListEntry Id="billing"
                                  Title="Migrate billing webhooks"
                                  Detail="Platform" />
        <arc:ReorderableListEntry Id="notes"
                                  Title="Write launch notes"
                                  Detail="Docs, draft ready" />
    </arc:ReorderableList>
</StackPanel>
```

设计决策体现在文案和规模上。四行足以展示抓手、标题、详情和位置，而无需滚动，这正是该控件所适用的列表长度。详情行写明负责人和状态，因此人们不必打开每一项就能判断顺序是否合理。

### 文案规范

控件会根据标题生成抓手名称和播报，因此标题承载措辞。

| 不要写 | 改为 |
|---|---|
| 项目 1、项目 2 | 工作项的名称，例如“发布定价页” |
| 拖动手柄 | 抓手；其无障碍名称由标题生成 |
| 重新排序列表 | `Label="Launch priorities"`，即被排序内容的名称 |
| 优先级 1：发布定价 | 发布定价页，位置由列表显示 |
| 已移动的项目 | 标题，播报中已经包含它 |

- 标题写成工作项的名称，使用句首大写，前面不加编号。列表会添加位置。
- 标题要足够短，以便在列表宽度内单行显示；负责人、日期或状态放入 `Detail`。
- 详情行中的日期和计数使用阿拉伯数字，例如“周五截止”或“3 条未解决评论”。

### 最佳实践

**推荐**

- 把 `Label` 设为被排序对象的名称。标签就是列表的无障碍名称，屏幕阅读器用户会在第一行之前听到它。
- 在 `Reordered` 中保存顺序，而不是等到窗口关闭时才保存，这样关闭窗口不会丢失修改。
- 当列表可能增长时，把它放在设置了高度的 `ScrollViewer` 中，使拖动能够越过可见的行。
- 为每个项目提供稳定的 `Id`，使保存的顺序在重新加载和重命名之后依然有效。
- 在状态行中显示结果，让人看到修改已被保存。

**避免**

- 把列表用于列已经定义的顺序。想按到期日查看项目的用户需要可排序的表格，而手动顺序会与排序冲突。
- 对包含数百行的列表重新排序。每当其他行移动时，每一行都会做动画，列表在失去实用性之前就已失去流畅度。
- 让整行都可拖动。只有抓手可以开始拖动，因此文字可以被选取，点击文字也不会触发移动。
- 在按钮处理程序中再保存一份顺序。列表已经在原地移动项目，因此应在 `Reordered` 触发时直接保存集合，而不要手动重建顺序。

### 常见问题

**列表移动了行，但我的源列表没有变化。** `MovesItems` 为 `False`，或者数据源不是可变集合。请使用 `ObservableCollection` 并保持 `MovesItems` 为默认值，或在 `Reordered` 中自行保存顺序。

**能从行的文字开始拖动吗？** 不能。只有抓手可以开始拖动，因此行中的文字可以被选取和复制。使用键盘的用户可以聚焦抓手，然后用方向键移动。

**为什么第一行按“上”没有反应？** 键盘移动在列表两端停止，焦点保留在抓手上。行不会绕到另一端；在选中一行时使用 Home 或 End，即可跳到第一位或最后一位。

**如何从代码移动项目？** 调用 `MoveItem(from, to)`。它使用与拖动相同的滑动动画，并会触发 `Reordered`，因此保存顺序的处理程序也会对代码移动生效。

**键盘移动之后，焦点会停留在该行上吗？** 会。焦点保留在被移动行的抓手上，因此下一次按键会再次移动同一行。Tab 像对待其他控件一样离开列表。

## API 参考

<!-- api: ReorderableList -->

<!-- api: ReorderableListEntry -->

## 无障碍

- 每个抓手都作为按钮公开。它的名称包含项目标题及其位置，例如 "Reorder Ship pricing page. Position 1 of 4. Use arrow keys."，并且每次移动后名称都会更新。
- 列表由 `Label` 命名，默认为 "Reorder items"。请把它设为被排序对象的名称。
- 只有抓手可以获得焦点用于拖动。行中的文字是普通的、可选取的文本区域。
- 每一步都在礼貌区域中播报："Picked up Ship pricing page. Position 1 of 4. Use the arrow keys to move, Space to drop, Escape to cancel."，然后是 "Ship pricing page dropped at position 3 of 4."
- 键盘移动之后，焦点保留在被移动行的抓手上，因此下一次按键会作用于同一行。

### 键盘

| 按键 | 操作 |
|---|---|
| `Up` / `Down`（在抓手上） | 立即把该行上移或下移一位；焦点保留在其抓手上 |
| `Space` / `Enter`（在抓手上） | 拾起该行；随后方向键会移动它，列表同时预览位置 |
| `Up` / `Down`（已拾起的行） | 把被拾起的行移动一位 |
| `Home` / `End`（已拾起的行） | 把被拾起的行移到第一位或最后一位 |
| `Space` / `Enter`（已拾起的行） | 在当前位置放下该行 |
| `Escape`（已拾起的行） | 取消：该行回到原来的位置 |
| 在抓手上按指针拖动 | 把该行移到新位置；被经过的行让出空间 |

## 动效

- 按下抓手时，该行会轻微抬起。松开后，它以一个短促的弹簧回弹到原位。
- 拖动时，该行跟随指针移动，被经过的行则以弹簧滑开。
- 位置数字沿该行移动的方向滚动。
- 减弱动效时，行仍跟随指针，但落下时直接到位且不抬起。关闭动效时的行为与减弱相同。
- 把行拖到滚动区域的边缘时，该区域会滚动。

| 变化 | 完整 | 减弱 | 关闭 |
|---|---|---|---|
| 按下抓手 | 该行抬起，并以灵敏的弹簧缩放到 1.02 | 该行不抬起 | 该行不抬起 |
| 拖动 | 该行跟随指针，被经过的行以弹簧滑开 | 该行跟随指针，落下时立即到位 | 该行跟随指针，落下时立即到位 |
| 放下 | 该行以快速弹簧落定，并保留释放时的速度 | 该行立即到位 | 该行立即到位 |

## 相关组件

- `Table`：当顺序来自列的值，而不是人的选择时使用。
- `SwipeActions`：对某一行做快速操作，而不重新排序它。

| 组件 | 适用场景 |
|---|---|
| `Table` | 顺序来自某一列中的值，人们按该列排序 |
| `SwipeActions` | 某一行需要归档或稍后提醒等快速操作，而不是新的位置 |
| `TreeView` | 项目是嵌套的，人们展开文件夹或分类来查找它们 |
| `ChipGroup` | 人们从固定的选项集合中选择，而顺序不由他们决定 |
