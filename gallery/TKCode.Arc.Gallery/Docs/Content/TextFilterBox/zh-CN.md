---
title: 文本筛选框
summary: 一个文本筛选条件：用复选框启用，可选包含、开头、结尾、等于或正则表达式，并可区分大小写。
source-hash: 593051d50c97
---

<Demo name="Basic" />

## 何时使用

`TextFilterBox` 是针对文本字段的高级筛选中的一行：启用条件的复选框、字段名、以符号显示的操作符和筛选文本。在用户需要按某个字段搜索列表、且需要比搜索框更多控制时使用：

- **商品或文档列表中的名称与标题**：“名称 ∋ Pro”。
- **文件与路径**：“文件名 $ .axaml”、“路径 ^ src/”。
- **符合某种模式的日志行与编码**：“消息 .* ^ERR\d+”。

筛选框只保存条件，从不过滤数据：由视图模型读取 `IsActive`、`Operator`、`Text` 和 `IsCaseSensitive`，再按合适的方式过滤。

## 何时不用

- **对所有内容的一次搜索**：使用 `SearchField`。
- **从已知值中挑选**：使用 `Select`、`MultiSelect` 或 `ChipGroup`。
- **数字**：使用 `NumberFilterBox`。

## 用法

`TextFilterBox` 派生自 `FilterBox`（一个 Avalonia `TemplatedControl`）。它的各部分都是 Arc 控件：带标签和说明的 `Checkbox`、选择操作符的 `Select` 和输入文本的 `Input`，后者占据剩余宽度。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:TextFilterBox Label="Product name"
                       Operator="Contains"
                       Text="Keyboard"
                       Placeholder="Type a product name"
                       MaxLength="80"
                       IsCaseSensitive="False" />
</UserControl>
```

`IsActive`、`Operator`、`Text` 和 `IsCaseSensitive` 默认就是双向绑定。操作符有 `Contains`（∋，默认）、`NotContains`（∌）、`Equal`（=）、`NotEqual`（≠）、`StartsWith`（^）、`EndsWith`（$）和 `Regex`（.*）。空文本不构成任何限制。`Clear()` 清空文本并保留操作符。

### 正则表达式

`Operator="Regex"` 时，文本是一个 .NET 正则表达式。无法解析的模式会在输入框下方显示解析器的消息，把 `IsPatternValid` 设为 `False`、`PatternError` 设为该消息，并加上 `:invalid` 伪类；修正之前，该条件不构成任何限制。模式由正则表达式解释器执行（运行时不编译也不生成代码，因此兼容 Native AOT），一次匹配超过 `MatchTimeout`（200 ms）即视为不匹配。

<Demo name="Regex" />

### 附加内容

`ExtraContent` 放在输入框之后，例如一个“区分大小写”复选框或一个按钮。它随筛选一起禁用，为 null 时不占空间。

<Demo name="ExtraContent" />

### 在视图模型中过滤

`Condition` 是当前条件，类型为 `TextFilterCondition`：在自己的属性变化时构造一个（不要每项构造一次，这样模式只解析一次），再用 `Matches` 逐项判断。比较按序号进行，可区分或不区分大小写。筛选关闭时，筛选框上的 `Matches` 总是返回 true。

```csharp
var name = new TextFilterCondition(NameOperator, NameText, NameCaseSensitive);
var visible = products.Where(p => !NameActive || name.Matches(p.Name));
```

<Demo name="NumberFilterBox.Combined" />

### 对齐叠放的筛选框

给每个筛选框设相同的 `LabelSizeGroup`，并在容器上设 `Grid.IsSharedSizeScope="True"`：数字与文本筛选框共用一列标签，操作符因此对齐。也可以用 `LabelWidth` 设固定宽度。

<Demo name="Alignment" />

## API 参考

<!-- api: TextFilterBox -->

<!-- api: FilterBox -->

<!-- api: TextFilterCondition -->

<!-- api: TextOperator -->

<!-- api: FilterOperators -->

## 无障碍

- 复选框以标签命名，以 `Description` 作说明；按空格切换。
- 操作符字段以标签加 `OperatorText` 命名（“Product name operator”），帮助文本是操作符的名称，如“Starts with”；同样的名称也显示为工具提示。
- 文本输入框以标签命名；无效模式是它的错误消息，会随输入框一起朗读。

### 键盘

| 按键 | 时机 | 操作 |
|---|---|---|
| Tab | 任意位置 | 在复选框、操作符、文本和附加内容之间移动 |
| 空格 | 复选框 | 启用或关闭筛选 |
| Enter、空格、上、下 | 操作符 | 打开菜单；之后的移动与选择同 `Select` |
| 输入 | 文本 | 编辑筛选文本 |

## 动效

- 各部分沿用各自控件的动效：勾选标记自行绘出，操作符菜单从字段下方展开，错误消息在输入框下方滑入。整行本身没有动画。
- 减少动效或关闭动效时，各部分按各自的减弱方式表现。

## 响应式

- 筛选框拉伸到给定宽度。84 px 的操作符保持宽度，文本输入框占据其余部分，最窄 160 px。过长的标签或说明会换行，为它们留出空间。
- 连 120 px 的标签都放不下时（约 390 px 以下），标签移到输入框上方单独一行，此时该筛选框不再使用共享的 `LabelSizeGroup`。固定的 `LabelWidth` 放不下时同样如此。
- `ExtraContent` 在行尾占用自身的宽度。
- 所有输入框至少 36 px 高（`Size="Small"`），复选框在任何尺寸下都保留 44 px 的点击区域。

## 相关组件

- `NumberFilterBox`：同样的一行，用于数字，支持 = ≠ < ≤ > ≥ 和容差。
- `SearchField`：整个列表的一个搜索框。
- `FilterToolbar`：列表上方几个快速分面的标签。
