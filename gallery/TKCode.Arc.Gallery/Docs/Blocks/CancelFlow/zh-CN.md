---
title: 取消订阅流程
summary: 贴心的取消流程：询问原因、提供合适的挽留方案、说明哪些内容将结束、按住确认，并允许快速恢复。
source-hash: e5d8f00ecc9e
---

<Demo name="Flow" />

## 何时使用

- 账单或套餐页面上的取消按钮：离开应当让人稍作考虑，但绝不能让人感觉被困住。
- 订阅中，优惠、暂停或一次沟通往往比离开更贴合用户的原因。
- 任何不可逆的套餐变更，都需要清楚说明哪些内容将在何时结束，并请用户明确确认。

## 何时不用

- 删除单个项目或单个席位：请在原位使用 `ConfirmMorph` 或 `HoldToConfirm`。
- 降级到另一个付费套餐：请用 `RadioCards` 或定价表并排展示各套餐。
- 法律或条款要求一键取消的情况：不要提供挽留方案（不设置任何挽留方案），或直接链接到条款所描述的确认页面。

## 用法

`CancelFlow` 是一张卡片。第一步询问原因，用 `RadioGroup` 列出各个原因；选定原因后，列表下方会展开一个可选的反馈输入框。点击“继续”会显示该原因的 `Offer`（若没有，则显示 `DefaultOffer`）。之后在挽留方案页面操作，或在没有挽留方案时直接进入确认步骤：确认步骤会列出 `Losses`（即将结束的内容），并请用户按住一个按钮。按住到底会进入已取消步骤，并引发 `CancellationConfirmed`；接受挽留方案则进入已保留步骤，并引发 `OfferAccepted`。

预览为两个原因设置了各自的挽留方案（“Take 40% off for 3 months”和“Pause instead”），其余原因则回退到默认挽留方案。`AccessEndsOn`（付费周期的结束日期）和 `PlanName`（套餐名称）用于填充句子。

```csharp
flow.AccessEndsOn = new DateTime(2026, 11, 1);

flow.CancellationConfirmed += (_, e) => billing.Cancel(e.Reason.Title, e.Feedback);
flow.OfferAccepted += (_, e) => billing.Apply(e.Offer.Title);
flow.KeepPlanRequested += (_, _) => page.CloseCancelFlow();
```

### 没有挽留方案时

当选定的原因和 `DefaultOffer` 都没有挽留方案时，流程会从原因直接进入确认步骤，步骤栏由三段变为两段。若 `Reasons` 留空，流程会使用 `Labels.DefaultReasons` 中的五个原因。`SelectedReason` 和 `Feedback` 为双向绑定；本示例的第一个原因会追问一个问题，因此反馈输入框显示“What were you missing?”。

<Demo name="NoOffer" />

### 在对话框中

当流程位于 `Dialog` 中时，将 `Appearance` 设为 `Plain`，即可去掉卡片的边框、阴影和内边距。“保留我的套餐”和“完成”都会引发 `KeepPlanRequested`，对话框会因此关闭。“重新激活套餐”会引发 `ReactivateRequested`，然后重新开始流程；将该事件标记为已处理，即可让已取消步骤保留在屏幕上。没有 `Icon` 的挽留方案会显示礼物图标。

<Demo name="InDialog" />

### 文本与其他语言

`CancelFlowLabels` 包含流程中的所有内置文字。若要改写或翻译，请整体替换该对象；日期依 `Labels.Culture` 格式化。

```csharp
flow.Labels = new CancelFlowLabels
{
    Culture = CultureInfo.GetCultureInfo("nl-NL"),
    ReasonTitle = "Waarom zeg je op?",
    Continue = "Doorgaan",
    KeepPlan = "Abonnement houden",
    HoldToCancel = "Houd vast om op te zeggen",
};
```

## API 参考

<!-- api: CancelFlow -->

`CancelFlowReason`

| 属性 | 类型 | 说明 |
|---|---|---|
| `Title` | `string` | 人们会说出口的原因，例如 “Too expensive”。 |
| `Description` | `string?` | 标题下方的一行简短说明。 |
| `Offer` | `CancelFlowOffer?` | 选择该原因后显示的挽留方案；为 null 时回退到 `DefaultOffer`。 |
| `AsksForDetails` | `bool` | 为 true 时，反馈输入框会询问 `DetailsPrompt`，而不是通用问题。回答仍为可选。 |
| `DetailsPrompt` | `string?` | 追问的问题，例如 “What were you missing?”；为 null 时使用 `Labels.DetailsPrompt`。 |

`CancelFlowOffer`

| 属性 | 类型 | 说明 |
|---|---|---|
| `Title` | `string` | 提供的内容，例如 “Take 40% off for 3 months”。 |
| `Description` | `string?` | 该挽留方案对套餐和账单意味着什么。 |
| `AcceptLabel` | `string?` | 接受按钮的文字，例如 “Claim 40% off”；为 null 时使用 `Labels.AcceptOffer`。 |
| `Icon` | `Geometry?` | 标题上方图块中的 24×24 描边图标；为 null 时显示礼物图标。 |
| `AcceptedTitle` | `string?` | 已保留步骤的标题，例如 “40% off is on”；为 null 时使用 `Labels.RetainedTitle`。 |
| `AcceptedDescription` | `string?` | 标题下方的句子；为 null 时使用 `Labels.RetainedDescription`。 |

`CancelFlowLabels`（均为 `init`；函数会根据套餐名称和格式化后的日期生成句子）

| 属性 | 默认值 |
|---|---|
| `Culture` | en-US |
| `StepCounter` | "Step {n} of {total}" |
| `Back` | "Back" |
| `ReasonTitle` / `ReasonDescription` | "Why are you canceling?" / "Your answer helps us improve {plan}." |
| `FeedbackLabel` / `DetailsPrompt` / `FeedbackPlaceholder` | "Anything else we should know?" / "Tell us a little more" / "Optional" |
| `KeepPlan` / `Continue` | "Keep my plan" / "Continue" |
| `AcceptOffer` / `ContinueCanceling` | "Accept offer" / "Continue canceling" |
| `ConfirmTitle` / `AccessUntil` / `AfterThat` | "Cancel your {plan} plan?" / "You keep access until {date}." / "After that:" |
| `LossesName` / `HoldToCancel` / `HoldConfirmed` | "What ends" / "Hold to cancel plan" / "Canceled" |
| `CanceledTitle` / `CanceledDescription` | "Your plan is canceled" / "You can keep using {plan} until {date}. We sent a confirmation to your email." |
| `Reactivate` | "Reactivate plan" |
| `RetainedTitle` / `RetainedDescription` / `Done` | "{offer} is on" / "Thanks for staying. You'll see the change on your next invoice." / "Done" |
| `DefaultReasons` | Too expensive; Not using it enough; Missing a feature I need; Switching to another tool; Something else |

## 无障碍

- 每次步骤切换后，焦点会移到新步骤的标题，标题以礼貌方式朗读。标题可以获得焦点，但不是 Tab 停靠点，因此 Tab 会直接前往该步骤的第一个控件。
- 原因列表是以步骤标题命名的单选组。方向键在各原因之间移动，整个分组只占一个 Tab 停靠点。
- Enter 执行步骤的主要操作：选定原因后为“继续”，挽留方案页面为接受按钮，完成步骤为“完成”。未选定原因时，Enter 会先选中该原因；在反馈输入框中，Enter 会换行（Ctrl+Enter 或 Cmd+Enter 则继续）。从上一步带过来且仍按住的 Enter 会被忽略。
- Alt+Left 可从挽留方案和确认步骤返回。圆形的返回按钮名称为“返回”，并带有工具提示。
- 取消需要刻意的按住操作：按住按钮，或让其获得焦点后按住 Space 或 Enter。帮助文字会说明这一点。
- 折叠的反馈输入框在选定原因之前不在 Tab 顺序中。

## 动效

- 步骤沿行进方向滑动 12px（前进时从右侧进入，后退时从左侧进入），并伴随短暂的淡入淡出；卡片高度以弹簧方式跟随新步骤变化。
- 反馈输入框以高度弹簧展开和收起。步骤栏的分段会交叉淡入为新颜色，返回按钮则在其所占空间收起时淡入淡出。
- 在最后一步，对勾会在约半秒内自行绘制出来。
- 减少动效时只保留淡入淡出：步骤在原位交叉淡入，分段颜色快速变化，对勾立即绘制。关闭动效时，所有变化都在原位立即完成。

## 相关组件

- 其他设置区块：`SettingsPage`、`SettingsCommand`、`SecuritySettings`、`BillingOverview`（其中的“取消套餐”按钮是进入此流程的自然入口）以及 `UsageBilling`。
- 本流程由 `RadioGroup`、`Textarea`、`HoldToConfirm`、`Button` 和 `Dialog` 组成。
- `ConfirmMorph`：用于较小的破坏性操作，提供更轻量的原位确认。
