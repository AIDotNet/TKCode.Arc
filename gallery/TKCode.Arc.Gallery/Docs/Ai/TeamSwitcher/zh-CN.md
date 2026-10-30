---
title: Team switcher
summary: 工作区切换器：触发器显示当前工作区的标志和套餐，菜单可以搜索你的工作区，为每个工作区显示你的角色和 Cmd/Ctrl+数字快捷键，并能就地创建或加入工作区。
source-hash: e7beb3710fa5
---

<Demo name="Preview" />

## 何时使用

`TeamSwitcher` 放在应用侧边栏顶部或页眉中，让用户在自己所属的组织、团队、项目或店铺之间切换。触发器显示用户当前所在的位置：
工作区的标志、名称和套餐。菜单可以按名称、套餐或角色查找工作区，为每个工作区显示角色和键盘快捷键，并且无需离开页面就能创建或加入工作区。
切换时，触发器会经过一个短暂的加载状态，交叉淡化到新的工作区。

- **多租户应用**：一个账号属于多个组织或团队。
- **账号、项目或店铺选择器**：需要搜索、角色和键盘快捷键。
- **创建工作区或接受邀请**：不必把用户带到另一个页面。

## 何时不用

- **表单中一个简短的固定列表**：使用 `Select` 或 `SegmentedControl`。
- **跨页面和命令的全局导航**：使用命令面板。
- **同一份数据的两三种视图**：使用 `Tabs`。

## 用法

添加你的工作区，并把当前工作区的 id 绑定到 `Value`。把 `SwitchHandler` 接到加载工作区的逻辑上（切换会话、预取路由）；在你的任务完成之前，
触发器会以加载状态显示新的工作区。你没有提供的每个委托都会被模拟。没有工作区（也没有下文的模拟）时，触发器显示 "No workspace"，菜单在 Create workspace 和 Join with invite 上方显示没有工作区（上方的 Empty 选项卡）。

```xml
<arc:TeamSwitcher xmlns:arc="https://github.com/AIDotNet/TKCode.Arc" Value="acme">
    <arc:TeamSwitcherWorkspace Id="acme" Name="Acme" Plan="Enterprise" Members="214" Role="Admin" />
    <arc:TeamSwitcherWorkspace Id="acme-labs" Name="Acme Labs" Plan="Pro" Members="12" Role="Owner" />
</arc:TeamSwitcher>
```

```csharp
switcher.SwitchHandler = (workspace, token) => session.SwitchAsync(workspace.Id, token);
switcher.ValueChanged += (_, e) => navigation.GoHome(e.Id);
```

### 结构

1. **触发器**：工作区的图块、名称及其下方的套餐行（“Enterprise · 214 members”），以及一个在加载工作区时变成加载圈的箭头。
   它高 52px，在不超过 360px 的范围内填满容器。其下的菜单与触发器同宽，但至少 288px，这样切换器放在窄侧边栏中时名称依然清晰可读。
2. **搜索**：输入时按名称、套餐或角色筛选列表。在列表中移动时焦点一直留在这里。
3. **行**：一次显示六行，每行 48px，超出后列表滚动，但不显示滚动条。每一行包含图块、名称和套餐行（两者都会截断）、你的角色、
   快捷键，当前工作区还有一个对勾。
4. **操作**：分隔线下方的“创建工作区”和“通过邀请加入”。
5. **高亮**：一个安静的色块，滑动到指针或键盘所指的行。

### 你的工作区

<Demo name="YourWorkspaces" />

每个 `TeamSwitcherWorkspace` 都有 `Id` 和 `Name`，还可以有 `Plan`、`Members` 和你的 `Role`。没有标志时，它的图块在一个方块中显示名称的
首字母，方块的色调取自主题的强调色和系列色（色调由名称决定，所以同一个工作区始终保持同一种颜色）。租户的品牌标志用 `Logo`
提供（24 × 24 框内的填充几何图形），并放在 `TileBackground` 上；也可以把标志图片（位图或 `DrawingImage`）放进 `LogoImage`，
它会铺满整个图块。`TeamSwitcherTile` 可以单独绘制同样的图块，例如在设置页中。

### 切换

点选一行、在行上按 Enter 或按它的快捷键，都会关闭菜单并开始切换。`SwitchHandler` 运行期间，触发器立即显示新的工作区，
带有闪烁的“Switching…”和加载圈；任务完成后再显示它的套餐，此时 `Value` 才会改变并触发 `ValueChanged`。如果任务抛出异常，
触发器会回到之前的工作区并显示异常消息几秒钟，同时触发 `SwitchFailed`。较新的切换会取消较早切换的令牌；你自己设置 `Value`
会放弃正在进行的加载。

调用 `SwitchToAsync(id)` 可以用同样的方式在代码中切换。

### 创建与加入

“创建工作区”和“通过邀请加入”会打开内嵌表单，以交叉淡化的方式替换列表；面板保持高度不变。搜索没有匹配项时，按 Enter
会打开“创建工作区”，并把搜索文字作为名称填好。

```csharp
switcher.CreateHandler = async (name, token) =>
{
    var created = await api.Workspaces.CreateAsync(name, token);   // throw to show a message under the field
    return new TeamSwitcherWorkspace(created.Id, created.Name, "Free", 1, "Owner");
};
switcher.JoinHandler = (invite, token) => api.Invites.AcceptAsync(invite, token);
switcher.WorkspaceAdded += (_, e) => myWorkspaces.Add(e.Workspace);
```

返回新的工作区即可添加并切换到它，返回 null 则只关闭菜单。切换器会立即显示它创建或加入的工作区；也请在 `WorkspaceAdded`
中把它加入你自己的列表，切换器会按 id 去重。没有 `CreateHandler` 时，模拟的创建会用这个名称创建一个只有一名成员的工作区；没有 `JoinHandler` 时，模拟的加入会拒绝所有邀请，除非模拟中列出了它。

用于演示或原型时，让 `Workspaces` 保持为空，并给切换器一个 `TeamSwitcherSimulation`：它的工作区、模拟加入所接受的邀请（每个都是一个 `TeamSwitcherSimulationInvite`：一个邀请码，可以单独匹配，也可以位于链接末尾，以及它加入的工作区；加入视图会列出这些邀请码），以及模拟创建时赋予的套餐和角色。你自己的 `Workspaces` 会让它让位，把 `Simulation` 设为 null 会清空切换器。预览中的工作区和邀请（ADATUM-4821、TAILSPIN-0917）是虚构的演示数据。

```csharp
switcher.Simulation = new TeamSwitcherSimulation
{
    Workspaces = [new TeamSwitcherWorkspace("acme", "Acme", "Enterprise", 214, "Admin")],
    Invites = [new TeamSwitcherSimulationInvite { Code = "ACME-1234", Workspace = new TeamSwitcherWorkspace("acme-labs", "Acme Labs") }],
    CreatedPlan = "Free",
    CreatedRole = "Owner",
};
```

### 快捷键

<Demo name="Sidebar" />

前九个工作区在 macOS 上对应 Cmd+1 到 9，在其他系统上对应 Ctrl+1 到 9。焦点在切换器内部时它们有效；
设置 `UsesGlobalShortcuts="True"` 后，在窗口的任何位置都有效。

### 禁用

<Demo name="Disabled" />

禁用的切换器仍然显示工作区，但不会打开，也会忽略快捷键。

### 自定义文字

`Labels` 包含切换器显示或朗读的所有文字。替换整个对象即可本地化或改写：

```csharp
switcher.Labels = new TeamSwitcherLabels
{
    Search = "Arbeitsbereich suchen…",
    CreateWorkspace = "Arbeitsbereich erstellen",
    PlanLine = (plan, members) => $"{plan} · {members} Mitglieder",
};
```

### 减弱动效

减弱动效时，菜单、触发器的各部分和表单都以简单的淡入淡出切换，高亮直接移动而不滑动。关闭动效时一切立即改变。

### 内容准则

- 用大家熟悉的名称命名工作区，而不是它的 id。
- 套餐用一个词（“Pro”“Enterprise”）；角色也一样（“Owner”“Admin”“Member”“Guest”）。
- 错误消息要说明发生了什么以及该怎么做：“That invite has expired. Ask for a new one.”

### 最佳实践

- 让切换任务尽量短，交给触发器显示加载状态；不要阻塞页面。
- 把切换器放在下方约有 450px 空间的位置，或者放在菜单可以溢出的地方。
- 保持列表顺序稳定，让快捷键始终不变。

### 常见问题

**应用可以打开菜单吗？** 设置 `IsOpen`，这样打开不会移动焦点。`Open()` 会打开菜单并聚焦搜索框。

**模拟的操作会做什么？** 没有 `SwitchHandler`、`CreateHandler` 或 `JoinHandler` 时，它会等待 `SimulatedDelay`（650ms），并播报没有发送任何内容。

## API 参考

<!-- api: TeamSwitcher -->

<!-- api: TeamSwitcherWorkspace -->

<!-- api: TeamSwitcherTile -->

<!-- api: TeamSwitcherLabels -->

<!-- api: TeamSwitcherSimulation -->

<!-- api: TeamSwitcherSimulationInvite -->

<!-- api: TeamSwitcherValueChangedEventArgs -->

<!-- api: TeamSwitcherSwitchFailedEventArgs -->

<!-- api: TeamSwitcherWorkspaceAddedEventArgs -->

<!-- api: TeamSwitcherOpenChangedEventArgs -->

<!-- api: TeamSwitcherAddKind -->

## 无障碍

- 触发器是一个按钮，会报告菜单是否打开。它的名称读出工作区及其套餐（“Northwind, Enterprise plan”），加载工作区时状态为“Switching…”。
- 方向键在列表中移动时，搜索框一直保持焦点；它的帮助文本是高亮行的名称，因此屏幕阅读器无需让焦点离开输入框就能读到它。
- 当前工作区用对勾以及名称中的“current”标出，而不仅仅依靠颜色。错误同时显示警告图标和文字。
- 一个礼貌的实时区域会播报切换开始、切换结果，以及工作由模拟操作完成的情况（“Simulated, nothing was sent”）。没有工作区时，触发器的名称为 "No workspace"。
- 用 Escape 关闭或选择后，焦点回到触发器。用 Tab 离开或点击外部会关闭菜单。

### 键盘

| 按键 | 操作 |
|---|---|
| Enter、Space、下方向键 | 在触发器上打开菜单，并把焦点移到搜索框 |
| 输入 | 按名称、套餐或角色筛选；没有匹配时，Enter 打开“创建工作区”并填好名称 |
| 下方向键、上方向键 | 在工作区和操作之间移动高亮，到两端时循环 |
| Home、End | 搜索为空时跳到第一项或最后一项 |
| Enter | 切换到高亮的工作区，或打开“创建工作区”或“通过邀请加入” |
| Cmd/Ctrl+1 到 9 | 切换到列表中对应位置的工作区（在切换器内部，或开启全局快捷键后在任何位置） |
| Escape | 先从表单返回，再清空搜索，最后关闭菜单并把焦点还给触发器 |
| Tab | 离开菜单并将其关闭 |

## 动效

| 变化 | 完整 | 减弱 | 关闭 |
|---|---|---|---|
| 菜单打开 | 在 180ms 内从 98% 淡入并放大 | 120ms 淡入 | 立即 |
| 菜单关闭 | 在 120ms 内淡出并缩小到 98% | 120ms 淡出 | 立即 |
| 高亮移动 | 沿 0.24s 无回弹弹簧滑动；在原位出现，绝不从顶部滑入 | 直接跳到位 | 直接跳到位 |
| 切换 | 标志、名称和套餐行各自交叉淡化：100ms 淡出，160ms 淡入并上升 2px；随后显示闪烁文字和加载圈 | 120ms 淡入淡出，无闪烁 | 立即 |
| 打开创建或加入 | 视图交叉淡化（100ms 淡出，160ms 淡入），并沿前进方向平移 6px | 120ms 淡入淡出 | 立即 |

没有任何东西改变尺寸：触发器在所有状态下保持同样的高度，菜单面板在搜索或打开表单时也保持同样的高度。

## 相关组件

- `DropdownMenu`：锚定在触发器上的一组聚焦操作。
- `Popover`：小型锚定浮层。
- `Avatar`：表示一个人的紧凑标识。
