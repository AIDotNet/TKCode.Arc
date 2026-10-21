---
title: 悬停卡片
summary: 悬停或聚焦时预览人员或链接，无需离开页面。
source-hash: 9342f8a0604b
---

<Demo name="Mention" />

## 何时使用

- 提及、头像或作者姓名，悬停后展开简短的个人资料。
- 链接预览，且点击仍需跳转到该链接。
- 密集列表中，从一个触发器移动到下一个时应立即打开下一张卡片。

## 何时不用

- 纯文本标签：用 `Tooltip`。
- 包含按钮或输入框的内容：用 `Popover`。焦点不会进入悬停卡片，因此其内容保持只读。
- 用户自己的账户菜单：用 `UserMenu`。

## 用法

`HoverCard` 在其 `Trigger` 上短暂悬停或获得键盘焦点后打开。其内容是预览，通常为 `HoverCardProfile`。

### 个人资料卡片

```xml
<arc:HoverCard Placement="Bottom">
    <arc:HoverCard.Trigger>
        <arc:Button Variant="Ghost" Content="@maya" />
    </arc:HoverCard.Trigger>
    <arc:HoverCardProfile DisplayName="Maya Chen"
                          Role="支付产品设计师"
                          Bio="负责结账与账单流程的设计。"
                          Meta="里斯本 · 当地时间 14:20">
        <arc:HoverCardProfile.Stats>
            <arc:HoverCardStat Label="项目" Value="12" />
            <arc:HoverCardStat Label="评审" Value="48" />
        </arc:HoverCardProfile.Stats>
    </arc:HoverCardProfile>
</arc:HoverCard>
```

### 链接预览

`arc:HoverCard.Attach` 可以为任意控件添加卡片，例如普通的文本链接。

<Demo name="LinkPreview" />

### 时间与宽度

`OpenDelay` 与 `CloseDelay` 默认分别为 500ms 与 140ms。`CardWidth` 设置卡片宽度，卡片始终受窗口可用空间的限制。

```csharp
card.OpenDelay = TimeSpan.FromMilliseconds(300);
card.CardWidth = 320;
```

### 从代码中显示

`Show` 与 `Hide` 直接修改 `IsOpen`。`IsOpen` 是双向的，`Opened` 与 `Closed` 会报告状态变化。

```csharp
card.Show();
card.Closed += (_, e) => log.Text = $"关闭方式：{e.Reason}";
```

### 密集列表

一张卡片打开后，移到下一个触发器时，下一张卡片几乎立即打开，且只有淡入效果。

<Demo name="DenseList" />

## API 参考

<!-- api: HoverCard -->

<!-- api: HoverCardProfile -->

<!-- api: HoverCardStat -->

## 无障碍

- 卡片打开时，其摘要会写入触发器的帮助文本中。
- 焦点从不进入卡片。请保持其内容只读，并将操作放在触发器上或 `Popover` 中。
- 通过 Tab 获得焦点会立即打开卡片，Enter 跳过打开延迟，按 Escape 关闭。指针停留在触发器上时保持关闭。
- 在触摸设备上，点按会切换卡片。同一时间只打开一张卡片。

## 动效

- 卡片从触发器边缘生长：以平滑弹簧从缩放 0.96 变为 1，并向触发器方向移动 4px，同时单独淡入。
- 个人资料各行从下方 4px 处依次就位。头像加载完成后从 4px 的模糊中淡入。
- 若另一张卡片在 300ms 之前刚关闭，则下一张卡片在 80ms 后仅以淡入方式打开。
- 减弱动效时移除缩放与移动，保留短暂淡入。关闭动效时卡片立即显示或移除。

## 相关组件

- `Tooltip` 用于悬停或聚焦时的简短文字标签。
- `Popover` 用于包含按钮或输入框、点击打开的卡片。
- `Avatar` 用于个人资料中显示的图片。
