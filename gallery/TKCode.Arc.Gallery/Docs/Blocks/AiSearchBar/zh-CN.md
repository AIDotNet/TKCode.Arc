---
title: AI search bar
summary: 一个能用自然语言作答的搜索框，答案显示在常规结果之上，并带有推荐问题、编号引用、来源卡片和可以接着问的问题。
source-hash: 49c2cccfb2fd
---

<Demo name="Preview" />

## 何时使用

`AiSearchBar` 是一个也能回答问题的搜索框。还没输入时，它会给出几个推荐问题。提出问题后，最先出现的是一段简短的自然语言回答，文中带有小小的编号引用，指向所用的来源；接着是来源卡片，然后是可以接着问的问题，最后是常规的结果列表。人们既能得到答案，也能查看答案的出处，或自己打开某个结果。

- **帮助中心和员工手册。** 像 "How many vacation days do new hires get?" 这样的问题，值得一句话的回答，而不是十个链接。
- **工作区搜索。** 在文档、工单和知识库中搜索，答案在上，文档在下。
- **产品文档。** 回答操作类问题，相关页面一键可达。

## 何时不用

- **对话。** 需要层层追问时，使用 `AI chat` 区块。
- **单纯的筛选。** 按名称或标签缩小列表时，使用 `SearchField` 或 `Search results`。
- **没有来源的回答。** 本区块围绕引用设计；如果回答无法引用任何内容，聊天回复会更诚实。

## 用法

本区块派生自 Avalonia 的 `TemplatedControl`。回答来自 `AnswerProvider`；不设置时，搜索框会根据一份虚构的手册作答（`AiSearchBarSamples`），因此演示可以独立运行。

```xml
<arc:AiSearchBar SuggestedQuestions="{Binding Questions}" />
```

```csharp
search.AnswerProvider = async (query, cancellation) =>
{
    var result = await assistant.AskAsync(query, cancellation);
    return new AiSearchAnswer
    {
        Text = result.Text,                 // "... from their first day [1]."
        Sources = result.Sources.Select(s => new AiSearchSource { Title = s.Title, Location = s.Path, Link = s.Url }).ToArray(),
        Results = result.Documents.Select(d => new AiSearchResult { Title = d.Title, Snippet = d.Snippet, Link = d.Url }).ToArray(),
        FollowUps = result.FollowUps,
    };
};
search.SourceInvoked += (_, e) => Open(e.SearchSource.Link);
search.ResultInvoked += (_, e) => Open(e.Result.Link);
```

### 结构

- **输入框**：`Query`（双向）、Ask 按钮，以及有内容可清除时出现的清除按钮。
- **推荐**（`:idle`）：`SuggestedQuestions` 显示为小标签；按下即提问。
- **搜索中**：一行闪光的提示和若干占位行。
- **回答**（`:answered`）：正文中写着 `[1]`、`[2]` 的位置变成引用按钮；来源显示为带编号的卡片；追问显示为小标签；结果显示为带标题、位置和摘要的行。
- **失败**（`:failed`）：异常的消息和一个重试按钮。

### 自己的回答

<Demo name="OwnAnswers" />

`State`、`Answer`、`AskedQuery` 和 `ErrorText` 说明搜索框当前所处的状态。`SearchAsync(query)` 从代码提问，`Cancel()` 停止正在进行的搜索，`Clear()` 清空所有内容。新的问题会取消仍在进行的问题，只显示最新的回答。

### 文字

`Labels`（`AiSearchBarLabels`）包含占位文字、按钮、各个标题、搜索中和出错的提示，以及引用和结果数量的读屏名称。

## API 参考

<!-- api: AiSearchBar -->

<!-- api: AiSearchAnswer -->

<!-- api: AiSearchSource -->

<!-- api: AiSearchResult -->

<!-- api: AiSearchBarState -->

<!-- api: AiSearchBarLabels -->

## 无障碍

- 搜索框是一个搜索地标；输入框以 `Labels.Field` 命名。
- **Enter** 提问，**Escape** 停止正在进行的搜索或清空搜索框，**Down** 从输入框移到第一个推荐、来源或结果。
- 每个引用都是一个按钮，命名为 "Source 2: Travel and expenses"；每张来源卡片使用同样的名称，每个结果以标题命名、以摘要说明。
- "Reading your workspace…"、"Answer ready, 4 results" 和错误都会礼貌地朗读。

## 动效

- 回答和推荐出现时在 160 ms 内淡入；等待期间搜索中的提示会闪光。
- 小标签、卡片和行在悬停时于 160 ms 内变色。
- 减弱动效和关闭动效时各个状态立即显示；闪光遵循它自己的动效规则。

## 响应式

- 搜索框最宽 720，在更窄的容器中占满宽度。宽度不超过 440 时（`:compact`）内边距从 20 收紧到 16。
- 小标签和来源卡片会换行；来源卡片最宽 260，文字以省略号截断。
- 结果行的标题和位置保持在一行，摘要换行显示。

## 相关组件

- `SearchField` 和 `ExpandingSearch`，用于不需要回答的搜索。
- `AI answer`，用于对一个问题给出像文章一样的长回答。
- `Search results`，带筛选的完整结果页。
