---
title: Voice recorder
summary: 语音留言输入区：录进实时波形，回听并拖动定位这段录音，再把它作为语音气泡发进对话。
source-hash: af4f52da0286
---

<Demo name="Preview" />

## 何时使用

`VoiceRecorder` 是聊天里的语音消息角落：上方是一串语音气泡，下方是只有一个圆形麦克风按钮的输入区。轻点一下，麦克风就舒展成录音条，
里面有呼吸闪烁的圆点、走动的计时和从右侧滚入的波形。停止后同一条录音条变为回听状态，可以回放并拖动定位；发送会把录音条带进对话串，
成为你最新的一条气泡。垃圾桶按钮或 Escape 会把它收回成麦克风。

- **对话里的异步语音回复**：客服聊天、团队讨论串、客户与施工方之间来回的简短留言。
- **打字不方便的场合**：在手机上、在柜台前、在工地戴着手套时。
- **发送前先回听**：先听一遍、看一遍这段录音，不会误发。
- **双向混排的对话串**：收到的气泡在左侧并带发送者头像，你发出的在右侧。

## 何时不用

- **音乐、播客或长录音**：使用带完整进度条和播放队列的媒体播放器；气泡适合简短留言。
- **通过长按按钮确认危险操作**：使用长按确认控件。
- **只显示某件事已经进行了多久**：使用计时器或秒表，而不是录音器。
- **向文本框实时口述**：使用 `DictationButton`，它会边说边把语音转成文字。
- **朗读一段文字回复**：使用 `SpeechPlayer`。

## 用法

给录音器一个对话串，并处理录好的这段录音。什么都不设置时，对话串为空，输入区照常工作。
录音是模拟的：每段录音都会得到一个虚构但稳定的声音，因此不需要麦克风也能运行。需要真实声音时，
通过 `MessageSent` 和 `LevelSource` 接入你的音频采集。

```xml
<arc:VoiceRecorder x:Name="Recorder"
                   Height="408"
                   Label="Voice messages with Tomas Novak" />
```

```csharp
Recorder.Messages = VoiceRecorderSamples.Thread();
Recorder.MessageSent += (_, e) => store.Save(e.Message);
Recorder.Cancelled += (_, e) => log.Info($"Discarded {e.Message.Duration}");
```

`VoiceRecorder` 派生自 Avalonia 的 `TemplatedControl`，位于 Special 包中：在 `UseArcControls()` 之外再用
`UseArcSpecial()` 注册。上面的预览就是参考配置，人物均为虚构：一张 416 × 408 的卡片，带一个头部（绘制的首字母头像、
联系人姓名、话题和一个重置按钮），三条消息的对话串（对方、你、对方，分别长 14、6 和 9 秒），以及默认 60 秒的上限。头部和重置按钮
属于示例本身；录音器只为它们提供一个 `Header` 位置。

### 消息

对话串是一组 `VoiceRecorderMessage` 记录，最早的在前。带 `From`（或 `Avatar`）的消息是收到的：
它在左侧，带一个 28px 头像（图片，或 `From` 的首字母），气泡为柔和底色、播放按钮为深色。
两者都没有的消息是你发出的：靠右对齐，气泡为深色、播放按钮为浅色。`Meta` 是气泡下方的小字行，例如发送时间。

```csharp
var message = new VoiceRecorderMessage
{
    Id = "fence-1",
    From = "Tomas Novak",
    Duration = TimeSpan.FromSeconds(14),
    Peaks = VoiceRecorderPeaks.Generate(seed: 1, seconds: 14),
    Meta = "10:12 AM",
};
```

`Peaks` 是从 0 到 1 的条高，其中 0.12 表示静音。一个气泡有 14 根条再加每秒 1.3 根，介于
18 到 36 之间（`VoiceRecorderPeaks.BarCount`），因此在约 20 秒以内，消息越长气泡越宽。用
`VoiceRecorderPeaks.Resample` 把你自己的电平序列变成这么多根条（每根条取它那一段里最响的样本），
或用 `VoiceRecorderPeaks.Generate` 生成稳定的虚构波形。

发送的录音会追加到 `Messages`（只读列表会被替换成一份加上这段录音的副本）。要在多次会话之间保留对话串，
在 `MessageSent` 里保存这条消息，下次再传回来。`ResetMessages` 会替换整个对话串。

### 你自己的事件

<Demo name="Events" />

`MessageSent` 和 `Cancelled` 都以 `VoiceRecorderMessage` 携带这段录音：它的时长、条形和一个生成的 id。
发送的那条还带有 `SentMeta`（默认为 "Just now"），并且在事件到达时已经在对话串里。
`Phase` 表示输入区所处的阶段（`Idle`、`Recording`、`Review`），可以绑定。`MaxDuration` 会自动结束一段录音
并打开回听；这个示例在 15 秒处停止，并滚动一个更长的对话串。

你也可以从代码驱动录音器：`StartRecording`、`StopRecording`、`Send`、`Discard`，以及传入消息 id 的 `PlayMessage` 或
`PauseMessage`。

### 录音与回听

- **录音**：录音条显示一个每 1.2 秒呼吸一次的玫红圆点、精确到十分之一秒的时间（0:02.4）和实时
  波形。每 90ms 有一根新条从右侧进入，整排匀速向左滑动；越靠左端的条越淡。
  停止按钮的无障碍名称会带上时间（"Stop recording at 0:02"）。
- **回听**：录音条大小不变，交叉淡化成播放按钮、这段录音的 40 根条和它的时长。播放时条从左往右
  填满，时间随之增加；暂停或播完后时间回到总时长。在条上任意位置按下即跳到那里，不会开始
  播放。
- **发送**：录音条变成对话串末尾的一个气泡，从列表下边缘升起，同时列表平滑滚到底部；
  麦克风弹回原位。

同一时间只播放一个声音：开始播放一个气泡会暂停其他气泡和回听，开始录音会把它们全部
暂停。

### 来自麦克风的电平

<Demo name="CustomLabels" />

`LevelSource` 每 90ms 的样本调用一次，参数是录音编号和样本序号；返回 0 到 1 的输入电平。
没有设置时，录音器使用以录音编号为种子（消息数加一）的 `VoiceRecorderPeaks.Levels`，
因此丢弃后重新录的那段会显示同样的声音。这个示例还通过 `Labels` 改写了文字，并修改了 `SentMeta`。

### 窄容器

<Demo name="Narrow" />

录音器没有断点：它跟随得到的宽度。录音条占用垃圾桶和发送按钮之外的空间，
气泡被挤压时条会变细，连 1.5px 的条都放不下时回听会显示更少的条。`Plain`
去掉卡片的边框、圆角和底色，适合手机宽度的栏。

### 减少动效

<Demo name="ReducedMotion" />

### 结构

1. **头部**（可选）：发丝线上方的任意内容；预览在这里放了头像、姓名、话题和重置按钮。
2. **对话串**：语音气泡，底部对齐，滚动时不显示滚动条。
3. **气泡**：44px 的胶囊，带一个 32px 播放按钮、波形（已播放部分为实色，其余为 28%）和时间。
4. **附注行**：气泡下方 12px 的弱化文字。
5. **输入区**：静止时是提示语和 48px 麦克风；录音或回听时是垃圾桶、录音条和停止（或发送）按钮。
6. **状态区域**：用于播报的隐藏礼貌型实时区域。

### 内容准则

- 提示语保持一行短句，说明轻点会发生什么。在窄容器中会被截断。
- 在 `Meta` 中使用真实时间或相对日期，并保持简短（"10:16 AM"、"Yesterday"）。
- 在 `Label` 中写出对方的名字（"Voice messages with Tomas Novak"）：它是录音器的无障碍名称。
- 把 `SentMeta` 写成状态而不是指令（"Just now"、"Sent"、"Delivered"）。

### 最佳实践

- 立刻保存发送的消息；控件只把它保存在 `Messages` 里。
- 选择适合对话的 `MaxDuration`：一分钟适合大多数语音留言。
- 给录音器一个固定高度（或一个限制高度的父元素），让对话串滚动而不是不断变高。
- 在本身已有边框的面板和手机布局中使用 `Plain`。
- 接入真实音频时，让 `LevelSource` 保持轻量：它每 90ms 在 UI 线程上调用一次。

### 常见问题

**它会录音吗？** 不会。这个包没有麦克风或音频依赖。电平和录音都是模拟的；
把你的采集接到 `LevelSource` 和 `MessageSent`。

**为什么丢弃后同一段录音看起来一样？** 模拟的声音以录音编号为种子，而录音编号只在发送后
才改变。需要实时电平时请提供 `LevelSource`。

**两条消息能同时播放吗？** 不能。开始播放一条会暂停另一条。

**如何清空对话串？** 用空列表调用 `ResetMessages`，或设置 `Messages`。

## API 参考

<!-- api: VoiceRecorder -->

<!-- api: VoiceRecorderMessage -->

<!-- api: VoiceRecorderLabels -->

<!-- api: VoiceRecorderMessageEventArgs -->

<!-- api: VoiceRecorderPeaks -->

<!-- api: VoiceRecorderSamples -->

枚举与委托：

| 类型 | 取值 |
|---|---|
| `VoiceRecorderPhase` | `Idle`、`Recording`、`Review` |
| `VoiceRecorderVariant` | `Card`（默认）、`Plain` |
| `VoiceRecorderLevelSource` | `double (int take, int sample)`：一个 90ms 样本的电平，0 到 1 |

## 无障碍

- 录音器以 `Label` 命名；对话串命名为 "Messages"。
- 每个按钮都有名称："Record voice note"、"Stop recording at 0:04"（每秒更新）、"Discard voice note"、
  "Send voice note"、"Play preview" 或 "Pause preview"，每个气泡上则是 "Play voice message from Tomas Novak, 0:14" 或
  "Play voice message, 0:06"（播放时为 Pause）。
- 礼貌型实时区域会播报 "Recording"、"Recorded 0:04. Play it back or send it."、"Voice note sent" 和 "Voice note
  discarded"。播放与重置不播报。
- 波形、计时和圆点是装饰性的，对辅助技术隐藏；时间由按钮名称承载。
- 键盘焦点在圆形按钮外 2px 处显示 2px 的焦点环；指针焦点不显示。
- 焦点跟随输入区：麦克风把焦点交给停止，停止交给发送，发送或丢弃再交回麦克风。指针按下
  同样会把焦点留在录音器内（不显示焦点环），因此 Escape 始终有效。

### 键盘

| 按键 | 操作 |
|---|---|
| `Tab` / `Shift+Tab` | 依次经过头部、各气泡的播放按钮，然后是输入区：静止时为麦克风；录音时为垃圾桶和停止；回听时为垃圾桶、回放预览和发送 |
| `Enter` / `Space` | 按下当前焦点所在的按钮 |
| `Escape` | 录音或回听时，在录音器内任意位置：丢弃这段录音并把焦点交回麦克风 |

波形不可获得焦点；用指针按下来定位。

## 动效

- 麦克风舒展成录音条（以及录音条收回）使用同一个临界阻尼弹簧，大约半秒看起来就已到位；
  两端一起移动，内容贴在录音条左端并被它裁剪。在录音条还在舒展时开始收回，会保持当时的速度，
  因此会稍微多冲一点再折返。
- 录音条展开时，垃圾桶和停止按钮以更快的弹簧从 70% 弹出；离开时在 120ms 内缩小并淡出。
- 提示语和录音条的两行内容以 2px 模糊互相替换：120ms 淡出，50ms 后用 240ms 淡入。
- 实时条以约每秒 55 像素向左移动，并在约 110ms 内长高。
- 发送时，气泡以与麦克风相同的弹簧从录音条中长出，同时列表在约 130ms 内平滑滚到底部；
  麦克风以更慢的弹簧从 80% 回来。
- 圆形播放按钮按下时缩小到 92%。

| 变化 | 完整 | 减少 | 关闭 |
|---|---|---|---|
| 麦克风到录音条、录音条到麦克风、录音条到气泡 | 弹簧 | 立即 | 立即 |
| 按钮出现 | 缩放并淡入 | 淡入 | 立即 |
| 提示语与录音条内容 | 模糊并淡入淡出，延迟 50ms | 模糊并淡入淡出，无延迟 | 立即 |
| 实时波形 | 平滑移动，条逐渐长高 | 每个样本移动一格 | 每个样本移动一格 |
| 发送后的列表 | 平滑滚动 | 直接跳转 | 直接跳转 |
| 录音圆点 | 呼吸 | 呼吸 | 静止 |

## 响应式

- `VoiceRecorder` 没有断点；与参考一样，它跟随父元素给的宽度。
- 录音条宽度为输入区行宽减去 104 px（垃圾桶、发送按钮和两个间距）；416 px 时为 282 px。
- 气泡在放得下时保持自然宽度；被挤压时先让 3 px 的条变细。
- 回听的 40 根条以每根 1.5 到 3 px 分享录音条的空间；再窄时会绘制更少的条，而不是把末尾裁掉。
- 高度由父元素决定：给录音器一个固定高度，让对话串滚动。

## 相关组件

- `DictationButton`：边说边把语音输入到文本框。
- `SpeechPlayer`：朗读一段文字回复。

| 组件 | 何时选择 |
|---|---|
| `VoiceRecorder` | 大家在对话串中互发简短语音留言 |
| `DictationButton` | 语音需要变成文字 |
| `SpeechPlayer` | 一段文字回复需要被朗读出来 |
