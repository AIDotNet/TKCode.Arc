---
title: 骨架屏
summary: 内容仍在加载时预留空间。
source-hash: 8554de464f45
---

<Demo name="Profile" />

## 何时使用

`Skeleton` 在数据加载期间占住一块内容的位置。它的占位形状与所代表的内容一致：一个头像加若干文字行，因此数据到达时布局不会变化。用于屏幕中独立加载的区域，例如个人资料头部、评论列表或活动动态。内容会替换同一位置的占位，用户不会看到页面跳动。

- **个人资料与账号头部**：SaaS 管理后台中，用户卡片在账号记录加载时的样子。头像与两行文字标出姓名和角色将出现的位置。
- **评论与活动动态**：团队讨论串或审计日志在页面打开后再获取最新条目。五个占位条目展示动态的形状，又不暗示实际数量。
- **发票与订单详情**：电商或账单页面的订单摘要区域，而明细项与合计仍从另一项服务加载。预留空间可以避免合计在光标下移动。
- **数据稍后到达的仪表板卡片**：分析仪表板中的收入卡片或客户列表，卡片外框已经显示，只有内容区域在等待查询。
- **原位替换的内容**：任何最终高度已由设计确定的区域，从加载到加载完成只是填入预留的空间。

## 何时不用

- **有可度量进度的工作**：工作会报告已完成多少时用 `Progress`，例如已知行数的上传或导入。骨架屏表示内容即将到来，而不是进行到哪一步。
- **已加载完成但没有内容可显示**：用 `EmptyState`，它说明列表为空的原因，并给出下一步操作。
- **行内状态或 AI 思考提示**：用于表示系统正在工作的一行短文字时，用 `TextShimmer`，例如“正在整理这张发票”。骨架屏用于一块内容，而不是一句话。
- **等待服务器回复的小操作**：使用按钮的忙碌状态。骨架屏用于一块内容区域，单个按钮或字段不需要占位。
- **有自身形状的图表与媒体**：在你自己的占位中画出图表或图片的形状。`Skeleton` 只绘制文字行和头像，无法与曲线或缩略图匹配。

## 用法

`Content` 为 null 时，骨架屏显示占位。设置 `Lines` 指定文字行数，设置 `IsAvatarVisible` 可在文字前显示圆形头像占位。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:Skeleton IsAvatarVisible="True" Lines="3" />
</UserControl>
```

`Skeleton` 派生自 Avalonia 的 `ContentControl`，因此 `Content`、`ContentTemplate` 以及内容对齐属性的用法与任何内容控件相同。占位与内容共用同一个外框：占位淡出，内容在该外框内就位，因此加载前后控件是同一个元素。控件默认横向拉伸至其所在槽位的宽度，文字行占该宽度的一定比例，因此占位会随周围的布局变化。

### 结构

1. **头像占位**（`IsAvatarVisible`）：直径 44 px 的圆形，即中等控件尺寸，使用块颜色绘制。只有开启该属性时才显示。
2. **文字行**（`Lines`）：一到六条圆角条带，使用块颜色，高 12 px，条与条之间相隔 12 px。第一行高 16 px，宽为整体的 48%。最后一行宽为整体的 72%，中间的行为满宽。
3. **块颜色**：边框强调色与表面色的混合，使占位在两种主题下都是安静的中性色，且不依赖强调色。
4. **块上方的间距**：第一个块上方留 4 px，使占位不贴住所在槽位的上边缘。
5. **内容框架**（`Content`）：加载完成的内容。当 `IsLoading` 变为 false 时，内容上移 4 px 就位。之后控件采用内容的高度。

块的圆角是完全圆润的。每个块的圆角半径是控件圆角与高度一半两者中的较小值，因此 16 px 的第一行与 12 px 的行都保持圆润的两端。

### 匹配布局

将 `Lines` 与 `IsAvatarVisible` 设为与加载后内容一致的样子，而不是为了填满槽位。带姓名和角色的个人资料头部是一个头像加两行文字。一条评论是一个头像加三行文字。行数要匹配，加载时的高度变化才会小，内容到达时页面也不会移动。

`Lines` 的取值被限制在 1 到 6 之间。大于 6 的值会降为 6，小于 1 的值会升为 1。大多数卡片使用一到三行，长文本区块不超过六行。

### 评论占位

`Lines` 的取值范围为 1 到 6。第一行更短、更高，最后一行的宽度为整体的 72%。

<Demo name="Lines" />

### 已加载的内容

将 `Content` 设为真实视图，并把 `IsLoading` 绑定到加载状态。`IsLoading` 变为 false 时，占位淡出，内容上移就位，高度平滑变化。

<Demo name="IntoContent" />

```xml
<arc:Skeleton IsAvatarVisible="True"
              Lines="2"
              IsLoading="{Binding IsLoadingProfile}">
    <StackPanel Spacing="8">
        <TextBlock Text="{Binding UserName}" />
        <TextBlock Text="{Binding Bio}" TextWrapping="Wrap" />
    </StackPanel>
</arc:Skeleton>
```

`IsLoading` 默认为 true。设置了 `Content` 的骨架屏只有在 `IsLoading` 为 false 时才显示内容，因此设置了 `Content` 却忘记设置 `IsLoading` 的页面会一直显示占位。没有内容时，无论 `IsLoading` 为何值，占位始终显示。

### 绑定到视图模型

在 MVVM 应用中，视图模型掌握加载状态与数据。使用 CommunityToolkit.Mvvm 时，由 `ObservableObject` 持有标志与值，并由 `[RelayCommand]` 启动加载。

```csharp
public interface IProfileService
{
    Task<UserProfile> GetCurrentAsync();
}

public partial class ProfileViewModel : ObservableObject
{
    private readonly IProfileService _profiles;

    [ObservableProperty]
    private bool _isLoadingProfile = true;

    [ObservableProperty]
    private string _userName = string.Empty;

    [ObservableProperty]
    private string _bio = string.Empty;

    public ProfileViewModel(IProfileService profiles) => _profiles = profiles;

    [RelayCommand]
    private async Task LoadAsync()
    {
        IsLoadingProfile = true;
        var profile = await _profiles.GetCurrentAsync();
        UserName = profile.DisplayName;
        Bio = profile.Bio;
        IsLoadingProfile = false;
    }
}
```

```xml
<arc:Skeleton IsAvatarVisible="True"
              Lines="2"
              IsLoading="{Binding IsLoadingProfile, Mode=OneWay}">
    <StackPanel Spacing="8">
        <TextBlock Text="{Binding UserName}" />
        <TextBlock Text="{Binding Bio}" TextWrapping="Wrap" />
    </StackPanel>
</arc:Skeleton>
```

`IsLoading` 使用单向绑定。控件从不修改它，因此视图模型始终是状态的唯一持有者。只有在值赋完之后才把标志设为 false，内容就不会显示空白文字。加载失败时也应把标志设为 false，并在内容中显示错误提示，因为一直不结束的骨架屏不会告诉用户任何信息。

### 示例：下一条记录加载时的活动动态

账号页面显示最近的活动。第一条已经加载，第二条仍在等待作者与正文。占位让第二条保持与真实条目相同的高度，因此分隔线与下方的条目位置不变。动态周围的文案写明了工作区名称，因此即使其中一条仍在加载，页面看起来也是完整的。

![账号活动列表，上方一条已加载，下方一条为骨架占位](images/ActivityFeed.png)

```xml
<Border Width="680"
        Padding="28"
        CornerRadius="12"
        Background="{arc:ArcDesignTokenResource Surface}"
        BorderBrush="{arc:ArcDesignTokenResource Border}"
        BorderThickness="1">
    <StackPanel Spacing="20">
        <StackPanel Spacing="4">
            <TextBlock Text="Account activity"
                       FontSize="18"
                       FontWeight="SemiBold"
                       Foreground="{arc:ArcDesignTokenResource Foreground}" />
            <TextBlock Text="Latest changes in the Northwind workspace"
                       FontSize="13"
                       Foreground="{arc:ArcDesignTokenResource TextMuted}" />
        </StackPanel>
        <StackPanel Orientation="Horizontal" Spacing="12">
            <arc:Avatar DisplayName="Maya Chen" Size="Medium" />
            <StackPanel Spacing="2">
                <TextBlock Text="Maya Chen"
                           FontWeight="Medium"
                           Foreground="{arc:ArcDesignTokenResource Foreground}" />
                <TextBlock Text="Changed the billing address on inv_2026_0412"
                           FontSize="13"
                           Foreground="{arc:ArcDesignTokenResource TextSecondary}" />
            </StackPanel>
        </StackPanel>
        <Border Height="1" Background="{arc:ArcDesignTokenResource Border}" />
        <arc:Skeleton IsAvatarVisible="True" Lines="2" />
    </StackPanel>
</Border>
```

已加载的条目与占位使用相同的头像尺寸，因此两行对齐。占位有两行，因为一条真实的活动记录由一个名字和一句话组成。分隔线位于条目之间，而不在骨架屏内部，因此条目加载时分隔线保持不动。

### 文案规范

骨架屏在屏幕上没有自己的文字，因此它的文字就是无障碍名称和周围的文案。用它所加载的内容为加载区域命名，使用句子式大小写，并让周围的文案保持具体。不要把“正在加载”写进区域的可见标题，因为内容到达后标题仍会留在屏幕上。

| 不要写 | 改为 |
|---|---|
| `Loading content`（评论列表的默认名称） | `Loading comments` |
| 占位下方可见的 `Please wait...` | 不显示任何文字；由名称向辅助技术传达状态 |
| 以 `Loading` 作为区域标题 | 区域标题，例如 `Recent activity`，骨架屏位于其下方 |
| 只显示 `Error loading data` | `Could not load the billing history. Try again`，并附带重试按钮 |

`Label` 应简短而具体。“正在加载发票摘要”之类的标签能告诉屏幕阅读器哪个区域正在忙碌，而单独的“正在加载”做不到。

### 最佳实践

**推荐**

- 让 `Lines` 与 `IsAvatarVisible` 匹配加载后的内容，这样加载时的高度变化就小，页面不会跳动。
- 为每个独立的区域设置各自的骨架屏，使某个慢查询不会拖住页面的其余部分。
- 视图模型赋值完成之后再把 `IsLoading` 设为 false，内容就不会显示空白文字。
- 加载失败时在内容区域显示错误，否则占位会一直留在屏幕上，却没有任何解释。

**避免**

- 对有已知进度的工作使用骨架屏。能报告已完成比例的进度条比占位更有用，这类工作请使用 `Progress`。
- 同时渲染数百个骨架屏，因为每个块都要绘制和动画，长列表会因此变慢。只显示少量占位行，并让列表随数据到达逐步填满。
- 在无法绘制的形状上放置骨架屏，例如图表或缩略图，因为占位会承诺一种内容并不具备的布局。
- 当页面为一个已经完成的加载设置了 `Content`，却仍保留默认值 true 的 `IsLoading`，内容会一直隐藏在占位之后。

### 常见问题

**设置了 `Content` 之后内容为什么不显示？** `IsLoading` 默认为 true，而内容只在它为 false 时才显示。将 `IsLoading` 绑定到你的加载标志，或在数据就绪时将其设为 false。

**占位会在一段时间后停止吗？** 脉动约持续 20 秒，然后静止。占位本身会一直留在屏幕上，直到 `IsLoading` 变为 false 或 `Content` 将其替换。如果加载时间超过脉动的时长，请显示一条状态提示。

**可以完全不设置内容来使用 `Skeleton` 吗？** 可以。`Content` 为 null 时占位始终显示，`IsLoading` 设为 false 的控件也是如此。这适用于数据尚未接入时作为装饰的区域。

**如何为图表或图片绘制骨架屏？** `Skeleton` 只绘制文字行和头像。请在你自己的控件或普通的 `Border` 中构建图表或图片的形状，并以内容所用的同一种淡入方式显示它。

## API 参考

<!-- api: Skeleton -->

## 无障碍

- 占位显示时，`Label` 是无障碍名称。默认值为“正在加载内容”。
- 占位是礼貌模式的实时区域，状态朗读为“加载中”。
- 占位的形状不会被朗读。内容加载后，请为真实内容提供各自的标签。
- 控件不可获得焦点，也没有键盘交互。
- 显示内容时，名称与状态会被清除，因此加载后的内容会作为页面自身的文字被朗读。

### 键盘

`Skeleton` 不接收焦点，也没有键盘交互。加载完成时焦点不会移动，因此用户正在使用的字段或按钮在内容到达后仍保持焦点。

## 动效

- 文字行与头像以平缓的波浪从上到下脉动，波浪持续约 20 秒后停止。
- 加载结束时，占位淡出，内容上移 4 px 就位，高度以弹簧过渡到内容高度。
- 减弱动效时，脉动停止，占位以短暂淡入切换为内容，高度不做动画。

| 变化 | 完整 | 减弱 | 关闭 |
|---|---|---|---|
| 占位脉动 | 每个块依次脉动，比上方的块晚 90 ms，周期 1.8 s，共 11 次（约 20 s），然后静止 | 停止 | 停止 |
| 加载结束 | 占位快速淡出，内容上移 4 px 就位 | 占位与内容之间短暂淡入 | 立即 |
| 高度变化 | 高度以弹簧从占位过渡到内容 | 不做动画 | 立即 |

### 克制地使用动效

脉动是有限的波浪，因此长时间的加载会在约 20 秒后停止移动，占位仍留在屏幕上。不要在骨架屏旁边再添加一个会移动的元素，例如闪烁的横幅或旋转的指示器，否则页面会同时出现两种互相竞争的加载提示。加载时间超过波浪时长时，应显示一条状态提示，说明页面正在等待什么，占位可以保持原样。减弱动效无需额外代码：脉动停止，内容以淡入方式切换。

## 相关组件

- `Progress`：工作量已知并可报告时使用。
- `EmptyState`：加载完成且没有内容可显示时使用。
- `TextShimmer`：用于行内状态或 AI 思考状态。
- `Avatar`：占位所代表的已加载头像。

| 组件 | 适用场景 |
|---|---|
| `Progress` | 工作量已知，并可以以比例形式报告 |
| `EmptyState` | 加载已完成，且没有内容可显示 |
| `TextShimmer` | 一行短文字表示系统正在工作 |
| `Avatar` | 已加载的身份在占位所在的位置显示 |
