---
title: Comparison table
summary: 一张我方与对手的对比表，带固定表头、高亮列和手机堆叠视图。
source-hash: 02f4e5dd2ddd
---

<Demo name="Preview" />

## 何时使用

在定价页或替代方案页使用 `ComparisonTable`，展示你的产品与具名替代方案的差异，适合三到五列、分组行的场景。

## 何时不用

- **比较自己的套餐**：使用 `PlanComparison` 区块。
- **需要排序的大型数据集**：使用数据表格。
- **无法核实的说法**：竞品名称保持通用或符合事实。

## 用法

```csharp
table.Columns =
[
    new ComparisonColumn { Id = "us", Name = "Relay", Caption = "$10 per seat", IsHighlighted = true },
    new ComparisonColumn { Id = "them", Name = "Legacy suite", Caption = "$24 per seat" }
];
table.Sections =
[
    new ComparisonSection
    {
        Id = "core", Title = "Core",
        Rows =
        [
            new ComparisonRow { Id = "offline", Feature = "Offline mode", Values = { ["us"] = true, ["them"] = false } },
            new ComparisonRow { Id = "sso", Feature = "SSO", Values = { ["us"] = true, ["them"] = ComparisonValue.Partial("Top plan") } },
            new ComparisonRow { Id = "seats", Feature = "Seats", Values = { ["us"] = "Unlimited", ["them"] = "Up to 50" } }
        ]
    }
];
table.Cta = new ComparisonCta { Label = "Start free trial", Href = "https://example.com/signup" };
```

<Demo name="CustomData" />

值可以是 `bool`（对勾或短横）、字符串（显示文本）或 `ComparisonValue.Partial(note)`。没有值的列显示短横。用 `IsHighlighted` 标记你的产品；否则第一列就是你的产品。

### 只看差异

开关设置 `IsDifferencesOnly`，隐藏所有可见列取值相同的行；备注不计入比较。一个分组没有剩余行时，其标题也会收起。

```xml
<arc:ComparisonTable MaxTableHeight="560" StickyTop="64" IsDifferencesOnly="True" />
```

### 固定表头

表格滚过时表头保持固定，对任意可滚动的祖先都生效。`StickyTop` 让它偏移到固定站点页眉下方。`MaxTableHeight` 限制表格高度并在区块内部滚动。

### 窄容器

宽度低于 `StackBelow` 时，表格显示你的列和一个通过选择器（`CompareWith`）选定的竞品。

<Demo name="Narrow" />

### 行动号召

`Cta` 在你的列底部添加按钮。设置 `Href` 且没有 `Command` 时打开链接，除非 `CtaInvoked` 的处理程序将事件标记为已处理。否则执行命令并变形为 `DoneLabel`。

## API 参考

<!-- api: ComparisonTable -->

<!-- api: ComparisonCtaEventArgs -->

## 无障碍

- 每个单元格都有带列名的文本替代：“Big suite: Included”“Partial, Top plan”“Not included”。
- 含义从不只依赖颜色：对勾、半圆和短横形状不同，并有图例说明。
- 开关的名称取自 `DifferencesLabel`，行被筛选时实时区域会读出提示。
- `Tab` 可到达开关、选择器和行动号召；`Space` 切换开关。

## 动效

- 你的列中的对勾在所在行完全进入视口时绘制一次，历时 480 ms，延迟 60 ms 后每行错开 35 ms，最多到第十二行。
- “只看差异”用 smooth 弹簧收起行和分组标题。
- 选择竞品时表头滚动 8 px，标记弹出（透明度与 .8 缩放，snappy 弹簧）。
- 行动号召变形宽度，并以 2 px 模糊切换文字。
- 减弱动效时标记立即显示，切换只淡入淡出，收起没有弹簧。关闭动效时立即完成。

## 响应式

- 区块跟随自身宽度。820 px 时特性列变窄；640 px 时间距收紧；低于 `StackBelow`（640 px）时堆叠为两列并显示选择器。
- 列宽按比例分配，因此不会横向滚动。行悬停只对鼠标或触控笔生效。

## 相关组件

- `PlanComparison` 区块：比较自己的定价层级。
- `PricingCalculator` 区块：随席位数变化的价格。
