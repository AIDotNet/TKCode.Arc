---
title: 移动端文本框
summary: 44 px 高的填充文本框，可带图标和清除按钮，获得焦点时边框变为强调色。
source-hash: 1cfacade8182
---

<Demo name="Basic" />

## 何时使用

`TextField` 是移动端表单里的单行文本输入：姓名、邮箱、简短的回答。

- **手机上的表单**：文本框高 44 px，拇指容易点中。
- **有含义的字段**：文字前的 `Icon` 说明这个字段是做什么的。
- **常需要改正的字段**：清除按钮一次点按就能清空。

实时示例在手机外框里运行，那是桌面上的模拟；真机上的软键盘这里没有验证。

## 何时不用

- **搜索**：用 `Searchbar`，它有搜索图标、胶囊形状和搜索键。
- **很长的多行文字**：应该用文本区域，这个控件只有一行。
- **从几个值里选一个**：用开关、分段控件或带列表的面板。

## 用法

`TextField` 派生自 Avalonia 的 `TextBox`，所以 `Text`、选区、`IsReadOnly` 和绑定都和那里一样。用 `PlaceholderText` 显示提示。

```xml
<arcm:TextField xmlns="https://github.com/avaloniaui"
                xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                xmlns:arc="https://github.com/AIDotNet/TKCode.Arc"
                xmlns:arcm="https://github.com/AIDotNet/TKCode.Arc.Mobile"
                PlaceholderText="姓名"
                Icon="{x:Static arc:ArcIcons.User}"
                Text="{Binding Name}" />
```

清除按钮只在文本框有焦点且有文字时显示；只读或 `ShowClearButton` 为 false 时不显示。点它会清空文本并保持焦点。文本框没有悬停状态：手指不会悬停。

### Token

`TextFieldToken` 保存几何：`FieldHeight`（44）、`ContentPadding`（两侧 12）、`BorderWidth`（1）、`GlyphExtent`（18）、`IconMargin` 和 `DisabledOpacity`（0.5）。颜色、圆角和字体来自 ArcDesign token。

## API 参考

<!-- api: MobileTextField -->

## 无障碍

给每个字段一个看得见的名称，放在旁边，或者用图标加占位提示；人开始输入后，占位提示就不再是唯一的标签。文本框至少 44 px 高，清除按钮是 44 px 的触摸目标。

## 动效

文本框获得或失去焦点时，边框按快速曲线变色。清除按钮从 0.6 缩放加淡入弹出（P26，通过 `PresenceHost`），离开比出现更快。减弱动效时，边框立即变色，清除按钮也立即出现和消失。

## 相关组件

- `MobileSearchbar`：为搜索准备的文本框。
- `MobilePage`：表单所在的屏幕。
- `MobileButton`：提交表单的常用方式。
