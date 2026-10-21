---
title: 卡片堆叠
summary: 逐张审阅卡片，支持划走与撤销。
source-hash: cb859fa03a17
---

<Demo name="Shortlist" />

## 何时使用

- 逐项处理一个队列，例如候选清单、审阅队列或一组偏好设置。
- 每张卡片都需要明确的“是”或“否”，并且阅读者可以撤回决定。
- 卡片堆清空后，可以从头再次审阅。

## 何时不用

- 并排比较或排序条目：用 `SortableDataTable`。
- 只浏览而不做决定：用 `Carousel`。
- 列表行上的快捷操作：用 `SwipeActions`。

## 用法

`ItemsSource` 提供卡片，`ItemTemplate` 绘制每张卡片的正面。`Label` 为整个堆叠命名。

```xml
<arc:CardStack Label="目的地"
               LeftLabel="跳过"
               RightLabel="保留"
               LeftOutcome="已跳过"
               RightOutcome="已入围">
    <arc:CardStack.ItemTemplate>
        <DataTemplate>
            <Border Width="300" Height="360" Padding="20" CornerRadius="12" Background="#F5F1EA">
                <TextBlock Text="{Binding}" FontSize="18" />
            </Border>
        </DataTemplate>
    </arc:CardStack.ItemTemplate>
</arc:CardStack>
```

### 为卡片命名

设置 `LabelSelector` 返回每张卡片的名称。该名称用作卡片的无障碍标签，也用于播报。当条目不是字符串时，选择器必须返回字符串。

```csharp
stack.LabelSelector = item => ((Destination)item!).Name;
```

### 自定义按钮与标记文字

`LeftLabel` 与 `RightLabel` 为按钮以及拖动卡片时出现的标记命名。`LeftOutcome` 与 `RightOutcome` 是播报中使用的过去式词语，例如“已跳过”。

<Demo name="CustomActions" />

### 在代码中做决定与撤销

`Decide` 会按指定方向划走顶部卡片。`Undo` 将最后一张卡片放回，`CanUndo` 说明是否有可放回的卡片。`Reset` 将所有卡片放回堆叠。

```csharp
stack.Decided += (_, e) => log.Text = $"{((Destination)e.Item!).Name}：{(e.Decision == CardDecision.Right ? "已入围" : "已跳过")}";
stack.DecisionUndone += (_, e) => log.Text = $"{((Destination)e.Item!).Name}：已恢复";
stack.StackReset += (_, _) => log.Text = "所有卡片已放回堆叠。";

stack.Decide(CardDecision.Right);
stack.Undo();
stack.Reset();
```

### 空状态

`EmptyContent` 会替换所有卡片都已做出决定后显示的默认提示。将按钮绑定到 `ResetCommand`，即可提供重新审阅的入口。

```xml
<arc:CardStack Label="候选人" ItemsSource="{Binding Candidates}">
    <arc:CardStack.EmptyContent>
        <Button Content="再次审阅" Command="{Binding ResetCommand, RelativeSource={RelativeSource AncestorType=arc:CardStack}}" />
    </arc:CardStack.EmptyContent>
</arc:CardStack>
```

### 照片

卡片正面可以在绘制的场景之上显示 `AsyncImage` 照片，因此离线时看起来仍然完整。

<Demo name="Photos" />

## API 参考

<!-- api: CardStack -->

## 无障碍

- 舞台可获得焦点，并以“卡片堆叠”播报。每张卡片都有名称，例如“里斯本，第 1 项，共 3 项”。
- 舞台获得焦点时，左右方向键决定顶部卡片。Backspace、Ctrl + Z 与 Cmd + Z 可撤销。
- 按钮与每个手势一一对应，因此划动不是决定卡片的唯一方式。禁用的撤销按钮会保留焦点。
- 决定、撤销与重置都会通过礼貌级别进行播报。
- 焦点环绘制在顶部卡片上，或空状态上。

## 动效

- 顶部卡片跟随拖动，朝移动方向倾斜并显示标记。快速轻扫或长距离拖动会让卡片带着松手时的速度划出堆叠。
- 顶部卡片离开后，后面的卡片上升到位。
- 撤销会让最后一张卡片从离开的位置飞回。重置时，卡片从最底层开始依次飞回。
- 短距离拖动会弹回堆叠。
- 减弱动效时，卡片在原位置淡出再淡入，不产生位移。关闭动效时立即到达最终状态。

## 相关组件

- `Carousel` 用于浏览一排条目，而不对每一项做决定。
- `SwipeActions` 用于列表行上的快捷操作。
- `SortableDataTable` 用于并排审阅大量记录。
