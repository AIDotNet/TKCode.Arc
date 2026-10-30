---
title: Social proof toast
summary: 用于落地页和定价页的社交证明通知堆叠：来自个人和公司的最新试用、升级与已验证购买每隔几秒出现一条；悬停时暂停并展开，访客可以关闭任意一条通知。
source-hash: ff6c35ef997f
---

<Demo name="Preview" />

## 何时使用

`SocialProofToast` 展示其他人此刻正在做什么：开始了试用、团队完成了升级、一笔已验证的购买。每隔几秒，一条新通知从前方升起，较早的通知依次退到它后面。指针悬停在堆叠上，或键盘焦点进入其中，会暂停轮播并把通知展开，方便逐条阅读或关闭。

- **落地页和定价页**：展示最近的试用、购买或升级。
- **发布页或候补名单页**：实时显示有人加入、有团队升级。
- **结账按钮附近**：突出知名公司的已验证购买。

## 何时不用

- **访客自己操作之后的反馈**（已保存、已复制、失败）：使用 `Toast`。
- **已登录的产品内部**，且这些动态不是营销内容：使用动态列表。
- **没有真实事件可展示时。** 捏造的社交证明会损害信任；模拟只用于预览。

## 用法

添加堆叠，并在 `Events` 中按从旧到新的顺序传入你的动态。在 `InitialDelay`（0.8 秒）之后，它每隔 `Interval`（默认 4 秒）显示一条通知，最多保留 `MaxVisible` 条（3 条），到末尾后从头开始，除非 `Loop` 为 false。没有事件也没有模拟（见下文）时，堆叠保持为空，直到你推送一条。

```csharp
// 计费后端近期动态中的一行。
record Signup(string Id, string FirstName, string Action, string City, DateTimeOffset CreatedAt, bool IsPaid);

stack.Events.AddRange(recent.Select(r => new SocialProofEvent(r.Id, r.FirstName, r.Action)
{
    Meta = r.City,
    Time = r.CreatedAt,
    VerifiedLabel = r.IsPaid ? "Verified purchase" : null,
}));
```

### 你自己的事件

<Demo name="YourEvents" />

每个 `SocialProofEvent` 都有稳定的 `Id`、一个 `Name`（名字或公司名，以中等字重显示）以及紧随其后的 `Action`。`Meta` 补充简短的上下文，例如城市或席位数。`Time` 显示为相对时间（Just now、4 min ago、2 h ago）；`TimeText` 原样显示。`IsVerified` 或 `VerifiedLabel` 会加上一个带文字的对勾徽章。

个人显示为带姓名首字母的 `Avatar`，或其 `Portrait` 头像照片。把 `Actor` 设为 `Company` 时显示为圆角方块，内含名称的首字母，或你在 `Logo` 中提供的标志（24 × 24 的描边图形，以文字颜色绘制，因此跟随主题）。

调用 `Push` 可以在事件发生的那一刻显示它，例如来自服务器推送的事件。它会立即出现在最前面，下一条通知的倒计时也会重新开始。

用于预览时，让 `Events` 保持为空，并给堆叠一个 `SocialProofToastSimulation`：它的事件按从旧到新排列，每条都是一个 `SocialProofToastSimulationEvent`，包含事件本身以及它发生在多久之前（`Ago`）。每一轮都会从那一刻往回计算时间，因此动态永远不会漂移成 "40 min ago"。你自己的 `Events` 会让它让位，把 `Simulation` 设为 null 会清空堆叠。这里预览中的所有名字、公司和地点都是虚构的。

```csharp
stack.Simulation = new SocialProofToastSimulation
{
    Events =
    [
        new SocialProofToastSimulationEvent { Event = new SocialProofEvent("s1", "Sofia", "started a Pro trial") { Meta = "Madrid" }, Ago = TimeSpan.FromMinutes(14) },
        new SocialProofToastSimulationEvent { Event = new SocialProofEvent("s2", "Mateo", "bought the Team plan") { Meta = "Lisbon" }, Ago = TimeSpan.FromMinutes(11) },
    ],
};
```

### 结构

1. **堆叠**：所有通知共用一个 64px 的位置。最前面的通知贴着边缘；其后的每一条后退 10px、缩小 5%，文字淡出。
2. **标识**：个人是 36px 的圆形头像，公司是圆角方块。
3. **句子**：名称与动作在同一行，过长时以省略号截断。
4. **第二行**：附加信息与时间，然后是已验证徽章（图标加文字）。
5. **关闭**：24px 的按钮，在悬停或键盘聚焦时出现，在触摸屏上始终可见。

### 角落位置

<Demo name="Corner" />

`Position` 把堆叠固定在窗口的某个角落：`BottomLeft`、`BottomRight`、`TopLeft` 或 `TopRight`。它宽 360px，距离边缘 24px；窗口窄于 480px 时，它横跨整个宽度并留出 16px 边距。顶部角落向下堆叠。`Inline`（默认）让它留在布局中并填满容器宽度；文档和预览用它，真实页面请用角落位置。

### 选择与限制

<Demo name="Selectable" />

`IsSelectable` 让每条通知成为一个按钮并引发 `Selected`，例如用来打开定价页或客户案例。`Limit` 在一次会话显示这么多条通知后停止，让页面保持安静。每当一条通知出现时触发 `Shown`（用于曝光统计），访客关闭一条通知时触发 `Dismissed`。

### 暂停

堆叠被悬停或持有键盘焦点时、滚出视野时以及被隐藏时，轮播都会自动暂停，倒计时保留剩余时间。模态框或 Cookie 横幅打开期间请设置 `IsPaused`。`ExpandOnHover="False"` 让堆叠在悬停时保持收起（仍会暂停）。`Prefill` 会立即显示这么多条通知且不播放入场动画，适合预览和截图。`Restart` 清空堆叠并从头开始播放动态。

### 自定义文案

`Labels` 包含堆叠显示或朗读的所有文字。替换整个对象即可本地化或改写：

```csharp
stack.Labels = new SocialProofToastLabels
{
    Region = "Actividad reciente",
    JustNow = "Ahora mismo",
    MinutesAgo = minutes => $"hace {minutes} min",
    Dismiss = sentence => "Cerrar: " + sentence,
};
```

### 内容指南

- 动作要简短具体："started a Pro trial"、"upgraded to Business"。
- 个人用名字，团队用公司名称。不要显示姓氏或邮箱地址。
- `Meta` 只放一项信息：城市或席位数，不要两者都放。

### 最佳实践

- 使用来自计费或分析后端的真实、近期事件。
- 保持安静：间隔 4 到 8 秒，每次会话限制在 5 到 8 条左右。
- 模态框或 Cookie 横幅打开时暂停它。

### 常见问题

**新通知会打断屏幕阅读器吗？** 不会。新通知不会被播报；只有关闭操作会以礼貌方式确认。

**屏幕上最多有几条通知？** 最多 `MaxVisible` 条，另加一条正从后方淡出的通知。

## API 参考

<!-- api: SocialProofToast -->

<!-- api: SocialProofEvent -->

<!-- api: SocialProofToastLabels -->

<!-- api: SocialProofToastSimulation -->

<!-- api: SocialProofToastSimulationEvent -->

<!-- api: SocialProofToastEventArgs -->

## 无障碍

- 堆叠是一个带标签的区域（"Recent activity"）。新通知不会被播报，屏幕阅读器用户不会每隔几秒就被打断；关闭一条通知后会以礼貌的状态提示确认。
- 每个关闭按钮都会说明它关闭的是哪条通知，例如 "Dismiss: Sofia started a Pro trial"。
- 超出限制而正在淡出的通知对辅助技术隐藏，也不接受输入。悬停和键盘焦点都会暂停轮播，因此有人阅读或操作时不会有东西移动。
- 已验证状态是图标加文字标签，从不只靠颜色表达。头像不会被单独朗读，因为名称已经在句子里。

### 键盘

| 按键 | 操作 |
|---|---|
| Tab | 移到每条可见通知的关闭按钮（设置 `IsSelectable` 时也会移到通知本身）。焦点进入堆叠会暂停并展开它 |
| Enter、Space | 在关闭按钮上关闭该通知；设置 `IsSelectable` 时选择获得焦点的通知 |
| Escape | 关闭获得焦点的通知，或最前面的通知。焦点移到下一条通知 |

## 动效

| 变化 | 完整 | 减弱 | 关闭 |
|---|---|---|---|
| 新通知到达 | 在最前面上升 16px 并淡入；其他通知以无回弹弹簧（视觉时长 0.24s）各后退 10px、缩小 5% | 淡入；其他通知立即就位 | 立即 |
| 被遮住的通知 | 内容在 160ms 内淡出，文字从不重叠 | 同样淡出 | 立即 |
| 悬停或聚焦 | 堆叠以同一弹簧展开 | 立即 | 立即 |
| 关闭 | 在 160ms 内淡出并略微缩小；其余通知以弹簧补上空位 | 淡出；其余立即就位 | 立即 |
| 超出数量 | 最后面的通知在 160ms 内淡出 | 同样淡出 | 立即 |

通知始终是 64px 高，因此各状态之间没有尺寸变化；内联堆叠预留了展开后的高度，悬停时页面不会移动。动效减弱或关闭时轮播仍会继续：它是内容，而不是装饰。

## 相关组件

- `Toast`：对访客自身操作的简短确认。
- `Avatar`：动态中的人物。
