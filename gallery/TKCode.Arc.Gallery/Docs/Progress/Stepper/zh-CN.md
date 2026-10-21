---
title: 步骤条
summary: 显示用户在多步骤流程中的位置以及已完成的步骤。
source-hash: 19ca1e8da110
---

<Demo name="Checkout" />

## 何时使用

- 严格的多步骤流程，如结账或账户设置，用户每次只能前进一步。
- 垂直时间线形式的步骤，每一步带有描述，失败的步骤可显示错误状态。

## 何时不用

- 已完成工作的单一百分比：用 `Progress`。
- 无序或用户可以自由跳转的步骤：用 `Tabs`。
- 步骤的内容：`Stepper` 只显示位置。请自行渲染步骤内容，并驱动 `CurrentIndex`。

## 用法

为 `Stepper` 提供步骤以及进行中步骤的索引。索引等于步骤数时，表示所有步骤均已完成。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:Stepper CurrentIndex="1">
        <arc:Stepper.Steps>
            <arc:StepperStep Label="账户" Description="创建登录信息" />
            <arc:StepperStep Label="配送" Description="寄送到哪里" />
            <arc:StepperStep Label="支付" Description="银行卡或发票" />
            <arc:StepperStep Label="确认" Description="检查并确认" />
        </arc:Stepper.Steps>
    </arc:Stepper>
</UserControl>
```

### 绑定步骤

`ItemsSource` 优先于 `Steps`。绑定一个 `StepperStep` 对象集合，并将 `CurrentIndex` 保存在视图模型中。

```xml
<arc:Stepper ItemsSource="{Binding Steps}"
             CurrentIndex="{Binding CurrentStep, Mode=TwoWay}" />
```

### 可导航的步骤

将 `IsNavigable` 设为 `True`，即可让用户通过点击，或使用方向键与 Enter 返回已完成的步骤。处理 `StepSelected` 以做出响应。如果该事件未被处理，所选步骤将成为当前步骤。

```xml
<arc:Stepper ItemsSource="{Binding Steps}"
             CurrentIndex="{Binding CurrentStep, Mode=TwoWay}"
             IsNavigable="True" />
```

```csharp
stepper.StepSelected += (_, e) =>
{
    // e.Index 是被选中的已完成步骤；e.Step 是它的 StepperStep。
    SaveDraft();
};
```

### 垂直方向与错误

将 `Orientation` 设为 `Vertical` 即得到时间线。在某个步骤上设置 `Error`，可将其标记变为提醒图标；错误消息会替换该步骤的描述。

<Demo name="VerticalWithError" />

```xml
<arc:Stepper ItemsSource="{Binding Steps}"
             CurrentIndex="2"
             Orientation="Vertical"
             Width="320" />
```

### 紧凑与当前详情

`IsCompact` 只显示标记，并在下方标注当前步骤的名称。宽度小于 480 px 的水平步骤条会自动切换到此模式。将 `Details` 设为 `Current` 时，只显示当前步骤的描述。

<Demo name="Compact" />

<Demo name="CurrentDetails" />

```xml
<arc:Stepper ItemsSource="{Binding Steps}"
             CurrentIndex="2"
             IsCompact="True" />
<arc:Stepper ItemsSource="{Binding Steps}"
             CurrentIndex="1"
             Details="Current" />
```

## API 参考

<!-- api: Stepper -->

<!-- api: StepperStep -->

## 无障碍

- `Label` 为步骤条命名。每个步骤都会以其状态命名：已完成、未开始或错误。
- 当前步骤被标记为当前步骤。
- 步骤变化会礼貌地播报，例如"第 2 步，共 4 步：配送"。所有步骤完成后，播报 `CompleteLabel`。
- 开启 `IsNavigable` 后，已完成的步骤可以获得焦点，并可用 Enter 或 Space 选择。方向键在可到达的步骤之间移动，Home 与 End 分别前往第一步或当前步骤。
- 尚未到达的步骤不可获得焦点，Tab 键会跳过它们。

## 动效

- 进度跨越多个步骤时，连接线依次填充，新的当前标记会出现一圈逐渐扩大的光晕。
- 步骤完成时，数字变为绘制的对勾；失败时，变为提醒标记。
- 变化的描述会从柔和的模糊中升起，它占据的空间以弹簧效果调整到新高度。
- 减弱动效时，所有变化立即生效。

## 相关组件

- `Progress`：工作以百分比而非步骤计量时使用。
- `Tabs`：用户可以以任意顺序切换分区时使用。
- `Skeleton`：某个步骤的内容仍在加载时使用。
