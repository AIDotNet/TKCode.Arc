---
title: 复选框
summary: 二元选择，状态精确且清晰易读。
source-hash: 1d622d74df4b
---

<Demo name="LabelAndDescription" />

## 何时使用

- 需要通过提交按钮确认的独立选择，例如同意条款。
- 父级行需要以半选状态反映部分子项的情况。
- 用户可以在一组选项中选择多个的场景。

## 何时不用

- 立即生效的设置：使用 `Switch`。
- 从一组选项中只能选择一个：使用 `RadioGroup`。
- 以标签形式呈现、经常切换的筛选条件：使用 `ChipGroup`。

## 用法

标签即控件的内容。`Description` 会在标签下方添加一行帮助文字。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:Checkbox Content="同意条款和条件"
                  Description="你同意我们的服务条款与隐私政策。" />
</UserControl>
```

### 必填与无效

`IsRequired` 会在表单中将该字段标记为必填。`IsInvalid` 显示错误状态，在未勾选时，外框边框呈危险色。

<Demo name="States" />

```xml
<arc:Checkbox Content="同意条款和条件"
              Description="勾选此项以继续。"
              IsRequired="True"
              IsInvalid="True" />
```

### 半选的父项

`CheckedState` 是一个枚举，包含 `Unchecked`、`Checked` 和 `Indeterminate`。`IsChecked` 是同一状态的可空布尔值，其中 `null` 表示半选。当部分子项被选中时，将父项设为半选。

<Demo name="IndeterminateParent" />

```xml
<arc:Checkbox Content="全部渠道" CheckedState="Indeterminate" />
```

若要让点击父项时同时设置所有子项，请在父项上处理 `CheckedStateChanged`，并将每个子项的 `IsChecked` 设为新状态。在该处理程序中跳过半选状态。

### 三态循环

`IsThreeState="True"` 会让点击在未选中、选中和半选之间循环，与 Avalonia 的 `CheckBox` 行为一致。

<Demo name="TriState" />

### 事件

`CheckedStateChanged` 会提供旧状态与新状态。每次 `IsChecked` 变化时都会触发 `IsCheckedChanged`。

```csharp
checkbox.CheckedStateChanged += (_, e) =>
{
    Console.WriteLine($"{e.OldState} -> {e.NewState}");
};
```

## API 参考

<!-- api: Checkbox -->

## 无障碍

- 标签即复选框的名称。没有标签时，请设置 `AutomationProperties.Name`；名称将回退为“复选框”。
- `Description` 是帮助文字。辅助技术会在名称之后朗读它。
- 空格键切换选中状态。与 WAI-ARIA 复选框模式一致，Enter 键不会切换。
- 半选状态会报告为混合状态。绘制的对勾与短横线不会单独暴露。
- 键盘焦点会绘制 2px 的轮廓，并与控件保持 2px 的偏移。指针聚焦时不显示。

## 动效

- 勾选时，填充从略小的尺寸放大出现；取消勾选时则快速淡出。
- 对勾会逐步绘制出来。切换为半选时，对勾会变形为短横线，切回时则反向变形。
- 按下复选框时会略微缩小，松开后弹回。
- 减弱动效时，所有状态变化立即生效，并取消按下缩放。关闭动效时效果相同。

## 相关组件

- `Switch`：立即生效的设置。
- `RadioGroup`：从一组选项中只选择一个。
- `ChipGroup`：用户经常切换的一组筛选条件。
