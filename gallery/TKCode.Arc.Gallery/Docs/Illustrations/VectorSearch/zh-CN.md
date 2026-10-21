---
title: 向量搜索
summary: 问题被嵌入为向量，并通过余弦相似度找出最相近的文档作为答案。
source-hash: 68b956871ae1
---

<Demo name="HelpCentreSearch" />

## 何时使用

- 说明语义搜索如何找到与问题没有任何相同词语、却仍然相关的答案。
- 帮助中心和产品文档，展示问题如何与文章进行匹配。
- 讲解嵌入、余弦相似度与 top-k 列表如何配合。

## 何时不用

- 在应用中回答真实用户的问题：内置嵌入是哈希词与三元组，而不是训练好的模型，因此应使用你自己的搜索服务，并用 `SearchField` 展示其结果。
- 精确或带过滤条件的查找，例如订单号：用带普通索引的 `SearchField`。
- 绘制真实测量值：用图表分类中的 `LineChart`。

## 用法

故事会输入一个问题，将其嵌入，找出最相近的文档，并列出它们及其相似度。内置索引包含四个主题下的 32 篇帮助中心文章，以及三个示例问题。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:VectorSearch />
</UserControl>
```

### 答案数量

`TopK` 设置列出的答案数量（1 到 5，默认 3）。列表按相关度从高到低排列，地图会圈出最远的那个答案。

```xml
<arc:VectorSearch TopK="4" />
```

### 你自己的文档与问题

`Documents` 接受 `VectorSearchDocument` 值（标题、主题和可选的关键词）。`Queries` 接受故事循环使用的问题。两者设置后都会替换默认值。

<Demo name="YourDocuments" />

### 搜索某个问题

`Search(query)` 会立即显示一个问题，之后故事继续播放它自己的问题。`Query` 是屏幕上显示的问题，`Results` 保存其答案。答案就绪时会触发 `ResultsChanged`。

<Demo name="AskYourOwn" />

每个 `VectorSearchResult` 都包含 `Document`、`Score`（余弦相似度）和 `Rank`（1 表示最匹配）。

### 标签

`Labels` 接受一个 `VectorSearchLabels` 对象，用于设置可见的文字：模型名称、列表标题和步骤说明文字。

```csharp
search.Labels = new VectorSearchLabels { Model = "文本模型", Header = k => $"最佳 {k} 个匹配" };
```

### 减弱动效

在减弱动效下，插图是第一个问题答案的静态画面。

<Demo name="ReducedMotion" />

## API 参考

<!-- api: VectorSearch -->

<!-- api: VectorSearchLabels -->

## 无障碍

- `Label` 是无障碍名称，当前的说明文字作为帮助文本提供。
- 插图可获得焦点，焦点环宽 2px。
- 空格键或 Enter 播放与暂停故事，R 键从头开始播放。
- 说明文字的变化不会被播报。排名后的答案也可通过 `Results` 以文字形式获取，因此应将其展示在用户可以阅读和导航的列表中。

## 动效

- 问题会以带闪烁光标的方式逐字输入，然后嵌入模型开始处理。
- 向量条逐格填充，查询点飞入文档地图。
- 半径不断扩大直到触及最后一个答案，答案按排名依次点亮，其所在行向上浮入列表。
- 减弱动效和关闭动效时，显示第一个问题答案的静态画面，没有光标和逐字输入。播放按钮会隐藏。

## 相关组件

- `SearchField`：应用中的问题输入框，也用于展示你自己的结果。
- `DataFlow`：搜索所依据的文档如何被收集与存储。
- `SortableDataTable`：以分数展示长列表结果。
