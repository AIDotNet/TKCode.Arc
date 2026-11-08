---
title: 穿梭框
summary: 在两个列表之间移动项目：逐个、按选择批量或全部移动，支持全选和搜索，并直接修改绑定的集合。
source-hash: 14a94ece2111
---

<Demo name="Basic" />

## 何时使用

`TransferBox` 把两个列表并排放置：左边是可选的项目，右边是已选的项目，中间的按钮把选中的项目从一个列表移到另一个列表。两个列表始终可见，人们能同时看到已经选了什么、还剩什么，并且可以用常见的列表选择键一次选中多项。移动会直接修改两个绑定的集合，因此视图模型始终持有最新结果。

- **角色与权限分配**：管理后台为某个角色授予权限。可用权限在左侧，已授予的在右侧，行模板在每个名称下显示一行说明。
- **项目与团队成员**：项目设置页把人员分配到项目中。打开搜索后，管理者无需滚动就能在两百人的团队中找到 "Grace"。
- **表格的可见列**：在"自定义列"对话框中，把列在"隐藏"和"可见"之间移动。可见列集合就是表格读取的集合，因此对话框应用后移动立即生效。
- **资源分组**：运维工具把服务器或区域移入一个部署组，常常用 Shift 或 Ctrl+A 一次移动很多项。
- **从已知列表中选出一个子集的设置**：通知渠道、同步文件夹或启用的插件，完整列表很长但有限。

## 何时不用

- **几个能放在一行里的选项**：用 `ChipGroup`，它把每个选项显示为一个标签，不需要第二个列表。
- **在表单字段中选择子集**：用 `MultiSelect`，它把选择保留在紧凑的字段中，只在需要时打开列表。
- **每项都有自己标签和校验的"是/否"回答**：用多行 `Checkbox`。
- **已选项目的顺序很重要**：已选项目用 `ReorderableList`，因为穿梭框把移入的项目加在末尾，不会重新排序。
- **层级数据，例如文件夹或部门**：用带复选框的 `TreeView`；穿梭框只列出平铺的项目。

## 用法

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:TransferBox SourceHeader="Available languages"
                     TargetHeader="Selected languages"
                     SourceItems="{Binding AvailableLanguages}"
                     TargetItems="{Binding SelectedLanguages}"
                     Height="340" />
</UserControl>
```

`SourceItems` 是左侧列表，`TargetItems` 是右侧列表。在一个列表中选中行，然后点击指向另一侧的箭头按钮、按 Enter，或双击某一行。选中的项目离开一个集合，按它们在原列表中的顺序加到另一个集合的末尾。移动的是同一个对象引用，不会复制任何内容。

### 结构

1. **面板**：两个带 1 px 边框、控件圆角（`RadiusControl`，18 px）和静止阴影的表面。每个面板至少 180 px 宽（`PanelMinWidth`），两者平分剩余宽度。
2. **标题栏**：全选框、标题（`SourceHeader`、`TargetHeader`，中等字重）和计数。计数显示行数；有选择时显示"已选/行数"。
3. **搜索框**（`IsSearchable`）：标题栏下方的小号 `SearchField`，筛选所在列表的行。
4. **行**：至少 36 px 高，圆角 12 px，距面板边缘 6 px。悬停时填充柔和的表面色；选中的行使用浅强调色填充，并显示强强调色的对勾，因此状态不只依赖颜色。
5. **空状态**：列表没有行时居中显示的弱化文字（`SourceEmptyContent`、`TargetEmptyContent`），搜索文字隐藏了所有行时显示 `NoMatchesText`。
6. **移动按钮**：面板之间 36 px 的方形次要按钮。设置 `IsMoveAllVisible` 后，在它们的上方和下方出现"全部移动"按钮。没有可移动的内容时按钮处于禁用状态。

### 选择与移动

每个列表的选择方式与列表框相同：单击选中一行，Ctrl+单击添加或移除一行，Shift+单击选择一个范围，Ctrl+A 选中整个列表。标题栏中的全选框选中所有行；只选中部分行时显示短横线；所有行都已选中时再点击则清除选择。如果一次只应移动一项，请设置 `SelectionMode="Single"`，此时全选框会隐藏。

```xml
<arc:TransferBox SourceHeader="Hidden columns"
                 TargetHeader="Visible columns"
                 SelectionMode="Single"
                 IsSelectAllVisible="False" />
```

其他行移走时，留下的行保持选中状态，列表也保持滚动位置。

### 绑定到视图模型

穿梭框会修改绑定的集合，因此数据归视图模型所有，视图模型随时可以读取结果。每一侧都使用 `ObservableCollection<T>`（或任何会报告变化的可修改 `IList`）。视图模型所做的修改，例如加载项目或删除一项，会立即显示在列表中。

```csharp
public partial class LanguagesViewModel : ObservableObject
{
    public ObservableCollection<string> AvailableLanguages { get; } =
        ["C#", "C++", "Java", "JavaScript", "Python", "Rust"];

    public ObservableCollection<string> SelectedLanguages { get; } = ["Go", "TypeScript"];

    [RelayCommand]
    private Task SaveAsync() => _settings.SaveLanguagesAsync(SelectedLanguages.ToArray());
}
```

集合的规则：

- 两个集合都必须已设置、可修改（不是只读、不是固定大小），并且是不同的对象。否则移动按钮被禁用、命令不能执行，直接调用 `MoveToTarget` 会抛出说明原因的 `InvalidOperationException`。
- 移动按位置从后往前移除项目，因此列表中其他位置的相等值（两个 "A" 字符串，或相等的记录）不会被误取。
- 如果目标集合拒绝某个项目，例如把 `int` 加入字符串集合，穿梭框会撤回已经加入的项目，并在重新抛出异常之前把每个项目放回原位。移动不会让某个项目同时从两个列表中消失。
- 没有变更通知的 `List<T>` 也能使用：穿梭框在自己的移动之后刷新两个列表，但看不到别处对这种列表所做的修改。

### 对象、文本与模板

项目可以是任意对象。`ItemTextSelector` 给出每个项目的文本：默认行显示它，搜索筛选匹配它，屏幕阅读器朗读它。它是委托，因此不使用反射，也能在裁剪下工作。

```csharp
members.ItemTextSelector = item => ((TeamMember)item!).Name;
```

需要更丰富的行时设置 `ItemTemplate`，两个列表都会使用它。在模板上声明 `x:DataType` 让绑定被编译，并继续设置 `ItemTextSelector` 以支持搜索和无障碍。

<Demo name="ItemTemplate" />

### 搜索与全部移动

`IsSearchable` 为每个列表添加一个搜索框。文字在项目文本中任意位置匹配，忽略大小写，并可以通过 `SourceSearchText` 和 `TargetSearchText` 双向绑定。`IsMoveAllVisible` 添加"全部移动"按钮；它们移动列表当前显示的所有行，因此有搜索文字时只移动匹配的行。

<Demo name="Search" />

```xml
<arc:TransferBox SourceHeader="Team"
                 TargetHeader="Project members"
                 IsSearchable="True"
                 IsMoveAllVisible="True"
                 SearchPlaceholder="Search people"
                 SourceSearchText="{Binding MemberQuery}"
                 TargetEmptyContent="No one is on the project yet" />
```

### 命令、方法与事件

穿梭框把移动公开为命令，因此你自己的按钮、菜单项或快捷键都可以驱动它。命令的状态跟随选择和集合，绑定的按钮会自动启用和禁用。

| 命令 | 方法 | 移动 |
|---|---|---|
| `MoveToTargetCommand` | `MoveToTarget()` | 左侧列表中选中的行 |
| `MoveToSourceCommand` | `MoveToSource()` | 右侧列表中选中的行 |
| `MoveAllToTargetCommand` | `MoveAllToTarget()` | 左侧列表显示的所有行 |
| `MoveAllToSourceCommand` | `MoveAllToSource()` | 右侧列表显示的所有行 |

这些方法返回移动的项目数。`GetSelectedItems`、`SelectAll` 和 `ClearSelection` 读取和修改一侧的选择，`SourceSelectionCount`、`TargetSelectionCount`、`CanMoveToTarget` 和 `CanMoveToSource` 可以绑定。每次移动只触发一次 `ItemsTransferred`，带有方向 `Direction` 和移动的项目 `Items`，不论一次移动了多少项。

<Demo name="Commands" />

```csharp
box.ItemsTransferred += (_, e) =>
{
    var verb = e.Direction == TransferDirection.ToTarget ? "Granted" : "Revoked";
    _audit.Write($"{verb}: {string.Join(", ", e.Items)}");
};
```

### 数据结构与性能

每个列表都是虚拟化的列表框，只创建可见的行，因此包含数千项的列表也能流畅滚动。搜索文字每变化一次，就遍历一次整个集合。移动在原位修改集合，列表逐行更新，不会整体重建。

### 文案规范

- **标题**：按两侧所装的内容命名，使用句子式大小写的名词："Available permissions" 和 "Granted to editors"，而不是"左""右"或"源""目标"。
- **空状态**：说明空列表对当前任务意味着什么，例如 "No one is on the project yet"，而不只是"无数据"。
- **搜索占位文字**：说明搜索的是什么，例如 "Search people"。
- **你自己按钮的标签**：动词加对象，例如 "Show column" 或 "Grant permission"。

| 不要写 | 改写为 |
|---|---|
| Source | Available permissions |
| Target | Granted to editors |
| No data | No one is on the project yet |
| Search... | Search people |
| Move | Grant permission |

### 最佳实践

**推荐**

- 把两侧绑定到视图模型拥有的集合，并在视图模型中保存，而不是在控件中。
- 对象项目即使有 `ItemTemplate` 也设置 `ItemTextSelector`，让搜索和屏幕阅读器有文本可用。
- 列表可能装下超出面板高度的项目时，打开搜索。
- 给穿梭框一个高度，或让父级给它高度；列表在其中滚动。

**避免**

- 只读集合或数组：穿梭框无法在其中移动项目，按钮会保持禁用。
- 把两侧绑定到同一个集合。
- 只为两三个选项使用穿梭框；`ChipGroup` 或复选框展示起来更省力。
- 依赖已选项目的顺序；移入的项目加在末尾。

### 常见问题

**为什么移动按钮是禁用的？** 按钮需要它所在列表中有选择（"全部移动"需要有行），穿梭框必须处于启用状态，两个集合都必须已设置、可修改且不同。可以检查 `CanMoveToTarget` 和 `CanMoveToSource`。

**能否按排序位置插入移入的项目？** 穿梭框把它们加在末尾。如果顺序重要，请处理 `ItemsTransferred`，在视图模型中对集合排序。

**是否支持 `DisplayMemberPath`？** 不支持。字符串路径需要反射；文本用 `ItemTextSelector`，编译绑定用带 `x:DataType` 的 `ItemTemplate`。

**按住按钮能否连续移动？** 不能。每次按下只移动一次当前选择；需要移动很多项时用 Ctrl+A 或"全部移动"按钮。

## API 参考

<!-- api: TransferBox -->

## 无障碍

- 每个列表都是以标题文字（`SourceHeader`、`TargetHeader`）命名的列表框，每一行都是列表项，名称取自 `ItemTextSelector` 给出的文本或模板中的文字。
- 全选框是复选框，名称为 "Select all: " 加列表标题；只选中部分行时处于混合状态。
- 移动按钮的无障碍名称和工具提示来自 `MoveToTargetLabel`、`MoveToSourceLabel`、`MoveAllToTargetLabel` 和 `MoveAllToSourceLabel`；你的标题不是 "available" 和 "selected" 时请设置它们。
- 选中的行除了强调色填充还显示对勾，因此选择状态不依赖颜色。
- 通过键盘聚焦的行显示焦点环颜色的焦点环。

### 键盘

| 按键 | 操作 |
|---|---|
| `Tab` / `Shift+Tab` | 在全选框、搜索框、列表和移动按钮之间移动 |
| `Up` / `Down`、`Home` / `End`、`PageUp` / `PageDown` | 在列表的行之间移动 |
| `Shift` + 方向键 | 扩展选择 |
| `Ctrl+A` | 选中列表的所有行 |
| `Enter` | 把选中的行移到另一个列表 |
| 按钮上的 `Space` 或 `Enter` | 执行移动 |

## 动效

- 行的悬停和选中填充交叉淡变，行被选中时对勾淡入。
- 移动的行立即离开和加入列表，列表不会滑动。
- 按钮按下和松开时使用 Arc 按钮的弹簧。

| 变化 | 完整 | 减弱 | 关闭 |
|---|---|---|---|
| 悬停或选中填充 | 160 ms 淡变 | 120 ms 淡变 | 立即 |
| 选中的对勾 | 160 ms 淡变 | 立即 | 立即 |
| 行被移动 | 立即 | 立即 | 立即 |

## 响应式

- 两个面板平分穿梭框的宽度，每个至少 180 px，移动按钮位于两者之间。请把穿梭框放在至少约 440 px 宽的容器中；在更窄的屏幕上，改为每一步显示一个列表（一个列表加一个"选择"面板）。
- 除非设置了 `Height` 或父级拉伸它，穿梭框高 320 px；列表在面板内滚动。
- 过长的项目文字在行内以省略号截断。
- 没有操作依赖悬停：轻点选中一行，双击移动它。

## 相关组件

- 在表单字段中选择子集用 `MultiSelect`，同时展示少量选项用 `ChipGroup`。
- 已选项目的顺序重要时用 `ReorderableList`，层级中的选择用 `TreeView`。

| 组件 | 何时选择 |
|---|---|
| `MultiSelect` | 选中的值必须放进紧凑的字段 |
| `ChipGroup` | 少量选项能放在一两行内 |
| `Checkbox` | 表单中每个选项都是"是/否"回答 |
| `ReorderableList` | 人们要排列已选项目的顺序 |
| `TreeView` | 项目构成层级 |
