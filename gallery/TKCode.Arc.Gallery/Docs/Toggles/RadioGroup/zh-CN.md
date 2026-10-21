---
title: 单选组
summary: 从可见的一组选项中选择一项。
source-hash: 440e811ad630
---

<Demo name="Plans" />

## 何时使用

单选组以所有选项同时可见的方式，展示一小组互斥的选项。它适用于表单和设置页面，人需要在提交前比较各个选择，并且每个选择都需要描述才能理解。该组是一个带标签的控件：标题为它命名，各行承载选项，所选的值是一个表单可以读取或保存的数值。

- **账单或升级页面中的套餐选择**：工作区所有者在爱好版、专业版和团队版之间选择。每一行都附有该套餐包含内容的描述，而这正是决策所依据的信息。
- **结账时的配送或履约选项**：标准配送、加急或自提，每项都以配送时段作为描述。所选选项会随订单一起提交。
- **分析控制台中的导出格式或报表范围**：CSV、Parquet 或 JSON，或“本月”与“年初至今”。每一行都说明文件包含什么，让人不必猜测。
- **邀请表单中的访问或角色选择**：查看者、编辑者或管理员，每个角色附有其权限的描述。对于使用者自己的方案不包含的角色，使用 `IsDisabled`。
- **开发者工具中的环境或地区选择**：为表单一次性选择部署目标，每个选项都必须保持可见，便于审阅。

## 何时不用

- **在屏幕上切换内容的简短行内视图**，例如图表上的日、周和月：用 `SegmentedControl`，它看起来像一排标签，并立即切换视图。
- **很长的选项列表**，例如国家、货币或数十名成员的团队：用 `Select`，它在需要之前不会占据版面。
- **一次需要选择多个选项**，例如选择多个通知渠道或产品领域：对每个选项使用 `Checkbox`，因为单选组只允许选择一项。
- **选项需要在卡片上展示价格、元信息或图标**：用 `RadioCards`，它把每个选项排成可选择的卡片。
- **单个开关式设置**，例如“通过邮件接收发票”：用 `Switch`，它立即生效，不需要分组。

## 用法

`Header` 是该组的标签。选项即控件的 `Items`；每个 `RadioOption` 接受一个值、一个标签以及可选的描述。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:RadioGroup Header="套餐" Width="420" SelectedValue="pro">
        <arc:RadioOption Value="hobby" Label="爱好版" Description="适合个人项目和尝试。" />
        <arc:RadioOption Value="pro" Label="专业版" Description="适合每周都要交付的专业人士。" />
        <arc:RadioOption Value="team" Label="团队版" Description="为不断壮大的团队提供共享工作区、角色与审计日志。" />
    </arc:RadioGroup>
</UserControl>
```

`RadioGroup` 派生自 Avalonia 的 `HeaderedSelectingItemsControl`。因此选择、`SelectedIndex`、`SelectedItem`、`SelectionChanged`、标题和 `HeaderTemplate` 与任何 Avalonia 选择控件中的行为相同，而该组在此之上加入 Arc 的行设计、单一移动的高亮、键盘模型和自动化名称。该组为每个选项创建一行，你无需自己添加行。

### 结构

1. **标题**（`Header`）：该组的图例，使用中等字重，位于第一行之前，下方留 8 px。字符串标题同时为辅助技术提供该组的名称。`HeaderTemplate` 可以用更丰富的内容替换文字。
2. **行**：选项，以 8 px 的间距纵向排列。每行是一个 1 px 边框的方框，具有控件圆角，内边距在四周均为 12 px。该行会拉伸到该组的宽度。
3. **圆环**：一个 17 px 的圆形，带 1 px 边框，颜色为较强的边框色。被选中的圆环以强调色（`ControlOn`）填充。
4. **圆点**：圆环内 7 px 的圆点，颜色为字形颜色（`ControlGlyph`）。它只出现在被选中的行上。当指针在未选中的行上按下时，一个 22% 不透明度的淡色圆点会预览该选择。
5. **标签**（`RadioOption.Label`）：中等字重，使用小号字体。它位于圆环右侧 12 px 处；当 `Label` 为空时，回退为该选项的 `Value`。未选中的标签使用次要文字颜色；被选中的标签使用前景色。
6. **描述**（`RadioOption.Description`）：标签下方的额外一行，使用超小号字体和弱化的文字颜色，位于标签下方 2 px。在窄宽度下会换行；描述为空时会收起。
7. **高亮**：覆盖被选中行的一个轮廓，带 1 px 的强调色描边，并以同一颜色 5% 的填充。它是整个组的单一元素，因此在行之间移动，而不是在每一行上分别出现。
8. **焦点环**：绘制在获得焦点的行外侧 2 px 处的 2 px 环。仅在键盘焦点时出现。

行高随内容变化：只有标签时行较紧凑，添加描述后，每个文字宽度会增加一行换行的文字。被禁用的行和被禁用的组会淡化到 50% 不透明度。

### 绑定选项

将 `ItemsSource` 绑定到选项列表，将 `SelectedValue` 绑定到所选的值。两者都是双向的。

```xml
<arc:RadioGroup Header="套餐"
                ItemsSource="{Binding Plans}"
                SelectedValue="{Binding PlanId}" />
```

```csharp
var plans = new RadioGroup
{
    Header = "套餐",
    ItemsSource = new[]
    {
        new RadioOption("hobby", "爱好版", "适合个人项目和尝试。"),
        new RadioOption("pro", "专业版", "适合每周都要交付的专业人士。")
    },
    SelectedValue = "pro"
};
plans.SelectionChanged += (_, _) => status.Text = $"已选择：{plans.SelectedValue}";
```

`SelectedValue` 保存的是所选选项的 `Value`，而不是选项对象。当你需要整个 `RadioOption`（例如在保存后读取它的描述）时，使用 `SelectedItem`。`SelectedValue` 会在选项到达时应用，因此可以在代码或 XAML 中先于 `ItemsSource` 设置。

### 绑定到视图模型

使用 CommunityToolkit.Mvvm 时，视图模型负责所选的套餐和选项列表，该组只负责显示它们。`SelectedValue` 和 `SelectedItem` 都是双向的，因此指针或键盘做出的更改会立即到达视图模型。

```csharp
public interface IBillingService
{
    Task ChangePlanAsync(string planId);
}

public partial class UpgradeViewModel : ObservableObject
{
    private readonly IBillingService _billing;

    public UpgradeViewModel(IBillingService billing)
    {
        _billing = billing;
        Plans.Add(new RadioOption("hobby", "爱好版", "适合个人项目和尝试。"));
        Plans.Add(new RadioOption("pro", "专业版", "适合每周都要交付的专业人士。"));
        Plans.Add(new RadioOption("team", "团队版", "为不断壮大的团队提供共享工作区、角色与审计日志。"));
        SelectedPlanId = "pro";
    }

    public ObservableCollection<RadioOption> Plans { get; } = new();

    [ObservableProperty]
    private string? _selectedPlanId;

    [RelayCommand(CanExecute = nameof(CanSave))]
    private async Task SaveAsync()
    {
        await _billing.ChangePlanAsync(SelectedPlanId!);
    }

    private bool CanSave() => !string.IsNullOrEmpty(SelectedPlanId);

    partial void OnSelectedPlanIdChanged(string? value) => SaveCommand.NotifyCanExecuteChanged();
}
```

```xml
<StackPanel Width="420" Spacing="16">
    <arc:RadioGroup Header="套餐"
                    ItemsSource="{Binding Plans}"
                    SelectedValue="{Binding SelectedPlanId}" />
    <arc:Button Content="更改套餐" Command="{Binding SaveCommand}" />
</StackPanel>
```

- `SelectedValue` 是用于绑定已保存选择的属性。只有当视图需要选项对象时，才同时绑定 `SelectedItem`。
- `ItemsSource` 接受 `ObservableCollection<RadioOption>`，因此添加或删除选项都会更新该组。
- 对于不属于数据的副作用（例如预览标签），在代码中使用 `SelectionChanged`。保存操作应放在命令中，这样它只在人按下按钮时运行一次，而不是每次按方向键都运行。
- 在 XAML 中用普通字符串设置 `Header`。字符串标题同时也是该组的无障碍名称。

### 禁用选项

`IsDisabled` 让选项保持可见但不可用。方向键会跳过被禁用的选项，指针、空格键和 Enter 都无法选中该行。

<Demo name="InlineOptions" />

当人需要知道某个选项存在以及它为何不可用时，应显示该禁用的选项，例如账号尚未达到的套餐。请在选项的描述中说明原因，例如“在团队版套餐中可用”。如果某个选项与此人始终无关，则应将其移除。

### 选择事件

当选择发生变化时（无论通过指针还是键盘），会引发 `SelectionChanged`。`SelectedItem` 返回 `RadioOption` 本身。

```csharp
group.SelectionChanged += (_, _) =>
{
    if (group.SelectedItem is RadioOption option)
    {
        Console.WriteLine(option.Value);
    }
};
```

### 选项数量与布局

默认的项面板是普通的纵向 `StackPanel`，因此在显示该组时会创建每一个选项。对于两到六个带描述的选项，这是合适的权衡。对于较长的列表，请使用 `Select`，它按需打开列表，而不会把每一行都放在页面上。对于水平排列的简短视图，请使用 `SegmentedControl`。

为该组设置一个宽度，使最长的描述可以换行为两到三行。过窄的组会把每段描述换成很多短行，选项也就更难比较。

### 示例：在工作区升级中选择套餐

来自 SaaS 管理控制台的工作区所有者账单页面。标题是一句说明选择后果的话（“更改将在下一张发票时生效”）。三个套餐各自带有一个区分它们的关键事实，因此人仅凭该组就能做出决定。被选中的行是团队版，主操作按钮说明它要做什么。

![账单页面，套餐组中已选中团队版，并带有保存操作](images/UpgradePlan.png)

```xml
<StackPanel Width="620" Spacing="20">
    <StackPanel Spacing="6">
        <TextBlock Text="Billing plan"
                   FontSize="18"
                   FontWeight="SemiBold"
                   Foreground="{arc:ArcDesignTokenResource Foreground}" />
        <TextBlock Text="Acme Analytics renews on 1 December 2026. Changes take effect at the next invoice."
                   FontSize="13"
                   TextWrapping="Wrap"
                   Foreground="{arc:ArcDesignTokenResource TextMuted}" />
    </StackPanel>
    <arc:RadioGroup Header="Plan" SelectedValue="team">
        <arc:RadioOption Value="hobby"
                         Label="Hobby"
                         Description="One workspace, up to 3 seats, 30 days of history." />
        <arc:RadioOption Value="pro"
                         Label="Pro"
                         Description="Unlimited seats, 1 year of history and scheduled exports." />
        <arc:RadioOption Value="team"
                         Label="Team"
                         Description="Roles, audit logs and single sign-on for up to 500 seats." />
        <arc:RadioOption Value="enterprise"
                         Label="Enterprise"
                         Description="Contact sales. Available on annual contracts."
                         IsDisabled="True" />
    </arc:RadioGroup>
    <StackPanel Orientation="Horizontal" Spacing="8" HorizontalAlignment="Right">
        <arc:Button Content="Cancel" Variant="Secondary" />
        <arc:Button Content="Save plan" />
    </StackPanel>
</StackPanel>
```

### 文案规范

- **标题**：一个简短的名词短语，命名这个选择，例如“套餐”“配送方式”“导出格式”。不要写成问句（“选哪个套餐？”）；选项本身就是答案。
- **选项标签**：一到两个词，以人自己的理解来命名该选项，例如“团队版”“标准配送”。使用句首大写，并保持标签的结构一致，让每一行要么都是套餐，要么都是配送方式。
- **描述**：一句话，写出把该选项与其他选项区分开的事实：席位、历史记录、配送时段、包含的内容。数字用阿拉伯数字书写，并让各描述的长度大致相同。
- **禁用原因**：在描述中写明条件，例如“在团队版套餐中可用”。

| 不要写 | 改为 |
|---|---|
| 您想选择哪个套餐？ | 套餐 |
| 超值推荐！！！ | 无限席位与 1 年历史记录。 |
| 选项 2 | 专业版 |
| 请联系我们的销售团队以了解企业版的价格信息 | 请联系销售。仅限年度合同。 |

### 最佳实践

**推荐**

- 把人最可能选择的选项放在最前面，或放在账号当前已选中的选项上，让决策从当前状态开始。
- 当仅靠标签无法说明差异时，为每个选项提供描述。没有描述的行读起来更快，因此只在描述承载信息时才添加。
- 设置与已保存状态相符的 `SelectedValue`。空选择不显示标记，在一个期望得到答案的表单上，这看起来像一个未回答的问题。
- 对存在但无法应用的选项使用 `IsDisabled`，并在其描述中说明原因。

**避免**

- 对“是或否”的选择使用单选组。`Switch` 或 `Checkbox` 能说明人正在设置什么，而两行单选会重复询问同一件事。
- 一个组中超过六个选项。页面会为每个选项增加一行，选择就变成扫读而不是比较；此时请改用 `Select`。
- 在人阅读时每次刷新都替换选项。只在数据变化时更新集合，这样该组就不会在指针下方重绘。
- 只依赖颜色来显示选中状态。填充的圆环和圆点是被选中行的第二重提示，因此选择不依赖于分辨两种颜色。

### 常见问题

**该组默认会选中任何东西吗？** 不会。在设置值之前，`SelectedIndex` 为 -1，任何行都不显示标记。请将 `SelectedValue` 设置为你想要的默认值。

**我可以在选项到达之前设置 `SelectedValue` 吗？** 可以。该值会在选项到达时应用，因此异步加载选项的组会保留最先设置的那个值。

**为什么该组不为自己命名？** 字符串 `Header` 会为该组命名。非字符串的标题（例如带有标题和提示的面板）则不会，此时请在该组上设置 `AutomationProperties.Name`。

**如何在别处显示所选选项的描述？** 在 `SelectionChanged` 中把 `SelectedItem` 读取为 `RadioOption`，并在文本块中显示它的 `Description`。该组自己的行仍会保留各自的描述。

**行可以接受任意对象吗？** 每一行都使用 `RadioOption`。行从该类型读取 `Label`、`Description` 和 `IsDisabled`，因此其他对象不会为行提供文字。请在视图模型中把你自己的模型映射为 `RadioOption`。

## API 参考

<!-- api: RadioGroup -->
<!-- api: RadioOption -->

## 无障碍

- 当 `Header` 为字符串时，它为该组命名。每个选项都是单选按钮，其描述即为帮助文字。
- 该组只占一个 Tab 停靠点。Tab 从选中的选项进入该组，下一次 Tab 离开。键盘焦点永远不会停在未选中的选项上。
- 方向键将选择移动到下一个或上一个选项，并循环切换。禁用的选项会被跳过。
- Home 和 End 选中第一个和最后一个选项。空格和 Enter 选中获得焦点的选项。
- 高亮和圆点对辅助技术隐藏。每个选项都会报告它是否被选中。
- 键盘焦点会在该行周围绘制 2px 的环。指针焦点不会绘制。
- 颜色并非唯一的提示：被选中的行带有填充的圆环、圆点和前景色标签。

### 键盘

| 按键 | 操作 |
|---|---|
| `Tab` | 在选中的选项处进入该组；下一次 `Tab` 离开该组 |
| `Down` / `Right` | 选中并聚焦下一个选项，从最后一个循环回到第一个 |
| `Up` / `Left` | 选中并聚焦上一个选项，从第一个循环回到最后一个 |
| `Home` / `End` | 选中并聚焦第一个或最后一个选项 |
| `Space` / `Enter` | 选中获得焦点的选项 |

方向键在两个方向上都会跳过禁用的选项。

## 动效

- 一个高亮块以形变弹簧滑动到被选中的行。新的圆点以弹簧弹入，旧的圆点同时缩小消失，因此一次变化读起来就像一次物理移动。
- 按下圆环时它会轻微缩小。按下未选中的行会预览其圆点。
- 该组尺寸变化时，高亮块会立即移动到新的行。
- 减弱动效时，高亮块和圆点立即就位，并取消按下时的缩放。关闭动效时效果相同。

| 变化 | 完整 | 减弱 | 关闭 |
|---|---|---|---|
| 选择移动 | 高亮以形变弹簧滑动；新圆点从 40% 弹入并淡入 | 高亮与圆点立即到位 | 立即完成 |
| 旧圆点离开 | 立即缩小并淡出 | 立即完成 | 立即完成 |
| 圆环按下 | 按住时缩放到 88%；松开后弹回 | 无缩放 | 无缩放 |
| 按下未选中的行 | 预览圆点以 22% 不透明度显示 | 预览圆点显示 | 预览圆点显示 |
| 行与标签颜色 | 背景与圆环颜色交叉淡化；标签颜色交叉淡化 | 立即完成 | 立即完成 |
| 组尺寸变化 | 高亮立即移到新的行 | 立即完成 | 立即完成 |

## 相关组件

- `RadioCards`：选项需要在卡片上展示价格、元信息或图标时使用。
- `SegmentedControl`：两到五个简短视图，需要立即切换内容时使用。
- `Checkbox`：用户可能选择多个选项时使用。
- `Select`：很长的选项列表。

| 组件 | 适用场景 |
|---|---|
| `RadioCards` | 每个选项需要价格、元信息或图标，并且卡片适合并排比较 |
| `SegmentedControl` | 两到五个针对同一内容的简短视图需要立即切换 |
| `Checkbox` | 用户可以选择多个选项，或某个选项是“是或否”的设置 |
| `Select` | 列表很长，或选项数量很多，例如国家或团队 |
| `Switch` | 单个设置立即生效，不需要分组 |
