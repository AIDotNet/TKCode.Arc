---
title: Speech player
summary: 为 AI 回复朗读的播放器：Read aloud 变成一个紧凑的播放器，带播放、可拖动的进度条、语速和音色选择，高亮随朗读的词在回复中移动。
source-hash: 0f58f8c877f9
---

<Demo name="Preview" />

## 何时使用

`SpeechPlayer` 把 AI 的回复朗读出来。回复下方是它的操作行；Read aloud 会把这一行变成同样高度的紧凑播放器：播放与暂停、已播放时间、
一条可以拖动的细进度条、总时长、语速按钮、音色选择和 Stop reading。播放时，一个圆角高亮像卡拉 OK 一样随朗读到的词在文本中移动，
尚未朗读的词保持变淡。

- **聊天与回答界面**：用户希望回复被读出来，并在文本中跟随正在朗读的词。
- **无障碍与免看阅读**：朗读较长的回答、摘要或简报。
- **试听文本转语音的音色与语速**：用真实的产品文案对比效果。

## 何时不用

- **把语音转成文字**写进输入框：使用听写控件。
- **播客或长录音**，没有可高亮的文字稿：使用普通的音频播放器。
- **文本与实际朗读内容不一致**：先去掉 Markdown 和格式，否则高亮会错位。

## 用法

把 `Text` 设为回复内容，把 `Synthesize` 设为调用你的文本转语音服务的函数。它收到一个 `SpeechRequest`（文本、音色和取消令牌），
返回一个 `SpeechAudio`：以 `ISpeechAudio` 表示的播放、逐词时间和总时长。播放器驱动播放（播放、暂停、跳转、倍速），并在每一帧读取
它的位置，所以你的音频就是时钟。

```csharp
player.Text = message.Text;
player.Synthesize = async request =>
{
    var speech = await tts.SpeakAsync(request.Text, request.Voice.Id, request.CancellationToken);
    return new SpeechAudio
    {
        Audio = new MyAudioAdapter(speech.Stream),   // implements ISpeechAudio
        Timings = speech.Words                       // one start and end per word, or null
            .Select(w => new SpeechWordTiming(w.Start, w.End))
            .ToList(),
    };
};
```

`Text` 中每个以空白分隔的词是一个高亮步骤，空行开始新段落，因此服务商的逐词对齐数据可以一一对应。没有逐词时间时，播放器会根据
文本估算，并按音频长度拉伸对齐（`SpeechSimulation.EstimateTimings`、`SpeechSimulation.FitTimings`）。不设 `Synthesize` 时，
播放器是一个不发声的定时模拟。不设 `Text`（也没有下文的模拟）时播放器为空：没有回复，Read aloud 保持禁用（上方的 Empty 选项卡）。不设 `Voices` 时，音色选择器只提供一个 id 为空的音色，即你的服务的默认音色，名称来自 `SpeechPlayerLabels.DefaultVoice`。

### 结构

1. **作者行**：16px 的标志和模型名称（`AuthorName`、`AuthorMark`）；没有作者时隐藏。
2. **回复**：分段的文本。朗读时，正在朗读的词落在强调色高亮上，已朗读的词为完整颜色，未朗读的词变淡。
3. **底栏**：一个 32px 的位置，放操作行（Read aloud，然后是 Copy、Good response 和 Bad response，或你的 `Actions`）或者播放器。
4. **播放器**：播放、已播放时间、进度条、总时长、语速、音色和 Stop reading。

### 状态

| 状态 | 播放器显示 |
|---|---|
| `Idle` | 带 Read aloud 的操作行 |
| `Preparing` | 播放按钮里的加载圈，以及带微光的 "Preparing Ava's voice" |
| `Playing` | 暂停按钮、移动的进度条，以及跟随词语的高亮 |
| `Paused` | 播放按钮；高亮停在当前词上 |
| `Finished` | Play again；整段回复为完整颜色 |
| `Failed` | Retry、警示图标，以及 "Couldn't generate audio" 和你的服务抛出的原因 |

朗读中切换音色时，会中止旧音色的请求，获取新音色，并从正在朗读的词继续。语音按音色保留，所以切回某个音色、或在 Stop reading
之后再次朗读，都会立即开始。

### 在聊天栏中

<Demo name="Plain" />

`Variant="Plain"` 去掉边框和内边距，让回复融入对话记录。`Actions` 用你自己的按钮替换 Copy、Good response 和 Bad response；
`ShowsDefaultActions="False"` 只保留 Read aloud。`Voices` 设置音色选择中的音色，每个音色有 id、名称和一行简短说明。

### 事件

<Demo name="Events" />

`Played`、`Paused`、`Ended` 和 `Seeked` 带有 1× 时间线上的时间与总时长、词的索引、音色和语速。`WordChanged` 额外带有这个词，
可用于字幕或统计。`Seeked` 在拖动进度条结束时只触发一次，而不是每次移动都触发。另有 `VoiceChanged`、`SpeedChanged` 和
`Closed`。`SelectedVoiceId` 与 `Speed` 是双向的。

```csharp
player.WordChanged += (_, e) => caption.Text = e.Word;
player.Played += (_, e) => analytics.Track("tts_play", e.Voice.Id, e.Speed);
```

### 用代码控制

`Open()` 等同于 Read aloud；`Play()`、`Pause()`、`TogglePlayback()`、`Seek(TimeSpan)`、`SeekToWord(int)`、`Retry()` 和
`Close()` 与对应按钮的作用相同。`State`、`IsOpen`、`CurrentWordIndex`、`Position` 和 `Duration` 说明它当前的状态。

### 模拟语音

用于演示时，不要设置 `Text`，而是给播放器一个 `SpeechSimulation`，并传入你自己的回复、作者和音色。播放器进入视口后它开始朗读，在完整动效下，短暂停顿后会换用前两个音色中的另一个再读一遍，直到有人操作。应用自己的 `Text` 总是优先，把 `Simulation` 设为 null 会停止它。预览中的回复和音色是虚构的；作者是真实模型 Claude Sonnet 5.5。

```csharp
player.Simulation = new SpeechSimulation
{
    Text = "Lisbon in early May is a lovely choice. Mornings start near 16°C…",
    AuthorName = "Claude Sonnet 5.5",
    Voices = [new SpeechVoice("ava", "Ava", "Warm and conversational"), new SpeechVoice("leo", "Leo", "Calm, lower register")],
};
```

`SpeechSimulation.SynthesizeAsync` 是所有未设置 `Synthesize` 的播放器背后的替身：它像一次网络往返那样等待（640ms），然后返回估算的逐词时间，不带音频。传入
`Fail = true` 可以查看失败状态，传入更长的 `Delay` 可以查看 Preparing。使用它时保持 `IsSimulated` 开启，让音色菜单和播报说明
不会播放声音。

```csharp
player.Synthesize = request => SpeechSimulation.SynthesizeAsync(request,
    new SpeechSimulationOptions { Delay = TimeSpan.FromSeconds(2) });
```

### 自定义文案

`Labels` 包含播放器显示或播报的每一个词。替换整个对象即可本地化或改写：

```csharp
player.Labels = new SpeechPlayerLabels
{
    ReadAloud = "Vorlesen",
    StopReading = "Vorlesen beenden",
    PreparingVoice = voice => $"Stimme {voice} wird vorbereitet",
};
```

### 窄容器与触屏

卡片填满容器，最宽 640px。宽度小于 440px 时，总时长让位，进度条保留空间；已播放时间、语速、音色和 Stop reading 仍然显示。
检测到触摸后，底栏增高到 36px，使每个控件都满足触控目标尺寸。

### 最佳实践

- 传入与朗读内容完全一致的文本：词数必须与逐词时间一致。
- 在 `Synthesize` 中抛出带有简短、易懂信息的异常；它会显示在 "Couldn't generate audio" 旁边。
- 遵守请求的取消令牌：切换音色、Stop reading 或新的文本都会取消它。
- 在对话记录中使用 plain 变体，并传入与消息其他按钮一致的操作。

### 常见问题

**它自己会发声吗？** 不会。`TKCode.Arc.Special` 不依赖任何音频组件；声音由你的 `ISpeechAudio` 播放。没有它时，播放器自行计时，
不发出任何声音。

**服务商没有逐词时间怎么办？** 只返回音频即可。逐词时间会根据文本估算，并按音频长度拉伸对齐。

## API 参考

<!-- api: SpeechPlayer -->

<!-- api: SpeechVoice -->

<!-- api: SpeechAudio -->

<!-- api: ISpeechAudio -->

<!-- api: SpeechRequest -->

<!-- api: SpeechWordTiming -->

<!-- api: SpeechPlaybackEventArgs -->

<!-- api: SpeechWordEventArgs -->

<!-- api: SpeechVoiceChangedEventArgs -->

<!-- api: SpeechSpeedChangedEventArgs -->

<!-- api: SpeechSimulation -->

<!-- api: SpeechSimulationOptions -->

<!-- api: SpeechPlayerLabels -->

## 无障碍

- 播放器是一个带名称的分组。播放按钮的名称随状态变化：Play、Pause、Play again、Retry 或 Preparing audio（请求进行中时保留焦点，
  但按下不起作用）。
- 进度条是一个滑块，其数值文本为 "0:04 of 0:22"，每秒更新一次，而不是每帧更新。
- 一个礼貌模式的状态区域播报变化：以 1× 用 Ava 朗读（模拟，不播放声音）、暂停在 0:12、切换到 Leo、朗读完毕、失败原因、语速变化
  和反馈。
- 状态从不只靠颜色表达：失败时警示图标与 "Couldn't generate audio" 及原因同时出现；变淡的未朗读词保持正文对比度，只作为进度提示。
- Read aloud 以回复内容作为描述。音色菜单使用单选行，打开时定位在已选音色上。
- 底栏中隐藏的一半在淡出后离开可视树，所以 Tab 不会落在看不见的控件上。指针获得焦点时不显示焦点环；键盘焦点显示 Arc 的焦点环。

### 键盘

| 按键 | 操作 |
|---|---|
| Tab | 在 Read aloud 与各操作之间移动；朗读时在播放、进度条、语速、音色和 Stop reading 之间移动 |
| Enter、Space | 在 Read aloud 上打开播放器并开始朗读，焦点落在播放按钮上；在播放按钮上播放或暂停；在 Stop reading 上关闭播放器，焦点回到 Read aloud |
| 左方向键、右方向键 | 在进度条上后退或前进 2 秒 |
| Page Down、Page Up | 在进度条上后退或前进 10 秒 |
| Home、End | 在进度条上跳到开头或结尾 |
| Space、K | 在进度条上播放或暂停 |
| 上方向键、下方向键 | 在音色菜单中切换音色；Enter 选择，Escape 关闭菜单 |

## 动效

| 变化 | 完整 | 减弱 | 关闭 |
|---|---|---|---|
| Read aloud 与 Stop reading | 在同一个 32px 位置中，一行在 100ms 内淡出，另一行在 160ms 内淡入并上升 2px | 120ms 交叉淡化 | 立即 |
| 高亮在一行内移动 | 1× 时 150ms 滑过，1.5× 时 100ms，2× 时 75ms | 立即移动 | 立即 |
| 高亮到达新的一行 | 直接切过去 | 直接切过去 | 直接切过去 |
| 一个词被朗读 | 高亮落下时从变淡渐变为完整颜色 | 渐变 | 立即 |
| 开始或停止朗读 | 变淡效果和高亮在 160ms 内淡入淡出 | 淡入淡出 | 立即 |
| 进度条 | 填充与滑块逐帧跟随；悬停、聚焦和拖动时轨道在 160ms 内变粗、滑块放大出现 | 立即变粗 | 立即 |
| 播放、暂停、重播、重试、加载圈 | 原地以无回弹的缩放（0.26s）和 160ms 淡化切换 | 120ms 淡化 | 立即 |
| 语速与音色标签 | 在所有选项的隐藏副本上交叉淡化，按钮宽度不变 | 120ms 淡化 | 立即 |
| Preparing 标签 | 平缓的微光 | 静止 | 静止 |
| 模拟 | 在两个音色之间循环，直到有人操作 | 只读一遍 | 只读一遍 |

时钟在所有动效等级下都运行：朗读是内容，不是装饰。尺寸变化时高亮重新测量，不播放动画。

## 相关组件

- `AgentStatus`：智能体处理回复时的实时状态。
- `ToolCall`：智能体在回答时调用的工具。
- `CopyButton`：消息操作行中的复制按钮。
