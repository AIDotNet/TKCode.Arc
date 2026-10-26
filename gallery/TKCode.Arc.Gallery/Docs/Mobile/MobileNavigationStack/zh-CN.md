---
title: 移动端导航栈
summary: 页面栈：推入的页面从右侧滑入盖住下面的页面，从左边缘滑动或点返回键即可滑走。
source-hash: 6309420b3338
---

<Demo name="Basic" />

## 何时使用

`NavigationStack` 是移动应用向更深一层的方式：从列表到详情，从设置到子页面，再返回。

- **逐层深入**：每点一行就推入一个页面来说明它。
- **只有一条返回路径的流程**，例如表单的几个步骤。
- **滑动返回**：用户习惯从左边缘把页面拉回来，页面会跟随手指。

实时示例在手机外框里运行，那是桌面上的模拟；滑动和滑入的手感在真机上这里没有验证。

## 何时不用

- **在顶层区域之间切换**：用 `Tabbar`；每个区域可以有自己的栈。
- **盖在当前屏幕上的简短任务**：用 `Sheet`，它不会离开当前页面。
- **并排的两个窗格**：栈一次只显示一个页面。

## 用法

把第一个页面放进栈里，之后用代码 `Push` 页面。页面可以是任何控件，通常是带 `Navbar` 的 `Page`。把导航栏的 `BackCommand` 绑定到栈的 `BackCommand`：它执行 `Pop`，在第一页时不可用。

```xml
<arcm:NavigationStack xmlns="https://github.com/avaloniaui"
                      xmlns:arcm="https://github.com/AIDotNet/TKCode.Arc.Mobile"
                      Name="Stack">
    <arcm:Page>
        <arcm:Page.Header>
            <arcm:Navbar Title="收件箱" />
        </arcm:Page.Header>
        <!-- 第一个页面 -->
    </arcm:Page>
</arcm:NavigationStack>
```

```csharp
stack.Push(new Page
{
    Header = new Navbar { Title = "Ada", ShowBack = true, BackText = "收件箱", BackCommand = stack.BackCommand },
    Content = detail,
});
```

推入时新页面从右侧滑入，下面的页面向左移动三分之一的宽度并变暗。`Pop` 反过来，`PopToRoot` 回到第一个页面，只有顶部的页面滑走。在另一次滑动进行中又收到推入或弹出时，会立即完成前一次。

栈静止时，只有顶部的页面在可视树里。下面的页面保持状态（例如滚动位置），回来时原样不变。

### 每个标签一个栈

标签栏下面通常是每个标签各自的栈。为每个标签保留一个 `NavigationStack`，选择变化时替换页面的 `Content`：每个标签都会保留它推入过的页面，标签栏在每个页面下面一直可见。

<Demo name="TabsWithStacks" />

### 滑动返回

从左边缘 24 px 内开始的水平拖动会拉走顶部页面，一比一跟随手指，下面的页面一边移动一边变亮。松手时，栈按手指的速度向前看 0.2 秒：过了中点页面就离开，向右的快速滑动超过 500 px/s 时无论从哪里都会弹出；否则页面弹回。对不能被滑动离开的页面，把 `IsSwipeBackEnabled` 设为 `False`。

`Navigated` 事件在栈变化时立即报告每一次推入和弹出，此时滑动可能还在进行。

### Token

`NavigationStackToken` 保存 `DimColor`：顶部页面还没移动时盖在下面页面上的暗层，明暗模式不同。视差（宽度的 0.3）和边缘宽度（24）是控件的常量 `ParallaxFraction` 和 `EdgeWidth`。

## API 参考

<!-- api: MobileNavigationStack -->

## 无障碍

让导航栏里的返回按钮保持可见，名称说明它回到哪里，这样滑动不会是唯一的返回方式。只有顶部页面在树里，所以读屏器和键盘不会碰到藏在下面的内容。

## 动效

滑动是代码驱动的运动值：拖动时每次移动都会写入它，松手时手指的速度交给平滑弹簧，这是主题里声明的过渡做不到的。下面页面的暗层和位移跟随顶部页面的位置。减弱动效时，页面直接切换。

## 相关组件

- `MobileNavbar`：它的 `BackCommand` 绑定到栈的 `BackCommand`。
- `MobilePage`：常用的页面。
- `MobileSheet`：适合留在当前页面上的简短任务。
