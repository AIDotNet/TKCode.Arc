---
title: 可展开卡片
summary: 在需要时为密集的卡片提供更多空间。
source-hash: 452e4ce95fef
---

<Demo name="Default" />

## 何时使用

- 摘要卡片，例如套餐、订单或日志条目，其详细信息有用但并非总是需要。
- 展开时既需要变高也需要变宽，以便容纳详细信息的卡片。
- 摘要行会随时间变化的状态卡片，例如配送状态。

## 何时不用

- 多个互斥的区块：用 `Accordion`。
- 没有可展开详细信息的卡片：用 `Card`。
- 在同一位置浏览多张卡片：用 `CardStack`。

## 用法

`Header` 是卡片标题，`Description` 是其下方的摘要行，`Content` 存放详细信息。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:ExpandableCard Header="Pro 套餐"
                        Description="每月 ¥24 · 按年付费"
                        CollapsedWidth="420">
        <StackPanel Spacing="8">
            <TextBlock Text="无限项目与协作者。" />
            <TextBlock Text="一整年的版本历史。" />
            <TextBlock Text="优先支持，四小时内响应。" />
        </StackPanel>
    </arc:ExpandableCard>
</UserControl>
```

未设置宽度时，卡片填满其容器。

### 横向展开

`ExpandedWidth` 限定卡片展开时的宽度上限。卡片会以与高度相同的弹簧，从 `CollapsedWidth` 增长到 `ExpandedWidth`。未设置 `ExpandedWidth` 时，展开后的卡片使用 `CollapsedWidth`。

<Demo name="GrowingSideways" />

### 默认展开

`IsExpanded` 为双向绑定。设为 `True` 即可默认展开，也可以绑定到视图模型属性。

<Demo name="OpenByDefault" />

### 更新摘要

在代码中修改 `Description`。只有发生变化的词会滚入，其余部分保持不动。

```csharp
order.Description = "派送中 · 今天送达";
```

### 事件

卡片展开或收起之后会触发 `ExpandedChanged`。

```csharp
card.ExpandedChanged += (_, _) =>
{
    logger.Info($"套餐详情已展开：{card.IsExpanded}");
};
```

## API 参考

<!-- api: ExpandableCard -->

## 无障碍

- 标题是一个按钮，遵循展开与收起模式。其展开或收起状态会被播报。
- 卡片的名称即 `Header`。`Description` 作为标题按钮的帮助文本对外提供。
- Enter 与空格键可在标题上切换卡片的展开状态。
- 焦点位于卡片内部时，Escape 会收起卡片，并将焦点返回到标题。
- 卡片收起时详细内容处于惰性状态，其中的控件无法获得焦点。

## 动效

- 展开是一个连续的动作：宽度与高度以不过冲的平滑弹簧一同增长，折叠箭头随之转动。
- 卡片腾出空间后详细内容才淡入。收起时，详细内容先淡出，卡片随后收缩。
- 摘要中变化的词从下方升起，旧词向上飘离并淡出。
- 动画进行中再次点击，会从当前尺寸反向运动。
- 减弱动效时直接应用终态，摘要文字无动画地替换。

## 相关组件

- `Accordion`：用于一组互斥的区块列表。
- `Card`：用于没有可展开详细信息的卡片。
- `ExpandableText`：用于在原位展开的长段文字。
