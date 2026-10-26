---
title: 移动端开关
summary: 51 乘 31 的开关，用于立即生效的设置，滑块带弹簧动效，点按区域至少 44 px。
source-hash: e0ab549e3f9a
---

<Demo name="Settings" />

## 何时使用

`TKCode.Arc.Mobile` 的 `Switch` 用来打开或关闭一项设置，更改立即生效。它是 51 乘 31 的滑轨，带 27 px 的滑块，滑块沿弹簧滑动，点按区域至少 44 乘 44。没有悬停状态。

- **设置列表**：每行一项设置，标签在左，开关在末端。
- **立即生效的偏好**：通知、同步、深色外观。

实时示例运行在手机框中，这是桌面上的模拟。Android 和 iOS 真机上的触控表现尚未验证。

## 何时不用

- **桌面设置页**：使用 `TKCode.Arc` 的 `Switch`。
- **需要用按钮确认的选择**：使用复选框或表单。
- **一个动作**：使用 `MobileButton`；开关表示的是状态。

## 用法

开关派生自 Avalonia 的 `ToggleButton`，所以 `IsChecked`、`IsCheckedChanged` 和 `Content` 照常使用。它位于 XAML 命名空间 `https://github.com/AIDotNet/TKCode.Arc.Mobile`（前缀 `arcm`）；桌面包里也有 `Switch`，所以 API 表把它称为 `MobileSwitch`。标签放在你自己的行里，开关放在行的末端。

```xml
<Grid xmlns="https://github.com/avaloniaui"
      xmlns:arcm="https://github.com/AIDotNet/TKCode.Arc.Mobile"
      ColumnDefinitions="*,Auto" MinHeight="52">
    <TextBlock Text="Notifications" VerticalAlignment="Center" />
    <arcm:Switch Grid.Column="1" IsChecked="True" />
</Grid>
```

选中时滑轨填充为开启色。禁用的开关变暗到 0.5，并保持其状态。

### Token

`SwitchToken` 保存几何：`TrackWidth`（51）、`TrackHeight`（31）、`TrackInset`（2）、`ThumbSize`（27）、`ThumbOnMargin`、`TouchTarget`（44）和 `DisabledOpacity`（0.5）。颜色来自 ArcDesign 的选择控件 token。

## API 参考

<!-- api: MobileSwitch -->

## 无障碍

它是切换按钮：可以获得焦点并用键盘切换，并暴露其状态。把设置的名称放在旁边的标签里，并让这一行至少 44 px 高。

## 动效

滑块沿弹簧滑动，滑轨渐变到开启色。手指按下时，滑块用快速弹簧朝目标方向拉伸到 1.18 倍宽，松开时用同一个弹簧回弹（P24）。这些都在控件主题中声明；减少动效时滑块直接跳转、不拉伸，颜色立即切换。

## 相关组件

- `MobileButton`：用于动作。
- `MobilePage`：设置列表所在的屏幕。
