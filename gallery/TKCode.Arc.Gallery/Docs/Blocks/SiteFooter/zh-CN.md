---
title: Site footer
summary: 带链接列和订阅区的网站页脚，提供极简布局，以及一个大型渐隐的 Arc 标志。
source-hash: 60817fc4175b
---

<Demo name="Preview" />

## 何时使用

`SiteFooter` 用于收尾营销网站或文档网站。它在页面底部放置读者常找的链接（产品、资源、公司）、法律信息、系统状态和电子邮件订阅，共有三种布局。

- **带订阅的产品网站**：Columns 布局把品牌、标语和发布说明订阅区放在三列链接旁边。
- **文档网站或应用的帮助页面**：Minimal 布局是一行安静的链接，外加一行版权、标语、状态和社交链接。
- **以品牌收尾的落地页**：Logo 布局去掉订阅，以一个大型拱形结束。拱形渐隐于页面之中，滚动到视野内时升起。

## 何时不用

- **高密度的应用界面**：应用内的状态栏或侧边栏页脚更小巧；本区块是页面级页脚。
- **订阅本身就是页面的重点**：请使用 Newsletter signup 区块，它能让表单占据整个版块。
- **结束性的行动号召**：请在页脚上方使用上文的 CTA section 区块，让页脚只负责导航。

## 用法

未设置任何内容时，页脚显示 Arc 的示例：Arc 品牌、三列、法律和社交链接，以及一条 "All systems normal" 状态。

```xml
<arc:SiteFooter />
```

所有内容都来自数据：设置品牌名称，提供你自己的列和链接，并处理被按下的操作。

<Demo name="Custom" />

```xml
<arc:SiteFooter BrandName="Northwind"
                BrandHref="https://northwind.example"
                Tagline="Freight tracking for small fleets."
                IsNewsletterVisible="False"
                StatusLabel="Delays in EU routing"
                StatusTone="Warning">
    <arc:SiteFooterColumn Title="Platform">
        <arc:SiteFooterLink Label="Tracking" Href="https://northwind.example/tracking" />
        <arc:SiteFooterLink Label="API" Href="https://developers.northwind.example" IsExternal="True" />
    </arc:SiteFooterColumn>
</arc:SiteFooter>
```

### 布局

`Variant` 可选 `Columns`、`Minimal` 或 `Logo`。Columns 和 Logo 共享简介、三列链接和法律行；只有 Columns 显示订阅，只有 Logo 显示拱形。Minimal 显示品牌和一行链接，然后是版权与标语，以及状态和社交链接。它的链接由 `Links` 提供；为 null 时，它使用每列的第一个链接以及前两个法律链接。

### 链接

`SiteFooterLink` 具有 `Label`、可选的 `Href` 和 `IsExternal`（显示向外的箭头）。按下链接、品牌或状态时，会引发 `Navigate`，参数包含该链接及其所在位置（`Brand`、`Column`、`Legal`、`Social`、`Status`），并执行 `NavigateCommand`。当事件未被标记为已处理，且 `Href` 是绝对的 http、https 或 mailto 地址时，页脚会通过平台启动器打开它。没有 `Href` 的链接只引发事件。

`Columns` 保存链接列（或绑定 `ColumnsSource`，它优先生效并会被监听）。`LegalLinks` 和 `SocialLinks` 会替换示例链接，分别位于版权旁边以及法律行末尾。

### 订阅

<Demo name="Subscribe" />

订阅表单会就地验证。字段为空时提示 "Enter your email address"，格式错误时提示 "That email doesn't look right"。当字段失去焦点且其中有内容时开始验证，之后每次按键都会重新验证。提交后进入加载状态（按钮转动，输入框变为只读），等待 `SubscribeAsync` 完成，最后进入完成状态：按钮显示 "Subscribed" 和对勾，其下方一行显示 "Check {address} to confirm"。`SubscribeAsync` 抛出异常会回到空闲状态，并显示失败信息。成功时会引发 `Subscribed`。

```csharp
footer.SubscribeAsync = async (email, cancellationToken) =>
{
    await Task.Delay(500, cancellationToken); // call your list here; throw to show the failure message
};
footer.Subscribed += (_, e) => Console.WriteLine($"Subscribed {e.Email}");
```

表单中的每段文字都是一个属性（`NewsletterTitle`、`NewsletterDescription`、`NewsletterPlaceholder`、`NewsletterSubscribeLabel`、`NewsletterSubscribedLabel`、`NewsletterEmptyEmailMessage`、`NewsletterInvalidEmailMessage`、`NewsletterFailedMessage`、`NewsletterConfirmFormat`），默认值为 Arc 的英文文本。

### 状态

`StatusLabel`、`StatusTone`（`Success`、`Warning`、`Danger`）和 `StatusHref` 用于描述状态链接；设置 `IsStatusVisible="False"` 可将其隐藏。

### 响应式行为

<Demo name="Narrow" />

断点依据的是页脚自身的宽度，而不是窗口的宽度。宽度在 760 及以下时，简介位于链接列上方，两者之间的间距为 40 px。宽度在 520 及以下时，链接列改为两列并排，间距为 32 / 16；订阅区不再受 400 px 的限制；法律行变为一列，左对齐。

## API 参考

<!-- api: SiteFooter -->

<!-- api: SiteFooterLink -->

<!-- api: SiteFooterColumn -->

<!-- api: SiteFooterNavigateEventArgs -->

<!-- api: SiteFooterSubscribedEventArgs -->

## 无障碍

- 页脚是一个 content-info 地标。链接列是一个导航区域，名称由 `NavigationLabel` 指定；法律和社交链接分别是列表，名称由 `LegalLabel` 和 `SocialLabel` 指定。
- 带地址的链接以链接的形式呈现；没有地址的则以按钮的形式呈现。
- 电子邮件输入框的名称由 `NewsletterEmailLabel` 指定。提示、确认和失败信息会以礼貌的方式播报，错误则以警报的方式播报。
- 标志、箭头和状态圆点都只起装饰作用。

### 键盘

| 按键 | 操作 |
|---|---|
| `Tab` / `Shift+Tab` | 在品牌、订阅、链接和状态之间移动 |
| `Enter` | 在电子邮件输入框中：订阅。在链接或按钮上：激活它 |
| `Space` | 激活获得焦点的链接或按钮 |

键盘焦点会在链接周围绘制一圈 2 px 的焦点环；鼠标按下时只获得焦点，不显示焦点环。

## 动效

| 变化 | 完整 | 减少 | 关闭 |
|---|---|---|---|
| 链接和状态的颜色 | 160 ms | 立即 | 立即 |
| 悬停时的向外箭头 | 在 160 ms 内向右上方移动 1 px | 静止 | 静止 |
| 输入框边框（聚焦、无效） | 160 ms | 立即 | 立即 |
| 订阅行 | 从下方 4 px 处升起并经过模糊，在 240 ms 内出现；旧行在 120 ms 内向上淡出 | 仅淡入 | 立即 |
| 订阅按钮 | 文字以模糊方式交叉淡化，宽度以弹簧变化，加载时显示旋转指示器 | 按钮的减少动效规则 | 立即 |
| 标志拱形 | 仅一次，当 30% 出现在屏幕上时：在 900 ms 内淡入并上升 40 px | 立即显示 | 立即显示 |

## 相关

- 用于整幅宽订阅版块的 Newsletter signup 区块。
- 用于页脚上方结束性行动号召的 CTA section 区块。
- 与之配套的顶部栏 Site header 区块。
