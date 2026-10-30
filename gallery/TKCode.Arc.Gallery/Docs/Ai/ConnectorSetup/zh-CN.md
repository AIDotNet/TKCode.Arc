---
title: Connector setup
summary: AI 助手的"连接你的工具"卡片：每个应用都在行内完成跳转、授权和首次同步，并以一条进度细线显示进度，随后列出获得的权限范围，并提供带行内确认的断开连接。
source-hash: f520864183fa
---

<Demo name="Preview" />

## 何时使用

`ConnectorSetup` 是让人们允许助手进入团队所用应用的卡片。每个应用占一行，包含它的标记、名称和一行状态。按下 Connect
会在这一行里走完整个握手过程：登录页打开、用户批准，首次同步一边计数已读取的内容，一边让一条 2px 细线沿着这一行填满。
已连接的应用显示其账户和一个 Connected 按钮，按下后展开助手拥有的权限范围，并提供 Disconnect 和行内确认。

- **助手的设置页**：人们在这里连接并查看工作区应用。
- **首次使用清单**：需要在不离开页面的情况下展示 OAuth 握手和首次同步。
- **复核访问权限**：查看助手在每个应用中拥有哪些权限范围，并撤销它们。

## 何时不用

- **按类别浏览几十个集成**：使用 `Integrations` 区块，这是一个带筛选的应用市场网格。
- **批准智能体想执行的某一个操作**：使用带审批的 `ToolCall`，或使用 `Dialog`。
- **展示智能体此刻正在调用的工具**：使用 `ToolCall`。

## 用法

在 `Connectors` 中传入你的应用，在 `ConnectedIds` 中传入已经连接的 id。设置 `ConnectHandler` 来运行你的 OAuth
流程：通过上下文报告各个步骤，返回账户、权限范围和同步数量；抛出带有简短可读消息（"Needs admin approval"）的异常，
即可显示该消息和 Retry。用户按下 Cancel 或卡片离开窗口时，上下文的令牌会被取消。

```csharp
setup.Connectors = apps;                  // ConnectorApp: Id, Name, Description, Kind or Glyph, Scopes, SyncCount, SyncNoun
setup.ConnectedIds = savedIds;
setup.ConnectHandler = async (app, context) =>
{
    var grant = await OpenOAuthPopupAsync(app.Id, context.CancellationToken);
    context.ReportAuthorizing();
    var approved = await grant.ResultAsync;
    var total = await StartFirstSyncAsync(app.Id, approved, synced => context.ReportSyncing(synced), context.CancellationToken);
    return new ConnectorConnectResult { Account = approved.Workspace, Scopes = approved.Scopes, Synced = total };
};
setup.DisconnectHandler = (app, token) => RevokeGrantAsync(app.Id, token);
setup.ConnectedIdsChanged += (_, e) => SaveConnectedApps(e.Ids);
```

没有设置处理程序时，流程是模拟的，不会发送任何内容，实时区域也会说明这一点。没有 `Connectors` 时，卡片列出它的
`Simulation` 中的应用；两者都没有时不列出任何应用。`AssistantName` 在文字中为助手命名；没有设置时文字会写成“your assistant”。

### 模拟

`ConnectorSetupSimulation` 是卡片自行播放的一组应用：`AssistantName`、`Apps`、已连接的 `Connected` id，以及脚本演示的 `Steps`。
卡片出现在屏幕上后，每个 `ConnectorSetupSimulationStep` 先等待它的 `Pause`，然后在其应用处于空闲或失败状态时连接它；带有 `Failure`
的步骤会在批准后失败，以便下一步重试。脚本演示会在卡片内第一次出现指针按下或按键时永久停止。你自己设置的 `Connectors`、
`ConnectedIds` 和 `AssistantName` 优先于它；把 `Simulation` 设为 null 会停止演示并清空列表。

```csharp
setup.Simulation = new ConnectorSetupSimulation
{
    AssistantName = "Atlas",
    Apps = apps,                          // the same ConnectorApp records as Connectors
    Connected = ["code"],
    Steps =
    [
        new ConnectorSetupSimulationStep("chat", TimeSpan.FromMilliseconds(900)),
        new ConnectorSetupSimulationStep("crm", TimeSpan.FromMilliseconds(1200), "Needs admin approval"),
        new ConnectorSetupSimulationStep("crm", TimeSpan.FromMilliseconds(1800)),
    ],
};
```

本页的演示从 `ConnectorSetupDemoData` 列出八种通用应用，账号和数量属于一家虚构的公司，助手也是虚构的。

### 你自己的流程

<Demo name="YourFlow" />

登录页等待批准时，处理程序调用 `ReportAuthorizing()`；首次同步推进时调用 `ReportSyncing(synced, total)`。这些报告
调用可以来自任何线程。`ConnectedIds` 是双向的，每次连接或断开完成后都会写回，同时引发 `ConnectedIdsChanged`。
`AppConnected` 带有账户、权限范围和同步数量；`AppDisconnected` 在撤销之后引发。

### 引导流程

<Demo name="Onboarding" />

`IsSearchable="False"` 为较短的列表去掉搜索框。`Title`、`Description` 和 `Footer` 替换标题、标题下的一行文字和隐私
说明（`IsFooterVisible="False"` 隐藏说明）。`AssistantName` 用于描述、权限范围标题、确认问题和页脚。

### 标记

<Demo name="Marks" />

应用以表示其类型的中性 Lucide 图标显示（`ConnectorMarkKind`：Chat、Code、Docs、Drive、Issues、Calendar、Mail、Crm、
Database、Analytics），绝不使用品牌标志。在 `ConnectorApp.Glyph` 中传入应用自己的 24 × 24 描边标记；两者都没有时，
显示名称的首字母。`ConnectorMark` 可以在设置界面的其他位置绘制同样的图块。

### 结构

1. **页头**：标题、其下的一行文字和搜索框（匹配名称、描述、账户和关键词）。
2. **行**：高 60px。标记、名称及其下的一行状态，以及一个 32px 的操作：Connect、Cancel、Retry 或 Connected。
3. **进度细线**：应用连接期间，沿行底边显示的 2px 细线。
4. **访问详情**：位于已连接的行下方，与其文字对齐："Atlas can" 和权限范围，然后是 Disconnect。
5. **页脚**：一把锁和一段简短的隐私说明。

### 状态

| 阶段 | 状态行 | 操作 |
|---|---|---|
| `Idle` | 应用的描述 | Connect |
| `Redirecting` | "Opening chat.example.com…" | Cancel |
| `Authorizing` | "Waiting for you to approve in Chat…" | Cancel |
| `Syncing` | "Syncing 1,240 messages"，数字滚动 | Cancel |
| `Connected` | "tailspin.chat · 1,240 messages synced" | Connected，展开详情 |
| `Failed` | 警示图标和原因 | Retry |
| `Disconnecting` | "Disconnecting…" | Connected（禁用） |

`GetPhase(id)` 读取应用所处的阶段；`StartConnect`、`CancelConnect`、`SetAccessOpen`、`Disconnect` 和
`StopAutoPlay` 用于在代码中驱动卡片。

### 自定义文字

`Labels` 包含卡片显示或朗读的每一个词。替换整个对象即可本地化或改写：

```csharp
setup.Labels = new ConnectorSetupLabels
{
    Title = "Verbinden Sie Ihre Tools",
    Connect = "Verbinden",
    Syncing = "Synchronisiere",
};
```

### 内容准则

- 用通俗的能力描述来写权限范围（"Post replies as Atlas"），不要写原始的 OAuth scope 字符串。
- 失败消息要简短、易懂（"Needs admin approval"）；它与警示图标共用一行。
- `SyncNoun` 使用复数形式（"messages"、"files"）。

### 最佳实践

- 在设置栏中只放一张卡片；它在 520px 以内随容器伸缩，最窄可到 360px。
- 把令牌传给流程中的每一次调用，这样 Cancel 才能真正停止它。
- 应用有数百个时，在你这边筛选，再把结果作为 `Connectors` 传入。

### 常见问题

**应用连接期间用户离开页面会怎样？** 令牌被取消，这一行回到静止状态。

**卡片会保存已连接列表吗？** 每次变化后它都会写回 `ConnectedIds`；在 `ConnectedIdsChanged` 中保存即可。

## API 参考

<!-- api: ConnectorSetup -->

<!-- api: ConnectorApp -->

<!-- api: ConnectorConnectResult -->

<!-- api: ConnectorConnectContext -->

<!-- api: ConnectorMark -->

<!-- api: ConnectorSetupLabels -->

<!-- api: ConnectorSetupSimulation -->

<!-- api: ConnectorSetupSimulationStep -->

## 无障碍

- 卡片是一个带标签的分组，有真正的标题；列表中每个应用一项，并按搜索显示出的应用编号。
- 一个礼貌型实时区域播报每一步：正在连接、已连接及同步数量、失败及原因、已取消和已断开。使用模拟流程时，会追加
  "Simulated, nothing was sent."
- 状态从不只靠颜色表达：失败时警示图标与消息同时出现，已连接的应用显示对勾和 Connected 一词。
- Connected 按钮会报告展开或折叠状态。Disconnect 会先询问；按下后焦点移到安全的回答 Cancel 上，破坏性按钮只会由用户
  自己聚焦。
- 正在淡出的内容会立即离开无障碍树，并且不再接收点击。

### 键盘

| 按键 | 操作 |
|---|---|
| Tab | 依次经过搜索框、每一行的 Connect、Cancel、Retry 或 Connected 按钮，以及已展开的详情 |
| Enter、Space | 连接、取消、重试，或展开、收起已连接应用的访问详情 |
| Escape | 清空搜索框，或退出断开连接的确认 |

## 动效

| 变化 | 完整 | 减弱 | 关闭 |
|---|---|---|---|
| 状态行或操作变化 | 旧内容在 100ms 内淡出，新内容在 160ms 内淡入并上升 2px，位于同一个单元格 | 立即 | 立即 |
| 步骤之间的进度 | 细线在 240ms 内缓动 | 立即 | 立即 |
| 同步时的进度 | 细线线性跟随计数；数字以等宽数字滚动，宽度按最终值预留 | 随计数跳变 | 随计数跳变 |
| 连接完成 | 细线填满后淡出 | 立即 | 立即 |
| 详情展开或收起 | 高度以弹簧变化（0.24s，无回弹），内容淡入淡出 | 立即 | 立即 |
| 箭头 | 以弹簧转动 | 立即 | 立即 |
| 脚本演示（模拟） | 卡片出现在屏幕上后开始，首次按下时停止 | 相同 | 相同 |

减弱动效会保留所有状态和播报。每种状态下行高都是 60px，所以应用连接期间，它下方的内容不会移动。

## 相关组件

- `ToolCall`：智能体此刻正在调用的工具，可就地审批。
- `AgentStatus`：整个智能体运行的实时状态。
- `ConfirmMorph`：就地询问的单个破坏性按钮。
