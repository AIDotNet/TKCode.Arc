---
title: 移动端底部面板
summary: 从页面底部升起、带遮罩的面板，点遮罩、下拉把手或快速下滑即可关闭。
source-hash: 23b17ea56a33
---

<Demo name="Basic" />

## 何时使用

`Sheet` 是移动端对弹窗的回答：从底部边缘升起的面板，承载一个简短的任务，完成后离开。

- **选择和操作**：分享选项、操作列表、筛选。
- **简短的表单或详情**，不想让人离开当前页面。
- **桌面应用会放进下拉或菜单的内容**：手机上，底部面板更容易够到、也更容易点中。

实时示例在手机外框里运行，那是桌面上的模拟；真机上的触摸反馈和拖动这里没有验证。

## 何时不用

- **有自己返回栈的流程步骤**：那是一个页面，不是面板。
- **简短的确认**：用对话框。
- **比屏幕还高的内容**：面板最高到页面的 90%，之后内容滚动；很长的任务适合整页。

## 用法

把面板放进页面的 `Overlay` 槽（它盖住页眉和页脚），用 `IsOpen` 打开。面板高度随内容而定；点遮罩、下拉把手、快速下滑或按 Esc 都能关闭它。

```xml
<arcm:Page xmlns="https://github.com/avaloniaui"
           xmlns:arcm="https://github.com/AIDotNet/TKCode.Arc.Mobile">
    <arcm:Page.Overlay>
        <arcm:Sheet Title="分享" IsOpen="{Binding ShowShare}">
            <arcm:ListGroup>
                <arcm:ListItem Content="发送给某人" ShowChevron="True" />
                <arcm:ListItem Content="复制链接" />
            </arcm:ListGroup>
        </arcm:Sheet>
    </arcm:Page.Overlay>
</arcm:Page>
```

`IsOpen` 是双向绑定，所以手势关闭面板时会把 `false` 写回去。需要只能用按钮关闭的面板时，设置 `IsDismissible="False"`；此时向下拉会像橡皮筋一样有阻力。

拖动时面板一比一跟随手指。松手时，面板按手指的速度向前看 0.2 秒：过了中点就关闭，向下的快速下滑超过 500 px/s 时无论从哪里都会关闭；否则它弹回打开的位置。

底部安全区目前还没有处理：在有 Home 指示条的手机上，请把这部分空间加进面板的 `Padding`。

### Token

`SheetToken` 保存 `ScrimColor`（遮罩完全不透明时的颜色，明暗模式不同）、`PanelRadius`（顶部两个角为 16）、`GrabberWidth`（36）、`GrabberHeight`（5）和 `HandlePadding`。颜色、圆角和字体来自 ArcDesign token。

## API 参考

<!-- api: MobileSheet -->

## 无障碍

Esc 会关闭可关闭的面板。请给面板设置 `Title`：它是把手区域里的第一段文字。打开时焦点会移到面板上。点遮罩只是捷径，不是唯一的方式：面板里有表单时，请在里面保留一个看得见的关闭方式。只有把手区域可以拖动，所以滚动内容不会拖动面板。

## 动效

面板的位置是代码驱动的运动值：拖动时每次移动都会写入它，松手时手指的速度交给弹簧（打开 0.4 秒平滑，关闭 0.3 秒），这是主题里声明的过渡做不到的。遮罩的不透明度跟随面板的位置。减弱动效时，面板直接跳到位置而不滑动。

## 相关组件

- `MobilePage`：`Overlay` 槽承载面板。
- `MobileListGroup`：操作面板常用的内容。
- `MobileButton`：打开面板的常用方式。
