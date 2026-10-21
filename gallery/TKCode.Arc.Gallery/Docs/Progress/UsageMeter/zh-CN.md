---
title: 用量表
summary: 显示什么占用了配额，以及距离上限还有多少。
source-hash: da12e4afa9b4
---

<Demo name="Storage" />

## 何时使用

- 以固定配额衡量的存储空间、席位或 API 调用次数。
- 账单页面，列出哪些内容占用了套餐，以及还剩多少。
- 套餐可能被超出的场景，超出部分必须不依赖颜色也能清楚辨认。

## 何时不用

- 朝向完成状态推进的工作：用 `Progress`。
- 没有上限、需要彼此比较的类别：用 `BarChart`。
- 单一的核心指标：用 `Gauge` 或 `MetricCard`。

## 用法

为用量表提供 `Label`、`Limit` 以及各个类别。每个类别是一个 `UsageSegment`，包含 `Id`、`Label` 与 `Value`，`Value` 的单位与上限相同。保持 `Id` 稳定，使类别在列表变化时仍保留各自的动效。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:UsageMeter Label="工作区存储" Limit="50" Unit="GB">
        <arc:UsageSegment Id="media" Label="媒体" Value="14.2" />
        <arc:UsageSegment Id="docs" Label="文档" Value="9.6" />
        <arc:UsageSegment Id="backups" Label="备份" Value="6.1" />
        <arc:UsageSegment Id="other" Label="其他" Value="2.4" />
    </arc:UsageMeter>
</UserControl>
```

### 绑定类别

`ItemsSource` 优先于内联的 `UsageSegment`。绑定一个 `UsageSegment` 对象的集合。用量表会监听集合及每个分段，因此修改 `Value` 会重新布局进度条。

```xml
<arc:UsageMeter Label="工作区存储"
                Limit="50"
                Unit="GB"
                ItemsSource="{Binding Categories}" />
```

```csharp
media.Value = 17.4; // 进度条、总计与徽标都会更新
```

### 接近上限与超出上限

徽标显示剩余量。达到上限的 90% 时，它会变为带警告图标的"即将用完"；超过上限后则显示超出的部分。`WarnThreshold` 用于更改触发警告的比例。

<Demo name="AlmostFull" />

<Demo name="OverTheLimit" />

```xml
<arc:UsageMeter Label="本月 API 调用"
                Limit="100"
                Unit="K"
                WarnThreshold="0.8" />
```

### 单位与小数位

`Unit` 显示在每个数值之后。`Decimals` 设置小数点后的位数。对于席位等整数计数，使用 `Decimals="0"`。

```xml
<arc:UsageMeter Label="席位" Limit="20" Decimals="0">
    <arc:UsageSegment Id="eng" Label="工程" Value="12" />
    <arc:UsageSegment Id="design" Label="设计" Value="6" />
    <arc:UsageSegment Id="sales" Label="销售" Value="5" />
</arc:UsageMeter>
```

### 剩余与超出部分的标签

`FreeLabel` 为图例中的剩余配额命名，`OverLabel` 在总量超过上限后为超出部分命名。

```xml
<arc:UsageMeter Label="项目"
                Limit="10"
                FreeLabel="剩余"
                OverLabel="超出套餐" />
```

### 代码中的状态

读取 `Status` 可获得当前状态（`Ok`、`Near` 或 `Over`），读取 `Total` 可获得所有类别的总和。

```csharp
if (meter.Status == UsageMeterStatus.Over)
{
    ShowUpgradePrompt();
}
meter.ClearPin(); // 清除从图例中固定的类别
```

## API 参考

<!-- api: UsageMeter -->

<!-- api: UsageSegment -->

## 无障碍

- 用量表是一个以 `Label` 命名的分组。进度条是一张图像，其名称为对每个类别、总计与状态的完整文字摘要。
- "即将用完"或超出量增长等状态变化会以礼貌方式播报。
- 每个图例项都是一个切换按钮，以类别与数量命名，例如"媒体，14.2 GB"。
- 图例只占一个 Tab 停靠点。方向键在各项之间移动，Home 与 End 分别前往第一项与最后一项。
- Enter 或 Space 会固定获得焦点的类别，Escape 清除固定。

## 动效

- 用量表第一次有 40% 可见时，各类别从左到右依次增长。
- 之后，类别的值变化时，各自滑动到新宽度，数字滚动到新值。
- 超出上限后，超出斜纹与上限标记淡入。
- 悬停或聚焦某个类别会使其他类别变暗，按下则将其固定。
- 减弱动效时，直接设置数值与进度条，文字以短暂淡入切换。

## 相关组件

- `Progress`：工作朝向完成推进且没有配额时使用。
- `BarChart`：没有上限、需要彼此比较的类别。
- `Gauge` 或 `MetricCard`：单一的核心数字。
