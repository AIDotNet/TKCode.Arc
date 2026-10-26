---
title: 移动端轻提示
summary: 在靠近底部处滑入的短暂消息，会自行消失，也可以滑开关闭。
source-hash: 34bb56491f76
---

<Demo name="Basic" />

## 何时使用

`Toast` 告诉用户发生了什么，而不要求任何回应：邮件已归档、链接已复制、提醒已设置。

- **已完成操作的确认**，不需要回答。
- **轻量的错误或状态**，用户可以忽略。

即时示例在手机外框里运行，那是桌面上的模拟；真机上的触摸反馈和滑动这里没有验证。

## 何时不用

- **需要做决定**：用 `Dialog`。
- **绝不能错过的消息**，或用户必须据此行动的消息：Toast 几秒后会自行消失。
- **长文本**：Toast 只有一两行。

## 用法

在页面的 `Overlay` 槽里放一个 `Toast`，有话要说时调用 `Show`。

```xml
<arcm:Page xmlns="https://github.com/avaloniaui"
           xmlns:arcm="https://github.com/AIDotNet/TKCode.Arc.Mobile">
    <arcm:Page.Overlay>
        <arcm:Toast Name="Toasts" Policy="Stack" MaxVisible="3" />
    </arcm:Page.Overlay>
</arcm:Page>
```

```csharp
Toasts.Show("Message archived");
Toasts.Show("Reminder set", TimeSpan.FromSeconds(5), ArcIcons.Bell);
Toasts.Show("Offline", TimeSpan.Zero); // stays until swiped away or DismissAll()
```

除非给 `Show` 传入时长，Toast 会停留 `DefaultDuration`（3 秒）。手指按在 Toast 上会暂停它的倒计时，松开后从停下的地方继续；向任一侧滑过 72 px，或快速一挥，就会把它送走。

`Policy` 决定在已有 Toast 显示时新来一条会怎样。`Stack` 保留最新的 `MaxVisible` 条，最新的在最下面，最旧的被送走。`Replace` 一次只显示一条：新的取代正在显示的。

Toast 位于页面页脚和底部安全区之上 `BottomOffset`（16）处：它会自己找到所在的 `Page`，加上页脚的高度和 `SafeArea.Bottom`，所以不会压住 `Tabbar`。浮层的其余部分不拦截触摸，所以 Toast 显示时页面仍可操作。

### 同一页面上的多个浮层

`Page.Overlay` 只能放一个元素。要让 Toast 与底部面板或对话框同时存在，把它们放进该槽里的一个 `Grid`，Toast 放在最后，让它显示在其他浮层之上。

```xml
<arcm:Page xmlns="https://github.com/avaloniaui"
           xmlns:arcm="https://github.com/AIDotNet/TKCode.Arc.Mobile">
    <arcm:Page.Overlay>
        <Grid>
            <arcm:Sheet Title="Share" />
            <arcm:Dialog Title="Delete?" />
            <arcm:Toast Name="Toasts" />
        </Grid>
    </arcm:Page.Overlay>
</arcm:Page>
```

### Token

`ToastToken` 包含 `BodyMinHeight`（44）、`BodyMaxWidth`（360）、`BodyPadding`、`StackGap`（8）、`SideInset`（16）和 `GlyphSize`（18）。Toast 是一颗反色胶囊：以 Foreground 色为填充，以 Background 色为文字。

## API 参考

<!-- api: MobileToast -->

## 无障碍

消息以礼貌的方式朗读，不抢焦点。因为 Toast 会自行消失，不要把重要信息只放在其中，文字要短。滑动只是捷径：Toast 本来也会自己离开。Toast 至少 44 高，手指可以按住它。

## 动效

Toast 上升 16 px 并在一根灵敏的弹簧上淡入，离开时更快，用 0.16 秒且没有过冲，所以下一条 Toast 不必等上一条。出现状态和滑动偏移是代码驱动的运动值：滑动时每次移动都会写入偏移，松手时手指的速度交给弹簧，把没滑完的 Toast 带回原位。倒计时是可暂停的时间线，所以即使没有绘制帧，也能准时结束。减弱动效时，Toast 只淡入淡出，用时 0.12 秒，滑动在松手时才判定，不跟随手指；关闭动效时，它立即出现和消失。

## 相关组件

- `MobilePage`：`Overlay` 槽承载 Toast，页脚和安全区决定 Toast 的位置。
- `MobileDialog`：用于需要做决定时。
- `MobileSheet`：用于选项和表单。
- `MobileTabbar`：Toast 在它之上升起。
