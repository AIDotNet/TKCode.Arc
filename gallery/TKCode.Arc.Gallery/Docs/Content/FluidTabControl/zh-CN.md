---
title: 流式标签页
summary: 标签列表旁是一个滚动区域，按顺序容纳每个标签的内容。选中标签会滚动到对应分区，滚动内容会选中视图中分区对应的标签。
source-hash: 3f88e2411322
---

<Demo name="Default" />

## 何时使用

`FluidTabControl` 在一个滚动区域旁放置标签列表，滚动区域按顺序容纳每个标签的内容，一个分区接一个分区。选择标签会平滑滚动到它的分区；滚动内容时，到达滚动区域顶部（或中线）的分区所对应的标签会被选中。内容不会被替换，所以人既可以一路读下去，也可以在分区之间跳转。这种模式也叫滚动联动标签页或 scroll spy。

- **设置页面**：通用、外观、账户、通知、隐私与高级。只想找一个开关的人选择对应标签；想全部检查一遍的人从上到下滚动一次即可。
- **分章节的长表单**：包含公司、联系人、账单与文件的客户申请。标签列表显示人进行到哪里，所有字段都在同一页，在各部分之间切换时不会丢失内容。
- **文档或手册**：章节加上一个跟随阅读位置的目录。
- **属性编辑器**：选中对象的属性按布局、外观与行为分组，各组依次排列，而不是藏在不同的标签后面。

## 何时不用

- **不应一起阅读的视图**，例如一条记录的概览、动态与设置：使用 `Tabs`，它一次只显示一个面板。
- **人每次打开一个或几个的少量分区**：使用 `Accordion`。
- **必须按顺序完成的步骤**：使用 `Stepper`。流式标签页允许跳到任何分区。
- **同类条目的长列表**，例如消息或搜索结果：使用自带滚动的列表。流式标签页的每个分区都会预先创建，适合几十个分区，而不是成千上万行。
- **应用页面之间的导航**：使用侧边栏。流式标签页只在一个页面内移动。

## 用法

项是 `FluidTabItem` 容器。`Header`（以及可选的 `Icon`）是标签，`Content` 是该标签的分区。给控件一个高度，或者把它放在高度受限的位置（网格行、DockPanel 的填充区）：分区在控件内部滚动。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:FluidTabControl Height="360" AutomationProperties.Name="Settings">
        <arc:FluidTabItem Header="General">
            <StackPanel Spacing="12">
                <TextBlock Text="General" FontSize="18" FontWeight="SemiBold" />
                <arc:Switch Content="Open at login" IsChecked="True" />
                <arc:Switch Content="Check for updates automatically" />
            </StackPanel>
        </arc:FluidTabItem>
        <arc:FluidTabItem Header="Appearance">
            <StackPanel Spacing="12">
                <TextBlock Text="Appearance" FontSize="18" FontWeight="SemiBold" />
                <arc:Switch Content="Follow the system theme" IsChecked="True" />
            </StackPanel>
        </arc:FluidTabItem>
        <arc:FluidTabItem Header="Account">
            <arc:Input Label="Email" Text="noor@larkspur.co" />
        </arc:FluidTabItem>
    </arc:FluidTabControl>
</UserControl>
```

`FluidTabControl` 派生自 Avalonia 的 `SelectingItemsControl`，所以 `SelectedIndex`、`SelectedItem` 与 `SelectionChanged` 的用法和任何可选择列表一样，两个属性都支持双向绑定。控件加载时选中第一个标签。无论以何种方式选中标签（点击、键盘、绑定、代码），它的分区都会滚动到位；滚动内容会改变选中项，但不会再把内容滚回去。

`FluidTabItem` 派生自 `HeaderedContentControl`。标签列表显示它的 `Header` 与 `Icon`；它的 `Content` 显示在滚动区域中，所以其中的控件在人滚开再滚回来时保持状态。禁用的标签会被方向键跳过且不能被选中；隐藏的标签（`IsVisible="False"`）也会隐藏它的分区。

### 结构

1. **标签列表**：宽 200 px（`TabListWidth`），内边距 8 px，在分区一侧有一条细边框色的 1 px 分隔线。标签放不下时它独立滚动。
2. **标签**（`FluidTabItem`）：至少 36 px 高，内边距 14 px × 8 px，圆角 12 px。文字使用小号字、中等字重与弱化文字色。悬停时以弱化表面色填充；选中的标签使用强调色（强），禁用的标签降到 50% 不透明度。
3. **图标**：在标题之前，与标题相距 10 px。可以是任何内容：字形、文字或模板的结果。
4. **选中高亮**：选中标签背后的淡强调色填充，在标签之间滑动。
5. **侧边指示器**：选中标签外侧边缘的 3 px 强调色竖条，上下各内缩 9 px。标签列表在右侧时它移到右边缘。
6. **滚动区域**：按项的顺序排列所有分区，间距 40 px（`SectionSpacing`），内边距 24 px（`ContentPadding`）。
7. **焦点环**：聚焦标签周围的 2 px 环。

### 位置与锚点

`TabPlacement` 把标签列表放在 `Left`（默认）或 `Right`。`SnapAlignment` 决定以滚动区域的哪条线为准：`Top`（默认）时，选中的分区滚动到顶部，位于顶部的分区就是当前分区；`Center` 时，选中的分区滚动到中间，跨过中线的分区就是当前分区。两者都可以随时切换，不会丢失选中项与滚动位置。

<Demo name="Placement" />

设置、表单与文档用 `Top`，因为每个分区以人首先阅读的标题开头。分区比滚动区域短、且作为整体阅读时（例如卡片或步骤）用 `Center`，让当前分区位于中间，前后的分区围在它周围。

两端的规则很简单：滚动到最顶部时选中第一个标签；滚动到最底部时选中最后一个标签，即使最后几个分区太短、无法到达锚线。无法到达锚线的分区会尽量滚动，它的标签保持选中。

### 数据驱动的标签

设置 `ItemsSource`。`ItemTemplate` 渲染标签，`IconTemplate` 渲染标签的图标，`ContentTemplate` 渲染分区；三者接收同一个数据项，所以标签和它的分区显示同一个对象。双向绑定 `SelectedItem`（或 `SelectedIndex`），即可在视图模型中保存当前分区。

<Demo name="DataBound" />

```csharp
public sealed record HandbookChapter(string Number, string Title, string Subtitle, string Body);

public partial class HandbookViewModel : ObservableObject
{
    public ObservableCollection<HandbookChapter> Chapters { get; } = new();

    [ObservableProperty]
    private HandbookChapter? _currentChapter;
}
```

```xml
<arc:FluidTabControl ItemsSource="{Binding Chapters}"
                     SelectedItem="{Binding CurrentChapter, Mode=TwoWay}"
                     AutomationProperties.Name="Handbook chapters">
    <arc:FluidTabControl.ItemTemplate>
        <DataTemplate>
            <TextBlock Text="{Binding Title}" />
        </DataTemplate>
    </arc:FluidTabControl.ItemTemplate>
    <arc:FluidTabControl.ContentTemplate>
        <DataTemplate>
            <TextBlock Text="{Binding Body}" TextWrapping="Wrap" />
        </DataTemplate>
    </arc:FluidTabControl.ContentTemplate>
</arc:FluidTabControl>
```

模板是强类型的（`x:DataType`）并使用编译绑定，所以数据驱动的标签兼容裁剪与 Native AOT；没有需要通过反射读取的成员路径。本身是 `FluidTabItem` 的数据项会被直接使用。

### 平滑滚动

选中标签时，内容在 `AnimationDuration`（默认 480 ms）内按 `EasingFunction`（默认 Arc 的 in-out 曲线）滚动到它的分区。时长为零、`IsAnimationEnabled="False"` 或减少动效时，直接跳到分区。上面的示例使用了 `AnimationDuration="0:0:0.6"` 与 `EasingFunction="CubicEaseOut"`。

滚动过程中经过的标签不会被选中，选中项直接移到被选的标签。滚动中再选另一个标签，会从当前位置继续滚向新的分区。用滚轮、触控板、滚动条或键盘滚动会立即停止平滑滚动，把控制权交还给人。滚动中某个分区的高度变化（图片加载、分组展开）时，滚动会跟随它的新位置。

```xml
<arc:FluidTabControl AnimationDuration="0:0:0.3"
                     EasingFunction="QuadraticEaseOut"
                     IsAnimationEnabled="True" />
```

### 添加与删除标签

分区始终与项一致。在 `ObservableCollection` 或 `Items` 中添加、删除、替换或移动项，滚动区域都会随之更新；清空项会清空分区。删除选中的标签时，会选中另一个标签并把它的分区滚动到位。修改 `FluidTabItem` 的 `Content` 会替换它的分区，隐藏该项会隐藏它的分区。

<Demo name="DynamicItems" />

### 在代码中选择

```csharp
settings.SelectTab(3);
settings.SelectionChanged += (_, _) =>
{
    status.Text = $"Reading: {settings.SelectedItem}";
};
```

`SelectTab` 在索引处的标签存在且启用时选中它，否则返回 `false`。对已选中的标签调用它，会把它的分区重新滚动到位，适合人稍微滚开之后使用。设置 `SelectedIndex` 或 `SelectedItem` 也会同样滚动，但不检查标签是否启用。

### 自定义标签与图标

`Header` 与 `Icon` 接受任何内容。在 `FluidTabItem` 上使用 `HeaderTemplate` 与 `IconTemplate`，或对数据项在控件上使用 `ItemTemplate` 与 `IconTemplate`。带图标的标签有 `:has-icon` 伪类；控件按位置设置 `:left` 或 `:right`，样式可以据此为每一侧调整标签。

```xml
<arc:FluidTabItem Header="Billing">
    <arc:FluidTabItem.Icon>
        <arc:ArcGlyph Data="{x:Static arc:ArcIcons.CreditCard}" Size="16" StrokeThickness="1.8" />
    </arc:FluidTabItem.Icon>
    <arc:FluidTabItem.HeaderTemplate>
        <DataTemplate>
            <StackPanel Spacing="2">
                <TextBlock Text="{Binding}" />
                <TextBlock Text="Plan and invoices" FontSize="12" FontWeight="Normal" />
            </StackPanel>
        </DataTemplate>
    </arc:FluidTabItem.HeaderTemplate>
    <TextBlock Text="Business plan, renews November 30, 2026." />
</arc:FluidTabItem>
```

### 数据结构与性能

每个分区在控件加载时创建并一直存在，这样人才能滚动浏览全部分区。这适合最多几十个分区的设置页与文档。如果一个分区里有很长的列表，在分区中放一个有固定高度的虚拟化列表，或改用列表控件。

控件只在需要时读取分区位置（选中、滚动、平滑滚动中的布局变化），其他时候不订阅布局事件。分区高度可以任意，也可以随时变化。

### 示例：账户设置

![左侧是标签列表、右侧依次排列通用与外观分区的设置页](images/AccountSettings.png)

```xml
<Border Width="720"
        Height="340"
        BorderThickness="1"
        BorderBrush="{arc:ArcDesignTokenResource Border}"
        CornerRadius="{arc:ArcDesignTokenResource RadiusPanel}"
        Background="{arc:ArcDesignTokenResource Surface}"
        ClipToBounds="True">
    <arc:FluidTabControl AutomationProperties.Name="Settings">
        <arc:FluidTabItem Header="General">
            <arc:FluidTabItem.Icon>
                <arc:ArcGlyph Data="{x:Static arc:ArcIcons.Settings}" Size="16" StrokeThickness="1.8" />
            </arc:FluidTabItem.Icon>
            <StackPanel Spacing="12">
                <TextBlock Text="General" FontSize="18" FontWeight="SemiBold" />
                <TextBlock Text="Startup, language and updates." FontSize="14"
                           Foreground="{arc:ArcDesignTokenResource TextMuted}" />
                <arc:Switch Content="Open at login" IsChecked="True" />
                <arc:Switch Content="Check for updates automatically" IsChecked="True" />
            </StackPanel>
        </arc:FluidTabItem>
        <arc:FluidTabItem Header="Appearance">
            <arc:FluidTabItem.Icon>
                <arc:ArcGlyph Data="{x:Static arc:ArcIcons.Palette}" Size="16" StrokeThickness="1.8" />
            </arc:FluidTabItem.Icon>
            <StackPanel Spacing="12">
                <TextBlock Text="Appearance" FontSize="18" FontWeight="SemiBold" />
                <TextBlock Text="Theme, density and motion." FontSize="14"
                           Foreground="{arc:ArcDesignTokenResource TextMuted}" />
                <arc:Switch Content="Follow the system theme" IsChecked="True" />
                <arc:Switch Content="Compact rows" />
            </StackPanel>
        </arc:FluidTabItem>
        <arc:FluidTabItem Header="Account">
            <arc:FluidTabItem.Icon>
                <arc:ArcGlyph Data="{x:Static arc:ArcIcons.User}" Size="16" StrokeThickness="1.8" />
            </arc:FluidTabItem.Icon>
            <arc:Input Label="Email" Text="noor@larkspur.co" />
        </arc:FluidTabItem>
        <arc:FluidTabItem Header="Privacy">
            <arc:FluidTabItem.Icon>
                <arc:ArcGlyph Data="{x:Static arc:ArcIcons.Shield}" Size="16" StrokeThickness="1.8" />
            </arc:FluidTabItem.Icon>
            <arc:Switch Content="Send crash reports" IsChecked="True" />
        </arc:FluidTabItem>
    </arc:FluidTabControl>
</Border>
```

### 文案规范

- **标签文字**：一两个词，用名词命名分区：“通知”“账单”。
- **分区标题**：每个分区以重复标签文字的标题开头，滚动到这里的人不用看列表也知道自己在哪。
- **顺序**：最常访问的分区放在前面，危险的分区（删除账户）放在最后。

| 不要写 | 改写为 |
|---|---|
| 标签“其他” | 高级 |
| 标签“通知设置相关内容” | 通知 |
| 分区标题“设置组 3” | 隐私 |

### 最佳实践

**推荐**

- 每个分区都有一个与标签文字一致的标题。
- 限制控件高度，让分区在控件内部滚动，而不是让整个页面变长。
- 用 `AutomationProperties.Name` 为标签列表命名。
- 让标签数量少到可以一眼扫完，大约四到十个。

**避免**

- 在分区中嵌套同方向、但没有自身高度的滚动区域；内层会抢走滚轮，选中项就不再跟随。
- 用它展示不应一起阅读的视图；请用 `Tabs`。
- 末尾有很多永远到不了锚线的短分区；这种情况优先用 `Center`，或用 `ContentPadding` 增加底部内边距。

### 常见问题

**为什么最后一个分区还没到顶部，最后一个标签就被选中了？** 滚动到最底部时总是选中最后一个标签，因为较短的最后分区永远无法到达顶部那条线。

**平滑滚动进行时人可以滚动吗？** 可以。滚动区域中的任何滚轮、触控板、滚动条或按键输入都会停止平滑滚动，选中项重新跟随人的滚动。

**来自视图模型的选中会让滚动跳变吗？** 不会；它和点击一样平滑滚动。若希望直接跳转，设置 `IsAnimationEnabled="False"`。

## API 参考

<!-- api: FluidTabControl -->

<!-- api: FluidTabItem -->

## 无障碍

- 标签列表的角色是 Tab，每个 `FluidTabItem` 的角色是 TabItem 并带有选中状态。用 `AutomationProperties.Name` 为控件命名。
- 标题是字符串时，每个标签以标题命名。其他标题请显式命名。
- 只有选中的标签在 Tab 键顺序中。Tab 键从标签列表进入分区，分区中的每个控件都能按顺序到达。
- 在分区内按下的键属于分区中的控件；只有标签获得焦点时，方向键才会切换标签。

### 键盘

| 按键 | 操作 |
|---|---|
| `Tab` / `Shift+Tab` | 在选中的标签上进入标签列表；从列表进入分区 |
| `Up` / `Down` | 选中上一个或下一个启用的标签并滚动到它的分区；到两端时停止 |
| `Home` / `End` | 选中第一个或最后一个启用的标签并滚动到它的分区 |
| `Enter` / `Space` | 再次滚动到聚焦标签的分区 |
| 指针按下 | 左键按下时选中标签；触摸在抬起时选中 |

## 动效

- 选中标签时，内容在 `AnimationDuration` 内按 `EasingFunction` 滚动到它的分区。
- 选中高亮与侧边指示器以 morph 弹簧滑到选中的标签。
- 标签列表以 smooth 弹簧把选中的标签滚动到可见位置。
- 减少动效时直接跳到分区，高亮立即移动；关闭动效时同样如此。

| 变化 | 完整 | 减少 | 关闭 |
|---|---|---|---|
| 选中一个标签 | 内容在 480 ms 内以 in-out 曲线滚动到它的分区 | 跳转 | 跳转 |
| 选中项移动 | 高亮与指示器以 morph 弹簧滑动 | 立即移动 | 立即移动 |
| 选中的标签在列表外 | 列表以 smooth 弹簧滚动 | 跳转 | 跳转 |
| 悬停与选中的标签 | 文字与填充颜色交叉淡化 | 立即 | 立即 |
| 人滚动内容 | 选中项立即跟随，没有动画 | 相同 | 相同 |

## 响应式

- `FluidTabControl` 没有断点。标签列表保持 `TabListWidth`，分区占据其余宽度，所以在窄屏上请减小 `TabListWidth` 或隐藏图标。
- 在手机尺寸的视图中，给标签列表约 120 px，或改用位于内容上方、不需要侧栏的 `Tabs`。
- 标签比控件高时，标签列表独立滚动，并让选中的标签保持可见。
- 标签至少 36 px 高。轻触与点击一样选中标签，在标签列表上拖动会滚动它。

## 相关组件

- `Tabs`：一次显示一个的并列视图。
- `Accordion`：在原位展开的分区。

| 组件 | 何时选择 |
|---|---|
| `Tabs` | 每个视图替换其他视图，一起阅读没有意义 |
| `Accordion` | 人每次打开一个或几个分区 |
| `Stepper` | 分区必须按顺序完成 |
| `ScrollArea` | 只需要滚动区域，不需要标签列表 |
