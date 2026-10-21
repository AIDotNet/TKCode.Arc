---
title: 图标变形
summary: 在不同状态之间变形的图标：菜单变关闭、播放变暂停、复制变勾选等。
source-hash: e691e73b8fa1
---

<Demo name="EightPairs" />

## 何时使用

- 按钮的图标在每个状态下含义不同，例如菜单变为关闭图标。
- 播放与暂停、主题、通知或点赞的切换，希望用户看到图标的变形过程。
- 展开控件的箭头会在分区展开时转动。

## 何时不用

- 带确认的复制操作：使用 `CopyButton`，它本身就会变形图标。
- 带文字的明暗切换：使用 `ThemeSwitcher`。
- 状态永不改变的图标：使用普通的 Lucide 图标即可。
- 单独将图标作为控件：`IconMorph` 仅起装饰作用，应放在 `Button` 或其他带名称和状态的控件内部。

## 用法

`IconMorph` 提供八组图标对。`Kind` 选择图标对，`IsActive` 选择第二个状态。将它放入按钮中，并为按钮设置名称。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:Button Variant="Ghost" AutomationProperties.Name="打开菜单">
        <arc:IconMorph Kind="Menu" />
    </arc:Button>
</UserControl>
```

### 图标类型

`Menu` 变为关闭，`Play` 变为暂停，`Plus` 变为勾选，`Copy` 变为勾选，`Theme` 变为月亮，`Chevron` 转动，`Bell` 在开启与关闭通知之间切换，`Heart` 表示点赞。

```xml
<arc:IconMorph Kind="Play" />
<arc:IconMorph Kind="Theme" IsActive="True" />
<arc:IconMorph Kind="Heart" IsActive="{Binding IsLiked}" />
```

### 切换状态

将 `IsActive` 绑定到按钮的状态。在点击处理程序中翻转它，并同步更新按钮的名称，使其与新含义一致。

```csharp
var icon = new IconMorph { Kind = IconMorphKind.Play };
var button = new Button { Content = icon, Variant = ButtonVariant.Ghost };
button.Click += (_, _) =>
{
    icon.IsActive = !icon.IsActive;
    AutomationProperties.SetName(button, icon.IsActive ? "暂停" : "播放");
};
```

### 尺寸与线宽

`IconSize` 设置渲染尺寸，单位为像素，默认值为 24。`StrokeThickness` 在 24 单位的网格上设置，并随 `IconSize` 等比缩放，因此默认线宽 1.75 在任何尺寸下都保持比例。

<Demo name="Sizes" />

```xml
<arc:IconMorph Kind="Menu" IconSize="16" />
<arc:IconMorph Kind="Menu" IconSize="32" StrokeThickness="2" />
```

### 箭头方向

对于 `Chevron`，`ChevronPointing` 设置静止时的方向，`ChevronTurn` 设置激活时转动的角度（单位为度）。值为 180 时由下向上翻转，值为 90 时由右向下转动。

<Demo name="ChevronAndMoments" />

### MorphGlyph

`MorphGlyph` 是路径变形基础组件，可作用于任意路径数据。从菜单到关闭会保留顶点，从关闭到对勾则按弧长重新采样。

<Demo name="MorphGlyph" />

## API 参考

<!-- api: IconMorph -->

## 无障碍

- 图标对辅助技术隐藏，名称由外围的按钮提供。
- 在按钮上体现状态：切换按钮使用按下状态，展开控件使用展开状态，或更改名称。
- 含义改变时更改名称，例如从播放改为暂停。
- 图标不接收焦点，也不处理按键，由外围的按钮负责。

## 动效

- 线条旋转并滑动到位，轮廓在两种形状之间插值。快速切换时，图标会从当前位置反向变形。
- 复制图标会收起后方的纸张并绘制一个勾。太阳会变成带月牙缺口的月亮。
- 铃铛开启时响一次，心形每次点赞时都会弹出，并伴随光环与火花。
- 减弱动效时，所有变形立即完成，并跳过光环与火花。关闭动效时同样如此。

## 相关组件

- `CopyButton`：带有自身确认的复制操作。
- `ThemeSwitcher`：带文字标签的明暗切换。
- `Button`：承载图标变形名称与状态的控件。
- `ExpandingButtonGroup`：展开后显示文字标签的图标操作。
