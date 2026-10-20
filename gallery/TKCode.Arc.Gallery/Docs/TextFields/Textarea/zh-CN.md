---
title: 多行文本框
summary: 用于备注、描述和较长文本的多行输入字段。
source-hash: 095ae5695edb
---

## 何时使用

- 跨越多行的自由文本，例如个人简介、评论或反馈。
- 较长的内容，且实时字数统计有助于用户控制在限制之内。
- 用户可能需要整块回读和编辑的文本。

## 何时不用

- 姓名、邮箱地址等单行内容：用 `Input`。
- 机密值：用 `PasswordField`。
- 在页面上原位编辑标题或描述：用 `InlineEdit`。
- 少量短值的列表：用 `TagInput`。

## 用法

按 Enter 会插入换行。字段撑满所在列，最小高度为 110 px。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:Textarea Label="简介"
                  Placeholder="用一两句话介绍你自己"
                  Description="还可输入 160 个字符"
                  MaxLength="160"
                  Width="420"
                  HorizontalAlignment="Left" />
</UserControl>
```

### 实时计数与校验

文本变化时更新描述。只有变化的数字会滚动。设置 `ErrorMessage` 可将字段标记为无效；值为空时清除错误。

```csharp
const int limit = 160;
bio.TextChanged += (_, _) =>
{
    var length = bio.Text?.Length ?? 0;
    bio.Description = $"还可输入 {limit - length} 个字符";
    bio.ErrorMessage = length < 40 ? "请再多写一些：至少 40 个字符。" : null;
};
```

### 绑定文本

`Text` 是双向属性，将其绑定到视图模型的字符串。

```xml
<arc:Textarea Label="反馈"
              Text="{Binding Feedback}"
              ErrorMessage="{Binding FeedbackError}" />
```

### 调整大小

拖动右下角的手柄可以增加字段高度。高度从最小值开始，不随内容变化；内容超出时字段会滚动。使用 `IsResizable` 可关闭手柄。

```xml
<arc:Textarea Label="备注"
              IsResizable="False"
              Text="固定高度。" />
```

用户拖动手柄后，`FrameHeight` 保存所选的高度（以像素为单位）。也可以在代码中设置它，使字段以指定高度开始。

```csharp
notes.FrameHeight = 200;
```

### 禁用

禁用的字段透明度降至 0.6，且没有手柄。

```xml
<arc:Textarea Label="备注"
              Text="暂时只读。"
              IsEnabled="False" />
```

## API 参考

<!-- api: Textarea -->

## 无障碍

- `Label` 是字段的名称。`Description` 与 `ErrorMessage` 合并为它的辅助文字。
- 错误出现时会立即被强势播报。带动画的文字对辅助技术隐藏，辅助技术读取的是纯文本版本。
- Enter 插入换行。光标、选择、输入法、撤销与剪贴板快捷键都是原生 `TextBox` 的行为。
- 获得焦点时，外框周围会绘制 3 px 的焦点环。

## 动效

- 辅助文字行与错误行以平滑弹簧展开高度；变化的词带着柔和的模糊升起。
- 字数统计只滚动变化的数字。
- 减弱动效时，弹簧、数字滚动与模糊都会移除，文案立即切换。关闭动效时同样如此。

## 相关组件

- `Input`：单行文本。
- `InlineEdit`：不显示外框、原位编辑值。
- `TagInput`：由短值组成的列表，而不是自由文本。
- `RichTextEditor`：需要标题或列表的文本。
</content>
