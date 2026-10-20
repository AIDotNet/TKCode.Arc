---
title: 滑动操作
summary: 通过滑动显示行操作，或从菜单中的同一组操作执行。
source-hash: a1c99fc6511f
---

## 何时使用

- 以触屏为主的待办列表，例如邮件、通知或任务。
- 每一侧一两个快捷操作，例如归档或标为未读。
- 可以通过完整滑动移除的行，移除后列表会自动填补空隙。

## 何时不用

- 桌面表格或主要用鼠标操作的列表：用 `ContextMenu` 或 `DropdownMenu`。
- 通过拖动上下调整行顺序：用 `ReorderableList`。
- 列表之外的单个危险操作：用 `HoldToConfirm`。

## 用法

`SwipeActions` 即列表本身。每一项是一个 `SwipeActionsRow`，或是通过 `ItemTemplate` 显示的数据项。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:SwipeActions Label="收件箱" ItemsSource="{Binding Messages}">
        <arc:SwipeActions.LeadingActions>
            <arc:SwipeAction Label="未读" Tone="Accent" KeepsRow="True" />
        </arc:SwipeActions.LeadingActions>
        <arc:SwipeActions.TrailingActions>
            <arc:SwipeAction Label="归档" />
            <arc:SwipeAction Label="删除" Tone="Danger" />
        </arc:SwipeActions.TrailingActions>
        <arc:SwipeActions.ItemTemplate>
            <DataTemplate>
                <TextBlock Text="{Binding Subject}" />
            </DataTemplate>
        </arc:SwipeActions.ItemTemplate>
    </arc:SwipeActions>
</UserControl>
```

前导与尾随操作由每一行共用。某一侧最外层的操作即为完整滑动时执行的操作。

### 执行操作

选中操作时，`Command` 会执行，可以通过点按、完整滑动或“更多操作”菜单触发。`Invoked` 与 `ActionInvoked` 会告诉你是哪一行、哪一项。`KeepsRow="True"` 会执行操作并让行回到原位，而不是将其移除，适合“未读”这类切换操作。

```csharp
inbox.ActionInvoked += (_, e) =>
{
    if (!e.Action.KeepsRow)
    {
        messages.Remove((Message)e.Item!);
    }
};
```

在处理程序中移除数据项即可。行会先滑出，列表随后填补空隙，因此移除时不会跳动。

### 行自带操作

`SwipeActionsRow` 声明自己的操作。`IsFullSwipeEnabled="False"` 会阻止该行在完整滑动时直接确认执行，因此用户必须点按露出的操作。

```xml
<arc:SwipeActions Label="待办" Width="440">
    <arc:SwipeActionsRow Label="审阅拉取请求 #418" IsFullSwipeEnabled="False">
        <arc:SwipeActionsRow.TrailingActions>
            <arc:SwipeAction Label="标记" KeepsRow="True" />
            <arc:SwipeAction Label="删除" Tone="Danger" />
        </arc:SwipeActionsRow.TrailingActions>
        <TextBlock Text="审阅拉取请求 #418" VerticalAlignment="Center" />
    </arc:SwipeActionsRow>
</arc:SwipeActions>
```

### 保持展开

`Open` 无需手势即可显示某一行的操作。同一列表中一次只能展开一行。

```csharp
open.AttachedToVisualTree += (_, _) => open.Open(SwipeActionSide.Trailing);
```

`Close` 收起已展开的行，`SwipeActions.CloseRows()` 则关闭列表中已展开的行。

## API 参考

<!-- api: SwipeActions -->

<!-- api: SwipeAction -->

<!-- api: SwipeActionsRow -->

## 无障碍

- 列表的名称来自 `Label`，每一行都是列表项。每一行都有一个“更多操作”按钮，名称为“更多操作：”加上该行的 `Label`。
- 鼠标与键盘用户无需使用手势：按 Tab 移到“更多操作”按钮，按 Enter 或 Space 打开菜单，再用方向键移动。
- 按 Esc 关闭菜单或收起已展开的行。在其他位置按下时也会收起已展开的行。
- 露出的操作对辅助技术隐藏，且不在 Tab 顺序中。
- 当某一行通过菜单被移除时，焦点移到相邻的行。

## 动效

- 行跟随指针移动。超过最后一个停靠点后会产生阻力，因此行会稍微拉伸，然后回弹。
- 松开后，行以保留松开速度的弹簧停靠到最近的停靠点。快速轻扫会被视为滑向对应方向的停靠点。
- 完整滑动时，最外层的操作会横向拉伸铺满整行，行随后滑出，列表再填补空隙。
- 减弱动效时，行直接跳到停靠点，行的出现与消失改为淡入淡出，不做高度动画。关闭动效时，所有内容一次移动到位。

## 相关组件

- `ContextMenu` 与 `DropdownMenu`：桌面端的行操作。
- `ReorderableList`：通过拖动将行重新排序。
- `HoldToConfirm`：列表之外的单个破坏性操作。
