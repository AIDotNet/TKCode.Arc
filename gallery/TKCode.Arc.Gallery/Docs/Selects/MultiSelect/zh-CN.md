---
title: 多选
summary: 选择多个值，同时保持字段清晰易读。
source-hash: 612daa4d75a6
---

<Demo name="Chips" />

## 何时使用

`MultiSelect` 让人从一个固定列表中选择多个值，同时字段不会随之变高。它用于表单和筛选栏中，答案是一组值的场景：工单上的标签、项目的负责人、接收告警的渠道。选中的值以标签的形式显示在字段内，人无需打开菜单就能看到已做的选择。

- **问题或工单上的标签**：支持人员在工单编辑器中为一个案例设置“缺陷”“计费”和“优先级”。固定列表让全团队的标签保持一致，标签则能一眼看出整组选择。
- **支持队列或订单列表中的负责人与团队筛选**：经理按两三名客服或地区筛选队列。筛选栏中的标签显示当前生效的筛选条件，`+N` 标签显示其余的数量。
- **告警规则中的通知渠道**：SRE 为一条规则选择“邮件”“Slack”和“PagerDuty”。每个渠道都是固定选项，规则表单按选中的先后顺序保存这些值。
- **部署或报表范围中的环境与地区**：发布负责人为一条流水线选择预发布和生产环境。人无权使用的选项可以禁用，字段则保持一行高度。
- **带搜索的大型固定列表**：销售负责人从一个很长的列表中选择多个城市。`IsSearchable` 会在菜单中加入搜索框，无需另开对话框也能顺畅使用。

## 何时不用

- **切换选项时需要始终看到全部选项**：用 `ChipGroup`。它的标签始终在屏幕上，人可以同时比较选项及其状态。
- **人需要自行输入并创建的值**：用 `TagInput`。`MultiSelect` 只能从你提供的列表中选取。
- **只能选一个值**：列表较短时用 `Select`，人需要搜索的长列表用 `Combobox`。
- **相互独立的是或否设置**：每个设置使用一个 `Checkbox` 或 `Switch`，让每个设置都有自己的标签和说明。
- **命令而非值**：用 `DropdownMenu`。在那里选择一项会执行操作；在这里选择选项则是把值存入表单。

## 用法

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:MultiSelect Label="标签"
                     Description="显示在问题卡片上。"
                     Width="320">
        <arc:SelectItem Content="缺陷" Value="bug" />
        <arc:SelectItem Content="功能" Value="feature" />
        <arc:SelectItem Content="文档" Value="docs" />
        <arc:SelectItem Content="基础设施" Value="infra" IsEnabled="False" />
    </arc:MultiSelect>
</UserControl>
```

`MultiSelect` 派生自 Avalonia 的 `SelectingItemsControl`，因此保留你熟悉的项模型：`ItemsSource`、`SelectedItems`、`Selection`、`SelectionChanged`、键盘焦点和 `IsEnabled`。它的选择模式默认为 `Multiple | Toggle`，因此每次选择会增加或移除一个值。Arc 层在此之上加入标签、说明、标签行、清除按钮和菜单。`SelectItem` 子元素和 `SelectOption` 记录都可以使用；没有 `Value` 的 `SelectItem` 会把 `Content` 作为值。

### 结构

1. **字段标签**（`Label`）：14 px、中等字重，位于触发器上方。它同时是字段的无障碍名称。
2. **触发器**：至少 44 px 高（`ControlSizeMD`）的带边框方框，标签前 12 px，下拉箭头后 11 px，上下各 5 px。它使用控件圆角、表面色和弱化的边框颜色。悬停与展开时只改变边框和背景颜色。
3. **标签行**（显示时替代 `Placeholder`）：标签按选中的先后顺序显示所选项的名称。每个标签最宽 144 px，左右内边距 7 px、上下内边距 3 px，使用 12 px 的超小字号，与前一个标签间距 5 px。标签使用弱化表面色并带有弱化的边框。
4. **溢出标签**：可见标签之后的 `+N` 标签。它的数字来自超出 `MaxVisibleChips` 的选中项，使用次要文字颜色。
5. **占位文字**（`Placeholder`，默认为“选择选项”）：未选中任何值时显示的弱化文字。选中第一个值后立即淡出。
6. **下拉箭头**：16 px 的向下箭头。菜单展开时旋转 180 度。选中值时，它的左外边距从 8 px 增加到 34 px，使旁边的清除按钮不会与箭头重叠。
7. **清除按钮**：22 px 的圆形按钮，内含 14 px 的叉号，距触发器右边缘 36 px。当至少选中一个值且字段启用时显示，名称为“清除选择”。
8. **说明**（`Description`）：触发器下方 12 px 的弱化辅助文字。它同时是字段的无障碍帮助文本。
9. **菜单**：带 1 px 边框、面板圆角和 5 px 内边距的抬起表面。它在触发器下方 8 px 处展开，宽度与触发器相同。每个选项行高 38 px，圆角为 21 px，已选中的选项显示勾选标记。禁用的选项以 42% 的不透明度绘制。

默认情况下，菜单没有高度限制。当长列表会超出屏幕时，`MaxDropDownHeight` 可以限制其高度（参见数据结构一节）。

### 标签与溢出

选中的值以标签的形式显示在字段内。`MaxVisibleChips` 设置在其余值折叠为 `+N` 标签之前显示多少个标签，默认为 2。窄的筛选栏可设为 1，只显示数量时设为 0。当标签行空间不足时，可见标签会随剩余宽度缩小，但 `+N` 标签保持其尺寸，因此数量不会消失。

<Demo name="OneVisibleChip" />

```xml
<arc:MultiSelect Label="负责人"
                 MaxVisibleChips="1"
                 Width="280"
                 ItemsSource="{Binding Owners}" />
```

应根据字段的宽度而不是列表的长度来选择数量。每个标签需要的空间取决于其文字，最宽 144 px。对于较长的标签，例如“基础设施评审”，使用一个可见标签，让字段保持在一行，并由菜单显示完整的选择。

### 绑定的选项与值

将 `ItemsSource` 设为 `SelectOption` 记录。`SelectedValues` 按选中的先后顺序列出值，它是只读的，因此请在事件处理程序或代码中读取它。

```csharp
var labels = new MultiSelect
{
    Label = "标签",
    Width = 320,
    ItemsSource = new[]
    {
        new SelectOption("bug", "缺陷"),
        new SelectOption("feature", "功能"),
        new SelectOption("docs", "文档"),
        new SelectOption("infra", "基础设施", isDisabled: true),
    }
};
labels.SelectionChanged += (_, _) => Console.WriteLine(string.Join(", ", labels.SelectedValues));
```

`SelectedValues` 是应当存储的列表。标签与 `SelectedValues` 都按选中的顺序排列，而不是选项的顺序：如果人先选中“文档”再选中“缺陷”，标签和保存的列表都会以“文档”开头。当保存的顺序很重要时，请在代码中对值排序。

### 绑定到视图模型

在 MVVM 应用中，视图模型拥有选项和选中项，字段负责显示它们。将 `ItemsSource` 绑定到选项，将 `SelectedItems` 绑定到视图模型中的 `ObservableCollection`。`MultiSelect` 派生自 Avalonia 的 `SelectingItemsControl`，它会让绑定的 `SelectedItems` 列表与选择保持双向同步：每次选中或移除标签都会在集合中增加或移除对应选项，而在代码中向集合增加或移除选项会更新标签。

```csharp
public partial class AlertRuleViewModel : ObservableObject
{
    private readonly IAlertRuleService _rules;

    public AlertRuleViewModel(IAlertRuleService rules) => _rules = rules;

    public ObservableCollection<SelectOption> Channels { get; } = new()
    {
        new SelectOption("email", "邮件"),
        new SelectOption("slack", "Slack"),
        new SelectOption("pagerduty", "PagerDuty"),
        new SelectOption("sms", "短信", isDisabled: true),
    };

    public ObservableCollection<SelectOption> SelectedChannels { get; } = new();

    [RelayCommand]
    private async Task SaveAsync()
    {
        var ids = SelectedChannels.Select(c => (string)c.Value!).ToArray();
        await _rules.SaveAsync("checkout-latency", ids);
    }
}
```

```xml
<StackPanel Width="320" Spacing="16">
    <arc:MultiSelect Label="通知"
                     ItemsSource="{Binding Channels}"
                     SelectedItems="{Binding SelectedChannels}"
                     Placeholder="选择渠道"
                     Description="接收第一条告警的渠道。" />
    <arc:Button Content="保存规则" Command="{Binding SaveCommand}" IsDefault="True" />
</StackPanel>
```

- `SelectedItems` 使用默认的单向绑定：控件会把选中项填入你提供的集合，因此视图模型在表单的整个生命周期内只保留一个集合。替换集合对象会把选择重置为其内容。
- 集合按 Avalonia 报告的选中顺序排列；`SelectedValues` 给出人选中的先后顺序。当顺序很重要时，请存储 `SelectedValues`。
- `IsOpen` 是双向的，因此绑定可以打开和关闭菜单，菜单也会把其状态写回。`SelectedValues` 是只读的：只能读取，绝不能赋值。
- 每次选中和移除都会引发 `SelectionChanged`，包括键盘选中；请用它处理实时预览等副作用，而不是用来复制选择。
- 编译绑定需要在视图上设置 `x:DataType`（为简洁起见，这里的示例省略了它）。

### 可搜索的菜单

`IsSearchable` 会在菜单顶部加入搜索框。输入会筛选选项；按 `Down` 高亮第一个匹配项，按 `Enter` 选中它。`SearchPlaceholder` 设置搜索框中的提示，`EmptyText` 设置没有匹配项时显示的文字。

<Demo name="Searchable" />

```xml
<arc:MultiSelect Label="城市"
                 Placeholder="选择城市"
                 SearchPlaceholder="输入城市名称"
                 IsSearchable="True"
                 Width="320"
                 ItemsSource="{Binding Cities}" />
```

默认的匹配方式是对每个选项的名称、其 `Meta` 文字和 `Keywords` 进行不区分大小写的文本搜索。为人们会搜索的同义词添加 `Keywords`：名称为“基础设施”且关键字为 "devops" 的选项，在有人输入 "devops" 时也会出现。当匹配必须遵循你自己的规则时，例如按客户代码的前缀匹配，请设置 `FilterPredicate`。该谓词接收选项和去除首尾空白后的搜索文字。

搜索是 Arc 之外的附加功能，Arc 的菜单本身没有搜索框，因此只在列表长到滚动比输入更慢时才使用它。打开菜单时焦点会移入搜索框；关闭菜单时，如果焦点原本在搜索框中，则会返回触发器。

### 在代码中选择

`Selection` 是选择模型。`SelectAll()` 和 `UnselectAll()` 只供代码使用，因为 Arc 没有全选控件。

```csharp
labels.Selection.Select(0);
labels.Selection.Select(1);
labels.UnselectAll();
```

使用 `Selection.Select` 和 `Selection.Deselect` 加上项的索引，每次改变一个选中项。`SelectAll()` 会选中每个已启用的选项，因此会跳过禁用的选项；`UnselectAll()` 会清空选择和标签。

### 占位文字与禁用状态

未选中任何值时显示 `Placeholder`。禁用的字段会忽略输入，如果菜单原本是展开的则会将其关闭，隐藏清除按钮，并以 50% 的不透明度和“不允许”光标淡化显示。

<Demo name="Disabled" />

```xml
<arc:MultiSelect Label="评审人" Placeholder="请选择选项" IsEnabled="False" />
```

只有在值无法更改时才禁用字段，并在附近说明原因，例如在说明中写明：“发布获批后锁定。”被禁用的字段仍会显示其标签，因此人可以读取当前的选择。

### 数据结构与性能

选项可以是 `SelectItem` 子元素或 `SelectOption` 记录，控件会自行绘制每个可见的选项（`SelectItem` 只是数据载体，而不是容器），因此菜单不会为每个项创建一个控件。让项模型保持精简：一个值、一个名称以及所需的标志（`IsDisabled`、`Keywords`）。把头像或账号详情等较重的数据放在视图模型中，并在保存时查询。

- 菜单默认没有高度限制，由屏幕边界决定其范围。对于超过十几个选项的列表，请设置 `MaxDropDownHeight`（例如 320），使菜单可以滚动并保持在屏幕内。
- 保持名称简短。超过标签宽度（144 px）的名称会以省略号截断，因此完整文字只在菜单中可见。
- 菜单展开时列表发生变化，菜单会立即重新筛选并保留已选中的值。加载新列表时请替换整个集合，而不是逐项编辑。

### 示例：告警规则表单

一个可观测性控制台中的告警规则表单。标签是简短的名词，渠道是固定列表，辅助文字说明选择的作用（“接收第一条告警的渠道”）。“通知”字段有一个标签，因此人能看到已选的值以及旁边的清除按钮。“环境”字段为空并显示占位文字，因此很清楚仍需要做出选择。每个选中渠道的标签显示其名称，下拉箭头告诉人该字段可以展开。

![带有一个渠道标签的告警规则表单，以及一个为空的环境字段](images/AlertRuleForm.png)

```xml
<StackPanel Width="560" Spacing="20">
    <arc:Input Label="Rule name"
               Text="Checkout latency above 800 ms"
               Description="Shown in the incident title." />
    <arc:MultiSelect Label="Notify"
                     Description="Channels that receive the first alert."
                     Placeholder="Pick channels"
                     SelectedIndex="0">
        <arc:SelectItem Content="Email" Value="email" />
        <arc:SelectItem Content="Slack" Value="slack" />
        <arc:SelectItem Content="PagerDuty" Value="pagerduty" />
        <arc:SelectItem Content="SMS" Value="sms" IsEnabled="False" />
    </arc:MultiSelect>
    <arc:MultiSelect Label="Environments"
                     Description="Environments the rule watches."
                     Placeholder="Pick environments">
        <arc:SelectItem Content="Production" Value="prod" />
        <arc:SelectItem Content="Staging" Value="staging" />
        <arc:SelectItem Content="Sandbox" Value="sandbox" />
    </arc:MultiSelect>
    <StackPanel Orientation="Horizontal" Spacing="8" HorizontalAlignment="Right">
        <arc:Button Content="Cancel" Variant="Secondary" />
        <arc:Button Content="Save rule" />
    </StackPanel>
</StackPanel>
```

示例中使用 `SelectedIndex` 来选中渠道，因为 XAML 无法在不借助名称的情况下列出多个选中项。在应用中，请像视图模型示例那样，把已保存的选择加载到 `SelectedItems`。

### 文案规范

- **标签**：句子式大小写的简短复数名词：“标签”“负责人”“渠道”。单值字段使用单数名词。
- **占位文字**：空字段的操作说明，使用动词短语：“选择渠道”“请选择选项”。选中第一个值后它就会消失，因此不要在其中放入标签所要承载的信息。
- **选项名称**：产品中人们已经在用的名称，句子式大小写，不加计数或标点。写“计费”，而不是“计费（3）”。计数属于 `+N` 标签。
- **说明**：一句话说明选择的作用或出现的位置：“显示在问题卡片上。”
- **搜索占位文字**：一个要搜索的对象，而不是一条命令：写“输入城市名称”，而不是“在此搜索城市”。

| 不要写 | 改为 |
|---|---|
| 选择选项…… | 选择渠道 |
| 标签（可多选） | 标签 |
| 已选：缺陷、功能、文档，+1 项 | `+N` 标签已经显示了其余的数量 |
| 搜索 | 输入城市名称 |
| 点击此处添加标签 | 选择标签 |

### 最佳实践

**推荐**

- 为每个字段提供可见的标签，因为仅靠标签本身无法说明这些值的含义。
- 存储值（`SelectedValues`）并显示名称，这样重命名选项不会改变已保存的数据。
- 根据字段宽度设置 `MaxVisibleChips`，让标签保持在一行内。
- 提交时校验必填的选择，并指向该字段，因为字段本身没有错误行。

**避免**

- 用 `MultiSelect` 处理人需要输入的值。自由文字应放在 `TagInput` 中，那里每一项都会被创建和检查。
- 在两个字段中显示同一组选择。人在一处设置了某项，就会期望它出现在另一处。
- 通过禁用选项来隐藏它们。被禁用的选项仍会列出，这样人才知道它的存在以及被锁定的原因。
- 无理由地在菜单展开时更改选项列表。列表一变化菜单就会重新筛选，因此人在选择时看到的会是另一组内容。
- 依赖选择顺序来表达含义。标签和 `SelectedValues` 按选中的顺序排列，而这一顺序因人而异；当顺序很重要时，请对值排序。

### 常见问题

**为什么 `SelectedValues` 与我的列表顺序不一致？** 控件在值被选中时就记录下来，并为标签和 `SelectedValues` 保留这一顺序。当顺序必须与列表一致时，请在代码中对值排序。

**如何预先选中值，之后又如何清除？** 将 `SelectedItems` 绑定到一个选项对象列表，即可设置初始选中项。之后要在代码中更改选择，请使用 `Selection.Select` 和 `Selection.Deselect` 加上项的索引，或用 `UnselectAll()` 清空。

**为什么没有清除按钮？** 它只在至少选中一个值且字段启用时显示。禁用的字段会隐藏它，空字段则没有可清除的内容。

**按 Enter 会提交表单或触发默认按钮吗？** 不会。在触发器上，Enter 会展开菜单，因此它永远不会到达窗口的默认按钮。在已展开的菜单中，Enter 会切换高亮选项的选中状态，并保持菜单展开。提交表单请使用按钮。

**人能用键盘或鼠标选择全部选项吗？** Arc 没有全选控件。`SelectAll()` 可在代码中使用，并且只会选中已启用的选项。

## API 参考

<!-- api: MultiSelect -->

<!-- api: SelectOption -->

## 无障碍

- `Label` 为字段命名。该名称同时列出已选中的标签，因此屏幕阅读器会读出当前的值。
- 字段会播报它打开的是一个多选列表。菜单展开时会公开展开状态。
- 标签对辅助技术隐藏，改由语音摘要替代。清除按钮的名称为“清除选择”。
- 焦点环绘制在触发器上而不是菜单上，因此菜单展开时焦点仍停留在字段上。
- 每次高亮移动和每次切换都会在实时区域中播报，包括选项在列表中的位置以及是否已选中。
- `Description` 是字段的帮助文字。禁用的字段会被读作已禁用，且无法获得焦点以进行输入。

### 键盘

以下按键在触发器获得焦点时有效。带有说明的按键在搜索框获得焦点时同样有效。

| 按键 | 操作 |
|---|---|
| `Enter` 或 `Space` | 菜单关闭时将其打开 |
| `Enter`（菜单展开，选项已高亮） | 切换高亮选项的选中状态，并保持菜单展开 |
| `Enter`（菜单展开，没有高亮选项） | 关闭菜单 |
| `Space`（菜单展开） | 关闭菜单；要切换选项请使用 `Enter` |
| `Down` / `Up` | 菜单关闭时先将其打开，然后移动高亮。两端循环，并跳过禁用的选项 |
| `Escape` | 关闭菜单，不改变已选中的项 |
| `Tab`（搜索框已打开） | 关闭菜单并把焦点移到下一个控件 |
| 字母与数字（搜索框已打开） | 筛选选项；`Space` 会在搜索文字中输入空格 |

点击选项会切换其选中状态并保持菜单展开，因此人可以一次就选中多个值。点击菜单之外的区域会关闭菜单；点击清除按钮会移除全部选中项、关闭菜单，并把焦点返回触发器。

## 动效

- 标签位置会以平滑的弹簧展开到新宽度，每个标签从模糊中逐渐放大出现。
- `+N` 数字会随数量变化向上或向下滚动。
- 每个勾选标记在菜单中自行绘制出来，菜单则从上方落入。
- 减弱动效时，尺寸与缩放变化会改为短促的交叉淡化。关闭动效时变化直接切换。
- 选中第一个值时，清除按钮会以灵敏的弹簧弹出；按下时会压缩到 96%。

| 变化 | 完整 | 减弱 | 关闭 |
|---|---|---|---|
| 选中一个值 | 标签位展开，标签从模糊中放大出现 | 短促的交叉淡化 | 立即完成 |
| 移除一个值 | 标签缩小消失，其位置收起 | 短促的交叉淡化 | 立即完成 |
| `+N` 数量变化 | 数字向上或向下滚动 | 短促的交叉淡化 | 立即完成 |
| 菜单展开或收起 | 从上方 6 px 处带轻微缩放落入 | 短促的交叉淡化 | 立即完成 |
| 选项的勾选变化 | 勾选标记绘入或绘出 | 短促的交叉淡化 | 立即完成 |
| 悬停、按下、展开 | 边框和背景颜色交叉淡化 | 立即完成 | 立即完成 |

## 相关组件

- `ChipGroup` 一次显示所有选项；`MultiSelect` 把列表收在菜单中。
- `TagInput` 用于人创建的值；`MultiSelect` 用于来自固定列表的值。
- `Select` 用于从短列表中选择一个值。
- `Combobox` 用于从人需要搜索的长列表中选择一个值。

| 组件 | 适用场景 |
|---|---|
| `ChipGroup` | 所有选项都应保持可见，并且只切换少数几个维度 |
| `TagInput` | 人创建自由值，例如标签或收件人 |
| `Select` | 表单中从短列表选择一个值 |
| `Combobox` | 从人需要搜索的长列表中选择一个值 |
| `Checkbox` / `Switch` | 每个设置都是相互独立的是或否 |
| `DropdownMenu` | 菜单用于执行操作，而不是存储值 |
