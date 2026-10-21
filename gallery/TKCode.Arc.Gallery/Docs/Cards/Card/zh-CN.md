---
title: 卡片
summary: 承载一组相关内容与操作的封闭式容器。
source-hash: 01e3fd1fef02
---

<Demo name="QuickLook" />

## 何时使用

`Card` 是可浏览集合的磁贴：项目、房源、帖子或工作区的网格，每一项都有一张视觉图、一个标题、一位所有者，以及一个会随时间变化的状态。它把这些部分和属于该项的少量操作组合进一个封闭的表面中，因此网格读起来是一组等大的单元。卡片还可以打开快速预览：卡片会扩展为同一项目的更大视图，而无需离开页面。

- **管理后台中的项目与工作区网格**：每个项目一张卡片，带封面图、所有者头像，以及“2 小时前更新”之类的状态。底部放一个操作，例如“打开项目”。
- **店铺与内部商品目录中的列表磁贴**：商品照片、名称、一行简短文案和末尾的按钮。媒体是每个磁贴的视觉锚点。
- **CRM 或案件管理网格中的记录快速预览**：客户或工单的卡片打开后显示其未决问题与里程碑。网格保持原位，关闭视图后用户会回到同一张磁贴。
- **账单或部署仪表板中磁贴上的实时状态**：任务运行时，卡片的 `Status` 随之变化（“正在同步 40 张发票中的 12 张”，然后“刚刚同步”）。新词会依次升起，并以礼貌的方式朗读。
- **开发者门户或编辑工具中的帖子与发布说明信息流**：每篇帖子一张卡片，带署名行（`Meta`）和底部操作，无需完整表格的分量。

## 何时不用

- **带有上下文说明的单个数字**，例如营收、转化率或未关闭的事件：用 `MetricCard`，它包含数值、变化与迷你趋势图。
- **在原处展开的内容**，无需对话框，例如从标题下方展开的设置分组：用 `ExpandableCard`。
- **没有可展开卡片的模态内容**，例如从工具栏按钮打开的确认或表单：用 `Dialog`。
- **逐个处理项目**，即人们逐张接受或拒绝卡片：用 `CardStack`，它为一叠卡片处理拖动、键盘与撤销。
- **需要按列比较的大量记录**，例如带金额、日期与状态的发票：用 `Table`。卡片会隐藏那些使各行可比较的列。
- **网格或列表中还没有任何内容**：在网格区域显示 `EmptyState`。空卡片不是一条消息。

## 用法

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:Card Title="港口改版"
              Description="全新的预订流程与房间页面。"
              Width="280" />
</UserControl>
```

`Card` 派生自 Avalonia 的 `ContentControl`。`Title` 与 `Description` 提供标题和说明行，`Content` 接收其他任何内容，放置在说明与底部之间。每个可选部分都是有类型的属性，因此带头像、状态和操作的卡片无需额外布局：卡片会排列底部、隐藏为空的部分，并在整个网格中保持相同的间距。

### 结构

1. **媒体**（`Media`）：顶部的图片或视觉元素，最小高度 112 px。属性为空时隐藏。
2. **标题**（`Title`）：18 px、中等字重、1.3 行高，为三级标题。设置 `Details` 后，标题变为打开快速预览的按钮。
3. **说明**（`Description`）：14 px 正文，位于标题下方 8 px。行长上限约为 34 个数字宽度，因此较长的文案会换行为更多行，而不会撑宽卡片。
4. **内容**（`Content`）：说明与底部之间的任意元素，例如标签列表或进度条。`Content` 为空时隐藏。
5. **底部**：位于内容下方 20 px。当 `Avatar`、`Meta`、`Status` 或 `Action` 有值时出现。
6. **头像**（`Avatar`）：前端 32 px 宽的插槽，用于放置所有者的 `Avatar` 控件，与署名行和操作之间各有 12 px 间距。
7. **署名行**（`Meta` 与 `Status`）：`Meta` 是所有者的一行 12 px 文字。`Status` 位于其下方，单行显示，超出边缘时裁切并以省略号结尾。
8. **操作**（`Action`）：末尾的控件，通常是小按钮。它保留自己的焦点和点击目标，不会打开快速预览。
9. **表面**：1 px 表面色边框、26 px 圆角与 20 px 内容内边距。静止时没有阴影，悬停时出现。

### 组成部分与组合

Arc 的卡片没有 `Variant` 属性。它的变体是可选部分的组合，按项目逐个选择。

| 组合 | 需要设置的部分 | 适用于 |
|---|---|---|
| 纯文本 | `Title`、`Description` | 参考型磁贴、链接列表、没有视觉元素的摘要 |
| 媒体与署名 | `Media`、`Avatar`、`Meta` | 每一项都有图片和所有者的网格 |
| 实时 | 在上述基础上加 `Status` | 页面打开期间状态会变化的项目 |
| 可交互 | 在上述基础上加 `Details` | 点开后可查看更多内容的项目，例如记录或项目简报 |

仅有标题和说明的纯文本卡片在 240 px 宽度下高度不到 80 px，因此纯文本卡片网格保持紧凑。除非高度一致，否则不要在同一行混用纯文本卡片和媒体卡片：网格单元会拉伸卡片，简短文案下方的空白看起来像布局错误。

### 媒体、所有者与底部

网格中每张卡片的媒体高度应相同，这样整行的标题才能对齐。`Avatar` 插槽中放 `Small` 尺寸的头像，它按 32 px 的插槽设计。`Meta` 是所有者的姓名，使用一行文字。操作是一个控件。如果卡片需要第二个操作，请把它放在 `Content` 或 `Details` 中，使底部保持一个末尾的点击目标。

```xml
<arc:Card Title="港口改版"
          Description="全新的预订流程与房间页面。"
          Meta="Maya Chen"
          Status="2 小时前更新"
          Width="280">
    <arc:Card.Media>
        <Border Height="132" Background="#5B8DEF" />
    </arc:Card.Media>
    <arc:Card.Avatar>
        <arc:Avatar DisplayName="Maya Chen" Size="Small" />
    </arc:Card.Avatar>
    <arc:Card.Action>
        <arc:Button Content="分享" Variant="Ghost" Size="Small" />
    </arc:Card.Action>
</arc:Card>
```

### 状态

`Status` 是一行简短文字，例如“2 小时前更新”。文字变化时，旧行向上移出，新词依次升起出现，间隔约 40 ms。该行使用缓存文本绘制，不会为每个词创建控件，因此经常更新的卡片依然轻量。

<Demo name="StatusUpdates" />

```xml
<arc:Card Title="每周摘要"
          Description="每周五汇总各团队的重点内容。"
          Status="{Binding LastUpdate}"
          Width="300" />
```

状态应写成带时间的事实：“正在同步 40 张发票中的 12 张”“已发布到 4 个团队”“2 小时前更新”。只有状态真正改变时才修改字符串；按计时器重复设置同样的文字会重放动画，却没有传达新信息。把 `Status` 设为 `null` 或空字符串会隐藏该行。只有当已有状态被替换时才播放滚动动画，因此第一个值会立即出现。

### 快速预览

设置 `Details` 后，标题变为按钮，整张卡片也成为触发器：在卡片上按下、且按下位置不在嵌套控件上时，会打开快速预览。表面、媒体、标题、说明、署名行与操作会飞入同一页面中更大的视图，之后详细内容随之升起。

<Demo name="Controlled" />

```xml
<arc:Card Title="季度计划"
          Description="未来三个月的目标、负责人与日期。"
          IsDetailsOpen="{Binding IsPlanOpen, Mode=TwoWay}"
          Width="280">
    <arc:Card.Details>
        <TextBlock Text="由代码打开：IsDetailsOpen 为双向绑定。" TextWrapping="Wrap" />
    </arc:Card.Details>
</arc:Card>
```

`Details` 保存更大视图的内容，可以是任意控件。快速预览最宽 480 px，窗口在其四周保留 16 px 的边距。`Details` 只放人们判断是否打开该记录所需的内容：摘要、下一步以及一两个链接。完整的编辑表单应放在 `Dialog` 中或页面上。

快速预览会重复卡片的文案和底部（其标题为 22 px，媒体位于顶部，并带有浮动的关闭按钮），因此同样的文字不必写两遍。`Details` 中只放额外的内容。

使用 `OpenDetails()` 与 `CloseDetails()` 从代码中打开和关闭快速预览，使用 `DetailsOpenChanged` 获知变化的原因。`IsDetailsOpen` 为双向绑定，因此绑定可以打开或关闭它，事件则报告每一次变化。

```csharp
card.DetailsOpenChanged += (_, e) => log.Text = e.IsOpen ? "打开" : $"已关闭（{e.Reason}）";
card.OpenDetails();
card.CloseDetails();
```

原因为 `Programmatic` 表示由代码发起的变化，`CloseButton` 表示关闭按钮，`Escape` 表示 Escape 键，`PressOutside` 表示在快速预览后方的遮罩上按下。

### 无快速预览

不设置 `Details` 时，标题只是普通标题，卡片仅在悬停时抬起。标题不获取焦点，在卡片上按下也没有反应，因此唯一的停留点是卡片内的控件。此形式适用于仅展示信息或带底部操作的卡片。

<Demo name="WithoutQuickLook" />

### 绑定到视图模型

项目网格为每个项目绑定一张卡片。视图模型拥有每个项目的状态，卡片负责显示。使用 CommunityToolkit.Mvvm 时，`ObservableObject` 持有属性，`ObservableCollection<T>` 为网格提供数据。

```csharp
public partial class ProjectCardViewModel : ObservableObject
{
    public ProjectCardViewModel(string title, string owner)
    {
        _title = title;
        _owner = owner;
    }

    [ObservableProperty]
    private string _title;

    [ObservableProperty]
    private string _owner;

    [ObservableProperty]
    private string? _status;

    [ObservableProperty]
    private bool _isBriefOpen;

    [ObservableProperty]
    private string? _brief;

    [RelayCommand]
    private async Task ShareAsync()
    {
        await _sharing.ShareAsync(Title);
    }
}

public partial class ProjectsViewModel : ObservableObject
{
    public ObservableCollection<ProjectCardViewModel> Projects { get; } = new();
}
```

```xml
<ItemsControl ItemsSource="{Binding Projects}">
    <ItemsControl.ItemsPanel>
        <ItemsPanelTemplate>
            <WrapPanel ItemSpacing="16" LineSpacing="16" />
        </ItemsPanelTemplate>
    </ItemsControl.ItemsPanel>
    <ItemsControl.ItemTemplate>
        <DataTemplate>
            <arc:Card Title="{Binding Title}"
                      Description="{Binding Brief}"
                      Meta="{Binding Owner}"
                      Status="{Binding Status}"
                      IsDetailsOpen="{Binding IsBriefOpen, Mode=TwoWay}"
                      Width="280"
                      VerticalAlignment="Top">
                <arc:Card.Action>
                    <arc:Button Content="分享" Variant="Ghost" Size="Small"
                                Command="{Binding ShareCommand}" />
                </arc:Card.Action>
                <arc:Card.Details>
                    <TextBlock Text="{Binding Brief}" TextWrapping="Wrap" />
                </arc:Card.Details>
            </arc:Card>
        </DataTemplate>
    </ItemsControl.ItemTemplate>
</ItemsControl>
```

- `Title`、`Description`、`Meta` 与 `Status` 为单向：由视图模型拥有，卡片不会写回。
- `IsDetailsOpen` 为双向：按下或关闭按钮会把 `False` 写回，代码也可以把它设为 `True` 以打开快速预览。只有当视图模型必须知道用户已关闭视图时，才使用 `Mode=TwoWay` 绑定。
- 需要原因的副作用（例如按 `Escape` 后保存草稿）使用 `DetailsOpenChanged`。状态本身使用绑定。
- `Action` 是普通按钮，拥有自己的 `Command`。按下它不会打开快速预览。
- 编译绑定需要在模板与视图上设置 `x:DataType`。为简洁起见，示例在视图上省略了它。

### 数据结构与性能

快速预览在第一次打开时才创建，因此没有人打开的卡片不会为 `Details` 额外付出开销。让 `Details` 的内容保持轻量：它在第一次打开时才构建。对于长集合，请把卡片放在支持虚拟化的面板中，使屏幕外的卡片不会被创建。

在卡片或其网格列上设置宽度。卡片会填满分配给它的单元格（最小宽度为 0），因此固定宽度 260 到 300 px 可形成常规网格，`UniformGrid` 在窗口缩放时保持列等宽。

### 焦点与关闭

设置了 `Details` 的卡片只是一个 Tab 停留点：标题按钮。在其上按 Enter 或 Space 会打开快速预览，焦点移到其中的关闭按钮。快速预览打开时，Tab 与 Shift+Tab 只在其内部循环，因此键盘无法到达后面的页面。Escape、在遮罩上按下以及关闭按钮都会关闭它，关闭后焦点回到标题。关闭按钮默认名为“Close quick look”；当文案需要说明关闭的是什么时，请设置 `CloseButtonName`，例如“关闭项目简报”。

按下 `Action` 按钮会执行其命令，不会打开快速预览。如果卡片在快速预览打开时离开窗口，快速预览会被移除，`IsDetailsOpen` 变为 `False`。

### 示例：工作区中的项目网格

工作区首页上的项目列表。每张卡片显示封面、所有者和最近更新时间，底部放一个“Open”操作。标题为名词，说明为一句话，状态是带时间的事实。两张卡片宽度相同且顶部对齐，使这一行读起来像一组。

![工作区网格中的两张项目卡片，其中一张带有实时状态行](images/ProjectGrid.png)

```xml
<StackPanel Width="640" Orientation="Horizontal" Spacing="20">
    <arc:Card Title="Harbour redesign"
              Description="New booking flow and room pages for the 2026 release."
              Meta="Maya Chen"
              Status="Updated 2 hours ago"
              Width="300"
              VerticalAlignment="Top">
        <arc:Card.Media>
            <Border Height="132">
                <Border.Background>
                    <LinearGradientBrush StartPoint="0%,0%" EndPoint="100%,100%">
                        <GradientStop Color="#5B8DEF" Offset="0" />
                        <GradientStop Color="#9CC2FF" Offset="1" />
                    </LinearGradientBrush>
                </Border.Background>
            </Border>
        </arc:Card.Media>
        <arc:Card.Avatar>
            <arc:Avatar DisplayName="Maya Chen" Size="Small" />
        </arc:Card.Avatar>
        <arc:Card.Action>
            <arc:Button Content="Open" Variant="Secondary" Size="Small" />
        </arc:Card.Action>
    </arc:Card>
    <arc:Card Title="Atlas API"
              Description="Endpoint inventory and examples for the public docs."
              Meta="Jonas Berg"
              Status="Syncing 12 of 40 pages"
              Width="300"
              VerticalAlignment="Top">
        <arc:Card.Media>
            <Border Height="132">
                <Border.Background>
                    <LinearGradientBrush StartPoint="0%,0%" EndPoint="100%,100%">
                        <GradientStop Color="#2FA88B" Offset="0" />
                        <GradientStop Color="#8EE0C6" Offset="1" />
                    </LinearGradientBrush>
                </Border.Background>
            </Border>
        </arc:Card.Media>
        <arc:Card.Avatar>
            <arc:Avatar DisplayName="Jonas Berg" Size="Small" />
        </arc:Card.Avatar>
        <arc:Card.Action>
            <arc:Button Content="Open" Variant="Secondary" Size="Small" />
        </arc:Card.Action>
    </arc:Card>
</StackPanel>
```

### 文案规范

- **标题**：句子式大小写的简短名词短语，点明项目是什么：“港口改版”“第三季度发票”。标题是卡片的无障碍名称，因此脱离上下文也必须说得通。
- **说明**：一句话，说明该项目是什么或包含什么。它大约在 34 个数字宽度处换行，因此最多保持两行。不要重复标题。
- **状态**：带时间的事实，句子式大小写：“2 小时前更新”“正在同步 40 张发票中的 12 张”。计数用阿拉伯数字，避免“最近”这类模糊词。
- **操作**：动词，卡片本身没有说明对象时再加上对象：“打开”“分享报告”。

| 不要写 | 改为 |
|---|---|
| 点击此处查看更多 | 打开项目 |
| 很多事情正在发生 | 正在同步 40 张发票中的 12 张 |
| 最近更新 | 2 小时前更新 |
| 这张卡片显示的是一个项目 | 港口改版 |

### 最佳实践

**推荐**

- 只有当项目的内容多于卡片所能显示的内容时，才设置 `Details`。重复页面内容的快速预览毫无价值。
- 网格中的每张卡片保持相同的宽度和媒体高度，使标题与底部对齐。
- 对页面打开期间会变化的状态使用 `Status`，并在变化之间保持措辞稳定。
- 当快速预览包含草稿或需要做出决定时，设置 `CloseButtonName`，使关闭控件说明其结果。

**避免**

- 在 `Details` 中放长表单。快速预览是有边界的视图，供阅读和一两个操作使用。
- 底部操作超过一个。底部只有一个末尾插槽，第二个操作会与打开卡片的按下操作争抢。
- 按计时器把 `Status` 重新设为相同的文字。每次变化都会重放滚动动画，却没有新信息，只会分散注意力。
- 依赖悬停抬起来表明卡片可点击。触摸和触控笔从不会让卡片抬起，因此标题与 `Details` 的提示必须独立传达含义。

### 常见问题

**为什么按下卡片不会打开快速预览？** 只有设置了 `Details` 的卡片才会在按下时打开，而且只有在按下位置不是嵌套控件时才会。先检查 `Details` 是否有值，再检查按下的位置是否不在 `Action` 上。

**如何从工具栏按钮打开快速预览？** 把 `IsDetailsOpen` 绑定到视图模型的属性，或在代码中调用 `OpenDetails()`。`CloseDetails()` 会以原因 `Programmatic` 关闭它。

**为什么卡片不在 Tab 顺序中？** 卡片本身不可获得焦点。设置了 `Details` 时，标题按钮是 Tab 停留点；没有设置时，标题只是标题，只有底部操作会获得焦点。

**第一个状态会有动画吗？** 不会。滚动动画只在已有状态被新文字替换时播放。设置第一个值或清空状态都会立即改变该行。

**为什么触摸时卡片不会抬起？** 抬起效果只响应鼠标指针。触摸与触控笔的布局和按下行为相同，只是没有悬停状态。

## API 参考

<!-- api: Card -->

## 无障碍

- 卡片是一个由 `Title` 命名的分组。没有 `Details` 时，标题为三级标题。
- 设置 `Details` 后标题变为按钮；按 Enter 或空格键会打开快速预览。
- 快速预览是以其标题命名的对话框，说明作为其帮助文本。其关闭按钮由 `CloseButtonName` 命名，默认为“Close quick look”。
- `Status` 的变化通过礼貌的实时区域朗读。
- 嵌套操作保留各自的焦点与名称。仅含图标的操作请使用 `AutomationProperties.Name` 设置名称；`Media` 中的图片请设置替代文字，或将其标记为装饰性。
- 悬停抬起与媒体缩放仅为视觉效果；去掉它们，卡片的内容和操作不变。

### 键盘

| 按键 | 操作 |
|---|---|
| `Tab` / `Shift+Tab` | 在标题（需设置 `Details`）、内容中的控件和底部操作之间移动 |
| 标题上的 `Enter` 或 `Space` | 打开快速预览，焦点移入其中 |
| 快速预览打开时的 `Tab` / `Shift+Tab` | 只在其中的控件之间移动；焦点留在快速预览内 |
| `Escape` | 关闭快速预览，并将焦点返回标题 |
| 关闭按钮上的 `Enter` 或 `Space` | 以原因 `CloseButton` 关闭快速预览 |

没有 `Details` 的卡片自身不接收焦点；只有其中的嵌套控件是停留点。

## 动效

- 悬停时卡片抬起 2px，边框与阴影加深。媒体缓慢放大至 1.04（仅限鼠标）。
- 设置 `Details` 后，表面、媒体、标题与署名行通过同一弹簧飞入快速预览，同时页面在模糊中变暗。关闭时它们飞回卡片。
- 新的 `Status` 会把旧行推走，同时新词依次升起。
- 减弱动效时取消抬起、缩放与变形，快速预览只淡入淡出。关闭动效时变化立即生效。

| 变化 | 完整 | 减弱 | 关闭 |
|---|---|---|---|
| 卡片悬停 | 抬起 2px，边框与阴影加深 | 边框与阴影立即变化，不抬起 | 立即完成 |
| 悬停时的媒体 | 960 ms 内缩放至 1.04，480 ms 内回落 | 无缩放 | 立即完成 |
| 快速预览打开或关闭 | 表面、媒体与标题以弹簧飞入（打开 0.3 s，关闭 0.26 s） | 淡入淡出 | 立即完成 |
| 状态变化 | 旧行推走，新词依次升起 | 立即替换 | 立即完成 |
| 快速预览后方的遮罩 | 在页面上淡入淡出 | 淡入淡出 | 立即完成 |

## 相关组件

- `MetricCard`：单个数字及其上下文与变化。
- `ExpandableCard`：原地展开、无需对话框的内容。
- `Dialog`：没有卡片可展开的模态内容。
- `CardStack`：逐张处理一叠卡片。
- `EmptyState`：尚无卡片的网格或列表。

| 组件 | 适用场景 |
|---|---|
| `MetricCard` | 内容是一个数字，并附带其变化与趋势 |
| `ExpandableCard` | 内容在页面内原地展开，无需对话框 |
| `Dialog` | 视图是模态的，且没有可以放大的磁贴 |
| `CardStack` | 人们需要逐张做决定，并使用拖动、键盘与撤销 |
| `Table` | 项目是需要可比较列的行 |
| `EmptyState` | 网格或列表还没有任何项目 |
