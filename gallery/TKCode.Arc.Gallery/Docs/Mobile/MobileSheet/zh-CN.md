---
title: 移动端底部面板
summary: 从页面底部升起、带遮罩的面板，可停在多个高度，点遮罩、下拉把手或快速下滑即可关闭。
source-hash: a5a3d8786439
---

<Demo name="Basic" />

<Demo name="Detents" />

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

### 停靠高度

设置 `Detents`（按面板所在区域高度的比例，可从 `"0.4, 0.9"` 这样的文字解析），面板就会停在多个高度：先露出一部分，再展开到完整视图。它在 `InitialDetentIndex`（0，最低的一个）处打开。`DetentIndex` 表示它停在哪里，停到另一个高度时触发 `DetentChanged`。`SnapTo(index)`、`Expand()` 和 `Collapse()` 可从代码里移动已打开的面板。

```xml
<arcm:Sheet Title="附近" Detents="0.4, 0.9" InitialDetentIndex="0">
    ...
</arcm:Sheet>
```

```csharp
nearby.DetentChanged += (_, e) => Log($"{e.Index}: {e.Fraction}");
```

拖动时面板一比一跟随手指，越过最高的停靠高度后像橡皮筋一样有阻力。松手时，面板按手指的速度向前看 0.2 秒，停在最近的停靠高度；若最近的是底部则关闭；速度超过 500 px/s 的快速滑动至少会移动一个停靠高度。只有一个值时是固定高度。内容只在最高的停靠高度滚动：低于它时，手指或滚轮会把面板升起来；内容滚到顶部后再下拉，又会移动面板。没有设置 `Detents` 时，面板保持随内容而定的单一高度。

### 底部安全区

面板不会读取平台信息。用 `SafeAreaBottom`（单位 px 的数字）给内容下方留出空间，例如绑定到页面的安全区：

```xml
<arcm:Page x:Name="Screen">
    <arcm:Page.Overlay>
        <arcm:Sheet SafeAreaBottom="{Binding SafeArea.Bottom, ElementName=Screen}" Title="分享">
            ...
        </arcm:Sheet>
    </arcm:Page.Overlay>
</arcm:Page>
```

### Token

`SheetToken` 保存 `ScrimColor`（遮罩完全不透明时的颜色，明暗模式不同）、`PanelRadius`（顶部两个角为 16）、`GrabberWidth`（36）、`GrabberHeight`（5）和 `HandlePadding`。颜色、圆角和字体来自 ArcDesign token。

## API 参考

<!-- api: MobileSheet -->

## 无障碍

Esc 会关闭可关闭的面板。请给面板设置 `Title`：它是把手区域里的第一段文字。打开时焦点会移到面板上。点遮罩只是捷径，不是唯一的方式：面板里有表单时，请在里面保留一个看得见的关闭方式。把手区域可用任何指针拖动，而内容只在触摸时才会拖动面板，所以鼠标滚轮或滚动条不会移动它。有停靠高度时，用 `DetentIndex` 和 `DetentChanged` 告诉应用面板停在哪个高度。

## 动效

面板的位置是代码驱动的运动值：拖动时每次移动都会写入它，松手时手指的速度交给弹簧（打开 0.4 秒平滑，关闭 0.3 秒），这是主题里声明的过渡做不到的。遮罩的不透明度每帧跟随面板的位置：最高的停靠高度时最深，多个高度中最低的一个稍浅，关闭时完全透明。在停靠高度之间移动是从面板当前位置出发的一次弹簧，所以再次点按或拖动会无缝打断它。减弱动效时，面板直接跳到停靠高度而不滑动。

## 相关组件

- `MobilePage`：`Overlay` 槽承载面板。
- `MobileListGroup`：操作面板常用的内容。
- `MobileButton`：打开面板的常用方式。
- `MobileActionSheet`：现成的操作列表，带一个取消行。
