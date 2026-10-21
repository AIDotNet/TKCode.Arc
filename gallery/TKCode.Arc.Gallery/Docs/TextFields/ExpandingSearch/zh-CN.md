---
title: 展开式搜索
summary: 图标展开为搜索框，并在下方显示结果。
source-hash: a233178b274e
---

<Demo name="HeaderSearch" />

## 何时使用

- 页眉或工具栏空间有限，放不下完整搜索框的场景。
- 从结果列表中跳转到文档、项目或页面。
- 用户还未输入任何内容时，展示最近的搜索。

## 何时不用

- 过滤页面上已有的列表：用 `SearchField`。
- 为表单选择一个值：用 `Combobox`。
- 覆盖整个应用的全局命令搜索：用命令面板，本库不提供。

## 用法

该控件初始为圆形图标按钮。激活后会展开搜索框，并显示结果或建议。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:ExpandingSearch Label="搜索项目与文档"
                         MaxResults="6"
                         ExpandedWidth="360" />
</UserControl>
```

### 项与建议

为 `ItemsSource` 填入 `ExpandingSearchItem` 对象。每一项都有 `Id` 和 `Title`，还可以设置 `Meta`、`Group` 与 `Keywords`。结果会与标题和关键词进行匹配，并按分组列出。`Suggestions` 会在用户输入任何内容之前显示。

```csharp
var items = new List<ExpandingSearchItem>
{
    new() { Id = "q3", Title = "季度报告", Meta = "文档 · 2 小时前编辑", Group = "文档" },
    new() { Id = "atlas", Title = "Atlas重新设计", Meta = "项目 · 12 个待办任务", Group = "项目" },
    new() { Id = "billing", Title = "账单迁移", Group = "项目", Keywords = ["invoices", "payments"] }
};

header.ItemsSource = items;
header.Suggestions = new[] { items[0], items[1] };
header.SuggestionsLabel = "最近";
```

### 选择

选中结果或建议后，输入框会先收起，然后触发 `ItemSelected`。如果希望改为用选中的项执行命令，请绑定 `SelectCommand`。

```csharp
header.ItemSelected += (_, e) => Open(e.Item.Id);
```

```xml
<arc:ExpandingSearch Label="搜索"
                     ItemsSource="{Binding Items}"
                     SelectCommand="{Binding OpenCommand}" />
```

### 锚点与宽度

`Anchor` 设置按钮所在的边缘。输入框会从该边缘向外展开，最宽至 `ExpandedWidth`。

<Demo name="AnchoredAtStart" />

### 在代码中打开与关闭

`Expand` 会打开输入框并将焦点移入其中。`Collapse` 会将其收回为图标，并清空查询内容。

```csharp
search.Expand();
search.Collapse();
```

`IsExpanded` 是双向的，因此也可以将它绑定到视图模型。

## API 参考

<!-- api: ExpandingSearch -->

<!-- api: ExpandingSearchItem -->

## 无障碍

- `Label` 为按钮、输入框和结果列表命名。
- 结果以带标题分组的列表呈现。结果数量与空状态会以礼貌的方式通报。
- 在按钮上按 Enter 或 Space 可打开输入框。ArrowDown 与 ArrowUp 在结果间移动，并会循环。
- Enter 选中高亮的结果，Escape 将输入框收回为图标。
- 收起的输入框不接收焦点，也不参与命中测试，因此不会挡住下方的内容。

## 动效

- 展开时宽度变形，圆形图标在同一弹簧上变为圆角输入框。收起更平滑，时长更短。
- 结果面板跟随输入框的宽度，高度平滑展开并淡入。
- 高亮在结果之间滑动，并保持上一次移动的速度。
- 减弱动效时，状态切换改为淡入淡出，不做宽度或位置移动。关闭动效时立即切换。

## 相关组件

- `SearchField`：用于过滤页面上已有内容的字段。
- `Combobox`：用于选择表单中保存的值。
- `Input`：通用单行输入框。
