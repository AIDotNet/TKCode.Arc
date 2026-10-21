---
title: 提及输入框
summary: 支持 @人员 与 #频道 提及的多行文本框，提及作为整体令牌处理，并在插入符处显示建议。
source-hash: 353fdfba5bf8
---

<Demo name="CommentBox" />

## 何时使用

- 评论与回复框，其中提及需要通过 id 通知某人。
- 聊天与任务编辑器，需要在正文中链接频道或项目。
- 将提及保存为结构化数据而非纯文本的编辑器。

## 何时不用

- 不含引用的普通多行文本：用 `Textarea`。
- 不属于句子的值列表：用 `TagInput`。
- 文本中的标题、列表与链接：用富文本编辑器。
- 名称或搜索词等单行文本：用 `Input` 或 `SearchField`。

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

该字段没有可见标签。`Label` 只为辅助技术提供字段名称。

### 人员与频道

`People` 与 `Channels` 提供建议列表。将其留空即可关闭对应的触发字符。每个人员和频道都有 id、名称以及可选的详细信息。

```csharp
var people = new[]
{
    new MentionPerson("u_maya", "Maya Chen") { Role = "产品设计师" },
    new MentionPerson("u_jordan", "Jordan Lee") { Role = "工程经理" }
};

var channels = new[]
{
    new MentionChannel("c_design", "design") { Description = "评审、审阅与交接", Members = 48 }
};

var comment = new MentionInput
{
    Label = "评论",
    People = people,
    Channels = channels,
    MaxSuggestions = 6
};
```

### 预填值

`Value` 是一个 `MentionValue`：纯文本加上各提及及其偏移量。在提及周围编辑会移动它，在提及内部编辑则会将其移除。

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

### 按 Enter 提交

启用 `IsSubmitOnEnter` 后，Enter 提交，Shift+Enter 换行。输入法正在组合文字时，Enter 永远不会提交。

<Demo name="SubmitOnEnter" />

```csharp
message.IsSubmitOnEnter = true;
message.Submitted += (_, e) =>
{
    Console.WriteLine($"{e.Value.Text} ({e.Value.Mentions.Count} mentions)");
    message.Clear();
};
```

### 尺寸与位置

`MinLines` 与 `MaxLines` 设定高度范围。`SuggestionPlacement` 设定列表的展开位置：`Auto` 选择空间更大的一侧，`Top` 与 `Bottom` 则强制指定一侧。

<Demo name="Growth" />

<Demo name="Placement" />

### 命令与事件

`Insert`、`OpenSuggestions` 与 `Clear` 从代码中操作字段。`ValueChanged` 与 `MentionAdded` 报告编辑变化。

```csharp
button.Click += (_, _) => comment.OpenSuggestions(MentionKind.Person);

comment.MentionAdded += (_, e) => Console.WriteLine($"Added {e.Mention.Text}");
```

### 禁用

整个字段会淡化到 55%，且不接受任何输入。

<Demo name="Disabled" />

### 序列化输出

`MentionSerializer.Serialize` 会将值转换为存储文本：人员为 `<@id>`，频道为 `<#id>`。

<Demo name="Serialized" />

## API 参考

<!-- api: MentionInput -->
<!-- api: MentionPerson -->
<!-- api: MentionChannel -->

## 无障碍

- 文本框是带列表自动补全的组合框，名称由 `Label` 提供。
- 建议列表名为 People 或 Channels。结果数量（例如 "3 people" 或 "No matches"）会以礼貌方式播报。
- 提及的颜色绘制在隐藏的图层上，因此辅助技术读取的是纯文本。Escape 仅关闭建议列表。

## 动效

- 字段随内容变化在 `MinLines` 与 `MaxLines` 之间伸缩。
- 建议面板从插入符处以轻微缩放展开，高亮在各行之间滑动。
- 结果数量变化时，面板高度以弹簧效果调整。
- 减弱动效时，高度与高亮直接跳转，面板仅淡入。关闭动效时立即显示或隐藏。

## 相关组件

- `Textarea`：普通多行文本。
- `TagInput`：不属于句子的短值列表。
- `Input`：单行输入。
