---
title: 模型选择器
summary: 在一个紧凑的胶囊中选择模型及其推理强度：精选模型各附一句说明，推理强度可以调整，其余模型按提供方分组，概览中列出该选择的费用。
source-hash: 4e7c68136a56
---

<Demo name="Preview" />

## 何时使用

模型选择器让用户决定由哪个模型作答，以及它要思考多久。它是一个胶囊，显示当前的选择；胶囊后面是一个菜单；此外还可以选择性地显示一段概览，说明所选模型能容纳多少内容、费用如何。它应放在选择确实重要、却不该在用户去找它之前占用空间的地方。

- **AI 功能的设置页**：支持助手或起草工具的默认模型。胶囊下方的概览显示上下文大小和每百万词元的价格，使选择有据可依。
- **带有少数精选模型的模型目录**：三到四个精选模型，各附一句说明（"Quick and capable for daily chats"），其余模型则在「更多模型」下按提供方分组。
- **与模型相关的推理强度控制**：有些模型在被要求时会推理更久。只有提供档位的模型才会出现 "Effort" 行；下一个模型没有的档位会移到最接近的一档。
- **标题栏或工具栏中的小控件**：把 `IsSummaryVisible` 设为 false，选择器就只剩胶囊，适合放在聊天顶部或提示输入框旁边。
- **按用量计费的产品**：概览会打印输入与输出价格，其下的一行小字说明它们所用的货币和单位。

## 何时不用

- **简短且固定、无需解释的选择**，例如小、中、大：使用 `SegmentedControl` 或 `Select`。
- **多个模型并排比较**：选择器一次只显示一个模型。当人们需要比较价格和上限时，用 `Table` 制作表格。
- **在数百个模型中按名称搜索**：菜单没有搜索框。使用 `Combobox`，并过滤你自己的列表。
- **与模型本身无关的模型设置**，例如温度或停止词：把它们放进由 `NumberField` 和 `Switch` 组成的表单。选择器只负责选择模型及其推理强度。
- **运行模型**：选择器不调用任何东西。它只报告选择；由你的代码决定如何处理。

## 用法

`ModelPicker` 派生自 Avalonia 的 `TemplatedControl`。它的模板包含一个 `ModelPill`、一个 `ModelSummary` 和一个隐藏的 `ModelMenu`。选择器持有状态：你向它提供模型，并读回所做的选择。`AiModel` 是一个 `AvaloniaObject`，包含名称、用于精选列表的单行 `Blurb`、用于概览的 `Description`、上下文大小和价格，以及它提供的推理强度档位。提供方对「更多模型」列表进行分组，并为没有标识的模型提供标识。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:ModelPicker SelectedModelId="aurora-prime-3" SummaryNote="Prices in USD per 1M tokens">
        <arc:ModelPicker.Providers>
            <arc:AiModelProvider Id="aurora" Name="Aurora Labs">
                <arc:AiModelProvider.Mark>
                    <StreamGeometry>M2 19a10 10 0 0 1 20 0h-3.2a6.8 6.8 0 0 0-13.6 0Z</StreamGeometry>
                </arc:AiModelProvider.Mark>
            </arc:AiModelProvider>
        </arc:ModelPicker.Providers>
        <arc:AiModel Id="aurora-prime-3" Name="Aurora Prime 3" ProviderId="aurora"
                     Blurb="Deep work: refactors, research, long tasks"
                     Description="A careful planner for multi-step coding and research."
                     ContextTokens="800000" InputPrice="6" OutputPrice="24" />
        <arc:AiModel Id="aurora-pocket-24" Name="Aurora Pocket 2.4" ProviderId="aurora" Tag="Fast"
                     ContextTokens="400000" InputPrice="0.4" OutputPrice="1.6" />
    </arc:ModelPicker>
</UserControl>
```

推理强度档位是一个列表，因此请在代码中设置（XAML 没有数组字面量）：

```csharp
prime.EffortLevels = [ModelEffort.Low, ModelEffort.Medium, ModelEffort.High, ModelEffort.ExtraHigh, ModelEffort.Max];
prime.DefaultEffort = ModelEffort.Medium;
```

前四个模型为精选模型。`FeaturedIds` 按你给出的顺序挑选其他模型。其余模型显示在「更多模型」下，并按 `Providers` 的顺序分组。没有选择时，选择器会一次性选中第一个精选模型，并把它写回 `SelectedModel`。

### 结构

1. **胶囊**：高 40 px，圆角为全圆，带 1 px 边框。它显示模型的标识（16 px，无底衬）、中等字重的名称、有推理强度时以弱化文字颜色显示的强度，以及一个在菜单打开时翻转的箭头。悬停和打开时使用弱化表面与较强的边框。键盘焦点环只在键盘操作时出现。
2. **菜单**：宽 304 px（窗口较窄时为窗口宽度减 24 px），位于抬起的表面上，使用面板圆角和浮动阴影。它在胶囊下方 8 px 处打开。
3. **精选行**：高 54 px。每行有标识（18 px）、名称、次要颜色的说明，以及所选模型上的勾选标记。
4. **"Effort" 行**：高 40 px，右侧显示当前档位。所选模型没有档位时隐藏。
5. **「更多模型」行**：当所选模型不在精选中时，在右侧显示其名称。所有模型都已精选时隐藏。
6. **子菜单**：推理强度列表宽 264 px，每个档位有名称、说明和勾选标记，模型的默认档位之后标注 "Default"。其他模型列表宽 272 px，最高 420 px，可以滚动，并把紧凑行（38 px）按提供方名称分组，行末显示模型的 `Tag`。
7. **概览**：模型说明、一个由 Context、Input、Output 和 Effort 组成的表格、一段可选的备注，以及一行确认提示。

### 选择模型、推理强度及其他

按下胶囊，或在它获得焦点时按 `Enter`、`Space` 或 `ArrowDown`，即可打开菜单。一个高亮随指针移动，并随方向键跳动。选择一个模型即可选中它、关闭菜单，并把焦点返回胶囊。选择 "Effort" 或「更多模型」会在右侧打开子菜单；右侧没有空间时改在左侧打开，两侧都没有空间时则以页面形式打开。选择已经选中的模型会关闭菜单，不做任何改变。

不支持当前推理强度的模型会把它移到最接近的一档，若两档距离相等则取较低的一档。选择器会在确认提示中说明，例如 "Aurora Prime 3 → Cobalt Atlas 2 · Effort adjusted to High"。没有档位的模型没有推理强度（`Effort` 为 null），"Effort" 行随之消失。

### 窄窗口中的页面

当窗口宽度不超过 `PagedMenuWidth`（默认 640）时，"Effort" 行和「更多模型」行会在菜单内打开页面，而不是弹出子菜单。每个页面顶部有一行返回行，内容从移动方向一侧滑入，菜单的高度随页面变化。示例把 `PagedMenuWidth` 设得很宽，因此在宽窗口中也能看到页面。

<Demo name="Pages" />

### 概览与确认

概览显示所选模型的 `Description` 以及四项信息：Context（`ContextTokens`，例如 "800K tokens"）、Input 和 Output（每百万词元的 `InputPrice` 与 `OutputPrice`，附带 `CurrencySymbol`），以及 Effort。没有档位的模型显示 "Fixed"。模型未填写的信息显示为短横线。在 400 px 或更窄的宽度下，这些信息会折叠为两行，每行两项；三行的说明会让概览变高，但不会移动胶囊。

在菜单中做出选择后，确认提示会出现在信息下方：模型变更时为 "Aurora Prime 3 → Cobalt Atlas 2"，推理强度变更时为 "Effort: Medium → High"。它保留 `NoticeDuration`（2.6 秒）后淡出。其空间始终预留，因此下方的内容不会移动。`SummaryNote` 是信息下方的小字，用来说明单位，以及价格是否为真实价格。

### 只剩胶囊

将 `IsSummaryVisible` 设为 false，选择器就只剩胶囊。菜单、键盘行为、事件和播报都与之前相同，因此胶囊可以放在标题栏或工具栏中。

<Demo name="PillOnly" />

### 读取与设置选择

`SelectedModel`、`SelectedModelId` 和 `Effort` 都是双向的。在菜单中做出选择、切换到模型支持的档位，或在代码中修改时，`ModelChanged` 与 `EffortChanged` 会被触发。推理强度事件上的 `IsSnap` 在新模型使推理强度发生移动时为 true。`SelectCommand` 与 `EffortCommand` 会携带新值运行，供偏好命令的视图模型使用。

<Demo name="Bindings" />

```csharp
public sealed class PickerSettings
{
    public string? ModelId { get; set; }

    public ModelEffort? Effort { get; set; }
}

picker.ModelChanged += (_, e) => settings.ModelId = e.NewModel?.Id;
picker.EffortChanged += (_, e) => settings.Effort = e.NewEffort;

// An id the catalog does not have yet waits for it.
picker.SelectedModelId = "cobalt-atlas-2";
```

```xml
<arc:ModelPicker SelectedModel="{Binding Model, Mode=TwoWay}"
                 Effort="{Binding Effort, Mode=TwoWay}"
                 ModelsSource="{Binding Catalog}"
                 IsOpen="{Binding IsPickerOpen, Mode=TwoWay}" />
```

- `ModelsSource` 跟随一个可以变化的集合；它取代内联的 `Models`。
- `IsOpen` 是双向的。在选择器显示到屏幕上之前设置它，会在显示时打开菜单一次。`Open()` 和 `Close()` 在代码中做同样的事；`Open(ModelMenuOpenFocus.Selected)` 会让键盘位置从所选模型开始。
- 把 `Effort` 设为所选模型不提供的档位，会立即修正它，并且事件会报告实际使用中的档位。

### 文案

`Labels` 保存选择器显示或朗读的每一个词，类型为 `ModelPickerLabels` 对象，并带有英文默认值。它包含胶囊、菜单和概览各自的部分，以及确认提示和播报。赋值一个新实例即可修改它们。档位名称出现在四个位置，因此请让每个部分使用同一个函数。

<Demo name="CustomWording" />

```csharp
picker.Labels = new ModelPickerLabels
{
    EffortName = effort => effort == ModelEffort.Max ? "Maximal" : ModelEffortNames.Name(effort),
    ModelNotice = (previous, next) => $"{previous} → {next}",
    Menu = new ModelMenuLabels { MoreModels = "Weitere Modelle", Effort = "Aufwand" }
};
```

### 标识

模型的 `Mark` 可以是一个在 24 × 24 方框中绘制、并以 `MarkColor` 填充（或以文字颜色填充，文字颜色随主题翻转）的 `Geometry`，也可以是一个 `IImage`，例如保留自身颜色的 `DrawingImage`。没有 `Mark` 时，模型使用其提供方的标识。标识仅作装饰；名称始终紧挨着它。请使用你自己的图形：选择器从不在标识周围添加底衬或边框。

### 文案规范

- **Blurb**：一行，说明模型擅长什么，例如 "Reads charts, screenshots and PDFs"。它会以省略号截断，因此把有用的词放在前面。
- **Description**：一到两句，用于概览。说明它用来做什么，以及人需要权衡什么。
- **Tag**：一到两个词，例如 "Fast" 或 "Open weights"。它是纯文本，而不是徽章。
- **价格**：只有在真实时才显示真实数值。对占位价格设置 `IsSamplePrice`，并在 `SummaryNote` 中注明。
- **名称**：使用你的产品中用户看到的名称，并把精选列表控制在三到五个模型。

| 不要写 | 改为 |
|---|---|
| 全能的最佳模型 | 深度工作：重构、研究、长任务 |
| 便宜 | 快速且能干的日常聊天 |
| 上下文为 1000000 词元的模型 | 1M 词元上下文（概览会自动格式化） |

### 最佳实践

**推荐**

- 为每个模型设置稳定的 `Id`，并存储该 id，而不是显示名称。
- 把 `EffortLevels` 与 `DefaultEffort` 放在同一个模型上；默认值必须是其中一个档位。
- 把选择器放在选择所作用的位置，例如聊天的标题栏，或它所影响的字段旁边。
- 处理 `ModelChanged` 和 `EffortChanged` 以保存选择；选择器只把它保存在内存中。
- 传入 `Providers`，使「更多模型」列表分组，并让没有标识的模型使用其提供方的标识。

**避免**

- 在不属于你自己的设计中使用真实的厂商标志。
- 精选模型超过五个。菜单就不再是一个简短的列表了。
- 隐藏模型无法改变的推理强度：把 `EffortLevels` 留空，"Effort" 行会自行消失。
- 在每次更新时调用 `Open()`。它会把键盘焦点移入菜单。

### 常见问题

**为什么没有搜索？** 菜单是一个简短的列表，加上按提供方分组的长尾。对于很长的目录，请使用 `Combobox` 和你自己的过滤。

**为什么我选择另一个模型时档位会变化？** 新模型不提供该档位。选择器会把推理强度移到该模型提供的最接近的一档，并在确认提示中说明。

**菜单可以在悬停时打开吗？** 不可以。胶囊在按下时打开，与菜单按钮相同，因此指针经过时不会打开任何东西。

**为什么概览显示 "Fixed"？** 所选模型不提供推理强度档位。可以用 `Labels.Summary.NotAdjustable` 修改这段文字。

## API 参考

<!-- api: ModelPicker -->

<!-- api: AiModel -->

<!-- api: AiModelProvider -->

### ModelEffort

| 值 | 名称 | 说明 |
|---|---|---|
| `Low` | Low | 回复简短，延迟最低 |
| `Medium` | Medium | 日常的平衡 |
| `High` | High | 能处理较难的问题 |
| `ExtraHigh` | Extra high | 长时间、细致的推理 |
| `Max` | Max | 详尽，速度最慢 |

`ModelEffortNames.Name(effort)` 和 `ModelEffortNames.Description(effort)` 返回这些词，`ModelEffortSnap.Snap` 会选出模型提供的档位。

### ModelPickerLabels

| 属性 | 默认值 | 显示位置 |
|---|---|---|
| `Pill` | `ModelPillLabels` | 胶囊的无障碍名称 |
| `Menu` | `ModelMenuLabels` | 菜单的各行、页面和无障碍名称 |
| `Summary` | `ModelSummaryLabels` | 概览的信息标签与值 |
| `EffortName`（档位的函数） | "Medium" | 确认提示与播报 |
| `ModelNotice`（新旧名称的函数） | "A → B" | 模型变更后的确认提示 |
| `EffortNotice`（新旧档位的函数） | "Effort: Medium → High" | 推理强度变更后的确认提示 |
| `ModelAdjustedNotice` | "A → B · Effort adjusted to High" | 推理强度也被调整时的确认提示 |
| `ModelAnnouncement` | "Now using Aurora Prime 3, medium effort" | 模型变更后的礼貌播报 |
| `EffortAnnouncement` | "Aurora Prime 3, high effort" | 推理强度变更后的礼貌播报 |

### ModelMenuLabels、ModelPillLabels 和 ModelSummaryLabels

| 属性 | 默认值 | 显示位置 |
|---|---|---|
| `ModelMenuLabels.Menu` | "Models" | 菜单的无障碍名称 |
| `ModelMenuLabels.Featured` | "Featured models" | 精选组的无障碍名称 |
| `ModelMenuLabels.Effort` / `MoreModels` | "Effort" / "More models" | 精选列表下方的两行 |
| `ModelMenuLabels.EffortMenu` / `MoreModelsMenu` | "Effort" / "More models" | 子菜单名称与页面标题 |
| `ModelMenuLabels.Default` | "Default" | 模型默认档位之后的标注 |
| `ModelMenuLabels.BackTo` | "Back to featured models, Effort" | 返回行的无障碍名称 |
| `ModelMenuLabels.Empty` | "No models available" | 空菜单中唯一的一行 |
| `ModelPillLabels.AutomationName` | "Model: Aurora Prime 3, medium effort. Change model" | 胶囊的无障碍名称 |
| `ModelSummaryLabels.Region` | "Selected model" | 概览的无障碍名称 |
| `ModelSummaryLabels.Context` / `Input` / `Output` / `Effort` | "Context" / "Input" / "Output" / "Effort" | 信息标签 |
| `ModelSummaryLabels.NotAdjustable` | "Fixed" | 没有档位的模型的推理强度信息 |
| `ModelSummaryLabels.ContextValue` | "800K tokens" | 上下文信息 |

## 无障碍

- 胶囊是一个按钮，名称由模型和推理强度组成，例如 "Model: Aurora Prime 3, medium effort. Change model"，并报告菜单是否打开。
- 菜单具有 menu 角色。精选模型和推理强度档位是带有选中状态的单选项；"Effort" 行和「更多模型」行报告它们会打开子菜单；提供方分组以提供方名称命名。
- 选择之后，一个礼貌的实时区域会播报 "Now using Aurora Swift 3, medium effort"（推理强度变更时则播报对应的句子）。屏幕阅读器听到的不是可见的确认提示。
- 菜单关闭时焦点返回胶囊。键盘关闭后，胶囊显示焦点环；指针关闭后则不显示。
- 概览是一个带标签的区域，每项信息读作一个短语，例如 "Context: 800K tokens"。
- 标识是装饰性的。
- 减弱动效会保留每一种状态和每一条播报。

### 键盘

| 按键 | 操作 |
|---|---|
| 胶囊上的 `Enter`、`Space` 或 `ArrowDown` | 打开菜单，并高亮所选模型 |
| 菜单中的 `ArrowUp` / `ArrowDown` | 在各行之间移动；到达两端时循环 |
| `Home` / `End` | 第一行或最后一行 |
| 一个字母 | 跳到下一个名称以它开头的行；重复按则循环 |
| 模型或档位上的 `Enter` 或 `Space` | 选择它，关闭菜单并把焦点返回胶囊 |
| "Effort" 或「更多模型」上的 `Enter`、`Space` 或 `ArrowRight` | 打开子菜单，并高亮所选项 |
| `ArrowLeft` | 关闭子菜单（或返回上一页），并回到其所在的行 |
| `Escape` | 关闭整个菜单，并把焦点返回胶囊 |
| `Tab` | 在菜单内不做任何事 |

## 动效

- 菜单从胶囊中生长出来：起始时朝胶囊方向偏移 6 px、缩放为 0.97，在 160 ms 内淡入，并在 580 ms 内以 Arc 的弹簧曲线稳定下来。它以 130 ms 离开，距离减半，缩放为 0.985。打开过程中关闭、或关闭过程中打开，都会从表面当前的位置继续。
- 一个高亮在各行之间移动：指针使用灵敏的弹簧滑动，键盘则直接跳动；它出现的位置以 80 ms 淡入，消失时以 80 ms 淡出。
- 子菜单以相同方式从所在行的一侧进入和离开。移到另一行会立即关闭已打开的子菜单；朝子菜单方向的斜向移动则不会。
- 在窄窗口中，新页面从移动方向一侧滑入 12 px，同时在 160 ms 内淡入，菜单的高度在平滑弹簧上跟随页面变化。
- 选择之后，胶囊的标识和文字在 100 ms 与 160 ms 内交叉淡入淡出，其宽度在快速弹簧上跟随新文字。概览会交叉淡入淡出其说明以及发生变化的信息项。确认提示淡入、保留 2.6 秒，然后淡出。
- 减弱动效会去掉移动和弹簧，保留 120 ms 的淡入淡出。关闭动效时，每项变化都立即生效。

| 变化 | 完整 | 减弱 | 关闭 |
|---|---|---|---|
| 菜单打开或关闭 | 从胶囊生长；160 ms 淡入，580 ms 弹簧 | 120 ms 淡入 | 立即完成 |
| 高亮 | 滑动，80 ms 淡入和淡出 | 直接跳动，120 ms 淡入 | 立即完成 |
| 窄窗口中的页面 | 滑入 12 px 并淡入；高度弹簧 | 120 ms 淡入 | 立即完成 |
| 胶囊更换模型 | 标识与文字交叉淡入淡出；宽度弹簧 | 120 ms 交叉淡入淡出 | 立即完成 |
| 概览变化 | 说明与变化的信息交叉淡入淡出 | 120 ms 交叉淡入淡出 | 立即完成 |
| 确认提示 | 淡入，停留 2.6 秒，淡出 | 120 ms 淡入淡出 | 立即完成 |

## 相关组件

- `Select` 和 `Combobox`：用于从一个与模型无关的列表中选择一项。
- `DropdownMenu`：用于操作的菜单。
- `SegmentedControl`：用于一个始终可见的短选择。

| 组件 | 适用场景 |
|---|---|
| `Select` / `Combobox` | 列表很长、需要搜索，或与模型无关 |
| `DropdownMenu` | 菜单列出的是操作，而不是保持选中的选择 |
| `SegmentedControl` | 需要同时看到少数几个选项 |
| `Popover` | 从按钮打开一个小表单或面板 |
