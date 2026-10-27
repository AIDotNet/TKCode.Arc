---
title: 状态切换按钮
summary: 切换操作的标签，同时保持它的位置与意图。
source-hash: c42723b9cecd
---

<Demo name="Preview" />

## 何时使用

`ActionSwap` 是一个在几个相关状态间轮换的控件：关注与已关注、发送邀请与已发送、加入资料库与已加入。每次按下都会进入下一个状态，到末尾再回绕。胶囊在屏幕上保持原位，宽度、色调、图标和字母一起变化，让人看到的是同一个操作进入了新状态，而不是换了一个按钮。

- **关注某人或某个话题**：“关注”变成“已关注”并安静地填充，再按一次就回去。进行中的文字让请求发出期间的版面保持稳定。
- **发送邀请或消息**：“发送邀请”在邮件服务应答时显示“发送中”，随后显示带绘制勾号的“已发送”，两秒后自行回到“发送邀请”，方便继续邀请下一个人。
- **把内容加入某个集合**（资料库、购物车、关注列表）：服务器处理时，胶囊折叠成一个圆形的加载圈，结果出来后重新展开。
- **可能失败的切换**：动作被拒绝时，在原位显示一个简短的失败词，然后回到切换前的状态，人们可以立刻重试。

## 何时不用

- **以成功勾号收尾的一次保存或发布**：用 `ActionButton`，它有固定的空闲、进行中、成功序列和复位计时器。
- **标签永远不变的操作**：用 `Button`。
- **明暗切换**：用 `ThemeSwitcher`。
- **需要先询问的破坏性操作**：用 `ConfirmMorph`，它在任何操作执行前先在原位提问。
- **每行一个控件的密集表格**：每个字母都是一个动画元素，所以一屏上会切换标签的控件数量要少。

## 用法

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:ActionSwap>
        <arc:ActionSwap.States>
            <arc:ActionSwapState Id="idle" Label="Send invite" Icon="{x:Static arc:ArcIcons.Send}" />
            <arc:ActionSwapState Id="sent" Label="Sent" Icon="{x:Static arc:ArcIcons.Check}" Tone="Success"
                                 PendingLabel="Sending" HoldFor="0:0:2" />
        </arc:ActionSwap.States>
    </arc:ActionSwap>
</UserControl>
```

```csharp
var swap = new ActionSwap { AsyncAction = async (next, current) => await invites.SendAsync() };
swap.States.Add(new ActionSwapState { Id = "idle", Label = "Send invite", Icon = ArcIcons.Send });
swap.States.Add(new ActionSwapState
{
    Id = "sent", Label = "Sent", Icon = ArcIcons.Check, Tone = ActionSwapTone.Success,
    PendingLabel = "Sending", HoldFor = TimeSpan.FromSeconds(2)
});
```

<Demo name="SendInvite" />

`ActionSwap` 派生自 Avalonia 的 `Button`，保留 `Click`、`Command`、`IsEnabled`、Enter 与空格激活、焦点模型和自动化 Peer。按顺序在 `States` 中描述各个状态；`Value` 是当前显示状态的 `Id`（为 null 时显示第一个）。控件至少需要两个状态，少于两个时它不响应并变暗。

### 状态

每个 `ActionSwapState` 是一个面：

- `Id` 与 `Label` 必填。标签同时是该状态显示期间的无障碍名称。
- `Icon` 是标签前 16 px 的 Lucide 图标。使用 `ArcIcons` 或 `ArcIcons.Parse(pathData)`。
- `Tone` 决定胶囊的颜色：`Neutral`（默认，前景色填充）、`Accent`、`Success`、`Warning` 与 `Quiet`。填充色调承载主要操作；着色的 `Success` 与 `Warning` 用图标上的颜色确认结果，`Quiet` 是带边框的表面，适合切换的“已完成”一侧。
- `PendingLabel` 在进入该状态途中 `AsyncAction` 运行时显示，例如“已发送”之前的“发送中”。不设置时，胶囊会折叠成圆形加载圈。
- `HoldFor` 让该状态停留一段时间后自行前进，适合“已发送”这样的短暂确认。计时器只在状态或时长变化时重启，所以数据刷新不会把它重置。

<Demo name="Tones" />

### 动作

`AsyncAction` 在按下时运行，参数是下一个状态与当前状态，运行在控件前进之前。返回一个任务显示进行中阶段，返回 null 则立即前进。

- 任务运行期间控件处于**进行中**：忽略按下，报告忙碌，并保留键盘焦点。即使应答很快，进行中的形态也至少显示 450 ms，让它读起来是一个步骤，而不是闪一下。
- 任务完成时新状态**提交**：`Value` 改变，标签被播报，并触发 `ValueChanged`。
- 任务出错或动作抛出异常时，控件显示 `FailureLabel`（默认 “Try again”）1.8 秒，然后回到离开的状态，不提交任何内容。

```csharp
follow.AsyncAction = async (next, current) =>
{
    await api.SetFollowingAsync(userId, next.Id == "following");
};
follow.ValueChanged += (_, e) => status.Text = e.State.Label;
```

<Demo name="FollowToggle" />

### 没有标签的进行中

没有 `PendingLabel` 的状态，会在其动作运行期间把整只胶囊折叠成一个圆，中间是小加载圈。内边距与宽度沿同一条弹簧收拢，字母随形状闭合而让开。结果到来时，胶囊重新展开，新字母随形状变宽升起。

<Demo name="Spinner" />

### 失败

<Demo name="Failure" />

把原因显示在控件旁边。失败词刻意写得很短（“Try again”）；详细信息应放在胶囊回到原状态之后人们仍能读到的消息里。

### 尺寸

`Size` 为 `Small`（高 36 px）、`Medium`（44 px，默认）或 `Large`（50 px），水平内边距分别为 16、20 和 24 px。胶囊紧贴标签，所以相关状态的标签长度要相近，避免挤动邻居。

<Demo name="Sizes" />

### 禁用

把 `IsEnabled` 设为 `False`，控件会降到 60% 不透明度并退出 Tab 顺序。

<Demo name="Disabled" />

### 内容规范

- **标签**：动词加对象，句首大写，要短：“Send invite”、“Follow”。标签既是显示的文字，也是无障碍名称。
- **进行中标签**：现在分词，不加省略号：“Sending”。加载圈已经表达了进度。
- **结果标签**：过去式，一到两个词：“Sent”、“Added”。
- **数量**：两到三个状态。更多就该用菜单或分段控件。

## API 参考

<!-- api: ActionSwap -->

<!-- api: ActionSwapState -->

<!-- api: ActionSwapValueChangedEventArgs -->

| `ActionSwapTone` 值 | 外观 |
|---|---|
| `Neutral` | 前景色填充，背景色文字（默认）。 |
| `Accent` | 强调色填充，强调前景色文字。 |
| `Success` | 成功色着色的表面，成功色图标。 |
| `Warning` | 警告色着色的表面，警告色图标。 |
| `Quiet` | 带边框的表面，默认文字颜色。 |

| `ActionSwapPhase` 值 | 含义 |
|---|---|
| `Idle` | 显示当前状态。按下会运行动作。 |
| `Working` | `AsyncAction` 正在运行，忽略按下。 |
| `Failed` | 动作失败，`FailureLabel` 显示 1.8 秒。 |

## 无障碍

- 控件是原生按钮：无障碍名称是当前显示面的可见标签，所以动画中的字母不会被逐个朗读。
- 进行中时它报告自己不可用，但保留键盘焦点；隐藏的礼貌状态区会播报新标签、进行中标签和失败。
- 少于两个状态时控件不响应。

### 键盘

| 按键 | 行为 |
|---|---|
| `Enter` / `Space` | 运行动作并进入下一个状态。进行中时忽略。 |
| `Tab` / `Shift+Tab` | 移到下一个或上一个控件。进行中的胶囊保留自己的焦点位置。 |

## 动效

- 只有标签中变化的字母从 4 px 的柔和模糊中升起，保留的字母滑到新位置；离开的字母更快地上移淡出。宽度沿弹簧跟随。
- 图标通过模糊换位：旧图标缩小退去，新图标从模糊中长出。成功图标带着多一点弹性落定，同时笔画从起始边擦入。
- 色调、内边距与宽度沿同一条弹簧运动，任何变化都可以在途中被打断。
- 按下时胶囊缩放到 97%（宽于 220 px 时为 98.5%）并弹回。悬停样式只对鼠标与笔生效；触摸上只有按下缩放作为反馈。

| 变化 | Full | Reduced | Off |
|---|---|---|---|
| 新标签或图标 | 字母从模糊中升起，图标长出，宽度弹簧 | 120 ms 交叉淡化，无宽度与折叠动画 | 立即 |
| 没有标签的进行中 | 胶囊折叠成圆形，中间是加载圈 | 立即折叠，静止的圆环 | 立即 |
| 按下 | 在灵敏的弹簧上缩放到 97% | 无缩放 | 无缩放 |

## 相关组件

| 组件 | 何时选择 |
|---|---|
| `ActionButton` | 一次保存或发布以成功勾号和复位计时器收尾 |
| `Button` | 标签不会随状态变化 |
| `ThemeSwitcher` | 操作是在明暗之间切换 |
| `ConfirmMorph` | 破坏性操作需要先在原位提问 |
