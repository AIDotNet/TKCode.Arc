---
title: 分段控件
summary: 在一组相关的视图之间切换。
source-hash: c75d07f09527
---

## 何时使用

- 同一内容的两到五个简短视图，例如日、周、月。
- 立即生效的布局或模式切换。
- 标签足够简短、一眼即可读懂的一排选项。

## 何时不用

- 选项需要描述或纵向布局：用 `RadioGroup`。
- 选项需要价格、元信息或图标：用 `RadioCards`。
- 单个开关设置：用 `Switch`。
- 选项很多的选择：用 `Select`。

## 用法

每个 `SegmentedOption` 接受一个值和一个标签。控件通过值 `SelectedValue` 选中对应的分段。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:SegmentedControl AutomationProperties.Name="范围" SelectedValue="week">
        <arc:SegmentedOption Value="day" Label="日" />
        <arc:SegmentedOption Value="week" Label="周" />
        <arc:SegmentedOption Value="month" Label="月" />
    </arc:SegmentedControl>
</UserControl>
```

### 通过代码设置项

将 `ItemsSource` 绑定到选项列表。控件也接受纯字符串，`SelectedIndex` 可按位置选中分段。

```csharp
var sections = new SegmentedControl
{
    MaxWidth = 300,
    ItemsSource = new[] { "概览", "活动", "部署", "分析", "日志", "设置" },
    SelectedIndex = 0
};
```

### 附加内容

`Accessory` 是显示在标签之后的内容，例如数量。

```xml
<arc:SegmentedControl SelectedValue="all">
    <arc:SegmentedOption Value="all" Label="全部" />
    <arc:SegmentedOption Value="unread" Label="未读">
        <arc:SegmentedOption.Accessory>
            <TextBlock Text="3" />
        </arc:SegmentedOption.Accessory>
    </arc:SegmentedOption>
    <arc:SegmentedOption Value="archived" Label="已归档" IsDisabled="True" />
</arc:SegmentedControl>
```

### 绑定

`SelectedValue` 是双向的。将它绑定到保存当前视图的视图模型属性。

```xml
<arc:SegmentedControl ItemsSource="{Binding Ranges}"
                      SelectedValue="{Binding SelectedRange}"
                      AutomationProperties.Name="范围" />
```

### 意图事件

当指针或焦点到达某个分段时，会在该分段被选中之前触发 `ItemIntent`。可用它来做预览，例如显示提示。不要用它来切换视图。

```csharp
segmented.ItemIntent += (_, e) => hint.Text = $"预览：{e.Value}";
segmented.SelectionChanged += (_, _) => ShowView(segmented.SelectedValue);
```

## API 参考

<!-- api: SegmentedControl -->
<!-- api: SegmentedOption -->

## 无障碍

- `AutomationProperties.Name` 为分组命名。对于没有可见标题的控件，每个控件都应设置它。
- 每个分段都是一个按钮，会报告其按下状态。滑动的药丸形高亮对辅助技术隐藏。
- Tab 键从选中的分段进入控件；若未选中任何分段，则从第一个分段进入。再按一次 Tab 键即离开。
- 方向键选中并聚焦下一个或上一个分段，并循环切换。Home 和 End 选中第一个和最后一个分段。
- 键盘焦点会在分段内部绘制一个 2px 的轮廓线，因此滚动区域不会裁剪它。指针焦点不会绘制。

## 动效

- 一个药丸形高亮以形变弹簧滑动到选中的分段下方。高亮位于标签之下，并随标签一起滚动。
- 分段本身不会缩放或改变字重，只有高亮移动，标签颜色快速变化。
- 分段溢出时，该行会以短促的缓动滚动，以保持选中的分段可见。
- 减弱动效时，高亮立即移动，并取消颜色过渡。关闭动效时效果相同。

## 相关组件

- `RadioGroup`：选项需要描述或纵向布局时使用。
- `BillingToggle`：用于按月或按年计费周期切换，并附带节省说明。
- `Switch`：立即生效的单个设置。
