---
title: 抽屉
summary: 用于专注工作的临时侧边面板。
source-hash: 55d20e5dba67
---

<Demo name="Sides" />

## 何时使用

- 筛选、设置或记录详情，需要让用户保持在当前页面语境中。
- 对 `Dialog` 来说过长、又不应离开当前视图的表单。
- 用户可能通过将标题栏拖向边缘来关闭的面板。

## 何时不用

- 简短的决定与确认：用 `Dialog`。
- 以移动端为主、需停靠在数个高度的面板：用 `BottomSheet`。
- 依附于某个控件的小型内容，且不需要遮罩：用 `Popover`。

## 用法

`Drawer` 是一种从某一边缘滑入的模态面板。可以在任意位置声明它，并绑定 `IsOpen`。

### 边缘

`Side` 决定面板从哪一边缘滑入：`Left`、`Right`、`Top` 或 `Bottom`。默认值为 `Right`。

```xml
<arc:Drawer Title="筛选"
            Description="在不离开列表的情况下缩小范围。"
            Side="Left"
            IsOpen="{Binding ShowFilters}">
    <StackPanel Spacing="16">
        <CheckBox Content="仅未结发票" />
        <CheckBox Content="包含草稿" />
    </StackPanel>
</arc:Drawer>
```

### 长内容

标题保持固定，主体区域可滚动。将完整内容放入抽屉即可，无需设置尺寸。

<Demo name="LongContent" />

```xml
<arc:Drawer Title="INV-2041" Description="由 Ada Lovelace 于 3 月 12 日支付。" IsOpen="{Binding ShowRecord}">
    <ItemsControl ItemsSource="{Binding LineItems}" />
</arc:Drawer>
```

### 操作

将按钮放在 `Actions` 中。带有 `arc:Dialog.Result` 的按钮会以该值关闭抽屉，与 `Dialog` 中的行为相同。

```xml
<arc:Drawer Title="筛选" IsOpen="{Binding ShowFilters}">
    <arc:Drawer.Actions>
        <StackPanel Orientation="Horizontal" Spacing="8">
            <arc:Button Variant="Secondary" Content="重置" arc:Dialog.Result="{x:False}" />
            <arc:Button Content="应用" arc:Dialog.Result="{x:True}" />
        </StackPanel>
    </arc:Drawer.Actions>
</arc:Drawer>
```

### 从代码中打开

当同一个抽屉服务于多个边缘时，应同时设置 `Side` 与 `IsOpen`。`OpenCommand` 可以在不设置 `IsOpen` 的情况下，由按钮打开抽屉。

```csharp
drawer.Side = DrawerSide.Bottom;
drawer.IsOpen = true;

openDetails.Command = details.OpenCommand;
```

### 关闭原因

`Closed` 会报告抽屉关闭的原因：`CloseButton`、`Escape`、`PressOutside`、`Gesture`、`Action` 或 `Programmatic`。

```csharp
drawer.Closed += (_, e) => log.Text = $"关闭方式：{e.Reason}";
```

## API 参考

<!-- api: Drawer -->

## 无障碍

- 面板名称取自 `Title`，帮助文本取自 `Description`。
- 抽屉打开时焦点移入其中，Tab 与 Shift+Tab 均停留在抽屉内，关闭后焦点返回打开它的控件。
- 按 Escape 关闭抽屉。关闭按钮始终存在，默认名称为“关闭抽屉”。
- 拖动是可选操作。减弱动效时拖动功能关闭，因此关闭操作不依赖手势。

## 动效

- 遮罩淡入的同时，面板以平滑弹簧从其边缘滑入。
- 拖动标题栏朝边缘移动时面板跟随指针。超过面板的三分之一，或以快于 500px/s 的速度轻扫时，抽屉关闭；否则弹回。
- 点击、按 Escape 或按下遮罩会以短暂的补间动画关闭抽屉，阴影在结束时淡出。
- 减弱动效时关闭拖动功能，滑动改为短暂淡入。关闭动效时抽屉立即显示或移除。

## 相关组件

- `Dialog` 用于必须打断页面的决定。
- `BottomSheet` 用于以移动端为主、需停靠在数个高度并可展开的面板。
- `Popover` 用于依附于某个控件的小型互动内容。
