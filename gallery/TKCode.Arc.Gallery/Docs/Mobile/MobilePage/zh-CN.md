---
title: 移动端页面
summary: 手机屏幕：页头、内容和页脚，排布在设备的安全区之内。
source-hash: 9c9b9e57e9a2
---

<Demo name="Basic" />

## 何时使用

`Page` 是手机或平板应用中一个屏幕的根。它提供屏幕背景，让内容避开状态栏、刘海和主屏指示条，并在内容上方放置可选的页头（通常是 `Navbar`），下方放置可选的页脚（通常是 `Tabbar`）。

- **移动应用的一个屏幕**：收件箱、设置列表或详情页，顶部是导航栏，底部是标签栏。
- **铺满的屏幕**：只有内容的欢迎页或引导页，背景仍延伸到状态栏之下。
- **需要响应设备安全区的屏幕**：页面会自行跟随平台的安全区边距，也可以使用你设置的值。

本页的实时示例运行在手机框中：这是桌面上的模拟，带有设备切换、竖屏与横屏、模拟的状态栏和安全区，以及键盘占位。它不是真机，控件在 Android 和 iOS 上的外观与行为（包括真实的软键盘）尚未验证。

## 何时不用

- **桌面窗口或网页式布局**：使用桌面控件（`TKCode.Arc`）和普通布局面板。平台没有报告安全区时，页面的安全区为零。
- **屏幕内部的容器**：使用 `Card` 或布局面板。一个屏幕只有一个 `Page`。
- **盖在屏幕上的模态或面板**：使用对话框或抽屉；页面是它们下面的屏幕。

## 用法

设置 `Header`、内容和 `Footer`。移动端控件有自己的 XAML 命名空间 `https://github.com/AIDotNet/TKCode.Arc.Mobile`，通常用前缀 `arcm`，因此可以与同名的桌面控件并存。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc"
             xmlns:arcm="https://github.com/AIDotNet/TKCode.Arc.Mobile">
    <arcm:Page>
        <arcm:Page.Header>
            <arcm:Navbar Title="Inbox" />
        </arcm:Page.Header>
        <TextBlock Margin="16" Text="Hello" />
    </arcm:Page>
</UserControl>
```

在 `UseArcCore()` 或 `UseArcControls()` 旁边用 `UseArcMobile()` 注册这些控件。

### 安全区

`SafeArea` 是页面在每条边上保留的空间。`UsePlatformSafeArea` 打开时（默认），页面跟随顶层窗口 insets 管理器的安全区边距，没有该管理器（例如桌面）时为零。关闭 `UsePlatformSafeArea` 并自行设置 `SafeArea` 即可覆盖。背景铺满整个页面，所以它延伸到状态栏之下，而页头和内容留在安全区之内。

手机框为示例做了这件事：它根据模拟的设备设置 `SafeArea`，并关闭 `UsePlatformSafeArea`。用手机上方的切换器可以看到类似 iPhone 的刘海和主屏指示条、手势条、小屏手机和平板，用键盘开关可以看到页面排布在键盘之上。

### 仅内容

没有页头和页脚的页面，内容可以使用整个安全区。

<Demo name="ContentOnly" />

### 滚动内容

默认情况下，内容直接得到页面的高度，铺满页面的界面可以自己排版。内容可能比页面高时，设置 `IsScrollable="True"`：内容会在页眉和页脚之间纵向滚动。`Header` 里带大标题的 `Navbar` 会随内容滚动而折叠。

```xml
<arcm:Page IsScrollable="True">
    <arcm:Page.Header>
        <arcm:Navbar Title="照片" LargeTitle="True" />
    </arcm:Page.Header>
    <!-- 较长的内容 -->
</arcm:Page>
```

### 键盘

软键盘弹出时，页面会自己排布在键盘之上：内容按键盘高度缩小，页脚退到键盘后面（就像 iOS 隐藏标签栏那样），获得焦点的输入框会滚动到可见位置。`AvoidKeyboard`（默认开启）控制这一行为；对允许键盘遮挡的页面可以关闭它。`KeyboardInset` 是键盘遮住的高度，隐藏时为 0，它与 `SafeArea` 合并：页面在底部取两者中较大的一个。表单请设置 `IsScrollable="True"`，让输入框在键盘上方滚动。

<Demo name="Keyboard" />

有平台输入面板（`TopLevel.InputPane`）时，高度来自它。没有的宿主（例如本页的手机框）用 `KeyboardInsetSource` 报告，把它设置在页面之上的任意控件上。

```csharp
var keyboard = new KeyboardInsetSource();
Page.SetKeyboardInsetSource(host, keyboard); // host 之下的每个页面都会继承
keyboard.Set(291);                            // 键盘弹出，高 291
keyboard.Set(0);                              // 再收起
```

`Sheet` 这样的浮层可以在页面内任意位置用 `Page.GetKeyboardInset(visual)` 读取该高度，让自己抬到键盘之上。

### Token

`PageToken` 保存几何：`ContentInset`（内容槽四周的内边距，0）。背景、前景、字体和字号来自 ArcDesign token（`Background`、`Foreground`、`BodyFontFamily`、`TypeBase`），因此页面跟随明暗与强调色。

## API 参考

<!-- api: MobilePage -->

## 无障碍

页面自己不添加键盘行为或角色；页头、内容和页脚保留各自的行为。保持内容按阅读顺序排列，并给导航栏设置 `Title`，让屏幕有名字。

## 动效

背景按标准曲线跟随明暗切换。减少动效时立即切换。

页面用底部内边距上的 `MotionValue` 跟随键盘，因为该内边距是布局输入，ControlTheme 过渡无法表达。平台报告了键盘自己的时长时使用该时长，否则升起时用平滑弹簧，收起时用利落的弹簧。减少动效或关闭动效时，布局直接跳到键盘的位置。键盘到达时以及稳定后，获得焦点的输入框都会滚动到可见位置。

## 相关组件

- `MobileNavbar`：页面的顶部栏。
- `MobileTabbar`：页面的底部栏。
- `MobileButton` 与 `MobileSwitch`：内容里的触控控件。
