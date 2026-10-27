---
title: Roles and permissions
summary: 角色与权限矩阵，带有会产生涟漪的分组复选框、锁定的所有者权限、角色复制，以及一次性保存全部更改的审阅栏。
source-hash: 88e5f5c8c024
---

<Demo name="Preview" />

## 何时使用

`RolesPermissions` 是工作区设置中的访问权限页面：角色排在顶部，权限按分组排在左侧，两者交汇处是一个开关。编辑过程中不会应用任何内容。每个被更改的单元格会出现一个小圆点，角色的表头也会出现一个，同时审阅栏从下方滑出，显示更改数量、更改涉及的角色，以及这些更改会影响多少人。用户可以一次性保存所有更改，也可以全部放弃。

- **产品设置中的基于角色的访问控制。** 类似所有者、管理员、成员和访客的角色，对应几组权限（文档、团队、账单、安全）。
- **在更改生效前先审阅。** 在保存之前，审阅栏会显示 "Lead and Member · reaches 13 people"，让用户在修改的同时就能看到更改的影响范围。
- **以现有角色为基础创建新角色。** 从表头菜单复制一个角色，调整副本所在的列，然后与其余更改一起保存这个新角色。

## 何时不用

- **把人分配到角色。** 矩阵决定角色能做什么，而不是谁拥有该角色。成员及其角色请使用 `Team members`。
- **少量彼此独立的设置。** 几个开关选项更适合在 `Settings page` 中以开关行的形式呈现。
- **数百个权限。** 每个单元格都会被绘制，没有虚拟化。请把长列表分组，并让大多数分组默认折叠（`IsExpanded="False"`），或者拆分页面。
- **用户自己的账号安全。** 密码、两步验证登录和会话应放在 `Security settings` 中。

## 用法

该区块派生自 Avalonia 的 `TemplatedControl`。角色是 `RolesPermissionsRole` 对象（包含键、名称、成员数和菜单中的一行说明；所有者使用 `IsLocked`），分组是包含 `RolesPermissionsItem` 权限的 `RolesPermissionsGroup` 对象，授权则是由角色键和权限键组成的一组 `RolesPermissionsGrant` 配对。`SavedGrants` 是已保存的状态；`Grants` 是当前状态，并在每次更改时写回。

```xml
<arc:RolesPermissions Title="Roles &amp; access" WorkspaceName="Northwind Studio" MemberCount="31"
                      RolesSource="{Binding Roles}" GroupsSource="{Binding Groups}"
                      SavedGrants="{Binding SavedGrants}" Grants="{Binding Grants, Mode=TwoWay}" />
```

```csharp
matrix.Roles.Add(new RolesPermissionsRole("founder", "Founder", 1, "Lena Hart", isLocked: true));
matrix.Roles.Add(new RolesPermissionsRole("lead", "Lead", 4, "Omar Diaz, Priya Rao and 2 others"));
matrix.Groups.Add(new RolesPermissionsGroup("team", "Team",
    new("team.invite", "Send invitations"), new("team.roles", "Assign roles"), new("team.remove", "Remove people")));
matrix.SavedGrants = new HashSet<RolesPermissionsGrant> { new("lead", "team.invite") };
```

### 结构

- **页头**：`Title`、一行包含工作区名称和两个计数的副标题（计数在变化时会滚动：成员数来自 `MemberCount`，或来自 `Members` 的数量；角色数来自列），以及右侧的成员头像。当头像在标题旁放不下时，它们会移到标题下方。
- **表头行**：标签列上方的 "Permission" 标题，以及每个角色一个按钮：角色名称（锁定的角色前面带锁图标）、成员数，以及在菜单打开时会旋转的箭头。
- **分组行**：一个显示分组名称和权限数量的展开按钮，以及每个角色一个复选框；当分组内所有权限都开启时为选中，只有部分开启时为半选。
- **权限行**：权限的名称（列较窄时会换行），以及每个角色一个开关。
- **页脚**：两个图例，锁和未保存的圆点。
- **审阅栏**：更改数量、角色和受影响的人数，以及 Discard 和 Save 按钮。有内容需要保存时，它会在页脚下方展开。

### 单元格、分组与锁定的角色

单元格可通过点击、**Space** 或 **Enter** 切换。分组复选框在关闭或半选时会把该分组的所有权限打开，在开启时则全部关闭；发生变化的单元格按分组顺序依次相隔 60 ms 变化。锁定角色的单元格始终开启：按下它会让方框抖动，并播报原因。切换回已保存值的单元格不再算作更改。

`GrantChanged` 会报告每个发生变化的单元格，包括角色、权限、新值、现在是否未保存，以及更改的来源（单元格、分组、撤销、Discard 或代码）。`GroupToggled` 在分组复选框之后触发。`SetGrant`、`ToggleGroup`、`IsGranted` 和 `IsUnsaved` 可在代码中完成同样的操作。

### 复制与移除角色

每个表头都会打开一个菜单，显示角色的名称和描述。**Copy role** 会在源角色右侧插入 "Guest (copy)"（该名称已被占用时为 "Guest (copy 2)"），并带有源角色的权限；它的列会弹簧般展开，焦点移到它的表头。副本在保存之前都是新建的：它显示为 "New role"，算作一项更改，它自己的编辑也会计数并显示圆点。**Undo edits** 会把该角色的单元格恢复原状；**Remove role**（仅限新角色）会再次关闭这一列。副本会插入 `Roles`，当 `RolesSource` 是可添加的列表时则插入 `RolesSource`。`RoleDuplicated`、`RoleDeleted` 和 `ChangesReverted` 会报告每一步；`DuplicateRole`、`DeleteRole` 和 `RevertRole` 可在代码中完成。

### 保存

Save 会等待 `SaveHandler` 处理一个 `RolesPermissionsSaveRequest`：其中包含保存后的所有授权、发生变化的单元格、角色和新角色，以及审阅栏显示的数量。运行期间按钮会旋转，矩阵不再接受更改（试图更改的用户会听到 "Saving, one moment"）。随后所有圆点一起消失，审阅栏在 `SavedHoldDuration`（1.6 s）内显示带对勾的 "Access updated" 并关闭，`SavedGrants` 成为新的基线。Discard 会一次性放弃所有更改，单元格逐行以涟漪方式恢复。

```csharp
matrix.SaveHandler = async (request, cancellation) =>
{
    // Send request.Grants (and request.AddedRoles) to your server; throw to report a failure.
    await Task.Delay(900, cancellation);
};
matrix.Saved += (_, e) => Debug.WriteLine($"{e.Request.ChangeCount} access changes saved");
```

发生异常时会保留所有更改：审阅栏显示 "Couldn't save" 和消息（或 `ErrorFormatter` 返回的文字），Save 变为 "Try again"，下一次编辑会清除该失败状态。`CancelSave` 会取消处理程序的令牌并返回审阅状态。

<Demo name="Failure" />

### 绑定状态与事件

`Grants` 是双向的：绑定它可让模型保持同步，也可以写入一个新的集合，从外部更改单元格。`PendingChangeCount`、`HasUnsavedChanges` 和 `SaveState` 跟随审阅栏。折叠分组时会把它的 `IsExpanded` 写回，因此页面可以折叠或展开所有分组。

<Demo name="Bound" />

### 带边框或平铺

`IsFramed`（默认值）会绘制一张带 1px 边框和 16 圆角的卡片，就像本页所示。将它设为 false 即可用于完整的设置页面：没有边框，页面边距为 32（宽度小于 540 时为 16）。

<Demo name="Flat" />

### 窄容器

该区块读取自身的内容宽度。宽度降到 760 时，标签列需要 200，每个角色需要 92；低于 540 时分别为 148 和 80，分组计数和表头箭头会消失，权限名称缩小到 12 并换行，审阅栏也会去掉 "pending" 一词。当角色放不下时，它们会在固定的标签列下方横向滚动，滚动时标签列会显示出边缘；复制的角色会自行滚动到可见位置。

<Demo name="Narrow" />

### 其他文字

所有可见和被播报的文字都在 `Labels` 中，这是一个带有英文默认值的 `RolesPermissionsLabels` 对象：副标题格式、"1 person" 和 "{0} people"、菜单行、副本名称、图例、审阅栏的文字和格式、屏幕阅读器名称以及每条播报。请用对象初始化器替换它，只设置需要更改的部分。

## API 参考

<!-- api: RolesPermissions -->

<!-- api: RolesPermissionsRole -->

<!-- api: RolesPermissionsGroup -->

<!-- api: RolesPermissionsItem -->

<!-- api: RolesPermissionsGrant -->

<!-- api: RolesPermissionsSaveRequest -->

<!-- api: RolesPermissionsSaveState -->

<!-- api: RolesPermissionsLabels -->

## 无障碍

- 矩阵是一个 **Tab** 停靠点。它从第一个角色表头开始，并记住用户最后操作的部分；随后 **Tab** 会移到审阅栏的按钮。
- **Left** 和 **Right** 沿行移动并在行的两端停住；在分组行上，从第一个复选框按 **Left** 会到达该分组的按钮。
- **Up** 和 **Down** 移到同一列中上方或下方的行；当该行在这一列没有单元格时，会对齐到最近的列（从分组按钮向下到第一个角色）；已折叠的行会被跳过。
- **Home** 和 **End** 移到该行的第一个和最后一个部分；**Ctrl+Home** 和 **Ctrl+End** 分别移到第一个角色表头和矩阵的最后一个单元格。
- **Space** 或 **Enter** 切换单元格、折叠分组或打开角色的菜单。**Escape** 关闭菜单并回到它的表头。
- Save 和 Discard 之后，焦点回到最后操作的单元格；复制之后回到新角色的表头；Remove role 之后回到左侧的表头。
- 矩阵是一个以标题命名的数据网格。分组复选框是名为 "Lead: all Team permissions" 的三态复选框；权限单元格是名为 "Lead: Send invitations" 的开关。锁定的单元格会追加 ", always on"；未保存的单元格会追加 ", unsaved"。
- 角色表头是带菜单的按钮，以角色和它的成员数命名；锁定的角色会追加 ", permissions are fixed"，有更改时会追加 ", unsaved changes"。分组按钮会报告它是否已展开。
- 每次开关、分组切换、复制、移除、撤销、放弃和保存都会以一句话礼貌地播报；保存失败则以强制方式播报。审阅栏是一个名为 "Unsaved changes" 的区域。
- 页头的头像是一个名为 "Workspace members" 的组。
- 从不需要悬停：列的底色带和行的着色既跟随指针，也跟随键盘焦点。

## 动效

- **单元格**：方框在 160 ms 内填充，对勾在约 100 ms 内自己画出。未保存的圆点以快速弹簧从 0.3 弹出。指针按下时方框按压到 0.88，并通过 CSS 弹簧回弹。分组复选框使其单元格以 60 ms 间隔产生涟漪；Discard 和 Undo edits 在整个矩阵中每行间隔 30 ms 产生涟漪；保存会一次性清除所有圆点。
- **锁定的单元格**在 330 ms 内左右抖动（3、−2、1 px）。
- **列的底色带**：在悬停或获得焦点的角色上于 160 ms 内淡入，并在角色之间以平滑弹簧滑动。悬停的行在 120 ms 内着色，它的固定标签列也一样。
- **分组**以平滑弹簧折叠和展开，并带有短暂的淡入淡出；箭头以略带弹性的弹簧旋转。
- **副本**在淡入的同时，其列以平滑弹簧从零宽度展开；被移除的角色会淡出并关闭。
- **审阅栏**：它的空间在 240 ms 内展开，同时栏从 24 px 下方以平滑弹簧升起并淡入。计数会滚动，"change" 和 "changes" 逐字母变形，第二行则以短暂的模糊切换。Save 按钮在保存时旋转，然后随着宽度的弹簧变化变形为 "Saved"。
- 减少动效和关闭动效时，所有更改立即生效，与 Arc 的减少模式一致。

## 相关

- `Team members`、`Invite people` 和 `Team directory`，即其他 People 区块。
- `Settings page`，用于带保存栏、由彼此独立的开关组成的页面。
- `Checkbox` 和 `Switch`，用于外观相同的单个控件。
- `DropdownMenu` 和 `AvatarGroup`，分别用于角色表头和页头。
