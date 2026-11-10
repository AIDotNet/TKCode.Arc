---
title: Glass menu
summary: 一个磨砂玻璃菜单：高亮在指针下逐行滑动，内部一团柔光沿着行跟随指针。
source-hash: e2b26fe72d49
---

<Demo name="Preview" />

## 何时使用

`GlassMenu` 是放在磨砂玻璃上的一列命令：菜单会模糊并着色身后的内容，因此它浮在图片、画布或彩色页面上，而不会把它们完全遮住。一块更亮的玻璃高亮标出指针下或键盘焦点所在的行。它在行与行之间滑动而不是跳动，内部一团柔光沿着行跟随指针，让整个菜单像一块受光的玻璃。它可以放在页面中，也可以从按钮弹出，执行命令后关闭。

- **画布或照片上的操作**：在编辑器里排列、组合、锁定或分享一个对象。
- **媒体上的上下文菜单**：保存、复制或打开一张图片或一段视频。
- **简短的“更多”菜单**：放在玻璃工具栏或玻璃卡片上。

## 何时不用

- **很长或有子菜单的菜单**：玻璃适合少量命令。子菜单、可勾选项和长列表请用 `DropdownMenu`。
- **为表单选择一个值**：使用 `Select`；菜单是用来执行命令的。
- **纯色背景上的菜单**：没有可以模糊的内容时，使用 `DropdownMenu` 或 `ContextMenu`。

## 用法

直接写出各行：命令用 `GlassMenuItem`（可选的 `Icon`、文字和可选的 `Shortcut` 提示），分组之间用 `GlassMenuSeparator`。`GlassMenuItem` 是一个 `Button`，所以它接受 `Command` 并触发 `Click`；每执行一个命令，菜单还会触发 `ItemInvoked`。

```xml
<Panel arc:GlassBackdrop.Source="{Binding #Scene}">
    <Border Name="Scene" Background="#2B3A55" /><!-- your picture or page -->
    <arc:GlassMenu>
        <arc:GlassMenuItem Content="Duplicate" Icon="{x:Static arc:ArcIcons.Copy}" Shortcut="Ctrl+D" />
        <arc:GlassMenuSeparator />
        <arc:GlassMenuItem Content="Move to trash" Icon="{x:Static arc:ArcIcons.Trash2}" IsDestructive="True" />
    </arc:GlassMenu>
</Panel>
```

`GlassMenu` 派生自 Avalonia 的 `ItemsControl`，位于 Special 包中：在 `UseArc()` 旁调用 `UseArcSpecial()` 注册。磨砂效果与所有玻璃表面相同：它如何用公开的 Avalonia API 实现、何时设置 `arc:GlassBackdrop.IsLive`，见 Glass card 页面。

### 作为弹出菜单

用 `arc:GlassMenu.Menu` 把菜单附加到按钮上。点击后菜单在按钮下方打开；按 Down、Enter 或 Space 打开时，第一个命令获得焦点。执行命令、按 Escape（焦点回到按钮）或点击外部时菜单关闭。对于不在页面中的菜单，可以在代码中调用 `Open(anchor)`、`Close()` 或 `Toggle(anchor)`；`Placement` 设置它打开的方向。

<Demo name="Popup" />

```xml
<arc:Button Content="Arrange">
    <arc:GlassMenu.Menu>
        <arc:GlassMenu>
            <arc:GlassMenuItem Content="Bring to front" Shortcut="Ctrl+]" />
            <arc:GlassMenuItem Content="Send to back" Shortcut="Ctrl+[" />
        </arc:GlassMenu>
    </arc:GlassMenu.Menu>
</arc:Button>
```

### 来自数据的行

`ItemsSource` 中的任何项都会成为显示其文字的行。`ItemInvoked` 在 `Item` 中返回数据项，在 `Container` 中返回对应的行。

<Demo name="Data" />

### 内容建议

- 文字以动词开头并保持简短：“Duplicate”“Move to trash”。
- 要么每一行都有图标，要么都没有，这样文字才能对齐。
- 危险的命令放在最后，用分隔线隔开，并用 `IsDestructive` 标记。
- 快捷键提示只是文字；快捷键本身请用 `HotKey` 或按键绑定注册。

## API 参考

<!-- api: GlassMenu -->

<!-- api: GlassMenuItem -->

<!-- api: GlassMenuSeparator -->

<!-- api: GlassMenuItemInvokedEventArgs -->

## 无障碍

- 菜单是一个菜单，每个命令是一个菜单项，以文字为名称；`Shortcut` 作为加速键报告。分隔线不作为内容朗读。
- 高亮跟随键盘焦点，因此它同时就是焦点指示；禁用的行会被跳过。
- 用键盘打开弹出菜单时第一个命令获得焦点；用 Escape 或执行命令关闭时，焦点回到按钮，并显示键盘焦点环。

### 键盘

| 按键 | 操作 |
|---|---|
| `Tab` | 进入菜单（第一个命令）或离开 |
| `Down` / `Up` | 下一个或上一个命令，循环 |
| `Home` / `End` | 第一个或最后一个命令 |
| `Enter` / `Space` | 执行获得焦点的命令 |
| 一个字母 | 下一个以它开头的命令 |
| `Escape` | 关闭弹出菜单并回到按钮 |
| 在按钮上按 `Down`、`Enter` 或 `Space` | 打开菜单，第一个命令获得焦点 |

## 动效

- 高亮以灵敏弹簧在行之间滑动（约 0.26 秒，轻微回落）；第一次出现时在 160ms 内淡入，指针和焦点都离开时淡出。
- 柔光以平滑弹簧沿行跟随指针，在 160ms 内淡入。
- 弹出菜单淡入时上升 4px，并从 97% 放大到原大小。

| 变化 | 完整 | 减弱 | 关闭 |
|---|---|---|---|
| 高亮移到另一行 | 滑动 | 立即移动 | 立即移动 |
| 高亮出现或离开 | 淡入淡出 | 淡入淡出 | 立即 |
| 指针柔光和光泽 | 跟随指针 | 无 | 无 |

## 响应式

- 菜单宽度至少 220px，并与最长的一行一样宽；空间不够时，长文字以省略号结尾。
- 按钮下方没有空间时，弹出菜单会翻到按钮另一侧。
- 触摸时点按即执行命令，没有悬停高亮。

## 相关组件

- `DropdownMenu` 和 `ContextMenu`：带子菜单和可勾选项的完整菜单。
- `GlassCard`、`GlassSegmented` 和 `GlassTabBar`：其他玻璃表面。

| 组件 | 适用场景 |
|---|---|
| `GlassMenu` | 一个简短的菜单要浮在图片或画布上 |
| `DropdownMenu` | 普通页面上的完整菜单 |
| `ContextMenu` | 右键时的命令 |
