---
title: 评分
summary: 一种简单、无障碍的方式，用来收集数字化的评价。
source-hash: ec85978b6dba
---

<Demo name="Feedback" />

## 何时使用

评分在一个简短的量表上询问分数，并说明这个分数的含义。指针指向星星时预览分值，按下或拖动时提交，星星旁边的文字会滚动到新的含义。同一控件也可以显示无人能修改的分数，例如平均分。

- **评价或反馈表单**：一个问题（例如“整体体验”），点一下就能回答。默认文字从 Poor 到 Excellent，告诉使用者每颗星代表什么。
- **商品或列表上的平均分**：用 `IsReadOnly` 配合 `AllowHalf`，在评价数量旁显示 4.5 颗星，不带任何输入行为。
- **工单关闭后的满意度调查**：`AllowClear="False"` 让答案一经给出就会保留，适合表单必须有分数才能继续的情况。
- **有专属措辞的量表**：`ValueLabels` 为每颗星指定自己的文字，例如从 Too slow 到 Very fast。

## 何时不用

- **连续的数值**：使用 `Slider`，它在一个范围内移动，而不是几个档位。
- **对帖子或评论的快速反应**：使用 `Reactions`，它统计许多人的表情。
- **不构成量表的具名选项**：使用 `RadioGroup`，每个选项都有自己的标签和说明。
- **在视图之间选择**：使用 `SegmentedControl`，它会切换旁边的内容。

## 用法

设置 `Label` 并绑定 `Value`，在使用者选择星星之前它是 0。`ValueChanged` 在使用者提交新值时触发，代码设置值时不触发。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:Rating Label="Overall experience" Value="{Binding Stars}" />
</UserControl>
```

### 半星与只读分数

`AllowHalf` 让指针、触摸和方向键以半星为单位移动。`IsReadOnly` 去掉输入、只留下分数，因此控件不获取焦点，可以低成本地在长列表中重复：所有星星由一个控件绘制，只有星星第一次移动时才创建动效。

<Demo name="Average" />

### 尺寸

`Size` 同时设置星星和点击目标：18 px 的星星配 28 px 的格子，24 配 36（默认），30 配 44。

<Demo name="Sizes" />

### 清除、量表与禁用状态

再次选择当前值会清除评分，Backspace 和 Delete 也一样。答案必须保留时设置 `AllowClear="False"`。`Maximum` 设置星星数量；不是五颗整星的量表显示为“7 out of 10”，除非提供 `ValueLabels`。

<Demo name="Required" />

### 自定义图标

`Icon` 把星星换成任意在 24 × 24 框内绘制的图形，例如 `{x:Static arc:ArcIcons.Heart}` 或路径数据；描边、填充、颜色和动效保持不变。
`EmptyIcon` 为未填充的部分设置另一个图形。任何图形都支持半星：左半边画 `Icon`，右半边画 `EmptyIcon`。

<Demo name="Icons" />

## API 参考

<!-- api: Rating -->

## 无障碍

| 按键 | 操作 |
|---|---|
| Right、Up | 将分值提高一档 |
| Left、Down | 将分值降低一档 |
| Home、End | 转到第一档或最后一颗星 |
| 1 到 9 | 选择对应的星星（量表中存在时） |
| Space、Enter | 未评分时选择第一档，否则确认当前值 |
| Backspace、Delete | 清除评分，除非 `AllowClear` 为 false |

评分是一个制表位，并把自己报告为从 0 到 `Maximum` 的范围，以 `Label` 作为名称。它的帮助文本用文字说明当前值，例如“Great, 4 of 5 stars”。只读评分不获取焦点。

## 动效

填充以 snappy 弹簧逐星扫过，每颗比上一颗晚 35 ms；预览其他值时，已提交的值保留为淡淡的标记。提交时，所选星星依次弹动一下；两次提交间隔不足 260 ms 时只有最后一颗星弹动。减弱动效时，星星立即变化，只有文字淡入淡出。

## 响应式

一行放不下时，分值文字会换到星星下方，并保持最长文字的宽度，因此旁边的内容不会移动。触摸时，分值跟随手指在这一行上移动，松开时提交，拖过第一颗星会清除评分。手机上使用 `Size="Large"` 可得到 44 px 的目标。

## 相关组件

| 组件 | 适用场景 |
|---|---|
| `Slider` | 数值连续或档位很多 |
| `Reactions` | 人们用表情和计数回应内容 |
| `RadioGroup` | 选项是具名选择，而不是量表 |
| `SegmentedControl` | 选择会切换旁边的视图 |
