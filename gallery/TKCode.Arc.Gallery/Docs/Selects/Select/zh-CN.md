---
title: 下拉选择
summary: 紧凑的选择字段，菜单支持键盘操作。
source-hash: 8f3c194c2ee3
---

## 何时使用

- 列表简短且固定，例如地区、排序方式或套餐，用户只需选择一个值，无需输入。
- 表单中带标签的字段，触发器下方可选择性显示说明文字。
- 列表足够短，便于浏览。菜单可以滚动，但较长的列表更适合搜索。

## 何时不用

- 较长或需要搜索的列表：用 `Combobox`。
- 需要一次选择多个值：用 `MultiSelect`。
- 只有两到四个选项且需要始终可见：用 `SegmentedControl`。

## 用法

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:Select Label="地区"
                Description="工作区数据的存储位置。"
                Width="280">
        <arc:SelectItem Content="北美" Value="na" />
        <arc:SelectItem Content="欧洲" Value="eu" />
        <arc:SelectItem Content="南美（即将推出）" Value="sa" IsEnabled="False" />
        <arc:SelectItem Content="非洲" Value="af" />
    </arc:Select>
</UserControl>
```

### 绑定选项与值

将 `ItemsSource` 设为任意对象或 `SelectOption` 记录。`SelectedValue` 为双向绑定，所选值会写入绑定的属性。

```xml
<arc:Select Label="排序方式"
            Width="220"
            ItemsSource="{Binding SortOrders}"
            SelectedValue="{Binding SortKey, Mode=TwoWay}" />
```

```csharp
var sort = new Select
{
    Label = "排序方式",
    Width = 220,
    ItemsSource = new[]
    {
        new SelectOption("newest", "最新优先"),
        new SelectOption("oldest", "最早优先"),
        new SelectOption("az", "名称，A 到 Z"),
    },
    SelectedValue = "newest"
};
sort.SelectionChanged += (_, _) => Console.WriteLine(sort.SelectedValue);
```

### 自定义标签与值

对于不是 `SelectOption` 的数据项，设置 `TextSelector` 和 `ValueSelector`。与 `DisplayMemberBinding` 不同，它们对 AOT 友好。

```csharp
var zones = new Select
{
    Label = "时区",
    Placeholder = "选择时区",
    Width = 300,
    ItemsSource = cities,
    TextSelector = item => item is City c ? $"{c.Name} ({c.Offset})" : null,
    ValueSelector = item => item is City c ? c.Id : null
};
```

### 占位文字与说明文字

未选择任何项时显示 `Placeholder`。`Description` 是触发器下方的弱化提示，同时也是无障碍的辅助文字。

```xml
<arc:Select Label="套餐" Placeholder="选择套餐" Description="之后可以随时更改。" />
```

### 长列表与禁用状态

菜单的高度上限为 `MaxDropDownHeight`（默认 320），超出部分可滚动。只绘制可见行，因此包含数千项的列表仍能保持响应。禁用的 `Select` 会忽略输入。

```xml
<arc:Select Label="构建编号" ItemsSource="{Binding Builds}" MaxDropDownHeight="240" />
<arc:Select Label="套餐" IsEnabled="False" SelectedIndex="1">
    <arc:SelectItem Content="免费版" />
    <arc:SelectItem Content="专业版" />
</arc:Select>
```

## API 参考

<!-- api: Select -->

<!-- api: SelectItem -->

## 无障碍

- `Label` 是字段的名称，`Description` 是其帮助文本。
- 触发器获得焦点。Enter、Space 和方向键可打开菜单。方向键、Home、End、PageUp 和 PageDown 用于移动高亮，并跳过禁用的选项。Enter 或 Space 确认选择，Escape 关闭菜单且不改变选择，Tab 关闭菜单。
- 输入字母会跳转到下一个以该字母开头的选项。字段处于关闭状态时，会直接选中该选项。
- 所选值以纯文本形式提供给辅助技术。带动画的副本会被隐藏。
- 菜单打开时，焦点框保留在触发器上。

## 动效

- 显示的值会沿列表方向滚动：后面的选项从下方升入，前面的选项从上方落下。
- 菜单以短促的缩放与淡入淡出从触发器处展开。关闭时即时完成。
- 高亮从一个选项平滑滑动到下一个。
- 减弱动效时，值以瞬时交叉淡入淡出的方式切换，菜单淡入淡出。关闭动效时，值与菜单直接切换，没有任何过渡。

## 相关组件

- 列表较长或需要用户搜索时用 `Combobox`。
- 需要同时选择多个值时用 `MultiSelect`。
- 触发器需要展开为列表，且选项需要图标或分组时用 `MorphSelect`。
- 两到四个选项需要始终可见时用 `SegmentedControl`。
