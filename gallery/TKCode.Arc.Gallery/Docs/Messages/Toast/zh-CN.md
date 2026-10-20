---
title: 轻提示
summary: 已完成后台操作的简短确认。
source-hash: 6adfd1506979
---

## 何时使用

- 后台操作完成后的确认，例如“更改已保存”，用户仍停留在当前页面。
- 由页面通过 `IsOpen` 控制的单条本地消息。
- 不排队、可自行关闭或通过滑动关闭的简短消息。

## 何时不用

- 多条带类型、操作或更新的通知：用 `ToastStack`。
- 需要保留在页面上、直到用户处理的消息：用 `Alert`。
- 必须先回应才能继续的消息：用 `Dialog`。
- 网站顶部轮播的横幅：用 `AnnouncementBar`。

## 用法

`Toast` 显示其 `Title` 与可选的 `Description`。`IsOpen` 为 `True` 时可见，并在 `Duration`（默认 4.5 秒）后关闭。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:Toast Title="更改已保存"
               Description="已同步到你的所有设备。"
               VerticalAlignment="Bottom" />
</UserControl>
```

### 显示与隐藏

轻提示关闭后，将 `IsOpen` 设为 `True` 即可再次显示。轻提示打开期间，标题与描述可以改变；新文字会向上浮现，高度随之变化。

```csharp
toast.Title = "草稿已发布";
toast.Description = "读者几秒内即可看到。";
toast.IsOpen = true;
```

### 持续时间

`Duration` 设置轻提示关闭前的时间。值为零或更小时，轻提示保持打开，直到用户将其关闭。悬停时计时器不会暂停，因此文案应简短。

```xml
<arc:Toast Title="上传完成" Duration="0:0:8" />
<arc:Toast Title="仍在同步" Duration="0" />
```

### 关闭事件

轻提示开始关闭时会引发 `Closed`。`Reason` 说明关闭原因：用户点击了关闭按钮、滑走了轻提示，还是时间到了。

```csharp
toast.Closed += (_, e) =>
{
    if (e.Reason == ToastCloseReason.TimedOut)
    {
        log.Text = "消息已自动关闭。";
    }
};
```

### 应用级轻提示

`Toast.Show` 可从窗口中的任何控件，将轻提示发送到窗口的 `ToastStack`。当消息来自不持有 `Toast` 控件的代码时使用它。`ToastKind` 选择图标与颜色。`Toast.Show` 返回一个 id，可传给 `Toast.Update` 与 `Toast.Dismiss`。

```csharp
Toast.Show(button, "邀请已发送", ToastKind.Success, "Maya 很快会收到一封邮件。");
```

## API 参考

<!-- api: Toast -->

## 无障碍

- `Title` 是轻提示的名称，`Description` 是其辅助文字。轻提示以礼貌方式播报，且不会获得焦点。
- 关闭按钮的名称为 `Dismiss notification`。Tab 可以聚焦到它，按 Enter 或空格键即可关闭轻提示。
- 对勾图标不向辅助技术公开。
- 文字请保持简短。计时器不会暂停，需要更多时间的用户应获得会一直保留的消息，例如 `Alert`。
- 滑动是指针与触控手势。键盘用户可通过关闭按钮获得相同的结果。

## 动效

- 轻提示从下方升起并淡入。片刻之后，其对勾图标会逐笔绘出。
- 关闭时轻提示略微下沉并淡出。已关闭的轻提示不再占用空间。
- 变化的文字向上浮现，高度随弹簧变化。
- 减弱动效时，轻提示只做淡入淡出，并关闭滑动拖动。
- 关闭动效时，轻提示立即显示与隐藏。

## 相关组件

- `ToastStack`：用于应用任何位置发出、可排队、带类型且可更新的通知。
- `Alert`：需要保留在页面上、直到问题解决的消息。
- `AnnouncementBar`：带倒计时的轮播横幅。
