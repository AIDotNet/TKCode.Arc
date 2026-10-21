---
title: 对话框
summary: 为需要用户关注的决定提供专注的浮层面板。
source-hash: 1eb8d04fe87d
---

<Demo name="ShortForm" />

## 何时使用

`Dialog` 是一个居中的模态面板，用于必须在用户返回页面之前完成的决定或简短任务。它把问题、说明和按钮放在同一处，压暗其后的页面，并在用户作答之前把焦点保持在面板内。在需要有意打断用户的地方使用它，并让面板保持足够小，使用户无需滚动即可读完整个面板。

- **管理控制台中的破坏性确认**：删除项目、撤销 API 密钥或取消订阅。对话框说明后果（“此操作将永久删除 Atlas redesign 及其 14 个文件”），并把破坏性操作放在 `Danger` 变体中，与“取消”并列。
- **适合一个面板的简短表单**：重命名工作区、通过邮件邀请成员并指定角色，或设置付款参考号。字段很少，保存或取消的决定是即时的。
- **在一个面板中完成的多步流程**：“邀请你的团队”之类的入门步骤通过修改 `Title` 与 `Description` 切换步骤，用户始终停留在同一个界面中，文案在原位淡入替换。
- **继续前的确认**：用户必须阅读的计费或政策变更，例如 Team 套餐的新价格，在用户打开账单页面之前显示。
- **来自代码的提示**：表格上的批量操作，例如“归档 42 张发票”，通过 `Dialog.ConfirmAsync` 请求确认，仅在返回 `true` 时继续。

## 何时不用

- **长表单或记录详情**：用 `Drawer`，它可以滚动正文，并让页面在边缘保持可见。
- **以移动端为主、停靠在预览高度的任务**：用 `BottomSheet`，它先部分打开，并可通过拖动展开。
- **不应阻塞页面的轻量内容**：用 `Popover`，它锚定在触发器上，不使用遮罩。
- **命令列表**：用 `DropdownMenu` 或 `ContextMenu`，它们把选项放在所作用的控件旁边。
- **需要留在页面中直到问题解决的消息**：用 `Alert`。应自行消失的简短结果属于 `Toast`。

## 用法

`Dialog` 派生自 `ModalOverlay`，而 `ModalOverlay` 本身是一个 `ContentControl`。它的 `Content` 是正文，`Title` 与 `Description` 构成页头，`Actions` 是页脚，`IsOpen` 控制显示或隐藏。控件在声明的位置不占用空间。当 `IsOpen` 为 true 时，面板会显示在窗口的覆盖层上，位于模糊遮罩之上，因此对话框可以从 XAML 树中的任何位置使用，且不会干扰周围的布局。

在触发器旁边声明 `Dialog`。它本身不渲染任何内容，`IsOpen` 为 true 时显示面板。

### 在 XAML 中声明

把正文放入内容区，把按钮放入 `Actions`。带有 `arc:Dialog.Result` 的按钮会以该值关闭对话框。

```xml
<arc:Dialog Title="重命名项目"
            Description="请选择一个团队成员能快速识别的简短名称。"
            IsOpen="{Binding IsRenaming}">
    <arc:Input Text="{Binding ProjectName}" />
    <arc:Dialog.Actions>
        <StackPanel Orientation="Horizontal" Spacing="8">
            <arc:Button Variant="Secondary" Content="取消" arc:Dialog.Result="{x:False}" />
            <arc:Button Content="保存" arc:Dialog.Result="{x:True}" />
        </StackPanel>
    </arc:Dialog.Actions>
</arc:Dialog>
```

### 结构

1. **标题**（`Title`）：大号字号、中等字重，颜色为前景色。对话框打开期间的修改会带着柔和的模糊升起，旧文案随之离开。
2. **说明**（`Description`）：小号字号、次要文字颜色，位于标题下方 8 px。为空时隐藏。
3. **关闭按钮**（`ShowCloseButton`、`CloseButtonName`）：32 px 按钮，带 1 px 边框和控件圆角，与标题第一行对齐。填充色为 50% 不透明度的弱化表面色。默认名称为“关闭对话框”。
4. **页头分隔线**：页头下方一条边框颜色的 1 px 线。
5. **正文**（`Content`）：四周各 24 px 内边距。当面板高于窗口时，整个面板滚动。
6. **操作区**（`Actions`）：右对齐，位于正文下方 24 px。`Actions` 为 null 时隐藏。
7. **面板**：凸起表面颜色、1 px 边框、表面圆角与浮动阴影。宽度由尺寸预设决定（见“尺寸”），且永远不会超过窗口宽度减去两侧各 16 px 的边距。
8. **遮罩**：近黑色的压暗层，两种外观下都为 46% 不透明度，并带 7 px 的背景模糊。当“减少透明度”设置开启时，模糊被关闭。

### 从代码中确认

`Dialog.ConfirmAsync` 会构建对话框、等待选择，并且只有点击确认按钮时才返回 `true`。按 Escape、点击遮罩或关闭按钮时返回 `false`。对于破坏性操作，将 `isDanger` 设为 `true`，确认按钮就会使用 `Danger` 变体。

<Demo name="Confirmation" />

`Dialog.ShowAsync` 接受自定义内容和一组 `DialogAction`，并返回被点击操作的结果；对话框被关闭时返回 `null`。当问题需要一个确认无法表达的正文时使用它，例如在三种导出格式之间做选择。

```csharp
var choice = await Dialog.ShowAsync(this, "导出发票", exportForm,
    "请选择格式。文件由服务端生成。",
    new DialogAction("取消", false),
    new DialogAction("导出", "csv"));
```

配套的记录类型 `DialogAction` 描述代码构建的对话框中的一个按钮。

| 成员 | 类型 | 含义 |
|---|---|---|
| `Label` | `string` | 按钮文字 |
| `Result` | `object?` | 点击此按钮时 `ShowAsync` 返回的值 |
| `Variant` | `ButtonVariant` | 用于取消类操作的 `Secondary`（默认）；用于主要操作的 `Primary` 或 `Danger` |

### 步骤标题

对话框打开期间可以修改 `Title` 与 `Description`。新文案会在原位淡入替换，因此分步流程无需第二个对话框。把步骤数量放在正文或一行小的状态文字中，而不是标题里，这样标题改写时长度不会变化太多。

<Demo name="Steps" />

```csharp
dialog.Title = "选择角色";
dialog.Description = "编辑者可以修改内容；查看者只能阅读。";
```

### 尺寸

`Size` 决定面板宽度：`Small` 为 360 px，`Medium` 为 440 px（Arc 默认值），`Large` 为 560 px。面板永远不会超过窗口宽度减去两侧各 16 px 的边距，因此窄窗口会把每种尺寸都缩小以适应。

<Demo name="Sizes" />

```xml
<arc:Dialog Title="工作区限制" Size="Large" IsOpen="{Binding ShowLimits}">
    <TextBlock Text="限制按工作区分别生效。" />
</arc:Dialog>
```

对于单个问题且只有一两个按钮的场景，例如删除确认，使用 `Small`。`Medium` 是表单和设置的默认值，适合大多数简短任务。`Large` 适合包含多个字段的表单，或用户必须审阅的变更表格，例如降级前的套餐限制。在整个应用中，每类决定使用一种尺寸，让用户知道按钮总会出现在哪里。

### 关闭与结果

`Closing` 在用户关闭之前引发，并且可以取消。`Closed` 报告原因和结果。带有 `arc:Dialog.Result` 的按钮会设置结果，并以原因 `Action` 关闭对话框。带有 `Command` 且没有结果的按钮本身不会关闭对话框；当工作成功时，由视图模型把 `IsOpen` 设为 `false`，这样在保存进行期间对话框保持打开，并报告原因 `Programmatic`。

```csharp
dialog.Closing += (_, e) =>
{
    if (e.Reason == DismissReason.PressOutside && isSaving)
    {
        e.Cancel = true;
    }
};
dialog.Closed += (_, e) => status.Text = e.Result is true ? "已保存" : "已关闭，方式：" + e.Reason;
```

### 绑定到视图模型

`IsOpen` 默认为双向绑定，因此通过 Escape、遮罩或关闭按钮关闭时，会把 `false` 写回视图模型。`Title` 与 `Description` 是单向的：文案由视图模型持有，对话框负责显示。对于可能失败的工作，请在操作按钮上使用 `Command`；结果属性只用于不需要执行工作的回答。编译绑定需要在视图上设置 `x:DataType`；示例为简洁起见省略了它。

```csharp
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

public interface IProjectService
{
    Task RenameAsync(string name);
}

public partial class ProjectSettingsViewModel : ObservableObject
{
    private readonly IProjectService _projects;

    public ProjectSettingsViewModel(IProjectService projects) => _projects = projects;

    [ObservableProperty]
    private bool _isRenaming;

    [ObservableProperty]
    private string _projectName = string.Empty;

    [ObservableProperty]
    private bool _isSaving;

    [RelayCommand]
    private async Task SaveNameAsync()
    {
        IsSaving = true;
        try
        {
            await _projects.RenameAsync(ProjectName);
            IsRenaming = false;
        }
        finally
        {
            IsSaving = false;
        }
    }

    [RelayCommand]
    private void CancelRename() => IsRenaming = false;
}
```

```xml
<arc:Dialog Title="重命名项目"
            Description="请选择一个团队成员能快速识别的简短名称。"
            IsOpen="{Binding IsRenaming}">
    <arc:Input Label="项目名称" Text="{Binding ProjectName}" />
    <arc:Dialog.Actions>
        <StackPanel Orientation="Horizontal" Spacing="8">
            <arc:Button Variant="Secondary"
                        Content="取消"
                        Command="{Binding CancelRenameCommand}" />
            <arc:Button Content="保存名称"
                        Command="{Binding SaveNameCommand}"
                        IsEnabled="{Binding !IsSaving}" />
        </StackPanel>
    </arc:Dialog.Actions>
</arc:Dialog>
```

只有当视图模型把 `IsRenaming` 设为 `false` 时，对话框才会关闭。如果重命名失败，对话框保持打开并保留用户输入的名称，视图模型可以在输入框的 `ErrorMessage` 中显示错误。

### 示例：账单控制台中的订阅取消

账单页面为 Team 套餐提供“取消订阅”。对话框用通俗的语言说明下一个计费日会发生什么，按钮分别为“保留套餐”和“取消订阅”。“保留套餐”是安全的默认选项，以次要变体放在左侧；破坏性操作以危险变体放在右侧。两个按钮都设置结果，因此调用方只需读取一个值，无需知道用的是哪个按钮。按 Escape 或关闭按钮会在没有结果的情况下关闭对话框，因此检查 `true` 的处理程序会把它们当作“保留套餐”。

```xml
<arc:Dialog Title="取消 Team 套餐？"
            Description="Acme Analytics 将保留访问权限至 2026 年 10 月 31 日。当前计费周期不予退款。"
            Size="Small"
            IsOpen="{Binding IsCancelling}">
    <arc:Dialog.Actions>
        <StackPanel Orientation="Horizontal" Spacing="8">
            <arc:Button Variant="Secondary"
                        Content="保留套餐"
                        arc:Dialog.Result="{x:False}" />
            <arc:Button Variant="Danger"
                        Content="取消订阅"
                        arc:Dialog.Result="{x:True}" />
        </StackPanel>
    </arc:Dialog.Actions>
</arc:Dialog>
```

### 文案规范

对话框只提出一个问题，其标题就是这个问题或操作。标题按句子格式书写。确认类标题以问号结尾；说明写明会发生什么变化，指明对象，数量用阿拉伯数字。按钮文字是动词加宾语，让用户在按下之前就知道结果。不要单独使用“好”“是”“否”，它们迫使用户回读标题才能知道自己同意了什么。

| 不要写 | 改为 |
|---|---|
| 你确定吗？ | 删除项目？ |
| 好 | 删除项目 |
| 是 / 否 | 取消订阅 / 保留套餐 |
| 出错了 | 无法保存名称，请重试。 |
| 点击此处继续 | 继续前往账单 |
| 删除 42 项？ | 归档 42 张发票？ |

### 最佳实践

**推荐**

- 把破坏性操作放在右侧，使用 `Danger` 变体；把安全操作放在左侧，使用 `Secondary` 变体。人们从左到右阅读，主要位置正是他们期待提交操作的地方。
- 在标题和按钮中都写明对象（“删除项目？”和“删除项目”）。只扫读按钮的读者仍然知道自己同意了什么。
- 对于不需要自定义正文的是或否问题，使用 `Dialog.ConfirmAsync`，结果就是一个 `bool`，调用处也更简短。
- 保持关闭按钮和 Escape 可用。它们是键盘与辅助技术的退出方式，对于不知道 Escape 的鼠标用户来说，关闭按钮是唯一可见的退出途径。

**避免**

- 对话框中的长表单。正文会滚动，但滚动的面板会把操作按钮隐藏在首屏以下；`Drawer` 让页面保持可见，表单放在更长的面板中。
- 为防止丢失工作而用 `CloseOnEscape` 禁用 Escape。应为 `Escape` 原因处理 `Closing`，并在保存进行时取消它，这样工作完成后键盘退出途径仍然可用。
- 为一个后续的单一问题在对话框之上再叠加对话框。每层都会增加遮罩与焦点陷阱，且只有最上层回应 Escape，因此用户需要关闭两个面板才能返回。
- 在工作成功之前由命令关闭对话框。调用失败时，用户将既没有对话框也没有错误提示；只有在工作成功后才把 `IsOpen` 设为 `false`。

### 焦点与关闭

对话框打开时焦点移入其中，第一个可获得焦点的元素会获得焦点，除非为该呈现设置了 `InitialFocus`。对话框为最上层的模态时，Tab 与 Shift+Tab 停留在其中。关闭时，焦点回到打开对话框之前拥有焦点的控件，也就是启动该流程的按钮。

当 `CloseOnEscape` 为 true（默认值）时，Escape 关闭对话框。当 `CloseOnOutsidePress` 为 true 时，按下遮罩会关闭对话框。两种原因都会经过 `Closing`，因此应用程序可以在保存进行时取消它们。只有在页面必须在对话框旁边保持可用时，才将 `IsModal` 设为 `false`；此时遮罩与焦点陷阱都会被移除。允许堆叠对话框：只有最上层的对话框响应 Escape，因此按下它会关闭内层对话框，外层对话框保持打开。

### 常见问题

**为什么保存期间按 Escape 没有反应？** 应用程序在保存进行时会取消 `Escape` 原因的 `Closing` 事件。只有当对话框绝不能通过键盘关闭时，才将 `CloseOnEscape` 设为 `False`，因为这会完全移除键盘退出途径。

**用户关闭对话框时结果为什么是 null？** 对于 Escape、遮罩和关闭按钮，`ShowAsync` 与 `ConfirmAsync` 返回 `null`；否则返回被点击操作的 `Result`。请检查你期望的值，而不是与 `null` 比较。

**带有 `Command` 的按钮会关闭对话框吗？** 不会。只有带有 `arc:Dialog.Result` 的按钮才会关闭它。对于可能失败的命令，请在工作成功后于视图模型中把 `IsOpen` 设为 `false`。

**如何让 Enter 触发主按钮？** 在 `Actions` 中为主按钮设置 `IsDefault="True"`。之后在对话框中按 Enter 就会按下该按钮，与任何 Avalonia 窗口一样。

**能否从尚不在窗口中的控件打开对话框？** 使用位于窗口中的可视元素调用 `ShowAsync(owner)`。对话框通过该所有者找到窗口，并在打开期间加入逻辑树。

## API 参考

<!-- api: Dialog -->

## 无障碍

- 面板名称取自 `Title`，帮助文本取自 `Description`。
- 关闭按钮默认名称为“关闭对话框”。用 `CloseButtonName` 修改它。
- 对话框打开时焦点移入其中，Tab 与 Shift+Tab 停留在对话框内。关闭时焦点返回给打开对话框的控件。
- 当 `CloseOnEscape` 为 true 时，Escape 关闭对话框，并把焦点返回给打开它的控件。
- 对话框是模态界面：打开期间，其后的页面无法通过键盘到达。

### 键盘

| 按键 | 操作 |
|---|---|
| `Tab` / `Shift+Tab` | 移到对话框内的下一个或上一个控件，到达两端时循环 |
| 按钮上的 `Enter` 或 `Space` | 按下获得焦点的按钮；带有 `arc:Dialog.Result` 的按钮会以该结果关闭对话框 |
| `Escape` | 以原因 `Escape` 关闭，除非 `CloseOnEscape` 为 false 或 `Closing` 取消了它 |

## 动效

- 遮罩淡入，面板以平滑弹簧从缩放 0.96 上升 8 px，同时淡入。关闭动画更快：面板移动 4 px 并缩放到 0.98，两者都快速淡出。关闭从入场的当前状态开始，因此在入场期间关闭的对话框不会跳动。
- 标题或说明变化时，带着柔和的模糊升起。
- 减弱动效时只保留短暂的不透明度淡入。关闭动效时，对话框立即显示或移除。

| 变化 | 完整 | 减弱 | 关闭 |
|---|---|---|---|
| 打开 | 遮罩淡入；面板以平滑弹簧从 0.96 上升 8 px，同时淡入 | 短暂的不透明度淡入 | 立即完成 |
| 关闭 | 面板移动 4 px 并缩放到 0.98，两者快速淡出 | 短暂的不透明度淡入 | 立即完成 |
| 标题或说明变化 | 新文案带着柔和的模糊升起 | 瞬时淡入 | 立即完成 |
| 按下关闭按钮 | 按钮缩放到 0.96，松开后弹回 | 不缩放 | 不缩放 |

## 相关组件

- `Drawer`：用于长表单及需保持页面可见的详情面板。
- `BottomSheet`：用于以移动端为主、停靠在预览高度的任务。
- `Popover`：用于不阻塞页面的交互内容。
- `Alert`：用于留在页面内的消息，`Toast` 用于简短的操作结果。

| 组件 | 适用场景 |
|---|---|
| `Drawer` | 表单或详情太长而无法放进一个面板，或页面必须保持可见 |
| `BottomSheet` | 任务以移动端为主，并且在预览高度下阅读效果良好 |
| `Popover` | 内容很小，锚定在一个控件上，且不需要遮罩 |
| `DropdownMenu` / `ContextMenu` | 内容是一个命令列表，而不是一个问题 |
| `Alert` | 消息留在页面中，直到问题被解决 |
| `Toast` | 简短的结果，例如“项目已重命名”，应自行消失 |
