---
title: API keys
summary: API 密钥管理：点击 Create key 胶囊即可展开为表单，新密钥只显示一次，每一行都能展开查看用量详情，长按即可吊销密钥。
source-hash: 3f74d0d55c2f
---

<Demo name="Preview" />

## 何时使用

`ApiKeys` 是 API 产品开发者设置中的密钥页面。页头说明这些密钥的用途，工具栏统计有效密钥的数量，每个密钥是一行，显示名称、测试或生产标记、末尾四位字符、权限范围以及上次使用时间。创建和吊销都在原地完成，用户无需离开列表：

- **创建密钥。** "Create key" 胶囊会展开为表单：名称、Test 或 Live，以及权限范围。由你的服务器创建密钥，随后同一个面板会把完整的密钥显示一次，并附带复制按钮和警告，同时新行从列表顶部滑入，并在面板关闭前保持高亮。
- **查看密钥。** 点击一行即可展开详情：创建者、最近一次请求（时间和来源）以及过去 30 天内由它签名的请求数量。长时间未使用的密钥会带有警告。
- **吊销密钥。** 详情末尾是长按吊销按钮。按住 1.2 秒会给该行加上删除线；你的服务器随即吊销该密钥，这一行折叠消失。

## 何时不用

- **需要反复查看的密钥。** 本区块只会把密钥显示一次。对于需要一直可读的值（配置、环境变量），请改用带显示按钮的遮罩输入框。
- **带有过期规则或大量权限的个人访问令牌。** 表单只提供名称、两种环境和一个扁平的权限范围列表。若令牌需要过期日期、资源选择器或按项目划分的权限，请使用完整的表单页面。
- **数百个密钥。** 行不做虚拟化；本区块面向几个到几十个密钥。搜索和分页应交给数据表格。
- **只读列表。** 如果这里没有人能创建或吊销密钥，普通列表或数据表格会更简单。

## 用法

本区块派生自 Avalonia 的 `TemplatedControl`。每个密钥是一个 `ApiKeyItem`（`AvaloniaObject`）：包含名称、环境、用于识别它的前缀和末尾四位字符（绝不包含密钥本身）、权限范围、创建日期和创建者、上次使用时间以及请求数量。表单提供的权限范围是 `ApiKeyScope`，包含名称、描述，以及新密钥是否默认勾选。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:ApiKeys x:Name="Keys"
                 Description="Keys let your backend call the Northwind Relay API. Store them in a vault, never in client code." />
</UserControl>
```

```csharp
Keys.Scopes.Add(new ApiKeyScope { Name = "orders:write", Description = "Create, update and cancel orders" });
Keys.Scopes.Add(new ApiKeyScope { Name = "accounts:read", Description = "Read account profiles and contacts", IsDefault = true });

var checkout = new ApiKeyItem
{
    Key = "checkout",
    Name = "Checkout service",
    Environment = ApiKeyEnvironment.Live,
    Prefix = "rk_live_",
    LastFour = "7c3e",
    LastUsedAt = DateTimeOffset.Now.AddMinutes(-4),
    CreatedAt = new DateTimeOffset(2026, 2, 11, 0, 0, 0, TimeSpan.Zero),
    CreatedBy = "Lena Ortiz",
    LastRequestText = "4 min ago from 18.144.62.10, Frankfurt",
    RequestsLast30Days = 942_317,
};
checkout.Scopes.AddRange(["orders:write", "accounts:read"]);
Keys.Keys.Add(checkout);
```

### 结构

- **页头**：`Title`（二级标题，默认为 "API keys"）和 `Description`，最大宽度 520。
- **工具栏**：数量（"5 active keys"，数字变化时会向上或向下滚动）以及末尾的 "Create key" 胶囊。整个胶囊都可以点击，包括其中的文字。面板打开时数量会淡出。
- **行**：整行是一个按钮，分四列（名称、标记和遮罩；权限范围标签；上次使用和创建日期；箭头）。详情在其下方展开：先是事实信息，然后是一行说明和长按吊销按钮。
- **空状态**：标题、一行文字和一个可选按钮（`EmptyActionText`），在最后一行折叠消失后显示。

### 在你的服务器上创建密钥

将 `GenerateAction` 设置为创建密钥的调用。它会接收一个 `ApiKeyRequest`（去除首尾空白的名称、环境、已勾选的权限范围以及尝试次数）和一个取消令牌，并返回 `ApiKeyCreated`：新的行以及一次性的密钥。运行期间 `PanelState` 为 `Generating`：Generate 按钮显示加载指示并保持焦点，输入项被锁定，按 Escape 会取消请求并回到表单。区块只在展示密钥期间保留它，面板关闭时就会丢弃。

```csharp
Keys.GenerateAction = async (request, cancellation) =>
{
    var created = await api.CreateAsync(request.Name, request.Environment, request.Scopes, cancellation);
    var item = new ApiKeyItem
    {
        Key = created.Id,
        Name = request.Name,
        Environment = request.Environment,
        Prefix = created.Prefix,
        LastFour = created.Secret[^4..],
        CreatedAt = DateTimeOffset.Now,
        CreatedBy = currentUser.Name,
    };
    item.Scopes.AddRange(request.Scopes);
    return new ApiKeyCreated(item, created.Secret);
};
```

新行会插入到 `Keys` 的顶部（如果 `KeysSource` 是可以添加项的列表，则插入其顶部），随后触发 `KeyCreated` 并运行 `KeyCreatedCommand`。如果没有设置 `GenerateAction`，区块会模拟服务器：约 730 ms 后生成一个以 `SimulatedPrefixFormat` 开头的随机 40 位密钥，并记为由 `CurrentUserName` 创建。仅在原型和演示中使用。

表单在每次打开时都会重置：名称为空、环境为 `DefaultEnvironment`，权限范围为标记了 `IsDefault` 的项。名称为空时，会在输入框下方显示错误并把焦点留在那里；没有选择任何权限范围时，会在权限范围下方显示错误并聚焦第一项。用户一输入或勾选权限范围，两种错误随即清除。在名称输入框中按 Enter 即可提交。

### 失败与重试

当 `GenerateAction` 抛出异常时，表单保持打开并保留用户已输入的所有内容，按钮上方出现一行错误（异常消息，或 `ApiKeyLabels.GenerateFailed`），`GenerateError` 保存该错误，并触发 `KeyCreateFailed`。再次按下 Generate 会以下一个尝试次数重试。当 `RevokeAction` 抛出异常时，带删除线的行会恢复原样，长按按钮重置，错误会被播报，并触发 `KeyRevokeFailed`。

<Demo name="Failing" />

### 吊销

按住按钮 `RevokeHoldDuration`（1.2 秒，可用指针，也可按住空格键或 Enter）即确认吊销。`KeyRevoking` 会先触发，并且可以取消。随后名称和遮罩被划上删除线，该行其余部分变暗，并运行 `RevokeAction`；该行至少保持这个状态 `RevokeExitDelay`（0.9 秒），两者都完成后折叠消失。密钥会从 `Keys`（或 `KeysSource` 列表）中移除，`KeyRevoked` 连同该行原先的索引一起触发，并运行 `KeyRevokedCommand`。`RevokeAsync` 可在代码中完成同样的操作，无需长按。

### 详情与闲置密钥

`ExpandedKey`（双向）是详情已展开的那一行；同一时间只能展开一行。`LastRequestText` 填充 "Last request" 这一项；没有它时，区块会写出密钥是多久之前使用的。`LastUsedAt` 早于 `StaleAfter`（30 天）的密钥，会在上次使用时间前显示警告，其吊销说明会写明它已闲置多少天。`ReferenceTime` 为这些文字固定"现在"的时间（用于测试和截图）。

### 窄容器

本区块跟随它自身的宽度，而不是窗口的宽度。宽度低于 700 时，权限范围列和创建日期离开行（上次使用移到末尾），改在详情中显示。低于 540 时，左右内边距从 20 收紧为 16，事实信息堆叠为一列，吊销按钮移到说明下方。当展开的面板宽度不超过 560 时，表单使用单列，提示文字独占按钮上方的一行。

<Demo name="Compact" />

### 空状态

没有密钥时，列表显示 `ApiKeyLabels.EmptyTitle` 和 `EmptyDescription`。设置 `EmptyActionText` 可添加一个按钮：它会触发 `EmptyActionInvoked` 并运行 `EmptyActionCommand`；如果两者都没有处理，则打开表单。新添加的密钥会一起淡入展开，数量向上滚动。

<Demo name="Empty" />

### 事件与状态

`PanelState`（`Closed`、`Form`、`Generating`、`Reveal`）和 `PanelStateChanged`（附带面板关闭的原因：Cancel、Done、关闭开关、Escape 或代码）让页面可以跟踪面板；`IsCreateOpen`（双向）、`OpenCreate` 和 `CloseCreate` 用来驱动它。`ActiveCount` 是列表中的密钥数量。密钥被复制时触发 `SecretCopied`。

<Demo name="EventLog" />

### 文字

区块显示或播报的每一个词都在 `Labels` 中，这是一个带英文默认值的 `ApiKeyLabels`。包含数值的文字是函数，因此语序可以调整：

```csharp
Keys.Labels = new ApiKeyLabels
{
    CreateKey = "New key",
    CountWords = count => count == 1 ? "key in use" : "keys in use",
    Used = ago => $"Last call {ago}",
};
```

## API 参考

<!-- api: ApiKeys -->

<!-- api: ApiKeyItem -->

<!-- api: ApiKeyScope -->

<!-- api: ApiKeyRequest -->

<!-- api: ApiKeyCreated -->

<!-- api: ApiKeyLabels -->

## 无障碍

- 区块以其标题命名，标题为二级标题。数量作为一个短语朗读（"5 active keys"）。
- 开关名称为 "Create key"，面板打开时为 "Close"。权限范围是一个名为 "Scopes" 的组；每个复选框以其权限范围命名，并由其说明文字描述。校验错误是警示信息。
- 密钥框的名称为 "Secret key"。密钥绝不会出现在视觉树的其他任何位置。
- 列表名称为 "Active keys"。每一行的按钮会读出名称、环境、"Key ending in 7c3e"、权限范围和日期，闲置密钥在上次使用前会读出 "Idle"，并说明详情是否已展开。详情的名称为 "<name> details"。长按按钮的名称为 "Hold to revoke: <name>"。
- 展开一行、复制密钥、创建密钥、吊销密钥以及吊销失败，都以礼貌方式播报。生成失败时，错误行以强烈方式播报。
- 行的键盘焦点环绘制在行内部，因此裁剪不会将其遮住。

### 键盘

- **Tab** 依次到达 "Create key" 开关和每一行。展开行的长按按钮紧跟在该行之后。
- 在开关上按 **Enter** 或 **空格键** 会打开表单并聚焦名称。随后 **Tab** 依次经过环境开关（一个停靠点，用方向键选择）、权限范围、Cancel 和 Generate。表单不会困住焦点。
- 在名称输入框中按 **Enter** 提交。在面板内任何位置按 **Escape** 会关闭面板并把焦点还给开关；正在生成密钥时，Escape 改为取消请求并留在表单中。
- 创建密钥后，焦点移到 Copy 按钮。
- 在行上按 **Enter** 或 **空格键** 展开或收起其详情。
- 在长按按钮上按住 **空格键** 或 **Enter** 1.2 秒后吊销；提前松开则取消。
- 吊销之后，焦点移到下一行，没有则移到上一行，再没有则移到 "Create key" 开关。

## 动效

- **胶囊变面板**：一条平滑弹簧让表面向左扩展到完整宽度并向下扩展到表单；加号旋转 45° 变成关闭叉号；表单在 140 ms 后淡入；数量淡出。关闭时，主体在 100 ms 内淡出，表面折回，数量在 100 ms 后淡入。
- **表单**：切换环境时，表单下方的提示以模糊方式交替替换；错误行以平滑弹簧展开。
- **生成**：标题以模糊方式替换为 "Key created"；表单在 120 ms 内淡出，展示区在淡入的同时上升 8 px（60 ms 后开始，历时 320 ms）；密钥从 8 px 的模糊中在 480 ms 内显影。新行从零高度以平滑弹簧长出，80 ms 后开始 240 ms 的淡入，并保持强调色底色直到面板关闭，然后底色在 300 ms 后用 1.2 秒淡去。数量向上滚动。
- **行**：详情以平滑弹簧展开，并在 50 ms 后用 240 ms 淡入；收起时在 160 ms 内淡出。箭头沿 580 ms 的弹簧曲线旋转 180°；展开时的底色在 160 ms 内变化。同一时间只展开一行。
- **吊销**：长按进度线性填充；确认后，一条线划过名称（480 ms），80 ms 后再划过遮罩；其余部分在 240 ms 内变暗到 40%。停顿之后该行以平滑弹簧折叠消失，下方的行随之上移；数量向下滚动。
- 减少动效会让尺寸和位置立即就位，只保留短促的淡入淡出；长按仍然需要 1.2 秒来填充。关闭动效则会立即应用每一次变化。

## 相关组件

- `HoldToConfirm`、`CopyButton`、`Input`、`SegmentedControl`、`Checkbox`、`Badge` 和 `Avatar`，即组成本区块的控件。
- Webhooks、Integrations 和 Env variables，另外几个开发者区块。
