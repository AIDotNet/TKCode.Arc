---
title: Call insights
summary: 拆开来看的一通销售通话录音：带情绪曲线、每位发言人一条轨道、关键时刻和话题的时间轴，可播放、拖动或用键盘逐步移动的播放头；跟随播放头的文字记录；发言时长占比和后续步骤。
source-hash: 43832af61191
---

<Demo name="Preview" />

## 何时使用

`CallInsights` 是销售团队在通话录音之后打开的页面。它一眼回答四个问题：通话的气氛如何随时间变化、谁在说话、谈了什么、约定了什么。时间轴把情绪曲线（中线以上为绿色，以下为红色）、每位发言人一条轨道和关键时刻放在同一条时间轴上，下方有播放头；旁边的文字记录跟随播放头，所以时间轴上的某个时刻和背后的原话始终只差一次点击。

- **回看一次需求或演示通话。** 看看气氛在哪里下降，跳到反对意见处，读一读当时说了什么。
- **辅导。** 发言时长占比、最长的独白和销售提出的问题，能看出通话是否均衡。
- **交接一单生意。** 话题和后续步骤告诉下一个人已经谈过什么、还欠什么。

## 何时不用

- **实时通话。** 本区块回看录音，不会实时传输语音，也不做转写。
- **播放音频。** 本区块没有自己的音频。有播放器时，把 `Position` 绑定到播放器上。
- **没有发言人的长篇会议记录。** 没有可对齐的文字记录时，请使用 `Markdown` 或文档视图。

## 用法

本区块派生自 `TemplatedControl`。没有数据时，它显示一通虚构的 24 分钟需求通话。通过五个集合提供你自己的通话：`Speakers`（键、名字、角色以及是否属于你这一方）、`Utterances`（发言人键、开始、结束、文字以及 −1 到 1 的情绪值）、`Moments`、`Topics` 和 `NextSteps`。

```xml
<arc:CallInsights Title="Renewal check-in" Subtitle="Thu, 6 min" IsFramed="False" />
```

```csharp
call.Speakers.Add(new CallInsightsSpeaker("jonas", "Jonas Weber", "Customer success", isHost: true));
call.Utterances.Add(new CallInsightsUtterance("jonas", TimeSpan.Zero, TimeSpan.FromSeconds(40), "How has the queue been?", 0.3));
call.NextStepToggled += (_, e) => tasks.SetDone(e.Step.Text, e.IsDone);
```

### 时间轴与播放头

`Position`（双向）就是播放头。在时间轴上按下或拖动会移动它，按在某个时刻标记上会对齐到该时刻，点击文字记录中的一行或侧栏中的一个时刻会跳到它的开始处。这些操作都会引发 `Seeked`。播放按钮按 `PlaybackRate` 实时移动播放头（速度按钮在 1×、1.5× 和 2× 之间切换），到结尾时停止；`Play`、`Pause` 和 `Seek` 在代码中做同样的事。

### 文字记录、话题与后续步骤

`SearchText` 只保留包含它的行，并在轨道下方标出。选择一个话题（`SelectedTopic`）会在时间轴上为它的各段着色。勾选一个后续步骤会写入 `IsDone` 并引发 `NextStepToggled`。发言时长占比、最长独白和提问次数都由发言内容算出（`TalkShares`）。本区块显示的每个文字都在 `Labels`（`CallInsightsLabels`）中。

<Demo name="OwnCall" />

## API 参考

<!-- api: CallInsights -->

<!-- api: CallInsightsSpeaker -->

<!-- api: CallInsightsUtterance -->

<!-- api: CallInsightsMoment -->

<!-- api: CallInsightsTopic -->

<!-- api: CallInsightsNextStep -->

## 无障碍

- 时间轴是一个名为 "Call timeline" 的滑块：**←/→** 移动 5 秒（**Shift**：30 秒），**Home/End** 跳到两端，**PageUp/PageDown** 跳到上一个或下一个时刻，**Space** 播放或暂停。它的值是以秒计的位置。
- 文字记录的行、时刻和话题都是按钮，以其内容命名；选中的话题会报告 "selected"。
- 后续步骤是复选框，以步骤命名，负责人和截止日期作为说明。
- 跳转、话题选择和搜索结果数会以礼貌方式播报。

## 动效

- 区块第一次显示时，情绪曲线从左向右画出（900 毫秒）。
- 行和话题在悬停时用 160 毫秒着色；播放头通过变换移动，所以播放时不会重画曲线。
- 减少动效和关闭动效时，曲线立即显示，着色也不再过渡。

## 响应式

- 本区块读取自身的宽度。在 `CompactWidth`（760 px）或更窄时，侧栏移到文字记录下方，内边距从 24 收紧到 16。
- 时间轴总是占满整个宽度；刻度文字会变稀，保证彼此至少相隔 70 px。
- 过长的名字和悬停读数以省略号结尾，而不是换行。

## 相关组件

- `LineChart` 和 `Sparkline`，用于普通的时间序列。
- `Timeline` 和 `ChatThread`，用于动态和消息。
- `Account health` 和 `Lead scoring`，其他销售类区块。
