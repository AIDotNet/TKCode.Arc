---
title: 展开式按钮组
summary: 紧凑排列的图标按钮组：指向或聚焦的按钮会展开显示文字，相邻按钮让位，操作会在原位确认。
source-hash: e8d67520ebca
---

## 何时使用

- 消息、文件或卡片上的三到六个相关操作，例如回复、归档和删除。
- 紧凑的图标操作，每个操作只在获得焦点时才需要显示文字。
- 需要在原位用过去式文字确认的操作，例如从归档变为已归档。

## 何时不用

- 需要保持选中状态的选择：用 Toggles 分类中的 `SegmentedControl`。
- 一两个操作且空间充足：用带文字的 `Button`。
- 超过约六个操作：用 Menus 分类中的 `DropdownMenu`。
- 需要始终显示文字的图标操作：用 `FloatingButtonGroup` 或 `ButtonGroup`。

## 用法

每个操作都是一个 `ExpandingButtonGroupItem`。组需要一个 `Label`，它就是该组的无障碍名称。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:ExpandingButtonGroup Label="消息操作">
        <arc:ExpandingButtonGroupItem Label="回复" />
        <arc:ExpandingButtonGroupItem Label="发送" DoneLabel="已发送" />
        <arc:ExpandingButtonGroupItem Label="归档" DoneLabel="已归档" />
    </arc:ExpandingButtonGroup>
</UserControl>
```

### 图标与静止时的操作

在代码中把 `Icon` 设为 16 × 16 的 Lucide 图形。静止时组只显示一个文字标签，默认是第一个可用的操作。`DefaultExpandedItem` 可以指定另一个操作。

```csharp
reply.Icon = ArcIcons.Reply;
archive.Icon = ArcIcons.Archive;

var comment = new ExpandingButtonGroupItem { Label = "评论", Icon = ArcIcons.MessageCircle };
var card = new ExpandingButtonGroup
{
    Label = "卡片操作",
    DefaultExpandedItem = comment
};
```

### 确认

`DoneLabel` 是操作完成后显示的过去式文字；打开内容的操作请不要设置它。确认状态会保留 `ConfirmDuration` 指定的时长，默认为 1.4 秒。

```xml
<arc:ExpandingButtonGroupItem Label="保存" DoneLabel="已保存" />
```

### 异步操作

`AsyncAction` 返回 `Task<bool>`。任务结束前该项处于忙碌状态。返回 `false` 或抛出异常都会跳过确认。没有异步操作时，`Click` 和 `Command` 视为成功。

```csharp
var archive = new ExpandingButtonGroupItem
{
    Label = "归档",
    Icon = ArcIcons.Archive,
    DoneLabel = "已归档",
    AsyncAction = async () =>
    {
        await archiveService.ArchiveAsync();
        return true;
    }
};
```

### 语气与禁用项

`Tone="Danger"` 会以危险色显示展开的破坏性操作。禁用的项仍可获得焦点并显示文字，但不会执行。

```xml
<arc:ExpandingButtonGroup Label="文件操作" Size="Small">
    <arc:ExpandingButtonGroupItem Label="下载" DoneLabel="已下载" />
    <arc:ExpandingButtonGroupItem Label="锁定" IsDisabled="True" />
    <arc:ExpandingButtonGroupItem Label="删除" Tone="Danger" DoneLabel="已删除" />
</arc:ExpandingButtonGroup>
```

### 事件

`ActionInvoked` 先于该项自身的 `AsyncAction` 执行。`e.Item` 是即将执行的操作。

```csharp
message.ActionInvoked += (_, e) => log.Text = $"操作：{e.Item.Label}";
```

## API 参考

<!-- api: ExpandingButtonGroup -->

<!-- api: ExpandingButtonGroupItem -->

## 无障碍

- 组的角色为 ToolBar，名称由 `Label` 提供。即使标签处于收起状态，完整的 `Label` 仍是名称。
- 整个组只占一个 Tab 停靠点。左右方向键在操作之间移动并循环。Home 和 End 分别移到第一个与最后一个操作。
- Enter 与 Space 执行当前聚焦的操作。操作处于忙碌状态时，重复触发会被忽略。
- 禁用的操作仍可获得键盘焦点，并报告为不可用。
- 确认文字会以礼貌方式播报，重复确认也会再次播报。

## 动效

- 聚焦或悬停的操作以弹簧展开，显示其文字，相邻操作随之让位。
- 文字从图标后方滑出，伴随短暂淡入与轻微模糊；收起时更快地收回。
- 确认时图标绘制出对勾，文字切换为完成词：新词升起，旧词移出。
- 指针需要进入相邻操作几个像素后，文字才会移动，因此指针静止时整组不会跳动。
- 减弱动效时，文字与宽度以快速淡入同时改变，对勾直接出现而不绘制。关闭动效时以相同的变化直接完成，没有任何过渡。

## 相关组件

- `FloatingButtonGroup`：独立的柔和按钮，文字始终显示在托盘中。
- `ButtonGroup`：把相关操作拼接在同一表面上，以分隔线区分。
- `ActionButton`：单次异步提交，按钮上显示等待与成功状态。
- `DropdownMenu`：较长的命令列表。
