---
title: 收件箱侧边栏
summary: 邮件应用的侧边栏：写邮件按钮、带未读数的文件夹、彩色标签和存储空间条，可收起为图标栏。
source-hash: a757b7f2b722
---

<Demo name="Preview" />

## 何时使用

`InboxSidebar` 是邮件或消息应用的左侧栏。顶部是把侧边栏收起为图标栏的按钮、邮箱名称和写邮件按钮。下面是文件夹，每个都有图标和计数；未读邮件的计数会让这一行更醒目，而草稿这类普通计数不会。标签在一个可折叠的标题下，每个标签有一个取自主题色板的彩色圆点，列表最后是“New label”一行。底部显示已用的存储空间和登录的账号。在图标栏上，图标、圆点、写邮件按钮的笔和头像都会保留，计数变成小圆点。它基于 `Sidebar` 构建。

- **邮件客户端**和共享收件箱：人们整天在文件夹和标签之间切换。
- **客服台和工单队列**：文件夹是队列，标签是主题。
- **任何带“新建”操作和筛选视图的应用**，例如消息工具或审核队列。

## 何时不用

- **没有计数或新建操作的一般应用导航**：使用 `Sidebar`。
- **文档**：使用 `DocsSidebar`，它带搜索和版本切换。
- **会话列表**：把邮件放在侧边栏旁边的页面中。

## 用法

```csharp
mail.Folders.Add(new InboxSidebarFolder("inbox", "Inbox", ArcIcons.Inbox, 12));
mail.Folders.Add(new InboxSidebarFolder("drafts", "Drafts", ArcIcons.FileText, 3, isCountUnread: false));
mail.MailLabels.Add(new InboxSidebarLabel("clients", "Clients", InboxSidebarLabelTone.Series1, 4));
mail.ComposeRequested += (_, _) => OpenComposer();
mail.ItemInvoked += (_, e) => ShowFolder(e.Key);
```

文件夹和标签都是带属性的对象：改变 `Count`，对应的行就会跟着变。超过 999 的计数显示为“999+”。没有文件夹和标签时，侧边栏显示一个虚构的邮箱。

<Demo name="OwnData" />

### 图标栏

`IsCollapsed` 切换到图标栏，面板按钮的作用相同。`ResizeMode="Push"` 让页面随宽度弹簧移动，默认的 `Overlay` 让侧边栏从页面上方滑过。

<Demo name="Rail" />

## API 参考

<!-- api: InboxSidebar -->

## 无障碍

- 侧边栏是名为“Mail”的导航区域。方向键在行之间移动，Enter 或空格打开一行。
- 有未读邮件的文件夹在名称后加上“unread”。面板按钮名为“Collapse sidebar”或“Expand sidebar”，写邮件按钮在图标栏上保留名称，并显示提示。

## 动效

- 宽度在展开的侧边栏和图标栏之间以弹簧变化，同时文字淡出；标记在行之间滑动；标签以高度弹簧折叠。
- 减少动效时文字快速淡出，不用弹簧；关闭动效时一切立即改变。

## 响应式

- 展开的侧边栏宽 240 px，图标栏宽 64 px。列表自行滚动，底部固定在最下方。
- 窗口较窄时，可从图标栏开始（`IsCollapsed="True"`），或把侧边栏放进 `Drawer`。

## 相关组件

- `Sidebar`：本区块的基础；以及 `DocsSidebar`。
- `NotificationCenter` 用于提醒列表，`SelectionToolbar` 用于对选中的邮件执行批量操作。
