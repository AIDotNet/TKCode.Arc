---
title: 徽标
summary: 用于状态、类别或元数据的小型标签。
source-hash: 3af91b4ba63d
---

<Demo name="Tones" />

## 何时使用

`Badge` 是一个小型药丸形标签，用来标明一种状态、一个类别或一条元数据。它放在标题旁边或表格单元格内，一眼就能读懂，并能与周围各行的徽标相互比较。它是带有色调的行内文字：不接收焦点，不打开任何内容，也不主动播报，因此标签本身必须能说明状态。

- **账单表格中的订单与发票状态**：每一行用 `Small` 徽标显示“已付款”“待处理”或“失败”，操作人员无需打开任何一张发票，就能浏览整页发票。
- **发布页上的部署与环境标签**：生产版本旁边用成功色调显示“已上线”，预览构建旁边用强调色调显示“测试版”，尚未发布的内容则显示“草稿”。
- **卡片或详情页头部的元数据**：客户名称旁边的套餐名称、地区或“新”标记，标签描述的是对象的事实而不是操作。
- **页面打开期间会变化的计数**：菜单项旁边的未读数或未关闭工单数。徽标保持挂载，新数字会原位上升，药丸宽度随之扩展。
- **带前导图标的行标记**：活动或事件时间线中，图标说明事件类型，色调说明它是正常、待处理还是失败。

## 何时不用

- **一整句反馈**，例如“你的银行卡被拒绝”：需要留在页面上的消息用 `Alert`，会自动消失的消息用 `Toast`。
- **用户会开关的值**，例如用户选择的筛选条件或标签：用 `ChipGroup`，它负责选择与键盘焦点。
- **带有上下文的大号数字**，例如本季度的营收：用 `MetricCard`，它能给数字留出空间并配上说明文字。
- **人的在线状态**，例如在线或离线：用 `Avatar` 上的状态点。徽标标明的是状态，不应覆盖在人的头像上。
- **用户需要通过徽标执行操作的计数或状态**：用 `Button` 或 `Tabs` 项，因为徽标不接收焦点，也没有点击行为。

## 用法

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:Badge Content="草稿" />
</UserControl>
```

`Badge` 派生自 Avalonia 的 `ContentControl`，因此 `Content` 可以是任意对象。字符串和数字会获得下文描述的原位动画；其他内容按原样显示，不做替换动画。药丸的填充、边框和文字颜色由色调与主题决定，因此请设置 `Tone` 和 `Size`，而不是直接设置画刷。

### 结构

1. **药丸**（控件本身）：1 px 边框，圆角为药丸形。`Medium` 时最小高度为 26 px，`Small` 时为 22 px；药丸会随标签变宽。
2. **色调环**（仅 `Info`）：覆盖在边框上的强调色环，不透明度为 24%。
3. **悬停光晕**（仅 `Info`）：指针悬停在徽标上时，表面叠加 10% 的强调色光晕，色环的不透明度提高到 32%。
4. **图标**（`Icon`，前导）：可选的字形，使用色调颜色。对辅助技术隐藏。
5. **标签**（`Content`）：中等字重的文字。请控制在一到两个词，因为药丸会随之变宽，过长的标签会把表格中相邻的内容挤开。

`Medium` 时每侧内边距为 10 px，`Small` 时为 8 px。图标与标签之间的间距在 `Medium` 时为 5 px，在 `Small` 时为 4 px。

### 色调

`Tone` 决定颜色样式，默认为 `Neutral`。标签本身必须说明状态，因为色调并不是唯一的信号，而且打印件或灰度报表会丢失颜色。

| 色调 | 文字 | 填充 | 边框 | 适用场景 |
|---|---|---|---|---|
| `Neutral` | 次要文字颜色 | 弱化表面 | 边框颜色 | 草稿、已归档、元数据 |
| `Success` | 成功色 | 成功色淡染，占表面 10% | 成功色淡染，占边框 25% | 上线、已付款、通过、健康 |
| `Info` | 加深的强调色 | 淡强调色 | 带 24% 强调色环的边框 | 测试版、同步中、强调 |
| `Warning` | 警告色 | 警告色淡染，占表面 11% | 警告色淡染，占边框 27% | 待处理、性能下降、即将到期 |
| `Danger` | 危险色 | 危险色淡染，占表面 10% | 危险色淡染，占边框 26% | 失败、逾期、已阻塞 |

成功、警告和危险的填充是状态颜色与中性表面的固定混合，因此强调色变化时它们的含义保持不变。`Info` 跟随强调色，所以它适合用于强调，而不适合用于必须始终保持同一外观的状态。

### 尺寸

`Size` 决定高度：`Medium`（默认）为 26 px，`Small` 为 22 px。密集的表格中用 `Small`，标题旁边和卡片中用 `Medium`。

<Demo name="Small" />

| 尺寸 | 最小高度 | 左右内边距 | 标签 | 图标间距 |
|---|---|---|---|---|
| `Medium` | 26 px | 10 px | Arc XS | 5 px |
| `Small` | 22 px | 8 px | 11 px | 4 px |

每个表格列或每组内使用同一尺寸，让各行高度保持一致。宽度固定的表格单元格能容纳一个词的标签加一个图标的 `Small` 徽标。

### 图标

`Icon` 接收一个前导图标。图标使用色调颜色，并对辅助技术隐藏。示例使用 `ArcIcons`（Arc 图标集）中的 `ArcGlyph`，尺寸为 12 px。

<Demo name="Icons" />

```csharp
badge.Tone = BadgeTone.Success;
badge.Content = "通过";
badge.Icon = new ArcGlyph { Data = ArcIcons.Check, Size = 12 };
```

只有当图标能增加信息时才添加，例如为失败的检查添加警告三角。如果每个徽标都带装饰性图标，表格会显得杂乱，而色调本身已经承载了颜色信息。

### 原位变化

保持徽标挂载，只修改 `Content`。新的字符串或数字会从柔和的模糊中升起，旧内容向上淡出，药丸宽度弹性地调整到合适的大小。重新创建徽标会失去这种动效，并且每次变化都要新建一个控件。

<Demo name="ChangingInPlace" />

新图标会从略小的尺寸弹出。由字体或主题变化引起的尺寸改变（而不是由新内容引起的）会立即生效，因此状态没有变化时，布局不会播放弹簧动效。

### 绑定到视图模型

`Badge` 只负责显示值。从视图模型绑定 `Content`、`Tone`、`Size` 和 `Icon`；徽标从不回写值，也不会引发事件或命令供视图模型处理。

```csharp
public partial class DeploymentViewModel : ObservableObject
{
    [ObservableProperty]
    private string _statusText = "排队中";

    [ObservableProperty]
    private BadgeTone _statusTone = BadgeTone.Info;

    [ObservableProperty]
    private int _commentCount;
}
```

```xml
<StackPanel Orientation="Horizontal" Spacing="8">
    <arc:Badge Tone="{Binding StatusTone}" Content="{Binding StatusText}" />
    <arc:Badge Tone="Neutral" Content="{Binding CommentCount}" />
</StackPanel>
```

这些属性必须在 UI 线程上设置，因为 Avalonia 属性只能在该线程上修改。状态变化时，请同时更新文字和色调，让标签与颜色在同一帧中变化。

### 数据结构与性能

徽标数量少时开销很小，在长表格中开销会增大。每个徽标都要测量自身宽度，以便弹性地适应标签，因此屏幕上有数千个徽标的表格，比普通文字表格做更多的布局工作。在长表格中：

- 使用 `Small` 与一个词的标签，让值变化时宽度的改变尽可能小。
- 保持行虚拟化（Avalonia 的 `ListBox` 与 `DataGrid` 默认如此），这样只有可见的行才会创建徽标。
- 在拥有数据的代码中按语言环境格式化数字，使用 `ToString("N0", culture)` 或格式化字符串，让分隔符与语言环境一致：英文中为 "1,204"，德文中为 "1.204"。

### 示例：发票状态表格

一个账单运营页面列出本周发出的发票。每一行显示客户名称，并以 `Small` 徽标显示状态。已付款的发票使用成功色调，不需要图标。待处理的发票带一个时钟图标，失败的发票带一个警告三角，因为失败的付款需要有人处理，图标提供了第二重信号。草稿行使用中性色调，让人读作“尚未发送”，而不是“有问题”。

![发票状态表格，包含已付款、待处理、失败和草稿徽标，尺寸为 Small](images/BadgeStatusTable.png)

```xml
<Border Width="640"
        Padding="20"
        Background="{arc:ArcDesignTokenResource Surface}"
        BorderBrush="{arc:ArcDesignTokenResource Border}"
        BorderThickness="1"
        CornerRadius="12">
    <StackPanel Spacing="14">
        <TextBlock Text="Invoices sent this week"
                   FontSize="16"
                   FontWeight="SemiBold"
                   Foreground="{arc:ArcDesignTokenResource Foreground}" />
        <Grid ColumnDefinitions="176,*,Auto">
            <TextBlock Text="Invoice" FontSize="12"
                       Foreground="{arc:ArcDesignTokenResource TextMuted}" />
            <TextBlock Grid.Column="1" Text="Customer" FontSize="12"
                       Foreground="{arc:ArcDesignTokenResource TextMuted}" />
            <TextBlock Grid.Column="2" Text="Status" FontSize="12"
                       Foreground="{arc:ArcDesignTokenResource TextMuted}" />
        </Grid>
        <Border Height="1" Background="{arc:ArcDesignTokenResource Border}" />
        <Grid ColumnDefinitions="176,*,Auto">
            <TextBlock Text="inv_2026_0412" FontSize="12" VerticalAlignment="Center"
                       Foreground="{arc:ArcDesignTokenResource TextMuted}" />
            <TextBlock Grid.Column="1" Text="Northwind Traders" VerticalAlignment="Center"
                       Foreground="{arc:ArcDesignTokenResource Foreground}" />
            <arc:Badge Grid.Column="2" Size="Small" Tone="Success" Content="Paid" />
        </Grid>
        <Grid ColumnDefinitions="176,*,Auto">
            <TextBlock Text="inv_2026_0413" FontSize="12" VerticalAlignment="Center"
                       Foreground="{arc:ArcDesignTokenResource TextMuted}" />
            <TextBlock Grid.Column="1" Text="Acme Analytics" VerticalAlignment="Center"
                       Foreground="{arc:ArcDesignTokenResource Foreground}" />
            <arc:Badge Grid.Column="2" Size="Small" Tone="Warning" Content="Pending">
                <arc:Badge.Icon>
                    <arc:ArcGlyph Data="{x:Static arc:ArcIcons.Clock}" Size="12" />
                </arc:Badge.Icon>
            </arc:Badge>
        </Grid>
        <Grid ColumnDefinitions="176,*,Auto">
            <TextBlock Text="inv_2026_0414" FontSize="12" VerticalAlignment="Center"
                       Foreground="{arc:ArcDesignTokenResource TextMuted}" />
            <TextBlock Grid.Column="1" Text="Contoso Labs" VerticalAlignment="Center"
                       Foreground="{arc:ArcDesignTokenResource Foreground}" />
            <arc:Badge Grid.Column="2" Size="Small" Tone="Danger" Content="Failed">
                <arc:Badge.Icon>
                    <arc:ArcGlyph Data="{x:Static arc:ArcIcons.TriangleAlert}" Size="12" />
                </arc:Badge.Icon>
            </arc:Badge>
        </Grid>
        <Grid ColumnDefinitions="176,*,Auto">
            <TextBlock Text="inv_2026_0415" FontSize="12" VerticalAlignment="Center"
                       Foreground="{arc:ArcDesignTokenResource TextMuted}" />
            <TextBlock Grid.Column="1" Text="Fabrikam Studio" VerticalAlignment="Center"
                       Foreground="{arc:ArcDesignTokenResource Foreground}" />
            <arc:Badge Grid.Column="2" Size="Small" Content="Draft" />
        </Grid>
    </StackPanel>
</Border>
```

### 文案规范

- **标签**：一到两个词，说明状态或类别，使用句首大写且不加标点，例如“已付款”“待处理”“已上线”。徽标会被读作它自身的名称，因此即使没有颜色也必须说得通。
- **数字**：计数用阿拉伯数字书写，并按语言环境格式化（"1,204"），且保持足够短，让药丸不会比相邻的徽标更宽。
- **色调**：根据状态的含义选择色调，而不是根据想要的外观。危险色表示用户必须采取行动；警告色表示状态很快需要关注；成功色表示状态良好。

| 不要写 | 改为 |
|---|---|
| `Content="LIVE NOW!!"` | `Content="Live"` |
| `Content="Status: failed"` | `Content="Failed"`，上下文由所在行提供 |
| `Content="Your card was declined"` | 改用 `Alert`；徽标只放一个词或短语 |
| `Content="1204"` | `Content="1,204"`，按语言环境格式化 |
| `Tone="Danger"` 用于中性计数 | `Tone="Neutral"`，因为红色意味着需要用户采取行动 |

### 最佳实践

**推荐**

- 在标签中说明状态。色调只是颜色，如果标签含糊，色盲读者和灰度打印件就无从得知状态。
- 保持徽标挂载，状态变化时修改 `Content` 和 `Tone`。标签会原位升起，药丸会弹性地过渡到新宽度，让读者感知到发生了变化。
- 每个列或每组使用同一尺寸。行高一致，表格才便于扫读。
- `Danger` 只用于失败以及需要用户采取行动的情况，`Info` 只用于强调。同一套颜色在不同页面表示不同含义，会让人不再信任颜色。

**避免**

- 用徽标承载一整句话。句子需要换行空间，还需要可关闭的操作，这正是 `Alert` 和 `Toast` 提供的。
- 为每个状态重新创建徽标。新控件会失去原位动效，并且每次变化都要重新构建布局。
- 在狭窄的表格单元格中加入很长的标签或很大的图标。药丸会随标签变宽，并把周围的单元格挤开。
- 期望它可以点击。徽标不接收焦点，因此用户必须打开查看的状态需要在旁边放一个 `Button` 或行操作。
- 从徽标上播报每一次变化。徽标不会播报自身的变化；如果变化必须被朗读，请把徽标放在外围内容的实时区域中。

### 常见问题

**为什么我的计数不做动画？** 很可能是徽标被重新创建了，或者它的 `Content` 不是字符串或数字。只有字符串和数字类型才会进行原位替换。请保持同一个徽标在屏幕上，并把 `Content` 设为字符串或数字。

**徽标能作为计数或圆点放在头像上吗？** 不能。徽标是独立的药丸，不是覆盖在其他元素上的叠加层。要表示人的在线状态，请使用 `Avatar` 上的状态点。

**为什么 Info 色调会随强调色变化，而 Success 不会？** `Info` 由强调色推导而来，因此跟随产品的强调色。成功、警告和危险是状态颜色与中性表面的固定淡染，因此强调色变化时它们的含义保持不变。

**如何在代码中修改色调？** 在实例上设置 `Tone`，例如 `badge.Tone = BadgeTone.Warning;`。开启完整动效时颜色交叉淡化；减弱动效时变化立即完成。

**能让徽标可点击吗？** 不能直接实现。徽标不可获得焦点，也没有点击事件。请把它放进处理操作的 `Button` 或行中，并把徽标保留为视觉标签。

## API 参考

<!-- api: Badge -->

## 无障碍

- 徽标是行内文字：它的标签会与周围的文字一起被朗读。
- 图标对辅助技术隐藏。标签必须说明状态，而不是说明颜色。
- 字符串或数字标签就是徽标的无障碍名称；数字会按当前文化区域命名。
- 标签变化时，旧文字对辅助技术隐藏，只朗读当前文字。
- 徽标不会播报自身的变化。如果变化必须被播报，请把徽标放在外围内容的实时区域中。
- 徽标不可获得焦点，也没有键盘行为：`Tab` 会跳过它，承载徽标的行或控件负责所有键盘操作。

## 动效

- 新标签从柔和的模糊中升起，旧标签带着轻微模糊向上淡出。药丸宽度随即在形变弹簧上调整。
- 新图标从略小的尺寸弹出。由字体变化引起的尺寸改变立即完成。
- 减弱动效时标签快速淡入，宽度直接跳到新值；关闭动效时所有变化立即完成。

| 变化 | 完整 | 减弱 | 关闭 |
|---|---|---|---|
| 标签变化 | 旧文字带着轻微模糊向上淡出，新文字从柔和的模糊中升起 | 快速淡入 | 立即完成 |
| 标签变化后的药丸宽度 | 弹性调整到适合新标签 | 直接跳到新宽度 | 立即完成 |
| 色调变化 | 背景、边框和文字按标准曲线交叉淡化 | 瞬间完成 | 立即完成 |
| 指针进入或离开（精确指针） | 背景与边框交叉淡化；`Info` 淡出其强调光晕 | 瞬间完成 | 立即完成 |
| 字体或主题变化导致徽标尺寸改变 | 立即完成 | 立即完成 | 立即完成 |

## 相关组件

- `Alert` 或 `Toast` 用于一句话的反馈。
- `ChipGroup` 用于用户可以选择的值。
- `MetricCard` 用于带上下文的大数值。

| 组件 | 适用场景 |
|---|---|
| `Alert` | 消息需要一句话，并留在页面上 |
| `Toast` | 短暂的确认或错误提示应出现后自动消失 |
| `ChipGroup` | 用户选择或清除值 |
| `MetricCard` | 大号数字需要配上标签、趋势和上下文 |
| `Avatar` | 状态属于某个人，以头像上的圆点显示 |
| `SortableDataTable` | 徽标位于用户会排序和比较的列中 |
