---
title: 抽屉
summary: 用于专注工作的临时侧边面板。
source-hash: b42421ed7675
---

<Demo name="Sides" />

## 何时使用

`Drawer` 是一种模态面板，从窗口的某一条边缘滑入，并在边缘处保留后方页面的可见部分。它适合依赖当前视图的专注工作：用户筛选列表并看到结果随之变化，或者查看某一条记录而不丢失其在表格中的位置。它可以通过按钮、Escape、点击遮罩或朝边缘拖动来关闭，因此很容易离开，并回到页面上的同一位置。

- **管理后台列表的筛选**：应收账款列表在用户于右侧抽屉中设置状态、日期范围和负责人时保持行可见，然后应用或重置筛选条件。
- **发票或订单详情**：客服人员选中一张发票，在抽屉中查看其明细项、付款记录和备注，然后关闭抽屉并选择下一行，页面无需跳转。
- **所选项目的设置**：产品编辑器打开左侧抽屉，展示正在编辑的项目的规格与定价设置，目录始终保持可见。
- **对话框容纳不下的长表单**：包含银行信息和税务字段的供应商入驻表单，超出了一个短面板的容量，但用户仍需要看到背后的供应商列表。
- **移动端与窄窗口**：在窄窗口中，抽屉几乎占满整个宽度，并把页头保留为拖动手柄，因此用户可以通过快速滑动将其关闭。

## 何时不用

- **简短的决定与确认**，例如删除项目：用 `Dialog`，它居中显示，并把后果和按钮放在同一个面板中。
- **需要停靠在多个高度的移动端优先面板**，例如配送选项或分享面板：用 `BottomSheet`，它停在预览高度，并分级展开。
- **锚定在控件上、没有遮罩的小型内容**，例如列选择器：用 `Popover`。
- **针对当前选中项执行的命令列表**：用 `DropdownMenu` 或 `ContextMenu`。
- **应用各部分之间的导航**：页面内的分区用 `Tabs`，应用的主要区域用侧边栏。承载导航的抽屉会把目的地藏在一个可关闭的面板后面。

## 用法

`Drawer` 派生自 `ModalOverlay`，而 `ModalOverlay` 本身是 `ContentControl`。它的 `Content` 是可滚动的正文，`Title` 和 `Description` 构成页头，`Actions` 是页脚，`Side` 选择边缘，`IsOpen` 显示或隐藏面板。该控件在声明的位置不占用空间；当 `IsOpen` 为 true 时，面板会在窗口的覆盖层上滑入，并位于轻微模糊的遮罩之上。可以在任何位置声明它，并绑定 `IsOpen`。

### 边缘

`Side` 设置面板滑入的边缘：`Left`、`Right`、`Top` 或 `Bottom`。默认值为 `Right`。

```xml
<arc:Drawer Title="筛选"
            Description="不离开列表即可缩小范围。"
            Side="Left"
            IsOpen="{Binding ShowFilters}">
    <StackPanel Spacing="16">
        <CheckBox Content="仅未结发票" />
        <CheckBox Content="包含草稿" />
    </StackPanel>
</arc:Drawer>
```

在宽窗口中，若列表位于一侧，筛选与详情抽屉适合使用 `Right`，因为列表在左侧保持可见。`Left` 适合用户在列表之前浏览的内容，例如分类树。`Top` 和 `Bottom` 横跨整个宽度；底部抽屉会显示一个抓手，提示它可以被拖走。每类任务使用一个边缘，让用户熟悉筛选或详情面板的打开位置。

### 结构

1. **页头**（`Title`、`Description`）：页头就是拖动手柄。它的内边距为 24 px（窄窗口时为 20 px），下方有 1 px 边框。标题使用大号字号和中等字重；描述使用小号字号和次要文字颜色，位于标题下方 8 px，最宽 512 px。
2. **关闭按钮**（`ShowCloseButton`、`CloseButtonName`）：32 px 的按钮，使用控件圆角和 1 px 边框，与标题的第一行对齐。它的填充为弱化的表面色，不透明度 65%。它始终存在，默认名称为“关闭抽屉”。
3. **抓手**：仅在 `Bottom` 抽屉上显示。它是 36 × 4 px 的胶囊形，使用强边框色，不透明度 75%，位于页头顶部下方 9 px。
4. **正文**（`Content`）：24 px 内边距（窄窗口时为 20 px）。页头保持不动，正文滚动。
5. **操作区**（`Actions`）：右对齐，位于正文下方 24 px。当 `Actions` 为 null 时隐藏。
6. **面板**：位于 `Left` 或 `Right` 边缘时，宽度最多 480 px（30 rem），且不超过窗口减去 16 px 边距（窄窗口时为 12 px）。位于 `Top` 或 `Bottom` 边缘时，面板横跨整个宽度，高度最多 512 px；若窗口高度更小，则取窗口高度。位于内侧的圆角半径为 22 px（窄窗口时为 18 px），细边框只绘制在内侧边缘。
7. **遮罩**：接近黑色的暗化层，不透明度 38%，比 `Dialog` 下方的遮罩更浅，并带有 4 px 的背景模糊。开启“减少透明度”设置时，模糊关闭。

窗口宽度小于 40 rem（640 px）时，面板会变窄：几乎占满整个宽度，圆角与内边距随之收紧。

### 长内容

正文会滚动，而页头保持不动。把完整内容放进抽屉即可，无需计算其尺寸。当抽屉比窗口更高时，正文会滚动，因此用户在浏览明细项列表时，标题、描述和关闭按钮始终可见。

<Demo name="LongContent" />

```xml
<arc:Drawer Title="INV-2041" Description="Ada Lovelace 于 3 月 12 日付款。" IsOpen="{Binding ShowRecord}">
    <ItemsControl ItemsSource="{Binding LineItems}" />
</arc:Drawer>
```

对于包含数百行的列表，请对数据分页，或在正文中使用会虚拟化其项的控件。正文是普通的滚动区域，因此放入其中的每一项都会被测量和排列。

### 操作

把按钮放在 `Actions` 中。带有 `arc:Dialog.Result` 的按钮会以该值关闭抽屉，与 `Dialog` 中的行为相同。筛选抽屉通常提供一个次要的“重置”（返回 `false`）和一个主要的“应用”（返回 `true`），因此调用方可以在抽屉关闭后读取一个结果。

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

### 从代码打开

当同一个抽屉服务多个边缘时，请同时设置 `Side` 和 `IsOpen`。`OpenCommand` 可以从按钮打开抽屉，而无需设置 `IsOpen`。

```csharp
drawer.Side = DrawerSide.Bottom;
drawer.IsOpen = true;

openDetails.Command = details.OpenCommand;
```

### 关闭原因

`Closed` 通过 `OverlayClosedEventArgs.Reason` 报告抽屉关闭的原因：`CloseButton`、`Escape`、`PressOutside`、`Gesture`、`Action` 或 `Programmatic`。`Closing` 在用户关闭之前引发，并且可以通过 `e.Cancel` 取消，例如当抽屉中的表单存在未保存的更改时。

```csharp
bool hasUnsavedChanges = true; // 表单字段变化时由表单设置
drawer.Closing += (_, e) =>
{
    if (e.Reason is DismissReason.PressOutside or DismissReason.Gesture && hasUnsavedChanges)
    {
        e.Cancel = true;
    }
};
drawer.Closed += (_, e) => log.Text = $"已由 {e.Reason} 关闭";
```

### 拖动关闭

页头就是拖动手柄。朝边缘拖动时，面板跟随指针。背离边缘拖动时会受到平方根曲线的阻力，因此面板只移动一点并弹回。松开时，若拖动距离超过面板尺寸的三分之一，或朝边缘的快速滑动速度超过 500 px/s，抽屉就会关闭；否则它会以利落的弹簧弹回。在页头中的按钮或文本框上按下是点击而非拖动，因此关闭按钮仍然有效。在减弱动效以及关闭动效时，拖动被禁用，因此关闭操作永远不依赖手势。

### 绑定到视图模型

`IsOpen` 默认为双向绑定，因此通过 Escape、遮罩或拖动关闭时，会把 `false` 写回视图模型。抽屉的 `Title` 和 `Description` 是单向绑定，在抽屉打开期间变化时会带着柔和模糊切换；当抽屉显示列表刚刚选中的记录时，这一点很有用。

```csharp
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

public partial class ReceivablesViewModel : ObservableObject
{
    [ObservableProperty]
    private bool _isDetailOpen;

    [ObservableProperty]
    private Invoice? _selectedInvoice;

    [RelayCommand]
    private void OpenInvoice(Invoice invoice)
    {
        SelectedInvoice = invoice;
        IsDetailOpen = true;
    }

    [RelayCommand]
    private async Task MarkPaidAsync()
    {
        await _invoices.MarkPaidAsync(SelectedInvoice!.Id);
        IsDetailOpen = false;
    }
}
```

```xml
<arc:Drawer Title="{Binding SelectedInvoice.Number}"
            Description="{Binding SelectedInvoice.Summary}"
            Side="Right"
            IsOpen="{Binding IsDetailOpen}">
    <ItemsControl ItemsSource="{Binding SelectedInvoice.LineItems}" />
    <arc:Drawer.Actions>
        <arc:Button Content="标记为已付款" Command="{Binding MarkPaidCommand}" />
    </arc:Drawer.Actions>
</arc:Drawer>
```

### 示例：应收账款列表中的发票详情

财务团队在宽窗口的表格中审查逾期发票。选中一行会打开右侧抽屉，它以发票编号作为标题，以客户和到期日作为描述，并在正文中显示明细项。抽屉不遮挡表格：用户可以关闭抽屉并选择另一行，从而将下一张发票与上一张进行比较。操作区中，“发送提醒”使用次要变体，“标记为已付款”使用主要变体；视图模型在付款记录完成后关闭抽屉，因此调用失败时抽屉保持打开，发票仍然可见。

```xml
<arc:Drawer Title="INV-2041"
            Description="Northwind Traders，2026 年 9 月 30 日到期，已逾期 21 天。"
            Side="Right"
            IsOpen="{Binding IsDetailOpen}">
    <StackPanel Spacing="12">
        <TextBlock Text="设计评审，12 小时，每小时 180 EUR" />
        <TextBlock Text="原型构建，20 小时，每小时 150 EUR" />
        <TextBlock Text="差旅与住宿，1 项，640 EUR" />
        <TextBlock Text="应付总额：EUR 5,800.00"
                   FontWeight="SemiBold"
                   Foreground="{arc:ArcDesignTokenResource Foreground}" />
    </StackPanel>
    <arc:Drawer.Actions>
        <StackPanel Orientation="Horizontal" Spacing="8">
            <arc:Button Variant="Secondary" Content="发送提醒" />
            <arc:Button Content="标记为已付款" Command="{Binding MarkPaidCommand}" />
        </StackPanel>
    </arc:Drawer.Actions>
</arc:Drawer>
```

### 文案规范

页头应命名对象或任务：筛选抽屉用“筛选”，记录抽屉用发票编号。描述用一句话说明范围，其中的数字使用阿拉伯数字。操作按钮使用动词加对象，筛选抽屉使用“应用筛选”而不是“确定”。

| 不要写 | 改为 |
|---|---|
| 选项 | 筛选 |
| INV2041 详情 | INV-2041 |
| 提交 | 发送提醒 |
| 确定 | 应用筛选 |
| 点击 X | 关闭抽屉 |
| 详情（13 项） | 明细项（13） |

抽屉打开期间保持标题稳定。如果标题在每次交互时都变化，页头就会通过模糊的文字不断闪动，因此只在所选对象变化时才更改标题。

### 最佳实践

**推荐**

- 当抽屉与列表相关时，让列表在抽屉后方保持可见。上下文让用户能够比较各行，并立即看到筛选的效果。
- 按任务选择边缘：宽窗口上的详情与筛选使用 `Right`；窄窗口上的短面板使用 `Bottom`，并配有可拖走的抓手。
- 为抽屉设置 `Title`。它是面板的无障碍名称，而页头又是拖动手柄，空白页头难以辨认。
- 保持关闭按钮和 Escape 键可用；拖动只是离开的额外方式，而不是唯一方式。

**避免**

- 在抽屉中放置简短的是或否决定。`Dialog` 会把问题居中，而抽屉会把后果藏在窗口边缘。
- 在抽屉中嵌套抽屉。叠加的遮罩会让页面难以阅读，而且只有最上层的面板响应 Escape。
- 把用户必须频繁访问的导航放在抽屉中。点击遮罩即会关闭抽屉，因此常用目的地应放在常驻的侧边栏中。
- 页头中的长文字。标题保持几个词，描述保持一句话；描述最宽 512 px。
- 在正文中放置水平滚动。正文只做垂直滚动；宽表格应当缩窄或重新组织，以适应面板。

### 常见问题

**为什么我松开页头后抽屉关闭了？** 松开时拖动距离超过面板的三分之一，或朝边缘的快速滑动速度超过 500 px/s。请用 `Gesture` 原因处理 `Closing`，并在表单有未保存的更改时取消关闭。

**如何在没有代码后置的情况下从按钮打开抽屉？** 通过名称将按钮的命令绑定到抽屉的 `OpenCommand`，例如 `Command="{Binding #RecordDrawer.OpenCommand}"`，或在视图模型中绑定 `IsOpen`。

**为什么抽屉的正文不滚动？** 当内容高于面板时，正文才会滚动。移除正文中嵌套的 `ScrollViewer`：正文本身已经滚动全部内容，第二个滚动区域会分散滚动。

**底部边缘的抽屉会显示抓手吗？** 会。抓手只属于底部面板；顶部、左侧和右侧抽屉不显示它，因为它们的拖动方向从边缘就能看出。

**保存失败后如何让抽屉保持打开？** 在 `Closing` 中取消关闭，或在保存成功之前不要把 `IsOpen` 设为 `false`。抽屉会带着内容保持打开，因此错误可以显示在字段旁边。

## API 参考

<!-- api: Drawer -->

抽屉的边缘由配套枚举 `DrawerSide` 表示，成员为 `Left`、`Top`、`Right` 和 `Bottom`。默认值为 `Right`。

## 无障碍

- 面板的名称来自 `Title`，帮助文本来自 `Description`。
- 抽屉打开时焦点移入其中，Tab 与 Shift+Tab 停留在抽屉内，关闭时焦点返回打开它的元素。
- 当 `CloseOnEscape` 为 true 时，Escape 关闭抽屉。关闭按钮始终存在，默认名称为“关闭抽屉”；可用 `CloseButtonName` 修改。
- 拖动是可选的。在减弱动效下它被关闭，因此关闭操作从不依赖手势。

### 键盘

| 按键 | 操作 |
|---|---|
| `Tab` / `Shift+Tab` | 移到抽屉内的下一个或上一个控件，到达两端时循环 |
| 按钮上的 `Enter` 或 `Space` | 按下获得焦点的按钮；带有 `arc:Dialog.Result` 的按钮会以该结果关闭抽屉 |
| `Escape` | 以 `Escape` 原因关闭，除非 `CloseOnEscape` 为 false 或 `Closing` 将其取消 |

抽屉不能通过键盘移动。拖动页头只是指针或触摸操作，因此关闭按钮和 Escape 是键盘离开的途径。

## 动效

- 面板以平滑弹簧从其边缘滑入，同时遮罩淡入。
- 朝边缘拖动页头即可让面板跟随指针。拖动超过面板的三分之一，或快速滑动速度超过 500px/s 时，抽屉关闭；否则它会弹回。
- 点击、Escape 或按下遮罩会以短暂的补间动画关闭抽屉，阴影在行程的最后一段淡出。快速滑动则以保留松手速度的弹簧关闭它。
- 减弱动效会关闭拖动，并将滑动替换为短暂的淡入。关闭动效时，抽屉立即显示或消失。

| 变化 | 完整 | 减弱 | 关闭 |
|---|---|---|---|
| 打开 | 面板以平滑弹簧从其边缘滑入；遮罩淡入 | 短暂淡入 | 立即完成 |
| 通过按钮、Escape 或遮罩关闭 | 标准补间滑出；阴影在行程的最后一段淡入 | 短暂淡入 | 立即完成 |
| 通过快速滑动关闭 | 保留松手速度的弹簧 | 短暂淡入 | 立即完成 |
| 页头拖动 | 朝边缘跟随指针；反方向受阻 | 禁用拖动 | 禁用拖动 |
| 标题或描述变化 | 新文案带着柔和模糊升起 | 瞬间淡入 | 立即完成 |

## 相关组件

- `Dialog`：必须打断页面、先行作出的决定。
- `BottomSheet`：移动端优先的面板，停在预览高度，并可展开。
- `Popover`：锚定在某个控件上的小型交互内容。

| 组件 | 适用场景 |
|---|---|
| `Dialog` | 问题简短，且必须先于其他操作作出回答 |
| `BottomSheet` | 任务以移动端为主，先停在预览高度，之后再展开 |
| `Popover` | 内容很小，锚定在一个控件上，且不需要遮罩 |
| `Tabs` | 同一页面的分区原地切换，且不应有面板遮住页面 |
| `ResizablePanels` | 列表与详情都是页面的固定部分，且用户需要调整它们的大小 |
| `ExpandableCard` | 记录在页面内展开，周围的行需要随之移动 |
