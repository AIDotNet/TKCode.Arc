---
title: Dot grid
summary: 一片点阵，像透镜一样在指针周围向外鼓起，每次点击都会向外扩散一圈涟漪。
source-hash: d97edb6e6314
---

<Demo name="Preview" />

## 何时使用

`DotGrid` 用一片安静的点阵铺满一块区域，并对指针做出反应。指针周围的点像透镜一样向外鼓起，并染上强调色；点击或轻触会让一圈波动扫过整片点阵。放在里面的内容浮在点阵之上，照常可用。

- **落地页首屏**：不用视频或厚重插图，也能让标题有生气。
- **登录面板、空状态和倒计时**：给等待中的人一个可以把玩的背景。
- **页面分隔**：在长页面的两部分之间放一条让人想戳一下的带子。

## 何时不用

- **密集的文字、表格或图表背后**：移动的点会干扰阅读。只在标题和少量操作背后使用。
- **不需要任何反应时**：用画刷画一张静态的点阵图案开销更小。
- **同一屏放好几个**：一个视图里有一个会反应的背景就够了。

## 用法

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:DotGrid Height="420">
        <TextBlock Text="Built to be poked at" HorizontalAlignment="Center" VerticalAlignment="Center" />
    </arc:DotGrid>
</UserControl>
```

`DotGrid` 是 Special 包中的 `ContentControl`。在 `UseArc()` 旁边调用 `UseArcSpecial()` 注册该包。给控件一个高度，或让内容撑开它；最小高度为 240px。点阵本身没有背景：设置 `Background`、`BorderBrush`、`BorderThickness` 和 `CornerRadius` 给它加上边框，点会被裁剪在这个圆角框内。

### 用作首屏

把标题和操作放在里面。点阵在整个控件范围内监听指针，内容上方也一样，所以人们移向按钮时透镜仍会跟随。

<Demo name="Hero" />

### 密度、透镜与颜色

`Gap` 是点与点的间距（默认 22px，最小 6）。数值越小点越密：720 × 420 的区域在默认间距下约有 660 个点，间距为 14 时约有 1,600 个。`DotSize` 是静止时点的半径。`Radius` 是透镜的作用范围，`Strength` 是透镜在最强处（距指针三分之一半径处）把点推开的距离。`DotBrush` 和 `ActiveDotBrush` 分别设置静止色和点亮色，默认使用强边框色和强调色 Token，因此点阵会跟随主题和强调色变化。

<Demo name="Colors" />

```xml
<arc:DotGrid Height="300"
             Gap="18"
             Radius="200"
             Strength="24"
             IsFadeEnabled="False"
             IsRippleEnabled="False"
             ActiveDotBrush="{arc:ArcDesignTokenResource Series2}" />
```

`IsFadeEnabled` 让点在靠近边缘处逐渐淡出（默认开启），这样点阵不会在一条硬边上戛然而止。`IsRippleEnabled` 用于关闭点击涟漪。

### 用代码触发涟漪

`Ripple(point)` 从控件坐标中的某一点发出一圈涟漪，效果与在那里点击相同。可以用它回应别处发生的事情，例如表单已发送。

```csharp
field.Ripple(new Point(field.Bounds.Width / 2, field.Bounds.Height / 2));
```

### 最佳实践

**建议**

- 内容保持简短并居中：一个标题、一句话和一两个操作。
- 全宽首屏和性能较弱的设备上使用更大的 `Gap`（26 到 32）。
- 用 `ActiveDotBrush` 而不是静止色来贴合品牌，颜色由被点亮的点来呈现。

**避免**

- 在很大的区域上使用很小的间距。每个点都要模拟，开销随点数增长。
- `Strength` 远大于间距：点会相互穿过，透镜失去形状。

### 常见问题

**里面的按钮还能用吗？** 能。点阵画在内容下方，从不抢走指针，只是观察指针经过。

**为什么点不动？** 减弱动效时每个点都保持原位，只点亮指针附近的点。关闭动效时点阵完全静止。

## API 参考

<!-- api: DotGrid -->

## 无障碍

- 点阵只是装饰。它不可聚焦，也不出现在自动化树中；内容保留自己的名称和角色，并留在阅读顺序中。
- 只有当这块区域本身有含义时才设置 `AutomationProperties.Name`，此时它会被读作一个分组。
- 减弱动效时点从不移动。透镜只点亮指针附近的点，点击也不会产生涟漪。

## 动效

- 每个点都沿着自己的轻阻尼弹簧移向透镜和涟漪要求的偏移，所以点阵会轻轻晃动后静止，而不是突然跳变。
- 透镜以很短的滞后跟随指针，指针到来时淡入、离开时淡出。它的推力在距指针三分之一半径处最强，到边缘处降为零；被它点亮的点会变大并换成点亮色。
- 涟漪以每秒 620px 的速度向外扩散，在 1.5 秒内淡出。最多六圈涟漪可以叠加，第七圈会替换最早的一圈。
- 触屏上只有手指按下时才出现透镜，每次轻触都会产生涟漪。
- 只有在有东西移动时才请求帧，点阵滚出视图时暂停。

| 变化 | 完整 | 减弱 | 关闭 |
|---|---|---|---|
| 透镜 | 推开并点亮点 | 只点亮点 | 无 |
| 涟漪 | 扫过整片点阵 | 无 | 无 |

## 相关组件

- `GravityWell` 让一格格图块（而不是点阵）围绕指针弯曲。
- `InkFluid` 用随指针搅动的墨水填满首屏。

| 组件 | 适用场景 |
|---|---|
| `DotGrid` | 想要一个带透镜的轻量几何背景 |
| `GravityWell` | 网格本身就是内容：照片、头像或标志 |
| `InkFluid` | 想要一个柔和、有机的背景 |
