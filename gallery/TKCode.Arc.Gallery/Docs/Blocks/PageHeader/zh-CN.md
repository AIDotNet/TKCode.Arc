---
title: 页头
summary: 项目页头会随滚动收拢为紧凑栏，标签页的数量会滚动更新。
source-hash: de9e8f154f51
---

<Demo name="Preview" />

## 何时使用

`PageHeader` 是记录或项目页面的框架：页头包含页面路径、标题、状态、描述、元信息行、操作和标签页，位于所选标签页可滚动内容的上方。它位于页面顶部，告诉用户当前处于哪条记录中、可以对它做什么，并在主体滚动时保持触手可及。

- **开发者工具中的项目或仓库页面**：“Checkout service” 及其状态、Share 和 Follow 操作，以及 Overview、Issues（带未关闭事项的数量）和 Deployments 标签页。
- **CRM 或内部管理控制台中的客户记录**：“Acme Analytics” 以及元信息行中的状态和负责人，并有 Contacts、Opportunities 和 Activity 标签页。
- **SaaS 管理后台中的账单或订阅页面**：“Acme Analytics” 带状态徽标，主操作为 Upgrade，次要操作为 Export，并有 Invoices（带数量）和 Payment methods 标签页。
- **电商中的订单详情页**：“Order 10482” 带 “Shipped” 状态，Print 操作，以及 Items、Shipments 和 Payments 标签页。
- **主体会滚动的长记录页面**：当主体滚动超过 16 像素时，页头会收拢为紧凑栏，保留标题和状态在视野中。

## 何时不用

- **在应用的各个部分之间切换**：用 `Sidebar`。页头只在一条记录内导航。
- **在卡片或面板内切换视图**：用 `Tabs`，它不需要页面框架。
- **带有自身标题的对话框或设置表单**：使用 `Dialog` 的标题，或普通的标题。
- **只有单一路径，没有标签页或操作**：只用 `Breadcrumb`，它不会占用页头的高度。
- **带有自身工具栏的列表或表格**：在 `Table` 上方使用 `FilterToolbar`。页头的高度由主体决定，因此放在其中的表格可用的空间会少于页面所需。

## 用法

`PageHeader` 派生自 `SelectingItemsControl`。每个标签页都是一个 `PageHeaderTab` 项，因此选择的行为与列表一致：`SelectedIndex`、`SelectedItem` 和 `SelectionChanged` 的表现与列表相同，禁用的标签页会被跳过。每个 `PageHeaderTab` 派生自 `HeaderedContentControl`：`Header` 是它的标签文字，`Count` 是可选的数字，`Content` 是它的主体。为页头设置固定高度，或把它放在一个有高度的行中，使主体可以滚动。

```xml
<arc:PageHeader Title="结账改版"
                Status="进展正常"
                Description="围绕已保存的支付方式重建移动端结账。"
                Height="480">
    <arc:PageHeaderTab Header="概览">
        <TextBlock Text="进度、里程碑和近期活动。" />
    </arc:PageHeaderTab>
    <arc:PageHeaderTab Header="问题" Count="9">
        <TextBlock Text="未关闭的问题。" />
    </arc:PageHeaderTab>
</arc:PageHeader>
```

### 结构

1. **框架**：1 px 边框，表面色背景，圆角 16 px。页头顶部内边距为 16 px，两侧为 28 px（宽度小于 560 px 时两侧为 18 px）。主体顶部与两侧内边距为 28 px，底部为 84 px。
2. **首部单元格**：面包屑（`Breadcrumbs`），或在页头收拢后显示的紧凑标题（`Title`）。两者占据相同的位置，并相互替换。
3. **操作行**：次要的 `Actions` 为小按钮，彼此相距 8 px；“More actions” 按钮（36 px 的方形，带 17 px 的省略号图形）；以及 `PrimaryAction`。操作行与首部单元格保持 16 px 的距离。
4. **标题**（`Title`）：最大的展示字号；宽度小于 560 px 时使用再小一级的字号，与状态之间相距 14 px。
5. **状态**（`Status`、`StatusTone`）：带 6 px 圆点的徽标，颜色取自语气。紧凑栏中同样会显示它。
6. **描述**（`Description`）：次要文字颜色的正文，约 60 个字符处换行。
7. **元信息**（`Meta`）：描述下方的任意内容，使用极小号的弱化文字。
8. **标签行**：标签页高 44 px，两侧内边距 10 px，字重为中等。`Count` 位于标签文字之后 6 px，其数字会滚动。该行延伸到框架边缘，因此第一个标签文字与标题对齐。
9. **指示条**：所选标签页下方 2 px 的条，两侧各内缩 10 px。
10. **主体**：所选标签页的内容，位于标签行下方的滚动区域中。

### 标题与标签页

页头只需要标题和标签页。没有面包屑、状态或操作时，页头行保持为空，直到页头收拢并显示紧凑标题。`Count` 是 `int?`：`null` 会隐藏数字，每次变化时数字都会滚动到新值。

<Demo name="Minimal" />

### 面包屑、状态与元信息

`Breadcrumbs` 接收通往页面的路径，根在前，可以是字符串、数据项或 `BreadcrumbItem`。最后一项是当前页面，永远不是链接。选择另一个面包屑会触发 `BreadcrumbClick`，其参数包含 `Item` 和 `Index`（0 为根）。`Status` 会在标题旁添加一个带圆点的徽标，颜色取自 `StatusTone`（默认为 success）。`Meta` 可以接收任意内容，用于描述下方的那一行，例如成员、负责人和日期。

```xml
<arc:PageHeader Title="结账改版" Status="有风险" StatusTone="Warning">
    <arc:PageHeader.Meta>
        <TextBlock Text="由 Emma Collins 负责" />
    </arc:PageHeader.Meta>
</arc:PageHeader>
```

```csharp
header.Breadcrumbs = new[] { "Northline", "项目", "结账改版" };
header.BreadcrumbClick += (_, e) => router.Open(e.Index);
```

### 操作

`Actions` 是页头行中的小型次要按钮。`MoreActions` 始终位于 “More actions” 菜单中，排在收拢的次要操作之后，并以分隔线隔开。`PrimaryAction` 是行末的一个小型主按钮，在 460 像素以下时只显示图标。每个 `PageHeaderAction` 都有 `Label`、`Icon`（Lucide 图形，例如 `ArcIcons.Bell`），以及 `Command` 或 `Click` 事件。`PageHeader.ActionInvoked` 在一处报告所有操作，操作来自菜单时 `IsFromMenu` 会被设为 true。

对会删除数据的菜单操作（例如删除或取消）使用 `IsDestructive="True"`。它会以危险色绘制菜单行。`IsEnabled="False"` 会禁用按钮并让其菜单行变淡。`IsLoading="True"` 会显示加载指示器并忽略点击。`Invoke()` 从代码运行操作，并遵守这些状态。

当面包屑的可用宽度将小于 240 像素时，次要操作会收拢到 “More actions” 菜单的顶部。宽度再次达到 272 像素时它们会恢复显示，因此页头不会在阈值处闪烁。只监视页头的宽度，因此变长的标签（Follow 变为 Following）不会让你刚刚按下的按钮被收拢。

<Demo name="FoldingActions" />

修改 `Label` 和 `Icon` 即可原位更新按钮。当菜单行需要不同的文字时，设置 `MenuLabel` 和 `MenuIcon`。

```csharp
follow.Click += (_, _) =>
{
    following = !following;
    follow.Label = following ? "已关注" : "关注";
    follow.MenuLabel = following ? "取消关注" : "关注";
};
share.IsLoading = true;
```

### 收拢

当主体滚动超过 16 像素时，页头会收拢为包含标题和状态的紧凑栏。只有当主体重新回到距顶部 4 像素以内时，它才会再次展开，因此页头不会在阈值附近闪烁。`IsCondensed` 表示当前状态。选择紧凑标题会调用 `ScrollToTop()`，它会滚回顶部，并把焦点移到标题上。只有当主体足够长，在页头变小后仍能保持滚动状态时，页头才会收拢。

在页头收拢时切换标签页，会显示新标签页的顶部，并保持紧凑栏。`RevealTab(index)` 可以在代码中做同样的事，例如在另一个标签页中创建项目之后。对于缺失或被禁用的标签页，它返回 `false`。

### 数据项

将 `ItemsSource` 绑定到你自己的数据项。`ItemTemplate` 渲染标签文字，`ContentTemplate` 渲染主体。`SelectedIndex` 和 `SelectedItem` 可以双向绑定。

<Demo name="DataItems" />

### 窄宽度

布局跟随控件的宽度，而不是窗口的宽度。宽度低于 560 像素时，内边距变小，根面包屑和紧凑徽标隐藏，标题降一级字号，标签行的远端边缘会渐隐。宽度低于 460 像素时，主操作只显示图标。在页头第一次测量之前，宽度小于 720 像素的页头会以收拢的次要操作开始，因此加载时不会闪烁。

### 绑定到视图模型

视图模型拥有所选标签页、计数和命令。页头显示它们，并报告用户的选择。使用 CommunityToolkit.Mvvm 时：

```csharp
public abstract class BillingApi
{
    public abstract Task ExportInvoicesAsync();
}

public partial class BillingPageViewModel : ObservableObject
{
    private readonly BillingApi _api;

    public BillingPageViewModel(BillingApi api)
    {
        _api = api;
        ExportAction = new PageHeaderAction { Label = "导出", Icon = ArcIcons.Download, Command = ExportCommand };
    }

    public PageHeaderAction ExportAction { get; }

    [ObservableProperty]
    private int _selectedTabIndex;

    public ObservableCollection<string> Invoices { get; } = new();

    [ObservableProperty]
    private int _openInvoiceCount;

    [ObservableProperty]
    private bool _isExporting;

    [RelayCommand]
    private async Task ExportAsync()
    {
        IsExporting = true;
        try
        {
            await _api.ExportInvoicesAsync();
        }
        finally
        {
            IsExporting = false;
        }
    }
}
```

```xml
<arc:PageHeader Title="Acme Analytics"
                Status="生效中"
                StatusTone="Success"
                SelectedIndex="{Binding SelectedTabIndex, Mode=TwoWay}">
    <arc:PageHeaderTab Header="发票" Count="{Binding OpenInvoiceCount}">
        <ItemsControl ItemsSource="{Binding Invoices}" />
    </arc:PageHeaderTab>
</arc:PageHeader>
```

- `SelectedIndex` 是双向的，因此视图模型可以把页头切换到某个标签页，用户切换标签页时也会更新该属性。
- `Count` 接收绑定，视图模型更改其值时，数字会滚动。
- `PageHeaderAction` 不是可视元素，因此它的 `Command` 需要在代码中设置，如上所示。它的 `IsLoading` 同样由视图模型设置（在 `PropertyChanged` 处理程序中执行 `ExportAction.IsLoading = vm.IsExporting`），之后按钮会显示加载指示器并忽略点击。
- 对于可能不可用的操作，例如已经生效的升级，请在命令上使用 `CanExecute`。菜单行和按钮会跟随命令的状态。

### 决定页头放什么

页头的空间有限，因此每个位置只放一类内容：

| 位置 | 放入 | 避免放入 |
|---|---|---|
| `Title` 和 `Status` | 记录名称，以及一个状态词，例如“生效中”或“有风险” | 句子、计数、日期 |
| `Description` | 关于记录的一两句话 | 页面的操作说明 |
| `Meta` | 负责人、成员、日期和少量事实 | 操作和表单控件 |
| `Actions` | 两到三个常用操作 | 破坏性操作 |
| `MoreActions` | 偶尔使用的操作和破坏性操作，例如取消或删除 | 最常用的操作 |
| `PrimaryAction` | 推动记录前进的那一个操作 | 多于一个操作 |

### 示例：账单账户页面

一个 SaaS 工作区的账单页面，标题中包含账户名称和状态，元信息行中是套餐详情，并有两个次要操作。主操作是“升级套餐”，而具有破坏性的取消操作放在菜单中，以免被误按。标签页涵盖账户的概览、带未关闭发票数量的发票，以及付款方式。健康状态的徽标使用 success 语气。

![账单账户页头，包含面包屑、状态徽标、操作和发票标签页](images/BillingHeader.png)

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc"
             xmlns:generic="using:System.Collections.Generic"
             Width="720">
    <arc:PageHeader Height="440"
                    Title="Acme Analytics"
                    Status="Active"
                    StatusTone="Success"
                    Description="Plan, invoices and payment methods for the Acme Analytics workspace."
                    TabsLabel="Billing sections">
        <arc:PageHeader.Breadcrumbs>
            <generic:List x:TypeArguments="x:String">
                <x:String>Northline</x:String>
                <x:String>Billing</x:String>
                <x:String>Acme Analytics</x:String>
            </generic:List>
        </arc:PageHeader.Breadcrumbs>
        <arc:PageHeader.Meta>
            <TextBlock Text="Billed yearly, renews March 3, 2027" />
        </arc:PageHeader.Meta>
        <arc:PageHeader.Actions>
            <arc:PageHeaderAction Label="Export" Icon="{x:Static arc:ArcIcons.Download}" />
        </arc:PageHeader.Actions>
        <arc:PageHeader.MoreActions>
            <arc:PageHeaderAction Label="Copy workspace ID" Icon="{x:Static arc:ArcIcons.Link2}" />
            <arc:PageHeaderAction Label="Cancel subscription"
                                  Icon="{x:Static arc:ArcIcons.Trash2}"
                                  IsDestructive="True" />
        </arc:PageHeader.MoreActions>
        <arc:PageHeader.PrimaryAction>
            <arc:PageHeaderAction Label="Upgrade plan" Icon="{x:Static arc:ArcIcons.Plus}" />
        </arc:PageHeader.PrimaryAction>
        <arc:PageHeaderTab Header="Overview">
            <StackPanel Spacing="8">
                <TextBlock Text="Team plan, 25 seats, 6.2 M of 10 M events used this month." TextWrapping="Wrap" />
                <TextBlock Text="The next invoice of $4,800.00 is issued on March 1, 2027." TextWrapping="Wrap" />
            </StackPanel>
        </arc:PageHeaderTab>
        <arc:PageHeaderTab Header="Invoices" Count="2">
            <StackPanel Spacing="8">
                <TextBlock Text="inv_2026_0412   Open   $4,800.00   Oct 1, 2026" />
                <TextBlock Text="inv_2026_0311   Paid   $4,800.00   Sep 1, 2026" />
            </StackPanel>
        </arc:PageHeaderTab>
        <arc:PageHeaderTab Header="Payment methods" Count="2">
            <TextBlock Text="Visa ending in 4242 is the default card." TextWrapping="Wrap" />
        </arc:PageHeaderTab>
    </arc:PageHeader>
</UserControl>
```

### 文案规范

- **标题**：记录的名称，即用户搜索时使用的名字：“Acme Analytics”“Order 10482”。避免使用 “Page:” 之类的前缀。
- **状态**：一到两个词，描述状态而不是整句：“生效中”“有风险”“已发货”。选择与状态相符的语气，并且只为需要关注的状态使用 Warning 和 Danger。
- **描述**：一到两句话，说明这条记录的用途。使用句子式大小写，并以句号结尾。
- **标签页名称**：句子式大小写的名词，例如“发票”或“付款方式”。计数只能是数字。
- **操作**：句子式大小写的动词加对象，例如“导出发票”“升级套餐”。把罕用或具有破坏性的操作放入菜单，并说明它们会删除什么。

| 不要写 | 改为 |
|---|---|
| 页面：账单设置 | Acme Analytics |
| 状态：该记录目前状况良好 | 生效中 |
| 导出 | 导出发票 |
| 全部删除 | 取消订阅 |
| 发票（2 张未付！） | 发票，并显示未关闭发票的数量 |

### 最佳实践

**推荐**

- 页头高度保持固定，或为它设置一个有高度的行。主体在其中滚动，收拢逻辑依赖这个高度来判断页头能否收拢。
- 把破坏性操作放入 `MoreActions`。菜单多一步操作，因此取消操作不会与常用操作只隔一次点击。
- 当视图模型需要了解或更改标签页时，把 `SelectedIndex` 设为双向绑定，使深层链接可以打开某个标签页。
- 在另一个标签页中创建项目后，使用 `RevealTab(index)`。这样新项目可见，而页头不会离开紧凑状态。
- 为每个操作设置简短的 `Label`。在宽页头中它是按钮文字；在 460 像素以下、主操作只显示图标时，它是无障碍名称。

**避免**

- 在 `Meta` 中放置表单控件。元信息行是极小号的弱化文字，在那里的输入框既不易阅读，也不便于用键盘操作。
- 每次切换标签页都调用 `ScrollToTop()`。它会把焦点移到标题上，使用户正在使用的内容失去焦点。
- 同时为多个操作设置 `IsLoading`。一个加载指示器说明正在加载什么；多个指示器则会让人以为页面卡住了。
- 为每个标签页都显示计数。计数告诉用户有多少项在等待处理；在只显示文档的标签页上，它只是噪音。
- 把页头做得太高，以致主体无法滚过收拢后的可视区域。主体较短时会保留完整页头，因此页面不会收拢，标题也保持较大。

### 常见问题

**为什么我的次要操作被移入 “More actions” 菜单？** 当面包屑的可用宽度将小于 240 像素时，页头会收拢它们；宽度达到 272 像素时再恢复。缩短面包屑的标签，把最少使用的操作移入 `MoreActions`，或加宽页头。`AreActionsFolded` 表示当前状态。

**为什么滚动时页头不收拢？** 只有当主体足够高，在页头变小后仍能保持滚过展开线时，页头才会收拢。主体较短时会保留完整页头，因此页面不会跳动。请为主体放入比收拢后可视区域更高的内容，或保持页头展开。

**如何从链接打开特定的标签页？** 将 `SelectedIndex` 设为标签页的索引；如果页头已经收拢，并且主体应从新标签页的顶部开始，则调用 `RevealTab(index)`。当索引缺失或标签页被禁用时，`RevealTab` 返回 `false`。

**可以用方向键选择禁用的标签页吗？** 不可以。方向键以及 Home、End 都会跳过禁用的标签页，因此选择会移到下一个启用的标签页。

**切换标签页时页头会保留紧凑标题吗？** 会。页头收拢时切换标签页，会显示新标签页的顶部并保持紧凑栏。选择标签页不会展开页头。

## API 参考

<!-- api: PageHeader -->

<!-- api: PageHeaderTab -->

<!-- api: PageHeaderAction -->

## 无障碍

- 该控件是一个以 `Title` 命名的区域。标题是二级标题。
- 面包屑是导航地标。当前页面会被标记，且不是链接。
- 标签行以 `TabsLabel` 命名。左右方向键在标签页之间移动并选中它们，并在两端循环。Home 和 End 分别移到第一个和最后一个标签页。只有所选标签页在 Tab 顺序中。
- 溢出按钮以 `MoreActionsLabel` 命名。菜单支持方向键、Home、End、输入字符跳转、Enter，以及 Escape 关闭菜单并把焦点返回按钮。
- 紧凑标题的名称为 “Title, back to top”（`BackToTopLabel`）。页头中被隐藏的部分会离开 Tab 顺序，并忽略指针。
- 在 460 像素以下，主按钮保留其文字作为无障碍名称。

### 键盘

| 按键 | 操作 |
|---|---|
| 标签页上的 `Left` / `Right` | 选择上一个或下一个启用的标签页；在两端循环 |
| 标签页上的 `Home` / `End` | 选择第一个或最后一个启用的标签页 |
| `Tab` / `Shift+Tab` | 进入或离开标签行；只有所选标签页是停留点 |
| “More actions” 上的 `Enter` | 打开菜单；方向键在各行之间移动 |
| 菜单中的 `Escape` | 关闭菜单，并把焦点返回其按钮 |
| 面包屑上的 `Enter` | 以该项及其索引触发 `BreadcrumbClick` |
| 紧凑标题上的 `Enter` | 滚动到顶部，并把焦点移到标题 |

## 动效

- 收拢：开场部分在平滑弹簧上收起高度。标题向左上角缩小至 62%，并伴随轻微模糊地淡出。面包屑向上移开，紧凑标题从柔和的模糊中升起。
- 操作收拢：次要操作在平滑弹簧上收起宽度并淡出。只在右边缘裁切，因此它们收拢到溢出按钮之下。
- 标签页：指示条在变形弹簧上在标签页之间滑动。计数逐位滚动。新的主体从移动方向一侧滑入 12 像素，旧的主体淡出。
- 减弱动效只保留淡入淡出。高度与宽度立即变化，主体交叉淡化。关闭动效时，所有变化立即完成。

| 变化 | 完整 | 减弱 | 关闭 |
|---|---|---|---|
| 页头收拢或展开 | 开场部分在平滑弹簧上收起；标题缩小并淡出 | 高度立即变化；标题与面包屑淡出 | 立即完成 |
| 紧凑标题出现 | 从柔和的模糊中升起 | 淡入 | 立即完成 |
| 次要操作收拢 | 宽度在平滑弹簧上收起，在右边缘裁切 | 宽度立即变化 | 立即完成 |
| 标签页切换 | 主体从移动方向一侧滑入 12 px；旧主体淡出 | 主体交叉淡化 | 立即完成 |

## 相关组件

- `Breadcrumb` 用于单独的路径。
- `Tabs` 用于卡片或面板内的视图。
- `Badge` 用于状态标签。
- `DropdownMenu` 用于操作菜单。

| 组件 | 适用场景 |
|---|---|
| `Sidebar` | 用户在整个应用的各个部分之间切换 |
| `Tabs` | 视图位于卡片或面板内，没有页面框架 |
| `Breadcrumb` | 只需要路径，没有标签页或操作 |
| `Dialog` | 标题和操作属于打断页面的任务 |
| `FilterToolbar` | 表格或列表需要在行上方设置自己的筛选器 |
| `DropdownMenu` | 操作菜单从页面其他位置的按钮中弹出 |
