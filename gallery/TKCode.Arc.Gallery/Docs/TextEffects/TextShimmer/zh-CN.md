---
title: 文字闪光
summary: 用平缓的光带扫过文字，表示正在进行的工作。
source-hash: 23c614235a9b
---

<Demo name="StatusLine" />

## 何时使用

`TextShimmer` 是一行状态文字，工作进行期间，一道柔和的光带会从上面扫过。它用于应用需要报告某件事仍在进行、却没有可测量数量可显示的位置：状态栏、页面标题、活动行或助手的回复。光带表示“仍在工作”，无需旋转指示器，文字则说明工作的内容。

- **仍在工作的 AI 助手**：回复上方显示“思考中”，长回答写作期间显示“正在生成摘要”。只有文字在移动，因此回复区域仍然易读。
- **管理后台中的后台导入或同步**：夜间任务运行时，面板显示“正在导入 2,340 个订单”。任务结束后，同一控件切换为“导入完成”，文字在原位稳定下来。
- **只知道步骤名称的多步骤任务**：工作区导出依次经历“正在读取 14 个文件”“正在起草计划”和“正在撰写摘要”。每一步替换文字，扫光持续进行。
- **构建期间的开发者工具标题**：部署视图的顶栏显示“正在编译 212 个模块”，构建通过后扫光停止。
- **结束后保留在屏幕上的标签**：以实色文字结束的状态行，例如“摘要已就绪”，使结果看起来已经尘埃落定，而不是加载状态。

## 何时不用

- **尚未到达内容的占位**：使用 `Skeleton`，它显示内容的形状。文字上的扫光传达的是工作信息，而不是布局。
- **已知完成量的工作**：使用 `Progress`，它显示工作进行到哪里。扫光无法显示百分比或剩余时间。
- **不扫光、逐字母变化的标签**：使用 `TextMorph`，它把字符变形为下一个标签。
- **段落或多行文字**：使用普通的 `TextBlock`。本控件为一行短文字而设计，不会换行。
- **需要用户处理的问题**：导入失败、会话过期或付款被拒绝。请使用 `Alert` 显示消息与下一步。失败的任务上仍有扫光，等于告诉用户工作仍在进行，这与事实不符。
- **从不变化且没有工作在进行的文字**：页面标题或字段标签。扫光只会增加噪音。

## 用法

`TextShimmer` 派生自 Avalonia 的 `Control`，而不是 `TextBlock`，并自行绘制文字。它的 `FontFamily`、`FontSize`、`FontWeight` 和 `Foreground` 属性默认继承自 `TextElement` 的值，因此无需额外标记即可采用周围样式的排版。由于它是一个直接绘制的控件，忙碌的标签在页面上只是一个元素，而不是每个字母一个元素。请让屏幕上有动画的标签保持较少：一两行忙碌文字易于阅读，六行并列就会互相抢夺注意力。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:TextShimmer Text="思考中" />
</UserControl>
```

只要 `IsActive` 为 `true`（默认值），扫光就会运行。因此新建的控件一显示就开始扫光。工作结束时将 `IsActive` 设为 `false`，文字即沉淀为实色。

### 结构

1. **基础文字**（以 `BaseForeground` 绘制）：该行静止时的颜色。未设置 `BaseForeground` 时，基础色为 `Foreground` 颜色的 12%，与弱化文字颜色在 OKLab 色彩空间中混合。主题将弱化颜色设为 TextMuted 令牌，因此静止的标签与周围文字保持同一套色板。
2. **光带**：一条钟形光带，使用 `Foreground` 颜色，宽 4.4 em（中心两侧各 2.2 em）。中心最强，两端渐隐为零。在内部，它是文字经由移动遮罩再次绘制的结果，因此光带会精确地跟随字母。
3. **实色文字**：全强度的 `Foreground` 颜色。当 `IsActive` 为 `false` 且光带已离开该行后，它取代基础文字。
4. **行**：一行文字，字号为 `FontSize`（默认 14 px，来自 TypeSM 令牌），字体为 `BodyFontFamily`，字重为常规。控件测量文字的宽度与一行的高度，且从不换行。

### 选择状态

使用 `IsActive` 表示工作是否仍在进行，而不是用来装饰页面。工作已经完成的标签上仍有扫光，会让人误以为需要等待。

- **忙碌**：`IsActive="True"`，即默认值。光带运行，无障碍的项目状态为“Busy”。
- **完成**：`IsActive="False"`。光带滑出，文字变为实色。让控件留在原处，使该行在原来的位置稳定下来。
- **首次请求前的空闲状态**：以 `IsActive="False"` 创建控件。此时它显示实色文字，并且在你将 `IsActive` 设为 `true` 之前不运行循环。

```xml
<arc:TextShimmer Text="准备导入" IsActive="False" />
```

### 更改文字

新的 `Text` 值会在原位升起，同时该行以弹簧方式调整到新宽度。步骤变化期间保持 `IsActive` 开启，光带就能跨越变化持续运行。

<Demo name="ChangingAndFinishing" />

每个步骤都应写成简短的现在分词加上其对象，并让每一步在屏幕上停留得足够久，以便阅读。文字变化得比人能读的速度还快，就会变成一团升起的模糊文字。

### 完成

将 `IsActive` 设为 `false`，而不是移除控件。光带滑出，文字变为实色，因此该行不会跳动，周围的布局也不会移位。

```csharp
summary.Text = "摘要已就绪";
summary.IsActive = false;
```

在同一步中设置最终文字和 `IsActive`，使完成的标签与其稳定状态同时出现。

### 扫光速度

`SweepDuration` 设置光带扫过文字一次所需的时间。默认值为 1.8 秒。每次扫过之后会有一段短暂的停顿，约为该时长的三分之一，然后才开始下一次。页面平静、标签是长时间运行的后台任务（例如为工作区建立索引）时，使用较慢的扫光。助手回复或按钮状态则保留默认值，因为人们期待的是轻快的信号。

<Demo name="SlowerSweep" />

`SweepDuration` 接受 `TimeSpan`。在代码中，`TimeSpan.FromSeconds(3)` 与上面 XAML 中的值相同。

### 尺寸与颜色

文字使用 `TextElement` 的字体属性。较大的字号适合状态标题；光带会随字号缩放，因此扫光在任何字号下看起来都一样。

```xml
<arc:TextShimmer Text="正在生成摘要" FontSize="22" FontWeight="SemiBold" />
```

`Foreground` 设置光带和实色文字的颜色。`BaseForeground` 显式设置静止时的颜色。当默认的混合不能保持你需要的对比度时设置它，例如在深色标题上，光带扫过时基础色仍须保持可读。

```xml
<arc:TextShimmer Text="正在同步订单"
                 Foreground="{arc:ArcDesignTokenResource Accent}"
                 BaseForeground="{arc:ArcDesignTokenResource TextMuted}" />
```

### 由代码驱动

该控件只负责显示，因此视图模型掌握文字与忙碌标志，视图负责绑定。扫光与文字变化由控件处理。

```csharp
public partial class ImportViewModel : ObservableObject
{
    private readonly IOrderImportService _imports;

    [ObservableProperty]
    private string _statusText = "正在导入 2,340 个订单";

    [ObservableProperty]
    private bool _isBusy = true;

    public ImportViewModel(IOrderImportService imports)
    {
        _imports = imports;
    }

    [RelayCommand]
    private async Task RunImportAsync()
    {
        IsBusy = true;
        StatusText = "正在导入 2,340 个订单";

        var result = await _imports.RunAsync();

        StatusText = result.IsSuccess ? "导入完成" : "导入需要处理";
        IsBusy = false;
    }
}
```

```xml
<arc:TextShimmer Text="{Binding StatusText}"
                 IsActive="{Binding IsBusy}"
                 FontSize="16" />
```

导入失败时，应在该行旁边用 `Alert` 显示原因，而不是只更改扫光的文字。扫光在 `IsBusy` 设为 `false` 时结束，因此它绝不会声称工作仍在进行。

### 克制地使用动效

动效在它标示出页面上唯一仍在运行的元素时最有帮助。当多行同时扫光、扫光作用于已完成的标签，或文字本身只是动效的装饰时，动效就会分散注意力。

- **适合的场合**：标题或面板中的状态行，其他元素都不移动，光带即可告诉人们任务仍在运行，无需旋转指示器。
- **分散注意力的场合**：一列忙碌的行全都在扫光，或在人们等待时阅读的标签上使用比默认值更快的扫光。
- **减弱动效**：光带停止，标签以普通淡入切换。仅靠文字也必须能传达状态，因此要写“正在读取 14 个文件”，而不是只依靠扫光；工作结束时停止扫光。

若要降低界面某部分的动效，请在容器上设置 `arc:ArcMotion.Level="Reduced"`。该级别会被继承，因此其下的每个扫光都会停止。

### 示例：夜间导入面板

管理后台中显示夜间订单导入的面板。忙碌行说明任务正在做什么以及涵盖多少订单，人们可以仅凭文字判断还需等待多久。其下是较早一次运行的完成行，它是同一个控件，但 `IsActive` 设为 `false`，因此两种状态读起来像同一个组件。操作行中，查看日志使用次要按钮，下一步使用主按钮。

![夜间导入面板，一行忙碌状态与一行已完成状态](images/ImportPanel.png)

```xml
<Border Width="640"
        Padding="24"
        CornerRadius="{arc:ArcDesignTokenResource RadiusPanel}"
        Background="{arc:ArcDesignTokenResource Surface}"
        BorderBrush="{arc:ArcDesignTokenResource BorderSubtle}"
        BorderThickness="1">
    <StackPanel Spacing="16">
        <StackPanel Spacing="4">
            <TextBlock Text="Nightly order import"
                       FontSize="{arc:ArcDesignTokenResource TypeLG}"
                       FontWeight="SemiBold"
                       Foreground="{arc:ArcDesignTokenResource Foreground}" />
            <TextBlock Text="Northwind Commerce, store 014"
                       Foreground="{arc:ArcDesignTokenResource TextMuted}" />
        </StackPanel>
        <arc:TextShimmer Text="Importing 2,340 orders" FontSize="16" />
        <StackPanel Spacing="8">
            <TextBlock Text="Last run"
                       Foreground="{arc:ArcDesignTokenResource TextMuted}" />
            <arc:TextShimmer Text="Import complete on 20 October 2026" FontSize="16"
                             IsActive="False" />
        </StackPanel>
        <StackPanel Orientation="Horizontal" Spacing="8" HorizontalAlignment="Right">
            <arc:Button Content="View log" Variant="Secondary" />
            <arc:Button Content="Open orders" />
        </StackPanel>
    </StackPanel>
</Border>
```

### 文案规范

文字承载含义，因此应把它写成工作的状态，而不是情绪。

- **忙碌标签**：现在分词加上其对象或数量：“正在读取 14 个文件”“正在生成摘要”。数量使用阿拉伯数字，让人一眼读懂：写“2,340 个订单”，而不是“两千三百四十个订单”。
- **完成标签**：结果用简短的名词短语或过去分词表示：“摘要已就绪”“导入完成”。
- **长度**：让行文字保持在约 40 个字符以内，使其能放进状态栏，或放进窄窗口的标题中。
- **避免含糊的等待文案**：说明工作的内容，而不是等待本身。“请稍候”无法告诉人们正在发生什么。

| 不要写 | 改为 |
|---|---|
| 加载中... | 正在读取 14 个文件 |
| 请稍候 | 正在同步订单 |
| 处理中 | 正在导入 2,340 个订单 |
| 完成！ | 摘要已就绪 |
| 正在思考你的请求 | 思考中 |

### 最佳实践

**推荐**

- 把含义放在文字中。光带只表示工作仍在进行；减弱动效和辅助技术会去掉它，因此标签仍须说明工作的内容。
- 工作结束时将 `IsActive` 设为 `false`，而不是移除控件。文字会在原位稳定下来，周围的布局也不会移位。
- 每个区域保持一个忙碌标签，每个标签占一行。本控件为简短的状态行而设计，第二行扫光会与第一行竞争。
- 任务失败时用 `Alert` 显示失败原因并停止扫光。扫光意味着工作仍在进行。

**避免**

- 把扫光当作尚未载入内容的占位。应使用 `Skeleton`，它显示内容的形状，使人们看到将出现的内容。
- 工作完成后仍保持 `IsActive` 开启，或用在没有任何需要等待的标签上。此时扫光与页面内容相互矛盾。
- 以比人能阅读的速度更快的定时器更改文字。每次变化都会带着模糊升起，连续的变化读起来像噪音而不是进度。
- 设置与背景接近的 `BaseForeground`。当光带位于该行的其他位置时，静止的基础色仍须保持可读。
- 把扫光用在同时也是唯一错误状态消息的标签上。错误应使用 `Alert`，使消息不依赖动效。

### 常见问题

**标签滚出视野或其窗口隐藏时，扫光为什么会停止？** 控件仅在可见、已挂入树中、距视口 64 px 以内且处于 Full 动效级别时运行其循环。它不会为没有人能看到的标签消耗帧，并会从停止的位置继续。

**如何在不隐藏文字的情况下停止扫光？** 将 `IsActive` 设为 `false`。文字会变为实色并保持可见，这正是完成状态应有的样子。若因用户要求减少动效而需要为整个区域停止扫光，请在容器上设置 `arc:ArcMotion.Level="Reduced"`；扫光停止，标签以淡入方式切换。

**列宽较窄时文字会换行吗？** 不会。本控件只有一行。请缩短标签，或为控件留出更多空间；不要依赖换行。

**如何让扫光更慢或更改其颜色？** 用 `SweepDuration` 设置速度，用 `Foreground` 设置光带与实色文字的颜色，用 `BaseForeground` 设置静止时的颜色。`FontSize` 与 `FontWeight` 改变文字本身。

**控件会向屏幕阅读器播报其变化吗？** 不会。它的文字即为无障碍名称，扫光时项目状态为“Busy”。若要播报完成状态，请在父元素上放置实时区域，或在将 `IsActive` 设为 `false` 的同一步中播报消息。

## API 参考

<!-- api: TextShimmer -->

## 无障碍

- `Text` 的值即为控件的无障碍名称，控件以文本元素的身份报告自身。
- `IsActive` 为 `true` 时，项目状态为“Busy”。`IsActive` 为 `false` 时清除。并非所有辅助技术都会暴露项目状态，因此文字本身必须能传达含义。
- 扫光仅为装饰。不会逐字母朗读，光带也不是独立的元素。
- 控件不会播报变化。请按上文常见问题所述，在父元素上放置实时区域以播报完成状态。
- 控件不会切换为系统颜色以支持高对比度。请从主题中选择 `Foreground` 与 `BaseForeground`，使光带和静止的基础色都保持页面所需的对比度。
- 控件不接收焦点，也没有键盘交互，因此没有键盘表格。其中没有任何元素可以通过 `Tab` 到达。

## 动效

- 光带每个 `SweepDuration` 扫过文字一次，随后短暂停留，再开始下一次扫过。第一次扫过会在控件开始后稍作等待。
- 扫光仅在控件处于激活、可见、距视口 64 px 以内且处于 Full 动效级别时运行，并从停止的位置继续。
- 新标签从下方带着柔和的模糊升起，同时该行以弹簧方式调整到新宽度。被替换的标签以较短的模糊向上离开。
- 将 `IsActive` 设为 `false` 时，飞行中的光带滑出文字，随后文字变为实色。将其设回 `true` 时，该行回到静止的基础色。
- 减弱动效时停止扫光，标签以即时淡入切换。关闭动效时立即切换标签，并不加动画地确定状态。

| 变化 | 完整 | 减弱 | 关闭 |
|---|---|---|---|
| 扫光光带 | 每个 `SweepDuration` 扫过文字一次，然后停留 | 没有光带；文字显示其静止基础色 | 没有光带；文字显示其静止基础色 |
| 新的 `Text` 值 | 带着柔和模糊在 240 ms 内升起；旧文字在 160 ms 内离开 | 120 ms 内即时淡入 | 立即 |
| 文字变化后的宽度 | 在标签变化的同时以弹簧调整到新宽度 | 立即跳到新宽度 | 立即 |
| `IsActive` 设为 `true` | 文字在 240 ms 内回到静止的基础色 | 120 ms 内即时淡入 | 立即 |
| `IsActive` 设为 `false` | 光带最多在 420 ms 内滑出，随后文字在 360 ms 内变为实色 | 120 ms 内实色文字 | 立即 |

## 相关组件

- `Skeleton`：页面加载时的占位内容；`Progress`：工作量可以度量时使用。
- `TextMorph`：字母逐个变化的简短标签；`Alert`：需要处理的消息。

| 组件 | 适用场景 |
|---|---|
| `Skeleton` | 内容正在加载，页面需要与最终布局相符的占位形状 |
| `Progress` | 已完成的工作量已知，可以以进度条或数值显示 |
| `Stepper` | 任务有固定、有名称的步骤，人们需要看到自己所处的位置 |
| `TextMorph` | 简短标签改变状态，字母应变形为下一个标签 |
| `Alert` | 失败、会话过期或某个结果需要人们注意并给出下一步 |
| `TextReveal` | 标题或短段落首次进入页面 |
