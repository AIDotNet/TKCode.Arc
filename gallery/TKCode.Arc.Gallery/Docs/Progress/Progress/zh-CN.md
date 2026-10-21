---
title: 进度
summary: 展示已知总量任务的完成进度。
source-hash: d642f7397d3e
---

<Demo name="CountedValue" />

## 何时使用

- 上传、导入等总工作量已知的长时间任务。
- 需要让用户一眼读到百分比的场景，此时开启 `IsValueVisible`。

## 何时不用

- 无法度量进度的加载：用 `Skeleton` 预留空间。
- 相对于上限的配额：用 `UsageMeter`。
- 仪表盘指标：用 `Gauge`。
- 多步骤流程中的位置：用 `Stepper`。

## 用法

设置 `Value`，若总量不是 100，还需设置 `Maximum`。进度条为只读，无法获得焦点。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:Progress Label="正在上传 4 个文件"
                  IsValueVisible="True"
                  Value="35" />
</UserControl>
```

### 仅进度条

不设置标签和数值时，进度条单独使用。其无障碍名称为 "进度"。

<Demo name="BarOnly" />

### 自定义最大值

`Percentage` 为 `round(Value / Maximum × 100)`，例如 1,392 / 2,400 显示为 58%。

<Demo name="CustomMaximum" />

### 切换标签

切换 `Label` 时，新文字从柔和的模糊中升起，旧文字同时淡出。

<Demo name="ChangingLabel" />

```xml
<arc:Progress Label="{Binding PhaseName}"
              Value="{Binding PhaseProgress}" />
```

### 完成

达到 100% 时，填充色变为成功色，计数旁出现对勾。使用 `:complete` 伪类为该状态设置样式。

```csharp
upload.Value = 100;
// 此时 upload.IsComplete 为 true；开启 IsValueVisible 时会显示对勾。
```

进度条本身不会播报完成状态。任务结束时，请通过 Toast 等方式给出提示。

## API 参考

<!-- api: Progress -->

## 无障碍

- `Label` 是无障碍名称。未设置标签时，名称为 "进度"。
- 控件以百分比形式公开数值，因此屏幕阅读器会朗读当前百分比。
- 它不是实时区域。任务结束时请单独播报完成状态。
- 无法获得焦点，也没有键盘交互。

## 动效

- 同一条平滑弹簧同时驱动填充与计数的百分比，因此数字始终与进度条一致。
- 达到 100% 时，填充在短暂延迟后变为成功色，对勾以轻微缩放和模糊的方式落定。
- 标签变化时，新标签从柔和的模糊中升起，旧标签淡出。
- 减弱动效时，填充与计数直接跳到新值，标签以短暂淡入切换。

## 相关组件

- `Skeleton`：工作量未知且内容仍在加载时使用。
- `UsageMeter`：数值占满带上限的配额时使用。
- `Gauge`：仪表盘上的单一指标。
- `Stepper`：任务由一连串命名步骤组成时使用。
