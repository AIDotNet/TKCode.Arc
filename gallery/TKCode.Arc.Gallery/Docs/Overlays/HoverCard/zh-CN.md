---
title: 悬停卡片
summary: 悬停或聚焦时预览人员或链接，无需离开页面。
source-hash: 309b3a59cc84
---

<Demo name="Mention" />

## 何时使用

`HoverCard` 在文字中的某个名字被指向或获得焦点时，显示该人员、记录或链接的简短只读预览。预览的主体留在页面中，紧挨着提及它的文字，因此读者可以确认提及的是谁，而不会在讨论串、表格或列表中丢失位置。

- **评论或事件讨论串中的提及。** 事后复盘中的 `@maya` 会展开她的角色、团队与当地时间，值班工程师在 @ 任何人之前就能知道该问谁。
- **表格中的负责人或作者姓名。** 带负责人列的部署列表可以在每个名字上展开一张小型资料卡，而不必为了快速确认而跳转到单独的用户页面。
- **指向内部记录的链接。** 指向拉取请求、订单或支持工单的链接，可以在悬停时预览标题和一行背景信息，点击仍会跳转到该记录。
- **密集的人员名单。** 评审人或负责人列表中，读者会从一个名字移到下一个。一张卡片打开后，下一张几乎立即打开，因此浏览名单仍然很快（参见密集列表）。

## 何时不用

- **图标或被截断单词的一行标签：** 用 `Tooltip`。悬停卡片用于呈现关于某个对象的多项信息；标签只是一个短语，提示更轻量，也不需要预览布局。
- **按钮、输入框或任何操作：** 用 `Popover`。焦点不会进入悬停卡片，因此其中的按钮无法通过 Tab 到达，字段也无法输入。
- **登录用户自己的账户菜单：** 用 `UserMenu`，它列出预览卡片不提供的账户操作（资料、退出登录）。
- **读者在继续之前必须看到的决定或警告：** 用 `Dialog`。指针离开或在卡片外按下时，悬停卡片就会关闭，因此它无法承载读者必须确认的消息。
- **完整资料、动态流或长篇简介：** 链接到该记录的页面。卡片默认宽 296 px，过长的预览会遮住下方的内容。

## 用法

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
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
</UserControl>
```

`HoverCard` 派生自 Avalonia 的 `ContentControl`，因此它的 `Content` 就是预览内容，`ContentTemplate` 作用于该内容。`Trigger` 是留在页面中、可获得焦点的控件。在页面中，卡片只显示触发器；其内容在一个锚定的 `PopoverSurface` 中打开，这与 `Popover` 使用的表面相同，并承载于弹层中。弹层不参与布局，因此卡片永远不会推动周围的文字。`HoverCardProfile` 派生自 `TemplatedControl`，提供标准的人员布局（头像、姓名、角色、简介、统计与页脚），使应用中的每张卡片外观一致。

### 结构

1. **触发器**（`Trigger`）：页面中可获得焦点的控件，例如带有账号句柄的 Ghost `Button` 或链接。它是布局中唯一占据空间的部分。
2. **卡片表面**：一个 `PopoverSurface`，宽度由 `CardWidth` 决定（默认 296），内边距 16 px，使用浮起表面色、1 px 的边框色边框与面板圆角。它不可获得焦点，浮动阴影由弹层绘制。
3. **头像**（`Avatar`）：一个大号 `Avatar`，由 `AvatarSource` 提供图像。没有肖像时显示 `DisplayName` 的首字母。
4. **姓名**（`DisplayName`）：基础字号、中等字重、主文字颜色。只占一行，过长时以省略号结尾。
5. **角色**（`Role`）：次要文字颜色的小号文字，只占一行，过长时以省略号结尾。字符串为空时隐藏。
6. **简介**（`Bio`）：次要文字颜色的小号段落。会换行；为空时隐藏。
7. **统计**（`Stats`）：位于 1 px 分隔线下方的一行，分隔线使用淡边框色，分隔线上方留 12 px。每项以基础字号、中等字重显示数值，数值下方是超小号弱化色的标签。各列间距 24 px，行间距 8 px，该行会换行。`Stats` 为空时隐藏。
8. **元信息**（`Meta`）：超小号弱化色的页脚，通常是地点和当地时间。值为 null 时隐藏。

资料各行之间相距 12 px，头像与文字列之间相距 12 px。卡片与触发器相距 8 px（`Offset`）。

### 资料卡片

上面的资料是预览的常见形式：姓名与角色用来识别此人，简介说明此人在做什么，统计给出两个有助于读者判断的数字，元信息则为身处其他时区的人提供地点和当地时间。统计项保持两到三个。卡片宽度固定，因此每项统计都与相邻项并排占据一列，统计项过多会让卡片比读者预期的更高。

### 链接预览

`arc:HoverCard.Attach` 可以为任意控件添加卡片，例如普通的文本链接，而无需 `Trigger`。附加的卡片锚定在它所附着的控件上。卡片打开时会加入该控件的逻辑树，因此样式与目标的 `DataContext` 都能传递到卡片中。

<Demo name="LinkPreview" />

此处卡片的 `Content` 是字符串。字符串会在卡片的默认文本模板中以换行文字显示，足以容纳链接的标题和一行摘要。当对象是人员时，请使用 `HoverCardProfile`。

### 时间与宽度

`OpenDelay` 默认为 500 ms，`CloseDelay` 默认为 140 ms。打开延迟可以防止指针在前往别处的途中经过某个名字时，每次都弹出卡片。关闭延迟是一段宽限期：它给指针留出穿过 8 px 间隙进入卡片的时间，而进入卡片会取消关闭，读者因此可以滚动简介或选中文字。`CardWidth` 设置卡片的宽度，默认为 296（18.5 rem）。卡片同样受窗口可用空间的限制，当首选一侧放不下时会翻转到另一侧。

```csharp
card.OpenDelay = TimeSpan.FromMilliseconds(300);
card.CardWidth = 320;
```

只有当简介长到在 296 宽度下会换行成多行时，才加宽卡片。只在部署负责人这类预览比安静页面更重要的触发器上缩短 `OpenDelay`。页面范围还有一种预热状态：任何卡片打开期间，以及最后一张卡片关闭后的 300 ms 内，下一张卡片会在 80 ms 后仅以淡入方式打开，因此跨越名单的读者不必在每个名字上都等待 500 ms。

### 从代码中显示

`Show` 与 `Hide` 直接修改 `IsOpen`。`Show` 立即打开卡片，不经过 `OpenDelay`；`Hide` 立即关闭卡片，不经过 `CloseDelay`。`IsOpen` 为双向绑定，因此绑定与代码修改都能打开卡片。`Opened` 与 `Closed` 报告状态变化。`Closed` 还携带 `Reason`：读者按下 Escape 时为 `Escape`，在卡片外按下时为 `PressOutside`，指针离开触发器或代码调用 `Hide` 时为 `Programmatic`。引发 `Closed` 时，退出动画仍可能在播放。

```csharp
card.Show();
card.Closed += (_, e) => log.Text = $"关闭方式：{e.Reason}";
```

`Anchor` 属性是卡片定位所依据的控件：卡片附加时为附加的目标，否则为 `Trigger`。`Surface` 与 `Popup` 在首次打开后暴露表面和弹层，在此之前为 null。

### 密集列表

卡片打开后，移到下一个触发器会几乎立即打开它，且只有淡入效果。评审人名单是典型场景：每个名字都显示各自的卡片而无需等待半秒，页面也不会在卡片之间闪烁。

<Demo name="DenseList" />

### 绑定到视图模型

在 MVVM 应用中，人员数据由视图模型持有，悬停卡片为每一项渲染一份资料。只有 `IsOpen` 是双向的；`Trigger`、`Content` 与资料属性都应从数据单向绑定。

```csharp
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

public sealed record MemberProfile(
    string Handle,
    string Name,
    string Role,
    string Bio,
    string Location,
    int Projects,
    int Reviews);

public partial class ReviewersViewModel : ObservableObject
{
    public ObservableCollection<MemberProfile> Reviewers { get; } = new();
}
```

```xml
<ItemsControl ItemsSource="{Binding Reviewers}">
    <ItemsControl.ItemTemplate>
        <DataTemplate>
            <arc:HoverCard>
                <arc:HoverCard.Trigger>
                    <arc:Button Variant="Ghost" Content="{Binding Handle}" />
                </arc:HoverCard.Trigger>
                <arc:HoverCardProfile DisplayName="{Binding Name}"
                                      Role="{Binding Role}"
                                      Bio="{Binding Bio}"
                                      Meta="{Binding Location}" />
            </arc:HoverCard>
        </DataTemplate>
    </ItemsControl.ItemTemplate>
</ItemsControl>
```

`Stats` 是只读集合，XAML 无法绑定它。请在创建资料时用代码填充，并以当前区域文化格式化数字，使千分位分隔符遵循读者的设置：

```csharp
var profile = new HoverCardProfile { DisplayName = member.Name, Role = member.Role };
profile.Stats.Add(new HoverCardStat("项目", member.Projects.ToString("N0", CultureInfo.CurrentCulture)));
profile.Stats.Add(new HoverCardStat("评审", member.Reviews.ToString("N0", CultureInfo.CurrentCulture)));
```

把账号句柄保留在触发器上，姓名放在卡片中：触发器是键盘用户会到达的控件，它需要有能标识此人的文字。

### 焦点与关闭

悬停卡片由三种输入打开：指针、Tab 焦点与点击。它们的规则各不相同，这样键盘用户与鼠标用户都能获得可预测的卡片行为。

- **Tab 焦点**会立即打开卡片。由指针点击获得的焦点不会打开卡片，打开它的是点击本身。
- **失去焦点**会启动关闭宽限期。卡片会在 `CloseDelay` 之后关闭，除非指针在触发器或卡片上。
- **Escape** 关闭已打开的卡片。之后卡片会保持关闭，直到指针或焦点离开触发器，因此读者仍停留在同一个名字上时，它不会重新打开。
- **在卡片外按下**会以 `PressOutside` 原因关闭它。
- **同一时间只有一张卡片打开。** 打开一张卡片会关闭之前打开的那张。
- **从页面移除触发器**会立即关闭卡片，且不播放关闭动画，因此刷新的列表不会留下残余的卡片。

焦点永远不会进入卡片。卡片本身没有可获得焦点的内容，读者无法通过 Tab 进入，也无法在其中输入。需要控件的卡片应改用 `Popover`。

### 示例：管理控制台中的部署负责人

SaaS 运维控制台中的部署历史。每位负责人都是一个带账号句柄的 Ghost 按钮，因此该行读起来很快，负责人的角色与当地时间则在悬停或聚焦时才出现。卡片只显示两项统计，因为读者需要的是负责人当前的工作量，而不是历史记录。元信息行给出当地时间，这在值班交接跨越时区时尤为重要。

```xml
<StackPanel Width="560" Spacing="12">
    <Grid ColumnDefinitions="*,Auto,Auto" ColumnSpacing="16">
        <TextBlock Text="Payments API 2026.10.14" VerticalAlignment="Center" />
        <TextBlock Grid.Column="1" Text="已成功" VerticalAlignment="Center"
                   Foreground="{arc:ArcDesignTokenResource TextSecondary}" />
        <arc:HoverCard Grid.Column="2" Placement="Left">
            <arc:HoverCard.Trigger>
                <arc:Button Variant="Ghost" Content="@ari" />
            </arc:HoverCard.Trigger>
            <arc:HoverCardProfile DisplayName="Ari Okafor"
                                  Role="高级工程师，平台"
                                  Bio="负责构建流水线与发布节奏。"
                                  Meta="拉各斯 · 当地时间 13:20">
                <arc:HoverCardProfile.Stats>
                    <arc:HoverCardStat Label="待审评审" Value="3" />
                    <arc:HoverCardStat Label="值班" Value="今天" />
                </arc:HoverCardProfile.Stats>
            </arc:HoverCardProfile>
        </arc:HoverCard>
    </Grid>
    <Grid ColumnDefinitions="*,Auto,Auto" ColumnSpacing="16">
        <TextBlock Text="账单 Webhook 2026.10.19" VerticalAlignment="Center" />
        <TextBlock Grid.Column="1" Text="运行中" VerticalAlignment="Center"
                   Foreground="{arc:ArcDesignTokenResource TextSecondary}" />
        <arc:HoverCard Grid.Column="2" Placement="Left">
            <arc:HoverCard.Trigger>
                <arc:Button Variant="Ghost" Content="@lena" />
            </arc:HoverCard.Trigger>
            <arc:HoverCardProfile DisplayName="Lena Novak"
                                  Role="研究负责人"
                                  Bio="负责每季度的可用性研究。"
                                  Meta="布拉格 · 当地时间 14:20">
                <arc:HoverCardProfile.Stats>
                    <arc:HoverCardStat Label="待审评审" Value="1" />
                    <arc:HoverCardStat Label="值班" Value="明天" />
                </arc:HoverCardProfile.Stats>
            </arc:HoverCardProfile>
        </arc:HoverCard>
    </Grid>
</StackPanel>
```

`Placement="Left"` 让每张卡片不遮挡状态列及其下方的行。

### 文案规范

- **显示名**：组织使用的完整名称，例如 "Maya Chen"。账号句柄属于触发器，因此卡片中不重复它。
- **角色**：职能与团队，以逗号分隔，例如“产品设计师，支付”。使用句子式大小写。不要用“员工”之类的占位文字凑数，宁可省略角色。
- **简介**：一句话说明此人在做什么，使用现在时。在卡片宽度下控制在一到两行之内。
- **元信息**：地点与当地时间，以中间点作分隔符，例如“里斯本 · 当地时间 14:20”。
- **统计标签**：一到两个词的复数名词（“项目”“评审”）。把数字以阿拉伯数字写入 `Value`，便于比较。

| 不要写 | 改为 |
|---|---|
| 嗨！我是一名热爱各种事物的设计师。 | 负责结账与账单流程的设计。 |
| 角色：员工 | 产品设计师，支付 |
| 你参与过的项目数量 | 项目 |
| 时间：14:20 | 里斯本 · 当地时间 14:20 |
| 名字：@maya | Maya Chen（句柄即触发器文字） |

### 最佳实践

**推荐**

- 使用可获得焦点的控件作为触发器，其文字要能标识此人，例如带账号句柄的 Ghost `Button`。键盘用户通过触发器到达卡片，而触发器的文字是屏幕阅读器最先朗读的内容。
- 保持卡片只读。操作应放在该记录的页面上或触发器上；当预览需要按钮时，改用 `Popover`。焦点永远不会进入悬停卡片，因此其中的按钮无法从键盘到达。
- 简介控制在一句话，统计控制在两到三个值。卡片宽度固定，因此在长列表中，短卡片能一眼看懂。
- 在包含很多触发器的页面上保留默认的 `OpenDelay`。500 ms 的等待能让指针经过其他名字前往别处时，卡片保持安静。
- 让链接预览的文字与链接文字相同，这样卡片打开时宣读的帮助文本，与读者已读到的内容一致。

**避免**

- 在卡片中放置按钮、字段或链接。焦点永远不会进入卡片，这些控件无法从键盘到达，触摸用户也无从按下它们。
- 在列表中的每个触发器上都把 `OpenDelay` 设为零。卡片会在指针每次经过时打开，页面看起来充满噪点。
- 把卡片作为了解某人详情的唯一途径。屏幕阅读器只会听到触发器帮助文本中的摘要，而关闭时卡片并不在页面上。
- 在 `Bio` 或 `Meta` 中放长段落。卡片会随文字增高，并遮住其下方的行；请改为链接到完整资料。
- 使用不可获得焦点的 `Trigger`，例如普通的 `TextBlock`。键盘用户无法聚焦它，卡片对他们永远不会打开。请在文字上使用 `arc:HoverCard.Attach`，或把可获得焦点的控件作为触发器。

### 常见问题

**为什么悬停要等半秒，而点击会立即打开卡片？** 在触发器上点击鼠标是明确的请求，因此它不经过 `OpenDelay` 就打开卡片。等待只适用于悬停，而悬停可能在指针经过触发器时意外发生。

**读者能把指针移入卡片吗？** 能。离开触发器会开始 `CloseDelay`（140 ms）的关闭宽限期。进入卡片会取消该关闭，只要指针停留在卡片上，卡片就保持打开。离开卡片后，它会在宽限期结束时关闭。

**触摸屏如何打开卡片？** 点按触发器会切换卡片：第一次点按立即打开，下一次点按触发器则关闭。触摸时的指针进入与离开事件不会打开或关闭它。

**卡片打开时屏幕阅读器会听到什么？** 卡片打开时，触发器的帮助文本会被设为摘要。对于 `HoverCardProfile`，即姓名，若有角色则其后接角色；对于字符串内容，即该字符串；否则为卡片自身的 `AutomationProperties.Name`。卡片关闭后会恢复之前的帮助文本，因此之后读者听到的是触发器的常规描述。

## API 参考

<!-- api: HoverCard -->

<!-- api: HoverCardProfile -->

<!-- api: HoverCardStat -->

## 无障碍

- 卡片打开时，其摘要会写入触发器的帮助文本。卡片本身没有无障碍名称（除非另行设置），因此触发器的文字必须能标识此人。
- 焦点永远不会进入卡片。请保持其内容只读，并将操作放在触发器上或 `Popover` 中。
- 卡片表面向辅助技术暴露为窗格，而非按钮或链接。
- 通过 Tab 获得焦点会立即打开卡片，Enter 跳过打开延迟，按 Escape 关闭。指针停留在触发器上时，卡片保持关闭。
- 在触摸设备上，点按会切换卡片。同一时间只有一张卡片打开，因此屏幕阅读器用户不会同时面对两个相互争夺注意力的预览。

### 键盘

| 按键 | 操作 |
|---|---|
| `Tab` 移到触发器上 | 立即打开卡片，没有打开延迟 |
| `Tab` 离开触发器 | 在 `CloseDelay`（140 ms）之后关闭卡片，除非指针在触发器或卡片上 |
| 触发器上的 `Enter` | 卡片关闭时立即打开它 |
| `Escape` | 关闭卡片；直到指针或焦点离开触发器之前，它都保持关闭 |

## 动效

- 卡片从触发器边缘生长：以平滑弹簧从缩放 0.96 变为 1，并向触发器方向移动 4px，同时单独淡入。
- 资料各行依次从下方 4px 处就位。头像在肖像加载完成后，从柔和的模糊中淡入。
- 若另一张卡片在 300ms 之前刚关闭，则下一张卡片会在 80ms 后仅以淡入方式打开。
- 每次打开都会重新播放资料各行，因此再次打开的卡片总是从相同的起点就位。
- 减弱动效时移除缩放与移动，保留短暂淡入。关闭动效时卡片立即显示或移除。

| 变化 | 完整 | 减弱 | 关闭 |
|---|---|---|---|
| 卡片打开 | 以平滑弹簧从缩放 0.96 开始并向触发器方向移动 4px；淡入同步进行 | 仅短暂淡入 | 立即显示 |
| 卡片关闭 | 淡出；若在退出途中重新打开则反向 | 短暂淡入 | 立即移除 |
| 资料各行打开 | 每一行上升 4px 并淡入，在前一行之后 35ms 开始，每次打开都如此 | 直接以静止状态显示各行 | 直接以静止状态显示各行 |
| 最近关闭后的下一张卡片 | 80ms 后打开，仅淡入 | 仅淡入 | 立即显示 |

## 相关组件

- `Tooltip` 用于悬停或聚焦时的简短文字标签。
- `Popover` 用于包含按钮或输入框、点击打开的卡片。
- `Avatar` 用于资料中显示的图片。

| 组件 | 适用场景 |
|---|---|
| `Tooltip` | 一行标签足以说明图标、快捷键或被截断的单词 |
| `Popover` | 内容包含按钮、输入框或小型表单，并通过点击打开 |
| `Avatar` / `AvatarGroup` | 需要卡片所显示的肖像或首字母，或一组人员 |
| `Dialog` | 读者必须阅读或处理某些内容后才能继续 |
| `UserMenu` | 菜单属于登录用户自己的账户 |
| `DropdownMenu` | 从触发器打开一组命令列表 |
