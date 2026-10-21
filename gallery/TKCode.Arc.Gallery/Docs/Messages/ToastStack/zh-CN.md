---
title: 轻提示堆叠
summary: 将简短结果堆叠在边缘，直到你需要查看时再展开。
source-hash: 5220d94576ed
---

<Demo name="Stack" />

## 何时使用

- 应用中任何位置异步工作的结果，例如上传失败或邀请已发送。
- 需要类型（成功、信息、警告、错误）或操作（如撤销或查看）的通知。
- 加载消息在工作完成后变为结果。

## 何时不用

- 页面控制的一条本地确认：用 `Toast`。
- 需要保留在页面上、直到用户处理的消息：用 `Alert`。
- 网站顶部轮播消息的横幅：用 `AnnouncementBar`。
- 必须先回应才能继续的消息：用 `Dialog`。

## 用法

将 `ToastStack` 放在内容之上，例如作为窗口根面板的最后一个子元素。在窗口中的任何控件里调用 `Toast.Show` 即可添加轻提示。如果窗口没有 `ToastStack`，`ToastHost` 会在浮层上创建一个。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <Grid>
        <!-- 页面内容放在这里。 -->
        <arc:ToastStack Label="通知" />
    </Grid>
</UserControl>
```

### 显示轻提示

`Show` 接收一个 `ToastOptions` 并返回轻提示的 id。`Kind` 选择图标与颜色。`Toast.Show` 以参数形式接收相同的值，并将其发送到拥有 `button` 的窗口的堆叠。

```csharp
stack.Show(new ToastOptions
{
    Kind = ToastKind.Warning,
    Title = "存储空间即将占满",
    Description = "已使用 10 GB 中的 92%。"
});

Toast.Show(button, "邀请已发送", ToastKind.Success, "Maya 很快会收到一封邮件。");
```

### 加载与更新

在 `ToastOptions` 中传入 id，再用相同的 id 调用 `Update`，即可原地修改轻提示。将 `Duration` 设为 `Timeout.InfiniteTimeSpan`，轻提示会一直保留，直到被更新或关闭。轻提示的图标、文字与高度会变形为新状态。

```csharp
var id = stack.Show(new ToastOptions
{
    Kind = ToastKind.Loading,
    Title = "正在上传 report.pdf",
    Duration = Timeout.InfiniteTimeSpan
});

await UploadAsync();

stack.Update(id, new ToastUpdate
{
    Kind = ToastKind.Success,
    Title = "报告已上传",
    Description = "已与 4 人共享。"
});
```

### 操作

`ToastAction` 会为轻提示添加一个按钮。操作执行后，除非回调更新了轻提示，否则轻提示会关闭。撤销操作可以将轻提示更新为结果，并通过 `RemoveAction` 移除按钮。

```csharp
stack.Show(new ToastOptions
{
    Title = "项目已归档",
    Action = new ToastAction("撤销", id => stack.Update(id, new ToastUpdate
    {
        Kind = ToastKind.Success,
        Title = "项目已恢复",
        RemoveAction = true
    }))
});
```

### 位置与数量限制

`Position` 将堆叠移到左下、居中或右侧边缘。`VisibleToasts` 设置同时显示的轻提示数量，`Limit` 则在超过数量时丢弃最早的轻提示。

<Demo name="Position" />

```xml
<arc:ToastStack Position="BottomCenter" VisibleToasts="3" Limit="12" />
```

## API 参考

<!-- api: ToastStack -->

<!-- api: ToastOptions -->

## 无障碍

- 通知区域是一个由 `Label` 命名的区域地标。键盘快捷键 Alt+T 会将焦点移入堆叠并将其展开。
- Tab 在各轻提示的操作按钮与关闭按钮之间移动。Escape 关闭当前聚焦的轻提示。
- 新增和更新的轻提示以 `类型：标题。描述` 的形式播报。错误以强提示方式播报，其他类型以礼貌方式播报。
- 每个轻提示都有名称，例如 `Error: Couldn't send invite`，屏幕阅读器可借此区分类型。
- 堆叠被悬停或聚焦、轻提示被拖动以及窗口最小化时，计时器会暂停。

## 动效

- 新轻提示从下方升起并淡入。较早的轻提示收在最前面的轻提示后方，并略微缩小。
- 悬停或聚焦堆叠时，轻提示展开为列表；离开后再折叠回去。
- 向右滑动轻提示时，它会跟随指针移动。快速或较长的滑动会以淡出方式关闭它。
- 更新时，图标、文字与高度会原地变形。工作进行时，加载圆环持续转动。
- 减弱动效时，位置直接跳转，轻提示只做淡入淡出。关闭动效时，每次变化立即显示。

## 相关组件

- `Toast`：由页面通过 `IsOpen` 控制的一条本地确认。
- `Alert`：页面内持续存在的消息。
- `AnnouncementBar`：带倒计时的轮播横幅。
