---
title: 文字变形
summary: 逐字母将标签变形为下一个状态。
source-hash: 3532e6f81631
---

<Demo name="Status" />

## 何时使用

`TextMorph` 是一个会原地变化状态的简短标签。它保持一行，按文字量测宽度，并把保持不变的字母移动到新位置，因此状态变化读起来是同一个标签在更新，而不是一个新标签出现。它适合在人们一眼扫过时阅读的一行产品文案：状态、套餐名称或在用户注视时变化的价格。

- **发布看板上的部署状态**：流水线卡片上的标签从"部署"变为"部署中"，再变为"已部署"或"已回滚"。共享的字母保持原位，因此状态变化时卡片不会闪烁。
- **账单详情页头中的发票状态**："草稿"、"已发送"或"已付款"，旁边是编号 `inv_2026_0412`。在财务评审中，变化从房间另一端就能看见，无需轻提示或模态框。
- **套餐选择器中的价格**："$12 / 月"在用户切换计费周期时变为"$120 / 年"。配合 `PrefixSuffix`，只有金额变化；末尾的单位保持原位。
- **自定义控件内的标签**：徽标或分段标签随用户操作改变文字，此时普通的 `Button` 无法提供合适的外形或行为。

## 何时不用

- **可点击且有自身状态的控件**：用 `Button`。`TextMorph` 只是文字，本身没有点击、悬停或焦点行为。
- **复制后的确认**：用 `CopyButton`，它专为复制操作及其反馈而设计。
- **会折成多行的文字**：`TextMorph` 始终为单行。请用普通的 `TextBlock`，或用 `TextReveal` 处理只播放一次的标题。
- **表示持续工作的状态行**：用 `TextShimmer`。它在工作进行时在文字上扫过一道光；`TextMorph` 只在文字变化时移动字母。
- **需要完整阅读的消息**：用 `Toast`，它承载完整的句子并有自己的时间。变形的标签太短，无法解释发生了什么。
- **随时间计数增减的数字**：用 `AnimatedCounter`。每秒变化的数字会让字母一直在动，令人分心。

## 用法

将 `Text` 设为当前标签。控件加载后 `Text` 变化时，字母会滑动、升起或淡入到新标签。该控件派生自 Avalonia 的 `Control`，并自行绘制文字：它用文字布局量测每个字母并直接绘制，因此不会为每个字母创建子控件。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:TextMorph Text="部署" FontSize="28" />
</UserControl>
```

```csharp
status.Text = "部署中";
```

控件从 `TextElement` 获取字体与画笔，与文本块相同。直接设置在控件上的值优先于父元素上的值，主题设定了正文字体族，因此未设置样式的标签已与正文文案一致。

### 结构

1. **标签**（`Text`）：一行文字。字母逐个量测和绘制，因此文字不会被换行或截断。
2. **字体**（`FontFamily`、`FontSize`、`FontWeight`）：来自主题的正文字体族，字号和字重可在控件或父元素上设置。修改其中任何一项都会以新字号重绘标签，且无动画。
3. **前景**（`Foreground`）：绘制字母的画笔。与字体一样从 `TextElement` 继承。
4. **宽度**：控件与当前文字等宽。宽度以弹簧方式跟随变化，因此标签周围的空间（徽标边缘或按钮边缘）会随文字增长和收缩。
5. **动效区域**：进入的字母从下方上升 0.14em，离开的字母上移 0.1em。控件不裁剪其内容，因此这些移动在测量框之外仍然可见。

### 切换状态

状态词从一个状态切换到下一个状态。两个标签共有的字母会滑动到新位置。新字母以短暂的错落效果升起，被移除的字母则淡出。例如当任务状态从服务端到达时，在代码中将 `Text` 设为下一个状态。

```csharp
var words = new[] { "部署", "部署中", "已部署", "已回滚" };
var index = 0;
next.Click += (_, _) => status.Text = words[index = (index + 1) % words.Length];
```

状态列表应简短，并按用户阅读的顺序排列。在彼此无关的词之间跳转（例如"部署"与"已回滚"）动画仍然正确，但共有的字母很少，动效所传达的含义也较少。对于这类组合请直接切换，不使用动效。

### 与视图模型绑定

`TextMorph` 只负责显示。状态由视图模型持有，标签跟随其文字。将 `Text` 绑定到字符串属性；每次变化都会是一次变形，因此只在状态变化时设置该属性。

```csharp
public partial class DeploymentViewModel : ObservableObject
{
    [ObservableProperty]
    private string _statusLabel = "部署";

    public void MarkRunning() => StatusLabel = "部署中";

    public void MarkDone(bool succeeded) => StatusLabel = succeeded ? "已部署" : "已回滚";
}
```

```xml
<arc:TextMorph Text="{Binding StatusLabel}" FontSize="28" FontWeight="SemiBold" />
```

不要把 `Text` 绑定到每一帧或每个计时周期都变化的值。每次变化都会触发一次变形，因此每秒变十次的值会让字母一直处于运动中。

### 匹配方式

`Matching` 设置新旧文字之间如何配对字母。它接受 `TextMorphMatching` 值。

- `Occurrence`（默认）将每个字符与新文字中相同次序的字符配对。第二个 "i" 与新文字中的第二个 "i" 配对。适用于单词，字母会移动到词内的新位置。
- `PrefixSuffix` 保持共有的开头与结尾不动，只替换中间部分。适用于价格和计数，此时数字变化而单位或货币符号不应移动。

<Demo name="PrefixAndSuffix" />

匹配以单个字符为单位，因此字体会将其连在一起显示为一个图形的字符（连字，或组合表情符号）在变形时可能被拆开。依赖这类字符串之前，请在真实字体中检查。

### 字体

`TextMorph` 使用 `TextElement` 的字体与前景属性。可以在控件或其父元素上设置。

```xml
<arc:TextMorph Text="已保存" FontSize="16" FontWeight="SemiBold" />
```

为整行设置样式的父元素同样有效，因此列表中的状态标签共享同一字号：

```xml
<StackPanel TextElement.FontSize="14"
            TextElement.FontWeight="Medium"
            TextElement.Foreground="{arc:ArcDesignTokenResource TextSecondary}">
    <arc:TextMorph Text="草稿" />
</StackPanel>
```

### 示例：账单页头中的发票状态

账单详情页头显示发票编号、金额和状态标签。在用户处理发票时，只有状态会变化，因此它是唯一的 `TextMorph`。无论处于哪种状态，卡片宽度都保持一致，因此标签从"已发送"变为"等待付款"时页头不会移动。金额和到期日是保持不动的普通文字。

![带有变形状态标签和应付金额的发票页头](images/TextMorphInvoiceStatus.png)

```xml
<Border Width="560"
        Padding="20"
        CornerRadius="{arc:ArcDesignTokenResource RadiusPanel}"
        Background="{arc:ArcDesignTokenResource Surface}"
        BorderBrush="{arc:ArcDesignTokenResource Border}"
        BorderThickness="1">
    <StackPanel Spacing="12">
        <TextBlock Text="Invoice inv_2026_0412"
                   FontSize="13"
                   Foreground="{arc:ArcDesignTokenResource TextSecondary}" />
        <arc:TextMorph Text="Awaiting payment"
                       FontSize="22"
                       FontWeight="SemiBold" />
        <Grid ColumnDefinitions="*,Auto">
            <TextBlock Grid.Column="0"
                       Text="Due 2026-11-05"
                       Foreground="{arc:ArcDesignTokenResource TextSecondary}" />
            <TextBlock Grid.Column="1"
                       Text="$4,800.00"
                       FontWeight="SemiBold"
                       Foreground="{arc:ArcDesignTokenResource Foreground}" />
        </Grid>
    </StackPanel>
</Border>
```

### 内容规范

变形标签按设计应当简短：一个词或一个简短短语，句首大写，末尾不加句号。各状态应属于同一类词，例如对象的状态，这样变化读起来是状态，而不是句子。数字用阿拉伯数字书写，并让单位与数字放在一起。

| 不要写 | 改为 |
|---|---|
| "你的发票现已发送给客户。" | "已发送" |
| "全部付清" | "已付款" |
| "正在部署……" | "部署中" |
| "价格：每年 120 美元" | "$120 / 年" |
| "因故障而回滚" | "已回滚" |

### 最佳实践

**推荐**

- 每个状态保持一到两个词。过长的标签会让宽度弹簧变化明显，并推动相邻的内容。
- 将标签放在宽度固定的容器中，或放在足够宽的 `Grid` 列中，以容纳最长的状态。控件宽度随文字变化，因此不设限的标签会推动其旁边的内容。
- 在价格和计数上使用 `PrefixSuffix`，使标签中稳定的部分保持原位。
- 只在状态变化时修改标签。每次变化都是用户需要跟随的动效，因此应尽量少。
- 当新状态很重要时，在父元素上用实时区域宣布变化。控件本身不会播报。

**避免**

- 将标签绑定到每个计时周期都变化的值，例如计时器或进度百分比。字母永远无法稳定，进度值更适合用 `Progress` 显示。
- 把 `TextMorph` 用作有点击行为的按钮标签。控件本身没有点击或焦点行为。
- 长句或换行的文案。控件是一行，过长的状态会推动其周围的布局。
- 依赖动效来传达变化。减弱动效和关闭动效会直接跳到新标签，因此文字本身必须已经说明了变化的内容。
- 在控件加载前设置标签并期望它有动画。首次文字会无动画地出现。

### 常见问题

**为什么我的标签直接跳变而不变形？** 标签只有在加载完成后才会变形，并且只在动效级别为 `Full` 时变形。在 `Reduced` 或 `Off` 下，变化会立即替换标签。修改 `FontSize`、`FontWeight` 或 `FontFamily` 也会直接跳到新字号，因为字母是重新量测而非移动的。

**状态变化时，标签旁边的一行为什么会移动？** 控件宽度以弹簧方式跟随其文字，因此较长的状态会把其旁边的控件推开。可在 `Grid` 列中为标签设置固定宽度，或测量最长的状态并预留该宽度。

**将 `Text` 设为相同的值时，标签会变形吗？** 不会。变形只在文字变化时开始；再次设置相同的字符串会让字母留在原位。

**标签能换成两行吗？** 不能。控件始终为一行。需要两行的文字，标题请用 `TextReveal`，正文请用普通文本块。

**如何让字母以另一种动效移动？** 动效由控件固定。它的时间与距离都已内置，而窗口或容器上设置的动效级别（`arc:ArcMotion.Level`）决定它是否播放。选定状态与对应的匹配模式，其余交给控件即可。

## API 参考

<!-- api: TextMorph -->

## 无障碍

- 当前文字即为控件的名称，辅助技术读取的是纯文本。
- 字母对辅助技术隐藏，因此变化不会被逐字母朗读。
- 控件本身不播报变化。需要播报新状态时，请在父元素上使用实时区域。
- 在 `Reduced` 和 `Off` 动效下，新文字会立即出现，因此仅靠文字本身必须能传达变化。
- 本控件不可获得焦点，也没有键盘交互，因此不会为页面增加焦点停靠点。

## 动效

- 共有的字母在弹簧作用下滑动到新位置。
- 新字母从 80% 的尺寸开始放大并升起，同时淡入，依次出现。第一个在 50 ms 后开始，之后每个字母间隔 32 ms，最多八个字母。
- 被移除的字母在轻微缩小并稍微上移的同时淡出，然后消失。其退场为一个较短的标准淡出。
- 容器宽度以弹簧方式跟随新标签变化。字体或容器尺寸变化时，宽度无动画地直接跳到新值。
- 减弱动效时，标签与宽度立即替换。关闭动效时同样如此。

| 变化 | 完整 | 减弱 | 关闭 |
|---|---|---|---|
| 共有字母移动 | 在弹簧作用下滑动到新位置 | 跳变 | 跳变 |
| 新字母出现 | 上升 0.14em 并淡入，错落进行 | 跳变 | 跳变 |
| 被移除的字母 | 淡出，上移 0.1em，然后离开 | 立即移除 | 立即移除 |
| 标签宽度 | 以弹簧方式变为新宽度 | 跳变 | 跳变 |
| 字体、字重或字号变化 | 直接跳到新字号 | 跳变 | 跳变 |
| 控件加载前设置的文字 | 立即显示，无动效 | 立即显示 | 立即显示 |

## 相关组件

- `Button`：用于可点击且有自身状态的控件。
- `TextShimmer`：表示持续工作的状态行。
- `TextReveal`：出现时只播放一次的标题。

| 组件 | 适用场景 |
|---|---|
| `Button` | 标签属于一个可点击的控件 |
| `CopyButton` | 标签确认某内容已被复制 |
| `TextShimmer` | 状态行显示仍在进行的工作 |
| `TextReveal` | 标题出现时应播放一次 |
| `AnimatedCounter` | 数字随时间增加或减少 |
| `Toast` | 必须阅读完整句子，例如某操作的结果 |
