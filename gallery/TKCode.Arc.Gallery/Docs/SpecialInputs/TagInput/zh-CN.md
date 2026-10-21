---
title: 标签输入
summary: 将简短文字值转换为可移除的标签。
source-hash: 3617ec564045
---

<Demo name="Default" />

## 何时使用

- 关键词、标签、话题或邮箱地址等自由输入的简短值。
- 用户通过输入逐条添加的列表，每一项用 Enter 或逗号加入。
- 需要数量上限的列表，使用 `MaxTags` 设置。

## 何时不用

- 从固定列表中选择的值：用 `MultiSelect` 或 `ChipGroup`。
- 单个值：用 `Input`。
- 正文中提及人员或频道：用 `MentionInput`。
- 多行自由文本：用 `Textarea`。

## 用法

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:TagInput Label="标签"
                  Placeholder="添加标签"
                  Description="按 Enter 或逗号即可添加标签。" />
</UserControl>
```

### 初始标签

`Value` 保存标签。它是 `IReadOnlyList<string>`，因此可在代码中设置，或绑定到一个集合。

```csharp
var tags = new TagInput
{
    Label = "标签",
    Value = ["design", "motion", "react"]
};
```

### 数量上限与错误

`MaxTags` 会拒绝超出上限的新增。字段会抖动，并播报已达上限。`ErrorMessage` 会将字段标记为无效，并显示错误行。

<Demo name="Limit" />

<Demo name="Error" />

### 粘贴列表

粘贴包含逗号、换行或制表符的文本时，会被拆分为多个标签。最后一段会保留在草稿中。

<Demo name="Paste" />

### 绑定与事件

`Value` 为双向绑定。`ValueChanged` 会报告旧列表与新列表，以及 `Added` 与 `Removed` 标签。

<Demo name="TwoWayValue" />

```xml
<arc:TagInput Label="关键词" Value="{Binding Keywords, Mode=TwoWay}" />
```

```csharp
tags.ValueChanged += (_, e) =>
{
    Console.WriteLine($"Added: {string.Join(", ", e.Added)}; removed: {string.Join(", ", e.Removed)}");
};

tags.AddTag("release");
tags.FocusEditor();
```

### 空字段与占位文字

只要字段中没有标签也没有草稿，占位文字就会浮现。

<Demo name="Empty" />

### 说明文字

字段下方的辅助文字，同时也会为屏幕阅读器描述编辑器。

<Demo name="Description" />

### 换行

标签会换到新的一行，外壳高度会随平滑的弹簧动画变化。移除其中一个，即可看到其余标签滑入新位置。

<Demo name="Wrapping" />

### 键盘

在起始位置按 Backspace 会选中最后一个标签，Backspace 或 Delete 会移除选中的标签，ArrowLeft 与 ArrowRight 会移动选择环，Escape 会清除选择。点击标签也会选中它。

<Demo name="Keyboard" />

### 禁用

禁用的字段会在 `SurfaceMuted` 上淡化到 0.5 的不透明度。

<Demo name="Disabled" />

## API 参考

<!-- api: TagInput -->

## 无障碍

- `Label` 是字段的名称。`IsLabelVisible="False"` 时标签不显示，但仍作为名称。
- 每个标签都有一个移除按钮，名称为 "移除 <tag>"。
- 新增、移除、选中与重复项都会以礼貌方式播报。重复项不会被添加：已有的标签会脉冲闪烁。
- 在输入框起始位置按 Backspace 会选中最后一个标签。Delete 移除选中的标签；ArrowLeft 与 ArrowRight 在标签与输入框之间移动选择。Escape 清除选择。
- 草稿在字段失去焦点时会转换为标签。

## 动效

- 新标签在输入位置以模糊和轻微缩放淡入。移除的标签离开时，其余标签滑动到新的位置。
- 选择环在标签之间滑动；标签换行到新行时，外壳高度随之调整。
- 重复项会让已有标签脉冲一次。被拒绝的新增会让字段抖动。
- 减弱动效时，去除布局移动、缩放与模糊，仅保留简短的淡入。关闭动效时直接跳到最终状态。

## 相关组件

- 值来自固定列表时，用 `MultiSelect` 或 `ChipGroup`。
- 单个值用 `Input`。
- 正文中的 @人员与 #频道引用用 `MentionInput`。
