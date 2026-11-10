---
title: Action morph
summary: 一个按钮，会展开成一个内嵌的小面板来完成它的操作，例如填写输入框或选择几个选项，完成后再缩回按钮。
source-hash: 11724fad05dc
---

<Demo name="Preview" />

## 何时使用

`ActionMorph` 是一个会变成操作发生之处的按钮。按下"New list"不会打开对话框：按钮本身在弹簧驱动下变宽变深，成为一个小面板，里面有一个输入框和一个 Create 按钮，列表创建后，面板再缩回按钮。操作留在用户正在看的地方，周围的页面只按面板所需移动。

- **快速创建**：新列表、标签、文件夹的名称。
- **简短的选择**：一小时后提醒、今晚提醒或明天提醒。
- **工具栏和行内的输入**：用对话框就太重了。

## 何时不用

- **有多个字段或验证步骤的表单**：请用对话框或页面。
- **有后果、需要警告的操作**：请用确认对话框。
- **布局不能移动的地方**：面板会占用旁边元素的空间；请改用 `Popover`。

## 用法

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:ActionMorph Label="New list">
        <StackPanel Orientation="Horizontal" Spacing="8">
            <arc:Input Placeholder="List name" Width="180" />
            <arc:Button Content="Create" Variant="Primary" Size="Small" arc:ActionMorph.Closes="True" />
        </StackPanel>
    </arc:ActionMorph>
</UserControl>
```

`ActionMorph` 在 Special 包中：请在 `UseArcControls()` 旁边用 `UseArcSpecial()` 注册。`Label` 和 `Glyph` 构成按钮，内容就是面板。面板打开时，焦点会移到它的第一个可获得焦点的元素。它可以通过 Escape、它的关闭按钮、在外部按下、`Collapse()`，或点击内部任何标有 `arc:ActionMorph.Closes="True"` 的按钮来收回。`IsExpanded` 是双向的，面板展开和收起时会触发 `Expanded` 和 `Collapsed`：请在 `Collapsed` 中清空面板的字段。

`CollapsesOnOutsidePress` 可以在用户点击别处时保持面板展开，`ShowsCloseButton` 则在内容有自己的退出方式时隐藏关闭按钮。

### 主色外观和选项

`Variant="Primary"` 让按钮使用强调色填充。展开后的面板始终是普通的表面，所以填充色会流入其中。几个标有 `Closes` 的幽灵按钮就能组成一次快速选择。

<Demo name="Options" />

### 最佳实践

**建议**

- 用操作来命名按钮："New list"、"Add tag"、"Remind me"。
- 面板保持一行或一个短列，让它读起来像是按钮的一部分。
- 用 `arc:ActionMorph.Closes="True"` 标出完成操作的按钮。

**避免**

- 把它放在一个会因为它展开而把重要内容挤出视野的位置。
- 在面板里放很长的内容：它不是一个页面。

## API 参考

<!-- api: ActionMorph -->

## 无障碍

- 按钮的名称取自 `Label`，并会报告它的面板是否已展开。展开的面板是一个同名的分组，关闭按钮的名称取自 `CloseLabel`。
- 面板打开期间，按钮不在 Tab 顺序中；焦点移入面板，关闭时回到按钮。
- 使用键盘时，焦点环显示在按钮周围；使用鼠标或触摸时不显示。

### 键盘交互

| 按键 | 操作 |
|---|---|
| 在按钮上按 Enter 或 Space | 打开面板，并让第一个元素获得焦点。 |
| Tab | 在面板的内容和它的关闭按钮之间移动。 |
| Escape | 收起面板，并让焦点回到按钮。 |

## 动效

- 展开和收起是同一块表面在 形变弹簧上改变宽度和高度，圆角从按钮的 18 px 过渡到面板的 20 px，阴影随之升起。
- 面板淡入时按钮淡出；收回时面板先保留空间直到淡出，然后表面缩回按钮。
- 运动期间只有这个 morph 会重新布局；它旁边的内容随它一起移动。

| 变化 | 完整 | 减弱 | 关闭 |
|---|---|---|---|
| 大小 | 形变弹簧 | 直接跳变 | 直接跳变 |
| 圆角、填充和阴影 | 弹簧和 240ms | 填充 120ms | 立即 |
| 按钮和面板 | 交叉淡入淡出 | 交叉淡入淡出 | 直接切换 |

## 响应式

- 面板至少宽 240 px，否则与内容一样宽。请让内容窄到能放进 320 px 的手机。
- 按钮保持它的高度（36 px），宽度跟随它的文字。
- 面板展开时，同一行里的相邻元素会移动；需要避免时，请把它放在一行的末尾。

## 相关组件

- `Popover` 在页面之上打开内容，不会移动页面。
- `OrbitMenu` 在一个按钮周围提供几个操作。

| 组件 | 适用场景 |
|---|---|
| `ActionMorph` | 一个操作需要一点输入，就在按钮所在的位置 |
| `Popover` | 内容不能移动布局 |
| `Dialog` | 任务需要多个字段或一次确认 |
