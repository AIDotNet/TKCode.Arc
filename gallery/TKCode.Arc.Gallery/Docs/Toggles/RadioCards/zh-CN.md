---
title: 单选卡片
summary: 可选择的选项卡片，带有滑动的选中光环、价格与描述插槽，并具有单选按钮的键盘行为。
source-hash: 39ff78d5f30d
---

## 何时使用

- 用户做决定前需要描述、价格或预估信息的选项，例如套餐、配送速度或地区。
- 无法选择时，需要通过 `DisabledReason` 显示原因的选项。
- 从简短列表中必须做出的一个选择。

## 何时不用

- 简短的纯文字选项：使用 `RadioGroup`。
- 两到四个视图，需要同时切换内容：使用 `SegmentedControl`。
- 长列表：使用 `Select`。

## 用法

卡片即该控件的 `Items`。每个 `RadioCardOption` 接受一个值、一个标签、一段描述以及一段元信息文字，元信息显示在卡片末端。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:RadioCards SelectedValue="pro">
        <arc:RadioCardOption Value="starter" Label="入门版" Description="适合刚开始使用的个人。" Meta="$0" />
        <arc:RadioCardOption Value="pro" Label="专业版" Description="无限项目与优先支持。" Meta="$12" />
        <arc:RadioCardOption Value="team" Label="团队版" Description="为小型团队提供共享工作区与角色。" Meta="$32" />
    </arc:RadioCards>
</UserControl>
```

### 绑定选项

将 `ItemsSource` 绑定到选项列表，并将 `SelectedValue` 绑定到所选值。两者都是双向的。

```xml
<arc:RadioCards ItemsSource="{Binding Plans}"
                SelectedValue="{Binding PlanId}"
                AutomationProperties.Name="套餐" />
```

```csharp
var plans = new RadioCards
{
    ItemsSource = new[]
    {
        new RadioCardOption("pro", "专业版", "无限项目与优先支持。", "$12") { Icon = ArcIcons.Zap },
        new RadioCardOption("team", "团队版", "为小型团队提供共享工作区与角色。", "$32") { Icon = ArcIcons.Users }
    },
    SelectedValue = "pro"
};
plans.SelectionChanged += (_, _) => Console.WriteLine(plans.SelectedValue);
```

`Icon` 接受一个 `Geometry`。它会以 18px 的图标绘制在卡片起始位置。

### 列表布局

`Layout="List"` 会将卡片堆叠为全宽行，元信息位于末端。较长的描述会自动换行。默认的 `Grid` 布局会填充至少 `MinColumnWidth` 宽的列（默认为 180）。

```xml
<arc:RadioCards Layout="List" SelectedValue="standard">
    <arc:RadioCardOption Value="standard" Label="标准" Description="4 至 6 个工作日" Meta="免费" />
    <arc:RadioCardOption Value="express" Label="快速" Description="2 至 3 个工作日" Meta="$9.00" />
    <arc:RadioCardOption Value="overnight" Label="次日达" Description="下一个工作日中午前送达" Meta="$24.00" />
</arc:RadioCards>
```

### 不可用的选项

在选项上设置 `IsDisabled`，可使其保持可见但不可用。设置 `DisabledReason` 说明原因。选项被禁用时，该原因会取代描述。方向键会跳过被禁用的选项。

```xml
<arc:RadioCards SelectedValue="pro">
    <arc:RadioCardOption Value="pro" Label="专业版" Description="无限项目与优先支持。" Meta="$12" />
    <arc:RadioCardOption Value="enterprise"
                         Label="企业版"
                         Description="单点登录、审计日志与专属联系人。"
                         Meta="定制"
                         IsDisabled="True"
                         DisabledReason="请联系销售以开通企业版。" />
</arc:RadioCards>
```

### 必填与禁用

`IsRequired` 会在表单中将该组标记为必填。`IsEnabled="False"` 会禁用所有卡片。

```xml
<arc:RadioCards IsRequired="True" IsEnabled="False" />
```

## API 参考

<!-- api: RadioCards -->
<!-- api: RadioCardOption -->

## 无障碍

- 对辅助技术而言，每张卡片都是一个单选按钮。当没有可见的标题为该组命名时，请在组上设置 `AutomationProperties.Name`。
- 卡片标签即卡片的名称。描述是其帮助文字。被禁用的卡片会改为报告其 `DisabledReason`。
- Tab 将焦点移至选中的卡片，若没有选中项则移至第一张可用卡片。方向键移动选择并会循环，Home 与 End 分别选中第一张和最后一张卡片。Enter 与空格键选中获得焦点的卡片。从右到左的布局中，方向键会镜像。
- 键盘焦点会绘制 2px 的轮廓，并与控件保持 2px 的偏移。指针聚焦时不显示。
- 选中状态以光环和圆点显示，因此不只依赖颜色来传达。

## 动效

- 选中光环以弹簧效果从一张卡片滑动到另一张。首次放置时淡入。布局尺寸变化时，光环会立即移动到新位置。
- 选中卡片时，圆点从无到有地放大出现。卡片背景与边框颜色快速变化。
- 减弱动效时，光环与圆点立即进入最终状态，并取消颜色过渡。关闭动效时效果相同。

## 相关组件

- `RadioGroup`：纵向列表中的简短纯文字选项。
- `SegmentedControl`：两到四个需要同时切换内容的视图。
- `Checkbox`：用户可以选择多个选项的场景。
- `BillingToggle`：按月或按年的计费周期。
