---
title: Undo toast
summary: 用于可以撤回的操作的提示，带有倒计时圆环和撤销按钮；指针停在上面或焦点在其中时，倒计时会暂停。
source-hash: a86c35284a6c
---

<Demo name="Preview" />

## 何时使用

`UndoToast` 让操作立即生效，同时在几秒内保留撤回的机会。它显示刚刚发生了什么（“Conversation archived”）、一个倒数剩余秒数的圆环和一个撤销按钮。指针停在提示上或焦点在其中时，倒计时暂停，之后从停下的地方继续，所以没有人会在伸手去按按钮时错过机会。提示打开时，系统的撤销快捷键也能用。倒计时结束或提示被关闭后，操作保留。

- **归档、移动或标记消息**：项目立即离开列表，撤销后再回来。
- **从看板、播放列表或购物车中移除一项**：做起来快，撤回也快。
- **容易恢复的批量修改**：“12 messages marked as read”。

## 何时不用

- **操作无法恢复**：永久删除或发送给别人；先用 `ConfirmPopover` 或 `Dialog` 询问。
- **只是确认，没有可以撤回的东西**：用 `Toast`。
- **同时有多条通知**：一个撤销提示只显示最近的操作；通知队列用 `ToastStack`。

## 用法

在布局中放置提示的位置（通常是窗口底部）放一个 `UndoToast`，操作后调用 `ShowAsync`。用户撤销时它返回 true。

```xml
<arc:UndoToast x:Name="Toast" HorizontalAlignment="Center" VerticalAlignment="Bottom" />
```

```csharp
inbox.Remove(thread);
if (await Toast.ShowAsync("Conversation archived"))
{
    inbox.Insert(index, thread);
}
else
{
    await api.ArchiveAsync(thread.Id);
}
```

`UndoToast` 位于 Special 包中：在 `UseArcControls()` 旁边用 `UseArcSpecial()` 注册。

提示打开时显示新的操作，会替换之前的操作，之前的操作保留（它的任务返回 false，`Closed` 报告 `Replaced`）；提示留在原位，只有文字和倒计时重新开始。不用 `ShowAsync` 时，调用 `Show` 并处理 `Undone` 或 `Closed`，或者绑定 `UndoCommand`。

### 倒计时

`Duration`（默认 5 秒）是可以撤销的时间，不包括指针停在上面或焦点在其中的时间。圆环按顺时针方向变空，并显示剩余的整秒数。`IsCountdownVisible="False"` 隐藏圆环，让提示更安静；倒计时照常进行。

<Demo name="Countdown" />

### 关闭原因

| `UndoToastCloseReason` | 何时 | 操作 |
|---|---|---|
| `Undone` | 撤销按钮、撤销快捷键或 `Undo()` | 被撤回 |
| `TimedOut` | 倒计时结束 | 保留 |
| `Dismissed` | 关闭按钮、Escape、`Dismiss()` 或 `IsOpen = false` | 保留 |
| `Replaced` | 打开时用 `Show` 显示新的操作 | 保留 |

### 撤销快捷键

提示打开时，系统的撤销快捷键（Ctrl+Z，macOS 上为 Cmd+Z）会撤销操作，除非焦点在需要自己使用这个快捷键的文本框中。如果页面有自己的撤销功能，把 `IsShortcutEnabled` 设为 false。

### 结构

1. **倒计时**（`IsCountdownVisible`）：28px 的圆环，显示剩余秒数。
2. **消息**（`Message`）：发生了什么，最多两行。
3. **撤销**（`UndoText`）：强调色的小号幽灵按钮。
4. **关闭**：圆形按钮，关闭提示并保留操作。

### 文案建议

用过去时说明发生了什么，对象放在前面。

| 不要写 | 改为 |
|---|---|
| Success! Your conversation has been archived | Conversation archived |
| Click here to undo | Undo |
| Items were moved | 3 files moved to Archive |

## API 参考

<!-- api: UndoToast -->

<!-- api: UndoToastClosedEventArgs -->

<!-- api: UndoToastLabels -->

| 类型 | 取值 |
|---|---|
| `UndoToastCloseReason` | `Undone`、`TimedOut`、`Dismissed`、`Replaced` |

## 无障碍

- 提示是以消息命名的礼貌型实时区域，出现时会被读出；它的帮助文本说明撤销快捷键（“Press Ctrl+Z to undo”）。
- 撤销按钮的名称包含消息（“Undo: Conversation archived”）；关闭按钮名为“Dismiss”。
- 焦点在提示中时倒计时暂停，键盘用户有足够的时间。
- 圆环只是装饰；剩余时间不会每秒读出一次。

### 键盘

| 按键 | 作用 |
|---|---|
| `Tab` | 先到撤销，再到关闭 |
| `Enter` / `Space` | 按下有焦点的按钮 |
| `Escape`（焦点在提示中） | 关闭提示，操作保留 |
| `Ctrl` + `Z` / `Cmd` + `Z` | 提示打开时撤销操作 |

## 动效

- 提示从 96% 大小、下方 16px 处以形变弹簧升起，并在 240ms 内淡入；离开时下沉 8px、缩到 97%，在 180ms 内淡出。
- 倒计时进行时，圆环每帧变空；倒计时暂停时，圆环变为一半不透明度。
- 新的操作让提示留在原位：只有文字和圆环重新开始。
- 减少动效时提示只淡入淡出（120ms）；关闭动效时立即出现和离开。倒计时在所有动效等级下都会进行。

## 响应式

- 提示最窄 280px、最宽 416px；可用宽度更小时取可用宽度。
- 消息最多换成两行，之后以省略号结尾。
- 关闭的提示不占空间。

## 相关组件

- `Toast` 和 `ToastStack`：不需要撤销的通知。
- `ConfirmPopover`：无法恢复的操作。

| 组件 | 适用场景 |
|---|---|
| `UndoToast` | 操作立即生效，并能在几秒内撤回 |
| `ConfirmPopover` | 操作无法恢复，需要再按一次 |
| `Toast` | 只需确认某件事已经发生 |
