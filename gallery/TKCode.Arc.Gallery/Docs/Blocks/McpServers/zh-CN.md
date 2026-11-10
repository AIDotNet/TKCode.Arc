---
title: MCP 服务器
summary: 管理助手可以调用的工具服务器：每个服务器都有实时状态，工具由开关控制，连接失败时显示错误和日志，并可通过 URL 或命令添加新的服务器。
source-hash: 2d02a8e47e09
---

<Demo name="Preview" />

## 何时使用

MCP 服务器是一张设置卡片，人们在这里决定助手可以调用哪些工具服务器。工具服务器（使用 Model Context Protocol 的服务器）可以通过 HTTP 地址访问，也可以作为本地程序启动，它会列出 `search_notes`、`post_entry` 这样的工具。卡片把每个服务器显示为一行：标记、名称、HTTP 或 Local 标签、实时状态行（"Connected · 5 tools"、"Connecting…"、"Couldn't connect"、"Off"）以及一个开关。展开一行会显示它的工具，每个工具都有自己的开关；会写入数据的工具带有 "Changes data" 标签，另有 Enable all 和 Disable all。连接失败的服务器会显示错误信息、可以打开的日志和 Retry。这一行还列出地址或命令、请求头或环境变量的键名以及版本，并提供 Reconnect 和 Remove（先确认）。Add server 打开一个表单，可以填写 URL，或填写命令及其参数，还可以加上请求头或环境变量；新服务器加入列表后会立即连接。

- **带有插件工具的助手或智能体**：人们能看到哪些已连接、哪些失败了，以及每个工具能做什么。
- **最小权限**：不必删除，就能关闭一个会写入数据的工具，或关闭整个服务器。
- **修复连接问题**：错误、日志和 Retry 都在同一个地方。

## 何时不用

- **一键连接应用的目录**：请使用 `Integrations`。
- **选择由哪个模型回答**：请使用 `ModelPicker`。
- **编辑密钥**：卡片只在添加服务器时接收请求头和变量，之后不会再显示它们的值；密钥请用 `ApiKeys` 或 `EnvVariables` 管理。
- **运行协议本身**：卡片只显示并修改一组 `McpServer` 对象。连接、列出工具和保存列表都由你的代码完成（`ConnectAsync` 和各个事件）。

## 用法

`McpServers` 派生自 Avalonia 的 `TemplatedControl`。不设置任何属性时，它会显示四个虚构的示例服务器并模拟连接，因此可以直接用于原型。把你自己的列表交给 `Servers`；使用 `ObservableCollection<McpServer>` 时，卡片会跟随你的修改，你也能看到卡片做出的修改。

```xml
<arc:McpServers Servers="{Binding ToolServers}" />
```

```csharp
var servers = new ObservableCollection<McpServer>
{
    new McpServer
    {
        Id = "field-notes", Name = "Field notes", Transport = McpTransport.Http,
        Url = "https://notes.example.dev/mcp", Status = McpServerStatus.Connected,
        Tools = { new McpTool("search_notes", "Finds notes that match a query.") }
    }
};
block.Servers = servers;
```

### 结构

- **页头**：标题、一句说明、汇总（"3 of 4 connected · 8 tools on"）以及 Add server。
- **添加服务器表单**：名称、URL 或 Command、地址或命令及其参数、可选的请求头或环境变量，以及 Cancel 和 Add and connect。
- **服务器行**：标记（名称首字母）、名称和传输标签、状态圆点和文字、地址或命令行、展开箭头以及开关。
- **展开的服务器**：错误框（信息、Retry、Show log）、带开关的工具、详细信息、Reconnect 和 Remove。

### 连接

设置 `ConnectAsync` 以进行真正的连接。它会收到服务器和一个取消令牌；返回 `McpConnectResult.Success(tools, version)` 或 `McpConnectResult.Failure(message, log)`。抛出异常时，以异常信息作为失败原因。当服务器被关闭、被移除、再次连接，或卡片离开窗口时，令牌会被取消。服务器再次列出之前已有的工具时，这些工具保持原来的开关状态。

<Demo name="Connect" />

```csharp
block.ConnectAsync = async (server, cancellation) =>
{
    var session = await client.ConnectAsync(server.Url!, cancellation);
    var tools = await session.ListToolsAsync(cancellation);
    return McpConnectResult.Success(tools.Select(t => new McpTool(t.Name, t.Description)), session.ServerVersion);
};
```

未设置 `ConnectAsync` 时，卡片会等待 `SimulatedConnectDuration`（1.2 秒），然后使用 `McpServersSamples.SimulateConnect`：主机名以 `.invalid` 结尾的地址，或名称以 `missing` 开头的命令会失败并给出日志；其他情况都会成功，并得到三个虚构的工具。

### 添加和移除

Add server 打开表单（`IsAddFormOpen`、`OpenAddForm()`）。表单会检查名称（必填，且不能重复）、地址（以 `http://` 或 `https://` 开头的完整地址）或命令（必填），以及变量（每行一个 `KEY=value`）。焦点会移到第一个有问题的字段，输入时错误会随之更新。表单有效时会添加服务器、引发 `ServerAdded`、关闭表单并连接服务器。`AddServer(draft)` 在代码中做同样的事。

Remove 会先询问："Remove Field notes? Its 5 tools stop being offered."。Remove server 会把它移出列表并引发 `ServerRemoved`；Keep 或 Escape 则返回。把 `CanAddServers` 或 `CanRemoveServers` 设为 false 可以隐藏这些操作；只读列表会同时隐藏两者。

<Demo name="Empty" />

### 关闭服务器和工具

行上的开关可以关闭服务器（状态变为 Off，连接被取消）或打开它（开始连接）。每个工具的开关决定是否提供这个工具；Enable all 和 Disable all 一次改变全部工具。`ServerToggled` 和 `ToolToggled` 报告这些变化，`ConnectedCount` 和 `EnabledToolCount` 给出汇总中显示的总数。

### 在窄栏中

设置 `ExpandedServer` 可以在代码中展开一个服务器。在侧边面板中，卡片会按自身宽度收紧内边距。

<Demo name="Narrow" />

### 文案

所有文字都来自 `Labels`（`McpServersLabels`）：替换这个实例即可翻译或改写。带有数值的句子是函数，因此可以调整语序。控件上的 `Title` 和 `Subtitle` 优先于文案对象。

```csharp
block.Labels = new McpServersLabels
{
    Title = "Serveurs d'outils",
    AddServer = "Ajouter un serveur",
    RemovedAnnouncement = name => $"{name} retiré"
};
```

### 内容准则

- **按服务器保存的内容来命名**（"Field notes"、"Ledgerline"），不要用协议名称来命名。
- **用一句以动词开头的话描述工具**："Finds notes whose title or text matches a query."
- **给每个会写入、发送或删除内容的工具设置** `ChangesData`；人们会最先关闭这些工具。
- **错误要说明发生了什么以及可以怎么做**："The server answered 401: the access key was refused."

## API 参考

<!-- api: McpServers -->

<!-- api: McpServer -->

<!-- api: McpTool -->

### 事件

| 事件 | 何时引发 | 参数 |
|---|---|---|
| `ServerAdded` | 有服务器加入列表 | `Server` |
| `ServerRemoved` | 有服务器离开列表 | `Server` |
| `ServerToggled` | 服务器的开关改变 | `Server`、`IsEnabled` |
| `ToolToggled` | 工具被打开或关闭 | `Server`、`Tools`、`IsEnabled` |
| `StatusChanged` | 服务器的状态改变 | `Server`、`OldStatus`、`NewStatus` |

## 无障碍

- 每个服务器的标题是一个按钮，以服务器名称命名，以状态和地址作为说明，并报告展开或收起。它旁边的开关名为 "Use Field notes"。
- 工具开关以工具名称命名（会写入数据时加上 "Changes data"），并以工具的说明句作为描述。
- 状态从不只靠颜色表达：文字会说明状态，打开时圆点是实心的，关闭时是空心的。
- 一个礼貌的实时区域会播报：正在连接、已连接及工具数量、失败及其信息、添加、移除、关闭服务器以及每一次工具变化。
- 表单字段都有可见的标签；错误显示在字段下方，焦点会移到第一个错误。

### 键盘

| 按键 | 操作 |
|---|---|
| `Tab` | Add server，然后是每个服务器的标题和开关，再然后是展开的服务器中的控件 |
| 标题上的 `Enter` 或 `Space` | 展开或收起服务器 |
| 标题上的 `ArrowDown` / `ArrowUp` | 下一个或上一个服务器 |
| 标题上的 `Home` / `End` | 第一个或最后一个服务器 |
| 展开的服务器中的 `Escape` | 收起它（或取消移除的询问），并回到它的标题 |
| 单行表单字段中的 `Enter` | 添加服务器 |
| 表单中的 `Escape` | 取消表单并回到 Add server |

## 动效

- 展开的服务器内容按平滑弹簧变高；展开箭头按弹簧转动。
- 状态文字通过短暂的标签切换改变（旧文字 100 毫秒淡出，新文字 160 毫秒淡入并上升 2 像素）。
- 已连接圆点的光晕每 2.4 秒向外呼吸一次；正在连接的圆点每 0.9 秒呼吸一次。
- 表单的高度按弹簧展开和收起。
- 行的底色在 160 毫秒内变化。

| 变化 | 完整 | 减弱 | 关闭 |
|---|---|---|---|
| 展开服务器 | 高度弹簧，箭头转动 | 高度和箭头直接跳到位 | 立即 |
| 状态文字 | 标签切换 | 120 毫秒交叉淡化 | 立即 |
| 实时圆点 | 呼吸 | 静止 | 静止 |
| 表单 | 高度弹簧 | 直接跳到位 | 立即 |

## 响应式

- 卡片按自身宽度变化。宽度不超过 520 像素时（`:compact`），页头、列表、表单和展开内容的内边距都会收紧；卡片最宽 720 像素，在更宽的容器中居中。
- 名称和地址行以省略号结尾；工具说明、错误和日志会换行。日志超过 160 像素时滚动。
- 在表单中，命令和参数按 2:3 的比例共用一行。
- 让卡片自然拉伸，不要给它固定宽度。

## 相关组件

- `Integrations`：连接应用的目录。
- `AgentMemory`：助手记住的内容。
- `ModelPicker`：调用这些工具的模型。

| 组件 | 何时选择 |
|---|---|
| `Integrations` | 人们从目录中连接应用 |
| `ApiKeys` | 人们创建和撤销密钥 |
| `Switch` | 一个设置的打开或关闭 |
| `Accordion` | 只需展开和收起各部分，不带状态和操作 |
