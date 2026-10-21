---
title: 提示
summary: 为不熟悉的控件提供简短的辅助文字。
source-hash: 9321fab1844c
---

<Demo name="Sides" />

## 何时使用

- 纯图标按钮或工具栏项的单行标签。
- 页面上被截断的标签的完整文字。
- 不影响主要操作的简短帮助，键盘用户通过聚焦控件即可看到。

## 何时不用

- 链接、按钮或输入框：用 `Popover`。提示内容无法交互。
- 人员或链接的丰富预览：用 `HoverCard`。
- 触摸用户必须看到的信息：应显示在页面上，而不是放在提示中。

## 用法

提示是一个附加属性。在任意控件上设置 `arc:Tooltip.Tip`；悬停或键盘聚焦时显示文字。

### 基础

```xml
<arc:Button Content="复制" arc:Tooltip.Tip="复制链接" />
```

### 方向

`arc:Tooltip.Side` 设置提示气泡在触发器的哪一侧打开：`Top`（默认）、`Bottom`、`Left` 或 `Right`。

```xml
<arc:Button Variant="Secondary" Content="分享" arc:Tooltip.Tip="与你的团队分享"
            arc:Tooltip.Side="Bottom" />
```

### 绑定文字

`Tip` 接受任意对象。当文字来自视图模型时进行绑定。

```xml
<arc:Button Content="保存" arc:Tooltip.Tip="{Binding SaveHint}" />
```

### 修改文字与延迟

提示打开期间可以在代码中修改文字。文字会交叉淡化，气泡随之调整大小以适应内容。设置 `ShowDelay` 可以修改首个提示打开前的等待时间，默认为 250ms。

<Demo name="LiveText" />

```csharp
Tooltip.SetTip(save, "刚刚已保存");
Tooltip.SetShowDelay(save, TimeSpan.FromMilliseconds(400));
```

## API 参考

<!-- api: Tooltip -->

## 无障碍

- 提示打开时，其文字会写入触发器的帮助文本中。
- 提示不能替代名称。仅含图标的触发器仍需设置自己的 `AutomationProperties.Name`。
- 提示在悬停和键盘聚焦时打开，按 Escape 关闭。
- 气泡中没有可交互的元素，因此键盘用户可以阅读内容而不丢失焦点。

## 动效

- 首个提示等待 250ms，然后从触发器向上升起 3px，同时从缩放 0.97 开始出现。
- 指针移动到相邻触发器，或上一个提示在 300ms 之前刚关闭时，下一个提示立即打开，仅以淡入方式呈现。
- 提示打开期间文字变化时，新词带着柔和的模糊升起，气泡以弹簧调整为新尺寸。
- 关闭为瞬间淡出，气泡同时缩小到 0.98。
- 减弱动效时移除移动，保留短暂淡入。关闭动效时提示立即显示或移除。

## 相关组件

- `Popover` 用于锚定在控件上的互动内容。
- `HoverCard` 用于人员或链接的丰富只读预览。
