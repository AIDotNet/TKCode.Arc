---
title: 上下文菜单
summary: 将次要操作放在所选对象附近。
source-hash: dcc977329161
---

<Demo name="FileTile" />

## 何时使用

- 针对文件、卡片或行的次要操作，例如“复制链接”或“删除文件”，并紧挨着它们作用的对象。
- 不希望在布局中占用可见菜单按钮的操作。
- 简短、扁平的列表，末尾有一个破坏性操作行。

## 何时不用

- 用户需要在不右键或长按的情况下找到的操作：用带可见触发器的 `DropdownMenu`。
- 以触屏为主、使用滑动操作的列表：用 `SwipeActions`。
- 内容本身需要点击操作：普通点击也会打开菜单，应在旁边使用 `Button`。
- 有默认操作和其他备选项：用 `SplitButton`。

## 用法

用 `ContextMenu` 包裹目标元素并设置其 `Label`。内容即为目标。在代码中向 `Items` 添加菜单行。下面的代码片段中，`file` 即为该 `ContextMenu`。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:ContextMenu Label="Q3 报告.pdf 的操作">
        <Border Width="220" Height="120" Padding="16" CornerRadius="18" BorderThickness="1">
            <TextBlock Text="Q3 报告.pdf" />
        </Border>
    </arc:ContextMenu>
</UserControl>
```

```csharp
file.Items.Add(new MenuEntry { Header = "复制链接", Icon = ArcIcons.Copy });
file.Items.Add(new MenuEntry { Header = "删除文件", Icon = ArcIcons.Trash2, IsDestructive = true });
```

### 处理选择

`ItemClicked` 会报告被激活的行。除非菜单行设置了 `StaysOpenOnClick`，否则事件之后菜单会关闭。

```csharp
file.ItemClicked += (_, e) => status.Text = $"Q3 报告.pdf：{e.Header}";
```

### 勾选行

没有图标的行在 `IsChecked` 为 true 时显示对勾。在该行的 `Click` 处理程序中切换它。

```csharp
var pinned = new MenuEntry { Header = "已固定", IsChecked = true };
pinned.Click += (_, _) => pinned.IsChecked = !pinned.IsChecked;
image.Items.Add(pinned);
```

### 数据驱动的行

`ItemsSource` 会为每个条目创建一行。`MenuEntry` 实例直接使用；其他数据需要 `ItemTemplate`。

```xml
<arc:ContextMenu Label="文件操作" ItemsSource="{Binding FileActions}">
    <TextBlock Text="{Binding Name}" />
</arc:ContextMenu>
```

### 为任意控件附加菜单

`arc:ContextMenu.Menu` 无需包裹即可为任意控件提供右键菜单。

<Demo name="Attached" />

### 通过代码打开和关闭

```csharp
file.Open(); // 显示在目标下方
file.Close();
```

## API 参考

<!-- api: ContextMenu -->

<!-- api: MenuEntry -->

## 无障碍

- 目标可获得焦点；菜单以 `Label` 播报，该标签同时命名目标与菜单。
- 菜单行具有菜单项角色。Enter 与 Space 选择一行，Escape 关闭菜单并将焦点交还目标。
- Shift+F10 与 ContextMenu 键可在已获得焦点的目标下方打开菜单。
- 没有图标的已勾选行会显示对勾，但勾选状态不会被播报。状态很重要时，请将其写入 `Header`。
- 破坏性操作行仅以颜色标示，因此应在 `Header` 中写明将被删除的内容。

## 动效

- 右键时菜单以快速弹簧从指针处展开，并伴随短暂的淡入。菜单行以短促的错峰方式依次淡入。
- 点击、轻触或 Shift+F10 会在目标下方以相同的动效打开菜单。
- 滚动或调整大小会关闭菜单，菜单不会随目标移动。
- 减弱动效时去掉缩放与滑动，仅保留快速淡入。关闭动效时菜单立即显示或隐藏。

## 相关组件

- 操作需要可见触发器时，用 `DropdownMenu`。
- 触屏为主的行，通过滑动显示操作时，用 `SwipeActions`。
- 一个操作为默认操作、菜单中为备选项时，用 `SplitButton`。
- `MenuSurface` 是共享的菜单弹层，可将菜单锚定到任意控件。
