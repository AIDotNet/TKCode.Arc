---
title: 步骤条
summary: 显示用户在多步骤流程中的位置以及已完成的步骤。
source-hash: d461c289e033
---

<Demo name="Checkout" />

## 何时使用

`Stepper` 显示在固定步骤序列中所处的位置，以及哪些步骤已经完成。它位于流程内容的上方，例如结账或引导向导，告诉人们已经走了多远、还剩什么。它只表示进度：步骤内容、校验以及进入下一步的规则仍然由你的应用负责。

- **电商店铺的结账**：账号、配送、支付与确认。人会看到配送已完成、接下来是支付，并且可以回到配送修改地址，而不会丢失订单的其余部分。
- **SaaS 管理控制台中的工作区设置**：创建工作区、邀请团队、添加账单并确认。支付步骤失败时，错误出现在该步骤上，也就是人需要处理的地方。
- **金融科技引导中的身份验证**：上传证件、自拍核验、居住地址与最终确认。步骤是严格的，后面的步骤绝不会在前面的步骤完成之前显示为已完成。
- **内部工具中的多步导入**：上传 CSV、映射列、预览行并确认。纵向步骤条配合每一步的描述，在侧边面板中解释每个阶段。
- **运维控制台中的运行手册**：确认、缓解、验证并关闭一次事件。步骤条显示值班团队在一个始终按相同顺序执行的流程中所处的位置。

## 何时不用

- **已完成工作的单一百分比**，例如上传或每晚导出：用 `Progress`，它将一个值与整体作对比。每百分之一对应一步的步骤条将无法阅读。
- **人们可以以任意顺序打开的分区**，例如账号设置页的标签页：用 `Tabs`。步骤条隐含顺序，并且只能从中选择已完成的步骤。
- **事件的历史记录**，例如订单的审计记录或工单的回复：用 `Timeline`。时间线列出随时间发生的事件；步骤条显示固定流程当前所处的位置。
- **仍在加载的内容**，例如某一步的字段在其数据到达之前：在步骤主体中显示 `Skeleton`。步骤条本身没有加载状态。
- **从多个选项中选择一个值**：用 `Select`、`RadioGroup` 或 `SegmentedControl`。步骤条不收集值，它只报告流程中的位置。

## 用法

为 `Stepper` 提供步骤以及进行中步骤的索引。`CurrentIndex` 从零开始计数。索引等于步骤数时，所有步骤都标记为已完成。步骤既可以像下例一样内联书写，也可以从集合绑定。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:Stepper CurrentIndex="1">
        <arc:Stepper.Steps>
            <arc:StepperStep Label="账号" Description="创建登录信息" />
            <arc:StepperStep Label="配送" Description="寄送到哪里" />
            <arc:StepperStep Label="支付" Description="银行卡或发票" />
            <arc:StepperStep Label="确认" Description="检查并确认" />
        </arc:Stepper.Steps>
    </arc:Stepper>
</UserControl>
```

`Stepper` 派生自 Avalonia 的 `TemplatedControl`，而不是项目控件。它负责整个流程：把步骤排布为一个面板、绘制步骤之间的连接线，并把流程作为一个整体做动画。每个步骤在面板中作为一个内部项绘制，这就是为什么进度跳跃时步骤条能按顺序填充连接线。由于它不是项目控件，因此没有 `ItemTemplate`；步骤内容始终是标签和描述。

两条规则决定了整个控件的行为：

- `CurrentIndex` 是唯一的数据来源。它之前的步骤已完成，它所在的步骤为当前步骤，之后的步骤为待办。小于零的值按零处理，大于步骤数的值按步骤数处理。
- 步骤的 `Error` 会覆盖其状态。即使带错误的步骤位于当前索引之前或之后，它也会显示为失败，因此只应在人必须修正的步骤上设置 `Error`。

### 结构

1. **标记**（每个步骤一个）：直径 28 px 的圆盘，带 1 px 的内描边，使用强边框色。步骤待办或进行中时显示步骤编号，完成时显示绘制的对勾，有错误时显示提醒标记。
2. **光晕**：当前步骤外 4 px 处的一圈环，使用强调色的弱化色。失败的步骤则改用强度为 16% 的危险色光晕。
3. **连接线**：两个标记之间 2 px 的线条。轨道为边框色；已到达的部分以强调色填充。
4. **标签**（`StepperStep.Label`）：步骤名称，14 px、中等字重，行高 20 px。已完成的步骤使用次要文字颜色，当前步骤和出错的步骤使用前景色，待办步骤使用弱化文字颜色。
5. **描述**（`StepperStep.Description`）：12 px 的文案，行高 16.8 px，位于标签下方 2 px，使用弱化文字颜色。`Error` 会以危险色替换它。
6. **说明文字**（仅紧凑模式）：当前步骤的标签与详情，上方留 12 px 间距。仅在紧凑模式的水平步骤条中显示。

尺寸来自步骤条的设计令牌。水平方向上，每个步骤占据自己的列，列与列之间留 16 px，标记与标签行之间留 12 px。垂直方向上，标记位于左侧，与文字之间留 12 px，每个步骤下方留 20 px（紧凑时为 24 px），首行标签下移 4 px，使其与标记居中对齐。

### 绑定步骤

`ItemsSource` 优先于内联的 `Steps`；设置它之后，内联步骤会被忽略。绑定一组 `StepperStep` 对象的集合。步骤条会监听集合以及每个步骤，因此修改某一步的 `Label`、`Description` 或 `Error` 会直接更新步骤条，而无需重建。

```xml
<arc:Stepper ItemsSource="{Binding Steps}"
             CurrentIndex="{Binding CurrentStep, Mode=TwoWay}" />
```

为每个步骤设置稳定的 `Id`。`Id` 是该步骤动效的键，因此当集合被重新排序或在其前面添加步骤时，该步骤会保留自己的状态动画。

### 绑定到视图模型

在 MVVM 应用中，视图模型拥有步骤、当前索引以及进入下一步的规则，步骤条只负责显示它们。使用 CommunityToolkit.Mvvm 时，下面的视图模型保存流程状态，并在对应步骤上标记支付失败。

```csharp
public partial class CheckoutViewModel : ObservableObject
{
    private readonly IOrderService _orders;

    public CheckoutViewModel(IOrderService orders)
    {
        _orders = orders;
    }

    public ObservableCollection<StepperStep> Steps { get; } = new()
    {
        new StepperStep("账号", "创建登录信息") { Id = "account" },
        new StepperStep("配送", "寄送到哪里") { Id = "shipping" },
        new StepperStep("支付", "银行卡或发票") { Id = "payment" },
        new StepperStep("确认", "检查并确认") { Id = "review" }
    };

    [ObservableProperty]
    private int _currentStep = 1;

    [RelayCommand]
    private async Task PayAsync()
    {
        var result = await _orders.PayAsync(OrderId);
        Steps[2].Error = result.IsSuccess ? null : result.Error;
        if (result.IsSuccess)
        {
            CurrentStep = 3;
        }
    }

    [RelayCommand]
    private void Continue()
    {
        CurrentStep = Math.Min(Steps.Count, CurrentStep + 1);
    }
}
```

```xml
<StackPanel Spacing="20">
    <arc:Stepper ItemsSource="{Binding Steps}"
                 CurrentIndex="{Binding CurrentStep}"
                 IsNavigable="True" />
    <arc:Button Content="继续" Command="{Binding ContinueCommand}" />
</StackPanel>
```

- `CurrentIndex` 默认为双向绑定，因此当人选择已完成的步骤时，视图模型能获知其返回的步骤。像上面那样显式写出 `Mode=TwoWay`，可以表明意图。
- 步骤条不会阻止移动。上例中的 `Continue` 只会向前移动；决定人能否离开某一步的规则应放在视图模型中，只有在通过校验时才设置 `CurrentIndex`。
- 在视图模型中为步骤设置 `Error`，并用 `null` 或空字符串将其清除。步骤条会把两者都视为“没有错误”。

### 错误与恢复

错误应属于人能够修正的那个步骤。状态立即改变，标记变为提醒，错误文字取代描述，因此原因会显示在需要操作的地方。

- 当服务端拒绝该步骤时（例如银行卡被拒、文件过大），设置 `Error`，并让人停留在该步骤。`CurrentIndex` 不变，因此流程仍停在原处。
- 值变化或人重试时立即清除 `Error`。已修正的步骤上残留的旧错误会被误读为第二次失败。
- 已完成步骤上的错误（例如审核之后被撤销的支付）会把该步骤变为提醒，而当前索引保持不变。可以考虑把当前索引移回该步骤，让人在上下文中看到原因。

### 垂直方向与错误

<Demo name="VerticalWithError" />

将 `Orientation` 设为 `Vertical` 即得到时间线：标记位于左侧，标签与描述位于其旁边，连接线向下延伸。它适用于侧边面板、抽屉或分栏布局的一列，步骤从上到下阅读。如下例所示为它设置宽度，使标签换行，而不是溢出该列。

```xml
<arc:Stepper ItemsSource="{Binding Steps}"
             CurrentIndex="2"
             Orientation="Vertical"
             Width="320" />
```

### 紧凑与当前详情

宽度小于 480 px 的水平步骤条会自动切换为紧凑模式。设置 `IsCompact="True"` 可以在任何宽度下请求紧凑模式。`IsCompact="False"` 并不会关闭自动规则：窄的水平步骤条仍然是紧凑的，因此完整的步骤条应至少有 480 px 宽。

<Demo name="Compact" />

在紧凑模式下，标记保留，标签隐藏，步骤条下方的说明文字会标出当前步骤及其描述。垂直步骤条不会按此规则变为紧凑，因为它有足够的空间容纳标签。

`Details` 决定显示哪些描述。`StepperDetails.All`（默认值）显示每个描述。`Current` 只显示当前步骤的描述，使长流程在狭小空间中保持简洁。无论此设置如何，错误始终会显示。

<Demo name="CurrentDetails" />

```xml
<arc:Stepper ItemsSource="{Binding Steps}"
             CurrentIndex="2"
             IsCompact="True" />
<arc:Stepper ItemsSource="{Binding Steps}"
             CurrentIndex="1"
             Details="Current" />
```

### 可导航的步骤

设置 `IsNavigable` 后，人可以通过点击或键盘返回已完成的步骤。只读的步骤条（默认值）仅显示进度：其项不接收焦点，也忽略点击。当步骤在流程回退之前必须执行某些工作（例如保存当前步骤的草稿）时，请处理 `StepSelected`。

```xml
<arc:Stepper ItemsSource="{Binding Steps}"
             CurrentIndex="{Binding CurrentStep, Mode=TwoWay}"
             IsNavigable="True" />
```

```csharp
stepper.StepSelected += (_, e) =>
{
    // e.Index 是被选中的已完成步骤；e.Step 是它的 StepperStep。
    SaveDraft();
};
```

`StepSelected` 是路由事件。它的参数包含 `Index`（所选步骤从零开始的索引）和 `Step`（`StepperStep` 本身）。如果没有处理程序将事件标记为已处理，所选步骤就会成为当前步骤。若处理程序设置 `e.Handled = true`，则由它接管：索引不会改变，因此当移动应当发生时，视图模型必须自行设置 `CurrentIndex`。

代码可以像点击一样通过 `Select(int index)` 选择步骤。当 `IsNavigable` 关闭、索引超出范围，或索引不在当前步骤之前时，它不起作用。

只有已完成的步骤可以被选择。当前步骤与待办步骤不可点击；在已完成的步骤上按下指针时，其标记会略微缩小，直到按下结束；只有在指针于其上松开时，该步骤才会被选中。

### 示例：为新工作区设置账单

SaaS 管理控制台引导所有者完成四个步骤：命名工作区、邀请团队、添加账单并确认。由于银行卡被拒，账单步骤已经失败。步骤条停留在该步骤上，标记变为提醒，错误文字取代描述，因此人能看到需要修正什么以及在哪里修正。页脚提供一个“返回”操作（次要按钮）和一个主要操作，因此下一步很明确。卡片与页脚都位于使用标准表面与边框的面板中，使整个流程在页面上呈现为一个整体。

![带有失败账单步骤与下一步操作的工作区设置卡片](images/SetupWorkspace.png)

```xml
<Border Width="680"
        Padding="24"
        CornerRadius="{arc:ArcDesignTokenResource RadiusPanel}"
        Background="{arc:ArcDesignTokenResource Surface}"
        BorderBrush="{arc:ArcDesignTokenResource Border}"
        BorderThickness="1">
    <StackPanel Spacing="24">
        <TextBlock Text="Set up Acme Analytics"
                   FontSize="{arc:ArcDesignTokenResource TypeLG}"
                   FontWeight="{arc:ArcDesignTokenResource WeightMedium}"
                   Foreground="{arc:ArcDesignTokenResource Foreground}" />
        <arc:Stepper CurrentIndex="2" Label="Workspace setup" CompleteLabel="Setup complete">
            <arc:Stepper.Steps>
                <arc:StepperStep Id="workspace" Label="Workspace" Description="Acme Analytics" />
                <arc:StepperStep Id="team" Label="Team" Description="Invite 6 people" />
                <arc:StepperStep Id="billing"
                                 Label="Billing"
                                 Description="Card or invoice"
                                 Error="Card declined. Try another card." />
                <arc:StepperStep Id="review" Label="Review" Description="Check and confirm" />
            </arc:Stepper.Steps>
        </arc:Stepper>
        <StackPanel Orientation="Horizontal" Spacing="8" HorizontalAlignment="Right">
            <arc:Button Content="Back" Variant="Secondary" />
            <arc:Button Content="Continue" />
        </StackPanel>
    </StackPanel>
</Border>
```

示例使用内联步骤，因为内容是固定的。对于运行时会变化的流程，应按前文所示绑定 `ItemsSource`。失败的步骤保持其在序列中的位置：步骤条从不跳过步骤，视图模型决定人能否离开失败的步骤。

### 文案规范

- **步骤标签**：一到两个词，句首大写，用于命名步骤，例如“配送”“支付”“确认”。避免动词和完整句子，细节由描述承担。
- **描述**：关于步骤内容的简短提示，例如“银行卡或发票”“邀请 6 人”。它显示在标签下方，是标签不足以说明时人们会阅读的内容。
- **错误**：说明哪里出了问题以及该怎么做，用词与步骤名称保持一致。错误会取代描述，因此它必须能独立成立。
- **完成标签**：说明流程已完成的简短陈述，例如“设置完成”或“所有步骤已完成”。它会向辅助技术播报，并显示在紧凑模式的说明文字中。
- **步骤条标签**：整个流程的名称，例如“结账进度”。它是该组的无障碍名称。

| 不要写 | 改为 |
|---|---|
| 第 1 步 | 账号 |
| 请在此输入您的收货地址 | 配送 |
| 错误！ | 银行卡被拒，请换一张卡。 |
| 完成 | 所有步骤已完成 |
| 进入下一步 | 继续 |
| 进度 | 结账进度 |

### 最佳实践

**推荐**

- 每个流程使用一个步骤条，并让步骤是同一类东西：一组页面，而不是页面与操作的混合。
- 把错误放在人必须修正的步骤上，并让当前索引停留在该步骤，使原因与操作位于同一处。
- 只有在返回已完成的步骤是安全的时候才使用 `IsNavigable`。先保存当前步骤的草稿，可以在视图模型中完成，也可以在 `StepSelected` 中完成。
- 当步骤来自数据时，为每个步骤设置稳定的 `Id`，使每个步骤在变化时保持自己的动效。
- 为纵向步骤条设置 `Width`，使标签在列内换行。

**避免**

- 把步骤条用于人可以从任意位置进入的流程。它的顺序由 `CurrentIndex` 决定，无法跳到待办步骤；需要自由导航时请使用 `Tabs`。
- 在存在未完成步骤的流程中，把 `CurrentIndex` 设到末尾来表示“已完成”。等于步骤数的索引会把所有步骤都标记为已完成，因此流程必须真正完成。
- 在 `Label` 中放入长文字。长标签会在步骤头部换行，并把其他步骤挤出对齐；应把细节移到 `Description` 中。
- 同时在多个步骤上设置 `Error`。连续多个提醒会让人不清楚该先修正哪一步；应标记第一个失败的步骤，并在页面上说明其余问题。
- 仅依靠颜色表示状态。每种状态都有自己的标记：数字、对勾和提醒标记，因此即使没有颜色，流程也能读懂。

### 常见问题

**当某个步骤无效时，步骤条会阻止人继续前进吗？** 不会。步骤条只显示 `CurrentIndex` 与 `Error`。在提高索引之前，请在视图模型中检查规则，并在失败的步骤上设置 `Error`。

**为什么我不能用 Tab 键切换到当前步骤？** 开启 `IsNavigable` 后，Tab 会停在每个已完成的步骤上。当前步骤需要从已完成的步骤用方向键到达，`End` 会移动到它。这样即使在长流程中，Tab 顺序也能保持简短。

**如何在窄屏上只显示当前步骤的标题？** 在水平步骤条上设置 `IsCompact="True"`。标记保留，下方的说明文字会标出当前步骤及其描述。不设置 `IsCompact` 时，宽度小于 480 px 的水平步骤条会自动这样做。

**如何修改辅助技术使用的默认名称？** 为流程设置 `Label`（默认为 "Progress"），为结束状态设置 `CompleteLabel`（默认为 "All steps complete"）。每个步骤的名称由其标签和状态生成，例如“账号，已完成”。

**流程运行时步骤数量变化会怎样？** 集合变化时，步骤条会重建其步骤，并把索引限制在新数量的范围内显示；它不会改写 `CurrentIndex`。请让视图模型中的索引与集合保持同步，使人不会在变化后落到另一个步骤上。

## API 参考

<!-- api: Stepper -->

<!-- api: StepperStep -->

`StepSelected` 的事件参数：

| 成员 | 类型 | 含义 |
|---|---|---|
| `Index` | `int` | 所选已完成步骤从零开始的索引 |
| `Step` | `StepperStep` | 所选的步骤 |

## 无障碍

- `Label` 为步骤条命名，默认为 "Progress"。将它设为流程的名称，例如“结账进度”。
- 每个步骤都以其状态命名，例如“账号，已完成”“确认，未开始”或“账单，错误”，因此失败的步骤既可通过名称识别，也可通过提醒标记识别。
- 当前步骤被标记为当前步骤，步骤条会报告其位置，例如“第 3 步，共 4 步：支付”。
- 步骤变化会以礼貌的方式播报，因此无需移动焦点即可读到新位置。当所有步骤都已完成时，改为播报 `CompleteLabel`。
- 只读的步骤条不接收焦点。它会作为一组被读出，连同其步骤及各自的状态。
- 可导航的步骤条会把焦点保留在步骤上。焦点以一圈环绘制，位于步骤头部外 4 px。
- 尚未到达的步骤不可获得焦点，并会被 Tab 跳过。

### 键盘

仅当 `IsNavigable` 开启时按键才生效。否则步骤条不接收焦点。

| 按键 | 操作 |
|---|---|
| `Tab` / `Shift+Tab` | 在已完成的步骤之间移动；当前步骤与待办步骤不是 Tab 停靠点 |
| `Right` / `Down`、`Left` / `Up` | 移到下一个或上一个可到达的步骤（已完成或当前）；在从右到左的水平步骤条中，`Left` 与 `Right` 互换 |
| `Home` | 移到第一个可到达的步骤 |
| `End` | 移到最后一个可到达的步骤，通常是当前步骤 |
| `Enter` / `Space` | 选择获得焦点的已完成步骤，与点击相同；对当前或待办步骤不起作用 |

## 动效

- 进度跨越多个步骤时，连接线会依次填充，新的当前标记会出现一圈逐渐扩大的光晕。
- 步骤完成时，数字变为绘制的对勾；失败时，变为提醒标记。
- 变化的描述会从柔和的模糊中升起，它占据的空间以弹簧效果调整到新高度。
- 减弱动效时，所有变化立即生效。

| 变化 | 完整 | 减弱 | 关闭 |
|---|---|---|---|
| 进度向前或向后移动 | 连接线依次填充或排空，每条相隔 80 ms；光晕在新的当前步骤周围扩大 | 立即完成 | 立即完成 |
| 步骤完成 | 数字从 0.5 弹出，伴随轻微模糊，其对勾笔画被绘制出来 | 立即完成 | 立即完成 |
| 步骤失败 | 标记变为提醒，错误文字取代描述 | 立即完成 | 立即完成 |
| 描述变化 | 文字从柔和的模糊中升起，行以弹簧效果调整到新高度 | 立即完成 | 立即完成 |
| 标记与标签颜色 | 标记颜色在 240 ms 内交叉过渡；标签颜色以标准补间变化 | 立即完成 | 立即完成 |

按下已完成的步骤时，其标记会缩小到 0.92。指针悬停在已完成的步骤上时，其标签会提亮为前景色。

## 相关组件

- `Progress`：工作以百分比而非步骤计量时使用。
- `Tabs`：人可以以任意顺序切换分区时使用。
- `Skeleton`：某个步骤的内容仍在加载时使用。

| 组件 | 适用场景 |
|---|---|
| `Progress` | 一个值显示单个任务完成了多少 |
| `Tabs` | 分区彼此平等，可以任意顺序打开 |
| `Timeline` | 随时间发生的事件列表本身就是内容，例如订单历史 |
| `Skeleton` | 某个步骤的内容正在加载，且尚未有形状 |
| `Breadcrumb` | 人需要看到通往某个页面的路径，而非流程中的进度 |
| `Select` / `RadioGroup` | 步骤要求从一组选项中选择一个值 |
