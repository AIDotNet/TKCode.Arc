---
title: 搜索框
summary: 易于识别的搜索入口，并带有清晰的操作提示。
source-hash: 72dd93e3ef34
---

<Demo name="FilterList" />

## 何时使用

`SearchField` 是用于已经显示着可搜索条目的视图的搜索框：客户表格、发票列表、带有大量开关的设置页。它位于结果上方，随人员输入逐步收窄结果；清除按钮出现时宽度保持不变，因此人员操作时周围的工具栏不会跳动。

- **在同一页面筛选客户或发票表格**：表格就在屏幕上，每次按键都会立即收窄行，人员马上能看到效果。在 `TextChanged` 处理程序中筛选，表格的排序和分页保持不变。
- **在很长的设置页中查找设置或功能开关**：一个有四十个开关的设置页，可以在人员输入 “webhook” 或 “export” 等词时隐藏不匹配的行，并保留分组标题。
- **在管理控制台中按 ID 打开记录**：操作员粘贴 `inv_2026_0412` 并按 Enter。`SearchSubmitted` 携带文字，因此控制台可以直接打开该记录，而不是筛选列表。
- **数据密集工具中的紧凑工具栏**：带筛选栏的监控或账单表格使用 `Size="Small"`，让搜索框与其他工具栏控件在 36 px 的行内对齐。
- **一键清空的搜索**：Escape 可以从键盘清空搜索框，清除按钮会把焦点返回搜索框，因此操作员不用鼠标就能完成搜索、清空、再搜索。

## 何时不用

- **点击后会打开结果面板的紧凑页眉搜索**：使用 `ExpandingSearch`。它以图标开始，按需展开，并把结果归组显示在搜索框下方。
- **搜索文字需要作为表单值保存**：使用 `Combobox`，它让人从列表中选择，保存的是所选值，而不是自由输入的查询词。
- **并排放置的多个筛选器与排序选项**：使用 `FilterToolbar`，它把搜索框与其他筛选控件排在一起并保持对齐。
- **不是查询的名称、标题或备注**：使用 `Input`。搜索框会在 Escape 时清空、在 Enter 时提交，这并不是名称或备注需要的行为。
- **多行文字，例如给支持助手的长问题**：使用 `Textarea`。

## 用法

搜索框起始位置有一个搜索图标。输入框中有文字时，末尾会出现清除按钮。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:SearchField Label="搜索人员"
                     Placeholder="按姓名筛选"
                     Width="360"
                     HorizontalAlignment="Left" />
</UserControl>
```

`SearchField` 派生自共享的 Arc 字段基类，该基类建立在 Avalonia 的 `TextBox` 之上。`Text` 是查询词且为双向属性，`TextChanged` 在每次按键时触发，`PlaceholderText`、`IsReadOnly`、光标、选择、撤销、输入法和剪贴板都与文本框的表现一致。字段在此之上加入标签、搜索图标、清除按钮与 Arc 外框。它还通过 `Description` 和 `ErrorMessage` 提供 `Input` 的辅助文字行与错误行，因此当搜索因某个必须让人看到的原因失败时，可以使用与表单字段相同的消息行。

### 结构

1. **标签**（`Label`）：14 px、中等字重，位于外框上方 8 px。它是字段的无障碍名称。只有当可见的标题已经为搜索命名时，才用 `IsLabelVisible="False"` 隐藏它，例如带有明确标题的工具栏。
2. **搜索图标**：位于外框起始处、18 px 的搜索图标，描边为 2 px。静止时为弱化色，字段获得焦点或有文字时变为完整前景色。它对辅助技术隐藏。
3. **文字与占位文字**（`Placeholder`）：查询词。字段为空时以弱化文字颜色显示占位文字，人员开始输入之前搜索图标也保持弱化。
4. **清除槽**：外框末尾始终保留一个 24 px 的方形槽位。24 px 的清除按钮（中间是 16 px 的叉号）只在字段有文字且可编辑时出现在槽位中。由于槽位始终存在，字段宽度保持不变。
5. **辅助文字行与错误行**（`Description`、`ErrorMessage`）：与 Field 共用的消息行，外框下方 12 px 的文案，行为与 `Input` 相同。

### 尺寸

`Size` 设置外框高度：`Small` 为 36 px，`Medium` 为 44 px（默认），`Large` 为 50 px。`Large` 还会把文字提升到基础字号及其行高，因此作为页面主搜索时阅读效果更好。

<Demo name="Sizes" />

每个工具栏只用一种尺寸。密集的管理表格和筛选栏用 `Small`，列表上方的搜索用 `Medium`，页面主要操作之一的搜索（例如目录或帮助中心）用 `Large`。工具栏中的字段应与旁边的控件使用相同的 `Size`，让整行共享同一高度。

### 绑定查询词

`Text` 是双向属性，因此视图模型可以读取查询词、重置它，并在页面导航之间保留它。使用默认绑定模式，由视图模型筛选它所拥有的行。

```csharp
public partial class CustomerListViewModel : ObservableObject
{
    private static readonly string[] AllCustomers =
    [
        "Acme Analytics", "Acme Labs", "Acme Retail Group", "Northwind Logistics",
        "Contoso Health", "Fabrikam Energy"
    ];

    [ObservableProperty]
    private string _query = string.Empty;

    public ObservableCollection<string> Visible { get; } = new(AllCustomers);

    partial void OnQueryChanged(string value)
    {
        Visible.Clear();
        foreach (var name in AllCustomers.Where(n => n.Contains(value, StringComparison.CurrentCultureIgnoreCase)))
        {
            Visible.Add(name);
        }
    }

    [RelayCommand]
    private void ClearSearch() => Query = string.Empty;
}
```

```xml
<StackPanel Spacing="12">
    <arc:SearchField Label="搜索客户"
                     Placeholder="按姓名筛选"
                     Text="{Binding Query}"
                     Width="360"
                     HorizontalAlignment="Left" />
    <ItemsControl ItemsSource="{Binding Visible}" />
</StackPanel>
```

`ClearSearch` 是应用自己的命令；字段自身通过清除按钮和 Escape 清空，因此只有页面其他位置有“清空”链接时才需要该命令。控件本身没有命令。

### 边输入边筛选

`Text` 在每次按键时都会变化。在 `TextChanged` 处理程序中筛选，或在上面视图模型的属性变更中筛选。如果筛选开销较大，请在代码中做防抖处理，让列表在人员停顿后再更新。

```csharp
search.TextChanged += (_, _) =>
{
    var query = search.Text ?? string.Empty;
    list.Children.Clear();
    foreach (var name in names.Where(n => n.Contains(query, StringComparison.CurrentCultureIgnoreCase)))
    {
        list.Children.Add(new TextBlock { Text = name });
    }
};
```

### 提交

Enter 会触发 `SearchSubmitted` 并携带字段中的文字，该事件被标记为已处理，因此 Enter 不会按下窗口中的默认按钮。把它用于只打开一个结果的搜索，例如 ID 或工单号；不要用于已经随每次按键更新的列表。

```csharp
search.SearchSubmitted += (_, e) => submitted.Text = $"已提交“{e.Query}”";
```

Escape 会清空有文字且非只读的字段，并让焦点留在字段中。

### 从代码清空

调用 `Clear` 可以从代码清空字段。除非传入 `false`，焦点会保留在字段中。这一改变会触发常规的文字变化，因此 `TextChanged` 处理程序中的筛选也会运行。

```csharp
search.Clear();
search.Clear(focus: false);
```

### 筛选大型列表

字段会把每次按键交给 `Text`。视图模型应在内存中保留完整数据集，并从该数据集而不是屏幕上的行进行筛选，这样清空查询词就能恢复所有行。对于远程数据源，应使用 `SearchSubmitted` 事件或等待输入停顿，而不是每次按键都发起一次请求。

```csharp
search.TextChanged += async (_, _) =>
{
    var query = search.Text ?? string.Empty;
    await Task.Delay(250);
    if (query != (search.Text ?? string.Empty))
    {
        return;
    }
    await LoadCustomersAsync(query);
};
```

延迟检查会丢弃已被更新按键超越的查询，因此慢速的响应永远不会覆盖最新文字的结果。

### 示例：带搜索筛选的客户表格

账单控制台中的客户表格。搜索框位于表格上方，查询词 `acme` 已输入，因此清除按钮和筛选后的行都可见。搜索框与行是同一视图中的兄弟元素，而不是另一个页面，因此人员能在同一视图中看到每次按键的效果。其下方的匹配数量用文字说明结果，而不只是通过行来体现。

![带搜索筛选、将表格缩减为三行匹配结果的客户搜索](images/CustomerSearch.png)

```xml
<StackPanel Width="620" Spacing="16">
    <arc:SearchField Label="Search customers"
                     Placeholder="Filter by name or ID"
                     Text="acme" />
    <TextBlock Text="3 customers match “acme”."
               FontSize="14"
               Foreground="{arc:ArcDesignTokenResource TextSecondary}" />
    <Border BorderBrush="{arc:ArcDesignTokenResource Border}"
            BorderThickness="1"
            CornerRadius="8">
        <StackPanel>
            <Grid ColumnDefinitions="*,160,140" Margin="16,12">
                <TextBlock Text="Customer" FontWeight="SemiBold"
                           Foreground="{arc:ArcDesignTokenResource TextMuted}" />
                <TextBlock Grid.Column="1" Text="Customer ID" FontWeight="SemiBold"
                           Foreground="{arc:ArcDesignTokenResource TextMuted}" />
                <TextBlock Grid.Column="2" Text="Plan" FontWeight="SemiBold"
                           Foreground="{arc:ArcDesignTokenResource TextMuted}" />
            </Grid>
            <Border Height="1" Background="{arc:ArcDesignTokenResource Border}" />
            <Grid ColumnDefinitions="*,160,140" Margin="16,12">
                <TextBlock Text="Acme Analytics" Foreground="{arc:ArcDesignTokenResource Foreground}" />
                <TextBlock Grid.Column="1" Text="cus_1042"
                           Foreground="{arc:ArcDesignTokenResource TextSecondary}" />
                <TextBlock Grid.Column="2" Text="Enterprise"
                           Foreground="{arc:ArcDesignTokenResource Foreground}" />
            </Grid>
            <Border Height="1" Background="{arc:ArcDesignTokenResource Border}" />
            <Grid ColumnDefinitions="*,160,140" Margin="16,12">
                <TextBlock Text="Acme Labs" Foreground="{arc:ArcDesignTokenResource Foreground}" />
                <TextBlock Grid.Column="1" Text="cus_2031"
                           Foreground="{arc:ArcDesignTokenResource TextSecondary}" />
                <TextBlock Grid.Column="2" Text="Team"
                           Foreground="{arc:ArcDesignTokenResource Foreground}" />
            </Grid>
            <Border Height="1" Background="{arc:ArcDesignTokenResource Border}" />
            <Grid ColumnDefinitions="*,160,140" Margin="16,12">
                <TextBlock Text="Acme Retail Group" Foreground="{arc:ArcDesignTokenResource Foreground}" />
                <TextBlock Grid.Column="1" Text="cus_1187"
                           Foreground="{arc:ArcDesignTokenResource TextSecondary}" />
                <TextBlock Grid.Column="2" Text="Trial"
                           Foreground="{arc:ArcDesignTokenResource Foreground}" />
            </Grid>
        </StackPanel>
    </Border>
</StackPanel>
```

### 文案规范

为被搜索的集合命名，并让占位文字展示匹配的示例，而不是指令。

| 不要写 | 改为 |
|---|---|
| 搜索 | 搜索客户 |
| 输入以搜索 | 按姓名或 ID 筛选 |
| 没有结果 | 没有客户与 “acme” 匹配。 |
| 清空 | 清除搜索 |
| 前往 | 打开发票 |

使用句子式写法。把匹配数量以纯文字形式放在字段旁边，并把空结果写成一句重复查询词的话，让人看清哪些内容没有匹配。

### 最佳实践

**推荐**

- 在标签中为集合命名，例如“搜索客户”而不是“搜索”。人员知道查询词作用于什么，屏幕阅读器也会播报同样的名称。
- 从完整数据集中筛选，而不是从屏幕上的行中筛选。清空查询词后即可恢复所有行。
- 在字段旁边显示匹配数量。人员在扫视空列表之前就能看出查询词没有匹配任何内容。
- 保持字段宽度固定。预留的清除槽已经让宽度保持稳定，因此不要在按钮出现时改变字段宽度。

**避免**

- 对远程数据源每次按键都发送一次请求。应对查询词做防抖，或等待 Enter，因为对较早查询词的响应可能晚于后面的响应到达。
- 在已经随每次按键筛选的列表中使用 `SearchSubmitted`。这样 Enter 不会产生任何效果，人员会不知道它有什么用。
- 在搜索框中放入一个已选值。应使用 `Combobox`，它保存的是选择，而不是自由输入的查询。
- 只依赖 Escape 清空字段。清除按钮是可见的提示，而 Escape 并不容易被发现。

### 常见问题

**为什么只读字段中按 Escape 没有效果？** Escape 只会清空有文字且非只读的字段。清除按钮遵循同样的规则，因此 `IsReadOnly` 为 true 时它会隐藏。

**如何翻译清除按钮？** 设置 `ClearText`。默认值为 "Clear search"，它也是按钮的无障碍名称。

**`Clear` 会移动焦点吗？** 默认会。`Clear(focus: false)` 会清空字段并保留焦点所在位置，这与 Escape 的行为一致。

**字段为空时按 Enter 会提交吗？** 会。`SearchSubmitted` 会以空的 `Query` 触发，因此如果空提交应当不做任何事，请在处理程序中忽略空查询词。

## API 参考

<!-- api: SearchField -->

## 无障碍

- `Label` 为字段命名。搜索图标对辅助技术隐藏，因此字段通过标签和值被播报。
- 清除按钮默认名称为“清除搜索”。设置 `ClearText` 可将其翻译。
- 按 Enter 提交查询词，按 Escape 从键盘清空字段。
- 清除按钮显示时可通过键盘访问，获得焦点时绘制焦点环。在其上按 Enter 会清空字段并把焦点返回字段，因此键盘用户不会丢失当前位置。
- `Description` 与 `ErrorMessage` 构成字段的帮助文本，与 `Input` 一样，在名称和值之后朗读。
- 如示例所示，在字段旁边以纯文字显示匹配数量，让人无需逐行计数即可看到结果。

### 键盘

| 按键 | 操作 |
|---|---|
| 文字按键 | 编辑查询词；每次按键都会运行 `TextChanged` |
| `Enter` | 以查询词触发 `SearchSubmitted` 并将该按键标记为已处理 |
| `Escape` | 清空有文字且非只读的字段；焦点保留在字段中 |
| `Tab` / `Shift+Tab` | 清除按钮显示时移到它，然后移到下一个控件 |
| 清除按钮上的 `Enter` | 清空字段并把焦点返回字段 |
| `Left` / `Right`、`Home` / `End`、`Shift` 选择、`Ctrl+A` | 原生文本框编辑 |

## 动效

- 清除按钮从略小的尺寸放大出现，并伴有轻微模糊。
- 其槽位始终保留，因此无论按钮是否可见，字段宽度都保持不变。
- 按下清除按钮时，按钮会略微缩小，仅在完整动效下生效。
- 减弱动效时，清除按钮只淡入淡出，没有缩放也没有模糊。关闭动效时立即显示或隐藏。

| 变化 | 完整 | 减弱 | 关闭 |
|---|---|---|---|
| 清除按钮出现 | 以灵敏的弹簧从 0.8 放大出现，并伴有柔和模糊 | 淡入 | 立即完成 |
| 清除按钮消失 | 先播放退出动画再移除 | 淡出 | 立即完成 |
| 按下清除按钮 | 缩放至 0.96 | 不缩放 | 不缩放 |

## 相关组件

- `ExpandingSearch`：页眉中的紧凑搜索，并在其下方显示结果。
- `Combobox`：从列表中选择一个值。
- `Input`：通用单行输入框。

| 组件 | 适用场景 |
|---|---|
| `ExpandingSearch` | 搜索位于页眉或工具栏中，只在使用时才展开 |
| `FilterToolbar` | 搜索与其他筛选、排序选项或视图切换放在一起 |
| `Combobox` | 所选值需要保存在表单中，而不是作为查询词 |
| `Input` | 文字是名称、标题或值，而不是查询词 |
| `Textarea` | 查询词需要多行，例如给助手的问题 |
| `Select` | 人员从固定列表中选择一个选项，而不是输入 |
