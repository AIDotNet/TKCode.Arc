---
title: 面包屑
summary: 显示页面在层级结构中的位置。
source-hash: b4c233743e1b
---

<Demo name="Default" />

## 何时使用

`Breadcrumb` 显示从某个区域的根部一直到用户当前页面的路径，例如工作区、设置和账单。它位于页面标题上方，回答“我现在在哪里？”，并提供一键返回上层的入口。在桌面应用中，它也可以显示客户端路径，例如文件浏览器的文件夹路径；此时选择某个面包屑会改变视图，而不是地址。

- **管理后台中的账单板块**：路径 Acme Analytics、账单、发票、inv_2026_0412 告诉管理员他们位于哪个工作区和哪个板块，每个父级面包屑都能返回上方的列表。
- **桌面应用中的文件浏览器或素材库**：每个文件夹一个面包屑（主目录、项目、Q3 报告）。选择某个面包屑会移除其后的面包屑，文件夹列表随之更新为较短的路径。
- **很深的设置树**：组织、安全、单点登录。用户常常从搜索结果或链接进入设置页，因此父级面包屑会指明返回的路径。
- **开发者工具与事件记录**：平台、事件、INC-2026-114 显示拥有该记录的团队和产品，当同一个 ID 出现在多处时，这一点尤为重要。
- **窄窗口上的长层级结构**：使用 `MaxVisibleItems` 时，根部和最后几级保持可见，中间的层级折叠为一个溢出按钮，因此很深的路径也能放进侧边栏宽度的列中。

## 何时不用

- **浏览带可展开节点的整棵树**：用 `TreeView`，它会显示每一层的同级项，并保持分支展开。
- **在同一对象的同级视图之间切换**，例如概览、活动和设置：用 `Tabs`。面包屑是用户会返回的位置，而不是用来切换的面板。
- **在同一集合的多个页之间翻页**：用 `Pagination`，它知道总页数和当前页码。
- **用户按固定顺序完成的步骤**，例如购物车、配送、付款和审核：用 `Stepper`，它显示进度和每一步的状态。
- **从列表中选择一个值**，例如表单中的文件夹或项目：用 `Select` 或 `Combobox`。面包屑不保存表单提交的值。

## 用法

每个面包屑都是一个 `BreadcrumbItem`。最后一个面包屑是当前页面：它是纯文本，从不是链接。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:Breadcrumb>
        <arc:BreadcrumbItem Content="工作区" />
        <arc:BreadcrumbItem Content="设置" />
        <arc:BreadcrumbItem Content="账单" />
    </arc:Breadcrumb>
</UserControl>
```

`Breadcrumb` 派生自 Avalonia 的 `ItemsControl`，因此 `Items`、`ItemsSource` 和 `ItemTemplate` 的行为与任何列表一致。每个项都包装在 `BreadcrumbItem`（一个 `ContentControl`）中，因此其标签就是 `Content`。面板把面包屑排成一行；当路径宽于可用空间时，它会换到新的一行。面包屑本身从不获得焦点；只有可以被选择的面包屑才会获得焦点。

### 结构

1. **地标**（`Breadcrumb`、`Label`）：由 `Label` 命名的导航地标，默认为“面包屑”。它的各个面包屑组成一个列表，每一项都会播报其在路径中的位置。
2. **分隔符**：一个 16 px 的尖括号形箭头，使用强边框色，以 1.75 的描边绘制。它位于除第一个之外的每个面包屑前面，因此根部没有箭头。箭头与其标签之间有 6 px 的间距。
3. **标签**（`BreadcrumbItem.Content`）：小字号，使用弱化文字颜色，水平内边距 2 px、垂直内边距 4 px，位于圆角为 7 px 的外框内。相邻面包屑之间相距 6 px。
4. **悬停下划线**：可交互面包屑标签下方的 1 px 线条。悬停时以 45% 的不透明度出现。
5. **当前面包屑**（`IsCurrent`）：最后一个面包屑。它使用强调色的强色和中等字重，且不可交互。每个面包屑都会按中等字重为其标签预留宽度，因此成为当前面包屑时，前面的面包屑不会移动。
6. **溢出按钮**：当 `MaxVisibleItems` 折叠路径时，第一个被隐藏的位置变为省略号按钮，其名称由 `OverflowLabel` 指定。
7. **焦点环**：2 px 的焦点色环，位于标签外框外侧 4 px 处。仅在键盘聚焦时出现。
8. **换行**：路径换行时，行与行之间相距 2 px。

### 通过点击导航

当前页面之前的每个面包屑都可以通过指针、`Enter` 或 `Space` 选择。处理 `ItemClick` 即可响应所选的面包屑。`Index` 是它在路径中的位置，0 为根部；`Item` 是数据项（或内联编写的 `BreadcrumbItem`）。

```csharp
breadcrumb.ItemClick += (_, e) =>
{
    status.Text = $"导航到 {((BreadcrumbItem)e.Item!).Content}（索引 {e.Index}）";
};
```

`ItemClick` 事件先执行。接着运行该面包屑的 `Command`（传入其 `CommandParameter`），然后打开其 `NavigateUri`。应用内导航请处理该事件，因为它能在同一处获得索引和项；只有当面包屑自身负责该操作时，才使用另外两种方式。

### 绑定路径

将 `ItemsSource` 设置为路径的集合。使用 `ObservableCollection<T>`，使面包屑随路径的增减而变化。

<Demo name="ClientSidePath" />

打开一个文件夹会通过 `path.Add(name)` 添加一个面包屑。选择某个面包屑会移除其后的面包屑，被移除的面包屑会以动画方式退出。添加到末尾的面包屑从左侧滑入，保留原位的面包屑则滑动让出空间。在相同索引处保留相同标签的面包屑不会再次播放其进入动画。

### 命令与导航 URI

带有 `Command` 的面包屑会在 `CanExecute` 返回 true 时以 `CommandParameter` 运行它，并呈现为按钮。带有 `NavigateUri` 的面包屑呈现为链接，其地址通过顶层的 `Launcher` 打开，并交由平台处理。每个可以被选择的面包屑都会被播报为按钮，带有 `NavigateUri` 的面包屑则播报为链接。对于桌面应用自身的视图，优先使用 `ItemClick` 或命令，使视图在窗口内变化，而不是在浏览器中打开。

```xml
<arc:BreadcrumbItem Content="账单"
                    Command="{Binding OpenBillingCommand}"
                    CommandParameter="{Binding BillingId}" />
```

设置了 `IsClickable="False"` 的面包屑即使不是当前页面，也只是纯文本。用于仅作标签的段落，例如没有自己页面的年份分组。

### 标签

`Label` 为辅助技术命名导航地标。当页面上不止一个导航地标时设置它，使屏幕阅读器用户能够区分两者。默认值为“面包屑”。

```xml
<arc:Breadcrumb Label="文件夹路径" />
```

### 折叠路径

`MaxVisibleItems` 把很长路径的中间部分折叠进溢出菜单。与 Arc 一样，默认关闭，即路径永不折叠。当路径长于 `MaxVisibleItems` 时，根部保持可见，第一个被隐藏的位置变为省略号按钮，最后 `MaxVisibleItems − 1` 个面包屑保持可见。按钮会打开一个菜单，按路径顺序列出被隐藏的面包屑。选择某个条目会引发 `ItemClick`，参数为该条目在路径中的索引。

<Demo name="CollapsedPath" />

菜单是位于按钮下方的轻量关闭型弹出层。它可以通过代码用 `OpenOverflow()` 和 `CloseOverflow()` 打开和关闭，`IsOverflowOpen` 表示其是否正在显示。菜单用 `Up`、`Down`、`Home` 和 `End` 移动焦点，按 `Escape` 关闭并将焦点返回按钮，选择之后也会关闭。

### 数据结构与性能

面包屑描述的是一条路径，因此只保留真正重要的层级。每个项都是一个 `BreadcrumbItem` 容器，面板会测量并排列所有容器；控件不会虚拟化其项。设计意图是只放几个层级的路径。对于很深的层级结构，请缩短标签并使用 `MaxVisibleItems`，而不是构建一条窗口必须换行的长路径。

标签应当是文本。内容为控件的面包屑会保留其位置，而无需为中等字重预留宽度；溢出菜单显示的是控件的自动化名称，而不是控件本身。

### 绑定到视图模型

文件浏览器把路径保存在视图模型中，并将面包屑绑定到它。使用 CommunityToolkit.Mvvm 时，路径是视图模型修改的 `ObservableCollection<string>`，面包屑只负责显示它。

```csharp
public partial class FolderBrowserViewModel : ObservableObject
{
    public ObservableCollection<string> Path { get; } = new() { "主目录", "项目", "Acme Analytics" };

    public void OpenFolder(string name)
    {
        Path.Add(name);
        // 在此加载新路径下的列表。
    }

    public void NavigateTo(int index)
    {
        while (Path.Count > index + 1)
        {
            Path.RemoveAt(Path.Count - 1);
        }
    }

    [RelayCommand]
    private void GoUp()
    {
        if (Path.Count > 1)
        {
            Path.RemoveAt(Path.Count - 1);
        }
    }
}
```

```xml
<StackPanel Spacing="12">
    <arc:Breadcrumb ItemsSource="{Binding Path}" Label="文件夹路径" />
    <arc:Button Content="上一级" Variant="Ghost" Command="{Binding GoUpCommand}" />
</StackPanel>
```

```csharp
breadcrumb.ItemClick += (_, e) => viewModel.NavigateTo(e.Index);
```

面包屑从不写入集合。选择某个面包屑只是一个请求，因此由视图模型决定是否进行导航、使用 `NavigateTo` 截断路径，并加载新的列表。`Path` 集合是唯一的数据来源；不要再保存一份当前文件夹名称的副本。

### 示例：账单板块页头

管理后台中某张发票的页头。路径说明发票位于何处，标题给出它的名称，两个按钮对它执行操作。当前面包屑是发票 ID，它是文本而非链接，因此用户能看到位置，而不会出现一个指回同一页面的链接。标签“发票位置”把这个地标与侧边栏中的导航区分开。

![带发票面包屑、标题和两个操作的账单页面页头](images/InvoiceHeader.png)

```xml
<StackPanel Width="640" Spacing="12">
    <arc:Breadcrumb Label="Invoice location">
        <arc:BreadcrumbItem Content="Acme Analytics" />
        <arc:BreadcrumbItem Content="Billing" />
        <arc:BreadcrumbItem Content="Invoices" />
        <arc:BreadcrumbItem Content="inv_2026_0412" />
    </arc:Breadcrumb>
    <TextBlock Text="Invoice inv_2026_0412"
               FontSize="24"
               FontWeight="SemiBold"
               Foreground="{arc:ArcDesignTokenResource Foreground}" />
    <TextBlock Text="Northwind Trading, issued April 3, 2026. Paid on April 9, 2026."
               FontSize="14"
               TextWrapping="Wrap"
               Foreground="{arc:ArcDesignTokenResource TextMuted}" />
    <StackPanel Orientation="Horizontal" Spacing="8">
        <arc:Button Content="Download PDF" Variant="Secondary" />
        <arc:Button Content="Send receipt" />
    </StackPanel>
</StackPanel>
```

### 文案规范

- **面包屑标签**：用户所熟悉的位置名称；专有名称用标题式大写（“Acme Analytics”），通用板块用句首大写（“Billing”）。每个标签控制在一两个词以内，使路径保持在一行。
- **当前面包屑**：页面的名称，而不是一句话。它会重复标题，因此两者保持一致。
- **地标标签**：一个简短的名词短语，说明路径，例如“发票位置”或“文件夹路径”。在已翻译的界面中避免使用“面包屑”，直接说明内容。
- **溢出标签**：说明按钮会显示什么（“显示隐藏的路径”），而不是它由什么构成。

| 不要写 | 改为 |
|---|---|
| 地标标签 "Breadcrumb" | 发票位置 |
| 当前面包屑做成指向同一页面的链接 | 当前页面名称，不加链接 |
| 溢出标签 "More" | 显示隐藏的路径 |
| 面包屑 "前往账单概览页面" | 账单 |
| 面包屑 "当前页面：发票" | 发票 |

### 最佳实践

**推荐**

- 把面包屑放在页面标题上方，并让标题与当前面包屑使用相同的名称，使路径与标题保持一致。
- 当页面有多个导航地标时，为每个面包屑设置 `Label`，使辅助技术用户能够区分它们。
- 应用内导航使用 `ItemClick`，由视图模型决定导航方式，面包屑本身只是一个简单的请求。
- 对于可能超出一列宽度的路径，设置 `MaxVisibleItems`，并在页面所支持的最窄宽度下测试路径。

**避免**

- 用面包屑表示同级视图或步骤。用户期望面包屑指向层级的上方；而同级视图或步骤会把他们带向旁边或前方，使路径令人困惑。
- 把当前页面做成链接。最后一个面包屑是用户所在的页面；指向它的链接会重新加载同一视图，并让当前状态更难辨认。
- 在代码后置中用字符串构建路径，再复制到视图中。应将路径绑定到同一个集合，使面包屑与内容同步变化。
- 在面包屑中放入控件或长句子。面包屑不会换行显示自己的标签，因此一句长话会让该面包屑远宽于其他，并把路径挤到第二行。

### 常见问题

**为什么当前面包屑对点击没有反应？** 最后一个面包屑是当前页面，因此它不可交互，没有悬停下划线，也不在 Tab 顺序中。其 `IsCurrent` 值跟随位置：路径中最后的那个面包屑即为当前。

**如何让中间的面包屑不成为链接？** 在该 `BreadcrumbItem` 上设置 `IsClickable="False"`。它仍保留在路径中，标签也仍然可见，但它是纯文本，无法被选择。

**如何知道选中了哪个面包屑，怎样获得数据项？** 在 `ItemClick` 处理程序中读取 `e.Index` 和 `e.Item`。对于 `ItemsSource` 路径，`Item` 是数据对象；对于内联的面包屑，它是 `BreadcrumbItem`。

**路径变化时溢出菜单会关闭吗？** 会。更改项或 `MaxVisibleItems` 会关闭菜单，因此在菜单打开时到达的路径更新不会在屏幕上留下过期的列表。

**路径可以显示多行吗？** 可以。当路径宽于可用空间时，面包屑会换到新的一行，行与行之间相距 2 px。路径可能变长时，优先使用 `MaxVisibleItems`，因为换行的路径更难快速浏览。

## API 参考

<!-- api: Breadcrumb -->

## 无障碍

- 根部是一个由 `Label` 命名的导航地标，默认为“面包屑”。其面包屑组成一个列表，每一项都会播报其位置和路径的大小，例如“第 2 项，共 4 项”。
- 当前面包屑会被播报为当前页面。它是文本，而不是按钮或链接，且不能获得焦点。
- 带有 `NavigateUri` 的面包屑会被播报为链接；其他可以被选择的面包屑会被播报为按钮。
- 尖括号形分隔符对辅助技术隐藏。
- 溢出按钮由 `OverflowLabel` 命名，默认为“显示隐藏的路径”。被隐藏的面包屑会以各自的名称出现在其菜单中。

### 键盘

| 按键 | 操作 |
|---|---|
| `Tab` / `Shift+Tab` | 在可选择的面包屑与溢出按钮之间移动；当前面包屑会被跳过 |
| `Left` / `Right` | 将焦点移到上一个或下一个可选择的面包屑 |
| `Enter` / `Space` | 选择获得焦点的面包屑，这会引发 `ItemClick`，并运行其命令或打开其地址 |
| `Down` / `Up` | 在溢出菜单中移到下一个或上一个条目，到达两端时循环 |
| `Home` / `End` | 在溢出菜单中移到第一个或最后一个条目 |
| 溢出菜单中的 `Enter` / `Space` | 选择该条目；菜单关闭，焦点返回按钮 |
| `Escape` | 在溢出菜单中关闭菜单，并将焦点返回溢出按钮 |

在面包屑上按下并松开指针，若松开发生在该面包屑内，则选择它。

## 动效

- 新的面包屑从左侧滑入并逐渐清晰，同时其同级项以平滑弹簧让出空间。
- 被移除的面包屑向左移动 4px 并快速淡出。其余面包屑让出空间。
- 路径未变化时，其布局不播放动画。
- 减弱动效和关闭动效会立即应用新增和移除的面包屑。滑入与退出仅在完整动效下运行。

| 变化 | 完整 | 减弱 | 关闭 |
|---|---|---|---|
| 在末尾添加面包屑 | 从左侧滑入 8 px，并在标准时长内逐渐清晰 | 立即显示 | 立即显示 |
| 移除面包屑 | 向左移动 4 px，在 120 ms 内淡出并模糊消失 | 立即移除 | 立即移除 |
| 变化之后的面包屑 | 以平滑弹簧滑至新位置 | 跳至新位置 | 跳至新位置 |
| 悬停、焦点、当前 | 标签颜色渐变 | 立即完成 | 立即完成 |

## 相关组件

- `Tabs`：同一对象的同级视图。
- `TreeView`：浏览整个层级结构。
- `Pagination`：翻阅结果的各页。

| 组件 | 适用场景 |
|---|---|
| `Tabs` | 视图是同一对象的同级，且每个视图都有自己的面板 |
| `TreeView` | 用户通过展开和折叠节点来浏览层级结构 |
| `Pagination` | 结果是同一集合，并被分成多页 |
| `Stepper` | 用户按固定顺序完成步骤 |
| `PageHeader`（块） | 页面需要在一个标准页头中包含标题、面包屑和操作 |
| `Select` / `Combobox` | 用户在表单中从列表中选择一个值 |
