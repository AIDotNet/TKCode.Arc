---
title: 输入框
summary: 标签清晰、状态实用的单行输入框。
source-hash: 73a65b253fc6
---

## 何时使用

- 姓名、邮箱地址、URL 等单行值。
- 随输入实时变化的校验文案。

## 何时不用

- 多行文本：用 `Textarea`。
- 有固定格式的值：用 `PasswordField`、`SearchField` 或 `NumberField`。
- 在页面上原位编辑文字：用 `InlineEdit`。

## 用法

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:Input Label="邮箱"
               Placeholder="you@example.com"
               Description="我们不会公开你的邮箱。" />
</UserControl>
```

### 辅助文字与错误提示

文案应由状态推导，直接修改字符串即可：只有变化的词会移动，数字逐位滚动。不要在几条独立的消息之间来回切换。

```xml
<arc:Input Label="用户名"
           Text="{Binding UserName}"
           Description="{Binding UserNameHint}"
           ErrorMessage="{Binding UserNameError}" />
```

```csharp
input.TextChanged += (_, _) =>
{
    var text = input.Text ?? string.Empty;
    input.Description = $"3 到 20 个字符，还可输入 {Math.Max(0, 20 - text.Length)} 个。";
    input.ErrorMessage = text.Length is > 0 and < 3 ? "至少输入 3 个字符。" : null;
};
```

`ErrorMessage` 非空时字段即为无效态；为空时显示 `Text` 绑定的校验错误。

### 尺寸

`Size` 决定外框高度：`Small` 为 36，`Medium` 为 44（Arc 的默认外观），`Large` 为 50。

```xml
<arc:Input Label="小" Size="Small" />
<arc:Input Label="大" Size="Large" />
```

### 前缀与后缀

```xml
<arc:Input Label="价格" Text="49">
    <arc:Input.Prefix>
        <TextBlock Text="¥" />
    </arc:Input.Prefix>
    <arc:Input.Suffix>
        <TextBlock Text="CNY" />
    </arc:Input.Suffix>
</arc:Input>
```

## API 参考

<!-- api: Input -->

## 无障碍

- `Label` 是字段的名称。`IsLabelVisible="False"` 时标签不显示，但仍作为名称。
- 辅助文字与错误提示合并为字段的帮助文本。
- 带动画的文字对辅助技术隐藏，辅助技术读取的是一份纯文本副本。
- 光标、选择、输入法、撤销与剪贴板快捷键都是原生 `TextBox` 的行为。

## 动效

- 辅助文字行与错误行以平滑弹簧展开高度；变化的词带着模糊升起并变清晰，数字逐位滚动。
- 减弱动效时，消息行直接以完整高度出现，换词改为瞬时淡入；关闭动效时直接到终态。
- 悬停与聚焦只加深边框颜色，尺寸不变。

## 相关组件

- 多行文本用 `Textarea`，密码用 `PasswordField`，搜索用 `SearchField`。
- `Field` 与 `FieldFrame` 为任何编辑器或触发器提供相同的标签、外框与消息行。
