---
title: 视口内标题
summary: 标题随滚动进入视口时逐步呈现。
source-hash: 9d59bccb3240
---

## 何时使用

- 长篇功能页面中的分节标题，需要共享同一种入场效果。
- 每次滚回视口时都应重新播放的标题，使用 `IsReplayEnabled`。
- 带有明确换行、在窄屏上不能重排的标题。

## 何时不用

- 页面加载时播放的首个大标题：用 `TextReveal`。
- 持续进行的状态文字，例如"正在生成摘要"：用 `TextShimmer`。
- 正文或长段落：逐词效果仅适用于标题。

## 用法

标题可见 45% 时开始呈现。默认使用 `Blur` 变体。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:InViewTitle Text="模糊效果逐词变清晰"
                     Variant="Blur" />
</UserControl>
```

### 变体

`Variant` 选择呈现样式。每种样式都先淡入不透明度，再清除模糊，最后才让位移稳定下来，因此标题在完全到位之前就已可读。

- `Word` 让每个词从各自的裁剪区中升起。
- `Line` 让每一行从裁剪区中升起，行与行间隔 80 毫秒。它使用 `Lines`。
- `Blur` 在词上升的同时，从模糊中逐词变清晰。这是默认值。
- `Tracking` 在每个词清除柔和模糊的同时，展开字间距。
- `Wipe` 以柔和的边缘从左到右揭开标题。

```xml
<arc:InViewTitle Text="文字从裁剪区中升起" Variant="Word" />
<arc:InViewTitle Text="柔和的擦拭揭开标题" Variant="Wipe" />
```

### 显式分行

`Lines` 为 `Line` 变体设置各行。各行按给定内容显示，不会重新排版。每一行都必须能在窄屏上放下。未设置 `Lines` 时，整个 `Text` 为一行。

```csharp
title.Variant = InViewTitleVariant.Line;
title.Lines = ["逐行升起，", "不再重新排版"];
```

### 每次进入都重播

`IsReplayEnabled` 会在标题离开视口时以快速淡出隐藏它，且没有错落效果。每次标题再次进入时都会重新播放。默认值为 `false`，因此标题只播放一次。

```xml
<arc:InViewTitle Text="每次进入都会重播"
                 IsReplayEnabled="True"
                 Level="H3" />
```

### 标题级别

`Level` 设置标题级别（`H1`、`H2` 或 `H3`），并同步设置对应的 `AutomationProperties.HeadingLevel`。默认为 `H2`。请根据页面大纲选择级别。

## API 参考

<!-- api: InViewTitle -->

## 无障碍

- 标题以其完整文字作为名称，并由 `Level` 设置 `HeadingLevel`。
- 动画部分对辅助技术隐藏，只朗读文字。
- 页面打开时已滚动越过的标题会立即显示最终状态。
- 本控件不可获得焦点，也没有键盘交互。
- 当需要播报变化时，请在父元素上使用实时区域。

## 动效

- 标题可见 45% 时开始呈现。不透明度先行，模糊随后清除，位移最后稳定。
- `Word` 与 `Line` 从裁剪区中升起，`Wipe` 从左到右揭开，`Tracking` 展开字距，`Blur` 逐词变清晰。
- 启用 `IsReplayEnabled` 后，离开视口是快速淡出，没有错落效果。
- 减弱动效时标题立即显示，没有位移、模糊或遮罩。关闭动效时同样如此。

## 相关组件

- `TextReveal`：页面加载时只播放一次的大标题。
- `TextShimmer`：表示正在进行工作的状态文字。
