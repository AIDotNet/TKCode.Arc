---
title: 文字变形
summary: 逐字母将标签变形为下一个状态。
source-hash: a984607c41a0
---

## 何时使用

- 会发生变化的简短状态词，例如"关注"与"已关注"，或"保存"与"已保存"。
- 自定义按钮或徽标内的标签，需要原地变化。
- 计数或价格的首尾保持不动、只有中间变化的场景。

## 何时不用

- 需要点击行为和多种状态的按钮：用 `Button`。
- 需要折成多行的文字：`TextMorph` 始终为单行。
- 长文本或正文：逐字母效果仅适用于简短标签。
- 表示持续工作的状态行：用 `TextShimmer`。

## 用法

将 `Text` 设为当前标签。当 `Text` 变化时，字母会滑动、升起或淡入到新标签。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:TextMorph Text="部署" FontSize="28" />
</UserControl>
```

```csharp
status.Text = "部署中";
```

### 切换状态

状态词从一个状态切换到下一个状态。两个标签共有的字母会滑动到新位置，新字母以短暂的错落效果升起，被移除的字母则模糊消失。

```csharp
var words = new[] { "部署", "部署中", "已部署", "已回滚" };
var index = 0;
next.Click += (_, _) => status.Text = words[index = (index + 1) % words.Length];
```

### 匹配方式

`Matching` 设置新旧文字之间如何配对字母。

- `Occurrence`（默认）将每个字符与新文字中相同次序的字符配对。第二个 "i" 与新文字中第二个 "i" 配对。
- `PrefixSuffix` 保持共有的开头与结尾不动，只替换中间部分。适用于价格和计数。

```xml
<arc:TextMorph Text="¥12 / 月" FontSize="20" Matching="PrefixSuffix" />
```

### 字体

`TextMorph` 使用 `TextElement` 的字体与前景属性。可以在控件或其父元素上设置。

```xml
<arc:TextMorph Text="已保存" FontSize="16" FontWeight="SemiBold" />
```

## API 参考

<!-- api: TextMorph -->

## 无障碍

- 当前文字即为控件的名称，辅助技术读取的是纯文本。
- 字母对辅助技术隐藏，因此变化不会被逐字母朗读。
- 控件本身不播报变化。当需要播报新状态时，请在父元素上使用实时区域。
- 本控件不可获得焦点，也没有键盘交互。

## 动效

- 共有的字母在弹簧作用下滑动到新位置。
- 新字母从柔和的模糊中以短暂的错落效果逐渐清晰。被移除的字母模糊淡出。
- 容器宽度随新标签以弹簧方式变化。字体或容器尺寸变化时则无动画地直接跳到新宽度。
- 减弱动效时标签与宽度立即切换。关闭动效时同样如此。

## 相关组件

- `Button`：用于可点击且有自身状态的控件。
- `TextShimmer`：表示持续工作的状态行。
- `TextReveal`：出现时只播放一次的标题。
