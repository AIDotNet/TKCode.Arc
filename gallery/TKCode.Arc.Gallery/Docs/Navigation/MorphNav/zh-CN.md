---
title: Morph nav
summary: 网站的顶部导航栏：各个分区、分区的链接面板和搜索共用一块会变形的表面；页面滚动后变得更紧凑，宽度不够时把分区收进一个菜单。
source-hash: 35f153c896a4
---

<Demo name="Preview" />

## 何时使用

`MorphNav` 位于产品或营销网站的顶部。从左到右依次是你的品牌、几个分区、一个搜索按钮和一个号召性操作。它们都不会打开自己的窗口：
指向某个分区时，导航栏向下伸展出该分区的链接；滑到相邻分区时，同一块表面换成相邻分区的链接；搜索直接接管导航栏本身，而不是弹出一个对话框。

- **分区不多的网站**，每个分区通向若干页面，例如产品网站。
- **希望搜索始终是页头的一部分**，而不是在页面上方打开。
- **一个希望随时够得着的操作**，例如开始试用，放在导航栏的最末端。

## 何时不用

- **同一个页面的几个视图**：`Tabs` 把它们并排放着。
- **应用的顶层区域**而不是网站：`LiquidTabBar` 更合适。
- **命令而不是目的地**：交给 `DropdownMenu`。

## 用法

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:MorphNav BrandName="Lantern" ActionLabel="Get started" Current="platform">
        <arc:MorphNavItem Value="platform" Label="Platform">
            <arc:MorphNavLink Label="Notebooks" Description="Notes and photos in one place" Href="/notebooks" />
            <arc:MorphNavLink Label="Maps" Description="Pin every note to a place" Href="/maps" />
        </arc:MorphNavItem>
        <arc:MorphNavItem Value="pricing" Label="Pricing" Href="/pricing" />
        <arc:MorphNav.SearchEntries>
            <arc:MorphNavSearchEntry Label="Maps" Group="Platform" Href="/maps" />
        </arc:MorphNav.SearchEntries>
    </arc:MorphNav>
</UserControl>
```

`MorphNav` 位于 Special 包中：在 `UseArcControls()` 旁边调用 `UseArcSpecial()` 注册。在里面放 `MorphNavItem`，每个分区一个。
给某项加上 `MorphNavLink` 子项或 `Feature`，它就有了面板；两者都不加，这一项本身就是一个链接。每个链接需要 `Label` 和 `Href`，
可选 `Description` 和 `Icon`。不超过三个链接时排成一列，四个及以上分成两列；`Columns` 可以改变这一点。

链接旁边的 `Feature` 放一张卡片：`MorphNavStory` 显示图片、标题和下面一行说明，其他内容按原样显示。`Footer` 在链接下方加一行，
前面有一条细线；放在这里的 `MorphNavLink` 显示为带箭头的短链接。面板里的任何链接或按钮一被使用，面板就会关闭。

`BrandName` 和 `BrandMark` 组成导航栏打头的品牌按钮，`ActionLabel` 加上收尾的号召性操作按钮，它触发 `ActionInvoked` 并执行
`ActionCommand`。`SearchEntries` 中有条目后才显示搜索。

### 去往目的地

凡是通往某处的东西都会触发 `Navigate`：面板链接、页脚链接、特色卡片、搜索结果、没有面板的项，或品牌。`e.Label` 和 `e.Href` 描述目的地，
`e.Section` 是它所属项的 `Value`，`e.Destination` 是被选中的对象。在处理程序里完成路由，并把 `Current` 设为当前所在的分区：
该项以前景色显示，并被读作当前页面。

<Demo name="Current" />

```csharp
nav.Navigate += (_, e) =>
{
    router.Go(e.Href);
    nav.Current = e.Section;
};
```

`Open(value)`、`ClosePanel()`、`OpenSearch()` 和 `CloseSearch()` 用代码控制导航栏；`OpenValue` 和 `IsSearchOpen`
报告当前打开的内容。

### 搜索

打开搜索会把导航栏变成搜索框：各项淡出并缩小，搜索框占据同一块表面，结果出现在它下方。还没输入任何内容时，前五个条目作为建议列出；输入后，标签以查询开头的排在最前，
其次是标签中某个词以它开头的，再次是标签包含它的，最后是说明、分组和 `Keywords` 中包含它的；最多显示六个结果，结尾显示其 `Group`。
方向键移动当前结果，Enter 打开它。

### 滚动后变紧凑

`IsCompact` 借助弹簧把导航栏从 56 px 压到 46 px，并把品牌名收起来。根据页面的滚动位置设置它，读者往下读时导航栏就少占些地方。

<Demo name="TightenOnScroll" />

```csharp
page.ScrollChanged += (_, _) => nav.IsCompact = page.Offset.Y > 24;
```

### 折叠成菜单

导航本身比 `CollapseBelow`（若不修改则为 720 px）窄时，各个分区被一个 Menu 项取代。这一项的面板把每个分区的链接归到该分区的名称下，后面跟着没有面板的项。
`IsCollapsed` 表示是否已经折叠。

<Demo name="Folded" />

### 最佳实践

**建议**

- 保持三到五个分区，每个面板放少量链接。
- 链接名称本身不够清楚时，加一句简短的说明。
- 让 `Current` 与页面保持一致，导航栏就总能说明你在哪里。

**避免**

- 放多个主要操作：导航栏只容得下一个。
- 把导航放在会裁剪内容的父元素里：面板会长到导航栏下方的页面上。

## API 参考

<!-- api: MorphNav -->

## 无障碍

- 导航是一个导航地标，名称由 `Label` 指定（默认 "Main"）。
- 带面板的项报告是否已展开；普通项、链接和卡片是链接。当前项被读作当前页面。
- 打开的面板以区域的形式公开，名称取自它的项。搜索框是控制结果列表的组合框；结果上方的那一行（"Suggested"、"6 results"、"No results for …"）
  位于礼貌型实时区域中，所以屏幕阅读器会读出匹配的数量。
- 键盘焦点显示统一的焦点环，高亮跟随键盘焦点在导航栏上移动。指针操作不显示焦点环。
- 在导航以外的任何地方按下，都会关闭打开的面板或搜索；焦点移出导航时，面板也会关闭。

### 键盘交互

| 按键 | 操作 |
|---|---|
| Left / Right | 沿着各项逐个移动，到两端时循环；如果有面板打开，它会换成新获得焦点的项的面板。 |
| Home / End | 移到第一项或最后一项。 |
| 在带面板的项上按 Down、Enter 或 Space | 显示面板并把焦点放在最上面的链接；面板已经显示时，把焦点移进面板。 |
| 面板中的 Down / Up | 逐个经过面板的链接；在最上面的链接上按 Up 回到该项。 |
| 搜索中的 Down / Up | 改变高亮的结果。 |
| 搜索中的 Enter | 前往高亮的结果。 |
| Escape | 关闭打开的面板或搜索，并把焦点放回打开它的控件上。 |

## 动效

- 弹簧调整表面大小以适应每个面板：变大时略微越过，变小时不越过、直接停稳。与此同时，圆角从导航栏的胶囊形变为面板的圆角，阴影加深。
- 从导航栏打开的第一个面板略微落到位，同时从模糊变清晰。切换到另一个分区时内容横向移动：新面板从新项所在的一侧进入，旧面板向另一侧退出并淡出。高亮沿导航栏滑到打开的项，否则滑到指针下或键盘焦点所在的项。
- 悬停 60 ms 后面板才打开，指针离开导航后面板再停留 180 ms，所以指针经过空隙时面板不会一闪而合。只有鼠标会悬停；触摸时，点按打开或关闭面板。
- 打开搜索时，导航栏的内容模糊、缩小并淡出，搜索框在同一块表面里展开。打开或关闭紧凑模式时，导航栏在弹簧上改变大小。

| 变化 | 完整 | 减弱 | 关闭 |
|---|---|---|---|
| 表面大小和圆角 | 弹簧 | 跳变 | 跳变 |
| 面板和搜索 | 淡入、模糊和位移 | 短暂淡入淡出 | 直接切换 |
| 高亮 | 滑动 | 跳变 | 跳变 |
| 紧凑模式 | 弹簧 | 跳变，品牌名淡出 | 跳变 |

## 响应式

- 面板最宽 700 px，搜索最宽 544 px，两者都不会比导航宽度减 16 px 更宽。
- 导航比 `CollapseBelow`（720 px）窄时，分区换成一个菜单，品牌只显示标志、不显示名称。
- 窗口宽度小于 420 px 时，链接面板只用一列，折叠菜单省略图标。

## 相关组件

- `Tabs` 用于同一页面内的视图。
- `LiquidTabBar` 用于应用的主要区域。
- `DropdownMenu` 用于命令列表。

| 组件 | 适用场景 |
|---|---|
| `MorphNav` | 站点顶栏需要带面板的分区、搜索和一个操作 |
| `Tabs` | 一个页面的几个视图需要一直可见 |
| `LiquidTabBar` | 应用有几个主要区域需要来回切换 |
