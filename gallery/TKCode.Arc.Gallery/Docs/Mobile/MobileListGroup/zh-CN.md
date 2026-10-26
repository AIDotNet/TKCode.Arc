---
title: 移动端列表
summary: 分组的行，用于设置和菜单：图标、标题、副标题、行尾的值或控件，以及箭头。
source-hash: 4ce9e55c4288
---

<Demo name="Basic" />

## 何时使用

`ListGroup` 和 `ListItem` 排出一屏的行：设置、菜单和账户页，每一行都是可以点按的标题。

- **设置**：把相关的行放在一个简短的 `Header` 下，用 `Footer` 作说明。
- **目的地菜单**：带 `Icon` 和箭头、会打开其他页面的行。
- **行尾的值或开关**：放进 `Trailing`。

实时示例在手机外框里运行，那是桌面上的模拟；真机上的触摸反馈这里没有验证。

## 何时不用

- **很长或无限的数据**：应该用虚拟化列表，这个还没有做。
- **带图片和多个操作的卡片**：自己排版。
- **桌面菜单**：用桌面的菜单控件；这里的行没有悬停状态。

## 用法

把 `ListItem` 放进 `ListGroup`，再把组放进可滚动的 `Page`。项的内容就是它的标题。`ListItem` 是一个按钮，给它 `Command` 或处理 `Click`；手指按在行上时，行会变暗。

```xml
<arcm:Page xmlns="https://github.com/avaloniaui"
           xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
           xmlns:arc="https://github.com/AIDotNet/TKCode.Arc"
           xmlns:arcm="https://github.com/AIDotNet/TKCode.Arc.Mobile"
           IsScrollable="True">
    <arcm:ListGroup Header="账户">
        <arcm:ListItem Icon="{x:Static arc:ArcIcons.User}" Content="个人资料" Subtitle="Ada Lovelace" ShowChevron="True" />
        <arcm:ListItem Content="每周摘要" Trailing="周日" ShowChevron="True" />
    </arcm:ListGroup>
</arcm:Page>
```

一组的最后一行下面没有分隔线。

### Token

`ListItemToken` 保存行的几何：`RowHeight`（48）、`ContentPadding`（16 乘 10）、`IconExtent`（22）、`IconGap`（12）、`TrailingMargin`、`ChevronExtent`（16）和 `SeparatorInset`（16）。`ListGroupToken` 保存组的几何：`GroupPadding`（16 乘 8）、`CardBorderThickness`（1）、`HeaderMargin` 和 `FooterMargin`。颜色、圆角和字体来自 ArcDesign token。

## API 参考

<!-- api: MobileListGroup -->

<!-- api: MobileListItem -->

## 无障碍

一行是一个按钮，名称来自标题和副标题。每一行都要有标题，图标不能是它唯一的名称。每一行至少 48 px 高，高于 44 px 的触摸目标。

## 动效

手指按在行上时，背景按快速曲线变暗；减弱动效时，立即变暗。

## 相关组件

- `MobilePage`：可滚动的页面承载这些组。
- `MobileSwitch`：设置项常用的 `Trailing` 控件。
- `MobileNavbar`：顶栏，根设置页可用大标题。
