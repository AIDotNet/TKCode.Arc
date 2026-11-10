---
title: Changelog feed
summary: 可筛选、可就地展开、可按月滚动浏览的发布说明。
source-hash: c24458c39c41
---

<Demo name="Preview" />

## 何时使用

将 `ChangelogFeed` 用于按月分组的产品更新日志或发布说明页，支持按变更类型筛选，并在页眉提供邮件订阅。

## 何时不用

- **没有筛选的简短事件列表**：使用 `Timeline`。
- **没有日期的可展开内容**：使用 `Accordion`。
- **成千上万条说明**：条目没有虚拟化，请按需加载较早的月份。

## 用法

```csharp
feed.Months = [new ChangelogMonth { Key = "2026-03", Label = "March 2026", ShortLabel = "Mar" }];
feed.Entries =
[
    new ChangelogEntry
    {
        Id = "tags", Month = "2026-03", Date = "Mar 3", Version = "2.1.0", Kind = ChangelogKind.New,
        Title = "Tags for every note", Summary = "Group notes across folders.",
        Details = ["Type a hash sign to add a tag."],
        Media = new ChangelogCode { File = "tags.json", Code = "{ }" }
    }
];
feed.Subscribed += async (_, e) => await list.AddAsync(e.Email);
```

<Demo name="CustomData" />

条目按键归属某个月并列在其下，最新优先。没有匹配条目的月份会从月份栏和列表中移除。`DefaultOpen` 选择初始展开的条目；未设置时展开最新一条。`ToggleEntry`、`ToggleFilter`、`ClearFilters` 和 `JumpToMonth` 可在代码中完成相同操作。

### 订阅

胶囊先变成邮箱输入框，再变成带撤销的确认。有效地址引发 `Subscribed`；撤销引发 `SubscriptionUndone`。区块本身不发送任何内容，请在处理程序中把地址提交到邮件列表。

### 媒体

`Media` 是 `ChangelogPhoto`（裁剪为 16:10、带说明的图片）或 `ChangelogCode`（带复制按钮的代码片段）。面板在条目首次展开时才创建。

### 窄容器

<Demo name="Narrow" />

## API 参考

<!-- api: ChangelogFeed -->

<!-- api: ChangelogSubscribeEventArgs -->

## 无障碍

- 每一行是一个按钮，名称包含日期、类型、标题和摘要，帮助文本说明是否已展开。
- 筛选标签是切换按钮，实时区域会读出显示的更新数量。
- 月份按钮的名称取自 `JumpFormat`。列表可获得焦点以便用按键滚动。
- `Escape` 取消订阅输入框并回到胶囊；`Enter` 提交。

## 动效

- 行沿 smooth 弹簧展开，沿 `{ visualDuration: 0.28, bounce: 0 }` 收起，箭头沿 snappy 弹簧旋转。面板内容延迟 60 ms 后从 4 px 模糊中上升 8 px。
- 切换筛选时行淡出（缩放 .98、模糊 2、180 ms），其余行沿弹簧移到新位置。计数沿 snappy 弹簧滚动。
- 月份标签随滚动方向上升 14 px，月份胶囊沿 morph 弹簧滑动。月份按钮沿 smooth 弹簧滚动。
- 订阅胶囊变形宽度；内容以缩放 .96 和模糊 2 切换；无效地址抖动 480 ms；对勾在 420 ms 内绘制。
- 减弱动效时只用短暂淡入淡出，没有抖动和位移。关闭动效时立即完成。

## 响应式

- 区块跟随自身宽度，最宽 920 px。
- 640 px 及以下时页眉堆叠，日期和版本移到文字上方，列表高度从 560 px 降到 520 px。
- 480 px 及以下时隐藏月份计数、年份和显示计数，让月份导航保持一行。
- 悬停样式只对鼠标或触控笔生效。

## 相关组件

- `Timeline`：简短的纵向事件列表。
- `Accordion`：没有日期的可展开内容。
- `CopyButton`：代码片段的复制操作。
