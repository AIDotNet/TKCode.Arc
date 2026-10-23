---
title: 表情回应
summary: 一键回应一条内容，计数会滚动变化。
source-hash: cc5e43d20f00
---

<Demo name="Default" />

## 何时使用

`Reactions` 是一排带计数的回应胶囊，外加一个打开小型选择器的添加按钮。它让人一键回应一段内容，无需写回复。它位于所回应内容的下方，例如发布说明、状态更新或设计评审评论，计数显示每个选项有多少人作出了回应。

- **发布说明与变更日志条目**：团队看到“2026.10 版本已上线”，点按“已发布”或“庆祝”。计数显示有多少工程师看过该版本；保存的列表包含查看者自己的回应时，该回应会高亮显示。
- **团队工作区中的状态更新**：每日事故摘要或项目更新会收到读者的“点赞”或“感谢”，作者无需开启回复串就能知道更新已被看到。
- **设计评审评论**：评审者点按一下，就把评论标记为“同意”或“有疑问”。计数告诉作者团队已经接受了哪些评论。
- **内部门户中的公告信息流**：欢迎新同事的帖子会收到“喜爱”和“庆祝”。提示框列出回应者的名字，让计数变成一种“谁看过”的信号。

## 何时不用

- **需要文字的回应**，例如提问、决定或对评论的回复：用 `CommentThread` 或 `ChatThread`，它们把文字与作者放在一起。回应无法承载一句话。
- **查看者无法更改的计数**，例如未关闭的工单数或未读消息数：固定计数用 `Badge`；当数字需要在指标中滚动显示时，用 `AnimatedCounter`。
- **从列表中选择一个值**，例如优先级、状态或分类：用 `Select`、`Combobox` 或 `MorphSelect`。回应栏允许同时选择多个选项，没有单一结果。
- **数字刻度上的评分**，例如一到五星或满意度分数：用 `Slider`，或用存储数字的表单控件。回应表达的是选择，而不是数量。
- **触摸列表中的行操作**，例如收件箱行上的归档或标为已读：用 `SwipeActions`，它让操作紧挨着其所作用的行。

## 用法

选项是 `Reactions` 的子元素。计数与查看者自己的回应构成它的 `Value`，可以在代码中设置，也可以从视图模型绑定。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:Reactions Label="表情回应">
        <arc:ReactionOption Id="like" Label="点赞" />
        <arc:ReactionOption Id="love" Label="喜爱" />
        <arc:ReactionOption Id="celebrate" Label="庆祝" />
    </arc:Reactions>
</UserControl>
```

```csharp
var reactions = new Reactions
{
    Value = [new Reaction("like", 3)]
};
reactions.Options.Add(new ReactionOption("like", "点赞", ArcIcons.ThumbsUp));
reactions.Options.Add(new ReactionOption("love", "喜爱", ArcIcons.Heart));
reactions.Options.Add(new ReactionOption("celebrate", "庆祝", ArcIcons.Smile));
```

`Reactions` 派生自 Avalonia 的 `TemplatedControl`，因此它的模板、样式和布局与其他 Arc 控件的行为一致。选项只是普通数据（`ReactionOption` 不是控件），栏会为每个有计数的回应构建一个切换胶囊。`Options` 与 `Value` 中使用相同的 `Id`：Id 是服务端为回应存储的值，Label 只是给人阅读的文字。

`ReactionOption` 通过 `Icon` 接收 24 x 24 的 Lucide 几何图形，以 1.75 的描边绘制。`ArcIcons` 中没有的图标可以用 `ArcIcons.Parse` 解析，或取自 `StreamGeometry` 资源，正如示例库为彩炮图标所做的那样。

### 结构

1. **栏**（`Label` 为其命名）：把胶囊包裹成多行的分组。同一行中胶囊相距 6 px，行与行相距 8 px。栏会按所需行数增高。
2. **胶囊**：高 32 px，圆角为胶囊圆角。图标前内边距 9 px，计数后内边距 11 px，图标与计数之间 6 px。图标 16 px，计数使用小号文字与次要文字颜色。
3. **计数**：回应的人数。数字变化时，只滚动发生变化的数位。
4. **查看者自己的回应**：设置了 `Reacted` 的胶囊带有淡色强调填充与深色强调文字。其边框会加入一圈强调色，按 45% 混入边框色，因此在任何表面上都清晰可见。
5. **添加按钮**（`AddLabel`，默认为“添加回应”）：宽 38 px、高 32 px，显示一个笑脸加号图标。选择器打开时改为显示关闭图标。
6. **选择器**（`PickerLabel`，默认为“添加一个回应”）：在添加按钮上方打开的托盘，与按钮相距 8 px。内容内边距为 5 px。每个选项是直径 40 px 的圆形切换按钮，图标 20 px；选项下方的说明文字会指出高亮的选项。查看者已选择该选项时，说明文字会加上一个勾号。
7. **提示**：胶囊的名称与提示框，在 `TipDelay`（默认 450 ms）之后出现。它显示选项标签；已知 `ReactedBy` 时，还显示回应的人。

### 值与变更

`Value` 为双向绑定。每个 `Reaction` 包含一个 id、一个计数和查看者自己的标记。计数为 0 的回应不会显示，因此会离开栏。id 与任何选项都不匹配的回应同样不显示。同一个 id 在 `Value` 中出现两次时，以第一项为准。

| 操作 | `Value` 中的结果 | 事件 |
|---|---|---|
| 查看者点按别人的胶囊 | 计数加 1，`Reacted` 设为 `true` | `ValueChanged`，`Change.Reacted` 为 `true` |
| 查看者点按自己的胶囊 | 计数减 1，`Reacted` 设为 `false`；计数为 0 时移除该回应 | `ValueChanged`，`Change.Reacted` 为 `false` |
| 查看者选择一个还没有胶囊的选项 | 新增一个计数为 1 的回应，加到列表末尾 | `ValueChanged` |
| 代码设置 `Value` | 栏显示新列表；不引发变更 | 无 |

<Demo name="WhoReacted" />

`ValueChanged` 在 `Value` 变化后引发。其 `Next` 属性是栏顺序下的新值（不含计数已降为零的回应），`Change` 属性说明选项名称以及查看者是否做出了回应。

```csharp
reactions.ValueChanged += (_, e) =>
{
    // 将 e.Next 与内容一并保存，并用 e.Change 说明查看者做了什么。
};
```

`Toggle(id)` 执行与点按相同的变更，因此键盘快捷键或菜单项可以在代码中为查看者添加或移除回应。`OpenPicker()` 与 `ClosePicker()` 打开和关闭选择器，`IsPickerOpen` 表示选择器是否打开。

### 绑定到视图模型

在 MVVM 应用中，视图模型持有已保存的回应，查看者点按时栏会更新它们。每个 `Reaction` 都是不可变记录，因此视图模型应替换列表，而不是原地修改某一项。使用 CommunityToolkit.Mvvm：

```csharp
public partial class ReleaseNoteViewModel : ObservableObject
{
    private readonly IReleaseNoteService _notes;
    private readonly string _noteId;

    public ReleaseNoteViewModel(IReleaseNoteService notes, string noteId)
    {
        _notes = notes;
        _noteId = noteId;
    }

    [ObservableProperty]
    private IReadOnlyList<Reaction> _reactionValues = [];

    public async Task SaveReactionsAsync(IReadOnlyList<Reaction> next)
    {
        await _notes.SaveReactionsAsync(_noteId, next);
    }
}
```

```xml
<arc:Reactions Label="对 2026.10 版本的回应"
               Value="{Binding ReactionValues}"
               HorizontalAlignment="Left">
    <arc:ReactionOption Id="ship" Label="已发布" Icon="{x:Static arc:ArcIcons.Zap}" />
    <arc:ReactionOption Id="love" Label="喜爱" Icon="{x:Static arc:ArcIcons.Heart}" />
    <arc:ReactionOption Id="celebrate" Label="庆祝" Icon="{x:Static arc:ArcIcons.Smile}" />
</arc:Reactions>
```

```csharp
// 在视图的代码后置文件中，即视图模型可用的地方：
reactions.ValueChanged += async (_, e) => await viewModel.SaveReactionsAsync(e.Next);
```

- `Value` 默认为双向绑定，因此栏会在引发 `ValueChanged` 之前把每次点按写回 `ReactionValues`。请保持双向绑定；单向绑定会让栏显示视图模型从未保存的变化。
- 栏本身不记住查看者。哪些回应属于已登录用户，由服务端或视图模型决定；加载列表时由它设置 `Reacted`。
- 编译绑定需要在视图上设置 `x:DataType`（为简洁起见，这里的示例省略了它）。

### 换行与宽度

栏放不下时会把胶囊换行。设置 `Width` 即可看到换行效果，或让宽度自由，使栏占满父容器的宽度。变更增加一行时，栏的高度会以弹簧过渡到新尺寸，胶囊则平滑滑动到新位置。

<Demo name="Wrapping" />

在密集的列表行或卡片页脚中，只要给栏足够的宽度容纳预期数量的选项，就能让它保持一行。在窄列中，换行是正确的行为：它让每个胶囊都可见，无需滚动条。

### 标签与提示

`Label` 为栏命名，`AddLabel` 为添加按钮及其提示命名，`PickerLabel` 为选择器命名。名称应说明栏指向什么，例如“对本次发布的回应”，让屏幕阅读器用户在听到胶囊之前先听到上下文。

`TipDelay` 设置指针停留在胶囊上多久后才显示提示，默认为 450 ms。`TipSelector` 为每个胶囊构建提示：它接收选项与回应，返回要显示的内容，或返回 `null` 以使用默认文字。

```xml
<arc:Reactions Label="对本次发布的回应"
               AddLabel="添加回应"
               TipDelay="0:0:0.6">
    <arc:ReactionOption Id="ship" Label="已发布" />
    <arc:ReactionOption Id="star" Label="星标" />
</arc:Reactions>
```

提示由选项标签加上回应者构成（数据中有这些人时）。默认最多列出三个名字，查看者已回应时“你”排在最前，然后说明还有多少其他人：

| 回应数据 | 提示 |
|---|---|
| 没有 `ReactedBy` | "喜爱" |
| `Reacted` 且有两个名字 | "喜爱 · 你、Ada 和 Lin" |
| 六人，列出四个名字 | "喜爱 · Ada、Lin、Sam 等另外 3 人" |

### 谁回应了

在 `Reaction` 上设置 `ReactedBy`，即可说明计数背后的人。名字只显示在提示框中，不显示在栏里，因此胶囊保持紧凑。按你希望的顺序传入名字，最相关的排在前面。提示框最多显示三个名字。

名字是可选的。只有计数而没有名字依然正确，此时提示框回退为显示选项标签。

### 实时计数

在别人屏幕上变化的计数通过 `Value` 到达。栏只滚动发生变化的数位，计数增加时向上，减少时向下；它不会对图标播放点按的冲击效果，因为查看者并没有点按。实时更新中出现的新胶囊会进入栏，计数降为零的胶囊则会离开栏。

<Demo name="LiveCounts" />

服务端发送更新时，用新列表替换 `Value` 中的列表。栏按 `Id` 匹配回应，因此留在列表中的胶囊保持其身份，只有计数发生变化。新的 id 会得到新胶囊，被移除的 id 对应的胶囊则会离开栏。

### 示例：发布说明中的表情回应

内部变更日志中的一条发布说明。回应位于摘要下方，团队无需回复即可确认这次发布。栏的标签写明了发布版本，计数显示团队的反应，查看者自己的“喜爱”会高亮显示。每个选项名称都是简短的动词或短语，在提示框中读起来通顺。在栏上方放一段简短摘要，让回应有具体的对象可指。栏绑定到上文所示视图模型的 `ReactionValues`，因此打开说明时会显示已保存的计数。

```xml
<Border Padding="20"
        CornerRadius="12"
        BorderThickness="1"
        BorderBrush="{arc:ArcDesignTokenResource Border}"
        Background="{arc:ArcDesignTokenResource Surface}">
    <StackPanel Spacing="12">
        <TextBlock Text="2026.10 版本已上线"
                   FontSize="16"
                   FontWeight="SemiBold"
                   Foreground="{arc:ArcDesignTokenResource Foreground}" />
        <TextBlock Text="用量导出最多快四倍，账单摘要现在读取新的发票结构。"
                   FontSize="14"
                   TextWrapping="Wrap"
                   Foreground="{arc:ArcDesignTokenResource TextSecondary}" />
        <arc:Reactions Label="对 2026.10 版本的回应"
                       Value="{Binding ReactionValues}"
                       HorizontalAlignment="Left">
            <arc:ReactionOption Id="ship" Label="已发布" Icon="{x:Static arc:ArcIcons.Zap}" />
            <arc:ReactionOption Id="love" Label="喜爱" Icon="{x:Static arc:ArcIcons.Heart}" />
            <arc:ReactionOption Id="celebrate" Label="庆祝" Icon="{x:Static arc:ArcIcons.Smile}" />
        </arc:Reactions>
    </StackPanel>
</Border>
```

```csharp
// 服务端在说明加载时返回已保存的计数；查看者自己的回应设置了 Reacted。
viewModel.ReactionValues =
[
    new Reaction("ship", 12),
    new Reaction("love", 4, true) { ReactedBy = ["Ada Okafor", "Jonas Berg", "Lin Park"] },
    new Reaction("celebrate", 2)
];
```

栏按 `Value` 的顺序显示回应，查看者自己的“喜爱”带有强调色填充。它的提示框显示为“喜爱 · 你、Ada Okafor、Jonas Berg 以及另外 1 人”：查看者排在最前，总共最多三个名字，然后是其余人数。

### 文案规范

- **选项标签**：一到两个词，用人们平常说的方式命名这个回应，例如“点赞”“喜爱”“庆祝”“已发布”。标签会被朗读并显示在提示框中，因此避免使用“thumbs_up”或“+1”之类的代码。
- **栏标签**：写明回应所指的内容，例如“对 2026.10 版本的回应”，而不是笼统的“回应”。
- **添加按钮**：默认为“添加回应”，与选择器的用途相符。除非产品对同一动作使用不同的术语，否则保留默认值。
- **计数用语**：无障碍名称以数字给出计数（“点赞，4 个回应，包括你的”）。不要在栏的 `Label` 中重复计数。
- **提示**：标签加回应者，末尾不加标点。

| 不要写 | 改为 |
|---|---|
| 回应 | 对 2026.10 版本的回应 |
| +1 | 点赞 |
| 点击回应 | 添加回应 |
| thumbs_up | 点赞 |
| 笑脸 | 庆祝 |

### 最佳实践

**推荐**

- 为每个选项设置稳定的 `Id`（数据会存储它），并设置可随语言变化的 `Label`。更改 id 会创建新的回应，并让旧计数失去归属。
- 加载列表时从已登录用户的数据中设置 `Reacted`。没有它，栏就无法显示查看者自己的回应，第二次点按会增加计数而不是减少计数。
- 在 `ValueChanged` 中保存变更，或把 `Value` 绑定到会保存它的视图模型。未保存的点按会在重新加载后回退，人与人之间的计数也会对不上。
- 选项集合保持简短，三到六个。选择器的设计本就紧凑，列表太长会让选择比点按一下更慢。
- 用 `Label` 为栏命名，让屏幕阅读器用户知道这些回应指的是什么。

**避免**

- 用于需要回复的选择。在提案上让人“同意”的栏会隐藏投票背后的理由；应使用评论串或投票。
- 把计数为零显示为胶囊。零计数按设计会离开栏，因此应把零计数存为缺失。
- 在栏已经显示的列表中原地修改 `Reaction`。记录是不可变的，应使用 `with` 替换该项并赋值新列表，否则栏可能不会刷新。
- 依赖提示来传达含义。提示在悬停或聚焦并经过延迟后才出现，因此选项标签本身必须已经能单独表达含义。
- 两个选项使用相同的标签。提示和屏幕阅读器的名称无法区分它们，查看者也无法选对。

### 常见问题

**为什么栏里不显示某个回应？** 栏只显示计数大于零且 `Id` 与某个选项匹配的回应。请检查选项 id 与回应 id 是否一致，并把计数设为正数。

**如何在代码中移除查看者做出的回应？** 对查看者已有的回应调用 `Toggle(id)`。查看者自己的回应会被移除，计数减一。对查看者没有做出的回应调用 `Toggle`，则会为其加一。

**为什么别人回应时计数没有变化？** 控件不会轮询服务端。数据变化时（例如来自实时信息流或刷新后）更新 `Value`，计数随即滚动到新值。

**选项的顺序会改变胶囊的顺序吗？** 不会。选择器按添加顺序显示选项。栏按 `Value` 的顺序显示回应，查看者新添加的回应会加到该列表末尾。

**查看者自己的回应如何在重新加载后保留？** 除非你的数据存储了它，否则不会保留。请与内容一同保存 `Reacted` 或回应的 `ReactedBy` 名字，并在下次打开页面时加载到 `Value` 中。

## API 参考

<!-- api: Reactions -->

<!-- api: ReactionOption -->

<!-- api: Reaction -->

### 变更值

`ValueChanged` 传递一个 `ReactionChange`，其中包含被变更的回应以及查看者的新状态。

| 成员 | 含义 |
|---|---|
| `Id` | 查看者点按或选择的选项 id |
| `Reacted` | 查看者做出回应时为 `true`，移除回应时为 `false` |

## 无障碍

- 整个栏是一个由 `Label` 命名的分组。每个胶囊都是切换按钮，其名称包含标签、计数以及查看者是否在其中，例如“点赞，4 个回应，包括你的”。
- 选择器是一个使用漫游焦点的工具栏。四个方向键在选项之间移动并循环，Home 与 End 跳到两端，每个选项都有名称。
- 在胶囊上按 Enter 或 Space 会切换查看者的回应。在添加按钮上按 Enter 或 Space 会打开选择器，按 Escape 关闭选择器并将焦点返回添加按钮。
- 每次变更都会通过礼貌级别的状态区域播报一次，例如“已添加喜爱，共 4 个回应。”该消息不显示在屏幕上。
- 焦点所在的胶囊离开栏时，焦点会移到相邻的胶囊或添加按钮，键盘焦点永远不会丢失。
- 悬停只在鼠标操作时高亮选择器中的选项。触摸直接选择选项，触摸用户无需先悬停。

### 键盘

| 按键 | 操作 |
|---|---|
| `Tab` / `Shift+Tab` | 在胶囊与添加按钮之间移动 |
| 胶囊上的 `Enter` 或 `Space` | 开启或关闭查看者的回应 |
| 添加按钮上的 `Enter` 或 `Space` | 打开选择器，并把焦点移到第一个选项 |
| `Right` / `Down`、`Left` / `Up`（在选择器中） | 移到下一个或上一个选项；焦点在两端处循环 |
| `Home` / `End`（在选择器中） | 移到第一个或最后一个选项 |
| `Enter` 或 `Space`（在选择器中） | 选中该选项，切换查看者的回应，并把焦点返回添加按钮 |
| `Escape`（在选择器中） | 关闭选择器，并把焦点返回添加按钮 |

## 动效

- 新胶囊从半尺寸开始，带着柔和模糊放大出现。被移除的胶囊会缩小并淡出。其他胶囊平滑滑动到新位置。
- 查看者回应时，图标会有一个小幅跳动；快速连续点按会叠加，而不是重新开始。
- 计数只滚动发生变化的数位，增加时向上，减少时向下。
- 选择器从添加按钮中展开，各选项依次出现。
- 减弱动效时只使用短暂淡入淡出，不使用尺寸弹簧。关闭动效时所有变化立即完成。

| 变化 | 完整 | 减弱 | 关闭 |
|---|---|---|---|
| 胶囊出现 | 从半尺寸与柔和模糊中放大出现，带有轻快的缩放与快速淡入 | 短暂淡入 | 立即完成 |
| 胶囊减为零时离开 | 缩小到 0.6 并带轻微模糊地淡出；其他胶囊平滑滑动 | 短暂淡入 | 立即完成 |
| 查看者点按回应 | 图标跳动，快速点按会叠加速度 | 无跳动 | 无跳动 |
| 计数变化 | 只滚动发生变化的数位，增加时向上，减少时向下 | 瞬间淡入 | 立即完成 |
| 行数变化 | 栏的高度以弹簧变化，胶囊平滑滑动 | 无尺寸弹簧 | 立即完成 |
| 选择器打开 | 从添加按钮中展开；选项在 40 ms 后出现，每个间隔 35 ms | 短暂淡入 | 立即完成 |

## 相关组件

- `Badge`：查看者无法更改的固定计数。
- `Popover` 与 `DropdownMenu`：由按钮打开的其他内容或选项。
- `SwipeActions`：触摸列表中的行操作。
- `HoldToConfirm`：不应因误触而执行的单个破坏性操作。

| 组件 | 适用场景 |
|---|---|
| `CommentThread` | 回应需要文字，并且评论应保留作者与时间 |
| `ChatThread` | 对话是人与人之间的一系列消息 |
| `Badge` | 数字固定，查看者无法更改 |
| `Select` / `MorphSelect` | 人从列表中选择一个值 |
| `SwipeActions` | 动作属于触摸列表中的某一行 |
| `Slider` | 人在刻度上设置数量 |
