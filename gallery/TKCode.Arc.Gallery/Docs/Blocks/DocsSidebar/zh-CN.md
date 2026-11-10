---
title: 文档侧边栏
summary: 文档站点的侧边栏：版本切换、边输入边筛选页面的搜索框、可展开的分组与页面，当前页面会被标出并滚动到可见处。
source-hash: 57ef5deb9eb8
---

<Demo name="Preview" />

## 何时使用

`DocsSidebar` 是文档站点或应用内帮助中心的左侧栏。顶部是产品名称和一个用来切换文档版本的小按钮。下面是搜索框，读者输入时列表随之缩小。页面按带标题的分组排列，分组可以折叠，一个页面也可以展开出子页面。正在阅读的页面会被标出；无论它因点击还是因你的路由而改变，所在的分组和上级页面都会展开，列表也会滚动让它保持可见。它基于 `Sidebar` 构建，因此共用其选中标记、键盘模型和筛选功能。

- **产品文档**：包含指南、API 参考和更新日志，读者会在许多页面之间跳转。
- **分版本的文档**：使用旧版本的读者需要看到对应的页面。
- **应用内的帮助中心**：当前文章由链接决定，侧边栏需要跟随。

## 何时不用

- **应用的主导航**：使用 `Sidebar`，它带图标并可收起为图标栏。
- **邮箱**：使用 `InboxSidebar`，它增加了写邮件、计数和标签。
- **一篇长文的各级标题**：在文章旁放一份锚点目录比页面列表更好读。

## 用法

```csharp
docs.Sections.Add(new DocsSidebarSection("Guides",
    new DocsSidebarPage("guides/caching", "Caching", "New"),
    new DocsSidebarPage("guides/deploying", "Deploying")));
docs.CurrentKey = "guides/caching";
docs.PageInvoked += (_, e) => router.Open(e.Page.Key);
docs.VersionChanged += (_, e) => LoadPages(e.NewVersion);
```

页面和分组也可以直接写在 XAML 中。没有 `Sections` 时，侧边栏显示一个虚构的示例站点。`GetAdjacentPage(-1)` 和 `GetAdjacentPage(1)` 按阅读顺序给出上一页和下一页，可用于文章底部的链接。

<Demo name="OwnPages" />

## API 参考

<!-- api: DocsSidebar -->

## 无障碍

- 侧边栏是名为“Documentation”的导航区域。方向键在行之间移动，Enter 或空格打开页面，上级页面会展开或收起其子页面。
- 在窗口任意位置按“/”或 Ctrl+K（Cmd+K）都会把光标放进搜索框。可用 `IsSearchShortcutEnabled` 关闭。
- 版本切换是一个名为“Version v3.2”的按钮，打开一个带单选项的菜单。

## 动效

- 选中标记在行之间滑动，分组以高度弹簧折叠（来自 `Sidebar`）。
- 显示当前页面时，列表会在行就位后滚动一次，并在展开的分组稳定后再滚动一次。
- 减少动效时保留折叠和标记，但不用弹簧；关闭动效时一切立即改变。

## 响应式

- 侧边栏保持 `Sidebar` 的宽度（240 px）并占满给定的高度；列表自行滚动。
- 过长的页面标题以省略号结尾。窗口较窄时，可把侧边栏放进 `Drawer`。

## 相关组件

- `Sidebar`：本区块的基础。
- `InboxSidebar` 用于邮箱，`SearchResults` 用于完整的搜索页面。
- `Breadcrumb` 用于显示当前页面所在的位置。
