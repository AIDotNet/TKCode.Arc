---
title: 手风琴
summary: 在原位逐步展示补充信息。
source-hash: 51581e99173c
---

<Demo name="Default" />

## 何时使用

- 问答列表，例如常见问题（FAQ），同一时间只展开一个答案。
- 按简短标题分组的长内容，例如设置页或帮助页。
- 读者可以跳过、又不会丢失页面位置的补充细节。

## 何时不用

- 单独一个展开区域：用 `ExpandableCard`。
- 在同一内容的并列视图之间切换：用 `Tabs`。
- 只需“阅读更多”链接的短段文字：用 `ExpandableText`。

## 用法

每一行是一个 `AccordionItem`。`Header` 是问题，`Content` 是答案。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:Accordion>
        <arc:AccordionItem Header="免费版包含哪些内容？">
            无限项目、三位协作者，以及一周的版本历史。
        </arc:AccordionItem>
        <arc:AccordionItem Header="之后可以更改套餐吗？">
            可以。套餐变更立即生效，差价会按比例计入下一期账单。
        </arc:AccordionItem>
    </arc:Accordion>
</UserControl>
```

点击已展开的行会将其收起。展开另一行时，当前展开的行会自动收起。

### 数据驱动的行

设置 `ItemsSource`，问题使用 `ItemTemplate`，答案使用 `ContentTemplate`。`ExpandedIndex = -1` 表示初始时所有行都处于收起状态。

<Demo name="Large" />

```xml
<arc:Accordion Size="Large"
               ExpandedIndex="-1"
               ItemsSource="{Binding Questions}">
    <arc:Accordion.ItemTemplate>
        <DataTemplate>
            <TextBlock Text="{Binding Question}" TextWrapping="Wrap" />
        </DataTemplate>
    </arc:Accordion.ItemTemplate>
    <arc:Accordion.ContentTemplate>
        <DataTemplate>
            <TextBlock Text="{Binding Answer}" TextWrapping="Wrap" />
        </DataTemplate>
    </arc:Accordion.ContentTemplate>
</arc:Accordion>
```

### 尺寸

`Size` 设置行的尺寸。`Medium` 使用 50 px 高的行，文字为小号；`Large` 使用 76 px 高的行，问题与答案均更大，适合页面级的常见问题。

```xml
<arc:Accordion Size="Large">
    <arc:AccordionItem Header="可以随时取消吗？">
        月付套餐会保持有效，直到当前计费周期结束。
    </arc:AccordionItem>
</arc:Accordion>
```

### 同时展开多行

`ExpandMode="Multiple"` 允许各行独立展开。这是 Arc 的附加选项；Arc 默认为 `Single`。

<Demo name="Multiple" />

### 通过代码展开某一行

`ExpandedIndex` 为双向绑定。可以将其绑定到视图模型属性，也可以在代码中设置。`AccordionItem` 上的 `IsExpanded` 同样为双向绑定。

```csharp
accordion.ExpandedIndex = 2;
item.IsExpanded = false;
```

## API 参考

<!-- api: Accordion -->

<!-- api: AccordionItem -->

## 无障碍

- 每一行的触发器是一个按钮，遵循展开与收起模式。其展开或收起状态会被播报。
- 每个问题都是一个标题，因此屏幕阅读器用户可以在各问题之间快速跳转。
- 每个答案都由其问题标注。收起的答案会离开无障碍树，也不会获得焦点。
- 下方向键与上方向键在触发器之间移动焦点，到达两端时循环。Home 与 End 跳到第一个与最后一个触发器。
- Enter 与空格键切换当前焦点所在的行。

## 动效

- 面板高度以不过冲的平滑弹簧滑动。展开一行时答案随之显现，收起时答案快速隐去。
- 展开时答案会从柔和的模糊中向下滑动数像素。动画进行中再次点击，会从当前位置反向运动。
- 行展开时，折叠箭头旋转 180 度。
- 减弱与关闭动效时，直接以一步到达相同的终态。

## 相关组件

- `ExpandableCard`：用于单个展开区域，使密集的卡片展开。
- `ExpandableText`：用于将一段长文字截断为几行。
- `Tabs`：用于在同一内容的不同视图之间切换。
