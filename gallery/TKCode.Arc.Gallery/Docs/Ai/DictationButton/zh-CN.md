---
title: Dictation button
summary: 带语音输入的聊天输入框：麦克风弹性展开为录音胶囊，带实时波形、计时器、取消与插入按钮；说话时文字流入输入框，识别器确定之前显示为浅色。
source-hash: a1eb764b4d8a
---

<Demo name="Preview" />

## 何时使用

`DictationButton` 是为宁愿说话也不想打字的人准备的输入框。按下麦克风，它会向左弹性展开为录音胶囊：取消、实时波形、已录时长和插入按钮；插入按钮占据麦克风原来的位置，下一步操作正好在指针所在之处。说话时文字流入输入框，识别器的猜测以浅色显示，确定后变为正文颜色。插入会把文字追加到输入框并把焦点交还给输入框；按 Enter 发送。

- **聊天与智能体输入框**：触屏设备、长提示词，或打字不便的用户。
- **笔记、评论与客服回复**：口述能节省时间，发送前仍可编辑。
- **任何已有发送操作的输入框**：需要一个状态清晰的语音入口时，使用 `DictationControl`。

## 何时不用

- **全双工语音对话**（带语音回复）：请改用带通话控件的语音智能体模式。
- **需要保留的录音**（语音备忘录）：听写只插入文字，音频会被丢弃。
- **命令面板**：一句口令就应立即执行的场景；只有在确有把握时才在 `SpeechEnded` 中免手动确认。

## 用法

TKCode.Arc 本身不录制任何内容。语音来自你为所在平台或语音服务编写的 `IDictationRecognizer`：它报告输入电平和识别出的文字，其余由输入框完成。没有识别器（也没有下文的模拟）时，输入框只是一个普通字段：没有模型标签、没有占位文字，按下麦克风会提示听写不可用（`DictationErrorCode.Unsupported`）。

```xml
<arc:DictationButton Placeholder="Message" MaxDuration="60" />
```

```csharp
composer.SendRequested += (_, e) =>
{
    // The send button spins until the task completes; the field clears when it succeeds.
    e.Completion = chat.SendAsync(e.Text);
};

composer.Recognizer = new MyPlatformRecognizer();
```

`Text` 是双向的：输入、插入听写内容以及发送后的清空都会改变它。没有 `SendRequested` 处理程序时，发送是模拟的。`Model` 是工具栏开头的标签（为 null，即默认值时隐藏）；请如实传入输入框实际发送到的模型。预览显示的是真实模型 Claude Sonnet 5.5，即这条消息将发送到的模型；听写的文字是虚构的。

### 结构

1. **卡片**：带 1px 边框的 Surface 卡片，填满所在容器。
2. **输入框**：16/24 的文字，一到六行，超出后滚动，并让最新的文字保持可见。实时文字跟在正文之后：已确定的词使用正文颜色，识别器的猜测使用浅色。
3. **工具栏**（36px）：标签（模型、聆听时的键盘提示，或错误信息）、听写位和强调色的发送按钮。
4. **录音胶囊**：216px 宽、36px 高、4px 内边距：28px 的取消、波形、计时器和 28px 的插入。

### 状态

| 状态 | 听写位显示 | 工具栏标签 |
|---|---|---|
| `Idle` | 麦克风 | 模型 |
| `Starting` | 胶囊，柱条静止为圆点 | 键盘提示 |
| `Listening` | 带实时波形和计时器的胶囊 | 键盘提示 |
| `Processing` | 胶囊，插入按钮转圈（"Transcribing"） | 模型 |
| `Denied` | 带麦克风关闭图标的 Try again | "Microphone blocked in system settings" |
| `Error` | 带警示图标的 Try again | 简短原因 |

`MaxDuration`（默认 120 秒，0 表示不限制）到时会自动停止聆听并插入。

### 你自己的识别器

<Demo name="YourRecognizer" />

约定很简单。`StartAsync` 在音频开始流动后完成；麦克风被阻止时抛出带 `NotAllowed` 的 `DictationException`。运行期间通过处理程序报告，它们可以从任何线程调用。令牌被取消表示用户取消了：此时释放麦克风。`StopAsync` 返回最终文字。

```csharp
public sealed class MyPlatformRecognizer : IDictationRecognizer
{
    public async Task<IDictationSession> StartAsync(DictationHandlers handlers, CancellationToken cancellationToken)
    {
        var engine = await SpeechEngine.OpenAsync(cancellationToken)
            ?? throw new DictationException(DictationErrorCode.NotAllowed);
        engine.Result += (final, interim) => handlers.OnTranscript(final, interim);
        engine.Level += level => handlers.OnLevel(level);         // 0 to 1, as often as you like
        engine.UtteranceEnded += () => handlers.OnSpeechEnd();
        engine.Failed += code => handlers.OnError(code);
        cancellationToken.Register(engine.Dispose);
        return engine;                                            // an IDictationSession: StopAsync returns the text
    }
}
```

用于演示和设计评审时，改为把 `Simulation` 设为一个 `SimulatedDictationRecognizer`：它从不访问麦克风，朗读你自己的 `Scripts`，每次听写一段，每段由 `DictationPhrase` 组成，每个短语可带一个先流式显示、随后被纠正的听错 `Guess`。`Deny` 会像麦克风被阻止一样拒绝每次启动；`StartDelay`、`FinalizeDelay` 和 `WordInterval` 设置它的节奏。没有脚本时声音保持静默。`Recognizer` 总是优先于模拟，把 `Simulation` 设为 null 会停止它。请给每个控件各自的实例。

```csharp
composer.Simulation = new SimulatedDictationRecognizer
{
    Scripts =
    [
        [
            new DictationPhrase("Draft a short reply to the design team", "Draft a short reply to the desk team"),
            new DictationPhrase("and ask if the empty states can ship on Friday."),
        ],
    ],
};
```

### 只用控件

<Demo name="ControlOnly" />

`DictationControl` 是不带输入框的麦克风和胶囊，用于你已有的输入框。自行渲染 `TranscriptChanged`（临时文字用浅色），追加 `Committed` 的文字，并在 `Cancelled` 时丢弃实时文字。`SpeechEnded` 表示识别器听到了一句话的结尾。两者背后的无界面状态机 `DictationController` 同样是公开的。

### 麦克风被阻止

<Demo name="Blocked" />

识别器报告 `NotAllowed` 时，听写位弹性变为带麦克风关闭图标的 Try again，工具栏说明去哪里修复。其他错误显示警示图标和简短原因。Try again 重新开始；Escape 关闭它。

### 窄栏

<Demo name="Narrow" />

断点跟随卡片自身的宽度，而不是窗口：宽度小于 460px 时隐藏键盘提示，小于 400px 时错误文字变短，360px 时录音期间工具栏标签让出位置。胶囊始终保持 216px，说话时不会有任何重排。

### 减少动效

<Demo name="ReducedMotion" />

减少动效时，胶囊与输入框的尺寸直接变化，文字出现时不淡入，波形不再移动：柱条随声音原地起伏。关闭动效时柱条静止为圆点。

### 自动播放

设置了 `Simulation` 且没有 `Recognizer` 时，输入框会自行播放它的脚本（聆听、插入、发送），直到卡片内第一次出现指针、按键或焦点；卡片不在屏幕上时暂停。设置 `AutoPlay="False"` 可关闭它；它用于文档和演示，不用于生产环境。

### 自定义文字

`Labels` 包含输入框显示或朗读的所有文字，包括错误信息和播报：

```csharp
composer.Labels = new DictationButtonLabels
{
    Dictate = "Dicter",
    Insert = "Insérer",
    Error = code => code == DictationErrorCode.NotAllowed ? "Micro bloqué dans les réglages" : "La dictée a échoué",
};
```

### 最佳实践

- 在产品文案中说明麦克风被阻止时该怎么做；输入框已经说明了去哪里修复。
- 让标签如实：传入输入框实际发送到的模型，或传 null。
- 不要在生产环境开启自动播放；它用于文档和演示。

## API 参考

<!-- api: DictationButton -->

<!-- api: DictationControl -->

<!-- api: SimulatedDictationRecognizer -->

<!-- api: DictationButtonLabels -->

## 无障碍

- 礼貌型实时区域会播报：开始聆听（以及模拟识别器不录制任何内容）、正在转写、插入的词数、取消、发送，以及每个错误和修复方法。流入的文字不会被播报。
- 状态从不只靠颜色表达：麦克风被阻止时显示麦克风关闭图标和说明，其他错误显示警示图标和简短原因；Try again 沿用同一个图标。
- 只有听写位当前可见的那一面接受指针和 Tab。焦点跟随操作：麦克风把焦点移到插入，插入把焦点交还到输入框文字末尾，取消交还给麦克风，启动失败移到 Try again。
- 已录时长使用等宽数字并预留宽度；波形是装饰性的，对辅助技术隐藏。
- 麦克风、取消、插入和发送都是带名称和提示的图标按钮（Dictate、Cancel、Insert 或 Transcribing、Send）。
- 临时文字使用介于 TextMuted 与 TextSecondary 之间的色调，确定后变为正文颜色，因此在两种主题下都能区分，而不依赖色相。

### 键盘

| 按键 | 操作 |
|---|---|
| Enter、Space | 在麦克风上：开始听写；焦点移到插入 |
| Enter | 聆听时：停止并插入，在卡片内任何位置都有效 |
| Escape | 听写时：取消并丢弃；在 Try again 上：关闭错误 |
| Enter | 在输入框中：发送。Shift+Enter 换行 |
| Tab | 依次经过取消、插入，然后是发送；听写位隐藏的那几面会被跳过 |

## 动效

| 变化 | 完整 | 减少 | 关闭 |
|---|---|---|---|
| 麦克风变为胶囊 | 宽度向左弹性展开到 216px（临界阻尼，0.24s，无回弹） | 直接变化 | 直接变化 |
| 胶囊变为 Try again | 宽度弹性变化到标签的实测宽度 | 直接变化 | 直接变化 |
| 听写位的各面 | 交叉淡化：淡出 100ms，淡入 160ms | 交叉淡化 | 直接变化 |
| 工具栏标签 | 旧内容 100ms 淡出，新内容 160ms 淡入并上升 2px | 短暂淡化 | 直接变化 |
| 转写时的插入按钮 | 对勾交叉淡化为转圈（160ms） | 直接变化 | 直接变化 |
| 新词出现 | 淡入 140ms；被纠正的猜测只让改变的词重新淡入 | 直接出现 | 直接出现 |
| 输入框增高 | 弹性变化到每一新行 | 直接变化 | 直接变化 |
| 波形 | 每 60ms 从右侧进入一个采样，每根柱条逐帧缓动靠近它；停止时柱条原地静止为圆点 | 柱条随声音原地起伏 | 圆点 |

柱条静止后波形的逐帧循环即停止，卡片不在屏幕上或被隐藏时暂停。逐帧零分配：电平存放在固定大小的环形缓冲区中，所有柱条由一个控件绘制。

## 相关组件

- `AiComposer`：带附件、工具和模型菜单的完整提示词输入框。
- `ToolCall` 和 `AgentStatus`：智能体如何处理这条消息。
- `Tooltip`：图标按钮的名称提示。
