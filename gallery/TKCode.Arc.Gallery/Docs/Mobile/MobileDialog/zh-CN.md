---
title: 移动端对话框
summary: 居中的提示或确认对话框，带标题、消息和最多三个操作，位于遮罩之上。
source-hash: 377004849ec9
---

<Demo name="Basic" />

## 何时使用

`Dialog` 让用户停下来做一个不能等的决定：确认删除、允许或拒绝权限、在几种继续方式中选一种。

- **确认**：在无法撤销的操作之前，把破坏性操作标成危险色。
- **提示**：必须先读完才能继续的信息。
- **简短的选择**：两三个操作，没有其他内容。

即时示例在手机外框里运行，那是桌面上的模拟；真机上的触摸反馈这里没有验证。

## 何时不用

- **选项列表或表单**：用 `Sheet`，它从底部升起，拇指够得着。
- **不需要回答的消息**：用 `Toast`。
- **用户事后可能想撤销的事**：直接去做，并在 Toast 里提供撤销，而不是先询问。

## 用法

把对话框放进页面的 `Overlay` 槽，用 `IsOpen` 打开。操作按钮就是对话框的子项，请使用 `Button`。

```xml
<arcm:Page xmlns="https://github.com/avaloniaui"
           xmlns:arcm="https://github.com/AIDotNet/TKCode.Arc.Mobile">
    <arcm:Page.Overlay>
        <arcm:Dialog Title="Delete this photo?"
                     Message="This cannot be undone."
                     IsOpen="{Binding ConfirmDelete}">
            <arcm:Button Content="Cancel" Variant="Tinted" />
            <arcm:Button Content="Delete" arcm:Dialog.IsDestructive="True" />
        </arcm:Dialog>
    </arcm:Page.Overlay>
</arcm:Page>
```

`IsOpen` 双向绑定。按下操作按钮会先执行它自己的 `Click` 处理或 `Command`，然后关闭对话框；设置 `CloseOnAction="False"` 可以自己决定何时关闭，例如请求结束之后。

两个操作在每个都放得进半宽时同排显示；三个操作，或标签太宽放不下一排时，按全宽竖向堆叠。面板最宽 320，四周至少留出 24。

默认只有操作能关闭对话框。设置 `IsDismissible="True"` 后，点遮罩和按 Esc 也能关闭，适合有无害退出方式的对话框。

在按钮上设置 `Dialog.IsDestructive="True"` 会用危险色绘制它：实心按钮变成危险色胶囊，淡色按钮是浅底上的危险色文字，纯文字按钮是危险色文字。

### 同一页面上的多个浮层

`Page.Overlay` 只能放一个元素。要让对话框与底部面板或 Toast 同时存在，把它们放进该槽里的一个 `Grid`，Toast 放在最后，这样它显示在其他浮层之上，演示里的两个对话框就是这样放的。

```xml
<arcm:Page xmlns="https://github.com/avaloniaui"
           xmlns:arcm="https://github.com/AIDotNet/TKCode.Arc.Mobile">
    <arcm:Page.Overlay>
        <Grid>
            <arcm:Sheet Title="Share" />
            <arcm:Dialog Title="Delete?" />
            <arcm:Toast />
        </Grid>
    </arcm:Page.Overlay>
</arcm:Page>
```

每个浮层在关闭时都不拦截触摸。

### Token

`DialogToken` 包含 `ScrimColor`（遮罩的完全不透明颜色，亮色和暗色不同）、`PanelMaxWidth`（320）、`PanelMargin`（24）和 `PanelPadding`。颜色、圆角和字体来自 ArcDesign token。

## API 参考

<!-- api: MobileDialog -->

## 无障碍

Esc 会关闭可取消的对话框。打开时焦点移到对话框上。给它一个把决定说成问句的 `Title`，操作用动词命名（“删除”“取消”），不要用“是”和“否”。按钮至少 44 乘 44。不能被取消的对话框必须有退路：包含一个取消操作。

## 动效

遮罩淡入的同时，面板从 0.94 放大并淡入，用一根灵敏的弹簧，由同一个值驱动。关闭更快：0.16 秒，走退出曲线，没有过冲；在对话框还在出现时关闭，它会从当前位置反向运动。它由代码驱动，因为对话框必须在退出动画结束后才停止拦截触摸，而主题过渡无法发出这个信号。减弱动效时，面板只淡入淡出，不缩放，用时 0.12 秒；关闭动效时，它立即出现和消失。

## 相关组件

- `MobilePage`：`Overlay` 槽承载对话框。
- `MobileButton`：对话框的操作，也是打开它的常用方式。
- `MobileSheet`：用于选项和表单。
- `MobileToast`：用于不需要回答的消息。
