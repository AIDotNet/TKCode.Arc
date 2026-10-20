---
title: 计费切换
summary: 按月与按年切换，带有节省徽标，价格会滚动至新金额。
source-hash: 7cbb30d851d5
---

## 何时使用

- 定价页顶部的计费周期切换。
- 价格随所选周期或套餐变化，并需要滚动至新金额的场景。
- 带有节省说明的小型套餐选择，例如按年或终身。

## 何时不用

- 不涉及价格变化的视图：使用 `SegmentedControl`。
- 需要描述或纵向布局的选项：使用 `RadioGroup`。
- 单个开关式设置：使用 `Switch`。

## 用法

默认选项为“按月”和“按年”，“按年”带有徽标“省 20%”。将 `Value` 设置为所选选项。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:BillingToggle Value="yearly" />
</UserControl>
```

### 自定义选项

可以直接内联编写选项，也可以绑定 `ItemsSource`。`Badge` 显示在选项上。选中该选项时，`ActiveBadge` 会替换 `Badge`，因此说明可以原位变化，而不会挪动周围的内容。

```xml
<arc:BillingToggle Value="monthly" Size="Large">
    <arc:BillingToggleOption Value="monthly" Label="按月" />
    <arc:BillingToggleOption Value="yearly" Label="按年" Badge="省 20%" ActiveBadge="你可省 $48" />
    <arc:BillingToggleOption Value="lifetime" Label="终身" Badge="最划算" />
</arc:BillingToggle>
```

### 价格

`BillingPrice` 显示一个金额。`Currency` 默认为 `$`，`Decimals` 默认为 0。当 `WasAmount` 高于 `Amount` 时，会以删除线绘制旧价格。`Period` 为金额后的文字。

```xml
<arc:BillingPrice Amount="24" WasAmount="30" Period="每月，按年计费" />
```

```csharp
var toggle = new BillingToggle { Value = "yearly" };
var price = new BillingPrice { Amount = 24, WasAmount = 30, Period = "每月，按年计费" };

toggle.ValueChanged += (_, e) =>
{
    var yearly = e.NewValue == "yearly";
    price.Amount = yearly ? 24 : 30;
    price.WasAmount = yearly ? 30 : null;
    price.Period = yearly ? "每月，按年计费" : "每月";
};
```

### 尺寸与强调色

`Size` 设置选项高度：`Medium` 为 34，`Large` 为 42。`BadgeAccent` 为选中的徽标着色，默认为 Arc 的成功色。

```xml
<arc:BillingToggle Size="Large" Value="yearly" />
```

### 绑定

`Value` 是双向的，并与 `SelectedValue` 保持同步。

```xml
<arc:BillingToggle Value="{Binding BillingPeriod}" />
```

## API 参考

<!-- api: BillingToggle -->
<!-- api: BillingPrice -->
<!-- api: BillingToggleOption -->

## 无障碍

- 默认情况下，该组的名称为“计费周期”。设置 `AutomationProperties.Name` 可使用其他名称。
- 每个选项都是一个单选按钮。其名称由标签和当前徽标组成，例如“按年 省 20%”。
- 滑块对辅助技术隐藏。`BillingPrice` 会朗读为金额、旧价格和周期，例如“$24，原为 $30，每月”。
- Tab 将焦点移至选中的选项。方向键会选中并聚焦前一个或后一个选项，并会循环。Home 与 End 分别选中第一个和最后一个选项。
- 键盘焦点会绘制 2px 的轮廓，并与控件保持 2px 的偏移。指针聚焦时不显示。

## 动效

- 滑块以约 0.34 秒的临界阻尼弹簧滑动到选中的选项下方，无回弹。
- 选中选项的徽标采用强调色，其文字通过模糊过渡切换。
- 价格滚动至新金额。旧价格收起消失，周期文字在原位切换。
- 减弱动效时，滑块立即就位，未选中的选项不做按下缩放。关闭动效时效果相同。

## 相关组件

- `SegmentedControl`：不涉及价格、只切换内容的视图。
- `RadioGroup`：需要描述或纵向布局的选项。
- `RadioCards`：需要价格、描述以及不可用原因的套餐。
- `Switch`：立即生效的单个设置。
