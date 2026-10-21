---
title: 原位编辑
summary: 原地重命名：文字变为输入框，位置不变。
source-hash: 5cd94f50bd18
---

<Demo name="Rename" />

## 何时使用

`InlineEdit` 将一个值显示为普通文字，当使用者选择编辑时，它会在原来的位置变为输入框。它适用于在页面上多次出现、且很少变化的名称和标题：页眉中的项目名称、仪表板标题、CRM 记录中客户的显示名称。开始编辑时，文字周围的内容不会移动，因此页面布局保持不变，并且该值会自动保存，无需外围的表单。

- **页眉中的项目与工作区名称**：项目页顶部的“Atlas 改版”。人们每天都会阅读它，偶尔才重命名，因此它应当看起来像标题，并在激活时变为可编辑。
- **分析应用中的报表与仪表板标题**：仪表板上的“按地区划分的第三季度收入”。使用者确认标题时即保存；保存失败时，标题会还原为上次保存的内容，并提供重试链接。
- **CRM 或账单页面中的联系人与账户名称**：客户的公司名称或联系人的显示名称，其中编辑只是一次更正，而不是一次表单提交。
- **标题下的描述与简短备注**：一行的项目摘要，或使用 `IsMultiline` 的多行描述，文字可以换行，按 Enter 仍会保存。
- **运维主控台中的事件与工单标题**：值班工程师原地修正标题，而时间线保持原位。

## 何时不用

- **需要一起保存的多个字段**：个人资料或账单地址是一个表单。应为每个字段使用 `Input` 或 `Textarea`，并配一个保存按钮，使人们在保存前能够检查全部内容。
- **必须始终看起来可编辑的字段**：搜索框、正在录入的订单中的发票号，或任何需要人们填写的值。使用 `Input`，它始终显示外框和标签。
- **使用者撰写的长正文**：发布说明、客服回复或政策文本。使用 `Textarea`，它在使用者输入时保持较大的文字区域和可见的外框。
- **需要解析的数字与金额**：价格、数量或百分比。使用 `NumberField` 或 `MoneyInput`，它们会解析、格式化并步进该值。
- **从固定列表中选取的值**：状态、负责人或货币。使用 `Select` 或 `Combobox`，它们会显示选项及其状态。

## 用法

该值以文字显示。激活它后，文字会变为同样尺寸的输入框。编辑提交时保存该值。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:InlineEdit Label="项目名称"
                    Value="Atlas redesign"
                    HeadingLevel="2" />
</UserControl>
```

`InlineEdit` 派生自 Avalonia 的 `TemplatedControl`。它的静态文字是一个按钮（`Enter` 和 `Space` 开始编辑，因此可以通过键盘触达该文字），其编辑器是一个 `TextBox`，因此光标、选择、输入法组字和剪贴板快捷键的表现与任何文本框相同。控件在此之上加入保存流程：新文字的乐观显示、保存中指示、失败时的回滚以及校验消息。

### 结构

1. **静态文字**：按钮中的值。`Title` 变体使用超大字号（22 px）与中等字重，行高 28 px，上下内边距 3 px，左右内边距 8 px。`Body` 变体使用 14 px 的次要文字颜色，行高 21 px，上下内边距 5 px，左右内边距 8 px。该框的前导边距为 -9 px，使文字与周围内容对齐。
2. **外框**：带有控件圆角的框。静止时保持透明；静止文字的悬停会显示弱化的表面色；编辑时变为带前景色边框的表面色框。
3. **焦点环**：键盘焦点位于静态文字时，围绕它显示 2 px 的焦点环，圆角半径 22 px。编辑时焦点环为 3 px，并位于外框之内。
4. **铅笔图标**：文字末尾 14 px 的弱化图标。使用精确指针时在悬停时出现，键盘焦点时也会出现。它是文字可以编辑的视觉提示；触控输入则直接进入编辑器。
5. **编辑器**：`TextBox`，其字体度量与静态文字相同，因此打开字段时文字不会偏移。它的内边距在末端增加 24 px，最小宽度为 5 em（`Title` 为 110 px，`Body` 为 70 px），因此下一个字符无需等待框体变宽。
6. **操作区**：末端的保留区域。编辑时显示保存按钮（28 px 圆形主按钮，带对勾图标）与取消按钮（28 px 描边圆形按钮，带关闭图标）。保存时显示 14 px 的加载指示；保存成功后显示自行绘制的对勾；保存失败后显示危险图标。单行保留 66 px 给操作区，多行保留 38 px，因此操作区出现时文字不会重新换行。
7. **消息行**：字段下方 12 px 的文案。校验消息使用危险色。保存失败时使用次要文字颜色，并以 `RetryText` 链接结尾，点选后会以失败的文字重新打开字段。
8. **状态**：一个隐藏的礼貌区域，用于播报保存进度与结果。

### 变体与多行

`Variant` 设置视觉权重。`Title` 是名称与标题的默认值；`Body` 适合描述文字。设置 `IsMultiline` 后，文字会换行，字段高度随之增长。按 Enter 仍会保存，Shift+Enter 则添加换行。

<Demo name="Multiline" />

```xml
<arc:InlineEdit Label="描述"
                Variant="Body"
                IsMultiline="True"
                PlaceholderText="添加描述"
                Value="{Binding Description}" />
```

对于为某物命名、并被读作标题的文字，使用 `Title`；对于描述该事物的文字，使用 `Body`。在同一页面上，每一类文字保持一种变体，使名称对齐，描述读起来像一组。只有人们以段落形式书写的文字才使用 `IsMultiline`；单行名称换行，说明它对所在位置来说太长了。

### 绑定

`Value` 是双向的。当使用者未在编辑时，来自外部的新值会替换文字。

```xml
<arc:InlineEdit Label="项目名称"
                Value="{Binding ProjectName}"
                HeadingLevel="2" />
```

### 绑定到视图模型

在 MVVM 应用中，视图模型拥有已保存的值与规则，控件负责编辑。`Value` 是双向绑定。`SaveHandler` 与 `Validator` 是委托，因此需要在视图的代码后置中，用视图模型的方法为它们赋值。委托无法在 XAML 中设置。

```csharp
// 应用自己的服务；它不属于控件库的一部分。
public interface IProjectService
{
    Task RenameAsync(ProjectId id, string name);
}

public partial class ProjectHeaderViewModel : ObservableObject
{
    private readonly IProjectService _projects;
    private readonly ProjectId _projectId;

    public ProjectHeaderViewModel(IProjectService projects, ProjectId projectId)
    {
        _projects = projects;
        _projectId = projectId;
    }

    [ObservableProperty]
    private string _projectName = string.Empty;

    [ObservableProperty]
    private string _description = string.Empty;

    public string? ValidateName(string value)
        => value.Length switch
        {
            0 => "项目需要一个名称。",
            > 80 => "请使用 80 个字符以内的名称。",
            _ => null
        };

    // 此任务完成时，控件会把 Value 设为已保存的文字，因此绑定会更新该属性。
    public Task SaveNameAsync(string name) => _projects.RenameAsync(_projectId, name);
}
```

```csharp
// 在视图的代码后置中，于 InitializeComponent 之后且设置 DataContext 之后。
ProjectTitle.Validator = viewModel.ValidateName;
ProjectTitle.SaveHandler = viewModel.SaveNameAsync;
```

```xml
<StackPanel Spacing="8">
    <arc:InlineEdit x:Name="ProjectTitle"
                    Label="项目名称"
                    Value="{Binding ProjectName, Mode=TwoWay}"
                    HeadingLevel="1" />
    <arc:InlineEdit Label="描述"
                    Variant="Body"
                    IsMultiline="True"
                    PlaceholderText="添加描述"
                    Value="{Binding Description, Mode=TwoWay}" />
</StackPanel>
```

- `Value` 是双向的，因此保存成功时，已保存的名称会传到视图模型。在保存成功之前，控件不会写入该值，因此保存失败时，视图模型的属性保持不变。
- 当使用者正在编辑或保存正在进行时，来自外部的值不会替换文字，因此编辑期间应避免把刷新的数据推送到视图模型。
- 代码后置中的名称（`x:Name`）属于视图。处理程序应放在视图或视图模型中；控件只负责调用它们。

### 保存与校验

`SaveHandler` 持久化已提交的值。它在字段显示保存状态时运行。出错的任务会使文字回滚，并显示重试链接。`Validator` 在草稿无法保存时返回消息；该消息显示在字段下方。

```csharp
title.Validator = value => value.Length == 0 ? "项目需要一个名称。" : null;

title.SaveHandler = async next =>
{
    await projects.RenameAsync(projectId, next);
};
```

处理 `TextCommitted`、`Saved` 和 `SaveFailed`，以对结果做出反应。

```csharp
title.TextCommitted += (_, e) => log.Text = $"已提交“{e.NewValue}”（原为“{e.OldValue}”）";
title.Saved += (_, e) => log.Text = $"已保存“{e.NewValue}”";
title.SaveFailed += (_, e) => log.Text = $"保存失败：{e.Error.Message}";
```

保存流程分为四个步骤，控件通过 `Phase`（`Idle`、`Saving`、`Saved`、`Failed`）公开每个步骤：

1. **提交**：文字会被修剪（单行还会折叠内部的空白）、校验，并且若与当前显示的值不同，则发送给 `SaveHandler`。`TextCommitted` 会携带旧值与新值一并引发。不改变文字的提交会直接关闭字段，不保存，也不引发事件。
2. **保存中**：新文字立即以半透明显示，并带有加载指示。`Value` 尚未改变。在保存结束之前，字段无法再次打开。
3. **已保存**：`Value` 变为新文字，引发 `Saved`，并绘制对勾。约 1.8 秒后，阶段回到 `Idle`。若没有 `SaveHandler`，值会立即保存，并随之引发相同的事件。
4. **失败**：显示的文字回到上次保存的值，引发 `SaveFailed`（携带尝试保存的文字与异常），外框会短暂闪现危险色。消息行使用 `FailedMessageFormat` 说明原因，并提供 `RetryText`。选择它会以失败的文字重新打开字段，光标位于末尾。

`ErrorMessage` 在校验消息生效期间保存该消息。校验失败时，若提交来自 `Enter` 或保存按钮，字段会保持打开并保留使用者的文字，进入无效状态，并把焦点留在字段中。当文字变化并再次通过校验时，消息会被清除。

```csharp
// 服务器拥有最终决定权：它的错误会显示为校验消息，下一次编辑时清除。
title.SaveHandler = async next =>
{
    var result = await projects.RenameAsync(projectId, next);
    if (!result.IsSuccess)
    {
        throw new InvalidOperationException(result.Error);
    }
};
```

### 空值

在有值之前，占位文字会保持该行。

<Demo name="Empty" />

`Value` 为空时，`PlaceholderText` 以弱化颜色显示，并且字段与有值时同样高，因此添加值时布局不会跳动。占位文字应说明要添加什么（“添加副标题”），而不是通用的提示。

### 保存与离开字段

Enter 保存，Escape 取消，离开字段则保存。无论使用者以哪种方式离开，规则都相同：

- **Enter** 提交。在多行字段中，Shift+Enter 改为插入换行。
- **Escape** 取消：字段关闭，已保存的文字恢复，并引发 `EditCanceled`。
- **Tab 或点击字段之外** 会提交，与 Enter 相同。焦点移到保存或取消按钮不会提交，因为这些按钮属于控件。
- **切换到其他窗口** 不会提交。字段保持打开，待使用者返回并离开字段时再提交。
- **校验消息** 即使提交来自失去焦点，也会让字段保持打开，使使用者在字段关闭前看到消息。

操作区中的保存按钮与取消按钮，其结果与 Enter 和 Escape 相同。代码可以用 `BeginEdit`、`CommitEdit` 与 `CancelEdit` 完成同样的操作。`BeginEdit(caret)` 会在指定位置打开字段并放置光标；`-1`（默认值）则选中全部文字，便于重新输入。

### 示例：项目页面页眉

一个分析工作区的项目页面。项目名称是 `Title` 标题，描述是多行的 `Body` 文字，第三行是副标题，它仍为空，并显示其占位文字。文字看起来就是页面自身的内容，而不像表单，因此在有人选择编辑之前，页眉读起来就是标题。描述下方的元数据行使用普通文字，因为此处的所有者与日期都是只读的。页眉在两种状态下保持相同的布局：外框与文字共享相同的度量，因此标题变为字段时不会移动。

![项目页眉，包含内联标题、多行描述以及空副标题的占位文字](images/ProjectHeader.png)

```xml
<StackPanel Width="620" Spacing="8">
    <arc:InlineEdit Label="Project name"
                    Value="Atlas redesign"
                    HeadingLevel="1" />
    <arc:InlineEdit Label="Subtitle"
                    Variant="Body"
                    PlaceholderText="Add a subtitle" />
    <arc:InlineEdit Label="Description"
                    Variant="Body"
                    IsMultiline="True"
                    PlaceholderText="Add a description"
                    Value="Rebuild the analytics home with clearer status, faster search and one place for every report." />
    <TextBlock Text="Owner Ada Lovelace · Created 3 Mar 2026 · Updated 2 hours ago"
               FontSize="12"
               Margin="0,8,0,0"
               Foreground="{arc:ArcDesignTokenResource TextMuted}" />
</StackPanel>
```

### 文案规范

- **标签**：字段的名称，使用句首大写（“项目名称”“报表标题”）。它是无障碍名称，并会出现在保存播报中（“正在保存项目名称”）。
- **占位文字**：说明要添加什么内容的指令（“添加副标题”“添加描述”）。不要使用“点击编辑”，因为文字本身已经表明可以修改。
- **校验消息**：说明哪里有问题以及允许什么，用与字段一致的术语（“项目需要一个名称。”“请使用 80 个字符以内的名称。”）。
- **保存失败语句**：默认格式为“无法保存“{0}”，已恢复为上次保存的{1}。”。若默认文案读起来通顺，请保留它；只有需要更精确地指明对象时，才修改 `FailedMessageFormat`。
- **重试文字**：一个动词（默认为“重试”）。保持简短，因为它位于消息行的末端。

| 不要写 | 改为 |
|---|---|
| Click to edit | 添加描述 |
| Name is required!!! | 项目需要一个名称。 |
| Error saving | 无法保存“Atlas”，已恢复为上次保存的项目名称。 |
| Enter text here | 添加副标题 |

### 最佳实践

**推荐**

- 名称与标题使用 `Title`，描述使用 `Body`，并且在同一页面上每类文字保持一种变体，使页面按一致的顺序阅读。
- 为页面或区块的标题设置 `HeadingLevel`，使屏幕阅读器用户能按标题快速跳转。对于不是标题的文字，保持 `0`。
- 用 `Validator` 在客户端校验规则；服务器的拒绝则通过失败的 `SaveHandler` 显示，并附上清晰的消息。在字段显示错误时，控件会保留使用者的文字。
- 为每次编辑设置独立的 `Label`，使播报能指明被保存的对象。

**避免**

- 将它用于表单。需要一起保存的多个字段应使用 `Input` 或 `Textarea`，使人们能在保存前检查每个值，并且取消会放弃全部内容。
- 没有 `SaveHandler` 时仍然保存。没有它，值会立即保存，且没有错误路径；只要变更要发送到服务器，就添加处理程序，使失败能够回滚，并让使用者知情。
- 页眉中换行或截断的长单行名称。标题应保持简短，或对以段落形式书写的文字使用 `IsMultiline`。
- 在保存期间从代码打开字段。控件在保存运行时拒绝打开，因此该调用不会有任何作用；打开前应先检查 `Phase`。

### 常见问题

**为什么我的新名称变回了旧名称？** 保存失败了。`SaveHandler` 抛出了异常，文字回滚到上次保存的值，消息行提供“重试”。处理 `SaveFailed`，记录 `e.Error` 并告知使用者出了什么问题。

**为什么按下 Enter 后字段仍保持打开？** `Validator` 返回了一条消息。字段保留使用者的文字，将自己标记为无效，并显示该消息。当文字变化并通过校验时，消息会被清除。带有校验消息的失去焦点同样会让字段保持打开。

**如何从代码打开字段或提交它？** 调用 `BeginEdit()` 可以打开字段并选中全部文字，或调用 `BeginEdit(caret)` 将光标放在指定位置。调用 `CommitEdit()` 保存，调用 `CancelEdit()` 放弃。先检查 `IsEditing`，因为字段未打开时这些调用不会有任何作用。

**服务器刷新的值会覆盖使用者正在输入的内容吗？** 不会。当使用者正在编辑或保存正在运行时，来自外部的值不会替换文字。应等到编辑结束后再推送刷新。

**能否用于有固定格式的值，例如金额或日期？** 不能直接使用。控件保存的是字符串，即使用者输入的内容。金额请使用 `NumberField` 或 `MoneyInput`，日期请使用 `DatePicker`，使值在到达视图模型之前已被解析。

## API 参考

<!-- api: InlineEdit -->

## 无障碍

- 静态文字是一个按钮，以标签和值命名，例如“项目名称：Atlas redesign”。值为空时，名称使用占位文字。其帮助文本为“激活以编辑。”。
- `HeadingLevel` 会把文字报告为标题，供辅助技术使用。页面或区块标题使用 `1` 至 `3`，非标题文字保持默认的 `0`。标题级别的支持取决于平台。
- 编辑器由 `Label` 命名。其帮助文本说明各按键的作用（“Enter 保存，Escape 取消。”）；当校验消息生效时，消息会取代该帮助文本。
- 校验消息与保存错误位于礼貌的消息行中；保存进度与结果则通过礼貌的状态区域播报（“正在保存项目名称”“项目名称已保存”）。
- 保存与取消按钮分别命名为“保存项目名称”和“取消编辑”；重试链接读作“重试”。
- Enter、保存按钮和 Escape 会把焦点返回静态文字，使键盘用户从原来的位置继续。失去焦点（Tab 或点击其他地方）时，焦点留在使用者移动到的位置。

### 键盘

| 按键 | 操作 |
|---|---|
| 文字上的 `Enter` 或 `Space` | 打开字段，并选中全部文字，便于重新输入 |
| 字段中的 `Enter` | 提交并保存；有校验消息时，字段保持打开 |
| `Shift+Enter`（多行） | 添加换行 |
| 字段中的 `Escape` | 取消，恢复已保存的文字，并把焦点返回文字 |
| 字段中的 `Tab` | 提交并保存；焦点移到下一个控件 |
| `Left` / `Right`、`Home` / `End`、`Shift` 选择 | 移动光标并扩展选择，与文本框相同 |
| `Ctrl+A`、`Ctrl+C`、`Ctrl+X`、`Ctrl+V`（macOS 上为 `Cmd`） | 在字段中全选、复制、剪切和粘贴 |

## 动态效果

- 旧值滚出，新值带着柔和模糊升起。保存失败时，回滚方向相反，并短暂为外框着上危险色。
- 输入时宽度跟随草稿变化；多行时，框体高度平滑变化。
- 保存时，一个小型加载指示持续旋转。保存完成后，绘制对勾。
- 减少动态效果时，文字交叉淡入，对勾立即绘制，加载指示替换为静态标记。关闭动态效果时效果相同，但没有淡入。

| 变化 | 完整 | 减少 | 关闭 |
|---|---|---|---|
| 从外部替换值，或保存完成 | 新文字带着柔和模糊从下方升起；旧文字上移并淡出 | 文字交叉淡入 | 立即完成 |
| 打开或关闭字段 | 外框的宽度和边框以弹簧就位；文字与操作区随外框切换 | 文字立即切换 | 立即完成 |
| 输入改变宽度 | 快速的弹簧，无过冲（约 0.16 s） | 立即完成 | 立即完成 |
| 保存中 | 加载指示持续旋转，文字为半透明 | 静态标记替换加载指示 | 与减少相同 |
| 保存完成 | 对勾在短暂延迟后约 0.32 s 内绘制 | 对勾立即绘制 | 对勾立即绘制 |
| 保存失败 | 旧文字从上方落回；外框泛起危险色调并逐渐消退 | 文字交叉淡入；色调无淡出即清除 | 立即完成 |
| 悬停与键盘焦点 | 铅笔图标淡入，外框显示悬停色调 | 立即完成 | 立即完成 |

## 相关组件

- `Input` 用于始终可见、并与表单一起保存的字段。
- `Textarea` 用于表单中较长的正文文字。
- `ExpandingSearch` 用于会展开搜索字段与结果的控件。

| 组件 | 适用场景 |
|---|---|
| `Input` | 字段始终可见，有标签，并与其他字段一起保存 |
| `Textarea` | 文字是使用者在保存前撰写并检查的长正文 |
| `NumberField` / `MoneyInput` | 值是需要解析和格式化的数字或金额 |
| `Select` / `Combobox` | 值是已知选项列表中的一项 |
| `ExpandingSearch` | 使用者从页眉搜索记录或页面 |
| `InlineEdit` | 标题或名称在显示位置被重命名，并单独保存 |
