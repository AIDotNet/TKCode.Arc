---
title: 设置命令面板
summary: 用一个搜索框找到任意设置并原地修改：开关翻转、选项轮换、链接打开，全部可用键盘完成。
source-hash: 1a7e720308b9
---

<Demo name="Inline" />

## 何时使用

`SettingsCommand` 把应用的设置放在一个搜索框之后。人们输入几个字母，例如“邮箱”或“深色”，就能当场修改设置：无需打开设置页，无需找到分区，也无需滚动到某一行。它可以与完整的设置页并列，作为访问同一组值的快捷路径，也可以单独放在通过快捷键打开的对话框中。面板保存这些值，并在其中一个值变化时调用应用，因此适合立即生效的设置。

- **数据分析或项目工具中通过 Ctrl+,（Mac 上为 ⌘,）打开的设置面板**：人们每天多次修改“主题”“紧凑表格”或“默认图表范围”。对话框中的面板省去了进入设置导航的过程。
- **团队收件箱中的通知偏好**：“提及”“产品更新”和“邮件摘要”是开关或三个具名的值。每项变更立即生效，底部栏会确认（“提及已关闭”），因此没有需要记得保存的步骤。
- **协作工具中的隐私与在线状态开关**：“显示在线状态”和“已读回执”对整个工作区生效。底部的提示会确认每次变更，人们无需打开对话框即可看到结果。
- **与相关设置放在一起的账户操作**：“修改邮箱”和“两步验证”会打开其他页面，“在所有设备上退出登录”则执行一条命令。每个操作都可以以键帽形式显示快捷键，因此一次搜索即可找到全部操作。
- **包含许多小开关的管理控制台**：在租户控制台中搜索“单点登录”，即可在“安全”“用户”和“账单”分组下的行中找到强制执行开关。保持列表短到便于浏览（参见“尺寸与性能”）。

## 何时不用

- **需要表单、校验或保存步骤的设置**：使用 `SettingsPage` 配合 `SettingsGroup` 和 `SettingsRow`，或由 `Input` 与 `Select` 构成的表单。面板会立即应用每个值，没有地方放置校验消息或保存按钮。
- **选项很多或需要自由输入的设置**，例如时区、国家或显示名称：长列表用 `Combobox` 或 `Select`，自由文本用 `Input`。面板中的行以短胶囊显示其选项，因此只适合少量取值。
- **日期与时间**，例如免打扰时段或报告开始日期：使用 `TimePicker` 或 `DatePicker`。面板的行无法显示日历或时钟。
- **不属于设置的应用命令**，例如“新建项目”或“导出此视图”：使用 `CommandPalette`。它搜索的是命令，而这个面板搜索的是人们配置的值。
- **搜索记录**，例如客户、发票或消息：使用 `SearchField` 或 `ExpandingSearch`，结果来自数据。此面板只搜索交给它的设置。

## 用法

在面板内编写设置。`SettingsCommandToggle` 为开或关，`SettingsCommandChoice` 依次切换其 `Options`，`SettingsCommandAction` 执行一条命令。`Group` 把设置列在某个标题下，`Keywords` 添加其他可以搜到该设置的词。焦点留在搜索框中：上、下方向键移动高亮，Enter 作用于高亮的行，左、右方向键切换选项。底部栏会确认每次变更。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:SettingsCommand MaxWidth="560">
        <arc:SettingsCommandChoice Group="外观" Header="主题" Options="浅色, 深色, 跟随系统" Keywords="深色 浅色" />
        <arc:SettingsCommandToggle Group="外观" Header="紧凑模式" Description="行距更紧凑，间距更小。" />
        <arc:SettingsCommandAction Group="账户" Header="修改邮箱" IsNavigation="True" />
    </arc:SettingsCommand>
</UserControl>
```

`SettingsCommand` 派生自 Avalonia 的 `TemplatedControl`，因此可以使用常见的布局属性（`Width`、`MaxWidth`、`Margin`）和样式设置。它的设置是一个普通集合：`SettingsCommandItem` 是三种设置的基类，这些项就是面板的内容，因此既可以在 XAML 中编写，也可以在代码中添加。面板默认带有外框，拥有自己的表面、边框、圆角与浮动阴影。当外层表面已经提供这些效果时，将 `IsFramed` 设为 `False`。

### 结构

1. **搜索行**（`Query`）：56 px 高，左侧是 18 px 的搜索图标（使用弱化文字颜色），中间是无边框文本框，右侧是 Esc 键帽。行下方有分隔线。
2. **结果列表**（`ResultsMaxHeight`）：默认 360 px，内边距 8 px。超过该高度时列表滚动，其上下边缘会以 20 px 的渐隐提示还有更多内容。
3. **分组标题**（`Group`）：使用弱化文字颜色的超小文字，内边距为 12、10、12 与 4 px。未设分组的设置显示在 `Labels.Ungrouped` 之下，且只有在其他设置有分组时才会显示。
4. **行**（`Header`、`Description`）：48 px 高，圆角 12 px。标题为中等字重的小号正文；说明为一行次要文字颜色的超小文字，超出部分以省略号截断。
5. **图标块**（`Icon`）：30 px 的块，圆角 9 px，图标 16 px。图标使用 `ArcIcons` 中的成员。
6. **行控件**：`SettingsCommandToggle` 为开关；`SettingsCommandChoice` 为 28 px 的胶囊，两侧各有 24 px 箭头，中间显示当前值；`SettingsCommandAction` 为键帽或右箭头。
7. **活动行高亮**：一个弱化表面，圆角 12 px，在各行之间滑动。
8. **底部栏**：40 px 高，上方有分隔线。左侧为按键提示（上下键用于“导航”，Enter 用于“切换”“更改”或“打开”，取决于当前行；Esc 用于“关闭”）。变化提示位于右侧。

### 搜索

查询按空格拆分。每个词都必须出现在标题、说明、分组或关键词中，不区分大小写，且可以匹配词的一部分。结果按标题的匹配方式排序：

1. 标题以整个查询开头。
2. 标题中某个词以查询开头。
3. 标题包含查询。
4. 只有说明、分组或关键词匹配。

同一等级的设置保持声明顺序，分组则按其最佳一行排序。标题中相匹配的部分保持前景色，标题的其余部分改为次要色，使人们能看出某行为何匹配。没有匹配时，面板会显示 `Labels.NoResults` 与 `Labels.NoResultsHint`，建议换一个词。空查询会按声明顺序列出所有设置，因此请按人们期望看到的顺序编写设置。

`Query` 为双向绑定，因此应用可以预先填入搜索词（例如来自深层链接），或保留上一次的查询。`Clear()` 会清空查询并重新列出所有设置，`FocusSearch()` 会把键盘焦点移到搜索框。

### 开关、选项与操作

按设置变更时的行为选择类型：

- **`SettingsCommandToggle`** 用于立即生效、只有开和关两种状态的设置，例如“紧凑模式”或“提及”。按 Enter 或点击会翻转它，提示会显示“紧凑模式已开启”或“紧凑模式已关闭”。需要第二步才能确认的开关不应放在这里。
- **`SettingsCommandChoice`** 用于三到五个具名的值，例如“浅色、深色、跟随系统”或“关闭、每日、每周”。Enter 移到下一个选项，并从最后一个回到第一个；而值两侧的箭头则逐个切换且不循环。请排列选项顺序，使常见的下一个选择紧跟默认值，因为 Enter 每次只移动一步。
- **`SettingsCommandAction`** 用于命令或链接：“修改邮箱”“下载你的数据”“在所有设备上退出登录”。按 Enter 或点击会执行 `Command`，然后引发 `ItemInvoked`。对于会打开其他页面的操作，请将 `IsNavigation` 设为 `True`，使该行显示箭头，人们便会预期进入新页面。

对于当前无法修改的设置，例如管理员已锁定的设置，请将 `IsEnabled` 设为 `False`。该行会变暗，键盘会跳过它，但该行仍保留在列表中，使人们知道该设置存在以及为何不可用。请用 `Description` 说明原因（“已由管理员关闭。”）。

### 绑定到视图模型

面板的值是样式属性。`IsChecked` 与 `SelectedOption` 为双向绑定：在面板中的修改会更新视图模型，应用其他位置的修改则会移动该行。下例使用 CommunityToolkit.Mvvm（`ObservableObject` 基类、`[ObservableProperty]` 与 `[RelayCommand]` 生成器）。状态由应用持有，面板只负责编辑它。

```csharp
public partial class WorkspaceSettingsViewModel : ObservableObject
{
    private readonly Action<string> _openPage;

    [ObservableProperty]
    private string? _theme = "跟随系统";

    [ObservableProperty]
    private bool _compactMode;

    [ObservableProperty]
    private bool _reduceMotion = true;

    [ObservableProperty]
    private string? _emailDigests = "每周";

    public WorkspaceSettingsViewModel(Action<string> openPage) => _openPage = openPage;

    [RelayCommand]
    private void ChangeEmail() => _openPage("account/email");
}
```

```xml
<arc:SettingsCommand>
    <arc:SettingsCommandChoice Group="外观" Header="主题"
                               Options="浅色, 深色, 跟随系统"
                               SelectedOption="{Binding Theme}" />
    <arc:SettingsCommandToggle Group="外观" Header="紧凑模式"
                               IsChecked="{Binding CompactMode}" />
    <arc:SettingsCommandToggle Group="外观" Header="减少动效"
                               IsChecked="{Binding ReduceMotion}" />
    <arc:SettingsCommandChoice Group="通知" Header="邮件摘要"
                               Options="关闭, 每日, 每周"
                               SelectedOption="{Binding EmailDigests}" />
    <arc:SettingsCommandAction Group="账户" Header="修改邮箱" IsNavigation="True"
                               Command="{Binding ChangeEmailCommand}" />
</arc:SettingsCommand>
```

双向的有 `IsChecked`、`SelectedOption` 与 `Query`。单向的有 `Items`、`Labels` 与 `ResultsMaxHeight`，由应用设置，面板读取。只读的是 `ActiveItem`，即高亮的设置；若没有显示任何设置则为 `null`。对于绑定无法表达的副作用（例如保存到服务器或记录审计日志），请使用事件。`SettingChanged` 只针对在面板中的修改而触发，因此当应用自身修改值时，处理程序中的保存不会再次执行。`ItemInvoked` 在操作的 `Command` 执行后触发。两个事件都是路由事件，因此处理程序既可以挂在面板上，也可以挂在其父元素上。

<Demo name="Bound" />

```csharp
palette.SettingChanged += (_, e) => _settings.SaveAsync(e.Item.Header, e.Value);
palette.ItemInvoked += (_, e) => _telemetry.Track("settings.action", e.Item.Header);
```

### 焦点与关闭

放在 `Dialog` 中的面板需要三个条件：将 `IsFramed` 设为 `False`，使面板不再绘制第二层外框；设置一个与对话框内边距相同大小的负外边距，使搜索行和底部栏延伸到对话框的边缘；以及一条关闭规则。示例为面板设置 -24 的外边距，以抵消对话框 24 px 的主体内边距。下面的代码会关闭承载面板的 `Dialog`。

查询为空时按 Escape 会引发 `CloseRequested`，查询非空时按 Escape 会先清空查询。在 `CloseRequested` 处理程序中关闭对话框，并将 `Handled` 设为 `True`，使对话框不会再次处理同一次按键。

对话框打开时会把焦点移到其第一个控件，这会使键盘离开搜索框。对话框打开后，在 UI 线程上投递 `FocusSearch()`，并在对话框打开时清空查询，使每次会话都从完整列表开始。

<Demo name="InDialog" />

```csharp
private Dialog SettingsDialog = null!; // 演示中带有 x:Name="SettingsDialog" 的 Dialog

private void OnCloseRequested(object? sender, RoutedEventArgs e)
{
    e.Handled = true;
    SettingsDialog.Close();
}
```

示例还通过 Ctrl+,（Mac 上为 ⌘,）打开对话框，这是设置的常用快捷键。面板只显示快捷键，因此应用需要自行注册按键手势；见下一小节。

### 快捷键与操作

`Shortcut` 以键帽形式显示操作的组合键。用 `mod` 表示命令键（Mac 上为 ⌘，其他平台为 Ctrl），并可与 `shift` 组合，例如 `mod+shift+e`。面板只负责显示快捷键，实际按键由应用处理，例如在窗口级的按键处理程序中，因此即使面板关闭时快捷键依然有效。`IsNavigation` 会为打开其他页面的操作添加箭头。

```xml
<arc:SettingsCommand>
    <arc:SettingsCommandAction Header="下载你的数据" Shortcut="mod+shift+e" />
    <arc:SettingsCommandAction Header="两步验证" IsNavigation="True" />
    <arc:SettingsCommandToggle Header="已读回执" Description="已由管理员关闭。" IsEnabled="False" />
</arc:SettingsCommand>
```

### 文本

内置文字来自 `Labels`：固定文字使用字符串，带有值的文字（例如无结果消息）使用函数。替换整个对象即可翻译，并保持每个函数的参数顺序不变。

```csharp
palette.Labels = new SettingsCommandLabels
{
    Placeholder = "Instellingen zoeken",
    NoResults = query => $"Geen instellingen voor “{query}”",
    TurnedOn = header => $"{header} aan",
    TurnedOff = header => $"{header} uit",
};
```

### 尺寸与性能

面板为每个设置创建一行，并在该项的整个生命周期中保留。筛选只改变显示哪些行以及它们的顺序。几百个以内的设置无需虚拟化；更长的目录请移到设置页。

通过 `Items` 集合在代码中添加或删除项，列表会立即更新。

### 示例：团队收件箱中的通知设置

团队收件箱把通知偏好与其他设置放在同一个面板中。此处的查询是 "email"，用于说明排序方式：“Email digests”以查询开头，因此排名第一；“Change email”的标题中有一个词以它开头；“Product updates”只通过关键词匹配，因此排在最后。每个分组按其最佳一行排序，这使 Notifications 排在 Account 之前。标题中的匹配文字保持前景色，底部栏显示适用于高亮选项的按键。面板宽 560 px，适合放在对话框或设置列中，而不会拉伸各行。

![设置面板搜索 email，结果跨两个分组排序](images/EmailSettings.png)

```xml
<StackPanel Width="560">
    <arc:SettingsCommand Query="email"
                         ResultsMaxHeight="320">
        <arc:SettingsCommandChoice Group="Notifications"
                                   Header="Email digests"
                                   Icon="{x:Static arc:ArcIcons.Inbox}"
                                   Description="A summary of activity in your projects."
                                   Keywords="summary newsletter"
                                   Options="Off, Daily, Weekly"
                                   SelectedOption="Weekly" />
        <arc:SettingsCommandToggle Group="Notifications"
                                   Header="Mentions"
                                   Icon="{x:Static arc:ArcIcons.AtSign}"
                                   Description="When someone @mentions you in a comment."
                                   IsChecked="True" />
        <arc:SettingsCommandToggle Group="Notifications"
                                   Header="Product updates"
                                   Icon="{x:Static arc:ArcIcons.Bell}"
                                   Description="Occasional news about new features."
                                   Keywords="news announcements" />
        <arc:SettingsCommandAction Group="Account"
                                   Header="Change email"
                                   Icon="{x:Static arc:ArcIcons.Mail}"
                                   Description="noor@larkspur.co"
                                   Keywords="address login"
                                   IsNavigation="True" />
        <arc:SettingsCommandAction Group="Account"
                                   Header="Download your data"
                                   Icon="{x:Static arc:ArcIcons.Download}"
                                   Description="A copy of your files, comments and settings."
                                   Keywords="export archive backup"
                                   Shortcut="mod+shift+e" />
    </arc:SettingsCommand>
</StackPanel>
```

### 文案规范

为每个标题使用设置的名称，句子式大小写，使它在行中、搜索中和提示中读起来一致。说明保持一行简短文字，因为行会以省略号截断。用设置页上人们使用的词命名选项，并在变更后的提示中沿用相同的名称。

| 不要写 | 改为 |
|---|---|
| 为提及开启通知 | 提及 |
| 每天发送一封摘要邮件 | 邮件摘要（选项：每日） |
| 切换紧凑视图模式 | 紧凑模式 |
| 更改你的邮箱地址 | 修改邮箱 |
| 退出所有设备的登录 | 在所有设备上退出登录 |
| 管理员已停用此设置 | 已由管理员关闭。 |

用关键词添加同义词，以及人们会搜索的词，例如为“主题”添加 “深色 浅色”，为“两步验证”添加 “2fa security”。不要在关键词中重复标题，也不要添加指向其他设置的关键词。

### 最佳实践

**推荐**

- 立即应用变更，并在底部栏中确认。提示是面板中唯一的反馈，因此它必须说明设置名称及其新值。
- 把值绑定到应用的状态，并在事件处理程序中保存，使面板与应用其余部分始终显示相同的值。
- 选项保持为少量具名的值；更长的选项列表移到带有合适控件的页面中。
- 为每个平台注册打开面板的快捷键（示例中为 `mod+,`），并在打开面板的按钮旁显示该快捷键，使人们在已经使用的界面上学会它。
- 对于锁定的设置，使用 `IsEnabled="False"` 并在说明中给出原因，使人们知道它并非缺失。

**避免**

- 需要保存步骤或校验的设置。面板的修改会立即生效，被拒绝的值已经生效了；应把这类设置交给 `SettingsPage`。
- 删除数据或让人退出登录的操作却没有确认。应从操作的命令打开 `Dialog`，使搜索不会绕过这一保护。
- 说明相同的长列表设置。搜索无法区分它们，各行看起来也很相似。
- 为每种状态更改设置的标题，例如“开启紧凑模式”和“关闭紧凑模式”。行中已经表示了值，标题应保持不变。
- 只把值保存在面板中。`SettingChanged` 只为用户的修改触发，因此应用必须自行保存状态。

### 常见问题

**为什么第一次按 Escape 不是关闭而是清空查询？** 非空查询时，Escape 会先清空查询，因此人们可以在不丢失面板的情况下取消搜索。查询为空时，Escape 才会引发 `CloseRequested`。承载面板的对话框应只在该处理程序中关闭。

**为什么在最后一行按下方向键时高亮会停止？** 高亮不会循环：它停在第一个和最后一个可用的行上。已禁用的行在两个方向上都会被跳过，因此高亮总是落在可以修改的设置上。

**如何响应应用自身修改的值？** 绑定该属性（例如 `IsChecked`），该行会跟随变化。`SettingChanged` 不会为此触发，因此对于必须触发副作用的变化，请在视图模型上使用 `PropertyChanged` 处理程序。

## API 参考

<!-- api: SettingsCommand -->
<!-- api: SettingsCommandToggle -->
<!-- api: SettingsCommandChoice -->
<!-- api: SettingsCommandAction -->

### SettingsCommandItem

三种设置的基类。每个属性都是样式属性，因此面板会跟随变化。

| 属性 | 类型 | 说明 |
|---|---|---|
| `Header` | `string?` | 设置的名称，显示在行中，并优先搜索。 |
| `Description` | `string?` | 标题下方的一行文字，并截断为单行；同样可搜索。 |
| `Icon` | `Geometry?` | 24 × 24 的描边图标，例如 `ArcIcons` 的成员，显示在标题前的图标块中。 |
| `Group` | `string?` | 该行所列的分组，例如“外观”；同样可搜索。 |
| `Keywords` | `string?` | 其他可用来找到该设置的词，以空格或逗号分隔。 |
| `IsEnabled` | `bool` | 设置是否可以修改。禁用的行会变暗，并被键盘跳过。 |

### SettingsCommandLabels

| 属性 | 类型 | 说明 |
|---|---|---|
| `Placeholder` | `string` | 搜索框的占位文字：“搜索设置”。 |
| `SearchName` | `string` | 搜索框的无障碍名称：“搜索设置”。 |
| `ResultsName` | `string` | 结果列表的无障碍名称：“设置”。 |
| `NoResults` | `Func<string, string>` | 没有匹配时的消息：“没有匹配“theme”的设置”。 |
| `NoResultsHint` | `string` | 其下方的提示：“换一个词再试，例如 theme 或 email。” |
| `Navigate`, `Toggle`, `Change`, `Open`, `Close` | `string` | 底部栏中按键旁的提示。 |
| `EscapeKey` | `string` | Escape 键帽的文字：“Esc”。 |
| `TurnedOn`, `TurnedOff` | `Func<string, string>` | 开关变化后的提示：“紧凑模式已开启”。 |
| `ChangedTo` | `Func<string, string, string>` | 选项变化后的提示：“主题：深色”。 |
| `Ungrouped` | `string` | 未设分组的设置所用的分组标题，在其他设置有分组时显示：“设置”。 |
| `On`, `Off` | `string` | 辅助技术朗读开关状态时使用的文字。 |
| `RowState` | `Func<string, string, string>` | 行的标题与状态：“深色模式，开启”。 |
| `PreviousOption`, `NextOption` | `string` | 选项值两侧箭头的名称。 |

### 事件参数

| 类型 | 属性 | 说明 |
|---|---|---|
| `SettingsCommandChangedEventArgs` | `Item` | 发生变化的设置。 |
| `SettingsCommandChangedEventArgs` | `Value` | 新值：开关为 `bool`，选项为所选的选项。 |
| `SettingsCommandItemEventArgs` | `Item` | 被调用的操作。 |

## 无障碍

- 搜索框名为“搜索设置”。其帮助文字跟随高亮的行，例如“紧凑模式，关闭”或“主题，深色”。
- 结果是名为“设置”的列表，每一行都是以其标题命名的列表项。
- 键盘焦点始终留在搜索框中。上、下方向键移动高亮并跳过已禁用的行，Page Up 与 Page Down 各移动五行。列表不循环。
- Enter 会切换开关、把选项移到下一个值，或执行操作。左、右方向键逐个切换选项且不循环；其他情况下，它们移动插入符。
- Escape 会清空查询。查询为空时，它会引发 `CloseRequested`。
- 变化提示是礼貌的实时区域，因此屏幕阅读器会朗读“紧凑模式已开启”。
- 开关、选项旁的箭头和键帽都无法获得焦点。点击任一行与按 Enter 效果相同，键盘焦点仍留在搜索框中。

### 键盘

| 按键 | 操作 |
|---|---|
| `Down` / `Up` | 移动高亮到下一个或上一个可用的设置；不循环 |
| `PageDown` / `PageUp` | 高亮移动五个设置 |
| `Enter` | 切换开关、把选项移到下一个值（最后一个之后是第一个），或执行操作 |
| `Right` / `Left` | 把选项切换到下一个或上一个值，不循环；在其他行上则移动插入符 |
| `Escape` | 非空查询时清空查询；查询为空时引发 `CloseRequested` |
| 输入文字 | 更改查询，列表随输入即时筛选 |

点击一行与按 Enter 效果相同。点击开关或选项箭头会直接修改该设置。

## 动效

- 高亮在各行之间以灵敏的弹簧滑动。结果变化时，高亮立即移动。
- 列表平滑滚动，使高亮行及其分组标题保持可见。
- 选项的值按变化方向上滚或下滚，开关的滑块会滑动。
- 变化提示伴随柔和的模糊上升出现，并在 2.5 秒后淡出。
- 减弱动效时，提示与值淡入淡出，高亮立即移动。关闭动效时，每次变化都立即显示。

| 变化 | 完整 | 减弱 | 关闭 |
|---|---|---|---|
| 高亮在行之间移动 | 以灵敏的弹簧滑动 | 立即移动 | 立即移动 |
| 选项的值变化 | 按变化方向上滚或下滚 | 淡入淡出 | 立即显示 |
| 变化提示出现 | 伴随柔和模糊上升，2.5 秒后淡出 | 淡入淡出 | 立即显示 |

## 相关组件

- `SettingsPage`：带有分区导航和保存条的完整设置页。
- `SecuritySettings`、`BillingOverview`、`UsageBilling` 与 `CancelFlow`，即其他设置区块。
- `Switch`：开关行中的控件；`Dialog`：通过快捷键承载面板的对话框。
- `SearchField` 与 `Select`：用于表单中单独的搜索或选择。

| 组件 | 适用场景 |
|---|---|
| `SettingsPage` | 设置分为多个分区，并通过保存条一起保存 |
| `CommandPalette` | 人们运行应用命令，而非修改已配置的值 |
| `SearchField` | 文字用于搜索记录或内容，而非设置 |
| `Switch` | 单个设置位于表单或无需搜索的行中 |
| `Select` / `Combobox` | 表单需要从长列表中选择一个值 |
