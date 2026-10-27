---
title: Integrations
summary: 可连接应用的目录：带搜索、显示数量的分类筛选、会转圈并连接或提供重试的连接胶囊，以及用于权限、同步频率和长按断开的抽屉。
source-hash: b24aed8fc1e9
---

<Demo name="Preview" />

## 何时使用

`Integrations` 是工作区关联其所依赖服务的设置页面。每个应用占一行，显示它的标志、名称、分类和一行描述，右侧的胶囊说明它当前所处的状态。点击 **Connect** 会运行你的授权流程，同时胶囊转圈；成功后胶囊变为绿色的 **Connected**，旁边滑入一个 **Settings** 按钮。点击名称或设置按钮会打开一个抽屉，里面是应用被允许做的事、同步的频率，以及一个按住即可断开的按钮。

- **应用市场或附加组件页面。** 输入时搜索会即时缩小列表，筛选项显示每个分类有多少应用，并且应用连接后顺序不会改变，所以用户总能在原来的位置找到它。
- **工作区或项目设置。** Connected 筛选显示目前已关联的内容，抽屉则回答"这个应用能看到什么"，无论关联之前还是之后。
- **新手引导。** 一份简短的推荐服务列表，每个服务只需点击一次即可关联。

## 何时不用

- **数百个应用。** 行是在列表变化时会滑动的动态元素；本区块面向几十个应用。大型市场请使用分页或虚拟化的目录。
- **只有一个服务。** 一个带有自己状态行的连接按钮，比只有一项的目录更清晰。
- **需要整页的设置。** 抽屉里只有权限、一个频率菜单和断开按钮。当应用需要字段映射、字段选择或多个步骤时，请链接到设置页面。

## 用法

### 应用、分类与频率

每个 `IntegrationItem` 是一行。`Name` 是它在各处的标题，`Category` 是某个 `IntegrationCategory` 的键（用于它的筛选，也是名称后面的那个词），`Description` 是其下方的一行文字，同时也是抽屉的描述。`Scopes` 列出应用被允许做的事，每项的 `Kind` 决定它的图标（read、write、sync、admin）。`SyncOptions` 填充抽屉中的菜单；留空则不显示该菜单。

```xml
<arc:Integrations Title="Connected apps" Subtitle="Link the services your workspace relies on.">
    <arc:Integrations.Categories>
        <arc:IntegrationCategory Key="files" Label="Files" />
        <arc:IntegrationCategory Key="insights" Label="Insights" />
    </arc:Integrations.Categories>
    <arc:Integrations.SyncOptions>
        <arc:IntegrationSyncOption Key="realtime" Label="Real-time" Hint="Updates land within seconds." Phrase="in real time" />
        <arc:IntegrationSyncOption Key="nightly" Label="Nightly" Hint="Runs every night at 03:00." Phrase="nightly" />
    </arc:Integrations.SyncOptions>
    <arc:IntegrationItem Key="vaultline" Name="Vaultline" Category="files"
                         Description="Mirror design files into the matching workspace board.">
        <arc:IntegrationScope Kind="Read" Text="Browse folders you share with the workspace" />
        <arc:IntegrationScope Kind="Write" Text="Add file links to boards and tasks" />
    </arc:IntegrationItem>
</arc:Integrations>
```

当应用来自你自己的列表时，请绑定 `ItemsSource`，而不是填充 `Items`；区块会跟随它的变化。`SelectedFilter`（`all`、`connected` 或某个分类的键）和 `SearchText` 都是双向的，因此页面可以直接打开到某个筛选，或把搜索词保存在地址中。

### 标志

`Logo` 接受图片、`Geometry`（用 `MarkBrush` 填充）或任意控件，以 20 乘 20 的尺寸绘制，不带底板。没有 Logo 时，区块会绘制一个生成的标志：名称的首字母（或 `Monogram`），放在一块圆角方块上，底色取自图表系列色之一，由键决定，因此每次运行都保持一致。区块不附带任何第三方 Logo。

### 连接

`ConnectAction` 是你的授权流程。它会接收应用和尝试次数，以及一个令牌，当调用 `CancelConnect` 或区块离开窗口时，该令牌会被取消。返回 `IntegrationConnectResult.Connected` 并带上连接者（null 表示正在使用区块的人："Linked by you just now"），或返回带有原因的 `IntegrationConnectResult.Failed`；抛出异常同样视为失败，`ErrorFormatter` 会把异常转换为抽屉中的那行文字。失败的应用会在红色底色上显示 **Retry**；点击它会以下一个尝试次数再次运行该操作。

<Demo name="Failing" />

```csharp
// authorize and revoke are your own calls to the service (here: delegates you provide).
Func<string, CancellationToken, Task<bool>> authorize = (key, token) => Task.FromResult(true);
Func<string, CancellationToken, Task> revoke = (key, token) => Task.CompletedTask;

directory.ConnectAction = async (request, cancellation) =>
{
    var approved = await authorize(request.Item.Key, cancellation);
    return approved
        ? IntegrationConnectResult.Connected()
        : IntegrationConnectResult.Failed($"{request.Item.Name} did not grant access. Try again.");
};
directory.DisconnectAction = (item, cancellation) => revoke(item.Key, cancellation);
```

没有 `ConnectAction` 时，区块会等待 `SimulatedConnectDuration`（1.4 秒）然后成功，本页的演示就是这样。`ConnectRequested` 可以在连接开始前取消它，`StatusChanged` 报告空闲、连接中、已连接和失败之间的每一次转换，`ConnectCommand` / `DisconnectCommand` 以该应用为参数运行。

### 窄容器

布局跟随区块自身的宽度，而不是窗口的宽度。低于 720 时，搜索框落到标题下方；到 520 时，左右内边距收紧；在 480 及以下，筛选项可横向滚动，行把胶囊放在两行描述旁边，隐藏分类和设置按钮，点击名称即可打开抽屉。

<Demo name="Compact" />

### 仅分类

设置 `ShowConnectedFilter="False"` 即可只提供 All 和各个分类；当你的应用没有同步计划时，请将 `SyncOptions` 留空：抽屉随后只显示权限和断开按钮。

<Demo name="CategoriesOnly" />

### 事件与绑定状态

`FilterChanged` 和 `SearchChanged` 报告新的可见数量，`SyncFrequencyChanged` 报告旧选项和新选项的键，`ConfigurationOpened` / `ConfigurationClosed` 报告抽屉所对应的应用。`ConnectedCount`、`VisibleCount` 和 `OpenItem` 可以绑定。`IsFramed="False"` 让区块平铺在页面上。每一个词都在 `Labels`（`IntegrationLabels`）中，包括读屏软件的播报文字。

<Demo name="EventLog" />

## API 参考

<!-- api: Integrations -->

<!-- api: IntegrationItem -->

<!-- api: IntegrationCategory -->

<!-- api: IntegrationScope -->

<!-- api: IntegrationSyncOption -->

<!-- api: IntegrationStatusChangedEventArgs -->

<!-- api: IntegrationConnectRequestedEventArgs -->

<!-- api: IntegrationFilterChangedEventArgs -->

<!-- api: IntegrationSearchChangedEventArgs -->

<!-- api: IntegrationSyncFrequencyChangedEventArgs -->

## 无障碍

- 筛选项是名称为 "{filter}, {count}" 的单选按钮；列表、搜索框以及每个胶囊、名称和按钮都有无障碍名称（"Connect Pulsegrid"、"Connecting Pulsegrid"、"Settings for Chirp"），名称的帮助文字就是描述。
- 已连接的胶囊是一个状态标签：不可获得焦点，名称为 "{app} connected"。
- 一个礼貌的实时区域会播报正在连接、已连接、失败和已断开的应用、频率变化，以及筛选或搜索变化之后的应用数量。
- 状态从不只依赖颜色：每个胶囊都有自己的图标和文字。

### 键盘

| 按键 | 操作 |
| --- | --- |
| Tab | 依次经过搜索框、已选中的筛选项，然后是每一行：先是它的名称，再是它的胶囊（空闲、连接中或失败）或设置按钮（已连接）。每个操作一个停靠点。 |
| Left / Up、Right / Down | 在筛选项中：选中上一个或下一个筛选项，到两端时循环。 |
| Home / End | 在筛选项中：选中第一个或最后一个筛选项。 |
| Enter / Space | 按下获得焦点的胶囊、名称或按钮。 |
| Escape | 关闭抽屉。 |

连接之后，焦点从胶囊移到新的设置按钮（设置按钮被隐藏时则移到名称）；在抽屉中连接之后，焦点移到同步频率菜单；断开之后，焦点落在抽屉的 Connect 胶囊上。当抽屉关闭而打开它的那一行已不在列表中时，焦点移到标题。持有焦点的行离开时，会把焦点交给下一行。

## 动效

- 搜索和筛选会让列表重新排布：离开的行在原位淡出并模糊到 4px（160ms），其余的行沿同一条弹簧滑到新位置，新行从模糊中淡入（240ms），列表高度随之变化。
- 筛选高亮沿同一条弹簧在胶囊之间滑动；数量变化时向上或向下滚动。
- 胶囊的标签以模糊方式交替替换，宽度以弹簧跟随；对勾自行绘制；设置按钮在连接后 140ms 滑入，断开后淡出。抽屉中的设置区域以高度弹簧展开和收起。
- 减少动效会保留 120ms 的淡入淡出，去掉位移、模糊和弹簧（离开的行会立即移除）；关闭动效则直接跳变。

## 相关组件

- `Drawer`，设置所在的面板。
- `HoldToConfirm`，用于断开按钮。
- `SearchField` 和 `Select`，用于搜索和频率菜单。
- `EmptyState`，用于因其他原因而为空的列表。
