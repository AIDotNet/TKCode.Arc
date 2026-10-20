# Accent 与调色板

> 8 个 accent 预设的明 / 暗取值、`ArcPalette.Resolve` 的匹配与推导规则，以及图表系列色的计算公式。

## 1. 预设

Arc 提供 8 个 accent：`neutral`（默认）、`violet`、`blue`、`green`、`amber`、`orange`、`coral`、`rose`。每个预设都有一套手调的明 / 暗值，Arc 原版通过 `:root[data-accent=…]` 与 `:root[data-theme="dark"][data-accent=…]` 设置。

### 1.1 浅色

| 预设 | Accent | AccentStrong | AccentSubtle | AccentForeground | ControlOn | ControlThumbOn | ControlGlyph |
|---|---|---|---|---|---|---|---|
| neutral | `#353535` | `#1F1F1F` | `#1A353535` | `#FFFFFF` | `#1D1D1D` | `#FFFFFF` | `#FFFFFF` |
| violet | `#7747FF` | `#5528CE` | `#217747FF` | `#FFFFFF` | `#7747FF` | `#FFFFFF` | `#FFFFFF` |
| blue | `#0562EF` | `#074DB7` | `#1F0562EF` | `#FFFFFF` | `#0562EF` | `#FFFFFF` | `#FFFFFF` |
| green | `#0DB879` | `#087C54` | `#210DB879` | `#102016` | `#10A870` | `#FFFFFF` | `#FFFFFF` |
| amber | `#F3AD20` | `#9C6300` | `#29F3AD20` | `#20190D` | `#EDA11A` | `#FFFFFF` | `#2A1C03` |
| orange | `#F48120` | `#AA4700` | `#21F48120` | `#17130F` | `#F07818` | `#FFFFFF` | `#FFFFFF` |
| coral | `#F15F55` | `#B92C27` | `#24F15F55` | `#21110F` | `#EC564B` | `#FFFFFF` | `#FFFFFF` |
| rose | `#ED4E9D` | `#AE2570` | `#24ED4E9D` | `#250F1B` | `#E64494` | `#FFFFFF` | `#FFFFFF` |

浅色下 `ControlFill` 等于 `ControlOn`。

### 1.2 深色

| 预设 | Accent | AccentStrong | AccentSubtle | AccentForeground | ControlOn | ControlThumbOn | ControlGlyph | ControlFill |
|---|---|---|---|---|---|---|---|---|
| neutral | `#BEBEBE` | `#E1E1E1` | `#21BEBEBE` | `#141414` | `#E8E8E8` | `#141414` | `#141414` | `#868686` |
| violet | `#A78BFF` | `#C7B7FF` | `#387747FF` | `#141414` | `#8A66FF` | `#FFFFFF` | `#FFFFFF` | `#8A66FF` |
| blue | `#6BA3FF` | `#A4C7FF` | `#380562EF` | `#141414` | `#3B82F6` | `#FFFFFF` | `#FFFFFF` | `#3B82F6` |
| green | `#53DCA6` | `#93EFD0` | `#330DB879` | `#102016` | `#1FAE78` | `#FFFFFF` | `#FFFFFF` | `#1FAE78` |
| amber | `#FFCB62` | `#FFE0A1` | `#33F3AD20` | `#20190D` | `#E8A52A` | `#FFFFFF` | `#241803` | `#E8A52A` |
| orange | `#FFAD5E` | `#FFD0A0` | `#33F48120` | `#17130F` | `#EF873A` | `#FFFFFF` | `#FFFFFF` | `#EF873A` |
| coral | `#FF9386` | `#FFC1BA` | `#33F15F55` | `#21110F` | `#EC6A60` | `#FFFFFF` | `#FFFFFF` | `#EC6A60` |
| rose | `#FF8BC5` | `#FFC0DF` | `#33ED4E9D` | `#250F1B` | `#E457A0` | `#FFFFFF` | `#FFFFFF` | `#E457A0` |

说明：

- 深色 neutral 是反相的：近白的轨道配深色拇指，保证开 / 关两种状态不会看起来一样。
- 深色的 `AccentSubtle` 用的是浅色 accent 的 20%–22% 透明，而不是深色 accent。
- violet、blue、neutral 的 `AccentForeground` 在 Arc 中取 `var(--background)`，所以随外观变化；其余预设写死为带色相的深色。
- `FocusRing` 由 `AccentStrong` 加 72% 透明得到（见 [Token 目录](token-catalog.md#10-焦点)）。

## 2. 输入：accent seed

- `ArcDesignToken` 从 `EffectiveGlobalToken.ColorPrimary` 读取 seed。
- 应用通过 ArcDesign identity 上的 `ColorPrimary` 覆盖 seed（不写 Algorithm），未覆盖时为根级 `ColorPrimary`（见 [主题配置](theme-configuration.md)）。
- 每个预设的 **seed 值**等于其浅色 Accent（neutral 为 `#353535`）。

## 3. `ArcPalette.Resolve(seed, isDark)` 的匹配顺序

| 顺序 | 条件 | 结果 |
|---|---|---|
| 1 | seed 精确等于某个预设的 seed | 该预设的手调明 / 暗表 |
| 2 | seed 等于某个预设 seed 经 AtomUI 暗色算法派生后的值 | 该预设（解决"只在根级设置 `ColorPrimary`、暗色下拿到派生色"的问题） |
| 3 | seed 等于 AtomUI 默认主色 `#1677FF`，或其暗色派生值 | Arc 默认预设 neutral（应用未设置品牌色时，Arc 保持原版外观） |
| 4 | 其他 | `custom`：按第 4 节的 OKLCH 公式推导 |

- 第 2、3 步的派生值在类型初始化时，用 AtomUI 公开的 `PaletteGenerator`（`src/AtomUI.Core/Theme/Algorithms/PaletteGenerator.cs`）预先计算并缓存，与 AtomUI 暗色算法使用同一个函数。
- 若 AtomUI 将来调整暗色算法，缓存会随之变化，无需改 Arc。
- 比较使用 sRGB 8 位精确相等，避免误匹配。
- 解析结果的预设名写入 `AccentPreset` Token，便于调试与测试。

## 4. OKLCH 推导（custom）

seed 转为 OKLCH `(L, C, H)` 后，按下表推导。参数是用 8 个预设回归得到的初值，S0.2 中会进一步校准。校准目标：对 8 个预设的推导结果与手调值之间 ΔE_OK ≤ 0.03。

| Token | 浅色 | 深色 |
|---|---|---|
| Accent | seed 本身 | `L' = 0.39 + 0.6·L`，`C' = 0.75·C`，`H' = H` |
| AccentStrong | `L = 0.243 + 0.385·L`，`C = 0.85·C` | `L = min(L' + 0.09, 0.93)`，`C = 0.6·C'` |
| AccentSubtle | seed，透明度 0.13 | seed，透明度 0.21 |
| AccentForeground | 白色对 Accent 对比度 ≥ 4.5 时用白色，否则用 `oklch(0.2, 0.02, H)` | 白色对深色 Accent 对比度 ≥ 4.5 时用白色，否则用 Background（`#141414`） |
| ControlOn | `L > 0.6` 时 `L − 0.025`，否则同 Accent | `L = clamp(L, 0.62, 0.77)`，`C = 0.88·C` |
| ControlGlyph | ControlOn 的 `L ≥ 0.75` 时用 `oklch(0.2, 0.04, H)`，否则白色 | 同左规则 |
| ControlThumbOn | 白色 | 白色 |
| ControlFill | 同 ControlOn | 同 ControlOn |

所有结果都经过 CSS Color 4 色域映射转回 sRGB。

**校准数据**（OKLCH，格式 L/C/H）：

| 预设 | 浅色 accent | 浅色 strong | 浅色 control-on | 深色 accent | 深色 strong | 深色 control-on |
|---|---|---|---|---|---|---|
| violet | 0.567/0.253/287 | 0.456/0.231/285 | 0.567/0.253/287 | 0.712/0.166/293 | 0.818/0.101/294 | 0.626/0.217/289 |
| blue | 0.542/0.226/261 | 0.452/0.179/260 | 0.542/0.226/261 | 0.717/0.147/260 | 0.824/0.088/260 | 0.623/0.188/260 |
| green | 0.691/0.155/160 | 0.519/0.112/162 | 0.647/0.143/160 | 0.806/0.143/164 | 0.886/0.099/170 | 0.666/0.140/161 |
| amber | 0.794/0.159/78 | 0.549/0.118/71 | 0.765/0.157/74 | 0.868/0.135/83 | 0.918/0.087/85 | 0.767/0.149/77 |
| orange | 0.719/0.171/54 | 0.521/0.146/46 | 0.700/0.175/51 | 0.812/0.135/63 | 0.887/0.082/67 | 0.724/0.155/53 |
| coral | 0.672/0.182/27 | 0.518/0.178/28 | 0.652/0.187/28 | 0.775/0.132/28 | 0.865/0.072/26 | 0.681/0.163/27 |
| rose | 0.665/0.207/354 | 0.511/0.184/353 | 0.641/0.210/354 | 0.779/0.154/350 | 0.874/0.082/348 | 0.662/0.189/351 |

低色度 seed（C ≤ 0.02）按 neutral 的规则处理：只变明度，不引入色相。

## 5. 图表系列色

Arc 用 CSS 相对颜色语法从 accent 推导 `--series-1` 到 `--series-4`。`ArcPalette` 用同一公式计算（输入为当前外观的 Accent）。

记 `s = (C > 0.02) ? 1 : 0`；s = 0（neutral）时使用 `L₀ = 0.58`（浅色）或 `0.73`（深色）、`C₀ = 0.15`、`H₀ = 250`；否则 `L₀, C₀, H₀` 取 accent 自身的值。

| 系列 | 浅色 | 深色 |
|---|---|---|
| Series1 | `(L₀, C₀, H₀)` | `(L₀, C₀, H₀)` |
| Series2 | `(clamp(0.45, L₀ − 0.25, 0.76), 0.19, H₀ + 57)` | `(clamp(0.485, L₀ − 0.19, 0.665), 0.21, H₀ + 66)` |
| Series3 | `(clamp(0.45, L₀ + 0.14, 0.76), 0.18, H₀ + 184)` | `(clamp(0.485, L₀ + 0.02, 0.665), 0.21, H₀ + 175)` |
| Series4 | `(clamp(0.45, L₀, 0.76), 0.21, H₀ + 292)` | `(clamp(0.485, L₀ − 0.1, 0.665), 0.18, H₀ + 270)` |

计算参考值（色域映射后）：

| 预设 | 浅色 Series1–4 | 深色 Series1–4 |
|---|---|---|
| neutral | `#1F7DCF #7128A5 #E58F00 #009E84`（CSS 回退值） | `#55ADFF #9C37BE #E66E00 #00A861`（CSS 回退值） |
| violet | `#7747FF #96036E #A6A900 #0086A7` | `#A78BFF #BE0567 #9F9900 #0098A4` |
| blue | `#0562EF #7E2098 #C68E00 #008381` | `#6BA3FF #A42BAA #CB8200 #00A078` |
| green | `#0DB879 #006279 #FD7DCD #C09600` | `#53DCA6 #0093CA #E153BE #CD8100` |
| amber | `#F3AD20 #388500 #7EB0FF #FF7C98` | `#FFCB62 #00B43F #3690FF #E35C9E` |
| orange | `#F48120 #5F6000 #0FBEFF #FB62C0` | `#FFAD5E #639A00 #009FF0 #D263C3` |
| coral | `#F15F55 #724E00 #00C9E7 #CB61E6` | `#FF9386 #977800 #00AAB7 #A276F2` |
| rose | `#ED4E9D #8F3500 #00D1B0 #8D78FF` | `#FF8BC5 #C45B00 #00B27A #4D90FF` |

- neutral 使用 Arc CSS 中写明的回退值。用公式计算的结果有 ±1–2 级差异，例如浅色 Series3 为 `#E39000`。
- 浏览器的色域映射实现可能与 CSS Color 4 算法略有差异；最终以 S0.2 中与 Arc 页面截图的像素对照为准。
- 应用可以通过覆盖 `Series1`–`Series4` 来重新定义图表品牌色（与 Arc "在任意元素上覆盖 --series-*" 的做法一致）。

## 6. 对比度要求（来自 Arc design.md）

- 文字对比度 ≥ 4.5:1；24px 以上或 18.5px medium 字重时 ≥ 3:1。
- 图标与控件边框 ≥ 3:1。
- 推导结果必须满足：`AccentForeground` 对 `Accent` ≥ 4.5，`ControlGlyph` 对 `ControlOn` ≥ 3，`TextMuted` 对 `SurfaceMuted` 的对比度需要检查。
- [Token 验证](verification.md) 对 8 个预设与一组随机 seed 自动检查以上对比度。
