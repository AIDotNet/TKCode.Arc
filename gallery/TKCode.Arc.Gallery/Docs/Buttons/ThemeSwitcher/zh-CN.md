---
title: 主题切换
summary: 在亮色与暗色外观之间切换的四种平滑方式。
source-hash: a1ed29b5c9c0
---

## 何时使用

- 页眉或工具栏中单一的亮色与暗色切换。
- 页面过渡需要与布局相配的切换：从按钮处扩散的圆形、日食式、中线接缝或上升式。
- 只负责发出请求、由应用自行应用主题的切换。

## 何时不用

- 主题选项包含“跟随系统”，或位于账户菜单中：使用 `UserMenu`。
- 不改变主题的普通操作按钮：使用 `Button`。

## 用法

`ThemeSwitcher` 显示下一次按下后将切换到的外观。如果未设置 `Appearance`，它会跟随窗口当前的实际主题变体。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:ThemeSwitcher Variant="Reveal" />
</UserControl>
```

### 变体

`Variant` 选择页面过渡方式。`Reveal` 从按钮处以圆形向外扩散新外观。`Eclipse` 像日食一样掠过页面，适合展示页面。`Split` 从中线接缝处展开，不依赖按钮的位置。`Rise` 从底部升起，最为安静。

```xml
<arc:ThemeSwitcher Variant="Eclipse" />
<arc:ThemeSwitcher Variant="Split" />
<arc:ThemeSwitcher Variant="Rise" IsIconOnly="True" />
```

### 仅图标与文字

`IsIconOnly` 显示 38 乘 38 的方形按钮。`Text` 设置图标旁可见的文字，`Label` 设置无障碍名称。未设置 `Label` 时，名称会说明下一次按下将切换到哪种外观。

```xml
<arc:ThemeSwitcher Text="外观" Label="切换外观" />
```

### 处理切换请求

主题变化前会触发 `ThemeChangeRequested`。其参数包含下一个外观、主题变体、触发的按钮以及扩散的起点。设置 `Handled` 后由你自行应用主题，切换控件不会再次应用。

```csharp
switcher.ThemeChangeRequested += (_, e) =>
{
    e.Handled = true;
    switcher.Appearance = e.Next;
    ApplyAppearance(e.Next);
};
```

### 受控外观

`Appearance` 为当前显示的外观，决定图标、按下状态与名称。在代码中设置它，可使切换控件与应用保持同步。

```xml
<arc:ThemeSwitcher Appearance="Light" Variant="Rise" />
```

```csharp
switcher.Toggle();
```

## API 参考

<!-- api: ThemeSwitcher -->

## 无障碍

- 切换控件是一个切换按钮。处于暗色外观时它报告为开启，其名称说明下一个外观。
- 太阳与月亮图标对辅助技术隐藏。
- 按 Enter 或 Space 切换主题。
- 键盘焦点显示标准的焦点环，与所有 Arc 按钮一致。

## 动效

- 太阳与月亮沿同一路径相互转动、缩放，旧图标迅速离开。
- 暗色模式下图标会沿其变体的方向轻微移动：`Reveal` 使其放大，`Eclipse` 与 `Rise` 使其略微移动。
- 页面过渡按变体揭示新外观。旧窗口保留为快照，新外观切入其上方。
- 首次设置外观时不产生动画。
- 减弱动效时移除图标的轻微移动与页面过渡，图标立即淡入。关闭动效时立即切换。

## 相关组件

- `Button`：不改变主题的普通操作。
- `UserMenu`：包含“跟随系统”并位于账户菜单中的主题选择。
- `SegmentedControl`：在几个具名选项之间选择并保持选中状态。
- `IconMorph`：在两种状态之间变形的独立图标。
