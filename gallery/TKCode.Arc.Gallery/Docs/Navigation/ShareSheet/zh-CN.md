---
title: Share sheet
summary: 就地分享：分享按钮伸展成一张卡片，可以复制项目链接、设置谁能打开、发布到渠道或发给几个人，发送后缩成一张确认卡片。
source-hash: 3baf3b4a19be
---

<Demo name="Preview" />

## 何时使用

`ShareSheet` 是放在工具栏或页头里的分享按钮。按下后，按钮本身长成分享面板：同一个形状从胶囊拉伸成卡片，所以面板看起来是按钮打开了，而不是弹出了新窗口。
面板写明分享的是什么，显示链接和复制按钮、链接附带的访问权限，提供几个可以发布的渠道，并列出你最常分享的人。选中一个人时，他的头像会飞进上方的标签行；按下发送后，名单交给你的应用。发送成功后，卡片收缩成一张带“撤销”和“完成”的确认卡片。
在手机尺寸的屏幕上，同一个面板改为从底部升起的面板。

- **人们反复转发的项目**，例如文档、看板或设计稿，按钮放在该项目自己的页头里。
- **链接和人员一站处理**：复制链接、决定谁能打开、发给同事，都在同一张卡片里完成。
- **两三个发布目标**，例如邮件或团队聊天，每个按钮各自报告进度和结果。

## 何时不用

- **只需要复制地址**：在链接旁边放一个复制按钮即可。
- **逐人设置角色，或按地址查找人员**：这属于表单，请为它打开一个对话框。
- **只有一组去处、无人可选**：弹出框或下拉菜单用更少的东西就能做到。

## 用法

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:ShareSheet Title="Quarterly review" Link="https://example.com/doc/quarterly-review" Access="team">
        <arc:ShareSheet.AccessLevels>
            <arc:ShareSheetAccess Value="private" Label="Private" Description="Only the people you add"
                                  Glyph="{x:Static arc:ArcIcons.Lock}" />
            <arc:ShareSheetAccess Value="team" Label="Your team" Description="Everyone on the team can comment"
                                  Glyph="{x:Static arc:ArcIcons.Users}" />
        </arc:ShareSheet.AccessLevels>
        <arc:ShareSheet.Channels>
            <arc:ShareSheetChannel Id="email" Label="Email" DoneLabel="Sent" Glyph="{x:Static arc:ArcIcons.Mail}" />
        </arc:ShareSheet.Channels>
        <arc:ShareSheet.People>
            <arc:ShareSheetPerson Id="ada" Name="Ada Moreno" />
            <arc:ShareSheetPerson Id="ravi" Name="Ravi Kapoor" />
        </arc:ShareSheet.People>
    </arc:ShareSheet>
</UserControl>
```

`ShareSheet` 位于 Special 包：在 `UseArcControls()` 旁边用 `UseArcSpecial()` 注册。布局空间只给分享按钮，因为面板画在页面上方的覆盖层里。按钮下方要空出一些位置；靠近边缘时，选一个朝远离边缘方向打开的 `Align`。`IsOpen`（双向）、`Open()` 与 `Close()` 用于在代码中控制它。

- `Title` 是项目的名称，面板标题把它显示为 Share “Title”。`Link` 是网址：它单独占一行，“复制”把它放到剪贴板上（为 null 时不显示该行）。
- `AccessLevels` 存放 `ShareSheetAccess` 项（`Value`、`Label`、`Description`、`Glyph`）。`Access` 是选中的值，双向绑定；初始为第一项。
- `Channels` 存放 `ShareSheetChannel` 项（`Id`、`Label`、`DoneLabel`、`Glyph`）。发布后按钮会把 `Label` 换成 `DoneLabel` 两秒，所以两者的字数要相近。
- `People` 存放 `ShareSheetPerson` 项（`Id`、`Name`、`Avatar`）。没有 `Avatar` 图片时，面板按 id 生成一个柔和的渐变，同一个人总是同样的样子。`SelectedPeople` 列出已选中的人；`Pick` 与 `Unpick` 在代码中修改它。

### 发送与发布

把服务器调用交给面板，等待由它处理。`SendHandler` 收到人员和访问权限；运行期间发送按钮转圈。若它抛出异常，按钮上方出现一行错误，面板保持打开，人员仍处于选中状态；若它完成，触发 `Sent`，面板收拢成确认卡片。确认卡片上的“撤销”回到面板、保留同样的人员，并触发 `SendUndone`，让你的应用撤回这次发送。
`ChannelHandler` 对单个渠道按钮的作用相同：先转圈，然后显示该渠道的 `DoneLabel`，处理程序抛出异常时显示 Failed。

```csharp
sheet.SendHandler = async (submission, token) =>
    await api.ShareAsync(submission.People.Select(p => p.Id), submission.Access?.Value, token);
sheet.ChannelHandler = (channel, token) => api.PostAsync(channel.Id, token);
```

<Demo name="Channels" />

### 面板向哪里展开

`Align` 设定面板从哪个锚点展开。`End`（默认）固定按钮的右上角，面板向左、向下伸展；`Start` 固定左上角，向右、向下伸展；`Center` 固定按钮的中心，面板对称地展开和收拢。面板若会越过窗口边缘，会被移回窗口内。

<Demo name="Align" />

### 只有人员，以及手机上的底部面板

只有你设置的内容才会显示对应的行：不设置 `Link`、`AccessLevels` 或 `Channels`，相应的行就省略。视口宽度为 520 px 或更窄时（手机预览也算），面板变成贴在底边的底部面板，后面的页面变暗；设置 `SheetOnPhones="False"` 可以在这些宽度下也保留浮动面板。

<Demo name="People" />

### 最佳实践

**推荐**

- `People` 里只放这个项目通常会发给的几个人，最可能的排在前面。
- 让处理程序返回任务，面板才能显示进度、错误和结果。
- 渠道名称要短，完成词的长度与之相近。

**避免**

- 把整本通讯录塞进 `People`：这一行用来快速选人，不是用来查找人的。
- 超过三四个渠道：按钮共用一行。

## API 参考

<!-- api: ShareSheet -->

## 无障碍

- 分享按钮公开面板的展开状态。焦点随流程移动：面板打开时落在“复制”上（或现有的第一个控件），发送后落在“完成”上，全部关闭后回到分享按钮。在页面其他地方按下会关闭面板，焦点留在被按下的元素上。
- 访问权限列表是一组选项；选中的一项标记为已选，也是列表唯一的 Tab 停靠点，方向键在选项之间移动。
- 每位最近联系人都是一个带全名的切换按钮，每个标签都有一个写明要移除谁的移除按钮。
- 屏幕阅读器通过一个礼貌的实时区域听到每一步：链接已复制、添加或移除了某人、渠道的结果，以及分享送达了哪些人。发送失败时还会出现一行警示文字。

### 键盘交互

| 按键 | 操作 |
|---|---|
| Enter 或 Space | 在分享按钮上时打开面板；在其他地方时激活获得焦点的控件。 |
| Tab | 依次经过关闭按钮、复制、访问权限行、渠道、人员、标签和发送。 |
| 上、下、左、右 | 访问权限列表展开时，逐个经过其中的选项；越过最后一项后从第一项重新开始。 |
| Home / End | 移到第一个或最后一个访问权限选项。 |
| Escape | 访问权限列表展开时将其关闭；否则把面板收拢回分享按钮。 |

## 动效

- 分享按钮、面板和发送后的确认卡片画成同一个表面；每次换面时，弹簧把它的尺寸和圆角带到新的形状。内容以短暂的模糊交换：离开的内容在淡出时略微缩小，进入的内容从 94% 放大。从面板到确认卡片以及返回时，内容改为横向滑动。
- 选中某人时，他的头像会有一个副本飞向新的标签。它画在面板上方，所以标签行的边缘不会把它截断。被移除的标签淡出，后面的标签移过来补上空位。
- 发送按钮上的数字随着添加或移除人员向上或向下跳动。还没选人就按发送时，收件人这一行会短促、生硬地抖一下。

| 变化 | 完整 | 减弱 | 关闭 |
|---|---|---|---|
| 打开、确认、收回 | 尺寸弹簧，模糊与缩放交叉切换 | 尺寸直接到位，两面淡入淡出 | 立即 |
| 面板 ↔ 确认卡片 | 两面横向滑动 32 px 并模糊切换 | 两面淡入淡出 | 立即 |
| 选中一个人 | 头像飞向标签 | 标签淡入 | 标签直接出现 |
| 发送按钮中的人数 | 数字滚动 | 替换 | 替换 |
| 没有选人就发送 | 收件人行轻推 | 不移动 | 不移动 |
| 手机底部面板 | 向上滑入，跟随把手拖动 | 淡入 | 直接出现 |

## 响应式

- 宽度：面板 384 px，确认卡片 304 px，两者都不超过视口宽度减 32 px，因此在 320 px 的屏幕上也放得下。
- 视口宽度不超过 520 px 时，面板是距两侧和底部 8 px 的底部面板。把把手向下拖过 120 px 或向下甩动即可关闭，拖得更短会弹回原位。Escape、点按变暗的页面和“完成”也会关闭它。
- 悬停色只响应鼠标或笔；触摸不会留下悬停状态。

## 相关组件

| 组件 | 适用场景 |
|---|---|
| `ShareSheet` | 一个项目通过链接、渠道和几个人来分享 |
| `ShareAccess` | 在对话框中管理人员及其角色 |
| `CopyButton` | 只需要复制链接 |
| `ActionMorph` | 按钮为另一种操作长成一个小的内嵌表单 |
