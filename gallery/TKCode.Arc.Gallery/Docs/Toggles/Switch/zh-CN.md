---
title: 开关
summary: 用于即时生效设置的触感式开关。
source-hash: feb4fcc5bdcb
---

<Demo name="SettingsList" />

## 何时使用

- 一经修改即生效的设置，例如通知。
- 设置列表，每一行放置一个开关。
- 只有开或关两种状态、且无需确认按钮的设置。

## 何时不用

- 需要通过提交按钮确认的选择，例如同意条款：用 `Checkbox`。
- 在具名选项之间做选择：用 `SegmentedControl` 或 `RadioGroup`。
- 用户可以选择多个选项的情况：为每个选项使用一个 `Checkbox`。

## 用法

`Content` 是开关的标签。整个标签都属于点击区域。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:Switch Content="邮件通知" IsChecked="True" />
</UserControl>
```

### 设置列表

将多个开关堆叠起来，即可形成一个设置列表。

```xml
<StackPanel Spacing="4">
    <arc:Switch Content="邮件通知" IsChecked="True" />
    <arc:Switch Content="每周摘要" />
    <arc:Switch Content="产品更新" />
</StackPanel>
```

### 绑定

将 `IsChecked` 绑定到视图模型属性。绑定 `IsEnabled` 可锁定开关。

```xml
<arc:Switch Content="邮件通知"
            IsChecked="{Binding EmailEnabled}"
            IsEnabled="{Binding CanEditSettings}" />
```

### 禁用

禁用的开关会保持其状态，且无法更改。

<Demo name="Disabled" />

### 变更

只要状态发生变化，无论来自指针、键盘还是代码，都会触发 `IsCheckedChanged`。请在此处应用设置，这样每种修改方式的效果都相同。

```csharp
notifications.IsCheckedChanged += (_, _) =>
{
    settings.EmailEnabled = notifications.IsChecked == true;
};
```

## API 参考

<!-- api: Switch -->

## 无障碍

- `Content` 即开关的名称。只包含图标的开关需要设置 `AutomationProperties.Name`。
- 按下空格键和 Enter 键可切换开关。按住空格键时，滑块保持拉伸的形状。
- 辅助技术会将开关报告为处于选中或未选中状态的切换按钮。Avalonia 没有单独的开关控件类型。
- 键盘焦点会绘制一个偏移 2px 的 2px 轮廓线。指针焦点不会绘制。
- 滑块对辅助技术隐藏。开和关的状态同时通过轨道颜色和滑块位置显示。

## 动效

- 按下开关时，滑块会向另一侧拉宽，远端边缘保持不动。松开后，滑块以无弹跳的弹簧滑过去。
- 并非由按下引起的变化（例如按键或代码修改），会播放一次短暂的滑块拉伸。
- 轨道填充会淡入淡出，滑块颜色随之变化。
- 减弱动效时，取消拉伸，滑块立即移动，并关闭颜色过渡。关闭动效时效果相同。

## 相关组件

- `Checkbox`：需要通过提交按钮确认的选择。
- `SegmentedControl`：在几个具名视图之间切换。
- `RadioGroup`：从一组选项中恰好选择一项。
