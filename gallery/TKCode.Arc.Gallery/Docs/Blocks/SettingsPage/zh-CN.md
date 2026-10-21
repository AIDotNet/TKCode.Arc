---
title: 设置页
summary: 完整的设置页：导航区带有账户卡片，旁边显示所选分区，并提供未保存修改的保存条。
source-hash: 4c5b5cdb75b0
---

<Demo name="Overview" />

## 何时使用

- 应用或工作区中有少量分区的设置区域，例如个人资料、通知、安全和账单。
- 各分区的修改需要一起保存，并有清晰的保存和放弃步骤。
- 需要从宽窗口一直适配到手机宽度单列的布局。

## 何时不用

- 只是一组简单的选项：单独使用 `SettingsGroup` 搭配 `SettingsRow`。
- 需要在应用任意位置通过名称查找并修改某项设置：使用 `SettingsCommand`。
- 在应用中不相关的页面之间导航：使用应用外壳的侧边栏。

## 用法

每个分区写一个 `SettingsSection`。`Header`、`Icon` 和 `Badge` 显示在导航中，所选分区则在右侧显示其 `Header`、`Description` 和内容。第一个已启用的分区会被选中；设置 `SelectedIndex` 或调用 `Select(section)` 可显示其他分区。`Title` 位于导航上方，`Footer` 位于导航下方，例如账户卡片。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:SettingsPage Height="480">
        <arc:SettingsSection Header="个人资料" Icon="{x:Static arc:ArcIcons.User}" Description="他人如何看到你。">
            <arc:Input Label="全名" Text="Noor Haddad" />
        </arc:SettingsSection>
        <arc:SettingsSection Header="通知" Icon="{x:Static arc:ArcIcons.Bell}" Badge="2">
            <arc:SettingsGroup Appearance="Plain">
                <arc:SettingsRow Header="提及" Description="有人提及你时。">
                    <arc:Switch IsChecked="True" />
                </arc:SettingsRow>
            </arc:SettingsGroup>
        </arc:SettingsSection>
    </arc:SettingsPage>
</UserControl>
```

### 未保存的修改

分区有未保存的修改时，将其 `IsDirty` 设为 `true`。它的导航项会显示一个圆点；选中该分区时，页面底部会升起一个保存条，其中有放弃和保存两个按钮。保存（或按 Ctrl+S，macOS 上为 ⌘S）会触发 `SaveRequested`，放弃会触发 `DiscardRequested`。两者都携带 `Section`。保存或还原修改后，将 `IsDirty` 设回 `false`。

<Demo name="UnsavedChanges" />

```csharp
page.SaveRequested += async (_, e) =>
{
    await SaveProfileAsync();
    e.Section.IsDirty = false;
};
```

### 紧凑布局

低于 `CompactWidth`（默认 720）时，页面变为单列：分区会变成标题下方一排可横向滚动的胶囊，导航下方的页脚隐藏，内容的内边距也会减小。`IsCompact` 反映当前是否为紧凑布局。

<Demo name="Compact" />

### 设置分组与设置行

`SettingsGroup` 和 `SettingsRow` 用于构建分区的内容，单独放在任何地方也可以使用。设置行可显示一个可选的 `Icon`、`Header` 和 `Description`，并将用于修改设置的控件作为其内容。当宽度低于 `StackWidth`（默认 440）时，控件会移到文字下方。当控件为 `Switch` 或 `Checkbox` 时，点击文字即可切换该控件；控件会将标题和描述用作其无障碍名称和帮助文本。

分组默认绘制为卡片，可选择设置 `Header`、`Description`、`HeaderActions`，以及带浅色底的 `Footer` 条。`Appearance="Plain"` 会去掉边框，适用于页面内用细线分隔的分组。

<Demo name="Rows" />

### 文本

`Labels` 包含保存条的文字和无障碍名称。若要翻译，请整体替换这个对象。

```csharp
page.Labels = new SettingsPageLabels
{
    UnsavedChanges = "未保存的修改",
    Save = "保存",
    Discard = "放弃",
};
```

## API 参考

<!-- api: SettingsPage -->
<!-- api: SettingsSection -->
<!-- api: SettingsGroup -->
<!-- api: SettingsRow -->

## 无障碍

- 每个导航项都是一个标签页项，名称依次为分区名、徽标；若分区有未保存的修改，还会加上“有未保存的修改”。导航的名称为“设置分区”。
- 导航只占一个 Tab 停靠点。上、下方向键（紧凑布局下为左、右方向键）移至上一个或下一个已启用的分区并将其显示；Home 与 End 分别移至第一个和最后一个。它们不会循环。
- 当所显示的分区有未保存的修改时，Ctrl+S（⌘S）可保存。
- 键盘焦点会在导航项周围绘制 2px 的焦点环。指针聚焦时不显示。
- 保存条中的提示文字位于实时区域（礼貌）中。

## 动效

- 选中胶囊会以轻快的弹簧在导航项之间滑动。
- 新进入的分区上移 6px 并淡入（标准时长、进入缓动），离开的分区则原地淡出。
- 保存条上移出现，并下移消失。
- 减少动效时保留淡入淡出，取消位移。关闭动效时，所有内容一次性切换。

## 相关组件

- `SettingsCommand`、`SecuritySettings`、`BillingOverview`、`UsageBilling` 和 `CancelFlow`：其他设置区块，可嵌入分区之中。
- `Tabs`：在同一对象的不同视图之间切换。
- `Switch`、`Input`、`Select`、`BillingToggle`：示例中分区所用的控件。
