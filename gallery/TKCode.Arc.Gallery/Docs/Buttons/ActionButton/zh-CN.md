---
title: 操作按钮
summary: 用于高频工具栏操作的紧凑按钮。
source-hash: f0f196c4f1de
---

## 何时使用

- 单次异步提交，如保存、发布或发送邀请，且结果应显示在按钮上。
- 文字会切换为等待中和成功两种状态、并自动回到空闲状态的操作。
- 表单或工具栏末尾的行动号召，需要清晰的结果反馈，但不需要弹出提示。

## 何时不用

- 普通的表单提交，只需加载指示、无需结果文字：用 `Button` 并设置 `IsLoading`。
- 需要用户刻意按住才能执行的破坏性操作：用 Gestures 中的 `HoldToConfirm`。
- 同一操作的多个备选项：用 `SplitButton`。
- 行数很多的密集表格：结果应保留在行内，不要为每一行都放置一个按钮。

## 用法

`ActionButton` 派生自 `Button`。其工作通过 `AsyncAction` 执行，该属性在代码中设置。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:ActionButton Label="保存" PendingLabel="保存中" SuccessLabel="已保存" />
</UserControl>
```

```csharp
var save = new ActionButton
{
    Label = "保存",
    PendingLabel = "保存中",
    SuccessLabel = "已保存",
    AsyncAction = () => documentService.SaveAsync()
};
```

### 标签与重置

`Label` 是空闲状态的文字，同时也是无障碍名称。`AsyncAction` 运行期间显示 `PendingLabel`，完成后显示 `SuccessLabel`。`ResetAfter` 设定成功状态保持的时长，默认为 2.4 秒。设为零时，成功状态会一直保留，直到调用 `Reset()`。

```xml
<arc:ActionButton Label="发布帖子"
                  PendingLabel="发布中"
                  SuccessLabel="已发布"
                  ResetAfter="00:00:03" />
```

```csharp
var settings = new ActionButton
{
    Label = "保存设置",
    SuccessLabel = "设置已保存",
    ResetAfter = TimeSpan.Zero
};

reset.Click += (_, _) => settings.Reset();
```

### 失败

`AsyncAction` 抛出异常时，按钮回到空闲状态，并引发 `ActionFailed`。事件参数携带 `Exception`。

```csharp
sync.ActionFailed += (_, e) => message.Text = $"已回到空闲状态：{e.Exception.Message}";
```

### 命令与点击

`Command` 与 `Click` 在 `AsyncAction` 之前运行。若没有异步操作，它们视为立即成功。

### 禁用

将 `IsEnabled` 设为 `False` 即可使按钮变暗。`AsyncAction` 等待期间，按钮保持键盘焦点，并忽略重复激活。

```xml
<arc:ActionButton Label="保存" IsEnabled="False" />
```

## API 参考

<!-- api: ActionButton -->

## 无障碍

- 无障碍名称始终为 `Label`，因此动画中的文字不会被逐字朗读。
- 等待期间，按钮向辅助技术报告自身不可用，但保持键盘焦点。
- 一个隐藏的礼貌状态区域会播报等待与成功文字。
- Enter 与 Space 均可执行操作。等待期间忽略重复激活。

## 动效

- 变化的文字从柔和的模糊中升起，保留的文字滑动到新位置。宽度随弹簧动画过渡。
- 箭头沿操作方向离开，并从左侧回到原位。操作运行时加载指示持续旋转，成功时对勾逐笔绘制。
- 减弱动效时，移除按下缩放、宽度弹簧和绘制的对勾。文字切换变为瞬时交叉淡化。关闭动效时，文字立即替换。

## 相关组件

- `Button`：普通操作；如只需加载指示而无结果文字，设置 `IsLoading`。
- `CopyButton`：复制一个值，并以同类的行内确认方式反馈。
- `ConfirmMorph`：破坏性操作，在原位要求确认。
- `HoldToConfirm`：需要用户刻意按住才能执行的破坏性操作。
