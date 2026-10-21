---
title: 文字呈现
summary: 以克制的动效呈现一小段内容。
source-hash: 7ffb6c8f9d25
---

<Demo name="Hero" />

## 何时使用

- 首屏大标题或落地页开场，需要在出现时播放一次。
- 位于标题下方的简短引言，逐词呈现。
- 即使脚本加载缓慢，也必须从第一帧起就可读的标题。

## 何时不用

- 页面更靠下、需要随滚动进入视口时呈现的分节标题：用 `InViewTitle`。
- 长正文：逐词效果仅适用于标题和短段落。
- 持续进行的状态行：用 `TextShimmer`。

## 用法

控件只播放一次入场效果。文本中的 `\n` 会有意换行；各行经过平衡处理，且宽度不超过 24 个字符。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:TextReveal Text="从第一帧起就显得周到的界面"
                    Level="H1" />
</UserControl>
```

### 标题级别与段落

`Level` 设置标题级别（`H1`、`H2`、`H3`）或 `Paragraph`。标题使用展示字号与更紧凑的行距；段落使用正文字号与更柔和的模糊。对于标题，`Level` 还会设置 `AutomationProperties.HeadingLevel`。

<Demo name="ForcedLinesAndDelay" />

<Demo name="Paragraph" />

`Delay` 会在第一个词升起前等待。之后的错落间隔保持在半秒以内，因此长文本不会拉长入场时间。

### 重播

`Replay()` 会再次播放入场效果。请在代码中调用，例如在按钮点击时。

```csharp
var hero = new TextReveal { Text = "从第一帧起就显得周到的界面", Level = TextLevel.H1 };
var replay = new Button { Content = "重播", Variant = ButtonVariant.Secondary };
replay.Click += (_, _) => hero.Replay();
```

### 首次布局前的入场

挂载在首屏以下的控件会保持隐藏，直到第一次进入视口才开始等待。挂载时已在屏幕上的控件则从第一帧开始播放。运行入场效果期间 `IsRevealing` 为 `true`。

### 字距、行距与模糊

`Tracking` 以 em 为单位设置字间距，`Leading` 以字号的倍数设置行高，`BlurRadius` 设置每个词的起始模糊半径。级别主题已设定这些值；仅当标题需要不同的节奏时才覆盖它们。

```xml
<arc:TextReveal Text="默认保持安静" Level="H2" Tracking="-0.02" Leading="1.2" />
```

## API 参考

<!-- api: TextReveal -->

## 无障碍

- 标题以完整文字作为名称，并将 `HeadingLevel` 设为 1 至 3。段落则为纯文本。
- 强制换行会被读作空格。
- 控件本身不播报变化。当需要播报文字时，请在父元素上使用实时区域。
- 本控件不可获得焦点，也没有键盘交互。
- 请根据页面大纲选择级别，而不是根据想要的字号。

## 动效

- 每个词从各自的裁剪区中升起，同时从柔和的模糊中逐渐清晰。词按顺序开始。
- 整个入场效果在控件出现于屏幕上时播放一次，只有调用 `Replay()` 时才会重播。
- 减弱动效时保留词序，但去掉上升与裁剪；词快速淡入。
- 关闭动效时立即显示最终文字。

## 相关组件

- `InViewTitle`：随滚动进入视口而呈现的分节标题。
- `TextShimmer`：表示持续工作的状态行。
- `TextMorph`：简短标签逐字母切换状态。
