# ArcDesign Token 目录

> 全部 ArcDesign Token 的名称、C# 类型与明 / 暗取值。这是 Arc 设计 Token 的**唯一来源**；控件主题只引用这里的名字。

## 0. 说明

- 来源：Arc foundation（`https://uiarc.dev/r/arc-foundation.json` 中的 `foundation.css`）与 motion tokens（`arc-motion-tokens.json`），快照日期 2026-10-20。
- 颜色换算：oklch 按 CSS Color 4 的色域映射（色度二分，JND 0.02）转为 sRGB。带透明度的颜色写作 `#AARRGGBB`。
- 长度单位：Arc 的 rem 按 16px 换算为 DIP（1rem = 16）。
- 阴影写作 Avalonia `BoxShadows` 字符串：`OffsetX OffsetY Blur Spread Color`，多个阴影用逗号分隔。
- 与 accent 相关的 Token，表中给出默认预设 neutral 的值；其他 7 个预设见 [accent 与调色板](accent-and-palette.md)。
- 命名已避开 AtomUI 的 255 个全局 Token 名（ATOMUIGEN019），例如用 `ControlSize*` 而不是 `ControlHeight*`，用 `Type*` 而不是 `FontSize*`。
- 类型必须在内置转换器列表内（见 [控件 Token](control-tokens.md#2-允许的属性类型)）。

## 1. 中性色阶（与外观无关）

| Token | 类型 | 值 | 来源 |
|---|---|---|---|
| `Neutral0` | Color | `#FFFFFF` | oklch(100% 0 0) |
| `Neutral1` | Color | `#FCFCFC` | oklch(99% 0 0) |
| `Neutral2` | Color | `#F8F8F8` | oklch(97.8% 0 0) |
| `Neutral3` | Color | `#F1F1F1` | oklch(95.8% 0 0) |
| `Neutral4` | Color | `#E3E3E3` | oklch(91.5% 0 0) |
| `Neutral5` | Color | `#CACACA` | oklch(84% 0 0) |
| `Neutral6` | Color | `#A4A4A4` | oklch(72% 0 0) |
| `Neutral7` | Color | `#7D7D7D` | oklch(59% 0 0) |
| `Neutral8` | Color | `#585858` | oklch(46% 0 0) |
| `Neutral9` | Color | `#383838` | oklch(34% 0 0) |
| `Neutral10` | Color | `#1D1D1D` | oklch(23% 0 0) |
| `Neutral11` | Color | `#0B0B0B` | oklch(15% 0 0) |

Arc 规则：控件主题只用语义 Token，不直接用中性色阶。中性色阶只供调色板计算与特殊场景使用。

## 2. 表面

| Token | 类型 | 浅色 | 深色 | 用途 |
|---|---|---|---|---|
| `Background` | Color | `#FFFFFF` | `#141414` | 页面底色 |
| `Surface` | Color | `#FFFFFF` | `#191919` | 卡片、面板 |
| `SurfaceRaised` | Color | `#FFFFFF` | `#1F1F1F` | 浮层（菜单、弹层、对话框） |
| `SurfaceMuted` | Color | `#F8F8F8` | `#242424` | 悬停底色、次级填充、禁用填充 |
| `Scrim` | Color | 待 P5 Dialog 规格确认 | 同左 | 模态遮罩 |

## 3. 文字

| Token | 类型 | 浅色 | 深色 | 用途 |
|---|---|---|---|---|
| `Foreground` | Color | `#0B0B0B` | `#F2F2F2` | 主文字 |
| `TextSecondary` | Color | `#585858` | `#B4B4B4` | 次级文字、描述 |
| `TextMuted` | Color | `#7D7D7D` | `#8C8C8C` | 占位、禁用、辅助 |

## 4. 边框

| Token | 类型 | 浅色 | 深色 | 用途 |
|---|---|---|---|---|
| `Border` | Color | `#E9E9E9` | `#2B2B2B` | 默认 1px 边框 |
| `BorderSubtle` | Color | `#F3F3F3` | `#222222` | 表面内部分隔线 |
| `BorderStrong` | Color | `#C4C4C4` | `#484848` | 输入框、强调边框 |

## 5. Accent（默认预设 neutral）

| Token | 类型 | 浅色 | 深色 | 用途 |
|---|---|---|---|---|
| `Accent` | Color | `#353535` | `#BEBEBE` | 当前项、选中、主要数据系列、进度、链接 |
| `AccentStrong` | Color | `#1F1F1F` | `#E1E1E1` | 强调文字、按下态 |
| `AccentSubtle` | Color | `#1A353535` | `#21BEBEBE` | accent 文字或图标背后的浅底 |
| `AccentForeground` | Color | `#FFFFFF` | `#141414` | accent 填充上的文字 |
| `AccentPreset` | string | `neutral` | 同左 | 解析出的预设名，`custom` 表示 OKLCH 推导 |

## 6. 选择控件（Switch、Checkbox、Radio、Slider）

| Token | 类型 | 浅色 | 深色 | 用途 |
|---|---|---|---|---|
| `ControlOn` | Color | `#1D1D1D` | `#E8E8E8` | 选中控件的填充 |
| `ControlOnSubtle` | Color | `#1A1D1D1D` | `#1AE8E8E8` | ControlOn 的 10% 透明版本 |
| `ControlGlyph` | Color | `#FFFFFF` | `#141414` | 选中控件上的勾或圆点 |
| `ControlTrack` | Color | `#DCDCDC` | `#353535` | 未选中的轨道 |
| `ControlTrackHover` | Color | `#D1D1D1` | `#404040` | 未选中轨道的悬停态 |
| `ControlThumb` | Color | `#FFFFFF` | `#F5F5F5` | 拇指（未选中） |
| `ControlThumbOn` | Color | `#FFFFFF` | `#141414` | 拇指（选中） |
| `ControlFill` | Color | `#1D1D1D` | `#868686` | Slider 填充；深色 neutral 用中灰，保证白色拇指可辨 |
| `ControlThumbShadow` | BoxShadows | `0 0 0 0.5 #12000000, 0 1 2 0 #24000000, 0 2 6 0 #0F000000` | `0 0 0 0.5 #4C000000, 0 1 2 0 #59000000, 0 2 6 0 #33000000` | 拇指阴影 |

## 7. 状态色

| Token | 类型 | 浅色 | 深色 | 来源 |
|---|---|---|---|---|
| `Success` | Color | `#007824` | `#4CD676` | oklch(49% .19 150) / oklch(78% .18 150) |
| `Warning` | Color | `#AA6B00` | `#FFB90F` | oklch(58% .18 75) / oklch(83% .17 80) |
| `Danger` | Color | `#CD1526` | `#FF817A` | oklch(54% .21 25) / oklch(76% .2 25) |
| `SuccessSubtle` / `WarningSubtle` / `DangerSubtle` | Color | 状态色 12% 透明 | 状态色 16% 透明 | 图标背后的浅底；Arc 原版用 color-mix，具体比例在首个使用组件（Alert，P3）的规格中确认 |

Info 使用 accent。

## 8. 图表系列色（默认预设 neutral）

| Token | 类型 | 浅色 | 深色 |
|---|---|---|---|
| `Series1` | Color | `#1F7DCF` | `#55ADFF` |
| `Series2` | Color | `#7128A5` | `#9C37BE` |
| `Series3` | Color | `#E58F00` | `#E66E00` |
| `Series4` | Color | `#009E84` | `#00A861` |

neutral 使用 Arc CSS 中写明的回退值；其他预设由 accent 推导，公式见 [accent 与调色板](accent-and-palette.md#5-图表系列色)。

## 9. 阴影

| Token | 类型 | 浅色 | 深色 | 用途 |
|---|---|---|---|---|
| `ShadowResting` | BoxShadows | `0 1 2 0 #09000000` | `0 1 2 0 #2E000000` | 少数抬起元素、按钮悬停 |
| `ShadowRaised` | BoxShadows | `0 6 18 0 #11000000, 0 1 3 0 #09000000` | `0 8 22 0 #38000000` | 少数抬起元素（不叠加） |
| `ShadowFloating` | BoxShadows | `0 20 48 0 #1B000000, 0 3 10 0 #0B000000` | `0 22 55 0 #57000000` | 浮层：菜单、弹层、对话框、抽屉 |

Arc 规则：卡片靠边框而不是阴影；阴影只用于浮层。

## 10. 焦点

| Token | 类型 | 浅色 | 深色 | 说明 |
|---|---|---|---|---|
| `FocusRing` | Color | `#B81F1F1F` | `#B8E1E1E1` | AccentStrong 的 72% 透明（color-mix in oklab 的近似） |
| `FocusRingWidth` | double | `2` | `2` | |
| `FocusRingOffset` | double | `2` | `2` | 被裁剪容器内的组件使用 `-2` |

只在键盘焦点（`:focus-visible`）时显示；文本框用边框与光标表示焦点，菜单项与列表项用高亮底色表示焦点（见 [可访问性](../accessibility.md)）。

## 11. 圆角

| Token | 类型 | 值 | 用途 |
|---|---|---|---|
| `RadiusControl` | CornerRadius | `18` | 输入框、按钮 |
| `RadiusPanel` | CornerRadius | `26` | 菜单、嵌套面板 |
| `RadiusSurface` | CornerRadius | `34` | 卡片 |
| `RadiusPill` | CornerRadius | `9999` | 标签、状态标记、胶囊 |

同心圆角规则：内圆角 = 外圆角 − 内边距，结果不小于 6。例如表面 34 加 8px 内边距得到 26，正好等于 panel。

## 12. 尺寸

| Token | 类型 | 值 | 说明 |
|---|---|---|---|
| `ControlSizeSM` | double | `36` | 小号控件高度 |
| `ControlSizeMD` | double | `44` | 默认控件高度（触屏可点击的最小尺寸） |
| `ControlSizeLG` | double | `50` | 大号控件高度 |
| `GlyphSizeSM` / `GlyphSizeMD` / `GlyphSizeLG` | double | `16` / `20` / `24` | 图标尺寸 |
| `IconStrokeWidth` | double | `1.75` | Lucide 描边宽度（24 网格下），按尺寸缩放见 [图标与字体](../icons-and-fonts.md) |

## 13. 间距（4px 网格）

| Token | 值 | Token | 值 | Token | 值 |
|---|---|---|---|---|---|
| `Space1` | 4 | `Space5` | 20 | `Space12` | 48 |
| `Space2` | 8 | `Space6` | 24 | `Space16` | 64 |
| `Space3` | 12 | `Space8` | 32 | `Space20` | 80 |
| `Space4` | 16 | `Space10` | 40 | `Space24` | 96 |

类型均为 double。Arc 没有 Space7 / Space9。

## 14. 字体排印

| Token | 类型 | 值 | 说明 |
|---|---|---|---|
| `DisplayFontFamily` | FontFamily | `fonts:Geist#Geist` | 30px 及以上的标题 |
| `BodyFontFamily` | FontFamily | `fonts:Inter#Inter` | 正文、标签、控件、小标题 |
| `MonoFontFamily` | FontFamily | 等宽系统字体栈（CodeBlock 规格中确认） | 代码 |
| `TypeXS` / `TypeSM` / `TypeBase` / `TypeLG` / `TypeXL` | double | `12` / `14` / `16` / `18` / `22` | 字号阶梯 |
| `Type2XL` / `Type3XL` / `Type4XL` / `Type5XL` | double | `28` / `36` / `52` / `72` | 字号阶梯 |
| `WeightRegular` / `WeightMedium` | FontWeight | `400` / `500` | 只允许这两种字重 |
| `TrackingDisplay` / `TrackingBody` | double | `-0.03` / `-0.01` | 单位 em；使用时乘以字号得到 Avalonia `LetterSpacing`（DIP） |
| `LeadingDisplay` / `LeadingBody` | double | `1.1` / `1.4` | 行高倍数；使用时乘以字号得到 `LineHeight` |

按钮文字为 14px、字重 500。数字变化或需要列对齐的地方使用等宽数字（字体特性 `tnum`）。

## 15. 动效

| Token | 类型 | 值 | 用途 |
|---|---|---|---|
| `DurationInstant` | TimeSpan | `00:00:00.120` | 按下等即时反馈 |
| `DurationFast` | TimeSpan | `00:00:00.160` | 颜色、透明度变化 |
| `DurationExit` | TimeSpan | `00:00:00.180` | 通用退场 |
| `DurationStandard` | TimeSpan | `00:00:00.240` | 标准进场 |
| `DurationConsidered` | TimeSpan | `00:00:00.480` | 首次揭示等较慢的动画 |
| `DurationSpring` | TimeSpan | `00:00:00.580` | CSS 侧弹簧近似的时长；曲线是代码常量 `ArcEasings.CssSpring`（自定义 Easing 不能作为 Token），主题中用 `Tween="Spring"` |
| `EaseEnter` | Easing | `0.16,1,0.3,1` | 进场 |
| `EaseExit` | Easing | `0.7,0,0.84,0` | 退场 |
| `EaseStandard` | Easing | `0.22,1,0.36,1` | 颜色、通用 |
| `EaseInOut` | Easing | `0.65,0,0.35,1` | 已在屏幕上的元素移动 |
| `SpringSmoothDuration` / `SpringSmoothBounce` | TimeSpan / double | `0.4s` / `0` | 报告状态的运动、面板、布局变化 |
| `SpringSnappyDuration` / `SpringSnappyBounce` | TimeSpan / double | `0.26s` / `0.12` | 按压、小指示器、图标切换、弹出 |
| `SpringMorphDuration` / `SpringMorphBounce` | TimeSpan / double | `0.42s` / `0.16` | 共享高亮、随内容变化的宽度、形状形变 |
| `SpringResponsiveStiffness` / `SpringResponsiveDamping` | double | `520` / `38` | Arc 保留的物理参数预设（免费组件未使用） |
| `SpringGentleStiffness` / `SpringGentleDamping` | double | `340` / `34` | 同上 |
| `StaggerChar` / `StaggerWord` / `StaggerLine` / `StaggerItem` | TimeSpan | `16ms` / `40ms` / `80ms` / `35ms` | 交错步长，总交错控制在约 0.4s 内 |

弹簧参数如何换算成刚度与阻尼，见 [弹簧引擎](../motion/spring-engine.md)。

## 16. 模糊与效果

| Token | 类型 | 值 | 用途 |
|---|---|---|---|
| `BlurSubtle` / `BlurSoft` / `BlurText` | double | `2` / `4` / `8` | 交叉淡化时的短暂模糊半径 |
| `BackdropBlurDialog` | double | `7` | 对话框、卡片快速预览的背景模糊 |
| `BackdropBlurDrawer` | double | `4` | 抽屉的背景模糊 |

## 17. 策略

| Token | 类型 | 默认值 | 说明 |
|---|---|---|---|
| `MotionMode` | string | `full`（`EnableMotion=false` 时为 `off`） | `full` / `reduced` / `off`；可被 own token 覆盖为最终值，见 [减少动效](../motion/reduced-motion.md) |
| `ReduceTransparency` | bool | `false` | 为 true 时，半透明菜单退化为实色表面，背景模糊关闭 |

## 18. 待组件规格补充的 Token

起草大类 overview 时发现的需求，在对应组件规格中确定名称与取值。归属按 [控件 Token](control-tokens.md#1-职责) 的派生色规则决定：

| 需求 | 来源组件（阶段） | 归属 |
|---|---|---|
| 模态遮罩色 `Scrim` 的明 / 暗值 | Dialog（P5） | ArcDesign（与 accent 无关，但被多个组件共享） |
| 状态色浅底 `SuccessSubtle` / `WarningSubtle` / `DangerSubtle` 的比例（Badge 源码为 10%，与第 7 节的暂定值不同） | Alert、Badge（P3） | ArcDesign |
| accent 明度阶梯（用量分段色） | UsageMeter（P3） | ArcDesign（依赖 accent） |
| 托盘底色（前景色 4.5%） | FloatingButtonGroup（P2） | 控件 Token（与 accent 无关） |
| 抽屉圆角 22px（不在圆角阶梯上） | Drawer（P5） | 控件 Token |
| 签名笔迹的明 / 暗颜色 | SignaturePad（P9） | 控件 Token |
| 点赞色（默认 `Danger`） | IconMorph（P3） | 控件 Token |

注意：Arc 源码中带透明度的十六进制颜色是 CSS 顺序 `#RRGGBBAA`，移植到 Avalonia 时要改写为 `#AARRGGBB`。

## 19. 不纳入的 Arc 变量

| 变量 | 原因 |
|---|---|
| `--arc-gradient*`（品牌渐变） | 只用于营销区块背景；Blocks 不在范围内 |
| `--focus-ring`（光晕） | Arc 中恒为透明（产品决定），由 `FocusRing` 轮廓替代 |
