---
title: 页头
summary: 项目页头会随滚动收拢为紧凑栏，标签页的数量会滚动更新。
source-hash: f0053fa759d8
---

<Demo name="Preview" />

## 何时使用

- 项目、代码库或记录页面的顶部，用于说明页面位于何处、页面是什么、状态如何，以及该页面有哪些视图。
- 页面主体可滚动，且标题应以紧凑栏的形式保持在手边的页面。
- 记录上带有少量操作的页面，例如关注、分享，以及一个主要操作。

## 何时不用

- 应用范围内的页面之间导航：使用 `Sidebar`。
- 在卡片或面板内切换视图：使用 `Tabs`。
- 带有自身标题的对话框或设置表单：使用 `Dialog` 的标题，或普通标题。

## 用法

`PageHeader` 是整个页面的框架。它显示页头，并在其下方显示所选标签页的可滚动主体。为它设置固定高度（或将它放在一个有固定高度的行中），以便主体可以滚动。每个 `PageHeaderTab` 都有一个 `Header`、一个可选的 `Count`，以及作为 `Content` 的主体。

```xml
<arc:PageHeader Title="结账改版"
                Status="按计划进行"
                Description="围绕已保存的支付方式重做移动端结账。"
                Height="480">
    <arc:PageHeaderTab Header="概览">
        <TextBlock Text="进度、里程碑与近期动态。" />
    </arc:PageHeaderTab>
    <arc:PageHeaderTab Header="问题" Count="9">
        <TextBlock Text="未关闭的问题。" />
    </arc:PageHeaderTab>
</arc:PageHeader>
```

### 标题与标签页

页头只需要标题和标签页。没有面包屑、状态或操作时，页头行会保持空白，直到页头收拢并显示紧凑标题。

<Demo name="Minimal" />

### 面包屑、状态与元信息

`Breadcrumbs` 接受通往页面的路径，根在前。最后一项是当前页面。选择其他面包屑会引发 `BreadcrumbClick`，并附上该项及其索引。`Status` 会在标题旁添加一个带圆点的徽标，颜色由 `StatusTone` 决定（默认为成功色）。`Meta` 可接受任意内容，用于描述标题下方的那一行，例如成员、负责人和日期。其文字为特小号，且为弱化色。

```xml
<arc:PageHeader Title="结账改版" Status="有风险" StatusTone="Warning">
    <arc:PageHeader.Meta>
        <TextBlock Text="由 Emma Collins 负责" />
    </arc:PageHeader.Meta>
</arc:PageHeader>
```

```csharp
header.Breadcrumbs = new[] { "Northline", "项目", "结账改版" };
header.BreadcrumbClick += (_, e) => router.Open(e.Index);
```

### 操作

`Actions` 是页头行中的小型次要按钮。`MoreActions` 始终位于“更多操作”菜单中。`PrimaryAction` 是行末的小型主要按钮。每个 `PageHeaderAction` 都有 `Label`、`Icon`（Lucide 图形，例如 `ArcIcons.Bell`），以及 `Command` 或 `Click` 事件。`PageHeader.ActionInvoked` 在同一个事件中报告所有操作。

当面包屑区域的宽度将小于 240 像素时，次要操作会折叠进“更多操作”菜单的顶部。当宽度再次达到 272 像素时，它们会重新展开，因此页头不会在阈值处闪烁。系统只监视页头的宽度，所以文字变长的标签（例如“关注”变为“已关注”）永远不会把你刚按下的按钮收进菜单。

<Demo name="FoldingActions" />

修改 `Label` 和 `Icon` 即可原地更新按钮。当菜单行需要显示不同文字时，设置 `MenuLabel` 和 `MenuIcon`。`IsLoading` 会显示加载指示器，并忽略点击。

```csharp
follow.Click += (_, _) =>
{
    following = !following;
    follow.Label = following ? "已关注" : "关注";
    follow.MenuLabel = following ? "取消关注" : "关注";
};
share.IsLoading = true;
```

### 收拢

当主体滚动超过 16 像素时，页头会收拢为一个紧凑栏，只显示标题和状态。只有当主体回到距顶部 4 像素以内时，它才会重新展开。`IsCondensed` 表示当前状态。选择紧凑标题会调用 `ScrollToTop()`，它会滚回顶部，并把焦点移到标题上。只有当主体足够长、页头变小后仍然可以滚动时，页头才会收拢。

页头收拢时切换标签页，会显示新标签页的顶部，并保留紧凑栏。`RevealTab(index)` 在代码中做同样的事，例如在另一个标签页中新建条目之后。

### 数据项

将 `ItemsSource` 绑定到你自己的数据项。`ItemTemplate` 渲染标签页标签，`ContentTemplate` 渲染主体。`SelectedIndex` 与 `SelectedItem` 可以双向绑定。

<Demo name="DataItems" />

### 窄屏布局

布局跟随控件的宽度，而不是窗口的宽度。宽度低于 560 像素时，内边距会变小，根级面包屑和紧凑徽标隐藏，标题降低一个字号，标签页行的远端边缘会渐隐。宽度低于 460 像素时，主要操作只显示图标。

## API 参考

<!-- api: PageHeader -->

<!-- api: PageHeaderTab -->

<!-- api: PageHeaderAction -->

## 无障碍

- 该控件是一个区域，名称由 `Title` 提供。标题为二级标题。
- 面包屑是导航地标。当前页面会被标记，且不是链接。
- 标签页行由 `TabsLabel` 命名。Left 与 Right 在标签页之间移动并选中它们，到达两端时循环。Home 与 End 跳到第一个和最后一个标签页。只有选中的标签页在 Tab 键顺序中。
- “更多操作”按钮由 `MoreActionsLabel` 命名。菜单支持方向键、Home、End、输入字符跳转、Enter，以及按 Escape 关闭并将焦点返回。
- 紧凑标题的默认名称为 “Title, back to top”（`BackToTopLabel`）。页头中被隐藏的部分会离开 Tab 键顺序，并忽略指针。
- 宽度低于 460 像素时，主要按钮仍以其标签作为无障碍名称。

## 动效

- 收拢：页头的介绍区以平滑弹簧收起高度。标题缩小到 62%，朝左上角收缩，并伴随轻微模糊淡出。面包屑向上移开，紧凑标题从柔和的模糊中升起。
- 操作折叠：次要操作以平滑弹簧收起宽度并淡出。只裁切右侧边缘，因此它们会折叠到“更多操作”按钮之下。
- 标签页：指示器在变形弹簧上于标签页之间滑动。数量逐位滚动。新的主体从移动方向的一侧滑入 12 像素，旧的主体同时淡出。
- 动效等级为 Reduced 时只保留淡入淡出。高度与宽度立即变化，主体交叉淡化。等级为 Off 时，所有变化立即生效。

## 相关组件

- `Breadcrumb`：用于单独的路径。
- `Tabs`：用于卡片或面板内的视图。
- `Badge`：用于状态徽标。
- `DropdownMenu`：用于操作菜单。
