---
title: 卡片堆叠
summary: 逐张审阅卡片，支持划走与撤销。
source-hash: 19ea9efed1b2
---

<Demo name="Shortlist" />

## 何时使用

`CardStack` 将一个待处理队列变成卡片堆，让人逐张处理。每张卡片要求一个明确的决定，即跳过或保留，然后下一张卡片升到原位。它适合每个条目都值得单独关注、而表格会隐藏关键细节（例如商品照片或候选人简介）的审核任务。把它放在专注任务的主区域，并在旁边显示当前结果，例如已保留条目的数量。

- **商品上架审核**：商品团队在商品上架前逐一审核新商品列表。每张卡片显示照片、价格和供应商，审核人员批准或拒绝。撤销可以恢复误批的商品。
- **招聘工具中的候选人筛选**：招聘人员保留或跳过求职者档案。每张卡片只呈现一位候选人，让审阅者一次只专注一份摘要，而不是面对一排只看了一半的记录。
- **SaaS 应用中的入门选择**：新的工作区管理员逐一保留或跳过推荐的集成，例如 Slack、Jira 或 Google Drive。卡片堆最后显示“所有卡片已审阅”，而不是一张冗长的设置表单。
- **金融科技控制台中的标记交易审核**：分析师确认或放行每笔被标记的交易。每个决定在发生时都会被播报，撤销会把上一笔决定放回卡片堆顶部，然后才提交整批结果。

## 何时不用

- **并排比较或排序条目**：用 `Table`。列和排序让人一次比较多条记录；卡片堆一次只显示一条记录。
- **只浏览而不做决定**：用 `Carousel`，适合一排人们只需查看、无需保留或跳过的幻灯片、横幅或照片。
- **列表行上的快捷操作**：用 `SwipeActions`，对每一行执行归档、稍后提醒或标为已读。行是用来操作的地方，而不是一个向前推进的队列。
- **重新排列人们拥有的列表**：用 `ReorderableList`，它在移动时让每一项保持可见。
- **表单中的单个是或否**：用 `Checkbox` 或 `Switch`，它们与其他字段并排放置，不需要队列、标记或撤销。

## 用法

```xml
<arc:CardStack Label="目的地"
               LeftLabel="跳过"
               RightLabel="保留"
               LeftOutcome="已跳过"
               RightOutcome="已入围">
    <arc:CardStack.ItemTemplate>
        <DataTemplate>
            <Border Width="300" Height="360" Padding="20" CornerRadius="12" Background="#F5F1EA">
                <TextBlock Text="{Binding}" FontSize="18" />
            </Border>
        </DataTemplate>
    </arc:CardStack.ItemTemplate>
</arc:CardStack>
```

`CardStack` 派生自 Avalonia 的 `ItemsControl`，因此 `ItemsSource`、`Items`、`ItemTemplate` 以及条目容器逻辑与列表中的用法一致。每个条目成为一张卡片，即一个承载条目模板的 `ContentControl` 容器。堆叠只布局并实例化最上面的四张卡片，以及仍在移动中的卡片，因此 500 个条目的队列与 5 个条目的队列成本大致相同。`Label` 为堆叠命名，供辅助技术使用。

上面的示例为简洁起见使用了固定的卡片尺寸。在主题中，卡片高 304 px，周围的舞台高 328 px，因此按这些尺寸制作的卡面能填满堆叠，不留空隙。

### 结构

1. **舞台**（`Label` 为其命名）：容纳卡片的可聚焦分组。它预留了顶部卡片以及其下方 24 px 的区域，后面的卡片会从这里露出，因此卡片离开时不会有任何位置变动。它的宽度取可用宽度与 320 px 中的较小值。
2. **顶部卡片**：接收输入的卡片，高 304 px、宽 320 px，卡面来自 `ItemTemplate`。它是唯一响应拖动、点击或按键的卡片。
3. **后方卡片**：最多三张，每张比前一张小 5%，向下偏移 12 px，并带有透明度为 3.5% 的黑色阴影（深色模式下为 22%）。顶部卡片离开后，它们会上升到位。
4. **标记**：卡片顶角的 32 px 胶囊，距卡片边缘 16 px。顶部卡片被拖动时显示 `LeftLabel` 或 `RightLabel`，静止时完全透明。
5. **焦点环**：当舞台获得键盘焦点时，绘制在顶部卡片周围（或空状态周围）的轮廓。舞台本身不绘制轮廓。
6. **按钮行**：`LeftLabel` 按钮、撤销按钮与 `RightLabel` 按钮，彼此相距 8 px，位于舞台下方 20 px 处。撤销按钮带有旋转图标与“撤销”文字；宽度低于 22.5rem（360 px）时只保留图标。
7. **空状态**：所有卡片都做出决定后显示。它是一个与卡片等高的面板，带有 24 px 内边距、12 px 间距、标题“所有卡片已审阅”以及“重新开始”按钮。`EmptyContent` 会替换标题和按钮。

低于 26rem（416 px）时，堆叠切换到紧凑布局：卡片高 280 px，舞台高 304 px，按钮使用紧凑尺寸。切换依据堆叠自身的宽度，而不是窗口宽度，因此放在窄列中的堆叠即使在宽窗口里也会切换布局。

### 卡片正面与尺寸

卡片高度由主题固定，因此卡面必须适配 304 px × 320 px。内容保持为一个标题、一到两行详情以及一张图片或图标。较长的描述应放在人从卡片打开的详情视图中。使用主题文字尺寸（正文用 `TypeSM`，标题用 `TypeBase` 配合 `WeightMedium`），让卡面与应用其他部分保持一致。

### 绑定到视图模型

视图模型拥有队列并记录每一个决定。堆叠没有用于做决定的命令：拖动、键盘和按钮都会触发同一个 `Decided` 事件，因此一个处理程序即可覆盖所有输入方式。视图模型持有集合，代码后置将每个决定转发给它。

```csharp
public interface IShortlistService
{
    IEnumerable<Destination> LoadCandidates();

    Task SaveDecisionAsync(int id, bool keep);
}

public partial class ShortlistViewModel : ObservableObject
{
    private readonly IShortlistService _shortlist;

    public ShortlistViewModel(IShortlistService shortlist)
    {
        _shortlist = shortlist;
        Destinations = new ObservableCollection<Destination>(shortlist.LoadCandidates());
    }

    public ObservableCollection<Destination> Destinations { get; }

    [ObservableProperty]
    private string _lastDecision = "拖动卡片，或让堆叠获得焦点后按方向键。";

    public async Task RecordAsync(Destination item, CardDecision decision)
    {
        await _shortlist.SaveDecisionAsync(item.Id, decision == CardDecision.Right);
        LastDecision = decision == CardDecision.Right
            ? $"{item.Name} 已保留。"
            : $"{item.Name} 已跳过。";
    }
}
```

```xml
<StackPanel Spacing="16">
    <arc:CardStack x:Name="Shortlist"
                   Label="目的地"
                   ItemsSource="{Binding Destinations}">
        <arc:CardStack.ItemTemplate>
            <DataTemplate>
                <StackPanel Spacing="6" Margin="20">
                    <TextBlock Text="{Binding Name}" FontSize="18" FontWeight="Medium" />
                    <TextBlock Text="{Binding Details}" FontSize="14" />
                </StackPanel>
            </DataTemplate>
        </arc:CardStack.ItemTemplate>
    </arc:CardStack>
    <TextBlock Text="{Binding LastDecision}" />
</StackPanel>
```

```csharp
// 代码后置：将决定转发给视图模型。
Shortlist.LabelSelector = item => ((Destination)item!).Name;
Shortlist.Decided += async (_, e) =>
    await ViewModel.RecordAsync((Destination)e.Item!, e.Decision);
```

- `ItemsSource` 是单向的：堆叠读取集合，但从不写入。`CurrentIndex` 与 `CanUndo` 是只读的，因此将它们单向绑定即可显示进度，例如“3 / 12”。
- 决定本身使用 `Decided` 与 `DecisionUndone`。事件参数携带条目与决定，因此视图模型无需读取控件的状态。
- `ResetCommand` 是一个用于重新开始卡片堆的 `ICommand`。它只有在 `CanUndo` 为 true 时才能执行，因此堆叠外的“再次审阅”按钮在有决定之前保持禁用。
- 更改集合（添加、删除或替换条目）会让堆叠从头开始。更改条目的某个属性则不会，因此当详情变化时应原地更新记录。

### 在代码中做决定与撤销

`Decide` 以默认的轻扫速度，沿指定方向划走顶部卡片。`Undo` 将最后一张卡片放回，`CanUndo` 说明是否有可放回的卡片。`Reset` 将每张卡片都放回堆叠。

```csharp
stack.Decided += (_, e) => log.Text = $"{((Destination)e.Item!).Name}：{(e.Decision == CardDecision.Right ? "已入围" : "已跳过")}";
stack.DecisionUndone += (_, e) => log.Text = $"{((Destination)e.Item!).Name}：已恢复";
stack.StackReset += (_, _) => log.Text = "所有卡片已放回堆叠。";

stack.Decide(CardDecision.Right);
stack.Undo();
stack.Reset();
```

用这些调用可以从工具栏、宿主应用的快捷键或不使用指针的测试中驱动卡片堆。在代码中做出的决定会触发与拖动或按键相同的事件，因此视图模型的逻辑无需改动。

### 为卡片命名

将 `LabelSelector` 设为返回每张卡片的名称。该名称即卡片的无障碍标签，并会出现在播报中。当条目不是字符串时，选择器必须返回字符串。

```csharp
stack.LabelSelector = item => ((Destination)item!).Name;
```

没有选择器时，如果条目本身是字符串，堆叠就使用条目本身；如果是控件条目，则使用其自动化名称；否则使用条目的 `ToString()`。对于任何模型类，都应设置选择器，使名称不依赖于 `ToString()`。

### 自定义按钮与标记文字

`LeftLabel` 与 `RightLabel` 为按钮以及拖动卡片时出现的标记命名。默认值为“跳过”和“保留”。`LeftOutcome` 与 `RightOutcome` 是播报中使用的过去式词语，例如“已跳过”。没有结果词时，播报会说明动作本身，例如“里斯本：保留。剩余 4 张。”

<Demo name="CustomActions" />

标签是纯文本，因此应写成人执行的动词：候选人卡片堆用“暂不”和“面试”，商品审核用“拒绝”和“批准”。每个标签都应足够短，能与其他按钮并排放置。

### 空状态

`EmptyContent` 会替换所有卡片都已做出决定后显示的默认消息。将按钮绑定到 `ResetCommand`，即可提供开始新一轮的入口。`EmptyContentTemplate` 通过数据模板构建同样的内容，并且仅在未设置 `EmptyContent` 时使用。

```xml
<arc:CardStack Label="候选人" ItemsSource="{Binding Candidates}">
    <arc:CardStack.EmptyContent>
        <Button Content="再次审阅" Command="{Binding ResetCommand, RelativeSource={RelativeSource AncestorType=arc:CardStack}}" />
    </arc:CardStack.EmptyContent>
</arc:CardStack>
```

### 照片

卡片正面可以在绘制的场景之上显示 `AsyncImage` 照片，因此卡片在照片加载期间或设备离线时仍然显得完整。照片应保持在卡片固定的高度之内，并让绘制的场景在图片到达之前代为占位。

<Demo name="Photos" />

### 数据结构与性能

- **条目模型**：任何对象都可以。每个条目只保留卡面需要的内容，详情则在单独的视图中按需加载。只要卡面模板很轻，大型模型的开销并不比小型模型更高。
- **条目数量**：堆叠只实例化四张卡片以及仍在移动的卡片，因此条目数量不影响每一帧的开销。真正有影响的是卡面模板，它会为这四张卡片各运行一次。
- **更新**：添加、删除或替换条目会让卡片堆从第一张卡片重新开始。规划更新时，应避免让人丢失正在处理的队列：更新单条记录时原地修改，只有在批次本身变化时才替换集合。
- **撤销历史**：每个决定都会被保留，以便 `Undo` 将其放回；撤销按相反的顺序进行，最近的决定最先撤销。

### 示例：商品上架审核

一家零售商的商品团队在新商品上架前进行审核。每张卡片是一个商品，包含照片区域、名称、价格和供应商。审核人员批准或拒绝商品，撤销可以恢复上一个。按钮标签使用审核的动词，结果词使用播报中的词语（“已批准”“已拒绝”），因此朗读出的状态与按钮一致。卡片堆下方的状态行用平实的文字告诉审核人员上一次的操作。为使截图自成一体，此处卡面使用固定文案；在应用中，应将相同元素绑定到商品数据。

![A product listing card in a review deck with Reject, Undo and Approve buttons](images/ProductReview.png)

```xml
<StackPanel Width="560" Spacing="16">
    <TextBlock Text="New listings to review"
               FontSize="16"
               FontWeight="Medium"
               Foreground="{arc:ArcDesignTokenResource Foreground}" />
    <arc:CardStack Label="Listings awaiting review"
                   LeftLabel="Reject"
                   RightLabel="Approve"
                   LeftOutcome="rejected"
                   RightOutcome="approved"
                   HorizontalAlignment="Center">
        <arc:CardStack.ItemTemplate>
            <DataTemplate>
                <StackPanel Spacing="12" Margin="20">
                    <Border Height="120"
                            CornerRadius="10"
                            Background="{arc:ArcDesignTokenResource SurfaceMuted}">
                        <arc:ArcGlyph Data="{x:Static arc:ArcIcons.Package}"
                                      Size="48"
                                      HorizontalAlignment="Center"
                                      VerticalAlignment="Center"
                                      Foreground="{arc:ArcDesignTokenResource TextMuted}" />
                    </Border>
                    <TextBlock Text="Trail Runner 2"
                               FontSize="18"
                               FontWeight="Medium"
                               Foreground="{arc:ArcDesignTokenResource Foreground}" />
                    <TextBlock Text="Northwind · Footwear · $129.00"
                               FontSize="14"
                               Foreground="{arc:ArcDesignTokenResource TextSecondary}" />
                    <TextBlock Text="Submitted by Mia Chen on 2026-10-18"
                               FontSize="13"
                               Foreground="{arc:ArcDesignTokenResource TextMuted}" />
                </StackPanel>
            </DataTemplate>
        </arc:CardStack.ItemTemplate>
        <x:String>Trail Runner 2</x:String>
        <x:String>Harbor Rain Shell</x:String>
        <x:String>Ridge Field Jacket</x:String>
    </arc:CardStack>
    <TextBlock Text="Drag a listing, or focus the deck and press the arrow keys."
               FontSize="13"
               HorizontalAlignment="Center"
               Foreground="{arc:ArcDesignTokenResource TextSecondary}" />
</StackPanel>
```

### 文案规范

- **堆叠标签**：一个名词短语，用来说明队列是什么，例如“待审核的商品列表”。它是屏幕阅读器给予堆叠的唯一名称，因此只写“审核”是不够的。
- **卡片名称**：只写条目的名称，因为堆叠会自行补上位置信息：“里斯本，第 1 项，共 5 项”。
- **按钮标签**：一个简短的动词，说明决定是什么：“跳过”和“保留”，“拒绝”和“批准”。避免使用“是”和“否”这类不说明条目会发生什么的回答。
- **结果词**：使用过去式，使播报读起来像一个结果：“里斯本已批准。剩余 3 张。”
- **空状态**：说明审核已经完成，并提供重新开始的方式：“所有卡片已审阅”，配以“重新开始”。

| 不要写 | 改为 |
|---|---|
| 是 / 否 | 批准 / 拒绝 |
| 左 / 右 | 跳过 / 保留 |
| 完成！ | 所有卡片已审阅。 |
| 已批准 | 已批准的商品，或条目名称加结果 |
| 重置 | 重新开始 |

### 最佳实践

**推荐**

- 为堆叠设置一个说明队列内容的 `Label`。卡片堆获得焦点时屏幕阅读器会播报它，因此它是人最先听到的内容。
- 使用过去式的 `LeftOutcome` 与 `RightOutcome` 词语，使每个决定都以结果的形式被播报。
- 在视图模型中记录 `Decided` 的每一个决定。拖动、键盘和按钮因此以相同的方式保存。
- 当队列可以复用时，在空状态中提供“重新开始”或“再次审阅”，让结束状态有一个去处。

**避免**

- 用卡片堆处理需要并排比较的记录。需要比较各列的读者会失去其他条目的上下文。
- 在卡片内部放置水平拖动或其他手势。它们会与卡片自身的拖动发生冲突。
- 卡面上放置过长的文字。卡片高度固定，放不下的卡面会被裁切。
- 只依赖标记来传达信息。标记只在拖动期间出现，因此键盘用户和屏幕阅读器用户依赖的是按钮与播报。
- 为刷新一条记录而替换整个集合。这会让卡片堆重新开始；应改为更新该记录。

### 常见问题

**为什么撤销保持禁用？** 只有在做出决定之后撤销才可用；撤销最后一个决定后，`CanUndo` 会再次变为 `False`。禁用时按钮仍可获得焦点，因此键盘用户不会丢失位置。

**如何显示还剩多少张卡片？** 将数量单向绑定到堆叠的 `CurrentIndex`，再用条目总数减去它；或者读取播报中“剩余 4 张”的部分。堆叠没有提供剩余数量的属性。

**卡片堆为空后，能否从代码重新开始？** 可以。调用 `Reset()`，或执行 `ResetCommand`。两者都会让每张卡片回到堆叠中，并且卡片从最底层的一张开始返回。

**编辑一个条目会让卡片堆重新开始吗？** 不会。更改条目的某个属性会让卡片堆保持在原处。在集合中添加、删除或替换条目才会使其重新开始，因此应原地更新记录。

**使用键盘时，为什么卡片不倾斜？** 倾斜与标记跟随拖动。按键会以默认的轻扫速度做出决定，因此没有拖动来让卡片倾斜。

## API 参考

<!-- api: CardStack -->

`CardDecision` 以及事件数据是本页使用的公共类型：

| 类型 | 成员 | 含义 |
|---|---|---|
| `CardDecision` | `Left` | 卡片被跳过（`LeftLabel`，默认“跳过”）。 |
| `CardDecision` | `Right` | 卡片被保留（`RightLabel`，默认“保留”）。 |
| `CardStackDecisionEventArgs` | `Item` | 被决定或被恢复的条目。 |
| `CardStackDecisionEventArgs` | `Index` | 该条目在条目集合中的索引。 |
| `CardStackDecisionEventArgs` | `Decision` | 决定，`Left` 或 `Right`。 |

## 无障碍

- 舞台是一个以 `Label` 命名的可聚焦分组（默认为“Cards”），角色为“card stack”。其帮助文字说明了方向键与 Backspace，并说明顶部卡片可以被跳过或保留。
- 每张卡片由 `LabelSelector` 命名，并附带它在堆叠中的位置，例如“里斯本，第 1 项，共 5 项”。
- 决定、撤销与重置都以礼貌级别播报，因此屏幕阅读器能听到它们而不丢失焦点。例如“里斯本已跳过。剩余 1 张。”“京都已恢复。”以及“所有卡片已放回堆叠。”
- 按钮与每个手势一一对应，因此划动从来不是做决定的唯一方式。禁用的撤销按钮会保留焦点，并被播报为不可用，因此键盘用户在没有可撤销的决定时不会丢失位置。
- 焦点环绘制在顶部卡片或空状态上，而不是舞台上。

### 键盘

舞台通过 `Tab` 获得焦点。只有舞台本身获得焦点时，方向键才会做出决定；当按钮获得焦点时，方向键照常作用于该控件。

| 按键 | 操作 |
|---|---|
| `Left` / `Right` | 舞台获得焦点时，跳过或保留顶部卡片 |
| `Backspace` | 撤销上一个决定 |
| `Ctrl+Z` / `Cmd+Z` | 撤销上一个决定 |
| `Tab` / `Shift+Tab` | 在舞台与按钮之间移动；撤销按钮禁用时仍保留在顺序中 |
| `Enter` 或 `Space` | 按下已获得焦点的按钮：跳过、撤销、保留或重新开始 |

堆叠不处理带有 `Ctrl` 或 `Cmd` 的方向键，因此宿主应用中的 `Cmd+Left` 等快捷键仍会被应用接收。

## 动效

- 顶部卡片跟随拖动，朝移动方向倾斜并显示标记。快速轻扫或长距离拖动，会让卡片带着松手时的速度划出堆叠。
- 顶部卡片离开后，后面的卡片上升到位。
- 撤销会让最后一张卡片从离开的位置飞回。重置时，卡片从最深处的一张开始依次飞回。
- 短距离拖动会弹回堆叠。
- 减弱动效时，卡片在原位置淡出再淡入，不产生位移。关闭动效时立即到达最终状态。

| 变化 | 完整 | 减弱 | 关闭 |
|---|---|---|---|
| 顶部卡片越过阈值后松手 | 带着松手速度划出，然后淡出 | 原位淡出 | 立即移除 |
| 一次决定后，后面的卡片 | 顶部卡片离开时上升到位 | 原位淡入 | 本已在位 |
| 撤销 | 从卡片离开的位置飞回 | 原位淡入 | 立即显示 |
| 重新开始 | 卡片从最深处开始依次飞回 | 原位淡入 | 立即显示 |
| 空状态出现 | 模糊消退时以短距离移动落定 | 淡入 | 立即显示 |

## 相关组件

- `Carousel` 用于浏览一排条目，而不对每一项做决定。
- `SwipeActions` 用于列表行上的快捷操作。
- `Table` 用于并排审阅大量记录。

| 组件 | 适用场景 |
|---|---|
| `Table` | 读者按列比较或排序大量记录 |
| `Carousel` | 人们只浏览幻灯片、横幅或照片，无需做决定 |
| `SwipeActions` | 列表的每一行需要归档或稍后提醒等快捷操作 |
| `ReorderableList` | 人们调整自己拥有的列表顺序 |
| `Checkbox` / `Switch` | 表单中并排放置单个是或否 |
| `Stepper` | 任务是固定的步骤序列，而不是条目队列 |
