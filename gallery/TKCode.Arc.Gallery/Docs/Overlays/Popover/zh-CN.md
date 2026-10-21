---
title: 弹出框
summary: 用于提供上下文信息的小型定位浮层。
source-hash: 144bfe7cbde0
---

<Demo name="Share" />

## 何时使用

`Popover` 在打开它的控件旁边承载一小块可交互的内容：分享设置、列表工具栏上的筛选表单，或由你自己的控件组成的选择器。它锚定在触发器上，因此阅读者能保持页面的上下文；它默认是非模态的，因此打开时页面其余部分仍可操作。

- **文档或记录的分享与访问设置。** 文档页眉中的“分享”按钮会打开一个面板，包含链接、复制控件和权限选择，无需带阅读者跳转到设置页。
- **列表工具栏上的筛选。** 订单或发票列表上的“状态”与日期范围筛选，从“筛选”按钮打开，应用其值后关闭，因此阅读者调整条件时表格仍然可见。
- **数据表格中的列或视图选项。** “列”按钮把可选列列为复选框，阅读者无需离开表格即可显示或隐藏字段。
- **画布或图表中由代码驱动的面板。** 应用在选中某个节点时调用 `ShowAt` 并传入该节点的控件，在旁边打开一个小型编辑器，面板随选中项移动。

## 何时不用

- **悬停或聚焦时的单行标签：** 用 `Tooltip`。弹出框由点击打开并承载控件；工具提示是只读文字，在悬停时出现。
- **人员或链接的只读预览：** 用 `HoverCard`，它在悬停时打开，且从不获得焦点。
- **用户继续之前必须作出回答的决定：** 用 `Dialog`。点击弹出框外部会将其关闭，因此它无法承载“删除工作区”这类确认操作。
- **仅有命令列表：** 用 `DropdownMenu`，它为操作提供方向键导航和菜单语义。
- **长表单或多步骤流程：** 使用页面、`Drawer` 或 `Dialog`。弹出框的尺寸只适合几个字段；字段很多的表单会把下面的页面挤到屏幕之外。

## 用法

`Popover` 是一个 `ContentControl`。它的 `Content` 是面板，`Trigger` 是打开面板的控件。面板显示在承载于弹层中的 `PopoverSurface` 里，因此它不参与布局，并且可以超出触发器的边缘。触发器与面板相互关联：面板依据触发器测量位置，因此触发器只以颜色响应按压，不缩放。

### 结构

1. **触发器**（`Trigger`）：打开面板的控件。它以自身尺寸保留在页面中；弹出框不会在它周围添加任何布局。
2. **面板表面**：`PopoverSurface`，使用浮起表面色（`SurfaceRaised`）、前景文字色、1 px 的边框（颜色为边框色 `Border`）以及面板圆角（`RadiusPanel`）。四周各有 16 px 的内边距，宽度至少 192 px（12 rem），最多 352 px（22 rem），使用正文字体的小号字级（`TypeSM`）。浮起阴影由弹层绘制。
3. **内容**（`Content`）：面板的内容。字符串会在表面的默认文字模板中以自动换行的文字显示；其他任何控件则原样显示。
4. **间距**（`Offset`）：默认情况下触发器与面板之间为 6 px，面板的入场动效从此距离开始。

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

激活触发器会切换弹出框：点击、轻触或通过键盘激活按钮都会打开它，同一操作会再次将其关闭。非按钮触发器则在按下 `Enter` 或 `Space` 时打开弹出框。

### 附加到控件

`arc:Popover.Attach` 可以为任意控件添加弹出框。点击该控件会切换面板。当控件本身就是阅读者预期的按钮、面板无需单独的触发器时使用它。

```xml
<arc:Button Content="筛选">
    <arc:Popover.Attach>
        <arc:Popover Placement="Top">
            <TextBlock Text="在顶部一侧打开。" />
        </arc:Popover>
    </arc:Popover.Attach>
</arc:Button>
```

附加的弹出框打开时会加入其目标的逻辑树，因此目标的样式和 `DataContext` 会在面板内生效。关闭时它会离开该树。

### 位置与对齐

`Placement` 是首选的一侧：`Top`、`Bottom`（默认）、`Left` 或 `Right`。当首选一侧在窗口中放不下时，面板会翻转到另一侧，其打开动效跟随它实际落定的那一侧。`Alignment` 决定面板沿触发器边缘的位置：`Start`（默认）、`Center` 或 `End`。`Offset` 是与触发器之间的间距，单位为像素，默认为 6。弹出框打开时修改 `Placement`、`Alignment` 或 `Offset`，面板会立即移动。

<Demo name="Sides" />

```xml
<arc:Popover Placement="Bottom" Alignment="Center" Offset="8">
    <arc:Popover.Trigger>
        <arc:Button Variant="Ghost" Content="筛选" />
    </arc:Popover.Trigger>
    <TextBlock Text="状态：任意" />
</arc:Popover>
```

对于列表左侧的工具栏按钮，使用 `Bottom` 配合 `Start`，让面板与按钮的左边缘对齐。对于一行中间的控件使用 `Center`，对于页面右边缘的按钮使用 `End`，这样面板不会超出窗口。默认的 6 px 间距是 Arc 的外观。

### 从代码中打开

`ShowAt` 将弹出框锚定到窗口中的任意控件，因此同一个弹出框可以服务于多个触发器，或用户选中的某个节点。`IsOpen` 是双向属性，`Opened` 与 `Closed` 会报告状态变化。弹出框打开时以另一个锚点调用 `ShowAt`，会先将其关闭，再在新锚点处重新打开。`Close` 从代码中关闭它，`OpenCommand` 与 `CloseCommand` 则以 `ICommand` 的形式提供同样两个操作，供按钮与绑定使用。

<Demo name="FromCode" />

```csharp
share.ShowAt(shareButton);
share.Closed += (_, e) => status.Text = $"已关闭：{e.Reason}";
```

`Closed` 携带一个 `Reason`：阅读者按下 Escape 时为 `Escape`，在面板外按下时为 `PressOutside`，触发器切换、代码调用 `Close` 或值被设为 false 时为 `Programmatic`。

### 模态弹出框

将 `IsModal` 设为 true 后，面板外的按压会被拦截，它们只会关闭弹出框，不会到达下面的控件。面板外的按压被拦截：它会关闭弹出框，且不会到达下面的控件。适用于阅读者在点击其他地方之前必须做出或取消的简短选择；当内容需要确认或自己的遮罩时，使用 `Dialog`。

```xml
<arc:Popover IsOpen="{Binding ShowPicker}" IsModal="True">
    <TextBlock Text="请先选择一个选项。" />
</arc:Popover>
```

### 小型表单

弹出框可以承载小型表单，例如筛选条件。打开时页面仍可操作。

<Demo name="SmallForm" />

### 绑定到视图模型

当视图模型决定面板是否打开时绑定 `IsOpen`，例如工具栏命令应打开一个筛选面板。`IsOpen` 是双向的，因此在面板外按压也会更新视图模型。

```csharp
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

public partial class OrdersViewModel : ObservableObject
{
    [ObservableProperty]
    private bool _isFilterOpen;

    [ObservableProperty]
    private string? _status;

    [RelayCommand]
    private void ApplyFilters()
    {
        IsFilterOpen = false;
        // 按所选状态与日期重新加载订单。
    }
}
```

```xml
<arc:Button Content="筛选" Variant="Ghost">
    <arc:Popover.Attach>
        <arc:Popover IsOpen="{Binding IsFilterOpen}" Placement="Bottom" Alignment="Start">
            <StackPanel Width="240" Spacing="12">
                <TextBlock Text="状态" FontWeight="Medium" />
                <arc:Button Content="应用筛选" Command="{Binding ApplyFiltersCommand}" />
            </StackPanel>
        </arc:Popover>
    </arc:Popover.Attach>
</arc:Button>
```

附加的弹出框通过点击切换，因此阅读者用按钮即可打开和关闭面板。让 `IsOpen` 作为状态的唯一来源，避免同时从绑定和代码中设置它。

### 焦点与关闭

面板打开时焦点会移入其中：移到其中第一个可获得焦点的控件，若没有则移到面板本身。弹出框关闭时，焦点在以下情况下会返回触发器：

- **Escape** 关闭弹出框，并始终将焦点返回触发器。
- **面板外按压**仅当焦点原本位于面板内时才将焦点返回触发器。落在其他控件上的按压会让焦点留在阅读者所点击的地方。
- **从代码关闭**时，如果焦点原本位于面板内，则将焦点返回触发器。

面板会在自身的控件之间循环 `Tab`，因此阅读者不会在未先关闭面板的情况下，通过 Tab 跳出面板进入下面的页面。嵌套在 `Dialog` 中的弹出框同样可以工作：焦点可以进入弹出框，面板外的按压只关闭弹出框，对话框保持打开。

### 示例：订单列表上的筛选面板

后台控制台中位于订单表格上方的工具栏。“筛选”按钮打开一个小表单，包含状态选择和两个日期字段，阅读者可以缩小列表，并在面板背后看到表格更新。面板有一个主操作和一个文字操作，宽 240 px，因此阅读者仍能看到背后的表格。主按钮的文字为“应用筛选”，它是一个带宾语的动词短语，因此阅读者点击前就清楚操作的含义。

```xml
<StackPanel Width="560" Spacing="12">
    <arc:Popover Placement="Bottom" Alignment="Start">
        <arc:Popover.Trigger>
            <arc:Button Variant="Secondary" Content="筛选订单" />
        </arc:Popover.Trigger>
        <StackPanel Width="240" Spacing="12"
                    AutomationProperties.Name="筛选订单">
            <TextBlock Text="状态"
                       FontWeight="Medium"
                       Foreground="{arc:ArcDesignTokenResource Foreground}" />
            <arc:Select Placeholder="任意状态" />
            <TextBlock Text="创建日期"
                       FontWeight="Medium"
                       Foreground="{arc:ArcDesignTokenResource Foreground}" />
            <arc:Input Placeholder="起始日期" />
            <arc:Input Placeholder="截止日期" />
            <StackPanel Orientation="Horizontal" Spacing="8" HorizontalAlignment="Right">
                <arc:Button Content="重置" Variant="Ghost" />
                <arc:Button Content="应用筛选" />
            </StackPanel>
        </StackPanel>
    </arc:Popover>
</StackPanel>
```

### 文案规范

- **触发器文字**：当面板会更改数据时使用动词加宾语（“筛选订单”“分享报告”）；当它只是打开一个视图时，使用设置项的名称（“列”）。单独的“更多”会隐藏面板的作用。
- **面板标题**：句子式的简短名词短语，例如“分享链接”。当内容没有标题时，为弹出框设置 `AutomationProperties.Name`，这样面板在焦点移入时会以名称被播报。
- **面板中的按钮文字**：主操作使用动词加宾语（“应用筛选”“复制链接”），关闭操作使用单纯的动词（“重置”“取消”）。
- **字段标签**：与任何表单一样可见且简短；弹出框不是只有占位文字字段的地方。
- **辅助文字**：用一句话说明设置的效果，例如“任何拥有链接的人都可以查看。”

| 不要写 | 改为 |
|---|---|
| 更多 | 筛选订单 |
| 点击此处分享此文档 | 分享链接 |
| 提交 | 应用筛选 |
| 列显示选项 | 列 |
| 请从下方列表中选择一个状态。 | 状态 |

### 最佳实践

**推荐**

- 面板只包含阅读者一次即可操作的少量控件。两个字段加一个主按钮很合适；更多内容应放在 `Dialog` 或页面中。
- 为每个弹出框提供名称：在面板内放一个标题，或在其上设置 `AutomationProperties.Name`，这样焦点移入面板时它会被播报。
- 把最重要的操作放在面板的第一位，并把关闭操作放在它旁边，靠右对齐。
- 对于位于页面右边缘的触发器使用 `Alignment="End"`，让面板留在窗口之内。
- 在面板中完成一次操作后关闭弹出框，让阅读者在页面上看到结果。

**避免**

- 在弹出框中放置确认或破坏性操作。点击外部会在没有选择的情况下关闭它，阅读者可能丢失输入；任何需要确认的内容请使用 `Dialog`。
- 长表单或可滚动内容。面板的尺寸适合少量内容，过高的面板会遮住打开它的表格或页面。
- 从 `HoverCard` 或 `Tooltip` 触发器打开弹出框。悬停只显示预览；点击应打开一个承载控件的面板。
- 从两个地方同时设置 `IsOpen`，例如绑定与争抢同一状态的 `ShowAt` 调用。
- 把弹出框当作导航菜单。命令列表应使用 `DropdownMenu`，它提供方向键和菜单语义。

### 常见问题

**在弹出框还未进入窗口之前设置 `IsOpen` 为 true 会怎样？** 弹出框会记录这一请求，并在附加到可视树后打开，因此视图模型可以在视图显示之前设置状态。

**为什么按下触发器时它不缩放？** 面板是依据触发器测量的，因此按下时缩放会在面板打开期间移动锚点。用作弹出框触发器的 Arc 按钮只以颜色变化表示按下。

**如何从其中的按钮关闭弹出框？** 在代码中调用 `Close()`，或将按钮绑定到视图模型命令，由它把 `IsOpen` 设为 false。无论哪种方式，焦点原本位于面板内时都会返回触发器。

**在面板内按下会关闭它吗？** 不会。只有 Escape、面板外按压、触发器切换、`Close` 以及把 `IsOpen` 设为 false 才会关闭它。面板内的按压会像平常一样到达其控件。

**能把弹出框附加到非按钮的元素上吗？** 可以。任何控件都可以使用 `arc:Popover.Attach`。点击会切换面板；对于非按钮控件，`Enter` 与 `Space` 可以从键盘打开它。

## API 参考

<!-- api: Popover -->

## 无障碍

- 在触发器上按 Enter 或 Space 会打开弹出框。按 Escape 关闭它，并将焦点返回触发器。
- 面板打开时焦点移入其中。面板外按压会关闭弹出框，并在焦点原本位于其中时将焦点返回触发器。
- 当内容没有标题时，为弹出框设置 `AutomationProperties.Name`。该名称为面板提供标签。
- 非模态弹出框不会限制焦点。面板在自身的控件之间循环 `Tab`，使阅读者在关闭前一直停留在面板内。
- 触发器会公开弹出框是否展开：打开时其项状态为 `Expanded`，关闭时为 `Collapsed`。

### 键盘

| 按键 | 操作 |
|---|---|
| 非按钮触发器上的 `Enter` 或 `Space` | 打开弹出框并将焦点移入面板 |
| 面板内的 `Tab` / `Shift+Tab` | 在面板的控件之间移动；焦点在面板内循环 |
| `Escape` | 关闭弹出框，并将焦点返回触发器 |
| 面板外按压 | 关闭弹出框；焦点原本位于面板内时返回触发器 |

## 动效

- 面板以 0.97 的缩放、从朝向触发器 5px 的位置开始，快速淡入，并以弹簧从落定的一侧就位。
- 关闭约需 160ms，移动距离为一半。打开与关闭在动画中途可以相互逆转。
- 触发器按下时改变颜色，但不缩放，因为面板是依据它测量的。
- 减弱动效时移除移动与缩放，只保留短暂淡入。关闭动效时面板立即显示或移除。

| 变化 | 完整 | 减弱 | 关闭 |
|---|---|---|---|
| 面板打开 | 从朝向触发器 5px、缩放 0.97 的位置开始，快速淡入，并以弹簧就位 | 仅短暂淡入 | 立即显示 |
| 面板关闭 | 约 160ms 内以一半的移动距离淡出 | 仅短暂淡入 | 立即移除 |
| 打开过程中被关闭 | 从当前位置反向运动 | 短暂淡入 | 立即完成 |
| 触发器按下 | 仅颜色变化，不缩放 | 仅颜色变化 | 仅颜色变化 |

## 相关组件

- `Tooltip`：悬停或聚焦时的单行标签。
- `HoverCard`：悬停时打开的只读预览。
- `Dialog`：必须先处理才能继续使用页面的内容。
- `DropdownMenu`：命令列表。

| 组件 | 适用场景 |
|---|---|
| `Tooltip` | 悬停或聚焦时显示一行标签即可，且其中没有可点击的内容 |
| `HoverCard` | 应在悬停时打开人员或链接的只读预览，且不获得焦点 |
| `Dialog` | 阅读者在继续之前必须确认或完成某事 |
| `DropdownMenu` | 内容是带有方向键导航的命令列表 |
| `Drawer` | 内容是从窗口边缘滑入的较长面板 |
