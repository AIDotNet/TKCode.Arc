---
title: 变形选择
summary: 触发器展开为列表的选择器，带有滑动高亮和键入查找。
source-hash: 6935d8323e05
---

<Demo name="GroupedWithSearch" />

## 何时使用

`MorphSelect` 是用于表单或属性面板中已知列表的单值选择器。收起时，触发器是一个与其标签等宽的药丸形状；展开时，同一形状会生长为选项列表，让人看到列表从正在修改的值中展开。它适合选择很重要、选项已知的场景：设置页、详情面板和工具栏，在这些地方普通下拉框显得过于笨重。

- **开发者控制台中的部署设置**：发布时从云区域分组中选择环境（Production、Staging、Preview）和地区。“Americas”“Europe”这样的分组标题让人无需浏览整个列表就能找到地区。
- **项目跟踪工具中的属性面板**：任务的状态、优先级和负责人从简短的已知列表中选取。紧凑的药丸排在描述上方的一行中，每个都在原位展开。
- **CRM 详情面板中的负责人与团队选择**：客户负责人从销售团队中选出。选项超过八个时，搜索框会自动出现，让较长的团队列表依然好用。
- **紧凑的状态与优先级工具栏**：看板或表格工具栏用带图标的药丸显示当前筛选条件。隐藏标签可以让工具栏更短，触发器仍以标签作为无障碍名称。
- **金融与账单设置**：发票币种、结算周期或税务地区是已知列表，每个选项在名称旁边的列表中带有一个简短的元信息值，例如汇率或时差。

## 何时不用

- **自由文本，或来自远程数据源的长列表**：用 `Combobox`，它会随输入搜索，并把已输入的文字保留为查询词。
- **选择多个值**：用 `MultiSelect`，它把每个已选值保留为词元，并在两次选择之间保持展开。
- **执行操作而非选择值**：用 `DropdownMenu`，其选项执行重命名或归档等命令。
- **三到四个且都能放进屏幕的选项**：用 `SegmentedControl` 或 `ChipGroup`，所有选项都可见，无需额外的展开步骤。
- **密集表单中只需一个收起的列表即可满足的普通带标签字段**：用 `Select`，它是带固定列表的经典字段。

## 用法

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:MorphSelect Label="状态" SelectedIndex="1">
        <arc:SelectItem Content="待办" />
        <arc:SelectItem Content="进行中" />
        <arc:SelectItem Content="待审核" />
        <arc:SelectItem Content="已完成" />
        <arc:SelectItem Content="已取消" IsEnabled="False" />
    </arc:MorphSelect>
</UserControl>
```

`MorphSelect` 派生自 Avalonia 的 `SelectingItemsControl`，因此 `ItemsSource`、`Items`、`SelectedItem`、`SelectedIndex`、`SelectedValue`、`Selection` 和 `SelectionChanged` 的行为与列表框一致。控件另外提供触发器、变形的展开面板、标签飞行动画、搜索以及分组列表模型。`SelectItem` 提供每个选项，代码中可以用 `SelectOption` 做同样的事。

### 结构

1. **标签**（`Label`）：小号字体、中等字重、次要文字颜色，位于触发器上方 8 px。无论是否绘制，它都是触发器及其列表的无障碍名称。
2. **触发器**：高 44 px 的药丸，1 px 边框，值前有 14 px 内边距，箭头保留 36 px。圆角半径 22 px，两端完全圆润。宽度随所选标签变化，再加上 50 px 的内边距与箭头，最大为 320 px。
3. **值或占位文字**：所选标签使用前景色。未选择时，以弱化颜色显示 `Placeholder`，默认值为 "Select"。
4. **箭头**：距触发器右边缘 12 px。
5. **展开面板**（展开状态）：触发器生长为宽 `PanelWidth` 的面板（默认 272 px，且不窄于触发器）。面板在列表四周有 6 px 的内边距，列表最高为 `MaxListHeight`（默认 296 px），超出时可滚动。
6. **选项行**：高 36 px。每组连续共享同一 `Group` 的选项上方会插入一个分组标题，标题高 28 px。
7. **高亮**：前景色的柔和淡色，透明度为 6.5%，随人在列表中移动而在选项之间滑动。
8. **搜索框**（开启搜索时）：在展开的触发器内替代值显示。其清除按钮使用前景色 8% 的淡色。
9. **焦点环**：触发器获得焦点时绘制，宽 2 px，位于触发器外 4 px。

### 选择模式与宽度

触发器是单选，其宽度由标签决定，因此较长的标签会得到较宽的药丸。当选项文字较长、列表需要比默认的 272 px 更宽时，设置 `PanelWidth`。当触发器位于工具栏右边缘时，将 `PanelAlignment` 设为 `End`，让面板朝空白处展开，而不是伸到窗口之外。

```xml
<arc:MorphSelect Label="优先级" PanelAlignment="End" PanelWidth="220">
    <arc:SelectItem Content="紧急" />
    <arc:SelectItem Content="高" />
    <arc:SelectItem Content="中" />
    <arc:SelectItem Content="低" />
    <arc:SelectItem Content="无优先级" />
</arc:MorphSelect>
```

### 分组、图标与元信息

为每个选项设置 `Group`、`Icon` 和 `Meta`。每组连续共享同一 `Group` 的选项上方会插入一个分组标题，因此请在源码中把同一组的选项放在一起。`Icon` 接受 Lucide 几何图形或图片。`Meta` 是尾部的简短细节，例如时差、汇率或数量，以等宽数字样式显示。

```csharp
var zone = new MorphSelect
{
    Label = "时区",
    SelectedValue = "London",
    ItemsSource = cities.Select(c => new SelectOption(c.City, c.City)
    {
        Group = c.Region,
        Meta = c.Utc,
        Keywords = c.Zone,
        Icon = ArcIcons.Globe
    }).ToArray()
};
zone.SelectionChanged += (_, _) => Console.WriteLine(zone.SelectedValue);
```

当选项属于人们已经习惯的几类（例如地区、套餐或团队）时使用分组。不要为只有一个选项的组添加分组，因为标题占用的空间会超过选项本身。

### 紧凑工具栏

使用 `IsLabelVisible="False"` 隐藏标签。触发器仍以 `Label` 作为无障碍名称，因此药丸保持紧凑，屏幕阅读器依然能读出它的名称。

<Demo name="CompactToolbar" />

```xml
<arc:MorphSelect Label="状态" IsLabelVisible="False" />
<arc:MorphSelect Label="优先级" PanelAlignment="End" PanelWidth="220"
                 ItemsSource="{Binding Priorities}" />
```

只有当药丸的值本身能说明情况时才隐藏标签，例如工具栏行中的状态或优先级。在表单中，如果值可能有多种理解，请让标签保持在触发器上方可见。

### 搜索

`SearchMode` 决定展开的面板是否有搜索框。默认值 `Auto` 在选项超过八个时添加搜索框，`On` 始终添加，`Off` 从不添加。`SearchPlaceholder` 设置其提示文字，默认为所选标签或 "搜索"。在收起的触发器上键入字母会跳转到匹配的选项并展开列表。列表展开且开启搜索后，键入的文字进入搜索框，并按标签、元信息和关键字进行匹配。

```xml
<arc:MorphSelect Label="负责人"
                 SearchMode="On"
                 SearchPlaceholder="查找人员"
                 ItemsSource="{Binding Owners}" />
```

对于超过约八个人员、项目或账号的任何列表，开启搜索。短列表保持 `Auto`，不显示搜索框；当列表按排名排序、而搜索会把它隐藏时，使用 `Off`。

### 绑定到视图模型

使用 CommunityToolkit.Mvvm 时，视图模型拥有值与列表，变形选择器通过绑定修改值。值的标签为只读，并显示在摘要中。

```csharp
public partial class DeploymentSettingsViewModel : ObservableObject
{
    public IReadOnlyList<SelectOption> Regions { get; }

    [ObservableProperty]
    private string? _regionId = "us-east-1";

    [ObservableProperty]
    private bool _isRegionOpen;

    public DeploymentSettingsViewModel(ICloudCatalog catalog)
    {
        Regions = catalog.Regions()
            .Select(r => new SelectOption(r.Id, r.Name) { Group = r.Area, Meta = r.Code })
            .ToArray();
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        await _deployments.SetRegionAsync(RegionId);
    }
}
```

```xml
<StackPanel Spacing="16" HorizontalAlignment="Left">
    <arc:MorphSelect Label="地区"
                     ItemsSource="{Binding Regions}"
                     SelectedValue="{Binding RegionId, Mode=TwoWay}"
                     IsOpen="{Binding IsRegionOpen, Mode=TwoWay}" />
    <arc:Button Content="保存地区" Command="{Binding SaveCommand}" />
</StackPanel>
```

- `SelectedValue` 为双向绑定，保存的是稳定的标识符，因此视图模型能收到选择结果。应使用值而不是索引，这样重新加载列表时选择不会改变。
- `IsOpen` 为双向绑定。当视图模型需要在导航前先关闭列表，或其他地方的变化应当将其展开时，绑定它。
- `SelectedText` 为只读，保存所选项的标签。把它显示在摘要行中，而不要把视图模型属性反向绑定到它。
- 对每次选择都必须执行的副作用使用 `SelectionChanged`，保存操作仍由命令完成。

### 校验

触发器没有自己的消息行。在字段下方放置一个 `TextBlock` 并绑定到视图模型的错误，消息使用危险文字颜色。应在列表收起时校验，而不是在人移动高亮时校验，以免消息在指针下方变化。

| 规则 | 何时检查 | 显示在哪里 |
|---|---|---|
| 必须选择一个值 | 保存时，或列表在没有值的情况下收起时 | 触发器下方的消息，并把焦点移到此处 |
| 所选地区仍然可用 | 保存时，因为选项之后可能被禁用 | 触发器下方的消息，并指明该地区 |
| 账号允许该选择 | 服务端响应时 | 同一消息行，值变化时清除 |

### 示例：部署设置面板

开发者控制台中的部署设置面板。环境与地区是带可见标签的收起式触发器，环境中有一个禁用选项，它说明了为何不能使用。截图展示静止状态：触发器药丸、它们的标签，以及尚无值的字段的占位文字。

![部署设置面板，包含环境与地区药丸以及负责人占位文字](images/DeploymentSettings.png)

```xml
<StackPanel Width="560" Spacing="20">
    <TextBlock Text="Deployment settings"
               FontSize="18"
               FontWeight="SemiBold"
               Foreground="{arc:ArcDesignTokenResource Foreground}" />
    <arc:MorphSelect Label="Environment" SelectedIndex="1">
        <arc:SelectItem Content="Production" />
        <arc:SelectItem Content="Staging" />
        <arc:SelectItem Content="Preview" />
        <arc:SelectItem Content="Archived" IsEnabled="False" />
    </arc:MorphSelect>
    <arc:MorphSelect Label="Region" SelectedIndex="2">
        <arc:SelectItem Content="US East (Virginia)" Group="Americas" Meta="us-east-1" />
        <arc:SelectItem Content="US West (Oregon)" Group="Americas" Meta="us-west-2" />
        <arc:SelectItem Content="EU West (Dublin)" Group="Europe" Meta="eu-west-1" />
        <arc:SelectItem Content="EU Central (Frankfurt)" Group="Europe" Meta="eu-central-1" />
        <arc:SelectItem Content="Asia Pacific (Tokyo)" Group="Asia Pacific" Meta="ap-northeast-1" />
    </arc:MorphSelect>
    <arc:MorphSelect Label="Release owner" Placeholder="Choose an owner">
        <arc:SelectItem Content="Priya Raman" />
        <arc:SelectItem Content="Marcus Hale" />
        <arc:SelectItem Content="Elena Ruiz" />
    </arc:MorphSelect>
    <TextBlock Text="Changes apply to the next deployment."
               FontSize="12"
               Foreground="{arc:ArcDesignTokenResource TextMuted}" />
</StackPanel>
```

### 文案规范

- **标签**：命名属性的名词，使用句首大写，例如“环境”或“地区”。即使标签被隐藏，它也会为辅助技术命名触发器。
- **占位文字**：补全标签的指令，例如“请选择负责人”。通用字段使用默认的“选择”即可。
- **选项标签**：人们在工作中使用的名称，例如“美国东部（弗吉尼亚）”或“生产环境”。把标识符放进 `Meta`，把别名放进 `Keywords`。
- **分组标题**：表示类别的复数名词，例如“美洲”或“欧洲”。
- **搜索占位文字**：说明要搜索什么，例如“查找人员”或“查找地区”。

| 不要写 | 改为 |
|---|---|
| 选择… | 请选择负责人 |
| 环境缩写 ENV | 环境 |
| 地区（必填） | 地区 |
| 杂项 | 其他地区 |
| 点击这里选择 | 请选择地区 |

### 最佳实践

**推荐**

- 表单中保持标签可见，只有在值本身已能说明控件用途的工具栏中才隐藏标签。
- 绑定 `SelectedValue`，并在视图模型中保存稳定的标识符，这样重命名选项不会改变已保存的选择。
- 每个分组至少给出两个选项，让标题帮助人快速浏览列表。
- 对于人们按名称熟知、超过约八个选项的列表，开启搜索。

**避免**

- 用药丸执行重命名或归档等操作。这些应放在 `DropdownMenu` 中，其选项执行命令。
- 在窄工具栏中使用长标签。触发器会随标签增长至 320 px，这会把其他工具栏项挤开。
- 禁用选项却不在其标签或周围文案中说明原因。无法选择“已归档”的人需要知道原因。
- 列表展开时重新排序选项。高亮停留在原来的索引上，人可能选错项。
- 仅用箭头作为字段可交互的提示。应保持标签和药丸形状可见。

### 常见问题

**选择较长的值时，药丸为什么会变宽？** 触发器的宽度等于标签加上内边距和箭头，最大 320 px，因此药丸恰好容纳其值。如果工具栏中的位置不能随值变化而移动，请为它提供固定的位置。

**如何让列表在最后一个选项处展开？** 在收起的触发器上按 `End`，列表会在最后一个选项处展开。`Home` 则在第一个选项处展开。Enter、Space、`Down` 和 `Up` 会在所选选项处展开。

**将 `SearchMode` 设为 `Off` 时，搜索框会显示吗？** 不会。`Off` 从不显示搜索框，即使是长列表；在收起的触发器上键入仍会跳转到匹配的选项。

**如何避免列表遮挡页面边缘？** 将 `PanelAlignment` 设为 `End`，使展开面板向触发器左侧生长。面板相对于触发器保持位置，不会翻转到另一侧。

**列表能显示来自服务器的选项吗？** 在视图模型中加载选项，并在它们到达时赋值 `ItemsSource`。选择按值匹配，因此每次加载时都应保持标识符稳定。

## API 参考

<!-- api: MorphSelect -->

<!-- api: SelectItem -->

## 无障碍

- `Label` 为触发器命名，即使 `IsLabelVisible` 为 `False` 也是如此。列表也由同一标签命名。
- 触发器是带有列表框弹出层的组合框。列表展开时会暴露展开状态，并播报当前高亮的选项。
- 方向键移动高亮并跳过禁用选项。`Page Up` 和 `Page Down` 每次移动八个选项，`Home` 和 `End` 分别前往第一个和最后一个选项。
- `Enter` 或 `Space` 选取高亮选项。`Alt+Up` 选取并关闭列表。`Escape` 关闭列表，并把焦点返回触发器。
- 选项分组以其标题命名。开启搜索时，结果数量会以礼貌的方式播报；列表展开期间，搜索框接管组合框角色。

### 键盘

| 按键 | 操作 |
|---|---|
| 收起的触发器上的 `Enter`、`Space`、`Down`、`Up` | 在所选选项处展开列表 |
| 收起的触发器上的 `Home` / `End` | 在第一个或最后一个选项处展开列表 |
| 列表展开时的 `Down` / `Up` | 把高亮移到下一个或上一个可用选项；到达两端时不循环 |
| `Page Down` / `Page Up` | 每次把高亮移动八个选项 |
| 列表展开时的 `Home` / `End` | 把高亮移到第一个或最后一个选项 |
| 列表展开时的 `Enter` 或 `Space` | 选取高亮选项并关闭列表 |
| `Alt+Up` | 选取高亮选项并关闭列表 |
| 可打印字符 | 跳转到第一个匹配的选项；开启搜索时，输入到搜索框中 |
| `Escape` | 不选取即关闭列表，并把焦点返回触发器 |
| `Tab` | 不选取即关闭列表 |

## 动效

- 触发器展开为列表。其圆角从药丸形状变形为面板的圆角；面板打开时带有轻微弹跳，关闭时无过冲。
- 高亮在选项之间滑动。
- 选中的标签从列表飞回触发器，触发器宽度调整为新标签的宽度。
- 触发器中的值会朝列表的方向滚入，使这一变化看起来像同一个值在移动。

| 变化 | 完整 | 减弱 | 关闭 |
|---|---|---|---|
| 列表展开 | 触发器以轻微弹跳生长为面板，圆角变形 | 尺寸跳到展开形状，并伴随短暂淡入 | 立即完成 |
| 列表收起 | 面板无过冲地缩回药丸 | 尺寸跳到收起形状，并伴随短暂淡入 | 立即完成 |
| 高亮移动 | 滑动到下一个选项 | 直接跳到该选项 | 立即完成 |
| 选中选项 | 标签飞入触发器，宽度调整到位 | 短暂淡入 | 立即完成 |
| 搜索接管触发器 | 搜索框以轻微模糊淡入 | 短暂淡入 | 立即完成 |

## 相关组件

- `Select`：带固定列表的普通带标签选择字段。
- `Combobox`：用户键入即可搜索的单选控件。
- `MultiSelect`：需要选择多个值时使用。
- `DropdownMenu`：菜单用于执行操作而非选择值时使用。

| 组件 | 适用场景 |
|---|---|
| `Select` | 带固定列表的经典带标签字段适合该表单 |
| `Combobox` | 用户需要键入查找值，或列表很长且来自远程 |
| `MultiSelect` | 需要选择多个值，且每个值都以词元的形式保持可见 |
| `DropdownMenu` | 菜单执行重命名或归档等命令 |
| `SegmentedControl` | 应一次性显示两到四个选项 |
| `ChipGroup` | 少数几个切换频繁的筛选项，且每个选项都保持可见 |
