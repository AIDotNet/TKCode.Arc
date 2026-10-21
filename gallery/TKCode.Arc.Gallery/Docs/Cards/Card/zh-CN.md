---
title: 卡片
summary: 承载一组相关内容与操作的封闭式容器。
source-hash: 0d186e88d311
---

<Demo name="QuickLook" />

## 何时使用

- 可浏览的网格项，例如带有媒体、所有者和状态的项目、房源或帖子。
- 应作为一个整体呈现的相关内容与底部操作。
- 需要在不离开页面的情况下展开为更大快速预览的卡片（设置 `Details`）。

## 何时不用

- 需要上下文说明的单个数字：用 `MetricCard`。
- 原地展开、无需对话框的卡片：用 `ExpandableCard`。
- 没有可展开卡片、需要模态框的内容：用 `Dialog`。

## 用法

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:Card Title="港口改版"
              Description="全新的预订流程与房间页面。"
              Width="280" />
</UserControl>
```

### 媒体、所有者与底部操作

`Media` 显示在顶部，悬停时会轻微漂入。`Avatar` 与 `Meta` 组成署名行，`Action` 是位于末尾的底部操作控件，可单独点击。

```xml
<arc:Card Title="港口改版"
          Description="全新的预订流程与房间页面。"
          Meta="Maya Chen"
          Status="2 小时前更新"
          Width="280">
    <arc:Card.Media>
        <Border Height="132" Background="#5B8DEF" />
    </arc:Card.Media>
    <arc:Card.Avatar>
        <arc:Avatar DisplayName="Maya Chen" Size="Small" />
    </arc:Card.Avatar>
    <arc:Card.Action>
        <arc:Button Content="分享" Variant="Ghost" Size="Small" />
    </arc:Card.Action>
</arc:Card>
```

### 状态

`Status` 是一行简短文字，例如“2 小时前更新”。文字变化时，新词会依次升起出现。

<Demo name="StatusUpdates" />

```xml
<arc:Card Title="每周摘要"
          Description="每周五汇总各团队的重点内容。"
          Status="{Binding LastUpdate}"
          Width="300" />
```

### 快速预览

设置 `Details` 后，标题会变为按钮；整张卡片随即在同一页面中展开为更大的视图。

<Demo name="Controlled" />

```xml
<arc:Card Title="季度计划"
          Description="未来三个月的目标、负责人与日期。"
          IsDetailsOpen="{Binding IsPlanOpen, Mode=TwoWay}"
          Width="280">
    <arc:Card.Details>
        <TextBlock Text="由代码打开：IsDetailsOpen 为双向绑定。" TextWrapping="Wrap" />
    </arc:Card.Details>
</arc:Card>
```

使用 `OpenDetails()` 与 `CloseDetails()` 从代码中打开和关闭快速预览，使用 `DetailsOpenChanged` 获知关闭的原因。

```csharp
card.DetailsOpenChanged += (_, e) => log.Text = e.IsOpen ? "打开" : $"已关闭（{e.Reason}）";
card.OpenDetails();
card.CloseDetails();
```

### 无快速预览

不设置 `Details` 时，标题只是普通标题，卡片仅在悬停时抬起。

<Demo name="WithoutQuickLook" />

## API 参考

<!-- api: Card -->

## 无障碍

- 标题为三级标题。设置 `Details` 后，标题变为按钮；按 Enter 或空格键即可打开快速预览。
- 按 Esc 关闭快速预览，并将焦点返回标题。快速预览打开时，Tab 键焦点保持在其内部。
- 底部操作保留各自的焦点。仅含图标的操作请用 `AutomationProperties.Name` 设置名称，`Media` 中的图片请设置替代文字。
- `Status` 的变化会以礼貌方式朗读。`CloseButtonName` 用于设置快速预览关闭按钮的名称。

## 动效

- 悬停时卡片抬起 2px，边框与阴影加深。媒体缓慢放大至 1.04（仅限鼠标）。
- 设置 `Details` 后，表面、媒体、标题与署名行通过同一弹簧动画飞入快速预览，同时页面在模糊中变暗。关闭时它们飞回卡片。
- 新的 `Status` 会把旧行推走，同时新词升起出现。
- 减弱动效时取消抬起、放大与变形，快速预览仅淡入淡出。关闭动效时变化立即生效。

## 相关组件

- `MetricCard`：单个数字及其上下文与变化。
- `ExpandableCard`：原地展开、无需对话框的内容。
- `Dialog`：没有卡片可展开的模态内容。
- `EmptyState`：尚无卡片的网格或列表。
