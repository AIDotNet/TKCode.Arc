---
title: 确认变形
summary: 一个破坏性按钮，可变形为就地确认、加载指示与带撤销的结果。
source-hash: d3b5eeb1aa3e
---

## 何时使用

- 针对某一对象的破坏性操作，例如删除所选文件或移除成员。
- 后果可以由一个问题说清的操作，例如"删除 3 个文件？"。
- 执行后短时间内可以撤销的操作，并在结果中提供撤销。

## 何时不用

- 后果需要多行说明或输入名称才能确认：使用 `Dialog`。
- 没有确认问题、安全的异步提交：使用 `ActionButton`。
- 绝不能被误触发的操作：使用 Gestures 中的 `HoldToConfirm`。
- 没有单一默认操作的命令列表：使用 Menus 中的 `DropdownMenu`。

## 用法

将 `Label` 设为静止时显示的文字。问题由 `Prompt` 提供；若未设置 `Prompt`，则使用加上问号的 `Label`。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:ConfirmMorph Label="删除项目" Prompt="删除此项目？" />
</UserControl>
```

### 操作与撤销

问题被回答时会执行 `ConfirmAction`。任务运行期间，药丸形状按钮显示等待状态。若任务抛出异常，则显示错误状态并提供重试。设置 `UndoAction` 可在结果中提供撤销。

```csharp
delete.ConfirmAction = () => documentService.DeleteAsync(selected);
delete.UndoAction = () => documentService.RestoreAsync(selected);
```

### 确认模式

`ConfirmMode` 决定如何回答问题。`Press` 按一次即执行。`Hold` 要求按住确认按钮达到 `HoldDuration`，默认为一秒。`Countdown` 会启动 `CountdownSeconds` 倒计时，期间仍可通过取消或 Escape 停止。

```xml
<arc:ConfirmMorph Label="移除成员"
                  ConfirmLabel="移除"
                  ConfirmMode="Hold"
                  HoldDuration="00:00:01" />

<arc:ConfirmMorph Label="放弃草稿"
                  ConfirmLabel="放弃"
                  ConfirmMode="Countdown"
                  CountdownSeconds="3" />
```

### 文字与色调

八个文字属性（`ConfirmLabel`、`CancelLabel`、`PendingLabel`、`DoneLabel`、`ErrorLabel`、`RetryLabel`、`UndoLabel` 与 `UndoingLabel`）分别设置每种状态的用词。`Tone="Neutral"` 会以强调色显示确认回答，适用于可以撤销的重要操作。默认的 `Danger` 适用于破坏性操作。

```xml
<arc:ConfirmMorph Label="撤销访问"
                  Prompt="撤销 3 个密钥？"
                  ConfirmLabel="撤销"
                  PendingLabel="撤销中"
                  DoneLabel="已撤销"
                  Tone="Neutral" />
```

### 受控状态与超时

`State` 为双向绑定。从代码设置它会移动控件，但不会执行操作。`StateChanged` 会在每次状态变化时报告，问题被关闭时会触发 `Cancelled`。`ConfirmTimeout` 会将未回答的问题恢复为静止状态，`ResultTimeout` 会移除结果。将 `IsOutsidePressCancelEnabled` 设为 `False`，可在外部按下时保持问题打开。

```csharp
confirm.StateChanged += (_, e) => log.Text = $"状态：{e.NewState}";
confirm.Cancelled += (_, _) => log.Text = "已取消";
confirm.State = ConfirmMorphState.Idle;
```

## API 参考

<!-- api: ConfirmMorph -->

## 无障碍

- 问题是一个以其提示命名的组。静止的按钮保留其标签的名称。
- 焦点随变形移动：问题打开时在确认按钮上，操作运行时在根元素上，结果出现时在撤销或重试按钮上，静止时回到触发按钮。
- 问题打开后的前 280 毫秒内的按下会被忽略，因此双击或按住 Enter 不会误将其确认。
- 在 `Hold` 模式下，帮助文字为"按住"。提前松开会播报需要更长时间的按住。
- 提示、等待与结果的文字，以及"可以撤销"，都会以礼貌方式播报。
- Enter 与 Space 会按下当前状态下的按钮。Escape 会取消问题或倒计时。

## 动效

- 只有药丸的宽度会变化。它以弹簧展开，收缩则更平顺，文字不会缩放。
- 每个新状态的各个部分按阅读顺序依次清除一层轻微模糊，旧状态则原地淡出。
- 结果的对勾会自行绘制。倒计时数字滚动，等待指示器旋转。
- 在 `Hold` 模式下，按住确认时，危险色的填充会从左侧显露；提前松开则以弹簧回弹。
- 减弱动效时，取消宽度弹簧与模糊，状态之间交叉淡化，并放慢指示器旋转。按住填充与倒计时圆环仍会显示进度。关闭动效时，状态立即切换。

## 相关组件

- `Dialog`：后果需要更多说明或输入名称时使用。
- `ActionButton`：没有确认问题的异步提交。
- `HoldToConfirm`：绝不能在单击时触发的操作。
- `DropdownMenu`：没有单一默认操作的命令列表。
</content>
