# Phase 0：基础设施与试点

> P0 的目标是用两个试点组件（Button、Switch）打通整条链路，并用 10 个 spike 验证所有尚未证实的架构假设。P0 不通过，不进入 P1。

## 1. 交付物

| 类别 | 交付物 | 详细设计 |
|---|---|---|
| 工程 | 仓库骨架：`TKCode.Arc.slnx`、`global.json`、`Directory.Build.props/targets`、`Directory.Packages.props`、`build/*.props`、`.editorconfig`、`LICENSE`、`THIRD-PARTY-NOTICES.md`、`AGENTS.md` | [解决方案布局](../architecture/solution-layout.md) |
| 工程 | 项目：Core、Arc、Icons.Lucide、Fonts、图标生成器、两个测试项目、消费验证应用、Gallery | 同上 |
| 主题 | `ArcDesign`、`ArcDesignToken`（完整目录）、`ArcPalette`、`ArcTokens.Bind`、`ArcTokenValue`、`WithArc` 配置辅助 | [主题与 Token](../architecture/theming/overview.md) |
| 动效 | `SpringSpec`、`SpringSolver`、`MotionValue<T>`、`MotionScheduler`、`ManualFrameClock`、`Spring*Transition`、`ArcSpringEasing`、`ArcMotion.Level`、单内容 `PresenceHost`（S3，Button 标签交叉淡化需要） | [动效系统](../architecture/motion/overview.md) |
| 基元 | S5 `SizeObserver`、S7 `FocusRing`；P1 按压、P2 模糊切换、P5 宽度弹簧、P21 加载圈、P24 拇指拉伸、P29 裁剪揭示 | [原语目录](../architecture/motion/primitives-catalog.md) |
| 图标与字体 | Lucide 生成器、Core 内置字形子集、`TKCode.Arc.Fonts`（Geist） | [图标与字体](../architecture/icons-and-fonts.md) |
| 组件 | Button、Switch（规格先行：`design/components/buttons/button.md`、`design/components/toggles/switch.md`） | 大类 overview |
| Gallery | 侧边栏、主题工具栏（明暗 / accent / 动效等级 / 语言）、Button 与 Switch 展示页、临时源码展示 | [Gallery 应用](../engineering/gallery-app.md) |
| 验证 | CI 流水线（构建、测试、打包、消费、AOT、文档校验） | [目标框架](../architecture/target-frameworks.md#6-ci-要求) |

## 2. Spike 清单

每个 spike 都在独立分支或临时目录中完成。结论写入本文第 3 节，并回写受影响的 ADR 与架构文档。

| 编号 | 验证内容 | 通过标准 | 失败时 |
|---|---|---|---|
| S0.1 | 工具链 | Core 与 Arc 在 SDK 11 RC2 下以 net10.0 与 net11.0 双目标构建；每个目标框架都生成了包注册；零 ATOMUIGEN / ATOMUIREG 诊断 | 记录失败环节；评估暂时只发布 net10 资产并向 AtomUI 反馈（重开 ADR 0009） |
| S0.2 | 载体 Token | 以下各项全部成立：<br>1. 试点主题能解析 `{arc:ArcDesignTokenResource …}`；<br>2. 明暗切换后数值变化；<br>3. 在 ArcDesign identity 上覆盖 `ColorPrimary`（不写 Algorithm）为 violet，两种外观都得到 violet 预设；<br>4. 只设根级 `ColorPrimary` 时，暗色下也映射回预设；<br>5. `ThemeConfigProvider` 子树内外数值不同；<br>6. `Capture<ArcDesign>` 可用；<br>7. 用 `CreateControlTokenBinding` 绑定 ArcDesign key 时会被拒绝（作为反例记录在文档中）；<br>8. 运行时能读到 `MotionMode` 的覆盖值；<br>9. OKLCH 推导参数完成校准（ΔE_OK ≤ 0.03） | 重开 ADR 0005，评估"抽象基 Token"方案 |
| S0.3 | 命名 | Button 声明 `Path = "arc.label"` 的语义部件，构建通过；改成 `icon` 能复现 ATOMUIGEN037；应用中使用 `atom:ButtonArcLabelStyle` 定制成功 | 重开 ADR 0004 |
| S0.4 | NuGet 消费 | 打包到本地 feed；在清空的 NuGet 缓存下，只引用 `TKCode.Arc` 的应用能构建、启动并显示 Button / Switch；删除 zh-TW 中的一条文案时触发 ATOMUIREG014 | 检查 buildTransitive 资产与包依赖声明 |
| S0.5 | NativeAOT | osx-arm64 上 net10.0 与 net11.0 的 Gallery 都能 NativeAOT 发布；产物运行、切换主题、播放动画；发布收据显示未用控件被裁剪；记录产物大小 | 按 AtomUI 的 AOT 规范排查；必要时暂缓 net11 AOT |
| S0.6 | 弹簧引擎 | 5 个预设与 framer `spring()` 参考采样的误差 ≤ 1e-3；打断时位置与速度连续（速度误差 ≤ 1%）；空闲时不请求帧；200 个变换弹簧每帧 ≤ 2ms（Release，UI 线程）；owner 分离或不可见时值跳到目标 | 优化调度器；必要时把部分动画移到 Composition |
| S0.7 | 弹簧过渡 | 两个 Switch 共享同一主题实例，同时切换时互不干扰；快速连续切换时速度保持；首帧不动画；Off 等级立即到终态 | 改为代码驱动的 MotionValue，并更新 ADR 0006 的集成方式 |
| S0.8 | Lucide | 7 种元素类型都能正确渲染；16 / 20 / 24 下与参考渲染的像素差在阈值内；记录包体积 | 调整生成器或描边缩放规则 |
| S0.9 | Gallery 骨架 | 侧边栏（分组、计数、折叠）、主题工具栏、示例页可用；NativeAOT 发布成功 | 简化 Gallery 结构 |
| S0.10 | 浮层与字体 | AtomUI `Popup` 上的 Arc 弹簧进出场正常；`ShadowFloating` 在窗口弹层与覆盖层两种宿主下都不被裁切；模态背景模糊方案的开销实测；从字段内触发器长成弹层的表面形变（MorphSelect / PhoneInput 的 P9）原型可行；Geist 与 Inter 注册不冲突；`TKCode.Arc.Fonts` 能生成包入口 | 调整浮层宿主或阴影边距；表面形变改为"触发器淡出 + 弹层从锚点长出"的降级方案；字体包改为在 Core 中注册 |

## 3. 结论记录

| 编号 | 日期 | 结论 | 影响的文档 |
|---|---|---|---|
| S0.1 | — | 待执行 | — |
| S0.2 | — | 待执行 | — |
| S0.3 | — | 待执行 | — |
| S0.4 | — | 待执行 | — |
| S0.5 | — | 待执行 | — |
| S0.6 | — | 待执行 | — |
| S0.7 | — | 待执行 | — |
| S0.8 | — | 待执行 | — |
| S0.9 | — | 待执行 | — |
| S0.10 | — | 待执行 | — |

## 4. 建议顺序

1. 工程骨架 + S0.1（工具链）。
2. S0.6（弹簧引擎，可脱离 UI 单测）与 S0.2（载体 Token）并行。
3. S0.3（命名）、S0.7（弹簧过渡），并编写 Button / Switch 规格。
4. 实现 Button、Switch；S0.8（Lucide）、S0.10（浮层与字体）。
5. Gallery 骨架 + S0.9；打包 + S0.4；AOT + S0.5。
6. 通过 [路线图](overview.md#4-阶段的进入与退出标准) 的退出标准评审后进入 P1。
