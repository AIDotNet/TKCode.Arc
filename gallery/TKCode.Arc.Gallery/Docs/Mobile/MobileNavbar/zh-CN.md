---
title: 移动端导航栏
summary: 页面顶部 44 px 高的栏：起始槽、居中的标题和放操作的末端槽。
source-hash: bd5a14d5e8d1
---

<Demo name="Basic" />

## 何时使用

`Navbar` 是移动屏幕的顶部栏。它显示使用者所在的位置（`Title`），并提供返回的入口（`Leading`）和本屏的操作（`Trailing`）。

- **推入的屏幕**：`Leading` 放返回按钮，标题是屏幕名称，`Trailing` 放"完成"或"编辑"按钮。
- **只有一个操作的根屏幕**：只有标题和末端的一个按钮。
- **纯标题栏**：只有标题，空的槽让它保持居中。

实时示例运行在手机框中，这是桌面上的模拟。该栏在 Android 和 iOS 真机的状态栏或刘海之下如何呈现尚未验证；这里由手机框设置的页面安全区让它避开这些区域。

## 何时不用

- **顶层区域之间的导航**：使用 `Tabbar`，它停在拇指够得到的底部。
- **带菜单和搜索的桌面页头**：使用桌面的页头区块和控件。
- **操作很多的工具栏**：导航栏两端各只容得下一两个触控目标。

## 用法

把导航栏放进页面的 `Header`。两个槽可以放任何内容，通常是 `arcm:Button`，并保持至少 44 的宽度，让按钮易于点按。

```xml
<arcm:Page xmlns="https://github.com/avaloniaui"
           xmlns:arcm="https://github.com/AIDotNet/TKCode.Arc.Mobile">
    <arcm:Page.Header>
        <arcm:Navbar Title="Settings">
            <arcm:Navbar.Leading>
                <arcm:Button Content="Back" />
            </arcm:Navbar.Leading>
            <arcm:Navbar.Trailing>
                <arcm:Button Content="Done" />
            </arcm:Navbar.Trailing>
        </arcm:Navbar>
    </arcm:Page.Header>
</arcm:Page>
```

### 仅标题

槽里没有内容时，标题保持居中。

<Demo name="TitleOnly" />

### 返回按钮

设置 `ShowBack` 会在 `Leading` 之前显示一个朴素的返回按钮（箭头加 `BackText`，通常是上一页的标题），用 `BackCommand` 指定动作。`Trailing` 里的操作建议用 `Variant="Plain"`，让标题保持视觉焦点。

```xml
<arcm:Navbar Title="设置" ShowBack="True" BackText="首页" BackCommand="{Binding GoBack}" />
```


### 大标题

在根页面设置 `LargeTitle`，标题会在栏下方再以大字显示一次。把导航栏放在 `IsScrollable="True"` 的 `Page` 的 `Header` 里，内容滚动时它会自己折叠。不在可滚动页面里时，请自己用滚动偏移量除以 `LargeTitleExtent`（52）来驱动 `CollapseProgress`（0 到 1）：大标题行逐渐收起，栏里的标题同时淡入，所以标题不会同时以大字出现两次。

<Demo name="LargeTitle" />

```xml
<arcm:Page IsScrollable="True">
    <arcm:Page.Header>
        <arcm:Navbar Title="照片" LargeTitle="True" />
    </arcm:Page.Header>
    <!-- 较长的内容 -->
</arcm:Page>
```

### Token

`NavbarToken` 保存几何：`BarHeight`（44，含细线）、`BarPadding`（两侧 8）、`SlotMinWidth`（44）、`BarBorderThickness`（栏下方 1 px 的细线）、`LargeTitleFontSize`（30）和 `LargeTitleMargin`。颜色和字体来自 ArcDesign token。

## API 参考

<!-- api: MobileNavbar -->

## 无障碍

标题是纯文本；每个屏幕都应设置一个。槽里的按钮是有 44 px 触控目标的普通按钮。给返回按钮起一个说明去向的名字。

## 动效

标题变化时，新标题从轻微模糊中升起，旧标题上移淡出（P2，`ArcContentSwap` 模糊交叉淡化）；第一个标题和减少动效时不播放。大标题一比一跟随滚动偏移，自身没有动画。槽里的按钮按下与松开的表现与 `MobileButton` 相同。

## 相关组件

- `MobilePage`：把导航栏放进它的 `Header`。
- `MobileButton`：槽里常见的内容。
- `MobileTabbar`：底部栏。
