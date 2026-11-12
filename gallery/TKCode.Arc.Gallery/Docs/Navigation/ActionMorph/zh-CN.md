---
title: Action morph
summary: 放在角落、用来新建最常用条目的按钮：它展开成一份简短的动作列表，所选动作把同一块表面变成小表单，保存成功后缩回按钮并显示对勾。
source-hash: 53ae8aa1f2da
---

<Demo name="Preview" />

## 何时使用

`ActionMorph` 让人从固定在页面角落的圆形按钮新建一条内容，例如一条待办或一则笔记。按下它不会弹出对话框：按钮本身展开成一份简短的动作列表，
选中其中一个，同一块表面就变成一个紧凑的表单，包含标题、输入框、可选的一排选项和提交按钮。条目保存后，表单缩回按钮，按钮换上成功色并描出一个对勾；
稍后加号回来。返回上一层、关闭表面或点击别处，表单里已输入的文字都会保留。

- **顺手添加**：一条待办、笔记或日程，添加时它所在的列表始终可见。
- **两到四种新条目**，人们每天要加很多次，应当随时一按即达。
- **窄屏**：从角落按钮长出的表单比对话框占地更少。

## 何时不用

- **需要多个输入框的条目**：放进 `Dialog` 或 `Drawer`。
- **不需要输入文字的命令**：`DropdownMenu` 直接从列表执行它们。
- **用一个按钮保存屏幕上已有的内容**：`ActionButton` 自己就会显示忙碌和完成状态。

## 用法

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <Panel>
        <!-- The page content goes first; the morph sits last, in a corner. -->
        <arc:ActionMorph HorizontalAlignment="Right" VerticalAlignment="Bottom" Margin="16">
            <arc:ActionMorphAction Label="To-do"
                                   Description="Something to finish"
                                   Glyph="{x:Static arc:ArcIcons.ListTodo}"
                                   Shortcut="d"
                                   Title="New to-do"
                                   Placeholder="What needs to get done?"
                                   ChoicesLabel="When"
                                   SubmitLabel="Add to-do"
                                   SuccessLabel="To-do added">
                <arc:ActionMorphAction.Choices>
                    <x:String>Today</x:String>
                    <x:String>This week</x:String>
                </arc:ActionMorphAction.Choices>
            </arc:ActionMorphAction>
            <arc:ActionMorphAction Label="Note"
                                   Glyph="{x:Static arc:ArcIcons.FileText}"
                                   Shortcut="n"
                                   Title="New note"
                                   IsMultiline="True"
                                   SubmitLabel="Save note"
                                   SuccessLabel="Note saved" />
        </arc:ActionMorph>
    </Panel>
</UserControl>
```

`ActionMorph` 在 Special 包中：在 `UseArcControls()` 旁边用 `UseArcSpecial()` 注册。它不会自己定位：在布局里只占 56 px 的按钮，
菜单和表单从按钮所在的角向外长，盖在周围内容之上，所以把它放在面板的最后、贴着某个角，并留出外边距。

每个 `ActionMorphAction` 是一行菜单以及它打开的表单：`Label`、可选的 `Description`、`Glyph` 和可选的单字母 `Shortcut`，然后是表单的
`Title`、`Placeholder`、`IsMultiline`，可选的 `ChoicesLabel` 与 `Choices`（显示为单选组，先选中 `DefaultChoice`），以及 `SubmitLabel`、
`SuccessLabel` 和 `EmptyHint`。`Id` 和 `Tag` 供你区分各个动作。控件上的 `Label` 是按钮的名称，也是菜单的标题。

### 保存提交的内容

`SubmitHandler` 收到一个 `ActionMorphSubmission`（动作、去掉首尾空白的文字和所选项）以及取消令牌。它的任务运行期间，提交按钮显示转圈，
输入框不能编辑；它抛出异常时，表单保持打开、保留草稿并显示 `ErrorMessage`。返回或关闭表单会取消令牌。提交被接受后引发 `Submitted`，
按钮显示对勾 `ResetAfter`（1.6 秒）。

```csharp
morph.SubmitHandler = async (submission, token) =>
{
    await plans.SaveAsync(submission.Action.Id, submission.Text, submission.Choice, token);
};
morph.Submitted += (_, e) => list.Insert(0, e.Submission.Text);
```

空输入不会提交：输入框被标记、轻轻晃一下，并显示该动作的 `EmptyHint`（没有时用控件的）。开始输入即清除提示。每个动作各自保留草稿，直到提交成功。

### 从代码打开

`IsExpanded` 是双向的。`Expand()`、`Collapse()`、`OpenComposer(action)` 和 `Back()` 在各个面之间切换，`View` 和 `CurrentAction`
表示当前显示的面，按钮展开和收回时分别引发 `Expanded` 和 `Collapsed`。

### 另一个角

`Corner="Start"` 用于左下角的按钮：表面向上、向右长。

<Demo name="Corner" />

### 最佳实践

**建议**

- 提供两到四个动作：该页面上最常用的那几个。
- 给每个动作一个字母快捷键和一句简短说明。
- 让 `SubmitHandler` 等待真正的保存；它抛出异常时，表单保留文字并显示错误。

**避免**

- 需要多于一两行文字和一个选择的表单。
- 放在打开的菜单会挡住输入时需要看到的内容的地方。

## API 参考

<!-- api: ActionMorph -->

## 无障碍

- 按钮以 `Label` 命名，并报告它会打开菜单以及是否已展开。提交成功后，该动作的 `SuccessLabel` 会附加到按钮名称后面，并通过礼貌状态消息读出。
- 打开的菜单是以 `Label` 命名的菜单；各行是以动作命名的菜单项，说明作为帮助文本，快捷键作为加速键。
- 表单是以标题命名的分组。输入框同样以标题命名；提交为空或失败时，输入框被标为无效，并以提示文字描述，提示会被礼貌地朗读。选项是以 `ChoicesLabel` 命名的单选组。
- 每个面出现时获得焦点（菜单的第一行，或表单的输入框）；表面缩回按钮时，焦点回到按钮。
- 键盘焦点环只在键盘操作后显示在按钮、表单按钮和选项周围；菜单里由高亮块表示当前行。

### 键盘交互

| 按键 | 作用 |
|---|---|
| 按钮上的 Enter 或 Space | 显示菜单，焦点在第一个动作上。 |
| 下 / 上 | 下一个或上一个动作；最后一个之后回到第一个，第一个之前是最后一个。 |
| Home / End | 第一个或最后一个动作。 |
| 快捷字母（菜单打开时） | 直接进入该动作的表单。 |
| 单行输入框中的 Enter | 提交表单。 |
| 多行输入框中的 Ctrl+Enter（macOS 上 ⌘+Enter） | 提交表单；单按 Enter 换行。 |
| 选项中的方向键 | 选中下一个或上一个选项；只有选中的选项是 Tab 停靠点。 |
| Escape | 向上一层：表单回到菜单并聚焦它的动作，菜单回到按钮。 |

## 动效

- 一块表面承载全部三个面。它的宽、高和圆角跟随弹簧：变深约 0.46 秒到位，返回约 0.38 秒，都没有过冲。底色、描边和阴影在 240 到 480 毫秒内变化。
- 新的面从 4 px 模糊中短暂淡入。按钮与面板之间，它从角上的 90% 长大；面板之间，更深的面从右侧滑入，旧的被推向左边，返回时方向相反。
- 菜单各行依次上升 8 px，间隔 35 毫秒，高亮块滑到当前活动的那一行。
- 保存期间，转圈替换提交按钮的文字。保存完成后表单缩进按钮，按钮填上成功色，对勾一笔描出；`ResetAfter` 到时，对勾淡去，加号转回原位。
- 提交为空或失败时，输入框向一侧轻推一次。

| 变化 | 完整 | 减少 | 关闭 |
|---|---|---|---|
| 表面尺寸与圆角 | 弹簧 | 跳变 | 跳变 |
| 各个面 | 淡入、模糊与缩放或滑动 | 100–160 毫秒淡入淡出 | 直接切换 |
| 菜单行 | 依次上升 | 同时出现 | 同时出现 |
| 按下、晃动 | 缩放到 0.94、轻推 | 无 | 无 |
| 对勾 | 弹入并描画 | 淡入 | 直接显示 |

## 响应式

- 宽度：菜单 256 px，表单 320 px，都不超过视口宽度减 32 px；320 px 宽的手机能完整显示任一者。
- 圆形按钮为 56 px。只有鼠标或笔会产生悬停颜色；触摸永远不会。
- 表面向上、并朝远离 `Corner` 的一侧展开；把那块空间留给表单。

## 相关组件

- `OrbitMenu` 在按钮周围展开几个动作，没有表单。
- `DropdownMenu` 列出立即执行的动作。
- `Dialog` 和 `Drawer` 承载较长的表单。

| 组件 | 适用场景 |
|---|---|
| `ActionMorph` | 两到四个新建动作各需要一行输入，直接从角落按钮完成 |
| `OrbitMenu` | 从一个按钮立即执行几个动作 |
| `Dialog` | 任务需要多个字段或确认 |
