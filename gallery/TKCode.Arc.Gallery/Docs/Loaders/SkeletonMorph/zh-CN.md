---
title: Skeleton morph
summary: 加载占位会逐块伸展为真实内容，同时卡片缓动至新的高度。
source-hash: 431adcc738cf
---

<Demo name="Preview" />

## 何时使用

`SkeletonMorph` 取代了从灰色方块到内容的生硬切换。加载后可见的每一部分布局都位于一个 `MorphBlock` 中。区域加载期间，每个块都是与其内容尺寸相同的骨架：圆形头像、按钮的胶囊形状、每行文字一条横条。数据到达后，各块按阅读顺序逐个解析。每个骨架在原位伸展到内容实际占用的盒子并淡出，同时内容从底下的柔和模糊中逐渐清晰。区域同时缓动到新的高度，因此卡片的边框永远不会跳动。

- **在几秒之内加载完成的资料卡片、信息流与列表**：布局从第一帧起即可见，并且没有闪烁地稳定下来。
- **布局在数据到达前已知的仪表板**：磁贴保持原位，只有内部内容发生变化。
- **生硬切换会显得突兀的地方**：增加或减少一行的卡片会缓动变化，而不是跳动。

## 何时不用

- **数百行**：每个块在解析时都会测量并执行动画。在虚拟化列表中请使用 `Skeleton` 行。
- **加载时间超过几秒**：请用 `Progress` 显示进度，让用户知道还有多少。
- **事先无法确定布局**：与到达内容不一致的骨架比没有骨架更糟。请使用 `Skeleton` 或 `EmptyState`。
- **单个占位且不需要变形**：使用 `Skeleton`。

## 用法

将加载后的布局放入 `SkeletonMorph`，并把每个可见部分包进一个 `MorphBlock`。将 `IsLoading` 绑定到你的请求。为文本块设置文字本身的行高和行数，并把骨架宽度设置为接近文字的典型宽度。添加 `card` 样式类，使区域本身成为卡片：这样其边框会跟随缓动后的高度。

```xml
<arc:SkeletonMorph Classes="card" IsLoading="{Binding IsLoading}" LoadingLabel="Loading profile">
    <StackPanel Spacing="16">
        <StackPanel Orientation="Horizontal" Spacing="12">
            <arc:MorphBlock IsCircle="True" SkeletonWidth="40">
                <arc:Avatar DisplayName="{Binding Name}" />
            </arc:MorphBlock>
            <arc:MorphBlock SkeletonWidth="120" Lines="1" LineHeight="20">
                <TextBlock Text="{Binding Name}" FontSize="16" LineHeight="20" />
            </arc:MorphBlock>
        </StackPanel>
        <arc:MorphBlock Lines="3" LineHeight="20">
            <TextBlock Text="{Binding Bio}" FontSize="14" LineHeight="20" TextWrapping="Wrap" />
        </arc:MorphBlock>
    </StackPanel>
</arc:SkeletonMorph>
```

两种类型都位于 Special 包中：请在 `UseArcControls()` 旁通过 `UseArcSpecial()` 注册它。`SkeletonMorph` 与 `MorphBlock` 都派生自 Avalonia 的 `ContentControl`。

上方的预览重建了一张资料卡片和一个转账列表。两者都在页面打开时加载一次（1.2 s），点击 Reload 会再次加载，耗时 1.5 s。在块仍在解析时按下 Reload：每个块会立即回到骨架，同时每张卡片缓动回到加载时的高度。Follow 开关和请求计时器属于演示的一部分；人名、金额和日期均为虚构。

### 驱动方式

`IsLoading` 是双向的，因此开关或视图模型都可以驱动它。`Resolved` 在最后一个块解析完成且其骨架淡出后触发；`IsResolving` 在两者之间为 true。

<Demo name="Controlled" />

```csharp
private async Task ShowAsync(Func<Task<object>> load)
{
    profile.IsLoading = true;
    DataContext = await load();
    profile.IsLoading = false; // the blocks resolve, then Resolved is raised
}
```

块在加载期间不显示其任何内容：内容不在可视树中，因此无法获得键盘焦点，屏幕阅读器也无法读到加载了一半的文字。绑定的内容在数据到达前可以为空。

### 骨架形状

除非设置了 `Lines`，块都是填充的形状。

- **形状**：`SkeletonWidth` 与 `SkeletonHeight` 以像素为单位，`SkeletonRadius` 为圆角（默认 6）。`IsCircle` 生成圆形头像占位；若只设置宽度而不设置高度，则为该宽度的圆形。
- **文本**：`Lines` 条横条，每条都在高 `LineHeight`（默认 20）的行框中居中。每条横条高 12 px；当行高小于 18 px 时，比行高少 6 px。多条横条中的最后一条宽度为 62 %。
- **宽度**：不设置 `SkeletonWidth` 则填满可用宽度；`SkeletonWidthRatio` 让填充的骨架按其比例绘制（`0.6` 即 60 %）。填充的块在加载后也会占满整个宽度。
- **高度**：未设置 `SkeletonHeight` 时，文本块高度为 `Lines` × `LineHeight`，其他块为 12 px。

<Demo name="Shapes" />

块默认左对齐和顶对齐，与它们所代表的文字一致。对于右对齐的数字（如金额），请使用 `HorizontalAlignment="Right"`：此时骨架会从块的新左边缘开始伸展。

### 错开

块按阅读顺序解析：即可视树的顺序，深度优先。`Stagger` 设置两个块之间的时间间隔（默认 40 ms）；设为 `0` 则所有块在同一帧解析。嵌套的 `SkeletonMorph` 的块属于该区域，并跟随其 `IsLoading`。

<Demo name="Stagger" />

### 减少动效

在减少动效下，骨架保留透明度脉冲（因此仍能看出正在加载），但没有伸展或模糊，每个块在 120 ms 内同时交叉淡化为其内容。区域的高度立即变化。关闭动效时骨架不脉冲。

<Demo name="ReducedMotion" />

### 结构

1. **区域**：持有加载后布局的 `ContentControl`。使用 `card` 类时，它具有表面背景、1 px 边框、26 px 圆角和 20 px 内边距。
2. **外壳**：内容外围的部分，其高度沿弹簧跟随内容。仅在移动时裁剪。
3. **状态**：隐藏的实时区域，在区域加载时朗读 `LoadingLabel`。
4. **块**：每个可见部分对应一个 `MorphBlock`：其内容，以及覆盖其上的骨架。
5. **骨架**：填充形状，或每个文本行一条圆角横条，颜色为柔和的表面色。伸展时它可能超出其块的范围。

### 状态

| 状态 | 区域 | 块 |
|---|---|---|
| 加载 | `:loading`；状态显示该标签 | 骨架保持各自的尺寸，一起呼吸；无内容 |
| 解析中 | `:resolving`；`IsResolving` 为 true | 逐个进行：内容挂载，骨架伸展并淡出 |
| 已加载 | 无伪类 | 仅显示内容；`IsResolved` 为 true |
| 回到加载 | 再次 `:loading` | 每个块在同一帧变为骨架；高度仍缓动 |

### 内容准则

- 让骨架与真实内容的尺寸一致。为文本设置明确的行高，并将相同的值传给 `LineHeight`；将 `SkeletonWidth` 设置为接近典型宽度，而非最长宽度。
- 包裹每一个可见部分。块之外的部分变化时没有动画，并且会早于其相邻部分出现。
- 将 `LoadingLabel` 写成正在加载的内容，用几个词即可："Loading profile"、"Loading transfers"。
- 让加载后的布局与骨架保持相同的形状。行数不同没有问题；布局不同则不行。

### 最佳实践

- 将卡片样式放在区域上（使用 `card` 类，或你自己的 `Background`、`BorderBrush`、`CornerRadius` 和 `Padding`），以便边框跟随缓动的高度。
- 保持区域的高度自由：默认顶对齐，因为被拉伸的区域会掩盖缓动效果。
- 通过将 `IsLoading` 设回 true 来重新加载。它是立即生效的，因此快速重新加载不会让块停在半途。
- 区域加载期间，不要禁用触发重新加载的按钮：应改为忽略其按下，这样键盘焦点能停留在原处。

### 常见问题

**加载后卡片为什么会改变高度？** 内容的尺寸与其骨架不同，例如简介需要两行而不是三行。区域会以与各块相同的弹簧缓动到新的高度。

**宽度可以使用百分比吗？** 不设置 `SkeletonWidth`，并设置 `SkeletonWidthRatio`。

**块在加载时，其内部的绑定会怎样？** 内容对象存在但不显示；其模板与绑定在块解析时才应用，因此它们读取的是已到达的数据。

## API 参考

<!-- api: SkeletonMorph -->

<!-- api: MorphBlock -->

## 无障碍

- 加载期间，区域的状态通过礼貌级别的实时区域读出 `LoadingLabel`，其条目状态携带相同的文字。加载结束时两者都会清除。
- 骨架是绘制出来的，不是控件：屏幕阅读器不会读到它们。
- 内容在其块解析之前不在可视树中，因此加载了一半的内容无法获得焦点或被读取。
- 块保持阅读顺序，因此加载前后的焦点顺序相同。区域和块本身都不可获得焦点。

### 键盘

区域本身不增加任何按键。块内的控件在显示后保持其自身的键盘行为；加载期间它们无法通过 `Tab` 到达。

## 动效

- 加载期间骨架一起呼吸：不透明度在 1.4 s 内从 1 变为 0.55，采用缓入缓出曲线，然后再回去。
- 块按阅读顺序解析，每块相隔 40 ms。骨架在临界阻尼弹簧（平滑弹簧，约 0.5 s，无过冲）上从其自身的盒子伸展到内容测得的盒子，并在 50 ms 停顿后于 380 ms 内淡出。内容在 330 ms 内淡入，同时从 4 px 的模糊中变清晰。
- 区域沿同一弹簧缓动到新的高度，并在移动时裁剪。
- 回到加载对于块是立即的；高度仍然缓动。

| 变化 | 完整 | 减少 | 关闭 |
|---|---|---|---|
| 脉冲 | 1 ↔ 0.55，1.4 s | 1 ↔ 0.55，1.4 s | 无 |
| 解析顺序 | 每块相隔 40 ms | 同时完成 | 同时完成 |
| 骨架到内容 | 弹簧伸展并淡出 | 120 ms 交叉淡化 | 瞬间 |
| 内容 | 从 4 px 模糊中淡入 | 120 ms 淡入 | 瞬间 |
| 区域高度 | 弹簧 | 瞬间 | 瞬间 |
| 回到加载 | 瞬间（高度以弹簧变化） | 瞬间 | 瞬间 |

## 响应式

- 块加载后会贴合其内容尺寸，因此布局在任何宽度下都会重新排列。
- 填充的骨架跟随可用宽度；`SkeletonWidthRatio` 使其保持为可用宽度的一个比例。
- 宽度变化时，区域的高度立即跟随；只有内容的变化才会缓动。

## 相关组件

- `Skeleton`：不带变形的简单占位。
- `MorphLoader`：一个小型加载指示器，会折叠为对勾或叉号。
- `EmptyState`：当没有内容可显示时，提供有用的下一步。

| 组件 | 何时选用 |
|---|---|
| `SkeletonMorph` | 布局已知，并且应当过渡为内容 |
| `Skeleton` | 你需要一个占位，或是很长的虚拟化列表 |
| `MorphLoader` | 一个紧凑的指示器就足够 |
| `Progress` | 加载耗时长到需要显示进度 |
