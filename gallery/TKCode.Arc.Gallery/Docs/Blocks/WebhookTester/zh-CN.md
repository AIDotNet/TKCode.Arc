---
title: Webhook tester
summary: Webhook 测试器：选择事件、编辑 JSON 负载并发送带签名的测试事件，观察它从已签名、已发送到端点作出应答的全过程，查看响应，并在最近投递列表中重试失败的投递。
source-hash: b80e9fc5bad2
---

<Demo name="Preview" />

## 何时使用

`WebhookTester` 是发送 Webhook 的产品（计费、认证、邮件、CI）中的"测试端点"页面。用户选择一个事件，调整它的 JSON 负载并发送带签名的测试事件；区块展示它从已签名、已发送到收到应答的过程，并在列表中保留最近的几次尝试。

- **上线前测试。** 客户确认自己的处理程序会返回 2xx，并且会校验签名。
- **集成引导。** 区块在应用内用端点的密钥签名，因此处理程序收到的内容与生产环境完全一致。
- **重放失败的投递。** 失败的行带有 Retry 按钮，会用新的时间戳和签名重新发送同一份内容。

## 何时不用

- **管理大量端点、过滤器和事件订阅。** 这类场景请使用 Webhooks 区块，并从那里链接到本区块来测试某个端点。
- **通用 HTTP 客户端。** 本区块始终发送带签名的 POST 与 JSON 正文，没有方法或请求头编辑器。
- **成千上万行的事件日志。** 列表只保留最近 20 次尝试。分页日志应交给数据表格。

## 用法

本区块派生自 Avalonia 的 `TemplatedControl`。给它端点、用户可选的事件（每个包含类型和默认 JSON 负载）以及端点的签名密钥。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:WebhookTester x:Name="Tester"
                       Endpoint="https://api.acme.dev/webhooks/billing"
                       Secret="{Binding SigningSecret}" />
</UserControl>
```

```csharp
Tester.Events.Add(new WebhookTestEvent("invoice.paid", """{"id":"evt_1","type":"invoice.paid","amount":4900}"""));
Tester.Events.Add(new WebhookTestEvent("invoice.failed", """{"id":"evt_2","type":"invoice.failed","amount":4900}"""));
```

`Events` 为空时，区块提供五个示例事件。选择事件会载入它的负载：合法的 JSON 会被格式化，其他内容原样保留。`SelectedEvent` 与 `Payload` 是双向的，页面可以读取或预设它们。

### 在你的服务器上发送

区块在应用内对正文签名（对 `timestamp.body` 做 HMAC SHA-256，以 `t=…,v1=…` 放入 `SignatureHeader`），但它自己绝不会向客户的端点发请求：浏览器或桌面应用不应代表客户调用任意网址。`SendAction` 收到已签名的 `WebhookTestRequest` 并交给你的服务器，由服务器真正发出 POST，再返回 `WebhookTestResponse`。服务器可以用同一份正文重新签名，核对两个签名是否一致。

```csharp
Tester.SendAction = async (request, cancellation) =>
{
    var result = await api.PostAsJsonAsync($"endpoints/{endpointId}/test", request, cancellation);
    return await result.Content.ReadFromJsonAsync<WebhookTestResponse>(cancellation)
           ?? new WebhookTestResponse { Error = "Empty answer" };
};
```

`Status` 为 0 并带有 `Error`（端点超时则同时设 `TimedOut`）表示没有收到应答；抛出异常同样算作失败的投递。无论哪种情况，该投递都会以失败状态进入列表，时间线以红色结束，并播报失败。`RetryAction` 用于重新发送失败的投递；未设置时，Retry 使用 `SendAction` 并带上新的签名。

<Demo name="Failing" />

未设置 `SendAction` 时，每次投递都是模拟的：经过 `SimulatedLatency` 后返回 200，播报内容会说明没有真正发送任何请求。启用 `AutoPlay`（默认）时，模拟状态的区块在滚动到一半进入视口约一秒后发送一个事件，除非用户已经操作过它。也可以在代码中调用 `SendAsync` 和 `RetryAsync`。

### 其他产品

`Source` 给出发送事件的产品名称（副标题显示为 "Signed test events from Relay Mail"），并可通过 `MarkFactory` 提供自己的 16 px 标志；没有时，区块会画一个带首字母的圆角方块。把 `SignatureHeader` 设为你的产品所用的请求头。

<Demo name="OtherProduct" />

### 负载编辑器

编辑器在输入时为 JSON 着色，显示行号，并用文字说明负载能否解析以及字节数（"Valid JSON · 327 bytes"；无法解析时给出行号和原因）。回车保持缩进，并在 `{` 或 `[` 之后加深缩进。**Format** 会格式化合法的 JSON。无法解析的负载不能发送：编辑器获得焦点并播报问题。端点必须是 https；`localhost` 和回环地址允许使用 http。

### 窄容器

区块跟随自身宽度。宽度达到 680 时，请求（端点、事件、密钥、负载）与响应（时间线、标签页）并排显示，编辑器拉伸到与响应列等高；更窄时上下堆叠。宽度低于 420 时，列表中的投递时间隐藏，内边距从 20 收紧为 16。

<Demo name="Narrow" />

### 文案

区块显示或播报的每个词都在 `Labels` 中，它是带英文默认值的 `WebhookTesterLabels`。包含数值的文案是函数，因此语序可以改变：

```csharp
Tester.Labels = new WebhookTesterLabels
{
    Send = "Send",
    FailedCount = count => $"{count} did not arrive",
};
```

## API 参考

<!-- api: WebhookTester -->

<!-- api: WebhookTestEvent -->

<!-- api: WebhookTestRequest -->

<!-- api: WebhookTestResponse -->

<!-- api: WebhookTestDelivery -->

<!-- api: WebhookTesterSource -->

<!-- api: WebhookTesterLabels -->

## 无障碍

- 每个字段都有可见标签。负载编辑器以事件命名（"Payload for order.paid"）；JSON 无法解析时，它的帮助文本是问题所在及行号，页脚也会显示。
- 签名密钥在显示之前对辅助技术隐藏；显示按钮是一个开关。
- 响应标签页是带漫游焦点的标签列表；面板可获得焦点且只读。
- 投递行是按钮，以事件、结果和时间命名，位于一个列表中；选中的行处于选中状态。状态片把状态码或文字与颜色搭配。
- 每次发送及其结果都会礼貌地播报；未设置 `SendAction` 时附加 "Simulated, nothing was sent."。

### 键盘

- **Ctrl+Enter**（Mac 上为 **Command+Enter**）在区块内任何位置发送；端点输入框中按 **Enter** 同样发送。
- 负载编辑器中按 **Enter** 换行并保持相同缩进。
- **左**、**右**方向键以及 **Home**、**End** 在 Body、Headers、Request 标签页之间移动。
- **Tab** 依次经过端点、事件列表、显示按钮、复制按钮、负载、Format、Send、当前标签页、响应面板，以及每个失败行的 Retry 按钮。

## 动效

- 时间线的圆圈以短促的淡变切换；两步之间的线从左向右填充，请求在途时，最后一段线上有一个圆点循环移动，直到收到应答。
- 新的投递出现在列表顶部并被选中；响应文本立即替换。
- 减弱动效保留终态，去掉圆点和填充的行进过程；关闭动效则所有变化立即生效。

## 相关

- `Webhooks`（管理多个端点的控制台）和 `ApiKeys`，同属 Developer 区块。
- `CopyButton`、`Button`、`Input` 和 `Select`，组成本区块的控件。
