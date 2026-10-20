---
title: 表情回应
summary: 一键回应一条内容，计数会滚动变化。
source-hash: daf4737245bc
---

## 何时使用

- 像聊天中的表情回应一样，一键回应帖子、评论或发布说明。
- 显示每个选项有多少人回应，以及当前用户是否在其中。
- 一组简短且固定的选项，例如点赞、喜爱和庆祝。

## 何时不用

- 需要文字回复的内容：应为帖子提供回复输入框。
- 查看者无法更改的计数：用 `Badge`。
- 从列表中选择一个值：用选择器或菜单。

## 用法

选项是 `Reactions` 的子元素。计数与查看者自己的回应是它的 `Value`，在代码中设置。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
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

`ReactionOption` 通过 `Icon` 接收 24 x 24 的 Lucide 图形数据。

### 值与变更

`Value` 为双向绑定。计数为 0 的胶囊会从栏中移除。`Reacted` 标记查看者自己的回应，`ReactedBy` 列出提示框中显示的名称。

```csharp
var people = new Reactions
{
    Value =
    [
        new Reaction("love", 4, true) { ReactedBy = ["Ada Okafor", "Jonas Berg", "Lin Park"] },
        new Reaction("celebrate", 2) { ReactedBy = ["Maya Chen", "Sam Ortiz"] }
    ]
};
```

`ValueChanged` 在值变化后触发。`Next` 为新值，`Change` 说明变更的选项以及查看者是否做出了回应。

```csharp
reactions.ValueChanged += (_, e) =>
{
    // 将 e.Next 随内容一并保存。
};
```

### 标签

`Label` 为整个栏命名。`AddLabel` 与 `PickerLabel` 分别为添加按钮和选择器命名。`TipDelay` 设置指针在胶囊上停留多久后才显示提示。

```xml
<arc:Reactions Label="对本次发布的回应"
               AddLabel="添加回应"
               TipDelay="0:0:0.6">
    <arc:ReactionOption Id="ship" Label="已发布" />
    <arc:ReactionOption Id="star" Label="星标" />
</arc:Reactions>
```

### 换行与宽度

胶囊放不下时，整个栏会换行。设置 `Width` 即可看到换行效果。

```xml
<arc:Reactions Width="220" HorizontalAlignment="Left" />
```

## API 参考

<!-- api: Reactions -->

<!-- api: ReactionOption -->

<!-- api: Reaction -->

## 无障碍

- 整个栏是一个由 `Label` 命名的分组。每个胶囊都是切换按钮，其名称包含标签、计数以及查看者是否在其中。
- 选择器是一个使用漫游焦点的工具栏。四个方向键在选项之间移动并循环，Home 与 End 跳到两端，每个选项都有名称。
- 在胶囊上按 Enter 或 Space 可切换查看者的回应。在添加按钮上按 Enter 或 Space 会打开选择器，按 Escape 关闭并将焦点返回添加按钮。
- 每次变更都会通过礼貌级别的状态区域播报一次，例如“已添加喜爱，共 4 个回应。”
- 当焦点所在的胶囊离开栏时，焦点会移到相邻胶囊或添加按钮。

## 动效

- 新胶囊从半尺寸带着柔和模糊放大出现。被移除的胶囊会缩小并淡出。其他胶囊平滑滑动到新位置。
- 查看者回应时图标会轻微跳动；快速连续点击会累加，而不是重新开始。
- 计数只滚动发生变化的数字：增加时向上，减少时向下。
- 选择器从添加按钮中展开，各选项依次出现。
- 减弱动效时只使用短暂淡入淡出，不使用尺寸弹簧。关闭动效时所有变化立即完成。

## 相关组件

- `Badge` 用于查看者无法更改的固定计数。
- `Popover` 与 `DropdownMenu` 用于由按钮打开的其他内容或选项。
- `SwipeActions` 用于触摸列表中的行操作。
