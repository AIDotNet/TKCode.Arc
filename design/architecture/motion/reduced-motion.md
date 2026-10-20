# 减少动效

> 动效等级的定义、来源的组合方式、按动效类别的降级策略，以及减少透明度。

## 1. 等级

```csharp
public enum ArcMotionLevel { Off = 0, Reduced = 1, Full = 2 }
```

| 等级 | 含义 |
|---|---|
| Full | 完整动效 |
| Reduced | 对应 Arc 的 `prefers-reduced-motion` 分支：保留终态、焦点与反馈；去掉位移、循环、视差与滚动跟随，改为短淡入淡出 |
| Off | 所有过渡立即到终态 |

## 2. 来源与组合

有效等级取以下来源中最低的一个：

| 来源 | 设置方式 | 默认 |
|---|---|---|
| 子树等级 | 附加属性 `ArcMotion.Level`（可继承），可设在 Window、页面或任意容器上 | Full |
| 主题等级 | ArcDesign own token `MotionMode`：`full` / `reduced` / `off` | `EnableMotion=true` 时为 `full`，否则为 `off` |
| 控件开关 | 控件的 `IsMotionEnabled`（绑定 AtomUI 全局 `EnableMotion`）；false 时视为 Off | true |
| 系统偏好（可选） | `ArcMotion.UseSystemPreference()` 启用平台探针；系统开启"减少动态效果"时为 Reduced | 不启用 |

- 读取：`ArcMotion.GetEffectiveLevel(Visual)`。
- 变化：任一来源变化都会发出通知，正在进行的动画立即按新等级处理（Off 跳到终态）。

系统探针（`IReducedMotionProbe`）的候选实现，均为可选，在 Phase 0 之后评估：

| 平台 | 数据来源 |
|---|---|
| macOS | `NSWorkspace.accessibilityDisplayShouldReduceMotion` |
| Windows | `SystemParametersInfo(SPI_GETCLIENTAREAANIMATION)` |
| Linux | `org.gnome.desktop.interface enable-animations`，或 XDG Desktop Portal 设置 |

Avalonia 12.1.4 没有对应的跨平台 API，所以探针默认关闭，避免引入平台互操作的风险。

## 3. 按类别的降级策略

| 动效类别 | Full | Reduced | Off |
|---|---|---|---|
| 空间运动：位移、缩放、旋转、尺寸、FLIP、共享元素 | 弹簧 | 直接到终态；进场 / 退场加 `DurationFast` 的透明度淡入淡出 | 立即 |
| 透明度、颜色 | 补间 | 更短的补间（`DurationInstant`） | 立即 |
| 模糊、持续循环、视差、滚动跟随 | 开启 | 移除 | 移除 |
| 用户拖拽 | 跟随手指，松开后惯性、橡皮筋 | 跟随手指，松开后无回弹地吸附 | 跟随手指，松开后立即吸附 |
| 按压缩放 | 开启 | 改为颜色反馈 | 改为颜色反馈 |
| 滚动数字 | 逐位滚动 | 交叉淡化到新值 | 立即 |
| 交错文字揭示 | 逐词 / 逐字揭示 | 整体一次淡入 | 立即 |
| 键盘反复导航（方向键连续移动高亮） | 不动画 | 不动画 | 不动画 |

## 4. 保留的反馈

以下反馈在 Reduced 下仍然保留，因为它们传达的是状态而不是装饰（来自 Arc 文档）：

- HoldToConfirm 的填充随按住时长推进。
- ConfirmMorph 的按住填充与倒计时环。
- NumberField 到达边界时，把位移与抖动改为短暂的警告色。
- 进度类组件（Progress、UsageMeter、Gauge）直接显示终值，数值文字立即更新。
- JsonViewer 在可见行数超过 400 时，即使在 Full 等级下也按 Reduced 处理高度动画。

## 5. 首帧

无论处于哪个等级，控件首次显示与主题初始应用都不播放动画（见 [ControlTheme 集成](transitions-integration.md#3-动效等级与首帧)）。

## 6. 减少透明度

- ArcDesign Token `ReduceTransparency`（bool）。也可以通过附加属性 `ArcEffects.ReduceTransparency` 在子树上设置。
- 为 true 时：
  - 半透明菜单与弹层（DropdownMenu、Select 等）退化为 `SurfaceRaised` 实色；
  - 背景模糊（`BackdropBlurDialog` / `BackdropBlurDrawer`）关闭，遮罩不透明度略微提高（具体值在 Dialog 规格中确定）。
- 与动效等级相互独立。

## 7. 规格要求

每个组件规格的"动效表"必须为每条动效写明 Reduced 与 Off 下的行为；评审时检查是否符合第 3 节的类别策略。
