---
title: 单选组
summary: 从可见的一组选项中选择一项。
source-hash: 545fc82438a2
---

<Demo name="Plans" />

## 何时使用

- 两到六个互斥的选项，且每个选项都需要一段描述，例如套餐。
- 表单中随表单一起提交的选择。
- 纵向排列的选项，每个选项都保持可见。

## 何时不用

- 切换内容的简短行内视图：用 `SegmentedControl`。
- 很长的选项列表：用 `Select`。
- 需要同时选择多个选项：用 `Checkbox`。
- 选项需要在卡片上展示价格、元信息或图标：用 `RadioCards`。

## 用法

`Header` 是分组的标签。选项即控件的 `Items`；每个 `RadioOption` 接受一个值、一个标签以及可选的描述。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:RadioGroup Header="套餐" Width="420" SelectedValue="pro">
        <arc:RadioOption Value="hobby" Label="爱好版" Description="适合个人项目和尝试。" />
        <arc:RadioOption Value="pro" Label="专业版" Description="适合每周都要交付的专业人士。" />
        <arc:RadioOption Value="team" Label="团队版" Description="为不断壮大的团队提供共享工作区、角色与审计日志。" />
    </arc:RadioGroup>
</UserControl>
```

### 绑定选项

将 `ItemsSource` 绑定到选项列表，将 `SelectedValue` 绑定到当前选中的值。两者都是双向绑定。

```xml
<arc:RadioGroup Header="套餐"
                ItemsSource="{Binding Plans}"
                SelectedValue="{Binding PlanId}" />
```

```csharp
var plans = new RadioGroup
{
    Header = "套餐",
    ItemsSource = new[]
    {
        new RadioOption("hobby", "爱好版", "适合个人项目和尝试。"),
        new RadioOption("pro", "专业版", "适合每周都要交付的专业人士。")
    },
    SelectedValue = "pro"
};
plans.SelectionChanged += (_, _) => status.Text = $"已选择：{plans.SelectedValue}";
```

### 禁用选项

`IsDisabled` 让选项保持可见但不可用。方向键会跳过禁用的选项。

<Demo name="InlineOptions" />

### 选择事件

选中项变化时（无论通过指针还是键盘）会触发 `SelectionChanged`。`SelectedItem` 返回 `RadioOption` 本身。

```csharp
group.SelectionChanged += (_, _) =>
{
    if (group.SelectedItem is RadioOption option)
    {
        Console.WriteLine(option.Value);
    }
};
```

## API 参考

<!-- api: RadioGroup -->
<!-- api: RadioOption -->

## 无障碍

- `Header` 是分组的名称。每个选项都是单选按钮，其描述即为辅助文字。
- 方向键将选择移动到下一个或上一个选项，并循环切换。禁用的选项会被跳过。
- Home 和 End 选中第一个和最后一个选项。空格和 Enter 选中当前焦点所在的选项。
- Tab 键从选中的选项进入分组，并从该处离开，因此整个分组只占一个 Tab 停靠点。
- 键盘焦点会绘制一个偏移 2px 的 2px 轮廓线。指针焦点不会绘制。

## 动效

- 一个高亮块以形变弹簧滑动到选中的行。新的圆点以弹簧弹入，旧的圆点同时缩小消失。
- 按下单选圆圈时会轻微缩小。按下未选中的行会预览其圆点。
- 分组尺寸变化时，高亮块立即移动到新的行。
- 减弱动效时，高亮块和圆点立即就位，并取消按下时的缩放。关闭动效时效果相同。

## 相关组件

- `RadioCards`：选项需要在卡片上展示价格、元信息或图标时使用。
- `SegmentedControl`：两到五个简短视图，需要立即切换内容时使用。
- `Checkbox`：用户可能选择多个选项时使用。
- `Select`：很长的选项列表。
