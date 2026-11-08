---
title: 席位管理
summary: 一张团队席位卡片，包含头像行和席位条（显示成员、邀请和空余席位），一个步进器，用来滚动显示今天的按比例计费金额和新的月度总额，任何收费之前都有确认步骤，邀请可以重新发送或带撤销恢复地撤销，并有一个满员状态，提供添加另一个席位的选项。
source-hash: faa5dbe05cf4
---

<Demo name="Preview" />

## 何时使用

`SeatManager` 是团队账单页面中的席位区块。它显示已付费的席位数量以及由谁占用，让管理员更改席位数，并在收费之前显示价格影响，同时把待接受的邀请（它们同样占用席位）保留在同一位置。

- **按席位订阅。** 席位数就是订阅数量。步进器显示现在增加席位的费用（按计费周期剩余天数按比例计算），以及续费时的周期总额会变成多少。
- **席位上限出现的时刻。** 当所有已付费席位都被占用时，提示行会说明这一点，并提供 "Add a seat"，这样刚刚尝试邀请他人的管理员一键即可解决。
- **占用席位的邀请。** 每个待接受的邀请都会占用一个席位，直到被接受或撤销。撤销会立即释放席位，并附带一个短暂的撤销恢复窗口，因此可以马上减少席位数。

## 何时不用

- **多名成员的角色与权限。** 使用 `Team members` 之类的成员表格，并链接到本卡片来管理席位。
- **用量或额度计费。** 当账单随消耗量变化时，使用 `Usage billing`、`Usage pricing` 或 `Credit top-up`。
- **在套餐之间选择。** 使用 `Plan comparison` 或 `Pricing calculator`；本卡片只在一个套餐之内工作。

## 用法

该区块派生自 Avalonia 的 `TemplatedControl`。为它提供套餐、已付费席位数、成员、邀请以及真实的计费周期，使其按比例计算与你的计费系统一致。然后把三个处理函数连接到你的后端。

```xml
<arc:SeatManager x:Name="Seats"
                 Workspace="Tidewater"
                 Purchased="12"
                 PeriodStart="2026-10-18"
                 PeriodEnd="2026-11-18"
                 ReceiptEmail="accounts@tidewater.example">
    <arc:SeatManager.Plan>
        <arc:SeatManagerPlan Name="Team" PricePerSeat="18" MaxSeats="25" />
    </arc:SeatManager.Plan>
    <arc:SeatManager.PaymentMethod>
        <arc:SeatManagerPaymentMethod Brand="Nova" Last4="1881" ProcessorName="Ledgerline" />
    </arc:SeatManager.PaymentMethod>
</arc:SeatManager>
```

```csharp
Seats.Members.Add(new SeatManagerMember { Id = "m1", Name = "Mira Okafor" });
Seats.Invites.Add(new SeatManagerInvite
{
    Id = "i1",
    Email = "theo.lindqvist@tidewater.example",
    InvitedBy = "Mira Okafor",
    Note = "3 days ago"
});

Seats.UpdateSeatsAsync = async (quote, cancellation) =>
{
    var response = await http.PostAsync("/api/billing/seats", JsonContent.Create(new { seats = quote.Target }), cancellation);
    if (!response.IsSuccessStatusCode)
    {
        throw new InvalidOperationException("The card was declined. Nothing was charged.");
    }
};
Seats.ResendInviteAsync = (invite, cancellation) => http.PostAsync($"/api/invites/{invite.Id}/resend", null, cancellation);
Seats.RevokeInviteAsync = (invite, cancellation) => http.DeleteAsync($"/api/invites/{invite.Id}", cancellation);
```

`SeatManagerSamples.Apply(block)` 用本页使用的虚构团队 Tidewater 填充区块。

### 结构

- **标题**：标题、套餐名称（以徽标形式显示），以及一行信息，包含工作区、每个席位的价格和续费日期。
- **概览**：前四位成员以头像显示，其余以 "+N" 表示；"12 of 12 seats in use"；席位条，每个席位一格；图例（成员、已邀请、空余，以及右侧正在添加或移除的席位）；以及提示行。
- **报价**：已付费席位数、步进器（减号、席位数、加号）、今天应付的金额及其说明，以及从续费日起的周期总额，旧总额作为其说明。
- **待接受的邀请**：每个邀请一行，带有重新发送和撤销操作，或显示 "No pending invites"。
- **页脚**：席位计费到的卡片，或以文字说明的变化，以及 "Update seats" 按钮。

### 席位条与提示

席位条用强调色绘制成员，用更浅的强调色色调绘制已邀请的席位，用空心格子绘制空余席位，用末尾的强调色空心格子绘制正在添加的席位，用淡色轮廓绘制正在移除的席位。提示行始终说明最有用的信息：刚刚触碰到的上限，然后是正在添加的席位，然后是正在移除的席位，然后是空余席位数，最后是团队已满。

### 更改席位

步进器受限于已占用的席位（成员加邀请）以及套餐的 `MaxSeats`。超出任一上限时，按钮仍然可用，并在提示行中解释该上限，持续 2.6 秒。只能把席位减少到已占用的席位数，因此移除的总是空余席位；它们今天不产生费用，并在续费时生效。

```csharp
// Today's charge for added seats: delta × price × days left ÷ days in period, rounded to cents on the total.
var quote = SeatManagerQuote.Build(current: 12, target: 15, plan, periodStart, periodEnd, DateTime.Today);
// quote.DueToday == 38.32m, quote.NextTotal == 270m for the Team plan at $18.
```

`SeatManagerQuote.Build` 与卡片使用的计算相同，因此服务器可以核对这些数字。每当步进器改变报价时都会触发 `QuoteChanged`，如果你想在旁边显示自己计费系统的发票预览，可以使用它。

### 确认、付款、完成

"Update seats" 会在页脚打开确认步骤：一个问题，以及返回和付款按钮（只移除席位时付款按钮显示为确认）。此期间步进器被锁定，按 **Escape** 可返回。付款会运行 `UpdateSeatsAsync`，按钮上显示加载指示，页脚显示一行闪烁的文字。成功时，`Purchased` 变为新的数量，触发 `SeatsChanged`，页脚显示相应提示 2.8 秒。抛出异常时，异常消息保留在页脚中，并附带重试按钮。

<Demo name="Backend" />

### 重新发送与撤销

重新发送时按钮上显示加载指示，然后显示对勾和 "Sent"。撤销会立即释放席位，并把该行变为撤销恢复行，持续 `UndoDuration`（3 秒）。只有当这个窗口关闭时，`RevokeInviteAsync` 才会运行，该行才会收起；如果处理函数抛出异常，邀请会回到原来的位置。

### 状态

<Demo name="States" />

### 窄容器

<Demo name="Narrow" />

### 其他文字、货币与动效

所有文字都位于 `Labels`（一个 `SeatManagerLabels`）中；它的 `Culture` 负责金额和日期的格式。套餐的 `Currency` 和 `Interval` 决定金额以及 "/mo" 或 "/yr" 后缀。没有 `PaymentMethod` 时，页脚不显示卡片。

<Demo name="Custom" />

### 演示循环

在没有设置任何处理函数时，卡片会模拟后端（付款 900 毫秒，重新发送 660 毫秒），并播放一个 12.7 秒的循环：添加三个席位、付款、重新发送第一个邀请、撤销第二个邀请，然后回到起点。卡片在屏幕之外时循环会等待，对屏幕阅读器保持静默，并在卡片内第一次按下指针或按键时永久停止。设置 `IsAutoPlayEnabled="False"` 可以关闭它，设置为 `True` 则可以在已设置处理函数时保留它。

### 最佳实践

- 传入真实的计费周期和"今天"（`Now`），使按比例计算的金额与发票一致。
- 从 `UpdateSeatsAsync` 中抛出异常时，使用为付款人编写的消息：它会原样显示。
- 当 `Members` 只是用于头像的示例时，请保持 `MemberCount` 准确。
- 让卡片可以拉伸；它最宽为 560 px，并在 460 px 以下收紧。

### 常见问题

- **为什么不能低于已占用的席位数？** 移除某人占用的席位会把他们锁在外面。先撤销邀请；席位会立即释放。
- **邀请何时才真正被撤销？** 撤销恢复窗口关闭时。在此之前，不会有任何内容发送到你的后端。
- **没有处理函数时，卡片会收费吗？** 不会。它会模拟收费，并对屏幕阅读器说明这一点。

## API 参考

<!-- api: SeatManager -->

<!-- api: SeatManagerPlan -->

<!-- api: SeatManagerMember -->

<!-- api: SeatManagerInvite -->

<!-- api: SeatManagerPaymentMethod -->

<!-- api: SeatManagerQuote -->

<!-- api: SeatManagerPhase -->

<!-- api: SeatManagerLabels -->

## 无障碍

- 标题是二级标题。头像行是一张名为 "10 members" 的图片。
- 席位条是一张图片，带有完整描述（"12 of 15 seats in use: 10 members, 2 invited, 3 open"）；图例以文字重复计数，因此颜色不是唯一的信号。
- 席位数是一个范围值，带有最小值、最大值和当前值，朗读为 "15 seats"。
- 减号和加号在达到上限时仍可获得焦点，并在提示行中解释原因，而不是保持沉默。
- 礼貌的实时区域会播报审核、更新、重新发送、撤销、移除和撤销恢复。没有处理函数时，它会加上 "Simulated, nothing was charged"（或 "sent"）。演示循环不播报任何内容。
- 滚动数字暴露其真实数值；数字条带对屏幕阅读器隐藏。
- 每个邀请操作都有自己的名称，例如 "Revoke invite to theo.lindqvist@tidewater.example"。
- **Tab** 依次经过 "Add a seat" 操作（团队已满时）、减号、席位数、加号、每个邀请的操作以及页脚按钮。没有可更新的内容时，会跳过 "Update seats"。
- 在席位数上：**Arrow up** 或 **Arrow right** 增加一个席位，**Arrow down** 或 **Arrow left** 减少一个，**Page up** / **Page down** 每次移动五个，**Home** 跳到已占用的席位数，**End** 跳到套餐的最大值。
- **Space** 或 **Enter** 按下已获得焦点的按钮；减号和加号按住时会重复。
- **Escape** 离开确认步骤。
- 确认步骤打开时，焦点移到付款按钮；点击返回后，焦点回到 "Update seats"；成功更新后，焦点移到席位数。"Add a seat" 把焦点交给加号；已撤销邀请的 "Undo" 会把焦点从撤销按钮拿走，并在恢复后归还。

## 动效

- **数字**滚动：每个数字都是一条 0–9 的条带，按自身的变化以缓出方式滑动 240 毫秒，全部同时进行。新增的位数（以及移位的 "$"）在 160 毫秒内淡入；宽度从不做动画。
- **席位格**从零开始增长，并在无回弹的 0.24 秒弹簧下缩小到零，同时相邻的格子让出空间；仅改变类型的格子在 160 毫秒内重新着色。
- **文字切换**（提示、说明、页脚、邀请行、重新发送 → Sent）交叉淡变：旧文字在 100 毫秒内淡出，新文字在 160 毫秒内淡入，并上移 2 px。
- **已撤销的邀请**在约 120 毫秒内淡入，同时其行在同一弹簧下收起。
- 减弱动效和关闭动效会立即应用所有变化；状态、数字和循环保持不变。

## 响应式

- `SeatManager` 读取自身的宽度。它最宽为 560 px；低于 460 px（`:narrow`）时，内边距从 24 px 变为 20 px，金额旁边的说明以及图例中正在添加或移除的条目会隐藏。
- 地址、说明和页脚行以省略号结尾；步进器保留 120 px，金额永不换行。

## 相关组件

- `Usage billing` 与 `Billing overview`，即账单页面其余部分的设置区块。
- `Credit top-up` 和 `Checkout summary`，即其他带确认步骤的定价区块。
- `Avatar group`、`Button` 和 `Text shimmer`，即本卡片所用的组成部分。
