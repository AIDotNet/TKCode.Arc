---
title: 变形选择
summary: 触发器展开为列表的选择器，带有滑动高亮和键入查找。
source-hash: 7d3f65226c37
---

## 何时使用

- 表单与属性面板中的固定列表，例如时区、负责人或项目。
- 带图标或简短元信息的分组选项，使用 `SelectItem.Group`、`Icon` 和 `Meta` 展示。
- 工具栏中的紧凑药丸形触发器，点击后展开为完整列表。

## 何时不用

- 自由文本，或来自远程数据源的长列表：用 `Combobox`。
- 需要选择多个值：用 `MultiSelect`。
- 执行操作而非选择值：用 `DropdownMenu`。

## 用法

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:MorphSelect Label="状态" SelectedIndex="1">
        <arc:SelectItem Content="待办" />
        <arc:SelectItem Content="进行中" />
        <arc:SelectItem Content="待审核" />
        <arc:SelectItem Content="已完成" />
        <arc:SelectItem Content="已取消" IsEnabled="False" />
    </arc:MorphSelect>
</UserControl>
```

### 分组、图标与元信息

为每个选项设置 `Group`、`Icon` 和 `Meta`。每组选项的上方会插入一个分组标题。`Icon` 接受 Lucide 几何图形或图片。`Meta` 以等宽数字样式显示。

```csharp
var zone = new MorphSelect
{
    Label = "时区",
    SelectedValue = "London",
    ItemsSource = cities.Select(c => new SelectOption(c.City, c.City)
    {
        Group = c.Region,
        Meta = c.Utc,
        Keywords = c.Zone,
        Icon = ArcIcons.Globe
    }).ToArray()
};
zone.SelectionChanged += (_, _) => Console.WriteLine(zone.SelectedValue);
```

### 紧凑工具栏

使用 `IsLabelVisible="False"` 隐藏标签。触发器仍以 `Label` 作为无障碍名称，因此药丸形状保持紧凑。

```xml
<arc:MorphSelect Label="状态" IsLabelVisible="False" />
<arc:MorphSelect Label="优先级" PanelAlignment="End" PanelWidth="220"
                 ItemsSource="{Binding Priorities}" />
```

### 搜索

`SearchMode` 决定菜单是否带有搜索字段。默认值 `Auto` 在选项超过八个时添加搜索字段。`On` 始终添加，`SearchPlaceholder` 设置其提示文字。在关闭状态的触发器上键入字母，会跳转到匹配的选项；菜单展开后由搜索字段接管输入。

```xml
<arc:MorphSelect Label="负责人"
                 SearchMode="On"
                 SearchPlaceholder="查找人员"
                 ItemsSource="{Binding Owners}" />
```

### 面板尺寸

`PanelWidth` 设置菜单宽度（默认 272）。`MaxListHeight` 限制其高度，超出时列表可滚动。

## API 参考

<!-- api: MorphSelect -->

<!-- api: SelectItem -->

## 无障碍

- `Label` 为触发器命名，即使 `IsLabelVisible` 为 `False` 也是如此。
- 触发器是带有列表框弹出层的组合框。菜单展开时会暴露展开状态，并播报当前高亮的选项。
- 方向键移动高亮并跳过禁用选项。Page Up 和 Page Down 每次移动八个选项。Home 和 End 分别移到第一个和最后一个选项。
- Enter 或 Space 选中，Alt 加 Up 选中并关闭，Escape 关闭菜单并将焦点返回触发器。
- 选项分组以其标题命名。搜索结果的数量会以礼貌方式播报。

## 动效

- 触发器展开为列表，其圆角从药丸形状变形为面板形状。
- 高亮在选项之间滑动。
- 选中的标签从列表飞回触发器，触发器宽度随之调整为新标签的宽度。
- 减弱动效时，尺寸直接跳转，并省去飞行与滚动，仅保留短暂淡入。关闭动效时直接切换。

## 相关组件

- `Select`：带固定列表的普通标签选择字段。
- `Combobox`：用户键入后可搜索的单选。
- `MultiSelect`：需要选择多个值时使用。
- `DropdownMenu`：菜单用于执行操作时使用。
