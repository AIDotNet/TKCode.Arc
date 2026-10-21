---
title: 按钮
summary: 清晰、响应迅速的操作按钮，次要状态保持低调。
source-hash: 37e98b8739a4
---

<Demo name="Variants" />

## 何时使用

`Button` 是页面、表单、对话框或工具栏上单一操作的默认控件。它提供产品所需的四种视觉层级（主要、次要、幽灵与危险）、三种高度、一个在加载时保持标签宽度的状态，以及一个可以原地变化的标签。应用中的大多数操作都是这个控件，其他 Arc 操作控件则是同一理念下的特定场景。

- **在 CRM 中保存客户记录**：联系人表单底部放一个“保存更改”主按钮，旁边是“取消”次要按钮。每个表单只有一个主操作，能告诉用户下一步是什么。
- **在账单控制台中创建发票**：“创建发票”作为对话框底部的主操作，“存为草稿”作为次要伙伴。两者使用相同的中等高度，让底部区域保持对齐。
- **在结账设置页中添加卡片**：“添加卡片”为主按钮，旁边的“查看历史”为幽灵按钮，它不会与主操作争抢注意力。
- **在分析仪表板中导出报表**：生成文件时“导出报表”使用 `IsLoading`。标签保持宽度，加载指示显示在标签原来的位置，键盘焦点仍停留在按钮上。
- **在设置页中删除项目**：“删除项目”使用危险按钮，让颜色在按下之前就发出警告。若删除无法撤销，应再配一个确认步骤。

## 何时不用

- **提交后必须在按钮上显示结果，例如“已保存”或“已发送”**：使用 `ActionButton`，它保存待处理和成功两组文字，并会自动回到空闲状态。
- **同一操作有两到五个相近的替代方式**：使用 `SplitButton`，把替代方式放在它的菜单中。
- **需要刻意按住才能执行的破坏性操作**：使用 Gestures 中的 `HoldToConfirm`，避免一次误点就删除数据。
- **需要保持选中的值，例如视图模式或时间范围**：使用 Toggles 中的 `SegmentedControl`。按钮只执行一次操作，不会记住选择。
- **同一对象上的多个相关操作，例如分享、复制和归档**：使用 `ButtonGroup`，它把它们合并为一个带分隔线的整体，而不是一排独立按钮。

## 用法

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:Button Content="保存更改" Variant="Primary" />
</UserControl>
```

`Button` 派生自 Avalonia 的 `Button`，因此 `Content`、`Click`、`Command`、`CommandParameter`、`IsEnabled`、`IsDefault`
以及 Enter 与空格键的激活方式，都与任何按钮一样工作。Arc 层在此之上增加变体、尺寸、加载状态，以及 Arc 外框、焦点环和动效。
由于 `Content` 承载标签，按钮可以显示任何内容，而不只是文字，但应保持标签简短，使宽度可预期。

### 结构

1. **外框**：1 px 边框，圆角为控件圆角（`RadiusControl`）。填充色与边框颜色由变体决定。
2. **标签**（`Content`）：小号正文字号（`TypeSM`）、中等字重，居中显示。内容变化时，新文字会在原位升起。
3. **加载指示**（`IsLoading`）：16 px 的圆环，加载时在外框中央旋转。标签留在原位并淡出，因此宽度不变。
4. **内边距**（随尺寸）：`Small` 为 12 px，`Medium` 为 16 px，`Large` 为 20 px，两侧各一份。
5. **高度**（`MinHeight`，随尺寸）：`Small` 为 36 px，`Medium` 为 44 px，`Large` 为 50 px。
6. **焦点环**：2 px 的焦点色环，绘制在外框外侧 2 px 处，圆角为 22 px。仅在键盘获得焦点时出现。

### 变体

`Variant` 设置视觉层级。每个表面只使用一个 `Primary` 操作。其他变体回答的问题是：“这个操作相对于旁边的那个有多重要？”

| 变体 | 填充与边框 | 文字 | 适用场景 |
|---|---|---|---|
| `Primary` | 前景色填充 | 背景色 | 表面上的主要操作，例如“保存更改”或“创建发票” |
| `Secondary` | 带边框的表面 | 前景色 | 主按钮旁边的替代操作，例如“取消”或“存为草稿” |
| `Ghost` | 透明，无边框 | 次要文字，悬停时为前景色 | 卡片与工具栏中的低强调操作，例如“查看历史” |
| `Danger` | 带边框的表面 | 危险色 | 破坏性操作，颜色在按下之前发出警告 |

```xml
<arc:Button Content="保存更改" Variant="Primary" />
<arc:Button Content="取消" Variant="Secondary" />
<arc:Button Content="了解更多" Variant="Ghost" />
<arc:Button Content="删除项目" Variant="Danger" />
```

每种变体都有自己的状态。`Primary` 在悬停和按下时降低不透明度。`Secondary` 与 `Ghost` 在悬停和按下时以弱化的表面色填充。
`Danger` 在悬停和按下时把边框变为危险色。禁用按钮保持其形状：`Primary` 淡化到 52% 的不透明度，而描边变体切换为弱化文字与弱化表面。
禁用的危险标签会向表面色偏移，因此仍能辨认为危险操作。

将 `Danger` 按钮与 `Primary` 操作分开放置，中间隔一个次要按钮或留出明显的间距，避免两者被误认。

### 尺寸

`Size` 设置最小高度：`Small` 为 36 px，`Medium` 为 44 px（默认），`Large` 为 50 px。尺寸不会随窗口宽度变化，因此窗口变窄时，一排按钮的节奏保持不变。

<Demo name="Sizes" />

每个表单、对话框或工具栏使用一种尺寸。`Small` 适合表格、工具栏和行内操作，`Medium` 是表单、对话框和设置页的默认值，`Large` 适合登录页或空状态页上单一的醒目操作。内边距随尺寸变化，使标签的边距与高度保持比例。

### 加载与标签变形

将 `IsLoading` 设为 `True` 以显示加载指示。标签按其宽度留在原位，因此请求运行时对话框的底部区域不会移动。按钮保持键盘焦点，但在工作结束前会忽略点击与按键。更改 `Content` 会替换标签：新文字从柔和模糊中升起 4 px，旧文字上移 3 px 并淡出，宽度弹性地调整为新标签的宽度。

<Demo name="LoadingMorph" />

```xml
<arc:Button Content="{Binding SaveLabel}"
            IsLoading="{Binding IsSaving}"
            Variant="Primary" />
```

`IsLoading` 适用于几秒内完成的工作。对于耗时更长的任务，例如导出数千行数据，应在页面中显示进度（例如进度指示器或状态行），让按钮只负责发起任务的请求。转了一分钟的加载圆环无法告诉人们工作进展到了哪里。

### 纯图标按钮

纯图标按钮没有可见文字，因此要为它提供无障碍名称。宽度为 48 px 或更窄的按钮被视为图标按钮：按下时会比文字按钮更深，压至其尺寸的 96%。

```xml
<arc:Button Size="Small" Variant="Ghost"
            AutomationProperties.Name="关闭">
    <TextBlock Text="×" />
</arc:Button>
```

为每个纯图标按钮提供一个描述其操作的名称，例如“关闭”或“移除筛选”；当图标含义不够直观时，还应加上工具提示。屏幕阅读器朗读的就是这个名称，对于看不见图标的人来说，它是识别该操作的唯一途径。

### 禁用

将 `IsEnabled` 设为 `False`。描边变体保持其形状，因此不可用的操作仍像按钮。禁用的按钮和其他禁用控件一样移出 Tab 顺序。

<Demo name="Disabled" />

只在操作确实无法执行时才禁用，并在附近说明原因，例如在按钮下方写“连接付款方式后才能发布发票”。没有原因的禁用按钮只会让人去猜问题出在哪里。

### 绑定到视图模型

按钮有命令、标签和忙碌状态。工作由视图模型负责，按钮负责显示其进度。下例使用 CommunityToolkit.Mvvm 以及一个保存草稿的应用服务。

```csharp
public interface IInvoiceService
{
    Task SaveDraftAsync(string invoiceId);
}

public partial class InvoiceViewModel : ObservableObject
{
    private readonly IInvoiceService _invoices;

    public InvoiceViewModel(IInvoiceService invoices)
    {
        _invoices = invoices;
    }

    [ObservableProperty]
    private string _invoiceId = "inv_2026_0412";

    [ObservableProperty]
    private bool _isSaving;

    [ObservableProperty]
    private string _saveLabel = "保存更改";

    [RelayCommand]
    private async Task SaveAsync()
    {
        IsSaving = true;
        try
        {
            await _invoices.SaveDraftAsync(InvoiceId);
            SaveLabel = "已保存";
        }
        finally
        {
            IsSaving = false;
        }
    }
}
```

```xml
<StackPanel Width="420" Spacing="16">
    <arc:Input Label="发票编号" Text="{Binding InvoiceId}" />
    <arc:Button Content="{Binding SaveLabel}"
                Command="{Binding SaveCommand}"
                IsLoading="{Binding IsSaving}"
                Variant="Primary"
                HorizontalAlignment="Right" />
</StackPanel>
```

所有绑定都是单向的。`Content`、`IsLoading` 和 `Variant` 来自视图模型，按钮不会写回任何值。按钮会触发 `Click` 并执行 `Command`，因此工作应放在 `Command` 中。`IsLoading` 为 `True` 时，按钮会忽略激活，因此用户再次按下时，`Command` 不会第二次执行。编译绑定需要在视图上设置 `x:DataType`；为简洁起见，示例中省略了它。

### 示例：账单设置页底部

SaaS 账单控制台中的付款设置面板。主操作“保存更改”位于最右侧，这是读完表单后视线最终落点的位置。“取消”在其左侧，作为次要选项。“移除卡片”是左侧的危险按钮，与保存组分开放置，使破坏性操作不会离保存只有一次误触的距离。“查看历史”是幽灵按钮，因为它只是跳转，不会更改任何内容。

![账单设置页底部，包含幽灵、危险、次要和主要按钮](images/BillingFooter.png)

```xml
<StackPanel Width="600" Spacing="20">
    <StackPanel Spacing="6">
        <TextBlock Text="Payment method"
                   FontSize="20"
                   FontWeight="SemiBold"
                   Foreground="{arc:ArcDesignTokenResource Foreground}" />
        <TextBlock Text="Visa ending 4242, used for the Growth plan. Next invoice on 1 November 2026."
                   TextWrapping="Wrap"
                   Foreground="{arc:ArcDesignTokenResource TextSecondary}" />
    </StackPanel>
    <Grid ColumnDefinitions="Auto,*,Auto">
        <arc:Button Grid.Column="0" Content="Remove card" Variant="Danger" />
        <StackPanel Grid.Column="2" Orientation="Horizontal" Spacing="8">
            <arc:Button Content="View history" Variant="Ghost" />
            <arc:Button Content="Cancel" Variant="Secondary" />
            <arc:Button Content="Save changes" Variant="Primary" />
        </StackPanel>
    </Grid>
</StackPanel>
```

### 文案规范

- **标签**：动词加宾语，句子式大小写，例如“保存更改”“创建发票”“删除项目”。当屏幕上有两个项目时，只写“删除”会含糊不清。
- **长度**：标签控制在两到三个词。过长的标签会把页脚中的其他按钮挤出一行。
- **关闭**：对于会丢弃更改的对话框使用“取消”；只有在未编辑任何内容时才使用“关闭”。
- **不加标点**：结尾不加句号，也不加感叹号。按钮是一条指令，不是句子。
- **避免含糊的确认**：“确定”没有告诉用户将发生什么。应说明具体的变更。

| 不要写 | 改为 |
|---|---|
| 确定 | 保存更改 |
| 提交 | 创建发票 |
| 删除 | 删除项目 |
| 点这里 | 查看发票 |
| 完成！ | 已保存 |

### 最佳实践

**推荐**

- 每个表面只使用一个 `Primary` 按钮。两个实心按钮会争夺同一注意力，用户必须读完两个标签才能找到下一步。
- 把主操作放在最后，位于行的末端，次要操作在其左侧。人们从左到右阅读这一行，会期望提交操作位于阅读的终点。
- 表单、对话框或工具栏中的所有按钮使用同一个 `Size`。高度不一致会让页脚看起来破碎，即使每个按钮本身都是正确的。
- 为纯图标按钮提供无障碍名称；当图标含义不够直观时，再加上工具提示。
- 对几秒内完成的请求使用 `IsLoading`。宽度会被保留，因此加载指示出现时布局不会移动。

**避免**

- 把 `Danger` 用于非破坏性的操作。红色按钮会让人习惯忽略颜色，真正需要警告时反而失去作用。
- 为了隐藏校验问题而禁用主按钮。应在用户按下时校验，并指向第一个问题，让原因可见。
- 用计时器反复切换标签。按钮最终会进入用户无法预测的状态；当结果文字需要出现在按钮上时，应使用 `ActionButton`。
- 把 `Ghost` 用于表面上唯一的操作。它的低对比度适合次要导航链接，但页面上最需要的操作应一眼可见。
- 在同一行中放入多个很长的标签。这一行会换行或滚动，操作的顺序也会乱掉。

### 常见问题

**为什么加载中的按钮保持焦点却忽略我的点击？** 这是设计如此。`IsLoading` 会吞掉点击和按键，避免请求被重复发送；同时它保持焦点，让用户和辅助技术留在原处。按钮会向外报告自己处于忙碌且不可用的状态。

**按钮加载时 `Command` 会执行吗？** 不会。`IsLoading` 为 `True` 时，按钮会在点击处理中提前返回，因此 `Click` 和 `Command` 都不会执行。

**为什么我的菜单锚点按钮按下时不缩放？** 对于会打开弹层或菜单的按钮，将 `IsPressScaleEnabled` 设为 `False`。弹层会测量锚点按下时的尺寸，因此锚点只通过颜色做出反馈。该属性默认为 `True`。

**标签变化时按钮宽度跳动，如何保持稳定？** 宽度会弹性地适应每个标签。若要固定宽度，把 `MinWidth` 设为你预期的最长标签的宽度，这样按钮就不会窄于它。

**为什么窄的图标按钮比文字按钮按下得更深？** 按下深度跟随宽度。宽度为 48 px 或更窄时，按钮压至 96%；超过 220 px 时压至 98.5%，使按压深度与目标大小相匹配。

## API 参考

<!-- api: Button -->

本页还用到的配套类型：

| `ButtonVariant` 值 | 含义 |
|---|---|
| `Primary` | 默认值。表面主要操作的前景色填充。 |
| `Secondary` | 带边框的表面，用于主要操作旁边的替代操作。 |
| `Ghost` | 透明按钮，用于低强调的操作。 |
| `Danger` | 带危险色标签的表面，用于破坏性操作。 |

`Size` 使用 `ControlSize` 的值 `Small`、`Medium`（默认）和 `Large`。

## 无障碍

- 按钮具有 Button 角色。纯图标按钮需要 `AutomationProperties.Name`，因为它没有可供命名的文字。
- 设置 `IsLoading` 时，按钮仍可获得焦点，并向外报告自己处于忙碌且不可用的状态。
- Enter 与空格键激活按钮。加载期间，点击和按键会被吞掉。
- 键盘焦点显示 2 px 的焦点环，并偏移 2 px。指针焦点不绘制焦点环，因此点击后不会留下高亮。
- 禁用的按钮会被移出 Tab 顺序，并向外报告自己不可用。它会被淡化而不是隐藏，因此用户仍能看到该操作的存在。

### 键盘

| 按键 | 操作 |
|---|---|
| `Tab` / `Shift+Tab` | 将焦点移到下一个或上一个按钮。禁用的按钮会被跳过。 |
| `Enter` / `Space` | 激活按钮，触发 `Click` 并执行 `Command`。`IsLoading` 为 `True` 时忽略。 |
| 文本框中的 `Enter` | 窗口中有标记为 `IsDefault="True"` 的默认按钮时，按下该按钮。 |

## 动效

- 按下时内容会轻微缩小，松开后弹回。窄的图标按钮缩放幅度稍大。
- 标签变化时，新文字从柔和模糊中升起，宽度弹性地调整到新标签。加载时，加载指示持续旋转。
- 减弱动效会去掉按下缩放和宽度弹簧。标签与加载指示以短暂的交叉淡化切换，加载指示停止旋转。关闭动效则立即替换标签与状态。

| 变化 | 完整 | 减弱 | 关闭 |
|---|---|---|---|
| 按下 | 内容缩放到 97%（图标按钮为 96%，宽按钮为 98.5%），然后弹回 | 无按下缩放 | 无按下缩放 |
| 标签变化 | 新文字从柔和模糊中升起，旧文字向上淡出，宽度弹性适配 | 瞬间交叉淡化 | 立即完成 |
| 开始或结束加载 | 标签在加载指示下方淡出，加载指示每 0.7 秒转一圈 | 交叉淡化，加载指示停止旋转 | 立即完成 |
| 悬停、按下、禁用颜色 | 颜色以快速曲线交叉渐变 | 瞬间完成 | 立即完成 |

## 相关组件

- `ActionButton`：用于异步提交，在按钮上显示待处理与成功状态。
- `SplitButton`：用于主要操作加上一个包含相近替代操作的菜单。
- `ButtonGroup`：用于把几个相关操作合并到一个表面上。
- `CopyButton`：用于复制一个值并给出确认。

| 组件 | 适用场景 |
|---|---|
| `ActionButton` | 提交时应在按钮上显示待处理词与结果词 |
| `SplitButton` | 一个默认操作旁边有两到五个相近的替代操作 |
| `ButtonGroup` | 多个操作作用于同一对象，并共享一个表面 |
| `ConfirmMorph` | 破坏性操作应在原位请求确认 |
| `HoldToConfirm` | 破坏性操作需要刻意按住，不能被误触发 |
| `SegmentedControl` | 控件显示一个会保持选中的选择，例如视图模式 |
