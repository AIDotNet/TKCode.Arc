---
title: 搜索结果
summary: 跨多种内容类型搜索，提供带实时数量的分面标签页，结果按类型分组并高亮匹配内容，支持键盘导航。
source-hash: ca0d8f0e89c8
---

<Demo name="Preview" />

## 何时使用

- 工作区、帮助中心或管理工具中的搜索页面，结果分为多种类型。
- 用户需要把结果缩小到某一类，例如文档、问题、文件或人员，并查看每一类的数量。
- 结果需要一些上下文：条目所在的位置、匹配处前后的文字，以及最后更新时间等简短备注。

## 何时不用

- 在应用任意位置跳转到页面或执行命令：使用命令面板区块。
- 页头或工具栏中的紧凑搜索：使用 `ExpandingSearch`。
- 过滤页面上已有的列表：在列表旁使用 `SearchField`。

## 用法

为控件设置项和查询词。查询中的每个词都必须与某一项的标题、摘录、位置或关键词相符。分面栏列出“全部”和每一种类型，并附上各自的结果数，列表则按类型对结果分组。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:SearchResults Height="480" Query="发票" Label="搜索财务">
        <arc:SearchResultItem Title="发票模板"
                              Kind="文档"
                              Location="财务 / 模板"
                              Meta="1 周前更新"
                              Snippet="每位客户的发票都使用此模板。"
                              Icon="{x:Static arc:ArcIcons.FileText}" />
        <arc:SearchResultItem Title="发票合计与报表不一致"
                              Kind="问题"
                              Location="FIN-88 · 报表"
                              Meta="待处理"
                              Icon="{x:Static arc:ArcIcons.CircleDot}" />
    </arc:SearchResults>
</UserControl>
```

### 项

直接在标记中添加 `SearchResultItem` 对象，或将 `ItemsSource` 设置为一个列表。当列表是可观察集合时，结果会跟随其变化。`Kind` 决定所属的分面和分组；没有类型的项会列在 `OtherLabel` 之下。`Icon` 接受 Lucide 图形或任意控件，例如用于人员的 `Avatar`。`Keywords` 是额外的关键词，同样可以搜到该项；`Tag` 可以是任意你希望取回的值。

```csharp
search.ItemsSource = new ObservableCollection<SearchResultItem>
{
    new() { Id = "q3", Title = "Q3 规划笔记", Kind = "文档", Location = "产品 / 规划", Snippet = notes },
    new() { Id = "fn-482", Title = "睡眠后同步停滞", Kind = "问题", Keywords = ["FN-482"] },
    new() { Id = "maya", Title = "Maya Lindqvist", Kind = "人员", Icon = new Avatar { DisplayName = "Maya Lindqvist", Size = AvatarSize.Small } }
};
```

如果原地修改了某一项，请调用 `Refresh()` 重新搜索。

### 匹配、排序与高亮

标题以第一个词开头的结果排在最前，其次是标题中某个词以它开头的结果，再次是标题包含它的结果，其余结果排在最后。排名相同的结果保持项的原有顺序。标题和摘录中的每处匹配都会被高亮。较长的摘录会截取为一段范围，该范围从第一处匹配之前稍早的位置开始，被截断的两端都会显示省略号。

### 分面与分组

`SelectedKind` 是当前分面，为 null 时表示“全部”。它是双向的，因此可以绑定到路由或视图模型。在“全部”下，每种类型会显示前 `GroupPreviewCount` 条结果，以及一个打开该分面的“查看全部”链接。单一分面会显示 `PageSize` 条结果和一个“显示更多”按钮。对于当前查询没有结果的分面，会保留在原位并以变暗的文字显示。

<Demo name="FacetsAndEvents" />

### 打开结果

按 Enter 或点击即可打开结果：会触发 `ItemInvoked`，并以该项为参数执行 `InvokeCommand`。`ActiveItem` 是键盘光标所在的结果。`IsEnabled` 设为 false 的结果会变暗，光标会跳过它，且无法打开。

```csharp
search.ItemInvoked += (_, e) => Navigate(e.Item.Id);
```

```xml
<arc:SearchResults ItemsSource="{Binding Results}"
                   Query="{Binding Query}"
                   SelectedKind="{Binding Kind}"
                   InvokeCommand="{Binding OpenCommand}" />
```

### 服务端搜索

当搜索由服务端完成时，将 `IsFilteringEnabled` 设为 false。此时项会按你提供的顺序显示，并按类型计数，查询中的词仍会被高亮。请求进行期间设置 `IsLoading`：骨架行会代替结果显示，并保持原有高度。

<Demo name="ServerSearch" />

### 无结果

没有任何匹配时，空状态会显示查询内容。如果当前分面为空而其他分面不为空，它会指出结果最多的类型，并提供“搜索全部”。`Suggestions` 用于添加推荐的查询；选择其中一项会将其填入搜索框。

<Demo name="NoResults" />

### 窄屏布局

宽度小于 560 像素时，外框会收紧，每个结果会把备注移到位置行中。分面栏在其轨道内滚动，并在仍有分面被隐藏的一侧边缘渐隐。

<Demo name="Narrow" />

### 文本

每段文字都有英文默认值，并可通过一个属性修改。格式字符串使用 `{0}` 表示数量或查询内容。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:SearchResults Label="搜索帮助中心"
                       PlaceholderText="搜索文章"
                       AllLabel="全部"
                       ResultsFormat="{}{0} 篇文章"
                       OneResultText="1 篇文章"
                       NoResultsText="未找到文章"
                       EmptyTitleFormat="没有找到“{0}”"
                       SuggestionsLabel="试试搜索" />
</UserControl>
```

## API 参考

<!-- api: SearchResults -->

<!-- api: SearchResultItem -->

## 无障碍

- 该控件是搜索地标，名称由 `Label` 提供，同时也为搜索框命名。
- 分面是一个标签页列表。只有选中的分面可通过 Tab 键获得焦点。ArrowLeft 与 ArrowRight 在分面之间移动并选中它们，Home 与 End 跳到第一个和最后一个。每个分面的无障碍名称包含其类型和数量。
- 结果是一个列表，光标所在的结果即为列表中的选中项。每个结果的无障碍名称包含标题、类型和位置。分组标题为三级标题。
- 在搜索框中，ArrowDown 移入结果，Enter 打开光标所在的结果，若没有光标则打开第一个结果。在结果中，ArrowUp、ArrowDown、Home、End、PageUp 与 PageDown 移动光标；在第一个结果上按 ArrowUp 返回搜索框；Enter 或 Space 打开结果，Escape 返回搜索框。
- 在搜索框中按 Escape 会清空查询。当焦点位于控件内时，Ctrl+F（macOS 上为 Cmd+F）或 `/` 会将焦点移到搜索框；在结果中输入则会在搜索框中继续输入查询。
- 统计行是礼貌式实时区域，结果数量变化时会被播报。

## 动效

- 光标在结果之间滑动：跟随指针时使用弹簧动画，跟随方向键时使用 70 毫秒的补间动画。光标首次出现时淡入，被清除时淡出。
- 新结果淡入并上移 4 像素，每个结果相隔 35 毫秒。已显示的结果保持不动。
- 分面数量滚动到新值，选中分面的区块在变形弹簧上于各分面之间滑动。
- 统计文字交叉淡化，结果、骨架行与空状态之间相互淡入淡出。
- 动效等级为 Reduced 时保留淡入淡出，取消所有位移：光标与分面区块直接跳转，数量立即变化，结果仅淡入。动效等级为 Off 时，所有变化立即生效。

## 相关组件

- 命令面板区块：用于在应用任意位置执行操作和导航。
- `ExpandingSearch`：用于页头中的紧凑搜索。
- `SearchField`：用于过滤页面上的列表。
- `EmptyState` 与 `Skeleton`：本区块分别用于无结果和加载状态。
