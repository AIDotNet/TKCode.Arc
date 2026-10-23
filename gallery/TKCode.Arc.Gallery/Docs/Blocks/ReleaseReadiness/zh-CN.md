---
title: Release readiness
summary: 一个发布闸口：一组简短的检查项解锁唯一的操作，并附带进度条、负责人、通知开关以及原地显示的结果。
source-hash: 93dc8b911186
---

<Demo name="Preview" />

## 何时使用

`ReleaseReadiness` 是发布前的最后一步：一份简短的清单列出必须成立的事项，以及一个在这些事项全部完成之前始终锁定的按钮。每勾选一项检查，进度条就会填充，文字也随之倒计时（"2 to go"、"All clear"）；当所有必需的检查项都完成后，进度条变为绿色，按钮解锁。按下按钮会运行你的代码，并在工作期间显示加载指示，随后在页脚正下方说明结果。

- **一次发布或上线。** 完成签核、测试、发布说明并向客服做好简报，然后点击 "Start rollout"。通知开关决定是否告知某个频道，成功提示也会说明这一点。
- **团队之间的交接。** 运行手册、仪表盘、告警和未关闭的事件必须全部完成；几项锦上添花的检查可以标记为可选，它们会显示出来，但不会阻止交接。
- **任何带检查清单的不可逆操作。** 迁移、价格调整、营销活动发送：任何需要有人先确认几项事实、并在同一位置看到结果的场景。

## 何时不用

- **很长或嵌套的清单。** 本区块设计用于一个列表中的三到八项检查。如果有几十项，或者每项都有自己的负责人和日期，请使用任务列表、`Timeline` 或项目看板。
- **系统能自行验证的检查。** 如果构建服务器已经知道测试是否通过，就显示该状态，而不是让人去勾选；这里的勾选代表的是某个人的说法。
- **表单。** 检查项是带标题和描述的布尔值。当人们需要输入数值时，请使用 `Checkbox`、`Input` 和提交按钮。
- **多个操作。** 闸口只会解锁一个操作。其他操作请放在页面的其他位置。

## 用法

本区块派生自 Avalonia 的 `TemplatedControl`。每个检查项都是一个 `ReleaseCheck`，它是一个 `AvaloniaObject`，包含标题、可选的描述以及双向绑定的 `IsDone`。负责人是 `ProjectPerson`，以生成的头像显示：在柔和的底色上显示姓名首字母，底色由该人员的键决定；除非你提供照片，否则不会使用照片。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:ReleaseReadiness Title="Version 5.3"
                          Subtitle="Lumen mobile · goes live Tuesday"
                          ChannelName="#launches">
        <arc:ReleaseReadiness.Owners>
            <arc:ProjectPerson Name="Rina Kato" />
            <arc:ProjectPerson Name="Samir Aziz" />
        </arc:ReleaseReadiness.Owners>
        <arc:ReleaseCheck Title="Design signed off" Description="Final screens approved by product" IsDone="True" />
        <arc:ReleaseCheck Title="Smoke tests green" Description="Build 5.3.0-beta.2 passed on preview" />
        <arc:ReleaseCheck Title="Release notes written" Description="Customer summary drafted and reviewed" />
        <arc:ReleaseCheck Title="Support briefed" Description="Help desk knows what is changing" />
    </arc:ReleaseReadiness>
</UserControl>
```

### 结构

- **页头**：`Title`、`Subtitle`，以及右侧的负责人头像（没有负责人时隐藏）。
- **进度**：左侧是剩余事项，右侧是数量（"1 of 4"），使用等宽数字；其下方是 4 px 高的进度条。
- **检查项**：每项一行。整个标签都属于复选框，因此点击标题或描述即可勾选；指针悬停在该行任意位置时，该行会着色。
- **页脚**：开始位置是通知开关，末尾是按钮。当两者无法放在同一行时，按钮会移到第二行，但仍位于末尾。
- **状态行**：在页脚下方展开，显示成功或错误文字。

### 检查项与闸口

`TotalCount` 统计必需的检查项数量，`DoneCount` 统计其中已完成的数量；当至少有一项检查，并且所有必需项都已完成时，`IsReady` 为 true。设置为 `IsRequired="False"` 的检查项会显示并可以勾选，但不计入数量、进度条和闸口。对于你自己管理的集合，请使用 `ChecksSource`（会监听 `ObservableCollection`），或直接填充 `Checks`。勾选复选框会把值写回 `IsDone`，因此对它使用双向绑定可以让你的模型保持同步。

`CheckChanged` 会报告每次勾选，包括旧值、新值以及此后的数量；`ReadyChanged` 会在闸口打开或再次关闭时触发。

### 开始发布

将 `ReleaseAction` 设置为按钮要启动的工作。它会接收一个 `ReleaseRequest`（是否通知、所有检查项、被跳过的可选检查项以及尝试次数）和一个取消令牌。任务运行期间 `State` 为 `Releasing`：按钮显示加载指示并保持键盘焦点，检查项和开关全部锁定。任务结束后，按钮会显示 `ReleasedText` 并带一个对勾，状态行会说明发布已在进行中（如果开关打开，还会说明频道已被告知），并出现一个重置按钮。

```csharp
gate.ReleaseAction = async (request, cancellation) =>
{
    await deployments.StartAsync("lumen-mobile", "5.3.0", notify: request.Notify, cancellation);
};
gate.Released += (_, e) => log.Info($"Rollout started on attempt {e.Request.Attempt}");
```

`ReleaseCommand` 会先运行，参数为该请求；如果它无法执行，发布就不会开始。`ReleaseStarting` 会在两者之前触发，并且可以取消。`ReleaseAsync()` 执行与按钮相同的操作；`CancelRelease()` 会取消令牌并立即让闸口恢复（任务迟到的结果会被忽略）；`Reset()` 会把每个检查项恢复：若设置了 `IsInitiallyDone`，则恢复为该值，否则恢复为该检查项在区块首次显示时的值。重置后，通知开关保持其当前值不变。

### 失败与重试

来自操作（或命令）的异常会把 `State` 设为 `Failed`。错误会以危险色显示在状态行中，检查项和开关解锁，按钮可以再次尝试，焦点仍留在按钮上。该行显示异常消息；消息为空时，则显示你设置的 `ErrorFormatter` 所返回的文字，否则显示 `FailureText`。失败后修改任何检查项都会清除错误。取消（`CancelRelease`，或任务运行时区块离开窗口）不算失败：闸口只是回到就绪状态。

<Demo name="Failing" />

### 带框或扁平

`IsFramed`（默认值）会绘制一张宽度最多为 560 的卡片，带 1 px 边框、16 的圆角半径、表面颜色，并在检查项和页脚上方绘制分隔线。将其设为 false，可让区块以容器的宽度平铺在页面上，适用于整页或侧边面板；各行仍保留浅色分隔线。

<Demo name="Flat" />

### 窄容器

本区块读取的是它自身的宽度，而不是窗口的宽度。宽度为 388 或更小时，所有左右内边距从 20 收紧为 16。页头保持在一行，副标题以省略号缩短；当按钮无法与开关并排放下时，页脚会把按钮移到开关下方。

### 其他文字与可选检查项

每段文字都是带英文默认值的属性：`ReleaseText`、`ReleasedText`、`ResetText`；进度文字 `RemainingFormat`（"{0} to go"）、`RemainingOneText`、`ReadyText`、`ReleasingText` 和 `CountFormat`（"{0} of {1}"）；通知开关的 `NotifyText` 或 `NotifyFormat`（"Notify {0}"），并配合 `ChannelName`；成功文字 `SuccessFormat` 和 `SuccessNotifiedFormat`（使用 `ReleaseName`，或标题，以及频道）；屏幕阅读器名称 `ChecksLabel`、`ProgressLabel`、`OwnersLabel` 或 `OwnersLabelPrefix`（配合 `AndWord`）。`IsNotifyVisible` 隐藏开关，`IsResetVisible` 隐藏重置按钮。

<Demo name="Handoff" />

## API 参考

<!-- api: ReleaseReadiness -->

<!-- api: ReleaseCheck -->

<!-- api: ReleaseRequest -->

<!-- api: ReleaseReadinessState -->

## 无障碍

- **Tab** 在检查项、通知开关和按钮之间移动。按钮在闸口就绪之前不是 Tab 停靠点；发布之后，只有重置按钮是。
- **空格键**勾选或清除获得键盘焦点的检查项。**Enter** 不会，因此检查项不会被误勾选。
- **空格键**或 **Enter** 会切换通知开关并按下按钮。
- 发布后，焦点移到重置按钮（若重置按钮隐藏，则移到状态行）。重置后，焦点移到第一个未完成的检查项。失败后，焦点留在按钮上以便重试。只有当焦点原本在区块内时才会移动，因此从代码启动的发布不会抢走焦点。
- 区块以其标题命名，标题为二级标题。
- 负责人是一个名为 "Owners: Rina Kato and Samir Aziz" 的组（由 `OwnersLabel` 或 `OwnersLabelPrefix` 指定）；头像不会逐个朗读。
- 进度条是只读的进度条，名称由 `ProgressLabel` 指定，当前值为已完成的必需检查项数量，最大值为全部必需项数量。检查项列表的名称由 `ChecksLabel` 指定。
- 每个复选框以其检查项的标题命名，并由其描述说明；必需的检查项会注明这一点。
- 剩余事项（"2 to go"、"All clear"、"Rolling out"）随变化以礼貌方式播报；旁边的数量则不播报。成功提示以礼貌方式播报，错误以强烈方式播报；重试时重复出现的错误会再次播报。
- 操作运行期间，按钮保持其名称，并以发布中的文字作为其状态。

## 动效

- **勾选检查项**：进度条的填充部分沿临界阻尼弹簧滑动（无超调，约 580 ms）；当所有必需项都完成时，进度条在 240 ms 内变为绿色。复选框的对勾在它自己的弹簧上绘制；标题在 160 ms 内淡化为次要颜色。文字原地变化，不带动画。
- **悬停**：该行在 160 ms 内着色。
- **发布中**：按钮显示加载指示，随后标签配合短暂的模糊进行交叉淡化，同时按钮宽度以弹簧过渡到新文字的宽度。状态行以平滑弹簧展开，并配合单独的 160 ms 淡入；重置时以同样的方式收起。
- 减少动效会让进度条、颜色和状态行立即就位，只保留按钮的短促交叉淡化。关闭动效则会立即应用每一次变化。

## 相关组件

- `Checkbox`、`Switch` 和 `Button`，即组成本区块的控件。
- `Progress`，带标签的独立进度条。
- `ActionButton`，适用于只有单个操作、带有进行中和成功状态、但没有检查项的场景。
- `Project board` 和 `Kanban board`，另外两个项目区块。
