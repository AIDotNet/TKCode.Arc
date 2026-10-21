---
title: 提及输入框
summary: 支持 @人员 与 #频道 提及的多行文本框，提及作为整体令牌处理，并在插入符处显示建议。
source-hash: 4412b13eb7b5
---

<Demo name="CommentBox" />

## 何时使用

`MentionInput` 是用于回复人员和频道的多行字段。用户输入 `@` 并选择同事，或输入 `#` 并选择频道，提及就会成为一个整体令牌，字段会将其作为整体保留。它适用于评论串的编辑区、回复框、聊天消息或任务描述：文字读起来像一个句子，而提及仍须指向应用可以通知或链接的记录。

- **事件、订单或工单的评论串**：支持负责人写下“感谢 @Alex Rivera，@Priya Natarajan 能确认一下退款编号吗？”，每个提及都保存人员的 ID，因此应用可以通知正确的人，而不必事后按名字匹配。
- **发布到频道的发布说明与更新**：工程师在句子中链接 `#launch-q3`，消息因此携带一个频道引用，聊天服务可以将其渲染为链接。
- **项目工具中的任务与评审描述**：设计师在描述中用 `@` 提及来分派后续工作，保存的值把人员作为结构化数据保留，供指派历史使用。
- **带发送操作的聊天编辑区**：消息框通过 `IsSubmitOnEnter` 在按下 Enter 时提交，自行清空，并将 `MentionValue` 传给发送命令。

## 何时不用

- **没有引用的普通多行文本**：用 `Textarea`，它没有建议列表，也没有需要整体保留的令牌。
- **独立于句子之外的值**：用 `TagInput`，它把每个值作为标签保留在文字之外。
- **标题、列表、链接或加粗文字**：用富文本编辑器。`MentionInput` 只保存纯文本加提及，没有格式模型。
- **名称、标题或搜索词等单行内容**：用 `Input` 或 `SearchField`。单行字段不需要自动增长的编辑区或按 Enter 提交的流程。
- **从大型目录中选择一条记录**：用带搜索的 `Combobox` 或 `Select`。提及列表只显示最佳的 `MaxSuggestions` 个匹配项，适合人员和频道的短列表，而不适合完整的目录查询。

## 用法

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:MentionInput Label="评论"
                      Placeholder="写下评论。输入 @ 提及，输入 # 选择频道"
                      MinLines="1"
                      MaxLines="8" />
</UserControl>
```

该字段没有可见标签：`Label` 只为辅助技术提供编辑区的名称，说明文字由占位文字承担。字段本身不是独立的 Tab 停靠点，焦点直接移入编辑区。

`MentionInput` 派生自 `TemplatedControl`。其模板包含一个真正的多行 `TextBox`，因此光标、选择、输入法组字、撤销与剪贴板的行为与文本框一致。编辑区自身的文字是透明的，其后的镜像层以相同的度量绘制同样的文字并为提及令牌着色，因此颜色与换行都与光标保持对齐。你通过 `Value`（一个 `MentionValue`）读写内容：纯文本加上各提及及其字符偏移量。

### 结构

1. **外框**（PART_Shell）：1 px 边框，控件圆角，表面色背景。静止时使用边框颜色，编辑区获得焦点时使用强边框颜色。悬停不会改变它。
2. **编辑区**（PART_TextBox）：16 px 正文字体的可编辑文字，行高 24 px，字母间距 -0.01 em，左右各 14 px 内边距，上下各 11 px。
3. **占位文字**（`Placeholder`）：值为空且没有待定的输入法文字时，以弱化文字颜色显示。
4. **提及令牌**：圆角背景上的 `@Label` 或 `#Label` 文字，水平内边距 3 px，垂直内边距 1 px。人员令牌使用强调色的浅色填充与强调色的深色文字；频道令牌使用前景色 7% 不透明度的背景与前景文字。
5. **建议面板**：在触发字符处弹出的浮层，宽 272 px（字段更窄时与字段同宽），距离所在行 6 px。每行依次包含 28 px 的头像或图标、名称，再是角色或描述。频道行在右侧显示成员数量。

### 何时展开建议

输入 `@` 展开人员列表，输入 `#` 展开频道列表。只有在词的开头才算触发：位于空格之后、行首，或左括号、引号之后。因此 `billing@acme.com` 中的 `@` 不会展开列表。触发字符之后的查询有以下限制：

- 人员查询可以包含一个空格，因此 `@maya chen` 可以匹配。第二个空格会关闭列表。
- 频道查询不允许空格，因此 `#launch q3` 在空格处关闭列表。
- 超过 48 个字符的查询，或跨越换行的查询，会关闭列表。
- 在查询中间再输入一个触发字符会关闭列表。

将 `People` 或 `Channels` 设为 `null` 或空集合即可关闭对应的触发。此时字段把 `@` 或 `#` 视为普通文字，这适合只需要通知人员的评论串。

结果会先排序，再保留前 `MaxSuggestions` 个（默认 6 个）。名称以查询开头的排在最前，其次是名称中以查询开头的单词，再次是名称中包含查询的，最后是角色或描述中的匹配。没有匹配项时显示空状态（“没有匹配 “zz” 的人员”或“没有匹配 “zz” 的频道”），列表保持打开，使用户可以继续输入或按 Escape。

### 绑定到视图模型

视图模型拥有存储的值。`Value` 默认为双向绑定，因此每次编辑都会回传到视图模型。使用 CommunityToolkit.Mvvm 时：

```csharp
public partial class ReplyComposerViewModel : ObservableObject
{
    private readonly ICommentService _comments;

    public ReplyComposerViewModel(ICommentService comments,
                                  IReadOnlyList<MentionPerson> people,
                                  IReadOnlyList<MentionChannel> channels)
    {
        _comments = comments;
        People = people;
        Channels = channels;
    }

    [ObservableProperty]
    private MentionValue _draft = MentionValue.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SendCommand))]
    private bool _isSending;

    public IReadOnlyList<MentionPerson> People { get; }

    public IReadOnlyList<MentionChannel> Channels { get; }

    [RelayCommand(CanExecute = nameof(CanSend))]
    private async Task SendAsync(MentionValue value)
    {
        IsSending = true;
        try
        {
            await _comments.PostAsync("inc_2026_0412", value);
            Draft = MentionValue.Empty;
        }
        finally
        {
            IsSending = false;
        }
    }

    private bool CanSend(MentionValue? value) =>
        !IsSending && value is not null && !string.IsNullOrWhiteSpace(value.Text);
}
```

```xml
<StackPanel Width="560" Spacing="12">
    <arc:MentionInput Label="回复"
                      Value="{Binding Draft, Mode=TwoWay}"
                      People="{Binding People}"
                      Channels="{Binding Channels}"
                      IsSubmitOnEnter="True"
                      SubmitCommand="{Binding SendCommand}"
                      MinLines="2"
                      MaxLines="6" />
    <arc:Button Content="发布回复"
                Command="{Binding SendCommand}"
                CommandParameter="{Binding Draft}"
                HorizontalAlignment="Right" />
</StackPanel>
```

- 绑定 `Value`，而不是文本，因为仅有文本会丢失提及。`People` 与 `Channels` 是单向的。
- 发送操作使用 `SubmitCommand` 或 `Submitted` 事件。只有当 `CanExecute` 对提交的值返回 `true` 时，命令才会执行。
- 选择人员或频道的副作用使用 `MentionAdded` 处理；依赖整段文字的内容使用 `ValueChanged`。

### 预填值

用文字及其提及来设置 `Value`。每个偏移量分别指向触发字符（`Start`）与标签之后的位置（`End`）。在提及周围编辑会移动它：在它之前输入会同时改变两个偏移量。在提及内部编辑会移除该提及，其文字作为纯文本保留在字段中。

<Demo name="Prefilled" />

```csharp
var text = "Thanks @Maya Chen. Can @Jordan Lee review it in #design?";
comment.Value = new MentionValue(text,
[
    new Mention(MentionKind.Person, "u_maya", "Maya Chen", 7, 17),
    new Mention(MentionKind.Person, "u_jordan", "Jordan Lee", 24, 34),
    new Mention(MentionKind.Channel, "c_design", "design", 47, 54)
]);
```

从模板生成内容时应根据文字计算偏移量，因为错误的偏移量会把提及附加到错误的字符上。

### 按 Enter 提交

将 `IsSubmitOnEnter` 设为 `True` 后，Enter 提交，Shift+Enter 换行。默认值 `False` 时，Enter 换行，发送操作是一个按钮。输入法正在组字时 Enter 永远不会提交，因此可以用 Enter 确认候选词。空白的值不会提交。建议列表有结果时，Enter 改为选择高亮的一行，因此写到一半的提及永远不会被发送。

<Demo name="SubmitOnEnter" />

```csharp
message.IsSubmitOnEnter = true;
message.Submitted += (_, e) =>
{
    Console.WriteLine($"{e.Value.Text} ({e.Value.Mentions.Count} mentions)");
    message.Clear();
};
```

### 行数与增长

`MinLines` 与 `MaxLines` 设定高度范围（以行计）。字段从 `MinLines` 开始，随文字换行或输入新行，每次增长一行 24 px，直到 `MaxLines`。超过该高度后，编辑区在外框内滚动并隐藏滚动条，使编辑区不会把页面其余部分向下推。默认值为 1 和 8。

评论串的编辑区通常使用 `MinLines="2"` 与 `MaxLines="6"`。

<Demo name="Growth" />

### 尺寸与位置

`SuggestionPlacement` 设定列表的展开位置。`Auto`（默认）在下方展开，但当下方空间少于 300 px 且上方空间更大时翻转到上方。`Top` 与 `Bottom` 始终使用所选的一侧。

<Demo name="Placement" />

### 命令与事件

`Insert`、`OpenSuggestions` 与 `Clear` 从代码中操作字段。`Insert` 在光标处输入文字，效果与用户手动输入相同，因此会进入撤销栈。`OpenSuggestions` 输入某类的触发字符；若光标前的字符不是空白，会先添加一个空格。`Clear` 清空值并重置光标。

```csharp
button.Click += (_, _) => comment.OpenSuggestions(MentionKind.Person);

comment.MentionAdded += (_, e) => Console.WriteLine($"Added {e.Mention.Text}");
```

`ValueChanged` 在 `Value` 的每次变化时触发，并携带旧值与新值。`MentionAdded` 只在建议变为提及时触发。

### 禁用

整个字段淡化到 55%，且不接受任何输入。其文字与提及仍可读。

<Demo name="Disabled" />

### 焦点与关闭

建议列表是会自行关闭的浮层：

- **Escape** 只关闭当前触发的列表。在同一查询中继续输入会让它保持关闭，空格之后的新触发则会再次打开它。
- **在字段和列表之外按下** 会关闭列表。在字段内按下（例如移动光标）不会关闭。
- 列表有结果时，**Tab** 选择高亮的建议；否则移动焦点。
- **点击某一行** 会让焦点留在编辑区，并把光标放在令牌及其后的空格之后，使用户可以继续输入。撤销（Ctrl+Z，macOS 上为 Cmd+Z）会撤回该选择，但不会撤回提及对象本身。

### 序列化输出

<Demo name="Serialized" />

`MentionSerializer.Serialize` 将值转换为存储文本。默认情况下，每个人员变为 `<@id>`，每个频道变为 `<#id>`，其余文字不变。传入格式化函数可以写成其他形式，例如方括号中的显示名称。当名称可能变化时应保存默认形式，因为存储的文本会持续指向同一个 ID。

```csharp
var stored = MentionSerializer.Serialize(comment.Value);
// 存储结果："Thanks <@u_maya>. Can <@u_jordan> review it in <#c_design>?"
```

### 示例：事件回复串

事件回复串中的回复框（`inc_2026_0412`，Northwind 订单的重复扣款）。被回复的消息位于其上方以供参考。回复中提及了两人和一个频道，每个都在字段中显示为令牌。主要操作位于字段下方右侧。

![包含两个人员提及和一个频道提及的事件回复框](images/ReplyThread.png)

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc"
             xmlns:generic="using:System.Collections.Generic">
    <StackPanel Width="600" Spacing="14">
        <StackPanel Spacing="4">
            <TextBlock Text="Duplicate charge on order NW-48213"
                       FontSize="14"
                       FontWeight="SemiBold"
                       Foreground="{arc:ArcDesignTokenResource Foreground}" />
            <TextBlock Text="Priya Natarajan, 2 hours ago"
                       FontSize="12"
                       Foreground="{arc:ArcDesignTokenResource TextMuted}" />
            <TextBlock Text="The card was charged twice at 09:14. The second charge is pending a refund."
                       FontSize="13"
                       TextWrapping="Wrap"
                       Margin="0,6,0,0"
                       Foreground="{arc:ArcDesignTokenResource TextSecondary}" />
        </StackPanel>
        <arc:MentionInput Label="Reply to incident" MinLines="2" MaxLines="6">
            <arc:MentionInput.Value>
                <arc:MentionValue>
                    <x:Arguments>
                        <x:String>Thanks @Maya Chen. Can @Jordan Lee confirm the refund window before we post to #launch-q3?</x:String>
                        <generic:List x:TypeArguments="arc:Mention">
                            <arc:Mention>
                                <x:Arguments>
                                    <arc:MentionKind>Person</arc:MentionKind>
                                    <x:String>u_maya</x:String>
                                    <x:String>Maya Chen</x:String>
                                    <x:Int32>7</x:Int32>
                                    <x:Int32>17</x:Int32>
                                </x:Arguments>
                            </arc:Mention>
                            <arc:Mention>
                                <x:Arguments>
                                    <arc:MentionKind>Person</arc:MentionKind>
                                    <x:String>u_jordan</x:String>
                                    <x:String>Jordan Lee</x:String>
                                    <x:Int32>23</x:Int32>
                                    <x:Int32>34</x:Int32>
                                </x:Arguments>
                            </arc:Mention>
                            <arc:Mention>
                                <x:Arguments>
                                    <arc:MentionKind>Channel</arc:MentionKind>
                                    <x:String>c_launch</x:String>
                                    <x:String>launch-q3</x:String>
                                    <x:Int32>78</x:Int32>
                                    <x:Int32>88</x:Int32>
                                </x:Arguments>
                            </arc:Mention>
                        </generic:List>
                    </x:Arguments>
                </arc:MentionValue>
            </arc:MentionInput.Value>
        </arc:MentionInput>
        <StackPanel Orientation="Horizontal" Spacing="8" HorizontalAlignment="Right">
            <arc:Button Content="Discard" Variant="Secondary" />
            <arc:Button Content="Post reply" />
        </StackPanel>
    </StackPanel>
</UserControl>
```

短回复会保持 `MinLines` 设定的两行高度，因此输入时回复框不会跳动。

### 文案规范

- **占位文字**：告诉用户这个字段用于什么，以及如何提及某人：“写下评论。输入 @ 提及，输入 # 选择频道”。在字段宽度内保持一行。
- **标签**：供辅助技术使用的简短名词短语，例如“回复事件”。它不显示在屏幕上。
- **操作**：用动词加宾语为发送操作命名，例如“发布回复”或“发送消息”，不要用“提交”。空状态文案（“没有匹配“{0}”的人员”）保持默认，因为它会指出查询内容。

| 不要写 | 改为 |
|---|---|
| 在这里输入你的评论！ | 写下评论。输入 @ 提及，输入 # 选择频道 |
| 提及某人 | 输入 @ 提及人员 |
| 提交 | 发布回复 |
| 提及（Maya Chen，产品设计师） | Maya Chen |
| 没有结果 | 没有匹配 “zz” 的人员 |

### 最佳实践

**推荐**

- 存储 `MentionValue`，或其序列化形式，而不是纯文本，使 ID 在编辑与改名之后仍然有效。
- 将 `MinLines` 设为典型回复的高度，将 `MaxLines` 设为字段下方可用的空间，使回复框在用户输入时不会推动页面。
- 当某类提及不适用时，用空集合关闭对应的触发，使 `@` 或 `#` 保持为普通文字。
- 使用目录中的稳定 ID，而不是显示名称，使改名后的人仍然指向同一引用。

**避免**

- 只存储 `Text`。这样提及就无法按 ID 通知，也无法在下一次编辑时恢复。
- 在文档编辑器中使用 `IsSubmitOnEnter`，因为那里 Enter 必须换行。在那里应使用按钮来发送。
- 把整个目录加载到 `People` 中。列表只显示最佳匹配，因此大型目录需要一个会为每个查询更新 `People` 的搜索。

### 常见问题

**为什么按 Enter 是选择建议，而不是提交？** 列表打开且有结果时，Enter 与 Tab 会选择高亮的一行。列表关闭后（选择之后、按下 Escape，或以空格结束查询之后），Enter 才会提交。

**撤销之后为什么提及丢失了？** 撤销会恢复令牌中输入的文字，但不会恢复提及对象。若撤销时必须保留提及，请处理 `ValueChanged`，并用 `NewValue` 重建你自己的提及列表。

## API 参考

<!-- api: MentionInput -->
<!-- api: MentionPerson -->
<!-- api: MentionChannel -->

`Mention` 是值的基本单位；`Start` 与 `End` 是 `Text` 中的字符偏移量。

| 成员 | 类型 | 含义 |
|---|---|---|
| `Kind` | `MentionKind` | `Person`（`@`）或 `Channel`（`#`） |
| `Id` | `string` | 人员或频道的 ID，用于存储与通知 |
| `Label` | `string` | 触发字符之后显示的名称 |
| `Start` | `int` | 触发字符的偏移量 |
| `End` | `int` | 标签之后的偏移量（不含末尾） |
| `Text` | `string` | 令牌文字：`@Label` 或 `#Label` |

## 无障碍

- 编辑区是带列表自动补全的组合框。`Label` 为其命名；当 `Label` 为 null 时，改用控件的 `AutomationProperties.Name`。
- 建议列表由 `PeopleListName`（人员）或 `ChannelsListName`（频道）命名。每一行朗读其名称及角色或描述，例如“Maya Chen，产品设计师”。
- 结果数量以礼貌方式播报（"3 people"、"1 channel"、"No matches"），列表关闭时清除。
- 提及颜色来自隐藏的图层，因此辅助技术读取的是纯文本。

### 键盘

| 按键 | 操作 |
|---|---|
| 空格、行首，或左括号、引号之后输入 `@` 或 `#` | 展开人员或频道建议 |
| `Down` / `Up` | 在建议之间移动高亮，到两端时循环（列表打开时） |
| `Enter` / `Tab` | 列表有结果时选择高亮的建议 |
| `Escape` | 只关闭当前触发的列表 |
| `Enter` | 当 `IsSubmitOnEnter` 为 `True` 且文字不为空时提交；否则换行 |
| `Shift+Enter` | 换行 |
| `Left` / `Right` | 移动一个字符，或整体跳过一个提及令牌 |
| 提及旁的 `Backspace` / `Delete` | 一次按键删除整个令牌 |
| `Ctrl+Z` / `Ctrl+Y`（macOS 上为 `Cmd+Z` / `Cmd+Shift+Z`） | 撤销与重做，包括从列表中做出的选择 |

Enter 在输入法组字期间永远不会提交。

## 动效

- 字段在平滑的弹簧上跟随内容变化，建议面板从光标处以小幅缩放展开，高亮在各行之间滑动。减弱动效时高度与高亮直接跳转，面板只淡入。

| 变化 | 完整 | 减弱 | 关闭 |
|---|---|---|---|
| 字段高度随文字变化 | 平滑弹簧 | 直接跳到新高度 | 立即完成 |
| 建议面板展开 | 从光标处小幅缩放，干脆利落 | 淡入，无缩放 | 立即完成 |
| 高亮在各行之间移动 | 0.3 秒内滑动，带轻微回弹 | 直接跳转 | 立即完成 |
| 结果变化时面板高度 | 弹簧 | 直接跳转 | 立即完成 |
| 聚焦时的边框 | 边框颜色渐变 | 短暂淡入 | 立即完成 |

## 相关组件

| 组件 | 适用场景 |
|---|---|
| `Textarea` | 文字是多行纯文本，没有需要引用的人员或频道 |
| `Input` / `SearchField` | 值只有一行，例如名称或搜索词 |
| `TagInput` | 值是一组独立于句子之外的标签 |
| `Select` / `Combobox` | 用户必须从完整列表中选择一个值，而不是在文字中提及 |
| `Field` + `FieldFrame` | 你要自行构建编辑器，并希望获得相同的标签、外框与消息行 |
