---
title: Blog grid
summary: 一个博客索引页，包含精选文章、分类筛选、文章卡片、分页和就地阅读器。
source-hash: 1f96c95cf4d9
---

<Demo name="Preview" />

## 何时使用

将 `BlogGrid` 用作博客、期刊、新闻页或资源中心的索引页，适合分类不多且有一篇精选文章的场景。

## 何时不用

- **带版本号的发布说明**：使用 `ChangelogFeed` 区块。
- **需要排序和搜索的大型归档**：使用数据表格。

## 用法

```csharp
grid.Posts = posts;
grid.Categories = ["Product", "Design", "Engineering", "Company"];
grid.HrefSelector = post => $"https://example.com/blog/{post.Id}";
grid.PostOpened += (_, e) => analytics.Track("post", e.Post.Id);
```

文章按最新优先排列。只有 `Category` 与筛选项匹配的文章才能被筛选，`Categories` 决定筛选项；“All”排在最前。

### 链接或阅读器

设置 `HrefSelector` 后，卡片会引发 `PostOpened` 并打开链接，除非处理程序将事件标记为已处理。未设置时，卡片打开就地阅读器；`ReadingPost` 表示当前打开的文章，`OpenPost` 和 `CloseReader` 可在代码中操作。

<Demo name="Links" />

### 绑定筛选和页码

`Category` 和 `Page` 支持双向绑定，应用可以把它们放进路由。选择筛选项会把页码重置为 1。

```xml
<arc:BlogGrid Category="{Binding Category}" Page="{Binding Page}" PageSize="9" ShowFeatured="False" />
```

### 窄容器

<Demo name="Narrow" />

## API 参考

<!-- api: BlogGrid -->

<!-- api: BlogPostEventArgs -->

## 无障碍

- 每张卡片是一个可聚焦的链接，名称包含标题、分类、日期和作者，只读一次。
- 筛选项和页码是切换按钮；页码按钮的名称取自 `PageLabelFormat`。
- 阅读器把焦点移到返回按钮，关闭后焦点回到卡片。封面使用 `ImageAlt`；头像是装饰性的。
- `Tab` 在筛选项、卡片和分页间移动；`Enter` 打开聚焦的文章。

## 动效

- 筛选和页码高亮沿 morph 弹簧滑动。
- 切换筛选时网格交叉淡入并上升 10 px；翻页时沿方向滑动 24 px：旧页先在 180 ms 内离开，新页再以 smooth 弹簧和 240 ms 淡入到达。高度跟随 smooth 弹簧。
- 打开文章时封面沿 morph 弹簧飞入阅读器，关闭时飞回，索引的其余部分同时淡入。阅读器正文延迟 120 ms 后上升 12 px。
- 鼠标悬停时封面在 480 ms 内放大 3%。
- 减弱动效时位移和飞行换成单纯淡入淡出。关闭动效时立即完成。

## 响应式

- 区块跟随自身宽度：900 px 以上三列，540 px 以上两列，其余一列。
- 680 px 及以下时精选文章的封面堆叠在文字上方，间距收紧。540 px 及以下时“上一页”和“下一页”收起为箭头。
- 放不下时筛选项可横向滚动。

## 相关组件

- `ChangelogFeed` 区块：带日期的发布说明。
- `Pagination`：单独使用的分页。
