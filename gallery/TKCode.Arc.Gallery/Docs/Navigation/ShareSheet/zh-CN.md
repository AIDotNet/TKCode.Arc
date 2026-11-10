---
title: Share sheet
summary: 分享一个项目。复制它的链接、选择一个人或一个去处，或执行一个操作；在手机上是从底部升起的面板，在桌面上是弹出框。
source-hash: 03565f0ead93
---

<Demo name="Preview" />

## 何时使用

`ShareSheet` 把传递一个项目的所有方式集中在同一个地方。最上面是项目的链接和复制按钮；下面是一排你最常分享的人，每个人是一个带姓名首字母的柔和圆圈；再下面是一排去处，例如电子邮件、消息或团队空间；最后是几个操作，例如嵌入或下载。在手机上，它作为可以拖走的底部面板从底部升起；在更宽的屏幕上，它在发起请求的按钮下方作为弹出框打开。面板只报告选了什么，发送由你的应用完成。

- **分享文档、照片集或页面**：发给人和应用。
- **邀请**：把链接发给几个人或加入日历。
- **与分享相邻的导出**：嵌入代码、下载、打印。

## 何时不用

- **管理谁有访问权限**（角色、权限、链接设置）：这需要设置对话框。
- **只有一个去处**：一个普通的"复制链接"按钮更快。
- **很长的联系人列表**：这些列表只显示你常用的人；搜索所有人应放在选择器里。

## 用法

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:ShareSheet Link="https://example.com/a/autumn-hike" Subtitle="Autumn hike photos">
        <arc:Button Content="Share" Variant="Secondary" />
        <arc:ShareSheet.People>
            <arc:ShareSheetTarget Name="Mira Okafor" />
            <arc:ShareSheetTarget Name="Tomas Reyes" />
        </arc:ShareSheet.People>
        <arc:ShareSheet.Destinations>
            <arc:ShareSheetTarget Name="Email" Glyph="{x:Static arc:ArcIcons.Mail}" />
        </arc:ShareSheet.Destinations>
        <arc:ShareSheet.Actions>
            <arc:ShareSheetAction Label="Download a copy" Glyph="{x:Static arc:ArcIcons.Download}" />
        </arc:ShareSheet.Actions>
    </arc:ShareSheet>
</UserControl>
```

`ShareSheet` 在 Special 包中：请在 `UseArcControls()` 旁边用 `UseArcSpecial()` 注册。它的内容就是触发按钮：那里的按钮会打开面板。`Open()`、`Close()`、`ShowAt(anchor)` 和双向的 `IsOpen` 可以在代码中控制它。

`People` 和 `Destinations` 是 `ShareSheetTarget` 的列表：包含 `Name`、供辅助技术朗读的可选 `Detail`、可选的 `Glyph`（用来代替首字母绘制），以及用于你自己的值的 `Tag`。`Actions` 是 `ShareSheetAction` 的列表，包含 `Label`、`Glyph` 和 `Command`。`Link` 会添加带复制按钮的链接行；设为 null 则隐藏该行。里面没有内容的分组不会显示。

### 获知选了什么

`Shared` 触发时，目标在 `e.Target` 中，是否为人在 `e.IsPerson` 中，面板随后关闭，除非 `ClosesOnShare` 为 false。`ActionInvoked` 在某个操作的命令执行之前触发。`LinkCopied` 在复制按钮把链接放入剪贴板之后触发。

### 在哪里打开

`Presentation` 默认为 `Auto`：视口宽度小于 640 px 时是底部面板，否则是弹出框。`Popover` 和 `Sheet` 则始终选定其中一种。

<Demo name="Sheet" />

### 最佳实践

**建议**

- 按顺序显示这个人最常分享的几个人。
- 把 `Subtitle` 设为项目的名称，让人清楚分享的是什么。
- 在 `Shared` 之后，在你的应用里确认这次分享，例如用一条轻提示。

**避免**

- 人或去处超过大约八个：这些列表可以滚动，但人们看到的是最前面几个。
- 用 `Actions` 放与该项目无关的事情。

## API 参考

<!-- api: ShareSheet -->

## 无障碍

- 触发按钮会报告面板是否已展开。弹出框或底部面板打开时获得焦点（优先到复制按钮，否则到第一个人），关闭时把焦点还给触发按钮。
- 人和去处是按钮，名称取自 `Name`，`Detail` 作为帮助文本；操作是菜单项，名称取自 `Label`。它们都可以通过辅助技术触发。
- 每一排以及操作列表在 Tab 顺序中各占一个停靠点；方向键在其内部移动。使用键盘时，焦点环显示在方块或行的内部。

### 键盘交互

| 按键 | 操作 |
|---|---|
| Tab | 在复制按钮、人、去处和操作之间移动。 |
| Left / Right | 沿着一排人或去处移动。 |
| Up / Down | 在操作之间移动。 |
| Home / End | 移到该组的第一项或最后一项。 |
| Enter 或 Space | 选择获得焦点的人、去处或操作。 |
| Escape | 关闭面板，并让焦点回到触发按钮。 |

## 动效

- 作为弹出框时，它从触发按钮一侧出现，伴随短暂的淡入和一个稳定下来的弹簧；作为底部面板时，它从窗口底部升起，可以向下甩动来关闭。两者都是本库自带的弹出框和底部面板。
- 复制按钮的文字换成"Copied"并带一个对勾，然后再换回来。
- 方块和行自己绘制自己；悬停和获得焦点只会重绘它们。

| 变化 | 完整 | 减弱 | 关闭 |
|---|---|---|---|
| 弹出框 | 从触发按钮淡入并稳定下来 | 淡入 | 直接出现 |
| 底部面板 | 升起，跟随拖动 | 淡入 | 直接出现 |
| 复制文字 | 模糊切换 | 交叉淡入淡出 | 直接切换 |

## 响应式

- 视口宽度小于 640 px 时（包括手机预览），面板从底部升起并占满宽度；它的高度适应内容，最高为窗口的 92%。
- 作为弹出框时，面板宽 320 px。
- 内容放不下时，各排会横向滚动，获得焦点的方块会被滚动到可见位置。

## 相关组件

- `Popover` 和 `BottomSheet` 是这个面板打开时使用的两种表面。
- `CopyButton` 是链接行的复制按钮。

| 组件 | 适用场景 |
|---|---|
| `ShareSheet` | 一个项目可以发给人、应用或一个链接 |
| `CopyButton` | 复制链接是唯一的分享方式 |
| `Dialog` | 分享需要访问设置或一条消息 |
