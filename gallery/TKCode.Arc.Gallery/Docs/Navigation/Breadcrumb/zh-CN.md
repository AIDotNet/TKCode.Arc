---
title: 面包屑
summary: 显示页面在层级结构中的位置。
source-hash: 68204e5bf393
---

## 何时使用

- 显示从某个区域根部到当前页面的路径，例如工作区、设置、账单。
- 桌面应用中的客户端路径，例如文件浏览器的文件夹路径。选择某一级时在应用内导航，而不是跳转到网址。
- 让用户一键返回上一层级。

## 何时不用

- 浏览整棵条目树：用 `TreeView`。
- 在同一对象的并列视图之间切换：用 `Tabs`。
- 在同一集合的多页之间翻页：用 `Pagination`。

## 用法

每一级都是一个 `BreadcrumbItem`。最后一级是当前页面：它是纯文本，从不作为链接。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:Breadcrumb>
        <arc:BreadcrumbItem Content="工作区" />
        <arc:BreadcrumbItem Content="设置" />
        <arc:BreadcrumbItem Content="账单" />
    </arc:Breadcrumb>
</UserControl>
```

### 通过点击导航

当前页面之前的每一级都可以点击。处理 `ItemClick` 以响应所选的层级；`Index` 是它在路径中的位置。

```csharp
breadcrumb.ItemClick += (_, e) =>
{
    status.Text = $"前往 {((BreadcrumbItem)e.Item!).Content}（索引 {e.Index}）";
};
```

### 绑定路径

将 `ItemsSource` 设为路径的集合。使用 `ObservableCollection<T>`，使路径变长或变短时层级随之变化。

```csharp
var path = new ObservableCollection<string> { "首页", "项目" };
breadcrumb.ItemsSource = path;
breadcrumb.ItemClick += (_, e) =>
{
    while (path.Count > e.Index + 1)
    {
        path.RemoveAt(path.Count - 1);
    }
};
```

打开文件夹时，用 `path.Add(name)` 添加一级。选择某一级时，其后的所有层级被移除，被移除的层级会以动画退出。

### 命令与导航 URI

带有 `Command` 的层级会以 `CommandParameter` 执行该命令，并以按钮形式显示。带有 `NavigateUri` 的层级则表现为链接。

```xml
<arc:BreadcrumbItem Content="账单"
                    Command="{Binding OpenBillingCommand}"
                    CommandParameter="{Binding BillingId}" />
```

### 标签

`Label` 为辅助技术提供地标名称。当面包屑不是页面上唯一的导航地标时设置它。

```xml
<arc:Breadcrumb Label="文件夹路径" />
```

## API 参考

<!-- api: Breadcrumb -->

## 无障碍

- 根节点是一个导航地标，由 `Label` 命名。其层级以列表形式呈现，并带有它们在路径中的位置。
- 最后一级是带有当前页面状态的文本。它从不作为链接，也无法获得焦点。
- Tab 键在作为链接或按钮的层级之间移动。Enter 跟随当前焦点所在的层级。
- 箭头形分隔符对辅助技术隐藏。

## 动效

- 新层级从左侧滑入并变清晰，同时相邻层级以平滑弹簧让出空间。
- 被移除的层级向左移动 4px 并快速淡出。其余层级随之让出空间。
- 路径未变化时，布局不做动画。
- 减弱动效时，新增与移除的层级仅以淡入淡出显示，不做位移。关闭动效时立即应用变化。

## 相关组件

- `Tabs`：同一对象的并列视图。
- `TreeView`：浏览整个层级结构。
- `Pagination`：在结果的多个页面之间翻页。
