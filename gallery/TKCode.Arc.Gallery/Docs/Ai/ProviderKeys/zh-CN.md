---
title: Provider keys
summary: 面向自带密钥（BYOK）AI 产品的设置卡片：显示每个提供商的掩码密钥、状态和最近使用时间，支持显示、复制、轮换和可撤销的删除，并通过内联表单在保存前测试新密钥。
source-hash: 1ebaeb7e3937
---

<Demo name="Preview" />

## 何时使用

`ProviderKeys` 是让用户自带 AI 提供商密钥的产品里的设置卡片。每个提供商占一行，显示其标志、掩码密钥（`sk-••••4f2a`）、由图标加文字组成的状态，以及密钥最近一次使用的时间。密钥可以显示五秒、复制、轮换，或在短暂的撤销窗口内删除。添加或轮换密钥时，会在该行下方打开一个内联表单，在输入时检查格式，并在保存前测试连接。

- **工作区或账户设置**，用户在这里为你支持的提供商添加自己的密钥。
- **引导步骤**，请求第一个密钥并显示它何时连接成功。
- **网关管理页面**，显示哪些提供商已连接、哪些密钥被拒绝，以及每个密钥的使用时间。
- **轮换已泄露的密钥**，或删除密钥并留出片刻撤销。

## 何时不用

- **为某次请求选择模型**：使用模型选择器。
- **你自己产品面向客户的 API 密钥**，带有权限范围和过期时间：使用表格。
- **花费或速率限制**：使用用量计量。

## 用法

把 `Providers` 设为你支持的提供商，把 `Keys`（双向）绑定到服务器返回的数据，处理 `KeySaved` 和 `KeyRemoved` 以持久化，并设置 `TestKey` 在服务器上测试密钥。没有密钥的提供商显示为 Not set。没有 `TestKey` 时，测试会以 "No key test is set up" 失败，因此任何密钥都不会未经检查就显示为 Connected。

```csharp
card.Providers =
[
    new ProviderKeyProvider { Id = "anthropic", Name = "Anthropic", KeyPrefix = "sk-ant-api03-" },
    new ProviderKeyProvider { Id = "google", Name = "Google" },   // no documented prefix: no prefix check
];
card.Keys = await api.GetProviderKeysAsync();      // one ProviderKey per provider
card.TestKey = request => api.TestProviderKeyAsync(request.Provider, request.Key, request.CancellationToken);
card.KeySaved += (_, e) => api.SaveProviderKey(e.Provider, e.Key);
card.KeyRemoved += (_, e) => api.DeleteProviderKey(e.Provider);
```

### 结构

1. **标志**：提供商的图标（或首字母）放在带色调的圆角方块里。
2. **名称与密钥**：提供商名称，下方是等宽字体的掩码密钥（显示时为完整密钥）。
3. **状态与最近使用**：带图标的 Connected、Invalid 或 Not set，下方是 "Used 2 min ago"、"Never used" 或拒绝原因。按下状态会重新测试已保存的密钥。
4. **操作**：带倒计时圆环的显示、复制、轮换和删除；空行显示 Add key；删除后显示 Undo。
5. **内联表单**：密钥输入框与 Cancel、Save，以及一行提示，说明哪里不对、Testing connection 或 Connected。

### 你的 API

<Demo name="YourApi" />

`TestKey` 接收一个 `ProviderKeyTestRequest`：提供商的 id、添加或轮换时粘贴的密钥（重新测试已保存的密钥时没有密钥，请在服务器上按提供商测试），以及一个在表单关闭时取消的令牌。返回 `ProviderKeyTestResult.Ok(detail)` 或 `ProviderKeyTestResult.Fail("401 Unauthorized")`；抛出的异常按失败处理，并使用其消息。轮换时，该行会保留旧密钥，直到新密钥通过测试才替换。

| 事件 | 何时触发 |
|---|---|
| `KeysChanged` | 每次变化：添加、轮换、删除、通过 Undo 恢复或重新测试。此时 `Keys` 已更新。 |
| `KeySaved` | 新密钥通过了测试。在这里持久化；该行已显示为已连接。 |
| `KeyRemoved` | 撤销窗口关闭，所以删除只有在确定后才会到达你的服务器。 |
| `KeyCopied` | 密钥已被复制，可用于审计日志。 |

在生产环境中不要把完整密钥发送到客户端：每个 `ProviderKey` 只提供 `Last4`，`Value` 保持为 null。该行随后显示掩码密钥，并关闭显示和复制。

### 空状态

<Demo name="Empty" />

空的 `Keys` 列表会让每一行显示为 Not set，并带有 Add key 按钮。`Providers` 决定哪些提供商有一行以及它们的顺序；每个 `ProviderKeyProvider` 提供名称、表单检查的密钥前缀和最小长度，以及 `Mark` 图标或 `Monogram`。使用提供商文档中给出的前缀；文档没有给出前缀时，让 `KeyPrefix` 保持为空。

### 模拟测试

用于演示或原型时，设置 `Simulation` 而不是 `TestKey`：`ProviderKeysSimulation` 等待 `Delay`（700ms），让已保存的无效密钥以其自身的错误（或 `RejectedError`）再次失败，其余密钥都连接成功。不会发送任何内容，每个播报的结果都以 "Simulated, nothing was sent." 结尾。本页的演示使用真实的提供商名称以及其文档给出的密钥前缀；密钥是占位符，被拒绝的密钥是虚构的。

```xml
<arc:ProviderKeys>
    <arc:ProviderKeys.Simulation>
        <arc:ProviderKeysSimulation Delay="0:0:0.7" />
    </arc:ProviderKeys.Simulation>
</arc:ProviderKeys>
```


### 加载中

<Demo name="Loading" />

`IsLoading` 在密钥加载期间显示占位行，几何尺寸与真实的行一致。

### 窄卡片

<Demo name="Narrow" />

宽度在 560px 及以上时，每一行是一行多列。低于该宽度（`NarrowWidth`）时，状态移到名称旁边，密钥单独占一行，最近使用与操作共用一行；表单把输入框放在 Cancel 和 Save 上方。卡片在 360px 宽时仍不会出现横向滚动。

### 你自己的文字

`Labels` 包含卡片显示或朗读的每一个词，`Title`、`Description` 和 `Footer` 设置卡片自己的文字（设为 null 会隐藏描述或页脚）：

```csharp
card.Labels = new ProviderKeysLabels
{
    Connected = "Verbunden",
    NotSet = "Nicht gesetzt",
    AddKey = "Schlüssel hinzufügen",
    CopyName = provider => $"{provider}-Schlüssel kopieren",
};
```

### 最佳实践

- 在服务器上测试密钥（请求一次模型列表就够了），并返回提供商自己的错误。
- 只传 `Last4`；显示和复制适用于把密钥保存在设备上的产品。
- 让 `RevealDuration` 保持简短；五秒足以读取或比对一个密钥。

## API 参考

<!-- api: ProviderKeys -->

<!-- api: ProviderKey -->

<!-- api: ProviderKeyProvider -->

<!-- api: ProviderKeysSimulation -->

<!-- api: ProviderKeysLabels -->

## 无障碍

- 卡片以其标题命名。每个图标按钮都有针对提供商的名称，例如 "Copy the Anthropic key"。
- 状态从不只靠颜色表达：图标加上 Connected、Invalid、Not set 或 Testing。状态按钮的名称包含拒绝原因（"OpenAI key: Invalid, 401 Unauthorized. Test again"）。
- 显示是一个报告按下状态的开关；Rotate 和 Add key 报告内联表单是否展开。
- 一个礼貌的实时区域会播报显示、复制、测试结果、删除和撤销。`Simulation` 的测试结果会附加 "Simulated, nothing was sent."
- 表单的输入框名为 "Anthropic API key"，其帮助文本就是提示行；格式错误和拒绝会把它标记为无效。
- 焦点受控：表单会聚焦其输入框，Cancel 和 Escape 会把焦点还给打开表单的按钮，Remove 会把焦点移到 Undo，Undo 再把它还给 Remove。

### 键盘

| 按键 | 操作 |
|---|---|
| Tab | 依次经过每一行的状态（重新测试）、显示、复制、轮换和删除，空行则是 Add key |
| Enter、Space | 按下获得焦点的按钮 |
| Enter | 在表单中检查格式，然后测试并保存密钥 |
| Escape | 在表单中取消，并把焦点还给打开它的按钮 |

## 动效

| 变化 | 完整 | 减弱 | 关闭 |
|---|---|---|---|
| 状态、密钥、最近使用和提示文字 | 原地交叉淡化一次：淡出 100ms，淡入 160ms 并上升 2px | 短暂交叉淡化，不上升 | 立即 |
| 内联表单打开或关闭 | 高度弹簧（0.24s，无回弹）并伴随短暂淡化 | 立即 | 立即 |
| 显示倒计时 | 按钮周围的细线圆环在 `RevealDuration` 内线性清空 | 没有圆环（密钥仍会自动遮盖） | 没有圆环 |
| 撤销倒计时 | Undo 内的小圆环在 `UndoDuration` 内清空 | 没有圆环 | 没有圆环 |
| 眼睛图标 | 新图标从 0.6 放大并在 160ms 内淡入 | 仅淡入 | 立即 |
| 按下图标按钮 | 以弹簧缩放到 0.94 | 不缩放 | 不缩放 |
| 测试中的加载圈 | 每 0.8s 转一圈 | 每 1.6s 转一圈 | 静止 |

每段文字都位于固定的单元格中，状态列宽度固定，所以状态或密钥变化时不会改变尺寸。相对时间每 30 秒刷新一次。

## 相关组件

- `CopyButton`：复制单个值并即时确认。
- `Input`：带标签和错误提示的单行输入框。
- `ContextWindow` 和 `AgentStatus`：AI 产品设置与运行的其他部分。
