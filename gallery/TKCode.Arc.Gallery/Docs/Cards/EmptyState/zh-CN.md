---
title: 空状态
summary: 暂无内容时，给出清晰的下一步操作。
source-hash: 869a4eb7bc23
---

<Demo name="FirstUse" />

## 何时使用

`EmptyState` 用于填补列表、文件夹或结果集暂无内容时所在的位置。它说明该区域为空的原因，并给出一个合理的下一步操作，让用户不会停留在一块空白面板前。它是由图标、标题、一到两句说明和可选操作组成的居中区块，位于它所替代的内容所在的同一表面之内。

- **列表的首次使用**：管理后台或 SaaS 产品中，“暂无项目”配一个主要的“新建项目”按钮，显示在首个项目出现后项目表格将会出现的位置。
- **搜索或筛选没有结果**：订单、发票或客户列表中，“没有找到 ‘quarterly plan’ 的结果”配“清除搜索”和“浏览全部”操作，让用户无需重新输入即可恢复。
- **完成的步骤不再有可列出的内容**：例如每张发票都已付清后的付款页面，“所有发票均已付清”，说明中给出下一次计划付款日期，且不提供任何操作。
- **原位变化的状态**：团队页面在第一个邀请被接受之前显示“暂无成员”，之后在不切换页面的情况下变为“邀请已发送”并提供“重新发送邀请”操作。
- **空的收件箱或动态信息流**：开发者工具或 CRM 中，“过去 7 天没有部署”配一个指向部署历史的链接，让空白区域说明它所覆盖的时间范围。

## 何时不用

- **数据仍在加载**：用 `Skeleton`，它会保留内容的形状。请求进行中显示空状态，会传达错误的信息。
- **请求失败，或已有内容的页面出现错误**：用 `Alert`，放在内容上方，让已加载的数据保持可见。
- **需要包含媒体、所有者和底部操作的网格项**：用 `Card`。空状态不是一个卡片，也不应看起来像一个卡片。
- **确认某项内容已保存或已变更**：用 `Toast`，它不会替换页面上的内容。
- **带有标签页的大型多场景插画**：用 `EmptyStates` 区块，它是一组现成的场景。当一条消息只属于页面上的某一处时，才使用 `EmptyState`。

## 用法

`Title` 与 `Description` 承载消息，`Action` 添加行动号召。`Label` 为辅助技术命名该区域。默认图标是文件夹。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:EmptyState Label="项目"
                    Title="暂无项目"
                    Description="项目会把你的文件、成员和讨论集中保存在一处。">
        <arc:EmptyState.Action>
            <arc:Button Content="新建项目" Variant="Primary" />
        </arc:EmptyState.Action>
    </arc:EmptyState>
</UserControl>
```

`EmptyState` 派生自 Avalonia 的 `TemplatedControl`。它没有值、选中项或项目集合，因此不会引发任何事件。它唯一的交互部分是放在 `Action` 中的按钮，这些按钮保留各自的焦点、按下与命令行为。

### 结构

1. **图标区域**（由 `Icon` 提供内容）：48 px 见方，1 px 边框使用边框颜色，背景为弱化的表面色，圆角为面板圆角。图形为 24 px，以 1.5 的描边绘制，颜色为次要文字颜色。
2. **标题**（`Title`）：16 px、中等字重、行高 22.4 px（1.4 倍），使用前景色，居中显示。它是三级标题。位于图标区域下方 20 px。
3. **描述**（`Description`）：14 px、行高 19.6 px，使用次要文字颜色，居中显示。位于标题下方 8 px，宽度上限为 288 px（18 rem），因此会换行成较短的行长。
4. **操作**（`Action`）：居中排列的一行，位于描述下方 20 px。在 `Action` 有值之前处于隐藏状态。
5. **内边距**：左右各 20 px。上下内边距随窗口宽度变化：取窗口宽度的 8%，并保持在 32 px 至 48 px 之间。
6. **区域**：整个控件是一个区域地标，其无障碍名称为 `Label`。

标题与描述是平衡的：文案换行时，各行会被收窄为长度均匀的行，而不是留下一个很短的末行，因此两行的描述看起来也是居中的。

### 选择文案与图标

图标与文案说明区域为何为空。先确定状态，再编写文案，并选择能推动用户前进的操作。

| 状态 | 图标 | 标题 | 操作 |
|---|---|---|---|
| 首次使用，尚未有任何内容 | 默认文件夹，或该功能自己的图标 | “暂无项目” | 主要的创建操作 |
| 搜索或筛选没有返回结果 | `ArcIcons.Search` | “没有找到 ‘quarterly plan’ 的结果” | 清除筛选，或浏览全部 |
| 工作已完成，没有可处理的事项 | `ArcIcons.Check` | “所有发票均已付清” | 无，或指向历史记录的链接 |
| 变化已在原位成功完成 | `ArcIcons.Check` | “项目已创建” | 通常没有；移除原来的操作 |

默认文件夹适合尚无任何项目的列表。搜索图标则告诉用户列表确实存在，只是查询把它过滤掉了。每种状态使用一个图标，不要每次出现都换成不同的图标装饰区块。

### 图标

`Icon` 可接受一个控件，或一份 24 × 24 的 Lucide `Geometry`，例如 `ArcIcons` 中的某个图标。传入几何图形时，它会以 24 px、1.5 描边绘制，即 Arc 的外观。需要不同尺寸或组合图像时，传入控件。将 `Icon` 设为 `null` 可恢复为默认文件夹。

```xml
<arc:EmptyState Title="没有结果"
                Description="检查拼写，或尝试更宽泛的搜索词。"
                Icon="{Binding SearchIcon}" />
```

```csharp
empty.Icon = ArcIcons.Search;
```

图标变化时，旧图标会被新图标以弹出的方式替换。再次设置相同的图标时保持不动，因此视图模型可以在每次刷新时都为 `Icon` 赋值，而不会每次刷新都产生动画。

### 操作区

`Action` 可接受一个按钮，也可接受包含多个按钮的面板。该区域在描述下方居中排列。前进的方向使用一个主要操作。当状态还需要一个恢复操作时，把次要操作放在它旁边的 `StackPanel` 中，并让主要按钮排在前面，使它成为 Tab 顺序中的第一个停靠点。

<Demo name="ZeroResults" />

```xml
<arc:EmptyState Icon="{x:Static arc:ArcIcons.Search}"
                Title="“quarterly plan” 没有结果"
                Description="检查拼写，或尝试更宽泛的搜索词。">
    <arc:EmptyState.Action>
        <StackPanel Orientation="Horizontal" Spacing="12">
            <arc:Button Content="清除搜索" Variant="Secondary" />
            <arc:Button Content="浏览全部" Variant="Ghost" />
        </StackPanel>
    </arc:EmptyState.Action>
</arc:EmptyState>
```

每个按钮都应包含动词和对象，例如“清除搜索”或“新建项目”。只是重复标题（“项目”）的按钮对用户没有任何帮助。

### 由数据驱动

大多数空状态取决于数量、筛选条件或加载结果。把判断保留在视图模型中，让 `EmptyState` 显示与之匹配的状态。只有列表为空时才显示该控件，因此列表与空状态不会争夺同一块空间。

```csharp
public partial class ProjectsViewModel : ObservableObject
{
    public ObservableCollection<ProjectRow> Projects { get; } = new();

    [ObservableProperty]
    private string _searchText = string.Empty;

    public bool HasProjects => Projects.Count > 0;

    public string EmptyTitle => string.IsNullOrWhiteSpace(SearchText)
        ? "暂无项目"
        : $"没有找到 “{SearchText}” 的结果";

    public string EmptyDescription => string.IsNullOrWhiteSpace(SearchText)
        ? "项目会把你的文件、成员和讨论集中保存在一处。"
        : "检查拼写，或尝试更宽泛的搜索词。";

    [RelayCommand]
    private void ClearSearch() => SearchText = string.Empty;

    [RelayCommand]
    private async Task CreateProjectAsync()
    {
        await _projects.CreateAsync();
    }
}
```

```xml
<Grid>
    <ItemsControl ItemsSource="{Binding Projects}" IsVisible="{Binding HasProjects}" />
    <arc:EmptyState IsVisible="{Binding !HasProjects}"
                    Label="项目"
                    Title="{Binding EmptyTitle}"
                    Description="{Binding EmptyDescription}">
        <arc:EmptyState.Action>
            <StackPanel Orientation="Horizontal" Spacing="12">
                <arc:Button Content="新建项目" Variant="Primary"
                            Command="{Binding CreateProjectCommand}" />
                <arc:Button Content="清除搜索" Variant="Ghost"
                            Command="{Binding ClearSearchCommand}" />
            </StackPanel>
        </arc:EmptyState.Action>
    </arc:EmptyState>
</Grid>
```

- `Title`、`Description` 与 `Icon` 是单向的：由视图模型决定文案与图标，控件从不把它们写回。
- 在视图模型中计算文案，使每种状态的措辞集中在一处，并且可以测试。
- 只有在加载返回零项之后才显示 `EmptyState`。请求进行期间，应改为在列表位置显示 `Skeleton`。
- `Action` 接受一个对象，因此如上所示，在操作面板内部绑定按钮的命令。

### 状态切换

保持控件挂载，只修改它的属性。标题与描述会滚动切换为新文案，高度以弹簧方式调整以适应新内容。将 `Action` 设为 `null` 可移除按钮。`:has-action` 伪类跟随 `Action` 变化，因此样式可以在没有按钮时调整间距。

```csharp
empty.Icon = ArcIcons.Check;
empty.Title = "项目已创建";
empty.Description = "准备好后，邀请团队成员或开始添加文件吧。";
empty.Action = null;
```

修改已挂载控件的属性，比替换控件更好：图标区域与文案保持原位，变化看起来就是同一区域在更新。

### 布局与尺寸

该控件没有固定宽度。它会占满父级分配给它的空间，并使内容居中，因此同一个控件既可用于狭窄的侧边面板，也可用于宽阔的内容区域。它的文案被限制在 288 px 以内，因此在宽页面上较长的描述仍保持易读的行长。

将控件放在内容原本会占用的空间中。表格区域、列表区域和网格区域都适用，只要外围表面与内容具有相同的背景和边框。演示使用的是带边框、圆角为 26 px 的表面面板；在你的页面中使用相同的面板，使空状态看起来是它的一部分。

标题应保持简短。它与描述一样会换行并保持平衡，因此较长的标题不会破坏区块，但整句的标题更适合放在描述中。

### 示例：工作区中的空项目列表

管理后台中的项目页面，团队尚未创建第一个项目。表面与它所替代的列表一致，文件夹图标说明该区域的内容，标题点明状态，描述告诉用户项目能为他们带来什么，而唯一的主要操作推动用户前进。页面上没有任何其他元素与按钮争夺注意力。

![空项目列表：文件夹图标、标题、描述与一个主要操作](images/ProjectsEmpty.png)

```xml
<Border Width="680"
        CornerRadius="26"
        BorderThickness="1"
        Background="{arc:ArcDesignTokenResource Surface}"
        BorderBrush="{arc:ArcDesignTokenResource Border}">
    <arc:EmptyState Label="Projects"
                    Title="No projects yet"
                    Description="Projects keep your files, people and conversations in one place.">
        <arc:EmptyState.Action>
            <arc:Button Content="New project" Variant="Primary" />
        </arc:EmptyState.Action>
    </arc:EmptyState>
</Border>
```

### 文案规范

- **标题**：用句子式大小写简短说明状态，不加感叹号，例如“暂无项目”“没有找到 ‘invoice’ 的结果”“所有发票均已付清”。
- **描述**：一到两句，说明区域为空的原因以及用户可以做什么。有下一步时点明它；没有时说明列表何时会更新。数字使用阿拉伯数字书写（“过去 7 天”）。
- **操作**：动词加对象，例如“新建项目”“清除搜索”或“浏览全部”。使用一个主要操作；次要操作使用幽灵按钮或次要按钮。
- **语气**：平静、客观。不要道歉，不要用标点开玩笑或庆祝。

| 不要写 | 改为 |
|---|---|
| 哎呀，这里什么都没有！ | 暂无项目 |
| 抱歉，我们没有找到任何结果。 | 没有找到 ‘quarterly plan’ 的结果 |
| 空 | 过去 7 天没有部署 |
| 提交 | 创建项目 |

### 最佳实践

**推荐**

- 在标题或描述的第一句中说明区域为空的原因，让用户无需猜测页面是否出了故障。
- 当空状态来自筛选或搜索时，提供恢复操作，例如“清除搜索”，这是走出死胡同最快的方式。
- 每种状态保持一个图标和一个主要操作，使区块每次出现时都以相同的方式呈现。
- 只有在加载返回无数据之后才显示空状态。请求进行期间使用 `Skeleton`。

**避免**

- 把空状态用于错误。错误需要原因和重试，而空状态没有放置它们的位置；应在内容上方使用 `Alert`。
- 在操作行中放置两个同等重要的主要按钮。用户将不得不在两者之间选择，却得不到任何指引。
- 每个屏幕都使用装饰性插画。一个文件夹或搜索图形能让区块保持安静，重复的插画会让状态看起来像营销页面。
- 搜索之后仍显示通用的“暂无项目”。当筛选条件导致结果为空时，应说明这一点，让用户知道该修改筛选而不是去创建内容。

### 常见问题

**为什么默认图标看起来像文件夹？** 默认图标是 `ArcIcons.Folder`，以 24 px、1.5 描边绘制。它适合没有任何项目的列表这一常见情况。其他状态请把 `Icon` 设为特定的图形。

**当状态不再需要按钮时，如何移除它？** 将 `Action` 设为 `null`。操作行会被隐藏，`:has-action` 类也会被移除，因此区块会收缩回仅剩文案的状态。

**控件会接收键盘焦点吗？** 不会。`EmptyState` 不可获得焦点，因此 Tab 会跳过该区块，并前往 `Action` 中的按钮（如果有的话）。

**为什么图标在每次更新时都不产生动画？** 图标区域在控件首次出现时入场一次，之后保持不动。之后更换图标时只有新的图形弹出，再次设置相同的图标则不做任何事。

**可以修改内边距或宽度吗？** 内边距由主题设定：水平 20 px，垂直内边距在 32 px 至 48 px 之间，随窗口宽度变化。可以在控件或其父级上设置 `Width` 或 `MaxWidth` 来约束区块；描述已经有自己的 288 px 行长限制。

## API 参考

<!-- api: EmptyState -->

## 无障碍

- 该控件是一个由 `Label` 命名的区域地标。请为页面上的每个空状态设置 `Label`，使屏幕阅读器能够播报哪个列表为空。
- 标题是三级标题。描述紧随其后，位于阅读顺序中；操作按钮排在最后。
- 图标不向辅助技术公开，含义由文字承载，因此标题必须在没有图标的情况下也能说明状态。
- `Action` 中的按钮保留各自的焦点与键盘行为。
- `EmptyState` 本身不接收键盘焦点。Tab 会越过该区块，按它们出现的顺序移到 `Action` 中的按钮，按下 Enter 或空格即可触发获得焦点的按钮。

## 动效

- 新的标题或描述会带着柔和的模糊从下方升起，旧文字向上淡出。
- 文字区域以平滑弹簧改变高度，以适应新文案。
- 新的 `Icon` 会弹入；图标区域首次出现时，从 92% 的缩放淡入并放大。
- 减弱动效时，文字淡入切换，高度立即变化；图标区域不做入场动画。关闭动效时，所有变化立即生效。

| 变化 | 完整 | 减弱 | 关闭 |
|---|---|---|---|
| 标题或描述变化 | 文字带着柔和模糊升起，旧文字向上淡出 | 文字淡入 | 立即完成 |
| 文案变化高度 | 以弹簧方式调整至适应新文案 | 立即跳到新高度 | 立即完成 |
| 图标区域首次出现 | 从 92% 缩放并在 480 ms 内淡入 | 无入场动画 | 立即完成 |

## 相关组件

- `Skeleton`：内容仍在加载时使用。
- `Alert`：已显示内容的页面中出现错误或提示时使用。
- `Card`：需要包含媒体、所有者与操作的网格项时使用。
- `EmptyStates`（区块）：需要一组带标签页的现成空状态场景时使用。

| 组件 | 适用场景 |
|---|---|
| `Skeleton` | 内容正在加载，且其形状已知 |
| `Alert` | 有内容的页面中出现问题或需要关注 |
| `Toast` | 结果已确认，且页面上没有需要替换的内容 |
| `Card` | 该项是包含媒体、所有者与操作的磁贴 |
| `EmptyStates` 区块 | 需要带标签页、包含多个场景的插画 |
