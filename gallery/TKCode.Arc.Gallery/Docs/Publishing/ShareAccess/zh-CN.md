---
title: Share access
summary: 文档共享对话框：按邮箱邀请成员并设定角色，修改或移除每个人的权限，选择还有谁能打开链接，并复制链接。
source-hash: f9608d5f119f
---

<Demo name="Preview" />

## 何时使用

`ShareAccess` 是文档、看板或文件夹“共享”对话框的内容。顶部的地址框把输入或粘贴的每个邮箱变成一个标签，旁边的选择框设定新成员的角色（查看者、评论者或编辑者），点击 Invite 把他们加入列表。下面，每个有权限的人都有一行，显示头像、名字和邮箱，以及一个角色按钮，它的菜单可以修改角色或移除这个人；所有者那一行不能修改。“General access”决定还有谁能打开链接：只有列表中的人、组织内的任何人，或拥有链接的任何人，每种都有自己的角色。底部可以复制链接并关闭对话框。

- **文档、看板和文件**：人们和同事或访客共享它们。
- **有组织的工作区**：中间的链接设置让同事可以打开，而不必公开链接。
- **管理页面**：列出谁能看到某个资源。

## 何时不用

- **分享到其他应用**（消息、社交帖子）：使用系统的分享面板，或 `ReferralCard` 的分享按钮。
- **管理整个工作区的成员**，包括邀请、席位和账单：使用成员表格。
- **只有一个链接**：在只读字段旁边使用 `CopyButton`。

## 用法

`ShareAccess` 位于 Special 包中：在 `UseArc()` 旁边用 `UseArcSpecial()` 注册它。设置文档的名称和链接、组织名称（用于中间的链接设置）以及成员。把它放进 `Dialog` 或 `Popover`，也可以直接放在设置页面中。

```xml
<arc:ShareAccess DocumentName="Spring trail plan"
                 OrganizationName="Fernhollow"
                 Link="https://notes.example/d/7hq2"
                 CurrentUserEmail="mira@fernhollow.example">
    <arc:ShareAccess.People>
        <arc:ShareAccessPerson Name="Mira Holt" Email="mira@fernhollow.example" Role="Owner" />
        <arc:ShareAccessPerson Name="Tobias Brandt" Email="tobias@fernhollow.example" Role="Editor" />
    </arc:ShareAccess.People>
</arc:ShareAccess>
```

每个更改都会立即在控件中生效，并触发一个事件供应用保存：`PeopleInvited`（新加入的人和他们的角色）、`RoleChanged`、`AccessRemoved` 和 `LinkAccessChanged`。`LinkScope`、`LinkRole` 和 `InviteRole` 是双向的。按下 Done 时触发 `DoneRequested`，在这里关闭对话框。

### 先发送邀请

设置 `InviteHandler`，在成员加入列表之前先发送邀请。它运行期间 Invite 按钮显示加载动画；如果它抛出异常，异常消息会显示在地址框下方，标签保留以便再试一次。下面的示例会等待 0.9 秒，并拒绝任何包含“blocked”的地址。

<Demo name="InviteHandler" />

### 窄对话框

宽度小于 460px 时，角色选择框和 Invite 按钮移到地址框下方，内边距减为 16px。

<Demo name="Narrow" />

### 减少动态效果

在减少动态效果下，行和链接角色选择框只淡入淡出而不移动，标签交叉淡化。

<Demo name="ReducedMotion" />

### 结构

1. **标题**：使用展示字体的“Share “名称””。
2. **邀请行**：地址框（`TagInput`）、角色选择框（`Select`）和 Invite 按钮。
3. **成员**：52px 高的行，包含小号 `Avatar`、名字和邮箱（或“Invite sent”），以及带菜单的角色按钮；列表超过 260px 时可以滚动。
4. **General access**：一个圆形图块，显示锁、人群或地球图标；范围选择框和一行说明；链接开放时还有链接角色选择框。
5. **底部**：`CopyButton`“Copy link”和 Done 按钮。

### 内容建议

- 用人们熟悉的名称称呼文档；标题最多两行。
- 有名字时就提供名字；没有名字的行只显示邮箱。
- `OrganizationName` 保持简短（“Fernhollow”，而不是正式全称）：它会出现在“Anyone at …”中。

### 常见问题

**什么算是一个邮箱地址？** 包含一个 @，域名中有点号且最后一段至少两个字母的文字。从邮件客户端粘贴的“Name &lt;address&gt;”会保留其中的地址。比较地址时不区分大小写。

**可以用代码邀请吗？** `InviteAsync` 做的事和按钮一样；`ChangeRole` 和 `RemovePerson` 作用于一行。

## API 参考

<!-- api: ShareAccess -->

<!-- api: ShareAccessPerson -->

<!-- api: ShareAccessLabels -->

<!-- api: ShareAccessInvitedEventArgs -->

<!-- api: ShareAccessRoleChangedEventArgs -->

<!-- api: ShareAccessRemovedEventArgs -->

<!-- api: ShareAccessLinkChangedEventArgs -->

## 无障碍

- 标题是二级标题。地址框、选择框和列表都有名称（“Add people by email”“Role for new people”“Who can open the link”“People with access”）。
- 每个角色按钮的名称包含这个人和角色，例如“Role of Tobias Brandt: Editor”；它的菜单勾选当前角色，最后一项是“Remove access”。
- 被拒绝的地址会在地址框下方显示原因。一个礼貌的实时区域会播报邀请、角色修改、移除和失败。
- 用键盘移除某人后，焦点移到下一个人的角色按钮；没有其他人时移到地址框。
- 范围图块只是装饰；选择框和说明行表达含义。

### 键盘

| 按键 | 作用 |
|---|---|
| Enter、逗号 | 在地址框中，把输入的地址变成标签 |
| 空输入时按 Enter、Ctrl/Cmd+Enter | 发送邀请 |
| Backspace | 在空的地址框中，先选中最后一个标签，再按一次移除它 |
| Tab | 依次经过地址框、角色选择框、Invite、每个角色按钮、链接选择框、Copy link 和 Done |
| Enter / Space / Down | 在角色按钮或选择框上，打开它的菜单 |
| Escape | 关闭打开的菜单或选择框 |

## 动效

- 新成员上升 6px 进入一个逐渐展开的行；被移除的人淡出，同时这一行收起。
- 角色按钮的标签使用 Arc 的标签切换；范围图标弹出切换，说明行同样切换，链接角色选择框淡入，同时它的位置变宽。
- 标签、选择框、菜单和按钮保留它们自己的动效。
- 减少动态效果时只保留淡入淡出；关闭动效时一切立即完成。

## 响应式

- 对话框最宽 520px，可以随容器变化。宽度小于 460px 时邀请行堆叠（地址框在上，角色和 Invite 在下），内边距为 16px。
- 名字和邮箱以省略号结束；角色按钮保持自己的大小。
- 每个按钮至少 36px 高，角色菜单轻点即可打开；没有任何功能依赖悬停。

## 相关组件

- `TagInput`：单独的地址框。
- `Select`：角色和范围选择框。
- `AvatarGroup`：用紧凑的一行显示谁有权限。
- `CopyButton`：复制一个值并给出反馈。
- `Dialog`：承载共享对话框。
