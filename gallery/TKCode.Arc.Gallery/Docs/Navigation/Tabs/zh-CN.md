---
title: 标签页
summary: 在同一语境中切换相关内容。
source-hash: ebbf2d24231f
---

## 何时使用

- 同一对象的多个视图，例如概览、动态与设置。
- 并列的视图：用户每次选择其中一个，各自有独立的面板。
- 标签较多的列表，可横向滚动并使用边缘按钮。

## 何时不用

- 不需要面板、只选一个紧凑值：用 `SegmentedControl`。
- 按顺序阅读的堆叠内容区块：用 `Accordion`。
- 在多页结果之间翻页：用 `Pagination`。

## 用法

项是 `TabItem` 容器。标题是触发器，内容是面板。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:Tabs AutomationProperties.Name="工作区">
        <arc:TabItem Header="概览">
            <TextBlock Text="本季度营收增长 12%。" />
        </arc:TabItem>
        <arc:TabItem Header="动态">
            <TextBlock Text="Maya 在两小时前合并了 Billing v2。" />
        </arc:TabItem>
        <arc:TabItem Header="设置">
            <CheckBox Content="邮件摘要" IsChecked="True" />
        </arc:TabItem>
        <arc:TabItem Header="已归档" IsEnabled="False" />
    </arc:Tabs>
</UserControl>
```

### 数据绑定标签页

设置 `ItemsSource`。`ItemTemplate` 渲染触发器，`ContentTemplate` 渲染所选项的面板。通过 `SelectedItem` 或 `SelectionChanged` 读取选中状态。

```xml
<arc:Tabs ItemsSource="{Binding Folders}"
          SelectedItem="{Binding CurrentFolder}"
          MaxWidth="460"
          HorizontalAlignment="Left">
    <arc:Tabs.ItemTemplate>
        <DataTemplate>
            <TextBlock Text="{Binding Name}" />
        </DataTemplate>
    </arc:Tabs.ItemTemplate>
    <arc:Tabs.ContentTemplate>
        <DataTemplate>
            <TextBlock Text="{Binding Summary}" />
        </DataTemplate>
    </arc:Tabs.ContentTemplate>
</arc:Tabs>
```

### 激活模式

`ActivationMode` 决定方向键的行为。`Automatic`（默认）会选中获得焦点的标签。`Manual` 只移动焦点；按 Enter 或空格键才会选中标签。

```xml
<arc:Tabs ActivationMode="Manual">
    <arc:TabItem Header="日" />
    <arc:TabItem Header="周" />
    <arc:TabItem Header="月" />
</arc:Tabs>
```

### 在代码中选择

```csharp
tabs.SelectTab(2);
tabs.SelectionChanged += (_, _) =>
{
    status.Text = $"已选择：{tabs.SelectedItem}";
};
```

`SelectTab` 在索引处的标签存在且可用时将其选中，否则返回 `false`。

## API 参考

<!-- api: Tabs -->

## 无障碍

- 列表具有 Tab 角色，每个 `TabItem` 都会公开其选中状态。使用 `AutomationProperties.Name` 为列表命名。
- 只有选中的标签在 Tab 键顺序中。方向键在标签之间移动，Home 和 End 分别跳到第一个和最后一个。
- Tab 键从列表移动到所选标签的面板。
- 溢出列表上的滚动按钮由 `ScrollLeftLabel` 和 `ScrollRightLabel` 命名。
- 正在离开的标签面板对辅助技术隐藏，并且在淡出期间无法获得焦点。

## 动效

- 选中指示条以 morph 弹簧在触发器之间滑动。
- 新面板从移动方向的一侧滑入 8px 并淡入。旧面板保持原位，向回移动 6px 并快速淡出。
- 面板区域从旧面板高度以弹簧过渡到新高度，因此下方内容随之移动。
- 减弱动效时，指示条立即切换，面板原位交叉淡化，不做高度动画。关闭动效时，所有变化立即完成。

## 相关组件

- `SegmentedControl`：用于不带面板的紧凑值选择。
- `Accordion`：用于按顺序阅读的堆叠区块。
- `Pagination`：用于在同一集合的多个页面之间翻页。
