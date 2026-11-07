---
title: Voice orb
summary: 一个发光的语音模式球体：静止时缓缓呼吸，倾听时泛起涟漪，思考时旋转，说话时随之脉动，并配有实时字幕与通话控件。
source-hash: a7c6fb7116ea
---

<Demo name="Preview" />

## 何时使用

`VoiceOrb` 是与助手进行语音对话时的核心视觉元素。它是一个带有大理石纹的流动球体，让人一眼就能看出该谁说话：没有任何动作时它缓缓呼吸；用户说话时它随着声音膨胀并向外发出涟漪；思考时它收拢，边缘绕着一条明亮的光带；回答时它随着每个词变得更加起伏并脉动。另外两个组件与之配合，构成完整的语音模式界面：`VoiceTranscript` 逐词显示实时字幕，`VoiceControls` 是通话栏，包含开始、静音、计时器和结束按钮。

- **免提助手界面**：厨房显示屏、车载仪表盘、帮助台自助终端、与机器人的电话通话。
- **聊天应用中的语音模式**：用户点按一下按钮，聊天界面退到一旁，光球接管界面，直到通话结束。
- **任何需要让人知道自己是否被听到的地方**：倾听、思考和说话三种状态看起来明显不同，而静音的光球会变得安静且呈灰色。

## 何时不用

- **录制语音消息以便发送**：请使用带波形和确认步骤的录音器；光球展示的是实时对话，而不是录音片段。
- **文字聊天**：请使用带流式文字的聊天线程。字幕只显示当前轮次。
- **通用的加载状态**：请使用加载指示器、进度条或骨架屏。光球的动效表示"助手正在思考你刚才说的话"，而不是"页面正在加载"。
- **向文本框口述输入**：请使用 `DictationButton`，它会把内容写入文本框。

## 用法

三个控件位于 Special 包中：在 `UseArcControls()` 旁边调用 `UseArcSpecial()` 进行注册。每个控件都是独立的；语音界面通常会把它们堆叠在一起。

```xml
<StackPanel Spacing="14" MaxWidth="480">
    <arc:VoiceOrb State="{Binding State}" IsMuted="{Binding IsMuted}" Size="200" />
    <arc:VoiceTranscript Turn="{Binding Turn}" Speaker="{Binding Speaker}"
                         Text="{Binding Caption}" IsComplete="{Binding CaptionIsFinal}" />
    <arc:VoiceControls IsActive="{Binding InCall, Mode=TwoWay}" IsMuted="{Binding IsMuted, Mode=TwoWay}"
                       Elapsed="{Binding Elapsed}" />
</StackPanel>
```

上方的预览由 `VoiceOrbSession` 驱动。它是真实语音后端的替身，该包提供它用于演示和原型开发。它会循环播放一段虚构的周末出游对话，时序十分逼真：用户的话语在开始倾听后 0.9 s 出现，之后每个词的间隔为 150 ms 加上前一个词每个字符 22 ms；光球在最后一个词之后 0.7 s 开始思考，持续 1.5 s；回答会逐词说出（每个词都会使光球脉动）；回答结束 1.3 s 后开始下一轮对话。点按光球会执行当前状态所暗示的操作：开始通话、立即发送这句话、取消回答并继续说话，或打断助手。光球按钮及其上方的状态行属于演示本身。

```csharp
var session = new VoiceOrbSession(VoiceOrbSamples.WeekendTrip);
var link = session.Attach(Orb, Transcript, Controls);   // dispose to disconnect
OrbButton.Click += (_, _) => session.Tap();
```

### 每种状态

<Demo name="States" />

| 状态 | 主体 | 周围 | 尺寸（占 `Size` 的比例） |
|---|---|---|---|
| `Idle` | 缓慢的大理石纹流动 | 淡淡的光晕；光球每 5.03 s 呼吸一次 | 0.302 ± 0.0085 |
| `Listening` | 随输入电平膨胀 | 两道涟漪从边缘向外扩散；光晕变亮 | 0.31，声音大时最高约 0.34 |
| `Listening`，已静音 | 灰色，略暗 | 没有涟漪，光晕暗淡 | 0.295 |
| `Thinking` | 收拢；大理石纹扭转成漩涡 | 一道明亮的光带沿边缘顺时针运行 | 0.275 |
| `Speaking` | 轮廓更起伏，随输出电平脉动 | 最强的光晕 | 0.31，最高约 0.34 |

每一次变化都以弹簧平滑过渡（约 0.8 s，无回弹）；在过渡中途再次切换状态时，速度会保持不变，因此快速地依次经历倾听、思考和说话时，画面不会跳动。

### 自定义电平

<Demo name="Levels" />

光球从不打开麦克风。倾听时，它每一帧读取 `InputLevel`（0 到 1）；说话时则读取 `OutputLevel`。如果两者都未设置（即不是数字），光球会使用 `LevelSource`，或内置的模拟语音：每个音节对应一次确定性的脉动，短语之间有换气般的起伏。对于语音输出，你还可以像会话那样，为每个说出的词调用一次 `PulseOutput`：每次脉冲会在一帧之内让光球膨胀，并在约一秒内逐渐消退。

```csharp
// A microphone meter from your audio stack, 0..1:
meter.LevelChanged += level => Orb.InputLevel = level;
// Or implement IVoiceOrbLevelSource to be read once per frame:
Orb.LevelSource = new MyLevels();
```

### 字幕与通话栏

<Demo name="CallBar" />

`VoiceTranscript` 按空白字符拆分 `Text`。追加词语时，只有新增的词会有入场动画；其他任何变化都会直接替换整行，不带入场效果。若要开始新的轮次，请同时更改 `Turn`、`Speaker` 和新的 `Text`（通常为空）：旧的一行向上离开，新的一行淡入。助手的行使用前景色，用户的行使用次要文字颜色；系统行（提示、"Call ended after 1:07."）字号小一级，且颜色较淡。视图中保留两行；更早的行会滑到顶部柔和的边缘之下。

`VoiceControls` 在用户按下按钮时会自行维护 `IsActive` 和 `IsMuted`（请使用双向绑定），并引发 `StartRequested`、`MuteChanged` 和 `EndRequested`。它本身不计时：请从你的通话时钟推送 `Elapsed`。

### 颜色

<Demo name="Tints" />

光球遵循主题的强调色。`Tint` 会替换球体主体、光晕和涟漪所用的颜色；大理石纹中的浅色纹路使用 `Tint2`，或由色调派生的更浅、更柔和的色调（暖色调偏向橙色）。在深色主题中，同样的强调色会呈现出一个苍白、带银光的光球，并带有明亮的光晕。

### 结构

1. **主体**：带有球面明暗效果的双色流动大理石纹，边缘有菲涅耳光边，左上方有柔和的高光。其轮廓是一条由 96 个点组成的曲线，带有轻微漂移的小圆瓣。
2. **光晕**：从边缘向外淡出的色调，在距离 `Size` 的 0.47 处消失；方形区域的其余部分留给光晕和涟漪。
3. **涟漪**（倾听时）：两道细环，从边缘外侧 4 px 处产生，缓缓扩散到 19 px 并逐渐淡出；输入声音越大，涟漪出现得越快。
4. **光带**（思考时）：一段约 60° 宽的明亮弧线，以每秒 160° 的速度沿边缘顺时针运行，并会略微越出边缘。
5. **字幕**：最多两行居中文字，字号 16/22.4（系统行为 14/19.6），宽度最多 480。
6. **通话栏**：一个 50 px 高的胶囊形控件，包含开始（麦克风图标和文字标签），或静音（40 px，静音时呈按下状态）、m:ss 计时器，以及结束（40 px，危险色调）。

### 减少动效

<Demo name="ReducedMotion" />

### 内容准则

- 字幕应显示用户实际说出的话，不要改写；助手的句子要简短，以便放进两行之内。
- 提示和结果使用系统行，例如："Tap the orb to start"、"Call ended after 1:07."。
- 当屏幕会从远处被查看时，请在光球旁配上简短的状态词（"Listening"、"Thinking"）。
- 清楚说明记录了什么。如果没有存储任何内容，请在控件旁边直接说明。

### 最佳实践

- 根据后端的真实事件驱动 `State`：麦克风打开时开始倾听，请求发出时开始思考，音频播放时开始说话。
- 有真实电平时就推送真实电平；模拟语音仅用于原型和演示。
- 在完整的语音界面上使用 200 到 208 的 `Size`；在窄屏或行内场景中使用约 144。
- 只有把光球包裹在一个按钮中，并为其指定能说明点按作用的名称，它才能成为点按目标。
- 静音必须停止你的麦克风，而不仅仅是让光球变灰。

### 常见问题

**光球会录制音频吗？** 不会。该包从不打开麦克风或扬声器；电平只是你的应用推送的数字。

**可以使用品牌色吗？** 可以：设置 `Tint`，并用 `Tint2` 设置第二种大理石纹颜色。

**为什么字幕不会逐字朗读？** 屏幕阅读器只会在 `IsComplete` 被设置时朗读一次完整的轮次，而不是一连串的碎片。

**隐藏时会占用帧率吗？** 当光球不在屏幕上、被隐藏或低于完整动效级别时，帧循环会暂停。

## API 参考

<!-- api: VoiceOrb -->

<!-- api: VoiceTranscript -->

<!-- api: VoiceControls -->

<!-- api: VoiceOrbLabels -->

<!-- api: VoiceControlsLabels -->

<!-- api: VoiceControlsMuteChangedEventArgs -->

<!-- api: IVoiceOrbLevelSource -->

<!-- api: VoiceOrbSimulatedLevels -->

<!-- api: VoiceOrbSession -->

<!-- api: VoiceOrbScript -->

<!-- api: VoiceOrbScriptTurn -->

| 类型 | 取值 |
|---|---|
| `VoiceOrbState` | `Idle`、`Listening`、`Thinking`、`Speaking` |
| `VoiceTranscriptSpeaker` | `Assistant`、`User`、`System` |

`VoiceOrbSamples.WeekendTrip` 是演示所播放的虚构对话。

## 无障碍

- 光球是一个以其状态命名的图像（"Voice assistant is idle"、"Listening"、"Thinking"、"Speaking"、"Muted"）；如需使用你自己的文字，请设置 `Label` 或 `Labels`。它不接收焦点。
- 字幕中做动画的词语会对辅助技术隐藏。一个礼貌型即时区域会在整个轮次定稿（`IsComplete`）后朗读一次：用户的话在发送时朗读，助手的回答在完成时朗读，系统行则立即朗读。
- 静音按钮是一个切换按钮（静音时呈按下状态），名称为 "Mute microphone" 或 "Unmute microphone"。计时器的名称以文字表示（"1 minute 5 seconds"）。
- 从通话栏开始通话时，焦点移到 Mute；结束通话时，焦点移回 Start。
- 键盘焦点显示 2 px 的环：在圆形按钮的外侧绘制，在 Start 的边缘内侧绘制（否则胶囊会将其裁切）。指针焦点不显示任何环；悬停样式仅适用于鼠标或触控笔。

### 键盘

| 按键 | 操作 |
|---|---|
| `Tab` / `Shift+Tab` | 通话前：Start。通话中：先 Mute，再 End（跳过计时器） |
| `Enter` / `Space` | 按下获得焦点的按钮；在 Mute 上则切换麦克风 |

## 动效

| 变化 | 完整 | 减弱 | 关闭 |
|---|---|---|---|
| 状态切换 | 十个形状弹簧（k 55，c 17：ω 7.4，ζ 1.15），约 0.8 s | 每种状态一个静止画面 | 每种状态一个静止画面 |
| 呼吸、流动、涟漪、光带 | 持续运动 | 静止 | 静止 |
| 电平与词语脉冲 | 在 1 到 3 帧内膨胀，约 0.5 s 内回落 | 不显示 | 不显示 |
| 新的字幕词 | 从 4 px 模糊中上升 3 px，340 ms | 120 ms 淡入 | 直接出现 |
| 更多行 | 整块向上弹起一行（ω 13.5） | 立即 | 立即 |
| 新轮次 | 旧行带模糊向上移出 10 px，180 ms；新行在 700 ms 内上升 8 px | 120 ms 淡入 | 直接替换 |
| 通话栏切换 | 宽度弹簧（起始 ω 11，ζ 0.93；结束 ω 10.6，ζ 0.9）；两组内容以 0.92 的缩放和 4 px 模糊交错切换（120 ms 移出，160 ms 移入） | 120 ms 淡入，宽度立即变化 | 立即 |
| 静音图标 | 在麦克风与关闭麦克风图标之间弹出切换 | 淡入淡出 | 直接替换 |
| 按钮填充 | 160 ms | 立即 | 立即 |

## 响应式

- 光球保持其 `Size`；在窄屏上请选择较小的尺寸。光晕需要整个方形区域，因此不要裁切它。
- 字幕占用所给的宽度，最宽 480，并以均衡的行数换行；在 320 px 宽的屏幕上，它们仍然显示两行。
- 通话栏的宽度与其内容相同（空闲时 166 px，通话中 144 px），并使自身居中。

## 相关组件

| 组件 | 何时选用 |
|---|---|
| `VoiceOrb` | 实时语音对话需要一个视觉主体、字幕和通话控件 |
| `DictationButton` | 语音输入到文本框中 |
| `SpeechPlayer` | 用播放器朗读一段文字回答 |
| `AiMascot` | 助手需要一个角色形象，而非抽象的光球 |
