---
title: 对话框
summary: 为需要用户关注的决定提供专注的浮层面板。
source-hash: a7de713d0fa6
---

## 何时使用

- 必须在用户继续前确认的操作，例如删除项目。
- 能放进一个面板的简短表单，例如重命名或邀请。
- 分步流程，且每一步的标题和说明不同。

## 何时不用

- 篇幅较长的表单或记录详情：用 `Drawer`。
- 以移动端为主、需停靠在若干高度的任务：用 `BottomSheet`。
- 不应阻塞页面的轻量内容：用 `Popover`。
- 命令列表：用 `DropdownMenu`。

## 用法

在触发器旁声明 `Dialog`。它本身不渲染任何内容，在 `IsOpen` 为 true 时显示面板。

### 在 XAML 中声明

将主体内容放在内容区，按钮放在 `Actions` 中。带有 `arc:Dialog.Result` 的按钮会以该值关闭对话框。

```xml
<arc:Dialog Title="重命名项目"
            Description="请选择一个团队成员能快速识别的简短名称。"
            IsOpen="{Binding IsRenaming}">
    <arc:Input Text="{Binding ProjectName}" />
    <arc:Dialog.Actions>
        <StackPanel Orientation="Horizontal" Spacing="8">
            <arc:Button Variant="Secondary" Content="取消" arc:Dialog.Result="{x:False}" />
            <arc:Button Content="保存" arc:Dialog.Result="{x:True}" />
        </StackPanel>
    </arc:Dialog.Actions>
</arc:Dialog>
```

### 从代码中确认

`Dialog.ConfirmAsync` 会构建对话框、等待选择，并且只有点击确认按钮时返回 `true`。按 Escape、点击遮罩或关闭按钮都返回 `false`。

```csharp
var confirmed = await Dialog.ConfirmAsync(deleteButton, "删除项目？",
    "此操作将永久删除 Atlas 改版项目，且无法撤销。", "删除", isDanger: true);
status.Text = confirmed ? "已删除" : "已保留";
```

`Dialog.ShowAsync` 接受自定义内容和一组 `DialogAction`，并返回被点击操作的结果。

### 步骤标题

对话框打开期间可以修改 `Title` 和 `Description`。新文案会在原位淡入替换，因此分步流程无需第二个对话框。

```csharp
dialog.Title = "选择角色";
dialog.Description = "编辑者可以修改内容；查看者只能阅读。";
```

### 尺寸

`Size` 决定面板宽度：`Small` 为 360，`Medium` 为 440（Arc 默认值），`Large` 为 560。面板宽度永远不会超过窗口宽度减去 16px 的边距。

```xml
<arc:Dialog Title="工作区限制" Size="Large" IsOpen="{Binding ShowLimits}">
    <TextBlock Text="限制按工作区分别生效。" />
</arc:Dialog>
```

### 关闭与结果

用户关闭之前会触发 `Closing`，该事件可以被取消。`Closed` 会报告关闭原因和结果。

```csharp
dialog.Closing += (_, e) =>
{
    if (e.Reason == DismissReason.PressOutside && isSaving)
    {
        e.Cancel = true;
    }
};
dialog.Closed += (_, e) => status.Text = e.Result is true ? "已保存" : "已关闭，方式：" + e.Reason;
```

## API 参考

<!-- api: Dialog -->

## 无障碍

- 面板名称取自 `Title`，帮助文本取自 `Description`。
- 关闭按钮默认名称为“关闭对话框”，可通过 `CloseButtonName` 修改。
- 对话框打开时焦点移入其中，Tab 与 Shift+Tab 均停留在对话框内。
- 按 Escape 关闭对话框并将焦点返回给打开它的控件；除非 `CloseOnEscape` 为 false。

## 动效

- 遮罩淡入，面板以平滑弹簧从缩放 0.96 上升 8px。关闭动画更短，且从入场的当前状态开始。
- 标题或说明变化时，带着柔和的模糊升起。
- 减弱动效时仅保留短暂的不透明度淡入；关闭动效时对话框立即显示或移除。

## 相关组件

- `Drawer` 用于长表单及需保持页面可见的详情面板。
- `BottomSheet` 用于以移动端为主、需停靠在若干高度的任务。
- `Popover` 用于不阻塞页面的交互内容。
- `Alert` 用于留在页面内的消息，`Toast` 用于简短的操作结果。
