---
title: Typewriter terminal
summary: 一个会播放脚本的终端窗口：按人的节奏逐字输入每条命令，再逐行打印输出，等待时光标闪烁。
source-hash: e272c2f47b7c
---

<Demo name="Preview" />

## 何时使用

`TypewriterTerminal` 是一个演出脚本的小终端窗口。每条命令出现在提示符之后，一个字符一个字符地输入，节奏略有不均，像真人在敲；稍作停顿，仿佛按下了 Enter，输出就逐行出现。输入的位置有一个方块光标，终端等待时它会闪烁。脚本可以播放一次或循环，并且会等窗口滚动进入视野后才开始，所以人们能从头看到。

- **产品或文档页面的安装与快速开始部分**：展示命令和它们的输出。
- **命令行工具的发布说明与更新日志**：一小段会话就能把故事讲清楚。
- **开发者工具的首页插图**：不用视频也能显得有生气。

## 何时不用

- **真实、可交互的命令行**：它只播放脚本，不接收输入。
- **人们要复制的代码**：播放时输入的文字无法选中。请同时用带复制按钮的代码块展示这些命令。
- **很长的日志**：每一行都由控件绘制。脚本请控制在几十行以内。

## 用法

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:TypewriterTerminal Title="~/projects/lantern">
        <arc:TypewriterTerminalLine Kind="Command" Text="lantern init my-site" />
        <arc:TypewriterTerminalLine Text="Creating a new site in ./my-site" Tone="Muted" />
        <arc:TypewriterTerminalLine Text="Ready in 2.3s" Tone="Success" />
    </arc:TypewriterTerminal>
</UserControl>
```

`TypewriterTerminal` 是 Special 包里的控件。在 `UseArcControls()` 旁边用 `UseArcSpecial()` 注册这个包。它的内容就是脚本：直接写在里面的 `TypewriterTerminalLine`，或在代码中加入 `Lines`（`TypewriterTerminalLine.Command("...")` 和 `TypewriterTerminalLine.Output("...", tone)`）。窗口会预留整个脚本的高度，所以播放时页面不会移动。给它设置 `Height`，文字就会滚动跟随最新的一行。

### 行与色调

行的 `Kind` 是 `Output`（整行打印，默认）或 `Command`（在 `Prompt` 之后输入，`Prompt` 默认是 `$`）。`Tone` 给行上色：`Default`，`Muted` 用于进度和注释，`Accent` 用于链接，`Success`、`Warning` 和 `Danger` 用于结果。`Pause` 在某一行之前加一段等待，比如缓慢的构建步骤。只播放一次的脚本在结束时，标题栏会显示一个重播按钮（`ShowsReplay`，文字为 `ReplayText`）。

<Demo name="Tones" />

### 节奏

`TypingSpeed` 是输入速度，单位为每秒字符数（28）；每次按键的间隔在平均值的约一半到一倍半之间变化。`CommandDelay` 是命令最后一次按键与输出之间的停顿（450 ms），`OutputDelay` 是两行输出之间的停顿（90 ms）。`IsLooping` 会在最后的提示符处等待 `LoopDelay`（3 s）后重新开始脚本。

### 用代码播放

`AutoPlay`（true）在窗口进入视野时播放脚本。把它设为 false，再自己调用 `Play()`。`Pause()` 让脚本停在当前位置，`Restart()` 从头开始，`Complete()` 一次显示所有行。`IsPlaying` 和 `IsFinished` 报告状态，每一行完成时触发 `LineCompleted`（带行号），最后一行出现时触发 `Completed`。

<Demo name="Controls" />

### 最佳实践

**建议**

- 命令要短，看起来真实，让输出说明结果：一行成功信息胜过十行进度信息。
- 在缓慢步骤之后的那一行使用 `Pause`，让时间感觉真实。
- 用 `Title` 给窗口命名：它同时也是无障碍名称。

**避免**

- 脚本太长，人们没看完就离开了；请改为循环播放一段短的。
- 把唯一一份安装命令藏在动画里。

### 常见问题

**人们不用等待就能读完整个脚本吗？** 可以。辅助技术会一次读出完整的记录，按 End 或 Enter 会显示所有行，关闭动效时整个脚本立即显示。

**为什么光标过一会儿就不闪了？** 在不循环的脚本的最后一个提示符处，光标闪烁 12 秒，然后保持常亮，这样页面就不再请求帧了。

## API 参考

<!-- api: TypewriterTerminal -->

<!-- api: TypewriterTerminalLine -->

<!-- api: TypewriterTerminalLineEventArgs -->

## 无障碍

- 窗口是一个可聚焦的组，由 `Title` 命名，并描述为“terminal”。它的帮助文本是带提示符的完整记录，所以辅助技术不必等待输入过程。
- 窗口上的圆点只是装饰，对辅助技术隐藏。重播按钮是真正的按钮，由 `ReplayText` 命名。
- 键盘聚焦时，窗口周围显示焦点环；指针聚焦不显示。

### 键盘交互

| 按键 | 操作 |
|---|---|
| `Tab` | 聚焦终端 |
| `Space` | 暂停，或继续播放 |
| `End` / `Enter` | 一次显示所有行 |
| `Home` | 从头开始脚本 |

## 动效

- 命令的提示符先出现；0.5 秒后按 `TypingSpeed` 开始输入，每次按键的间隔是平均间隔的 0.55 到 1.45 倍。
- 光标是一个字符宽的方块。敲键时和之后半秒内保持常亮，然后在终端等待时以 1.06 秒为周期闪烁。
- 输出行整行出现；没有滑动也没有淡入，就像真正的终端。
- 文字每行只排版一次；正在输入的命令是整行被光标处裁剪后的画面，所以输入时不分配内存。只有在脚本播放或光标闪烁时才运行帧，窗口离开视野时脚本会等待。

| 变化 | 完整 | 减弱 | 关闭 |
|---|---|---|---|
| 命令 | 逐个字符输入 | 到时整条出现 | 全部一次显示 |
| 输出 | 逐行出现 | 逐行出现 | 全部一次显示 |
| 光标 | 等待时闪烁 | 常亮 | 常亮 |

## 响应式行为

- 窗口填满给定的宽度，长行在其中换行，命令在提示符的宽度之后换行。
- 它的高度是该宽度下整个脚本的高度，所以窄屏手机会得到更高的窗口；设置 `Height` 可以让它保持紧凑，并让文字滚动到最新的一行。
- 文字是 13 px 等宽字体；命令要短，这样在手机上不用换行也放得下。

## 相关组件

- `CodeBlock` 展示人们可以阅读和复制的代码。
- `InstallSnippet` 用带编号的步骤和代码片段，带开发者从安装走到第一个结果。

| 组件 | 何时选择 |
|---|---|
| `TypewriterTerminal` | 一小段会话要像真的在发生一样播放 |
| `CodeBlock` | 人们需要阅读或复制代码 |
| `InstallSnippet` | 快速开始需要人们跟着做并复制的步骤 |
