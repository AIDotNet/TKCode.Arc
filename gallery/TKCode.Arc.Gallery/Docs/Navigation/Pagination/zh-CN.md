---
title: 分页
summary: 在有明确边界的长集合中翻页。
source-hash: bc8fce527d6d
---

## 何时使用

- 浏览服务端结果或长表格，页码保存在状态或地址中。
- 显示用户在固定页数中的位置，并在两端禁用上一页和下一页按钮。
- 与数据表格或列表共享页数，页码变化时重新加载。

## 何时不用

- 浏览条目的幻灯片：用 `Carousel`。
- 随滚动阅读长信息流：用带高度限制的 `Timeline`，而不是页码按钮。
- 跳转到层级结构中的某个位置：用 `Breadcrumb`。

## 用法

绑定 `CurrentPage` 与 `PageCount`。控件会渲染一个包含五个页码按钮的窗口，窗口随当前页移动。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:Pagination PageCount="{Binding PageCount}"
                    CurrentPage="{Binding CurrentPage, Mode=TwoWay}" />
</UserControl>
```

### 响应页码变化

处理 `PageChanged` 以加载页面。`NewPage` 是用户请求的页码。

```csharp
pagination.PageChanged += async (_, e) =>
{
    status.Text = $"第 {e.NewPage} 页，共 {pagination.PageCount} 页";
    await LoadPageAsync(e.NewPage);
};
```

### 边界页

`ShowBoundaryPages` 让首页和末页保持一次点击即可到达，并在它们与窗口之间显示省略号。

```xml
<arc:Pagination PageCount="48"
                CurrentPage="20"
                ShowBoundaryPages="True" />
```

### 页数很少或没有页

页数少于五页时全部显示；两端的箭头会被禁用。`PageCount` 为 `0` 时渲染一个空的导航地标。

```xml
<arc:Pagination PageCount="3" CurrentPage="2" />
<arc:Pagination PageCount="0" />
```

### 标签

`Label` 为导航地标命名。`PreviousLabel`、`NextLabel` 与 `PageLabelFormat` 为辅助技术提供按钮名称。当默认文字不是用户所用语言时进行设置。

```xml
<arc:Pagination Label="结果分页"
                PreviousLabel="上一页"
                NextLabel="下一页" />
```

## API 参考

<!-- api: Pagination -->

## 无障碍

- 控件是一个导航地标，由 `Label` 命名。
- 每个页码按钮由 `PageLabelFormat` 命名，例如“第 3 页”。当前页具有当前页状态。
- 上一页与下一页按钮在两端被禁用，但保留其名称。
- Tab 键在上一页按钮、页码按钮与下一页按钮之间移动。Enter 或空格键进入当前焦点所在的页码。左右方向键在按钮之间移动焦点。

## 动效

- 单个当前页标记以 morph 弹簧滑动到所选按钮。
- 窗口移动时，数字按移动的槽位数像传送带一样滑动，新数字淡入。
- 减弱动效时，标记立即移动，数字原位替换。关闭动效时立即应用变化。

## 相关组件

- `Tabs`：用于切换视图，而不是同一集合的页面。
- `Breadcrumb`：用于显示页面在层级结构中的位置。
- `Carousel`：用于逐张浏览幻灯片。
