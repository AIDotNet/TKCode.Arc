---
title: 知识库
summary: 助手可以搜索的文档：用选择器或拖放添加文件，看着每个文件上传、解析和向量化并显示分块数，重新索引或删除文件，并预览一个问题会找到哪些段落，匹配的词会被标出。
source-hash: 589ff42e1b82
---

<Demo name="Preview" />

## 何时使用

知识库是人们管理助手回答前所读文档的页面：手册、政策、产品指南和笔记。人们在这里需要知道三件事：里面有什么，每个文件是否已经进入索引，以及助手对某个问题实际会找到什么。`KnowledgeBase` 在一张卡片上回答这三个问题。页头统计文档数、分块数和大小，并说明还有多少文件在索引中。文件通过"Add files"或拖放到卡片上进入队列。每一行随后依次经过 Uploading、Parsing 和 Embedding，并显示进度环，最后变为 Ready 并显示分块数和日期，或者变为 Failed 并显示原因和"Retry"按钮。每行末尾有"Re-index"和"Delete"（带行内确认）。列表旁边的搜索预览会为一个问题给已索引的段落排序，显示每个段落的相关度，并标出匹配的词。按下一个结果会跳到它所在的文档。

- **助手或客服机器人的来源页面**：人们上传帮助中心文章，并看到每篇文章何时可以被找到。
- **排查回答**：助手答错时，搜索预览会显示它读了哪些段落。
- **检索流程的管理视图**：各个阶段、失败和分块数通过 `IndexHandler` 来自你自己的索引程序。

## 何时不用

- **普通的文件存储**：这里没有文件夹、共享或版本。请使用 `FileUpload` 加上你自己的列表。
- **聊天中的一个附件**：请使用 `AiComposer`，它把文件上传到消息中。
- **编辑文档**：卡片只列出并索引文件，不会打开或修改它们。
- **完整的搜索页**：预览只显示几个段落，用来检查检索结果。供人浏览的搜索请使用 `SearchResults`。

## 用法

`KnowledgeBase` 派生自 Avalonia 的 `TemplatedControl`。一个文档是一个 `KnowledgeDocument`，它是一个 `AvaloniaObject`，包含 id、名称、类型、大小、状态、进度、分块数、日期、错误，以及供内置流程使用的文本和分块。没有你自己的文档时，卡片会显示一个虚构的资料库（`KnowledgeBaseSamples`）。真实的空知识库请设置 `IsSampleData="False"`。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:KnowledgeBase AssistantName="Pip" DocumentsSource="{Binding Documents}" />
</UserControl>
```

### 组成

- **页头**：标题、写有助手名字的副标题、总计（"6 documents · 13 chunks · 2.6 MB"）、带转圈的索引提示，以及"Add files"。
- **拖放区**：文件拖到卡片上方时会亮起的一条区域，显示可接受的类型和大小上限，或者被跳过文件的说明。
- **文档列表**：每个文件一行。类型图标、名称及其下方的信息（类型、大小、分块数、日期）或错误、带进度环的阶段，然后是"Retry"、"Re-index"和"Delete"。
- **搜索预览**：一个搜索框、一行状态，以及最多 `MaxPassages` 个结果。每个结果显示排名、文档和分块、带条形的分数，以及标出匹配词的段落。

### 添加文档

"Add files"会打开系统的文件选择器，并按 `AcceptedExtensions` 过滤。你也可以把文件拖放到卡片上的任何位置。其他类型或大于 `MaxFileSize`（20 MB）的文件会被跳过，拖放区会说明这一点。其余文件以 Queued 状态加到列表顶部。最多 `MaxConcurrentIndexing`（2）个文档同时索引，其余按到达顺序等待。在代码中可以调用 `AddDocument`、`AddFiles`（名称、大小和文本）或 `AddFilesAsync`（存储文件）。

<Demo name="Empty" />

### 索引

设置 `IndexHandler` 可以把文档发送到你的后端。通过 progress 参数报告阶段和整体进度，行中的文字和进度环会跟着变化。文档进入索引后返回分块；抛出异常则文档失败，并显示异常的消息。

```csharp
knowledge.IndexHandler = async (document, progress, cancellation) =>
{
    progress.Report(new KnowledgeIndexProgress(KnowledgeDocumentStatus.Uploading, 0.1));
    var id = await api.UploadAsync(document.SourceFile, cancellation);
    progress.Report(new KnowledgeIndexProgress(KnowledgeDocumentStatus.Embedding, 0.5));
    return await api.WaitForChunksAsync(id, cancellation);
};
```

没有设置时，卡片会运行一个模拟流程：选择的文件分四步上传，文本被分成约 480 个字符的分块（段落尽量保持完整），向量化时每个分块一步。纯文本文件（.txt、.md、.csv、.json、.html）的文本在添加时读取；其他文件需要你的处理程序，模拟流程会以"No readable text"让它们失败。`KnowledgeBase.ChunkText` 用同样的方式分块，所以你也可以在服务器上分块。

<Demo name="Pipeline" />

### 重新索引、重试和删除

"Re-index"会让已就绪的文档再走一遍流程；"Retry"对失败的文档做同样的事。"Delete"会把这一行换成一个问题（"Delete Returns and refunds.pdf? Its 5 chunks leave the index."），并提供"Cancel"和"Delete"。设置 `DeleteHandler` 可以先从你的存储中删除文档；如果它抛出异常，文档会保留，消息会被朗读。`Reindex`、`ReindexAll` 和 `RemoveAsync` 在代码中做同样的事。

### 搜索预览

输入一个问题，稍停片刻后，预览会给已就绪文档的分块排序。内置搜索对词语使用 BM25，忽略"the"和"how"这类常见词，也能找到复数和更长的形式（"refund"能找到"refunds"和"refunded"）。分数在 0 到 1 之间。设置 `SearchHandler` 可以改为查询你的向量索引；预览仍会在你返回的结果中标出查询的词。Enter 立即搜索，Escape 清空搜索框。按下一个结果会把列表滚动到它的文档，给这一行着色，并引发 `PassageInvoked`。

`SearchText` 是双向的，`Passages` 保存结果。

### 只读

`IsReadOnly` 会隐藏"Add files"、拖放区和行内按钮，保留列表和搜索预览。适合可以查看来源但不能修改的人。

<Demo name="ReadOnly" />

### 文字

所有文字都来自 `Labels`（`KnowledgeBaseLabels`）。带数量或名称的文字是函数，因此译文可以按自己的语序排列。控件上的 `Title` 优先于标签。

### 内容指南

- **保留人们上传时的文件名**：他们认得自己起的名字，搜索结果也会显示它。
- **用一句话说明失败原因**："The workbook is password protected"告诉人们要修什么；"Error 500"做不到。
- **用真实的问题来预览**：输入客户会问的话，而不是关键词，才能看到助手会读到什么。

## API 参考

<!-- api: KnowledgeBase -->

<!-- api: KnowledgeDocument -->

### 事件

| 事件 | 何时引发 | 参数 |
|---|---|---|
| `DocumentAdded` | 一个文档加入列表 | `Document` |
| `DocumentDeleted` | 一个文档离开列表 | `Document` |
| `IndexingCompleted` | 一个文档已就绪 | `Document`、`ChunkCount`、`IsReindex` |
| `IndexingFailed` | 索引一个文档失败 | `Document`、`Exception`、`Message` |
| `PassageInvoked` | 按下了一个搜索结果 | `Passage` |

## 无障碍

- 卡片是一个区域。标题是二级标题；"Documents"和"Search preview"是三级标题。
- 每一行有一个名称，读出文件、阶段、信息和错误（"Battery care.txt, Embedding 64%, TXT · 6 KB"）。"Re-index"、"Delete"和"Retry"按钮的名称带有文件名（"Delete Shipping FAQ.md"），并有工具提示。
- 一个礼貌的实时区域会朗读添加的文件、每个就绪或失败的文档、删除、重新索引以及找到的段落数。
- "Delete"从不立即执行：问题出现时焦点在"Cancel"上；取消后焦点回到"Delete"，删除后焦点移到下一行。
- 状态从不只靠颜色：Ready 有对勾，Failed 有警告图标和原因，进行中的阶段有名称和百分比。

### 键盘

| 按键 | 操作 |
|---|---|
| `Tab` | "Add files"，然后是每行的按钮、搜索框和结果 |
| 行内按钮上的 `ArrowUp` / `ArrowDown` | 上一行或下一行中的同一个按钮 |
| 行内按钮上的 `Home` / `End` | 第一行或最后一行 |
| 删除问题中的 `Escape` | 取消并回到"Delete" |
| 搜索框中的 `Enter` | 立即搜索 |
| 搜索框中的 `Escape` | 清空搜索框 |
| 搜索框中的 `ArrowDown` | 移到第一个结果；`ArrowUp` / `ArrowDown` 在结果之间移动 |
| 结果上的 `Enter` 或 `Space` | 在列表中打开它的文档 |

## 动效

- 进度环平滑地跟随每次报告的进度，而不是跳变。
- 文件拖到卡片上方时，拖放区的底色和边框在 160 ms 内变化。
- 悬停时，行和结果的底色在 160 ms 内变化。删除问题在 240 ms 内淡入。
- 拖放区和搜索预览中的状态文字交叉淡化，进入时上升 2 px。
- 减少动效时淡化很短，进度环直接画到当前值。关闭动效时所有变化立即生效。

| 变化 | 完整 | 减少 | 关闭 |
|---|---|---|---|
| 进度环 | 跟随数值 | 跳变 | 跳变 |
| 拖放区和行的底色 | 160 ms | 立即 | 立即 |
| 删除问题 | 淡入 240 ms | 立即 | 立即 |
| 状态文字 | 交叉淡化，上升 2 px | 120 ms 交叉淡化 | 立即 |

## 响应式

- `KnowledgeBase` 按自身宽度而不是窗口宽度变化。从 760 px（`WideWidth`，`:wide`）起，搜索预览是列表旁边一个 340 px 宽的列；更窄时它排在列表下方。
- 窄于 480 px（`CompactWidth`，`:compact`）时，内边距收紧，行会隐藏日期和阶段文字（进度环和图标保留）。
- 卡片最宽 960 px 并居中。列表超过 420 px 时在卡片内滚动，并且是虚拟化的，所以几千个文档也很快。
- 名称和信息以省略号结尾；错误和段落会换行。

## 相关组件

- `FileUpload` 用于上传文件而不建立索引。
- `AiChat` 用于使用这些文档的聊天。
- `AgentMemory` 用于助手记住的关于一个人的内容。

| 组件 | 何时选择 |
|---|---|
| `FileUpload` | 文件只是存储，不为助手建立索引 |
| `SearchResults` | 人们浏览搜索结果 |
| `AgentMemory` | 助手保存关于一个人的简短事实 |
| `AiAnswer` | 你要展示一个带来源的回答 |
