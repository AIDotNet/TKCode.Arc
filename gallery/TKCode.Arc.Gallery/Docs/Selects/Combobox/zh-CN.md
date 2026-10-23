---
title: 组合框
summary: 无需离开字段即可搜索并从列表中选择。
source-hash: c6681c6f8080
---

<Demo name="Searchable" />

## 何时使用

`Combobox` 是用于过长、难以扫视列表的单值选择器，适合人们已经知道想要什么名称的场景。它在表单中的外观与输入框相似：可见的标签、44 px 的字段和一行说明，人们输入时列表在字段下方展开。凡是滚动查找的成本高于直接输入的列表，都适合使用它。

- **账号设置中的时区和国家**：团队从 IANA 时区列表中设置默认时区或账单国家。人们输入“berlin”“new york”或“UTC+01”，即可一步找到匹配项。
- **CRM 或帮助台中的账号负责人和指派对象**：商机负责人或工单指派对象从团队通讯录中选取。`Keywords` 让人们可以通过邮箱别名或团队名称找到“Priya Raman”。
- **订单编辑器中的产品与 SKU 查找**：一条订单明细需要从目录中选取一个产品。关键词包含 SKU 和旧产品名称，因此搜索已停售的名称仍能找到当前的产品。
- **财务工具中的账本与成本中心选择**：一份费用报销需要从数百个成本中心中选出一个。已停用的选项仍然可见，人们因此能看到某个中心存在，但已不允许新增支出。
- **选择目标的开发者工具**：部署表单需要从长列表中选择一个环境或一个地区。搜索会缩小范围，所选标签则作为人们阅读的值保留在字段中。

## 何时不用

- **简短固定、无需输入的列表**：用 `Select`。三到七个选项在列表中比藏在搜索框后面更容易阅读。
- **需要选择多个值**：用 `MultiSelect`，它把每个选择都以标记的形式保持可见，并且选完后不会关闭。
- **人们经常切换的面向**：用 `ChipGroup`，它的每个选项都可见，一次点击即可改变筛选。
- **搜索的目的是浏览内容而非设置值**：在页头使用 `ExpandingSearch`，它打开的是结果，而不是填入字段。
- **不是从列表中选择的自由文本**：用 `Input`。组合框只保存其选项中的值。

## 用法

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:Combobox Label="框架"
                  Width="280"
                  Description="输入以搜索。">
        <arc:SelectItem Content="Avalonia" Keywords="xaml dotnet" />
        <arc:SelectItem Content="AtomUI" Keywords="ant design" />
        <arc:SelectItem Content="MAUI" Keywords="xamarin" IsEnabled="False" />
        <arc:SelectItem Content="WinUI" />
    </arc:Combobox>
</UserControl>
```

`Combobox` 派生自 Avalonia 的 `SelectingItemsControl`，因此 `ItemsSource`、`Items`、`SelectedItem`、`SelectedIndex`、`SelectedValue`、`Selection` 和 `SelectionChanged` 的行为与列表框一致，所选项始终是其中的某个选项。控件新增的是字段本身：筛选列表的文本输入框、标签与说明行、弹出列表以及清除按钮。在 `SelectingItemsControl` 中，每个 `SelectItem` 就是一个选项，其上的 `Keywords` 参与搜索但从不显示。

### 结构

1. **标签**（`Label`）：字段上方的可见标签，使用小号字体尺寸、中等字重，位于字段上方 8 px。它同时为辅助技术标注字段及其列表。
2. **字段**：高 44 px（中等控件尺寸），1 px 边框、控件圆角、表面色背景，水平内边距 12 px。悬停和聚焦只改变颜色，不改变尺寸。
3. **文本输入框**：列表打开时显示查询文字。列表关闭时显示所选标签，而不是值。
4. **占位文字**（`Placeholder`）：默认文字为“搜索或选择…”。人们搜索时，所选标签以弱化的文字保留在字段中，因此当前的选择不会丢失。
5. **清除按钮**：22 px 的按钮，在已有选择时出现。它会清除选择和查询文字。
6. **说明**（`Description`）：字段下方的弱化提示行，12 px，使用弱化的文字颜色。
7. **列表**（弹出层）：在字段下方 8 px 处展开，使用面板圆角、5 px 的内边距，选项行高 36 px。列表最高 300 px，且不超过窗口高度的 40%（`MaxDropDownHeight`）。
8. **状态行**（`EmptyText`）：选项下方的结果数量；没有匹配项时显示空文字（默认为“没有找到匹配项”）。使用小号字体尺寸和弱化颜色。

### 选择数据

无论列表有五个选项还是五百个，字段都是相同的。区别在于项目数据：

- 对于在 XAML 中定义的短而固定的列表，使用 `SelectItem` 子元素，如用法示例所示。
- 对于来自代码或视图模型的列表，将 `ItemsSource` 与 `SelectOption` 记录配合使用。`SelectOption` 接收一个值和一个标签；`IsDisabled` 标记显示但不可用的选项；`Keywords` 添加搜索时匹配的词语。
- 当项目是你自己的对象时，设置 `TextSelector` 和 `ValueSelector`，使标签和存储的值来自属性，而不是 `ToString()`。

```csharp
var zone = new Combobox
{
    Label = "时区",
    Width = 320,
    ItemsSource = cities.Select(c => new SelectOption(c.City, c.City)
    {
        Keywords = $"{c.Zone} {c.Offset}"
    }).ToArray()
};
zone.SelectionChanged += (_, _) => Console.WriteLine(zone.SelectedValue);
```

### 绑定到视图模型

使用 CommunityToolkit.Mvvm 时，视图模型同时持有值和选项列表，组合框通过绑定编辑这两者。值是存储的标识符，标签则显示给人们看。

```csharp
public partial class TeamSettingsViewModel : ObservableObject
{
    public IReadOnlyList<SelectOption> Owners { get; }

    [ObservableProperty]
    private string? _ownerId;

    [ObservableProperty]
    private string? _timeZoneId = "europe-berlin";

    public TeamSettingsViewModel(IDirectoryService directory)
    {
        Owners = directory.ListMembers()
            .Select(m => new SelectOption(m.Id, m.DisplayName) { Keywords = m.Email })
            .ToArray();
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (OwnerId is null)
        {
            return;
        }
        await _settings.SaveAsync(OwnerId, TimeZoneId);
    }
}
```

```xml
<StackPanel Width="360" Spacing="16">
    <arc:Combobox Label="账号负责人"
                  Placeholder="搜索成员"
                  ItemsSource="{Binding Owners}"
                  SelectedValue="{Binding OwnerId, Mode=TwoWay}" />
    <arc:Combobox Label="默认时区"
                  ItemsSource="{Binding TimeZones}"
                  SelectedValue="{Binding TimeZoneId, Mode=TwoWay}" />
    <arc:Button Content="保存设置" Command="{Binding SaveCommand}" />
</StackPanel>
```

- `SelectedValue` 为双向绑定，未选择任何项时为 `null`。请用 `Mode=TwoWay` 绑定，使视图模型能接收用户的选择，并能从代码中设置字段。
- `SelectedText` 为只读，保存所选项的标签。请把它显示在摘要行中，而不要再绑定回去。
- `IsOpen` 为双向绑定。只有当命令需要在跳转前关闭列表时，才从代码中设置它。
- 不要为来自数据库的值绑定 `SelectedIndex`。重新加载列表时索引会变化，因此应改为绑定值。

### 搜索与筛选

默认情况下，搜索会以不区分大小写的子字符串匹配标签、元信息和关键词，结果列表保持项目原有的顺序。`SearchText` 报告当前的查询词，列表关闭时为空。

设置 `FilterPredicate` 可以改变匹配方式。它接收源项目和去除首尾空白后的查询词，并且会在每次按键时对每个项目执行一次，因此应保持开销很小。

<Demo name="CustomFilter" />

```csharp
combo.FilterPredicate = (item, query) =>
    item is SelectOption option && option.Label.StartsWith(query, StringComparison.CurrentCultureIgnoreCase);
```

对于人们按首字母扫视的列表，例如国家列表，使用前缀匹配。对于名称和别名，保留默认的子字符串匹配，使“york”能找到词中间的“New York”。

### 占位文字、空状态文字与状态

字段为空时显示 `Placeholder`。`EmptyText` 是没有任何匹配项时的状态行，它应当指出被搜索的对象，例如“没有匹配的时区。”。状态行要简短，因为它在出现时会被播报。

```xml
<arc:Combobox Label="国家"
              Placeholder="搜索国家"
              EmptyText="没有匹配的国家。" />
```

### 清除与焦点

`Clear()` 会清除选择和查询词，人们按下清除按钮时也会执行同样的操作。使用 `FocusInput()` 把焦点移到字段上，并打开列表，效果与人们用 Tab 切换到字段时相同。

```csharp
zone.Clear();
zone.FocusInput();
```

当空状态是合法的结果时，清除是正确的做法，例如一个可以匹配全部内容的筛选。对于必填的值，不要在保存时清空字段；应保留该值，并在字段旁边显示消息。

### 校验

组合框自身没有消息行。它的 `Description` 适合放置始终成立的约束（“用于报表计划。”）。对于可能不满足的规则，请在字段下方放置一个 `TextBlock`，把它的文本绑定到视图模型的错误，并使用危险色设计令牌对应的文字颜色。应在列表关闭时检查规则，而不是在每次按键时检查，这样消息就不会在人们仍在搜索时出现。

| 规则 | 何时检查 | 显示在哪里 |
|---|---|---|
| 必须选择一个值 | 保存时，或列表关闭且没有值时 | 字段下方的消息，并把焦点移到该处 |
| 值仍然可用 | 保存时，因为选项之后可能被禁用 | 字段下方的消息，并指出该选项 |
| 负责人必须属于团队 | 服务端返回结果时 | 同一消息行，值变化时清除 |

### 内联选项与禁用选项

禁用的选项会显示在列表中，但无法选取。方向键会跳过它们。这适用于存在但对用户不可用的选项，例如已关闭的成本中心。请在选项标签或说明中解释原因，而不要只把该行变灰。

<Demo name="InlineOptions" />

```xml
<arc:SelectItem Content="MAUI" Keywords="xamarin" IsEnabled="False" />
```

### 禁用

禁用的 `Combobox` 会保留其值并忽略输入。它无法打开，因此人们仍可读取该值，却无法更改它。当值由策略决定时使用它，例如工作区创建后不能更改的财年起始月份，并在说明中写明原因。

<Demo name="Disabled" />

### 示例：团队设置表单

管理控制台中的团队设置表单。负责人字段是一个可搜索的人员列表，关键词包含各人的邮箱地址，因此人们可以输入姓氏或域名。时区字段已关闭并选定了一个值；财年字段被禁用，并在说明中给出原因。标签是名词，占位文字说明要搜索什么，说明文字则写出每个选择的效果。

![团队设置表单，包含可搜索的负责人字段、时区字段和禁用的财年字段](images/TeamSettings.png)

```xml
<StackPanel Width="560" Spacing="20">
    <arc:Combobox Label="Account owner"
                  Width="360"
                  Placeholder="Search people"
                  Description="Receives billing and security emails.">
        <arc:SelectItem Content="Priya Raman" Keywords="priya.raman@acme.com finance" />
        <arc:SelectItem Content="Marcus Hale" Keywords="marcus.hale@acme.com operations" />
        <arc:SelectItem Content="Elena Ruiz" Keywords="elena.ruiz@acme.com support" />
        <arc:SelectItem Content="Tomas Berg" Keywords="tomas.berg@acme.com engineering" />
    </arc:Combobox>
    <arc:Combobox Label="Default time zone"
                  Width="360"
                  SelectedIndex="1"
                  Description="Used for report schedules and invoice dates.">
        <arc:SelectItem Content="Pacific time (UTC-08:00)" Keywords="los angeles seattle" />
        <arc:SelectItem Content="Central European time (UTC+01:00)" Keywords="berlin paris rome" />
        <arc:SelectItem Content="Japan time (UTC+09:00)" Keywords="tokyo osaka" />
    </arc:Combobox>
    <arc:Combobox Label="Fiscal year starts"
                  Width="360"
                  IsEnabled="False"
                  SelectedIndex="0"
                  Description="Set when the workspace is created and cannot change.">
        <arc:SelectItem Content="January" />
        <arc:SelectItem Content="April" />
    </arc:Combobox>
    <StackPanel Orientation="Horizontal" Spacing="8" HorizontalAlignment="Right">
        <arc:Button Content="Cancel" Variant="Secondary" />
        <arc:Button Content="Save settings" />
    </StackPanel>
</StackPanel>
```

### 文案规范

- **标签**：命名所选值的名词，使用句首大写，例如“账号负责人”或“默认时区”。不要加冒号或问号。
- **占位文字**：动作加对象，例如“搜索成员”或“搜索国家”。它不应重复标签。
- **说明**：一句话说明选择的效果，例如“用于报表计划”，或说明某个选项无法选取的原因。
- **空状态文字**：写明搜索的对象，并说明没有匹配项。只保留一行。
- **选项标签**：人们认得的名称，例如完整的时区名称或人员的显示名。别名应放在 `Keywords` 中，而不要放在标签里。

| 不要写 | 改为 |
|---|---|
| 选择 | 搜索成员 |
| 输入一个值 | 搜索或选择 |
| 没有找到 | 没有匹配的时区。 |
| 负责人： | 账号负责人 |
| 点击选择时区 | 选择默认时区 |

### 最佳实践

**推荐**

- 使用 `Keywords` 放置人们实际会输入的词，例如邮箱别名、城市、旧产品名或地区，使匹配无需标签中包含这些词。
- 绑定值（`SelectedValue`）而不是索引，使重新加载列表时选择不会改变。
- 按人们所读的标签排序列表；只有当列表是有序目录时，才按前缀筛选。
- 当选择会改变总额或日程时，在摘要中显示所选项，使其效果无需重新打开列表即可看到。

**避免**

- 对三到四个选项使用组合框。搜索框会增加一个步骤，而 `Select` 列表则能直接显示所有选项。
- 隐藏选项被禁用的原因。无法选择“MAUI”的人需要知道原因。
- 在人们于列表中输入时替换选项。列表会在查询下重建，因此高亮可能移动到一个并非用户选择的选项上。
- 在 `FilterPredicate` 中执行开销较大的工作，例如服务器请求。它会在每次按键时运行。请在视图模型中使用防抖的查询从服务器加载列表。
- 依赖占位文字说明哪个字段是必填的。应在字段上方放置可见的标签。

### 常见问题

**为什么字段显示的是标签而不是值？** `SelectedValue` 是存储的标识符，而字段显示的是人们阅读的文字。列表关闭时，字段显示 `SelectedText`。值保存在视图模型中。

**按下 Escape 时，已输入的查询会怎样？** 列表关闭，查询被丢弃，之前的值保持不变。字段会再次显示所选的标签。

**如何用键盘移动高亮或选取选项？** 按 `Down` 打开列表，再用 `Down` 和 `Up` 移动高亮，按 `Enter` 选取。高亮会从最后一个选项循环到第一个。禁用的选项会被跳过。

**能否从代码中打开列表？** 将 `IsOpen` 设为 `True`，或调用 `FocusInput()`，后者还会把焦点移入字段。字段获得焦点或用户输入时，列表会打开。

**聚焦字段会打开列表吗？** 会。获得焦点会打开列表，输入也会打开它。`Enter` 选取高亮的选项并关闭列表，`Tab` 则关闭列表并把焦点移走。

## API 参考

<!-- api: Combobox -->

<!-- api: SelectOption -->

## 无障碍

- `Label` 为字段及其列表命名。`Description` 是帮助文本，在名称和值之后朗读。
- 该字段以组合框的形式对外暴露。它的值是所选的标签，展开状态与列表保持一致。
- 人们用方向键移动高亮时，焦点保留在文本输入框中，因此输入与导航不会互相争抢。
- 清除按钮的名称为“清除选择”。选项下方的状态行是礼貌式实时区域，因此结果数量或空消息的变化会被播报，而焦点不会移动。
- 光标、选择、输入法组合与剪贴板快捷键都是原生 `TextBox` 的行为。
- 焦点环绘制在字段外侧 3 px 处。

### 键盘

| 按键 | 操作 |
|---|---|
| 输入文字 | 按标签、元信息和关键词筛选选项；列表关闭时将其打开 |
| `Down` / `Up` | 列表关闭时打开列表；否则把高亮移到下一个或上一个可用选项，并循环 |
| `Enter` | 选取高亮的选项并关闭列表；没有高亮时无任何作用 |
| `Escape` | 关闭列表并丢弃查询词；所选的值保持不变 |
| `Tab` | 关闭列表，并把焦点移到下一个控件 |
| 焦点进入字段 | 打开列表 |
| 鼠标点击选项 | 选取该选项并关闭列表 |

## 动效

- 列表以弹簧效果从上方展开。其高度随匹配结果的数量变化，因此查询变化时会增高或缩短。
- 选中的标签从下方升入字段，升起 0.35em，并从柔和的模糊中逐渐清晰。
- 有选中项时清除按钮弹出；按下时轻微缩小。
- 没有匹配项时，选项下方的状态行上移 4 px 并淡入。

| 变化 | 完整 | 减弱 | 关闭 |
|---|---|---|---|
| 列表打开 | 从上方以弹簧效果弹入 | 淡入 | 立即完成 |
| 结果数量变化 | 列表高度以平滑弹簧跟随变化 | 高度立即变化 | 立即完成 |
| 选中的标签出现 | 从下方升起 0.35em，从柔和模糊中清晰显现 | 淡入 | 立即完成 |
| 清除按钮出现 | 弹出 | 淡入 | 立即完成 |
| 空状态行出现 | 上移 4 px 并淡入 | 淡入 | 立即完成 |

## 相关组件

- 简短固定、无需输入的列表用 `Select`。
- 需要选择多个值时用 `MultiSelect`，经常切换的面向用 `ChipGroup`。
- 搜索的目的是浏览内容而非设置值时用 `ExpandingSearch`。
- 不是从列表中选择的自由文本用 `Input`。

| 组件 | 适用场景 |
|---|---|
| `Select` | 列表简短且固定，人们无需输入即可选择 |
| `MultiSelect` | 需要选择多个值，且每个值都以标记的形式保持可见 |
| `ChipGroup` | 少数面向经常切换，且每个选项都应保持可见 |
| `ExpandingSearch` | 搜索用于打开结果以供阅读或导航，而不是设置字段 |
| `Input` | 值是自由文本，而不是某个选项 |
| `TagInput` | 人们创建自己的标签，而不是选择已存在的选项 |
