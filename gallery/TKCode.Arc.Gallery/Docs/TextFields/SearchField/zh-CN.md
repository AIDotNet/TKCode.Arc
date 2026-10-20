---
title: 搜索框
summary: 易于识别的搜索入口，并带有清晰的操作提示。
source-hash: 88552bbca4f4
---

## 何时使用

- 筛选页面上已有的列表、表格或工具栏。
- 带清除按钮、可通过键盘访问的搜索入口。
- 搜索结果与搜索框显示在同一视图中的查询。

## 何时不用

- 点击后打开结果面板的紧凑型页眉搜索：使用 `ExpandingSearch`。
- 搜索值需要保存在表单中：使用 `Combobox`。
- 并排放置的多个筛选器和排序选项：将搜索框与其他控件一起放在工具栏中。

## 用法

搜索框起始位置有一个搜索图标。输入框中有文字时，末尾会出现清除按钮。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:SearchField Label="搜索人员"
                     Placeholder="按姓名筛选"
                     Width="360"
                     HorizontalAlignment="Left" />
</UserControl>
```

### 边输入边筛选

每次按键时 `Text` 都会变化。在 `TextChanged` 处理程序中筛选列表。如果筛选开销较大，请在代码中自行做防抖处理。

```csharp
search.TextChanged += (_, _) =>
{
    var query = search.Text ?? string.Empty;
    list.Children.Clear();
    foreach (var name in names.Where(n => n.Contains(query, StringComparison.CurrentCultureIgnoreCase)))
    {
        list.Children.Add(new TextBlock { Text = name });
    }
};
```

### 绑定查询词

`Text` 是双向属性，因此视图模型可以读取或重置查询词。

```xml
<arc:SearchField Label="搜索"
                 Placeholder="按姓名筛选"
                 Text="{Binding Query}" />
```

### 提交

按 Enter 会触发 `SearchSubmitted`，并携带搜索框中的文字。按 Escape 会清空搜索框。

```csharp
search.SearchSubmitted += (_, e) => submitted.Text = $"已提交“{e.Query}”";
```

在代码中调用 `Clear` 可清空搜索框。除非传入 `false`，焦点会保留在搜索框中。

```csharp
search.Clear();
```

### 尺寸

`Size` 设置外框高度：`Small` 为 36，`Medium`（默认）为 44，`Large` 为 50。

```xml
<arc:SearchField Label="小" Size="Small" Placeholder="搜索" />
<arc:SearchField Label="大" Size="Large" Text="路线图" />
```

## API 参考

<!-- api: SearchField -->

## 无障碍

- `Label` 为搜索框命名。搜索图标对辅助技术隐藏。
- 清除按钮默认名称为“清除搜索”。设置 `ClearText` 可将其翻译。
- 按 Enter 提交查询词，按 Escape 从键盘清空搜索框。
- 清除按钮可通过键盘访问，获得焦点时显示焦点环。

## 动效

- 清除按钮从略小的尺寸放大出现，并伴有轻微模糊。
- 其位置始终保留，因此无论按钮是否可见，搜索框的宽度都保持不变。
- 按下清除按钮时，按钮会略微缩小。
- 减弱动效时，清除按钮只做淡入淡出，不缩放也不模糊。关闭动效时立即显示或隐藏。

## 相关组件

- `ExpandingSearch`：页眉中的紧凑搜索，并在其下方显示结果。
- `Combobox`：从列表中选择一个值。
- `Input`：通用单行输入框。
