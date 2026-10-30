---
title: Meeting summary
summary: 一次通话的 AI 纪要：平台、参会者和时长，随后是流式出现的摘要、决定和待办事项；每一行都链接到它出自的转录原话，每项任务都可以勾选完成或发送到任务跟踪工具。
source-hash: 3e51431ba295
---

<Demo name="Preview" />

## 何时使用

`MeetingSummary` 是 AI 记录员在通话结束后留下的卡片。头部写明平台、日期、时长和参会的人。一段简短的总结过程之后，三个分区以每次几个词的节奏流式出现：摘要（Summary）、决定（Decisions）和待办事项（Action items）。每一行都带有时间戳，悬停在这一行上（或聚焦它的时间戳）会显示它出自的转录原话，读者可以把要点和实际说过的话逐条核对。待办事项可以勾选完成，也可以发送到问题跟踪工具或文档应用。

- **通话结束后的纪要**：用于 CRM、招聘工具、会议录制工具或团队工作区。
- **把转录变成决定和有负责人的任务**，再由大家推送到自己的跟踪工具。
- **任何需要可核对的摘要**：每一行都能对照它的出处。

## 何时不用

- **通话进行中的实时字幕或滚动转录**：直接显示陆续到达的转录本身。
- **背后没有会议的普通任务列表**：在你自己的列表里使用一行行 `Checkbox`。
- **包含很多章节的长文档**：把它渲染成文档页面，而不是卡片。

## 用法

把纪要放进 `Meeting`，并自己驱动 `Status`：请求进行中设为 `Summarizing`，请求完成时设为 `Ready`（纪要随后流式出现），失败时设为 `Error` 并附上 `ErrorText`。不设置 `Status` 时，卡片在滚动进入视图后会自己播放一段简短的总结过程。既没有 `Meeting`、`Summarizer` 也没有 `Simulation` 时，卡片直接显示"无转写记录"状态（"No transcript to summarize"），页眉不显示详情。

```csharp
card.Meeting = new MeetingSummaryData
{
    Title = "Onboarding flow review",
    Platform = "Video call",
    StartsAt = new DateTime(2026, 10, 27, 10, 30, 0),
    DurationMinutes = 48,
    Attendees = [new("ines", "Ines Duarte", "Design"), new("kofi", "Kofi Mensah")],
    Summary = [new("s1", "Most new teams stall on the invite step.",
        new MeetingSummaryQuote("ines", TimeSpan.FromSeconds(252), "Seven of the ten teams got stuck on invites."))],
    ActionItems = [new MeetingActionItem("a1", "Move the invite step after project creation", "kofi",
        new DateOnly(2026, 11, 3))],
};
card.Status = MeetingSummaryStatus.Summarizing;
// ... when your request resolves:
card.Status = MeetingSummaryStatus.Ready;
```

`Quote.At` 是从录音开始算起的时间，卡片把它显示为 `4:12`。`StartsAt` 按原样打印，因此请在传入之前完成时区换算。

### 模拟

想在没有模型的情况下展示卡片（演示、设计评审、新手引导），就给它一个 `MeetingSummarySimulation`：纪要、要署名的模型，以及模拟发送的编号方式。卡片滚动进入视图后，会播放总结过程、流式显示纪要，并且只要没有人先操作卡片，就会勾选第二个待办事项并发送第一个；发送是模拟的，并会说明什么都没有发送。你自己的 `Meeting` 或 `Summarizer` 优先于模拟；把 `Simulation` 设为 null 会停止模拟并显示"无转写记录"状态。

```csharp
card.Simulation = new MeetingSummarySimulation
{
    Meeting = notes,
    Model = new AgentModel("Qwen3.8-Max", ArcIcons.Sparkles),
    ReferenceFormats = new Dictionary<string, string> { ["issues"] = "ISS-{0}", ["docs"] = "Notes {0}" },
    FirstReferenceNumber = 142,
};
```

上方的预览播放的就是一个模拟，其会议来自图库自己的演示数据。

### 你自己的会议

<Demo name="YourMeeting" />

这张卡片的数据、状态和发送都由应用掌管。页脚的重新生成和错误状态中的重试都会触发 `RegenerateRequested`；自己驱动状态的卡片还会重新播放它的总结过程。

### 从你的模型流式获取

<Demo name="Summarizer" />

`Summarizer` 是一个异步流式委托。卡片自己驱动状态时，会在首次进入视图和重新生成时调用它：在第一个快照到达之前显示总结过程，之后流式显示你产出的每一个快照（每一个都是截至目前的纪要）。仍然吻合的文字会继续流式出现，抛出异常则会显示带有异常消息的错误状态。请把 `Meeting` 设为这次通话的头部信息，这样在总结期间也能显示标题和参会者。

```csharp
card.Meeting = header; // title, date and attendees, no notes yet
card.Summarizer = SummarizeAsync;

async IAsyncEnumerable<MeetingSummaryData> SummarizeAsync([EnumeratorCancellation] CancellationToken token)
{
    await foreach (var notes in myModel.StreamNotesAsync(transcript, token))
    {
        yield return notes;
    }
}
```

### 发送待办事项

每个待办事项为 `SendTargets` 中的每一项提供一个发送按钮：默认是通用的 `Issues` 和 `Docs`（可以传入带有你产品标志的 `MeetingSummarySendTarget`，或传入空列表以隐藏按钮）。按下按钮会触发 `SendRequested`；把 `Completion` 设为你的请求，并把令牌传下去。按钮先显示加载圈，然后显示一个标签，内含你返回的编号（"ENG-31"）和一个对勾；任务出错时则显示警示图标和 Retry。没有处理程序时，发送是模拟的，实时区域会说明什么都没有发送；其标签显示"Sent"，或模拟的 `ReferenceFormats` 给出的编号。

```csharp
card.SendRequested += (_, e) => e.Completion = CreateIssueAsync(e.Item, e.Target, e.CancellationToken);

async Task<MeetingSummarySendResult?> CreateIssueAsync(MeetingActionItem item, MeetingSummarySendTarget target,
    CancellationToken token)
{
    var issue = await tracker.CreateAsync(item.Text, token);
    return new MeetingSummarySendResult(issue.Key, issue.Url);
}
```

发送成功后会触发 `ActionItemSent`。卡片被移除或重新生成时，令牌会被取消，按钮回到它的标志。带有 `Url` 的已发送标签点击后会打开该链接。

### 勾选待办事项

勾选一个事项会触发 `ActionItemChanged`，参数中带有该事项及其新状态；勾选状态由卡片自己保存。`IsDone` 为 true 的事项一开始就是勾选状态。

### 转录原话

在任意一行上稍作悬停，它的原话就会在下方打开（窗口空间不足时翻到上方）。原话打开期间，移到另一行会立即切换。用键盘聚焦时间戳会显示它的原话；Enter、Space 或轻点会把它固定住，Escape 或在别处按下则会关闭。

### 状态

| Status | 主体 |
|---|---|
| `Summarizing` | 在占位行上方闪烁的 "Summarizing transcript" |
| `Ready` | 摘要、决定和待办事项（没有内容的分区不显示） |
| `Ready`，列表为空 | "No transcript to summarize" |
| `Error` | "Couldn't summarize this meeting"、`ErrorText` 和 Try again |

### 窄卡片

<Demo name="Narrow" />

卡片会填满容器，最宽 640px，最窄可到约 360px。宽度低于 460px 时，参会者头像组移到日期下方；低于 520px 时，负责人只显示名字，让每个事项下的信息保持在一行（屏幕阅读器仍会读出全名）。`AutoPlay="False"` 让卡片一开始就显示完成的纪要。

### 你自己的文字

`Labels` 包含卡片显示或朗读的每一个文字。替换这个对象即可本地化或改写：

```csharp
card.Labels = new MeetingSummaryLabels
{
    Summary = "Résumé",
    ActionItems = "Actions",
    Due = due => "Pour le " + due.ToString("d MMM"),
};
```

`Model` 设置页脚中署名的模型（一个带名称和标志的 `AgentModel`）；设为 null 则隐藏署名。`MeetingSummaryData.PlatformMark` 用你的平台标志替换视频图标。

### 减弱动效

减弱动效时，纪要整段出现而不是流式出现，主体和发送按钮位置的尺寸立即改变，复选框、删除线和原话直接显示最终状态，不做描绘或滑动。所有播报照常进行。总结过程会等到卡片出现在屏幕上才开始。

### 内容准则

- 一条摘要是一句话，说明发生了什么；一条决定说明决定了什么，而不是谁争论了什么。
- 待办事项以动词开头，并只写一位负责人。
- 每段原话只保留支撑这一行的一两句话。

### 常见问题

**能显示已经完成的纪要吗？** 把 `Status` 设为 `Ready`（或把 `AutoPlay` 设为 false），纪要就会整段显示。

**勾选状态由谁保存？** 由卡片保存。`ActionItemChanged` 会告诉你每一次变化；`IsDone` 提供初始状态。

## API 参考

<!-- api: MeetingSummary -->

<!-- api: MeetingSummaryData -->

<!-- api: MeetingActionItem -->

<!-- api: MeetingSummarySendTarget -->

<!-- api: MeetingSummaryLabels -->

<!-- api: MeetingSummarySimulation -->

## 无障碍

- 卡片是一个以会议命名的分组，标题是二级标题，每个分区是三级标题。
- 时间戳是按钮，原话打开期间报告为已展开；原话的文字是它的帮助文本。
- 每个复选框都是具有复选框角色和勾选状态的按钮，每个复选框和发送按钮都以待办事项的文字命名，因此即使连成一串也能读得清楚。
- 16px 的复选框有 32px 的点击区域，键盘焦点环紧贴方框。
- 一个礼貌型实时区域会播报总结开始、总结完成、勾选的事项和发送结果；没有处理程序时还会说明 "Simulated, nothing was sent"。
- 状态从不只依赖颜色：发送失败显示警示图标和 Retry，已发送的事项显示目标的标志、编号和对勾。
- 平台名称先于日期读出；只显示名字的负责人对屏幕阅读器仍保留全名。

### 键盘

| 按键 | 操作 |
|---|---|
| Tab | 依次经过参会者、每个时间戳、复选框和发送按钮，然后是复制和重新生成 |
| 聚焦时间戳 | 显示这一行的转录原话 |
| Enter、Space | 在时间戳上固定或关闭原话；在复选框上切换勾选；在发送按钮上发送该事项 |
| Escape | 关闭打开的原话 |

## 动效

| 变化 | 完整 | 减弱 | 关闭 |
|---|---|---|---|
| 总结中 | 约一秒，标签闪烁 | 相同，不闪烁 | 相同，不闪烁 |
| 纪要到达 | 每 34ms 出现 3 到 8 个词，每块淡入（总共约 1.5 秒） | 立即整段出现 | 立即整段出现 |
| 主体在状态之间切换 | 高度弹簧过渡（0.24 秒，无回弹），新状态淡入 | 立即 | 立即 |
| 负责人、日期、按钮 | 文字流式出现时保留位置，文字完整后淡入 | 立即 | 立即 |
| 勾选事项 | 填充放大出现（160ms），对勾描绘出来（200ms，ease-out），1px 删除线横扫每一个换行，文字过渡为弱化色 | 立即到最终状态 | 立即到最终状态 |
| 取消勾选 | 反向进行，更快（120ms） | 立即 | 立即 |
| 按下复选框 | 方框缩放到 0.9，整行不缩放 | 相同 | 相同 |
| 发送 | 标志、加载圈和已发送标签交叉淡化，按钮位置弹簧过渡到新宽度（0.25 秒，无回弹） | 短暂交叉淡化，尺寸立即改变 | 立即 |
| 原话 | 短暂悬停后用 160ms 淡入并上升 3px；在行之间立即切换 | 淡入 | 立即 |

## 相关组件

- `ToolCall`：智能体发起的工具调用。
- `AgentStatus`：正在运行的智能体的实时状态。
- `Checkbox`：普通的勾选清单。
- `Avatar` 和 `AvatarGroup`：人员。
- `CopyButton`：复制一个值并给出确认。
