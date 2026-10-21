---
title: 组合框
summary: 无需离开字段即可搜索并从列表中选择。
source-hash: 7880a8c53e61
---

<Demo name="Searchable" />

## 何时使用

- 用户会按名称搜索的长列表，例如时区或国家。
- 带别名的选项。`Keywords` 可让用户通过不在标签中的词找到项目。
- 单值选择，且输入答案比滚动查找更快。

## 何时不用

- 无需输入的简短固定列表：用 `Select`。
- 需要选择多个值：用 `MultiSelect`。
- 搜索的目的是浏览内容而非设置值：用 `ExpandingSearch`。

## 用法

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:Combobox Label="框架"
                  Width="280"
                  Description="输入以搜索。">
        <arc:SelectItem Content="Avalonia" Keywords="xaml dotnet" />
        <arc:SelectItem Content="AtomUI" Keywords="ant design" />
        <arc:SelectItem Content="MAUI" Keywords="xamarin" IsEnabled="False" />
        <arc:SelectItem Content="WinUI" />
    </arc:Combobox>
</UserControl>
```

### 绑定选项与值

将 `ItemsSource` 设为 `SelectOption` 记录。`Keywords` 参与搜索匹配，但不会显示。`SelectedValue` 为双向绑定，未选择任何项时为 `null`。

```xml
<arc:Combobox Label="时区"
              Width="320"
              ItemsSource="{Binding Zones}"
              SelectedValue="{Binding ZoneId, Mode=TwoWay}" />
```

```csharp
var zone = new Combobox
{
    Label = "时区",
    Width = 320,
    ItemsSource = cities.Select(c => new SelectOption(c.City, c.City)
    {
        Keywords = $"{c.Zone} {c.Offset}"
    }).ToArray()
};
zone.SelectionChanged += (_, _) => Console.WriteLine(zone.SelectedValue);
```

### 自定义筛选

默认情况下，标签、元信息和关键词按不区分大小写的子字符串进行匹配。设置 `FilterPredicate` 可以更改匹配方式。它接收项目和去除首尾空白后的查询词。

<Demo name="CustomFilter" />

### 占位文字与空状态文字

字段为空时显示 `Placeholder`。用户搜索时，所选的标签会让位于搜索内容。`EmptyText` 是没有任何匹配项时显示的状态行。

```xml
<arc:Combobox Label="国家"
              Placeholder="搜索国家"
              EmptyText="没有匹配的国家。" />
```

### 清除与焦点

`Clear()` 会清除选择和查询词。`FocusInput()` 将焦点移到字段上，同时打开列表。

```csharp
zone.Clear();
zone.FocusInput();
```

### 内联选项

禁用的选项会显示，但无法选取。选中某项后，清除按钮会弹出。

<Demo name="InlineOptions" />

### 禁用

禁用的 `Combobox` 会保留其值，并忽略输入。

<Demo name="Disabled" />

## API 参考

<!-- api: Combobox -->

<!-- api: SelectOption -->

## 无障碍

- `Label` 为字段及其列表命名，`Description` 是帮助文本。
- 该字段是组合框。用户使用方向键移动高亮时，焦点保留在文本输入框中。
- Enter 选中高亮的选项。Escape 关闭列表并丢弃查询词，因此之前的值保持不变。
- 空状态会以礼貌方式通知。清除按钮的名称为“清除选择”。
- 焦点框绘制在字段上。光标、选择、输入法和剪贴板均为原生 `TextBox` 的行为。

## 动效

- 列表以弹簧效果从上方展开，高度随匹配结果的数量变化。
- 所选的标签从下方升入字段，并带着柔和的模糊渐渐清晰。
- 有选中项时，清除按钮弹出显示；按下时缩小。
- 减弱动效时只保留淡入淡出，列表高度立即变化。关闭动效时所有变化直接完成。

## 相关组件

- 简短固定且无需输入的列表用 `Select`。
- 需要选择多个值时用 `MultiSelect`。
- 搜索的目的是浏览内容而非设置值时用 `ExpandingSearch`。
- 不从列表中选择的自由文本用 `Input`。
