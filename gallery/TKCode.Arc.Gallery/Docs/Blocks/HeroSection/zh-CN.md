---
title: 首屏区块
summary: 三个全屏的 SaaS 首屏：从底边升起、覆盖在飘动渐变之上的实时仪表盘，逐节点传递示例事件的工作流图，以及网格渐变上的大字排版。
source-hash: 99d69ed6ec96
---

<Demo name="Preview" />

## 何时使用

首屏是落地页的第一屏，只有片刻时间说明产品是做什么的。`HeroSection` 为这一刻提供三种完整设计，以及一种用来放你自己文字的简单布局。

- **产品界面本身就是卖点**：`Centered` 把标题放在缓慢流动的柔和渐变上，下方是一个从底边升起的仪表盘。里面的时间范围切换确实可用。
- **展示产品运转就是卖点**：`Split` 把文案放在一个自动运行示例事件的工作流旁边，适合自动化、开发者工具和基础设施产品。
- **一句有力的话**：`Minimal` 是满屏网格渐变上的大字，适合发布页或下载页。
- **以上都不合适**：设置 `Title` 后，首屏会在所选布局中显示你自己的公告、标题、操作、安装命令、要点和配图。

## 何时不用

- **应用内部的页面标题**：请使用 `PageHeader`。首屏用于产品的对外页面，而不是产品内的界面。
- **目标是收集邮箱地址**：请使用 `NewsletterSignup`，它会就地校验并确认。
- **页面的结尾**：请使用 `CtaSection`。首屏开启一段内容，行动号召负责收尾。

## 用法

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:HeroSection Variant="Split" />
</UserControl>
```

`HeroSection` 派生自 `TemplatedControl`。`Variant` 选择设计。每种设计都会填满它得到的高度；放在滚动容器里时则占满一个视口，因此在页面中占满一屏，在预览中贴合外框。示例文案和数据属于虚构产品，上线前请替换，或设置 `Title`。

### 设计

| `Variant` | 显示内容 | 适合 |
|---|---|---|
| `Centered` | 飘动渐变上的标题，以及从倾斜状态落定、在底边淡出的仪表盘 | 界面有说服力的产品 |
| `Split` | 文案旁边是点阵画布上的工作流图 | 自动化、开发者工具 |
| `Minimal` | 带颗粒的网格渐变上的大字，底边排列客户标志 | 发布、下载 |

<Demo name="Editorial" />

### 操作

`PrimaryAction` 和 `SecondaryAction` 接受 `HeroAction`，它在 `CtaAction` 的基础上增加了 `IsExternal`。未设置时显示该设计自带的操作。按下时先引发 `ActionInvoked` 并执行 `Command`，然后：

| 操作具有 | 按下时还会 |
|---|---|
| `Href` | 打开该地址，除非处理程序已将事件标记为已处理。只会打开绝对的 http、https 和 mailto 地址 |
| `ConfirmedLabel` 且没有 `Href` | 显示勾选标记和该标签 2.4 秒 |
| 两者都没有 | 不再做其他事 |

主操作末尾有一个箭头，在鼠标或触控笔悬停时前倾 2 px。`IsExternal` 会改为显示向外的箭头。

```csharp
hero.PrimaryAction = new HeroAction { Label = "Start free trial", Command = viewModel.StartTrial };
hero.SecondaryAction = new HeroAction { Label = "Pricing", Href = "https://example.com/pricing", IsExternal = true };
hero.ActionInvoked += (_, e) => analytics.Track("hero", e.Action.Label);
```

### 工作流

`Split` 在区块内部绘制工作流图，不依赖其他包。每个事件都走相同的步骤：触发、查询、分支，然后是三个动作。每完成一步，数据就沿连线绘制过去，每一步都会报告耗时。金额较小的付款走“否”分支，因此有两个动作被跳过，它们的连线变为虚线。**Send test event** 会立即运行下一个示例。离开屏幕时运行暂停，回来后从停下的地方继续。

<Demo name="Workflow" />

### 你自己的内容

设置 `Title` 后，首屏按自然高度显示你的内容。此时 `Variant` 表示布局：`Centered` 让文案居中，`Minimal` 让文案靠左，`Split` 还会在旁边显示 `Media`。其他属性都是可选的：`Description`、`Announcement`、`InstallCommand`（带复制按钮）和 `Meta`（小字要点）。操作只有在你设置后才会显示。

<Demo name="OwnContent" />

```xml
<arc:HeroSection Variant="Split" Title="Ship your docs today" InstallCommand="npm create quickdocs">
    <arc:HeroSection.Media>
        <Border Height="320" Background="{arc:ArcDesignTokenResource SurfaceMuted}" />
    </arc:HeroSection.Media>
</arc:HeroSection>
```

### 最佳实践

**建议**

- 用几个词在标题里说清产品做什么。标题会平衡各行，并在 12 到 16 个字符处换行。
- 给主操作设置真实的命令或地址。
- 把示例产品、数字和客户换成你自己的。

**避免**

- 过长的描述。两句话就够了。
- 一个页面放两个首屏。每种设计都占满一屏，并各自运行动态背景。

## API 参考

<!-- api: HeroSection -->

<!-- api: HeroAction -->

## 无障碍

- 标题是一级标题；设置 `Title` 时，区块以它命名。
- 每个操作、仪表盘内的切换器和复制按钮都有无障碍名称。
- 网格渐变、点阵、光晕和客户标志都是装饰。仪表盘和工作流图被命名为示例内容，每个节点的状态都有名称：Waiting、Running、Done 或 Skipped。

### 键盘

| 按键 | 操作 |
|---|---|
| `Tab` / `Shift+Tab` | 在各操作之间移动，然后进入设计内部的控件 |
| `Enter` / `Space` | 执行操作或发送测试事件 |

## 动效

- 入场动画只播放一次：各项从 2 px 模糊中上升 12 px，用时 640 ms，间隔 80 ms。将 `IsEntranceAnimated` 设为 false 可跳过，调用 `ReplayEntrance` 可重新播放。
- 仪表盘在 1.1 秒内上升 56 px，同时在 1.5 秒内从 22 度倾斜和 94% 缩放落定。
- 网格渐变在屏幕内时以每秒 30 帧、20 秒一轮的节奏飘动。
- 工作流节点的状态以 snappy 弹簧弹出，填充在 240 ms 内变化，每条连线按其步骤的时长绘制。

| 变化 | Full | Reduced | Off |
|---|---|---|---|
| 入场 | 上升、模糊并倾斜 | 只淡入 | 立即显示 |
| 网格渐变 | 飘动 | 一帧静止画面 | 一帧静止画面 |
| 工作流 | 逐个事件运行 | 每个事件直接显示完成状态 | 每个事件直接显示完成状态 |

## 响应式

- `HeroSection` 跟随自身宽度，而不是窗口宽度。标题随宽度缩放。
- `Centered`：可用宽度不足 720 px 时，仪表盘切换到 600 px 设计，并在两侧均匀裁剪，而不是缩小到 60% 以下。宽度不超过 560 px 时，侧边留白消失，操作占满一行。
- `Split`：宽度不超过 900 px 时，工作流图移到文案下方。低于 520 px 时使用紧凑布局并隐藏运行摘要。
- `Minimal`：宽度不超过 800 px 时，描述和操作上下堆叠，客户标志折为四列，然后是三列。
- 触控：箭头只在鼠标或触控笔下前倾，因此触控操作从不依赖悬停。

<Demo name="Narrow" />

## 相关组件

| 组件 | 何时选择 |
|---|---|
| `HeroSection` | 落地页的第一屏 |
| `CtaSection` | 在页面末尾请求试用或演示 |
| `NewsletterSignup` | 操作是填写邮箱地址 |
| `StatsBand` | 在首屏下方用数字支撑你的说法 |
