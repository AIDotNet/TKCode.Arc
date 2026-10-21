---
title: 公告栏
summary: 顶部横幅，轮播消息、倒计时，关闭时平滑收起。
source-hash: 234440d08e86
---

<Demo name="RotatingMessages" />

## 何时使用

- 页面顶部的全站通知，例如促销、发布或维护窗口。
- 在同一条栏中轮播的两到四条简短公告。
- 带实时倒计时的促销活动。

## 何时不用

- 关于页面某个区域或表单的消息：用 `Alert`。
- 访客刚刚操作后的反馈：用 `Toast` 或 `ToastStack`。
- Cookie 或追踪的同意选择：用 `CookieConsent`。
- 用户必须先处理才能继续的一次性消息：用 `Dialog`。

## 用法

将公告添加到 `Messages`。公告栏先显示第一条，然后每隔 `Interval` 轮播其余消息，默认间隔为 6 秒。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:AnnouncementBar>
        <arc:AnnouncementBar.Messages>
            <arc:Announcement Id="launch"
                              Message="Arc 2.0 已发布，新增 40 个组件。" />
            <arc:Announcement Id="webinar"
                              Message="周四 17:00（UTC）进行在线演示。" />
            <arc:Announcement Id="status"
                              Message="所有系统运行正常。" />
        </arc:AnnouncementBar.Messages>
    </arc:AnnouncementBar>
</UserControl>
```

### 操作与倒计时

`Announcement` 可以带一个 `Action` 按钮，设置 `Label` 与 `Href`；也可以带一个 `Countdown`，倒数到 `To`。当操作需要执行代码时，在按钮上使用 `Command` 代替 `Href`。

```xml
<arc:AnnouncementBar>
    <arc:AnnouncementBar.Messages>
        <arc:Announcement Id="sale"
                          Message="春季促销：年付套餐享 7 折。">
            <arc:Announcement.Action>
                <arc:AnnouncementAction Label="升级" Href="https://example.com/upgrade" />
            </arc:Announcement.Action>
            <arc:Announcement.Countdown>
                <arc:AnnouncementCountdown To="2026-11-01T00:00:00+00:00" Label="距结束" />
            </arc:Announcement.Countdown>
        </arc:Announcement>
    </arc:AnnouncementBar.Messages>
</arc:AnnouncementBar>
```

```csharp
bar.ActionInvoked += (_, e) => Analytics.Track("announcement.action", e.Announcement.Id);
bar.CountdownEnded += (_, e) => bar.Messages.Remove(e.Announcement);
```

### 控制与自动轮播

设置 `IsNavigationVisible` 即可显示上一条、下一条按钮以及暂停环。`IsAutoPlayEnabled` 可关闭轮播；`Next()` 与 `Previous()` 则可在代码中切换消息。

<Demo name="ControlsAndCountdown" />

```xml
<arc:AnnouncementBar IsNavigationVisible="True"
                     Interval="0:0:8"
                     IsAutoPlayEnabled="True" />
```

### 语气

`Inverted` 将前景色用作填充，背景色用作文字。仅用于最重要的通知。

<Demo name="Inverted" />

### 记住关闭状态

设置 `DismissalKey` 可在多次运行之间记住关闭状态。该键保存在 `Storage` 中。调用 `AnnouncementBar.ClearDismissal` 清除已保存的值，即可再次显示公告栏。关闭状态由 `Storage` 保存，默认为应用数据文件夹中的一个文件。

<Demo name="RememberedDismissal" />

```xml
<arc:AnnouncementBar DismissalKey="spring-campaign" IsDismissible="True" />
```

```csharp
AnnouncementBar.ClearDismissal("spring-campaign");
bar.IsOpen = true;
```

## API 参考

<!-- api: AnnouncementBar -->

<!-- api: Announcement -->

## 无障碍

- 公告栏是一个区域，名称由 `Label` 指定，默认为 `Announcements`。
- 每条消息都有名称，例如 `1 of 3`，屏幕阅读器由此得知它在轮播中的位置。
- 轮播切换时不会逐条播报。公告栏暂停时才会播报，方便用户阅读。
- Tab 依次聚焦操作按钮、上一条按钮、暂停环、下一条按钮和关闭按钮。Enter 与空格键激活当前焦点控件。
- 暂停环是切换按钮，其名称在 `Pause announcements` 与 `Resume announcements` 之间变化。
- 指针位于公告栏上方、焦点位于其内部，或窗口最小化时，轮播会暂停。

## 动效

- 下一条消息从下方带着轻微模糊升起，同时公告栏以弹簧调整至适合的高度。
- 暂停环在每个间隔内逐渐填满。暂停时停在当前位置，恢复后继续。
- 倒计时数字变化时以下落方式出现。
- 关闭时公告栏收起，下方页面随之上移。
- 减弱动效时，轮播停止，消息与数字改为淡入淡出。关闭时仅淡出。
- 关闭动效时，每次变化立即显示。

## 相关组件

- `Alert`：位于某个区域或表单内、直到问题解决前一直保留的消息。
- `ToastStack`：用户刚执行的操作的排队结果。
- `CookieConsent`：Cookie 与追踪的同意。
