---
title: 移动端标签栏
summary: 页面底部的标签栏：图标在上、文字在下的标签平分宽度，选中的一个高亮。
source-hash: ec2854917835
---

<Demo name="Basic" />

## 何时使用

`Tabbar` 是移动屏幕的底部导航：使用者用拇指在几个顶层区域之间切换。

- **应用的顶层区域**：首页、搜索、提醒和我的，始终触手可及。
- **两到五个目的地**：各项平分宽度，所以标签要短。

每个 `TabbarItem` 在标签文字上方显示一个 `Icon`。实时示例运行在手机框中，这是桌面上的模拟；真机上的触控反馈尚未验证。

## 何时不用

- **同一对象的多个视图**：在屏幕内部使用分段控件或标签页。
- **超过五个目的地**：把其余放进"更多"屏幕或菜单。
- **桌面侧边栏**：使用桌面包里的 `Sidebar`。

## 用法

把 `TabbarItem` 放进栏里，用 `SelectedIndex`（或 `SelectedItem`）设置打开的一项。`Tabbar` 派生自 Avalonia 的 `ListBox`，所以选择、`SelectionChanged` 和绑定 `ItemsSource` 的用法与那里相同。把这个栏用作页面的 `Footer`。

```xml
<arcm:Tabbar xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc"
             xmlns:arcm="https://github.com/AIDotNet/TKCode.Arc.Mobile"
             SelectedIndex="0">
    <arcm:TabbarItem Content="Home" Icon="{x:Static arc:ArcIcons.House}" />
    <arcm:TabbarItem Content="Search" Icon="{x:Static arc:ArcIcons.Search}" />
    <arcm:TabbarItem Content="Profile" Icon="{x:Static arc:ArcIcons.User}" />
</arcm:Tabbar>
```

选中的项使用前景色，其余使用弱化的文字色。手指按在某一项上时它会变暗；没有悬停状态。

### Token

`TabbarToken` 保存栏的几何：`BarHeight`（49）和 `BarBorderThickness`（上方 1 px 的细线）。`TabbarItemToken` 保存每一项的几何：`TouchTarget`（44）、`IconExtent`（24）、`IconGap`（2）、`PressedOpacity`（0.6）和 `ItemPadding`（4）。颜色来自 ArcDesign token。

## API 参考

<!-- api: MobileTabbar -->

<!-- api: MobileTabbarItem -->

## 无障碍

各项是带文字的可选择列表项，以其内容命名。保留文字，图标就不会是唯一的名称。每一项至少 44 px 高。

## 动效

一个强调色胶囊在各项之间沿弹簧滑动（P7，`GlidingIndicator`）：第一次显示时在落点原地淡入，栏尺寸变化时直接吸附而不滑动，减少动效时直接跳到目标。图标和文字颜色按快速曲线变化。手指按下时该项变暗，内容用快速弹簧缩小到 0.96（P1）。减少动效时不缩放，一切立即切换。

## 相关组件

- `MobilePage`：把标签栏放进它的 `Footer`。
- `MobileNavbar`：顶部栏。
