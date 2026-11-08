---
title: 枚举选择器
summary: 从枚举的成员中选择：普通枚举显示为单选项，[Flags] 枚举显示为按位组合的复选框，成员信息在编译期生成，不使用反射。
source-hash: 061ff55675fd
---

<Demo name="Basic" />

## 何时使用

`EnumSelector` 把一个枚举变成一组可见的选项。指定枚举并绑定 `SelectedEnum`，它会列出成员、显示它们的 `[Description]` 文字，并让值与选项保持一致。普通枚举显示为 `RadioGroup`，`[Flags]` 枚举显示为一组 `Checkbox`，勾选的成员按位组合。

- **设置页**：主题、密度或默认视图，视图模型里本来就是一个枚举。
- **权限与能力**：读取、写入、执行这样的 `[Flags]` 枚举，包括 `None` 成员和 `ReadWrite` 这样的组合成员。
- **筛选与状态**：从一个简短、固定的列表中选择状态或优先级。

## 何时不用

- **很长的列表**：超过大约八个成员会占用太多空间，改用 `Select` 或 `Combobox`。
- **选项不是枚举**：来自数据的选项请用 `RadioGroup`、`ChipGroup` 或 `MultiSelect`。
- **带说明、价格或图标的选项**：使用 `RadioCards`，或带 `RadioOption` 的 `RadioGroup`。
- **立即生效的开关**：单个 `bool` 用 `Switch` 或 `Checkbox`。

## 用法

给枚举加上 `[EnumMetadata]`。随 TKCode.Arc.Foundation 一起发布的生成器会在编译期读取它，并在程序集加载时把成员、`[Description]`（或 `[Display(Name = ...)]`）文字和 `[Flags]` 标记注册到 `EnumTypes`。运行时不使用反射，因此选择器兼容 Native AOT 和裁剪。

```csharp
using System.ComponentModel;
using TKCode.Arc;

[EnumMetadata]
public enum Priority
{
    [Description("Low")] Low,
    [Description("Medium")] Medium,
    [Description("High")] High,
    [Description("Critical")] Critical
}
```

然后设置 `EnumType` 并绑定 `SelectedEnum`（默认双向绑定）：

```xml
<arc:EnumSelector xmlns="https://github.com/avaloniaui"
                  xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                  xmlns:arc="https://github.com/AIDotNet/TKCode.Arc"
                  xmlns:demos="using:TKCode.Arc.Gallery.Demos"
                  Header="Priority"
                  EnumType="{x:Type demos:DemoPriority}"
                  SelectedEnum="{x:Static demos:DemoPriority.Medium}"
                  Columns="2" />
```

设置了 `SelectedEnum` 时可以省略 `EnumType`：选择器会列出这个值的类型。标记了 `[Browsable(false)]` 的成员不列出；两个成员值相同时只列出第一个。没有标记（也没有在代码中注册）的枚举不显示任何选项。

### 标志枚举

`[Flags]` 枚举显示为复选框。勾选一个成员会设置它的位，取消勾选会清除它的位，因此：

- 勾选 `ReadWrite` 这样的组合成员会同时勾选 `Read` 和 `Write`，取消勾选则把两者都清除；
- 组合成员的位只设置了一部分时显示短横（不确定状态），点击它会补齐其余的位；
- `None` 这样的零值成员只在没有任何位时才处于勾选状态，勾选它会清除所有标志，勾选其他任何成员都会取消它；
- 没有被任何列出成员命名的位会被保留。

`byte`、`short`、`int`、`long` 及其无符号类型都能正确处理，包括符号位和 `ulong` 的最高位。

<Demo name="Flags" />

### 显示模式

`DisplayMode` 默认为 `Auto`：普通枚举显示为单选项，`[Flags]` 枚举显示为复选框。`EffectiveDisplayMode` 是实际使用的模式，`HasFlags` 表示枚举是否带 `[Flags]`。

- `Radio` 对任何枚举都显示单选项。此时 `[Flags]` 枚举一次只取一个成员，组合成员也算一个。
- `Check` 对任何枚举都显示复选框。普通枚举仍然只取一个成员：勾选一个会取消其他的，因此值永远不会是枚举没有定义的组合。

<Demo name="DisplayModes" />

### 显示名称

`UseDisplayNames`（默认为 true）显示每个成员的显示文字；设为 `False` 则显示代码中的名称。文字立即切换，已选项保持不变。

<Demo name="DisplayNames" />

要翻译这些文字，请指定一个语言目录。`Priority` 的成员 `Low` 的文字就是目录成员 `PriorityLow`（`KeyPrefix` 加成员名；前缀默认是枚举名），并随 `ArcLanguage.Set` 切换。目录中没有对应条目的成员保留它的 `[Description]`，构建时给出警告（ARCF037）。

```csharp
using TKCode.Arc;
using TKCode.Arc.Localization;

[LanguageCatalog]
public enum AppLangResourceKind
{
    PriorityLow,
    PriorityMedium,
    PriorityHigh,
    PriorityCritical
}

[EnumMetadata(LanguageCatalog = typeof(AppLangResourceKind))]
public enum Priority { Low, Medium, High, Critical }
```

### 布局

选项放在一个均匀网格中。`Columns` 设置列数，`Rows` 设置行数；只设置其中一个时另一个自动计算，两个都不设置时选项排成一列。`RowSpacing` 和 `ColumnSpacing` 设置间距（单选项为 8 和 16，复选框为 0 和 16，因为复选框 44 px 的点击区域已经拉开了距离）。其他排列方式可以给 `ItemsPanel` 指定自己的面板，例如按可用宽度换行的 `WrapPanel`。

<Demo name="Layout" />

其他程序集中的枚举（例如上面的 `DayOfWeek`）用项目中的程序集特性来描述：

```csharp
[assembly: TKCode.Arc.EnumMetadata(typeof(System.DayOfWeek))]
```

### 绑定到视图模型

`SelectedEnum` 的类型是 `object?`，因此可以绑定到任何枚举类型的属性，包括可空类型。选择会写入属性，视图模型的新值（包括组合标志）会更新选项。设为 null 会清除选择。`SelectedEnumChanged` 会报告每次变化的旧值和新值。

<Demo name="Binding" />

### 自定义标签

`ItemTemplate` 根据 `EnumSelectorItem` 构建每个选项的标签：`Text` 是显示的文字，`Name` 是成员名，`Value` 是枚举值，`Member` 是完整的成员描述。

<Demo name="ItemTemplate" />

### 在代码中注册枚举

`EnumTypes.Register` 接受手写的描述，例如来自无法加标记、又不想在程序集特性中列出的库的枚举：

```csharp
EnumTypes.Register(new EnumTypeInfo(typeof(Size), isFlags: false,
    toBits: static v => (ulong)(int)(Size)v,
    fromBits: static b => (Size)(int)b,
    members:
    [
        new EnumMemberInfo("Small", Size.Small, 0) { DisplayName = "S" },
        new EnumMemberInfo("Large", Size.Large, 1) { DisplayName = "L" }
    ]));
```

## API 参考

<!-- api: EnumSelector -->

<!-- api: EnumSelectorItem -->

<!-- api: EnumSelectorDisplayMode -->

<!-- api: EnumSelectorChangedEventArgs -->

<!-- api: EnumMetadataAttribute -->

<!-- api: EnumTypes -->

## 无障碍

- 选择器是一个分组；字符串类型的 `Header` 是它的无障碍名称，也是内部单选组的名称。
- 单选项的行为与 `RadioGroup` 相同：整组只占一个 Tab 停靠点，方向键、Home 和 End 移动选择。
- 复选框的行为与 `Checkbox` 相同：每个都是一个 Tab 停靠点，空格键切换；部分设置的组合成员报告混合状态。

## 动效

- 选择器本身不添加动效：单选高亮的滑动和圆点的弹出与 `RadioGroup` 相同，对勾的绘制和变为短横与 `Checkbox` 相同。
- 来自视图模型的值以同样的方式移动选项；减弱动效或关闭动效时遵循这些控件的规则。

## 响应式

- `EnumSelector` 没有断点。均匀网格把得到的宽度平均分给各列，比列宽更长的标签会换行。
- 在窄屏上请减少 `Columns`，或把 `WrapPanel` 用作 `ItemsPanel`，让选项流到新的一行。
- 单选行和复选框的点击区域保持适合触控的高度（复选框为 44 px）。

## 相关组件

- `RadioGroup`：从自己列出的选项中选择一个。
- `Checkbox`：单个二元选择。
- `ChipGroup`：以标签形式选择筛选条件。
- `Select`：在菜单中从较长的列表中选择。
- `PropertyGrid`：与对象的其他属性一起编辑枚举属性，同样使用编译期生成的描述。
