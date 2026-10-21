---
title: 原位编辑
summary: 原地重命名：文字变为输入框，位置不变。
source-hash: 645c78156d49
---

<Demo name="Rename" />

## 何时使用

- 经常阅读、很少修改的名称、标题或描述，例如项目名称。
- 单独保存、不处于表单中的单个值。
- 平时应像普通内容一样显示，只有用户选择编辑时才变为输入状态的文字。

## 何时不用

- 需要一起保存的多个字段：使用由 `Input` 或 `Textarea` 组成的表单。
- 必须始终呈现为可编辑状态的字段：使用 `Input`。
- 用户在表单中撰写的多行正文：使用 `Textarea`。

## 用法

值以文字形式显示。激活后，它会变为相同尺寸的输入框。提交编辑时保存该值。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:InlineEdit Label="项目名称"
                    Value="Atlas重新设计"
                    HeadingLevel="2" />
</UserControl>
```

### 保存与校验

`SaveHandler` 负责持久化提交的值，并在字段显示保存中状态时运行。任务失败时，文字会回滚，并显示重试链接。当草稿无法保存时，`Validator` 返回一条提示信息，该信息显示在字段下方。

```csharp
title.Validator = value => value.Length == 0 ? "项目需要一个名称。" : null;

title.SaveHandler = async next =>
{
    await projects.RenameAsync(projectId, next);
};
```

处理 `TextCommitted`、`Saved` 和 `SaveFailed` 可以对结果作出响应。

```csharp
title.TextCommitted += (_, e) => log.Text = $"已提交“{e.NewValue}”（原为“{e.OldValue}”）";
title.Saved += (_, e) => log.Text = $"已保存“{e.NewValue}”";
title.SaveFailed += (_, e) => log.Text = $"保存失败：{e.Error.Message}";
```

### 变体与多行

`Variant` 设置视觉层级。`Title` 是名称与标题的默认值，`Body` 适合描述文字。设置 `IsMultiline` 后，文字会自动换行，字段高度随之增长。Enter 仍然是保存，Shift+Enter 则插入换行。

<Demo name="Multiline" />

```xml
<arc:InlineEdit Label="描述"
                Variant="Body"
                IsMultiline="True"
                PlaceholderText="添加描述"
                Value="{Binding Description}" />
```

### 绑定

`Value` 是双向的。用户未在编辑时，外部传入的新值会替换当前文字。

```xml
<arc:InlineEdit Label="项目名称"
                Value="{Binding ProjectName}"
                HeadingLevel="2" />
```

### 空值

在有值之前，占位文字会保留这一行。

<Demo name="Empty" />

## API 参考

<!-- api: InlineEdit -->

## 无障碍

- 静止状态下的文字是一个按钮，其名称由标签和值组成，例如“项目名称：Atlas重新设计”。
- `HeadingLevel` 会将文字报告为标题。是否支持取决于平台。
- 按 Enter 或 Space 开始编辑。Enter 保存，Escape 取消并恢复已保存的值，焦点离开字段时 Tab 会保存。
- 校验提示与保存错误会以礼貌的方式通报，并作为帮助文本关联到字段。保存进度和结果通过状态区域通报。

## 动效

- 旧值滚出，新值带着柔和的模糊升起。保存失败时，回滚方向相反，并短暂给外框着色。
- 输入时宽度跟随草稿变化；多行时，外框高度平滑变化。
- 保存时，小型加载指示器旋转。保存成功后，会绘制一个勾号。
- 减弱动效时，文字交叉淡入淡出，勾号立即绘制，加载指示器改为静态标记。关闭动效时效果相同，但不做淡入淡出。

## 相关组件

- `Input`：始终可见、随表单一起保存的字段。
- `Textarea`：表单中较长的正文文字。
- `ExpandingSearch`：可展开搜索字段与结果的控件。
