---
title: 移动端工具栏
summary: 页面底部 44 px 高的操作栏：等分宽度的朴素操作，或起始、居中和末端三个槽。
source-hash: 532381e2349c
---

<Demo name="Basic" />

## 何时使用

`Toolbar` 是属于当前屏幕的操作栏，放在拇指够得到的底部。它作用于人正在看的内容，而 `Tabbar` 用于在不同区域之间切换。

- **针对单个条目的操作**：照片或文档下方的分享、编辑、收藏和删除。
- **选中之后**：选中若干行后出现“移动”“复制”“删除”，中间显示选中数量。
- **编辑器或表单**：起始处“取消”，末端“发送”。

实时示例在手机外框里运行，那是桌面上的模拟；栏在 iOS 和 Android 的主屏指示条之上如何排布，这里没有验证。外框设置的页面安全区会让它避开这些区域。

## 何时不用

- **在顶层区域之间切换**：用 `Tabbar`。
- **屏幕顶部的操作**：用 `Navbar` 的槽。
- **超过大约五个操作**：把其余的放进 `Sheet`。

## 用法

把工具栏放进页面的 `Footer`。条目是 `Variant="Plain"` 的朴素 `arcm:Button`，让栏保持安静；它们等分宽度，每个至少 44 px 高。

```xml
<arcm:Page xmlns="https://github.com/avaloniaui"
           xmlns:arcm="https://github.com/AIDotNet/TKCode.Arc.Mobile">
    <arcm:Page.Footer>
        <arcm:Toolbar>
            <arcm:Button Variant="Plain" Content="Share" />
            <arcm:Button Variant="Plain" Content="Edit" />
            <arcm:Button Variant="Plain" Content="Delete" />
        </arcm:Toolbar>
    </arcm:Page.Footer>
</arcm:Page>
```

### 槽

`Leading`、`Center` 和 `Trailing` 把内容放在栏的起始、中间和末端，而不是等分条目。

<Demo name="Slots" />

```xml
<arcm:Toolbar>
    <arcm:Toolbar.Leading>
        <arcm:Button Variant="Plain" Content="Cancel" />
    </arcm:Toolbar.Leading>
    <arcm:Toolbar.Trailing>
        <arcm:Button Variant="Plain" Content="Send" />
    </arcm:Toolbar.Trailing>
</arcm:Toolbar>
```

### 在标签栏之上，显示与隐藏

把工具栏和 `Tabbar` 放进 `Footer` 里的同一个 `StackPanel`。`IsOpen` 以滑动加淡入淡出显示和隐藏工具栏；把它绑定到“有没有可操作对象”的状态。

<Demo name="WithTabbar" />

```xml
<arcm:Page xmlns="https://github.com/avaloniaui"
           xmlns:arcm="https://github.com/AIDotNet/TKCode.Arc.Mobile">
    <arcm:Page.Footer>
        <StackPanel>
            <arcm:Toolbar IsOpen="{Binding HasSelection}">
                <arcm:Button Variant="Plain" Content="Delete" />
            </arcm:Toolbar>
            <arcm:Tabbar SelectedIndex="0" />
        </StackPanel>
    </arcm:Page.Footer>
</arcm:Page>
```

关闭的工具栏仍占着布局中的位置，上方的内容不会跳动。要移除它，把 `IsVisible` 设为 `False`。

### Token

`ToolbarToken` 保存几何：`BarHeight`（44，含发丝线）、`BarPadding`（两侧 8）和 `BarBorderThickness`（栏上方 1 px 的发丝线）。颜色和字体来自 ArcDesign token。

## API 参考

<!-- api: MobileToolbar -->

## 无障碍

每个操作都是带 44 px 触摸目标的普通按钮；只有图标的操作要给出可访问名称。关闭的工具栏不拦截触摸。

## 动效

关闭时，栏向下滑出自身的高度并淡出，用一段短的补间；打开时，在一个轻快的弹簧上滑回。栏在移动时再次切换 `IsOpen`，会从当前位置掉头。首帧没有动效，减弱动效时栏直接跳转。按钮的按下与松开和 `MobileButton` 一致。

## 相关组件

- `MobilePage`：把工具栏放进它的 `Footer`。
- `MobileButton`：通常的内容，使用 `Variant="Plain"`。
- `MobileTabbar`：在不同区域之间切换的栏，常常就在工具栏下方。
- `MobileSheet`：放不下的操作。
