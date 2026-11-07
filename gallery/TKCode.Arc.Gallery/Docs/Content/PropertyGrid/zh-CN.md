---
title: 属性面板
summary: 以"标签 + 编辑器"的行编辑一个或多个对象的属性，按分类分组，支持搜索、嵌套值和列表。
source-hash: 6491735f049a
---

<Demo name="Basic" />

## 何时使用

`PropertyGrid` 像设计、建模软件的"特性"面板那样编辑对象的属性：每个属性一行，左边是名称，右边是编辑器；行按分类分组，选中行的说明显示在下方。属性来自对象本身、而不是手工布局的表单时使用它。

- **CAD 或建模软件的特性面板**：选中一段圆弧，编辑它的图层、颜色、圆心和半径；选中多个图形，编辑它们共有的属性。
- **设置与配置编辑器**：带分类和说明的设置类，不写表单就能得到编辑界面。
- **可视化设计器**：编辑画布上某个控件的 Avalonia 属性。

## 何时不用

- **只填写一次的表单**（注册、结账）：自己布局 `Input`、`Select` 等字段，把标签、帮助文字和校验放在合适的位置。
- **字段相同的大量记录**：用 `Table` 或 `DataGrid`，便于跨行比较。
- **只有几个选项**：在 `Card` 里放几个 `Switch` 或 `Select`，比属性面板更清楚。

## 用法

给类加上 `[PropertyGridModel]`，再把对象交给属性面板。Foundation 生成器（随 `TKCode.Arc.Foundation` 分发）在编译时读取这个类，为每个公开属性注册强类型的读写委托，运行时不做反射，因此支持 Native AOT 和裁剪。

```csharp
using System.ComponentModel;
using TKCode.Arc;

[PropertyGridModel]
public sealed class Arc
{
    [Category("General"), Description("The layer the shape belongs to.")]
    public string Layer { get; set; } = "Walls";

    [Category("General"), DisplayName("Line type")]
    public LineType LineType { get; set; }

    [Category("Geometry"), PropertyGridOptions(Minimum = 0, FractionDigits = 4)]
    public double Radius { get; set; } = 1624.1491;

    [Category("Geometry")]
    public double Area => Math.PI * Radius * Radius;   // no setter: read-only
}

properties.SelectedObject = new Arc();
```

属性面板读取 `System.ComponentModel` 的 `[Category]`、`[DisplayName]`、`[Description]`、`[ReadOnly]` 和 `[Browsable(false)]`，以及 DataAnnotations 的 `[Display(Name, GroupName, Description)]`。`[PropertyGridOptions]` 提供属性面板自己的选项：`Order`，数字编辑器的 `Minimum`、`Maximum`、`Step` 和 `FractionDigits`，用于自定义编辑器的 `EditorKey`，行的样式类 `Classes`，以及 `Expand`。

每种类型使用 Small 尺寸的 Arc 字段作为编辑器：文本用 `Input`，数字用 `NumberField`，`bool` 用 `Switch`，枚举用 `Select`（成员上的 `[Description]` 是显示文字），`Color` 用 `ColorPicker`，`DateOnly`、`DateTime` 和 `DateTimeOffset` 用 `DatePicker`。只读属性以文字显示。文本在按 Enter 或焦点离开时提交，按 Escape 恢复原值。对象触发 `PropertyChanged` 时各行会重新读取，所以由其他属性算出的值（上例的面积）会随每次编辑更新；不发通知的对象调用 `Refresh()`。

### 多个对象

`SelectedObjects` 同时编辑多个对象。行是它们共有的属性（名称和类型都相同）；取值不一致时显示 `MixedValueText`（"Varies"），编辑时写入所有对象。像 CAD 的特性面板一样，可以在属性面板上方放一个选择器，把选择范围缩小到某一类图形。

<Demo name="MultiSelect" />

### 嵌套值和列表

属性的类型也标了 `[PropertyGridModel]`（或在代码中注册过）时，该行带有展开箭头，可以展开成它自己的行。结构体会被复制、修改，再逐级写回到它的所有者，直到选中对象，所以 `Point` 这类属性和其他属性一样编辑。列表（`IList`，包括数组）展开成 `[0]`、`[1]`……行；元素本身是模型时还能继续展开。列表可增长、且生成器能创建元素（值类型、字符串，或有公开无参构造函数的类）时，列表行有添加按钮，每个元素有删除按钮。

<Demo name="Lists" />

### 自定义编辑器和样式

`Editors` 为部分行替换内置编辑器。`PropertyGridEditor` 先按 `EditorKey` 匹配（用 `[PropertyGridOptions(EditorKey = "...")]` 设置），再按 `PropertyName`，最后按 `PropertyType`。模板的数据上下文是 `PropertyGridRow`：绑定 `Value`、`IsReadOnly`、`IsMixed` 和 `DisplayText`。

每行是一个 `PropertyGridItem` 控件。可以按伪类（`:readonly`、`:mixed`、`:expanded`、`:nested`、`:list-item`、`:invalid`、`:selected`）、按 `PropertyName`，或按 `[PropertyGridOptions(Classes = "...")]` 添加的样式类来设置样式。

<Demo name="CustomEditors" />

### 在代码中注册类型

无法加特性的类型（如 `System.Numerics.Vector3` 或其他库中的类型）在启动时注册一次。`Set` 写入类；`Update` 返回结构体或不可变类型修改后的副本，由属性面板写回。

```csharp
PropertyGridTypes.Register<Vector3>(t =>
{
    t.Property("X", v => v.X).Update((v, x) => { v.X = x; return v; });
    t.Property("Y", v => v.Y).Update((v, y) => { v.Y = y; return v; });
    t.Property("Z", v => v.Z).Update((v, z) => { v.Z = z; return v; });
    t.Summary(v => $"{v.X:0.##}, {v.Y:0.##}, {v.Z:0.##}");
});
```

### Avalonia 控件

Avalonia 对象不需要注册：它的 styled 和 direct 属性会被列出，按声明它们的类型分组。用 `PropertyFilter` 保留要显示的属性。

<Demo name="Controls" />

### 布局选项

`IsCategorized` 在分类标题和按字母排序的列表之间切换，`IsSearchVisible` 和 `IsDescriptionVisible` 显示或隐藏搜索框和说明区，`IsReadOnly` 让所有值都以文字显示。拖动标签和编辑器之间的分隔线可以改变 `LabelWidth`。

<Demo name="Options" />

### 变更和错误

每次编辑后都会触发 `PropertyValueChanged`，参数包含行、各对象原来的值和写入的值（添加或删除元素后，是列表所在的行和列表本身）。setter 抛出异常或文本无法转换时，值保持不变，该行变为 `:invalid`，`PropertyGridRow.ErrorMessage` 说明原因。

```csharp
properties.PropertyValueChanged += (_, e) =>
    undo.Push(e.Row.Path, e.OldValues, e.NewValue);
```

## API 参考

<!-- api: PropertyGrid -->

<!-- api: PropertyGridItem -->

<!-- api: PropertyGridCategoryItem -->

<!-- api: PropertyGridEditor -->

<!-- api: PropertyGridOptionsAttribute -->

## 无障碍

- 每行以标签命名，以属性说明作为描述；可展开的行会报告是否已展开，每个编辑器以所在行的标签命名。
- 列表的添加和删除按钮分别以 `AddItemText` 和 `RemoveItemText` 命名。
- 写入失败时，标签变为危险色，并以带 `ErrorMessage` 的提示显示原因，而不只依赖颜色。

### 键盘

| 按键 | 操作 |
|---|---|
| Tab | 按顺序在行、分类标题和编辑器之间移动。 |
| ArrowDown / ArrowUp | 在行或分类标题上：移到下一个或上一个。 |
| Home / End | 在行或分类标题上：移到第一个或最后一个。 |
| ArrowRight | 展开行或分类；在已展开的行上，移到第一个子行。 |
| ArrowLeft | 收起行或分类；在已收起的行上，移到父行或所属分类。 |
| Enter / F2 | 在行上：进入它的编辑器。在分类标题上，Enter 和 Space 收起或展开。 |
| Escape | 在编辑器中：恢复正在输入的值，然后回到所在行。 |

## 动效

- 行和分类的箭头在展开或收起时以 Arc 的弹簧转动；悬停和选中的底色在 120 ms 内淡入。
- 编辑器保留各自的动效（数字输入框的数字、选择器的菜单、取色器的面板）。
- 减弱动效时箭头直接转动、不用弹簧；关闭动效时没有任何动画。

## 响应式

- `PropertyGrid` 没有断点。标签列宽为 `LabelWidth`（默认 140 px），编辑器占用其余宽度；标签列不会把编辑器压到 96 px 以下，标签列本身也不会窄于 64 px。
- 它按自身宽度（`Bounds.Width`）计算，而不是窗口宽度；`MinWidth` 为 240 px。
- 标签与值以省略号截断（`CharacterEllipsis`），关闭横向滚动，说明区最多两行（`MaxLines`），因此很长的值不会把表格横向推开。
- 行高为 40 px（`RowHeight`）。没有任何功能依赖悬停，但集合行上的添加与删除按钮是 28 px 见方（`ActionSize`）。在手机上请调低 `LabelWidth`（最低 64 px），为编辑器留出更多空间。

## 相关组件

- `Input`、`NumberField`、`Switch`、`Select`、`ColorPicker` 和 `DatePicker`：内置编辑器。
- `TreeTable` 和 `TreeView`：展示记录的层级，而不是单个对象的属性。
- `Accordion`：在可展开收起的标题下分组内容，与属性面板的分类类似。
