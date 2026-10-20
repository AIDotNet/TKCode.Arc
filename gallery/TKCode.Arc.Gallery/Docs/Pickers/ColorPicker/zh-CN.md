---
title: 颜色选择器
summary: 色块展开为选择器，支持格式切换、吸管、已保存色块和对比度读数。
source-hash: d1062b7bf35e
---

## 何时使用

- 主题编辑器、品牌设置或设计工具中，需要以 HEX、RGB、HSL 或 OKLCH 输入颜色的场景。
- 带透明度的颜色，其中 Alpha 通道是值的一部分。
- 需要与背景对比检查、并显示 WCAG 对比度读数的颜色。
- 用户逐步积累的一小组已保存颜色。

## 何时不用

- 少量固定颜色：用 `SegmentedControl` 或 `RadioGroup`。
- 没有选择器的普通 HEX 文本值：用 `Input`。
- 用于筛选列表的小型预设颜色标签：用 `ChipGroup`。

## 用法

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:ColorPicker Label="强调色"
                     Color="#2F6BFF"
                     Format="Hex" />
</UserControl>
```

### 值与格式

`Color` 是 Avalonia 的 `Color`，且为双向绑定。`Format` 设置文本框中显示的格式，用户也可以在面板中切换。`HexValue` 为只读，以 CSS 使用的顺序给出 `#RRGGBB` 或 `#RRGGBBAA` 形式的颜色。

```xml
<arc:ColorPicker Label="品牌色" Color="#E5484D" Format="Oklch" />
```

```csharp
picker.ColorChanged += (_, e) => status.Text = $"颜色：{e.Hex}";
```

### 已保存色块

`Swatches` 是一组 `ColorSwatch` 记录。色块从面板中保存，每次变更都会生成新的列表，因此请绑定集合或处理 `SwatchesChanged`。`MaxSwatches` 默认为 7；列表已满时，最早的色块会被移除。

```csharp
picker.Swatches =
[
    ColorSwatch.Create(Color.Parse("#E5484D")),
    ColorSwatch.Create(Color.Parse("#30A46C")),
    ColorSwatch.Create(Color.Parse("#0090FF")),
];
picker.MaxSwatches = 7;
```

### 对比度读数

`ContrastBackground` 是 WCAG 读数用来与当前值比较的颜色。面板会显示对比度比值与等级。

```xml
<arc:ColorPicker Label="深色背景上的文字"
                 Color="#A1A1AA"
                 ContrastBackground="#111113"
                 Description="对比度读数基于 #111113 进行比较。" />
```

### 错误与禁用状态

`ErrorMessage` 非空时字段被标记为无效。`IsEnabled="False"` 会禁用选择器。

```xml
<arc:ColorPicker Label="已锁定" IsEnabled="False" />
```

## API 参考

<!-- api: ColorPicker -->

<!-- api: ColorSwatch -->

## 无障碍

- `Label` 为触发器命名，面板以该标签命名。触发器会公开其展开状态。
- 饱和度与明度区域是一个二维滑块，其值同时报告饱和度与明度。色相与不透明度滑块分别报告角度与百分比。
- 方向键移动手柄，按住 Shift 或使用 PageUp 与 PageDown 则以十为步长移动。Home 与 End 分别移到最小值与最大值。
- 在文本框中，Enter 应用该值。无效值会使字段抖动，并随错误信息一并播报。Escape 先放弃草稿，再次按下 Escape 则关闭面板并将焦点返回触发器。
- 对比度比值及其等级以文字形式提供。已保存色块组成一个列表，其中 Delete 删除选中的色块，Alt 加方向键可移动它。

## 动效

- 面板从色块中展开：裁剪区域从触发器的角落生长为完整面板，阴影随裁剪区域变化。
- 手柄以快速弹簧跟随指针，拖动时会收紧。
- 格式按钮使文字在 HEX、RGB、HSL 与 OKLCH 之间变形。对比度比值计数到新的数值。
- 无效输入会使字段抖动。新色块弹出，被移动的色块落定到新位置。
- 减弱动效时手柄直接跳转，面板以淡入代替裁剪展开，并取消格式变形与抖动。关闭动效时直接到达终态。

## 相关组件

- `SegmentedControl` 或 `RadioGroup` 用于少量固定颜色。
- `Input` 用于普通文本值。
- `ChipGroup` 用于小型预设标签。
