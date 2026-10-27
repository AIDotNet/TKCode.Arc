---
title: Invite people
summary: 按姓名或邮箱邀请人员加入工作区，可为每个人指定角色，并按套餐显示席位用量；头像会从建议飞入标签，再飞入待接受的邀请列表。
source-hash: 35edc5dc76be
---

<Demo name="Preview" />

## 何时使用

`InvitePeople` 是“Invite people”按钮背后的表单：一个可输入姓名和地址的输入框、一项席位检查，以及仍在等待接受的邀请。在通讯录中找到的人会变成带头像和角色的标签；无法邀请的内容会变成红色标签，并说明原因。发送后，标签会移入待接受的邀请列表，每条邀请都可以重新发送或撤回；邀请被接受后会离开列表，并加入顶部的团队头像中。

- **一次邀请多个人。** 输入几个字母即可从通讯录中选人，也可以输入地址，或从表格或邮件中粘贴整份名单：逗号、分号和换行都会分隔各条目。
- **需要付费的席位。** 用量条按套餐统计成员、待接受的邀请和新增标签。数量超出时，发送按钮会被暂停，并就地提供下一档套餐。
- **对话框或设置页。** 带边框时，本 Block 是最宽 600 的卡片；无边框时，它放在你自己的容器中。

## 何时不用

- **管理已经加入的人。** 成员的角色、移除和席位变更请使用 `Team members`。
- **编辑角色能做什么。** 请使用 `Roles and permissions`。
- **普通标签。** 当条目不是人员（标签、关键词、没有通讯录的收件人）时，请使用 `TagInput`。

## 用法

本 Block 派生自 Avalonia 的 `TemplatedControl`。人员是 `InvitePeoplePerson` 对象（姓名、邮箱、职位和可选的肖像；没有肖像时头像显示姓名首字母）。`Directory` 存放所有可被找到的人，`Members` 是团队，`PendingInvites` 是正在等待的邀请；通讯录中已在团队内或已有邀请的人，在建议中显示为不可选。`Roles` 和 `Plans` 分别填充角色菜单和席位用量条。

```xml
<arc:InvitePeople Title="Invite people"
                  Description="Bring people into Lumen Labs by name or email"
                  WorkspaceName="Lumen Labs">
    <arc:InvitePeople.Roles>
        <arc:InvitePeopleRole Name="Admin" Description="People, billing and settings" />
        <arc:InvitePeopleRole Name="Member" Description="Works on every project" IsDefault="True" />
        <arc:InvitePeopleRole Name="Guest" Description="Sees only shared projects" />
    </arc:InvitePeople.Roles>
    <arc:InvitePeople.Plans>
        <arc:InvitePeoplePlan Name="Basic" SeatLimit="10" />
        <arc:InvitePeoplePlan Name="Growth" SeatLimit="25" />
    </arc:InvitePeople.Plans>
    <arc:InvitePeoplePerson Name="Clara Weiss" Email="clara.weiss@lumen.example" Title="Brand designer" />
    <arc:InvitePeoplePerson Name="Mateus Rocha" Email="mateus.rocha@lumen.example" Title="Backend engineer" />
</arc:InvitePeople>
```

### 结构

- **页头**：`Title`、`Description` 和团队头像（最多五个，其余以“+N”胶囊表示）。在窄版 Block 中，头像移到标题下方。
- **输入框**：标签和文本框会按需要换行，输入框的高度以弹簧跟随变化。下方是提示文字，以及在设置了 `SampleText` 时用于粘贴它的按钮。
- **错误面板**：当有标签无法邀请时展开，每个标签一行，并带有一个全部移除它们的按钮。
- **席位**：“8 of 10 seats”、套餐名称、分为三段（成员、待接受、新增）的用量条和图例。超出上限时，数量和新增段变红，并有一行提示下一档套餐。
- **页脚**：重置按钮（当 `IsResetVisible` 时显示）和发送按钮，发送按钮的文字会统计标签数量。
- **待接受**：邀请列表，最新的在前，每条带有状态、重新发送按钮和撤回按钮。

### 添加人员

输入时按姓名或邮箱筛选通讯录（不会搜索职位）；可邀请的人排在前面，不可选的排在后面，最多五个（`MaxSuggestions`）。输入框为空时，先显示四个人（`SuggestedCount`）。不在通讯录中的完整地址会提供“Invite by email”。按 Enter 或点击会选中高亮的人；没有高亮时按 Enter、逗号或分号，会把文本变成标签。

粘贴一份名单（或调用 `AddText`）时，会识别通讯录中的姓名和地址、新地址以及 `Name <address>` 格式；它会跳过重复项，并把无法邀请的条目作为红色标签加入。输入框下方的提示会显示几秒钟的汇总。选中红色标签会把它的文本放回输入框以便修改。

### 发送

将 `SendAction` 设为发送按钮要执行的工作。它会收到新的待接受行（状态为 `Sending`）和一个取消令牌。每封邀请发出时调用 `MarkDelivered`；任务完成时仍在发送中的行会被标记为已发送。有人接受邀请时调用 `MarkJoined`：该行显示“Joined”，片刻后离开，此人加入 `Members`，其头像飞入页头。发生异常时，这些行会被收回，标签回到输入框。

```csharp
// Your mail call; here it only waits.
static Task SendInviteAsync(string email, string? role, CancellationToken token) => Task.Delay(300, token);

invite.SendAction = async (request, token) =>
{
    foreach (var pending in request.Invites)
    {
        await SendInviteAsync(pending.Person!.Email!, pending.Role?.Name, token);
        invite.MarkDelivered(pending);
    }
};
invite.InviteJoined += (_, e) => log.Info($"{e.Invite.Person?.DisplayName} joined");
```

当标签需要修正或席位超出时，发送按钮会被暂停（它仍可获得焦点并说明原因）；此时按下它，会让红色标签或超限提示抖动。`ResendAction`、`RevokeAction` 和 `UpgradeAction` 对行内按钮和升级按钮的作用方式相同。下面的演示让每第二次发送失败，并让撤回 Theo 的邀请失败，因此该行会回来。

<Demo name="Failure" />

### 状态与事件

`Entries` 列出标签，`SeatsUsed` 和 `IsOverLimit` 可以绑定，`CurrentPlan` 是双向的。`EntriesChanged`、`RoleChanged`、`InvitesSent`、`SendFailed`、`InviteJoined`、`InviteResent`、`InviteRevoked` 和 `PlanUpgraded` 会报告发生的事情。`Reset()` 让 Block 回到最初的样子，并触发 `ResetPerformed`。

<Demo name="EventLog" />

### 带边框与无边框

`IsFramed`（默认值）会绘制一张最宽 600 的卡片，带 1px 边框和 16 的圆角，页脚和待接受列表上方有分隔线。将其设为 false，可把本 Block 放进你自己的对话框或面板中。

<Demo name="Flat" />

### 窄容器

本 Block 会读取自身的宽度。宽度为 480 或更小时，内边距从 20 收紧到 16；当内容窄于 440 时，待接受的行会把状态放在姓名下方，建议中则省略职位。

<Demo name="Narrow" />

### 其他文字

所有文字都在 `Labels`（`InvitePeopleLabels`）中，默认是英文：占位符、提示、错误原因、席位行、按钮名称和每条朗读通知。带数值的文字是函数，因此语序可以改变。

## API 参考

<!-- api: InvitePeople -->

<!-- api: InvitePeoplePerson -->

<!-- api: InvitePeopleRole -->

<!-- api: InvitePeoplePlan -->

<!-- api: InvitePeoplePendingInvite -->

<!-- api: InvitePeopleEntry -->

<!-- api: InvitePeopleSendRequest -->

## 无障碍

- 文本框名为“Email or name”，并由提示文字描述。建议列表不可获得焦点：高亮移动时焦点仍留在文本框中，不可选的人会说明原因。
- 红色标签的按钮名为“Edit {entry}. {reason}”；移除按钮会说明移除的是哪个标签；角色按钮会说明这是谁的角色以及当前是什么。
- 席位用量条名为“Seats used”，其值读作“8 of 10 seats”（并附上超出上限的数量）。
- 发送按钮以数量命名（“Send 3 invites”）。被暂停时，它会说明原因：错误面板的标题，或超出上限的席位数。
- 撤回之后，焦点移到“待接受”标题。团队头像是一个组（“Team, 6 people”）；“+N”胶囊读作“and N more”。
- 礼貌模式的实时区域会播报每一步：添加和移除的人、粘贴汇总、被标记的标签、角色变更、发送被暂停的原因、正在发送和已发送、被接受的邀请、重新发送、撤回、升级和重置。

### 键盘

- 在输入框中按 **Arrow Down** 打开建议（输入框为空时显示初始列表）；**Arrow Up** 和 **Arrow Down** 移动高亮并跳过不可选的人。**Enter** 添加高亮的人，没有高亮时把文本变成标签。**逗号**和**分号**会把文本变成标签。**Escape** 先关闭列表，再清空文本。
- 在空输入框中按 **Backspace** 会标记最后一个标签；再按一次 **Backspace** 将其移除。输入任何内容都会清除标记。
- **Tab** 依次经过每个标签的角色按钮和移除按钮，然后是输入框、粘贴按钮、错误面板的按钮、升级按钮、重置、发送，以及每个待接受行的重新发送和撤回按钮。
- 在角色按钮上，**Enter**、**Space** 或 **Arrow Down** 会打开角色菜单，焦点落在当前角色上。**Arrow Up** 和 **Arrow Down** 循环移动，**Home** 和 **End** 跳转，**Enter** 或 **Space** 选中，**Escape** 关闭；焦点回到角色按钮。

## 动效

- **头像飞行**：从建议飞入新标签，发送时从标签飞入它的待接受行，被接受后从该行飞入页头的团队，每次都用一根弹簧（约 600 ms），出发处此时已经空出。
- **布局**：标签增减时，标签和文本框以弹簧移到新位置，输入框的高度及其下方的一切以平滑弹簧跟随，待接受行也以同样方式展开和收起（收起的行还会在 130 ms 内淡出）。
- **建议**：列表上升 4 px 并从 0.98 放大；高亮在行与行之间滑动。角色菜单从它的按钮以同样的方式进入。
- **数字与文字**：席位数、待接受数量和发送按钮上的数量会滚动；变化的文字（角色、套餐、发送按钮的名词、行的状态、“Sent again”）带短暂模糊交叉淡入淡出，同时宽度以弹簧变化。
- **拒绝反馈**：红色标签出现时抖动一次；发送被暂停时按下它，红色标签（或超限提示）会抖动：6、6、4、4、2 px，历时 420 ms。
- 减少动效会让一切立即就位，只保留短暂的淡入淡出和颜色变化；没有飞行、抖动和弹簧。关闭时所有变化立即生效。

## 相关

- `Team members`、`Roles and permissions`、`Team directory` 和 `Profile page`，即其他 People 区块。
- `TagInput` 用于普通标签，`Avatar` 和 `AvatarGroup` 用于肖像，`UsageMeter` 用于独立的用量条。
