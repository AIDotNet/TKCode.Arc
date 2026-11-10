---
title: Inbox triage
summary: 一个为快速处理而做的收件箱：滑动或按一个键即可归档、稍后提醒、分配或加标签，底部的条可以撤销任何一步，剩余数量会一路滚到清空。
source-hash: a186fdaed653
---

<Demo name="Preview" />

## 何时使用

`InboxTriage` 是为清空而做的收件箱：工单、审核请求、消息或提醒，每一项只需要一个快速决定。每一行都可以通过滑动或一个按键来归档、稍后提醒、分配给同事或加标签，每一步都能在底部的条上撤销。滚动的计数和进度条显示还剩多少，清空时会明确告诉你。

- **客服队列**：一次处理一个工单。
- **审核或审批请求**：每项需要同意、稍后或交给别人。
- **手机上的通知整理**：滑动操作很自然。

## 何时不用

- **阅读长消息**：请把列表和阅读区配合使用；整理行只显示主题和一行预览。
- **一次处理数百项**：行没有虚拟化，请给收件箱分页。
- **对多项选择批量操作**：使用带选择和工具栏的 `Table`。

## 用法

```xml
<arc:InboxTriage x:Name="Inbox" Title="Support inbox">
    <arc:InboxTriageItem Id="t1" Sender="Ines Duarte" Subject="Refund for order 4471" />
</arc:InboxTriage>
```

```csharp
Inbox.Triaged += (_, e) => api.Apply(e.Item.Id, e.Action, e.Value);   // Value is the teammate or the label
Inbox.Undone += (_, e) => api.Revert(e.Item.Id, e.Action);
```

处理过的项目会离开 `Items`，撤销时回到原来的位置，并恢复原有的标签和负责人。`Teammates` 和 `AvailableLabels` 提供“分配”和“标签”列表。首次加载时收件箱为空，会得到一组示例项目、同事和标签；设置 `UsesSampleData="False"` 可从空开始。

### 操作

| 操作 | 滑动 | 按键 | 结果 |
|---|---|---|---|
| 归档 | 向右 | `E` | 离开收件箱。 |
| 稍后提醒 | 向左 | `S` | 离开收件箱，并设置 `SnoozedUntil`（`SnoozeFor`，或明天 9:00）。 |
| 分配 | 向左 | `A` | 打开同事列表；项目离开并设置 `Assignee`。 |
| 标签 | 向左 | `L` | 打开标签列表；添加或移除标签，项目保留。 |
| 撤销 | | `Z` / `Ctrl+Z` | 撤回上一步。 |

同样的操作也在每一行的“更多操作”菜单里，并且可以用代码调用：`Archive`、`Snooze`、`Assign`、`ToggleLabel`、`Undo`、`OpenAssign`、`OpenLabels` 和 `MoveTo`。

### 窄栏且不显示提示

<Demo name="Narrow" />

`ShowsShortcuts="False"` 隐藏按键提示。`UndoDuration` 设置撤销条停留多久（为零时一直保留）。

## API 参考

<!-- api: InboxTriage -->

<!-- api: InboxTriageItem -->

<!-- api: InboxTriageEventArgs -->

## 无障碍

- 列表以标题命名；每一行以主题命名，并有包含全部操作的“更多操作”菜单，所以不需要手势。
- 当前项目用一条强调色竖条标出，而不只是改变文字颜色。每一步都会读出剩余数量（“已归档：订单 4471 退款。还剩 5 项。”）。

### 键盘

| 按键 | 作用 |
|---|---|
| `J` / `↓`、`K` / `↑` | 下一项或上一项。 |
| `Home` / `End` | 第一项或最后一项。 |
| `E`、`S`、`A`、`L` | 对当前项目归档、稍后提醒、分配、加标签。 |
| `U` | 标为已读或未读。 |
| `Z` 或 `Ctrl+Z` | 撤销。 |
| 列表中的 `↑` / `↓`、`Enter`、`Escape` | 选择同事或标签，或关闭列表。 |

## 动效

- 处理过的行朝操作的方向滑走，列表随后合拢；撤销的项目在原位回来。
- 计数逐位向下滚动，进度条增长，撤销条从底部升起。
- 减少动效时，行改为淡出而不是滑走，计数不再滚动；关闭动效时变化立即发生。

## 响应式

- 区块最宽 560 px，列表超过 520 px（`ListMaxHeight`）后滚动。
- 行内的主题和预览各截断为一行；在窄宽度下，标签会换到发件人下方。
- 滑动支持触摸、笔和鼠标拖动，所以在手机上用法相同。

## 相关组件

- `SwipeActions`：单独的可滑动列表。
- `NotificationCenter`：只读的更新列表。
- `Toast`：一条可撤销的消息。
