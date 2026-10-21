---
title: 颜色选择器
summary: 色块展开为选择器，支持格式切换、吸管、已保存色块和对比度读数。
source-hash: 7dccad862e49
---

<Demo name="ColorField" />

## 何时使用

`ColorPicker` 在紧凑的字段中提供完整的颜色编辑器。收起时，控件是一个带标签和 HEX 值的色块；展开后会显示饱和度与明度区域、色相与不透明度滑块、可接受四种表示法的文本框、对比度读数以及一排已保存的颜色。它适用于颜色是使用者必须细致调整的真实设置的场景，而不是从一个短列表中做选择。

- **多租户管理后台中的品牌设置**：管理员为某个客户的账单和登录页设置主色。色块在设置列表中显示当前品牌色，面板可以粘贴客户样式指南中的 HEX 值。
- **主题与设计令牌编辑器**：产品团队编辑背景色和强调色，包括半透明的叠加层，其中 Alpha 通道是值的一部分。OKLCH 格式让他们手动调整明度和彩度。
- **内容工具中的文字颜色检查**：编辑者选择文字颜色，并读取它与所在页面背景之间的 WCAG 对比度。等级（AAA、AA、AA large 或 Fails）会显示文字在发布前是否清晰可读。
- **图表与仪表板的系列颜色**：分析师为每个图表系列分配颜色。已保存的颜色保留团队已经约定的配色，因此每个新系列都从同一组颜色中选取。
- **营销工具中的已保存品牌色块**：活动搭建者最多保留七种颜色，团队在横幅和邮件中复用，最新的颜色排在最前。

## 何时不用

- **少量固定颜色**：选项是“浅色”“深色”或三种状态色这类有名称的选择时，用 `SegmentedControl` 或 `RadioGroup`。使用者只是挑选一个，不会调整颜色。
- **没有选择器的普通 HEX 值**：值是输入或粘贴的，使用者从不需要色彩区域或滑块时，用 `Input`，例如从文档复制到 Webhook 设置中的 API 令牌颜色。
- **用于筛选列表的小型预设颜色标签**：“红色”“蓝色”或“未分配”这类用于筛选发票或工单的标签，用 `ChipGroup`。标签组只在预设中选择，不会产生任意的值。
- **使用者不应修改、由品牌色板派生的颜色**：在它所属的设置旁边，用只读的 `Input` 显示该值。禁用的选择器仍会占据空间，并让人疑惑为何被锁定；应改为在说明文字中写明规则。
- **日期、数字或度量**：有单位的值用 `NumberField`、`DatePicker` 或 `Slider`。颜色有三到四个通道，选择器需要让它们保持同步；这些控件只保存一个数字。

## 用法

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:ColorPicker Label="强调色"
                     Color="#2F6BFF"
                     Format="Hex" />
</UserControl>
```

`ColorPicker` 派生自 Avalonia 的 `TemplatedControl`，并实现 Arc 选择器的自动化契约，因此它支持常见的布局、`IsEnabled`、焦点与样式属性。它的值是 Avalonia 的 `Color`，因此同一个颜色无需转换即可用于画笔、资源与视图模型。标签、说明与错误文案都来自共享的字段基础，所以选择器能与表单中的其他字段对齐。

### 结构

1. **触发器**：44 px 的胶囊形，圆角为 22 px。内含 32 px 的色块（颜色为半透明时，色块下方显示棋盘格）、12 px 文字的 `Label` 和 14 px 文字的 `HexValue`，色块与文字之间留 10 px 间距。左对齐，宽度随内容而定。
2. **说明**（`Description`）：触发器下方的辅助文案，使用弱化的文字颜色。
3. **错误**（`ErrorMessage`）：触发器下方的危险色文案。内容非空时，选择器被标记为无效。
4. **面板头部**：面板展开时，重复显示色块、标签和 HEX 值，右侧是 32 px 的“完成”按钮。面板最宽 304 px，且不会超过窗口宽度减 32 px。
5. **饱和度与明度区域**：高 164 px，圆角 16 px。饱和度从左到右递增，明度从下到上递增，滑块指示当前的组合。
6. **色相与不透明度滑块**：区域下方是两条 14 px 的轨道。不透明度轨道会在棋盘格上显示该颜色。
7. **值输入框**：一行 40 px 高的区域，圆角 14 px。起始处的格式按钮（HEX、RGB、HSL 或 OKLCH）宽度至少 56 px。文本框占据该行的其余部分；输入无效时，其下方显示提示信息。
8. **对比度行**：36 × 28 px 的色样，圆角 9 px，显示 `ContrastBackground` 上的颜色；随后是比值文字、22 px 胶囊中的等级，以及“相对于背景”字样（视口宽 360 px 及以下时隐藏）。
9. **已保存色块**：一排已保存的颜色，每个都是小方块，末尾是虚线边框的“保存”按钮。

面板从触发器向下展开，并从触发器中生长出来。只有当窗口边缘会裁切它时，面板才会向左或向上移动；页面滚动到底部时，页面会先滚动以腾出空间。

### 值与格式

`Color` 是 Avalonia 的 `Color`，且为双向绑定，是需要绑定的值。`Format` 设置文本框中显示的表示法，格式按钮会在面板中循环切换四种表示法。在代码中设置 `Format` 只会改变表示法，不会改变颜色。

`HexValue` 为只读，以大写的 CSS 十六进制给出颜色：`#RRGGBB`，颜色为半透明时为 `#RRGGBBAA`。用于显示、复制和导出。它的顺序与 Avalonia 自身的字符串形式不同：Avalonia 的 `Color.Parse` 把八位数值读作 `#AARRGGBB`，因此应把 CSS 值交给选择器的文本框，或自行转换。

| 格式 | #2F6BFF 的示例输出 | 说明 |
|---|---|---|
| `Hex` | `#2F6BFF` | 默认值。半透明颜色会多出两位：`#2F6BFF80`。 |
| `Rgb` | `rgb(47 107 255)` | 以空格分隔。半透明颜色以相同形式追加 `/ 50%`。 |
| `Hsl` | `hsl(223 100% 59%)` | 色相以度数表示，饱和度与明度以百分比表示。 |
| `Oklch` | `oklch(56.2% 0.214 263.5)` | 感知明度与彩度；当两种颜色必须看起来一样亮时很有用。 |

文本框接受相同的四种表示法，且输入时很宽容：三位与四位的十六进制、`rgb()` 与 `rgba()`、带或不带 `deg` 的 `hsl()` 与 `hsla()`，以及 `oklch()`，并同时支持现代的空格分隔形式和逗号分隔形式。其他输入都会被拒绝，见下文的校验一节。

```xml
<arc:ColorPicker Label="品牌色" Color="#E5484D" Format="Oklch" />
```

```csharp
picker.ColorChanged += (_, e) => status.Text = $"颜色：{e.Hex}";
```

事件参数包含变更前的颜色、变更后的颜色和 CSS 十六进制值：

| 成员 | 类型 | 含义 |
|---|---|---|
| `OldColor` | `Color` | 变更前的颜色。 |
| `NewColor` | `Color` | 变更后的颜色。 |
| `Hex` | `string` | 新颜色的大写 CSS 十六进制，半透明时包含透明度的两位。 |

`Color` 每次变化时都会触发 `ColorChanged`，包括拖动手柄时指针的每一次移动。若每次移动都向服务端保存，会产生大量请求；应在面板关闭时保存（`IsDropDownOpen` 变为 `False` 时），或为处理程序加上防抖。

### 已保存色块

`Swatches` 是 `IReadOnlyList<ColorSwatch>?`。`ColorSwatch` 是带有 `Id` 和 `Color` 的记录；用 `ColorSwatch.Create(color)` 创建，它会获得新的标识。列表按最新在前排列。“保存”按钮把当前颜色加到最前，当列表长于 `MaxSwatches`（默认为 7）时，最早的色块会被移除。色块可以通过拖动，或用 `Alt` 加方向键重新排序，并可用 `Delete` 或 `Backspace` 删除。

每次变更都会生成新的列表，因此视图模型应双向绑定 `Swatches`，并替换列表而不是修改它。在面板中进行保存、重排序和删除时会触发 `SwatchesChanged` 事件。在代码中设置 `Swatches` 只会更新属性和色块行，不会触发该事件。

<Demo name="FormatsAndSavedSwatches" />

每个色块在重新排序时都会保留其 `Id`，因此视图模型可以跟踪某个色块的移动。

### 限制与吸管

`ColorPicker` 不提供吸管。Avalonia 没有跨平台的 API 可以从屏幕读取颜色，而且在 macOS 上尝试这样做需要屏幕录制权限。需要吸管的设计工具，应从自己的画布或剪贴板中获取颜色，并把 HEX 值粘贴到字段中。

### 对比度与无障碍

`ContrastBackground` 是所选颜色将要放置其上的颜色。读数使用 WCAG 对比度比值（范围 1 到 21）将该值与之比较，并以等级标注结果：

| 比值 | 等级 | 用途 |
|---|---|---|
| 7 或以上 | AAA | 任意字号的正文 |
| 4.5 或以上 | AA | 正文 |
| 3 或以上 | AA large | 仅用于大号文字与界面图形 |
| 低于 3 | Fails | 不要用于文字 |

默认背景为白色。应把它设为颜色实际所在的表面，例如卡片颜色或深色页面颜色，因为比值的可靠程度取决于所测量的背景。`ContrastRatio` 与 `ContrastGrade` 在代码中提供相同的值，因此表单无需打开面板就能对不合格的文字颜色发出警告。

<Demo name="Contrast" />

### 校验与错误状态

`ErrorMessage` 非空时，字段会被标记为无效。使用者按下 Enter 时，若输入的文本不是颜色，会被拒绝：字段抖动，出现危险色边框，面板中显示“请输入 HEX、RGB、HSL 或 OKLCH 颜色”这段文字（设置 `InvalidColorText` 可更改它）。在输入有效值之前，选择器会保留最后一个有效颜色。

`ErrorMessage` 属于应用本身。用它表达选择器无法得知的规则，例如颜色与必填元素的背景过于接近，并用平实的语言说明规则。`IsEnabled="False"` 会禁用选择器；它仍会显示色块，因此被锁定的值依然能显示当前生效的颜色。

```xml
<arc:ColorPicker Label="已锁定" IsEnabled="False" />
```

### 绑定到视图模型

租户的品牌设置是视图模型中的一个属性，包含一个颜色和一个已保存色块的列表。选择器对两者都做双向绑定，何时保存由视图模型决定。

```csharp
public interface IBrandSettingsStore
{
    Task SaveBrandAsync(Color primary, IReadOnlyList<Color> saved);
}

public partial class BrandSettingsViewModel(IBrandSettingsStore settings) : ObservableObject
{
    [ObservableProperty]
    private Color _primaryColor = Color.Parse("#2F6BFF");

    [ObservableProperty]
    private IReadOnlyList<ColorSwatch> _savedColors = [];

    [RelayCommand]
    private async Task SaveAsync()
    {
        await settings.SaveBrandAsync(PrimaryColor, SavedColors.Select(s => s.Color).ToList());
    }
}
```

```xml
<StackPanel Width="304" Spacing="16">
    <arc:ColorPicker Label="主色"
                     Color="{Binding PrimaryColor, Mode=TwoWay}"
                     Swatches="{Binding SavedColors, Mode=TwoWay}"
                     Description="按钮、链接与焦点环。" />
    <arc:Button Content="保存品牌色" Command="{Binding SaveAsyncCommand}" />
</StackPanel>
```

`Color`、`Swatches` 和 `Format` 是双向属性。`HexValue`、`ContrastRatio` 和 `ContrastGrade` 是单向读取的值，视图模型可以用来显示。`IsDropDownOpen` 也是双向的，因此使用者离开设置页时，视图模型可以关闭面板。视图模型的 `SaveAsync` 由按钮触发，而不是每次颜色变化都触发，因此拖动到一半的颜色不会送到服务端。

### 示例：租户品牌色

SaaS 产品管理后台中的一张品牌设置卡片。卡片包含三个选择器：主色、图表强调色，以及带有对比度读数的正文颜色。标签是一个词，说明文字写明每种颜色用在哪里，正文颜色上的错误信息则指出了未通过的规则。截图中选择器是收起的，因此该场景展示的是使用者打开面板之前所看到的内容。

![带有三个颜色选择器的品牌设置卡片，其中一个对比度未通过](images/BrandSettings.png)

```xml
<Border Width="560"
        Padding="24"
        Background="{arc:ArcDesignTokenResource Surface}"
        BorderBrush="{arc:ArcDesignTokenResource BorderSubtle}"
        BorderThickness="1"
        CornerRadius="{arc:ArcDesignTokenResource RadiusPanel}">
    <StackPanel Spacing="20">
        <StackPanel Spacing="4">
            <TextBlock Text="Brand colors"
                       FontSize="{arc:ArcDesignTokenResource TypeLG}"
                       FontWeight="{arc:ArcDesignTokenResource WeightMedium}"
                       Foreground="{arc:ArcDesignTokenResource Foreground}" />
            <TextBlock Text="Acme Analytics customer portal"
                       FontSize="{arc:ArcDesignTokenResource TypeSM}"
                       Foreground="{arc:ArcDesignTokenResource TextMuted}" />
        </StackPanel>
        <arc:ColorPicker Label="Primary"
                         Color="#2F6BFF"
                         Description="Buttons, links and focus rings." />
        <arc:ColorPicker Label="Chart accent"
                         Color="#E5484D"
                         Format="Oklch"
                         Description="Highlights the selected series in charts." />
        <arc:ColorPicker Label="Body text"
                         Color="#9CA3AF"
                         ContrastBackground="#FFFFFF"
                         ErrorMessage="Contrast 2.5 to 1 fails. Choose a darker color." />
        <arc:Button Content="Save brand colors" HorizontalAlignment="Right" />
    </StackPanel>
</Border>
```

示例为图表强调色使用 `Oklch`，因为团队需要比较各系列之间的明度；正文颜色与白色卡片进行对比检查，因此无需打开面板就能看到错误。按钮写明了它的作用，并且是卡片上唯一的操作。

### 文案规范

- **标签**：一个简短的名词，说明颜色的角色，例如“主色”“图表强调色”“正文颜色”。标签绘制在色块内部，因此请控制在两个词以内。
- **说明**：用一句话说明颜色的使用位置，例如“按钮、链接与焦点环。”
- **错误**：说明规则和修正方法，例如“对比度 2.5 比 1，未通过。请选择更深的颜色。”不要重复标签。
- **保存操作**：使用动词加上描述保存范围的对象，例如“保存品牌色”，而不是“提交”。
- **背景名称**：用背景的角色来命名，例如“卡片背景”，让使用者知道对比度读数比较的是什么。

| 不要写 | 改为 |
|---|---|
| 颜色 | 主按钮颜色 |
| 颜色无效！！ | 请输入 HEX、RGB、HSL 或 OKLCH 颜色。 |
| 提交 | 保存品牌色 |
| 对比度不通过 | 对比度 2.5 比 1，未通过。请选择更深的颜色。 |

### 最佳实践

**推荐**

- 将 `Color` 双向绑定，并在明确的操作时保存，因为拖动手柄时每次指针移动都会触发 `ColorChanged`。
- 将 `ContrastBackground` 设为真实的表面，让读数与使用者实际看到的一致。
- 将 `Format` 设为团队的表示法：CSS 与设计交接用 `Hex`，需要比较明度时用 `Oklch`。
- 除非团队的色板需要更多，否则保持 `MaxSwatches` 的默认值 7；过长的一行更难扫读。
- 为每个选择器设置 `Label`，即使在紧凑的设置中也是如此，因为屏幕阅读器播报的正是这个名称。

**避免**

- 将 `ColorChanged` 用作保存的触发器。每次移动都会产生一个事件，因此每次移动都发起一次网络请求会淹没服务端。
- 对 CSS 八位数值调用 `Color.Parse`。Avalonia 读取的是 `#AARRGGBB`，因此透明度通道会落在错误的位置；CSS 输出请使用 `HexValue`，CSS 输入交给选择器解析。
- 在代码中设置 `Swatches` 并期望触发 `SwatchesChanged`。该事件描述的是在面板中所做的变更，自行设置列表的视图模型本身已经知道新值。
- 为必须保持固定的色板使用选择器。应改用 `SegmentedControl` 或 `ChipGroup` 提供已批准的颜色，因为自由选择器会让人脱离色板。
- 颜色位于深色表面上时，却以默认的白色背景判断对比度。等级对所给的背景是准确的，对其他背景则会误导。

### 常见问题

**如何从已保存的 HEX 字符串启动选择器？** 在视图模型中以 Avalonia 的 `Color` 保存该值，并设置 `Color`。对于 CSS 顺序的值，例如 `#2F6BFF80`，在调用 `Color.Parse` 之前把数字重新排列为 `#802F6BFF`，因为 Avalonia 把八位数值读作 `#AARRGGBB`。用 `HexValue` 输出该值，使存储形式保持 CSS 顺序。

**为什么选择器显示的表示法与我设置的不同？** `Format` 是文本框的表示法。使用者可以在面板中循环切换，而值不会改变；颜色在每种表示法中都是相同的。

**能否在代码中关闭面板？** 可以。将 `IsDropDownOpen` 设为 `False`。“完成”、Escape 和在面板外按下，都会以同样的方式关闭面板；“完成”和 Escape 会把焦点返回到触发器。

**为什么输入时按 Escape 不能关闭面板？** 第一次 Escape 会放弃输入的草稿，使文本框恢复为当前颜色；第二次 Escape 才会关闭面板。这样可以避免输入一半的值被意外丢失。

**半透明颜色的读数如何处理？** 测量比值前，它会先将颜色与 `ContrastBackground` 合成，因此等级描述的是颜色在该背景上的实际样子，而不是原始颜色。

## API 参考

<!-- api: ColorPicker -->

<!-- api: ColorSwatch -->

## 无障碍

- `Label` 为触发器命名，展开的面板以它命名：“主色”。触发器会公开其展开状态，因此屏幕阅读器会播报面板是否已打开。
- 饱和度与明度区域是一个二维滑块，其值同时报告饱和度与明度，类型播报为“二维滑块”。色相与不透明度滑块分别报告角度与百分比。
- 文本框以标签和表示法命名，例如“主色的 Hex 格式”。输入无效时，辅助文字会被设为错误信息，并以礼貌的方式播报。
- 对比度比值及其等级是文字，而不只是颜色：读数会说“对比度 4.52 比 1，通过 AA”或“未通过”，因此等级不依赖胶囊的颜色。
- 已保存的色块组成一行水平列表，只有一个 Tab 停靠点。当前颜色以已选中的状态公开，色块的名称会说明如何移动和删除它们。
- 通过“完成”或 Escape 关闭面板时，焦点会返回触发器，使使用者在表单中保持原来的位置。

### 键盘

| 按键 | 操作 |
|---|---|
| 触发器上的 `Enter` / `Space` | 打开面板，并把焦点移到饱和度区域 |
| 展开时触发器上的 `Enter` / `Space` | 关闭面板，并把焦点返回触发器 |
| 面板中的 `Escape` | 关闭面板，并把焦点返回触发器 |
| 区域上的 `Left` / `Right` / `Up` / `Down` | 以 1% 为步长移动饱和度（左右）和明度（上下） |
| 区域上的 `Shift` 或 `PageUp` / `PageDown` | 以 10 个步长移动饱和度 |
| 滑块上的 `Left` / `Right`（`Up` / `Down`） | 将色相移动 1 度，或将不透明度移动 1% |
| 滑块上的 `Shift`、`PageUp` / `PageDown` | 以 10 为步长移动 |
| 滑块上的 `Home` / `End` | 移到最小值或最大值 |
| 文本框中的 `Enter` | 应用输入的值；无效值会使字段抖动并显示错误 |
| 文本框中的 `Escape` | 放弃输入的草稿；再按一次 `Escape` 关闭面板 |
| 色块上的 `Left` / `Right`、`Home` / `End` | 在已保存的色块之间移动焦点 |
| 色块上的 `Alt` + `Left` / `Right` | 将色块移动一个位置 |
| 色块上的 `Enter` / `Space` | 应用已保存的颜色 |
| 色块上的 `Delete` / `Backspace` | 删除该色块 |

指针可以在区域和滑块上拖动。在面板外按下会关闭面板，并且不会把焦点从使用者按下的位置移走。

## 动效

- 面板从色块中展开：裁剪区域从触发器的角落生长为完整的面板，阴影随裁剪区域变化。
- 手柄以快速弹簧跟随指针，拖动时会收紧。
- 格式按钮使文字在 HEX、RGB、HSL 与 OKLCH 之间变形。对比度比值会计数到新的数值。
- 无效输入会使字段抖动。新色块弹出，被移动的色块落定到新位置。
- 减弱动效时，手柄直接跳到新位置，面板以淡入代替裁剪展开，并取消格式变形与抖动。关闭动效时直接到达最终状态。

| 变化 | 完整 | 减弱 | 关闭 |
|---|---|---|---|
| 面板打开 | 裁剪区域以弹簧从触发器生长；内容淡入，并在前缘后方升起 10 px | 面板以完整尺寸淡入 | 立即完成 |
| 面板关闭 | 裁剪区域以短促、无回弹的弹簧折回触发器 | 面板淡出 | 立即完成 |
| 手柄移动 | 以快速弹簧跟随指针；拖动时收紧为较硬的弹簧 | 直接跳到新位置 | 立即完成 |
| 格式变化 | 文字从旧表示法变形为新表示法 | 文字立即改变 | 立即完成 |
| 对比度比值变化 | 数字计数到新的值 | 数字立即改变 | 立即完成 |
| 无效输入 | 字段左右抖动一次，错误随即出现 | 错误出现，不抖动 | 错误立即出现 |
| 色块保存或重排 | 色块弹出，或落定到新位置 | 立即出现或移动 | 立即完成 |

## 相关组件

- 少量固定颜色用 `SegmentedControl` 或 `RadioGroup`。
- 普通文本值用 `Input`。
- 小型预设标签用 `ChipGroup`。
- 表单中遵循相同字段模式的日期值用 `DatePicker`。

| 组件 | 适用场景 |
|---|---|
| `SegmentedControl` / `RadioGroup` | 使用者从几种固定颜色或主题中选择一种 |
| `Input` | 颜色是输入或粘贴的，从不需要在区域中调整 |
| `ChipGroup` | 颜色是用于筛选或标记列表的预设 |
| `Slider` | 只需设置单个数值通道，例如不透明度 |
| `DatePicker` | 值是日期而非颜色，表单布局相同 |
