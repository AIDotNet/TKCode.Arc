---
title: 徽标
summary: 用于状态、类别或元数据的小型标签。
source-hash: c21246ac5464
---

<Demo name="Tones" />

## 何时使用

- 标题旁或表格单元格中的简短状态，例如 Live、Draft 或 Failed。
- 徽标保持显示时原地变化的计数或状态。
- 带有色调和可选前导图标的行标记。

## 何时不用

- 完整的反馈句子：用 `Alert` 或 `Toast`。
- 用户可以开启或关闭的值：用 `ChipGroup`。
- 周围带有说明的大数字：用 `MetricCard`。

## 用法

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:Badge Content="草稿" />
</UserControl>
```

### 色调

`Tone` 设置颜色样式：`Neutral`（默认）、`Success`、`Info`（强调色）、`Warning` 和 `Danger`。标签本身必须说明状态，因为色调并不是唯一的提示。

```xml
<WrapPanel>
    <arc:Badge Content="草稿" />
    <arc:Badge Tone="Success" Content="上线" />
    <arc:Badge Tone="Info" Content="测试版" />
    <arc:Badge Tone="Warning" Content="降级" />
    <arc:Badge Tone="Danger" Content="失败" />
</WrapPanel>
```

### 尺寸

`Size` 设置高度：`Medium`（默认）为 26，`Small` 为 22。在紧凑的表格中使用 `Small`。

<Demo name="Small" />

### 图标

`Icon` 接收一个前导图标。它会采用色调颜色，并对辅助技术隐藏。示例使用来自 `ArcIcons` 的 `ArcGlyph`。

<Demo name="Icons" />

```csharp
badge.Tone = BadgeTone.Success;
badge.Content = "通过";
badge.Icon = new ArcGlyph { Data = ArcIcons.Check, Size = 12 };
```

### 原地变化

保持徽标挂载并修改 `Content`。新的字符串或数字会从柔和的模糊中升起，胶囊宽度会以弹簧方式调整以适应内容。重新创建徽标会失去这一动效。

<Demo name="ChangingInPlace" />

## API 参考

<!-- api: Badge -->

## 无障碍

- 徽标是行内文字：其标签与周围的文字一同被朗读。
- 图标对辅助技术隐藏。标签必须说明状态，而不是颜色。
- 标签变化时，旧文字对辅助技术隐藏，只朗读当前文字。
- 徽标不会播报其变化。如果变化必须被播报，请将徽标放入外围内容的实时区域中。
- 徽标无法获得焦点，也没有键盘行为。

## 动效

- 新标签会从柔和的模糊中升起，旧标签则带着轻微模糊向上离开。胶囊宽度随变形弹簧跟进。
- 新图标会从略小的尺寸弹出。由字体变化引起的尺寸变化会立即完成。
- 减弱动效时，标签快速淡入，宽度直接跳转。关闭动效时，所有变化立即完成。

## 相关组件

- `Alert` 或 `Toast`：用于一句反馈。
- `ChipGroup`：用于用户可以选择的值。
- `MetricCard`：用于带说明的大数值。
