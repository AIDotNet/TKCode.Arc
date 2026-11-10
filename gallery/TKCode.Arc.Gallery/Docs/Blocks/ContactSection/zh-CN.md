---
title: 联系区块
summary: 校验后变形为确认信息的联系表单、支持渠道，以及带当地时间的办公室卡片。
source-hash: dd414720dc34
---

<Demo name="Preview" />

## 何时使用

联系页面只有一个任务：把问题送到对的人手里。`ContactSection` 提供三种布局，按你希望被联系的方式选用。

- **一个入口**：`Form` 询问姓名、地址、主题和留言，校验后变成确认信息。
- **多个入口**：`Channels` 列出聊天、邮件、销售等方式，旁边的面板显示所选方式的详情。
- **真实地点**：`Offices` 为每个办公室显示一张卡片，包含地址、实时当地时间以及是否营业。

## 何时不用

- **只收集邮箱地址**：请使用 `NewsletterSignup`。
- **分多步的长表单**：请使用 `MultiStepForm`。
- **页面上就能回答的问题**：先使用 `FaqSection`，再从它的联系行链接到这里。

## 用法

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:ContactSection Title="Talk to us" />
</UserControl>
```

```csharp
contact.SubmitHandler = async message =>
{
    var response = await http.PostAsJsonAsync("/api/contact", message);
    response.EnsureSuccessStatusCode();
};
```

`ContactSection` 派生自 `TemplatedControl`。**在应用中请设置 `SubmitHandler`**：否则表单只会等待 1.1 秒并报告成功，不会发送任何内容。`Channels` 和 `Offices` 为空时显示示例数据，也请替换。

### 布局

| `Variant` | 显示内容 | 需设置 |
|---|---|---|
| `Form` | 标题和两条信息，旁边是表单卡片 | `SubmitHandler`、`Topics`、`ResponseNote`、`ContactEmail` |
| `Channels` | 标签列表，旁边是详情面板 | `Channels`、`SelectedChannel` |
| `Offices` | 每个办公室一张卡片 | `Offices` |

`Title` 和 `Description` 可替换任一布局的标题。未设置时，每种布局显示自己的文案。

### 表单

按下 **Send message**，或在姓名、邮箱字段中按 `Enter`，都会校验字段。第一个错误的字段获得焦点，此后错误信息随输入更新。

| 字段 | 规则 |
|---|---|
| Name | 不能为空 |
| Work email | 没有空格，一个 @，域名中有点，且点后至少两个字符 |
| Message | 至少 20 个字符，最多 500 个，并实时计数 |

随后 `SubmitHandler` 收到一个 `ContactMessage`，其中是去除首尾空格后的值，此时字段为只读，按钮显示加载状态。任务完成后，卡片以弹簧变形为确认信息，其中写明对方的姓名和主题，焦点移到它的标题上。任务失败时，表单和已输入的内容都会保留，字段下方显示一条消息。

<Demo name="FailedSend" />

主题是一组单选标签：高亮滑向所选项，方向键可以移动它。`Topics` 可替换默认的四个主题。

### 渠道

每个 `ContactChannel` 都有 `Value`、`Label`、其下一行 `Meta`、一个 24×24 的 `Icon` 和 `Detail`（可以是文本或任意控件）。`SelectedChannel` 是双向的，保存 `Value`。面板以弹簧变化到每个详情的高度，内容沿移动方向滑动。

```csharp
contact.Variant = ContactSectionVariant.Channels;
contact.Channels.Add(new ContactChannel
{
    Value = "email",
    Label = "Email the team",
    Meta = "One working day",
    Icon = ArcIcons.Mail,
    Detail = "Attach files or a short recording. A person answers every message."
});
```

### 办公室

每个 `ContactOffice` 都有 `City`、`TimeZone` id、`Address` 各行、可选的 `Email`，以及当地 24 小时制的 `OpenHour` 和 `CloseHour`（默认 9 和 18）。办公室在周一至周五的这些时段内营业。卡片在屏幕上时每 15 秒刷新一次，只有这种布局运行计时器。系统不认识的时区显示 `--:--` 和 “Checking hours”。浏览器版本可能需要为此提供时区数据。

<Demo name="Offices" />

### 最佳实践

**建议**

- 说明回复需要多久，并遵守承诺。
- 通过任务把错误返回，让对方保留已输入的内容。
- 用文字写明营业状态：卡片会把它写出来，而不只是用颜色表示。

**避免**

- 索取超出需要的信息。每多一个字段都会流失一些人。
- 不设置 `SubmitHandler` 就上线。表单看起来成功了，其实什么都没有发送。

## API 参考

<!-- api: ContactSection -->

<!-- api: ContactMessage -->

<!-- api: ContactChannel -->

<!-- api: ContactOffice -->

## 无障碍

- 字段有可见的标签，错误在第一次尝试后出现并随输入更新。发送失败会立即播报。
- 焦点会移到第一个错误的字段、确认信息的标题，以及按下 “Send another message” 后回到姓名字段。
- 主题和渠道各自只占一个 Tab 停靠点，办公室的营业状态用文字写明。

### 键盘

| 按键 | 操作 |
|---|---|
| 方向键 | 在主题标签或渠道之间移动，到两端时循环 |
| `Home` / `End` | 第一个或最后一个渠道 |
| `Enter` | 在姓名或邮箱字段中发送表单 |
| `Tab` / `Shift+Tab` | 在字段和操作之间移动 |

## 动效

- 卡片从旧高度以弹簧变化到新高度（0.4 秒，无回弹）。新内容延迟 40 ms 后，在 240 ms 内从 4 px 模糊中移入 18 px，旧内容在 160 ms 内移出 12 px。
- 确认信息的勾选标记延迟 160 ms 后在 420 ms 内绘制。主题或渠道的高亮以 morph 弹簧滑动。

| 变化 | Full | Reduced | Off |
|---|---|---|---|
| 卡片切换内容 | 高度弹簧、滑动和模糊 | 交叉淡化 | 立即 |
| 高亮移动 | Morph 弹簧 | 立即 | 立即 |
| 勾选标记绘制 | 420 ms | 立即显示 | 立即显示 |

## 响应式

- `ContactSection` 跟随自身宽度，而不是窗口宽度。标题从 28 px 缩放到 52 px。
- 宽度不超过 820 px（`:stacked`）时，表单移到标题下方，渠道列表变成可横向滚动的一行，并隐藏说明行。
- 宽度不超过 520 px（`:compact`）时，姓名和邮箱上下堆叠，发送按钮占满宽度，内边距收紧。
- 办公室卡片按可用空间排成尽量多的列，每列至少 240 px。
- 触控：标签和按钮至少 36 px 高，没有信息依赖悬停。

## 相关组件

| 组件 | 何时选择 |
|---|---|
| `ContactSection` | 需要让人联系到你 |
| `FaqSection` | 答案可能已经在页面上 |
| `NewsletterSignup` | 只需要一个邮箱地址 |
| `MultiStepForm` | 表单分多个步骤 |
