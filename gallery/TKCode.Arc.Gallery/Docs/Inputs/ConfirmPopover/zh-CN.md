---
title: Confirm popover
summary: 在发起操作的按钮旁弹出一个小问题，用于删除这类操作；焦点先落在取消上，Escape 退出，操作也可以在关闭前先完成。
source-hash: 45442a9aefcf
---

<Demo name="Preview" />

## 何时使用

`ConfirmPopover` 在用户操作的位置提出一个简短的问题：按下垃圾桶按钮后，旁边弹出“删除这些笔记？”，带有取消和操作两个按钮。页面保持可见，操作需要再按一次才会执行，焦点一开始落在取消上，所以误按 Enter 不会造成损失。Escape 或在外面按下都会退出，焦点回到打开它的按钮。如果操作需要一点时间，确认按钮可以先执行工作，并在完成前显示加载动画。

- **删除列表或表格行中的一项**：一条评论、一个文件、一个保存的视图、一个 API 密钥。
- **放弃未保存的内容**：在草稿自己的“放弃”按钮上确认。
- **无法反悔的切换**：撤销访问、结束会话、向读者发布。

## 何时不用

- **操作可以撤销**：直接执行，再提供 `UndoToast`；一次询问会拖慢每个人，只为保护少数误操作的人。
- **决定需要不止一句话**：列出会失去的内容、输入名称来确认，或者除了是和否还有别的选择，这些都应该放进 `Dialog`。
- **一次处理很多项**：批量删除容易判断失误；用写明数量的 `Dialog`。

## 用法

把提问的按钮放进 `Trigger`，在 `Title` 中写下问题，并处理 `Confirmed`。

```xml
<arc:ConfirmPopover Title="Delete this comment?" ConfirmText="Delete">
    <arc:ConfirmPopover.Trigger>
        <arc:Button Variant="Ghost" Content="Delete" />
    </arc:ConfirmPopover.Trigger>
</arc:ConfirmPopover>
```

`ConfirmPopover` 位于 Special 包中：在 `UseArcControls()` 旁边用 `UseArcSpecial()` 注册。

问题在一个 `Popover` 中打开，因此位置设置相同（`Placement`、`Alignment`），放不下时会翻到另一侧，动效也一样。确认后触发 `Confirmed`，以其他方式关闭时触发 `Cancelled`，并带有 `Reason`（取消按钮、Escape 或在外面按下）。`Command` 在 `Confirmed` 之后执行。

### 附加到按钮

`arc:ConfirmPopover.Attach` 为已有的按钮加上一个问题，点击按钮即可打开。

<Demo name="Attached" />

对于不会删除任何东西的操作，把 `IsDestructive` 设为 false：确认按钮和标记使用强调色，而不是危险色。当退出是少见的情况时，`InitialFocus="Confirm"` 让焦点先落在操作按钮上。`Icon` 可以替换警告三角形，设为 null 时隐藏标记。

### 执行工作

确认时会运行 `ConfirmHandler`。在它完成前，确认按钮显示加载动画，取消按钮不可用；返回 true 时作为已确认关闭，返回 false 或抛出异常时保持打开，方便再试一次。

<Demo name="Async" />

```csharp
confirm.ConfirmHandler = async () =>
{
    var response = await api.RevokeKeyAsync(keyId);
    return response.IsSuccessStatusCode;
};
```

### 从代码调用

`ConfirmAsync` 打开问题并在关闭时给出答案，所以命令可以先问再做。

```csharp
if (await confirm.ConfirmAsync(rowButton))
{
    await DeleteAsync(row);
}
```

### 结构

1. **触发按钮**（`Trigger`）：提问的按钮；它留在原位，按下时只改变颜色。
2. **标记**（`Icon`）：32px 的圆形，内含 16px 的图标，使用危险色或强调色。
3. **问题**（`Title`）与**说明**（`Message`）：先是中等字重，再是次要文字；两者都在 272px 内换行。
4. **按钮**：取消（`CancelText`）和操作（`ConfirmText`），小尺寸，右对齐，间距 8px。

### 文案建议

针对具体的对象提问，并在按钮上写出操作名称。

| 不要写 | 改为 |
|---|---|
| Are you sure? | Delete these notes? |
| OK | Delete |
| This action cannot be undone. Are you really sure you want to continue? | They are removed for everyone. |

## API 参考

<!-- api: ConfirmPopover -->

<!-- api: ConfirmPopoverCancelledEventArgs -->

| 类型 | 取值 |
|---|---|
| `ConfirmPopoverFocus` | `Cancel`（默认）、`Confirm` |

## 无障碍

- 问题打开时焦点落在取消上（设置 `InitialFocus="Confirm"` 时落在操作上）；按键打开后显示焦点环，点击打开后不显示。
- Tab 和 Shift+Tab 在问题内的两个按钮之间循环；左右方向键也可以在两者之间移动。
- Escape 关闭问题，焦点回到触发按钮并显示焦点环；在外面按下也会关闭，焦点回到触发按钮但不显示焦点环。
- 弹出面板以问题命名，操作按钮的帮助文本也会重复问题，所以“Delete”会与“Delete these notes?”一起读出。
- 触发按钮会报告问题是否打开（展开或收起）。

### 键盘

| 按键 | 作用 |
|---|---|
| 在触发按钮上按 `Enter` / `Space` | 打开问题 |
| `Tab` / `Shift` + `Tab` | 在取消和操作之间移动 |
| `Left` / `Right` | 在取消和操作之间移动 |
| `Enter` / `Space` | 按下有焦点的按钮 |
| `Escape` | 取消并回到触发按钮 |

## 动效

- 问题的打开和关闭使用 `Popover` 的动效：从朝向触发按钮 5px、97% 大小的位置出发，以弹簧动画落定，大约 160ms 离开。
- `ConfirmHandler` 运行时，确认按钮的文字淡出，换成加载动画。
- 减少动效时问题只淡入淡出；关闭动效时立即出现和离开。

## 响应式

- 问题在弹出面板内宽 272px，较长的文字会换行。
- 靠近边缘时翻到触发按钮的另一侧，并保持在窗口内。

## 相关组件

- `Dialog`：需要更多空间或需要阻止页面的决定。
- `UndoToast`：最好直接执行、事后撤销的操作。
- `Popover`：其他小型交互面板。

| 组件 | 适用场景 |
|---|---|
| `ConfirmPopover` | 一个会造成损失的操作需要再明确按一次 |
| `UndoToast` | 操作在几秒内可以撤销 |
| `Dialog` | 决定需要详细说明、输入或两个以上的选择 |
