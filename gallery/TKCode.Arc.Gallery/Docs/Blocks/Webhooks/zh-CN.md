---
title: Webhooks
summary: Webhook 控制台：查看每个端点的健康状况和流量，检查发出的内容与收到的响应，在原地重试失败的投递，并添加端点（签名密钥只显示一次）。
source-hash: 103d1356447b
---

<Demo name="Preview" />

## 何时使用

`Webhooks` 是开发者设置区域中的一个页面，用户在这里管理你的产品向其推送事件的 URL。从上到下依次显示：端点（每个端点带有所订阅的事件、成功送达的事件占比和健康标记）、所选端点的详情以及过去一天的流量图表、它最近的投递，以及针对列表中所选投递的检查器：响应、请求体和请求头。失败的投递可以用 Retry 在原地重新发送，"Add endpoint" 会展开为一个表单，并把新端点的签名密钥只显示一次。

- **带有对外 Webhook 的产品。** 支付、物流、票务或排期服务会通知客户的服务器，支持人员和开发者需要看到某个端点为什么停止响应。
- **集成的诊断页面。** 一个端点及其流量，并带有测试事件，用于在上线前验证往返流程。
- **内部事件总线控制台。** 把事件路由到内部服务的团队可以观察出错的服务，并在修复后重新发送。

## 何时不用

- **成千上万条投递。** 列表显示的是最近的投递，不做虚拟化；完整的投递历史请使用带分页和筛选的 `DataGrid`。
- **编辑端点。** 区块负责添加和显示端点；重命名、暂停、删除和轮换密钥应放在你自己的设置页面中（签名密钥只在创建时显示）。
- **单纯的 URL 列表。** 当没有流量可以显示时，表格或卡片列表更简单。

## 用法

### 端点与投递

每个 `WebhookEndpoint` 有 `Name`（为空时显示主机名）、`Url`、所订阅的 `Events`、添加者和添加时间（`AddedBy`、`AddedOn`；此人会获得一个生成的头像）、遮罩的 `SecretHint`、`ApiVersion`、以 `Volume` 桶表示的流量（最旧的在前；48 个半小时构成 24 小时图表）以及最近的 `Deliveries`（最新的在前）。`DeliveredRate`、`FailingCount` 和 `Health` 会随投递和图表的变化而变化：成功率统计图表中未失败的事件，向下取整到千分之一，当端点有一条投递失败且尚未重试时，该端点处于失败状态。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:Webhooks Description="Corvid signs each event and sends it to your endpoints.">
        <arc:WebhookEndpoint Name="Shipping sync"
                             Url="https://relay.tallowfield.dev/hooks/corvid/v2"
                             AddedBy="Lena Okafor"
                             SecretHint="sig_••••c41e"
                             ApiVersion="Version 2026.09" />
    </arc:Webhooks>
</UserControl>
```

`WebhookDelivery` 包含 `EventType` 和 `EventId`、`Attempt`、`StatusCode`（0 表示端点没有响应）和 `StatusText`、`Duration`、`SentAt`、`ResponseBody`、请求的 `Payload`（JSON 会缩进并着色，其他文本原样显示）以及它的 `Headers`。对于你自己拥有的集合，请使用 `EndpointsSource`：`ObservableCollection` 会被监听，新端点会插入到其顶部。

`SelectedEndpoint`、`SelectedDelivery` 和 `InspectorTab` 是双向的。选中端点时，会选中它的第一条失败投递，没有则选中最新一条；切换投递时，检查器的标签页保持不变。

### 重试、新端点与测试事件

三个服务器操作都是异步委托。没有它们时，区块会模拟一个始终成功的服务器，使用固定延迟（1.1 秒、0.76 秒和 0.9 秒），预览就是这样。

- `RetryAction` 重新发送一条失败的投递并返回新的尝试。它会带着短暂的闪烁添加到列表顶部；若成功，原来的那条会标记为已解决，端点的成功率和标记随之更新，选中项移到新的尝试。当委托抛出异常（或返回失败的尝试）时，会改为添加一条带有自己 Retry 按钮的失败尝试，原来的那条显示 "Retried"，并播报失败。
- `CreateEndpointAction` 以 `WebhookEndpointDraft` 的形式接收表单，并返回带有签名密钥的端点（`WebhookEndpointCreated`）。端点会添加到列表顶部，闪烁直到面板关闭，密钥只显示一次并附带 Copy 按钮；面板关闭时它被丢弃，也绝不会存储在端点上。当委托抛出异常时，表单保持打开，消息显示在按钮上方。等待期间按 Escape 会取消请求。
- `SendTestAction` 由没有投递的端点所显示的 "Send test event" 按钮调用。失败的测试会作为失败的投递添加。

`AppendDelivery` 会添加由你的服务器报告的投递，用于实时推送，并把它计入图表的最后一格。

```csharp
var client = new HttpClient();
hooks.RetryAction = async (delivery, cancellation) =>
{
    var started = DateTimeOffset.Now;
    using var response = await client.PostAsync(
        $"https://api.example.dev/deliveries/{delivery.EventId}/resend", null, cancellation);
    return new WebhookDelivery
    {
        EventType = delivery.EventType,
        EventId = delivery.EventId,
        Attempt = delivery.Attempt + 1,
        StatusCode = (int)response.StatusCode,
        Duration = DateTimeOffset.Now - started,
        SentAt = started,
        ResponseBody = await response.Content.ReadAsStringAsync(cancellation),
        Payload = delivery.Payload,
    };
};
```

<Demo name="Failing" />

### 新端点

没有投递的端点，成功率显示为短横线，标记为 "No traffic"，图表为空，并带有测试按钮。`EventTypes` 列出表单中的复选框；`IsDefaultSelected` 会在表单打开时勾选其中一项（否则勾选第一项）。`UrlValidator` 会取代内置的 URL 检查（必填、https、不能是 localhost）。当区块模拟服务器时，`CurrentUser` 指明是谁添加了端点。

<Demo name="NewEndpoint" />

### 窄容器

布局跟随区块自身的宽度。在 880 及以下，检查器移到投递列表下方；在 760 及以下，端点行只保留名称、成功率和标记；在 540 及以下，内边距收紧，工具栏的说明文字隐藏。表单在 800 时事件分为两列，在 560 时全部变为一列。宽度不超过 460 的投递列表会把耗时和时间放在事件下方，窄的请求头则把每个名称放在其值上方。

<Demo name="Compact" />

### 事件与绑定状态

每一次更改都是一个冒泡的路由事件，并带有发生了什么变化：`EndpointSelectionChanged` 和 `DeliverySelectionChanged`（带有旧项和新项，以及是指针、键盘还是区块本身改变了它）、`RetryStarted`、`RetryCompleted`（带有旧成功率和新成功率）、`ActionFailed`、`EndpointCreated`、`TestEventSent`、`ContentCopied`、`AddPanelOpened` 和 `AddPanelClosed`（带有原因）。`RetryCommand`、`SendTestCommand` 和 `EndpointCreatedCommand` 会同时运行。`OpenAddPanel`、`CloseAddPanel`、`RetryAsync` 和 `SendTestAsync` 在代码中完成按钮所做的事；所有文字都在 `Labels` 中。

<Demo name="EventLog" />

## API 参考

<!-- api: Webhooks -->

<!-- api: WebhookEndpoint -->

<!-- api: WebhookDelivery -->

<!-- api: WebhookVolumeBucket -->

<!-- api: WebhookEventType -->

<!-- api: WebhookHeader -->

<!-- api: WebhooksLabels -->

## 无障碍

- **Tab** 依次经过 "Add endpoint" 胶囊、每个端点行、每个投递行及其 Retry 按钮、Copy 按钮、所选标签页和负载面板。
- 在端点或投递行上按 **上** 和 **下** 会移到上一行或下一行并选中它；**Home** 和 **End** 移到第一行和最后一行。它们不会循环，并且会跳过 Retry 按钮。
- 在标签页上按 **左** 和 **右** 在 Payload 和 Headers 之间切换；负载面板可用方向键滚动。
- **Enter** 或 **空格键** 选中一行或按下按钮。在表单中，在字段里按 **Enter** 提交。
- 在面板内按 **Escape** 会关闭它并把焦点还给胶囊；表单等待服务器时，Escape 改为取消请求。
- 端点和投递都是带名称的列表（"Endpoints"、"Recent deliveries to Shipping sync"）。端点行会读出名称、主机名、成功率、标记和事件；投递行会读出状态、事件、耗时和时间，以及重试是否已将其解决。Retry 按钮的名称包含事件及其发送时间，等待时读作 "Resending"。
- 图表是一幅图片，用一句话描述：事件数、仍有多少失败，以及最繁忙的半小时。
- 结果以礼貌方式播报：重试成功或失败、新端点、测试事件、复制、表单错误。
- 焦点永远不会丢失：重试之后从 Retry 按钮移到新的尝试，发送测试事件之后移到测试投递，面板关闭之后移到胶囊，当投递行持有焦点而端点发生变化时，移到新端点的所选投递。创建失败时焦点留在提交按钮上。
- 焦点环只在键盘焦点时显示；在行上，它采用选中底板的形状。

## 动效

- **选择**：选中底板沿临界阻尼弹簧滑向新的行（约 550 ms），并跟随在它下面移动的行。端点的名称、详情和图表标题以短暂的模糊交替替换；柱形依次弹到新的高度（间隔 4.2 ms，从第一根到最后一根约 200 ms）。下半部分交叉淡化，新的一半上升 6 px；旧的一半立即离开布局，因此它下方的内容不会跳动。
- **检查**：标题、响应和面板以短暂的模糊交替替换。旧的响应在淡出期间被抬出布局，因此替换过程中标签页不会移动。
- **重试**：按钮显示加载指示和 "Resending"，同时宽度以弹簧过渡到新标签的宽度。新尝试的行长出（平滑弹簧，80 ms 后开始改变透明度），并带有保持 1.4 秒、再用 1.2 秒淡去的闪烁；"Resolved" 在 Retry 原来的位置从模糊中出现；成功率中变化的数字每次替换一位，从右到左，间隔 30 ms；约 500 ms 之后选中项随之移动。
- **添加端点**：胶囊沿同一条弹簧长成整宽的面板，加号变成关闭叉号，面板展开到一半时表单淡入。密钥从柔和的模糊中显影。新行在顶部长出，并保持闪烁直到面板关闭。
- **悬停图表**：指针下的柱形变亮，其余的柱形在 160 ms 内变暗到 45%；标题立即显示那半个小时。
- 减少动效只保留短促的淡入淡出：底板、柱形、行和面板立即就位，数字原地变化。关闭动效则会立即应用每一次变化。

## 相关组件

- API keys、Integrations 和 Env variables，另外几个开发者区块。
- `Badge`、`Button`、`CopyButton`、`Input`、`Checkbox` 和 `SegmentedControl`，即组成本区块的控件。
- `JsonViewer`，以树形方式浏览较大的 JSON 文档。
