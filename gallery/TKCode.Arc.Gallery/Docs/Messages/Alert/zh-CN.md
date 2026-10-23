---
title: 警告提示
summary: 持续显示的消息，帮助用户恢复或继续操作。
source-hash: e9109aea0ed2
---

<Demo name="Tones" />

## 何时使用

`Alert` 是一条会一直留在页面上、直到其所涉情况解决的通知。它位于页面或表单的内容流中，紧挨着相关内容，并能随情况变化改变语气、标题和文字。当消息必须在用户移开视线后依然可见时，它是合适的工具。

- **账单与套餐通知**：账单页面上的银行卡扣款失败，或七天后续费的套餐。通知说明后果，并一直保留，直到更新付款方式。
- **配额与上限警告**：工作区已用 10 个席位中的 8 个，或导出队列接近每日上限。警告位于“邀请”或“导出”操作上方，让用户在触碰上限之前就看到它。
- **长时间变更的状态**：域名验证、同步或保存从“正在保存更改”变为“所有更改已保存”或“保存失败”。保留同一个 `Alert` 并改变它的内容，布局就不会跳动。
- **用户可以关闭的通知**：仪表板上的维护窗口提示，或设置表单中的小贴士。用户关闭后，下方内容会填补空缺。

## 何时不用

- **对已完成操作的确认**，例如点击后的“更改已保存”：用 `Toast`，它会自行消失。
- **排队的应用结果**，带有撤销或查看等操作，且可能同时到达：用 `ToastStack`。
- **用户必须先作出决定才能继续的事项**：用 `Dialog`。
- **针对单个字段的消息**，例如“请输入完整的邮箱地址”：用 `Input` 的 `ErrorMessage` 行，它与所描述的字段相连。
- **在所有页面上方显示的站点通知**：用 `AnnouncementBar`，它会轮播，并可记住关闭状态。
- **没有内容的页面或列表**：用 `EmptyState`。

## 用法

基础用法设置 `Title` 与 `Content`。默认的 `Tone` 为 `Info`。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:Alert Title="你的银行卡下个月到期"
               Content="更新付款方式，以保持套餐有效。" />
</UserControl>
```

`Alert` 派生自 Avalonia 的 `ContentControl`，因此 `Content` 与 `ContentTemplate` 的行为与任何内容控件一致。字符串类型的 `Content` 会成为描述行；其他任何内容都会原样显示在标题下方。它没有 `Size` 属性：宽度由其容器决定，因此请把它放在与所描述内容匹配的列中。

### 结构

1. **语气图标**（`Tone`）：18 px 的图标，描边 2 px，向下偏移 1 px 使其与标题第一行对齐，与文字之间相距 12 px。颜色随语气变化。
2. **标题**（`Title`）：14 px，中等字重，行高 19.6 px，使用默认前景色。标题会换行，不会截断。
3. **描述**（字符串形式的 `Content`）：12 px，使用次要文字颜色，位于标题下方 4 px，行高 16.8 px。空描述不占用空间。
4. **富内容**（非字符串形式的 `Content`）：与描述位于同一列，同样间隔 4 px。其中的文字通过 `TextElement` 继承 12 px 字号与次要颜色。
5. **关闭按钮**（`IsDismissible`）：位于右上角、28 px 的圆形幽灵按钮。仅当 `IsDismissible` 为 `True` 时显示，并被拉入 16 px 的内边距中（上下各 5 px，右侧 6 px），使其图标与标题对齐。
6. **表面**：内边距 16 px，1 px 边框，圆角为控件圆角，表面色背景，带静止阴影。

### 语气

`Tone` 决定图标、颜色以及变化时的播报方式。根据用户接下来需要做什么来选择语气，而不是根据事件看起来有多严重。

| 语气 | 图标 | 适用于 | 播报 |
|---|---|---|---|
| `Info`（默认） | 信息 | 中性的事实与提示：套餐续费日期、新功能 | 礼貌 |
| `Success` | 对勾 | 问题已解决：域名已验证、付款已到账 | 礼貌 |
| `Warning` | 带感叹号的三角形 | 需要尽快处理：上限将至、银行卡即将到期 | 礼貌 |
| `Danger` | 带叉的圆形 | 操作失败，用户必须处理：付款或同步失败 | 强提示 |

```xml
<StackPanel Spacing="12">
    <arc:Alert Tone="Success" Title="域名已验证"
               Content="发往 acme.dev 的邮件现在可以正常送达。" />
    <arc:Alert Tone="Warning" Title="你即将达到存储空间上限"
               Content="释放空间或升级，否则上传将暂停。" />
    <arc:Alert Tone="Danger" Title="付款失败"
               Content="无法扣除尾号为 4242 的银行卡付款。" />
</StackPanel>
```

请谨慎使用 `Danger`：如果每隔一条通知都是它，人们就会不再留意颜色。`Danger` 通知以强提示方式播报，因此屏幕阅读器会打断当前的朗读来读它。

### 原位变形

保留同一个实例，并在状态变化时修改它的属性。图标会替换，新文字升起出现，高度则以弹簧跟随变化。若改为替换控件，就会跳过这些过渡效果，页面也会跳动。

<Demo name="MorphInPlace" />

```csharp
// 同一个通知实例在保存过程中改变语气、标题和内容。
Status.Tone = AlertTone.Info;
Status.Title = "正在保存更改";
Status.Content = "正在将 3 个文件上传到工作区。";

await _workspace.SaveAsync(draft);

Status.Tone = AlertTone.Success;
Status.Title = "所有更改已保存";
Status.Content = "工作区中的所有成员现在都能看到最新版本。";
```

当失败路径需要用户能够采取行动的消息时，把下一步写进 `Content`（“检查网络连接后重试。”），而不是放进第二个通知。

### 关闭

设置 `IsDismissible` 即可显示关闭按钮。按下后，通知收起，然后引发 `Dismissed`。把 `IsOpen` 重新设为 `True` 会再次显示通知，它的图标也会从较小尺寸回到完整尺寸。

<Demo name="Dismiss" />

```csharp
var notice = new Alert { Tone = AlertTone.Warning, IsDismissible = true, Title = "周日计划维护" };
notice.Dismissed += (_, _) => SaveDismissedState();

// 稍后再次显示该通知。
notice.IsOpen = true;
```

`Dismiss()` 通过代码完成相同的操作：它会收起通知，并在收起动画结束后引发 `Dismissed`。把 `IsOpen` 设为 `False` 同样会收起通知，但不会引发 `Dismissed`，因为 `Dismissed` 表示用户的选择。若因其他原因隐藏通知，例如某个状态变化使它过时，请使用 `IsOpen`。

### 自定义内容

任何非文本的 `Content` 都会显示在标题下方。文本内容会应用详情样式；其他内容则原样显示。

<Demo name="CustomContent" />

```xml
<arc:Alert Title="有新版本可用">
    <WrapPanel HorizontalAlignment="Center" ItemSpacing="12" LineSpacing="12">
        <arc:Button Content="重新加载" Size="Small" Variant="Secondary" />
    </WrapPanel>
</arc:Alert>
```

自定义内容只保留一个操作或一个简短列表。通知内若包含表单，它就变成了卡片，而卡片应放在它自己的区块布局中。内容为纯数字或其他格式化的值时，会按其文本显示，因为 `Alert` 会用 `ToString` 转换 `IFormattable` 值。

### 由数据驱动

通知只负责显示，但它的语气、标题和可见性常来自视图模型。使用 CommunityToolkit.Mvvm 时，视图模型掌握状态，通知负责显示。只有当关闭操作需要反映到视图模型时，才对 `IsOpen` 使用双向绑定。

```csharp
public record PaymentResult(bool IsSuccess);

public abstract class BillingService
{
    public abstract Task<PaymentResult> RetryAsync(string invoiceId);
}

public partial class BillingViewModel : ObservableObject
{
    private readonly BillingService _billing;

    public BillingViewModel(BillingService billing) => _billing = billing;

    [ObservableProperty]
    private AlertTone _paymentTone = AlertTone.Danger;

    [ObservableProperty]
    private string _paymentTitle = "付款失败";

    [ObservableProperty]
    private bool _paymentNoticeOpen = true;

    [RelayCommand]
    private async Task RetryPaymentAsync()
    {
        var result = await _billing.RetryAsync("inv_2026_0412");
        PaymentTone = result.IsSuccess ? AlertTone.Success : AlertTone.Danger;
        PaymentTitle = result.IsSuccess ? "付款已到账" : "付款失败";
    }
}
```

```xml
<StackPanel Spacing="12">
    <arc:Alert Tone="{Binding PaymentTone}"
               Title="{Binding PaymentTitle}"
               IsOpen="{Binding PaymentNoticeOpen, Mode=TwoWay}"
               IsDismissible="True"
               Content="Acme Analytics 的发票 inv_2026_0412 仍未付款。" />
    <arc:Button Content="重试付款" Command="{Binding RetryPaymentCommand}" />
</StackPanel>
```

让通知的语气与标题绑定到它所反映的状态上，并让命令决定下一个状态。被关闭的通知会保持隐藏，直到视图模型把 `PaymentNoticeOpen` 设回 `True`。

### 焦点与关闭

通知出现时不接收焦点，因为通知不应打断输入。它唯一的焦点停靠点是关闭按钮，而关闭按钮只在 `IsDismissible` 为 `True` 时存在。关闭后焦点不会移到其他控件；如果用户正在通知内操作，请在 `Dismissed` 处理程序中自行移动焦点。

按通知判断关闭是一种选择还是一种修复。用户无需解决问题即可进行的关闭（例如维护提示）可以允许关闭。用户仍需修复的问题（例如付款失败）应保持打开，直到其状态改变，并且根本不应允许关闭。

### 示例：账单页面通知

一个 SaaS 管理后台的账单页面在套餐区域顶部显示三条通知。付款失败使用 `Danger`，因为工作区将在 2026 年 3 月 3 日失去访问权限。席位提醒使用 `Warning`，并说明下一步操作；收据使用 `Success` 且可关闭，因为无需任何操作。每个标题都说明情况，描述则告诉用户可以如何应对。

![带付款失败、席位提醒和收据通知的账单区块](images/BillingNotices.png)

```xml
<StackPanel Width="600" Spacing="12">
    <TextBlock Text="Billing" FontSize="20" FontWeight="Medium"
               Foreground="{arc:ArcDesignTokenResource Foreground}" />
    <arc:Alert Tone="Danger"
               Title="Payment failed for Acme Analytics"
               Content="We couldn't charge the card ending in 4242. Update it by March 3, 2026 to keep Pro features."
               IsDismissible="True" />
    <arc:Alert Tone="Warning"
               Title="You're close to your seat limit"
               Content="8 of 10 seats are in use. Remove an inactive member or add seats before you invite more." />
    <arc:Alert Tone="Success"
               Title="Invoice INV-2026-0412 paid"
               Content="A receipt was sent to billing@acme.com."
               IsDismissible="True" />
</StackPanel>
```

### 文案规范

- **标题**：用句子式大小写简短陈述情况，结尾不加句号：“Acme Analytics 付款失败”。屏幕阅读器首先读取标题，因此它必须能独立理解。
- **描述**：一到两句话，说明后果和下一步：“请在 2026 年 3 月 3 日前更新，以保留 Pro 功能。”日期与数量使用阿拉伯数字。
- **操作文字**：通知中含按钮时，使用动词加宾语：“更新付款方式”，而不是“点击这里”。
- **语气**：平实、冷静。指出问题但不指责，避免感叹号。

| 不要写 | 改为 |
|---|---|
| 错误！ | 付款失败 |
| 出了点问题。 | 无法保存更改。请检查网络连接后重试。 |
| 你的账户有危险！！！ | 你的套餐将于 2026 年 3 月 3 日结束。 |
| 点击这里解决。 | 更新你的付款方式。 |

### 最佳实践

**推荐**

- 每种情况只用一条通知，情况变化时改变它的语气和文字。针对同一问题再加一条通知，只会增加噪音，并留下过时的消息。
- 把通知放在它所描述的内容旁边，例如发票表格上方或席位设置内。它的含义取决于周围的上下文。
- 只为用户必须处理的失败使用 `Danger`。常规事件使用 `Danger`，会让人学会忽略这种颜色。
- 在情况解决之前保持通知打开，除非用户对此确实有需要作出的选择。

**避免**

- 为已完成的操作使用通知。简短的确认属于 `Toast`，它不会一直停留在页面上。
- 让付款或安全通知可以关闭。用户可能在问题未解决时就将其关闭。
- 每次状态变化都替换控件。新实例没有变形效果，页面会跳动。
- 在同一位置堆叠超过三条通知。页面会变成警告列表，重要的那条会被淹没。

### 常见问题

**设置 `IsOpen` 为 `False` 时会触发 `Dismissed` 吗？** 不会。只有在 `Dismiss()` 或关闭按钮收起通知之后才会引发 `Dismissed`，因此它表示用户做出的选择。从代码中隐藏通知只会收起它。

**关闭状态会在重启后保留吗？** 不会。`Alert` 不保存任何状态。若要记住关闭状态，请在引发 `Dismissed` 时把它保存到你自己的设置中，然后在启动时将 `IsOpen` 设为 `False`。如果通知是全站通知，`AnnouncementBar` 提供了配合 `DismissalKey` 的记住关闭功能。

**为什么标题和文字之间没有间距？** `Content` 为空或为 null 时，描述行会被完全移除，包括它的 4 px 间距。只有在 `Content` 有文字时才设置它。

**通知会在一段时间后自动隐藏吗？** 不会。`Alert` 没有计时器，也没有持续时间。应该自行消失的消息属于 `Toast`。务必设置 `Title`，因为它是整个通知的无障碍名称。

**如何在通知中放置按钮？** 将 `Content` 设为包含该按钮的面板，如自定义内容示例所示。按钮保留自己的尺寸与命令，通知则提供内边距与语气。

## API 参考

<!-- api: Alert -->

配套类型 `AlertTone`：

| 成员 | 图标 | 含义 |
|---|---|---|
| `Info` | 信息 | 中性信息（强调色）。这是默认值。 |
| `Success` | 对勾 | 操作已成功。 |
| `Warning` | 带感叹号的三角形 | 需要尽快关注。 |
| `Danger` | 带叉的圆形 | 操作失败。以强提示方式播报。 |

## 无障碍

- `Title` 为通知命名，`Content` 作为其辅助文字被朗读。标题变化时会重新播报。
- `Danger` 通知以强提示方式播报，其他语气以礼貌方式播报。富内容没有辅助文字，因此请把关键句子放在标题中，或放在富内容的文本块里。
- 关闭按钮的名称为 `Dismiss: {Title}`，让用户听到它关闭的是哪一条通知。
- 语气图标不向辅助技术暴露。语气通过标题和内容传达，因此即使没有颜色，标题也必须说明发生了什么。
- 关闭按钮是普通按钮：Tab 可以聚焦到它，Enter 或空格键可以激活。
- 显示通知时不接收焦点，因此不会打断输入。

### 键盘

| 按键 | 操作 |
|---|---|
| `Tab` / `Shift+Tab` | 当 `IsDismissible` 为 `True` 时，移入或移出关闭按钮 |
| `Enter` / `Space` | 关闭按钮获得焦点时，关闭通知 |

## 动效

- 显示与隐藏通知时，其高度以平滑弹簧展开或收起，下方内容随之收拢。
- 语气变化时，图标以短暂的缩放与模糊切换。变化的文字升起并逐渐清晰。
- 减弱动效时，去掉高度动画与位移。文字与图标只做淡入淡出，显示或隐藏立即完成。
- 关闭动效时直接到达最终状态。

| 变化 | 完整 | 减弱 | 关闭 |
|---|---|---|---|
| 通知显示（`IsOpen` 设为 `True`） | 高度以弹簧展开，文字淡入，图标从较小尺寸过渡到完整尺寸 | 以完整高度显示，文字与图标淡入 | 立即完成 |
| 通知被关闭或隐藏 | 高度以平滑弹簧收起，通知淡出，下方内容上移 | 立即完成 | 立即完成 |
| 语气变化 | 图标以缩放与短暂模糊切换 | 图标淡入淡出 | 立即完成 |
| 标题或内容变化 | 新文字从柔和模糊中升起，高度以弹簧调整至合适尺寸 | 文字淡入淡出 | 立即完成 |

## 相关组件

- `Toast`：用于已完成操作的简短确认。
- `ToastStack`：用于排队、更新和堆叠的应用通知。
- `AnnouncementBar`：用于网站顶部轮播的横幅。
- `EmptyState`：完全没有内容可显示时使用。

| 组件 | 适用场景 |
|---|---|
| `Toast` | 已完成的操作需要一条会自行关闭的简短确认 |
| `ToastStack` | 应用中的多个结果或撤销操作需要堆叠显示 |
| `AnnouncementBar` | 站点级消息显示在每个页面上方并轮播 |
| `Dialog` | 用户必须先作出回应，页面才能继续 |
| `Input` | 消息属于某个字段，显示在其描述或错误行中 |
| `EmptyState` | 列表或页面没有内容可显示 |
