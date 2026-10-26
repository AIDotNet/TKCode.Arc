---
title: 移动端按钮
summary: 触控优先的按钮：用按压状态代替悬停状态，最小触控目标 44 px。
source-hash: 19fcda420f7e
---

<Demo name="Basic" />

## 何时使用

`TKCode.Arc.Mobile` 的 `Button` 是给触屏用的按钮。它是填充样式，至少 44 乘 44，并有按压状态：手指按在上面时，按钮缩小到 0.97 并变暗到 0.85。没有悬停状态，因为手指不会悬停。

- **屏幕的主要操作**：内容底部通栏的"继续"或"保存"。
- **导航栏槽里的操作**："编辑"或"完成"。
- **表单的提交按钮**。

实时示例运行在手机框中，这是桌面上的模拟。用鼠标按住按钮即可看到按压状态。手指在 Android 和 iOS 上的手感尚未验证。

## 何时不用

- **桌面界面**：使用 `TKCode.Arc` 的 `Button`，它有变体、尺寸和悬停状态。
- **保持选中的选择**：设置用 `MobileSwitch`，多个选项用列表。

## 用法

移动端的 `Button` 位于 XAML 命名空间 `https://github.com/AIDotNet/TKCode.Arc.Mobile`（前缀 `arcm`）和 .NET 命名空间 `TKCode.Arc.Mobile`，因此不会与桌面的 `Button` 冲突；出于同样的原因，本页的 API 表把它称为 `MobileButton`。它派生自 Avalonia 的 `Button`，所以 `Content`、`Command` 和 `Click` 照常使用。

```xml
<StackPanel xmlns="https://github.com/avaloniaui"
            xmlns:arcm="https://github.com/AIDotNet/TKCode.Arc.Mobile"
            Spacing="12">
    <arcm:Button Content="Continue" HorizontalAlignment="Stretch" />
    <arcm:Button Content="Not available" IsEnabled="False" />
</StackPanel>
```

禁用的按钮变暗到 0.5。

### 变体

`Variant` 决定按钮的显著程度：`Filled`（默认，实心胶囊，用于主操作）、`Tinted`（浅强调色胶囊，用于次要操作）和 `Plain`（强调色文字、无背景，用于栏上的操作）。每个页面只放一个 `Filled` 按钮。

```xml
<arcm:Button Variant="Tinted" Content="更多" />
<arcm:Button Variant="Plain" Content="完成" />
```

### Token

`ButtonToken` 保存几何：`TouchTarget`（44）、`ContentPadding`（16 乘 10）、`PressedScale`（0.97）、`PressedOpacity`（0.85）和 `DisabledOpacity`（0.5）。颜色、圆角和字体来自 ArcDesign token，因此按钮跟随明暗、强调色和 Pixel 风格。

## API 参考

<!-- api: MobileButton -->

## 无障碍

它是普通的 Avalonia 按钮：可以获得焦点并用键盘触发，并以内容作为名称。44 px 的最小尺寸让目标容易点中。

## 动效

按压的缩放和变暗在控件主题中声明，减少动效时立即切换。

## 相关组件

- `MobileSwitch`：设置用的开关。
- `MobileNavbar`：按钮放在它的槽里。
- `MobilePage`：按钮所在的屏幕。
