# 滑块（Sliders）

> Arc 的滑块大类用拖拽设定近似值：通用的单值与范围滑块，以及越过两端时像橡皮一样拉伸的弹性滑块。

## 1. 大类定位
- 解决"位置比精确数字更重要"的取值：音量、不透明度、价格区间（Slider），以及注重手感的单个设置，如音量、亮度、缩放、播放速度（ElasticSlider）。
- 边界：精确数值用 [特殊输入](../special-inputs/overview.md) 的 NumberField 或 ScrubInput；只读进度用 [进度](../progress/overview.md) 的 Progress；需要两个拇指或范围时只能用 Slider。
- 共同特征：拖拽 1:1 跟手、松手按速度落定；到达极限时向按压方向拉伸后弹回；拖拽或按键时拇指上方出现值气泡，读数逐位滚动；值事件分为逐步与落定两种。

## 2. 组件一览

| 组件 | 类名 | Arc 页面 | 阶段 | Arc 依赖 | 建议基类 | 原语 |
|---|---|---|---|---|---|---|
| Slider | `Slider` | [slider](https://uiarc.dev/components/slider) | P7 | 无 | RangeBase | P12 P13 P14 P26 |
| Elastic slider | `ElasticSlider` | [elastic-slider](https://uiarc.dev/components/elastic-slider) | P7 | 无 | RangeBase | P12 P13 P24 P26 |

## 3. 共享约定
- **事件**：`onValueChange` 在拖拽与按键的每一步触发，映射为 RangeBase 的 `ValueChanged`（Slider 范围模式为 `RangeChanged`）；`onValueCommit` 在松手、按键改变或点击刻度标签时触发一次，映射为 `ValueCommitted`（SliderValueCommittedEventArgs）；网络请求等重操作应挂在 commit 上。
- **键盘**：ArrowRight / ArrowUp 加一步，ArrowLeft / ArrowDown 减一步；Shift + 方向键或 PageUp / PageDown 走大步（默认区间的十分之一）；Home / End 到最小或最大。`step` 映射为 `SmallChange`（兼作吸附步长），`largeStep` 映射为 `LargeChange`。
- **格式与刻度**：`format` 映射为 `ValueFormatter`（Func<double, string>?），同时用于读数、气泡与 valuetext；`showValue` 映射为 `IsValueVisible`；`marks` 映射为 `Marks`（IList<SliderMark>：Value、Label?），带标签的刻度印在轨道下方且可点击，两端标签与轨道边缘对齐、不溢出。
- **可访问性**：每个拇指是 role="slider"（valuemin、valuemax、valuenow、valuetext、orientation），映射为 Slider 类型 peer + IRangeValueProvider；名称来自可见标签（`LabeledBy`）；读数、气泡、图标与刻度标签对辅助技术隐藏；点击刻度只是指针捷径，同样的值可用键盘到达。
- **指针**：拖拽使用指针捕获（整行接收按下）；拖拽直接写入动效值，不在每帧触发布局；触摸方向锁定（S10）尚未映射。
- **外观与焦点**：轨道 `ControlTrack`，填充 `ControlFill`（中性暗色下为中灰，以区分白色拇指），拇指 `ControlThumb` 加拇指阴影；指针聚焦时不显示焦点环与气泡，按下任意键后才出现（S7）。
- **动效降级**：Reduced 去掉动量、拉伸、摇摆与数字滚动，拇指直接到值、气泡只淡入淡出，并关闭过渡；Off 直接到终态。
- **不映射**：`name` 生成的隐藏 input；触感振动（Arc 只在支持的触屏设备上振动，桌面不适用）。

## 4. 组件

### 4.1 Slider（Slider）
`slug: slider` · 阶段 P7 · Arc 依赖：无 · 建议基类：RangeBase

**Arc 行为摘要**：在跟手的轨道上选一个值或一个范围，带动量拖拽、刻度、值气泡与滚动读数。用于音量、不透明度、价格区间等近似值，以及带两个拇指、标注刻度与格式化读数的范围。精确数字用 NumberField；不可修改的值用 Progress。

**API（已实现）**：

| Arc 属性 | 类型 / 默认值 | Avalonia 属性（类型） |
|---|---|---|
| `label`（必填） | `string`（未设 thumbLabels 时命名拇指） | `Label`（string?） |
| `value` / `defaultValue` / `onValueChange` | `number \| [number, number]`；二元组即范围 | 单值：`Value`（double，继承，双向）+ `ValueChanged`；范围：`IsRange` + `LowerValue` / `UpperValue`（double，双向，默认 0 / 100）+ `RangeChanged`（SliderRangeChangedEventArgs）；范围模式下 `Value` 不使用 |
| `onValueCommit` | `(value) => void` | `ValueCommitted`（SliderValueCommittedEventArgs：Value、LowerValue、UpperValue、IsRange） |
| `min` / `max` / `step` / `largeStep` | `0` / `100` / `1` / 区间的十分之一 | `Minimum` / `Maximum` / `SmallChange`（兼作吸附步长，默认 1）/ `LargeChange`（未显式设置时取区间的十分之一） |
| `marks` | `(number \| { value, label? })[]` | `Marks`（IList<SliderMark>，默认实例，可内联；`SliderMark` 可由 double 隐式转换） |
| `minStepsBetweenThumbs` | `0` | `MinStepsBetweenThumbs`（int） |
| `format` / `showValue` | 无 / `true` | `ValueFormatter`（Func<double, string>?）/ `IsValueVisible`（bool） |
| `thumbLabels` / `start` / `end` | `[string, string]` / 节点 | `LowerThumbName` / `UpperThumbName`（默认 "{Label}, minimum / maximum"）/ `StartContent` / `EndContent`（object） |

**交互与键盘**：
- 键盘见共享约定；Home / End 移到该拇指允许的最低或最高值（范围模式受另一拇指与 `MinStepsBetweenThumbs` 约束）。
- 按下轨道时最近的拇指以 snappy 弹簧追到指针（位置 + 追赶偏移），抓住拇指（22px + 每侧 11px 命中区）时保持抓取偏移；两个拇指重叠时等第一次移动的方向再决定。
- 松手后带速度落定；只有当一步不小于 12px 时，动量才可能多走一步，且最多一步。

**可访问性**：
- 控件本身是 Group peer，每个拇指一个 Slider 类型 peer（IRangeValueProvider，最小 / 最大值随另一拇指变化）；名称取 Label 或 thumb 名称，格式化文本写入 ItemStatus（对应 aria-valuetext）；读数、气泡与刻度标签不进可访问性树。

**动效**：
- 拖拽 1:1 跟手；松手速度由最近 6 个采样（暂停超过 60ms 视为 0）求得，叠加追赶弹簧的速度，用 snappy 弹簧继承速度落定（P12）。
- 极限处按键时拇指以 morph 弹簧带 150px/s 初速度向按压方向撞墙后弹回；拖过极限时橡皮筋 `(1 − 1/(|d|·0.55/9 + 1))·9`，最多 9px（P13）。
- 气泡在活动拇指上方从 0.6、下方 6px、2px 模糊以 snappy 弹出（透明度与模糊 fast + enter），退出 instant 缩到 0.8（P26）；拖拽时拇指放大到 1.16（snappy）；读数与气泡数字逐位滚动（P14，Core `RollingDigits`），松手后气泡停留 700ms。
- Reduced：去掉动量、拉伸与数字滚动，拇指直接到值，气泡只淡入淡出；Off：直接到终态。

**Avalonia 实现要点（已实现）**：
- 轨道 6px、拇指 22px、刻度点 4px（填充内为 `ControlGlyph` 60%，在填充边缘精确裁切）；气泡位于拇指上方 8px（触摸 / 笔按下后 18px），靠近组件边缘时夹住不溢出。
- 内部 `SliderRail` 一次 Render 画轨道、填充与刻度；拇指 `SliderThumb`（可聚焦、自绘环）与气泡 `SliderBubble` 只用渲染变换移动，位置以轨道千分比保存，拖拽与弹簧期间零布局、零逐帧分配。
- 格式化文本拆成前缀 / 数字 / 后缀交给 RollingDigits（如 "$1,250"），无法拆出数字时退回静态文本。
- 共享件在 `Sliders/_Shared`：`SliderMath`（橡皮筋、投影、吸附）、`SliderVelocityTracker`、`SliderThumb`、`SliderBubble`、`SliderMarkLabels`、`SliderRailBase`。

**AtomUI 参考**：Slider（仅供学习行为与边界，不复用其实现）。

**风险与未决**：
- 触摸的方向锁定（横向拖动拇指、纵向滚动页面）未映射：触摸按下即捕获指针。粗指针（触摸、笔）按下后气泡上移 10px（18px），对应 Arc 的 `pointer: coarse`。

### 4.2 ElasticSlider（Elastic slider）
`slug: elastic-slider` · 阶段 P7 · Arc 依赖：无 · 建议基类：RangeBase

**Arc 行为摘要**：越过两端时像橡皮一样拉伸的单值滑块，值标签随速度摇摆，刻度是拇指会卡入的制动点。用于音量、亮度、不透明度等凭手感拖动的单个设置，以及适合实体触感的媒体与创意工具。安静的表单与设置页用 Slider；精确数字比手感重要时用 NumberField；两个拇指或范围用 Slider。

**API（已实现）**：

| Arc 属性 | 类型 / 默认值 | Avalonia 属性（类型） |
|---|---|---|
| `label`（必填） | `string`（同时命名拇指） | `Label`（string?） |
| `value` / `defaultValue` / `onValueChange` | `number` / `min` | `Value`（double，继承，双向）+ `ValueChanged` |
| `onValueCommit` | 松手、按键改变或点击刻度标签时触发 | `ValueCommitted`（SliderValueCommittedEventArgs） |
| `min` / `max` / `step` / `largeStep` | `0` / `100` / `1` / 区间的十分之一 | `Minimum` / `Maximum` / `SmallChange` / `LargeChange`（未设置时取十分之一） |
| `marks` | 制动点；拖动时约 7px 内卡入 | `Marks`（IList<SliderMark>） |
| `format` / `showValue` | 无 / `true`（拖拽或按键时标签仍出现） | `ValueFormatter` / `IsValueVisible` |
| `startIcon` / `endIcon` | 节点；轨道越界拉伸时被向外推 | `StartIcon` / `EndIcon`（object） |
| `disabled` | `false`（同时移出 Tab 顺序） | `IsEnabled`（继承） |

**交互与键盘**：
- 键盘见共享约定；在极限处按键时轨道以 260px/s 初速度向按压方向拉伸后弹回；刻度是软吸附，不阻止中间值；落到刻度上时刻度弹一下；松手后值标签停留 700ms，标签出现时读数淡出（fast）。

**可访问性**：
- 单个 Slider 类型拇指 peer，由 Label 命名，格式化文本写入 ItemStatus；控件本身是 Group peer。

**动效**：
- 轨道越过两端时按 iOS 式阻力拉伸，最多 26px（远离锚定端伸长并变细，按下时加粗 0.45），松手按释放速度以 0.5s、bounce 0.42 弹回；两端图标随拉伸被推开（P12、P13）。
- 拖拽时拇指以 snappy 放大到 1.28 倍（P24）；值标签以软弹簧（刚度 420、阻尼 30、质量 0.7）跟随拇指，按速度倾斜，最大 11°，绕标签下方 30% 处摆动。
- 卡入制动点时刻度 1 → 2.1 → 1 弹一下（0.34s，每段 enter 缓动，P26）。
- Reduced：去掉拉伸、摇摆与刻度弹出，拇指直接到值，标签只淡入淡出（0.15s）；Off：同上。

**Avalonia 实现要点（已实现）**：
- 轨道 8px、拇指 24px（命中区每侧外扩 12px）；拇指位置 = 已提交位置 + 追赶偏移，指针 1:1 移动时拇指仍可滑入制动点或按下点。
- 内部 `ElasticSliderRail` 用一个变换画拉伸的轨道与填充，刻度不变形；标签跟随弹簧 `ElasticSliderSway` 是挂在 Core 帧调度器上的轻量驱动（每帧改目标也不分配），读出速度换算倾角。
- 与 Slider 共用 `Sliders/_Shared` 的橡皮筋公式（上限不同）、速度采样、拇指、气泡与刻度标签。

**AtomUI 参考**：Slider（仅供学习行为与边界，不复用其实现）。

**风险与未决**：
- Arc 在触屏上为制动点提供 6ms 振动，Avalonia 桌面无对应 API，未实现。
- Arc 的拇指 `outline: none`（没有焦点环）；这里为键盘焦点保留 2px 焦点环。
- 刻度颜色在 on / off 之间直接切换（Arc 有 fast 过渡）。

## 5. 实现顺序与依赖
- **P7**：Slider 先行，沉淀拖拽速度采样、动量投影、极限拉伸、值气泡与读数滚动；ElasticSlider 复用这些件，再加橡皮筋轨道、制动点与摇摆标签。
- 所需原语都已在更早阶段落地：P12（UserMenu，P5）、P13（PasswordStrength，P6）、P14（Calendar，P1）、P24（Switch，P0）、P26（Avatar，P1）；服务 S1、S2、S5、S7、S10。
- 跨大类：[特殊输入](../special-inputs/overview.md) 的 ScrubInput 与本大类同在 P7，共享橡皮筋阻力公式与"换算刚度阻尼以继承速度"的弹簧写法；NumberField（P6）的极限拉伸是更早的同类实现。

## 6. 相关文档
- [组件总清单](../overview.md) · [规格模板](../spec-template.md) · [路线图](../../roadmap/overview.md)
- [原语目录](../../architecture/motion/primitives-catalog.md) · [Token 目录](../../architecture/theming/token-catalog.md)
- [浮层与输入](../../architecture/overlays-and-input.md)（手势与键盘约定）
- 相邻大类：[特殊输入](../special-inputs/overview.md) · [进度](../progress/overview.md) · [开关与选项](../toggles/overview.md)
