---
title: Site header
summary: 粘性网站页眉，滚动时变为实心，带有滑动的当前链接标记、Mega 菜单面板和移动端菜单抽屉。
source-hash: 2f5e4352dce1
---

<Demo name="Preview" />

## 何时使用

`SiteHeader` 是营销网站或文档网站的顶部栏。它承载品牌标识、主要链接和一到两个操作，在读者滚动页面时保持在页面顶部；当菜单太大、无法放在一行时，它会打开一个分组链接面板。

- **包含多个章节的产品网站**：Product 和 Resources 打开带有描述性链接和功能卡片的面板；Pricing 和 Customers 是普通链接。
- **文档网站**：`Simple` 布局在品牌旁边放三到四个链接，当前章节由其下方的一条线标示。
- **链接较少、外观简洁的网站**：`Centered` 布局把链接放在中间的一个安静胶囊中，当前链接显示为凸起的药丸形。
- **较窄的宿主**：低于 `CollapseWidth`（默认 760）时，链接会折叠进菜单抽屉。该区块跟随自身的宽度，因此即使窗口很宽，放在窄面板中的页眉也会折叠。

## 何时不用

- **应用内部的导航**：请使用 `Sidebar` 或 `Tabs`。页眉用于读者进入的页面，而不是在已经在用的工具之间切换。
- **带面包屑和操作的页面标题**：请使用 `PageHeader`。
- **单一的命令菜单**：请使用 `DropdownMenu`。

## 用法

```xml
<ScrollViewer>
    <StackPanel>
        <arc:SiteHeader Current="product" />
        <!-- the page -->
    </StackPanel>
</ScrollViewer>
```

没有 items 时，页眉显示 Arc 的示例集（Product、Resources、Pricing、Customers），因此裸用的 `SiteHeader` 看起来与 Arc 预览一致。请将页眉放在它应当吸附的 `ScrollViewer` 内部；当它覆盖在某个 `ScrollViewer` 之上时，请设置 `ScrollSource`。Avalonia 没有 `position: sticky`：页眉跟随滚动偏移量，并保持在视口顶部。页面内容可以从页眉下方开始（使用等于页眉高度的负上边距），这样首屏的大图就会滑到页眉下面。

### 条目、链接和功能卡片

```xml
<arc:SiteHeader>
    <arc:SiteHeaderItem Value="guides" Label="Guides">
        <arc:SiteHeaderLink Label="Quick start" Description="Install and run in five minutes" Href="https://example.com/start" />
        <arc:SiteHeaderLink Label="Theming" Description="Tune color, radius and motion" />
        <arc:SiteHeaderItem.Feature>
            <arc:SiteHeaderFeature Title="What's new" Description="The spring release." Href="https://example.com/new" />
        </arc:SiteHeaderItem.Feature>
    </arc:SiteHeaderItem>
    <arc:SiteHeaderItem Value="pricing" Label="Pricing" Href="https://example.com/pricing" />
</arc:SiteHeader>
```

`Items` 是一个 `AvaloniaList<SiteHeaderItem>`；设置了 `ItemsSource` 时它优先生效，并且会监听其集合变化通知。带有 `Links` 的条目在 `Mega` 变体中会打开一个面板，在其他变体中则是普通链接。`Value` 是条目的稳定名称：`Current` 和 `SiteHeaderNavigateEventArgs.Section` 都使用它。`SiteHeader.CreateExampleItems()` 返回 Arc 的示例条目，可作为起点。功能卡片的 `Image` 可以是任意 `IImage`；它以 16:10 显示并裁剪以填满卡片，`ImageDescription` 描述它所显示的内容。没有图片时，卡片只显示文字。

<Demo name="Simple" />

### 选择目的地

每个目的地（条目、面板中的链接、功能卡片、品牌标识、操作）都会冒泡触发 `Navigate` 事件，并以相同的 `SiteHeaderNavigateEventArgs` 执行 `NavigateCommand`。`Kind` 说明选中的是什么，`Section` 指明它所属的条目，`Link`、`Feature` 或 `Item` 携带数据对象。选中条目内的内容还会把 `Current` 设为该条目，并关闭面板和菜单抽屉。当没有人设置 `Handled`，且目的地具有绝对的 http、https 或 mailto `Href` 时，页眉会用平台的启动器打开它。

```csharp
header.Navigate += (_, e) =>
{
    router.GoTo(e.Href ?? e.Label);
    e.Handled = true; // do not launch the address
};
```

<Demo name="Events" />

### 滚动与实心状态

页眉在页面顶部时是透明的。滚动偏移量超过 `ScrollThreshold`（8）后，它变为实心：有背景、底部边框和静止阴影。只有当偏移量降到阈值的一半以下时，它才会重新变为透明，因此在边缘不会闪烁。`IsScrolled` 报告当前状态。面板或菜单抽屉打开时，页眉也保持实心。将 `IsSticky` 设为 false，可让页眉随页面一起滚走。

### Mega 菜单面板

用鼠标悬停在触发器上时，经过 70 ms 会打开其面板；移动到另一个触发器时会立即切换。指针离开页眉和面板后，面板在 180 ms 后关闭。点击触发器会切换其面板的开合。触摸和笔的点按不会通过悬停打开面板。面板的高度会以弹簧过渡到当前内容的高度，增大和减小使用不同的弹簧。当你移到相邻条目时，新内容会从你移动的方向滑入，旧内容则滑出。可在代码中使用 `OpenPanel(value)` 和 `ClosePanel()`；`OpenItemValue` 报告当前打开的条目。

### 窄布局与菜单抽屉

低于 `CollapseWidth` 时，链接、次要操作和面板会让位给一个菜单按钮。菜单抽屉从页眉向下展开：分组一次只展开一个，普通链接排成行，操作放在最后，并分为两列等宽的按钮。遮罩覆盖页面，按下遮罩会关闭抽屉；抽屉打开时页面不滚动。可在代码中使用 `OpenMenu()`、`CloseMenu()` 以及双向的 `IsMenuOpen`。

<Demo name="Narrow" />

### 内容指南

- 页眉保持四到五个链接，其余的移入面板。
- 对面板中的链接，用一句简短的话说明其中有什么，而不是说它是一个链接。
- 一个主要操作。次要操作面向已有账户的人。

## API 参考

<!-- api: SiteHeader -->

<!-- api: SiteHeaderItem -->

<!-- api: SiteHeaderLink -->

<!-- api: SiteHeaderFeature -->

<!-- api: SiteHeaderNavigateEventArgs -->

## 无障碍

- 链接是以 `Label` 命名的导航地标。菜单抽屉是第二个导航地标，名称相同。
- 触发器会报告其面板是否展开；当前链接会报告自己处于选中状态。面板是以其条目命名的区域。菜单按钮的名称会在 `OpenMenuLabel` 和 `CloseMenuLabel` 之间切换，并报告抽屉是否打开；分组行会报告是否展开。
- 键盘焦点显示 2 px 的焦点环；指针焦点不显示。悬停效果仅适用于鼠标或笔。
- 每段文字都是一个属性，因此本地化的网站可以替换 "Get Arc"、"Sign in"、菜单标签和地标名称。

### 键盘

| 按键 | 操作 |
|---|---|
| `Tab` / `Shift+Tab` | 按顺序在品牌、链接、操作和菜单按钮之间移动 |
| `Left` / `Right` | 在链接中：将焦点移到上一个或下一个链接，到两端时循环；面板打开时，面板会跟随到新链接（若是普通链接则关闭面板） |
| 在触发器上按 `Down` | 打开其面板，并将焦点移到第一个链接 |
| 在面板中按 `Up` / `Down` / `Home` / `End` | 在面板的链接与功能卡片之间移动；在第一项上按 `Up` 会关闭面板并回到触发器 |
| `Enter` / `Space` | 选择聚焦的链接，或切换触发器或菜单分组 |
| `Escape` | 关闭面板并回到其触发器；否则关闭菜单抽屉并回到菜单按钮 |

## 动效

| 变化 | 完整动效 | 减少动效 | 关闭动效 |
|---|---|---|---|
| 页眉变为实心 | 背景、边框和阴影在 240 ms 内淡入 | 立即 | 立即 |
| 悬停块 | 出现在第一个链接上，在链接之间滑动，指针离开时消失 | 立即 | 立即 |
| 当前标记 | 在弹簧上滑动到不同的链接之间 | 立即 | 立即 |
| 面板打开 | 在 160 ms 内淡入，同时从上方 6 px 处轻微缩放着就位 | 出现 | 出现 |
| 面板关闭 | 在 180 ms 内淡出，同时向上移动 4 px | 120 ms 淡出 | 立即 |
| 面板高度 | 以弹簧过渡到内容的高度；增大和减小使用不同的弹簧 | 立即 | 立即 |
| 面板内容变化 | 新内容在短暂模糊中滑入 36 px；旧内容滑出，并在 120 ms 内淡入 | 淡入淡出 | 立即 |
| 菜单图标 | 菜单与关闭图标以四分之一圈的转动和轻微缩放切换 | 淡入淡出 | 立即 |
| 菜单抽屉 | 以弹簧从页眉落下；各行依次升起；分组以弹簧展开 | 立即 | 立即 |

## 相关组件

- `PageHeader`，用于应用页面的标题区块。
- `Sidebar`，用于应用内部的导航。
- 站点页脚，用于营销页面底部的对应部分。
