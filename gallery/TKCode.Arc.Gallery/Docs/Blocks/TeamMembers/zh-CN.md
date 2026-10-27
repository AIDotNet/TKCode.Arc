---
title: Team members
summary: 工作区的成员页：输入或粘贴邮箱地址即可邀请，地址会飞入待接受的邀请列表并滚动更新席位数；还可更改角色、搜索，并通过行内确认移除成员。
source-hash: 631db0a7fb43
---

<Demo name="Preview" />

## 何时使用

`TeamMembers` 是工作区或团队设置页面中的成员页。它在同一处回答三个问题：谁拥有访问权限以及是什么角色，谁已被邀请但尚未加入，以及还剩多少席位。邀请很快捷：输入或粘贴地址，选择角色，发送。发出的地址会从输入框飞入待接受的列表，席位数同时向上滚动，让人清楚看到邀请去了哪里。

- **工作区或组织设置。** 显示成员及其角色、最近活跃时间和在线状态，标出所有者，并把当前用户标记为 "You"。
- **一次邀请多人。** 按 Enter、逗号、分号或空格，地址会变成标签，粘贴的列表会被一次性拆分。拼写有误的地址会连同错误提示留在输入框中；已属于某人的地址会被拒绝并说明原因。
- **有席位上限的套餐。** 成员和待接受的邀请各占一个席位。发送按钮会拒绝超出空闲席位，并说明需要删去多少个地址；工作区已满时，席位条会变为警告色。

## 何时不用

- **用于浏览的人员通讯录。** 个人资料、部门和联系方式请使用 Team directory 区块；本区块管理的是访问权限。
- **定义每个角色可以做什么。** 请使用 Roles and permissions 区块。这里的角色只是一个名称加一行简短文字，从菜单中选取。
- **通用的标签输入。** 在邀请流程之外输入标签或收件人，请使用 `TagInput`。
- **大型组织。** 行未做虚拟化。成员达到数百人时，请一次只显示一页成员（并在服务器端搜索）。

## 用法

本区块派生自 Avalonia 的 `TemplatedControl`。成员是 `TeamMembersPerson` 对象，待接受的邀请是 `TeamMembersInvite` 对象，两者都是可以绑定的 `AvaloniaObject`。角色是键（默认为 `"admin"`、`"member"`、`"viewer"`），由 `TeamMembersRoleOption` 描述；所有者没有角色菜单。

```xml
<arc:TeamMembers Title="Members"
                 Description="Choose who can work in the Northwind Lab workspace."
                 WorkspaceName="Northwind Lab"
                 SeatLimit="10">
    <arc:TeamMembersPerson Name="Lena Hart" Email="lena@northwind.example" IsOwner="True" IsCurrentUser="True"
                           IsOnline="True" LastActive="Active now" />
    <arc:TeamMembersPerson Name="Theo Brandt" Email="theo@northwind.example" Role="admin" LastActive="Active now" />
    <arc:TeamMembersPerson Name="Ines Kowal" Email="ines@northwind.example" Role="member" LastActive="Active 3 hours ago" />
</arc:TeamMembers>
```

没有处理程序时，每个操作都会立即生效。请用异步处理程序把每个操作连接到你的服务器；处理程序运行时区块会显示忙碌状态，抛出异常时则保持原状态：

```csharp
public interface IMembersApi
{
    Task InviteAsync(string workspace, IReadOnlyList<string> emails, string? role, CancellationToken cancel);
    Task ResendAsync(string inviteId, CancellationToken cancel);
    Task RevokeAsync(string inviteId, CancellationToken cancel);
    Task RemoveAsync(string memberId, CancellationToken cancel);
    Task SetRoleAsync(string memberId, string? role, CancellationToken cancel);
}

members.SendInvitesAction = async (request, cancel) =>
    await api.InviteAsync(workspaceId, request.Emails, request.Role, cancel);
members.ResendInviteAction = (invite, cancel) => api.ResendAsync(invite.Id!, cancel);
members.RevokeInviteAction = (invite, cancel) => api.RevokeAsync(invite.Id!, cancel);
members.RemoveMemberAction = (person, cancel) => api.RemoveAsync(person.Id!, cancel);
members.ChangeRoleAction = (change, cancel) => api.SetRoleAsync(change.Member.Id!, change.NewRole, cancel);
```

### 结构

- **页头**：`Title`、`Description` 和席位计量（"7 of 10 seats"，下方是一条 4px 的进度条）。用 `IsSeatMeterVisible` 可隐藏计量。
- **邀请表单**：带标签的地址输入框、"Invite as" 角色选择器（`InviteRole`，双向）和发送按钮，按钮文字为 "Send invite" 或 "Send 3 invites"。输入框下方的一行显示提示、席位警告或错误。`IsInviteVisible` 会隐藏整个表单。
- **成员**：带数量的标题、搜索框（`IsSearchVisible`），以及每位成员一行：带在线状态点的头像、姓名和邮箱、最近活跃时间、角色菜单（或 "Owner"）和移除按钮。
- **待接受的邀请**：带数量的标题，以及每个邀请一行：以地址首字母为缩写的虚线头像、地址、"role · detail"，以及 Resend 和 Revoke 按钮。
- **页脚**：最近一次操作的结果（"2 invites sent as Member"）。它会保留到下一次操作；`StatusText` 保存该文字。`IsFooterVisible` 会隐藏页脚，但结果仍会被朗读。

### 邀请

每段提交的文字都会单独判断，不区分大小写：

1. 不是完整地址（只有一个 @、域名带点、最后一段至少两个字母）：它留在输入框中，输入框变红，下方的一行给出说明。再次输入会清除错误。
2. 已经是标签：静默丢弃。
3. 是某位成员的地址：丢弃，下方的一行会指出该成员。
4. 是某个待接受邀请的地址：丢弃，并显示错误。
5. 其他情况都会作为标签添加到末尾。

`EmailValidator` 可替换地址检查。在空输入框中按 Backspace 会移除最后一个标签，在空输入框中按 Enter 则发送。仍在输入的有效地址会计入按钮文字，并在发送邀请时变成标签。`AddEmails`、`RemovePendingEmail`、`ClearPendingEmails` 和 `SendInvitesAsync` 可在代码中完成同样的操作；`PendingEmails` 列出这些标签。

`SendInvitesAction` 收到一个 `TeamMembersSendRequest`，其中包含地址、角色键，以及区块即将添加的 `TeamMembersInvite` 对象（如果你的服务器返回了 `Id`，请填入）。处理完成后，这些邀请会加入待接受列表，并触发 `InvitesSent`。

### 席位

`SeatsUsed` 是成员加上待接受的邀请，`SeatsRemaining` 是 `SeatLimit` 的剩余部分。只有标签会占用空闲席位：标签多于空闲席位时，发送按钮被禁用，下方的一行会要求删去一些；席位全部占满时，进度条变为警告色，下方的一行也会这样说明。撤回邀请或移除成员会立即释放一个席位。

### 绑定集合与事件

`MembersSource` 和 `InvitesSource` 接受你自己的集合并跟随其变化。当它们是可写列表时，区块会自行添加已发送的邀请，并移除被撤回的邀请和被移除的成员；对于只读集合，请在 `InvitesSent` 处理程序中添加邀请。每个操作都会引发一个携带变更内容的事件（`InvitesSent`、`InviteResent`、`InviteRevoked`、`MemberRemoved`、`MemberRoleChanged`），并执行对应的命令。`SearchText` 和 `InviteRole` 是双向的。

<Demo name="Bound" />

### 服务器拒绝时

抛出异常的处理程序会保持之前的状态，并引发 `ActionFailed`，其中包含操作、操作对象和消息：

- **发送**：标签保留，按钮恢复，输入框下方的一行显示错误（页脚也会显示）。
- **重新发送**：按钮恢复为 Resend，页脚显示错误。
- **撤回**和**移除**：该行保留（确认仍保持打开），页脚显示错误。
- **角色更改**：新角色立即显示，处理程序抛出异常时会恢复。

文字取自异常的消息、`ErrorFormatter` 的文字，或在没有消息时使用内置的句子。取消（区块离开窗口）不会被报告。

<Demo name="Failures" />

### 移除成员

移除按钮会把该行就地变成确认状态：该行着色，"Remove Ines Kowal?" 和一行关于将失去访问权限的说明取代姓名，Cancel 和 Remove 取代各列。同一时间只有一行在询问。按 Escape 或 Cancel 会返回，并把焦点还给移除按钮；移除之后，焦点移到下一行的移除按钮。`RequestRemove`、`CancelRemove` 和 `RemoveMemberAsync`（无需确认）可在代码中完成同样的操作。

### 窄容器

区块会读取自身的宽度。宽度不超过 640 时，最近活跃时间的文字移到邮箱下方；600 时，页头和邀请行堆叠，搜索框独占一行，内边距从 20 收紧到 16；480 时，待接受邀请的按钮移到地址下方；420 时，每个成员行拆分为上方的人员信息，以及下方的最近活跃时间、角色和移除按钮。

<Demo name="Narrow" />

### 只读

`IsReadOnly` 把列表显示给无权管理的人：没有邀请表单，角色以文字显示，没有移除、重新发送或撤回按钮。搜索仍然可用。`IsFramed="False"` 让区块平铺在页面上，而不是放在卡片中。

<Demo name="ReadOnly" />

### 文字

区块显示或朗读的每一段文字都在 `Labels` 中，这是一个带英文默认值的 `TeamMembersLabels`；带数值的文字是函数，因此在其他语言中可以调整语序。角色名称和描述来自 `Roles`。

## API 参考

<!-- api: TeamMembers -->

<!-- api: TeamMembersPerson -->

<!-- api: TeamMembersInvite -->

<!-- api: TeamMembersRoleOption -->

<!-- api: TeamMembersLabels -->

<!-- api: TeamMembersSendRequest -->

<!-- api: TeamMembersRoleChange -->

## 无障碍

### 键盘

| 按键 | 操作 |
| --- | --- |
| Enter | 在输入框中：把文字变成标签；输入框为空时则发送。 |
| , ; Space | 在输入框中：把文字变成标签。 |
| Backspace | 在空输入框中：移除最后一个标签。 |
| Enter, Space, Up, Down | 在角色选择器上：打开菜单，并聚焦当前角色。 |
| Up, Down, Home, End | 在角色菜单中：移动；Enter 或 Space 选择，Escape 关闭。 |
| Escape | 在请求确认移除的行中：取消。在搜索框中：清空。 |

### 屏幕阅读器与焦点

- 区块是一个以标题命名的分组，标题为 2 级标题；两个分区标题为 3 级，并为各自的列表命名。
- 席位条是名为 "Seats" 的只读进度条，其描述读作 "7 of 10 seats used"。
- 输入框由 `Labels.EmailFieldName` 命名。每个标签是以其地址命名的列表项，带有一个名为 "Remove … from the invite list" 的移除按钮。
- 输入框下方的一行在出现警告或错误时会以礼貌方式朗读；每个操作的结果以礼貌方式朗读，失败则以强制方式朗读。
- 角色选择器是名为 "Role for <name>"（或 "Invite as"）的组合框，其值为角色；菜单项会报告哪一项被选中。
- Remove、Resend 和 Revoke 按钮以人员或地址命名；重新发送之后，按钮会说明邀请已再次发出。
- 确认打开时焦点移到 Cancel，关闭时回到移除按钮，移除或撤回之后再移到下一行。

## 动效

- **标签**弹入（缩放 0.9 → 1，使用快速弹簧，并伴随 100 ms 的淡入和模糊）；输入框和其他标签以平滑弹簧滑到新位置。被移除的标签在 130 ms 内缩小并淡出。
- **发送**：处理程序运行时按钮旋转。随后每个地址沿弹簧（视觉时长 0.5 s，无回弹）从其标签飞向新的待接受行，飞行载体是一个色调逐渐淡出的胶囊；新行以平滑弹簧展开，虚线头像在 170 ms 后依次弹入，间隔 100 ms，席位数和各数量随之滚动。
- **行**通过在平滑弹簧上展开和收起来进入与离开（离开的行在 120 ms 内淡出）。搜索会收起不匹配的行，并以 200 ms 的淡入把它们带回来。
- **原处变化的文字**（输入框下方的一行、待接受行的详情、页脚）带着模糊升起离开，新文字随后升起进入；输入框下方的一行还会用弹簧调整其高度。更改后的角色逐字变形。
- **席位**：进度条的填充跟随平滑弹簧，并在 240 ms 内变为警告色。
- **角色菜单**：从其触发器以短暂的缩放和淡入打开，关闭更快；箭头翻转。
- 减少动态效果会保留发送按钮的旋转和短暂的交叉淡入淡出；标签、行、飞行、数量和进度条则直接跳转。关闭时所有变化立即生效。

## 相关

- Invite people、Team directory 和 Roles and permissions，是其他 People 区块。
- `Avatar`、`SearchField`、`TextMorph` 和 `Button`，组成本区块的控件。
- `TagInput`，用于邀请流程之外的标签。
