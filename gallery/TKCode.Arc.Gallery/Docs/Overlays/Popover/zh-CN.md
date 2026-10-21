---
title: 弹出框
summary: 用于提供上下文信息的小型定位浮层。
source-hash: 05cbf1a4a141
---

<Demo name="Share" />

## 何时使用

- 不应阻塞页面的互动内容，例如分享设置或小型筛选表单。
- 由用户点击按钮或其他控件后打开的内容。
- 通过代码使用 `ShowAt` 锚定到任意控件的内容。

## 何时不用

- 悬停或聚焦时的简短文字标签：用 `Tooltip`。
- 人员或链接的只读预览：用 `HoverCard`。
- 用户继续前必须作出的决定：用 `Dialog`。
- 命令列表：用 `DropdownMenu`。

## 用法

`Popover` 锚定在触发器上，默认为非模态，因此页面其余部分仍可操作。

### 触发器与内容

将 `Trigger` 设为打开面板的控件。弹出框的内容即为面板。

```xml
<arc:Popover>
    <arc:Popover.Trigger>
        <arc:Button Variant="Secondary" Content="分享" />
    </arc:Popover.Trigger>
    <StackPanel Spacing="8">
        <TextBlock Text="分享链接" FontWeight="Medium" />
        <TextBlock Text="任何拥有链接的人都可以查看。" />
    </StackPanel>
</arc:Popover>
```

### 附加到控件

`arc:Popover.Attach` 可以为任意控件添加弹出框。点击该控件会切换面板的显示状态。

```xml
<arc:Button Content="筛选">
    <arc:Popover.Attach>
        <arc:Popover Placement="Top">
            <TextBlock Text="在顶部一侧打开。" />
        </arc:Popover>
    </arc:Popover.Attach>
</arc:Button>
```

### 位置与对齐

`Placement` 为首选一侧：`Top`、`Bottom`、`Left` 或 `Right`。空间不足时面板会翻转。`Alignment` 决定沿触发器边缘的位置：`Start`、`Center` 或 `End`。`Offset` 为与触发器之间的间距，单位为像素，默认值为 6。

<Demo name="Sides" />

```xml
<arc:Popover Placement="Bottom" Alignment="Center" Offset="8">
    <arc:Popover.Trigger>
        <arc:Button Variant="Ghost" Content="筛选" />
    </arc:Popover.Trigger>
    <TextBlock Text="状态：任意" />
</arc:Popover>
```

### 从代码中打开

`ShowAt` 将弹出框锚定到任意控件。`IsOpen` 是双向的，`Opened` 与 `Closed` 会报告状态变化。

<Demo name="FromCode" />

### 模态弹出框

将 `IsModal` 设为 true 后，面板外的按压会被拦截，且只会关闭弹出框。焦点被限制在面板内。

```xml
<arc:Popover IsOpen="{Binding ShowPicker}" IsModal="True">
    <TextBlock Text="请先选择一个选项。" />
</arc:Popover>
```

### 小型表单

弹出框可以放置小型表单，例如筛选条件。打开时页面仍可操作。

<Demo name="SmallForm" />

## API 参考

<!-- api: Popover -->

## 无障碍

- 在触发器上按 Enter 或空格会打开弹出框。按 Escape 关闭，并将焦点返回触发器。
- 面板打开时焦点移入其中。面板外按压会关闭弹出框，并将焦点返回触发器。
- 当内容没有标题时，请为弹出框设置 `AutomationProperties.Name`。该名称用于标注面板。
- 非模态弹出框不限制焦点。将 `IsModal` 设为 true 会将焦点限制在面板内。
- 触发器会公开弹出框是否已展开。

## 动效

- 面板以缩放 0.97 从朝向触发器的 5px 处开始，快速淡入，并以弹簧从落定的一侧就位。
- 关闭约需 160ms，移动距离为一半。开启与关闭在动画中途可以相互逆转。
- 触发器按下时改变颜色，但不缩放，因为面板是根据它测量的。
- 减弱动效时移除移动与缩放，仅保留短暂淡入。关闭动效时面板立即显示或移除。

## 相关组件

- `Tooltip` 用于悬停或聚焦时的单行标签。
- `HoverCard` 用于悬停时打开的只读预览。
- `Dialog` 用于必须先处理才能继续使用页面的内容。
- `DropdownMenu` 用于命令列表。
