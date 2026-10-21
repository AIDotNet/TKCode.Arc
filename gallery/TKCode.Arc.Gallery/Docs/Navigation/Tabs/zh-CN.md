---
title: 标签页
summary: 在同一语境中切换相关内容。
source-hash: d1602006c5ed
---

<Demo name="Default" />

## 何时使用

`Tabs` 把一个对象的界面拆分为并列的视图，例如客户记录上的概览、活动与设置。人每次选择一个视图，每个视图在标签列表下方都有自己的面板。切换视图时标签列表保持不动，因此人在同一条记录、订单或工作区内不会迷失方向。

- **CRM 中的客户记录**：概览、订单、发票与备注都是同一账户的视图。账户标题位于标签页上方，选中的标签决定面板中显示哪个列表。
- **工作区或项目页面**：概览、活动与设置。各视图彼此并列，因此没有哪一个是步骤序列中的一环。
- **按状态筛选同一份数据**：在同一份发票列表上使用“全部”“未结”“逾期”与“已付”标签，每个标签在标题中显示其计数。
- **桌面工具中的大量视图**：邮件客户端的文件夹（收件箱、草稿、已发送、定时发送），窗口较窄时带有边缘滚动按钮。选中的文件夹始终会滚动到可见位置。

## 何时不用

- **选择一个没有面板的紧凑值**，例如日、周或月的范围：使用 `SegmentedControl`。选项本身没有内容，标签面板不会带来任何价值。
- **按顺序阅读的堆叠内容**，例如结账时的条款：使用 `Accordion`，每个分区在原位展开，人可以同时阅读多个分区。
- **翻阅结果的页面**：使用 `Pagination`。标签页用于切换视图，而不是翻阅同一集合的页面。
- **显示页面在层级结构中的位置**：使用 `Breadcrumb`。面包屑指向上层路径，而标签页彼此并列。
- **必须按顺序完成的步骤**，例如配送、付款与确认：使用 `Stepper`。在结账中，人不能像切换标签页那样跳过前面的步骤。

## 用法

项是 `TabItem` 容器。标题是触发器，内容是面板。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:Tabs AutomationProperties.Name="工作区">
        <arc:TabItem Header="概览">
            <TextBlock Text="本季营收增长 12%。" />
        </arc:TabItem>
        <arc:TabItem Header="活动">
            <TextBlock Text="Maya 在两小时前合并了 Billing v2。" />
        </arc:TabItem>
        <arc:TabItem Header="设置">
            <CheckBox Content="邮件摘要" IsChecked="True" />
        </arc:TabItem>
        <arc:TabItem Header="已归档" IsEnabled="False" />
    </arc:Tabs>
</UserControl>
```

`Tabs` 派生自 Avalonia 的 `SelectingItemsControl`，因此选择行为与任何选择型列表一致：`SelectedIndex`、`SelectedItem` 与 `SelectionChanged` 的用法相同，控件加载时会选中第一个标签页。标签列表本身不接收焦点，每个 `TabItem` 才接收。`TabItem` 派生自 `HeaderedContentControl`，因此 `Header` 是它的触发器，`Content` 是它的面板。禁用的标签页会被方向键跳过，也无法被选中。

### 结构

1. **标签列表**（`Tabs`）：在次要表面色上的 1 px 边框，颜色为边框色，圆角与控件一致。它会裁剪内容，并在外框内保留 4 px 的内边距。
2. **选中胶囊**：表面色、1 px 边框与静止阴影。它位于选中的触发器之后，并在各触发器之间滑动。
3. **触发器**（`TabItem.Header`）：至少 88 px 宽、36 px 高，水平内边距 12 px，圆角 15 px。标签使用小号字号与中等字重，颜色为弱化文字色。悬停时变为主文字色，选中的标签页使用强调色，禁用的标签页降至 50% 不透明度。
4. **间距**：触发器之间为 4 px。
5. **边缘滚动按钮**（`ScrollLeftLabel`、`ScrollRightLabel`）：宽 33 px，仅当列表宽于外框时显示，内含 17 px 的箭头。某一侧有隐藏的标签页时，该侧按钮才可用。
6. **边缘渐隐**：有隐藏标签页的每一侧都有 35 px 的渐隐效果。
7. **面板**：选中标签页的内容，位于列表下方 16 px 处，使用次要文字色、小号字号，行高 19.6 px。
8. **焦点环**：围绕获得焦点的触发器有 2 px 的焦点环；面板获得焦点时，面板周围也有焦点环。

### 由数据驱动的标签页

设置 `ItemsSource`。`ItemTemplate` 渲染触发器，`ContentTemplate` 渲染选中项的面板。通过 `SelectedItem` 或 `SelectionChanged` 读取选择结果。

<Demo name="Overflow" />

```xml
<arc:Tabs ItemsSource="{Binding Folders}"
          SelectedItem="{Binding CurrentFolder}"
          MaxWidth="460"
          HorizontalAlignment="Left">
    <arc:Tabs.ItemTemplate>
        <DataTemplate>
            <TextBlock Text="{Binding Name}" />
        </DataTemplate>
    </arc:Tabs.ItemTemplate>
    <arc:Tabs.ContentTemplate>
        <DataTemplate>
            <TextBlock Text="{Binding Summary}" />
        </DataTemplate>
    </arc:Tabs.ContentTemplate>
</arc:Tabs>
```

不是 `TabItem` 的数据项会直接成为面板内容，并由 `ContentTemplate` 渲染。

### 绑定到视图模型

客户记录在视图模型中保存其各个分区。标签页展示这些分区，选中的分区是一个双向属性，视图模型会为人打开的分区加载数据。使用 CommunityToolkit.Mvvm 时，视图模型是一个 `ObservableObject`，带有 `ObservableCollection<T>` 形式的分区集合，以及用于选择项的 `[ObservableProperty]`。

```csharp
public sealed record CustomerSection(string Title, string Summary);

public partial class CustomerViewModel : ObservableObject
{
    public ObservableCollection<CustomerSection> Sections { get; } = new()
    {
        new("概览", "账户负责人 Maya Chen。商业版方案，2026 年 11 月 30 日续订。"),
        new("订单", "最近 90 天内有 12 笔订单。最近一笔下单于 2026 年 4 月 8 日。"),
        new("发票", "2 张未结发票，其中一张已逾期 6 天。"),
        new("备注", "2026 年 3 月 2 日询问了增加 40 个席位的报价。")
    };

    [ObservableProperty]
    private CustomerSection? _selectedSection;

    partial void OnSelectedSectionChanged(CustomerSection? value)
    {
        // 加载人打开的分区的数据，而不是预先加载每个分区。
    }
}
```

```xml
<arc:Tabs ItemsSource="{Binding Sections}"
          SelectedItem="{Binding SelectedSection, Mode=TwoWay}"
          AutomationProperties.Name="客户分区">
    <arc:Tabs.ItemTemplate>
        <DataTemplate>
            <TextBlock Text="{Binding Title}" />
        </DataTemplate>
    </arc:Tabs.ItemTemplate>
    <arc:Tabs.ContentTemplate>
        <DataTemplate>
            <TextBlock Text="{Binding Summary}" TextWrapping="Wrap" />
        </DataTemplate>
    </arc:Tabs.ContentTemplate>
</arc:Tabs>
```

`SelectedItem` 是应双向绑定的值：它是人选择的分区，而视图模型中的变化（例如从通知中打开“发票”分区）也会选中对应的标签。当视图本身必须对变化作出反应时（例如保存上一分区的滚动位置），请在代码后置中使用 `SelectionChanged`。不要为同一目的绑定 `SelectedIndex`：添加或删除分区时索引会移动，而项本身不变。

### 激活模式

`ActivationMode` 决定方向键的行为。`Automatic`（默认）会选中获得焦点的标签页。`Manual` 只移动焦点；按 Enter 或空格键才会选中标签页。

<Demo name="ManualActivation" />

当面板显示成本低、人正在浏览时使用 `Automatic`：用方向键在标签页之间移动即可预览每个面板。当面板显示成本高、人可能因预览了并非本意打开的面板而丢失工作，或者标签页用于筛选每次选择都会重新加载的列表时，使用 `Manual`。`Manual` 模式还能防止人仍在判断时，方向键就改变了数据。

### 在代码中选择

```csharp
tabs.SelectTab(2);
tabs.SelectionChanged += (_, _) =>
{
    status.Text = $"已选择：{tabs.SelectedItem}";
};
```

`SelectTab` 在标签页存在且已启用时选中该索引处的标签页，否则返回 `false`。`SelectedIndex` 不检查标签页是否启用，因此当目标可能被禁用（例如被权限检查关闭的标签页）时，请使用 `SelectTab`。

### 长标签列表

宽于外框的标签列表会水平滚动。边缘按钮仅在还有内容可滚动时出现，列表会以平滑弹簧显示选中或获得焦点的标签页。点击边缘按钮会使列表移动约可见宽度的四分之三。代码改变选择时，列表会保持选中的标签页可见，因此长列表深处的标签页无需搜索即可找到。

请让标签使用简短的文字，使其在页面支持的最窄宽度下仍能放入列表；其余信息移入面板。十个文件夹的列表配合边缘按钮可以使用；十五个且标签很长的列表更适合改用 `Select` 或侧边栏，因为人每次选择都得滚动。

### 数据结构与性能

只显示选中标签页的内容。选择变化时，面板宿主会替换所显示的内容，并在淡出期间保留离开的面板。应将繁重的加载放在选择之后，例如放在视图模型的选择处理程序中，而不是在页面打开时加载每个分区。请保持标签数量较少：大约五到七个标签页的列表易于扫视，更长的列表需要不同的结构。

### 示例：客户记录标签页

CRM 中的一条客户记录。账户名称及其方案位于标签页上方，因此人在概览、订单、发票与备注之间切换时，上下文不会改变。概览面板是一个两栏的简短事实列表，标签使用弱化颜色，值使用主文字色。标签文字为与其打开的列表相符的名词，第一个标签页被选中，因为它是人打开最频繁的视图。

![客户记录标题，带四个标签页，面板中显示概览信息](images/CustomerTabs.png)

```xml
<StackPanel Width="680" Spacing="16">
    <StackPanel Spacing="4">
        <TextBlock Text="Northwind Trading"
                   FontSize="22"
                   FontWeight="SemiBold"
                   Foreground="{arc:ArcDesignTokenResource Foreground}" />
        <TextBlock Text="Business plan, renews November 30, 2026"
                   FontSize="14"
                   Foreground="{arc:ArcDesignTokenResource TextMuted}" />
    </StackPanel>
    <arc:Tabs AutomationProperties.Name="Customer sections">
        <arc:TabItem Header="Overview">
            <StackPanel Spacing="12">
                <Grid ColumnDefinitions="180,*">
                    <TextBlock Text="Account owner" FontSize="14"
                               Foreground="{arc:ArcDesignTokenResource TextMuted}" />
                    <TextBlock Grid.Column="1" Text="Maya Chen" FontSize="14" />
                </Grid>
                <Grid ColumnDefinitions="180,*">
                    <TextBlock Text="Plan" FontSize="14"
                               Foreground="{arc:ArcDesignTokenResource TextMuted}" />
                    <TextBlock Grid.Column="1" Text="Business, billed annually" FontSize="14" />
                </Grid>
                <Grid ColumnDefinitions="180,*">
                    <TextBlock Text="Renewal date" FontSize="14"
                               Foreground="{arc:ArcDesignTokenResource TextMuted}" />
                    <TextBlock Grid.Column="1" Text="November 30, 2026" FontSize="14" />
                </Grid>
                <Grid ColumnDefinitions="180,*">
                    <TextBlock Text="Open invoices" FontSize="14"
                               Foreground="{arc:ArcDesignTokenResource TextMuted}" />
                    <TextBlock Grid.Column="1" Text="2, one 6 days overdue" FontSize="14" />
                </Grid>
            </StackPanel>
        </arc:TabItem>
        <arc:TabItem Header="Orders">
            <TextBlock Text="12 orders in the last 90 days." FontSize="14" />
        </arc:TabItem>
        <arc:TabItem Header="Invoices">
            <TextBlock Text="inv_2026_0412 and inv_2026_0399 are open." FontSize="14" />
        </arc:TabItem>
        <arc:TabItem Header="Notes">
            <TextBlock Text="Asked for a quote for 40 more seats on March 2, 2026." FontSize="14" />
        </arc:TabItem>
    </arc:Tabs>
</StackPanel>
```

### 文案规范

- **标签文字**：一到两个词，命名它所打开的列表的名词，例如“发票”“备注”。保持各标签的措辞一致，使列表读起来是一组并列的视图。
- **标签中的计数**：只有当计数会改变人接下来的操作时才添加，例如“未结（2）”。从不变化的计数只会给每个标签增加噪音。
- **面板文字**：以状态或数字开头（“2 张未结发票”“暂无备注”），而不是要求人去看标签页。
- **禁用的标签页**：禁用某个标签页时，在附近说明原因，例如在相邻标签页的面板中或页面标题中说明，让人知道视图是缺失还是对其不可用。

| 不要写 | 改为 |
|---|---|
| 标签文字 “Misc” | 备注 |
| 标签文字 “Stuff about the customer” | 账户 |
| 面板文字 “点击上方标签查看更多” | 没有未结发票。 |
| 标签文字 “INVOICES” | 发票 |

### 最佳实践

**推荐**

- 保持标签简短，并让列表在页面支持的最窄宽度内放得下。被滚动隐藏的标签比同时可见的标签更难找到。
- 把共享的上下文（记录名称、状态、主要操作）保留在标签页上方，使人切换视图时它始终可见。
- 对 `SelectedItem` 使用双向绑定，并用 `AutomationProperties.Name` 为列表命名。视图模型保存选择状态，辅助技术通过名称朗读列表。
- 当面板显示成本高时使用 `Manual` 激活模式，使方向键只移动焦点，而不加载每个视图。

**避免**

- 用标签页表示步骤。人会期望标签页可以按任意顺序访问，而必须先完成的步骤需要 `Stepper` 或一系列页面。
- 在标签页内嵌套标签页。两层并列选择会让人分不清自己所在的层级，而且内层列表会在外层列表中滚动。
- 只在一个标签页中放置页面的主要操作。人来到记录的原因通常在每个视图中都可见，而不是只藏在其中一个里。
- 在页面上没有原因地禁用标签页。没有说明的禁用标签页看起来像是坏了。

### 常见问题

**为什么选择标签页时我的页面不会滚动？** 标签列表会在它自己的视口中显示选中的标签页，而不会把整个控件带入视野。位于首屏以下的标签组会停留在人滚动到的位置，因此选择标签时页面不会跳动。

**页面加载时如何打开某个特定标签页？** 在控件加载之前或之后设置 `SelectedIndex` 或 `SelectedItem`。如果该标签页可能被禁用，改为调用 `SelectTab(index)`：对于禁用或不存在的标签页它返回 `false`，因此你可以回退到第一个启用的标签页。

**为什么在 `Manual` 模式下方向键不选择标签页？** 在 `Manual` 模式下，方向键只移动焦点。在获得焦点的标签页上按 Enter 或空格键即可选中它。

**可以为溢出状态设置标签列表的样式吗？** 可以。当列表宽于外框时，`Tabs` 会设置 `:overflow` 伪类；当某一侧有隐藏的标签页时，会设置 `:scrolled-start` 与 `:scrolled-end`。在样式选择器中使用它们，即可在不改变控件模板的情况下修改边缘效果。

## API 参考

<!-- api: Tabs -->

## 无障碍

- 列表具有 Tab 角色，每个 `TabItem` 都公开其选择状态。请用 `AutomationProperties.Name` 为列表命名。
- 字符串形式的标题会作为每个 `TabItem` 的名称；其他类型的标题需要显式命名。
- 只有选中的标签页在 Tab 顺序中。方向键在标签页之间移动，Home 与 End 分别跳到第一个与最后一个。
- Tab 键从列表移动到选中标签页的面板。
- 溢出列表上的滚动按钮由 `ScrollLeftLabel` 与 `ScrollRightLabel` 命名，默认分别为“向左滚动标签页”和“向右滚动标签页”。
- 正在离开的标签页的面板对辅助技术隐藏，并且在淡出期间不能获得焦点。

### 键盘

| 按键 | 操作 |
|---|---|
| `Tab` / `Shift+Tab` | 进入列表中的选中标签页，或最后一个获得过焦点的标签页；从列表移入选中标签页的面板 |
| `Left` / `Right` | 移到上一个或下一个启用的标签页，到达两端时循环；在 `Automatic` 模式下该标签页同时被选中 |
| `Home` / `End` | 移到第一个或最后一个启用的标签页；在 `Automatic` 模式下该标签页同时被选中 |
| `Enter` / `Space` | 选中获得焦点的标签页。在 `Manual` 模式下键盘通过此方式选择；指针按下在两种模式下都会选中 |
| 指针按下 | 左键按下时选中标签页；触摸在松开时选中 |

禁用的标签页会被方向键、Home 与 End 跳过，指针按下也无法选中它。

## 动效

- 选中指示胶囊以 morph 弹簧在各触发器之间滑动。
- 新面板从切换方向滑入 8 px 并淡入。旧面板保持原位，向后移动 6 px 后快速淡出。
- 面板区域以弹簧从旧面板高度过渡到新高度，因此下方的内容随之移动。
- 减弱动效时，指示胶囊立即切换，面板在原位交叉淡化，不做高度动画。关闭动效时一次性完成所有变化。

| 变化 | 完整 | 减弱 | 关闭 |
|---|---|---|---|
| 选择移动 | 胶囊以 morph 弹簧在触发器之间滑动 | 胶囊立即切换 | 立即完成 |
| 新面板出现 | 从切换方向滑入 8 px，并在标准时长内淡入 | 在 120 ms 内原位交叉淡化 | 立即完成 |
| 旧面板离开 | 保持原位，向后移动 6 px，并在 120 ms 内淡出 | 在 100 ms 内淡出 | 立即完成 |
| 面板区域改变高度 | 以弹簧从旧高度过渡到新高度，带动下方内容 | 无高度动画 | 立即完成 |
| 列表滚动到某个标签页 | 以 smooth 弹簧滚动 | 直接跳转 | 直接跳转 |
| 悬停与选中的触发器 | 标签颜色渐变 | 立即完成 | 立即完成 |

## 相关组件

- `SegmentedControl`：用于紧凑地选择一个没有面板的值。
- `Accordion`：用于按顺序阅读的堆叠分区。
- `Pagination`：用于翻阅同一集合的页面。

| 组件 | 适用场景 |
|---|---|
| `SegmentedControl` | 选择的是一个值，例如范围或视图模式，本身没有面板 |
| `Accordion` | 堆叠的分区按顺序阅读，并且可以同时展开多个 |
| `Breadcrumb` | 页面位于层级结构中，人需要沿路径返回上层 |
| `Pagination` | 视图是同一集合的页面，而不是并列视图 |
| `Stepper` | 人按固定顺序完成步骤 |
| `Select` | 视图列表很长，表单中单次选择即可满足需要 |
