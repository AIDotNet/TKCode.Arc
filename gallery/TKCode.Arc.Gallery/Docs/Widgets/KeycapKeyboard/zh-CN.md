---
title: Keycap keyboard
summary: 由键帽组成的键盘：点亮一个快捷键，按住修饰键时显示各键的作用，打字或点击时按键会按下。
source-hash: 9289b98555f8
---

<Demo name="Preview" />

## 何时使用

`KeycapKeyboard` 绘制一个紧凑的键盘，键帽看起来像真的一样。它可以用强调色点亮一个快捷键，也可以在键上写出每个键的作用：按住 Ctrl，带 Ctrl 快捷键的键就显示它们的动作。真实键盘按下时对应的键会跟着按下，键也可以点击；点击的修饰键会一直保持按住，直到下一个键。

- **快捷键帮助**：“键盘快捷键”面板，人们按住修饰键来查看。
- **新手引导与提示**：展示完成某件事的那一个组合键。
- **按键测试与打字练习**：打字时看着键亮起。

## 何时不用

- **输入文字**：它不是用来输入的屏幕键盘；请用系统自带的。
- **很长的快捷键列表**：可搜索的表格更好读。
- **美式键盘以外的布局**：按键的位置和标签都按美式布局。

## 用法

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:KeycapKeyboard Label="Editor shortcuts">
        <arc:KeycapBinding Gesture="Ctrl+C" Label="Copy" />
        <arc:KeycapBinding Gesture="Ctrl+V" Label="Paste" />
        <arc:KeycapBinding Gesture="Ctrl+Shift+P" Label="Run" />
    </arc:KeycapKeyboard>
</UserControl>
```

`KeycapKeyboard` 在 Special 包中；请在 `UseArcControls()` 旁边用 `UseArcSpecial()` 注册。在里面放入 `KeycapBinding`：当按住的修饰键（`ActiveModifiers`）正好是某个快捷键的修饰键时，它的键就显示它的标签。没有修饰键的快捷键在什么都没按住时显示。每点击一个键都会触发 `KeyInvoked`；跟踪按键时，每次真实按键也会触发。它带有这个键、按住的修饰键以及它们组成的快捷键。

### 一个快捷键

`Shortcut` 点亮一个组合，写法可以是“Ctrl+Shift+P”“Cmd+K”，也可以用 Mac 符号“⇧⌘P”。`Platform` 让修饰键印上 Windows 或 Mac 的标签（Auto 跟随系统），`ShowsFunctionRow` 加上 Esc 和 F1 到 F12，`KeySize` 设定最大的键。

<Demo name="Shortcut" />

### 跟随真实键盘

`Tracking` 决定哪些按键会让键亮起：不跟踪、键盘获得焦点时的按键（默认），或窗口中的所有按键。窗口跟踪只是观察；按键仍会送到拥有焦点的控件。

<Demo name="Typing" />

### 最佳实践

**建议**

- 快捷键标签只写一个短词：一个键大约能放八个字母。
- 快捷键针对某个系统时，设置 `Platform`。
- 用 `Label` 给键盘起名，比如“Editor shortcuts”。

**避免**

- 在有密码的界面上使用窗口跟踪：键会显示输入的内容。
- 同时点亮多个快捷键；请改用快捷键列表。

## API 参考

<!-- api: KeycapKeyboard -->

<!-- api: KeycapBinding -->

## 无障碍

- 键盘是一个分组，名称来自 `Label`。它的帮助文本会读出点亮的快捷键（“Shortcut Ctrl+Shift+P.”）和当前显示的快捷键（“Ctrl+C: Copy.”）。
- 它是一个 Tab 停靠点。默认跟踪方式下，它获得焦点时真实按键会按下对应的键并触发 `KeyInvoked`；Tab 仍会把焦点移走。
- 指针获得焦点时不显示焦点环；键盘获得焦点时在整个键盘周围显示焦点环。

### 键盘交互

| 按键 | 作用 |
|---|---|
| Tab | 把焦点移到键盘上，再移走。 |
| 任意键 | 在键盘上按下这个键（默认 `Tracking`）。 |
| Ctrl、Shift、Alt、Win 或 ⌘ | 按住时显示这些修饰键的快捷键。 |

## 动效

- 键按下时陷入键帽侧面，松开时升起，大约 120 ms。
- 一个帧循环驱动所有的键，所有键停下后立即结束。
- 键和标签由键盘自己绘制；每帧不分配内存。

| 变化 | 完整 | 减弱 | 关闭 |
|---|---|---|---|
| 按键 | 陷下再升起 | 直接按下再弹起 | 直接按下再弹起 |
| 按住修饰键 | 显示它的快捷键 | 显示它的快捷键 | 显示它的快捷键 |

## 响应式

- 键盘宽 15 个键位。空间足够时键使用 `KeySize`（44 px），否则缩小以适应给定的宽度，所以在手机上也完整；标签随之缩小。
- 触摸时不显示悬停效果；点按即点击这个键。

## 相关组件

- `ShortcutRecorder` 记录人们按下的快捷键。
- `CommandPalette` 按名称查找命令，并显示它们的快捷键。

| 组件 | 适用场景 |
|---|---|
| `KeycapKeyboard` | 展示快捷键所在的位置，或让人查看 |
| `ShortcutRecorder` | 让人设置自己的快捷键 |
| `CommandPalette` | 按名称查找命令 |
