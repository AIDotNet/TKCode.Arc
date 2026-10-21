---
title: 下拉菜单
summary: 锚定在触发器上的聚焦操作列表。
source-hash: 7192f95491ae
---

<Demo name="Actions" />

## 何时使用

- 带有标签触发器背后的一小组命令，例如“重命名”“复制”和“删除”。
- 行或卡片的操作菜单，其中可见的入口比右键更清晰。
- 用分隔线对相关行分组、并将破坏性操作放在最后的列表。

## 何时不用

- 有默认操作和少数备选项：用 `SplitButton`。
- 针对某段内容的右键操作：用 `ContextMenu`。
- 头像背后包含身份与主题的账户菜单：用 `UserMenu`。
- 表单中的普通选项列表：用下拉选择或单选组。

## 用法

直接声明菜单行。命令用 `MenuEntry`，分隔线用 `MenuEntrySeparator`，分组标题用 `MenuEntryLabel`。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:DropdownMenu Label="操作">
        <arc:MenuEntry Header="重命名" />
        <arc:MenuEntry Header="复制" />
        <arc:MenuEntry Header="删除"
                       IsDestructive="True"
                       HasSeparatorBefore="True" />
    </arc:DropdownMenu>
</UserControl>
```

通过代码使用 `ArcIcons` 设置图标，它是 24x24 的 Lucide 几何图形。

```csharp
var rename = (MenuEntry)menu.Items[0];
rename.Icon = ArcIcons.Pencil;
```

### 处理选择

`ItemClicked` 会报告被激活的行。除非菜单行设置了 `StaysOpenOnClick`，否则事件之后菜单会关闭。

```csharp
menu.ItemClicked += (_, e) => status.Text = $"已选择：{e.Header}";
```

### 实时标签与单选行

通过代码设置 `Label` 可更改触发器文字。具有相同 `GroupName` 的单选行互斥，选中后菜单会关闭。设置了 `StaysOpenOnClick="True"` 的复选行（`ToggleType="CheckBox"`）会保持菜单打开，便于一次访问中切换多个选项。

<Demo name="LabelAndToggles" />

### 数据驱动的行与自定义触发器

`ItemsSource` 会为每个条目创建一行。`Trigger` 可用任意控件替换内置按钮。菜单会锚定到该控件，点击 `Button` 即可打开。

<Demo name="CustomTrigger" />

```xml
<arc:DropdownMenu ItemsSource="{Binding FileActions}" Side="Bottom">
    <arc:DropdownMenu.Trigger>
        <Button Content="..." />
    </arc:DropdownMenu.Trigger>
</arc:DropdownMenu>
```

### 通过代码打开

```csharp
menu.Open(focusFirst: true);
menu.Close();
```

### MenuSurface

`MenuSurface` 是每个 Arc 菜单所依据的可复用菜单弹层。可在代码中针对任意锚点将其打开。

<Demo name="MenuSurface" />

## API 参考

<!-- api: DropdownMenu -->

<!-- api: MenuEntry -->

## 无障碍

- 触发器以其 `Label` 播报，并报告菜单是否展开。
- 菜单行具有菜单项角色。输入首字母可按字母快速定位到对应行，禁用的行会被跳过。
- 在触发器上按 Enter、Space 或 ArrowDown 会打开菜单并定位到第一行。Escape 关闭菜单并将焦点交还触发器。
- 破坏性操作行仅以颜色标示，因此其 `Header` 应写明将被删除的内容。
- 图标与高亮对辅助技术隐藏。

## 动效

- 菜单以弹簧从触发器边缘展开，并伴随短暂的淡入。关闭时更快、幅度更小。
- 指针移动时，一个高亮块在行之间滑动；键盘操作时则直接跳转。菜单行以短促的错峰方式依次淡入。
- 新的 `Label` 会带着轻微模糊升起，触发器宽度随之调整。菜单打开时，箭头会旋转。
- 减弱动效时去掉缩放与滑动，仅保留快速淡入。关闭动效时菜单立即显示。

## 相关组件

- 针对选中对象、通过右键打开的次要操作：用 `ContextMenu`。
- 一个操作为默认操作、菜单中为备选项时：用 `SplitButton`。
- 应用顶栏中的账户入口：用 `UserMenu`。
- `MenuSurface` 是共享的菜单弹层，可将菜单锚定到任意控件。
