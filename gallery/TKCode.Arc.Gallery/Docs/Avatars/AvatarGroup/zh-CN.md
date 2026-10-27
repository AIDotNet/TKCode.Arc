---
title: 头像组
summary: 在较小的空间内展示团队或一组贡献者。
source-hash: 3bb01c375828
---

<Demo name="Team" />

## 何时使用

`AvatarGroup` 展示一小组拥有、编辑或审阅同一项内容的人员：页头、卡片或表格单元格中。头像会部分重叠，前面的几个人完整显示，其余人折叠进一个 "+N" 标签，因此无论团队有四人还是四十人，分组占用的空间都相同。它只用于展示；人员姓名通过无障碍名称或分组所概括的列表来读取。

- **共享文档或定价实验的编辑者**：页面头部显示当前正在处理该项内容的人，人员加入或离开会话时，头像堆叠随之变化。
- **拉取请求、发票或合同卡片中的审阅者**：前几位审阅者可见，"+3" 标示其余人，而不会让一个列表占满整张卡片。
- **支持工单或交易表格列中的经办人**：`Size="Small"` 让列保持窄窄的，同时每一行的负责人依然可见。
- **管理员编辑时保持打开的项目团队面板**：成员被添加或移除时，堆叠会滑动而不是跳动，无需刷新即可看到变化。
- **实时记录的关注者**，例如正在查看事件时间线的人：前几位关注者显示，计数涵盖其余的人。

## 何时不用

- **只有一个人**：用 `Avatar`，它承载单个人员的姓名与状态。
- **需要读取或比较每个人的姓名和角色的列表**：用 `Table` 或普通列表。
- **某位人员的账号操作**，例如登出或切换工作区：用 `UserMenu`。
- **从目录中选择多个人员**：用 `MultiSelect`，它支持搜索与选择。分组只展示人员。
- **每个人都需要自己的操作**，例如移除或发消息：用 `Table`，其中每一行都可以放置自己的控件。分组不可获得焦点，也没有针对个人的操作。

## 用法

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:AvatarGroup Label="编辑者">
        <arc:AvatarGroupMember DisplayName="Maya Chen" Status="Online" />
        <arc:AvatarGroupMember DisplayName="Noah Park" />
        <arc:AvatarGroupMember DisplayName="Leah Fox" Status="Offline" />
    </arc:AvatarGroup>
</UserControl>
```

`AvatarGroup` 派生自 Avalonia 的 `TemplatedControl`。它的模板是一个负责排布人员并绘制溢出标签的面板，因此除了悬停时的展开效果外，分组没有自己的焦点或指针行为。它的成员是 `AvatarGroupMember` 对象；`Avatar` 实例与普通字符串也可以接受。在设置 `ItemsSource` 之前，使用 `Items` 内容中的内联成员；设置之后，`ItemsSource` 将完全接管。

### 结构

1. **堆叠**（分组本身）：人员从左到右排列，相邻头像重叠 6 px。后面的人绘制在前面的人之上，因此每个头像的左边缘始终可见。
2. **人员槽位**：每个头像周围有一圈 1 px 的 `Border` 颜色环。头像本身有 2 px 的 `Surface` 边缘，将它与相邻头像分隔开。悬停时，环会加深为强边框颜色。
3. **头像**（`AvatarGroupMember`）：成员的照片或首字母，以及可选的状态圆点，尺寸与分组一致。圆点及其含义在 `Avatar` 中说明。
4. **溢出标签**：一个圆形，填色为 `SurfaceMuted`，文字为 `TextSecondary`，显示 "+N"。在 `Small` 尺寸下文字为 10 px，在 `Medium` 下为 Arc 的 XS 级，在 `Large` 下为 Arc 的 SM 级。
5. **名称胶囊**：悬停人员上方的胶囊，填色为前景色，文字为背景色，内边距水平 8 px、垂直 4 px。

分组的 `Label` 不会被绘制。它为辅助技术命名分组，并生成溢出标签的文字；当人们需要读懂分组是什么时，请在分组旁边放一个可见的标题。

### 绑定成员

将 `ItemsSource` 绑定到 `AvatarGroupMember`、`Avatar` 或普通名称的集合。该集合优先于内联的 `Items`。

```xml
<arc:AvatarGroup ItemsSource="{Binding Team}"
                 Label="团队成员" />
```

### 绑定到视图模型

使用 CommunityToolkit.Mvvm 时，集合以及改变它的命令归视图模型所有，分组只负责显示。`ItemsSource` 是单向的：分组从不编辑集合。`OverflowCount` 为只读，由集合与 `MaxVisibleCount` 计算得出。分组没有可供绑定的命令或点击事件；操作属于视图模型，或分组旁边的列表。

```csharp
public partial class ProjectViewModel : ObservableObject
{
    public ObservableCollection<AvatarGroupMember> Editors { get; } = new()
    {
        new("Maya Chen", status: AvatarStatus.Online),
        new("Noah Park"),
        new("Leah Fox", status: AvatarStatus.Offline),
    };

    [ObservableProperty]
    private int _maxVisibleCount = 4;

    [RelayCommand]
    private void AddEditor(string name) => Editors.Add(new AvatarGroupMember(name));

    [RelayCommand]
    private void RemoveEditor(AvatarGroupMember member) => Editors.Remove(member);
}
```

```xml
<arc:AvatarGroup ItemsSource="{Binding Editors}"
                 MaxVisibleCount="{Binding MaxVisibleCount}"
                 Label="编辑者" />
```

加入与离开会为槽位加入动画，列表是就地更新的，因此应对集合使用 `Add`、`Insert` 和 `Remove`。整体替换集合会让每个人都离开再重新加入。若要改变某人的显示方式，请设置现有 `AvatarGroupMember` 的 `Status`、`DisplayName` 或 `Source`；头像会随之变化，而不产生槽位动画。

```csharp
var team = new AvaloniaList<AvatarGroupMember>
{
    new("Maya Chen", status: AvatarStatus.Online),
    new("Noah Park"),
    new("Leah Fox", status: AvatarStatus.Offline),
};
group.ItemsSource = team;

// Joining and leaving animate the slot; the list is updated in place.
team.Insert(1, new AvatarGroupMember("Omar Haddad"));
team.RemoveAt(1);
```

### 溢出

`MaxVisibleCount`（默认为 4）决定在 "+N" 标签之前显示多少人。`OverflowCount` 报告折叠进标签的人数。列表中靠前的人会被显示，其余的被计数，因此请把最相关的人放在前面。只有可见的人会获得控件，因此长列表依然开销较小。

```xml
<arc:AvatarGroup ItemsSource="{Binding Team}"
                 MaxVisibleCount="3" />
```

标签的无障碍名称由溢出数量加上小写的 `Label` 组成，例如 "另外 2 位团队成员"。

### 尺寸

`Size` 作用于每个头像和溢出标签：`Small` 为 28 px，`Medium`（默认）为 36 px，`Large` 为 48 px。`XLarge` 按 `Large` 绘制，即主题所定义的最大分组尺寸。

<Demo name="SizesAndLimits" />

```xml
<arc:AvatarGroup ItemsSource="{Binding Team}" Size="Small" MaxVisibleCount="3" />
<arc:AvatarGroup ItemsSource="{Binding Team}" Size="Large" MaxVisibleCount="5" />
```

分组的宽度由成员与尺寸决定：6 px 的内边距加上每个可见槽位的宽度，再减去重叠部分。在 `Medium` 尺寸下，四个可见人员加一个标签时，分组宽 156 px；因此在选择 `Large` 之前，请先测量它将放入的单元格。

### 内联成员

成员可以直接书写，代替绑定的列表。这适用于固定的人员集合，例如静态卡片上的人员。

<Demo name="InlineMembers" />

### 数据结构与性能

- **条目**：每一项都是 `AvatarGroupMember`、`Avatar` 或普通字符串。空条目会被跳过，因此中间有空缺的列表计数依然正确。
- **可见的工作量**：只有前 `MaxVisibleCount` 个条目会获得头像，外加一个标签。一个有 500 名成员的分组与只有 5 名成员的分组，创建的控件数量相同。
- **身份标识**：条目是通过对象及其出现位置来跟踪的，而不是通过名称。两个同名的人是两个槽位，因此重名不会破坏动画。每个人保持一个对象。
- **更新**：实时更新需要一个会发出变更通知的集合，例如 `AvaloniaList` 或 `ObservableCollection`。分组只在挂载到可视树时才监听源，因此已分离的分组不会让列表继续存活。若使用普通列表，请为 `ItemsSource` 赋一个新集合来刷新。
- **动画**：加入与离开只在分组已加载且动效级别为完整时才会有动画。尚未加载的分组，或处于减弱动效的页面，会立即应用变化。

### 示例：带团队堆叠的项目卡片

SaaS 规划工具中的一张定价项目卡片列出了参与该实验的人员。标题与描述位于左侧，分组位于右侧，视线会在读完名称之后再落到团队上。共分配了六位编辑者，因此显示四个，标签为 "+2"。第一位编辑者在线，第五位离线，这让审阅者知道谁在线、可以回答问题。分组的 `Label` 为 "Editors"，因此溢出标签对辅助技术而言的名称为 "2 more editors"。

![项目卡片头部，六位编辑者，显示四个头像和一个 +2 标签](images/AvatarGroupHeader.png)

```xml
<Border Width="640"
        Padding="20"
        Background="{arc:ArcDesignTokenResource Surface}"
        BorderBrush="{arc:ArcDesignTokenResource Border}"
        BorderThickness="1"
        CornerRadius="12">
    <StackPanel Spacing="16">
        <Grid ColumnDefinitions="*,Auto">
            <StackPanel Spacing="4" VerticalAlignment="Center">
                <TextBlock Text="Q3 pricing experiment"
                           FontSize="16"
                           FontWeight="SemiBold"
                           Foreground="{arc:ArcDesignTokenResource Foreground}" />
                <TextBlock Text="Shared with the growth and finance teams"
                           FontSize="12"
                           Foreground="{arc:ArcDesignTokenResource TextMuted}" />
            </StackPanel>
            <arc:AvatarGroup Grid.Column="1" Label="Editors" VerticalAlignment="Center">
                <arc:AvatarGroupMember DisplayName="Maya Chen" Status="Online" />
                <arc:AvatarGroupMember DisplayName="Noah Park" />
                <arc:AvatarGroupMember DisplayName="Leah Fox" Status="Offline" />
                <arc:AvatarGroupMember DisplayName="Omar Haddad" />
                <arc:AvatarGroupMember DisplayName="Ines Duarte" />
                <arc:AvatarGroupMember DisplayName="Kenji Sato" />
            </arc:AvatarGroup>
        </Grid>
        <Border Height="1" Background="{arc:ArcDesignTokenResource Border}" />
        <Grid ColumnDefinitions="*,Auto">
            <TextBlock Text="Last edited by Maya Chen on 18 Oct 2026"
                       FontSize="12"
                       VerticalAlignment="Center"
                       Foreground="{arc:ArcDesignTokenResource TextMuted}" />
            <arc:Button Grid.Column="1" Content="Invite editor" Variant="Secondary" />
        </Grid>
    </StackPanel>
</Border>
```

### 文案规范

- **Label**：表示角色的复数名词，使用句首大写且不带冒号："Editors"、"Reviewers"、"Assignees"。Label 不会被绘制，但会被朗读，因此冒号会被读出来。
- **溢出短语**：标签依次读出数量、"more" 和小写的 Label。请选择在 "more" 之后读起来通顺的 Label："2 more reviewers" 很好；当分组只涉及一种角色时，"2 more people" 则含糊不清。
- **标题**：在分组旁边放一个可见的句首大写标题或一行短文字，例如 "Editing now"，因为分组本身不显示任何文字。

| 不要写 | 改为 |
|---|---|
| `Label="People"` | `Label="Reviewers"`，标签读作 "2 more reviewers" |
| `Label="Editors:"` | `Label="Editors"`，不带冒号 |
| `Label="Users"` | 当条目有负责人时用 `Label="Assignees"` |
| `Label="team"` | `Label="Team members"`，完整的名词短语在 "more" 之后读起来通顺 |

### 最佳实践

**推荐**

- 按相关程度排列成员，把最相关的人放在前面。溢出会折叠列表的尾部，因此排在末尾的人是读者最不可能看到的。
- 把 `Label` 设为表示角色的复数名词。Label 既是分组的无障碍名称，也是溢出短语的来源，一个词可以同时满足两者。
- 在分组旁边放一个可见的标题。Label 不会被绘制，因此看得见的读者需要在某处看到这些文字。
- 当在线状态变化时，修改现有 `AvatarGroupMember` 的 `Status`。头像会就地更新，人员保留其槽位，而不是离开并重新加入堆叠。

**避免**

- 每次刷新都整体替换集合。每次替换都会让每个人离开再加入，因此堆叠会让所有槽位同时做动画。
- 在触控布局中依赖悬停名称。触控不会展开堆叠，也不会显示名称，因此旁边的列表必须承载姓名。
- 在狭窄的单元格中放置 `Large` 尺寸的头像。在 `Large` 尺寸下每个人约增加 42 px，而分组既不换行也不收缩，因此单元格会溢出。
- 为了在狭小区域显示所有人而把 `MaxVisibleCount` 设得很高。堆叠每显示一人就增加一个槽位，因此宽度由人数决定，而不是由可用空间决定。

### 常见问题

**为什么触摸屏上不显示悬停名称？** 展开、突出的人员和名称胶囊只响应鼠标和触控笔指针。触摸输入按设计会被忽略，因此当触控很重要时，请在包含分组的列表或卡片中显示名称。

**如何显示所有人而不出现 "+N" 标签？** 把 `MaxVisibleCount` 设为成员数或更大的值。此时溢出数量为 0，不会创建标签。

**为什么列表的变化没有出现在分组中？** 分组只监听会发出变更通知的集合，例如 `AvaloniaList` 和 `ObservableCollection`，并且只在挂载到可视树时才监听。普通的 `List` 从不报告变化。请为 `ItemsSource` 赋一个新集合，或改用可观察集合。

**两个成员能否同名？** 可以。每个成员对象都是独立的槽位，分组跟踪的是对象而非名称。两人都会被绘制，并且都计入溢出数量。请为每个人分配各自的 `AvatarGroupMember`。

**如何让某个人可以点击？** 分组没有点击事件，也不取得焦点。请在它旁边放置作用于同一集合的列表、表格或按钮，分组只用于显示该项目上有哪些人。

## API 参考

<!-- api: AvatarGroup -->

<!-- api: AvatarGroupMember -->

## 无障碍

- 分组以 `Label` 命名。每个头像为辅助技术保留其自身的名称与状态。
- 溢出标签是一个以 `Label` 命名的图像，例如 "另外 2 位团队成员"。
- 堆叠没有键盘行为，也无法获得焦点。键盘操作请使用分组所概括的列表或表格。
- 悬停时显示的名称仅为视觉效果：每个人的名称也包含在头像的无障碍名称中。
- 触控输入不会展开堆叠或显示名称，因此当触控是受支持的输入方式时，包含分组的布局中也必须显示名称。

### 键盘

分组不取得焦点，也没有键盘命令，因此 `Tab` 会跳过它。人员可通过分组所概括的列表、表格或表单访问，在那里每个人都可以获得焦点并被操作。

## 动效

- 有人加入或离开时，其槽位会以变形弹簧伸缩，同时轻微淡入淡出并缩放，因此堆叠是滑动而非跳动。
- 溢出数量增加时向上滚动，减少时向下滚动。
- 指针悬停在堆叠上时，人员会从中心向外散开；指针下的人员会略微升起，显示光环与名称，相邻的人员则向两侧让开。
- 减弱动效时，移除悬停时的散开效果，保留光环与名称；数量以淡入淡出变化。关闭动效时，所有变化立即完成。

| 变化 | 完整 | 减弱 | 关闭 |
|---|---|---|---|
| 有人加入 | 其槽位在变形弹簧上展开，堆叠滑动 | 立即显示 | 立即 |
| 有人离开 | 其槽位在变形弹簧上收起，同时淡出并缩放 | 立即移除 | 立即 |
| 溢出数量变化 | 随变化向上或向下滚动 | 淡入淡出 | 立即 |
| 指针在堆叠上移动 | 人员每步散开 4 px，悬停者上升 2 px，相邻者移动 3 px | 不展开；保留光环与名称 | 立即 |
| 悬停人员上方的名称 | 延迟 60 ms 后显示 | 立即显示 | 立即 |

## 相关组件

- `Avatar`：单个人员。
- `UserMenu`：分组成员的头像需要打开账号操作时使用。
- `Table`：需要比较姓名、角色及其他列时使用。

| 组件 | 适用场景 |
|---|---|
| `Avatar` | 需要在姓名、评论或记录旁显示一个人 |
| `UserMenu` | 头像需要打开退出登录、个人资料或工作区操作 |
| `Table` | 姓名、角色及其他列需要被阅读和比较 |
| `MultiSelect` | 需要从目录中搜索并选择人员 |
| `Badge` | 标签是状态或类别，而非一组人员 |
| `ChatThread` | 这些人员是对话的作者 |
