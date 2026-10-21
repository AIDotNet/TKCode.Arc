---
title: 快捷键录入
summary: 将按键组合录入为键帽，支持冲突警告、Kbd 与可搜索的速查表。
source-hash: 71acd8b4e49f
---

<Demo name="Default" />

## 何时使用

- 允许用户重新映射操作的键盘快捷键设置。
- 显示某个操作当前绑定的快捷键提示。
- 键盘操作是主要路径的桌面与 Web 应用。

## 何时不用

- 快捷键固定、只需展示的场景：请用你自己的文字展示。
- 通过名称执行操作：请改用命令面板模式。
- 仅支持触屏的产品：没有可录入的物理键盘。

## 用法

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:ShortcutRecorder Label="命令面板"
                          Description="在应用的任何位置打开命令面板。" />
</UserControl>
```

点击字段或按 Enter，然后按下组合键。录入器会保存一个 `KeyGesture`。

### 值与重置

`Value` 保存录入的 `KeyGesture`，没有快捷键时为 `null`。`ResetValue` 是重置按钮恢复的值。示例中它被设为默认快捷键。

<Demo name="Empty" />

```csharp
var palette = new ShortcutRecorder
{
    Label = "命令面板",
    Value = ShortcutFormatter.Parse("mod+k", ShortcutPlatform.Other),
    ResetValue = ShortcutFormatter.Parse("mod+k", ShortcutPlatform.Other)
};
```

`mod` 在 macOS 上代表 Command，在其他平台上代表 Control。使用 `ShortcutFormatter.Parse` 或 `TryParse` 可根据该文本创建手势。

### 已知快捷键与冲突

`Bindings` 列出已在使用的快捷键。录入其中一个时，会出现冲突提示，并带有「仍然使用」和「取消」按钮。「仍然使用」会替换原有绑定，并在事件中报告。

<Demo name="Conflicts" />

关闭标签页或复制等保留组合同样会给出警告。将 `IsReservedWarningEnabled` 设为 `False` 即可关闭该警告。

<Demo name="Reserved" />

### 修饰键与平台

`IsModifierRequired="False"` 允许单个按键。功能键始终可用。`Platform` 设置键帽样式为 `Mac` 或 `Other`。默认情况下，录入器使用当前运行的平台。

<Demo name="NoModifier" />

<Demo name="Platforms" />

### 事件

`ValueChanged` 会报告旧手势与新手势。点击「仍然使用」之后，`Replaced` 保存被替换的绑定。

<Demo name="Events" />

```csharp
recorder.ValueChanged += (_, e) =>
{
    var replaced = e.Replaced is { } binding ? $" (replaced {binding.Label})" : string.Empty;
    Console.WriteLine($"{e.OldValue} -> {e.NewValue}{replaced}");
};
```

### 隐藏标签

`IsLabelVisible="False"` 会让标签仅供屏幕阅读器使用。

<Demo name="HiddenLabel" />

### 说明文字

`Description` 是字段下方的辅助文字。录入提示、修饰键提醒与冲突提示在出现时会取代它。

<Demo name="Description" />

### 禁用

禁用的录入器会变淡，显示在柔和的背景上，并且不在 Tab 顺序中。

<Demo name="Disabled" />

### 键盘

- Enter 或 Space 开始录入。Escape 取消。Backspace 或 Delete 清除快捷键，未在录入时同样有效。
- Tab 会停止录入并移动焦点，且不录入 Tab。离开字段也会停止录入。
- 按键读取自物理按键，因此 Alt+K 录入的是 K，而不是键入的符号。系统保留的组合键，例如 ⌘Tab、Alt+Tab 和 Win 键组合，永远不会到达应用。

## API 参考

<!-- api: ShortcutRecorder -->

## 无障碍

- 录入器是一个切换按钮，名称由其标签和朗读的快捷键组成，例如 "Command Shift K"。
- 录入状态、结果、冲突与清除都会以礼貌方式播报。帮助文字会随字段一起朗读。
- 录入器内部的键帽对辅助技术隐藏，朗读的名称改由快捷键本身提供。
- Enter 或 Space 开始录入。Escape 取消录入；Tab 会停止录入并移动焦点，且不录入 Tab。
- 录入期间，按键不会触发窗口快捷键。

## 动效

- 键帽以轻微缩放和模糊弹出。组合键完成后，键帽按顺序落定。
- 组合键变化时，键帽滑动到新的位置。
- 录入时，录制指示点会脉冲闪烁。消息行以高度弹簧展开和收起。
- 减弱动效时，去除缩放、错峰与滑动，键帽与消息仅淡入淡出。关闭动效时直接到达最终状态。

## 相关组件

- `Input` 用于非快捷键的自由文本。
- `Button` 用于快捷键所触发的命令。
- `Field` 与 `FieldFrame` 可用于搭建带自定义编辑器的类似字段。
