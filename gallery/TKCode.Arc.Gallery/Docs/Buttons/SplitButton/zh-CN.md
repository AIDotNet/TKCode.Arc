---
title: 分割按钮
summary: 一个主操作，加上一组相近备选项的菜单。
source-hash: 47c862821190
---

## 何时使用

- 带有两到五个相近备选项的默认操作，例如“压缩并合并”与“变基并合并”。
- 导出或分享操作：通常使用某一种格式，其余格式放在菜单中。
- 默认操作的文字会原地变化以确认结果，例如将“复制页面”变为“已复制”。

## 何时不用

- 没有默认操作、选项地位对等：使用 Menus 中的 `DropdownMenu`。
- 只有一个操作、没有可选项：使用 `Button`。
- 针对行或卡片等内容对象的操作：使用 Menus 中的 `ContextMenu`。

## 用法

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:SplitButton Label="合并拉取请求">
        <arc:MenuEntry Header="压缩并合并" />
        <arc:MenuEntry Header="变基并合并" />
        <arc:MenuEntry Header="关闭拉取请求" IsDestructive="True" HasSeparatorBefore="True" />
    </arc:SplitButton>
</UserControl>
```

### 点击与菜单选择

主区域触发 `Click` 并执行 `Command`。箭头区域打开菜单，选中某一行时触发 `ItemClicked`，并传入所选的行。

```xml
<arc:SplitButton Label="导出为 CSV"
                 Variant="Secondary"
                 Command="{Binding ExportCommand}">
    <arc:MenuEntry Header="导出为 JSON" />
    <arc:MenuEntry Header="导出为 PDF" />
</arc:SplitButton>
```

```csharp
merge.Click += (_, _) => log.Text = "已通过合并提交完成合并。";
merge.ItemClicked += (_, e) => log.Text = $"菜单：{e.Header}";
```

### 来自数据的行

对于来自视图模型的行，使用 `ItemsSource`；使用 `ItemTemplate` 绘制它们。不是 `MenuEntry` 实例的项会显示为行的标题。

```xml
<arc:SplitButton Label="分享" ItemsSource="{Binding ShareTargets}" />
```

### 图标与原地结果

`Icon` 接收 24 乘 24 的 Lucide 几何图形，在代码中设置。更改 `Label` 与 `Icon` 时，主区域会变形为新的文字，因此可以直接在按钮上显示结果。

```csharp
copy.Click += async (_, _) =>
{
    copy.Label = "已复制";
    copy.Icon = ArcIcons.Check;
    await Task.Delay(1600);
    copy.Label = "复制页面";
    copy.Icon = ArcIcons.Copy;
};
```

### 禁用

将 `IsEnabled` 设为 `False` 可同时禁用两个区域。

```xml
<arc:SplitButton Label="部署" IsEnabled="False">
    <arc:MenuEntry Header="部署到预发布环境" />
</arc:SplitButton>
```

## API 参考

<!-- api: SplitButton -->

<!-- api: MenuEntry -->

## 无障碍

- 主区域与箭头是两个按钮。箭头的名称为 `{Label} more actions`，除非 `MenuButtonLabel` 设置了其他名称；它会报告菜单是否展开。
- 菜单使用 Menu 与 MenuItem 角色。方向键在各行之间移动，并在两端循环。
- 在主区域按 Enter 或 Space 执行主操作；在箭头上按下则打开菜单。
- 按 Escape 关闭菜单，并将焦点返回到箭头。
- `Label` 变化时会以礼貌方式通知屏幕阅读器。

## 动效

- 文字逐字母变化：相同的字母平滑滑动，变化的字母从柔和的模糊中升起。图标交叉淡化，主区域以弹簧方式过渡到新的宽度。
- 主区域会轻微下压。箭头从不缩放，菜单打开时它会转动。
- 菜单在箭头下方 4px 处打开，各行依次淡入。
- 减弱动效时移除按下效果、宽度弹簧与菜单变换，仅保留短暂的淡入。关闭动效时菜单与文字变化立即呈现。

## 相关组件

- `DropdownMenu`：没有默认操作、选项地位对等的菜单。
- `Button`：没有备选项的单一操作。
- `ButtonGroup`：并排显示的多个相关操作。
- `MenuEntry`：分割按钮与菜单共用的行类型。
