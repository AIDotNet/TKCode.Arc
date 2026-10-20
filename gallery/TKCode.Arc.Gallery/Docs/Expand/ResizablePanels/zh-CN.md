---
title: 可调整大小的面板
summary: 拖动窗格之间的分隔条，在各窗格之间调配空间。
source-hash: e535bd1502d7
---

## 何时使用

- 编辑器、邮件或仪表盘布局，用户需要自行决定每个窗格占用多少空间。
- 可以收起的侧边窗格，收起后为主窗格腾出空间。
- 需要在会话之间保存并恢复的布局。

## 何时不用

- 以浮层形式覆盖页面的窗格：用 `Drawer`。
- 一次只显示一个窗格：用 `Tabs`。
- 上下堆叠的窗格：Arc 只支持水平分割。请改用布局容器。
- 窄屏下窗格需要堆叠显示：用 `Tabs`，并让页面改变其布局。

## 用法

为 `Panels` 中的每个窗格添加一个 `ResizablePanel`。每个窗格都有一个 `DefaultSize` 份额。各份额是相对值，不必加起来等于 100。该组需要父级提供明确的高度。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:ResizablePanels Label="邮件布局" Height="320">
        <arc:ResizablePanel Id="folders"
                            Label="文件夹"
                            DefaultSize="22"
                            MinSize="140"
                            IsCollapsible="True">
            <TextBlock Text="收件箱、草稿、已发送、归档" />
        </arc:ResizablePanel>
        <arc:ResizablePanel Id="messages"
                            Label="邮件列表"
                            DefaultSize="33"
                            MinSize="200">
            <TextBlock Text="42 个会话" />
        </arc:ResizablePanel>
        <arc:ResizablePanel Id="reading"
                            Label="阅读窗格"
                            DefaultSize="45"
                            MinSize="220"
                            IsCollapsible="True">
            <TextBlock Text="季度回顾" />
        </arc:ResizablePanel>
    </arc:ResizablePanels>
</UserControl>
```

`Label` 为辅助技术提供该组的名称。每个窗格的 `Label` 为其分隔条命名。

### 默认收起

在可收起的窗格上设置 `IsCollapsed="True"`，即可使其初始时处于隐藏状态。其分隔条会显示一个用于恢复该窗格的标签。

```xml
<arc:ResizablePanel Id="files"
                    Label="文件"
                    DefaultSize="25"
                    IsCollapsible="True"
                    IsCollapsed="True">
    <TextBlock Text="src / components / panels.tsx" />
</arc:ResizablePanel>
```

### 限制

`MinSize` 是窗格开始产生阻力时的宽度（以像素计）。`MaxSize` 限定宽度上限，默认不限制。

```xml
<arc:ResizablePanel Id="code" Label="代码" DefaultSize="50" MinSize="240" MaxSize="800" />
```

### 保存与恢复布局

`Sizes` 以百分比保存每个窗格的份额，且为双向绑定。在用户完成一次调整后保存它，再次设置即可恢复布局。`LayoutChanged` 会在调整结束、布局稳定后触发。

```csharp
panels.LayoutChanged += (_, e) =>
{
    settings.PaneSizes = e.Sizes.ToArray();
};

panels.Sizes = settings.PaneSizes;
```

### 重置

`Reset` 会将各窗格恢复为默认份额，并适配当前宽度。

```csharp
panels.Reset();
```

## API 参考

<!-- api: ResizablePanels -->

<!-- api: ResizablePanel -->

## 无障碍

- 每个分隔条都可获得焦点。其值为该窗格的份额；对于隐藏的窗格，会以“按 Enter 显示”的提示进行播报。
- 该组以其 `Label` 命名并作为一个组被播报。收起的窗格对辅助技术隐藏。
- 左右方向键以小步长调整分隔条。按住 Shift 再按方向键则以大步长调整。
- Home 与 End 将分隔条移动到最小与最大位置。Enter 可隐藏或恢复可收起的相邻窗格。
- 指针拖动过程中按 Escape，分隔条会返回到拖动开始时的位置。

## 动效

- 拖动时分隔条直接跟随指针。超过窗格的最小尺寸后，分隔条会以短促的橡皮筋效果产生阻力。
- 松开时，窗格以保留指针速度的弹簧回落到位。将可收起的窗格拖到远低于其最小尺寸，或朝边缘快速甩动，即会将其收起。
- 键盘调整使用灵敏的弹簧。
- 窗格收起时会弹出一个恢复标签，窗格缩小时其内容淡出。
- 减弱动效时，窗格直接移动到目标位置，恢复标签仅淡入。

## 相关组件

- `Drawer`：用于以浮层形式覆盖页面的窗格。
- `Tabs`：用于一次只显示一个窗格，或用于窄屏布局。
- `ScrollArea`：用于在窗格内溢出的内容。
