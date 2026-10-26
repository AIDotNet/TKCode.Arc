---
title: 移动端操作面板
summary: 从底部升起的操作列表，可有一个破坏性操作行和单独的取消行，选择一行、点遮罩或下拉即可关闭。
source-hash: e3b6387486bf
---

<Demo name="Basic" />

## 何时使用

`ActionSheet` 是移动端对菜单的回答：围绕同一个对象的一小组操作，从底部边缘升起。

- **对某个条目的操作**：分享、加入相册、删除。
- **需要退路的选择**：取消行始终在列表下方、与列表分开，拇指容易够到。
- **破坏性操作**：既要容易找到、又不能轻易误触，把该行标记为 `IsDestructive`。

实时示例在手机外框里运行，那是桌面上的模拟；真机上的触摸反馈和拖动这里没有验证。

## 何时不用

- **超过大约六个操作**：用带 `ListGroup` 的 `Sheet`，它可以滚动和分组。
- **表单或有多个步骤的任务**：用 `Sheet`。
- **对单个操作的确认**：用对话框。

## 用法

把操作面板放进页面的 `Overlay` 槽，用 `IsOpen` 打开。它的内容是一列 `ActionSheetItem` 行。给每一行设置 `Command`，或者在整个面板上处理一次 `ActionInvoked`；选择一行后面板会关闭。

```xml
<arcm:Page xmlns="https://github.com/avaloniaui"
           xmlns:arcm="https://github.com/AIDotNet/TKCode.Arc.Mobile">
    <arcm:Page.Overlay>
        <arcm:ActionSheet Title="照片" Message="IMG_2041.jpg" IsOpen="{Binding ShowOptions}">
            <arcm:ActionSheetItem Content="分享" Command="{Binding ShareCommand}" />
            <arcm:ActionSheetItem Content="加入相册" />
            <arcm:ActionSheetItem Content="删除照片" IsDestructive="True" Command="{Binding DeleteCommand}" />
        </arcm:ActionSheet>
    </arcm:Page.Overlay>
</arcm:Page>
```

```csharp
void OnAction(object? sender, ActionSheetActionEventArgs e) => Log((string)e.Action.Content!);
```

`Title` 和 `Message` 是可选的，位于第一行上方。`CancelText` 是取消行的文字（`"Cancel"`）；设为 null 会隐藏该行。取消会关闭面板，但不触发 `ActionInvoked`。操作面板就是一个 `Sheet`：点遮罩、下拉或按 Esc 都能关闭它（设置 `IsDismissible="False"` 可禁止），`SafeAreaBottom` 让取消行避开 Home 指示条（见 `MobileSheet`）。

### Token

`ActionSheetToken` 保存 `PanelPadding`（两张卡片周围的空间）、`CancelMargin`（取消卡片上方的间隔）和 `HeaderPadding`；`ActionSheetItemToken` 保存 `RowHeight`（56）和 `ContentPadding`。颜色、圆角和字体来自 ArcDesign token：操作用强调色，破坏性操作用危险色。

## API 参考

<!-- api: MobileActionSheet -->

## 无障碍

每一行都是按钮，高度至少 56 px，容易点中，也可以用 Tab 和 Enter 或空格操作。Esc 会关闭可关闭的操作面板，打开时焦点会移到面板上。破坏性的行有颜色，但也靠文字说明，所以不要只依赖颜色：写“删除照片”，而不是“删除”。点遮罩只是捷径，取消行才是看得见的退路。

## 动效

面板的升起和离开使用与 `MobileSheet` 相同的代码驱动运动值：拖动时每次移动都会写入位置，松手时手指的速度交给弹簧（打开 0.4 秒平滑，关闭 0.3 秒），遮罩每帧跟随位置；主题里声明的过渡跟不上手指。这些行是列表行，不是浮在表面上的按钮，所以不会缩放：手指按下时，背景以一次短淡入变深。减弱动效时，面板直接跳到位置，按下的底色也立即改变。

## 相关组件

- `MobileSheet`：它所基于的面板，用于不是操作列表的内容。
- `MobilePage`：`Overlay` 槽承载操作面板。
- `MobileButton`：打开它的常用方式。
