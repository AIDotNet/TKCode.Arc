---
title: CTA section
summary: 以居中收尾区块、与设置卡片并列的分栏，或可关闭的横幅呈现的行动号召。
source-hash: f8f3f6f2d2d5
---

<Demo name="Preview" />

## 何时使用

行动号召只向读者提出一个请求：开始试用、预约演示、注册。`CtaSection` 为这个请求提供三种形态，因此同一个控件既可以收尾一个落地页，也可以在页面中间与产品视觉并列，或横跨应用的顶部。若不做任何设置，它会为你所选的形态显示 Arc 的默认文案。

- **落地页的最后一个区块**：使用 `Centered` 形态，一张安静的卡片，包含标题、一句话、两个操作，以及一行小字说明，并附上已经加入的人的头像。
- **与产品视觉并列的页内提示**：`Split` 形态把文案和简短的优势条目放在左侧，把设置卡片放在右侧。卡片进入视野后会逐步完成各个步骤，让读者看到按钮将带来的结果。
- **位于内容上方的窄条促销横幅**：`Banner` 形态是一行文字、一个操作和一个可选的关闭按钮。关闭后它所占的空间会收起，下方的页面会平滑上移。

## 何时不用

- **页面的第一屏**：请使用首屏（hero）。行动号召用于收尾或打断一段内容，而不是用来开场。
- **收集邮箱地址**：请使用新闻订阅（newsletter signup）区块，它会在原处完成校验、发送和确认。
- **长期显示的全站通知**：请使用公告栏。这里的横幅只有一条消息和一个操作，由读者关闭，而不是轮换或计数的通知。
- **有后果的决定**：请使用对话框或警告对话框。行动号召是邀请，不是打断。

## 用法

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:CtaSection />
</UserControl>
```

`CtaSection` 派生自 `TemplatedControl`。`Variant` 选择形态；每段文字、每个操作、每个列表和每张图片都是一个属性。除图片外，每个属性都有 Arc 的英文默认值，图片则在你添加之前保持为空。该区块只负责布局，因此操作规则、数据和跳转目标都留在你的应用中。

### 形态

| `Variant` | 显示内容 | 操作 |
|---|---|---|
| `Centered` | 标题、文字、操作，以及在安静卡片上最多四个头像的说明 | 主要、次要 |
| `Split` | 文案、优势条目和操作位于舞台旁；舞台承载设置卡片或你自己的 `Visual` | 主要、次要 |
| `Banner` | 一行文字、一个操作和一个可选的关闭按钮 | 仅主要 |

设置 `Title`、`Description`、`PrimaryAction` 和 `SecondaryAction` 即可替换任一形态的文字。若某个值未设置，形态会显示它自己的 Arc 文案，因此把 `Variant` 从 `Centered` 切换为 `Split` 时，文字也会随之变化。

### 操作

每个操作都是一个 `CtaAction`，具有 `Label`、可选的 `Href`、`ConfirmedLabel`、`Command` 和 `CommandParameter`。按下时发生什么，取决于你为它设置了什么。

| 操作具有 | 按下时 |
|---|---|
| `Href` | 触发 `ActionInvoked`，若有命令则执行该命令，然后打开该地址，除非某个处理程序已将事件标记为已处理。只打开绝对的 http、https 和 mailto 地址 |
| `Command`，或处理程序将 `ActionInvoked` 标记为已处理 | 只执行，屏幕上没有其他变化 |
| 以上都没有 | 原地确认：一个对勾和 `ConfirmedLabel`（若无则为 `Label`）替换文字，再按一次则恢复原文，变化会以礼貌的方式播报 |

原地确认让预览能够响应每一次按下。在应用中，请为每个操作设置命令、跳转目标或处理程序。

```xml
<arc:CtaSection Variant="Split">
    <arc:CtaSection.PrimaryAction>
        <arc:CtaAction Label="Create a workspace" Command="{Binding CreateWorkspace}" />
    </arc:CtaSection.PrimaryAction>
    <arc:CtaSection.SecondaryAction>
        <arc:CtaAction Label="Book a demo" Href="https://example.com/demo" />
    </arc:CtaSection.SecondaryAction>
</arc:CtaSection>
```

```csharp
section.ActionInvoked += (_, e) =>
{
    if (e.IsPrimary)
    {
        analytics.Track("cta_primary", e.Action.Label);
    }
};
```

`CtaActionEventArgs` 包含被按下的 `Action`、`IsPrimary`，以及 `IsConfirmed`：当本次按下使普通操作进入确认状态时为 true。将事件标记为 `Handled`，即可完全接管一个普通操作。

<Demo name="Links" />

主要操作的末尾是一个箭头，在鼠标或触控笔下会向前倾斜 3 px。触摸不会触发倾斜。在居中和分栏形态中，操作是大按钮；在横幅中，操作是小按钮。

### 居中

`Centered` 形态会把它的说明放在操作下方。`Note` 默认为 "No credit card required"；将它设为空字符串即可隐藏该文字。向 `Faces` 中添加图片后，最多四个重叠的头像会显示在说明旁边；当它既没有头像也没有说明时，该行会隐藏。

```csharp
section.Note = "Join 4,000 teams";
section.Faces.Add(new Bitmap("avatar-1.png"));
section.Faces.Add(new Bitmap("avatar-2.png"));
```

这些头像只是装饰，屏幕阅读器不会读出，因此请用 `Title` 和 `Description` 说明该区块提供什么。

### 分栏与设置卡片

`Split` 形态在 `Points` 中列出优势条目（null 时显示 Arc 的三条；空列表则隐藏列表），并在右侧放置一个舞台。舞台默认承载一张会自行完成的设置卡片：

1. 舞台在其 35% 可见时向上浮入，仅一次。
2. 卡片一半可见时，三个计时器启动。第 *i* 个步骤在 520 + 620 × *i* ms 后完成（大约在 0.5、1.1 和 1.8 秒时）。
3. 每个步骤完成时，其标记会填充一个弹跳进入的对勾，进度线逐渐增长，下一个步骤变为淡色填充。顶部的状态从 "Setting up" 变为 "Ready"。
4. 最后一行显示 `SetupFaces`，当该步骤成为当前步骤时，这些头像会依次滑入。

使用 `SetupWorkspace`（其首字母即为标记）、`SetupSteps`（null 时显示 Arc 的三个步骤）和 `SetupFaces` 来配置该卡片。这张卡片纯粹是结果的示意。它对屏幕阅读器隐藏，旁边的文字承载信息。

若要显示你自己的截图或控件，请设置 `Visual`。舞台保留其尺寸、边框和入场效果；设置卡片及其计时器则不再使用。

<Demo name="CustomVisual" />

```xml
<arc:CtaSection Variant="Split">
    <arc:CtaSection.Visual>
        <Border Width="320" Height="200" CornerRadius="26" Background="{arc:ArcDesignTokenResource SurfaceRaised}" />
    </arc:CtaSection.Visual>
</arc:CtaSection>
```

### 横幅

`Banner` 形态是一行圆角条。设置 `IsDismissible` 即可显示关闭按钮。按下它，或调用 `Dismiss()`，都会关闭横幅：它在 160 ms 内淡出，同时高度弹簧式收缩到零，然后 `IsOpen` 变为 false 并触发 `Dismissed`。`Show()` 会立即让它重新出现。

```xml
<arc:CtaSection Variant="Banner"
                IsDismissible="True"
                IsOpen="{Binding ShowPromo, Mode=TwoWay}" />
```

`IsOpen` 是双向的。从你自己的代码将它设为 false 会播放同样的退场效果，但不会触发 `Dismissed`。如果横幅应在下次启动后保持关闭，请自行记住这一选择（例如通过设置或等同 cookie 的存储）。设置 `DismissLabel` 可以为关闭按钮换成其他语言的名称。

### 响应式布局

该区块跟随它自身的宽度，而不是窗口的宽度。在 860 px 及以下时，分栏变为单列，舞台位于文案下方。在 560 px 及以下时，内边距收紧，卡片圆角变为 28 px，操作按钮以全宽堆叠，横幅把文字放在操作上方，关闭按钮位于右边缘。标题随区块增大，从 30 px 增至 52 px。

<Demo name="Narrow" />

### 内容指南

- **标题**：用几个词给出承诺，例如 "Start building in minutes"。居中的标题大约每 18 个字符换行，并使各行长度平衡。
- **操作文字**：一个动词加至多两个其他词（"Create a workspace"、"Book a demo"）。主要操作是你希望读者接下来做的那一件事。
- **确认文字**：说明发生了什么（"Workspace created"），而不是 "Done"。
- **优势条目**：三条或更少，每条都要足够短，能在左侧一行内放下。

### 最佳实践

**建议**

- 为主要操作提供真实的跳转目标或命令，并且每个区块只保留一个主要操作。
- 在页面末尾使用 `Centered`，在页面中间与相关状态并列时使用 `Split`，在应用或文档页面顶部使用 `Banner`。
- 对于非必要的横幅，保持开启 `IsDismissible`，并尊重已关闭横幅的读者的选择。

**避免**

- 两个主要操作。次要操作有意保持安静。
- 过长的标题。标题受宽度限制并会做平衡处理，因此一个长句会变成很高的区块。
- 在生产环境中依赖原地确认。它只为预览中的按下提供响应。

## API 参考

<!-- api: CtaSection -->

<!-- api: CtaAction -->

<!-- api: CtaActionEventArgs -->

## 无障碍

- 该区块是一个区域，名称来自其标题。
- 每个操作和关闭按钮都有可访问的名称。确认会通过隐藏的实时区域礼貌地播报，再次按下已确认的操作时会播报其普通标签。
- 头像和设置卡片属于装饰，对屏幕阅读器隐藏。
- 横幅关闭后会收起其空间，因此关闭后键盘焦点顺序保持清晰。

### 键盘

| 按键 | 操作 |
|---|---|
| `Tab` / `Shift+Tab` | 在操作之间移动，横幅中则再移到关闭按钮 |
| `Enter` / `Space` | 执行操作或关闭横幅 |

键盘焦点会在获得焦点的操作或关闭按钮周围显示 2 px 的焦点环；指针焦点不显示。

## 动效

- 主要操作在悬停时将其箭头向前倾斜 3 px（160 ms），标签变化时进行交叉淡化。
- 分栏的舞台会一次性向上浮入（不透明度变化，并移动 24 px，480 ms）。在设置卡片中，状态文字以短暂的模糊切换，进度线在 480 ms 内增长，步骤标记在 240 ms 内填充，对勾在 580 ms 内弹跳进入，头像则以 60 ms 的间隔依次滑入。
- 关闭横幅时，它在 160 ms 内淡出，同时高度弹簧式收拢。没有缩放效果。

| 变化 | 完整 | 减少 | 关闭 |
|---|---|---|---|
| 舞台出现 | 一次性向上浮入，480 ms | 立即显示 | 立即显示 |
| 设置卡片运行 | 步骤依次勾选，进度线增长，头像滑入 | 每个步骤立即完成 | 时间线照常运行；每次变化直接跳转 |
| 横幅被关闭 | 160 ms 内淡出，高度弹簧式收至零 | 120 ms 内淡出，然后移除 | 立即移除 |
| 箭头倾斜 | 160 ms | 直接跳转 | 直接跳转 |

## 相关组件

- `Button`，用于单个操作；`SegmentedControl`，用于预览中的布局切换。
- 当操作需要收集邮箱地址时，使用新闻订阅。

| 组件 | 适用于 |
|---|---|
| `CtaSection` | 你需要在页面末尾、与视觉并列或在横幅中请求试用、演示或注册 |
| 新闻订阅 | 操作是一个邮箱地址 |
| `Button` | 你只需要一个操作，不需要包裹它的区块 |
