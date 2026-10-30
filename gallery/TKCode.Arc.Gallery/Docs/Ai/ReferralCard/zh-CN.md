---
title: Referral card
summary: 双向奖励的推荐卡片：复制或分享邀请链接，用标签形式按邮箱邀请好友，看着邀请从已邀请到已注册再到已赚取，奖励金额随之累加，里程碑奖励随之解锁。
source-hash: c111993428ce
---

<Demo name="Preview" />

## 何时使用

`ReferralCard` 把整个推荐计划放进一张卡片。顶部是邀约（"Give $20, get $20"），邀请链接放在一个安静的字段里，旁边是复制按钮和三个分享按钮，邮箱字段会把输入的每个地址变成一个标签。下方三个滚动数字分别统计已邀请、已注册和已赚取，分段进度条显示离下一个奖励还有多远，被邀请的人在原位从 Invited 变为 Signed up 再变为 Earned。

- **推荐或邀请计划**，放在账户设置、账单或新手引导清单中。
- **展示邀请带来的收益**，以及离额外奖励还有多远。

## 何时不用

- **带角色和权限的团队成员邀请**：使用成员表格。
- **公开排行榜或多列的联盟佣金结算**：使用表格。
- **一次性的分享链接**，没有需要跟踪的内容：一个字段加 `CopyButton` 就够了。

## 用法

把后端返回的推荐记录按从新到旧传给 `Referrals`，注册和付款发生时替换这个列表：计数、里程碑和列表都由它推导而来。发送邀请会调用 `InviteHandler`；服务器接受后返回即可，失败时抛出异常，异常消息就是用户会看到的文字。

```csharp
card.Link = $"https://app.example.com/r/{me.Code}";
card.Reward = 20;
card.Referrals = referrals;            // IReadOnlyList<Referral>, newest first
card.Milestone = new ReferralMilestone
{
    Goal = 5,
    Reward = "3 months free",
    Detail = "Applied to your next invoice",
};
card.InviteHandler = async (emails, token) =>
{
    var response = await http.PostAsJsonAsync("/api/referrals/invite", emails, token);
    if (!response.IsSuccessStatusCode)
    {
        throw new InvalidOperationException("Could not send the invites. Nothing was sent.");
    }
};
card.MilestoneReached += (_, e) => analytics.Track("referral_milestone_unlocked");
```

没有 `Referrals` 也没有 `Simulation` 时，卡片为空：计数为零，列表显示 "No invites yet. Friends you invite show up here."，没有 `Reward` 时标题为 "Invite friends"；`Link` 和 `Milestone` 也为空。包里不附带示例计划。要展示一个推进中的计划（预览、新手引导），请给卡片一个 `ReferralCardSimulation`：它起始的列表，以及让好友依次经历各个阶段的节拍。`Referrals` 为 null 时，卡片显示模拟的列表，并在卡片有 40% 进入屏幕时播放一次节拍；上方的预览就是这样播放展示库编写的虚构计划（一位已注册的好友完成订阅，一位已邀请的好友先注册再订阅，第五个付费推荐解锁里程碑）。

```csharp
card.Simulation = new ReferralCardSimulation
{
    Referrals = program,                                   // the list it starts from, newest first
    Beats =
    [
        new(TimeSpan.FromSeconds(1.4), "p4", ReferralStage.Earned),
        new(TimeSpan.FromSeconds(2.2), "p5", ReferralStage.SignedUp, "Ana Souza")
    ]
};
```

设置 `Referrals` 会把卡片交给你的数据并停止计划；每张卡片使用自己的模拟。`Simulate` 可以关掉播放；对使用你自己列表的卡片也可以打开它（此时每次模拟发送的第一个地址会依次经历各个阶段）。

### 结构

1. **邀约**：礼物标记、由 `Reward` 和 `Currency` 生成的标题，以及说明双方各得什么的一行文字。
2. **邀请链接**：域名为弱化色、邀请码为正文色，末尾是复制按钮，另有三个分享按钮：动态、人脉和邮件。
3. **按邮箱邀请**：固定高度、可横向滚动的字段中的标签，发送按钮，以及下方的提示。
4. **漏斗**：已邀请、已注册和已赚取，以等宽数字滚动显示。
5. **里程碑**：每个所需的付费推荐一个分段（最多 12 段），以及一行说明它解锁的奖励。
6. **你的邀请**：每个人的头像、邮箱、日期和阶段（图标加文字），放在固定高度的列表中。

### 你的数据

<Demo name="YourData" />

当服务报告有人注册或付款时，用新列表替换 `Referrals`；各行按 `Id` 保持位置，只有变化的部分会动。发送成功后，这些地址会以 Invited 状态出现在列表顶部，直到你的下一个列表到来。

### 分享

每个分享按钮都会触发 `ShareRequested`，带上渠道和一个现成的 URL。打开它并设置 `Handled`；没有处理程序这样做时，分享是模拟的，实时区域会说明什么也没有发布。邮件按钮会生成一个 `mailto:` 链接，正文是 `ShareMessage` 和链接。动态和人脉按钮使用通用图标，而不是社交网络的标志：把你所用网络的分享地址写进 `PostShareUrlFormat` 和 `NetworkShareUrlFormat`，其中的 `{url}` 和 `{text}` 会被填入。

```csharp
card.ShareMessage = "I use our invoicing app. Join with my link and we both get $20.";
card.PostShareUrlFormat = "https://social.example/share?url={url}&text={text}";
card.ShareRequested += (_, e) =>
{
    Launcher.LaunchUriAsync(new Uri(e.Url));
    e.Handled = true;
};

// The same URLs elsewhere:
var mail = ReferralCard.BuildShareUrl(ReferralShareChannel.Email, link, message);
```

### 按邮箱邀请

输入一个地址后按 Enter、逗号、空格或 Tab，它就会变成一个标签；粘贴一串地址会把其中每一个都加进来。无效的地址会变成带警示图标的标签，在修正或移除之前发送会被拒绝。重复的地址、已在列表中的地址以及超过 `MaxInvites`（10）的部分会被拒绝并给出说明。邀请发送期间，字段变为只读，发送按钮显示加载动画；失败时标签保留，并显示原因。

### 窄卡片

<Demo name="Narrow" />

卡片宽度自适应，最宽 560px。卡片宽度小于 470px 时，分享按钮单独占满一行，里程碑那一行预留两行的高度。过长的邮箱地址以省略号截断，日期和阶段始终可见。

### 你自己的文字

`Labels` 包含卡片显示或朗读的每一个词；数量、金额和日期是函数。`Title` 和 `Description` 会直接替换标题及其下方的文字。

```csharp
card.Labels = new ReferralCardLabels
{
    Title = reward => $"Offrez {reward}, recevez {reward}",
    Send = "Envoyer",
    StageEarned = reward => $"{reward} gagnés",
};
```

### 内容准则

- `Reward`、`Currency` 和文案要与真实的计划条款一致；标题由奖励金额生成。
- 用几个词说明里程碑奖励（"3 months free"），细则放在 `Detail` 里。
- 预览中的人物、金额和里程碑都是虚构的；上线前请传入你自己的计划。

### 常见问题

**卡片会自己发邮件吗？** 不会。由 `InviteHandler` 发送；没有它时发送是模拟的，提示也会这样说明。

**为什么列表高度不变？** 新邀请在固定高度的列表顶部展开，所以卡片不会变高，下方的内容也不会移动。

## API 参考

<!-- api: ReferralCard -->

<!-- api: Referral -->

<!-- api: ReferralMilestone -->

<!-- api: ReferralCardSimulation -->

<!-- api: ReferralCardSimulationBeat -->

<!-- api: ReferralCardLabels -->

<!-- api: ReferralShareEventArgs -->

<!-- api: ReferralInvitesEventArgs -->

<!-- api: ReferralLinkEventArgs -->

<!-- api: ReferralMilestoneEventArgs -->

## 无障碍

- 分享按钮是带名称和工具提示的图标按钮（Share in a post、Share with your network、Share by email）；一个礼貌的实时区域会报告发生了什么，包括 "Simulated, nothing was posted."。
- 邮箱字段下方的提示描述该字段，并以礼貌方式朗读。出错时显示警示图标加文字，字段边框变为危险色，从不只靠颜色；无效的标签会在名称中说明。
- 阶段是图标加文字。里程碑进度条以进度条的身份朗读，数值作为帮助文本，例如 "4 of 5 paid referrals"。
- 每个漏斗数字都带有它的标签作为名称；人员列表的名称是 "Your invites"。
- 发送期间字段变为只读而不是禁用，所以键盘焦点保持不动。

### 键盘

| 按键 | 操作 |
|---|---|
| Tab | 依次经过复制按钮、三个分享按钮、邮箱字段和发送按钮 |
| Enter、逗号、空格或 Tab | 在邮箱字段中把输入的地址变成标签 |
| Enter | 在空的邮箱字段中发送标签 |
| Backspace | 在空的邮箱字段中删除最后一个标签；每个标签也有自己的删除按钮 |
| Ctrl/Cmd + V | 粘贴以逗号、空格或换行分隔的地址，会全部加入 |

## 动效

| 变化 | 完整 | 减弱 | 关闭 |
|---|---|---|---|
| 漏斗数字 | 以标准曲线在 600ms 内滚动到新值，使用等宽数字 | 立即变化 | 立即变化 |
| 阶段文字、里程碑那一行、分享图标和提示 | 旧内容 100ms 淡出，新内容 160ms 淡入并上升 2px | 短暂交叉淡化，不上升 | 立即变化 |
| 里程碑分段 | 240ms 内从左侧填充；解锁时填充变为成功色 | 相同 | 立即变化 |
| 新的邀请 | 在固定高度的列表顶部以无回弹的 0.24s 高度弹簧展开，同时在 160ms 内淡入 | 立即出现 | 立即出现 |
| 模拟计划 | 进入屏幕 40% 时播放一次 | 以两倍速度播放 | 以两倍速度播放 |

各个等级的最终状态都相同；减弱动效只去掉滚动、上升和高度变化。

## 相关组件

- `CopyButton`：复制单个值并给出确认。
- `TagInput`：不涉及邮箱和发送的自由标签。
- `Avatar`：列表中的人员。
- `AgentStatus`：另一个紧凑的实时状态卡片。
