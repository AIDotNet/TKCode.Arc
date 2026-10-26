---
title: 移动端图标按钮
summary: 44 乘 44 的纯图标按钮，有朴素、浅色、实心和悬浮几种变体，带弹簧按压状态。
source-hash: 3e3134f8f834
---

<Demo name="Basic" />

## 何时使用

`IconButton` 是只显示图标的按钮：用在导航栏里、卡片上，或悬浮在内容之上。

- **栏上的操作**：搜索、分享、更多。默认的 `Plain` 变体是没有背景的强调色图标。
- **卡片或行上的次要操作**：`Tinted`。
- **视图的主要操作**：`Filled`，悬浮在滚动内容之上时用 `Fab`。

实时示例在手机外框里运行，那是桌面上的模拟；真机上的触摸反馈这里没有验证。

## 何时不用

- **需要文字才能看懂的操作**：用带文字的 `Button`。
- **在几个选项之间选择**：用 `Segmented`、`Radio` 或 `Checkbox`。
- **多个同等的悬浮操作**：悬浮按钮只有一个主要操作；更多操作用 `Sheet`。

## 用法

`IconButton` 派生自 Avalonia 的 `Button`，所以 `Click`、`Command` 和绑定都和那里一样。把 `Icon` 设为一个几何，用 `Variant` 选外观。按钮是 44 乘 44。

```xml
<arcm:IconButton xmlns="https://github.com/avaloniaui"
                 xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                 xmlns:arc="https://github.com/AIDotNet/TKCode.Arc"
                 xmlns:arcm="https://github.com/AIDotNet/TKCode.Arc.Mobile"
                 Icon="{x:Static arc:ArcIcons.Share}"
                 Variant="Filled"
                 AutomationProperties.Name="Share" />
```

### 悬浮操作按钮

`Variant="Fab"` 得到一个 56 px 的强调色圆，带 ArcDesign token 的悬浮阴影。把它放在内容之上，例如 `Page` 的覆盖层槽，或对齐到右下角的 `Panel`。

<Demo name="Fab" />

### Token

`IconButtonToken` 保存几何：`TouchTarget`（44）、`FabSize`（56）、`IconExtent`（22）、`FabIconExtent`（24）、`PressedOpacity`（0.85）和 `DisabledOpacity`（0.5）。颜色和阴影来自 ArcDesign token。

## API 参考

<!-- api: MobileIconButton -->

## 无障碍

图标没有文字，所以一定要设置 `AutomationProperties.Name` 说明按钮做什么（“搜索”、“新消息”）；没有它，读屏软件什么有用的都读不出来。按钮至少 44 乘 44，悬浮的是 56。

## 动效

按下时按钮在快速弹簧上缩到 0.97（悬浮的缩到 0.92），向下没有延迟，并略微变暗；松手时弹回。半途松开的按压会保留速度。减少动效时按钮不缩放，只变暗。

## 相关组件

- `MobileButton`：带文字的按钮。
- `MobileNavbar`：朴素图标按钮在栏里的位置。
- `MobileSheet`：需要比一个悬浮按钮更多的操作时使用。
