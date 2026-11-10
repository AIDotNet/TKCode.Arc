---
title: Help center
summary: 帮助中心首页，有搜索、主题和热门文章；文章页有路径导航、可跳到各节的目录、相关文章，以及“这篇文章有帮助吗”的提问。
source-hash: c7e12d080065
---

<Demo name="Preview" />

## 何时使用

`HelpCenter` 是产品帮助文章的首页。搜索框在输入时就能找到文章，主题把文章分组，阅读最多的文章列在前面。打开文章后，有返回所在主题的路径导航、可跳到各节的目录、相关文章，以及“这篇文章有帮助吗？”的提问，回答“否”后会询问缺了什么。

- **帮助网站或应用内帮助页**：几十到几百篇文章。
- **产品知识库**：给客户或自己的团队使用。
- **按任务分组的上手指南**。

## 何时不用

- **页面角落里的小帮助面板**：使用 `SupportWidget`。
- **单篇长文档**：使用 `Markdown` 并自己做目录。
- **数千篇文档的全文搜索**：在服务器上搜索，用自己的列表显示结果。

## 用法

```xml
<arc:HelpCenter x:Name="Help" Title="How can we help?" Height="680" />
```

```csharp
Help.FeedbackGiven += (_, e) => api.Rate(e.Article.Id, e.IsHelpful, e.Comment);
Help.Categories.Add(new HelpCenterCategory("billing", "Billing", "Invoices and plans", ArcIcons.CreditCard));
Help.Articles.Add(new HelpCenterArticle("refunds", "billing", "How refunds work", "When the money comes back.", markdown)
{
    IsPopular = true,
    UpdatedAt = DateTimeOffset.Now,
});
```

文章使用 Markdown。正文中的 `##` 和 `###` 标题构成目录。`IsPopular` 让文章出现在首页，阅读时间按正文计算。搜索要求查询中的每个词都出现在标题、摘要或正文中，标题匹配排在前面；按 `Enter` 打开第一条结果。首次加载时没有文章，会显示一组示例。

`SearchText`、`CurrentCategory` 和 `CurrentArticle` 是双向的，`View` 表示正在显示哪一页。`OpenArticle`、`OpenCategory`、`GoHome` 和 `GoBack` 可用代码导航。请给区块一个高度，让页面在其中滚动。

### 窄栏

<Demo name="Narrow" />

## API 参考

<!-- api: HelpCenter -->

<!-- api: HelpCenterView -->

<!-- api: HelpCenterCategory -->

<!-- api: HelpCenterArticle -->

<!-- api: HelpCenterArticleEventArgs -->

<!-- api: HelpCenterFeedbackEventArgs -->

## 无障碍

- 首页标题是页面标题；主题、列表和目录都是有名称的组，每一行都是以标题和阅读时间命名的按钮。
- 搜索结果的数量和打开的文章会礼貌地读出；路径导航是一排按钮。
- 反馈提问保持焦点不动，并读出感谢。

### 键盘

| 按键 | 作用 |
|---|---|
| `/` | 聚焦搜索框（首页和结果页）。 |
| 搜索框中的 `Enter` | 打开第一条结果。 |
| `Escape` | 在文章或主题中返回上一级；在搜索框中清空内容。 |
| `Alt+←` | 返回上一级。 |

## 动效

- 页面横向滑动：深入时向前，返回时向后。
- 反馈区以短距离上移切换内容。
- 减少动效时滑动改为淡入淡出；关闭动效时页面立即切换。

## 响应式

- 宽度小于 720 px 时（`:narrow`，`NarrowWidth`），主题排成一列，目录隐藏，路径导航会换行。
- 更宽时，主题最多排成三列，目录显示在文章旁边（200 px，`TocWidth`）。

## 相关组件

- `SupportWidget`：任何页面角落里的帮助。
- `Markdown`：显示单篇文章。
- `SearchResults`：带筛选的工作区搜索页。
