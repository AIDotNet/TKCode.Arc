---
title: Env variables
summary: 项目的环境变量：以每个环境一列开关的矩阵呈现，支持遮罩值、原地编辑、导入 .env、指纹对比以及重新部署横幅。
source-hash: ec25f6b52de3
---

<Demo name="Preview" />

## 何时使用

`EnvVariables` 是团队保存项目配置和秘密值的设置页面。每一行是一个值以及它所适用的环境，因此同一个键可以在开发环境和预发布环境中有一个值，在生产环境中有另一个值。环境是一列列开关：拨动开关会把该行移入或移出该环境；如果某个开关会让同一个键在同一环境里有两个值，则会被拒绝并给出一条简短提示。值在有人显示之前一直隐藏，被标记为敏感的值可以被替换，但永远不会再次显示。

每一次更改都会标记所在的行，并打开一个横幅，统计更改数量，并指明哪些环境仍在运行旧值。点击 Redeploy 会调用你的部署；横幅会确认部署完成，或变红、保留这些更改并提供重试。

- **项目或服务设置。** 保存应用、后台任务或站点的变量，每个环境一列。
- **项目接入。** 粘贴现有的 `.env` 文件，在任何内容改变之前，逐行查看将添加、替换或跳过什么。
- **发布检查。** 通过指纹对比预发布和生产环境，找出一方缺少的键或持有不同值的键，并把缺少的值复制过去。

## 何时不用

- **编辑单个配置文件。** 当用户处理的是文件本身，且注释和顺序很重要时，请使用 `CodeBlock` 或文本编辑器。
- **成千上万个键。** 行不做虚拟化，而且每一行都有动画；本区块适合几十个键，而不是整个秘密存储库。
- **绝不能发送到客户端的秘密值。** 区块只显示你提供给它的内容。对敏感行请将 `Value` 留空，改为提供 `Fingerprint`，并且只在 `ValueRevealed` 中加载可读的值。
- **一组互不相关的设置。** 对于与环境无关的开关和选项，请使用由 `Switch` 和 `Select` 组成的设置表单。

## 用法

### 环境与行

`Environments` 按顺序列出各列；每个 `EnvEnvironment` 有供行引用的 `Key` 和供用户阅读的 `Name`。如果没有提供，区块会使用 Development、Preview 和 Production。每个 `EnvVariable` 有 `Key`、`Value` 以及它所适用的 `Environments` 的键；添加 `UpdatedAt` 和 `UpdatedBy` 可以填充该行的第二行文字。行可以按任意顺序提供：区块会按键排序（按序数比较：大写字母在小写字母之前，下划线在字母之后），键相同时按其第一个环境排序。

```xml
<arc:EnvVariables ProjectName="lumen-console" SampleDotenv="FEATURE_FLAGS_URL=https://flags.lumen.example">
    <arc:EnvVariables.Environments>
        <arc:EnvEnvironment Key="development" Name="Development" />
        <arc:EnvEnvironment Key="staging" Name="Staging" />
        <arc:EnvEnvironment Key="production" Name="Production" />
    </arc:EnvVariables.Environments>
</arc:EnvVariables>
```

```csharp
block.Variables.Add(new EnvVariable("CACHE_TTL_SECONDS", "30", "development", "staging"));
block.Variables.Add(new EnvVariable("CACHE_TTL_SECONDS", "300", "production"));
block.Variables.Add(new EnvVariable("PRIMARY_DB_DSN", null, "production") { IsSensitive = true, Fingerprint = "5e0c2a" });
```

对于你自己拥有的集合，请使用 `VariablesSource`；`ObservableCollection` 会被跟踪，行的属性和环境列表的变化也会被跟踪。当用户添加、导入或删除行时，区块会就地编辑该集合。

### 值、敏感值与指纹

在有人点击眼睛图标之前，值不会出现在屏幕和无障碍树中；它在那一刻才被读取，隐藏时再次丢弃。`ValueRevealed` 会在显示之前触发（在这里填充 `Value` 即可按需加载），并在隐藏之后再触发一次。敏感行显示的是锁和 "Sensitive"，而不是眼睛和复制按钮，它的编辑器一开始是空的：新值会替换旧值，只有删除该行再重新添加，它才会变为可读。

对比视图显示的是六位指纹，而不是每个值：默认取值的 SHA-256 的前六位十六进制数字，在视觉树之外计算。相同的值得到相同的指纹。对于你没有加载的值，请自行设置 `Fingerprint`。

### 添加与导入

Add variable 会打开单个变量的表单：键、值、环境（默认选中除最后一个之外的所有环境；设置 `DefaultAddEnvironments` 可选择其他环境）以及敏感选项。错误在第一次尝试后出现，并随用户输入而更新：键为空、键以数字开头或包含字母、数字和下划线以外的字符、所选环境中已设置该键、值为空，或没有选择环境。

Import .env 会打开带有 `SampleDotenv` 的粘贴表单。在 Key 字段中粘贴多行文字也会切换到该表单。文本框下方的预览会把每一行键标记为新增、替换现有值或跳过（在后面的行中再次设置、键不合法、缺少等号）；注释、空行、`export` 和引号都会被处理。导入时会在被替换的行上关闭所选环境，删除不再有任何环境的行，并为每一行添加一行。

<Demo name="ImportDotenv" />

### 对比环境

对比视图把两个环境并排放置（`CompareLeft` 和 `CompareRight`，双向；此处为预发布和生产）。一方缺少的键会有一个 Copy to 按钮，它会把该环境添加到持有该值的行上，敏感标记也一并带上。`ShowMatching`（Show identical 开关）会同时列出相同的键。搜索会同时过滤两个视图。

<Demo name="Compare" />

### 重新部署

`RedeployAction` 接收一个 `EnvRedeployRequest`（环境以及更改数量）和一个取消令牌。运行期间按钮会转圈；这期间做出的更改会等待下一次部署。抛出异常即表示失败：横幅会显示消息，保留所有更改，并提供 Retry。没有设置操作时，区块会等待约 1.7 秒，作为演示。在 `RedeployRequested` 中设置 `Cancel` 可以先行阻止，也可以在代码中调用 `RedeployAsync` 和 `CancelRedeploy`。

```csharp
// Your own call to the hosting service; here it only waits.
static Task StartDeployment(IReadOnlyList<string> environments, CancellationToken cancellation) =>
    Task.Delay(1500, cancellation);

block.RedeployAction = (request, cancellation) => StartDeployment(request.Environments, cancellation);
```

<Demo name="FailingRedeploy" />

### 窄容器

区块读取的是它自身的宽度。低于 760 时，每一行堆叠显示：先是键和编辑按钮，然后是值，再是每个环境一个带标签的胶囊，每行最多放得下几个就放几个（每个至少 120）；对比表格变为每个键一张卡片。低于 560 时，左右内边距收紧，页头按钮平分宽度，搜索和视图开关各占一行。

<Demo name="Compact" />

### 事件与绑定状态

`ViewMode`、`SearchText`、`CompareLeft`、`CompareRight` 和 `ShowMatching` 是双向的。`VariablesChanged` 跟随每一次更改，附带更改原因和所有行；具体的事件（`VariableAdded`、`VariableSaved`、`VariableRemoved`、`VariablesImported`、`ScopeToggled`、`ScopeConflict`、`ValueCopied`）则携带发生了什么变化。`PendingChangeCount`、`PendingEnvironments` 和 `RedeployState` 描述横幅。`IsReadOnly` 让区块仅限读取和对比，`IsFramed="False"` 让它平铺在页面上。

<Demo name="EventLog" />

### 文字

每一个词都在 `Labels`（`EnvVariablesLabels`）中，格式是函数，因此语序可以调整。

```csharp
block.Labels = new EnvVariablesLabels
{
    Title = "Variables d’environnement",
    AddButton = "Ajouter",
    ScopeName = (env, key) => $"{key} en {env}",
};
```

## API 参考

<!-- api: EnvVariables -->

<!-- api: EnvVariable -->

<!-- api: EnvEnvironment -->

<!-- api: EnvRedeployRequest -->

<!-- api: EnvVariablesChangedEventArgs -->

## 无障碍

键盘：

- Tab 依次经过 Import .env、Add variable、横幅的按钮、搜索框、视图开关，然后是每一行：眼睛图标（敏感行没有；值显示时，复制按钮在它之前）、每个环境一个开关以及编辑按钮。
- 空格键或 Enter 拨动开关、显示或隐藏值，以及打开或关闭编辑器。
- 视图开关和添加面板的表单开关用方向键移动并选择。
- 在编辑器和添加面板中，Enter 保存，Escape 关闭；焦点回到打开它们的按钮。
- 在搜索框中按 Escape 会清除搜索。

读屏软件：

- 每个开关是一个名为 "<Environment> for <KEY>" 的切换按钮，位于名为 "Environments for <KEY>" 的组中；列标题不会被朗读。眼睛图标是 "Reveal <KEY>"，复制按钮是 "Copy <KEY>"，编辑按钮是 "Edit <KEY>"。
- 隐藏的值读作 "Hidden value"，敏感值读作 "Write-only value"；圆点不会被朗读。对比单元格会读出环境、"Fingerprint" 和数字。
- 开关被拒绝的提示是一个强烈的实时区域；更改以礼貌方式播报："KEY now on in Staging"、"KEY updated"、"KEY created in Development and Staging"、"KEY deleted from Production"、"Imported 3 variables into …"、"KEY now set in Staging"、"Deployed Production"，以及带有消息的部署失败。显示值和搜索结果不会播报。
- 焦点永远不会回落到窗口：保存后回到编辑按钮，添加或导入后回到打开面板的按钮，删除后移到下一行的编辑按钮，Copy 之后移到下一个 Copy to 按钮（或 Show identical），Clear search 之后移到搜索框，横幅关闭后移到搜索框。

## 动效

- **展开与收起**：横幅、添加面板、编辑器、冲突提示和新行沿平滑的临界阻尼弹簧长出（约 700 ms），透明度沿同一条曲线变化；它们的关闭，以及被删除或被过滤掉的行的折叠，使用加速曲线，历时 180 ms。在单个表单和 .env 表单之间切换是即时的。
- **保存、添加和导入**：该行以强调色底色发光 1.6 秒（先保持，再淡去）。
- **开关**：方框在 120 ms 内填充，并在对勾绘制的同时，以一条快速弹簧从 60% 放大。
- **值**：圆点向上淡出，同时值升起；复制按钮弹出。
- **横幅**：数量和文字行以短暂的退出和上升的进入相互替换；成功时图标弹成对勾，底色渐变为绿色，横幅在 2.8 秒后关闭。冲突提示在 3.8 秒后关闭。
- **视图**：旧视图在 120 ms 内淡出，然后新视图上升 6 px 并在 240 ms 内淡入，同时高度以弹簧适应。
- 减少动效和关闭动效会立即应用每一次变化，不带发光。

## 相关组件

- API keys、Webhooks 和 Integrations，另外几个开发者区块。
- `SearchField`、`SegmentedControl` 和 `Switch`，用于工具栏和对比控件。
- `CopyButton`，用于把单个值复制到别处。
- `Alert`，用于不与更改列表绑定的横幅。
