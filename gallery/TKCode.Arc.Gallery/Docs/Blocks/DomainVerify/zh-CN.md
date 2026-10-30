---
title: Domain verify
summary: 自定义域名设置卡片：客户添加域名并复制 DNS 记录，随后实时检查器把每条记录标为有效、未找到或配置错误并给出一行修复建议，最后完成域名验证并签发 SSL 证书。
source-hash: ab584178445a
---

<Demo name="Preview" />

## 何时使用

`DomainVerify` 是产品设置里的"自定义域名"一步。客户输入域名后会得到需要创建的 DNS 记录（每条都带复制按钮），然后看着实时检查器逐条处理：

- **让客户用自己的域名访问应用、文档站、状态页或店铺。**
- **在开启邮件投递之前验证发信域名**（SPF、DKIM、DMARC）。
- **任何需要用户把值复制到第三方、再等待生效的设置步骤。**

## 何时不使用

- **购买或搜索域名。** 请改用带结果列表的搜索框。
- **同时管理很多域名。** 请用每个域名一行的表格，再为其中一个打开此卡片。
- **只需一次性复制单个值。** 在值旁边放一个复制按钮就够了。

## 用法

该卡片派生自 Avalonia 的 `TemplatedControl`，不需要任何数据：没有设置任何动作时，它出现在屏幕上后会播放一段模拟流程（添加域名、检查、发现冲突记录、修复、验证通过），在其中点击或按键就会把控制权交给用户。此时状态行会播报"Simulated, no DNS was queried"。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:DomainVerify Title="Custom domain" DefaultDomain="" />
</UserControl>
```

### 流程

`Phase` 表示卡片所处的阶段，每次变化都会触发 `PhaseChanged`：

1. **Enter**：域名输入框和"Add domain"按钮；后面是变暗的占位行和一句"DNS records appear here after you add a domain"。输入的内容在检查之前会先去掉协议、路径、端口和末尾的点。
2. **Adding**：`GetRecordsAction` 运行期间，按钮显示加载圈。
3. **Records**：域名行取代输入框，列出各条记录，页脚提供"Check records"。
4. **Checking**：每隔 `PollInterval` 轮询解析器。尚未生效的行显示加载圈；页脚闪烁并统计已生效的区域数。
5. **Invalid**：某条记录的值有冲突。轮询停止，该行显示"Misconfigured"，下方展开一条提示，说明找到了什么以及如何修复。"Check again"开始新一轮检查。
6. **Verified**：所有记录均有效。域名行显示"Issuing SSL certificate"，直到 `IssueCertificateAction` 返回，随后显示"SSL by Let's Encrypt · renews in 60 days"。此时触发 `Verified`，"Add another"重新开始。

### 连接你的服务器

三个异步委托代表你的后端。每个委托都会收到取消令牌，移除域名或卡片离开窗口时会取消它。

```csharp
card.GetRecordsAction = async (domain, cancellation) =>
    await api.AddDomainAsync(projectId, domain, cancellation);          // IReadOnlyList<DomainRecord>

card.VerifyAction = async (domain, records, context, cancellation) =>
    await api.CheckDomainAsync(projectId, domain, cancellation);        // DomainVerifyResult

card.IssueCertificateAction = async (domain, cancellation) =>
    await api.IssueCertificateAsync(projectId, domain, cancellation);   // DomainCertificate

card.Verified += (_, e) => Console.WriteLine($"{e.Domain} is live");
```

`VerifyAction` 只做一次检查，不要写成循环。记录仍在生效时返回 `DomainRecordStatus.Missing`，卡片会继续轮询；只有值冲突时才返回 `DomainRecordStatus.Invalid`，并附上 `Found` 和一句话的 `Fix`。卡片在"全部有效"或出现任何无效记录时停止。模拟检查时 `PollInterval` 为 800 毫秒，设置了 `VerifyAction` 时为 5 秒。任一委托抛出的异常会显示在页脚、触发 `Failed`，并让卡片保持可重试状态。

<Demo name="Handlers" />

### 窄容器

卡片会填满容器，最宽 680。宽度低于 544 时列标题消失，每条记录改为堆叠：第一行是类型、名称和状态，下面是值和复制按钮，再下面是用途说明。过长的名称和值以省略号截断，完整内容保留在工具提示里；复制按钮始终复制完整的值。

<Demo name="Narrow" />

### 发信域名

对于邮件，请从 `GetRecordsAction` 返回 MX、SPF（TXT）、DKIM（CNAME 或 TXT）和 DMARC 记录，把 `Title` 设为"Sending domain"，并用 `Labels` 改写文案：这样最后一步显示"Turning on delivery"，而不是签发证书。不知道 DNS 托管方时，设置 `Provider="{x:Null}"` 即可隐藏"DNS hosted at"一行。

<Demo name="SendingDomain" />

### 文案

卡片显示或播报的每个词都是 `DomainVerifyLabels` 的属性；给 `Labels` 赋一个新实例即可翻译或改写。带格式的文本（例如页脚的"Checking records · 2 of 6 regions propagated"）是函数，因此语序可以调整。

## API 参考

<!-- api: DomainVerify -->

<!-- api: DomainRecord -->

<!-- api: DomainRecordCheck -->

<!-- api: DomainVerifyResult -->

<!-- api: DomainCertificate -->

<!-- api: DomainProvider -->

## 无障碍

- 卡片以标题（二级标题）命名。记录是名为"DNS records"的列表；每一行读出类型、名称和状态（"CNAME app, Valid"）。
- 每种状态都是图标加一个简短的词，绝不只靠颜色。冲突记录的提示是该行的一部分。
- 礼貌型实时区域播报每一步：域名已添加、正在检查、某条记录需要修复、域名已验证、证书已签发、失败。未设置 `VerifyAction` 时会附加"Simulated, no DNS was queried"。
- 域名输入框的标签是"Domain"；域名无效时下方显示错误。复制按钮的名称是"Copy TXT value"、"Copy CNAME value"等。变暗的占位行不可交互，并对读屏隐藏。

### 键盘

- 在域名输入框中按 **Enter** 添加域名，与"Add domain"相同。
- **Tab** 依次经过输入框和按钮、关闭按钮、每条记录的复制按钮和页脚操作。
- 在获得焦点的复制、关闭或页脚按钮上按 **Enter** 或 **Space** 即可触发。
- 添加域名后焦点移到页脚操作；移除后焦点回到域名输入框。

## 动效

- **状态**：状态文字和图标交叉淡化（100 毫秒淡出、160 毫秒淡入，上升 2 像素）；检查中的行显示加载圈，页脚显示闪光。
- **行**：记录在 160 毫秒内淡入，每一行比前一行晚 40 毫秒。修复提示按平滑弹簧展开。
- 减少动效时保留所有状态，去掉错开效果，只做短暂淡化。关闭动效时所有变化立即生效。模拟流程只在卡片位于屏幕内时播放。

## 相关

- `CopyButton`、`Button`、`Input` 和 `TextShimmer`，组成该卡片的控件。
- API keys、Webhooks、Integrations、Env variables 和 Webhook tester，Developer 分组里的其他区块。
