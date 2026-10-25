---
title: 移动端导航栏
summary: 页面顶部 44 px 高的栏：起始槽、居中的标题和放操作的末端槽。
source-hash: 50823e765d24
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

### Token

`NavbarToken` 保存几何：`BarHeight`（44）、`BarPadding`（两侧 8）、`SlotMinWidth`（44）和 `BarBorderThickness`（栏下方 1 px 的细线）。颜色和字体来自 ArcDesign token。

## API 参考

<!-- api: MobileNavbar -->

## 无障碍

标题是纯文本；每个屏幕都应设置一个。槽里的按钮是有 44 px 触控目标的普通按钮。给返回按钮起一个说明去向的名字。

## 动效

栏本身没有动效；槽里的按钮按下与松开的表现与 `MobileButton` 相同。

## 相关组件

- `MobilePage`：把导航栏放进它的 `Header`。
- `MobileButton`：槽里常见的内容。
- `MobileTabbar`：底部栏。
